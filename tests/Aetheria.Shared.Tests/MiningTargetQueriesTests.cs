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

// Mining target-queries cut (docs/aetheria-release-map.md; operator 2026-10-03: "Fix the belt freeze", "Fix the
// off-by-one"; reticle-exact: "Keep reticles exact"): the reticle, next, previous and nearest presses are Entity's
// best-first questions to the zone's targeting index. Every answer is compared with a test-local sort or argmin of
// every visible candidate.
public sealed partial class MiningCut3Tests
{
    // One key press at 30k or 300k rocks of one density examines under this many rocks (measured at most 620 across the presses below, at both sizes).
    private const long KeyPressExaminedCeiling = 1500;

    // A belt and the observer: reach `reach`, with the dimmest sensor (found by doubling) that sees at least `share`
    // of the rocks in its reach, so some are lit and some dark whatever the fixture's light.
    private (Scene scene, Ship observer) TargetScene(int seed, float2 at, float reach = 120f, float share = .4f, int rocks = 3000)
    {
        var kind = Kind("Asteroid", 4f);
        var rng = new System.Random(seed);
        var s = BuildSceneAt(float2.zero, 1500f, 0, null, new[] { kind }, Belt(kind, Annulus(rng, rocks, 300f, 700f)));
        var inReach = AllChunks(s).Where(c => length(At(s, c) - at) <= reach).ToList();
        Assert.True(inReach.Count > 20, $"degenerate: {inReach.Count} rocks in reach");
        for (var sensitivity = .5f; sensitivity < 1e9f; sensitivity *= 2f)
        {
            var observer = Observer(s, at, reach, sensitivity, Falloff());
            if (inReach.Count(observer.ChunkVisible) >= share * inReach.Count) return (s, observer);
            s.Zone.Entities.Remove(observer);
        }
        throw new InvalidOperationException("no sensitivity sees the rocks");
    }

    // Visible ships at every range, in the observer's VisibleEntities.
    private static Ship Sight(Scene s, Ship observer, float2 at, bool enemy = false)
    {
        var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
        s.Zone.Entities.Add(ship);
        Hold(ship, at);
        observer.VisibleEntities.Add(ship);
        if (enemy) observer.VisibleEnemies.Add(ship);
        return ship;
    }

    // Every target a press may pick, as a plain scan: other visible ships, and visible chunks within reach, by
    // planar distance then entity before chunk, entities by zone order, chunks by field key then index.
    private static List<TargetRef> ByDistance(Scene s, Ship observer, float reach)
    {
        var at = observer.Position.xz;
        var all = new List<(TargetRef target, float distance)>();
        foreach (var e in observer.VisibleEntities)
            if (e != observer) all.Add((e, length(e.Position.xz - at)));
        foreach (var c in AllChunks(s))
            if (s.Zone.ChunkExists(c) && length(At(s, c) - at) <= reach && observer.ChunkVisible(c))
                all.Add((c, length(At(s, c) - at)));
        return all.OrderBy(x => x.distance)
            .ThenBy(x => x.target.Chunk.HasValue ? 1 : 0)
            .ThenBy(x => x.target.Chunk.HasValue ? 0 : s.Zone.Entities.IndexOf(x.target.Entity))
            .ThenBy(x => x.target.Chunk.HasValue ? x.target.Chunk.Value.Field.Value : "", StringComparer.Ordinal)
            .ThenBy(x => x.target.Chunk.HasValue ? x.target.Chunk.Value.Index : 0)
            .Select(x => x.target).ToList();
    }

    private static void SightShips(Scene s, Ship observer, float2 at)
    {
        // Three ships at one distance (a tie), one past mining reach, one far beyond it.
        Sight(s, observer, at + float2(40, 0));
        Sight(s, observer, at + float2(0, 40));
        Sight(s, observer, at + float2(-40, 0));
        Sight(s, observer, at + float2(250, 0));
        Sight(s, observer, at + float2(0, -900));
    }

