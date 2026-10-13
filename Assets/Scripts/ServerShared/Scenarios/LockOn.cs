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

    // The hostile starts far beyond the player's sight and the hostile's own, 4000 m ahead facing the player. Stepping the
    // arena headlessly, it closes to about 3000 m, sees the player after some eight seconds, and locks two ticks later;
    // at Duel's 900 m (and at 600 m) it sees and locks within a second.
    public static readonly CultMath.float2 HostileAt = float2(0, 4000);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

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
            ("PotaT+-", int2(1, 5), None),
            ("Fire Control Array", int2(1, 6), Clockwise),
            ("Store-All Plus", int2(2, 8), None));

        stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        stage.Place(fighter, HostileAt, facing: float2(0, -1), stance: ScenarioStance.Hostile, piloted: true);
    }
}
