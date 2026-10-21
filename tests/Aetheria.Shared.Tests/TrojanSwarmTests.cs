using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using Xunit.Abstractions;
using static CultMath.math;
using float2 = CultMath.float2;

// Trojan swarms (rulings trojans-first, trojans-ride-host-period-with-stations; map B60, B61): a belt on a copy of its host's
// rail whose rocks round L4 and L5 ride the rail's period, so the swarm keeps its shape, adds nothing to the well tree, and
// leaves every other draw in the zone where it was.
public class TrojanSwarmTests
{
    private const uint First = 1, Last = 6;
    private readonly ITestOutputHelper _out;
    public TrojanSwarmTests(ITestOutputHelper output) => _out = output;

    private static bool IsSwarm(BodyData body) => body is AsteroidBeltData { RidesRail: true };

    private static IEnumerable<(Orbit Host, Orbit Rail, AsteroidBelt Swarm)> Swarms(Zone zone)
    {
        foreach (var (key, belt) in zone.AsteroidBelts.Where(b => b.Value.Data.RidesRail))
        {
            var rail = zone.Orbits[zone.Planets[key].Orbit.Key];
            var host = zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).Select(b => zone.Orbits[b.Orbit.Key])
                .Single(o => o.Data.Parent.Key.Equals(rail.Data.Parent.Key) && o.Data.Distance == rail.Data.Distance && o.Data.Phase == rail.Data.Phase);
            yield return (host, rail, belt);
        }
    }

    private static HashSet<CultRecordKey> SwarmRails(Zone zone) =>
        zone.AsteroidBelts.Where(x => x.Value.Data.RidesRail).Select(x => zone.Planets[x.Key].Orbit.Key).ToHashSet();

    private static float Turn(float2 p) => frac((float) (Math.Atan2(p.y, p.x) / (2 * Math.PI)));
    private static float Wrap(float t) => t - (float) Math.Round(t);

    // A standing binary leaves a bodyless node with a parent and its pair's mass, heavy enough to pass the host mass filter; a
    // swarm round it would ride a rail no body is on (finding swarm-on-bodyless-node). StationPlacementTests' binary tuning with a
    // swarm drawn for every eligible host: every swarm's rail copies a body's orbit.
    [Fact]
    public void NoSwarmRidesABinarysBodylessNode()
    {
        using var zones = new ChildrenWithinReachTests.Zones(First, Last, z =>
        {
            z.SatelliteCreationProbability = 1;
            z.BinaryCreationProbability = 1;
            z.ZoneRadius.Minimum *= 3; z.ZoneRadius.Maximum *= 3;
            z.ZoneMass.Minimum *= .1f; z.ZoneMass.Maximum *= .1f;
            z.TrojanProbability = 1;
        });
        int binaries = 0, swarms = 0, bodyless = 0;
        foreach (var (seed, zone) in zones.All)
        {
            var bodyOrbits = zone.Planets.Values.Select(b => b.Orbit.Key).ToHashSet();
            binaries += zone.Orbits.Count(o => o.Value.Data.Parent.IsSet() && !bodyOrbits.Contains(o.Key) && zone.Wells.Reach(o.Key) > 0f);
            var bodies = zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).Select(b => zone.Orbits[b.Orbit.Key].Data).ToList();
            foreach (var rail in zone.AsteroidBelts.Where(b => b.Value.Data.RidesRail).Select(b => zone.Orbits[zone.Planets[b.Key].Orbit.Key].Data))
            {
                swarms++;
                if (!bodies.Any(o => o.Parent.Key.Equals(rail.Parent.Key) && o.Distance == rail.Distance && o.Phase == rail.Phase)) bodyless++;
            }
        }
        _out.WriteLine($"zones {zones.All.Count}, bodyless nodes {binaries}, swarms {swarms}, on a bodyless node {bodyless}");
        Assert.True(binaries > 5 && swarms > 10, $"{binaries} bodyless nodes and {swarms} swarms: the seeds do not exercise the rule");
        Assert.Equal(0, bodyless);
    }

    [Fact]
    public void ASwarmKeepsItsShapeOverAnHour()
    {
        using var zones = new ChildrenWithinReachTests.Zones(First, Last, null);
        var s = zones.Settings;
        int swarms = 0, l4 = 0, l5 = 0;
        float drift = 0f, widest = 0f, longest = 0f;
        foreach (var (seed, zone) in zones.All)
            foreach (var (host, rail, swarm) in Swarms(zone))
            {
                swarms++;
                var d = host.Data.Distance;
                // The host is a planet or a gas giant: heavier than a moon, no heavier than a sun.
                var hostMass = zone.Planets.Values.Where(b => !(b is AsteroidBeltData) && zone.Orbits[b.Orbit.Key] == host).Select(b => b.Mass).Single();
                Assert.True(hostMass > s.PlanetMass && hostMass <= s.SunMass, $"seed {seed}: a swarm host of mass {hostMass}");
                Assert.Equal(2 * (int) s.TrojanCount.Evaluate(d), swarm.Data.Asteroids.Length);
                int sideA = 0, sideB = 0;
                for (var i = 0; i < swarm.Data.Asteroids.Length; i++)
                {
                    var rock = swarm.Data.Asteroids[i];
                    Assert.True(Math.Abs(rock.Distance / d - 1) <= s.TrojanHalfWidth * 1.0001f, $"seed {seed}: a rock sits {rock.Distance} from a rail at {d}");
                    widest = Math.Max(widest, Math.Abs(rock.Distance / d - 1));
                    float Offset(double t) => Wrap(Turn(swarm.Pose(i, t, float2.zero, 1).xy) - Turn(OrbitData.Evaluate((float) frac(t / host.Period + host.Data.Phase))));
                    var at0 = Offset(0);
                    var side = at0 > 0 ? 1f / 6 : -1f / 6;
                    if (side > 0) { l4++; sideA++; } else { l5++; sideB++; }
                    Assert.Equal(i % 2 == 0, side > 0); // even rocks round L4 (a sixth of a turn ahead), odd round L5
                    longest = Math.Max(longest, Math.Abs(at0 - side));
                    Assert.True(Math.Abs(at0 - side) <= s.TrojanArc / 2 + 1e-4f, $"seed {seed}: a rock sits {at0} turns from its host");
                    drift = Math.Max(drift, Math.Abs(Wrap(Offset(3600) - at0)));
                }
                Assert.Equal(sideA, sideB);
            }
        _out.WriteLine($"swarms {swarms}, rocks at L4 {l4}, L5 {l5}, largest drift against the host over an hour {drift} turns");
        Assert.True(swarms >= 10, $"only {swarms} swarms in seeds {First}-{Last}");
        Assert.True(l4 > 0 && l5 > 0);
        Assert.True(widest > s.TrojanHalfWidth / 2, $"the widest rock sits {widest} off its rail, half-width {s.TrojanHalfWidth}");
        Assert.True(longest > s.TrojanArc / 4, $"the farthest rock sits {longest} turns from its point, half-arc {s.TrojanArc / 2}");
        Assert.True(drift < 1e-3f, $"a swarm rock drifts {drift} turns against its host in an hour");
    }

    [Fact]
    public void StationsStillLandOnSwarmHosts()
    {
        using var zones = new ChildrenWithinReachTests.Zones(First, Last, null);
        var onSwarmHost = 0;
        foreach (var (seed, zone) in zones.All)
        {
            var hosts = Swarms(zone).Select(x => x.Host).ToHashSet();
            foreach (var station in zone.Entities.OfType<OrbitalEntity>())
            {
                var orbit = zone.Orbits[station.OrbitData].Data;
                if (hosts.Any(h => h.Data.Parent.Key.Equals(orbit.Parent.Key) && Math.Abs(h.Data.Distance - orbit.Distance) < .01f)) onSwarmHost++;
            }
        }
        _out.WriteLine($"orbital entities on a swarm host's rail {onSwarmHost}");
        Assert.True(onSwarmHost > 0, "no station or turret sits on a swarm host's rail");
    }

    // The widened digests (ZoneFingerprint with PopulatedRadius) were computed on Yggdrasil over the generator of cut laned-belts
    // C1 (one belt extent rule) with swarms off, over the zones of each seed; both samples draw no lane, so the base is the zone
    // with neither shape: a zone with no swarm is the base zone, and a zone with
    // swarms is the base zone plus them. The per-zone comparison leaves PopulatedRadius out, since a swarm's outer rock may grow it.
    private static readonly Dictionary<uint, string> BaseDigest = new Dictionary<uint, string>
    {
        [1] = "1873400d50d11b27913a67daca460f0829629aa21dacabac2cfc97a66537a700",
        [2] = "5872680ee91c2019b345925f54c2113303cc2817ee9ea28d26ba839e43d1944e",
        [3] = "e92cc32010f01b7962a2a58b6ea91c9c39ee3d915301164f176d0ebad218837c",
        [4] = "61db9bdae2ca8704a7946debb4fe63ae3e649112fcda19d293ba2fddcaf45674",
        [5] = "69ca46ebfae646b3a09faf9d636f3c706e5f6b7146f111a0ea06196332ae58b9",
        [6] = "307dc4b38ff341670a5b5179ab977b6c7c59ba682d529d80dcaf9c4852c21fe2",
    };

    [Fact]
    public void ASwarmMovesNothingElseInTheZone()
    {
        using var swarmless = new ChildrenWithinReachTests.Zones(First, Last, z => { z.TrojanProbability = 0; z.LaneProbability = 0; });
        using var live = new ChildrenWithinReachTests.Zones(First, Last, z => z.LaneProbability = 0);
        Assert.Equal(swarmless.All.Count, live.All.Count);
        int swarms = 0, grew = 0;
        foreach (var seed in BaseDigest.Keys)
        {
            var a = swarmless.All.Where(x => x.Seed == seed).Select(x => x.Zone).ToList();
            var b = live.All.Where(x => x.Seed == seed).Select(x => x.Zone).ToList();
            Assert.Empty(a.SelectMany(z => z.AsteroidBelts.Where(x => x.Value.Data.RidesRail)));
            Assert.Equal(BaseDigest[seed], ZoneFingerprint.Of(a, _ => false, _ => false, true));
            string Swarmed(Zone z) => ZoneFingerprint.Of(new[] { z }, SwarmRails(z).Contains, IsSwarm, false);
            for (var n = 0; n < a.Count; n++)
            {
                Assert.Equal(ZoneFingerprint.Of(new[] { a[n] }, _ => false, _ => false, false), Swarmed(b[n]));
                swarms += SwarmRails(b[n]).Count;
                Assert.True(b[n].PopulatedRadius >= a[n].PopulatedRadius, $"seed {seed}: a swarm shrank the populated radius");
                if (b[n].PopulatedRadius > a[n].PopulatedRadius) grew++;
            }
        }
        _out.WriteLine($"zones {live.All.Count}, swarms {swarms}, populated radius grown by a swarm's outer rock in {grew}");
        Assert.True(swarms > 0);
    }

    // A pack saved before key 11 existed holds the belt as an 11-slot array; it reads as the annulus it was.
    [Fact]
    public void ABeltSavedBeforeTheFlagIsAnAnnulus()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);
        var saved = MessagePackSerializer.Serialize(new AsteroidBeltData { Mass = 5, Asteroids = new[] { new Asteroid { Distance = 3, Phase = .2f } } }, options);
        Assert.Equal(0x9C, saved[0]);  // a fixarray of 12 slots, keys 0-11
        Assert.Equal(0xC2, saved[^1]); // its last slot, RidesRail, false
        var old = saved[..^1];
        old[0] = 0x9B;
        var belt = MessagePackSerializer.Deserialize<AsteroidBeltData>(old, options);
        Assert.False(belt.RidesRail);
        Assert.Equal(3f, belt.Asteroids[0].Distance);
        Assert.Equal(7f, AsteroidBelt.Extent(belt, 7f));
        Assert.Equal(3f, AsteroidBelt.Extent(belt, 2f));
        var swarm = MessagePackSerializer.Deserialize<AsteroidBeltData>(MessagePackSerializer.Serialize(
            new AsteroidBeltData { Asteroids = belt.Asteroids, RidesRail = true }, options), options);
        Assert.True(swarm.RidesRail);
        Assert.Equal(2f, AsteroidBelt.Extent(swarm, 2f));
    }
}
