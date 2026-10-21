using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// The weapon product lines the shipped catalog holds (campaign aetheria-release, cut product-lines-weapons; rulings
// catalog-grows-generic-designs-branded-products, maker-design-tiers-allowed): a sold weapon design has several brands, a
// brand name is one product's, and every row of the command's table is in the catalog with the quality its maker's profile gives.
// The rows come from the command's own table and the profiles from its own maker-profile table, so a row edited in one place
// and not shipped fails here.
public sealed class ProductLinesCatalogTests
{
    // A design with any product is sold; the Smoke test designs have none and stay exempt.
    [Fact]
    public void EveryWeaponDesignWithAProductHasTwoMakers()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        var unsold = new List<string>();
        foreach (var design in cache.GetAll<WeaponItemData>())
        {
            var key = cache.RefOf(design).Key;
            var makers = products.Where(p => p.Design.Key.Equals(key)).Select(p => p.Manufacturer.Key).Distinct().Count();
            if (makers == 0)
            {
                unsold.Add(design.Name);
                continue;
            }
            Assert.True(makers >= 2, $"the weapon design \"{design.Name}\" is sold by {makers} maker");
        }
        // The designs no product sells are the Smoke test designs and no others: a design that lost all its products is a failure.
        Assert.All(unsold, name => Assert.StartsWith("Smoke ", name));
    }

    [Fact]
    public void NoTwoProductsShareAName()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var shared = cache.GetAll<FactionProductData>()
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.Empty(shared);
    }

    // Ruling gear-without-presence: a sold ship gear design with roles has two makers. The six reactor designs named here are
    // station gear or Faction Play's NiteLife-only quirk, and keep their one maker.
    private static readonly string[] OneMakerGear =
    {
        "Muon Fusion Reactor", "Muon Fusion Station Reactor", "Station Reactor", "Heavy Station Reactor",
        "High-Yield Station Reactor", "Ring Station Reactor",
    };

    [Fact]
    public void EveryRoleBearingGearDesignWithAProductHasTwoMakers()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        var gear = cache.GetAll<GearData>().Where(d => d is not WeaponItemData && (d.Roles?.Count ?? 0) > 0).ToArray();
        Assert.All(OneMakerGear, name => Assert.Contains(gear, d => d.Name == name));
        var sold = 0;
        foreach (var design in gear.Where(d => !OneMakerGear.Contains(d.Name)))
        {
            var key = cache.RefOf(design).Key;
            var makers = products.Where(p => p.Design.Key.Equals(key)).Select(p => p.Manufacturer.Key).Distinct().Count();
            if (makers == 0) continue;
            sold++;
            Assert.True(makers >= 2, $"the gear design \"{design.Name}\" is sold by {makers} maker");
        }
        Assert.True(sold >= 20, "the gear designs with products should be many");
    }

    [Theory]
    [InlineData("weapons", 20)]
    [InlineData("gear", 22)]
    [InlineData("minor-powers", 10)]
    public void LineRowsAreShipped(string line, int count)
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var table = ProductLinesCommand.Table(line);
        Assert.Equal(count, table.Count);
        foreach (var row in table)
        {
            var product = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Name == row.Name);
            var design = cache.Get(product.Design);
            Assert.Equal(row.Design, design.Name);
            Assert.Equal(row.Maker, cache.Get(product.Manufacturer).ShortName);
            Assert.Equal(row.Description, product.Description);
            var (mean, dev) = Program.MakerProfiles[row.Maker];
            Assert.Equal(
                (design.Roles ?? new()).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal),
                product.Roles.Select(r => r.Role).OrderBy(n => n, StringComparer.Ordinal));
            foreach (var role in product.Roles)
            {
                Assert.Equal(mean, role.Mean, 4);
                Assert.Equal(dev, role.StandardDeviation, 4);
            }
        }
    }
    // Cut product-tiers: the six bins of the command's tier table are in the catalog, each a single product that names its line.
    [Fact]
    public void TierRowsAreShipped()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var table = ProductLinesCommand.TierTable();
        Assert.Equal(6, table.Count);
        foreach (var row in table)
        {
            var bin = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Name == row.Name);
            var line = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Name == row.Line);
            Assert.Equal(cache.RefOf(line).Key, bin.Bin.Line.Key);
            Assert.Equal(line.Design.Key, bin.Design.Key);
            Assert.Equal(line.Manufacturer.Key, bin.Manufacturer.Key);
            Assert.Equal(row.Floor, bin.Bin.Floor);
            Assert.Equal(row.Description, bin.Description);
            Assert.Empty(bin.Roles);
            Assert.Null(bin.Assembly);
        }
    }

    // A bin bins an unbinned line of its own design and maker, and the catalog holds no bin the table does not author.
    [Fact]
    public void BinsBinAnUnbinnedLineOfTheirOwnDesignAndMaker()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var bins = cache.GetAll<FactionProductData>().Where(p => p.Bin != null).ToArray();
        Assert.Equal(ProductLinesCommand.TierTable().Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal), bins.Select(b => b.Name).OrderBy(n => n, StringComparer.Ordinal));
        foreach (var bin in bins)
        {
            var line = cache.Get(bin.Bin.Line);
            Assert.NotNull(line);
            Assert.Null(line.Bin);
            Assert.Equal(line.Design.Key, bin.Design.Key);
            Assert.Equal(line.Manufacturer.Key, bin.Manufacturer.Key);
            Assert.True(bin.Bin.Floor >= 0 && float.IsFinite(bin.Bin.Floor));
        }
    }

    // FactionProductData slots 7 and 8 are additive under the same schema id: a bin and NotOffered round-trip, a record written
    // before slot 8 existed (an array one element shorter) is on the market, and one written before slot 7 also reads no bin.
    [Fact]
    public void BinIsSlotSevenAndACatalogWithoutItReadsNoBin()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(FactionProductData).Assembly);
        var binned = MessagePackSerializer.Deserialize<FactionProductData>(
            MessagePackSerializer.Serialize(new FactionProductData { Name = "Binned", Bin = new ProductBin { Floor = .67f } }, options), options);
        Assert.Equal(.67f, binned.Bin.Floor);

        Assert.True(MessagePackSerializer.Deserialize<FactionProductData>(
            MessagePackSerializer.Serialize(new FactionProductData { Name = "Unsold", NotOffered = true }, options), options).NotOffered);

        var bytes = MessagePackSerializer.Serialize(new FactionProductData { Name = "Old", Description = "d" }, options);
        Assert.Equal(0x99, bytes[0]);
        Assert.Equal(0xC0, bytes[^2]);
        Assert.Equal(0xC2, bytes[^1]);
        var beforeNotOffered = new byte[] { 0x98 }.Concat(bytes.Skip(1).Take(bytes.Length - 2)).ToArray();
        var offered = MessagePackSerializer.Deserialize<FactionProductData>(beforeNotOffered, options);
        Assert.Equal("Old", offered.Name);
        Assert.False(offered.NotOffered);
        var before = new byte[] { 0x97 }.Concat(bytes.Skip(1).Take(bytes.Length - 3)).ToArray();
        var old = MessagePackSerializer.Deserialize<FactionProductData>(before, options);
        Assert.Equal("Old", old.Name);
        Assert.Null(old.Bin);
        Assert.False(old.NotOffered);
    }
}
