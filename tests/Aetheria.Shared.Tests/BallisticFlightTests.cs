/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Ballistic flight: FireControl.RoundAt is the one statement of where a fired round is at a sim time, from the
// PendingShot's frozen origin, direction, speed and fire time. Unity draws rounds there and integrates nothing, so
// these tests pin the line itself against first principles (the shooter's position, the aim, the authored velocity)
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

    [Fact]
    public void RoundAtIsTheFiredLine()
    {
        // Burst round: leaves the shooter, is where its own speed puts it a second in, and ends on the burst point.
        var e = FireBurstRound(out var origin);
        var burst = SafeAssert.OnlyShot(e.Zone);
        var aim = normalize(float2(2, 1));
        // The shooter moves on after the round left: the line is the one frozen at Fire, not one drawn from where it is now.
        e.Shooter.Position += float3(7, 0, 3);

        var start = FireControl.RoundAt(burst, burst.FireTime);
        Assert.Equal(origin.x, start.x, 3);
        Assert.Equal(origin.z, start.y, 3);

        var second = FireControl.RoundAt(burst, burst.FireTime + 1f);
        Assert.Equal(origin.x + aim.x * BallisticSpeed, second.x, 3);
        Assert.Equal(origin.z + aim.y * BallisticSpeed, second.y, 3);

        var arrival = FireControl.RoundAt(burst, burst.ArrivalTime);
        Assert.Equal(burst.BurstPosition.x, arrival.x, 3);
        Assert.Equal(burst.BurstPosition.z, arrival.y, 3);

        // Past the weapon's range it is held at the range point, however late it is asked.
        var held = FireControl.RoundAt(burst, burst.FireTime + NoLockRange / BallisticSpeed + 50f);
        Assert.Equal(origin.x + aim.x * NoLockRange, held.x, 3);
        Assert.Equal(origin.z + aim.y * NoLockRange, held.y, 3);
        // Before it was fired it is still at the origin, not behind it.
        var before = FireControl.RoundAt(burst, burst.FireTime - 5f);
        Assert.Equal(origin.x, before.x, 3);
        Assert.Equal(origin.z, before.y, 3);

        // Direct round at a stationary target 100 ahead (+z): arrives on the target, and a miss would fly on to the weapon's range.
        var d = Build(TestSettings(), SolidShape(5, 4), velocity: BallisticSpeed, weaponRange: 1000f);
        var dOrigin = d.Shooter.Position;
        FireControl.Fire(d.Weapon, d.WeaponItem, d.Shooter);
        var direct = SafeAssert.OnlyShot(d.Zone);
        var hit = FireControl.RoundAt(direct, direct.ArrivalTime);
        Assert.Equal(d.Target.Position.x, hit.x, 2);
        Assert.Equal(d.Target.Position.z, hit.y, 2);
        Assert.Equal(dOrigin.z + 100f / 2f, FireControl.RoundAt(direct, direct.FireTime + 2.5f).y, 2);
        var heldDirect = FireControl.RoundAt(direct, direct.FireTime + 1000f / BallisticSpeed + 99f);
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

        var late = FireControl.RoundAt(shot, shot.FireTime + 5000f);
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
        var at = FireControl.RoundAt(after, after.FireTime + 1f);
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

        for (var i = 0; i < 25; i++)
            FireControl.RoundAt(shot, shot.FireTime + i * .37f);

        Assert.Equal(time, e.Zone.Time);
        Assert.True(e.Zone.PendingShots.Count == 1 && e.Zone.PendingShots[0].Equals(shot), "the pending shot changed");
        Assert.True(e.Zone.Entities.SequenceEqual(entities), "the zone's entities changed");
        Assert.True(entities.Select(x => x.Position).SequenceEqual(positions), "an entity moved");
        Assert.True(entities.Select(x => x.Velocity).SequenceEqual(velocities), "an entity's velocity changed");
    }
}
