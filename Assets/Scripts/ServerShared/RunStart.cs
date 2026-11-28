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

    // The arena's live Zone, which staging needs before the rest of the region exists. Idempotent.
    public static Zone BuildArena(ItemManager itemManager, PlanetSettings planetSettings, Galaxy galaxy) =>
        Live(itemManager, planetSettings, galaxy, galaxy.Entrance);

    // The one owner of whether a zone of the run exists: every zone, in index order, has a generation snapshot and a live
    // Zone. A zone draws its items from the run seed and its index alone (RegionStep), so the order zones are generated
    // in, or whether the rest were generated at all, moves none of them. A fresh run generates every zone but its arena
    // (GenerateArena's); a saved run generates only a zone its save holds no pack for. Idempotent, and ambient: only the
    // arena takes the scenario's orders and Ambient rule. Nothing steps but the player's zone, so the others stay frozen.
    public static void Materialize(ItemManager itemManager, ZoneGenerationSettings zoneSettings, PlanetSettings planetSettings, Galaxy galaxy)
    {
        for (var i = 0; i < galaxy.Zones.Length; i++)
        {
            var zone = galaxy.Zones[i];
            if (zone.PackedContents == null)
            {
                SeedItems(itemManager, galaxy, RegionStep + (uint) i);
                zone.PackedContents = ZoneGenerator.GenerateZone(itemManager, zoneSettings, planetSettings, galaxy, zone);
            }
            Live(itemManager, planetSettings, galaxy, zone);
        }
    }

    private static Zone Live(ItemManager itemManager, PlanetSettings planetSettings, Galaxy galaxy, GalaxyZone zone) =>
        zone.Contents ??= new Zone(itemManager, planetSettings, zone.PackedContents, zone, galaxy);

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

    // The steps of a run that draw items: generating its arena, staging its scenario, continuing a saved run, and generating each zone of the region.
    public const uint ArenaStep = 1;
    public const uint StageStep = 2;
    public const uint ContinueStep = 3;

    // Zone i of the region draws its items from ItemSeed(galaxy, RegionStep + i). It sits clear of the steps above, since

    public const uint RegionStep = 0x100;

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
