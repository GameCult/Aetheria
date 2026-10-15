using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Mount cycle (rulings cycle-skips-only-perceived-destroyed, quiet-gear-identifiable-hull-layout-known, reveal-ends-on-perception,
// same-ship-same-inputs): the player's target-item cycle visits every mount the observer has located on its target, empty or
// not, and skips only the mounts whose item it perceived destroyed. What the cycle holds depends on the observer's own
// state -- its identification of the ship, its book, the destructions it sensed -- and on nothing the target alone knows:
// a ship lying about its hull and an honest ship of the claimed hull offer the same cycle. Fixtures are LayoutAimTests'
// (Bay: reactor mount, shield mount, sensors mount; Wing: two sensors mounts). Hardpoints and items are compared with
// ReferenceEquals, as there.
public sealed partial class ContactRevealTests
{
    private static HardpointData[] MountsOf(IEnumerable<AimTarget> cycle) => cycle.Where(a => a.Mount != null).Select(a => a.Mount).ToArray();

    private static bool SameMounts(HardpointData[] a, HardpointData[] b) => a.Length == b.Length && a.Zip(b, (x, y) => ReferenceEquals(x, y)).All(same => same);

    // Whether a cycle entry is exactly `item` or exactly `mount`, by reference.
    private static bool Is(AimTarget aim, EquippedItem item, HardpointData mount) => ReferenceEquals(aim.Item, item) && ReferenceEquals(aim.Mount, mount);

    // ---- TheCycleCoversEveryLocatedMount ----

    // An identified honest Bay with its gun seen firing and its shield mount empty. The cycle lists the revealed gun, then every
    // mount of the hull in layout order -- the empty shield mount included -- and CycleAim visits each in turn and wraps.
    [Fact]
    public void TheCycleCoversEveryLocatedMount()
    {
        var bay = AddBay(float3(0, 0, 100), gun: true);
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[bay] = 1f;
        var gun = Guns(bay).Single();
        observer.RecordReveal(bay, gun, SignatureChannel.LaunchFlash, gun.Data);
        var mounts = bay.HullData.Hardpoints;
        Assert.True(bay.Equipment.All(i => i.Hardpoint == null || !ReferenceEquals(i.Hardpoint, mounts[1])), "fixture: the shield mount is empty");
        Target(observer, bay);

        var cycle = observer.AimCycle(bay).ToArray();
        Assert.Equal(4, cycle.Length);
        Assert.True(Is(cycle[0], gun, null), "the revealed gun leads");
        Assert.True(Is(cycle[1], null, mounts[0]) && Is(cycle[2], null, mounts[1]) && Is(cycle[3], null, mounts[2]), "then every mount in layout order, the empty one included");

        for (var i = 0; i < 5; i++)
        {
            observer.CycleAim();
            var want = cycle[i % 4];
            Assert.True(Is(observer.ResolvedAim, want.Item, want.Mount), $"step {i} of the cycle visits entry {i % 4}, wrapping after the last");
        }
    }

    // ---- UnderAHullLieTheCycleIsTheClaimedHulls ----

    // A Wing claiming to be a Bay and an honest Bay, in the observer's info alike (identified, nothing revealed): the cycle's
    // mounts are the claimed hull's own HardpointData in its order -- three, not the Wing's two -- each can be selected, and a
    // mount aim prices a shot at the same cells whichever ship it is aimed on. The cycle shows nothing of the lie.
    [Fact]
    public void UnderAHullLieTheCycleIsTheClaimedHulls()
    {
        var honest = AddBay(float3(0, 0, 100));
        var (liar, _, _) = AddWing(float3(0, 0, -100));
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[honest] = 1f;
        observer.EntityInfoGathered[liar] = 1f;
        liar.Claim(honest.HullData);
        Assert.Equal(2, liar.HullData.Hardpoints.Count);

        var honestMounts = MountsOf(observer.AimCycle(honest));
        var liarMounts = MountsOf(observer.AimCycle(liar));
        Assert.Equal(3, honestMounts.Length);
        Assert.True(SameMounts(honestMounts, liarMounts), "the same HardpointData in the same order");
        Assert.True(SameMounts(honest.HullData.Hardpoints.ToArray(), liarMounts), "and they are the claimed hull's");
        Assert.Equal(3, observer.AimCycle(liar).Count());

        foreach (var (subject, mounts) in new[] { (honest, honestMounts), (liar, liarMounts) })
        {
            Target(observer, subject);
            foreach (var mount in mounts) Assert.True(observer.TrySelectMount(mount), "each cycle mount can be selected");
        }

        for (var i = 0; i < honestMounts.Length; i++)
        {
            var onHonest = FireControl.Silhouette(honest, honest.HullData, null, float2(0, 1), 1f, null, honestMounts[i]).AimPoint;
            var onLiar = FireControl.Silhouette(liar, liar.HullData, null, float2(0, 1), 1f, null, liarMounts[i]).AimPoint;
            Assert.Equal(onHonest.x, onLiar.x, 4);
            Assert.Equal(onHonest.y, onLiar.y, 4);
        }
        var reactorAim = FireControl.Silhouette(liar, liar.HullData, null, float2(0, 1), 1f, null, liarMounts[0]).AimPoint;
        Assert.Equal(3.5f, reactorAim.x, 4);
        Assert.Equal(3.5f, reactorAim.y, 4);
    }

    // ---- TheIdentificationGateReadsOnlyObserverState ----

    // Whether the cycle holds any mount is the observer's identification of the ship (its info against
    // TargetGearInfoThreshold), the same for a liar and an honest ship and with the claim set or not: just below the threshold
    // the cycle holds no mount, at it, all of the hull the observer believes in.
    [Fact]
    public void TheIdentificationGateReadsOnlyObserverState()
    {
        var honest = AddBay(float3(0, 0, 100));
        var (liar, _, _) = AddWing(float3(0, 0, -100));
        var observer = Add(float3(0, 0, 0));

        foreach (var claimed in new[] { false, true })
        {
            liar.Claim(claimed ? honest.HullData : null);
            var believed = claimed ? 3 : 2;
            foreach (var (subject, count) in new[] { (honest, 3), (liar, believed) })
            {
                observer.EntityInfoGathered[subject] = Identify - .01f;
                Assert.Empty(MountsOf(observer.AimCycle(subject)));
                observer.EntityInfoGathered[subject] = Identify;
                Assert.Equal(count, MountsOf(observer.AimCycle(subject)).Length);
            }
        }
    }

    // ---- AMountSeenDestroyedLeavesTheCycle ----

    // The reactor is destroyed while the observer is below identification: it sees nothing, and its mount stays in the cycle
    // when the identification is later reached but not yet sensed. Sensed at identification, the mount leaves and an aim at
    // it resolves to none; it stays out when the info falls back below and recovers (the record is the observer's, not a
    // live read), and the other mounts stay.
    [Fact]
    public void AMountSeenDestroyedLeavesTheCycle()
    {
        var (bay, observer, reactor) = Identified(Identify - .01f);
        var mounts = bay.HullData.Hardpoints;
        var sense = SenseOf(observer);
        Target(observer, bay);

        reactor.EquippableItem.Durability = 0f;
        Assert.True(reactor.EquippableItem.IsDestroyed, "fixture");
        sense();

        observer.EntityInfoGathered[bay] = Identify;
        Assert.True(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "destroyed below identification: the mount stays");
        Assert.True(observer.TrySelectMount(reactor.Hardpoint), "fixture: aimed at it");

        sense();
        Assert.True(SameMounts(new[] { mounts[1], mounts[2] }, MountsOf(observer.AimCycle(bay))), "sensed at identification: the reactor mount leaves, the others stay");
        Assert.True(observer.ResolvedAim.Mount == null, "an aim at it resolves to none");
        Assert.False(observer.TrySelectMount(reactor.Hardpoint));

        observer.EntityInfoGathered[bay] = Identify - .01f;
        Assert.Empty(MountsOf(observer.AimCycle(bay)));
        observer.EntityInfoGathered[bay] = Identify;
        Assert.True(SameMounts(new[] { mounts[1], mounts[2] }, MountsOf(observer.AimCycle(bay))), "it stays out after the identification recovers");
        Assert.False(observer.TrySelectMount(reactor.Hardpoint));
    }

    // ---- ADestroyedItemKnownOnlyByItsActLeavesItsMountUntilPerceived ----

    // The reactor acted in the observer's sight (it is in the book) and is then destroyed while the observer does not resolve the
    // ship: the book is no perception of the destruction. Its mount is still in the cycle when the identification is later
    // reached, until a Sense at identification perceives it.
    [Fact]
    public void ADestroyedItemKnownOnlyByItsActLeavesItsMountUntilPerceived()
    {
        var (bay, observer, reactor) = Identified(Identify - .01f);
        observer.RecordReveal(bay, reactor, SignatureChannel.LaunchFlash, reactor.Data);
        Assert.True(FireControl.IsRevealed(observer, reactor), "fixture: the reactor is in the book");
        var sense = SenseOf(observer);

        reactor.EquippableItem.Durability = 0f;
        sense();
        observer.EntityInfoGathered[bay] = Identify;
        Assert.True(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "not perceived: the mount stays");

        sense();
        Assert.False(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "perceived at identification: the mount leaves");
    }

    // ---- TheSeenDestroyedRecordGoesWithTheSubject ----

    // The record is the observer's memory of one subject: when that subject has left the zone and the observer holds no book
    // of it (a quiet ship, never seen to act), the record is dropped at the observer's next Sense, not held for the sitting.
    [Theory]
    [InlineData("remove")]
    [InlineData("warp")]
    public void TheSeenDestroyedRecordGoesWithTheSubject(string how)
    {
        var (bay, observer, reactor) = Identified();
        var sense = SenseOf(observer);
        reactor.EquippableItem.Durability = 0f;
        sense();
        Assert.False(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "fixture: recorded while the ship is here");
        Assert.Empty(observer.RevealsOf(bay));

        Leave(_zone, bay, how);
        sense();
        observer.EntityInfoGathered[bay] = 1f;
        Assert.True(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "the record went with the ship");
    }

    // ---- ADestroyedClaimedMountIsNotSkippedUnderALie ----

    // The known gap (follow-up perceived-destruction-under-hull-lie): perceiving a destruction reads the true item, and the
    // record keys the true item's mount, so under a hull lie no claimed mount is recorded. The Wing's destroyed guns leave
    // all three claimed Bay mounts in the cycle. This pins the gap as it stands, so closing it (aetheria-ew) edits this test.
    [Fact]
    public void ADestroyedClaimedMountIsNotSkippedUnderALie()
    {
        var honest = AddBay(float3(0, 0, 100));
        var (liar, gunA, gunB) = AddWing(float3(0, 0, -100));
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[liar] = 1f;
        liar.Claim(honest.HullData);

        gunA.EquippableItem.Durability = 0f;
        gunB.EquippableItem.Durability = 0f;
        SenseOf(observer)();

        Assert.True(SameMounts(honest.HullData.Hardpoints.ToArray(), MountsOf(observer.AimCycle(liar))), "no claimed mount is recorded destroyed");
    }

    // ---- TheRecordIsPerSubject ----

    // Two ships of one hull share their HardpointData. The reactor seen destroyed on one Bay leaves that Bay's cycle and no
    // other: the observer's memory is of the ship it saw, not of the mount.
    [Fact]
    public void TheRecordIsPerSubject()
    {
        var (bayA, observer, reactorA) = Identified();
        var bayB = AddBay(float3(100, 0, 100));
        observer.EntityInfoGathered[bayB] = 1f;
        var reactorB = Fitted(bayB, "BayReactor");
        Assert.True(ReferenceEquals(reactorA.Hardpoint, reactorB.Hardpoint), "fixture: the two Bays share their mount objects");

        reactorA.EquippableItem.Durability = 0f;
        SenseOf(observer)();

        Assert.False(MountsOf(observer.AimCycle(bayA)).Any(m => ReferenceEquals(m, reactorA.Hardpoint)), "fixture: seen destroyed on A");
        Assert.True(MountsOf(observer.AimCycle(bayB)).Any(m => ReferenceEquals(m, reactorB.Hardpoint)), "B's reactor mount stays in B's cycle");
        Target(observer, bayB);
        Assert.True(observer.TrySelectMount(reactorB.Hardpoint), "and can be aimed at on B");
    }

    // ---- DeactivateClearsTheRecord ----

    // A ship that warps out forgets what it saw: its fresh book no longer supports the record, so the mount seen destroyed
    // is back in the cycle.
    [Fact]
    public void DeactivateClearsTheRecord()
    {
        var (bay, observer, reactor) = Identified();
        reactor.EquippableItem.Durability = 0f;
        SenseOf(observer)();
        Assert.False(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "fixture: recorded");

        observer.Deactivate();
        observer.EntityInfoGathered[bay] = 1f;
        Assert.True(MountsOf(observer.AimCycle(bay)).Any(m => ReferenceEquals(m, reactor.Hardpoint)), "the record went with the observer's memory");
    }
}
