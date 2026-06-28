using System;
using System.IO;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Mesh;
using GameCult.Networking;
using R3;

namespace Aetheria.State;

public sealed class AetheriaStateNode : IAsyncDisposable, IDisposable
{
    private readonly CultMeshNode _node;
    private CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot>? _runtimeCatalog;

    private AetheriaStateNode(string statePath, string runtimeId, CultMeshNode node)
    {
        StatePath = statePath;
        RuntimeId = string.IsNullOrWhiteSpace(runtimeId)
            ? "aetheria-local"
            : runtimeId;
        _node = node;
    }

    public string StatePath { get; }

    public string RuntimeId { get; }

    public CultMeshNode MeshNode => _node;

    public CultCache Cache => _node.Cache;

    public CultNetDatabase Database => _node.Database;

    public static async Task<AetheriaStateNode> OpenAsync(
        string statePath,
        string runtimeId = "aetheria-local",
        bool startServer = false,
        bool enableDurableShardLogs = true)
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
                EnableDurableShardLogs = enableDurableShardLogs,
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

        return new AetheriaStateNode(statePath, runtimeId, node);
    }

    public CultMeshDocumentHandle<TDocument> Document<TDocument>(CultRecordKey key)
        where TDocument : class
    {
        return CultMesh.Document<TDocument>(
            key.ToString(),
            CultMesh.Verse("aetheria.local", RuntimeId),
            async _ => (await Database.GetAsync<TDocument>(key).ConfigureAwait(false))!,
            _ => Database.WatchRecord<TDocument>(key)
                .Where(change => change.Document != null)
                .Select(change => change.Document!),
            sources: new[]
            {
                CultMesh.ProjectionSource(key.ToString())
            },
            routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed state node document"));
    }

    public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> RuntimeCatalog()
    {
        return _runtimeCatalog ??= CultMesh.Document(
            "aetheria.catalog.runtime",
            CultMesh.Verse("aetheria.local", RuntimeId),
            _ => Task.FromResult(AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath)),
            _ => Database.WatchRecord<AetheriaRuntimeDaemonFrameDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
                .Where(change => change.Document != null)
                .Select(_ => AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath)),
            sources: new[]
            {
                CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                CultMesh.ProjectionSource(
                    "aetheria.catalog.runtime",
                    AetheriaRuntimeCatalogSnapshot.SchemaId,
                    "managed Aetheria runtime catalog document")
            },
            routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed catalog state"));
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

    public Task<CultRecordHandle<AetheriaTradeValuePolicy>> PutTradeValuePolicyAsync(
        AetheriaTradeValuePolicy policy)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaTradeValuePolicy.RecordKey), policy);
    }

    public Task<AetheriaTradeValuePolicy?> GetTradeValuePolicyAsync()
    {
        return Database.GetAsync<AetheriaTradeValuePolicy>(
            new CultRecordKey(AetheriaTradeValuePolicy.RecordKey));
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

    public Task<CultRecordHandle<EveSurfaceState>> PutPlayerSettingsSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaPlayerSettingsSurfaceProjector.SurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetPlayerSettingsSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(new CultRecordKey(AetheriaPlayerSettingsSurfaceProjector.SurfaceKey));
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

    public Task<CultRecordHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument>> PutDaemonProviderAdvertisementAsync(
        AetheriaRuntimeDaemonProviderAdvertisementDocument advertisement)
    {
        return Database.PutAsync(new CultRecordKey("daemon:aetheria.provider_advertisement.v1"), advertisement);
    }

    public Task<AetheriaRuntimeDaemonProviderAdvertisementDocument?> GetDaemonProviderAdvertisementAsync()
    {
        return Database.GetAsync<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
            new CultRecordKey("daemon:aetheria.provider_advertisement.v1"));
    }

    public Task<CultRecordHandle<AetheriaRuntimeDaemonHealthDocument>> PutDaemonHealthAsync(
        AetheriaRuntimeDaemonHealthDocument health)
    {
        return Database.PutAsync(new CultRecordKey("daemon:aetheria.health.v1"), health);
    }

    public Task<AetheriaRuntimeDaemonHealthDocument?> GetDaemonHealthAsync()
    {
        return Database.GetAsync<AetheriaRuntimeDaemonHealthDocument>(
            new CultRecordKey("daemon:aetheria.health.v1"));
    }

    public Task<CultRecordHandle<AetheriaRuntimeDaemonCommandBoundaryDocument>> PutDaemonCommandBoundaryAsync(
        AetheriaRuntimeDaemonCommandBoundaryDocument boundary)
    {
        return Database.PutAsync(new CultRecordKey("daemon:aetheria.command_boundary.v1"), boundary);
    }

    public Task<AetheriaRuntimeDaemonCommandBoundaryDocument?> GetDaemonCommandBoundaryAsync()
    {
        return Database.GetAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>(
            new CultRecordKey("daemon:aetheria.command_boundary.v1"));
    }

    public Task<CultRecordHandle<AetheriaRuntimeDaemonCommandDocument>> SubmitDaemonCommandAsync(
        AetheriaRuntimeDaemonCommandDocument command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        command.Schema = AetheriaRuntimeDaemonSchemas.Command;
        if (string.IsNullOrWhiteSpace(command.CommandId))
            command.CommandId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
            command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

        return Database.PutAsync(DaemonCommandKey(command.CommandId), command);
    }

    public IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> ReadObservedDaemonCommands()
    {
        return Cache
            .GetAll<AetheriaRuntimeDaemonCommandDocument>()
            .OrderBy(command => command.IssuedAtUtc ?? "", StringComparer.Ordinal)
            .ThenBy(command => command.CommandId ?? "", StringComparer.Ordinal)
            .ToArray();
    }

    public Task<CultRecordHandle<AetheriaRuntimeCommittedCommandFactDocument>> PutCommittedCommandFactAsync(
        AetheriaRuntimeCommittedCommandFactDocument fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        fact.Schema = AetheriaRuntimeDaemonSchemas.CommittedCommandFact;
        if (string.IsNullOrWhiteSpace(fact.FactId))
            fact.FactId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(fact.CommittedAtUtc))
            fact.CommittedAtUtc = DateTime.UtcNow.ToString("O");

        return Database.PutAsync(
            new CultRecordKey(AetheriaRuntimeCommittedCommandFactDocument.CreateRecordKey(fact.FactId)),
            fact);
    }

    public IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument> ReadCommittedCommandFacts()
    {
        return Cache
            .GetAll<AetheriaRuntimeCommittedCommandFactDocument>()
            .OrderBy(fact => fact.CommittedAtUtc ?? "", StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId ?? "", StringComparer.Ordinal)
            .ToArray();
    }

    public Task<CultRecordHandle<AetheriaRuntimeVerseAuthorityPolicyDocument>> PutVerseAuthorityPolicyAsync(
        AetheriaRuntimeVerseAuthorityPolicyDocument policy)
    {
        return Database.PutAsync(
            new CultRecordKey(AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey),
            policy);
    }

    public Task<AetheriaRuntimeVerseAuthorityPolicyDocument?> GetVerseAuthorityPolicyAsync()
    {
        return Database.GetAsync<AetheriaRuntimeVerseAuthorityPolicyDocument>(
            new CultRecordKey(AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey));
    }

    public IReadOnlyList<AetheriaRuntimeAuthorityLeaseDocument> ReadAuthorityLeases()
    {
        return Cache.GetAll<AetheriaRuntimeAuthorityLeaseDocument>().ToArray();
    }

    private static CultRecordKey DaemonCommandKey(string commandId)
    {
        return new CultRecordKey($"daemon:commands:{StableToken(commandId)}:gamecult.aetheria.daemon_command.v1");
    }

    public Task<CultRecordHandle<AetheriaRuntimeEveCommandDocument>> SubmitEveCommandAsync(
        AetheriaRuntimeEveCommandDocument command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        AetheriaRuntimeEveCommandClient.NormalizeDocument(command);
        command.Schema = AetheriaRuntimeEveCommandDocument.SchemaId;
        if (string.IsNullOrWhiteSpace(command.CommandId))
            command.CommandId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
            command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

        return Database.PutAsync(EveCommandKey(command.CommandId), command);
    }

    public IReadOnlyList<AetheriaRuntimeEveCommandDocument> ReadObservedEveCommands()
    {
        return Cache
            .GetAll<AetheriaRuntimeEveCommandDocument>()
            .Select(AetheriaRuntimeEveCommandClient.NormalizeDocument)
            .OrderBy(command => command.IssuedAtUtc ?? "", StringComparer.Ordinal)
            .ThenBy(command => command.CommandId ?? "", StringComparer.Ordinal)
            .ToArray();
    }

    private static CultRecordKey EveCommandKey(string commandId)
    {
        return new CultRecordKey($"eve:commands:{StableToken(commandId)}:gamecult.eve.command.v1");
    }

    public Task<CultRecordHandle<AetheriaRuntimeDaemonFrameDocument>> PutDaemonFrameAsync(
        AetheriaRuntimeDaemonFrameDocument frame)
    {
        return Database.PutAsync(new CultRecordKey("daemon:aetheria.frame.latest.v1"), frame);
    }

    public Task<AetheriaRuntimeDaemonFrameDocument?> GetDaemonFrameAsync()
    {
        return Database.GetAsync<AetheriaRuntimeDaemonFrameDocument>(
            new CultRecordKey("daemon:aetheria.frame.latest.v1"));
    }

    public Task<CultRecordHandle<AetheriaRuntimeDaemonSoaViewDocument>> PutDaemonSoaViewAsync(
        AetheriaRuntimeDaemonSoaViewDocument view)
    {
        return Database.PutAsync(new CultRecordKey("daemon:aetheria.soa_view.latest.v1"), view);
    }

    public Task<AetheriaRuntimeDaemonSoaViewDocument?> GetDaemonSoaViewAsync()
    {
        return Database.GetAsync<AetheriaRuntimeDaemonSoaViewDocument>(
            new CultRecordKey("daemon:aetheria.soa_view.latest.v1"));
    }

    public Task<CultRecordHandle<AetheriaRuntimeStarbridgeScenarioDocument>> PutStarbridgeScenarioAsync(
        AetheriaRuntimeStarbridgeScenarioDocument scenario)
    {
        return Database.PutAsync(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest, scenario);
    }

    public Task<AetheriaRuntimeStarbridgeScenarioDocument?> GetStarbridgeScenarioAsync()
    {
        return Database.GetAsync<AetheriaRuntimeStarbridgeScenarioDocument>(
            AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
    }

    public Task<CultRecordHandle<AetheriaRuntimeStarbridgeSessionDocument>> PutStarbridgeSessionAsync(
        AetheriaRuntimeStarbridgeSessionDocument session)
    {
        return Database.PutAsync(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest, session);
    }

    public Task<AetheriaRuntimeStarbridgeSessionDocument?> GetStarbridgeSessionAsync()
    {
        return Database.GetAsync<AetheriaRuntimeStarbridgeSessionDocument>(
            AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
    }

    public Task<CultRecordHandle<EveSurfaceState>> PutDaemonGameSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonGameSurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetDaemonGameSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(
            new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonGameSurfaceKey));
    }

    public Task<CultRecordHandle<EveSurfaceState>> PutDaemonGameTuiSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonGameTuiSurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetDaemonGameTuiSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(
            new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonGameTuiSurfaceKey));
    }

    public Task<CultRecordHandle<EveSurfaceState>> PutDaemonEditorSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonEditorSurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetDaemonEditorSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(
            new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonEditorSurfaceKey));
    }

    public Task<CultRecordHandle<EveSurfaceState>> PutDaemonEditorTuiSurfaceAsync(EveSurfaceState surface)
    {
        return Database.PutAsync(new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonEditorTuiSurfaceKey), surface);
    }

    public Task<EveSurfaceState?> GetDaemonEditorTuiSurfaceAsync()
    {
        return Database.GetAsync<EveSurfaceState>(
            new CultRecordKey(AetheriaProviderAdvertisementProjector.DaemonEditorTuiSurfaceKey));
    }

    public Task<CultRecordHandle<AetheriaRuntimeSession>> PutRuntimeSessionAsync(AetheriaRuntimeSession session)
    {
        return Database.PutAsync(new CultRecordKey($"runtime:{session.RuntimeId}:aetheria.runtime_session.v1"), session);
    }

    public Task<AetheriaRuntimeSession?> GetRuntimeSessionAsync(string runtimeId)
    {
        return Database.GetAsync<AetheriaRuntimeSession>(
            new CultRecordKey($"runtime:{runtimeId}:aetheria.runtime_session.v1"));
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

    public Task<CultRecordHandle<AetheriaVerseHostSettings>> PutVerseHostSettingsAsync(
        AetheriaVerseHostSettings settings)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.verse_host_settings.v1"), settings);
    }

    public Task<AetheriaVerseHostSettings?> GetVerseHostSettingsAsync()
    {
        return Database.GetAsync<AetheriaVerseHostSettings>(
            new CultRecordKey("global:aetheria.verse_host_settings.v1"));
    }

    public Task<CultRecordHandle<AetheriaEveCommandAcceptanceStatus>> PutEveCommandAcceptanceStatusAsync(
        AetheriaEveCommandAcceptanceStatus status)
    {
        return Database.PutAsync(new CultRecordKey("global:aetheria.eve_command_acceptance_status.v1"), status);
    }

    public Task<AetheriaEveCommandAcceptanceStatus?> GetEveCommandAcceptanceStatusAsync()
    {
        return Database.GetAsync<AetheriaEveCommandAcceptanceStatus>(
            new CultRecordKey("global:aetheria.eve_command_acceptance_status.v1"));
    }

    public Task FlushAsync(bool soft = false)
    {
        return _node.FlushAsync(soft);
    }

    private static string StableToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "empty";

        var chars = value
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var token = new string(chars).Trim('-').ToLowerInvariant();
        while (token.Contains("--", StringComparison.Ordinal))
            token = token.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(token) ? "empty" : token;
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
