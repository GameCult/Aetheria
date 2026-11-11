using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Ruling ballistics-real-ammo, read from the shipped catalog: every gun that fires a round names the commodity it draws, and every
// such commodity (and the chain behind it) is produced from extraction, not from nothing. A round with no source is a magazine
// nobody can ever refill.
public sealed class MunitionsSourcingCatalogTests
{
    // The guns that fire real rounds: ballistic and launcher hardpoints, and the mine layer (a launcher in all but hardpoint).
    private static bool FiresRounds(WeaponItemData item, WeaponData data) =>
        item.HardpointType == HardpointType.Ballistic || item.HardpointType == HardpointType.Launcher || data is MineLayerData;

    // Kills: a ballistic gun (or the Light Machine Gun's free hose) authored with AmmoType unset, which reloads for nothing; a
    // drawn commodity that is not a commodity record; a magazine too small for a reload to mean anything.
    [Fact]
    public void EveryRoundFiringGunNamesAnAmmunitionCommodityAndHoldsARealMagazine()
    {
        using var db = AetherDb.Open();
        var guns = db.Cache.GetAll<WeaponItemData>()
            .SelectMany(i => i.Behaviors.OfType<WeaponData>().Where(d => FiresRounds(i, d)).Select(d => (i.Name, d))).ToArray();
        Assert.True(guns.Length >= 14, $"fixture: the catalog holds its round-firing guns ({guns.Length})");
        foreach (var (name, data) in guns)
        {
            Assert.True(data.AmmoType.IsSet(), $"{name} draws no ammunition");
            Assert.NotNull(db.Cache.Get<SimpleCommodityData>(data.AmmoType.Key));
            Assert.True(data.MagazineSize >= 1, $"{name} has no magazine");
        }
        Assert.Equal(new[] { "Ammo", "Mines", "Missiles" }, guns.Select(g => db.Cache.Get<SimpleCommodityData>(g.d.AmmoType.Key).Name).Distinct().OrderBy(n => n).ToArray());
    }

    // Kills: an ammunition commodity no chain of recipes reaches raw goods for (the Ammo, Missiles and Mines leaves had no recipe at all).
    [Fact]
    public void EveryDrawnAmmunitionHasAFiniteProductionDepth()
    {
        using var db = AetherDb.Open();
        var depth = EconomyChecks.Deep(db.Cache);
        var drawn = db.Cache.GetAll<WeaponItemData>().SelectMany(i => i.Behaviors.OfType<WeaponData>()).Where(d => d.AmmoType.IsSet())
            .Select(d => d.AmmoType.Key.Value).Distinct().ToArray();
        Assert.Equal(3, drawn.Length);
        foreach (var key in drawn)
            Assert.True(depth[key] != null, $"{db.Cache.Get<ItemData>(new CultRecordKey(key)).Name} has no finite production depth");
    }

    // Kills: a leaf of the munitions chain left without a source or a sink (the 19 L-source-sink faults this chain shipped with).
    [Fact]
    public void TheMunitionsChainHasNoSourceSinkFault()
    {
        using var db = AetherDb.Open();
        var chain = new HashSet<string>(ChainOf(db.Cache), System.StringComparer.Ordinal);
        Assert.True(chain.Count >= 25, $"fixture: the chain is walked ({chain.Count} goods)");
        var faults = EconomyChecks.SourceSink(db.Cache).Where(f => chain.Contains(KeyOfRecipe(f.Record))).ToArray();
        Assert.True(faults.Length == 0, string.Join("\n", faults.Select(f => f.Message)));
    }

    private static string KeyOfRecipe(string recipeKey) => recipeKey.StartsWith("recipe:") ? recipeKey.Substring("recipe:".Length) : recipeKey;

    // Every good that feeds a drawn round, or fills a role of a round-firing gun, through the recipe graph; the rounds included.
    private static IEnumerable<string> ChainOf(CultCache catalog)
    {
        var recipes = catalog.GetAll<RecipeData>().ToDictionary(r => r.Output.Key.Value);
        var open = new Stack<string>(catalog.GetAll<WeaponItemData>().SelectMany(i => i.Behaviors.OfType<WeaponData>())
            .Where(d => d.AmmoType.IsSet()).Select(d => d.AmmoType.Key.Value).Distinct());
        foreach (var item in catalog.GetAll<WeaponItemData>().Where(i => i.Behaviors.OfType<WeaponData>().Any(d => FiresRounds(i, d))))
        {
            var recipe = Recipes.RecipeOf(catalog, item);
            if (recipe == null) continue;
            foreach (var role in Recipes.RoleInputsOf(catalog, recipe))
                foreach (var filler in Recipes.Fillers(catalog, role.Name)) open.Push(filler.Value);
        }
        var seen = new HashSet<string>(open);
        while (open.Count > 0)
            if (recipes.TryGetValue(open.Pop(), out var recipe))
                foreach (var input in recipe.Inputs.Select(c => c.Commodity.Key.Value))
                    if (seen.Add(input)) open.Push(input);
        return seen;
    }
}
