using System;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The helm split: Ship.Turn is the one facing command, Steering.Toward turns a heading into it, HelmInput owns
// the player's face-the-aim state, and the hull never reads Aim. Ships are the restored Longinus with its real
// rotation thrusters, so rotation pays the thruster path the way it does in play.
public sealed class SteeringTests
{
    private const string Hull = "Longinus";

    private static float2 Right(Ship ship) => normalize(ship.Direction).Rotate(ItemRotation.Clockwise);

    // A unit heading `degrees` clockwise of the hull's nose.
    private static float2 Heading(Ship ship, float degrees)
    {
        var r = radians(degrees);
        return normalize(ship.Direction) * cos(r) + Right(ship) * sin(r);
    }

    private static float Angle(float2 from, float2 to) => acos(clamp(dot(normalize(from), normalize(to)), -1f, 1f));

    [Fact]
    public void TowardIsZeroOnTheHeading()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);

        Assert.Equal(0f, Steering.Toward(ship, ship.Direction));
        Assert.Equal(0f, Steering.Toward(ship, Heading(ship, .5f)));
        Assert.Equal(0f, Steering.Toward(ship, Heading(ship, -.5f)));
    }

    [Fact]
    public void TowardTurnsClockwiseForAStarboardHeading()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);

        var expected = sqrt(sin(radians(45f)));
        Assert.Equal(expected, Steering.Toward(ship, Heading(ship, 45f)), 4);
        Assert.Equal(-expected, Steering.Toward(ship, Heading(ship, -45f)), 4);
    }

    [Fact]
    public void AHeadingBehindStillTurns()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);

        Assert.Equal(1f, Steering.Toward(ship, -ship.Direction)); // exactly astern: no lateral component, so +1
        Assert.Equal(-1f, Steering.Toward(ship, Heading(ship, -170f)));
        Assert.Equal(1f, Steering.Toward(ship, Heading(ship, 170f)));
    }

    [Fact]
    public void ANonFiniteHeadingHoldsCourse()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);

        foreach (var heading in new[] { float2.zero, float2(float.NaN, 1), float2(1, float.NaN), float2(float.PositiveInfinity, 0) })
        {
            ship.Turn = Steering.Toward(ship, heading);
            Assert.Equal(0f, ship.Turn);
            for (var i = 0; i < 5; i++) ship.Update(1f / 60f);
            Assert.False(float.IsNaN(ship.Direction.x) || float.IsNaN(ship.Direction.y));
        }
    }

    [Fact]
    public void HoldAndToggleAgree()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var held = new HelmInput { Held = true };
        var latched = new HelmInput();
        latched.Toggle();

        foreach (var direction in new[] { float2(0, 1), float2(1, 0), float2(-1, -1), float2(.3f, -.9f) })
        foreach (var aim in new[] { float2(0, 1), float2(1, 1), float2(-1, 0), float2(0, -1), float2(.2f, -.7f), float2.zero })
        foreach (var axis in new[] { -1f, 0f, .5f, 1f })
        {
            ship.Direction = direction;
            var expected = Steering.Toward(ship, aim);
            Assert.Equal(expected, held.Demand(ship, aim, axis));
            Assert.Equal(expected, latched.Demand(ship, aim, axis));
        }
    }

    [Fact]
    public void FaceAimOffFollowsTheAxis()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var helm = new HelmInput();

        foreach (var aim in new[] { float2(0, 1), float2(1, 0), float2(-1, -1) })
        {
            Assert.Equal(.25f, helm.Demand(ship, aim, .25f));
            Assert.Equal(1f, helm.Demand(ship, aim, 3f));
            Assert.Equal(-1f, helm.Demand(ship, aim, -3f));
        }

        helm.Toggle();
        Assert.True(helm.Latched);
        helm.Toggle();
        Assert.False(helm.Latched);
        Assert.False(helm.FaceAim);
    }

    [Fact]
    public void TheHullIgnoresTheAim()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var start = ship.Direction;

        ship.Turn = 0;
        ship.Aim = float3(Right(ship).x, 0, Right(ship).y);
        for (var i = 0; i < 60; i++) ship.Update(1f / 60f);

        Assert.True(Angle(start, ship.Direction) < radians(.01f));
    }

    [Fact]
    public void TurnRotatesThroughTheThrusters()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var clockwise = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var counter = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var dead = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var start = clockwise.Direction;
        var right = Right(clockwise);

        foreach (var item in dead.Equipment)
            if (item.Behaviors.Any(b => b is Thruster)) item.Enabled.Value = false;

        clockwise.Turn = 1;
        counter.Turn = -1;
        dead.Turn = 1;
        for (var i = 0; i < 10; i++)
        {
            clockwise.Update(1f / 60f);
            counter.Update(1f / 60f);
            dead.Update(1f / 60f);
        }

        Assert.True(dot(clockwise.Direction, right) > 1e-3f, $"clockwise {dot(clockwise.Direction, right)}");
        Assert.True(dot(counter.Direction, right) < -1e-3f, $"counter {dot(counter.Direction, right)}");
        Assert.True(Angle(start, dead.Direction) < radians(.01f), $"dead {Angle(start, dead.Direction)}");
    }

    [Fact]
    public void AccelerateWritesTurnNotAim()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var agent = new Agent(ship);
        var aim = float3(0, 0, 1);
        ship.Aim = aim;

        agent.Accelerate(Right(ship) * 100f);
        Assert.True(ship.Turn > 0f);
        Assert.Equal(aim, ship.Aim);

        ship.Turn = .37f;
        agent.Accelerate(-Right(ship) * 100f, true);
        Assert.Equal(.37f, ship.Turn);
        Assert.Equal(aim, ship.Aim);
    }

    // The thresholds Accelerate steers by: above 20 it turns toward the velocity error, between 1 and 20 it
    // thrusts sideways and leaves Turn to the caller, at or below 1 it holds still.
    [Fact]
    public void AccelerateThresholdsAreExact()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var agent = new Agent(ship);
        Assert.Equal(float2(0, 1), ship.Direction); // starboard is +x

        ship.Turn = .37f;
        agent.Accelerate(float2(20, 0), false); // exactly the forward threshold: not above it
        Assert.Equal(.37f, ship.Turn);
        Assert.Equal(float2(1, 0), ship.MovementDirection);

        agent.Accelerate(float2(21, 0), false);
        Assert.True(ship.Turn > .5f);
        Assert.Equal(float2(0, 0), ship.MovementDirection);

        ship.Turn = .37f;
        agent.Accelerate(float2(1, 0), false); // exactly the thrust threshold: not above it
        Assert.Equal(float2.zero, ship.MovementDirection);
        Assert.Equal(.37f, ship.Turn);

        agent.Accelerate(float2(5, 0), false);
        Assert.Equal(float2(1, 0), ship.MovementDirection);
        Assert.Equal(.37f, ship.Turn);
    }

    [Fact]
    public void AccelerateSteersByTheVelocityError()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, Hull);
        var agent = new Agent(ship);
        ship.Velocity = float2(100, 0);
        ship.Turn = .37f;

        agent.Accelerate(float2(100, 0), false); // already at the wanted velocity: nothing to correct

        Assert.Equal(.37f, ship.Turn);
        Assert.Equal(float2.zero, ship.MovementDirection);
    }
}
