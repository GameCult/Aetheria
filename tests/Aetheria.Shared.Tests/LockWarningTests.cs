using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// A painted ship owns a typed incoming-lock warning (cut lock-warning; rulings sensor-stat-set item 4 and
// lock-warning-strength). Every fixture drives the production path: a real LockWeapon on a real ship executes,
// the receiver's own Update publishes. The warning's strength is the emitter's own Lock at the paint.
public sealed class LockWarningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-lockwarn-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 HardpointCell = new int2(0, 0);
    private CultCache _openCache;
    private ItemManager _items;
    private Zone _zone;

    public LockWarningTests()
    {
        Directory.CreateDirectory(_root);
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            cache.Upsert(new VerseGrammar { Revision = 1 });
            var hullShape = new Shape(3, 3);
            foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
            cache.Upsert(new HullData
            {
                Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 1,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
            });
            cache.Upsert(new GearData
            {
                Name = "Launcher", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                Behaviors =
                {
                    new LockWeaponData
                    {
                        Count = Const(1), Cooldown = Const(1),
                        LockSpeed = Const(.6f), SensorImpact = Const(1), LockAngle = Const(30),
                        DirectionImpact = Const(1), Decay = Const(.1f)
                    }
                }
            });
            cache.FlushAsync().Wait();
        }

        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        _items = new ItemManager(_openCache, ledger, new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .5f
        }, _ => { });
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
    }

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Const(float v) => new PerformanceStat { Min = v, Max = v };

    private Ship NewShip(float3 position, bool launcher)
    {
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>("Skiff"));
        var ship = new Ship(_items, _zone, new EquippableItem { Data = hullRef, Durability = 1, Lot = 1 }, new EntitySettings())
            { Faction = new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
        if (launcher)
        {
            var gearRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<GearData>("Launcher"));
            Assert.True(ship.TryEquip(new EquippableItem { Data = gearRef, Durability = 1, Lot = 2 }, HardpointCell));
        }
        _zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        return ship;
    }

    private static LockWeapon LauncherOf(Entity e) =>
        (LockWeapon) e.Equipment.Single().Behaviors.Single(b => b is LockWeapon);

    // The locker targets `target`, hostile to it, aimed straight at it (or along `aim`), then the launcher ticks.
    private static LockWeapon Aim(Ship locker, Ship target, float dt, float3? aim = null)
    {
        locker.SetIff(target, true);
        locker.EntityInfoGathered[target] = 1;
        locker.SetTarget(target);
        locker.Aim = aim ?? normalize(target.Position - locker.Position);
        var launcher = LauncherOf(locker);
        launcher.Execute(dt);
        return launcher;
    }

    [Fact]
    public void APaintedShipLearnsBearingClassAndStrength()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(3, 0, 4), launcher: false);

        var launcher = Aim(locker, b, .5f);
        b.Update(.01f);

        var warning = Assert.Single(b.IncomingLocks);
        Assert.Equal(-.6f, warning.Bearing.x, 4);
        Assert.Equal(-.8f, warning.Bearing.y, 4);
        Assert.Same(locker.Equipment.Single().Data, warning.EmitterClass);
        Assert.Equal(.3f, launcher.Lock, 4); // fixture: 0.6 speed * 0.5 s on a dead-on, fully sensed target
        Assert.Equal(.3f, warning.Strength, 4);

        // The strength follows the emitter's lock as it grows: a stale or scaled copy would not.
        Aim(locker, b, .5f);
        b.Update(.01f);
        Assert.Equal(.6f, launcher.Lock, 4);
        Assert.Equal(.6f, Assert.Single(b.IncomingLocks).Strength, 4);
    }

    [Fact]
    public void AStrengthAtTheCeilingIsOne()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(0, 0, 9), launcher: false);

        Aim(locker, b, 5f); // saturates the lock
        b.Update(.01f);

        Assert.Equal(1f, Assert.Single(b.IncomingLocks).Strength, 4);
    }

    [Fact]
    public void APaintFromALockerItCannotSeeStillWarns()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(0, 0, 9), launcher: false);
        b.EntityInfoGathered[locker] = 0;
        Assert.DoesNotContain(locker, b.VisibleEnemies);

        Aim(locker, b, .5f);
        b.Update(.01f);

        Assert.Single(b.IncomingLocks);
        Assert.DoesNotContain(locker, b.VisibleEnemies);
    }

    [Fact]
    public void TargetedIsNotPainted()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(0, 0, 9), launcher: false);
        var c = NewShip(float3(9, 0, 0), launcher: false);

        // B is targeted but the launcher is aimed at C, far outside LockAngle of B.
        locker.SetIff(b, true);
        locker.SetIff(c, true);
        locker.EntityInfoGathered[b] = 1;
        locker.EntityInfoGathered[c] = 1;
        locker.SetTarget(b);
        locker.Aim = float3(1, 0, 0);
        LauncherOf(locker).Execute(.5f);
        b.Update(.01f);
        c.Update(.01f);

        Assert.Empty(b.IncomingLocks);
        Assert.Empty(c.IncomingLocks); // C is aimed at but not the target: nothing paints it either
    }

    [Fact]
    public void AWarningEndsOneTickAfterItsPaint()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(0, 0, 9), launcher: false);

        var launcher = Aim(locker, b, .5f);
        b.Update(.01f);
        Assert.Single(b.IncomingLocks);

        // No emission this tick (the launcher item is offline): the warning is gone after B's next Update.
        b.Update(.01f);
        Assert.Empty(b.IncomingLocks);

        // The locker turns away: the lock decays but is still above zero, and nothing is painted.
        Aim(locker, b, .5f);
        b.Update(.01f);
        Assert.Single(b.IncomingLocks);
        locker.Aim = float3(0, 0, -1);
        launcher.Execute(.5f);
        Assert.True(launcher.Lock > 0f, "fixture: the lock decays, it does not vanish");
        b.Update(.01f);
        Assert.Empty(b.IncomingLocks);
    }

    [Fact]
    public void ARetargetedLockerNoLongerWarnsItsOldTarget()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(0, 0, 9), launcher: false);
        var c = NewShip(float3(9, 0, 0), launcher: false);

        var launcher = Aim(locker, b, .5f);
        b.Update(.01f);
        Assert.Single(b.IncomingLocks);

        Aim(locker, c, .5f); // retarget: aim at C
        b.Update(.01f);
        c.Update(.01f);

        Assert.Empty(b.IncomingLocks);
        Assert.Equal(.3f, Assert.Single(c.IncomingLocks).Strength, 4); // the lock restarted on C
        Assert.Equal(.3f, launcher.Lock, 4);
    }

    [Fact]
    public void EachPainterIsItsOwnWarning()
    {
        var one = NewShip(float3(0, 0, 0), launcher: true);
        var two = NewShip(float3(10, 0, 10), launcher: true);
        var b = NewShip(float3(0, 0, 10), launcher: false);

        Aim(one, b, .5f);   // 0.3
        Aim(two, b, .25f);  // 0.15
        b.Update(.01f);

        Assert.Equal(2, b.IncomingLocks.Count);
        var strengths = b.IncomingLocks.Select(w => w.Strength).OrderBy(s => s).ToArray();
        Assert.Equal(.15f, strengths[0], 4);
        Assert.Equal(.3f, strengths[1], 4); // never summed to 0.45
        Assert.Equal(2, b.IncomingLocks.Select(w => (w.Bearing.x, w.Bearing.y)).Distinct().Count());
    }

    [Fact]
    public void AnInactiveShipHoldsNoWarnings()
    {
        var locker = NewShip(float3(0, 0, 0), launcher: true);
        var b = NewShip(float3(0, 0, 9), launcher: false);
        Aim(locker, b, .5f);
        b.Update(.01f);
        Assert.Single(b.IncomingLocks);

        b.Deactivate();
        Assert.Empty(b.IncomingLocks);

        Aim(locker, b, .5f); // painted while docked or dead
        b.Update(.01f);
        Assert.Empty(b.IncomingLocks);

        b.Activate();
        b.Update(.01f);
        Assert.Empty(b.IncomingLocks); // the paint received while inactive was never stored
    }
}
