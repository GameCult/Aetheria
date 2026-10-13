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

    // A design's rated figure is each stat's Max through the live weapon's own formula. Every stat is authored with
    // Min below Max and every divisor distinct, so reading Min, the wrong stat or the wrong operator changes the answer.
    // Mutation: any rated override reads a Min, drops the burst divisor, the charge multiplier or the charge time.
    [Fact]
    public void ARatedFigureIsTheDesignsMaxThroughTheSharedFormula()
    {
        var instant = new InstantWeaponData
        {
            Damage = Span(10, 100), Count = Span(1, 2), Cooldown = Span(8, 2), MinRange = Span(0, 0), Range = Span(5, 400), DamageCurve = Flat()
        };
        Assert.Equal(25f, instant.RatedRangeDamagePerSecond(10f)); // 100 / 2 rounds / 2 s
        Assert.Equal(400f, instant.RatedRange);

        var charged = new ChargedWeaponData
        {
            Damage = Span(10, 100), Count = Span(1, 2), Cooldown = Span(8, 2), ChargeTime = Span(1, 3), MinRange = Span(0, 0),
            Range = Span(5, 400), DamageCurve = Flat(), ChargeFiringDamageMultiplier = 2f
        };
        Assert.Equal(20f, charged.RatedRangeDamagePerSecond(10f)); // 100 / 2 * 2 / (2 s + 3 s)

        var beam = new ConstantWeaponData { Damage = Span(10, 80), MinRange = Span(0, 0), Range = Span(5, 90), DamageCurve = Flat() };
        Assert.Equal(80f, beam.RatedRangeDamagePerSecond(10f));
        Assert.Equal(90f, beam.RatedRange);
    }

    // The rated read and the live read are one formula: a nominal weapon's live figure is its design's rated figure.
    // Mutation: a rated override with its own arithmetic, or a live read that stops calling the shared formula.
    [Fact]
    public void ARatedFigureIsTheLiveFigureOfANominalWeapon()
    {
        var e = Add(float3(0, 0, 0), new[] { "Gun", "Beam" });
        Tick(.1f);
        var weapons = e.Equipment.Where(i => i.Behaviors.Any(b => b is Weapon)).ToArray();
        Assert.Equal(2, weapons.Length);
        foreach (var item in weapons)
        {
            var live = item.Behaviors.OfType<Weapon>().Single();
            var rated = item.Data.Behaviors.OfType<WeaponData>().Single();
            Assert.Equal(100f, live.RangeDamagePerSecond(10f)); // fixture: Damage 100, one round per second
            Assert.Equal(live.RangeDamagePerSecond(10f), rated.RatedRangeDamagePerSecond(10f));
        }
    }
}
