using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// Operator ask (2026-09-19, docs/... none yet -- this is Cut 8 on top of Cut 7's brownout work): "you can't
// currently tell when [a thruster is] busted -- the particle system it spawns responds only to thrust intent.
// Should multiply that by actual performance." EquippedItem.ConditionRatio (Entity.cs) is the one number that
// answers "how healthy does this item actually look": the item's resolved value for a stat against what the
// same item, same lot, same modifiers would produce with Heat/Durability/PowerSupply pinned to their identity.
// Thruster.Condition and AetherDrive.Condition (Thruster.cs/AetherDrive.cs) are that ratio read against each
// behaviour's own governing stat (Thrust, Torque); ShipInstance.cs (Unity-side, not covered here) multiplies
// presentation intent by it instead of recomputing any durability/heat/power arithmetic of its own.
// Each rule here has a matching mutation in tests/mutation_tests_condition_ratio_cut8.py.
public sealed class ConditionRatioTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-conditionratio-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public ConditionRatioTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // Optimal temperature 280, plateau 400 wide (matches BrownoutTests' own consumer shape): the ambient
    // temperature this fixture starts at (280, set explicitly per test rather than relied on as a default) sits
    // dead center of the plateau, so ThermalPerformance reads exactly 1 there.
    private const float OptimalTemperature = 280f;

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheria-conditionratio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    // Hull, a Reactor (BrownoutTests' own shape: no capacitor, so a tick's grant ratio is a clean
    // min(1, generation/demand)), and one Consumer carrying the given ThrusterData. Defaults to this instance's
    // own catalog path; a test that needs two independent ships (the power-grant comparison below) passes a
    // second, freshly rooted path rather than reopening the same file twice in one process.
    private CultCache OpenCatalog(ThrusterData thruster, string catalogPath = null)
    {
        var cache = AetheriaStores.Open(catalogPath ?? Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = OptimalTemperature, PlateauWidth = 400,
            Behaviors = { new ReactorData
            {
                Charge = Constant(0), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2)
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Consumer", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = OptimalTemperature, PlateauWidth = 400,
            Behaviors = { thruster }
        });
        cache.FlushAsync().Wait();
        return cache;
    }

    private Ship BuildShip(CultCache cache, float reactorCharge, out EquippedItem consumer)
    {
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);
        Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Consumer"))));

        zone.Entities.Add(ship);
        ship.LookDirection = float3(0, 0, 1); // Ship.Update's own steering pass stays a no-op, as BrownoutTests
        ship.Activate();
        consumer = ship.Equipment.Single(e => e.Data.Name == "Consumer");

        // Settle at the plateau's center: two ticks so UpdatePerformance's deltaTemp term reads 0 before a test
        // drives its own change, matching HeatResponseTests' own settle pattern.
        foreach (var cell in consumer.InsetShape.Coordinates) ship.Temperature[cell.x, cell.y] = OptimalTemperature;
        consumer.UpdatePerformance();
        consumer.UpdatePerformance();
        return ship;
    }

    // --- 1: a perfect item -- full durability, a grant far exceeding demand -- reads exactly 1, on a stat that
    // --- declares Durability and PowerSupply together (both at their identity). The Heat term's own identity
    // --- case is pinned by ConditionFallsWithTemperatureAwayFromThePlateauAlone's "atPlateau" reading below;
    // --- combining it here too would make this test depend on UpdateTemperature's own ambient drift over the
    // --- Update tick PowerSupply needs, which is a real dependency this test has no reason to take on. ---
    [Fact]
    public void APerfectItemsConditionIsOne()
    {
        var thrust = new PerformanceStat
        {
            Min = 0, Max = 100,
            Terms =
            {
                new StatTerm { Source = StatSource.Durability, Exponent = 1 },
                new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 }
            }
        };
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 1000, out var consumer);
        ship.Update(1f);

        Assert.Equal(1f, ship.GetBehavior<Thruster>().Condition, 4);
    }

    // --- 2: durability alone. Full temperature/power, half durability, a stat with only a Durability term. ---
    [Fact]
    public void ConditionFallsWithDurabilityAlone()
    {
        var thrust = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.Durability, Exponent = 1 } } };
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 1000, out var consumer);

        consumer.EquippableItem.Durability = consumer.Data.Durability; // full
        consumer.UpdatePerformance(); // direct call, not ship.Update -- no reason to also churn UpdateTemperature
        var full = ship.GetBehavior<Thruster>().Condition;

        consumer.EquippableItem.Durability = consumer.Data.Durability * .5f; // half
        consumer.UpdatePerformance();
        var half = ship.GetBehavior<Thruster>().Condition;

        Assert.Equal(1f, full, 4);
        Assert.True(half > 0f && half < full, $"half={half} should sit strictly between 0 and full={full}");
    }

    // --- 3: temperature alone, away from the plateau. Full durability/power, a stat with only a Heat term. ---
    [Fact]
    public void ConditionFallsWithTemperatureAwayFromThePlateauAlone()
    {
        var thrust = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.Heat, Exponent = 1 } } };
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 1000, out var consumer);
        var atPlateau = ship.GetBehavior<Thruster>().Condition; // BuildShip already settled at OptimalTemperature

        // Push the item's own temperature far outside the plateau (center 280, half-width 200 -> outside past
        // 480), then read performance directly (not through ship.Update, whose UpdateTemperature would pull the
        // value we just set back toward ambient equilibrium within the same tick, before UpdatePerformance ever
        // sees it -- exactly HeatResponseTests' own SetTemperature-then-UpdatePerformance pattern).
        foreach (var cell in consumer.InsetShape.Coordinates) ship.Temperature[cell.x, cell.y] = 900f;
        consumer.UpdatePerformance();
        var offPlateau = ship.GetBehavior<Thruster>().Condition;

        Assert.Equal(1f, atPlateau, 4);
        Assert.True(offPlateau > 0f && offPlateau < atPlateau, $"offPlateau={offPlateau} should sit strictly between 0 and atPlateau={atPlateau}");
    }

    // --- 4: power alone. Full durability/temperature, a partial grant, a stat with only a PowerSupply term
    // --- (mirrors BrownoutTests' own Curved fixture). ---
    [Fact]
    public void ConditionFallsWithAPartialPowerGrantAlone()
    {
        var thrust = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };

        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var fullShip = BuildShip(cache, reactorCharge: 1000, out _); // demand 100, generation far exceeds it -> ratio 1
        fullShip.MovementDirection = float2(0, -1); // real thrust demand -- PowerRequest is 0 with no input at all
        fullShip.Update(1f);
        var full = fullShip.GetBehavior<Thruster>().Condition;

        var secondRoot = NewRoot();
        try
        {
            using var cache2 = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) }, catalogPath: Path.Combine(secondRoot, "Aetheria.cc"));
            var halfShip = BuildShip(cache2, reactorCharge: 50, out _); // demand 100, generation 50 -> ratio .5
            halfShip.MovementDirection = float2(0, -1);
            halfShip.Update(1f);
            var half = halfShip.GetBehavior<Thruster>().Condition;

            Assert.Equal(1f, full, 4);
            Assert.True(half > 0f && half < full, $"half={half} should sit strictly between 0 and full={full}");
        }
        finally { Directory.Delete(secondRoot, true); }
    }

    // --- 5: dead ends at 0, not some small positive residue -- a fully destroyed item on a stat with Min=0
    // --- produces nothing, so the ratio itself is 0/0-guarded down to 0 rather than a divide-by-zero or NaN. ---
    [Fact]
    public void ConditionIsZeroWhenTheItemProducesNothing()
    {
        var thrust = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.Durability, Exponent = 1 } } };
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 1000, out var consumer);

        consumer.EquippableItem.Durability = 0f;
        ship.Update(1f);

        Assert.Equal(0f, ship.GetBehavior<Thruster>().Condition, 4);
    }

    // --- 5b: the guard's real reason to exist -- a stat authored with Max=0 (no terms at all) makes the nominal
    // --- side degenerate to exactly 0 too, so actual/nominal is 0/0. Without the guard that is NaN, which is not
    // --- equal to 0f (the ConditionIsZeroWhenTheItemProducesNothing case above never hits this: its nominal
    // --- stays 100 -- only the numerator goes to 0 there). ---
    [Fact]
    public void ConditionIsZeroNotNaNWhenTheNominalValueItselfDegeneratesToZero()
    {
        var thrust = Constant(0); // Min == Max == 0, no Terms -- both actual and nominal evaluate to exactly 0
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 1000, out _);
        ship.Update(1f);

        Assert.Equal(0f, ship.GetBehavior<Thruster>().Condition, 4);
    }

    // --- 6: presentation reading Condition must not have moved what the thruster actually does. Exact numbers
    // --- from BrownoutTests.ThrusterAtFullGrantIsUnchangedFromToday and ...DegradesByItsAuthoredExponentNotLinearly,
    // --- reproduced here against a ship that also reads Condition every tick, to prove Execute()'s own force
    // --- math is untouched by this cut. ---
    [Fact]
    public void ThrustForceForAGivenInputIsUnchangedByThisCut()
    {
        var thrust = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 1000, out _); // demand 100, generation far exceeds it -> ratio 1
        ship.MovementDirection = float2(0, -1); // reverse-thruster axis += -MovementDirection.y == 1
        ship.Update(1f);

        // Reading Condition after Update (as presentation would every frame) must not perturb the result.
        _ = ship.GetBehavior<Thruster>().Condition;

        Assert.Equal(.1f, -ship.Velocity.y, 3); // 100 thrust / 1000 mass * dt 1, exactly as BrownoutTests pins it
    }

    [Fact]
    public void ThrustForceDegradesByItsAuthoredExponentUnchangedByThisCut()
    {
        var thrust = new PerformanceStat { Min = 0, Max = 100, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 2 } } };
        using var cache = OpenCatalog(new ThrusterData { Thrust = thrust, Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100) });
        var ship = BuildShip(cache, reactorCharge: 50, out _); // demand 100, generation 50 -> ratio .5
        ship.MovementDirection = float2(0, -1);
        ship.Update(1f);

        _ = ship.GetBehavior<Thruster>().Condition;

        // Thrust = lerp(0, 100, pow(.5, 2)) = 25; velocity delta = 25 / 1000 mass * dt 1 = .025.
        Assert.Equal(.025f, -ship.Velocity.y, 3);
    }
}
