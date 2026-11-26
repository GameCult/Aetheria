using System;
using System.Linq;
using GameCult.Caching;
using Xunit;

// The static economy balance (EconomyBalance): implied wage per chain link, prices derived from inputs and labour, the report-only
// health faults. Fixture chains are hand-solved; the shipped catalog test asserts reach, never a fault count (faults are tuning state).
public sealed class EconomyBalanceTests
{
    private static void Price(EconomyFixture f, string key, int price)
    {
        var item = f.Cache.Get<ItemData>(new CultRecordKey(key));
        item.Price = price;
        f.Cache.Upsert(item.GetType(), item, new CultRecordKey(key));
    }

    private static void Labour(EconomyFixture f, string key, int workforce, float cycle) =>
        f.EditRecipe(key, r => { r.Workforce = workforce; r.Cycle = cycle; r.OutputCount = Math.Max(1, r.OutputCount); });

    // ore (labour 20, price 50) -> bar (2 per cycle from 3 ore, labour 10, price 80) -> part (2 bars, labour 4, price 200), all times scale.
    // Wages 2.5, 1, 10: median 2.5. Derived cost at 2.5: ore 50, bar (3 x 50 + 2.5 x 10) / 2 = 87.5, part 2 x 87.5 + 2.5 x 4 = 185.
    private static EconomyFixture Chain(int scale = 1)
    {
        var f = new EconomyFixture();
        f.Good("ore", workforce: 2, cycle: 10);
        f.Good("bar", inputs: new[] { ("ore", 3) }, outputCount: 2, workforce: 2, cycle: 5);
        f.Good("part", inputs: new[] { ("bar", 2) }, workforce: 1, cycle: 4);
        Price(f, "commodity:ore", 50 * scale);
        Price(f, "commodity:bar", 80 * scale);
        Price(f, "commodity:part", 200 * scale);
        return f;
    }

    private static BalanceLink Link(BalanceReport r, string key) => r.Links.Single(l => l.Output == key);

    [Fact]
    public void ALinkSellingBelowItsInputsIsAMarginFault()
    {
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        f.Good("bar", inputs: new[] { ("ore", 2) }, workforce: 1, cycle: 1);
        Price(f, "commodity:ore", 10);
        Price(f, "commodity:bar", 5);
        var report = EconomyBalance.Balance(f.Cache);
        var fault = Assert.Single(report.Faults, x => x.Invariant == EconomyBalance.HMargin);
        Assert.Equal("commodity:bar", fault.Record);
        Assert.Equal(-15, Link(report, "commodity:bar").Wage.Value, 9);
    }

    [Fact]
    public void TheCheaperFillerSetsTheDerivedCost()
    {
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        // The dearer filler sorts first, so a first-filler pick and a max both read it.
        f.Good("a-dear", inputs: new[] { ("ore", 3) }, fills: new[] { "Plate" }, workforce: 1, cycle: 1);
        f.Good("z-cheap", inputs: new[] { ("ore", 1) }, fills: new[] { "Plate" }, workforce: 1, cycle: 1);
        var gear = f.Gear("laser", new[] { ("Plate", 1) });
        Labour(f, gear, 1, 1);
        Price(f, "commodity:ore", 10);
        Price(f, "commodity:a-dear", 40);
        Price(f, "commodity:z-cheap", 30);
        f.EditItem<GearData>(gear, g => g.Price = 100);
        // Wages 10, 10, 20, 70: median 15. C: ore 15, a-dear 60, z-cheap 30, laser min(60, 30) + 15 = 45.
        var report = EconomyBalance.Balance(f.Cache);
        Assert.Equal(15, report.WageReference, 9);
        Assert.Equal(45, Link(report, gear).Cost, 9);
    }

    [Fact]
    public void DerivedCostMatchesTheHandSolvedChain()
    {
        using var f = Chain();
        var report = EconomyBalance.Balance(f.Cache);
        Assert.Equal(2.5, report.WageReference, 9);
        Assert.Equal(50, Link(report, "commodity:ore").Cost, 9);
        Assert.Equal(87.5, Link(report, "commodity:bar").Cost, 9);
        Assert.Equal(185, Link(report, "commodity:part").Cost, 9);
    }

    [Fact]
    public void ScalingEveryPriceScalesTheWageAndNoFault()
    {
        using var one = Chain();
        using var hundred = Chain(100);
        var a = EconomyBalance.Balance(one.Cache);
        var b = EconomyBalance.Balance(hundred.Cache);
        Assert.NotEmpty(a.Faults);
        Assert.Equal(a.WageReference * 100, b.WageReference, 7);
        Assert.Equal(a.Faults.Select(x => x.Invariant + x.Record), b.Faults.Select(x => x.Invariant + x.Record));
        foreach (var link in a.Links) Assert.Equal(link.WageRatio.Value, Link(b, link.Output).WageRatio.Value, 9);
    }

