/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Ore geography (aetheria-living-world, cut ore-geography): regions cut at the links out of boss zones, provinces grown inside
// them, province types dealt per region from a deck, and the kind draw and ore yields they weight. Galaxies are built the way
// the game builds them, from the authored settings and the shipped catalog.
public sealed class OreGeographyTests : IDisposable
{
    private static readonly AuthoredSettings Authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
    private static readonly CultCache Shared = RestoredHullsTests.OpenCatalog();
    private static readonly Dictionary<(bool, uint), Galaxy> Built = new();

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-oregeography-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new();
    private int _count;

    public OreGeographyTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    // ---- fixtures

    private (string Catalog, string Run) NewPaths()
    {
        var dir = Path.Combine(_root, $"cache{_count++}");
        Directory.CreateDirectory(dir);
        return (Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"));
    }

    // An empty writable catalog.
    private CultCache Empty()
    {
        var (catalog, run) = NewPaths();
        var cache = AetheriaStores.Open(catalog, run, catalogWritable: true);
        _caches.Add(cache);
        return cache;
    }

    private static CultCache OpenCopy(string catalog, string run)
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog, false), AetheriaStores.CatalogTypes);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(run), AetheriaStores.RunTypes);
        return cache;
    }

    // A writable copy of the shipped catalog and a scratch run store.
    private (CultCache Cache, string Catalog, string Run) Shipped()
    {
        var (catalog, run) = NewPaths();
        File.Copy(TestCatalog.Repo, catalog);
        var cache = OpenCopy(catalog, run);
        _caches.Add(cache);
        return (cache, catalog, run);
    }

    private static CultRecordRef<T> Put<T>(CultCache cache, string key, T document) where T : class
    {
        cache.Commit(batch => batch.Upsert(typeof(T), document, new CultRecordKey(key)));
        return new CultRecordRef<T>(new CultRecordKey(key));
    }

    private static CultRecordRef<FieldKindData> Kind(CultCache cache, string name, float weight) =>
        Put(cache, "field-kind:" + name, new FieldKindData { Name = name, CrossSection = 1f, GenerationWeight = weight });

    private static CultRecordRef<ProvinceData> Province(CultCache cache, string name, int cards,
        ProvinceKindWeight[] kinds = null, ProvinceOre[] ores = null) =>
        Put(cache, "province:" + name, new ProvinceData
        {
            Name = name, GenerationWeight = cards,
            Kinds = kinds?.ToList() ?? new List<ProvinceKindWeight>(), Ores = ores?.ToList() ?? new List<ProvinceOre>()
        });

    private static ProvinceKindWeight KindFactor(CultRecordRef<FieldKindData> kind, float weight) => new ProvinceKindWeight {Kind = kind, Weight = weight};
    private static ProvinceOre OreFactor(CultRecordRef<SimpleCommodityData> ore, float weight) => new ProvinceOre {Commodity = ore, Weight = weight};
    private static CultRecordRef<SimpleCommodityData> Ore(string name) => new CultRecordRef<SimpleCommodityData>(new CultRecordKey("ore:" + name));
    private static FieldYield Yield(string ore, float abundance, bool regional = false) => new FieldYield {Commodity = Ore(ore), Abundance = abundance, Regional = regional};

    private static int[][] Graph(int count, params (int, int)[] links)
    {
        var adjacency = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        foreach (var (a, b) in links)
        {
            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }
        return adjacency.Select(list => list.OrderBy(i => i).ToArray()).ToArray();
    }

    private static int[][] Line(int count) => Graph(count, Enumerable.Range(0, count - 1).Select(i => (i, i + 1)).ToArray());

    private static int[][] AdjacencyOf(Galaxy galaxy) =>
        galaxy.Zones.Select(zone => zone.AdjacentZones.Select(next => Array.IndexOf(galaxy.Zones, next)).ToArray()).ToArray();

    private static int[] BossesOf(Galaxy galaxy) => galaxy.BossZones.Values.Select(zone => Array.IndexOf(galaxy.Zones, zone)).ToArray();

    private static int ExitOf(Galaxy galaxy) => galaxy.Exit == null ? -1 : Array.IndexOf(galaxy.Zones, galaxy.Exit);

    private static int[] RegionsOf(Galaxy galaxy) => Provinces.Regions(AdjacencyOf(galaxy), BossesOf(galaxy), ExitOf(galaxy));

    private static bool[] ReachableAvoiding(int[][] adjacency, int from, int avoid)
    {
        var reached = new bool[adjacency.Length];
        var stack = new Stack<int>();
        reached[from] = true;
        stack.Push(from);
        while (stack.Count > 0)
            foreach (var next in adjacency[stack.Pop()])
                if (next != avoid && !reached[next])
                {
                    reached[next] = true;
                    stack.Push(next);
                }
        return reached;
    }

    private static Galaxy Build(CultCache cache, bool prelude, uint seed, TutorialGenerationSettings cast = null) => prelude
        ? new Galaxy(cast ?? Authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"), Authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
            cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed)
        : new Galaxy(Authored.Read<SectorGenerationSettings>("SectorGenerationSettings"), Authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"),
            cache, _ => { }, null, seed);

    // Galaxy refuses some seeds (RestoredHullsTests calls it seed-dependent flakiness); those are skipped.
    private static Galaxy TryBuild(CultCache cache, bool prelude, uint seed, TutorialGenerationSettings cast = null)
    {
        try { return Build(cache, prelude, seed, cast); }
        catch (Exception) { return null; }
    }

    private static Galaxy SharedGalaxy(bool prelude, uint seed)
    {
        lock (Built)
            return Built.TryGetValue((prelude, seed), out var known) ? known : Built[(prelude, seed)] = TryBuild(Shared, prelude, seed);
    }

    private static IEnumerable<Galaxy> Galaxies(bool prelude, uint last) =>
        Enumerable.Range(1, (int) last).Select(seed => SharedGalaxy(prelude, (uint) seed)).Where(galaxy => galaxy != null);

    // The demo's cast: the same fixed cast, with the province size chosen.
    private static TutorialGenerationSettings Cast(int provinceZones)
    {
        var cast = DemoTerminus.Cast;
        return new TutorialGenerationSettings
        {
            ProtagonistFaction = cast.ProtagonistFaction, AntagonistFaction = cast.AntagonistFaction, BufferFaction = cast.BufferFaction,
            NeutralFactions = cast.NeutralFactions, QuestFaction = cast.QuestFaction, ZoneCount = cast.ZoneCount, LinkDensity = cast.LinkDensity,
            ProvinceZones = provinceZones
        };
    }

    private static Galaxy DemoGalaxy(CultCache cache, uint seed, int provinceZones = Provinces.DefaultZonesPerProvince)
    {
        var galaxy = TryBuild(cache, true, seed, Cast(provinceZones));
        galaxy?.PlaceGate(galaxy.ResolveFaction(DemoTerminus.Cast.AntagonistFaction));
        return galaxy;
    }

    private static bool ExitIsACutVertex(Galaxy galaxy)
    {
        var adjacency = AdjacencyOf(galaxy);
        var exit = ExitOf(galaxy);
        var start = Enumerable.Range(0, adjacency.Length).First(zone => zone != exit);
        return ReachableAvoiding(adjacency, start, exit).Count(reached => reached) < adjacency.Length - 1;
    }

    private static string[] Shape(Galaxy galaxy) => galaxy.Provinces
        .Select(province => $"{Array.IndexOf(galaxy.Zones, province.Seed)}:{province.Type.Key.Value}:" +
                            string.Join(",", province.Zones.Select(zone => Array.IndexOf(galaxy.Zones, zone)).OrderBy(i => i)))
        .ToArray();

    // The live run as plain documents, as the game writes it, then Continue: Capture, Commit, and a Galaxy from the committed save.
    private (SavedGame Game, SavedZone[] Zones) Capture(CultCache cache, Galaxy galaxy)
    {
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), Authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
        var zone = new Zone(items, RestoredHullsTests.AuthoredPlanetSettings(), new ZonePack(), galaxy.Entrance, galaxy);
        return RunSave.Capture(cache, galaxy, zone, null, new SavedActionBarBinding[0], 0);
    }

    private static Galaxy Continue(CultCache cache, SavedGame game, SavedZone[] zones)
    {
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        return new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });
    }

    // A sector galaxy built from the authored sector settings with the province size chosen.
    private static Galaxy SectorGalaxy(CultCache cache, uint seed, int provinceZones)
    {
        var authored = Authored.Read<SectorGenerationSettings>("SectorGenerationSettings");
        try
        {
            return new Galaxy(new SectorGenerationSettings
                {
                    LinkDensity = authored.LinkDensity, ZoneCount = authored.ZoneCount, MegaCount = authored.MegaCount,
                    BossCount = authored.BossCount, ProvinceZones = provinceZones
                },
                Authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"), cache, _ => { }, null, seed);
        }
        catch (Exception) { return null; }
    }

    // A sector galaxy whose boss zones cut it into several regions, with the province types dealt to them.
    private static Galaxy MultiRegionSector(CultCache cache, int provinceZones = Provinces.DefaultZonesPerProvince) =>
        Enumerable.Range(1, 40).Select(seed => SectorGalaxy(cache, (uint) seed, provinceZones))
            .First(galaxy => galaxy != null && RegionsOf(galaxy).Distinct().Count() > 1);

    // ---- the kind draw and the yields

    // The pick as it stood before ore geography, spelled out here: one weighted draw by a stable hash of the site.
    private static string BasePick(string site, CultCache catalog)
    {
        var kinds = catalog.GetAll<FieldKindData>()
            .Where(kind => kind.GenerationWeight > 0f)
            .Select(kind => (Key: catalog.RefOf(kind).Key, Weight: kind.GenerationWeight))
            .OrderBy(kind => kind.Key.Value, StringComparer.Ordinal)
            .ToArray();
        if (kinds.Length == 0) return null;
        var pick = site.StableHash() / 4294967296.0 * kinds.Sum(kind => (double) kind.Weight);
        foreach (var kind in kinds)
        {
            if (pick < kind.Weight) return kind.Key.Value;
            pick -= kind.Weight;
        }
        return kinds[kinds.Length - 1].Key.Value;
    }

    [Fact]
    public void NoProvinceTypeIsTodaysPick()
    {
        var cache = Empty();
        Kind(cache, "a", 1f);
        Kind(cache, "b", 2f);
        Kind(cache, "never", 0f);
        Kind(cache, "c", 3.5f);
        var dangling = new CultRecordRef<ProvinceData>(new CultRecordKey("province:missing"));
        var seen = new HashSet<string>();
        for (var i = 0; i < 2000; i++)
        {
            var site = $"belt-{i}";
            var expected = BasePick(site, cache);
            seen.Add(expected);
            Assert.Equal(expected, FieldKinds.Assign(site, cache, default).Key.Value);
            Assert.Equal(expected, FieldKinds.Assign(site, cache, dangling).Key.Value);
        }
        Assert.Equal(3, seen.Count);
        Assert.False(FieldKinds.Assign("belt-0", Empty(), default).IsSet());
    }

    [Fact]
    public void AKindWhoseCombinedWeightIsNotPositiveIsNeverDrawn()
    {
        var cache = Empty();
        var negative = Kind(cache, "negative", -1f);
        Kind(cache, "positive", 1f);
        var flipped = Province(cache, "flipped", 1, new[] {KindFactor(negative, -1f)});
        for (var i = 0; i < 500; i++)
        {
            Assert.Equal("field-kind:positive", FieldKinds.Assign($"belt-{i}", cache, default).Key.Value);
            Assert.Equal("field-kind:positive", FieldKinds.Assign($"belt-{i}", cache, flipped).Key.Value);
        }

        // With nothing positive left the belt has no kind, rather than the negative one.
        var only = Empty();
        var lone = Kind(only, "negative", -1f);
        Assert.False(FieldKinds.Assign("belt-0", only, Province(only, "flipped", 1, new[] {KindFactor(lone, -1f)})).IsSet());
    }

    [Fact]
    public void ProvinceWeightsTheKindDraw()
    {
        var cache = Empty();
        var light = Kind(cache, "light", 1f);
        var middle = Kind(cache, "middle", 2f);
        Kind(cache, "heavy", 3f);
        Kind(cache, "never", 0f);
        var province = Province(cache, "p", 1, new[] {KindFactor(light, 0f), KindFactor(middle, 3f)});
        var counts = new Dictionary<string, int>();
        const int sites = 3000;
        for (var i = 0; i < sites; i++)
        {
            var picked = FieldKinds.Assign($"belt-{i}", cache, province).Key.Value;
            counts[picked] = counts.GetValueOrDefault(picked) + 1;
        }
        // light 1 x 0 = 0, middle 2 x 3 = 6, heavy 3 x 1 (unlisted) = 3
        Assert.Equal(0, counts.GetValueOrDefault("field-kind:light"));
        Assert.Equal(0, counts.GetValueOrDefault("field-kind:never"));
        Assert.InRange(counts["field-kind:middle"] / (float) sites, 2f / 3 - .04f, 2f / 3 + .04f);
        Assert.InRange(counts["field-kind:heavy"] / (float) sites, 1f / 3 - .04f, 1f / 3 + .04f);

        // A province that zeroes every kind leaves the belt without one rather than ignoring it.
        var barren = Province(cache, "barren", 1, new[] {KindFactor(light, 0f), KindFactor(middle, 0f), KindFactor(new CultRecordRef<FieldKindData>(new CultRecordKey("field-kind:heavy")), 0f)});
        Assert.False(FieldKinds.Assign("belt-0", cache, barren).IsSet());
    }

    [Fact]
    public void YieldScalesAbundanceByProvince()
    {
        var cache = Empty();
        var kind = Put(cache, "field-kind:rock", new FieldKindData
        {
            Name = "rock",
            Yields = new List<FieldYield> {Yield("iron", .5f), Yield("copper", 1f), Yield("copper", 1f), Yield("nothing", 0f), Yield("tin", 1f)}
        });
        var province = Province(cache, "p", 1, ores: new[] {OreFactor(Ore("iron"), 4f), OreFactor(Ore("tin"), 0f)});

        var here = FieldKinds.Yield(kind, province, cache);
        // copper's two entries are summed; iron is .5 x 4; tin is scaled to nothing; the zero-abundance ore is dropped; commodity key order
        Assert.Equal(new[] {("ore:copper", 2f), ("ore:iron", 2f)}, here.Select(y => (y.Commodity.Key.Value, y.Amount)).ToArray());

        var nowhere = FieldKinds.Yield(kind, default, cache);
        Assert.Equal(new[] {("ore:copper", 2f), ("ore:iron", .5f), ("ore:tin", 1f)}, nowhere.Select(y => (y.Commodity.Key.Value, y.Amount)).ToArray());

        Assert.Empty(FieldKinds.Yield(default, province, cache));
        Assert.Empty(FieldKinds.Yield(new CultRecordRef<FieldKindData>(new CultRecordKey("field-kind:missing")), province, cache));
    }

    [Fact]
    public void ARegionalOreYieldsOnlyWhereItsProvinceListsIt()
    {
        var cache = Empty();
        var kind = Put(cache, "field-kind:rock", new FieldKindData
        {
            Name = "rock",
            Yields = new List<FieldYield> {Yield("stone", 2f), Yield("iridium", 3f, regional: true)}
        });
        var lists = Province(cache, "lists", 1, ores: new[] {OreFactor(Ore("iridium"), 1f)});
        var silent = Province(cache, "silent", 1);

        string[] Held(CultRecordRef<ProvinceData> province) => FieldKinds.Yield(kind, province, cache).Select(y => y.Commodity.Key.Value).ToArray();
        Assert.Equal(new[] {"ore:iridium", "ore:stone"}, Held(lists));
        Assert.Equal(new[] {"ore:stone"}, Held(silent));
        Assert.Equal(new[] {"ore:stone"}, Held(default));
        Assert.Equal(3f, FieldKinds.Yield(kind, lists, cache).Single(y => y.Commodity.Key.Value == "ore:iridium").Amount);
    }

    // ---- regions

    [Fact]
    public void ARegionIsCutAtTheLinksOutOfABossZone()
    {
        // Two boss cut vertices on a path give three regions; each boss zone stays with the zone before it.
        Assert.Equal(new[] {0, 0, 0, 3, 3, 5, 5}, Provinces.Regions(Line(7), new[] {2, 4}, 6));

        // A boss zone with two links into the far side has both cut, and the far neighbours are one region.
        var diamond = Graph(7, (0, 1), (1, 2), (2, 3), (2, 4), (3, 5), (4, 5), (5, 6));
        Assert.Equal(new[] {0, 0, 0, 3, 3, 3, 3}, Provinces.Regions(diamond, new[] {2}, 6));

        // A branch hanging off a boss zone is not on the exit side, so it stays near.
        var pendant = Graph(6, (0, 1), (1, 2), (2, 3), (3, 4), (2, 5));
        Assert.Equal(new[] {0, 0, 0, 3, 3, 0}, Provinces.Regions(pendant, new[] {2}, 4));

        // Two adjacent boss zones: the link between them is the first one's crossing link, and the second is alone on its near side.
        Assert.Equal(new[] {0, 0, 2, 3, 3}, Provinces.Regions(Line(5), new[] {1, 2}, 4));

        // The exit may be zone 0.
        Assert.Equal(new[] {0, 0, 2, 2, 2}, Provinces.Regions(Line(5), new[] {2}, 0));

        // No boss zone, no exit, or a boss zone that is the exit: nothing is cut.
        Assert.All(Provinces.Regions(Line(7), new int[0], 6), region => Assert.Equal(0, region));
        Assert.All(Provinces.Regions(Line(7), new[] {2, 4}, -1), region => Assert.Equal(0, region));
        Assert.All(Provinces.Regions(Line(7), new[] {6}, 6), region => Assert.Equal(0, region));

        // Every zone is in exactly one region, named by its least zone.
        var regions = Provinces.Regions(diamond, new[] {2}, 6);
        for (var zone = 0; zone < regions.Length; zone++)
        {
            Assert.True(regions[zone] <= zone);
            Assert.Equal(regions[zone], regions[regions[zone]]);
        }
    }

    [Fact]
    public void EveryBossZoneIsInTheRegionOfItsEntranceSide()
    {
        var checkedBosses = 0;
        foreach (var galaxy in Galaxies(false, 8))
        {
            var adjacency = AdjacencyOf(galaxy);
            var exit = ExitOf(galaxy);
            var regionOf = RegionsOf(galaxy);
            var bosses = BossesOf(galaxy).Distinct().Where(boss => boss != exit).ToArray();
            foreach (var boss in bosses)
            {
                var exitSide = ReachableAvoiding(adjacency, exit, boss);
                foreach (var next in adjacency[boss])
                {
                    if (exitSide[next]) Assert.NotEqual(regionOf[boss], regionOf[next]);
                    else Assert.Equal(regionOf[boss], regionOf[next]);
                }
                checkedBosses++;
            }
            Assert.Equal(bosses.Length + 1, regionOf.Distinct().Count());
        }
        Assert.True(checkedBosses >= 4, $"only {checkedBosses} boss zones were exercised");
    }

    // ---- one exit per region

    private const uint OneExitSeeds = 60;

    private static int[][] Without(int[][] adjacency, params (int, int)[] removed) =>
        adjacency.Select((next, zone) => next.Where(other => !removed.Contains((zone, other)) && !removed.Contains((other, zone))).ToArray()).ToArray();

    private static int[] Hops(int[][] adjacency, int from)
    {
        var hops = Enumerable.Repeat(-1, adjacency.Length).ToArray();
        hops[from] = 0;
        var queue = new Queue<int>(new[] {from});
        while (queue.Count > 0)
        {
            var zone = queue.Dequeue();
            foreach (var next in adjacency[zone])
                if (hops[next] < 0)
                {
                    hops[next] = hops[zone] + 1;
                    queue.Enqueue(next);
                }
        }
        return hops;
    }

    private static int[] ExitPathOf(Galaxy galaxy) => galaxy.ExitPath.Select(zone => Array.IndexOf(galaxy.Zones, zone)).ToArray();

    private static int[] OtherBosses(Galaxy galaxy) => BossesOf(galaxy).Distinct().Where(boss => boss != ExitOf(galaxy)).ToArray();

    private static Galaxy[] SectorGalaxies()
    {
        var galaxies = Galaxies(false, OneExitSeeds).ToArray();
        Assert.True(galaxies.Length >= 50, $"only {galaxies.Length} of {OneExitSeeds} sector galaxies were built");
        return galaxies;
    }

    [Fact]
    public void EveryBossZoneHasOneExit()
    {
        var checkedBosses = 0;
        foreach (var galaxy in SectorGalaxies())
        {
            var adjacency = AdjacencyOf(galaxy);
            var exit = ExitOf(galaxy);
            var entrance = Array.IndexOf(galaxy.Zones, galaxy.Entrance);
            var crossing = Provinces.CrossingLinks(adjacency, BossesOf(galaxy), exit);
            foreach (var boss in OtherBosses(galaxy))
            {
                var exits = crossing.Where(link => link.Boss == boss).ToArray();
                Assert.Single(exits);
                Assert.False(ReachableAvoiding(Without(adjacency, (boss, exits[0].Far)), entrance, -1)[exit]);
                checkedBosses++;
            }
            Assert.Empty(Provinces.ExtraExits(adjacency, BossesOf(galaxy), exit, ExitPathOf(galaxy)));
        }
        Assert.True(checkedBosses >= 40, $"only {checkedBosses} boss zones were exercised");
    }

    [Fact]
    public void TheFarSideStaysConnected()
    {
        foreach (var galaxy in SectorGalaxies())
        {
            var adjacency = AdjacencyOf(galaxy);
            var exit = ExitOf(galaxy);
            var entrance = Array.IndexOf(galaxy.Zones, galaxy.Entrance);
            Assert.All(Hops(adjacency, entrance), hops => Assert.True(hops >= 0));
            var path = ExitPathOf(galaxy);
            foreach (var boss in OtherBosses(galaxy))
            {
                // Without the boss zone the graph is exactly two components: the entrance's, and the exit's holding every later boss.
                var near = ReachableAvoiding(adjacency, entrance, boss);
                var far = ReachableAvoiding(adjacency, exit, boss);
                for (var zone = 0; zone < adjacency.Length; zone++)
                    if (zone != boss) Assert.True(near[zone] ^ far[zone], $"zone {zone} is on neither or both sides of boss zone {boss}");
                foreach (var later in OtherBosses(galaxy).Where(other => Array.IndexOf(path, other) > Array.IndexOf(path, boss)))
                    Assert.True(far[later]);
            }
            Assert.Equal(OtherBosses(galaxy).Length + 1, RegionsOf(galaxy).Distinct().Count());
        }
    }

    [Fact]
    public void LinksStaySymmetric()
    {
        foreach (var galaxy in SectorGalaxies())
        {
            var adjacency = AdjacencyOf(galaxy);
            for (var zone = 0; zone < adjacency.Length; zone++)
            {
                Assert.Equal(adjacency[zone].Length, adjacency[zone].Distinct().Count());
                Assert.DoesNotContain(zone, adjacency[zone]);
                foreach (var next in adjacency[zone]) Assert.Contains(zone, adjacency[next]);
            }
        }
    }

    [Fact]
    public void ExtraExitsOfHandBuiltGraphs()
    {
        // A boss zone with two links into the far side lists both, and ExtraExits spares the one on the exit path.
        var diamond = Graph(7, (0, 1), (1, 2), (2, 3), (2, 4), (3, 5), (4, 5), (5, 6));
        Assert.Equal(new[] {(2, 3), (2, 4)}, Provinces.CrossingLinks(diamond, new[] {2}, 6));
        Assert.Equal(new[] {(2, 4)}, Provinces.ExtraExits(diamond, new[] {2}, 6, new[] {0, 1, 2, 3, 5, 6}));
        Assert.Equal(new[] {(2, 3)}, Provinces.ExtraExits(diamond, new[] {2}, 6, new[] {0, 1, 2, 4, 5, 6}));

        // Three links: the two off the path are extra.
        var fan = Graph(8, (0, 1), (1, 2), (2, 3), (2, 4), (2, 5), (3, 6), (4, 6), (5, 6), (6, 7));
        Assert.Equal(new[] {(2, 3), (2, 4), (2, 5)}, Provinces.CrossingLinks(fan, new[] {2}, 7));
        Assert.Equal(new[] {(2, 3), (2, 5)}, Provinces.ExtraExits(fan, new[] {2}, 7, new[] {0, 1, 2, 4, 6, 7}));

        // A branch hanging off a boss zone and the link back to the near side are never crossing links.
        var pendant = Graph(6, (0, 1), (1, 2), (2, 3), (3, 4), (2, 5));
        Assert.Equal(new[] {(2, 3)}, Provinces.CrossingLinks(pendant, new[] {2}, 4));
        Assert.Empty(Provinces.ExtraExits(pendant, new[] {2}, 4, new[] {0, 1, 2, 3, 4}));

        // A boss zone that is the exit, or no exit at all, lists nothing; neither does no boss zone. A repeated boss zone counts once.
        Assert.Empty(Provinces.CrossingLinks(diamond, new[] {6}, 6));
        Assert.Empty(Provinces.CrossingLinks(diamond, new[] {2}, -1));
        Assert.Empty(Provinces.ExtraExits(diamond, new[] {2}, -1, new[] {0, 1, 2, 3, 5, 6}));
        Assert.Empty(Provinces.CrossingLinks(diamond, new int[0], 6));
        Assert.Equal(2, Provinces.CrossingLinks(diamond, new[] {2, 2}, 6).Length);

        // Two bosses in a line each keep their one link.
        Assert.Empty(Provinces.ExtraExits(Line(7), new[] {2, 4}, 6, Enumerable.Range(0, 7).ToArray()));

        // Two diamonds in a row: each boss zone keeps its path link, and pruning again finds nothing.
        var chain = Graph(8, (0, 1), (1, 2), (1, 3), (2, 4), (3, 4), (4, 5), (4, 6), (5, 7), (6, 7));
        var path = new[] {0, 1, 2, 4, 5, 7};
        var extra = Provinces.ExtraExits(chain, new[] {4, 1}, 7, path);
        Assert.Equal(new[] {(1, 3), (4, 6)}, extra);
        var pruned = Without(chain, extra);
        Assert.Empty(Provinces.ExtraExits(pruned, new[] {4, 1}, 7, path));
        Assert.Equal(Provinces.Regions(chain, new[] {4, 1}, 7), Provinces.Regions(pruned, new[] {4, 1}, 7));

        // A boss zone off the path, last on it, or followed by a zone it has no crossing link to is never left with fewer links.
        Assert.Empty(Provinces.ExtraExits(diamond, new[] {2}, 6, new[] {0, 1, 5, 6}));
        Assert.Empty(Provinces.ExtraExits(diamond, new[] {2}, 6, new[] {0, 1, 2}));
        Assert.Empty(Provinces.ExtraExits(diamond, new[] {2}, 6, new[] {0, 1, 2, 5, 6}));
    }

    [Fact]
    public void DistancesAreOfTheFinalGraph()
    {
        foreach (var galaxy in SectorGalaxies())
        {
            var adjacency = AdjacencyOf(galaxy);
            for (var zone = 0; zone < adjacency.Length; zone++)
            {
                var hops = Hops(adjacency, zone);
                Assert.Equal(hops.Sum(), galaxy.Zones[zone].Isolation);
                for (var other = 0; other < adjacency.Length; other++)
                    Assert.Equal(hops[other], galaxy.Zones[zone].Distance[galaxy.Zones[other]]);
            }
            // Homes were placed on the pruned graph: each boss mega's boss zone is within its influence of its home.
            foreach (var pair in galaxy.BossZones)
                Assert.True(Hops(adjacency, Array.IndexOf(galaxy.Zones, galaxy.HomeZones[pair.Key]))[Array.IndexOf(galaxy.Zones, pair.Value)] <= galaxy.InfluenceOf(pair.Key));
        }
    }

    [Fact]
    public void TheExitPathSurvivesPruning()
    {
        foreach (var galaxy in SectorGalaxies())
        {
            var adjacency = AdjacencyOf(galaxy);
            var path = ExitPathOf(galaxy);
            for (var i = 0; i + 1 < path.Length; i++) Assert.Contains(path[i + 1], adjacency[path[i]]);
            Assert.All(BossesOf(galaxy), boss => Assert.Contains(boss, path));
        }
    }

    [Fact]
    public void PruningIsDeterministic()
    {
        foreach (var seed in new uint[] {1, 7, 23})
        {
            var a = TryBuild(Shared, false, seed);
            var b = TryBuild(Shared, false, seed);
            Assert.NotNull(a);
            Assert.Equal(AdjacencyOf(a), AdjacencyOf(b));
            Assert.Equal(a.Zones.Select(zone => zone.Name), b.Zones.Select(zone => zone.Name));
            Assert.Equal(Shape(a), Shape(b));
        }
    }

    [Fact]
    public void ASavedBorderIsWhatLoads()
    {
        var (cache, _, _) = Shipped();
        var galaxy = MultiRegionSector(cache);
        var bosses = OtherBosses(galaxy);

        // Capture then Continue keeps one exit per boss zone.
        var (game, zones) = Capture(cache, galaxy);
        var loaded = Continue(cache, game, zones);
        Assert.Equal(AdjacencyOf(galaxy), AdjacencyOf(loaded));
        Assert.Equal(bosses.Length, Provinces.CrossingLinks(AdjacencyOf(loaded), BossesOf(loaded), ExitOf(loaded)).Length);

        // A save given a second crossing link loads with it: load never prunes, and the regions are still the bosses and one.
        var adjacency = AdjacencyOf(galaxy);
        var boss = bosses[0];
        var far = Provinces.CrossingLinks(adjacency, BossesOf(galaxy), ExitOf(galaxy)).First(link => link.Boss == boss).Far;
        var extra = adjacency[far].First(zone => zone != boss && !BossesOf(galaxy).Contains(zone) && !adjacency[boss].Contains(zone));
        var (older, olderZones) = Capture(cache, galaxy);
        olderZones[boss].AdjacentZones = olderZones[boss].AdjacentZones.Append(extra).ToArray();
        olderZones[extra].AdjacentZones = olderZones[extra].AdjacentZones.Append(boss).ToArray();
        var restored = Continue(cache, older, olderZones);
        Assert.Equal(2, Provinces.CrossingLinks(AdjacencyOf(restored), BossesOf(restored), ExitOf(restored)).Count(link => link.Boss == boss));
        Assert.Contains(restored.Zones[extra], restored.Zones[boss].AdjacentZones);
        Assert.Equal(bosses.Length + 1, RegionsOf(restored).Distinct().Count());
    }

    [Fact]
    public void ThePreludeIsNotPruned()
    {
        var (cache, _, _) = Shipped();
        var galaxy = Enumerable.Range(1, 25).Select(seed => TryBuild(cache, true, (uint) seed, Cast(Provinces.DefaultZonesPerProvince))).First(g => g != null);
        Assert.Empty(galaxy.BossZones);
        var before = AdjacencyOf(galaxy);
        galaxy.PlaceGate(galaxy.ResolveFaction(DemoTerminus.Cast.AntagonistFaction));
        Assert.Equal(before, AdjacencyOf(galaxy));
    }

    // ---- provinces

    [Fact]
    public void PartitionIsContiguousCompleteAndInsideRegions()
    {
        var galaxies = Galaxies(false, 6).Concat(Galaxies(true, 6)).ToArray();
        Assert.True(galaxies.Length >= 6);
        foreach (var galaxy in galaxies)
        {
            var regionOf = RegionsOf(galaxy);
            Assert.All(galaxy.Zones, zone => Assert.NotNull(zone.Province));
            var members = galaxy.Provinces.SelectMany(province => province.Zones).ToArray();
            Assert.Equal(galaxy.Zones.Length, members.Length);
            Assert.Equal(galaxy.Zones.Length, members.Distinct().Count());
            Assert.Equal(galaxy.Provinces.Select(province => Array.IndexOf(galaxy.Zones, province.Seed)).OrderBy(i => i), galaxy.Provinces.Select(province => Array.IndexOf(galaxy.Zones, province.Seed)));
            foreach (var province in galaxy.Provinces)
            {
                Assert.Contains(province.Seed, province.Zones);
                Assert.All(province.Zones, zone => Assert.Same(province, zone.Province));
                var indices = province.Zones.Select(zone => Array.IndexOf(galaxy.Zones, zone)).ToArray();
                Assert.Single(indices.Select(zone => regionOf[zone]).Distinct());
                var reached = new HashSet<GalaxyZone> {province.Seed};
                var stack = new Stack<GalaxyZone>(reached);
                while (stack.Count > 0)
                    foreach (var next in stack.Pop().AdjacentZones.Where(next => province.Zones.Contains(next) && reached.Add(next)))
                        stack.Push(next);
                Assert.Equal(province.Zones.Length, reached.Count);
            }
        }
    }

    [Fact]
    public void PartitionCountFollowsProvinceZones()
    {
        int Count(int[][] graph, int[] bosses, int exit, int perProvince, out int[] seedOf)
        {
            var regionOf = Provinces.Regions(graph, bosses, exit);
            seedOf = Provinces.Partition(graph, regionOf, 7u, perProvince);
            return seedOf.Distinct().Count();
        }

        Assert.Equal(3, Count(Line(20), new int[0], -1, 8, out _)); // 2.5 rounds up
        Assert.Equal(2, Count(Line(12), new int[0], -1, 8, out _)); // 1.5 rounds up
        Assert.Equal(1, Count(Line(5), new int[0], -1, 8, out _)); // a region smaller than ProvinceZones is one province
        Assert.Equal(5, Count(Line(20), new int[0], -1, 4, out _));
        Assert.Equal(1, Count(Line(20), new int[0], -1, 100, out _));

        // Per region: a boss zone at 11 on a path of 30 leaves regions of 12 and 18 zones.
        var path = Line(30);
        var count = Count(path, new[] {11}, 29, 6, out var seedOf);
        Assert.Equal(2 + 3, count);
        Assert.Equal(2, seedOf.Take(12).Distinct().Count());
        Assert.Equal(3, seedOf.Skip(12).Distinct().Count());
        Assert.All(seedOf.Distinct(), seed => Assert.Equal(seed, seedOf[seed]));
    }

    [Fact]
    public void PartitionIsDeterministic()
    {
        var links = new List<(int, int)>();
        for (var i = 0; i < 36; i++)
        {
            if (i % 6 != 5) links.Add((i, i + 1));
            if (i + 6 < 36) links.Add((i, i + 6));
        }
        var grid = Graph(36, links.ToArray());
        var regionOf = Provinces.Regions(grid, new int[0], -1);
        var first = Provinces.Partition(grid, regionOf, 1u, 8);
        Assert.Equal(first, Provinces.Partition(grid, regionOf, 1u, 8));
        Assert.Equal(first.Distinct().Count(), 5); // 36 / 8 = 4.5 rounds up
        var differing = Enumerable.Range(2, 30).Count(seed => !Provinces.Partition(grid, regionOf, (uint) seed, 8).SequenceEqual(first));
        Assert.True(differing > 20, $"only {differing} of 30 other run seeds moved the seeds");
    }

    [Fact]
    public void TheDemoGalaxyIsOneRegionOfSeveralProvinces()
    {
        var cutVertexExits = 0;
        var checkedGalaxies = 0;
        for (uint seed = 1; seed <= 24; seed++)
        {
            var galaxy = TryBuild(Shared, true, seed, Cast(Provinces.DefaultZonesPerProvince));
            if (galaxy == null) continue;
            checkedGalaxies++;
            Assert.All(RegionsOf(galaxy), region => Assert.Equal(0, region));
            Assert.Equal(8, galaxy.Provinces.Length); // 64 zones / 8 per province
            var before = Shape(galaxy);

            galaxy.PlaceGate(galaxy.ResolveFaction(DemoTerminus.Cast.AntagonistFaction));
            if (ExitIsACutVertex(galaxy)) cutVertexExits++;
            // The boss zone is the exit: re-deriving from the saved graph, boss zones and Exit still finds one region, and PlaceGate moved nothing.
            Assert.All(RegionsOf(galaxy), region => Assert.Equal(0, region));
            Assert.Equal(before, Shape(galaxy));
        }
        Assert.True(checkedGalaxies >= 10);
        Assert.True(cutVertexExits >= 3, $"only {cutVertexExits} demo galaxies had a cut-vertex Exit, so the case that splits a naive cut went unexercised");
    }

    [Fact]
    public void ProvincesTakeNothingFromTheGalaxyStream()
    {
        string[] Describe(Galaxy g) => g.Zones.Select((zone, i) =>
                $"{zone.Name}|{zone.Position.x:R},{zone.Position.y:R}|{string.Join(",", zone.AdjacentZones.Select(next => Array.IndexOf(g.Zones, next)))}|{zone.Owner?.Name}|{string.Join(",", zone.Factions.Select(f => f.Name))}")
            .Concat(new[]
            {
                $"entrance {Array.IndexOf(g.Zones, g.Entrance)}", $"exit {ExitOf(g)}",
                "bosses " + string.Join(",", g.BossZones.Select(b => $"{b.Key.Name}={Array.IndexOf(g.Zones, b.Value)}")),
                "homes " + string.Join(",", g.HomeZones.Select(b => $"{b.Key.Name}={Array.IndexOf(g.Zones, b.Value)}"))
            }).ToArray();

        var (cache, _, _) = Shipped();
        Province(cache, "one", 2);
        Province(cache, "two", 1);
        var compared = 0;
        foreach (var prelude in new[] {false, true})
            for (uint seed = 1; seed <= 4; seed++)
            {
                var without = SharedGalaxy(prelude, seed);
                var with = TryBuild(cache, prelude, seed);
                Assert.Equal(without == null, with == null);
                if (without == null) continue;
                Assert.Equal(Describe(without), Describe(with));
                Assert.All(without.Provinces, province => Assert.False(province.Type.IsSet()));
                Assert.All(with.Provinces, province => Assert.True(province.Type.IsSet()));
                compared++;
            }
        Assert.True(compared >= 4);
    }

    [Fact]
    public void PartitionSeedsAreTheLeastHashThenTheFarthestAndTiesGoToTheEarlierSeed()
    {
        var line = Line(9);
        var regionOf = Provinces.Regions(line, new int[0], -1);
        var zones = Enumerable.Range(0, 9).ToArray();
        var tied = 0;
        for (uint seed = 1; seed <= 60; seed++)
        {
            uint Hash(int zone) => $"{seed}:province-seed:{zone}".StableHash();
            var first = zones.OrderBy(Hash).ThenBy(zone => zone).First();
            var reach = zones.Max(zone => Math.Abs(zone - first));
            var second = zones.Where(zone => Math.Abs(zone - first) == reach).OrderBy(Hash).First();
            if (zones.Any(zone => Math.Abs(zone - first) == Math.Abs(zone - second))) tied++;
            // 9 zones at 5 per province is two seeds; a zone midway between them goes to the first.
            var expected = zones.Select(zone => Math.Abs(zone - first) <= Math.Abs(zone - second) ? first : second).ToArray();
            Assert.Equal(expected, Provinces.Partition(line, regionOf, seed, 5));
            // One province: the seed is the least hash.
            Assert.All(Provinces.Partition(line, regionOf, seed, 100), seedOf => Assert.Equal(first, seedOf));
        }
        Assert.True(tied > 5, "fixture: too few run seeds put a zone midway between the seeds");
    }

    [Fact]
    public void ListingAnEntryTwiceSumsItsWeights()
    {
        var cache = Empty();
        var a = Kind(cache, "a", 1f);
        Kind(cache, "b", 1f);
        var twice = Province(cache, "twice", 1, new[] {KindFactor(a, 2f), KindFactor(a, 2f)}, new[] {OreFactor(Ore("iron"), 2f), OreFactor(Ore("iron"), 3f)});
        var rock = Put(cache, "field-kind:rock", new FieldKindData {Name = "rock", GenerationWeight = 0f, Yields = new List<FieldYield> {Yield("iron", 1f)}});
        Assert.Equal(5f, FieldKinds.Yield(rock, twice, cache).Single().Amount);
        const int sites = 3000;
        var share = Enumerable.Range(0, sites).Count(i => FieldKinds.Assign($"belt-{i}", cache, twice).Key.Value == "field-kind:a") / (float) sites;
        Assert.InRange(share, 4f / 5 - .04f, 4f / 5 + .04f); // a: 1 x (2 + 2) against b: 1
        Assert.InRange(Enumerable.Range(0, sites).Count(i => FieldKinds.Assign($"belt-{i}", cache, default).Key.Value == "field-kind:a") / (float) sites, .46f, .54f);
    }

    // ---- the deal

    private static int[] Counts(CultRecordRef<ProvinceData>[] hand, params string[] types) =>
        types.Select(type => hand.Count(card => card.Key.Value == "province:" + type)).ToArray();

    [Fact]
    public void ARegionWithAsManyProvincesAsCardsHoldsEveryType()
    {
        var cache = Empty();
        Province(cache, "a", 3);
        Province(cache, "b", 1);
        Province(cache, "c", 2);
        Province(cache, "zero", 0);
        for (uint seed = 1; seed <= 40; seed++)
        {
            Assert.Equal(new[] {3, 1, 2}, Counts(Provinces.Deal(5, 6, seed, cache), "a", "b", "c"));
            Assert.Equal(new[] {6, 2, 4}, Counts(Provinces.Deal(5, 12, seed, cache), "a", "b", "c"));
            var more = Provinces.Deal(5, 8, seed, cache);
            Assert.All(new[] {"a", "b", "c"}, type => Assert.Contains(more, card => card.Key.Value == "province:" + type));
            Assert.DoesNotContain(more, card => card.Key.Value == "province:zero");
        }
    }

    [Fact]
    public void TheSameRunSeedDealsTheSameHand()
    {
        var cache = Empty();
        Province(cache, "a", 1);
        Province(cache, "b", 1);
        Province(cache, "never", 0);
        string[] Hand(int region, uint seed) => Provinces.Deal(region, 6, seed, cache).Select(card => card.Key.Value).ToArray();
        for (uint seed = 1; seed <= 20; seed++)
        {
            Assert.Equal(Hand(3, seed), Hand(3, seed));
            Assert.DoesNotContain("province:never", Hand(3, seed));
        }
        Assert.Contains(Enumerable.Range(2, 30), seed => !Hand(3, (uint) seed).SequenceEqual(Hand(3, 1u)));

        // No card to deal: every province is unset, whether the catalog holds no type or only types of weight zero.
        Assert.All(Provinces.Deal(0, 4, 1u, Empty()), card => Assert.False(card.IsSet()));
        var zeroes = Empty();
        Province(zeroes, "never", 0);
        var hand = Provinces.Deal(0, 4, 1u, zeroes);
        Assert.Equal(4, hand.Length);
        Assert.All(hand, card => Assert.False(card.IsSet()));
    }

    [Fact]
    public void CardCountNotProbabilityIsTheWeight()
    {
        var cache = Empty();
        Province(cache, "common", 3);
        Province(cache, "rare", 1);
        for (uint seed = 1; seed <= 200; seed++)
        {
            Assert.Equal(new[] {3, 1}, Counts(Provinces.Deal(0, 4, seed, cache), "common", "rare"));
            Assert.Equal(new[] {6, 2}, Counts(Provinces.Deal(0, 8, seed, cache), "common", "rare"));
        }
    }

    [Fact]
    public void ARegionsHandDoesNotDependOnOtherRegions()
    {
        var (cache, _, _) = Shipped();
        Province(cache, "a", 1);
        Province(cache, "b", 1);
        Province(cache, "c", 1);
        var hands = new List<string>();
        var regionsChecked = 0;
        foreach (var galaxy in new[] {false, true}.SelectMany(prelude => Enumerable.Range(1, 5).Select(seed => TryBuild(cache, prelude, (uint) seed))).Where(g => g != null))
        {
            var regionOf = RegionsOf(galaxy);
            foreach (var region in Enumerable.Range(0, galaxy.Zones.Length).GroupBy(zone => regionOf[zone]))
            {
                var provinces = galaxy.Provinces.Where(province => regionOf[Array.IndexOf(galaxy.Zones, province.Seed)] == region.Key).ToArray();
                var expected = Provinces.Deal(region.Key, provinces.Length, galaxy.Seed, cache);
                // Keyed by the region's least zone: nothing outside the region and the run seed enters the hand.
                Assert.Equal(expected.Select(card => card.Key.Value), provinces.Select(province => province.Type.Key.Value));
                if (provinces.Length >= 3) hands.Add(string.Join(",", expected.Select(card => card.Key.Value)));
                regionsChecked++;
            }
        }
        Assert.True(regionsChecked >= 10);
        Assert.True(hands.Distinct().Count() > 1, "every region of three or more provinces was dealt the same hand");
    }

    [Fact]
    public void TheDealDrawsFromTheDeckInKeyOrderAndReshufflesWhenItRunsOut()
    {
        var cache = Empty();
        Province(cache, "c", 3);
        Province(cache, "a", 2);
        Province(cache, "b", 1);
        for (uint seed = 1; seed <= 30; seed++)
        {
            // The rule restated: cards per type in key order, one weighted pick per draw at the draw's own site.
            var types = new[] {"province:a", "province:b", "province:c"};
            var full = new[] {2, 1, 3};
            var remaining = (int[]) full.Clone();
            var expected = new List<string>();
            for (var n = 0; n < 10; n++)
            {
                if (remaining.Sum() == 0) remaining = (int[]) full.Clone();
                var pick = $"{seed}:province-deal:7:{n}".StableHash() / 4294967296.0 * remaining.Sum();
                var card = 0;
                while (pick >= remaining[card]) pick -= remaining[card++];
                remaining[card]--;
                expected.Add(types[card]);
            }
            Assert.Equal(expected, Provinces.Deal(7, 10, seed, cache).Select(card => card.Key.Value));
        }
    }

    // ---- belts

    private static IEnumerable<(AsteroidBeltData Belt, GalaxyZone Zone, ZonePack Pack)> Belts(CultCache cache, Galaxy galaxy, ItemManager items, int zones)
    {
        foreach (var zone in galaxy.Zones.Take(zones))
        {
            var pack = ZoneGenerator.GenerateZone(items, RestoredHullsTests.TutorialZoneSettings(), RestoredHullsTests.AuthoredPlanetSettings(), galaxy, zone, isTutorial: zone == galaxy.Entrance);
            foreach (var belt in pack.Planets.Select(body => cache.Get(body)).OfType<AsteroidBeltData>())
                yield return (belt, zone, pack);
        }
    }

    private ItemManager Items(CultCache cache) => new ItemManager(1u, cache, new ProvenanceLedger(), Authored.Read<GameplaySettings>("GameplaySettings"), _ => { });

    private static void SilenceShippedKinds(CultCache cache)
    {
        foreach (var existing in cache.GetAll<FieldKindData>().ToArray())
        {
            existing.GenerationWeight = 0f;
            cache.Upsert(existing);
        }
    }

    [Fact]
    public void GeneratedBeltsDrawByTheirZonesProvince()
    {
        var firstOnly = 0;
        var secondOnly = 0;
        for (uint seed = 1; seed <= 25 && (firstOnly < 3 || secondOnly < 3); seed++)
        {
            var (cache, _, _) = Shipped();
            SilenceShippedKinds(cache);
            var a = Kind(cache, "a", 1f);
            var b = Kind(cache, "b", 1f);
            Province(cache, "no-a", 1, new[] {KindFactor(a, 0f)});
            Province(cache, "no-b", 1, new[] {KindFactor(b, 0f)});
            var galaxy = TryBuild(cache, true, seed, Cast(Provinces.DefaultZonesPerProvince));
            if (galaxy == null) continue;
            foreach (var (belt, zone, _) in Belts(cache, galaxy, Items(cache), 24))
            {
                var kind = belt.Kind.Key.Value;
                if (zone.Province.Type.Key.Value == "province:no-a")
                {
                    Assert.Equal("field-kind:b", kind);
                    firstOnly++;
                }
                else
                {
                    Assert.Equal("field-kind:a", kind);
                    secondOnly++;
                }
            }
        }
        Assert.True(firstOnly >= 3 && secondOnly >= 3, $"belts exercised: {firstOnly} and {secondOnly}");
    }

    [Fact]
    public void FirstLoadAssignsByTheZonesProvince()
    {
        for (uint seed = 1; seed <= 25; seed++)
        {
            var (cache, catalogPath, runPath) = Shipped();
            SilenceShippedKinds(cache);
            var galaxy = TryBuild(cache, true, seed, Cast(Provinces.DefaultZonesPerProvince));
            if (galaxy == null) continue;
            var items = Items(cache);
            // Generated while no kind could be picked: every belt is kindless, as one saved before field kinds existed.
            var generated = Belts(cache, galaxy, items, 16).ToArray();
            if (generated.Length < 4) continue;
            Assert.All(generated, g => Assert.False(g.Belt.Kind.IsSet()));

            var a = Kind(cache, "a", 1f);
            Kind(cache, "b", 1f);
            var noA = Province(cache, "no-a", 1, new[] {KindFactor(a, 0f)});
            var packs = generated.GroupBy(g => g.Pack).ToArray();
            var inProvince = packs[0].First().Zone;
            inProvince.Province = new GalaxyProvince {Seed = inProvince, Type = noA, Zones = new[] {inProvince}};

            var settings = RestoredHullsTests.AuthoredPlanetSettings();
            var loaded = new Zone(items, settings, packs[0].Key, inProvince, galaxy);
            foreach (var key in packs[0].Select(g => cache.RefOf(g.Belt).Key))
                Assert.Equal(FieldKinds.Assign(key.Value, cache, noA).Key.Value, ((AsteroidBeltData) loaded.Planets[key]).Kind.Key.Value);
            Assert.All(packs[0].Select(g => cache.RefOf(g.Belt).Key), key => Assert.Equal("field-kind:b", ((AsteroidBeltData) loaded.Planets[key]).Kind.Key.Value));

            // No GalaxyZone: the unweighted pick.
            var unweighted = 0;
            foreach (var pack in packs.Skip(1))
            {
                var bare = new Zone(items, settings, pack.Key, null, null);
                foreach (var key in pack.Select(g => cache.RefOf(g.Belt).Key))
                {
                    var kind = ((AsteroidBeltData) bare.Planets[key]).Kind.Key.Value;
                    Assert.Equal(FieldKinds.Assign(key.Value, cache, default).Key.Value, kind);
                    if (kind == "field-kind:a") unweighted++;
                }
            }
            Assert.True(unweighted > 0, "fixture: no unweighted belt drew the kind the province forbids, so the province could not be told from none");

            // Kept: a kind that would now win every pick changes nothing about a belt that has one.
            var keys = packs[0].Select(g => cache.RefOf(g.Belt).Key).ToArray();
            var assigned = keys.ToDictionary(key => key.Value, key => ((AsteroidBeltData) loaded.Planets[key]).Kind.Key.Value);
            Kind(cache, "z", 1e6f);
            var again = new Zone(items, settings, packs[0].Key, null, null);
            foreach (var key in keys) Assert.Equal(assigned[key.Value], ((AsteroidBeltData) again.Planets[key]).Kind.Key.Value);

            // Saved: a fresh open of the stores reads the assignment back.
            cache.FlushAsync().Wait();
            cache.Dispose();
            _caches.Remove(cache);
            var reopened = OpenCopy(catalogPath, runPath);
            _caches.Add(reopened);
            foreach (var key in keys) Assert.Equal(assigned[key.Value], ((AsteroidBeltData) reopened.Get(key)).Kind.Key.Value);
            return;
        }
        Assert.Fail("no seed generated enough belts; the first-load path went unexercised");
    }

    // ---- save and continue

    [Fact]
    public void ProvincesSurviveSaveAndContinue()
    {
        var (cache, catalog, run) = Shipped();
        Province(cache, "a", 1);
        Province(cache, "b", 1);
        Province(cache, "c", 1);
        // Four zones to a province, so a galaxy's saved provinces differ from what the save constructor's default would derive.
        var galaxy = Enumerable.Range(1, 25).Select(seed => DemoGalaxy(cache, (uint) seed, 4)).First(g => g != null);
        Assert.Equal(16, galaxy.Provinces.Length);
        var (game, zones) = Capture(cache, galaxy);
        Assert.Equal(galaxy.Provinces.Length, game.Provinces.Count);
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        cache.FlushAsync().Wait();
        cache.Dispose();
        _caches.Remove(cache);

        // Reopened from disk with a type of overwhelming weight added: a saved run keeps what it was dealt.
        var reopened = OpenCopy(catalog, run);
        _caches.Add(reopened);
        Put(reopened, "province:huge", new ProvinceData {Name = "huge", GenerationWeight = 1000000});
        var restored = new Galaxy(reopened, reopened.GetGlobal<SavedGame>(), _ => { });
        Assert.Equal(Shape(galaxy), Shape(restored));
        Assert.DoesNotContain(restored.Provinces, province => province.Type.Key.Value == "province:huge");
    }

    [Fact]
    public void AnOlderSaveGetsProvincesByTheSameRule()
    {
        var (cache, _, _) = Shipped();
        var a = Province(cache, "a", 1);
        Province(cache, "b", 1);
        Province(cache, "c", 1);
        var galaxy = Enumerable.Range(1, 40).Select(seed => DemoGalaxy(cache, (uint) seed)).First(g => g != null && ExitIsACutVertex(g));

        // No key 15: the same Regions, Partition and Deal, from the saved graph, boss zones, Exit and seed.
        var (older, olderZones) = Capture(cache, galaxy);
        older.Provinces = null;
        foreach (var zone in olderZones) zone.Province = 0;
        Assert.Equal(Shape(galaxy), Shape(Continue(cache, older, olderZones)));

        // A saved type that no longer resolves takes the card the region's Deal gives that province's place; the others are kept.
        var (game, zones) = Capture(cache, galaxy);
        game.Provinces[0].Type = new CultRecordRef<ProvinceData>(new CultRecordKey("province:gone"));
        foreach (var province in game.Provinces.Skip(1)) province.Type = a;
        var deal = Provinces.Deal(0, galaxy.Provinces.Length, galaxy.Seed, cache);
        Assert.Contains(deal.Skip(1), card => card.Key.Value != a.Key.Value);
        var restored = Continue(cache, game, zones);
        Assert.Equal(deal[0].Key.Value, restored.Provinces[0].Type.Key.Value);
        Assert.All(restored.Provinces.Skip(1), province => Assert.Equal(a.Key.Value, province.Type.Key.Value));
        Assert.Equal(galaxy.Provinces.Select(p => Array.IndexOf(galaxy.Zones, p.Seed)), restored.Provinces.Select(p => Array.IndexOf(restored.Zones, p.Seed)));
    }

    [Fact]
    public void ASectorGalaxyBuildsItsProvincesFromItsProvinceZones()
    {
        var (cache, _, _) = Shipped();
        var seed = Enumerable.Range(1, 40).First(s => SectorGalaxy(cache, (uint) s, 4) != null && SectorGalaxy(cache, (uint) s, 40) != null);
        foreach (var perProvince in new[] {4, 40})
        {
            var galaxy = SectorGalaxy(cache, (uint) seed, perProvince);
            var regionOf = RegionsOf(galaxy);
            var expected = Enumerable.Range(0, regionOf.Length).GroupBy(zone => regionOf[zone])
                .Sum(region => Math.Min(region.Count(), Math.Max(1, (int) Math.Round(region.Count() / (double) perProvince, MidpointRounding.AwayFromZero))));
            Assert.Equal(expected, galaxy.Provinces.Length);
        }
        Assert.True(SectorGalaxy(cache, (uint) seed, 4).Provinces.Length > SectorGalaxy(cache, (uint) seed, 40).Provinces.Length);
    }

    [Fact]
    public void ASectorGalaxysProvincesSurviveSaveAndContinue()
    {
        var (cache, catalog, run) = Shipped();
        Province(cache, "a", 1);
        Province(cache, "b", 1);
        Province(cache, "c", 1);
        var galaxy = MultiRegionSector(cache, 4);
        Assert.True(RegionsOf(galaxy).Distinct().Count() > 1);
        var (game, zones) = Capture(cache, galaxy);
        Assert.Equal(galaxy.Provinces.Length, game.Provinces.Count);
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        cache.FlushAsync().Wait();
        cache.Dispose();
        _caches.Remove(cache);

        // Reopened from disk with a type of overwhelming weight added: a saved run keeps what it was dealt.
        var reopened = OpenCopy(catalog, run);
        _caches.Add(reopened);
        Put(reopened, "province:huge", new ProvinceData {Name = "huge", GenerationWeight = 1000000});
        var restored = new Galaxy(reopened, reopened.GetGlobal<SavedGame>(), _ => { });
        Assert.Equal(Shape(galaxy), Shape(restored));
        Assert.DoesNotContain(restored.Provinces, province => province.Type.Key.Value == "province:huge");
    }

    [Fact]
    public void ASectorGalaxysOlderSaveGetsProvincesByTheSameRule()
    {
        var (cache, _, _) = Shipped();
        Province(cache, "a", 2);
        Province(cache, "b", 1);
        Province(cache, "c", 1);
        var galaxy = MultiRegionSector(cache);
        var shape = Shape(galaxy);

        // No key 15: every region is re-derived (Regions, Partition, Deal) from the saved graph, boss zones, Exit and seed.
        var (older, olderZones) = Capture(cache, galaxy);
        older.Provinces = null;
        foreach (var zone in olderZones) zone.Province = 0;
        var rederived = Continue(cache, older, olderZones);
        Assert.Equal(shape, Shape(rederived));

        // And the re-derived galaxy saves and continues to the same provinces.
        var (again, againZones) = Capture(cache, rederived);
        Assert.Equal(shape, Shape(Continue(cache, again, againZones)));
    }

    // ---- the documents

    [Fact]
    public void YieldsAndProvincesRoundTrip()
    {
        var (cache, catalog, run) = Shipped();
        var ore = Ore("iron");
        var kind = Put(cache, "field-kind:rock", new FieldKindData {Name = "rock", Yields = new List<FieldYield> {Yield("iron", 1.5f), Yield("gold", 2f, regional: true)}});
        var province = Put(cache, "province:rich", new ProvinceData
        {
            Name = "rich", GenerationWeight = 3,
            Kinds = new List<ProvinceKindWeight> {KindFactor(kind, 2.5f)}, Ores = new List<ProvinceOre> {OreFactor(ore, 4f)}
        });
        cache.FlushAsync().Wait();
        cache.Dispose();
        _caches.Remove(cache);

        var reopened = OpenCopy(catalog, run);
        _caches.Add(reopened);
        var rock = reopened.Get(kind);
        Assert.Equal(new[] {("ore:iron", 1.5f, false), ("ore:gold", 2f, true)}, rock.Yields.Select(y => (y.Commodity.Key.Value, y.Abundance, y.Regional)).ToArray());
        var rich = reopened.Get(province);
        Assert.Equal(3, rich.GenerationWeight);
        Assert.Equal(new[] {("field-kind:rock", 2.5f)}, rich.Kinds.Select(k => (k.Kind.Key.Value, k.Weight)).ToArray());
        Assert.Equal(new[] {("ore:iron", 4f)}, rich.Ores.Select(o => (o.Commodity.Key.Value, o.Weight)).ToArray());

        // A kind written before key 3 loads with no yields.
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);
        var old = MessagePackSerializer.Deserialize<FieldKindData>(MessagePackSerializer.Serialize(new object[] {"Old", 1.5f, 2f}), options);
        Assert.Equal("Old", old.Name);
        Assert.Equal(2f, old.GenerationWeight);
        Assert.Empty(old.Yields);
    }

    [Fact]
    public void ABodySavedWithResourcesStillLoads()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);
        var belt = new AsteroidBeltData {Name = "retired-key", Mass = 12f, Asteroids = new Asteroid[0]};
        var bytes = MessagePackSerializer.Serialize(typeof(AsteroidBeltData), belt, options);

        // The same record with a non-empty key 4, the stored Resources of a body from before the key was retired.
        var source = new System.Buffers.ReadOnlySequence<byte>(bytes);
        var reader = new MessagePackReader(source);
        var length = reader.ReadArrayHeader();
        Assert.True(length > 4);
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(output);
        writer.WriteArrayHeader(length);
        for (var key = 0; key < length; key++)
        {
            var start = reader.Position;
            reader.Skip();
            if (key == 4)
            {
                writer.WriteMapHeader(1);
                writer.Write("ore:iron");
                writer.Write(3.5f);
            }
            else writer.WriteRaw(source.Slice(start, reader.Position));
        }
        writer.Flush();

        var loaded = (AsteroidBeltData) MessagePackSerializer.Deserialize(typeof(AsteroidBeltData), output.WrittenMemory, options);
        Assert.Equal("retired-key", loaded.Name);
        Assert.Equal(12f, loaded.Mass);
        Assert.Empty(loaded.Asteroids);
    }
}
