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

// The mount term (docs/aetheria-release-map.md, "Tracking per gun"): how fast a gun's mount can follow a target
// sweeping across the shooter's sky, one factor of every hit price. Rulings tracking-per-gun ("gun times ship"),
// weapon-tracking-authored (a rate is authored per gun, never derived from its footprint), jink-per-ship and
// evasion-angular-velocity. Reuses EvasionTermTests' fixtures: Longinus gun platforms and a generated Djinni.
public sealed partial class RunStartTests
{
    private static PerformanceStat EvRate(float rate) => new PerformanceStat { Min = rate, Max = rate };

    // The gun's rate, set on its design the way an author sets it, flown two updates so the weapon reads it.
    private static void EvSetRate(Weapon weapon, Ship shooter, float rate)
    {
        weapon.WeaponData.Tracking = EvRate(rate);
        shooter.Update(EvDt);
        shooter.Update(EvDt);
        Assert.Equal(rate, weapon.Tracking);
    }

    // The range fixture with the shooter's projectile gun at one rate and its laser at another, the target seen. The
    // shooter is put back at the origin at rest, so the geometry is exactly what the test says.
    private EvRange EvMountRange(float gunRate, float laserRate)
    {
        var range = EvRangeLaunch();
        EvSetRate(range.Gun, range.Gunner, gunRate);
        EvSetRate(range.GunnerLaser, range.Gunner, laserRate);
        range.Gunner.Position = float3(0, range.Gunner.Position.y, 0);
        range.Gunner.Velocity = float2(0, 0);
        Assert.True(range.Gunner.VisibleEntities.Contains(range.Target), "fixture: the shooter still sees the target");
        return range;
    }

    // The two guns of the one shooter: same gear, same ship, so only the gun's own rate differs.
    private static (Weapon weapon, Ship shooter)[] EvGuns(EvRange range) => new[] { (range.Gun, range.Gunner), (range.GunnerLaser, range.Gunner) };

    [Fact]
    public void AngularVelocityIsTransverseOverRange()
    {
        // 150 m/s across a thousand metres is 0.15 rad/s, whichever way across, whichever way the line of sight points.
        var expected = degrees(.15f);
        Assert.Equal(expected, FireControl.AngularVelocity(float2(1000, 0), float2(0, 150)), 3);
        Assert.Equal(expected, FireControl.AngularVelocity(float2(1000, 0), float2(0, -150)), 3);
        Assert.Equal(expected, FireControl.AngularVelocity(float2(0, 1000), float2(150, 0)), 3);
        Assert.Equal(expected, FireControl.AngularVelocity(float2(-1000, 0), float2(0, 150)), 3);
        var angle = radians(37f);
        float2 Turn(float2 v) => float2(v.x * cos(angle) - v.y * sin(angle), v.x * sin(angle) + v.y * cos(angle));
        Assert.Equal(expected, FireControl.AngularVelocity(Turn(float2(1000, 0)), Turn(float2(0, 150))), 3);
        // Only the transverse part counts: radial velocity is no sweep at all, and a general case reads both terms.
        Assert.Equal(0f, FireControl.AngularVelocity(float2(1000, 0), float2(150, 0)), 4);
        Assert.Equal(degrees(17000f / 250000f), FireControl.AngularVelocity(float2(300, 400), float2(-20, 30)), 3);
        // A target nearer than a metre is read at a metre: 1 m/s across half a metre is 0.5 rad/s, not 2.
        Assert.Equal(degrees(.5f), FireControl.AngularVelocity(float2(.5f, 0), float2(0, 1)), 3);

        // The entity overload reads the relative velocity, planar: a common velocity is no sweep, and the shooter's own
        // motion is subtracted from the target's.
        var (_, source, target) = EvFleet();
        source.Position = float3(0, 0, 0);
        target.Position = float3(1000, 0, 0);
        source.Velocity = float2(30, 40);
        target.Velocity = float2(30, 40);
        Assert.Equal(0f, FireControl.AngularVelocity(source, target), 4);
        // Wherever the pair stands: the offset is target minus source.
        source.Position = float3(500, 0, 300);
        target.Position = float3(1500, 0, 300);
        source.Velocity = float2(0, 100);
        target.Velocity = float2(0, 250);
        Assert.Equal(expected, FireControl.AngularVelocity(source, target), 3);
    }

