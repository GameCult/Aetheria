using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// A ThrottleLock on an active consumable replaces the pilot's movement intent with full forward, read once at
// the top of Ship.Update's active branch; the allocator is the only thing that turns intent into throttles. Turn is
// untouched. Every observation is a thruster's Axis after a real Ship.Update, with the effect started through
// Entity.ActivateConsumable. A lock is observed against an unlocked twin of the same fleet fed full forward and the same
// Turn through the same ticks: the two must answer alike, with no constant of the old mixer in the assertion.
public sealed class ThrottleLockTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-throttlelock-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public ThrottleLockTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TorqueMultiplier = 1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design, ItemRotation rotation = ItemRotation.None) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Rotation = rotation,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    private sealed class Fixture
    {
        public CultCache Cache;
        public ItemManager Items;
        public Ship Ship;
        public ConsumableItemData Lock;
        public Thruster Forward, Reverse, Clockwise;
        public Thruster[] Right, Left; // pairs with opposing torque: a lone off-axis strafe thruster cancels its own torque

        public void ActivateLock() => Ship.ActivateConsumable(new ConsumableItem
        {
            Data = Cache.RefOf<ItemData>(Lock),
            Lot = Items.Lots.Add(new Lot { Design = Cache.RefOf<ItemData>(Lock), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        });
    }

    // One thruster per intent direction, plus two reverse thrusters either side of the centre line, one of
    // which has clockwise torque (the one Turn reaches). Thrusters are found by their item's rotation, as Ship
    // sorts them.
    private Fixture Build(float lockDuration)
    {
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Thruster", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ThrusterData { Thrust = Constant(100), Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(0) } }
        });
        var lockData = new ConsumableItemData { Name = "Overdrive", Duration = lockDuration, Behaviors = { new ThrottleLockData() } };
        cache.Upsert(lockData);
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var ship = new Ship(items, zone, Mint(cache, items, cache.GetByName<HullData>("Skiff")), new EntitySettings());
        var gear = cache.GetByName<GearData>("Thruster");
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.Reversed), new int2(2, 1)));
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.None), new int2(1, 2)));
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.None), new int2(3, 2)));
        // Strafe pairs sit either side of the centre line across the ship's length, so their torques oppose.
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.CounterClockwise), new int2(1, 1)));
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.CounterClockwise), new int2(1, 3)));
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.Clockwise), new int2(3, 1)));
        Assert.True(ship.TryEquip(Mint(cache, items, gear, ItemRotation.Clockwise), new int2(3, 3)));
        zone.Entities.Add(ship);
        ship.Aim = float3(0, 0, 1);
        ship.Activate();
        // A thruster is live from its first performance update; the allocator reads that every tick.
        foreach (var item in ship.Equipment) item.UpdatePerformance();

        var thrusters = ship.GetBehaviors<Thruster>().ToArray();
        return new Fixture
        {
            Cache = cache, Items = items, Ship = ship, Lock = cache.GetByName<ConsumableItemData>("Overdrive"),
            Forward = thrusters.Single(t => t.Item.EquippableItem.Rotation == ItemRotation.Reversed),
            Right = thrusters.Where(t => t.Item.EquippableItem.Rotation == ItemRotation.CounterClockwise).ToArray(),
            Left = thrusters.Where(t => t.Item.EquippableItem.Rotation == ItemRotation.Clockwise).ToArray(),
            Reverse = thrusters.First(t => t.Item.EquippableItem.Rotation == ItemRotation.None && t.Torque <= 0),
            Clockwise = thrusters.First(t => t.Item.EquippableItem.Rotation == ItemRotation.None && t.Torque > 0)
        };
    }

    private static float[] Axes(Fixture f) => f.Ship.GetBehaviors<Thruster>().Select(t => t.Axis).ToArray();

    private static float Apart(float[] a, float[] b) => a.Zip(b, (x, y) => Math.Abs(x - y)).Max();

    // Drives a (possibly locked) ship at `intent` and its unlocked twin at full forward, both at the same Turn and dt, and
    // requires every thruster to answer alike.
    private static void Tick(Fixture ship, Fixture twin, float2 intent, float turn, float dt)
    {
        ship.Ship.MovementDirection = intent;
        twin.Ship.MovementDirection = float2(0, 1);
        ship.Ship.Turn = twin.Ship.Turn = turn;
        ship.Ship.Update(dt);
        twin.Ship.Update(dt);
        Assert.True(Apart(Axes(ship), Axes(twin)) < 1e-6f, "the ship's throttles differ from its full-forward twin's");
        Assert.True(twin.Forward.Axis > .98f, "fixture: the twin's forward drive is at full");
    }

    // Spec ALockedThrottleIgnoresIntent. Mutation: read MovementDirection in place of move in Ship.Update and a locked
    // ship stops answering as its full-forward twin does.
    [Fact]
    public void ALockedThrottleIgnoresIntent()
    {
        var f = Build(100f);
        var twin = Build(100f);
        using var _ = f.Cache;
        using var __ = twin.Cache;
        f.ActivateLock();
        Assert.True(f.Ship.ThrottleLocked);
        Assert.False(twin.Ship.ThrottleLocked);

        foreach (var intent in new[] { float2(0, -1), float2(1, 0), float2(-1, 0) })
            Tick(f, twin, intent, 0, 0.01f);

        // Turning stays with the pilot: a Turn moves the throttles while the lock holds, and the lock follows it.
        Tick(f, twin, float2(0, -1), 1, 0.01f);
        var turning = Axes(twin);
        Tick(f, twin, float2(0, -1), 0, 0.01f);
        Assert.True(Apart(turning, Axes(twin)) > .01f, "a Turn changes the throttles");
    }

    // Spec TheLockEndsWithItsEffect. Mutation: make ThrottleLocked a latch (set on activation, never cleared) and the
    // ship keeps answering as its full-forward twin after expiry.
    [Fact]
    public void TheLockEndsWithItsEffect()
    {
        var f = Build(1f);
        var twin = Build(1f);
        using var _ = f.Cache;
        using var __ = twin.Cache;
        f.ActivateLock();
        Tick(f, twin, float2(0, -1), 0, 0.5f);
        Assert.True(f.Reverse.Axis < 1e-6f);

        Tick(f, twin, float2(0, -1), 0, 2f); // runs past the Duration; the effect leaves at the end of this tick
        Assert.False(f.Ship.ThrottleLocked);

        f.Ship.MovementDirection = float2(0, -1);
        twin.Ship.MovementDirection = float2(0, 1);
        f.Ship.Update(0.01f);
        twin.Ship.Update(0.01f);
        Assert.True(f.Reverse.Axis > .5f);
        Assert.True(f.Forward.Axis < 1e-6f);
        Assert.True(Apart(Axes(f), Axes(twin)) > .5f, "the unlocked ship follows its stick, not the twin's full forward");
    }

    // Spec AnUnlockedShipFollowsIntent. Mutation: replace the conditional by an unconditional float2(0, 1) and the four
    // intents give the same throttles.
    [Fact]
    public void AnUnlockedShipFollowsIntent()
    {
        var f = Build(1f);
        using var _ = f.Cache;
        Assert.False(f.Ship.ThrottleLocked);

        float[] Fly(float2 intent)
        {
            f.Ship.MovementDirection = intent;
            f.Ship.Update(0.01f);
            return Axes(f);
        }

        var back = Fly(float2(0, -1));
        Assert.True(f.Reverse.Axis > .5f);
        Assert.True(f.Forward.Axis < 1e-6f);

        var right = Fly(float2(1, 0));
        Assert.True(f.Right.Max(t => t.Axis) > .5f);
        Assert.All(f.Left, t => Assert.True(t.Axis < 1e-6f));

        var left = Fly(float2(-1, 0));
        Assert.True(f.Left.Max(t => t.Axis) > .5f);
        Assert.All(f.Right, t => Assert.True(t.Axis < 1e-6f));

        var forward = Fly(float2(0, 1));
        Assert.True(f.Forward.Axis > .98f, "a full stick asks for the hull's whole forward thrust");
        Assert.True(f.Reverse.Axis < 1e-6f);

        // The stick's length is the fraction of the hull's forward thrust asked for, and no stick asks for nothing.
        Fly(float2(0, .5f));
        Assert.InRange(f.Forward.Axis, .4f, .6f);
        Assert.All(Fly(float2(0, 0)), a => Assert.True(a < 1e-6f));

        var all = new[] { back, right, left, forward };
        for (var a = 0; a < all.Length; a++)
            for (var b = a + 1; b < all.Length; b++)
                Assert.True(Apart(all[a], all[b]) > .1f, "two different intents gave the same throttles");
    }

    // Finding zero-stick-unpinned. Mutation: float2(0, length(MovementDirection)) as the locked intent. The lock
    // supplies full forward whatever the stick's magnitude, including none and a short reverse push.
    [Fact]
    public void ALockedShipGetsFullForwardWhateverTheStickLength()
    {
        var f = Build(100f);
        var twin = Build(100f);
        using var _ = f.Cache;
        using var __ = twin.Cache;
        f.ActivateLock();
        foreach (var intent in new[] { float2(0, 0), float2(.3f, .2f), float2(-.5f, -.5f) })
            Tick(f, twin, intent, 0, 0.01f);
    }

    // Finding ccw-turn-unpinned. Mutation: drop the counter-clockwise Turn. Turn in either sign gives the locked ship
    // its full-forward twin's throttles, and the twin's solved net turns the way it was asked.
    [Fact]
    public void ALockedShipTurnsBothWays()
    {
        var f = Build(100f);
        var twin = Build(100f);
        using var _ = f.Cache;
        using var __ = twin.Cache;
        f.ActivateLock();
        var thrusters = twin.Ship.GetBehaviors<Thruster>().ToArray();
        foreach (var turn in new[] { 1f, -1f })
        {
            Tick(f, twin, float2(1, -1), turn, 0.01f);
            var yaw = thrusters.Sum(t => t.Axis * t.Column(t.NominalThrust).z);
            Assert.True(yaw * turn > 1e-4f, "the solved net turns the way it was asked");
        }
    }

    // Finding non-lock-consumable-unpinned. Mutation: any active consumable locks. A consumable without
    // ThrottleLockData leaves the pilot's intent in force: the ship answers as an unlocked ship at the same stick.
    [Fact]
    public void AConsumableWithoutTheLockDoesNotLock()
    {
        var f = Build(100f);
        var twin = Build(100f);
        using var _ = f.Cache;
        using var __ = twin.Cache;
        var plain = new ConsumableItemData { Name = "Plain", Duration = 100f };
        f.Cache.Upsert(plain);
        f.Ship.ActivateConsumable(new ConsumableItem
        {
            Data = f.Cache.RefOf<ItemData>(plain),
            Lot = f.Items.Lots.Add(new Lot { Design = f.Cache.RefOf<ItemData>(plain), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        });
        Assert.False(f.Ship.ThrottleLocked);
        f.Ship.MovementDirection = twin.Ship.MovementDirection = float2(0, -1);
        f.Ship.Update(0.01f);
        twin.Ship.Update(0.01f);
        Assert.True(Apart(Axes(f), Axes(twin)) < 1e-6f);
        Assert.True(f.Reverse.Axis > .5f);
        Assert.True(f.Forward.Axis < 1e-6f);
    }

    // Finding lock-end-boundary-unpinned, finding lock-end-at-90pct-unpinned. Mutations: the lock ends at half
    // Duration, at 0.8 or 0.9 of Duration, or one tick late. Duration 1 stepped in 0.25 s ticks (exact in
    // floats) answers as the full-forward twin on exactly four ticks, the effect is gone when the fourth ends,
    // and the fifth tick follows the stick. The count scales with Duration: 2 s locks eight ticks.
    [Theory]
    [InlineData(1f, 4)]
    [InlineData(2f, 8)]
    public void TheLockEndsAtTheExactTickOfItsDuration(float duration, int lockedTicks)
    {
        var f = Build(duration);
        var twin = Build(duration);
        using var _ = f.Cache;
        using var __ = twin.Cache;
        f.ActivateLock();
        for (var i = 0; i < lockedTicks; i++)
        {
            Assert.True(f.Ship.ThrottleLocked);
            Tick(f, twin, float2(0, -1), 0, .25f);
            Assert.True(f.Reverse.Axis < 1e-6f);
        }
        Assert.False(f.Ship.ThrottleLocked);
        f.Ship.MovementDirection = float2(0, -1);
        twin.Ship.MovementDirection = float2(0, 1);
        f.Ship.Update(.25f);
        twin.Ship.Update(.25f);
        Assert.True(f.Reverse.Axis > .5f);
        Assert.True(f.Forward.Axis < 1e-6f);
    }
}
