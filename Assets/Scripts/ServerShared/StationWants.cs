using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// What a station buys (aetheria-living-world:ruling:installed-gear-decides-what-a-station-buys): the goods its installed
// recipe facilities draw, and, when it carries a silo, the goods of the haul orders that start at its orbit
// (ruling transit-depots-buy-for-onward-haul). Derived on every call from the station's gear and the catalog, stored nowhere.
// StationServices.TrySellPart is gated by it and limited by the bays' room.
//
// Where goods land and how much of a product a station holds (cut facility-output-room). A station with a silo keeps what its
// facilities make there, and each product holds an even share of the silo: Allocation, floor(silo cells / products) x the
// product's MaxStack. A recipe starts only when its product has room (CanStart), and a station draws an ore only while some recipe
// that takes it can start, so a full product stops its ores being bought while the station keeps running its other recipes. Prior
// art: X4's per-ware storage allocation (a full ware stops that production, the station keeps running the rest) and Factorio's
// per-product output slot. A station with no silo has no bound: outputs land in any bay and Room happens at the put.
public static class StationWants
{
    public static IReadOnlyCollection<CultRecordKey> Of(Entity station)
    {
        var wants = Drawn(station);
        wants.UnionWith(Ordered(station));
        return wants;
    }

    // The bays a bought or delivered good may land in: the non-silo bays when an installed recipe facility draws it (a drawn good
    // is stock, never silo content), the silo bays when the station wants it through a haul order (a transit depot holds goods in
    // its silo, never in its stock bay). The one owner of where a good lands; none when the station does not want it.
    public static IEnumerable<EquippedCargoBay> Bays(Entity station, CultRecordKey good)
    {
        var bays = Enumerable.Empty<EquippedCargoBay>();
        if (Drawn(station).Contains(good)) bays = bays.Concat(station.CargoBays.Where(bay => !(bay.Data is SiloData)));
        if (Ordered(station).Contains(good)) bays = bays.Concat(station.CargoBays.Where(bay => bay.Data is SiloData));
        return bays;
    }

    // The bays a facility's output may land in: the silo bays when the station has one, else every bay. The one owner of where
    // a facility output lands.
    public static IEnumerable<EquippedCargoBay> OutputBays(Entity station, CultRecordKey good) =>
        station.CargoBays.Any(bay => bay.Data is SiloData) ? station.CargoBays.Where(bay => bay.Data is SiloData) : station.CargoBays;

    // The units of a product the station may hold in its output bays: the silo's cells divided evenly among the distinct outputs of
    // its recipe facilities, each share worth that many full stacks of the product. Null (no bound) without a silo or a recipe
    // facility. Derived on every call, stored nowhere.
    public static int? Allocation(Entity station, CultRecordKey product)
    {
        var silos = station.CargoBays.Where(bay => bay.Data is SiloData).ToArray();
        if (silos.Length == 0) return null;
        var products = FacilityRecipes(station).Select(recipe => recipe.Output.Key).Distinct().Count();
        if (products == 0) return null;
        var cells = silos.Sum(bay => bay.Data.InteriorShape.Coordinates.Length);
        var stack = station.ItemManager.ItemData.Get<SimpleCommodityData>(product)?.MaxStack ?? 0;
        return cells / products * stack;
    }

    // Whether the recipe's product has room for one more cycle's output.
    public static bool CanStart(Entity station, RecipeData recipe)
    {
        var allocation = Allocation(station, recipe.Output.Key);
        return allocation == null || OutputBays(station, recipe.Output.Key).Sum(bay => bay.Count(recipe.Output.Key)) + recipe.OutputCount <= allocation;
    }

    private static IEnumerable<RecipeData> FacilityRecipes(Entity station)
    {
        var catalog = station.ItemManager.ItemData;
        foreach (var equipped in station.Equipment)
        {
            var design = station.ItemManager.GetData(equipped.EquippableItem);
            if (!StationKinds.IsRecipeFacility(design)) continue;
            foreach (var recipe in Production.RunList(catalog, catalog.RefOf(design).Key)) yield return recipe;
        }
    }

    private static HashSet<CultRecordKey> Drawn(Entity station)
    {
        var catalog = station.ItemManager.ItemData;
        var wants = new HashSet<CultRecordKey>();
        foreach (var recipe in FacilityRecipes(station).Where(recipe => CanStart(station, recipe)))
            foreach (var draw in Production.Draws(catalog, recipe))
                wants.Add(draw);
        return wants;
    }

    private static IEnumerable<CultRecordKey> Ordered(Entity station)
    {
        if (!(station is OrbitalEntity { OrbitData: var orbit }) || !orbit.IsSet() || !station.CargoBays.Any(bay => bay.Data is SiloData))
            return Enumerable.Empty<CultRecordKey>();
        return station.ItemManager.ItemData.GetAll<HaulOrderData>().Where(order => order.From.Key.Equals(orbit))
            .SelectMany(order => order.Goods.Select(good => good.Key)).ToArray();
    }
}

// A standing haul order: goods that move from one station's orbit to another's, keyed haul-order:<id>. A run document: saved
// with the run, cleared, replaced and discarded with it. Fixed until offices issue orders (ruling transit-depots-buy-for-onward-haul);
// From and To are the stations' orbit records until installations give a station a key of its own.
[CultDocument("aetheria.haulorderdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class HaulOrderData
{
    [JsonProperty("from"), Key(0)] public CultRecordRef<OrbitData> From;
    [JsonProperty("to"), Key(1)] public CultRecordRef<OrbitData> To;
    [JsonProperty("goods"), Key(2)] public List<CultRecordRef<ItemData>> Goods = new List<CultRecordRef<ItemData>>();
    // What stood this order up (scenario:<id>), so a reader can tell a staged order from one an office issues.
    [JsonProperty("commitment"), Key(3)] public string Commitment;

    public static CultRecordKey KeyOf(string id) => new CultRecordKey("haul-order:" + id);
}
