using static CultMath.math;
using static ItemRotation;

public sealed class LockOn : Scenario
{
    public override string Name => "Lock On";
    public override string Brief => "A hostile closes from out of sight, finds you and locks. Verify: no warning at first; once it locks, one " +
                                    "indicator appears toward it and tightens as the lock grows; break line of sight or range and it vanishes; " +
                                    "your own ping shows no incoming paint on its side, only the lock does.";
    public override uint Seed => 1;
    public override bool Ambient => false;

    // The hostile starts about 4000 m off, ahead and to port of the player, facing the player. At that range its passive
    // sensors hold no track of the player (the test asks the sim's own PassiveInfo against the detection threshold). The
    // hostile closes, sees the player some fourteen seconds in and locks. Its flanking approach and the zone's pull carry it
    // off the staged line (about 5 degrees by the lock on the merged tree), and the warning must point at where the hostile
    // is at the lock, which the test checks against the live bearing. At Duel's 900 m (and at 600 m) it sees and locks
    // within a second.
    public static readonly CultMath.float2 HostileAt = float2(-2000, 3450);

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
            ("GT 3K", int2(0, 5), None), // Guided Missile Launcher
            ("GT 3K", int2(5, 5), None), // Guided Missile Launcher
            ("FastBlast+-", int2(1, 8), None), // Cell Blaster
            ("FastBlast+-", int2(4, 8), None), // Cell Blaster
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None), // Compact Sensor
            ("Capacitor", int2(1, 5), None),
            ("Panopticon Prime", int2(1, 6), Clockwise), // Fire Control Array
            ("Store-All Plus", int2(2, 8), None)); // Cargo Bay

        stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        stage.Place(fighter, HostileAt, facing: normalize(-HostileAt), stance: ScenarioStance.Hostile, piloted: true);
    }
}
