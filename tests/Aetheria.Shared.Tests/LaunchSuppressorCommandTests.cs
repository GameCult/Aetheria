using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// AetherDb's launch-suppressor command (aetheria-ew, cut ew-hide-launch-catalog), run on a scratch copy of the shipped catalog with
// the two designs and their six products removed from it. Apply must write what the shipped catalog holds, and a catalog that is
// neither untouched nor fully authored is refused rather than completed.
public sealed class LaunchSuppressorCommandTests : IDisposable
{
    private static readonly string[] DesignNames = { LaunchSuppressorCatalogTests.StrongName, LaunchSuppressorCatalogTests.MildName };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "launch-suppressor-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    // The designs and products as the shipped catalog holds them, taken before they are removed from the scratch copy.
    private readonly (string Name, byte[] Bytes)[] _shippedDesigns;
    private readonly string[] _shippedProducts;

    private static byte[] Pack(GearData design) =>
        MessagePackSerializer.Serialize(design, CultDocumentMessagePackSerialization.OptionsFor(typeof(GearData).Assembly));

    private static string Describe(CultCache cache, FactionProductData product) =>
        $"{cache.Get(product.Design).Name} | {product.Name} | {cache.Get(product.Manufacturer).ShortName} | {product.Description} | " +
        string.Join(",", product.Roles.OrderBy(r => r.Role, StringComparer.Ordinal).Select(r => $"{r.Role}:{r.Mean}:{r.StandardDeviation}"));

    public LaunchSuppressorCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        var designs = cache.GetAll<GearData>().Where(g => DesignNames.Contains(g.Name)).ToArray();
        Assert.Equal(DesignNames.Length, designs.Length);
        _shippedDesigns = designs.Select(d => (d.Name, Pack(d))).ToArray();
        var keys = designs.Select(d => cache.RefOf(d).Key).ToArray();
        var products = cache.GetAll<FactionProductData>().Where(p => keys.Any(k => p.Design.Key.Equals(k))).ToArray();
        _shippedProducts = products.Select(p => Describe(cache, p)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(6, products.Length);
        foreach (var product in products) Assert.True(cache.Remove(cache.RefOf(product).Key));
        foreach (var key in keys) Assert.True(cache.Remove(key));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private (GearData[] Designs, string[] Products) Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var designs = db.Cache.GetAll<GearData>().Where(g => DesignNames.Contains(g.Name)).ToArray();
            var keys = designs.Select(d => db.Cache.RefOf(d).Key).ToArray();
            var products = db.Cache.GetAll<FactionProductData>().Where(p => keys.Any(k => p.Design.Key.Equals(k))).ToArray();
            return (designs, products.Select(p => Describe(db.Cache, p)).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
        finally { db.Cache.Dispose(); }
    }

    // Remove from the scratch catalog what the predicate picks: products by name, designs by name.
    private void Remove(Func<FactionProductData, bool> products, Func<GearData, bool> designs)
    {
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        foreach (var product in cache.GetAll<FactionProductData>().Where(products).ToArray()) Assert.True(cache.Remove(cache.RefOf(product).Key));
        foreach (var design in cache.GetAll<GearData>().Where(designs).ToArray()) Assert.True(cache.Remove(cache.RefOf(design).Key));
        cache.FlushAsync().Wait();
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_the_shipped_designs_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Read().Designs);

        Assert.Equal(0, Program.LaunchSuppressorCatalog(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Read().Designs);

        Assert.Equal(0, Program.LaunchSuppressorCatalog(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var (designs, products) = Read();
        // The record keys are CultCache's to mint and differ run to run; the designs and products do not.
        foreach (var (name, bytes) in _shippedDesigns)
            Assert.True(bytes.AsSpan().SequenceEqual(Pack(designs.Single(d => d.Name == name))), $"{name} differs from the shipped catalog's record");
        Assert.Equal(_shippedProducts, products);

        // A second apply finds everything authored and writes nothing; so does a dry run.
        Assert.Equal(0, Program.LaunchSuppressorCatalog(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, Program.LaunchSuppressorCatalog(apply: false, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    public enum PartialState { OneDesignOnly, DesignWithSomeProducts, ProductsWithoutTheirDesign }

    [Theory]
    [InlineData(PartialState.OneDesignOnly)]
    [InlineData(PartialState.DesignWithSomeProducts)]
    [InlineData(PartialState.ProductsWithoutTheirDesign)]
    public void Apply_refuses_a_partly_authored_catalog_and_writes_nothing(PartialState state)
    {
        Assert.Equal(0, Program.LaunchSuppressorCatalog(apply: true, root: _root));
        switch (state)
        {
            case PartialState.OneDesignOnly:
                Remove(p => DesignOf(p) == DesignNames[1], g => g.Name == DesignNames[1]);
                break;
            case PartialState.DesignWithSomeProducts:
                Remove(p => p.Name == "Hush" || p.Name == "Low Profile", g => false);
                break;
            case PartialState.ProductsWithoutTheirDesign:
                Remove(p => false, g => g.Name == DesignNames[0]);
                break;
        }
        var partial = File.ReadAllBytes(CatalogPath);

        Assert.Throws<InvalidOperationException>(() => Program.LaunchSuppressorCatalog(apply: true, root: _root));
        Assert.Equal(partial, File.ReadAllBytes(CatalogPath));
    }

    private string DesignOf(FactionProductData p) => p.Name is "Low Profile" or "Fast Pass Dim" ? DesignNames[1] : DesignNames[0];
}
