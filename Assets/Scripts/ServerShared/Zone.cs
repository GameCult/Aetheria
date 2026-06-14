/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using Random = Unity.Mathematics.Random;

public class Zone
{
    public Action<string> Log;
    public ReactiveCollection<Entity> Entities = new ReactiveCollection<Entity>();
    public Dictionary<Guid, Planet> PlanetInstances = new Dictionary<Guid, Planet>();

    public Dictionary<Guid, Orbit> Orbits = new Dictionary<Guid, Orbit>();
    public Dictionary<Guid, AsteroidBelt> AsteroidBelts = new Dictionary<Guid, AsteroidBelt>();
    public PlanetSettings Settings;
    
    private HashSet<Guid> _updatedOrbits = new HashSet<Guid>();

    private ItemManager _itemManager;
    private double _time;
    private Random _random;
    public List<Agent> Agents = new List<Agent>();

    private List<Task> BeltUpdates = new List<Task>();
    
    public float Time
    {
        get => (float) _time;
    }
    public float Radius { get; }
    public float Mass { get; }
    public GalaxyZone GalaxyZone { get; }
    public Galaxy Galaxy { get; }

    public Zone(ItemManager itemManager, PlanetSettings settings, ZoneConstructionBlueprint blueprint, GalaxyZone galaxyZone, Galaxy galaxy)
    {
        _time = blueprint.Time;
        GalaxyZone = galaxyZone;
        Galaxy = galaxy;
        Radius = blueprint.Radius;
        Mass = blueprint.Mass;
        _itemManager = itemManager;
        Settings = settings;
        _random = new Random(Convert.ToUInt32(abs(galaxyZone?.Name.GetHashCode() ?? 1337)));
        
        foreach (var orbit in blueprint.Orbits)
        {
            Orbits.Add(orbit.ID, new Orbit(Settings, orbit));
        }
        
        foreach (var planet in blueprint.Planets)
        {
            switch (planet)
            {
                case AsteroidBeltData belt:
                    AsteroidBelts[belt.ID] = new AsteroidBelt(belt);
                    break;
                case SunData sun:
                    PlanetInstances.Add(sun.ID, new Sun(settings, sun, Orbits[planet.Orbit]));
                    break;
                case GasGiantData gas:
                    PlanetInstances.Add(gas.ID, new GasGiant(settings, gas, Orbits[planet.Orbit]));
                    break;
                default:
                    PlanetInstances.Add(planet.ID, new Planet(settings, planet, Orbits[planet.Orbit]));
                    break;
            }
        }

        foreach (var entityBlueprint in blueprint.Entities)
        {
            var entity = EntityConstructionBlueprintProjector.InstantiateFromBlueprint(_itemManager, this, entityBlueprint);
            Entities.Add(entity);
            entity.Activate();
            if (entity is Ship {IsPlayerShip: false} ship)
            {
                Agents.Add(CreateAgent(ship));
                if (lengthsq(ship.Position) < 1)
                    ship.Position = _itemManager.Random.NextFloat3(float3(-blueprint.Radius * .5f), float3(blueprint.Radius * .5f));
            }
        }

        // TODO: Associate planets with stored entities for planetary colonies
    }

    private Agent CreateAgent(Ship ship)
    {
        var agent = new Minion(ship);
        var task = new PatrolOrbitsTask();
        task.Circuit = Orbits.OrderBy(_ => _itemManager.Random.NextFloat()).Take(4).Select(x => x.Key).ToArray();
        agent.Task = task;
        return agent;
    }

    public void Update(float deltaTime)
    {
        _time += deltaTime;
        _updatedOrbits.Clear();
        foreach (var t in BeltUpdates)
            t.Wait();
        BeltUpdates.Clear();
        foreach (var orbit in Orbits)
        {
            orbit.Value.PreviousPosition = orbit.Value.Position;
            orbit.Value.Position = GetOrbitPosition(orbit.Key);
            orbit.Value.Velocity = (orbit.Value.Position - orbit.Value.PreviousPosition) / deltaTime;
        }

        foreach (var belt in AsteroidBelts)
        {
            Array.Copy(belt.Value.NewTransforms, belt.Value.Transforms, belt.Value.Transforms.Length);
            belt.Value.OrbitPosition = belt.Value.NewOrbitPosition;
            BeltUpdates.Add(Task.Run(() => UpdateAsteroidTransforms(belt.Key)));
        }
        
        foreach(var agent in Agents)
            agent.Update(deltaTime);
        
        foreach (var entity in Entities.ToArray()) entity.Update(deltaTime);
    }
    
