using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Cut 3 (docs/stats-and-power-cut.md): the power bus replaces Entity.TryConsumeEnergy/CanConsumeEnergy. At HEAD
// every draw succeeded as long as one reactor was online, and any shortfall was silently taxed onto the reactor
// as heat (§0.5) -- an assertion pinning "demand beyond supply is refused" was unwritable against that code.
// This file pins PowerBus's three verification bullets from the cut map's Cut 3 section. Each rule has a
// matching mutation in tests/mutation_tests_stats_power_cut3.py.
//
// Cut 4 (docs/stats-and-power-cut.md, Cut 4) deleted this file's Entity.TrySpendCapacitorCharge atomicity test
// along with the method itself -- the named, temporary exception Cut 3 left standing for the four instant
// draws is closed; see InputCapacitorTests.cs and mutation_tests_stats_power_cut4.py instead.
public sealed class PowerBusTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-powerbus-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 HullOrigin = new int2(0, 0);

    public PowerBusTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // A hull with a 5x5 fully-interior Shape: HullData.InteriorCells shrinks the border away, leaving a 3x3
    // (9-cell) interior -- enough room for several Tool-hardpoint items (Reactor/Capacitor/Drain all use Tool,
    // which needs an interior cell of its own, unlike the Sensors-hardpoint fixtures elsewhere in this suite).
    // Every gear item's heat-response bounds are authored wide around the hull's default 280-degree start
    // temperature (MapEntity), so ThermalOnline is true without the test ever having to touch Entity.Temperature.
    private CultCache OpenCatalog()
    {
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        // Mass must be nonzero: HullData.SpecificHeat defaults to 1 but Mass defaults to 0, and Entity.MapEntity
        // divides by it to seed ThermalMass -- a zero ThermalMass turns UpdateTemperature's conduction average
        // into a division by zero (NaN) the moment a real tick (ship.Update, not just UpdatePerformance) runs.
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ReactorData
            {
                Charge = Constant(0), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2)
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Capacitor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new CapacitorData { Capacity = Constant(1000), Efficiency = Constant(1) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Drain", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new EnergyDrawData { EnergyDraw = Constant(0), PerSecond = true } }
        });
        cache.FlushAsync().Wait();
        return cache;
    }

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    // Reactor generation (per tick, dt=1), starting capacitor charge, and one Drain's requested rate (per
    // second). Equips in the given order -- opposite orders are how OppositeEquipOrderGetsIdenticalGrants pins
    // the death of the old "whoever equipped first drains the capacitors first" hidden priority (§0.5).
    private (Ship ship, ItemManager items, Reactor reactor, Capacitor capacitor) BuildShip(
        CultCache cache, float reactorCharge, float startingCapacitorCharge, bool reactorFirst)
    {
        var ledger = new ProvenanceLedger();
        var items = new ItemManager(cache, ledger, Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        var capacitorData = cache.GetByName<GearData>("Capacitor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);

        void EquipReactor() => Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));
        void EquipCapacitor() => Assert.True(ship.TryEquip(Mint(cache, items, capacitorData)));

        if (reactorFirst) { EquipReactor(); EquipCapacitor(); } else { EquipCapacitor(); EquipReactor(); }

        zone.Entities.Add(ship);
        ship.Activate();

        var reactor = ship.GetBehavior<Reactor>();
        var capacitor = ship.GetBehavior<Capacitor>();
        // Capacitor.Capacity defaults to 0 until its own Execute has run once (it clamps AddCharge to
        // [0, Capacity]), so a zero-dt warm-up tick has to happen before seeding starting charge.
        ship.Update(0f);
        if (startingCapacitorCharge > 0) capacitor.AddCharge(startingCapacitorCharge);
        return (ship, items, reactor, capacitor);
    }

    // TryEquip refuses while the entity is active (Entity.cs), so a Drain added after BuildShip's own Activate()
    // needs a deactivate/equip/reactivate round trip -- harmless here: nothing this fixture cares about (charge,
    // durability) is reset by Deactivate/Activate, only the hostility/subscription bookkeeping neither test uses.
    private EquippedItem EquipDrain(CultCache cache, Ship ship, float rate)
    {
        // Each Drain item needs its own catalog record and PerformanceStat instance -- the "Drain" record from
        // OpenCatalog is shared and evaluated by generation-cached resolver entries keyed on it, so two Drains
        // sharing one mutated PerformanceStat would fight over the same rate.
        var perItemData = cache.Upsert(new GearData
        {
            Name = "Drain " + Guid.NewGuid(), Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new EnergyDrawData { EnergyDraw = Constant(rate), PerSecond = true } }
        });
        cache.FlushAsync().Wait();
        ship.Deactivate();
        Assert.True(ship.TryEquip(Mint(cache, ship.ItemManager, cache.Get(perItemData))));
        ship.Activate();
        return ship.Equipment.Last();
    }

    // --- Cut 3 verification bullet 1: "a ship whose requests exceed generation gets a total grant equal to
    // --- generation plus available capacitor charge, and not more." At HEAD this assertion was unwritable
    // --- because every draw succeeded (Entity.TryConsumeEnergy always found an online reactor to bill). ---
    [Fact]
    public void GrantIsCappedAtGenerationPlusStoredCapacitorCharge()
    {
        using var cache = OpenCatalog();
        var (ship, _, _, capacitor) = BuildShip(cache, reactorCharge: 10, startingCapacitorCharge: 5, reactorFirst: true);
        EquipDrain(cache, ship, rate: 100);

        ship.Update(1f);

        Assert.Equal(15f, ship.PowerBus.TotalGrant, 3);      // 10 generation + 5 stored charge, not 100
        Assert.Equal(.15f, ship.PowerBus.GrantRatio, 3);
        Assert.Equal(0f, capacitor.Charge, 3);                // the whole stored reserve was spent covering the gap
    }

    // --- Cut 3 verification bullet 2: "a refused request produces no effect at all -- not a partial one, and
    // --- not an overload-heat receipt." Zero generation and zero stored charge: the bus must not finance any of
    // --- the demand, the exact HEAD bug (§0.5: "every draw succeeds while one reactor is online"). Map's own
    // --- mutation: "make the bus grant the full request and heat the reactor; it goes red." ---
    [Fact]
    public void ZeroSupplyRefusesTheWholeRequest()
    {
        using var cache = OpenCatalog();
        var (ship, _, _, _) = BuildShip(cache, reactorCharge: 0, startingCapacitorCharge: 0, reactorFirst: true);
        EquipDrain(cache, ship, rate: 50);

        ship.Update(1f);

        Assert.Equal(0f, ship.PowerBus.TotalGrant, 3);
        Assert.Equal(0f, ship.PowerBus.GrantRatio, 3);
    }

    // --- Cut 3 verification bullet 3: "two ships with identical loadouts fitted in opposite equip order get
    // --- identical grants. That is the death of the hidden priority, and it fails at HEAD." At HEAD,
    // --- EquippedItem.SortPosition (set only by Reactor) decided who drained the capacitors first (§0.5); here
    // --- every consuming item shares the bus's one GrantRatio regardless of equip order, so the two ships (and
    // --- the two consumers on either) can never diverge by position. ---
    [Fact]
    public void OppositeEquipOrderGetsIdenticalGrants()
    {
        using var cacheA = OpenCatalog();
        var (shipA, _, _, _) = BuildShip(cacheA, reactorCharge: 10, startingCapacitorCharge: 0, reactorFirst: true);
        EquipDrain(cacheA, shipA, rate: 40);

        using var cacheB = OpenCatalog();
        var (shipB, _, _, _) = BuildShip(cacheB, reactorCharge: 10, startingCapacitorCharge: 0, reactorFirst: false);
        EquipDrain(cacheB, shipB, rate: 40);

        shipA.Update(1f);
        shipB.Update(1f);

        Assert.Equal(shipA.PowerBus.GrantRatio, shipB.PowerBus.GrantRatio, 5);
        Assert.Equal(shipA.PowerBus.TotalGrant, shipB.PowerBus.TotalGrant, 5);
    }

    // The same invariant bullet 3 rests on, pinned directly: with two consumers sharing one shortfall, BOTH read
    // back the identical GrantRatio (never "first equipped gets its full request, second one starves"), which is
    // what makes equip order unable to matter in the first place.
    [Fact]
    public void EveryConsumingItemReadsTheSameGrantRatio()
    {
        using var cache = OpenCatalog();
        var (ship, _, _, _) = BuildShip(cache, reactorCharge: 10, startingCapacitorCharge: 0, reactorFirst: true);
        var drainOne = EquipDrain(cache, ship, rate: 30);
        var drainTwo = EquipDrain(cache, ship, rate: 30);

        ship.Update(1f);

        Assert.True(ship.PowerBus.GrantRatio < 1f); // a real shortfall: 60 requested against 10 generated
        Assert.Equal(ship.PowerBus.GrantRatio, drainOne.PowerSupply, 5);
        Assert.Equal(ship.PowerBus.GrantRatio, drainTwo.PowerSupply, 5);
    }

    // --- F2 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): "one predicate, both directions." Step used to
    // --- collect every equipped IPowerConsumer's request unconditionally, but Entity.Update only ever runs a
    // --- behaviour's Execute when the item is Active (Enabled && Online) -- so a consumer the player switched
    // --- off (Enabled = false) never executes, never spends anything, and yet was still billed here, starving
    // --- every live consumer and draining the capacitors for energy nobody spent. A disabled consumer must
    // --- contribute nothing to TotalDemand at all. ---
    [Fact]
    public void DisabledConsumerContributesNoDemand()
    {
        using var cache = OpenCatalog();
        var (ship, _, _, capacitor) = BuildShip(cache, reactorCharge: 0, startingCapacitorCharge: 100, reactorFirst: true);
        var drain = EquipDrain(cache, ship, rate: 50);
        drain.Enabled.Value = false;
        Assert.False(drain.Active.Value);

        ship.Update(1f);

        Assert.Equal(0f, ship.PowerBus.TotalDemand, 3);
        Assert.Equal(100f, capacitor.Charge, 3); // nothing was spent on a request nobody will ever execute
    }

    // --- The other direction of the same rule: PowerBus.Step gated a reactor on Item.Online alone, but
    // --- Reactor.Execute (the behaviour that actually produces the heat receipt) only runs when Item.Active --
    // --- Enabled && Online -- is true. A reactor the player switched off stays Online (thermal/durability are
    // --- fine) but is not Active, so it must not generate here either, or it supplies the whole ship for free
    // --- with no heat cost at all. ---
    [Fact]
    public void DisabledReactorGeneratesNoPower()
    {
        using var cache = OpenCatalog();
        var (ship, _, reactor, _) = BuildShip(cache, reactorCharge: 100, startingCapacitorCharge: 0, reactorFirst: true);
        reactor.Item.Enabled.Value = false;
        Assert.True(reactor.Item.Online.Value);
        Assert.False(reactor.Item.Active.Value);
        EquipDrain(cache, ship, rate: 100);

        ship.Update(1f);

        Assert.Equal(0f, ship.PowerBus.TotalGeneration, 3);
        Assert.Equal(0f, ship.PowerBus.TotalGrant, 3);
    }
}
