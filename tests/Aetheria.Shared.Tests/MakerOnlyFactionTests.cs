/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Faction.ManufacturerOnly records ruling minor-power-brands' "no galaxy presence": both generating Galaxy constructors
// refuse to place such a faction (the main draw filters it, the prelude cast rejects it), so it holds no territory and its
// products reach buyers through allegiance alone (rulings gear-without-presence, design-only-weighted-draw).
public sealed class MakerOnlyFactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-makeronly-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly string _repo = RunStartTests.FindRepoRoot();

    public MakerOnlyFactionTests()
    {
        Directory.CreateDirectory(_root);
        var catalog = Path.Combine(_root, "Aetheria.cc");
        File.Copy(TestCatalog.Repo, catalog);
        _cache = new CultCache(TestCatalog.Registry());
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(_root, "run.cc")), AetheriaStores.RunTypes);
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private Galaxy MainGalaxy(int megaCount, uint seed)
    {
        var authored = AuthoredSettings.Load(_repo);
        var sector = authored.Read<SectorGenerationSettings>("SectorGenerationSettings");
        sector.MegaCount = megaCount;
        return new Galaxy(sector, authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"),
            authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), _cache, _ => { }, null, seed);
    }

    private Galaxy PreludeGalaxy(TutorialGenerationSettings settings)
    {
        var authored = AuthoredSettings.Load(_repo);
        return new Galaxy(settings, authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
            authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), _cache, new PlayerSettings(),
            Directory.CreateDirectory(Path.Combine(_root, "Narrative")), _ => { }, null, 1);
    }

    private Faction Maker(string name)
    {
        var maker = new Faction { Name = name, ShortName = name.Replace(" ", ""), ManufacturerOnly = true };
        _cache.Upsert(maker);
        return maker;
    }

    private static void AssertAbsent(Galaxy galaxy, Faction maker)
    {
        Assert.DoesNotContain(maker, galaxy.Factions);
        Assert.False(galaxy.HomeZones.ContainsKey(maker));
        Assert.False(galaxy.BossZones.ContainsKey(maker));
        Assert.False(galaxy.FactionRelationships.ContainsKey(maker));
        foreach (var zone in galaxy.Zones)
        {
            Assert.False(ReferenceEquals(maker, zone.Owner));
            Assert.DoesNotContain(maker, zone.Factions ?? new Faction[0]);
        }
    }

    // Flag each shipped faction in turn, so no position in the catalog's order is exempt from the rule.
    [Fact]
    public void EveryFlaggedFactionIsLeftOut()
    {
        var shipped = _cache.GetAll<Faction>().ToArray();
        Assert.True(shipped.Length > 3);
        for (var i = 0; i < shipped.Length; i++)
        {
            var flagged = _cache.GetAll<Faction>().ElementAt(i);
            flagged.ManufacturerOnly = true;
            try
            {
                var galaxy = MainGalaxy(shipped.Length, 1);
                Assert.Equal(shipped.Where(f => f != flagged).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal),
                    galaxy.Factions.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
                AssertAbsent(galaxy, flagged);
            }
            finally { flagged.ManufacturerOnly = false; }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    [InlineData(40)]
    public void TwoMakersAtSmallAndLargeMegaCount(int megaCount)
    {
        var early = _cache.GetAll<Faction>().First();
        early.ManufacturerOnly = true;
        var late = Maker("Test Maker");
        var eligible = _cache.GetAll<Faction>().Count(f => !f.ManufacturerOnly);
        Assert.True(eligible > 3);

        for (uint seed = 1; seed <= 4; seed++)
        {
            var galaxy = MainGalaxy(megaCount, seed);
            Assert.Equal(Math.Min(megaCount, eligible), galaxy.Factions.Length);
            AssertAbsent(galaxy, early);
            AssertAbsent(galaxy, late);
        }
    }

    // Filtering before the draw consumes no extra randomness, so no shipped seed changes its galaxy. The order was taken from
    // the unfiltered draw at base aa12cbac.
    [Fact]
    public void UnflaggedDrawIsUnchanged()
    {
        var count = _cache.GetAll<Faction>().Count();
        for (uint seed = 1; seed <= 4; seed++)
            Assert.Equal(DrawAtBase[seed - 1].Split('|'), MainGalaxy(count, seed).Factions.Select(f => f.Name));
    }

    private static readonly string[] DrawAtBase =
    {
        "SNAPSHOT1", "SNAPSHOT2", "SNAPSHOT3", "SNAPSHOT4",
    };

    // The wire shape: ManufacturerOnly rides at slot 19, slot 18 is reserved for Livery, and a record written before the slot
    // existed decodes false.
    [Fact]
    public void Key19WirePin()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(Faction).Assembly);
        var slots = MessagePackSerializer.Deserialize<object[]>(MessagePackSerializer.Serialize(
            new Faction { Name = "Maker", ManufacturerOnly = true }, options));

        Assert.Equal(20, slots.Length);
        Assert.Null(slots[18]);
        Assert.Equal(true, slots[19]);

        var old = MessagePackSerializer.Serialize(slots.Take(19).ToArray(), MessagePackSerializerOptions.Standard);
        Assert.False(MessagePackSerializer.Deserialize<Faction>(old, options).ManufacturerOnly);
    }

    [Fact]
    public void APreludeCastRefusesAManufacturerOnlyFaction()
    {
        var authored = AuthoredSettings.Load(_repo);
        TutorialGenerationSettings Settings() => authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings");
        PreludeGalaxy(Settings());

        var cast = new Func<TutorialGenerationSettings, (string Field, string Name)>[]
        {
            s => (nameof(s.ProtagonistFaction), s.ProtagonistFaction),
            s => (nameof(s.AntagonistFaction), s.AntagonistFaction),
            s => (nameof(s.BufferFaction), s.BufferFaction),
            s => (nameof(s.NeutralFactions), s.NeutralFactions[0]),
        };
        foreach (var pick in cast)
        {
            var settings = Settings();
            var (field, name) = pick(settings);
            var member = _cache.GetAll<Faction>().First(f => f.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));
            member.ManufacturerOnly = true;
            try
            {
                var refusal = Assert.Throws<InvalidOperationException>(() => PreludeGalaxy(settings));
                Assert.Contains(field, refusal.Message);
                Assert.Contains("manufacturer-only", refusal.Message);
            }
            finally { member.ManufacturerOnly = false; }
        }
    }

    [Fact]
    public void MakerOnlyGearReachesAlliedBuyers()
    {
        var maker = Maker("Test Maker");
        var seller = _cache.GetAll<FactionProductData>().First();
        var product = new FactionProductData { Name = "Test Product", Design = seller.Design, Manufacturer = _cache.RefOf(maker) };
        _cache.Upsert(product);
        var galaxy = MainGalaxy(_cache.GetAll<Faction>().Count(), 1);
        var ally = galaxy.Factions.First();
        var stranger = galaxy.Factions.Last();
        ally.Allegiance[_cache.RefOf(maker)] = .6f;
        Assert.False(stranger.Allegiance.ContainsKey(_cache.RefOf(maker)));

        var authored = AuthoredSettings.Load(_repo);
        var items = new ItemManager(_cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
        var random = new Random(1);
        var allied = new LoadoutGenerator(ref random, items, galaxy, galaxy.Entrance, ally, .5f);
        var strange = new LoadoutGenerator(ref random, items, galaxy, galaxy.Entrance, stranger, .5f);

        Assert.True(allied.IsAvailable(product));
        Assert.False(strange.IsAvailable(product));
        Assert.Equal(.6f / (2 + galaxy.Entrance.Distance.Values.Max()), allied.ManufacturerPreference(_cache.RefOf(maker)), 5);
    }

    [Fact]
    public void ShippedFactionsArePresent()
    {
        Assert.All(_cache.GetAll<Faction>(), faction => Assert.False(faction.ManufacturerOnly, faction.Name));
        var count = _cache.GetAll<Faction>().Count();
        Assert.Equal(count, MainGalaxy(count, 1).Factions.Length);
    }
}
