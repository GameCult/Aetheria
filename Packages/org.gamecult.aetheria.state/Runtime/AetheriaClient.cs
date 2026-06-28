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

        public Task<AetheriaRuntimeObservedDaemonState?> ObserveAsync()
        {
            ThrowIfDisposed();
            return _verse.GetObservedDaemonStateAsync();
        }

        public AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog()
        {
            ThrowIfDisposed();
            return _verse.OpenRuntimeCatalog();
        }

        public Task<AetheriaRuntimePlayerSettingsSnapshot?> PlayerSettingsAsync()
        {
            ThrowIfDisposed();
            return _verse.GetPlayerSettingsAsync();
        }

        public Task<AetheriaRuntimeVerseHostSettingsSnapshot?> VerseHostSettingsAsync()
        {
            ThrowIfDisposed();
            return _verse.GetVerseHostSettingsAsync();
        }

        public Task<System.Collections.Generic.IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>> LoadoutTemplatesAsync()
        {
            ThrowIfDisposed();
            return _verse.GetLoadoutTemplatesAsync();
        }

        public async Task<AetheriaRuntimeLoadoutTemplateCommit> LoadoutTemplateAsync(string entityKey)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeLoadoutSnapshotProjector.ProjectLoadoutTemplate(
                frame.Run ?? new AetheriaRuntimeRunCheckpointCommit(),
                entityKey ?? "");
        }

        public Task<AetheriaRuntimeDaemonFrameDocument?> LatestAuthoritativeRunFrameAsync()
        {
            ThrowIfDisposed();
            return _verse.GetLatestAuthoritativeRunFrameAsync();
        }

        public async Task<AetheriaRuntimeRtsViewportDocument> MapViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectViewport(frame, viewport);
        }

        public async Task<AetheriaRuntimeObjectsViewportDocument> ObjectsViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectObjectsViewport(frame, viewport);
        }

        public async Task<AetheriaRuntimeGravityViewportDocument> GravityViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectGravityViewport(frame, viewport);
        }

        public async Task<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplatsViewportAsync(
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectRenderSplatsViewport(frame, viewport);
        }

        public async Task<AetheriaRuntimeAssetManifestDocument> AssetManifestAsync()
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeAssets.ProjectManifest(
                _verse.OpenRuntimeCatalog(),
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
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectZoneDetails(frame, zoneIndex);
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
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            scenario ??= await _verse.GetStarbridgeScenarioAsync().ConfigureAwait(false);
            session ??= await _verse.GetStarbridgeSessionAsync().ConfigureAwait(false);
            return AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(
                frame,
                scenario,
                session,
                _verse.OpenRuntimeCatalog());
        }

        public async Task<AetheriaRuntimeSelectedObjectDocument> SelectedObjectAsync(int entityIndex)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectSelectedObject(frame, entityIndex);
        }

        public async Task<AetheriaRuntimeInventoryDocument> InventoryAsync(int entityIndex)
        {
            ThrowIfDisposed();
            var frame = await RequireFrameAsync().ConfigureAwait(false);
            return AetheriaRuntimeRtsProjection.ProjectInventory(frame, entityIndex);
        }

        public Task<AetheriaRuntimeDaemonHealthDocument?> DaemonHealthAsync()
        {
            ThrowIfDisposed();
            return _verse.GetHealthAsync();
        }

        public Task<AetheriaRuntimeVerseAuthorityPolicyDocument?> AuthorityStatusAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(
                AetheriaRuntimeDaemonPublicationStore.TryReadVerseAuthorityPolicy(
                    StatePath,
                    out var policy)
                    ? policy
                    : null);
        }

        public Task<AetheriaRuntimeDaemonSoaViewDocument?> SoaViewAsync()
        {
            ThrowIfDisposed();
            return _verse.GetLatestSoaViewAsync();
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonGameSurfaceAsync()
        {
            ThrowIfDisposed();
            return _verse.GetDaemonGameSurfaceAsync();
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonGameTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return _verse.GetDaemonGameTuiSurfaceAsync();
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonEditorSurfaceAsync()
        {
            ThrowIfDisposed();
            return _verse.GetDaemonEditorSurfaceAsync();
        }

        public Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonEditorTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return _verse.GetDaemonEditorTuiSurfaceAsync();
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
            var frame = await _verse.GetLatestFrameAsync().ConfigureAwait(false);
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
