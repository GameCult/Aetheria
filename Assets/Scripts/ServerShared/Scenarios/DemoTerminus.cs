using float2 = CultMath.float2;

// The demo: one gate, one region and one boss, with a fixed cast. The Pirates are the protagonist's ostensible
// employer, Zhestokost the antagonist whose home is the boss zone and the exit, Lucent Media and Aeronautics Unlimited
// the neutrals. A prelude galaxy of that cast, entered in a Pirates-made hull.
public sealed class DemoTerminus : Scenario
{
    public static readonly TutorialGenerationSettings Cast = new TutorialGenerationSettings
    {
        ProtagonistFaction = "Pirates",
        AntagonistFaction = "Zhe",
        BufferFaction = "Luc",
        NeutralFactions = new[] { "Aero" },
        QuestFaction = null,
        ZoneCount = 64,
        LinkDensity = .5f
    };

    public override string Name => "Terminus (Demo)";
    public override string Brief => "One gate, one region, one boss. Allied: the Pirates. Opposed: Zhestokost. " +
                                    "Neutral: Lucent Media and Aeronautics Unlimited.";

    public override Galaxy Generate(GalaxyStage stage)
    {
        var galaxy = stage.Prelude(Cast);
        galaxy.PlaceGate(galaxy.ResolveFaction(Cast.AntagonistFaction));
        return galaxy;
    }

    public override void Stage(ScenarioStage stage)
    {
        var galaxy = stage.Galaxy;
        var pirates = galaxy.ResolveFaction(Cast.ProtagonistFaction);
        galaxy.FactionRelationships[pirates] = FactionRelationship.Friendly;
        galaxy.FactionRelationships[galaxy.ResolveFaction(Cast.AntagonistFaction)] = FactionRelationship.Hated;
        galaxy.FactionRelationships[galaxy.ResolveFaction(Cast.BufferFaction)] = FactionRelationship.Neutral;
        foreach (var neutral in Cast.NeutralFactions)
            galaxy.FactionRelationships[galaxy.ResolveFaction(neutral)] = FactionRelationship.Neutral;
        stage.Player(stage.Generated(stage.StartingHull, pirates), float2.zero);
    }
}