    // Repeated Next from nothing visits every target in the scan's order and wraps; Previous is the reverse. Ties
    // between ships at one distance, ships past reach, and rocks both near and at reach are all in the order.
    [Fact]
    public void NextAndPreviousWalkTheBruteForceOrder()
    {
        var walked = 0;
        foreach (var (seed, at) in new[] { (7, float2(500, 0)), (8, float2(-350, 400)), (9, float2(0, -650)) })
        {
            var (s, observer) = TargetScene(seed, at);
            SightShips(s, observer, at);
            var order = ByDistance(s, observer, 120f);
            Assert.True(order.Count(t => t.Chunk.HasValue) >= 8 && order.Count(t => t.Entity != null) == 5, $"degenerate: {order.Count} candidates");

            observer.SetTarget(TargetRef.None);
            for (var i = 0; i <= order.Count; i++)
            {
                Assert.True(observer.TargetNext());
                Assert.Equal(order[i % order.Count], observer.Target.Value);
                walked++;
            }
            observer.SetTarget(TargetRef.None);
            for (var i = 0; i <= order.Count; i++)
            {
                Assert.True(observer.TargetPrevious());
                Assert.Equal(order[(order.Count - 1 - i % order.Count)], observer.Target.Value);
                walked++;
            }
            // From every target, Next is its successor and Previous its predecessor.
            for (var i = 0; i < order.Count; i++)
            {
                observer.SetTarget(order[i]);
                observer.TargetNext();
                Assert.Equal(order[(i + 1) % order.Count], observer.Target.Value);
                observer.SetTarget(order[i]);
                observer.TargetPrevious();
                Assert.Equal(order[(i + order.Count - 1) % order.Count], observer.Target.Value);
            }
        }
        Assert.True(walked > 60, $"degenerate: {walked} steps");
    }

    // Operator, "Fix the off-by-one": with nothing targeted, Previous takes the farthest candidate, not the one
    // before it.
    [Fact]
    public void PreviousWithNoTargetPicksTheFarthest()
    {
        var at = float2(500, 0);
        var (s, observer) = TargetScene(7, at);
        Sight(s, observer, at + float2(60, 0));
        var order = ByDistance(s, observer, 120f);
        Assert.True(order.Count > 3);
        Assert.True(order[^1].Chunk.HasValue, "the farthest should be a rock here");
        Assert.True(observer.TargetPrevious());
        Assert.Equal(order[^1], observer.Target.Value);
        Assert.NotEqual(order[^2], observer.Target.Value);

        // With a far ship as well, the farthest is the ship.
        var far = Sight(s, observer, at + float2(0, -900));
        observer.SetTarget(TargetRef.None);
        Assert.True(observer.TargetPrevious());
        Assert.Equal(new TargetRef(far), observer.Target.Value);
    }

    // The reticle's pick is the scan's least planar angle, over ships and rocks alike, in many look directions; the
    // same press again clears the target.
    [Fact]
    public void TheReticlePicksTheSmallestAngle()
    {
        var at = float2(-350, 400);
        var (s, observer) = TargetScene(8, at);
        SightShips(s, observer, at);
        var order = ByDistance(s, observer, 120f);
        Assert.True(order.Count > 15);
        float2 Where(TargetRef t) => t.Chunk is ChunkId c ? At(s, c) : t.Entity.Position.xz;
        var rng = new System.Random(3);
        var picked = new HashSet<TargetRef>();
        for (var i = 0; i < 60; i++)
        {
            var heading = rng.NextDouble() * 2 * Math.PI;
            observer.LookDirection = float3((float) Math.Cos(heading), 0, (float) Math.Sin(heading));
            double Angle(TargetRef t)
            {
                var to = Where(t) - at;
                return Math.Abs(Math.Atan2((double) Math.Cos(heading) * to.y - Math.Sin(heading) * to.x,
                    Math.Cos(heading) * to.x + Math.Sin(heading) * to.y));
            }
            var expected = order.OrderBy(Angle).First();
            observer.SetTarget(TargetRef.None);
            Assert.True(observer.TargetUnderReticle());
            Assert.Equal(expected, observer.Target.Value);
            picked.Add(expected);
            Assert.True(observer.TargetUnderReticle());
            Assert.True(observer.Target.Value.IsNone, "the second press on the same target clears it");
        }
        Assert.True(picked.Count > 10, $"degenerate: {picked.Count} distinct picks");
    }

    // The operator's rule for F1: a key press does not grow with the belt. TargetNext, from nothing and from a held
    // rock, at 30k and 300k rocks of one density, examines about as many in both, under a fixed ceiling.
    // Measured at this commit: see ExaminedAtPress.
    [Fact]
    public void AKeyPressDoesNotGrowWithTheBelt()
    {
        var kind = Kind("Asteroid", 4f);
        long Press(int count, bool held, bool reticle)
        {
            var outer = sqrt(2000f * 2000f + count * 100f / PI);
            var s = BuildSceneAt(float2.zero, 20000f, 0, null, new[] { kind }, Belt(kind, Annulus(new System.Random(21), count, 2000f, outer)));
            var at = 2100f * float2(cos(1f), sin(1f));
            var observer = Observer(s, at, 150f, 3f, Falloff());
            observer.LookDirection = float3(1, 0, 0);
            var order = ByDistance(s, observer, 150f);
            Assert.True(order.Count > 5);
            if (held) observer.SetTarget(order[order.Count / 2]);
            var before = s.Zone.Targets.Examined;
            Assert.True(reticle ? observer.TargetUnderReticle() : observer.TargetNext());
            var cost = s.Zone.Targets.Examined - before;
            if (!reticle) Assert.Equal(order[held ? order.Count / 2 + 1 : 0], observer.Target.Value);
            return cost;
        }
        var report = new List<string>();
        var grew = false;
        var over = false;
        foreach (var (held, reticle) in new[] { (false, false), (true, false), (false, true) })
        {
            var small = Press(30_000, held, reticle);
            var large = Press(300_000, held, reticle);
            report.Add($"held {held} reticle {reticle}: {small} at 30k, {large} at 300k");
            grew |= !(large < 2 * small && small < 2 * large);
            over |= large >= KeyPressExaminedCeiling;
        }
        Assert.False(grew || over, $"examined rocks per press, ceiling {KeyPressExaminedCeiling}: {string.Join("; ", report)}");
    }

