using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using Random = CultMath.Random;

// Ruling loadout-product-optional-context-chooses: a slot or fit may name a product; a design-only slot is chosen by
// the outfitter (LoadoutGenerator.ChooseProduct), never by record-key order.
public sealed class ProductChoiceTests : IDisposable
{
    private static readonly int2 HardpointCell = new int2(0, 0);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-productchoice-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();
    private int _catalogs;

    public ProductChoiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    // One Skiff (made by Alpha only) and one Lamp with a product from each of Alpha and Zeta. Two outfitting factions:
    // OutA's allegiance names only Alpha, OutZ's only Zeta. lampKeys are the Lamp products' record keys in insertion
    // order [lamp-z, lamp-a], so a permutation shuffles both keys and enumeration.
    private sealed class Rig
    {
        public CultCache Cache;
        public ItemManager Items;
        public Galaxy Galaxy;
        public Faction OutA, OutZ;

        public LoadoutGenerator Outfitter(Faction faction, uint seed)
        {
            var random = new Random(seed);
            return new LoadoutGenerator(ref random, Items, faction == null ? null : Galaxy, null, faction, .5f);
        }

        public FactionProductData Product(string name) => Cache.GetAll<FactionProductData>().Single(p => p.Name == name);
        public CultRecordRef<FactionProductData> ProductRef(string name) => Cache.RefOf(Product(name));

        public Loadout Fit(CultRecordRef<FactionProductData> lamp = default) => new Loadout
        {
            Name = "Lamp skiff",
            Hull = Cache.RefOf(Cache.GetByName<HullData>("Skiff")),
            HullProduct = ProductRef("Skiff by Alpha"),
            Slots = { new LoadoutSlot { Position = HardpointCell, Design = Cache.RefOf<EquippableItemData>(Cache.GetByName<GearData>("Lamp")), Product = lamp } },
            WeaponGroups = new int[0][]
        };

        public string LampBrand(Entity ship)
        {
            var unit = ship.Equipment.Single(item => item.EquippableItem != ship.Hull && item.Position.Equals(HardpointCell)).EquippableItem;
            return Items.Brand(unit).Product.Name;
        }
    }

    private Rig Build(params string[] lampOrder)
    {
        var root = Path.Combine(_root, "rig" + _catalogs++);
        Directory.CreateDirectory(root);
        var catalog = Path.Combine(root, "Aetheria.cc");
        using (var seed = AetheriaStores.Open(catalog, catalogWritable: true))
        {
            seed.Upsert(new VerseGrammar { Revision = 1 });
            var alpha = seed.Upsert(new Faction { Name = "Alpha", ShortName = "ALP" });
            var zeta = seed.Upsert(new Faction { Name = "Zeta", ShortName = "ZET" });
            var mu = seed.Upsert(new Faction { Name = "Mu", ShortName = "MU" });
            seed.Upsert(new Faction { Name = "OutA", ShortName = "OTA", Allegiance = { { alpha, 1f } } });
            seed.Upsert(new Faction { Name = "OutZ", ShortName = "OTZ", Allegiance = { { zeta, 1f } } });
            var shape = new Shape(3, 3);
            foreach (var cell in shape.AllCoordinates) shape[cell] = true;
            var skiff = seed.Upsert(new HullData
            {
                Name = "Skiff", HullType = HullType.Ship, Shape = shape, Price = 100,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
            });
            var lamp = seed.Upsert(new GearData { Name = "Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10 });
            void Sell(string name, CultRecordKey design, CultRecordRef<Faction> maker, string key) =>
                seed.Commit(batch => batch.Upsert(typeof(FactionProductData), new FactionProductData
                    { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = maker }, new CultRecordKey(key)));
            Sell("Skiff by Alpha", skiff.Key, alpha, "skiff-a");
            var makers = new Dictionary<string, CultRecordRef<Faction>> { ["lamp-a"] = alpha, ["lamp-z"] = zeta, ["lamp-m"] = mu };
            // lampOrder holds product names; the record key of the i-th inserted is "k" + i, so keys and insertion order
            // both follow the permutation.
            for (var i = 0; i < lampOrder.Length; i++) Sell(lampOrder[i], lamp.Key, makers[lampOrder[i]], "k" + i);
            seed.FlushAsync().Wait();
        }

        var cache = AetheriaStores.Open(catalog, Path.Combine(root, "run.cc"), Path.Combine(root, "player.cc"));
        _caches.Add(cache);
        var outA = cache.GetByName<Faction>("OutA");
        var outZ = cache.GetByName<Faction>("OutZ");
        var game = new SavedGame
        {
            Factions = new[] { cache.RefOf(outA), cache.RefOf(outZ) },
            Relationships = new[] { FactionRelationship.Neutral, FactionRelationship.Neutral },
            HomeZones = new Dictionary<int, int> { { 0, 0 }, { 1, 0 } },
            BossZones = new Dictionary<int, int>(),
            DiscoveredZones = new[] { 0 },
            ActionBarBindings = new SavedActionBarBinding[0],
            Exit = -1
        };
        var zones = new[]
        {
            new SavedZone { Name = "Zone 0", Position = new float2(0, 0), AdjacentZones = Array.Empty<int>(), Factions = new[] { 0, 1 }, Owner = 0, Contents = null }
        };
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        return new Rig
        {
            Cache = cache, OutA = outA, OutZ = outZ,
            Items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { }),
            Galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { })
        };
    }

