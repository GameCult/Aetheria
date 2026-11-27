using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Made commodities are priced from their chain at the raw wage (EconomyBalance.Prices); economy author writes the price and L-price
// refuses drift. Fixture chains are hand-solved.
public sealed class DerivedPriceTests
{
    private sealed class Lambda : EconomyBatch
    {
        private readonly Action<EconomyAuthor> _author;
        public Lambda(Action<EconomyAuthor> author) => _author = author;
        public override void Author(EconomyAuthor author) => _author(author);
    }

    private static void Price(EconomyFixture f, string key, int price) =>
        f.EditItem<ItemData>(key, item => item.Price = price);

    // ore (raw, labour 20, price 50), salt (raw, labour 10, price 100) -> raw wages 2.5 and 10, wage 6.25;
    // bar (2 per cycle from 3 ore, labour 10), part (2 bars, labour 4). Authored made prices are deliberately wrong.
    private static EconomyFixture Chain(int scale = 1)
    {
        var f = new EconomyFixture();
        f.Good("ore", workforce: 2, cycle: 10);
        f.Good("salt", workforce: 1, cycle: 10);
        f.Good("bar", inputs: new[] { ("ore", 3) }, outputCount: 2, workforce: 2, cycle: 5);
        f.Good("part", inputs: new[] { ("bar", 2) }, workforce: 1, cycle: 4);
        Price(f, "commodity:ore", 50 * scale);
        Price(f, "commodity:salt", 100 * scale);
        Price(f, "commodity:bar", 1);
        Price(f, "commodity:part", 1);
        return f;
    }

    private static int Author(EconomyFixture f, bool apply, out string output, params Action<EconomyAuthor>[] batches)
    {
        f.Cache.FlushAsync().GetAwaiter().GetResult();
        var writer = new StringWriter();
        var code = EconomyCommand.Author(f.CatalogPath, batches.Select(b => (EconomyBatch) new Lambda(b)).ToList(), apply, writer, TestCatalog.Registry());
        output = writer.ToString();
        return code;
    }

    private static CultCache Read(EconomyFixture f) => AetheriaStores.Open(f.CatalogPath, registry: TestCatalog.Registry());

    [Fact]
    public void AMadeGoodIsPricedAtItsInputsPlusTheRawWage()
    {
        using var f = Chain();
        Assert.Equal(6.25, EconomyBalance.Wage(f.Cache), 9);
        var prices = EconomyBalance.Prices(f.Cache, out _);
        // bar = (3 x 50 + 6.25 x 10) / 2; the ore stays at its authored 50, not its labour cost 125.
        Assert.Equal(106.25, prices["commodity:bar"], 9);
        Assert.Equal(2 * 106.25 + 6.25 * 4, prices["commodity:part"], 9);
        Assert.DoesNotContain("commodity:ore", prices.Keys);
    }

    [Fact]
    public void TheWageIsTheMedianOfRawLinks()
    {
        using var f = new EconomyFixture();
        f.Good("a", workforce: 1, cycle: 1);
        f.Good("b", workforce: 1, cycle: 1);
        f.Good("c", workforce: 1, cycle: 1);
        f.Good("bar", inputs: new[] { ("a", 1) }, workforce: 1, cycle: 1);
        Price(f, "commodity:a", 1);
        Price(f, "commodity:b", 2);
        Price(f, "commodity:c", 10);
        Price(f, "commodity:bar", 100000);
        Assert.Equal(2, EconomyBalance.Wage(f.Cache), 9);
    }

    [Fact]
    public void RawRecipelessAndDesignPricesAreNotRepriced()
    {
        using var f = Chain();
        f.Cache.Upsert(typeof(SimpleCommodityData), new SimpleCommodityData { Name = "tin", Price = 7 }, new CultRecordKey("commodity:tin"));
        var gear = f.Gear("laser", new[] { ("Plate", 1) });
        f.Good("plate", inputs: new[] { ("bar", 1) }, fills: new[] { "Plate" }, workforce: 1, cycle: 1);
        f.EditItem<GearData>(gear, g => g.Price = 12345);
        Assert.Equal(0, Author(f, true, out _));
        using var after = Read(f);
        Assert.Equal(50, after.Get<ItemData>(new CultRecordKey("commodity:ore")).Price);
        Assert.Equal(100, after.Get<ItemData>(new CultRecordKey("commodity:salt")).Price);
        Assert.Equal(7, after.Get<ItemData>(new CultRecordKey("commodity:tin")).Price);
        Assert.Equal(12345, after.Get<ItemData>(new CultRecordKey(gear)).Price);
        Assert.Equal(106, after.Get<ItemData>(new CultRecordKey("commodity:bar")).Price);
    }

