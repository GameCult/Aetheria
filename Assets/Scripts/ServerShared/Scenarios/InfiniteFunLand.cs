using static CultMath.math;

// Nibu's private simulation (ruling terminus-boots-into-ifl): New Game boots it, nothing in it is saved, and its
// wormhole asks to leave and starts a Terminus run (ruling ifl-wormhole-exit-standin). It stages the Longinus until
// the Nibu stand-in hull exists (ruling nibu-demo-hull-agent-standin); swapping the hull is the one line below.
public sealed class InfiniteFunLand : Scenario
{
    public override string Name => "Infinite Fun Land";
    public override string Id => "infinite-fun-land";
    public override string Brief => "Nibu's private simulation. Nothing in it is saved; leaving it starts a Terminus run.";
    public override uint Seed => 7;
    public override bool Ambient => false;
    public override bool Saved => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Cast(DemoTerminus.Cast);

    public override void Stage(ScenarioStage stage) => stage.Player(stage.Generated("Longinus"), float2(0, 0));
}
