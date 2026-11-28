using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// What a station buys (aetheria-living-world:ruling:installed-gear-decides-what-a-station-buys): the goods its installed
// recipe facilities draw, and, when it carries a silo, the goods of the haul orders that start at its orbit
// (ruling transit-depots-buy-for-onward-haul). Derived on every call from the station's gear and the catalog, stored nowhere.
// StationServices.TrySellPart is gated by it and limited by the bays' room.
public static class StationWants
{
    public static IReadOnlyCollection<CultRecordKey> Of(Entity station)
    {
        var catalog = station.ItemManager.ItemData;
        var wants = new HashSet<CultRecordKey>();
        foreach (var equipped in station.Equipment)
        {
            var design = station.ItemManager.GetData(equipped.EquippableItem);
            if (!StationKinds.IsRecipeFacility(design)) continue;
            foreach (var recipe in Production.RunList(catalog, catalog.RefOf(design).Key))
                foreach (var draw in Production.Draws(catalog, recipe))
                    wants.Add(draw);
        }
        if (station is OrbitalEntity { OrbitData: var orbit } && orbit.IsSet() && station.CargoBays.Any(bay => bay.Data is SiloData))
            foreach (var order in catalog.GetAll<HaulOrderData>().Where(order => order.From.Key.Equals(orbit)))
                foreach (var good in order.Goods)
                    wants.Add(good.Key);
        return wants;
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
