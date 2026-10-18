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
        public Faction OutA, OutZ, OutBoth, OutEven, OutNone;

        // zone is the index of the zone the outfitter stands in; -1 stands in none, so distance does not weigh.
        public LoadoutGenerator Outfitter(Faction faction, uint seed, int zone = -1)
        {
            var random = new Random(seed);
            return new LoadoutGenerator(ref random, Items, faction == null ? null : Galaxy, faction == null || zone < 0 ? null : Galaxy.Zones[zone], faction, .5f);
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

    private Rig Build(params string[] lampOrder) => Build(new float[0], lampOrder);

    // binFloors adds one bin of lamp-a's line per floor, named "lamp-a bin" and "lamp-a bin 2"..., by the same maker.
    private Rig Build(float[] binFloors, string[] lampOrder)
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
            seed.Upsert(new Faction { Name = "OutBoth", ShortName = "OTB", Allegiance = { { alpha, 1f }, { zeta, .1f } } });
            seed.Upsert(new Faction { Name = "OutEven", ShortName = "OTE", Allegiance = { { alpha, 1f }, { zeta, 1f } } });
            seed.Upsert(new Faction { Name = "OutLoose", ShortName = "OTL", Allegiance = { { alpha, 1f }, { zeta, .25f } } });
            seed.Upsert(new Faction { Name = "OutZero", ShortName = "OTO", Allegiance = { { alpha, 0f }, { zeta, 0f } } });
            seed.Upsert(new Faction { Name = "OutNone", ShortName = "OTN" });
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
            for (var i = 0; i < binFloors.Length; i++)
                seed.Commit(batch => batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = i == 0 ? "lamp-a bin" : "lamp-a bin " + (i + 1), Design = new CultRecordRef<CraftedItemData>(lamp.Key), Manufacturer = alpha,
                    Bin = new ProductBin { Line = new CultRecordRef<FactionProductData>(new CultRecordKey("k" + Array.IndexOf(lampOrder, "lamp-a"))), Floor = binFloors[i] }
                }, new CultRecordKey("bin" + i)));
            seed.FlushAsync().Wait();
        }

        var cache = AetheriaStores.Open(catalog, Path.Combine(root, "run.cc"), Path.Combine(root, "player.cc"));
        _caches.Add(cache);
        var outA = cache.GetByName<Faction>("OutA");
        var outZ = cache.GetByName<Faction>("OutZ");
        var game = new SavedGame
        {
            // Alpha's headquarters are in zone 0 and Zeta's in zone 1.
            Factions = new[] { cache.RefOf(outA), cache.RefOf(outZ), cache.RefOf(cache.GetByName<Faction>("Alpha")), cache.RefOf(cache.GetByName<Faction>("Zeta")) },
            Relationships = new[] { FactionRelationship.Neutral, FactionRelationship.Neutral, FactionRelationship.Neutral, FactionRelationship.Neutral },
            HomeZones = new Dictionary<int, int> { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 3, 1 } },
            BossZones = new Dictionary<int, int>(),
            DiscoveredZones = new[] { 0 },
            ActionBarBindings = new SavedActionBarBinding[0],
            Exit = -1
        };
        var zones = new[]
        {
            new SavedZone { Name = "Zone 0", Position = new float2(0, 0), AdjacentZones = new[] { 1 }, Factions = new[] { 0, 1 }, Owner = 0, Contents = null },
            new SavedZone { Name = "Zone 1", Position = new float2(1, 0), AdjacentZones = new[] { 0 }, Factions = new[] { 0, 1 }, Owner = 0, Contents = null }
        };
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        return new Rig
        {
            Cache = cache, OutA = outA, OutZ = outZ, OutBoth = cache.GetByName<Faction>("OutBoth"), OutEven = cache.GetByName<Faction>("OutEven"), OutNone = cache.GetByName<Faction>("OutNone"),
            Items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { }),
            Galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { })
        };
    }

    // Small seeds give correlated first draws, so the seeds a distribution test runs over are spread across the range.
    private static uint Seed(int i) => unchecked((uint) (i * 2654435761L));

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

    // An outfitter that reaches both makers favors the one its allegiance weighs ten times heavier: the draw follows
    // ManufacturerPreference rather than splitting evenly.
    [Fact]
    public void TheDrawFollowsTheOutfittersPreference()
    {
        var rig = Build("lamp-z", "lamp-a");
        var lamp = rig.Cache.GetByName<GearData>("Lamp");
        var drawn = Enumerable.Range(1, 200).Select(i => rig.Outfitter(rig.OutBoth, Seed(i)).ChooseProduct(lamp).Name).ToArray();
        var alpha = drawn.Count(name => name == "lamp-a");
        Assert.InRange(alpha, 160, 199);
        Assert.Contains("lamp-z", drawn);
    }

    // An outfitter that reaches no maker of the design gets nothing, however the weights fall: the slot fails.
    [Fact]
    public void AnOutfitterThatReachesNoMakerGetsNoProduct()
    {
        var rig = Build("lamp-z", "lamp-a");
        var generator = rig.Outfitter(rig.OutNone, 1);
        Assert.Null(generator.ChooseProduct(rig.Cache.GetByName<GearData>("Lamp")));
        var failures = new List<string>();
        Assert.Null(Materialize(rig, rig.Fit(), generator.ChooseProduct, failures));
        Assert.Equal("slot 0,0: no available product of Lamp", Assert.Single(failures));
    }

    [Fact]
    public void ANullFactionDrawIsSeededAndReachesEveryMaker()
    {
        var rig = Build("lamp-z", "lamp-a");
        var lamp = rig.Cache.GetByName<GearData>("Lamp");
        Assert.Equal(rig.Outfitter(null, 7).ChooseProduct(lamp).Name, rig.Outfitter(null, 7).ChooseProduct(lamp).Name);
        var seen = Enumerable.Range(1, 64).Select(i => rig.Outfitter(null, Seed(i)).ChooseProduct(lamp).Name).Distinct().OrderBy(name => name).ToArray();
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

    private static int MakerDraws(Rig rig, Faction faction, int zone, string maker, int n)
    {
        var lamp = rig.Cache.GetByName<GearData>("Lamp");
        return Enumerable.Range(1, n).Count(i => rig.Outfitter(faction, Seed(i), zone).ChooseProduct(lamp).Name == maker);
    }

    // Two makers an even allegiance weighs the same differ only by distance: standing in a zone, the maker whose
    // headquarters is nearer comes up about twice as often (1 against 1/2).
    [Fact]
    public void ANearerMakerIsFavored()
    {
        var rig = Build("lamp-z", "lamp-a");
        Assert.InRange(MakerDraws(rig, rig.OutEven, 0, "lamp-a", 300), 180, 220);
        Assert.InRange(MakerDraws(rig, rig.OutEven, 1, "lamp-a", 300), 80, 120);
    }

    // Ruling bin-share-by-floor: a bin of lamp-a's line takes the fraction of lamp-a's stock that would pass its floor, 1 - Phi(Floor),
    // of lamp-a's preference, and the line keeps the rest, so the maker's presence for the design is what it was without the bin.
    [Fact]
    public void ABinTakesItsPassFractionOfItsMakersPreference()
    {
        var alone = Build("lamp-z", "lamp-a");
        var preference = alone.Outfitter(alone.OutEven, 1, 0).ProductWeight(alone.Product("lamp-a"));
        Assert.True(preference > 0);

        var quarter = Build(new[] { .67f }, new[] { "lamp-z", "lamp-a" });
        var generator = quarter.Outfitter(quarter.OutEven, 1, 0);
        Assert.Equal(.2514f * preference, generator.ProductWeight(quarter.Product("lamp-a bin")), 3);
        Assert.Equal(.7486f * preference, generator.ProductWeight(quarter.Product("lamp-a")), 3);
        Assert.Equal(preference, generator.ProductWeight(quarter.Product("lamp-a bin")) + generator.ProductWeight(quarter.Product("lamp-a")), 3);
        // Another maker's product, whose line has no bin, weighs what it did without any bin in the catalog.
        Assert.Equal(alone.Outfitter(alone.OutEven, 1, 0).ProductWeight(alone.Product("lamp-z")), generator.ProductWeight(quarter.Product("lamp-z")), 3);

        var half = Build(new[] { 0f }, new[] { "lamp-z", "lamp-a" });
        generator = half.Outfitter(half.OutEven, 1, 0);
        Assert.Equal(.5f * preference, generator.ProductWeight(half.Product("lamp-a bin")), 3);
        Assert.Equal(.5f * preference, generator.ProductWeight(half.Product("lamp-a")), 3);

        // The maker keeps its presence: Alpha's line and bin together draw as lamp-a alone did.
        var lamp = quarter.Cache.GetByName<GearData>("Lamp");
        var alpha = Enumerable.Range(1, 300).Count(i => quarter.Cache.Get(quarter.Outfitter(quarter.OutEven, Seed(i), 0).ChooseProduct(lamp).Manufacturer).Name == "Alpha");
        Assert.InRange(alpha, 180, 220);
    }

    // A line gives up the sum of its bins' pass fractions and no more: two top-half bins leave it nothing, and a third does not
    // take it below nothing.
    [Fact]
    public void ALineKeepsWhatItsBinsLeaveAndNeverLessThanNothing()
    {
        var two = Build(new[] { 0f, .67f }, new[] { "lamp-z", "lamp-a" });
        var alone = Build("lamp-z", "lamp-a");
        var preference = alone.Outfitter(alone.OutEven, 1, 0).ProductWeight(alone.Product("lamp-a"));
        Assert.Equal((1 - .5f - .2514f) * preference, two.Outfitter(two.OutEven, 1, 0).ProductWeight(two.Product("lamp-a")), 3);

        var three = Build(new[] { 0f, 0f, 0f }, new[] { "lamp-z", "lamp-a" });
        Assert.Equal(0f, three.Outfitter(three.OutEven, 1, 0).ProductWeight(three.Product("lamp-a")));
    }

    // A weight that read the allegiance alone would give 1 : .25 here, an 80% draw; the zone and allegiance together
    // give 1 : .125 for alpha at its own zone, 89%.
    [Fact]
    public void AnAlliedMakerWeighsByItsAllegiance()
    {
        var rig = Build("lamp-z", "lamp-a");
        Assert.InRange(MakerDraws(rig, rig.Cache.GetByName<Faction>("OutLoose"), 0, "lamp-a", 300), 245, 290);
    }

    // When every reachable maker weighs 0 the draw is uniform over them, not a pick of the first or last.
    [Fact]
    public void WhenEveryWeightIsZeroTheDrawIsUniform()
    {
        var rig = Build("lamp-z", "lamp-a");
        var zero = rig.Cache.GetByName<Faction>("OutZero");
        Assert.InRange(MakerDraws(rig, zero, 0, "lamp-a", 300), 100, 200);
        Assert.InRange(MakerDraws(rig, zero, 0, "lamp-z", 300), 100, 200);
    }

    // The wire positions of the new fields: a preset written by one build is read by the next.
    [Fact]
    public void TheProductRefsSitAtTheirWirePositions()
    {
        Assert.Equal(4, typeof(Loadout).GetField(nameof(Loadout.HullProduct)).GetCustomAttribute<KeyAttribute>().IntKey);
        Assert.Equal(3, typeof(LoadoutSlot).GetField(nameof(LoadoutSlot.Product)).GetCustomAttribute<KeyAttribute>().IntKey);
    }
}
