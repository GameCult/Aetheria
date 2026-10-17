/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameCult.Caching;
using Xunit;
using Random = CultMath.Random;

// Each faction names the zones it owns from its own curated list (aetheria-release, cut faction-curated-place-names): the
// home zone takes the capital, the rest follow in distance order, a spent or absent list falls back to catalogue designations,
// and no name is used twice. Galaxies are built the way the game builds them, from the authored settings and the shipped catalog.
public sealed class PlaceNamesTests : IDisposable
{
    private static readonly Regex Designation = new(@"^EAC-\d{1,4}$");
    private static readonly AuthoredSettings Authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
    private static readonly CultCache Shared = RestoredHullsTests.OpenCatalog();
    private static readonly Dictionary<(bool, uint), Galaxy> Built = new();

    private readonly List<CultCache> _own = new();

    public void Dispose()
    {
        foreach (var cache in _own) cache.Dispose();
        foreach (var root in _scratch) Directory.Delete(root, true);
    }

    private static Galaxy Build(CultCache cache, bool prelude, uint seed) => prelude
        ? new Galaxy(Authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"), Authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
            cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed)
        : new Galaxy(Authored.Read<SectorGenerationSettings>("SectorGenerationSettings"), Authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"),
            cache, _ => { }, null, seed);

    // A prelude galaxy sometimes refuses to generate for a seed (RestoredHullsTests calls it Galaxy's own seed-dependent
    // flakiness); those seeds are skipped, and a test that depends on how many built says so.
    private static Galaxy TryBuild(bool prelude, uint seed)
    {
        lock (Built)
        {
            if (Built.TryGetValue((prelude, seed), out var known)) return known;
            Galaxy galaxy;
            try { galaxy = Build(Shared, prelude, seed); }
            catch (Exception) { galaxy = null; }
            return Built[(prelude, seed)] = galaxy;
        }
    }

    private static IEnumerable<Galaxy> Galaxies(bool prelude, uint last) =>
        Enumerable.Range(1, (int)last).Select(seed => TryBuild(prelude, (uint)seed)).Where(g => g != null);

    private static Faction[] Placed(Galaxy galaxy) =>
        galaxy.Factions.Where(f => galaxy.HomeZones.ContainsKey(f) && f.Places.Length != 0).ToArray();

    private static GalaxyZone[] Owned(Galaxy galaxy, Faction faction) =>
        galaxy.Zones.Select((zone, index) => (zone, index)).Where(z => z.zone.Owner == faction)
            .OrderBy(z => z.zone.Distance[galaxy.HomeZones[faction]]).ThenBy(z => z.index).Select(z => z.zone).ToArray();

    // Two factions can share a home zone (the prelude puts the quest faction on the buffer's); the zone's owner is the
    // nearest home in faction order, and that owner's capital is what the zone is called.
    [Fact]
    public void TheHomeZoneTakesTheOwnersFirstName()
    {
        var own = 0;
        var shared = 0;
        foreach (var galaxy in Galaxies(true, 10).Concat(Galaxies(false, 10)))
            foreach (var faction in galaxy.Factions.Where(galaxy.HomeZones.ContainsKey))
            {
                var home = galaxy.HomeZones[faction];
                var owner = home.Owner;
                Assert.NotNull(owner);
                if (owner.Places.Length == 0) Assert.Matches(Designation, home.Name);
                else Assert.Equal(owner.Places[0].Name, home.Name);
                if (owner == faction) own++; else shared++;
            }
        Assert.True(own >= 20, $"only {own} factions owned their home");
        Assert.True(shared > 0, "no galaxy shared a home zone, so the shared case was not exercised");
    }

    [Fact]
    public void OwnedZonesTakeTheListInDistanceOrder()
    {
        var checkedFactions = 0;
        foreach (var galaxy in Galaxies(true, 10).Concat(Galaxies(false, 10)))
            foreach (var faction in Placed(galaxy))
            {
                var owned = Owned(galaxy, faction);
                var listed = Math.Min(owned.Length, faction.Places.Length);
                Assert.Equal(faction.Places.Take(listed).Select(p => p.Name), owned.Take(listed).Select(z => z.Name));
                Assert.All(owned.Skip(listed), zone => Assert.Matches(Designation, zone.Name));
                checkedFactions++;
            }
        Assert.True(checkedFactions >= 20, $"only {checkedFactions} factions were checked");
    }

