using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;

public static class AetheriaUnityObservedRunProjection
{
    public static Galaxy Project(
        AetheriaRuntimeSectorMapDocument sectorMap,
        SectorBackgroundSettings backgroundSettings,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log)
    {
        return global::Galaxy.ProjectObservedSectorMap(sectorMap, backgroundSettings, runtimeCatalog, log);
    }

    public static GalaxyZone FindZone(Galaxy galaxy, int daemonZoneIndex)
    {
        return daemonZoneIndex < 0
            ? null
            : galaxy?.Zones?.FirstOrDefault(zone => zone != null && zone.ZoneIndex == daemonZoneIndex);
    }
}
