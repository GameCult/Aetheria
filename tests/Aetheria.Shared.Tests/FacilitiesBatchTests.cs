using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// The Facilities economy batch (aetheria-living-world, cut recipe-facilities): over a copy of the shipped catalog stripped of its
// facilities, it gives every kind a recipe names one facility design, and every recipe its kind's.
public sealed class FacilitiesBatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-facilities-" + Guid.NewGuid().ToString("N"));

    public FacilitiesBatchTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    // The shipped Aetheria.cc on its own (the batch's catalog: no packages), as it stood before the batch: no facility design, no
    // kind's Facilities, no recipe's Facility.
    private CultCache Stripped()
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc");
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        var cache = AetheriaStores.Open(path, catalogWritable: true, registry: TestCatalog.Registry());
        foreach (var recipe in cache.GetAll<RecipeData>().ToArray())
        {
            recipe.Facility = default;
            cache.Upsert(typeof(RecipeData), recipe, cache.RefOf(recipe).Key);
        }
        foreach (var kind in cache.GetAll<StationKindData>().ToArray())
        {
            kind.Facilities.Clear();
            cache.Upsert(typeof(StationKindData), kind, cache.RefOf(kind).Key);
        }
        foreach (var key in cache.GetAll<GearData>().Where(StationKinds.IsRecipeFacility).Select(gear => cache.RefOf(gear).Key).ToArray()) cache.Remove(key);
        foreach (var product in cache.GetAll<FactionProductData>().Where(p => cache.Get(p.Design) == null).ToArray()) cache.Remove(cache.RefOf(product).Key);
        Assert.Empty(cache.GetAll<StationKindData>().SelectMany(kind => kind.Facilities));
        return cache;
    }

    [Fact]
    public void EveryRecipeNamesItsKindsFacility()
    {
        using var cache = Stripped();
        new Facilities().Author(new EconomyAuthor(cache));
        var recipes = cache.GetAll<RecipeData>().ToArray();
        Assert.NotEmpty(recipes);
        foreach (var recipe in recipes)
        {
            var kind = cache.Get(recipe.StationKind);
            var facility = Assert.Single(kind.Facilities);
            Assert.Equal(facility.Key.Value, recipe.Facility.Key.Value);
            Assert.True(StationKinds.IsRecipeFacility(cache.Get(facility)));
        }
        Assert.Equal(recipes.Select(r => r.StationKind.Key.Value).Distinct().Count(), recipes.Select(r => r.Facility.Key.Value).Distinct().Count());
        Assert.DoesNotContain(EconomyChecks.Recipe(cache), fault => fault.Invariant == EconomyChecks.LFacility && fault.Record != "station-facilities");
    }

    [Fact]
    public void RerunConverges()
    {
        using var cache = Stripped();
        new Facilities().Author(new EconomyAuthor(cache));
        var again = new EconomyAuthor(cache);
        new Facilities().Author(again);
        Assert.Empty(again.Touched);
    }

    [Fact]
    public void EachFacilityHasOneUnofferedProduct()
    {
        using var cache = Stripped();
        new Facilities().Author(new EconomyAuthor(cache));
        var facilities = cache.GetAll<StationKindData>().SelectMany(kind => kind.Facilities).Select(facility => facility.Key).ToArray();
        Assert.NotEmpty(facilities);
        foreach (var facility in facilities)
        {
            var product = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(facility));
            Assert.True(product.NotOffered);
            Assert.False(LoadoutGenerator.IsOffered(cache, product));
            Assert.True(cache.Get(product.Manufacturer) != null);
        }
    }

    [Fact]
    public void AKindWithoutARowThrows()
    {
        using var cache = Stripped();
        var recipe = cache.GetAll<RecipeData>().First();
        recipe.StationKind = new CultRecordRef<StationKindData>(StationKindData.KeyOf("shipyard"));
        cache.Upsert(typeof(RecipeData), recipe, cache.RefOf(recipe).Key);
        var refusal = Assert.Throws<InvalidOperationException>(() => new Facilities().Author(new EconomyAuthor(cache)));
        Assert.Contains("station-kind:shipyard", refusal.Message);
    }

    // The munitions batch rewrites its kind whole; the facilities the catalog already holds for it survive, and so does every recipe's.
    [Fact]
    public void MunitionsRewriteKeepsAKindsFacilities()
    {
        var path = Path.Combine(_root, "munitions.cc");
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        using var cache = AetheriaStores.Open(path, catalogWritable: true, registry: TestCatalog.Registry());
        var works = StationKindData.KeyOf("munitions-works");
        var held = Assert.Single(cache.Get<StationKindData>(works).Facilities).Key.Value;
        new Munitions().Author(new EconomyAuthor(cache));
        Assert.Equal(held, Assert.Single(cache.Get<StationKindData>(works).Facilities).Key.Value);
        Assert.DoesNotContain(EconomyChecks.Recipe(cache), fault => fault.Invariant == EconomyChecks.LFacility);
    }
}
