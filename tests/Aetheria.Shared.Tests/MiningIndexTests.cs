/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using float4 = CultMath.float4;

// Mining index (docs/aetheria-release-map.md, L3-L5; Soul F1, operator 2026-10-01: "Fix the belt freeze"): the zone's
// targeting index answers range and best-first queries from providers, and a query near a few rocks examines few
// rocks however large the belt. Every answer is compared with a test-local scan of every rock.
public sealed partial class MiningCut3Tests
{
    private static void ChunksWithin(Scene s, float2 from, float range, List<ChunkId> into)
    {
        var candidates = new List<TargetCandidate>();
        s.Zone.Targets.Within(new TargetSearch(from, range), candidates);
        into.Clear();
        into.AddRange(candidates.Where(c => c.Target.Chunk.HasValue).Select(c => c.Target.Chunk.Value));
    }

    private static IEnumerable<ChunkId> AllChunks(Scene s) =>
        s.Belts.SelectMany(b => Enumerable.Range(0, ((AsteroidBeltData) s.Zone.Planets[b]).Asteroids.Length).Select(i => new ChunkId(b, i)));

    // Rocks spread evenly over the annulus [inner, outer]: `count` of them at the same density per area as any other
    // call with the same density.
    private static Asteroid[] Annulus(System.Random rng, int count, float inner, float outer) =>
        Enumerable.Range(0, count).Select(_ =>
            Rock(sqrt(inner * inner + (float) rng.NextDouble() * (outer * outer - inner * inner)), (float) rng.NextDouble(),
                (float) rng.NextDouble())).ToArray();

    // A ship at `at` with one sensor of the given sensitivity and curve, and one gun of `reach`.
    private Ship Observer(Scene s, float2 at, float reach, float sensitivity, BezierCurve curve)
    {
        var eye = Gear($"Eye {Guid.NewGuid():N}", new SensorData
        {
            Sensitivity = Constant(sensitivity),
            SensitivityCurve = curve,
            PingBoost = Constant(.02f),
            PingEnergy = Constant(0f),
            PingVisibility = Constant(5f),
            PingRange = Constant(300f),
            PingCooldown = Constant(4f)
        });
        s.Cache.Upsert(eye);
        var gun = Gear($"Gun {reach}", new InstantWeaponData { Range = Constant(reach) });
        s.Cache.Upsert(gun);
        var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
        Assert.True(ship.TryEquip(Mint(s, eye)));
        Assert.True(ship.TryEquip(Mint(s, gun)));
        s.Zone.Entities.Add(ship);
        Hold(ship, at);
        ship.LookDirection = float3(0, 0, 1);
        ship.Activate();
        Tick((ship, at)); // equipment comes online and weapons read their ranges on the first tick
        return ship;
    }

    // Within's chunks, at random positions and times -- including after time jumps that shear every band past its
    // re-key threshold many times over -- equal the scan of every rock, broken rocks left out.
    [Fact]
    public void WithinIsExactlyTheBruteForceSet()
    {
        var kind = Kind("Asteroid", 4f);
        var rng = new System.Random(5);
        var centre = float2(-300, 820);
        var s = BuildSceneAt(centre, 5000f, 1234.5, null, new[] { kind },
            Belt(kind, Annulus(rng, 3000, 200f, 900f)), Belt(kind, Annulus(rng, 700, 1000f, 1060f)));
        for (var i = 0; i < 40; i++) Assert.True(s.Zone.Wear(new ChunkId(s.Belts[0], i * 73), 1e6f));

        var found = new List<ChunkId>();
        var nonEmpty = 0;
        foreach (var step in new[] { 0f, 3f, 250f, 4e4f, 9e5f, 1.7f, 3e6f })
        {
            if (step > 0f) s.Zone.Update(step);
            for (var q = 0; q < 60; q++)
            {
                var angle = (float) (rng.NextDouble() * 2 * PI);
                var from = centre + (float) rng.NextDouble() * 1200f * float2(cos(angle), sin(angle));
                var range = (float) rng.NextDouble() * 250f;
                ChunksWithin(s, from, range, found);
                var expected = AllChunks(s).Where(c => s.Zone.ChunkExists(c) && length(At(s, c) - from) <= range).ToList();
                Assert.Equal(expected.Count, found.Count);
                Assert.True(expected.ToHashSet().SetEquals(found), $"step {step}, query {q}: {expected.Count} expected, {found.Count} found");
                if (expected.Count > 0) nonEmpty++;
            }
        }
        Assert.True(nonEmpty > 200, $"degenerate queries: {nonEmpty} non-empty");
    }

