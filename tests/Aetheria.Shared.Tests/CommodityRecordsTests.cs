using System;
using System.Linq;
using System.Reflection;
using MessagePack;
using Xunit;

// Commodity records carry no stored classification and no demand weights (aetheria-living-world, cut
// catalog-subtraction): stage, component-ness and consumer-ness are derived later from recipes, roles and needs.
public sealed class CommodityRecordsTests
{
    // The shipped bytes still hold values at the retired keys; MessagePack skips them and every record loads by name.
    [Fact]
    public void ShippedCatalogOpensWithTheRetiredKeysGone()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        Assert.Equal(22, cache.GetAll<SimpleCommodityData>().Count());
        Assert.Equal(63, cache.GetAll<CompoundCommodityData>().Count());
        Assert.Contains(cache.GetAll<SimpleCommodityData>(), c => c.Name == "Ammo");
        Assert.All(cache.GetAll<CompoundCommodityData>(), c => Assert.False(string.IsNullOrEmpty(c.Name)));
    }

    // Keys 10 and 11 are retired: no commodity member may reuse them for a different meaning.
    [Fact]
    public void RetiredKeysStayRetired()
    {
        int[] KeysOf(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(f => f.GetCustomAttribute<KeyAttribute>()?.IntKey).Where(k => k.HasValue).Select(k => k.Value).ToArray();
        Assert.Equal(new[] { 9 }, KeysOf(typeof(SimpleCommodityData)));
        Assert.Empty(KeysOf(typeof(CompoundCommodityData)));
        Assert.Null(typeof(SimpleCommodityData).GetField("Category"));
        Assert.Null(typeof(CompoundCommodityData).GetField("Category"));
        Assert.Null(typeof(CompoundCommodityData).GetField("DemandProfile"));
    }
}
