using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Xunit;

// The economy checks over small fixture catalogs (aetheria-living-world, cut recipe-schema-and-checks). Each rule is refused by
// a catalog that breaks only it.
public sealed class EconomyChecksTests
{
    private static string[] Records(IEnumerable<EconomyFault> faults, string invariant) =>
        faults.Where(f => f.Invariant == invariant).Select(f => f.Record).OrderBy(r => r, StringComparer.Ordinal).ToArray();

    private static string Ore => EconomyFixture.Key("ore");

    [Fact]
    public void AHealthyCatalogHasNoFaults()
    {
        using var f = EconomyFixture.Healthy();
        var report = EconomyChecks.Check(f.Cache);
        Assert.Empty(report.Faults);
        Assert.Equal(2, report.Depth["gear:laser"]);
        Assert.Equal(2, report.Depth["hull:skiff"]);
    }

    [Fact]
    public void RecipeRefusesAGoodWithoutOne()
    {
        using var f = EconomyFixture.Healthy();
        f.Cache.Remove(RecipeData.KeyOf(new CultRecordKey(Ore)));
        f.Gear("bare", new[] { ("Plate", 1) }, recipe: false);
        f.Hull("barehull", HullType.Ship, new[] { ("Plate", 1) }, recipe: false);
        f.Hull("barestation", HullType.Station, new (string, int)[0], recipe: false);
        f.Hull("bareturret", HullType.Turret, new (string, int)[0], recipe: false);
        f.Gear("unoffered", new[] { ("Plate", 1) }, offered: false, recipe: false);
        Assert.Equal(new[] { "commodity:ore", "gear:bare", "hull:barehull" }, Records(EconomyChecks.Recipe(f.Cache), EconomyChecks.LRecipe));
    }

    public static IEnumerable<object[]> BadShapes()
    {
        object[] Case(string name, Action<EconomyFixture> mutate, string record, string invariant = "L-recipe", string says = "") => new object[] { name, mutate, record, invariant, says };
        yield return Case("zero output count", f => f.EditRecipe(Ore, r => r.OutputCount = 0), "recipe:" + Ore);
        yield return Case("negative output count", f => f.EditRecipe(Ore, r => r.OutputCount = -1), "recipe:" + Ore);
        yield return Case("zero cycle", f => f.EditRecipe(Ore, r => r.Cycle = 0), "recipe:" + Ore);
        yield return Case("negative cycle", f => f.EditRecipe(Ore, r => r.Cycle = -2), "recipe:" + Ore);
        yield return Case("NaN cycle", f => f.EditRecipe(Ore, r => r.Cycle = float.NaN), "recipe:" + Ore);
        yield return Case("infinite cycle", f => f.EditRecipe(Ore, r => r.Cycle = float.PositiveInfinity), "recipe:" + Ore);
        yield return Case("negative workforce", f => f.EditRecipe(Ore, r => r.Workforce = -1), "recipe:" + Ore);
        yield return Case("zero input count", f => f.EditRecipe("commodity:plate", r => r.Inputs[0].Count = 0), "recipe:commodity:plate", says: "Inputs[0] count");
        yield return Case("zero upkeep count", f => f.EditRecipe("commodity:plate", r => r.Upkeep.Add(new CommodityCount { Commodity = EconomyFixture.Item(Ore), Count = 0 })), "recipe:commodity:plate", says: "Upkeep[0] count");
        yield return Case("input that is not a commodity", f => f.EditRecipe("commodity:plate", r => r.Inputs.Add(new CommodityCount { Commodity = EconomyFixture.Item("gear:laser"), Count = 1 })), "recipe:commodity:plate");
        yield return Case("upkeep that is not a commodity", f => f.EditRecipe("commodity:plate", r => r.Upkeep.Add(new CommodityCount { Commodity = EconomyFixture.Item("hull:skiff"), Count = 1 })), "recipe:commodity:plate");
        yield return Case("input naming nothing", f => f.EditRecipe("commodity:plate", r => r.Inputs.Add(new CommodityCount { Commodity = EconomyFixture.Item("commodity:ghost"), Count = 1 })), "recipe:commodity:plate");
        yield return Case("inputs on a role recipe", f => f.EditRecipe("gear:laser", r => r.Inputs.Add(new CommodityCount { Commodity = EconomyFixture.Item(Ore), Count = 1 })), "recipe:gear:laser");
        yield return Case("a role with no filler", f => f.EditItem<GearData>("gear:laser", g => g.Roles.Add(new ItemRole { Name = "Mystery" })), "recipe:gear:laser");
        yield return Case("a role count of zero", f => f.EditItem<GearData>("gear:laser", g => g.Roles[0].Count = 0), "recipe:gear:laser");
        yield return Case("fills on a design", f => f.EditRecipe("gear:laser", r => r.Fills.Add("Plate")), "recipe:gear:laser");
        yield return Case("a design with no roles", f => f.EditItem<GearData>("gear:laser", g => g.Roles.Clear()), "recipe:gear:laser");
        yield return Case("an unknown station kind", f => f.EditRecipe(Ore, r => r.StationKind = new CultRecordRef<StationKindData>(StationKindData.KeyOf("nowhere"))), "recipe:" + Ore);
        yield return Case("an unset station kind", f => f.EditRecipe(Ore, r => r.StationKind = default), "recipe:" + Ore);
        yield return Case("an output naming nothing", f => f.EditRecipe(Ore, r => r.Output = EconomyFixture.Item("commodity:ghost")), "recipe:" + Ore);
        yield return Case("a kind no faction places", f =>
        {
            var ghost = f.Station("ghost", "Ghost");
            f.EditRecipe(Ore, r => r.StationKind = ghost);
        }, "recipe:" + Ore, EconomyChecks.LOneKindList);
    }

