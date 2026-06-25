using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using GameCult.Networking;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseRecordKeys
    {
        public static CultRecordKey DaemonProviderAdvertisement { get; } =
            new CultRecordKey("daemon:aetheria.provider_advertisement.v1");

        public static CultRecordKey DaemonHealth { get; } =
            new CultRecordKey("daemon:aetheria.health.v1");

        public static CultRecordKey DaemonCommandBoundary { get; } =
            new CultRecordKey("daemon:aetheria.command_boundary.v1");

        public static CultRecordKey DaemonFrameLatest { get; } =
            new CultRecordKey("daemon:aetheria.frame.latest.v1");

        public static CultRecordKey DaemonSoaViewLatest { get; } =
            new CultRecordKey("daemon:aetheria.soa_view.latest.v1");

        public static CultRecordKey StarbridgeScenarioLatest { get; } =
            new CultRecordKey("starbridge:aetheria.scenario.latest.v1");

        public static CultRecordKey StarbridgeSessionLatest { get; } =
            new CultRecordKey("starbridge:aetheria.session.latest.v1");

        public static CultRecordKey DaemonGameSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game");

        public static CultRecordKey DaemonGameTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game.tui");

        public static CultRecordKey DaemonEditorSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor");

        public static CultRecordKey DaemonEditorTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor.tui");

        public static CultRecordKey DaemonCommand(string commandId)
        {
            return new CultRecordKey($"daemon:commands:{StableToken(commandId)}:gamecult.aetheria.daemon_command.v1");
        }

        public static CultRecordKey EveCommand(string commandId)
        {
            return new CultRecordKey($"eve:commands:{StableToken(commandId)}:gamecult.eve.command.v1");
        }

        public static string StableToken(string value)
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
    }

    public static class AetheriaRuntimeVerseContractRegistry
    {
        private static readonly Type[] RuntimeDocumentTypes =
        {
            typeof(AetheriaRuntimeDaemonProviderAdvertisementDocument),
            typeof(AetheriaRuntimeDaemonHealthDocument),
            typeof(AetheriaRuntimeDaemonCommandBoundaryDocument),
            typeof(AetheriaRuntimeDaemonFrameDocument),
            typeof(AetheriaRuntimeDaemonSoaViewDocument),
            typeof(AetheriaRuntimeObjectsViewportDocument),
            typeof(AetheriaRuntimeGravityViewportDocument),
            typeof(AetheriaRuntimeCurrentZoneDocument),
            typeof(AetheriaRuntimeCurrentEntityDocument),
            typeof(AetheriaRuntimeCurrentDockingDocument),
            typeof(AetheriaRuntimeZoneContactsDocument),
            typeof(AetheriaRuntimeStationRefitDocument),
            typeof(AetheriaRuntimeSectorMapDocument),
            typeof(AetheriaRuntimeZoneDetailsDocument),
            typeof(AetheriaRuntimeZoneRenderDocument),
            typeof(AetheriaRuntimeSelectedObjectDocument),
            typeof(AetheriaRuntimeInventoryDocument),
            typeof(AetheriaRuntimeStarbridgeScenarioDocument),
            typeof(AetheriaRuntimeStarbridgeSessionDocument),
            typeof(AetheriaRuntimeStarbridgeSessionSummaryDocument),
            typeof(AetheriaRuntimeDaemonCommandDocument),
            typeof(AetheriaRuntimeEveCommandDocument),
            typeof(EveSurfaceState)
        };

        public static CultDocumentRegistry CreateCultCacheRegistry()
        {
            var registry = new CultDocumentRegistry();
            foreach (var documentType in RuntimeDocumentTypes)
            {
                registry.GetRequired(documentType);
            }

            return registry;
        }

        public static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry? cacheRegistry = null)
        {
            var registry = cacheRegistry ?? CreateCultCacheRegistry();
            return new CultNetDocumentRegistry(
                registry,
                new[]
                {
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonHealthDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonFrameDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonSoaViewDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeObjectsViewportDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeGravityViewportDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeCurrentZoneDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeCurrentEntityDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeCurrentDockingDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeZoneContactsDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeStationRefitDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeSectorMapDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeZoneDetailsDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeZoneRenderDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeSelectedObjectDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeInventoryDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeStarbridgeScenarioDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeStarbridgeSessionDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeEveCommandDocument>(registry),
                    CultNetDocumentBinding.ForDocument<EveSurfaceState>(registry)
                });
        }
    }

    public sealed class AetheriaRuntimeVerseClient : IDisposable
    {
        public const string DefaultRuntimeId = "aetheria-verse-client";

        private readonly CultMeshNode _node;
        private bool _disposed;

        private AetheriaRuntimeVerseClient(string statePath, string runtimeId, CultMeshNode node)
        {
            StatePath = statePath;
            RuntimeId = runtimeId;
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string StatePath { get; }

        public string RuntimeId { get; }

        public CultMeshNode Node => _node;

        public CultCache Cache => _node.Cache;

        public CultNetDatabase Database => _node.Database;

        public static async Task<AetheriaRuntimeVerseClient> OpenAsync(
            string statePath,
            string runtimeId = DefaultRuntimeId,
            bool startServer = false,
            bool pullOnOpen = true)
        {
            if (string.IsNullOrWhiteSpace(statePath))
                throw new ArgumentException("State path must be non-empty.", nameof(statePath));

            var fullPath = Path.GetFullPath(statePath);
            var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId;
            var registry = AetheriaRuntimeVerseContractRegistry.CreateCultCacheRegistry();
            var node = await CultMesh.CreateNodeAsync(
                    fullPath,
                    new CultMeshNodeOptions
                    {
                        StartServer = startServer,
                        EnableDurableShardLogs = true,
                        CacheOptions = new CultCacheOpenOptions
                        {
                            Registry = registry,
                            PullOnOpen = pullOnOpen,
                            StoreFlushOnDispose = true,
                            UseDirectoryStore = true
                        },
                        DatabaseOptions = new CultNetDatabaseOptions
                        {
                            RuntimeId = effectiveRuntimeId,
                            DocumentRegistry = AetheriaRuntimeVerseContractRegistry.CreateCultNetRegistry(registry)
                        }
                    })
                .ConfigureAwait(false);

            return new AetheriaRuntimeVerseClient(fullPath, effectiveRuntimeId, node);
        }

        public static Func<string, string> CreateEveSurfaceStateRefResolver(
            string statePath,
            string runtimeId = DefaultRuntimeId)
        {
            return CreateEveSurfaceCultMeshStateRefResolver(statePath, runtimeId).AsFunc();
        }

        public static CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver(
            string statePath,
            string runtimeId = DefaultRuntimeId)
        {
            return AetheriaRuntimeStateReader.CreateEveSurfaceCultMeshStateRefResolver(
                statePath,
                string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId);
        }

        public Task<AetheriaRuntimeDaemonProviderAdvertisementDocument?> GetProviderAdvertisementAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public Task<AetheriaRuntimeDaemonHealthDocument?> GetHealthAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public Task<AetheriaRuntimeDaemonCommandBoundaryDocument?> GetCommandBoundaryAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public Task<AetheriaRuntimeDaemonFrameDocument?> GetLatestFrameAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public Task<AetheriaRuntimeDaemonSoaViewDocument?> GetLatestSoaViewAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public Task<AetheriaRuntimeStarbridgeScenarioDocument?> GetStarbridgeScenarioAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
        }

        public Task<AetheriaRuntimeStarbridgeSessionDocument?> GetStarbridgeSessionAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
        }

        public AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog()
        {
            ThrowIfDisposed();
            return AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath);
        }

        public Task<AetheriaRuntimePlayerSettingsSnapshot?> GetPlayerSettingsAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(AetheriaRuntimeCatalogStore.ReadPlayerSettings(StatePath));
        }

        public Task<AetheriaRuntimeVerseHostSettingsSnapshot?> GetVerseHostSettingsAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(AetheriaRuntimeCatalogStore.ReadVerseHostSettings(StatePath));
        }

        public Task<IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>> GetLoadoutTemplatesAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(StatePath));
        }

        public async Task<AetheriaRuntimeObservedDaemonState?> GetObservedDaemonStateAsync()
        {
            ThrowIfDisposed();

            var frame = await GetLatestFrameAsync().ConfigureAwait(false);
            if (frame == null)
                return null;

            var soaView = await GetLatestSoaViewAsync().ConfigureAwait(false);
            if (soaView == null ||
                !string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
            {
                soaView = null;
            }

            return new AetheriaRuntimeObservedDaemonState(
                frame,
                soaView,
                AetheriaRuntimeDaemonFrameStore.GetFramePath(StatePath),
                AetheriaRuntimeDaemonSoaViewStore.GetViewPath(StatePath));
        }

        public Func<string, string> CreateEveSurfaceStateRefResolver()
        {
            return CreateEveSurfaceCultMeshStateRefResolver().AsFunc();
        }

        public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()
        {
            ThrowIfDisposed();

            var frame = GetLatestFrameAsync().GetAwaiter().GetResult();
            var health = GetHealthAsync().GetAwaiter().GetResult();
            var commandBoundary = GetCommandBoundaryAsync().GetAwaiter().GetResult();
            AetheriaRuntimeCatalogSnapshot? catalog = null;

            var daemonRefs = CultMesh.StateRefResolver(
                "aetheria.daemon.refs",
                stateRef =>
                    stateRef.StartsWith(AetheriaRuntimeDaemonStateRefs.Prefix + "/", StringComparison.Ordinal) &&
                    AetheriaRuntimeStateReader.TryResolveDaemonStateRef(
                        frame,
                        health,
                        commandBoundary,
                        stateRef,
                        out var daemonValue)
                        ? daemonValue
                        : "",
                new[]
                {
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString())
                });

            var itemStatRefs = CultMesh.StateRefResolver(
                "aetheria.daemon.item_stats.refs",
                stateRef =>
                {
                    if (!stateRef.StartsWith(AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix + "/", StringComparison.Ordinal))
                        return "";

                    catalog ??= OpenRuntimeCatalog();
                    return AetheriaRuntimeStateReader.TryResolveDaemonItemStatRef(frame, catalog, stateRef, out var itemValue)
                        ? itemValue
                        : "";
                },
                new[]
                {
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()),
                    CultMesh.ProjectionSource("catalog:aetheria.runtime")
                });

            return daemonRefs.Or(itemStatRefs);
        }

        public async Task<AetheriaRuntimeDaemonFrameDocument?> GetLatestAuthoritativeRunFrameAsync()
        {
            ThrowIfDisposed();

            var frame = await GetLatestFrameAsync().ConfigureAwait(false);
            if (frame == null ||
                !frame.IsAuthoritative ||
                frame.Run == null ||
                frame.Run.Zones == null ||
                frame.Run.Zones.Count == 0)
            {
                return null;
            }

            return frame;
        }

        public Task<EveSurfaceState?> GetDaemonGameSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public Task<EveSurfaceState?> GetDaemonGameTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public Task<EveSurfaceState?> GetDaemonEditorSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public Task<EveSurfaceState?> GetDaemonEditorTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonHealthDocument> Health()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonFrameDocument> LatestFrame()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameTuiSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorTuiSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonProviderAdvertisementDocument>>
            WatchProviderAdvertisements()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonHealthDocument>> WatchHealth()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonCommandBoundaryDocument>>
            WatchCommandBoundary()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonFrameDocument>> WatchLatestFrames()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonSoaViewDocument>> WatchLatestSoaViews()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeStarbridgeScenarioDocument>> WatchStarbridgeScenarios()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeStarbridgeSessionDocument>> WatchStarbridgeSessions()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonGameSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonGameTuiSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonEditorSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonEditorTuiSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        internal async Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(
            AetheriaRuntimeDaemonCommandDocument command,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Schema = AetheriaRuntimeDaemonSchemas.Command;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.ClientId))
                command.ClientId = RuntimeId;
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.DaemonCommand(command.CommandId), command)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);

            return AetheriaRuntimeDaemonOperationClient.ToEnvelope(command);
        }

        internal async Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(
            AetheriaRuntimeEveCommandDocument command,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));

            AetheriaRuntimeEveCommandClient.NormalizeDocument(command);
            command.Schema = AetheriaRuntimeEveCommandDocument.SchemaId;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.EveCommand(command.CommandId), command)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);

            return AetheriaRuntimeEveCommandClient.ToEnvelope(command);
        }

        public async Task PutStarbridgeScenarioAsync(
            AetheriaRuntimeStarbridgeScenarioDocument scenario,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            scenario.Schema = AetheriaRuntimeDaemonSchemas.StarbridgeScenario;
            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest, scenario)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);
        }

        public async Task PutStarbridgeSessionAsync(
            AetheriaRuntimeStarbridgeSessionDocument session,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (session == null) throw new ArgumentNullException(nameof(session));

            session.Schema = AetheriaRuntimeDaemonSchemas.StarbridgeSession;
            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest, session)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitInputSettingsCommandAsync(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string clientId,
            bool flush = true)
        {
            ThrowIfDisposed();
            return SubmitEveCommandAsync(
                AetheriaRuntimeEveCommandClient.ToDocument(
                    AetheriaRuntimeEveCommandClient.CreateInputSettingsCommand(command, body, clientId)),
                flush);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitLoadoutTemplateCommandAsync(
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string clientId,
            bool flush = true)
        {
            ThrowIfDisposed();
            return SubmitEveCommandAsync(
                AetheriaRuntimeEveCommandClient.ToDocument(
                    AetheriaRuntimeEveCommandClient.CreateLoadoutTemplateCommand(loadoutTemplate, clientId)),
                flush);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitKnownSurfaceCommandAsync(
            EveSurfaceCommandRequest request,
            string clientId,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (!AetheriaRuntimeEveCommandClient.TryCreateKnownSurfaceCommand(request, out var envelope))
            {
                throw new InvalidOperationException(
                    $"Unknown Aetheria Eve surface command: {request?.ProviderId}/{request?.SurfaceId}/{request?.Command}");
            }

            var document = AetheriaRuntimeEveCommandClient.ToDocument(envelope!);
            if (!string.IsNullOrWhiteSpace(clientId))
                document.ClientId = clientId;
            return SubmitEveCommandAsync(document, flush);
        }

        public Task FlushAsync(bool soft = false)
        {
            ThrowIfDisposed();
            return _node.FlushAsync(soft);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _node.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AetheriaRuntimeVerseClient));
        }

        private CultMeshMutableStatePointer<T> Document<T>(CultRecordKey key) where T : class
        {
            return CultMesh.MutableStatePointer(
                key.ToString(),
                _ => Database.GetAsync<T>(key),
                _ => Database.WatchRecord<T>(key)
                    .Where(change => change.Document != null)
                    .Select(change => change.Document!),
                async (_, value) => { await Database.PutAsync(key, value).ConfigureAwait(false); },
                sources: new[]
                {
                    CultMesh.ProjectionSource(key.ToString())
                });
        }
    }
}
