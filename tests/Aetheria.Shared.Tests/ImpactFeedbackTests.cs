/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching.MessagePack;
using MessagePack;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Feedback cut 1: FireControl publishes every landed impact once on the receiver's Capabilities, and
// DrawAhead.Carried / Feedback.Cue derive what a presenter draws. The engagement fixtures are FireControl
// Cut 12.4's (a partial of its class), so every absorb here comes from the production path: Fire, Apply,
// Detonate, Zone.StepMines.
public sealed partial class FireControlCut124Tests
{
    private static List<AbsorbEvent> Collect(Entity entity, List<IDisposable> subs)
    {
        var list = new List<AbsorbEvent>();
        subs.Add(entity.Capabilities.Absorb.Subscribe(list.Add));
        return list;
    }

    // A hull-only ship (no marker item, no armour): its hull loss is exactly the blast share it reports.
    private Ship AddBare(Engagement e, float3 position, int lot)
    {
        var hullRef = e.Items.ItemData.RefOf<ItemData>(e.Items.ItemData.GetByName<HullData>("Hull"));
        var ship = new Ship(e.Items, e.Zone, new EquippableItem { Data = hullRef, Durability = 100000, Lot = lot }, new EntitySettings());
        e.Zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        return ship;
    }

    // Fires until a shot lands off the hull's centre column; returns that outcome and the absorbs the target
    // published for that shot alone.
    private static (ShotOutcome Outcome, List<AbsorbEvent> Absorbs) FireUntilOffCentreHit(Engagement e, List<IDisposable> subs)
    {
        var absorbs = Collect(e.Target, subs);
        ShotOutcome landed = null;
        subs.Add(e.Zone.ShotResolved.Subscribe(o => landed = o));
        for (var i = 0; i < 300; i++)
        {
            absorbs.Clear();
            landed = null;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
            if (landed != null && landed.Hit && abs(e.Target.ToWorldPoint((float2) landed.Cell).x - e.Target.Position.x) > 2f)
                return (landed, absorbs);
        }
        throw new Xunit.Sdk.XunitException("fixture: no off-centre hit within the attempt budget");
    }

    [Fact]
    public void ADirectHullHitPublishesOneHullAbsorbAtTheImpactCell()
    {
        var e = Build(TestSettings(), SolidShape(21, 3), velocity: 0, damage: 100f);
        var subs = new List<IDisposable>();
        var (outcome, absorbs) = FireUntilOffCentreHit(e, subs);
        foreach (var s in subs) s.Dispose();

        var absorb = Assert.Single(absorbs);
        Assert.Equal(AbsorbLayer.Hull, absorb.Layer);
        var cell = e.Target.ToWorldPoint((float2) outcome.Cell);
        Assert.Equal(cell.x, absorb.Position.x, 1e-4);
        Assert.Equal(cell.y, absorb.Position.z, 1e-4);
        Assert.Equal(e.Target.Position.y, absorb.Position.y, 1e-4);
        Assert.Equal(100f, absorb.Magnitude, 1e-3);
        Assert.Equal(float3(0, 0, 1), absorb.Direction);
        Assert.True(abs(absorb.Position.x - e.Target.Position.x) > 1f, "fixture: the impact is more than half a cell off centre");
    }

    [Fact]
    public void AShieldedHitPublishesOneShieldAbsorbAndNoHull()
    {
        var e = Build(TestSettings(), SolidShape(21, 3), velocity: 0, damage: 100f, equipShield: true, shieldCapacity: 100000f);
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f);
        var subs = new List<IDisposable>();
        var (outcome, absorbs) = FireUntilOffCentreHit(e, subs);
        foreach (var s in subs) s.Dispose();

