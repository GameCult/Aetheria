/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using UniRx;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

public class Zone
{
    public Action<string> Log;
    public ReactiveCollection<Entity> Entities = new ReactiveCollection<Entity>();

    // Laid mines, in lay order. Zone owns their existence: Lay is the one adder, the mine step the one remover,
    // and every removal is a blast. Runtime state, never saved.
    public ReactiveCollection<Mine> Mines = new ReactiveCollection<Mine>();

    // Vented vapour clouds, in vent order. Zone owns their existence: Vent is the one adder, StepClouds the one
    // remover, and Obscuration the one reader that detection uses. Runtime state, never saved.
    public ReactiveCollection<VapourCloud> Clouds = new ReactiveCollection<VapourCloud>();
    public Dictionary<CultRecordKey, BodyData> Planets = new Dictionary<CultRecordKey, BodyData>();
    public Dictionary<CultRecordKey, Planet> PlanetInstances = new Dictionary<CultRecordKey, Planet>();

    public Dictionary<CultRecordKey, Orbit> Orbits = new Dictionary<CultRecordKey, Orbit>();
    public Dictionary<CultRecordKey, AsteroidBelt> AsteroidBelts = new Dictionary<CultRecordKey, AsteroidBelt>();
    public PlanetSettings Settings;

    // Mining index (docs/aetheria-release-map.md, L3-L5): every targetable thing in the zone, fed by providers --
    // the entities, and one provider per belt. The reticle, cycling and any range query over rocks go through it;
    // nothing else scans a belt.
    public readonly TargetingIndex Targets = new TargetingIndex();
    private readonly List<Sun> _suns = new List<Sun>();

    private HashSet<CultRecordKey> _updatedOrbits = new HashSet<CultRecordKey>();

    private ItemManager _itemManager;
    private double _time;
    public List<Agent> Agents = new List<Agent>();

    // Cut 2 (docs/mining-cut.md): chunk wear, keyed by chunk. Zone.Wear is the only writer; ChunkExists and
    // ChunkRadius are the only readers besides the pack/unpack round trip below.
    private Dictionary<ChunkId, ChunkWear> _wear = new Dictionary<ChunkId, ChunkWear>();

    // Cut 3 (docs/fire-control-cut.md, 0b table): Zone owns the pending-shot collection and the ShotId
    // namespace; FireControl owns every transition a shot goes through. ShotCommitted publishes once a shot's
    // outcome is decided (R4's commit horizon); ShotResolved republishes the same, unchanged outcome at
    // arrival, right before the shot is removed. Presentations are the only readers of either.
    public List<PendingShot> PendingShots = new List<PendingShot>();
    public Subject<ShotOutcome> ShotCommitted = new Subject<ShotOutcome>();
    public Subject<ShotOutcome> ShotResolved = new Subject<ShotOutcome>();
    private int _nextShotId;
    public int NextShotId() => ++_nextShotId;

    // The committed shot a presentation was handed the id of by OnFire. It carries what the simulation decided
    // -- the target it flies at (null for a round that carries none) and where it bursts -- so a presentation
    // reads that rather than the shooter's current selection.
    public bool TryGetShot(int shotId, out PendingShot shot)
    {
        foreach (var pending in PendingShots)
            if (pending.ShotId == shotId)
            {
                shot = pending;
                return true;
            }
        shot = default;
        return false;
    }

    public float Time
    {
        get => (float) _time;
    }

    // Zone time at full precision, for the index's orbital keys, which must agree with ChunkPose's own.
    internal double ExactTime => _time;
    public ZonePack Pack { get; }
    public GalaxyZone GalaxyZone { get; }
    public Galaxy Galaxy { get; }

    // Cut 6b (docs/fire-control-cut.md, 6.1, Soul finding 6): one zone, one stable identity -- a galaxy seed
    // reproduces it, and nothing else in the zone (an NPC joining, a shop opening, a wormhole exit) can
    // perturb it. FireControl.Commit builds its own local generator from this and the shot's own id instead
    // of touching a shared stream.
    public uint CombatSeed { get; }

    // The one source of floating item and mine ids: zone-scoped, from 1, never reused. Runtime state, not saved.
    private uint _lastBodyId;
    public FloatingBodyId NextBodyId() => new FloatingBodyId(++_lastBodyId);

