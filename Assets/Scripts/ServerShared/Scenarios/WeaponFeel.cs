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
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Talaria", int2(2, 14), CounterClockwise),
            ("Talaria", int2(3, 14), Clockwise),
            ("Core Power", int2(2, 4), None),
            ("ChargeBlast SG", int2(1, 17), None),
            ("Autocannon", int2(3, 17), None),
            ("FastBlast+-", int2(1, 8), None),
            ("Iapyx", int2(2, 2), CounterClockwise),
            ("Iapyx", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(1, 6), Clockwise));

        stage.Player(shooter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("Longinus"), Target);
    }
}
