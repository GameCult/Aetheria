using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    // Unity runtime shells acquire typed state through this port so the transport can move to the daemon without spelunking every consumer.
    public static class AetheriaRuntimeStateReader
    {
        public static AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.OpenReadOnly(stateFilePath);
        }

        public static AetheriaRuntimePlayerSettingsSnapshot? ReadPlayerSettings(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadPlayerSettings(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot> ReadLoadoutTemplates(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeRunStateSnapshot> ReadRunStates(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadRunStates(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeZoneStateSnapshot> ReadZoneStates(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadZoneStates(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeEntitySnapshot> ReadEntitySnapshots(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadEntitySnapshots(stateFilePath);
        }

        public static EveSurfaceDocument? ReadEveSurface(string stateFilePath, string surfaceId)
        {
            return AetheriaRuntimeCatalogStore.ReadEveSurfaces(stateFilePath)
                .FirstOrDefault(candidate => string.Equals(candidate.Surface.Id, surfaceId, StringComparison.Ordinal));
        }
    }
}
