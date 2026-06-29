using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public enum AetheriaClientEveSurface
    {
        Game,
        GameTui,
        Editor,
        EditorTui
    }

    public sealed class AetheriaClientState : IDisposable
    {
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRtsViewportDocument>> _mapViewport;
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument>> _objectsViewport;
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument>> _gravityViewport;
        private readonly Func<AetheriaRuntimeRtsViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument>> _renderSplatsViewport;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument>> _zoneDetails;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument>> _selectedObject;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument>> _inventory;
        private readonly Func<string, CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument>> _starbridgePlayerSeat;
        private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? _eveStateRefFrame;
        private CultMeshReactiveDocument<AetheriaRuntimeDaemonHealthDocument>? _eveStateRefHealth;
        private CultMeshReactiveDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>? _eveStateRefCommandBoundary;
        private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>? _eveStateRefCatalog;

        internal AetheriaClientState(
            CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> providerAdvertisement,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> health,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> commandBoundary,
            CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> authorityPolicy,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> daemonFrame,
            CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> daemonSoaView,
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
            ProviderAdvertisement = providerAdvertisement ?? throw new ArgumentNullException(nameof(providerAdvertisement));
            Health = health ?? throw new ArgumentNullException(nameof(health));
            CommandBoundary = commandBoundary ?? throw new ArgumentNullException(nameof(commandBoundary));
            AuthorityPolicy = authorityPolicy ?? throw new ArgumentNullException(nameof(authorityPolicy));
            DaemonFrame = daemonFrame ?? throw new ArgumentNullException(nameof(daemonFrame));
            DaemonSoaView = daemonSoaView ?? throw new ArgumentNullException(nameof(daemonSoaView));
            GameSurface = gameSurface ?? throw new ArgumentNullException(nameof(gameSurface));
            GameTuiSurface = gameTuiSurface ?? throw new ArgumentNullException(nameof(gameTuiSurface));
            EditorSurface = editorSurface ?? throw new ArgumentNullException(nameof(editorSurface));
            EditorTuiSurface = editorTuiSurface ?? throw new ArgumentNullException(nameof(editorTuiSurface));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            LoadoutTemplates = loadoutTemplates ?? throw new ArgumentNullException(nameof(loadoutTemplates));
            PlayerSettings = playerSettings ?? throw new ArgumentNullException(nameof(playerSettings));
            VerseHostSettings = verseHostSettings ?? throw new ArgumentNullException(nameof(verseHostSettings));
            CurrentZone = currentZone ?? throw new ArgumentNullException(nameof(currentZone));
            CurrentEntity = currentEntity ?? throw new ArgumentNullException(nameof(currentEntity));
            CurrentDocking = currentDocking ?? throw new ArgumentNullException(nameof(currentDocking));
            ZoneContacts = zoneContacts ?? throw new ArgumentNullException(nameof(zoneContacts));
            StationRefit = stationRefit ?? throw new ArgumentNullException(nameof(stationRefit));
            SectorMap = sectorMap ?? throw new ArgumentNullException(nameof(sectorMap));
            ZoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
            _mapViewport = mapViewport ?? throw new ArgumentNullException(nameof(mapViewport));
            _objectsViewport = objectsViewport ?? throw new ArgumentNullException(nameof(objectsViewport));
            _gravityViewport = gravityViewport ?? throw new ArgumentNullException(nameof(gravityViewport));
            _renderSplatsViewport = renderSplatsViewport ?? throw new ArgumentNullException(nameof(renderSplatsViewport));
            _zoneDetails = zoneDetails ?? throw new ArgumentNullException(nameof(zoneDetails));
            _selectedObject = selectedObject ?? throw new ArgumentNullException(nameof(selectedObject));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            StarbridgeScenario = starbridgeScenario ?? throw new ArgumentNullException(nameof(starbridgeScenario));
            StarbridgeSession = starbridgeSession ?? throw new ArgumentNullException(nameof(starbridgeSession));
            StarbridgeSummary = starbridgeSummary ?? throw new ArgumentNullException(nameof(starbridgeSummary));
            _starbridgePlayerSeat = starbridgePlayerSeat ?? throw new ArgumentNullException(nameof(starbridgePlayerSeat));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement { get; }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument> ReactiveProviderAdvertisement()
        {
            return ProviderAdvertisement.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> Health { get; }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonHealthDocument> ReactiveHealth()
        {
            return Health.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary { get; }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonCommandBoundaryDocument> ReactiveCommandBoundary()
        {
            return CommandBoundary.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicy { get; }

        public CultMeshReactiveDocument<AetheriaRuntimeVerseAuthorityPolicyDocument> ReactiveAuthorityPolicy()
        {
            return AuthorityPolicy.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> DaemonFrame { get; }

        public AetheriaRuntimeDaemonFrameDocument CurrentDaemonFrame()
        {
            return DaemonFrame.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> DaemonSoaView { get; }

        public AetheriaRuntimeDaemonSoaViewDocument CurrentDaemonSoaView()
        {
            return DaemonSoaView.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> Catalog { get; }

        public AetheriaRuntimeCatalogSnapshot CurrentCatalog()
        {
            return Catalog.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates { get; }

        public AetheriaRuntimeLoadoutTemplatesDocument CurrentLoadoutTemplates()
        {
            return LoadoutTemplates.Latest();
        }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameTuiSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorTuiSurface { get; }

        public CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> PlayerSettings { get; }

        public AetheriaRuntimePlayerSettingsDocument CurrentPlayerSettings()
        {
            return PlayerSettings.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> VerseHostSettings { get; }

        public AetheriaRuntimeVerseHostSettingsDocument CurrentVerseHostSettings()
        {
            return VerseHostSettings.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> CurrentZone { get; }

        public AetheriaRuntimeCurrentZoneDocument CurrentZoneState()
        {
            return CurrentZone.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> CurrentEntity { get; }

        public AetheriaRuntimeCurrentEntityDocument CurrentEntityState()
        {
            return CurrentEntity.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> CurrentDocking { get; }

        public AetheriaRuntimeCurrentDockingDocument CurrentDockingState()
        {
            return CurrentDocking.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario { get; }

        public AetheriaRuntimeStarbridgeScenarioDocument CurrentStarbridgeScenario()
        {
            return StarbridgeScenario.Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> ReactiveStarbridgeScenario()
        {
            return StarbridgeScenario.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession { get; }

        public AetheriaRuntimeStarbridgeSessionDocument CurrentStarbridgeSession()
        {
            return StarbridgeSession.Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> ReactiveStarbridgeSession()
        {
            return StarbridgeSession.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSummary { get; }

        public AetheriaRuntimeStarbridgeSessionSummaryDocument CurrentStarbridgeSummary()
        {
            return StarbridgeSummary.Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument> ReactiveStarbridgeSummary()
        {
            return StarbridgeSummary.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> ZoneContacts { get; }

        public AetheriaRuntimeZoneContactsDocument CurrentZoneContacts()
        {
            return ZoneContacts.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefit { get; }

        public AetheriaRuntimeStationRefitDocument CurrentStationRefit()
        {
            return StationRefit.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> SectorMap { get; }

        public AetheriaRuntimeSectorMapDocument CurrentSectorMap()
        {
            return SectorMap.Latest();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

        public AetheriaRuntimeZoneRenderDocument CurrentZoneRender()
        {
            return ZoneRender.Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> ReactiveDaemonFrame()
        {
            return DaemonFrame.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> ReactiveDaemonSoaView()
        {
            return DaemonSoaView.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> ReactiveZoneRender()
        {
            return ZoneRender.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> ReactiveLoadoutTemplates()
        {
            return LoadoutTemplates.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> ReactiveSectorMap()
        {
            return SectorMap.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> ReactiveCurrentZone()
        {
            return CurrentZone.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> ReactiveZoneContacts()
        {
            return ZoneContacts.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument> ReactiveVerseHostSettings()
        {
            return VerseHostSettings.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> ReactiveCurrentDocking()
        {
            return CurrentDocking.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> ReactiveCurrentEntity()
        {
            return CurrentEntity.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> ReactiveStationRefit()
        {
            return StationRefit.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> ReactiveCatalogSnapshot()
        {
            return Catalog.Reactive();
        }

        public CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> ReactivePlayerSettingsDocument()
        {
            return PlayerSettings.Reactive();
        }

        public static bool TryResolveEveSurface(
            string? surfaceId,
            out AetheriaClientEveSurface surface)
        {
            if (string.Equals(surfaceId, AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
            {
                surface = AetheriaClientEveSurface.Game;
                return true;
            }

            if (string.Equals(surfaceId, AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, StringComparison.Ordinal))
            {
                surface = AetheriaClientEveSurface.GameTui;
                return true;
            }

            if (string.Equals(surfaceId, AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
            {
                surface = AetheriaClientEveSurface.Editor;
                return true;
            }

            if (string.Equals(surfaceId, AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, StringComparison.Ordinal))
            {
                surface = AetheriaClientEveSurface.EditorTui;
                return true;
            }

            surface = default;
            return false;
        }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState>? EveSurfaceDocument(
            string? surfaceId)
        {
            if (!TryResolveEveSurface(surfaceId, out var surface))
                return null;

            return surface switch
            {
                AetheriaClientEveSurface.Game => GameSurface,
                AetheriaClientEveSurface.GameTui => GameTuiSurface,
                AetheriaClientEveSurface.Editor => EditorSurface,
                AetheriaClientEveSurface.EditorTui => EditorTuiSurface,
                _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null)
            };
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>? ReactiveEveSurface(
            string? surfaceId)
        {
            return EveSurfaceDocument(surfaceId)?.Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeRtsViewportDocument> RtsViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _mapViewport(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public AetheriaRuntimeRtsViewportDocument CurrentRtsViewport(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return RtsViewport(viewport).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeRtsViewportDocument> ReactiveRtsViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return RtsViewport(viewport).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument> ObjectsViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _objectsViewport(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public AetheriaRuntimeObjectsViewportDocument CurrentObjectsViewport(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return ObjectsViewport(viewport).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> ReactiveObjectsViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return ObjectsViewport(viewport).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument> GravityViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _gravityViewport(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public AetheriaRuntimeGravityViewportDocument CurrentGravityViewport(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return GravityViewport(viewport).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeGravityViewportDocument> ReactiveGravityViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return GravityViewport(viewport).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplatsViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return _renderSplatsViewport(viewport ?? new AetheriaRuntimeRtsViewportBounds());
        }

        public AetheriaRuntimeRenderSplatsViewportDocument CurrentRenderSplatsViewport(AetheriaRuntimeRtsViewportBounds viewport)
        {
            return RenderSplatsViewport(viewport).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> ReactiveRenderSplatsViewport(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return RenderSplatsViewport(viewport).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument> ZoneDetails(int zoneIndex)
        {
            if (zoneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneIndex), zoneIndex, "Aetheria zone index must be non-negative.");

            return _zoneDetails(zoneIndex);
        }

        public AetheriaRuntimeZoneDetailsDocument CurrentZoneDetails(int zoneIndex)
        {
            return ZoneDetails(zoneIndex).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> ReactiveZoneDetails(int zoneIndex)
        {
            return ZoneDetails(zoneIndex).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument> SelectedObject(int zoneIndex)
        {
            if (zoneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneIndex), zoneIndex, "Aetheria zone index must be non-negative.");

            return _selectedObject(zoneIndex);
        }

        public AetheriaRuntimeSelectedObjectDocument CurrentSelectedObject(int zoneIndex)
        {
            return SelectedObject(zoneIndex).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeSelectedObjectDocument> ReactiveSelectedObject(int zoneIndex)
        {
            return SelectedObject(zoneIndex).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument> Inventory(int entityIndex)
        {
            if (entityIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(entityIndex), entityIndex, "Aetheria entity index must be non-negative.");

            return _inventory(entityIndex);
        }

        public AetheriaRuntimeInventoryDocument CurrentInventory(int entityIndex)
        {
            return Inventory(entityIndex).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> ReactiveInventory(int entityIndex)
        {
            return Inventory(entityIndex).Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument> StarbridgePlayerSeat(string seatId)
        {
            if (string.IsNullOrWhiteSpace(seatId))
                throw new ArgumentException("Seat id must be non-empty.", nameof(seatId));

            return _starbridgePlayerSeat(seatId);
        }

        public AetheriaRuntimeStarbridgePlayerSeatDocument CurrentStarbridgePlayerSeat(string seatId)
        {
            return StarbridgePlayerSeat(seatId).Latest();
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgePlayerSeatDocument> ReactiveStarbridgePlayerSeat(
            string seatId)
        {
            return StarbridgePlayerSeat(seatId).Reactive();
        }

        public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()
        {
            _eveStateRefFrame ??= ReactiveDaemonFrame();
            _eveStateRefHealth ??= ReactiveHealth();
            _eveStateRefCommandBoundary ??= ReactiveCommandBoundary();
            _eveStateRefCatalog ??= ReactiveCatalogSnapshot();

            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                () => _eveStateRefFrame.Current,
                () => _eveStateRefHealth.Current,
                () => _eveStateRefCommandBoundary.Current,
                () => _eveStateRefCatalog.Current);
        }

        public void Dispose()
        {
            _eveStateRefFrame?.Dispose();
            _eveStateRefHealth?.Dispose();
            _eveStateRefCommandBoundary?.Dispose();
            _eveStateRefCatalog?.Dispose();
            _eveStateRefFrame = null;
            _eveStateRefHealth = null;
            _eveStateRefCommandBoundary = null;
            _eveStateRefCatalog = null;
        }
    }

}
