using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// How a good is made: one authored record per output, keyed recipe:<output key>, so a second recipe for one output replaces the
// first. A commodity's recipe takes named commodities (Inputs) and roles (RoleInputs), each role filled by any commodity whose
// recipe Fills it, and says which roles it can fill. A role recipe (the output is a design with roles, a ship hull, a piece of
// gear, a consumer good) restates nothing of the design: its role inputs are the design's Roles and their counts. Station and
// Turret hulls take no recipe.
[CultDocument("aetheria.recipedata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class RecipeData
{
    [JsonProperty("output"), Key(0)]
    public CultRecordRef<ItemData> Output;

    [JsonProperty("outputCount"), Key(1)]
    public int OutputCount = 1;

    // Commodities one cycle consumes. Empty on a raw good and on a role recipe.
    [JsonProperty("inputs"), Key(2)]
    public List<CommodityCount> Inputs = new List<CommodityCount>();

    // Seconds of sim time for one cycle.
    [JsonProperty("cycle"), Key(3)]
    public float Cycle = 1;

    // Crew per running facility; no kind until workforce kinds exist.
    [JsonProperty("workforce"), Key(4)]
    public int Workforce;

    // Commodities one cycle uses up on the side (power, coolant), per cycle.
    [JsonProperty("upkeep"), Key(5)]
    public List<CommodityCount> Upkeep = new List<CommodityCount>();

    // The kind of station that runs this recipe: the kind whose Facilities list Facility. A checked mirror of Facility, no longer an
    // owner; follow-up recipe-station-kind-derived deletes it once every reader reads the facility.
    [JsonProperty("stationKind"), Key(6)]
    public CultRecordRef<StationKindData> StationKind;

    // Why this good exists in the world, in the operator's reading. Presence is checked, never its meaning.
    [JsonProperty("loreHook"), Key(7)]
    public string LoreHook;

    // The roles of a design this commodity can fill. Only a commodity's recipe has any.
    [JsonProperty("fills"), Key(8)]
    public List<string> Fills = new List<string>();

    // Roles one cycle consumes, any filler will do and the facility picks (ruling ores-fill-metal-roles). Empty on a role recipe;
    // records written before key 9 read it as empty.
    [JsonProperty("roleInputs"), Key(9)]
    public List<ItemRole> RoleInputs = new List<ItemRole>();

    // The facility gear design that runs this recipe: a GearData of HardpointType Facility holding a FacilityData. Records written
    // before key 10 read it unset.
    [JsonProperty("facility"), Key(10)]
    public CultRecordRef<ItemData> Facility;

    public static CultRecordKey KeyOf(CultRecordKey outputKey) => new CultRecordKey("recipe:" + outputKey.Value);
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class CommodityCount
{
    [JsonProperty("commodity"), Key(0)]
    public CultRecordRef<ItemData> Commodity;

    [JsonProperty("count"), Key(1)]
    public int Count = 1;

    // Element identity: minted by the cache on write; "" means unset.
    [Key(2)] [CultElementId] public string Id = "";
}

// A region gate's trade with the world beyond it: what the outside buys and sells, at what price and at most how fast.
// The union of all records is the outside; keyed outside-market:<id>.
[CultDocument("aetheria.outsidemarketdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class OutsideMarketData
{
    [CultName, JsonProperty("name"), Key(0)]
    public string Name;

    [JsonProperty("rows"), Key(1)]
    public List<OutsideMarketRow> Rows = new List<OutsideMarketRow>();

    public static CultRecordKey KeyOf(string id) => new CultRecordKey("outside-market:" + id);
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class OutsideMarketRow
{
    [JsonProperty("commodity"), Key(0)]
    public CultRecordRef<ItemData> Commodity;

    [JsonProperty("price"), Key(1)]
    public int Price;

    [JsonProperty("buys"), Key(2)]
    public bool Buys;

    [JsonProperty("sells"), Key(3)]
    public bool Sells;

    [JsonProperty("ratePerHour"), Key(4)]
    public float RatePerHour;

    // Element identity: minted by the cache on write; "" means unset.
    [Key(5)] [CultElementId] public string Id = "";
}

// The one reader of recipes: which recipe makes a good, a recipe's role slots (RoleInputsOf), which commodities fill a role, which
// recipes a facility design runs (RunOn), and what a good is by its recipe.
public static class Recipes
{
    public static RecipeData RecipeOf(CultCache catalog, ItemData item) =>
        item == null ? null : catalog.Get<RecipeData>(RecipeData.KeyOf(catalog.RefOf(item).Key));

    public static bool IsCommodity(ItemData item) => item is SimpleCommodityData || item is CompoundCommodityData;

    public static IReadOnlyList<ItemRole> RolesOf(ItemData item) =>
        (item as CraftedItemData)?.Roles ?? new List<ItemRole>();

    // A recipe whose output is a design with roles: a part list rather than a list of inputs.
    public static bool IsRoleRecipe(CultCache catalog, RecipeData recipe)
    {
        var output = catalog.Get(recipe.Output);
        return output is CraftedItemData && RolesOf(output).Count > 0;
    }

    // The one reader of a recipe's role slots: the design's Roles for a role recipe, else the recipe's RoleInputs.
    public static IReadOnlyList<ItemRole> RoleInputsOf(CultCache catalog, RecipeData recipe) =>
        IsRoleRecipe(catalog, recipe) ? RolesOf(catalog.Get(recipe.Output)) : (IReadOnlyList<ItemRole>) recipe.RoleInputs ?? new List<ItemRole>();

    // Raw: takes no commodity and no role. Everything else is processed. Derived, never stored.
    public static bool IsRaw(CultCache catalog, RecipeData recipe) => recipe.Inputs.Count == 0 && RoleInputsOf(catalog, recipe).Count == 0;

    // A commodity made by a role recipe: it is assembled from the same components gear is.
    public static bool IsConsumerGood(CultCache catalog, RecipeData recipe) =>
        IsCommodity(catalog.Get(recipe.Output)) && IsRoleRecipe(catalog, recipe);

    // The commodities whose recipes Fill the role, ordered by key so a choice never depends on store order. Read from the catalog's
    // filler index, which is built on the first read and rebuilt on the first read after a recipe or commodity changes.
    public static CultRecordKey[] Fillers(CultCache catalog, string role) => FillerIndexes.GetValue(catalog, cache => new FillerIndex(cache)).Of(role);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CultCache, FillerIndex> FillerIndexes = new();

    // Role to its fillers, derived from the recipes and owned by the catalog it was built from (one index per catalog, dropped with it).
    // The catalog is read-only while a game runs, so a build serves every read; a write to a recipe or a commodity design (a test, an
    // authoring batch) marks it stale, and the next read rebuilds it. It is right only for recipes written through the catalog: a
    // document edited in place and never written back is outside the catalog's admission, so the index cannot see it and the scan it
    // replaced could. Write an edited recipe back (every authoring path and fixture does).
    private sealed class FillerIndex
    {
        private readonly CultCache _catalog;
        private readonly object _gate = new object();
        private Dictionary<string, CultRecordKey[]> _byRole;
        private volatile bool _stale = true;

        public FillerIndex(CultCache catalog)
        {
            _catalog = catalog;
            // The journal runs under the catalog's own gate and must wait for nothing, so it only raises the flag.
            catalog.AddJournal(changes =>
            {
                foreach (var change in changes)
                    if (Matters(change.Document) || Matters(change.PreviousDocument)) _stale = true;
            });
        }

        private static bool Matters(object document) => document is RecipeData || document is SimpleCommodityData || document is CompoundCommodityData;

        public CultRecordKey[] Of(string role)
        {
            lock (_gate)
            {
                if (_stale)
                {
                    _stale = false;
                    _byRole = Build();
                }
                return _byRole.TryGetValue(role, out var fillers) ? fillers : Array.Empty<CultRecordKey>();
            }
        }

        private Dictionary<string, CultRecordKey[]> Build() =>
            _catalog.GetAll<RecipeData>()
                .Where(recipe => recipe.Fills != null && IsCommodity(_catalog.Get(recipe.Output)))
                .SelectMany(recipe => recipe.Fills.Where(role => role != null).Distinct().Select(role => (role, recipe.Output.Key)))
                .GroupBy(pair => pair.role)
                .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).OrderBy(key => key.Value, StringComparer.Ordinal).ToArray());
    }

    // The recipes a facility design runs, ordered by recipe key so a choice never depends on store order. The one reader of which
    // recipes a facility runs; RecipeData.Facility owns it and nothing is stored on the facility.
    public static RecipeData[] RunOn(CultCache catalog, CultRecordKey facilityDesign) =>
        catalog.GetAll<RecipeData>()
            .Where(recipe => recipe.Facility.IsSet() && recipe.Facility.Key.Equals(facilityDesign))
            .OrderBy(recipe => RecipeData.KeyOf(recipe.Output.Key).Value, StringComparer.Ordinal)
            .ToArray();
}
