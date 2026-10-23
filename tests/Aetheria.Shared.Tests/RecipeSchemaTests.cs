using System.Linq;
using GameCult.Caching;
using MessagePack;
using Xunit;

// Recipe records and role counts as catalog data (aetheria-living-world, cut recipe-schema-and-checks).
public sealed class RecipeSchemaTests
{
    [Fact]
    public void RecipeIsKeyedByItsOutput()
    {
        using var f = new EconomyFixture();
        var ore = f.Good("ore", lore: "first");
        Assert.Equal("recipe:commodity:ore", RecipeData.KeyOf(new CultRecordKey(ore)).Value);
        Assert.Equal("first", Recipes.RecipeOf(f.Cache, f.Cache.Get<ItemData>(new CultRecordKey(ore))).LoreHook);
        f.Good("ore", lore: "second");
        var recipes = f.Cache.GetAll<RecipeData>().ToArray();
        Assert.Single(recipes);
        Assert.Equal("second", recipes[0].LoreHook);
        Assert.Null(Recipes.RecipeOf(f.Cache, null));
    }

    [Fact]
    public void OldRoleReadsCountOne()
    {
        var options = GameCult.Caching.MessagePack.CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemRole).Assembly);
        var old = MessagePackSerializer.Serialize(new[] { "Lens" });
        var role = MessagePackSerializer.Deserialize<ItemRole>(old, options);
        Assert.Equal("Lens", role.Name);
        Assert.Equal(1, role.Count);
        var counted = MessagePackSerializer.Deserialize<ItemRole>(MessagePackSerializer.Serialize(new ItemRole { Name = "Lens", Count = 4 }, options), options);
        Assert.Equal(4, counted.Count);
    }

    [Fact]
    public void RoleCountSurvivesTheStore()
    {
        using var f = new EconomyFixture();
        f.Gear("laser", new[] { ("Lens", 3), ("Frame", 1) });
        f.Reopen();
        var laser = f.Cache.Get<GearData>(new CultRecordKey("gear:laser"));
        Assert.Equal(new[] { 3, 1 }, laser.Roles.Select(r => r.Count));
    }

    [Fact]
    public void RecipeAndMarketRoundTrip()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("plate", inputs: new[] { ("ore", 2) }, fills: new[] { "Plate", "Cover" }, outputCount: 3, cycle: 12.5f, workforce: 4,
            upkeep: new[] { ("ore", 1) }, lore: "forged in the Mill");
        f.Gate(("commodity:ore", true, false), ("commodity:plate", false, true));
        f.Reopen();
        var recipe = f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:plate")));
        Assert.Equal("commodity:plate", recipe.Output.Key.Value);
        Assert.Equal(3, recipe.OutputCount);
        Assert.Equal(12.5f, recipe.Cycle);
        Assert.Equal(4, recipe.Workforce);
        Assert.Equal(("commodity:ore", 2), (recipe.Inputs.Single().Commodity.Key.Value, recipe.Inputs.Single().Count));
        Assert.Equal(("commodity:ore", 1), (recipe.Upkeep.Single().Commodity.Key.Value, recipe.Upkeep.Single().Count));
        Assert.Equal(f.Mill.Key.Value, recipe.StationKind.Key.Value);
        Assert.Equal("forged in the Mill", recipe.LoreHook);
        Assert.Equal(new[] { "Plate", "Cover" }, recipe.Fills);
        var market = f.Cache.Get<OutsideMarketData>(OutsideMarketData.KeyOf("gate"));
        Assert.Equal("Gate", market.Name);
        Assert.Equal(2, market.Rows.Count);
        var row = market.Rows[1];
        Assert.Equal(("commodity:plate", 10, false, true, 5f), (row.Commodity.Key.Value, row.Price, row.Buys, row.Sells, row.RatePerHour));
        Assert.True(market.Rows[0].Buys);
    }

    [Fact]
    public void RecipesReadDerivedFacts()
    {
        using var f = new EconomyFixture();
        f.Good("zinc", fills: new[] { "Plate" });
        f.Good("alum", fills: new[] { "Plate", "Cover" });
        f.Good("ore");
        f.Good("tin", inputs: new[] { ("ore", 1) });
        f.Good("kettle", roles: new[] { ("Plate", 1) });
        var gear = f.Gear("laser", new[] { ("Plate", 1) });
        Assert.Equal(new[] { "commodity:alum", "commodity:zinc" }, Recipes.Fillers(f.Cache, "Plate").Select(k => k.Value));
        Assert.Empty(Recipes.Fillers(f.Cache, "Nothing"));
        RecipeData Of(string key) => f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey(key)));
        Assert.True(Recipes.IsRaw(f.Cache, Of("commodity:ore")));
        Assert.False(Recipes.IsRaw(f.Cache, Of("commodity:tin")));
        Assert.False(Recipes.IsRaw(f.Cache, Of("commodity:kettle")));
        Assert.True(Recipes.IsRoleRecipe(f.Cache, Of(gear)));
        Assert.False(Recipes.IsRoleRecipe(f.Cache, Of("commodity:tin")));
        Assert.True(Recipes.IsConsumerGood(f.Cache, Of("commodity:kettle")));
        Assert.False(Recipes.IsConsumerGood(f.Cache, Of(gear)));
        Assert.False(Recipes.IsConsumerGood(f.Cache, Of("commodity:tin")));
    }
}
