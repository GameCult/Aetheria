using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

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
                RunSave.Commit(cache, Game(cache), Zones(3, contents, 0));
            }

            using (var cache = Open())
            {
                Assert.Equal(7, RunRecordCount(cache));
                Assert.Equal(7, RecordsIn(Run));
                Assert.Equal(new[] { "savedzone-0", "savedzone-1", "savedzone-2" }, SavedZoneKeys(cache));
            }
        }
    }

    [Fact]
    public void SaveRemovesZonesNoLongerInTheRun()
    {
        using (var cache = Open())
        {
            RunSave.Commit(cache, Game(cache), Zones(3, null, -1));
            RunSave.Commit(cache, Game(cache), Zones(2, null, -1));
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
            RunSave.Commit(cache, Game(cache), Zones(3, StageZoneContents(cache), 0));

        Assert.Equal(before, new[] { Catalog, Player }.Select(Hash).ToArray());
        Assert.Equal(7, RecordsIn(Run));
    }

    [Fact]
    public void ResumeThenSaveKeepsUnloadedZoneContents()
    {
        using (var cache = Open())
            RunSave.Commit(cache, Game(cache), Zones(2, StageZoneContents(cache), 1));

        using (var cache = Open())
        {
            var galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });
            var itemManager = new ItemManager(cache, TestSettings(), _ => { });
            galaxy.Zones[0].Contents = new Zone(itemManager, new PlanetSettings(), new ZonePack(), galaxy.Zones[0], galaxy);
            var (game, zones) = RunSave.Capture(cache, galaxy, galaxy.Zones[0].Contents, null, false, new SavedActionBarBinding[0]);
            RunSave.Commit(cache, game, zones);
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
            RunSave.Commit(cache, Game(cache), Zones(3, StageZoneContents(cache), 0));
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
