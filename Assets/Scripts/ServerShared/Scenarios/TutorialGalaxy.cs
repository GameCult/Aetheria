using float2 = CultMath.float2;

// New Game as it has always been: a prelude galaxy, entered in the starting hull of the protagonist faction.
public sealed class TutorialGalaxy : Scenario
{
    public override string Name => "Tutorial Galaxy";
    public override string Brief => "A small galaxy of fixed factions, entered in a ship of the protagonist faction.";

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        var protagonist = stage.Galaxy.ResolveFaction(stage.TutorialGenerationSettings.ProtagonistFaction);
        stage.Player(stage.Generated(stage.StartingHull, protagonist), float2.zero);
    }
}
