using System;
using System.Linq;
using System.Reflection;
using MessagePack;
using Xunit;

// The shipped catalog carries each maker's derived assembly, and the Faction format pins its wire positions
// (aetheria-release, cut assembly-from-maker).
public sealed class AssemblyFromMakerCatalogTests
{
    [Fact]
    public void ShippedCatalogCarriesDerivation()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().Where(p => p.Roles.Count > 0).ToArray();
        var makers = products.Select(p => p.Manufacturer.Key).Distinct().ToArray();
        Assert.NotEmpty(makers);
        foreach (var key in makers)
        {
            var roles = products.Where(p => p.Manufacturer.Key.Equals(key)).SelectMany(p => p.Roles).ToArray();
            var faction = cache.Get<Faction>(key);
            Assert.Equal(Math.Round(roles.Average(r => r.Mean), 2), faction.AssemblyMean, 2);
            Assert.Equal(Math.Round(roles.Average(r => r.StandardDeviation), 2), faction.AssemblyDeviation, 2);
        }
    }

    // 16 is reserved for Relations, 17 is Doctrine, 18 is reserved for Livery.
    [Fact]
    public void AssemblyFieldsSitAtKeys19And20()
    {
        int KeyOf(string field) => typeof(Faction).GetField(field).GetCustomAttribute<KeyAttribute>().IntKey.Value;
        Assert.Equal(19, KeyOf(nameof(Faction.AssemblyMean)));
        Assert.Equal(20, KeyOf(nameof(Faction.AssemblyDeviation)));
    }
}
