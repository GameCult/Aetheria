/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Controls 2 (rulings fire-on-solutions, controls-solution-subject, controls-solution-trigger): FireControl.Solution is
// each gun's one solution against the designated target, and ArcPermitsFire is the one trigger gate over it. The
// fixture is FireControlCut124Tests': a forward mount at the origin facing +z, the designated target 100 ahead.
public sealed partial class FireControlCut124Tests
{
    private const float SolutionArc = 120f;

    private static GunSolution SolutionOf(Engagement e) => FireControl.Solution(e.Weapon, e.Shooter, e.Shooter.Target.Value);

    // A designated target in the arc is fired on whatever the aim says; the round goes to its intercept, not the aim.
    // Kills an aim-only gate (the astern aim holds) and a direction that ignores the subject.
    [Fact]
    public void ASolutionFiresWithoutTheAim()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc);
        Aim(e, float2(0, -1));

        var solution = SolutionOf(e);

        Assert.True(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
        Assert.True(solution.Bears);
        Assert.False(solution.Free);
        Assert.Same(e.Target, solution.Subject);
        Near(FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target), solution.Direction);
        Near(float2(0, 1), solution.Direction);
    }

    [Fact]
    public void FreeFireNeedsTheAimInArc()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc);
        e.Shooter.SetTarget(TargetRef.None);

        Aim(e, float2(0, 1));
        Assert.True(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
        var free = SolutionOf(e);
        Assert.True(free.Free);
        Assert.False(free.Bears);
        Near(float2(0, 1), free.Direction);

        Aim(e, float2(0, -1));
        Assert.False(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
        Assert.False(SolutionOf(e).Free);
    }

    // A designated target 90 degrees off a 120 degree forward mount gives no solution. With the aim astern the gun
    // holds; with the aim ahead it fires free along the aim, and the price of that shot is zero.
    [Fact]
    public void ATargetOutOfArcGivesNoSolution()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc, weaponRange: 200f);
        e.Target.Position = e.Shooter.Position + float3(100, 0, 0);

        Aim(e, float2(0, -1));
        Assert.False(SolutionOf(e).Bears);
        Assert.False(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));

        Aim(e, float2(0, 1));
        var solution = SolutionOf(e);
        Assert.False(solution.Bears);
        Assert.True(solution.Free);
        Assert.True(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
        Near(float2(0, 1), solution.Direction);
        Assert.Same(e.Target, solution.Subject);
        Assert.Equal(0f, FireControl.HitProbability(e.Weapon, e.Shooter, e.Target));
    }

    [Fact]
    public void AFusedWeaponIgnoresTheGate()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 4f, weaponRange: NoLockRange, hardpointArc: SolutionArc);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, -1));

        var solution = SolutionOf(e);

        Assert.False(solution.Bears);
        Assert.False(solution.Free);
        Assert.True(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
    }

    // HitProbability above zero implies Bears and no Bears means no price, over bearings, ranges and visibility around
    // a weapon of range 100: the price's gate and the solution's are the one Bears. The grid is checked to price
    // something, so a gate that closed everything could not pass it.
    [Fact]
    public void BearsIsThePricesGate()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc, weaponRange: 100f);
        var priced = 0;
        foreach (var degrees in new[] { 0f, 30f, 59f, 61f, 90f, 135f, 180f })
        foreach (var range in new[] { 20f, 99f, 101f, 150f })
        foreach (var visible in new[] { true, false })
        {
            var at = radians(degrees);
            e.Target.Position = e.Shooter.Position + float3(sin(at), 0, cos(at)) * range;
            if (visible && !e.Shooter.VisibleEntities.Contains(e.Target)) e.Shooter.VisibleEntities.Add(e.Target);
            if (!visible) e.Shooter.VisibleEntities.Remove(e.Target);

            var bears = FireControl.Bears(e.Weapon, e.Shooter, e.Target, out _);
            var price = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);

            Assert.Equal(bears, SolutionOf(e).Bears);
            if (price > 0f) Assert.True(bears, $"priced {price} at {degrees} degrees, {range} out, visible {visible}, without bearing");
            if (!bears) Assert.Equal(0f, price);
            if (price > 0f) priced++;
        }
        Assert.True(priced > 0, "fixture: nothing on the grid was priced");
    }

    // Each branch of what a round does flies along Solution.Direction as it stood at fire time: a target that bears
    // (aim ignored), an unfused round at a designated target out of arc (the clamped aim), a fused round with no lock.
    [Fact]
    public void SolveFliesAlongTheSolution()
    {
        var bearing = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc);
        Aim(bearing, float2(0, -1));
        Assert.True(SolutionOf(bearing).Bears);
        AssertFlies(bearing);

        var outOfArc = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc, weaponRange: 200f);
        outOfArc.Target.Position = outOfArc.Shooter.Position + float3(100, 0, 0);
        Aim(outOfArc, float2(1, 4));
        Assert.False(SolutionOf(outOfArc).Bears);
        AssertFlies(outOfArc);

        var fused = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 4f, weaponRange: NoLockRange, hardpointArc: SolutionArc);
        fused.Shooter.SetTarget(TargetRef.None);
        Aim(fused, float2(0, -1));
        AssertFlies(fused);
    }

    private static void AssertFlies(Engagement e)
    {
        var direction = SolutionOf(e).Direction;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        Near(direction, SafeAssert.OnlyShot(e.Zone).TravelDirection);
    }

    [Fact]
    public void AFullArcTracksTheAimAllRound()
    {
        var settings = TestSettings();
        settings.FiringArc = 360;
        var e = Build(settings, SolidShape(5, 4));
        e.Shooter.SetTarget(TargetRef.None);
        for (var degrees = 0; degrees < 360; degrees += 30)
        {
            var aim = float2(sin(radians(degrees)), cos(radians(degrees)));
            Aim(e, aim);
            Assert.True(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
            Near(aim, SolutionOf(e).Direction);
        }
    }

    // An agent's aim is its intercept (Combat sets it), so an in-arc target is a solution and the free aim both: its
    // fire is the same with or without the aim.
    [Fact]
    public void TheAgentStillFiresAtItsIntercept()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc);
        var intercept = FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target);
        Aim(e, intercept);

        var solution = SolutionOf(e);

        Assert.True(solution.Bears && solution.Free);
        Assert.True(FireControl.ArcPermitsFire(e.Weapon, e.Shooter));
        Assert.True(FireControl.AgentFires(e.Weapon, e.Shooter, e.Target));
        Near(intercept, solution.Direction);
    }

    // Solve flies along the solution against the target it is given, not the shooter's designated one (the HUD forecast
    // and an agent's burst ask about other entities). A decoy 40 degrees off, in the same zone, is passed while the
    // designated target sits dead ahead; the round goes to the decoy's intercept. Kills a Solve that reads the shooter's
    // target, a constant, or a swapped argument; the fixture checks the two intercepts differ.
    [Fact]
    public void SolveFliesToTheTargetItIsGiven()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), hardpointArc: SolutionArc, weaponRange: 200f);
        var hull = e.Items.ItemData.RefOf<ItemData>(e.HullData);
        var decoy = new Ship(e.Items, e.Zone, new EquippableItem { Data = hull, Durability = 1000000, Lot = 400 }, new EntitySettings());
        e.Zone.Entities.Add(decoy);
        decoy.Activate();
        var at = radians(40f);
        decoy.Position = e.Shooter.Position + float3(sin(at), 0, cos(at)) * 100f;
        e.Shooter.VisibleEntities.Add(decoy);
        e.Shooter.EntityInfoGathered[decoy] = 1f;
        e.Shooter.SetIff(decoy, true);
        Aim(e, float2(0, -1));

        var designated = FireControl.Solution(e.Weapon, e.Shooter, e.Target).Direction;
        var other = FireControl.Solution(e.Weapon, e.Shooter, decoy).Direction;
        Assert.True(FireControl.Bears(e.Weapon, e.Shooter, decoy, out _));
        Assert.True(length(designated - other) > .3f, "fixture: the two intercepts must differ");
        Assert.Same(e.Target, e.Shooter.Target.Value.Entity);

        Near(other, FireControl.Solve(e.Weapon, e.WeaponItem, e.Shooter, decoy).TravelDirection);
        Near(designated, FireControl.Solve(e.Weapon, e.WeaponItem, e.Shooter, e.Target).TravelDirection);
    }
}
