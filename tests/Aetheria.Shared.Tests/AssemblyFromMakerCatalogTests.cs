using System;
using System.Linq;
using System.Reflection;
using MessagePack;
using Xunit;

// The shipped catalog carries each maker's seeded assembly, and the Faction format pins its wire positions
// (aetheria-release, cut assembly-from-maker).
public sealed class AssemblyFromMakerCatalogTests
{
    // Every maker of a roled product carries an Assembly the command seeded; no product overrides its maker's.
    [Fact]
    public void ShippedCatalogCarriesAssembly()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var makers = cache.GetAll<FactionProductData>().Where(p => p.Roles.Count > 0).Select(p => p.Manufacturer.Key).Distinct().ToArray();
        Assert.Equal(15, makers.Length);
        foreach (var key in makers)
        {
            var assembly = cache.Get<Faction>(key).Assembly;
            Assert.NotNull(assembly);
            Assert.InRange(assembly.Mean, .01f, 1f);
        }

        Assert.All(cache.GetAll<FactionProductData>(), p => Assert.Null(p.Assembly));
    }

    // 16 is reserved for Relations, 17 is Doctrine, 18 is reserved for Livery, 20 for the place-names pass.
    [Fact]
    public void AssemblyRidesItsWireKeys()
    {
        int KeyOf(Type type) => type.GetField("Assembly").GetCustomAttribute<KeyAttribute>().IntKey.Value;
        Assert.Equal(19, KeyOf(typeof(Faction)));
        Assert.Equal(6, KeyOf(typeof(FactionProductData)));
    }
}
