using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    public static class AetheriaEveRuntimeBootstrap
    {
        public const string DefaultSurfaceId = "aetheria.operations";
        public const string DisableEnvironmentVariable = "AETHERIA_DISABLE_EVE_RUNTIME_BOOTSTRAP";
        public const string SurfaceEnvironmentVariable = "AETHERIA_EVE_SURFACE_ID";
        public const string StatePathEnvironmentVariable = "AETHERIA_EVE_STATE_PATH";
        public const string DisableCommandLineSwitch = "--aetheria-disable-eve-runtime-bootstrap";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MountDefaultSurface()
        {
            if (IsDisabled() || UnityEngine.Object.FindObjectOfType<AetheriaEveSurfacePresenter>() != null)
                return;

            var host = new GameObject("Aetheria Eve Runtime Surface");
            UnityEngine.Object.DontDestroyOnLoad(host);

            host.AddComponent<UIDocument>();
            var presenter = host.AddComponent<AetheriaEveSurfacePresenter>();
            presenter.SurfaceId = SurfaceId();
            presenter.StateFilePathOverride = StatePathOverride();
        }

        private static bool IsDisabled()
        {
            if (Application.isBatchMode)
                return true;

            var environment = Environment.GetEnvironmentVariable(DisableEnvironmentVariable);
            if (string.Equals(environment, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(environment, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Environment.GetCommandLineArgs()
                .Any(argument => string.Equals(argument, DisableCommandLineSwitch, StringComparison.OrdinalIgnoreCase));
        }

        private static string SurfaceId()
        {
            var configured = Environment.GetEnvironmentVariable(SurfaceEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configured) ? DefaultSurfaceId : configured;
        }

        private static string StatePathOverride()
        {
            return Environment.GetEnvironmentVariable(StatePathEnvironmentVariable) ?? "";
        }
    }
}
