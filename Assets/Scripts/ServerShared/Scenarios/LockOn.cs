using static CultMath.math;
using static ItemRotation;

public sealed class LockOn : Scenario
{
    public override string Name => "Lock On";
    public override string Id => "lock-on";
    public override string Brief => "A hostile closes from out of sight, finds you and locks. Verify: no warning at first; once it locks, one " +
                                    "indicator appears toward it and tightens as the lock grows; break line of sight or range and it vanishes; " +
                                    "your own ping shows no incoming paint on its side, only the lock does.";
    public override uint Seed => 1;
    public override bool Ambient => false;

    // The hostile starts about 4000 m off, ahead and to port of the player, facing the player. At that range its passive
    // sensors hold no track of the player (the test asks the sim's own PassiveInfo against the detection threshold). The
    // hostile is ordered to close on the player, finds it and locks. The zone's pull carries it off the staged line, so the
    // warning must point at where the hostile was when the paint was stamped, which the test checks against the poses of
    // that tick. At Duel's 900 m (and at 600 m) it sees and locks within a second.
    public static readonly CultMath.float2 HostileAt = float2(-2000, 3450);

    // The fit's lots are minted at this quality, the one Crossing and the other RunStart scenarios stage (the brief states no
    // other), and the hostile's order is to close on the player, so the lock follows from the staged situation and not
    // from item draws or the pilot's drawn circuit (ruling scenario-stages-hostile-task).
    const float StagedQuality = .6f;

    private readonly CultMath.float2 _hostileAt;
    private readonly float _quality;

    public LockOn() : this(HostileAt, StagedQuality) { }

    // The staged inputs, open to a test that moves each a little and asks that the scenario still hold.
    internal LockOn(CultMath.float2 hostileAt, float quality)
    {
        _hostileAt = hostileAt;
        _quality = quality;
    }

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        var fighter = stage.Fit("Longinus", _quality,
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

        var player = stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        var hostile = stage.Place(fighter, _hostileAt, facing: normalize(-_hostileAt), stance: ScenarioStance.Hostile, piloted: true);
        stage.Approach(hostile, player);
    }
}