    // 4,000 rocks over 300..1300 about `centre`, and a second sun off the belt centre, so the light bound's bearing to
    // a sun matters.
    private Scene TwoSunBelt(System.Random rng, float2 centre)
    {
        var kind = Kind("Asteroid", 4f);
        return BuildSceneAt(centre, 1500f, 0, new[] { centre + float2(900, 400) }, new[] { kind }, Belt(kind, Annulus(rng, 4000, 300f, 1300f)));
    }

    // Every bound a belt region states holds for every rock it hands back: distance, bearing and visibility.
    [Fact]
    public void BeltRegionBoundsHoldForEveryRock()
    {
        var rng = new System.Random(8);
        var centre = float2(150, -60);
        var s = TwoSunBelt(rng, centre);
        var data = (AsteroidBeltData) s.Zone.Planets[s.Belts[0]];
        var belt = new BeltTargets(s.Zone, s.Belts[0], data, s.Zone.Settings, 0);
        var regions = new List<TargetRegion>();
        var candidates = new List<TargetCandidate>();
        var checkedRocks = 0;
        foreach (var step in new[] { 0f, 50f, 2e5f })
        {
            if (step > 0f) s.Zone.Update(step);
            for (var q = 0; q < 40; q++)
            {
                // Every fourth search starts near the centre and reaches the whole belt, so sectors across the
                // searcher's antipode are bounded too.
                var whole = q % 4 == 0;
                var angle = (float) (rng.NextDouble() * 2 * PI);
                var from = centre + (float) rng.NextDouble() * (whole ? 120f : 1500f) * float2(cos(angle), sin(angle));
                var search = new TargetSearch(from, whole ? 1500f : 50f + (float) rng.NextDouble() * 400f);
                regions.Clear();
                belt.Regions(search, regions);
                foreach (var region in regions)
                {
                    candidates.Clear();
                    belt.Candidates(search, region, candidates);
                    foreach (var c in candidates)
                    {
                        var to = c.Position - from;
                        var d = length(to);
                        Assert.InRange(d, region.Nearest, region.Farthest);
                        var off = abs(atan2(to.y, to.x) - region.BearingCentre);
                        off = min(off, 2 * PI - off);
                        Assert.True(off <= region.BearingHalfWidth, $"bearing {off} outside {region.BearingHalfWidth}");
                        Assert.True(s.Zone.ChunkVisibility(c.Target.Chunk.Value) <= region.MaxVisibility);
                        checkedRocks++;
                    }
                }
            }
        }
        Assert.True(checkedRocks > 5000, $"only {checkedRocks} rocks checked");
    }

