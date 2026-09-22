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

// Cut 4 (docs/fire-control-cut.md): pins the two rules this cut actually adds -- a continuous weapon is a
// sequence of rolls rather than a continuous truth, and splash (mine/airburst blast) is one rule applied to
// every entity in radius, directional over each target's own hull. FireAuthorityTests.cs already pins the
// roll itself (seeding, commit horizon, the shield-absorb branch, the damage rule); this file builds its own
// minimal fixtures rather than reusing that one's InstantWeapon-shaped Engagement, the same convention that
// file's own comment establishes.
public sealed class FireControlCut4Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut4-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut4Tests() => Directory.CreateDirectory(_root);

    private readonly List<CultCache> _openCaches = new List<CultCache>();

    public void Dispose()
    {
        foreach (var c in _openCaches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 1f,
        UnaidedAccuracy = .05f,
        AgentMinHitProbability = 0f,
        BeamResolveInterval = .1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // A 5x5 fully-solid hull, the same shape FireAuthorityTests.Build uses.
    private static HullData MakeHullData(float armor = 0) => new HullData
    {
        Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(), Durability = 1000, Mass = 1000, Armor = armor,
        Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
    };

    private static Shape SolidShape()
    {
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // ---- Cut 4's beam rule: ConstantWeapon rolls once per BeamResolveInterval, not once per tick and not
    // once for the whole burst. ----

    private sealed class BeamEngagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public ConstantWeapon Weapon;
    }

    private BeamEngagement BuildBeam(GameplaySettings settings, float damage, float targetRange = 100)
    {
        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(MakeHullData());
        cache.Upsert(new GearData
        {
            Name = "Beam", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ConstantWeaponData
            {
                Damage = Constant(damage), Range = Constant(1000), MinRange = Constant(0),
                Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            // Cut 6d (docs/fire-control-cut.md): Precision now feeds pOnHull (the dart-throw kernel's sigma)
            // for every shot, aimed or not -- 0 would balloon sigma and starve this fixture's rolls, which
            // don't care about aim placement. See FireAuthorityTests.Build's own comment on this same change.
            Behaviors = { new TargetingSystemData
            {
                Accuracy = Constant(1), Resolution = Constant(1), Precision = Constant(2), Tracking = Constant(1000)
            } }
        });
        // Powers the beam: with no Reactor, PowerBus grants nothing and ConstantWeapon.Execute safes itself
        // off (Item.PowerSupply <= 1e-4f) after the first tick, same rake FireAuthorityTests.ShieldTakesHit
        // notes for its own Reactor/Shield pair.
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ReactorData { Charge = Constant(1000), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[5] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[9] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Beam"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);
        var weapon = (ConstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var reactorRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Reactor"));
        var reactor = new EquippableItem { Data = reactorRef, Durability = 10, Lot = 5 };
        Assert.True(shooter.TryFindSpace(reactor, out var rpos));
        Assert.True(shooter.TryEquip(reactor, rpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Beam"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 9 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, targetRange);
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        zone.Update(0f); // warm up resolved stats, same as FireAuthorityTests.Build

        return new BeamEngagement { Items = items, Zone = zone, Shooter = shooter, Target = target, Weapon = weapon };
    }

    // A beam is a sequence of discrete rolls, not a continuous truth: over one second it produces exactly
    // 1/BeamResolveInterval outcomes (the damage each carries is FireControlCut11Tests.ADamageOverrideIsWhatTheShotCarries). Interval (.25s)
    // and tick (.125s) are both exact dyadic fractions so float accumulation cannot drift the count -- and the
    // tick is strictly smaller than the interval, so the two are distinguishable from a "one roll per tick"
    // mutant (which would produce 8 outcomes here, not 4) and from a "one roll per burst" mutant (1, not 4).
    [Fact]
    public void BeamRollsPerInterval()
    {
        var settings = TestSettings();
        settings.BeamResolveInterval = .25f;
        var e = BuildBeam(settings, damage: 10);

        var outcomes = new List<ShotOutcome>();
        e.Zone.ShotResolved.Subscribe(outcome => { outcomes.Add(outcome); });

        e.Weapon.Activate();
        for (var i = 0; i < 8; i++) e.Zone.Update(.125f); // 1 second in 8 ticks of .125s

        Assert.Equal(4, outcomes.Count); // 1s / .25s per roll
        Assert.All(outcomes, o => Assert.True(o.Hit));
    }

    // ---- Regression: FireControl.Fire must run whether or not a presentation is listening. Both
    // InstantWeapon.Execute and ConstantWeapon.Execute used to write `OnFire?.Invoke(FireControl.Fire(...))`
    // inline -- C#'s null-conditional short-circuits the WHOLE expression, argument included, whenever the
    // event has no subscriber, so with no Unity EntityInstance wired up (a headless run,
    // docs/headless-playground-cut.md) the shot silently never fired. Caught by this cut's own
    // BeamRollsPerInterval fixture; this test pins the discrete-weapon half of the same fix, which no
    // existing test could catch because FireAuthorityTests.cs always calls FireControl.Fire directly, never
    // through Trigger()/Execute(). Mutation: inline the call back into the null-conditional. ----
    [Fact]
    public void InstantWeaponFiresWithoutAnyOnFireSubscriber()
    {
        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(MakeHullData());
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(10), Range = Constant(1000), MinRange = Constant(0), Velocity = Constant(0),
                Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0),
                Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger { Lots = { [1] = new Lot { Origin = new Attributed(), Quality = 1 } } };
        var items = new ItemManager(cache, ledger, TestSettings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());
        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 1 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.EquippableItem == gun).Behaviors.Single(b => b is Weapon);

        zone.Entities.Add(shooter);
        shooter.Activate();
        zone.Update(0f); // warm up resolved stats

        // No target, no OnFire subscriber -- exactly a headless shooter with no target and no presentation.
        var resolvedCount = 0;
        zone.ShotResolved.Subscribe(_ => resolvedCount++);

        weapon.Activate(); // Trigger() -> queues a burst
        zone.Update(.016f); // one ordinary tick -- Execute() should fire and the zero-velocity shot resolves same tick

        Assert.Equal(1, resolvedCount);
    }

    // ---- Cut 4's splash rule: FireControl.Splash is an unconditional area effect (no roll), directional per
    // target's own hull facing. Tested directly against the function, no weapon/gear catalog needed -- Entity.
    // MapEntity (called from the Entity constructor) is enough to give a Ship its Armor/GearOccupancy/Hull. ----

    private ProvenanceLedger _splashLedger;

    private (ItemManager Items, Zone Zone) BuildSplashZone(GameplaySettings settings, float armor = 0)
    {
        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(MakeHullData(armor));
        cache.FlushAsync().Wait();

        _splashLedger = new ProvenanceLedger();
        var items = new ItemManager(cache, _splashLedger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        return (items, zone);
    }

    private Ship AddShip(ItemManager items, Zone zone, float3 position, float2 direction, int lot)
    {
        _splashLedger.Lots[lot] = new Lot { Origin = new Attributed(), Quality = 1 };
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var hull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = lot };
        var ship = new Ship(items, zone, hull, new EntitySettings());
        ship.Position = position;
        ship.Direction = direction;
        zone.Entities.Add(ship);
        return ship;
    }

    // Splash is one rule, not per-effect: every entity within radius takes damage, one outside radius takes
    // none. Mutation: damage only the nearest entity in range instead of every one.
    [Fact]
    public void SplashHitsEveryEntityInRadius()
    {
        var (items, zone) = BuildSplashZone(TestSettings());
        var near = AddShip(items, zone, float3(10, 0, 0), float2(0, 1), 1);
        var far = AddShip(items, zone, float3(20, 0, 0), float2(0, 1), 2);
        var outOfRange = AddShip(items, zone, float3(1000, 0, 0), float2(0, 1), 3);

        var nearBefore = near.Hull.Durability;
        var farBefore = far.Hull.Durability;
        var outBefore = outOfRange.Hull.Durability;

        FireControl.Splash(zone, float3.zero, radius: 30, damage: 50, damageType: DamageType.Kinetic);

        Assert.True(near.Hull.Durability < nearBefore);
        Assert.True(far.Hull.Durability < farBefore);
        Assert.Equal(outBefore, outOfRange.Hull.Durability);
    }

    // Splash is directional over each target's own hull: cells facing the blast take it, cells facing away do
    // not. Mutation: damage the whole shape regardless of direction.
    [Fact]
    public void SplashIsDirectional()
    {
        // Nonzero armor so an undamaged cell is distinguishable from a damaged one -- with armor 0 every cell
        // starts and stays at 0 regardless of which half took the schematic hit.
        var (items, zone) = BuildSplashZone(TestSettings(), armor: 10);
        // Facing +Z (the default); the blast sits behind the ship along -Z, so the near half of the hull
        // (negative-Z cells, closest to the blast) should take damage and the far half (positive-Z) should not.
        var ship = AddShip(items, zone, float3(0, 0, 10), float2(0, 1), 1);

        FireControl.Splash(zone, float3(0, 0, 0), radius: 50, damage: 1000, damageType: DamageType.Kinetic);

        var hullData = (HullData) items.GetData(ship.Hull);
        var center = hullData.Shape.CenterOfMass;
        var nearCellDamaged = false;
        var farCellUntouched = false;
        foreach (var v in hullData.Shape.Coordinates)
        {
            var offset = (float2) v - center;
            if (offset.y < -.5f && ship.Armor[v.x, v.y] < ship.MaxArmor[v.x, v.y]) nearCellDamaged = true;
            if (offset.y > .5f && ship.Armor[v.x, v.y] >= ship.MaxArmor[v.x, v.y]) farCellUntouched = true;
        }

        Assert.True(nearCellDamaged);
        Assert.True(farCellUntouched);
    }
}