    // Determine orbital position recursively, caching parent positions to avoid repeated calculations
    public float2 GetOrbitPosition(Guid orbitID)
    {
        // Root orbit is fixed at origin
        if(orbitID==Guid.Empty)
            return float2.zero;
        if (!Orbits.ContainsKey(orbitID))
        {
            Log?.Invoke("Requested orbit is not part of this zone!");
            return float2.zero;
        }
        
        if (!_updatedOrbits.Contains(orbitID))
        {
            var orbit = Orbits[orbitID];
            float2 pos = float2.zero;
            if (orbit.Period > .01f)
            {
                var phase = (float) frac(_time / orbit.Period);
                pos = Orbit.Evaluate(frac(phase + orbit.Phase)) * orbit.Distance;
                
                if (float.IsNaN(pos.x))
                {
                    //_context.Log("Orbit position is NaN, something went very wrong!");
                    pos = float2.zero;
                }
            }

            var parentPosition = orbit.Parent == Guid.Empty
                ? orbit.FixedPosition
                : GetOrbitPosition(orbit.Parent);
            Orbits[orbitID].Position = parentPosition + pos;
            _updatedOrbits.Add(orbitID);
        }

        return Orbits[orbitID].Position;
    }

    public float2 GetOrbitVelocity(Guid orbit)
    {
        if (Orbits.ContainsKey(orbit))
            return Orbits[orbit].Velocity;
        return float2.zero;
    }

    public int NearestAsteroid(Guid planetDataID, float2 position)
    {
        var belt = AsteroidBelts[planetDataID];
        var asteroidPositions = belt.Transforms;

        int nearest = 0;
        float nearestDistance = Single.MaxValue;
        for (int i = 0; i < belt.AsteroidCount; i++)
        {
            var dist = lengthsq(asteroidPositions[i].xz - position);
            if (AsteroidExists(planetDataID, i) && dist < nearestDistance)
            {
                nearest = i;
                nearestDistance = dist;
            }
        }

        return nearest;
    }

    public bool AsteroidExists(Guid planetDataID, int asteroid) => AsteroidBelts[planetDataID].ContainsAsteroid(asteroid);

    private void UpdateAsteroidTransforms(Guid planetDataID)
    {
        var belt = AsteroidBelts[planetDataID];

        var orbit = Orbits[belt.Orbit];
        belt.NewOrbitPosition = GetOrbitPosition(orbit.Parent);
        for (var i = 0; i < belt.AsteroidCount; i++)
        {
            var asteroid = belt.GetAsteroid(i);
            float size;
            if(belt.RespawnTimers.ContainsKey(i)) size = 0;
            else if (belt.Damage.ContainsKey(i))
            {
                var asteroidHitpoints = Settings.AsteroidHitpoints.Evaluate(asteroid.Size);
                var damage = (asteroidHitpoints - belt.Damage[i]) / asteroidHitpoints;
                size = Settings.AsteroidSize.Evaluate(damage * asteroid.Size);
            }
            else size = Settings.AsteroidSize.Evaluate(asteroid.Size);

            var rot = (float) (_time * asteroid.RotationSpeed % (PI * 2));
            var pos = Orbit.Evaluate((float) frac(_time / Settings.OrbitPeriod.Evaluate(asteroid.Distance) +
                                                      asteroid.Phase)) * asteroid.Distance + belt.NewOrbitPosition;
            //belt.NewPositions[i] = float3(pos.x, GetHeight(pos) + Settings.AsteroidVerticalOffset, pos.y);
            belt.NewTransforms[i] = float4(pos.x, pos.y, rot, size);
        }
    }

