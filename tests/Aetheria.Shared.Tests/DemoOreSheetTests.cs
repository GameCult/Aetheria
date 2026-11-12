using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// The interim ore sheet (rulings mining-demo-ore-minimal-yields-now, mining-demo-ore-prices-gear-scale): what the shipped catalog's
// Asteroid belt holds can be carried and sold, so mine, sell, repair closes on the existing stations.
public sealed class DemoOreSheetTests : IDisposable
{
    private static readonly string[] Ores = { "Rock", "New Metals", "Gold" };
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-demoore-" + Guid.NewGuid().ToString("N"));

    public DemoOreSheetTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private CultCache Shipped()
    {
        var path = Path.Combine(_root, "Aetheria.cc");
        File.Copy(TestCatalog.Repo, path, true);
        return AetheriaStores.Open(path, catalogWritable: true, registry: TestCatalog.Registry());
    }

    private static CultRecordRef<FieldKindData> Asteroid(CultCache cache) =>
        cache.RefOf(cache.GetAll<FieldKindData>().Single(k => k.Name == "Asteroid"));

    [Fact]
    public void AsteroidYieldsThreeOresInKeyOrder()
    {
        using var cache = Shipped();
        var yield = FieldKinds.Yield(Asteroid(cache), default, cache);
        var amounts = yield.ToDictionary(y => cache.Get(y.Commodity).Name, y => y.Amount);
        Assert.Equal(Ores.OrderBy(n => n).ToArray(), amounts.Keys.OrderBy(n => n).ToArray());
        Assert.Equal(6f, amounts["Rock"]);
        Assert.Equal(3f, amounts["New Metals"]);
        Assert.Equal(1f, amounts["Gold"]);
        Assert.Equal(yield.Select(y => y.Commodity.Key.Value).OrderBy(k => k, StringComparer.Ordinal), yield.Select(y => y.Commodity.Key.Value));
    }

    [Fact]
    public void EveryYieldedOreCanBeHeldAndSold()
    {
        using var cache = Shipped();
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var bay = new Shape(4, 4);
        foreach (var cell in bay.AllCoordinates) bay[cell] = true;
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(new HullData { Name = "OreSkiff", HullType = HullType.Ship, Shape = shape, Price = 1000, Durability = 10, Armor = 10 });
        cache.Upsert(new CargoBayData { Name = "OreBay", Shape = new Shape(), InteriorShape = bay, Price = 20, Durability = 4 });
        cache.FlushAsync().GetAwaiter().GetResult();
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), AuthoredSettings.Load(RestoredHullsTests.FindRepoRoot()).Read<GameplaySettings>("GameplaySettings"), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        EquippableItem Mint(string design)
        {
            var data = cache.GetByName<EquippableItemData>(design);
            return new EquippableItem
            {
                Data = cache.RefOf<ItemData>(data), Durability = data.Durability,
                Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(data), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
            };
        }
        var ship = new Ship(items, zone, Mint("OreSkiff"), new EntitySettings());
        Assert.True(ship.TryEquip(Mint("OreBay"), new int2(1, 1)));

        foreach (var (commodity, _) in FieldKinds.Yield(Asteroid(cache), default, cache))
        {
            var data = cache.Get(commodity);
            Assert.NotNull(data.Shape);
            Assert.True(data.Price > 0, data.Name);
            Assert.True(data.Mass > 0, data.Name);
            Assert.True(ship.CargoBays.Single().TryStore(new SimpleCommodity { Data = cache.RefOf<ItemData>(data), Quantity = 1 }), data.Name);
        }
    }

    [Fact]
    public void TheSheetConverges()
    {
        var path = Path.Combine(_root, "Converge.cc");
        File.Copy(Path.Combine(RestoredHullsTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        var before = File.ReadAllBytes(path);
        var output = new StringWriter();
        Assert.Equal(0, EconomyCommand.Author(path, new List<EconomyBatch> { new DemoOre() }, true, output, TestCatalog.Registry(), _root));
        Assert.Equal(before, File.ReadAllBytes(path));

        using var cache = AetheriaStores.Open(path, registry: TestCatalog.Registry());
        var ids = cache.GetAll<FieldKindData>().Single(k => k.Name == "Asteroid").Yields.Select(y => y.Id).ToArray();
        Assert.Equal(new[] { "a57e0c000001", "a57e0c000002", "a57e0c000003" }, ids);
    }
}
