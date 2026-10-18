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

    [Fact]
    public void ASwarmKeepsItsShapeOverAnHour()
    {
        using var zones = new ChildrenWithinReachTests.Zones(First, Last, null);
        var s = zones.Settings;
        int swarms = 0, l4 = 0, l5 = 0;
        var drift = 0f;
        foreach (var (seed, zone) in zones.All)
            foreach (var (host, rail, swarm) in Swarms(zone))
            {
                swarms++;
                var d = host.Data.Distance;
                Assert.Equal(2 * (int) s.TrojanCount.Evaluate(d), swarm.Data.Asteroids.Length);
                int sideA = 0, sideB = 0;
                for (var i = 0; i < swarm.Data.Asteroids.Length; i++)
                {
                    var rock = swarm.Data.Asteroids[i];
                    Assert.True(Math.Abs(rock.Distance / d - 1) <= s.TrojanHalfWidth * 1.0001f, $"seed {seed}: a rock sits {rock.Distance} from a rail at {d}");
                    float Offset(double t) => Wrap(Turn(swarm.Pose(i, t, float2.zero, 1).xy) - Turn(OrbitData.Evaluate((float) frac(t / host.Period + host.Data.Phase))));
                    var at0 = Offset(0);
                    var side = at0 > 0 ? 1f / 6 : -1f / 6;
                    if (side > 0) { l4++; sideA++; } else { l5++; sideB++; }
                    Assert.True(Math.Abs(at0 - side) <= s.TrojanArc / 2 + 1e-4f, $"seed {seed}: a rock sits {at0} turns from its host");
                    drift = Math.Max(drift, Math.Abs(Wrap(Offset(3600) - at0)));
                }
                Assert.Equal(sideA, sideB);
            }
        _out.WriteLine($"swarms {swarms}, rocks at L4 {l4}, L5 {l5}, largest drift against the host over an hour {drift} turns");
        Assert.True(swarms >= 10, $"only {swarms} swarms in seeds {First}-{Last}");
        Assert.True(l4 > 0 && l5 > 0);
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

    // The digests were computed at 67517f40, before this cut, over the zones of each seed with the authored settings: a zone
    // with no swarm is the base zone, and a zone with swarms is the base zone plus them.
    private static readonly Dictionary<uint, string> BaseDigest = new Dictionary<uint, string>
    {
        [1] = "02a2bef42b5d4aeaf1e7004d0ab26620ee570ced95115abf98226ab4e0db23eb",
        [2] = "de231f392e5724ecdce7e746957633bef2724252147ecbb5830db047b635aead",
        [3] = "5c33fe9f748d6b0b34a7bec923216c7f9bbdfdf8f326c59b77fde9ec96ca2bcb",
        [4] = "585284386657e4714ad325cb053a78853c5bcaf7eb1a02cbd4826b0e86ad3593",
        [5] = "4a1aeecad09c61ff7daa43eda40a9c0109dc4b6833439b8400d8c72d939950c8",
        [6] = "7fd3c5844144b73faf05cd6a53fe92274e4455f38da4aaca4624ea13d96a12c3",
    };

    [Fact]
    public void ASwarmMovesNothingElseInTheZone()
    {
        using var swarmless = new ChildrenWithinReachTests.Zones(First, Last, z => z.TrojanProbability = 0);
        using var live = new ChildrenWithinReachTests.Zones(First, Last, null);
        Assert.Equal(swarmless.All.Count, live.All.Count);
        int swarms = 0, grew = 0;
        foreach (var seed in BaseDigest.Keys)
        {
            var a = swarmless.All.Where(x => x.Seed == seed).Select(x => x.Zone).ToList();
            var b = live.All.Where(x => x.Seed == seed).Select(x => x.Zone).ToList();
            Assert.Empty(a.SelectMany(z => z.AsteroidBelts.Where(x => x.Value.Data.RidesRail)));
            Assert.Equal(BaseDigest[seed], ZoneFingerprint.Of(a, _ => false, _ => false));
            string Swarmed(Zone z) => ZoneFingerprint.Of(new[] { z }, SwarmRails(z).Contains, IsSwarm);
            for (var n = 0; n < a.Count; n++)
            {
                Assert.Equal(ZoneFingerprint.Of(new[] { a[n] }, _ => false, _ => false), Swarmed(b[n]));
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
        Assert.Equal(7f, new AsteroidBelt(belt).Extent(7f));
        Assert.Equal(3f, new AsteroidBelt(belt).Extent(2f));
        var swarm = MessagePackSerializer.Deserialize<AsteroidBeltData>(MessagePackSerializer.Serialize(
            new AsteroidBeltData { Asteroids = belt.Asteroids, RidesRail = true }, options), options);
        Assert.True(swarm.RidesRail);
        Assert.Equal(2f, new AsteroidBelt(swarm).Extent(2f));
    }
}
