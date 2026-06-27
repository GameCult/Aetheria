using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using GameCult.Caching;
using GameCult.Mesh;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public readonly struct AetheriaRuntimeStateQueryParameters
    {
        public static readonly AetheriaRuntimeStateQueryParameters Empty = new AetheriaRuntimeStateQueryParameters();
    }

    public sealed class AetheriaRuntimeProjectedDocument<TDocument> : IAetheriaRuntimeProjectedDocument
    {
        private static readonly CultMeshPollingWatchOptions<TDocument> DefaultWatchOptions =
            new CultMeshPollingWatchOptions<TDocument>(TimeSpan.FromMilliseconds(100));
        private static readonly CultDocumentAttribute? DocumentAttribute =
            typeof(TDocument).GetCustomAttribute<CultDocumentAttribute>();

        private readonly CultMeshBoundLiveFeed<AetheriaRuntimeStateQueryParameters, TDocument> _feed;

        internal AetheriaRuntimeProjectedDocument(
            CultMeshBoundLiveFeed<AetheriaRuntimeStateQueryParameters, TDocument> feed)
        {
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        }

        public string DocumentId => _feed.FeedId;

        public Type DocumentType => typeof(TDocument);

        public string SchemaName => DocumentAttribute?.SchemaName ?? "";

        public string SchemaVersion => DocumentAttribute?.SchemaVersion ?? "";

        public CultMeshRouteHint RouteHint => _feed.RouteHint;

        public System.Collections.Generic.IReadOnlyList<CultMeshProjectionSource> Sources => _feed.Sources;

        public CultMeshLiveFeed<AetheriaRuntimeStateQueryParameters, TDocument> Feed => _feed.Feed;

        public Task<TDocument> LatestAsync()
        {
            return _feed.SnapshotAsync(AetheriaRuntimeStateQueryParameters.Empty);
        }

        public Observable<TDocument> Watch()
        {
            return _feed.Watch(AetheriaRuntimeStateQueryParameters.Empty);
        }

        public IDisposable Watch(Action<TDocument> onNext)
        {
            if (onNext == null) throw new ArgumentNullException(nameof(onNext));
            return Watch().Subscribe(onNext);
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

            var feed = new CultMeshLiveFeed<AetheriaRuntimeStateQueryParameters, TDocument>(
                documentId,
                (_parameters, _context) => latest(),
                (_parameters, context) => frameChanges
                    .SelectAwait(async (frame, cancellationToken) =>
                        await projectFrame(frame).ConfigureAwait(false)),
                sources: sources,
                routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed projected state"));

            var verse = CultMesh.Verse("aetheria.local", runtimeId);
            return new AetheriaRuntimeProjectedDocument<TDocument>(feed.Bind(verse));
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
    }

    public sealed class AetheriaClientState
    {
        private readonly IReadOnlyDictionary<Type, object> _documentsByType;
        private readonly IReadOnlyDictionary<string, IAetheriaRuntimeProjectedDocument> _documentsBySchema;

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
            _documentsBySchema = BuildSchemaIndex(_documentsByType.Values);
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
            if (!string.IsNullOrWhiteSpace(schemaVersion) &&
                _documentsBySchema.TryGetValue(schemaVersion, out document!))
            {
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
        {
            if (_documentsByType.TryGetValue(typeof(TDocument), out var untypedDocument) &&
                untypedDocument is AetheriaRuntimeProjectedDocument<TDocument> typedDocument)
            {
                document = typedDocument;
                return true;
            }

            var schemaVersion = SchemaVersionFor(typeof(TDocument));
            if (!string.IsNullOrWhiteSpace(schemaVersion) &&
                _documentsBySchema.TryGetValue(schemaVersion, out var schemaDocument) &&
                schemaDocument is AetheriaRuntimeProjectedDocument<TDocument> schemaTypedDocument)
            {
                document = schemaTypedDocument;
                return true;
            }

            document = null!;
            return false;
        }

        public AetheriaRuntimeProjectedDocument<TDocument> Document<TDocument>()
        {
            if (TryGetDocument<TDocument>(out var document))
                return document;

            throw new NotSupportedException(
                $"Aetheria typed state does not expose a projected document for {typeof(TDocument).FullName}.");
        }

        public Task<TDocument> LatestAsync<TDocument>()
        {
            return Document<TDocument>().LatestAsync();
        }

        public Observable<TDocument> Watch<TDocument>()
        {
            return Document<TDocument>().Watch();
        }

        public IDisposable Watch<TDocument>(Action<TDocument> onNext)
        {
            return Document<TDocument>().Watch(onNext);
        }

        private static IReadOnlyDictionary<string, IAetheriaRuntimeProjectedDocument> BuildSchemaIndex(
            IEnumerable<object> documents)
        {
            var index = new Dictionary<string, IAetheriaRuntimeProjectedDocument>(StringComparer.Ordinal);
            foreach (var document in documents)
            {
                if (document is not IAetheriaRuntimeProjectedDocument projectedDocument)
                    continue;

                if (!string.IsNullOrWhiteSpace(projectedDocument.SchemaVersion))
                    index[projectedDocument.SchemaVersion] = projectedDocument;
                if (!string.IsNullOrWhiteSpace(projectedDocument.SchemaName))
                    index[projectedDocument.SchemaName] = projectedDocument;
            }

            return index;
        }

        private static string SchemaVersionFor(Type documentType)
        {
            return documentType.GetCustomAttribute<CultDocumentAttribute>()?.SchemaVersion ?? "";
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
