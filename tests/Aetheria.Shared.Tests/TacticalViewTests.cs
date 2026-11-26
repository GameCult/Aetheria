/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using CultMath;
using Xunit;

// TacticalView owns the view: one eased log-distance axis with a detent between flight and tactical. Blend and time rate
// are derived from the eased distance, never stored. The constants are the placeholders GameSettings holds for the operator.
public sealed class TacticalViewTests
{
    private const float Radius = 20000, Fit = 2, Start = 250, End = 1000, Scale = .01f, BaseDistance = 30;
    private const float Step = 1.25f, Resistance = .6f, Relax = .4f, Ease = .15f;
    private static readonly float LnStep = MathF.Log(Step);

    private static TacticalView Make(float radius = Radius)
    {
        var view = new TacticalView(Step, Start, End, Scale, Fit, Resistance, Relax, Ease);
        view.Reset(radius, BaseDistance, new float2(100, -50));
        return view;
    }

    // Notches at a rate: the real time between them is ticked first, so a held scroll feeds the relaxation too.
    private static void Flick(TacticalView view, int notches, float perSecond, float direction = 1)
    {
        for (var i = 0; i < notches; i++)
        {
            view.Tick(1 / perSecond);
            view.Scroll(direction);
        }
    }

    // A fast scroll, 20 notches a second, until the view changes; the notches it took.
    private static int Cross(TacticalView view, float direction = 1)
    {
        var was = view.Tactical;
        var notches = 0;
        while (view.Tactical == was && notches < 30)
        {
            view.Tick(.05f);
            view.Scroll(direction);
            notches++;
        }
        return notches;
    }

    private static void Settle(TacticalView view)
    {
        for (var i = 0; i < 200; i++) view.Tick(.02f);
    }

    // The flight's farthest edge: ten notches carry it just into the band, and the band relaxes it to the edge.
    private static void ToFlightEdge(TacticalView view)
    {
        view.Scroll(10);
        Settle(view);
        Assert.Equal(MathF.Log(Start), view.Commanded, 3);
    }

    [Fact]
    public void ScrollMovesFreelyInsideEachView()
    {
        var view = Make();
        var lnBase = MathF.Log(BaseDistance);
        view.Scroll(-5);
        Assert.Equal(lnBase, view.Commanded, 5);
        view.Scroll(3);
        Assert.Equal(lnBase + 3 * LnStep, view.Commanded, 4);
        view.Scroll(-1);
        Assert.Equal(lnBase + 2 * LnStep, view.Commanded, 4);
        Assert.False(view.Tactical);

        ToFlightEdge(view);
        Assert.InRange(Cross(view), 1, 12);
        Assert.True(view.Tactical);
        Assert.Equal(MathF.Log(End), view.Commanded, 4);
        view.Scroll(2);
        Assert.Equal(MathF.Log(End) + 2 * LnStep, view.Commanded, 4);
        view.Scroll(-1);
        Assert.Equal(MathF.Log(End) + LnStep, view.Commanded, 4);
        view.Scroll(500);
        Assert.Equal(MathF.Log(Radius * Fit), view.Commanded, 4);
        // A zone smaller than the band's far edge still reaches that edge and no farther.
        var tiny = Make(10);
        ToFlightEdge(tiny);
        Cross(tiny);
        tiny.Scroll(100);
        Assert.Equal(MathF.Log(End), tiny.Commanded, 4);
    }

    [Fact]
    public void DetentHoldsASlowScroll()
    {
        var view = Make();
        ToFlightEdge(view);
        Flick(view, 40, 5);
        Assert.False(view.Tactical);
        Assert.True(view.Commanded > MathF.Log(Start));
        Settle(view);
        Assert.Equal(MathF.Log(Start), view.Commanded, 3);
        Assert.False(view.Tactical);
    }

    [Fact]
    public void DetentYieldsToAFastScroll()
    {
        var view = Make();
        ToFlightEdge(view);
        var notches = 0;
        while (!view.Tactical && notches < 10)
        {
            view.Tick(.05f);
            view.Scroll(1);
            notches++;
        }
        Assert.True(view.Tactical);
        Assert.Equal(MathF.Log(End), view.Commanded, 5);
    }

