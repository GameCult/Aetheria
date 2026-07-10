using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using GameCult.Networking;
using R3;
using EveUiCommandRequest = GameCult.Eve.Surface.EveSurfaceCommandRequest;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseRecordKeys
    {
        public static CultRecordKey DaemonProviderAdvertisement { get; } =
            new CultRecordKey("daemon:aetheria.provider_advertisement.v1");

        public static CultRecordKey EveProviderAdvertisement { get; } =
            new CultRecordKey("eve:provider:aetheria.daemon");

        public const string EveCommandRecordPrefix = "eve:commands:aetheria.daemon";

        public const string EveReceiptRecordPrefix = "eve:receipts:aetheria.daemon";

        public static CultRecordKey DaemonHealth { get; } =
            new CultRecordKey("daemon:aetheria.health.v1");

        public static CultRecordKey DaemonCommandBoundary { get; } =
            new CultRecordKey("daemon:aetheria.command_boundary.v1");

        public static CultRecordKey DaemonAssetManifest { get; } =
            new CultRecordKey("daemon:aetheria.asset_manifest.latest.v1");

        public static CultRecordKey EveAssetCatalog { get; } =
            new CultRecordKey("eve:assets:aetheria.daemon");

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

        public static CultRecordKey StarbridgeCommanderSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.starbridge.commander");

        public static CultRecordKey DaemonGameTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game.tui");

        public static CultRecordKey DaemonEditorSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor");

        public static CultRecordKey DaemonEditorTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor.tui");

        public static CultRecordKey MainMenuSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.main_menu.root");

        public static CultRecordKey MainMenuSettingsSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.main_menu.settings");

        public static CultRecordKey MainMenuInputSettingsSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.main_menu.input_settings");

        public static CultRecordKey MainMenuPlayerSettingsSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.main_menu.player_settings");

        public static CultRecordKey MainMenuVerseSettingsSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.main_menu.verse_settings");

        public static CultRecordKey InventoryPanelSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.inventory.panel");

        public static CultRecordKey InventoryDropdownSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.inventory.panel.dropdown");

        public static CultRecordKey MapMenuSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.map.zone_details");

        public static CultRecordKey TradeMenuSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.trade.menu");

        public static CultRecordKey DaemonCommand(string commandId)
        {
            return new CultRecordKey($"daemon:commands:{StableToken(commandId)}:gamecult.aetheria.daemon_command.v1");
        }

        public static CultRecordKey EveCommand(string commandId)
        {
            return new CultRecordKey($"eve:commands:{StableToken(commandId)}:gamecult.eve.command.v1");
        }

        public static CultRecordKey EveReceiptForCommand(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Eve receipt command id must be non-empty.", nameof(commandId));
            return new CultRecordKey($"{EveReceiptRecordPrefix}:{commandId}");
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
            typeof(AetheriaRuntimeSurfaceDocument),
            typeof(EveProviderAdvertisementDocument),
            typeof(EveSurfaceDocument),
            typeof(EveSurfaceCommandRequest),
            typeof(EveCommandReceiptDocument),
            typeof(EveAssetCatalogDocument),
            typeof(AetheriaRuntimeDaemonCommandDocument),
            typeof(AetheriaRuntimeEveCommandDocument),
            typeof(AetheriaRuntimeCommittedCommandFactDocument)
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
        private const string RemoteShardId = "primary";

        private readonly CultMeshNode _node;
        private AetheriaClientState? _aetheriaState;
        private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? _managedDaemonFrame;
        private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>? _managedCatalog;
        private CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument>? _managedLoadoutTemplates;
        private CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument>? _managedPlayerSettings;
        private CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument>? _managedStarbridgeScenario;
        private CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument>? _managedStarbridgeSession;
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

        public string RemoteEndpoint { get; private set; } = "";

        public bool IsRemoteReplica => !string.IsNullOrWhiteSpace(RemoteEndpoint);

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

        public static async Task<AetheriaRuntimeVerseClient> OpenRemoteAsync(
            string replicaStatePath,
            string endpoint,
            string runtimeId = DefaultRuntimeId,
            bool pullOnOpen = false,
            bool synchronizeOnOpen = true)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("CultMesh endpoint must be non-empty.", nameof(endpoint));

            var fullPath = Path.GetFullPath(replicaStatePath ?? throw new ArgumentNullException(nameof(replicaStatePath)));
            var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId;
            var registry = AetheriaRuntimeVerseContractRegistry.CreateCultCacheRegistry();
            var shard = new CultNetShardDescriptor(
                RemoteShardId,
                "aetheria-daemon",
                epoch: 1,
                isPrimary: false,
                primaryEndpoints: new[] { endpoint.Trim() });
            var node = await CultMesh.CreateNodeAsync(
                    fullPath,
                    new CultMeshNodeOptions
                    {
                        StartServer = false,
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
                            Shards = new[] { shard },
                            DocumentRegistry = AetheriaRuntimeVerseContractRegistry.CreateCultNetRegistry(registry)
                        }
                    })
                .ConfigureAwait(false);

            var client = new AetheriaRuntimeVerseClient(fullPath, effectiveRuntimeId, node)
            {
                RemoteEndpoint = endpoint.Trim()
            };
            if (synchronizeOnOpen)
                await client.RefreshRemoteAsync().ConfigureAwait(false);
            return client;
        }

        public async Task<int> RefreshRemoteAsync(
            IReadOnlyList<string>? recordKeys = null,
            TimeSpan? connectTimeout = null,
            TimeSpan? responseTimeout = null)
        {
            ThrowIfDisposed();
            if (!IsRemoteReplica)
                throw new InvalidOperationException("Remote refresh requires a client opened with OpenRemoteAsync.");

            var snapshot = CultMesh.SnapshotEndpoint(
                RemoteEndpoint,
                new CultMeshSnapshotEndpointOptions
                {
                    Context = CultMesh.Verse("aetheria.remote", RuntimeId).Context,
                    DocumentRegistry = AetheriaRuntimeVerseContractRegistry.CreateCultNetRegistry(),
                    Request = new CultMeshSnapshotRequestOptions
                    {
                        RecordKeys = recordKeys,
                        ShardId = RemoteShardId,
                        ShardEpoch = 1,
                        ConnectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5),
                        ResponseTimeout = responseTimeout ?? TimeSpan.FromSeconds(10),
                        MessageIdPrefix = "aetheria-unity",
                        RudpRuntimeId = RuntimeId,
                        RudpMaxFragmentBytes = 1200
                    }
                });
            var result = await snapshot.SyncSnapshotAsync(_node).ConfigureAwait(false);
            await _node.FlushAsync().ConfigureAwait(false);
            return result.AppliedCount;
        }

        public AetheriaClientState Aetheria()
        {
            ThrowIfDisposed();
            return _aetheriaState ??= CreateAetheriaState();
        }

        public CultMeshMutableStatePointer<TDocument> MutableDocument<TDocument>(
            CultRecordKey key)
            where TDocument : class
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key.Value))
                throw new ArgumentException("Value must be non-empty.", nameof(key));
            return MutableDocumentPointer<TDocument>(key);
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

        public Observable<CultNetDatabaseChange<TDocument>> WatchRecord<TDocument>(
            CultRecordKey key)
            where TDocument : class
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key.Value))
                throw new ArgumentException("Value must be non-empty.", nameof(key));
            return Database.WatchRecord<TDocument>(key);
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
            EveUiCommandRequest request,
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
            _aetheriaState?.Dispose();
            _aetheriaState = null;
            _managedDaemonFrame?.Dispose();
            _managedCatalog?.Dispose();
            _managedLoadoutTemplates?.Dispose();
            _managedPlayerSettings?.Dispose();
            _managedStarbridgeScenario?.Dispose();
            _managedStarbridgeSession?.Dispose();
            _node.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AetheriaRuntimeVerseClient));
        }

        private AetheriaClientState CreateAetheriaState()
        {
            var frameChanges = WatchRecord<AetheriaRuntimeDaemonFrameDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
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
            var playerSettingsDocument = BootstrapCatalogDocument(
                "aetheria.settings.player",
                () => Task.FromResult(BootstrapPlayerSettingsDocument()),
                AetheriaRuntimePlayerSettingsDocument.SchemaId,
                CatalogBootstrapSource("catalog:aetheria.player_settings"));
            var verseHostSettingsDocument = BootstrapCatalogDocument(
                "aetheria.settings.verse_host",
                () => Task.FromResult(BootstrapVerseHostSettingsDocument()),
                AetheriaRuntimeVerseHostSettingsDocument.SchemaId,
                CatalogBootstrapSource("catalog:aetheria.verse_host_settings"));
            var starbridgeScenarioDocument = Document<AetheriaRuntimeStarbridgeScenarioDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest);
            var starbridgeSessionDocument = Document<AetheriaRuntimeStarbridgeSessionDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest);
            var latestFrameDocument = Document<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
            CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? managedDaemonFrame = null;
            CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>? managedCatalog = null;
            CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument>? managedLoadoutTemplates = null;
            CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument>? managedPlayerSettings = null;
            CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument>? managedStarbridgeScenario = null;
            CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument>? managedStarbridgeSession = null;

            var state = new AetheriaClientState(
                Document<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement),
                Document<AetheriaRuntimeDaemonHealthDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonHealth),
                Document<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary),
                Document<AetheriaRuntimeAssetManifestDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest),
                Document<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                    AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy),
                latestFrameDocument,
                Document<AetheriaRuntimeDaemonSoaViewDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest),
                Document<EveSurfaceDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonGameSurface),
                Document<EveSurfaceDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface),
                Document<EveSurfaceDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface),
                Document<EveSurfaceDocument>(
                    AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface),
                catalogDocument,
                loadoutTemplatesDocument,
                playerSettingsDocument,
                verseHostSettingsDocument,
                ManagedFrameDocument(
                    "aetheria.current.zone",
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.CurrentZone(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentZone),
                ManagedFrameDocument(
                    "aetheria.current.entity",
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.CurrentEntity(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentEntity),
                ManagedFrameDocument(
                    "aetheria.current.docking",
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.CurrentDocking(frame)),
                    AetheriaRuntimeDaemonSchemas.CurrentDocking),
                ManagedFrameDocument(
                    "aetheria.zone.contacts",
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.ZoneContacts(frame)),
                    AetheriaRuntimeDaemonSchemas.ZoneContacts),
                ManagedFrameDocument(
                    "aetheria.station.refit",
                    StationRefitAsync,
                    AetheriaRuntimeDaemonSchemas.StationRefit,
                    CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                    CultMesh.ProjectionSource("loadout-templates:aetheria.runtime")),
                ManagedFrameDocument(
                    "aetheria.sector.map",
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.SectorMap(frame)),
                    AetheriaRuntimeDaemonSchemas.SectorMap),
                ManagedFrameDocument(
                    "aetheria.zone.render",
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.ZoneRender(frame)),
                    AetheriaRuntimeDaemonSchemas.ZoneRender),
                viewport => ManagedFrameDocument(
                    ViewportDocumentId("aetheria.viewport.map", viewport),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.Viewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.GameViewport),
                viewport => ManagedFrameDocument(
                    ViewportDocumentId("aetheria.viewport.objects", viewport),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.ObjectsViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                viewport => ManagedFrameDocument(
                    ViewportDocumentId("aetheria.viewport.gravity", viewport),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.GravityViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.GravityViewport),
                viewport => ManagedFrameDocument(
                    ViewportDocumentId("aetheria.viewport.render_splats", viewport),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.RenderSplatsViewport(frame, viewport)),
                    AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                zoneIndex => ManagedFrameDocument(
                    IndexedDocumentId("aetheria.zone.details", zoneIndex),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.ZoneDetails(frame, zoneIndex)),
                    AetheriaRuntimeDaemonSchemas.ZoneDetails),
                zoneIndex => ManagedFrameDocument(
                    IndexedDocumentId("aetheria.zone.details.surface", zoneIndex),
                    frame => ZoneDetailsSurfaceAsync(frame, zoneIndex),
                    AetheriaRuntimeZoneDetailsSurfaceBuilder.SurfaceId,
                    CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                    CultMesh.ProjectionSource("catalog:aetheria.player_settings")),
                request => ManagedFrameDocument(
                    InventoryPanelSurfaceDocumentId(request),
                    frame => InventoryPanelSurfaceAsync(frame, request),
                    AetheriaRuntimeInventoryPanelSurfaceBuilder.SurfaceId,
                    CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                    CultMesh.ProjectionSource("catalog:aetheria.player_settings"),
                    CultMesh.ProjectionSource("loadout-templates:aetheria.runtime")),
                request => ManagedFrameDocument(
                    InventoryDropdownSurfaceDocumentId(request),
                    frame => InventoryDropdownSurfaceAsync(frame, request),
                    AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId,
                    CultMesh.ProjectionSource("catalog:aetheria.runtime"),
                    CultMesh.ProjectionSource("loadout-templates:aetheria.runtime")),
                (surfaceId, canOpenRuntimeInputScreen, inGame) => ManagedMainMenuSurfaceDocument(
                    surfaceId,
                    canOpenRuntimeInputScreen,
                    inGame),
                entityIndex => ManagedFrameDocument(
                    IndexedDocumentId("aetheria.object.selected", entityIndex),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.SelectedObject(frame, entityIndex)),
                    AetheriaRuntimeDaemonSchemas.SelectedObject),
                entityIndex => ManagedFrameDocument(
                    IndexedDocumentId("aetheria.inventory", entityIndex),
                    frame => Task.FromResult(AetheriaRuntimeGameDocuments.Inventory(frame, entityIndex)),
                    AetheriaRuntimeDaemonSchemas.Inventory),
                starbridgeScenarioDocument,
                starbridgeSessionDocument,
                ManagedFrameDocument(
                    "aetheria.starbridge.summary",
                    StarbridgeSummaryAsync,
                    AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest.ToString()),
                    CultMesh.ProjectionSource("catalog:aetheria.runtime")),
                seatId => Document<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                    AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seatId)));

            managedPlayerSettings = state.PlayerSettings.Reactive();
            _managedPlayerSettings = managedPlayerSettings;
            return state;

            CultMeshDocumentHandle<TDocument> ManagedFrameDocument<TDocument>(
                string documentId,
                Func<AetheriaRuntimeDaemonFrameDocument, Task<TDocument>> derive,
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
                    .Append(CultMesh.ProjectionSource(documentId, schemaId, "managed Aetheria client document"))
                    .ToArray();

                var verse = CultMesh.Verse("aetheria.local", RuntimeId);
                return CultMesh.Document(
                    documentId,
                    verse,
                    async _ => await derive(RequireManagedFrame()).ConfigureAwait(false),
                    _ => frameChanges
                        .SelectAwait(async (frame, cancellationToken) =>
                            await derive(frame).ConfigureAwait(false)),
                    sources: sources,
                    routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria managed typed state"));
            }

            AetheriaRuntimeDaemonFrameDocument RequireManagedFrame()
            {
                managedDaemonFrame ??= latestFrameDocument.Reactive();
                _managedDaemonFrame = managedDaemonFrame;
                return managedDaemonFrame?.Current
                    ?? throw new InvalidOperationException("Aetheria Verse client has no daemon frame yet.");
            }

            AetheriaRuntimeCatalogSnapshot RequireManagedCatalog()
            {
                managedCatalog ??= catalogDocument.Reactive();
                _managedCatalog = managedCatalog;
                return managedCatalog?.Current
                    ?? throw new InvalidOperationException("Aetheria Verse client has no runtime catalog document yet.");
            }

            AetheriaRuntimeLoadoutTemplatesDocument RequireManagedLoadoutTemplates()
            {
                managedLoadoutTemplates ??= loadoutTemplatesDocument.Reactive();
                _managedLoadoutTemplates = managedLoadoutTemplates;
                return managedLoadoutTemplates?.Current
                    ?? throw new InvalidOperationException("Aetheria Verse client has no loadout templates document yet.");
            }

            AetheriaRuntimePlayerSettingsDocument RequireManagedPlayerSettings()
            {
                return managedPlayerSettings?.Current
                    ?? throw new InvalidOperationException("Aetheria Verse client has no player settings document yet.");
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

            CultMeshDocumentHandle<AetheriaRuntimeSurfaceDocument> ManagedMainMenuSurfaceDocument(
                string surfaceId,
                bool canOpenRuntimeInputScreen,
                bool inGame)
            {
                surfaceId = string.IsNullOrWhiteSpace(surfaceId)
                    ? AetheriaRuntimeMainMenuCommands.RootSurfaceId
                    : surfaceId;
                var documentId = string.Join(
                    ".",
                    "aetheria.main_menu.surface",
                    AetheriaRuntimeVerseRecordKeys.StableToken(surfaceId),
                    canOpenRuntimeInputScreen ? "input-open" : "input-closed",
                    inGame ? "in-game" : "title");
                var descriptor = CultDocumentRegistry.Shared.GetRequired<AetheriaRuntimeSurfaceDocument>();
                var sources = new[]
                {
                    CultMesh.ProjectionSource(documentId, descriptor.SchemaId, "managed Aetheria main menu surface"),
                    CultMesh.ProjectionSource("catalog:aetheria.player_settings"),
                    CultMesh.ProjectionSource("catalog:aetheria.verse_host_settings"),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString())
                };
                var verse = CultMesh.Verse("aetheria.local", RuntimeId);
                return CultMesh.Document(
                    documentId,
                    verse,
                    _ => Task.FromResult(BuildMainMenuSurface(surfaceId, canOpenRuntimeInputScreen, inGame)),
                    _ => frameChanges
                        .Select(_ => BuildMainMenuSurface(surfaceId, canOpenRuntimeInputScreen, inGame)),
                    sources: sources,
                    routeHint: new CultMeshRouteHint(CultMeshLocalityKind.SharedMemory, "Aetheria managed main menu surface"));
            }

            Task<AetheriaRuntimeStationRefitDocument> StationRefitAsync(
                AetheriaRuntimeDaemonFrameDocument frame)
            {
                return Task.FromResult(AetheriaRuntimeGameDocuments.StationRefit(
                    frame,
                    RequireManagedLoadoutTemplates().Templates,
                    RequireManagedCatalog()));
            }

            Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSummaryAsync(
                AetheriaRuntimeDaemonFrameDocument frame)
            {
                return Task.FromResult(AetheriaRuntimeStarbridgeDocuments.SessionSummary(
                    frame,
                    CurrentStarbridgeScenario(),
                    CurrentStarbridgeSession(),
                    RequireManagedCatalog()));
            }

            AetheriaRuntimeStarbridgeScenarioDocument? CurrentStarbridgeScenario()
            {
                try
                {
                    managedStarbridgeScenario ??= starbridgeScenarioDocument.Reactive();
                    _managedStarbridgeScenario = managedStarbridgeScenario;
                    return managedStarbridgeScenario.Current;
                }
                catch (KeyNotFoundException)
                {
                    return null;
                }
                catch (MessagePack.FormatterNotRegisteredException)
                {
                    return null;
                }
            }

            AetheriaRuntimeStarbridgeSessionDocument? CurrentStarbridgeSession()
            {
                try
                {
                    managedStarbridgeSession ??= starbridgeSessionDocument.Reactive();
                    _managedStarbridgeSession = managedStarbridgeSession;
                    return managedStarbridgeSession.Current;
                }
                catch (KeyNotFoundException)
                {
                    return null;
                }
                catch (MessagePack.FormatterNotRegisteredException)
                {
                    return null;
                }
            }

            Task<AetheriaRuntimeSurfaceDocument> ZoneDetailsSurfaceAsync(
                AetheriaRuntimeDaemonFrameDocument frame,
                int zoneIndex)
            {
                return Task.FromResult(AetheriaRuntimeZoneDetailsSurfaceBuilder.BuildFromDocuments(
                    AetheriaRuntimeGameDocuments.ZoneDetails(frame, zoneIndex),
                    AetheriaRuntimeGameDocuments.SectorMap(frame),
                    RequireManagedCatalog(),
                    RequireManagedPlayerSettings(),
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }

            Task<AetheriaRuntimeSurfaceDocument> InventoryPanelSurfaceAsync(
                AetheriaRuntimeDaemonFrameDocument frame,
                AetheriaRuntimeInventoryPanelSurfaceRequest request)
            {
                request ??= new AetheriaRuntimeInventoryPanelSurfaceRequest();
                var entityIndex = request.DisplayedEntityIndex >= 0
                    ? request.DisplayedEntityIndex
                    : request.DisplayedCargoEntityIndex;
                var inventory = entityIndex < 0
                    ? new AetheriaRuntimeInventoryDocument()
                    : AetheriaRuntimeGameDocuments.Inventory(frame, entityIndex);

                return Task.FromResult(AetheriaRuntimeInventoryPanelSurfaceBuilder.BuildFromDocuments(
                    AetheriaRuntimeGameDocuments.CurrentEntity(frame),
                    AetheriaRuntimeGameDocuments.StationRefit(
                        frame,
                        RequireManagedLoadoutTemplates().Templates,
                        RequireManagedCatalog()),
                    inventory,
                    RequireManagedCatalog(),
                    RequireManagedPlayerSettings(),
                    request,
                    InventoryDropdownSurfaceDocumentId(ToDropdownRequest(request)),
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }

            Task<AetheriaRuntimeSurfaceDocument> InventoryDropdownSurfaceAsync(
                AetheriaRuntimeDaemonFrameDocument frame,
                AetheriaRuntimeInventoryDropdownSurfaceRequest request)
            {
                return Task.FromResult(AetheriaRuntimeInventoryDropdownSurfaceBuilder.BuildFromDocuments(
                    AetheriaRuntimeGameDocuments.StationRefit(
                        frame,
                        RequireManagedLoadoutTemplates().Templates,
                        RequireManagedCatalog()),
                    request,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }

            AetheriaRuntimeSurfaceDocument BuildMainMenuSurface(
                string surfaceId,
                bool canOpenRuntimeInputScreen,
                bool inGame)
            {
                var updatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                var stateBoot = AetheriaRuntimeStateBoot.Inspect(
                    new DirectoryInfo(Path.GetDirectoryName(StatePath) ?? "."),
                    StatePath);

                if (string.Equals(surfaceId, AetheriaRuntimeMainMenuCommands.RootSurfaceId, StringComparison.Ordinal))
                {
                    return AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(
                        stateBoot,
                        TryCurrentDaemonFrame(),
                        TryCurrentVerseHostSettings(),
                        TryCurrentPlayerSettings(),
                        canOpenRuntimeInputScreen,
                        inGame,
                        updatedAtUtc);
                }

                if (string.Equals(surfaceId, AetheriaRuntimeMainMenuCommands.SettingsSurfaceId, StringComparison.Ordinal))
                    return AetheriaRuntimeMainMenuSurfaceBuilder.BuildSettings(updatedAtUtc);

                if (string.Equals(surfaceId, AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId, StringComparison.Ordinal))
                {
                    return AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(
                        stateBoot,
                        TryCurrentPlayerSettings(),
                        canOpenRuntimeInputScreen,
                        inGame,
                        updatedAtUtc);
                }

                if (string.Equals(surfaceId, AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId, StringComparison.Ordinal))
                {
                    return AetheriaRuntimeMainMenuSurfaceBuilder.BuildPlayerSettings(
                        TryCurrentPlayerSettings(),
                        updatedAtUtc);
                }

                if (string.Equals(surfaceId, AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId, StringComparison.Ordinal))
                {
                    return AetheriaRuntimeMainMenuSurfaceBuilder.BuildVerseSettings(
                        AetheriaRuntimeClientTargetSurfaceBuilder.Build(
                            stateBoot,
                            TryCurrentVerseHostSettings(),
                            updatedAtUtc));
                }

                throw new ArgumentException($"Unknown Aetheria main menu surface id '{surfaceId}'.", nameof(surfaceId));
            }

            AetheriaRuntimePlayerSettingsDocument? TryCurrentPlayerSettings()
            {
                try
                {
                    managedPlayerSettings ??= playerSettingsDocument.Reactive();
                    _managedPlayerSettings = managedPlayerSettings;
                    return managedPlayerSettings.Current;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            AetheriaRuntimeVerseHostSettingsDocument? TryCurrentVerseHostSettings()
            {
                try
                {
                    return verseHostSettingsDocument.Latest();
                }
                catch (Exception)
                {
                    return null;
                }
            }

            AetheriaRuntimeDaemonFrameDocument? TryCurrentDaemonFrame()
            {
                try
                {
                    return latestFrameDocument.Latest();
                }
                catch (Exception)
                {
                    return null;
                }
            }

            static string IndexedDocumentId(string prefix, int index)
            {
                return $"{prefix}.{index.ToString(CultureInfo.InvariantCulture)}";
            }

            static string ViewportDocumentId(string prefix, AetheriaRuntimeViewportBounds viewport)
            {
                var normalized = AetheriaRuntimeGameDocuments.Normalize(viewport);
                return string.Join(
                    ".",
                    prefix,
                    ViewportToken(normalized.MinX),
                    ViewportToken(normalized.MinY),
                    ViewportToken(normalized.MaxX),
                    ViewportToken(normalized.MaxY));
            }

            static string InventoryPanelSurfaceDocumentId(AetheriaRuntimeInventoryPanelSurfaceRequest? request)
            {
                request ??= new AetheriaRuntimeInventoryPanelSurfaceRequest();
                return string.Join(
                    ".",
                    "aetheria.inventory.panel.surface",
                    AetheriaRuntimeVerseRecordKeys.StableToken(request.DisplayedEntityKey),
                    request.DisplayedEntityIndex.ToString(CultureInfo.InvariantCulture),
                    AetheriaRuntimeVerseRecordKeys.StableToken(request.DisplayedCargoEntityKey),
                    request.DisplayedCargoEntityIndex.ToString(CultureInfo.InvariantCulture),
                    request.DisplayedCargoIndex.ToString(CultureInfo.InvariantCulture),
                    request.ThermalView ? "thermal" : "inventory");
            }

            static string InventoryDropdownSurfaceDocumentId(AetheriaRuntimeInventoryDropdownSurfaceRequest? request)
            {
                request ??= new AetheriaRuntimeInventoryDropdownSurfaceRequest();
                return string.Join(
                    ".",
                    "aetheria.inventory.dropdown.surface",
                    AetheriaRuntimeVerseRecordKeys.StableToken(request.DisplayedEntityKey),
                    AetheriaRuntimeVerseRecordKeys.StableToken(request.DisplayedCargoEntityKey),
                    request.DisplayedCargoIndex.ToString(CultureInfo.InvariantCulture),
                    request.CanSaveLoadout ? "save" : "readonly");
            }

            static AetheriaRuntimeInventoryDropdownSurfaceRequest ToDropdownRequest(
                AetheriaRuntimeInventoryPanelSurfaceRequest? request)
            {
                request ??= new AetheriaRuntimeInventoryPanelSurfaceRequest();
                return new AetheriaRuntimeInventoryDropdownSurfaceRequest
                {
                    CurrentView = request.ViewTitle ?? "",
                    DisplayedEntityKey = request.DisplayedEntityKey ?? "",
                    DisplayedCargoEntityKey = request.DisplayedCargoEntityKey ?? "",
                    DisplayedCargoIndex = request.DisplayedCargoIndex,
                    CanSaveLoadout = !string.IsNullOrWhiteSpace(request.DisplayedEntityKey)
                };
            }

            static string ViewportToken(double value)
            {
                return value
                    .ToString("0.###", CultureInfo.InvariantCulture)
                    .Replace('-', 'n')
                    .Replace('.', 'p');
            }
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
                description: "managed Aetheria document bootstrap seed");
        }

        private AetheriaRuntimeCatalogSnapshot BootstrapRuntimeCatalogSnapshot()
        {
            return AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath);
        }

        public IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument> CommittedCommandFacts()
        {
            ThrowIfDisposed();
            return Cache.GetAll<AetheriaRuntimeCommittedCommandFactDocument>().ToArray();
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
