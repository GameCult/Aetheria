using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Mesh;
using R3;

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
                LatestFrame,
                LatestSoaView,
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

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> LatestFrame { get; }

        public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView { get; }

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

        public Task<TDocument> LatestAsync<TDocument>(AetheriaClientEveSurface surface)
            where TDocument : class
        {
            return Document<TDocument>(surface).LatestAsync();
        }

        public TDocument Latest<TDocument>(AetheriaClientEveSurface surface)
            where TDocument : class
        {
            return LatestAsync<TDocument>(surface).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(
            AetheriaClientEveSurface surface,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(surface).ReactiveAsync(options);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(
            AetheriaClientEveSurface surface,
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            return Document<TDocument>(surface).Reactive(options);
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

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)
            where TDocument : class
        {
            if (typeof(TDocument) == typeof(AetheriaRuntimeZoneDetailsDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_zoneDetails(entityOrZoneIndex);
            if (typeof(TDocument) == typeof(AetheriaRuntimeSelectedObjectDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_selectedObject(entityOrZoneIndex);
            if (typeof(TDocument) == typeof(AetheriaRuntimeInventoryDocument))
                return (CultMeshDocumentHandle<TDocument>)(object)_inventory(entityOrZoneIndex);

            throw new NotSupportedException(
                $"Aetheria typed state does not expose an indexed document for {typeof(TDocument).FullName}.");
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
