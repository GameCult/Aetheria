using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// A painted ship owns a typed incoming-lock warning (cut lock-warning; rulings sensor-stat-set item 4 and
// lock-warning-strength). Every fixture drives the production path: a real LockWeapon in a real ship's gear
// executes inside Zone.Update, and the zone publishes every ship's paints before any ship updates. A paint made
// during tick N is therefore what IncomingLocks holds after tick N+1, whatever order the ships sit in the zone.
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
                Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 1000, Mass = 1000,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
            });
            cache.Upsert(Launcher("Launcher", lockSpeed: .6f, sensorImpact: 1, lockAngle: 30, directionImpact: 1));
            cache.Upsert(Launcher("SkewLauncher", lockSpeed: .8f, sensorImpact: 3, lockAngle: 45, directionImpact: 2));
            cache.FlushAsync().Wait();
        }

        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        for (var lot = 1; lot < 12; lot++) ledger.Lots[lot] = new Lot { Origin = new Attributed(), Quality = 1 };
        _items = new ItemManager(_openCache, ledger, new GameplaySettings
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
        }, _ => { });
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
    }

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Const(float v) => new PerformanceStat { Min = v, Max = v };

    private static GearData Launcher(string name, float lockSpeed, float sensorImpact, float lockAngle, float directionImpact) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors =
        {
            new LauncherData // the production launcher type; the catalog round-trips it (a bare LockWeaponData is not a stored union member)
            {
                Damage = Const(0), Range = Const(1000), MinRange = Const(0), Velocity = Const(0),
                Spread = Const(0), DamageSpread = Const(0), Penetration = Const(0), Count = Const(1),
                BurstTime = Const(0), Cooldown = Const(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
                LockSpeed = Const(lockSpeed), SensorImpact = Const(sensorImpact), LockAngle = Const(lockAngle),
                DirectionImpact = Const(directionImpact), Decay = Const(.1f)
            }
        }
    };

    private int _nextLot = 2;

    private Ship NewShip(float3 position, string launcher = null)
    {
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>("Skiff"));
        var ship = new Ship(_items, _zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings())
            { Faction = new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
        if (launcher != null)
        {
            var gearRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<GearData>(launcher));
            Assert.True(ship.TryEquip(new EquippableItem { Data = gearRef, Durability = 1, Lot = _nextLot++ }, HardpointCell));
        }
        _zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        _zone.Update(0f); // brings the gear online
        return ship;
    }

    private static LockWeapon LauncherOf(Entity e) => e.GetBehavior<LockWeapon>();

    private static EquippedItem GearOf(Entity e) => e.Equipment.Single(i => i.Behaviors.Any(b => b is LockWeapon));

    // The locker targets `target`, hostile to it, aimed straight at it (or along `aim`). The next zone tick paints.
    private static void Aim(Ship locker, Ship target, float3? aim = null, float info = 1f)
    {
        locker.SetIff(target, true);
        locker.EntityInfoGathered[target] = info;
        locker.SetTarget(target);
        locker.Aim = aim ?? normalize(target.Position - locker.Position);
    }

    private void Tick(float dt) => _zone.Update(dt);

    [Fact]
    public void APaintedShipLearnsBearingClassAndStrength()
    {
        var locker = NewShip(float3(0, 0, 0), "Launcher");
        var b = NewShip(float3(3, 0, 4));
        var launcher = LauncherOf(locker);

        Aim(locker, b);
        Tick(.5f);
        Assert.Empty(b.IncomingLocks); // published at the start of the next tick
        Assert.Equal(.3f, launcher.Lock, 4); // fixture: 0.6 speed * 0.5 s on a dead-on, fully sensed target
        Tick(.5f);

        var warning = Assert.Single(b.IncomingLocks);
        Assert.Equal(-.6f, warning.Bearing.x, 4);
        Assert.Equal(-.8f, warning.Bearing.y, 4);
        Assert.Same(GearOf(locker).Data, warning.EmitterClass);
        Assert.Equal(.3f, warning.Strength, 4);

        // The strength follows the emitter's lock as it grows: a stale or scaled copy would not.
        Assert.Equal(.6f, launcher.Lock, 4);
        Tick(0f);
        Assert.Equal(.6f, Assert.Single(b.IncomingLocks).Strength, 4);
    }

    // The whole range of strength is pinned, not its middle: lock = 0.6 * dt on a dead-on, fully sensed target.
    [Theory]
    [InlineData(.25f, .15f)]
    [InlineData(1f, .6f)]
    [InlineData(1.25f, .75f)]
    [InlineData(1.5f, .9f)]
    [InlineData(1.55f, .93f)]
    [InlineData(1.6f, .96f)]
    [InlineData(1.65f, .99f)]
    [InlineData(5f, 1f)]
    public void StrengthIsTheLockAcrossItsWholeRange(float dt, float expectedLock)
    {
        var locker = NewShip(float3(0, 0, 0), "Launcher");
        var b = NewShip(float3(0, 0, 9));

        Aim(locker, b);
        Tick(dt);
        // A full lock fires the launcher and spends the lock, so the emitter's own Lock is read only below it.
        if (expectedLock < 1f) Assert.Equal(expectedLock, LauncherOf(locker).Lock, 4);
        Tick(0f);

        Assert.Equal(expectedLock, Assert.Single(b.IncomingLocks).Strength, 4);
    }

    // The lock's progress terms: direction (lerp ^ DirectionImpact) and sensor info (info ^ SensorImpact), each
    // with an exponent other than 1, aimed off-axis at a partly sensed target. Lock after dt = 1:
    // 0.8 * (1 - 30/90)^2 * 0.8^3 = 0.18204.
    [Fact]
    public void TheLockGrowsByDirectionAndSensorTermsNotJustSpeed()
    {
        var locker = NewShip(float3(0, 0, 0), "SkewLauncher");
        var b = NewShip(float3(0, 0, 9));
        var thirtyOff = float3(sin(30f * .0174533f), 0, cos(30f * .0174533f));

        Aim(locker, b, thirtyOff, info: .8f);
        Tick(1f);
        Assert.Equal(.18204f, LauncherOf(locker).Lock, 4);
        Tick(0f);

        Assert.Equal(.18204f, Assert.Single(b.IncomingLocks).Strength, 4);
    }

    // Dead on, the direction term is 1 and the sensor term alone scales the lock: 0.8 * 0.5^3 = 0.1.
    [Fact]
    public void TheSensorTermAloneScalesADeadOnLock()
    {
        var locker = NewShip(float3(0, 0, 0), "SkewLauncher");
        var b = NewShip(float3(0, 0, 9));

        Aim(locker, b, info: .5f);
        Tick(1f);

        Assert.Equal(.1f, LauncherOf(locker).Lock, 4);
    }

    [Fact]
    public void APaintFromALockerItCannotSeeStillWarns()
    {
        var locker = NewShip(float3(0, 0, 0), "Launcher");
        var b = NewShip(float3(0, 0, 9));
        b.EntityInfoGathered[locker] = 0;
        Assert.DoesNotContain(locker, b.VisibleEnemies);

        Aim(locker, b);
        Tick(.5f);
        Tick(0f);

        Assert.Single(b.IncomingLocks);
        Assert.DoesNotContain(locker, b.VisibleEnemies);
    }

    [Fact]
    public void TargetedIsNotPainted()
    {
        var locker = NewShip(float3(0, 0, 0), "Launcher");
        var b = NewShip(float3(0, 0, 9));
        var c = NewShip(float3(9, 0, 0));

        // B is targeted but the launcher is aimed at C, far outside LockAngle of B.
        locker.SetIff(b, true);
        locker.SetIff(c, true);
        locker.EntityInfoGathered[b] = 1;
        locker.EntityInfoGathered[c] = 1;
        locker.SetTarget(b);
        locker.Aim = float3(1, 0, 0);
        Tick(.5f);
        Tick(0f);

        Assert.Empty(b.IncomingLocks);
        Assert.Empty(c.IncomingLocks); // C is aimed at but not the target: nothing paints it either
    }

    // A broken lock stops warning on the same tick whichever of the two ships the zone updates first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWarningEndsTwoTicksAfterItsLastPaintInEveryEntityOrder(bool receiverFirst)
    {
        Ship locker, b;
        if (receiverFirst)
        {
            b = NewShip(float3(0, 0, 9));
            locker = NewShip(float3(0, 0, 0), "Launcher");
        }
        else
        {
            locker = NewShip(float3(0, 0, 0), "Launcher");
            b = NewShip(float3(0, 0, 9));
        }
        Assert.Equal(receiverFirst ? (Entity) b : locker, _zone.Entities[0]);

        Aim(locker, b);
        Tick(.5f);                    // tick 1: painted
        Assert.Empty(b.IncomingLocks);
        Tick(.5f);                    // tick 2: tick 1's paint published, painted again
        Assert.Single(b.IncomingLocks);

        locker.Aim = float3(0, 0, -1); // the lock breaks: decays, paints nothing
        Tick(.5f);                    // tick 3: tick 2's paint published, nothing painted
        Assert.Single(b.IncomingLocks);
        Assert.True(LauncherOf(locker).Lock > 0f, "fixture: the lock decays, it does not vanish");
        Tick(.5f);                    // tick 4: nothing to publish
        Assert.Empty(b.IncomingLocks);
    }

    [Fact]
    public void ARetargetedLockerNoLongerWarnsItsOldTarget()
    {
        var locker = NewShip(float3(0, 0, 0), "Launcher");
        var b = NewShip(float3(0, 0, 9));
        var c = NewShip(float3(9, 0, 0));
        var launcher = LauncherOf(locker);

        Aim(locker, b);
        Tick(.5f);
        Tick(.5f);
        Assert.Single(b.IncomingLocks);

        Aim(locker, c); // retarget: aim at C
        Tick(.5f);      // the lock restarts on C: 0.3
        Tick(0f);       // tick after the retarget: B has nothing newer to publish

        Assert.Empty(b.IncomingLocks);
        Assert.Equal(.3f, Assert.Single(c.IncomingLocks).Strength, 4);
        Assert.Equal(.3f, launcher.Lock, 4);
    }

    [Fact]
    public void EachPainterIsItsOwnWarning()
    {
        var one = NewShip(float3(0, 0, 0), "Launcher");
        var two = NewShip(float3(10, 0, 10), "Launcher");
        var b = NewShip(float3(0, 0, 10));

        Aim(one, b);                                       // dead on: 0.6 * 0.5 = 0.3
        var twentyOff = float3(-cos(20f * .0174533f), 0, sin(20f * .0174533f));
        Aim(two, b, twentyOff);                            // 20 degrees off: 0.3 * (1 - 20/90) = 0.23333
        Tick(.5f);
        Tick(0f);

        Assert.Equal(2, b.IncomingLocks.Count);
        var strengths = b.IncomingLocks.Select(w => w.Strength).OrderBy(s => s).ToArray();
        Assert.Equal(.23333f, strengths[0], 4);
        Assert.Equal(.3f, strengths[1], 4); // never summed
        Assert.Equal(2, b.IncomingLocks.Select(w => (w.Bearing.x, w.Bearing.y)).Distinct().Count());
    }

    [Fact]
    public void AnInactiveShipHoldsNoWarnings()
    {
        var locker = NewShip(float3(0, 0, 0), "Launcher");
        var b = NewShip(float3(0, 0, 9));
        Aim(locker, b);
        Tick(.5f);
        Tick(0f);
        Assert.Single(b.IncomingLocks);

        b.Deactivate();
        Assert.Empty(b.IncomingLocks);

        Tick(.5f); // painted while docked or dead
        b.Activate();
        Tick(0f);
        Assert.Empty(b.IncomingLocks); // the paint received while inactive was never stored

        // A paint still waiting in the inbox when the ship deactivates does not survive to the next activation.
        Tick(.5f);
        b.Deactivate();
        b.Activate();
        Tick(0f);
        Assert.Empty(b.IncomingLocks);
    }
}
