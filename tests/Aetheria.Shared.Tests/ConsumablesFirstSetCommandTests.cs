using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's consumables-first-set command (aetheria-release, cut consumables-first-set), run on a scratch copy of the
// shipped catalog with the two designs and their four products removed from it.
public sealed class ConsumablesFirstSetCommandTests : IDisposable
{
    private static readonly string[] DesignNames = { "Thruster Overdrive", "Coolant Vent" };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "consumables-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    public ConsumablesFirstSetCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        // Stripped through a registry scoped to the shipped assembly, as PiratesFactionCommandTests does.
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        var designs = cache.GetAll<ConsumableItemData>().Where(c => DesignNames.Contains(c.Name)).ToArray();
        Assert.Equal(DesignNames.Length, designs.Length);
        var keys = designs.Select(d => cache.RefOf(d).Key).ToArray();
        foreach (var product in cache.GetAll<FactionProductData>().Where(p => keys.Any(k => p.Design.Key.Equals(k))).ToArray())
            Assert.True(cache.Remove(cache.RefOf(product).Key));
        foreach (var key in keys) Assert.True(cache.Remove(key));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private (ConsumableItemData[] Designs, FactionProductData[] Products) Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var designs = db.Cache.GetAll<ConsumableItemData>().Where(c => DesignNames.Contains(c.Name)).ToArray();
            var products = db.Cache.GetAll<FactionProductData>().Where(p => designs.Any(d => p.Design.Key.Equals(db.Cache.RefOf(d).Key))).ToArray();
            return (designs, products);
        }
        finally { db.Cache.Dispose(); }
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_the_designs_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Read().Designs);

        Assert.Equal(0, Program.ConsumablesFirstSetCatalog(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Read().Designs);

        Assert.Equal(0, Program.ConsumablesFirstSetCatalog(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var (designs, products) = Read();
        Assert.Equal(DesignNames.OrderBy(n => n, StringComparer.Ordinal), designs.Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(4, products.Length);

        // A second apply finds both designs authored and writes nothing; so does a dry run.
        Assert.Equal(0, Program.ConsumablesFirstSetCatalog(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, Program.ConsumablesFirstSetCatalog(apply: false, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        var (designsAgain, productsAgain) = Read();
        Assert.Equal(2, designsAgain.Length);
        Assert.Equal(4, productsAgain.Length);
    }
}
