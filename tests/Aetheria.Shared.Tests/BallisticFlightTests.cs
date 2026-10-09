/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Ballistic flight: FireControl.RoundAt is the one statement of where a fired round is at a sim time, from the
// PendingShot's frozen origin, direction, speed and fire time, and FireControl.RoundEnd the one statement of when its
// drawn flight ends for what the sim has published of its outcome. Unity draws rounds through DrawAhead's round statics
// and integrates nothing, so these tests pin the line, the end and the drawing against first principles (the shooter's
// position, the aim, the authored velocity and range) rather than against the code that computes them. Shares the
// Cut 12.4 engagement fixture.
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

    [Fact]
    public void RoundAtIsTheFiredLine()
    {
        // Burst round: leaves the shooter, is where its own speed puts it a second in, and ends on the burst point.
        var e = FireBurstRound(out var origin);
        var burst = SafeAssert.OnlyShot(e.Zone);
        var aim = normalize(float2(2, 1));
        // The shooter moves on after the round left: the line is the one frozen at Fire, not one drawn from where it is now.
        e.Shooter.Position += float3(7, 0, 3);

        var start = FireControl.RoundAt(burst, null, burst.FireTime);
        Assert.Equal(origin.x, start.x, 3);
        Assert.Equal(origin.z, start.y, 3);

        var second = FireControl.RoundAt(burst, null, burst.FireTime + 1f);
        Assert.Equal(origin.x + aim.x * BallisticSpeed, second.x, 3);
        Assert.Equal(origin.z + aim.y * BallisticSpeed, second.y, 3);

        foreach (ShotResult? known in new ShotResult?[] { null, ShotResult.Burst })
            foreach (var later in new[] { 0f, 1f, 50f })
            {
                var arrival = FireControl.RoundAt(burst, known, burst.ArrivalTime + later);
                Assert.Equal(burst.BurstPosition.x, arrival.x, 3);
                Assert.Equal(burst.BurstPosition.z, arrival.y, 3);
            }

        // Past the weapon's range it is held at the range point, however late it is asked.
        var held = FireControl.RoundAt(burst, ShotResult.Miss, burst.FireTime + NoLockRange / BallisticSpeed + 50f);
        Assert.Equal(origin.x + aim.x * NoLockRange, held.x, 3);
        Assert.Equal(origin.z + aim.y * NoLockRange, held.y, 3);
        // Before it was fired it is still at the origin, not behind it.
        var before = FireControl.RoundAt(burst, null, burst.FireTime - 5f);
        Assert.Equal(origin.x, before.x, 3);
        Assert.Equal(origin.z, before.y, 3);

        // Direct round at a stationary target 100 ahead (+z): arrives on the target, and a miss would fly on to the weapon's range.
        var d = Build(TestSettings(), SolidShape(5, 4), velocity: BallisticSpeed, weaponRange: 1000f);
        var dOrigin = d.Shooter.Position;
        FireControl.Fire(d.Weapon, d.WeaponItem, d.Shooter);
        var direct = SafeAssert.OnlyShot(d.Zone);
        foreach (ShotResult? known in new ShotResult?[] { null, ShotResult.Hit })
            // A round not known to miss waits at its arrival point however late it is asked.
            foreach (var at in new[] { direct.ArrivalTime, direct.FireTime + 99f })
            {
                var hit = FireControl.RoundAt(direct, known, at);
                Assert.Equal(d.Target.Position.x, hit.x, 2);
                Assert.Equal(d.Target.Position.z, hit.y, 2);
            }
        Assert.Equal(dOrigin.z + 100f / 2f, FireControl.RoundAt(direct, null, direct.FireTime + 2.5f).y, 2);
        var heldDirect = FireControl.RoundAt(direct, ShotResult.Miss, direct.FireTime + 1000f / BallisticSpeed + 99f);
        Assert.Equal(dOrigin.z + 1000f, heldDirect.y, 2);
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

        var late = FireControl.RoundAt(shot, ShotResult.Miss, shot.FireTime + 5000f);
        Assert.Equal(origin.x, late.x, 3);
        Assert.Equal(origin.z, late.y, 3);
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
        var at = FireControl.RoundAt(after, null, after.FireTime + 1f);
        Assert.Equal(origin.x + aim.x * BallisticSpeed, at.x, 3);
        Assert.Equal(origin.z + aim.y * BallisticSpeed, at.y, 3);
    }

    [Fact]
    public void RoundAtIsPure()
    {
        var e = FireBurstRound(out _);
        var shot = SafeAssert.OnlyShot(e.Zone);
        var time = e.Zone.Time;
        var entities = e.Zone.Entities.ToList();
        var positions = entities.Select(x => x.Position).ToList();
        var velocities = entities.Select(x => x.Velocity).ToList();

        var barrel = shot.FireOrigin + float3(1, .5f, -2);
        for (var i = 0; i < 25; i++)
        {
            var at = shot.FireTime + i * .37f;
            foreach (ShotResult? known in new ShotResult?[] { null, ShotResult.Miss })
            {
                FireControl.RoundAt(shot, known, at);
                FireControl.RoundEnd(shot, known);
                DrawAhead.Round(shot, known, barrel, .1f, at);
                DrawAhead.RoundOver(shot, known, at);
            }
        }

        Assert.Equal(time, e.Zone.Time);
        Assert.True(e.Zone.PendingShots.Count == 1 && e.Zone.PendingShots[0].Equals(shot), "the pending shot changed");
        Assert.True(e.Zone.Entities.SequenceEqual(entities), "the zone's entities changed");
        Assert.True(entities.Select(x => x.Position).SequenceEqual(positions), "an entity moved");
        Assert.True(entities.Select(x => x.Velocity).SequenceEqual(velocities), "an entity's velocity changed");
    }

    // The weapon's Range is frozen with the rest: a round that misses flies on to the range it left with, whatever the stat
    // becomes after.
    [Fact]
    public void RangeIsFrozenAtFire()
    {
        var e = FireBurstRound(out var origin);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(NoLockRange, shot.MaxRange, 4);
        var endBefore = FireControl.RoundEnd(shot, ShotResult.Miss);

        // The stat changes after the round left, and the weapon re-reads it.
        ((InstantWeaponData) e.Weapon.WeaponData).Range = Constant(NoLockRange * 5f);
        e.Weapon.Execute(0f);
        Assert.Equal(NoLockRange * 5f, e.Weapon.Range, 4);

        var after = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(NoLockRange, after.MaxRange, 4);
        Assert.Equal(endBefore, FireControl.RoundEnd(after, ShotResult.Miss), 4);
        var aim = normalize(float2(2, 1));
        var held = FireControl.RoundAt(after, ShotResult.Miss, after.FireTime + 1000f);
        Assert.Equal(origin.x + aim.x * NoLockRange, held.x, 3);
        Assert.Equal(origin.z + aim.y * NoLockRange, held.y, 3);
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
            Assert.Equal(direct.FireTime + 100f / speed, FireControl.RoundEnd(direct, null), 4);
            Assert.Equal(direct.FireTime + 100f / speed, FireControl.RoundEnd(direct, ShotResult.Hit), 4);
            Assert.Equal(direct.FireTime + 100f / speed, FireControl.RoundEnd(direct, ShotResult.Burst), 4);
            Assert.Equal(direct.FireTime + 1000f / speed, FireControl.RoundEnd(direct, ShotResult.Miss), 4);

            foreach (ShotResult? known in new ShotResult?[] { null, ShotResult.Hit, ShotResult.Burst })
                Assert.False(DrawAhead.RoundOver(direct, known, direct.FireTime + 10000f));
            var missEnd = direct.FireTime + 1000f / speed;
            Assert.False(DrawAhead.RoundOver(direct, ShotResult.Miss, missEnd - .01f));
            Assert.True(DrawAhead.RoundOver(direct, ShotResult.Miss, missEnd));

            // A target beyond the weapon's range: a miss never ends short of its own arrival.
            var far = FireDirectRound(speed, 100f, targetRange: 150f);
            var beyond = SafeAssert.OnlyShot(far.Zone);
            Assert.Equal(beyond.FireTime + 150f / speed, beyond.ArrivalTime, 4);
            Assert.Equal(beyond.FireTime + 150f / speed, FireControl.RoundEnd(beyond, ShotResult.Miss), 4);

            // A round fired at nothing arrives where it left, and a miss flies on to the range.
            var none = FireDirectRound(speed, 100f, atNothing: true);
            var nothing = SafeAssert.OnlyShot(none.Zone);
            Assert.Equal(nothing.FireTime, nothing.ArrivalTime, 4);
            Assert.Equal(nothing.FireTime, FireControl.RoundEnd(nothing, null), 4);
            Assert.Equal(nothing.FireTime + 100f / speed, FireControl.RoundEnd(nothing, ShotResult.Miss), 4);

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
            var time = shot.ArrivalTime + lead;
            foreach (ShotResult? known in new ShotResult?[] { null, ShotResult.Hit, ShotResult.Burst })
            {
                var drawn = DrawAhead.Round(shot, known, shot.FireOrigin, 0f, time);
                Assert.Equal(origin.x, drawn.x, 3);
                Assert.Equal(origin.z + 100f, drawn.z, 3);
            }
            var missed = DrawAhead.Round(shot, ShotResult.Miss, shot.FireOrigin, 0f, time);
            Assert.Equal(origin.x, missed.x, 3);
            Assert.Equal(origin.z + 100f + lead * speed, missed.z, 3);
        }

        for (var time = shot.FireTime; time <= shot.ArrivalTime + 1f; time += 1f / 144f)
        {
            var drawn = DrawAhead.Round(shot, null, shot.FireOrigin, 0f, time);
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

        var stop = DrawAhead.RoundStop(record, outcome, record.FireOrigin, 0f);
        var expected = outcome.HasBurstPoint
            ? outcome.BurstPoint
            : record.FireOrigin.xz + record.TravelDirection * (20f * (record.ArrivalTime - record.FireTime));
        Assert.Equal(expected.x, stop.x, 3);
        Assert.Equal(expected.y, stop.z, 3);
        foreach (var later in new[] { 0f, .5f, 5f })
        {
            var drawn = DrawAhead.Round(record, ShotResult.Burst, record.FireOrigin, 0f, record.ArrivalTime + later);
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

        Close(barrel, DrawAhead.Round(shot, null, barrel, blend, shot.FireTime));
        Close(Line(blend / 2f) + offset * .5f, DrawAhead.Round(shot, null, barrel, blend, shot.FireTime + blend / 2f));
        Close(Line(blend), DrawAhead.Round(shot, null, barrel, blend, shot.FireTime + blend));
        Close(Line(2f * blend), DrawAhead.Round(shot, null, barrel, blend, shot.FireTime + 2f * blend));
        Close(Line(0f), DrawAhead.Round(shot, null, barrel, 0f, shot.FireTime));
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

        foreach (var frames in histories)
        {
            var elapsed = 0f;
            var frame = 0;
            foreach (var checkpoint in checkpoints)
            {
                while (elapsed + 1e-4f < checkpoint)
                {
                    elapsed = min(checkpoint, elapsed + frames[frame++ % frames.Length]);
                    DrawAhead.Round(shot, ShotResult.Miss, shot.FireOrigin, .1f, shot.FireTime + elapsed);
                }
                var drawn = DrawAhead.Round(shot, ShotResult.Miss, shot.FireOrigin, .1f, shot.FireTime + checkpoint);
                var fresh = DrawAhead.Round(shot, ShotResult.Miss, shot.FireOrigin, .1f, shot.FireTime + checkpoint);
                Assert.Equal(fresh.z, drawn.z, 3);
                Assert.Equal(shot.FireOrigin.z + speed * min(checkpoint, 1000f / speed), drawn.z, 3);
                Assert.Equal(shot.FireOrigin.x, drawn.x, 3);
                Assert.Equal(shot.FireOrigin.y, drawn.y, 3);
            }
        }
    }

    // Today's sim geometry: the round flies the line frozen at Fire and stops at the arrival point on it, whatever the
    // target does afterwards. (Where a leading round's arrival should lie is the sim's question, never Projectile's.)
    [Fact]
    public void AHitOnAMovingTargetStopsOnItsFrozenLine()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: BallisticSpeed, weaponRange: 1000f);
        e.Target.Velocity = float2(30, 0);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.TravelDirection.x > 0f, "the round did not lead the target");

        e.Target.Position += float3(40, 0, 0);
        var stop = DrawAhead.RoundStop(shot, new ShotOutcome { ShotId = shot.ShotId, Result = ShotResult.Hit }, shot.FireOrigin, 0f);
        var line = shot.FireOrigin.xz + shot.TravelDirection * (BallisticSpeed * (shot.ArrivalTime - shot.FireTime));
        Assert.Equal(line.x, stop.x, 3);
        Assert.Equal(shot.FireOrigin.y, stop.y, 3);
        Assert.Equal(line.y, stop.z, 3);
    }
}
