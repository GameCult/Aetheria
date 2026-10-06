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
    // One key press at 30k or 300k rocks of one density examines under this many rocks (measured at most 635 across the presses below, at both sizes).
    private const long KeyPressExaminedCeiling = 700;

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
            // From nothing, nearest first stops at the first region no nearer rock can lie in: a fraction of the rocks in reach.
            if (!held && !reticle)
            {
                var inReach = AllChunks(s).Count(c => length(At(s, c) - at) <= 150f);
                Assert.True(cost * 4 < inReach, $"Next from nothing examined {cost} of {inReach} rocks in reach");
            }
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

    // The pruning of every press, pinned by what a press examines. Each ceiling sits within about 10% of the count
    // measured at this commit (the same fixture, fixed seed, one density of 100 square units per rock); a press that
    // loses its angular bound (the reticle), its held-target prune (Next and Previous from a rock at the median
    // distance) or its angle wrap examines 15% to 55% more than that, and fails. The belt's size and the reach vary:
    // 30k rocks at reach 150, 300k at reach 150, and 300k at reach 400 with some 5000 rocks in reach.
    [Fact]
    public void PressPruningKeepsEachPressNearItsMeasuredCost()
    {
        var kind = Kind("Asteroid", 4f);
        var failures = new List<string>();
        foreach (var (count, ring, reaches) in new[] { (30_000, 2100f, new[] { 150f }), (300_000, 2800f, new[] { 150f, 400f }) })
        {
            var outer = sqrt(2000f * 2000f + count * 100f / PI);
            var s = BuildSceneAt(float2.zero, 20000f, 0, null, new[] { kind }, Belt(kind, Annulus(new System.Random(21), count, 2000f, outer)));
            var at = ring * float2(cos(1f), sin(1f));
            foreach (var reach in reaches)
            {
                var observer = Observer(s, at, reach, 3f, Falloff());
                var order = ByDistance(s, observer, reach);
                Assert.True(order.Count > 500, $"degenerate: {order.Count} candidates at reach {reach}");
                var median = order[order.Count / 2];
                long Cost(Func<bool> press, TargetRef held)
                {
                    observer.SetTarget(held);
                    var before = s.Zone.Targets.Examined;
                    Assert.True(press());
                    return s.Zone.Targets.Examined - before;
                }
                void Check(string press, long examined, long ceiling)
                {
                    if (examined > ceiling) failures.Add($"{count} rocks, reach {reach}, {press}: examined {examined}, ceiling {ceiling}");
                }
                var (nextNone, nextHeld, previousNone, previousHeld, reticle) = (count, reach) switch
                {
                    (30_000, _) => (100L, 575L, 290L, 590L, 620L),
                    (_, 150f) => (40L, 560L, 460L, 560L, 680L),
                    _ => (120L, 4700L, 4600L, 4700L, 5900L)
                };
                Check("Next from nothing", Cost(observer.TargetNext, TargetRef.None), nextNone);
                Check("Next from the median rock", Cost(observer.TargetNext, median), nextHeld);
                Check("Previous from nothing", Cost(observer.TargetPrevious, TargetRef.None), previousNone);
                Check("Previous from the median rock", Cost(observer.TargetPrevious, median), previousHeld);
                foreach (var heading in new[] { 0f, 2f, 4f })
                {
                    observer.LookDirection = float3(cos(heading), 0, sin(heading));
                    Check($"the reticle at {heading}", Cost(observer.TargetUnderReticle, TargetRef.None), reticle);
                }
                s.Zone.Entities.Remove(observer);
            }
        }
        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    // Rocks at one distance are ordered by field key, then index, across belts: four rocks on one spot, two in each
    // of two belts, are walked in exactly that order by Next and, reversed, by Previous.
    [Fact]
    public void RocksAtOneDistanceAreOrderedByFieldThenIndex()
    {
        var kind = Kind("Asteroid", 40f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f), Rock(150f)), Belt(kind, Rock(150f), Rock(150f)));
        var eyeAt = float2(150, -100);
        var observer = SpawnShip(s, eyeAt, sensor: true, weaponRanges: new[] { 300f });
        Tick((observer, eyeAt));
        var expected = s.Belts.OrderBy(b => b.Value, StringComparer.Ordinal)
            .SelectMany(b => new[] { new TargetRef(new ChunkId(b, 0)), new TargetRef(new ChunkId(b, 1)) }).ToList();
        Assert.All(expected, t => Assert.True(observer.ChunkVisible(t.Chunk.Value)));
        Assert.Equal(expected, ByDistance(s, observer, 300f));
        for (var i = 0; i < 4; i++)
        {
            Assert.True(observer.TargetNext());
            Assert.Equal(expected[i], observer.Target.Value);
        }
        observer.SetTarget(TargetRef.None);
        for (var i = 3; i >= 0; i--)
        {
            Assert.True(observer.TargetPrevious());
            Assert.Equal(expected[i], observer.Target.Value);
        }
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

    // A ship in the zone that the observer has not detected: it is in Zone.Entities and nowhere in VisibleEntities.
    private static Ship Unseen(Scene s, float2 at)
    {
        var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
        s.Zone.Entities.Add(ship);
        Hold(ship, at);
        return ship;
    }

    // The pilot's instruments bound the pick: a ship the observer has not detected is never offered, whichever end of
    // the order or look direction it would win. Unseen ships sit nearest, farthest, and dead ahead.
    [Fact]
    public void ShipsTheObserverHasNotDetectedAreNeverPicked()
    {
        var at = float2(500, 0);
        var (s, observer) = TargetScene(7, at);
        Sight(s, observer, at + float2(60, 0));
        Sight(s, observer, at + float2(0, -900));
        var hidden = new TargetRef[] { Unseen(s, at + float2(0, 5)), Unseen(s, at + float2(0, 3000)), Unseen(s, at + float2(0, 40)) };
        var order = ByDistance(s, observer, 120f);
        Assert.True(order.Count > 5 && order.Count(t => t.Entity != null) == 2, $"degenerate: {order.Count} candidates");
        Assert.All(hidden, h => Assert.DoesNotContain(h, order));

        observer.SetTarget(TargetRef.None);
        for (var i = 0; i <= order.Count; i++)
        {
            Assert.True(observer.TargetNext());
            Assert.Equal(order[i % order.Count], observer.Target.Value);
        }
        observer.SetTarget(TargetRef.None);
        for (var i = 0; i <= order.Count; i++)
        {
            Assert.True(observer.TargetPrevious());
            Assert.Equal(order[order.Count - 1 - i % order.Count], observer.Target.Value);
        }
        // Looking straight at a hidden ship (heading 0 is dead ahead of the nearest) and in every other direction: a
        // visible target, never a hidden one.
        for (var heading = 0f; heading < 2 * PI; heading += .7f)
        {
            observer.LookDirection = float3(sin(heading), 0, cos(heading));
            observer.SetTarget(TargetRef.None);
            Assert.True(observer.TargetUnderReticle());
            Assert.DoesNotContain(observer.Target.Value, hidden);
            Assert.Contains(observer.Target.Value, order);
        }
    }

    // Candidates at one distance are ordered entity before chunk, then entities by zone order and chunks by field key
    // then index; candidates at one bearing tie the same way for the reticle. A ship stands on the rocks' spot, so
    // all five tie exactly in distance and in angle.
    [Fact]
    public void AShipOnTheRocksSpotOrdersBeforeThem()
    {
        var kind = Kind("Asteroid", 40f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f), Rock(150f)), Belt(kind, Rock(150f), Rock(150f)));
        var eyeAt = float2(150, -100);
        var observer = SpawnShip(s, eyeAt, sensor: true, weaponRanges: new[] { 300f });
        Tick((observer, eyeAt));
        var chunks = s.Belts.OrderBy(b => b.Value, StringComparer.Ordinal)
            .SelectMany(b => new[] { new TargetRef(new ChunkId(b, 0)), new TargetRef(new ChunkId(b, 1)) }).ToList();
        var spot = At(s, chunks[0].Chunk.Value);
        var ship = Sight(s, observer, spot);
        var expected = new List<TargetRef> { new TargetRef(ship) };
        expected.AddRange(chunks);
        Assert.All(chunks, t => Assert.Equal(spot, At(s, t.Chunk.Value)));
        Assert.All(chunks, t => Assert.True(observer.ChunkVisible(t.Chunk.Value)));
        Assert.Equal(expected, ByDistance(s, observer, 300f));

        observer.SetTarget(TargetRef.None);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(observer.TargetNext());
            Assert.Equal(expected[i], observer.Target.Value);
        }
        observer.SetTarget(TargetRef.None);
        for (var i = 4; i >= 0; i--)
        {
            Assert.True(observer.TargetPrevious());
            Assert.Equal(expected[i], observer.Target.Value);
        }
        for (var i = 0; i < 5; i++)
        {
            observer.SetTarget(expected[i]);
            Assert.True(observer.TargetNext());
            Assert.Equal(expected[(i + 1) % 5], observer.Target.Value);
            observer.SetTarget(expected[i]);
            Assert.True(observer.TargetPrevious());
            Assert.Equal(expected[(i + 4) % 5], observer.Target.Value);
        }

        // The reticle, looking at the spot: the ship; then with the ship gone from sight, the first chunk.
        observer.LookDirection = float3(0, 0, 1);
        observer.SetTarget(TargetRef.None);
        Assert.True(observer.TargetUnderReticle());
        Assert.Equal(expected[0], observer.Target.Value);
        observer.VisibleEntities.Remove(ship);
        observer.SetTarget(TargetRef.None);
        Assert.True(observer.TargetUnderReticle());
        Assert.Equal(expected[1], observer.Target.Value);
    }

    // A press with nothing to offer returns false and leaves the target as it was: a ship with no sensor sees no rock
    // and has no mining reach, and sees no ship, whether it holds a target or not.
    [Fact]
    public void APressWithNoCandidateReturnsFalseAndKeepsTheTarget()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f), Rock(160f, .3f)));
        var eyeAt = float2(150, -100);
        var blind = SpawnShip(s, eyeAt);
        var held = Unseen(s, float2(100, 0));
        foreach (var target in new[] { TargetRef.None, new TargetRef(held) })
        {
            blind.SetTarget(target);
            Assert.False(blind.TargetUnderReticle());
            Assert.False(blind.TargetNext());
            Assert.False(blind.TargetPrevious());
            Assert.False(blind.TargetNearestEnemy());
            Assert.Equal(target, blind.Target.Value);
        }
    }

    // Enemies at one distance: the first seen wins, whichever order they were seen in; the observer is never its own
    // enemy, though it sits at distance zero.
    [Fact]
    public void EnemiesAtOneDistanceTieToTheFirstSeenAndNeverTheObserver()
    {
        var at = float2(500, 0);
        var (s, observer) = TargetScene(7, at);
        var a = Sight(s, observer, at + float2(0, 40), enemy: true);
        var b = Sight(s, observer, at + float2(40, 0), enemy: true);
        Assert.Equal(length(a.Position - observer.Position), length(b.Position - observer.Position));
        Assert.True(observer.TargetNearestEnemy());
        Assert.Equal(new TargetRef(a), observer.Target.Value);

        observer.VisibleEnemies.Clear();
        observer.VisibleEnemies.Add(b);
        observer.VisibleEnemies.Add(a);
        Assert.True(observer.TargetNearestEnemy());
        Assert.Equal(new TargetRef(b), observer.Target.Value);

        observer.VisibleEnemies.Clear();
        observer.VisibleEnemies.Add(observer);
        observer.VisibleEnemies.Add(a);
        Assert.True(observer.TargetNearestEnemy());
        Assert.Equal(new TargetRef(a), observer.Target.Value);
        observer.VisibleEnemies.Remove(a);
        observer.SetTarget(TargetRef.None);
        Assert.False(observer.TargetNearestEnemy());
        Assert.True(observer.Target.Value.IsNone);
    }

    // Ships between the mining reach and 250 keep their distance order for Previous: its key is the distance itself
    // at every range, not a function that agrees with it only near and far.
    [Fact]
    public void ShipsBetweenReachAndTwoFiftyAreOrderedByDistanceBothWays()
    {
        var at = float2(500, 0);
        var (s, observer) = TargetScene(7, at);
        foreach (var offset in new[] { float2(135, 0), float2(0, 160), float2(-185, 0), float2(0, -215) }) Sight(s, observer, at + offset);
        var order = ByDistance(s, observer, 120f);
        Assert.True(order.Count(t => t.Chunk.HasValue) >= 8 && order.Count(t => t.Entity != null) == 4, $"degenerate: {order.Count} candidates");
        Assert.All(order.Take(order.Count - 4), t => Assert.True(t.Chunk.HasValue));
        observer.SetTarget(TargetRef.None);
        for (var i = 0; i <= order.Count; i++)
        {
            Assert.True(observer.TargetPrevious());
            Assert.Equal(order[order.Count - 1 - i % order.Count], observer.Target.Value);
        }
        observer.SetTarget(TargetRef.None);
        for (var i = 0; i <= order.Count; i++)
        {
            Assert.True(observer.TargetNext());
            Assert.Equal(order[i % order.Count], observer.Target.Value);
        }
    }
}
