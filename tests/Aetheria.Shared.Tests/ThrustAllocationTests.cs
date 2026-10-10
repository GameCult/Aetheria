/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Flight control allocates thrust (ruling flight-control-allocates-thrust): Ship.Update hands the intent and every
// thruster's live column to one ThrustAllocator, and a ship flies on what that answers. These tests fly the Duel's
// Longinus (two mismatched Large Drives forward, a Talaria on each flank) through the real Ship.Update; a test that reads
// velocity steps the arena, because the zone places the orbits that Ship.Update alone leaves stacked at the spawn.
public sealed partial class RunStartTests
{
    private const float AllocDt = 1f / 60f;

    private (Zone arena, Ship player) AllocDuel()
    {
        var launched = Launch(new Duel());
        Assert.Empty(launched.failures);
        return (launched.arena, launched.staged.Player);
    }

    private static Thruster[] AllocDrives(Ship ship) =>
        ship.GetBehaviors<Thruster>().Where(t => t.Item.EquippableItem.Rotation == ItemRotation.Reversed).OrderBy(t => t.NominalThrust).ToArray();

    // The signed heading change in degrees, positive clockwise, of `ticks` of the ship's own Update.
    private static float AllocFly(Ship ship, int ticks)
    {
        var before = ship.Direction;
        for (var tick = 0; tick < ticks; tick++) ship.Update(AllocDt);
        return degrees(SignedTurn(before, ship.Direction));
    }

    [Fact]
    public void MismatchedDrivesHoldHeading()
    {
        var (_, player) = AllocDuel();
        var drives = AllocDrives(player);
        player.MovementDirection = float2(0, 1);
        player.Turn = 0;
        Assert.True(abs(AllocFly(player, 120)) < .1f);
        // The drives start alike and heat apart as they fire: by now they are mismatched and both pushing.
        Assert.True(drives.Length >= 2 && drives[^1].NominalThrust > drives[0].NominalThrust * 1.05f, "fixture: the drives are mismatched");
        Assert.All(drives, d => Assert.True(d.Axis > .5f, "fixture: the drives are pushing"));
    }

    [Theory]
    [InlineData("strongest disabled")]
    [InlineData("weakest disabled")]
    [InlineData("one destroyed")]
    public void ALostDriveStillHoldsHeading(string loss)
    {
        var (_, player) = AllocDuel();
        var drives = AllocDrives(player);
        var lost = loss == "weakest disabled" ? drives[0] : drives[^1];
        var kept = loss == "weakest disabled" ? drives[^1] : drives[0];
        player.MovementDirection = float2(0, 1);
        player.Turn = 0;
        for (var tick = 0; tick < 10; tick++) player.Update(AllocDt);
        if (loss == "one destroyed")
        {
            var destroyed = 0;
            using var sub = player.ItemDestroyed.Subscribe(_ => destroyed++);
            player.ItemAbsorb(lost.Item, new[] { 1e6f });
            Assert.Equal(1, destroyed); // the death event fired
        }
        else lost.Item.Enabled.Value = false;
        player.Update(AllocDt);
        Assert.False(lost.Item.Active.Value);

        Assert.True(abs(AllocFly(player, 120)) < .1f);
        Assert.True(kept.Axis > .5f, "the live drive pushes");
        Assert.Equal(0f, lost.Axis);
    }

    [Fact]
    public void ADegradedDriveIsBalancedLive()
    {
        var (_, player) = AllocDuel();
        var drives = AllocDrives(player);
        player.MovementDirection = float2(0, 1);
        player.Turn = 0;
        Assert.True(abs(AllocFly(player, 30)) < .1f);

        // The drive is heated a little off its plateau: its live thrust moves, and a column cached before would not.
        var weakened = drives[^1];
        var before = weakened.Thrust;
        for (var step = 0; step < 20 && weakened.Thrust > before * .9f; step++)
        {
            foreach (var cell in weakened.Item.InsetShape.Coordinates) player.Temperature[cell.x, cell.y] += 2f;
            weakened.Item.UpdatePerformance();
        }
        Assert.True(weakened.Thrust < before * .95f && weakened.Item.Active.Value, "fixture: the heated drive pushes measurably less");
        Assert.True(abs(AllocFly(player, 60)) < .1f);
    }

