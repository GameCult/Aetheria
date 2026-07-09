using System;
using System.Collections.Generic;
using System.IO;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using UnityEngine;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    public static class AetheriaEveRuntimeUnityClientCache
    {
        private static readonly Dictionary<string, AetheriaClient> RuntimeClients =
            new Dictionary<string, AetheriaClient>(StringComparer.Ordinal);
        private static DirectoryInfo? s_gameDataDirectory;
        private static string? s_runtimeStateFilePath;

        public static DirectoryInfo GameDataDirectory =>
            s_gameDataDirectory ??= new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData");

        public static string RuntimeStateFilePath =>
            s_runtimeStateFilePath ??= AetheriaRuntimeStateBoot.Inspect(GameDataDirectory).StateFilePath;

        public static AetheriaClientState RuntimeState(
            AetheriaRuntimeStateBootReport stateBoot,
            string runtimeId = "")
        {
            return RuntimeClient(stateBoot, runtimeId).State;
        }

        public static AetheriaControl Control(
            AetheriaRuntimeStateBootReport stateBoot,
            string runtimeId = "")
        {
            return RuntimeClient(stateBoot, runtimeId).Control;
        }

        public static AetheriaUi Ui(
            AetheriaRuntimeStateBootReport stateBoot,
            string runtimeId = "")
        {
            return RuntimeClient(stateBoot, runtimeId).Ui;
        }

        public static CultMeshStateRefResolver EveSurfaceCultMeshStateRefResolver(
            AetheriaRuntimeStateBootReport stateBoot,
            string runtimeId = "")
        {
            return RuntimeState(stateBoot, runtimeId).CreateEveSurfaceCultMeshStateRefResolver();
        }

        public static void ClearRuntimeStateFilePathCache()
        {
            s_runtimeStateFilePath = null;
        }

        public static void Dispose()
        {
            foreach (var client in RuntimeClients.Values)
                client.Dispose();

            RuntimeClients.Clear();
            s_runtimeStateFilePath = null;
        }

        private static AetheriaClient RuntimeClient(
            AetheriaRuntimeStateBootReport stateBoot,
            string runtimeId = "")
        {
            return ResolveClient(
                stateBoot.StateFilePath,
                string.IsNullOrWhiteSpace(runtimeId) ? stateBoot.RuntimeId : runtimeId);
        }

        private static AetheriaClient ResolveClient(string stateFilePath, string runtimeId)
        {
            var effectiveStateFilePath = stateFilePath ?? "";
            var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId)
                ? AetheriaRuntimeStateBoundary.DefaultClientRuntimeId
                : runtimeId;
            var cacheKey = effectiveStateFilePath + "\n" + effectiveRuntimeId;
            if (RuntimeClients.TryGetValue(cacheKey, out var runtimeClient))
                return runtimeClient;

            runtimeClient = AetheriaClient
                .OpenAsync(
                    effectiveStateFilePath,
                    effectiveRuntimeId,
                    "local",
                    startServer: false,
                    pullOnOpen: true)
                .GetAwaiter()
                .GetResult();
            RuntimeClients[cacheKey] = runtimeClient;
            return runtimeClient;
        }
    }
}
