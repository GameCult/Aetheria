using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// AI pilots use consumables (aetheria-release cut consumable-ai, ruling sensor-stat-set: a locked target knows it is
// locked): CombatState vents when an enemy lock on the ship passes .5 and otherwise overdrives to close a target
// farther than twice its optimum range while heading within 15 degrees of it. Both go through TryActivateConsumable,
// and a consumable is chosen by what its behaviours are, never by its name -- so the vent design here is *named*
// "Overdrive" and the overdrive design "Coolant". Observations are the ship's cargo and its active effects after a
// real Zone.Update.
//
// The agent carries a gun of range 400 and one of range 1000, flat damage and no range bias, so its optimum range is
// the first sample, 400 plus a random offset below 18.75: the thresholds are bracketed (799 is below twice any
// optimum, 850 above twice every one) rather than read from the private field.
public sealed class ConsumableAiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-consumable-ai-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public ConsumableAiTests() => Directory.CreateDirectory(_root);

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

    private sealed class Scene
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Agent;
        public Ship Prey;
        public EquippedCargoBay Hold;
        public ConsumableItemData VentDesign, OverdriveDesign, DecoyDesign;
        public CultRecordRef<Faction> Maker;
        public Func<LockWeaponData, Ship> NewLocker;
        public readonly List<Ship> Lockers = new List<Ship>();

        public void Store(ConsumableItemData design) =>
            Assert.True(Hold.TryStore((ItemInstance) Items.CreateInstance(Items.CreateLot(design, Maker, .5f))));

        public bool Active(ConsumableItemData design) => Agent.FindActiveConsumable(design) != null;
        public int Carried => Hold.Cargo.Count;
    }

    // The agent, its prey (a ship with a lock gun that targets nobody) and `lockers`: each a ship whose lock gun
    // builds `lock` on its target in the first tick, then is turned away so the lock holds (Decay 0).
    private Scene Build(float preyDistance, params (float lockSpeed, bool onAgent)[] lockers)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetArmorInfoThreshold = .2f, TargetGearInfoThreshold = .8f,
            FiringArc = 360, SchematicCellSize = 1f
        };
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
        cache.Upsert(new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = Solid(5, 5), Durability = 1000000, Mass = 1000,
            Hardpoints =
            {
                new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = new int2(1, 0), Shape = new Shape() }
            }
        });
        GearData Gear(string name, BehaviorData behavior) => new GearData
        {
            Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { behavior }
        };
        InstantWeaponData Gun(float range) => new InstantWeaponData
        {
            Damage = Constant(10), Range = Constant(range), MinRange = Constant(0), Velocity = Constant(0),
            Cooldown = Constant(1),
            DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
        };
        cache.Upsert(Gear("ShortGun", Gun(400)));
        cache.Upsert(Gear("LongGun", Gun(1000)));
        cache.Upsert(new CargoBayData { Name = "Hold", Shape = new Shape(), InteriorShape = Solid(4, 1), Durability = 10 });
        cache.Upsert(new ConsumableItemData { Name = "Flare", Duration = 1f, Shape = new Shape() });
        cache.Upsert(new ConsumableItemData
        {
            Name = "Overdrive", Duration = 100f, Shape = new Shape(), // a vent, named like the other kind
            Behaviors = { new VapourDumpData { Radius = Constant(20), Opacity = Constant(.9f), Lifetime = Constant(100) } }
        });
        cache.Upsert(new ConsumableItemData
        {
            Name = "Coolant", Duration = 100f, Shape = new Shape(), // an overdrive, named like the other kind
            Behaviors = { new ThrottleLockData() }
        });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        EquippableItem Make<T>(string name) where T : CraftedItemData =>
            (EquippableItem) items.CreateInstance(items.CreateLot(cache.GetByName<T>(name), maker, .5f));
        var zone = new Zone(items, new PlanetSettings { }, new ZonePack { Radius = 5000 }, new GalaxyZone { Name = "Ai", Owner = null }, null);
        var hull = cache.GetByName<HullData>("Hull");
        Ship NewShip() => new Ship(items, zone, (EquippableItem) items.CreateInstance(items.CreateLot(hull, maker, .5f)), new EntitySettings());
        var scene = new Scene
        {
            Items = items, Zone = zone, Maker = maker,
            VentDesign = cache.GetByName<ConsumableItemData>("Overdrive"),
            OverdriveDesign = cache.GetByName<ConsumableItemData>("Coolant"),
            DecoyDesign = cache.GetByName<ConsumableItemData>("Flare")
        };

        scene.Agent = NewShip();
        Assert.True(scene.Agent.TryEquip(Make<GearData>("ShortGun"), new int2(0, 0)));
        Assert.True(scene.Agent.TryEquip(Make<GearData>("LongGun"), new int2(1, 0)));
        Assert.True(scene.Agent.TryEquip(Make<CargoBayData>("Hold"), new int2(3, 3)));
        scene.Hold = scene.Agent.CargoBays.Single();

        scene.NewLocker = data =>
        {
            var name = "LockGun" + Guid.NewGuid().ToString("N");
            var gear = Gear(name, data);
            cache.Upsert(gear);
            var ship = NewShip();
            Assert.True(ship.TryEquip((EquippableItem) items.CreateInstance(items.CreateLot(gear, maker, .5f)), new int2(0, 0)));
            return ship;
        };
        LockWeaponData LockGun(float speed) => new LockWeaponData
        {
            Damage = Constant(0), Range = Constant(2000), MinRange = Constant(0), Velocity = Constant(0),
            Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0), Count = Constant(1),
            BurstTime = Constant(0), Cooldown = Constant(100000),
            DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
            LockSpeed = Constant(speed), SensorImpact = Constant(1), LockAngle = Constant(30),
            DirectionImpact = Constant(1), Decay = Constant(0)
        };

        scene.Prey = scene.NewLocker(LockGun(0));
        var ships = new List<Ship> { scene.Agent, scene.Prey };
        foreach (var (speed, _) in lockers)
        {
            var locker = scene.NewLocker(LockGun(speed));
            scene.Lockers.Add(locker);
            ships.Add(locker);
        }
        foreach (var ship in ships) zone.Entities.Add(ship);
        foreach (var ship in ships) ship.Activate();
        foreach (var other in ships.Skip(1))
        {
            scene.Agent.SetIff(other, true);
            other.SetIff(scene.Agent, true);
        }
        zone.Update(0f);

        // Full information both ways: the agent sees its prey first, so its Minion targets the prey.
        zone.Agents.Add(new Minion(scene.Agent));
        scene.Agent.Position = float3.zero;
        scene.Prey.Position = float3(0, 0, preyDistance);
        scene.Agent.EntityInfoGathered[scene.Prey] = 1f;
        for (var i = 0; i < scene.Lockers.Count; i++)
        {
            var locker = scene.Lockers[i];
            locker.Position = float3(30 * (i + 1), 0, 30);
            scene.Agent.EntityInfoGathered[locker] = 1f;
            locker.EntityInfoGathered[scene.Agent] = 1f;
            locker.EntityInfoGathered[scene.Prey] = 1f;
        }
        Assert.Equal(scene.Prey, scene.Agent.Target.Value.Entity);
        for (var i = 0; i < scene.Lockers.Count; i++)
        {
            var locker = scene.Lockers[i];
            locker.SetTarget(lockers[i].onAgent ? scene.Agent : (Entity) scene.Prey);
            Aim(locker, towards: true);
        }
        return scene;
    }

    private static void Aim(Ship locker, bool towards)
    {
        var at = locker.Target.Value.Entity.Position - locker.Position;
        locker.Aim = towards ? normalize(at) : -normalize(at);
    }

    // One tick builds each locker's lock (dt 1, info 1, dead ahead: lock = LockSpeed); the next tick the lockers
    // look away, so each lock holds at exactly that value while the agent reads it.
    private static void HoldLocks(Scene s)
    {
        s.Zone.Update(1f); // transitions the Minion into combat and builds the locks
        foreach (var locker in s.Lockers) Aim(locker, towards: false);
    }

    private static void Face(Scene s, float degrees)
    {
        var r = radians(degrees);
        s.Agent.Direction = float2(sin(r), cos(r));
        s.Agent.Position = float3.zero;
    }

    private static void Settle(Scene s)
    {
        foreach (var ship in new[] { s.Agent, s.Prey }.Concat(s.Lockers)) ship.Velocity = float2.zero;
        s.Zone.Update(1f);
    }

    [Theory]
    [InlineData(.4f, false)]
    [InlineData(.5f, false)]
    [InlineData(.6f, true)]
    public void AnAgentVentsWhenLocked(float lockValue, bool vents)
    {
        var s = Build(100, (lockValue, true));
        HoldLocks(s);
        Assert.Equal(lockValue, s.Lockers[0].Weapons.OfType<LockWeapon>().Single().Lock, 3);
        s.Store(s.DecoyDesign);
        s.Store(s.OverdriveDesign);
        s.Store(s.VentDesign);
        Face(s, 0);

        Settle(s);

        Assert.Equal(vents, s.Active(s.VentDesign));
        Assert.False(s.Active(s.OverdriveDesign));
        Assert.False(s.Active(s.DecoyDesign));
        Assert.Equal(vents ? 2 : 3, s.Carried);
    }

    // The lock on the ship is what counts: two locks of .3 do not add up to one that passes half, a lock held on
    // someone else is not a lock on this ship, and a locker the agent cannot see is not among its visible enemies.
    [Fact]
    public void AnAgentVentsOnOneLockOnItselfThatItCanSee()
    {
        var scattered = Build(100, (.3f, true), (.3f, true));
        HoldLocks(scattered);
        scattered.Store(scattered.VentDesign);
        Face(scattered, 0);
        Settle(scattered);
        Assert.False(scattered.Active(scattered.VentDesign));

        var elsewhere = Build(100, (.9f, false));
        HoldLocks(elsewhere);
        elsewhere.Store(elsewhere.VentDesign);
        Face(elsewhere, 0);
        Settle(elsewhere);
        Assert.False(elsewhere.Active(elsewhere.VentDesign));

        var unseen = Build(100, (.9f, true));
        HoldLocks(unseen);
        unseen.Agent.EntityInfoGathered[unseen.Lockers[0]] = 0f;
        Assert.DoesNotContain(unseen.Lockers[0], unseen.Agent.VisibleEnemies);
        unseen.Store(unseen.VentDesign);
        Face(unseen, 0);
        Settle(unseen);
        Assert.False(unseen.Active(unseen.VentDesign));

        var painted = Build(100, (.3f, true), (.9f, true));
        HoldLocks(painted);
        painted.Store(painted.VentDesign);
        Face(painted, 0);
        Settle(painted);
        Assert.True(painted.Active(painted.VentDesign));
    }

    // Venting is the answer to a lock; the surge to close distance is the answer when nobody is locking.
    [Fact]
    public void AnAgentThatIsLockedVentsInsteadOfOverdriving()
    {
        var s = Build(1300, (.6f, true));
        HoldLocks(s);
        s.Store(s.OverdriveDesign);
        s.Store(s.VentDesign);
        Face(s, 0);

        Settle(s);

        Assert.True(s.Active(s.VentDesign));
        Assert.False(s.Active(s.OverdriveDesign));
        Assert.False(s.Agent.ThrottleLocked);
    }

    // Past twice the optimum range (400 to 418.75 here) and within 15 degrees of the target; a lock under half
    // does not stop it.
    [Theory]
    [InlineData(1300, 0, 0f, true)]
    [InlineData(850, 0, 0f, true)]
    [InlineData(850, 0, .4f, true)]
    [InlineData(850, 10, 0f, true)]
    [InlineData(799, 0, 0f, false)]
    [InlineData(700, 0, 0f, false)]
    [InlineData(410, 0, 0f, false)]
    [InlineData(1300, 20, 0f, false)]
    [InlineData(1300, 30, 0f, false)]
    [InlineData(1300, 180, 0f, false)]
    public void AnAgentOverdrivesToClose(float distance, float degreesOff, float lockValue, bool overdrives)
    {
        var s = lockValue > 0 ? Build(distance, (lockValue, true)) : Build(distance);
        HoldLocks(s);
        s.Store(s.DecoyDesign);
        s.Store(s.VentDesign);
        s.Store(s.OverdriveDesign);
        Face(s, degreesOff);

        Settle(s);

        Assert.Equal(overdrives, s.Active(s.OverdriveDesign));
        Assert.Equal(overdrives, s.Agent.ThrottleLocked);
        Assert.False(s.Active(s.VentDesign));
        Assert.Equal(overdrives ? 2 : 3, s.Carried);
    }

    // With nothing to use, or nothing of the right kind, the same situations change nothing and do not throw.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAgentWithoutConsumablesDoesNothing(bool carriesOnlyADecoy)
    {
        var s = Build(1300, (.6f, true));
        HoldLocks(s);
        if (carriesOnlyADecoy) s.Store(s.DecoyDesign);
        Face(s, 0);

        var thrown = Record.Exception(() =>
        {
            Settle(s);
            Settle(s);
        });

        Assert.Null(thrown);
        Assert.False(s.Active(s.VentDesign));
        Assert.False(s.Active(s.OverdriveDesign));
        Assert.False(s.Active(s.DecoyDesign));
        Assert.False(s.Agent.ThrottleLocked);
        Assert.Equal(carriesOnlyADecoy ? 1 : 0, s.Carried);
    }
}
