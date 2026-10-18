using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// The Inspection Scanner the catalog sells (campaign aetheria-release, cut scanner-catalog; rulings designs-are-tech-nodes,
// catalog-breadth-roles-and-components, sensor-stat-set, scan-held-beam, miss-terris-belongs-to-emily-r3), read from the shipped
// catalog and fitted to a shipped hull: what the design says, who sells it, and that a lot's roles move the figures they are
// authored on. The scanner's behaviour belongs to ScannerTests; this file owns the catalog's figures.
public sealed class InspectionScannerCatalogTests
{
    internal const string DesignName = "Inspection Scanner";
    private const string ScanArray = "scan array";
    private const string Discipline = "emission-discipline";

    private static GearData Design(CultCache cache) => Assert.Single(cache.GetAll<GearData>(), g => g.Name == DesignName);

    private static void AssertStat(PerformanceStat stat, float min, float max, string role, float exponent)
    {
        Assert.Equal(min, stat.Min, 4);
        Assert.Equal(max, stat.Max, 4);
        var term = Assert.Single(stat.Terms);
        Assert.Equal(StatSource.Quality, term.Source);
        Assert.Equal(role, term.Role);
        Assert.Equal(exponent, term.Exponent, 4);
    }

    // roles-backfill's map for the design (CB-R1): each figure reads the role it is authored on, so a lot's roles move the
    // figures the catalog test below reads. Dropping the entry or mapping a figure to the wrong role fails here.
    [Fact]
    public void RolesBackfillMapsTheInspectionScannersFiguresToTheirRoles()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var map = Program.RoleMapOf(Design(cache));
        Assert.True(map != null, "roles-backfill finds no role map for the Inspection Scanner design");
        Assert.Equal(
            new[] { ("InterceptFactor", Discipline), ("Range", ScanArray), ("ScanTime", ScanArray) },
            map.Select(p => (p.Key, p.Value)).OrderBy(p => p.Key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void TheCatalogSellsAnInspectionScanner()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var design = Design(cache);
        var computer = Assert.Single(cache.GetAll<GearData>(), g => g.Name == "Targeting Computer");

        Assert.Equal(HardpointType.Tool, design.Hardpoint);
        Assert.Equal((1, 1), (design.Shape.Width, design.Shape.Height));
        Assert.True(design.Shape.Cells[0, 0], "the one cell is solid");
        Assert.False(string.IsNullOrWhiteSpace(design.Description));
        Assert.Equal(new[] { ScanArray, Discipline }, design.Roles.Select(r => r.Name).ToArray());
        // The thermal constants and temperatures are the Targeting Computer's, the catalog's 1x1 tool.
        Assert.Equal(
            (computer.SpecificHeat, computer.Conductivity, computer.MinimumTemperature, computer.MaximumTemperature, computer.OptimalTemperature, computer.PlateauWidth, computer.ThermalResilience),
            (design.SpecificHeat, design.Conductivity, design.MinimumTemperature, design.MaximumTemperature, design.OptimalTemperature, design.PlateauWidth, design.ThermalResilience));
        CultRecordRefs.Validate(design);

        // One behaviour, one linear term per figure on the scan array, the sensor curve on emission discipline.
        var data = Assert.IsType<ScannerData>(Assert.Single(design.Behaviors));
        AssertStat(data.Range, 350f, 550f, ScanArray, 1f);
        AssertStat(data.ScanTime, 10f, 6f, ScanArray, 1f);
        AssertStat(data.InterceptFactor, .5f, 2f, Discipline, MathF.Log2(3f));

        // Three products from three makers, none Miss Terri's or the Pirates', each authoring both roles; the voice is in the
        // emission-discipline mean: quiet Finch, middling AU, loud Lucent.
        var key = cache.RefOf(design).Key;
        var products = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(key)).ToArray();
        Assert.Equal(new[] { "AU", "Finch", "Lucent" }, products.Select(p => cache.Get(p.Manufacturer).ShortName).OrderBy(m => m, StringComparer.Ordinal).ToArray());
        Assert.Equal(3, products.Select(p => p.Name).Distinct().Count());
        var expected = new Dictionary<string, (float ScanArray, float Discipline, float Dev)>
        {
            ["Finch"] = (.65f, .8f, .20f), ["AU"] = (.5f, .5f, .12f), ["Lucent"] = (.6f, .2f, .15f),
        };
        Assert.All(products, product =>
        {
            var (scan, discipline, dev) = expected[cache.Get(product.Manufacturer).ShortName];
            Assert.Equal(new[] { Discipline, ScanArray }, product.Roles.Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray());
            Assert.Equal(scan, product.Roles.Single(r => r.Role == ScanArray).Mean, 3);
            Assert.Equal(discipline, product.Roles.Single(r => r.Role == Discipline).Mean, 3);
            Assert.All(product.Roles, r => Assert.Equal(dev, r.StandardDeviation, 3));
            Assert.False(string.IsNullOrWhiteSpace(product.Description));
        });
    }

