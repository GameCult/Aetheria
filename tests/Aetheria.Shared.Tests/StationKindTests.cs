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
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(dir, "run.cc")), AetheriaStores.RunTypes);
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

        // Takes a sold hull's price to zero, or its product's maker away: the hull is still on a product but no hull draw can reach it.
        public void Unsellable(HullData hull, bool unsetMaker)
        {
            var product = Cache.GetAll<FactionProductData>().Single(p => Cache.Get(p.Design) == hull);
            if (unsetMaker) { product.Manufacturer = default; Cache.Upsert(product); }
            else { hull.Price = 0; Cache.Upsert(hull); }
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
        // A stand-in whose kind stands in for another faction only is one hop for this faction: Open accepts it.
        SeedAndReopen(cache =>
        {
            CultRecordRef<Faction> mine = cache.Upsert(new Faction { Name = "F", ShortName = "F" });
            CultRecordRef<Faction> other = cache.Upsert(new Faction { Name = "G", ShortName = "G" });
            var third = Mint(cache, "Third");
            var second = Mint(cache, "Second", kind => kind.StandIns.Add(new StationKindStandIn { Faction = other, Kind = third }));
            Mint(cache, "First", kind => kind.StandIns.Add(new StationKindStandIn { Faction = mine, Kind = second }));
        }, out var accepted);
        AetheriaStores.Open(accepted).Dispose();
        // Any one of the stand-in kind's entries for the same faction makes a second hop.
        Assert.Contains("one hop", OpenRefusal(cache =>
        {
            CultRecordRef<Faction> mine = cache.Upsert(new Faction { Name = "F", ShortName = "F" });
            CultRecordRef<Faction> other = cache.Upsert(new Faction { Name = "G", ShortName = "G" });
            var third = Mint(cache, "Third");
            var second = Mint(cache, "Second", kind =>
            {
                kind.StandIns.Add(new StationKindStandIn { Faction = other, Kind = third });
                kind.StandIns.Add(new StationKindStandIn { Faction = mine, Kind = third });
            });
            Mint(cache, "First", kind => kind.StandIns.Add(new StationKindStandIn { Faction = mine, Kind = second }));
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
            var kind = StationKinds.Choose(world.Cache, world.Au, "site-" + site);
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

    // A faction that places a kind through a stand-in no maker offers cannot field it: the kind is out of its pool and it falls back
    // exactly as a faction with no kinds does. Once any maker offers the stand-in's hull kind, the faction keeps its kind.
    [Fact]
    public void FactionWhoseStandInNoMakerOffersFallsBackToAnyMaker()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "shipyard");
        world.StandIn("mining", world.Lucent, "depot");
        Assert.Equal(new[] { Kind("mining").Key }, StationKinds.KindsOf(world.Cache, world.Lucent).Select(kind => kind.Key));

        var logged = new List<string>();
        var kind = StationKinds.Choose(world.Cache, world.Lucent, "site", logged.Add);
        Assert.Equal(Kind("shipyard").Key, kind.Key);
        Assert.Contains(world.Lucent.Name, Assert.Single(logged));
        var pack = world.Generator(world.Lucent).GenerateStationLoadout(kind);
        Assert.NotNull(pack);
        Assert.Equal(Kind("shipyard").Key, world.HullOf(pack).Kind.Key);

        world.Sell(world.Au, "depot");
        var kept = new List<string>();
        var placed = StationKinds.Choose(world.Cache, world.Lucent, "site", kept.Add);
        Assert.Equal(Kind("mining").Key, placed.Key);
        Assert.Empty(kept);
        var depotStation = world.Generator(world.Lucent).GenerateStationLoadout(placed);
        Assert.NotNull(depotStation);
        Assert.Equal(Kind("depot").Key, world.HullOf(depotStation).Kind.Key);
    }

    [Fact]
    public void FactionWithoutModelsFallsBack()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "shipyard");
        Assert.Empty(StationKinds.KindsOf(world.Cache, world.Lucent));

        var logged = new List<string>();
        var kind = StationKinds.Choose(world.Cache, world.Lucent, "site", logged.Add);
        Assert.Equal(Kind("shipyard").Key, kind.Key);
        Assert.Single(logged);
        Assert.Contains(world.Lucent.Name, logged[0]);
        var anonymous = new List<string>();
        StationKinds.Choose(world.Cache, null, "site", anonymous.Add);
        Assert.Contains("This galaxy", Assert.Single(anonymous));
        world.StandIn("mining", world.Au, "shipyard");
        Assert.Equal(Kind("mining").Key, StationKinds.HullKindFor(world.Cache, null, Kind("mining")).Key);
        Assert.False(StationKinds.HullKindFor(world.Cache, world.Au, default).IsSet());

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
        var keys = Enumerable.Range(0, 30).Select(i => "orbit-" + i).ToArray();
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

    // A station's kind is a function of the zone's seed and the orbit's place in generation, not of a key the cache mints:
    // the same zone generated again, through the real key minting, gives the same stations the same kinds in the same order.
    [Fact]
    public void SameSeedGivesTheSameStationKinds()
    {
        string[] Kinds()
        {
            var w = World.Make(this, zenithOffered: true);
            foreach (var faction in new[] { w.Au, w.Zh, w.Lucent })
                foreach (var kind in new[] { "mining", "shipyard", "depot", "power" }) w.Sell(faction, kind);
            var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
            var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
                authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"), w.Cache, new PlayerSettings(),
                Directory.CreateDirectory(Path.Combine(_root, "galaxy" + _worlds++, "Narrative")), _ => { }, null, 3);
            return galaxy.Zones.Take(8).SelectMany(zone => ZoneGenerator.GenerateZone(w.Items, authored.Read<ZoneGenerationSettings>("ZoneSettings"),
                    authored.Read<PlanetSettings>("PlanetSettings"), galaxy, zone, zone == galaxy.Entrance && galaxy.IsPrelude, true)
                .Entities.OfType<OrbitalEntityPack>().Where(e => e.Kind.IsSet()).Select(e => e.Kind.Key.Value)).ToArray();
        }
        var first = Kinds();
        Assert.True(first.Length >= 3, "the sample placed stations");
        for (var run = 1; run < 6; run++) Assert.Equal(first, Kinds());
    }

    [Fact]
    public void GeneratedZonesPlaceStationsByKind()
    {
        var w = World.Make(this, zenithOffered: true);
        w.Sell(w.Au, "mining");
        w.Sell(w.Au, "shipyard");
        w.StandIn("depot", w.Lucent, "power");
        w.StandIn("depot", w.Zh, "power");
        var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
        var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"), w.Cache, new PlayerSettings(),
            Directory.CreateDirectory(Path.Combine(_root, "zones", "Narrative")), _ => { }, null, 5);
        var stations = 0;
        var fallbacks = 0;
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
                var fieldable = OracleFieldableKinds(w.Cache, faction);
                if (fieldable.Count > 0) Assert.Contains(station.Kind.Key.Value, fieldable);
                else
                {
                    fallbacks++;
                    Assert.Contains(station.Kind.Key.Value, OracleOfferedKinds(w.Cache));
                }
            }
        }
        Assert.True(fallbacks > 0, "the sample seated a faction that could field none of its kinds, by the fallback");
        Assert.True(stations > 0, "the sample generated stations");
    }

    // An oracle for what a hull draw can reach, read straight off the records and independent of StationKinds and LoadoutGenerator:
    // a station hull with a kind and a price, sold by an unbinned product that names a maker and is not off the market.
    private static IEnumerable<(CultRecordKey maker, string kind)> OracleSoldStationKinds(CultCache cache) =>
        cache.GetAll<FactionProductData>()
            .Where(p => !p.NotOffered && p.Bin == null && p.Manufacturer.IsSet())
            .Select(p => (p, hull: cache.Get(p.Design) as HullData))
            .Where(e => e.hull != null && e.hull.HullType == HullType.Station && e.hull.Kind.IsSet() && e.hull.Price > 0)
            .Select(e => (e.p.Manufacturer.Key, e.hull.Kind.Key.Value));

    private static HashSet<string> OracleOfferedKinds(CultCache cache) => OracleSoldStationKinds(cache).Select(e => e.kind).ToHashSet();

    // The kinds a faction can field: those it has its own model of, and those it stands in on whose stand-in kind some maker sells.
    private static HashSet<string> OracleFieldableKinds(CultCache cache, Faction faction)
    {
        var offered = OracleOfferedKinds(cache);
        var own = OracleSoldStationKinds(cache).Where(e => e.maker.Equals(cache.RefOf(faction).Key)).Select(e => e.kind).ToHashSet();
        foreach (var kind in cache.GetAll<StationKindData>())
            foreach (var standIn in kind.StandIns.Where(entry => cache.Get(entry.Faction) == faction))
                if (offered.Contains(standIn.Kind.Key.Value)) own.Add(cache.RefOf(kind).Key.Value);
        return own;
    }

    // Choose keeps a kind exactly when GenerateStationLoadout can draw a hull of it: a hull sold at Price 0, or by a product with no
    // maker, is no hull a draw reaches, so a faction standing in on its kind is not seated on it. The tutorial entrance still gets a station.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AKindIsKeptExactlyWhenAHullOfItCanBeDrawn(bool unsetMaker)
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "mining");
        var unsold = world.Sell(world.Au, "shipyard");
        world.Unsellable(unsold, unsetMaker);
        // No hull draw reaches it: a station of its kind cannot be generated for anyone.
        Assert.Null(world.Generator(world.Au).GenerateStationLoadout(Kind("shipyard")));
        Assert.Null(world.Generator(world.Lucent).GenerateStationLoadout(Kind("shipyard")));
        world.StandIn("mining", world.Lucent, "shipyard");
        world.StandIn("mining", world.Zh, "shipyard");

        foreach (var faction in new[] { world.Au, world.Lucent, world.Zh })
            for (var site = 0; site < 12; site++)
            {
                var kind = StationKinds.Choose(world.Cache, faction, "site-" + site);
                Assert.Equal(Kind("mining").Key, kind.Key);
                Assert.NotNull(world.Generator(faction).GenerateStationLoadout(kind));
            }

        var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
        var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"), world.Cache, new PlayerSettings(),
            Directory.CreateDirectory(Path.Combine(_root, "galaxy" + _worlds++, "Narrative")), _ => { }, null, 5);
        var entrance = ZoneGenerator.GenerateZone(world.Items, authored.Read<ZoneGenerationSettings>("ZoneSettings"),
            authored.Read<PlanetSettings>("PlanetSettings"), galaxy, galaxy.Entrance, galaxy.IsPrelude, true);
        Assert.Contains(entrance.Entities.OfType<OrbitalEntityPack>(), e => world.HullOf(e).HullType == HullType.Station);
    }

    // A faction with one kind it can field and one it cannot places only the one it can, and never takes the fallback.
    [Fact]
    public void AFactionWithSomeKindsFieldablePlacesOnlyThose()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "shipyard");
        world.StandIn("mining", world.Lucent, "shipyard");
        world.StandIn("depot", world.Lucent, "power");
        Assert.Equal(2, StationKinds.KindsOf(world.Cache, world.Lucent).Length);

        var logged = new List<string>();
        for (var site = 0; site < 40; site++)
            Assert.Equal(Kind("mining").Key, StationKinds.Choose(world.Cache, world.Lucent, "site-" + site, logged.Add).Key);
        Assert.Empty(logged);
    }

    // The fallback pool is every kind some maker offers, spread over sites, not the first of them.
    [Fact]
    public void TheFallbackSpreadsSitesOverEveryOfferedKind()
    {
        var world = World.Make(this, zenithOffered: false);
        world.Sell(world.Au, "mining");
        world.Sell(world.Au, "shipyard");
        world.Sell(world.Au, "depot");
        var chosen = Enumerable.Range(0, 40).Select(site => StationKinds.Choose(world.Cache, world.Lucent, "site-" + site).Key.Value).ToHashSet();
        Assert.Equal(new[] { Kind("depot"), Kind("mining"), Kind("shipyard") }.Select(kind => kind.Key.Value).OrderBy(k => k, StringComparer.Ordinal),
            chosen.OrderBy(k => k, StringComparer.Ordinal));
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
            "habitat-support", "urban", "administrative", "power", "research", "salvage", "black-market", "waystation", "munitions-works"
        };
        Assert.Equal(ids.Select(id => StationKindData.KeyOf(id).Value).OrderBy(key => key, StringComparer.Ordinal),
            cache.GetAll<StationKindData>().Select(kind => cache.RefOf(kind).Key.Value).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(StationKindData.KeyOf("trade-hub").Value, cache.GetAll<HullData>().Single(h => h.Name == "Zenith").Kind.Key.Value);
    }

    // Every stand-in is judged from the catalog: its target is a real kind; the faction owns no model of the kind it stands in for; and
    // the stand-in is live (the faction can field the kind) exactly when some maker sells a hull of the target kind. A stand-in
    // no maker can field is not a defect: the generator drops the kind from the faction's pool and seats it by the any-maker
    // fallback, logged (StationKinds.Choose). Returns how many stand-ins were live, so a caller can see the catalog's state.
    private static int AssertStandInsLive(CultCache cache)
    {
        var live = 0;
        foreach (var kind in cache.GetAll<StationKindData>())
            foreach (var standIn in kind.StandIns)
            {
                var faction = cache.Get(standIn.Faction);
                var owning = cache.RefOf(kind).Key.Value;
                Assert.NotNull(cache.Get(standIn.Kind));
                Assert.DoesNotContain(owning, OracleSoldStationKinds(cache).Where(e => e.maker.Equals(cache.RefOf(faction).Key)).Select(e => e.kind));
                var carried = OracleOfferedKinds(cache).Contains(standIn.Kind.Key.Value);
                var fieldable = StationKinds.FieldableKindsOf(cache, faction).Select(k => k.Key.Value).Contains(owning);
                Assert.Equal(carried, fieldable);
                if (carried) { live++; continue; }
                if (StationKinds.FieldableKindsOf(cache, faction).Length > 0) continue;
                var logged = new List<string>();
                var seated = StationKinds.Choose(cache, faction, "site", logged.Add);
                if (OracleOfferedKinds(cache).Count > 0)
                {
                    Assert.Single(logged);
                    Assert.Contains(seated.Key.Value, OracleOfferedKinds(cache));
                }
            }
        return live;
    }

    [Fact]
    public void ShippedStandInsAreLive()
    {
        using var cache = AetheriaStores.Open(TestCatalog.Repo);
        AssertStandInsLive(cache);
    }

    // The stand-in check is not vacuous: a stand-in passes live when the faction can field the kind it stands in with and has no model
    // of the owning kind; fails when the faction has gained its own model of the owning kind; is dead, with the fallback observed,
    // when no maker sells the target kind (a retarget to a kind no hull carries included); and is caught if its target is no kind.
    [Fact]
    public void StandInCheckCanFail()
    {
        var live = World.Make(this, zenithOffered: false);
        live.Sell(live.Au, "mining");
        live.StandIn("shipyard", live.Au, "mining");
        Assert.Single(live.Cache.GetAll<StationKindData>().SelectMany(kind => kind.StandIns), standIn => live.Cache.Get(standIn.Faction) == live.Au);
        Assert.Equal(1, AssertStandInsLive(live.Cache));

        var retired = World.Make(this, zenithOffered: false);
        retired.Sell(retired.Au, "mining");
        retired.Sell(retired.Au, "shipyard");
        retired.StandIn("shipyard", retired.Au, "mining");
        Assert.ThrowsAny<Exception>(() => AssertStandInsLive(retired.Cache));

        var nowhere = World.Make(this, zenithOffered: false);
        nowhere.Sell(nowhere.Au, "mining");
        nowhere.StandIn("shipyard", nowhere.Lucent, "power");
        Assert.Equal(0, AssertStandInsLive(nowhere.Cache));
        var logged = new List<string>();
        Assert.Equal(Kind("mining").Key, StationKinds.Choose(nowhere.Cache, nowhere.Lucent, "site", logged.Add).Key);
        Assert.Single(logged);

        var missing = World.Make(this, zenithOffered: false);
        missing.Sell(missing.Au, "mining");
        missing.StandIn("shipyard", missing.Zh, "mining");
        var kind = missing.Cache.Get(Kind("shipyard"));
        kind.StandIns[0].Kind = Kind("no-such-kind");
        Assert.ThrowsAny<Exception>(() => AssertStandInsLive(missing.Cache));
    }
}