    [Fact]
    public void TheMountHitsHalfAtItsRate()
    {
        foreach (var rate in new[] { .5f, 3f, 40f })
        {
            Assert.Equal(.5f, FireControl.PMount(rate, rate), 6);
            Assert.Equal(1f, FireControl.PMount(0f, rate));
            Assert.Equal(1f / 16f, FireControl.PMount(2 * rate, rate), 6);
            Assert.Equal(1f, FireControl.PMount(rate, float.PositiveInfinity));
        }
        Assert.Equal(1f, FireControl.PMount(1e6f, float.PositiveInfinity));
        // A mount that follows nothing hits only what stands still.
        Assert.Equal(0f, FireControl.PMount(1f, 0f));
        Assert.Equal(1f, FireControl.PMount(0f, 0f));
        Assert.Equal(0f, FireControl.PMount(1f, -2f));
        var previous = float.PositiveInfinity;
        for (var omega = 0f; omega <= 5f; omega += .25f)
        {
            var p = FireControl.PMount(omega, 1.5f);
            Assert.True(p < previous, $"PMount falls as the sweep rises: {p} after {previous} at {omega}");
            previous = p;
        }
    }

    // Ruling tracking-per-gun, "gun times ship": the rate is the gun's authored rate times the shooter's gear over the
    // unaided baseline, and nothing else multiplies the two.
    [Fact]
    public void TrackingRateIsGunTimesShip()
    {
        var range = EvMountRange(40f, 3f);
        var unaided = _items.GameplaySettings.UnaidedTracking;
        foreach (var (weapon, shooter) in EvGuns(range))
        {
            var gear = FireControl.Tracking(shooter);
            Assert.True(gear > unaided * 1.2f, $"fixture: the shooter's targeting gear tracks better than unaided ({gear} against {unaided})");
            var expected = weapon.Tracking * gear / unaided;
            Assert.InRange(FireControl.TrackingRate(weapon, shooter), expected * .999f, expected * 1.001f);
        }
        var with = EvGuns(range).Select(g => FireControl.TrackingRate(g.weapon, g.shooter)).ToArray();

        // The gear off: both guns' rates fall by the same ratio, and the ratio between the guns does not move.
        foreach (var (_, shooter) in EvGuns(range))
        {
            shooter.GetBehavior<TargetingSystem>().Item.EquippableItem.Durability = 0f;
            shooter.Update(EvDt);
            shooter.Update(EvDt);
            Assert.Equal(unaided, FireControl.Tracking(shooter));
        }
        var without = EvGuns(range).Select(g => FireControl.TrackingRate(g.weapon, g.shooter)).ToArray();
        Assert.Equal(40f, without[0], 3);
        Assert.Equal(3f, without[1], 3);
        Assert.Equal(with[0] / without[0], with[1] / without[1], 3);
        Assert.True(with[0] / without[0] > 1.2f);
        Assert.Equal(with[0] / with[1], without[0] / without[1], 3);
    }

