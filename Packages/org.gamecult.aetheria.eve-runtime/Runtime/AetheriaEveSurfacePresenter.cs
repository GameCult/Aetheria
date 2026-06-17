using System;
using System.IO;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
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
        private string surfaceId = "aetheria.catalog.operator";

        [SerializeField]
        private string stateFilePathOverride = "";

        [SerializeField]
        private bool mountOnEnable = true;

        private UIDocument? _document;

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

            var lowerer = new EveUiToolkitSurfaceLowerer();
            root.Add(lowerer.Lower(surface, request => EmitCommand(statePath, request)));
        }

        private void OnEnable()
        {
            if (mountOnEnable)
                Mount();
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

        private static void EmitCommand(string statePath, EveSurfaceCommandRequest request)
        {
            var envelope = AetheriaRuntimeEveCommandLog.QueueCommand(statePath, request);
            Debug.Log(
                $"Queued Eve command for CultMesh bridge: {envelope.ProviderId}/{envelope.SurfaceId}/{envelope.Command} {envelope.CommandId}");
        }
    }
}