    public void MineAsteroid(Entity miner, Guid asteroidBelt, int asteroid, float damage, float efficiency, float penetration)
    {
        var belt = AsteroidBelts[asteroidBelt];
        //var asteroidTransform = belt.Transforms[asteroid];

        var size = belt.GetAsteroid(asteroid).Size;
        var asteroidHitpoints = Settings.AsteroidHitpoints.Evaluate(size);
        
        if (!belt.Damage.ContainsKey(asteroid))
            belt.Damage[asteroid] = 0;
        belt.Damage[asteroid] = belt.Damage[asteroid] + damage;
        
        if (!belt.MiningAccumulator.ContainsKey((miner, asteroid)))
            belt.MiningAccumulator[(miner, asteroid)] = 0;
        belt.MiningAccumulator[(miner, asteroid)] = belt.MiningAccumulator[(miner, asteroid)] + damage;
        
        if (belt.Damage[asteroid] > asteroidHitpoints)
        {
            belt.RespawnTimers[asteroid] = Settings.AsteroidRespawnTime.Evaluate(size);
            belt.Damage.Remove(asteroid);
            belt.MiningAccumulator.Remove((miner, asteroid));
            return;
        }

        var resourceCount = belt.Resources.Sum(x => x.Value);
        var resource = belt.Resources.MaxBy(x => pow(x.Value, 1f / penetration) * _random.NextFloat());
        if (efficiency * _random.NextFloat() * belt.MiningAccumulator[(miner, asteroid)] * resourceCount / Settings.MiningDifficulty > 1)
        {
            belt.MiningAccumulator.Remove((miner, asteroid));
            // var newSimpleCommodity = new SimpleCommodity
            // {
            //     Data = resource.Key,
            //     Quantity = 1
            // };
            // TODO: Drop item onto the Grid
            //miner.AddCargo(newSimpleCommodity);
        }
    }

    public SecurityLevel GetSecurityLevel(float2 pos)
    {
        if (GalaxyZone.Owner==null) return SecurityLevel.Open;
        
        var security = SecurityLevel.Open;
        foreach (var entity in Entities)
        {
            if (entity is OrbitalEntity orbitalEntity && orbitalEntity.SecurityRadius > 1 && entity.Faction.ID == GalaxyZone.Owner.ID)
            {
                if (orbitalEntity.SecurityLevel > security && length(orbitalEntity.Position.xz - pos) < orbitalEntity.SecurityRadius * Settings.SecureAreaRadiusMultiplier)
                    security = orbitalEntity.SecurityLevel;
            }
        }

        return security;
    }
    
    public float GetHeight(float2 position)
    {
        var result = -PowerPulse(length(position)/(Radius*2), Settings.ZoneDepthExponent) * Settings.ZoneDepth;
        foreach (var body in PlanetInstances.Values)
        {
            var p = position - body.Orbit.Position;
            var distSqr = lengthsq(p);
            var gravityRadius = body.GravityWellRadius;
            if (distSqr < gravityRadius*gravityRadius)
            {
                var depth = body.GravityWellDepth;
                result -= PowerPulse(sqrt(distSqr) / gravityRadius, body.GravityDepthExponent) * depth;
            }

            if (body is GasGiant gas)
            {
                var waveRadius = gas.GravityWavesRadius;
                if(distSqr < waveRadius*waveRadius)
                {
                    var depth = gas.GravityWavesDepth;
                    var frequency = Settings.WaveFrequency.Evaluate(body.Mass);
                    var speed = gas.GravityWavesSpeed;
                    result -= RadialWaves(sqrt(distSqr) / waveRadius, 8, 1.25f, frequency, (float) (_time * speed)) * depth;
                }
            }
        }

        return result;
    }

    public float GetLight(float2 position)
    {
        var light = 0f;
        foreach (var body in PlanetInstances.Values)
        {
            if (body is Sun sun)
            {
                var p = position - body.Orbit.Position;
                var distSqr = lengthsq(p);
                var lightRadius = sun.LightRadius;
                if (distSqr < lightRadius * lightRadius)
                {
                    light += PowerPulse(sqrt(distSqr) / lightRadius, 8);
                }
            }
        }

        return light;
    }

