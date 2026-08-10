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
    //public HashSet<Guid> Planets = new HashSet<Guid>();
    public ReactiveCollection<Entity> Entities = new ReactiveCollection<Entity>();
    //public Dictionary<Guid, OrbitData> Orbits = new Dictionary<Guid, OrbitData>();
    public Dictionary<Guid, BodyData> Planets = new Dictionary<Guid, BodyData>();
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
    public ZonePack Pack { get; }
    public GalaxyZone GalaxyZone { get; }
    public Galaxy Galaxy { get; }

    public Zone(ItemManager itemManager, PlanetSettings settings, ZonePack pack, GalaxyZone galaxyZone, Galaxy galaxy)
    {
        _time = pack.Time;
        GalaxyZone = galaxyZone;
        Galaxy = galaxy;
        Pack = pack;
        _itemManager = itemManager;
        Settings = settings;
        _random = new Random(Convert.ToUInt32(abs(galaxyZone?.Name.GetHashCode() ?? 1337)));
        
        foreach (var orbit in pack.Orbits)
        {
            Orbits.Add(orbit.ID, new Orbit(Settings, orbit));
        }
        
        foreach (var planet in pack.Planets)
        {
            Planets.Add(planet.ID, planet);
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

        foreach (var entityPack in pack.Entities)
        {
            var entity = EntitySerializer.Unpack(_itemManager, this, entityPack);
            Entities.Add(entity);
            entity.Activate();
            if (entity is Ship {IsPlayerShip: false} ship)
            {
                Agents.Add(CreateAgent(ship));
                if (lengthsq(ship.Position) < 1)
                    ship.Position = _itemManager.Random.NextFloat3(float3(-pack.Radius * .5f), float3(pack.Radius * .5f));
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

    public ZonePack PackZone()
    {
        return new ZonePack
        {
            Radius = Pack.Radius,
            Mass = Pack.Mass,
            Entities = Entities.Select(EntitySerializer.Pack).ToList(),
            Orbits = Orbits.Values.Select(o=>o.Data).ToList(),
            Planets = Planets.Values.ToList(),
            Time = _time
        };
    }

    public void AddOrbit(OrbitData orbit)
    {
        Orbits.Add(orbit.ID, new Orbit(Settings, orbit));
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
                pos = OrbitData.Evaluate(frac(phase + orbit.Data.Phase)) * orbit.Data.Distance;
                
                if (float.IsNaN(pos.x))
                {
                    //_context.Log("Orbit position is NaN, something went very wrong!");
                    pos = float2.zero;
                }
            }

            var parentPosition = Orbits[orbitID].Data.Parent == Guid.Empty 
                ? Orbits[orbitID].Data.FixedPosition : 
                GetOrbitPosition(orbit.Data.Parent);
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
        var beltData = Planets[planetDataID] as AsteroidBeltData;

        var asteroidPositions = AsteroidBelts[planetDataID].Transforms;

        int nearest = 0;
        float nearestDistance = Single.MaxValue;
        for (int i = 0; i < beltData.Asteroids.Length; i++)
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

    public bool AsteroidExists(Guid planetDataID, int asteroid) => ((AsteroidBeltData) Planets[planetDataID]).Asteroids.Length > asteroid && asteroid >= 0;

    private void UpdateAsteroidTransforms(Guid planetDataID)
    {
        var beltData = Planets[planetDataID] as AsteroidBeltData;
        
        var belt = AsteroidBelts[planetDataID];

        var orbitData = Orbits[beltData.Orbit].Data;
        belt.NewOrbitPosition = GetOrbitPosition(orbitData.Parent);
        for (var i = 0; i < beltData.Asteroids.Length; i++)
        {
            float size;
            if(belt.RespawnTimers.ContainsKey(i)) size = 0;
            else if (belt.Damage.ContainsKey(i))
            {
                var asteroidHitpoints = Settings.AsteroidHitpoints.Evaluate(beltData.Asteroids[i].Size);
                var damage = (asteroidHitpoints - belt.Damage[i]) / asteroidHitpoints;
                size = Settings.AsteroidSize.Evaluate(damage * beltData.Asteroids[i].Size);
            }
            else size = Settings.AsteroidSize.Evaluate(beltData.Asteroids[i].Size);

            var rot = (float) (_time * beltData.Asteroids[i].RotationSpeed % (PI * 2));
            var pos = OrbitData.Evaluate((float) frac(_time / Settings.OrbitPeriod.Evaluate(beltData.Asteroids[i].Distance) +
                                                      beltData.Asteroids[i].Phase)) * beltData.Asteroids[i].Distance + belt.NewOrbitPosition;
            //belt.NewPositions[i] = float3(pos.x, GetHeight(pos) + Settings.AsteroidVerticalOffset, pos.y);
            belt.NewTransforms[i] = float4(pos.x, pos.y, rot, size);
        }
    }

    public OrbitData CreateOrbit(Guid parent, float2 position)
    {
        var parentPosition = GetOrbitPosition(parent);
        var delta = position - parentPosition;
        var distance = length(delta);
        var period = Settings.OrbitPeriod.Evaluate(distance);
        var phase = atan2(delta.y, delta.x) / (PI * 2);
        var currentPhase = frac(_time / period);
        var storedPhase = (float) frac(phase - currentPhase);

        var orbit = new OrbitData
        {
            ID = Guid.NewGuid(),
            Distance = distance,
            Parent = parent,
            Phase = storedPhase
        };
        Orbits.Add(orbit.ID, new Orbit(Settings, orbit));
        return orbit;
    }

    public void MineAsteroid(Entity miner, Guid asteroidBelt, int asteroid, float damage, float efficiency, float penetration)
    {
        var beltData = Planets[asteroidBelt] as AsteroidBeltData;
        var belt = AsteroidBelts[asteroidBelt];
        //var asteroidTransform = belt.Transforms[asteroid];

        var size = beltData.Asteroids[asteroid].Size;
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

        var resourceCount = beltData.Resources.Sum(x => x.Value);
        var resource = beltData.Resources.MaxBy(x => pow(x.Value, 1f / penetration) * _random.NextFloat());
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
        var result = -PowerPulse(length(position)/(Pack.Radius*2), Settings.ZoneDepthExponent) * Settings.ZoneDepth;
        foreach (var body in PlanetInstances.Values)
        {
            var p = position - body.Orbit.Position; //GetOrbitPosition(body.BodyData.Orbit)
            var distSqr = lengthsq(p);
            var gravityRadius = body.GravityWellRadius;
            if (distSqr < gravityRadius*gravityRadius)
            {
                var depth = body.GravityWellDepth;
                result -= PowerPulse(sqrt(distSqr) / gravityRadius, body.BodyData.GravityDepthExponent) * depth;
            }

            if (body is GasGiant gas)
            {
                var waveRadius = gas.GravityWavesRadius;
                if(distSqr < waveRadius*waveRadius)
                {
                    var depth = gas.GravityWavesDepth;
                    var frequency = Settings.WaveFrequency.Evaluate(body.BodyData.Mass);
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
    public BodyData BodyData;
    public float GravityWellDepth;
    public float GravityWellRadius;
    public float BodyRadius;

    public Planet(PlanetSettings settings, BodyData data, Orbit orbit)
    {
        Settings = settings;
        Orbit = orbit;
        BodyData = data;
        CalculateProperties();
    }

    public void CalculateProperties()
    {
        BodyRadius = Settings.BodyRadius.Evaluate(BodyData.Mass) * BodyData.BodyRadiusMultiplier;
        GravityWellRadius = Settings.GravityRadius.Evaluate(BodyData.Mass) * BodyData.GravityRadiusMultiplier;
        GravityWellDepth = Settings.GravityDepth.Evaluate(BodyData.Mass) * BodyData.GravityDepthMultiplier;
    }
}

public class GasGiant : Planet
{
    public GasGiantData GasGiantData;
    public float GravityWavesDepth;
    public float GravityWavesRadius;
    public float GravityWavesSpeed;

    public GasGiant(PlanetSettings settings, GasGiantData data, Orbit orbit) : base(settings, data, orbit)
    {
        GasGiantData = data;
        CalculateProperties();
    }

    public new void CalculateProperties()
    {
        base.CalculateProperties();
        GravityWavesDepth = Settings.WaveDepth.Evaluate(BodyData.Mass) * GasGiantData.WaveDepthMultiplier;
        GravityWavesRadius = Settings.WaveRadius.Evaluate(BodyData.Mass) * GasGiantData.WaveRadiusMultiplier;
        GravityWavesSpeed = Settings.WaveSpeed.Evaluate(BodyData.Mass) * GasGiantData.WaveSpeedMultiplier;
    }
}

public class Sun : GasGiant
{
    public SunData SunData;
    public float LightRadius;

    public Sun(PlanetSettings settings, SunData data, Orbit orbit) : base(settings, data, orbit)
    {
        SunData = data;
        CalculateProperties();
    }

    public new void CalculateProperties()
    {
        base.CalculateProperties();
        LightRadius = Settings.LightRadius.Evaluate(BodyData.Mass) * SunData.LightRadiusMultiplier;
    }
}

public class AsteroidBelt
{
    public AsteroidBeltData Data;
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
        Data = data;
        Transforms = new float4[data.Asteroids.Length];
        NewTransforms = new float4[data.Asteroids.Length];
        Radius = data.Asteroids.Max(a => a.Distance);
    }
}

public class Orbit
{
    public OrbitData Data { get; }
    public float2 Velocity = float2.zero;
    public float2 Position = float2.zero;
    public float2 PreviousPosition = float2.zero;
    public float Period;

    public Orbit(PlanetSettings settings, OrbitData data)
    {
        Data = data;
        Period = settings.OrbitPeriod.Evaluate(data.Distance);
    }
}
