/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using Random = CultMath.Random;

// Consumables are scarce (ruling consumables-scarce): a quarter of stations stock one product, one ship in ten carries
// one unit, and the one availability rule (LoadoutGenerator.AvailableProducts) decides which products a faction is
// offered. Every count here is measured on packs the production generator returns, from fixed seeds, so the runs are
// deterministic; the 3-sigma band is the binomial one, sigma = sqrt(n p (1 - p)) with n = 400 seeds, and a rule that
// breaks (chance 1, chance 0, an unconditional roll) lands far outside it.
public sealed class ConsumableSupplyTests : IDisposable
{
    private const int Seeds = 400;

    // Ships are cheap to generate and the ruling's one in ten is a narrow target: at 3000 seeds the 3-sigma band is
    // +/- 49 around 300, which chances of .12, .15 and .05 all fall outside.
    private const int ShipSeeds = 3000;

    // The ruling's numbers, spelled out: a quarter of stations stock one product of 1-2 units, one ship in ten
    // carries one unit. The tests read these, never LoadoutGenerator's constants, so a changed constant fails them.
    private const float StationChance = .25f, ShipChance = .1f;
    private const int StationUnits = 2, ShipUnits = 1;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-consumablesupply-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();
    private int _rigs;

    public ConsumableSupplyTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private enum Offer { None, Reachable, Stranger }

    // Pinned is the original one-hull-each catalog with a roomy hold, which AvailabilityGatesTheDraw's pinned values
    // were measured on. Tight adds a second ship hull, a second station hull and a 2x2 gear whose sixteen picks fill
    // a 12-cell hold, as the shipped holds do. Holdless gives every hold no free cell. DockRefuses is Tight with a
    // docking bay that has no free cell, so a station's first bay refuses and only a later bay can take a consumable.
    private enum Holds { Pinned, Tight, Holdless, DockRefuses }

    // One catalog: a ship hull, a station hull and the station's required fittings, all made by Maker; and, when
    // offered, one consumable design with a product from each of Maker, its ally and (unless the offer is Stranger
    // only) a stranger Maker's allegiance does not name.
    private sealed class Rig
    {
        public CultCache Cache;
        public ItemManager Items;
        public Galaxy Galaxy;
        public Faction Maker;
        public LoadoutGenerator Generator(ref Random random) => new LoadoutGenerator(ref random, Items, Galaxy, null, Maker, .5f);
    }

    private static Shape Solid(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    private Rig Build(Offer offer, Holds holds = Holds.Pinned)
    {
        var root = Path.Combine(_root, "rig" + _rigs++);
        Directory.CreateDirectory(root);
        var catalog = Path.Combine(root, "Aetheria.cc");
        CultRecordRef<Faction> maker;
        using (var seed = AetheriaStores.Open(catalog, catalogWritable: true))
        {
            seed.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            var ally = seed.Upsert(new Faction { Name = "Ally", ShortName = "ALY" });
            var stranger = seed.Upsert(new Faction { Name = "Stranger", ShortName = "STR" });
            maker = seed.Upsert(new Faction { Name = "Maker", ShortName = "MKR", Allegiance = { { ally, 1f } } });

            var skiff = seed.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = Solid(4, 3), Price = 100 });
            var platform = seed.Upsert(new HullData { Name = "Platform", HullType = HullType.Station, Shape = Solid(5, 5), Price = 100 });
            // A hold with no cell cannot take a consumable.
            var crate = seed.Upsert(new CargoBayData
            {
                Name = "Crate", Shape = new Shape(), Price = 5,
                InteriorShape = holds == Holds.Holdless ? new Shape(1, 1) : holds == Holds.Pinned ? Solid(16, 12) : Solid(4, 3)
            });
            var dock = seed.Upsert(new DockingBayData { Name = "Dock", Shape = new Shape(), Price = 5, InteriorShape = holds == Holds.DockRefuses ? new Shape(1, 1) : Solid(1, 1) });
            var capacitor = seed.Upsert(new GearData { Name = "Cap", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new CapacitorData() } });
            var heater = seed.Upsert(new GearData
            {
                Name = "Heater", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1,
                Behaviors = { new ThermotoggleData { HighPass = false }, new HeatData() }
            });
            // Hardpoint gear no hull in this catalog has a hardpoint for: it has no home.
            var orphan = seed.Upsert(new GearData { Name = "Orphan", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1 });

