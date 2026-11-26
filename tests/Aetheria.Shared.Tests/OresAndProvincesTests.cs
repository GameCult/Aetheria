using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Batch OresAndProvinces on the shipped catalog (aetheria-living-world, cut ore-batch-apply): every ore a belt can yield is held,
// priced and sold, and MunitionsFeed leaves the recipes the batch owns.
public sealed class OresAndProvincesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-ores-" + Guid.NewGuid().ToString("N"));

    public OresAndProvincesTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private CultCache Shipped()
    {
        var path = Path.Combine(_root, "Aetheria.cc");
        File.Copy(TestCatalog.Repo, path, true);
        return AetheriaStores.Open(path, catalogWritable: true, registry: TestCatalog.Registry());
    }

    // Kills: an ore written without a Shape (ItemData.Shape has no default and a cargo bay reads it to store the good).
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

        var kinds = cache.GetAll<FieldKindData>().ToArray();
        Assert.NotEmpty(kinds);
        var yielded = kinds.SelectMany(kind => kind.Yields).Select(yield => yield.Commodity).GroupBy(commodity => commodity.Key.Value).Select(group => group.First()).ToArray();
        Assert.NotEmpty(yielded);
        foreach (var commodity in yielded)
        {
            var data = cache.Get(commodity);
            Assert.NotNull(data);
            Assert.NotNull(data.Shape);
            Assert.True(data.Price > 0, data.Name);
            Assert.True(data.Mass > 0, data.Name);
            var hold = ship.CargoBays.Single();
            Assert.True(hold.TryStore(new SimpleCommodity { Data = cache.RefOf<ItemData>(data), Quantity = 1 }), data.Name);
            // One ore at a time: the bay has fewer cells than the ores there are.
            foreach (var held in hold.Cargo.Keys.ToArray()) hold.Remove(held);
        }
    }

    // MunitionsFeed finds the ores it takes as inputs by key (Regolith, Nickel-Iron) or by name (Gold): a name lookup refused at head
    // ("0 items are named 'Rock'"). A dry run at the shipped catalog succeeds and changes nothing.
    [Fact]
    public void MunitionsFeedDryRunSucceedsAtHeadAndChangesNothing()
    {
        var path = Path.Combine(_root, "Feed.cc");
        File.Copy(Path.Combine(RestoredHullsTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        var before = File.ReadAllBytes(path);
        var output = new StringWriter();
        Assert.Equal(0, EconomyCommand.Author(path, new List<EconomyBatch> { new MunitionsFeed() }, false, output, TestCatalog.Registry(), _root));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.DoesNotContain("are named", output.ToString());
    }

    // The batch is the one writer of the four recipes MunitionsFeed once wrote (Regolith, Nickel-Iron, Gold, Potassium): applying the feed
    // to the applied catalog leaves each as the batch wrote it. Kills: a surviving Make for any of the four (mining without RoleInputs
    // for Gold and Potassium, the feed's lore for any).
    [Fact]
    public void MunitionsFeedLeavesTheBatchRecipes()
    {
        var path = Path.Combine(_root, "Leaves.cc");
        File.Copy(Path.Combine(RestoredHullsTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        string[] Names = { "Regolith", "Nickel-Iron", "Gold", "Potassium" };

        List<string> Read()
        {
            using var cache = AetheriaStores.Open(path, registry: TestCatalog.Registry());
            return Names.Select(name =>
            {
                var item = cache.GetAll<SimpleCommodityData>().Single(c => c.Name == name);
                var recipe = cache.Get<RecipeData>(RecipeData.KeyOf(cache.RefOf(item).Key));
                Assert.NotNull(recipe);
                return $"{name}|{cache.Get(recipe.StationKind).Name}|inputs {recipe.Inputs.Count}|roles {string.Join(",", recipe.RoleInputs.Select(r => r.Name + "x" + r.Count))}|fills {string.Join(",", recipe.Fills)}|{recipe.LoreHook}";
            }).ToList();
        }

        var batch = Read();
        Assert.Contains(batch, line => line.StartsWith("Regolith|Mining Station|inputs 0|roles |"));
        Assert.Contains(batch, line => line.StartsWith("Nickel-Iron|Mining Station|inputs 0|roles |"));
        Assert.Contains(batch, line => line.StartsWith("Gold|Processing Station|inputs 0|roles gold orex"));
        Assert.Contains(batch, line => line.StartsWith("Potassium|Processing Station|inputs 0|roles potassium orex"));

        Assert.Equal(0, EconomyCommand.Author(path, new List<EconomyBatch> { new MunitionsFeed() }, true, new StringWriter(), TestCatalog.Registry(), _root));
        Assert.Equal(batch, Read());
    }
}
