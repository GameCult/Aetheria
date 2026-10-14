/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;

// Damage falloff (aetheria-release, damage-curve-fate-ruled): a weapon's damage is flat to its effective range, then one
// lerp to DamageAtMaxRange at its Range, and 0 outside [MinRange, Range]. The function, the catalog authored through it,
// and the fire-time freeze that reads it.
public sealed class RangeFalloffTests
{
    private static PerformanceStat Span(float min, float max) => new PerformanceStat { Min = min, Max = max };

    // [20, 60, 120], .25 at the end: flat to 60, then 1 -> .25 over 60 units.
    [Fact]
    public void TheFactorIsFlatToEffectiveThenOneLerp()
    {
        float F(float at) => WeaponData.RangeFactor(20f, 60f, 120f, .25f, at);
        Assert.Equal(0f, F(19.9f));
        Assert.Equal(0f, F(120.1f));
        Assert.Equal(1f, F(20f));
        Assert.Equal(1f, F(40f));
        Assert.Equal(1f, F(60f));
        Assert.Equal(.625f, F(90f), 5);
        Assert.Equal(.25f, F(120f), 5);
    }

    [Fact]
    public void AnUnauthoredOrDegenerateEffectiveRangeIsFlat()
    {
        var fresh = new InstantWeaponData { MinRange = Span(10, 10), Range = Span(100, 100) };
        Assert.True(float.IsPositiveInfinity(fresh.EffectiveRange.Min) && float.IsPositiveInfinity(fresh.EffectiveRange.Max));
        foreach (var at in new[] { 10f, 55f, 100f })
            Assert.Equal(1f, fresh.RatedRangeFactor(at));

        // Below the minimum range the effective range reads as the minimum: full damage only at it, then the lerp.
        Assert.Equal(1f, WeaponData.RangeFactor(20f, 5f, 120f, 0f, 20f));
        Assert.Equal(.5f, WeaponData.RangeFactor(20f, 5f, 120f, 0f, 70f), 5);
        // At or past the maximum range it is flat everywhere inside the gate.
        foreach (var at in new[] { 20f, 70f, 120f })
            Assert.Equal(1f, WeaponData.RangeFactor(20f, 120f, 120f, 0f, at));
        Assert.Equal(1f, WeaponData.RangeFactor(20f, 500f, 120f, 0f, 70f));
    }

    // The properties panel evaluates an unequipped item's stats through ItemManager.Evaluate. An unauthored stat is +infinity at
    // both ends (EffectiveRange, Tracking) and must evaluate to that, not throw: a span of one value has nothing to interpolate.
    [Theory]
    [InlineData("SRMM72", true)]
    [InlineData("Mine Launcher", true)]
    [InlineData("Autocannon", false)]
    public void AnUnequippedWeaponsUnauthoredStatsEvaluate(string design, bool unauthored)
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var data = cache.GetAll<WeaponItemData>().Single(x => x.Name == design);
        var lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(data), Origin = new Attributed(), Quality = .5f, Roles = new System.Collections.Generic.List<RoleFill>() });
        var item = (EquippableItem) items.CreateInstance(lot);
        var weapon = data.Behaviors.OfType<WeaponData>().Single();

        var effective = items.Evaluate(weapon.EffectiveRange, item);
        Assert.Equal(unauthored, float.IsPositiveInfinity(effective));
        Assert.False(float.IsNaN(effective));
        Assert.True(float.IsPositiveInfinity(items.Evaluate(weapon.Tracking, item)) || !unauthored);
    }

    [Fact]
    public void ShippedWeaponsFallOffAsAuthored()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var weapons = cache.GetAll<WeaponItemData>().SelectMany(i => i.Behaviors.OfType<WeaponData>().Select(w => (i.Name, w))).ToArray();
        Assert.Equal(27, weapons.Length);
        var falling = weapons.Where(x => !(x.w is LockWeaponData || x.w is GuidedWeaponData || x.w is MineLayerData)).ToArray();
        Assert.Equal(21, falling.Length);
        foreach (var (name, weapon) in falling)
        {
            Assert.True(weapon.EffectiveRange.Min == weapon.Range.Min * .5f && weapon.EffectiveRange.Max == weapon.Range.Max * .5f, $"{name}: effective range is half the range");
            Assert.Equal(weapon.Range.Terms.Select(t => (t.Source, t.Exponent, t.Role)), weapon.EffectiveRange.Terms.Select(t => (t.Source, t.Exponent, t.Role)));
            Assert.Equal(0f, weapon.DamageAtMaxRange);
        }
        foreach (var (name, weapon) in weapons.Except(falling))
            Assert.True(float.IsPositiveInfinity(weapon.EffectiveRange.Max) && float.IsPositiveInfinity(weapon.EffectiveRange.Min), $"{name}: no falloff");
        foreach (var (name, weapon) in weapons)
            for (var i = 0; i <= 64; i++)
                Assert.True(weapon.RatedRangeFactor(lerp(weapon.MinRange.Min, weapon.Range.Max, i / 64f)) <= 1f, $"{name}: factor above 1");
    }
}

