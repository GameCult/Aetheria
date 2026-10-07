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
using int2 = CultMath.int2;

// The evasion term (docs/aetheria-release-map.md, "Evasion is how unpredictably the target can and does change its
// motion"): how far a target can and does move off the line of fire in the solution window, one factor of every hit
// price. The fixture is RunStartTests': the shipped catalog and the authored settings, so the ships fly in the real
// Ship.Update with the real thrusters and aether drive. Ship flights step the ships directly (they are alone in
// their arena); the one price test that rolls shots steps only FireControl.
public sealed partial class RunStartTests
{
    private const float EvDt = 1f / 60f;

    // The Duel's LonginusX fit: one Traction aether drive that pushes all four ways, with its guns.
    private static ScenarioFit EvGemini(ScenarioStage stage, string gun = "FastBlast+-") => stage.Fit("LonginusX",
        ("Cockpit 2x2", int2(2, 6), ItemRotation.None), ("Traction", int2(2, 3), ItemRotation.None), ("Core Power", int2(2, 1), ItemRotation.None),
        ("GT 3K", int2(0, 5), ItemRotation.None), ("GT 3K", int2(5, 5), ItemRotation.None), (gun, int2(1, 8), ItemRotation.None),
        (gun, int2(4, 8), ItemRotation.None), ("Iapyx", int2(1, 2), ItemRotation.CounterClockwise), ("Iapyx", int2(4, 2), ItemRotation.Clockwise),
        ("not if i see you first", int2(3, 10), ItemRotation.None), ("PotaT+-", int2(1, 5), ItemRotation.None),
        ("Fire Control Array", int2(2, 5), ItemRotation.None), ("Store-All Plus", int2(2, 8), ItemRotation.None));

