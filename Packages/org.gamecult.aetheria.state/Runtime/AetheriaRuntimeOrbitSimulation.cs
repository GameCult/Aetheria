using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    /// <summary>
    /// Advances daemon-owned orbital phase, then projects the same canonical
    /// orbit graph into the physical pose and velocity consumed by Ymir and Eve.
    /// Period-zero orbits remain fixed compatibility orbits.
    /// </summary>
    public static class AetheriaRuntimeOrbitSimulation
    {
        public static void StepZone(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit>? entities,
            double deltaSeconds)
        {
            if (zone == null || deltaSeconds <= 0)
                return;

            foreach (var orbit in zone.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
            {
                if (orbit == null || orbit.Period <= 0.01)
                    continue;
                orbit.Phase = Fraction(orbit.Phase + deltaSeconds / orbit.Period);
            }

            var positions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
            foreach (var entity in entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.OrbitKey) ||
                    !positions.TryGetValue(entity.OrbitKey, out var position))
                    continue;

                var previousX = entity.PositionX;
                var previousZ = entity.PositionZ;
                entity.PositionX = position.x;
                entity.PositionZ = position.z;
                entity.VelocityX = (position.x - previousX) / deltaSeconds;
                entity.VelocityY = (position.z - previousZ) / deltaSeconds;
            }
        }

        private static double Fraction(double value) => value - Math.Floor(value);
    }
}
