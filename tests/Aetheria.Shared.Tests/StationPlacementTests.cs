using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;

// Aetheria gravity, turret placement: a station sits at its planet's L4 or L5 (a sixth of a turn along the ring) and
// its turrets flank it along the same ring, spaced by hull reach so that no two bodies overlap. The zones here are
// the production generator's, ambient on, over several galaxies; every expectation is read back from the generated
// zone (its orbits, its hulls) and spelled from the rule, never from the generator's own helpers.
public sealed class StationPlacementTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stationplacement-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public StationPlacementTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private sealed record Built(uint Seed, Zone Zone, ItemManager Items);

    // stock may rearrange the catalog before a galaxy is generated; storyTurrets > 0 gives every zone a story station that
    // authors that many turrets (the '#turrets' tag), the way a narrative does.
    private IEnumerable<Built> GeneratedZones(Action<ItemManager> stock = null, int storyTurrets = 0, uint seeds = 6, int zonesPerSeed = int.MaxValue)
    {
        for (uint seed = 1; seed <= seeds; seed++)
        {
            var dir = Path.Combine(_root, $"galaxy{seed}");
            Directory.CreateDirectory(dir);
            var catalog = Path.Combine(dir, "Aetheria.cc");
            File.Copy(TestCatalog.Repo, catalog);
            var cache = new CultCache(TestCatalog.Registry());
            _caches.Add(cache);
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(Path.Combine(dir, "run.cc")), AetheriaStores.RunTypes);
            var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
            var settings = authored.Read<GameplaySettings>("GameplaySettings");
            var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
                authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
                authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), cache, new PlayerSettings(),
                Directory.CreateDirectory(Path.Combine(dir, "Narrative")), _ => { }, null, seed);
            var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
            stock?.Invoke(items);
            foreach (var galaxyZone in galaxy.Zones.Take(zonesPerSeed))
            {
                if (storyTurrets > 0)
                    galaxyZone.Locations.Add(new LocationStory { Zone = galaxyZone, Name = "Test station", Faction = galaxy.Factions[0], Type = LocationType.Station, Turrets = storyTurrets });
                var pack = ZoneGenerator.GenerateZone(items, authored.Read<ZoneGenerationSettings>("ZoneSettings"), galaxy, galaxyZone,
                    galaxyZone == galaxy.Entrance && galaxy.IsPrelude, true);
                var zone = new Zone(items, authored.Read<PlanetSettings>("PlanetSettings"), pack, galaxyZone, galaxy);
                zone.Update(.02f);
                yield return new Built(seed, zone, items);
            }
        }
    }

    private static HullType TypeOf(ItemManager items, OrbitalEntity e) => (items.GetData(e.Hull) as HullData).HullType;

    private static bool SameRing(Orbit a, Orbit b) =>
        a.Data.Parent.IsSet() && b.Data.Parent.IsSet() && a.Data.Parent.Key.Equals(b.Data.Parent.Key) &&
        Math.Abs(a.Data.Distance - b.Data.Distance) < .01f;

    private static float Turns(float turns) => turns - (float) Math.Floor(turns);

    [Fact]
    public void AStationSitsAtItsPlanetsL4OrL5()
    {
        var checkedStations = 0;
        var ahead = 0;
        var behind = 0;
        foreach (var built in GeneratedZones())
        {
            var zone = built.Zone;
            // The source is the one ring orbit no orbital entity rides: a planet's, or a bodyless node's the generator picked.
            var ridden = zone.Entities.OfType<OrbitalEntity>().Select(e => e.OrbitData).ToHashSet();
            foreach (var station in zone.Entities.OfType<OrbitalEntity>().Where(e => TypeOf(built.Items, e) == HullType.Station))
            {
                var orbit = zone.Orbits[station.OrbitData];
                var sources = zone.Orbits.Where(o => !ridden.Contains(o.Key) && SameRing(o.Value, orbit)).ToList();
                Assert.True(sources.Count == 1, $"seed {built.Seed}: a station shares its ring with {sources.Count} unridden orbits, not one");
                var planet = sources[0].Value;

                var offset = Turns(orbit.Data.Phase - planet.Data.Phase);
                var isAhead = Math.Abs(offset - 1f / 6f) < 1e-5f;
                var isBehind = Math.Abs(offset - 5f / 6f) < 1e-5f;
                Assert.True(isAhead || isBehind, $"seed {built.Seed}: a station sits {offset} turns from its planet, not 1/6 or 5/6");
                if (isAhead) ahead++; else behind++;

                var parent = zone.Orbits[orbit.Data.Parent.Key].Position;
                var toPlanet = planet.Position - parent;
                var toStation = station.Position.xz - parent;
                var angle = Math.Acos(Math.Clamp(dot(toPlanet, toStation) / (length(toPlanet) * length(toStation)), -1f, 1f)) * 180 / Math.PI;
                Assert.True(Math.Abs(angle - 60) < .01, $"seed {built.Seed}: planet-parent-station is {angle} degrees, not 60");
                checkedStations++;
            }
        }
        Assert.True(checkedStations > 0, "the seeds generated no station");
        Assert.True(ahead > 0 && behind > 0, $"the seeds put stations on one side only (ahead {ahead}, behind {behind})");
    }

    private static List<OrbitalEntity> Orbitals(Built built) => built.Zone.Entities.OfType<OrbitalEntity>().ToList();

    // Every turret belongs to one station on its ring. Each side of a station grows outward from it: a turret's centre sits
    // one body's reach (the one inside it) plus two of its own beyond that body's centre. Expectations are spelled from the
    // zone's entities and their reaches, never from the generator's helpers. Returns the largest turret count of a station.
    private static int AssertFlanks(Built built, ref int flanked, ref int mixedReach)
    {
        var orbitals = Orbitals(built);
        var turrets = orbitals.Where(e => TypeOf(built.Items, e) == HullType.Turret).ToList();
        var matched = 0;
        var most = 0;
        foreach (var station in orbitals.Where(e => TypeOf(built.Items, e) == HullType.Station))
        {
            var orbit = built.Zone.Orbits[station.OrbitData];
            var mine = turrets.Where(t => SameRing(built.Zone.Orbits[t.OrbitData], orbit)).ToList();
            matched += mine.Count;
            if (mine.Count == 0) continue;
            flanked++;
            most = Math.Max(most, mine.Count);
            var circumference = 2 * PI * orbit.Data.Distance;
            // The arc is float phase arithmetic: its absolute error grows with the ring, so the tolerance does too.
            var slack = circumference * 2e-7f;

            var arcs = mine.Select(turret =>
            {
                var phase = Turns(built.Zone.Orbits[turret.OrbitData].Data.Phase - orbit.Data.Phase);
                if (phase > .5f) phase -= 1;
                return (turret, arc: phase * circumference);
            }).ToList();
            Assert.DoesNotContain(arcs, a => a.arc == 0f);
            var behind = arcs.Where(a => a.arc < 0).OrderByDescending(a => a.arc).ToList();
            var ahead = arcs.Where(a => a.arc > 0).OrderBy(a => a.arc).ToList();
            // Turrets alternate behind then ahead, so behind holds the extra one when the count is odd.
            Assert.Equal((mine.Count + 1) / 2, behind.Count);
            Assert.Equal(mine.Count / 2, ahead.Count);
            if (mine.Select(Contacts.Radius).Distinct().Count() > 1) mixedReach++;

            foreach (var (side, sign) in new[] { (behind, -1f), (ahead, 1f) })
            {
                var innerArc = 0f;
                var innerReach = Contacts.Radius(station);
                foreach (var (turret, arc) in side)
                {
                    var expected = innerArc + sign * (innerReach + 2 * Contacts.Radius(turret));
                    Assert.True(Math.Abs(arc - expected) <= 1e-4f * Math.Abs(expected) + slack,
                        $"seed {built.Seed}: a turret sits {arc} along its ring, not {expected}");
                    innerArc = expected;
                    innerReach = Contacts.Radius(turret);
                }
            }

            var bodies = new List<OrbitalEntity> { station };
            bodies.AddRange(mine);
            for (var a = 0; a < bodies.Count; a++)
                for (var b = a + 1; b < bodies.Count; b++)
                    Assert.True(length(bodies[a].Position.xz - bodies[b].Position.xz) > Contacts.Radius(bodies[a]) + Contacts.Radius(bodies[b]),
                        $"seed {built.Seed}: two bodies on one ring overlap");
        }
        // A turret off its station's ring is matched to no station and would be dropped from every check above.
        Assert.True(matched == turrets.Count, $"seed {built.Seed}: {turrets.Count - matched} turrets sit on no station's ring");
        return most;
    }

    [Fact]
    public void TurretsFlankTheirStation()
    {
        var flanked = 0;
        var mixed = 0;
        foreach (var built in GeneratedZones())
        {
            AssertFlanks(built, ref flanked, ref mixed);
            // No station or turret reaches any other orbital entity.
            var orbitals = Orbitals(built);
            var placed = orbitals.Where(e => TypeOf(built.Items, e) is HullType.Station or HullType.Turret).ToList();
            foreach (var body in placed)
                foreach (var other in orbitals.Where(o => !ReferenceEquals(o, body)))
                    Assert.True(length(body.Position.xz - other.Position.xz) > Contacts.Radius(body) + Contacts.Radius(other),
                        $"seed {built.Seed}: a {TypeOf(built.Items, body)} overlaps a {TypeOf(built.Items, other)}");
        }
        Assert.True(flanked > 0, "the seeds generated no station with turrets");
    }

    // A station that authors more turrets than the ambient two (a narrative's '#turrets' tag) takes the tail of the
    // sequence, and turret hulls of differing reach each clear their neighbours: a second, larger turret hull is on offer.
    [Fact]
    public void ANarrativeStationsTurretsKeepFlankingAndClearEachOtherWhateverTheirReach()
    {
        const int authored = 5;
        var flanked = 0;
        var mixed = 0;
        var most = 0;
        foreach (var built in GeneratedZones(WithALargerTurretHull, authored, 3, 3))
            most = Math.Max(most, AssertFlanks(built, ref flanked, ref mixed));
        Assert.True(most == authored, $"no story station carried its {authored} authored turrets (most {most})");
        Assert.True(mixed > 0, "no station carried turrets of differing reach");
    }

    // A zone with no turret hull on offer places no turret, and leaves no orbit behind for one.
    [Fact]
    public void ANoTurretZoneLeavesNoOrphanTurretOrbit()
    {
        var stations = 0;
        foreach (var built in GeneratedZones(WithoutTurretHulls, 0, 3, 4))
        {
            var zone = built.Zone;
            Assert.DoesNotContain(Orbitals(built), e => TypeOf(built.Items, e) == HullType.Turret);
            var ridden = zone.Entities.OfType<OrbitalEntity>().Select(e => e.OrbitData).ToHashSet();
            foreach (var station in Orbitals(built).Where(e => TypeOf(built.Items, e) == HullType.Station))
            {
                var orbit = zone.Orbits[station.OrbitData];
                var strays = zone.Orbits.Count(o => !ridden.Contains(o.Key) && SameRing(o.Value, orbit));
                Assert.True(strays == 1, $"seed {built.Seed}: a station's ring holds {strays} orbits no entity rides, not just its source's");
                stations++;
            }
        }
        Assert.True(stations > 0, "the seeds generated no station");
    }

    private static List<FactionProductData> TurretProducts(ItemManager items) =>
        items.ItemData.GetAll<FactionProductData>().Where(p => items.ItemData.Get(p.Design) is HullData { HullType: HullType.Turret }).ToList();

    // Priced at zero a design is not on offer, which is how the generator's own product filter drops it.
    private static void WithoutTurretHulls(ItemManager items)
    {
        foreach (var product in TurretProducts(items)) ((HullData) items.ItemData.Get(product.Design)).Price = 0;
    }

    // The shipped catalog has one turret hull. A copy with a bigger solid shape (a larger reach) and its own product
    // gives the generator two turret hulls to choose between.
    private static void WithALargerTurretHull(ItemManager items)
    {
        var copyOf = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        foreach (var product in TurretProducts(items))
        {
            var hull = (HullData) items.ItemData.Get(product.Design);
            var larger = (HullData) copyOf.Invoke(hull, null);
            larger.Name = hull.Name + " (large)";
            larger.Shape = new Shape(hull.Shape.Width + 2, hull.Shape.Height + 2);
            foreach (var cell in larger.Shape.AllCoordinates) larger.Shape[cell] = true;
            var largerProduct = (FactionProductData) copyOf.Invoke(product, null);
            largerProduct.Name = product.Name + " (large)";
            largerProduct.Design = new CultRecordRef<CraftedItemData>(items.ItemData.Upsert(larger).Key);
            items.ItemData.Upsert(largerProduct);
        }
    }

    // The reach every spacing is built from: the farthest corner of any occupied cell from the centre of mass, scaled by
    // the cell size. Spelled here from that definition over the shipped station and turret hulls.
    [Fact]
    public void AHullsReachIsItsFarthestCorner()
    {
        var seen = new HashSet<string>();
        foreach (var built in GeneratedZones().Take(6))
            foreach (var entity in built.Zone.Entities.OfType<OrbitalEntity>())
            {
                var hull = built.Items.GetData(entity.Hull) as HullData;
                if (!seen.Add(hull.Name)) continue;
                var com = hull.Shape.CenterOfMass;
                var farthest = 0f;
                foreach (var c in hull.Shape.Coordinates)
                    foreach (var dx in new[] { -.5f, .5f })
                        foreach (var dy in new[] { -.5f, .5f })
                            farthest = Math.Max(farthest, (float) Math.Sqrt(Math.Pow(c.x + dx - com.x, 2) + Math.Pow(c.y + dy - com.y, 2)));
                var cell = built.Items.GameplaySettings.SchematicCellSize;
                Assert.True(Math.Abs(hull.Reach(cell) - farthest * cell) <= 1e-4f * farthest * cell, $"hull {hull.Name}: reach {hull.Reach(cell)} is not {farthest * cell}");
                Assert.True(hull.Reach(cell) > 0f);
            }
        Assert.True(seen.Count >= 2, "the zones held fewer than a station hull and a turret hull");
    }
}
