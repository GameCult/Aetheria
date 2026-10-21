using System;
using System.Linq;
using GameCult.Caching;
using Xunit;
using Xunit.Abstractions;

// Laned belts (rulings belt-shapes-direction, lanes-two-sided-visual-margin; map B60): a belt candidate that kept its body carries
// an annulus on a copy of its rail that fills the body's slot less a lane LaneBodyRadii body radii a side, with rocks on both
// sides; it is drawn from a stream of its own and sits inside the slot, so it moves nothing else in the zone. Seeds 1-6, authored
// settings, against the same seeds drawing no lane.
public sealed class LanedBeltTests : IClassFixture<LanedBeltTests.Samples>
{
    private const uint First = 1, Last = 6;
    private const float Tolerance = 1e-3f;

    public sealed class Samples : IDisposable
    {
        public readonly ChildrenWithinReachTests.Zones Authored = new ChildrenWithinReachTests.Zones(First, Last, null);
        public readonly ChildrenWithinReachTests.Zones Laneless = new ChildrenWithinReachTests.Zones(First, Last, z => z.LaneProbability = 0);

        public void Dispose()
        {
            Authored.Dispose();
            Laneless.Dispose();
        }
    }

    private readonly Samples _samples;
    private readonly ITestOutputHelper _out;

    public LanedBeltTests(Samples samples, ITestOutputHelper output)
    {
        _samples = samples;
        _out = output;
    }

    private static CultRecordKey KeyOf(Zone zone, Orbit orbit) => zone.Orbits.First(o => o.Value == orbit).Key;

    private static BodyData BodyAt(Zone zone, Orbit orbit) =>
        zone.Planets.Values.Single(b => !(b is AsteroidBeltData) && zone.Orbits[b.Orbit.Key] == orbit);

    // The lane's half-width, spelled from the settings: LaneBodyRadii of the host's body radius.
    private float HalfWidth(Zone zone, float hostMass) => _samples.Authored.Settings.LaneBodyRadii * zone.Settings.BodyRadius.Evaluate(hostMass);

    [Fact]
    public void ALaneMovesNothingElseInTheZone()
    {
        var a = _samples.Laneless.All;
        var b = _samples.Authored.All;
        Assert.Equal(a.Count, b.Count);
        var lanes = 0;
        for (var n = 0; n < a.Count; n++)
        {
            Assert.Equal(a[n].Seed, b[n].Seed);
            Assert.Empty(BeltShapes.Lanes(a[n].Zone));
            var zone = b[n].Zone;
            var laned = BeltShapes.Lanes(zone).ToList();
            lanes += laned.Count;
            bool SkipOrbit(CultRecordKey k) => laned.Any(l => l.Rail == zone.Orbits[k]);
            bool SkipBody(BodyData body) => laned.Any(l => l.Belt.Data == body);
            Assert.Equal(ZoneFingerprint.Of(new[] { a[n].Zone }, _ => false, _ => false, true), ZoneFingerprint.Of(new[] { zone }, SkipOrbit, SkipBody, true));
        }
        _out.WriteLine($"zones {b.Count}, lanes {lanes}");
        Assert.True(lanes > 0, $"no lane in seeds {First}-{Last}");
    }

