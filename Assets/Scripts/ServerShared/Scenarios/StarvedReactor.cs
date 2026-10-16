using static CultMath.math;
using static ItemRotation;

public sealed class StarvedReactor : Scenario
{
    public override string Name => "Starved Reactor";
    public override string Brief => "Two Light Lasers on a Muon Fusion Reactor, which can fall to a quarter of the Compact Reactor's charge, and a bare hull " +
                                    "to shoot. Verify: starvation reads as starvation, brownout degrades, refills stutter.";
    public override uint Seed => 102;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var starved = stage.Fit("Longinus",
            ("Cockpit 2x2", int2(2, 6), None),
            ("Large Drive", int2(1, 0), Reversed),
            ("Large Drive", int2(3, 0), Reversed),
            ("Rugged Drive", int2(2, 14), CounterClockwise),
            ("Rugged Drive", int2(3, 14), Clockwise),
            ("Muon Fusion Reactor", int2(2, 4), None),
            ("Spectra", int2(1, 8), None), // Light Laser
            ("Spectra", int2(4, 8), None), // Light Laser
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None), // Compact Sensor
            ("Panopticon Prime", int2(1, 6), Clockwise)); // Fire Control Array
        stage.Player(starved, float2(0, 0));
        stage.Place(stage.Bare("Longinus"), float2(0, 400));
    }
}
