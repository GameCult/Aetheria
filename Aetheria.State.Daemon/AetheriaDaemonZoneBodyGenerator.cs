using Aetheria.State.Documents;

public sealed record AetheriaDaemonGeneratedZonePlan(
    uint GenerationSeed,
    uint PostBodyRandomState,
    double Radius,
    double Mass,
    double HierarchyMass,
    int EmptyOrbitCount,
    AetheriaOrbitSnapshot[] Orbits,
    AetheriaBodySnapshot[] Bodies)
{
    public double GravityTerrainDepth { get; init; } = 64;
    public double GravityTerrainDepthExponent { get; init; } = 2;
    public double GravityTerrainBoundaryFog { get; init; }
}

public sealed record AetheriaDaemonTutorialWorldPlan(
    AetheriaDaemonTutorialTopology Topology,
    IReadOnlyDictionary<int, AetheriaDaemonGeneratedZonePlan> Zones);

public static class AetheriaDaemonTutorialWorldGenerator
{
    public static AetheriaDaemonTutorialWorldPlan Generate(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        uint seed,
        AetheriaDaemonTutorialTopologySettings? settings = null)
    {
        var topology = AetheriaDaemonTutorialTopologyGenerator.GenerateFossil(factions, seed, settings);
        var zones = topology.Zones.ToDictionary(
            zone => zone.ZoneIndex,
            zone => AetheriaDaemonZoneBodyGenerator.Generate(
                seed,
                zone,
                AetheriaDaemonTutorialTopologyGenerator.TutorialCloudDensity(
                    zone.X,
                    zone.Y,
                    topology.NoisePosition)));
        return new AetheriaDaemonTutorialWorldPlan(topology, zones);
    }
}

/// <summary>
/// Daemon-owned port of the fossil ZoneGenerator celestial hierarchy. Entity
/// population consumes this plan later; it cannot regenerate bodies or orbits.
/// </summary>
public static class AetheriaDaemonZoneBodyGenerator
{
    private const int MaximumPlacementSamples = 32;

    public static AetheriaDaemonGeneratedZonePlan Generate(
        uint galaxySeed,
        AetheriaDaemonTutorialZoneTopology zone,
        float cloudDensity)
    {
        var density = Saturate(cloudDensity / 2f);
        var radius = ExponentialLerp(density, 1.5f, 1000, 10000);
        var mass = ExponentialLerp(density, 1.5f, 10000, 500000);
        var targetSubzoneCount = ExponentialLerp(density, 1.5f, 0, 8);
        var zoneSeed = StableZoneSeed(galaxySeed, zone);
        var random = new CultMath.Random(zoneSeed);
        var planets = new List<PlanetNode>();

        if (targetSubzoneCount > 1)
        {
            var boundary = new Circle(new CultMath.float2(0, 0), radius * 0.9f);
            var occupied = new List<Circle>();
            var start = random.NextFloat(radius * 0.25f, radius * 0.5f) * random.NextFloat2Direction();
            occupied.Add(new Circle(start, -boundary.DistanceTo(start)));

            var samples = 0;
            while (occupied.Count < targetSubzoneCount && samples < MaximumPlacementSamples)
            {
                samples = 0;
                for (var i = 0; i < MaximumPlacementSamples; i++)
                {
                    var point = random.NextFloat2(-radius, radius);
                    var tangentRadius = MathF.Min(
                        -boundary.DistanceTo(point),
                        occupied.Min(circle => circle.DistanceTo(point)));
                    if (tangentRadius > 0)
                    {
                        occupied.Add(new Circle(point, tangentRadius));
                        break;
                    }
                    samples++;
                }
            }

            var totalArea = occupied.Sum(circle => circle.Area);
            foreach (var circle in occupied)
                planets.AddRange(GenerateSystem(
                    ref random,
                    circle.Area / totalArea * mass,
                    circle.Radius,
                    circle.Center));
        }
        else
        {
            planets.AddRange(GenerateSystem(ref random, mass, radius, new CultMath.float2(0, 0)));
        }

        var orbitKeys = planets.Select((_, index) => $"tutorial.zone.{zone.ZoneIndex}.orbit.{index}").ToArray();
        var nodeIndices = planets.Select((node, index) => (node, index)).ToDictionary(value => value.node, value => value.index);
        var orbits = planets.Select((planet, index) => new AetheriaOrbitSnapshot
        {
            OrbitKey = orbitKeys[index],
            ParentOrbitKey = planet.Parent == null ? "" : orbitKeys[nodeIndices[planet.Parent]],
            FixedPosition = new AetheriaVector2 { X = planet.FixedPosition.x, Y = planet.FixedPosition.y },
            Distance = planet.Distance,
            Phase = planet.Phase,
            Period = planet.Distance
        }).ToArray();

        var bodies = new List<AetheriaBodySnapshot>();
        for (var nodeIndex = 0; nodeIndex < planets.Count; nodeIndex++)
        {
            var planet = planets[nodeIndex];
            if (planet.Empty)
                continue;
            bodies.Add(ProjectBody(zone.ZoneIndex, bodies.Count, planet, orbits[nodeIndex], ref random));
        }

        return new AetheriaDaemonGeneratedZonePlan(
            zoneSeed,
            random.state,
            radius,
            mass,
            planets.Sum(planet => (double)planet.Mass),
            planets.Count(planet => planet.Empty),
            orbits,
            bodies.ToArray());
    }

