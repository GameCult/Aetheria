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

// Loot in the sim (aetheria-release cut loot-1): a death drops gear by probability and cargo whole, rolled on the
// zone's own dice; the drops float as free bodies, expire, and are collected through Zone.TryPickUp. Rulings:
// jettison-shared-floating-items, mines-float-like-loot, one-gravity-law. Every death here goes through the
// production path: an entity's own death stream, observed by the one death subscription in Zone.
public sealed class LootTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-loot-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public LootTests() => Directory.CreateDirectory(_root);

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

    private sealed class Lab
    {
        public ItemManager Items;
        public Zone Zone;
        public int Lot = 1;

        public EquippableItem Make(string name, float durability = 1) => new EquippableItem
        {
            Data = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<EquippableItemData>(name)), Durability = durability, Lot = Lot++
        };

        public SimpleCommodity Ore(int quantity) => new SimpleCommodity
        {
            Data = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<SimpleCommodityData>("Ore")), Quantity = quantity
        };

        public string Describe(ItemInstance item) => Items.GetData(item).Name + (item is SimpleCommodity c ? "x" + c.Quantity : "");
    }

    private Lab Build(string zoneName = "Loot", float probability = .5f, float lifetime = 30f, float velocity = 25f,
        PlanetSettings planets = null, float spread = 0f)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetArmorInfoThreshold = .2f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 1f,
            UnaidedAccuracy = .05f, UnaidedTracking = 10f, UnaidedPrecision = 2f, AgentMinHitProbability = 0f,
            LootDropProbability = probability, LootDropVelocity = velocity, FloatingItemLifetime = lifetime
        };
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var hardpoint = new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() };
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = Solid(5, 5), Durability = 1000000, Mass = 1000, Hardpoints = { hardpoint } });
        cache.Upsert(new HullData { Name = "Hub", HullType = HullType.Station, Shape = Solid(5, 5), Durability = 1000000, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(1), Range = Constant(1000), MinRange = Constant(0), Velocity = Constant(0), Spread = Constant(spread),
                DamageSpread = Constant(0), Penetration = Constant(0), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData { Accuracy = Constant(1), Resolution = Constant(1000), Precision = Constant(1), Tracking = Constant(100000) } }
        });
        // One occupied cell, so a bay can be filled with it.
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
        var zone = new Zone(items, planets ?? new PlanetSettings(), new ZonePack { Radius = planets == null ? 0 : 1000 }, new GalaxyZone { Name = zoneName, Owner = null }, null);
        return new Lab { Items = items, Zone = zone };
    }

    // A ship carrying a gun, a targeting system and a spare, with ore and a spare gun in its crate.
    private static Ship Spawn(Lab lab, float3 at, bool cargo = true)
    {
        var hull = lab.Items.ItemData.RefOf<ItemData>(lab.Items.ItemData.GetByName<HullData>("Skiff"));
        var ship = new Ship(lab.Items, lab.Zone, new EquippableItem { Data = hull, Durability = 1000000, Lot = lab.Lot++ }, new EntitySettings());
        Assert.True(ship.TryEquip(lab.Make("Gun"), new int2(0, 0)));
        foreach (var name in new[] { "Targeting", "Spare", "Spare", "Spare", "Spare" })
        {
            var gear = lab.Make(name);
            Assert.True(ship.TryFindSpace(gear, out var pos));
            Assert.True(ship.TryEquip(gear, pos));
        }
        var crate = lab.Make("Crate");
        Assert.True(ship.TryFindSpace(crate, out var cratePos));
        Assert.True(ship.TryEquip(crate, cratePos));
        if (cargo)
        {
            Assert.True(ship.CargoBays.Single().TryStore(lab.Ore(7)));
            Assert.True(ship.CargoBays.Single().TryStore(lab.Make("Spare")));
        }
        lab.Zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = at;
        return ship;
    }

    private static Entity Station(Lab lab, float3 at)
    {
        var hull = lab.Items.ItemData.RefOf<ItemData>(lab.Items.ItemData.GetByName<HullData>("Hub"));
        var station = new OrbitalEntity(lab.Items, lab.Zone, new EquippableItem { Data = hull, Durability = 1000000, Lot = lab.Lot++ }, default, new EntitySettings());
        Assert.True(station.TryEquip(lab.Make("Crate"), new int2(1, 1)));
        var spare = lab.Make("Spare");
        Assert.True(station.TryFindSpace(spare, out var pos));
        Assert.True(station.TryEquip(spare, pos));
        Assert.True(station.CargoBays.Single().TryStore(lab.Ore(3)));
        lab.Zone.Entities.Add(station);
        station.Activate();
        station.Position = at;
        return station;
    }

    private static void Kill(Entity e) => e.HeatstrokeDeath.OnNext(Unit.Default);

    private static List<(string Item, float2 Position, float2 Velocity)> Released(Lab lab, int from) =>
        lab.Zone.FloatingItems.Skip(from).Select(f => (lab.Describe(f.Item), f.Body.Position, f.Body.Velocity)).ToList();

    private static void AssertSameDrops(List<(string Item, float2 Position, float2 Velocity)> a, List<(string Item, float2 Position, float2 Velocity)> b)
    {
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Item, b[i].Item);
            Assert.Equal(a[i].Position, b[i].Position);
            Assert.Equal(a[i].Velocity, b[i].Velocity);
        }
    }

    private static bool Differ(List<(string Item, float2 Position, float2 Velocity)> a, List<(string Item, float2 Position, float2 Velocity)> b) =>
        a.Count != b.Count || a.Zip(b, (x, y) => x.Item != y.Item || !x.Position.Equals(y.Position) || !x.Velocity.Equals(y.Velocity)).Any(d => d);

    [Fact]
    public void DeathDropsAreRolledOnTheZonesDice()
    {
        var at = float3(31, 0, -17);
        var first = Build("Dice");
        Kill(Spawn(first, at));
        var dropsA = Released(first, 0);
        Assert.True(dropsA.Count > 2, "the fixture must drop enough for a draw to show");
        Assert.All(dropsA, d => Assert.Equal(new float2(31, -17), d.Position));
        Assert.All(dropsA, d => Assert.Equal(25f, length(d.Velocity), 3));

        // Another zone with the same name (the same galaxy seed) and the same ship drops the same things the same way,
        // whatever the item manager's own generator has been asked in between.
        var second = Build("Dice");
        for (var i = 0; i < 5; i++) second.Items.Random.NextFloat();
        var shipB = Spawn(second, at);
        for (var i = 0; i < 5; i++) second.Items.Random.NextFloat();
        Kill(shipB);
        AssertSameDrops(dropsA, Released(second, 0));

        // Another combat seed rolls differently.
        var other = Build("Other dice");
        Kill(Spawn(other, at));
        Assert.True(Differ(dropsA, Released(other, 0)));

        // A second death in the same zone uses the next ordinal.
        var count = first.Zone.FloatingItems.Count;
        Kill(Spawn(first, at));
        Assert.True(Differ(dropsA, Released(first, count)));
    }

    [Fact]
    public void GearDropsByProbabilityCargoAlways()
    {
        foreach (var probability in new[] { 0f, 1f })
        {
            var lab = Build(probability: probability);
            var ship = Spawn(lab, float3(5, 0, 5));
            var gear = ship.Equipment.Where(e => e != ship.EquippedHull).Select(e => (ItemInstance) e.EquippableItem).ToList();
            var cargo = ship.CargoBays.Single().Cargo.Keys.ToList();
            Assert.Equal(6, gear.Count);
            Assert.Equal(2, cargo.Count);
            Kill(ship);
            var dropped = lab.Zone.FloatingItems.Select(f => f.Item).ToList();
            Assert.All(cargo, c => Assert.Contains(c, dropped));
            Assert.DoesNotContain(ship.EquippedHull.EquippableItem, dropped);
            if (probability == 0f) Assert.Equal(cargo.Count, dropped.Count);
            else
            {
                Assert.Equal(cargo.Count + gear.Count, dropped.Count);
                Assert.All(gear, g => Assert.Contains(g, dropped));
            }
        }

        // 400 deaths at .5: the drop rate of the equipped gear is .5 within three sigma (sigma = .5 / sqrt(2400)).
        var rate = Build(probability: .5f);
        var equipped = 0;
        for (var i = 0; i < 400; i++)
        {
            var ship = Spawn(rate, float3(i, 0, 0), cargo: false);
            var gear = ship.Equipment.Where(e => e != ship.EquippedHull).Select(e => (ItemInstance) e.EquippableItem).ToList();
            var before = rate.Zone.FloatingItems.Count;
            Kill(ship);
            equipped += rate.Zone.FloatingItems.Skip(before).Count(f => gear.Contains(f.Item));
        }
        Assert.InRange(equipped / (400f * 6f), .5f - 3 * .0102f, .5f + 3 * .0102f);

        // A station's death drops through the same path.
        var hub = Build(probability: 1f);
        Kill(Station(hub, float3(9, 0, 9)));
        Assert.Equal(new[] { "Orex3", "Spare" }, hub.Zone.FloatingItems.Select(f => hub.Describe(f.Item)).OrderBy(x => x).ToArray());
    }

    // One unaimed shot with spread, and what the commit decided.
    private static (ShotResult Result, int2 Cell, float Lateral, float2 Bearing) OneShot(Lab lab, Ship shooter)
    {
        ShotOutcome outcome = null;
        using var c = lab.Zone.ShotCommitted.Subscribe(o => outcome = o);
        var gun = shooter.Equipment.Single(e => lab.Items.GetData(e.EquippableItem).Name == "Gun");
        ((InstantWeapon) gun.Behaviors.Single(b => b is Weapon)).Activate();
        lab.Zone.Update(.01f);
        Assert.NotNull(outcome);
        return (outcome.Result, outcome.Cell, outcome.Lateral, outcome.Bearing);
    }

    [Fact]
    public void LootDiceDoNotTouchShotDice()
    {
        (ShotResult Result, int2 Cell, float Lateral, float2 Bearing) Run(bool death)
        {
            var lab = Build("Shots");
            var shooter = Spawn(lab, float3(37, 0, -11), cargo: false);
            var target = Spawn(lab, float3(37, 0, 89), cargo: false);
            var bystander = Spawn(lab, float3(900, 0, 900));
            shooter.SetTarget(target);
            shooter.EntityInfoGathered[target] = 1f;
            shooter.SetIff(target, true);
            lab.Zone.Update(0f);
            if (death) Kill(bystander);
            return OneShot(lab, shooter);
        }
        var quiet = Run(false);
        var after = Run(true);
        Assert.Equal(ShotResult.Hit, quiet.Result);
        Assert.NotEqual(0f, quiet.Lateral);
        Assert.Equal(quiet, after);
    }

    [Fact]
    public void AFloatingItemDriftsAsABodyAndExpires()
    {
        var planets = new PlanetSettings { ZoneDepth = 1000, ZoneDepthExponent = 1, GravityStrength = 1 };
        var lab = Build(lifetime: 3f, planets: planets);
        var settings = lab.Items.GameplaySettings;
        var start = new float2(40, -25);
        var launch = new float2(6, 2);
        var item = lab.Ore(1);
        var floating = lab.Zone.Release(item, start, launch);
        Assert.Same(item, floating.Item);
        Assert.Equal(lab.Zone.Time, floating.ReleasedAt);
        Assert.Equal(3f, floating.Lifetime);
        Assert.True(length(lab.Zone.GetForce(start)) > 0, "the fixture's zone must pull, or drift proves nothing");

        var removals = 0;
        using var sub = lab.Zone.FloatingItems.ObserveRemove().Subscribe(_ => removals++);
        var alone = new KinematicBody { Position = start, Velocity = launch };
        var steps = 0;
        while (lab.Zone.FloatingItems.Count > 0)
        {
            Assert.True(steps < 20, "the item must expire");
            alone.Step(.5f, lab.Zone.GetForce(alone.Position), settings);
            lab.Zone.Update(.5f);
            steps++;
            if (lab.Zone.FloatingItems.Count > 0)
            {
                Assert.Equal(alone.Position, floating.Body.Position);
                Assert.Equal(alone.Velocity, floating.Body.Velocity);
                Assert.Equal(alone.Drift, floating.Body.Drift);
            }
        }
        // Three seconds at half-second steps: it survives five steps and goes on the sixth, not before and not after.
        Assert.Equal(6, steps);
        Assert.Equal(1, removals);
    }

    [Fact]
    public void TryPickUpStoresAndRemoves()
    {
        var lab = Build();
        var taker = Station(lab, float3(0, 0, 0));
        Assert.Contains(Solid(5, 5).AllCoordinates, cell => taker.TryEquip(lab.Make("Crate"), cell));
        Assert.Equal(2, taker.CargoBays.Count);
        // The first bay is full, so the first bay that accepts the item is the second.
        while (taker.CargoBays[0].TryStore(lab.Make("Spare"))) { }

        var removals = 0;
        using var sub = lab.Zone.FloatingItems.ObserveRemove().Subscribe(_ => removals++);
        var item = lab.Make("Spare");
        var floating = lab.Zone.Release(item, new float2(1, 2), new float2(3, 4));
        Assert.True(lab.Zone.TryPickUp(taker, floating.Id));
        Assert.Equal(1, removals);
        Assert.Empty(lab.Zone.FloatingItems);
        Assert.Contains(item, taker.CargoBays[1].Cargo.Keys);
        Assert.DoesNotContain(item, taker.CargoBays[0].Cargo.Keys);

        // The same id again is unknown now.
        Assert.False(lab.Zone.TryPickUp(taker, floating.Id));

        // Every bay full: false, and the item floats on with its body unchanged.
        while (taker.CargoBays[1].TryStore(lab.Make("Spare"))) { }
        var stuck = lab.Zone.Release(lab.Make("Spare"), new float2(7, 8), new float2(1, 1));
        var body = stuck.Body;
        Assert.False(lab.Zone.TryPickUp(taker, stuck.Id));
        Assert.Same(stuck, lab.Zone.FloatingItems.Single());
        Assert.Equal(body.Position, stuck.Body.Position);
        Assert.Equal(body.Velocity, stuck.Body.Velocity);
        Assert.Equal(1, removals);

        Assert.False(lab.Zone.TryPickUp(taker, new FloatingBodyId(9999)));
    }

    [Fact]
    public void DropsTakeFreshBodyIds()
    {
        var lab = Build(probability: 1f);
        Kill(Spawn(lab, float3(0, 0, 0)));
        var before = lab.Zone.FloatingItems.Select(f => f.Id.Value).ToList();
        var mine = new Mine { Id = lab.Zone.NextBodyId(), Body = new KinematicBody { Position = new float2(50, 50) }, Lifetime = 1000, FuseDelay = 1000, ArmingDelay = 1000 };
        lab.Zone.Lay(mine);
        Kill(Spawn(lab, float3(0, 0, 0)));
        var all = lab.Zone.FloatingItems.Select(f => f.Id.Value).ToList();
        var after = all.Skip(before.Count).ToList();
        Assert.NotEmpty(before);
        Assert.NotEmpty(after);
        Assert.Equal(Enumerable.Range(1, all.Count + 1).Select(i => (uint) i), before.Concat(new[] { mine.Id.Value }).Concat(after));
    }

    [Fact]
    public void FloatingItemsAreNotSaved()
    {
        var lab = Build(probability: 1f);
        Kill(Spawn(lab, float3(0, 0, 0)));
        Assert.NotEmpty(lab.Zone.FloatingItems);
        var pack = lab.Zone.PackZone();
        var restored = new Zone(lab.Items, new PlanetSettings(), pack, new GalaxyZone { Name = "Loot", Owner = null }, null);
        Assert.Empty(restored.FloatingItems);
    }
}
