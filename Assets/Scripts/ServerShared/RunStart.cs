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
            itemManager, zoneSettings, planetSettings, galaxy, galaxy.Entrance, scenario.EntranceStations, scenario.Ambient);
    }

    // Runs a start and answers whether it produced a run: null, with the reason in `failures`, when `start` throws or
    // returns nothing. The caller shows the failure and clears its run; nothing past a null may run the game.
    public static Staged Launch(Func<Staged> start, List<string> failures, Action<Exception> log = null)
    {
        Staged staged = null;
        try { staged = start(); }
        catch (Exception exception)
        {
            log?.Invoke(exception);
            failures.Add(exception.Message);
        }
        if (staged == null && failures.Count == 0) failures.Add("the start produced no run");
        return staged;
    }

    // Runs the scenario's Stage, then has the stage admit its player and everything it placed.
    // Returns null, with every failure listed and the arena unchanged,
    // unless the whole scenario staged.
    public static Staged Stage(ItemManager itemManager, Zone arena, Scenario scenario, string startingHull,
        List<string> failures)
    {
        SeedItems(itemManager, arena.Galaxy, StageStep);
        var stage = new ScenarioStage(itemManager, arena, startingHull);
        scenario.Stage(stage);
        var reported = failures.Count;
        failures.AddRange(stage.Failures);
        if (!stage.PlayerPlaced)
            failures.Add("player: the scenario places no player");
        if (failures.Count > reported) return null;
        stage.Admit();
        return new Staged { Player = stage.PlayerShip, Credits = stage.Credits, Entities = stage.Placed.Select(placed => placed.Entity).ToList() };
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