    private static PlanetNode[] GenerateSystem(
        ref CultMath.Random random,
        float mass,
        float radius,
        CultMath.float2 fixedPosition)
    {
        var root = new PlanetNode
        {
            Mass = mass,
            ChildDistanceMaximum = radius * 0.75f,
            ChildDistanceMinimum = PlanetSafetyRadius(mass),
            FixedPosition = fixedPosition
        };
        var rosette = random.NextFloat() < 0.1f;
        if (rosette)
        {
            root.ExpandRosette(ref random, (int)(random.NextFloat(1, 5) + random.NextFloat(1, 5)));
            root.ExpandSolar(
                ref random,
                (int)(random.NextFloat(1, 3) * random.NextFloat(1, 2)),
                0.6f, 0.8f, 1.25f, 1.75f, 1, 0.1f);
            var averageChildMass = root.Children.Sum(value => value.Mass) / root.Children.Count;
            foreach (var planet in root.Children.Where(value => value.Mass > 1000).ToArray())
            {
                var scale = planet.Mass / averageChildMass;
                planet.ExpandSolar(
                    ref random,
                    (int)(random.NextFloat(1, 3 * scale) + random.NextFloat(1, 3 * scale)),
                    0.75f, 2.5f, 1 + scale * 0.25f, 1.05f + scale * 0.5f,
                    random.NextFloat() * random.NextFloat() * 10 + 1,
                    0.5f);
            }
        }
        else
        {
            root.ExpandSolar(
                ref random,
                random.NextInt(5, 15),
                0.75f, 2.5f, 1.1f, 1.25f,
                random.NextFloat() * random.NextFloat() * 10 + 1,
                0.25f);
        }

        var expanded = new HashSet<PlanetNode>();
        var binaries = new HashSet<PlanetNode>();
        for (var pass = 0; pass < 5; pass++)
        {
            var candidates = root.All()
                .Where(planet => planet != root &&
                    (!rosette || planet.Parent != root) &&
                    planet.Mass > 100 &&
                    !expanded.Contains(planet))
                .ToArray();
            foreach (var planet in candidates)
            {
                if (random.NextFloat() >= 0.25f)
                    continue;
                if (random.NextFloat() < 0.1f)
                {
                    planet.ExpandRosette(ref random, 2);
                    binaries.UnionWith(planet.Children);
                }
                else
                {
                    planet.ExpandSolar(
                        ref random,
                        planet.Mass < 1000 ? random.NextInt(1, 3) : random.NextInt(2, 6),
                        0.75f, 1.5f, 1.05f, 1.25f, 1, 0.15f);
                }
                expanded.Add(planet);
            }
        }

        var beltCandidates = root.All()
            .Where(planet => planet != root &&
                (!rosette || planet.Parent != root) &&
                planet.Mass < 500 &&
                !binaries.Contains(planet) &&
                planet.Children.Count == 0)
            .Reverse()
            .ToArray();
        foreach (var planet in beltCandidates)
        {
            if (random.NextFloat() < 0.25f && planet.Parent!.Children.All(sibling => !sibling.Belt))
                planet.Belt = true;
        }

        var all = root.All().ToArray();
        var totalMass = all.Sum(planet => planet.Mass);
        foreach (var planet in all)
            planet.Mass = planet.Mass / totalMass * mass;
        return all;
    }

