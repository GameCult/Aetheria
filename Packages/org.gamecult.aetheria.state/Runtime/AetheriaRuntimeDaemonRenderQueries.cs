using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public enum AetheriaRuntimeGravityInfluenceKind
    {
        Planet,
        GasGiant,
        Sun
    }

    public readonly struct AetheriaRuntimeXzRect
    {
        public AetheriaRuntimeXzRect(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
    }

    public readonly struct AetheriaRuntimeGravityInfluenceBrush
    {
        public AetheriaRuntimeGravityInfluenceBrush(
            string bodyKey,
            string orbitKey,
            AetheriaRuntimeGravityInfluenceKind kind,
            double centerX,
            double centerZ,
            double radius,
            double gravityDepth,
            double gravityDepthExponent,
            double waveRadius,
            double waveDepth,
            double waveSpeed)
        {
            BodyKey = bodyKey ?? "";
            OrbitKey = orbitKey ?? "";
            Kind = kind;
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = Math.Max(0, radius);
            GravityDepth = gravityDepth;
            GravityDepthExponent = gravityDepthExponent;
            WaveRadius = Math.Max(0, waveRadius);
            WaveDepth = waveDepth;
            WaveSpeed = waveSpeed;
        }

        public string BodyKey { get; }
        public string OrbitKey { get; }
        public AetheriaRuntimeGravityInfluenceKind Kind { get; }
        public double CenterX { get; }
        public double CenterZ { get; }
        public double Radius { get; }
        public double GravityDepth { get; }
        public double GravityDepthExponent { get; }
        public double WaveRadius { get; }
        public double WaveDepth { get; }
        public double WaveSpeed { get; }
    }

    public static class AetheriaRuntimeDaemonRenderQueries
    {
        public static AetheriaRuntimeGravityInfluenceBrush[] QueryGravityInfluences(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            AetheriaRuntimeXzRect viewport)
        {
            var brushes = new List<AetheriaRuntimeGravityInfluenceBrush>();
            QueryGravityInfluences(zone, viewport, brushes);
            return brushes.Count == 0 ? Array.Empty<AetheriaRuntimeGravityInfluenceBrush>() : brushes.ToArray();
        }

        public static int QueryGravityInfluences(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            AetheriaRuntimeXzRect viewport,
            List<AetheriaRuntimeGravityInfluenceBrush> brushes)
        {
            if (brushes == null) throw new ArgumentNullException(nameof(brushes));
            brushes.Clear();
            if (zone == null)
                return 0;

            var orbitPositions = BuildOrbitPositions(zone);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null || !TryResolveBodyCenter(body, orbitPositions, out var center))
                    continue;

                var gravityRadius = ResolveGravityRadius(body);
                var waveRadius = ResolveWaveRadius(body);
                var radius = Math.Max(gravityRadius, waveRadius);
                if (!IntersectsCircle(viewport, center.x, center.z, radius))
                    continue;

                brushes.Add(new AetheriaRuntimeGravityInfluenceBrush(
                    body.BodyKey,
                    body.OrbitKey,
                    GravityKind(body),
                    center.x,
                    center.z,
                    radius,
                    ResolveGravityDepth(body),
                    body.GravityDepthExponent,
                    waveRadius,
                    ResolveWaveDepth(body),
                    ResolveWaveSpeed(body)));
            }

            return brushes.Count;
        }

        public static AetheriaRuntimeDaemonRenderGroupDocument[] QueryRenderGroups(
            AetheriaRuntimeDaemonSoaViewIndex? index,
            double minX,
            double minY,
            double minZ,
            double maxX,
            double maxY,
            double maxZ)
        {
            var groups = new List<AetheriaRuntimeDaemonRenderGroupDocument>();
            QueryRenderGroups(index, minX, minY, minZ, maxX, maxY, maxZ, groups);
            return groups.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>() : groups.ToArray();
        }

        public static int QueryRenderGroups(
            AetheriaRuntimeDaemonSoaViewIndex? index,
            double minX,
            double minY,
            double minZ,
            double maxX,
            double maxY,
            double maxZ,
            List<AetheriaRuntimeDaemonRenderGroupDocument> groups)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            groups.Clear();
            if (index == null)
                return 0;

            Normalize(ref minX, ref maxX);
            Normalize(ref minY, ref maxY);
            Normalize(ref minZ, ref maxZ);

            foreach (var group in index.RenderGroups)
            {
                if (group == null || !IntersectsBounds(group, minX, minY, minZ, maxX, maxY, maxZ))
                    continue;

                groups.Add(group);
            }

            return groups.Count;
        }

        private static Dictionary<string, AetheriaRuntimeXzPoint> BuildOrbitPositions(AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var source = new Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit>(StringComparer.Ordinal);
            foreach (var orbit in zone.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
            {
                if (orbit != null && !string.IsNullOrWhiteSpace(orbit.OrbitKey))
                    source[orbit.OrbitKey] = orbit;
            }

            var positions = new Dictionary<string, AetheriaRuntimeXzPoint>(StringComparer.Ordinal)
            {
                [""] = new AetheriaRuntimeXzPoint(0, 0)
            };
            foreach (var orbitKey in source.Keys)
                ResolveOrbitPosition(orbitKey, source, positions);

            return positions;
        }

        private static AetheriaRuntimeXzPoint ResolveOrbitPosition(
            string orbitKey,
            Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit> source,
            Dictionary<string, AetheriaRuntimeXzPoint> positions)
        {
            if (positions.TryGetValue(orbitKey ?? "", out var cached))
                return cached;
            if (!source.TryGetValue(orbitKey ?? "", out var orbit))
                return new AetheriaRuntimeXzPoint(0, 0);

            var parent = ResolveOrbitPosition(orbit.ParentOrbitKey ?? "", source, positions);
            var position = new AetheriaRuntimeXzPoint(
                parent.x + orbit.FixedPositionX + Math.Cos(orbit.Phase * Math.PI * 2) * orbit.Distance,
                parent.z + orbit.FixedPositionY + Math.Sin(orbit.Phase * Math.PI * 2) * orbit.Distance);
            positions[orbitKey ?? ""] = position;
            return position;
        }

        private static bool IntersectsCircle(AetheriaRuntimeXzRect rect, double centerX, double centerZ, double radius)
        {
            if (radius <= 0)
                return false;

            var nearestX = Clamp(centerX, rect.MinX, rect.MaxX);
            var nearestZ = Clamp(centerZ, rect.MinZ, rect.MaxZ);
            var dx = centerX - nearestX;
            var dz = centerZ - nearestZ;
            return dx * dx + dz * dz <= radius * radius;
        }

        private static bool IntersectsBounds(
            AetheriaRuntimeDaemonRenderGroupDocument group,
            double minX,
            double minY,
            double minZ,
            double maxX,
            double maxY,
            double maxZ)
        {
            var halfX = group.BoundsSizeX * 0.5;
            var halfY = group.BoundsSizeY * 0.5;
            var halfZ = group.BoundsSizeZ * 0.5;
            return group.BoundsCenterX + halfX >= minX &&
                   group.BoundsCenterX - halfX <= maxX &&
                   group.BoundsCenterY + halfY >= minY &&
                   group.BoundsCenterY - halfY <= maxY &&
                   group.BoundsCenterZ + halfZ >= minZ &&
                   group.BoundsCenterZ - halfZ <= maxZ;
        }

        private static AetheriaRuntimeGravityInfluenceKind GravityKind(AetheriaRuntimeBodySnapshotCommit body)
        {
            return (body.Kind ?? "").ToLowerInvariant() switch
            {
                "sun" => AetheriaRuntimeGravityInfluenceKind.Sun,
                "gas_giant" => AetheriaRuntimeGravityInfluenceKind.GasGiant,
                _ => AetheriaRuntimeGravityInfluenceKind.Planet
            };
        }

        private static bool TryResolveBodyCenter(
            AetheriaRuntimeBodySnapshotCommit body,
            Dictionary<string, AetheriaRuntimeXzPoint> orbitPositions,
            out AetheriaRuntimeXzPoint center)
        {
            if (IsFinite(body.GravityInfluenceCenterX) && IsFinite(body.GravityInfluenceCenterZ))
            {
                center = new AetheriaRuntimeXzPoint(body.GravityInfluenceCenterX, body.GravityInfluenceCenterZ);
                return true;
            }

            return orbitPositions.TryGetValue(body.OrbitKey ?? "", out center);
        }

        private static double ResolveGravityRadius(AetheriaRuntimeBodySnapshotCommit body)
        {
            return body.GravityInfluenceRadius > 0 && IsFinite(body.GravityInfluenceRadius)
                ? body.GravityInfluenceRadius
                : 0;
        }

        private static double ResolveGravityDepth(AetheriaRuntimeBodySnapshotCommit body)
        {
            return body.GravityWellDepth;
        }

        private static double ResolveWaveRadius(AetheriaRuntimeBodySnapshotCommit body)
        {
            return string.Equals(body.Kind, "gas_giant", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(body.Kind, "sun", StringComparison.OrdinalIgnoreCase)
                ? Math.Max(0, body.GravityWaveRadius)
                : 0;
        }

        private static double ResolveWaveDepth(AetheriaRuntimeBodySnapshotCommit body)
        {
            return body.GravityWaveDepth;
        }

        private static double ResolveWaveSpeed(AetheriaRuntimeBodySnapshotCommit body)
        {
            return body.GravityWaveSpeed;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void Normalize(ref double a, ref double b)
        {
            if (a > b)
                (a, b) = (b, a);
        }

        private readonly struct AetheriaRuntimeXzPoint
        {
            public AetheriaRuntimeXzPoint(double x, double z)
            {
                this.x = x;
                this.z = z;
            }

            public readonly double x;
            public readonly double z;
        }
    }
}