    [Fact]
    public void NoZoneCarriesAnotherFactionsName()
    {
        var curated = Shared.GetAll<Faction>().SelectMany(f => f.Places.Select(p => (p.Name, Faction: f))).ToDictionary(t => t.Name, t => t.Faction, StringComparer.OrdinalIgnoreCase);
        var zones = 0;
        foreach (var galaxy in Galaxies(true, 10).Concat(Galaxies(false, 10)))
            foreach (var zone in galaxy.Zones.Where(z => curated.ContainsKey(z.Name)))
            {
                Assert.Same(curated[zone.Name], zone.Owner);
                zones++;
            }
        Assert.True(zones >= 50, $"only {zones} curated zones were checked");
    }

    [Fact]
    public void ASpentListFallsBackToCatalogueDesignations()
    {
        // Its own cache: the faction's list is cut, which must not reach the galaxies the other tests share.
        var cache = RestoredHullsTests.OpenCatalog();
        _own.Add(cache);
        var galaxy = Enumerable.Range(1, 20).Select(seed => { try { return Build(cache, true, (uint)seed); } catch (Exception) { return null; } })
            .First(g => g != null && Placed(g).Any(f => Owned(g, f).Length >= 4));
        var faction = Placed(galaxy).First(f => Owned(galaxy, f).Length >= 4);
        faction.Places = faction.Places.Take(2).ToArray();

        var random = new Random(7);
        galaxy.NamePlaces(ref random);

        var owned = Owned(galaxy, faction);
        Assert.Equal(faction.Places.Select(p => p.Name), owned.Take(2).Select(z => z.Name));
        Assert.Same(owned[0], galaxy.HomeZones[faction]);
        Assert.All(owned.Skip(2), zone => Assert.Matches(Designation, zone.Name));
    }