    private static AetheriaBodySnapshot ProjectBody(
        int zoneIndex,
        int bodyIndex,
        PlanetNode planet,
        AetheriaOrbitSnapshot orbit,
        ref CultMath.Random random)
    {
        var kind = planet.Belt ? "asteroid_belt" :
            planet.Mass > 5000 ? "sun" :
            planet.Mass > 1000 ? "gas_giant" :
            planet.Mass > 100 ? "planet" : "planetoid";
        var body = new AetheriaBodySnapshot
        {
            BodyKey = $"tutorial.zone.{zoneIndex}.body.{bodyIndex}",
            Kind = kind,
            Name = $"Z{zoneIndex:D2}-{bodyIndex:D3}",
            OrbitKey = orbit.OrbitKey,
            Mass = planet.Mass,
            Resources = [],
            BodyRadiusMultiplier = 1,
            GravityRadiusMultiplier = 1,
            GravityDepthMultiplier = 1,
            GravityDepthExponent = 2,
            GravityInfluenceCenterX = double.NaN,
            GravityInfluenceCenterZ = double.NaN,
            GravityInfluenceRadius = 500 * Math.Pow(planet.Mass, 0.25),
            GravityWellDepth = 30 * Math.Pow(planet.Mass, 0.175),
            GravityWaveRadius = 500 * Math.Pow(planet.Mass, 0.25),
            GravityWaveDepth = 0.5 * Math.Pow(planet.Mass, 0.3),
            GravityWaveSpeed = 0.025,
            Asteroids = [],
            GasGiantVisual = new AetheriaGasGiantVisualState(),
            SunVisual = new AetheriaSunVisualState()
        };

        if (kind == "asteroid_belt")
        {
            var count = Math.Max(0, (int)(MathF.Pow(planet.Mass * planet.Distance, 0.5f) + 11));
            var asteroids = new AetheriaAsteroidSnapshot[count];
            for (var asteroidIndex = 0; asteroidIndex < count; asteroidIndex++)
            {
                asteroids[asteroidIndex] = new AetheriaAsteroidSnapshot
                {
                    Distance = planet.Distance + random.NextFloat() * (random.NextFloat() - 0.5f) *
                        (5 * MathF.Pow(planet.Distance, 0.666f) + 50),
                    Phase = random.NextFloat(),
                    Size = random.NextFloat(),
                    RotationSpeed = ExponentialLerp(random.NextFloat(), 2, 0.1f, 0.5f),
                    MiningAccumulators = []
                };
            }
            body.Asteroids = asteroids;
        }
        else if (kind is "gas_giant" or "sun")
        {
            var colors = new List<AetheriaColor>();
            if (kind == "sun")
            {
                var primary = random.NextFloat();
                var secondary = Fraction(primary + 1 + 0.33f * (random.NextFloat() > 0.5f ? 1 : -1));
                colors.Add(Color(Hsv(primary, 0.85f, 0.5f), 0));
                colors.Add(Color(Hsv(secondary, 0.85f, 1), 1));
                var fog = Hsv(primary, 0.55f, 1);
                var light = Hsv(primary, 0.5f, 1);
                body.SunVisual = new AetheriaSunVisualState
                {
                    FogTintColor = Vector(fog),
                    LightColor = Vector(light),
                    LightRadiusMultiplier = 1
                };
            }
            else
            {
                var primary = random.NextFloat();
                var right = Fraction(primary + 0.25f);
                var left = Fraction(primary + 0.75f);
                var bandCount = (int)(ExponentialLerp(random.NextFloat(), 2, 5, 8) + 0.5f);
                for (var band = 0; band < bandCount; band++)
                {
                    var time = (float)band / (bandCount - 1);
                    var hue = random.NextFloat() > 0.25f ? primary : random.NextFloat() > 0.5f ? right : left;
                    colors.Add(Color(Hsv(
                        hue,
                        ExponentialLerp(random.NextFloat(), 0.5f, 0, 0.8f),
                        ExponentialLerp(random.NextFloat(), 0.5f, 0, 0.8f)), time));
                }
            }
            body.GasGiantVisual = new AetheriaGasGiantVisualState
            {
                FirstOffsetDomainRotationSpeed = kind == "sun" ? 5 : 0,
                FirstOffsetRotationSpeed = 5,
                SecondOffsetDomainRotationSpeed = -25,
                SecondOffsetRotationSpeed = 10,
                AlbedoRotationSpeed = -3,
                Colors = colors.ToArray(),
                MaterialOverrides = []
            };
        }
        return body;
    }

