using System;
using System.IO;
using System.Threading.Tasks;
using GameCult.Eve.Surface;

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

            var observed = State.CurrentObservedDaemon();
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AetheriaClient));
        }
    }
}
