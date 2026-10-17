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
        _items = new ItemManager(_cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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

    private float[] Mint(FactionProductData product, int count) =>
        Enumerable.Range(0, count).Select(_ => _items.Lots[_items.CreateLot(product)].Quality).ToArray();

    // Two makers, assembly .3 and .8: each lot's own quality follows its manufacturer, not a product-blind roll.
    [Fact]
    public void LotQualityFollowsMaker()
    {
        var low = Product("Low", new Faction { Name = "Low Maker", AssemblyMean = .3f, AssemblyDeviation = .02f }, .5f);
        var high = Product("High", new Faction { Name = "High Maker", AssemblyMean = .8f, AssemblyDeviation = .02f }, .5f);
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
        var wide = Mint(Product("Wide", new Faction { Name = "Wide Maker", AssemblyMean = .5f, AssemblyDeviation = .2f }, .5f), 400);
        var tight = Mint(Product("Tight", new Faction { Name = "Tight Maker", AssemblyMean = .5f, AssemblyDeviation = .02f }, .5f), 400);
        double Spread(float[] lots) => Math.Sqrt(lots.Select(q => Math.Pow(q - lots.Average(), 2)).Average());
        Assert.InRange(Spread(wide), .15, .25);
        Assert.InRange(Spread(tight), .01, .04);
    }

    // A maker the catalog cannot resolve assembles at a new Faction's defaults.
    [Fact]
    public void UnresolvedMakerAssemblesAtDefaults()
    {
        var orphan = Product("Orphan", null, .5f);
        var defaults = new Faction();
        Assert.InRange(Mint(orphan, 400).Average(), defaults.AssemblyMean - .03f, defaults.AssemblyMean + .03f);
    }

    // The ceiling at the mint: a part drawn at .9 in an assembly of .4 reads .4; a part at .2 in the same assembly reads .2.
    [Fact]
    public void RoleCappedByAssembly()
    {
        var maker = new Faction { Name = "Capped", AssemblyMean = .4f, AssemblyDeviation = 0 };
        var over = _items.Lots[_items.CreateLot(Product("Over", maker, .9f))];
        var under = _items.Lots[_items.CreateLot(Product("Under", maker, .2f))];
        Assert.Equal(.4f, over.Quality, 3);
        Assert.Equal(.4f, over.QualityForRole("lens"), 3);
        Assert.Equal(.2f, under.QualityForRole("lens"), 3);
        // The raw part is kept for disassembly (named-components-after-demo).
        Assert.Equal(.9f, over.Roles.Single().Quality, 3);
    }

    // The fallbacks still read the assembly: an unnamed role, an unrolled role, and a lot with no roles.
    [Fact]
    public void FallbacksReadTheAssembly()
    {
        var lot = new Lot { Quality = .4f, Roles = new List<RoleFill> { new RoleFill { Role = "lens", Quality = .9f } } };
        Assert.Equal(.4f, lot.QualityForRole(null), 3);
        Assert.Equal(.4f, lot.QualityForRole(""), 3);
        Assert.Equal(.4f, lot.QualityForRole("ghost"), 3);
        Assert.Equal(.4f, new Lot { Quality = .4f }.QualityForRole("lens"), 3);
    }

    // A Quality term on a role with part .9 and assembly .4 evaluates as if quality were .4, on a real minted instance.
    [Fact]
    public void QualityTermReadsCeiling()
    {
        var maker = new Faction { Name = "Capped", AssemblyMean = .4f, AssemblyDeviation = 0 };
        var instance = (EquippableItem) _items.CreateInstance(_items.CreateLot(Product("Over", maker, .9f)));
        var stat = new PerformanceStat { Min = 0, Max = 1, Terms = { new StatTerm { Source = StatSource.Quality, Exponent = 1, Role = "lens" } } };
        Assert.Equal(.4f, _items.Evaluate(stat, instance), 3);
    }
}
