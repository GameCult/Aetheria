using GameCult.Aetheria.State.Verse;
using UnityEngine;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    public static class AetheriaEveRuntimeUnityHookInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            AetheriaEveRuntimeUnityHooks.ResolveStateBoot =
                overridePath => AetheriaRuntimeStateBoot.Inspect(
                    AetheriaEveRuntimeUnityClientCache.GameDataDirectory,
                    overridePath ?? "");
            AetheriaEveRuntimeUnityHooks.RuntimeState =
                (stateBoot, runtimeId) => AetheriaEveRuntimeUnityClientCache.RuntimeState(stateBoot, runtimeId);
            AetheriaEveRuntimeUnityHooks.Control =
                (stateBoot, runtimeId) => AetheriaEveRuntimeUnityClientCache.Control(stateBoot, runtimeId);
            AetheriaEveRuntimeUnityHooks.Ui =
                (stateBoot, runtimeId) => AetheriaEveRuntimeUnityClientCache.Ui(stateBoot, runtimeId);
            AetheriaEveRuntimeUnityHooks.StateRefResolver =
                (stateBoot, runtimeId) =>
                    AetheriaEveRuntimeUnityClientCache.EveSurfaceCultMeshStateRefResolver(stateBoot, runtimeId);
        }
    }
}
