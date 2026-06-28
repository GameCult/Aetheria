using System;
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
            DockingState = new AetheriaClientDockingState(Current.Entity, Current.Docking, StationRefit);
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
                Starbridge.Session);
        }

        public AetheriaClientDaemonState Daemon { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> LatestFrame { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> Catalog { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates { get; }

        public AetheriaClientSettingsState Settings { get; }

        public AetheriaClientCurrentState Current { get; }

        public AetheriaClientDockingState DockingState { get; }

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

        public Task<AetheriaRuntimeSectorMapDocument> LatestSectorMapAsync()
        {
            return SectorMap.LatestAsync();
        }

        public AetheriaRuntimeSectorMapDocument LatestSectorMap()
        {
            return LatestSectorMapAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<AetheriaRuntimeZoneContactsDocument> LatestZoneContactsAsync()
        {
            return ZoneContacts.LatestAsync();
        }

        public AetheriaRuntimeZoneContactsDocument LatestZoneContacts()
        {
            return LatestZoneContactsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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
            var frameTask = Daemon.LatestFrame.LatestAsync();
            var healthTask = Daemon.Health.LatestAsync();
            var commandBoundaryTask = Daemon.CommandBoundary.LatestAsync();

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

        public Task<AetheriaRuntimeCurrentEntityDocument> LatestEntityAsync()
        {
            return Entity.LatestAsync();
        }

        public AetheriaRuntimeCurrentEntityDocument LatestEntity()
        {
            return LatestEntityAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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

        public Task<AetheriaRuntimeVerseHostSettingsDocument> LatestVerseHostAsync()
        {
            return VerseHost.LatestAsync();
        }

        public AetheriaRuntimeVerseHostSettingsDocument LatestVerseHost()
        {
            return LatestVerseHostAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    public sealed class AetheriaClientDockingState
    {
        private readonly CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> _currentEntity;
        private readonly CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> _currentDocking;
        private readonly CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> _stationRefit;

        internal AetheriaClientDockingState(
            CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> currentEntity,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> currentDocking,
            CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> stationRefit)
        {
            _currentEntity = currentEntity ?? throw new ArgumentNullException(nameof(currentEntity));
            _currentDocking = currentDocking ?? throw new ArgumentNullException(nameof(currentDocking));
            _stationRefit = stationRefit ?? throw new ArgumentNullException(nameof(stationRefit));
        }

        public Task<AetheriaClientDockingSnapshot> LatestAsync()
        {
            return CultMesh.LatestAsync(
                _currentEntity,
                _currentDocking,
                _stationRefit,
                CreateSnapshot);
        }

        public AetheriaClientDockingSnapshot Latest()
        {
            return LatestAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public bool TryLatest(out AetheriaClientDockingSnapshot? snapshot)
        {
            snapshot = null;
            try
            {
                snapshot = Latest();
                return snapshot != null;
            }
            catch
            {
                return false;
            }
        }

        private static AetheriaClientDockingSnapshot CreateSnapshot(
            AetheriaRuntimeCurrentEntityDocument? entity,
            AetheriaRuntimeCurrentDockingDocument? docking,
            AetheriaRuntimeStationRefitDocument? refit)
        {
            var dockingBay = refit?.IsDocked == true && refit.DockingBayIndex >= 0
                ? (refit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
                    .FirstOrDefault(row => row != null && row.DockingBayIndex == refit.DockingBayIndex)
                : null;
            return new AetheriaClientDockingSnapshot(entity, docking, refit, dockingBay);
        }
    }

    public sealed class AetheriaClientDockingSnapshot
    {
        internal AetheriaClientDockingSnapshot(
            AetheriaRuntimeCurrentEntityDocument? currentEntity,
            AetheriaRuntimeCurrentDockingDocument? currentDocking,
            AetheriaRuntimeStationRefitDocument? stationRefit,
            AetheriaRuntimeStationDockingBayRow? currentDockingBay)
        {
            CurrentEntity = currentEntity;
            CurrentDocking = currentDocking;
            StationRefit = stationRefit;
            CurrentDockingBay = currentDockingBay;
        }

        public AetheriaRuntimeCurrentEntityDocument? CurrentEntity { get; }

        public AetheriaRuntimeCurrentDockingDocument? CurrentDocking { get; }

        public AetheriaRuntimeStationRefitDocument? StationRefit { get; }

        public AetheriaRuntimeStationDockingBayRow? CurrentDockingBay { get; }

        public string CurrentEntityKey => CurrentEntity?.EntityKey ?? CurrentDocking?.CurrentEntityKey ?? "";

        public string DockParentEntityKey => StationRefit?.DockParentEntityKey ?? CurrentDocking?.DockParentEntityKey ?? "";

        public int DockingBayIndex => CurrentDockingBay?.DockingBayIndex ?? StationRefit?.DockingBayIndex ?? CurrentDocking?.DockingBayIndex ?? -1;

        public bool IsDocked => StationRefit?.IsDocked == true && CurrentDockingBay != null;
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

        public CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument> Gravity(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _gravity(viewport ?? new AetheriaRuntimeRtsViewportBounds());
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

        public CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument> SelectedObject(int entityIndex)
        {
            return _selectedObject(entityIndex);
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
    }
}
