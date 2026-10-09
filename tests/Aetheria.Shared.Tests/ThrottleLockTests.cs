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
}
