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
        using var node = await OpenReplicaNodeAsync(replicaStatePath, endpoint, runtimeId).ConfigureAwait(false);
        var shard = BuildReplicaShard(endpoint);
        var fetcher = new CultNetSchemaShardSnapshotFetcher();
        var snapshot = await fetcher.FetchAsync(shard).ConfigureAwait(false);
        var appliedSequence = await node.Database.ApplyShardSnapshotResponseAsync(shard, snapshot).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
        return appliedSequence;
    }

    public static async Task RunReplicaAsync(
        string replicaStatePath,
        string endpoint,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        string runtimeId = "aetheria-verse-replica")
    {
        using var node = await OpenReplicaNodeAsync(replicaStatePath, endpoint, runtimeId).ConfigureAwait(false);
        var shard = BuildReplicaShard(endpoint);
        var snapshotFetcher = new CultNetSchemaShardSnapshotFetcher();
        var logFetcher = new CultNetSchemaShardLogFetcher();

        var snapshot = await snapshotFetcher.FetchAsync(shard).ConfigureAwait(false);
        await node.Database.ApplyShardSnapshotResponseAsync(shard, snapshot).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);

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

    private static Task<CultMeshNode> OpenReplicaNodeAsync(
        string replicaStatePath,
        string endpoint,
        string runtimeId)
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
                    PullOnOpen = true,
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
