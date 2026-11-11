using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using Xunit;

// CultLib 3.0 requires element ids and reads the stores written before it (2.0 and older), which carry none.
// The shipped GameData stores are those stores, byte for byte: they have never been rewritten under the new pin.
// Upgrading is one-way: once a store is written under the new pin it holds element ids, and an older pin can read it only by
// dropping them (CultLib #98). Readers upgrade before writers.
public class ElementIdUpgradeTests
{
    private static string Shipped => Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc");

    [Fact]
    public async Task AStoreWrittenBeforeElementIdsOpensMintsThemOnceAndKeepsEveryRecord()
    {
        using var directory = new TempDirectory();
        var copy = Path.Combine(directory.Path, "Aetheria.cc");
        File.Copy(Shipped, copy);

        int records, minted;
        using (var cache = AetheriaStores.Open(copy, catalogWritable: true, registry: TestCatalog.Registry()))
        {
            records = cache.AllStoredDocuments.Count();
            Assert.True(records > 0);
            minted = cache.MintElementIds();
            Assert.True(minted > 0, "the shipped catalog predates element ids; opening it must have minted some");
            Assert.Equal(0, cache.MintElementIds());
            await cache.FlushAsync();
        }

        using var reopened = AetheriaStores.Open(copy, registry: TestCatalog.Registry());
        Assert.Equal(records, reopened.AllStoredDocuments.Count());
        Assert.Equal(0, reopened.MintElementIds());
        foreach (var hull in reopened.GetAll<HullData>())
        {
            var ids = hull.Hardpoints.Select(hardpoint => hardpoint.Id).ToArray();
            Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
            Assert.Equal(ids.Length, ids.Distinct().Count());
        }
    }

    // Upgrading assigns ids and changes nothing else: every record of the old-format file decodes, once the id members are
    // blank, to the same bytes before and after the mint, flush and reopen.
    [Fact]
    public async Task UpgradingKeepsEveryFieldOfEveryRecordAndOnlyAddsIds()
    {
        using var directory = new TempDirectory();
        var copy = Path.Combine(directory.Path, "Aetheria.cc");
        File.Copy(Shipped, copy);
        var registry = TestCatalog.Registry();
        Dictionary<string, string> Content(string path) =>
            GameCult.Caching.MessagePack.CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path)).Records
                .ToDictionary(record => record.Key, record => record.SchemaId + ":" + Convert.ToHexString(ElementIdBlind.Payload(registry, record.SchemaId, record.Payload)));
        var before = Content(copy);

        using (var cache = AetheriaStores.Open(copy, catalogWritable: true, registry: TestCatalog.Registry()))
        {
            Assert.True(cache.MintElementIds() > 0);
            await cache.FlushAsync();
        }

        var after = Content(copy);
        Assert.Equal(before.Count, after.Count);
        foreach (var record in before) Assert.True(after[record.Key] == record.Value, $"{record.Key} changed content when its element ids were minted");
    }

    [Fact]
    public void EveryShippedModPackageStillLoads()
    {
        var packages = Directory.GetDirectories(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Mods"));
        Assert.NotEmpty(packages);
        foreach (var package in packages)
        {
            var (hull, visual, _) = ShipAuthoringStore.Load(Path.Combine(package, "ship.cc"));
            Assert.NotNull(hull);
            Assert.NotNull(visual);
        }
    }

    // A package whose file still holds a retired slot reads but cannot be written over (CultSchemaConflictException).
    [Fact]
    public void EveryShippedModPackageCanBeWrittenOver()
    {
        foreach (var package in Directory.GetDirectories(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Mods")))
        {
            using var directory = new TempDirectory();
            var copy = Path.Combine(directory.Path, "ship.cc");
            File.Copy(Path.Combine(package, "ship.cc"), copy);
            var (hull, visual, products) = ShipAuthoringStore.Load(copy);
            using (var cache = ShipAuthoringStore.Open(copy, writable: true))
            {
                ShipAuthoringStore.Write(cache, hull, visual);
                foreach (var product in products) ShipAuthoringStore.WriteProduct(cache, visual.Id, product);
                cache.FlushAsync().GetAwaiter().GetResult();
            }
            Assert.Equal(visual.Id, ShipAuthoringStore.Load(copy).Visual.Id);
        }
    }

    // The tool that regenerated the shipped packages: a dry run touches nothing, apply keeps every record's content.
    [Fact]
    public void RegenerateDryRunLeavesTheFileAndApplyKeepsTheContent()
    {
        using var directory = new TempDirectory();
        var copy = Path.Combine(directory.Path, "ship.cc");
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Mods", "aetheria.wren", "ship.cc"), copy);
        var bytes = File.ReadAllBytes(copy);
        var (hull, visual, products) = ShipAuthoringStore.Load(copy);

        Assert.Equal(0, ShipAuthoringCommands.Run(new[] { "regenerate", copy }));
        Assert.Equal(bytes, File.ReadAllBytes(copy));
        Assert.Equal(0, ShipAuthoringCommands.Run(new[] { "regenerate", copy, "apply" }));

        var (hull2, visual2, products2) = ShipAuthoringStore.Load(copy);
        Assert.Equal(ElementIdBlind.Pack(hull), ElementIdBlind.Pack(hull2));
        Assert.Equal(ElementIdBlind.Pack(visual), ElementIdBlind.Pack(visual2));
        Assert.Equal(products.Length, products2.Length);
        Assert.Equal(new[] { copy }, Directory.GetFiles(directory.Path).Where(file => !file.EndsWith(".lock")).ToArray());
    }
}
