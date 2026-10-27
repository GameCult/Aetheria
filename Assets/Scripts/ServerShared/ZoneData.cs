/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;
using UniRx;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ZonePack
{
    [JsonProperty("planets"), Key(0)]
    public List<CultRecordRef<BodyData>> Planets = new List<CultRecordRef<BodyData>>();

    [JsonProperty("orbits"), Key(1)]
    public List<CultRecordRef<OrbitData>> Orbits = new List<CultRecordRef<OrbitData>>();

    [JsonProperty("entities"), Key(2)]
    public List<EntityPack> Entities = new List<EntityPack>();

    [JsonProperty("radius"), Key(3)]
    public float Radius = 2000;

    [JsonProperty("mass"), Key(4)]
    public float Mass = 10000;

    [JsonProperty("time"), Key(5)]
    public double Time;

    // Cut 2 (docs/mining-cut.md): chunk wear (damage taken, broken-until time), keyed by chunk. Nullable, not
    // defaulted to an empty list: an older record has no key 6 at all, and MessagePack fills a missing trailing
    // key with nil, which a reference-typed field reads back as null. ARunWithoutKeySixLoadsWhole pins that a
    // record from before this key existed loads with no wear, rather than throwing or silently reading a
    // default instance.
    [JsonProperty("chunkWear"), Key(6)]
    public List<ChunkWearPack> ChunkWear;

    // True once this zone's boss has been spawned. The dead boss leaves no entity, so this is the one fact that tells a
    // slain boss from one that never existed; RunGoal.ExitOpen reads it. Written only by ZoneGenerator, carried by PackZone.
    [JsonProperty("bossSpawned"), Key(7)]
    public bool BossSpawned;
}

// Cut 2 (docs/mining-cut.md): one packed wear entry for one chunk. "Field" names the body that owns the chunk
// (an asteroid belt today; the operator's ruling generalizes chunks to other kinds of debris field later), not
// "belt" -- the chunk surface itself must not bake in the one live field kind.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ChunkWearPack
{
    [JsonProperty("field"), Key(0)]
    public CultRecordKey Field;

    [JsonProperty("index"), Key(1)]
    public int Index;

    [JsonProperty("damage"), Key(2)]
    public float Damage;

    [JsonProperty("brokenUntil"), Key(3)]
    public double? BrokenUntil;
}

[JsonObject(MemberSerialization.OptIn)]
public abstract class BodyData
{
    [CultName, JsonProperty("name"), Key(1)]
    public string Name = "";

    [JsonProperty("orbit"), Key(2)]
    public CultRecordRef<OrbitData> Orbit;

    [JsonProperty("mass"), Key(3)]
    public float Mass = 0;

    // MessagePack key 4 belonged to the removed stored Resources; do not reuse it. A belt's yield is derived by FieldKinds.Yield.

    [JsonProperty("bodyRadiusMul")] [Key(5)]
    public float BodyRadiusMultiplier = 1;
}

