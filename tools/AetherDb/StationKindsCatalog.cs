using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Caching;
using int2 = CultMath.int2;

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
        ("mining", "Mining Station", "Extracts ore from belts: Regolith, Nickel-Iron and Troilite in common ground; Magnetite and Pentlandite in carbonaceous rock; Ilmenite and Anorthite in basalt; and only where the ground holds them Sylvite, Orthoclase, Chalcopyrite, Sperrylite, Native Copper, Native Gold, Isoferroplatinum, Uraninite, Brannerite, Monazite, Columbite and Wolframite. Cuts Diamond from deep cores; consumes tools and food."),
        ("volatiles", "Volatiles Station", "Skims Methane, Ammonia and Water from gas giants and ice; consumes tools."),
        ("agriculture", "Agriculture Station", "Grows Algae from Water and Potassium; feeds residential."),
        ("processing", "Processing Station", "Refines ore and volatiles into Metals, Alloys and Compounds."),
        ("fabrication", "Fabrication Station", "Turns Alloys and Compounds into Manufacturing compounds, Assemblies, gear, Ammo and consumer goods."),
        ("shipyard", "Shipyard", "Assembles hulls from Assemblies and Alloys."),
        ("depot", "Depot", "Stores and dispatches stock; loads haulers and tenders."),
        ("trade-hub", "Trade Hub", "Exchanges every bloc's goods between haulers and takes a cut; produces nothing."),
        ("residential", "Residential Station", "A bloom: consumes food, water, Wearables, Consumables and Luxuries; supplies crews."),
        ("habitat-support", "Habitat Support Station", "Air, water recycling, life support and medicine for nearby habitats; consumes Water and Compounds."),
        ("urban", "Urban Station", "Dense city and services: market, media, repair; consumes luxuries and food."),
        ("administrative", "Administrative Station", "Issues quotas, contracts and security; consumes luxuries."),
        ("power", "Power Station", "Generates and distributes power for nearby stations."),
        ("research", "Research Station", "Certification, compute, prototypes; consumes Assemblies."),
        ("salvage", "Salvage Station", "Breaks wrecks into Metals and second-hand gear."),
        ("black-market", "Black Market", "Buys anything, fences stolen cargo."),
        ("waystation", "Waystation", "Fuel, repair and medicine along a corridor."),
    };

    // A kind's authored description: the one text a fresh catalog gets and an economy batch restates.
    public static string DescriptionOf(string id) => Table.Single(row => row.id == id).description;

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

        foreach (var hull in cache.GetAll<HullData>().Where(h => h.HullType == HullType.Station && !h.Kind.IsSet() && h != zenith).OrderBy(h => h.Name, StringComparer.Ordinal))
            output.WriteLine($"Station hull '{hull.Name}' has no kind; set one with: station-kinds hull \"{hull.Name}\" <kind> apply");

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

    // Gives a station hull its kind: the way to repair a hull the catalog holds without one. Dry run unless apply.
    public static int SetHullKind(CultCache cache, string hullName, string kindId, bool apply, TextWriter output)
    {
        var hull = cache.GetAll<HullData>().FirstOrDefault(h => h.HullType == HullType.Station && h.Name == hullName);
        if (hull == null)
        {
            output.WriteLine($"The catalog holds no station hull named '{hullName}'.");
            return 1;
        }
        var kind = new CultRecordRef<StationKindData>(StationKindData.KeyOf(kindId));
        if (cache.Get(kind) == null)
        {
            output.WriteLine($"The catalog holds no station kind '{kindId}'.");
            return 1;
        }
        output.WriteLine($"{hull.Name}: kind {kindId}");
        if (!apply)
        {
            output.WriteLine("\nDry run. Pass \"apply\" to set it.");
            return 0;
        }
        hull.Kind = kind;
        cache.Upsert(hull);
        output.WriteLine($"\nSet {hull.Name}'s kind in Aetheria.cc");
        return 0;
    }

    public const string FacilitiesUsage = "station-kinds facilities \"<hull name>\" <x>,<y>:<footprint>... [apply]";

    // Sets a shipped station hull's whole set of Facility rows (ruling station-facilities-on-typed-mounts): the one writer of a
    // shipped hull's Facility rows (a package's go through ship-authoring push). A row is <x>,<y>:<footprint>, the footprint as
    // package_hull.md writes it (rows of # and . joined by /, the top row highest y), the cell its lowest-x, lowest-y cell. The
    // rows replace the hull's Facility rows exactly, mount ids Fa.1, Fa.2... in argument order; every other row is kept untouched.
    // Refuses a hull that is not a Station, a malformed row, a cell outside the interior and a row over another row. Prints the
    // designs that take each Facility row before and after. Rows equal to the hull's write nothing. Dry run unless apply.
    public static int SetFacilityRows(CultCache cache, string hullName, string[] rows, bool apply, TextWriter output)
    {
        var named = cache.GetAll<HullData>().Where(h => h.Name == hullName).ToArray();
        if (named.Length != 1)
        {
            output.WriteLine($"The catalog holds {named.Length} hulls named '{hullName}'; exactly one is required.");
            return 1;
        }
        var hull = named[0];
        if (hull.HullType != HullType.Station)
        {
            output.WriteLine($"Hull '{hull.Name}' is a {hull.HullType}; only a station hull takes facilities.");
            return 1;
        }
        var wanted = new List<HardpointData>();
        var claimed = new HashSet<(int, int)>();
        foreach (var other in hull.Hardpoints.Where(row => row.Type != HardpointType.Facility))
            foreach (var cell in other.Shape.Coordinates) claimed.Add((other.Position.x + cell.x, other.Position.y + cell.y));
        foreach (var text in rows)
        {
            var colon = text.IndexOf(':');
            var at = colon < 0 ? null : text.Substring(0, colon).Split(',');
            if (at == null || at.Length != 2 || !int.TryParse(at[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x) || !int.TryParse(at[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y))
            {
                output.WriteLine($"'{text}' is not a row: {FacilitiesUsage}");
                return 1;
            }
            Shape shape;
            try { shape = ShipAuthoringPush.ParseFootprint(text.Substring(colon + 1), $"Row '{text}'"); }
            catch (InvalidOperationException e)
            {
                output.WriteLine(e.Message);
                return 1;
            }
            foreach (var cell in shape.Coordinates)
            {
                var taken = (x + cell.x, y + cell.y);
                if (x + cell.x >= hull.Shape.Width || y + cell.y >= hull.Shape.Height || !hull.InteriorCells[new int2(taken.Item1, taken.Item2)])
                {
                    output.WriteLine($"Row '{text}' puts cell ({taken.Item1},{taken.Item2}) outside {hull.Name}'s interior.");
                    return 1;
                }
                if (!claimed.Add(taken))
                {
                    output.WriteLine($"Row '{text}' overlaps another row at cell ({taken.Item1},{taken.Item2}).");
                    return 1;
                }
            }
            wanted.Add(new HardpointData { Type = HardpointType.Facility, Position = new int2(x, y), Shape = shape, Transform = "Fa." + (wanted.Count + 1) });
        }
        var held = hull.Hardpoints.Where(row => row.Type == HardpointType.Facility).ToArray();
        var designs = cache.GetAll<EquippableItemData>().ToArray();
        string Describe(HardpointData row) => $"{row.Transform} ({row.Position.x},{row.Position.y}) {ShipAuthoringPush.Footprint(row.Shape)} [{string.Join(", ", designs.Where(row.Takes).Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal))}]";
        foreach (var row in held) output.WriteLine($"{hull.Name} held {Describe(row)}");
        foreach (var row in wanted) output.WriteLine($"{hull.Name} sets {Describe(row)}");
        if (held.Length == 0 && wanted.Count == 0 || held.Length == wanted.Count && held.Zip(wanted, (a, b) => Same(a, b)).All(same => same))
        {
            output.WriteLine("No change.");
            return 0;
        }
        if (!apply)
        {
            output.WriteLine("\nDry run. Pass \"apply\" to set them.");
            return 0;
        }
        hull.Hardpoints = hull.Hardpoints.Where(row => row.Type != HardpointType.Facility).Concat(wanted).ToList();
        cache.Upsert(hull);
        output.WriteLine($"\nSet {hull.Name}'s Facility rows in Aetheria.cc");
        return 0;
    }

    private static bool Same(HardpointData a, HardpointData b) =>
        a.Transform == b.Transform && a.Position.x == b.Position.x && a.Position.y == b.Position.y && a.Rotation == b.Rotation && a.Armor == b.Armor
        && a.FiringArc == b.FiringArc && ShipAuthoringPush.Footprint(a.Shape) == ShipAuthoringPush.Footprint(b.Shape);
}
