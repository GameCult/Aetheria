using static CultMath.math;
using static ItemRotation;

public sealed class Broadside : Scenario
{
    public override string Name => "Broadside";
    public override string Brief => "A hull with a port gun and a starboard gun in separate groups, bare hulls abeam to port and starboard and one ahead. " +
                                    "Verify: designate the starboard hull, aim ahead and fire the starboard group: it fires on the hull without the aim. " +
                                    "Fire the port group: it holds, because nothing bears and the aim is outside its arc. Undesignate and aim to port: " +
                                    "the port group fires free along the aim.";
    public override uint Seed => 104;
    public override bool Ambient => false;

    // Where the targets sit: the player is at the origin facing +z.
    public static readonly CultMath.float2 Port = float2(-400, 0), Starboard = float2(400, 0), Ahead = float2(0, 400);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var broadside = stage.Fit("LonginusX",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Traction", int2(2, 3), None),
            ("Core Power", int2(2, 1), None),
            ("GT 3K", int2(0, 5), CounterClockwise),
            ("FastBlast+-", int2(4, 8), Clockwise),
            ("Iapyx", int2(1, 2), CounterClockwise),
            ("Iapyx", int2(4, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(2, 5), None));

        stage.Player(broadside, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("LonginusX"), Port);
        stage.Place(stage.Bare("LonginusX"), Starboard);
        stage.Place(stage.Bare("LonginusX"), Ahead);
    }
}
