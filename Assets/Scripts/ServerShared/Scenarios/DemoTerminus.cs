using System.Collections.Generic;
using float2 = CultMath.float2;

// The demo: one gate, one region and one boss, with a fixed cast. The Pirates are the protagonist's ostensible
// employer, Zhestokost the antagonist whose home is the boss zone and the exit, Lucent Media and Aeronautics Unlimited
// the neutrals. A galaxy of that cast at half influence; the player, who is not a Pirate, flies the base Longinus. Its
// entrance holds two stations on every seed: Aeronautics Unlimited's Zenith trade hub and the Pirates' refinery.
public sealed class DemoTerminus : Scenario
{
    public static readonly GalaxyCast Cast = new GalaxyCast
    {
        ProtagonistFaction = "Pirates",
        AntagonistFaction = "Zhe",
        BufferFaction = "Luc",
        NeutralFactions = new[] { "Aero" },
        QuestFaction = null,
        ZoneCount = 64,
        LinkDensity = .5f,
        InfluenceScale = .5f
    };

    // Zenith (ruling demo-zenith-ore-depot) and the Pirates' processing station (ruling demo-processing-station-buys-ore),
    // named from the cast so the cast and the orders cannot drift.
    public override IReadOnlyList<EntranceStationOrder> EntranceStations { get; } = new[]
    {
        new EntranceStationOrder(Cast.NeutralFactions[0], "trade-hub"),
        new EntranceStationOrder(Cast.ProtagonistFaction, "processing")
    };

    public override string Name => "Terminus (Demo)";
    public override string Id => "demo-terminus";
    public override string Brief => "One gate, one region, one boss. Allied: the Pirates. Opposed: Zhestokost. " +
                                    "Neutral: Lucent Media and Aeronautics Unlimited.";

    public override Galaxy Generate(GalaxyStage stage)
    {
        var galaxy = stage.Cast(Cast);
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
        stage.Player(stage.Generated("Longinus"), float2.zero);
    }
}
