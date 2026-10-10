using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;

namespace Aetheria.Shared.Tests;

// ThrustAllocator on synthetic columns. A column is (starboard m/s^2, forward m/s^2, clockwise yaw rad/s) per unit
// throttle; the fixtures are written in deg/s the way the probes measured them (map TA3, TA12).
public class ThrustAllocatorTests
{
    const float Deg = MathF.PI / 180f;

    // The drive carries a constant throttle of about 1.2e-4 (1.6e-4 on the halved hull) from the first touch of Turn, the
    // optimum of the ridge against the cost rows; recruitment past the onset is 1e-3 and more within .005 of Turn.
    const float LeakBound = 3e-4f;

    static float3 Col(float starboard, float forward, float yawDeg) => new(starboard, forward, yawDeg * Deg);

    // TA3, the Duel Longinus: two mismatched drives (0, 1) and two Talarias (2 clockwise, 3 counter-clockwise).
    static float3[] Duel() => new[]
    {
        Col(0, 53.94f, 49.24f), Col(0, 61.19f, -55.86f), Col(29.08f, 0, 166.26f), Col(-21.80f, 0, -124.63f),
    };

    // The clockwise Talaria lost.
    static float3[] DamagedDuel() => new[]
    {
        Col(0, 53.94f, 49.24f), Col(0, 61.19f, -55.86f), default, Col(-21.80f, 0, -124.63f),
    };

    // TA12's symmetric hull: mains, fore and aft laterals in torque-free pairs, a bow thruster.
    static float3[] Symmetric() => new[]
    {
        Col(0, 50, 20), Col(0, 50, -20), Col(20, 0, 80), Col(-20, 0, -80), Col(20, 0, -60), Col(-20, 0, 60), Col(0, -30, 0),
    };

    // The hull's half-axis extremes, from the definition: the sum of a row's positive entries, or of the negated
    // negative ones. Axis 0 starboard, 1 forward, 2 clockwise yaw.
    static (float plus, float minus) Extremes(float3[] columns, int axis)
    {
        float plus = 0, minus = 0;
        foreach (var c in columns)
        {
            var v = axis == 0 ? c.x : axis == 1 ? c.y : c.z;
            if (v > 0) plus += v; else minus -= v;
        }
        return (plus, minus);
    }

    static float3 Net(float3[] columns, float[] throttle)
    {
        float3 net = default;
        for (var i = 0; i < columns.Length; i++) net += columns[i] * throttle[i];
        return net;
    }

    static float[] Solve(ThrustAllocator allocator, float3[] columns, float moveX, float moveY, float turn)
    {
        var throttle = new float[columns.Length];
        allocator.Allocate(columns, new float2(moveX, moveY), turn, throttle);
        return throttle;
    }

    static float[] Solve(float3[] columns, float moveX, float moveY, float turn) => Solve(new ThrustAllocator(), columns, moveX, moveY, turn);

    static float YawDeg(float3[] columns, float[] throttle) => Net(columns, throttle).z / Deg;