    private static Entity Materialize(Rig rig, Loadout loadout, Func<CraftedItemData, FactionProductData> choose, List<string> failures) =>
        Loadouts.Materialize(rig.Items, null, loadout, choose, failures);

    [Fact]
    public void ADifferentOutfitterChoosesADifferentProduct()
    {
        var rig = Build("lamp-z", "lamp-a");
        var failures = new List<string>();
        Assert.Equal("lamp-a", rig.LampBrand(Materialize(rig, rig.Fit(), rig.Outfitter(rig.OutA, 1).ChooseProduct, failures)));
        Assert.Equal("lamp-z", rig.LampBrand(Materialize(rig, rig.Fit(), rig.Outfitter(rig.OutZ, 1).ChooseProduct, failures)));
        Assert.Empty(failures);
    }

    [Fact]
    public void ANullFactionDrawIsSeededAndReachesEveryMaker()
    {
        var rig = Build("lamp-z", "lamp-a");
        var lamp = rig.Cache.GetByName<GearData>("Lamp");
        Assert.Equal(rig.Outfitter(null, 7).ChooseProduct(lamp).Name, rig.Outfitter(null, 7).ChooseProduct(lamp).Name);
        var seen = Enumerable.Range(1, 64).Select(seed => rig.Outfitter(null, (uint) seed).ChooseProduct(lamp).Name).Distinct().OrderBy(name => name).ToArray();
        Assert.Equal(new[] { "lamp-a", "lamp-z" }, seen);
    }

    [Fact]
    public void APinnedProductIsBuiltInEveryContext()
    {
        var rig = Build("lamp-z", "lamp-a");
        var failures = new List<string>();
        var pinned = rig.Fit(rig.ProductRef("lamp-z"));
        Assert.Equal("lamp-z", rig.LampBrand(Materialize(rig, pinned, rig.Outfitter(rig.OutA, 1).ChooseProduct, failures)));
        Assert.Equal("lamp-z", rig.LampBrand(Materialize(rig, pinned, _ => null, failures)));
        Assert.Empty(failures);

        var foreign = rig.Fit(rig.ProductRef("Skiff by Alpha"));
        Assert.Null(Materialize(rig, foreign, rig.Outfitter(null, 1).ChooseProduct, failures));
        Assert.Equal("slot 0,0: product Skiff by Alpha is not a product of Lamp", Assert.Single(failures));
    }

    [Fact]
    public void ShuffledKeysDoNotChangeTheChoice()
    {
        var orders = new[] { new[] { "lamp-a", "lamp-m", "lamp-z" }, new[] { "lamp-z", "lamp-a", "lamp-m" }, new[] { "lamp-m", "lamp-z", "lamp-a" } };
        var rigs = orders.Select(order => Build(order)).ToArray();
        foreach (var faction in new Func<Rig, Faction>[] { _ => null, rig => rig.OutA })
            for (uint seed = 1; seed <= 16; seed++)
            {
                var chosen = rigs.Select(rig => rig.Outfitter(faction(rig), seed).ChooseProduct(rig.Cache.GetByName<GearData>("Lamp"))?.Name).Distinct().ToArray();
                Assert.Single(chosen);
            }
    }

    [Fact]
    public void CaptureKeepsTheLotsProduct()
    {
        var rig = Build("lamp-z", "lamp-a");
        var failures = new List<string>();
        var ship = Materialize(rig, rig.Fit(rig.ProductRef("lamp-z")), rig.Outfitter(rig.OutA, 1).ChooseProduct, failures);
        var captured = Loadouts.Capture(rig.Items, ship, "captured");
        Assert.Equal(rig.ProductRef("lamp-z").Key, captured.Slots.Single().Product.Key);
        Assert.Equal(rig.ProductRef("Skiff by Alpha").Key, captured.HullProduct.Key);

        var rebuilt = Materialize(rig, captured, _ => null, failures);
        Assert.Empty(failures);
        Assert.Equal("lamp-z", rig.LampBrand(rebuilt));
        Assert.Equal("Skiff by Alpha", rig.Items.Brand(rebuilt.Hull).Product.Name);
    }

    [Fact]
    public void AChooserOfAnotherDesignIsRefused()
    {
        var rig = Build("lamp-z", "lamp-a");
        var failures = new List<string>();
        Assert.Null(Materialize(rig, rig.Fit(), _ => rig.Product("Skiff by Alpha"), failures));
        Assert.Equal("slot 0,0: product Skiff by Alpha is not a product of Lamp", Assert.Single(failures));
    }

    // The wire positions of the new fields: a preset written by one build is read by the next.
    [Fact]
    public void TheProductRefsSitAtTheirWirePositions()
    {
        Assert.Equal(4, typeof(Loadout).GetField(nameof(Loadout.HullProduct)).GetCustomAttribute<KeyAttribute>().IntKey);
        Assert.Equal(3, typeof(LoadoutSlot).GetField(nameof(LoadoutSlot.Product)).GetCustomAttribute<KeyAttribute>().IntKey);
    }
}
