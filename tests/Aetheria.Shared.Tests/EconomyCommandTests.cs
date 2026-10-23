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
        Assert.Contains("  L-lore recipe:commodity:tar: ", output);
        Assert.Contains("  recipe:commodity:tar: A commodity's recipe needs a lore hook.", output);
        Assert.Contains("1 shape faults", output);
        Assert.Contains("commodity:ore depth 0", output);
        Assert.True(output.IndexOf("commodity:ore depth", StringComparison.Ordinal) < output.IndexOf("commodity:plate depth", StringComparison.Ordinal));
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
        Assert.Contains("L-source-sink", output);
        Assert.True(output.IndexOf("L-productive (", StringComparison.Ordinal) < output.IndexOf("L-source-sink (", StringComparison.Ordinal));
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
        Assert.Contains("Wrote 4 records to Aetheria.cc", output);
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
        Assert.Contains("0 added, 0 changed, 0 removed, 2 unchanged", again);
        Assert.Contains("Nothing to write.", again);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));

        Assert.Equal(0, Author(f, true, out var revised, a => Good(f, a, "coal", lore: "black rock", fills: new[] { "Plate" })));
        Assert.Contains("0 added, 1 changed, 0 removed, 1 unchanged", revised);
        using var after = Read(f);
        Assert.Equal("black rock", after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:coal"))).LoreHook);
        Assert.Equal("a good", after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:ore"))).LoreHook);
    }

    [Fact]
    public void AFaultOnARecordTheBatchDidNotChangeDoesNotBlock()
    {
        using var f = EconomyFixture.Healthy();
        f.Good("tar", lore: " ");
        var code = Author(f, true, out var output, a => Good(f, a, "tar", lore: " "), a => Good(f, a, "coal"));
        Assert.Equal(0, code);
        Assert.Contains("L-lore", output);
        Assert.Contains("2 added, 0 changed, 0 removed, 2 unchanged", output);
        using var after = Read(f);
        Assert.NotNull(after.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:coal"))));
    }

    [Fact]
    public void TheTrialCopyIsRemoved()
    {
        using var f = EconomyFixture.Healthy();
        var before = Directory.GetDirectories(Path.GetTempPath(), "aetherdb-economy-*").Length;
        Author(f, false, out _, a => Good(f, a, "coal"));
        Author(f, true, out _, a => Good(f, a, "tar", lore: ""));
        Assert.Equal(before, Directory.GetDirectories(Path.GetTempPath(), "aetherdb-economy-*").Length);
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
        Assert.Contains("The catalog holds no SimpleCommodityData named 'unobtainium'.", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void AnEditNamingTwoItemsIsARefusalAndNothingIsWritten()
    {
        using var f = EconomyFixture.Healthy();
        f.Cache.Upsert(typeof(SimpleCommodityData), new SimpleCommodityData { Name = "twin" }, new CultRecordKey("commodity:twin-a"));
        f.Cache.Upsert(typeof(SimpleCommodityData), new SimpleCommodityData { Name = "twin" }, new CultRecordKey("commodity:twin-b"));
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output, a => a.Edit<SimpleCommodityData>("twin", i => i.Price = 3));
        Assert.Equal(1, code);
        Assert.Contains("2 SimpleCommodityData records are named 'twin'", output);
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
        var text = healthy.ToString();
        Assert.Contains("0 faults", text);
        Assert.Contains("commodity:plate depth 1", text);
        Assert.Contains("commodity:ore <- nothing | Mill, cycle 1, workforce 0 | a good", text);
        Assert.Contains("commodity:plate <- 2 x commodity:ore | Mill, cycle 1, workforce 0 | a good", text);
        Assert.Contains("gear:laser <- 2 x Plate | Mill", text);
        Assert.True(text.IndexOf("commodity:ore <-", StringComparison.Ordinal) < text.IndexOf("commodity:plate <-", StringComparison.Ordinal));
        Assert.True(text.IndexOf("commodity:plate <-", StringComparison.Ordinal) < text.IndexOf("gear:laser <-", StringComparison.Ordinal));
        f.EditRecipe("commodity:plate", r => r.LoreHook = "");
        var broken = new StringWriter();
        Assert.Equal(1, EconomyCommand.Report(f.Cache, broken));
        Assert.Contains("L-lore", broken.ToString());
    }

    [Fact]
    public void AnUnknownSubcommandFails() => Assert.Equal(1, EconomyCommand.Run(new[] { "bogus" }));

    private static CultRecordKey K(string id) => new CultRecordKey(EconomyFixture.Key(id));

    private static string FaultsOf(CultCache catalog, string invariant) =>
        string.Join(";", EconomyChecks.Check(catalog).Faults.Where(x => x.Invariant == invariant).Select(x => x.Record));

    [Fact]
    public void ACycleClosedByALaterBatchIsRefusedEvenThoughTheFaultNamesAnEarlierBatchsRecord()
    {
        using var f = EconomyFixture.Healthy();
        Assert.Equal(0, Author(f, true, out _, a =>
        {
            a.Put(new CompoundCommodityData { Name = "loopb" }, K("loopb"));
            Good(f, a, "loopa", inputs: new[] { ("loopb", 1) });
        }));
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output, a => Good(f, a, "loopb", inputs: new[] { ("loopa", 2) }));
        Assert.Equal(1, code);
        Assert.Contains("refused", output);
        Assert.Contains("L-productive", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void AnInPlaceEditThroughTheCatalogIsReportedChangedAndCheckedBeforeItIsWritten()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var plate = RecipeData.KeyOf(K("plate"));
        var code = Author(f, true, out var output, a =>
        {
            var held = a.Catalog.Get<RecipeData>(plate);
            held.LoreHook = " ";
            a.Put(held, plate);
        });
        Assert.Equal(1, code);
        Assert.Contains("changed recipe:commodity:plate", output);
        Assert.Contains("L-lore", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void ABatchRunsOnceAndWhatIsWrittenIsWhatWasChecked()
    {
        using var f = EconomyFixture.Healthy();
        var runs = 0;
        var code = Author(f, true, out _, a =>
        {
            runs++;
            Good(f, a, "tin", lore: runs == 1 ? "a good" : " ");
        });
        Assert.Equal(0, code);
        Assert.Equal(1, runs);
        using var after = Read(f);
        Assert.Equal("a good", after.Get<RecipeData>(RecipeData.KeyOf(K("tin"))).LoreHook);
        Assert.DoesNotContain(EconomyChecks.Check(after).Faults, x => x.Invariant == EconomyChecks.LLore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ABatchThatWritesTheRealCatalogIsRefusedByName(bool apply)
    {
        using var f = EconomyFixture.Healthy();
        var code = Author(f, apply, out var output, a => File.AppendAllText(f.CatalogPath, "x"));
        Assert.Equal(1, code);
        Assert.Contains("Aetheria.cc changed during the trial", output);
        Assert.DoesNotContain("Dry run", output);
    }

    [Fact]
    public void ABatchThatThrowsIsARefusalNamingItWithNoStackTrace()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output, a => Good(f, a, "zinc"), a => throw new IOException("boom"));
        Assert.Equal(1, code);
        Assert.Contains("The batch Lambda is refused: boom", output);
        Assert.DoesNotContain("   at ", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullOutsideMarketRowsOrRowIsANamedFaultNotAThrow(bool rowsNull)
    {
        using var f = EconomyFixture.Healthy();
        f.Cache.Upsert(typeof(OutsideMarketData), new OutsideMarketData { Name = "gate", Rows = rowsNull ? null : new List<OutsideMarketRow> { null } }, OutsideMarketData.KeyOf("gate"));
        var report = EconomyChecks.Check(f.Cache);
        Assert.Contains(report.Faults, x => x.Invariant == EconomyChecks.LRecipe && x.Record == "outside-market:gate");
        Assert.Equal(1, EconomyCommand.Report(f.Cache, new StringWriter()));
    }

    [Fact]
    public void ANullEntryInFillsIsAnLRecipeFault()
    {
        using var f = EconomyFixture.Healthy();
        f.EditRecipe("commodity:plate", r => r.Fills.Add(null));
        Assert.Contains(EconomyChecks.Check(f.Cache).Faults, x => x.Invariant == EconomyChecks.LRecipe && x.Record == "recipe:commodity:plate" && x.Message.Contains("Fills"));
    }

    [Fact]
    public void FixingOneFaultOnARecordDoesNotHideANewOneThere()
    {
        using var f = EconomyFixture.Healthy();
        f.EditRecipe("commodity:plate", r => r.Upkeep.Add(null));
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var key = RecipeData.KeyOf(new CultRecordKey(EconomyFixture.Key("plate")));
        var code = Author(f, true, out var output, a => { var r = a.Catalog.Get<RecipeData>(key); r.Upkeep.RemoveAll(x => x == null); r.Cycle = 0; a.Put(r, key); });
        Assert.Equal(1, code);
        Assert.Contains("Cycle is not a finite number above zero", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    private static EconomyFixture HeldLoop()
    {
        var f = EconomyFixture.Healthy();
        f.Good("loopa", inputs: new[] { ("loopb", 1) });
        f.Good("loopb", inputs: new[] { ("loopa", 2) });
        return f;
    }

    [Theory]
    [InlineData("worsen")]
    [InlineData("move")]
    [InlineData("move-lower")]
    [InlineData("grow")]
    [InlineData("disjoint")]
    public void AHeldUnproductiveCatalogRefusesABatchThatWorsensMovesOrGrowsTheCycle(string how)
    {
        using var f = HeldLoop();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        Action<EconomyAuthor> batch = how switch
        {
            "worsen" => a => Good(f, a, "loopb", inputs: new[] { ("loopa", 50) }),
            "move" => a => { Good(f, a, "loopb"); Good(f, a, "loopz", inputs: new[] { ("loopa", 9) }); Good(f, a, "loopa", inputs: new[] { ("loopz", 1) }); },
            "move-lower" => a => { Good(f, a, "loopb"); Good(f, a, "loopz", inputs: new[] { ("loopa", 1) }); Good(f, a, "loopa", inputs: new[] { ("loopz", 1) }); },
            "grow" => a => { Good(f, a, "loopy", inputs: new[] { ("loopa", 7) }); Good(f, a, "loopa", inputs: new[] { ("loopb", 1), ("loopy", 7) }); },
            _ => a => { Good(f, a, "loopc", inputs: new[] { ("loopd", 5) }); Good(f, a, "loopd", inputs: new[] { ("loopc", 5) }); }
        };
        var code = Author(f, true, out var output, batch);
        Assert.Equal(1, code);
        Assert.Contains("L-productive", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void AHeldUnproductiveCatalogStillAppliesABatchThatLeavesTheCycleAlone()
    {
        using var f = HeldLoop();
        var code = Author(f, true, out var output, a => Good(f, a, "coal"));
        Assert.Equal(0, code);
        Assert.Contains("Wrote 2 records", output);
    }

    private static void Seed(EconomyFixture f, string id)
    {
        var key = new CultRecordKey(EconomyFixture.Key(id));
        f.Cache.Upsert(typeof(SimpleCommodityData), new SimpleCommodityData { Name = id }, key);
        f.Cache.Upsert(typeof(RecipeData), new RecipeData { Output = EconomyFixture.Item(key.Value), StationKind = f.Mill, LoreHook = "a good", Inputs = new List<CommodityCount>() }, RecipeData.KeyOf(key));
    }

    [Fact]
    public void RemovingACommodityAndItsRecipeIsListedAndApplies()
    {
        using var f = EconomyFixture.Healthy();
        Seed(f, "tar");
        var key = new CultRecordKey(EconomyFixture.Key("tar"));
        var code = Author(f, true, out var output, a => { a.Catalog.Remove(RecipeData.KeyOf(key)); a.Catalog.Remove(key); });
        Assert.Equal(0, code);
        Assert.Contains("removed commodity:tar", output);
        Assert.Contains("removed recipe:commodity:tar", output);
        Assert.Contains("2 removed", output);
        Assert.Contains("Wrote 2 records", output);
        Assert.DoesNotContain("Nothing to write", output);
        using var after = Read(f);
        Assert.Null(after.Get<RecipeData>(RecipeData.KeyOf(key)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemovingARecipeAloneIsARefusedLRecipeFault(bool alongsideAnAddition)
    {
        using var f = EconomyFixture.Healthy();
        Seed(f, "tar");
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var key = new CultRecordKey(EconomyFixture.Key("tar"));
        var code = Author(f, true, out var output, a => { a.Catalog.Remove(RecipeData.KeyOf(key)); if (alongsideAnAddition) Good(f, a, "coal"); });
        Assert.Equal(1, code);
        Assert.Contains("L-recipe commodity:tar", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void ANewCommodityWithNoRecipeYetIsStillPending()
    {
        using var f = EconomyFixture.Healthy();
        var code = Author(f, true, out var output, a => a.Put(new SimpleCommodityData { Name = "coal" }, new CultRecordKey(EconomyFixture.Key("coal"))));
        Assert.Equal(0, code);
        Assert.Contains("Wrote 1 records", output);
    }

    [Fact]
    public void AReadOnlyCatalogIsARefusalOnEveryPlatformAndStaysIntact()
    {
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        File.SetAttributes(f.CatalogPath, FileAttributes.ReadOnly);
        try
        {
            var code = Author(f, true, out var output, a => Good(f, a, "coal"));
            Assert.Equal(1, code);
            Assert.Contains("read-only", output);
            Assert.DoesNotContain("   at ", output);
            Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
        }
        finally { File.SetAttributes(f.CatalogPath, FileAttributes.Normal); }
    }

    [Fact]
    public void ACatalogHeldOpenWithoutDeleteSharingIsANamedRefusalAndStaysIntact()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var bytes = File.ReadAllBytes(f.CatalogPath);
        using (new FileStream(f.CatalogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var code = Author(f, true, out var output, a => Good(f, a, "coal"));
            Assert.Equal(1, code);
            Assert.Contains("close Unity", output);
            Assert.DoesNotContain("   at ", output);
        }
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.CatalogPath), "*.economy-*"));
    }

    [Fact]
    public void TheSwapKeepsTheCatalogsFileMode()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f = EconomyFixture.Healthy();
        Flush(f);
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
        File.SetUnixFileMode(f.CatalogPath, mode);
        Assert.Equal(0, Author(f, true, out _, a => Good(f, a, "coal")));
        Assert.Equal(mode, File.GetUnixFileMode(f.CatalogPath));
    }
}
