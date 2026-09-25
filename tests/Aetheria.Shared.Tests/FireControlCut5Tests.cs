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
using float3 = CultMath.float3;
using int2 = CultMath.int2;
using Random = CultMath.Random;

// Cut 5 (docs/fire-control-cut.md, "the rules that landed in one path and not its twin"): pins 5.1-5.7, each
// a rule the campaign declared and implemented on one path while leaving it off the path beside it. Builds
// its own fixtures, the same convention every earlier cut's test file establishes, rather than reusing
// FireAuthorityTests'/FireControlCut4Tests'/TargetingSystemTests' private helpers.
public sealed class FireControlCut5Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut5-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut5Tests() => Directory.CreateDirectory(_root);

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
        TargetArmorInfoThreshold = .2f,
        TargetGearInfoThreshold = .8f,
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 1f,
        UnaidedAccuracy = .05f,
        UnaidedTracking = 10f,
        // Cut 6d (docs/fire-control-cut.md): high enough that pOnHull saturates to 1 at this fixture's 5x5
        // hull's own centre of mass -- see FireAuthorityTests.TestSettings' own comment on this same value.
        UnaidedPrecision = 2f,
        AgentMinHitProbability = 0f,
        BeamResolveInterval = .1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static Shape SolidShape(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // ---- A discrete-weapon shooter/target engagement, the same shape FireAuthorityTests.Build uses, with an
    // optional weak shield (capacity small enough that a real hit overwhelms it, for 5.2). ----

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
    }

    private Engagement Build(
        GameplaySettings settings,
        float damage = 10, float range = 1000, float minRange = 0, float velocity = 0,
        float spread = 0, float damageSpread = 0, float penetration = 0,
        // Cut 6d (docs/fire-control-cut.md): Precision now feeds every shot's pOnHull (the dart-throw kernel's
        // sigma), aimed or not -- see FireAuthorityTests.Build's own comment on this same default change. 0
        // would balloon sigma and starve this fixture's hit tests, which don't care about aim placement.
        float accuracy = 1, float resolution = 1, float precision = 1, float tracking = 1000,
        float targetRange = 100, bool equipTargeting = true, float hullDurability = 1000, float armor = 0,
        bool equipShield = false, float shieldCapacity = 1000,
        Action<ItemManager, Ship, Ship> beforeActivate = null)
    {
        var hullShape = SolidShape(5, 5);
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = hullDurability, Mass = 1000, Armor = armor,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(damage), Range = Constant(range), MinRange = Constant(minRange),
                Velocity = Constant(velocity), Spread = Constant(spread), DamageSpread = Constant(damageSpread),
                Penetration = Constant(penetration), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData
            {
                Accuracy = Constant(accuracy), Resolution = Constant(resolution), Precision = Constant(precision), Tracking = Constant(tracking)
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ReactorData { Charge = Constant(1000), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Shield", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            // Capacity is deliberately the caller's own knob: 5.2 needs a shield too weak to CanTakeHit the
            // shot under test, so its damage overwhelms this reserve outright.
            Behaviors = { new ShieldData { Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(shieldCapacity), RefillDuration = Constant(.01f), RestoreDuration = Constant(.01f) } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        for (var i = 5; i <= 12; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        if (equipTargeting)
        {
            var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
            var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
            Assert.True(shooter.TryFindSpace(targeting, out var tpos));
            Assert.True(shooter.TryEquip(targeting, tpos));
        }

        var targetHull = new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 9 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        if (equipShield)
        {
            var reactorRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Reactor"));
            var reactor = new EquippableItem { Data = reactorRef, Durability = 10, Lot = 10 };
            Assert.True(target.TryFindSpace(reactor, out var rpos));
            Assert.True(target.TryEquip(reactor, rpos));

            var shieldRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Shield"));
            var shield = new EquippableItem { Data = shieldRef, Durability = 10, Lot = 11 };
            Assert.True(target.TryFindSpace(shield, out var spos));
            Assert.True(target.TryEquip(shield, spos));
        }

        beforeActivate?.Invoke(items, shooter, target);

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, targetRange);
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        if (equipShield) target.Shield.Item.Enabled.Value = true;

        zone.Update(0f); // warm up resolved stats

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon };
    }

    // ---- 5.1: Tracking is a floor, not a cliff (Soul finding 3). ----

    // FireControl.Tracking's own unaided fallback: an entity with no working targeting system used to fall
    // back to 0, which Commit's old ternary turned into a hard wall (deviation < .01f ? 1 : 0) -- Q4's
    // "really, really bad" became "impossible." Mutation: revert the fallback to a literal 0f.
    [Fact]
    public void UnaidedTrackingFallsBackToSettingNotZero()
    {
        var e = Build(TestSettings(), equipTargeting: false);
        Assert.Equal(TestSettings().UnaidedTracking, FireControl.Tracking(e.Shooter));
    }

    // The wall itself, end to end: an unaided shooter (Tracking = UnaidedTracking, not 0) against a target
    // that strayed a real but forgivable distance from its fire-time position still lands hits over a run of
    // shots. Mutation: restore Commit's deviation<.01f wall (or equivalently, the 0f fallback above) -- either
    // makes hitCount deterministically 0 for every shot in this run, since the jink (3 units) is comfortably
    // above the old wall's .01f tolerance.
    [Fact]
    public void TrackingIsAFloorNotACliff()
    {
        var settings = TestSettings();
        settings.UnaidedAccuracy = 1f; // isolate this test from Q4's own low ceiling
        settings.UnaidedTracking = 10f;
        // velocity: 0 keeps every shot's flight time (and so its commit horizon) at zero, so a single
        // zone.Update call both commits and resolves it -- no multi-tick float accumulation to muddy exactly
        // how much deviation each shot judges, unlike a long flight stepped in many small dt increments.
        var e = Build(settings, damage: 5, velocity: 0, accuracy: 1, resolution: 1, spread: 0,
            targetRange: 100, equipTargeting: false);
        e.Items.Random = new Random(9001u);

        var hits = 0;
        e.Zone.ShotResolved.Subscribe(o => { if (o.Hit) hits++; });

        var basePosition = e.Target.Position;
        for (var i = 0; i < 30; i++)
        {
            e.Target.Position = basePosition; // Fire captures this as FireTargetPosition
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Target.Position = basePosition + float3(3, 0, 0); // jink: a real, forgivable deviation (< Tracking)
            e.Zone.Update(.01f); // flight time 0 -> commits and resolves in this one call
        }

        Assert.True(hits > 0);
    }

    // ---- 5.2: a shot the shield cannot absorb breaks it (Soul finding 4). ----

    // An unabsorbable hit (damage far past the reserve's Capacity) is decided ShieldBroken at commit and
    // Apply performs Break() before routing the remainder to the hull. Mutation: delete shield.Break() from
    // Apply (FireControl.cs, the map's own named mutation) -- the shield never breaks and the very next shot
    // is absorbed again on schedule instead of paying RestoreDuration.
    [Fact]
    public void UnabsorbableHitBreaksShield()
    {
        var e = Build(TestSettings(), damage: 50, velocity: 0, accuracy: 1, resolution: 1, spread: 0, armor: 0,
            equipShield: true, shieldCapacity: 5); // 50 damage against a 5-capacity reserve: CanTakeHit is false
        for (var i = 0; i < 20; i++) e.Zone.Update(.1f); // charge the (small) reserve to full
        Assert.False(e.Target.Shield.Broken);
        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 50));

        var beforeHull = e.Target.Hull.Durability;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.1f);

        Assert.True(e.Target.Shield.Broken);
        Assert.True(e.Target.Hull.Durability < beforeHull); // the shield didn't absorb it -- the hull did
    }

    // ---- 5.3: continuous weapons obey their arc (Soul finding 9). ----

    private sealed class BeamEngagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public ConstantWeapon Weapon;
    }

    private BeamEngagement BuildBeam(GameplaySettings settings, float3 targetPosition)
    {
        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 1000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        });
        cache.Upsert(new GearData
        {
            Name = "Beam", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ConstantWeaponData
            {
                Damage = Constant(10), Range = Constant(1000), MinRange = Constant(0), Velocity = Constant(0),
                Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
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

        var reactorRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Reactor"));
        var reactor = new EquippableItem { Data = reactorRef, Durability = 10, Lot = 3 };
        Assert.True(shooter.TryFindSpace(reactor, out var rpos));
        Assert.True(shooter.TryEquip(reactor, rpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Beam"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 5 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = targetPosition;
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        zone.Update(0f); // warm up resolved stats

        return new BeamEngagement { Items = items, Zone = zone, Shooter = shooter, Weapon = weapon };
    }

    // A side-mounted beam does not fire forward: StanceAllowsFire alone used to gate ConstantWeapon, so a
    // target well outside a narrow arc still drew power and fired. Mutation: drop ArcAllowsFire from either
    // gate (PowerRequest or Execute's safety check), restoring StanceAllowsFire-only gating.
    [Fact]
    public void ConstantWeaponObeysArc()
    {
        var settings = TestSettings();
        settings.FiringArc = 60; // narrow -- an abeam target is well outside it
        var e = BuildBeam(settings, float3(100, 0, 0)); // directly abeam

        e.Weapon.Activate();
        Assert.True(e.Weapon.Firing); // Activate() sets _firing unconditionally, same as InstantWeapon.Trigger

        // Cut 5b (5b.1): pin PowerRequest's own arc term here, before Execute ever runs. _firing is still true
        // and StanceAllowsFire is true, so ArcAllowsFire is the only thing this call can be testing -- asking
        // only after Zone.Update (below) would be vacuous, because Execute's own gate already drives _firing
        // to false first, and PowerRequest short-circuits on _firing regardless of what ArcAllowsFire mutates
        // to. Without this line, a mutation that drops ArcAllowsFire from PowerRequest alone was a survivor:
        // Execute's separate, correct gate still zeroed Firing and resolvedCount by the time the old
        // post-Update PowerRequest assertion ran, so that assertion was trivially satisfied either way.
        Assert.Equal(0f, e.Weapon.PowerRequest(.1f));

        var resolvedCount = 0;
        e.Zone.ShotResolved.Subscribe(_ => resolvedCount++);
        e.Zone.Update(.1f); // ConstantWeapon.Execute runs -- its own arc gate should safe it off on this tick

        Assert.False(e.Weapon.Firing);
        Assert.Equal(0f, e.Weapon.PowerRequest(.1f)); // trivially 0 now (_firing is false) -- kept as a sanity check
        Assert.Equal(0, resolvedCount);
    }

    // ---- 5.4: one bearing test, including at zero range (Soul finding 11). ----

    private (ItemManager items, Ship shooter, Ship target, EquippedItem weaponItem) BuildPointBlank(GameplaySettings settings)
    {
        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(3, 3), Durability = 1000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        });
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData() }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[5] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooter = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings());
        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);

        var target = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 3 }, new EntitySettings());
        var targetGun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 4 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        return (items, shooter, target, weaponItem);
    }

    // A planar bearing shorter than 1e-6 (point-blank -- target co-located with the firer) bears rather than
    // falling through normalize's NaN. Mutation: drop InArc's lengthsq guard, restoring the NaN comparison
    // that silently returns false.
    [Fact]
    public void PointBlankBearingAlwaysBears()
    {
        var (_, shooter, target, weaponItem) = BuildPointBlank(TestSettings());
        shooter.Position = float3(5, 0, 5);
        target.Position = shooter.Position; // zero-length bearing

        Assert.True(FireControl.InArc(weaponItem, target.Position - shooter.Position));

        // The shared path: Weapon.ArcAllowsFire (the trigger gate every shooter passes through) must agree,
        // now that it no longer carries its own special case and defers to InArc entirely.
        var weapon = (Weapon) weaponItem.Behaviors.Single(b => b is Weapon);
        shooter.Target.Value = target;
        Assert.True(weapon.ArcAllowsFire);
    }

    // A named Soul-pass survivor (docs/fire-control-cut.md Cut 5 Verification): Weapon.ArcAllowsFire must
    // actually reflect InArc's real answer, not just agree with it at point-blank range. Mutation: hardcode
    // ArcAllowsFire's body to `true` -- the point-blank assertion above stays green, but a genuinely abeam
    // target (outside even a 170-degree arc) must read false.
    [Fact]
    public void WeaponArcAllowsFireReflectsRealBearing()
    {
        var settings = TestSettings();
        settings.FiringArc = 60; // narrow enough that abeam is unambiguously out
        var (_, shooter, target, weaponItem) = BuildPointBlank(settings);
        shooter.Position = float3.zero;
        target.Position = float3(100, 0, 0); // directly abeam
        shooter.Target.Value = target;

        var weapon = (Weapon) weaponItem.Behaviors.Single(b => b is Weapon);
        Assert.False(weapon.ArcAllowsFire);

        target.Position = float3(0, 0, 100); // dead ahead
        Assert.True(weapon.ArcAllowsFire);
    }

    // A named Soul-pass survivor (docs/fire-control-cut.md Cut 5 Verification): the player's own trigger
    // (InstantWeapon.Trigger, reached through Activate()) must actually refuse to queue a shot when
    // ArcAllowsFire is false -- Q2's ruling that manual and programmatic fire are one truth, pinned at the
    // one place every shooter's trigger passes through. Mutation: delete
    // `if (!ArcAllowsFire) return;` from InstantWeapon.Trigger -- Activate() would then queue and fire a
    // burst even dead abeam.
    [Fact]
    public void PlayerTriggerObeysArcGate()
    {
        var settings = TestSettings();
        settings.FiringArc = 60;
        var e = Build(settings, damage: 10, velocity: 0, accuracy: 1, resolution: 1, spread: 0, targetRange: 100);
        e.Shooter.Position = float3.zero;
        e.Target.Position = float3(100, 0, 0); // abeam -- outside the narrow 60-degree arc
        e.Shooter.Target.Value = e.Target;

        var resolvedCount = 0;
        e.Zone.ShotResolved.Subscribe(_ => resolvedCount++);

        e.Weapon.Activate(); // the player's own trigger path (CanFire -> Trigger() -> the arc gate)
        e.Zone.Update(.1f);

        Assert.Equal(0, resolvedCount);
    }

    // A named Soul-pass survivor (docs/fire-control-cut.md Cut 5 Verification), rewritten to Detonate (12.4(b),
    // "Retargeted, because Absorb is deleted" / Q12-9 = A): the blast's shield branch must actually break an
    // unabsorbed shield, not merely route the remainder to the hull. A radius this much larger than the hull
    // covers essentially the whole disc's damage on the target's own cells, overwhelming a 5-capacity reserve
    // however the exact per-cell shares fall -- the expected amount is not loosened, it was never asserted
    // precisely here (only that the shield breaks). Mutation: delete `shield.Break()` from Detonate -- the
    // target's hull still takes the blast (ABlastHitsEveryEntityItCovers stays green), but the shield itself
    // never registers the overwhelming hit and keeps absorbing on schedule.
    [Fact]
    public void ABlastBreaksUnabsorbedShield()
    {
        var e = Build(TestSettings(), damage: 10, velocity: 0, accuracy: 1, resolution: 1, spread: 0, armor: 0,
            equipShield: true, shieldCapacity: 5);
        for (var i = 0; i < 20; i++) e.Zone.Update(.1f); // charge the (small) reserve to full
        Assert.False(e.Target.Shield.Broken);
        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 500));

        // radius 1, centred on the target's own position (its hull's centre of mass): well inside the 5x5
        // solid hull's interior, so the disc never spills off the hull and the covered share equals the whole
        // damage exactly, the same amount Splash always charged.
        FireControl.Detonate(e.Zone, e.Target.Position.xz, radius: 1, damage: 500, damageType: DamageType.Kinetic);

        Assert.True(e.Target.Shield.Broken);
    }

    // ---- 5.5: a destroyed subsystem stops being the aim point (Soul finding 10). ----

    private (ItemManager items, Ship target, EquippedItem big, EquippedItem medium) BuildRevealTarget(GameplaySettings settings)
    {
        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(6, 4), Durability = 1000, Mass = 1000
        });
        var bigShape = new Shape(2, 1);
        bigShape[new int2(0, 0)] = true;
        bigShape[new int2(1, 0)] = true;
        cache.Upsert(new GearData
        {
            Name = "Big", Hardpoint = HardpointType.Tool, Shape = bigShape, Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.Upsert(new GearData
        {
            Name = "Medium", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var target = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings());

        var bigRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Big"));
        var big = new EquippableItem { Data = bigRef, Durability = 1, Lot = 2 };
        Assert.True(target.TryFindSpace(big, out var bigPos));
        Assert.True(target.TryEquip(big, bigPos));
        var bigItem = target.Equipment.Single(e => e.EquippableItem == big);

        var mediumRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Medium"));
        var medium = new EquippableItem { Data = mediumRef, Durability = 1, Lot = 3 };
        Assert.True(target.TryFindSpace(medium, out var mediumPos));
        Assert.True(target.TryEquip(medium, mediumPos));
        var mediumItem = target.Equipment.Single(e => e.EquippableItem == medium);

        zone.Entities.Add(target);
        target.Activate();

        return (items, target, bigItem, mediumItem);
    }

    // A destroyed item drops out of the ranking entirely: it is never revealed regardless of info, and the
    // survivor's own tier closes up over the gap rather than leaving one. Mutation: drop the Durability filter
    // from IsRevealed's ranking (a destroyed item keeps its old tier and TargetItem keeps aiming at a corpse).
    [Fact]
    public void DestroyedItemStopsBeingAimPoint()
    {
        var settings = TestSettings(); // Armor .2, Gear .8
        var (items, target, big, medium) = BuildRevealTarget(settings);
        var observerHullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var observer = new Ship(items, target.Zone, new EquippableItem { Data = observerHullRef, Durability = 1000, Lot = 1 }, new EntitySettings());
        target.Zone.Entities.Add(observer);
        observer.Activate();
        observer.Target.Value = target;

        // With both items alive: Big (index 0) reveals at .2, Medium (index 1) at .8.
        observer.EntityInfoGathered[target] = .3f;
        Assert.True(FireControl.IsRevealed(observer, big));
        Assert.False(FireControl.IsRevealed(observer, medium));
        Assert.True(observer.TrySelectTargetItem(big));

        // Destroy Big: it drops out of the ranking outright, so it can never be revealed again regardless of
        // info, and it stops being a valid aim point on the very next read -- no clearing loop needed.
        big.EquippableItem.Durability = 0f;
        Assert.False(FireControl.IsRevealed(observer, big));
        Assert.Null(observer.ResolvedTargetItem);

        // Medium is now the only survivor, so it inherits index 0's own tier (.2), not its old index-1 tier
        // (.8) -- the gap closes rather than leaving Medium stranded behind a tier meant for a corpse.
        Assert.True(FireControl.IsRevealed(observer, medium));
    }

    // ---- 5.6: death removes the ship, in the simulation (Soul finding 7). ----

    // Zone itself removes a dead entity from Entities -- not only Unity's own loot-drop subscription, which
    // this test suite never runs. Mutation: remove the ObserveAdd/Death subscription from Zone's constructor
    // (or narrow it to only entities added through the deserialization loop) -- the target then never leaves
    // Entities and DeadEntityStopsTakingShots-shaped assertions would only pass by calling Remove by hand.
    [Fact]
    public void DeathRemovesShipFromSimulation()
    {
        var e = Build(TestSettings(), damage: 10000, velocity: 0, accuracy: 1, resolution: 1, spread: 0,
            hullDurability: 5, armor: 0); // one hit is lethal

        Assert.Contains(e.Target, e.Zone.Entities);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.1f);

        Assert.True(e.Target.Hull.Durability < .01f);
        Assert.DoesNotContain(e.Target, e.Zone.Entities);
    }

    // ---- 5.7: delete the two carried-and-unread fields (Soul finding 12). ----

    // PendingShot.PredictedIntercept and .FlightTime decided nothing (Commit judges deviation against
    // FireTargetPosition/FireTargetVelocity, never the stored intercept) and are gone. Mutation: re-add either
    // field to the struct.
    [Fact]
    public void PendingShotCarriesNoDeadFields()
    {
        var fields = typeof(PendingShot).GetFields().Select(f => f.Name).ToList();
        Assert.DoesNotContain("PredictedIntercept", fields);
        Assert.DoesNotContain("FlightTime", fields);
    }
}