    // No press ever picks a rock this ship cannot see, or one beyond mining reach; launchers add no reach (Q12 A).
    [Fact]
    public void DarkAndOutOfReachRocksAreNeverPicked()
    {
        var at = float2(500, 0);
        // A dim sensor: only some rocks in reach are visible.
        var (s, observer) = TargetScene(7, at, reach: 150f);
        var inReach = AllChunks(s).Where(c => length(At(s, c) - at) <= 150f).ToList();
        var visible = inReach.Where(observer.ChunkVisible).ToList();
        Assert.True(visible.Count > 3 && visible.Count < inReach.Count, $"degenerate: {visible.Count} of {inReach.Count} visible");
        var picks = new HashSet<ChunkId>();
        var rng = new System.Random(5);
        for (var i = 0; i < 200; i++)
        {
            var heading = rng.NextDouble() * 2 * Math.PI;
            observer.LookDirection = float3((float) Math.Cos(heading), 0, (float) Math.Sin(heading));
            observer.SetTarget(TargetRef.None);
            switch (i % 3)
            {
                case 0: observer.TargetUnderReticle(); break;
                case 1: observer.TargetNext(); observer.TargetNext(); break;
                default: observer.TargetPrevious(); observer.TargetPrevious(); break;
            }
            if (observer.Target.Value.Chunk is ChunkId chunk)
            {
                Assert.Contains(chunk, visible);
                picks.Add(chunk);
            }
        }
        Assert.True(picks.Count > 3, $"degenerate: {picks.Count} distinct rocks picked");

        // A launcher's longer range is no reach: a ship fitted with one alone picks no rock.
        var launcher = Gear("Launcher", new LockWeaponData { Range = Constant(400f), LockSpeed = Constant(.5f), LockAngle = Constant(30f), DirectionImpact = Constant(1f) });
        s.Cache.Upsert(launcher);
        var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
        Assert.True(ship.TryEquip(Mint(s, s.Eye)));
        Assert.True(ship.TryEquip(Mint(s, launcher)));
        s.Zone.Entities.Add(ship);
        Hold(ship, at);
        ship.LookDirection = float3(0, 0, 1);
        ship.Activate();
        Tick((ship, at));
        Assert.Contains(ship.Weapons, w => w.Range == 400f);
        Assert.True(AllChunks(s).Any(c => length(At(s, c) - at) <= 400f && ship.ChunkVisible(c)), "there are rocks within the launcher's range");
        // It may see the other ship; it must not pick a rock.
        foreach (var press in new Func<bool>[] { ship.TargetUnderReticle, ship.TargetNext, ship.TargetPrevious })
        {
            ship.SetTarget(TargetRef.None);
            press();
            Assert.False(ship.Target.Value.Chunk.HasValue, "a launcher-only ship picked a rock");
        }
    }

    // Nearest stays enemies only: a nearer rock and a nearer friendly ship are ignored.
    [Fact]
    public void NearestStaysEnemiesOnly()
    {
        var at = float2(500, 0);
        var (s, observer) = TargetScene(7, at);
        var friend = Sight(s, observer, at + float2(10, 0));
        var farEnemy = Sight(s, observer, at + float2(0, 90), enemy: true);
        var nearEnemy = Sight(s, observer, at + float2(60, 0), enemy: true);
        Assert.True(observer.TargetNearestEnemy());
        Assert.Equal(new TargetRef(nearEnemy), observer.Target.Value);

        observer.VisibleEnemies.Clear();
        observer.SetTarget(friend);
        Assert.False(observer.TargetNearestEnemy());
        Assert.Equal(new TargetRef(friend), observer.Target.Value);
        Assert.NotEqual(new TargetRef(farEnemy), observer.Target.Value);
    }
}
