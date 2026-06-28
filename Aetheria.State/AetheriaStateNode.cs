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

    public CultMeshMutableStatePointer<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()
    {
        return MutableDocumentPointer<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeDaemonHealthDocument> Health()
    {
        return MutableDocumentPointer<AetheriaRuntimeDaemonHealthDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonHealth);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()
    {
        return MutableDocumentPointer<AetheriaRuntimeDaemonCommandBoundaryDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeVerseAuthorityPolicyDocument> VerseAuthorityPolicy()
    {
        return MutableDocumentPointer<AetheriaRuntimeVerseAuthorityPolicyDocument>(
            AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeDaemonFrameDocument> LatestFrame()
    {
        return MutableDocumentPointer<AetheriaRuntimeDaemonFrameDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()
    {
        return MutableDocumentPointer<AetheriaRuntimeDaemonSoaViewDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario()
    {
        return MutableDocumentPointer<AetheriaRuntimeStarbridgeScenarioDocument>(
            AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession()
    {
        return MutableDocumentPointer<AetheriaRuntimeStarbridgeSessionDocument>(
            AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSessionSummary()
    {
        return MutableDocumentPointer<AetheriaRuntimeStarbridgeSessionSummaryDocument>(
            AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary);
    }

    public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
    }

    public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameTuiSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
    }

    public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
    }

    public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorTuiSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
    }

    public CultMeshMutableStatePointer<EveSurfaceState> CatalogSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(new CultRecordKey(AetheriaCatalogSurfaceProjector.SurfaceKey));
    }

    public CultMeshMutableStatePointer<EveSurfaceState> OperationsSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(new CultRecordKey(AetheriaOperationsSurfaceProjector.SurfaceKey));
    }

    public CultMeshMutableStatePointer<EveSurfaceState> PlayerSettingsSurface()
    {
        return MutableDocumentPointer<EveSurfaceState>(new CultRecordKey(AetheriaPlayerSettingsSurfaceProjector.SurfaceKey));
    }

    public CultMeshMutableStatePointer<EveProviderAdvertisementState> ProviderAdvertisementSurface()
    {
        return MutableDocumentPointer<EveProviderAdvertisementState>(
            new CultRecordKey(AetheriaProviderAdvertisementProjector.AdvertisementKey));
    }

    public CultMeshMutableStatePointer<AetheriaRuntimeSession> RuntimeSession(string runtimeId)
    {
        return MutableDocumentPointer<AetheriaRuntimeSession>(RuntimeSessionKey(runtimeId));
    }

    public CultMeshMutableStatePointer<AetheriaPlayerSettings> PlayerSettings()
    {
        return MutableDocumentPointer<AetheriaPlayerSettings>(PlayerSettingsKey);
    }

    public CultMeshMutableStatePointer<AetheriaVerseHostSettings> VerseHostSettings()
    {
        return MutableDocumentPointer<AetheriaVerseHostSettings>(VerseHostSettingsKey);
    }

    public CultMeshMutableStatePointer<AetheriaEveCommandAcceptanceStatus> EveCommandAcceptanceStatus()
    {
        return MutableDocumentPointer<AetheriaEveCommandAcceptanceStatus>(EveCommandAcceptanceStatusKey);
    }

    public CultMeshMutableStatePointer<AetheriaWorldState> World()
    {
        return MutableDocumentPointer<AetheriaWorldState>(new CultRecordKey("global:aetheria.world_state.v1"));
    }

    public CultMeshMutableStatePointer<AetheriaMigrationLedger> MigrationLedger()
    {
        return MutableDocumentPointer<AetheriaMigrationLedger>(
            new CultRecordKey("global:aetheria.migration_ledger.v1"));
    }

    public CultMeshMutableStatePointer<AetheriaLegacyCatalogQuarantine> LegacyCatalogQuarantine()
    {
        return MutableDocumentPointer<AetheriaLegacyCatalogQuarantine>(
            new CultRecordKey("global:aetheria.legacy_catalog_quarantine.v1"));
    }

    public CultMeshMutableStatePointer<AetheriaItemDefinition> ItemDefinition(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaItemDefinition>(key);
    }

    public CultMeshMutableStatePointer<AetheriaItemDefinition> ItemDefinitionByLegacyId(string legacyId)
    {
        return ItemDefinition(AetheriaCatalogKeys.ItemDefinitionFromLegacyId(legacyId));
    }

    public CultMeshMutableStatePointer<AetheriaCorporation> Corporation(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaCorporation>(key);
    }

    public CultMeshMutableStatePointer<AetheriaCorporation> CorporationByLegacyId(string legacyId)
    {
        return Corporation(AetheriaCatalogKeys.CorporationFromLegacyId(legacyId));
    }

    public CultMeshMutableStatePointer<AetheriaNameFile> NameFile(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaNameFile>(key);
    }

    public CultMeshMutableStatePointer<AetheriaNameFile> NameFileByLegacyId(string legacyId)
    {
        return NameFile(AetheriaCatalogKeys.NameFileFromLegacyId(legacyId));
    }

    public CultMeshMutableStatePointer<AetheriaTradeValuePolicy> TradeValuePolicy()
    {
        return MutableDocumentPointer<AetheriaTradeValuePolicy>(
            new CultRecordKey(AetheriaTradeValuePolicy.RecordKey));
    }

    public CultMeshMutableStatePointer<AetheriaLoadoutTemplate> LoadoutTemplate(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaLoadoutTemplate>(key);
    }

    public CultMeshMutableStatePointer<AetheriaRunState> RunState(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaRunState>(key);
    }

    public CultMeshMutableStatePointer<AetheriaZoneState> ZoneState(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaZoneState>(key);
    }

    public CultMeshMutableStatePointer<AetheriaEntitySnapshot> EntitySnapshot(CultRecordKey key)
    {
        return MutableDocumentPointer<AetheriaEntitySnapshot>(key);
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

    private static CultRecordKey RuntimeSessionKey(string runtimeId)
    {
        return new CultRecordKey($"runtime:{runtimeId}:aetheria.runtime_session.v1");
    }

    private static CultRecordKey PlayerSettingsKey { get; } =
        new("global:aetheria.player_settings.v1");

    private static CultRecordKey VerseHostSettingsKey { get; } =
        new("global:aetheria.verse_host_settings.v1");

    private static CultRecordKey EveCommandAcceptanceStatusKey { get; } =
        new("global:aetheria.eve_command_acceptance_status.v1");

    private CultMeshMutableStatePointer<T> MutableDocumentPointer<T>(CultRecordKey key) where T : class
    {
        return CultMesh.MutableStatePointer(
            key.ToString(),
            _ => Database.GetAsync<T>(key),
            _ => Database.WatchRecord<T>(key)
                .Where(change => change.Document != null)
                .Select(change => change.Document!),
            async (_, value) => { await Database.PutAsync(key, value).ConfigureAwait(false); },
            sources:
            [
                CultMesh.ProjectionSource(key.ToString())
            ]);
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
