using System;
using System.IO;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
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

            var surface = AetheriaRuntimeStateReader.ReadEveSurface(statePath, surfaceId);
            if (surface == null)
            {
                root.Add(BuildError($"Eve surface not found: {surfaceId}"));
                return;
            }

            MountSurface(statePath, surface);
        }

        private void OnEnable()
        {
            if (mountOnEnable)
                Mount();
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

            var surface = AetheriaRuntimeStateReader.ReadEveSurface(stateBoot.StateFilePath, surfaceId);
            if (surface == null || !ShouldMountSurface(stateBoot.StateFilePath, surface))
                return;

            MountSurface(stateBoot.StateFilePath, surface);
        }

        private UIDocument ResolveDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
            return _document;
        }

        private AetheriaRuntimeStateBootReport ResolveStateBoot()
        {
            var gameDataDirectory = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "GameData"));
            return AetheriaRuntimeStateBoot.Inspect(gameDataDirectory, stateFilePathOverride);
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

        private void MountSurface(string statePath, EveSurfaceDocument surface)
        {
            AetheriaEveUnitySurfaceHost.Render(
                transform,
                ResolveDocument(),
                "Aetheria Eve Surface",
                surface,
                request => EmitCommand(statePath, request),
                RootOnlyChrome,
                stateRef => AetheriaRuntimeStateReader.ResolveEveSurfaceStateRef(statePath, stateRef));
            _mountedStatePath = statePath;
            _mountedSurfaceId = surface.Surface.Id;
            _mountedSurfaceVersion = surface.Version;
            _mountedSurfaceUpdatedAtUtc = surface.UpdatedAtUtc ?? "";
        }

        private static void EmitCommand(string statePath, EveSurfaceCommandRequest request)
        {
            if (AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(statePath, request, out var daemonEnvelope))
            {
                Debug.Log(
                    $"Submitted Aetheria daemon operation from Eve surface: {daemonEnvelope!.Kind} {daemonEnvelope.CommandId}");
                return;
            }

            if (!AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand(
                    statePath,
                    request,
                    string.IsNullOrWhiteSpace(request.ClientId) ? "unity-uitoolkit" : request.ClientId,
                    out var envelope,
                    out var error))
            {
                Debug.LogWarning(
                    $"Ignored or failed Aetheria Eve command: {request.ProviderId}/{request.SurfaceId}/{request.Command}: {error}");
                return;
            }

            Debug.Log(
                $"Submitted Eve command for CultMesh bridge: {envelope!.ProviderId}/{envelope.SurfaceId}/{envelope.Command} {envelope.CommandId}");
        }
    }
}
