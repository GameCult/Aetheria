using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

// Aetheria release, cut roles-backfill, rule CB-R1: a crafted design declares roles iff a stat varies with quality,
// every declared role is read by a non-flat stat, and every seller authors one ProductRole per role. Both tests read
// the shipped catalog through the real reader.
public sealed class CatalogRoleTests
{
    // Every weapon and every gear of a kind that carries per-part quality stats (the kinds roles-migrate keeps a role
    // map for). Cargo bays, docking bays, control modules, tools and hulls carry no quality-reading stat.
    private static readonly HardpointType[] QualityGearKinds =
        { HardpointType.Radiator, HardpointType.Reactor, HardpointType.Sensors, HardpointType.Thruster };

    private static IEnumerable<PerformanceStat> StatsOf(EquippableItemData design) =>
        design.Behaviors.Where(b => b != null)
            .SelectMany(b => b.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat)).Select(f => (PerformanceStat)f.GetValue(b)))
            .Where(s => s?.Terms != null);

    private static PerformanceStat Stat(EquippableItemData design, string field) =>
        Assert.Single(design.Behaviors.SelectMany(b => b.GetType().GetFields().Where(f => f.Name == field && f.FieldType == typeof(PerformanceStat)).Select(f => (PerformanceStat)f.GetValue(b))));

    private static string[] NamedQualityRoles(PerformanceStat stat) =>
        stat.Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => t.Role).ToArray();

    [Fact]
    public void ShippedCatalogRolesAreLive()
    {
        var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        var broken = new List<string>();
        foreach (var design in cache.GetAll<EquippableItemData>()
                     .Where(i => i is WeaponItemData || (i is GearData && QualityGearKinds.Contains(i.HardpointType)))
                     .OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            var varying = StatsOf(design).Where(s => s.Min != s.Max).ToArray();
            var read = varying.SelectMany(NamedQualityRoles).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToArray();
            var declared = (design.Roles ?? new List<ItemRole>()).Select(r => r.Name).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            if (varying.Length == 0) broken.Add($"{design.Name}: every stat is flat");
            if (!read.SequenceEqual(declared))
                broken.Add($"{design.Name}: declares [{string.Join(", ", declared)}] but non-flat stats read [{string.Join(", ", read)}]");
            foreach (var product in products.Where(p => p.Design.Key.Equals(cache.RefOf(design).Key)))
            {
                var authored = (product.Roles ?? new List<ProductRole>()).Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray();
                if (!authored.SequenceEqual(declared))
                    broken.Add($"{design.Name}: product \"{product.Name}\" authors [{string.Join(", ", authored)}], not each of [{string.Join(", ", declared)}] once");
            }
        }
        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    // value, then the stat's expected Min and Max: a benefit rises with quality (0.8x to 1.2x), a cost falls (1.2x to 0.8x).
    private static void AssertRange(EquippableItemData design, string field, float value, bool cost, string role)
    {
        var stat = Stat(design, field);
        Assert.Equal(value * (cost ? 1.2f : .8f), stat.Min, 3);
        Assert.Equal(value * (cost ? .8f : 1.2f), stat.Max, 3);
        Assert.Contains(role, NamedQualityRoles(stat));
    }

    [Fact]
    public void RolesBackfillIsAuthored()
    {
        var cache = RestoredHullsTests.OpenCatalog();
        EquippableItemData Design(string name) => Assert.Single(cache.GetAll<EquippableItemData>(), i => i.Name == name);
        string[] Roles(EquippableItemData d) => d.Roles.Select(r => r.Name).OrderBy(r => r, StringComparer.Ordinal).ToArray();

        var autocannon = Design("Autocannon");
        Assert.Equal(new[] { "barrel", "feed mechanism" }, Roles(autocannon));
        AssertRange(autocannon, "Damage", 3f, false, "barrel");
        AssertRange(autocannon, "Range", 1000f, false, "barrel");

        var lrm = Design("LRMM72");
        Assert.Equal(new[] { "guidance system", "thruster", "warhead" }, Roles(lrm));
        AssertRange(lrm, "Damage", 256f, false, "warhead");
        AssertRange(lrm, "Cooldown", 10f, true, "guidance system");

        var srm = Design("SRMM72");
        Assert.Equal(new[] { "guidance system", "thruster", "warhead" }, Roles(srm));
        AssertRange(srm, "Damage", 128f, false, "warhead");
        AssertRange(srm, "Cooldown", 5f, true, "guidance system");

        var mine = Design("Mine Launcher");
        Assert.Equal(new[] { "dispenser", "warhead" }, Roles(mine));
        AssertRange(mine, "Damage", 50f, false, "warhead");
        AssertRange(mine, "Cooldown", 2f, true, "dispenser");
        AssertRange(mine, "Spread", 5f, true, "dispenser");
        var layer = Assert.Single(mine.Behaviors.OfType<MineLayerData>());
        Assert.Equal(2f, layer.ArmingDelay);
        Assert.Equal(2f, layer.FuseDelay);
        Assert.Equal(30f, layer.Lifetime);

        var drive = Design("Large Drive");
        Assert.Equal(new[] { "injector", "nozzle" }, Roles(drive));
        AssertRange(drive, "Thrust", 250000f, false, "nozzle");
        AssertRange(drive, "Visibility", 100f, true, "nozzle");
        AssertRange(drive, "Heat", 2500f, true, "injector");

        var smallDrive = Design("Small Drive");
        AssertRange(smallDrive, "Thrust", 75000f, false, "nozzle");

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
