using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's loadout-commit command (aetheria-release, cut boss-hull-derived; ruling boss-loadouts-agent-authored), run on a scratch
// copy of the shipped catalog. Commit writes one Loadout at Loadouts.KeyOf(name) with the records its names resolve to; every
// refusal leaves the catalog byte-identical; a Loadout it commits on a Boss hull's product is that maker's boss (Loadouts.BossOf).
public sealed class LoadoutCommitCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loadout-commit-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

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
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
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

    private int Run(out string output, params string[] args)
    {
        var writer = new StringWriter();
        var code = LoadoutCommitCommand.Run(args, _root, writer);
        output = writer.ToString();
        return code;
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
    }

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
    [InlineData("mod-design")]
    public void A_name_that_does_not_resolve_or_a_loadout_that_cannot_be_built_is_refused_and_nothing_is_written(string refusal)
    {
        var fit = Fit();
        var canary = "Canary9c1e";
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        T Copy<T>(T document) => MessagePack.MessagePackSerializer.Deserialize<T>(MessagePack.MessagePackSerializer.Serialize(document, options), options);
        using (var cache = Open())
        {
            switch (refusal)
            {
                case "ambiguous-hull-product":
                    cache.Upsert(Copy(cache.GetAll<FactionProductData>().Single(p => p.Name == fit.hullProduct)));
                    break;
                case "ambiguous-design":
                    cache.Upsert(Copy(cache.GetAll<EquippableItemData>().Single(d => d.Name == fit.designName)));
                    break;
                case "mod-design":
                    var mod = Copy(cache.GetAll<EquippableItemData>().Single(d => d.Name == fit.designName));
                    mod.Name = "Mod gadget";
                    cache.Upsert(mod.GetType(), mod, new CultRecordKey("mod-ship:mod.gadget"));
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
            "mod-design" => new[] { "Test boss", fit.hullProduct, $"{cell[0]},{cell[1]},{cell[2]},Mod gadget" },
            _ => new[] { "Test boss", fit.hullProduct, fit.slot }
        };
        var before = Hash(CatalogPath);

        Assert.Equal(1, Run(out var output, args));

        Assert.StartsWith("refused: ", output);
        Assert.DoesNotContain(canary, output);
        if (refusal == "mod-design") Assert.Contains("names mod designs", output);
        Assert.Equal(before, Hash(CatalogPath));
        using var after = Open();
        Assert.Empty(after.GetAll<Loadout>());
    }

    // Ruling boss-loadouts-agent-authored end to end: a Boss hull and its maker's product in the catalog, a Loadout written by the
    // command on that product, and the maker's boss is that Loadout.
    [Fact]
    public void A_loadout_committed_on_a_boss_hulls_product_is_its_makers_boss()
    {
        var fit = Fit();
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        T Copy<T>(T document) => MessagePack.MessagePackSerializer.Deserialize<T>(MessagePack.MessagePackSerializer.Serialize(document, options), options);
        using (var cache = Open())
        {
            var likeProduct = cache.GetAll<FactionProductData>().Single(p => p.Name == fit.hullProduct);
            var maker = cache.GetAll<Faction>().OrderBy(f => f.Name, StringComparer.Ordinal).Last();
            var boss = Copy((HullData) cache.Get(likeProduct.Design));
            boss.Name = "Command Boss";
            boss.Boss = true;
            var bossRef = cache.Upsert(boss);
            var product = Copy(likeProduct);
            product.Name = "Command Boss Mk I";
            product.Design = new CultRecordRef<CraftedItemData>(bossRef.Key);
            product.Manufacturer = cache.RefOf(maker);
            cache.Upsert(product);
            cache.FlushAsync().Wait();
        }

        Assert.Equal(0, Run(out _, "Command Boss loadout", "Command Boss Mk I", fit.slot));

        using var reopened = Open();
        var makerAfter = reopened.GetAll<Faction>().OrderBy(f => f.Name, StringComparer.Ordinal).Last();
        var found = Loadouts.BossOf(reopened, makerAfter, out var refusal);
        Assert.Equal(BossRefusal.None, refusal);
        Assert.Equal("Command Boss loadout", found.Name);
        Assert.Equal("Command Boss", reopened.Get(found.Hull).Name);
        foreach (var other in reopened.GetAll<Faction>().Where(f => f != makerAfter))
            Assert.Null(Loadouts.BossOf(reopened, other, out _));
    }
}
