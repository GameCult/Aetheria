using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// A commodity holds a Shape (aetheria-living-world, cut commodity-shapes): one cell by the type default, refused as an L-shape fault
// when a stored commodity has none, and held by a cargo bay in the shipped catalog.
public sealed class CommodityShapeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-commodityshape-" + Guid.NewGuid().ToString("N"));

    public CommodityShapeTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private sealed class Lambda : EconomyBatch
    {
        private readonly Action<EconomyAuthor> _author;
        public Lambda(Action<EconomyAuthor> author) => _author = author;
        public override void Author(EconomyAuthor author) => _author(author);
    }

    // Kills: the default dropped from either commodity type.
    [Fact]
    public void ANewCommodityIsOneCell()
    {
        Assert.Single(new SimpleCommodityData().Shape.Coordinates);
        Assert.Single(new CompoundCommodityData().Shape.Coordinates);
    }

    // Kills: a check that reads only simple commodities, or one that treats a restored shape as a fault.
    [Fact]
    public void AShapelessCommodityIsAnLShapeFault()
    {
        using var f = EconomyFixture.Healthy();
        Assert.Empty(EconomyChecks.Shapes(f.Cache));
        var key = new CultRecordKey(EconomyFixture.Key("plate"));
        var plate = f.Cache.Get<CompoundCommodityData>(key);
        plate.Shape = null;
        f.Cache.Upsert(typeof(CompoundCommodityData), plate, key);
        var fault = Assert.Single(EconomyChecks.Shapes(f.Cache));
        Assert.Equal(EconomyChecks.LShape, fault.Invariant);
        Assert.Equal(key.Value, fault.Record);
        Assert.Single(EconomyChecks.Check(f.Cache).Faults, x => x.Invariant == EconomyChecks.LShape);
        plate.Shape = new Shape();
        f.Cache.Upsert(typeof(CompoundCommodityData), plate, key);
        Assert.Empty(EconomyChecks.Shapes(f.Cache));
    }

    // Kills: LShape missing from the refusing set in EconomyCommand.
    [Fact]
    public void ABatchThatWritesAShapelessCommodityIsRefused()
    {
        using var f = EconomyFixture.Healthy();
        f.Cache.FlushAsync().GetAwaiter().GetResult();
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var output = new StringWriter();
        var batch = new Lambda(a => a.Put(new SimpleCommodityData { Name = "void", Shape = null }, new CultRecordKey(EconomyFixture.Key("void"))));
        var code = EconomyCommand.Author(f.CatalogPath, new List<EconomyBatch> { batch }, true, output, TestCatalog.Registry(), _root);
        Assert.Equal(1, code);
        Assert.Contains("The batch is refused", output.ToString());
        Assert.Contains("L-shape commodity:void", output.ToString());
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    private CultCache Shipped()
    {
        var path = Path.Combine(_root, "Aetheria.cc");
        File.Copy(TestCatalog.Repo, path, true);
        return AetheriaStores.Open(path, catalogWritable: true, registry: TestCatalog.Registry());
    }

    // Kills: the data pass skipping compound or raw goods.
    [Fact]
    public void EveryShippedCommodityHasAShape()
    {
        using var cache = Shipped();
        var commodities = cache.GetAll<ItemData>().Where(Recipes.IsCommodity).ToArray();
        Assert.True(commodities.Length > 100);
        Assert.All(commodities, item => Assert.NotEmpty(item.Shape?.Coordinates ?? Array.Empty<int2>()));
        Assert.Empty(EconomyChecks.Shapes(cache));
    }

    // Kills: a zero-cell shape and the null crash on the way into a hold.
    [Fact]
    public void EveryShippedSimpleCommodityFitsAnEmptyHold()
    {
        using var cache = Shipped();
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var bay = new Shape(4, 4);
        foreach (var cell in bay.AllCoordinates) bay[cell] = true;
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(new HullData { Name = "ShapeSkiff", HullType = HullType.Ship, Shape = shape, Price = 1000, Durability = 10, Armor = 10 });
        cache.Upsert(new CargoBayData { Name = "ShapeBay", Shape = new Shape(), InteriorShape = bay, Price = 20, Durability = 4 });
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
        var ship = new Ship(items, zone, Mint("ShapeSkiff"), new EntitySettings());
        Assert.True(ship.TryEquip(Mint("ShapeBay"), new int2(1, 1)));
        var simple = cache.GetAll<SimpleCommodityData>().ToArray();
        Assert.NotEmpty(simple);
        foreach (var data in simple)
        {
            var hold = ship.CargoBays.Single();
            Assert.True(hold.TryStore(new SimpleCommodity { Data = cache.RefOf<ItemData>(data), Quantity = 1 }), data.Name);
            foreach (var held in hold.Cargo.Keys.ToArray()) hold.Remove(held);
        }
    }
}
