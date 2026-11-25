/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Xunit;

// TacticalView owns the flight camera's zoom; blend and time rate are derived from the distance, never stored.
public sealed class TacticalViewTests
{
    private const float Radius = 20000, Fit = 2, Start = 250, End = 1000, Scale = .01f, BaseDistance = 30;

    private static TacticalView Make()
    {
        var view = new TacticalView(1.25f, Start, End, Scale, Fit);
        view.Reset(Radius);
        return view;
    }

    [Fact]
    public void ZoomIsClampedToFlightAndTheWholeZone()
    {
        var view = Make();
        view.Step(-5, BaseDistance);
        Assert.Equal(1f, view.Zoom);
        Assert.Equal(BaseDistance, view.Distance(BaseDistance));
        for (var i = 0; i < 200; i++) view.Step(1, BaseDistance);
        Assert.Equal(Radius * Fit, view.Distance(BaseDistance));
        Assert.True(view.Zoom <= Radius * Fit / BaseDistance + 1e-3f);
        view.Step(1, BaseDistance);
        Assert.Equal(Radius * Fit, view.Distance(BaseDistance));
        view.Step(-2, BaseDistance);
        Assert.True(view.Distance(BaseDistance) < Radius * Fit);
        view.Reset(Radius);
        Assert.Equal(BaseDistance, view.Distance(BaseDistance));
        // A hull framed wider than the zone's fit still never sits closer than its own framing.
        Assert.Equal(Radius * Fit * 2, view.Distance(Radius * Fit * 2));
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
