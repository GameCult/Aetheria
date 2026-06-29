using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class AetheriaEveSurfacePresenter : MonoBehaviour
    {
        [SerializeField]
        private string surfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId;

        [SerializeField]
        private string stateFilePathOverride = "";

        [SerializeField]
        private bool mountOnEnable = true;

        [SerializeField]
        private bool refreshInUpdate = true;

        [SerializeField]
        private float refreshIntervalSeconds = 0.1f;

        private UIDocument? _document;
        private float _nextRefreshTime;
        private string _mountedStatePath = "";
        private string _mountedSurfaceId = "";
        private long _mountedSurfaceVersion = -1;
        private string _mountedSurfaceUpdatedAtUtc = "";
        private CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>? _reactiveSurfaceState;
        private string _reactiveSurfaceStatePath = "";
        private string _reactiveSurfaceId = "";
        private static readonly AetheriaEveUnitySurfaceChrome RootOnlyChrome = new AetheriaEveUnitySurfaceChrome
        {
            UseShell = false,
            RootPaddingTop = 0f,
            RootAlignItems = Align.Stretch,
            RootJustifyContent = Justify.FlexStart,
            RootPickingMode = PickingMode.Position
        };

        public string SurfaceId
        {
            get => surfaceId;
            set => surfaceId = value ?? "";
        }

        public string StateFilePathOverride
        {
            get => stateFilePathOverride;
            set => stateFilePathOverride = value ?? "";
        }

        public bool RefreshInUpdate
        {
            get => refreshInUpdate;
            set => refreshInUpdate = value;
        }

        public void Mount()
        {
            var document = ResolveDocument();
            var root = document.rootVisualElement;
            root.Clear();

            var stateBoot = ResolveStateBoot();
            if (!stateBoot.SupportsLocalStateFileRead)
            {
                root.Add(BuildError(stateBoot.FailureMessage));
                return;
            }

            var statePath = stateBoot.StateFilePath;
            if (!stateBoot.StateFileExists)
            {
                root.Add(BuildError($"Aetheria state file not found: {statePath}"));
                return;
            }

            var surface = ReadDaemonSurface(stateBoot);
            if (surface == null)
            {
                root.Add(BuildError($"Eve surface not found: {surfaceId}"));
                return;
            }

            MountSurface(stateBoot, surface);
        }

        private void OnEnable()
        {
            if (mountOnEnable)
                Mount();
        }

        private void OnDisable()
        {
            DisposeReactiveSurfaceState();
        }

        private void Update()
        {
            if (!refreshInUpdate || Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, refreshIntervalSeconds);
            RefreshIfChanged();
        }

        private void RefreshIfChanged()
        {
            var stateBoot = ResolveStateBoot();
            if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
                return;

            var surface = ReadDaemonSurface(stateBoot);
            if (surface == null || !ShouldMountSurface(stateBoot.StateFilePath, surface))
                return;

            MountSurface(stateBoot, surface);
        }

        private EveSurfaceDocument? ReadDaemonSurface(AetheriaRuntimeStateBootReport stateBoot)
        {
            var surface = ReadDaemonSurfaceState(stateBoot);
            return surface == null
                ? null
                : AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
                    surface,
                    AetheriaUnityRuntimeClientProvider.EveSurfaceStateRefResolver(
                        stateBoot,
                        "unity-eve-surface-presenter"));
        }

        private global::Aetheria.State.Documents.EveSurfaceState? ReadDaemonSurfaceState(
            AetheriaRuntimeStateBootReport stateBoot)
        {
            var reactive = ResolveReactiveDaemonSurfaceState(stateBoot);
            return reactive?.Current;
        }

        private CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>? ResolveReactiveDaemonSurfaceState(
            AetheriaRuntimeStateBootReport stateBoot)
        {
            if (_reactiveSurfaceState != null &&
                string.Equals(_reactiveSurfaceStatePath, stateBoot.StateFilePath, StringComparison.Ordinal) &&
                string.Equals(_reactiveSurfaceId, surfaceId, StringComparison.Ordinal))
            {
                return _reactiveSurfaceState;
            }

            DisposeReactiveSurfaceState();
            _reactiveSurfaceState = CreateReactiveDaemonSurfaceState(stateBoot);
            if (_reactiveSurfaceState != null)
            {
                _reactiveSurfaceStatePath = stateBoot.StateFilePath;
                _reactiveSurfaceId = surfaceId ?? "";
            }

            return _reactiveSurfaceState;
        }

        private CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>? CreateReactiveDaemonSurfaceState(
            AetheriaRuntimeStateBootReport stateBoot)
        {
            return AetheriaUnityRuntimeClientProvider
                .RuntimeState(stateBoot, "unity-eve-surface-presenter")
                .ReactiveEveSurface(surfaceId);
        }

        private UIDocument ResolveDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
            return _document;
        }

        private AetheriaRuntimeStateBootReport ResolveStateBoot()
        {
            return AetheriaRuntimeStateBoot.Inspect(
                AetheriaUnityRuntimePaths.GameDataDirectory,
                stateFilePathOverride);
        }

        private static VisualElement BuildError(string message)
        {
            var container = new VisualElement();
            container.AddToClassList("aetheria-eve-runtime-error");
            var label = new Label(message);
            label.AddToClassList("aetheria-eve-runtime-error-label");
            container.Add(label);
            return container;
        }

        private bool ShouldMountSurface(string statePath, EveSurfaceDocument surface)
        {
            return !string.Equals(_mountedStatePath, statePath, StringComparison.Ordinal) ||
                   !string.Equals(_mountedSurfaceId, surface.Surface.Id, StringComparison.Ordinal) ||
                   _mountedSurfaceVersion != surface.Version ||
                   !string.Equals(_mountedSurfaceUpdatedAtUtc, surface.UpdatedAtUtc, StringComparison.Ordinal);
        }

        private void MountSurface(AetheriaRuntimeStateBootReport stateBoot, EveSurfaceDocument surface)
        {
            var statePath = stateBoot.StateFilePath;
            AetheriaEveUnitySurfaceHost.Render(
                transform,
                ResolveDocument(),
                "Aetheria Eve Surface",
                surface,
                request => EmitCommand(stateBoot, request),
                RootOnlyChrome,
                AetheriaUnityRuntimeClientProvider.EveSurfaceStateRefResolver(
                    stateBoot,
                    "unity-eve-surface-presenter"));
            _mountedStatePath = statePath;
            _mountedSurfaceId = surface.Surface.Id;
            _mountedSurfaceVersion = surface.Version;
            _mountedSurfaceUpdatedAtUtc = surface.UpdatedAtUtc ?? "";
        }

        private void EmitCommand(AetheriaRuntimeStateBootReport stateBoot, EveSurfaceCommandRequest request)
        {
            var clientId = string.IsNullOrWhiteSpace(request.ClientId)
                ? "unity-eve-surface-presenter"
                : request.ClientId;
            var control = AetheriaUnityRuntimeClientProvider.Control(
                stateBoot,
                clientId);
            if (control.TrySubmitSurfaceCommand(request, out var daemonEnvelope))
            {
                Debug.Log(
                    $"Submitted Aetheria daemon operation from Eve surface: {daemonEnvelope!.Kind} {daemonEnvelope.CommandId}");
                return;
            }

            try
            {
                var envelope = AetheriaUnityRuntimeClientProvider
                    .Ui(stateBoot, "unity-eve-surface-presenter")
                    .SurfaceCommandAsync(
                        request,
                        string.IsNullOrWhiteSpace(request.ClientId) ? "unity-uitoolkit" : request.ClientId)
                    .GetAwaiter()
                    .GetResult();

                Debug.Log(
                    $"Submitted Eve operation for CultMesh bridge: {envelope.OperationId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"Ignored or failed Aetheria Eve command: {request.ProviderId}/{request.SurfaceId}/{request.Operation?.OperationId}: {ex}");
            }
        }

        private void DisposeReactiveSurfaceState()
        {
            _reactiveSurfaceState?.Dispose();
            _reactiveSurfaceState = null;
            _reactiveSurfaceStatePath = "";
            _reactiveSurfaceId = "";
        }
    }
}