    private static uint StableZoneSeed(uint galaxySeed, AetheriaDaemonTutorialZoneTopology zone)
    {
        var hash = 2166136261u ^ galaxySeed;
        foreach (var value in System.Text.Encoding.UTF8.GetBytes(zone.Name ?? ""))
            hash = (hash ^ value) * 16777619u;
        hash = (hash ^ unchecked((uint)BitConverter.SingleToInt32Bits(zone.X))) * 16777619u;
        hash = (hash ^ unchecked((uint)BitConverter.SingleToInt32Bits(zone.Y))) * 16777619u;
        hash = (hash ^ unchecked((uint)zone.ZoneIndex)) * 16777619u;
        return hash == 0 ? 0x6E62_4EB7u : hash;
    }

    private static float PlanetSafetyRadius(float mass) => 2.5f * MathF.Pow(mass, 0.25f);
    private static float ExponentialLerp(float value, float exponent, float minimum, float maximum) =>
        minimum + MathF.Pow(Saturate(value), exponent) * (maximum - minimum);
    private static float Saturate(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    private static float Fraction(float value) => value - MathF.Floor(value);

    private static CultMath.float3 Hsv(float hue, float saturation, float value)
    {
        hue = Fraction(hue);
        var sector = hue * 6;
        var index = (int)MathF.Floor(sector);
        var fraction = sector - index;
        var p = value * (1 - saturation);
        var q = value * (1 - saturation * fraction);
        var t = value * (1 - saturation * (1 - fraction));
        return index switch
        {
            0 => new CultMath.float3(value, t, p),
            1 => new CultMath.float3(q, value, p),
            2 => new CultMath.float3(p, value, t),
            3 => new CultMath.float3(p, q, value),
            4 => new CultMath.float3(t, p, value),
            _ => new CultMath.float3(value, p, q)
        };
    }

    private static AetheriaColor Color(CultMath.float3 value, float time) => new()
    {
        X = value.x,
        Y = value.y,
        Z = value.z,
        W = time
    };

    private static AetheriaVector3 Vector(CultMath.float3 value) => new()
    {
        X = value.x,
        Y = value.y,
        Z = value.z
    };

    private sealed record Circle(CultMath.float2 Center, float Radius)
    {
        public float Area => MathF.PI * Radius * Radius;
        public float DistanceTo(CultMath.float2 point) => CultMath.math.length(point - Center) - Radius;
    }

    private sealed class PlanetNode
    {
        public float Distance;
        public float Phase;
        public float Mass;
        public float ChildDistanceMinimum;
        public float ChildDistanceMaximum;
        public bool Empty;
        public bool Belt;
        public List<PlanetNode> Children { get; } = [];
        public PlanetNode? Parent;
        public CultMath.float2 FixedPosition;

        public IEnumerable<PlanetNode> All() => new[] { this }.Concat(Children.SelectMany(child => child.All()));

        public void ExpandRosette(ref CultMath.Random random, int vertices)
        {
            Empty = true;
            var sharedMass = Mass / vertices * 2;
            var proportion = vertices % 2 == 0 ? random.NextFloat(0.5f, 0.95f) : 0.5f;
            var distance = (ChildDistanceMinimum + ChildDistanceMaximum) / 2;
            var p0 = new CultMath.float2(0, distance);
            var angle = 1f / vertices * MathF.PI * 2;
            var p1 = new CultMath.float2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            var neighborDistance = CultMath.math.length(p0 - p1);
            var p0ChildDistance = (neighborDistance * proportion - PlanetSafetyRadius(sharedMass * (1 - proportion))) * 0.75f;
            var p1ChildDistance = (neighborDistance * (1 - proportion) - PlanetSafetyRadius(sharedMass * proportion)) * 0.75f;
            for (var index = 0; index < vertices; index++)
            {
                var child = new PlanetNode
                {
                    Parent = this,
                    Mass = sharedMass * (index % 2 == 0 ? proportion : 1 - proportion),
                    Distance = distance,
                    Phase = (float)index / vertices,
                    ChildDistanceMaximum = index % 2 == 0 ? p0ChildDistance : p1ChildDistance
                };
                child.ChildDistanceMinimum = PlanetSafetyRadius(child.Mass) * 2;
                Children.Add(child);
            }
            ChildDistanceMinimum = distance + p0ChildDistance;
        }

        public void ExpandSolar(
            ref CultMath.Random random,
            int count,
            float massMultiplierMinimum,
            float massMultiplierMaximum,
            float distanceMultiplierMinimum,
            float distanceMultiplierMaximum,
            float jupiterJump,
            float massFraction)
        {
            if (count == 0 || ChildDistanceMaximum < ChildDistanceMinimum)
                return;
            var masses = new float[count];
            var distances = new float[count];
            float massTotal = distances[0] = masses[0] = 1;
            for (var index = 1; index < count; index++)
            {
                massTotal += masses[index] = masses[index - 1] *
                    random.NextFloat(massMultiplierMinimum, massMultiplierMaximum) *
                    (count / 2 == index ? jupiterJump : 1);
                distances[index] = distances[index - 1] *
                    random.NextFloat(distanceMultiplierMinimum, distanceMultiplierMaximum);
            }
            for (var index = 0; index < count; index++)
                masses[index] *= random.NextFloat(0.1f, 1);
            for (var index = 0; index < count; index++)
                masses[index] = masses[index] / massTotal * Mass * massFraction;
            if (count > 1)
            {
                var original = distances.ToArray();
                for (var index = 0; index < count; index++)
                    distances[index] = ChildDistanceMinimum +
                        (ChildDistanceMaximum - ChildDistanceMinimum) *
                        ((original[index] - original[0]) / (original[^1] - original[0])) +
                        PlanetSafetyRadius(masses[index]);
            }
            for (var index = 0; index < count; index++)
            {
                if (masses[index] <= 1)
                    continue;
                var child = new PlanetNode
                {
                    Parent = this,
                    Mass = masses[index],
                    Distance = distances[index],
                    Phase = random.NextFloat()
                };
                child.ChildDistanceMinimum = PlanetSafetyRadius(child.Mass) * 2;
                child.ChildDistanceMaximum = MathF.Min(
                    index == 0 ? child.Distance - ChildDistanceMinimum : child.Distance - distances[index - 1],
                    index < count - 1 ? distances[index + 1] - child.Distance : float.PositiveInfinity);
                if (float.IsNaN(child.Distance))
                    throw new NotFiniteNumberException("Planet created with NaN distance.");
                if (child.ChildDistanceMaximum > child.ChildDistanceMinimum)
                    Children.Add(child);
            }
        }
    }
}
