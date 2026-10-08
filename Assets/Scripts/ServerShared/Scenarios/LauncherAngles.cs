using static CultMath.math;
using static ItemRotation;

public sealed class LauncherAngles : Scenario
{
    public override string Name => "Launcher Angles";
    public override string Brief => "A GT 3K, a pswarm and two FastBlasts; a hostile bare Longinus broadside on, another beside it. " +
                                    "Verify: hits pulse only the facing edge; a pswarm round whose target dies fades unexploded.";
    public override uint Seed => 105;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var launcher = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Talaria", int2(2, 14), CounterClockwise),
            ("Talaria", int2(3, 14), Clockwise),
            ("Core Power", int2(2, 4), None),
            ("GT 3K", int2(0, 5), None),
            ("pswarm", int2(5, 5), None),
            ("FastBlast+-", int2(1, 8), None),
            ("FastBlast+-", int2(4, 8), None),
            ("Iapyx", int2(2, 2), CounterClockwise),
            ("Iapyx", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(0, 4), None));

        stage.Player(launcher, float2(0, 0));
        stage.Place(stage.Bare("Longinus"), float2(0, 900), facing: float2(1, 0), stance: ScenarioStance.Hostile);
        stage.Place(stage.Bare("Longinus"), float2(300, 700));
    }
}