    // The shipped hull with the scanner fitted before the ship goes live, from a lot minted at the product's own role means
    // (the mean is what the product's distribution is centred on; the roll's noise is not under test).
    private sealed class Rig : IDisposable
    {
        public readonly CultCache Cache = RestoredHullsTests.OpenCatalog();
        public readonly EquippedItem Scanner;

        public Rig(string maker)
        {
            var design = Design(Cache);
            var key = Cache.RefOf(design).Key;
            var product = Cache.GetAll<FactionProductData>().Single(p => p.Design.Key.Equals(key) && Cache.Get(p.Manufacturer).ShortName == maker);
            var hull = Cache.GetByName<HullData>("Longinus");
            var settings = RestoredHullsTests.Settings();
            var ledger = new ProvenanceLedger();
            ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = .5f };
            ledger.Lots[2] = new Lot
            {
                Origin = new Attributed(), Quality = 1f,
                Roles = product.Roles.Select(r => new RoleFill { Role = r.Role, Quality = r.Mean }).ToList(),
            };
            var items = new ItemManager(Cache, ledger, settings, _ => { });
            var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
            var ship = new Ship(items, zone, new EquippableItem { Data = Cache.RefOf<ItemData>(hull), Durability = hull.Durability, Lot = 1 }, settings.DefaultEntitySettings);
            Assert.True(ship.TryEquip(new EquippableItem { Data = Cache.RefOf<ItemData>(design), Durability = design.Durability, Lot = 2 }));
            zone.Admit(ship, piloted: false);
            zone.Update(0f);
            Scanner = ship.Equipment.Single(e => e.Data.Name == DesignName);
        }

        public void Dispose() => Cache.Dispose();

        public ScannerData Data => Assert.IsType<ScannerData>(Assert.Single(Scanner.Data.Behaviors));
        public float Read(Func<ScannerData, PerformanceStat> stat) => Scanner.Evaluate(stat(Data));
    }

    // Each figure reads the lot's quality for the role it is authored on, linearly (Range, ScanTime) or on the sensor curve
    // (InterceptFactor): the Finch lot (scan array .65, discipline .8) reads 480 and 7.4 s and an alpha of 1.5534, the Lucent lot
    // (.6, .2) 470, 7.6 s and .6168. A stat reading the wrong role, the wrong end or another curve lands elsewhere.
    [Theory]
    [InlineData("Finch", 480f, 7.4f, 1.5534f)]
    [InlineData("AU", 450f, 8f, 1f)]
    [InlineData("Lucent", 470f, 7.6f, .6168f)]
    public void AnInspectionScannersFiguresReadItsLotsRoles(string maker, float range, float scanTime, float alpha)
    {
        using var rig = new Rig(maker);

        Assert.Equal(range, rig.Read(d => d.Range), 2);
        Assert.Equal(scanTime, rig.Read(d => d.ScanTime), 2);
        Assert.Equal(alpha, rig.Read(d => d.InterceptFactor), 3);
    }

    [Fact]
    public void AQuietMakersLotIsHeardLessThanALoudMakersAtTheSameRange()
    {
        using var finch = new Rig("Finch");
        using var lucent = new Rig("Lucent");

        Assert.True(finch.Read(d => d.InterceptFactor) > lucent.Read(d => d.InterceptFactor), "alpha is how far it sees over how far it is heard: quiet is high");
    }
}
