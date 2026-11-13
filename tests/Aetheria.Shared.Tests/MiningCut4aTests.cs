/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Mining cut 4a (docs/mining-cut-refresh.md, Q10 = A): a blast's disc is shared by area over whatever it covers, hull
// cells or rocks, through FireControl.Detonate; each worn rock publishes one absorb on Zone.ChunkCapabilities. Reuses
// MiningCut3Tests' belt fixture (a partial of that class), so a filter on these tests names the methods below.
public sealed partial class MiningCut3Tests
{
    // A one-rock belt: the rock at phase 0 sits on +x of the belt centre, `size` its authored size.
    private (Scene scene, ChunkId rock) OneRock(float size = .5f, float distance = 200f)
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(distance, 0f, size)));
        return (s, new ChunkId(s.Belts[0], 0));
    }

    private static List<AbsorbEvent> Absorbs(Scene s, List<IDisposable> subs)
    {
        var list = new List<AbsorbEvent>();
        subs.Add(s.Zone.ChunkCapabilities.Absorb.Subscribe(list.Add));
        return list;
    }

    // The area of a disc at the origin of radius r1 and a disc at (d, 0) of radius r2, by midpoint integration of the
    // chord overlap -- no formula of the code under test.
    private static double NumericLens(double d, double r1, double r2)
    {
        var lo = Math.Max(-r1, d - r2);
        var hi = Math.Min(r1, d + r2);
        if (hi <= lo) return 0;
        const int n = 400000;
        var h = (hi - lo) / n;
        var area = 0.0;
        for (var i = 0; i < n; i++)
        {
            var x = lo + (i + .5) * h;
            var a = Math.Sqrt(Math.Max(0, r1 * r1 - x * x));
            var b = Math.Sqrt(Math.Max(0, r2 * r2 - (x - d) * (x - d)));
            area += 2 * Math.Min(a, b) * h;
        }
        return area;
    }

    // The area of a disc centred (cx, cy) of radius r inside the axis-aligned rectangle, by the same integration.
    private static double NumericDiscInRect(double cx, double cy, double r, double x0, double x1, double y0, double y1)
    {
        var lo = Math.Max(cx - r, x0);
        var hi = Math.Min(cx + r, x1);
        if (hi <= lo) return 0;
        const int n = 400000;
        var h = (hi - lo) / n;
        var area = 0.0;
        for (var i = 0; i < n; i++)
        {
            var x = lo + (i + .5) * h;
            var half = Math.Sqrt(Math.Max(0, r * r - (x - cx) * (x - cx)));
            area += Math.Max(0, Math.Min(cy + half, y1) - Math.Max(cy - half, y0)) * h;
        }
        return area;
    }

    [Theory]
    [InlineData(5f, 3f, 4f)]
    [InlineData(1f, 3f, 4f)]
    [InlineData(6.5f, 3f, 4f)]
    [InlineData(2.5f, 2f, 5f)]
    [InlineData(3f, 3f, 3f)]
    public void CircleCircleOverlapIsTheLens(float d, float r1, float r2)
    {
        var expected = NumericLens(d, r1, r2);
        Assert.Equal(expected, FireControl.CircleCircleOverlap(d, r1, r2), 3);
        Assert.Equal(expected, FireControl.CircleCircleOverlap(d, r2, r1), 3);
    }

    [Fact]
    public void CircleCircleOverlapIsExactAtTheLimits()
    {
        Assert.Equal(0f, FireControl.CircleCircleOverlap(7f, 3f, 4f));    // tangent
        Assert.Equal(0f, FireControl.CircleCircleOverlap(8f, 3f, 4f));    // disjoint
        Assert.Equal(PI * 9f, FireControl.CircleCircleOverlap(1f, 3f, 4f), 3); // contained: the smaller disc
        Assert.Equal(PI * 9f, FireControl.CircleCircleOverlap(0f, 4f, 3f), 3); // concentric
        Assert.Equal(PI * 9f, FireControl.CircleCircleOverlap(1f, 3f, 4f), 3);
        Assert.Equal(0f, FireControl.CircleCircleOverlap(0f, 0f, 3f));
    }

    [Fact]
    public void ABlastBesideAnUntargetedRockWearsIt()
    {
        var (s, rock) = OneRock();
        var at = At(s, rock);
        var radius = 5f;
        var offset = 3f;
        var undamaged = s.Zone.ChunkRadius(rock);
        FireControl.Detonate(s.Zone, at + float2(offset, 0), radius, 10f, DamageType.Kinetic);

        var share = (float) (10.0 * NumericLens(offset, radius, undamaged) / (Math.PI * radius * radius));
        Assert.True(share > 0f);
        Assert.Equal(s.Zone.AsteroidBelts[rock.Field].DamagedSize(rock.Index, share, s.Zone.Settings), s.Zone.ChunkRadius(rock), 3);
    }

    [Fact]
    public void ABlastSharesItsDiscOverCellsAndRocksByArea()
    {
        var (s, rock) = OneRock();
        var rockAt = At(s, rock);
        var rockRadius = s.Zone.ChunkRadius(rock);
        var ship = SpawnShip(s, rockAt + float2(-12f, 0f));
        var radius = 6f;
        var blast = rockAt + float2(-5.5f, 0f);
        var damage = 1f;
        var subs = new List<IDisposable>();
        var hullAbsorbs = new List<AbsorbEvent>();
        subs.Add(ship.Capabilities.Absorb.Subscribe(hullAbsorbs.Add));
        var rockAbsorbs = Absorbs(s, subs);

        FireControl.Detonate(s.Zone, blast, radius, damage, DamageType.Kinetic);
        foreach (var sub in subs) sub.Dispose();

        // The hull is a 5 x 5 block of 2-unit cells about its position; the rock a disc; the two do not overlap.
        var p = ship.Position.xz;
        var area = Math.PI * radius * radius;
        var hullArea = NumericDiscInRect(blast.x, blast.y, radius, p.x - 5, p.x + 5, p.y - 5, p.y + 5);
        var rockArea = NumericLens(length(rockAt - blast), radius, rockRadius);
        Assert.True(hullArea > 0 && rockArea > 0 && hullArea + rockArea < area, "fixture: both covered, some of the disc lost");
        var hull = Assert.Single(hullAbsorbs);
        var worn = Assert.Single(rockAbsorbs);
        Assert.Equal(damage * hullArea / area, hull.Magnitude, 4);
        Assert.Equal(damage * rockArea / area, worn.Magnitude, 4);
        var lost = damage * (area - hullArea - rockArea) / area;
        Assert.Equal(damage, hull.Magnitude + worn.Magnitude + lost, 4);
    }

    [Fact]
    public void ARockWhoseCentreIsOutsideTheBlastIsStillWorn()
    {
        // The widest rock (size 1, radius 9) and a small blast: the blast radius 3 plus the rock's 9 exceeds the
        // 8 between them, yet the centre is far outside the blast disc.
        var (s, rock) = OneRock(size: 1f);
        var at = At(s, rock);
        var radius = 3f;
        Assert.Equal(9f, s.Zone.ChunkRadius(rock), 3);
        var subs = new List<IDisposable>();
        var absorbs = Absorbs(s, subs);
        FireControl.Detonate(s.Zone, at + float2(8f, 0f), radius, 10f, DamageType.Kinetic);
        subs.ForEach(x => x.Dispose());

        var worn = Assert.Single(absorbs);
        Assert.Equal(10.0 * NumericLens(8, radius, 9) / (Math.PI * radius * radius), worn.Magnitude, 4);
        Assert.True(s.Zone.ChunkRadius(rock) < 9f);
    }

    [Fact]
    public void ATangentOrDistantBlastWearsNothing()
    {
        var (s, rock) = OneRock();
        var at = At(s, rock);
        var radius = s.Zone.ChunkRadius(rock);
        var subs = new List<IDisposable>();
        var absorbs = Absorbs(s, subs);
        FireControl.Detonate(s.Zone, at + float2(2f * radius, 0f), radius, 10f, DamageType.Kinetic);
        FireControl.Detonate(s.Zone, at + float2(60f, 0f), 5f, 10f, DamageType.Kinetic);
        subs.ForEach(x => x.Dispose());

        Assert.Empty(absorbs);
        Assert.Equal(radius, s.Zone.ChunkRadius(rock));
    }

    [Fact]
    public void ABrokenRockTakesNothingAndPublishesNothing()
    {
        var (s, rock) = OneRock();
        var at = At(s, rock);
        Assert.True(s.Zone.Breaks(rock, 1e6f));
        var subs = new List<IDisposable>();
        var absorbs = Absorbs(s, subs);
        FireControl.Detonate(s.Zone, at, 8f, 10f, DamageType.Kinetic);
        subs.ForEach(x => x.Dispose());

        Assert.Empty(absorbs);
        Assert.False(s.Zone.ChunkExists(rock));
        Assert.Equal(0f, s.Zone.ChunkRadius(rock));
    }

    [Fact]
    public void EachWornRockPublishesOneAbsorb()
    {
        var kind = Kind("Asteroid", 4f);
        // Two rocks side by side and a third out of reach, listed out of order of their distance.
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(200f, 0f, .5f), Rock(300f, 0f, .5f), Rock(205f, 0f, .5f)));
        var a = new ChunkId(s.Belts[0], 0);
        var b = new ChunkId(s.Belts[0], 2);
        var far = new ChunkId(s.Belts[0], 1);
        var blast = (At(s, a) + At(s, b)) / 2f;
        var radius = 8f;
        var damage = 10f;
        var seen = new List<(AbsorbEvent e, float aRadius, float bRadius)>();
        using var sub = s.Zone.ChunkCapabilities.Absorb.Subscribe(e => seen.Add((e, s.Zone.ChunkRadius(a), s.Zone.ChunkRadius(b))));
        var undamagedA = s.Zone.ChunkRadius(a);
        var undamagedB = s.Zone.ChunkRadius(b);

        FireControl.Detonate(s.Zone, blast, radius, damage, DamageType.Kinetic);

        Assert.Equal(2, seen.Count);
        Assert.All(seen, x => Assert.Equal(AbsorbLayer.Hull, x.e.Layer));
        Assert.All(seen, x => Assert.Equal(0f, x.e.Position.y));
        // Walked in index order; each absorb lands before its own rock's wear, and the first rock's wear is already
        // done when the second is published.
        Assert.Equal(undamagedA, seen[0].aRadius);
        Assert.Equal(undamagedB, seen[0].bRadius);
        Assert.True(seen[1].aRadius < undamagedA);
        Assert.Equal(undamagedB, seen[1].bRadius);
        Assert.Equal(damage * NumericLens(length(At(s, a) - blast), radius, undamagedA) / (Math.PI * radius * radius), seen[0].e.Magnitude, 4);
        Assert.Equal(damage * NumericLens(length(At(s, b) - blast), radius, undamagedB) / (Math.PI * radius * radius), seen[1].e.Magnitude, 4);
        // The direction runs from the blast out through the rock.
        var away = normalize(At(s, a) - blast);
        Assert.Equal(away.x, seen[0].e.Direction.x, 4);
        Assert.Equal(away.y, seen[0].e.Direction.z, 4);
        Assert.Equal(s.Zone.AsteroidBelts[far.Field].UndamagedSize(far.Index, s.Zone.Settings), s.Zone.ChunkRadius(far));
    }

    // The walk is fixed: (field ordinal, index), whatever order the targeting index yields. Two belts, rocks scattered
    // so the index's own order is not the sorted one; the absorbs come in sorted order.
    [Fact]
    public void RocksAreWornInFieldThenIndexOrder()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind },
            Belt(kind, Rock(200f, .7f, .5f), Rock(200f, .1f, .5f), Rock(200f, .4f, .5f)),
            Belt(kind, Rock(200f, .9f, .5f), Rock(200f, .2f, .5f)));
        var rocks = new[] { 0, 1, 2 }.Select(i => new ChunkId(s.Belts[0], i)).Concat(new[] { 0, 1 }.Select(i => new ChunkId(s.Belts[1], i))).ToList();
        var blast = float2.zero;
        var seen = new List<AbsorbEvent>();
        using var sub = s.Zone.ChunkCapabilities.Absorb.Subscribe(seen.Add);

        FireControl.Detonate(s.Zone, blast, 400f, 1000f, DamageType.Kinetic);

        Assert.Equal(rocks.Count, seen.Count);
        var expected = rocks.OrderBy(r => r.Field.Value, StringComparer.Ordinal).ThenBy(r => r.Index).ToList();
        for (var i = 0; i < expected.Count; i++)
        {
            var away = normalize(At(s, expected[i]) - blast);
            Assert.Equal(away.x, seen[i].Direction.x, 3);
            Assert.Equal(away.y, seen[i].Direction.z, 3);
        }
    }

    [Fact]
    public void AMineBlastWearsRocks()
    {
        var (s, rock) = OneRock();
        var at = At(s, rock);
        var undamaged = s.Zone.ChunkRadius(rock);
        var absorbs = new List<AbsorbEvent>();
        using var sub = s.Zone.ChunkCapabilities.Absorb.Subscribe(absorbs.Add);
        s.Zone.Lay(new Mine
        {
            Id = s.Zone.NextBodyId(), Body = new KinematicBody { Position = at + float2(2f, 0f), Velocity = float2.zero },
            ArmingDelay = 0f, FuseDelay = 100f, Lifetime = 0f, BlastRadius = 6f, Damage = 10f,
            DamageType = DamageType.Kinetic, Layer = null, LayerFaction = null
        });

        s.Zone.Update(.001f);

        Assert.Empty(s.Zone.Mines);
        Assert.Single(absorbs);
        Assert.True(s.Zone.ChunkRadius(rock) < undamaged);
    }
}