[CultDocument("aetheria.planetdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PlanetData : BodyData
{

}

[CultDocument("aetheria.asteroidbeltdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AsteroidBeltData : BodyData
{
    [JsonProperty("asteroids"), Key(9)]
    public Asteroid[] Asteroids;

    // Mining Cut 3 (docs/mining-cut-refresh.md, Q16 B): the belt's field kind, chosen once and saved. Unset on a
    // belt from before this key existed (MessagePack leaves a missing trailing key at its default); Zone assigns
    // it on first load through FieldKinds.Ensure, the same function generation uses.
    [JsonProperty("kind"), Key(10)]
    public CultRecordRef<FieldKindData> Kind;

    // A Trojan swarm (ruling trojans-ride-host-period-with-stations): every rock rides the belt's rail at the rail's period, so
    // the swarm keeps its shape. False on a belt from before this key (MessagePack leaves a missing trailing key at its default),
    // which keeps each rock on its own distance's period: an annulus.
    [JsonProperty("ridesRail"), Key(11)]
    public bool RidesRail;
}

// Mining Cut 3 (docs/mining-cut-refresh.md, Q11): a kind of debris field, an authored catalog record. A chunk's
// reflectivity comes from its field's kind; its yields (ore geography, ruling ores-are-regional) are the ores a belt of
// this kind holds before a province weights them. FieldKinds.Yield is the one reader.
[CultDocument("aetheria.fieldkinddata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FieldKindData
{
    [CultName, JsonProperty("name"), Key(0)]
    public string Name = "";

    // Reflectivity per schematic cell of chunk area: a chunk reflects CrossSection × its area in cells × the light
    // on it (Zone.ChunkVisibility), the rule Reflector applies to a hull.
    [JsonProperty("crossSection"), Key(1)]
    public float CrossSection;

    // How often generation picks this kind, relative to the others (FieldKinds.Assign). Zero never picks it.
    [JsonProperty("generationWeight"), Key(2)]
    public float GenerationWeight = 1;

    // The ores a belt of this kind holds. Empty on a kind from before this key (MessagePack leaves a missing trailing key at its default).
    [JsonProperty("yields"), Key(3)]
    public List<FieldYield> Yields = new List<FieldYield>();
}

// One ore a kind of field holds: Abundance of Commodity before the belt's province weights it. A Regional ore is held only
// where the province lists it (ruling ores-are-regional: some ores exist only in some provinces); any other entry is held
// everywhere, scaled by the province's factor for it.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FieldYield
{
    [JsonProperty("commodity"), Key(0)]
    public CultRecordRef<SimpleCommodityData> Commodity;

    [JsonProperty("abundance"), Key(1)]
    public float Abundance;

    [JsonProperty("regional"), Key(2)]
    public bool Regional;
}

// Ore geography (rulings provinces-within-regions, ores-are-regional): a kind of ground, authored. A region deals its
// provinces' types from a deck holding GenerationWeight cards of each type (ruling province-types-dealt-per-region);
// a province's type weights which kinds its belts draw and which ores a kind holds there.
[CultDocument("aetheria.provincedata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ProvinceData
{
    [CultName, JsonProperty("name"), Key(0)]
    public string Name = "";

    // The number of cards of this type in a region's deck. Zero is never dealt.
    [JsonProperty("generationWeight"), Key(1)]
    public int GenerationWeight = 1;

    // Multiplies the kind's own GenerationWeight in a belt's draw; a kind not listed is multiplied by one.
    [JsonProperty("kinds"), Key(2)]
    public List<ProvinceKindWeight> Kinds = new List<ProvinceKindWeight>();

    // Multiplies an ore's Abundance wherever it is held; an ore not listed is multiplied by one, or by zero when the
    // kind's entry for it is Regional.
    [JsonProperty("ores"), Key(3)]
    public List<ProvinceOre> Ores = new List<ProvinceOre>();
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ProvinceKindWeight
{
    [JsonProperty("kind"), Key(0)]
    public CultRecordRef<FieldKindData> Kind;

    [JsonProperty("weight"), Key(1)]
    public float Weight = 1;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ProvinceOre
{
    [JsonProperty("commodity"), Key(0)]
    public CultRecordRef<SimpleCommodityData> Commodity;

    [JsonProperty("weight"), Key(1)]
    public float Weight = 1;
}

// Mining Cut 3 (Q16 B): the one owner of which kind a belt is. Ore geography: and of what it yields.
public static class FieldKinds
{
    // The one weighted pick: an index into weights, chosen by a stable hash of the site (the same site and the same
    // weights always give the same index), never one whose weight is not positive. -1 when no weight is positive.
    internal static int Pick(string site, double[] weights)
    {
        var pick = site.StableHash() / 4294967296.0 * weights.Sum();
        var last = -1;
        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0) continue;
            last = i;
            if (pick < weights[i]) return i;
            pick -= weights[i];
        }
        return last;
    }

    // A weighted pick among the catalog's kinds, by a stable hash of the belt's site: the same site, catalog and province
    // type always give the same kind. A kind's weight is its GenerationWeight times its province's factor for it; a belt with
    // no province type (unset, or naming nothing the catalog holds) draws unweighted. Generation names a belt by the zone's
    // seed and the belt's place in generation order (never by the key the cache mints, which differs every run); a belt saved
    // without a kind is named by its key. Kinds are ordered by key so the pick does not depend on store order.
    // Unset when the catalog has no kind with positive weight.
    public static CultRecordRef<FieldKindData> Assign(string site, CultCache catalog, CultRecordRef<ProvinceData> province)
    {
        var type = catalog.Get(province);
        var kinds = catalog.GetAll<FieldKindData>()
            .Select(kind =>
            {
                var key = catalog.RefOf(kind).Key;
                var factor = Factor(type?.Kinds.Where(entry => entry.Kind.Key.Equals(key)).Select(entry => (double) entry.Weight), 1.0);
                // Both must be positive: two negatives do not multiply into a weight.
                return (Key: key, Weight: kind.GenerationWeight > 0f && factor > 0.0 ? kind.GenerationWeight * factor : 0.0);
            })
            .Where(kind => kind.Weight > 0.0)
            .OrderBy(kind => kind.Key.Value, StringComparer.Ordinal)
            .ToArray();
        if (kinds.Length == 0) return default;

        return new CultRecordRef<FieldKindData>(kinds[Pick(site, kinds.Select(kind => kind.Weight).ToArray())].Key);
    }

    // A province's factor for one entry: the sum of its listings, or the default when it lists none.
    private static double Factor(IEnumerable<double> listed, double unlisted)
    {
        var weights = listed?.ToArray();
        return weights == null || weights.Length == 0 ? unlisted : weights.Sum();
    }

    // Gives a belt with no kind its kind and saves it; a belt that has one keeps it, whatever the catalog now says.
    // Generation (ZoneGenerator) and first load (Zone) both call this, so there is one assignment rule.
    public static void Ensure(string site, AsteroidBeltData belt, CultCache cache, CultRecordRef<ProvinceData> province)
    {
        if (belt.Kind.IsSet()) return;
        belt.Kind = Assign(site, cache, province);
        if (belt.Kind.IsSet()) cache.Upsert(belt);
    }

    // What a belt of this kind holds in a zone of this province: each of the kind's yields, its Abundance times the
    // province's factor for the ore (unlisted: one, or zero when the yield is Regional), summed per commodity, in commodity
    // key order, zero amounts dropped. Derived on every call, never stored. Empty for an unset kind.
    public static (CultRecordRef<SimpleCommodityData> Commodity, float Amount)[] Yield(CultRecordRef<FieldKindData> kind,
        CultRecordRef<ProvinceData> province, CultCache catalog)
    {
        var data = catalog.Get(kind);
        if (data == null) return Array.Empty<(CultRecordRef<SimpleCommodityData>, float)>();
        var type = catalog.Get(province);
        return data.Yields
            .Select(yield => (yield.Commodity, Amount: yield.Abundance * Factor(
                type?.Ores.Where(ore => ore.Commodity.Key.Equals(yield.Commodity.Key)).Select(ore => (double) ore.Weight), yield.Regional ? 0.0 : 1.0)))
            .GroupBy(yield => yield.Commodity.Key.Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (group.First().Commodity, Amount: (float) group.Sum(yield => yield.Amount)))
            .Where(yield => yield.Amount > 0f)
            .ToArray();
    }
}

// Ore geography (rulings provinces-within-regions, boss-zone-is-near-side, province-partition-by-size, province-types-dealt-per-region).
// Pure functions of the zone graph, the run seed and the catalog: nothing here draws from Galaxy's Random stream, stores membership
// or reads who owns what. Galaxy builds provinces once, in its generation and save constructors, and nothing re-partitions them.
public static class Provinces
{
    // Zones per province when a settings object does not say (SectorGenerationSettings.ProvinceZones): calibration, not a rule.
    public const int DefaultZonesPerProvince = 8;

    // A boss zone sits on the near side of its border: its crossing links are its links to zones in the component of exit in
    // the graph without it. The one definition, read by Regions (which cuts them) and ExtraExits (which prunes all but one).
    // exit unset (-1) gives none, and a boss zone that is the exit has none. Ordered by boss, then far zone.
    public static (int Boss, int Far)[] CrossingLinks(int[][] adjacency, IReadOnlyCollection<int> bossZones, int exit)
    {
        var links = new List<(int Boss, int Far)>();
        if (exit >= 0)
            foreach (var boss in bossZones.Distinct().Where(boss => boss != exit).OrderBy(boss => boss))
            {
                var exitSide = Flood(adjacency, exit, (a, b) => b != boss);
                foreach (var next in adjacency[boss].OrderBy(next => next))
                    if (exitSide[next]) links.Add((boss, next));
            }
        return links.ToArray();
    }

    // The crossing links to prune so each boss zone keeps one exit: all of them but the link to the boss zone's successor on
    // exitPath (the zones from the entrance to the exit), which is the link the run goes through. A boss zone that is not on
    // exitPath, or whose successor is not one of its crossing links, keeps all of its links: it is never left with none.
    public static (int Boss, int Far)[] ExtraExits(int[][] adjacency, IReadOnlyCollection<int> bossZones, int exit, int[] exitPath) =>
        CrossingLinks(adjacency, bossZones, exit).GroupBy(link => link.Boss)
            .SelectMany(links =>
            {
                var at = Array.IndexOf(exitPath, links.Key);
                var kept = at >= 0 && at + 1 < exitPath.Length ? exitPath[at + 1] : -1;
                return links.Any(link => link.Far == kept) ? links.Where(link => link.Far != kept) : Enumerable.Empty<(int Boss, int Far)>();
            })
            .ToArray();

    // Each zone's region, named by the region's least zone index. Cutting every boss zone's crossing links (CrossingLinks)
    // leaves the regions as the connected components. Every zone, boss zones included, is in exactly one.
    public static int[] Regions(int[][] adjacency, IReadOnlyCollection<int> bossZones, int exit)
    {
        var cut = CrossingLinks(adjacency, bossZones, exit).Select(link => (Math.Min(link.Boss, link.Far), Math.Max(link.Boss, link.Far))).ToHashSet();

        var regionOf = new int[adjacency.Length];
        Array.Fill(regionOf, -1);
        for (var start = 0; start < adjacency.Length; start++)
        {
            if (regionOf[start] >= 0) continue;
            var members = Flood(adjacency, start, (a, b) => !cut.Contains((Math.Min(a, b), Math.Max(a, b))));
            for (var i = 0; i < members.Length; i++) if (members[i]) regionOf[i] = start;
        }
        return regionOf;
    }

    // Each zone's province, named by the province's seed zone index. A region of size s takes max(1, round(s / zonesPerProvince))
    // seeds: the first is the zone with the least hash of the run seed, each next the zone farthest (in-region hops) from the
    // seeds chosen so far, ties by that hash; then every zone joins the nearest seed, ties to the earlier seed. A province is
    // contiguous and never crosses a region.
    public static int[] Partition(int[][] adjacency, int[] regionOf, uint runSeed, int zonesPerProvince)
    {
        var seedOf = new int[adjacency.Length];
        uint Hash(int index) => $"{runSeed}:province-seed:{index}".StableHash();
        foreach (var region in Enumerable.Range(0, adjacency.Length).GroupBy(zone => regionOf[zone]).OrderBy(group => group.Key))
        {
            var zones = region.ToArray();
            var count = Math.Min(zones.Length, Math.Max(1, (int) Math.Round((double) zones.Length / Math.Max(1, zonesPerProvince), MidpointRounding.AwayFromZero)));
            var seeds = new List<int> { zones.OrderBy(Hash).ThenBy(zone => zone).First() };
            while (seeds.Count < count)
            {
                var hops = Grow(adjacency, regionOf, seeds.ToArray()).Hops;
                seeds.Add(zones.Where(zone => !seeds.Contains(zone)).OrderByDescending(zone => hops[zone]).ThenBy(Hash).ThenBy(zone => zone).First());
            }
            var owner = Grow(adjacency, regionOf, seeds.ToArray()).Owner;
            foreach (var zone in zones) seedOf[zone] = seeds[owner[zone]];
        }
        return seedOf;
    }

    // A region's hand of province types, one per province in ascending seed-zone order. Types are dealt without replacement from
    // a deck of GenerationWeight cards per type (catalog types in key order), each draw one weighted pick over the cards remaining
    // at a site named by the run seed, the region and the draw's place; the deck is restored whole when it runs out. An empty
    // deck deals every province an unset type.
    public static CultRecordRef<ProvinceData>[] Deal(int regionKey, int seedCount, uint runSeed, CultCache catalog)
    {
        var types = catalog.GetAll<ProvinceData>()
            .Where(type => type.GenerationWeight > 0)
            .Select(type => (Ref: catalog.RefOf(type), Cards: type.GenerationWeight))
            .OrderBy(type => type.Ref.Key.Value, StringComparer.Ordinal)
            .ToArray();
        var hand = new CultRecordRef<ProvinceData>[seedCount];
        if (types.Length == 0) return hand;

        var remaining = types.Select(type => type.Cards).ToArray();
        for (var n = 0; n < seedCount; n++)
        {
            if (remaining.Sum() == 0) remaining = types.Select(type => type.Cards).ToArray();
            var card = FieldKinds.Pick($"{runSeed}:province-deal:{regionKey}:{n}", remaining.Select(cards => (double) cards).ToArray());
            remaining[card]--;
            hand[n] = types[card].Ref;
        }
        return hand;
    }

    // Every zone reachable from start over the links the filter allows.
    private static bool[] Flood(int[][] adjacency, int start, Func<int, int, bool> allowed)
    {
        var reached = new bool[adjacency.Length];
        var stack = new Stack<int>();
        reached[start] = true;
        stack.Push(start);
        while (stack.Count > 0)
        {
            var zone = stack.Pop();
            foreach (var next in adjacency[zone])
                if (!reached[next] && allowed(zone, next))
                {
                    reached[next] = true;
                    stack.Push(next);
                }
        }
        return reached;
    }

    // Breadth-first growth from seeds inside their regions, level by level: each zone's hops to the nearest seed and the rank
    // (place in seeds) of the earliest seed among those that reach it first.
    private static (int[] Owner, int[] Hops) Grow(int[][] adjacency, int[] regionOf, int[] seeds)
    {
        var owner = new int[adjacency.Length];
        var hops = new int[adjacency.Length];
        Array.Fill(owner, -1);
        for (var rank = 0; rank < seeds.Length; rank++)
        {
            owner[seeds[rank]] = rank;
            hops[seeds[rank]] = 0;
        }
        var frontier = seeds.ToList();
        for (var level = 1; frontier.Count > 0; level++)
        {
            var claims = new Dictionary<int, int>();
            foreach (var zone in frontier)
                foreach (var next in adjacency[zone])
                    if (owner[next] < 0 && regionOf[next] == regionOf[zone])
                        claims[next] = claims.TryGetValue(next, out var rank) ? Math.Min(rank, owner[zone]) : owner[zone];
            foreach (var claim in claims)
            {
                owner[claim.Key] = claim.Value;
                hops[claim.Key] = level;
            }
            frontier = claims.Keys.OrderBy(zone => zone).ToList();
        }
        return (owner, hops);
    }
}

[CultDocument("aetheria.gasgiantdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GasGiantData : BodyData
{
    [JsonProperty("firstOffsetDomainRotationSpeed"), Key(9)]
    public float FirstOffsetDomainRotationSpeed = 1;

    [JsonProperty("firstOffsetRotationSpeed"), Key(10)]
    public float FirstOffsetRotationSpeed = 1;

    [JsonProperty("secondOffsetDomainRotationSpeed"), Key(11)]
    public float SecondOffsetDomainRotationSpeed = 1;

    [JsonProperty("secondOffsetRotationSpeed"), Key(12)]
    public float SecondOffsetRotationSpeed = 1;

    [JsonProperty("albedoRotationSpeed"), Key(13)]
    public float AlbedoRotationSpeed = 1;

    [JsonProperty("materialOverrides"), Key(18)]
    public List<string> MaterialOverrides = new List<string>();

    [JsonProperty("colors"), Key(19)]
    public float4[] Colors = new float4[0];
}

[CultDocument("aetheria.sundata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class SunData : GasGiantData
{
    [JsonProperty("lightColor"), Key(20)]
    public float3 LightColor = float3.zero;

    [JsonProperty("fogTintColor"), Key(21)]
    public float3 FogTintColor = float3.zero;

    [JsonProperty("lightRadiusMul")] [Key(22)]
    public float LightRadiusMultiplier = 1;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class Asteroid
{
    [JsonProperty("distance"), Key(0)]
    public float Distance;

    [JsonProperty("phase"), Key(1)]
    public float Phase;

    [JsonProperty("size"), Key(2)]
    public float Size;

    [JsonProperty("rotationSpeed"), Key(3)]
    public float RotationSpeed;
}

[CultDocument("aetheria.orbitdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class OrbitData
{
    [JsonProperty("parent"), Key(1)]
    public CultRecordRef<OrbitData> Parent;

    [JsonProperty("distance"), Key(2)]
    public float Distance;

    [JsonProperty("phase"), Key(3)]
    public float Phase;

    [JsonProperty("phase"), Key(4)]
    public float2 FixedPosition = float2.zero;

    // [JsonProperty("period"), Key(4)]
    // public float Period;

    public static float2 Evaluate(float phase)
    {
        phase *= PI * 2;
        return new float2(cos(phase), sin(phase));
    }
}
