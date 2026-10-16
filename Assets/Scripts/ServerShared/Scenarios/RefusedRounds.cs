using static CultMath.math;
using static ItemRotation;

public sealed class RefusedRounds : Scenario
{
    public override string Name => "Refused Rounds";
    public override string Brief => "The Smoke Test Hull with Smoke Refused, Smoke Refused Beam, Smoke Refused Charge and an Autocannon; a bare Longinus " +
                                    "dead ahead. Each smoke weapon's blast radius is wider than its range, so it can never be fired. Verify: pull each " +
                                    "trigger and a refused shot costs no ammo, energy, heat, sound, visibility or cooldown, the beam never starts and " +
                                    "the charged weapon never starts charging; the Autocannon fires normally.";
    public override uint Seed => 109;
    public override bool Ambient => false;

    // The one target, ahead of the nose: inside every forward mount's arc.
    public static readonly CultMath.float2 Target = float2(0, 300);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        var shooter = stage.Fit("Smoke Test Hull",
            ("Cockpit 2x2", int2(4, 6), None),
            ("Large Drive", int2(3, 0), Reversed),
            ("Large Drive", int2(5, 0), Reversed),
            ("Rugged Drive", int2(4, 14), CounterClockwise),
            ("Rugged Drive", int2(5, 14), Clockwise),
            ("Compact Reactor", int2(4, 4), None),
            ("Smoke Refused", int2(3, 8), None),
            ("Smoke Refused Beam", int2(6, 8), None),
            ("Smoke Refused Charge", int2(3, 17), None),
            ("Autocannon", int2(5, 17), None),
            ("Radiator", int2(4, 2), CounterClockwise),
            ("Radiator", int2(5, 2), Clockwise),
            ("Compact Sensor", int2(5, 10), None),
            ("Fire Control Array", int2(3, 6), Clockwise));

        stage.Player(shooter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("Longinus"), Target);
    }
}
