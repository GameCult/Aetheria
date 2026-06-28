using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using GameCult.Networking;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseRecordKeys
    {
        public static CultRecordKey DaemonProviderAdvertisement { get; } =
            new CultRecordKey("daemon:aetheria.provider_advertisement.v1");

        public static CultRecordKey DaemonHealth { get; } =
            new CultRecordKey("daemon:aetheria.health.v1");

        public static CultRecordKey DaemonCommandBoundary { get; } =
            new CultRecordKey("daemon:aetheria.command_boundary.v1");

        public static CultRecordKey DaemonAssetManifest { get; } =
            new CultRecordKey("daemon:aetheria.asset_manifest.v1");

        public static CultRecordKey VerseAuthorityPolicy { get; } =
            new CultRecordKey(AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey);

        public static CultRecordKey DaemonFrameLatest { get; } =
            new CultRecordKey("daemon:aetheria.frame.latest.v1");

        public static CultRecordKey DaemonSoaViewLatest { get; } =
            new CultRecordKey("daemon:aetheria.soa_view.latest.v1");

        public static CultRecordKey StarbridgeScenarioLatest { get; } =
            new CultRecordKey("starbridge:aetheria.scenario.latest.v1");

        public static CultRecordKey StarbridgeSessionLatest { get; } =
            new CultRecordKey("starbridge:aetheria.session.latest.v1");

        public static CultRecordKey StarbridgeSessionSummary { get; } =
            new CultRecordKey("daemon:aetheria.starbridge.session.latest.v1");

        public static CultRecordKey StarbridgePlayerSeat(string seatId)
        {
            return new CultRecordKey(AetheriaRuntimeStarbridgePlayerSeatDocument.RecordKey(seatId));
        }

        public static CultRecordKey DaemonGameSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game");

        public static CultRecordKey DaemonGameTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game.tui");

        public static CultRecordKey DaemonEditorSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor");

        public static CultRecordKey DaemonEditorTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor.tui");

        public static CultRecordKey DaemonCommand(string commandId)
        {
            return new CultRecordKey($"daemon:commands:{StableToken(commandId)}:gamecult.aetheria.daemon_command.v1");
        }

        public static CultRecordKey EveCommand(string commandId)
        {
            return new CultRecordKey($"eve:commands:{StableToken(commandId)}:gamecult.eve.command.v1");
        }

        public static string StableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            var chars = value
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();
            var token = new string(chars).Trim('-').ToLowerInvariant();
            while (token.Contains("--", StringComparison.Ordinal))
                token = token.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(token) ? "empty" : token;
        }
    }

    public static class AetheriaRuntimeVerseContractRegistry
    {
        private static readonly Type[] RuntimeDocumentTypes =
        {
            typeof(AetheriaRuntimeDaemonProviderAdvertisementDocument),
            typeof(AetheriaRuntimeDaemonHealthDocument),
            typeof(AetheriaRuntimeDaemonCommandBoundaryDocument),
            typeof(AetheriaRuntimeVerseAuthorityPolicyDocument),
            typeof(AetheriaRuntimeDaemonFrameDocument),
            typeof(AetheriaRuntimeDaemonSoaViewDocument),
            typeof(AetheriaRuntimeCatalogSnapshot),
            typeof(AetheriaRuntimeLoadoutTemplatesDocument),
            typeof(AetheriaRuntimeAssetManifestDocument),
            typeof(AetheriaRuntimeObjectsViewportDocument),
            typeof(AetheriaRuntimeGravityViewportDocument),
            typeof(AetheriaRuntimeCurrentZoneDocument),
            typeof(AetheriaRuntimeCurrentEntityDocument),
            typeof(AetheriaRuntimeCurrentDockingDocument),
            typeof(AetheriaRuntimeZoneContactsDocument),
            typeof(AetheriaRuntimeStationRefitDocument),
            typeof(AetheriaRuntimeSectorMapDocument),
            typeof(AetheriaRuntimeZoneDetailsDocument),
            typeof(AetheriaRuntimeZoneRenderDocument),
            typeof(AetheriaRuntimeSelectedObjectDocument),
            typeof(AetheriaRuntimeInventoryDocument),
            typeof(AetheriaRuntimeStarbridgeScenarioDocument),
            typeof(AetheriaRuntimeStarbridgeSessionDocument),
            typeof(AetheriaRuntimeStarbridgeSessionSummaryDocument),
            typeof(AetheriaRuntimeStarbridgePlayerSeatDocument),
            typeof(AetheriaRuntimePlayerSettingsDocument),
            typeof(AetheriaRuntimeVerseHostSettingsDocument),
            typeof(AetheriaRuntimeDaemonCommandDocument),
            typeof(AetheriaRuntimeEveCommandDocument),
            typeof(EveSurfaceState)
        };

        public static CultDocumentRegistry CreateCultCacheRegistry()
        {
            return CultMesh.CreateCultCacheDocumentRegistry(RuntimeDocumentTypes);
        }

        public static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry? cacheRegistry = null)
        {
            var registry = cacheRegistry ?? CreateCultCacheRegistry();
            return CultMesh.CreateCultNetDocumentRegistry(RuntimeDocumentTypes, registry);
        }
    }

    public sealed class AetheriaRuntimeVerseClient : IDisposable
    {
        public const string DefaultRuntimeId = "aetheria-verse-client";

        private readonly CultMeshNode _node;
        private AetheriaClientState? _aetheriaState;
        private AetheriaRuntimeReactiveProjectionInputs? _projectionInputs;
        private bool _disposed;

        private AetheriaRuntimeVerseClient(string statePath, string runtimeId, CultMeshNode node)
        {
            StatePath = statePath;
            RuntimeId = runtimeId;
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string StatePath { get; }

        public string RuntimeId { get; }

        public CultMeshNode Node => _node;

        public CultCache Cache => _node.Cache;

        public CultNetDatabase Database => _node.Database;

        public static async Task<AetheriaRuntimeVerseClient> OpenAsync(
            string statePath,
            string runtimeId = DefaultRuntimeId,
            bool startServer = false,
            bool pullOnOpen = true)
        {
            if (string.IsNullOrWhiteSpace(statePath))
                throw new ArgumentException("State path must be non-empty.", nameof(statePath));

            var fullPath = Path.GetFullPath(statePath);
            var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId;
            var registry = AetheriaRuntimeVerseContractRegistry.CreateCultCacheRegistry();
            var node = await CultMesh.CreateNodeAsync(
                    fullPath,
                    new CultMeshNodeOptions
                    {
                        StartServer = startServer,
                        EnableDurableShardLogs = true,
                        CacheOptions = new CultCacheOpenOptions
                        {
                            Registry = registry,
                            PullOnOpen = pullOnOpen,
                            StoreFlushOnDispose = true,
                            UseDirectoryStore = true
                        },
                        DatabaseOptions = new CultNetDatabaseOptions
                        {
                            RuntimeId = effectiveRuntimeId,
                            DocumentRegistry = AetheriaRuntimeVerseContractRegistry.CreateCultNetRegistry(registry)
                        }
                    })
                .ConfigureAwait(false);

            return new AetheriaRuntimeVerseClient(fullPath, effectiveRuntimeId, node);
        }

        public AetheriaClientState Aetheria()
        {
            ThrowIfDisposed();
            return _aetheriaState ??= CreateAetheriaState();
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonHealthDocument> Health()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeVerseAuthorityPolicyDocument> VerseAuthorityPolicy()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonFrameDocument> LatestFrame()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
        }

        public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgePlayerSeatDocument> StarbridgePlayerSeat(
            string seatId)
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId));
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameTuiSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorTuiSurface()
        {
            ThrowIfDisposed();
            return MutableDocumentPointer<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public CultMeshDocumentHandle<TDocument> Document<TDocument>(
            CultRecordKey key,
            string? documentId = null)
            where TDocument : class
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key.Value))
                throw new ArgumentException("Value must be non-empty.", nameof(key));
            var descriptor = CultDocumentRegistry.Shared.GetRequired<TDocument>();
            return CultMesh.Document<TDocument>(
                Database,
                key,
                CultMesh.Verse("aetheria.local", RuntimeId),
                documentId,
                new[]
                {
                    CultMesh.ProjectionSource(key.ToString(), descriptor.SchemaId, "Aetheria Verse database document")
                },
                new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria Verse database"));
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveDocumentAsync<TDocument>(
            CultRecordKey key,
            CultMeshReactiveDocumentOptions? options = null,
            string? documentId = null)
            where TDocument : class
        {
            return Document<TDocument>(key, documentId).ReactiveAsync(options);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonProviderAdvertisementDocument>>
            WatchProviderAdvertisements()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonHealthDocument>> WatchHealth()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonCommandBoundaryDocument>>
            WatchCommandBoundary()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeVerseAuthorityPolicyDocument>>
            WatchVerseAuthorityPolicies()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonFrameDocument>> WatchLatestFrames()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonSoaViewDocument>> WatchLatestSoaViews()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeStarbridgeScenarioDocument>> WatchStarbridgeScenarios()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeStarbridgeSessionDocument>> WatchStarbridgeSessions()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeStarbridgePlayerSeatDocument>>
            WatchStarbridgePlayerSeat(string seatId)
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId));
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonGameSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonGameTuiSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonEditorSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonEditorTuiSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        internal async Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(
            AetheriaRuntimeDaemonCommandDocument command,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Schema = AetheriaRuntimeDaemonSchemas.Command;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.ClientId))
                command.ClientId = RuntimeId;
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.DaemonCommand(command.CommandId), command)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);

            return AetheriaRuntimeDaemonOperationClient.ToEnvelope(command);
        }

        internal async Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(
            AetheriaRuntimeEveCommandDocument command,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));

            AetheriaRuntimeEveCommandClient.NormalizeDocument(command);
            command.Schema = AetheriaRuntimeEveCommandDocument.SchemaId;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.EveCommand(command.CommandId), command)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);

            return AetheriaRuntimeEveCommandClient.ToEnvelope(command);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitInputSettingsCommandAsync(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string clientId,
            bool flush = true)
        {
            ThrowIfDisposed();
            return SubmitEveCommandAsync(
                AetheriaRuntimeEveCommandClient.ToDocument(
                    AetheriaRuntimeEveCommandClient.CreateInputSettingsCommand(command, body, clientId)),
                flush);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitLoadoutTemplateCommandAsync(
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string clientId,
            bool flush = true)
        {
            ThrowIfDisposed();
            return SubmitEveCommandAsync(
                AetheriaRuntimeEveCommandClient.ToDocument(
                    AetheriaRuntimeEveCommandClient.CreateLoadoutTemplateCommand(loadoutTemplate, clientId)),
                flush);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitKnownSurfaceCommandAsync(
            EveSurfaceCommandRequest request,
            string clientId,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (!AetheriaRuntimeEveCommandClient.TryCreateKnownSurfaceCommand(request, out var envelope))
            {
                throw new InvalidOperationException(
                    $"Unknown Aetheria Eve surface command: {request?.ProviderId}/{request?.SurfaceId}/{request?.Command}");
            }

            var document = AetheriaRuntimeEveCommandClient.ToDocument(envelope!);
            if (!string.IsNullOrWhiteSpace(clientId))
                document.ClientId = clientId;
            return SubmitEveCommandAsync(document, flush);
        }

        public Task FlushAsync(bool soft = false)
        {
            ThrowIfDisposed();
            return _node.FlushAsync(soft);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _projectionInputs?.Dispose();
            _node.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AetheriaRuntimeVerseClient));
        }

        private AetheriaClientState CreateAetheriaState()
        {
            var frameChanges = WatchLatestFrames()
                .Where(change => change.Document != null)
                .Select(change => change.Document!);
            var catalogDocument = BootstrapCatalogDocument(
                "aetheria.catalog.runtime",
                () => Task.FromResult(BootstrapRuntimeCatalogSnapshot()),
                AetheriaRuntimeCatalogSnapshot.SchemaId,
                CatalogBootstrapSource("catalog:aetheria.runtime"));
            var loadoutTemplatesDocument = BootstrapCatalogDocument(
                "aetheria.catalog.loadout_templates",
                () => Task.FromResult(BootstrapLoadoutTemplatesDocument()),
                AetheriaRuntimeLoadoutTemplatesDocument.SchemaId,
                CatalogBootstrapSource("catalog:aetheria.loadout_templates"));
            var starbridgeScenarioDocument = Document<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
            var starbridgeSessionDocument = Document<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
            var projectionInputs = new AetheriaRuntimeReactiveProjectionInputs(
                catalogDocument.Reactive(),
                loadoutTemplatesDocument.Reactive(),
                starbridgeScenarioDocument.Reactive(),
                starbridgeSessionDocument.Reactive(),
                BootstrapRuntimeCatalogSnapshot(),
                BootstrapLoadoutTemplatesDocument());
            _projectionInputs = projectionInputs;

            return new AetheriaClientState(
                Document<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement),
                Document<AetheriaRuntimeDaemonHealthDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonHealth),
                Document<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary),
                Document<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                    AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy),
                Document<AetheriaRuntimeDaemonFrameDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest),
                Document<AetheriaRuntimeDaemonSoaViewDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonGameSurface),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface),
                Document<EveSurfaceState>(
                    AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface),
                catalogDocument,
                loadoutTemplatesDocument,
                BootstrapCatalogDocument(
                    "aetheria.settings.player",
                    () => Task.FromResult(BootstrapPlayerSettingsDocument()),
                    AetheriaRuntimePlayerSettingsDocument.SchemaId,
                    CatalogBootstrapSource("catalog:aetheria.player_settings")),
                BootstrapCatalogDocument(
                    "aetheria.settings.verse_host",
                    () => Task.FromResult(BootstrapVerseHostSettingsDocument()),
                    AetheriaRuntimeVerseHostSettingsDocument.SchemaId,
                    CatalogBootstrapSource("catalog:aetheria.verse_host_settings")),
                ProjectedDocument(
                    "aetheria.current.zone",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectCurrentZone(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentZone),
                ProjectedDocument(
                    "aetheria.current.entity",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectCurrentEntity(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentEntity),
                ProjectedDocument(
                    "aetheria.current.docking",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectCurrentDocking(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentDocking),
                ProjectedDocument(
                    "aetheria.zone.contacts",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectZoneContacts(frame)),
                    AetheriaRuntimeDaemonSchemas.ZoneContacts),
                ProjectedDocument(
                    "aetheria.station.refit",
                    ProjectStationRefitAsync,
                    AetheriaRuntimeDaemonSchemas.StationRefit,
                    CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                    CultMesh.ProjectionSource("loadout-templates:aetheria.runtime")),
                ProjectedDocument(
                    "aetheria.sector.map",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectSectorMap(frame)),
                    AetheriaRuntimeDaemonSchemas.SectorMap),
                ProjectedDocument(
                    "aetheria.zone.render",
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectZoneRender(frame)),
                    AetheriaRuntimeDaemonSchemas.ZoneRender),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.map", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.RtsViewport),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.objects", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectObjectsViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.gravity", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectGravityViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.GravityViewport),
                viewport => ProjectedDocument(
                    ViewportDocumentId("aetheria.viewport.render_splats", viewport),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectRenderSplatsViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                zoneIndex => ProjectedDocument(
                    IndexedDocumentId("aetheria.zone.details", zoneIndex),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectZoneDetails(frame, zoneIndex)),
                    AetheriaRuntimeDaemonSchemas.ZoneDetails),
                entityIndex => ProjectedDocument(
                    IndexedDocumentId("aetheria.object.selected", entityIndex),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectSelectedObject(frame, entityIndex)),
                    AetheriaRuntimeDaemonSchemas.SelectedObject),
                entityIndex => ProjectedDocument(
                    IndexedDocumentId("aetheria.inventory", entityIndex),
                    frame => Task.FromResult(AetheriaRuntimeRtsProjection.ProjectInventory(frame, entityIndex)),
                    AetheriaRuntimeDaemonSchemas.Inventory),
                starbridgeScenarioDocument,
                starbridgeSessionDocument,
                ProjectedDocument(
                    "aetheria.starbridge.summary",
                    ProjectStarbridgeSummaryAsync,
                    AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest.ToString()),
                    CultMesh.ProjectionSource("catalog:aetheria.runtime")),
                seatId => Document<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                    AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId)));

            CultMeshDocumentHandle<TDocument> ProjectedDocument<TDocument>(
                string documentId,
                Func<AetheriaRuntimeDaemonFrameDocument, Task<TDocument>> project,
                string schemaId,
                params CultMeshProjectionSource[] additionalSources)
                where TDocument : class
            {
                var sources = new[]
                    {
                        CultMesh.ProjectionSource(
                            AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                            AetheriaRuntimeDaemonSchemas.Frame,
                            "latest authoritative daemon frame")
                    }
                    .Concat(additionalSources ?? Array.Empty<CultMeshProjectionSource>())
                    .Append(CultMesh.ProjectionSource(documentId, schemaId, "projected Aetheria client document"))
                    .ToArray();

                var verse = CultMesh.Verse("aetheria.local", RuntimeId);
                return CultMesh.Document(
                    documentId,
                    verse,
                    async _ => await project(await RequireFrameAsync().ConfigureAwait(false)).ConfigureAwait(false),
                    _ => frameChanges
                        .SelectAwait(async (frame, cancellationToken) =>
                            await project(frame).ConfigureAwait(false)),
                    sources: sources,
                    routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria typed projected state"));
            }

            CultMeshDocumentHandle<TDocument> BootstrapCatalogDocument<TDocument>(
                string documentId,
                Func<Task<TDocument>> bootstrap,
                string schemaId,
                params CultMeshProjectionSource[] additionalSources)
                where TDocument : class
            {
                var sources = (additionalSources ?? Array.Empty<CultMeshProjectionSource>())
                    .Append(CultMesh.ProjectionSource(documentId, schemaId, "managed Aetheria catalog document"))
                    .ToArray();

                var verse = CultMesh.Verse("aetheria.local", RuntimeId);
                return CultMesh.Document(
                    documentId,
                    verse,
                    async _ => await bootstrap().ConfigureAwait(false),
                    _ => frameChanges
                        .SelectAwait(async (_, cancellationToken) =>
                            await bootstrap().ConfigureAwait(false)),
                    sources: sources,
                    routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria managed catalog state"));
            }

            Task<AetheriaRuntimeStationRefitDocument> ProjectStationRefitAsync(
                AetheriaRuntimeDaemonFrameDocument frame)
            {
                return Task.FromResult(AetheriaRuntimeRtsProjection.ProjectStationRefit(
                    frame,
                    projectionInputs.LoadoutTemplates.Templates,
                    projectionInputs.Catalog));
            }

            Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> ProjectStarbridgeSummaryAsync(
                AetheriaRuntimeDaemonFrameDocument frame)
            {
                return Task.FromResult(AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(
                    frame,
                    projectionInputs.StarbridgeScenario,
                    projectionInputs.StarbridgeSession,
                    projectionInputs.Catalog));
            }

            static string IndexedDocumentId(string prefix, int index)
            {
                return $"{prefix}.{index.ToString(CultureInfo.InvariantCulture)}";
            }

            static string ViewportDocumentId(string prefix, AetheriaRuntimeRtsViewportBounds viewport)
            {
                var normalized = AetheriaRuntimeRtsProjection.Normalize(viewport);
                return string.Join(
                    ".",
                    prefix,
                    ViewportToken(normalized.MinX),
                    ViewportToken(normalized.MinY),
                    ViewportToken(normalized.MaxX),
                    ViewportToken(normalized.MaxY));
            }

            static string ViewportToken(double value)
            {
                return value
                    .ToString("0.###", CultureInfo.InvariantCulture)
                    .Replace('-', 'n')
                    .Replace('.', 'p');
            }
        }

        private async Task<AetheriaRuntimeDaemonFrameDocument> RequireFrameAsync()
        {
            var frame = await Aetheria().Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
            if (frame == null)
                throw new InvalidOperationException("Aetheria Verse client has no daemon frame yet.");
            return frame;
        }

        private CultMeshMutableStatePointer<T> MutableDocumentPointer<T>(CultRecordKey key) where T : class
        {
            return CultMesh.MutableStatePointer(
                key.ToString(),
                _ => Database.GetAsync<T>(key),
                _ => Database.WatchRecord<T>(key)
                    .Where(change => change.Document != null)
                    .Select(change => change.Document!),
                async (_, value) => { await Database.PutAsync(key, value).ConfigureAwait(false); },
                sources: new[]
                {
                    CultMesh.ProjectionSource(key.ToString())
                });
        }

        private static CultMeshProjectionSource CatalogBootstrapSource(string sourceId)
        {
            return CultMesh.ProjectionSource(
                sourceId,
                description: "legacy catalog store bootstrap seed for managed Aetheria state");
        }

        private sealed class AetheriaRuntimeReactiveProjectionInputs : IDisposable
        {
            private readonly CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
            private readonly CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> _loadoutTemplates;
            private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> _starbridgeScenario;
            private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> _starbridgeSession;
            private readonly AetheriaRuntimeCatalogSnapshot _fallbackCatalog;
            private readonly AetheriaRuntimeLoadoutTemplatesDocument _fallbackLoadoutTemplates;

            public AetheriaRuntimeReactiveProjectionInputs(
                CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> catalog,
                CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> loadoutTemplates,
                CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> starbridgeScenario,
                CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> starbridgeSession,
                AetheriaRuntimeCatalogSnapshot fallbackCatalog,
                AetheriaRuntimeLoadoutTemplatesDocument fallbackLoadoutTemplates)
            {
                _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
                _loadoutTemplates = loadoutTemplates ?? throw new ArgumentNullException(nameof(loadoutTemplates));
                _starbridgeScenario = starbridgeScenario ?? throw new ArgumentNullException(nameof(starbridgeScenario));
                _starbridgeSession = starbridgeSession ?? throw new ArgumentNullException(nameof(starbridgeSession));
                _fallbackCatalog = fallbackCatalog ?? throw new ArgumentNullException(nameof(fallbackCatalog));
                _fallbackLoadoutTemplates = fallbackLoadoutTemplates ?? throw new ArgumentNullException(nameof(fallbackLoadoutTemplates));
            }

            public AetheriaRuntimeCatalogSnapshot Catalog => _catalog.Current ?? _fallbackCatalog;
            public AetheriaRuntimeLoadoutTemplatesDocument LoadoutTemplates => _loadoutTemplates.Current ?? _fallbackLoadoutTemplates;
            public AetheriaRuntimeStarbridgeScenarioDocument? StarbridgeScenario => _starbridgeScenario.Current;
            public AetheriaRuntimeStarbridgeSessionDocument? StarbridgeSession => _starbridgeSession.Current;

            public void Dispose()
            {
                _catalog.Dispose();
                _loadoutTemplates.Dispose();
                _starbridgeScenario.Dispose();
                _starbridgeSession.Dispose();
            }
        }

        private AetheriaRuntimeCatalogSnapshot BootstrapRuntimeCatalogSnapshot()
        {
            return AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath);
        }

        private AetheriaRuntimeLoadoutTemplatesDocument BootstrapLoadoutTemplatesDocument()
        {
            return new AetheriaRuntimeLoadoutTemplatesDocument(
                AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(StatePath));
        }

        private AetheriaRuntimePlayerSettingsDocument BootstrapPlayerSettingsDocument()
        {
            return AetheriaRuntimePlayerSettingsDocument.FromSnapshot(
                AetheriaRuntimeCatalogStore.ReadPlayerSettings(StatePath));
        }

        private AetheriaRuntimeVerseHostSettingsDocument BootstrapVerseHostSettingsDocument()
        {
            return AetheriaRuntimeVerseHostSettingsDocument.FromSnapshot(
                AetheriaRuntimeCatalogStore.ReadVerseHostSettings(StatePath));
        }
    }
}
