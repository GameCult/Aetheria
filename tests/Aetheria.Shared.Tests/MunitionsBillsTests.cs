using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// The munitions chain's design bills (aetheria-living-world, cut design-bills-of-materials): tier role names, the four tier parts
// and the role recipes, owned by the MunitionsBills batch and not by Munitions.
public sealed class MunitionsBillsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-bills-" + Guid.NewGuid().ToString("N"));

    public MunitionsBillsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private string ShippedCopy(string name)
    {
        var path = Path.Combine(_root, name);
        File.Copy(TestCatalog.Repo, path, true);
        return path;
    }

    private static string[] Reach(CultCache catalog, ItemData item)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Walk(ItemData good)
        {
            var recipe = Recipes.RecipeOf(catalog, good);
            if (recipe == null) return;
            foreach (var input in recipe.Inputs) Visit(catalog.Get(input.Commodity));
            foreach (var role in Recipes.RoleInputsOf(catalog, recipe))
                foreach (var filler in Recipes.Fillers(catalog, role.Name)) Visit(catalog.Get<ItemData>(filler));
        }
        void Visit(ItemData good)
        {
            if (good != null && seen.Add(catalog.RefOf(good).Key.Value)) Walk(good);
        }
        Walk(item);
        return seen.OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    // Kills: a bill that drops the rare inputs, or a starter whose chain reaches them.
    [Fact]
    public void TheTopBallisticBillReachesTheScarceMetalsAndTheStarterDoesNot()
    {
        using var cache = AetheriaStores.Open(TestCatalog.Repo, registry: TestCatalog.Registry());
        ItemData Gun(string name) => Assert.Single(cache.GetAll<WeaponItemData>(), w => w.Name == name);
        var scarce = new[] { "commodity:niobium", "commodity:rare-earth-oxides", "commodity:platinum-group-metals" };
        Assert.All(scarce, key => Assert.Contains(key, Reach(cache, Gun("Charged Scattergun"))));
        Assert.All(scarce, key => Assert.DoesNotContain(key, Reach(cache, Gun("Light Machine Gun"))));
    }

    // Kills: a rename that misses a stat term, a seller's profile or a bin, or one that does not converge.
    [Fact]
    public void RenamingARoleReachesEveryReaderAndConverges()
    {
        using var cache = AetheriaStores.Open(ShippedCopy("Rename.cc"), catalogWritable: true, registry: TestCatalog.Registry());
        var author = new EconomyAuthor(cache);
        var rifle = Assert.Single(cache.GetAll<WeaponItemData>(), w => w.Name == "Slug Rifle");
        var key = cache.RefOf(rifle).Key;

        IEnumerable<string> TermRoles() => rifle.Behaviors.SelectMany(b => b.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat))
            .SelectMany(f => ((PerformanceStat) f.GetValue(b)).Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => t.Role)));
        var sellers = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(key)).ToArray();
        Assert.Contains(sellers, p => p.Bin != null);
        Assert.True(sellers.Count(p => p.Bin == null) >= 2);
        Assert.Contains("rifled barrel", TermRoles());

        MunitionsBills.RenameRole(author, "Slug Rifle", "rifled barrel", "barrel");
        Assert.DoesNotContain("rifled barrel", TermRoles());
        Assert.Contains("barrel", TermRoles());
        Assert.Contains("barrel", rifle.Roles.Select(r => r.Name));
        Assert.All(sellers.Where(p => p.Bin == null), p => Assert.Contains(p.Roles, r => r.Role == "barrel"));
        Assert.All(sellers.Where(p => p.Bin != null), p => Assert.Empty(p.Roles));

        MunitionsBills.RenameRole(author, "Slug Rifle", "rifled barrel", "barrel");
        MunitionsBills.RenameRole(author, "Slug Rifle", "barrel", "rifled barrel");
        Assert.DoesNotContain("barrel", TermRoles());
        Assert.All(sellers.Where(p => p.Bin == null), p => Assert.Contains(p.Roles, r => r.Role == "rifled barrel"));
    }

    private sealed class DifferentGunRecipe : EconomyBatch
    {
        public override void Author(EconomyAuthor author)
        {
            var gun = author.Catalog.GetAll<WeaponItemData>().Single(w => w.Name == "Autocannon");
            var key = RecipeData.KeyOf(author.Catalog.RefOf(gun).Key);
            var recipe = author.Catalog.Get<RecipeData>(key);
            recipe.Workforce = 99;
            author.Put(recipe, key);
        }
    }

    // Kills: the design-recipe block surviving in Munitions beside the bills.
    [Fact]
    public void MunitionsWritesNoDesignRecipe()
    {
        var path = ShippedCopy("Munitions.cc");
        Assert.Equal(0, EconomyCommand.Author(path, new EconomyBatch[] { new DifferentGunRecipe(), new Munitions() }, true, TextWriter.Null, TestCatalog.Registry(), _root));
        using var cache = AetheriaStores.Open(path, registry: TestCatalog.Registry());
        var gun = Assert.Single(cache.GetAll<WeaponItemData>(), w => w.Name == "Autocannon");
        Assert.Equal(99, cache.Get<RecipeData>(RecipeData.KeyOf(cache.RefOf(gun).Key)).Workforce);
    }
}
