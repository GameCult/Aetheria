using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// How a good is made: one authored record per output, keyed recipe:<output key>, so a second recipe for one output replaces the
// first. A commodity's recipe says what it takes and which roles it can fill. A role recipe (the output is a design with roles,
// a ship hull, a piece of gear, a consumer good) restates nothing of the design: its inputs are derived from the design's Roles and
// their counts, each filled by any commodity whose recipe Fills the role. Station and Turret hulls take no recipe.
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

    // The kind of station that runs this recipe.
    [JsonProperty("stationKind"), Key(6)]
    public CultRecordRef<StationKindData> StationKind;

    // Why this good exists in the world, in the operator's reading. Presence is checked, never its meaning.
    [JsonProperty("loreHook"), Key(7)]
    public string LoreHook;

    // The roles of a design this commodity can fill. Only a commodity's recipe has any.
    [JsonProperty("fills"), Key(8)]
    public List<string> Fills = new List<string>();

    public static CultRecordKey KeyOf(CultRecordKey outputKey) => new CultRecordKey("recipe:" + outputKey.Value);
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class CommodityCount
{
    [JsonProperty("commodity"), Key(0)]
    public CultRecordRef<ItemData> Commodity;

    [JsonProperty("count"), Key(1)]
    public int Count = 1;
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
}

// The one reader of recipes: which recipe makes a good, which commodities fill a role, and what a good is by its recipe.
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

    // Raw: no inputs and not a role recipe. Everything else is processed. Derived, never stored.
    public static bool IsRaw(CultCache catalog, RecipeData recipe) => recipe.Inputs.Count == 0 && !IsRoleRecipe(catalog, recipe);

    // A commodity made by a role recipe: it is assembled from the same components gear is.
    public static bool IsConsumerGood(CultCache catalog, RecipeData recipe) =>
        IsCommodity(catalog.Get(recipe.Output)) && IsRoleRecipe(catalog, recipe);

    // The commodities whose recipes Fill the role, ordered by key so a choice never depends on store order.
    public static CultRecordKey[] Fillers(CultCache catalog, string role) =>
        catalog.GetAll<RecipeData>()
            .Where(recipe => recipe.Fills != null && recipe.Fills.Contains(role) && IsCommodity(catalog.Get(recipe.Output)))
            .Select(recipe => recipe.Output.Key)
            .OrderBy(key => key.Value, StringComparer.Ordinal)
            .ToArray();
}
