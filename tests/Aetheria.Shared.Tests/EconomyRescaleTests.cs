using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Newtonsoft.Json;
using Xunit;

// The one-time price transform (aetheria-living-world, cut gear-money-rescale), on a fixture game folder. Deleted with the command.
public sealed class EconomyRescaleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-rescale-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "GameData", "Aetheria.cc");
    private string Package => Path.Combine(_root, "GameData", "Mods", "aetheria.wren", "ship.cc");

    public void Dispose() => Directory.Delete(_root, true);

    // A catalog with a commodity, a gun, a hull, a cargo bay and a consumable at prices that exercise rounding, and the Wren package (6000).
    private void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Package));
        Directory.CreateDirectory(Path.GetDirectoryName(Catalog));
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "tests", "fixtures", "mods", "aetheria.wren", "ship.cc"), Package);
        using var f = EconomyFixture.Healthy();
        f.EditItem<SimpleCommodityData>("commodity:ore", ore => ore.Price = 777);
        f.EditItem<GearData>("gear:laser", gear => gear.Price = 12345);
        f.EditItem<HullData>("hull:skiff", hull => hull.Price = 250);
        f.Cache.Upsert(typeof(CargoBayData), new CargoBayData { Name = "Bay", Shape = new Shape(), InteriorShape = new Shape(), Price = 30 }, new CultRecordKey("bay:small"));
        f.Cache.Upsert(typeof(ConsumableItemData), new ConsumableItemData { Name = "Patch", Shape = new Shape(), Price = 18000 }, new CultRecordKey("consumable:patch"));
        f.Cache.FlushAsync().GetAwaiter().GetResult();
        f.Cache.Dispose();
        File.Copy(f.CatalogPath, Catalog);
    }

    private (int Price, string Json) Item<T>(string key) where T : ItemData
    {
        using var cache = AetheriaStores.Open(Catalog, registry: TestCatalog.Registry());
        var item = cache.Get<T>(new CultRecordKey(key));
        return (item.Price, JsonConvert.SerializeObject(item));
    }

    [Fact]
    public void GearConsumablesAndPackagesScaleAndCommoditiesDoNot()
    {
        Build();
        var catalog = File.ReadAllBytes(Catalog);
        var package = File.ReadAllBytes(Package);
        var output = new StringWriter();

        Assert.Equal(0, EconomyRescale.Apply(_root, 100, null, false, output));
        Assert.Equal(catalog, File.ReadAllBytes(Catalog));
        Assert.Equal(package, File.ReadAllBytes(Package));
        Assert.Contains("gear:laser", output.ToString());

        var ore = Item<SimpleCommodityData>("commodity:ore");
        Assert.Equal(0, EconomyRescale.Apply(_root, 100, null, true, new StringWriter()));
        Assert.Equal(123, Item<GearData>("gear:laser").Price);          // 123.45
        Assert.Equal(3, Item<HullData>("hull:skiff").Price);            // 2.5 rounds away from zero
        Assert.Equal(1, Item<CargoBayData>("bay:small").Price);         // 0.3 floors at 1
        Assert.Equal(180, Item<ConsumableItemData>("consumable:patch").Price);
        Assert.Equal(ore, Item<SimpleCommodityData>("commodity:ore"));  // untouched, field for field
        Assert.Equal(60, ShipAuthoringStore.Load(Package).Hull.Price);
    }
}
