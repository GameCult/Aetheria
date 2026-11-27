using System.Linq;
using GameCult.Caching;
using Xunit;

// The shields the catalog sells (campaign aetheria-release, cut hardpoint-fit-1; rulings hardpoint-sizes-are-footprint-palette,
// catalog-grows-generic-designs-branded-products): a light 1x1 and a heavy 2x2 projector, each with a product a maker offers, and
// the fit they give the mounts the game boots. Prices and stats are starting knobs, so none is pinned here.
public sealed class ShieldCatalogTests
{
    private static GearData Design(CultCache cache, string name) => Assert.Single(cache.GetAll<GearData>(), g => g.Name == name);

    private static FactionProductData[] Offered(CultCache cache, GearData design) =>
        cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(cache.RefOf(design).Key) && !p.NotOffered).ToArray();

    [Theory]
    [InlineData(ShieldCatalog.Light, 1)]
    [InlineData(ShieldCatalog.Heavy, 2)]
    public void EachProjectorIsAShieldOfItsFootprintWithAnOfferedProduct(string name, int size)
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var design = Design(cache, name);
        Assert.Equal(HardpointType.Shield, design.Hardpoint);
        Assert.Equal((size, size), (design.Shape.Width, design.Shape.Height));
        Assert.Equal(size * size, design.Shape.Coordinates.Length);
        var shield = Assert.Single(design.Behaviors.OfType<ShieldData>());
        Assert.True(shield.Capacity.Min > 0f && shield.Capacity.Max > 0f, "a shield with no capacity breaks on its first hit");
        Assert.NotEmpty(Offered(cache, design));
    }

    [Fact]
    public void MegiddoSellsTheHeavyProjector()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        Assert.Contains(Offered(cache, Design(cache, ShieldCatalog.Heavy)), p => cache.Get(p.Manufacturer).ShortName == "Megiddo");
    }

    // Loose fit: the light projector serves a 1x1 shield mount, the heavy one fills every 2x2 shield mount of the booted hulls.
    [Fact]
    public void TheProjectorsFitTheMountsTheGameBoots()
    {
        using var cache = RestoredHullsTests.OpenCatalogWithFixtures();
        var light = Design(cache, ShieldCatalog.Light);
        var heavy = Design(cache, ShieldCatalog.Heavy);
        var mounts = cache.GetAll<HullData>().SelectMany(h => h.Hardpoints).Where(m => m.Type == HardpointType.Shield).ToArray();
        Assert.Contains(mounts, m => m.Shape.Coordinates.Length == 1);
        Assert.Contains(mounts, m => m.Shape.Coordinates.Length == 4);
        Assert.All(mounts, m => Assert.True(m.Takes(light) || m.Takes(heavy)));
        Assert.All(mounts.Where(m => m.Shape.Coordinates.Length == 1), m => Assert.True(m.Takes(light) && !m.Takes(heavy)));
        Assert.All(mounts.Where(m => m.Shape.Coordinates.Length == 4), m => Assert.True(m.IsFilledBy(heavy) && m.Takes(light)));
    }
}
