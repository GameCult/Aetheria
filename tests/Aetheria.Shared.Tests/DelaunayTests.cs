using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CultMath;
using Xunit;
using static CultMath.math;

// Delaunay.Edges is exact: it agrees with an empty-circumcircle oracle evaluated in BigInteger, and degenerate input
// either links correctly or throws.
public sealed class DelaunayTests
{
    // Every edge of a triangle whose circumcircle holds no other point strictly inside, evaluated exactly. When no
    // empty circle has a point on it the triangulation is unique and these are its edges; otherwise they are a superset.
    internal static (SortedSet<(int a, int b)> Edges, bool Unique) Oracle(IReadOnlyList<float2> points)
    {
        var n = points.Count;
        var (px, py) = ExactCoordinates(points);

        var edges = new SortedSet<(int a, int b)>();
        var unique = true;
        for (var i = 0; i < n; i++)
        for (var j = i + 1; j < n; j++)
        for (var k = j + 1; k < n; k++)
        {
            var orient = (px[i] - px[k]) * (py[j] - py[k]) - (py[i] - py[k]) * (px[j] - px[k]);
            if (orient.IsZero) continue;
            var inside = false;
            var onCircle = false;
            for (var m = 0; m < n && !inside; m++)
            {
                if (m == i || m == j || m == k) continue;
                BigInteger adx = px[i] - px[m], ady = py[i] - py[m], bdx = px[j] - px[m], bdy = py[j] - py[m], cdx = px[k] - px[m], cdy = py[k] - py[m];
                var det = (adx * adx + ady * ady) * (bdx * cdy - cdx * bdy) + (bdx * bdx + bdy * bdy) * (cdx * ady - adx * cdy) +
                          (cdx * cdx + cdy * cdy) * (adx * bdy - bdx * ady);
                var sign = det.Sign * orient.Sign;
                inside = sign > 0;
                onCircle |= sign == 0;
            }
            if (inside) continue;
            unique &= !onCircle;
            edges.Add((i, j));
            edges.Add((j, k));
            edges.Add((i, k));
        }
        return (edges, unique);
    }

    // Every coordinate as an integer in one shared unit, so differences and products are exact.
    private static (BigInteger[] X, BigInteger[] Y) ExactCoordinates(IReadOnlyList<float2> points)
    {
        var n = points.Count;
        var raw = new (BigInteger m, int e)[n, 2];
        var lowest = int.MaxValue;
        for (var i = 0; i < n; i++)
            for (var axis = 0; axis < 2; axis++)
            {
                var bits = BitConverter.SingleToInt32Bits(axis == 0 ? points[i].x : points[i].y);
                var biased = (bits >> 23) & 0xFF;
                var m = biased == 0 ? bits & 0x7FFFFF : (bits & 0x7FFFFF) | 0x800000;
                raw[i, axis] = (bits < 0 ? -m : m, biased == 0 ? -149 : biased - 150);
                if (m != 0) lowest = Math.Min(lowest, raw[i, axis].e);
            }
        var px = new BigInteger[n];
        var py = new BigInteger[n];
        for (var i = 0; i < n; i++)
        {
            px[i] = raw[i, 0].m << Math.Max(0, raw[i, 0].e - lowest);
            py[i] = raw[i, 1].m << Math.Max(0, raw[i, 1].e - lowest);
        }
        return (px, py);
    }

    private static int Turn(BigInteger[] x, BigInteger[] y, int a, int b, int c) =>
        ((x[b] - x[a]) * (y[c] - y[a]) - (y[b] - y[a]) * (x[c] - x[a])).Sign;

