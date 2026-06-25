using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Mesh;
using GameCult.Networking;

namespace Aetheria.State;

public static class AetheriaVerseReplica
{
    private const string ReplicaShardId = "primary";
    private const string ReplicaOwnerRuntimeId = "aetheria-remote";

    public static async Task<long> SyncSnapshotAsync(
        string replicaStatePath,
        string endpoint,
        string runtimeId = "aetheria-verse-replica")
    {
        using var node = await OpenReplicaNodeAsync(
            replicaStatePath,
            endpoint,
            runtimeId,
            pullOnOpen: false).ConfigureAwait(false);
        return await SyncRawSnapshotChunksAsync(node, endpoint).ConfigureAwait(false);
    }

    public static async Task<int> SyncScopedSnapshotAsync(
        string replicaStatePath,
        string endpoint,
        string runtimeId = "aetheria-verse-replica",
        IReadOnlyList<string>? schemaIds = null,
        IReadOnlyList<string>? recordKeys = null,
        TimeSpan? connectTimeout = null,
        TimeSpan? responseTimeout = null)
    {
        using var node = await OpenReplicaNodeAsync(
            replicaStatePath,
            endpoint,
            runtimeId,
            pullOnOpen: false).ConfigureAwait(false);
        using var client = await ConnectedSnapshotClient.ConnectAsync(
            endpoint,
            connectTimeout ?? TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        var snapshot = await client.RequestAsync(
            schemaIds,
            recordKeys,
            responseTimeout ?? TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        var applied = await node.Database.Documents
            .ApplyRawSnapshotResponseAsync(node.Cache, snapshot)
            .ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
        return applied.Count;
    }

    public static async Task<CultNetSnapshotResponseRawMessage> FetchScopedSnapshotAsync(
        string endpoint,
        IReadOnlyList<string>? schemaIds = null,
        IReadOnlyList<string>? recordKeys = null,
        TimeSpan? connectTimeout = null,
        TimeSpan? responseTimeout = null)
    {
        using var client = await ConnectedSnapshotClient.ConnectAsync(
            endpoint,
            connectTimeout ?? TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        return await client.RequestAsync(
            schemaIds,
            recordKeys,
            responseTimeout ?? TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<T>> FetchScopedDocumentsAsync<T>(
        string endpoint,
        IReadOnlyList<string>? schemaIds = null,
        IReadOnlyList<string>? recordKeys = null,
        TimeSpan? connectTimeout = null,
        TimeSpan? responseTimeout = null)
        where T : class
    {
        var snapshot = await FetchScopedSnapshotAsync(
            endpoint,
            schemaIds,
            recordKeys,
            connectTimeout,
            responseTimeout).ConfigureAwait(false);
        var registry = AetheriaDocumentRegistry.CreateCultNetRegistry();
        var documents = new List<T>(snapshot.Documents.Length);
        foreach (var record in snapshot.Documents)
        {
            if (record == null)
                continue;

            var binding = registry.GetBySchemaId(record.SchemaId);
            if (binding == null || binding.DocumentType != typeof(T))
                continue;

            if (!string.Equals(record.PayloadEncoding, "messagepack", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"CultNet raw document payloadEncoding must be \"messagepack\", not \"{record.PayloadEncoding}\".");

            documents.Add((T)binding.PayloadDeserializer(record.Payload));
        }

        return documents;
    }

    public static async Task RunReplicaAsync(
        string replicaStatePath,
        string endpoint,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        string runtimeId = "aetheria-verse-replica")
    {
        using var node = await OpenReplicaNodeAsync(
            replicaStatePath,
            endpoint,
            runtimeId,
            pullOnOpen: false).ConfigureAwait(false);
        var snapshotFetcher = new CultNetSchemaShardSnapshotFetcher();
        var logFetcher = new CultNetSchemaShardLogFetcher();

        await SyncRawSnapshotChunksAsync(node, endpoint).ConfigureAwait(false);

        using var replicator = new CultNetShardReplicator(
            node.Database,
            new CultNetShardReplicatorOptions
            {
                PollInterval = pollInterval,
                Fetcher = logFetcher,
                SnapshotFetcher = snapshotFetcher
            });
        replicator.Start();

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await node.FlushAsync().ConfigureAwait(false);
        }
    }

    private static CultNetShardDescriptor BuildReplicaShard(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Replica endpoint must be non-empty.", nameof(endpoint));

        return new CultNetShardDescriptor(
            ReplicaShardId,
            ReplicaOwnerRuntimeId,
            epoch: 1,
            isPrimary: false,
            primaryEndpoints: new[] { endpoint.Trim() });
    }

    private static async Task<long> SyncRawSnapshotChunksAsync(CultMeshNode node, string endpoint)
    {
        using var client = await ConnectedSnapshotClient.ConnectAsync(
            endpoint,
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        var hot = await SyncHotPublicationsAsync(node, client).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
        return hot;
    }

    private static async Task<long> SyncHotPublicationsAsync(CultMeshNode node, ConnectedSnapshotClient client)
    {
        var recordKeys = new[]
        {
            AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement.ToString(),
            AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString(),
            AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(),
            AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
            AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey,
            "daemon:aetheria.starbridge.session.latest.v1"
        };

        var sequence = 0L;
        foreach (var recordKey in recordKeys)
        {
            var applied = await SyncScopedSnapshotAsync(
                node,
                client,
                schemaIds: null,
                recordKeys: new[] { recordKey },
                responseTimeout: TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            sequence = Math.Max(sequence, SnapshotSequence(applied));
        }

        return sequence;
    }

    private static async Task<CultNetSnapshotResponseRawMessage> SyncScopedSnapshotAsync(
        CultMeshNode node,
        ConnectedSnapshotClient client,
        IReadOnlyList<string>? schemaIds,
        IReadOnlyList<string>? recordKeys,
        TimeSpan responseTimeout)
    {
        var snapshot = await client.RequestAsync(schemaIds, recordKeys, responseTimeout).ConfigureAwait(false);
        await node.Database.Documents.ApplyRawSnapshotResponseAsync(node.Cache, snapshot).ConfigureAwait(false);
        return snapshot;
    }

    private static long SnapshotSequence(CultNetSnapshotResponseRawMessage snapshot)
    {
        return snapshot.ShardLogSequence ?? 0L;
    }

    private sealed class ConnectedSnapshotClient : IDisposable
    {
        private const uint RtsConnectionId = 0x43554c54;
        private readonly ICultNetSchemaClient _client;
        private readonly string _endpoint;
        private readonly Dictionary<string, TaskCompletionSource<CultNetSnapshotResponseRawMessage>> _pending =
            new(StringComparer.Ordinal);
        private readonly object _pendingLock = new();
        private bool _disposed;

        private ConnectedSnapshotClient(ICultNetSchemaClient client, string endpoint)
        {
            _client = client;
            _endpoint = endpoint;
            _client.OnCultNet<CultNetSnapshotResponseRawMessage>(OnSnapshotResponse);
            _client.OnCultNet<CultNetErrorMessage>(OnError);
        }

        public static async Task<ConnectedSnapshotClient> ConnectAsync(string endpoint, TimeSpan connectTimeout)
        {
            var (host, port) = ParseEndpoint(endpoint);
            var client = string.Equals(new Uri(endpoint).Scheme, "rudp", StringComparison.OrdinalIgnoreCase)
                ? CultNetSchemaClients.CreateRudp(
                    runtimeId: "aetheria-verse-replica",
                    connectionId: RtsConnectionId,
                    maxFragmentBytes: 1200)
                : CultNetSchemaClients.CreateForEndpoint(endpoint);
            client.Connect(host, port);
            var connected = new ConnectedSnapshotClient(client, endpoint);
            await connected.WaitForConnectionAsync(connectTimeout).ConfigureAwait(false);
            return connected;
        }

        public async Task<CultNetSnapshotResponseRawMessage> RequestAsync(
            IReadOnlyList<string>? schemaIds,
            IReadOnlyList<string>? recordKeys,
            TimeSpan responseTimeout)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConnectedSnapshotClient));

            var messageId = $"aetheria-replica:{Guid.NewGuid():N}";
            var completion = new TaskCompletionSource<CultNetSnapshotResponseRawMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pendingLock)
            {
                _pending.Add(messageId, completion);
            }

            _client.SendCultNet(new CultNetSnapshotRequestMessage
            {
                MessageId = messageId,
                SchemaIds = CleanFilter(schemaIds),
                RecordKeys = CleanFilter(recordKeys),
                ShardId = ReplicaShardId,
                ShardEpoch = 1
            });

            var timeoutTask = Task.Delay(responseTimeout);
            var completed = await Task.WhenAny(completion.Task, timeoutTask).ConfigureAwait(false);
            if (completed != completion.Task)
            {
                lock (_pendingLock)
                {
                    _pending.Remove(messageId);
                }

                throw new TimeoutException(
                    $"Timed out waiting for shard snapshot response from {_endpoint} " +
                    $"for schemas [{string.Join(", ", CleanFilter(schemaIds) ?? Array.Empty<string>())}] " +
                    $"and records [{string.Join(", ", CleanFilter(recordKeys) ?? Array.Empty<string>())}].");
            }

            return await completion.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            _disposed = true;
            _client.Dispose();
            lock (_pendingLock)
            {
                foreach (var pending in _pending.Values)
                {
                    pending.TrySetCanceled();
                }

                _pending.Clear();
            }
        }

        private async Task WaitForConnectionAsync(TimeSpan connectTimeout)
        {
            var deadline = DateTimeOffset.UtcNow + connectTimeout;
            while (!_client.Connected)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Timed out connecting to shard primary endpoint {_endpoint}.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(75)).ConfigureAwait(false);
        }

        private void OnSnapshotResponse(CultNetSnapshotResponseRawMessage response)
        {
            if (string.IsNullOrWhiteSpace(response.MessageId))
                return;

            TaskCompletionSource<CultNetSnapshotResponseRawMessage>? completion;
            lock (_pendingLock)
            {
                if (!_pending.Remove(response.MessageId, out completion))
                    return;
            }

            completion.TrySetResult(response);
        }

        private void OnError(CultNetErrorMessage error)
        {
            List<TaskCompletionSource<CultNetSnapshotResponseRawMessage>> pending;
            lock (_pendingLock)
            {
                pending = _pending.Values.ToList();
                _pending.Clear();
            }

            var exception = new InvalidOperationException(error.Error);
            foreach (var completion in pending)
            {
                completion.TrySetException(exception);
            }
        }

        private static string[]? CleanFilter(IReadOnlyList<string>? values)
        {
            if (values is not { Count: > 0 })
                return null;

            var filtered = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return filtered.Length == 0 ? null : filtered;
        }

        private static (string Host, int Port) ParseEndpoint(string endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                throw new FormatException($"CultNet endpoint '{endpoint}' must be an absolute URI.");
            }

            if (!string.Equals(uri.Scheme, "rudp", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "cultnet", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"CultNet endpoint '{endpoint}' must use cultnet://host:port or rudp://host:port.");
            }

            if (uri.Port <= 0 || uri.Port > 65535)
            {
                throw new FormatException($"CultNet endpoint '{endpoint}' must include a valid port.");
            }

            return (uri.Host, uri.Port);
        }
    }

    private static Task<CultMeshNode> OpenReplicaNodeAsync(
        string replicaStatePath,
        string endpoint,
        string runtimeId,
        bool pullOnOpen = true)
    {
        if (string.IsNullOrWhiteSpace(replicaStatePath))
            throw new ArgumentException("Replica state path must be non-empty.", nameof(replicaStatePath));

        var shard = BuildReplicaShard(endpoint);
        var cacheRegistry = AetheriaDocumentRegistry.CreateCultCacheRegistry();
        return CultMesh.CreateNodeAsync(
            replicaStatePath,
            new CultMeshNodeOptions
            {
                StartServer = false,
                EnableDurableShardLogs = true,
                CacheOptions = new CultCacheOpenOptions
                {
                    Registry = cacheRegistry,
                    PullOnOpen = pullOnOpen,
                    StoreFlushOnDispose = true,
                    UseDirectoryStore = true
                },
                DatabaseOptions = new CultNetDatabaseOptions
                {
                    RuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? "aetheria-verse-replica" : runtimeId,
                    Shards = new[] { shard },
                    DocumentRegistry = AetheriaDocumentRegistry.CreateCultNetRegistry(cacheRegistry)
                }
            });
    }
}
