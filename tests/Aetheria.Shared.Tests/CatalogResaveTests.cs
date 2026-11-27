using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// AetherDb catalog resave (follow-up aetherdb-catalog-resave): a record stored under an older schema id is re-saved under the
// current one, a dry run writes nothing, and a second apply writes nothing. The two probe types share one schema name; the
// second appends a member, which is how a real schema change restamps a record.
public sealed class CatalogResaveTests : IDisposable
{
    [MessagePackObject]
    [CultDocument("aetheria.test.resaveprobe", "1")]
    public sealed class ProbeV1
    {
        [Key(0)] public string Name = "";
    }

    [MessagePackObject]
    [CultDocument("aetheria.test.resaveprobe", "1")]
    public sealed class ProbeV2
    {
        [Key(0)] public string Name = "";
        [Key(1)] public int Appended = 7;
    }

    private readonly TempDirectory _directory = new TempDirectory();
    private string Path_ => System.IO.Path.Combine(_directory.Path, "probe.cc");

    public void Dispose() => _directory.Dispose();

    private static CultCache Open(string path, Type type)
    {
        var cache = new CultCache(CultDocumentRegistry.ForTypes(new[] { type }));
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(path), new[] { type });
        return cache;
    }

    private void WriteOld()
    {
        using var cache = Open(Path_, typeof(ProbeV1));
        cache.Commit(batch =>
        {
            batch.Upsert(typeof(ProbeV1), new ProbeV1 { Name = "a" }, new CultRecordKey("a"));
            batch.Upsert(typeof(ProbeV1), new ProbeV1 { Name = "b" }, new CultRecordKey("b"));
        });
        cache.FlushAsync().Wait();
    }

    private string[] StoredIds() =>
        CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(Path_)).Records.Select(r => r.SchemaId).ToArray();

    [Fact]
    public void ADryRunListsAndWritesNothing()
    {
        WriteOld();
        var before = File.ReadAllBytes(Path_);
        var oldIds = StoredIds();
        using (var cache = Open(Path_, typeof(ProbeV2)))
        {
            var output = new StringWriter();
            Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: false, output));
            Assert.Contains("2 record(s) would be re-saved", output.ToString());
        }
        Assert.Equal(before, File.ReadAllBytes(Path_));
        Assert.Equal(oldIds, StoredIds());
    }

    [Fact]
    public void ApplyRestampsEveryRecordAndASecondApplyWritesNothing()
    {
        WriteOld();
        var oldIds = StoredIds();
        string[] newIds;
        using (var cache = Open(Path_, typeof(ProbeV2)))
        {
            Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: true, new StringWriter()));
        }
        newIds = StoredIds();
        Assert.Equal(2, newIds.Length);
        Assert.All(newIds, id => Assert.DoesNotContain(id, oldIds));
        var written = File.ReadAllBytes(Path_);

        using (var cache = Open(Path_, typeof(ProbeV2)))
        {
            Assert.Equal(new[] { "a", "b" }, cache.GetAll<ProbeV2>().Select(p => p.Name).OrderBy(n => n));
            Assert.All(cache.GetAll<ProbeV2>(), p => Assert.Equal(7, p.Appended));
            var output = new StringWriter();
            Assert.Equal(0, CatalogResaveCommand.Run(cache, apply: true, output));
            Assert.Contains("0 record(s) would be re-saved", output.ToString());
        }
        Assert.Equal(written, File.ReadAllBytes(Path_));
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
