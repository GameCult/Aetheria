using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Mesh;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaClientState
    {
        private readonly CultMeshDocumentCatalog _documents;

        internal AetheriaClientState(
            CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> providerAdvertisement,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> health,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> commandBoundary,
            CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> authorityPolicy,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> latestFrame,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> latestSoaView,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> gameSurface,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> gameTuiSurface,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> editorSurface,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> editorTuiSurface,
            CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> catalog,
            CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> loadoutTemplates,
            CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> playerSettings,
            CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> verseHostSettings,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> currentZone,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> currentEntity,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> currentDocking,
            CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> zoneContacts,
            CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> stationRefit,
            CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> sectorMap,
            CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> zoneRender,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRtsViewportDocument>> mapViewport,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument>> objectsViewport,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument>> gravityViewport,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument>> renderSplatsViewport,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument>> zoneDetails,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument>> selectedObject,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument>> inventory,
            CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> starbridgeScenario,
            CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> starbridgeSession,
            CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> starbridgeSummary,
            Func<string, CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument>> starbridgePlayerSeat)
        {
            Daemon = new AetheriaClientDaemonState(
                providerAdvertisement,
                health,
                commandBoundary,
                authorityPolicy,
                latestFrame,
                latestSoaView,
                gameSurface,
                gameTuiSurface,
                editorSurface,
                editorTuiSurface);
            LatestFrame = latestFrame ?? throw new ArgumentNullException(nameof(latestFrame));
            LatestSoaView = latestSoaView ?? throw new ArgumentNullException(nameof(latestSoaView));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            LoadoutTemplates = loadoutTemplates ?? throw new ArgumentNullException(nameof(loadoutTemplates));
            Settings = new AetheriaClientSettingsState(playerSettings, verseHostSettings);
            Current = new AetheriaClientCurrentState(currentZone, currentEntity, currentDocking);
            ZoneContacts = zoneContacts ?? throw new ArgumentNullException(nameof(zoneContacts));
            StationRefit = stationRefit ?? throw new ArgumentNullException(nameof(stationRefit));
            SectorMap = sectorMap ?? throw new ArgumentNullException(nameof(sectorMap));
            ZoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
            Viewports = new AetheriaClientViewportState(
                mapViewport,
                objectsViewport,
                gravityViewport,
                renderSplatsViewport);
            Details = new AetheriaClientDetailState(
                zoneDetails,
                selectedObject,
                inventory);
            Starbridge = new AetheriaClientStarbridgeState(
                starbridgeScenario,
                starbridgeSession,
                starbridgeSummary,
                starbridgePlayerSeat);
            _documents = CultMesh.Documents(
                Daemon.ProviderAdvertisement,
                Daemon.Health,
                Daemon.CommandBoundary,
                Daemon.AuthorityPolicy,
                Daemon.GameSurface,
                Daemon.GameTuiSurface,
                Daemon.EditorSurface,
                Daemon.EditorTuiSurface,
                LatestFrame,
                LatestSoaView,
                Catalog,
                LoadoutTemplates,
                Settings.Player,
                Settings.VerseHost,
                Current.Zone,
                Current.Entity,
                Current.Docking,
                ZoneContacts,
                StationRefit,
                SectorMap,
                ZoneRender,
                Starbridge.Scenario,
                Starbridge.Session,
                Starbridge.Summary);
        }

        public AetheriaClientDaemonState Daemon { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> LatestFrame { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> Catalog { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates { get; }

        public AetheriaClientSettingsState Settings { get; }

        public AetheriaClientCurrentState Current { get; }

        public AetheriaClientViewportState Viewports { get; }

        public AetheriaClientDetailState Details { get; }

        public AetheriaClientStarbridgeState Starbridge { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> ZoneContacts { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefit { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> SectorMap { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

        public bool TryGetDocumentBySchema(
            string schemaVersion,
            out ICultMeshDocumentHandle document)
        {
            return _documents.TryGetDocumentBySchema(schemaVersion, out document);
        }

        public ICultMeshDocumentHandle DocumentBySchema(string schemaVersion)
        {
            if (TryGetDocumentBySchema(schemaVersion, out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria typed state does not expose a projected document for schema '{schemaVersion}'.");
        }

        public bool TryGetDocument<TDocument>(
            out CultMeshDocumentHandle<TDocument> document)
            where TDocument : class
        {
            return _documents.TryGetDocument(out document);
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>()
            where TDocument : class
        {
            if (TryGetDocument<TDocument>(out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria typed state does not expose a projected document for {typeof(TDocument).FullName}.");
        }

        public Task<TDocument> LatestAsync<TDocument>()
            where TDocument : class
        {
            return Document<TDocument>().LatestAsync();
        }

        public TDocument Latest<TDocument>()
            where TDocument : class
        {
            return Document<TDocument>().LatestAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<AetheriaRuntimeObservedDaemonState?> LatestObservedDaemonAsync()
        {
            var frame = await LatestAsync<AetheriaRuntimeDaemonFrameDocument>().ConfigureAwait(false);
            var soaView = await TryReadDaemonSoaViewAsync().ConfigureAwait(false);
            var zoneRender = await LatestAsync<AetheriaRuntimeZoneRenderDocument>().ConfigureAwait(false);
            return new AetheriaRuntimeObservedDaemonState(frame, soaView, zoneRender);
        }

        public AetheriaRuntimeObservedDaemonState? LatestObservedDaemon()
        {
            return LatestObservedDaemonAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public AetheriaRuntimeObservedDaemonState? CurrentObservedDaemon(
            CultMeshReactiveDocumentOptions? options = null)
        {
            using var frame = Reactive<AetheriaRuntimeDaemonFrameDocument>(options);
            using var soaView = TryReactive<AetheriaRuntimeDaemonSoaViewDocument>(options);
            using var zoneRender = Reactive<AetheriaRuntimeZoneRenderDocument>(options);
            return AetheriaRuntimeObservedDaemonState.TryCreateCurrent(frame, soaView, zoneRender, out var current)
                ? current
                : null;
        }

        public AetheriaRuntimeObservedDockingState? CurrentDocking(
            CultMeshReactiveDocumentOptions? options = null)
        {
            using var entity = Reactive<AetheriaRuntimeCurrentEntityDocument>(options);
            using var docking = Reactive<AetheriaRuntimeCurrentDockingDocument>(options);
            using var refit = Reactive<AetheriaRuntimeStationRefitDocument>(options);
            return AetheriaRuntimeObservedDockingState.TryCreateCurrent(entity, docking, refit, out var current)
                ? current
                : null;
        }

        private CultMeshReactiveDocument<TDocument>? TryReactive<TDocument>(
            CultMeshReactiveDocumentOptions? options)
            where TDocument : class
        {
            try
            {
                return Reactive<TDocument>(options);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private async Task<AetheriaRuntimeDaemonSoaViewDocument?> TryReadDaemonSoaViewAsync()
        {
            try
            {
                var soaView = await LatestAsync<AetheriaRuntimeDaemonSoaViewDocument>().ConfigureAwait(false);
                return string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal)
                    ? soaView
                    : null;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestGameSurfaceAsync()
        {
            return Daemon.LatestGameSurfaceAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestGameSurface()
        {
            return LatestGameSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveGameSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveGameSurfaceAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveGameSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveGameSurface(options);
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestGameTuiSurfaceAsync()
        {
            return Daemon.LatestGameTuiSurfaceAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestGameTuiSurface()
        {
            return LatestGameTuiSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveGameTuiSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveGameTuiSurfaceAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveGameTuiSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveGameTuiSurface(options);
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestEditorSurfaceAsync()
        {
            return Daemon.LatestEditorSurfaceAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestEditorSurface()
        {
            return LatestEditorSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveEditorSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveEditorSurfaceAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveEditorSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveEditorSurface(options);
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestEditorTuiSurfaceAsync()
        {
            return Daemon.LatestEditorTuiSurfaceAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestEditorTuiSurface()
        {
            return LatestEditorTuiSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveEditorTuiSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveEditorTuiSurfaceAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveEditorTuiSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Daemon.ReactiveEditorTuiSurface(options);
        }

        public Observable<TDocument> Watch<TDocument>()
            where TDocument : class
        {
            return Document<TDocument>().Watch();
        }

        public IDisposable Watch<TDocument>(Action<TDocument> onNext)
            where TDocument : class
        {
            if (onNext == null) throw new ArgumentNullException(nameof(onNext));
            return Document<TDocument>().Watch(onNext);
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>().ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>().Reactive(options);
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
            var frameTask = LatestAsync<AetheriaRuntimeDaemonFrameDocument>();
            var healthTask = LatestAsync<AetheriaRuntimeDaemonHealthDocument>();
            var commandBoundaryTask = LatestAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>();

            await Task.WhenAll(frameTask, healthTask, commandBoundaryTask).ConfigureAwait(false);

            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                frameTask.Result,
                healthTask.Result,
                commandBoundaryTask.Result,
                () => Latest<AetheriaRuntimeCatalogSnapshot>());
        }
    }

    public sealed class AetheriaClientDaemonState
    {
        internal AetheriaClientDaemonState(
            CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> providerAdvertisement,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> health,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> commandBoundary,
            CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> authorityPolicy,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> latestFrame,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> latestSoaView,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> gameSurface,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> gameTuiSurface,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> editorSurface,
            CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> editorTuiSurface)
        {
            ProviderAdvertisement = providerAdvertisement ?? throw new ArgumentNullException(nameof(providerAdvertisement));
            Health = health ?? throw new ArgumentNullException(nameof(health));
            CommandBoundary = commandBoundary ?? throw new ArgumentNullException(nameof(commandBoundary));
            AuthorityPolicy = authorityPolicy ?? throw new ArgumentNullException(nameof(authorityPolicy));
            LatestFrame = latestFrame ?? throw new ArgumentNullException(nameof(latestFrame));
            LatestSoaView = latestSoaView ?? throw new ArgumentNullException(nameof(latestSoaView));
            GameSurface = gameSurface ?? throw new ArgumentNullException(nameof(gameSurface));
            GameTuiSurface = gameTuiSurface ?? throw new ArgumentNullException(nameof(gameTuiSurface));
            EditorSurface = editorSurface ?? throw new ArgumentNullException(nameof(editorSurface));
            EditorTuiSurface = editorTuiSurface ?? throw new ArgumentNullException(nameof(editorTuiSurface));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> Health { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicy { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> LatestFrame { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameTuiSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorTuiSurface { get; }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestGameSurfaceAsync()
        {
            return GameSurface.LatestAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestGameSurface()
        {
            return LatestGameSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveGameSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return GameSurface.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveGameSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return GameSurface.Reactive(options);
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestGameTuiSurfaceAsync()
        {
            return GameTuiSurface.LatestAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestGameTuiSurface()
        {
            return LatestGameTuiSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveGameTuiSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return GameTuiSurface.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveGameTuiSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return GameTuiSurface.Reactive(options);
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestEditorSurfaceAsync()
        {
            return EditorSurface.LatestAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestEditorSurface()
        {
            return LatestEditorSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveEditorSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return EditorSurface.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveEditorSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return EditorSurface.Reactive(options);
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState> LatestEditorTuiSurfaceAsync()
        {
            return EditorTuiSurface.LatestAsync();
        }

        public global::Aetheria.State.Documents.EveSurfaceState LatestEditorTuiSurface()
        {
            return LatestEditorTuiSurfaceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>> ReactiveEditorTuiSurfaceAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return EditorTuiSurface.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState> ReactiveEditorTuiSurface(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return EditorTuiSurface.Reactive(options);
        }
    }

    public sealed class AetheriaClientCurrentState
    {
        internal AetheriaClientCurrentState(
            CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> zone,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> entity,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> docking)
        {
            Zone = zone ?? throw new ArgumentNullException(nameof(zone));
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Docking = docking ?? throw new ArgumentNullException(nameof(docking));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> Zone { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> Entity { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> Docking { get; }

    }

    public sealed class AetheriaClientSettingsState
    {
        internal AetheriaClientSettingsState(
            CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> player,
            CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> verseHost)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            VerseHost = verseHost ?? throw new ArgumentNullException(nameof(verseHost));
        }

        public CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> Player { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> VerseHost { get; }

    }

    public sealed class AetheriaClientViewportState
    {
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRtsViewportDocument>> _map;
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument>> _objects;
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument>> _gravity;
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument>> _renderSplats;

        internal AetheriaClientViewportState(
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRtsViewportDocument>> map,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument>> objects,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument>> gravity,
            Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument>> renderSplats)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _gravity = gravity ?? throw new ArgumentNullException(nameof(gravity));
            _renderSplats = renderSplats ?? throw new ArgumentNullException(nameof(renderSplats));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeRtsViewportDocument> Map(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _map(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public bool TryGetDocument<TDocument>(
            AetheriaRuntimeRtsViewportBounds viewport,
            out CultMeshDocumentHandle<TDocument> document)
            where TDocument : class
        {
            if (typeof(TDocument) == typeof(AetheriaRuntimeRtsViewportDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)Map(viewport);
                return true;
            }

            if (typeof(TDocument) == typeof(AetheriaRuntimeObjectsViewportDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)Objects(viewport);
                return true;
            }

            if (typeof(TDocument) == typeof(AetheriaRuntimeGravityViewportDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)Gravity(viewport);
                return true;
            }

            if (typeof(TDocument) == typeof(AetheriaRuntimeRenderSplatsViewportDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)RenderSplats(viewport);
                return true;
            }

            document = null!;
            return false;
        }

        public CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument> Objects(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _objects(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)
            where TDocument : class
        {
            if (TryGetDocument<TDocument>(viewport, out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria viewport state does not expose a projected document for type '{typeof(TDocument).FullName}'.");
        }

        public Task<TDocument> LatestAsync<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)
            where TDocument : class
        {
            return Document<TDocument>(viewport).LatestAsync();
        }

        public TDocument Latest<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)
            where TDocument : class
        {
            return LatestAsync<TDocument>(viewport).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(viewport).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(viewport).Reactive(options);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument> Gravity(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _gravity(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplats(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _renderSplats(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

    }

    public sealed class AetheriaClientDetailState
    {
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument>> _zoneDetails;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument>> _selectedObject;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument>> _inventory;

        internal AetheriaClientDetailState(
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument>> zoneDetails,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument>> selectedObject,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument>> inventory)
        {
            _zoneDetails = zoneDetails ?? throw new ArgumentNullException(nameof(zoneDetails));
            _selectedObject = selectedObject ?? throw new ArgumentNullException(nameof(selectedObject));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument> Zone(int zoneIndex)
        {
            return _zoneDetails(zoneIndex);
        }

        public bool TryGetDocument<TDocument>(
            int entityOrZoneIndex,
            out CultMeshDocumentHandle<TDocument> document)
            where TDocument : class
        {
            if (typeof(TDocument) == typeof(AetheriaRuntimeZoneDetailsDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)Zone(entityOrZoneIndex);
                return true;
            }

            if (typeof(TDocument) == typeof(AetheriaRuntimeSelectedObjectDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)SelectedObject(entityOrZoneIndex);
                return true;
            }

            if (typeof(TDocument) == typeof(AetheriaRuntimeInventoryDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)Inventory(entityOrZoneIndex);
                return true;
            }

            document = null!;
            return false;
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument> SelectedObject(int entityIndex)
        {
            return _selectedObject(entityIndex);
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)
            where TDocument : class
        {
            if (TryGetDocument<TDocument>(entityOrZoneIndex, out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria detail state does not expose a projected document for type '{typeof(TDocument).FullName}'.");
        }

        public Task<TDocument> LatestAsync<TDocument>(int entityOrZoneIndex)
            where TDocument : class
        {
            return Document<TDocument>(entityOrZoneIndex).LatestAsync();
        }

        public TDocument Latest<TDocument>(int entityOrZoneIndex)
            where TDocument : class
        {
            return LatestAsync<TDocument>(entityOrZoneIndex).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(
            int entityOrZoneIndex,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(entityOrZoneIndex).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(
            int entityOrZoneIndex,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(entityOrZoneIndex).Reactive(options);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument> Inventory(int entityIndex)
        {
            return _inventory(entityIndex);
        }

    }

    public sealed class AetheriaClientStarbridgeState
    {
        private readonly Func<string, CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument>> _playerSeat;

        internal AetheriaClientStarbridgeState(
            CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> scenario,
            CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> session,
            CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> summary,
            Func<string, CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument>> playerSeat)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            _playerSeat = playerSeat ?? throw new ArgumentNullException(nameof(playerSeat));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> Scenario { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> Session { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> Summary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument> PlayerSeat(string seatId)
        {
            if (string.IsNullOrWhiteSpace(seatId))
                throw new ArgumentException("Seat id must be non-empty.", nameof(seatId));
            return _playerSeat(seatId);
        }

        public bool TryGetDocument<TDocument>(
            string seatId,
            out CultMeshDocumentHandle<TDocument> document)
            where TDocument : class
        {
            if (typeof(TDocument) == typeof(AetheriaRuntimeStarbridgePlayerSeatDocument))
            {
                document = (CultMeshDocumentHandle<TDocument>)(object)PlayerSeat(seatId);
                return true;
            }

            document = null!;
            return false;
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(string seatId)
            where TDocument : class
        {
            if (TryGetDocument<TDocument>(seatId, out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria Starbridge state does not expose a parameterized document for {typeof(TDocument).FullName}.");
        }

        public Task<TDocument> LatestAsync<TDocument>(string seatId)
            where TDocument : class
        {
            return Document<TDocument>(seatId).LatestAsync();
        }

        public TDocument Latest<TDocument>(string seatId)
            where TDocument : class
        {
            return LatestAsync<TDocument>(seatId).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(
            string seatId,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(seatId).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(
            string seatId,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(seatId).Reactive(options);
        }

    }
}
