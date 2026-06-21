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

    public readonly struct AetheriaRuntimeDaemonBodyPose
    {
        public AetheriaRuntimeDaemonBodyPose(
            string bodyKey,
            string orbitKey,
            string parentOrbitKey,
            string kind,
            double centerX,
            double centerZ,
            double parentCenterX,
            double parentCenterZ,
            double gravityWaveSpeed)
        {
            BodyKey = bodyKey ?? "";
            OrbitKey = orbitKey ?? "";
            ParentOrbitKey = parentOrbitKey ?? "";
            Kind = kind ?? "";
            CenterX = centerX;
            CenterZ = centerZ;
            ParentCenterX = parentCenterX;
            ParentCenterZ = parentCenterZ;
            GravityWaveSpeed = gravityWaveSpeed;
        }

        public string BodyKey { get; }
        public string OrbitKey { get; }
        public string ParentOrbitKey { get; }
        public string Kind { get; }
        public double CenterX { get; }
        public double CenterZ { get; }
        public double ParentCenterX { get; }
        public double ParentCenterZ { get; }
        public double GravityWaveSpeed { get; }
    }

    public readonly struct AetheriaRuntimeDaemonAsteroidBeltPose
    {
        public AetheriaRuntimeDaemonAsteroidBeltPose(
            string bodyKey,
            string orbitKey,
            double centerX,
            double centerZ,
            double radius,
            int asteroidCount)
        {
            BodyKey = bodyKey ?? "";
            OrbitKey = orbitKey ?? "";
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = Math.Max(0, radius);
            AsteroidCount = Math.Max(0, asteroidCount);
        }

        public string BodyKey { get; }
        public string OrbitKey { get; }
        public double CenterX { get; }
        public double CenterZ { get; }
        public double Radius { get; }
        public int AsteroidCount { get; }
    }

    public readonly struct AetheriaRuntimeDaemonAsteroidInstancePose
    {
        public AetheriaRuntimeDaemonAsteroidInstancePose(
            string bodyKey,
            int asteroidIndex,
            double positionX,
            double positionZ,
            double rotation,
            double size)
        {
            BodyKey = bodyKey ?? "";
            AsteroidIndex = Math.Max(0, asteroidIndex);
            PositionX = positionX;
            PositionZ = positionZ;
            Rotation = rotation;
            Size = Math.Max(0, size);
        }

        public string BodyKey { get; }
        public int AsteroidIndex { get; }
        public double PositionX { get; }
        public double PositionZ { get; }
        public double Rotation { get; }
        public double Size { get; }
    }

    public readonly struct AetheriaRuntimeDaemonCompassMarker
    {
        public AetheriaRuntimeDaemonCompassMarker(
            int targetEntityIndex,
            double positionX,
            double positionZ,
            double deltaX,
            double deltaZ,
            double distance,
            double infoGathered,
            bool hostile)
        {
            TargetEntityIndex = targetEntityIndex;
            PositionX = positionX;
            PositionZ = positionZ;
            DeltaX = deltaX;
            DeltaZ = deltaZ;
            Distance = Math.Max(0, distance);
            InfoGathered = infoGathered;
            Hostile = hostile;
        }

        public int TargetEntityIndex { get; }
        public double PositionX { get; }
        public double PositionZ { get; }
        public double DeltaX { get; }
        public double DeltaZ { get; }
        public double Distance { get; }
        public double InfoGathered { get; }
        public bool Hostile { get; }
    }

    public readonly struct AetheriaRuntimeDaemonWormholeExit
    {
        public AetheriaRuntimeDaemonWormholeExit(
            int targetZoneIndex,
            double directionX,
            double directionZ,
            double positionX,
            double positionZ)
        {
            TargetZoneIndex = targetZoneIndex;
            DirectionX = directionX;
            DirectionZ = directionZ;
            PositionX = positionX;
            PositionZ = positionZ;
        }

        public int TargetZoneIndex { get; }
        public double DirectionX { get; }
        public double DirectionZ { get; }
        public double PositionX { get; }
        public double PositionZ { get; }
    }

    public readonly struct AetheriaRuntimeGravityTerrainBand
    {
        public AetheriaRuntimeGravityTerrainBand(double startDepth, double depthRange)
        {
            StartDepth = startDepth;
            DepthRange = depthRange;
        }

        public double StartDepth { get; }
        public double DepthRange { get; }
    }

    public static class AetheriaRuntimeDaemonRenderQueries
    {
        public static double ResolveZoneRenderRadius(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            double fallbackRadius)
        {
            if (zone != null && zone.GravityTerrainRadius > 0)
                return zone.GravityTerrainRadius;

            return Math.Max(0, fallbackRadius);
        }

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

        public static AetheriaRuntimeDaemonBodyPose[] QueryBodyPoses(
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            var poses = new List<AetheriaRuntimeDaemonBodyPose>();
            QueryBodyPoses(zone, poses);
            return poses.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonBodyPose>() : poses.ToArray();
        }

        public static int QueryBodyPoses(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            List<AetheriaRuntimeDaemonBodyPose> poses)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            poses.Clear();
            if (zone == null)
                return 0;

            var orbitPositions = BuildOrbitPositions(zone);
            var orbits = BuildOrbitMap(zone);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null || !TryResolveBodyCenter(body, orbitPositions, out var center))
                    continue;

                var orbitKey = body.OrbitKey ?? "";
                var parentOrbitKey = orbits.TryGetValue(orbitKey, out var orbit) ? orbit.ParentOrbitKey ?? "" : "";
                var parentCenter = orbitPositions.TryGetValue(parentOrbitKey, out var parent)
                    ? parent
                    : new AetheriaRuntimeXzPoint(0, 0);
                poses.Add(new AetheriaRuntimeDaemonBodyPose(
                    body.BodyKey,
                    orbitKey,
                    parentOrbitKey,
                    body.Kind,
                    center.x,
                    center.z,
                    parentCenter.x,
                    parentCenter.z,
                    ResolveWaveSpeed(body)));
            }

            return poses.Count;
        }

        public static AetheriaRuntimeDaemonAsteroidBeltPose[] QueryAsteroidBeltPoses(
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            var poses = new List<AetheriaRuntimeDaemonAsteroidBeltPose>();
            QueryAsteroidBeltPoses(zone, poses);
            return poses.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonAsteroidBeltPose>() : poses.ToArray();
        }

        public static int QueryAsteroidBeltPoses(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            List<AetheriaRuntimeDaemonAsteroidBeltPose> poses)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            poses.Clear();
            if (zone == null)
                return 0;

            var orbitPositions = BuildOrbitPositions(zone);
            var orbits = BuildOrbitMap(zone);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null ||
                    !string.Equals(body.Kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase) ||
                    !TryResolveAsteroidBeltCenter(body, orbitPositions, orbits, out var center))
                    continue;

                poses.Add(new AetheriaRuntimeDaemonAsteroidBeltPose(
                    body.BodyKey,
                    body.OrbitKey,
                    center.x,
                    center.z,
                    ResolveAsteroidBeltRadius(body),
                    body.Asteroids?.Count ?? 0));
            }

            return poses.Count;
        }

        public static AetheriaRuntimeDaemonAsteroidInstancePose[] QueryAsteroidInstancePoses(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            string bodyKey,
            double simulationTimeSeconds)
        {
            var poses = new List<AetheriaRuntimeDaemonAsteroidInstancePose>();
            QueryAsteroidInstancePoses(zone, bodyKey, simulationTimeSeconds, poses);
            return poses.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonAsteroidInstancePose>() : poses.ToArray();
        }

        public static int QueryAsteroidInstancePoses(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            string bodyKey,
            double simulationTimeSeconds,
            List<AetheriaRuntimeDaemonAsteroidInstancePose> poses)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            poses.Clear();
            if (zone == null)
                return 0;

            var orbitPositions = BuildOrbitPositions(zone);
            var orbits = BuildOrbitMap(zone);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null ||
                    !string.Equals(body.BodyKey ?? "", bodyKey ?? "", StringComparison.Ordinal) ||
                    !string.Equals(body.Kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase) ||
                    !TryResolveAsteroidBeltCenter(body, orbitPositions, orbits, out var center))
                    continue;

                var asteroids = body.Asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>();
                for (var index = 0; index < asteroids.Count; index++)
                {
                    var asteroid = asteroids[index];
                    if (asteroid == null)
                        continue;

                    var phase = Fraction(asteroid.Phase);
                    var x = center.x + Math.Cos(phase * Math.PI * 2.0) * asteroid.Distance;
                    var z = center.z + Math.Sin(phase * Math.PI * 2.0) * asteroid.Distance;
                    var size = asteroid.RespawnTimer > 0
                        ? 0
                        : Math.Max(0, asteroid.Size - asteroid.Damage);
                    var rotation = simulationTimeSeconds * asteroid.RotationSpeed;
                    poses.Add(new AetheriaRuntimeDaemonAsteroidInstancePose(
                        body.BodyKey ?? "",
                        index,
                        x,
                        z,
                        rotation,
                        size));
                }
            }

            return poses.Count;
        }

        public static AetheriaRuntimeDaemonCompassMarker[] QueryCompassMarkers(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            double minimumDistance)
        {
            var markers = new List<AetheriaRuntimeDaemonCompassMarker>();
            QueryCompassMarkers(zone, observerEntityIndex, minimumInfoGathered, minimumDistance, markers);
            return markers.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonCompassMarker>() : markers.ToArray();
        }

        public static int QueryCompassMarkers(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            double minimumDistance,
            List<AetheriaRuntimeDaemonCompassMarker> markers)
        {
            if (markers == null) throw new ArgumentNullException(nameof(markers));
            markers.Clear();
            if (zone == null || observerEntityIndex < 0)
                return 0;

            var entities = BuildEntityMap(zone);
            if (!entities.TryGetValue(observerEntityIndex, out var observer))
                return 0;

            var requiredDistance = Math.Max(0, minimumDistance);
            foreach (var contact in observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            {
                if (contact == null ||
                    !contact.Visible ||
                    contact.InfoGathered <= minimumInfoGathered ||
                    !entities.TryGetValue(contact.TargetEntityIndex, out var target))
                {
                    continue;
                }

                var deltaX = target.PositionX - observer.PositionX;
                var deltaZ = target.PositionZ - observer.PositionZ;
                var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                if (distance <= requiredDistance)
                    continue;

                markers.Add(new AetheriaRuntimeDaemonCompassMarker(
                    target.EntityIndex,
                    target.PositionX,
                    target.PositionZ,
                    deltaX,
                    deltaZ,
                    distance,
                    contact.InfoGathered,
                    contact.Hostile));
            }

            return markers.Count;
        }

        public static int[] QueryVisibleEntityIndices(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered)
        {
            var entityIndices = new List<int>();
            QueryVisibleEntityIndices(zone, observerEntityIndex, minimumInfoGathered, entityIndices);
            return entityIndices.Count == 0 ? Array.Empty<int>() : entityIndices.ToArray();
        }

        public static int QueryVisibleEntityIndices(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            List<int> entityIndices)
        {
            if (entityIndices == null) throw new ArgumentNullException(nameof(entityIndices));
            entityIndices.Clear();
            if (zone == null || observerEntityIndex < 0)
                return 0;

            var entities = BuildEntityMap(zone);
            if (!entities.TryGetValue(observerEntityIndex, out var observer))
                return 0;

            entityIndices.Add(observer.EntityIndex);
            foreach (var contact in observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            {
                if (contact != null &&
                    contact.Visible &&
                    contact.InfoGathered > minimumInfoGathered &&
                    entities.ContainsKey(contact.TargetEntityIndex))
                {
                    entityIndices.Add(contact.TargetEntityIndex);
                }
            }

            return entityIndices.Count;
        }

        public static AetheriaRuntimeDaemonWormholeExit[] QueryWormholeExits(
            AetheriaRuntimeRunCheckpointCommit? run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            double zoneRadius,
            double wormholeDistanceRatio)
        {
            var exits = new List<AetheriaRuntimeDaemonWormholeExit>();
            QueryWormholeExits(run, zone, zoneRadius, wormholeDistanceRatio, exits);
            return exits.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonWormholeExit>() : exits.ToArray();
        }

        public static int QueryWormholeExits(
            AetheriaRuntimeRunCheckpointCommit? run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            double zoneRadius,
            double wormholeDistanceRatio,
            List<AetheriaRuntimeDaemonWormholeExit> exits)
        {
            if (exits == null) throw new ArgumentNullException(nameof(exits));
            exits.Clear();
            if (run == null || zone == null)
                return 0;

            var zones = BuildZoneMap(run);
            var distance = Math.Max(0, zoneRadius) * Math.Max(0, wormholeDistanceRatio);
            foreach (var targetZoneIndex in zone.AdjacentZoneIndices ?? Array.Empty<int>())
            {
                if (!zones.TryGetValue(targetZoneIndex, out var target))
                    continue;

                var deltaX = target.PositionX - zone.PositionX;
                var deltaZ = target.PositionY - zone.PositionY;
                var magnitude = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                if (magnitude <= 0)
                    continue;

                var directionX = deltaX / magnitude;
                var directionZ = deltaZ / magnitude;
                exits.Add(new AetheriaRuntimeDaemonWormholeExit(
                    target.ZoneIndex,
                    directionX,
                    directionZ,
                    directionX * distance,
                    directionZ * distance));
            }

            return exits.Count;
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

        public static double EvaluateGravityTerrainHeight(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            double positionX,
            double positionZ,
            double simulationTimeSeconds)
        {
            if (zone == null)
                return 0;

            var height = 0.0;
            if (zone.GravityTerrainRadius > 0 && zone.GravityTerrainDepth != 0)
            {
                var distance = Math.Sqrt(positionX * positionX + positionZ * positionZ);
                height -= PowerPulse(
                    distance / (zone.GravityTerrainRadius * 2.0),
                    Math.Max(0.0001, zone.GravityTerrainDepthExponent)) * zone.GravityTerrainDepth;
            }

            foreach (var brush in QueryGravityInfluences(
                         zone,
                         new AetheriaRuntimeXzRect(positionX, positionZ, positionX, positionZ)))
            {
                var dx = positionX - brush.CenterX;
                var dz = positionZ - brush.CenterZ;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                if (brush.Radius > 0 && distance < brush.Radius && brush.GravityDepth != 0)
                {
                    height -= PowerPulse(
                        distance / brush.Radius,
                        Math.Max(0.0001, brush.GravityDepthExponent)) * brush.GravityDepth;
                }

                if (brush.WaveRadius > 0 && distance < brush.WaveRadius && brush.WaveDepth != 0)
                {
                    height -= RadialWaves(
                        distance / brush.WaveRadius,
                        8.0,
                        1.25,
                        zone.GravityTerrainWaveFrequency,
                        simulationTimeSeconds * brush.WaveSpeed) * brush.WaveDepth;
                }
            }

            return height;
        }

        public static AetheriaRuntimeGravityTerrainBand QueryGravityTerrainBand(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            double minimapGravityRange,
            double maxDepth)
        {
            if (zone == null)
                return new AetheriaRuntimeGravityTerrainBand(0, Math.Max(0, maxDepth));

            var startDepth = PowerPulse(
                minimapGravityRange,
                Math.Max(0.0001, zone.GravityTerrainDepthExponent)) * zone.GravityTerrainDepth;
            return new AetheriaRuntimeGravityTerrainBand(
                startDepth,
                zone.GravityTerrainDepth - startDepth + maxDepth);
        }

        private static Dictionary<int, AetheriaRuntimeEntitySnapshotCommit> BuildEntityMap(AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var entities = new Dictionary<int, AetheriaRuntimeEntitySnapshotCommit>();
            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity != null && entity.EntityIndex >= 0)
                    entities[entity.EntityIndex] = entity;
            }

            return entities;
        }

        private static Dictionary<int, AetheriaRuntimeZoneSnapshotCommit> BuildZoneMap(AetheriaRuntimeRunCheckpointCommit run)
        {
            var zones = new Dictionary<int, AetheriaRuntimeZoneSnapshotCommit>();
            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            {
                if (zone != null && zone.ZoneIndex >= 0)
                    zones[zone.ZoneIndex] = zone;
            }

            return zones;
        }

        private static Dictionary<string, AetheriaRuntimeXzPoint> BuildOrbitPositions(AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var source = BuildOrbitMap(zone);

            var positions = new Dictionary<string, AetheriaRuntimeXzPoint>(StringComparer.Ordinal)
            {
                [""] = new AetheriaRuntimeXzPoint(0, 0)
            };
            foreach (var orbitKey in source.Keys)
                ResolveOrbitPosition(orbitKey, source, positions);

            return positions;
        }

        private static Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit> BuildOrbitMap(
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var source = new Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit>(StringComparer.Ordinal);
            foreach (var orbit in zone.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
            {
                if (orbit != null && !string.IsNullOrWhiteSpace(orbit.OrbitKey))
                    source[orbit.OrbitKey] = orbit;
            }

            return source;
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

        private static bool TryResolveAsteroidBeltCenter(
            AetheriaRuntimeBodySnapshotCommit body,
            Dictionary<string, AetheriaRuntimeXzPoint> orbitPositions,
            Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit> orbits,
            out AetheriaRuntimeXzPoint center)
        {
            if (orbits.TryGetValue(body.OrbitKey ?? "", out var orbit) &&
                orbitPositions.TryGetValue(orbit.ParentOrbitKey ?? "", out center))
            {
                return true;
            }

            return TryResolveBodyCenter(body, orbitPositions, out center);
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

        private static double ResolveAsteroidBeltRadius(AetheriaRuntimeBodySnapshotCommit body)
        {
            var radius = 0.0;
            foreach (var asteroid in body.Asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>())
            {
                if (asteroid != null && asteroid.Distance > radius)
                    radius = asteroid.Distance;
            }

            return radius;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static double PowerPulse(double x, double exponent)
        {
            x *= 2.0;
            x = Clamp(x, -1.0, 1.0);
            return Math.Pow((x + 1.0) * (1.0 - x), exponent);
        }

        private static double Fraction(double value)
        {
            return value - Math.Floor(value);
        }

        private static double RadialWaves(
            double x,
            double maskExponent,
            double sineExponent,
            double frequency,
            double phase)
        {
            return PowerPulse(x, maskExponent) *
                   Math.Cos(Math.Pow(x * 2.0, sineExponent) * frequency + phase);
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