    // Ruling weapon-tracking-authored, in play: a fast crosser close in is beyond a heavy gun and within a light one.
    [Fact]
    public void LightAndHeavyGunsSplitOnAFastCloseCrosser()
    {
        var range = EvMountRange(40f, 3f);
        EvCross(range, jinking: false, float2(100, 0));
        var unaided = _items.GameplaySettings.UnaidedTracking;
        var omega = degrees(100f / 200f);
        var mounts = new List<float>();
        foreach (var (weapon, shooter) in EvGuns(range))
        {
            var d = FireControl.Inspect(weapon, shooter, range.Target);
            var rate = weapon.Tracking * FireControl.Tracking(shooter) / unaided;
            Assert.InRange(d.AngularVelocity, omega * .999f, omega * 1.001f);
            Assert.InRange(d.TrackingRate, rate * .999f, rate * 1.001f);
            Assert.Equal((float) Math.Pow(.5, Math.Pow(omega / rate, 2)), d.PMount, 4);
            var price = FireControl.HitProbability(weapon, shooter, range.Target);
            var withoutMount = d.PFire * d.PSpread * d.POnHull * d.PEvasion;
            Assert.True(withoutMount > 0f, "fixture: the shot is priced without the mount");
            Assert.InRange(price, withoutMount * d.PMount * .999f, withoutMount * d.PMount * 1.001f);
            Assert.Equal(price, d.PBase, 6);
            mounts.Add(d.PMount);
        }
        Assert.True(mounts[0] >= .85f, $"the light gun follows a fast close crosser: {mounts[0]}");
        Assert.True(mounts[1] <= .01f, $"the heavy gun does not: {mounts[1]}");
    }

    // Ruling jink-per-ship: the changes-of-vector term is forgiven by the ship's gear alone, in metres, whatever the gun.
    // The target flies straight along the line of sight after jinking (no sweep, PMount 1), so the evasion is all there is.
    [Fact]
    public void TheJinkIsStillForgivenPerShip()
    {
        var range = EvMountRange(40f, 3f);
        EvCross(range, jinking: true, float2(0, -50));
        foreach (var (weapon, shooter) in EvGuns(range))
        {
            var d = FireControl.Inspect(weapon, shooter, range.Target);
            Assert.Equal(1f, d.PMount, 4);
            Assert.True(d.Evasion > 0f, "fixture: the target jinks");
            Assert.Equal(FireControl.PDeviation(d.Evasion, FireControl.Tracking(shooter)), d.PEvasion, 5);
            var id = FireControl.Fire(weapon, weapon.Item, shooter);
            Assert.True(id > 0, "fixture: the weapon fires");
            var shot = range.Arena.PendingShots.Single(s => s.ShotId == id);
            Assert.Equal(FireControl.Tracking(shooter), shot.Tracking);
        }
    }

    // Finding jink-price-unpinned, ruling jink-per-ship at the price layer: with the target flying straight at the shooter (no
    // sweep, PMount 1) the live price of two guns of different rates carries the same jink factor, PDeviation(Evasion,
    // Tracking(shooter)), whatever their rates. The prices differ by the guns' own spread and fire factors; the jink does not.
    [Fact]
    public void TheJinkIsForgivenPerShipInThePrice()
    {
        var range = EvMountRange(40f, 3f);
        EvCross(range, jinking: true, float2(0, -50));
        var guns = EvGuns(range);
        Assert.NotEqual(guns[0].weapon.Tracking, guns[1].weapon.Tracking);
        var factors = new List<float>();
        foreach (var (weapon, shooter) in guns)
        {
            var d = FireControl.Inspect(weapon, shooter, range.Target);
            Assert.Equal(1f, d.PMount, 4);
            var forgiven = FireControl.PDeviation(d.Evasion, FireControl.Tracking(shooter));
            Assert.True(forgiven > .01f && forgiven < .99f, $"fixture: the jink prices between nothing and everything: {forgiven}");
            var shared = d.PFire * d.PMount * d.PSpread * d.POnHull;
            Assert.True(shared > 0f, "fixture: the shared factors price the shot");
            var price = FireControl.HitProbability(weapon, shooter, range.Target);
            Assert.InRange(price, shared * forgiven * .999f, shared * forgiven * 1.001f);
            factors.Add(price / shared);
        }
        Assert.InRange(factors[0], factors[1] * .999f, factors[1] * 1.001f);
    }

