using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Cut 6 (docs/stats-and-power-cut.md): "the power-supply term and the request-independence rule." Purely
// additive -- StatSource.PowerSupply already existed as an identity everywhere (Cut 2), and the bus already wrote
// EquippedItem.PowerSupply (Cut 3/5). This file pins the two things Cut 6 actually adds: a stat that declares a
// PowerSupply term now really degrades as the grant falls, and a stat that decides a power request can never read
// that same term back, directly or through a modifier chain. Each rule has a matching mutation in
// tests/mutation_tests_stats_power_cut6.py.
public sealed class PowerCurveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-powercurve-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 HardpointCell = new int2(0, 0);
    private static readonly int2 SecondHardpointCell = new int2(1, 0);

    public PowerCurveTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    // Mirrors PowerBusTests/PowerTierTests' own fixture: a 5x5 hull (a real interior for Tool-hardpoint items),
    // wide heat bounds around the default start temperature so ThermalOnline never has to be driven separately,
    // and a Reactor whose Charge each test sets directly (dt=1 in every Update call here, so reactorCharge reads
    // straight as this tick's generation, same as PowerBusTests). The consumer is an EnergyDrawData -- exactly
    // PowerBusTests' own "Drain" -- rather than Thruster: Thruster's own request is gated on its Axis, which Ship
    // itself recomputes from MovementDirection every tick (Ship.cs, its own thruster-axis pass), so an axis set
    // directly on the behaviour is clobbered before PowerBus.Step ever reads it. EnergyDraw's request is a plain
    // authored constant, with nothing else deciding it -- deterministic demand is the whole point of this
    // fixture, not thruster flight. curveStat is a second PerformanceStat on the same item (CapacitorData's own
    // Capacity, which no IPowerConsumer ever requests through) declaring the PowerSupply term under test: reading
    // it through the item's Evaluate proves the resolver's own curve, independent of what any particular
    // behaviour does with a partial grant.
    private CultCache OpenCatalog(PerformanceStat curveStat)
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
        cache.Upsert(new GearData
        {
            Name = "Engine", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors =
            {
                new EnergyDrawData { EnergyDraw = Constant(100), PerSecond = true },
                new CapacitorData { Capacity = curveStat, Efficiency = Constant(1) }
            }
        });
        cache.FlushAsync().Wait();
        return cache;
    }

    private Ship BuildShip(CultCache cache, float reactorCharge)
    {
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);
        Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Engine"))));

        zone.Entities.Add(ship);
        ship.Activate();
        return ship;
    }

    // --- Cut 6 verification bullet 1: "a thruster at half grant produces less thrust, by the authored exponent" --
    // --- generalised to any PowerSupply-termed stat, since the resolver does not care which behaviour declared
    // --- it. curveStat's exponent-2 term must degrade to pow(.5, 2) == .25 of its range, not the linear .5 a
    // --- careless implementation (or a mutation that drops the exponent) would produce.
    // ---
    // --- Reads the stat once at full grant BEFORE dropping the reactor's own charge, so the resolver has a real
    // --- cached (generation-stamped) value in hand when the grant actually changes -- a fixture that only ever
    // --- reads once, after the fact, can never tell "recomputed correctly" from "never cached, so it happened to
    // --- read fresh": the missing invalidate half of this cut's own wiring (PowerBus.AllocateTiers) only shows up
    // --- against a stat that was already cached under the old grant. ---
    [Fact]
    public void APowerSupplyTermedStatAtHalfGrantDegradesByItsAuthoredExponent()
    {
        var curve = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 2 } } };
        using var cache = OpenCatalog(curve);
        var ship = BuildShip(cache, reactorCharge: 1000); // over-supplied: ratio 1, establishes a real cache entry

        ship.Update(1f);
        var engine = ship.Equipment.Single(e => e.Data.Name == "Engine");
        Assert.Equal(1f, engine.PowerSupply, 3);
        Assert.Equal(100f, engine.Evaluate(curve), 3); // cached at full grant

        var reactor = (ReactorData) ship.Equipment.Single(e => e.Data.Name == "Reactor").Data.Behaviors[0];
        reactor.Charge = Constant(50); // request 100, generation 50 -> ratio .5
        ship.Update(1f);

        Assert.Equal(100f, ship.PowerBus.TotalDemand, 3);
        Assert.Equal(.5f, engine.PowerSupply, 3);
        Assert.Equal(25f, engine.Evaluate(curve), 2); // lerp(0, 100, pow(.5, 2)) == 25, not the stale 100
    }

    // --- The other half of the same rule, named explicitly in the ruling ("unchanged at full supply"): a
    // --- PowerSupply term is the identity at grant 1 regardless of its exponent -- pow(1, n) == 1 -- so an item
    // --- that is never starved reads exactly as if it had never declared the term. ---
    [Fact]
    public void APowerSupplyTermedStatAtFullGrantIsUnaffectedByItsExponent()
    {
        var curve = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 5 } } };
        using var cache = OpenCatalog(curve);
        var ship = BuildShip(cache, reactorCharge: 1000); // generation far exceeds the 100 request

        ship.Update(1f);

        var engine = ship.Equipment.Single(e => e.Data.Name == "Engine");
        Assert.Equal(1f, engine.PowerSupply, 3);
        Assert.Equal(100f, engine.Evaluate(curve), 3);
    }

    // --- F1 (docs/stats-and-power-cut.md, operator ruling 2026-09-19): "power supply multiplies, it does not
    // --- interpolate." Before this fix PowerSupply was one more term feeding PerformanceStat's Min/Max lerp, so
    // --- zero supply bottomed out at Min, not 0 -- Soul measured a radiator still pumping 19-25% of full heat at
    // --- zero power. Min here is 1000 (nonzero), so lerp(1000, 4000, pow(0, 2)) would read 1000 under the old
    // --- code; the multiplier form must read exactly 0 regardless of Min. ---
    [Fact]
    public void APowerSupplyTermedStatWithNonzeroMinResolvesToExactlyZeroAtZeroSupply()
    {
        var curve = new PerformanceStat { Min = 1000, Max = 4000, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 2 } } };
        using var cache = OpenCatalog(curve);
        var ship = BuildShip(cache, reactorCharge: 0); // no generation, no stored charge -> demand of 100 is unmet

        ship.Update(1f);

        var engine = ship.Equipment.Single(e => e.Data.Name == "Engine");
        Assert.Equal(0f, engine.PowerSupply, 4);
        Assert.Equal(0f, engine.Evaluate(curve), 3); // NOT 1000 (Min) -- the old lerp-based bug's exact number
    }

    // --- The ruling's other named consequence: "a stat whose Min equals its Max still responds" -- three of Cut
    // --- 7's eight authored terms were inert for exactly this reason, because lerp(x, x, anything) == x. The
    // --- multiplier form has no interpolation to degenerate, so it must still move. ---
    [Fact]
    public void AMinEqualsMaxStatStillRespondsToItsPowerSupplyTerm()
    {
        var curve = new PerformanceStat { Min = 2500, Max = 2500, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using var cacheStarved = OpenCatalog(curve);
        var starved = BuildShip(cacheStarved, reactorCharge: 0);
        starved.Update(1f);
        var starvedEngine = starved.Equipment.Single(e => e.Data.Name == "Engine");

        var curve2 = new PerformanceStat { Min = 2500, Max = 2500, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using var cacheFed = OpenCatalog(curve2);
        var fed = BuildShip(cacheFed, reactorCharge: 1000);
        fed.Update(1f);
        var fedEngine = fed.Equipment.Single(e => e.Data.Name == "Engine");

        Assert.Equal(0f, starvedEngine.Evaluate(curve), 3);   // was inert (always 2500) before this fix
        Assert.Equal(2500f, fedEngine.Evaluate(curve2), 3);
        Assert.NotEqual(fedEngine.Evaluate(curve2), starvedEngine.Evaluate(curve));
    }

    // A small, deliberately fake context (StatResolverTests' own pattern) so the "no cost when undeclared" rule
    // is provable without any catalog, entity or item plumbing -- and so it can count exactly how many times the
    // resolver ever asked for the power-supply factor.
    private sealed class CountingContext : IStatContext
    {
        public Lot Lot { get; set; }
        public int PowerSupplyFactorCalls;
        public int HeatFactorCalls;
        public float HeatFactor(float exponent) { HeatFactorCalls++; return 1f; }
        public float DurabilityFactor(float exponent) => 1f;
        public float ConsumableProgressFactor(float exponent) => 1f;
        public float PowerSupplyFactor(float exponent) { PowerSupplyFactorCalls++; return 1f; }
        public float ScaleModifier(PerformanceStat stat) => 1f;
        public float ConstantModifier(PerformanceStat stat) => 0f;
    }

    // --- Cut 6 verification bullet 4 / the operator's own "don't pay that cost for every stat evaluation" as an
    // --- assertion: a stat with no PowerSupply term never asks the context for one, at all, cache or no cache --
    // --- proving the term list genuinely is not walked for a source the stat never declared, not merely that the
    // --- answer happens to be 1. ---
    [Fact]
    public void AStatWithNoPowerSupplyTermNeverAsksForOne()
    {
        var resolver = new StatResolver();
        var owner = new object();
        var context = new CountingContext();
        var stat = new PerformanceStat { Min = 0, Max = 10, Terms = { new StatTerm { Source = StatSource.Heat, Exponent = 1 } } };

        resolver.Resolve(owner, stat, context);
        resolver.InvalidateSource(owner, StatSource.PowerSupply); // the bus's own per-tick call, unconditional
        resolver.Resolve(owner, stat, context);

        Assert.Equal(1, context.HeatFactorCalls); // recomputed once, for its own declared source
        Assert.Equal(0, context.PowerSupplyFactorCalls); // never once asked -- this stat has no PowerSupply term
    }

    // --- The same bullet's other half: a stat that DOES declare the term recomputes "at most once per tick," not
    // --- once per read and not once per grant change. PowerBus invalidates PowerSupply unconditionally, exactly
    // --- once per draw per Step (mirroring Heat/Durability's own "every tick, not only when the value moved"
    // --- scheme) -- this pins that shape directly against the resolver, independent of PowerBus's own plumbing.
    // --- ---
    [Fact]
    public void APowerTermedStatRecomputesAtMostOncePerTick()
    {
        var resolver = new StatResolver();
        var owner = new object();
        var context = new CountingContext();
        var stat = new PerformanceStat { Min = 0, Max = 10, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };

        // Tick 1: the bus invalidates once; three reads inside the tick must share one evaluation.
        resolver.InvalidateSource(owner, StatSource.PowerSupply);
        resolver.Resolve(owner, stat, context);
        resolver.Resolve(owner, stat, context);
        resolver.Resolve(owner, stat, context);
        Assert.Equal(1, context.PowerSupplyFactorCalls);

        // Tick 2: the bus invalidates again (PowerBus.AllocateTiers does this every tick, moved or not).
        resolver.InvalidateSource(owner, StatSource.PowerSupply);
        resolver.Resolve(owner, stat, context);
        Assert.Equal(2, context.PowerSupplyFactorCalls); // exactly one more -- not zero, not three again
    }

    // --- Cut 6 verification bullet 2: "a catalog holding a thruster whose EnergyUsage carries a PowerSupply term
    // --- is refused at load, naming the item and the stat." Written through the raw UpsertAsync (bypassing
    // --- CultRecordRefs.Upsert's own validation, AetheriaStoresTests.WeaponWrittenThroughGearHandleReloadsAsWeapon's
    // --- own pattern for reaching disk with something Upsert would refuse) so this pins AetheriaStores.Open's own
    // --- half of the rule, not Upsert's. ---
    [Fact]
    public void OpenRefusesAThrusterWhoseEnergyUsageCarriesAPowerSupplyTerm()
    {
        var badEnergy = new PerformanceStat { Min = 1, Max = 1, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            cache.UpsertAsync(new GearData
            {
                Name = "BadThruster", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
                MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
                Behaviors = { new ThrusterData { EnergyUsage = badEnergy } }
            }).Wait();
            cache.FlushAsync().Wait();
        }

        var error = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(Catalog));
        Assert.Contains("BadThruster", error.Message);
        Assert.Contains(nameof(ThrusterData), error.Message);
        Assert.Contains(nameof(ThrusterData.EnergyUsage), error.Message);
    }

    // The same rule at Upsert (the write path every tool, test and migration script actually goes through), so a
    // bad record is refused before it ever reaches disk -- not just the next time someone reopens the catalog.
    [Fact]
    public void UpsertRefusesAThrusterWhoseEnergyUsageCarriesAPowerSupplyTerm()
    {
        var badEnergy = new PerformanceStat { Min = 1, Max = 1, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });

        var error = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
        {
            Name = "BadThruster", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ThrusterData { EnergyUsage = badEnergy } }
        }));
        Assert.Contains("BadThruster", error.Message);
        Assert.Contains(nameof(ThrusterData.EnergyUsage), error.Message);
    }

    // --- F6 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): the registry used to name only each consumer's
    // --- top-level request field, so a PowerSupply term on a stat a request reads only indirectly -- here,
    // --- Radiator.PowerRequest's own early-out gate on PumpedHeat, exactly the shape Soul reproduced against
    // --- the shipped catalog's "OK Disperser" (SOUL_RadiatorRequestDependsOnItsOwnGrantAndOscillates) -- sailed
    // --- straight through Upsert. PumpedHeat is now in PowerRequestFields, so this must be refused. ---
    [Fact]
    public void UpsertRefusesARadiatorWhosePumpedHeatCarriesAPowerSupplyTerm()
    {
        var badPumpedHeat = new PerformanceStat { Min = 1000, Max = 4000, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });

        var error = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
        {
            Name = "BadRadiator", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new RadiatorData
            {
                PumpedHeat = badPumpedHeat, WasteHeat = Constant(100), EnergyUsage = Constant(6),
                Emissivity = Constant(.5f), ThermalMass = Constant(100), TemperatureFloor = 0
            } }
        }));
        Assert.Contains("BadRadiator", error.Message);
        Assert.Contains(nameof(RadiatorData.PumpedHeat), error.Message);
    }

    // --- The same hole on the other consumer the ruling names by field: Shield.PowerRequest calls RefreshReserve,
    // --- which reads RefillDuration (or RestoreDuration while broken) to size the reserve's own fill rate -- a
    // --- PowerSupply term there makes the reserve's own refill speed depend on how much of it was already
    // --- granted, the same self-reference the rule exists to forbid. ---
    [Fact]
    public void UpsertRefusesAShieldWhoseRefillDurationCarriesAPowerSupplyTerm()
    {
        var badRefillDuration = new PerformanceStat { Min = 2, Max = 2, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });

        var error = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
        {
            Name = "BadShield", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ShieldData
            {
                Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(100),
                RefillDuration = badRefillDuration, RestoreDuration = Constant(5)
            } }
        }));
        Assert.Contains("BadShield", error.Message);
        Assert.Contains(nameof(ShieldData.RefillDuration), error.Message);
    }

    // --- Cut 6 verification bullet 3: "the same refusal through a modifier chain, at equip time, naming both
    // --- items." Drain's own EnergyDraw declares no PowerSupply term (so Upsert/Open both accept it on its own),
    // --- but Booster's StatModifier targets it with a magnitude that DOES carry one -- the request would inherit
    // --- the dependency the moment the modifier attaches. This is dynamic (StatValidation's static check has no
    // --- entity to resolve the modifier's target against), so it is refused at Activate, mirroring
    // --- StatResolverTests.AModifierThatTargetsItsOwnMagnitudeStatIsRefusedAtEquip's own equip-time pattern. ---
    [Fact]
    public void ActivateRefusesAModifierChainThatWouldCorruptAPowerRequest()
    {
        var request = new PerformanceStat { Min = 10, Max = 10 }; // Drain's own request: no PowerSupply term
        var magnitude = new PerformanceStat { Min = 1, Max = 1, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        var modifier = new StatModifierData
        {
            Stat = new StatReference { Target = nameof(EnergyDrawData), Stat = nameof(EnergyDrawData.EnergyDraw) },
            Modifier = magnitude,
            Type = StatModifierType.Constant
        };

        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10,
            Hardpoints =
            {
                new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = SecondHardpointCell, Shape = new Shape() }
            }
        });
        cache.Upsert(new GearData
        {
            Name = "Drain", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new EnergyDrawData { EnergyDraw = request, PerSecond = true } }
        });
        cache.Upsert(new GearData
        {
            Name = "Booster", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { modifier }
        });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hullItem = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hullItem, new EntitySettings());
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Drain")), HardpointCell));
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Booster")), SecondHardpointCell));
        zone.Entities.Add(ship);

        var error = Assert.Throws<InvalidOperationException>(() => ship.Activate());
        Assert.Contains("Drain", error.Message);   // the item whose request would be corrupted
        Assert.Contains("Booster", error.Message); // the item whose modifier would corrupt it
    }
}
