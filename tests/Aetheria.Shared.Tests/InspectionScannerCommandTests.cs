using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// AetherDb's inspection-scanner command (cut scanner-catalog), run on a scratch copy of the shipped catalog with the design and
// its three products removed from it. Apply must write what the shipped catalog holds, only its own records, and a catalog that
// is neither untouched nor fully authored is refused rather than completed.
public sealed class InspectionScannerCommandTests : IDisposable
{
    private const string DesignName = InspectionScannerCatalogTests.DesignName;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "inspection-scanner-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    // The design and products as the shipped catalog holds them, taken before they are removed from the scratch copy.
    private readonly byte[] _shippedDesign;
    private readonly string[] _shippedProducts;

    private static byte[] Pack(GearData design) =>
        MessagePackSerializer.Serialize(design, CultDocumentMessagePackSerialization.OptionsFor(typeof(GearData).Assembly));

    private static string Describe(CultCache cache, FactionProductData product) =>
        $"{cache.Get(product.Design).Name} | {product.Name} | {cache.Get(product.Manufacturer).ShortName} | {product.Description} | " +
        string.Join(",", product.Roles.OrderBy(r => r.Role, StringComparer.Ordinal).Select(r => $"{r.Role}:{r.Mean}:{r.StandardDeviation}"));

    public InspectionScannerCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        var design = Assert.Single(cache.GetAll<GearData>(), g => g.Name == DesignName);
        _shippedDesign = Pack(design);
        var key = cache.RefOf(design).Key;
        var products = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(key)).ToArray();
        _shippedProducts = products.Select(p => Describe(cache, p)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(3, products.Length);
        foreach (var product in products) Assert.True(cache.Remove(cache.RefOf(product).Key));
        Assert.True(cache.Remove(key));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private (GearData[] Designs, string[] Products) Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var designs = db.Cache.GetAll<GearData>().Where(g => g.Name == DesignName).ToArray();
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
    public void A_dry_run_writes_nothing_and_apply_lands_the_shipped_design_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Read().Designs);

        Assert.Equal(0, Program.InspectionScannerCatalog(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Read().Designs);

        Assert.Equal(0, Program.InspectionScannerCatalog(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var (designs, products) = Read();
        // The record keys are CultCache's to mint and differ run to run; the design and products do not.
        Assert.True(_shippedDesign.AsSpan().SequenceEqual(Pack(Assert.Single(designs))), "the design differs from the shipped catalog's record");
        Assert.Equal(_shippedProducts, products);

        // A second apply finds everything authored and writes nothing; so does a dry run.
        Assert.Equal(0, Program.InspectionScannerCatalog(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, Program.InspectionScannerCatalog(apply: false, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    // Re-spell every record's payload in a longer, equally valid MessagePack form (an array16 header for the fixarray one). The
    // catalog then holds bytes no re-encode through the current types would reproduce, so a write that re-encodes records it did
    // not author shows as changed bytes; a write that lands onto the file as it is leaves them alone.
    private int WidenEveryPayload()
    {
        var snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(CatalogPath));
        var widened = 0;
        foreach (var record in snapshot.Records)
        {
            var payload = record.Payload;
            if (payload.Length == 0 || (payload[0] & 0xF0) != 0x90) continue;
            record.Payload = new byte[] { 0xDC, 0x00, (byte)(payload[0] & 0x0F) }.Concat(payload.Skip(1)).ToArray();
            widened++;
        }
        File.WriteAllBytes(CatalogPath, CultDocumentMessagePackSerialization.SerializeSnapshot(snapshot));
        return widened;
    }

    private Dictionary<string, CultPersistedRecord> Records() =>
        CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(CatalogPath)).Records.ToDictionary(r => r.Key, StringComparer.Ordinal);

    [Fact]
    public void Apply_writes_only_the_records_it_authors()
    {
        Assert.True(WidenEveryPayload() > 100, "the scratch catalog should hold many records in the widened spelling");
        var before = Records();

        Assert.Equal(0, Program.InspectionScannerCatalog(apply: true, root: _root));

        var after = Records();
        Assert.Equal(before.Count + 1 + _shippedProducts.Length, after.Count);
        foreach (var (key, record) in before)
        {
            Assert.True(after.TryGetValue(key, out var kept), "a record the command did not author is gone");
            Assert.Equal(record.SchemaId, kept.SchemaId);
            Assert.Equal(record.StoredAt, kept.StoredAt);
            Assert.True(record.Payload.AsSpan().SequenceEqual(kept.Payload), "a record the command did not author has changed bytes");
        }
    }

    public enum PartialState { DesignWithSomeProducts, ProductsWithoutTheirDesign, OneProductAlone }

    [Theory]
    [InlineData(PartialState.DesignWithSomeProducts)]
    [InlineData(PartialState.ProductsWithoutTheirDesign)]
    [InlineData(PartialState.OneProductAlone)]
    public void Apply_refuses_a_partly_authored_catalog_and_writes_nothing(PartialState state)
    {
        Assert.Equal(0, Program.InspectionScannerCatalog(apply: true, root: _root));
        switch (state)
        {
            case PartialState.DesignWithSomeProducts:
                Remove(p => p.Name == "Inkling" || p.Name == "Assay", g => false);
                break;
            case PartialState.ProductsWithoutTheirDesign:
                Remove(p => false, g => g.Name == DesignName);
                break;
            case PartialState.OneProductAlone:
                Remove(p => p.Name != "Assay", g => g.Name == DesignName);
                break;
        }
        var partial = File.ReadAllBytes(CatalogPath);

        Assert.Throws<InvalidOperationException>(() => Program.InspectionScannerCatalog(apply: true, root: _root));
        Assert.Equal(partial, File.ReadAllBytes(CatalogPath));
    }
}
