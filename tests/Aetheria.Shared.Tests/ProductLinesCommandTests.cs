using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's product-lines command (cuts product-lines-weapons and gear-lines), run per line on a scratch copy of the shipped
// catalog with that line's products removed from it. Apply must write what the shipped catalog holds, only its own records,
// land on an older-schema catalog, and accept a maker that already sells the design. The weapon line adds the tests that
// need weapon rows by name; the gear line adds the cargo-bay exemption from the no-roles refusal.
public abstract class ProductLinesCommandTests : IDisposable
{
    protected readonly string[] Names;
    protected readonly string[] Line;
    protected readonly string[] LineApply;

    protected readonly string _root = Path.Combine(Path.GetTempPath(), "product-lines-command-" + Guid.NewGuid().ToString("N"));
    protected string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    // The rows as the shipped catalog holds them, taken before they are removed from the scratch copy.
    protected readonly string[] _shipped;
    private readonly string[] _shippedKeys;

    private static string Describe(CultCache cache, FactionProductData product) =>
        $"{cache.Get(product.Design).Name} | {product.Name} | {cache.Get(product.Manufacturer).ShortName} | {product.Description} | " +
        string.Join(",", product.Roles.OrderBy(r => r.Role, StringComparer.Ordinal).Select(r => $"{r.Role}:{r.Mean}:{r.StandardDeviation}"));

