using System;
using System.Linq;
using GameCult.Caching;
using Xunit;

// A commodity's recipe takes roles as well as named commodities (aetheria-living-world, cut recipe-role-inputs): RecipeData.RoleInputs,
// the one reader of role slots (Recipes.RoleInputsOf), and the checks reading a role input as any commodity that fills it.
public sealed class RecipeRoleInputTests
{
    private static string Iron => EconomyFixture.Key("iron");

    // hematite and ilmenite are raw ores filling iron-ore (ilmenite also titanium-ore); iron takes 2 iron-ore.
    private static EconomyFixture Smelter()
    {
        var f = new EconomyFixture();
        f.Good("hematite", fills: new[] { "iron-ore" });
        f.Good("ilmenite", fills: new[] { "iron-ore", "titanium-ore" });
        f.Good("iron", roleInputs: new[] { ("iron-ore", 2) }, fills: new[] { "Plate" });
        f.Gear("laser", new[] { ("Plate", 2) });
        return f;
    }

    private static string[] Records(System.Collections.Generic.IEnumerable<EconomyFault> faults, string invariant) =>
        faults.Where(x => x.Invariant == invariant).Select(x => x.Record).ToArray();

    [Fact]
    public void ShippedRecipesReadAnEmptyRoleInputList()
    {
        using var cache = AetheriaStores.Open(TestCatalog.Repo, registry: TestCatalog.Registry());
        var recipes = cache.GetAll<RecipeData>().ToArray();
        Assert.NotEmpty(recipes);
        Assert.All(recipes, r => { Assert.NotNull(r.RoleInputs); Assert.Empty(r.RoleInputs); });
    }

