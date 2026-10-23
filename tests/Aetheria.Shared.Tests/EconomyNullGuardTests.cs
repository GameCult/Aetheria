using System;
using System.Linq;
using Xunit;

// A recipe read from disk can hold null lists. The checks refuse it as an L-recipe fault naming the record; they never throw
// (finding null-fills-throws).
public sealed class EconomyNullGuardTests
{
    private static void Null(EconomyFixture f, string list) =>
        f.EditRecipe("commodity:plate", r =>
        {
            if (list == "Fills") r.Fills = null;
            if (list == "Inputs") r.Inputs = null;
            if (list == "Upkeep") r.Upkeep = null;
        });

    [Theory]
    [InlineData("Fills")]
    [InlineData("Inputs")]
    [InlineData("Upkeep")]
    public void ANullListIsAnLRecipeFaultNotAThrow(string list)
    {
        using var f = EconomyFixture.Healthy();
        Null(f, list);
        var report = EconomyChecks.Check(f.Cache);
        Assert.Contains(report.Faults, x => x.Invariant == EconomyChecks.LRecipe && x.Record == "recipe:commodity:plate" && x.Message.Contains(list));
    }

    [Theory]
    [InlineData("Fills")]
    [InlineData("Inputs")]
    [InlineData("Upkeep")]
    public void EveryRuleSurvivesANullList(string list)
    {
        using var f = EconomyFixture.Healthy();
        Null(f, list);
        EconomyChecks.Recipe(f.Cache);
        EconomyChecks.Lore(f.Cache);
        EconomyChecks.SourceSink(f.Cache);
        EconomyChecks.Productive(f.Cache);
        EconomyChecks.ConsumerDemand(f.Cache);
        Assert.NotEmpty(EconomyChecks.Deep(f.Cache));
    }

    [Fact]
    public void ANullEntryInAListIsAnLRecipeFault()
    {
        using var f = EconomyFixture.Healthy();
        f.EditRecipe("commodity:plate", r => r.Inputs.Add(null));
        var report = EconomyChecks.Check(f.Cache);
        Assert.Contains(report.Faults, x => x.Invariant == EconomyChecks.LRecipe && x.Record == "recipe:commodity:plate" && x.Message.Contains("Inputs"));
    }
}
