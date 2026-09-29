using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Cut 1 (docs/stats-and-power-cut.md), R-heat: the authored heat-response shape (minimum, maximum, optimum,
// plateau width), its load-time validation, and the wear-lever consequence (the plateau is where Wear's thermal
// term goes to zero). Each rule here has a matching mutation in tests/mutation_tests_stats_power_cut1.py.
public sealed class HeatResponseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-heatresponse-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 HardpointCell = new int2(0, 0);

    public HeatResponseTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    // A hull plus one thermal gear design, equipped and settled at the given start temperature (two
    // UpdatePerformance calls so deltaTemp reads 0 before the test drives its own change).
    private EquippedItem BuildEquippedAt(CultCache cache, ItemManager items, float startTemperature, out Ship ship)
    {
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        var hullData = cache.GetByName<HullData>("Skiff");
        var thermalData = cache.GetByName<GearData>("Thermal");
        var hullItem = (EquippableItem) items.CreateInstance(items.CreateLot(hullData, maker, .5f));
        ship = new Ship(items, null, hullItem, new EntitySettings());
        var gearItem = (EquippableItem) items.CreateInstance(items.CreateLot(thermalData, maker, .5f));
        Assert.True(ship.TryEquip(gearItem, HardpointCell));
        var equipped = ship.Equipment.Single(e => e.Data is GearData);
        foreach (var cell in equipped.InsetShape.Coordinates) ship.Temperature[cell.x, cell.y] = startTemperature;
        equipped.UpdatePerformance();
        equipped.UpdatePerformance(); // settle: oldTemperature now equals startTemperature, deltaTemp reads 0
        return equipped;
    }

    private void SetTemperature(EquippedItem equipped, Ship ship, float temperature)
    {
        foreach (var cell in equipped.InsetShape.Coordinates) ship.Temperature[cell.x, cell.y] = temperature;
    }

    private CultCache OpenCatalogWithThermalGear()
    {
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Price = 100,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
        });
        // Plateau 40-60 of a 0-100 range: full performance across it, linear falloff to the bounds outside it.
        cache.Upsert(new GearData
        {
            Name = "Thermal", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Durability = 100, MinimumTemperature = 0, MaximumTemperature = 100,
            OptimalTemperature = 50, PlateauWidth = 20
        });
        cache.FlushAsync().Wait();
        return cache;
    }

    // --- R-heat shape: Performance() is 1 across the plateau and falls linearly to 0 at the bounds ---

    [Fact]
    public void PerformanceIsFullAcrossThePlateau()
    {
        using var cache = OpenCatalogWithThermalGear();
        var data = cache.GetByName<GearData>("Thermal");
        Assert.Equal(1f, data.Performance(40), 5);
        Assert.Equal(1f, data.Performance(50), 5);
        Assert.Equal(1f, data.Performance(60), 5);
    }

    [Fact]
    public void PerformanceFallsLinearlyOutsideThePlateau()
    {
        using var cache = OpenCatalogWithThermalGear();
        var data = cache.GetByName<GearData>("Thermal");
        // Halfway from the plateau's low edge (40) down to the minimum (0) -> halfway performance.
        Assert.Equal(.5f, data.Performance(20), 5);
        Assert.Equal(0f, data.Performance(0), 5);
        Assert.Equal(0f, data.Performance(100), 5);
    }

    // S7 (docs/stats-and-power-cut.md Cut 1 findings): a design is dead at and beyond its bounds -- that is the
    // point of R-heat's ruling, not an accident. A mutation restoring the old curve's "1 at any temperature"
    // behaviour outside the bounds must fail this, at the boundary itself and strictly beyond it on both sides.
    [Fact]
    public void PerformanceIsZeroAtAndBeyondEachBound()
    {
        using var cache = OpenCatalogWithThermalGear();
        var data = cache.GetByName<GearData>("Thermal");
        Assert.Equal(0f, data.Performance(0), 5);
        Assert.Equal(0f, data.Performance(-10), 5);
        Assert.Equal(0f, data.Performance(100), 5);
        Assert.Equal(0f, data.Performance(150), 5);
    }

    // --- Validation: fails loudly at catalog load AND at every write through CultRecordRefs.Upsert (S6: the
    // writable path used to trust Open()'s one-time check and could write an invalid catalog that only failed
    // the next time somebody reopened it). Each test below proves the earlier refusal at Upsert time, then
    // confirms the record never reached disk by reopening read-only and finding it absent. ---

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheria-heatresponse-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void UpsertRefusesAnOptimumOutsideItsBounds()
    {
        var root = NewRoot();
        try
        {
            var catalog = Path.Combine(root, "Aetheria.cc");
            using var cache = AetheriaStores.Open(catalog, catalogWritable: true);
            cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            var ex = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
            {
                Name = "BadOptimum", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1,
                MinimumTemperature = 0, MaximumTemperature = 100, OptimalTemperature = 150, PlateauWidth = 0
            }));
            Assert.Contains("BadOptimum", ex.Message);
            cache.FlushAsync().Wait();
            Assert.Null(cache.GetAll<GearData>().SingleOrDefault(g => g.Name == "BadOptimum"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void UpsertRefusesANegativePlateauWidth()
    {
        var root = NewRoot();
        try
        {
            var catalog = Path.Combine(root, "Aetheria.cc");
            using var cache = AetheriaStores.Open(catalog, catalogWritable: true);
            cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            var ex = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
            {
                Name = "BadPlateau", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1,
                MinimumTemperature = 0, MaximumTemperature = 100, OptimalTemperature = 50, PlateauWidth = -1
            }));
            Assert.Contains("BadPlateau", ex.Message);
        }
        finally { Directory.Delete(root, true); }
    }

    // S5/S6: a zero-span range (Tractor Beam's exact bug) is refused, not silently accepted as an authored
    // immunity.
    [Fact]
    public void UpsertRefusesAZeroSpanRange()
    {
        var root = NewRoot();
        try
        {
            var catalog = Path.Combine(root, "Aetheria.cc");
            using var cache = AetheriaStores.Open(catalog, catalogWritable: true);
            cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            var ex = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
            {
                Name = "ZeroSpan", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1,
                MinimumTemperature = 50, MaximumTemperature = 50, OptimalTemperature = 50, PlateauWidth = 0
            }));
            Assert.Contains("ZeroSpan", ex.Message);
        }
        finally { Directory.Delete(root, true); }
    }

    // S5/S6: NaN in any of the four heat-response fields is refused.
    [Theory]
    [InlineData(float.NaN, 100f, 50f, 20f)]
    [InlineData(0f, float.NaN, 50f, 20f)]
    [InlineData(0f, 100f, float.NaN, 20f)]
    [InlineData(0f, 100f, 50f, float.NaN)]
    public void UpsertRefusesNaNInAnyHeatResponseField(float min, float max, float optimum, float plateau)
    {
        var root = NewRoot();
        try
        {
            var catalog = Path.Combine(root, "Aetheria.cc");
            using var cache = AetheriaStores.Open(catalog, catalogWritable: true);
            cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            var ex = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
            {
                Name = "NaNHeat", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1,
                MinimumTemperature = min, MaximumTemperature = max, OptimalTemperature = optimum, PlateauWidth = plateau
            }));
            Assert.Contains("NaNHeat", ex.Message);
        }
        finally { Directory.Delete(root, true); }
    }

    // S5/S6: the plateau clamps to the bounds rather than poking past them -- an authored half-width that would
    // reach past Minimum or Maximum is refused at write time, naming the item, rather than silently clamped.
    [Theory]
    [InlineData(0f, 100f, 10f, 40f)]  // low edge: 10 - 20 = -10 < 0
    [InlineData(0f, 100f, 90f, 40f)]  // high edge: 90 + 20 = 110 > 100
    public void UpsertRefusesAPlateauThatPokesPastItsBounds(float min, float max, float optimum, float plateau)
    {
        var root = NewRoot();
        try
        {
            var catalog = Path.Combine(root, "Aetheria.cc");
            using var cache = AetheriaStores.Open(catalog, catalogWritable: true);
            cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            var ex = Assert.Throws<InvalidOperationException>(() => cache.Upsert(new GearData
            {
                Name = "PokingPlateau", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1,
                MinimumTemperature = min, MaximumTemperature = max, OptimalTemperature = optimum, PlateauWidth = plateau
            }));
            Assert.Contains("PokingPlateau", ex.Message);
        }
        finally { Directory.Delete(root, true); }
    }

    // --- The wear lever (Entity.cs UpdatePerformance): inside the plateau the thermal term is zero ---

    [Fact]
    public void HeldInsidePlateauTakesNoThermalWear()
    {
        using var cache = OpenCatalogWithThermalGear();
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var equipped = BuildEquippedAt(cache, items, 50f, out var ship);
        Assert.Equal(1f, equipped.ThermalPerformance, 5);
        Assert.Equal(0f, equipped.Wear, 5); // no thermal term (Performance == 1) and no deltaTemp (settled)
    }

    [Fact]
    public void HeldOutsidePlateauTakesThermalWear()
    {
        using var cache = OpenCatalogWithThermalGear();
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var equipped = BuildEquippedAt(cache, items, 10f, out var ship); // outside 40-60, inside 0-100
        Assert.True(equipped.ThermalPerformance < 1f);
        Assert.True(equipped.Wear > 0f);
    }

    [Fact]
    public void AFastSwingAcrossThePlateauStillWears()
    {
        using var cache = OpenCatalogWithThermalGear();
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var equipped = BuildEquippedAt(cache, items, 10f, out var ship);
        // Jump straight into the plateau in one tick: ThermalPerformance ends at 1 (no thermal term), but the
        // swing itself (deltaTemp) still costs.
        SetTemperature(equipped, ship, 50f);
        equipped.UpdatePerformance();
        Assert.Equal(1f, equipped.ThermalPerformance, 5);
        Assert.True(equipped.Wear > 0f);
    }
}
