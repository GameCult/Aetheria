using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

public sealed class EconomyCommandTests
{
    private sealed class Lambda : EconomyBatch
    {
        private readonly Action<EconomyAuthor> _author;
        public Lambda(Action<EconomyAuthor> author) => _author = author;
        public override void Author(EconomyAuthor author) => _author(author);
    }

    private static void Flush(EconomyFixture f) => f.Cache.FlushAsync().GetAwaiter().GetResult();

    private static CultCache Read(EconomyFixture f) => AetheriaStores.Open(f.CatalogPath, registry: TestCatalog.Registry());

    private static int Author(EconomyFixture f, bool apply, out string output, params Action<EconomyAuthor>[] batches)
    {
        Flush(f);
        var writer = new StringWriter();
        var code = EconomyCommand.Author(f.CatalogPath, batches.Select(b => (EconomyBatch) new Lambda(b)).ToList(), apply, writer, TestCatalog.Registry());
        output = writer.ToString();
        return code;
    }

    // A commodity and its recipe, run by the fixture's mill unless a kind is given.
    private static void Good(EconomyFixture f, EconomyAuthor a, string id, string lore = "a good", string[] fills = null, (string Id, int Count)[] inputs = null,
        CultRecordRef<StationKindData>? kind = null)
    {
        var key = new CultRecordKey(EconomyFixture.Key(id));
        if (inputs == null) a.Put(new SimpleCommodityData { Name = id }, key);
        else a.Put(new CompoundCommodityData { Name = id }, key);
        a.Put(new RecipeData
        {
            Output = EconomyFixture.Item(key.Value), StationKind = kind ?? f.Mill, LoreHook = lore,
            Inputs = (inputs ?? Array.Empty<(string, int)>()).Select(i => new CommodityCount { Commodity = EconomyFixture.Item(EconomyFixture.Key(i.Id)), Count = i.Count }).ToList(),
            Fills = (fills ?? Array.Empty<string>()).ToList()
        }, RecipeData.KeyOf(key));
    }

