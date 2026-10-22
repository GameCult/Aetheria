using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// What a station is for: one authored catalog record per kind (mining, trade-hub, shipyard...). The catalog's list of
// these records is the one list of station kinds. A kind names no hull: the hull names its kind (HullData.Kind), and
// which hull a faction builds for a kind is derived from the products that faction makes (StationKinds), so a package can
// add a faction's model of a kind without editing any kind record. Records are keyed station-kind:<id>.
[CultDocument("aetheria.stationkinddata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class StationKindData
{
    [CultName, JsonProperty("name"), Key(0)]
    public string Name;

    [JsonProperty("description"), Key(1)]
    public string Description;

    // A faction with no model of this kind builds the stand-in's hull and the station stays this kind. Ignored once the
    // faction offers a model of this kind.
    [JsonProperty("standIns"), Key(2)]
    public List<StationKindStandIn> StandIns = new List<StationKindStandIn>();

    public static CultRecordKey KeyOf(string id) => new CultRecordKey("station-kind:" + id);
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class StationKindStandIn
{
    [JsonProperty("faction"), Key(0)]
    public CultRecordRef<Faction> Faction;

    // The kind whose model this faction builds in place of the owning kind's.
    [JsonProperty("kind"), Key(1)]
    public CultRecordRef<StationKindData> Kind;
}

// The one owner of which kinds a faction builds, which kind a station gets, and which hull kind stands in.
public static class StationKinds
{
    // The kinds this faction can place: those of the station hulls its products offer, and those that stand in for it.
    // Ordered by key so a choice never depends on store order. Empty for no faction.
    public static CultRecordRef<StationKindData>[] KindsOf(CultCache catalog, Faction faction)
    {
        if (faction == null) return Array.Empty<CultRecordRef<StationKindData>>();
        var kinds = new HashSet<string>(OfferedStationHulls(catalog, faction).Select(hull => hull.Kind.Key.Value), StringComparer.Ordinal);
        foreach (var kind in catalog.GetAll<StationKindData>())
            if (kind.StandIns.Any(standIn => catalog.Get(standIn.Faction) == faction))
                kinds.Add(catalog.RefOf(kind).Key.Value);
        return Ordered(kinds);
    }

    // A station's kind: a stable pick among the faction's kinds by the station's site, which generation names by the zone's
    // seed and the station's place in its orbit order (never by a key the cache mints). It draws nothing from any random
    // stream, so adding the kind to generation moves no other roll. A faction with no kinds falls back to any kind some
    // offered station hull carries, logged, and the hull draw then falls back to any maker. Unset when the catalog has none.
    public static CultRecordRef<StationKindData> Choose(CultCache catalog, Faction faction, string site, Action<string> log = null)
    {
        var pool = KindsOf(catalog, faction);
        if (pool.Length == 0)
        {
            pool = Ordered(new HashSet<string>(
                catalog.GetAll<FactionProductData>().Where(product => LoadoutGenerator.IsOffered(catalog, product))
                    .Select(product => catalog.Get(product.Design) as HullData)
                    .Where(hull => hull != null && hull.HullType == HullType.Station && hull.Kind.IsSet())
                    .Select(hull => hull.Kind.Key.Value), StringComparer.Ordinal));
            if (pool.Length == 0) return default;
            log?.Invoke($"{faction?.Name ?? "This galaxy"} builds no station kind; placing a station of any kind some maker offers.");
        }
        return pool[(int) (site.StableHash() % (uint) pool.Length)];
    }

    // The kind of hull a faction builds for a station kind: the kind itself when the faction offers a model of it (a model
    // wins over a stale stand-in), else the kind its stand-in names, else the kind itself (the hull draw then falls back to
    // any maker).
    public static CultRecordRef<StationKindData> HullKindFor(CultCache catalog, Faction faction, CultRecordRef<StationKindData> kind)
    {
        if (!kind.IsSet()) return kind;
        if (OfferedStationHulls(catalog, faction).Any(hull => hull.Kind.Key.Equals(kind.Key))) return kind;
        var standIn = catalog.Get(kind)?.StandIns.FirstOrDefault(entry => catalog.Get(entry.Faction) == faction);
        return standIn != null && standIn.Kind.IsSet() ? standIn.Kind : kind;
    }

    // What is wrong with a hull's kind, or null: a Station hull names a kind the catalog holds, and no other hull names one.
    // The one rule, applied to the catalog on Open and to each mod package on Compose.
    public static string HullKindFault(CultCache catalog, HullData hull)
    {
        if (hull.HullType == HullType.Station)
        {
            if (!hull.Kind.IsSet())
                return $"Station hull '{hull.Name}' has no Kind; a station hull names the station kind it is built to be.";
            if (catalog.Get(hull.Kind) == null)
                return $"Station hull '{hull.Name}' Kind names no station kind this catalog holds.";
        }
        else if (hull.Kind.IsSet())
            return $"Hull '{hull.Name}' is a {hull.HullType} and names a Kind; only a station hull has one.";
        return null;
    }

    // Open refuses a malformed kind catalog, naming the record and field. Every Station hull names a kind the catalog holds
    // and no other hull names one; every stand-in names a faction and a kind the catalog holds, is never its own kind, and
    // does not stand on another stand-in for the same faction (one hop). A caller that gives kindlessStation is the owner
    // of the catalog being repaired: a Station hull with no Kind is reported to it by name instead of refused.
    public static void Validate(CultCache catalog, Action<string> kindlessStation = null)
    {
        foreach (var hull in catalog.GetAll<HullData>())
        {
            var fault = HullKindFault(catalog, hull);
            if (fault == null) continue;
            if (kindlessStation != null && hull.HullType == HullType.Station && !hull.Kind.IsSet()) kindlessStation(fault);
            else throw new InvalidOperationException(fault);
        }
        foreach (var kind in catalog.GetAll<StationKindData>())
        {
            var self = catalog.RefOf(kind).Key;
            for (var i = 0; i < kind.StandIns.Count; i++)
            {
                var entry = kind.StandIns[i];
                var at = $"Station kind '{kind.Name}' StandIns[{i}]";
                var faction = catalog.Get(entry.Faction);
                if (faction == null) throw new InvalidOperationException($"{at}.Faction names no faction this catalog holds.");
                var other = catalog.Get(entry.Kind);
                if (other == null) throw new InvalidOperationException($"{at}.Kind names no station kind this catalog holds.");
                if (entry.Kind.Key.Equals(self)) throw new InvalidOperationException($"{at}.Kind is the kind itself; a kind cannot stand in for itself.");
                if (other.StandIns.Any(next => catalog.Get(next.Faction) == faction))
                    throw new InvalidOperationException($"{at}.Kind stands in for the same faction itself; a stand-in is one hop.");
            }
        }
    }

    private static IEnumerable<HullData> OfferedStationHulls(CultCache catalog, Faction faction) =>
        catalog.GetAll<FactionProductData>()
            .Where(product => LoadoutGenerator.IsOffered(catalog, product) && catalog.Get(product.Manufacturer) == faction)
            .Select(product => catalog.Get(product.Design) as HullData)
            .Where(hull => hull != null && hull.HullType == HullType.Station && hull.Kind.IsSet());

    private static CultRecordRef<StationKindData>[] Ordered(IEnumerable<string> keys) =>
        keys.OrderBy(key => key, StringComparer.Ordinal).Select(key => new CultRecordRef<StationKindData>(new CultRecordKey(key))).ToArray();
}
