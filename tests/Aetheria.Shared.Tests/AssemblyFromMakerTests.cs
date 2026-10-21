using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Assembly is the maker's (aetheria-release, cut assembly-from-maker; rulings quality-is-assembly-over-supply-chain,
// quality-lean-assembly-hard-ceiling): a branded lot's own quality is drawn from its manufacturer's assembly distribution,
// and every role a stat reads is min(part, assembly). Lots here come from CreateLot(product), the production mint.
public sealed class AssemblyFromMakerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-assembly-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly CultRecordRef<CraftedItemData> _lens;

    public AssemblyFromMakerTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true);
        var gear = _cache.Upsert(new GearData
        {
            Name = "Lensed", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Roles = { new ItemRole { Name = "lens" } }
        });
        _lens = new CultRecordRef<CraftedItemData>(gear.Key);
        _items = new ItemManager(1u, _cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private FactionProductData Product(string name, Faction maker, float partMean, float partDeviation = 0)
    {
        var makerRef = maker == null ? default : _cache.Upsert(maker);
        _cache.Upsert(new FactionProductData
        {
            Name = name, Design = _lens, Manufacturer = makerRef,
            Roles = { new ProductRole { Role = "lens", Mean = partMean, StandardDeviation = partDeviation } }
        });
        return _cache.GetAll<FactionProductData>().Single(p => p.Name == name);
    }

    private static AssemblyDistribution A(float mean, float deviation) => new AssemblyDistribution { Mean = mean, StandardDeviation = deviation };

    private float[] Mint(FactionProductData product, int count) =>
        Enumerable.Range(0, count).Select(_ => _items.Lots[_items.CreateLot(product)].Quality).ToArray();

    // Two makers, assembly .3 and .8: each lot's own quality follows its manufacturer, not a product-blind roll.
    [Fact]
    public void LotQualityFollowsMaker()
    {
        var low = Product("Low", new Faction { Name = "Low Maker", Assembly = A(.3f, .02f) }, .5f);
        var high = Product("High", new Faction { Name = "High Maker", Assembly = A(.8f, .02f) }, .5f);
        var lows = Mint(low, 300);
        var highs = Mint(high, 300);
        Assert.InRange(lows.Average(), .27f, .33f);
        Assert.InRange(highs.Average(), .77f, .83f);
        Assert.True(lows.Max() < highs.Min(), "a lot read the other maker's band");
    }

    // Quality control is the maker's too: the same mean, a wide and a tight deviation, and the spread of their lots follows.
    [Fact]
    public void LotSpreadFollowsMakerDeviation()
    {
        var wide = Mint(Product("Wide", new Faction { Name = "Wide Maker", Assembly = A(.5f, .2f) }, .5f), 400);
        var tight = Mint(Product("Tight", new Faction { Name = "Tight Maker", Assembly = A(.5f, .02f) }, .5f), 400);
        double Spread(float[] lots) => Math.Sqrt(lots.Select(q => Math.Pow(q - lots.Average(), 2)).Average());
        Assert.InRange(Spread(wide), .15, .25);
        Assert.InRange(Spread(tight), .01, .04);
    }

    // A maker the catalog cannot resolve, and a maker whose Assembly is not authored, assemble at AssemblyDistribution's defaults.
    [Fact]
    public void UnauthoredAssemblyAssemblesAtDefaults()
    {
        var defaults = new AssemblyDistribution();
        Assert.InRange(Mint(Product("Orphan", null, .5f), 400).Average(), defaults.Mean - .03f, defaults.Mean + .03f);
        Assert.InRange(Mint(Product("Blank", new Faction { Name = "Blank Maker" }, .5f), 400).Average(), defaults.Mean - .03f, defaults.Mean + .03f);
    }

    // A line's own final test (maker-design-tiers-allowed): a product carrying an Assembly mints at it, and a sibling product
    // of the same maker without one reads the maker's.
    [Fact]
    public void ProductAssemblyOverridesMaker()
    {
        var maker = new Faction { Name = "Binner", Assembly = A(.3f, .02f) };
        var plain = Product("Plain", maker, .5f);
        var premium = Product("Premium", maker, .5f);
        premium.Assembly = A(.85f, .02f);
        Assert.InRange(Mint(premium, 300).Average(), .82f, .88f);
        Assert.InRange(Mint(plain, 300).Average(), .27f, .33f);
    }

    // A stated quality is every lot's and every fill's, clamped, and draws nothing from the item stream.
    [Fact]
    public void StatedQualityDrawsNothing()
    {
        var product = Product("Staged", new Faction { Name = "Staged Maker", Assembly = A(.3f, .2f) }, .5f, .2f);
        var before = _items.Random;
        var lot = _items.Lots[_items.CreateLot(product, .6f)];
        Assert.Equal(.6f, lot.Quality, 3);
        Assert.Equal(.6f, lot.Roles.Single().Quality, 3);
        Assert.Equal(before.NextFloat(), _items.Random.NextFloat());
        Assert.Equal(1f, _items.Lots[_items.CreateLot(product, 7f)].Quality, 3);
        Assert.Equal(.01f, _items.Lots[_items.CreateLot(product, -1f)].Roles.Single().Quality, 3);
    }

    // A drawn assembly stays a quality: a maker authored past either end mints lots at .01 or 1.
    [Fact]
    public void DrawnAssemblyIsClamped()
    {
        Assert.All(Mint(Product("Over", new Faction { Name = "Over Maker", Assembly = A(1.5f, 0) }, .5f), 20), q => Assert.Equal(1f, q, 3));
        Assert.All(Mint(Product("Under", new Faction { Name = "Under Maker", Assembly = A(-.5f, 0) }, .5f), 20), q => Assert.Equal(.01f, q, 3));
    }

    // A staged fit's slot is built by Loadouts.Resolve: a product at the stated quality, a design no maker sells at the stated
    // quality instead of UnbrandedQuality, and both at their own draws without one.
    [Fact]
    public void ResolveMintsAtTheStatedQuality()
    {
        var failures = new List<string>();
        Lot Built(CultRecordRef<FactionProductData> product, float? quality) =>
            _items.GetLot(Loadouts.Resolve(_items, _lens, product, _ => null, "lens", failures, quality)());
        var unbranded = Built(default, .6f);
        Assert.Equal(.6f, unbranded.Quality, 3);
        Assert.Equal(Loadouts.UnbrandedQuality, Built(default, null).Quality, 3);

        var made = Product("Made", new Faction { Name = "Made Maker", Assembly = A(.3f, .2f) }, .5f, .2f);
        var branded = Built(_cache.RefOf(made), .6f);
        Assert.Equal(.6f, branded.Quality, 3);
        Assert.Equal(.6f, branded.Roles.Single().Quality, 3);
        Assert.Empty(failures);
    }

    // The ceiling at the mint: a part drawn at .9 in an assembly of .4 reads .4; a part at .2 in the same assembly reads .2.
    [Fact]
    public void RoleCappedByAssembly()
    {
        var maker = new Faction { Name = "Capped", Assembly = A(.4f, 0) };
        var over = _items.Lots[_items.CreateLot(Product("Over", maker, .9f))];
        var under = _items.Lots[_items.CreateLot(Product("Under", maker, .2f))];
        Assert.Equal(.4f, over.Quality, 3);
        Assert.Equal(.4f, over.QualityForRole("lens"), 3);
        Assert.Equal(.2f, under.QualityForRole("lens"), 3);
        // The raw part is kept for disassembly (named-components-after-demo).
        Assert.Equal(.9f, over.Roles.Single().Quality, 3);
    }

    // The ceiling is the assembly itself at every height and to the last digit: a poor assembler (below any floor) still caps a
    // good part at its own quality, a part just above the assembly reads the assembly and not its own, and a part at or below it
    // keeps its own.
    [Fact]
    public void RoleCeilingHasNoFloorAndNoToleranceBand()
    {
        float Role(float assembly, float part) =>
            _items.Lots[_items.CreateLot(Product($"A{assembly}P{part}", new Faction { Name = $"Maker {assembly}/{part}", Assembly = A(assembly, 0) }, part))].QualityForRole("lens");
        Assert.Equal(.1f, Role(.1f, .9f), 3);
        Assert.Equal(.02f, Role(.02f, .5f), 3);
        Assert.Equal(.4f, Role(.4f, .41f), 3);
        Assert.Equal(.4f, Role(.4f, .43f), 3);
        Assert.Equal(.4f, Role(.4f, .4f), 3);
        Assert.Equal(.39f, Role(.4f, .39f), 3);
    }

    // The line's own final test replaces the maker's whole distribution, spread included: a maker of no spread and a product
    // of a wide one mint a wide spread, and the reverse mints none.
    [Fact]
    public void ProductAssemblyOverridesMakersSpreadToo()
    {
        var steady = new Faction { Name = "Steady", Assembly = A(.5f, 0) };
        var wild = Product("Wild", steady, .5f);
        wild.Assembly = A(.5f, .2f);
        var wildLots = Mint(wild, 400);
        Assert.InRange(Math.Sqrt(wildLots.Select(q => Math.Pow(q - wildLots.Average(), 2)).Average()), .15, .25);

        var loose = new Faction { Name = "Loose", Assembly = A(.5f, .2f) };
        var tight = Product("Tight", loose, .5f);
        tight.Assembly = A(.5f, 0);
        Assert.All(Mint(tight, 50), q => Assert.Equal(.5f, q, 3));
    }

    // The fallbacks read the item's effective quality: an unnamed role and an unfilled role on a lot whose fills sit below and
    // above its assembly (lens .3 -> .3, frame .9 -> .6, mean .45), and a lot with no roles reading its assembly. A found
    // fill still reads its own capped part.
    [Fact]
    public void FallbacksReadEffectiveQuality()
    {
        var lot = new Lot
        {
            Quality = .6f,
            Roles = new List<RoleFill> { new RoleFill { Role = "lens", Quality = .3f }, new RoleFill { Role = "frame", Quality = .9f } }
        };
        Assert.Equal(.45f, lot.QualityForRole(null), 3);
        Assert.Equal(.45f, lot.QualityForRole(""), 3);
        Assert.Equal(.45f, lot.QualityForRole("ghost"), 3);
        Assert.Equal(.3f, lot.QualityForRole("lens"), 3);
        Assert.Equal(.4f, new Lot { Quality = .4f }.QualityForRole("lens"), 3);
        Assert.Equal(.4f, new Lot { Quality = .4f, Roles = new List<RoleFill>() }.QualityForRole("lens"), 3);
    }

    // The effective quality is the mean over the fills of min(part, assembly): assembly .6 with parts .9, .3, .5 reads
    // (.6 + .3 + .5) / 3, not the raw mean .5667, the min .3, the assembly .6 or the max. A lot with no fills reads its assembly.
    [Fact]
    public void EffectiveQualityIsTheMeanOfCappedFills()
    {
        var lot = new Lot
        {
            Quality = .6f,
            Roles = new List<RoleFill>
            {
                new RoleFill { Role = "a", Quality = .9f }, new RoleFill { Role = "b", Quality = .3f }, new RoleFill { Role = "c", Quality = .5f }
            }
        };
        Assert.Equal(.4667f, lot.EffectiveQuality(), 3);
        Assert.Equal(.6f, new Lot { Quality = .6f }.EffectiveQuality(), 3);
        Assert.Equal(.6f, new Lot { Quality = .6f, Roles = new List<RoleFill>() }.EffectiveQuality(), 3);
    }

    // A Quality term on a role with part .9 and assembly .4 evaluates as if quality were .4, on a real minted instance.
    [Fact]
    public void QualityTermReadsCeiling()
    {
        var maker = new Faction { Name = "Capped", Assembly = A(.4f, 0) };
        var instance = (EquippableItem) _items.CreateInstance(_items.CreateLot(Product("Over", maker, .9f)));
        var stat = new PerformanceStat { Min = 0, Max = 1, Terms = { new StatTerm { Source = StatSource.Quality, Exponent = 1, Role = "lens" } } };
        Assert.Equal(.4f, _items.Evaluate(stat, instance), 3);
    }
}