    [Fact]
    public void AuthorRepricesAfterTheBatchesAndConverges()
    {
        using var f = Chain();
        // The batch sets a made good's price to 1; author runs after it and lists bar as changed, to the derived value.
        Assert.Equal(0, Author(f, false, out var dry, a => a.SetPrice("commodity:bar", 1)));
        Assert.Contains("changed commodity:bar", dry);
        Assert.Contains("Dry run", dry);
        Assert.Equal(0, Author(f, true, out _, a => a.SetPrice("commodity:bar", 1)));
        using (var after = Read(f))
        {
            Assert.Equal(106, after.Get<ItemData>(new CultRecordKey("commodity:bar")).Price);
            Assert.Equal(238, after.Get<ItemData>(new CultRecordKey("commodity:part")).Price);
        }
        f.Reopen();
        Assert.Equal(0, Author(f, true, out var again));
        Assert.Contains("Nothing to write", again);
    }

    [Fact]
    public void AnAuthorBatchCannotKeepAMadePriceItChose()
    {
        using var f = Chain();
        Assert.Equal(0, Author(f, true, out _, a => a.SetPrice("commodity:part", 999999)));
        using var after = Read(f);
        Assert.Equal(238, after.Get<ItemData>(new CultRecordKey("commodity:part")).Price);
    }

    [Fact]
    public void AStaleMadePriceIsAnLPriceFault()
    {
        using var f = Chain();
        var stale = EconomyChecks.PriceCheck(f.Cache);
        Assert.Equal(new[] { "commodity:bar", "commodity:part" }, stale.Select(x => x.Record).ToArray());
        Assert.All(stale, x => Assert.Equal(EconomyChecks.LPrice, x.Invariant));
        f.Settle();
        Assert.Empty(EconomyChecks.PriceCheck(f.Cache));
        // 106.25 rounds to 106 and 237.5 to 238 (half away from zero): a comparison without rounding would fault both.
        Assert.Equal(106, f.Cache.Get<ItemData>(new CultRecordKey("commodity:bar")).Price);
        Assert.Equal(238, f.Cache.Get<ItemData>(new CultRecordKey("commodity:part")).Price);
    }

    [Fact]
    public void ScalingRawPricesScalesMadePrices()
    {
        using var one = Chain();
        using var hundred = Chain(100);
        var a = EconomyBalance.Prices(one.Cache, out _);
        var b = EconomyBalance.Prices(hundred.Cache, out _);
        foreach (var (key, price) in a) Assert.Equal(price * 100, b[key], 7);
    }

