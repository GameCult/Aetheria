using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVisibilitySimulation
    {
        private const string ReflectorVisibilityGrid = "reflector-visibility";

        public static void StepZone(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit>? entities,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonRenderSettings? renderSettings = null)
        {
            if (zone == null || catalog == null)
                return;

            var settings = renderSettings ?? AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;
            var orbitPositions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
            var suns = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .Where(body => body != null && string.Equals(body.Kind, "sun", StringComparison.OrdinalIgnoreCase))
                .SelectMany(body =>
                {
                    if (!AetheriaRuntimeOrbitQueries.TryResolveBodyPosition(body, orbitPositions, out var position))
                        return Array.Empty<(AetheriaRuntimeOrbitPosition position, double radius)>();
                    var radius = settings.ResolveLightRadius(body.Mass) *
                        Math.Max(0.01, body.SunVisual?.LightRadiusMultiplier ?? 1);
                    return new[] { (position, radius) };
                })
                .Where(sun => sun.radius > 0)
                .ToArray();

            foreach (var entity in entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null || !entity.IsActive)
                    continue;

                var light = suns.Sum(sun => StellarLight(
                    entity.PositionX - sun.position.x,
                    entity.PositionZ - sun.position.z,
                    sun.radius));
                var reflected = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(
                        entity, catalog, AetheriaRuntimeBehaviorKinds.Reflector)
                    .Sum(behavior => Math.Max(0, behavior.EvaluateStat(
                        1, ThermalPerformance(entity, behavior.EquipmentIndex))) * light);
                SetVisibilitySource(entity, reflected);
            }
        }

        private static double StellarLight(double deltaX, double deltaZ, double radius)
        {
            var distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            if (distanceSquared >= radius * radius)
                return 0;

            var x = Math.Min(1, Math.Sqrt(distanceSquared) / radius * 2);
            return Math.Pow((x + 1) * (1 - x), 8);
        }

        private static void SetVisibilitySource(AetheriaRuntimeEntitySnapshotCommit entity, double value)
        {
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var grid = grids.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, ReflectorVisibilityGrid, StringComparison.Ordinal));
            var previous = grid?.Values.FirstOrDefault() ?? 0;
            entity.Visibility = Math.Max(0, entity.Visibility - previous) + value;
            if (grid == null)
            {
                grid = new AetheriaRuntimeEntityStatGridCommit
                {
                    Name = ReflectorVisibilityGrid,
                    Width = 1,
                    Height = 1
                };
                grids.Add(grid);
            }
            grid.Values = [value];
            entity.StatGrids = grids;
        }

        private static double ThermalPerformance(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int equipmentIndex)
        {
            return (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .FirstOrDefault(state => state != null && state.EquipmentIndex == equipmentIndex)?
                .ThermalPerformance ?? 1;
        }
    }
}
