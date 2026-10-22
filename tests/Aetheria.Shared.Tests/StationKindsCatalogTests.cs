using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

public sealed class StationKindsCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stationkindscmd-" + Guid.NewGuid().ToString("N"));

    public StationKindsCatalogTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void DryRunWritesNothingApplyIsIdempotent()
    {
        var path = Path.Combine(_root, "Aetheria.cc");
        using (var seed = AetheriaStores.Open(path, catalogWritable: true))
        {
            seed.Upsert(new VerseGrammar { Revision = 1 });
            seed.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
            seed.FlushAsync().Wait();
        }
        var before = File.ReadAllBytes(path);

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            Assert.Equal(0, StationKindsCatalog.Run(cache, false, new StringWriter()));
            cache.FlushAsync().Wait();
            Assert.Empty(cache.GetAll<StationKindData>());
        }
        Assert.Equal(before, File.ReadAllBytes(path));

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            Assert.Equal(0, StationKindsCatalog.Run(cache, true, new StringWriter()));
            cache.FlushAsync().Wait();
            Assert.Equal(17, cache.GetAll<StationKindData>().Count());
        }
        var applied = File.ReadAllBytes(path);

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var output = new StringWriter();
            StationKindsCatalog.Run(cache, true, output);
            cache.FlushAsync().Wait();
            Assert.Contains("Nothing to author", output.ToString());
            Assert.Equal(17, cache.GetAll<StationKindData>().Count());
        }
        Assert.Equal(applied, File.ReadAllBytes(path));
    }
}