    [Fact]
    public void DetentIsSymmetric()
    {
        var view = Make();
        ToFlightEdge(view);
        Assert.InRange(Cross(view), 1, 12);
        Assert.True(view.Tactical);
        Settle(view);
        Flick(view, 40, 5, -1);
        Assert.True(view.Tactical);
        Settle(view);
        Assert.Equal(MathF.Log(End), view.Commanded, 3);
        Assert.InRange(Cross(view, -1), 1, 12);
        Assert.False(view.Tactical);
        Assert.Equal(MathF.Log(Start), view.Commanded, 5);
    }

    [Fact]
    public void TickNeverFlipsTactical()
    {
        // The commanded position sampled across the whole band, from both views, then ticked every which way.
        foreach (var fromTactical in new[] { false, true })
        foreach (var notches in new[] { 1, 2, 3, 4, 5, 6 })
        foreach (var dt in new[] { .001f, .05f, .4f, 3f, 100f })
        {
            var view = Make();
            ToFlightEdge(view);
            if (fromTactical) Assert.InRange(Cross(view), 1, 12);
            view.Scroll(fromTactical ? -notches : notches);
            var was = view.Tactical;
            for (var i = 0; i < 5; i++)
            {
                view.Tick(dt);
                Assert.Equal(was, view.Tactical);
            }
        }
    }

    [Fact]
    public void BlendFollowsTheEasedDistance()
    {
        var view = Make();
        ToFlightEdge(view);
        var before = view.Distance;
        Cross(view);
        Assert.True(view.Tactical);
        // The committing scroll moved the commanded position, not the camera: the eased distance is still near flight's edge.
        Assert.True(view.Distance < End);
        var last = view.Distance;
        var lastBlend = view.Blend(last);
        Assert.True(last >= before - 1e-3f);
        for (var i = 0; i < 600; i++)
        {
            view.Tick(.005f);
            Assert.True(view.Distance >= last - 1e-3f);
            Assert.True(view.Distance <= End * 1.0001f);
            Assert.True(view.Blend(view.Distance) >= lastBlend - 1e-6f);
            last = view.Distance;
            lastBlend = view.Blend(last);
        }
        Assert.Equal(End, view.Distance, 0);
        Assert.Equal(1f, view.Blend(view.Distance), 2);
    }

    [Fact]
    public void RightAfterACommitDistanceAndBlendAreUnchanged()
    {
        var view = Make();
        ToFlightEdge(view);
        var distance = view.Distance;
        var blend = view.Blend(distance);
        for (var i = 0; i < 8; i++) view.Scroll(1);
        Assert.True(view.Tactical);
        Assert.Equal(distance, view.Distance, 3);
        Assert.Equal(blend, view.Blend(view.Distance), 5);
    }

    [Fact]
    public void TargetTracksTheShipOnlyInFlight()
    {
        var view = Make();
        Assert.Equal(new float2(100, -50), view.Target);
        view.Pan(new float2(5, 5));
        Assert.Equal(new float2(100, -50), view.Target);
        view.Track(new float2(7, 8));
        Assert.Equal(new float2(7, 8), view.Target);
        ToFlightEdge(view);
        Cross(view);
        Assert.True(view.Tactical);
        Settle(view);
        view.Track(new float2(-900, 900));
        Assert.Equal(new float2(7, 8), view.Target);
        view.Pan(new float2(10, -4));
        Assert.Equal(new float2(17, 4), view.Target);
        view.Pan(new float2(1e6f, 0));
        Assert.Equal(Radius, MathF.Sqrt(view.Target.x * view.Target.x + view.Target.y * view.Target.y), 1);
        view.Reset(Radius, BaseDistance, new float2(3, 4));
        Assert.Equal(new float2(3, 4), view.Target);
        Assert.False(view.Tactical);
    }

    [Fact]
    public void BlendRisesMonotonicallyAcrossTheBand()
    {
        var view = Make();
        Assert.Equal(0f, view.Blend(BaseDistance));
        Assert.Equal(0f, view.Blend(Start));
        Assert.Equal(1f, view.Blend(End));
        Assert.Equal(1f, view.Blend(Radius * Fit));
        var last = 0f;
        for (var distance = Start; distance <= End; distance += 5)
        {
            var blend = view.Blend(distance);
            Assert.InRange(blend, last, 1f);
            last = blend;
        }
        Assert.InRange(view.Blend(Start * 1.01f), 0f, .05f);
        Assert.InRange(view.Blend(End / 1.01f), .95f, 1f);
    }

