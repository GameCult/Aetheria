using System;
using System.Collections.Generic;
using System.Globalization;
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

        public static CultRecordKey VerseAuthorityPolicy { get; } =
            new CultRecordKey(AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey);

        public static CultRecordKey DaemonFrameLatest { get; } =
            new CultRecordKey("daemon:aetheria.frame.latest.v1");

        public static CultRecordKey DaemonSoaViewLatest { get; } =
            new CultRecordKey("daemon:aetheria.soa_view.latest.v1");

        public static CultRecordKey StarbridgeScenarioLatest { get; } =
            new CultRecordKey("starbridge:aetheria.scenario.latest.v1");

        public static CultRecordKey StarbridgeSessionLatest { get; } =
            new CultRecordKey("starbridge:aetheria.session.latest.v1");

        public static CultRecordKey StarbridgePlayerSeat(string seatId)
        {
            return new CultRecordKey(AetheriaRuntimeStarbridgePlayerSeatDocument.RecordKey(seatId));
        }

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
            typeof(AetheriaRuntimeVerseAuthorityPolicyDocument),
            typeof(AetheriaRuntimeDaemonFrameDocument),
            typeof(AetheriaRuntimeDaemonSoaViewDocument),
            typeof(AetheriaRuntimeAssetManifestDocument),
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
            typeof(AetheriaRuntimeStarbridgePlayerSeatDocument),
            typeof(AetheriaRuntimePlayerSettingsDocument),
            typeof(AetheriaRuntimeVerseHostSettingsDocument),
            typeof(AetheriaRuntimeDaemonCommandDocument),
            typeof(AetheriaRuntimeEveCommandDocument),
            typeof(EveSurfaceState)
        };

        public static CultDocumentRegistry CreateCultCacheRegistry()
        {
            return CultMesh.CreateCultCacheDocumentRegistry(RuntimeDocumentTypes);
        }

        public static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry? cacheRegistry = null)
        {
            var registry = cacheRegistry ?? CreateCultCacheRegistry();
            return CultMesh.CreateCultNetDocumentRegistry(RuntimeDocumentTypes, registry);
        }
    }

    public sealed class AetheriaRuntimeVerseClient : IDisposable
    {
        public const string DefaultRuntimeId = "aetheria-verse-client";

        private readonly CultMeshNode _node;
        private AetheriaClientState? _aetheriaState;
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
            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                statePath,
                string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId,
                AetheriaRuntimeCatalogStore.OpenReadOnly);
        }

        public AetheriaClientState Aetheria()
        {
            ThrowIfDisposed();
            return _aetheriaState ??= CreateAetheriaStateFacade();
        }

        public async Task<AetheriaRuntimeObservedDaemonState?> GetObservedDaemonStateAsync()
        {
            ThrowIfDisposed();

            var state = Aetheria();
            var frame = await state.Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
            if (frame == null)
                return null;

            var soaView = await state.Daemon.LatestSoaView.LatestAsync().ConfigureAwait(false);
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
            return CreateEveSurfaceCultMeshStateRefResolverAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<CultMeshStateRefResolver> CreateEveSurfaceCultMeshStateRefResolverAsync()
        {
            ThrowIfDisposed();

            var state = Aetheria();
            var frameTask = state.Daemon.LatestFrame.LatestAsync();
            var healthTask = state.Daemon.Health.LatestAsync();
            var commandBoundaryTask = state.Daemon.CommandBoundary.LatestAsync();

            await Task.WhenAll(frameTask, healthTask, commandBoundaryTask).ConfigureAwait(false);

            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                frameTask.Result,
                healthTask.Result,
                commandBoundaryTask.Result,
                () => state.Catalog.Latest());
        }

        public async Task<AetheriaRuntimeDaemonFrameDocument?> GetLatestAuthoritativeRunFrameAsync()
        {
            ThrowIfDisposed();

            var frame = await Aetheria().Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
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

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonHealthDocument> Health()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeVerseAuthorityPolicyDocument> VerseAuthorityPolicy()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonFrameDocument> LatestFrame()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgePlayerSeatDocument> StarbridgePlayerSeat(
            string seatId)
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId));
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameTuiSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorTuiSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(
            CultRecordKey key,
            string? documentId = null)
            where TDocument : class
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key.Value))
                throw new ArgumentException("Value must be non-empty.", nameof(key));
            var descriptor = CultDocumentRegistry.Shared.GetRequired<TDocument>();
            return CultMesh.Document<TDocument>(
                Database,
                key,
                CultMesh.Verse("aetheria.local", RuntimeId),
                documentId,
                new[]
                {
                    CultMesh.ProjectionSource(key.ToString(), descriptor.SchemaId, "Aetheria Verse database document")
                },
                new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria Verse database"));
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveDocumentAsync<TDocument>(
            CultRecordKey key,
            CultMeshReactiveDocumentOptions? options = null,
            string? documentId = null)
            where TDocument : class
        {
            return Document<TDocument>(key, documentId).ReactiveAsync(options);
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

        public Observable<CultNetDatabaseChange<AetheriaRuntimeVerseAuthorityPolicyDocument>>
            WatchVerseAuthorityPolicies()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy);
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

        public Observable<CultNetDatabaseChange<AetheriaRuntimeStarbridgePlayerSeatDocument>>
            WatchStarbridgePlayerSeat(string seatId)
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId));
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

        public async Task PutStarbridgePlayerSeatAsync(
            AetheriaRuntimeStarbridgePlayerSeatDocument seat,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (seat == null) throw new ArgumentNullException(nameof(seat));

            seat.Schema = AetheriaRuntimeDaemonSchemas.StarbridgePlayerSeat;
            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seat.SeatId), seat)
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

        private AetheriaClientState CreateAetheriaStateFacade()
        {
            var frameChanges = WatchLatestFrames()
                .Where(change => change.Document != null)
                .Select(change => change.Document!);
            var catalogDocument = CatalogDocument(
                "aetheria.catalog.runtime",
                () => Task.FromResult(ReadRuntimeCatalogSnapshot()),
                AetheriaRuntimeCatalogSnapshot.SchemaId,
                CultMesh.ProjectionSource("catalog:aetheria.runtime"));
            var loadoutTemplatesDocument = CatalogDocument(
                "aetheria.catalog.loadout_templates",
                () => Task.FromResult(ReadLoadoutTemplatesDocument()),
                AetheriaRuntimeLoadoutTemplatesDocument.SchemaId,
                CultMesh.ProjectionSource("catalog:aetheria.loadout_templates"));
            var starbridgeScenarioDocument = Document<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
            var starbridgeSessionDocument = Document<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);

            return new AetheriaClientState(
                Document<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement),
                Document<AetheriaRuntimeDaemonHealthDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonHealth),
                Document<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary),
                Document<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                    AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy),
                Document<AetheriaRuntimeDaemonFrameDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest),
                Document<AetheriaRuntimeDaemonSoaViewDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonGameSurface),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface),
                catalogDocument,
                loadoutTemplatesDocument,
                CatalogDocument(
                    "aetheria.settings.player",
                    () => Task.FromResult(ReadPlayerSettingsDocument()),
                    AetheriaRuntimePlayerSettingsDocument.SchemaId,
                    CultMesh.ProjectionSource("catalog:aetheria.player_settings")),
                CatalogDocument(
                    "aetheria.settings.verse_host",
                    () => Task.FromResult(ReadVerseHostSettingsDocument()),
                    AetheriaRuntimeVerseHostSettingsDocument.SchemaId,
                    CultMesh.ProjectionSource("catalog:aetheria.verse_host_settings")),
                ProjectedDocument(
                    "aetheria.current.zone",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectCurrentZone(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentZone),
                ProjectedDocument(
                    "aetheria.current.entity",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectCurrentEntity(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentEntity),
                ProjectedDocument(
                    "aetheria.current.docking",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectCurrentDocking(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentDocking),
                ProjectedDocument(
                    "aetheria.zone.contacts",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectZoneContacts(frame)),
                    AetheriaRuntimeDaemonSchemas.ZoneContacts),
                ProjectedDocument(
                    "aetheria.station.refit",
                    ProjectStationRefitAsync,
                    AetheriaRuntimeDaemonSchemas.StationRefit,
                    CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                    CultMesh.ProjectionSource("loadout-templates:aetheria.runtime")),
                ProjectedDocument(
                    "aetheria.sector.map",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectSectorMap(frame)),
                    AetheriaRuntimeDaemonSchemas.SectorMap),
                ProjectedDocument(
                    "aetheria.zone.render",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectZoneRender(frame)),
                    AetheriaRuntimeDaemonSchemas.ZoneRender),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.map", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.RtsViewport),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.objects", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectObjectsViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.gravity", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectGravityViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.GravityViewport),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.render_splats", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectRenderSplatsViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                zoneIndex => ProjectedDocument(
                    IndexedDocumentId("aetheria.zone.details", zoneIndex),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectZoneDetails(frame, zoneIndex)),
                    AetheriaRuntimeDaemonSchemas.ZoneDetails),
                entityIndex => ProjectedDocument(
                    IndexedDocumentId("aetheria.object.selected", entityIndex),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectSelectedObject(frame, entityIndex)),
                    AetheriaRuntimeDaemonSchemas.SelectedObject),
                entityIndex => ProjectedDocument(
                    IndexedDocumentId("aetheria.inventory", entityIndex),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectInventory(frame, entityIndex)),
                    AetheriaRuntimeDaemonSchemas.Inventory),
                starbridgeScenarioDocument,
                starbridgeSessionDocument,
                ProjectedDocument(
                    "aetheria.starbridge.summary",
                    ProjectStarbridgeSummaryAsync,
                    AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest.ToString()),
                    CultMesh.ProjectionSource("catalog:aetheria.runtime")),
                seatId => Document<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                    AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId)));

            CultMeshDocumentHandle<TDocument> ProjectedDocument<TDocument>(
                string documentId,
                Func<AetheriaRuntimeDaemonFrameDocument, Task<TDocument>> project,
                string schemaId,
                params CultMeshProjectionSource[] additionalSources)
                where TDocument : class
            {
                var sources = new[]
                    {
                        CultMesh.ProjectionSource(
                            AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                            AetheriaRuntimeDaemonSchemas.Frame,
                            "latest authoritative daemon frame")
                    }
                    .Concat(additionalSources ?? Array.Empty<CultMeshProjectionSource>())
                    .Append(CultMesh.ProjectionSource(documentId, schemaId, "projected Aetheria client document"))
                    .ToArray();

                var verse = CultMesh.Verse("aetheria.local", RuntimeId);
                return CultMesh.Document(
                    documentId,
                    verse,
                    async _ => await project(await RequireFrameAsync().ConfigureAwait(false)).ConfigureAwait(false),
                    _ => frameChanges
                        .SelectAwait(async (frame, cancellationToken) =>
                            await project(frame).ConfigureAwait(false)),
                    sources: sources,
                    routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed projected state"));
            }

            CultMeshDocumentHandle<TDocument> CatalogDocument<TDocument>(
                string documentId,
                Func<Task<TDocument>> project,
                string schemaId,
                params CultMeshProjectionSource[] additionalSources)
                where TDocument : class
            {
                var sources = (additionalSources ?? Array.Empty<CultMeshProjectionSource>())
                    .Append(CultMesh.ProjectionSource(documentId, schemaId, "projected Aetheria catalog document"))
                    .ToArray();

                var verse = CultMesh.Verse("aetheria.local", RuntimeId);
                return CultMesh.Document(
                    documentId,
                    verse,
                    async _ => await project().ConfigureAwait(false),
                    _ => frameChanges
                        .SelectAwait(async (_, cancellationToken) =>
                            await project().ConfigureAwait(false)),
                    sources: sources,
                    routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed catalog state"));
            }

            async Task<AetheriaRuntimeStationRefitDocument> ProjectStationRefitAsync(
                AetheriaRuntimeDaemonFrameDocument frame)
            {
                var loadoutTemplates = await loadoutTemplatesDocument.LatestAsync().ConfigureAwait(false);
                var catalog = await catalogDocument.LatestAsync().ConfigureAwait(false);
                return AetheriaRuntimeRtsProjection.ProjectStationRefit(frame, loadoutTemplates.Templates, catalog);
            }

            async Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> ProjectStarbridgeSummaryAsync(
                AetheriaRuntimeDaemonFrameDocument frame)
            {
                var scenario = await starbridgeScenarioDocument.LatestAsync().ConfigureAwait(false);
                var session = await starbridgeSessionDocument.LatestAsync().ConfigureAwait(false);
                var catalog = await catalogDocument.LatestAsync().ConfigureAwait(false);
                return AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(
                    frame,
                    scenario,
                    session,
                    catalog);
            }

            static string IndexedDocumentId(string prefix, int index)
            {
                return $"{prefix}.{index.ToString(CultureInfo.InvariantCulture)}";
            }

            static string ViewportDocumentId(string prefix, AetheriaRuntimeRtsViewportBounds viewport)
            {
                var normalized = AetheriaRuntimeRtsProjection.Normalize(viewport);
                return string.Join(
                    ".",
                    prefix,
                    ViewportToken(normalized.MinX),
                    ViewportToken(normalized.MinY),
                    ViewportToken(normalized.MaxX),
                    ViewportToken(normalized.MaxY));
            }

            static string ViewportToken(double value)
            {
                return value
                    .ToString("0.###", CultureInfo.InvariantCulture)
                    .Replace('-', 'n')
                    .Replace('.', 'p');
            }
        }

        private async Task<AetheriaRuntimeDaemonFrameDocument> RequireFrameAsync()
        {
            var frame = await Aetheria().Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
            if (frame == null)
                throw new InvalidOperationException("Aetheria Verse client has no daemon frame yet.");
            return frame;
        }

        private CultMeshMutableStatePointer<T> MutableDocumentPointer<T>(CultRecordKey key) where T : class
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

        private AetheriaRuntimeCatalogSnapshot ReadRuntimeCatalogSnapshot()
        {
            return AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath);
        }

        private AetheriaRuntimeLoadoutTemplatesDocument ReadLoadoutTemplatesDocument()
        {
            return new AetheriaRuntimeLoadoutTemplatesDocument(
                AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(StatePath));
        }

        private AetheriaRuntimePlayerSettingsDocument ReadPlayerSettingsDocument()
        {
            return AetheriaRuntimePlayerSettingsDocument.FromSnapshot(
                AetheriaRuntimeCatalogStore.ReadPlayerSettings(StatePath));
        }

        private AetheriaRuntimeVerseHostSettingsDocument ReadVerseHostSettingsDocument()
        {
            return AetheriaRuntimeVerseHostSettingsDocument.FromSnapshot(
                AetheriaRuntimeCatalogStore.ReadVerseHostSettings(StatePath));
        }
    }
}
