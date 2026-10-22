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

    // Membership, in admission order. Admit and Remove are the only writers; everyone else reads this view, which has
    // no mutating member, so the death watch below follows membership by construction.
    private readonly ReactiveCollection<Entity> _entities = new ReactiveCollection<Entity>();
    public MemberView Entities { get; }

    public sealed class MemberView : IReadOnlyReactiveCollection<Entity>
    {
        private readonly ReactiveCollection<Entity> _members;
        internal MemberView(ReactiveCollection<Entity> members) => _members = members;
        public int Count => _members.Count;
        public Entity this[int index] => _members[index];
        public int IndexOf(Entity entity) => _members.IndexOf(entity);
        public bool Contains(Entity entity) => _members.Contains(entity);
        public IEnumerator<Entity> GetEnumerator() => _members.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public UniRx.IObservable<CollectionAddEvent<Entity>> ObserveAdd() => _members.ObserveAdd();
        public UniRx.IObservable<int> ObserveCountChanged(bool notifyCurrentCount = false) => _members.ObserveCountChanged(notifyCurrentCount);
        public UniRx.IObservable<CollectionMoveEvent<Entity>> ObserveMove() => _members.ObserveMove();
        public UniRx.IObservable<CollectionRemoveEvent<Entity>> ObserveRemove() => _members.ObserveRemove();
        public UniRx.IObservable<CollectionReplaceEvent<Entity>> ObserveReplace() => _members.ObserveReplace();
        public UniRx.IObservable<Unit> ObserveReset() => _members.ObserveReset();
    }

    // Laid mines, in lay order. Zone owns their existence: Lay is the one adder, the mine step the one remover,
    // and every removal is a blast. Runtime state, never saved.
    public ReactiveCollection<Mine> Mines = new ReactiveCollection<Mine>();

    // Vented vapour clouds, in vent order. Zone owns their existence: Vent is the one adder, StepClouds the one
    // remover, and Obscuration the one reader that detection uses. Runtime state, never saved.
    public ReactiveCollection<VapourCloud> Clouds = new ReactiveCollection<VapourCloud>();

    // Floating items (death drops, jettisoned cargo), in release order. Zone owns their existence: Release is the one
    // adder; StepFloatingItems (expiry) and TryPickUp are the only removers. TryHold, Free and Remove-of-the-holder
    // are the only writers of FloatingItem.Holder. Runtime state, never saved.
    public ReactiveCollection<FloatingItem> FloatingItems = new ReactiveCollection<FloatingItem>();
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
    // One flight per faction, created on its first piloted join; unaffiliated ships share Unaffiliated. A flight owns
    // what its members target and do (Agents/Flight.cs).
    private static readonly Faction Unaffiliated = new Faction();
    private readonly Dictionary<Faction, Flight> _flights = new Dictionary<Faction, Flight>();
    public IEnumerable<Flight> Flights => _flights.Values;

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
    // Contacts.Step publishes each hull-on-hull contact it resolves; presentations are the only readers.
    public Subject<ContactEvent> Contacted = new Subject<ContactEvent>();
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

    private readonly List<Departure> _departures = new List<Departure>();
    internal IReadOnlyList<Departure> Departures => _departures;

    public float Time
    {
        get => (float) _time;
    }

    // Zone time at full precision, for the index's orbital keys, which must agree with ChunkPose's own.
    public double ExactTime => _time;
    public ZonePack Pack { get; }
    public GalaxyZone GalaxyZone { get; }
    public Galaxy Galaxy { get; }

    // Cut 6b (docs/fire-control-cut.md, 6.1, Soul finding 6): one zone, one stable identity -- a galaxy seed
    // reproduces it, and nothing else in the zone (an NPC joining, a shop opening, a wormhole exit) can
    // perturb it. FireControl.Commit builds its own local generator from this and the shot's own id instead
    // of touching a shared stream.
    public uint CombatSeed { get; }

    // The zone's drop dice (loot and mine scatter): CombatSeed mixed with its run's seed (ruling loot-seed-in-save), so a
    // new game drops differently and a reloaded save repeats. Mix(0) is 0, so a zone with no galaxy rolls on CombatSeed.
    public uint DropSeed { get; }

    // The one source of floating item and mine ids: zone-scoped, from 1, never reused. Runtime state, not saved.
    private uint _lastBodyId;
    public FloatingBodyId NextBodyId() => new FloatingBodyId(++_lastBodyId);

    // The death ordinal of the loot dice: the nth death in this zone rolls on SimulationDice.For(DropSeed, LootStream, n).
    private uint _deaths;

    // The zone-scoped agent ordinal, from 1, never reused: the key of a pilot's own dice.
    private uint _lastAgentId;
    public uint NextAgentId() => ++_lastAgentId;

    // One death subscription per member entity: Admit creates it together with the membership, Remove disposes it
    // together with the membership, and nothing else touches either.
    private readonly Dictionary<Entity, IDisposable> _deathWatches = new Dictionary<Entity, IDisposable>();

    public Zone(ItemManager itemManager, PlanetSettings settings, ZonePack pack, GalaxyZone galaxyZone, Galaxy galaxy)
    {
        Entities = new MemberView(_entities);
        _time = pack.Time;
        GalaxyZone = galaxyZone;
        Galaxy = galaxy;
        Pack = pack;
        _itemManager = itemManager;
        Settings = settings;
        _orbitPositionAt = OrbitPositionAt;
        CombatSeed = galaxyZone?.Name.StableHash() ?? 1337u;
        DropSeed = CombatSeed ^ SimulationDice.Mix(galaxy?.Seed ?? 0u);
        var cache = itemManager.ItemData;

        // Cut 5, 5.6 (docs/fire-control-cut.md, Soul finding 7; operator ruling Q3): death removes the ship,
        // in the simulation, not only in Unity's own loot-drop subscription. One subscription point covers
        // every join, whichever caller admits the entity (deserialization below, a jump, a spawned turret) --
        // ObserveAdd fires for all of them. Forbidden writer: no presentation may remove an entity from Zone.
        // The death watch lives exactly as long as the entity's membership in this zone: installed on admission,
        // disposed on removal (death, dock, jump), so an entity admitted again never holds a second one.
        // The one record of a removal for observers (ruling reveal-ends-on-perception): read by the next Sense pass.
        _entities.ObserveRemove().Subscribe(remove => _departures.Add(new Departure(remove.Value, remove.Value.Position.xz,
            remove.Value.Visibility)));

        // Leaving Entities, by death, docking or a transfer, is leaving the zone's pilots: the agent and its flight
        // membership go with the ship, so nothing keeps flying a hull that is not here.
        Entities.ObserveRemove().Subscribe(remove =>
        {
            var agent = Agents.FirstOrDefault(a => a.Ship == remove.Value);
            if (agent == null) return;
            Agents.Remove(agent);
            foreach (var flight in _flights.Values) flight.Leave(agent);
        });

        Targets.Add(new EntityTargets());

        foreach (var orbit in pack.Orbits)
        {
            Orbits.Add(orbit.Key, new Orbit(cache.Get(orbit)));
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

        // Orbits, bodies and belts exist: the field's terms are built once, and every rail's period is derived from them.
        var wellMass = new Dictionary<CultRecordKey, float>();
        foreach (var body in PlanetInstances)
            if (body.Value.WellStrength > 0)
                wellMass.TryAdd(Planets[body.Key].Orbit.Key, body.Value.BodyData.Mass);
        var beltExtent = new Dictionary<CultRecordKey, float>();
        foreach (var belt in AsteroidBelts)
        {
            var rail = Planets[belt.Key].Orbit.Key;
            beltExtent[rail] = AsteroidBelt.Extent(belt.Value.Data, Orbits[rail].Data.Distance);
        }
        var wellNodes = new List<WellNode>();
        foreach (var orbit in Orbits)
        {
            var data = orbit.Value.Data;
            wellNodes.Add(new WellNode(orbit.Key, data.Parent.Key, data.Distance, data.Phase,
                wellMass.TryGetValue(orbit.Key, out var mass) ? mass : 0f,
                beltExtent.TryGetValue(orbit.Key, out var reach) ? reach : data.Distance,
                    data.FixedPosition + OrbitData.Evaluate(frac(data.Phase)) * data.Distance)); // a root's stationary place: Offset at an infinite period
        }
        Wells = WellTree.Build(wellNodes, settings, pack.Radius);
        foreach (var orbit in Orbits)
            orbit.Value.Period = orbit.Value.Data.Parent.IsSet()
                ? PeriodAround(orbit.Value.Data.Parent.Key, orbit.Value.Data.Distance, orbit.Key)
                : float.PositiveInfinity;
        foreach (var belt in AsteroidBelts)
        {
            var beltOrbit = Planets[belt.Key].Orbit.Key;
            var beltParent = Orbits[beltOrbit].Data.Parent.Key;
            for (var i = 0; i < belt.Value.Periods.Length; i++)
                belt.Value.Periods[i] = belt.Value.Data.RidesRail
                    ? Orbits[beltOrbit].Period // a swarm's rocks ride their rail (trojans-ride-host-period-with-stations)
                    : PeriodAround(beltParent, belt.Value.Data.Asteroids[i].Distance, beltOrbit);
        }

        PopulatedRadius = ComputePopulatedRadius();

        // Aetheria gravity (target invariant zone-valid-before-tick): every orbit is placed at the pack's time
        // when the zone is built, so each well, light and body pose read before the first Update is where the
        // body is. Velocity stays zero until the first Update differences two placed positions.
        foreach (var orbit in Orbits)
            GetOrbitPosition(orbit.Key);

        // BeltTargets copies the belts' periods, so it is built after them.
        foreach (var belt in AsteroidBelts)
            Targets.Add(new BeltTargets(this, belt.Key, belt.Value.Data, settings, _time));

        // Cut 2 (docs/mining-cut.md): read after belts are built so a later ChunkExists/ChunkRadius call has
        // Planets/AsteroidBelts populated. A pack from before key 6 existed has ChunkWear == null (nullable
        // persistence rule); that reads as no wear rather than throwing.
        if (pack.ChunkWear != null)
            foreach (var wear in pack.ChunkWear)
                _wear[new ChunkId(wear.Field, wear.Index)] = new ChunkWear { Damage = wear.Damage, BrokenUntil = wear.BrokenUntil };

        var ordinal = 0u;
        foreach (var entityPack in pack.Entities)
        {
            ordinal++;
            var entity = EntitySerializer.Unpack(_itemManager, this, entityPack);
            var ship = entity as Ship;
            Admit(entity, piloted: ship is {IsPlayerShip: false});
            // Generation placement, not admission: a ship packed at the origin is scattered across the zone, on the
            // zone's own dice, so nothing drawn from the item stream can move it.
            if (ship is {IsPlayerShip: false} && lengthsq(ship.Position) < 1)
                ship.Position = SimulationDice.For(CombatSeed, SimulationDice.ScatterStream, ordinal)
                    .NextFloat3(float3(-pack.Radius * .5f), float3(pack.Radius * .5f));
        }

        // TODO: Associate planets with stored entities for planetary colonies
    }

    // The one admission into this zone (docs/scenarios-cut.md, 2.2): the only writer of membership and the only
    // creator of agents. Construction, new-run staging (RunStart), warp arrival, undock and the console's spawned
    // turret all join through here. A piloted entity gets the zone's Minion; an unpiloted one gets no agent at all.
    // Only a ship can be piloted. The death watch is installed here, for every join, whichever caller admits it.
    // An entity that is already a member is refused: it would hold a second watch and a second agent.
    // A piloted entity starts on the zone's drawn patrol circuit unless the caller states its task (a scenario's approach).
    public void Admit(Entity entity, bool piloted, AgentTask task = null)
    {
        if (_entities.Contains(entity)) throw new InvalidOperationException("entity: already a member of this zone");
        _entities.Add(entity);
        _deathWatches.Add(entity, entity.Death.Subscribe(_ => { DropLoot(entity); Remove(entity); entity.Deactivate(); }));
        entity.Activate();
        if (piloted) Agents.Add(CreateAgent((Ship) entity, task));
    }

    // The one removal from this zone (death, dock, jump): membership and its death watch leave together. False, and
    // nothing changes, when the entity is not a member. The caller deactivates; Remove does not.
    public bool Remove(Entity entity)
    {
        if (!_entities.Remove(entity)) return false;
        _deathWatches.Remove(entity, out var watch);
        watch.Dispose();
        foreach (var floating in FloatingItems)
            if (ReferenceEquals(floating.Holder, entity)) Free(floating.Id);
        return true;
    }

    private Agent CreateAgent(Ship ship, AgentTask stated)
    {
        var key = ship.Faction ?? Unaffiliated;
        if (!_flights.TryGetValue(key, out var flight))
            _flights[key] = flight = new Flight(ship.Faction?.Doctrine, _itemManager.GameplaySettings);
        var id = NextAgentId();
        var agent = new Minion(ship, flight.Combatant) { Id = id };
        flight.Join(agent, stated ?? PatrolCircuit(id));
        return agent;
    }

    private PatrolOrbitsTask PatrolCircuit(uint id)
    {
        var dice = SimulationDice.For(CombatSeed, SimulationDice.PatrolStream, id);
        return new PatrolOrbitsTask { Circuit = Orbits.OrderBy(_ => dice.NextFloat()).Take(4).Select(x => x.Key).ToArray() };
    }

    public ZonePack PackZone()
    {
        return new ZonePack
        {
            Radius = Pack.Radius,
            Mass = Pack.Mass,
            Entities = _entities.Select(EntitySerializer.Pack).ToList(),
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

    public void Update(float deltaTime)
    {
        _time += deltaTime;
        _updatedOrbits.Clear();
        // Every previous position is taken before any orbit is placed: GetOrbitPosition places a parent as a
        // side effect of placing its child, so differencing inside one loop would depend on dictionary order.
        foreach (var orbit in Orbits)
            orbit.Value.PreviousPosition = orbit.Value.Position;
        foreach (var orbit in Orbits)
        {
            orbit.Value.Position = GetOrbitPosition(orbit.Key);
            orbit.Value.Velocity = (orbit.Value.Position - orbit.Value.PreviousPosition) / deltaTime;
        }

        foreach (var flight in _flights.Values) flight.Update(deltaTime);
        // Every pilot's command is applied by PilotApplier, once per step. Entities with no agent (the player, a scripted
        // pilot, a lone ship) first, as the player's input lands before the step; then each agent applies right after it
        // decides, so the next agent sees it as it saw the direct writes this replaced.
        var agents = Agents.ToArray();
        var piloted = new HashSet<Entity>();
        foreach (var agent in agents) piloted.Add(agent.Ship);
        foreach (var entity in Entities.ToArray())
            if (!piloted.Contains(entity)) PilotApplier.Apply(entity);
        foreach (var agent in agents)
        {
            agent.Update(deltaTime);
            PilotApplier.Apply(agent.Ship);
        }

        var entities = Entities.ToArray();
        foreach (var entity in entities) entity.PublishInbox();
        foreach (var entity in entities) entity.Update(deltaTime);
        foreach (var entity in entities) entity.Sense();
        _departures.Clear();

        CargoTransfer.Step(this, deltaTime);
        StepMines(deltaTime);
        StepClouds(deltaTime);
        StepFloatingItems(deltaTime);

        // Hull contact (cut sim-contact): after every entity has moved and the mines and clouds have taken
        // their toll, so only hulls still in the zone touch, and before shots resolve against the result.
        Contacts.Step(this);

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

    // The one adder of floating items: death drops and jettison both come here. The item drifts as a KinematicBody
    // launched at `velocity` and expires after FloatingItemLifetime.
    public FloatingItem Release(ItemInstance item, float2 position, float2 velocity)
    {
        var floating = new FloatingItem
        {
            Id = NextBodyId(), Item = item, Body = new KinematicBody { Position = position, Velocity = velocity },
            ReleasedAt = _time, Lifetime = _itemManager.GameplaySettings.FloatingItemLifetime
        };
        FloatingItems.Add(floating);
        return floating;
    }

    // The one commit of a pickup: the first of the taker's cargo bays that accepts the item stores it and the item
    // stops floating. Otherwise nothing changes.
    // An item held by another entity is refused: one holder per item holds at the commit as well.
    public bool TryPickUp(Entity taker, FloatingBodyId id)
    {
        var floating = FloatingItems.FirstOrDefault(f => f.Id.Equals(id));
        if (floating == null || (floating.Holder != null && !ReferenceEquals(floating.Holder, taker))) return false;
        if (!taker.CargoBays.Any(bay => bay.TryStore(floating.Item))) return false;
        FloatingItems.Remove(floating);
        return true;
    }

    // The tractor beam's claim on a floating item: false if it is absent or already held. The item stays where it is
    // until its pull begins; StepFloatingItems places it from then on.
    public bool TryHold(Entity holder, FloatingBodyId id)
    {
        var floating = FloatingItems.FirstOrDefault(f => f.Id.Equals(id));
        if (floating == null || floating.Holder != null) return false;
        floating.Holder = holder;
        floating.HeldFrom = floating.Body.Position;
        floating.HeldProgress = 0;
        return true;
    }

    // How far along its pull to the holder a held item is, 0..1.
    public void SetHeldProgress(FloatingBodyId id, float progress)
    {
        var floating = FloatingItems.FirstOrDefault(f => f.Id.Equals(id));
        if (floating?.Holder != null) floating.HeldProgress = saturate(progress);
    }

    // Lets a held item go: it floats on from where it is with the Velocity and Drift it had.
    public void Free(FloatingBodyId id)
    {
        var floating = FloatingItems.FirstOrDefault(f => f.Id.Equals(id));
        if (floating == null) return;
        floating.Holder = null;
        floating.HeldProgress = 0;
    }

    // Called from the death subscription, before the entity leaves the zone: one generator per death. Each equipped
    // item but the hull drops on a LootDropProbability draw; every cargo stack drops whole. Each drop leaves from the
    // dead entity's position at a random heading. Draw order: Equipment order (the probability draw, then the heading
    // of a dropped item), then each cargo bay's stacks in CargoBays order (a heading each).
    private void DropLoot(Entity dead)
    {
        var settings = _itemManager.GameplaySettings;
        var random = SimulationDice.For(DropSeed, SimulationDice.LootStream, ++_deaths);
        var at = new float2(dead.Position.x, dead.Position.z);
        float2 Heading() { var angle = random.NextFloat() * 2f * PI; return float2(cos(angle), sin(angle)); }
        foreach (var equipped in dead.Equipment.ToArray())
        {
            if (equipped == dead.EquippedHull || !(random.NextFloat() < settings.LootDropProbability)) continue;
            Release(equipped.EquippableItem, at, Heading() * settings.LootDropVelocity);
        }
        foreach (var bay in dead.CargoBays.ToArray())
            foreach (var stack in bay.Cargo.Keys.ToArray())
                Release(stack, at, Heading() * settings.LootDropVelocity);
    }

    // After StepClouds and before FireControl.Step: each floating item drifts as a KinematicBody on the zone's force
    // and is removed once its lifetime has run out. A held item is neither integrated nor expired: it is placed
    // between where it was claimed and its holder, now that the entities have moved.
    // The curve a held item rides from where it was claimed to its holder: the one owner, which the test scene's
    // stand-in sim and tendril call rather than spell again.
    public static float HeldPull(float progress) => smoothstep(0f, 1f, progress);

    private void StepFloatingItems(float deltaTime)
    {
        var settings = _itemManager.GameplaySettings;
        foreach (var floating in FloatingItems.ToArray())
        {
            if (floating.Holder != null)
            {
                floating.Body.Position = lerp(floating.HeldFrom, floating.Holder.Position.xz, HeldPull(floating.HeldProgress));
                continue;
            }
            floating.Body.Step(deltaTime, GetForce(floating.Body.Position), settings);
            if (floating.ReleasedAt + floating.Lifetime <= _time) FloatingItems.Remove(floating);
        }
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
            Orbits[orbitID].Position = RailAt(orbitID, _time);
            _updatedOrbits.Add(orbitID);
        }

        return Orbits[orbitID].Position;
    }

    // The rail law, once: where an orbit sits from its parent's node at a time. An infinite period (a parentless orbit,
    // or one with no field to circle in) leaves the orbit at its phase: frac(t / inf) is 0.
    private static float2 Offset(Orbit orbit, double time)
    {
        var pos = float2.zero;
        if (orbit.Period > .01f)
        {
            var phase = (float) frac(time / orbit.Period);
            pos = OrbitData.Evaluate(frac(phase + orbit.Data.Phase)) * orbit.Data.Distance;

            if (float.IsNaN(pos.x))
                pos = float2.zero;
        }

        return pos;
    }

    // The same rail law at any time, with no memo read or written: a parent is its own rail at that time.
    private float2 RailAt(CultRecordKey orbitID, double time)
    {
        if (!orbitID.IsSet())
            return float2.zero;
        if (!Orbits.TryGetValue(orbitID, out var orbit))
        {
            Log?.Invoke("Requested orbit is not part of this zone!");
            return float2.zero;
        }
        var parentPosition = !orbit.Data.Parent.IsSet() ? orbit.Data.FixedPosition : RailAt(orbit.Data.Parent.Key, time);
        return parentPosition + Offset(orbit, time);
    }

    // Where an orbit is at a time: pure, writes nothing. At the step's own time it is the step's memo (GetOrbitPosition);
    // at any other time (a presenter's drawn time) it is the rail law evaluated there.
    public float2 OrbitPositionAt(CultRecordKey orbitID, double time)
    {
        if (!orbitID.IsSet())
            return float2.zero;
        if (!Orbits.ContainsKey(orbitID))
        {
            Log?.Invoke("Requested orbit is not part of this zone!");
            return float2.zero;
        }

        return time == _time ? GetOrbitPosition(orbitID) : RailAt(orbitID, time);
    }

    /// <summary>The zone's gravity: the terms of its orbit tree (WellTree owns which exist and the walk that sums them).</summary>
    public WellTree Wells { get; }

    private readonly Func<CultRecordKey, double, float2> _orbitPositionAt;

    // The one owner of a rail's period (Orbit.Period, AsteroidBelt.Periods): 2 pi sqrt(d / a), a being the pull the field
    // gives at distance d from the node being orbited (WellTree.PullAround). No pull gives an infinite period.
    private float PeriodAround(CultRecordKey parentOrbit, float distance, CultRecordKey self)
    {
        var a = Wells.PullAround(parentOrbit, distance, self);
        return a > 0 && distance > 0 ? 2 * PI * sqrt(distance / a) : float.PositiveInfinity;
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
    public float4 ChunkPose(CultRecordKey belt, int index) => ChunkPose(belt, index, _time);

    // The same pose at any time: the sim reads it at _time, a presenter at the drawn time (DrawAhead.Time).
    public float4 ChunkPose(CultRecordKey belt, int index, double time)
    {
        var beltData = Planets[belt] as AsteroidBeltData;
        var parentPosition = OrbitPositionAt(Orbits[beltData.Orbit.Key].Data.Parent.Key, time);
        var size = ChunkRadius(new ChunkId(belt, index));
        return AsteroidBelts[belt].Pose(index, time, parentPosition, size);
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

    public void EvaluateBelt(CultRecordKey belt, Span<float4> into, double time)
    {
        var beltData = Planets[belt] as AsteroidBeltData;
        var parentPosition = OrbitPositionAt(Orbits[beltData.Orbit.Key].Data.Parent.Key, time);
        var beltInstance = AsteroidBelts[belt];
        for (var i = 0; i < beltData.Asteroids.Length; i++)
            into[i] = beltInstance.Pose(i, time, parentPosition, ChunkRadius(new ChunkId(belt, i)));
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

    // The extent of the populated area, from the zone's origin: owned here; presenters read it, nothing authors it. A static
    // bound on where anything can be, so it holds at every time and reads nothing the pack's Radius decides: for each body,
    // its orbit chain's reach plus its Roche limit; for each belt, the chain's reach to the orbit its asteroids circle (the
    // belt orbit's parent) plus the belt's own Radius, its largest asteroid Distance.
    public float PopulatedRadius { get; }

    private float ComputePopulatedRadius()
    {
        var radius = 0f;
        foreach (var body in PlanetInstances.Values)
            radius = max(radius, ChainReach(body.BodyData.Orbit.Key) + body.RocheLimit);
        foreach (var belt in AsteroidBelts)
            radius = max(radius, ChainReach(Orbits[Planets[belt.Key].Orbit.Key].Data.Parent.Key) + belt.Value.Radius);
        return radius;
    }

    // The most an orbit can lie from the origin: the root's offset plus every orbit Distance down the chain to it.
    private float ChainReach(CultRecordKey key)
    {
        var reach = 0f;
        while (key.IsSet() && Orbits.TryGetValue(key, out var orbit))
        {
            if (orbit.Data.Parent.IsSet()) reach += orbit.Data.Distance;
            else reach += length(orbit.Data.FixedPosition);
            key = orbit.Data.Parent.Key;
        }
        return reach;
    }

    public float GetHeight(float2 position) => GetHeight(position, _time);

    // The field at a time: each term sits where its node's rail puts it then. The sim reads it at _time, a presenter at
    // the drawn time, so a ship rides the same slope the drawn planet's well makes.
    public float GetHeight(float2 position, double time) => Wells.Potential(position, _orbitPositionAt, time) / Settings.HeightExaggeration;

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

    // The one source of gravitational acceleration (u/s^2): the walk of the zone's terms (WellTree). Ships, floating items,
    // clouds and mines all read it, and so does Flow. It is the gradient of the potential GetHeight draws, so the two
    // cannot disagree.
    public float2 GetForce(float2 position) => Wells.Force(position, _orbitPositionAt, _time);

    // The global flow velocity at `position` in zone time, in u/s (FlowField is the law). The slope term reads the
    // downhill direction, which is the force.
    public float3 Flow(float3 position) =>
        FlowField.At(Settings.Flow, position, FlowScroll, GetForce(position.xz));

    // The flow's noise scroll at zone time (Zone.Flow evaluates at exactly this); lowered to the shaders' _FlowScroll.
    public float2 FlowScroll => FlowField.Scroll(Settings.Flow, _time);

    // The phase of a cycle of `period` seconds at zone time, for presenters that animate on the flow.
    public float CyclePhase(float period) => FlowField.Cycle(_time, period);

    public static float PowerPulse(float x, float exponent)
    {
        x *= 2;
        x = clamp(x, -1, 1);
        return pow((x + 1) * (1 - x), exponent);
    }

    // The surface normal of the drawn height (potential / K), derived from the force. Display only (ship tilt): the
    // sim never reads it as a force.
    public float3 GetNormal(float2 pos)
    {
        var slope = GetForce(pos) / Settings.HeightExaggeration;
        return normalize(new float3(slope.x, 1f, slope.y));
    }
}

public class Planet
{
    public Orbit Orbit;
    protected readonly PlanetSettings Settings;
    public BodyData BodyData;
    public float BodyRadius;

    // The well (GravityField): strength c by mass, and the Roche limit, which is both the closest a child may orbit and
    // the well's softened core radius R (one curve of mass, never per body). Both are authored PlanetSettings curves.
    public float WellStrength;
    public float RocheLimit;

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
        (WellStrength, RocheLimit) = WellTree.Curves(Settings, BodyData.Mass);
    }
}

public class GasGiant : Planet
{
    public GasGiantData GasGiantData;

    public GasGiant(PlanetSettings settings, GasGiantData data, Orbit orbit) : base(settings, data, orbit)
    {
        GasGiantData = data;
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

    // Each asteroid's orbital period, in seconds: derived once at zone build by Zone.PeriodAround (the field at the
    // asteroid's own distance from the belt's parent), the only period Pose and BeltTargets divide by.
    public float[] Periods { get; }

    public AsteroidBelt(AsteroidBeltData data)
    {
        Data = data;
        Periods = new float[data.Asteroids.Length];
        Radius = data.Asteroids.Max(a => a.Distance);
    }

    // What the belt's rail adds to its parent's L_F (WellTree): an annulus reaches its outermost rock, whose period reads the
    // field there; a swarm riding its rail reaches only the rail, whose period every rock shares. The one belt extent rule:
    // Zone, ZoneGenerator's clearance tree and the station tests all read it.
    public static float Extent(AsteroidBeltData data, float railDistance) =>
        data.RidesRail ? railDistance : Math.Max(data.Asteroids.Max(a => a.Distance), railDistance);

    // Cut 1 (docs/mining-cut.md): the formula of Zone.cs:269-271 verbatim, `time` in double as `_time` was used.
    // The only place a chunk pose is computed; no per-tick copy survives it. Cut 2: size is no longer read from
    // per-belt storage -- Zone owns wear now, so the caller (Zone.ChunkPose/EvaluateBelt) computes it through
    // Zone.ChunkRadius and passes it in.
    public float4 Pose(int index, double time, float2 parentPosition, float size)
    {
        var asteroid = Data.Asteroids[index];
        var rot = (float) (time * asteroid.RotationSpeed % (PI * 2));
        var pos = OrbitData.Evaluate((float) frac(time / Periods[index] + asteroid.Phase)) * asteroid.Distance + parentPosition;
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

    // Seconds per turn, written only by the Zone constructor (Zone.PeriodAround); infinite when the orbit has no
    // field to circle in.
    public float Period { get; internal set; }

    public Orbit(OrbitData data)
    {
        Data = data;
    }
}
