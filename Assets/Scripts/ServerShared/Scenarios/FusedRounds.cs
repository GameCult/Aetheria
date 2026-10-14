using static CultMath.math;
using static ItemRotation;

public sealed class FusedRounds : Scenario
{
    public override string Name => "Fused Rounds";
    public override string Brief => "The Smoke Test Hull with Smoke Proximity, Smoke Delayed and a DeathCluster; a hostile bare Longinus at mid range, a bare " +
                                    "hull dead ahead, a fragile one to starboard, a close one inside the proximity blast radius, and one astern. " +
                                    "Verify with nothing selected: aim past every hull and the round bursts at max range along the aim; aim at the hull " +
                                    "ahead and it stops and bursts on it; fire at the fragile hull and kill it with the DeathCluster before the round " +
                                    "lands: no burst. Select the astern hull: the round fires and bursts at its range along the arc edge. Select the " +
                                    "close hull: the burst is pushed out to the blast radius from your centre. A Smoke Proximity burst beside the " +
                                    "Longinus damages where the model shows the blast; a Smoke Delayed round into its nose bursts inside. The HUD " +
                                    "reads Direct, Burst at N m or Refused for the fused weapons and a percentage for the DeathCluster, and the " +
                                    "smoke weapons show no Manufacturer row.";
    public override uint Seed => 108;
    public override bool Ambient => false;

    // Where everything sits: the player is at the origin facing +z. Ahead lies on the initial aim line, inside every forward
    // mount's arc. Fragile is a bare Longinus inside the DeathCluster's effective range (full damage); Close is inside Smoke Proximity's blast radius
    // (8 m from the ship's centre) and beside the aim line, not across it; Stern is outside every mount's arc.
    public static readonly CultMath.float2 Mid = float2(-200, 600), Ahead = float2(0, 300), Fragile = float2(90, 270),
        Close = float2(6.4f, 4f), Stern = float2(0, -400);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage)
    {
        var shooter = stage.Fit("Smoke Test Hull",
            ("Cockpit 2x2", int2(4, 6), None),
            ("Large Drive", int2(3, 0), Reversed),
            ("Large Drive", int2(5, 0), Reversed),
            ("Talaria", int2(4, 14), CounterClockwise),
            ("Talaria", int2(5, 14), Clockwise),
            ("Core Power", int2(4, 4), None),
            ("Smoke Proximity", int2(3, 8), None),
            ("Smoke Delayed", int2(6, 8), None),
            ("DeathCluster", int2(5, 17), None),
            ("Iapyx", int2(4, 2), CounterClockwise),
            ("Iapyx", int2(5, 2), Clockwise),
            ("not if i see you first", int2(5, 10), None),
            ("Fire Control Array", int2(3, 6), Clockwise));

        stage.Player(shooter, float2(0, 0), facing: float2(0, 1));
        stage.Place(stage.Bare("Longinus"), Mid, stance: ScenarioStance.Hostile);
        stage.Place(stage.Bare("Longinus"), Ahead);
        stage.Place(stage.Bare("Longinus"), Fragile);
        stage.Place(stage.Bare("Longinus"), Close);
        stage.Place(stage.Bare("Longinus"), Stern);
    }
}
