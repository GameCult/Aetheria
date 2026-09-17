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
            batch.Upsert(new TestCatalogGlobal { Name = "Temperament" });
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
            var (game, zones) = RunSave.Capture(cache, galaxy, galaxy.Zones[0].Contents, null, false, new SavedActionBarBinding[0]);
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

    [Fact]
    public void MissingLotIsLoud()
    {
        var ledger = new ProvenanceLedger();
        Assert.Throws<InvalidOperationException>(() => ledger[0]);
        Assert.Throws<InvalidOperationException>(() => ledger.Reachable(new[] { 9 }));
    }

    // F1: PersistedBehaviors is now a pairs array (Dictionary<int2,...> has no hash-resistant comparer under
    // CultCache's untrusted-data security, so a store holding any entity could never be reopened). Two positions,
    // each with one persisted behavior, must round-trip through a commit and reopen.
    [Fact]
    public void ReopenedPackKeepsPersistedBehaviors()
    {
        var posA = new int2(0, 0);
        var posB = new int2(1, 0);
        var pack = BarePack(hull: null, persistedBehaviors: new (int2, PersistentBehaviorData[])[]
        {
            (posA, new PersistentBehaviorData[] { new MarkerPersistentBehaviorData { Value = 11 } }),
            (posB, new PersistentBehaviorData[] { new MarkerPersistentBehaviorData { Value = 22 } })
        });

        using (var cache = Open())
        {
            var zones = new[]
            {
                new SavedZone
                {
                    Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                    Contents = new ZonePack { Entities = new List<EntityPack> { pack } }
                }
            };
            RunSave.Commit(cache, Game(cache), zones, new ProvenanceLedger());
        }

        using (var cache = Open())
        {
            var run = cache.GetGlobal<SavedGame>();
            var zone = cache.Get(run.Zones[0]);
            var reopened = zone.Contents.Entities[0].PersistedBehaviors.OrderBy(p => p.position.x).ToArray();
            Assert.Equal(2, reopened.Length);
            Assert.Equal(posA, reopened[0].position);
            Assert.Equal(11, Assert.IsType<MarkerPersistentBehaviorData>(reopened[0].data.Single()).Value);
            Assert.Equal(posB, reopened[1].position);
            Assert.Equal(22, Assert.IsType<MarkerPersistentBehaviorData>(reopened[1].data.Single()).Value);
        }
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

    // A minimal, valid EntityPack with every collection field non-null, for tests that build packs directly rather
    // than through EntitySerializer.Pack.
    private static ShipPack BarePack(EquippableItem hull, EntityPack[] children = null,
        (int2, ItemInstance)[][] dockingBayContents = null,
        (int2, PersistentBehaviorData[])[] persistedBehaviors = null) => new ShipPack
    {
        Name = "ship",
        Hull = hull,
        Equipment = Array.Empty<(int2, EquippableItem)>(),
        CargoBays = Array.Empty<(int2, EquippableItem)>(),
        DockingBays = Array.Empty<(int2, EquippableItem)>(),
        CargoContents = Array.Empty<(int2, ItemInstance)[]>(),
        DockingBayContents = dockingBayContents ?? Array.Empty<(int2, ItemInstance)[]>(),
        Children = children ?? Array.Empty<EntityPack>(),
        PersistedBehaviors = persistedBehaviors ?? Array.Empty<(int2, PersistentBehaviorData[])>(),
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
