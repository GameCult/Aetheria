using System.Linq;
using GameCult.Caching;
using Xunit;

// A station kind's description is what a player and a planner read of what it does; the recipes at the kind are what it does.
public sealed class StationKindDescriptionTests
{
    // Every good the shipped catalog extracts at a station kind (a commodity recipe with no inputs; a gear recipe is a design on offer, not raw) is named in that kind's description.
    // Kills: a kind that gains an extraction recipe (Diamond at mining, Ammonia at volatiles) while its description still lists the old set.
    [Fact]
    public void EveryRawGoodAKindExtractsIsNamedInItsDescription()
    {
        using var db = AetherDb.Open();
        var cache = db.Cache;
        var unnamed = cache.GetAll<RecipeData>().Where(recipe => recipe.Inputs.Count == 0 && recipe.StationKind.IsSet())
            .Select(recipe => (Kind: cache.Get(recipe.StationKind), Output: cache.Get(recipe.Output)))
            .Where(pair => pair.Kind != null && (pair.Output is SimpleCommodityData || pair.Output is CompoundCommodityData) && !pair.Kind.Description.Contains(pair.Output.Name))
            .Select(pair => $"{pair.Kind.Name} extracts {pair.Output.Name}, which its description omits")
            .Distinct().OrderBy(line => line).ToList();
        Assert.True(unnamed.Count == 0, string.Join("\n", unnamed));
    }

    // The two kinds the rule was cut for, by name, so the rule cannot pass by the catalog having lost the recipes.
    [Fact]
    public void MiningExtractsDiamondAndVolatilesAmmonia()
    {
        using var db = AetherDb.Open();
        var cache = db.Cache;
        string Extracted(string kindId) => string.Join(",", cache.GetAll<RecipeData>()
            .Where(recipe => recipe.Inputs.Count == 0 && recipe.StationKind.IsSet() && recipe.StationKind.Key.Equals(StationKindData.KeyOf(kindId)))
            .Select(recipe => cache.Get(recipe.Output).Name));
        Assert.Contains("Diamond", Extracted("mining"));
        Assert.Contains("Ammonia", Extracted("volatiles"));
        Assert.Contains("Diamond", cache.Get<StationKindData>(StationKindData.KeyOf("mining")).Description);
        Assert.Contains("Ammonia", cache.Get<StationKindData>(StationKindData.KeyOf("volatiles")).Description);
    }
}