    [Fact]
    public void ALaneIsEmptyAndBothSidesHoldRocks()
    {
        var s = _samples.Authored.Settings;
        var q = 1 / (1 - GravityField.Reach / 2);
        var lanes = 0;
        foreach (var (seed, zone) in _samples.Authored.All)
        {
            var placed = ChildrenWithinReachTests.Placed(zone);
            foreach (var (host, _, belt) in BeltShapes.Lanes(zone))
            {
                lanes++;
                Assert.False(belt.Data.RidesRail);
                Assert.Equal(0f, belt.Data.Mass);
                var hostMass = BodyAt(zone, host).Mass;
                var d = host.Data.Distance;
                var e = HalfWidth(zone, hostMass);
                Assert.Equal((int) s.AsteroidCount.Evaluate(hostMass * d), belt.Data.Asteroids.Length);

                // The host's slot, spelled from its siblings, its parent's Roche margin and its parent's window.
                var parent = host.Data.Parent.Key;
                var sibling = ChildrenWithinReachTests.ChildrenOf(zone, parent, placed);
                var at = sibling.IndexOf(KeyOf(zone, host));
                Assert.True(at >= 0 && at < sibling.Count - 1, $"seed {seed}: a lane host is its parent's outermost child");
                var parentBody = zone.Planets.Values.FirstOrDefault(p => !(p is AsteroidBeltData) && p.Orbit.Key.Equals(parent));
                var roche = parentBody == null ? 0f : 1.5f * zone.Settings.RocheLimit.Evaluate(parentBody.Mass);
                var innerBound = Math.Max(roche, at > 0 ? zone.Orbits[sibling[at - 1]].Data.Distance * q : 0f);
                var grand = zone.Orbits[parent].Data.Parent;
                var window = grand.IsSet()
                    ? GravityField.Gamma * GravityField.Reach * zone.Orbits[parent].Data.Distance / Math.Max(GravityField.Beta, 1 + GravityField.Reach)
                    : float.PositiveInfinity;
                var outerBound = Math.Min(window, zone.Orbits[sibling[at + 1]].Data.Distance / q);

                var rocks = belt.Data.Asteroids.Select(r => r.Distance).ToList();
                var inner = rocks.Where(r => r < d).ToList();
                var outer = rocks.Where(r => r > d).ToList();
                _out.WriteLine($"seed {seed}: lane at {d} half-width {e}, slot [{innerBound}, {outerBound}], rocks {inner.Count} inside, {outer.Count} outside");
                Assert.True(rocks.All(r => Math.Abs(r - d) >= e - 1e-5f * d), $"seed {seed}: a rock sits {rocks.Min(r => Math.Abs(r - d))} from a host at {d}, inside a lane of {e}");
                Assert.True(inner.Count > 0 && outer.Count > 0, $"seed {seed}: a lane at {d} holds {inner.Count} rocks inside and {outer.Count} outside");
                Assert.True(d - e - innerBound >= e * (1 - Tolerance), $"seed {seed}: the inner band [{innerBound}, {d - e}] is narrower than the lane's half-width {e}");
                Assert.True(outerBound - d - e >= e * (1 - Tolerance), $"seed {seed}: the outer band [{d + e}, {outerBound}] is narrower than the lane's half-width {e}");
                Assert.True(inner.Min() >= innerBound * (1 - Tolerance), $"seed {seed}: a rock at {inner.Min()} reaches inside the slot's inner bound {innerBound}");
                Assert.True(outer.Max() <= outerBound * (1 + Tolerance), $"seed {seed}: a rock at {outer.Max()} reaches past the slot's outer bound {outerBound}");
            }
        }
        Assert.True(lanes > 0, $"no lane in seeds {First}-{Last}");
    }

    [Fact]
    public void ALaneHostIsALightBodyWithNoOtherShape()
    {
        var s = _samples.Authored.Settings;
        var lanes = 0;
        foreach (var (seed, zone) in _samples.Authored.All)
        {
            var laned = BeltShapes.Lanes(zone).ToList();
            foreach (var (host, _, _) in laned)
            {
                lanes++;
                var body = BodyAt(zone, host);
                Assert.True(body.GetType() == typeof(PlanetData) && body.Mass < s.BeltMassCeiling, $"seed {seed}: a lane host is a {body.GetType().Name} of mass {body.Mass}");
                var key = KeyOf(zone, host);
                Assert.DoesNotContain(zone.Orbits.Values, o => o.Data.Parent.IsSet() && o.Data.Parent.Key.Equals(key));
                // A swarm's rail is a copy of its host's too: no lane host carries one.
                Assert.DoesNotContain(zone.AsteroidBelts.Where(b => b.Value.Data.RidesRail).Select(b => zone.Orbits[zone.Planets[b.Key].Orbit.Key].Data),
                    r => r.Parent.Key.Equals(host.Data.Parent.Key) && r.Distance == host.Data.Distance && r.Phase == host.Data.Phase);
            }
            // One belt shape per parent: an annulus (a belt on a placed slot) or a lane, never both, never two lanes.
            var annuli = zone.AsteroidBelts.Keys.Select(k => zone.Planets[k].Orbit.Key).Where(o => !BeltShapes.CopiedRail(zone, o))
                .Select(o => zone.Orbits[o].Data.Parent.Key);
            var doubled = annuli.Concat(laned.Select(l => l.Host.Data.Parent.Key)).GroupBy(p => p).Count(g => g.Count() > 1);
            Assert.True(doubled == 0, $"seed {seed}: {doubled} parents carry two belt shapes");
        }
        Assert.True(lanes > 0, $"no lane in seeds {First}-{Last}");
    }

    // A belt's field kind is chosen at generation and saved with it (Mining Cut 3, Q16 B): every belt the generator writes,
    // annulus, swarm or lane, carries one before any Zone reads it (finding ensure-unpinned).
    [Fact]
    public void EveryBeltIsKindedAtGeneration()
    {
        var zones = _samples.Authored;
        _out.WriteLine($"belts {zones.Belts}, unkinded at generation {zones.UnkindedAtGeneration}");
        Assert.True(zones.All.Any(z => z.Zone.AsteroidBelts.Values.Any(b => b.Data.RidesRail)), "no swarm in the sample");
        Assert.True(zones.All.Any(z => BeltShapes.Lanes(z.Zone).Any()), "no lane in the sample");
        Assert.True(zones.Belts > 20, $"only {zones.Belts} belts generated");
        Assert.Equal(0, zones.UnkindedAtGeneration);
    }
}