    // The edges are a triangulation of the points' convex hull: no two cross, and their count is 3n - 3 - b, b being the
    // points on the hull's boundary (a planar graph with that many edges has no face but triangles and the outer one).
    // Cocircular input has several Delaunay triangulations; this pins that the one returned is a whole triangulation.
    internal static void AssertTriangulatesTheHull(IReadOnlyList<float2> points, IReadOnlyList<(int a, int b)> edges, string context)
    {
        var n = points.Count;
        var (x, y) = ExactCoordinates(points);
        var order = Enumerable.Range(0, n).OrderBy(i => x[i]).ThenBy(i => y[i]).ToArray();
        var hull = new List<int>();
        foreach (var pass in new[] { order, order.Reverse().ToArray() })
        {
            var start = hull.Count;
            foreach (var i in pass)
            {
                while (hull.Count >= start + 2 && Turn(x, y, hull[^2], hull[^1], i) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(i);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        var boundary = 0;
        for (var p = 0; p < n; p++)
            for (var h = 0; h < hull.Count; h++)
            {
                int u = hull[h], v = hull[(h + 1) % hull.Count];
                if (Turn(x, y, u, v, p) != 0) continue;
                if (BigInteger.Min(x[u], x[v]) <= x[p] && x[p] <= BigInteger.Max(x[u], x[v]) && BigInteger.Min(y[u], y[v]) <= y[p] && y[p] <= BigInteger.Max(y[u], y[v]))
                {
                    boundary++;
                    break;
                }
            }
        if (hull.Count == 2) boundary = n; // collinear input: every point is on the boundary
        for (var i = 0; i < edges.Count; i++)
            for (var j = i + 1; j < edges.Count; j++)
            {
                var (a, b) = edges[i];
                var (c, d) = edges[j];
                if (a == c || a == d || b == c || b == d) continue;
                Assert.False(Turn(x, y, a, b, c) * Turn(x, y, a, b, d) < 0 && Turn(x, y, c, d, a) * Turn(x, y, c, d, b) < 0,
                    $"{context}: edges ({a},{b}) and ({c},{d}) cross");
            }
        Assert.True(edges.Count == 3 * n - 3 - boundary, $"{context}: {edges.Count} edges, a triangulation of the hull has {3 * n - 3 - boundary}");
    }

    // The triangulation lies within the oracle's edges, and equals them when the oracle's answer is unique.
    internal static void AssertDelaunay(IReadOnlyList<float2> points, string context)
    {
        var edges = Delaunay.Edges(points);
        var (oracle, unique) = Oracle(points);
        Assert.True(edges.All(oracle.Contains), $"{context}: edge outside the Delaunay set: {string.Join(" ", edges.Where(e => !oracle.Contains(e)))}");
        if (unique)
            Assert.True(oracle.SetEquals(edges), $"{context}: missed Delaunay edges: {string.Join(" ", oracle.Where(e => !edges.Contains(e)))}");
        if (!unique) AssertTriangulatesTheHull(points, edges, context);
        Assert.Equal(points.Count, edges.SelectMany(e => new[] { e.a, e.b }).Distinct().Count());
    }

    // Distinct points on a 1/1024 lattice inside [0, 1024), then lifted by offset.
    private static float2[] RandomPoints(int seed, int count, float offset = 0)
    {
        var random = new System.Random(seed);
        var seen = new HashSet<(int, int)>();
        var points = new List<float2>();
        while (points.Count < count)
        {
            (int, int) cell = (random.Next(1 << 20), random.Next(1 << 20));
            if (seen.Add(cell)) points.Add(float2(cell.Item1 / 1024f + offset, cell.Item2 / 1024f + offset));
        }
        return points.ToArray();
    }

    // Kills: the plain double-precision in-circle (a seed in 0..400 that missed a true edge), the filter bound too small.
    [Fact]
    public void TwoHundredRandomSetsOf64PointsAreExactlyDelaunay()
    {
        Parallel.For(0, 200, seed => AssertDelaunay(RandomPoints(seed, 64), $"seed {seed}"));
    }

    // The seeds the double-precision predicate got wrong (found sweeping 0..400 on the old code: 42 and 184), by name.
    // Kills: the plain double-precision in-circle.
    [Theory]
    [InlineData(42)]
    [InlineData(184)]
    public void SeedsTheDoublePredicateMissedStayExact(int seed) => AssertDelaunay(RandomPoints(seed, 64), $"seed {seed}");

    // Kills: the filter bound too small (it answers wrongly where the double determinant is within rounding of zero).
    [Fact]
    public void NearlyCocircularPointsChooseTheTrueDiagonal()
    {
        // Eight lattice points exactly on a circle, then one nudged by an ulp inward and outward.
        var circle = new[] { (5f, 0f), (-5f, 0f), (0f, 5f), (0f, -5f), (3f, 4f), (-3f, 4f), (3f, -4f), (-3f, -4f), (4f, 3f), (-4f, 3f), (4f, -3f), (-4f, -3f) };
        foreach (var nudge in new[] { 0, 1, -1 })
            foreach (var scale in new[] { 1f, 1e4f, 1e-3f })
                for (var which = 0; which < circle.Length; which++)
                {
                    var points = circle.Select(p => float2(p.Item1 * scale, p.Item2 * scale)).ToArray();
                    if (nudge != 0 && points[which].x != 0)
                        points[which] = float2(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(points[which].x) + nudge), points[which].y);
                    AssertDelaunay(points, $"nudge {nudge} scale {scale} point {which}");
                }
    }

    // Kills: no offset-safe predicate (squares of 1e6-sized coordinates drown the differences in double precision).
    [Fact]
    public void LargeCoordinateOffsetsStayExact()
    {
        Parallel.For(0, 40, seed =>
        {
            AssertDelaunay(RandomPoints(seed, 64, 1e6f), $"offset 1e6 seed {seed}");
            AssertDelaunay(RandomPoints(seed, 32, -3e7f), $"offset -3e7 seed {seed}");
        });
    }

    // A lattice is cocircular everywhere: every edge is a true one and every point is linked.
    [Fact]
    public void ALatticeLinksEveryPointAlongTrueEdges()
    {
        var grid = new List<float2>();
        for (var i = 0; i < 6; i++)
            for (var j = 0; j < 6; j++)
                grid.Add(float2(i * 100 + 1e6f, j * 100 + 1e6f));
        AssertDelaunay(grid, "lattice");
        var linked = Delaunay.Edges(grid).SelectMany(e => new[] { e.a, e.b }).ToHashSet();
        Assert.Equal(grid.Count, linked.Count);
    }

    // Kills: the guard on fewer than two points, the collinear branch dropped (every triangle then holds a super-vertex
    // and nothing links).
    [Fact]
    public void DegenerateInputGivesTheSpecifiedOutcome()
    {
        Assert.Empty(Delaunay.Edges(new float2[0]));
        Assert.Empty(Delaunay.Edges(new[] { float2(3, 4) }));
        Assert.Equal(new[] { (0, 1) }, Delaunay.Edges(new[] { float2(3, 4), float2(-1, 2) }));

        // Collinear: each point links to its neighbour along the line, whatever the index order and slope.
        var line = new[] { float2(30, 15), float2(0, 0), float2(20, 10), float2(10, 5), float2(-10, -5) };
        Assert.Equal(new[] { (0, 2), (1, 3), (1, 4), (2, 3) }, Delaunay.Edges(line));
        var vertical = new[] { float2(7, 2), float2(7, -9), float2(7, 40) };
        Assert.Equal(new[] { (0, 1), (0, 2) }, Delaunay.Edges(vertical));
        // Exactly collinear off the axes, where a double cross product need not be zero.
        var slanted = Enumerable.Range(0, 20).Select(i => float2(1e6f + i, 3e6f + 3 * i)).ToArray();
        Assert.Equal(19, Delaunay.Edges(slanted).Count);
    }

    // Kills: the orientation exact path replaced by the plain double sign. Each triple is far from collinear in
    // exact arithmetic, yet its double cross product is 0 (the coordinate differences span more than 53 bits and round),
    // so the plain sign calls the three points collinear and links only two of the three edges.
    [Theory]
    [InlineData(66144f, -196608f, 480f, 384f, -70368744177664f, 211106232532992f)]
    [InlineData(1.9075232557952404e-05f, 6.62929865148937e+18f, 1.9073442672379315e-05f, 6.62929865148937e+18f, 844424930131968f, 6.629861601442791e+18f)]
    [InlineData(-0.00018310546875f, 1759218604441600f, -1610612736f, 1759220215054336f, -2.8823037615171174e+18f, 2.884062980121559e+18f)]
    [InlineData(-1536f, -0.0361328125f, 162304f, 81919.9609375f, -1.8014398509481984e+17f, -9.007199254740992e+16f)]
    public void WideExponentTrianglesAreNotCalledCollinear(float ax, float ay, float bx, float by, float cx, float cy)
    {
        var points = new[] { float2(ax, ay), float2(bx, by), float2(cx, cy) };
        Assert.Equal(new[] { (0, 1), (0, 2), (1, 2) }, Delaunay.Edges(points));
    }

    // Kills: the collinear detector skipping the last sorted point. One point off the line sorts last, so a detector
    // that never tests it reads the set as a line and drops the hull edges to it.
    [Fact]
    public void ALoneOffLinePointThatSortsLastStillTriangulates()
    {
        var points = new[] { float2(4, 1), float2(0, 0), float2(2, 0), float2(1, 0), float2(3, 0) };
        // The apex fans to every point on the base: the only triangulation of this hull.
        Assert.Equal(new[] { (0, 1), (0, 2), (0, 3), (0, 4), (1, 3), (2, 4), (3, 2) }.Select(e => (Math.Min(e.Item1, e.Item2), Math.Max(e.Item1, e.Item2))).OrderBy(e => e).ToArray(),
            Delaunay.Edges(points));
        AssertDelaunay(points, "off-line point last");
    }

    // Kills: the super-triangle too small to keep its vertices out of a thin triangle's circumcircle. A triangle of
    // height h on a base of 1 has a circumradius near 1/(8h); the super vertices must stay outside that circle, or
    // they cut the triangle's hull edges.
    [Theory]
    [InlineData(-10)]
    [InlineData(-30)]
    [InlineData(-60)]
    [InlineData(-100)]
    [InlineData(-140)]
    public void ThinTrianglesKeepTheirHullEdges(int exponent)
    {
        var h = (float) Math.Pow(2, exponent);
        AssertDelaunay(new[] { float2(0, 0), float2(1, 0), float2(0.5f, h) }, $"3 points, h 2^{exponent}");
        AssertDelaunay(new[] { float2(0, 0), float2(1, 0), float2(0.25f, h), float2(0.5f, h), float2(0.75f, h) }, $"5 points, h 2^{exponent}");
        Assert.Equal(new[] { (0, 1), (0, 2), (1, 2) }, Delaunay.Edges(new[] { float2(0, 0), float2(1, 0), float2(0.5f, h) }));
    }

    // Kills: the duplicate check.
    [Fact]
    public void DuplicatePointsAreRefusedByIndex()
    {
        var exception = Assert.Throws<ArgumentException>(() => Delaunay.Edges(new[] { float2(0, 0), float2(5, 5), float2(9, 1), float2(5, 5) }));
        Assert.Contains("points 1 and 3", exception.Message);
        Assert.Throws<ArgumentException>(() => Delaunay.Edges(new[] { float2(1, 1), float2(1, 1) }));
        Assert.Throws<ArgumentException>(() => Delaunay.Edges(new[] { float2(0, 0), float2(float.NaN, 1), float2(2, 2) }));
    }

    // Kills: the Galaxy guard removed.
    [Fact]
    public void GalaxyRefusesAZoneNoLinkReaches()
    {
        var zones = new[] { new GalaxyZone { Position = float2(0, 0) }, new GalaxyZone { Position = float2(1, 0) }, new GalaxyZone { Position = float2(2, 5) } };
        var exception = Assert.Throws<InvalidOperationException>(() => Galaxy.RequireEveryZoneLinked(zones, new[] { (zones[0], zones[1]) }));
        Assert.Contains("1 of 3 zones", exception.Message);
        Galaxy.RequireEveryZoneLinked(zones, new[] { (zones[0], zones[1]), (zones[1], zones[2]) });
        Galaxy.RequireEveryZoneLinked(new[] { zones[0] }, new (GalaxyZone, GalaxyZone)[0]);
    }
}
