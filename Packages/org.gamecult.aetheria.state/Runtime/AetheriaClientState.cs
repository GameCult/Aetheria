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
        private readonly CultMeshDocumentCatalog _documents;
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
            _documents = CultMesh.Documents(
                ProviderAdvertisement,
                Health,
                CommandBoundary,
                AuthorityPolicy,
                GameSurface,
                GameTuiSurface,
                EditorSurface,
                EditorTuiSurface,
                DaemonFrame,
                DaemonSoaView,
                Catalog,
                LoadoutTemplates,
                PlayerSettings,
                VerseHostSettings,
                CurrentZone,
                CurrentEntity,
                CurrentDockingDocument,
                ZoneContacts,
                StationRefit,
                SectorMap,
                ZoneRender,
                StarbridgeScenario,
                StarbridgeSession,
                StarbridgeSummary);
        }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> Health { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicy { get; }

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

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSummary { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> ZoneContacts { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefit { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> SectorMap { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

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
                $"Aetheria typed state does not expose a managed document for {typeof(TDocument).FullName}.");
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>()
            where TDocument : class
        {
            return Document<TDocument>().Reactive();
        }

        public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> CurrentDocking()
        {
            return CurrentDockingDocument;
        }

        public CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> ReactiveCurrentDocking()
        {
            return CurrentDocking().Reactive();
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
            return TryResolveEveSurface(surfaceId, out var surface)
                ? Document<global::Aetheria.State.Documents.EveSurfaceState>(surface)
                : null;
        }

        public CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>? ReactiveEveSurface(
            string? surfaceId)
        {
            return EveSurfaceDocument(surfaceId)?.Reactive();
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaClientEveSurface surface)
            where TDocument : class
        {
            if (typeof(TDocument) != typeof(global::Aetheria.State.Documents.EveSurfaceState))
                throw new NotSupportedException(
                    $"Aetheria typed state does not expose an Eve surface document for {typeof(TDocument).FullName}.");

            var document = surface switch
            {
                AetheriaClientEveSurface.Game => GameSurface,
                AetheriaClientEveSurface.GameTui => GameTuiSurface,
                AetheriaClientEveSurface.Editor => EditorSurface,
                AetheriaClientEveSurface.EditorTui => EditorTuiSurface,
                _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null)
            };
            return (CultMeshDocumentHandle<TDocument>)(object)document;
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(AetheriaClientEveSurface surface)
            where TDocument : class
        {
            return Document<TDocument>(surface).Reactive();
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)
            where TDocument : class
        {
            viewport ??= new AetheriaRuntimeRtsViewportBounds();

            if (typeof(TDocument) == typeof(AetheriaRuntimeRtsViewportDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_mapViewport(viewport);
            if (typeof(TDocument) == typeof(AetheriaRuntimeObjectsViewportDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_objectsViewport(viewport);
            if (typeof(TDocument) == typeof(AetheriaRuntimeGravityViewportDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_gravityViewport(viewport);
            if (typeof(TDocument) == typeof(AetheriaRuntimeRenderSplatsViewportDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_renderSplatsViewport(viewport);

            throw new NotSupportedException(
                $"Aetheria typed state does not expose a viewport document for {typeof(TDocument).FullName}.");
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)
            where TDocument : class
        {
            return Document<TDocument>(viewport).Reactive();
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(int index)
            where TDocument : class
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Aetheria document index must be non-negative.");

            if (typeof(TDocument) == typeof(AetheriaRuntimeZoneDetailsDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_zoneDetails(index);
            if (typeof(TDocument) == typeof(AetheriaRuntimeSelectedObjectDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_selectedObject(index);
            if (typeof(TDocument) == typeof(AetheriaRuntimeInventoryDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_inventory(index);

            throw new NotSupportedException(
                $"Aetheria typed state does not expose an indexed document for {typeof(TDocument).FullName}.");
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(int index)
            where TDocument : class
        {
            return Document<TDocument>(index).Reactive();
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(string seatId)
            where TDocument : class
        {
            if (typeof(TDocument) != typeof(AetheriaRuntimeStarbridgePlayerSeatDocument))
                throw new NotSupportedException(
                    $"Aetheria typed state does not expose a keyed document for {typeof(TDocument).FullName}.");
            if (string.IsNullOrWhiteSpace(seatId))
                throw new ArgumentException("Seat id must be non-empty.", nameof(seatId));

            return (CultMeshDocumentHandle<TDocument>)(object)_starbridgePlayerSeat(seatId);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(string seatId)
            where TDocument : class
        {
            return Document<TDocument>(seatId).Reactive();
        }

        public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()
        {
            _eveStateRefFrame ??= Reactive<AetheriaRuntimeDaemonFrameDocument>();
            _eveStateRefHealth ??= Reactive<AetheriaRuntimeDaemonHealthDocument>();
            _eveStateRefCommandBoundary ??= Reactive<AetheriaRuntimeDaemonCommandBoundaryDocument>();
            _eveStateRefCatalog ??= Reactive<AetheriaRuntimeCatalogSnapshot>();

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
