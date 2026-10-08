/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// SimClock owns sim time's relation to real time: frames of real seconds in, whole fixed steps out. Nothing here hands
// a clock to a presenter; DrawAhead is pure reads of the latest state.
public sealed class SimClockTests
{
    private static readonly float[] Irregular = { 1f / 144, 1f / 30, 1f / 90 };

    // Runs frames cycling through the lengths until the clock has run the steps; returns the step values it passed.
    private static List<float> Drive(SimClock clock, float[] frames, long steps)
    {
        var passed = new List<float>();
        for (var frame = 0; clock.Steps < steps; frame++)
            clock.Advance(frames[frame % frames.Length], dt => passed.Add(dt));
        return passed;
    }

    [Fact]
    public void TheStepNeverChanges()
    {
        foreach (var frames in new[] { new[] { 1f / 60 }, Irregular, new[] { .09f, .004f, 0f, 3f } })
        {
            var clock = new SimClock(1f / 60f);
            var passed = Drive(clock, frames, 500);
            Assert.True(passed.Count >= 500);
            Assert.All(passed, dt => Assert.Equal(clock.Step, dt));
        }
        var slow = new SimClock(.1f, 6);
        Assert.All(Drive(slow, Irregular, 50), dt => Assert.Equal(.1f, dt));
    }

    [Fact]
    public void StepsFollowRealTime()
    {
        // Ten real seconds of irregular frames.
        var clock = new SimClock(1f / 60f);
        var elapsed = 0.0;
        for (var frame = 0; elapsed < 10.0; frame++)
        {
            var seconds = Irregular[frame % Irregular.Length];
            elapsed += seconds;
            clock.Advance(seconds, _ => { });
        }
        Assert.InRange(elapsed, 10.0, 10.1);
        Assert.InRange(clock.Steps, Math.Round(elapsed * 60) - 1, Math.Round(elapsed * 60) + 1);

        var slow = new SimClock(1f / 60f, 30);
        for (var frame = 0; frame < 600; frame++) slow.Advance(1f / 60, _ => { });
        Assert.InRange(slow.Steps, 299, 301);
    }

    [Fact]
    public void AHitchIsClamped()
    {
        var clock = new SimClock(1f / 60f);
        var ran = clock.Advance(5f, _ => { });
        Assert.Equal(6, ran);
        Assert.Equal(6, clock.Steps);
    }

    [Fact]
    public void PauseFeedsNothing()
    {
        var clock = new SimClock(1f / 60f);
        clock.Advance(.025f, _ => { });
        var lead = clock.Lead;
        var steps = clock.Steps;
        Assert.True(lead > 0);
        foreach (var seconds in new[] { 0f, -1f })
        {
            Assert.Equal(0, clock.Advance(seconds, _ => throw new InvalidOperationException()));
            Assert.Equal(lead, clock.Lead);
            Assert.Equal(steps, clock.Steps);
        }
    }

    [Fact]
    public void LeadIsHonest()
    {
        var clock = new SimClock(.1f, 6);
        double sinceLastStep = 0;
        for (var frame = 0; frame < 400; frame++)
        {
            // The real time since the last step: what was left over plus this frame, less the steps it paid for.
            sinceLastStep += Irregular[frame % Irregular.Length];
            var seconds = Irregular[frame % Irregular.Length];
            clock.Advance(seconds, _ => sinceLastStep -= 1.0 / clock.StepsPerRealSecond);
            Assert.InRange(clock.Lead, 0f, clock.Step * .99999f);
            Assert.Equal(sinceLastStep * clock.StepsPerRealSecond * clock.Step, clock.Lead, 4);
        }
    }

    [Fact]
    public void ABadClockIsRefusedByFieldName()
    {
        Assert.Equal("step", Assert.Throws<ArgumentOutOfRangeException>(() => new SimClock(0f)).ParamName);
        Assert.Equal("stepsPerRealSecond", Assert.Throws<ArgumentOutOfRangeException>(() => new SimClock(1f, 0f)).ParamName);
    }
}

