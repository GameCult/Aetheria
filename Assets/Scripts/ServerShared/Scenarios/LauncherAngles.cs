using static CultMath.math;
using static ItemRotation;

public sealed class LauncherAngles : Scenario
{
    public override string Name => "Launcher Angles";
    public override string Brief => "A GT 3K, a pswarm and two FastBlasts; a hostile LonginusX broadside on, a bare Longinus beside it. " +
                                    "Verify: hits pulse only the facing edge; a pswarm round whose target dies fades unexploded.";
    public override uint Seed => 105;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var launcher = stage.Fit("LonginusX",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Traction", int2(2, 3), None),
            ("Core Power", int2(2, 1), None),
            ("GT 3K", int2(0, 5), None),
            ("pswarm", int2(5, 5), None),
            ("FastBlast+-", int2(1, 8), None),
            ("FastBlast+-", int2(4, 8), None),
            ("Iapyx", int2(1, 2), CounterClockwise),
            ("Iapyx", int2(4, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(2, 5), None));

        stage.Player(launcher, float2(0, 0));
        stage.Place(stage.Bare("LonginusX"), float2(0, 900), facing: float2(1, 0), stance: ScenarioStance.Hostile);
        stage.Place(stage.Bare("Longinus"), float2(300, 700));
    }
}
