using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// A consumable is a cargo item with a timed effect (aetheria-release CS-R1). Its modifiers exist in the resolver
// exactly while its effect is active: the host (Entity) initialises the effect's behaviours at activation and
// disposes them at expiry, and ItemManager.CreateInstance is the one way a lot becomes a ConsumableItem.
public sealed class ConsumableTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-consumable-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 EngineCell = new int2(0, 0);
    private static readonly int2 HoldCell = new int2(1, 1);

    public ConsumableTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private sealed class Rig
    {
        public CultCache Cache;
        public ItemManager Items;
        public Ship Ship;
        public EquippedItem Engine;
        public PerformanceStat Thrust;
        public ConsumableItemData Consumable;
        public ConsumableItemData Other; // a second, unrelated non-stackable design
        public Func<EquippableItem> NewEngine;
        public CultRecordKey Key;
        public CultRecordRef<Faction> Maker;

        public ConsumableItem Mint(ConsumableItemData design = null) =>
            Assert.IsType<ConsumableItem>(Items.CreateInstance(Items.CreateLot(design ?? Consumable, Maker, .5f)));
        public EquippedCargoBay Hold => Ship.CargoBays.Single();
        public int InCargo => Hold.Cargo.Keys.Count(i => i is ConsumableItem);
    }

    // A ship with a thruster (Thrust 10) and a hold; the consumable doubles ThrusterData.Thrust for one second.
    private Rig Build(bool stackable = false)
    {
        var thrust = new PerformanceStat { Min = 10, Max = 10 };
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hullRef = cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = EngineCell, Shape = new Shape() } }
        });
        var engineRef = cache.Upsert(new GearData
        {
            Name = "Engine", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10,
            Behaviors = { new ThrusterData { Thrust = thrust } }
        });
        var interior = new Shape(2, 1); // room for two consumables
        foreach (var cell in interior.AllCoordinates) interior[cell] = true;
        var holdRef = cache.Upsert(new CargoBayData { Name = "Hold", Shape = new Shape(), InteriorShape = interior, Durability = 10 });
        var consumableRef = cache.Upsert(new ConsumableItemData
        {
            Name = "Overdrive", Duration = 1f, Stackable = stackable, Shape = new Shape(),
            Behaviors =
            {
                new StatModifierData
                {
                    Stat = new StatReference { Target = nameof(ThrusterData), Stat = nameof(ThrusterData.Thrust) },
                    Modifier = new PerformanceStat { Min = 2, Max = 2 },
                    Type = StatModifierType.Multiplier
                }
            }
        });
        var otherRef = cache.Upsert(new ConsumableItemData { Name = "Flare", Duration = 1f, Shape = new Shape() });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var makerRef = cache.RefOf(cache.GetByName<Faction>("Maker"));
        EquippableItem Gear(EquippableItemData design) => (EquippableItem) items.CreateInstance(items.CreateLot(design, makerRef, .5f));
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var ship = new Ship(items, zone, Gear(cache.Get(hullRef)), new EntitySettings());
        Assert.True(ship.TryEquip(Gear(cache.Get(engineRef)), EngineCell));
        Assert.True(ship.TryEquip(Gear(cache.Get(holdRef)), HoldCell));
        zone.Entities.Add(ship);
        ship.Activate();
        var data = cache.Get(consumableRef);
        return new Rig
        {
            Cache = cache, Items = items, Ship = ship, Thrust = thrust, Consumable = data, Other = cache.Get(otherRef),
            NewEngine = () => Gear(cache.Get(engineRef)),
            Key = items.ItemData.RefOf<ItemData>(data).Key,
            Engine = ship.Equipment.Single(e => e.Data.Name == "Engine"),
            Maker = makerRef
        };
    }

    // Two ticks after activation the modifier has attached (the first tick executes it, the second applies it).
    private static void Tick(Rig rig, int times, float dt)
    {
        for (var i = 0; i < times; i++) rig.Ship.Update(dt);
    }

    // The rule: a consumable's modifiers exist in the resolver exactly while its effect is active, never after.
    // Observed at the resolved stat and at the resolver's modifier entry count, not at a flag.
    [Fact]
    public void AConsumableModifierBoostsUntilExpiry()
    {
        var rig = Build();
        using var _ = rig.Cache;
        var resolver = rig.Ship.Resolver;
        Assert.Equal(10f, rig.Engine.Evaluate(rig.Thrust), 3);
        var baseline = resolver.ModifierEntryCount;

        rig.Ship.ActivateConsumable(rig.Mint());
        Tick(rig, 2, .3f); // 0.4s of the 1s remain; the modifier has attached
        Assert.Equal(20f, rig.Engine.Evaluate(rig.Thrust), 3);
        Assert.True(resolver.ModifierEntryCount > baseline);

        Tick(rig, 3, .3f); // 1.5s elapsed in all: the effect ran out on the fourth tick
        Assert.Equal(10f, rig.Engine.Evaluate(rig.Thrust), 3);
        Assert.Equal(baseline, resolver.ModifierEntryCount);
    }

    // A lot minted through the one production path is a ConsumableItem, and a hold holding it activates it.
    [Fact]
    public void ABoughtConsumableActivates()
    {
        var rig = Build();
        using var _ = rig.Cache;
        Assert.True(rig.Hold.TryStore(rig.Mint()));
        Assert.Equal(1, rig.InCargo);

        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.Equal(0, rig.InCargo);
        Assert.NotNull(rig.Ship.FindActiveConsumable(rig.Consumable));
    }

    // CS-R1: a non-stackable design allows one active effect at a time, so its Duration is its cooldown.
    [Fact]
    public void ANonStackableConsumableRefusesASecondActivation()
    {
        var rig = Build();
        using var _ = rig.Cache;
        Assert.True(rig.Hold.TryStore(rig.Mint()));
        Assert.True(rig.Hold.TryStore(rig.Mint()));

        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.False(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.Equal(1, rig.InCargo);

        Tick(rig, 5, .3f); // the first effect has run out
        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.Equal(0, rig.InCargo);
    }

    // The expiry boundary: Duration 1 with 0.3 s ticks leaves 0.1 s after the third tick (still active) and
    // -0.2 s after the fourth (gone). An expiry one tick early or one tick late changes one of these two reads.
    [Fact]
    public void AConsumableLivesForItsDurationAndNoLonger()
    {
        var rig = Build();
        using var _ = rig.Cache;
        rig.Ship.ActivateConsumable(rig.Mint());
        Tick(rig, 3, .3f);
        Assert.Equal(20f, rig.Engine.Evaluate(rig.Thrust), 3);
        Tick(rig, 1, .3f);
        Assert.Equal(10f, rig.Engine.Evaluate(rig.Thrust), 3);
    }

    // CS-R1, the stackable side: a stackable design activates again while its first effect runs, and every
    // multiplier on the stat multiplies.
    [Fact]
    public void TwoStackableConsumablesOverlapAndMultiply()
    {
        var rig = Build(stackable: true);
        using var _ = rig.Cache;
        Assert.True(rig.Hold.TryStore(rig.Mint()));
        Assert.True(rig.Hold.TryStore(rig.Mint()));

        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.Equal(0, rig.InCargo);
        Tick(rig, 2, .3f);
        Assert.Equal(40f, rig.Engine.Evaluate(rig.Thrust), 3);
    }

    // Two modifiers share one resolver entry. The first expires while the second still runs: the second's
    // boost stays, and when it expires too nothing is left behind.
    [Fact]
    public void AnExpiringConsumableLeavesAnOverlappingOnesBoost()
    {
        var rig = Build(stackable: true);
        using var _ = rig.Cache;
        var resolver = rig.Ship.Resolver;
        var baseline = resolver.ModifierEntryCount;
        Assert.True(rig.Hold.TryStore(rig.Mint()));
        Assert.True(rig.Hold.TryStore(rig.Mint()));

        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Tick(rig, 2, .3f);
        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Tick(rig, 2, .3f); // the first ran out on the fourth tick overall; the second has 0.4 s left
        Assert.Equal(20f, rig.Engine.Evaluate(rig.Thrust), 3);
        Assert.True(resolver.ModifierEntryCount > baseline);

        Tick(rig, 2, .3f);
        Assert.Equal(10f, rig.Engine.Evaluate(rig.Thrust), 3);
        Assert.Equal(baseline, resolver.ModifierEntryCount);
    }

    // CS-R1: the cooldown is per design. A running effect of one design does not refuse another design.
    [Fact]
    public void ADesignsCooldownDoesNotBlockAnotherDesign()
    {
        var rig = Build();
        using var _ = rig.Cache;
        Assert.True(rig.Hold.TryStore(rig.Mint()));
        Assert.True(rig.Hold.TryStore(rig.Mint(rig.Other)));

        Assert.True(rig.Ship.TryActivateConsumable(rig.Consumable));
        Assert.True(rig.Ship.TryActivateConsumable(rig.Other));
        Assert.Equal(0, rig.InCargo);
    }

    // Docking: an effect still running when the entity deactivates, is refitted and activates again boosts the
    // items now fitted, as gear modifiers do, not the thruster that was swapped out.
    [Fact]
    public void AConsumableActiveAcrossARefitBoostsTheNewItems()
    {
        var rig = Build();
        using var _ = rig.Cache;
        rig.Ship.ActivateConsumable(rig.Mint());
        Tick(rig, 2, .3f);
        Assert.Equal(20f, rig.Engine.Evaluate(rig.Thrust), 3);

        rig.Ship.Deactivate();
        Assert.NotNull(rig.Ship.TryUnequip(rig.Engine));
        Assert.True(rig.Ship.TryEquip(rig.NewEngine(), EngineCell));
        rig.Ship.Activate();
        Tick(rig, 2, .1f); // 0.2 s more: the effect still has 0.2 s of its second
        var fitted = rig.Ship.Equipment.Single(e => e.Data.Name == "Engine");
        Assert.NotSame(rig.Engine, fitted);
        Assert.Equal(20f, fitted.Evaluate(rig.Thrust), 3);
    }
}
