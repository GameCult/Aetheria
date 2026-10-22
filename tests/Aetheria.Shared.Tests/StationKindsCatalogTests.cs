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

    private static readonly string[] Ids =
    {
        "mining", "volatiles", "agriculture", "processing", "fabrication", "shipyard", "depot", "trade-hub", "residential",
        "habitat-support", "urban", "administrative", "power", "research", "salvage", "black-market", "waystation"
    };

    // The catalog this command first runs on holds a station hull with no kind, which Open refuses, so the command opens
    // the catalog itself: Zenith gets the ruled kind, a kind it already has is kept.
    [Fact]
    public void ZenithGetsTheRuledKindAndAKindedOneIsKept()
    {
        var path = Path.Combine(_root, "Zenith.cc");
        using (var seed = AetheriaStores.Open(path, catalogWritable: true))
        {
            seed.Upsert(new VerseGrammar { Revision = 1 });
            seed.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
            seed.Upsert(new HullData { Name = "Zenith", HullType = HullType.Station, Shape = new Shape(2, 2), Price = 1 });
            seed.Upsert(new HullData { Name = "Cutter", HullType = HullType.Ship, Shape = new Shape(2, 2), Price = 1 });
            seed.FlushAsync().Wait();
        }
        Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(path));

        var dry = new StringWriter();
        using (var cache = StationKindsCatalog.OpenCatalog(false, path)) StationKindsCatalog.Run(cache, false, dry);
        Assert.Contains("Zenith: kind trade-hub", dry.ToString());

        var applied = new StringWriter();
        using (var cache = StationKindsCatalog.OpenCatalog(true, path))
        {
            StationKindsCatalog.Run(cache, true, applied);
            cache.FlushAsync().Wait();
        }
        Assert.Contains("and Zenith's kind", applied.ToString());
        using (var cache = AetheriaStores.Open(path))
        {
            Assert.Equal(StationKindData.KeyOf("trade-hub").Value, cache.GetAll<HullData>().Single(h => h.Name == "Zenith").Kind.Key.Value);
            Assert.False(cache.GetAll<HullData>().Single(h => h.Name == "Cutter").Kind.IsSet());
        }

        var again = new StringWriter();
        using (var cache = StationKindsCatalog.OpenCatalog(true, path)) StationKindsCatalog.Run(cache, true, again);
        Assert.Contains("Zenith: already has a kind, kept", again.ToString());
        Assert.Contains("Nothing to author", again.ToString());
    }

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
            var dry = new StringWriter();
            Assert.Equal(0, StationKindsCatalog.Run(cache, false, dry));
            cache.FlushAsync().Wait();
            Assert.Equal(17, dry.ToString().Split((char) 10).Count(line => line.TrimEnd().EndsWith("authors")));
            Assert.Contains("Dry run", dry.ToString());
            Assert.Contains("Zenith: no such hull", dry.ToString());
            Assert.Empty(cache.GetAll<StationKindData>());
        }
        Assert.Equal(before, File.ReadAllBytes(path));

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var output = new StringWriter();
            Assert.Equal(0, StationKindsCatalog.Run(cache, true, output));
            cache.FlushAsync().Wait();
            Assert.Contains("Authored 17 station kinds in", output.ToString());
            var kinds = cache.GetAll<StationKindData>().ToArray();
            Assert.Equal(Ids.Select(id => StationKindData.KeyOf(id).Value).OrderBy(key => key, StringComparer.Ordinal),
                kinds.Select(kind => cache.RefOf(kind).Key.Value).OrderBy(key => key, StringComparer.Ordinal));
            Assert.All(kinds, kind => { Assert.False(string.IsNullOrWhiteSpace(kind.Name)); Assert.False(string.IsNullOrWhiteSpace(kind.Description)); });
        }
        var applied = File.ReadAllBytes(path);

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var output = new StringWriter();
            StationKindsCatalog.Run(cache, true, output);
            cache.FlushAsync().Wait();
            Assert.Contains("Nothing to author", output.ToString());
            Assert.Equal(17, output.ToString().Split((char) 10).Count(line => line.TrimEnd().EndsWith("kept")));
            Assert.Equal(17, cache.GetAll<StationKindData>().Count());
        }
        Assert.Equal(applied, File.ReadAllBytes(path));
    }
}
