/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
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

    private Rig Build(Offer offer, bool holdless = false)
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

            var skiff = seed.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = Solid(5, 5), Price = 100 });
            var platform = seed.Upsert(new HullData { Name = "Platform", HullType = HullType.Station, Shape = Solid(5, 5), Price = 100 });
            // A hold with no cell cannot take a consumable; the others take up to eight.
            var crate = seed.Upsert(new CargoBayData
            {
                Name = "Crate", Shape = new Shape(), Price = 5, InteriorShape = holdless ? new Shape(1, 1) : Solid(4, 2)
            });
            var dock = seed.Upsert(new DockingBayData { Name = "Dock", Shape = new Shape(), Price = 5, InteriorShape = Solid(1, 1) });
            var capacitor = seed.Upsert(new GearData { Name = "Cap", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new CapacitorData() } });
            var heater = seed.Upsert(new GearData
            {
                Name = "Heater", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1,
                Behaviors = { new ThermotoggleData { HighPass = false }, new HeatData() }
            });
            // Hardpoint gear no hull in this catalog has a hardpoint for: it has no home.
            var orphan = seed.Upsert(new GearData { Name = "Orphan", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1 });

            void Sell(string name, CultRecordKey design, CultRecordRef<Faction> by) =>
                seed.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = by });
            foreach (var (name, design) in new[]
                     {
                         ("Skiff", skiff.Key), ("Platform", platform.Key), ("Crate", crate.Key), ("Dock", dock.Key),
                         ("Cap", capacitor.Key), ("Heater", heater.Key), ("Orphan", orphan.Key)
                     })
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

    private static void AssertWithinThreeSigma(int observed, float chance, string what)
    {
        var expected = Seeds * chance;
        var sigma = MathF.Sqrt(Seeds * chance * (1 - chance));
        Assert.True(MathF.Abs(observed - expected) <= 3 * sigma,
            $"{what}: {observed} of {Seeds}, expected {expected} +/- {3 * sigma}");
    }

    [Fact]
    public void AFewStationsStockConsumables()
    {
        var rig = Build(Offer.Reachable);
        var stocked = 0;
        var makers = new HashSet<string>();
        for (var i = 0; i < Seeds; i++)
        {
            var random = new Random(SeedOf(i));
            var pack = rig.Generator(ref random).GenerateStationLoadout();
            Assert.NotEmpty(pack.CargoContents.SelectMany(bay => bay)); // the gear stock is read from the same pack
            var held = Consumables(pack);
            if (held.Length == 0) continue;
            stocked++;
            Assert.InRange(held.Length, 1, LoadoutGenerator.StationConsumableUnits);
            var brands = held.Select(item => rig.Items.Brand(item)).ToArray();
            var product = Assert.Single(brands.Select(brand => brand.Product.Name).Distinct()); // exactly one product
            Assert.Contains(product, new[] { "Overdrive by Maker", "Overdrive by Ally" }); // sold by a maker on offer
            makers.Add(product);
        }
        AssertWithinThreeSigma(stocked, LoadoutGenerator.StationConsumableChance, "stations stocking a consumable");
        Assert.Equal(2, makers.Count); // the pick is among the products on offer, not always the first
    }

    [Fact]
    public void FewShipsCarryOne()
    {
        var rig = Build(Offer.Reachable);
        var carrying = 0;
        for (var i = 0; i < Seeds; i++)
        {
            var random = new Random(SeedOf(i));
            var held = Consumables(rig.Generator(ref random).GenerateShipLoadout());
            if (held.Length == 0) continue;
            carrying++;
            Assert.Equal(LoadoutGenerator.ShipConsumableUnits, held.Length);
        }
        AssertWithinThreeSigma(carrying, LoadoutGenerator.ShipConsumableChance, "ships carrying a consumable");

        // A ship whose only hold has no free cell takes none, and generating it does not throw.
        var holdless = Build(Offer.Reachable, holdless: true);
        for (var i = 0; i < 100; i++)
        {
            var random = new Random(SeedOf(i));
            Assert.Empty(Consumables(holdless.Generator(ref random).GenerateShipLoadout()));
        }
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

        var baseline = AfterGeneration(plain);
        Assert.Equal(baseline, AfterGeneration(gated));
        Assert.NotEqual(baseline, AfterGeneration(control));
    }
}
