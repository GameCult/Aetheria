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

// Cut 6b (docs/fire-control-cut.md, "the dice stop being shared, and the flak cannon works again"): pins 6.1
// (a shot's dice are a pure function of zone identity and shot id, not a shared stream) and 6.2 (airburst
// resolves in the simulation, via Splash instead of Apply, never both). Builds its own fixtures, the same
// convention every earlier cut's test file establishes.
public sealed class FireControlCut6Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut6-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut6Tests() => Directory.CreateDirectory(_root);

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

    // ---- A discrete-weapon shooter/target engagement, the same shape FireControlCut5Tests.Build uses. Every
    // engagement shares the fixed GalaxyZone name "Test", so every Zone built from this fixture shares one
    // CombatSeed (R1's stable identity) -- exactly the substrate 6.1's determinism claim depends on. ----

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
        float damage = 10, float range = 1000, float velocity = 0, float spread = 0,
        float accuracy = 1, float resolution = 1, float tracking = 100000,
        float targetRange = 100, bool airburst = false, float airburstRange = 0)
    {
        var hullShape = SolidShape(5, 5);
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 100000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        // Cut 6b, 6.2: a real WeaponItemData (not the bare GearData earlier cuts' fixtures use) -- it is the
        // one catalog type carrying WeaponModifiers (the Airburst flag) and, since this cut, AirburstRange.
        cache.Upsert(new WeaponItemData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            WeaponModifiers = airburst ? WeaponModifiers.Airburst : WeaponModifiers.None,
            AirburstRange = airburstRange,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(damage), Range = Constant(range), MinRange = Constant(0),
                Velocity = Constant(velocity), Spread = Constant(spread), DamageSpread = Constant(0),
                Penetration = Constant(0), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData
            {
                Accuracy = Constant(accuracy), Resolution = Constant(resolution), Precision = Constant(0), Tracking = Constant(tracking)
            } }
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
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 100000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<WeaponItemData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 100000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        // A target with no non-hull equipment never runs TryEquip, so Entity's _orderedEquipment (only ever
        // populated there) stays null and Entity.Update NREs on the very first tick -- give it an inert item
        // purely so that array gets initialized, the same reason FireControlCut5Tests' fixtures always equip
        // a target gun.
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<WeaponItemData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 5 };
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

        zone.Update(0f); // warm up resolved stats

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon };
    }

    // ---- 6.1: a shot's dice belong to the shot (Soul finding 6). ----

    // A fixed fight, run twice from the same zone identity (CombatSeed comes from the fixed GalaxyZone name
    // "Test", shared by both engagements), with an arbitrary number of unrelated draws from ItemManager.Random
    // between shots in the second run. Outcomes must be identical shot-for-shot. Mutation: restore
    // `shot.Source.ItemManager.Random` as Commit's generator -- the unrelated draws below then advance the
    // shared stream Commit reads from, so the second run's hit/cell sequence diverges from the first's. This
    // is the test Soul's own hermetic-fixture probe could never be (docs/fire-control-cut.md, 6.1): a fixture
    // that never draws anything unrelated cannot see a shared-stream defect.
    [Fact]
    public void SameFightRollsSameThroughUnrelatedDraws()
    {
        var settings = TestSettings();

        List<(bool hit, int2 cell)> RunFight(Action<ItemManager, int> unrelatedDraws)
        {
            var e = Build(settings, damage: 10, velocity: 0, accuracy: 1, resolution: 1, tracking: 100000, targetRange: 100);
            var outcomes = new List<(bool, int2)>();
            e.Zone.ShotResolved.Subscribe(o => outcomes.Add((o.Hit, o.Cell)));
            for (var i = 0; i < 8; i++)
            {
                unrelatedDraws?.Invoke(e.Items, i);
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
                e.Zone.Update(.01f); // flight time 0 (velocity 0) -> commits and resolves in this one call
            }
            return outcomes;
        }

        var baseline = RunFight(null);
        // A different, growing number of unrelated draws before each shot -- exactly "how many draws came
        // before it" perturbing a shared stream would be sensitive to.
        var perturbed = RunFight((items, i) => { for (var k = 0; k < i * 3 + 1; k++) items.Random.NextFloat(); });

        Assert.Equal(baseline.Count, perturbed.Count);
        Assert.True(baseline.Count > 0);
        Assert.Equal(baseline, perturbed);
    }

    // A shot's outcome is a function of its own id: firing many shots at an identical, unmoving target (same
    // frozen payload in every way but ShotId, which increments per shot) must not always land on the same
    // cell. Mutation: drop ShotId from the seed -- every shot in a zone would then build the exact same local
    // generator, so every one of these draws the same cell every time (no variety at all across the run).
    [Fact]
    public void ShotIdDecidesTheDie()
    {
        var settings = TestSettings();
        var e = Build(settings, damage: 10, velocity: 0, accuracy: 1, resolution: 1, tracking: 100000, targetRange: 100);

        var cells = new List<int2>();
        e.Zone.ShotResolved.Subscribe(o => { if (o.Hit) cells.Add(o.Cell); });

        for (var i = 0; i < 12; i++)
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }

        Assert.True(cells.Count > 1);
        Assert.True(cells.Distinct().Count() > 1, "Every shot landed on the same cell -- ShotId is not reaching the seed.");
    }

    // ---- 6.2: airburst resolves in the simulation (Soul finding 5). ----

    // An airburst shot resolves via Splash, and Splash does not gate on the discrete hit roll at all -- it
    // damages everything in radius unconditionally (Cut 4's rule). Accuracy 0 pins PBase (and so Commit's hit)
    // to false with no dependence on the random draw, so a normal discrete shot would apply no damage
    // whatsoever (Apply's own `if (!shot.Outcome.Hit) return;`). An airburst shot must still deal its area
    // damage. Mutation: call Apply as well as Splash -- harmless here (Apply still no-ops on a miss), so this
    // test alone does not catch that mutation; AirburstAndDiscreteNeverDoubleUp below does, with a guaranteed
    // hit where an extra Apply call is not a no-op.
    [Fact]
    public void AirburstSplashesEvenOnAGuaranteedMiss()
    {
        var settings = TestSettings();
        var e = Build(settings, damage: 500, velocity: 0, accuracy: 0, resolution: 1, tracking: 100000,
            targetRange: 50, airburst: true, airburstRange: 100);

        var before = e.Target.Hull.Durability;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.True(e.Target.Hull.Durability < before, "An airburst shot must splash regardless of the discrete hit roll.");
    }

    // The double-application Soul was told to hunt for: with a guaranteed hit (accuracy 1), an airburst shot's
    // total damage must equal a bare Splash call's own damage -- not more. Mutation: call Apply as well as
    // Splash at arrival -- Apply's own discrete cell hit would land on top of Splash's area damage, so the
    // airburst shot's total damage exceeds a lone Splash call's.
    [Fact]
    public void AirburstAndDiscreteNeverDoubleUp()
    {
        var settings = TestSettings();

        var e = Build(settings, damage: 10, velocity: 0, accuracy: 1, resolution: 1, tracking: 100000,
            targetRange: 50, airburst: true, airburstRange: 100);
        var before = e.Target.Hull.Durability;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        var actualDelta = before - e.Target.Hull.Durability;

        // A fresh, identically-shaped target, damaged by one bare Splash call with the same parameters
        // FireControl.Fire freezes for this weapon (BurstPosition, for a stationary velocity-0 target, is
        // exactly its own position -- PredictedIntercept falls back to target.Position below weapon.Velocity's
        // .01f floor).
        var e2 = Build(settings, damage: 10, velocity: 0, accuracy: 1, resolution: 1, tracking: 100000,
            targetRange: 50, airburst: true, airburstRange: 100);
        var before2 = e2.Target.Hull.Durability;
        FireControl.Splash(e2.Zone, e2.Target.Position, 100, 10, DamageType.Kinetic);
        var expectedDelta = before2 - e2.Target.Hull.Durability;

        Assert.True(expectedDelta > 0);
        Assert.Equal(expectedDelta, actualDelta, 3);
    }

    // The other side: a non-airburst shot must never splash, even one that lands exactly on its own frozen
    // burst position at zero range (a stationary target, so BurstPosition == the target's own position at
    // arrival) -- the zero-radius edge case a careless "splash unconditionally" mutant would still pass
    // through, since distance 0 is never greater than radius 0. Accuracy 0 pins the discrete roll to a
    // guaranteed miss, so the correct path (Apply, gated on Outcome.Hit) deals no damage at all. Mutation:
    // splash unconditionally at arrival instead of gating on BurstRadius -- Splash does not check Hit, so it
    // would still deal damage even though this shot never carried the Airburst flag.
    [Fact]
    public void NonAirburstNeverSplashes()
    {
        var settings = TestSettings();
        var e = Build(settings, damage: 1000, velocity: 0, accuracy: 0, resolution: 1, tracking: 100000,
            targetRange: 50, airburst: false);

        var before = e.Target.Hull.Durability;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.Equal(before, e.Target.Hull.Durability);
    }

    // ---- negative: ItemManager.Random no longer appears in FireControl.cs; Airburst no longer appears
    // anywhere under Assets/Scripts/Gameplay (Projectile.cs's fields and ProjectileManager.cs's write-back are
    // deleted, not merely unused). Enforced by tests/mutation_tests_fire_control_cut6.sh's negative-grep step,
    // not restated here as a source-scanning unit test.
}
