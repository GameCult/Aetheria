using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using Random = CultMath.Random;

// The run lifecycle over all three stores: a save is one commit with stable zone keys, and a clear removes every run record.
public sealed class RunSaveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-runsave-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private string Run => Path.Combine(_root, "run.cc");
    private string Player => Path.Combine(_root, "player.cc");

    public RunSaveTests()
    {
        Directory.CreateDirectory(_root);
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Commit(batch =>
        {
            batch.Upsert(new VerseGrammar { Revision = 1 });
            batch.Upsert(new Faction { Name = "Adrasteia", ShortName = "ADR" });
        });
    }

    public void Dispose() => Directory.Delete(_root, true);

    private CultCache Open() => AetheriaStores.Open(Catalog, Run, Player);

    [Fact]
    public void RepeatedSavesKeepRecordCountConstant()
    {
        ZonePack contents = null;
        for (var i = 0; i < 5; i++)
        {
            using (var cache = Open())
            {
                contents ??= StageZoneContents(cache);
                RunSave.Commit(cache, Game(cache), Zones(3, contents, 0), new ProvenanceLedger());
            }

            using (var cache = Open())
            {
                Assert.Equal(8, RunRecordCount(cache));
                Assert.Equal(8, RecordsIn(Run));
                Assert.Equal(new[] { "savedzone-0", "savedzone-1", "savedzone-2" }, SavedZoneKeys(cache));
            }
        }
    }

    [Fact]
    public void SaveRemovesZonesNoLongerInTheRun()
    {
        using (var cache = Open())
        {
            RunSave.Commit(cache, Game(cache), Zones(3, null, -1), new ProvenanceLedger());
            RunSave.Commit(cache, Game(cache), Zones(2, null, -1), new ProvenanceLedger());
        }

        using (var cache = Open())
            Assert.Equal(new[] { "savedzone-0", "savedzone-1" }, SavedZoneKeys(cache));
    }

    [Fact]
    public void SaveWritesOnlyTheRunStore()
    {
        using (var cache = Open())
            cache.Commit(batch => batch.Upsert(new PlayerSettings()));
        var before = new[] { Catalog, Player }.Select(Hash).ToArray();

        using (var cache = Open())
            RunSave.Commit(cache, Game(cache), Zones(3, StageZoneContents(cache), 0), new ProvenanceLedger());

        Assert.Equal(before, new[] { Catalog, Player }.Select(Hash).ToArray());
        Assert.Equal(8, RecordsIn(Run));
    }

    [Fact]
    public void ResumeThenSaveKeepsUnloadedZoneContents()
    {
        using (var cache = Open())
            RunSave.Commit(cache, Game(cache), Zones(2, StageZoneContents(cache), 1), new ProvenanceLedger());

        using (var cache = Open())
        {
            var galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });
            var itemManager = new ItemManager(cache, new ProvenanceLedger(), TestSettings(), _ => { });
            galaxy.Zones[0].Contents = new Zone(itemManager, new PlanetSettings(), new ZonePack(), galaxy.Zones[0], galaxy);
            var (game, zones) = RunSave.Capture(cache, galaxy, galaxy.Zones[0].Contents, null, new SavedActionBarBinding[0], 0);
            RunSave.Commit(cache, game, zones, RunSave.Lots(cache));
        }

        using (var cache = Open())
        {
            var unloaded = cache.Get(cache.GetGlobal<SavedGame>().Zones[1]);
            Assert.NotNull(unloaded.Contents);
            Assert.NotNull(cache.Get(unloaded.Contents.Orbits[1]));
            Assert.NotNull(cache.Get(unloaded.Contents.Planets[0]));
        }
    }

    // The run's credits ride the save: what Capture is given is what a later Continue reads from the run store.
    [Fact]
    public void CreditsSurviveContinue()
    {
        using (var cache = Open())
            RunSave.Commit(cache, Game(cache), Zones(2, StageZoneContents(cache), 0), new ProvenanceLedger());

        using (var cache = Open())
        {
            var galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });
            var itemManager = new ItemManager(cache, new ProvenanceLedger(), TestSettings(), _ => { });
            galaxy.Zones[0].Contents = new Zone(itemManager, new PlanetSettings(), new ZonePack(), galaxy.Zones[0], galaxy);
            var (game, zones) = RunSave.Capture(cache, galaxy, galaxy.Zones[0].Contents, null, new SavedActionBarBinding[0], 4242);
            RunSave.Commit(cache, game, zones, RunSave.Lots(cache));
        }

        using (var cache = Open())
            Assert.Equal(4242, cache.GetGlobal<SavedGame>().Credits);
    }

    [Fact]
    public void ClearRemovesEveryRunRecord()
    {
        using (var cache = Open())
        {
            cache.Commit(batch => batch.Upsert(new PlayerSettings()));
            RunSave.Commit(cache, Game(cache), Zones(3, StageZoneContents(cache), 0), new ProvenanceLedger());
        }
        var before = new[] { Catalog, Player }.Select(Hash).ToArray();

        using (var cache = Open())
            RunSave.Clear(cache);

        using (var cache = Open())
        {
            Assert.Equal(0, RunRecordCount(cache));
            Assert.Null(cache.GetGlobal<SavedGame>());
        }
        Assert.Equal(0, RecordsIn(Run));
        Assert.Equal(before, new[] { Catalog, Player }.Select(Hash).ToArray());
    }

    // A new run's start writes its arena's orbits and bodies while the saved run is still stored. A start that fails,
    // by returning nothing or by throwing, takes away only what it wrote; one that starts takes away the saved run.
    [Fact]
    public void ANewRunReplacesTheSavedRunOnlyOnceItHasStarted()
    {
        using (var cache = Open())
            RunSave.Commit(cache, Game(cache), Zones(3, StageZoneContents(cache), 0), new ProvenanceLedger());
        string[] saved;
        using (var cache = Open())
            saved = RunKeys(cache);
        Assert.Equal(8, saved.Length);

        using (var cache = Open())
        {
            Assert.Null(RunSave.Replace<ZonePack>(cache, () =>
            {
                WriteAnOrbitTheSavedRunLacks(cache);
                return null;
            }));
            Assert.Equal(saved, RunKeys(cache));
        }
        using (var cache = Open())
        {
            Assert.Equal(saved, RunKeys(cache));
            Assert.NotNull(cache.GetGlobal<SavedGame>());
        }

        using (var cache = Open())
        {
            Assert.Throws<InvalidOperationException>(() => RunSave.Replace<ZonePack>(cache, () =>
            {
                WriteAnOrbitTheSavedRunLacks(cache);
                throw new InvalidOperationException("the arena would not generate");
            }));
            Assert.Equal(saved, RunKeys(cache));
        }
        using (var cache = Open())
            Assert.Equal(saved, RunKeys(cache));

        ZonePack started;
        using (var cache = Open())
            started = RunSave.Replace(cache, () => StageZoneContents(cache));
        using (var cache = Open())
        {
            Assert.Null(cache.GetGlobal<SavedGame>());
            Assert.Equal(started.Orbits.Select(orbit => orbit.Key.Value).Concat(started.Planets.Select(body => body.Key.Value))
                .OrderBy(key => key, StringComparer.Ordinal), RunKeys(cache));
        }
    }

    private static string[] RunKeys(CultCache cache) => cache.AllStoredDocuments
        .Where(stored => RunSave.IsRunRecord(stored.Descriptor.DocumentType))
        .Select(stored => stored.Key.Value)
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToArray();

    // Lot 1 sits on the root ship's hull; lot 3 (Produced from facility 4, input 5) sits in a docked child's
    // docking-bay contents; lots 2 and 6 are minted but referenced by nothing packed. Only the reachable closure
    // {1, 3, 4, 5} survives into the written copy; the live ledger keeps every lot.
    [Fact]
    public void CommitKeepsOnlyReachableLots()
    {
        using (var cache = Open())
        {
            var lots = new ProvenanceLedger { NextLot = 7 };
            lots.Lots[1] = new Lot { Origin = new Attributed() };
            lots.Lots[2] = new Lot { Origin = new Attributed() };
            lots.Lots[3] = new Lot { Origin = new Produced { Facility = 4, Inputs = new[] { 5 } } };
            lots.Lots[4] = new Lot { Origin = new Attributed() };
            lots.Lots[5] = new Lot { Origin = new Extracted() };
            lots.Lots[6] = new Lot { Origin = new Attributed() };

            var child = BarePack(hull: null, dockingBayContents: new (int2, ItemInstance)[][]
            {
                new (int2, ItemInstance)[] { (default, new CompoundCommodity { Lot = 3 }) }
            });
            var root = BarePack(hull: new EquippableItem { Lot = 1 }, children: new EntityPack[] { child });

            var zones = new[]
            {
                new SavedZone
                {
                    Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                    Contents = new ZonePack { Entities = new List<EntityPack> { root } }
                }
            };
            RunSave.Commit(cache, Game(cache), zones, lots);

            // The live ledger is never pruned.
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, lots.Lots.Keys.OrderBy(k => k));
        }

        using (var cache = Open())
        {
            var stored = RunSave.Lots(cache);
            Assert.Equal(new[] { 1, 3, 4, 5 }, stored.Lots.Keys.OrderBy(k => k));
            Assert.Equal(7, stored.NextLot);
        }
    }

    // F4: RunSave.Commit must take GC roots from every committed zone, not only the first. Two zones, each with a
    // different lot on its hull; both must survive.
    [Fact]
    public void CommitTakesRootsFromEveryZone()
    {
        using (var cache = Open())
        {
            var lots = new ProvenanceLedger { NextLot = 3 };
            lots.Lots[1] = new Lot { Origin = new Attributed() };
            lots.Lots[2] = new Lot { Origin = new Attributed() };

            var zones = new[]
            {
                new SavedZone
                {
                    Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                    Contents = new ZonePack { Entities = new List<EntityPack> { BarePack(hull: new EquippableItem { Lot = 1 }) } }
                },
                new SavedZone
                {
                    Name = "Zone 1", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                    Contents = new ZonePack { Entities = new List<EntityPack> { BarePack(hull: new EquippableItem { Lot = 2 }) } }
                }
            };
            RunSave.Commit(cache, Game(cache), zones, lots);
        }

        using (var cache = Open())
        {
            var stored = RunSave.Lots(cache);
            Assert.Equal(new[] { 1, 2 }, stored.Lots.Keys.OrderBy(k => k));
        }
    }

    [Fact]
    public void MissingLotIsLoud()
    {
        var ledger = new ProvenanceLedger();
        Assert.Throws<InvalidOperationException>(() => ledger[0]);
        Assert.Throws<InvalidOperationException>(() => ledger.Reachable(new[] { 9 }));
    }

    // F1: the reopen defect was found through a hand-built pack; this pins the same defect class at the layer it
    // actually failed, a real ship generated by LoadoutGenerator against a catalog fixture, committed into a
    // SavedZone and reopened (Soul's probe in docs/headless-playground-cut.md §2.6).
    [Fact]
    public void ReopenSurvivesAGeneratedShipWithEntities()
    {
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            var maker = cache.Upsert(new Faction { Name = "Generator Maker", ShortName = "GEN" });
            var hullShape = new Shape(4, 4);
            foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
            var hull = cache.Upsert(new HullData
            {
                Name = "Runabout", HullType = HullType.Ship, Shape = hullShape, Price = 50,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
            });
            var gear = cache.Upsert(new GearData { Name = "Scanner", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 5 });
            var cargo = cache.Upsert(new CargoBayData { Name = "Hold", Shape = new Shape(), InteriorShape = new Shape(), Price = 3 });
            var capacitor = cache.Upsert(new GearData { Name = "Cap", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new CapacitorData() } });
            foreach (var (name, design) in new[] { ("Runabout by Generator Maker", hull.Key), ("Scanner by Generator Maker", gear.Key), ("Hold by Generator Maker", cargo.Key), ("Cap by Generator Maker", capacitor.Key) })
                cache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = maker });
            cache.FlushAsync().Wait();
        }

        EntityPack pack;
        ProvenanceLedger lots;
        using (var cache = Open())
        {
            var random = new Random(7);
            var items = new ItemManager(cache, new ProvenanceLedger(), TestSettings(), _ => { });
            var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
            pack = generator.GenerateShipLoadout(candidate => candidate.Name == "Runabout");
            Assert.NotNull(pack);
            lots = items.Lots;

            var game = new SavedGame
            {
                Factions = Array.Empty<CultRecordRef<Faction>>(),
                Relationships = Array.Empty<FactionRelationship>(),
                HomeZones = new Dictionary<int, int>(),
                BossZones = new Dictionary<int, int>(),
                DiscoveredZones = new[] { 0 },
                ActionBarBindings = new SavedActionBarBinding[0],
                Exit = -1
            };
            var zones = new[]
            {
                new SavedZone
                {
                    Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                    Contents = new ZonePack { Entities = new List<EntityPack> { pack } }
                }
            };
            RunSave.Commit(cache, game, zones, lots);
        }

        using (var cache = Open())
        {
            var storedLots = RunSave.Lots(cache);
            var run = cache.GetGlobal<SavedGame>();
            var zone = cache.Get(run.Zones[0]);
            Assert.NotNull(zone.Contents);
            var crafted = EntitySerializer.Items(zone.Contents.Entities[0]).OfType<CraftedItemInstance>().ToArray();
            Assert.NotEmpty(crafted);
            foreach (var instance in crafted)
            {
                var lot = storedLots[instance.Lot];
                Assert.Equal(instance.Data.Key, lot.Design.Key);
            }
        }
    }

    // A lot's product is what names its units, so it must come back from the run file as the same product key; a lot
    // minted without one reads unset.
    [Fact]
    public void ALotsProductSurvivesTheRunSave()
    {
        CultRecordRef<FactionProductData> product;
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            var lamp = new CultRecordRef<CraftedItemData>(cache.Upsert(new GearData { Name = "Lamp" }).Key);
            var maker = cache.RefOf(cache.GetAll<Faction>().Single());
            product = new CultRecordRef<FactionProductData>(cache.Upsert(
                new FactionProductData { Name = "Lamp Prime", Design = lamp, Manufacturer = maker }).Key);
            cache.FlushAsync().Wait();
        }
        using (var cache = Open())
        {
            var design = cache.RefOf<ItemData>(cache.GetByName<GearData>("Lamp"));
            var lots = new ProvenanceLedger { NextLot = 3 };
            lots.Lots[1] = new Lot { Design = design, Origin = new Attributed(), Product = product };
            lots.Lots[2] = new Lot { Design = design, Origin = new Attributed() };
            var zone = new SavedZone
            {
                Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                Contents = new ZonePack
                {
                    Entities = new List<EntityPack>
                    {
                        BarePack(hull: new EquippableItem { Data = design, Lot = 1 }),
                        BarePack(hull: new EquippableItem { Data = design, Lot = 2 })
                    }
                }
            };
            RunSave.Commit(cache, Game(cache), new[] { zone }, lots);
        }
        using (var cache = Open())
        {
            var stored = RunSave.Lots(cache);
            Assert.Equal(product.Key, stored[1].Product.Key);
            Assert.False(stored[2].Product.IsSet());
        }
    }

    // MQ4: Continue refuses a run that names a design the catalog no longer holds, naming the missing mod ships, and
    // never edits the save. A design can be named by an item in a zone or only by a minted lot, so each is covered.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RunReferencingAMissingModRefusesContinue(bool itemNamesIt, bool lotNamesIt)
    {
        var modHull = new CultRecordKey("mod-hull:mod.skiff");
        var missing = new CultRecordRef<ItemData>(modHull);
        CultRecordRef<ItemData> present;
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            cache.UpsertAsync(typeof(HullData), new HullData { Name = "Skiff" }, modHull).GetAwaiter().GetResult();
            present = new CultRecordRef<ItemData>(cache.Upsert(new HullData { Name = "Wasp" }).Key);
            cache.FlushAsync().Wait();
        }
        var itemDesign = itemNamesIt ? missing : present;
        var lotDesign = lotNamesIt ? missing : present;

        void CommitRun()
        {
            using var cache = Open();
            var lots = new ProvenanceLedger { NextLot = 3 };
            lots.Lots[1] = new Lot { Design = lotDesign, Origin = new Attributed() };
            // A lot minted before designs were recorded has none; an unset design is not a missing one.
            lots.Lots[2] = new Lot { Origin = new Attributed() };
            var zone = new SavedZone
            {
                Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                Contents = new ZonePack
                {
                    Entities = new List<EntityPack>
                    {
                        BarePack(hull: new EquippableItem { Data = itemDesign, Lot = 1 }),
                        BarePack(hull: new EquippableItem { Lot = 2 })
                    }
                }
            };
            RunSave.Commit(cache, Game(cache), new[] { zone }, lots);
        }
        CommitRun();

        // Installed: nothing is missing.
        using (var cache = Open()) RunSave.RequireDesigns(cache, cache.GetGlobal<SavedGame>());
        var before = File.ReadAllBytes(Run);

        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
            cache.Commit(batch => batch.Remove(modHull));
        using (var cache = Open())
        {
            var refusal = Assert.Throws<InvalidOperationException>(() => RunSave.RequireDesigns(cache, cache.GetGlobal<SavedGame>()));
            Assert.Contains("missing mod ships: mod.skiff", refusal.Message);
            Assert.DoesNotContain("other designs", refusal.Message);
        }
        Assert.Equal(before, File.ReadAllBytes(Run));
    }

    // --- RequireDesigns: what it walks, what it lists ---------------------------------------------------------------

    // Commits a run whose zones the caller builds, then returns RequireDesigns' refusal message, or null when it passes.
    // `present` is a gear item ("Lamp", not a hull) the catalog holds; unit(design, lot) mints an item naming `design`
    // whose lot names `lot`.
    private string DesignsRefused(Func<CultRecordRef<ItemData>, Func<CultRecordRef<ItemData>, CultRecordRef<ItemData>, EquippableItem>, SavedZone[]> build)
    {
        CultRecordRef<ItemData> present;
        using (var setup = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            var existing = setup.GetAll<GearData>().SingleOrDefault(gear => gear.Name == "Lamp");
            var lamp = existing != null ? setup.RefOf(existing) :
                setup.Upsert(new GearData { Name = "Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape() });
            present = new CultRecordRef<ItemData>(lamp.Key);
            setup.FlushAsync().Wait();
        }
        using var cache = Open();
        var ledger = new ProvenanceLedger();
        EquippableItem Unit(CultRecordRef<ItemData> design, CultRecordRef<ItemData> lot)
        {
            var number = ledger.NextLot++;
            ledger.Lots[number] = new Lot { Design = lot, Origin = new Attributed() };
            return new EquippableItem { Data = design, Lot = number };
        }
        ledger.NextLot = 1;
        RunSave.Commit(cache, Game(cache), build(present, Unit), ledger);
        try { RunSave.RequireDesigns(cache, cache.GetGlobal<SavedGame>()); return null; }
        catch (InvalidOperationException refusal) { return refusal.Message; }
    }

    private static CultRecordRef<ItemData> Key(string key) => new CultRecordRef<ItemData>(new CultRecordKey(key));

    private static SavedZone ZoneOf(params EntityPack[] packs) => new SavedZone
    {
        Name = "Zone", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
        Contents = new ZonePack { Entities = packs.ToList() }
    };

    // Whatever the slot, a design missing from the catalog is found, and a design the catalog holds (a gear item, not
    // only a hull) is not: the gate walks every unit an entity carries, recursively, and resolves any item design.
    [Theory]
    [InlineData("hull")]
    [InlineData("equipment")]
    [InlineData("cargo bay")]
    [InlineData("docking bay")]
    [InlineData("cargo contents")]
    [InlineData("docking contents")]
    [InlineData("child hull")]
    [InlineData("child equipment")]
    public void RequireDesignsFindsAMissingDesignInEverySlotOfAnEntity(string slot)
    {
        string Refusal(bool missing) => DesignsRefused((present, unit) =>
        {
            EquippableItem Slot(string where) => unit(missing && where == slot ? Key("hull:gone") : present, present);
            var child = BarePack(Slot("child hull"));
            child.Equipment = new[] { (new int2(0, 0), Slot("child equipment")) };
            var pack = BarePack(Slot("hull"), new EntityPack[] { child });
            pack.Equipment = new[] { (new int2(0, 0), Slot("equipment")) };
            pack.CargoBays = new[] { (new int2(0, 0), Slot("cargo bay")) };
            pack.DockingBays = new[] { (new int2(0, 0), Slot("docking bay")) };
            pack.CargoContents = new[] { new (int2, ItemInstance)[] { (new int2(0, 0), Slot("cargo contents")) } };
            pack.DockingBayContents = new[] { new (int2, ItemInstance)[] { (new int2(0, 0), Slot("docking contents")) } };
            return new[] { ZoneOf(pack) };
        });

        Assert.Null(Refusal(missing: false));
        Assert.Contains("missing other designs: hull:gone. Reinstall", Refusal(missing: true));
    }

    // Every zone and every lot is read, not the first: each names its own missing design.
    [Fact]
    public void RequireDesignsReadsEveryZoneAndEveryLot()
    {
        var refusal = DesignsRefused((present, unit) => new[]
        {
            ZoneOf(BarePack(unit(Key("hull:zone-a"), present))),
            ZoneOf(BarePack(unit(present, present))),
            ZoneOf(BarePack(unit(Key("hull:zone-b"), present)), BarePack(unit(present, Key("hull:lot-a")))),
            ZoneOf(BarePack(unit(present, present)), BarePack(unit(present, Key("hull:lot-b"))))
        });

        Assert.Contains("missing other designs: hull:lot-a, hull:lot-b, hull:zone-a, hull:zone-b. Reinstall", refusal);
    }

    // Each missing design is listed once however many units name it, ordinally, mod ships apart from the rest.
    [Fact]
    public void RequireDesignsListsEachMissingDesignOnceInStableOrder()
    {
        var refusal = DesignsRefused((present, unit) => new[]
        {
            ZoneOf(BarePack(unit(Key("hull:b"), Key("mod-hull:z"))), BarePack(unit(Key("mod-hull:z"), Key("hull:b")))),
            ZoneOf(BarePack(unit(Key("hull:B"), Key("mod-hull:m"))), BarePack(unit(Key("hull:a"), Key("hull:b"))))
        });

        Assert.Equal("This run names designs the catalog no longer holds; missing mod ships: m, z; " +
            "missing other designs: hull:B, hull:a, hull:b. Reinstall them, or start a new game.", refusal);
    }

    // A save can hold a null where the gate needs a value: refused with a message that says where, never a
    // NullReferenceException. The records are written straight to the run store, since a save never writes them itself.
    [Theory]
    [InlineData("item", "an item in savedzone-0 is null")]
    [InlineData("entity", "an entity is null")]
    [InlineData("zone", "zone savedzone-1 has no record")]
    [InlineData("lot", "lot 2 is null")]
    [InlineData("lots", "the lot ledger has no lots")]
    public void RequireDesignsRefusesANullWhereTheWalkNeedsAValue(string kind, string where)
    {
        using var cache = Open();
        var pack = BarePack(new EquippableItem { Data = Key("hull:none"), Lot = 1 });
        if (kind == "item") pack.Equipment = new[] { (new int2(0, 0), (EquippableItem) null) };
        if (kind == "entity") pack.Children = new EntityPack[] { null };
        var ledger = new ProvenanceLedger { NextLot = 3, Lots = { [1] = new Lot { Origin = new Attributed() } } };
        if (kind == "lot") ledger.Lots[2] = null;
        if (kind == "lots") ledger.Lots = null;
        var game = Game(cache);
        game.Zones = new[] { new CultRecordRef<SavedZone>(new CultRecordKey("savedzone-0")), new CultRecordRef<SavedZone>(new CultRecordKey("savedzone-1")) };
        cache.Commit(batch =>
        {
            batch.Upsert(typeof(SavedZone), ZoneOf(pack), new CultRecordKey("savedzone-0"));
            if (kind != "zone") batch.Upsert(typeof(SavedZone), ZoneOf(), new CultRecordKey("savedzone-1"));
            batch.Upsert(game);
            batch.Upsert(ledger);
        });

        var refusal = Assert.Throws<InvalidOperationException>(() => RunSave.RequireDesigns(cache, cache.GetGlobal<SavedGame>()));
        Assert.Contains("save is malformed: " + where, refusal.Message);
    }

    // The same run with nothing null is not malformed: null contents (a zone never visited) and unset designs are legitimate.
    [Fact]
    public void RequireDesignsAcceptsAZoneNeverVisitedAndAnUnsetDesign()
    {
        Assert.Null(DesignsRefused((present, unit) => new[]
        {
            new SavedZone { Name = "Never", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1 },
            ZoneOf(BarePack(unit(default, default)))
        }));
    }

    [Fact]
    public void ARunNamingAMissingShippedDesignIsRefusedToo()
    {
        var gone = new CultRecordRef<ItemData>(new CultRecordKey("hull:gone"));
        using var cache = Open();
        var zone = new SavedZone
        {
            Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
            Contents = new ZonePack { Entities = new List<EntityPack> { BarePack(hull: new EquippableItem { Data = gone, Lot = 1 }) } }
        };
        RunSave.Commit(cache, Game(cache), new[] { zone }, new ProvenanceLedger { NextLot = 2, Lots = { [1] = new Lot { Origin = new Attributed() } } });

        var refusal = Assert.Throws<InvalidOperationException>(() => RunSave.RequireDesigns(cache, cache.GetGlobal<SavedGame>()));
        Assert.Contains("missing other designs: hull:gone", refusal.Message);
        Assert.DoesNotContain("mod ships", refusal.Message);
    }

    // A minimal, valid EntityPack with every collection field non-null, for tests that build packs directly rather
    // than through EntitySerializer.Pack.
    private static ShipPack BarePack(EquippableItem hull, EntityPack[] children = null,
        (int2, ItemInstance)[][] dockingBayContents = null) => new ShipPack
    {
        Name = "ship",
        Hull = hull,
        Equipment = Array.Empty<(int2, EquippableItem)>(),
        CargoBays = Array.Empty<(int2, EquippableItem)>(),
        DockingBays = Array.Empty<(int2, EquippableItem)>(),
        CargoContents = Array.Empty<(int2, ItemInstance)[]>(),
        DockingBayContents = dockingBayContents ?? Array.Empty<(int2, ItemInstance)[]>(),
        Children = children ?? Array.Empty<EntityPack>(),
        Temperature = new float[0, 0],
        Armor = new float[0, 0],
        Conductivity = new bool2[0, 0],
        DockingBayAssignments = Array.Empty<int>(),
        Settings = new EntitySettings(),
        WeaponGroups = Array.Empty<int[]>()
    };

    // Zone seeds hash names with this; string.GetHashCode differs per process on .NET Core. The literal was computed
    // once, from an independent transcription of CultMath's pcg over the UTF-8 bytes.
    [Fact]
    public void StableHashIsNotProcessRandomized() => Assert.Equal(2427892944u, "Adrasteia".StableHash());

    // As in AetherDb's loadout command: the settings an ItemManager needs to build units, and nothing authored.
    internal static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    // What a start that fails leaves behind: a record of its own, which the saved run does not hold.
    private static void WriteAnOrbitTheSavedRunLacks(CultCache cache) => cache.Upsert(new OrbitData { Distance = 99 });

    // Two orbits and a body upserted into the run store and left staged, as zone generation leaves them.
    private static ZonePack StageZoneContents(CultCache cache)
    {
        var root = cache.Upsert(new OrbitData());
        var moon = cache.Upsert(new OrbitData { Parent = root, Distance = 5 });
        var body = cache.Upsert<BodyData>(new PlanetData { Name = "Rock", Orbit = moon });
        return new ZonePack { Orbits = { root, moon }, Planets = { body } };
    }

    private static SavedGame Game(CultCache cache) => new SavedGame
    {
        Factions = new[] { cache.RefOf(cache.GetAll<Faction>().Single()) },
        Relationships = new[] { FactionRelationship.Neutral },
        HomeZones = new Dictionary<int, int> { { 0, 0 } },
        BossZones = new Dictionary<int, int>(),
        DiscoveredZones = new[] { 0 },
        ActionBarBindings = new SavedActionBarBinding[0],
        Exit = -1
    };

    // Fresh instances every call; zone contentsIndex holds the given contents, every other zone was never visited.
    private static SavedZone[] Zones(int count, ZonePack contents, int contentsIndex) => Enumerable.Range(0, count)
        .Select(i => new SavedZone
        {
            Name = $"Zone {i}",
            Position = new float2(i * 10, 0),
            AdjacentZones = Enumerable.Range(0, count).Where(j => j != i).ToArray(),
            Factions = new[] { 0 },
            Owner = 0,
            Contents = i == contentsIndex ? contents : null
        })
        .ToArray();

    private static int RunRecordCount(CultCache cache) =>
        cache.AllStoredDocuments.Count(stored => RunSave.IsRunRecord(stored.Descriptor.DocumentType));

    private static string[] SavedZoneKeys(CultCache cache) => cache.AllStoredDocuments
        .Where(stored => stored.Document is SavedZone)
        .Select(stored => stored.Key.Value)
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToArray();

    private static int RecordsIn(string path) =>
        CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path)).Records.Count();

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