    [Fact]
    public void ABatchWithAShapeFaultIsRefusedAndTheCatalogStaysByteIdentical()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output, a => Good(f, a, "tar", lore: " "));
        Assert.Equal(1, code);
        Assert.Contains("L-lore", output);
        Assert.Contains("refused", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void TheGateRefusesALoopThatCannotSustainItself()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output, a =>
        {
            Good(f, a, "loopa", inputs: new[] { ("loopb", 1) }, fills: new[] { "Plate" });
            Good(f, a, "loopb", inputs: new[] { ("loopa", 1) });
        });
        Assert.Equal(1, code);
        Assert.Contains("L-productive", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void ADryRunWritesNothingAndPrintsWhatItWouldDo()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, false, out var output, a => Good(f, a, "coal"), a => a.Edit<SimpleCommodityData>("ore", ore => ore.Description = "grey"));
        Assert.Equal(0, code);
        Assert.Contains("added commodity:coal", output);
        Assert.Contains("added recipe:commodity:coal", output);
        Assert.Contains("changed commodity:ore", output);
        Assert.Contains("Dry run", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void ACleanBatchAppliesAndTheCatalogThenReportsNoFault()
    {
        using var f = EconomyFixture.Healthy();
        var code = Author(f, true, out var output, a =>
        {
            Good(f, a, "coal");
            Good(f, a, "steel", inputs: new[] { ("coal", 2) }, fills: new[] { "Plate" });
        });
        Assert.Equal(0, code);
        Assert.Contains("0 faults", output);
        using var after = Read(f);
        Assert.NotNull(after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:steel"))));
        Assert.Empty(EconomyChecks.Check(after).Faults);
    }

    [Fact]
    public void ApplyConvergesAndTouchesOnlyWhatItNames()
    {
        using var f = EconomyFixture.Healthy();
        Action<EconomyAuthor> coal = a => Good(f, a, "coal", fills: new[] { "Plate" });
        Assert.Equal(0, Author(f, true, out _, coal));
        var bytes = File.ReadAllBytes(f.CatalogPath);
        Assert.Equal(0, Author(f, true, out var again, coal));
        Assert.Contains("0 added, 0 changed, 2 unchanged", again);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));

        Assert.Equal(0, Author(f, true, out var revised, a => Good(f, a, "coal", lore: "black rock", fills: new[] { "Plate" })));
        Assert.Contains("0 added, 1 changed, 1 unchanged", revised);
        using var after = Read(f);
        Assert.Equal("black rock", after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:coal"))).LoreHook);
        Assert.Equal("a good", after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:ore"))).LoreHook);
    }

    [Fact]
    public void ACompletenessFaultIsReportedAndDoesNotBlockApply()
    {
        using var f = EconomyFixture.Healthy();
        var code = Author(f, true, out var output, a => Good(f, a, "dust"));
        Assert.Equal(0, code);
        Assert.Contains("L-source-sink", output);
        using var after = Read(f);
        Assert.NotNull(after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:dust"))));
    }

    [Fact]
    public void ACommodityWrittenWithoutARecipeIsCompletenessNotARefusal()
    {
        using var f = EconomyFixture.Healthy();
        var code = Author(f, true, out var output, a => a.Put(new SimpleCommodityData { Name = "bare" }, new CultRecordKey("commodity:bare")));
        Assert.Equal(0, code);
        Assert.Contains("L-recipe", output);
    }

    [Fact]
    public void AuthorsAKindWithStandIns()
    {
        using var f = EconomyFixture.Healthy();
        var forge = StationKindData.KeyOf("forge");
        var code = Author(f, true, out _, a =>
        {
            a.Put(new StationKindData { Name = "Forge", StandIns = new List<StationKindStandIn> { new StationKindStandIn { Faction = f.Maker, Kind = f.Mill } } }, forge);
            Good(f, a, "slab", kind: new CultRecordRef<StationKindData>(forge), fills: new[] { "Plate" });
        });
        Assert.Equal(0, code);
        using var after = Read(f);
        Assert.Single(after.Get<StationKindData>(forge).StandIns);
        Assert.DoesNotContain(EconomyChecks.Check(after).Faults, x => x.Invariant == EconomyChecks.LOneKindList);
    }

    [Fact]
    public void AKindThatStandsInForItselfIsRefusedAndNothingIsWritten()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var forge = StationKindData.KeyOf("forge");
        var code = Author(f, true, out var output, a => a.Put(new StationKindData
        {
            Name = "Forge", StandIns = new List<StationKindStandIn> { new StationKindStandIn { Faction = f.Maker, Kind = new CultRecordRef<StationKindData>(forge) } }
        }, forge));
        Assert.Equal(1, code);
        Assert.Contains("refused", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void AnEditNamingNoItemIsARefusalNotAThrow()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output, a => a.Edit<SimpleCommodityData>("unobtainium", i => i.Price = 3));
        Assert.Equal(1, code);
        Assert.Contains("unobtainium", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void AnEditChangesOnlyTheNamedItem()
    {
        using var f = EconomyFixture.Healthy();
        var code = Author(f, true, out _, a => a.Edit<SimpleCommodityData>("ore", ore => { ore.Description = "grey"; ore.Price = 7; ore.Mass = 2; }));
        Assert.Equal(0, code);
        using var after = Read(f);
        var ore = after.Get<SimpleCommodityData>(new CultRecordKey("commodity:ore"));
        Assert.Equal(("grey", 7, 2f), (ore.Description, ore.Price, ore.Mass));
        Assert.Null(after.Get<CompoundCommodityData>(new CultRecordKey("commodity:plate")).Description);
    }

    [Fact]
    public void ReportPrintsFaultsAndDepthAndFailsOnAnyFault()
    {
        using var f = EconomyFixture.Healthy();
        var healthy = new StringWriter();
        Assert.Equal(0, EconomyCommand.Report(f.Cache, healthy));
        Assert.Contains("commodity:plate", healthy.ToString());
        Assert.Contains("depth 1", healthy.ToString());
        f.EditRecipe("commodity:plate", r => r.LoreHook = "");
        var broken = new StringWriter();
        Assert.Equal(1, EconomyCommand.Report(f.Cache, broken));
        Assert.Contains("L-lore", broken.ToString());
    }

    [Fact]
    public void AnUnknownSubcommandFails() => Assert.Equal(1, EconomyCommand.Run(new[] { "bogus" }));
}
