/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// The tractor beam in the sim (aetheria-release cut loot-2): a Pickup behaviour spins its beam up in sim time and runs
// one timed grab of a floating item, ending in Zone.TryPickUp. Rulings: jettison-shared-floating-items,
// unity-presents-only-r3, sim-speed-presentation; the question loot-grab-release-cancels, built hold-required. Every
// grab here is run by Zone.Update ticking a ship's own gear: nothing calls the behaviour directly.
public sealed class PickupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-pickup-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public PickupTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var c in _caches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static Shape Solid(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    private static PickupData Beam(float extend = .3f, float envelop = .2f, float pull = .5f) => new PickupData
    {
        Range = Constant(75), BeamRadius = Constant(25), GrabPower = Constant(.5f), SpinRate = Constant(2),
        ExtendTime = Constant(extend), EnvelopTime = Constant(envelop), PullTime = Constant(pull)
    };

    private sealed class Lab
    {
        public ItemManager Items;
        public Zone Zone;
        public int Lot = 1;

        public EquippableItem Make(string name) => new EquippableItem
        {
            Data = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<EquippableItemData>(name)), Durability = 1, Lot = Lot++
        };

        public SimpleCommodity Ore(int quantity) => new SimpleCommodity
        {
            Data = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<SimpleCommodityData>("Ore")), Quantity = quantity
        };
    }

    // With `wells`, the zone has a sun (a parentless rail off the ship's side, strength 400) and a planet on a moving rail round
    // it (strength 25), the field a floater layer is read against; without, the zone is flat and nothing pulls on anything.
    private Lab Build(PickupData beam, float lifetime = 30f, bool wells = false)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 1f,
            UnaidedAccuracy = .05f, UnaidedTracking = 10f, UnaidedPrecision = 2f, AgentMinHitProbability = 0f,
            LootDropProbability = 0, LootDropVelocity = 25, FloatingItemLifetime = lifetime,
            ShipLayerHeight = 3f, FloaterLayerHeight = -1.5f
        };
        var id = Guid.NewGuid().ToString("N");
        var cache = AetheriaStores.Open(Path.Combine(_root, id + ".cc"), Path.Combine(_root, id + "-run.cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = Solid(5, 5), Durability = 1000000, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Tractor", Hardpoint = HardpointType.Tool, Shape = Solid(1, 1), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { beam }
        });
        cache.Upsert(new GearData
        {
            Name = "Spare", Hardpoint = HardpointType.Tool, Shape = Solid(1, 1), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.Upsert(new GearData
        {
            Name = "Block", Hardpoint = HardpointType.Tool, Shape = Solid(2, 2), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = Solid(4, 4), Durability = 4 });
        cache.Upsert(new SimpleCommodityData { Name = "Ore", Shape = Solid(1, 1), MaxStack = 100 });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 9000; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var pack = new ZonePack { Radius = 0 };
        var curves = new PlanetSettings();
        if (wells)
        {
            curves = new PlanetSettings
            {
                BodyRadius = new ExponentialCurve(),
                WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
                RocheLimit = new ExponentialCurve { Constant = 2f }
            };
            var sunOrbit = cache.Upsert(new OrbitData { FixedPosition = float2(-50, 0) }).Key;
            var planetOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(sunOrbit), Distance = 90f, Phase = .9f }).Key;
            pack.Radius = 5000f;
            pack.Mass = 10000f;
            pack.Orbits.Add(new CultRecordRef<OrbitData>(sunOrbit));
            pack.Orbits.Add(new CultRecordRef<OrbitData>(planetOrbit));
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Name = "Sun", Orbit = new CultRecordRef<OrbitData>(sunOrbit), Mass = 400f }).Key));
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Name = "Planet", Orbit = new CultRecordRef<OrbitData>(planetOrbit), Mass = 25f }).Key));
        }
        var zone = new Zone(items, curves, pack, new GalaxyZone { Name = "Pickup", Owner = null }, null);
        return new Lab { Items = items, Zone = zone };
    }

    // A ship with `bays` cargo bays (bays are equipped before the zone admits the ship), aimed along +x and
    // (unless told otherwise) carrying the tractor beam.
    private static Ship Spawn(Lab lab, float3 at, bool beam = true, int bays = 1)
    {
        var hull = lab.Items.ItemData.RefOf<ItemData>(lab.Items.ItemData.GetByName<HullData>("Skiff"));
        var ship = new Ship(lab.Items, lab.Zone, new EquippableItem { Data = hull, Durability = 1000000, Lot = lab.Lot++ }, new EntitySettings());
        for (var i = 0; i < bays; i++) Equip(ship, lab.Make("Crate"));
        if (beam) Equip(ship, lab.Make("Tractor"));
        lab.Zone.Admit(ship, piloted: false);
        ship.Position = at;
        ship.Aim = float3(1, 0, 0);
        return ship;
    }

    private static void Equip(Ship ship, EquippableItem gear)
    {
        Assert.True(ship.TryFindSpace(gear, out var pos));
        Assert.True(ship.TryEquip(gear, pos));
    }

    private static FloatingItem Drift(Lab lab, float x, float y, float2 velocity = default, int ore = 5) =>
        lab.Zone.Release(lab.Ore(ore), float2(x, y), velocity);

    private static List<GrabEvent> Watch(Entity ship, CompositeDisposable bag)
    {
        var events = new List<GrabEvent>();
        ship.Capabilities.Grab.Subscribe(events.Add).AddTo(bag);
        return events;
    }

    private static void Tick(Lab lab, int ticks, float dt)
    {
        for (var i = 0; i < ticks; i++) lab.Zone.Update(dt);
    }

    // Ticks until the condition holds, for at most twenty sim seconds; a grab that never comes fails here instead of hanging.
    private static void Until(Lab lab, float dt, Func<bool> done)
    {
        for (var tick = 0; tick < 1200 && !done(); tick++) lab.Zone.Update(dt);
        Assert.True(done(), "the expected moment never came");
    }

    private static void Kill(Entity e) => e.HeatstrokeDeath.OnNext(Unit.Default);

    [Fact]
    public void NoPickupBehaviourNoGrab()
    {
        using var bag = new CompositeDisposable();

        // The same ship and item, with and without the gear: only the gear grabs.
        var bare = Build(Beam());
        var plain = Spawn(bare, float3(0, 0, 0), beam: false);
        var left = Drift(bare, 40, 0);
        var plainEvents = Watch(plain, bag);
        plain.TractorCommand = 1;
        Tick(bare, 240, 1 / 60f);
        Assert.True(left.Holder == null, "left is held");
        Assert.True(bare.Zone.FloatingItems.Contains(left), "left left the zone");
        Assert.Equal(0f, plain.TractorPower);
        Assert.Empty(plainEvents);

        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 60, 1 / 60f);
        Assert.True(ReferenceEquals(ship, item.Holder), "item is not held by ship");
        Assert.Equal(GrabPhase.Started, events.First().Phase);
        Assert.True(ship.TractorPower > 0f);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(600)]
    public void TheBeamSpinsUpInSimTime(int ticksPerSecond)
    {
        var dt = 1f / ticksPerSecond;
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0);

        // Power rises two per sim second and not faster, at any tick rate; the claim comes on the tick it passes GrabPower.
        ship.TractorCommand = 1;
        int claim = -1, full = -1;
        var before = 0f;
        for (var tick = 1; tick <= ticksPerSecond; tick++)
        {
            lab.Zone.Update(dt);
            if (claim < 0 && item.Holder != null)
            {
                claim = tick;
                Assert.True(ship.TractorPower >= .5f);
                Assert.True(before < .5f);
            }
            if (full < 0 && ship.TractorPower >= 1f) full = tick;
            before = ship.TractorPower;
        }
        Assert.Equal(ticksPerSecond / 4, claim);
        Assert.Equal(ticksPerSecond / 2, full);

        // Command 0 spins it down at the same rate.
        ship.TractorCommand = 0;
        var empty = -1;
        for (var tick = 1; tick <= ticksPerSecond; tick++)
        {
            lab.Zone.Update(dt);
            if (empty < 0 && ship.TractorPower <= 0f) empty = tick;
        }
        Assert.Equal(ticksPerSecond / 2, empty);
    }

    [Theory]
    [InlineData(.3f, .2f, .5f, 60)]
    [InlineData(1f, 1f, 1f, 180)]
    public void AGrabTakesExactlyItsPhasesThenCommits(float extend, float envelop, float pull, int ticksAfterClaim)
    {
        foreach (var rate in new[] { 60, 600 })
        {
            var dt = 1f / rate;
            var lab = Build(Beam(extend, envelop, pull));
            var ship = Spawn(lab, float3(0, 0, 0));
            var item = Drift(lab, 40, 0);
            ship.TractorCommand = 1;
            int claim = -1, commit = -1;
            for (var tick = 1; tick <= rate * 6 && commit < 0; tick++)
            {
                lab.Zone.Update(dt);
                if (claim < 0 && item.Holder != null) claim = tick;
                if (!lab.Zone.FloatingItems.Contains(item)) commit = tick;
            }
            Assert.True(claim > 0);
            Assert.Equal(ticksAfterClaim * rate / 60, commit - claim);
            Assert.Equal(5, ship.CargoBays.Single().Cargo.Keys.OfType<SimpleCommodity>().Single().Quantity);
        }
    }

    // The beam ends on the loot where the loot is drawn: the field plus the floater layer, not the grabbing ship's plane
    // (ruling orbitals-above-floaters-below).
    [Fact]
    public void AGrabPointRidesTheFloaterLayerNotTheShips()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam(), wells: true);
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0);

        // The fixture has a field to ride: it differs between the ship's point and the item's, and between the start and
        // the end of the run, so a grab point read at the ship's xz or at the wrong time cannot agree with it.
        var atShip = lab.Zone.GetHeight(float2(0, 0), lab.Zone.ExactTime);
        var atItem = lab.Zone.GetHeight(float2(40, 0), lab.Zone.ExactTime);
        Assert.True(abs(atItem - atShip) > 1f, $"the field is flat under the grab: ship {atShip}, item {atItem}");
        var start = lab.Zone.ExactTime;

        // Each event is checked at the zone time it was published, where the sim read the field.
        var seen = new List<(GrabEvent Event, double Time)>();
        ship.Capabilities.Grab.Subscribe(e => seen.Add((e, lab.Zone.ExactTime))).AddTo(bag);
        ship.TractorCommand = 1;
        for (var tick = 1; tick <= 150; tick++) lab.Zone.Update(1 / 60f);
        Assert.NotEmpty(seen);
        Assert.True(abs(lab.Zone.GetHeight(float2(40, 0), lab.Zone.ExactTime) - lab.Zone.GetHeight(float2(40, 0), start)) > .01f, "the wells did not move");
        Assert.All(seen, s => Assert.Equal(lab.Zone.GetHeight(s.Event.TargetPosition.xz, s.Time) - 1.5f, s.Event.TargetPosition.y, 3));
        Assert.All(seen, s => Assert.True(abs(s.Event.TargetPosition.y - (-1.5f)) > 1f, "the grab point sits on the bare layer, the field dropped"));
    }


    [Fact]
    public void GrabEventsNameTheFloatingItem()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var other = Drift(lab, 40, 20);
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        for (var tick = 1; tick <= 150; tick++) lab.Zone.Update(1 / 60f);
        Assert.Equal(new[] { GrabPhase.Started, GrabPhase.Extend, GrabPhase.Envelop, GrabPhase.Pull, GrabPhase.Completed },
            events.Take(5).Select(e => e.Phase).ToArray());
        Assert.All(events.Take(5), e => Assert.Equal(item.Id, e.Target));
        Assert.NotEqual(other.Id, item.Id);
        Assert.Equal(.3f, events[1].PhaseDuration, 4);
        Assert.Equal(.2f, events[2].PhaseDuration, 4);
        Assert.Equal(.5f, events[3].PhaseDuration, 4);
    }

    // Each phase is published on the first tick at or after its boundary (.3, .5 and 1 s after the claim), at any
    // tick rate or an uneven one. The clock is a float sum, so a boundary an exact number of ticks away can land one
    // tick late (Envelop and Pull do at 1/600): the rule is "not before, at most one tick after", not a tick count.
    [Theory]
    [InlineData(1, 60, 60, 60, 60)]
    [InlineData(1, 600, 600, 600, 600)]
    [InlineData(4, 60, 24, 90, 33)]
    public void EachPhaseFiresOnTheFirstTickAtOrAfterItsBoundary(int cycle, int a, int b, int c, int d)
    {
        using var bag = new CompositeDisposable();
        var rates = cycle == 1 ? new[] { a } : new[] { a, b, c, d };
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        var since = -1.0;
        var times = new List<double>();
        var steps = new List<double>();
        for (var tick = 0; tick < rates.Length * 3000 && events.Count(e => e.Phase == GrabPhase.Completed) == 0; tick++)
        {
            var dt = 1f / rates[tick % rates.Length];
            lab.Zone.Update(dt);
            if (since >= 0) since += dt;
            else if (events.Count > 0) since = 0;
            while (times.Count < events.Count) { times.Add(since); steps.Add(dt); }
        }
        var phases = events.Select(e => e.Phase).ToList();
        Assert.Equal(GrabPhase.Completed, phases.Last());
        foreach (var (phase, boundary) in new[] { (GrabPhase.Envelop, .3), (GrabPhase.Pull, .5), (GrabPhase.Completed, 1.0) })
        {
            var i = phases.IndexOf(phase);
            Assert.True(i >= 0, "the phase never came");
            Assert.True(times[i] >= boundary - 1e-5, "the phase fired before its boundary");
            // Where no tick lands exactly on a boundary (the uneven cycle) the tick before must be strictly short of it;
            // on a regular grid the float sum may leave that tick a hair short, so it may be equal.
            var slack = cycle == 1 ? 1e-5 : -1e-5;
            Assert.True(times[i] - steps[i] < boundary + slack, "the phase fired after the first tick at or past its boundary");
        }
    }

    [Fact]
    public void ReleasingTheBeamCancelsAndTheItemFloatsFree()
    {
        using var bag = new CompositeDisposable();
        const float dt = 1 / 60f;
        var lab = Build(Beam(), lifetime: 2f);
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0, float2(3, 1));
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        var tick = 0;
        while (!events.Any(e => e.Phase == GrabPhase.Pull) && tick++ < 600) lab.Zone.Update(dt);
        Assert.True(ReferenceEquals(ship, item.Holder), "item is not held by ship");
        var velocity = item.Body.Velocity;
        var drift = item.Body.Drift;

        // The key comes up during the pull: Power falls below GrabPower and the grab is dropped.
        ship.TractorCommand = 0;
        while (!events.Any(e => e.Phase == GrabPhase.Cancelled) && tick++ < 600) lab.Zone.Update(dt);
        Assert.True(tick < 110);
        Assert.DoesNotContain(events, e => e.Phase == GrabPhase.Completed);
        Assert.True(item.Holder == null, "item is held");
        Assert.True(lab.Zone.FloatingItems.Contains(item), "item left the zone");
        // (It has taken the one step of its freeing tick, so it is the held velocity less a tick's drag.)
        Assert.Equal(velocity.x, item.Body.Velocity.x, velocity.x * .01f);
        Assert.Equal(velocity.y, item.Body.Velocity.y, velocity.y * .01f);
        Assert.True(velocity.x > 1f);

        // It drifts on with that velocity and expires on the clock it was released on, not from the cancel.
        var at = item.Body.Position;
        lab.Zone.Update(dt);
        Assert.True(item.Body.Position.x > at.x);
        while (lab.Zone.FloatingItems.Contains(item) && tick++ < 600) lab.Zone.Update(dt);
        Assert.InRange(tick, 118, 122);
    }

    [Fact]
    public void AHoldThatFillsMidGrabCancelsAtTheEnd()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var bay = ship.CargoBays.Single();
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Until(lab, 1 / 60f, () => item.Holder != null);

        // The hold fills while the grab runs: the item slides in and then floats free.
        for (var i = 0; i < 16; i++) Assert.True(bay.TryStore(lab.Make("Spare")));
        var tick = 0;
        while (!events.Any(e => e.Phase == GrabPhase.Cancelled) && tick++ < 300) lab.Zone.Update(1 / 60f);
        Assert.Equal(new[] { GrabPhase.Started, GrabPhase.Extend, GrabPhase.Envelop, GrabPhase.Pull, GrabPhase.Cancelled },
            events.Select(e => e.Phase).ToArray());
        Assert.True(item.Holder == null, "item is held");
        Assert.True(lab.Zone.FloatingItems.Contains(item), "item left the zone");
        Assert.Equal(16, bay.Cargo.Count);
    }

    [Fact]
    public void AFullHoldNeverClaimsWhatItCannotStore()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var bay = ship.CargoBays.Single();
        for (var i = 0; i < 16; i++) Assert.True(bay.TryStore(lab.Make("Spare")));
        var item = Drift(lab, 40, 0, float2(3, 1));
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 300, 1 / 60f);
        Assert.Equal(1f, ship.TractorPower);
        Assert.Empty(events);
        Assert.True(item.Holder == null, "item is held");
        Assert.True(item.Body.Position.x > 40f, "item was pinned instead of drifting");
    }

    [Fact]
    public void AnItemTheHoldCannotStoreDoesNotBlockOneItCan()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var bay = ship.CargoBays.Single();
        for (var i = 0; i < 15; i++) Assert.True(bay.TryStore(lab.Make("Spare")));
        // One free cell: the 2x2 block is nearest the axis and cannot be stored; the ore beside it can.
        var block = lab.Zone.Release(lab.Make("Block"), float2(30, 0), default);
        var ore = Drift(lab, 50, 10);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 150, 1 / 60f);
        Assert.Contains(events, e => e.Phase == GrabPhase.Completed);
        Assert.All(events, e => Assert.Equal(ore.Id, e.Target));
        Assert.False(lab.Zone.FloatingItems.Contains(ore), "ore is still in the zone");
        Assert.True(lab.Zone.FloatingItems.Contains(block), "block left the zone");
        Assert.True(block.Holder == null, "block is held");
    }

    // Either bay may be the full one: a selection that read only the first bay, or only the last, fails one orientation.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TheBeamTakesFromWhicheverBayCanStore(int fullBay)
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0), bays: 2);
        Assert.Equal(2, ship.CargoBays.Count);
        var freeBay = 1 - fullBay;
        for (var i = 0; i < 16; i++) Assert.True(ship.CargoBays[fullBay].TryStore(lab.Make("Spare")));
        var ore = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 300, 1 / 60f);
        Assert.Contains(events, e => e.Phase == GrabPhase.Started && e.Target.Equals(ore.Id));
        Assert.Contains(events, e => e.Phase == GrabPhase.Completed);
        Assert.False(lab.Zone.FloatingItems.Contains(ore), "ore is still in the zone");
        Assert.Equal(16, ship.CargoBays[fullBay].Cargo.Count);
        Assert.Single(ship.CargoBays[freeBay].Cargo);
    }

    [Fact]
    public void AnItemInTheBeamsFarCornerIsCollected()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        // 74 along and 20 off the axis: inside the beam, though 76.7 from the ship, past Range as a straight distance.
        var corner = Drift(lab, 74, 20);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 150, 1 / 60f);
        Assert.DoesNotContain(events, e => e.Phase == GrabPhase.Cancelled);
        Assert.Contains(events, e => e.Phase == GrabPhase.Completed);
        Assert.False(lab.Zone.FloatingItems.Contains(corner), "corner is still in the zone");
    }

    [Fact]
    public void AnItemLeavingTheBeamBeforeThePullIsCancelled()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Until(lab, 1 / 60f, () => item.Holder != null);

        // The pilot swings the beam away while the grab extends: the item is off the axis and the grab is dropped.
        ship.Aim = float3(0, 0, 1);
        Tick(lab, 3, 1 / 60f);
        Assert.Equal(GrabPhase.Cancelled, events.Last().Phase);
        Assert.True(item.Holder == null, "item is held");
    }

    [Fact]
    public void OneHolderPerItem()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var a = Spawn(lab, float3(0, 0, 0));
        var b = Spawn(lab, float3(0, 0, 10));
        var eventsA = Watch(a, bag);
        var eventsB = Watch(b, bag);

        // One item, two beams: exactly one claims it, the other never does, and the non-holder cannot take it.
        var only = Drift(lab, 40, 5);
        a.TractorCommand = 1;
        b.TractorCommand = 1;
        Tick(lab, 20, 1 / 60f);
        Assert.Equal(1, new[] { eventsA, eventsB }.Count(e => e.Any(x => x.Phase == GrabPhase.Started)));
        var holder = only.Holder;
        var loser = ReferenceEquals(holder, a) ? b : a;
        Assert.False(lab.Zone.TryPickUp(loser, only.Id));
        Assert.False(lab.Zone.TryHold(loser, only.Id));
        Assert.True(lab.Zone.FloatingItems.Contains(only), "only left the zone");
        Assert.Empty(loser.CargoBays.Single().Cargo);
        Tick(lab, 120, 1 / 60f);
        Assert.False(lab.Zone.FloatingItems.Contains(only), "only is still in the zone");
        Assert.Single(holder.CargoBays.Single().Cargo);
        Assert.Empty(loser.CargoBays.Single().Cargo);

        // Two items, each nearest a different beam's axis: each ship holds its own.
        var first = Drift(lab, 60, 0);
        var second = Drift(lab, 60, 10);
        Tick(lab, 20, 1 / 60f);
        Assert.True(first.Holder != null, "first is free");
        Assert.True(second.Holder != null, "second is free");
        Assert.False(ReferenceEquals(first.Holder, second.Holder), "one holder for two items");

        // Both ships' nearest item is the same one: the one that loses it takes the other.
        var shared = Build(Beam());
        var p = Spawn(shared, float3(0, 0, 0));
        var q = Spawn(shared, float3(0, 0, 10));
        var near = Drift(shared, 40, 5);
        var far = Drift(shared, 40, -14);
        p.TractorCommand = 1;
        q.TractorCommand = 1;
        Tick(shared, 20, 1 / 60f);
        Assert.True(near.Holder != null, "near is free");
        Assert.True(far.Holder != null, "far is free");
        Assert.False(ReferenceEquals(near.Holder, far.Holder), "one holder for two items");
    }

    [Fact]
    public void AGrabIsDroppedWhenItsGearGoesInactiveOrTheItemFallsOutOfRangeBeforeThePull()
    {
        using var bag = new CompositeDisposable();
        const float dt = 1 / 60f;

        // Gear switched off mid-grab: cancelled, the item free.
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 30, dt);
        Assert.True(ReferenceEquals(ship, item.Holder), "item is not held by ship");
        ship.Equipment.Single(e => e.Behaviors.OfType<Pickup>().Any()).Enabled.Value = false;
        Tick(lab, 2, dt);
        Assert.Equal(GrabPhase.Cancelled, events.Last().Phase);
        Assert.True(item.Holder == null, "item is held");

        // The ship leaves while the beam is still extending: cancelled. The same departure during the pull is not.
        foreach (var departAtPull in new[] { false, true })
        {
            var other = Build(Beam());
            var pilot = Spawn(other, float3(0, 0, 0));
            var target = Drift(other, 40, 0);
            var seen = Watch(pilot, bag);
            pilot.TractorCommand = 1;
            Until(other, dt, () => seen.Any(e => departAtPull ? e.Phase == GrabPhase.Pull : e.Phase == GrabPhase.Extend));
            pilot.Position = float3(-200, 0, 0);
            Tick(other, 120, dt);
            if (departAtPull)
            {
                Assert.Equal(GrabPhase.Completed, seen.First(e => e.Phase == GrabPhase.Completed || e.Phase == GrabPhase.Cancelled).Phase);
                Assert.False(other.Zone.FloatingItems.Contains(target), "target is still in the zone");
            }
            else
            {
                Assert.Equal(GrabPhase.Cancelled, seen.First(e => e.Phase == GrabPhase.Completed || e.Phase == GrabPhase.Cancelled).Phase);
                Assert.True(target.Holder == null, "target is held");
                Assert.True(other.Zone.FloatingItems.Contains(target), "target left the zone");
            }
        }
    }

    [Fact]
    public void SelectionIsTheItemNearestTheBeamAxis()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;

        // The nearer to the axis wins, though the other is closer to the ship.
        var near = Drift(lab, 20, 12);
        var onAxis = Drift(lab, 60, 3);
        Tick(lab, 30, 1 / 60f);
        Assert.True(ReferenceEquals(ship, onAxis.Holder), "onAxis is not held by ship");
        Assert.True(near.Holder == null, "near is held");
        Assert.Equal(onAxis.Id, events.First().Target);

        // Out of the beam, past its range or behind the ship, an item is never taken.
        foreach (var (x, y) in new[] { (30f, 30f), (80f, 0f), (-10f, 0f) })
        {
            var other = Build(Beam());
            var lone = Spawn(other, float3(0, 0, 0));
            var stray = Drift(other, x, y);
            var attempts = Watch(lone, bag);
            lone.TractorCommand = 1;
            Tick(other, 240, 1 / 60f);
            Assert.Empty(attempts);
            Assert.True(stray.Holder == null, "stray is held");
            Assert.True(other.Zone.FloatingItems.Contains(stray), "stray left the zone");
        }

        // Equally far from the axis, the lower id.
        var tie = Build(Beam());
        var pilot = Spawn(tie, float3(0, 0, 0));
        var lower = Drift(tie, 40, 5);
        var higher = Drift(tie, 40, -5);
        pilot.TractorCommand = 1;
        Tick(tie, 30, 1 / 60f);
        Assert.True(ReferenceEquals(pilot, lower.Holder), "lower is not held by pilot");
        Assert.True(higher.Holder == null, "higher is held");
    }

    [Fact]
    public void AHeldItemIsPlacedNotIntegratedAndDoesNotExpire()
    {
        using var bag = new CompositeDisposable();
        const float dt = 1 / 60f;
        var lab = Build(Beam(), lifetime: 1f);
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0, float2(3, 0));
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Until(lab, dt, () => item.Holder != null);
        var claimed = item.Body.Position;
        Assert.Equal(claimed, item.HeldFrom);

        // Through Extend and Envelop the item stays where it was claimed, whatever velocity it had.
        for (var guard = 0; guard < 1200 && !events.Any(e => e.Phase == GrabPhase.Pull); guard++)
        {
            lab.Zone.Update(dt);
            if (events.Any(e => e.Phase == GrabPhase.Pull)) break;
            Assert.Equal(claimed, item.Body.Position);
        }
        Assert.Contains(events, e => e.Phase == GrabPhase.Pull);

        // During the pull it moves toward the holder, and a held item outlives its lifetime.
        lab.Zone.Update(dt * 10);
        Assert.True(item.Body.Position.x < claimed.x);
        Assert.True(item.Body.Position.x > 0);
        var held = Build(Beam(), lifetime: 1f);
        var carrier = Spawn(held, float3(5, 0, 7));
        var stuck = Drift(held, 40, 0);
        Assert.True(held.Zone.TryHold(carrier, stuck.Id));
        Tick(held, 180, dt);
        Assert.True(held.Zone.FloatingItems.Contains(stuck), "stuck left the zone");
        Assert.Equal(float2(40, 0), stuck.Body.Position);

        // The pose eases (smoothstep): a quarter of the progress is 15.625 per cent of the way, not 25.
        held.Zone.SetHeldProgress(stuck.Id, .25f);
        held.Zone.Update(dt);
        Assert.Equal(40f - 35f * .15625f, stuck.Body.Position.x, 3);
        Assert.Equal(7f * .15625f, stuck.Body.Position.y, 3);

        // Placed, not drifted: the same progress held for two steps puts it at the same halfway point.
        held.Zone.SetHeldProgress(stuck.Id, .5f);
        held.Zone.Update(dt);
        held.Zone.Update(dt);
        Assert.Equal(22.5f, stuck.Body.Position.x, 3);
        Assert.Equal(3.5f, stuck.Body.Position.y, 3);

        // At progress 1 it is at the holder; freed, an item past its lifetime goes on the next step.
        held.Zone.SetHeldProgress(stuck.Id, 1);
        held.Zone.Update(dt);
        Assert.Equal(5f, stuck.Body.Position.x, 3);
        Assert.Equal(7f, stuck.Body.Position.y, 3);
        held.Zone.Free(stuck.Id);
        held.Zone.Update(dt);
        Assert.False(held.Zone.FloatingItems.Contains(stuck), "stuck is still in the zone");
    }

    [Fact]
    public void AnInactiveBeamSpinsDownAndMustSpinUpAgain()
    {
        using var bag = new CompositeDisposable();
        const float dt = 1 / 60f;
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var gear = ship.Equipment.Single(e => e.Behaviors.OfType<Pickup>().Any());
        ship.TractorCommand = 1;
        Tick(lab, 60, dt);
        Assert.Equal(1f, ship.TractorPower);

        // Switched off with the key still down, the beam spins down at the spin rate...
        gear.Enabled.Value = false;
        Tick(lab, 15, dt);
        Assert.InRange(ship.TractorPower, .45f, .55f);
        Tick(lab, 30, dt);
        Assert.Equal(0f, ship.TractorPower);

        // ...and switched back on it spins up from nothing: an item in the beam is not claimed at once.
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        gear.Enabled.Value = true;
        Tick(lab, 10, dt);
        Assert.Empty(events);
        Assert.True(item.Holder == null, "item is held");
        Tick(lab, 20, dt);
        Assert.True(ReferenceEquals(ship, item.Holder), "item is not held by ship");
    }

    [Fact]
    public void RemovingOneHolderFreesOnlyItsOwnItems()
    {
        var lab = Build(Beam());
        var a = Spawn(lab, float3(0, 0, 0));
        var b = Spawn(lab, float3(0, 0, 50));
        var mine = Drift(lab, 40, 0);
        var theirs = Drift(lab, 40, 50);
        a.TractorCommand = 1;
        b.TractorCommand = 1;
        Tick(lab, 30, 1 / 60f);
        Assert.True(ReferenceEquals(a, mine.Holder), "mine is not held by a");
        Assert.True(ReferenceEquals(b, theirs.Holder), "theirs is not held by b");

        Kill(a);
        Assert.True(mine.Holder == null, "mine is held");
        Assert.True(ReferenceEquals(b, theirs.Holder), "theirs was freed with a");
    }

    [Fact]
    public void TheHoldersLeavingFreesItsItems()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        Assert.True(ship.CargoBays.Single().TryStore(lab.Ore(7)));
        var item = Drift(lab, 40, 0, ore: 3);
        ship.TractorCommand = 1;
        Tick(lab, 30, 1 / 60f);
        Assert.True(ReferenceEquals(ship, item.Holder), "item is not held by ship");
        var before = lab.Zone.FloatingItems.Count;

        // Death goes through the zone's one removal: the held item is free, and the ship's own cargo drops as always.
        Kill(ship);
        Assert.True(item.Holder == null, "item is held");
        Assert.True(lab.Zone.FloatingItems.Contains(item), "item left the zone");
        Assert.Equal(before + 1, lab.Zone.FloatingItems.Count);
        Assert.Equal(7, ((SimpleCommodity) lab.Zone.FloatingItems.Last().Item).Quantity);
        Tick(lab, 60, 1 / 60f);
        Assert.True(lab.Zone.FloatingItems.Contains(item), "item left the zone");
        Assert.True(item.Holder == null, "item is held");
    }
}