        Assert.True(outcome.Shielded);
        var absorb = Assert.Single(absorbs);
        Assert.Equal(AbsorbLayer.Shield, absorb.Layer);
        var cell = e.Target.ToWorldPoint((float2) outcome.Cell);
        Assert.Equal(cell.x, absorb.Position.x, 1e-4);
        Assert.Equal(cell.y, absorb.Position.z, 1e-4);
        Assert.True(dot(absorb.Position.xz - e.Target.Position.xz, float2(0, 1)) < 0f, "the impact is on the side the shot came from");
    }

    [Fact]
    public void AShieldBreakingDirectHitPublishesHull()
    {
        var e = Build(TestSettings(), SolidShape(21, 3), velocity: 0, damage: 100f, equipShield: true, shieldCapacity: 5f);
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f);
        var subs = new List<IDisposable>();
        var absorbs = Collect(e.Target, subs);
        ShotOutcome landed = null;
        subs.Add(e.Zone.ShotResolved.Subscribe(o => landed = o));
        for (var i = 0; i < 300 && (landed == null || !landed.Hit); i++)
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        foreach (var s in subs) s.Dispose();

        Assert.True(landed != null && landed.Hit && landed.ShieldBroken && !landed.Shielded, "fixture: a shield-breaking hit");
        var absorb = Assert.Single(absorbs);
        Assert.Equal(AbsorbLayer.Hull, absorb.Layer);
    }

    [Fact]
    public void AMissOrAVanishedTargetPublishesNoAbsorb()
    {
        var miss = Build(TestSettings(), SolidShape(5, 4), velocity: 0, damage: 100f, accuracy: 0f);
        var subs = new List<IDisposable>();
        var all = new[] { miss.Target, miss.Shooter }.SelectMany(x => Collect(x, subs)).ToList();
        var seenMiss = false;
        subs.Add(miss.Zone.ShotResolved.Subscribe(o => seenMiss |= !o.Hit));
        for (var i = 0; i < 10; i++)
        {
            FireControl.Fire(miss.Weapon, miss.WeaponItem, miss.Shooter);
            miss.Zone.Update(.01f);
        }
        Assert.True(seenMiss, "fixture: shots resolved as misses");

        var gone = Build(TestSettings(), SolidShape(5, 4), velocity: 50f, damage: 100f);
        var goneAbsorbs = new[] { gone.Target, gone.Shooter }.SelectMany(x => Collect(x, subs)).ToList();
        FireControl.Fire(gone.Weapon, gone.WeaponItem, gone.Shooter);
        gone.Zone.Entities.Remove(gone.Target);
        for (var i = 0; i < 40; i++) gone.Zone.Update(.1f);
        foreach (var s in subs) s.Dispose();

        Assert.Empty(all);
        Assert.Empty(goneAbsorbs);
    }

    // The blast fixture: A sits on the blast point, B beside it (partly in the disc), C far outside.
    private static readonly float2 BlastAt = float2(200f, -300f);

    private (Engagement E, Ship A, Ship B, Ship C) BlastFixture()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), velocity: 0, damage: 100f);
        var y = e.Target.Position.y;
        var a = AddBare(e, float3(BlastAt.x, y, BlastAt.y), 300);
        var b = AddBare(e, float3(BlastAt.x + 7f, y, BlastAt.y), 302);
        var c = AddBare(e, float3(BlastAt.x + 90f, y, BlastAt.y), 304);
        return (e, a, b, c);
    }

    private static void AssertBlastAbsorbs(Ship a, Ship b, float2 at, List<AbsorbEvent> aa, List<AbsorbEvent> bb, List<AbsorbEvent> cc, float aLoss, float bLoss)
    {
        Assert.Empty(cc);
        var ea = Assert.Single(aa);
        var eb = Assert.Single(bb);
        Assert.Equal(AbsorbLayer.Hull, ea.Layer);
        Assert.Equal(AbsorbLayer.Hull, eb.Layer);
        Assert.True(aLoss > 0f && bLoss > 0f, "fixture: both hulls were damaged");
        Assert.Equal(aLoss, ea.Magnitude, 1e-3);
        Assert.Equal(bLoss, eb.Magnitude, 1e-3);
        Assert.True(abs(aLoss - bLoss) > 1f, "fixture: the two shares differ");
        Assert.Equal(float3(at.x, a.Position.y, at.y), ea.Position);
        Assert.Equal(float3(at.x, b.Position.y, at.y), eb.Position);
        Assert.Equal(float3.zero, ea.Direction);
        Assert.Equal(float3(1, 0, 0), eb.Direction);
    }

    [Fact]
    public void ABlastPublishesOneAbsorbPerTouchedEntityWithItsShare()
    {
        var (e, a, b, c) = BlastFixture();
        var subs = new List<IDisposable>();
        var aa = Collect(a, subs); var bb = Collect(b, subs); var cc = Collect(c, subs);
        float aLoss = 0, bLoss = 0;
        subs.Add(a.HullDamage.Subscribe(x => aLoss += x));
        subs.Add(b.HullDamage.Subscribe(x => bLoss += x));

        FireControl.Detonate(e.Zone, BlastAt, 5f, 1000f, DamageType.Kinetic);
        foreach (var s in subs) s.Dispose();

        AssertBlastAbsorbs(a, b, BlastAt, aa, bb, cc, aLoss, bLoss);
    }

    [Fact]
    public void AMineBlastPublishesTheSameWay()
    {
        var (e, a, b, c) = BlastFixture();
        var subs = new List<IDisposable>();
        var aa = Collect(a, subs); var bb = Collect(b, subs); var cc = Collect(c, subs);
        float aLoss = 0, bLoss = 0;
        subs.Add(a.HullDamage.Subscribe(x => aLoss += x));
        subs.Add(b.HullDamage.Subscribe(x => bLoss += x));

        var mine = new Mine
        {
            Id = e.Zone.NextBodyId(), Body = new KinematicBody { Position = BlastAt, Velocity = float2.zero },
            ArmingDelay = 0f, FuseDelay = 0f, Lifetime = .05f, BlastRadius = 5f, Damage = 1000f,
            DamageType = DamageType.Kinetic, Layer = e.Shooter, LayerFaction = e.Shooter.Faction
        };
        e.Zone.Lay(mine);
        for (var i = 0; i < 5 && e.Zone.Mines.Count > 0; i++) e.Zone.Update(.1f);
        foreach (var s in subs) s.Dispose();

        Assert.Empty(e.Zone.Mines);
        AssertBlastAbsorbs(a, b, BlastAt, aa, bb, cc, aLoss, bLoss);
    }

    [Fact]
    public void ABlastShieldDecisionPicksTheLayer()
    {
        // A shield that can take the share: one Shield absorb, no Hull.
        var e = Build(TestSettings(), SolidShape(5, 4), equipShield: true, shieldCapacity: 1000f);
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f);
        var subs = new List<IDisposable>();
        var takes = Collect(e.Target, subs);
        FireControl.Detonate(e.Zone, e.Target.Position.xz, 2f, 50f, DamageType.Kinetic);
        var take = Assert.Single(takes);
        Assert.Equal(AbsorbLayer.Shield, take.Layer);
        Assert.False(e.Target.Shield.Broken);

        // One that cannot: it breaks, and the hull takes the absorb.
        var f = Build(TestSettings(), SolidShape(5, 4), equipShield: true, shieldCapacity: 20f);
        for (var i = 0; i < 30; i++) f.Zone.Update(.1f);
        var breaks = Collect(f.Target, subs);
        FireControl.Detonate(f.Zone, f.Target.Position.xz, 8f, 5000f, DamageType.Kinetic);
        foreach (var s in subs) s.Dispose();
        var brk = Assert.Single(breaks);
        Assert.Equal(AbsorbLayer.Hull, brk.Layer);
        Assert.True(f.Target.Shield.Broken);
    }

    [Fact]
    public void TheImpactIsPublishedBeforeItIsApplied()
    {
        // A lethal hull hit: the handler still sees the unharmed hull and the target still in the zone.
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, damage: 3000000f);
        float? hullInHandler = null;
        var inZone = false;
        var hullBefore = e.Target.Hull.Durability;
        using var sub = e.Target.Capabilities.Absorb.Subscribe(a =>
        {
            hullInHandler = e.Target.Hull.Durability;
            inZone = e.Zone.Entities.Contains(e.Target);
        });
        var deaths = 0;
        using var d = e.Target.Death.Subscribe(_ => deaths++);
        for (var i = 0; i < 300 && hullInHandler == null; i++)
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.Equal(hullBefore, hullInHandler);
        Assert.True(inZone);
        Assert.Equal(1, deaths);
        Assert.False(e.Zone.Entities.Contains(e.Target));

        // A shield absorb: the handler sees the shield before TakeHit has used the reserve.
        var f = Build(TestSettings(), SolidShape(5, 4), velocity: 0, damage: 600f, equipShield: true, shieldCapacity: 1000f);
        for (var i = 0; i < 30; i++) f.Zone.Update(.1f);
        bool? couldTakeInHandler = null;
        using var sub2 = f.Target.Capabilities.Absorb.Subscribe(a => couldTakeInHandler = f.Target.Shield.CanTakeHit(DamageType.Kinetic, 600f));
        for (var i = 0; i < 300 && couldTakeInHandler == null; i++)
        {
            FireControl.Fire(f.Weapon, f.WeaponItem, f.Shooter);
            f.Zone.Update(.01f);
        }
        Assert.True(couldTakeInHandler);
        Assert.False(f.Target.Shield.CanTakeHit(DamageType.Kinetic, 600f), "fixture: the hit then used the reserve");
    }

    [Fact]
    public void CarriedMovesAPointWithTheDrawnHull()
    {
        var e = Build(TestSettings(), SolidShape(9, 5));
        var t = e.Target;
        var cell = new int2(7, 3);
        var world = t.ToWorldPoint((float2) cell);
        var point = float3(world.x, t.Position.y, world.y);

        t.Velocity = float2(3f, -2f);
        t.TurnRate = 0f;
        Assert.Equal(point, DrawAhead.Carried(t, point, 0f));
        var slid = DrawAhead.Carried(t, point, .5f);
        Assert.Equal(point.x + 1.5f, slid.x, 1e-4);
        Assert.Equal(point.z - 1f, slid.z, 1e-4);
        Assert.Equal(point.y, slid.y, 1e-4);

        t.TurnRate = .8f;
        var lead = .5f;
        var centre = DrawAhead.Position(t, lead);
        var turned = DrawAhead.Carried(t, point, lead);
        var before = float2(point.x - t.Position.x, point.z - t.Position.z);
        var after = float2(turned.x - centre.x, turned.z - centre.z);
        Assert.Equal(length(before), length(after), 1e-4);
        Assert.Equal(abs(t.TurnRate * lead), acos(clamp(dot(before, after) / (length(before) * length(after)), -1f, 1f)), 1e-3);

        var raised = DrawAhead.Carried(t, float3(point.x, point.y + 3f, point.z), lead);
        Assert.Equal(turned.y + 3f, raised.y, 1e-4);
        Assert.Equal(DrawAhead.Impact(t, cell, lead), turned);
    }
}