    [Fact]
    public void ShippedCatalogHasNoPriceFault()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        Assert.Empty(EconomyChecks.PriceCheck(cache));
        var prices = EconomyBalance.Prices(cache, out _);
        Assert.NotEmpty(prices);
        Assert.All(prices.Keys, key => Assert.True(cache.Get<ItemData>(new CultRecordKey(key)).Price >= 1));
        foreach (var name in new[] { "Water", "Methane", "Diamond" })
            Assert.True(cache.GetAll<SimpleCommodityData>().Concat<ItemData>(cache.GetAll<CompoundCommodityData>()).Single(i => i.Name == name).Price > 0, name);
    }

    // ore (raw, price 50) feeds a, a feeds b, b feeds a again: the cycle's radius is sqrt(ratio), so 1 and 1.0005 are the edge cases.
    private static EconomyFixture Cycle(int bPerA, int perBatch)
    {
        var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        Price(f, "commodity:ore", 50);
        f.Good("a", inputs: new[] { ("ore", 1), ("b", 1) });
        f.Good("b", inputs: new[] { ("a", bPerA) }, outputCount: perBatch);
        return f;
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(1001, 1000)]
    [InlineData(1, 1)]
    public void AChainThatDoesNotSettleHasNoPriceAndAuthorRefusesIt(int bPerA, int perBatch)
    {
        using var f = Cycle(bPerA, perBatch);
        EconomyBalance.Prices(f.Cache, out var unsettled);
        Assert.Equal(new[] { "commodity:a", "commodity:b" }, unsettled);
        var fault = Assert.Single(EconomyChecks.Check(f.Cache).Faults, x => x.Invariant == EconomyChecks.LPrice);
        Assert.Contains("a, b", fault.Message);
        f.Cache.FlushAsync().GetAwaiter().GetResult();
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var code = Author(f, true, out var output);
        Assert.Equal(1, code);
        Assert.Contains("No price settles", output);
        Assert.Contains("a, b", output);
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }

    [Fact]
    public void ACycleThatShrinksSettlesAndIsPriced()
    {
        using var f = Cycle(1, 2);
        var prices = EconomyBalance.Prices(f.Cache, out var unsettled);
        Assert.Empty(unsettled);
        // a = 50 + b, b = a / 2: a = 100, b = 50.
        Assert.Equal(100, prices["commodity:a"], 6);
        Assert.Equal(50, prices["commodity:b"], 6);
    }

    [Fact]
    public void ADriftedMadePriceIsInTheReportAndAnIntroducedOneRefusesAuthor()
    {
        using var f = Chain();
        Assert.Equal(2, EconomyChecks.Check(f.Cache).Faults.Count(x => x.Invariant == EconomyChecks.LPrice));
        // Tool upkeep is a made good: pricing it moves the raw wage the made price reads, so the repriced catalog is itself off its derived price.
        using var g = new EconomyFixture();
        g.Good("crude", workforce: 1, cycle: 1);
        g.Good("fuel", inputs: new[] { ("crude", 1) }, workforce: 1, cycle: 1);
        g.Good("ore", workforce: 1, cycle: 1, upkeep: new[] { ("fuel", 5) });
        Price(g, "commodity:ore", 300);
        Price(g, "commodity:fuel", 1);
        Price(g, "commodity:crude", 100);
        g.Cache.FlushAsync().GetAwaiter().GetResult();
        var bytes = File.ReadAllBytes(g.CatalogPath);
        Assert.Equal(1, Author(g, true, out var output));
        Assert.Contains("L-price", output);
        Assert.Equal(bytes, File.ReadAllBytes(g.CatalogPath));
    }

    [Fact]
    public void AMadePriceIsWholeCreditsAtLeastOneRoundedHalfAwayFromZero()
    {
        Assert.Equal(1, EconomyBalance.Rounded(0.2));
        Assert.Equal(1, EconomyBalance.Rounded(0));
        Assert.Equal(3, EconomyBalance.Rounded(2.5));
        Assert.Equal(4, EconomyBalance.Rounded(3.5));
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        f.Good("dust", inputs: new[] { ("ore", 1) }, outputCount: 10, workforce: 1, cycle: 1);
        Price(f, "commodity:ore", 1);
        Assert.Equal(0.2, EconomyBalance.Prices(f.Cache, out _)["commodity:dust"], 9);
        Assert.Equal(0, Author(f, true, out _));
        using var after = Read(f);
        Assert.Equal(1, after.Get<ItemData>(new CultRecordKey("commodity:dust")).Price);
    }

    [Fact]
    public void UpkeepIsPartOfAMadeGoodsPrice()
    {
        using var f = new EconomyFixture();
        f.Good("ore", workforce: 1, cycle: 1);
        f.Good("tool", workforce: 1, cycle: 1);
        f.Good("bar", inputs: new[] { ("ore", 1) }, upkeep: new[] { ("tool", 2) }, workforce: 2, cycle: 1);
        Price(f, "commodity:ore", 10);
        Price(f, "commodity:tool", 5);
        // Raw wages 10 and 5: wage 7.5. bar = 10 + 2 x 5 + 7.5 x 2.
        Assert.Equal(35, EconomyBalance.Prices(f.Cache, out _)["commodity:bar"], 9);
    }
}