public sealed partial class RunStartTests
{
    private const float ClockFrame = 1f / 60f;
    private const int HistorySteps = 1800;

    // The Duel launched as the game launches it, its item random reseeded so play draws the same numbers every time.
    private (Zone arena, Ship player) LaunchDuel()
    {
        var launched = Launch(new Duel());
        Assert.Empty(launched.failures);
        _items.Random = new CultMath.Random(0xC0FFEE);
        launched.staged.Player.MovementDirection = float2(0, 1);
        launched.staged.Player.Turn = .5f;
        foreach (var ship in launched.arena.Entities.OfType<Ship>())
            ship.SetTarget(launched.arena.Entities.OfType<Ship>().First(other => other != ship));
        return (launched.arena, launched.staged.Player);
    }

    private static string R(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    // Everything the step decides, as text: the zone clock, each entity's pose and condition, and each shot resolved.
    private static string Snapshot(Zone zone, List<string> shots)
    {
        var text = new StringBuilder(R(zone.Time));
        foreach (var entity in zone.Entities)
        {
            text.Append($"|{entity.Name} {R(entity.Position.x)},{R(entity.Position.z)} v{R(entity.Velocity.x)},{R(entity.Velocity.y)} " +
                        $"d{R(entity.Direction.x)},{R(entity.Direction.y)} w{R(entity.TurnRate)} h{R(entity.Hull.Durability)}");
            if (entity.Shield != null) text.Append($" s{R(entity.Shield.Progress)}");
            if (entity.Temperature != null)
            {
                var heat = 0f;
                foreach (var cell in entity.Temperature) heat += cell;
                text.Append($" t{R(heat)}");
            }
        }
        text.Append($"|shots {string.Join(",", shots)}");
        shots.Clear();
        return text.ToString();
    }

    // Feeds the zone through one SimClock with the frames cycled, snapshotting every step until HistorySteps have run.
    private static List<string> History(Zone zone, float[] frames)
    {
        var clock = new SimClock(1f / 60f);
        var shots = new List<string>();
        using var _ = zone.ShotResolved.Subscribe(outcome => shots.Add($"{outcome.ShotId}:{outcome.Result}"));
        var history = new List<string>();
        for (var frame = 0; history.Count < HistorySteps; frame++)
            clock.Advance(frames[frame % frames.Length], dt =>
            {
                // Both ships open fire every 90th step, on the step count, so the fight is the same fight whatever
                // the frames were.
                if (history.Count % 90 == 0)
                    foreach (var ship in zone.Entities.OfType<Ship>())
                        foreach (var weapon in ship.GetBehaviors<Weapon>())
                            FireControl.Fire(weapon, weapon.Item, ship);
                zone.Update(dt);
                history.Add(Snapshot(zone, shots));
            });
        return history.Take(HistorySteps).ToList();
    }

    // Control: the same launch driven twice the same way is the same history. If this fails the simulation is
    // nondeterministic before the clock is involved, and FrameTimesDoNotChangeHistory proves nothing.
    [Fact]
    public void OneHistoryTwice()
    {
        var first = History(LaunchDuel().arena, new[] { ClockFrame });
        var second = History(LaunchDuel().arena, new[] { ClockFrame });
        Assert.Equal(first, second);
        Assert.Contains(first, line => !line.EndsWith("|shots ")); // shots were fired and resolved
    }

    [Fact]
    public void FrameTimesDoNotChangeHistory()
    {
        var steady = History(LaunchDuel().arena, new[] { ClockFrame });
        var irregular = History(LaunchDuel().arena, new[] { 1f / 144, 1f / 30, 1f / 90 });
        Assert.Equal(steady.Count, irregular.Count);
        for (var step = 0; step < steady.Count; step++)
            Assert.True(steady[step] == irregular[step], $"step {step}: {steady[step]} != {irregular[step]}");
    }

    private static float SignedTurn(float2 before, float2 after) =>
        atan2(before.x * after.y - before.y * after.x, dot(before, after));

    [Fact]
    public void TurnRateIsTheActuatorsTurn()
    {
        var (arena, player) = LaunchDuel();
        Assert.Equal(0f, player.TurnRate); // staged: nothing has turned it
        var turned = 0;
        for (var step = 0; step < 240; step++)
        {
            var before = player.Direction;
            arena.Update(ClockFrame);
            var angle = SignedTurn(before, player.Direction);
            Assert.InRange(abs(abs(angle) - abs(player.TurnRate * ClockFrame)), 0f, 1e-6f);
            // A positive turn demand turns clockwise, and TurnRate is signed the way the step turned Direction.
            if (abs(player.TurnRate) > 1e-4f)
            {
                turned++;
                Assert.True(player.TurnRate > 0 == dot(player.Direction, before.Rotate(ItemRotation.Clockwise)) > 0);
            }
        }
        Assert.True(turned > 60, "the held turn turned the ship");

        // A write to Direction between steps (staging, a load, a wormhole exit) is not a turn.
        player.Turn = 0;
        for (var step = 0; step < 600 && abs(player.TurnRate) > 0; step++) arena.Update(ClockFrame);
        player.Direction = normalize(float2(3, -4));
        arena.Update(ClockFrame);
        Assert.Equal(0f, player.TurnRate);
    }

    [Fact]
    public void DrawAheadIsTheStateAtZeroLead()
    {
        var (arena, player) = LaunchDuel();
        for (var step = 0; step < 120; step++) arena.Update(ClockFrame);
        var before = Snapshot(arena, new List<string>());

        var zero = DrawAhead.Position(player, 0);
        Assert.Equal(player.Position.x, zero.x);
        Assert.Equal(player.Position.y, zero.y);
        Assert.Equal(player.Position.z, zero.z);
        var rotation = DrawAhead.Rotation(player, 0);
        Assert.Equal(1f, abs(rotation.x * player.Rotation.x + rotation.y * player.Rotation.y + rotation.z * player.Rotation.z + rotation.w * player.Rotation.w), 5);

        const float lead = .007f;
        var ahead = DrawAhead.Position(player, lead);
        Assert.Equal(player.Position.x + player.Velocity.x * lead, ahead.x, 6);
        Assert.Equal(player.Position.y, ahead.y);
        Assert.Equal(player.Position.z + player.Velocity.y * lead, ahead.z, 6);
        Assert.Equal(arena.Time + lead, DrawAhead.Time(arena, lead));
        Assert.Equal(before, Snapshot(arena, new List<string>()));
    }

    // The turn a presenter draws ahead is the turn the next step makes: a ship held in a steady turn, drawn one whole
    // step ahead of the pose it was last given, is where the sim puts that pose next.
    [Fact]
    public void DrawnRotationFollowsTheTurn()
    {
        var (arena, player) = LaunchDuel();
        player.Turn = 1f;
        for (var step = 0; step < 300; step++) arena.Update(ClockFrame);
        var rate = player.TurnRate;
        Assert.True(abs(rate) > .05f, "the ship is turning");
        var drawn = DrawAhead.Rotation(player, ClockFrame);
        arena.Update(ClockFrame);
        Assert.True(abs(player.TurnRate - rate) < 1e-3f * abs(rate), "the turn is steady");
        var next = player.Rotation;
        var error = 2 * acos(min(1f, abs(drawn.x * next.x + drawn.y * next.y + drawn.z * next.z + drawn.w * next.w)));
        var turn = abs(rate * ClockFrame);
        Assert.True(turn > .005f, "the step turns the ship enough to tell its sense");
        Assert.True(error < .2f * turn, $"drawn one step ahead is where the sim puts the pose next (off by {error} of a {turn} turn)");
    }
}