public sealed class ImpactCueTests
{
    private static AbsorbEvent Hit(float magnitude) =>
        new AbsorbEvent(float3(1, 2, 3), float3(0, 0, 1), magnitude, DamageType.Kinetic, AbsorbLayer.Hull);

    [Fact]
    public void IntensityIsTheHitsShareOfMaxDurability()
    {
        var small = Feedback.Cue(Hit(20f), 100f, false, 1f, 1f).Intensity;
        var large = Feedback.Cue(Hit(20f), 400f, false, 1f, 1f).Intensity;
        Assert.Equal(4f, small / large, 1e-4);
        Assert.Equal(.2f, small, 1e-5);

        Assert.Equal(4f, Feedback.Cue(Hit(20f), 100f, false, 1f, .5f).Intensity / Feedback.Cue(Hit(20f), 400f, false, 1f, .5f).Intensity, 1e-4);
        Assert.Equal(.1f, Feedback.Cue(Hit(20f), 100f, false, 1f, .5f).Intensity, 1e-5);

        Assert.Equal(1f, Feedback.Cue(Hit(500f), 100f, false, 1f, 1f).Intensity);
        Assert.Equal(0f, Feedback.Cue(Hit(20f), 0f, false, 1f, 1f).Intensity);

        var cue = Feedback.Cue(Hit(20f), 100f, false, 1f, 1f);
        Assert.Equal(float3(1, 2, 3), cue.Position);
        Assert.Equal(float3(0, 0, 1), cue.Direction);
        Assert.Equal(AbsorbLayer.Hull, cue.Layer);
    }

    [Fact]
    public void ShakeIsZeroOffThePlayerAndScalesWithPreference()
    {
        foreach (var pref in new[] { 0f, .5f, 1f })
            Assert.Equal(0f, Feedback.Cue(Hit(50f), 100f, false, pref, 1f).Shake);

        Assert.Equal(0f, Feedback.Cue(Hit(50f), 100f, true, 0f, 1f).Shake);
        var full = Feedback.Cue(Hit(50f), 100f, true, 1f, 1f).Shake;
        Assert.Equal(.5f, full, 1e-5);
        Assert.Equal(full * .5f, Feedback.Cue(Hit(50f), 100f, true, .5f, 1f).Shake, 1e-5);
    }

    [Fact]
    public void ScreenShakeRoundTrips()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(PlayerGraphicsSettings).Assembly);
        Assert.Equal(1f, new PlayerGraphicsSettings().ScreenShake);
        var bytes = MessagePackSerializer.Serialize(new PlayerGraphicsSettings { ScreenShake = .3f }, options);
        Assert.Equal(.3f, MessagePackSerializer.Deserialize<PlayerGraphicsSettings>(bytes, options).ScreenShake);
    }
}
