using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

// Aetheria release, cut roles-backfill, rule CB-R1: a crafted design declares roles iff a stat varies with quality,
// every declared role is read by a non-flat stat, and every seller authors one ProductRole per role. These tests read
// the shipped catalog through the real reader.
public sealed class CatalogRoleTests
{
    internal static IEnumerable<(string Name, PerformanceStat Stat)> StatsOf(EquippableItemData design) =>
        design.Behaviors.Where(b => b != null)
            .SelectMany(b => b.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat))
                .Select(f => ($"{b.GetType().Name}.{f.Name}", (PerformanceStat)f.GetValue(b))))
            .Where(s => s.Item2?.Terms != null);

    private static PerformanceStat Stat(EquippableItemData design, string field) =>
        Assert.Single(design.Behaviors.SelectMany(b => b.GetType().GetFields().Where(f => f.Name == field && f.FieldType == typeof(PerformanceStat)).Select(f => (PerformanceStat)f.GetValue(b))));

    internal static string[] NamedQualityRoles(PerformanceStat stat) =>
        stat.Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => t.Role).ToArray();

    // The whole of CB-R1 over every design in the catalog: each non-flat stat reads exactly one declared role, every
    // declared role is read, and every seller authors each role once. Weapons, and designs of a kind the tool keeps a role
    // map for, must also carry a non-flat stat (a design with no stat at all has nothing for quality to vary).
    [Fact]
    public void ShippedCatalogRolesAreLive()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        var broken = new List<string>();
        foreach (var design in cache.GetAll<EquippableItemData>().OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            var stats = StatsOf(design).ToArray();
            var varying = stats.Where(s => s.Stat.Min != s.Stat.Max).ToArray();
            var declared = (design.Roles ?? new List<ItemRole>()).Select(r => r.Name).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            foreach (var (name, stat) in varying)
            {
                var named = NamedQualityRoles(stat);
                if (named.Length != 1 || !declared.Contains(named[0]))
                    broken.Add($"{design.Name}: non-flat {name} reads [{string.Join(", ", named)}], not one of its declared roles [{string.Join(", ", declared)}]");
            }
            var read = varying.SelectMany(s => NamedQualityRoles(s.Stat)).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToArray();
            if (!read.SequenceEqual(declared))
                broken.Add($"{design.Name}: declares [{string.Join(", ", declared)}] but non-flat stats read [{string.Join(", ", read)}]");
            if (varying.Length == 0 && stats.Length > 0 && (design is WeaponItemData || Program.HasRoleMap(design)))
                broken.Add($"{design.Name}: every stat is flat");
            foreach (var product in products.Where(p => p.Design.Key.Equals(cache.RefOf(design).Key)))
            {
                var authored = (product.Roles ?? new List<ProductRole>()).Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray();
                if (!authored.SequenceEqual(declared))
                    broken.Add($"{design.Name}: product \"{product.Name}\" authors [{string.Join(", ", authored)}], not each of [{string.Join(", ", declared)}] once");
            }
        }
        Assert.True(broken.Count == 0, string.Join("
", broken));
    }

    // A product with no maker would title as its design and take no maker profile for its roles.
    [Fact]
    public void EveryShippedProductHasAMaker()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var makerless = cache.GetAll<FactionProductData>().Where(p => cache.Get(p.Manufacturer) == null).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(makerless.Length == 0, "products with no maker: " + string.Join(", ", makerless));
    }

    // A benefit rises with quality across 0.8x to 1.2x of its authored value (Max/Min 1.5), a cost falls across 1.2x to 0.8x
    // (Max/Min 2/3). The ratio and the role are pinned, not the value, so an operator retune of a design stays green.
    private static void AssertRange(EquippableItemData design, string field, bool cost, string role)
    {
        var stat = Stat(design, field);
        Assert.Equal(cost ? 2f / 3f : 1.5f, stat.Max / stat.Min, 3);
        Assert.Equal(new[] { role }, NamedQualityRoles(stat));
    }

    // Stats that already varied before the backfill keep their range; only their unnamed Quality term gains the role.
    private static void AssertNamed(EquippableItemData design, string field, string role) =>
        Assert.Equal(new[] { role }, NamedQualityRoles(Stat(design, field)));

    [Fact]
    public void RolesBackfillIsAuthored()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        EquippableItemData Design(string name) => Assert.Single(cache.GetAll<EquippableItemData>(), i => i.Name == name);
        string[] Roles(EquippableItemData d) => d.Roles.Select(r => r.Name).OrderBy(r => r, StringComparer.Ordinal).ToArray();

        var autocannon = Design("Autocannon");
        Assert.Equal(new[] { "barrel", "feed mechanism" }, Roles(autocannon));
        AssertRange(autocannon, "Damage", false, "barrel");
        AssertRange(autocannon, "Range", false, "barrel");
        AssertRange(autocannon, "Penetration", false, "barrel");

        var lrm = Design("LRMM72");
        Assert.Equal(new[] { "guidance system", "thruster", "warhead" }, Roles(lrm));
        AssertRange(lrm, "Damage", false, "warhead");
        AssertRange(lrm, "Cooldown", true, "guidance system");
        AssertRange(lrm, "LockAngle", false, "guidance system");

        var srm = Design("SRMM72");
        Assert.Equal(new[] { "guidance system", "thruster", "warhead" }, Roles(srm));
        AssertRange(srm, "Damage", false, "warhead");
        AssertRange(srm, "Cooldown", true, "guidance system");

        var mine = Design("Mine Launcher");
        Assert.Equal(new[] { "dispenser", "warhead" }, Roles(mine));
        AssertRange(mine, "Damage", false, "warhead");
        AssertRange(mine, "Range", false, "dispenser");
        AssertRange(mine, "Cooldown", true, "dispenser");
        AssertRange(mine, "Spread", true, "dispenser");
        var layer = Assert.Single(mine.Behaviors.OfType<MineLayerData>());
        Assert.Equal(2f, layer.ArmingDelay);
        Assert.Equal(2f, layer.FuseDelay);
        Assert.Equal(30f, layer.Lifetime);

        var drive = Design("Large Drive");
        Assert.Equal(new[] { "injector", "nozzle" }, Roles(drive));
        AssertRange(drive, "Thrust", false, "nozzle");
        AssertRange(drive, "Visibility", true, "nozzle");
        AssertRange(drive, "Heat", true, "injector");

        AssertRange(Design("Small Drive"), "Thrust", false, "nozzle");

        // Designs whose non-flat stats named no role are pointed at the role their kind's map gives them.
        AssertNamed(Design("DeathCluster"), "Count", "feed mechanism");
        AssertNamed(Design("Flak Gun"), "Count", "feed mechanism");
        AssertNamed(Design("GT 3K"), "MinRange", "guidance system");
        AssertNamed(Design("plight"), "DamageSpread", "focusing array");
        AssertNamed(Design("MoveOnPro"), "Modifier", "core");
        AssertNamed(Design("MoveOnPro Station Reactor"), "Modifier", "core");

        // Each seller's means come from its maker's profile; the Pirates are middling and inconsistent.
        var profiles = new Dictionary<string, (float Mean, float Dev)>
        {
            ["Zhestokost"] = (.50f, .08f), ["R&D"] = (.60f, .15f), ["DME"] = (.55f, .20f), ["Lightsail"] = (.55f, .10f), ["Pirates"] = (.45f, .22f),
        };
        foreach (var design in new[] { autocannon, lrm, srm, mine, drive })
        {
            var sellers = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(cache.RefOf(design).Key)).ToArray();
            Assert.NotEmpty(sellers);
            foreach (var product in sellers)
            {
                var maker = cache.Get(product.Manufacturer);
                var (mean, dev) = profiles[maker.ShortName];
                Assert.Equal(Roles(design), product.Roles.Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray());
                Assert.All(product.Roles, r =>
                {
                    Assert.Equal(mean, r.Mean, 3);
                    Assert.Equal(dev, r.StandardDeviation, 3);
                });
            }
        }
        Assert.Contains(cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(cache.RefOf(autocannon).Key) && cache.Get(p.Manufacturer).ShortName == "Pirates");
    }
}
