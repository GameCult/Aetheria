using static CultMath.math;
using static ItemRotation;

public sealed class Arcs : Scenario
{
    public override string Name => "Arcs";
    public override string Brief => "Forward mounts, a bare hull off the bow, one off the stern, a hostile turret. Verify: the bow " +
                                    "hull can be hit, nothing fires at the stern one through the ship, the turret tracks you all the way round.";
    public override uint Seed => 103;
    public override bool Ambient => false;

    // Where the targets sit: the player is at the origin facing +z, and every mount fires forward.
    public static readonly CultMath.float2 Bow = float2(0, 400), Stern = float2(0, -400), Turret = float2(600, 0);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var fighter = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Talaria", int2(2, 14), CounterClockwise),
            ("Talaria", int2(3, 14), Clockwise),
            ("Core Power", int2(2, 4), None),
            ("GT 3K", int2(0, 5), None),
            ("GT 3K", int2(5, 5), None),
            ("FastBlast+-", int2(1, 8), None),
            ("FastBlast+-", int2(4, 8), None),
            ("Iapyx", int2(2, 2), CounterClockwise),
            ("Iapyx", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(0, 4), None));
        var turret = stage.Fit("Turret",
            ("ClearPath", int2(1, 2), None),
            ("ClearPath", int2(5, 2), None),
            ("Skiron", int2(0, 2), CounterClockwise),
            ("Skiron", int2(7, 2), Clockwise),
            ("Core Power", int2(3, 3), None),
            ("Turret Control Module", int2(3, 5), None),
            ("not if i see you first", int2(4, 5), None),
            ("Fire Control Array", int2(3, 6), None));

        stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("Longinus"), Bow);
        stage.Place(stage.Bare("Longinus"), Stern);
        stage.Place(turret, Turret, stance: ScenarioStance.Hostile);
    }
}
