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

    public Task<AetheriaMigrationLedger?> GetMigrationLedgerAsync()
    {
        return Database.GetAsync<AetheriaMigrationLedger>(new CultRecordKey("global:aetheria.migration_ledger.v1"));
    }

    public Task<CultRecordHandle<AetheriaLegacyCatalogQuarantine>> PutLegacyCatalogQuarantineAsync(
        AetheriaLegacyCatalogQuarantine quarantine)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.legacy_catalog_quarantine.v1"), quarantine);
    }

    public Task<AetheriaLegacyCatalogQuarantine?> GetLegacyCatalogQuarantineAsync()
    {
        return Database.GetAsync<AetheriaLegacyCatalogQuarantine>(
            new CultRecordKey("global:aetheria.legacy_catalog_quarantine.v1"));
    }

    public Task<CultRecordHandle<AetheriaItemDefinition>> PutItemDefinitionAsync(
        CultRecordKey key,
        AetheriaItemDefinition item)
    {
        return Database.PutAsync(key, item);
    }

    public Task<CultRecordHandle<AetheriaItemDefinition>> PutLegacyItemDefinitionAsync(AetheriaItemDefinition item)
    {
        return PutItemDefinitionAsync(AetheriaCatalogKeys.ItemDefinitionFromLegacyId(item.LegacyId), item);
    }

    public Task<AetheriaItemDefinition?> GetItemDefinitionAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaItemDefinition>(key);
    }

    public Task<AetheriaItemDefinition?> GetItemDefinitionByLegacyIdAsync(string legacyId)
    {
        return GetItemDefinitionAsync(AetheriaCatalogKeys.ItemDefinitionFromLegacyId(legacyId));
    }

    public Task<CultRecordHandle<AetheriaCorporation>> PutCorporationAsync(CultRecordKey key, AetheriaCorporation corporation)
    {
        return Database.PutAsync(key, corporation);
    }

    public Task<CultRecordHandle<AetheriaCorporation>> PutLegacyCorporationAsync(AetheriaCorporation corporation)
    {
        return PutCorporationAsync(AetheriaCatalogKeys.CorporationFromLegacyId(corporation.LegacyId), corporation);
    }

    public Task<AetheriaCorporation?> GetCorporationAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaCorporation>(key);
    }

    public Task<AetheriaCorporation?> GetCorporationByLegacyIdAsync(string legacyId)
    {
        return GetCorporationAsync(AetheriaCatalogKeys.CorporationFromLegacyId(legacyId));
    }

    public Task<CultRecordHandle<AetheriaNameFile>> PutNameFileAsync(CultRecordKey key, AetheriaNameFile nameFile)
    {
        return Database.PutAsync(key, nameFile);
    }

    public Task<CultRecordHandle<AetheriaNameFile>> PutLegacyNameFileAsync(AetheriaNameFile nameFile)
    {
        return PutNameFileAsync(AetheriaCatalogKeys.NameFileFromLegacyId(nameFile.LegacyId), nameFile);
    }

    public Task<AetheriaNameFile?> GetNameFileAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaNameFile>(key);
    }

    public Task<AetheriaNameFile?> GetNameFileByLegacyIdAsync(string legacyId)
    {
        return GetNameFileAsync(AetheriaCatalogKeys.NameFileFromLegacyId(legacyId));
    }

    public AetheriaCatalogSnapshot ReadCatalogSnapshot()
    {
        return new AetheriaCatalogSnapshot(
            Cache.GetAll<AetheriaItemDefinition>(),
            Cache.GetAll<AetheriaCorporation>(),
            Cache.GetAll<AetheriaNameFile>());
    }

    public Task<CultRecordHandle<EveSurfaceState>> PutCatalogSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaCatalogSurfaceProjector.SurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetCatalogSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(new CultRecordKey(AetheriaCatalogSurfaceProjector.SurfaceKey));
    }

    public Task<CultRecordHandle<EveSurfaceState>> PutOperationsSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaOperationsSurfaceProjector.SurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetOperationsSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(new CultRecordKey(AetheriaOperationsSurfaceProjector.SurfaceKey));
    }

    public Task<CultRecordHandle<EveProviderAdvertisementState>> PutProviderAdvertisementAsync(
        EveProviderAdvertisementState advertisement)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaProviderAdvertisementProjector.AdvertisementKey), advertisement);
    }

    public Task<EveProviderAdvertisementState?> GetProviderAdvertisementAsync()
    {
        return Database.GetAsync<EveProviderAdvertisementState>(
            new CultRecordKey(AetheriaProviderAdvertisementProjector.AdvertisementKey));
    }

    public Task<CultRecordHandle<AetheriaPlayerSettings>> PutPlayerSettingsAsync(AetheriaPlayerSettings settings)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.player_settings.v1"), settings);
    }

    public Task<AetheriaPlayerSettings?> GetPlayerSettingsAsync()
    {
        return Database.GetAsync<AetheriaPlayerSettings>(new CultRecordKey("global:aetheria.player_settings.v1"));
    }

    public Task<CultRecordHandle<AetheriaLoadoutTemplate>> PutLoadoutTemplateAsync(
        CultRecordKey key,
        AetheriaLoadoutTemplate loadout)
    {
        return Database.PutAsync(key, loadout);
    }

    public Task<AetheriaLoadoutTemplate?> GetLoadoutTemplateAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaLoadoutTemplate>(key);
    }

    public Task<CultRecordHandle<AetheriaRunState>> PutRunStateAsync(CultRecordKey key, AetheriaRunState run)
    {
        return Database.PutAsync(key, run);
    }

    public Task<AetheriaRunState?> GetRunStateAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaRunState>(key);
    }

    public Task<CultRecordHandle<AetheriaZoneState>> PutZoneStateAsync(CultRecordKey key, AetheriaZoneState zone)
    {
        return Database.PutAsync(key, zone);
    }

    public Task<AetheriaZoneState?> GetZoneStateAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaZoneState>(key);
    }

    public Task<CultRecordHandle<AetheriaEntitySnapshot>> PutEntitySnapshotAsync(
        CultRecordKey key,
        AetheriaEntitySnapshot entity)
    {
        return Database.PutAsync(key, entity);
    }

    public Task<AetheriaEntitySnapshot?> GetEntitySnapshotAsync(CultRecordKey key)
    {
        return Database.GetAsync<AetheriaEntitySnapshot>(key);
    }

    public Task<CultRecordHandle<AetheriaRuntimeCommitDrainStatus>> PutRuntimeCommitDrainStatusAsync(
        AetheriaRuntimeCommitDrainStatus status)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.runtime_commit_drain_status.v1"), status);
    }

    public Task<AetheriaRuntimeCommitDrainStatus?> GetRuntimeCommitDrainStatusAsync()
    {
        return Database.GetAsync<AetheriaRuntimeCommitDrainStatus>(
            new CultRecordKey("global:aetheria.runtime_commit_drain_status.v1"));
    }

    public Task<CultRecordHandle<AetheriaEveCommandDrainStatus>> PutEveCommandDrainStatusAsync(
        AetheriaEveCommandDrainStatus status)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.eve_command_drain_status.v1"), status);
    }

    public Task<AetheriaEveCommandDrainStatus?> GetEveCommandDrainStatusAsync()
    {
        return Database.GetAsync<AetheriaEveCommandDrainStatus>(
            new CultRecordKey("global:aetheria.eve_command_drain_status.v1"));
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
