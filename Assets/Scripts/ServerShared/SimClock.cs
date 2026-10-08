/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

// The one relation between simulation time and real time. Real seconds go in; whole fixed steps come out, each
// handed to the caller's step action with the same Step. Nothing else in the game converts frame time to sim time,
// and nothing integrates a sim quantity on this clock: presenters read Lead to draw ahead of the latest step
// (DrawAhead) and decide no sim fact.
public sealed class SimClock
{
    // The longest real time one frame may feed. A hitch (a load, a breakpoint) is a slow frame, not a burst of
    // catch-up steps.
    public const float MaxFrameSeconds = .1f;

    private readonly double _period;
    private double _accumulator;

    public SimClock(float step, float stepsPerRealSecond = 60)
    {
        if (!(step > 0)) throw new ArgumentOutOfRangeException(nameof(step));
        if (!(stepsPerRealSecond > 0)) throw new ArgumentOutOfRangeException(nameof(stepsPerRealSecond));
        Step = step;
        StepsPerRealSecond = stepsPerRealSecond;
        _period = 1.0 / stepsPerRealSecond;
    }

    // Sim seconds per step, fixed for the clock's life.
    public float Step { get; }

    public float StepsPerRealSecond { get; }

    public long Steps { get; private set; }

    // Sim seconds since the last step, 0 <= Lead < Step.
    public float Lead => (float) (_accumulator * StepsPerRealSecond * Step);

    // Feeds real seconds (negative counts as none, one frame counts at most MaxFrameSeconds) and runs the whole
    // steps they pay for. Returns how many ran.
    public int Advance(float realSeconds, Action<float> step)
    {
        _accumulator += Math.Min(Math.Max(realSeconds, 0f), MaxFrameSeconds);
        var ran = 0;
        while (_accumulator >= _period)
        {
            _accumulator -= _period;
            step(Step);
            Steps++;
            ran++;
        }
        return ran;
    }
}
