using static CultMath.math;
using static ItemRotation;

public sealed class AiFusedDiscipline : Scenario
{
    public override string Name => "AI Fused Discipline";
    public override string Brief => "A Djinni; a piloted neutral Longinus carrying a Smoke Proximity and a piloted hostile Longinus whose only weapon is " +
                                    "Smoke Refused. Verify: the hostile closes on you and never fires its refused weapon; the neutral never fires its " +
                                    "fused one.";
    public override uint Seed => 110;
    public override bool Ambient => false;

    public static readonly CultMath.float2 Hostile = float2(0, 700), Neutral = float2(500, 300);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        ScenarioFit Armed(string weapon) => stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Rugged Drive", int2(2, 14), CounterClockwise),
            ("Rugged Drive", int2(3, 14), Clockwise),
            ("Compact Reactor", int2(2, 4), None),
            (weapon, int2(1, 8), None),
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("Compact Sensor", int2(3, 10), None),
            ("Fire Control Array", int2(1, 6), Clockwise));

        stage.Player(stage.Generated("Djinni"), float2(0, 0));
        stage.Place(Armed("Smoke Refused"), Hostile, facing: float2(0, -1), stance: ScenarioStance.Hostile, piloted: true);
        stage.Place(Armed("Smoke Proximity"), Neutral, facing: float2(-1, 0), piloted: true);
    }
}
