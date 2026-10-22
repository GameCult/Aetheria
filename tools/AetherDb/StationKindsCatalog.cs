using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;

// Authors the catalog's one list of station kinds (aetheria-living-world, cut station-kind-datum) and gives Zenith, the
// one station hull in the repo, its ruled kind (ruling zenith-is-a-trade-hub: provisional, one edit here to change).
// The 17 kinds are the station-round bench's economic roles; descriptions are its produces/consumes column. A kind that
// already has a record is kept as it is and Zenith keeps a kind it already has, so a rerun that finds nothing to author
// writes nothing. Dry run unless apply.
public static class StationKindsCatalog
{
    public const string ZenithKind = "trade-hub";

    private static readonly (string id, string name, string description)[] Table =
    {
        ("mining", "Mining Station", "Extracts ore (Rock, New Metals, Gold, Uranium, Potassium) from bodies and belts; consumes tools and food."),
        ("volatiles", "Volatiles Station", "Skims Hydrogen, Methane and Water from gas giants and ice; consumes tools."),
        ("agriculture", "Agriculture Station", "Grows Organics (Algae, Fungi, Bacteria, Animal Products) from Water and Potassium; feeds residential."),
        ("processing", "Processing Station", "Refines ore and volatiles into Metals, Alloys and Compounds."),
        ("fabrication", "Fabrication Station", "Turns Alloys and Compounds into Manufacturing compounds, Assemblies, gear, Ammo and consumer goods."),
        ("shipyard", "Shipyard", "Assembles hulls from Assemblies and Alloys."),
        ("depot", "Depot", "Stores and dispatches stock; loads haulers and tenders."),
        ("trade-hub", "Trade Hub", "Exchanges every bloc's goods between haulers and takes a cut; produces nothing."),
        ("residential", "Residential Station", "A bloom: consumes food, water, Wearables, Consumables and Luxuries; supplies crews."),
        ("habitat-support", "Habitat Support Station", "Air, water recycling, life support and medicine for nearby habitats; consumes Bacteria, Water, Compounds."),
        ("urban", "Urban Station", "Dense city and services: market, media, repair; consumes luxuries and food."),
        ("administrative", "Administrative Station", "Issues quotas, contracts and security; consumes luxuries."),
        ("power", "Power Station", "Makes reactor fuel, reservoirs and capacitors from Uranium and Hydrogen."),
        ("research", "Research Station", "Certification, compute, prototypes; consumes Assemblies."),
        ("salvage", "Salvage Station", "Breaks wrecks into Metals and second-hand gear."),
        ("black-market", "Black Market", "Buys anything, fences stolen cargo."),
        ("waystation", "Waystation", "Fuel, repair and medicine along a corridor."),
    };

    // The catalog opened without AetheriaStores.Open's checks: the catalog this command first runs on holds a station hull with no
    // kind, which StationKinds.Validate refuses, and this command is what gives it one.
    public static CultCache OpenCatalog(bool apply)
    {
        var cache = new CultCache();
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), !apply), AetheriaStores.CatalogTypes);
        return cache;
    }

    public static int Run(CultCache cache, bool apply, TextWriter output)
    {
        var held = cache.GetAll<StationKindData>().Select(kind => cache.RefOf(kind).Key.Value).ToHashSet(StringComparer.Ordinal);
        var missing = Table.Where(row => !held.Contains(StationKindData.KeyOf(row.id).Value)).ToArray();
        foreach (var row in Table)
            output.WriteLine($"{row.id,-16} {(missing.Any(m => m.id == row.id) ? "authors" : "kept")}");

        var zenith = cache.GetAll<HullData>().Where(h => h.HullType == HullType.Station && h.Name == "Zenith").SingleOrDefault();
        var setZenith = zenith != null && !zenith.Kind.IsSet();
        output.WriteLine(zenith == null ? "Zenith: no such hull, kind not set"
            : setZenith ? $"Zenith: kind {ZenithKind}" : "Zenith: already has a kind, kept");

        if (!apply)
        {
            output.WriteLine("\nDry run. Pass \"apply\" to author them.");
            return 0;
        }
        if (missing.Length == 0 && !setZenith)
        {
            output.WriteLine("Nothing to author.");
            return 0;
        }

        cache.Commit(batch =>
        {
            foreach (var (id, name, description) in missing)
                batch.Upsert(typeof(StationKindData), new StationKindData { Name = name, Description = description }, StationKindData.KeyOf(id));
            if (setZenith)
            {
                zenith.Kind = new CultRecordRef<StationKindData>(StationKindData.KeyOf(ZenithKind));
                batch.Upsert(typeof(HullData), zenith, cache.RefOf(zenith).Key);
            }
        });
        output.WriteLine($"\nAuthored {missing.Length} station kinds{(setZenith ? " and Zenith's kind" : "")} in Aetheria.cc");
        return 0;
    }
}
