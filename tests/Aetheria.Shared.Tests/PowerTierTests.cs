using System;
using System.IO;
using System.Linq;
using Xunit;
using CultMath;
using GameCult.Caching;

// Cut 5 (docs/stats-and-power-cut.md §1.3): replaces PowerBus's single shared GrantRatio with priority tiers
// (PowerTiers.cs). This file pins Cut 5's own verification bullets plus the "rules worth pinning" the cut brief
// names: critical fed in full while a lower tier starves; two items in one tier rationed proportionally
// regardless of equip order; a tier with no demand passes its share down; reactor throttling changes which
// tiers starve; and a tier boundary does not leak. Each rule has a matching mutation in
// tests/mutation_tests_stats_power_cut5.py.
//
// Fixture shape mirrors PowerBusTests.OpenCatalog/BuildShip (see its comments for why: a 3x3 interior hull, wide
// heat bounds, a Reactor whose Charge each test sets directly). EquipDrain here takes an explicit tier so a test
// can put two Drains in different tiers without waiting on -- or fighting -- EnergyDraw's own kind default
// (PowerTiers.Utility for both), which would otherwise put every Drain in this fixture in the same tier.
public sealed class PowerTierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-powertier-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public PowerTierTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private CultCache OpenCatalog()
    {
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
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
        cache.FlushAsync().Wait();
        return cache;
    }

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new System.Collections.Generic.List<RoleFill>() })
    };

    private (Ship ship, ItemManager items, Reactor reactor) BuildShip(CultCache cache, float reactorCharge)
    {
        var ledger = new ProvenanceLedger();
        var items = new ItemManager(cache, ledger, Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);
        Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));

        zone.Entities.Add(ship);
        ship.Activate();

        var reactor = ship.GetBehavior<Reactor>();
        ship.Update(0f);
        return (ship, items, reactor);
    }

    // Every Drain needs its own catalog record and PerformanceStat instance (PowerBusTests.EquipDrain's own
    // comment explains why: shared resolver entries keyed on the record would otherwise let two Drains fight
    // over one mutated rate). The tier is set on the minted unit BEFORE equipping it, so EquippedItem's
    // constructor -- which only ever seeds a kind default onto PowerTiers.Unassigned -- never overwrites it;
    // this is exactly the "player choice, not a derivation" path §1.3 describes, just exercised directly instead
    // of through a schematic-UI click.
    private EquippedItem EquipDrain(CultCache cache, Ship ship, float rate, int tier)
    {
        var perItemData = cache.Upsert(new GearData
        {
            Name = "Drain " + Guid.NewGuid(), Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new EnergyDrawData { EnergyDraw = Constant(rate), PerSecond = true } }
        });
        cache.FlushAsync().Wait();
        ship.Deactivate();
        var unit = Mint(cache, ship.ItemManager, cache.Get(perItemData));
        unit.PowerTier = tier;
        Assert.True(ship.TryEquip(unit));
        ship.Activate();
        return ship.Equipment.Last();
    }

    // --- Cut 5 verification bullet 1 / "rules worth pinning" #1: "with generation short of total demand, tier 0
    // --- is satisfied in full before tier 1 receives anything." Generation (30) covers Critical's own demand
    // --- (30) exactly, leaving nothing at all for Utility (50) -- the map's own "starves first" framing, pinned
    // --- at its sharpest: full satisfaction for the fed tier, total starvation for the other. ---
    [Fact]
    public void CriticalTierIsFedInFullWhileLowerTierStarves()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 30);
        var critical = EquipDrain(cache, ship, rate: 30, tier: PowerTiers.Critical);
        var utility = EquipDrain(cache, ship, rate: 50, tier: PowerTiers.Utility);

        ship.Update(1f);

        Assert.Equal(1f, critical.PowerSupply, 3);
        Assert.Equal(0f, utility.PowerSupply, 3);
        Assert.Equal(1f, ship.PowerBus.TierGrantRatio[PowerTiers.Critical], 3);
        Assert.Equal(0f, ship.PowerBus.TierGrantRatio[PowerTiers.Utility], 3);
    }

    // --- "Rules worth pinning" #2: "two items in one tier are rationed proportionally regardless of equip
    // --- order." Two Medium consumers, unequal requests (20 and 40), generation covering half the tier's total
    // --- demand (30 of 60): both must land at the same .5 ratio, and their absolute grants (10 and 20) must
    // --- still be proportional to their own requests, not equal to each other. ---
    [Fact]
    public void TwoConsumersInOneTierAreRationedProportionally()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 30);
        var small = EquipDrain(cache, ship, rate: 20, tier: PowerTiers.Medium);
        var large = EquipDrain(cache, ship, rate: 40, tier: PowerTiers.Medium);

        ship.Update(1f);

        Assert.Equal(.5f, small.PowerSupply, 3);
        Assert.Equal(.5f, large.PowerSupply, 3);
        Assert.Equal(10f, 20f * small.PowerSupply, 3); // the small request's absolute grant: half of 20
        Assert.Equal(20f, 40f * large.PowerSupply, 3); // the large request's absolute grant: half of 40, not equal to the small one's
    }

    // --- "Rules worth pinning" #3: "a tier with no demand passes its share down." Only a Utility consumer is
    // --- equipped -- Critical, High, Medium and Low all sit at zero demand -- under a shortfall (generation 10
    // --- against a request of 40). If an empty tier silently swallowed its share instead of passing it on, this
    // --- ratio would come out far below the true .25 the single active tier is owed. ---
    [Fact]
    public void ATierWithNoDemandPassesItsShareDown()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 10);
        var utility = EquipDrain(cache, ship, rate: 40, tier: PowerTiers.Utility);

        ship.Update(1f);

        Assert.Equal(.25f, utility.PowerSupply, 3);
        Assert.Equal(.25f, ship.PowerBus.GrantRatio, 3);
        // F3 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): an empty tier reports PowerBus.NoDemand (NaN),
        // not the "fully met" signal 1f -- there was nothing here to satisfy, and its share still passed down
        // (proven by utility's own .25 above, not by a since-corrected "1f" reading on the empty tiers).
        for (var tier = 0; tier < PowerTiers.Utility; tier++)
            Assert.True(float.IsNaN(ship.PowerBus.TierGrantRatio[tier]), $"tier {tier} should report NoDemand, read {ship.PowerBus.TierGrantRatio[tier]}");
    }

    // --- "Rules worth pinning" #4: "reactor throttling changes what is available and therefore which tiers
    // --- starve." Same two-tier loadout (Critical 30, Utility 50) at two different reactor charges: a weak
    // --- reactor starves Utility entirely (as CriticalTierIsFedInFullWhileLowerTierStarves already pins); a
    // --- strong one (80, covering both) starves nothing. The player's own reactor-tier choice is what moves a
    // --- subsystem across that line, exactly the ruling's "reactor throttling is a player choice." ---
    [Fact]
    public void ReactorThrottlingChangesWhichTiersStarve()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 80);
        var critical = EquipDrain(cache, ship, rate: 30, tier: PowerTiers.Critical);
        var utility = EquipDrain(cache, ship, rate: 50, tier: PowerTiers.Utility);

        ship.Update(1f);

        Assert.Equal(1f, critical.PowerSupply, 3);
        Assert.Equal(1f, utility.PowerSupply, 3); // unlike the 30-charge case, generation now covers both in full
    }

    // --- "Rules worth pinning" #5: "a tier boundary does not leak." Critical alone outweighs generation (demand
    // --- 100 against 40), so Critical itself is starved (.4). A lower tier must still receive exactly zero --
    // --- not some leftover sliver reasoned from Critical's own shortfall -- because remaining hits 0 once
    // --- Critical's grant is committed, before Utility is ever considered. ---
    [Fact]
    public void LowerTierGetsExactlyZeroWhenAHigherTierAloneStarves()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 40);
        var critical = EquipDrain(cache, ship, rate: 100, tier: PowerTiers.Critical);
        var utility = EquipDrain(cache, ship, rate: 50, tier: PowerTiers.Utility);

        ship.Update(1f);

        Assert.Equal(.4f, critical.PowerSupply, 3);
        Assert.Equal(0f, utility.PowerSupply, 3);
    }

    // --- Cut 5's own verification bullet: "a tier change on an equipped item takes effect on the next tick with
    // --- no re-equip." §1.3 calls the tier "a stored player choice, not a derivation" -- this proves the store
    // --- is live, not latched at equip time. A fixed Critical consumer (5) and generation of 15 leave 10 for a
    // --- Utility mover requesting 20 (.5 ratio); moving the mover into Critical alongside the fixed one changes
    // --- the tier's own total demand to 25 against the same 15 supply (.6 ratio) on the very next Step, with no
    // --- unequip/re-equip round trip -- proof the read is live, not a value latched once at equip time.
    [Fact]
    public void ATierChangeOnAnEquippedItemTakesEffectNextTickWithNoReEquip()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 15);
        EquipDrain(cache, ship, rate: 5, tier: PowerTiers.Critical);
        var mover = EquipDrain(cache, ship, rate: 20, tier: PowerTiers.Utility);

        ship.Update(1f);
        Assert.Equal(.5f, mover.PowerSupply, 3);

        mover.EquippableItem.PowerTier = PowerTiers.Critical;
        ship.Update(1f);

        Assert.Equal(.6f, mover.PowerSupply, 3);
    }

    // --- F3 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): "a sub-epsilon demand is granted out of
    // --- nothing." A tier whose real demand is genuinely tiny (5e-5, below the old 1e-4 free-pass threshold) is
    // --- not empty -- it must be rationed at a total blackout exactly like any other tier: PowerSupply 0, not
    // --- the old code's unconditional 1. ---
    [Fact]
    public void SubEpsilonDemandGetsNoGrantAtTotalBlackout()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 0);
        var tiny = EquipDrain(cache, ship, rate: 5e-5f, tier: PowerTiers.Critical);

        ship.Update(1f);

        Assert.Equal(0f, ship.PowerBus.TotalGrant, 6);
        Assert.Equal(0f, tiny.PowerSupply, 4); // NOT 1f -- there is no power to grant
    }

    // --- The other half of the same bug: a higher tier's sub-epsilon demand must still be subtracted from
    // --- `remaining`, or a lower tier is overfed by the sliver the higher tier was never charged for. Critical's
    // --- own tiny request (5e-5) and Utility's real one (10) together exceed the 10 generated by exactly that
    // --- sliver; the two tiers' actual delivered energy must never exceed what the ship made. ---
    [Fact]
    public void HigherTierSubEpsilonDemandIsStillSubtractedFromRemaining()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 10);
        var critical = EquipDrain(cache, ship, rate: 5e-5f, tier: PowerTiers.Critical);
        var utility = EquipDrain(cache, ship, rate: 10f, tier: PowerTiers.Utility);

        ship.Update(1f);

        var delivered = 5e-5f * critical.PowerSupply + 10f * utility.PowerSupply;
        Assert.True(delivered <= ship.PowerBus.TotalGrant + 1e-6f,
            $"delivered {delivered} against a total grant of {ship.PowerBus.TotalGrant}");
    }

    // --- The first named bug, directly: "an empty tier reports fully supplied." Only a Utility consumer is
    // --- equipped and the ship is fully blacked out -- Critical must not read as 1f ("fine"), it must read as
    // --- PowerBus.NoDemand, because there is nothing in Critical to be fine or starved. ---
    [Fact]
    public void EmptyTierAtBlackoutReportsNoDemandNotFullySupplied()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 0);
        EquipDrain(cache, ship, rate: 10f, tier: PowerTiers.Utility);

        ship.Update(1f);

        Assert.True(float.IsNaN(ship.PowerBus.TierGrantRatio[PowerTiers.Critical]),
            $"empty Critical tier should report NoDemand at blackout, read {ship.PowerBus.TierGrantRatio[PowerTiers.Critical]}");
        Assert.Equal(0f, ship.PowerBus.TierGrantRatio[PowerTiers.Utility], 3); // Utility itself is genuinely starved
    }
}
