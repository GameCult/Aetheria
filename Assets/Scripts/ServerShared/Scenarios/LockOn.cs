using static CultMath.math;
using static ItemRotation;

public sealed class LockOn : Scenario
{
    public override string Name => "Lock On";
    public override string Brief => "A hostile closes from out of sight, finds you and locks. Verify: no warning at first; once it locks, one " +
                                    "indicator appears toward it and tightens as the lock grows; break line of sight or range and it vanishes; " +
                                    "your own ping shows no incoming paint on its side, only the lock does.";
    public override uint Seed => 113;
    public override bool Ambient => false;

    // The hostile starts about 4000 m off, to the side of the player's axis, facing the player. At that range its passive
    // sensors hold no track of the player (the test asks the sim's own PassiveInfo against the detection threshold); the
    // player sees the hostile a little sooner, at about tick 20. The hostile closes, sees the player some eight seconds in,
    // and locks two ticks later. At Duel's 900 m (and at 600 m) it sees and locks within a second.
    public static readonly CultMath.float2 HostileAt = float2(1500, 3700);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        var fighter = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Rugged Drive", int2(2, 14), CounterClockwise),
            ("Rugged Drive", int2(3, 14), Clockwise),
            ("Compact Reactor", int2(2, 4), None),
            ("Guided Missile Launcher", int2(0, 5), None),
            ("Guided Missile Launcher", int2(5, 5), None),
            ("Cell Blaster", int2(1, 8), None),
            ("Cell Blaster", int2(4, 8), None),
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("Compact Sensor", int2(3, 10), None),
            ("Capacitor", int2(1, 5), None),
            ("Fire Control Array", int2(1, 6), Clockwise),
            ("Cargo Bay", int2(2, 8), None));

        stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        stage.Place(fighter, HostileAt, facing: normalize(-HostileAt), stance: ScenarioStance.Hostile, piloted: true);
    }
}
