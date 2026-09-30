using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The owner of what a new run starts with (docs/scenarios-cut.md, 2.2): how the arena zone is generated, the player
// ship, and a scenario's other entities with their stance, pilot and cargo. A plain New Game is the absent-scenario
// case of the same path. Nothing else generates, unpacks or admits a new run's player; everything it stages joins the
// arena through Zone.Admit. It holds no Unity type.
public static class RunStart
{
    public sealed class Staged
    {
        public Ship Player;
        public List<Entity> Entities;             // the scenario's entities, in its order; empty with no scenario
    }

    // The arena is the galaxy's entrance zone, generated under the scenario's Ambient rule; with no scenario, ambient.
    public static ZonePack GenerateArena(ItemManager itemManager, ZoneGenerationSettings zoneSettings, Galaxy galaxy, Scenario scenario) =>
        galaxy.Entrance.PackedContents = ZoneGenerator.GenerateZone(
            itemManager, zoneSettings, galaxy, galaxy.Entrance, galaxy.IsPrelude, scenario?.Ambient ?? true);

    // The check entry: every failure staging this scenario would hit, found with no zone. With no galaxy there is no
    // availability to filter by (LoadoutGenerator.IsAvailable), and a scenario's arena is a prelude, which offers every
    // product anyway. It touches no store, so the menu can call it before it clears the saved run.
    public static List<string> Check(ItemManager itemManager, Scenario scenario)
    {
        var failures = new List<string>();
        if (scenario != null) Build(itemManager, null, scenario, _ => true, failures);
        return failures;
    }

    // Stages a new run into its arena, all-or-nothing: null, with every failure listed and the zone unchanged, unless
    // everything builds. With no scenario: a generated ship of the startingHull (any ship hull when it is empty) for
    // defaultFaction, at the origin. With one: its player and entities, each at its authored position and heading with
    // its cargo, each entity's stance set both ways with the player, and a Minion for each piloted one.
    public static Staged Stage(ItemManager itemManager, Zone arena, Scenario scenario, string startingHull,
        Faction defaultFaction, List<string> failures)
    {
        if (scenario == null)
        {
            var generator = new LoadoutGenerator(ref itemManager.Random, itemManager, arena.Galaxy, arena.GalaxyZone, defaultFaction, 2);
            var pack = generator.GenerateShipLoadout(data => string.IsNullOrEmpty(startingHull) || data.Name == startingHull);
            if (pack == null)
            {
                failures.Add($"player: no ship hull named {startingHull}");
                return null;
            }

            var ship = (Ship) EntitySerializer.Unpack(itemManager, arena, pack);
            ship.IsPlayerShip = true;
            ship.Position = float3.zero;
            arena.Admit(ship, piloted: false);
            return new Staged { Player = ship, Entities = new List<Entity>() };
        }

        // Presets carry no faction, so availability is the galaxy's alone.
        var availability = new LoadoutGenerator(ref itemManager.Random, itemManager, arena.Galaxy, arena.GalaxyZone, null, 2);
        var built = Build(itemManager, arena, scenario, availability.IsAvailable, failures);
        if (built == null) return null;

        var (player, entities) = built.Value;
        player.IsPlayerShip = true;
        arena.Admit(player, piloted: false);
        for (var i = 0; i < entities.Count; i++)
        {
            var hostile = scenario.Entities[i].Stance == ScenarioStance.Hostile;
            entities[i].SetIff(player, hostile);
            player.SetIff(entities[i], hostile);
            arena.Admit(entities[i], scenario.Entities[i].Piloted);
        }
        return new Staged { Player = player, Entities = entities };
    }

    // Everything a scenario places, built and positioned but admitted nowhere; null when anything failed.
    private static (Ship player, List<Entity> entities)? Build(ItemManager itemManager, Zone zone, Scenario scenario,
        Predicate<FactionProductData> isAvailable, List<string> failures)
    {
        var reported = failures.Count;
        Entity player = null;
        if (scenario.Player == null) failures.Add("player: the scenario places no player");
        else
        {
            player = Place(itemManager, zone, scenario.Player, "player", isAvailable, failures);
            if (player != null && !(player is Ship)) failures.Add($"player: {player.Name} is not a ship hull");
        }

        var entities = new List<Entity>();
        for (var i = 0; i < scenario.Entities.Count; i++)
        {
            var where = $"entity {i}";
            var entity = Place(itemManager, zone, scenario.Entities[i], where, isAvailable, failures);
            if (entity != null && scenario.Entities[i].Piloted && !(entity is Ship))
                failures.Add($"{where}: {entity.Name} is not a ship hull, so it cannot be piloted");
            entities.Add(entity);
        }
        return failures.Count > reported ? null : ((Ship) player, entities);
    }

    private static Entity Place(ItemManager itemManager, Zone zone, ScenarioShip placed, string where,
        Predicate<FactionProductData> isAvailable, List<string> failures)
    {
        var loadout = itemManager.ItemData.Get(placed.Loadout);
        if (loadout == null)
        {
            failures.Add($"{where}: preset {placed.Loadout.Key} is not in the catalog");
            return null;
        }

        var reported = failures.Count;
        var materialized = new List<string>();
        var entity = Loadouts.Materialize(itemManager, zone, loadout, isAvailable, materialized);
        failures.AddRange(materialized.Select(failure => $"{where} ({loadout.Name}): {failure}"));
        var cargo = placed.Cargo.Select(design => Loadouts.Resolve(itemManager, design, isAvailable, $"{where} cargo", failures)).ToList();
        if (failures.Count > reported) return null;

        entity.Position.xz = placed.Position;
        if (lengthsq(placed.Direction) > 0) entity.Direction = normalize(placed.Direction);
        var bay = entity.CargoBays.FirstOrDefault();
        foreach (var build in cargo)
        {
            var item = build();
            if (bay == null || !bay.TryStore(item))
                failures.Add($"{where} cargo: no room for {itemManager.GetData(item).Name}");
        }
        return failures.Count > reported ? null : entity;
    }
}
