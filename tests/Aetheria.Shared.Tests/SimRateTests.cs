using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CultMath;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Cut sim-rate (ruling lower-game-sim-rate, map docs/aetheria-arena-sim-rate-map.md): the game's tick has one owner,
// and content authored in seconds means the same at every tick, because a restarted timer keeps what the last step
// overshot and passive sensing decays exactly.
public sealed class SimRateClockTests
{
    [Fact]
    public void OneTickOwner()
    {
        var clock = SimClock.ForGame();
        Assert.Equal(1f / SimClock.TickRate, SimClock.GameStep);
        Assert.Equal(SimClock.GameStep, clock.Step);
        Assert.Equal(SimClock.TickRate, clock.StepsPerRealSecond);

        // Ten real seconds of irregular frames pay ten seconds of steps, so the game runs at real time.
        var frames = new[] { .004f, .031f, .016f, .0083f, .05f, .022f, .0125f, .027f };
        var real = 0f;
        for (var i = 0; real < 10f; i++)
        {
            var frame = Math.Min(frames[i % frames.Length], 10f - real);
            real += frame;
            clock.Advance(frame, _ => { });
        }
        Assert.InRange(clock.Steps, 10 * SimClock.TickRate - 1, 10 * SimClock.TickRate + 1);
    }
}
