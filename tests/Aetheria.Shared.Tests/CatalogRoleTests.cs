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

    // CB-R1 for one stat. A non-flat stat carries exactly one Quality term, it names a declared role, and its exponent is
    // positive (a role that cannot move the stat is not read). A flat stat carries no Quality term that names a role.
    // Returns the fault, or null.
    internal static string StatFault(PerformanceStat stat, string[] declared)
    {
        var quality = stat.Terms.Where(t => t.Source == StatSource.Quality).ToArray();
        if (stat.Min == stat.Max)
            return quality.Any(t => !string.IsNullOrEmpty(t.Role)) ? $"is flat but a Quality term names [{string.Join(", ", NamedQualityRoles(stat))}]" : null;
        if (quality.Length != 1)
            return $"is non-flat with {quality.Length} Quality terms, not one";
        if (string.IsNullOrEmpty(quality[0].Role) || !declared.Contains(quality[0].Role))
            return $"is non-flat and reads [{quality[0].Role}], not one of its declared roles [{string.Join(", ", declared)}]";
        return quality[0].Exponent > 0f ? null : $"is non-flat but its role \"{quality[0].Role}\" has exponent {quality[0].Exponent}";
    }

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
            foreach (var (name, stat) in stats)
            {
                var fault = StatFault(stat, declared);
                if (fault != null) broken.Add($"{design.Name}: {name} {fault}");
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
        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    private static PerformanceStat Ranged(params StatTerm[] terms) => new PerformanceStat { Min = 1f, Max = 2f, Terms = terms.ToList() };
    private static StatTerm Quality(string role, float exponent = 1f) => new StatTerm { Source = StatSource.Quality, Role = role, Exponent = exponent };

    // The stat check refuses what the shipped catalog does not hold but a later content cut could author.
    [Fact]
    public void TheStatCheckRefusesGenericQualityDeadRolesAndNamedFlatStats()
    {
        var declared = new[] { "barrel" };
        Assert.Null(StatFault(Ranged(Quality("barrel")), declared));
        Assert.Null(StatFault(new PerformanceStat { Min = 3f, Max = 3f, Terms = new List<StatTerm>() }, declared));
        Assert.NotNull(StatFault(Ranged(Quality("barrel"), Quality(null)), declared));
        Assert.NotNull(StatFault(Ranged(Quality("barrel", 0f)), declared));
        Assert.NotNull(StatFault(Ranged(Quality(null)), declared));
        Assert.NotNull(StatFault(Ranged(Quality("bogus")), declared));
        Assert.NotNull(StatFault(new PerformanceStat { Min = 3f, Max = 3f, Terms = new List<StatTerm> { Quality("bogus") } }, declared));
    }

    // A product with no maker would title as its design and take no maker profile for its roles.
    [Fact]
    public void EveryShippedProductHasAMaker()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var makerless = cache.GetAll<FactionProductData>().Where(p => cache.Get(p.Manufacturer) == null).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(makerless.Length == 0, "products with no maker: " + string.Join(", ", makerless));
    }

    // A design is named like a node on a tech tree and the brand lives in the product it sells (rulings
    // catalog-grows-generic-designs-branded-products, designs-are-tech-nodes): no design's product bears the design's name.
    [Fact]
    public void EveryProductCarriesABrand()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        var unbranded = cache.GetAll<EquippableItemData>().Where(design => design is not HullData)
            .Where(design => products.Any(p => p.Design.Key.Equals(cache.RefOf(design).Key) && p.Name == design.Name))
            .Select(design => design.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(unbranded.Length == 0, "designs sold under their own name: " + string.Join(", ", unbranded));
    }

    // The Capacitor is the one Tool design with a role map by name; a key that falls out of step with the design's name leaves
    // roles-backfill finding no map for it.
    [Fact]
    public void TheCapacitorDesignCarriesItsRoleMap()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var capacitor = cache.GetAll<EquippableItemData>().Single(design => design.Name == "Capacitor");
        Assert.True(Program.HasRoleMap(capacitor), "roles-backfill finds no role map for the Capacitor design");
    }

    // A design is a tech node: its text may not name a product the catalog sells (the brand lives on the product).
    [Fact]
    public void NoDesignTextNamesAProduct()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var productNames = cache.GetAll<FactionProductData>().Select(p => p.Name).Where(n => n.Length > 3).ToHashSet();
        var branded = cache.GetAll<EquippableItemData>().Where(design => design is not HullData && design.Description != null)
            .Where(design => productNames.Any(n => design.Description.Contains(n)))
            .Select(design => design.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(branded.Length == 0, "designs whose text names a product: " + string.Join(", ", branded));
    }

    // Authoring notes are not player text, and every product of a design this cut renamed or branded speaks in its own
    // voice (Miss Terri's and the Pirates' records are other passes').
    [Fact]
    public void NoProductTextIsAnAuthoringNote()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        string[] notes = { Program.AuthoringNote.Trim(), "[TODO]", "subsidiary plight" };
        var noted = products.Where(p => p.Description != null && notes.Any(n => p.Description.Contains(n))).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(noted.Length == 0, "products whose text is an authoring note: " + string.Join(", ", noted));

        var touched = Program.Renames.Select(r => r.Generic).Concat(Program.Brands.Select(b => b.Design)).ToHashSet();
        var designs = cache.GetAll<EquippableItemData>().Where(d => touched.Contains(d.Name)).ToArray();
        Assert.Equal(touched.Count, designs.Length);
        var silent = products
            .Where(p => designs.Any(d => p.Design.Key.Equals(cache.RefOf(d).Key)))
            .Where(p => cache.Get(p.Manufacturer)?.ShortName is not ("Miss Terri's" or "Pirates"))
            .Where(p => string.IsNullOrWhiteSpace(p.Description))
            .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(silent.Length == 0, "products with no text of their own: " + string.Join(", ", silent));
    }

    // A rename changes Name and Description and nothing else: the design and its products keep their keys and makers, and the
    // flavour that moved lives on the product.
    [Fact]
    public void RenamedDesignsKeepTheirKeysAndProducts()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var expected = new (string DesignKey, string Generic, (string Key, string Name, string Maker)[] Products)[]
        {
            ("0271bb97-4ce8-4d64-97c3-8bfd3f76b2c6", "Light Machine Gun", new[] { ("7a1f63b0-68de-4976-a338-bc779db3386a", "ClearPath", "Lightsail") }),
            ("3baa2c43-b389-4438-94cb-fbb04489cd77", "Heavy Reactor", new[] { ("1edd87f7-2592-4744-a9ba-618f403f91ce", "Manhattan", "Zhestokost") }),
            ("49b4c15a-c10b-49c0-9dad-31dfb9b3a9b9", "Cargo Bay", new[] { ("4ecc97af-c1fa-4563-902e-72a094ad8322", "Store-All Plus", "Lightsail"), ("ed3c70eb23814d5181d99fa84f6194d4", null, "Pirates") }),
            ("88e81a2b-45fd-49de-830f-c896adae8d5b", "Racing Drive", new[] { ("9ccc095374d64fde94dd8d04bdc97c2a", "Victoire", "Alakrita") }),
            ("632420a6-4419-4647-a92b-5e22ea289025", "Lightning Projector", new[] { ("f0f8389f9dd64228b02a68392fcf4007", "DragOnBreath", "Lucent") }),
        };
        foreach (var (designKey, generic, sold) in expected)
        {
            var design = Assert.Single(cache.GetAll<EquippableItemData>(), d => cache.RefOf(d).Key.Value == designKey);
            Assert.Equal(generic, design.Name);
            Assert.Equal(Program.Renames.Single(r => r.Generic == generic).Functional, design.Description);
            var products = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(cache.RefOf(design).Key)).ToArray();
            Assert.Equal(sold.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal), products.Select(p => cache.RefOf(p).Key.Value).OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (key, name, maker) in sold)
            {
                var product = products.Single(p => cache.RefOf(p).Key.Value == key);
                if (name != null) Assert.Equal(name, product.Name);
                Assert.Equal(maker, cache.Get(product.Manufacturer).ShortName);
            }
        }

        // The text that moved: Victoire carries the racing drive's old line, DragOnBreath plight's old one.
        string TextOf(string productKey) => cache.GetAll<FactionProductData>().Single(p => cache.RefOf(p).Key.Value == productKey).Description;
        Assert.Equal("The most responsive thruster on the market, for the most irresponsible of racing pilots.", TextOf("9ccc095374d64fde94dd8d04bdc97c2a"));
        Assert.StartsWith("Chain lightning has your enemies chain smoking", TextOf("f0f8389f9dd64228b02a68392fcf4007"));

        // The mine launcher: the design takes the functional line its product used to carry; the product is the brand.
        var mine = Assert.Single(cache.GetAll<EquippableItemData>(), d => cache.RefOf(d).Key.Value == "1b4a2bb358eb432eb1cfc0ccf3317303");
        Assert.Equal("Mine Launcher", mine.Name);
        Assert.Equal("Lays a drifting proximity mine that arms after two seconds.", mine.Description);
        var brand = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(cache.RefOf(mine).Key));
        Assert.Equal("parting gift", brand.Name);
        Assert.Equal("DME", cache.Get(brand.Manufacturer).ShortName);
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

        var lrm = Design("Long-Range Missile Rack");
        Assert.Equal(new[] { "guidance system", "thruster", "warhead" }, Roles(lrm));
        AssertRange(lrm, "Damage", false, "warhead");
        AssertRange(lrm, "Cooldown", true, "guidance system");
        AssertRange(lrm, "LockAngle", false, "guidance system");

        var srm = Design("Short-Range Missile Rack");
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
        AssertNamed(Design("Cluster Cannon"), "Count", "feed mechanism");
        AssertNamed(Design("Flak Gun"), "Count", "feed mechanism");
        AssertNamed(Design("Guided Missile Launcher"), "MinRange", "guidance system");
        AssertNamed(Design("Lightning Projector"), "DamageSpread", "focusing array");
        AssertNamed(Design("Muon Fusion Reactor"), "Modifier", "core");
        AssertNamed(Design("Muon Fusion Station Reactor"), "Modifier", "core");

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

    // aetheria-release cut sensor-catalog (ruling sensor-stat-set): every sensor design's intercept factor varies with its
    // maker's emission-discipline quality, brand flavour is a product's quality (Lucent loud, Finch quiet), and the
    // Passive Array is a Tool listener with two products from makers other than Miss Terri's and the Pirates.
    [Fact]
    public void TheCatalogCarriesSensorStats()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var products = cache.GetAll<FactionProductData>().ToArray();
        string MakerOf(FactionProductData p) => cache.Get(p.Manufacturer).ShortName;
        var sensors = cache.GetAll<EquippableItemData>().Where(d => d.Behaviors.OfType<SensorData>().Any()).ToArray();
        Assert.NotEmpty(sensors);
        foreach (var design in sensors)
        {
            var alpha = design.Behaviors.OfType<SensorData>().Single().InterceptFactor;
            Assert.NotNull(alpha);
            Assert.NotEqual(alpha.Min, alpha.Max);
            Assert.Equal(new[] { "emission-discipline" }, NamedQualityRoles(alpha));
        }

        double[] Discipline(string maker) => products
            .Where(p => sensors.Any(s => cache.RefOf(s).Key.Equals(p.Design.Key)) && MakerOf(p) == maker)
            .Select(p => (double)p.Roles.Single(r => r.Role == "emission-discipline").Mean).ToArray();
        var lucent = Discipline("Lucent");
        var finch = Discipline("Finch");
        Assert.NotEmpty(lucent);
        Assert.NotEmpty(finch);
        Assert.True(lucent.Max() < finch.Min(), "a Lucent sensor must be louder than every Finch one");

        var array = Assert.Single(cache.GetAll<EquippableItemData>(), d => d.Name == "Passive Array");
        Assert.Equal(HardpointType.Tool, array.HardpointType);
        Assert.True(array.Behaviors.OfType<ListenerData>().Single().Multiple.Min >= 2f);
        var makers = products.Where(p => p.Design.Key.Equals(cache.RefOf(array).Key)).Select(MakerOf).ToArray();
        Assert.Equal(2, makers.Distinct().Count());
        Assert.DoesNotContain(makers, m => m == "Pirates" || m == "Miss Terri's");
    }

    // The intercept factor alpha is quality-derived (ruling sensor-stat-set): evaluated by the stat's own Evaluate over a
    // lot that fills only emission-discipline, so a design that read the lot's workmanship instead would read the decoy.
    private sealed class DisciplineContext : IStatContext
    {
        public DisciplineContext(float discipline) =>
            Lot = new Lot { Quality = .123f, Roles = new List<RoleFill> { new RoleFill { Role = "emission-discipline", Quality = discipline } } };
        public Lot Lot { get; }
        public float HeatFactor(float exponent) => 1f;
        public float DurabilityFactor(float exponent) => 1f;
        public float ConsumableProgressFactor(float exponent) => 1f;
        public float PowerSupplyFactor(float exponent) => 1f;
        public float ScaleModifier(PerformanceStat stat) => 1f;
        public float ConstantModifier(PerformanceStat stat) => 0f;
    }

    // A sensor of middling discipline reads exactly 1 (it is heard where it sees), the worst discipline reads the loud end
    // .5 and the best the quiet end 2, and the exponent is the one that puts the midpoint on 1: lerp(.5, 2, .5^e) = 1.
    // Each product then reads alpha at its own authored discipline: Lucent below 1 (loud), Finch and Sol above (quiet),
    // every other maker at 1. The expectations come from that formula, not from numbers copied out of the catalog.
    [Fact]
    public void ASensorsInterceptFactorIsDerivedFromItsEmissionDiscipline()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var sensors = cache.GetAll<EquippableItemData>().Where(d => d.Behaviors.OfType<SensorData>().Any()).ToArray();
        Assert.NotEmpty(sensors);
        float Alpha(EquippableItemData design, float discipline) =>
            design.Behaviors.OfType<SensorData>().Single().InterceptFactor.Evaluate(new DisciplineContext(discipline));
        foreach (var design in sensors)
        {
            Assert.Equal(1f, Alpha(design, .5f), 4);
            Assert.Equal(.5f, Alpha(design, 0f), 4);
            Assert.Equal(2f, Alpha(design, 1f), 4);
            var term = Assert.Single(design.Behaviors.OfType<SensorData>().Single().InterceptFactor.Terms);
            Assert.Equal(MathF.Log(3f) / MathF.Log(2f), term.Exponent, 4); // (2 - .5) / (1 - .5) = 3 = 2^e
        }

        var products = cache.GetAll<FactionProductData>()
            .Where(p => sensors.Any(s => cache.RefOf(s).Key.Equals(p.Design.Key))).ToArray();
        Assert.NotEmpty(products);
        foreach (var product in products)
        {
            var design = sensors.Single(s => cache.RefOf(s).Key.Equals(product.Design.Key));
            var maker = cache.Get(product.Manufacturer).ShortName;
            var alpha = Alpha(design, product.Roles.Single(r => r.Role == "emission-discipline").Mean);
            if (maker == "Lucent") Assert.True(alpha < .8f, $"{product.Name}: a Lucent sensor must be loud, reads {alpha}");
            else if (maker == "Finch" || maker == "Sol") Assert.True(alpha > 1.25f, $"{product.Name}: a {maker} sensor must be quiet, reads {alpha}");
            else Assert.Equal(1f, alpha, 3);
        }
        Assert.Contains(products, p => cache.Get(p.Manufacturer).ShortName == "Lucent");
        Assert.Contains(products, p => cache.Get(p.Manufacturer).ShortName == "Finch");
    }
}
