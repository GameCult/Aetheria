using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;

// Cut ew-claims-in-the-book (aetheria-ew, classify-from-belief, lie-is-own-emission, belief-one-writer): every act a ship
// makes is stamped with the class its ship claims for the item (the true item's data unless claimed otherwise), the
// observer's book keeps that claim beside the true item, and every classifying read takes the claim while aim keeps the
// truth. The fixtures drive the production path: real ships fire, ping and lock inside Zone.Update.
public sealed partial class ContactRevealTests
{
    private EquippableItemData DataOf(string name) => (EquippableItemData) _items.ItemData.GetByName<ItemData>(name);

    private static PerformanceStat Span(float min, float max) => new PerformanceStat { Min = min, Max = max };

    // A damage curve that falls from 1 to .2 over the range span, so every range argument lands on a different factor.
    private static BezierCurve Falloff() => new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, .2f, 0, 0) } };

    private static BezierCurve Flat() => new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };

    // ---- ClaimsDefaultToTheTruth ----

    [Fact]
    public void ClaimsDefaultToTheTruth()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        Fire(e, gun);
        Assert.Same(gun.Data, Assert.Single(o.RevealsOf(e)).Claimed); // a fired gun claims its own design

        // The hull belief: none below the gear threshold, the true hull at it (TargetGearInfoThreshold .8).
        o.EntityInfoGathered[e] = .79f;
        Assert.Null(o.ContactOf(e).Profile);
        o.EntityInfoGathered[e] = .8f;
        Assert.Same(e.HullData, o.ContactOf(e).Profile);
    }

    [Fact]
    public void AHeardPingDefaultsToTheTruth()
    {
        var p = Add(float3(0, 0, 0), new[] { "Shout" });
        var hearer = Add(float3(149, 0, 0), new[] { "Eye" }); // off the beam: heard, never painted
        PingAt(p);
        Assert.Same(SensorItemOf(p).Data, Assert.Single(hearer.RevealsOf(p)).Claimed);
    }

    [Fact]
    public void APaintDefaultsToTheTruth()
    {
        var (locker, target) = LockOn("Locker");
        Tick(.5f);
        Tick(.5f);
        var lockItem = locker.Equipment.Single(i => i.Behaviors.Any(b => b is LockWeapon));
        Assert.Same(lockItem.Data, Assert.Single(target.IncomingPaints).EmitterClass);
        Assert.Same(lockItem.Data, Assert.Single(target.RevealsOf(locker)).Claimed);
    }

    // ---- AnItemClaimIsWhatTheBookSays ----

    // The book keeps the true item as its key and the claim beside it; RevealedWeapons lists it by the claim, and aim
    // still opens on the true item. Mutation: stamp item.Data in SetSignature, or have RecordReveal store the true data.
    [Fact]
    public void AnItemClaimIsWhatTheBookSays()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        var other = DataOf("Beam");
        e.Claim(gun, other);

        Fire(e, gun);

        var reveal = Assert.Single(o.RevealsOf(e));
        Assert.Same(gun, reveal.Item);
        Assert.Same(other, reveal.Claimed);
        Assert.Same(gun, Assert.Single(o.RevealedWeapons(e)).Item);
        Assert.True(FireControl.IsRevealed(o, gun));
    }

    // ---- AGunClaimedAsNonWeaponIsNotListed ----

    [Fact]
    public void AGunClaimedAsNonWeaponIsNotListed()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        e.Claim(gun, DataOf("Eye"));

        Fire(e, gun);

        Assert.Empty(o.RevealedWeapons(e));
        Assert.Single(o.RevealsOf(e)); // the book still holds the gun, for aim
        Assert.True(FireControl.IsRevealed(o, gun));
    }

    // ---- AHullClaimIsTheProfile ----

    // Mutation: ContactOf reads subject.HullData, or reads the claim below the gear threshold.
    [Fact]
    public void AHullClaimIsTheProfile()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var truth = e.HullData;
        var hull = e.Hull;
        var decoy = new HullData { Name = "Decoy" };
        e.Claim(decoy);
        Fire(e, Guns(e)[0]);

        o.EntityInfoGathered[e] = .79f;
        Assert.Null(o.ContactOf(e).Profile);
        o.EntityInfoGathered[e] = .8f;
        Assert.Same(decoy, o.ContactOf(e).Profile);
        Assert.Same(truth, e.HullData); // the truth is untouched: damage, shape and physics keep it
        Assert.Same(hull, e.Hull);
    }

    // ---- APaintCarriesTheClaim ----

    [Fact]
    public void APaintCarriesTheClaim()
    {
        var locker = Add(float3(0, 0, 0), new[] { "Locker" });
        var target = Add(float3(0, 0, 200));
        var lockItem = locker.Equipment.Single(i => i.Behaviors.Any(b => b is LockWeapon));
        var claimed = DataOf("Eye");
        locker.Claim(lockItem, claimed);
        locker.SetIff(target, true);
        locker.EntityInfoGathered[target] = 1f;
        locker.SetTarget(target);
        locker.Aim = normalize(target.Position - locker.Position);

        Tick(.5f);
        Tick(.5f);

        Assert.Same(claimed, Assert.Single(target.IncomingPaints).EmitterClass);
        var reveal = Assert.Single(target.RevealsOf(locker));
        Assert.Same(lockItem, reveal.Item);
        Assert.Same(claimed, reveal.Claimed);
    }

    // The emission a ping makes is stamped by the ship that makes it. Mutation: Emit stamps item.Data.
    [Fact]
    public void AHeardPingCarriesTheClaim()
    {
        var p = Add(float3(0, 0, 0), new[] { "Shout" });
        var hearer = Add(float3(149, 0, 0), new[] { "Eye" }); // off the beam: heard, never painted
        var claimed = DataOf("Ear");
        p.Claim(SensorItemOf(p), claimed);

        PingAt(p);

        Assert.Same(claimed, Assert.Single(hearer.RevealsOf(p)).Claimed);
    }

    // ---- AClaimEndsWithItsShip ----

    [Fact]
    public void AClaimEndsWithItsShip()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var gun = Guns(e)[0];
        e.Claim(new HullData { Name = "Decoy" });
        e.Claim(gun, DataOf("Eye"));

        e.Deactivate();

        Assert.Same(e.HullData, e.ClaimedHull);
        Assert.Same(gun.Data, e.ClaimOf(gun));
    }

    // ---- Rated figures (ruling ew-claim-weapon-measure-ruled) ----

    // A hull's claim is its own item's claim: the hull's acts (a Reflector's reflection, a launch flash, the hull as the
    // item of any signature) carry the class the profile reports, so the book never holds the true hull beside a decoy.
    // Mutation: ClaimedHull reads a slot of its own, or Claim(HullData) writes one.
    [Fact]
    public void AHullClaimReachesTheHullsOwnReveal()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var decoy = new HullData { Name = "Decoy" };
        e.Claim(decoy);
        Assert.Same(decoy, e.ClaimOf(e.EquippedHull));
        Assert.Same(decoy, e.ClaimedHull);

        e.SetSignature(new object(), SignatureChannel.LaunchFlash, 1000f, e.EquippedHull);
        Tick(.1f);

        var reveal = Assert.Single(o.RevealsOf(e), r => r.Item == e.EquippedHull);
        Assert.Same(decoy, reveal.Claimed);
        o.EntityInfoGathered[e] = .8f;
        Assert.Same(reveal.Claimed, o.ContactOf(e).Profile);

        e.Claim(null);
        Assert.Same(e.HullData, e.ClaimedHull);
        Assert.Same(e.HullData, e.ClaimOf(e.EquippedHull));
    }

    [Fact]
    public void TheHullCanOnlyClaimToBeAHull()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        Assert.Throws<System.ArgumentException>(() => e.Claim(e.EquippedHull, DataOf("Eye")));
        Assert.Same(e.HullData, e.ClaimedHull);
    }

    // ---- Rated figures (ruling ew-claim-weapon-measure-ruled) ----

    // A design's rated figure is each stat's Max through the live weapon's own formula. Every stat is authored with
    // Min below Max, a falling curve and a MinRange above 0, so reading a Min, scaling the range argument, the wrong
    // stat or the wrong operator changes the answer. At 70 the range factor is (70 - 20) / (120 - 20) = .5.
    // Mutation: any rated override reads Range.Min or MinRange.Min or a Min, drops the burst divisor, the charge
    // multiplier or the charge time; the shared formula scales its range argument.
    [Fact]
    public void ARatedFigureIsTheDesignsMaxThroughTheSharedFormula()
    {
        var curve = Falloff();
        var half = curve.Evaluate(.5f);
        Assert.NotEqual(half, curve.Evaluate(1f)); // the Range.Min and halved-range mutants land elsewhere on the curve
        Assert.NotEqual(half, curve.Evaluate(.15f));
        Assert.NotEqual(half, curve.Evaluate(70f / 120f));

        var instant = Spanned(new InstantWeaponData());
        Assert.Equal(100f / 2 * half / 2, instant.RatedRangeDamagePerSecond(70f), 4); // 100 / 2 rounds, curve, / 2 s
        Assert.Equal(120f, instant.RatedRange);

        var charged = (ChargedWeaponData) Spanned(new ChargedWeaponData
        {
            ChargeTime = Span(1, 3), ChargeFiringDamageMultiplier = 2f
        });
        Assert.Equal(100f / 2 * 2 * half / (2 + 3), charged.RatedRangeDamagePerSecond(70f), 4);

        var beam = Spanned(new ConstantWeaponData());
        Assert.Equal(100f * half, beam.RatedRangeDamagePerSecond(70f), 4);
        Assert.Equal(120f, beam.RatedRange);
    }

    // The rated read and the live read are one formula: a nominal weapon's live figure is its design's rated figure,
    // for each kind, at ranges before, inside and past the falloff span, and equals the hand arithmetic.
    // Mutation: a rated override with its own arithmetic, or a live read that drifts from the shared formula (the
    // charged read's ChargeTime, the range, the curve).
    [Fact]
    public void ARatedFigureIsTheLiveFigureOfANominalWeapon()
    {
        var e = Add(float3(0, 0, 0), new[] { "SpanGun", "SpanCharged", "SpanBeam" });
        Tick(.1f);
        var curve = Falloff();
        var expected = new Dictionary<string, Func<float, float>>
        {
            ["SpanGun"] = at => 100f / 2 * curve.Evaluate(Math.Clamp((at - 20f) / 100f, 0f, 1f)) / 2,
            ["SpanCharged"] = at => 100f / 2 * 2 * curve.Evaluate(Math.Clamp((at - 20f) / 100f, 0f, 1f)) / (2 + 3),
            ["SpanBeam"] = at => 100f * curve.Evaluate(Math.Clamp((at - 20f) / 100f, 0f, 1f))
        };
        foreach (var item in e.Equipment.Where(i => i.Behaviors.Any(b => b is Weapon)))
        {
            var live = item.Behaviors.OfType<Weapon>().Single();
            var rated = item.Data.Behaviors.OfType<WeaponData>().Single();
            foreach (var at in new[] { 10f, 70f, 110f, 500f })
            {
                Assert.Equal(expected[item.Data.Name](at), live.RangeDamagePerSecond(at), 3);
                Assert.Equal(live.RangeDamagePerSecond(at), rated.RatedRangeDamagePerSecond(at), 3);
            }
        }
    }
}