    public Zone(ItemManager itemManager, PlanetSettings settings, ZonePack pack, GalaxyZone galaxyZone, Galaxy galaxy)
    {
        _time = pack.Time;
        GalaxyZone = galaxyZone;
        Galaxy = galaxy;
        Pack = pack;
        _itemManager = itemManager;
        Settings = settings;
        CombatSeed = galaxyZone?.Name.StableHash() ?? 1337u;
        var cache = itemManager.ItemData;

        // Cut 5, 5.6 (docs/fire-control-cut.md, Soul finding 7; operator ruling Q3): death removes the ship,
        // in the simulation, not only in Unity's own loot-drop subscription. One subscription point covers
        // every join, whichever caller admits the entity (deserialization below, a jump, a spawned turret) --
        // ObserveAdd fires for all of them. Forbidden writer: no presentation may remove an entity from Zone.
        Entities.ObserveAdd().Subscribe(add => add.Value.Death.Subscribe(_ => { Entities.Remove(add.Value); add.Value.Deactivate(); }));

        Targets.Add(new EntityTargets());

        foreach (var orbit in pack.Orbits)
        {
            Orbits.Add(orbit.Key, new Orbit(Settings, cache.Get(orbit)));
        }

        foreach (var body in pack.Planets)
        {
            var planet = cache.Get(body);
            Planets.Add(body.Key, planet);
            switch (planet)
            {
                case AsteroidBeltData belt:
                    // Mining Cut 3 (Q16 B): a belt from before field kinds existed gets its kind here, once, by
                    // the rule generation uses, and keeps it.
                    FieldKinds.Ensure(body.Key, belt, cache);
                    AsteroidBelts[body.Key] = new AsteroidBelt(belt);
                    Targets.Add(new BeltTargets(this, body.Key, belt, settings, _time));
                    break;
                case SunData sun:
                    var sunInstance = new Sun(settings, sun, Orbits[planet.Orbit.Key]);
                    PlanetInstances.Add(body.Key, sunInstance);
                    _suns.Add(sunInstance);
                    break;
                case GasGiantData gas:
                    PlanetInstances.Add(body.Key, new GasGiant(settings, gas, Orbits[planet.Orbit.Key]));
                    break;
                default:
                    PlanetInstances.Add(body.Key, new Planet(settings, planet, Orbits[planet.Orbit.Key]));
                    break;
            }
        }

        // Cut 2 (docs/mining-cut.md): read after belts are built so a later ChunkExists/ChunkRadius call has
        // Planets/AsteroidBelts populated. A pack from before key 6 existed has ChunkWear == null (nullable
        // persistence rule); that reads as no wear rather than throwing.
        if (pack.ChunkWear != null)
            foreach (var wear in pack.ChunkWear)
                _wear[new ChunkId(wear.Field, wear.Index)] = new ChunkWear { Damage = wear.Damage, BrokenUntil = wear.BrokenUntil };

        foreach (var entityPack in pack.Entities)
        {
            var entity = EntitySerializer.Unpack(_itemManager, this, entityPack);
            var ship = entity as Ship;
            Admit(entity, piloted: ship is {IsPlayerShip: false});
            // Generation placement, not admission: a ship packed at the origin is scattered across the zone.
            if (ship is {IsPlayerShip: false} && lengthsq(ship.Position) < 1)
                ship.Position = _itemManager.Random.NextFloat3(float3(-pack.Radius * .5f), float3(pack.Radius * .5f));
        }

        // TODO: Associate planets with stored entities for planetary colonies
    }

