using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public static class AetheriaUnityEveRuntimeHooks
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        AetheriaEveRuntimeUnityHooks.ResolveStateBoot =
            overridePath => AetheriaRuntimeStateBoot.Inspect(
                AetheriaUnityRuntimePaths.GameDataDirectory,
                overridePath ?? "");
        AetheriaEveRuntimeUnityHooks.RuntimeState =
            (stateBoot, runtimeId) => AetheriaUnityRuntimeClientProvider.RuntimeState(stateBoot, runtimeId);
        AetheriaEveRuntimeUnityHooks.Control =
            (stateBoot, runtimeId) => AetheriaUnityRuntimeClientProvider.Control(stateBoot, runtimeId);
        AetheriaEveRuntimeUnityHooks.Ui =
            (stateBoot, runtimeId) => AetheriaUnityRuntimeClientProvider.Ui(stateBoot, runtimeId);
        AetheriaEveRuntimeUnityHooks.StateRefResolver =
            (stateBoot, runtimeId) => AetheriaUnityRuntimeClientProvider.EveSurfaceStateRefResolver(stateBoot, runtimeId);
    }
}
