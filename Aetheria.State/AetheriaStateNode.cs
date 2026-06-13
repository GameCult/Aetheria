using System;
using System.IO;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Mesh;
using GameCult.Networking;

namespace Aetheria.State;

public sealed class AetheriaStateNode : IAsyncDisposable, IDisposable
{
    private readonly CultMeshNode _node;

    private AetheriaStateNode(string statePath, CultMeshNode node)
    {
        StatePath = statePath;
        _node = node;
    }

    public string StatePath { get; }

    public CultCache Cache => _node.Cache;

    public CultNetDatabase Database => _node.Database;

    public static async Task<AetheriaStateNode> OpenAsync(
        string statePath,
        string runtimeId = "aetheria-local",
        bool startServer = false)
    {
        if (string.IsNullOrWhiteSpace(statePath))
        {
            throw new ArgumentException("State path must be non-empty.", nameof(statePath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(statePath)) ?? ".");

        var cacheRegistry = AetheriaDocumentRegistry.CreateCultCacheRegistry();
        var node = await CultMesh.CreateNodeAsync(
            statePath,
            new CultMeshNodeOptions
            {
                StartServer = startServer,
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
                    RuntimeId = runtimeId,
                    DocumentRegistry = AetheriaDocumentRegistry.CreateCultNetRegistry(cacheRegistry)
                }
            }).ConfigureAwait(false);

        return new AetheriaStateNode(statePath, node);
    }

    public Task<CultRecordHandle<AetheriaWorldState>> PutWorldAsync(AetheriaWorldState world)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.world_state.v1"), world);
    }

    public Task<AetheriaWorldState?> GetWorldAsync()
    {
        return Database.GetAsync<AetheriaWorldState>(new CultRecordKey("global:aetheria.world_state.v1"));
    }

    public Task<CultRecordHandle<AetheriaMigrationLedger>> PutMigrationLedgerAsync(AetheriaMigrationLedger ledger)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.migration_ledger.v1"), ledger);
    }

    public Task<CultRecordHandle<AetheriaItemDefinition>> PutItemDefinitionAsync(
        CultRecordKey key,
        AetheriaItemDefinition item)
    {
        return Database.PutAsync(key, item);
    }

    public Task<AetheriaItemDefinition?> GetItemDefinitionAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaItemDefinition>(key);
    }

    public Task<CultRecordHandle<AetheriaPlayerSettings>> PutPlayerSettingsAsync(AetheriaPlayerSettings settings)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.player_settings.v1"), settings);
    }

    public Task<AetheriaPlayerSettings?> GetPlayerSettingsAsync()
    {
        return Database.GetAsync<AetheriaPlayerSettings>(new CultRecordKey("global:aetheria.player_settings.v1"));
    }

    public Task<CultRecordHandle<AetheriaSavedRun>> PutSavedRunAsync(CultRecordKey key, AetheriaSavedRun run)
    {
        return Database.PutAsync(key, run);
    }

    public Task<AetheriaSavedRun?> GetSavedRunAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaSavedRun>(key);
    }

    public Task FlushAsync(bool soft = false)
    {
        return _node.FlushAsync(soft);
    }

    public void Dispose()
    {
        _node.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync().ConfigureAwait(false);
        Dispose();
    }
}