    // The band is smoothstep over the logarithm of distance: a quarter, half and three quarters of the way up in ln
    // are 250 x 4^t m, and the thresholds themselves are the 250 m and 1000 m the operator tunes by eye (GameSettings).
    [Fact]
    public void BlendIsSmoothstepOverLogDistanceBetweenTheThresholds()
    {
        var view = Make();
        Assert.Equal(.15625f, view.Blend(250f * MathF.Pow(4, .25f)), 3);
        Assert.Equal(.5f, view.Blend(500f), 3);
        Assert.Equal(.84375f, view.Blend(250f * MathF.Pow(4, .75f)), 3);
        Assert.Equal(0f, view.Blend(249f));
        Assert.Equal(1f, view.Blend(1001f));
    }

    [Fact]
    public void TimeRateIsTheScaleToTheBlendPowerNotLinear()
    {
        var view = Make();
        Assert.Equal(.1f, view.TimeRate(.5f), 5);
        Assert.Equal(MathF.Pow(.01f, .25f), view.TimeRate(.25f), 5);
        Assert.Equal(MathF.Pow(.01f, .75f), view.TimeRate(.75f), 5);
        Assert.Equal(1f, view.TimeRate(-1));
        Assert.Equal(Scale, view.TimeRate(2), 6);
    }

    [Fact]
    public void TimeRateIsOneInFlightAndTheScaleInTactical()
    {
        var view = Make();
        Assert.Equal(1f, view.TimeRate(0));
        Assert.Equal(Scale, view.TimeRate(1), 6);
        var last = 1f;
        for (var blend = 0f; blend <= 1f; blend += .05f)
        {
            var rate = view.TimeRate(blend);
            Assert.InRange(rate, Scale - 1e-6f, last);
            last = rate;
        }
        // Flying never sees it: every distance at or under the near edge runs at full speed.
        Assert.Equal(1f, view.TimeRate(view.Blend(Start)));
    }

    // The committed notch count of a held scroll at a rate, ticked between notches; 0 if it never crosses in 200.
    private static int HeldCrossing(float perSecond)
    {
        var view = Make();
        ToFlightEdge(view);
        for (var i = 1; i <= 200; i++)
        {
            view.Tick(1 / perSecond);
            view.Scroll(1);
            if (view.Tactical) return i;
        }
        return 0;
    }

    [Fact]
    public void TenNotchesASecondHoldsAndFifteenCrosses()
    {
        Assert.Equal(0, HeldCrossing(10));
        Assert.InRange(HeldCrossing(15), 1, 12);
        // The ruled claim sits clear of the boundary on both sides, so a softened or stiffened detent is red.
        Assert.Equal(0, HeldCrossing(8));
        Assert.InRange(HeldCrossing(20), 1, 10);
    }

    [Fact]
    public void ACrestAtTheBandsMidpointSplitsATwoNotchPushBothWays()
    {
        // One notch into the band stays in flight; the commit lands only past the crest, not after a fixed few notches.
        var view = Make();
        ToFlightEdge(view);
        view.Scroll(1);
        Assert.False(view.Tactical);
        Assert.True(view.Commanded > MathF.Log(Start));
        Assert.True(view.Commanded < (MathF.Log(Start) + MathF.Log(End)) / 2);
        Assert.Equal(MathF.Log(Start) + LnStep * Resistance, view.Commanded, 3);
    }

    // Eased from rest, critically damped: ln d(t) = c - offset (1 + wt) e^(-wt), w = 2 / EaseTime.
    [Fact]
    public void TheEaseIsACriticallyDampedSpringNotASnap()
    {
        var view = Make();
        var lnBase = MathF.Log(BaseDistance);
        view.Scroll(2);
        Assert.Equal(BaseDistance, view.Distance, 4);
        view.Tick(.05f);
        var w = 2 / Ease;
        var expected = lnBase + 2 * LnStep * (1 - (1 + w * .05f) * MathF.Exp(-w * .05f));
        Assert.Equal(MathF.Exp(expected), view.Distance, 3);
        Assert.True(view.Distance < BaseDistance * 1.25f * 1.25f);
        // The same step in two halves lands at the same place: the solution is exact at any dt.
        var halves = Make();
        halves.Scroll(2);
        halves.Tick(.025f);
        halves.Tick(.025f);
        Assert.Equal(view.Distance, halves.Distance, 3);
    }