    public float2 GetForce(float2 position)
    {
        var normal = GetNormal(position);
        var f = new float2(normal.x, normal.z);
        return f * Settings.GravityStrength * lengthsq(f);
    }

    public static float PowerPulse(float x, float exponent)
    {
        x *= 2;
        x = clamp(x, -1, 1);
        return pow((x + 1) * (1 - x), exponent);
    }

    public static float RadialWaves(float x, float maskExponent, float sineExponent, float frequency, float phase)
    {
        //x *= 2;
        return PowerPulse(x, maskExponent) * cos(pow(x*2, sineExponent) * frequency + phase);
    }

    public float3 GetNormal(float2 pos, float step = .1f, float mul = 1)
    {
        float hL = GetHeight(new float2(pos.x - step, pos.y)) * mul;
        float hR = GetHeight(new float2(pos.x + step, pos.y)) * mul;
        float hD = GetHeight(new float2(pos.x, pos.y - step)) * mul;
        float hU = GetHeight(new float2(pos.x, pos.y + step)) * mul;

        // Deduce terrain normal
        float3 normal = new float3((hL - hR), (hD - hU), step*2);
        return normalize(normal).xzy;
    }
}

public class Planet
{
    public Orbit Orbit;
    protected readonly PlanetSettings Settings;
    public Guid ID { get; }
    public string Name { get; }
    public Guid OrbitId { get; }
    public float Mass { get; }
    public IReadOnlyDictionary<Guid, float> Resources { get; }
    public float BodyRadiusMultiplier { get; }
    public float GravityRadiusMultiplier { get; }
    public float GravityDepthMultiplier { get; }
    public float GravityDepthExponent { get; }
    public float GravityWellDepth;
    public float GravityWellRadius;
    public float BodyRadius;

    public Planet(PlanetSettings settings, BodyData data, Orbit orbit)
    {
        Settings = settings;
        Orbit = orbit;
        ID = data.ID;
        Name = data.Name ?? "";
        OrbitId = data.Orbit;
        Mass = data.Mass;
        Resources = data.Resources;
        BodyRadiusMultiplier = data.BodyRadiusMultiplier;
        GravityRadiusMultiplier = data.GravityRadiusMultiplier;
        GravityDepthMultiplier = data.GravityDepthMultiplier;
        GravityDepthExponent = data.GravityDepthExponent;
        CalculateProperties();
    }

    public void CalculateProperties()
    {
        BodyRadius = Settings.BodyRadius.Evaluate(Mass) * BodyRadiusMultiplier;
        GravityWellRadius = Settings.GravityRadius.Evaluate(Mass) * GravityRadiusMultiplier;
        GravityWellDepth = Settings.GravityDepth.Evaluate(Mass) * GravityDepthMultiplier;
    }

}

public class GasGiant : Planet
{
    public float FirstOffsetDomainRotationSpeed { get; }
    public float FirstOffsetRotationSpeed { get; }
    public float SecondOffsetDomainRotationSpeed { get; }
    public float SecondOffsetRotationSpeed { get; }
    public float AlbedoRotationSpeed { get; }
    public float WaveRadiusMultiplier { get; }
    public float WaveDepthMultiplier { get; }
    public float WaveDepthExponent { get; }
    public float WaveSpeedMultiplier { get; }
    public IReadOnlyList<string> MaterialOverrides { get; }
    public float4[] Colors { get; }
    public float GravityWavesDepth;
    public float GravityWavesRadius;
    public float GravityWavesSpeed;

    public GasGiant(PlanetSettings settings, GasGiantData data, Orbit orbit) : base(settings, data, orbit)
    {
        FirstOffsetDomainRotationSpeed = data.FirstOffsetDomainRotationSpeed;
        FirstOffsetRotationSpeed = data.FirstOffsetRotationSpeed;
        SecondOffsetDomainRotationSpeed = data.SecondOffsetDomainRotationSpeed;
        SecondOffsetRotationSpeed = data.SecondOffsetRotationSpeed;
        AlbedoRotationSpeed = data.AlbedoRotationSpeed;
        WaveRadiusMultiplier = data.WaveRadiusMultiplier;
        WaveDepthMultiplier = data.WaveDepthMultiplier;
        WaveDepthExponent = data.WaveDepthExponent;
        WaveSpeedMultiplier = data.WaveSpeedMultiplier;
        MaterialOverrides = data.MaterialOverrides;
        Colors = data.Colors;
        CalculateProperties();
    }

