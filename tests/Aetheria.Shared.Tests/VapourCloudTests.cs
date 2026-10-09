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

// Vapour clouds in the sim (aetheria-release cut vapour-cloud, ruling vapour-cloud-obscuration): a VapourDump
// behaviour vents a zone cloud that thins every sensor sight line through it, both ways, through Sensor.Gain's
// one detection rule; a lock falls by the existing detection-threshold rule and nothing else. The scene is two
// ships facing each other along z with a fixed visibility, so what a sensor gathers is a number the clouds
// alone change: steady info is visibility x sensitivity / (distance x TargetInfoDecay), and visibility 100,
// sensitivity 1, decay .5 puts it at .5 at range 400 (above the .1 threshold) and saturated at range 40.
public sealed class VapourCloudTests : IDisposable
{
    private const float Dt = .05f;
    private const float Threshold = .1f;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-vapour-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();
    private static readonly object Glow = new object();

    public VapourCloudTests() => Directory.CreateDirectory(_root);

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
        public Ship Observer;
        public Ship Target;
        public EquippedItem Dump;
        public LockWeapon Lock;
        public int Lot = 1;
        public bool HoldTarget = true;
    }

    private Lab Build(float distance, float radius = 20f, float opacity = .9f, float lifetime = 1000f)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = Threshold, TargetArmorInfoThreshold = .2f, TargetGearInfoThreshold = .8f,
            TargetInfoDecay = .5f, VisibilityDecay = 0f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 1f
        };
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = Solid(5, 5), Durability = 1000000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        });
        GearData Gear(string name, HardpointType hardpoint, BehaviorData behavior) => new GearData
        {
            Name = name, Hardpoint = hardpoint, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { behavior }
        };
        cache.Upsert(Gear("Eye", HardpointType.Tool, new SensorData
        {
            Sensitivity = Constant(1f),
            SensitivityCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
            PingBoost = Constant(.02f), PingEnergy = Constant(0f), PingVisibility = Constant(5f),
            PingRange = Constant(300f), PingCooldown = Constant(4f)
        }));
        cache.Upsert(Gear("Vent", HardpointType.Tool, new VapourDumpData
        {
            Radius = Constant(radius), Opacity = Constant(opacity), Lifetime = Constant(lifetime)
        }));
        cache.Upsert(Gear("LockGun", HardpointType.Sensors, new LockWeaponData
        {
            Damage = Constant(0), Range = Constant(1000), MinRange = Constant(0), Velocity = Constant(0),
            Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0), Count = Constant(1),
            BurstTime = Constant(0), Cooldown = Constant(1000),
            DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
            LockSpeed = Constant(1), SensorImpact = Constant(1), LockAngle = Constant(170),
            DirectionImpact = Constant(1), Decay = Constant(1)
        }));
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 50; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack { Radius = 5000 }, new GalaxyZone { Name = "Vapour", Owner = null }, null);
        var lab = new Lab { Items = items, Zone = zone };

        EquippableItem Make(string name) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>(name)), Durability = 1, Lot = lab.Lot++
        };
        Ship NewShip()
        {
            var hull = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
            var ship = new Ship(items, zone, new EquippableItem { Data = hull, Durability = 1000000, Lot = lab.Lot++ }, new EntitySettings());
            Assert.True(ship.TryEquip(Make("Eye")));
            return ship;
        }

        lab.Observer = NewShip();
        Assert.True(lab.Observer.TryEquip(Make("LockGun"), new int2(0, 0)));
        lab.Target = NewShip();
        var dump = Make("Vent");
        Assert.True(lab.Target.TryEquip(dump));
        lab.Dump = lab.Target.Equipment.Single(x => x.EquippableItem == dump);
        // Held off until a test pulls the trigger: the dump vents on its first run.
        lab.Dump.Enabled.Value = false;

        zone.Entities.Add(lab.Observer);
        zone.Entities.Add(lab.Target);
        lab.Observer.Activate();
        lab.Target.Activate();
        lab.Observer.Position = float3(0, 0, 0);
        lab.Observer.Aim = float3(0, 0, 1);
        lab.Target.Position = float3(0, 0, distance);
        lab.Target.Aim = float3(0, 0, -1);
        lab.Observer.SetIff(lab.Target, true);
        lab.Target.SetIff(lab.Observer, true);
        zone.Update(0f);
        lab.Lock = lab.Observer.Weapons.OfType<LockWeapon>().Single();
        return lab;
    }

    // One tick: both ships stay where they were put, facing each other, each a fixed visibility to the other's sensor.
    private static void Tick(Lab lab)
    {
        foreach (var ship in new[] { lab.Observer, lab.Target })
        {
            ship.VisibilitySources[Glow] = 100f;
            if (ship != lab.Target || lab.HoldTarget) ship.Velocity = float2.zero;
        }
        lab.Observer.Direction = float2(0, 1);
        lab.Target.Direction = float2(0, -1);
        lab.Zone.Update(Dt);
    }

    private static void Run(Lab lab, int ticks)
    {
        for (var i = 0; i < ticks; i++) Tick(lab);
    }

    private static float Info(Ship watcher, Ship watched) => watcher.EntityInfoGathered[watched];

    // The observer has settled on the target and holds a full lock on it: the state a cloud has to break.
    private static void Establish(Lab lab)
    {
        Run(lab, 100);
        Assert.True(Info(lab.Observer, lab.Target) > Threshold, "the observer never gathered the target");
        Assert.Contains(lab.Target, lab.Observer.VisibleEnemies);
        lab.Observer.SetTarget(lab.Target);
        Run(lab, 100);
        Assert.True(lab.Lock.IsLocked, "the observer never locked the target");
        Assert.Empty(lab.Zone.Clouds);
    }

    private static void Vent(Lab lab)
    {
        lab.Dump.Enabled.Value = true;
        for (var i = 0; i < 5 && lab.Zone.Clouds.Count == 0; i++) Tick(lab);
        Assert.Single(lab.Zone.Clouds);
    }

    private static VapourCloud Hand(Zone zone, float2 at, float radius, float opacity, float lifetime = 1000f)
    {
        var cloud = new VapourCloud { Body = new KinematicBody { Position = at }, Radius = radius, Opacity = opacity, Lifetime = lifetime };
        zone.Vent(cloud);
        return cloud;
    }

    [Fact]
    public void ACloudFadesAndIsRemoved()
    {
        var lab = Build(400f);
        var cloud = Hand(lab.Zone, float2(0, 0), 10f, .8f, lifetime: 10f);
        var a = float2(-5, 0);
        var b = float2(5, 0);
        Assert.Equal(.2f, lab.Zone.Obscuration(a, b), 5);

        for (var i = 0; i < 5; i++) lab.Zone.Update(1f);
        Assert.Equal(.4f, cloud.OpacityAt(lab.Zone.Time), 5);
        Assert.Equal(.6f, lab.Zone.Obscuration(a, b), 5);
        Assert.Single(lab.Zone.Clouds);

        for (var i = 0; i < 5; i++) lab.Zone.Update(1f);
        Assert.Empty(lab.Zone.Clouds);
        Assert.Equal(1f, lab.Zone.Obscuration(a, b));
    }

    [Fact]
    public void ACloudWithNoLifetimeObscuresNothing()
    {
        var lab = Build(400f);
        var cloud = Hand(lab.Zone, float2(0, 0), 10f, .8f, lifetime: 0f);
        Assert.Equal(0f, cloud.OpacityAt(lab.Zone.Time));
        Assert.Equal(1f, lab.Zone.Obscuration(float2(-5, 0), float2(5, 0)));
    }

    [Fact]
    public void AnEffectVentsOnce()
    {
        var lab = Build(400f);
        lab.HoldTarget = false;
        Run(lab, 2);
        lab.Target.Velocity = float2(3, 0);
        VapourCloud vented = null;
        float2 venterVelocity = float2.zero;
        float2 venterAt = float2.zero;
        using var sub = lab.Zone.Clouds.ObserveAdd().Subscribe(add =>
        {
            vented = add.Value;
            venterVelocity = lab.Target.Velocity;
            venterAt = lab.Target.Position.xz;
        });

        lab.Dump.Enabled.Value = true;
        Run(lab, 100);

        Assert.Single(lab.Zone.Clouds);
        Assert.Same(vented, lab.Zone.Clouds.Single());
        Assert.True(length(venterVelocity) > 0f, "the venter was not moving");
        Assert.Equal(venterVelocity, vented.Body.Velocity);
        Assert.Equal(venterAt, vented.Body.Position);
        Assert.Same(lab.Target, vented.Venter);
    }

    [Fact]
    public void ACloudThinsOnlyTheSightLinesItTouches()
    {
        var lab = Build(400f);
        Hand(lab.Zone, float2(0, 0), 10f, .6f);
        float Through(float2 a, float2 b) => lab.Zone.Obscuration(a, b);

        Assert.Equal(.4f, Through(float2(-50, 0), float2(50, 0)), 5);
        Assert.Equal(.4f, Through(float2(50, 0), float2(-50, 0)), 5);
        // Passing wide of the centre but within the radius, neither end inside the disc.
        Assert.Equal(.4f, Through(float2(-50, 9.9f), float2(50, 9.9f)), 5);
        Assert.Equal(1f, Through(float2(-50, 10.1f), float2(50, 10.1f)));
        // Ending inside the disc, and a point inside it (the observer inside its own cloud).
        Assert.Equal(.4f, Through(float2(-50, 0), float2(5, 0)), 5);
        Assert.Equal(.4f, Through(float2(3, 0), float2(3, 0)), 5);
        Assert.Equal(1f, Through(float2(30, 0), float2(30, 0)));
        // A segment that stops short of the disc, or starts past it, touches nothing even on the same line.
        Assert.Equal(1f, Through(float2(-50, 0), float2(-20, 0)));
        Assert.Equal(1f, Through(float2(20, 0), float2(50, 0)));

        // Two clouds on one sight line multiply.
        Hand(lab.Zone, float2(30, 0), 5f, .5f);
        Assert.Equal(.2f, Through(float2(-50, 0), float2(50, 0)), 5);
        Assert.Equal(.4f, Through(float2(-50, 0), float2(15, 0)), 5);
    }
}
