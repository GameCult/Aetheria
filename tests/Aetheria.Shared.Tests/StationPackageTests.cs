using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Station packages (aetheria-release, cut station-packages): a package hull of type Station enters the game as data through
// the one compose path, names its kind (HullData.Kind, written by ship-authoring kind) and is drawn for that kind by
// StationKinds and LoadoutGenerator. The first is GameData/Mods/aetheria.arsenal-depot, the Zhestokost Arsenal Depot, whose
// offer makes every depot stand-in live. The tests read the owners (ruling census-tests-read-the-owners).
public sealed class StationPackageTests : IDisposable
{
    private const string Id = "aetheria.arsenal-depot";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stationpackage-" + Guid.NewGuid().ToString("N"));
    private int _caches;

    public StationPackageTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private static CultRecordRef<StationKindData> Kind(string id) => new CultRecordRef<StationKindData>(StationKindData.KeyOf(id));

    // The booted catalog (TestCatalog.Repo: shipped catalog with the first-party packages composed in), a private copy so a test may edit it.
    private CultCache Catalog()
    {
        var path = Path.Combine(_root, $"catalog{_caches++}.cc");
        File.Copy(TestCatalog.Repo, path);
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(path), AetheriaStores.CatalogTypes);
        return cache;
    }

    private static HullData Depot(CultCache cache) => cache.Get(new CultRecordRef<HullData>(ShipModCatalog.HullKey(Id)));

    // What stands in the way of this maker's generator drawing this hull and fitting a docking bay and a heater, or null.
    private static string Fault(CultCache cache, Faction maker, HullData hull)
    {
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var random = new CultMath.Random(1u);
        var generator = new LoadoutGenerator(ref random, items, null, null, maker, .5f);
        OrbitalEntityPack pack;
        try { pack = generator.GenerateStationLoadout(hull.Kind); }
        catch (InvalidLoadoutException error) { return $"{hull.Name}: {error.Message}"; }
        if (pack == null) return $"{hull.Name}: no station was drawn for its kind.";
        var entity = EntitySerializer.Unpack(items, null, pack);
        if (!entity.HullData.Kind.Key.Equals(hull.Kind.Key)) return $"{hull.Name}: the draw gave a {entity.HullData.Name}, not a hull of its kind.";
        if (pack.DockingBays.Length == 0) return $"{hull.Name}: no docking bay was fitted.";
        if (!entity.Equipment.Select(item => item.Data).OfType<GearData>().Any(gear => gear.Behaviors.Any(behavior => behavior is HeatData)))
            return $"{hull.Name}: no heater was fitted.";
        return null;
    }

    [Fact]
    public void TheDepotIsAnOfferedStationOfKindDepot()
    {
        using var cache = Catalog();
        var hull = Depot(cache);
        Assert.NotNull(hull);
        Assert.Equal(("Arsenal Depot", HullType.Station), (hull.Name, hull.HullType));
        Assert.Equal(StationKindData.KeyOf("depot"), hull.Kind.Key);
        Assert.True(hull.Visual.IsSet());
        var product = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(ShipModCatalog.HullKey(Id)));
        Assert.Equal("Zhestokost", cache.Get(product.Manufacturer).Name);
        Assert.True(LoadoutGenerator.IsDrawable(cache, product, hull));
    }

    [Fact]
    public void EveryDepotStandInIsLive()
    {
        using var cache = Catalog();
        var depot = Kind("depot");
        var standIns = cache.GetAll<StationKindData>()
            .SelectMany(kind => kind.StandIns.Where(entry => entry.Kind.Key.Equals(depot.Key)).Select(entry => (kind: cache.RefOf(kind), faction: cache.Get(entry.Faction))))
            .ToArray();
        Assert.NotEmpty(standIns);
        foreach (var (kind, faction) in standIns)
        {
            Assert.Equal(depot.Key, StationKinds.HullKindFor(cache, faction, kind).Key);
            Assert.Contains(kind.Key, StationKinds.FieldableKindsOf(cache, faction).Select(fieldable => fieldable.Key));
        }
    }

    [Fact]
    public void EveryStationPackageGeneratesItsKind()
    {
        using var cache = Catalog();
        var packages = cache.GetAll<HullData>().Where(hull => hull.HullType == HullType.Station && hull.Visual.IsSet()).ToArray();
        Assert.Contains(Depot(cache), packages);
        foreach (var hull in packages)
        {
            var makers = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(cache.RefOf(hull).Key)).Select(p => cache.Get(p.Manufacturer));
            foreach (var maker in makers) Assert.Null(Fault(cache, maker, hull));
        }
    }

    // The generation check is not vacuous: a station package too small to take a docking bay fails it, naming the package.
    [Fact]
    public void AStationThatTakesNoDockingBayFails()
    {
        using var cache = Catalog();
        var depot = Depot(cache);
        var depotProduct = cache.GetAll<FactionProductData>().Single(p => p.Design.Key.Equals(cache.RefOf(depot).Key));
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var tiny = MessagePackSerializer.Deserialize<HullData>(MessagePackSerializer.Serialize(depot, options), options);
        tiny.Name = "Tiny Depot";
        tiny.Shape = new Shape(3, 3);
        foreach (var cell in tiny.Shape.AllCoordinates) tiny.Shape[cell] = true;
        tiny.Hardpoints.Clear();
        var tinyRef = cache.Upsert(tiny);
        var tinyProduct = MessagePackSerializer.Deserialize<FactionProductData>(MessagePackSerializer.Serialize(depotProduct, options), options);
        tinyProduct.Name = "Tiny Depot";
        tinyProduct.Design = new CultRecordRef<CraftedItemData>(tinyRef.Key);
        cache.Upsert(tinyProduct);
        depotProduct.NotOffered = true;
        cache.Upsert(depotProduct);

        var fault = Fault(cache, cache.Get(tinyProduct.Manufacturer), tiny);
        Assert.NotNull(fault);
        Assert.Contains("Tiny Depot", fault);
        Assert.Contains("docking bay", fault);
    }
}