    [Fact]
    public void ARoleInputSurvivesReopen()
    {
        using var f = Smelter();
        f.Reopen();
        var role = Assert.Single(f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey(Iron))).RoleInputs);
        Assert.Equal(("iron-ore", 2), (role.Name, role.Count));
    }

    [Fact]
    public void RoleInputsOfReadsTheDesignOrTheRecipe()
    {
        using var f = Smelter();
        var laser = Assert.Single(Recipes.RoleInputsOf(f.Cache, f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("gear:laser")))));
        Assert.Equal(("Plate", 2), (laser.Name, laser.Count));
        var ironRecipe = f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey(Iron)));
        var ore = Assert.Single(Recipes.RoleInputsOf(f.Cache, ironRecipe));
        Assert.Equal(("iron-ore", 2), (ore.Name, ore.Count));
        Assert.False(Recipes.IsRaw(f.Cache, ironRecipe));
        Assert.True(Recipes.IsRaw(f.Cache, f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey(EconomyFixture.Key("hematite"))))));
        Assert.False(Recipes.IsConsumerGood(f.Cache, ironRecipe));
    }

    [Fact]
    public void ANullListElsewhereInARecipeLeavesItsRoleInputsToTheRules()
    {
        using var f = Smelter();
        Assert.Empty(EconomyChecks.Check(f.Cache).Faults.Where(x => x.Invariant != EconomyChecks.LRecipe));
        f.EditRecipe(Iron, r => r.Upkeep = null);
        Assert.Empty(EconomyChecks.Check(f.Cache).Faults.Where(x => x.Invariant != EconomyChecks.LRecipe));
    }

    [Fact]
    public void DepthReadsARoleInputAsItsShallowestFiller()
    {
        using var f = new EconomyFixture();
        f.Good("hematite", fills: new[] { "iron-ore" });
        f.Good("grit");
        f.Good("pulp", inputs: new[] { ("grit", 1) });
        f.Good("sludge", inputs: new[] { ("pulp", 1) }, fills: new[] { "iron-ore" });
        f.Good("iron", roleInputs: new[] { ("iron-ore", 1) });
        Assert.Equal(1, EconomyChecks.Deep(f.Cache)[Iron]);
        f.EditRecipe(EconomyFixture.Key("hematite"), r => r.Fills.Clear());
        Assert.Equal(3, EconomyChecks.Deep(f.Cache)[Iron]);
        f.EditRecipe(EconomyFixture.Key("sludge"), r => r.Fills.Clear());
        Assert.Null(EconomyChecks.Deep(f.Cache)[Iron]);
    }

    [Fact]
    public void SourceComesThroughAnyFillerOfARoleInput()
    {
        using var f = new EconomyFixture();
        f.Good("hematite", fills: new[] { "iron-ore" });
        f.Good("loopa", inputs: new[] { ("loopb", 1) }, fills: new[] { "iron-ore" });
        f.Good("loopb", inputs: new[] { ("loopa", 1) });
        f.Good("iron", roleInputs: new[] { ("iron-ore", 1) });
        bool NoSource() => EconomyChecks.SourceSink(f.Cache).Any(x => x.Record == "recipe:" + Iron && x.Message.Contains("no source"));
        Assert.False(NoSource());
        f.EditRecipe(EconomyFixture.Key("hematite"), r => r.Fills.Clear());
        Assert.True(NoSource());
    }

    [Fact]
    public void AGoodFillingOnlyARoleInputHasASink()
    {
        using var f = Smelter();
        f.Good("titanium", roleInputs: new[] { ("titanium-ore", 1) });
        bool NoSink() => EconomyChecks.SourceSink(f.Cache).Any(x => x.Record == "recipe:" + EconomyFixture.Key("ilmenite") && x.Message.Contains("no sink"));
        Assert.False(NoSink());
        f.EditRecipe(Iron, r => r.RoleInputs.Clear());
        f.EditRecipe(EconomyFixture.Key("titanium"), r => r.RoleInputs.Clear());
        Assert.True(NoSink());
    }

    [Fact]
    public void ProductiveReadsTheWorstFillerOfARoleInput()
    {
        using var f = new EconomyFixture();
        f.Good("hematite", fills: new[] { "iron-ore" });
        f.Good("iron", roleInputs: new[] { ("iron-ore", 2) });
        Assert.Empty(EconomyChecks.Productive(f.Cache));
        f.Good("scrap", inputs: new[] { ("iron", 2) }, fills: new[] { "iron-ore" });
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache));
        Assert.Contains("commodity:iron", fault.Message);
        Assert.Contains("commodity:scrap", fault.Message);
    }

    [Fact]
    public void ConsumerDemandWalksThroughARoleInput()
    {
        using var f = new EconomyFixture();
        f.Good("rawx");
        f.Good("acid", inputs: new[] { ("rawx", 1) }, fills: new[] { "reagent" });
        f.Good("g1", roleInputs: new[] { ("reagent", 1) }, fills: new[] { "Barrel" });
        f.Good("g2", roleInputs: new[] { ("reagent", 1) }, fills: new[] { "Plate" });
        f.Gear("cannon", new[] { ("Barrel", 1) });
        f.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        Assert.Equal(new[] { "recipe:" + EconomyFixture.Key("acid") }, Records(EconomyChecks.ConsumerDemand(f.Cache), EconomyChecks.LDemand));
        f.Good("toaster", roles: new[] { ("Barrel", 1) });
        Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));
    }

    private static EconomyFixture Casings(Action<EconomyFixture> design)
    {
        var f = EconomyFixture.Healthy();
        f.Good("cap", inputs: new[] { ("ore", 1) }, fills: new[] { "Casing" });
        f.Good("alloy", inputs: new[] { ("ore", 1) }, fills: new[] { "Casing", "Plate" });
        design(f);
        return f;
    }

    [Fact]
    public void ADesignWithARecipeNamesItsRolesOfferedOrNot()
    {
        using var f = Casings(g => g.Gear("shell", new[] { ("Casing", 1) }, offered: false));
        Assert.DoesNotContain(EconomyChecks.SourceSink(f.Cache), x => x.Record == "recipe:" + EconomyFixture.Key("cap") && x.Message.Contains("no sink"));
        Assert.Equal(new[] { "recipe:" + EconomyFixture.Key("alloy") }, Records(EconomyChecks.ConsumerDemand(f.Cache), EconomyChecks.LDemand));
    }

    [Fact]
    public void OfferedDesignsWithoutARecipeAreFaultedInKeyOrder()
    {
        using var f = EconomyFixture.Healthy();
        f.Gear("zeta", new[] { ("Plate", 1) }, recipe: false);
        f.Gear("alpha", new[] { ("Plate", 1) }, recipe: false);
        Assert.Equal(new[] { "gear:alpha", "gear:zeta" }, EconomyChecks.Recipe(f.Cache).Select(x => x.Record));
    }

    [Fact]
    public void AnOfferedDesignWithoutARecipeStillNamesItsRoles()
    {
        using (var offered = Casings(f => f.Gear("shell", new[] { ("Casing", 1) }, recipe: false)))
        {
            Assert.DoesNotContain(EconomyChecks.SourceSink(offered.Cache), x => x.Record == "recipe:" + EconomyFixture.Key("cap") && x.Message.Contains("no sink"));
            Assert.Equal(new[] { "recipe:" + EconomyFixture.Key("alloy") }, Records(EconomyChecks.ConsumerDemand(offered.Cache), EconomyChecks.LDemand));
        }
        foreach (var design in new Action<EconomyFixture>[]
        {
            f => f.Gear("shell", new[] { ("Casing", 1) }, offered: false, recipe: false),
            f => f.Hull("outpost", HullType.Station, new[] { ("Casing", 1) }, recipe: false)
        })
            using (var f = Casings(design))
            {
                Assert.Contains(EconomyChecks.SourceSink(f.Cache), x => x.Record == "recipe:" + EconomyFixture.Key("cap") && x.Message.Contains("no sink"));
                Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));
            }
    }
}
