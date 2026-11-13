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

    // The triangulation lies within the oracle's edges, and equals them when the oracle's answer is unique.
    internal static void AssertDelaunay(IReadOnlyList<float2> points, string context)
    {
        var edges = Delaunay.Edges(points);
        var (oracle, unique) = Oracle(points);
        Assert.True(edges.All(oracle.Contains), $"{context}: edge outside the Delaunay set: {string.Join(" ", edges.Where(e => !oracle.Contains(e)))}");
        if (unique)
            Assert.True(oracle.SetEquals(edges), $"{context}: missed Delaunay edges: {string.Join(" ", oracle.Where(e => !edges.Contains(e)))}");
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
