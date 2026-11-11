using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using Xunit;

// The CultLib pin that requires element ids (abd1a92e) reads the stores written before it (b8fc5eb0 and older), which carry none.
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
}
