using System;
using System.Collections.Generic;
using System.Linq;
using Random = CultMath.Random;

// The owner of a new run (docs/scenarios-cut.md, R.4). Every new run is a scenario: RunStart generates its galaxy,
// generates its arena (the galaxy's entrance zone) under the scenario's Ambient rule, and admits what the scenario
// stages through Zone.Admit, all of it or none of it. A scenario only declares; nothing else generates a new run's
// galaxy, generates or admits its player, or chooses its hull or faction. It holds no Unity type.
public static class RunStart
{
    public sealed class Staged
    {
        public Ship Player;
        public int Credits;                       // the run's starting credits, as the scenario left them
        public List<Entity> Entities;             // what the scenario placed besides the player, in its order
    }

    // The scenario's galaxy, at the scenario's seed or, when that is zero, the clock's.
    public static Galaxy Generate(Scenario scenario, GalaxyStage stage)
    {
        stage.SeedFrom(scenario);
        return scenario.Generate(stage) ?? throw new InvalidOperationException($"Scenario {scenario.Name} generated no galaxy.");
    }

    public static ZonePack GenerateArena(ItemManager itemManager, ZoneGenerationSettings zoneSettings, PlanetSettings planetSettings, Galaxy galaxy, Scenario scenario)
    {
        SeedItems(itemManager, galaxy, ArenaStep);
        return galaxy.Entrance.PackedContents = ZoneGenerator.GenerateZone(
            itemManager, zoneSettings, planetSettings, galaxy, galaxy.Entrance, galaxy.IsPrelude, scenario.Ambient);
    }

    // Runs the scenario's Stage, then admits its player and everything it placed: each entity's stance set both ways
    // with the player, a pilot for each piloted one. Returns null, with every failure listed and the arena unchanged,
    // unless the whole scenario staged.
    public static Staged Stage(ItemManager itemManager, Zone arena, Scenario scenario, string startingHull,
        TutorialGenerationSettings tutorialGenerationSettings, List<string> failures)
    {
        SeedItems(itemManager, arena.Galaxy, StageStep);
        var stage = new ScenarioStage(itemManager, arena, startingHull, tutorialGenerationSettings);
        scenario.Stage(stage);
        var reported = failures.Count;
        failures.AddRange(stage.Failures);
        if (!stage.PlayerPlaced)
            failures.Add("player: the scenario places no player");
        if (failures.Count > reported) return null;

        var player = stage.PlayerShip;
        player.IsPlayerShip = true;
        arena.Admit(player, piloted: false);
        foreach (var placed in stage.Placed)
        {
            var hostile = placed.Stance == ScenarioStance.Hostile;
            placed.Entity.SetIff(player, hostile);
            player.SetIff(placed.Entity, hostile);
            arena.Admit(placed.Entity, placed.Piloted, placed.Task);
        }

        // A start state includes what a pilot knows (ruling stage-states-pilot-knowledge): its anchor is tracked from the first
        // step, by the sensors' own path (info gathered past the detection threshold), so they can lose it like any track.
        foreach (var placed in stage.Placed)
            if (placed.Knows != null && placed.Piloted)
                placed.Entity.EntityInfoGathered[placed.Knows] = (itemManager.GameplaySettings.TargetDetectionInfoThreshold + 1f) / 2f;
        return new Staged { Player = player, Credits = stage.Credits, Entities = stage.Placed.Select(placed => placed.Entity).ToList() };
    }

    // A new run's item draws (each lot's quality, each generated fit) come from its galaxy's seed, not the process clock,
    // so a fixed seed fixes the arena's gear and the player's fit. Each step draws its own stream, so whatever runs
    // between them (populating the level) cannot shift it.
    private static void SeedItems(ItemManager itemManager, Galaxy galaxy, uint step) =>
        itemManager.Random = new Random(ItemSeed(galaxy, step));

    // The steps of a run that draw items: generating its arena, staging its scenario, and continuing a saved run.
    public const uint ArenaStep = 1;
    public const uint StageStep = 2;
    public const uint ContinueStep = 3;

    // The item seed of an ActionGameManager scene opened with no galaxy. No run is loaded, so nothing a run decides
    // draws from it.
    public const uint NoRunSeed = 0x5EED0001u;

    // The item stream's seed for one step of a run: the run's seed and the step. Never zero, which no stream accepts.
    public static uint ItemSeed(Galaxy galaxy, uint step)
    {
        var seed = galaxy.Seed * 0x9E3779B1u + step;
        if (seed == 0) throw new InvalidOperationException($"Item seed is zero for galaxy seed {galaxy.Seed} at step {step}.");
        return seed;
    }
}
