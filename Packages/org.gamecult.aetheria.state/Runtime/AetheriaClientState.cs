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
            CultMeshDocumentHandle<AetheriaRuntimeCurrentZoneDocument> currentZone,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentEntityDocument> currentEntity,
            CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> currentDocking,
            CultMeshDocumentHandle<AetheriaRuntimeZoneContactsDocument> zoneContacts,
            CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> stationRefit,
            CultMeshDocumentHandle<AetheriaRuntimeSectorMapDocument> sectorMap,
            CultMeshDocumentHandle<AetheriaRuntimeZoneRenderDocument> zoneRender)
        {
            Current = new AetheriaClientCurrentState(currentZone, currentEntity, currentDocking);
            ZoneContacts = zoneContacts ?? throw new ArgumentNullException(nameof(zoneContacts));
            StationRefit = stationRefit ?? throw new ArgumentNullException(nameof(stationRefit));
            SectorMap = sectorMap ?? throw new ArgumentNullException(nameof(sectorMap));
            ZoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
            DockingState = new AetheriaClientDockingState(Current.Entity, Current.Docking, StationRefit);
            _documents = CultMesh.Documents(
                Current.Zone,
                Current.Entity,
                Current.Docking,
                ZoneContacts,
                StationRefit,
                SectorMap,
                ZoneRender);
        }

        public AetheriaClientCurrentState Current { get; }

        public AetheriaClientDockingState DockingState { get; }

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

        public bool IsDocked => StationRefit?.IsDocked == true && CurrentDockingBay != null;
    }
}
