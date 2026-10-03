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
        public List<Entity> Entities;             // what the scenario placed besides the player, in its order
    }

    // The scenario's galaxy, at the scenario's seed or, when that is zero, the clock's.
    public static Galaxy Generate(Scenario scenario, GalaxyStage stage)
    {
        stage.SeedFrom(scenario);
        return scenario.Generate(stage) ?? throw new InvalidOperationException($"Scenario {scenario.Name} generated no galaxy.");
    }

    public static ZonePack GenerateArena(ItemManager itemManager, ZoneGenerationSettings zoneSettings, Galaxy galaxy, Scenario scenario)
    {
        SeedItems(itemManager, galaxy, 1);
        return galaxy.Entrance.PackedContents = ZoneGenerator.GenerateZone(
            itemManager, zoneSettings, galaxy, galaxy.Entrance, galaxy.IsPrelude, scenario.Ambient);
    }

    // Runs the scenario's Stage, then admits its player and everything it placed: each entity's stance set both ways
    // with the player, a pilot for each piloted one. Returns null, with every failure listed and the arena unchanged,
    // unless the whole scenario staged.
    public static Staged Stage(ItemManager itemManager, Zone arena, Scenario scenario, string startingHull,
        TutorialGenerationSettings tutorialGenerationSettings, List<string> failures)
    {
        SeedItems(itemManager, arena.Galaxy, 2);
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
            arena.Admit(placed.Entity, placed.Piloted);
        }
        return new Staged { Player = player, Entities = stage.Placed.Select(placed => placed.Entity).ToList() };
    }

    // A new run's item draws (each lot's quality, each generated fit) come from its galaxy's seed, not the process clock,
    // so a fixed seed fixes the arena's gear and the player's fit. Each step draws its own stream, so whatever runs
    // between them (populating the level) cannot shift it.
    private static void SeedItems(ItemManager itemManager, Galaxy galaxy, uint step) =>
        itemManager.Random = new Random(galaxy.Seed * 0x9E3779B1u + step);
}
