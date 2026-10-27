using System;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Pins found by mutation testing the economy checks: what each fault says, how the rules compose, the order faults come in, and
// the corners of the worst-fill search.
public sealed class EconomyChecksFurtherTests
{
    [Fact]
    public void OutsideMarketIsKeyedByItsId() =>
        Assert.Equal("outside-market:gate", OutsideMarketData.KeyOf("gate").Value);

    [Fact]
    public void AFaultReadsInvariantRecordMessage() =>
        Assert.Equal("L-x rec: said", new EconomyFault("L-x", "rec", "said").ToString());

    [Fact]
    public void CheckRunsEveryRule()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("slag", inputs: new[] { ("ore", 1) });
        f.Good("ingot", inputs: new[] { ("slag", 1) });
        f.Good("steel", inputs: new[] { ("ingot", 1) });
        f.Good("barrel", inputs: new[] { ("steel", 1) }, fills: new[] { "Barrel" });
        f.Good("plate", inputs: new[] { ("steel", 1) }, fills: new[] { "Plate" });
        f.Gear("cannon", new[] { ("Barrel", 1) });
        f.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        f.Gear("bare", new[] { ("Barrel", 1) }, recipe: false);
        f.Good("tar", lore: " ");
        f.Good("waste");
        f.Good("loopa", inputs: new[] { ("loopb", 1) }, fills: new[] { "Barrel" });
        f.Good("loopb", inputs: new[] { ("loopa", 1) });
        var report = EconomyChecks.Check(f.Cache);
        foreach (var invariant in new[] { EconomyChecks.LRecipe, EconomyChecks.LLore, EconomyChecks.LSource, EconomyChecks.LProductive, EconomyChecks.LDemand })
            Assert.Contains(report.Faults, x => x.Invariant == invariant);
        Assert.All(report.Faults, x => Assert.True(x.Message.Length > 20, x.ToString()));
        Assert.Equal(0, report.Depth["commodity:ore"]);
    }

    [Fact]
    public void FaultsComeOrderedByKeyAndSayWhichRecord()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("zinc");
        f.Good("alum");
        f.Cache.Remove(RecipeData.KeyOf(new CultRecordKey("commodity:zinc")));
        f.Cache.Remove(RecipeData.KeyOf(new CultRecordKey("commodity:alum")));
        f.Gear("bare", new[] { ("Plate", 1) }, recipe: false);
        var faults = EconomyChecks.Recipe(f.Cache);
        Assert.Equal(new[] { "commodity:alum", "commodity:zinc", "gear:bare" }, faults.Select(x => x.Record));
        Assert.Contains("alum", faults[0].Message);
        Assert.Contains("bare", faults[2].Message);
        f.Good("a1", lore: "");
        f.Good("b1", lore: "");
        Assert.Equal(new[] { "recipe:commodity:a1", "recipe:commodity:b1" }, EconomyChecks.Lore(f.Cache).Select(x => x.Record));
        Assert.All(EconomyChecks.Lore(f.Cache), x => Assert.Contains("lore hook", x.Message));
    }

    [Fact]
    public void SourceSinkSaysWhatIsMissingAndReadsUpkeepAsASink()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("coolant");
        f.Good("heater", upkeep: new[] { ("coolant", 1) }, fills: new[] { "Plate" });
        Assert.Empty(EconomyChecks.SourceSink(f.Cache));
        f.Good("waste");
        f.Good("loopa", inputs: new[] { ("loopb", 1) }, fills: new[] { "Plate" });
        f.Good("loopb", inputs: new[] { ("loopa", 1) }, fills: new[] { "Plate" });
        var faults = EconomyChecks.SourceSink(f.Cache);
        Assert.Contains(faults, x => x.Record == "recipe:commodity:waste" && x.Message.Contains("sink") && x.Message.Length > 20);
        Assert.Contains(faults, x => x.Record == "recipe:commodity:loopa" && x.Message.Contains("source") && x.Message.Length > 20);
    }

    [Fact]
    public void ChecksSurviveARecipeWhoseOutputIsGone()
    {
        using var f = EconomyFixture.Healthy();
        f.EditRecipe(Ore(), r => r.Output = EconomyFixture.Item("commodity:ghost"));
        f.Good("shard", inputs: new[] { ("ghost", 1) });
        Assert.NotEmpty(EconomyChecks.Recipe(f.Cache));
        EconomyChecks.SourceSink(f.Cache);
        EconomyChecks.Productive(f.Cache);
        EconomyChecks.ConsumerDemand(f.Cache);
        EconomyChecks.Deep(f.Cache);
    }

    private static string Ore() => EconomyFixture.Key("ore");

    [Fact]
    public void ConsumerDemandReadsAnInputTheCatalogLacks()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("steel", inputs: new[] { ("ore", 1), ("ghost", 1) });
        f.Good("barrel", inputs: new[] { ("steel", 1), ("ghost", 1) }, fills: new[] { "Barrel" });
        f.Good("plate", inputs: new[] { ("steel", 1) }, fills: new[] { "Plate" });
        f.Gear("cannon", new[] { ("Barrel", 1) });
        f.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        var faults = EconomyChecks.ConsumerDemand(f.Cache);
        Assert.Equal(new[] { "recipe:commodity:steel" }, faults.Select(x => x.Record));
        Assert.Contains("steel", faults[0].Message);
        Assert.True(faults[0].Message.Length > 20);
    }

    [Fact]
    public void ProductiveSkipsAnEmptyOutputAndUnknownGoods()
    {
        using var f = Loop();
        f.Good("z", inputs: new[] { ("a", 1) }, outputCount: 0, workforce: 1);
        f.Good("w", inputs: new[] { ("ghost", 5), ("a", 1) }, outputCount: 1);
        f.EditRecipe("commodity:a", r => r.Inputs.Add(new CommodityCount { Commodity = EconomyFixture.Item("commodity:z"), Count = 1 }));
        f.EditRecipe("commodity:a", r => r.Inputs.Add(new CommodityCount { Commodity = EconomyFixture.Item("commodity:ghost"), Count = 5 }));
        f.EditRecipe("commodity:a", r => r.Upkeep.Add(new CommodityCount { Commodity = EconomyFixture.Item("commodity:ghost"), Count = 5 }));
        Assert.Empty(EconomyChecks.Productive(f.Cache));
    }

    private static EconomyFixture Loop()
    {
        var f = new EconomyFixture();
        f.Good("a", inputs: new[] { ("b", 1) }, outputCount: 2);
        f.Good("b", inputs: new[] { ("a", 1) });
        return f;
    }

    [Fact]
    public void ProductiveNamesOnlyTheGoodsThatCarryTheRadius()
    {
        using var f = new EconomyFixture();
        f.Good("a", inputs: new[] { ("b", 1) });
        f.Good("b", inputs: new[] { ("a", 1) });
        f.Good("c", inputs: new[] { ("a", 1) });
        f.Good("d", inputs: new[] { ("c", 1) });
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache));
        Assert.EndsWith("carried by commodity:a -> commodity:b.", fault.Message);
        Assert.StartsWith("The goods cannot sustain themselves", fault.Message);
    }

    [Fact]
    public void ProductiveRefusesAGoodThatNeedsItself()
    {
        using var f = new EconomyFixture();
        f.Good("a", inputs: new[] { ("a", 1) });
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache));
        Assert.EndsWith("carried by commodity:a.", fault.Message);
        using var g = new EconomyFixture();
        g.Good("a", inputs: new[] { ("a", 1) }, outputCount: 2);
        Assert.Empty(EconomyChecks.Productive(g.Cache));
    }

    [Fact]
    public void ProductiveNamesTheCycleInOrderThroughLabour()
    {
        using var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Fuel", 1) });
        f.Good("afuel", fills: new[] { "Fuel" }, workforce: 3, outputCount: 5);
        f.Good("bfuel", fills: new[] { "Fuel" }, workforce: 6, outputCount: 5);
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } } }));
        Assert.EndsWith("carried by commodity:bfuel -> labour -> commodity:meal.", fault.Message);
        Assert.Equal("commodity:bfuel", fault.Record);
        Assert.Contains("spectral radius 1.06265", fault.Message);
    }

    [Fact]
    public void ProductiveScalesARoleByItsCountAndOutputAndTheInputFactor()
    {
        using var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Fuel", 1) }, outputCount: 2);
        f.Good("fuel", fills: new[] { "Fuel" }, workforce: 8, outputCount: 5);
        var needs = new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } } };
        Assert.Empty(EconomyChecks.Productive(f.Cache, needs));
        Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } }, InputFactor = 1.3 }));
        f.EditItem<CompoundCommodityData>("commodity:meal", m => m.Roles[0].Count = 2);
        Assert.Single(EconomyChecks.Productive(f.Cache, needs));
    }

    [Fact]
    public void ProductiveSkipsARoleNothingFills()
    {
        using var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Nothing", 1), ("Fuel", 0) });
        f.Good("fuel", fills: new[] { "Fuel" }, workforce: 8, outputCount: 5);
        Assert.Empty(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } } }));
    }

    [Fact]
    public void ProductiveFindsTheWorstFillFromAnAcyclicStart()
    {
        using var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Fuel", 1) });
        f.Good("afuel", fills: new[] { "Fuel" }, workforce: 0, outputCount: 5);
        f.Good("bfuel", fills: new[] { "Fuel" }, workforce: 10, outputCount: 5);
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } } }));
        Assert.Equal("commodity:bfuel", fault.Record);
    }

    [Fact]
    public void ProductiveFollowsAChoiceThatOpensAnotherChoice()
    {
        using var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Fuel", 1) });
        f.Good("x", fills: new[] { "Fuel" }, workforce: 1, outputCount: 5);
        f.Good("y", roles: new[] { ("Core", 1) }, fills: new[] { "Fuel" });
        f.Good("c1", fills: new[] { "Core" }, workforce: 0, outputCount: 5);
        f.Good("c2", fills: new[] { "Core" }, workforce: 10, outputCount: 5);
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } } }));
        Assert.Contains("commodity:y", fault.Message);
        Assert.Contains("commodity:c2", fault.Message);
    }
}
