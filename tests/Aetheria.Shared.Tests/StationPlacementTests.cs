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

    private IEnumerable<Built> GeneratedZones()
    {
        for (uint seed = 1; seed <= 6; seed++)
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
            foreach (var galaxyZone in galaxy.Zones)
            {
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

    [Fact]
    public void TurretsFlankTheirStation()
    {
        var flanked = 0;
        var pairsChecked = 0;
        foreach (var built in GeneratedZones())
        {
            var zone = built.Zone;
            var orbitals = zone.Entities.OfType<OrbitalEntity>().ToList();
            var turrets = orbitals.Where(e => TypeOf(built.Items, e) == HullType.Turret).ToList();
            foreach (var station in orbitals.Where(e => TypeOf(built.Items, e) == HullType.Station))
            {
                var orbit = zone.Orbits[station.OrbitData];
                var mine = turrets.Where(t => SameRing(zone.Orbits[t.OrbitData], orbit)).ToList();
                if (mine.Count == 0) continue;
                flanked++;
                var stationReach = Contacts.Radius(station);
                var circumference = 2 * PI * orbit.Data.Distance;
                // The arc is float phase arithmetic: its absolute error grows with the ring, so the tolerance does too.
                var slack = circumference * 2e-7f;

                var ks = new List<int>();
                foreach (var turret in mine)
                {
                    var arcUnit = stationReach + 2 * Contacts.Radius(turret);
                    var phase = Turns(zone.Orbits[turret.OrbitData].Data.Phase - orbit.Data.Phase);
                    if (phase > .5f) phase -= 1;
                    var signedArc = phase * circumference;
                    var k = (int) Math.Round(signedArc / arcUnit);
                    Assert.True(k != 0, $"seed {built.Seed}: a turret sits on its station");
                    Assert.True(Math.Abs(signedArc - k * arcUnit) <= 1e-3f * arcUnit + slack,
                        $"seed {built.Seed}: a turret's arc {signedArc} is not a whole multiple ({k}) of {arcUnit}");
                    ks.Add(k);
                }
                var expected = Enumerable.Range(0, mine.Count).Select(t => (t / 2 + 1) * (t % 2 == 0 ? -1 : 1)).OrderBy(k => k);
                Assert.Equal(expected, ks.OrderBy(k => k));

                var bodies = new List<OrbitalEntity> { station };
                bodies.AddRange(mine);
                for (var a = 0; a < bodies.Count; a++)
                    for (var b = a + 1; b < bodies.Count; b++)
                    {
                        var apart = length(bodies[a].Position.xz - bodies[b].Position.xz);
                        Assert.True(apart > Contacts.Radius(bodies[a]) + Contacts.Radius(bodies[b]),
                            $"seed {built.Seed}: two bodies on one ring overlap ({apart} apart)");
                        pairsChecked++;
                    }
            }
        }
        Assert.True(flanked > 0, "the seeds generated no station with turrets");
        Assert.True(pairsChecked > 0, "no pair was checked");
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