    protected ProductLinesCommandTests(string line)
    {
        Names = ProductLinesCommand.Table(line).Select(r => r.Name).ToArray();
        Line = new[] { line };
        LineApply = new[] { line, "apply" };
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        using var cache = Open();
        var products = cache.GetAll<FactionProductData>().Where(p => Names.Contains(p.Name)).ToArray();
        Assert.Equal(Names.Length, products.Length);
        _shipped = products.Select(p => Describe(cache, p)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        _shippedKeys = products.Select(p => cache.RefOf(p).Key.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        foreach (var product in products) Assert.True(cache.Remove(cache.RefOf(product).Key));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    protected CultCache Open()
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        return cache;
    }

    protected string[] Line20()
    {
        using var cache = Open();
        return cache.GetAll<FactionProductData>().Where(p => Names.Contains(p.Name)).Select(p => Describe(cache, p)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private string[] Line20Keys()
    {
        using var cache = Open();
        return cache.GetAll<FactionProductData>().Where(p => Names.Contains(p.Name)).Select(p => cache.RefOf(p).Key.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_the_shipped_rows_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Line20());

        Assert.Equal(0, ProductLinesCommand.Run(Line, _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Line20());

        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        // The command derives each record's key from the product's name, so applying the table again lands the keys the
        // shipped catalog holds.
        Assert.Equal(_shipped, Line20());
        Assert.Equal(_shippedKeys, Line20Keys());

        // A second apply finds everything authored and writes nothing; so does a dry run.
        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, ProductLinesCommand.Run(Line, _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    // Re-spell every record's payload in a longer, equally valid MessagePack form (an array16 header for the fixarray one). A
    // write that re-encodes records it did not author shows as changed bytes; one that lands onto the file as it is leaves them.
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

        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));

        var after = Records();
        Assert.Equal(before.Count + Names.Length, after.Count);
        foreach (var (key, record) in before)
        {
            Assert.True(after.TryGetValue(key, out var kept), "a record the command did not author is gone");
            Assert.Equal(record.SchemaId, kept.SchemaId);
            Assert.Equal(record.StoredAt, kept.StoredAt);
            Assert.True(record.Payload.AsSpan().SequenceEqual(kept.Payload), "a record the command did not author has changed bytes");
        }
    }

    // Ruling maker-design-tiers-allowed: a maker that already sells a design may sell another product of it.
    [Fact]
    public void Apply_accepts_a_maker_that_already_sells_the_design()
    {
        using (var cache = Open())
        {
            var laser = cache.GetAll<WeaponItemData>().First(w => w.Name == "Light Laser");
            var lucent = cache.GetAll<Faction>().First(f => f.ShortName == "Lucent");
            cache.Upsert(new FactionProductData { Name = "Scratch tier", Design = new CultRecordRef<CraftedItemData>(cache.RefOf(laser).Key), Manufacturer = cache.RefOf(lucent) });
            cache.FlushAsync().Wait();
        }

        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(_shipped, Line20());
        using var after = Open();
        Assert.Contains(after.GetAll<FactionProductData>(), p => p.Name == "Scratch tier");
    }

    // Master's committed catalog holds records written on a schema older than today's (HullData lost a member after the last
    // catalog write). The scratch copy gets that shape: the hull records point at an earlier schema id the loader resolves to
    // the current type.
    private string MakeHullsStale()
    {
        var snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(CatalogPath));
        var current = snapshot.SchemaCatalog.Single(e => e.SchemaName == "aetheria.hulldata");
        var stale = new CultSchemaCatalogEntry
        {
            SchemaId = "sha256:earlier-hulldata", SchemaName = current.SchemaName, SchemaVersion = current.SchemaVersion,
            ContentHash = "sha256:earlier-hulldata", CanonicalSchemaJson = current.CanonicalSchemaJson, Members = current.Members,
        };
        snapshot.SchemaCatalog = snapshot.SchemaCatalog.Append(stale).ToArray();
        var hulls = snapshot.Records.Where(r => r.SchemaId == current.SchemaId).ToArray();
        Assert.NotEmpty(hulls);
        foreach (var hull in hulls) hull.SchemaId = stale.SchemaId;
        File.WriteAllBytes(CatalogPath, CultDocumentMessagePackSerialization.SerializeSnapshot(snapshot));
        return stale.SchemaId;
    }

    [Fact]
    public void Apply_lands_on_a_catalog_that_holds_records_of_an_older_schema_and_replays_to_the_same_bytes()
    {
        var stale = MakeHullsStale();
        var before = Records();

        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.Equal(_shipped, Line20());
        var after = Records();
        foreach (var (key, record) in before)
            Assert.True(record.Payload.AsSpan().SequenceEqual(after[key].Payload) && record.SchemaId == after[key].SchemaId, "a record the command did not author changed");
        Assert.Contains(after.Values, r => r.SchemaId == stale);

        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

}

public sealed class WeaponLineCommandTests : ProductLinesCommandTests
{
    public WeaponLineCommandTests() : base("weapons") { }

    [Fact]
    public void Apply_finishes_a_catalog_that_holds_part_of_the_line()
    {
        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        using (var cache = Open())
        {
            foreach (var product in cache.GetAll<FactionProductData>().Where(p => p.Name == "Merlin" || p.Name == "Tripwire").ToArray())
                Assert.True(cache.Remove(cache.RefOf(product).Key));
            cache.FlushAsync().Wait();
        }
        Assert.Equal(Names.Length - 2, Line20().Length);

        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(_shipped, Line20());
    }

    [Fact]
    public void Apply_refuses_a_row_name_that_is_another_products_and_writes_nothing()
    {
        using (var cache = Open())
        {
            var other = cache.GetAll<WeaponItemData>().First(w => w.Name == "Light Machine Gun");
            var maker = cache.GetAll<Faction>().First(f => f.ShortName == "Zhestokost");
            cache.Upsert(new FactionProductData { Name = "Sparrow", Design = new CultRecordRef<CraftedItemData>(cache.RefOf(other).Key), Manufacturer = cache.RefOf(maker) });
            cache.FlushAsync().Wait();
        }
        var conflicted = File.ReadAllBytes(CatalogPath);

        Assert.Throws<InvalidOperationException>(() => ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(conflicted, File.ReadAllBytes(CatalogPath));
    }

    private void AddProduct(string name, string design, string maker)
    {
        using var cache = Open();
        var designRecord = cache.GetAll<WeaponItemData>().First(w => w.Name == design);
        var makerRecord = cache.GetAll<Faction>().First(f => f.ShortName == maker);
        cache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(cache.RefOf(designRecord).Key), Manufacturer = cache.RefOf(makerRecord) });
        cache.FlushAsync().Wait();
    }

    // Sparrow is the Light Laser by Finch. A product of that name is the row only when design and maker both match.
    [Theory]
    [InlineData("Sparrow", "Light Machine Gun", "Finch")]
    [InlineData("Sparrow", "Light Laser", "Zhestokost")]
    [InlineData("sparrow", "Light Laser", "Finch")]
    public void Apply_refuses_a_name_unless_design_and_maker_both_match_it_ignoring_case(string name, string design, string maker)
    {
        AddProduct(name, design, maker);
        var conflicted = File.ReadAllBytes(CatalogPath);

        Assert.Throws<InvalidOperationException>(() => ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(conflicted, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void Apply_refuses_a_row_name_that_is_a_designs_and_writes_nothing()
    {
        using (var cache = Open())
        {
            cache.Upsert(new WeaponItemData { Name = "Tripwire" });
            cache.FlushAsync().Wait();
        }
        var conflicted = File.ReadAllBytes(CatalogPath);

        Assert.Throws<InvalidOperationException>(() => ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(conflicted, File.ReadAllBytes(CatalogPath));
    }

    // The command is reached as AetherDb's product-lines word, with the line after it; the shipped catalog holds the line.
    [Fact]
    public void The_product_lines_word_runs_the_named_line_and_refuses_an_unknown_one()
    {
        Assert.Equal(0, Program.Main(new[] { "product-lines", "weapons" }));
        Assert.Equal(1, Program.Main(new[] { "product-lines", "nonesuch" }));
    }
}

public sealed class GearLineCommandTests : ProductLinesCommandTests
{
    public GearLineCommandTests() : base("gear") { }

    // Cargo bays carry no roles (ruling catalog-breadth-roles-and-components): their product lands with none. Any other design
    // with no roles is still refused, with nothing written.
    [Fact]
    public void Apply_accepts_a_roleless_cargo_bay_and_refuses_a_roleless_gear_design()
    {
        using (var cache = Open())
        {
            var radiator = cache.GetAll<GearData>().Single(g => g.Name == "Radiator");
            radiator.Roles.Clear();
            cache.Upsert(radiator);
            cache.FlushAsync().Wait();
        }
        var conflicted = File.ReadAllBytes(CatalogPath);
        Assert.Throws<InvalidOperationException>(() => ProductLinesCommand.Run(LineApply, _root));
        Assert.Equal(conflicted, File.ReadAllBytes(CatalogPath));

        using (var cache = Open())
        {
            var radiator = cache.GetAll<GearData>().Single(g => g.Name == "Radiator");
            radiator.Roles.Add(new ItemRole { Name = "Cooling" });
            cache.Upsert(radiator);
            cache.FlushAsync().Wait();
        }
        Assert.Equal(0, ProductLinesCommand.Run(LineApply, _root));
        using var after = Open();
        var pithos = Assert.Single(after.GetAll<FactionProductData>(), p => p.Name == "Pithos");
        Assert.IsType<CargoBayData>(after.Get(pithos.Design));
        Assert.Empty(pithos.Roles);
    }
}
