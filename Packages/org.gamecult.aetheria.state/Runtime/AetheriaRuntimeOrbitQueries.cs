using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public readonly struct AetheriaRuntimeOrbitPosition
    {
        public AetheriaRuntimeOrbitPosition(double x, double z)
        {
            this.x = x;
            this.z = z;
        }

        public readonly double x;
        public readonly double z;
    }

    public static class AetheriaRuntimeOrbitQueries
    {
        public static bool TryResolveBodyPosition(
            AetheriaRuntimeBodySnapshotCommit? body,
            IReadOnlyDictionary<string, AetheriaRuntimeOrbitPosition> positions,
            out AetheriaRuntimeOrbitPosition position)
        {
            position = default;
            if (body == null)
                return false;
            if (double.IsFinite(body.GravityInfluenceCenterX) &&
                double.IsFinite(body.GravityInfluenceCenterZ))
            {
                position = new AetheriaRuntimeOrbitPosition(
                    body.GravityInfluenceCenterX,
                    body.GravityInfluenceCenterZ);
                return true;
            }
            return positions.TryGetValue(body.OrbitKey ?? "", out position);
        }

        public static Dictionary<string, AetheriaRuntimeOrbitPosition> BuildPositions(
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            var source = new Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit>(StringComparer.Ordinal);
            foreach (var orbit in zone?.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
            {
                if (orbit != null && !string.IsNullOrWhiteSpace(orbit.OrbitKey))
                    source[orbit.OrbitKey] = orbit;
            }

            var positions = new Dictionary<string, AetheriaRuntimeOrbitPosition>(StringComparer.Ordinal)
            {
                [""] = new AetheriaRuntimeOrbitPosition(0, 0)
            };
            foreach (var orbitKey in source.Keys)
                ResolvePosition(orbitKey, source, positions, new HashSet<string>(StringComparer.Ordinal));

            return positions;
        }

        private static AetheriaRuntimeOrbitPosition ResolvePosition(
            string orbitKey,
            IReadOnlyDictionary<string, AetheriaRuntimeOrbitSnapshotCommit> source,
            IDictionary<string, AetheriaRuntimeOrbitPosition> positions,
            ISet<string> resolving)
        {
            orbitKey ??= "";
            if (positions.TryGetValue(orbitKey, out var cached))
                return cached;
            if (!source.TryGetValue(orbitKey, out var orbit) || !resolving.Add(orbitKey))
                return new AetheriaRuntimeOrbitPosition(0, 0);

            var parent = ResolvePosition(orbit.ParentOrbitKey ?? "", source, positions, resolving);
            resolving.Remove(orbitKey);
            var position = new AetheriaRuntimeOrbitPosition(
                parent.x + orbit.FixedPositionX + Math.Cos(orbit.Phase * Math.PI * 2) * orbit.Distance,
                parent.z + orbit.FixedPositionY + Math.Sin(orbit.Phase * Math.PI * 2) * orbit.Distance);
            positions[orbitKey] = position;
            return position;
        }
    }
}
