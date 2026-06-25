using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;

public static class AetheriaUnityObservedRunProjection
{
    public static Galaxy Galaxy { get; private set; }

    public static void Project(
        AetheriaRuntimeSectorMapDocument sectorMap,
        SectorBackgroundSettings backgroundSettings,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log)
    {
        Galaxy = global::Galaxy.ProjectObservedSectorMap(sectorMap, backgroundSettings, runtimeCatalog, log);
    }

    public static void Project(
        AetheriaRuntimeDaemonFrameDocument frame,
        SectorBackgroundSettings backgroundSettings,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log)
    {
        if (frame == null || frame.Run == null)
            throw new ArgumentNullException(nameof(frame));

        Galaxy = global::Galaxy.ProjectObservedDaemonRun(frame.Run, backgroundSettings, runtimeCatalog, log);
    }

    public static GalaxyZone FindZone(int daemonZoneIndex)
    {
        return daemonZoneIndex < 0
            ? null
            : Galaxy?.Zones?.FirstOrDefault(zone => zone != null && zone.ZoneIndex == daemonZoneIndex);
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