    // The table refuses a repeated name, but a list that did not come through it (a package's factions) can still hold
    // one: the later faction skips a name the earlier one already used, whatever its case.
    [Fact]
    public void AnotherFactionsNameIsNotReusedWhateverItsCase()
    {
        var cache = RestoredHullsTests.OpenCatalog();
        _own.Add(cache);
        var galaxy = Enumerable.Range(1, 20).Select(seed => { try { return Build(cache, true, (uint)seed); } catch (Exception) { return null; } })
            .First(g => g != null && Placed(g).Count(f => Owned(g, f).Length >= 2) >= 2);
        var pair = Placed(galaxy).Where(f => Owned(galaxy, f).Length >= 2).Take(2).ToArray();
        var (first, later) = (pair[0], pair[1]);
        Assert.True(Array.IndexOf(galaxy.Factions, first) < Array.IndexOf(galaxy.Factions, later));
        var taken = first.Places[0].Name;
        var shouting = taken.ToUpperInvariant();
        Assert.NotEqual(taken, shouting);
        var own = later.Places[0].Name;
        later.Places = new[] { new FactionPlace { Name = shouting }, new FactionPlace { Name = own } };

        var random = new Random(7);
        galaxy.NamePlaces(ref random);

        Assert.Equal(taken, Owned(galaxy, first)[0].Name);
        Assert.Equal(own, Owned(galaxy, later)[0].Name);
        var names = galaxy.Zones.Select(z => z.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void NoTwoZonesShareAName()
    {
        var checkedGalaxies = 0;
        foreach (var galaxy in Galaxies(true, 40).Concat(Galaxies(false, 40)))
        {
            var names = galaxy.Zones.Select(z => z.Name).ToArray();
            Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
            Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            checkedGalaxies++;
        }
        Assert.True(checkedGalaxies >= 40, $"only {checkedGalaxies} galaxies were checked");
    }

    [Fact]
    public void AFactionWithoutAListGetsCatalogueDesignations()
    {
        // Its own cache: the list is cleared, which must not reach the galaxies the other tests share.
        var cache = RestoredHullsTests.OpenCatalog();
        _own.Add(cache);
        var terris = cache.GetAll<Faction>().Single(f => f.ShortName == "Miss Terri's");
        Assert.NotEmpty(terris.Places);
        var galaxy = Enumerable.Range(1, 20).Select(seed => { try { return Build(cache, true, (uint)seed); } catch (Exception) { return null; } })
            .First(g => g != null && g.Zones.Any(z => z.Owner == terris));
        terris.Places = Array.Empty<FactionPlace>();

        var random = new Random(7);
        galaxy.NamePlaces(ref random);

        var held = galaxy.Zones.Where(z => z.Owner == terris).ToArray();
        Assert.NotEmpty(held);
        Assert.All(held, zone => Assert.Matches(Designation, zone.Name));
    }

    [Fact]
    public void TheShippedListsAreTheTable()
    {
        var factions = Shared.GetAll<Faction>().ToArray();
        foreach (var row in PlaceNamesCommand.Table)
        {
            var faction = Assert.Single(factions, f => Shared.RefOf(f).Key.Value == row.RecordKey);
            Assert.Equal(row.ShortName, faction.ShortName);
            Assert.Equal(row.Names, faction.Places.Select(p => p.Name));
        }
        Assert.Equal(PlaceNamesCommand.Table.Length, factions.Count(f => f.Places.Length != 0));

        var names = factions.SelectMany(f => f.Places.Select(p => p.Name)).ToArray();
        Assert.All(names, name =>
        {
            Assert.False(string.IsNullOrEmpty(name));
            Assert.Equal(name.Trim(), name);
            Assert.False(name.StartsWith("EAC-", StringComparison.OrdinalIgnoreCase));
        });
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // The command's scratch catalog: the shipped one with every list cleared, so an apply has all of it to author.
    private string ScratchWithoutLists()
    {
        var root = Path.Combine(Path.GetTempPath(), "place-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "GameData"));
        var path = Path.Combine(root, "GameData", "Aetheria.cc");
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        using (var cache = new CultCache(TestCatalog.Registry()))
        {
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(path), AetheriaStores.CatalogTypes);
            foreach (var faction in cache.GetAll<Faction>().ToArray())
            {
                faction.Places = Array.Empty<FactionPlace>();
                cache.Commit(batch => batch.Upsert(typeof(Faction), faction, cache.RefOf(faction).Key));
            }
            cache.FlushAsync().Wait();
        }
        _scratch.Add(root);
        return root;
    }

    private readonly List<string> _scratch = new();
    private static string CatalogOf(string root) => Path.Combine(root, "GameData", "Aetheria.cc");

    [Fact]
    public void TheCommandIsIdempotentAndRefusesAMismatch()
    {
        var root = ScratchWithoutLists();
        var before = File.ReadAllBytes(CatalogOf(root));
        Assert.Equal(0, PlaceNamesCommand.Run(new string[0], root));
        Assert.Equal(before, File.ReadAllBytes(CatalogOf(root)));

        Assert.Equal(0, PlaceNamesCommand.Run(new[] { "apply" }, root));
        var landed = File.ReadAllBytes(CatalogOf(root));
        Assert.NotEqual(before, landed);
        var db = AetherDb.Open(root: root);
        try
        {
            foreach (var row in PlaceNamesCommand.Table)
                Assert.Equal(row.Names, db.Cache.GetAll<Faction>().Single(f => db.Cache.RefOf(f).Key.Value == row.RecordKey).Places.Select(p => p.Name));
        }
        finally { db.Cache.Dispose(); }
        Assert.Equal(0, PlaceNamesCommand.Run(new[] { "apply" }, root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogOf(root)));

        // Aya's ShortName changed under its key: identity is the key, so the row no longer describes that faction.
        var squatter = AetherDb.Open(catalogWritable: true, root: root);
        var aya = squatter.Cache.GetAll<Faction>().Single(f => f.ShortName == "Aya");
        aya.ShortName = "Ayx";
        squatter.Cache.Commit(batch => batch.Upsert(typeof(Faction), aya, squatter.Cache.RefOf(aya).Key));
        squatter.Cache.Dispose();
        var changed = File.ReadAllBytes(CatalogOf(root));
        var refusal = Assert.Throws<InvalidOperationException>(() => PlaceNamesCommand.Run(new[] { "apply" }, root));
        Assert.DoesNotContain("Ayx", refusal.Message);
        Assert.Equal(changed, File.ReadAllBytes(CatalogOf(root)));
    }

    [Theory]
    [InlineData("EAC-12")]
    [InlineData(" Padded")]
    [InlineData("")]
    [InlineData("shunya")]
    public void ATableWithABadNameIsRefusedWithNothingWritten(string bad)
    {
        var root = ScratchWithoutLists();
        var before = File.ReadAllBytes(CatalogOf(root));
        var table = PlaceNamesCommand.Table.Select((row, i) => i == 1 ? row with { List = row.List + " | " + bad } : row).ToArray();
        var refusal = Assert.Throws<InvalidOperationException>(() => PlaceNamesCommand.Run(new[] { "apply" }, root, table));
        if (bad.Length != 0) Assert.DoesNotContain(bad, refusal.Message);
        Assert.Equal(before, File.ReadAllBytes(CatalogOf(root)));
    }

    [Fact]
    public void ARowForAKeyTheCatalogDoesNotHoldIsRefusedWithNothingWritten()
    {
        var root = ScratchWithoutLists();
        var before = File.ReadAllBytes(CatalogOf(root));
        var table = PlaceNamesCommand.Table.Append(new PlaceNamesCommand.Row("00000000-0000-0000-0000-000000000000", "Nobody", "Elsewhere")).ToArray();
        Assert.Throws<InvalidOperationException>(() => PlaceNamesCommand.Run(new[] { "apply" }, root, table));
        Assert.Equal(before, File.ReadAllBytes(CatalogOf(root)));
    }
}
