using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// A commodity holds a Shape (aetheria-living-world, cut commodity-shapes): one cell by the type default, refused as an L-shape fault
// when a stored commodity has none, and held by a cargo bay in the shipped catalog.
public sealed class CommodityShapeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-commodityshape-" + Guid.NewGuid().ToString("N"));

    public CommodityShapeTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private sealed class Lambda : EconomyBatch
    {
        private readonly Action<EconomyAuthor> _author;
        public Lambda(Action<EconomyAuthor> author) => _author = author;
        public override void Author(EconomyAuthor author) => _author(author);
    }

    // Kills: the default dropped from either commodity type.
    [Fact]
    public void ANewCommodityIsOneCell()
    {
        Assert.Single(new SimpleCommodityData().Shape.Coordinates);
        Assert.Single(new CompoundCommodityData().Shape.Coordinates);
    }

    // Kills: a check that reads only simple commodities, or one that treats a restored shape as a fault.
    [Fact]
    public void AShapelessCommodityIsAnLShapeFault()
    {
        using var f = EconomyFixture.Healthy();
        Assert.Empty(EconomyChecks.Shapes(f.Cache));
        var key = new CultRecordKey(EconomyFixture.Key("plate"));
        var plate = f.Cache.Get<CompoundCommodityData>(key);
        plate.Shape = null;
        f.Cache.Upsert(typeof(CompoundCommodityData), plate, key);
        var fault = Assert.Single(EconomyChecks.Shapes(f.Cache));
        Assert.Equal(EconomyChecks.LShape, fault.Invariant);
        Assert.Equal(key.Value, fault.Record);
        Assert.Single(EconomyChecks.Check(f.Cache).Faults, x => x.Invariant == EconomyChecks.LShape);
        plate.Shape = new Shape();
        f.Cache.Upsert(typeof(CompoundCommodityData), plate, key);
        Assert.Empty(EconomyChecks.Shapes(f.Cache));
    }

    // Kills: LShape missing from the refusing set in EconomyCommand.
    [Fact]
    public void ABatchThatWritesAShapelessCommodityIsRefused()
    {
        using var f = EconomyFixture.Healthy();
        f.Cache.FlushAsync().GetAwaiter().GetResult();
        var bytes = File.ReadAllBytes(f.CatalogPath);
        var output = new StringWriter();
        var batch = new Lambda(a => a.Put(new SimpleCommodityData { Name = "void", Shape = null }, new CultRecordKey(EconomyFixture.Key("void"))));
        var code = EconomyCommand.Author(f.CatalogPath, new List<EconomyBatch> { batch }, true, output, TestCatalog.Registry(), _root);
        Assert.Equal(1, code);
        Assert.Contains("The batch is refused", output.ToString());
        Assert.Contains("L-shape commodity:void", output.ToString());
        Assert.Equal(bytes, File.ReadAllBytes(f.CatalogPath));
    }
}