    [Fact]
    public void ResetClearsTheEasedPositionAndTheVelocity()
    {
        var view = Make();
        view.Scroll(4);
        view.Tick(.05f);
        Assert.NotEqual(BaseDistance, view.Distance);
        view.Reset(Radius, BaseDistance, new float2(0, 0));
        Assert.Equal(BaseDistance, view.Distance, 4);
        Assert.Equal(MathF.Log(BaseDistance), view.Commanded, 5);
        view.Tick(.1f);
        Assert.Equal(BaseDistance, view.Distance, 4);
    }

    [Fact]
    public void ANonPositiveTickChangesNothing()
    {
        var view = Make();
        ToFlightEdge(view);
        view.Scroll(1);
        view.Tick(.02f);
        var commanded = view.Commanded;
        var distance = view.Distance;
        view.Tick(0);
        view.Tick(-1);
        Assert.Equal(commanded, view.Commanded, 6);
        Assert.Equal(distance, view.Distance, 4);
    }

    // The ruling: everything follows the eased distance. Flicking back from a panned tactical view must not move the
    // camera's target by the pan offset in one frame; it returns to the ship with the rig's weight.
    [Fact]
    public void TargetReturnsToTheShipWithTheEasedCameraNotTheCommandedFlip()
    {
        var view = Make();
        var ship = new float2(100, -50);
        ToFlightEdge(view);
        Cross(view);
        Settle(view);
        view.Pan(new float2(12000, 0));
        view.Track(ship);
        var panned = view.Target;
        Assert.Equal(12100f, panned.x, 2);
        view.Track(ship);
        Assert.Equal(panned, view.Target);

        var last = view.Target;
        var worst = 0f;
        // A fast flick back at 20 notches a second, stepped at 5 ms frames, then settled: no frame moves it far.
        for (var i = 0; i < 40 && view.Tactical; i++)
        {
            for (var frame = 0; frame < 10; frame++)
            {
                view.Tick(.005f);
                view.Track(ship);
                worst = MathF.Max(worst, MathF.Abs(view.Target.x - last.x));
                last = view.Target;
            }
            view.Scroll(-1);
            worst = MathF.Max(worst, MathF.Abs(view.Target.x - last.x));
            last = view.Target;
        }
        Assert.False(view.Tactical);
        for (var i = 0; i < 600; i++)
        {
            view.Tick(.005f);
            view.Track(ship);
            var step = MathF.Abs(view.Target.x - last.x);
            worst = MathF.Max(worst, step);
            last = view.Target;
        }
        Assert.True(worst < 12000 * .1f, "target stepped " + worst);
        Assert.Equal(ship, view.Target);
    }

    [Fact]
    public void ThePanStartsFromTheShipOnEntry()
    {
        var view = Make();
        ToFlightEdge(view);
        view.Track(new float2(500, 500));
        Cross(view);
        Settle(view);
        Assert.Equal(new float2(500, 500), view.Target);
    }

    // Ruling tactical-input-hands-off: ActionGameManager takes keyboard flight, toggles, mouse look and fire only while Direct,
    // so the ship holds its last commands through the tactical view and takes input again on return.
    [Fact]
    public void DirectInputIsTakenInFlightPausesInTacticalAndReturnsWithTheFlightView()
    {
        var view = Make();
        Assert.True(view.Direct);
        Assert.False(view.Tactical);

        Cross(view);
        Assert.True(view.Tactical);
        Assert.False(view.Direct);
        for (var i = 0; i < 40; i++) { view.Tick(.05f); Assert.False(view.Direct); }

        Cross(view, -1);
        Assert.False(view.Tactical);
        Assert.True(view.Direct);

        Cross(view);
        view.Reset(Radius, BaseDistance, new float2(0, 0));
        Assert.True(view.Direct);
    }

    [Fact]
    public void DirectIsPausedExactlyWhereTacticalIsAtEveryNotchOfTheDetent()
    {
        var view = Make();
        for (var i = 0; i < 80; i++)
        {
            view.Scroll(i < 40 ? 1 : -1);
            view.Tick(.02f);
            Assert.Equal(!view.Tactical, view.Direct);
        }
    }
}
