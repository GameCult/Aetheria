using static CultMath.math;
using static ItemRotation;

public sealed class WeaponFeel : Scenario
{
    public override string Name => "Weapon Feel";
    public override string Brief => "The Smoke Test Hull with a ChargeBlast SG, an Autocannon and a FastBlast+- burst weapon; a bare Longinus dead ahead. " +
                                    "Verify: ChargeBlast SG and Autocannon fire with effects, particles and action-bar icons; the burst weapon's " +
                                    "cadence is as before; a fast release-and-press on the ChargeBlast SG starts no second charge.";
    public override uint Seed => 107;
    public override bool Ambient => false;

    // The one target, ahead of the nose: inside every forward mount's arc.
    public static readonly CultMath.float2 Target = float2(0, 400);

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
            ("Charged Scatter Blaster", int2(3, 17), None),
            ("Autocannon", int2(5, 17), None),
            ("Cell Blaster", int2(3, 8), None),
            ("Radiator", int2(4, 2), CounterClockwise),
            ("Radiator", int2(5, 2), Clockwise),
            ("Compact Sensor", int2(5, 10), None),
            ("Fire Control Array", int2(3, 6), Clockwise));

        stage.Player(shooter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("Longinus"), Target);
    }
}
