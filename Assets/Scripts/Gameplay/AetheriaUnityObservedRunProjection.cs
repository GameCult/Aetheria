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

    public static Galaxy Project(
        AetheriaRuntimeDaemonFrameDocument frame,
        SectorBackgroundSettings backgroundSettings,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log)
    {
        if (frame == null || frame.Run == null)
            throw new ArgumentNullException(nameof(frame));

        return global::Galaxy.ProjectObservedDaemonRun(frame.Run, backgroundSettings, runtimeCatalog, log);
    }

    public static GalaxyZone FindZone(Galaxy galaxy, int daemonZoneIndex)
    {
        return daemonZoneIndex < 0
            ? null
            : galaxy?.Zones?.FirstOrDefault(zone => zone != null && zone.ZoneIndex == daemonZoneIndex);
    }

    public static AetheriaRuntimeZoneSnapshotCommit FindZoneSnapshot(AetheriaRuntimeRunCheckpointCommit run)
    {
        if (run == null)
            return null;

        return (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .FirstOrDefault(zone => zone != null && zone.ZoneIndex == run.CurrentZoneIndex)
            ?? (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(zone => zone != null);
    }
}