    // The visibility bound never drops a visible rock: over a belt the sun lights unevenly, at two sensor
    // sensitivities and two curve shapes (one whose tangents overshoot its keys), VisibleChunksInReach equals the scan
    // of every rock in reach that ChunkVisible accepts -- and the bound did prune some.
    [Fact]
    public void VisibilityPruningNeverDropsAVisibleChunk()
    {
        var kind = Kind("Asteroid", 4f);
        var rng = new System.Random(13);
        // The sun at the belt centre, its light gone by 750: the belt runs from bright to dark.
        var s = BuildSceneAt(float2.zero, 1500f, 0, null, new[] { kind }, Belt(kind, Annulus(rng, 4000, 200f, 1200f)));
        var overshoot = new BezierCurve { Keys = new[] { new float4(0, .4f, 0, 3f), new float4(1, .1f, -2f, 0) } };
        var offered = new List<ChunkId>();
        var visibleSeen = 0;
        var pruned = 0;
        foreach (var sensitivity in new[] { 3f, .05f })
        foreach (var curve in new[] { Falloff(), overshoot })
        for (var q = 0; q < 8; q++)
        {
            var angle = (float) (rng.NextDouble() * 2 * PI);
            var at = (200f + (float) rng.NextDouble() * 1000f) * float2(cos(angle), sin(angle));
            var observer = Observer(s, at, 350f, sensitivity, curve);
            var before = s.Zone.Targets.Examined;
            observer.VisibleChunksInReach(offered);
            var examined = s.Zone.Targets.Examined - before;
            var inReach = AllChunks(s).Where(c => s.Zone.ChunkExists(c) && length(At(s, c) - at) <= 350f).ToList();
            var expected = inReach.Where(observer.ChunkVisible).ToList();
            Assert.True(expected.ToHashSet().SetEquals(offered), $"sensitivity {sensitivity}: {expected.Count} visible, {offered.Count} offered");
            visibleSeen += expected.Count;
            if (examined < inReach.Count) pruned++;
            s.Zone.Entities.Remove(observer);
        }
        Assert.True(visibleSeen > 50 && pruned > 4, $"degenerate: {visibleSeen} visible, {pruned} queries pruned");
    }

    // The operator's rule for F1: the cost of a key press must not grow with belt size. Belts of 30k and 300k rocks at
    // one density, the observer in the same neighbourhood: one VisibleChunksInReach examines about as many rocks in
    // both, and under 2% of the larger belt.
    [Fact]
    public void AQueryNearAFewRocksExaminesFewRocks()
    {
        var kind = Kind("Asteroid", 4f);
        // 100 square units per rock: 30k rocks fill 2000..2226, 300k fill 2000..3681.
        long Examine(int count)
        {
            var outer = sqrt(2000f * 2000f + count * 100f / PI);
            var s = BuildSceneAt(float2.zero, 20000f, 0, null, new[] { kind }, Belt(kind, Annulus(new System.Random(21), count, 2000f, outer)));
            var at = 2100f * float2(cos(1f), sin(1f));
            var observer = Observer(s, at, 150f, 3f, Falloff());
            var offered = new List<ChunkId>();
            var before = s.Zone.Targets.Examined;
            observer.VisibleChunksInReach(offered);
            Assert.NotEmpty(offered);
            var cost = s.Zone.Targets.Examined - before;
            // Time passes, too little to shear a band past re-keying: the arcs widen by their drift only.
            s.Zone.Update(5f);
            Tick((observer, at));
            before = s.Zone.Targets.Examined;
            observer.VisibleChunksInReach(offered);
            Assert.True(s.Zone.Targets.Examined - before < 2 * cost, $"after 5 s, examined {s.Zone.Targets.Examined - before} against {cost}");
            return cost;
        }
        var small = Examine(30_000);
        var large = Examine(300_000);
        Assert.True(large < 2 * small && small < 2 * large, $"examined {small} at 30k, {large} at 300k");
        Assert.True(large < 300_000 / 50, $"examined {large} of 300k");
    }

