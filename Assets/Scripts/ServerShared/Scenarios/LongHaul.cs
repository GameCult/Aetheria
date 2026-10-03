using float2 = CultMath.float2;

public sealed class LongHaul : Scenario
{
    public override string Name => "Long Haul";
    public override string Brief => "A standard ship in a populated galaxy. Verify: memory stays flat across zones with kills; " +
                                    "warp, quit and Continue keep items, tier and brand; death disables Continue.";
    public override uint Seed => 106;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage) =>
        stage.Player(stage.Generated(stage.StartingHull), float2.zero);
}