    // The catalog's three fitted ships, far apart in the fixture arena: the generated Longinus and Djinni at the
    // fixture seed and the omnidirectional LonginusX.
    private (Zone arena, Ship longinus, Ship djinni, Ship gemini) EvFleet()
    {
        Ship longinus = null, djinni = null, gemini = null;
        var scenario = new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            // The draw order is part of the fixture: the generator draws from the run's item stream, so the Djinni is fitted
            // first, then the Longinus, as the map's probe did (M23).
            djinni = stage.Place(stage.Generated("Djinni"), float2(-10000, -30000), facing: float2(0, 1)) as Ship;
            longinus = stage.Place(stage.Generated("Longinus"), float2(-30000, -30000), facing: float2(0, 1)) as Ship;
            gemini = stage.Place(EvGemini(stage), float2(10000, -30000), facing: float2(0, 1)) as Ship;
        });
        var (_, arena, _, failures) = Launch(scenario, Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.NotNull(longinus);
        Assert.NotNull(djinni);
        Assert.NotNull(gemini);
        return (arena, longinus, djinni, gemini);
    }

    // Idle long enough for stats, power and the aether rotors to reach their idle state, then at rest, facing +y,
    // and, after three seconds of coasting, with nothing observed: the track forgets on its own.
    private static void EvSettle(Ship ship)
    {
        ship.MovementDirection = float2(0, 0);
        ship.LookDirection = float3(0, 0, 1);
        for (var step = 0; step < 180; step++) ship.Update(EvDt);
        ship.Velocity = float2(0, 0);
        ship.Direction = float2(0, 1);
    }

    // One of the fixed jinks, from rest facing +y: a square wave across the heading (the jink that crosses a shooter
    // nose-on) or along it (the one that crosses a shooter broadside), a new random direction each half-period, or a
    // turn-and-burn weave of +-60 degrees. Flown in Ship.Update; what the ship does is its own motion.
    private enum EvJink { Lateral, Longitudinal, Random, Weave }

    private static void EvFly(Ship ship, EvJink jink, float halfPeriod, float seconds)
    {
        var random = new System.Random(7);
        var steps = (int) Math.Round(seconds / EvDt);
        var phaseSteps = Math.Max(1, (int) Math.Round(halfPeriod / EvDt));
        float2 movement = float2(0, 0);
        float3 look = float3(0, 0, 1);
        for (var step = 0; step < steps; step++)
        {
            if (step % phaseSteps == 0)
            {
                var phase = step / phaseSteps;
                var side = phase % 2 == 0 ? 1f : -1f;
                switch (jink)
                {
                    case EvJink.Lateral:
                        movement = float2(side, 0);
                        break;
                    case EvJink.Longitudinal:
                        movement = float2(0, side);
                        break;
                    case EvJink.Random:
                        var angle = (float) (random.NextDouble() * 2 * Math.PI);
                        movement = float2(sin(angle), cos(angle));
                        break;
                    case EvJink.Weave:
                        movement = float2(0, 1);
                        look = float3(side * sin(radians(60)), 0, cos(radians(60)));
                        break;
                }
            }
            ship.MovementDirection = movement;
            ship.LookDirection = look;
            ship.Update(EvDt);
        }
        ship.MovementDirection = float2(0, 0);
        ship.LookDirection = float3(0, 0, 1);
    }

    private static float2 EvRight(Ship ship) => normalize(ship.Direction).Rotate(ItemRotation.Clockwise);

    // The best evasion a ship shows over the fixed jink set, against a shooter nose-on (along its heading) and one
    // broadside, at the authored window.
    private static (float noseOn, float broadside) EvBest(Func<Ship> flightShip)
    {
        float noseOn = 0, broadside = 0;
        foreach (var jink in new[] { EvJink.Lateral, EvJink.Longitudinal, EvJink.Random, EvJink.Weave })
            foreach (var halfPeriod in new[] { .5f, 1f })
            {
                // A fresh ship each flight where the caller wants one: an aether drive heats and spends its rotors, and a
                // drive that has run itself offline has no envelope.
                var ship = flightShip();
                EvSettle(ship);
                EvFly(ship, jink, halfPeriod, 4f);
                var flightNose = FireControl.Evasion(ship, normalize(ship.Direction));
                var flightSide = FireControl.Evasion(ship, EvRight(ship));
                var e = ship.Envelope;
                Console.WriteLine($"EVASION flight {ship.HullData.Name} {jink} {halfPeriod}: nose-on {flightNose:F2} broadside {flightSide:F2} rms x {ship.Manoeuvre.LateralRms(float2(1, 0)):F1} y {ship.Manoeuvre.LateralRms(float2(0, 1)):F1} envelope F{e.Forward:F0} R{e.Reverse:F0} L{e.Left:F0} Rt{e.Right:F0} CW{e.Clockwise:F2} CCW{e.CounterClockwise:F2} speed {length(ship.Velocity):F0}");
                noseOn = max(noseOn, flightNose);
                broadside = max(broadside, flightSide);
            }
        return (noseOn, broadside);
    }

    // Ruling evasion-is-unpredictability: the Gemini is evasive because it can floor it in any direction at any time,
    // the Longinus needs to aim itself where it wants to go, and the Djinni is somewhere between.
    [Fact]
    public void TheThreeShipsOrder()
    {
        var (_, longinus, djinni, _) = EvFleet();
        var g = EvBest(() => EvFleet().gemini);
        var l = EvBest(() => longinus);
        var d = EvBest(() => djinni);
        Console.WriteLine($"EVASION window {_items.GameplaySettings.SolutionWindow}: gemini-like nose-on {g.noseOn:F2} broadside {g.broadside:F2}; " +
                          $"longinus {l.noseOn:F2} {l.broadside:F2}; djinni {d.noseOn:F2} {d.broadside:F2}");
        Assert.True(g.noseOn > l.noseOn && l.noseOn > d.noseOn, $"nose-on order: {g.noseOn} > {l.noseOn} > {d.noseOn}");
        Assert.True(g.broadside > l.broadside && l.broadside > d.broadside, $"broadside order: {g.broadside} > {l.broadside} > {d.broadside}");
        // Re-measured (M25): the Djinni's thrusters push 1.7 times harder than the figure the map priced it with, so
        // it evades more than the map's 1.7 m and the Longinus leads it by less than the map's factor of eight.
        Assert.True(l.noseOn >= 2 * d.noseOn && l.broadside >= 2 * d.broadside, "the Longinus evades at least twice the Djinni");
        Assert.True(d.noseOn < 4f && d.broadside < 4f, $"the Djinni is limited by its mass: {d.noseOn}, {d.broadside}");
    }

    // The speed demon, as pure numbers (ruling speed-demon-counterplay): after 4 s of a lateral square wave at full
    // thrust, half-period 0.5 s, nose-on. The window is passed explicitly, not read from settings.
    private static ManoeuvreTrack EvSquare(float amplitude, float halfPeriod, float seconds, float dt, float window)
    {
        var track = default(ManoeuvreTrack);
        var steps = (int) Math.Round(seconds / dt);
        for (var step = 0; step < steps; step++)
            track.Observe(float2(((int) Math.Floor(step * dt / halfPeriod) % 2 == 0 ? 1f : -1f) * amplitude, 0), dt, window);
        return track;
    }

    [Fact]
    public void TheSpeedDemonNumbers()
    {
        var demon = new ManoeuvreEnvelope(60, 60, 60, 60, 3, 3);
        var heavy = new ManoeuvreEnvelope(10, 10, 10, 10, .8f, .8f);
        var noseOn = float2(0, 1);

        float Evade(ManoeuvreEnvelope envelope, float amplitude, float window) =>
            FireControl.Evasion(envelope, noseOn, EvSquare(amplitude, .5f, 4f, EvDt, window), noseOn, window);

        var demonShort = Evade(demon, 60, .5f);
        Assert.InRange(demonShort, 5.07f * .95f, 5.07f * 1.05f);
        Assert.InRange(FireControl.PDeviation(demonShort, 10), .49f - .03f, .49f + .03f);
        Assert.InRange(FireControl.PDeviation(demonShort, 50), .90f - .03f, .90f + .03f);

        var demonLong = Evade(demon, 60, .75f);
        // The map's 9.63 is the model's time mean; this reads the track at the instant of the last flip, 5.1% above it.
        Assert.InRange(demonLong, 9.63f * .94f, 9.63f * 1.06f);
        Assert.InRange(FireControl.PDeviation(demonLong, 10), 0f, .04f + .05f);
        Assert.InRange(FireControl.PDeviation(demonLong, 50), .81f - .03f, .81f + .03f);

        var heavyShort = Evade(heavy, 10, .5f);
        Assert.InRange(heavyShort, .84f * .95f, .84f * 1.05f);
        Assert.InRange(FireControl.PDeviation(heavyShort, 10), .92f - .03f, .92f + .03f);
        Assert.InRange(FireControl.PDeviation(heavyShort, 50), .98f - .03f, .98f + .03f);
    }

    // Reach is what the ship can do: the largest displacement from the coasting path in the window, from the
    // envelope alone, against the same ship's best motion in Ship.Update under fifteen control policies (strafe,
    // forward, reverse, corner, turn-and-burn at several angles, each mirrored).
    private static (float noseOn, float broadside) EvMeasuredHalfWidths(Ship ship, float window)
    {
        EvSettle(ship);
        var start = ship.Position;
        float2 Run(float2 move, float2 look)
        {
            ship.Position = start;
            ship.Velocity = float2(0, 0);
            ship.Direction = float2(0, 1);
            ship.MovementDirection = move;
            ship.LookDirection = float3(look.x, 0, look.y);
            var steps = (int) Math.Round(window / EvDt);
            for (var step = 0; step < steps; step++) ship.Update(EvDt);
            var displacement = ship.Position.xz - start.xz;
            ship.MovementDirection = float2(0, 0);
            ship.LookDirection = float3(0, 0, 1);
            return displacement;
        }

        var ahead = float2(0, 1);
        var idle = Run(float2(0, 0), ahead);
        var policies = new (float2 move, float2 look)[]
        {
            (float2(1, 0), ahead), (float2(-1, 0), ahead), (float2(0, 1), ahead), (float2(0, -1), ahead),
            (float2(1, 1), ahead), (float2(-1, 1), ahead),
            (float2(0, 1), float2(1, 0)), (float2(0, 1), float2(-1, 0)),
            (float2(1, 1), float2(1, 0)), (float2(-1, 1), float2(-1, 0)),
            (float2(0, 1), normalize(float2(1, 1))), (float2(0, 1), normalize(float2(-1, 1))),
            (float2(0, 1), normalize(float2(1, -1))), (float2(0, 0), float2(1, 0)),
            (float2(0, 1), normalize(float2(.05f, -1)))
        };
        float right = 0, left = 0, forward = 0, back = 0;
        foreach (var (move, look) in policies)
        {
            var d = Run(move, look) - idle;
            right = max(right, d.x);
            left = max(left, -d.x);
            forward = max(forward, d.y);
            back = max(back, -d.y);
        }
        ship.Position = start;
        return ((right + left) / 2, (forward + back) / 2);
    }

    private static (float noseOn, float broadside) EvReach(Ship ship, float window)
    {
        var heading = float2(0, 1);
        float Half(float2 n) => .5f * (FireControl.Reach(ship.Envelope, heading, n, window) + FireControl.Reach(ship.Envelope, heading, -n, window));
        return (Half(float2(1, 0)), Half(float2(0, 1)));
    }

    [Fact]
    public void ReachIsWhatTheShipCanDo()
    {
        var (_, longinus, djinni, _) = EvFleet();
        const float window = .5f;
        var results = new List<(string name, (float noseOn, float broadside) reach, (float noseOn, float broadside) measured)>();
        foreach (var (name, ship) in new[] { ("longinus", longinus), ("djinni", djinni) })
        {
            var measured = EvMeasuredHalfWidths(ship, window);
            EvSettle(ship);
            ship.Update(EvDt);
            var reach = EvReach(ship, window);
            Console.WriteLine($"REACH {name}: nose-on reach {reach.noseOn:F3} measured {measured.noseOn:F3}; broadside reach {reach.broadside:F3} measured {measured.broadside:F3}");
            results.Add((name, reach, measured));
        }
        // The reach is the capability and the measured motion is the best of fifteen fixed control policies, so reach may
        // sit above it: by up to 15% for the Longinus, whose controller shapes its turn input (a square root of the
        // remaining angle) where the reach turns at full rate.
        foreach (var (name, reach, measured) in results)
        {
            // The Djinni's measured motion is further below its reach (+53% nose-on, +27% broadside at the last measurement): its
            // strafers are off-axis and Ship.Update cancels their torque (M24), which the envelope, summed from what each thruster
            // can push, does not model. Stated deviation; the bound still pins that the reach covers what the ship does.
            var ceiling = name == "djinni" ? 1.6f : 1.15f;
            Assert.InRange(reach.noseOn, measured.noseOn * .9f, measured.noseOn * ceiling);
            Assert.InRange(reach.broadside, measured.broadside * .9f, measured.broadside * ceiling);
        }
    }

    // The envelope is summed from what each live propulsor reports with its own Execute arithmetic.
    [Fact]
    public void TheEnvelopeComesFromTheGear()
    {
        var (_, longinus, _, gemini) = EvFleet();
        EvSettle(longinus);
        // A thruster's Thrust property is refreshed only while it fires: turn each way so the flank thrusters hold what
        // they push with, then read the envelope at rest.
        foreach (var look in new[] { float3(1, 0, 0), float3(-1, 0, 0) })
        {
            longinus.LookDirection = look;
            for (var step = 0; step < 20; step++) longinus.Update(EvDt);
        }
        EvSettle(longinus);
        longinus.Update(EvDt);
        var settings = _items.GameplaySettings;
        var thrusters = longinus.GetBehaviors<Thruster>().Where(t => t.Item.Active.Value).ToList();
        var largeDrives = thrusters.Where(t => t.Item.Data.Name == "Large Drive").ToList();
        var flanks = thrusters.Where(t => t.Item.Data.Name == "Talaria").ToList();
        Assert.Equal(2, largeDrives.Count);
        Assert.Equal(2, flanks.Count);

        var envelope = longinus.Envelope;
        var mass = longinus.Mass;
        Assert.InRange(envelope.Forward, 500000f / mass * .99f, 500000f / mass * 1.01f);
        Assert.Equal(0f, envelope.Reverse);
        var flankAcceleration = flanks.Max(t => t.Thrust) / mass;
        Assert.InRange(envelope.Left, flankAcceleration * .99f, flankAcceleration * 1.01f);
        Assert.InRange(envelope.Right, flankAcceleration * .99f, flankAcceleration * 1.01f);
        var clockwise = thrusters.Where(t => t.Torque > settings.TorqueFloor).Sum(t => t.Torque * t.Thrust) * settings.TorqueMultiplier / mass;
        var counterClockwise = thrusters.Where(t => t.Torque < -settings.TorqueFloor).Sum(t => -t.Torque * t.Thrust) * settings.TorqueMultiplier / mass;
        Assert.InRange(envelope.Clockwise, clockwise * .99f, clockwise * 1.01f);
        Assert.InRange(envelope.CounterClockwise, counterClockwise * .99f, counterClockwise * 1.01f);
        Assert.True(clockwise > 1f && counterClockwise > 1f, "the flank thrusters turn the Longinus");

        // Destroying one Large Drive halves the push forward.
        largeDrives[0].Item.EquippableItem.Durability = 0f;
        longinus.Update(EvDt);
        longinus.Update(EvDt);
        Assert.InRange(longinus.Envelope.Forward, envelope.Forward * .49f, envelope.Forward * .51f);

        // The LonginusX's Traction drive pushes and turns every way.
        EvSettle(gemini);
        gemini.Update(EvDt);
        var drive = gemini.Envelope;
        Assert.True(drive.Forward > 1f && drive.Reverse > 1f && drive.Left > 1f && drive.Right > 1f, $"traction: {drive.Forward} {drive.Reverse} {drive.Left} {drive.Right}");
        Assert.True(drive.Clockwise > 0f && drive.CounterClockwise > 0f);
    }

    // Which thruster pushes which way and turns which way is read from the gear, not assumed symmetric: destroy the
    // Djinni's right-pushing thrusters and the envelope loses its right push and the turn they gave one way, which
    // the ship's own flight confirms.
    [Fact]
    public void TheEnvelopeFollowsWhichThrustersAreLeft()
    {
        var (_, _, djinni, gemini) = EvFleet();
        var settings = _items.GameplaySettings;
        EvSettle(djinni);
        // Refresh every thruster's live Thrust by firing it: strafe, burn and turn each way.
        foreach (var (move, look) in new[] { (float2(1, 0), float3(0, 0, 1)), (float2(-1, 0), float3(0, 0, 1)), (float2(0, 1), float3(0, 0, 1)),
                     (float2(0, -1), float3(0, 0, 1)), (float2(0, 0), float3(1, 0, 0)), (float2(0, 0), float3(-1, 0, 0)) })
        {
            djinni.MovementDirection = move;
            djinni.LookDirection = look;
            for (var step = 0; step < 20; step++) djinni.Update(EvDt);
        }
        EvSettle(djinni);
        djinni.Update(EvDt);
        var whole = djinni.Envelope;

        // Full reverse for one update accelerates at the reverse push.
        EvSettle(djinni);
        djinni.MovementDirection = float2(0, -1);
        djinni.Update(EvDt);
        Assert.InRange(-djinni.Acceleration.y, whole.Reverse * .98f, whole.Reverse * 1.02f);

        foreach (var thruster in djinni.GetBehaviors<Thruster>().Where(t => t.Item.EquippableItem.Rotation == ItemRotation.CounterClockwise))
            thruster.Item.EquippableItem.Durability = 0f;
        EvSettle(djinni);
        djinni.Update(EvDt);
        djinni.Update(EvDt);
        var maimed = djinni.Envelope;
        Assert.Equal(0f, maimed.Right);
        Assert.InRange(maimed.Left, whole.Left * .99f, whole.Left * 1.01f);
        var live = djinni.GetBehaviors<Thruster>().Where(t => t.Item.Active.Value).ToList();
        var clockwise = live.Where(t => t.Torque > settings.TorqueFloor).Sum(t => t.Torque * t.Thrust) * settings.TorqueMultiplier / djinni.Mass;
        var counterClockwise = live.Where(t => t.Torque < -settings.TorqueFloor).Sum(t => -t.Torque * t.Thrust) * settings.TorqueMultiplier / djinni.Mass;
        Assert.True(Math.Abs(clockwise - counterClockwise) > .05f * clockwise, "fixture: the surviving thrusters turn the two ways unequally");
        Assert.InRange(maimed.Clockwise, clockwise * .99f, clockwise * 1.01f);
        Assert.InRange(maimed.CounterClockwise, counterClockwise * .99f, counterClockwise * 1.01f);
        djinni.MovementDirection = float2(1, 0);
        djinni.Update(EvDt);
        Assert.InRange(djinni.Acceleration.x, -.5f, .5f);
        djinni.MovementDirection = float2(-1, 0);
        djinni.Update(EvDt);
        Assert.True(djinni.Acceleration.x < -1f, "it still strafes left");

        // The Traction drive's turn is the rate it turns the hull at when told to turn right.
        EvSettle(gemini);
        gemini.MovementDirection = float2(0, 0);
        gemini.LookDirection = float3(1, 0, 0);
        gemini.Update(EvDt);
        var turned = Math.Atan2(gemini.Direction.x, gemini.Direction.y);
        Assert.InRange((float) turned / EvDt, gemini.Envelope.Clockwise * .9f, gemini.Envelope.Clockwise * 1.1f);
    }

    // The reach and the evasion read each side of the envelope for themselves: a forward-only ship that turns clockwise
    // fast and counter-clockwise slowly reaches further to its right than its left in the window, and its evasion
    // across a line of sight is the average of what it reaches to either side, capped by the spread it is observed to
    // make. A diagonal jink reads as unpredictable along the diagonal and not across it.
    [Fact]
    public void ReachAndEvasionReadEachSideOfTheEnvelope()
    {
        var forwardOnly = new ManoeuvreEnvelope(50, 0, 0, 0, 4, .5f);
        var ahead = float2(0, 1);
        var toRight = FireControl.Reach(forwardOnly, ahead, float2(1, 0), .75f);
        var toLeft = FireControl.Reach(forwardOnly, ahead, float2(-1, 0), .75f);
        Assert.True(toRight > 2 * toLeft && toLeft > 0f, $"right {toRight}, left {toLeft}");

        var wild = default(ManoeuvreTrack);
        for (var step = 0; step < 240; step++) wild.Observe(float2(step / 30 % 2 == 0 ? 500 : -500, 0), EvDt, .75f);
        var evasion = FireControl.Evasion(forwardOnly, ahead, wild, float2(0, 1), .75f);
        Assert.InRange(evasion, .5f * (toRight + toLeft) * .999f, .5f * (toRight + toLeft) * 1.001f);

        var diagonal = default(ManoeuvreTrack);
        for (var step = 0; step < 240; step++)
            diagonal.Observe(float2(1, 1) * (step / 30 % 2 == 0 ? 40 : -40), EvDt, .75f);
        var along = diagonal.LateralRms(normalize(float2(1, 1)));
        var across = diagonal.LateralRms(normalize(float2(1, -1)));
        Assert.InRange(along, diagonal.LateralRms(float2(1, 0)) * 1.414f * .97f, diagonal.LateralRms(float2(1, 0)) * 1.414f * 1.03f);
        Assert.True(across < .05f * along, $"along {along}, across {across}");
    }

    // A station or any other non-Ship has no envelope; whatever it does, it does not evade.
    [Fact]
    public void NonShipsDoNotEvade()
    {
        var zone = Arena(true);
        var station = zone.Entities.First(entity => !(entity is Ship));
        for (var step = 0; step < 240; step++)
            station.Manoeuvre.Observe(float2(step / 30 % 2 == 0 ? 60 : -60, 0), EvDt, _items.GameplaySettings.SolutionWindow);
        Assert.True(station.Manoeuvre.LateralRms(float2(1, 0)) > 1f, "fixture: the station's own track reads a jink");
        Assert.Equal(0f, FireControl.Evasion(station, float2(0, 1)));
        Assert.Equal(0f, FireControl.Evasion(station, float2(1, 0)));
    }

    // Entity.Update is the one writer of Acceleration and Manoeuvre: the velocity change across the base update, so
    // thrust and not drag or gravity.
    [Fact]
    public void ManoeuvreIsObserved()
    {
        var (_, longinus, djinni, gemini) = EvFleet();

        // Coasting: no acceleration, an all-zero track.
        EvSettle(longinus);
        longinus.Velocity = float2(40, 0);
        for (var step = 0; step < 60; step++) longinus.Update(EvDt);
        Assert.Equal(0f, lengthsq(longinus.Acceleration));
        Assert.Equal(0f, lengthsq(longinus.Manoeuvre.Trend) + lengthsq(longinus.Manoeuvre.Innovation) + lengthsq(longinus.Manoeuvre.Moments));

        // A full burn for one update: the acceleration is the thrust over the mass, along the heading. (The Large
        // Drives' opposed torques turn the hull a little between their two pushes, hence the sideways slack.)
        EvSettle(longinus);
        longinus.MovementDirection = float2(0, 1);
        longinus.Update(EvDt);
        Assert.InRange(longinus.Acceleration.y, longinus.Envelope.Forward * .995f, longinus.Envelope.Forward * 1.005f);
        Assert.InRange(abs(longinus.Acceleration.x), 0f, .03f * longinus.Envelope.Forward);

        // The Djinni's thrusters push harder than their construction value (M25): measured acceleration is what its
        // envelope says.
        EvSettle(djinni);
        djinni.MovementDirection = float2(0, 1);
        djinni.Update(EvDt);
        Assert.InRange(djinni.Acceleration.y, djinni.Envelope.Forward * .98f, djinni.Envelope.Forward * 1.02f);

        // Strafing at full thrust for one update on the Traction drive: the drive's own push over the step.
        EvSettle(gemini);
        gemini.MovementDirection = float2(1, 0);
        gemini.Update(EvDt);
        var drive = gemini.GetBehaviors<AetherDrive>().Single();
        Assert.True(drive.ThrustDirection.x > 0f);
        Assert.InRange(gemini.Acceleration.x, drive.ThrustDirection.x / EvDt * .999f, drive.ThrustDirection.x / EvDt * 1.001f);
        Assert.InRange(gemini.Acceleration.x, gemini.Envelope.Right * .95f, gemini.Envelope.Right * 1.05f);

        // The same jink observed at two step sizes reads the same unpredictability.
        var fine = EvSquare(40, .5f, 4f, 1f / 60f, .75f);
        var coarse = EvSquare(40, .5f, 4f, 1f / 30f, .75f);
        var n = float2(1, 0);
        Assert.InRange(coarse.LateralRms(n), fine.LateralRms(n) * .9f, fine.LateralRms(n) * 1.1f);
    }

    // A ship holding a steady burn, or dithering far faster than the window, does not evade; a jink at the window's
    // own scale does; a coasting ship evades nothing.
    [Fact]
    public void ASteadyBurnAndDitheringAreNotEvasion()
    {
        var window = _items.GameplaySettings.SolutionWindow;
        // A fresh ship for each phase: an aether drive heats and spends its rotors as it flies.
        var gemini = EvFleet().gemini;
        float Nose() => FireControl.Evasion(gemini, normalize(gemini.Direction));

        EvSettle(gemini);
        EvFly(gemini, EvJink.Lateral, .5f, 4f);
        var jinking = Nose();
        Assert.True(jinking > 1f, $"fixture: the jink evades ({jinking})");

        gemini = EvFleet().gemini;
        EvSettle(gemini);
        gemini.MovementDirection = float2(0, 1);
        for (var step = 0; step < (int) Math.Round(3 * window / EvDt); step++) gemini.Update(EvDt);
        Assert.True(Nose() < .05f * jinking, $"a steady burn: {Nose()} against {jinking}");

        gemini = EvFleet().gemini;
        EvSettle(gemini);
        EvFly(gemini, EvJink.Lateral, .05f, 4f);
        Assert.True(Nose() < .25f * jinking, $"a dither: {Nose()} against {jinking}");

        gemini = EvFleet().gemini;
        EvSettle(gemini);
        for (var step = 0; step < 240; step++) gemini.Update(EvDt);
        Assert.Equal(0f, Nose());
    }

    // Two shooters on opposite sides of the same target, 200 m away: one with a projectile gun and one with a laser
    // (a beam's authored velocity is 0, a flight time of zero). The target is the omnidirectional LonginusX.
    private sealed class EvRange
    {
        public Zone Arena;
        public Ship Gunner, Lasing, Target;
        public Weapon Gun, Laser;
    }

    private EvRange EvRangeLaunch()
    {
        Ship gunner = null, lasing = null, target = null;
        var scenario = new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            gunner = stage.Place(EvGemini(stage), float2(0, 0), facing: float2(0, 1)) as Ship;
            target = stage.Place(EvGemini(stage), float2(0, 200), facing: float2(0, 1)) as Ship;
            lasing = stage.Place(EvGemini(stage, "ColdFire"), float2(0, 400), facing: float2(0, -1)) as Ship;
        });
        var (_, arena, _, failures) = Launch(scenario, Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        foreach (var shooter in new[] { gunner, lasing })
        {
            shooter.SetTarget(target);
            shooter.EntityInfoGathered[target] = 1f;
        }
        arena.Update(1f);
        Assert.True(gunner.VisibleEntities.Contains(target) && lasing.VisibleEntities.Contains(target), "fixture: the shooters see the target");
        var gun = gunner.GetBehaviors<Weapon>().First(weapon => weapon is AutoWeapon && weapon.Velocity > 100f);
        var laser = lasing.GetBehaviors<Weapon>().First(weapon => weapon is AutoWeapon && weapon.Velocity <= .01f);
        return new EvRange { Arena = arena, Gunner = gunner, Lasing = lasing, Target = target, Gun = gun, Laser = laser };
    }

    // The target crossing at 150 m/s, either coasting or after two seconds of jinking, at the same velocity, heading
    // and place.
    private static void EvCross(EvRange range, bool jinking)
    {
        var target = range.Target;
        EvSettle(target);
        if (jinking) EvFly(target, EvJink.Lateral, .5f, 2f);
        target.Velocity = float2(150, 0);
        target.Direction = float2(0, 1);
        target.Position = float3(0, target.Position.y, 200);
    }

    // Fires a volley and returns the hits it committed and the mean price those shots were committed at.
    private static (int hits, float price) EvVolley(EvRange range, Weapon weapon, Ship shooter, int shots)
    {
        var committed = 0;
        var hits = 0;
        using var subscription = range.Arena.ShotCommitted.Subscribe(outcome => { committed++; if (outcome.Hit) hits++; });
        var ids = new List<int>();
        for (var i = 0; i < shots; i++)
        {
            var id = FireControl.Fire(weapon, weapon.Item, shooter);
            Assert.True(id > 0, "fixture: the weapon fires");
            ids.Add(id);
        }
        var price = ids
            .Select(id => range.Arena.PendingShots.Single(shot => shot.ShotId == id))
            .Average(shot => FireControl.CommitProbability(shot, range.Arena.Time, out _));
        FireControl.Step(range.Arena, .01f);
        Assert.Equal(shots, committed);
        return (hits, price);
    }

    // The evasion term is in the live price, the HUD's forecast and the roll: a ship crossing at 150 m/s that has been
    // jinking is harder to hit than the same ship coasting, for a projectile gun.
    [Fact]
    public void ACoastingAgileShipIsEasierToHitThanTheSameShipJinking()
    {
        var range = EvRangeLaunch();
        var gun = range.Gun;
        var shooter = range.Gunner;
        var target = range.Target;

        EvCross(range, jinking: false);
        var coastingDiagnostic = FireControl.Inspect(gun, shooter, target);
        var coastingPrice = FireControl.HitProbability(gun, shooter, target);
        var coasting = EvVolley(range, gun, shooter, 200);
        Assert.Equal(0f, coastingDiagnostic.Evasion);
        Assert.Equal(1f, coastingDiagnostic.PEvasion);
        Assert.True(coastingPrice > .2f, $"fixture: a coasting target is hittable ({coastingPrice})");

        EvCross(range, jinking: true);
        var jinkingDiagnostic = FireControl.Inspect(gun, shooter, target);
        var jinkingPrice = FireControl.HitProbability(gun, shooter, target);
        var jinking = EvVolley(range, gun, shooter, 200);
        Console.WriteLine($"EVASION price: coasting {coastingPrice:F3} hits {coasting.hits}/200 committed {coasting.price:F3}; " +
                          $"jinking {jinkingPrice:F3} (evasion {jinkingDiagnostic.Evasion:F2} m, tracking {jinkingDiagnostic.Tracking:F1}, PEvasion {jinkingDiagnostic.PEvasion:F3}) " +
                          $"hits {jinking.hits}/200 committed {jinking.price:F3}");

        Assert.True(jinkingDiagnostic.Evasion > 0f);
        Assert.True(jinkingDiagnostic.PEvasion < 1f);
        Assert.True(jinkingPrice < coastingPrice, "the live price falls");
        Assert.True(jinking.price < coasting.price, "the committed price falls");
        Assert.True(jinking.hits < coasting.hits, $"the committed hit rate falls: {jinking.hits} against {coasting.hits}");

        // The HUD's number stays the price: the diagnostic's base is the live price, and at fire time, with no
        // deviation yet realized, the live price is the commit price.
        Assert.Equal(jinkingPrice, jinkingDiagnostic.PBase, 6);
        var id = FireControl.Fire(gun, gun.Item, shooter);
        var shot = range.Arena.PendingShots.Single(s => s.ShotId == id);
        Assert.InRange(FireControl.CommitProbability(shot, range.Arena.Time, out _), jinkingPrice * .999f, jinkingPrice * 1.001f);
    }

    // Ruling evasion-not-only-flight-time: a laser has no flight time to dodge, and a jinking target still evades it.
    // The whole difference is the evasion term at the shooter's Tracking.
    [Fact]
    public void AnInstantWeaponFeelsTheJink()
    {
        var range = EvRangeLaunch();
        var laser = range.Laser;
        var shooter = range.Lasing;
        var target = range.Target;
        Assert.True(laser.Velocity <= .01f, "fixture: the beam has no flight time");

        (float hit, float commit, PendingShot shot) Price(bool jinking)
        {
            EvCross(range, jinking);
            var hit = FireControl.HitProbability(laser, shooter, target);
            var id = FireControl.Fire(laser, laser.Item, shooter);
            var shot = range.Arena.PendingShots.Single(s => s.ShotId == id);
            return (hit, FireControl.CommitProbability(shot, range.Arena.Time, out _), shot);
        }

        var coasting = Price(false);
        var jinking = Price(true);
        Assert.True(coasting.commit > .2f, $"fixture: a coasting target is hittable ({coasting.commit})");
        Assert.True(jinking.hit < coasting.hit, $"live: {jinking.hit} against {coasting.hit}");
        Assert.True(jinking.commit < coasting.commit, $"commit: {jinking.commit} against {coasting.commit}");

        var evasion = FireControl.Evasion(target, jinking.shot.TravelDirection);
        Assert.True(evasion > 0f);
        var pDeviation = FireControl.DeviationProbability(jinking.shot, range.Arena.Time, out var deviation);
        Assert.Equal(evasion, deviation, 4); // nothing realized: a beam commits where it fires
        var expected = FireControl.PDeviation(evasion, jinking.shot.Tracking);
        Assert.Equal(expected, pDeviation, 5);
        Assert.InRange(jinking.commit / coasting.commit, expected * .999f, expected * 1.001f);
        Assert.InRange(jinking.hit / coasting.hit, expected * .999f, expected * 1.001f);
    }

    // Finding coasting-memory: a ship that stops jinking stops evading. The track is never reset by hand here: the
    // Gemini jinks for four seconds, the input is cut, and evasion must read exactly 0 within the window plus the
    // drive's own spin-down (half a second), and stay 0, with the moments back at exactly zero.
    [Fact]
    public void AShipThatStopsJinkingStopsEvading()
    {
        var (_, _, _, gemini) = EvFleet();
        var window = _items.GameplaySettings.SolutionWindow;
        EvSettle(gemini);
        EvFly(gemini, EvJink.Lateral, 1f, 4f);
        var jinking = Math.Max(FireControl.Evasion(gemini, float2(0, 1)), FireControl.Evasion(gemini, EvRight(gemini)));
        Assert.True(jinking > 5f, $"the jink should evade, read {jinking}");

        var zeroAt = -1f;
        const float bound = 2f;
        for (var step = 1; step * EvDt <= bound + 3f; step++)
        {
            gemini.Update(EvDt);
            var nose = FireControl.Evasion(gemini, float2(0, 1));
            var side = FireControl.Evasion(gemini, EvRight(gemini));
            if (zeroAt < 0f && nose == 0f && side == 0f) zeroAt = step * EvDt;
            if (zeroAt >= 0f) Assert.True(nose == 0f && side == 0f, $"evasion came back at {step * EvDt:F2} s: {nose}, {side}");
        }
        Assert.True(zeroAt >= 0f && zeroAt <= window + .5f, $"evasion reached zero at {zeroAt:F2} s, window {window}");
        Assert.Equal(0f, gemini.Manoeuvre.Moments.x);
        Assert.Equal(0f, gemini.Manoeuvre.Moments.y);
        Assert.Equal(0f, gemini.Manoeuvre.Moments.z);
    }

    // Finding limit-clamp-evasion: a velocity limiter's clamp is the ceiling, not a change of vector. A Longinus
    // assigned four times its top speed and given no input evades nothing.
    [Fact]
    public void ASpeedAboveTheLimitIsNotEvasion()
    {
        var (_, longinus, _, _) = EvFleet();
        EvSettle(longinus);
        longinus.Velocity = float2(0, 400);
        // Read every tick: the track forgets within a window, so only the ticks right after the clamp can see it.
        for (var step = 0; step < 2 * 60; step++)
        {
            longinus.Update(EvDt);
            Assert.Equal(0f, FireControl.Evasion(longinus, float2(0, 1)));
            Assert.Equal(0f, FireControl.Evasion(longinus, EvRight(longinus)));
            Assert.Equal(0f, longinus.Manoeuvre.Moments.x);
            Assert.Equal(0f, longinus.Manoeuvre.Moments.z);
        }
    }

    // Finding thruster-thrust-cache: Ship's aggregates read what the thrusters push with now, not what they pushed
    // with at construction. The idle Djinni's forward thrust, in newtons, is its envelope's forward acceleration
    // times its mass,.
    [Fact]
    public void TheAggregatesReadLiveThrust()
    {
        var (_, _, djinni, _) = EvFleet();
        EvSettle(djinni);
        djinni.Update(EvDt);
        Assert.InRange(djinni.ForwardThrust, djinni.Envelope.Forward * djinni.Mass * .99f, djinni.Envelope.Forward * djinni.Mass * 1.01f);
        Assert.True(djinni.ForwardThrust > 0f);
        var thrusters = djinni.GetBehaviors<Thruster>().ToList();
        Assert.NotEmpty(thrusters);
        foreach (var thruster in thrusters)
        {
            var stat = ((ThrusterData) thruster.Data).Thrust;
            Assert.Equal(thruster.Evaluate(stat), thruster.Thrust);
            Assert.True(thruster.Thrust > stat.Min * 1.1f, $"idle thrust {thruster.Thrust} against the stat minimum {stat.Min}");
        }
    }
}
