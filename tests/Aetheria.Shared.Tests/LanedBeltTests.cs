using System;
using System.Linq;
using GameCult.Caching;
using Xunit;
using Xunit.Abstractions;

// Laned belts (rulings belt-shapes-direction, lanes-two-sided-visual-margin, lane-narrow, lane-not-outermost; map B60): a belt
// candidate that kept its body and is not its parent's outermost child carries an annulus on a copy of its rail that fills the
// body's slot less a lane LaneBodyRadii (1.5) body radii a side, with rocks on both sides; it is drawn from a stream of its own and
// sits inside the slot, so it moves nothing else in the zone. Seeds 1-6, authored settings, against the same seeds drawing no lane.
public sealed class LanedBeltTests : IClassFixture<LanedBeltTests.Samples>
{
    private const uint First = 1, Last = 6;
    private const float Tolerance = 1e-3f;

    public sealed class Samples : IDisposable
    {
        public readonly ChildrenWithinReachTests.Zones Authored = new ChildrenWithinReachTests.Zones(First, Last, null);
        public readonly ChildrenWithinReachTests.Zones Laneless = new ChildrenWithinReachTests.Zones(First, Last, z => z.LaneProbability = 0);
        // Every candidate tried for a lane and every eligible host given a swarm, seeds 1-30: the authored sample holds no lane
        // candidate that is a swarm host or shares a parent with another eligible host, so only this one reaches the swarm-host and
        // one-shape-per-parent filters (finding lane-filters-unexercised).
        public readonly ChildrenWithinReachTests.Zones Tuned = new ChildrenWithinReachTests.Zones(1, 30, z => { z.LaneProbability = 1; z.TrojanProbability = 1; });

        public void Dispose()
        {
            Authored.Dispose();
            Laneless.Dispose();
            Tuned.Dispose();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALaneHostIsALightBodyWithNoOtherShape(bool tuned)
    {
        var sample = tuned ? _samples.Tuned : _samples.Authored;
        var s = sample.Settings;
        int lanes = 0, lightChildlessSwarmHosts = 0;
        foreach (var (seed, zone) in sample.All)
        {
            // A swarm host light enough and childless enough to be a lane host: the case the swarm-host filter refuses.
            foreach (var rail in zone.AsteroidBelts.Where(b => b.Value.Data.RidesRail).Select(b => zone.Orbits[zone.Planets[b.Key].Orbit.Key].Data))
            {
                var hostBody = zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).FirstOrDefault(b => zone.Orbits[b.Orbit.Key].Data is var o
                    && o.Parent.Key.Equals(rail.Parent.Key) && o.Distance == rail.Distance && o.Phase == rail.Phase);
                if (hostBody != null && hostBody.Mass < s.BeltMassCeiling
                    && !zone.Orbits.Values.Any(o => o.Data.Parent.IsSet() && o.Data.Parent.Key.Equals(hostBody.Orbit.Key)))
                    lightChildlessSwarmHosts++;
            }
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
        _out.WriteLine($"zones {sample.All.Count}, lanes {lanes}, light childless swarm hosts {lightChildlessSwarmHosts}");
        Assert.True(lanes > 0, "no lane in the sample");
        if (tuned) Assert.True(lightChildlessSwarmHosts > 100, $"only {lightChildlessSwarmHosts} light childless swarm hosts: the swarm-host filter is not reached");
    }

    // LaneRocks puts each rock in a band by the band's area (ruling lanes-two-sided-visual-margin, map B60), not half and half and
    // not by width (finding lane-split-unpinned). A lopsided slot, inner band [10, 18] (area 224) and outer [22, 24] (area 92):
    // area share 0.709, width share 0.8, half 0.5. Over 4000 rocks one sigma of the share is 0.0072, so the area share sits within
    // 4 sigma and the other two lie past 12.
    [Fact]
    public void ALaneSplitsItsRocksByBandArea()
    {
        const float inner = 10f, distance = 20f, lane = 2f, outer = 24f;
        var settings = _samples.Authored.Settings;
        var p = (18.0 * 18 - 10 * 10) / (18.0 * 18 - 10 * 10 + 24 * 24 - 22 * 22);
        int n = 0, k = 0;
        for (uint draw = 1; n < 4000; draw++)
        {
            var random = new CultMath.Random(draw);
            var rocks = ZoneGenerator.LaneRocks(settings, ref random, 1f, distance, lane, inner, outer);
            Assert.NotEmpty(rocks);
            foreach (var rock in rocks)
            {
                Assert.True(rock.Distance >= inner && rock.Distance <= distance - lane || rock.Distance >= distance + lane && rock.Distance <= outer,
                    $"a rock at {rock.Distance} sits outside both bands");
                n++;
                if (rock.Distance < distance) k++;
            }
        }
        var z = (k - n * p) / Math.Sqrt(n * p * (1 - p));
        _out.WriteLine($"rocks {n}, inner {k}, area share {p:F3}, measured {(double) k / n:F3}, z {z:F2}");
        Assert.True(Math.Abs(z) < 4, $"{k} of {n} rocks in the inner band, {(double) k / n:F3} against an area share of {p:F3} (z {z:F2})");
    }

    // An only child is its parent's outermost (ruling lane-not-outermost): Place stretches nothing for it, so its slot runs out to
    // the window and has room for a lane on both sides, yet its lane's outer band would sit past every sibling's extent and raise
    // the parent's support. The host filter refuses it on that ground alone.
    [Fact]
    public void AnOnlyChildHostsNoLane()
    {
        var root = new GeneratorPlanet { Mass = 1e5f };
        var child = ChildrenWithinReachTests.Body(root, 10f);
        var planetSettings = ChildrenWithinReachTests.AuthoredPlanetSettings();
        ZoneGenerator.Place(root, planetSettings, 1e6f, 1e5f);
        var lane = _samples.Authored.Settings.LaneBodyRadii * planetSettings.BodyRadius.Evaluate(child.Mass);
        _out.WriteLine($"only child at {child.Distance}, slot [{child.SlotInner}, {child.SlotOuter}], lane half-width {lane}");
        Assert.True(child.SlotInner <= child.Distance - 2 * lane && child.SlotOuter >= child.Distance + 2 * lane,
            $"the only child's slot [{child.SlotInner}, {child.SlotOuter}] has no room for a lane of {lane} at {child.Distance}");
        Assert.False(ZoneGenerator.LaneHost(child, lane));
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
