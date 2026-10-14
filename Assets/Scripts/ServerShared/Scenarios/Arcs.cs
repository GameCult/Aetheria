using static CultMath.math;
using static ItemRotation;

public sealed class Arcs : Scenario
{
    public override string Name => "Arcs";
    public override string Brief => "The Smoke Test Hull with forward mounts and a side mount on each flank; a bare hull off the bow, one off " +
                                    "the stern, one abeam to port, one abeam to starboard, and a hostile turret. Verify: the hull keeps its " +
                                    "heading while you aim at the stern hull, and the forward guns hold. Designate the bow hull and fire with the " +
                                    "aim anywhere: the forward guns fire on it and hit. The side guns fire at their beam hull and never through " +
                                    "the hull. Designate the starboard hull, aim ahead and fire its group: it fires on the hull without the aim " +
                                    "and the port group holds; undesignate and aim to port: the port group fires free. The turret tracks you " +
                                    "all the way round.";
    public override uint Seed => 103;
    public override bool Ambient => false;

    // Where the targets sit: the player is at the origin facing +z. The forward mounts fire forward, the starboard mount
    // (Clockwise) along +x and the port mount (CounterClockwise) along -x.
    public static readonly CultMath.float2 Bow = float2(0, 400), Stern = float2(0, -400), Turret = float2(600, 300),
        Port = float2(-400, 0), Starboard = float2(400, 0);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var fighter = stage.Fit("Smoke Test Hull",
            ("Cockpit 2x2", int2(4, 6), None),
            ("Large Drive", int2(3, 0), Reversed),
            ("Large Drive", int2(5, 0), Reversed),
            ("Rugged Drive", int2(4, 14), CounterClockwise),
            ("Rugged Drive", int2(5, 14), Clockwise),
            ("Compact Reactor", int2(4, 4), None),
            ("Guided Missile Launcher", int2(2, 5), None),
            ("Guided Missile Launcher", int2(7, 5), None),
            ("Cell Blaster", int2(3, 8), None),
            ("Cell Blaster", int2(6, 8), None),
            ("Radiator", int2(4, 2), CounterClockwise),
            ("Radiator", int2(5, 2), Clockwise),
            ("Compact Sensor", int2(5, 10), None),
            ("Fire Control Array", int2(3, 6), Clockwise),
            ("Autocannon", int2(8, 4), Clockwise),
            ("Charged Scatter Blaster", int2(0, 4), CounterClockwise));
        var turret = stage.Fit("Turret",
            ("Light Machine Gun", int2(1, 2), None),
            ("Light Machine Gun", int2(5, 2), None),
            ("Long Radiator", int2(0, 2), CounterClockwise),
            ("Long Radiator", int2(7, 2), Clockwise),
            ("Compact Reactor", int2(3, 3), None),
            ("Turret Control Module", int2(3, 5), None),
            ("Compact Sensor", int2(4, 5), None),
            ("Fire Control Array", int2(3, 6), None));

        stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("Longinus"), Bow);
        stage.Place(stage.Bare("Longinus"), Stern);
        stage.Place(turret, Turret, stance: ScenarioStance.Hostile);
        stage.Place(stage.Bare("Longinus"), Port);
        stage.Place(stage.Bare("Longinus"), Starboard);
    }
}
