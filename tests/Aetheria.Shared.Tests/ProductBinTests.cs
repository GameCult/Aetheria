using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// A bin is a product of its maker's line above a floor (aetheria-release, cut product-tiers; ruling maker-design-tiers-allowed):
// the mint derives its distributions from the line on every lot, truncated below line mean + Floor deviations, and the lot is
// still the bin's. Lots here come from CreateLot(product), the production mint, on the AssemblyFromMakerTests fixture.
public sealed class ProductBinTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-bins-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly CultRecordRef<CraftedItemData> _lens;
    private readonly List<string> _log = new List<string>();

    public ProductBinTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true);
        var gear = _cache.Upsert(new GearData
        {
            Name = "Lensed", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Roles = { new ItemRole { Name = "lens" } }
        });
        _lens = new CultRecordRef<CraftedItemData>(gear.Key);
        _items = new ItemManager(1u, _cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _log.Add) { Random = new CultMath.Random(7u) };
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static AssemblyDistribution A(float mean, float deviation) => new AssemblyDistribution { Mean = mean, StandardDeviation = deviation };

    // A maker at assembly .6 +- .1 and a line of the Lensed design at lens .5 +- .1.
    private FactionProductData Line(string name = "Line", float lensMean = .5f, float lensDeviation = .1f, AssemblyDistribution own = null, float makerMean = .6f)
    {
        var maker = _cache.Upsert(new Faction { Name = name + " Maker", Assembly = A(makerMean, .1f) });
        _cache.Upsert(new FactionProductData
        {
            Name = name, Design = _lens, Manufacturer = maker, Assembly = own,
            Roles = { new ProductRole { Role = "lens", Mean = lensMean, StandardDeviation = lensDeviation } }
        });
        return _cache.GetAll<FactionProductData>().Single(p => p.Name == name);
    }

    private FactionProductData Bin(FactionProductData line, string name, float floor)
    {
        _cache.Upsert(new FactionProductData
        {
            Name = name, Design = line.Design, Manufacturer = line.Manufacturer,
            Bin = new ProductBin { Line = _cache.RefOf(line), Floor = floor }
        });
        return _cache.GetAll<FactionProductData>().Single(p => p.Name == name);
    }

    private Lot[] Mint(FactionProductData product, int count) =>
        Enumerable.Range(0, count).Select(_ => _items.Lots[_items.CreateLot(product)]).ToArray();

    private static float[] Fills(Lot[] lots) => lots.Select(l => l.Roles.Single().Quality).ToArray();

    // Floor 0 is the top half of the line: nothing under the line's mean, in the fills or in the assembly.
    [Fact]
    public void BinnedFillsAndAssemblyNeverFallBelowTheFloor()
    {
        var lots = Mint(Bin(Line(), "Top Half", 0), 400);
        Assert.All(lots, lot =>
        {
            Assert.True(lot.Roles.Single().Quality >= .5f - 1e-5f, "a fill fell under the line's mean");
            Assert.True(lot.Quality >= .6f - 1e-5f, "an assembly fell under the maker's mean");
        });
        Assert.InRange(Fills(lots).Average(), .55f, .60f);
    }

    // The floor is in the line's standard deviations: .67 on .5 +- .1 cuts at .567 and the truncated mean is .627.
    [Fact]
    public void FloorIsInDeviations()
    {
        var fills = Fills(Mint(Bin(Line(), "Top Quarter", .67f), 400));
        Assert.All(fills, fill => Assert.True(fill >= .567f - 1e-5f, "a fill fell under mean + floor deviations"));
        Assert.InRange(fills.Average(), .61f, .645f);
    }

    // A bin keeps no distribution of its own: after it exists, the line and the maker move and the bin follows.
    [Fact]
    public void BinDerivesFromItsLineNotACopy()
    {
        var line = Line();
        var bin = Bin(line, "Follower", 0);
        Assert.Empty(bin.Roles);
        Assert.Null(bin.Assembly);
        line.Roles.Single().Mean = .7f;
        var maker = _cache.Get(line.Manufacturer);
        maker.Assembly = A(.8f, .1f);
        _cache.Commit(batch =>
        {
            batch.Upsert(typeof(FactionProductData), line, _cache.RefOf(line).Key);
            batch.Upsert(typeof(Faction), maker, _cache.RefOf(maker).Key);
        });
        var lots = Mint(bin, 300);
        Assert.All(lots, lot =>
        {
            Assert.True(lot.Roles.Single().Quality >= .7f - 1e-5f, "the bin read a copy of its line's fill");
            Assert.True(lot.Quality >= .8f - 1e-5f, "the bin read a copy of its maker's assembly");
        });
    }

    // A line's own assembly is the final test a bin of it sits: the maker's is read only when the line sets none.
    [Fact]
    public void BinReadsTheLinesOwnAssemblyBeforeTheMakers()
    {
        var bin = Bin(Line(own: A(.85f, .05f), makerMean: .4f), "Certified", 0);
        Assert.All(Mint(bin, 300), lot => Assert.True(lot.Quality >= .85f - 1e-5f, "the bin assembled as its maker, not its line"));
    }

    // 6 deviations above a .2 +- .01 line is out of reach in 64 draws: the mint returns the cutoff, .26, and does not loop.
    [Fact]
    public void UnreachableFloorMintsAtTheCutoff()
    {
        var bin = Bin(Line(lensMean: .2f, lensDeviation: .01f, own: A(.2f, .01f)), "Unreachable", 6);
        Assert.All(Mint(bin, 20), lot =>
        {
            Assert.Equal(.26f, lot.Roles.Single().Quality, 5);
            Assert.Equal(.26f, lot.Quality, 5);
        });
    }

    // The search draws at most 64 times, and stops at the first draw that clears the cutoff: the stream after it is what shows how
    // many draws it took. An unreachable cutoff takes exactly 64, a deviation of 0 (draw equal to the cutoff) takes 1.
    [Fact]
    public void TheSearchDrawsAtMostSixtyFourTimesAndStopsAtTheFirstDrawThatClearsTheCutoff()
    {
        float AfterDraws(int draws)
        {
            var random = new CultMath.Random(11u);
            for (var i = 0; i < draws; i++) random.NextGaussian(0, 1);
            return random.NextGaussian(0, 1);
        }

        var unreachable = new CultMath.Random(11u);
        Assert.Equal(40f, unreachable.NextGaussianAbove(0, 1, 40f));
        Assert.Equal(AfterDraws(64), unreachable.NextGaussian(0, 1));

        var immediate = new CultMath.Random(11u);
        Assert.Equal(.5f, immediate.NextGaussianAbove(.5f, 0, 0));
        Assert.Equal(AfterDraws(1), immediate.NextGaussian(0, 1));
    }

    // Snapshot of the first five assemblies and fills at base, before the bin existed (seed 20261018, lens .5 +- .1, maker .6 +- .1).
    [Fact]
    public void UnbinnedMintConsumesTheStreamAsBefore()
    {
        var line = Line();
        _items.Random = new CultMath.Random(20261018u);
        var lots = Mint(line, 5);
        Assert.Empty(_log);
        var assemblies = new[] { .59560823f, .68289334f, .57563853f, .5639112f, .59828424f };
        var fills = new[] { .6311789f, .5835483f, .47641385f, .46416125f, .45124212f };
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(assemblies[i], lots[i].Quality, 5);
            Assert.Equal(fills[i], lots[i].Roles.Single().Quality, 5);
        }
    }

    // The lot is the bin's: its product, its title, and the line's maker as its brand.
    [Fact]
    public void ABinnedLotIsBrandedAsTheBin()
    {
        var line = Line();
        var bin = Bin(line, "Premiere", 0);
        var lotId = _items.CreateLot(bin);
        Assert.Equal(_cache.RefOf(bin), _items.Lots[lotId].Product);
        var instance = _items.CreateInstance(lotId);
        Assert.Equal("Premiere", _items.Title(instance));
        var (maker, product) = _items.Brand(instance);
        Assert.Equal(_cache.Get(line.Manufacturer).Name, maker.Name);
        Assert.Equal("Premiere", product.Name);
    }

    // The line a bin draws from is the one Bin.Line names, not another line of the design: three lines of one design, by three
    // makers, with the bin on the middle one by name, so a bin that reads the first or the last line of the design is far off.
    [Fact]
    public void ABinMintsFromExactlyTheLineItNames()
    {
        Line("A line", lensMean: .3f, makerMean: .2f);
        var middle = Line("M line", lensMean: .6f, makerMean: .5f);
        Line("Z line", lensMean: .9f, makerMean: .9f);
        var lots = Mint(Bin(middle, "Middle Premiere", 0), 400);
        Assert.All(lots, lot =>
        {
            Assert.True(lot.Roles.Single().Quality >= .6f - 1e-5f, "a fill came from a line below the named one");
            Assert.True(lot.Quality >= .5f - 1e-5f, "an assembly came from a maker below the named line's");
        });
        Assert.InRange(Fills(lots).Average(), .64f, .72f);
        Assert.InRange(lots.Average(lot => lot.Quality), .55f, .62f);
    }

    private const string Canary = "canary-7f3a91";

    private enum Damage { None, LineRemoved, LineIsBin }

    // A catalog on disk holding one line and a bin of it, damaged as asked, for AetheriaStores.Open to read.
    private string DamagedCatalog(float floor, Damage damage)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N"), "Aetheria.cc");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var seed = AetheriaStores.Open(path, catalogWritable: true);
        seed.Upsert(new VerseGrammar { Revision = 1 });
        var maker = seed.Upsert(new Faction { Name = Canary + " Maker" });
        var gear = seed.Upsert(new GearData { Name = "Lensed", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10, Roles = { new ItemRole { Name = "lens" } } });
        var design = new CultRecordRef<CraftedItemData>(gear.Key);
        var line = seed.Upsert(new FactionProductData { Name = Canary + " Line", Design = design, Manufacturer = maker, Roles = { new ProductRole { Role = "lens" } } });
        var bin = seed.Upsert(new FactionProductData { Name = Canary, Design = design, Manufacturer = maker, Bin = new ProductBin { Line = line, Floor = floor } });
        if (damage == Damage.LineIsBin)
            seed.Upsert(new FactionProductData { Name = Canary + " Nested", Design = design, Manufacturer = maker, Bin = new ProductBin { Line = bin, Floor = 0 } });
        if (damage == Damage.LineRemoved) seed.Remove(line.Key);
        seed.FlushAsync().Wait();
        return path;
    }

    // The runtime does not trust authoring for a bin: a NaN floor would weigh NaN and null every draw of its design.
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ACatalogWhoseBinFloorIsNotFiniteIsRefusedWhereItIsRead(float floor)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(DamagedCatalog(floor, Damage.None)));
        Assert.Contains("Floor", refusal.Message);
        Assert.DoesNotContain(Canary, refusal.Message);
        Assert.DoesNotContain(floor.ToString(), refusal.Message);
    }

    [Fact]
    public void ACatalogWhoseBinNamesNoLineIsRefusedWhereItIsRead()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(DamagedCatalog(.67f, Damage.LineRemoved)));
        Assert.Contains("Line", refusal.Message);
        Assert.DoesNotContain(Canary, refusal.Message);
    }

    [Fact]
    public void ACatalogWhoseBinNamesAnotherBinIsRefusedWhereItIsRead()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(DamagedCatalog(.67f, Damage.LineIsBin)));
        Assert.Contains("Line", refusal.Message);
        Assert.DoesNotContain(Canary, refusal.Message);
    }

    // The refusals are of damage, not of bins: a sound bin on a sound line reads.
    [Fact]
    public void ACatalogWithASoundBinIsRead()
    {
        using var cache = AetheriaStores.Open(DamagedCatalog(.67f, Damage.None));
        Assert.Equal(.67f, cache.GetAll<FactionProductData>().Single(product => product.Name == Canary).Bin.Floor);
    }

    // A bin whose line the catalog does not hold mints as a product with no roles of its own, and says so.
    [Fact]
    public void ABinOfALineTheCatalogLacksMintsAtDefaultsAndLogs()
    {
        var line = Line();
        var bin = Bin(line, "Orphan", 0);
        _cache.Remove(_cache.RefOf(line).Key);
        var lots = Mint(bin, 5);
        Assert.Equal(5, lots.Length);
        Assert.Equal(5, _log.Count);
        Assert.All(_log, message => Assert.False(string.IsNullOrWhiteSpace(message)));
    }
}
