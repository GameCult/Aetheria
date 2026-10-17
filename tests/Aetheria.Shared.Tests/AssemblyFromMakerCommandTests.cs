using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// assembly-from-maker (aetheria-release, cut assembly-from-maker): each maker's assembly distribution is derived from the
// roles of the products it manufactures.
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
        cache.Upsert(new Faction { Name = "Bystander", AssemblyMean = .9f, AssemblyDeviation = .01f });
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

    [Fact]
    public void DryRunWritesNothingApplyIsIdempotent()
    {
        Seed();
        var before = File.ReadAllBytes(Catalog);
        Assert.Contains("Maker", Run(apply: false));
        Assert.Equal(before, File.ReadAllBytes(Catalog));

        Run(apply: true);
        using (var cache = AetheriaStores.Open(Catalog))
        {
            var maker = cache.GetAll<Faction>().Single(f => f.Name == "Maker");
            Assert.Equal(.5f, maker.AssemblyMean, 3);
            Assert.Equal(.2f, maker.AssemblyDeviation, 3);
            // A faction that manufactures nothing is written the field defaults.
            var bystander = cache.GetAll<Faction>().Single(f => f.Name == "Bystander");
            Assert.Equal(.5f, bystander.AssemblyMean, 3);
            Assert.Equal(.15f, bystander.AssemblyDeviation, 3);
        }

        var written = File.ReadAllBytes(Catalog);
        Assert.Contains("No changes", Run(apply: true));
        Assert.Equal(written, File.ReadAllBytes(Catalog));
    }
}
