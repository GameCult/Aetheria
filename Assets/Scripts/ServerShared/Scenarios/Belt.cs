using System.Linq;
using static CultMath.math;
using static ItemRotation;

public sealed class Belt : Scenario
{
    public override string Name => "Belt";
    public override string Brief => "The biggest asteroid belt the generator made, with the Longinus parked 100 m outside its ring, facing along it, " +
                                    "and nothing else in the arena. Verify: next and previous target step through the rocks and the reticle sweeps " +
                                    "across the belt without a hitch; Previous with nothing targeted takes the farthest rock; the target indicator " +
                                    "sits on the rock it names.";

    // Seeds 1-90 scanned for the arena whose entrance zone holds the belt with the most asteroids once the generator
    // placed belts by their parent's window (children-within-reach): seed 86, a belt of 484 rocks. The next four:
    // 55 (478), 83 (402), 64 (383), 63 (323). 37 of the 90 arenas hold no belt.
    public override uint Seed => 86;
    public override bool Ambient => false;

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
            ("DragOnBreath", int2(1, 8), None), // Lightning Projector
            ("FastBlast+-", int2(4, 8), None), // Cell Blaster
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("not if i see you first", int2(3, 10), None), // Compact Sensor
            ("Panopticon Prime", int2(1, 6), Clockwise)); // Fire Control Array

        // The belt is a ring about its parent's position at the belt orbit's distance. The player stands 100 m outside
        // it, at the point nearest the origin, facing along the ring.
        var arena = stage.Arena;
        var belt = arena.Planets.Values.OfType<AsteroidBeltData>().OrderByDescending(candidate => candidate.Asteroids.Length).First();
        var orbit = arena.Orbits[belt.Orbit.Key].Data;
        var centre = arena.GetOrbitPosition(orbit.Parent.Key);
        var outward = lengthsq(centre) > 0f ? normalize(-centre) : float2(1, 0);
        var at = centre + outward * (orbit.Distance + 100f);
        stage.Player(fighter, at, facing: float2(-outward.y, outward.x));
    }
}
