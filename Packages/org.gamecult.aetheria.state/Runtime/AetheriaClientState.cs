using System;
using System.Collections.Generic;
using GameCult.Mesh;
using MessagePack;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;

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
        private readonly Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGameViewportDocument>> _mapViewport;
        private readonly Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument>> _objectsViewport;
        private readonly Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument>> _gravityViewport;
        private readonly Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument>> _renderSplatsViewport;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument>> _zoneDetails;
        private readonly Func<int, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> _zoneDetailsSurface;
        private readonly Func<AetheriaRuntimeInventoryPanelSurfaceRequest, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> _inventoryPanelSurface;
        private readonly Func<AetheriaRuntimeInventoryDropdownSurfaceRequest, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> _inventoryDropdownSurface;
        private readonly Func<string, bool, bool, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> _mainMenuSurfaceDocument;
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
            CultMeshDocumentHandle<EveSurfaceDocument> gameSurface,
            CultMeshDocumentHandle<EveSurfaceDocument> gameTuiSurface,
            CultMeshDocumentHandle<EveSurfaceDocument> editorSurface,
            CultMeshDocumentHandle<EveSurfaceDocument> editorTuiSurface,
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
            Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGameViewportDocument>> mapViewport,
            Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument>> objectsViewport,
            Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument>> gravityViewport,
            Func<AetheriaRuntimeViewportBounds, CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument>> renderSplatsViewport,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument>> zoneDetails,
            Func<int, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> zoneDetailsSurface,
            Func<AetheriaRuntimeInventoryPanelSurfaceRequest, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> inventoryPanelSurface,
            Func<AetheriaRuntimeInventoryDropdownSurfaceRequest, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> inventoryDropdownSurface,
            Func<string, bool, bool, CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument>> mainMenuSurfaceDocument,
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
            _zoneDetailsSurface = zoneDetailsSurface ?? throw new ArgumentNullException(nameof(zoneDetailsSurface));
            _inventoryPanelSurface = inventoryPanelSurface ?? throw new ArgumentNullException(nameof(inventoryPanelSurface));
            _inventoryDropdownSurface = inventoryDropdownSurface ?? throw new ArgumentNullException(nameof(inventoryDropdownSurface));
            _mainMenuSurfaceDocument = mainMenuSurfaceDocument ?? throw new ArgumentNullException(nameof(mainMenuSurfaceDocument));
            _selectedObject = selectedObject ?? throw new ArgumentNullException(nameof(selectedObject));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            StarbridgeScenario = starbridgeScenario ?? throw new ArgumentNullException(nameof(starbridgeScenario));
            StarbridgeSession = starbridgeSession ?? throw new ArgumentNullException(nameof(starbridgeSession));
            StarbridgeSummary = starbridgeSummary ?? throw new ArgumentNullException(nameof(starbridgeSummary));
            _starbridgePlayerSeat = starbridgePlayerSeat ?? throw new ArgumentNullException(nameof(starbridgePlayerSeat));
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> Health { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicy { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> DaemonFrame { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> DaemonSoaView { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> Catalog { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates { get; }

        public CultMeshDocumentHandle<EveSurfaceDocument> GameSurface { get; }

        public CultMeshDocumentHandle<EveSurfaceDocument> GameTuiSurface { get; }

        public CultMeshDocumentHandle<EveSurfaceDocument> EditorSurface { get; }

        public CultMeshDocumentHandle<EveSurfaceDocument> EditorTuiSurface { get; }

        public CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> PlayerSettings { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> VerseHostSettings { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> CurrentZone { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> CurrentEntity { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> CurrentDocking { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSummary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> ZoneContacts { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefit { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> SectorMap { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

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

        public CultMeshDocumentHandle<EveSurfaceDocument>? EveSurfaceDocument(
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

        public CultMeshDocumentHandle<AetheriaRuntimeGameViewportDocument> GameViewport(
            AetheriaRuntimeViewportBounds viewport)
        {
            return _mapViewport(viewport ?? new AetheriaRuntimeViewportBounds());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeObjectsViewportDocument> ObjectsViewport(
            AetheriaRuntimeViewportBounds viewport)
        {
            return _objectsViewport(viewport ?? new AetheriaRuntimeViewportBounds());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeGravityViewportDocument> GravityViewport(
            AetheriaRuntimeViewportBounds viewport)
        {
            return _gravityViewport(viewport ?? new AetheriaRuntimeViewportBounds());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplatsViewport(
            AetheriaRuntimeViewportBounds viewport)
        {
            return _renderSplatsViewport(viewport ?? new AetheriaRuntimeViewportBounds());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneDetailsDocument> ZoneDetails(int zoneIndex)
        {
            if (zoneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneIndex), zoneIndex, "Aetheria zone index must be non-negative.");

            return _zoneDetails(zoneIndex);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument> ZoneDetailsSurface(int zoneIndex)
        {
            if (zoneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneIndex), zoneIndex, "Aetheria zone index must be non-negative.");

            return _zoneDetailsSurface(zoneIndex);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument> InventoryPanelSurface(
            AetheriaRuntimeInventoryPanelSurfaceRequest request)
        {
            return _inventoryPanelSurface(request ?? new AetheriaRuntimeInventoryPanelSurfaceRequest());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument> InventoryDropdownSurface(
            AetheriaRuntimeInventoryDropdownSurfaceRequest request)
        {
            return _inventoryDropdownSurface(request ?? new AetheriaRuntimeInventoryDropdownSurfaceRequest());
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument> MainMenuSurface(
            string surfaceId,
            bool canOpenRuntimeInputScreen,
            bool inGame)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                throw new ArgumentException("Main menu surface id must be non-empty.", nameof(surfaceId));

            return _mainMenuSurfaceDocument(surfaceId, canOpenRuntimeInputScreen, inGame);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeSelectedObjectDocument> SelectedObject(int zoneIndex)
        {
            if (zoneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneIndex), zoneIndex, "Aetheria zone index must be non-negative.");

            return _selectedObject(zoneIndex);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeInventoryDocument> Inventory(int entityIndex)
        {
            if (entityIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(entityIndex), entityIndex, "Aetheria entity index must be non-negative.");

            return _inventory(entityIndex);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgePlayerSeatDocument> StarbridgePlayerSeat(string seatId)
        {
            if (string.IsNullOrWhiteSpace(seatId))
                throw new ArgumentException("Seat id must be non-empty.", nameof(seatId));

            return _starbridgePlayerSeat(seatId);
        }

        public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()
        {
            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                () => ReadOptionalReactive(ref _eveStateRefFrame, DaemonFrame),
                () => ReadOptionalReactive(ref _eveStateRefHealth, Health),
                () => ReadOptionalReactive(ref _eveStateRefCommandBoundary, CommandBoundary),
                () => ReadOptionalReactive(ref _eveStateRefCatalog, Catalog));
        }

        private static TDocument? ReadOptionalReactive<TDocument>(
            ref CultMeshReactiveDocument<TDocument>? reactive,
            CultMeshDocumentHandle<TDocument> handle)
            where TDocument : class
        {
            try
            {
                reactive ??= handle.Reactive();
                return reactive.Current;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
            catch (FormatterNotRegisteredException)
            {
                return null;
            }
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
