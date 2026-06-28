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

        public AetheriaRuntimeCatalogSnapshot LatestCatalog()
        {
            return Latest<AetheriaRuntimeCatalogSnapshot>();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>> ReactiveCatalogAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Catalog.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> ReactiveCatalog(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Catalog.Reactive(options);
        }

        public AetheriaRuntimeCatalogSession ObserveCatalog(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeCatalogSession(ReactiveCatalog(options));
        }

        public Task<AetheriaRuntimeDaemonFrameDocument> LatestDaemonFrameAsync()
        {
            return LatestFrame.LatestAsync();
        }

        public AetheriaRuntimeDaemonFrameDocument LatestDaemonFrame()
        {
            return LatestDaemonFrameAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>> ReactiveDaemonFrameAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return LatestFrame.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> ReactiveDaemonFrame(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return LatestFrame.Reactive(options);
        }

        public AetheriaRuntimeDaemonFrameSession ObserveDaemonFrame(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeDaemonFrameSession(ReactiveDaemonFrame(options));
        }

        public Task<AetheriaRuntimeDaemonSoaViewDocument> LatestDaemonSoaViewAsync()
        {
            return LatestSoaView.LatestAsync();
        }

        public AetheriaRuntimeDaemonSoaViewDocument LatestDaemonSoaView()
        {
            return LatestDaemonSoaViewAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>> ReactiveDaemonSoaViewAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return LatestSoaView.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> ReactiveDaemonSoaView(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return LatestSoaView.Reactive(options);
        }

        public AetheriaRuntimeDaemonSoaViewSession ObserveDaemonSoaView(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeDaemonSoaViewSession(ReactiveDaemonSoaView(options));
        }

        public async Task<AetheriaRuntimeObservedDaemonState?> LatestObservedDaemonAsync()
        {
            var frame = await LatestDaemonFrameAsync().ConfigureAwait(false);
            var soaView = await TryLatestDaemonSoaViewAsync().ConfigureAwait(false);
            var zoneRender = await LatestZoneRenderAsync().ConfigureAwait(false);
            return new AetheriaRuntimeObservedDaemonState(frame, soaView, zoneRender);
        }

        public AetheriaRuntimeObservedDaemonState? LatestObservedDaemon()
        {
            return LatestObservedDaemonAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public AetheriaRuntimeObservedDaemonState? CurrentObservedDaemon(
            CultMeshReactiveDocumentOptions? options = null)
        {
            using var frame = ReactiveDaemonFrame(options);
            using var soaView = TryReactiveDaemonSoaView(options);
            using var zoneRender = ReactiveZoneRender(options);
            return AetheriaRuntimeObservedDaemonState.TryCreateCurrent(frame, soaView, zoneRender, out var current)
                ? current
                : null;
        }

        public AetheriaRuntimeObservedDaemonSession ObserveDaemon(
            CultMeshReactiveDocumentOptions? options = null)
        {
            var frame = ReactiveDaemonFrame(options);
            CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? soaView = null;
            CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument>? zoneRender = null;
            try
            {
                soaView = TryReactiveDaemonSoaView(options);
                zoneRender = ReactiveZoneRender(options);
                return new AetheriaRuntimeObservedDaemonSession(frame, soaView, zoneRender);
            }
            catch
            {
                frame.Dispose();
                soaView?.Dispose();
                zoneRender?.Dispose();
                throw;
            }
        }

        public AetheriaRuntimeObservedDockingState? CurrentDocking(
            CultMeshReactiveDocumentOptions? options = null)
        {
            using var entity = Current.ReactiveEntity(options);
            using var docking = Current.ReactiveDocking(options);
            using var refit = ReactiveStationRefit(options);
            return AetheriaRuntimeObservedDockingState.TryCreateCurrent(entity, docking, refit, out var current)
                ? current
                : null;
        }

        public AetheriaRuntimePlayerHudSession ObservePlayerHud(
            CultMeshReactiveDocumentOptions? options = null)
        {
            var catalog = ReactiveCatalog(options);
            CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument>? playerSettings = null;
            CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument>? currentEntity = null;
            try
            {
                playerSettings = Settings.ReactivePlayer(options);
                currentEntity = Current.ReactiveEntity(options);
                return new AetheriaRuntimePlayerHudSession(catalog, playerSettings, currentEntity);
            }
            catch
            {
                catalog.Dispose();
                playerSettings?.Dispose();
                currentEntity?.Dispose();
                throw;
            }
        }

        private CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? TryReactiveDaemonSoaView(
            CultMeshReactiveDocumentOptions? options)
        {
            try
            {
                return ReactiveDaemonSoaView(options);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private async Task<AetheriaRuntimeDaemonSoaViewDocument?> TryLatestDaemonSoaViewAsync()
        {
            try
            {
                var soaView = await LatestDaemonSoaViewAsync().ConfigureAwait(false);
                return string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal)
                    ? soaView
                    : null;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public Task<AetheriaRuntimeDaemonProviderAdvertisementDocument> LatestProviderAdvertisementAsync()
        {
            return Daemon.LatestProviderAdvertisementAsync();
        }

        public AetheriaRuntimeDaemonProviderAdvertisementDocument LatestProviderAdvertisement()
        {
            return LatestProviderAdvertisementAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeDaemonHealthDocument> LatestHealthAsync()
        {
            return Daemon.LatestHealthAsync();
        }

        public AetheriaRuntimeDaemonHealthDocument LatestHealth()
        {
            return LatestHealthAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeDaemonCommandBoundaryDocument> LatestCommandBoundaryAsync()
        {
            return Daemon.LatestCommandBoundaryAsync();
        }

        public AetheriaRuntimeDaemonCommandBoundaryDocument LatestCommandBoundary()
        {
            return LatestCommandBoundaryAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeVerseAuthorityPolicyDocument> LatestAuthorityPolicyAsync()
        {
            return Daemon.LatestAuthorityPolicyAsync();
        }

        public AetheriaRuntimeVerseAuthorityPolicyDocument LatestAuthorityPolicy()
        {
            return LatestAuthorityPolicyAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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

        public Task<AetheriaRuntimeLoadoutTemplatesDocument> LatestLoadoutTemplatesAsync()
        {
            return LoadoutTemplates.LatestAsync();
        }

        public AetheriaRuntimeLoadoutTemplatesDocument LatestLoadoutTemplates()
        {
            return LatestLoadoutTemplatesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument>> ReactiveLoadoutTemplatesAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return LoadoutTemplates.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> ReactiveLoadoutTemplates(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return LoadoutTemplates.Reactive(options);
        }

        public AetheriaRuntimeLoadoutTemplatesSession ObserveLoadoutTemplates(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeLoadoutTemplatesSession(ReactiveLoadoutTemplates(options));
        }

        public Task<AetheriaRuntimeSectorMapDocument> LatestSectorMapAsync()
        {
            return SectorMap.LatestAsync();
        }

        public AetheriaRuntimeSectorMapDocument LatestSectorMap()
        {
            return LatestSectorMapAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument>> ReactiveSectorMapAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return SectorMap.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> ReactiveSectorMap(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return SectorMap.Reactive(options);
        }

        public AetheriaRuntimeSectorMapSession ObserveSectorMap(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeSectorMapSession(ReactiveSectorMap(options));
        }

        public Task<AetheriaRuntimeZoneContactsDocument> LatestZoneContactsAsync()
        {
            return ZoneContacts.LatestAsync();
        }

        public AetheriaRuntimeZoneContactsDocument LatestZoneContacts()
        {
            return LatestZoneContactsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument>> ReactiveZoneContactsAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return ZoneContacts.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> ReactiveZoneContacts(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return ZoneContacts.Reactive(options);
        }

        public AetheriaRuntimeZoneContactsSession ObserveZoneContacts(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeZoneContactsSession(ReactiveZoneContacts(options));
        }

        public Task<AetheriaRuntimeStationRefitDocument> LatestStationRefitAsync()
        {
            return StationRefit.LatestAsync();
        }

        public AetheriaRuntimeStationRefitDocument LatestStationRefit()
        {
            return LatestStationRefitAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument>> ReactiveStationRefitAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return StationRefit.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> ReactiveStationRefit(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return StationRefit.Reactive(options);
        }

        public AetheriaRuntimeStationRefitSession ObserveStationRefit(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeStationRefitSession(ReactiveStationRefit(options));
        }

        public Task<AetheriaRuntimeZoneRenderDocument> LatestZoneRenderAsync()
        {
            return ZoneRender.LatestAsync();
        }

        public AetheriaRuntimeZoneRenderDocument LatestZoneRender()
        {
            return LatestZoneRenderAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument>> ReactiveZoneRenderAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return ZoneRender.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> ReactiveZoneRender(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return ZoneRender.Reactive(options);
        }

        public AetheriaRuntimeZoneRenderSession ObserveZoneRender(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeZoneRenderSession(ReactiveZoneRender(options));
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
            var frameTask = LatestDaemonFrameAsync();
            var healthTask = LatestHealthAsync();
            var commandBoundaryTask = LatestCommandBoundaryAsync();

            await Task.WhenAll(frameTask, healthTask, commandBoundaryTask).ConfigureAwait(false);

            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                frameTask.Result,
                healthTask.Result,
                commandBoundaryTask.Result,
                LatestCatalog);
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

        public Task<AetheriaRuntimeDaemonProviderAdvertisementDocument> LatestProviderAdvertisementAsync()
        {
            return ProviderAdvertisement.LatestAsync();
        }

        public AetheriaRuntimeDaemonProviderAdvertisementDocument LatestProviderAdvertisement()
        {
            return LatestProviderAdvertisementAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeDaemonHealthDocument> LatestHealthAsync()
        {
            return Health.LatestAsync();
        }

        public AetheriaRuntimeDaemonHealthDocument LatestHealth()
        {
            return LatestHealthAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeDaemonCommandBoundaryDocument> LatestCommandBoundaryAsync()
        {
            return CommandBoundary.LatestAsync();
        }

        public AetheriaRuntimeDaemonCommandBoundaryDocument LatestCommandBoundary()
        {
            return LatestCommandBoundaryAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeVerseAuthorityPolicyDocument> LatestAuthorityPolicyAsync()
        {
            return AuthorityPolicy.LatestAsync();
        }

        public AetheriaRuntimeVerseAuthorityPolicyDocument LatestAuthorityPolicy()
        {
            return LatestAuthorityPolicyAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeDaemonFrameDocument> LatestFrameDocumentAsync()
        {
            return LatestFrame.LatestAsync();
        }

        public AetheriaRuntimeDaemonFrameDocument LatestFrameDocument()
        {
            return LatestFrameDocumentAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaViewDocumentAsync()
        {
            return LatestSoaView.LatestAsync();
        }

        public AetheriaRuntimeDaemonSoaViewDocument LatestSoaViewDocument()
        {
            return LatestSoaViewDocumentAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

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

        public Task<AetheriaRuntimeCurrentZoneDocument> LatestZoneAsync()
        {
            return Zone.LatestAsync();
        }

        public AetheriaRuntimeCurrentZoneDocument LatestZone()
        {
            return LatestZoneAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument>> ReactiveZoneAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Zone.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> ReactiveZone(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Zone.Reactive(options);
        }

        public AetheriaRuntimeCurrentZoneSession ObserveZone(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeCurrentZoneSession(ReactiveZone(options));
        }

        public Task<AetheriaRuntimeCurrentEntityDocument> LatestEntityAsync()
        {
            return Entity.LatestAsync();
        }

        public AetheriaRuntimeCurrentEntityDocument LatestEntity()
        {
            return LatestEntityAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument>> ReactiveEntityAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Entity.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> ReactiveEntity(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Entity.Reactive(options);
        }

        public AetheriaRuntimeCurrentEntitySession ObserveEntity(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeCurrentEntitySession(ReactiveEntity(options));
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument>> ReactiveDockingAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Docking.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> ReactiveDocking(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Docking.Reactive(options);
        }

        public AetheriaRuntimeCurrentDockingSession ObserveDocking(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeCurrentDockingSession(ReactiveDocking(options));
        }
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

        public Task<AetheriaRuntimePlayerSettingsDocument> LatestPlayerAsync()
        {
            return Player.LatestAsync();
        }

        public AetheriaRuntimePlayerSettingsDocument LatestPlayer()
        {
            return LatestPlayerAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument>> ReactivePlayerAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Player.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> ReactivePlayer(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Player.Reactive(options);
        }

        public AetheriaRuntimePlayerSettingsSession ObservePlayer(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimePlayerSettingsSession(ReactivePlayer(options));
        }

        public Task<AetheriaRuntimeVerseHostSettingsDocument> LatestVerseHostAsync()
        {
            return VerseHost.LatestAsync();
        }

        public AetheriaRuntimeVerseHostSettingsDocument LatestVerseHost()
        {
            return LatestVerseHostAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument>> ReactiveVerseHostAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return VerseHost.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument> ReactiveVerseHost(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return VerseHost.Reactive(options);
        }

        public AetheriaRuntimeVerseHostSettingsSession ObserveVerseHost(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeVerseHostSettingsSession(ReactiveVerseHost(options));
        }
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

        public Task<AetheriaRuntimeRtsViewportDocument> LatestMapAsync(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return Map(viewport).LatestAsync();
        }

        public AetheriaRuntimeRtsViewportDocument LatestMap(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return LatestMapAsync(viewport).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeRtsViewportDocument>> ReactiveMapAsync(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Map(viewport).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeRtsViewportDocument> ReactiveMap(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Map(viewport).Reactive(options);
        }

        public AetheriaRuntimeMapViewportSession ObserveMap(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeMapViewportSession(ReactiveMap(viewport, options));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument> Objects(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _objects(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public Task<AetheriaRuntimeObjectsViewportDocument> LatestObjectsAsync(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return Objects(viewport).LatestAsync();
        }

        public AetheriaRuntimeObjectsViewportDocument LatestObjects(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return LatestObjectsAsync(viewport).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument>> ReactiveObjectsAsync(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Objects(viewport).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> ReactiveObjects(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Objects(viewport).Reactive(options);
        }

        public AetheriaRuntimeObjectsViewportSession ObserveObjects(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeObjectsViewportSession(ReactiveObjects(viewport, options));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument> Gravity(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _gravity(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public Task<AetheriaRuntimeGravityViewportDocument> LatestGravityAsync(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return Gravity(viewport).LatestAsync();
        }

        public AetheriaRuntimeGravityViewportDocument LatestGravity(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return LatestGravityAsync(viewport).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeGravityViewportDocument>> ReactiveGravityAsync(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Gravity(viewport).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeGravityViewportDocument> ReactiveGravity(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Gravity(viewport).Reactive(options);
        }

        public AetheriaRuntimeGravityViewportSession ObserveGravity(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeGravityViewportSession(ReactiveGravity(viewport, options));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplats(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _renderSplats(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public Task<AetheriaRuntimeRenderSplatsViewportDocument> LatestRenderSplatsAsync(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return RenderSplats(viewport).LatestAsync();
        }

        public AetheriaRuntimeRenderSplatsViewportDocument LatestRenderSplats(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return LatestRenderSplatsAsync(viewport).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument>> ReactiveRenderSplatsAsync(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return RenderSplats(viewport).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> ReactiveRenderSplats(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return RenderSplats(viewport).Reactive(options);
        }

        public AetheriaRuntimeRenderSplatsViewportSession ObserveRenderSplats(
            AetheriaRuntimeRtsViewportBounds viewport,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeRenderSplatsViewportSession(ReactiveRenderSplats(viewport, options));
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

        public Task<AetheriaRuntimeZoneDetailsDocument> LatestZoneAsync(int zoneIndex)
        {
            return Zone(zoneIndex).LatestAsync();
        }

        public AetheriaRuntimeZoneDetailsDocument LatestZone(int zoneIndex)
        {
            return LatestZoneAsync(zoneIndex).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument>> ReactiveZoneAsync(
            int zoneIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Zone(zoneIndex).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> ReactiveZone(
            int zoneIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Zone(zoneIndex).Reactive(options);
        }

        public AetheriaRuntimeZoneDetailsSession ObserveZone(
            int zoneIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeZoneDetailsSession(ReactiveZone(zoneIndex, options));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument> SelectedObject(int entityIndex)
        {
            return _selectedObject(entityIndex);
        }

        public Task<AetheriaRuntimeSelectedObjectDocument> LatestSelectedObjectAsync(int entityIndex)
        {
            return SelectedObject(entityIndex).LatestAsync();
        }

        public AetheriaRuntimeSelectedObjectDocument LatestSelectedObject(int entityIndex)
        {
            return LatestSelectedObjectAsync(entityIndex).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeSelectedObjectDocument>> ReactiveSelectedObjectAsync(
            int entityIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return SelectedObject(entityIndex).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeSelectedObjectDocument> ReactiveSelectedObject(
            int entityIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return SelectedObject(entityIndex).Reactive(options);
        }

        public AetheriaRuntimeSelectedObjectSession ObserveSelectedObject(
            int entityIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeSelectedObjectSession(ReactiveSelectedObject(entityIndex, options));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument> Inventory(int entityIndex)
        {
            return _inventory(entityIndex);
        }

        public Task<AetheriaRuntimeInventoryDocument> LatestInventoryAsync(int entityIndex)
        {
            return Inventory(entityIndex).LatestAsync();
        }

        public AetheriaRuntimeInventoryDocument LatestInventory(int entityIndex)
        {
            return LatestInventoryAsync(entityIndex).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument>> ReactiveInventoryAsync(
            int entityIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Inventory(entityIndex).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> ReactiveInventory(
            int entityIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Inventory(entityIndex).Reactive(options);
        }

        public AetheriaRuntimeInventorySession ObserveInventory(
            int entityIndex,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeInventorySession(ReactiveInventory(entityIndex, options));
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

        public Task<AetheriaRuntimeStarbridgeScenarioDocument> LatestScenarioAsync()
        {
            return Scenario.LatestAsync();
        }

        public AetheriaRuntimeStarbridgeScenarioDocument LatestScenario()
        {
            return LatestScenarioAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument>> ReactiveScenarioAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Scenario.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> ReactiveScenario(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Scenario.Reactive(options);
        }

        public AetheriaRuntimeStarbridgeScenarioSession ObserveScenario(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeStarbridgeScenarioSession(ReactiveScenario(options));
        }

        public Task<AetheriaRuntimeStarbridgeSessionDocument> LatestSessionAsync()
        {
            return Session.LatestAsync();
        }

        public AetheriaRuntimeStarbridgeSessionDocument LatestSession()
        {
            return LatestSessionAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument>> ReactiveSessionAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Session.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> ReactiveSession(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Session.Reactive(options);
        }

        public AetheriaRuntimeStarbridgeRunSession ObserveSession(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeStarbridgeRunSession(ReactiveSession(options));
        }

        public Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> LatestSummaryAsync()
        {
            return Summary.LatestAsync();
        }

        public AetheriaRuntimeStarbridgeSessionSummaryDocument LatestSummary()
        {
            return LatestSummaryAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument>> ReactiveSummaryAsync(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Summary.ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument> ReactiveSummary(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return Summary.Reactive(options);
        }

        public AetheriaRuntimeStarbridgeSummarySession ObserveSummary(
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeStarbridgeSummarySession(ReactiveSummary(options));
        }

        public Task<AetheriaRuntimeStarbridgePlayerSeatDocument> LatestPlayerSeatAsync(string seatId)
        {
            return PlayerSeat(seatId).LatestAsync();
        }

        public AetheriaRuntimeStarbridgePlayerSeatDocument LatestPlayerSeat(string seatId)
        {
            return LatestPlayerSeatAsync(seatId).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<AetheriaRuntimeStarbridgePlayerSeatDocument>> ReactivePlayerSeatAsync(
            string seatId,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return PlayerSeat(seatId).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgePlayerSeatDocument> ReactivePlayerSeat(
            string seatId,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return PlayerSeat(seatId).Reactive(options);
        }

        public AetheriaRuntimeStarbridgePlayerSeatSession ObservePlayerSeat(
            string seatId,
            CultMeshReactiveDocumentOptions? options = null)
        {
            return new AetheriaRuntimeStarbridgePlayerSeatSession(ReactivePlayerSeat(seatId, options));
        }
    }
}
