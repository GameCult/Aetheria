using static CultMath.math;
using static ItemRotation;

public sealed class Duel : Scenario
{
    public override string Name => "Duel";
    public override string Brief => "One piloted, hostile Longinus with slow guided missiles and a Light Laser in its hold. Verify: rolled " +
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

        stage.Player(fighter, float2(0, 0));
        var rival = stage.Place(fighter, float2(0, 900), facing: float2(0, -1), stance: ScenarioStance.Hostile, piloted: true);
        stage.Cargo(rival, "Spectra");
    }
}
