using System;
using System.Collections.Generic;
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
}
