using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Mesh;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeProjectedDocument<TDocument> : IAetheriaRuntimeProjectedDocument
        where TDocument : class
    {
        private readonly CultMeshDocumentHandle<TDocument> _handle;

        internal AetheriaRuntimeProjectedDocument(
            CultMeshDocumentHandle<TDocument> handle)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public string DocumentId => _handle.DocumentId;

        public Type DocumentType => _handle.DocumentType;

        public string SchemaName => _handle.SchemaName;

        public string SchemaVersion => _handle.SchemaVersion;

        public CultMeshRouteHint RouteHint => _handle.RouteHint;

        public System.Collections.Generic.IReadOnlyList<CultMeshProjectionSource> Sources => _handle.Sources;

        public bool CanReplace => _handle.CanReplace;

        public CultMeshDocumentHandle<TDocument> Handle => _handle;

        public ICultMeshDocumentHandle UntypedHandle => _handle;

        public Task<TDocument> LatestAsync()
        {
            return _handle.LatestAsync();
        }

        public TDocument Latest()
        {
            return _handle.LatestAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Observable<TDocument> Watch()
        {
            return _handle.Watch();
        }

        public IDisposable Watch(Action<TDocument> onNext)
        {
            if (onNext == null) throw new ArgumentNullException(nameof(onNext));
            return _handle.Watch(onNext);
        }

        public Task ReplaceAsync(TDocument value)
        {
            return _handle.ReplaceAsync(value);
        }

        internal static AetheriaRuntimeProjectedDocument<TDocument> Create(
            string documentId,
            string runtimeId,
            Func<Task<TDocument>> latest,
            Observable<AetheriaRuntimeDaemonFrameDocument> frameChanges,
            Func<AetheriaRuntimeDaemonFrameDocument, Task<TDocument>> projectFrame,
            params CultMeshProjectionSource[] sources)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Document id must be non-empty.", nameof(documentId));
            if (string.IsNullOrWhiteSpace(runtimeId))
                throw new ArgumentException("Runtime id must be non-empty.", nameof(runtimeId));
            if (latest == null) throw new ArgumentNullException(nameof(latest));
            if (frameChanges == null) throw new ArgumentNullException(nameof(frameChanges));
            if (projectFrame == null) throw new ArgumentNullException(nameof(projectFrame));

            var verse = CultMesh.Verse("aetheria.local", runtimeId);
            var handle = CultMesh.Document(
                documentId,
                verse,
                _ => latest(),
                _ => frameChanges
                    .SelectAwait(async (frame, cancellationToken) =>
                        await projectFrame(frame).ConfigureAwait(false)),
                sources: sources,
                routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed projected state"));

            return new AetheriaRuntimeProjectedDocument<TDocument>(handle);
        }

        public AetheriaRuntimeProjectedDocument<TAlias> AsSchemaAlias<TAlias>() where TAlias : class
        {
            return new AetheriaRuntimeProjectedDocument<TAlias>(_handle.AsSchemaAlias<TAlias>());
        }
    }

    public interface IAetheriaRuntimeProjectedDocument
    {
        Type DocumentType { get; }

        string DocumentId { get; }

        string SchemaName { get; }

        string SchemaVersion { get; }

        CultMeshRouteHint RouteHint { get; }

        System.Collections.Generic.IReadOnlyList<CultMeshProjectionSource> Sources { get; }

        bool CanReplace { get; }

        ICultMeshDocumentHandle UntypedHandle { get; }

        AetheriaRuntimeProjectedDocument<TAlias> AsSchemaAlias<TAlias>() where TAlias : class;
    }

    internal sealed class AetheriaRuntimeUntypedProjectedDocument : IAetheriaRuntimeProjectedDocument
    {
        private readonly ICultMeshDocumentHandle _handle;

        public AetheriaRuntimeUntypedProjectedDocument(ICultMeshDocumentHandle handle)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public Type DocumentType => _handle.DocumentType;

        public string DocumentId => _handle.DocumentId;

        public string SchemaName => _handle.SchemaName;

        public string SchemaVersion => _handle.SchemaVersion;

        public CultMeshRouteHint RouteHint => _handle.RouteHint;

        public System.Collections.Generic.IReadOnlyList<CultMeshProjectionSource> Sources => _handle.Sources;

        public bool CanReplace => _handle.CanReplace;

        public ICultMeshDocumentHandle UntypedHandle => _handle;

        public AetheriaRuntimeProjectedDocument<TAlias> AsSchemaAlias<TAlias>() where TAlias : class
        {
            return new AetheriaRuntimeProjectedDocument<TAlias>(_handle.AsSchemaAlias<TAlias>());
        }
    }

    public sealed class AetheriaClientState
    {
        private readonly IReadOnlyDictionary<Type, object> _documentsByType;
        private readonly CultMeshDocumentCatalog _documents;

        internal AetheriaClientState(
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentZoneDocument> currentZone,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentEntityDocument> currentEntity,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentDockingDocument> currentDocking,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeZoneContactsDocument> zoneContacts,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeStationRefitDocument> stationRefit,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeSectorMapDocument> sectorMap,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeZoneRenderDocument> zoneRender)
        {
            Current = new AetheriaClientCurrentState(currentZone, currentEntity, currentDocking);
            ZoneContacts = zoneContacts ?? throw new ArgumentNullException(nameof(zoneContacts));
            StationRefit = stationRefit ?? throw new ArgumentNullException(nameof(stationRefit));
            SectorMap = sectorMap ?? throw new ArgumentNullException(nameof(sectorMap));
            ZoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
            _documentsByType = new Dictionary<Type, object>
            {
                [typeof(AetheriaRuntimeCurrentZoneDocument)] = Current.Zone,
                [typeof(AetheriaRuntimeCurrentEntityDocument)] = Current.Entity,
                [typeof(AetheriaRuntimeCurrentDockingDocument)] = Current.Docking,
                [typeof(AetheriaRuntimeZoneContactsDocument)] = ZoneContacts,
                [typeof(AetheriaRuntimeStationRefitDocument)] = StationRefit,
                [typeof(AetheriaRuntimeSectorMapDocument)] = SectorMap,
                [typeof(AetheriaRuntimeZoneRenderDocument)] = ZoneRender
            };
            _documents = CultMesh.Documents(_documentsByType
                .Values
                .OfType<IAetheriaRuntimeProjectedDocument>()
                .Select(document => document.UntypedHandle));
        }

        public AetheriaClientCurrentState Current { get; }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeZoneContactsDocument> ZoneContacts { get; }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeStationRefitDocument> StationRefit { get; }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeSectorMapDocument> SectorMap { get; }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

        public bool TryGetDocumentBySchema(
            string schemaVersion,
            out IAetheriaRuntimeProjectedDocument document)
        {
            if (_documents.TryGetDocumentBySchema(schemaVersion, out var handle))
            {
                document = new AetheriaRuntimeUntypedProjectedDocument(handle);
                return true;
            }

            document = null!;
            return false;
        }

        public IAetheriaRuntimeProjectedDocument DocumentBySchema(string schemaVersion)
        {
            if (TryGetDocumentBySchema(schemaVersion, out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria typed state does not expose a projected document for schema '{schemaVersion}'.");
        }

        public bool TryGetDocument<TDocument>(
            out AetheriaRuntimeProjectedDocument<TDocument> document)
            where TDocument : class
        {
            if (_documents.TryGetDocument<TDocument>(out var handle))
            {
                document = new AetheriaRuntimeProjectedDocument<TDocument>(handle);
                return true;
            }

            document = null!;
            return false;
        }

        public AetheriaRuntimeProjectedDocument<TDocument> Document<TDocument>()
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
            return Document<TDocument>().Latest();
        }

        public Observable<TDocument> Watch<TDocument>()
            where TDocument : class
        {
            return Document<TDocument>().Watch();
        }

        public IDisposable Watch<TDocument>(Action<TDocument> onNext)
            where TDocument : class
        {
            return Document<TDocument>().Watch(onNext);
        }

    }

    public sealed class AetheriaClientCurrentState
    {
        internal AetheriaClientCurrentState(
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentZoneDocument> zone,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentEntityDocument> entity,
            AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentDockingDocument> docking)
        {
            Zone = zone ?? throw new ArgumentNullException(nameof(zone));
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Docking = docking ?? throw new ArgumentNullException(nameof(docking));
        }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentZoneDocument> Zone { get; }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentEntityDocument> Entity { get; }

        public AetheriaRuntimeProjectedDocument<AetheriaRuntimeCurrentDockingDocument> Docking { get; }
    }
}
