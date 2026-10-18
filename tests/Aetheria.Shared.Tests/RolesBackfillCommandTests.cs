using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's roles-backfill command (aetheria-release, cut roles-backfill) run on a scratch copy of the shipped catalog,
// with the designs it backfills put back to the state they shipped in before it: flat stats at the middle of their range,
// no roles, no seller quality, and the unnamed Quality terms of the stats that already varied. The shipped catalog is the
// oracle, since the command produced it.
public sealed class RolesBackfillCommandTests : IDisposable
{
    private static readonly string[] Flattened = { "Autocannon", "Long-Range Missile Rack", "Short-Range Missile Rack", "Mine Launcher", "Large Drive", "Small Drive" };

    // Designs that shipped with non-flat stats whose Quality term named no role; the field is the stat to unname.
    private static readonly (string Design, string Field)[] Unnamed =
    {
        ("Cluster Cannon", "Count"), ("Flak Gun", "Count"), ("Guided Missile Launcher", "MinRange"), ("Lightning Projector", "DamageSpread"),
        ("Muon Fusion Reactor", "Modifier"), ("Muon Fusion Station Reactor", "Modifier"),
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "roles-backfill-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");
    private readonly Dictionary<string, string> _shipped = new Dictionary<string, string>();

    public RolesBackfillCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        File.Copy(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), CatalogPath);
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        var products = cache.GetAll<FactionProductData>().ToArray();
        foreach (var design in cache.GetAll<EquippableItemData>().Where(d => Flattened.Contains(d.Name) || Unnamed.Any(u => u.Design == d.Name)))
            _shipped[design.Name] = Describe(cache, design, products);
    }

    // A registry scoped to the shipped assembly, so this test assembly's own documents never reach the real catalog.
    // Puts the named designs back to the state they shipped in before the backfill, through a registry scoped to the shipped
    // assembly (as PiratesFactionCommandTests strips its faction).
    private void Reset(bool flattened, bool unnamed)
    {
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        var designs = cache.GetAll<EquippableItemData>().ToArray();
        var products = cache.GetAll<FactionProductData>().ToArray();
        var changed = new List<object>();
        if (flattened)
            foreach (var design in designs.Where(d => Flattened.Contains(d.Name)))
            {
                foreach (var (_, stat) in CatalogRoleTests.StatsOf(design))
                {
                    if (stat.Min != stat.Max) stat.Min = stat.Max = (stat.Min + stat.Max) / 2f;
                    // The drives shipped with unnamed Quality terms on their flat stats (roles-migrate); the command keeps those
                    // exponents and names them. The other four shipped with no term at all.
                    if (design.Name.EndsWith(" Drive", StringComparison.Ordinal)) foreach (var term in stat.Terms.Where(t => t.Source == StatSource.Quality)) term.Role = null;
                    else stat.Terms.RemoveAll(t => t.Source == StatSource.Quality);
                }
                design.Roles = new List<ItemRole>();
                changed.Add(design);
                // Small Drive shipped with its sellers' quality already authored (roles-migrate); only its stats were flat.
                foreach (var product in products.Where(p => design.Name != "Small Drive" && p.Design.Key.Equals(cache.RefOf(design).Key)))
                {
                    product.Roles = new List<ProductRole>();
                    changed.Add(product);
                }
            }
        if (unnamed)
            foreach (var (name, field) in Unnamed)
            {
                var design = designs.Single(d => d.Name == name);
                foreach (var term in Stat(design, field).Terms.Where(t => t.Source == StatSource.Quality)) term.Role = null;
                changed.Add(design);
            }
        if (changed.Count > 0)
            cache.Commit(batch =>
            {
                foreach (var document in changed) batch.Upsert(document.GetType(), document, cache.RefOf(document).Key);
            });
        cache.FlushAsync().Wait();
        // Then the record AetherDb's process-wide registry demands in this test host (as PiratesFactionCommandTests).
        using (var plain = new CultCache())
        {
            plain.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
            plain.Upsert(new VerseGrammar { Revision = 1 });
            plain.FlushAsync().Wait();
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static PerformanceStat Stat(EquippableItemData design, string field) =>
        Assert.Single(CatalogRoleTests.StatsOf(design), s => s.Name.EndsWith("." + field)).Stat;

    // Everything the command decides about one design: its roles, each stat's range, role and exponent, and its sellers' role quality.
    private static string Describe(CultCache cache, EquippableItemData design, FactionProductData[] products)
    {
        static string F(float v) => float.IsInfinity(v) ? v.ToString() : ((double) v).ToString("G5");
        var stats = CatalogRoleTests.StatsOf(design).Select(s =>
            $"{s.Name} {F(s.Stat.Min)}..{F(s.Stat.Max)} {string.Join("+", s.Stat.Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => $"{t.Role}^{F(t.Exponent)}"))}");
        var sellers = products.Where(p => p.Design.Key.Equals(cache.RefOf(design).Key)).OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}: " + string.Join(", ", (p.Roles ?? new List<ProductRole>()).OrderBy(r => r.Role, StringComparer.Ordinal)
                .Select(r => $"{r.Role} {F(r.Mean)}/{F(r.StandardDeviation)}")));
        var roles = "roles " + string.Join(", ", (design.Roles ?? new List<ItemRole>()).Select(r => r.Name).OrderBy(r => r, StringComparer.Ordinal));
        return string.Join("\n", new[] { roles }.Concat(stats).Concat(sellers));
    }

    private Dictionary<string, string> Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var products = db.Cache.GetAll<FactionProductData>().ToArray();
            return db.Cache.GetAll<EquippableItemData>().Where(d => _shipped.ContainsKey(d.Name))
                .ToDictionary(d => d.Name, d => Describe(db.Cache, d, products));
        }
        finally { db.Cache.Dispose(); }
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_what_the_catalog_ships_and_replays_as_a_no_op()
    {
        Reset(flattened: true, unnamed: true);
        var before = File.ReadAllBytes(CatalogPath);
        var start = Read();
        Assert.All(_shipped.Keys, name => Assert.NotEqual(_shipped[name], start[name]));

        Assert.Equal(0, Program.RolesBackfill(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));

        Assert.Equal(0, Program.RolesBackfill(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var after = Read();
        foreach (var name in _shipped.Keys.OrderBy(n => n, StringComparer.Ordinal))
            Assert.Equal(_shipped[name], after[name]);

        // A second apply finds every stat ranged and named and every seller authored, and writes nothing.
        Assert.Equal(0, Program.RolesBackfill(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    // Only unnamed Quality terms to name: no seller and no range changes, and apply must still land the designs.
    [Fact]
    public void Naming_an_unnamed_term_alone_lands_without_touching_ranges_or_sellers()
    {
        Reset(flattened: false, unnamed: true);
        var before = File.ReadAllBytes(CatalogPath);
        var start = Read();
        Assert.All(Unnamed.Select(u => u.Design), name => Assert.NotEqual(_shipped[name], start[name]));

        Assert.Equal(0, Program.RolesBackfill(apply: true, root: _root));
        Assert.NotEqual(before, File.ReadAllBytes(CatalogPath));
        var after = Read();
        foreach (var (name, _) in Unnamed) Assert.Equal(_shipped[name], after[name]);
    }

    // The exponents a first apply authors do not depend on which designs it targets: applied to the base-shaped catalog, the
    // command reproduces the shipped exponents of Autocannon and LRMM72, which differ across stats (the mode is not one value).
    [Fact]
    public void Apply_on_the_base_shaped_catalog_reproduces_the_shipped_exponents()
    {
        static Dictionary<string, float> Exponents(EquippableItemData d) => CatalogRoleTests.StatsOf(d)
            .SelectMany(s => s.Stat.Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => (s.Name, t.Exponent)))
            .ToDictionary(x => x.Name, x => x.Exponent);
        var designs = new[] { "Autocannon", "Long-Range Missile Rack" };
        Dictionary<string, Dictionary<string, float>> shipped;
        Dictionary<string, Dictionary<string, float>> Read(EquippableItemData[] all) => all.Where(d => designs.Contains(d.Name)).ToDictionary(d => d.Name, Exponents);
        using (var catalog = RestoredHullsTests.OpenCatalog()) shipped = Read(catalog.GetAll<EquippableItemData>().ToArray());
        Assert.All(designs, name => Assert.True(shipped[name].Values.Distinct().Count() > 1, name + " exponents are one value; the test would not tell a mode from a default"));

        Reset(flattened: true, unnamed: true);
        Assert.Equal(0, Program.RolesBackfill(apply: true, root: _root));
        var db = AetherDb.Open(root: _root);
        Dictionary<string, Dictionary<string, float>> after;
        try { after = Read(db.Cache.GetAll<EquippableItemData>().ToArray()); }
        finally { db.Cache.Dispose(); }
        foreach (var name in designs) Assert.Equal(shipped[name].OrderBy(x => x.Key, StringComparer.Ordinal), after[name].OrderBy(x => x.Key, StringComparer.Ordinal));
    }

    // A seller that authors one of a design's two roles gets only the other one added, and keeps the one it authored.
    [Fact]
    public void A_seller_with_one_of_two_roles_authored_gains_only_the_missing_role()
    {
        Reset(flattened: false, unnamed: false);
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var autocannon = db.Cache.GetAll<WeaponItemData>().Single(weapon => weapon.Name == "Autocannon");
        var seller = db.Cache.GetAll<FactionProductData>().OrderBy(p => p.Name, StringComparer.Ordinal).First(p => p.Design.Key.Equals(db.Cache.RefOf(autocannon).Key) && p.Roles.Count == 2);
        var kept = seller.Roles.OrderBy(r => r.Role, StringComparer.Ordinal).First();
        kept.Mean = .91f;
        seller.Roles = new List<ProductRole> { kept };
        var sellerName = seller.Name;
        var keptRole = kept.Role;
        db.Cache.Commit(batch => batch.Upsert(typeof(FactionProductData), seller, db.Cache.RefOf(seller).Key));
        db.Cache.Dispose();

        Assert.Equal(0, Program.RolesBackfill(apply: true, root: _root));
        db = AetherDb.Open(root: _root);
        try
        {
            var after = db.Cache.GetAll<FactionProductData>().Single(p => p.Name == sellerName);
            Assert.Equal(new[] { "barrel", "feed mechanism" }, after.Roles.Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray());
            Assert.Equal(.91f, after.Roles.Single(r => r.Role == keptRole).Mean, 3);
        }
        finally { db.Cache.Dispose(); }
    }

    // A bin authors no roles (ProductBin): the backfill that restores its line's missing roles leaves the bin's list empty.
    [Fact]
    public void A_binned_seller_gains_no_roles()
    {
        Reset(flattened: false, unnamed: false);
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var autocannon = db.Cache.GetAll<WeaponItemData>().Single(weapon => weapon.Name == "Autocannon");
        var seller = db.Cache.GetAll<FactionProductData>().OrderBy(p => p.Name, StringComparer.Ordinal).First(p => p.Design.Key.Equals(db.Cache.RefOf(autocannon).Key) && p.Roles.Count == 2);
        var sellerName = seller.Name;
        seller.Roles = new List<ProductRole> { seller.Roles.OrderBy(r => r.Role, StringComparer.Ordinal).First() };
        var bin = new FactionProductData
        {
            Name = "Binned " + sellerName, Design = seller.Design, Manufacturer = seller.Manufacturer,
            Bin = new ProductBin { Line = db.Cache.RefOf(seller), Floor = 0f }
        };
        db.Cache.Commit(batch =>
        {
            batch.Upsert(typeof(FactionProductData), seller, db.Cache.RefOf(seller).Key);
            batch.Upsert(typeof(FactionProductData), bin);
        });
        db.Cache.Dispose();

        Assert.Equal(0, Program.RolesBackfill(apply: true, root: _root));
        db = AetherDb.Open(root: _root);
        try
        {
            Assert.Equal(2, db.Cache.GetAll<FactionProductData>().Single(p => p.Name == sellerName).Roles.Count);
            Assert.Empty(db.Cache.GetAll<FactionProductData>().Single(p => p.Name == "Binned " + sellerName).Roles);
        }
        finally { db.Cache.Dispose(); }
    }

    // The census reports a seller that authors no spread for a role of its design as a gap, and never a bin: a bin reads its line's.
    [Fact]
    public void The_census_reports_a_roleless_seller_and_never_a_bin()
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var autocannon = db.Cache.GetAll<WeaponItemData>().Single(weapon => weapon.Name == "Autocannon");
        var seller = db.Cache.GetAll<FactionProductData>().OrderBy(p => p.Name, StringComparer.Ordinal).First(p => p.Design.Key.Equals(db.Cache.RefOf(autocannon).Key));
        var sellerName = seller.Name;
        seller.Roles = new List<ProductRole>();
        db.Cache.Commit(batch =>
        {
            batch.Upsert(typeof(FactionProductData), seller, db.Cache.RefOf(seller).Key);
            batch.Upsert(typeof(FactionProductData), new FactionProductData
            {
                Name = "Binned " + sellerName, Design = seller.Design, Manufacturer = seller.Manufacturer,
                Bin = new ProductBin { Line = db.Cache.RefOf(seller), Floor = 0f }
            });
        });
        db.Cache.Dispose();

        db = AetherDb.Open(root: _root);
        try
        {
            var gaps = Program.RoleGaps(db.Cache, db.Cache.GetAll<EquippableItemData>().ToArray(), db.Cache.GetAll<FactionProductData>().ToArray()).ToArray();
            Assert.Contains(gaps, gap => gap.Product.Name == sellerName && gap.Missing.Length == 2);
            Assert.DoesNotContain(gaps, gap => gap.Product.Name == "Binned " + sellerName);
        }
        finally { db.Cache.Dispose(); }
    }

    // roles-migrate authors a role on every product that sells a design lacking one; a bin sells none of its own.
    [Fact]
    public void A_binned_seller_gains_no_roles_from_roles_migrate()
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var autocannon = db.Cache.GetAll<WeaponItemData>().Single(weapon => weapon.Name == "Autocannon");
        var seller = db.Cache.GetAll<FactionProductData>().OrderBy(p => p.Name, StringComparer.Ordinal).First(p => p.Design.Key.Equals(db.Cache.RefOf(autocannon).Key));
        var bin = new FactionProductData
        {
            Name = "Binned " + seller.Name, Design = seller.Design, Manufacturer = seller.Manufacturer,
            Bin = new ProductBin { Line = db.Cache.RefOf(seller), Floor = 0f }
        };
        db.Cache.Commit(batch => batch.Upsert(typeof(FactionProductData), bin));
        db.Cache.Dispose();

        Assert.Equal(0, Program.RolesMigrate(apply: true, root: _root));
        db = AetherDb.Open(root: _root);
        try { Assert.Empty(db.Cache.GetAll<FactionProductData>().Single(p => p.Name == "Binned " + seller.Name).Roles); }
        finally { db.Cache.Dispose(); }
    }

    [Fact]
    public void A_design_the_catalog_does_not_hold_refuses_and_lands_nothing()
    {
        Reset(flattened: false, unnamed: false);
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var autocannon = db.Cache.GetAll<WeaponItemData>().Single(weapon => weapon.Name == "Autocannon");
        autocannon.Name = "Autocannon (renamed)";
        db.Cache.Commit(batch => batch.Upsert(typeof(WeaponItemData), autocannon, db.Cache.RefOf(autocannon).Key));
        db.Cache.Dispose();
        var before = File.ReadAllBytes(CatalogPath);

        Assert.Throws<InvalidOperationException>(() => Program.RolesBackfill(apply: true, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }
}