    [Fact]
    public void AnUnpricedLinkHasNoWage()
    {
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        f.Good("bar", inputs: new[] { ("ore", 1) }, workforce: 1, cycle: 1);
        f.Good("tin", workforce: 1, cycle: 1);
        Price(f, "commodity:bar", 80);
        Price(f, "commodity:tin", 7);
        var report = EconomyBalance.Balance(f.Cache);
        Assert.Null(Link(report, "commodity:ore").Wage);
        Assert.Null(Link(report, "commodity:bar").Wage);
        Assert.Equal(7, report.WageReference, 9);
        Assert.Empty(report.Faults);
    }

    [Fact]
    public void AnUnpricedLinkNamesWhetherItsOutputOrAnInputIsFree()
    {
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        f.Good("bar", inputs: new[] { ("ore", 1) }, workforce: 1, cycle: 1);
        f.Good("tin", workforce: 1, cycle: 1);
        Price(f, "commodity:bar", 80);
        Price(f, "commodity:tin", 7);
        var report = EconomyBalance.Balance(f.Cache);
        Assert.Equal("output", Link(report, "commodity:ore").Unpriced);
        Assert.Equal("input", Link(report, "commodity:bar").Unpriced);
        Assert.Null(Link(report, "commodity:tin").Unpriced);
    }

    [Fact]
    public void AFreeFillerLeavesTheRoleUnpricedEvenWithAPricedOneBesideIt()
    {
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        f.Good("a-free", inputs: new[] { ("ore", 1) }, fills: new[] { "Plate" }, workforce: 1, cycle: 1);
        f.Good("z-priced", inputs: new[] { ("ore", 1) }, fills: new[] { "Plate" }, workforce: 1, cycle: 1);
        var gear = f.Gear("laser", new[] { ("Plate", 1) });
        Labour(f, gear, 1, 1);
        Price(f, "commodity:ore", 10);
        Price(f, "commodity:z-priced", 30);
        f.EditItem<GearData>(gear, g => g.Price = 100);
        var link = Link(EconomyBalance.Balance(f.Cache), gear);
        Assert.Null(link.Wage);
        Assert.Equal("input", link.Unpriced);
    }

    [Fact]
    public void TwoRecipesForOneOutputTakeTheCheaperDerivedCostInEitherOrder()
    {
        foreach (var cheapFirst in new[] { true, false })
        {
            using var f = new EconomyFixture();
            f.Good("ore", workforce: 1, cycle: 1);
            f.Good("bar", inputs: new[] { ("ore", cheapFirst ? 1 : 3) }, workforce: 1, cycle: 1);
            f.Good("tin", workforce: 1, cycle: 1);
            Price(f, "commodity:ore", 10);
            Price(f, "commodity:bar", 80);
            Price(f, "commodity:tin", 7);
            // A second recipe for bar under its own key, written after the first, from a different input count.
            var first = f.Cache.Get<RecipeData>(RecipeData.KeyOf(new CultRecordKey("commodity:bar")));
            f.Cache.Upsert(typeof(RecipeData), new RecipeData
            {
                Output = first.Output, OutputCount = 1, Cycle = 1, Workforce = 1, StationKind = first.StationKind, LoreHook = "a good",
                Inputs = new System.Collections.Generic.List<CommodityCount> { new CommodityCount { Commodity = EconomyFixture.Item("commodity:ore"), Count = cheapFirst ? 3 : 1 } }
            }, new CultRecordKey("recipe:commodity:bar-alt"));
            var report = EconomyBalance.Balance(f.Cache);
            // Cost of one bar from one ore: ore cost plus the wage reference for one worker-second.
            var ore = Link(report, "commodity:ore").Cost;
            var cheap = ore + report.WageReference;
            Assert.All(report.Links.Where(l => l.Output == "commodity:bar"), l => Assert.Equal(cheap, l.Cost, 9));
        }
    }

    [Fact]
    public void ShippedCatalogBalanceListsTheMaterializedChain()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var report = EconomyBalance.Balance(cache);
        var expected = cache.GetAll<RecipeData>().Count(r => cache.Get(r.Output) != null && r.OutputCount > 0 && float.IsFinite(r.Cycle));
        Assert.Equal(expected, report.Links.Count);
        Assert.Contains(report.Links, l => l.Output == "commodity:steel");
        var guns = cache.GetAll<RecipeData>().Where(r => Recipes.IsRoleRecipe(cache, r) && !Recipes.IsCommodity(cache.Get(r.Output))).Select(r => r.Output.Key.Value).ToArray();
        Assert.True(guns.Length >= 9);
        Assert.All(guns, g => Assert.Contains(report.Links, l => l.Output == g));
    }
}
