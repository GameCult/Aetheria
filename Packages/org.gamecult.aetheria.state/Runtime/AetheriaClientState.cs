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
            CurrentDockingDocument = currentDocking ?? throw new ArgumentNullException(nameof(currentDocking));
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

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisementDocument()
        {
            return ProviderAdvertisement;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument> ReactiveProviderAdvertisement()
        {
            return ProviderAdvertisementDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> Health { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> HealthDocument()
        {
            return Health;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonHealthDocument> ReactiveHealth()
        {
            return HealthDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundaryDocument()
        {
            return CommandBoundary;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonCommandBoundaryDocument> ReactiveCommandBoundary()
        {
            return CommandBoundaryDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicy { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicyDocument()
        {
            return AuthorityPolicy;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeVerseAuthorityPolicyDocument> ReactiveAuthorityPolicy()
        {
            return AuthorityPolicyDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> DaemonFrame { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> DaemonSoaView { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> Catalog { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameTuiSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorSurface { get; }

        public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorTuiSurface { get; }

        public CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> PlayerSettings { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> VerseHostSettings { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> CurrentZone { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> CurrentEntity { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> CurrentDockingDocument { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenarioDocument()
        {
            return StarbridgeScenario;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> ReactiveStarbridgeScenario()
        {
            return StarbridgeScenarioDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSessionDocument()
        {
            return StarbridgeSession;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> ReactiveStarbridgeSession()
        {
            return StarbridgeSessionDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSummary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSummaryDocument()
        {
            return StarbridgeSummary;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument> ReactiveStarbridgeSummary()
        {
            return StarbridgeSummaryDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> ZoneContacts { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefit { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> SectorMap { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> DaemonFrameDocument()
        {
            return DaemonFrame;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> ReactiveDaemonFrame()
        {
            return DaemonFrameDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> DaemonSoaViewDocument()
        {
            return DaemonSoaView;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> ReactiveDaemonSoaView()
        {
            return DaemonSoaViewDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> ZoneRenderDocument()
        {
            return ZoneRender;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> ReactiveZoneRender()
        {
            return ZoneRenderDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplatesDocument()
        {
            return LoadoutTemplates;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> ReactiveLoadoutTemplates()
        {
            return LoadoutTemplatesDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> SectorMapDocument()
        {
            return SectorMap;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> ReactiveSectorMap()
        {
            return SectorMapDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> CurrentZoneDocument()
        {
            return CurrentZone;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> ReactiveCurrentZone()
        {
            return CurrentZoneDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> ZoneContactsDocument()
        {
            return ZoneContacts;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> ReactiveZoneContacts()
        {
            return ZoneContactsDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> VerseHostSettingsDocument()
        {
            return VerseHostSettings;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument> ReactiveVerseHostSettings()
        {
            return VerseHostSettingsDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> CurrentDocking()
        {
            return CurrentDockingDocument;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> ReactiveCurrentDocking()
        {
            return CurrentDocking().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> CurrentEntityDocument()
        {
            return CurrentEntity;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> ReactiveCurrentEntity()
        {
            return CurrentEntityDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefitDocument()
        {
            return StationRefit;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> ReactiveStationRefit()
        {
            return StationRefitDocument().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> CatalogSnapshot()
        {
            return Catalog;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> ReactiveCatalogSnapshot()
        {
            return CatalogSnapshot().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> PlayerSettingsDocument()
        {
            return PlayerSettings;
        }

        public CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> ReactivePlayerSettingsDocument()
        {
            return PlayerSettingsDocument().Reactive();
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
