using static CultMath.math;
using static ItemRotation;

public sealed class LauncherAngles : Scenario
{
    public override string Name => "Launcher Angles";
    public override string Brief => "A Guided Missile Launcher, a Swarm Rocket Pod and two Cell Blasters; a hostile bare Longinus broadside on, another beside it. " +
                                    "Verify: hits pulse only the facing edge; a Swarm Rocket Pod round whose target dies fades unexploded.";
    public override uint Seed => 105;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var launcher = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Rugged Drive", int2(2, 14), CounterClockwise),
            ("Rugged Drive", int2(3, 14), Clockwise),
            ("Compact Reactor", int2(2, 4), None),
            ("GT 3K", int2(0, 5), None), // Guided Missile Launcher
            ("Leonid", int2(5, 5), None), // Swarm Rocket Pod
            ("FastBlast+-", int2(1, 8), None), // Cell Blaster
            ("FastBlast+-", int2(4, 8), None), // Cell Blaster
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None), // Compact Sensor
            ("Panopticon Prime", int2(1, 6), Clockwise)); // Fire Control Array

        stage.Player(launcher, float2(0, 0));
        stage.Place(stage.Bare("Longinus"), float2(0, 900), facing: float2(1, 0), stance: ScenarioStance.Hostile);
        stage.Place(stage.Bare("Longinus"), float2(300, 700));
    }
}
