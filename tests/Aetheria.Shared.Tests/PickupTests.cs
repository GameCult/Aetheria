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

    private Lab Build(PickupData beam, float lifetime = 30f)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetArmorInfoThreshold = .2f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 1f,
            UnaidedAccuracy = .05f, UnaidedTracking = 10f, UnaidedPrecision = 2f, AgentMinHitProbability = 0f,
            LootDropProbability = 0, LootDropVelocity = 25, FloatingItemLifetime = lifetime
        };
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
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
        cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = Solid(4, 4), Durability = 4 });
        cache.Upsert(new SimpleCommodityData { Name = "Ore", Shape = Solid(1, 1), MaxStack = 100 });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 9000; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack { Radius = 0 }, new GalaxyZone { Name = "Pickup", Owner = null }, null);
        return new Lab { Items = items, Zone = zone };
    }

    // A ship with a cargo bay, aimed along +x and (unless told otherwise) carrying the tractor beam.
    private static Ship Spawn(Lab lab, float3 at, bool beam = true)
    {
        var hull = lab.Items.ItemData.RefOf<ItemData>(lab.Items.ItemData.GetByName<HullData>("Skiff"));
        var ship = new Ship(lab.Items, lab.Zone, new EquippableItem { Data = hull, Durability = 1000000, Lot = lab.Lot++ }, new EntitySettings());
        var crate = lab.Make("Crate");
        Assert.True(ship.TryFindSpace(crate, out var cratePos));
        Assert.True(ship.TryEquip(crate, cratePos));
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
        Assert.Null(left.Holder);
        Assert.Contains(left, bare.Zone.FloatingItems);
        Assert.Equal(0f, plain.TractorPower);
        Assert.Empty(plainEvents);

        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        Tick(lab, 60, 1 / 60f);
        Assert.Same(ship, item.Holder);
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
        Tick(lab, 150, 1 / 60f);
        Assert.Equal(new[] { GrabPhase.Started, GrabPhase.Extend, GrabPhase.Envelop, GrabPhase.Pull, GrabPhase.Completed },
            events.Take(5).Select(e => e.Phase).ToArray());
        Assert.All(events.Take(5), e => Assert.Equal(item.Id, e.Target));
        Assert.NotEqual(other.Id, item.Id);
        Assert.Equal(.3f, events[1].PhaseDuration, 4);
        Assert.Equal(.2f, events[2].PhaseDuration, 4);
        Assert.Equal(.5f, events[3].PhaseDuration, 4);
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
        Assert.Same(ship, item.Holder);
        var velocity = item.Body.Velocity;
        var drift = item.Body.Drift;

        // The key comes up during the pull: Power falls below GrabPower and the grab is dropped.
        ship.TractorCommand = 0;
        while (!events.Any(e => e.Phase == GrabPhase.Cancelled) && tick++ < 600) lab.Zone.Update(dt);
        Assert.True(tick < 110);
        Assert.DoesNotContain(events, e => e.Phase == GrabPhase.Completed);
        Assert.Null(item.Holder);
        Assert.Contains(item, lab.Zone.FloatingItems);
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
    public void AFullHoldCancelsAtTheEnd()
    {
        using var bag = new CompositeDisposable();
        var lab = Build(Beam());
        var ship = Spawn(lab, float3(0, 0, 0));
        var bay = ship.CargoBays.Single();
        for (var i = 0; i < 16; i++) Assert.True(bay.TryStore(lab.Make("Spare")));
        Assert.False(bay.TryStore(lab.Make("Spare")));
        var item = Drift(lab, 40, 0);
        var events = Watch(ship, bag);
        ship.TractorCommand = 1;
        var tick = 0;
        while (!events.Any(e => e.Phase == GrabPhase.Cancelled) && tick++ < 300) lab.Zone.Update(1 / 60f);
        Assert.Equal(new[] { GrabPhase.Started, GrabPhase.Extend, GrabPhase.Envelop, GrabPhase.Pull, GrabPhase.Cancelled },
            events.Select(e => e.Phase).ToArray());
        Assert.Null(item.Holder);
        Assert.Contains(item, lab.Zone.FloatingItems);
        Assert.Equal(16, bay.Cargo.Count);
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
        Assert.Contains(only, lab.Zone.FloatingItems);
        Assert.Empty(loser.CargoBays.Single().Cargo);
        Tick(lab, 120, 1 / 60f);
        Assert.DoesNotContain(only, lab.Zone.FloatingItems);
        Assert.Single(holder.CargoBays.Single().Cargo);
        Assert.Empty(loser.CargoBays.Single().Cargo);

        // Two items, each nearest a different beam's axis: each ship holds its own.
        var first = Drift(lab, 60, 0);
        var second = Drift(lab, 60, 10);
        Tick(lab, 20, 1 / 60f);
        Assert.NotNull(first.Holder);
        Assert.NotNull(second.Holder);
        Assert.NotSame(first.Holder, second.Holder);

        // Both ships' nearest item is the same one: the one that loses it takes the other.
        var shared = Build(Beam());
        var p = Spawn(shared, float3(0, 0, 0));
        var q = Spawn(shared, float3(0, 0, 10));
        var near = Drift(shared, 40, 5);
        var far = Drift(shared, 40, -14);
        p.TractorCommand = 1;
        q.TractorCommand = 1;
        Tick(shared, 20, 1 / 60f);
        Assert.NotNull(near.Holder);
        Assert.NotNull(far.Holder);
        Assert.NotSame(near.Holder, far.Holder);
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
        Assert.Same(ship, item.Holder);
        ship.Equipment.Single(e => e.Behaviors.OfType<Pickup>().Any()).Enabled.Value = false;
        Tick(lab, 2, dt);
        Assert.Equal(GrabPhase.Cancelled, events.Last().Phase);
        Assert.Null(item.Holder);

        // The ship leaves while the beam is still extending: cancelled. The same departure during the pull is not.
        foreach (var departAtPull in new[] { false, true })
        {
            var other = Build(Beam());
            var pilot = Spawn(other, float3(0, 0, 0));
            var target = Drift(other, 40, 0);
            var seen = Watch(pilot, bag);
            pilot.TractorCommand = 1;
            while (!seen.Any(e => departAtPull ? e.Phase == GrabPhase.Pull : e.Phase == GrabPhase.Extend)) other.Zone.Update(dt);
            pilot.Position = float3(-200, 0, 0);
            Tick(other, 120, dt);
            if (departAtPull)
            {
                Assert.Equal(GrabPhase.Completed, seen.First(e => e.Phase == GrabPhase.Completed || e.Phase == GrabPhase.Cancelled).Phase);
                Assert.DoesNotContain(target, other.Zone.FloatingItems);
            }
            else
            {
                Assert.Equal(GrabPhase.Cancelled, seen.First(e => e.Phase == GrabPhase.Completed || e.Phase == GrabPhase.Cancelled).Phase);
                Assert.Null(target.Holder);
                Assert.Contains(target, other.Zone.FloatingItems);
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
        Assert.Same(ship, onAxis.Holder);
        Assert.Null(near.Holder);
        Assert.Equal(onAxis.Id, events.First().Target);

        // Out of the beam, past its range or behind the ship, an item is never taken.
        foreach (var (x, y) in new[] { (30f, 30f), (80f, 0f), (-10f, 0f) })
        {
            var other = Build(Beam());
            var lone = Spawn(other, float3(0, 0, 0));
            var stray = Drift(other, x, y);
            lone.TractorCommand = 1;
            Tick(other, 240, 1 / 60f);
            Assert.Null(stray.Holder);
            Assert.Contains(stray, other.Zone.FloatingItems);
        }

        // Equally far from the axis, the lower id.
        var tie = Build(Beam());
        var pilot = Spawn(tie, float3(0, 0, 0));
        var lower = Drift(tie, 40, 5);
        var higher = Drift(tie, 40, -5);
        pilot.TractorCommand = 1;
        Tick(tie, 30, 1 / 60f);
        Assert.Same(pilot, lower.Holder);
        Assert.Null(higher.Holder);
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
        while (item.Holder == null) lab.Zone.Update(dt);
        var claimed = item.Body.Position;
        Assert.Equal(claimed, item.HeldFrom);

        // Through Extend and Envelop the item stays where it was claimed, whatever velocity it had.
        while (!events.Any(e => e.Phase == GrabPhase.Pull))
        {
            lab.Zone.Update(dt);
            if (events.Any(e => e.Phase == GrabPhase.Pull)) break;
            Assert.Equal(claimed, item.Body.Position);
        }

        // During the pull it moves toward the holder, and a held item outlives its lifetime.
        lab.Zone.Update(dt * 10);
        Assert.True(item.Body.Position.x < claimed.x);
        Assert.True(item.Body.Position.x > 0);
        var held = Build(Beam(), lifetime: 1f);
        var carrier = Spawn(held, float3(5, 0, 7));
        var stuck = Drift(held, 40, 0);
        Assert.True(held.Zone.TryHold(carrier, stuck.Id));
        Tick(held, 180, dt);
        Assert.Contains(stuck, held.Zone.FloatingItems);
        Assert.Equal(float2(40, 0), stuck.Body.Position);

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
        Assert.DoesNotContain(stuck, held.Zone.FloatingItems);
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
        Assert.Same(ship, item.Holder);
        var before = lab.Zone.FloatingItems.Count;

        // Death goes through the zone's one removal: the held item is free, and the ship's own cargo drops as always.
        Kill(ship);
        Assert.Null(item.Holder);
        Assert.Contains(item, lab.Zone.FloatingItems);
        Assert.Equal(before + 1, lab.Zone.FloatingItems.Count);
        Assert.Equal(7, ((SimpleCommodity) lab.Zone.FloatingItems.Last().Item).Quantity);
        Tick(lab, 60, 1 / 60f);
        Assert.Contains(item, lab.Zone.FloatingItems);
        Assert.Null(item.Holder);
    }
}
