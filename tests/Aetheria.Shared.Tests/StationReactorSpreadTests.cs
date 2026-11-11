using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// station-reactor-spread (aetheria-release follow-up scenarios-batch4-minors): a station reactor's product keeps the
// quality spread of the reactor product it was scaled from. The mint reads a product's Roles (Mean, StandardDeviation) per
// design role, and a role missing or left at the unauthored default draws a flat .5 / .15 instead of the maker's spread.
public sealed class StationReactorSpreadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-station-spread-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public StationReactorSpreadTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    // Read the way the mint does (ItemManager.Mint): the product's own Roles, matched by role name.
    [Fact]
    public void ShippedStationReactorProductsCarryTheirSourcesSpread()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        FactionProductData Find(string name, string maker) =>
            products.Single(p => p.Name == name && cache.Get(p.Manufacturer)?.ShortName == maker);

        foreach (var (stationName, sourceName, maker) in StationReactorSpreadCommand.Rows)
        {
            var station = Find(stationName, maker);
            var source = Find(sourceName, maker);
            Assert.NotEmpty(station.Roles);
            foreach (var role in station.Roles)
            {
                var from = source.Roles.Single(r => r.Role == role.Role);
                Assert.Equal(from.Mean, role.Mean);
                Assert.Equal(from.StandardDeviation, role.StandardDeviation);
            }
            Assert.Contains(station.Roles, r => r.Mean != .5f || r.StandardDeviation != .15f);
        }
    }

    private void Seed()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var maker = cache.Upsert(new Faction { Name = "Maker", ShortName = "M" });
        var gear = cache.Upsert(new GearData { Name = "Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10 });
        var design = new CultRecordRef<CraftedItemData>(gear.Key);
        cache.Upsert(new FactionProductData
        {
            Name = "Source", Design = design, Manufacturer = maker,
            Roles = { new ProductRole { Role = "core", Mean = .7f, StandardDeviation = .05f }, new ProductRole { Role = "regulator", Mean = .3f, StandardDeviation = .2f } }
        });
        cache.Upsert(new FactionProductData
        {
            Name = "Source Station", Design = design, Manufacturer = maker,
            Roles = { new ProductRole { Role = "core" }, new ProductRole { Role = "regulator", Mean = .9f, StandardDeviation = .01f } }
        });
        cache.FlushAsync().Wait();
    }

    private static readonly (string Station, string Source, string Maker)[] Fixture = { ("Source Station", "Source", "M") };

    private string Run(bool apply)
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: apply);
        var output = new StringWriter();
        Assert.Equal(0, StationReactorSpreadCommand.Run(cache, apply, output, Fixture));
        cache.FlushAsync().Wait();
        return output.ToString();
    }

    private ProductRole RoleOf(string role)
    {
        using var cache = AetheriaStores.Open(Catalog);
        return cache.GetAll<FactionProductData>().Single(p => p.Name == "Source Station").Roles.Single(r => r.Role == role);
    }

    // A role at the unauthored default takes the source's pair, a role tuned in Studio is kept, a dry run writes nothing,
    // and a second apply has nothing to do.
    [Fact]
    public void CopiesOnlyUnauthoredRolesAndReplaysToNothing()
    {
        Seed();
        var before = File.ReadAllBytes(Catalog);
        Assert.Contains("core", Run(apply: false));
        Assert.Equal(before, File.ReadAllBytes(Catalog));

        Run(apply: true);
        Assert.Equal(.7f, RoleOf("core").Mean, 3);
        Assert.Equal(.05f, RoleOf("core").StandardDeviation, 3);
        Assert.Equal(.9f, RoleOf("regulator").Mean, 3);
        Assert.Equal(.01f, RoleOf("regulator").StandardDeviation, 3);

        var written = File.ReadAllBytes(Catalog);
        Assert.Contains("No changes", Run(apply: true));
        Assert.Equal(written, File.ReadAllBytes(Catalog));
    }
}
