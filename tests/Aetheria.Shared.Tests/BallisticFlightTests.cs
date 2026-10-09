/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using System.Reflection;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float2x2 = CultMath.float2x2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Ballistic flight: a round's flight is presentation (ruling projectile-flight-is-presentation). The simulation publishes
// the facts (the PendingShot's frozen origin, direction, speed, fire time and arrival, the outcome with its commit tick,
// impact cell and burst point) and DrawAhead's statics draw the round from them: Round the one statement of where a round
// is at a sim time, Impact where a hit lands on the target's drawn pose, RoundEnd and RoundOver when its drawn flight
// ends. FireControl owns none of it (ruling direct-arrival-keep-timing: damage keeps landing at the arrival time, the
// hit is drawn at its cell). Unity's Projectile calls Round and integrates nothing, so these tests pin the drawing
// against first principles (the shooter's position, the aim, the authored velocity and range, the hull's own frame)
// rather than against the code that computes it. Shares the Cut 12.4 engagement fixture.
public sealed partial class FireControlCut124Tests
{
    private const float BallisticSpeed = 20f;

    // A no-lock proximity round: flies the aim line (2,1) to max range, bursting there.
    private Engagement FireBurstRound(out float3 origin)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: BallisticSpeed, fuse: WeaponFuse.Proximity, blastRadius: 4f,
            damage: 100f, weaponRange: NoLockRange);
        Aim(e, float2(2, 1));
        e.Shooter.SetTarget(TargetRef.None);
        origin = e.Shooter.Position;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        return e;
    }

    // What the sim publishes for a shot, as a presenter holds it: only the result (no impact, no burst point).
    private static ShotOutcome Known(in PendingShot shot, ShotResult result) =>
        new ShotOutcome { ShotId = shot.ShotId, Result = result, ArrivalIn = max(0f, shot.ArrivalTime - shot.CommitTime) };

    // A hit on a cell of the target, committed at `commit`: ArrivalIn is how long after the commit damage lands.
    private static ShotOutcome Hit(in PendingShot shot, Entity target, int2 cell, float commit) =>
        new ShotOutcome { ShotId = shot.ShotId, Result = ShotResult.Hit, Target = target, Cell = cell, ArrivalIn = shot.ArrivalTime - commit };

    // The hull's occupied coordinate farthest from its centre of mass: a cell well off the target's position.
    private static int2 FarCell(Engagement e) =>
        e.HullData.Shape.Coordinates.OrderByDescending(c => length((float2) c - e.HullData.Shape.CenterOfMass)).First();

    [Fact]
    public void ARoundIsDrawnOnItsFiredLine()
    {
        // Burst round: leaves the shooter, is where its own speed puts it a second in, and ends on the burst point.
        var e = FireBurstRound(out var origin);
        ShotOutcome outcome = null;
        PendingShot burst = default;
        using var c = e.Zone.ShotCommitted.Subscribe(o =>
        {
            outcome = o;
            e.Zone.TryGetShot(o.ShotId, out burst);
        });
        e.Zone.Update(.01f);
        Assert.NotNull(outcome);
        Assert.True(outcome.HasBurstPoint);
        var aim = normalize(float2(2, 1));
        // The shooter moves on after the round left: the line is the one frozen at Fire, not one drawn from where it is now.
        e.Shooter.Position += float3(7, 0, 3);

        var start = DrawAhead.Round(burst, null, burst.FireOrigin, 0f, burst.FireTime, 0f);
        Assert.Equal(origin.x, start.x, 3);
        Assert.Equal(origin.z, start.z, 3);

        var second = DrawAhead.Round(burst, null, burst.FireOrigin, 0f, burst.FireTime + 1f, 0f);
        Assert.Equal(origin.x + aim.x * BallisticSpeed, second.x, 3);
        Assert.Equal(origin.z + aim.y * BallisticSpeed, second.z, 3);

        foreach (var known in new[] { null, outcome })
            foreach (var later in new[] { 0f, 1f, 50f })
            {
                var arrival = DrawAhead.Round(burst, known, burst.FireOrigin, 0f, burst.ArrivalTime + later, 0f);
                Assert.Equal(burst.BurstPosition.x, arrival.x, 3);
                Assert.Equal(burst.BurstPosition.z, arrival.z, 3);
            }

        // Past the weapon's range it is held at the range point, however late it is asked.
        var held = DrawAhead.Round(burst, Known(burst, ShotResult.Miss), burst.FireOrigin, 0f, burst.FireTime + NoLockRange / BallisticSpeed + 50f, 0f);
        Assert.Equal(origin.x + aim.x * NoLockRange, held.x, 3);
        Assert.Equal(origin.z + aim.y * NoLockRange, held.z, 3);
        // Before it was fired it is still at the origin, not behind it.
        var before = DrawAhead.Round(burst, null, burst.FireOrigin, 0f, burst.FireTime - 5f, 0f);
        Assert.Equal(origin.x, before.x, 3);
        Assert.Equal(origin.z, before.z, 3);

        // Direct round at a stationary target 100 ahead (+z): arrives on the target, and a miss would fly on to the weapon's range.
        var d = Build(TestSettings(), SolidShape(5, 4), velocity: BallisticSpeed, weaponRange: 1000f);
        var dOrigin = d.Shooter.Position;
        FireControl.Fire(d.Weapon, d.WeaponItem, d.Shooter);
        var direct = SafeAssert.OnlyShot(d.Zone);
        // A round not known to miss waits at its arrival point however late it is asked.
        foreach (var at in new[] { direct.ArrivalTime, direct.FireTime + 99f })
        {
            var waiting = DrawAhead.Round(direct, null, direct.FireOrigin, 0f, at, 0f);
            Assert.Equal(d.Target.Position.x, waiting.x, 2);
            Assert.Equal(d.Target.Position.z, waiting.z, 2);
        }
        Assert.Equal(dOrigin.z + 100f / 2f, DrawAhead.Round(direct, null, direct.FireOrigin, 0f, direct.FireTime + 2.5f, 0f).z, 2);
        var heldDirect = DrawAhead.Round(direct, Known(direct, ShotResult.Miss), direct.FireOrigin, 0f, direct.FireTime + 1000f / BallisticSpeed + 99f, 0f);
        Assert.Equal(dOrigin.z + 1000f, heldDirect.z, 2);
    }

    // A round with no speed (the weapon authors Velocity 0, or effectively 0) does not fly: it is at its origin at any
    // time, and is not stretched along its line by a vanishing speed over the range it would need forever to cover.
    [Fact]
    public void ARoundWithNoSpeedStaysAtItsOrigin()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: .005f, weaponRange: 1000f);
        var origin = e.Shooter.Position;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);

        var late = DrawAhead.Round(shot, Known(shot, ShotResult.Miss), shot.FireOrigin, 0f, shot.FireTime + 5000f, 0f);
        Assert.Equal(origin.x, late.x, 3);
        Assert.Equal(origin.z, late.z, 3);
    }

    [Fact]
    public void SpeedIsFrozenAtFire()
    {
        var e = FireBurstRound(out var origin);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(BallisticSpeed, shot.Speed, 4);

        // The stat changes after the round left, and the weapon re-reads it.
        ((InstantWeaponData) e.Weapon.WeaponData).Velocity = Constant(BallisticSpeed * 5f);
        e.Weapon.Execute(0f);
        Assert.Equal(BallisticSpeed * 5f, e.Weapon.Velocity, 4);

        var after = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(BallisticSpeed, after.Speed, 4);
        var aim = normalize(float2(2, 1));
        var at = DrawAhead.Round(after, null, after.FireOrigin, 0f, after.FireTime + 1f, 0f);
        Assert.Equal(origin.x + aim.x * BallisticSpeed, at.x, 3);
        Assert.Equal(origin.z + aim.y * BallisticSpeed, at.z, 3);
    }

    [Fact]
    public void ARoundIsPure()
    {
        var e = FireDirectRound(20f, 1000f);
        var shot = SafeAssert.OnlyShot(e.Zone);
        e.Target.Direction = normalize(float2(1, 2));
        e.Target.TurnRate = 3f;
        var time = e.Zone.Time;
        var entities = e.Zone.Entities.ToList();
        var positions = entities.Select(x => x.Position).ToList();
        var velocities = entities.Select(x => x.Velocity).ToList();
        var directions = entities.Select(x => x.Direction).ToList();
        var turnRates = entities.Select(x => x.TurnRate).ToList();

        var barrel = shot.FireOrigin + float3(1, .5f, -2);
        var cell = FarCell(e);
        foreach (var known in new[] { null, Known(shot, ShotResult.Miss), Hit(shot, e.Target, cell, shot.CommitTime) })
            for (var i = 0; i < 25; i++)
            {
                var at = shot.FireTime + i * .37f;
                DrawAhead.Round(shot, known, barrel, .1f, at, .01f);
                DrawAhead.RoundOver(shot, known, at, .01f);
                DrawAhead.RoundEnd(shot, known?.Result);
                DrawAhead.Impact(e.Target, cell, .01f * i);
            }

        Assert.Equal(time, e.Zone.Time);
        Assert.True(e.Zone.PendingShots.Count == 1 && e.Zone.PendingShots[0].Equals(shot), "the pending shot changed");
        Assert.True(e.Zone.Entities.SequenceEqual(entities), "the zone's entities changed");
        Assert.True(entities.Select(x => x.Position).SequenceEqual(positions), "an entity moved");
        Assert.True(entities.Select(x => x.Velocity).SequenceEqual(velocities), "an entity's velocity changed");
        Assert.True(entities.Select(x => x.Direction).SequenceEqual(directions), "an entity turned");
        Assert.True(entities.Select(x => x.TurnRate).SequenceEqual(turnRates), "an entity's turn rate changed");
    }

    // The weapon's Range is frozen with the rest: a round that misses flies on to the range it left with, whatever the stat
    // becomes after.
    [Fact]
    public void RangeIsFrozenAtFire()
    {
        var e = FireBurstRound(out var origin);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(NoLockRange, shot.MaxRange, 4);
        var endBefore = DrawAhead.RoundEnd(shot, ShotResult.Miss);

        // The stat changes after the round left, and the weapon re-reads it.
        ((InstantWeaponData) e.Weapon.WeaponData).Range = Constant(NoLockRange * 5f);
        e.Weapon.Execute(0f);
        Assert.Equal(NoLockRange * 5f, e.Weapon.Range, 4);

        var after = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(NoLockRange, after.MaxRange, 4);
        Assert.Equal(endBefore, DrawAhead.RoundEnd(after, ShotResult.Miss), 4);
        var aim = normalize(float2(2, 1));
        var held = DrawAhead.Round(after, Known(after, ShotResult.Miss), after.FireOrigin, 0f, after.FireTime + 1000f, 0f);
        Assert.Equal(origin.x + aim.x * NoLockRange, held.x, 3);
        Assert.Equal(origin.z + aim.y * NoLockRange, held.z, 3);
    }

    // A direct round at a stationary target `targetRange` ahead (+z) of the shooter, weapon Range `range`, at `speed`;
    // or, with atNothing, the same weapon fired along +z at no target.
    private Engagement FireDirectRound(float speed, float range, float targetRange = 100f, bool atNothing = false)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: speed, weaponRange: range, targetRange: targetRange);
        if (atNothing)
        {
            Aim(e, float2(0, 1));
            e.Shooter.SetTarget(TargetRef.None);
        }
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        return e;
    }

    // A hit, a burst or an outcome not yet known ends at the round's arrival; a miss flies on to the weapon's range and
    // never ends short of its own arrival; only a published miss is over by the clock.
    [Fact]
    public void RoundEndAndRoundOverAreTheOutcomesEnd()
    {
        foreach (var speed in new[] { 20f, 200f })
        {
            var d = FireDirectRound(speed, 1000f);
            var direct = SafeAssert.OnlyShot(d.Zone);
            Assert.Equal(direct.FireTime + 100f / speed, direct.ArrivalTime, 4);
            Assert.Equal(direct.FireTime + 100f / speed, DrawAhead.RoundEnd(direct, null), 4);
            Assert.Equal(direct.FireTime + 100f / speed, DrawAhead.RoundEnd(direct, ShotResult.Hit), 4);
            Assert.Equal(direct.FireTime + 100f / speed, DrawAhead.RoundEnd(direct, ShotResult.Burst), 4);
            Assert.Equal(direct.FireTime + 1000f / speed, DrawAhead.RoundEnd(direct, ShotResult.Miss), 4);

            Assert.False(DrawAhead.RoundOver(direct, null, direct.FireTime + 10000f, 0f));
            foreach (var result in new[] { ShotResult.Hit, ShotResult.Burst })
                Assert.False(DrawAhead.RoundOver(direct, Known(direct, result), direct.FireTime + 10000f, 0f));
            var missEnd = direct.FireTime + 1000f / speed;
            Assert.False(DrawAhead.RoundOver(direct, Known(direct, ShotResult.Miss), missEnd - .01f, 0f));
            Assert.True(DrawAhead.RoundOver(direct, Known(direct, ShotResult.Miss), missEnd, 0f));

            // A target beyond the weapon's range: a miss never ends short of its own arrival.
            var far = FireDirectRound(speed, 100f, targetRange: 150f);
            var beyond = SafeAssert.OnlyShot(far.Zone);
            Assert.Equal(beyond.FireTime + 150f / speed, beyond.ArrivalTime, 4);
            Assert.Equal(beyond.FireTime + 150f / speed, DrawAhead.RoundEnd(beyond, ShotResult.Miss), 4);

            // A round fired at nothing arrives where it left, and a miss flies on to the range.
            var none = FireDirectRound(speed, 100f, atNothing: true);
            var nothing = SafeAssert.OnlyShot(none.Zone);
            Assert.Equal(nothing.FireTime, nothing.ArrivalTime, 4);
            Assert.Equal(nothing.FireTime, DrawAhead.RoundEnd(nothing, null), 4);
            Assert.Equal(nothing.FireTime + 100f / speed, DrawAhead.RoundEnd(nothing, ShotResult.Miss), 4);

        }
    }

    // The frame falls Lead (< one step) after the last step, so a hit or burst is drawn at Zone.Time + Lead; it must stay
    // on the arrival point until the sim resolves it, and only a published miss flies on.
    [Theory]
    [InlineData(20f)]
    [InlineData(200f)]
    public void ARoundIsDrawnNoFurtherThanItsArrivalUntilItMisses(float speed)
    {
        var e = FireDirectRound(speed, 1000f);
        var shot = SafeAssert.OnlyShot(e.Zone);
        var origin = shot.FireOrigin;
        const float step = 1f / 30f;

        foreach (var lead in new[] { 0f, step / 2f, .999f * step })
        {
            var waiting = DrawAhead.Round(shot, null, shot.FireOrigin, 0f, shot.ArrivalTime, lead);
            Assert.Equal(origin.x, waiting.x, 3);
            Assert.Equal(origin.z + 100f, waiting.z, 3);
            var missed = DrawAhead.Round(shot, Known(shot, ShotResult.Miss), shot.FireOrigin, 0f, shot.ArrivalTime, lead);
            Assert.Equal(origin.x, missed.x, 3);
            Assert.Equal(origin.z + 100f + lead * speed, missed.z, 3);
        }

        for (var time = shot.FireTime; time <= shot.ArrivalTime + 1f; time += 1f / 144f)
        {
            var drawn = DrawAhead.Round(shot, null, shot.FireOrigin, 0f, time, 0f);
            Assert.True(drawn.z - origin.z <= 100f + 1e-3f, "the round was drawn past its arrival point");
        }
    }

    // A no-lock contact round stopped by a hull short of max range has its arrival shortened at the commit; the
    // simulation writes the record back before ShotCommitted fires, so a presenter re-reading it there draws the stop
    // at the committed arrival, not the fire-time copy's.
    [Fact]
    public void TheCommittedRecordCarriesTheCommittedArrival()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        AddShip(e, PointAlong(e, float2(0, 1), 25f), 1f, 300);
        ShotOutcome outcome = null;
        PendingShot record = default;
        var found = false;
        using var c = e.Zone.ShotCommitted.Subscribe(o =>
        {
            outcome = o;
            found = e.Zone.TryGetShot(o.ShotId, out record);
        });

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var fired = SafeAssert.OnlyShot(e.Zone);
        Assert.False(fired.Committed);
        e.Zone.Update(.01f);

        Assert.True(found, "the committed record was not readable when ShotCommitted fired");
        Assert.True(record.Committed);
        Assert.Equal(fired.FireTime + 3f, fired.ArrivalTime, 2);
        Assert.Equal(fired.FireTime + 1.05f, record.ArrivalTime, 2);
        Assert.True(record.ArrivalTime < fired.ArrivalTime);

        var stop = DrawAhead.Round(record, outcome, record.FireOrigin, 0f, record.ArrivalTime, 0f);
        var expected = outcome.HasBurstPoint
            ? outcome.BurstPoint
            : record.FireOrigin.xz + record.TravelDirection * (20f * (record.ArrivalTime - record.FireTime));
        Assert.Equal(expected.x, stop.x, 3);
        Assert.Equal(expected.y, stop.z, 3);
        foreach (var later in new[] { 0f, .5f, 5f })
        {
            var drawn = DrawAhead.Round(record, outcome, record.FireOrigin, 0f, record.ArrivalTime + later, 0f);
            Assert.Equal(expected.x, drawn.x, 3);
            Assert.Equal(expected.y, drawn.z, 3);
        }
    }

    // The barrel is only where the round is first seen: its offset from the sim line decays linearly to nothing over
    // blendTime sim seconds from FireTime.
    [Theory]
    [InlineData(.1f)]
    [InlineData(.25f)]
    public void TheBarrelBlendDecaysOnSimTime(float blend)
    {
        var e = FireBurstRound(out var origin);
        var shot = SafeAssert.OnlyShot(e.Zone);
        var aim = normalize(float2(2, 1));
        var offset = float3(1, .5f, -2);
        var barrel = shot.FireOrigin + offset;
        float3 Line(float dt) => float3(origin.x + aim.x * BallisticSpeed * dt, origin.y, origin.z + aim.y * BallisticSpeed * dt);

        void Close(float3 want, float3 got)
        {
            Assert.Equal(want.x, got.x, 3);
            Assert.Equal(want.y, got.y, 3);
            Assert.Equal(want.z, got.z, 3);
        }

        Close(barrel, DrawAhead.Round(shot, null, barrel, blend, shot.FireTime, 0f));
        Close(Line(blend / 2f) + offset * .5f, DrawAhead.Round(shot, null, barrel, blend, shot.FireTime + blend / 2f, 0f));
        Close(Line(blend), DrawAhead.Round(shot, null, barrel, blend, shot.FireTime + blend, 0f));
        Close(Line(2f * blend), DrawAhead.Round(shot, null, barrel, blend, shot.FireTime + 2f * blend, 0f));
        Close(Line(0f), DrawAhead.Round(shot, null, barrel, 0f, shot.FireTime, 0f));
    }

    // Placement depends on sim time alone: any history of frames walked to a checkpoint draws what a fresh call draws, which
    // is the frozen line at the frozen speed.
    [Theory]
    [InlineData(20f)]
    [InlineData(200f)]
    public void ARoundIsAFunctionOfSimTime(float speed)
    {
        var e = FireDirectRound(speed, 1000f);
        var shot = SafeAssert.OnlyShot(e.Zone);
        var checkpoints = new[] { .05f, .3f, 1f, 3f };
        var histories = new[] { new[] { 1f / 144f }, new[] { 1f / 30f }, new[] { 1f / 144f, 1f / 30f, 1f / 90f } };
        var miss = Known(shot, ShotResult.Miss);

        foreach (var frames in histories)
        {
            var elapsed = 0f;
            var frame = 0;
            foreach (var checkpoint in checkpoints)
            {
                while (elapsed + 1e-4f < checkpoint)
                {
                    elapsed = min(checkpoint, elapsed + frames[frame++ % frames.Length]);
                    DrawAhead.Round(shot, miss, shot.FireOrigin, .1f, shot.FireTime + elapsed, 0f);
                }
                var drawn = DrawAhead.Round(shot, miss, shot.FireOrigin, .1f, shot.FireTime + checkpoint, 0f);
                var fresh = DrawAhead.Round(shot, miss, shot.FireOrigin, .1f, shot.FireTime + checkpoint, 0f);
                // A frame that falls `lead` after the step draws what the sim time lead later would.
                foreach (var lead in new[] { 1f / 60f, 1f / 90f })
                {
                    var ahead = DrawAhead.Round(shot, miss, shot.FireOrigin, .1f, shot.FireTime + checkpoint - lead, lead);
                    Assert.Equal(fresh.x, ahead.x, 3);
                    Assert.Equal(fresh.y, ahead.y, 3);
                    Assert.Equal(fresh.z, ahead.z, 3);
                }
                Assert.Equal(fresh.z, drawn.z, 3);
                Assert.Equal(shot.FireOrigin.z + speed * min(checkpoint, 1000f / speed), drawn.z, 3);
                Assert.Equal(shot.FireOrigin.x, drawn.x, 3);
                Assert.Equal(shot.FireOrigin.y, drawn.y, 3);
            }
        }
    }

    // A hit ends at its impact cell on the target's drawn pose when damage lands, not at the arrival point of the line
    // frozen at Fire (ruling direct-arrival-keep-timing): the target has moved and turned since, and is drawn ahead by its
    // last step's velocity and turn. The expected point is computed apart from DrawAhead, by moving the entity itself and
    // asking its own schematic frame (Entity.ToWorldPoint) where the cell is.
    [Theory]
    [InlineData(20f)]
    [InlineData(200f)]
    public void AHitEndsAtItsImpactCellOnTheTargetsDrawnPose(float speed)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: speed, weaponRange: 1000f);
        e.Target.Velocity = float2(10, 0);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.TravelDirection.x > 0f, "the round did not lead the target");
        const float step = 1f / 30f;

        e.Target.Position += float3(40, 0, 0);
        e.Target.Direction = normalize(float2(1, 2));
        e.Target.TurnRate = 3f;
        var cell = FarCell(e);
        var hit = Hit(shot, e.Target, cell, shot.CommitTime);
        var arrivalOnTheLine = DrawAhead.Round(shot, null, shot.FireOrigin, 0f, shot.ArrivalTime, 0f);
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;

        foreach (var lead in new[] { 0f, step / 2f, .999f * step })
        {
            var position = e.Target.Position;
            var direction = e.Target.Direction;
            e.Target.Position += float3(e.Target.Velocity.x, 0, e.Target.Velocity.y) * lead;
            e.Target.Direction = mul(e.Target.Direction, float2x2.Rotate(e.Target.TurnRate * lead));
            var cellPoint = e.Target.ToWorldPoint((float2) cell);
            var expected = float3(cellPoint.x, e.Target.Position.y, cellPoint.y);
            e.Target.Position = position;
            e.Target.Direction = direction;

            Assert.True(length((expected - arrivalOnTheLine).xz) > cellSize, "the fixture put the cell on the frozen line's arrival point");
            var impact = DrawAhead.Impact(e.Target, cell, lead);
            Assert.Equal(expected.x, impact.x, 3);
            Assert.Equal(expected.y, impact.y, 3);
            Assert.Equal(expected.z, impact.z, 3);
            foreach (var simTime in new[] { shot.ArrivalTime, shot.ArrivalTime + .5f })
            {
                var drawn = DrawAhead.Round(shot, hit, shot.FireOrigin, .1f, simTime, lead);
                Assert.Equal(expected.x, drawn.x, 3);
                Assert.Equal(expected.y, drawn.y, 3);
                Assert.Equal(expected.z, drawn.z, 3);
            }
        }
    }

    // A hit leaves its frozen line at the commit tick and steers onto its end by the time damage lands: before the commit it
    // is on the line, half way through the steer it is half way to the impact cell, and a frame drawn ahead by the clock's
    // lead places the round where the sim time that much later would.
    [Fact]
    public void ASteeredRoundLeavesItsLineAtTheCommit()
    {
        var e = FireDirectRound(20f, 1000f);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(shot.FireTime + 5f, shot.ArrivalTime, 3);
        var cell = FarCell(e);
        var commit = shot.ArrivalTime - 2f;
        var hit = Hit(shot, e.Target, cell, commit);

        foreach (var t in new[] { commit - 1f, commit })
        {
            var steered = DrawAhead.Round(shot, hit, shot.FireOrigin, 0f, t, 0f);
            var line = DrawAhead.Round(shot, null, shot.FireOrigin, 0f, t, 0f);
            Assert.Equal(line.x, steered.x, 3);
            Assert.Equal(line.y, steered.y, 3);
            Assert.Equal(line.z, steered.z, 3);
        }

        var m = commit + 1f;
        var onTheLine = DrawAhead.Round(shot, null, shot.FireOrigin, 0f, m, 0f);
        var end = DrawAhead.Impact(e.Target, cell, 0f);
        Assert.True(length((end - onTheLine).xz) > 1f, "the fixture put the impact cell on the line");
        var halfway = DrawAhead.Round(shot, hit, shot.FireOrigin, 0f, m, 0f);
        Assert.Equal(onTheLine.x + (end.x - onTheLine.x) * .5f, halfway.x, 3);
        Assert.Equal(onTheLine.y + (end.y - onTheLine.y) * .5f, halfway.y, 3);
        Assert.Equal(onTheLine.z + (end.z - onTheLine.z) * .5f, halfway.z, 3);

        var ahead = DrawAhead.Round(shot, hit, shot.FireOrigin, 0f, m - .02f, .02f);
        Assert.Equal(halfway.x, ahead.x, 3);
        Assert.Equal(halfway.y, ahead.y, 3);
        Assert.Equal(halfway.z, ahead.z, 3);
    }

    // The sim states facts and draws nothing: no drawing function, and no lead, blend or barrel, lives in FireControl;
    // those belong to DrawAhead, the presentation statics.
    [Fact]
    public void FireControlHasNoDrawingFunctions()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var methods = typeof(FireControl).GetMethods(all).Cast<MethodBase>().Concat(typeof(FireControl).GetConstructors(all)).ToList();
        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.False(method.Name.StartsWith("Round") || method.Name.StartsWith("Draw"), "FireControl has a drawing function: " + method.Name);
            foreach (var parameter in method.GetParameters())
                Assert.False(new[] { "lead", "blendTime", "barrel" }.Contains(parameter.Name), "FireControl takes a drawing parameter: " + parameter.Name);
        }

        foreach (var name in new[] { "Round", "Impact", "RoundOver", "RoundEnd" })
            Assert.NotNull(typeof(DrawAhead).GetMethod(name, BindingFlags.Public | BindingFlags.Static));
    }

    // A proximity round at a target that recedes faster than the round closes bursts at the weapon's range, pulled back
    // along its line, while it arrives when it has flown the distance the target was fired at: its burst point is not
    // the line at its arrival. The end is the sim's burst point, so a presenter that ended at the arrival line would
    // end the tracer short of where the round burst.
    [Fact]
    public void ABurstRoundEndsAtItsBurstPointNotItsArrivalLine()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: BallisticSpeed, fuse: WeaponFuse.Proximity, blastRadius: 4f,
            damage: 100f, weaponRange: NoLockRange, targetRange: 50f);
        e.Target.Velocity = float2(0, 15);
        ShotOutcome outcome = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => outcome = o);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var origin = SafeAssert.OnlyShot(e.Zone).FireOrigin;
        for (var i = 0; i < 100 && outcome == null; i++) e.Zone.Update(.05f);
        Assert.NotNull(outcome);
        Assert.True(outcome.HasBurstPoint);
        Assert.True(e.Zone.TryGetShot(outcome.ShotId, out var record));

        Assert.Equal(origin.x, outcome.BurstPoint.x, 3);
        Assert.Equal(origin.z + NoLockRange, outcome.BurstPoint.y, 3);
        Assert.Equal(origin.z + 50f, DrawAhead.Round(record, null, record.FireOrigin, 0f, record.ArrivalTime, 0f).z, 3);
        var stop = DrawAhead.Round(record, outcome, record.FireOrigin, 0f, record.ArrivalTime, 0f);
        Assert.Equal(outcome.BurstPoint.x, stop.x, 3);
        Assert.Equal(outcome.BurstPoint.y, stop.z, 3);
    }
}
