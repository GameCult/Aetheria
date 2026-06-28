using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaClient : IDisposable
    {
        private readonly AetheriaRuntimeVerseClient _verse;
        private readonly string _clientId;
        private readonly string _sessionId;
        private readonly AetheriaRuntimeDaemonOperationsClient _operations;
        private readonly AetheriaControl _control;
        private readonly AetheriaUi _ui;
        private readonly AetheriaClientState _state;
        private bool _disposed;

        private AetheriaClient(AetheriaRuntimeVerseClient verse, string clientId, string sessionId)
        {
            _verse = verse ?? throw new ArgumentNullException(nameof(verse));
            _clientId = string.IsNullOrWhiteSpace(clientId) ? AetheriaRuntimeVerseClient.DefaultRuntimeId : clientId;
            _sessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId;
            _operations = new AetheriaRuntimeDaemonOperationsClient(SendOperation);
            _control = new AetheriaControl(_operations);
            _ui = new AetheriaUi(this);
            _state = _verse.Aetheria();
        }

        public string StatePath => _verse.StatePath;
        public string RuntimeId => _verse.RuntimeId;
        public AetheriaControl Control => _control;
        public AetheriaUi Ui => _ui;
        public AetheriaClientState State => _state;

        public AetheriaClientState Aetheria() => State;

        public CultMeshDocumentHandle<TDocument> Document<TDocument>()
            where TDocument : class
        {
            ThrowIfDisposed();
            return State.Document<TDocument>();
        }

        public ICultMeshDocumentHandle DocumentBySchema(string schemaVersion)
        {
            ThrowIfDisposed();
            return State.DocumentBySchema(schemaVersion);
        }

        public Task<TDocument> LatestAsync<TDocument>()
            where TDocument : class
        {
            ThrowIfDisposed();
            return State.LatestAsync<TDocument>();
        }

        public Observable<TDocument> Watch<TDocument>()
            where TDocument : class
        {
            ThrowIfDisposed();
            return State.Watch<TDocument>();
        }

        public IDisposable Watch<TDocument>(Action<TDocument> onNext)
            where TDocument : class
        {
            ThrowIfDisposed();
            return State.Watch(onNext);
        }

        public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            ThrowIfDisposed();
            return State.ReactiveAsync<TDocument>(options);
        }

        public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(
            CultMeshReactiveDocumentOptions? options = null)
            where TDocument : class
        {
            ThrowIfDisposed();
            return State.Reactive<TDocument>(options);
        }

        internal AetheriaRuntimeDaemonOperationsClient Operations => _operations;

        public static async Task<AetheriaClient> OpenAsync(
            string statePath,
            string runtimeId = AetheriaRuntimeVerseClient.DefaultRuntimeId,
            string sessionId = "local",
            bool startServer = false,
            bool pullOnOpen = true)
        {
            var verse = await AetheriaRuntimeVerseClient
                .OpenAsync(statePath, runtimeId, startServer, pullOnOpen)
                .ConfigureAwait(false);
            return new AetheriaClient(verse, runtimeId, sessionId);
        }

        public static Task<AetheriaClient> OpenLocalAsync(
            DirectoryInfo gameDataDirectory,
            string runtimeId,
            string sessionId = "local",
            bool pullOnOpen = true)
        {
            if (gameDataDirectory == null) throw new ArgumentNullException(nameof(gameDataDirectory));

            var stateBoot = AetheriaRuntimeStateBoot.Inspect(gameDataDirectory);
            if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
            {
                throw new InvalidOperationException(
                    $"Aetheria local client requires a readable local Verse state file: {stateBoot.FailureMessage}");
            }

            return OpenAsync(
                stateBoot.StateFilePath,
                runtimeId,
                sessionId,
                startServer: false,
                pullOnOpen: pullOnOpen);
        }

        public async Task<AetheriaRuntimeObservedDaemonState?> ObserveAsync()
        {
            ThrowIfDisposed();
            var frame = await State.Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
            if (frame == null)
                return null;

            var soaView = await State.Daemon.LatestSoaView.LatestAsync().ConfigureAwait(false);
            if (soaView == null ||
                !string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
            {
                soaView = null;
            }

            return new AetheriaRuntimeObservedDaemonState(
                frame,
                soaView,
                AetheriaRuntimeDaemonFrameStore.GetFramePath(StatePath),
                AetheriaRuntimeDaemonSoaViewStore.GetViewPath(StatePath));
        }

        public async Task<AetheriaRuntimeLoadoutTemplateCommit> LoadoutTemplateAsync(string entityKey)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeLoadoutSnapshotProjector.ProjectLoadoutTemplate(
                frame.Run ?? new AetheriaRuntimeRunCheckpointCommit(),
                entityKey ?? "");
        }

        public async Task<AetheriaRuntimeDaemonFrameDocument?> LatestAuthoritativeRunFrameAsync()
        {
            ThrowIfDisposed();
            var frame = await State.Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
            if (frame == null ||
                !frame.IsAuthoritative ||
                frame.Run == null ||
                frame.Run.Zones == null ||
                frame.Run.Zones.Count == 0)
            {
                return null;
            }

            return frame;
        }

        public async Task<AetheriaRuntimeRtsViewportDocument> MapViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            return await State.Viewports.Map(viewport).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeObjectsViewportDocument> ObjectsViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            return await State.Viewports.Objects(viewport).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeGravityViewportDocument> GravityViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            return await State.Viewports.Gravity(viewport).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplatsViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            return await State.Viewports.RenderSplats(viewport).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeAssetManifestDocument> AssetManifestAsync()
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeAssets.ProjectManifest(
                State.Catalog.Latest(),
                frame.Run?.RunId ?? "");
        }

        public async Task<AetheriaRuntimeCurrentZoneDocument> CurrentZoneAsync()
        {
            ThrowIfDisposed();
            return await State.Current.Zone.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeCurrentEntityDocument> CurrentEntityAsync()
        {
            ThrowIfDisposed();
            return await State.Current.Entity.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeCurrentDockingDocument> CurrentDockingAsync()
        {
            ThrowIfDisposed();
            return await State.Current.Docking.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeZoneContactsDocument> ZoneContactsAsync()
        {
            ThrowIfDisposed();
            return await State.ZoneContacts.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeStationRefitDocument> StationRefitAsync()
        {
            ThrowIfDisposed();
            return await State.StationRefit.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeSectorMapDocument> SectorMapAsync()
        {
            ThrowIfDisposed();
            return await State.SectorMap.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeZoneDetailsDocument> ZoneDetailsAsync(int zoneIndex)
        {
            ThrowIfDisposed();
            return await State.Details.Zone(zoneIndex).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeZoneRenderDocument> ZoneRenderAsync()
        {
            ThrowIfDisposed();
            return await State.ZoneRender.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSessionSummaryAsync(
            AetheriaRuntimeStarbridgeScenarioDocument? scenario = null,
            AetheriaRuntimeStarbridgeSessionDocument? session = null)
        {
            ThrowIfDisposed();
            if (scenario == null && session == null)
                return await State.Starbridge.Summary.LatestAsync().ConfigureAwait(false);

            var frame = await RequireFrameAsync().ConfigureAwait(false);
            scenario ??= await State.Starbridge.Scenario.LatestAsync().ConfigureAwait(false);
            session ??= await State.Starbridge.Session.LatestAsync().ConfigureAwait(false);
            return AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(
                frame,
                scenario,
                session,
                State.Catalog.Latest());
        }

        public async Task<AetheriaRuntimeSelectedObjectDocument> SelectedObjectAsync(int entityIndex)
        {
            ThrowIfDisposed();
            return await State.Details.SelectedObject(entityIndex).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeInventoryDocument> InventoryAsync(int entityIndex)
        {
            ThrowIfDisposed();
            return await State.Details.Inventory(entityIndex).LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeDaemonHealthDocument?> DaemonHealthAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.Health.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeVerseAuthorityPolicyDocument?> AuthorityStatusAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.AuthorityPolicy.LatestAsync().ConfigureAwait(false);
        }

        public async Task<AetheriaRuntimeDaemonSoaViewDocument?> SoaViewAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.LatestSoaView.LatestAsync().ConfigureAwait(false);
        }

        public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonGameSurfaceAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.GameSurface.LatestAsync().ConfigureAwait(false);
        }

        public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonGameTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.GameTuiSurface.LatestAsync().ConfigureAwait(false);
        }

        public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonEditorSurfaceAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.EditorSurface.LatestAsync().ConfigureAwait(false);
        }

        public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonEditorTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return await State.Daemon.EditorTuiSurface.LatestAsync().ConfigureAwait(false);
        }

        public Func<string, string> CreateEveSurfaceStateRefResolver()
        {
            ThrowIfDisposed();
            return _verse.CreateEveSurfaceStateRefResolver();
        }

        public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()
        {
            ThrowIfDisposed();
            return _verse.CreateEveSurfaceCultMeshStateRefResolver();
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitInputSettingsCommandAsync(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string? clientId = null,
            bool flush = true)
        {
            ThrowIfDisposed();
            return _verse.SubmitInputSettingsCommandAsync(
                command,
                body,
                string.IsNullOrWhiteSpace(clientId) ? _clientId : clientId!,
                flush);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitLoadoutTemplateCommandAsync(
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string? clientId = null,
            bool flush = true)
        {
            ThrowIfDisposed();
            return _verse.SubmitLoadoutTemplateCommandAsync(
                loadoutTemplate,
                string.IsNullOrWhiteSpace(clientId) ? _clientId : clientId!,
                flush);
        }

        internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitKnownSurfaceCommandAsync(
            EveSurfaceCommandRequest request,
            string? clientId = null,
            bool flush = true)
        {
            ThrowIfDisposed();
            return _verse.SubmitKnownSurfaceCommandAsync(
                request,
                string.IsNullOrWhiteSpace(clientId) ? _clientId : clientId!,
                flush);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
            double directionX,
            double directionY,
            double scalarValue = 1.0)
        {
            return Control.SetMoveVector(directionX, directionY, scalarValue);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetTarget(string targetEntityKey)
        {
            return Control.SetTarget(targetEntityKey);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SubmitDaemonCommandDocument(
            AetheriaRuntimeDaemonCommandDocument command)
        {
            ThrowIfDisposed();
            return _verse
                .SubmitDaemonCommandAsync(command)
                .GetAwaiter()
                .GetResult();
        }

        internal AetheriaRuntimeEveCommandEnvelope SubmitEveCommandDocument(
            AetheriaRuntimeEveCommandDocument command)
        {
            ThrowIfDisposed();
            return _verse
                .SubmitEveCommandAsync(command)
                .GetAwaiter()
                .GetResult();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _verse.Dispose();
        }

        private AetheriaRuntimeDaemonCommandEnvelope SendOperation(
            Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState?, AetheriaRuntimeDaemonCommandEnvelope> submit)
        {
            ThrowIfDisposed();
            if (submit == null) throw new ArgumentNullException(nameof(submit));

            var observed = ObserveAsync().GetAwaiter().GetResult();
            var operationClient = new AetheriaRuntimeDaemonOperationClient(
                StatePath,
                _clientId,
                observed?.Frame.SessionId ?? _sessionId,
                command => _verse
                    .SubmitDaemonCommandAsync(command)
                    .GetAwaiter()
                    .GetResult());

            return submit(operationClient, observed);
        }

        private async Task<AetheriaRuntimeDaemonFrameDocument> RequireFrameAsync()
        {
            var frame = await State.Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
            if (frame == null)
                throw new InvalidOperationException("Aetheria local client has no daemon frame yet.");
            return frame;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AetheriaClient));
        }
    }
}
