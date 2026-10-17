using static CultMath.math;
using static ItemRotation;

public sealed class Crossing : Scenario
{
    public override string Name => "Crossing";
    public override string Brief => "A Lightning Projector at the starboard gun and a Cell Blaster at the port gun, an unpiloted Djinni coasting across the bow " +
                                    "close ahead and an unpiloted one waiting at range. The crosser is launched to port at 50 m/s and only coasts: nothing steers it, " +
                                    "its hull drag slows it to about 25 m/s at the bow line, and it does not shoot back. Make the pass coasting: give no throttle until the Djinni has crossed the bow, then fly freely " +
                                    "(thrusting from the start carries you past it). Verify: as the Djinni crosses close, FastBlast lands some and projector " +
                                    "almost none; against the Djinni at range both land. Fly it again without the Fire Control Array: " +
                                    "with it the small gun lands more.";
    public override uint Seed => 112;
    public override bool Ambient => false;

    // Where the Djinni sit: the player is at Origin facing +z. Origin is 250 m or more from every body, where the zone's
    // pull is weakest (about 1.4 u/s^2; the sum of the wells never drops to nothing, and at the old origin the player sat on the star).
    // The crosser is unpiloted and placed moving at the Djinni's 50 m/s cap, so its pass is ballistic: bent by the field and slowed
    // by hull drag (it reaches the bow line at about 25 m/s relative to the player, 3.6 s in), and nothing steers it.
    // CrosserAt is the centre of the start window in which the pass holds: shifting it by 10 m along either axis keeps the pass
    // inside the test's time and distance windows (CrossingHoldsAcrossTheStartWindow).
    // The waiting Djinni sits inside the Lightning Projector's arc at 80% of its Range.
    public static readonly CultMath.float2 Origin = float2(724, 124);
    public static readonly CultMath.float2 CrosserAt = Origin + float2(-128, 45), CrosserFacing = float2(1, 0), CrosserVelocity = float2(50, 0);
    // The fighter is staged at this quality, so its gear and the range below do not ride the item stream.
    public const float StagedQuality = .6f;
    // The Lightning Projector's Range in metres at the staged fit: 996.99 after the three seconds the test settles for
    // (EvSettle); it falls toward 864 m as the projector heats. The fit as it stood at 0f5def17 (unstaged, its draws on the
    // item stream) measured 1216.4.
    public const float ProjectorRange = 997f;
    public static readonly CultMath.float2 Waiting = Origin + float2(0, ProjectorRange * .8f);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        var fighter = stage.Fit("Longinus", StagedQuality,
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

        stage.Player(fighter, Origin, facing: float2(0, 1));
        stage.Place(stage.Generated("Djinni"), CrosserAt, facing: CrosserFacing, stance: ScenarioStance.Hostile, velocity: CrosserVelocity);
        stage.Place(stage.Bare("Djinni"), Waiting);
    }
}
