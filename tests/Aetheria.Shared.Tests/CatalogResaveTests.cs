using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// AetherDb catalog resave (follow-up aetherdb-catalog-resave): a record stored under an older schema id is re-saved under the
// current one, a dry run writes nothing, and a second apply writes nothing. The fixture tests/fixtures/catalog-resave/probe-v1.cc
// holds two records of schema aetheria.test.resaveprobe as written by a type with one member; Probe appends a second, which is
// how a real schema change restamps a record.
public sealed class CatalogResaveTests : IDisposable
{
    [MessagePackObject]
    [CultDocument("aetheria.test.resaveprobe", "1")]
    public sealed class Probe
    {
        [Key(0)] public string Name = "";
        [Key(1)] public int Appended = 7;
    }

    private readonly TempDirectory _directory = new TempDirectory();
    private string Stored => System.IO.Path.Combine(_directory.Path, "probe.cc");

    public CatalogResaveTests() =>
        File.Copy(System.IO.Path.Combine(RunStartTests.FindRepoRoot(), "tests", "fixtures", "catalog-resave", "probe-v1.cc"), Stored);

    public void Dispose() => _directory.Dispose();

    private CultCache Open()
    {
        var cache = new CultCache(CultDocumentRegistry.ForTypes(new[] { typeof(Probe) }));
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(Stored), new[] { typeof(Probe) });
        return cache;
    }

    private string[] StoredIds() =>
        CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(Stored)).Records.Select(r => r.SchemaId).ToArray();

    [Fact]
    public void ADryRunListsAndWritesNothing()
    {
        var before = File.ReadAllBytes(Stored);
        using (var cache = Open())
        {
            var output = new StringWriter();
            Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: false, output));
            Assert.Contains("2 record(s) would be re-saved", output.ToString());
        }
        Assert.Equal(before, File.ReadAllBytes(Stored));
    }

    [Fact]
    public void ApplyRestampsEveryRecordAndASecondApplyWritesNothing()
    {
        var oldIds = StoredIds();
        using (var cache = Open())
            Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: true, new StringWriter()));
        var newIds = StoredIds();
        Assert.Equal(2, newIds.Length);
        Assert.All(newIds, id => Assert.DoesNotContain(id, oldIds));
        var written = File.ReadAllBytes(Stored);

        using (var cache = Open())
        {
            Assert.Equal(new[] { "a", "b" }, cache.GetAll<Probe>().Select(p => p.Name).OrderBy(n => n));
            Assert.All(cache.GetAll<Probe>(), p => Assert.Equal(7, p.Appended));
            var output = new StringWriter();
            Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: true, output));
            Assert.Contains("0 record(s) would be re-saved", output.ToString());
        }
        Assert.Equal(written, File.ReadAllBytes(Stored));
    }

    // The catalog the game ships is current: resaving it changes nothing.
    [Fact]
    public void TheShippedCatalogIsAlreadyCurrent()
    {
        var copy = System.IO.Path.Combine(_directory.Path, "Aetheria.cc");
        File.Copy(System.IO.Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), copy);
        using var cache = AetheriaStores.Open(copy, catalogWritable: true, registry: TestCatalog.Registry());
        var output = new StringWriter();
        Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: true, output));
        Assert.Contains("0 record(s) would be re-saved", output.ToString());
    }
}