            var sold = new List<(string, CultRecordKey)>();
            if (holds != Holds.Pinned)
            {
                sold.Add(("Cutter", seed.Upsert(new HullData { Name = "Cutter", HullType = HullType.Ship, Shape = Solid(3, 3), Price = 100 }).Key));
                sold.Add(("Outpost", seed.Upsert(new HullData { Name = "Outpost", HullType = HullType.Station, Shape = Solid(4, 4), Price = 100 }).Key));
                sold.Add(("Plate", seed.Upsert(new GearData { Name = "Plate", Hardpoint = HardpointType.Tool, Shape = Solid(2, 2), Price = 1 }).Key));
            }

            void Sell(string name, CultRecordKey design, CultRecordRef<Faction> by) =>
                seed.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = by });
            foreach (var (name, design) in new[]
                     {
                         ("Skiff", skiff.Key), ("Platform", platform.Key), ("Crate", crate.Key), ("Dock", dock.Key),
                         ("Cap", capacitor.Key), ("Heater", heater.Key), ("Orphan", orphan.Key)
                     }.Concat(sold))
                Sell(name + " by Maker", design, maker);

            if (offer != Offer.None)
            {
                var overdrive = seed.Upsert(new ConsumableItemData { Name = "Overdrive", Duration = 1f, Shape = new Shape(), Price = 50 });
                if (offer == Offer.Reachable)
                {
                    Sell("Overdrive by Maker", overdrive.Key, maker);
                    Sell("Overdrive by Ally", overdrive.Key, ally);
                }
                Sell("Overdrive by Stranger", overdrive.Key, stranger);
            }
            seed.FlushAsync().Wait();
        }

        var cache = AetheriaStores.Open(catalog, Path.Combine(root, "run.cc"), Path.Combine(root, "player.cc"));
        _caches.Add(cache);
        var game = new SavedGame
        {
            Factions = new[] { maker },
            Relationships = new[] { FactionRelationship.Neutral },
            HomeZones = new Dictionary<int, int> { { 0, 0 } },
            BossZones = new Dictionary<int, int>(),
            DiscoveredZones = new[] { 0 },
            ActionBarBindings = new SavedActionBarBinding[0],
            Exit = -1
        };
        var zones = new[]
        {
            new SavedZone { Name = "Zone 0", Position = new float2(0, 0), AdjacentZones = Array.Empty<int>(), Factions = new[] { 0 }, Owner = 0, Contents = null }
        };
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        return new Rig
        {
            Cache = cache,
            Items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { }),
            Galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { }),
            Maker = cache.GetByName<Faction>("Maker")
        };
    }

    private static ConsumableItem[] Consumables(EntityPack pack) =>
        pack.CargoContents.Concat(pack.DockingBayContents ?? Array.Empty<(int2 position, ItemInstance item)[]>())
            .SelectMany(bay => bay).Select(entry => entry.item).OfType<ConsumableItem>().ToArray();

    // Distinct, fixed seeds: the same 400 generators on every run.
    private static uint SeedOf(int i) => (uint) (i * 7919 + 13);

    private static void AssertWithinThreeSigma(int observed, int of, float chance, string what)
    {
        var expected = of * chance;
        var sigma = MathF.Sqrt(of * chance * (1 - chance));
        Assert.True(MathF.Abs(observed - expected) <= 3 * sigma,
            $"{what}: {observed} of {of}, expected {expected} +/- {3 * sigma}");
    }

    // Hits and trials per hull name, so a chance that scales with hull size is measured on each hull it scales.
    private static void AssertEachHullWithinThreeSigma(Dictionary<string, (int hits, int of)> byHull, float chance, string what)
    {
        Assert.Equal(2, byHull.Count); // both hulls are drawn
        foreach (var (hull, (hits, of)) in byHull) AssertWithinThreeSigma(hits, of, chance, $"{what} on {hull}");
    }

    [Fact]
    public void AFewStationsStockConsumables()
    {
        var rig = Build(Offer.Reachable, Holds.Tight);
        var stocked = 0;
        var byHull = new Dictionary<string, (int hits, int of)>();
        var makers = new HashSet<string>();
        int fullest = 0, thinnest = int.MaxValue;
        for (var i = 0; i < Seeds; i++)
        {
            var random = new Random(SeedOf(i));
            var pack = rig.Generator(ref random).GenerateStationLoadout();
            Assert.NotEmpty(pack.CargoContents.SelectMany(bay => bay)); // the gear stock is read from the same pack
            var held = Consumables(pack);
            var hull = rig.Items.GetData(pack.Hull).Name;
            byHull.TryGetValue(hull, out var seen);
            byHull[hull] = (seen.hits + (held.Length > 0 ? 1 : 0), seen.of + 1);
            if (held.Length == 0) continue;
            stocked++;
            fullest = Math.Max(fullest, held.Length);
            thinnest = Math.Min(thinnest, held.Length);
            Assert.InRange(held.Length, 1, StationUnits);
            var brands = held.Select(item => rig.Items.Brand(item)).ToArray();
            var product = Assert.Single(brands.Select(brand => brand.Product.Name).Distinct()); // exactly one product
            Assert.Contains(product, new[] { "Overdrive by Maker", "Overdrive by Ally" }); // sold by a maker on offer
            makers.Add(product);
        }
        AssertWithinThreeSigma(stocked, Seeds, StationChance, "stations stocking a consumable");
        AssertEachHullWithinThreeSigma(byHull, StationChance, "stations stocking a consumable");
        Assert.Equal(StationUnits, fullest); // a roll can stock up to the maximum
        Assert.Equal(1, thinnest); // and as few as one
        Assert.Equal(2, makers.Count); // the pick is among the products on offer, not always the first
    }

    [Fact]
    public void FewShipsCarryOne()
    {
        var rig = Build(Offer.Reachable, Holds.Tight);
        var carrying = 0;
        var byHull = new Dictionary<string, (int hits, int of)>();
        for (var i = 0; i < ShipSeeds; i++)
        {
            var random = new Random(SeedOf(i));
            var pack = rig.Generator(ref random).GenerateShipLoadout();
            var held = Consumables(pack);
            var hull = rig.Items.GetData(pack.Hull).Name;
            byHull.TryGetValue(hull, out var seen);
            byHull[hull] = (seen.hits + (held.Length > 0 ? 1 : 0), seen.of + 1);
            if (held.Length == 0) continue;
            carrying++;
            Assert.Equal(ShipUnits, held.Length);
        }
        AssertWithinThreeSigma(carrying, ShipSeeds, ShipChance, "ships carrying a consumable");
        AssertEachHullWithinThreeSigma(byHull, ShipChance, "ships carrying a consumable");

        // A ship whose only hold has no free cell takes none, and generating it does not throw.
        var holdless = Build(Offer.Reachable, Holds.Holdless);
        for (var i = 0; i < 100; i++)
        {
            var random = new Random(SeedOf(i));
            Assert.Empty(Consumables(holdless.Generator(ref random).GenerateShipLoadout()));
        }
    }

    // The first bay may refuse (a docking bay with no free cell); a later bay that takes the unit still holds it.
    [Fact]
    public void AStationWhoseFirstBayRefusesStocksInAnotherBay()
    {
        var rig = Build(Offer.Reachable, Holds.DockRefuses);
        var stocked = 0;
        for (var i = 0; i < Seeds; i++)
        {
            var random = new Random(SeedOf(i));
            if (Consumables(rig.Generator(ref random).GenerateStationLoadout()).Length > 0) stocked++;
        }
        AssertWithinThreeSigma(stocked, Seeds, StationChance, "stations stocking a consumable past a refusing first bay");
    }

    // The ruled rates hold on the shipped catalog's own station and ship hulls and holds, with one consumable on offer
    // (the shipped catalog has none yet): a station rolled to stock one must end holding one, however full gear fills
    // its hold.
    [Fact]
    public void TheShippedHullsKeepTheRuledRates()
    {
        var repo = Repo();
        var catalog = Path.Combine(_root, "shipped.cc");
        File.Copy(Path.Combine(repo, "GameData", "Aetheria.cc"), catalog);
        var cache = new CultCache(CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }).Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null)));
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(_root, "shipped-run.cc")), AetheriaStores.RunTypes);
        _caches.Add(cache);
        var maker = cache.RefOf(cache.GetAll<Faction>().First());
        var overdrive = cache.Upsert(new ConsumableItemData { Name = "Overdrive", Duration = 1f, Shape = new Shape(), Price = 50 });
        cache.Upsert(new FactionProductData { Name = "Overdrive by Maker", Design = new CultRecordRef<CraftedItemData>(overdrive.Key), Manufacturer = maker });

        var authored = AuthoredSettings.Load(repo);
        var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"), authored.Read<NameGeneratorSettings>("NameGeneratorSettings"),
            cache, new PlayerSettings(), Directory.CreateDirectory(Path.Combine(_root, "Narrative")), _ => { }, null, 1);
        var items = new ItemManager(cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });

        int stations = 0, ships = 0;
        for (var i = 0; i < Seeds; i++)
        {
            items.Random = new Random(SeedOf(i));
            var random = new Random(SeedOf(i));
            var generator = new LoadoutGenerator(ref random, items, galaxy, galaxy.Entrance, null, .5f);
            if (Consumables(generator.GenerateStationLoadout()).Length > 0) stations++;
            if (Consumables(generator.GenerateShipLoadout()).Length > 0) ships++;
        }
        AssertWithinThreeSigma(stations, Seeds, StationChance, "shipped stations stocking a consumable");
        AssertWithinThreeSigma(ships, Seeds, ShipChance, "shipped ships carrying a consumable");
    }

    private static string Repo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }

    [Fact]
    public void ConsumablesNeedNoHome()
    {
        var rig = Build(Offer.Reachable);
        var random = new Random(1);
        var generator = rig.Generator(ref random);
        Assert.NotEmpty(generator.RandomProducts<ConsumableItemData>(8, 0));
        Assert.Empty(generator.RandomProducts<GearData>(8, 0, design => design.Name == "Orphan"));
        Assert.NotEmpty(generator.RandomProducts<GearData>(8, 0, design => design.Name == "Cap")); // a gear with a home still is
    }

    [Fact]
    public void AvailabilityGatesTheDraw()
    {
        // The same catalog with and without a consumable design whose every maker the faction cannot reach.
        var gated = Build(Offer.Stranger);
        var plain = Build(Offer.None);
        var control = Build(Offer.Reachable); // positive control: the consumable on offer does move the generator

        float[] AfterGeneration(Rig rig)
        {
            rig.Items.Random = new Random(99);
            var random = new Random(5);
            var generator = rig.Generator(ref random);
            generator.GenerateStationLoadout();
            for (var i = 0; i < 3; i++) generator.GenerateShipLoadout();
            return Enumerable.Range(0, 4).Select(_ => generator.Random.NextFloat())
                .Concat(Enumerable.Range(0, 4).Select(_ => rig.Items.Random.NextFloat())).ToArray();
        }

        // What the plain catalog leaves in both generators' Random, measured on the tree before consumables existed
        // (origin/master d4a27def, the same fixture and seeds). The gated and plain catalogs agreeing is not enough:
        // a step that drew unconditionally would draw in both. This pins the value itself.
        var beforeConsumables = new[]
        {
            0.85624266f, 0.037778974f, 0.7931649f, 0.3596493f, 0.17511952f, 0.6642336f, 0.31897342f, 0.5977364f
        };
        var plainAfter = AfterGeneration(plain);
        Assert.Equal(beforeConsumables, plainAfter);
        Assert.Equal(plainAfter, AfterGeneration(gated));
        Assert.NotEqual(plainAfter, AfterGeneration(control));
    }
}