    public new void CalculateProperties()
    {
        base.CalculateProperties();
        GravityWavesDepth = Settings.WaveDepth.Evaluate(Mass) * WaveDepthMultiplier;
        GravityWavesRadius = Settings.WaveRadius.Evaluate(Mass) * WaveRadiusMultiplier;
        GravityWavesSpeed = Settings.WaveSpeed.Evaluate(Mass) * WaveSpeedMultiplier;
    }
}

public class Sun : GasGiant
{
    public float3 LightColor { get; }
    public float3 FogTintColor { get; }
    public float LightRadiusMultiplier { get; }
    public float LightRadius;

    public Sun(PlanetSettings settings, SunData data, Orbit orbit) : base(settings, data, orbit)
    {
        LightColor = data.LightColor;
        FogTintColor = data.FogTintColor;
        LightRadiusMultiplier = data.LightRadiusMultiplier;
        CalculateProperties();
    }

    public new void CalculateProperties()
    {
        base.CalculateProperties();
        LightRadius = Settings.LightRadius.Evaluate(Mass) * LightRadiusMultiplier;
    }
}

public class AsteroidBelt
{
    private readonly Asteroid[] _asteroids;
    public Guid ID { get; }
    public string Name { get; }
    public Guid Orbit { get; }
    public float Mass { get; }
    public IReadOnlyDictionary<Guid, float> Resources { get; }
    public float BodyRadiusMultiplier { get; }
    public float GravityRadiusMultiplier { get; }
    public float GravityDepthMultiplier { get; }
    public float GravityDepthExponent { get; }
    public float4[] Transforms; // x, y, rotation, scale
    public float4[] NewTransforms; // x, y, rotation, scale
    public float Radius { get; }
    public float2 OrbitPosition;
    public float2 NewOrbitPosition;
    public Dictionary<int, float> RespawnTimers = new Dictionary<int, float>();
    public Dictionary<int, float> Damage = new Dictionary<int, float>();
    public Dictionary<(Entity, int), float> MiningAccumulator = new Dictionary<(Entity, int), float>();

    public AsteroidBelt(AsteroidBeltData data)
    {
        _asteroids = data.Asteroids;
        ID = data.ID;
        Name = data.Name ?? "";
        Orbit = data.Orbit;
        Mass = data.Mass;
        Resources = data.Resources;
        BodyRadiusMultiplier = data.BodyRadiusMultiplier;
        GravityRadiusMultiplier = data.GravityRadiusMultiplier;
        GravityDepthMultiplier = data.GravityDepthMultiplier;
        GravityDepthExponent = data.GravityDepthExponent;
        Transforms = new float4[_asteroids.Length];
        NewTransforms = new float4[_asteroids.Length];
        Radius = _asteroids.Max(a => a.Distance);
    }

    public int AsteroidCount => _asteroids.Length;

    public bool ContainsAsteroid(int asteroid) => asteroid >= 0 && asteroid < _asteroids.Length;

    public Asteroid GetAsteroid(int asteroid) => _asteroids[asteroid];

}

public class Orbit
{
    public Guid ID { get; }
    public Guid Parent { get; }
    public float Distance { get; }
    public float Phase { get; }
    public float2 FixedPosition { get; }
    public float2 Velocity = float2.zero;
    public float2 Position = float2.zero;
    public float2 PreviousPosition = float2.zero;
    public float Period;

    public static float2 Evaluate(float phase)
    {
        phase *= PI * 2;
        return new float2(cos(phase), sin(phase));
    }

    public Orbit(PlanetSettings settings, OrbitData data)
    {
        ID = data.ID;
        Parent = data.Parent;
        Distance = data.Distance;
        Phase = data.Phase;
        FixedPosition = data.FixedPosition;
        Period = settings.OrbitPeriod.Evaluate(data.Distance);
    }

}
