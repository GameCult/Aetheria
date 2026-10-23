using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;

// A small writable catalog for the economy tests: one maker that places one station kind (Mill), and builders for commodities,
// gear, hulls, recipes and the outside market. Keys are readable: commodity:<id>, gear:<id>, hull:<id>.
public sealed class EconomyFixture : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aetheria-economy-" + Guid.NewGuid().ToString("N"));
    public string CatalogPath => Path.Combine(_dir, "Aetheria.cc");
    public CultCache Cache { get; private set; }
    public CultRecordRef<Faction> Maker { get; private set; }
    public CultRecordRef<StationKindData> Mill { get; private set; }

    public EconomyFixture()
    {
        Directory.CreateDirectory(_dir);
        Cache = AetheriaStores.Open(CatalogPath, catalogWritable: true, registry: TestCatalog.Registry());
        Cache.Upsert(new VerseGrammar { Revision = 1 });
        Cache.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
        Maker = new CultRecordRef<Faction>(Cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" }).Key);
        Mill = Station("mill", "Mill");
        Sell(Cache.Upsert(typeof(HullData), new HullData { Name = "Yard", HullType = HullType.Station, Kind = Mill, Shape = new Shape(), Price = 1 }, new CultRecordKey("hull:yard")));
    }

    public void Dispose()
    {
        Cache.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    // Closes the catalog and opens the file again, as the game does.
    public void Reopen()
    {
        Cache.FlushAsync().GetAwaiter().GetResult();
        Cache.Dispose();
        Cache = AetheriaStores.Open(CatalogPath, catalogWritable: true, registry: TestCatalog.Registry());
    }

    public static CultRecordRef<ItemData> Item(string key) => new CultRecordRef<ItemData>(new CultRecordKey(key));
    public static string Key(string id) => "commodity:" + id;

    public CultRecordRef<StationKindData> Station(string id, string name)
    {
        var key = StationKindData.KeyOf(id);
        Cache.Upsert(typeof(StationKindData), new StationKindData { Name = name }, key);
        return new CultRecordRef<StationKindData>(key);
    }

    private void Sell(CultRecordKey design) =>
        Cache.Upsert(new FactionProductData { Name = design.Value, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = Maker });

    // A commodity and its recipe. Roles make it a consumer good (a role recipe); Inputs make it a compound commodity.
    public string Good(string id, (string Id, int Count)[] inputs = null, string[] fills = null, int outputCount = 1, float cycle = 1,
        int workforce = 0, (string Id, int Count)[] upkeep = null, string lore = "a good", (string Role, int Count)[] roles = null, CultRecordRef<StationKindData>? kind = null)
    {
        var key = new CultRecordKey(Key(id));
        var roleList = (roles ?? Array.Empty<(string, int)>()).Select(r => new ItemRole { Name = r.Role, Count = r.Count }).ToList();
        if (inputs == null && roles == null) Cache.Upsert(typeof(SimpleCommodityData), new SimpleCommodityData { Name = id }, key);
        else Cache.Upsert(typeof(CompoundCommodityData), new CompoundCommodityData { Name = id, Roles = roleList }, key);
        Cache.Upsert(typeof(RecipeData), new RecipeData
        {
            Output = Item(key.Value),
            OutputCount = outputCount,
            Cycle = cycle,
            Workforce = workforce,
            Inputs = (inputs ?? Array.Empty<(string, int)>()).Select(i => new CommodityCount { Commodity = Item(Key(i.Id)), Count = i.Count }).ToList(),
            Upkeep = (upkeep ?? Array.Empty<(string, int)>()).Select(i => new CommodityCount { Commodity = Item(Key(i.Id)), Count = i.Count }).ToList(),
            StationKind = kind ?? Mill,
            LoreHook = lore,
            Fills = (fills ?? Array.Empty<string>()).ToList()
        }, RecipeData.KeyOf(key));
        return key.Value;
    }

    // Gear taking the given roles, with its recipe, sold by the maker unless offered is false.
    public string Gear(string id, (string Role, int Count)[] roles, bool offered = true, bool recipe = true)
    {
        var key = new CultRecordKey("gear:" + id);
        Cache.Upsert(typeof(GearData), new GearData
        {
            Name = id, Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1,
            Roles = roles.Select(r => new ItemRole { Name = r.Role, Count = r.Count }).ToList()
        }, key);
        Design(key, offered, recipe);
        return key.Value;
    }

    public string Hull(string id, HullType type, (string Role, int Count)[] roles, bool offered = true, bool recipe = true)
    {
        var key = new CultRecordKey("hull:" + id);
        Cache.Upsert(typeof(HullData), new HullData
        {
            Name = id, HullType = type, Shape = new Shape(), Price = 1, Kind = type == HullType.Station ? Mill : default,
            Roles = roles.Select(r => new ItemRole { Name = r.Role, Count = r.Count }).ToList()
        }, key);
        Design(key, offered, recipe);
        return key.Value;
    }

    private void Design(CultRecordKey key, bool offered, bool recipe)
    {
        Cache.Upsert(new FactionProductData
        {
            Name = key.Value, Design = new CultRecordRef<CraftedItemData>(key), Manufacturer = Maker, NotOffered = !offered
        });
        if (recipe)
            Cache.Upsert(typeof(RecipeData), new RecipeData { Output = Item(key.Value), StationKind = Mill }, RecipeData.KeyOf(key));
    }

    public void EditRecipe(string outputKey, Action<RecipeData> edit)
    {
        var key = RecipeData.KeyOf(new CultRecordKey(outputKey));
        var recipe = Cache.Get<RecipeData>(key);
        edit(recipe);
        Cache.Upsert(typeof(RecipeData), recipe, key);
    }

    public void EditItem<T>(string key, Action<T> edit) where T : class
    {
        var item = Cache.Get<T>(new CultRecordKey(key));
        edit(item);
        Cache.Upsert(typeof(T), item, new CultRecordKey(key));
    }

    public void Gate(params (string Commodity, bool Buys, bool Sells)[] rows) =>
        Cache.Upsert(typeof(OutsideMarketData), new OutsideMarketData
        {
            Name = "Gate",
            Rows = rows.Select(r => new OutsideMarketRow { Commodity = Item(r.Commodity), Price = 10, Buys = r.Buys, Sells = r.Sells, RatePerHour = 5 }).ToList()
        }, OutsideMarketData.KeyOf("gate"));

    // ore (raw) -> plate (fills Plate) -> a laser and a skiff that each take two plates: no fault anywhere.
    public static EconomyFixture Healthy()
    {
        var f = new EconomyFixture();
        f.Good("ore");
        f.Good("plate", inputs: new[] { ("ore", 2) }, fills: new[] { "Plate" });
        f.Gear("laser", new[] { ("Plate", 2) });
        f.Hull("skiff", HullType.Ship, new[] { ("Plate", 2) });
        return f;
    }
}

internal static class EconomyUpserts
{
    // A write under a readable key: the cache's own keyed write, completed synchronously.
    public static CultRecordKey Upsert(this CultCache cache, Type type, object document, CultRecordKey key) =>
        cache.UpsertAsync(type, document, key).GetAwaiter().GetResult();
}
