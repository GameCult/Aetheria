using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVisibilitySimulation
    {
        private const string TransientVisibilityPrefix = "transient-visibility:";
        private const string ReflectorVisibilityGrid = "reflector-visibility";
        private const string EquipmentVisibilityGrid = "equipment-visibility";

        public static void BeginTick(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit>? entities,
            double deltaSeconds,
            double visibilityDecay)
        {
            if (deltaSeconds <= 0)
                return;
            foreach (var entity in entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;
                var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
                foreach (var grid in grids.Where(candidate =>
                             candidate != null &&
                             (candidate.Name ?? "").StartsWith(TransientVisibilityPrefix, StringComparison.Ordinal)).ToArray())
                {
                    var previous = grid.Values.FirstOrDefault();
                    var next = previous * Math.Exp(-Math.Max(0, visibilityDecay) * deltaSeconds);
                    if (next < 0.1)
                    {
                        next = 0;
                        grids.Remove(grid);
                    }
                    else
                    {
                        grid.Values = new[] { next };
                    }
                    entity.Visibility = Math.Max(0, entity.Visibility - previous) + next;
                }
                entity.StatGrids = grids;
            }
        }

        public static void SetTransientSource(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string sourceKey,
            double value)
        {
            SetVisibilitySource(entity, TransientVisibilityPrefix + (sourceKey ?? ""), Math.Max(0, value));
        }

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

                var equipmentVisibility = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(
                        entity, catalog, AetheriaRuntimeBehaviorKinds.Visibility)
                    .Sum(behavior => Math.Max(0, behavior.EvaluateStat(
                        1, ThermalPerformance(entity, behavior.EquipmentIndex))));
                SetVisibilitySource(entity, EquipmentVisibilityGrid, equipmentVisibility);
                var light = suns.Sum(sun => StellarLight(
                    entity.PositionX - sun.position.x,
                    entity.PositionZ - sun.position.z,
                    sun.radius));
                var reflected = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(
                        entity, catalog, AetheriaRuntimeBehaviorKinds.Reflector)
                    .Sum(behavior => Math.Max(0, behavior.EvaluateStat(
                        1, ThermalPerformance(entity, behavior.EquipmentIndex))) * light);
                SetVisibilitySource(entity, ReflectorVisibilityGrid, reflected);
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

        private static void SetVisibilitySource(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string sourceName,
            double value)
        {
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var grid = grids.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, sourceName, StringComparison.Ordinal));
            var previous = grid?.Values.FirstOrDefault() ?? 0;
            entity.Visibility = Math.Max(0, entity.Visibility - previous) + value;
            if (grid == null)
            {
                grid = new AetheriaRuntimeEntityStatGridCommit
                {
                    Name = sourceName,
                    Width = 1,
                    Height = 1
                };
                grids.Add(grid);
            }
            grid.Values = new[] { value };
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