public sealed partial class FireControlCut124Tests
{
    private ShotRecord FreezeAt(float targetRange, float effective = 50f, float weaponRange = 150f, float? damageOverride = null)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, damage: 100f, weaponRange: weaponRange, targetRange: targetRange,
            effectiveRange: effective, damageAtMaxRange: 0f);
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter, damageOverride);
        Assert.NotEqual(0, shotId);
        return new ShotRecord { Damage = e.Zone.PendingShots.Single(s => s.ShotId == shotId).Damage };
    }

    private struct ShotRecord { public float Damage; }

    // The observation is where damage is decided at fire time: the shot right after Fire, before anything resolves.
    [Fact]
    public void FireFreezesDamageTimesTheFactorAtDeliveryRange()
    {
        Assert.Equal(50f, FreezeAt(100f).Damage, 3);   // halfway from 50 to 150
        Assert.Equal(100f, FreezeAt(40f).Damage, 3);   // inside the effective range
        Assert.Equal(0f, FreezeAt(150f).Damage, 3);    // at the maximum range
    }

    // A no-lock fused round reads the falloff where its burst point is decided, at its commit: the shot right after the tick
    // that committed it, still in the air (velocity 20). With a clear line it travels to Range ("bomb fishing") and lands at
    // the end multiplier.
    private float NoLockDamageAfterCommit(float effective, float blockerAt, out float travelled)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, penetration: 1f,
            damage: 100f, weaponRange: NoLockRange, effectiveRange: effective, damageAtMaxRange: .25f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        if (blockerAt > 0f) AddShip(e, PointAlong(e, float2(0, 1), blockerAt), 100000f, 300);
        ShotOutcome outcome = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => outcome = o);
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        Assert.NotEqual(0, shotId);
        e.Zone.Update(.01f);
        Assert.True(outcome != null && outcome.HasBurstPoint, "the round committed a burst");
        travelled = length(outcome.BurstPoint - e.Shooter.Position.xz);
        return e.Zone.PendingShots.Single(s => s.ShotId == shotId).Damage;
    }

    [Fact]
    public void AFusedRoundFishedToRangeLandsAtTheEndMultiplier()
    {
        Assert.Equal(25f, NoLockDamageAfterCommit(effective: 20f, blockerAt: 0f, out var travelled), 3);
        Assert.Equal(NoLockRange, travelled, 3);
    }

    // The round stops on the first hull ahead, 21 out (well inside a 30 effective range of a 60 range weapon): it delivers
    // full damage there, not the end multiplier of the point it was planned to. With a shorter effective range (10) the
    // factor is the lerp at the distance it actually travelled.
    [Fact]
    public void ANoLockRoundStoppingShortReadsTheFactorWhereItStops()
    {
        Assert.Equal(100f, NoLockDamageAfterCommit(effective: 30f, blockerAt: 25f, out var travelled), 3);
        Assert.True(travelled < NoLockRange - 20f, "the round stopped short of Range");
        var damage = NoLockDamageAfterCommit(effective: 10f, blockerAt: 25f, out var stopped);
        Assert.Equal(travelled, stopped, 3);
        Assert.Equal(100f * lerp(1f, .25f, (stopped - 10f) / (NoLockRange - 10f)), damage, 3);
    }

    // A beam roll fires through damageOverride: the override is what the factor multiplies.
    [Fact]
    public void ABeamRollCarriesTheFactor() => Assert.Equal(5f, FreezeAt(100f, damageOverride: 10f).Damage, 3);
}