    [Fact]
    public void AHoldIsExactWithMismatchedDrives()
    {
        var duel = Duel();
        var throttle = Solve(duel, 0, 1, 0);
        var clockwise = Extremes(duel, 2).plus;
        var net = Net(duel, throttle);
        Assert.True(MathF.Abs(net.z) <= 1e-5f * clockwise, $"yaw {net.z / Deg} deg/s");
        Assert.True(net.y >= .99f * Extremes(duel, 1).plus, $"forward {net.y}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ALostDriveIsBalancedByWhatIsLeft(int zeroed)
    {
        var columns = Duel();
        var extreme = Extremes(columns, 2).plus;
        columns[zeroed] = default;
        var throttle = Solve(columns, 0, 1, 0);
        var net = Net(columns, throttle);
        Assert.True(MathF.Abs(net.z) <= 1e-5f * extreme, $"yaw {net.z / Deg} deg/s");
        Assert.Equal(0f, throttle[zeroed]);
        Assert.True(net.y > 0, $"forward {net.y}");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(.5f)]
    [InlineData(-.5f)]
    [InlineData(-1f)]
    public void ATurnIsThatFractionOfTheBoxExtreme(float turn)
    {
        var duel = Duel();
        var (plus, minus) = Extremes(duel, 2);
        var expected = turn * (turn > 0 ? plus : minus);
        var net = Net(duel, Solve(duel, 0, 0, turn));
        Assert.True(MathF.Abs(net.z - expected) <= 1e-4f * MathF.Abs(expected), $"yaw {net.z / Deg} expected {expected / Deg}");
    }

    [Fact]
    public void AttitudeServesFirstAndTheOnsetIsTheColumnsRatio()
    {
        var duel = Duel();
        // The onset is the attitude thruster's yaw over the hull's extreme in that direction.
        var clockwiseOnset = duel[2].z / Extremes(duel, 2).plus;
        var counterOnset = -duel[3].z / Extremes(duel, 2).minus;
        Assert.InRange(clockwiseOnset, .7714f, .7716f);
        Assert.InRange(counterOnset, .6904f, .6906f);
        var allocator = new ThrustAllocator();
        for (var step = 0; step <= 1000; step++)
        {
            var turn = step * .001f;
            var drive = Solve(allocator, duel, 0, 0, turn)[0];
            if (turn <= clockwiseOnset - .005f) Assert.True(drive <= LeakBound, $"clockwise turn {turn}: drive {drive}");
            if (turn >= clockwiseOnset + .005f) Assert.True(drive > 1e-3f, $"clockwise turn {turn}: drive {drive}");
        }
        allocator = new ThrustAllocator();
        for (var step = 0; step <= 1000; step++)
        {
            var turn = step * .001f;
            var drive = Solve(allocator, duel, 0, 0, -turn)[1];
            if (turn <= counterOnset - .005f) Assert.True(drive <= LeakBound, $"counter-clockwise turn {turn}: drive {drive}");
            if (turn >= counterOnset + .005f) Assert.True(drive > 1e-3f, $"counter-clockwise turn {turn}: drive {drive}");
        }
    }

    [Fact]
    public void TheOnsetMovesWithTheColumns()
    {
        // Clockwise Talaria lost: the drive serves a clockwise turn from the first touch.
        var damaged = DamagedDuel();
        var small = Solve(damaged, 0, 0, .01f);
        Assert.True(small[0] > 0, $"drive {small[0]}");
        Assert.True(MathF.Abs(YawDeg(damaged, small) - .01f * 49.24f) <= .01f * 49.24f * 1e-3f, $"yaw {YawDeg(damaged, small)}");
        var full = Solve(damaged, 0, 0, 1f);
        Assert.True(MathF.Abs(YawDeg(damaged, full) - 49.24f) <= 49.24f * 1e-3f, $"yaw {YawDeg(damaged, full)}");

        // Clockwise Talaria halved: the onset is its new share of the clockwise extreme.
        var halved = Duel();
        halved[2] = halved[2] * .5f;
        var onset = halved[2].z / Extremes(halved, 2).plus;
        Assert.InRange(onset, .627f, .629f);
        var allocator = new ThrustAllocator();
        for (var step = 0; step <= 1000; step++)
        {
            var turn = step * .001f;
            var drive = Solve(allocator, halved, 0, 0, turn)[0];
            if (turn <= onset - .005f) Assert.True(drive <= LeakBound, $"turn {turn}: drive {drive}");
            if (turn >= onset + .005f) Assert.True(drive > 1e-3f, $"turn {turn}: drive {drive}");
        }
    }

    [Fact]
    public void EveryThrottleIsContinuousInTurn()
    {
        foreach (var columns in new[] { Duel(), DamagedDuel(), Symmetric() })
        {
            var allocator = new ThrustAllocator();
            var previous = Solve(allocator, columns, 0, 0, -1f);
            for (var step = -999; step <= 1000; step++)
            {
                var current = Solve(allocator, columns, 0, 0, step * .001f);
                for (var i = 0; i < columns.Length; i++)
                    Assert.True(MathF.Abs(current[i] - previous[i]) <= .02f, $"turn {step * .001f}: throttle {i} {previous[i]} -> {current[i]}");
                previous = current;
            }
        }
    }

    [Fact]
    public void PureTorqueNeedsNoTranslation()
    {
        var hull = Symmetric();
        var allocator = new ThrustAllocator();
        var starboard = Extremes(hull, 0).plus;
        var forward = Extremes(hull, 1).plus;
        for (var step = -87; step <= 87; step++)
        {
            var net = Net(hull, Solve(allocator, hull, 0, 0, step * .01f));
            Assert.True(MathF.Abs(net.x) <= 1e-3f * starboard, $"turn {step * .01f}: starboard {net.x}");
            Assert.True(MathF.Abs(net.y) <= 1e-3f * forward, $"turn {step * .01f}: forward {net.y}");
        }
        var strafe = Net(hull, Solve(hull, 1, 0, 0));
        Assert.True(strafe.x >= .9f * starboard, $"strafe {strafe.x} of {starboard}");
        Assert.True(MathF.Abs(strafe.z) <= 1e-3f * Extremes(hull, 2).plus, $"yaw {strafe.z / Deg}");
    }

    [Fact]
    public void TurnComesBeforeTranslation()
    {
        var duel = Duel();
        var throttle = Solve(duel, 0, 1, 1f);
        var net = Net(duel, throttle);
        var extreme = Extremes(duel, 2).plus;
        Assert.True(MathF.Abs(net.z - extreme) <= 1e-4f * extreme, $"yaw {net.z / Deg} of {extreme / Deg}");
        Assert.True(throttle[1] < .01f, $"counter-turning drive {throttle[1]}");
    }

    [Fact]
    public void MovementIsThatFractionOfTheHalfAxis()
    {
        // A torque-free strafe pair of unequal strength and a torque-free main.
        var columns = new[] { Col(30, 0, 0), Col(-20, 0, 0), Col(0, 60, 0) };
        const float yaw = 1e-4f;
        var right = Net(columns, Solve(columns, .5f, 0, 0));
        Assert.True(MathF.Abs(right.x - 15f) <= 15e-3f && MathF.Abs(right.z) <= yaw, $"right {right.x}");
        var left = Net(columns, Solve(columns, -.5f, 0, 0));
        Assert.True(MathF.Abs(left.x + 10f) <= 10e-3f && MathF.Abs(left.z) <= yaw, $"left {left.x}");
        var ahead = Net(columns, Solve(columns, 0, .5f, 0));
        Assert.True(MathF.Abs(ahead.y - 30f) <= 30e-3f && MathF.Abs(ahead.z) <= yaw, $"ahead {ahead.y}");
    }

    [Fact]
    public void IntentBeyondOneIsClamped()
    {
        var duel = Duel();
        Assert.Equal(Solve(duel, 0, 0, 1f), Solve(duel, 0, 0, 3f));
        Assert.Equal(Solve(duel, 1, -1, 0), Solve(duel, 3, -3, 0));
        Assert.Equal(Solve(duel, 0, 0, -1f), Solve(duel, 0, 0, -3f));
    }

    [Fact]
    public void NothingAskedNothingFired()
    {
        Assert.All(Solve(Duel(), 0, 0, 0), t => Assert.Equal(0f, t));
        Assert.All(Solve(new float3[4], 1, 1, 1), t => Assert.Equal(0f, t));
        new ThrustAllocator().Allocate(ReadOnlySpan<float3>.Empty, new float2(1, 1), 1, Span<float>.Empty);
        var withNaN = Duel();
        withNaN[2] = new float3(float.NaN, 0, 0);
        Assert.All(Solve(withNaN, 0, 1, 1), t => Assert.Equal(0f, t));
        var nanYaw = Duel();
        nanYaw[0] = new float3(0, 1, float.NaN);
        Assert.All(Solve(nanYaw, 0, 1, 0), t => Assert.Equal(0f, t));

        // A bad column zeroes the answer even after a good call left throttles in the warm start and in the caller's buffer.
        var allocator = new ThrustAllocator();
        var buffer = new float[4];
        allocator.Allocate(Duel(), new float2(0, 1), .5f, buffer);
        Assert.Contains(buffer, t => t > .1f);
        allocator.Allocate(withNaN, new float2(0, 1), .5f, buffer);
        Assert.All(buffer, t => Assert.Equal(0f, t));
    }

    [Fact]
    public void TheStartDoesNotChangeTheAnswer()
    {
        var duel = Duel();
        var cold = Solve(duel, .3f, .8f, .6f);
        foreach (var previous in new[] { (1f, 1f, 1f), (-1f, -.5f, -1f), (0f, 0f, 0f) })
        {
            var allocator = new ThrustAllocator();
            Solve(allocator, duel, previous.Item1, previous.Item2, previous.Item3);
            var warm = Solve(allocator, duel, .3f, .8f, .6f);
            for (var i = 0; i < duel.Length; i++) Assert.True(MathF.Abs(warm[i] - cold[i]) <= 1e-5f, $"previous {previous}: throttle {i} {cold[i]} vs {warm[i]}");
        }
    }

    [Fact]
    public void EqualThrustersShareTheDemand()
    {
        var columns = new[] { Col(0, 50, 0), Col(0, 50, 0) };
        var throttle = Solve(columns, 0, .5f, 0);
        Assert.True(MathF.Abs(throttle[0] - throttle[1]) <= 1e-4f, $"{throttle[0]} vs {throttle[1]}");
        Assert.True(throttle[0] > .1f);
    }

    [Fact]
    public void AColumnCountChangeResets()
    {
        var four = Duel();
        var three = new[] { four[0], four[2], four[3] };
        var allocator = new ThrustAllocator();
        var steps = new List<(float3[] columns, float x, float y, float turn)>
        {
            (four, .2f, .9f, -.7f), (three, -.4f, 1f, .9f), (four, .2f, .9f, -.7f),
        };
        foreach (var (columns, x, y, turn) in steps)
        {
            var warm = Solve(allocator, columns, x, y, turn);
            var fresh = Solve(columns, x, y, turn);
            for (var i = 0; i < columns.Length; i++) Assert.True(MathF.Abs(warm[i] - fresh[i]) <= 1e-5f, $"{columns.Length} columns: throttle {i}");
        }
    }

    [Fact]
    public void AShipWithOneThrusterIsFlownFromItsFirstCall()
    {
        var throttle = Solve(new[] { Col(0, 50, 0) }, 0, 1, 0);
        Assert.True(throttle[0] > .99f, $"{throttle[0]}");
    }

    [Fact]
    public void ChangedColumnsOfTheSameCountLeaveNothingBehind()
    {
        // Thruster 1 first strafes, then only pushes forward: the strafe row it left must not shape the second answer.
        var strafing = new[] { Col(0, 60, 0), Col(30, 0, 0), Col(-20, 0, 0) };
        var forwardOnly = new[] { Col(0, 60, 0), Col(0, 40, 0), Col(0, -20, 0) };
        var allocator = new ThrustAllocator();
        Solve(allocator, strafing, 1, 1, .5f);
        var warm = Solve(allocator, forwardOnly, 0, 1, 0);
        var fresh = Solve(forwardOnly, 0, 1, 0);
        for (var i = 0; i < warm.Length; i++) Assert.True(MathF.Abs(warm[i] - fresh[i]) <= 1e-5f, $"throttle {i}: {warm[i]} vs {fresh[i]}");
        Assert.True(warm[1] > .9f, $"{warm[1]}");
    }

    [Fact]
    public void AtTheIterationCapTheIterateIsKept()
    {
        // 128 torque-free forward thrusters from a cold start: the active set frees about one throttle per iteration, so
        // the solver stops at its iteration cap (measured: IterationLimit at 100). TA-R4 keeps the iterate it reached,
        // which already carries a share of the forward demand; a call that zeroed it would give nothing.
        var forward = new float3[128];
        for (var i = 0; i < forward.Length; i++) forward[i] = Col(0, 30f + i % 7, 0);
        var throttle = Solve(forward, 0, 1, 0);
        var reached = Net(forward, throttle).y;
        var extreme = Extremes(forward, 1).plus;
        Assert.True(reached > .1f * extreme, $"{reached} of {extreme}");
    }

    [Fact]
    public void AFullTurnAgainstAFullReverseKeepsItsWholeYaw()
    {
        // The only clockwise actuator also pushes forward and the only reverse actuator turns the other way. Delivering
        // a full turn against a full reverse leaves a forward error of 2, the top of TA-R2's error box: an error bound
        // below 2 would take yaw back to pay for it, and turn-authority-full says the turn is whole.
        foreach (var (columns, x, y) in new[]
                 {
                     (new[] { Col(0, 50, 100), Col(0, -50, -100) }, 0f, -1f),
                     (new[] { Col(50, 0, 100), Col(-50, 0, -100) }, -1f, 0f),
                     (new[] { Col(0, -50, 100), Col(0, 50, -100) }, 0f, 1f),
                     (new[] { Col(-50, 0, 100), Col(50, 0, -100) }, 1f, 0f),
                 })
        {
            var yaw = Net(columns, Solve(columns, x, y, 1)).z;
            var extreme = Extremes(columns, 2).plus;
            Assert.True(MathF.Abs(yaw - extreme) <= 1e-4f * extreme, $"move ({x},{y}): yaw {yaw / Deg} of {extreme / Deg}");
        }
    }

    // Forty thrusters of 20..60 m/s^2 on random half-axes with random yaw, the shape of a hull far larger than any demo
    // ship. Deterministic from the seed.
    static float3[] LargeHull(int n, int seed)
    {
        var rng = new System.Random(seed);
        var columns = new float3[n];
        for (var i = 0; i < n; i++)
        {
            var a = 20f + 40f * (float)rng.NextDouble();
            var yaw = (float)(rng.NextDouble() * 2 - 1) * 120f;
            var dir = rng.Next(10);
            columns[i] = dir < 4 ? Col(0, a, yaw) : dir < 6 ? Col(0, -a, yaw) : dir < 8 ? Col(a, 0, yaw) : Col(-a, 0, yaw);
        }
        return columns;
    }

    [Fact]
    public void NothingAskedAfterAnIterationCapCallFiresNothing()
    {
        // On this hull the warm solve of a zero-intent call needs more than the solver's 100 iterations, and the iterate it
        // stopped at fired throttles up to 1.0 with nothing asked. Whether nothing is asked is decided from the intent, not
        // by how far the solver gets.
        var histories = new[]
        {
            (Seed: 9039, History: new[] { (0.0112729073f, -0.170811713f, 0.809294581f), (-0.474286854f, 0.703138232f, 0f), (0.294080615f, 0.934491992f, -0.776120305f) }),
            (Seed: 9079, History: new[] { (0.607828975f, 1f, 0.202640772f), (-0.620587587f, 0.0162956715f, -0.898228765f), (0.0739500523f, 1f, -0.903907537f) }),
        };
        foreach (var (seed, history) in histories)
        {
            var hull = LargeHull(40, seed);
            var allocator = new ThrustAllocator();
            foreach (var (x, y, turn) in history) Solve(allocator, hull, x, y, turn);
            Assert.All(Solve(allocator, hull, 0, 0, 0), t => Assert.Equal(0f, t));
            Assert.All(Solve(allocator, hull, 0, 0, 0), t => Assert.Equal(0f, t));
        }
    }

    [Fact]
    public void ASmallIntentIsStillAnsweredWhateverTheColumnScale()
    {
        // Zero is a fact about the intent alone: an intent of 1e-3 of the half-axis is served at any scale of thrust.
        foreach (var scale in new[] { 1e-3f, 1f, 1e3f })
        {
            var hull = Duel();
            for (var i = 0; i < hull.Length; i++) hull[i] *= scale;
            var extreme = Extremes(hull, 2).plus;
            var yaw = Net(hull, Solve(hull, 0, 0, 1e-3f)).z;
            Assert.True(yaw > .5e-3f * extreme, $"scale {scale}: yaw {yaw} of {extreme}");
        }
    }

    [Fact]
    public void NothingAskedAfterACallFiresNothing()
    {
        // The warm start left by a call must not leak into a hold of zero intent: exactly zero, not solver residue.
        foreach (var hull in new[] { Duel(), Symmetric() })
            foreach (var previous in new[] { (.5f, .5f, .5f), (1f, 1f, 1f), (-.3f, .8f, .6f), (0f, 0f, -1f), (0f, 1f, 0f) })
            {
                var allocator = new ThrustAllocator();
                Solve(allocator, hull, previous.Item1, previous.Item2, previous.Item3);
                Assert.All(Solve(allocator, hull, 0, 0, 0), t => Assert.Equal(0f, t));
            }
    }

    [Fact]
    public void TheDriveCarriesTheLinearCostsLeakBeforeTheOnset()
    {
        // The error variables are paid linearly toward zero (TA-R2): that cost, against the ridge, holds the drive at a
        // small constant throttle before the onset, 1.30e-4 at turn .5 and 1.14e-4 at turn -.5 on the Duel (a cost row
        // pulling the errors the other way gives 1.06e-4 and 0.93e-4). The bounds are the measured value less 8%.
        var duel = Duel();
        Assert.InRange(Solve(duel, 0, 0, .5f)[0], 1.2e-4f, LeakBound);
        Assert.InRange(Solve(duel, 0, 0, -.5f)[1], 1.05e-4f, LeakBound);
    }

    [Fact]
    public void TheForwardFloorHoldsEveryColumnThatPushesForward()
    {
        // The throttle lock (ruling overdrive-forward-floor): a column is floored by the sign of its own forward push, not
        // by its size or its torque. A sliver of forward push is held at full through a hard turn either way, and the
        // columns that push nowhere or back are free.
        var hull = new[] { Col(0, 53.94f, 49.24f), Col(0, 61.19f, -55.86f), Col(0, .01f, -80f), Col(29.08f, 0, 166.26f), Col(0, -40f, 90f) };
        var allocator = new ThrustAllocator();
        foreach (var turn in new[] { 1f, -1f })
        {
            var throttle = new float[hull.Length];
            allocator.Allocate(hull, new float2(1, -1), turn, throttle, forwardFloor: true);
            Assert.All(throttle.Take(3), t => Assert.True(t > .9999f, "a forward-pushing column holds full"));

            var blind = new float[hull.Length];
            new ThrustAllocator().Allocate(hull, default, turn, blind, forwardFloor: true);
            var apart = throttle.Zip(blind, (a, b) => MathF.Abs(a - b)).Max();
            Assert.True(apart < 1e-6f, $"the stick changed the floored throttles by {apart}");

            var open = new float[hull.Length];
            new ThrustAllocator().Allocate(hull, new float2(0, 1), turn, open);
            Assert.True(open.Take(3).Min() < .5f, "fixture: unfloored, the turn idles a forward drive");
        }
    }

    [Fact]
    public void TheThrottlesStayInBoundsWhateverTheSolveEnds()
    {
        // The solver can stop at its iteration limit rather than converge. Probed on 64000 calls over 1 to 32 columns, scales
        // 1e-6 to 1e6, micro and ordinary intents (2026-10-10): 8 of 60297 solves reached it, all on 32 synthetic columns, all with
        // intents near 1e-8, none from a fresh allocator, none on a shipped hull (the largest has 8 thrusters). Wherever the
        // solve ends, the allocator hands back what it has: finite, within [0,1], and the floored columns at full.
        var rng = new System.Random(20261010);
        float Rand(float lo, float hi) => lo + (float) rng.NextDouble() * (hi - lo);
        foreach (var scale in new[] { 1e-3f, 1f })
            for (var round = 0; round < 12; round++)
            {
                var hull = Enumerable.Range(0, 32).Select(_ => new float3(Rand(-1, 1) * scale, Rand(-1, 1) * scale, rng.Next(4) == 0 ? 0 : Rand(-.3f, .3f) * scale)).ToArray();
                var allocator = new ThrustAllocator();
                var throttle = new float[hull.Length];
                for (var call = 0; call < 40; call++)
                {
                    var micro = rng.Next(4) == 0 ? 1e-7f : 1f;
                    var floor = rng.Next(3) == 0;
                    allocator.Allocate(hull, new float2(Rand(-1, 1) * micro, Rand(-1, 1) * micro), Rand(-1.5f, 1.5f) * micro, throttle, floor);
                    Assert.All(throttle, t => Assert.InRange(t, 0f, 1f));
                    if (floor)
                        for (var i = 0; i < hull.Length; i++)
                            if (hull[i].y > 0) Assert.Equal(1f, throttle[i]);
                }
            }
    }

    [Fact]
    public void AMicroIntentIsServed()
    {
        // Zero is a fact about the intent alone (follow-up allocator-residual-pins): an intent of 1e-6 or 1e-10 of the
        // half-axis is not zero, so something fires and the net goes the way it was asked.
        void Served(float3[] hull, float x, float y, float turn, string what)
        {
            var throttle = Solve(hull, x, y, turn);
            var net = Net(hull, throttle);
            var asked = x != 0 ? x : y != 0 ? y : turn;
            var got = x != 0 ? net.x : y != 0 ? net.y : net.z;
            Assert.True(throttle.Max() > 0f, $"{what}: nothing fired");
            Assert.True(got * asked > 0f, $"{what}: the net does not go the way it was asked");
        }

        foreach (var micro in new[] { 1e-6f, 1e-10f })
        {
            Served(Duel(), 0, 0, micro, "duel turn +");
            Served(Duel(), 0, 0, -micro, "duel turn -");
            Served(Duel(), 0, micro, 0, "duel forward");
            Served(Symmetric(), micro, 0, 0, "symmetric starboard");
            Served(Symmetric(), -micro, 0, 0, "symmetric port");
            Served(Symmetric(), 0, micro, 0, "symmetric forward");
            Served(Symmetric(), 0, -micro, 0, "symmetric reverse");
        }
    }
}
