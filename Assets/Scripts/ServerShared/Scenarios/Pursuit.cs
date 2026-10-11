using static CultMath.math;
using static ItemRotation;

public sealed class Pursuit : Scenario
{
    // The pursuer starts astern, inside the 400 at which VapourCloudTests.ACloudBreaksALockAtRange shows a fresh vent
    // cloud breaking a sensor's lock, and well outside the 40 at which AStrongCloseSensorBurnsThrough shows a close
    // sensor burning through.
    private const float Astern = 300;

    public override string Name => "Pursuit";
    public override string Brief => "A hostile Longinus on your tail; a Mine Launcher fitted, three Coolant Vents and two Thruster " +
                                    "Overdrives aboard. Verify: lay mines in its path, the mine pulses slowly after 2 s, fast when " +
                                    "the hostile is near, explodes 2 s later; vent and coast to lose it; overdrive away.";
    public override uint Seed => 111;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        var layer = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Talaria", int2(2, 14), CounterClockwise),
            ("Talaria", int2(3, 14), Clockwise),
            ("Core Power", int2(2, 4), None),
            ("Mine Launcher", int2(0, 5), None),
            ("pswarm", int2(5, 5), None),
            ("FastBlast+-", int2(1, 8), None),
            ("FastBlast+-", int2(4, 8), None),
            ("Iapyx", int2(2, 2), CounterClockwise),
            ("Iapyx", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None),
            ("Fire Control Array", int2(1, 6), Clockwise),
            ("Store-All Plus", int2(2, 8), None));
        var pursuer = stage.Fit("Longinus",
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
            ("PotaT+-", int2(1, 5), None),
            ("Fire Control Array", int2(1, 6), Clockwise),
            ("Store-All Plus", int2(2, 8), None));

        var player = stage.Player(layer, float2(0, 0), facing: float2(0, 1));
        stage.Cargo(player, "Coolant Vent", "Coolant Vent", "Coolant Vent", "Thruster Overdrive", "Thruster Overdrive");
        stage.Place(pursuer, float2(0, -Astern), facing: float2(0, 1), stance: ScenarioStance.Hostile, piloted: true);
    }
}
