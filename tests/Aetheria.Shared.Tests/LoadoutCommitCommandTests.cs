using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's loadout-commit command (aetheria-release, cut loadout-commit; rulings boss-loadouts-agent-authored,
// catalog-is-a-package-stack), run on a scratch copy of the shipped catalog with a scratch GameData/Mods. Commit writes one
// Loadout at Loadouts.KeyOf(name) with the records its names resolve to in the composed catalog the game boots, and with
// --boss-of makes it that faction's boss in the same commit; every refusal leaves the catalog byte-identical.
public sealed class LoadoutCommitCommandTests : IDisposable
{
    private const string PackageId = "mod.loadoutcommit";
    private const string PackageProduct = "Loadout Commit Skiff Mk I";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "loadout-commit-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");
    private string Mods => Path.Combine(_root, "GameData", "Mods");

    public LoadoutCommitCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
    }

    public void Dispose() => Directory.Delete(_root, true);

    private CultCache Open()
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        return cache;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // A hull product and a weapon slot of the shipped catalog that fit together, found the production way: a generated fit,
    // captured. Every name in them is unique in the catalog, so the command resolves it exactly.
    private (string hullProduct, string slot, string openSlot, string designName, string productName) Fit()
    {
        using var cache = Open();
        var settings = AuthoredSettings.Load(_root).Read<GameplaySettings>("GameplaySettings");
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), settings, _ => { });
        bool UniqueProduct(string name) => cache.GetAll<FactionProductData>().Count(item => item.Name == name) == 1;
        bool UniqueDesign(string name) => cache.GetAll<EquippableItemData>().Count(item => item.Name == name) == 1;
        foreach (var hull in cache.GetAll<HullData>().Where(h => h.HullType == HullType.Ship && h.Price > 0).OrderBy(h => h.Name, StringComparer.Ordinal))
        {
            var random = new CultMath.Random(1);
            var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
            var pack = generator.GenerateShipLoadout(candidate => candidate == hull);
            if (pack == null) continue;
            var fit = Loadouts.Capture(items, (Ship) EntitySerializer.Unpack(items, null, pack), "probe");
            var hullProduct = cache.Get(fit.HullProduct);
            if (hullProduct == null || !UniqueProduct(hullProduct.Name)) continue;
            foreach (var slot in fit.Slots.Where(s => s.Product.IsSet() && cache.Get(s.Design).Behaviors.Any(b => b is WeaponData)))
            {
                var design = cache.Get(slot.Design);
                var product = cache.Get(slot.Product);
                if (!UniqueDesign(design.Name) || !UniqueProduct(product.Name)) continue;
                var cell = $"{slot.Position.x},{slot.Position.y},{slot.Rotation},{design.Name}";
                return (hullProduct.Name, $"{cell}@{product.Name}", cell, design.Name, product.Name);
            }
        }
        throw new InvalidOperationException("the shipped catalog has no ship hull with a uniquely named weapon fit");
    }

    // A package under the scratch Mods root whose ship.cc alone holds its hull and its one product, made by the first shipped
    // faction by name: the shipped catalog holds neither.
    private void WritePackage()
    {
        CultRecordKey maker;
        using (var cache = Open())
            maker = cache.RefOf(cache.GetAll<Faction>().OrderBy(f => f.Name, StringComparer.Ordinal).First()).Key;
        var directory = ShipFixture.WritePackage(Mods, PackageId, "Loadout Commit Skiff", tweak: ship => ship.Hull.Price = 10);
        using var store = ShipAuthoringStore.Open(Path.Combine(directory, "ship.cc"), writable: true);
        ShipAuthoringStore.WriteProduct(store, PackageId, new FactionProductData
        {
            Name = PackageProduct,
            Design = new CultRecordRef<CraftedItemData>(ShipModCatalog.HullKey(PackageId)),
            Manufacturer = new CultRecordRef<Faction>(maker)
        });
        store.FlushAsync().Wait();
    }

    private int Run(out string output, params string[] args)
    {
        var writer = new StringWriter();
        var code = LoadoutCommitCommand.Run(args, _root, writer);
        output = writer.ToString();
        return code;
    }

    [Fact]
    public void No_arguments_print_the_usage_and_fail()
    {
        Assert.Equal(1, Run(out var output));
        Assert.StartsWith("usage: loadout-commit ", output);
        Assert.Contains("--boss-of <faction>", output);
    }

    [Fact]
    public void A_commit_writes_one_loadout_with_the_records_its_names_resolve_to()
    {
        var fit = Fit();
        Assert.Equal(0, Run(out var output, "Test boss", fit.hullProduct, fit.slot, "--group", "0"));
        Assert.StartsWith("Test boss: committed, hull ", output);

        using var cache = Open();
        var loadout = Assert.Single(cache.GetAll<Loadout>());
        Assert.Equal(Loadouts.KeyOf("Test boss"), cache.RefOf(loadout).Key);
        var product = cache.GetAll<FactionProductData>().Single(p => p.Name == fit.hullProduct);
        Assert.Equal(cache.RefOf(product).Key, loadout.HullProduct.Key);
        Assert.Equal(product.Design.Key, loadout.Hull.Key);
        var slot = Assert.Single(loadout.Slots);
        Assert.Equal(fit.designName, cache.Get(slot.Design).Name);
        Assert.Equal(fit.productName, cache.Get(slot.Product).Name);
        Assert.Equal(new[] { new[] { 0 } }, loadout.WeaponGroups);
        Assert.DoesNotContain(cache.GetAll<Faction>(), faction => faction.BossLoadout.IsSet() && faction.BossLoadout.Key.Equals(Loadouts.KeyOf("Test boss")));
    }

    // @product is optional: a design-only slot commits with no product. Replace overwrites only when asked.
    [Fact]
    public void A_design_only_slot_commits_with_no_product_and_replace_overwrites()
    {
        var fit = Fit();
        Assert.Equal(0, Run(out _, "Test boss", fit.hullProduct, fit.slot));
        var written = Hash(CatalogPath);
        Assert.Equal(1, Run(out var refused, "Test boss", fit.hullProduct, fit.openSlot));
        Assert.Contains("already exists", refused);
        Assert.Equal(written, Hash(CatalogPath));

        Assert.Equal(0, Run(out _, "Test boss", fit.hullProduct, fit.openSlot, "--replace"));
        using var cache = Open();
        var loadout = Assert.Single(cache.GetAll<Loadout>());
        Assert.False(Assert.Single(loadout.Slots).Product.IsSet());
        Assert.True(loadout.WeaponGroups == null || loadout.WeaponGroups.Length == 0);
    }

    // Every refusal happens before Loadouts.Commit, so the catalog is byte-identical; none echoes the name it was given.
    [Theory]
    [InlineData("unknown-hull-product")]
    [InlineData("ambiguous-hull-product")]
    [InlineData("unknown-design")]
    [InlineData("ambiguous-design")]
    [InlineData("unknown-slot-product")]
    [InlineData("rotation-not-a-name")]
    [InlineData("rotation-a-number")]
    [InlineData("slot-does-not-fit")]
    [InlineData("group-names-no-slot")]
    [InlineData("unknown-boss-faction")]
    public void A_name_that_does_not_resolve_or_a_loadout_that_cannot_be_built_is_refused_and_nothing_is_written(string refusal)
    {
        var fit = Fit();
        var canary = "Canary9c1e";
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        object Copy(object document) => MessagePack.MessagePackSerializer.Deserialize(document.GetType(), MessagePack.MessagePackSerializer.Serialize(document.GetType(), document, options), options);
        using (var cache = Open())
        {
            // Under a key of its own, so the copy is a second record of the same name, not the first overwritten.
            void Add(object document) => Assert.True(cache.Commit(batch => batch.Upsert(document.GetType(), document, new CultRecordKey("duplicate:" + Guid.NewGuid().ToString("N")))));
            switch (refusal)
            {
                case "ambiguous-hull-product":
                    Add(Copy(cache.GetAll<FactionProductData>().Single(p => p.Name == fit.hullProduct)));
                    break;
                case "ambiguous-design":
                    Add(Copy(cache.GetAll<EquippableItemData>().Single(d => d.Name == fit.designName)));
                    break;
            }
            cache.FlushAsync().Wait();
        }

        var cell = fit.openSlot.Split(',');
        var args = refusal switch
        {
            "unknown-hull-product" => new[] { "Test boss", canary, fit.slot },
            "unknown-design" => new[] { "Test boss", fit.hullProduct, $"{cell[0]},{cell[1]},{cell[2]},{canary}" },
            "unknown-slot-product" => new[] { "Test boss", fit.hullProduct, $"{fit.openSlot}@{canary}" },
            "rotation-not-a-name" => new[] { "Test boss", fit.hullProduct, $"{cell[0]},{cell[1]},{canary},{fit.designName}" },
            "rotation-a-number" => new[] { "Test boss", fit.hullProduct, $"{cell[0]},{cell[1]},1,{fit.designName}" },
            "slot-does-not-fit" => new[] { "Test boss", fit.hullProduct, $"999,999,{cell[2]},{fit.designName}" },
            "group-names-no-slot" => new[] { "Test boss", fit.hullProduct, fit.slot, "--group", "5" },
            "unknown-boss-faction" => new[] { "Test boss", fit.hullProduct, fit.slot, "--boss-of", canary },
            _ => new[] { "Test boss", fit.hullProduct, fit.slot }
        };
        var before = Hash(CatalogPath);

        Assert.Equal(1, Run(out var output, args));

        Assert.StartsWith("refused: ", output);
        Assert.DoesNotContain(canary, output);
        Assert.Equal(before, Hash(CatalogPath));
        using var after = Open();
        Assert.Empty(after.GetAll<Loadout>());
    }

    // Ruling catalog-is-a-package-stack: a hull product only a package under Mods holds resolves, because names resolve
    // against the composed catalog the game boots, and the loadout is committed into the shipped file naming the package's keys.
    // The shipped file gains the loadout and nothing of the package.
    [Fact]
    public void A_hull_product_held_only_by_a_package_resolves_and_commits()
    {
        WritePackage();
        using (var cache = Open())
            Assert.DoesNotContain(cache.GetAll<FactionProductData>(), p => p.Name == PackageProduct);

        Assert.Equal(0, Run(out var output, "Package boss", PackageProduct));
        Assert.StartsWith("Package boss: committed, hull Loadout Commit Skiff (Loadout Commit Skiff Mk I), 0 slots", output);

        using var after = Open();
        var loadout = Assert.Single(after.GetAll<Loadout>());
        Assert.Equal(ShipModCatalog.HullKey(PackageId), loadout.Hull.Key);
        Assert.StartsWith("mod-product:" + PackageId + ":", loadout.HullProduct.Key.Value);
        Assert.Null(after.Get(loadout.Hull));
        Assert.Null(after.Get(loadout.HullProduct));
    }

    // --boss-of, by Name or by ShortName, makes the loadout that faction's boss in the same commit; no other faction changes.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Boss_of_sets_the_factions_boss_loadout_in_the_same_commit(bool byShortName)
    {
        var fit = Fit();
        string factionName, argument;
        CultRecordKey[] othersBefore;
        using (var cache = Open())
        {
            var faction = cache.GetAll<Faction>().Where(f => !f.BossLoadout.IsSet())
                .Where(f => cache.GetAll<Faction>().Count(o => o.Name == f.Name || o.ShortName == f.Name) == 1)
                .Where(f => cache.GetAll<Faction>().Count(o => o.Name == f.ShortName || o.ShortName == f.ShortName) == 1)
                .OrderBy(f => f.Name, StringComparer.Ordinal).First();
            factionName = faction.Name;
            argument = byShortName ? faction.ShortName : faction.Name;
            othersBefore = cache.GetAll<Faction>().Where(f => f != faction).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.BossLoadout.Key).ToArray();
        }

        Assert.Equal(0, Run(out var output, "Test boss", fit.hullProduct, fit.slot, "--boss-of", argument));
        Assert.EndsWith($", boss of {factionName}", output.TrimEnd());

        using var after = Open();
        var boss = after.GetAll<Faction>().Single(f => f.Name == factionName);
        var loadout = Loadouts.BossOf(after, boss, out var refusal);
        Assert.Equal(BossRefusal.None, refusal);
        Assert.Equal("Test boss", loadout.Name);
        Assert.Equal(othersBefore, after.GetAll<Faction>().Where(f => f != boss).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.BossLoadout.Key).ToArray());
    }

    // A refused commit with --boss-of leaves the faction as it was: the loadout and the reference land together or not at all.
    [Fact]
    public void A_refused_boss_commit_leaves_the_faction_unchanged()
    {
        var fit = Fit();
        Assert.Equal(0, Run(out _, "Test boss", fit.hullProduct, fit.slot));
        string factionName;
        CultRecordKey bossBefore;
        using (var cache = Open())
        {
            var faction = cache.GetAll<Faction>().Where(f => cache.GetAll<Faction>().Count(o => o.Name == f.Name || o.ShortName == f.Name) == 1)
                .OrderBy(f => f.Name, StringComparer.Ordinal).First();
            factionName = faction.Name;
            bossBefore = faction.BossLoadout.Key;
        }
        var before = Hash(CatalogPath);

        Assert.Equal(1, Run(out var output, "Test boss", fit.hullProduct, fit.openSlot, "--boss-of", factionName));

        Assert.Contains("already exists", output);
        Assert.Equal(before, Hash(CatalogPath));
        using var after = Open();
        Assert.Equal(bossBefore, after.GetAll<Faction>().Single(f => f.Name == factionName).BossLoadout.Key);
    }
}