    // Finding zero-gear-nan: a gun with no authored limit follows anything, whatever the gear reads. Infinity times a gear
    // that reads zero (active, not yet executed) is still infinity, so the price stays a number; a limited gun on that gear
    // follows nothing that moves (rate 0). Both keep the ship's gear as the other factor of the product (tracking-per-gun).
    [Fact]
    public void AnUnlimitedGunFollowsAnythingOnGearThatReadsZero()
    {
        var range = EvMountRange(float.PositiveInfinity, 3f);
        EvCross(range, jinking: false, float2(100, 0));
        var gear = range.Gunner.GetBehaviors<TargetingSystem>().First(t => t.Item.Active.Value);
        Assert.True(FireControl.Tracking(range.Gunner) > 0f, "fixture: the gear reads a positive Tracking before it is zeroed");
        ((TargetingSystemData) gear.Data).Tracking = EvRate(0f);
        range.Gunner.Update(EvDt);
        range.Gunner.Update(EvDt);
        Assert.Equal(0f, FireControl.Tracking(range.Gunner));
        Assert.True(float.IsPositiveInfinity(FireControl.TrackingRate(range.Gun, range.Gunner)), "an unlimited gun's rate is +infinity on any gear");
        Assert.Equal(1f, FireControl.Inspect(range.Gun, range.Gunner, range.Target).PMount);
        Assert.False(float.IsNaN(FireControl.HitProbability(range.Gun, range.Gunner, range.Target)));
        Assert.Equal(0f, FireControl.TrackingRate(range.GunnerLaser, range.Gunner));
        Assert.Equal(0f, FireControl.Inspect(range.GunnerLaser, range.Gunner, range.Target).PMount);
    }

    // The mount factor is frozen at fire inside PFire, and the commit price is the live price.
    [Fact]
    public void AShotFreezesItsMount()
    {
        var range = EvMountRange(40f, 3f);
        EvCross(range, jinking: false, float2(100, 0));
        foreach (var (weapon, shooter) in EvGuns(range))
        {
            var d = FireControl.Inspect(weapon, shooter, range.Target);
            var live = FireControl.HitProbability(weapon, shooter, range.Target);
            Assert.True(d.PMount < 1f && d.PMount > 0f && d.PFire > 0f, "fixture: the mount prices the shot");
            var id = FireControl.Fire(weapon, weapon.Item, shooter);
            Assert.True(id > 0, "fixture: the weapon fires");
            var shot = range.Arena.PendingShots.Single(s => s.ShotId == id);
            Assert.InRange(shot.PFire, d.PFire * d.PMount * .999f, d.PFire * d.PMount * 1.001f);
            var commit = FireControl.CommitProbability(shot, range.Arena.Time, out _);
            Assert.InRange(commit, live * .999f, live * 1.001f);

            // The target turns to fly straight along the line of sight: the live mount factor is now 1, the frozen one is not.
            range.Target.Velocity = float2(0, 100);
            Assert.Equal(1f, FireControl.Inspect(weapon, shooter, range.Target).PMount, 4);
            shot = range.Arena.PendingShots.Single(s => s.ShotId == id);
            Assert.InRange(shot.PFire, d.PFire * d.PMount * .999f, d.PFire * d.PMount * 1.001f);
            Assert.InRange(FireControl.CommitProbability(shot, range.Arena.Time, out _), commit * .999f, commit * 1.001f);
            range.Target.Velocity = float2(100, 0);
        }

        // A round fired at nothing carries no mount factor to freeze: it prices at nothing and nothing throws.
        range.Gunner.SetTarget(TargetRef.None);
        var untargeted = FireControl.Fire(range.Gun, range.Gun.Item, range.Gunner);
        Assert.True(untargeted > 0, "fixture: the weapon fires at nothing");
        Assert.Equal(0f, range.Arena.PendingShots.Single(s => s.ShotId == untargeted).PFire);
    }

