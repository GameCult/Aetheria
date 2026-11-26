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
}
