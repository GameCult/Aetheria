using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// A ThrottleLock on an active consumable replaces the pilot's movement intent with full forward, read once at
// the top of Ship.Update's active branch. Turn is untouched. Every observation is a thruster's Axis after a real
// Ship.Update, with the effect started through Entity.ActivateConsumable.
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
        QualityPriceModifier = new ExponentialLerp()
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

    // Spec ALockedThrottleIgnoresIntent. Mutation: read MovementDirection in place of move at any of the six
    // sites in Ship.Update and the matching assertion goes red.
    [Fact]
    public void ALockedThrottleIgnoresIntent()
    {
        var f = Build(100f);
        using var _ = f.Cache;
        f.ActivateLock();
        Assert.True(f.Ship.ThrottleLocked);

        foreach (var intent in new[] { float2(0, -1), float2(1, 0), float2(-1, 0) })
        {
            f.Ship.MovementDirection = intent;
            f.Ship.Turn = 0;
            f.Ship.Update(0.01f);
            Assert.Equal(1f, f.Forward.Axis);
            Assert.Equal(0f, f.Reverse.Axis);
            Assert.All(f.Right, t => Assert.Equal(0f, t.Axis));
            Assert.All(f.Left, t => Assert.Equal(0f, t.Axis));
        }

        // Turning stays with the pilot: the clockwise-torque thruster follows Turn while the lock holds.
        f.Ship.MovementDirection = float2(0, -1);
        f.Ship.Turn = 1;
        f.Ship.Update(0.01f);
        Assert.Equal(1f, f.Clockwise.Axis);
        f.Ship.Turn = 0;
        f.Ship.Update(0.01f);
        Assert.Equal(0f, f.Clockwise.Axis);
    }

    // Spec TheLockEndsWithItsEffect. Mutation: make ThrottleLocked a latch (set on activation, never cleared)
    // and the post-expiry reverse axis stays 0.
    [Fact]
    public void TheLockEndsWithItsEffect()
    {
        var f = Build(1f);
        using var _ = f.Cache;
        f.ActivateLock();
        f.Ship.MovementDirection = float2(0, -1);
        f.Ship.Update(0.5f);
        Assert.Equal(0f, f.Reverse.Axis);

        f.Ship.Update(2f); // runs past the Duration; the effect leaves at the end of this tick
        Assert.False(f.Ship.ThrottleLocked);

        f.Ship.Update(0.01f);
        Assert.Equal(1f, f.Reverse.Axis);
        Assert.Equal(0f, f.Forward.Axis);
    }

    // Spec AnUnlockedShipFollowsIntent. Mutation: replace the conditional by an unconditional float2(0, 1) and
    // the reverse and strafe assertions go red.
    [Fact]
    public void AnUnlockedShipFollowsIntent()
    {
        var f = Build(1f);
        using var _ = f.Cache;
        Assert.False(f.Ship.ThrottleLocked);

        f.Ship.MovementDirection = float2(0, -1);
        f.Ship.Update(0.01f);
        Assert.Equal(1f, f.Reverse.Axis);
        Assert.Equal(0f, f.Forward.Axis);

        f.Ship.MovementDirection = float2(1, 0);
        f.Ship.Update(0.01f);
        Assert.All(f.Right, t => Assert.Equal(1f, t.Axis));
        Assert.All(f.Left, t => Assert.Equal(0f, t.Axis));
        Assert.Equal(0f, f.Forward.Axis);

        f.Ship.MovementDirection = float2(-1, 0);
        f.Ship.Update(0.01f);
        Assert.All(f.Left, t => Assert.Equal(1f, t.Axis));
        Assert.All(f.Right, t => Assert.Equal(0f, t.Axis));

        f.Ship.MovementDirection = float2(0, 1);
        f.Ship.Update(0.01f);
        Assert.Equal(1f, f.Forward.Axis);
        Assert.Equal(0f, f.Reverse.Axis);
    }

    // Finding zero-stick-unpinned. Mutation: float2(0, length(MovementDirection)) as the locked intent. The lock
    // supplies full forward whatever the stick's magnitude, including none and a short reverse push.
    [Fact]
    public void ALockedShipGetsFullForwardWhateverTheStickLength()
    {
        var f = Build(100f);
        using var _ = f.Cache;
        f.ActivateLock();
        foreach (var intent in new[] { float2(0, 0), float2(.3f, .2f), float2(-.5f, -.5f) })
        {
            f.Ship.MovementDirection = intent;
            f.Ship.Turn = 0;
            f.Ship.Update(0.01f);
            Assert.Equal(1f, f.Forward.Axis);
            Assert.Equal(0f, f.Reverse.Axis);
            Assert.All(f.Right, t => Assert.Equal(0f, t.Axis));
            Assert.All(f.Left, t => Assert.Equal(0f, t.Axis));
        }
    }

    // Finding ccw-turn-unpinned. Mutation: drop the counter-clockwise Turn. Turn in either sign reaches only the
    // thrusters with torque of that sign, at full, while the lock holds forward.
    [Fact]
    public void ALockedShipTurnsBothWays()
    {
        var f = Build(100f);
        using var _ = f.Cache;
        f.ActivateLock();
        var all = f.Ship.GetBehaviors<Thruster>().ToArray();
        var cw = all.Where(t => t.Torque > 0 && t.Item.EquippableItem.Rotation == ItemRotation.None).ToArray();
        var ccw = all.Where(t => t.Torque < 0 && t.Item.EquippableItem.Rotation == ItemRotation.None).ToArray();
        Assert.NotEmpty(cw);
        Assert.NotEmpty(ccw);
        f.Ship.MovementDirection = float2(1, -1);
        foreach (var turn in new[] { 1f, -1f })
        {
            f.Ship.Turn = turn;
            f.Ship.Update(0.01f);
            Assert.All(turn > 0 ? cw : ccw, t => Assert.Equal(1f, t.Axis));
            Assert.All(turn > 0 ? ccw : cw, t => Assert.Equal(0f, t.Axis));
            Assert.Equal(1f, f.Forward.Axis);
        }
    }

    // Finding non-lock-consumable-unpinned. Mutation: any active consumable locks. A consumable without
    // ThrottleLockData leaves the pilot's intent in force.
    [Fact]
    public void AConsumableWithoutTheLockDoesNotLock()
    {
        var f = Build(100f);
        using var _ = f.Cache;
        var plain = new ConsumableItemData { Name = "Plain", Duration = 100f };
        f.Cache.Upsert(plain);
        f.Ship.ActivateConsumable(new ConsumableItem
        {
            Data = f.Cache.RefOf<ItemData>(plain),
            Lot = f.Items.Lots.Add(new Lot { Design = f.Cache.RefOf<ItemData>(plain), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        });
        Assert.False(f.Ship.ThrottleLocked);
        f.Ship.MovementDirection = float2(0, -1);
        f.Ship.Update(0.01f);
        Assert.Equal(1f, f.Reverse.Axis);
        Assert.Equal(0f, f.Forward.Axis);
    }

    // Finding lock-end-boundary-unpinned, finding lock-end-at-90pct-unpinned. Mutations: the lock ends at half
    // Duration, at 0.8 or 0.9 of Duration, or one tick late. Duration 1 stepped in 0.25 s ticks (exact in
    // floats) locks the axes on exactly four ticks (reverse stays 0), the effect is gone when the fourth ends,
    // and the fifth tick follows the stick. The count scales with Duration: 2 s locks eight ticks.
    [Theory]
    [InlineData(1f, 4)]
    [InlineData(2f, 8)]
    public void TheLockEndsAtTheExactTickOfItsDuration(float duration, int lockedTicks)
    {
        var f = Build(duration);
        using var _ = f.Cache;
        f.ActivateLock();
        f.Ship.MovementDirection = float2(0, -1);
        for (var i = 0; i < lockedTicks; i++)
        {
            Assert.True(f.Ship.ThrottleLocked);
            f.Ship.Update(.25f);
            Assert.Equal(0f, f.Reverse.Axis);
            Assert.Equal(1f, f.Forward.Axis);
        }
        Assert.False(f.Ship.ThrottleLocked);
        f.Ship.Update(.25f);
        Assert.Equal(1f, f.Reverse.Axis);
        Assert.Equal(0f, f.Forward.Axis);
    }
}