    // A gun nobody has authored a rate for follows anything: +infinity, and PMount 1 exactly.
    [Fact]
    public void AnUnauthoredWeaponHasNoMountLimit()
    {
        var unauthored = new AutoWeaponData().Tracking;
        Assert.True(float.IsPositiveInfinity(unauthored.Min) && float.IsPositiveInfinity(unauthored.Max), "a code-built weapon's rate is +infinity");

        var range = EvMountRange(5f, 5f);
        EvCross(range, jinking: false, float2(100, 0));
        var limited = FireControl.HitProbability(range.Gun, range.Gunner, range.Target);
        range.Gun.WeaponData.Tracking = unauthored;
        range.Gunner.Update(EvDt);
        range.Gunner.Update(EvDt);
        Assert.True(float.IsPositiveInfinity(range.Gun.Tracking), $"it evaluates to +infinity: {range.Gun.Tracking}");
        Assert.True(float.IsPositiveInfinity(FireControl.TrackingRate(range.Gun, range.Gunner)));
        var d = FireControl.Inspect(range.Gun, range.Gunner, range.Target);
        Assert.Equal(1f, d.PMount);
        var price = FireControl.HitProbability(range.Gun, range.Gunner, range.Target);
        var withoutMount = d.PFire * d.PSpread * d.POnHull * d.PEvasion;
        Assert.InRange(price, withoutMount * .999f, withoutMount * 1.001f);
        Assert.True(price > limited * 2f, $"the mount limit priced the limited gun down: {limited} against {price}");
    }

    // The shipped catalog's weapon behaviours: launchers, guided weapons and mine layers (which lay at no bearing)
    // never mount-track, every other gun is a mount.
    private static bool EvMountless(WeaponData data) => data is LauncherData || data is GuidedWeaponData || data is MineLayerData;

    private IEnumerable<(WeaponItemData item, WeaponData data)> EvCatalogGuns() =>
        _cache.GetAll<WeaponItemData>().SelectMany(item => item.Behaviors.OfType<WeaponData>().Select(data => (item, data)));

    // Data check, not a behaviour test: it reads the migrated catalog. Pins finiteness and the authored shape of the
    // value (Min = Max > 0), never a value (ruling weapon-tracking-authored).
    [Fact]
    public void EveryCatalogGunTracks()
    {
        var guns = EvCatalogGuns().ToArray();
        Assert.NotEmpty(guns);
        foreach (var (item, data) in guns)
        {
            var rate = data.Tracking;
            if (EvMountless(data))
                Assert.True(float.IsPositiveInfinity(rate.Min) && float.IsPositiveInfinity(rate.Max), $"{item.Name} is a launcher or guided weapon and tracks without a mount limit");
            else
            {
                Assert.True(!float.IsInfinity(rate.Min) && !float.IsNaN(rate.Min), $"{item.Name} has a finite rate, not {rate.Min}");
                Assert.Equal(rate.Min, rate.Max);
                Assert.True(rate.Min > 0f, $"{item.Name} has a positive rate, not {rate.Min}");
            }
        }
    }

    // Data check, not a behaviour test: ruling plight-shape, plight is one wide, two high and fits a Longinus Energy hardpoint.
    [Fact]
    public void PlightHasItsRuledShape()
    {
        var plight = _cache.GetAll<WeaponItemData>().Single(w => w.Name == "plight");
        Assert.Equal(1, plight.Shape.Width);
        Assert.Equal(2, plight.Shape.Height);
        Assert.True(plight.Shape.Cells[0, 0] && plight.Shape.Cells[0, 1], "both cells are occupied");
        var longinus = _cache.GetAll<HullData>().Single(h => h.Name == "Longinus");
        Assert.Contains(longinus.Hardpoints, hardpoint => hardpoint.Type == HardpointType.Energy && hardpoint.Takes(plight));
    }
}
