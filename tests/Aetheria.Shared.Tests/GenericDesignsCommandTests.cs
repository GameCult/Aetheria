using System;
using System.IO;
using System.Linq;
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
