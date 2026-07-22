using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Geometry;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public enum AetheriaRuntimeGravityInfluenceKind
    {
        Planet,
        GasGiant,
        Sun
    }

    /// <summary>
    /// Horizontal X/Z viewport boundary for callers projecting the world into a two-dimensional surface.
    /// </summary>
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

        public CultRect ToAetheriaXyRect()
        {
            return new CultRect(
                (float)MinX,
                (float)MinZ,
                (float)MaxX,
                (float)MaxZ);
        }
    }

    [MessagePackObject(true)]
    public readonly struct AetheriaRuntimeExponentialCurve
    {
        public AetheriaRuntimeExponentialCurve(double exponent, double multiplier, double constant)
        {
            Exponent = exponent;
            Multiplier = multiplier;
            Constant = constant;
        }

        public double Exponent { get; }
        public double Multiplier { get; }
        public double Constant { get; }

        public double Evaluate(double value)
        {
            return Multiplier * Math.Pow(value, Exponent) + Constant;
        }
    }

    [MessagePackObject(true)]
    public readonly struct AetheriaRuntimeDaemonRenderSettings
    {
        public static AetheriaRuntimeDaemonRenderSettings AetheriaDefault { get; } =
            new AetheriaRuntimeDaemonRenderSettings(
                new AetheriaRuntimeExponentialCurve(3.0, 0.000000001, 0.0),
                new AetheriaRuntimeExponentialLerp(4.0, 0.01, 10.0),
                new AetheriaRuntimeExponentialLerp(2.0, 5.0, 720.0),
                50.0,
                273.0,
                330.0,
                0.25,
                0.1,
                256.0,
                heatstrokePhasingFloor: 0.0,
                heatstrokePhasingFrequency: 5.0,
                minimapZoomLevels: new[] { 250.0, 500.0, 1000.0, 2000.0, 4000.0 },
                defaultMinimapZoom: 3,
                wormholeDistanceRatio: 0.75,
                defaultViewDistance: 4096.0,
                minimapIconScale: 0.125,
                minimapAsteroidSize: 3.0,
                bodyIconSizeCurve: new AetheriaRuntimeExponentialCurve(0.15, 10.0, 25.0),
                minimapZoneGravityRange: 0.45,
                asteroidVerticalOffset: -10.0,
                planetRotationSpeed: 0.1,
                zoneBoundaryPower: 2.0,
                zoneBoundaryDepth: 64.0,
                asteroidMeshCount: 4,
                bodyRadiusCurve: new AetheriaRuntimeExponentialCurve(0.25, 3.0, 0.0),
                lightRadiusCurve: new AetheriaRuntimeExponentialCurve(0.25, 300.0, 0.0),
                gravityWaveFrequencyCurve: new AetheriaRuntimeExponentialCurve(0.45, 0.2, 0.0),
                useValue3DFlow: true);

        public AetheriaRuntimeDaemonRenderSettings(
            AetheriaRuntimeExponentialCurve temperatureEmissionCurve,
            AetheriaRuntimeExponentialLerp lockIndicatorFrequency,
            AetheriaRuntimeExponentialLerp lockSpinSpeed,
            double convergenceMinimumDistance,
            double hypothermiaTemperature,
            double heatstrokeTemperature,
            double severeHeatstrokeRiskThreshold,
            double targetDetectionInfoThreshold,
            double lockIndicatorNoiseAmplitude,
            double heatstrokePhasingFloor = 0.0,
            double heatstrokePhasingFrequency = 5.0,
            double targetSpottedBlinkFrequency = 20.0,
            double targetSpottedBlinkOffset = -0.25,
            IReadOnlyList<double>? minimapZoomLevels = null,
            int defaultMinimapZoom = 0,
            double wormholeDistanceRatio = 1.0,
            double defaultViewDistance = 0.0,
            double minimapIconScale = 0.0,
            double minimapAsteroidSize = 0.0,
            AetheriaRuntimeExponentialCurve bodyIconSizeCurve = default,
            double minimapZoneGravityRange = 0.0,
            double asteroidVerticalOffset = 0.0,
            double planetRotationSpeed = 0.0,
            double zoneBoundaryPower = 0.0,
            double zoneBoundaryDepth = 0.0,
            int asteroidMeshCount = int.MaxValue,
            AetheriaRuntimeExponentialCurve bodyRadiusCurve = default,
            AetheriaRuntimeExponentialCurve lightRadiusCurve = default,
            AetheriaRuntimeExponentialCurve gravityWaveFrequencyCurve = default,
            bool useValue3DFlow = false)
        {
            TemperatureEmissionCurve = temperatureEmissionCurve;
            LockIndicatorFrequency = lockIndicatorFrequency;
            LockSpinSpeed = lockSpinSpeed;
            ConvergenceMinimumDistance = convergenceMinimumDistance;
            HypothermiaTemperature = hypothermiaTemperature;
            HeatstrokeTemperature = heatstrokeTemperature;
            SevereHeatstrokeRiskThreshold = severeHeatstrokeRiskThreshold;
            TargetDetectionInfoThreshold = targetDetectionInfoThreshold;
            LockIndicatorNoiseAmplitude = lockIndicatorNoiseAmplitude;
            HeatstrokePhasingFloor = heatstrokePhasingFloor;
            HeatstrokePhasingFrequency = heatstrokePhasingFrequency;
            TargetSpottedBlinkFrequency = targetSpottedBlinkFrequency;
            TargetSpottedBlinkOffset = targetSpottedBlinkOffset;
            MinimapZoomLevels = CopyPositiveMinimapZoomLevels(minimapZoomLevels);
            DefaultMinimapZoom = defaultMinimapZoom;
            WormholeDistanceRatio = Math.Max(0.0, wormholeDistanceRatio);
            DefaultViewDistance = Math.Max(0.0, defaultViewDistance);
            MinimapIconScale = Math.Max(0.0, minimapIconScale);
            MinimapAsteroidSize = Math.Max(0.0, minimapAsteroidSize);
            BodyIconSizeCurve = bodyIconSizeCurve;
            MinimapZoneGravityRange = Math.Max(0.0, minimapZoneGravityRange);
            AsteroidVerticalOffset = asteroidVerticalOffset;
            PlanetRotationSpeed = planetRotationSpeed;
            ZoneBoundaryPower = zoneBoundaryPower;
            ZoneBoundaryDepth = zoneBoundaryDepth;
            AsteroidMeshCount = Math.Max(0, asteroidMeshCount);
            BodyRadiusCurve = bodyRadiusCurve;
            LightRadiusCurve = lightRadiusCurve;
            GravityWaveFrequencyCurve = gravityWaveFrequencyCurve;
            UseValue3DFlow = useValue3DFlow;
        }

        public AetheriaRuntimeExponentialCurve TemperatureEmissionCurve { get; }
        public AetheriaRuntimeExponentialLerp LockIndicatorFrequency { get; }
        public AetheriaRuntimeExponentialLerp LockSpinSpeed { get; }
        public double ConvergenceMinimumDistance { get; }
        public double HypothermiaTemperature { get; }
        public double HeatstrokeTemperature { get; }
        public double SevereHeatstrokeRiskThreshold { get; }
        public double TargetDetectionInfoThreshold { get; }
        public double LockIndicatorNoiseAmplitude { get; }
        public double HeatstrokePhasingFloor { get; }
        public double HeatstrokePhasingFrequency { get; }
        public double TargetSpottedBlinkFrequency { get; }
        public double TargetSpottedBlinkOffset { get; }
        public IReadOnlyList<double> MinimapZoomLevels { get; }
        public int DefaultMinimapZoom { get; }
        public double WormholeDistanceRatio { get; }
        public double DefaultViewDistance { get; }
        public double MinimapIconScale { get; }
        public double MinimapAsteroidSize { get; }
        public AetheriaRuntimeExponentialCurve BodyIconSizeCurve { get; }
        public double MinimapZoneGravityRange { get; }
        public double AsteroidVerticalOffset { get; }
        public double PlanetRotationSpeed { get; }
        public double ZoneBoundaryPower { get; }
        public double ZoneBoundaryDepth { get; }
        public int AsteroidMeshCount { get; }
        public AetheriaRuntimeExponentialCurve BodyRadiusCurve { get; }
        public AetheriaRuntimeExponentialCurve LightRadiusCurve { get; }
        public AetheriaRuntimeExponentialCurve GravityWaveFrequencyCurve { get; }
        public bool UseValue3DFlow { get; }

        public int ResolveDefaultMinimapZoomIndex()
        {
            var levels = ResolveMinimapZoomLevels();
            return ClampIndex(DefaultMinimapZoom, levels.Count);
        }

        public int ResolveNextMinimapZoomIndex(int currentIndex)
        {
            var levels = ResolveMinimapZoomLevels();
            if (levels.Count == 0)
                return 0;

            return currentIndex < 0
                ? ResolveDefaultMinimapZoomIndex()
                : (currentIndex + 1) % levels.Count;
        }

        public double ResolveMinimapDistance(int zoomIndex)
        {
            var levels = ResolveMinimapZoomLevels();
            return levels[ClampIndex(zoomIndex, levels.Count)];
        }

        public double ResolveDefaultMinimapDistance()
        {
            return ResolveMinimapDistance(ResolveDefaultMinimapZoomIndex());
        }

        public double ResolveMinimapIconSize(double minimapDistance)
        {
            return Math.Max(0.0, minimapDistance) * MinimapIconScale;
        }

        public double ResolveBodyIconSize(double mass)
        {
            return BodyIconSizeCurve.Evaluate(Math.Max(0.0, mass));
        }

        public double ResolveBodyRadius(double mass)
        {
            return BodyRadiusCurve.Evaluate(Math.Max(0.0, mass));
        }

        public double ResolveLightRadius(double mass)
        {
            return LightRadiusCurve.Evaluate(Math.Max(0.0, mass));
        }

        public double ResolveGravityWaveFrequency(double mass)
        {
            return GravityWaveFrequencyCurve.Evaluate(Math.Max(0.0, mass));
        }

        public double NormalizeThermalRisk(double temperature)
        {
            var range = HeatstrokeTemperature - HypothermiaTemperature;
            if (range <= 0)
                return temperature >= HeatstrokeTemperature ? 1.0 : 0.0;

            return Math.Max(0.0, Math.Min(1.0, (temperature - HypothermiaTemperature) / range));
        }

        public double NormalizeHeatstrokePost(double heatstroke)
        {
            if (SevereHeatstrokeRiskThreshold <= 0)
                return heatstroke > 0 ? 1.0 : 0.0;

            return Saturate(heatstroke / SevereHeatstrokeRiskThreshold);
        }

        public double NormalizeSevereHeatstrokePost(double heatstroke)
        {
            var range = 1.0 - SevereHeatstrokeRiskThreshold;
            if (range <= 0)
                return heatstroke >= 1.0 ? 1.0 : 0.0;

            return Saturate((heatstroke - SevereHeatstrokeRiskThreshold) / range);
        }

        public double ResolveSevereHeatstrokePostWeight(double heatstroke, double timeSeconds)
        {
            var normalized = NormalizeSevereHeatstrokePost(heatstroke);
            return normalized + normalized * (1.0 - normalized) *
                Math.Max(HeatstrokePhasingFloor, Math.Sin(timeSeconds * HeatstrokePhasingFrequency));
        }

        public double NormalizeDetectionProgress(double infoGathered)
        {
            return TargetDetectionInfoThreshold <= 0
                ? (infoGathered > 0 ? 1.0 : 0.0)
                : Saturate(infoGathered / TargetDetectionInfoThreshold);
        }

        public bool ResolveTargetSpottedFillEnabled(double infoGathered, double timeSeconds)
        {
            return !(infoGathered > TargetDetectionInfoThreshold) ||
                   Math.Sin(TargetSpottedBlinkFrequency * timeSeconds) + TargetSpottedBlinkOffset > 0;
        }

        public double NormalizeTargetVisibilityFill(double infoGathered)
        {
            var denominator = 1.0 - TargetDetectionInfoThreshold;
            var normalized = denominator <= 0
                ? (infoGathered >= 1.0 ? 1.0 : 0.0)
                : (infoGathered - TargetDetectionInfoThreshold) / denominator;
            return Lerp(0.25, 0.75, normalized);
        }

        public double NormalizeVisibilityToTargetFill(double infoGathered)
        {
            return Lerp(0.25, 0.75, NormalizeDetectionProgress(infoGathered));
        }

        public double NormalizeTargetStatusFill(double normalizedValue)
        {
            return Lerp(0.25, 0.75, normalizedValue);
        }

        public double ResolveLockIndicatorNoiseAmplitude(double lockProgress)
        {
            return LockIndicatorNoiseAmplitude * (1.0 - Saturate(lockProgress));
        }

        public double ResolveLockIndicatorNoiseFrequency(double lockProgress)
        {
            return LockIndicatorFrequency.Evaluate(lockProgress);
        }

        public double ResolveLockSpinSpeed(double lockProgress)
        {
            return LockSpinSpeed.Evaluate(lockProgress);
        }

        private static double Saturate(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double Lerp(double minimum, double maximum, double value)
        {
            return minimum + Saturate(value) * (maximum - minimum);
        }

        private IReadOnlyList<double> ResolveMinimapZoomLevels()
        {
            return MinimapZoomLevels != null && MinimapZoomLevels.Count > 0
                ? MinimapZoomLevels
                : DefaultMinimapZoomLevels;
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 1)
                return 0;

            return Math.Max(0, Math.Min(count - 1, index));
        }

        private static IReadOnlyList<double> CopyPositiveMinimapZoomLevels(IReadOnlyList<double>? levels)
        {
            if (levels == null || levels.Count == 0)
                return DefaultMinimapZoomLevels;

            var copy = new List<double>(levels.Count);
            for (var index = 0; index < levels.Count; index++)
            {
                var level = levels[index];
                if (level > 0 && !double.IsNaN(level) && !double.IsInfinity(level))
                    copy.Add(level);
            }

            return copy.Count > 0 ? copy.ToArray() : DefaultMinimapZoomLevels;
        }

        private static readonly IReadOnlyList<double> DefaultMinimapZoomLevels = new[] { 1000.0 };
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
            double waveSpeed,
            double waveFrequency)
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
            WaveFrequency = waveFrequency;
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
        public double WaveFrequency { get; }
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

    public readonly struct AetheriaRuntimeDaemonBodyView
    {
        public AetheriaRuntimeDaemonBodyView(
            AetheriaRuntimeBodySnapshotCommit body,
            AetheriaRuntimeDaemonBodyPose pose,
            bool isAsteroidBelt)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Pose = pose;
            IsAsteroidBelt = isAsteroidBelt;
        }

        public AetheriaRuntimeBodySnapshotCommit Body { get; }
        public AetheriaRuntimeDaemonBodyPose Pose { get; }
        public bool IsAsteroidBelt { get; }
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

    public readonly struct AetheriaRuntimeDaemonEntityContact
    {
        public AetheriaRuntimeDaemonEntityContact(
            int observerEntityIndex,
            int targetEntityIndex,
            double targetPositionX,
            double targetPositionZ,
            double deltaX,
            double deltaZ,
            double distance,
            double infoGathered,
            bool hostile,
            bool visible)
        {
            ObserverEntityIndex = observerEntityIndex;
            TargetEntityIndex = targetEntityIndex;
            TargetPositionX = targetPositionX;
            TargetPositionZ = targetPositionZ;
            DeltaX = deltaX;
            DeltaZ = deltaZ;
            Distance = Math.Max(0, distance);
            InfoGathered = infoGathered;
            Hostile = hostile;
            Visible = visible;
        }

        public int ObserverEntityIndex { get; }
        public int TargetEntityIndex { get; }
        public double TargetPositionX { get; }
        public double TargetPositionZ { get; }
        public double DeltaX { get; }
        public double DeltaZ { get; }
        public double Distance { get; }
        public double InfoGathered { get; }
        public bool Hostile { get; }
        public bool Visible { get; }
    }

    public readonly struct AetheriaRuntimeDaemonEntityTarget
    {
        public AetheriaRuntimeDaemonEntityTarget(
            int observerEntityIndex,
            int targetEntityIndex,
            double targetPositionX,
            double targetPositionZ,
            double deltaX,
            double deltaZ,
            double distance)
        {
            ObserverEntityIndex = observerEntityIndex;
            TargetEntityIndex = targetEntityIndex;
            TargetPositionX = targetPositionX;
            TargetPositionZ = targetPositionZ;
            DeltaX = deltaX;
            DeltaZ = deltaZ;
            Distance = Math.Max(0, distance);
        }

        public int ObserverEntityIndex { get; }
        public int TargetEntityIndex { get; }
        public double TargetPositionX { get; }
        public double TargetPositionZ { get; }
        public double DeltaX { get; }
        public double DeltaZ { get; }
        public double Distance { get; }
    }

    public readonly struct AetheriaRuntimeDaemonObjectViewportEntity
    {
        public AetheriaRuntimeDaemonObjectViewportEntity(
            int entityIndex,
            string entityKey,
            string displayName,
            double3 position,
            double2 xy,
            double2 direction,
            double2 velocity,
            bool controlled)
        {
            EntityIndex = entityIndex;
            EntityKey = entityKey ?? "";
            DisplayName = displayName ?? "";
            Position = position;
            Xy = xy;
            Direction = direction;
            Velocity = velocity;
            Controlled = controlled;
        }

        public int EntityIndex { get; }
        public string EntityKey { get; }
        public string DisplayName { get; }
        public double3 Position { get; }
        public double2 Xy { get; }
        public double2 Direction { get; }
        public double2 Velocity { get; }
        public bool Controlled { get; }
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

    public readonly struct AetheriaRuntimeEffectiveContact
    {
        public AetheriaRuntimeEffectiveContact(
            int observerEntityIndex,
            int primarySensorSourceEntityIndex,
            AetheriaRuntimeEntityContactCommit contact)
        {
            ObserverEntityIndex = observerEntityIndex;
            PrimarySensorSourceEntityIndex = primarySensorSourceEntityIndex;
            Contact = contact ?? throw new ArgumentNullException(nameof(contact));
        }

        public int ObserverEntityIndex { get; }
        public int PrimarySensorSourceEntityIndex { get; }
        public AetheriaRuntimeEntityContactCommit Contact { get; }
    }

    public static class AetheriaRuntimeDaemonRenderQueries
    {
        public const double DefaultZoneRenderRadius = 2000.0;

        public static AetheriaRuntimeEffectiveContact[] QueryEffectiveContacts(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex)
        {
            if (zone == null || observerEntityIndex < 0)
                return Array.Empty<AetheriaRuntimeEffectiveContact>();

            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var observer = entities.FirstOrDefault(entity =>
                entity != null && entity.EntityIndex == observerEntityIndex);
            if (observer == null)
                return Array.Empty<AetheriaRuntimeEffectiveContact>();

            var sources = new List<AetheriaRuntimeEntitySnapshotCommit> { observer };
            var dockParent = entities.FirstOrDefault(entity => entity != null &&
                (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(observerEntityIndex));
            if (dockParent != null && dockParent.EntityIndex != observerEntityIndex)
                sources.Add(dockParent);

            var contacts = new Dictionary<int, AetheriaRuntimeEffectiveContact>();
            foreach (var source in sources)
            foreach (var contact in source.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            {
                if (contact == null || contact.TargetEntityIndex < 0)
                    continue;

                if (!contacts.TryGetValue(contact.TargetEntityIndex, out var previous))
                {
                    contacts.Add(contact.TargetEntityIndex, Effective(observerEntityIndex, source.EntityIndex, contact));
                    continue;
                }

                var previousContact = previous.Contact;
                var sourceWins = contact.InfoGathered > previousContact.InfoGathered ||
                    (contact.InfoGathered == previousContact.InfoGathered && contact.Visible && !previousContact.Visible);
                contacts[contact.TargetEntityIndex] = new AetheriaRuntimeEffectiveContact(
                    observerEntityIndex,
                    sourceWins ? source.EntityIndex : previous.PrimarySensorSourceEntityIndex,
                    new AetheriaRuntimeEntityContactCommit
                    {
                        TargetEntityIndex = contact.TargetEntityIndex,
                        InfoGathered = Math.Max(previousContact.InfoGathered, contact.InfoGathered),
                        Hostile = previousContact.Hostile || contact.Hostile,
                        Visible = previousContact.Visible || contact.Visible
                    });
            }

            return contacts.Values
                .OrderBy(contact => contact.Contact.TargetEntityIndex)
                .ToArray();
        }

        private static AetheriaRuntimeEffectiveContact Effective(
            int observerEntityIndex,
            int primarySensorSourceEntityIndex,
            AetheriaRuntimeEntityContactCommit contact) =>
            new AetheriaRuntimeEffectiveContact(
                observerEntityIndex,
                primarySensorSourceEntityIndex,
                new AetheriaRuntimeEntityContactCommit
                {
                    TargetEntityIndex = contact.TargetEntityIndex,
                    InfoGathered = contact.InfoGathered,
                    Hostile = contact.Hostile,
                    Visible = contact.Visible
                });

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
            return QueryGravityInfluences(zone, viewport.ToAetheriaXyRect());
        }

        public static AetheriaRuntimeGravityInfluenceBrush[] QueryGravityInfluences(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            CultRect viewport)
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
            return QueryGravityInfluences(zone, viewport.ToAetheriaXyRect(), brushes);
        }

        public static int QueryGravityInfluences(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            CultRect viewport,
            List<AetheriaRuntimeGravityInfluenceBrush> brushes)
        {
            if (brushes == null) throw new ArgumentNullException(nameof(brushes));
            brushes.Clear();
            if (zone == null)
                return 0;

            var orbitPositions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
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
                    ResolveWaveSpeed(body),
                    ResolveWaveFrequency(body)));
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

            var orbitPositions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
            var orbits = BuildOrbitMap(zone);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null || !TryResolveBodyCenter(body, orbitPositions, out var center))
                    continue;

                var orbitKey = body.OrbitKey ?? "";
                var parentOrbitKey = orbits.TryGetValue(orbitKey, out var orbit) ? orbit.ParentOrbitKey ?? "" : "";
                var parentCenter = orbitPositions.TryGetValue(parentOrbitKey, out var parent)
                    ? parent
                    : new AetheriaRuntimeOrbitPosition(0, 0);
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

        public static AetheriaRuntimeDaemonBodyView[] QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            var views = new List<AetheriaRuntimeDaemonBodyView>();
            QueryBodyViews(zone, views);
            return views.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonBodyView>() : views.ToArray();
        }

        public static AetheriaRuntimeDaemonBodyView[] QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            AetheriaRuntimeXzRect viewport)
        {
            return QueryBodyViews(zone, viewport.ToAetheriaXyRect());
        }

        public static AetheriaRuntimeDaemonBodyView[] QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            CultRect viewport)
        {
            var views = new List<AetheriaRuntimeDaemonBodyView>();
            QueryBodyViews(zone, viewport, views);
            return views.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonBodyView>() : views.ToArray();
        }

        public static int QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            List<AetheriaRuntimeDaemonBodyView> views)
        {
            return QueryBodyViews(zone, null, views);
        }

        public static int QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            AetheriaRuntimeXzRect viewport,
            List<AetheriaRuntimeDaemonBodyView> views)
        {
            return QueryBodyViews(zone, viewport.ToAetheriaXyRect(), views);
        }

        public static int QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            CultRect viewport,
            List<AetheriaRuntimeDaemonBodyView> views)
        {
            return QueryBodyViews(zone, (CultRect?)viewport, views);
        }

        private static int QueryBodyViews(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            CultRect? viewport,
            List<AetheriaRuntimeDaemonBodyView> views)
        {
            if (views == null) throw new ArgumentNullException(nameof(views));
            views.Clear();
            if (zone == null)
                return 0;

            var orbitPositions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
            var orbits = BuildOrbitMap(zone);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null || !TryResolveBodyCenter(body, orbitPositions, out var center))
                    continue;

                if (viewport.HasValue &&
                    !IntersectsCircle(
                        viewport.Value,
                        center.x,
                        center.z,
                        Math.Max(ResolveGravityRadius(body), ResolveWaveRadius(body))))
                {
                    continue;
                }

                var orbitKey = body.OrbitKey ?? "";
                var parentOrbitKey = orbits.TryGetValue(orbitKey, out var orbit) ? orbit.ParentOrbitKey ?? "" : "";
                var parentCenter = orbitPositions.TryGetValue(parentOrbitKey, out var parent)
                    ? parent
                    : new AetheriaRuntimeOrbitPosition(0, 0);
                var pose = new AetheriaRuntimeDaemonBodyPose(
                    body.BodyKey,
                    orbitKey,
                    parentOrbitKey,
                    body.Kind,
                    center.x,
                    center.z,
                    parentCenter.x,
                    parentCenter.z,
                    ResolveWaveSpeed(body));
                views.Add(new AetheriaRuntimeDaemonBodyView(
                    body,
                    pose,
                    string.Equals(body.Kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase)));
            }

            return views.Count;
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

            var orbitPositions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
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

            var orbitPositions = AetheriaRuntimeOrbitQueries.BuildPositions(zone);
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

        public static int[] QueryPresentationEntityIndices(
            AetheriaRuntimeRunCheckpointCommit? run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect viewport)
        {
            return QueryPresentationEntityIndices(run, zone, observerEntityIndex, minimumInfoGathered, viewport.ToAetheriaXyRect());
        }

        public static int[] QueryPresentationEntityIndices(
            AetheriaRuntimeRunCheckpointCommit? run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            CultRect viewport)
        {
            var currentEntityIndex = TryParseEntityIndex(run?.CurrentEntityKey);
            return QueryPresentationEntityIndices(
                currentEntityIndex,
                zone,
                observerEntityIndex,
                minimumInfoGathered,
                viewport);
        }

        public static int QueryPresentationEntityIndices(
            AetheriaRuntimeRunCheckpointCommit? run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect viewport,
            List<int> entityIndices)
        {
            return QueryPresentationEntityIndices(run, zone, observerEntityIndex, minimumInfoGathered, viewport.ToAetheriaXyRect(), entityIndices);
        }

        public static int QueryPresentationEntityIndices(
            AetheriaRuntimeRunCheckpointCommit? run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            CultRect viewport,
            List<int> entityIndices)
        {
            var currentEntityIndex = TryParseEntityIndex(run?.CurrentEntityKey);
            return QueryPresentationEntityIndices(
                currentEntityIndex,
                zone,
                observerEntityIndex,
                minimumInfoGathered,
                viewport,
                entityIndices);
        }

        public static int[] QueryPresentationEntityIndices(
            string? currentEntityKey,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect viewport)
        {
            return QueryPresentationEntityIndices(currentEntityKey, zone, observerEntityIndex, minimumInfoGathered, viewport.ToAetheriaXyRect());
        }

        public static int[] QueryPresentationEntityIndices(
            string? currentEntityKey,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            CultRect viewport)
        {
            var entityIndices = new List<int>();
            var currentEntityIndex = TryParseEntityIndex(currentEntityKey);
            QueryPresentationEntityIndices(
                currentEntityIndex,
                zone,
                observerEntityIndex,
                minimumInfoGathered,
                viewport,
                entityIndices);
            return entityIndices.Count == 0 ? Array.Empty<int>() : entityIndices.ToArray();
        }

        private static int[] QueryPresentationEntityIndices(
            int currentEntityIndex,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            CultRect viewport)
        {
            var entityIndices = new List<int>();
            QueryPresentationEntityIndices(
                currentEntityIndex,
                zone,
                observerEntityIndex,
                minimumInfoGathered,
                viewport,
                entityIndices);
            return entityIndices.Count == 0 ? Array.Empty<int>() : entityIndices.ToArray();
        }

        public static int QueryPresentationEntityIndices(
            string? currentEntityKey,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect viewport,
            List<int> entityIndices)
        {
            return QueryPresentationEntityIndices(currentEntityKey, zone, observerEntityIndex, minimumInfoGathered, viewport.ToAetheriaXyRect(), entityIndices);
        }

        public static int QueryPresentationEntityIndices(
            string? currentEntityKey,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            CultRect viewport,
            List<int> entityIndices)
        {
            var currentEntityIndex = TryParseEntityIndex(currentEntityKey);
            return QueryPresentationEntityIndices(
                currentEntityIndex,
                zone,
                observerEntityIndex,
                minimumInfoGathered,
                viewport,
                entityIndices);
        }

        private static int QueryPresentationEntityIndices(
            int currentEntityIndex,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            CultRect viewport,
            List<int> entityIndices)
        {
            if (entityIndices == null) throw new ArgumentNullException(nameof(entityIndices));
            entityIndices.Clear();
            if (zone == null)
                return 0;

            var selected = new HashSet<int>();
            if (currentEntityIndex >= 0)
                selected.Add(currentEntityIndex);

            var entities = BuildEntityMap(zone);
            if (observerEntityIndex >= 0)
            {
                if (entities.TryGetValue(observerEntityIndex, out var observer))
                {
                    selected.Add(observer.EntityIndex);
                    foreach (var contact in observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    {
                        if (contact != null &&
                            contact.Visible &&
                            contact.InfoGathered > minimumInfoGathered &&
                            entities.ContainsKey(contact.TargetEntityIndex))
                        {
                            selected.Add(contact.TargetEntityIndex);
                        }
                    }
                }
            }

            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity != null &&
                    entity.EntityIndex >= 0 &&
                    ContainsPoint(viewport, entity.PositionX, entity.PositionZ))
                {
                    selected.Add(entity.EntityIndex);
                }
            }

            foreach (var entityIndex in selected.OrderBy(index => index))
                entityIndices.Add(entityIndex);

            return entityIndices.Count;
        }

        public static int[] QueryObjectsViewportEntityIndices(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            CultRect viewport)
        {
            var entityIndices = new List<int>();
            QueryObjectsViewportEntityIndices(zone, controlledEntityIndices, minimumInfoGathered, viewport, entityIndices);
            return entityIndices.Count == 0 ? Array.Empty<int>() : entityIndices.ToArray();
        }

        public static int QueryObjectsViewportEntityIndices(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            CultRect viewport,
            List<int> entityIndices)
        {
            if (entityIndices == null) throw new ArgumentNullException(nameof(entityIndices));
            entityIndices.Clear();
            if (zone == null || controlledEntityIndices == null || controlledEntityIndices.Count == 0)
                return 0;

            var entities = BuildEntityMap(zone);
            var visible = new HashSet<int>();
            foreach (var observerEntityIndex in controlledEntityIndices)
            {
                if (observerEntityIndex < 0 ||
                    !entities.TryGetValue(observerEntityIndex, out var observer))
                {
                    continue;
                }

                visible.Add(observer.EntityIndex);
                foreach (var contact in observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                {
                    if (contact != null &&
                        contact.Visible &&
                        contact.InfoGathered > minimumInfoGathered &&
                        entities.ContainsKey(contact.TargetEntityIndex))
                    {
                        visible.Add(contact.TargetEntityIndex);
                    }
                }
            }

            foreach (var entityIndex in visible.OrderBy(index => index))
            {
                if (entities.TryGetValue(entityIndex, out var entity) &&
                    viewport.Contains(new float2((float)entity.PositionX, (float)entity.PositionZ)))
                {
                    entityIndices.Add(entityIndex);
                }
            }

            return entityIndices.Count;
        }

        public static int[] QueryObjectsViewportEntityIndices(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect unityViewport)
        {
            return QueryObjectsViewportEntityIndices(
                zone,
                controlledEntityIndices,
                minimumInfoGathered,
                ToRuntimeRect(unityViewport));
        }

        public static int QueryObjectsViewportEntityIndices(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect unityViewport,
            List<int> entityIndices)
        {
            return QueryObjectsViewportEntityIndices(
                zone,
                controlledEntityIndices,
                minimumInfoGathered,
                ToRuntimeRect(unityViewport),
                entityIndices);
        }

        public static AetheriaRuntimeDaemonObjectViewportEntity[] QueryObjectsViewport(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            CultRect viewport)
        {
            var entities = new List<AetheriaRuntimeDaemonObjectViewportEntity>();
            QueryObjectsViewport(zone, controlledEntityIndices, minimumInfoGathered, viewport, entities);
            return entities.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonObjectViewportEntity>() : entities.ToArray();
        }

        public static int QueryObjectsViewport(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            CultRect viewport,
            List<AetheriaRuntimeDaemonObjectViewportEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            entities.Clear();

            var entityIndices = new List<int>();
            QueryObjectsViewportEntityIndices(zone, controlledEntityIndices, minimumInfoGathered, viewport, entityIndices);
            if (zone == null || entityIndices.Count == 0)
                return 0;

            var entityMap = BuildEntityMap(zone);
            var controlled = new HashSet<int>(controlledEntityIndices ?? Array.Empty<int>());
            foreach (var entityIndex in entityIndices)
            {
                if (entityMap.TryGetValue(entityIndex, out var entity))
                    entities.Add(BuildObjectViewportEntity(entity, controlled.Contains(entityIndex)));
            }

            return entities.Count;
        }

        public static AetheriaRuntimeDaemonObjectViewportEntity[] QueryObjectsViewport(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect unityViewport)
        {
            return QueryObjectsViewport(
                zone,
                controlledEntityIndices,
                minimumInfoGathered,
                ToRuntimeRect(unityViewport));
        }

        public static int QueryObjectsViewport(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<int>? controlledEntityIndices,
            double minimumInfoGathered,
            AetheriaRuntimeXzRect unityViewport,
            List<AetheriaRuntimeDaemonObjectViewportEntity> entities)
        {
            return QueryObjectsViewport(
                zone,
                controlledEntityIndices,
                minimumInfoGathered,
                ToRuntimeRect(unityViewport),
                entities);
        }

        public static bool TryQueryEntityContact(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            int targetEntityIndex,
            out AetheriaRuntimeDaemonEntityContact entityContact)
        {
            entityContact = default;
            if (zone == null || observerEntityIndex < 0 || targetEntityIndex < 0)
                return false;

            var entities = BuildEntityMap(zone);
            if (!entities.TryGetValue(observerEntityIndex, out var observer) ||
                !entities.TryGetValue(targetEntityIndex, out var target))
            {
                return false;
            }

            foreach (var contact in observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            {
                if (contact == null || contact.TargetEntityIndex != targetEntityIndex)
                    continue;

                entityContact = BuildEntityContact(observer, target, contact);
                return true;
            }

            return false;
        }

        public static AetheriaRuntimeDaemonEntityContact[] QueryEntityContacts(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            bool visibleOnly)
        {
            var contacts = new List<AetheriaRuntimeDaemonEntityContact>();
            QueryEntityContacts(zone, observerEntityIndex, minimumInfoGathered, visibleOnly, contacts);
            return contacts.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonEntityContact>() : contacts.ToArray();
        }

        public static int QueryEntityContacts(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            double minimumInfoGathered,
            bool visibleOnly,
            List<AetheriaRuntimeDaemonEntityContact> contacts)
        {
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
            contacts.Clear();
            if (zone == null || observerEntityIndex < 0)
                return 0;

            var entities = BuildEntityMap(zone);
            if (!entities.TryGetValue(observerEntityIndex, out var observer))
                return 0;

            foreach (var contact in observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            {
                if (contact == null ||
                    contact.InfoGathered <= minimumInfoGathered ||
                    visibleOnly && !contact.Visible ||
                    !entities.TryGetValue(contact.TargetEntityIndex, out var target))
                {
                    continue;
                }

                contacts.Add(BuildEntityContact(observer, target, contact));
            }

            return contacts.Count;
        }

        public static bool TryQueryEntityTarget(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            int observerEntityIndex,
            out AetheriaRuntimeDaemonEntityTarget entityTarget)
        {
            entityTarget = default;
            if (zone == null || observerEntityIndex < 0)
                return false;

            var entities = BuildEntityMap(zone);
            if (!entities.TryGetValue(observerEntityIndex, out var observer) ||
                observer.TargetEntityIndex < 0 ||
                !entities.TryGetValue(observer.TargetEntityIndex, out var target))
            {
                return false;
            }

            entityTarget = BuildEntityTarget(observer, target);
            return true;
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

        public static int QueryWormholeExits(
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<AetheriaRuntimeZoneRenderAdjacentZone>? adjacentZones,
            double zoneRadius,
            double wormholeDistanceRatio,
            List<AetheriaRuntimeDaemonWormholeExit> exits)
        {
            if (exits == null) throw new ArgumentNullException(nameof(exits));
            exits.Clear();
            if (zone == null || adjacentZones == null || adjacentZones.Count == 0)
                return 0;

            var adjacentByIndex = adjacentZones
                .Where(candidate => candidate != null && candidate.ZoneIndex >= 0)
                .ToDictionary(candidate => candidate.ZoneIndex);
            var distance = Math.Max(0, zoneRadius) * Math.Max(0, wormholeDistanceRatio);
            foreach (var targetZoneIndex in zone.AdjacentZoneIndices ?? Array.Empty<int>())
            {
                if (!adjacentByIndex.TryGetValue(targetZoneIndex, out var target))
                    continue;

                var deltaX = target.X - zone.PositionX;
                var deltaZ = target.Y - zone.PositionY;
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
                         new CultRect((float)positionX, (float)positionZ, (float)positionX, (float)positionZ)))
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
                        brush.WaveFrequency,
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

        private static AetheriaRuntimeDaemonEntityContact BuildEntityContact(
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEntitySnapshotCommit target,
            AetheriaRuntimeEntityContactCommit contact)
        {
            var deltaX = target.PositionX - observer.PositionX;
            var deltaZ = target.PositionZ - observer.PositionZ;
            var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            return new AetheriaRuntimeDaemonEntityContact(
                observer.EntityIndex,
                target.EntityIndex,
                target.PositionX,
                target.PositionZ,
                deltaX,
                deltaZ,
                distance,
                contact.InfoGathered,
                contact.Hostile,
                contact.Visible);
        }

        private static AetheriaRuntimeDaemonObjectViewportEntity BuildObjectViewportEntity(
            AetheriaRuntimeEntitySnapshotCommit entity,
            bool controlled)
        {
            var position = new double3(entity.PositionX, entity.PositionY, entity.PositionZ);
            var xy = new double2(entity.PositionX, entity.PositionZ);
            var direction = new double2(entity.DirectionX, entity.DirectionY);
            var velocity = new double2(entity.VelocityX, entity.VelocityY);
            return new AetheriaRuntimeDaemonObjectViewportEntity(
                entity.EntityIndex,
                entity.Name,
                entity.Name,
                position,
                xy,
                direction,
                velocity,
                controlled);
        }

        private static AetheriaRuntimeDaemonEntityTarget BuildEntityTarget(
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEntitySnapshotCommit target)
        {
            var deltaX = target.PositionX - observer.PositionX;
            var deltaZ = target.PositionZ - observer.PositionZ;
            var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            return new AetheriaRuntimeDaemonEntityTarget(
                observer.EntityIndex,
                target.EntityIndex,
                target.PositionX,
                target.PositionZ,
                deltaX,
                deltaZ,
                distance);
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

        private static bool IntersectsCircle(CultRect viewport, double centerX, double centerY, double radius)
        {
            if (radius <= 0)
                return false;

            var nearestX = Clamp(centerX, viewport.min.x, viewport.max.x);
            var nearestZ = Clamp(centerY, viewport.min.y, viewport.max.y);
            var dx = centerX - nearestX;
            var dz = centerY - nearestZ;
            return dx * dx + dz * dz <= radius * radius;
        }

        private static bool IntersectsCircle(AetheriaRuntimeXzRect rect, double centerX, double centerZ, double radius)
        {
            return IntersectsCircle(rect.ToAetheriaXyRect(), centerX, centerZ, radius);
        }

        private static bool ContainsPoint(AetheriaRuntimeXzRect rect, double x, double z)
        {
            return ContainsPoint(rect.ToAetheriaXyRect(), x, z);
        }

        private static bool ContainsPoint(CultRect viewport, double x, double y)
        {
            return viewport.Contains(new float2((float)x, (float)y));
        }

        private static CultRect ToRuntimeRect(AetheriaRuntimeXzRect unityViewport)
        {
            return unityViewport.ToAetheriaXyRect();
        }

        private static int TryParseEntityIndex(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return -1;

            var marker = ".entity.";
            var markerIndex = key.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return -1;

            var start = markerIndex + marker.Length;
            var end = start;
            while (end < key.Length && char.IsDigit(key[end]))
                end++;

            return int.TryParse(key.Substring(start, end - start), out var value)
                ? value
                : -1;
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
            Dictionary<string, AetheriaRuntimeOrbitPosition> orbitPositions,
            out AetheriaRuntimeOrbitPosition center)
        {
            return AetheriaRuntimeOrbitQueries.TryResolveBodyPosition(body, orbitPositions, out center);
        }

        private static bool TryResolveAsteroidBeltCenter(
            AetheriaRuntimeBodySnapshotCommit body,
            Dictionary<string, AetheriaRuntimeOrbitPosition> orbitPositions,
            Dictionary<string, AetheriaRuntimeOrbitSnapshotCommit> orbits,
            out AetheriaRuntimeOrbitPosition center)
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

        private static double ResolveWaveFrequency(AetheriaRuntimeBodySnapshotCommit body)
        {
            return body != null && IsFinite(body.GravityWaveFrequency) && body.GravityWaveFrequency > 0
                ? body.GravityWaveFrequency
                : 1.0;
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

    }
}