    // A belt outside every sun's light: VisibleChunksInReach examines none of its rocks, while Within with no
    // visibility bound still finds them.
    [Fact]
    public void DarkBandsAreSkippedWhole()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildSceneAt(float2.zero, 1000f, 0, null, new[] { kind }, Belt(kind, Annulus(new System.Random(3), 2000, 1500f, 1600f)));
        var at = float2(1550f, 0f);
        var observer = Observer(s, at, 300f, 3f, Falloff());
        var offered = new List<ChunkId>();
        var before = s.Zone.Targets.Examined;
        observer.VisibleChunksInReach(offered);
        Assert.Equal(0, s.Zone.Targets.Examined - before);
        Assert.Empty(offered);
        var found = new List<ChunkId>();
        ChunksWithin(s, at, 300f, found);
        Assert.NotEmpty(found);
    }

    // After a time jump that shears every band, one query re-sorts at most the budget of bands and still returns the
    // scan's answer: the bands it did not re-sort are searched through their widened arcs.
    [Fact]
    public void ReKeyingIsBoundedPerQuery()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildSceneAt(float2.zero, 5000f, 1e5, null, new[] { kind }, Belt(kind, Annulus(new System.Random(17), 256 * 40, 200f, 1200f)));
        var data = (AsteroidBeltData) s.Zone.Planets[s.Belts[0]];
        var belt = new BeltTargets(s.Zone, s.Belts[0], data, s.Zone.Settings, 1e5);
        var whole = new TargetSearch(float2(700, 0), 2000f);
        var regions = new List<TargetRegion>();
        // Ten seconds shear no band past the limit, however late the zone's clock: nothing is re-keyed.
        s.Zone.Update(10f);
        var before = belt.Rekeys;
        belt.Regions(whole, regions);
        Assert.Equal(0, belt.Rekeys - before);
        regions.Clear();
        s.Zone.Update(5e6f);
        before = belt.Rekeys;
        belt.Regions(whole, regions);
        Assert.Equal(8, belt.Rekeys - before);
        regions.Clear();
        belt.Regions(whole, regions);
        Assert.Equal(16, belt.Rekeys - before);

        var found = new List<ChunkId>();
        foreach (var (from, range) in new[] { (float2(700, 0), 120f), (float2(-300, 500), 300f), (float2(0, -1100), 90f) })
        {
            ChunksWithin(s, from, range, found);
            var expected = AllChunks(s).Where(c => length(At(s, c) - from) <= range).ToList();
            Assert.NotEmpty(expected);
            Assert.True(expected.ToHashSet().SetEquals(found) && expected.Count == found.Count);
        }
    }

    // Perception stays Entity's: an entity the observer has not detected is never offered, even inside reach; one it
    // has detected is; and a search with no observer offers no entities at all.
    [Fact]
    public void EntitiesComeFromPerception()
    {
        var s = BuildScene();
        var here = float2(300, 200);
        var observer = SpawnShip(s, here);
        var seen = SpawnShip(s, here + float2(40, 0));
        var unseen = SpawnShip(s, here + float2(0, 40));
        var candidates = new List<TargetCandidate>();
        s.Zone.Targets.Within(new TargetSearch(here, 500f, observer), candidates);
        Assert.Empty(candidates);

        observer.VisibleEntities.Add(seen);
        s.Zone.Targets.Within(new TargetSearch(here, 50f, observer), candidates);
        Assert.Equal(new[] { new TargetRef(seen) }, candidates.Select(c => c.Target));
        Assert.Equal(here + float2(40, 0), candidates[0].Position);

        s.Zone.Targets.Within(new TargetSearch(here, 20f, observer), candidates);
        Assert.Empty(candidates);
        s.Zone.Targets.Within(new TargetSearch(here, 500f), candidates);
        Assert.Empty(candidates);
        Assert.DoesNotContain(unseen, observer.VisibleEntities);
    }

    private readonly struct NearestChunk : ITargetKey
    {
        private readonly float2 _from;
        public NearestChunk(float2 from) => _from = from;
        public float Bound(in TargetRegion region) => region.Nearest;
        public bool Key(in TargetCandidate candidate, out float key)
        {
            key = length(candidate.Position - _from);
            return candidate.Target.Chunk.HasValue;
        }
        public bool Precedes(in TargetCandidate a, in TargetCandidate b) => a.Target.Chunk.Value.Index < b.Target.Chunk.Value.Index;
    }

    // Best by distance is the scan's nearest rock in reach, and examines fewer rocks than the range query.
    [Fact]
    public void BestIsTheBruteForceNearest()
    {
        var kind = Kind("Asteroid", 4f);
        var rng = new System.Random(29);
        var s = BuildSceneAt(float2.zero, 5000f, 0, null, new[] { kind }, Belt(kind, Annulus(rng, 5000, 300f, 1100f)));
        s.Zone.Update(777f);
        long bestCost = 0, withinCost = 0;
        var candidates = new List<TargetCandidate>();
        for (var q = 0; q < 100; q++)
        {
            var angle = (float) (rng.NextDouble() * 2 * PI);
            var from = (float) rng.NextDouble() * 1300f * float2(cos(angle), sin(angle));
            var search = new TargetSearch(from, 40f + (float) rng.NextDouble() * 300f);
            var expected = AllChunks(s).Where(c => length(At(s, c) - from) <= search.Reach)
                .OrderBy(c => length(At(s, c) - from)).ThenBy(c => c.Index).ToList();
            var before = s.Zone.Targets.Examined;
            var found = s.Zone.Targets.Best(search, new NearestChunk(from), out var best);
            bestCost += s.Zone.Targets.Examined - before;
            Assert.Equal(expected.Count > 0, found);
            if (found) Assert.Equal(expected[0], best.Target.Chunk.Value);
            before = s.Zone.Targets.Examined;
            s.Zone.Targets.Within(search, candidates);
            withinCost += s.Zone.Targets.Examined - before;
        }
        Assert.True(bestCost * 2 < withinCost, $"best examined {bestCost}, within {withinCost}");
    }

    // The hull bound holds: no sample of a curve with overshooting and stepped segments leaves HullRange.
    [Fact]
    public void ACurveStaysWithinItsHullRange()
    {
        var curve = new BezierCurve
        {
            Keys = new[]
            {
                new float4(0, .4f, 0, 6f), new float4(.5f, .1f, -2f, float.PositiveInfinity), new float4(.7f, .9f, 0, -4f),
                new float4(1, .2f, 1f, 0)
            }
        };
        var range = curve.HullRange();
        for (var i = -10; i <= 1010; i++)
        {
            var value = curve.Evaluate(i / 1000f);
            Assert.InRange(value, range.x, range.y);
        }
        Assert.True(range.y > 1f, $"hull max {range.y} does not reach past the keys");
    }

    // Rocks of one Size spread evenly over the annulus [inner, outer].
    private static Asteroid[] AnnulusOfSize(System.Random rng, int count, float inner, float outer, float size) =>
        Enumerable.Range(0, count).Select(_ =>
            Rock(sqrt(inner * inner + (float) rng.NextDouble() * (outer * outer - inner * inner)), (float) rng.NextDouble(), size)).ToArray();

    // A belt's regions stay near the search's arc: of the regions a small search gets, few lie wholly past its reach
    // (the arc window is not widened), and some do (the sector's nearest distance is not loosened toward zero).
    // Measured on a4e8e147 over these 120 searches, as the share of regions with Nearest > Reach: unmutated 3.97%
    // (15 of 378). Window widened: BeltTargets.cs:111 asin * 2pi 82.96%, :111 asin(reach * distance) 85.89%, :131
    // 2 / half 85.89%, :140 Math.Max(to, 1) 75.29%. Nearest loosened: :247 / 2 * pi 0.00%. The ceiling, 20%, is 5.0x
    // the unmutated share and 3.8x below the least widened mutant; the floor, 2%, is half the unmutated share.
    [Fact]
    public void RegionsStayNearTheSearchArc()
    {
        var rng = new System.Random(8);
        var s = TwoSunBelt(rng, float2(150, -60));
        var data = (AsteroidBeltData) s.Zone.Planets[s.Belts[0]];
        var belt = new BeltTargets(s.Zone, s.Belts[0], data, s.Zone.Settings, 0);
        var regions = new List<TargetRegion>();
        var searches = new System.Random(31);
        int total = 0, past = 0;
        // At the key time, and after 5 s, when the arcs have widened by their drift.
        foreach (var step in new[] { 0f, 5f })
        {
            if (step > 0f) s.Zone.Update(step);
            for (var q = 0; q < 60; q++)
            {
                var rock = At(s, new ChunkId(s.Belts[0], searches.Next(data.Asteroids.Length)));
                var angle = (float) (searches.NextDouble() * 2 * PI);
                var from = rock + (float) searches.NextDouble() * 20f * float2(cos(angle), sin(angle));
                var search = new TargetSearch(from, 30f + (float) searches.NextDouble() * 50f);
                regions.Clear();
                belt.Regions(search, regions);
                total += regions.Count;
                past += regions.Count(r => r.Nearest > search.Reach);
            }
        }
        var share = (double) past / total;
        Assert.True(share < .2, $"{past} of {total} regions lie past reach: the search window has widened");
        Assert.True(share > .02, $"{past} of {total} regions lie past reach: region distances have loosened");
    }

    // The visibility bound prunes: a belt of rocks all of the largest Size, so the belt-wide radius bound is exact per
    // rock, lit by a sun at its centre whose light ends inside it. Summed over the spots, one VisibleChunksInReach
    // each, the rocks examined are a small share of the rocks in reach.
    // Measured on a4e8e147, examined over in reach: unmutated 0.162 (231 of 1,429); BeltTargets.cs:181 radius * cell
    // size, the bound 16x too loose, 0.434 (620). The ceiling, 0.265, is 1.64x the unmutated share and 1.64x below
    // the mutant's. (:247 / 2 * pi, which loosens region distances, also fails it at 0.318.) The spots are where
    // light, not reach, decides which regions survive. In a measurement at 450 units, sector geometry decided
    // (94 examined unmutated, 105 mutated), and past the light nothing is lit either way.
    // The cut asked for Examined <= 2x the rocks in reach within their own detection distance. Unmutated it fails: 64
    // examined for 5 such rocks at 250 units. A region is 32 rocks of a band spanning an eighth of the orbit, so the
    // segment size sets Examined, not the detectable count.
    [Fact]
    public void VisibilityBoundPrunesADimBelt()
    {
        var kind = Kind("Asteroid", 4f);
        var rng = new System.Random(13);
        var s = BuildSceneAt(float2.zero, 750f, 0, null, new[] { kind }, Belt(kind, AnnulusOfSize(rng, 3000, 200f, 1200f, 1f)));
        var all = AllChunks(s).ToList();
        // The sensitivity at which the brightest rock is detectable 120 units away.
        var brightest = all.Max(c => s.Zone.ChunkVisibility(c));
        var unit = Observer(s, float2(5000, 0), 350f, 1f, Falloff());
        var sensitivity = 120f / (brightest * unit.DetectionReachPerVisibility());
        s.Zone.Entities.Remove(unit);

        var offered = new List<ChunkId>();
        long examined = 0, inReach = 0;
        var spots = new System.Random(41);
        foreach (var radius in new[] { 250f, 350f, 550f, 650f, 700f, 740f })
        {
            var angle = (float) (spots.NextDouble() * 2 * PI);
            var at = radius * float2(cos(angle), sin(angle));
            var observer = Observer(s, at, 350f, sensitivity, Falloff());
            Assert.InRange(brightest * observer.DetectionReachPerVisibility(), 80f, 150f);
            var before = s.Zone.Targets.Examined;
            observer.VisibleChunksInReach(offered);
            examined += s.Zone.Targets.Examined - before;
            inReach += all.Count(c => length(At(s, c) - at) <= 350f);
            s.Zone.Entities.Remove(observer);
        }
        Assert.True(examined < .265 * inReach, $"examined {examined} of {inReach} rocks in reach: the visibility bound has loosened");
    }

    // A rock exactly at reach is found, and Within equals the scan of every rock in reach. Two belts, one spread over
    // 50..6000 and one thin and dense over 999..1001, under the fixture's orbit periods and the game's (period =
    // distance), across time steps up to 3.3e7 s. Every third search starts from a belt's outermost rock, radially
    // outward and over 50 units past the belt's edge, so the rock lies on its band's outer bound as well as on the
    // search's reach.
    // Measured on a4e8e147: unmutated, none of the 240 searches miss. BeltTargets.cs:110 Reach - slack misses 94
    // (80 of them outer). :109 Outer - distance misses all 80 outer searches. That mutant shortens reach by 0.005 to
    // 0.03 units only beyond a belt's edge, which drops a rock only when the rock lies on a region's bound. With
    // random directions, the cut's draft for the outer third, it missed none of 80 here and none of Soul's 640.
    [Fact]
    public void ARockExactlyAtReachIsFound()
    {
        var kind = Kind("Asteroid", 4f);
        var rng = new System.Random(101);
        var centre = float2(40, -70);
        var spread = Enumerable.Range(0, 700).Select(_ => Rock(50f + (float) rng.NextDouble() * 5950f, (float) rng.NextDouble(), (float) rng.NextDouble())).ToArray();
        var s = BuildSceneAt(centre, 20000f, 777.25, null, new[] { kind }, Belt(kind, spread), Belt(kind, Annulus(rng, 3000, 999f, 1001f)));
        var game = PlanetSettings(20000f);
        game.OrbitPeriod = new ExponentialCurve { Multiplier = 1f, Exponent = 1f, Constant = 0f };
        var searches = 0;
        foreach (var zone in new[] { s.Zone, new Zone(s.Items, game, s.Pack, new GalaxyZone { Name = "Game periods", Owner = null }, null) })
        foreach (var step in new[] { 0f, 13f, 1e4f, 3.3e7f })
        {
            if (step > 0f) zone.Update(step);
            for (var q = 0; q < 30; q++)
            {
                var field = s.Belts[q % 2];
                var rocks = ((AsteroidBeltData) zone.Planets[field]).Asteroids;
                var edge = q % 3 == 0;
                var index = edge ? Enumerable.Range(0, rocks.Length).OrderBy(i => rocks[i].Distance).Last() : rng.Next(rocks.Length);
                var rock = zone.ChunkPose(field, index).xy;
                var angle = (float) (rng.NextDouble() * 2 * PI);
                var direction = edge ? normalize(rock - centre) : float2(cos(angle), sin(angle));
                var from = rock + (edge ? 60f + (float) rng.NextDouble() * 240f : 1f + (float) rng.NextDouble() * 299f) * direction;
                var reach = length(rock - from);
                var candidates = new List<TargetCandidate>();
                zone.Targets.Within(new TargetSearch(from, reach), candidates);
                var found = candidates.Where(c => c.Target.Chunk.HasValue).Select(c => c.Target.Chunk.Value).ToList();
                Assert.Contains(new ChunkId(field, index), found);
                var expected = s.Belts.SelectMany(b => Enumerable.Range(0, ((AsteroidBeltData) zone.Planets[b]).Asteroids.Length).Select(i => new ChunkId(b, i)))
                    .Where(c => zone.ChunkExists(c) && length(zone.ChunkPose(c.Field, c.Index).xy - from) <= reach).ToList();
                Assert.True(expected.ToHashSet().SetEquals(found) && expected.Count == found.Count,
                    $"step {step}, search {q}: {expected.Count} expected, {found.Count} found");
                searches++;
            }
        }
        Assert.Equal(240, searches);
    }
}
