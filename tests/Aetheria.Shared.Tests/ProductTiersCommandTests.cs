using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's product-lines tiers command (cut product-tiers; rulings maker-design-tiers-allowed, bin-share-by-floor), run on a
// scratch copy of the shipped catalog with the table's bins removed from it. A standalone class: a tier row is not a Row.
public sealed class ProductTiersCommandTests : IDisposable
{
    private static readonly string[] Apply = { "tiers", "apply" };
    private static readonly string[] DryRun = { "tiers" };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "product-tiers-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");
    private readonly string[] _names = ProductLinesCommand.TierTable().Select(r => r.Name).ToArray();

    public ProductTiersCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        using var cache = Open();
        foreach (var bin in cache.GetAll<FactionProductData>().Where(p => _names.Contains(p.Name)).ToArray()) Assert.True(cache.Remove(cache.RefOf(bin).Key));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private CultCache Open()
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        return cache;
    }

    private Dictionary<string, CultPersistedRecord> Records() =>
        CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(CatalogPath)).Records.ToDictionary(r => r.Key, StringComparer.Ordinal);

    private FactionProductData[] Bins()
    {
        using var cache = Open();
        return cache.GetAll<FactionProductData>().Where(p => _names.Contains(p.Name)).ToArray();
    }

    // Edits the scratch catalog before a refusal case; the bins the table authors stay out of it.
    private void Edit(Action<CultCache> edit)
    {
        using var cache = Open();
        edit(cache);
        cache.FlushAsync().Wait();
    }

    // The command refuses the row with a message naming the rule, and the catalog's bytes are as they were.
    private void Refused(ProductLinesCommand.TierRow row, string phrase)
    {
        var before = File.ReadAllBytes(CatalogPath);
        var error = Assert.Throws<InvalidOperationException>(() => ProductLinesCommand.RunTiers(new[] { row }, true, _root));
        Assert.Contains(phrase, error.Message);
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_the_table_once_and_replays_as_a_no_op()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Bins());

        Assert.Equal(0, ProductLinesCommand.Run(DryRun, _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));

        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        Assert.Equal(6, Bins().Length);

        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, ProductLinesCommand.Run(DryRun, _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void Each_bin_is_its_lines_design_and_maker_at_its_floor_with_nothing_of_its_own()
    {
        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        using var cache = Open();
        foreach (var row in ProductLinesCommand.TierTable())
        {
            var bin = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Name == row.Name);
            var line = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Name == row.Line);
            Assert.Equal(cache.RefOf(line).Key, bin.Bin.Line.Key);
            Assert.Equal(line.Design.Key, bin.Design.Key);
            Assert.Equal(line.Manufacturer.Key, bin.Manufacturer.Key);
            Assert.Equal(row.Floor, bin.Bin.Floor);
            Assert.Equal(row.Description, bin.Description);
            Assert.Empty(bin.Roles);
            Assert.Null(bin.Assembly);
            Assert.Null(line.Bin);
        }
    }

    [Fact]
    public void Apply_writes_only_the_records_it_authors()
    {
        var before = Records();
        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        var after = Records();
        Assert.Equal(before.Count + 6, after.Count);
        foreach (var (key, record) in before)
        {
            Assert.True(after.TryGetValue(key, out var kept), "a record the command did not author is gone");
            Assert.Equal(record.StoredAt, kept.StoredAt);
            Assert.True(record.Payload.AsSpan().SequenceEqual(kept.Payload), "a record the command did not author has changed bytes");
        }
    }

    [Fact]
    public void A_bin_already_authored_at_another_floor_is_refused()
    {
        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        Refused(new ProductLinesCommand.TierRow("Sparrow", "Goldcrest", .5f, "x"), "already belongs");
    }

    [Fact]
    public void A_bin_already_authored_on_another_line_is_refused()
    {
        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        Refused(new ProductLinesCommand.TierRow("Spotlight", "Goldcrest", .67f, "x"), "already belongs");
    }

    [Fact]
    public void A_line_the_catalog_does_not_hold_is_refused()
    {
        Refused(new ProductLinesCommand.TierRow("No Such Line", "Fresh Bin", 0f, "x"), "found 0");
    }

    [Fact]
    public void An_ambiguous_line_is_refused()
    {
        Edit(cache =>
        {
            var sparrow = cache.GetAll<FactionProductData>().Single(p => p.Name == "Sparrow");
            cache.Upsert(new FactionProductData { Name = "Sparrow", Design = sparrow.Design, Manufacturer = sparrow.Manufacturer });
        });
        Refused(new ProductLinesCommand.TierRow("Sparrow", "Fresh Bin", 0f, "x"), "found 2");
    }

    [Fact]
    public void A_line_that_is_itself_a_bin_is_refused()
    {
        Assert.Equal(0, ProductLinesCommand.Run(Apply, _root));
        Refused(new ProductLinesCommand.TierRow("Goldcrest", "Fresh Bin", 0f, "x"), "itself a bin");
    }

    [Theory]
    [InlineData(-.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void A_floor_that_is_negative_or_not_finite_is_refused(float floor)
    {
        Refused(new ProductLinesCommand.TierRow("Sparrow", "Fresh Bin", floor, "x"), "finite number");
    }

    [Theory]
    [InlineData("Spotlight")]
    [InlineData("SPOTLIGHT")]
    public void A_bin_name_held_by_another_product_is_refused_ignoring_case(string name)
    {
        Refused(new ProductLinesCommand.TierRow("Sparrow", name, 0f, "x"), "already belongs");
    }

    [Fact]
    public void A_bin_name_that_is_a_designs_is_refused()
    {
        string design;
        using (var cache = Open()) design = cache.GetAll<CraftedItemData>().First().Name;
        Refused(new ProductLinesCommand.TierRow("Sparrow", design.ToUpperInvariant(), 0f, "x"), "name of a design");
    }
}
