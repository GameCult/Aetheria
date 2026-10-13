using static CultMath.math;
using static ItemRotation;

public sealed class Crossing : Scenario
{
    public override string Name => "Crossing";
    public override string Brief => "plight at the starboard gun and a FastBlast+- at the port gun, a fast Djinni crossing close ahead and an " +
                                    "unpiloted one waiting at range. The Djinni starts to port moving across the bow; the arena's gravity bends its run. Make the pass coasting: give no throttle until the Djinni has crossed the bow, then fly freely (thrusting from the start carries you past it). Verify: as the Djinni crosses close, FastBlast lands some and plight " +
                                    "almost none; against the Djinni at range both land. Fly it again without the Fire Control Array: " +
                                    "with it the small gun lands more.";
    public override uint Seed => 112;
    public override bool Ambient => false;

    // Where the Djinni sit: the player is at the origin facing +z. The crosser starts ahead and to port, facing across
    // the bow and already moving at the Djinni's 50 m/s cap, so its run sweeps across the bow within seconds (the arena
    // pulls the player to port, which brings the two together). The waiting Djinni sits inside plight's arc at 80% of
    // its Range.
    public static readonly CultMath.float2 CrosserAt = float2(-150, 200), CrosserFacing = float2(1, 0), CrosserVelocity = float2(50, 0);
    public const float PlightRange = 1216f; // plight's Range in metres, 1216.4 as the fit stands settled at rest
    public static readonly CultMath.float2 Waiting = float2(0, PlightRange * .8f);

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
            ("Lightning Projector", int2(1, 8), None),
            ("Cell Blaster", int2(4, 8), None),
            ("Radiator", int2(2, 2), CounterClockwise),
            ("Radiator", int2(3, 2), Clockwise),
            ("Compact Sensor", int2(3, 10), None),
            ("Fire Control Array", int2(1, 6), Clockwise));

        stage.Player(fighter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Generated("Djinni"), CrosserAt, facing: CrosserFacing, stance: ScenarioStance.Hostile, piloted: true, velocity: CrosserVelocity);
        stage.Place(stage.Bare("Djinni"), Waiting);
    }
}