    [Theory]
    [MemberData(nameof(BadShapes))]
    public void RecipeRefusesBadShape(string name, Action<EconomyFixture> mutate, string record, string invariant, string says)
    {
        using var f = EconomyFixture.Healthy();
        Assert.Empty(EconomyChecks.Recipe(f.Cache));
        mutate(f);
        var faults = EconomyChecks.Recipe(f.Cache);
        var matched = faults.Where(x => x.Invariant == invariant && x.Record == record).ToArray();
        Assert.True(matched.Length > 0, name + ": " + string.Join(" | ", faults));
        Assert.All(matched, x => Assert.True(x.Message.Length > 20 && x.Message.Contains(says), name + ": " + x.Message));
        if (name == "an output naming nothing") Assert.Single(faults.Where(x => x.Record == record));
        if (invariant == EconomyChecks.LOneKindList) Assert.DoesNotContain(faults, x => x.Invariant == EconomyChecks.LRecipe);
    }

    [Fact]
    public void RecipeRefusesARecipeUnderTheWrongKey()
    {
        using var f = EconomyFixture.Healthy();
        var recipe = f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey(Ore)));
        f.Cache.Remove(RecipeData.KeyOf(new CultRecordKey(Ore)));
        f.Cache.Upsert(typeof(RecipeData), recipe, new CultRecordKey("recipe:stray"));
        var faults = EconomyChecks.Recipe(f.Cache);
        Assert.Contains(faults, x => x.Record == "recipe:stray" && x.Message.Contains("key"));
        Assert.Contains(faults, x => x.Record == Ore);
    }

    [Fact]
    public void LoreRefusesBlankHookOnCommodities()
    {
        using var f = EconomyFixture.Healthy();
        Assert.Empty(EconomyChecks.Lore(f.Cache));
        f.EditRecipe(Ore, r => r.LoreHook = "");
        f.EditRecipe("commodity:plate", r => r.LoreHook = "  \t");
        f.Good("tar");
        f.EditRecipe("commodity:tar", r => r.LoreHook = null);
        Assert.Equal(new[] { "recipe:commodity:ore", "recipe:commodity:plate", "recipe:commodity:tar" }, Records(EconomyChecks.Lore(f.Cache), EconomyChecks.LLore));
        Assert.DoesNotContain(EconomyChecks.Lore(f.Cache), x => x.Record.Contains("gear") || x.Record.Contains("hull"));
    }

    [Fact]
    public void SourceSinkCountsTheGate()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("waste");
        f.Good("loopa", inputs: new[] { ("loopb", 1) }, fills: new[] { "Plate" });
        f.Good("loopb", inputs: new[] { ("loopa", 1) }, fills: new[] { "Plate" });
        var faults = EconomyChecks.SourceSink(f.Cache);
        Assert.All(faults, x => Assert.Equal(EconomyChecks.LSource, x.Invariant));
        Assert.Equal(new[] { "recipe:commodity:loopa", "recipe:commodity:loopb" }, faults.Where(x => x.Message.Contains("source")).Select(x => x.Record).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "recipe:commodity:waste" }, faults.Where(x => x.Message.Contains("sink")).Select(x => x.Record));

        f.Gate(("commodity:waste", true, false), ("commodity:loopa", false, true));
        Assert.Empty(EconomyChecks.SourceSink(f.Cache));

        f.Gate(("commodity:waste", false, true), ("commodity:loopa", true, false));
        Assert.Equal(new[] { "recipe:commodity:loopa", "recipe:commodity:loopb", "recipe:commodity:waste" },
            EconomyChecks.SourceSink(f.Cache).Select(x => x.Record).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void SourceSinkLeavesAConsumerGoodWithoutASink()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("kettle", roles: new[] { ("Plate", 1) });
        Assert.Empty(EconomyChecks.SourceSink(f.Cache));
    }

    [Fact]
    public void SourceSinkTakesARoleSomeDesignNamesAsASink()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("rivet", fills: new[] { "Plate" });
        f.Good("spare", fills: new[] { "Unwanted" });
        Assert.Equal(new[] { "recipe:commodity:spare" }, EconomyChecks.SourceSink(f.Cache).Select(x => x.Record).ToArray());
    }

    private static EconomyFixture Loop(int aCount, int aOut, int bCount = 1, int bOut = 1)
    {
        var f = new EconomyFixture();
        f.Good("a", inputs: new[] { ("b", aCount) }, outputCount: aOut);
        f.Good("b", inputs: new[] { ("a", bCount) }, outputCount: bOut);
        return f;
    }

    [Fact]
    public void ProductiveRefusesALoopAtOne()
    {
        using var f = Loop(1, 1);
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache));
        Assert.Equal("L-productive", fault.Invariant);
        Assert.Equal(EconomyFixture.Key("a"), fault.Record);
        Assert.Contains("spectral radius 1 ", fault.Message);
        Assert.Contains("commodity:a -> commodity:b.", fault.Message);
        using var gentle = Loop(9, 10);
        Assert.Empty(EconomyChecks.Productive(gentle.Cache));
        using var feed = Loop(1, 1);
        feed.Good("c", inputs: new[] { ("a", 1) });
        Assert.Single(EconomyChecks.Productive(feed.Cache));
    }

    [Fact]
    public void ProductiveRefusesJustBelowTheLine()
    {
        using var f = Loop(1999999999, 2000000000);
        Assert.Single(EconomyChecks.Productive(f.Cache));
        using var g = Loop(1999999900, 2000000000);
        Assert.Empty(EconomyChecks.Productive(g.Cache));
    }

    [Fact]
    public void ProductiveIncludesInputsAndTheInputFactor()
    {
        using var f = Loop(1, 2);
        Assert.Empty(EconomyChecks.Productive(f.Cache));
        Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { InputFactor = 2 }));
        Assert.Empty(EconomyChecks.Productive(f.Cache, new EconomyLoad { UpkeepFactor = 4, WorkforceFactor = 4 }));
    }

    [Fact]
    public void ProductiveIncludesUpkeepAndItsFactor()
    {
        using var f = new EconomyFixture();
        f.Good("a", upkeep: new[] { ("b", 3) }, outputCount: 4);
        f.Good("b", upkeep: new[] { ("a", 1) });
        Assert.Empty(EconomyChecks.Productive(f.Cache));
        Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { UpkeepFactor = 2 }));
        Assert.Empty(EconomyChecks.Productive(f.Cache, new EconomyLoad { InputFactor = 4, WorkforceFactor = 4 }));
        f.EditRecipe("commodity:a", r => r.Upkeep[0].Count = 4);
        Assert.Single(EconomyChecks.Productive(f.Cache));
    }

    [Fact]
    public void ProductiveIncludesLabourAndTheWorkforceFactor()
    {
        using var f = new EconomyFixture();
        f.Good("a", outputCount: 2, workforce: 1, cycle: 1);
        var needs = new EconomyLoad { LabourNeeds = { { "commodity:a", 1.5 } } };
        Assert.Empty(EconomyChecks.Productive(f.Cache));
        Assert.Empty(EconomyChecks.Productive(f.Cache, needs));
        Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:a", 1.5 } }, WorkforceFactor = 2 }));
        Assert.Empty(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:a", 1.5 } }, InputFactor = 4, UpkeepFactor = 4 }));
        f.EditRecipe("commodity:a", r => r.Cycle = 3);
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache, needs));
        Assert.Contains("labour", fault.Message);
        f.EditRecipe("commodity:a", r => { r.Cycle = 3; r.Workforce = 0; });
        Assert.Empty(EconomyChecks.Productive(f.Cache, needs));
    }

    [Fact]
    public void ProductiveRefusesFactorsBelowOne()
    {
        using var f = new EconomyFixture();
        Assert.Contains("at least 1", Assert.Throws<ArgumentException>(() => EconomyChecks.Productive(f.Cache, new EconomyLoad { InputFactor = 0.5 })).Message);
        Assert.Throws<ArgumentException>(() => EconomyChecks.Productive(f.Cache, new EconomyLoad { WorkforceFactor = 0.5 }));
        Assert.Throws<ArgumentException>(() => EconomyChecks.Productive(f.Cache, new EconomyLoad { UpkeepFactor = double.NaN }));
        Assert.Empty(EconomyChecks.Productive(f.Cache, new EconomyLoad { InputFactor = 1, WorkforceFactor = 1, UpkeepFactor = 1 }));
    }

    // Labour needs a meal, the meal takes one filler of "Fuel", the filler takes labour: the loop closes through the role.
    private static EconomyFixture MealLoop(int firstWorkers, int secondWorkers)
    {
        var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Fuel", 1) });
        f.Good("afuel", fills: new[] { "Fuel" }, workforce: firstWorkers, outputCount: 5);
        f.Good("bfuel", fills: new[] { "Fuel" }, workforce: secondWorkers, outputCount: 5);
        return f;
    }

    private static EconomyLoad MealNeeds => new EconomyLoad { LabourNeeds = { { "commodity:meal", 1 } } };

    [Fact]
    public void ProductiveTakesTheWorstSingleFill()
    {
        foreach (var (first, second) in new[] { (3, 6), (6, 3) })
        {
            using var f = MealLoop(first, second);
            var fault = Assert.Single(EconomyChecks.Productive(f.Cache, MealNeeds));
            Assert.Contains(first > second ? "commodity:afuel" : "commodity:bfuel", fault.Message);
        }
        using var even = MealLoop(3, 3);
        Assert.Empty(EconomyChecks.Productive(even.Cache, MealNeeds));
        Assert.Empty(EconomyChecks.Productive(even.Cache));
        using var better = MealLoop(2, 3);
        Assert.Empty(EconomyChecks.Productive(better.Cache, MealNeeds));
    }

    [Fact]
    public void ProductiveFindsTheWorstFillAmongSeveralRoles()
    {
        using var f = new EconomyFixture();
        f.Good("meal", roles: new[] { ("Fuel", 1), ("Spice", 1) });
        f.Good("fuela", fills: new[] { "Fuel" }, workforce: 1, outputCount: 5);
        f.Good("fuelb", fills: new[] { "Fuel" }, workforce: 1, outputCount: 5);
        f.Good("spicea", fills: new[] { "Spice" }, workforce: 1, outputCount: 5);
        f.Good("spiceb", fills: new[] { "Spice" }, workforce: 3, outputCount: 5);
        Assert.Empty(EconomyChecks.Productive(f.Cache, MealNeeds));
        f.EditItem<CompoundCommodityData>("commodity:meal", m => m.Roles[1].Count = 2);
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache, new EconomyLoad { LabourNeeds = { { "commodity:meal", 1.3 } } }));
        Assert.Contains("commodity:spiceb", fault.Message);
    }

    [Fact]
    public void DeepIsMinOverFillersMaxOverInputs()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("bar", inputs: new[] { ("ore", 1) });
        f.Good("cores", inputs: new[] { ("ore", 1) }, fills: new[] { "Core" });
        f.Good("cored", inputs: new[] { ("bar", 1) }, fills: new[] { "Core" });
        f.Good("alloy", inputs: new[] { ("bar", 1), ("cored", 1) }, fills: new[] { "Cover" });
        f.Gear("rifle", new[] { ("Core", 1) });
        f.Hull("frigate", HullType.Ship, new[] { ("Core", 1), ("Cover", 1) });
        f.Good("loopa", inputs: new[] { ("loopb", 1) });
        f.Good("loopb", inputs: new[] { ("loopa", 1) });
        var depth = EconomyChecks.Deep(f.Cache);
        Assert.Equal(0, depth["commodity:ore"]);
        Assert.Equal(1, depth["commodity:bar"]);
        Assert.Equal(1, depth["commodity:cores"]);
        Assert.Equal(2, depth["commodity:cored"]);
        Assert.Equal(3, depth["commodity:alloy"]);
        Assert.Equal(2, depth["gear:rifle"]);
        Assert.Equal(4, depth["hull:frigate"]);
        Assert.Null(depth["commodity:loopa"]);
        Assert.Null(depth["commodity:loopb"]);
    }

    [Fact]
    public void DeepReportsAndNeverRefuses()
    {
        using var f = EconomyFixture.Healthy();
        var report = EconomyChecks.Check(f.Cache);
        Assert.Equal(2, report.Depth["hull:skiff"]);
        Assert.DoesNotContain(report.Faults, x => x.Invariant.Contains("deep", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(report.Faults);
    }

    private static string[] DemandRecords(EconomyFixture f) => EconomyChecks.ConsumerDemand(f.Cache).Select(x => x.Record).ToArray();

    // A gear whose weapon draws the given commodity as ammunition (nothing when draws is null).
    private static void Rifle(EconomyFixture f, string id, string draws, bool offered = true)
    {
        var key = f.Gear(id, new[] { ("Frame", 1) }, offered);
        f.EditItem<GearData>(key, g => g.Behaviors.Add(new InstantWeaponData
        {
            AmmoType = draws == null ? default : new CultRecordRef<SimpleCommodityData>(new CultRecordKey(EconomyFixture.Key(draws)))
        }));
    }

    [Fact]
    public void ConsumerDemandExemptsAGoodWhoseUsesLeadIntoOneGearRole()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("ingot", inputs: new[] { ("ore", 1) });
        f.Good("steel", inputs: new[] { ("ingot", 1) });
        f.Good("barrel", inputs: new[] { ("steel", 1) }, fills: new[] { "Barrel" });
        f.Gear("cannon", new[] { ("Barrel", 1) });
        f.Gear("mortar", new[] { ("Barrel", 2) });
        Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));
    }

    [Fact]
    public void ConsumerDemandCoversAGoodThatLeadsIntoTwoGearOrHullRoles()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("slag", inputs: new[] { ("ore", 1) });
        f.Good("ingot", inputs: new[] { ("slag", 1) });
        f.Good("barrel", inputs: new[] { ("ingot", 1) }, fills: new[] { "Barrel" });
        f.Good("plate", inputs: new[] { ("ingot", 1) }, fills: new[] { "Plate" });
        f.Gear("cannon", new[] { ("Barrel", 1) });
        f.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        var faults = EconomyChecks.ConsumerDemand(f.Cache);
        Assert.Equal(new[] { "recipe:commodity:ingot", "recipe:commodity:slag" }, faults.Select(x => x.Record));
        Assert.All(faults, x => Assert.Equal(EconomyChecks.LDemand, x.Invariant));
        Assert.Contains("Barrel and Plate", faults[0].Message);

        // A consumer good taking a role that a good downstream of ingot fills clears the chain behind it.
        f.Good("pot", roles: new[] { ("Casing", 1) });
        f.EditRecipe("commodity:barrel", r => r.Fills.Add("Casing"));
        Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));
    }

    [Fact]
    public void ConsumerDemandIgnoresTheStepWindow()
    {
        using var near = new EconomyFixture();
        near.Good("ore");
        near.Good("ingot", inputs: new[] { ("ore", 1) }, fills: new[] { "Barrel", "Plate" });
        near.Gear("cannon", new[] { ("Barrel", 1) });
        near.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        Assert.Equal(new[] { "recipe:commodity:ingot" }, DemandRecords(near));

        using var far = new EconomyFixture();
        far.Good("ore");
        far.Good("c1", inputs: new[] { ("ore", 1) });
        for (var i = 2; i <= 5; i++) far.Good("c" + i, inputs: new[] { ("c" + (i - 1), 1) });
        far.Good("c6", inputs: new[] { ("c5", 1) }, fills: new[] { "Barrel", "Plate" });
        far.Gear("cannon", new[] { ("Barrel", 1) });
        far.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        Assert.Equal(new[] { "c1", "c2", "c3", "c4", "c5", "c6" }.Select(id => "recipe:commodity:" + id), DemandRecords(far));
    }

    [Fact]
    public void ConsumerDemandCoversAGoodThatLeadsIntoUpkeep()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("coke", inputs: new[] { ("ore", 1) });
        f.Good("fuel", inputs: new[] { ("coke", 1) });
        f.Good("plate", inputs: new[] { ("ore", 1) }, fills: new[] { "Plate" }, upkeep: new[] { ("fuel", 1) });
        f.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        // Fuel is upkeep itself; coke is shared because a good it leads into is.
        var faults = EconomyChecks.ConsumerDemand(f.Cache);
        Assert.Equal(new[] { "recipe:commodity:coke", "recipe:commodity:fuel" }, faults.Select(x => x.Record));
        Assert.Contains("'fuel' leads into upkeep but no use leads into a consumer good.", faults[1].Message);

        f.Good("pot", roles: new[] { ("Casing", 1) });
        f.EditRecipe("commodity:fuel", r => r.Fills.Add("Casing"));
        Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));
    }

    [Fact]
    public void ConsumerDemandCountsADrawnRoundAsOneUse()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("casing", inputs: new[] { ("ore", 1) });
        f.Good("round", inputs: new[] { ("casing", 1) });
        Rifle(f, "rifle", "round");
        Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));

        // Acid also feeds a warhead role: two uses, so it is shared; the casing still feeds only the round.
        f.Good("acid", inputs: new[] { ("ore", 1) });
        f.Good("warhead", inputs: new[] { ("acid", 1) }, fills: new[] { "Warhead" });
        f.Gear("mine", new[] { ("Warhead", 1) });
        f.EditRecipe("commodity:round", r => r.Inputs.Add(new CommodityCount { Commodity = EconomyFixture.Item(EconomyFixture.Key("acid")), Count = 1 }));
        var fault = Assert.Single(EconomyChecks.ConsumerDemand(f.Cache));
        Assert.Equal("recipe:commodity:acid", fault.Record);
        Assert.Contains("Warhead and round", fault.Message);

        // Without the weapon nothing is drawn, and acid has one use again.
        f.EditItem<GearData>("gear:rifle", g => g.Behaviors.Clear());
        Assert.Empty(EconomyChecks.ConsumerDemand(f.Cache));
    }

    [Fact]
    public void ConsumerDemandNamesEveryUseThatMadeTheGoodShared()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("acid", inputs: new[] { ("ore", 1) });
        f.Good("warhead", inputs: new[] { ("acid", 1) }, fills: new[] { "Warhead" });
        f.Good("roundb", inputs: new[] { ("acid", 1) });
        f.Good("rounda", inputs: new[] { ("acid", 1) });
        f.Gear("mine", new[] { ("Warhead", 1) });
        Rifle(f, "riflea", "rounda");
        Rifle(f, "rifleb", "roundb");
        var fault = Assert.Single(EconomyChecks.ConsumerDemand(f.Cache));
        Assert.Equal("'acid' leads into Warhead, rounda and roundb but no use leads into a consumer good.", fault.Message);
    }

    [Fact]
    public void ConsumerDemandTreatsTheGateAsNoUse()
    {
        using var f = new EconomyFixture();
        f.Good("ore");
        f.Good("ingot", inputs: new[] { ("ore", 1) }, fills: new[] { "Barrel", "Plate" });
        f.Good("slug", inputs: new[] { ("ore", 1) }, fills: new[] { "Slug" });
        f.Gear("cannon", new[] { ("Barrel", 1) });
        f.Gear("sling", new[] { ("Slug", 1) });
        f.Hull("tug", HullType.Ship, new[] { ("Plate", 1) });
        f.Gate(("commodity:ingot", true, true), ("commodity:slug", true, true));
        Assert.Equal(new[] { "recipe:commodity:ingot" }, DemandRecords(f));
    }

    [Fact]
    public void SourceSinkCountsAWeaponsDrawAsASink()
    {
        static EconomyFault NoSink(EconomyFixture f) =>
            EconomyChecks.SourceSink(f.Cache).FirstOrDefault(x => x.Record == "recipe:commodity:round" && x.Message.Contains("no sink"));
        static EconomyFixture Catalog(string draws, bool offered)
        {
            var f = new EconomyFixture();
            f.Good("ore");
            f.Good("round", inputs: new[] { ("ore", 1) });
            Rifle(f, "rifle", draws, offered);
            return f;
        }
        using (var drawn = Catalog("round", true)) Assert.Null(NoSink(drawn));
        using (var unoffered = Catalog("round", false)) Assert.Contains("no weapon draws it", NoSink(unoffered).Message);
        using (var unset = Catalog(null, true)) Assert.NotNull(NoSink(unset));
    }

    [Fact]
    public void ProductiveMembersAreSortedWhileTheMessageKeepsCycleOrder()
    {
        using var f = new EconomyFixture();
        f.Good("loopa", inputs: new[] { ("loopc", 1) });
        f.Good("loopc", inputs: new[] { ("loopb", 1) });
        f.Good("loopb", inputs: new[] { ("loopa", 2) });
        var fault = Assert.Single(EconomyChecks.Productive(f.Cache));
        Assert.Equal(new[] { "commodity:loopa", "commodity:loopb", "commodity:loopc" }, fault.Members);
        Assert.Contains("commodity:loopa -> commodity:loopc -> commodity:loopb", fault.Message);
    }

    [Fact]
    public void ChecksArePure()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("loopa", inputs: new[] { ("loopb", 1) });
        f.Good("loopb", inputs: new[] { ("loopa", 1) });
        f.EditRecipe(Ore, r => r.LoreHook = "");
        var options = GameCult.Caching.MessagePack.CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);
        string[] Snapshot() => f.Cache.AllStoredDocuments
            .Select(d => d.Key.Value + "|" + Convert.ToBase64String(MessagePackSerializer.Serialize(d.Document.GetType(), d.Document, options)))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var before = Snapshot();
        var first = EconomyChecks.Check(f.Cache);
        var second = EconomyChecks.Check(f.Cache);
        Assert.NotEmpty(first.Faults);
        Assert.Equal(first.Faults.Select(x => x.ToString()), second.Faults.Select(x => x.ToString()));
        Assert.Equal(first.Depth.OrderBy(p => p.Key, StringComparer.Ordinal), second.Depth.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal(before, Snapshot());
    }
}
