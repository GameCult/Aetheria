using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
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
}
