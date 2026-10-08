using static CultMath.math;
using static ItemRotation;

public sealed class Duel : Scenario
{
    public override string Name => "Duel";
    public override string Brief => "One piloted, hostile Longinus with slow GT 3K missiles and a Spectra in its hold. Verify: rolled " +
                                    "hits and near-misses, subsystem aim, beam reads above bow, armour takes the missile, loot keeps its brand.";
    public override uint Seed => 104;
    public override bool Ambient => false;

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
            ("PotaT+-", int2(1, 5), None),
            ("Fire Control Array", int2(0, 4), None),
            ("Store-All Plus", int2(2, 8), None));

        stage.Player(fighter, float2(0, 0));
        var rival = stage.Place(fighter, float2(0, 900), facing: float2(0, -1), stance: ScenarioStance.Hostile, piloted: true);
        stage.Cargo(rival, "Spectra");
    }
}
