using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using Random = CultMath.Random;

// Test-side handle on the shipped catalog's trade-hub kind, the one Zenith carries (ruling zenith-is-a-trade-hub).
internal static class TestStationKinds
{
    public static readonly CultRecordRef<StationKindData> TradeHub = new CultRecordRef<StationKindData>(StationKindData.KeyOf("trade-hub"));
}

// Station kinds (aetheria-living-world, cut station-kind-datum): the one list of kinds in the catalog, a hull's kind, a
// placed station's kind, and the stand-in rule for a faction with no model of a kind. Worlds here are the composed shipped
// catalog copied to a temp directory, with Zenith taken off the market so the only station hulls are the ones a test adds.
public sealed class StationKindTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stationkinds-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();
    private int _worlds;

    public StationKindTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static CultRecordRef<StationKindData> Kind(string id) => new CultRecordRef<StationKindData>(StationKindData.KeyOf(id));

    private sealed class World
    {
        public CultCache Cache;
        public ItemManager Items;
        public List<string> Log = new List<string>();
        public Faction Zh, Au, Lucent;
        public HullData Zenith;
        private FactionProductData _zenithProduct;
        private int _added;

        public static World Make(StationKindTests host, bool zenithOffered)
        {
            var dir = Path.Combine(host._root, "world" + host._worlds++);
            Directory.CreateDirectory(dir);
            File.Copy(TestCatalog.Repo, Path.Combine(dir, "Aetheria.cc"));
            var cache = new CultCache(TestCatalog.Registry());
            host._caches.Add(cache);
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(dir, "Aetheria.cc")), AetheriaStores.CatalogTypes);
            var world = new World { Cache = cache };
            world.Items = new ItemManager(1u, cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), world.Log.Add);
            world.Zh = cache.GetAll<Faction>().Single(f => f.Name == "Zhestokost");
            world.Au = cache.GetAll<Faction>().Single(f => f.ShortName == "AU");
            world.Lucent = cache.GetAll<Faction>().Single(f => f.ShortName == "Lucent");
            world.Zenith = cache.GetAll<HullData>().Single(h => h.Name == "Zenith");
            world._zenithProduct = cache.GetAll<FactionProductData>().Single(p => cache.Get(p.Design) == world.Zenith);
            if (!zenithOffered)
            {
                world._zenithProduct.NotOffered = true;
                cache.Upsert(world._zenithProduct);
            }
            return world;
        }

        // A station hull of the given kind that this faction sells: a copy of Zenith's body, so it takes the same docking bay
        // and heater, with its own name and kind.
        public HullData Sell(Faction maker, string kindId, string name = null)
        {
            var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
            var hull = MessagePackSerializer.Deserialize<HullData>(MessagePackSerializer.Serialize(Zenith, options), options);
            hull.Name = name ?? $"{maker.ShortName} {kindId} {_added++}";
            hull.Kind = Kind(kindId);
            var hullRef = Cache.Upsert(hull);
            var product = MessagePackSerializer.Deserialize<FactionProductData>(MessagePackSerializer.Serialize(_zenithProduct, options), options);
            product.Name = hull.Name;
            product.Design = new CultRecordRef<CraftedItemData>(hullRef.Key);
            product.Manufacturer = Cache.RefOf(maker);
            product.NotOffered = false;
            Cache.Upsert(product);
            return hull;
        }

        public void StandIn(string kindId, Faction faction, string asKindId)
        {
            var kind = Cache.Get(Kind(kindId));
            kind.StandIns.Add(new StationKindStandIn { Faction = Cache.RefOf(faction), Kind = Kind(asKindId) });
            Cache.Upsert(kind);
        }

        public LoadoutGenerator Generator(Faction faction, uint seed = 1)
        {
            var random = new Random(seed);
            Items.Random = new Random(seed);
            return new LoadoutGenerator(ref random, Items, null, null, faction, .5f);
        }

        public string HullName(OrbitalEntityPack pack) => Items.GetData(pack.Hull).Name;
        public HullData HullOf(OrbitalEntityPack pack) => (HullData) Items.GetData(pack.Hull);
    }

    // ---- Open ----

    private CultCache SeedAndReopen(Action<CultCache> seed, out string path)
    {
        path = Path.Combine(_root, "open" + _worlds++ + ".cc");
        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
            seed(cache);
            cache.FlushAsync().Wait();
        }
        return null;
    }

    private string OpenRefusal(Action<CultCache> seed)
    {
        SeedAndReopen(seed, out var path);
        var failure = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(path));
        return failure.Message;
    }

    private static HullData Hull(HullType type, CultRecordRef<StationKindData> kind = default) =>
        new HullData { Name = "Probe " + type, HullType = type, Shape = new Shape(2, 2), Price = 1, Kind = kind };

    [Fact]
    public void OpenRefusesKindlessStationHull()
    {
        Assert.Contains("has no Kind", OpenRefusal(cache => cache.Upsert(Hull(HullType.Station))));
        Assert.Contains("names no station kind", OpenRefusal(cache => cache.Upsert(Hull(HullType.Station, Kind("nowhere")))));
        var shipWithKind = OpenRefusal(cache =>
        {
            var kind = new CultRecordRef<StationKindData>(cache.Upsert(new StationKindData { Name = "Hub" }).Key);
            cache.Upsert(Hull(HullType.Ship, kind));
        });
        Assert.Contains("only a station hull has one", shipWithKind);
        var turretWithKind = OpenRefusal(cache =>
        {
            var kind = new CultRecordRef<StationKindData>(cache.Upsert(new StationKindData { Name = "Hub" }).Key);
            cache.Upsert(Hull(HullType.Turret, kind));
        });
        Assert.Contains("only a station hull has one", turretWithKind);
    }

    [Fact]
    public void OpenRefusesBadStandIn()
    {
        CultRecordRef<StationKindData> Mint(CultCache cache, string name, Action<StationKindData> shape = null)
        {
            var kind = new StationKindData { Name = name };
            shape?.Invoke(kind);
            return new CultRecordRef<StationKindData>(cache.Upsert(kind).Key);
        }

        Assert.Contains("kind itself", OpenRefusal(cache =>
        {
            CultRecordRef<Faction> faction = cache.Upsert(new Faction { Name = "F", ShortName = "F" });
            var kind = new StationKindData { Name = "Self" };
            var handle = cache.Upsert(kind);
            kind.StandIns.Add(new StationKindStandIn { Faction = faction, Kind = new CultRecordRef<StationKindData>(handle.Key) });
            cache.Upsert(kind);
        }));
        Assert.Contains("StandIns[0].Faction names no faction", OpenRefusal(cache =>
        {
            var other = Mint(cache, "Other");
            Mint(cache, "Lonely", kind => kind.StandIns.Add(new StationKindStandIn { Faction = new CultRecordRef<Faction>(new CultRecordKey("nobody")), Kind = other }));
        }));
        Assert.Contains("StandIns[0].Kind names no station kind", OpenRefusal(cache =>
        {
            CultRecordRef<Faction> faction = cache.Upsert(new Faction { Name = "F", ShortName = "F" });
            Mint(cache, "Lonely", kind => kind.StandIns.Add(new StationKindStandIn { Faction = faction, Kind = Kind("nowhere") }));
        }));
        Assert.Contains("one hop", OpenRefusal(cache =>
        {
            CultRecordRef<Faction> faction = cache.Upsert(new Faction { Name = "F", ShortName = "F" });
            var third = Mint(cache, "Third");
            var second = Mint(cache, "Second", kind => kind.StandIns.Add(new StationKindStandIn { Faction = faction, Kind = third }));
            Mint(cache, "First", kind => kind.StandIns.Add(new StationKindStandIn { Faction = faction, Kind = second }));
        }));
    }

    // ---- generation ----

    [Fact]
    public void StationsArePlacedByKind()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "mining");
        world.Sell(world.Au, "shipyard");
        world.Sell(world.Au, "mining");

        var kinds = StationKinds.KindsOf(world.Cache, world.Au).Select(kind => kind.Key.Value).ToArray();
        Assert.Equal(new[] { StationKindData.KeyOf("mining").Value, StationKindData.KeyOf("shipyard").Value }, kinds);

        var seen = new HashSet<string>();
        for (var site = 0; site < 40; site++)
        {
            var kind = StationKinds.Choose(world.Cache, world.Au, new CultRecordKey("site-" + site));
            Assert.Contains(kind.Key.Value, kinds);
            seen.Add(kind.Key.Value);
            var pack = world.Generator(world.Au, (uint) site + 1).GenerateStationLoadout(kind);
            Assert.NotNull(pack);
            Assert.Equal(kind.Key, pack.Kind.Key);
            Assert.Equal(kind.Key, world.HullOf(pack).Kind.Key);
        }
        Assert.Equal(kinds.Length, seen.Count);
    }

    [Fact]
    public void StandInUsesFactionModelAndKeepsName()
    {
        var world = World.Make(this, zenithOffered: false);
        var hub = world.Sell(world.Zh, "trade-hub", "Zh Hub");
        world.StandIn("mining", world.Zh, "trade-hub");
        var mining = world.Cache.Get(Kind("mining"));

        Assert.Contains(Kind("mining").Key.Value, StationKinds.KindsOf(world.Cache, world.Zh).Select(kind => kind.Key.Value));
        Assert.Equal(Kind("trade-hub").Key, StationKinds.HullKindFor(world.Cache, world.Zh, Kind("mining")).Key);
        for (uint seed = 1; seed <= 10; seed++)
        {
            var pack = world.Generator(world.Zh, seed).GenerateStationLoadout(Kind("mining"));
            Assert.Equal("Zh Hub", world.HullName(pack));
            Assert.Equal(Kind("mining").Key, pack.Kind.Key);
            Assert.Equal(mining.Name, pack.Name);
        }
        var own = world.Generator(world.Zh).GenerateStationLoadout(Kind("trade-hub"));
        Assert.Equal(hub.Name, own.Name);
    }

    [Fact]
    public void ModelWinsOverStaleStandIn()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Zh, "trade-hub", "Zh Hub");
        world.Sell(world.Zh, "mining", "Zh Mine");
        world.StandIn("mining", world.Zh, "trade-hub");

        Assert.Equal(Kind("mining").Key, StationKinds.HullKindFor(world.Cache, world.Zh, Kind("mining")).Key);
        for (uint seed = 1; seed <= 10; seed++)
        {
            var pack = world.Generator(world.Zh, seed).GenerateStationLoadout(Kind("mining"));
            Assert.Equal("Zh Mine", world.HullName(pack));
            Assert.Equal("Zh Mine", pack.Name);
        }
    }

    [Fact]
    public void FactionWithoutModelsFallsBack()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "shipyard");
        Assert.Empty(StationKinds.KindsOf(world.Cache, world.Lucent));

        var logged = new List<string>();
        var kind = StationKinds.Choose(world.Cache, world.Lucent, new CultRecordKey("site"), logged.Add);
        Assert.Equal(Kind("shipyard").Key, kind.Key);
        Assert.Single(logged);

        var pack = world.Generator(world.Lucent).GenerateStationLoadout(kind);
        Assert.NotNull(pack);
        Assert.Equal(kind.Key, pack.Kind.Key);
    }

    [Fact]
    public void KindChoiceLeavesRandomStream()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "mining");
        world.Sell(world.Au, "shipyard");
        world.Sell(world.Au, "depot");
        var keys = Enumerable.Range(0, 30).Select(i => new CultRecordKey("orbit-" + i)).ToArray();
        var first = keys.Select(key => StationKinds.Choose(world.Cache, world.Au, key).Key.Value).ToArray();
        Assert.Equal(first, keys.Reverse().Select(key => StationKinds.Choose(world.Cache, world.Au, key).Key.Value).Reverse().ToArray());
        Assert.True(first.Distinct().Count() > 1, "the stable hash spreads sites over the faction's kinds");

        // Generating a zone with one station kind in the catalog and with several draws the same orbits and turrets.
        (float, float)[] Orbits(Action<World> stock)
        {
            var w = World.Make(this, zenithOffered: true);
            stock?.Invoke(w);
            var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
            var dir = Path.Combine(_root, "galaxy" + _worlds++);
            var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
                authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"), w.Cache, new PlayerSettings(),
                Directory.CreateDirectory(Path.Combine(dir, "Narrative")), _ => { }, null, 3);
            var pack = ZoneGenerator.GenerateZone(w.Items, authored.Read<ZoneGenerationSettings>("ZoneSettings"),
                authored.Read<PlanetSettings>("PlanetSettings"), galaxy, galaxy.Entrance, galaxy.IsPrelude, true);
            Assert.NotEmpty(pack.Entities.OfType<OrbitalEntityPack>());
            return pack.Orbits.Select(orbit => w.Cache.Get(orbit)).Select(orbit => (orbit.Distance, orbit.Phase)).ToArray();
        }
        var one = Orbits(null);
        var several = Orbits(w => { w.Sell(w.Au, "mining"); w.Sell(w.Zh, "shipyard"); w.Sell(w.Lucent, "depot"); });
        Assert.Equal(one, several);
    }

    [Fact]
    public void GeneratedZonesPlaceStationsByKind()
    {
        var w = World.Make(this, zenithOffered: true);
        w.Sell(w.Au, "mining");
        w.Sell(w.Au, "shipyard");
        var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
        var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"), w.Cache, new PlayerSettings(),
            Directory.CreateDirectory(Path.Combine(_root, "zones", "Narrative")), _ => { }, null, 5);
        var stations = 0;
        foreach (var galaxyZone in galaxy.Zones.Take(8))
        {
            var pack = ZoneGenerator.GenerateZone(w.Items, authored.Read<ZoneGenerationSettings>("ZoneSettings"),
                authored.Read<PlanetSettings>("PlanetSettings"), galaxy, galaxyZone, galaxyZone == galaxy.Entrance && galaxy.IsPrelude, true);
            foreach (var station in pack.Entities.OfType<OrbitalEntityPack>().Where(e => w.HullOf(e).HullType == HullType.Station))
            {
                stations++;
                Assert.True(station.Kind.IsSet(), "a placed station has a kind");
                Assert.Equal(station.Kind.Key, w.HullOf(station).Kind.Key);
                var faction = w.Cache.Get(station.Faction);
                var kinds = StationKinds.KindsOf(w.Cache, faction);
                if (kinds.Length > 0) Assert.Contains(station.Kind.Key.Value, kinds.Select(kind => kind.Key.Value));
            }
        }
        Assert.True(stations > 0, "the sample generated stations");
    }

    [Fact]
    public void KindSurvivesSave()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "research");
        var pack = world.Generator(world.Au).GenerateStationLoadout(Kind("research"));
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var bytes = MessagePackSerializer.Serialize<EntityPack>(pack, options);
        var restored = Assert.IsType<OrbitalEntityPack>(MessagePackSerializer.Deserialize<EntityPack>(bytes, options));
        Assert.Equal(Kind("research").Key, restored.Kind.Key);

        var entity = Assert.IsType<OrbitalEntity>(EntitySerializer.Unpack(world.Items, null, restored));
        Assert.Equal(Kind("research").Key, entity.Kind.Key);
        Assert.Equal(Kind("research").Key, ((OrbitalEntityPack) EntitySerializer.Pack(entity)).Kind.Key);
    }

    // ---- the shipped catalog ----

    [Fact]
    public void ShippedCatalogHoldsOneKindList()
    {
        using var cache = AetheriaStores.Open(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"));
        var ids = new[]
        {
            "mining", "volatiles", "agriculture", "processing", "fabrication", "shipyard", "depot", "trade-hub", "residential",
            "habitat-support", "urban", "administrative", "power", "research", "salvage", "black-market", "waystation"
        };
        Assert.Equal(ids.Select(id => StationKindData.KeyOf(id).Value).OrderBy(key => key, StringComparer.Ordinal),
            cache.GetAll<StationKindData>().Select(kind => cache.RefOf(kind).Key.Value).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(StationKindData.KeyOf("trade-hub").Value, cache.GetAll<HullData>().Single(h => h.Name == "Zenith").Kind.Key.Value);
    }

    [Fact]
    public void ShippedStandInsAreLive()
    {
        using var cache = AetheriaStores.Open(TestCatalog.Repo);
        foreach (var kind in cache.GetAll<StationKindData>())
            foreach (var standIn in kind.StandIns)
            {
                var faction = cache.Get(standIn.Faction);
                var models = StationKinds.KindsOf(cache, faction);
                var own = cache.GetAll<FactionProductData>().Where(p => cache.Get(p.Manufacturer) == faction && LoadoutGenerator.IsOffered(cache, p))
                    .Select(p => cache.Get(p.Design) as HullData).Where(h => h != null && h.HullType == HullType.Station).Select(h => h.Kind.Key).ToArray();
                Assert.DoesNotContain(cache.RefOf(kind).Key, own);
                Assert.Contains(standIn.Kind.Key, own);
                Assert.NotEmpty(models);
            }
    }
}
