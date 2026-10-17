using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// assembly-from-maker (aetheria-release, cut assembly-from-maker): each maker's unauthored assembly is seeded from the roles
// of the products it manufactures.
public sealed class AssemblyFromMakerCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-assembly-cmd-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public AssemblyFromMakerCommandTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private void Seed()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var maker = cache.Upsert(new Faction { Name = "Maker" });
        cache.Upsert(new Faction { Name = "Bystander" });
        var gear = cache.Upsert(new GearData { Name = "Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10 });
        var design = new CultRecordRef<CraftedItemData>(gear.Key);
        // Two products, three roles: the mean is over roles (.2, .4, .9 -> .5), the deviation likewise (.1, .1, .4 -> .2).
        cache.Upsert(new FactionProductData
        {
            Name = "A", Design = design, Manufacturer = maker,
            Roles = { new ProductRole { Role = "x", Mean = .2f, StandardDeviation = .1f }, new ProductRole { Role = "y", Mean = .4f, StandardDeviation = .1f } }
        });
        cache.Upsert(new FactionProductData
        {
            Name = "B", Design = design, Manufacturer = maker,
            Roles = { new ProductRole { Role = "x", Mean = .9f, StandardDeviation = .4f } }
        });
        // Figures that need two decimals: (.2 + .27 + .31) / 3 = .26 and (.11 + .17 + .2) / 3 = .16.
        var fine = cache.Upsert(new Faction { Name = "Fine" });
        cache.Upsert(new FactionProductData
        {
            Name = "C", Design = design, Manufacturer = fine,
            Roles =
            {
                new ProductRole { Role = "x", Mean = .2f, StandardDeviation = .11f },
                new ProductRole { Role = "y", Mean = .27f, StandardDeviation = .17f },
                new ProductRole { Role = "z", Mean = .31f, StandardDeviation = .2f }
            }
        });
        // A maker whose parts are near the top: the margin is capped at 1. And one whose assembly was already authored in Studio.
        var top = cache.Upsert(new Faction { Name = "Top" });
        cache.Upsert(new FactionProductData
        {
            Name = "D", Design = design, Manufacturer = top,
            Roles = { new ProductRole { Role = "x", Mean = .95f, StandardDeviation = .05f } }
        });
        var tuned = cache.Upsert(new Faction { Name = "Tuned", Assembly = new AssemblyDistribution { Mean = .42f, StandardDeviation = .07f } });
        cache.Upsert(new FactionProductData
        {
            Name = "E", Design = design, Manufacturer = tuned,
            Roles = { new ProductRole { Role = "x", Mean = .1f, StandardDeviation = .3f } }
        });
        cache.FlushAsync().Wait();
    }

    private string Run(bool apply)
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: apply);
        var output = new StringWriter();
        Assert.Equal(0, AssemblyFromMakerCommand.Run(cache, apply, output));
        cache.FlushAsync().Wait();
        return output.ToString();
    }

    private Faction Maker(string name)
    {
        using var cache = AetheriaStores.Open(Catalog);
        return cache.GetAll<Faction>().Single(f => f.Name == name);
    }

    // The assembly is the part mean plus the tolerance margin and the deviation the parts' mean; the margin is capped at 1; a
    // faction that makes no roled product stays unauthored.
    [Fact]
    public void DerivesPartMeanPlusMargin()
    {
        Seed();
        Run(apply: true);
        Assert.Equal(.6f, Maker("Maker").Assembly.Mean, 3);
        Assert.Equal(.2f, Maker("Maker").Assembly.StandardDeviation, 3);
        Assert.Equal(.36f, Maker("Fine").Assembly.Mean, 3);
        Assert.Equal(.16f, Maker("Fine").Assembly.StandardDeviation, 3);
        Assert.Equal(1f, Maker("Top").Assembly.Mean, 3);
        Assert.Equal(.05f, Maker("Top").Assembly.StandardDeviation, 3);
        Assert.Null(Maker("Bystander").Assembly);
    }

    // A value tuned in Studio survives every replay, a dry run writes nothing, and a second apply has nothing to do.
    [Fact]
    public void AuthoredAssemblyIsKept()
    {
        Seed();
        var before = File.ReadAllBytes(Catalog);
        Assert.Contains("Maker", Run(apply: false));
        Assert.Equal(before, File.ReadAllBytes(Catalog));

        Run(apply: true);
        Assert.Equal(.42f, Maker("Tuned").Assembly.Mean, 3);
        Assert.Equal(.07f, Maker("Tuned").Assembly.StandardDeviation, 3);
        using (var cache = AetheriaStores.Open(Catalog))
            Assert.All(cache.GetAll<FactionProductData>(), p => Assert.Null(p.Assembly));

        var written = File.ReadAllBytes(Catalog);
        Assert.Contains("No changes", Run(apply: true));
        Assert.Equal(written, File.ReadAllBytes(Catalog));
    }
}
