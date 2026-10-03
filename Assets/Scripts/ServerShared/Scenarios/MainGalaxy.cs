using float2 = CultMath.float2;

// The full galaxy: factions drawn from the catalog, bosses on the road to the exit, entered in the starting hull.
public sealed class MainGalaxy : Scenario
{
    public override string Name => "Main Galaxy";
    public override string Brief => "The full galaxy: factions drawn at random, bosses guarding the road to the exit.";

    public override Galaxy Generate(GalaxyStage stage) => stage.Main();

    public override void Stage(ScenarioStage stage) =>
        stage.Player(stage.Generated(stage.StartingHull), float2.zero);
}