    [Fact]
    public void ATinyImbalanceStillHolds()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        RestoredHullsTests.FreeThrusterPower(cache);
        var ship = RestoredHullsTests.BuildThrustedShip(cache, "Longinus");
        var drives = AllocDrives(ship);
        Assert.True(drives.Length >= 2);
        var other = drives[0];
        var detuned = drives[1];

        // Detune one drive until its live thrust is .4% under the other's, by bisecting its durability.
        var full = detuned.Item.Data.Durability;
        float lo = 0, hi = 1;
        for (var step = 0; step < 50; step++)
        {
            var mid = (lo + hi) / 2;
            detuned.Item.EquippableItem.Durability = full * mid;
            detuned.Item.UpdatePerformance();
            if (detuned.Thrust < other.Thrust * .996f) lo = mid; else hi = mid;
        }
        var ratio = detuned.Thrust / other.Thrust;
        Assert.InRange(ratio, .995f, .998f);

        ship.MovementDirection = float2(0, 1);
        ship.Turn = 0;
        // Six seconds: the ship has no heatsinks, and its drives overheat and shut down at about seven.
        Assert.True(abs(AllocFly(ship, 360)) < .05f);
    }

    [Fact]
    public void ARepairedDriveRejoins()
    {
        var (_, player) = AllocDuel();
        var drive = AllocDrives(player)[^1];
        player.MovementDirection = float2(0, 1);
        player.Turn = 0;
        drive.Item.Enabled.Value = false;
        for (var tick = 0; tick < 30; tick++) player.Update(AllocDt);
        Assert.Equal(0f, drive.Axis);
        drive.Item.Enabled.Value = true;
        player.Update(AllocDt);
        Assert.True(drive.Axis > .5f);
    }

    [Fact]
    public void ADeadThrusterNeverGetsThrottle()
    {
        var (_, player) = AllocDuel();
        var thrusters = player.GetBehaviors<Thruster>().ToArray();
        Assert.True(thrusters.Length >= 4);
        var asks = new (float2 move, float turn)[]
        {
            (float2(0, 1), 0), (float2(0, -1), 0), (float2(1, 0), 0), (float2(-1, 0), 0), (float2(.7f, .7f), 0),
            (float2(0, 0), 1), (float2(0, 0), -1)
        };
        foreach (var dead in thrusters)
        {
            dead.Item.Enabled.Value = false;
            foreach (var (move, turn) in asks)
            {
                player.MovementDirection = move;
                player.Turn = turn;
                for (var tick = 0; tick < 30; tick++)
                {
                    player.Update(AllocDt);
                    Assert.Equal(0f, dead.Axis);
                }
            }
            dead.Item.Enabled.Value = true;
        }
    }

    [Fact]
    public void AFullTurnSpendsTheHullsWholeTurn()
    {
        var (_, player) = AllocDuel();
        var thrusters = player.GetBehaviors<Thruster>().ToArray();
        player.MovementDirection = float2(0, 1);
        player.Turn = 1;
        var sum = 0f;
        var extremeSum = 0f;
        var count = 0;
        for (var tick = 0; tick < 60; tick++)
        {
            // The clockwise box extreme this tick: every thruster that turns clockwise, at full, from its own column.
            var extreme = thrusters.Sum(t => max(t.Column(t.NominalThrust).z, 0f));
            player.Update(AllocDt);
            if (tick < 10) continue;
            sum += player.TurnRate;
            extremeSum += extreme;
            count++;
        }
        Assert.True(extremeSum > 0f);
        Assert.True(sum >= .98f * extremeSum, $"the turn spent {sum / extremeSum} of the hull's whole");
    }

    [Fact]
    public void TheDuelFliesStraightWhenTheArenaIsStepped()
    {
        var (arena, player) = AllocDuel();
        player.MovementDirection = float2(0, 1);
        player.Turn = 0;
        var start = player.Direction;
        var previous = 0f;
        for (var tick = 0; tick < 240; tick++)
        {
            arena.Update(AllocDt);
            var along = dot(player.Velocity, normalize(player.Direction));
            var oneTickOfDrag = previous - decay(previous, player.HullData.Drag, AllocDt);
            Assert.True(along >= previous - oneTickOfDrag - 1e-3f, $"tick {tick}: along-nose speed fell from {previous} to {along}");
            previous = along;
        }
        Assert.True(previous > 10f, "fixture: the ship got going");
        Assert.True(abs(degrees(SignedTurn(start, player.Direction))) < .1f);
    }
}
