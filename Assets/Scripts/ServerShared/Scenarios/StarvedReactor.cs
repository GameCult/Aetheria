using static CultMath.math;
using static ItemRotation;

public sealed class StarvedReactor : Scenario
{
    public override string Name => "Starved Reactor";
    public override string Brief => "Two Spectras on a MoveOnPro, which can fall to a quarter of Core Power's charge, and a bare hull " +
                                    "to shoot. Verify: starvation reads as starvation, brownout degrades, refills stutter.";
    public override uint Seed => 102;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var starved = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Talaria", int2(2, 14), CounterClockwise),
            ("Talaria", int2(3, 14), Clockwise),
            ("MoveOnPro", int2(2, 4), None),
            ("Spectra", int2(1, 8), None),
            ("Spectra", int2(4, 8), None),
            ("Iapyx", int2(2, 2), CounterClockwise),
            ("Iapyx", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(0, 4), None));
        stage.Player(starved, float2(0, 0));
        stage.Place(stage.Bare("Longinus"), float2(0, 400));
    }
}
