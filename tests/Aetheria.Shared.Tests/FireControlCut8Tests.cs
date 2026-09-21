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

// Cut 8 (docs/fire-control-cut.md, "a removed entity is a dead entity, and the anchors the diagnostics
// moved"): pins 8.1 (removal and deactivation are one transition) and 8.2 (a deactivated entity does not
// update). Builds its own fixtures, the same convention every earlier cut's test file establishes.
public sealed class FireControlCut8Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut8-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut8Tests() => Directory.CreateDirectory(_root);

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

    // ---- A discrete-weapon shooter/target engagement, the same shape FireControlCut5Tests.Build uses. ----

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
    }

    private Engagement Build(GameplaySettings settings, float damage = 10, float hullDurability = 1000)
    {
        var hullShape = SolidShape(5, 5);
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = hullDurability, Mass = 1000,
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
                Damage = Constant(damage), Range = Constant(1000), MinRange = Constant(0),
                Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(0),
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
                Accuracy = Constant(1), Resolution = Constant(1), Precision = Constant(1000), Tracking = Constant(1000)
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
        var shooterHull = new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 5 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = 3 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 4 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, 100);
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        zone.Update(0f); // warm up resolved stats

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon };
    }

    // ---- A LockWeapon-carrying ship with a live target, the exact indexer shape from the operator's own
    // crash (LockWeapon.cs:98, docs/fire-control-cut.md Cut 8 header): Entity.EntityInfoGathered[Entity.Target.Value]
    // read through a raw indexer. ----

    private sealed class LockScenario
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Locker;
        public Ship Bystander;
    }

    private LockScenario BuildLockScenario(GameplaySettings settings)
    {
        var hullShape = SolidShape(5, 5);
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 1000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(new GearData
        {
            Name = "LockGun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new LockWeaponData
            {
                Damage = Constant(0), Range = Constant(1000), MinRange = Constant(0), Velocity = Constant(0),
                Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0), Count = Constant(1),
                BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
                LockSpeed = Constant(1), SensorImpact = Constant(1), LockAngle = Constant(170),
                DirectionImpact = Constant(1), Decay = Constant(1)
            } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var lockerHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 };
        var locker = new Ship(items, zone, lockerHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("LockGun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(locker.TryEquip(gun, new int2(0, 0)));

        var bystanderHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 3 };
        var bystander = new Ship(items, zone, bystanderHull, new EntitySettings());
        var bystanderGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("LockGun"));
        var bystanderGun = new EquippableItem { Data = bystanderGunRef, Durability = 1, Lot = 4 };
        Assert.True(bystander.TryEquip(bystanderGun, new int2(0, 0))); // _orderedEquipment must not be empty/null for Update to run

        zone.Entities.Add(locker);
        zone.Entities.Add(bystander);
        locker.Activate();
        bystander.Activate();

        locker.Position = float3.zero;
        bystander.Position = float3(0, 0, 100);
        locker.LookDirection = float3(0, 0, 1);
        locker.Target.Value = bystander;
        locker.EntityInfoGathered[bystander] = 1f;
        locker.SetIff(bystander, true);

        zone.Update(0f); // warm up resolved stats -- brings weapon Online/Active up

        return new LockScenario { Items = items, Zone = zone, Locker = locker, Bystander = bystander };
    }

    // ---- 8.1/8.2: removal and deactivation are one transition, and a deactivated entity does not update. ----

    // The always-runs prefix of Entity.Update (TargetRange, temperature, visibility decay, equipment
    // performance) used to execute unconditionally, even on an entity that had already been torn down --
    // Deactivate() clears EntityInfoGathered/VisibleEntities/etc, but nothing stopped Update from still
    // touching the rest of the entity's state every subsequent call it remained reachable (a stale snapshot
    // slot in the same Zone.Update pass, or a docked/child entity still walked by its parent's own Children
    // loop). Mutation: drop the `if (!_active) return;` guard -- TargetRange keeps recomputing against a
    // live target after the entity is dead, instead of freezing at its last value.
    [Fact]
    public void DeadEntityDoesNotUpdate()
    {
        var e = Build(TestSettings());
        e.Target.Target.Value = e.Shooter; // give the target its own TargetRange to freeze
        e.Zone.Update(.1f);
        var rangeBeforeDeath = e.Target.TargetRange;

        e.Target.Deactivate();
        Assert.False(e.Target.Active);

        e.Shooter.Position += float3(1000, 0, 0); // a live target would recompute a very different TargetRange
        e.Target.Update(.1f);

        Assert.Equal(rangeBeforeDeath, e.Target.TargetRange);
    }

    // The named crash shape, as a regression pin: LockWeapon.cs:98 reaches
    // Entity.EntityInfoGathered[Entity.Target.Value] through a raw indexer. Deactivate() clears
    // EntityInfoGathered but does not null the entity's own Target -- so a torn-down entity whose Target is
    // still set is exactly the shape of the operator's crash. This must never throw, however the entity came
    // to be inactive.
    [Fact]
    public void LockWeaponOnDeactivatedEntityDoesNotThrow()
    {
        var s = BuildLockScenario(TestSettings());
        s.Locker.Update(.1f); // let the lock behavior run once while alive, same as normal play

        s.Locker.Deactivate();
        Assert.False(s.Locker.Active);
        Assert.DoesNotContain(s.Bystander, s.Locker.EntityInfoGathered.Keys);
        Assert.Equal(s.Bystander, s.Locker.Target.Value); // Deactivate does not null the entity's own Target

        var ex = Record.Exception(() => s.Locker.Update(.1f));
        Assert.Null(ex);
    }

    // 8.1: Zone's own Death subscription must pair removal with deactivation, exactly like TryDock always has.
    // Mutation: remove the Deactivate() call from Zone's Death subscription -- the target leaves Entities but
    // stays active, holding live subscriptions and a stale, uncleared EntityInfoGathered/VisibleEntities.
    [Fact]
    public void DeathDeactivates()
    {
        var e = Build(TestSettings(), damage: 10000, hullDurability: 5); // one hit is lethal
        Assert.True(e.Target.Active);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.1f);

        Assert.True(e.Target.Hull.Durability < .01f);
        Assert.DoesNotContain(e.Target, e.Zone.Entities);
        Assert.False(e.Target.Active);
        Assert.Empty(e.Target.EntityInfoGathered);
        Assert.Empty(e.Target.VisibleEntities);
    }

    // ---- Ordering risk (8.1's own callout): EntityInstance's loot-drop subscription (EntityInstance.cs:292-325)
    // reads Entity.Equipment on Entity.HullDamage, independent of Entity.Death/Deactivate. EntityInstance
    // itself is a MonoBehaviour and cannot run headlessly (it lives in the Gameplay assembly, not
    // Aetheria.Shared), so the actual drop call (ZoneRenderer.DropItem) is an operator check, not something
    // this suite reaches. What this suite CAN confirm headlessly is the data precondition that call depends
    // on: Deactivate() must not clear Equipment, regardless of subscription order relative to Zone's own
    // Death-triggered removal. ----

    // Mirrors EntityInstance's own subscription shape (HullDamage, re-checking Durability itself) established
    // AFTER the target has already joined the zone -- so Zone's Death-derived Deactivate (subscribed at join
    // time, per 8.1) fires first on the same HullDamage.OnNext call, and this handler observes Equipment
    // strictly after that. If Deactivate ever starts clearing Equipment, this goes red.
    [Fact]
    public void LootStillDropsOnDeath_EquipmentSurvivesDeactivate()
    {
        var e = Build(TestSettings(), damage: 10000, hullDurability: 5); // one hit is lethal

        List<EquippedItem> equipmentAtDeath = null;
        e.Target.HullDamage.Subscribe(_ =>
        {
            if (e.Target.Hull.Durability < .01f) equipmentAtDeath = e.Target.Equipment.ToList();
        });

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.1f);

        Assert.True(e.Target.Hull.Durability < .01f);
        Assert.False(e.Target.Active); // Zone's Death subscription (subscribed before this test's own) already
                                        // deactivated the target by the time this handler runs.
        Assert.NotNull(equipmentAtDeath);
        Assert.NotEmpty(equipmentAtDeath); // the target's own gun -- Deactivate must not have cleared Equipment

        // Operator check, not reachable headlessly: confirm in play that ZoneRenderer.DropItem still fires for
        // each surviving equipment/cargo entry (EntityInstance.cs:292-325) after this cut, i.e. that loot
        // actually drops on a live kill, not only that the data behind it survived Deactivate().
    }
}
