using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's generic-designs command (aetheria-release, cut generic-design-names), run on a scratch copy of the shipped
// catalog, which already carries the applied result: each case first puts a few records back to their old shape.
public sealed class GenericDesignsCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "generic-designs-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    public GenericDesignsCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void Edit(Action<AetherDb> edit)
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        try { edit(db); }
        finally { db.Cache.Dispose(); }
    }

    private T Read<T>(Func<AetherDb, T> read)
    {
        var db = AetherDb.Open(root: _root);
        try { return read(db); }
        finally { db.Cache.Dispose(); }
    }

    private static void Save(AetherDb db, EquippableItemData design) =>
        db.Cache.Commit(batch => batch.Upsert(design.GetType(), design, db.Cache.RefOf(design).Key));

    private static void Save(AetherDb db, FactionProductData product) =>
        db.Cache.Commit(batch => batch.Upsert(typeof(FactionProductData), product, db.Cache.RefOf(product).Key));

    private static EquippableItemData DesignNamed(AetherDb db, string name) =>
        db.Cache.GetAll<EquippableItemData>().Single(d => d.Name == name);

    private static FactionProductData ProductNamed(AetherDb db, string name) =>
        db.Cache.GetAll<FactionProductData>().Single(p => p.Name == name);

    [Fact]
    public void An_applied_catalog_is_left_alone_by_a_dry_run_and_by_apply()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Equal(0, Program.GenericDesigns(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, Program.GenericDesigns(apply: true, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_restores_both_records()
    {
        var (designText, productText, brandedDesignText, rowText) = Read(db => (DesignNamed(db, "Light Machine Gun").Description,
            ProductNamed(db, "Hammerfall").Description, DesignNamed(db, "Autocannon").Description, ProductNamed(db, "Skiron").Description));
        Edit(db =>
        {
            var design = DesignNamed(db, "Light Machine Gun");
            design.Name = "ClearPath";
            design.Description = "Defend your precious cargo.";
            Save(db, design);
            var product = ProductNamed(db, "Hammerfall");
            product.Name = "Autocannon";
            product.Description = "";
            Save(db, product);
            var branded = DesignNamed(db, "Autocannon");
            branded.Description = "";
            Save(db, branded);
            var row = ProductNamed(db, "Skiron");
            row.Description = "";
            Save(db, row);
        });
        var reverted = File.ReadAllBytes(CatalogPath);

        Assert.Equal(0, Program.GenericDesigns(apply: false, root: _root));
        Assert.Equal(reverted, File.ReadAllBytes(CatalogPath));

        Assert.Equal(0, Program.GenericDesigns(apply: true, root: _root));
        Read(db =>
        {
            Assert.Equal(designText, DesignNamed(db, "Light Machine Gun").Description);
            Assert.DoesNotContain(db.Cache.GetAll<EquippableItemData>(), d => d.Name == "ClearPath");
            Assert.Equal(productText, ProductNamed(db, "Hammerfall").Description);
            Assert.Equal(brandedDesignText, DesignNamed(db, "Autocannon").Description);
            Assert.Equal(rowText, ProductNamed(db, "Skiron").Description);
            Assert.DoesNotContain(db.Cache.GetAll<FactionProductData>(), p => p.Name == "Autocannon" && db.Cache.Get(p.Manufacturer)?.ShortName == "Zhestokost");
            return 0;
        });
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
    public void Apply_writes_only_the_records_it_authors_and_restores_the_rewritten_design_texts()
    {
        var (largeDriveText, tractorText, smokeText, tractorProductText) = Read(db => (DesignNamed(db, "Large Drive").Description, DesignNamed(db, "Tractor Beam").Description,
            DesignNamed(db, "Smoke Proximity").Description, ProductNamed(db, "Space Tractor").Description));
        Edit(db =>
        {
            var drive = DesignNamed(db, "Large Drive");
            drive.Description = "Keeps you going and going and going...";
            Save(db, drive);
            var smoke = DesignNamed(db, "Smoke Proximity");
            smoke.Description = "A Spectra that bursts by proximity, blast 8. For scenarios; no one builds it.";
            Save(db, smoke);
            var tractor = ProductNamed(db, "Space Tractor");
            tractor.Description = "It's a tractor. In space.";
            Save(db, tractor);
        });
        Assert.True(WidenEveryPayload() > 100, "the scratch catalog should hold many records in the widened spelling");
        var before = Records();

        Assert.Equal(0, Program.GenericDesigns(apply: true, root: _root));

        var after = Records();
        Assert.Equal(before.Count, after.Count);
        var changed = before.Keys.Where(key => !before[key].Payload.AsSpan().SequenceEqual(after[key].Payload)).ToArray();
        Assert.Equal(3, changed.Length);
        foreach (var (key, record) in before.Where(pair => !changed.Contains(pair.Key)))
            Assert.True(record.Payload.AsSpan().SequenceEqual(after[key].Payload), "a record the command did not author has changed bytes");
        Read(db =>
        {
            Assert.Equal(largeDriveText, DesignNamed(db, "Large Drive").Description);
            Assert.Equal(tractorText, DesignNamed(db, "Tractor Beam").Description);
            Assert.Equal(smokeText, DesignNamed(db, "Smoke Proximity").Description);
            Assert.Equal(tractorProductText, ProductNamed(db, "Space Tractor").Description);
            return 0;
        });
    }

    [Fact]
    public void A_generic_name_held_by_another_design_is_refused_and_nothing_is_written()
    {
        Edit(db =>
        {
            var cargo = DesignNamed(db, "Cargo Bay");
            cargo.Name = "Store-All Plus";
            Save(db, cargo);
            var flak = DesignNamed(db, "Flak Gun");
            flak.Name = "Cargo Bay";
            Save(db, flak);
        });
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Throws<InvalidOperationException>(() => Program.GenericDesigns(apply: true, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void A_brand_already_held_by_another_product_is_refused()
    {
        Edit(db =>
        {
            var product = ProductNamed(db, "Hammerfall");
            product.Name = "Autocannon";
            Save(db, product);
            var other = ProductNamed(db, "Wren");
            other.Name = "Hammerfall";
            Save(db, other);
        });
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Throws<InvalidOperationException>(() => Program.GenericDesigns(apply: true, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void A_row_whose_old_and_generic_names_are_both_absent_is_refused()
    {
        Edit(db =>
        {
            var gun = DesignNamed(db, "Light Machine Gun");
            gun.Name = "Neither Name";
            Save(db, gun);
        });
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Throws<InvalidOperationException>(() => Program.GenericDesigns(apply: true, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void A_brand_row_whose_maker_sells_no_product_of_the_design_under_either_name_is_refused()
    {
        Edit(db =>
        {
            var product = ProductNamed(db, "Hammerfall");
            product.Name = "Neither Name";
            Save(db, product);
        });
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Throws<InvalidOperationException>(() => Program.GenericDesigns(apply: true, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void A_shipped_authoring_note_is_stripped_from_a_product_the_tables_do_not_name()
    {
        Edit(db =>
        {
            var product = ProductNamed(db, "LockOn");
            product.Description += Program.AuthoringNote;
            Save(db, product);
        });
        Assert.Equal(0, Program.GenericDesigns(apply: true, root: _root));
        Read(db =>
        {
            Assert.EndsWith("target.", ProductNamed(db, "LockOn").Description);
            return 0;
        });
    }
}
