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
        return await SyncRawSnapshotChunksAsync(node, endpoint, runtimeId).ConfigureAwait(false);
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
        var applied = await CultMesh.ApplySnapshotAsync(
                node,
                endpoint,
                CreateSnapshotOptions(runtimeId, schemaIds, recordKeys, connectTimeout, responseTimeout))
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
        return await CultMesh.FetchSnapshotAsync(
                endpoint,
                CreateSnapshotOptions("aetheria-verse-replica", schemaIds, recordKeys, connectTimeout, responseTimeout))
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<T>> FetchScopedDocumentsAsync<T>(
        string endpoint,
        IReadOnlyList<string>? schemaIds = null,
        IReadOnlyList<string>? recordKeys = null,
        TimeSpan? connectTimeout = null,
        TimeSpan? responseTimeout = null)
        where T : class
    {
        var documents = await CultMesh.FetchSnapshotDocumentsAsync<T>(
            endpoint,
            CreateSnapshotOptions("aetheria-verse-replica", schemaIds, recordKeys, connectTimeout, responseTimeout),
            AetheriaDocumentRegistry.CreateCultNetRegistry()).ConfigureAwait(false);
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

        await SyncRawSnapshotChunksAsync(node, endpoint, runtimeId).ConfigureAwait(false);

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

    private static async Task<long> SyncRawSnapshotChunksAsync(
        CultMeshNode node,
        string endpoint,
        string runtimeId)
    {
        var hot = await SyncHotPublicationsAsync(node, endpoint, runtimeId).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
        return hot;
    }

    private static async Task<long> SyncHotPublicationsAsync(
        CultMeshNode node,
        string endpoint,
        string runtimeId)
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
                endpoint,
                runtimeId,
                schemaIds: null,
                recordKeys: new[] { recordKey },
                responseTimeout: TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            sequence = Math.Max(sequence, SnapshotSequence(applied));
        }

        return sequence;
    }

    private static async Task<CultNetSnapshotResponseRawMessage> SyncScopedSnapshotAsync(
        CultMeshNode node,
        string endpoint,
        string runtimeId,
        IReadOnlyList<string>? schemaIds,
        IReadOnlyList<string>? recordKeys,
        TimeSpan responseTimeout)
    {
        var snapshot = await CultMesh.FetchSnapshotAsync(
                endpoint,
                CreateSnapshotOptions(runtimeId, schemaIds, recordKeys, null, responseTimeout))
            .ConfigureAwait(false);
        await node.Database.Documents.ApplyRawSnapshotResponseAsync(node.Cache, snapshot).ConfigureAwait(false);
        return snapshot;
    }

    private static long SnapshotSequence(CultNetSnapshotResponseRawMessage snapshot)
    {
        return snapshot.ShardLogSequence ?? 0L;
    }

    private static CultMeshSnapshotRequestOptions CreateSnapshotOptions(
        string runtimeId,
        IReadOnlyList<string>? schemaIds,
        IReadOnlyList<string>? recordKeys,
        TimeSpan? connectTimeout,
        TimeSpan? responseTimeout)
    {
        return new CultMeshSnapshotRequestOptions
        {
            SchemaIds = schemaIds,
            RecordKeys = recordKeys,
            ShardId = ReplicaShardId,
            ShardEpoch = 1,
            ConnectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5),
            ResponseTimeout = responseTimeout ?? TimeSpan.FromSeconds(5),
            MessageIdPrefix = "aetheria-replica",
            RudpRuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? "aetheria-verse-replica" : runtimeId,
            RudpMaxFragmentBytes = 1200
        };
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