    // The one admission into this zone (docs/scenarios-cut.md, 2.2): the only writer of Entities.Add and the only
    // creator of agents. Construction, new-run staging (RunStart), warp arrival, undock and the console's spawned
    // turret all join through here. A piloted entity gets the zone's Minion; an unpiloted one gets no agent at all.
    // Only a ship can be piloted. The Death subscription above covers every join, whichever caller admits it.
    public void Admit(Entity entity, bool piloted)
    {
        Entities.Add(entity);
        entity.Activate();
        if (piloted) Agents.Add(CreateAgent((Ship) entity));
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
            Orbits = Orbits.Keys.Select(key => new CultRecordRef<OrbitData>(key)).ToList(),
            Planets = Planets.Keys.Select(key => new CultRecordRef<BodyData>(key)).ToList(),
            Time = _time,
            BossSpawned = Pack.BossSpawned,
            // Cut 2 (docs/mining-cut.md): drop entries that carry no live information -- healed (no damage) and
            // not currently broken -- so a save never accumulates wear rows for chunks nobody has touched since
            // they last respawned.
            ChunkWear = _wear
                .Where(kv => !IsExpired(kv.Value))
                .Select(kv => new ChunkWearPack { Field = kv.Key.Field, Index = kv.Key.Index, Damage = kv.Value.Damage, BrokenUntil = kv.Value.BrokenUntil })
                .ToList()
        };
    }

    private bool IsExpired(ChunkWear wear) => wear.Damage <= 0f && (!wear.BrokenUntil.HasValue || wear.BrokenUntil.Value <= _time);

    public void AddOrbit(OrbitData orbit)
    {
        Orbits.Add(_itemManager.ItemData.RefOf(orbit).Key, new Orbit(Settings, orbit));
    }

    public void Update(float deltaTime)
    {
        _time += deltaTime;
        _updatedOrbits.Clear();
        foreach (var orbit in Orbits)
        {
            orbit.Value.PreviousPosition = orbit.Value.Position;
            orbit.Value.Position = GetOrbitPosition(orbit.Key);
            orbit.Value.Velocity = (orbit.Value.Position - orbit.Value.PreviousPosition) / deltaTime;
        }

        foreach(var agent in Agents)
            agent.Update(deltaTime);

        var entities = Entities.ToArray();
        foreach (var entity in entities) entity.PublishPaints();
        foreach (var entity in entities) entity.Update(deltaTime);

        StepMines(deltaTime);
        StepClouds(deltaTime);

        // Cut 3: after every entity has had its chance to fire this tick, age and resolve the shots that
        // firing queued. A shot fired this tick with a flight time shorter than CommitHorizon commits and
        // resolves in this same call.
        FireControl.Step(this, deltaTime);
    }

    public void Lay(Mine mine)
    {
        mine.LaidAt = _time;
        Mines.Add(mine);
    }

    public void Vent(VapourCloud cloud)
    {
        cloud.Id = NextBodyId();
        cloud.VentedAt = _time;
        Clouds.Add(cloud);
    }

    // The fraction of a sight line's signal that survives the clouds between `a` and `b`: the product of
    // (1 - opacity now) over every cloud whose disc the segment touches, so 1f when none does. Derived from the
    // live clouds at each call and stored nowhere; Sensor.Gain is where it enters detection.
    public float Obscuration(float2 a, float2 b)
    {
        var through = 1f;
        for (var i = 0; i < Clouds.Count; i++)
        {
            var cloud = Clouds[i];
            if (cloud.Touches(a, b)) through *= 1f - cloud.OpacityAt(_time);
        }
        return through;
    }

    // After StepMines: each cloud drifts as a KinematicBody on the zone's force, as a mine does, and is removed
    // once its lifetime has run out. Nothing else removes a cloud.
    private void StepClouds(float deltaTime)
    {
        var settings = _itemManager.GameplaySettings;
        foreach (var cloud in Clouds.ToArray())
        {
            cloud.Body.Step(deltaTime, GetForce(cloud.Body.Position), settings);
            if (cloud.VentedAt + cloud.Lifetime <= _time) Clouds.Remove(cloud);
        }
    }

    // After the entity loop and before FireControl.Step, in lay order: each mine drifts as a KinematicBody, is
    // triggered by the first eligible hull its armed disc touches, and detonates through FireControl.Detonate at
    // its trigger plus fuse delay or at its lifetime, whatever its layer does (ruling mine-owner-leash). The
    // blast reads no faction; only the trigger does.
    private void StepMines(float deltaTime)
    {
        var settings = _itemManager.GameplaySettings;
        foreach (var mine in Mines.ToArray())
        {
            mine.Body.Step(deltaTime, GetForce(mine.Body.Position), settings);

            if (mine.TriggeredAt == null && mine.Armed(_time) &&
                Entities.Any(entity => mine.Triggers(entity) && FireControl.Touches(entity, mine.Body.Position, mine.BlastRadius)))
                mine.TriggeredAt = _time;

            var fused = mine.TriggeredAt + mine.FuseDelay <= _time;
            if (!fused && mine.LaidAt + mine.Lifetime > _time) continue;
            Mines.Remove(mine);
            FireControl.Detonate(this, mine.Body.Position, mine.BlastRadius, mine.Damage, mine.DamageType);
        }
    }

    // Determine orbital position recursively, caching parent positions to avoid repeated calculations
    public float2 GetOrbitPosition(CultRecordKey orbitID)
    {
        // Root orbit is fixed at origin
        if(!orbitID.IsSet())
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

            var parentPosition = !Orbits[orbitID].Data.Parent.IsSet()
                ? Orbits[orbitID].Data.FixedPosition :
                GetOrbitPosition(orbit.Data.Parent.Key);
            Orbits[orbitID].Position = parentPosition + pos;
            _updatedOrbits.Add(orbitID);
        }

        return Orbits[orbitID].Position;
    }

    public float2 GetOrbitVelocity(CultRecordKey orbit)
    {
        if (Orbits.ContainsKey(orbit))
            return Orbits[orbit].Velocity;
        return float2.zero;
    }

    // Cut 2 (docs/mining-cut.md): the index is in range for its field and the chunk is not broken at zone time.
    // Respawn is a comparison against zone time, never a per-tick loop or a write on read.
    public bool ChunkExists(ChunkId chunk)
    {
        if (!(Planets.TryGetValue(chunk.Field, out var body) && body is AsteroidBeltData beltData))
            return false;
        if (chunk.Index < 0 || chunk.Index >= beltData.Asteroids.Length)
            return false;
        return !(_wear.TryGetValue(chunk, out var wear) && wear.BrokenUntil.HasValue && wear.BrokenUntil.Value > _time);
    }

    // Cut 2: the size rule of AsteroidBelt.Size (Cut 1), now reading wear from its one owner (Zone) instead of
    // per-belt dictionaries. A broken chunk (BrokenUntil in the future) has no size; an unbroken, undamaged
    // chunk reads its authored size; a damaged one shrinks toward it.
    public float ChunkRadius(ChunkId chunk)
    {
        var belt = AsteroidBelts[chunk.Field];
        if (!_wear.TryGetValue(chunk, out var wear))
            return belt.UndamagedSize(chunk.Index, Settings);
        if (wear.BrokenUntil.HasValue && wear.BrokenUntil.Value > _time)
            return 0f;
        if (wear.Damage <= 0f)
            return belt.UndamagedSize(chunk.Index, Settings);
        return belt.DamagedSize(chunk.Index, wear.Damage, Settings);
    }

    // Cut 2: the only writer of chunk wear. Returns whether this hit broke the chunk. The break threshold is
    // strictly greater-than, matching the deleted per-tick miner's own comparison against accumulated damage:
    // damage exactly equal to hitpoints does not yet break the chunk, only damage that exceeds them does.
    // A chunk that is currently broken (zone time < BrokenUntil) absorbs nothing: this hit did not break it
    // (it was already broken), and its wear stays zeroed until it respawns and starts fresh.
    public bool Wear(ChunkId chunk, float damage)
    {
        _wear.TryGetValue(chunk, out var wear);
        if (wear.BrokenUntil.HasValue && wear.BrokenUntil.Value > _time)
            return false;

        var beltData = (AsteroidBeltData) Planets[chunk.Field];
        var size = beltData.Asteroids[chunk.Index].Size;
        var hitpoints = Settings.AsteroidHitpoints.Evaluate(size);

        var newDamage = wear.Damage + damage;

        if (newDamage > hitpoints)
        {
            var respawnTime = Settings.AsteroidRespawnTime.Evaluate(size);
            _wear[chunk] = new ChunkWear { Damage = 0f, BrokenUntil = _time + respawnTime };
            return true;
        }

        _wear[chunk] = new ChunkWear { Damage = newDamage, BrokenUntil = null };
        return false;
    }

    // Cut 1 (docs/mining-cut.md): a chunk's pose is a pure function of zone time, computed when asked. No stored
    // pose survives a tick; the sim and the renderer both read it fresh through here.
    public float4 ChunkPose(CultRecordKey belt, int index)
    {
        var beltData = Planets[belt] as AsteroidBeltData;
        var parentPosition = GetOrbitPosition(Orbits[beltData.Orbit.Key].Data.Parent.Key);
        var size = ChunkRadius(new ChunkId(belt, index));
        return AsteroidBelts[belt].Pose(index, _time, parentPosition, Settings, size);
    }

    // Mining Cut 3 (docs/mining-cut-refresh.md): how bright a chunk is -- Reflector's rule (cross-section × the light
    // on it), with the field kind's reflectivity per schematic cell times the chunk's area in cells. Computed when
    // asked; nothing about it is stored. A worn chunk shrinks and dims; a broken one, or one of a kindless belt, is dark.
    public float ChunkVisibility(ChunkId chunk)
    {
        if (!ChunkExists(chunk)) return 0f;
        var kind = _itemManager.ItemData.Get(((AsteroidBeltData) Planets[chunk.Field]).Kind);
        if (kind == null) return 0f;
        var cells = ChunkRadius(chunk) / _itemManager.GameplaySettings.SchematicCellSize;
        return kind.CrossSection * PI * cells * cells * GetLight(ChunkPose(chunk.Field, chunk.Index).xy);
    }

    public void EvaluateBelt(CultRecordKey belt, Span<float4> into)
    {
        var beltData = Planets[belt] as AsteroidBeltData;
        var parentPosition = GetOrbitPosition(Orbits[beltData.Orbit.Key].Data.Parent.Key);
        var beltInstance = AsteroidBelts[belt];
        for (var i = 0; i < beltData.Asteroids.Length; i++)
            into[i] = beltInstance.Pose(i, _time, parentPosition, Settings, ChunkRadius(new ChunkId(belt, i)));
    }

    public OrbitData CreateOrbit(CultRecordKey parent, float2 position)
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
            Distance = distance,
            Parent = new CultRecordRef<OrbitData>(parent),
            Phase = storedPhase
        };
        Orbits.Add(_itemManager.ItemData.Upsert(orbit).Key, new Orbit(Settings, orbit));
        return orbit;
    }

    public SecurityLevel GetSecurityLevel(float2 pos)
    {
        if (GalaxyZone.Owner==null) return SecurityLevel.Open;

        var security = SecurityLevel.Open;
        foreach (var entity in Entities)
        {
            if (entity is OrbitalEntity orbitalEntity && orbitalEntity.SecurityRadius > 1 && entity.Faction == GalaxyZone.Owner)
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
        foreach (var sun in _suns)
            light += SunLight(sun, length(position - sun.Orbit.Position));
        return light;
    }

    // One sun's light at `distance` from it. It never grows with distance, so the light at the nearest point of a
    // region bounds the light anywhere in it (BeltTargets' visibility bound).
    public static float SunLight(Sun sun, float distance) =>
        distance < sun.LightRadius ? PowerPulse(distance / sun.LightRadius, 8) : 0f;

    internal IReadOnlyList<Sun> Suns() => _suns;
    internal float SchematicCellSize => _itemManager.GameplaySettings.SchematicCellSize;
    internal float FieldCrossSection(AsteroidBeltData belt) => _itemManager.ItemData.Get(belt.Kind)?.CrossSection ?? 0f;

    // The one law of how a gravity well pushes anything: ships, floating items and mines all read it. The shape is
    // the one ships have always felt, normalize(f) * strength * (1 / (1 - |f|^2) - 1), gated off below |f|^2 = .001.
    // It is an acceleration, per second: the legacy law added that amount once per 60 fps frame with no dt, so
    // GravityReferenceRate (60) restates it and `acceleration * dt` gives the legacy push at 60 fps.
    public const float GravityReferenceRate = 60f;

    public static float2 GravityAcceleration(float2 normalXz, float strength)
    {
        var magnitude = lengthsq(normalXz);
        if (magnitude <= .001f) return float2.zero;
        return normalize(normalXz) * strength * (1 / (1 - magnitude) - 1) * GravityReferenceRate;
    }

    // The acceleration the well exerts at `position`.
    public float2 GetForce(float2 position)
    {
        var normal = GetNormal(position);
        return GravityAcceleration(new float2(normal.x, normal.z), Settings.GravityStrength);
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
    public float Radius { get; }

    public AsteroidBelt(AsteroidBeltData data)
    {
        Data = data;
        Radius = data.Asteroids.Max(a => a.Distance);
    }

    // Cut 1 (docs/mining-cut.md): the formula of Zone.cs:269-271 verbatim, `time` in double as `_time` was used.
    // The only place a chunk pose is computed; no per-tick copy survives it. Cut 2: size is no longer read from
    // per-belt storage -- Zone owns wear now, so the caller (Zone.ChunkPose/EvaluateBelt) computes it through
    // Zone.ChunkRadius and passes it in.
    public float4 Pose(int index, double time, float2 parentPosition, PlanetSettings settings, float size)
    {
        var asteroid = Data.Asteroids[index];
        var rot = (float) (time * asteroid.RotationSpeed % (PI * 2));
        var pos = OrbitData.Evaluate((float) frac(time / settings.OrbitPeriod.Evaluate(asteroid.Distance) +
                                                  asteroid.Phase)) * asteroid.Distance + parentPosition;
        return float4(pos.x, pos.y, rot, size);
    }

    // Cut 1's size rule (Zone.cs:259-267), split in Cut 2 into its undamaged and damaged halves now that wear
    // lives on Zone instead of here. Neither reads wear directly; the caller (Zone.ChunkRadius) decides which
    // applies and supplies the damage.
    public float UndamagedSize(int index, PlanetSettings settings) => settings.AsteroidSize.Evaluate(Data.Asteroids[index].Size);

    public float DamagedSize(int index, float damage, PlanetSettings settings)
    {
        var asteroidHitpoints = settings.AsteroidHitpoints.Evaluate(Data.Asteroids[index].Size);
        var remaining = (asteroidHitpoints - damage) / asteroidHitpoints;
        return settings.AsteroidSize.Evaluate(remaining * Data.Asteroids[index].Size);
    }
}

// Cut 2 (docs/mining-cut.md): a chunk is (field, index). "Field" is deliberate, not "belt" -- the operator's
// ruling generalizes chunks to other kinds of debris field later; only AsteroidBelt/AsteroidBeltData, the one
// live field kind, keep their existing names.
public readonly struct ChunkId : IEquatable<ChunkId>
{
    public readonly CultRecordKey Field;
    public readonly int Index;

    public ChunkId(CultRecordKey field, int index)
    {
        Field = field;
        Index = index;
    }

    public bool Equals(ChunkId other) => Field.Equals(other.Field) && Index == other.Index;
    public override bool Equals(object obj) => obj is ChunkId other && Equals(other);
    public override int GetHashCode() => (Field.GetHashCode() * 397) ^ Index;
}

// Mining Cut 3 (docs/mining-cut-refresh.md, Q2 A): what an entity targets -- another entity, a chunk, or nothing.
// One slot holds either kind (Entity.Target, written only by Entity.SetTarget). A chunk never equals an entity.
// There is deliberately no == operator: a comparison against null or an entity must say which side it means.
public readonly struct TargetRef : IEquatable<TargetRef>
{
    public readonly Entity Entity;
    public readonly ChunkId? Chunk;

    public TargetRef(Entity entity)
    {
        Entity = entity;
        Chunk = null;
    }

    public TargetRef(ChunkId chunk)
    {
        Entity = null;
        Chunk = chunk;
    }

    public static TargetRef None => default;
    public bool IsNone => Entity == null && !Chunk.HasValue;

    public static implicit operator TargetRef(Entity entity) => new TargetRef(entity);
    public static implicit operator TargetRef(ChunkId chunk) => new TargetRef(chunk);

    public bool Equals(TargetRef other) => ReferenceEquals(Entity, other.Entity) && Nullable.Equals(Chunk, other.Chunk);
    public override bool Equals(object obj) => obj is TargetRef other && Equals(other);
    public override int GetHashCode() => Chunk.HasValue ? Chunk.Value.GetHashCode() : Entity?.GetHashCode() ?? 0;
}

// Cut 2: wear on one chunk. BrokenUntil is the absolute zone time the chunk respawns at; null means the chunk
// carries damage (or none) but is not currently broken. Respawn is read lazily by comparing to zone time --
// nothing decrements or clears this on a timer.
public struct ChunkWear
{
    public float Damage;
    public double? BrokenUntil;
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
