using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

public sealed record AetheriaDaemonMaterializedTutorialZone(
    AetheriaDaemonTutorialZoneTopology Topology,
    AetheriaDaemonGeneratedZonePlan CelestialPlan,
    string NearestFactionKey,
    int FactionPresence,
    AetheriaOrbitSnapshot[] Orbits,
    AetheriaEntitySnapshot[] Entities,
    int PlayerEntityIndex);

public sealed record AetheriaDaemonMaterializedTutorialWorld(
    AetheriaDaemonTutorialTopology Topology,
    IReadOnlyDictionary<int, AetheriaDaemonMaterializedTutorialZone> Zones,
    int PlayerZoneIndex,
    int PlayerEntityIndex);

/// <summary>
/// Daemon-owned port of the fossil ZoneGenerator population pass. Celestial
/// generation hands its exact remaining random state to this organ, so adding
/// presentation work cannot perturb station, turret, or enemy placement.
/// </summary>
public static class AetheriaDaemonTutorialWorldMaterializer
{
    public static AetheriaDaemonMaterializedTutorialWorld Materialize(
        AetheriaDaemonTutorialWorldPlan world,
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        var homes = world.Topology.HomeZoneByFactionKey;
        var adjacency = world.Topology.Zones.ToDictionary(
            zone => zone.ZoneIndex,
            zone => zone.AdjacentZoneIndices,
            EqualityComparer<int>.Default);
        var distances = factions.ToDictionary(
            faction => faction.CorporationKey,
            faction => DistancesFrom(homes[faction.CorporationKey], adjacency),
            StringComparer.Ordinal);
        var zones = new Dictionary<int, AetheriaDaemonMaterializedTutorialZone>();
        var playerEntityIndex = -1;

        foreach (var topology in world.Topology.Zones.OrderBy(zone => zone.ZoneIndex))
        {
            var celestial = world.Zones[topology.ZoneIndex];
            var random = new CultMath.Random(celestial.PostBodyRandomState);
            var spawnRandom = new CultMath.Random(celestial.PostBodyRandomState ^ 0x51A7_10C5u);
            var nearest = factions
                .OrderBy(faction => distances[faction.CorporationKey][topology.ZoneIndex])
                .First();
            var homeDistance = distances[nearest.CorporationKey][topology.ZoneIndex];
            var tutorialInfluence = Math.Max(0, (nearest.InfluenceDistance + 1) / 2);
            var factionPresence = tutorialInfluence - homeDistance + 1;
            var stationCount = (int)(random.NextFloat() * (factionPresence + 1));
            var orbits = celestial.Orbits.ToList();
            var entities = new List<AetheriaEntitySnapshot>();
            var generators = new Dictionary<string, AetheriaDaemonLoadoutGenerator>(StringComparer.Ordinal);

            AetheriaDaemonLoadoutGenerator Generator(string factionKey)
            {
                if (!generators.TryGetValue(factionKey, out var generator))
                {
                    generator = new AetheriaDaemonLoadoutGenerator(
                        catalog,
                        (uint)random.NextInt(1, int.MaxValue),
                        topology.ZoneIndex,
                        homes,
                        adjacency,
                        isPrelude: true);
                    generators.Add(factionKey, generator);
                }
                return generator;
            }

            var potential = celestial.Orbits
                .Where(orbit => !string.IsNullOrWhiteSpace(orbit.ParentOrbitKey))
                .Where(orbit => !celestial.Orbits.Any(sibling =>
                    !ReferenceEquals(sibling, orbit) &&
                    string.Equals(sibling.ParentOrbitKey, orbit.ParentOrbitKey, StringComparison.Ordinal) &&
                    Math.Abs(sibling.Distance - orbit.Distance) < 0.1))
                .OrderBy(orbit => orbit.Distance)
                .ToArray();
            var selected = potential.Skip(potential.Length / 2).Take(Math.Max(0, stationCount)).ToArray();
            for (var stationIndex = 0; stationIndex < selected.Length; stationIndex++)
            {
                var baseOrbit = selected[stationIndex];
                var stationOrbit = new AetheriaOrbitSnapshot
                {
                    OrbitKey = $"tutorial.zone.{topology.ZoneIndex}.station.{stationIndex}.orbit",
                    ParentOrbitKey = baseOrbit.ParentOrbitKey,
                    Distance = baseOrbit.Distance,
                    Phase = baseOrbit.Phase + MathF.PI / 3 * Sign(random.NextFloat() - 0.5f),
                    FixedPosition = new AetheriaVector2()
                };
                orbits.Add(stationOrbit);
                var security = (int)((1f - MathF.Pow(random.NextFloat(), factionPresence / 2f)) * 3f);
                entities.Add(AetheriaDaemonGeneratedEntityFactory.Create(
                    $"{topology.Name} Station {stationIndex + 1}",
                    "station",
                    nearest.CorporationKey,
                    Generator(nearest.CorporationKey).Build("station", nearest.CorporationKey),
                    orbitKey: stationOrbit.OrbitKey,
                    securityLevel: security,
                    securityRadius: celestial.Radius));

                for (var turretIndex = 0; turretIndex < 2; turretIndex++)
                {
                    var distanceMultiplier = turretIndex / 2;
                    if (turretIndex % 2 == 0) distanceMultiplier = -distanceMultiplier;
                    var turretOrbit = new AetheriaOrbitSnapshot
                    {
                        OrbitKey = $"tutorial.zone.{topology.ZoneIndex}.station.{stationIndex}.turret.{turretIndex}.orbit",
                        ParentOrbitKey = stationOrbit.ParentOrbitKey,
                        Distance = stationOrbit.Distance,
                        Phase = stationOrbit.Phase + 20f * distanceMultiplier / stationOrbit.Distance,
                        FixedPosition = new AetheriaVector2()
                    };
                    orbits.Add(turretOrbit);
                    entities.Add(AetheriaDaemonGeneratedEntityFactory.Create(
                        $"{topology.Name} Turret {stationIndex + 1}-{turretIndex + 1}",
                        "turret",
                        nearest.CorporationKey,
                        Generator(nearest.CorporationKey).Build("turret", nearest.CorporationKey),
                        orbitKey: turretOrbit.OrbitKey,
                        securityLevel: security,
                        securityRadius: celestial.Radius));
                }
            }

            var enemyCount = (int)(random.NextFloat() * factionPresence * 2) + stationCount;
            for (var enemyIndex = 0; enemyIndex < Math.Max(0, enemyCount); enemyIndex++)
            {
                var halfRadius = celestial.Radius * 0.5;
                var enemy = AetheriaDaemonGeneratedEntityFactory.Create(
                    $"{nearest.ShortName} Ship {enemyIndex + 1}",
                    "ship",
                    nearest.CorporationKey,
                    Generator(nearest.CorporationKey).Build("ship", nearest.CorporationKey),
                    spawnRandom.NextFloat((float)-halfRadius, (float)halfRadius),
                    spawnRandom.NextFloat((float)-halfRadius, (float)halfRadius));
                enemy.AgentTaskCapabilities = ["attack", "defend", "explore"];
                entities.Add(enemy);
            }

            var localPlayerIndex = -1;
            if (topology.ZoneIndex == world.Topology.EntranceZoneIndex)
            {
                var protagonist = factions[0];
                var starter = new AetheriaDaemonLoadoutGenerator(
                    catalog,
                    world.Topology.GenerationSeed ^ 0x50A7_EA11u,
                    topology.ZoneIndex,
                    homes,
                    adjacency,
                    isPrelude: true).Build("ship", protagonist.CorporationKey);
                localPlayerIndex = entities.Count;
                entities.Add(AetheriaDaemonGeneratedEntityFactory.Create(
                    "Pilot",
                    "ship",
                    protagonist.CorporationKey,
                    starter));
                playerEntityIndex = localPlayerIndex;
            }

            ApplyOrbitalPositions(orbits, entities);

            zones.Add(topology.ZoneIndex, new AetheriaDaemonMaterializedTutorialZone(
                topology,
                celestial,
                nearest.CorporationKey,
                factionPresence,
                orbits.ToArray(),
                entities.ToArray(),
                localPlayerIndex));
        }

        if (playerEntityIndex < 0)
            throw new InvalidOperationException("Tutorial materialization produced no player entry ship.");
        return new AetheriaDaemonMaterializedTutorialWorld(
            world.Topology,
            zones,
            world.Topology.EntranceZoneIndex,
            playerEntityIndex);
    }

    private static void ApplyOrbitalPositions(
        IReadOnlyList<AetheriaOrbitSnapshot> orbits,
        IReadOnlyList<AetheriaEntitySnapshot> entities)
    {
        var runtimeZone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Orbits = orbits.Select(orbit => new AetheriaRuntimeOrbitSnapshotCommit
            {
                OrbitKey = orbit.OrbitKey,
                ParentOrbitKey = orbit.ParentOrbitKey,
                Distance = orbit.Distance,
                Phase = orbit.Phase,
                FixedPositionX = orbit.FixedPosition?.X ?? 0,
                FixedPositionY = orbit.FixedPosition?.Y ?? 0
            }).ToArray()
        };
        var positions = AetheriaRuntimeOrbitQueries.BuildPositions(runtimeZone);
        foreach (var entity in entities.Where(entity => !string.IsNullOrWhiteSpace(entity.OrbitKey)))
        {
            if (!positions.TryGetValue(entity.OrbitKey, out var position))
                throw new InvalidDataException(
                    $"Generated orbital entity '{entity.Name}' references missing orbit '{entity.OrbitKey}'.");
            entity.Position.X = position.x;
            entity.Position.Z = position.z;
        }
    }

    private static IReadOnlyDictionary<int, int> DistancesFrom(
        int start,
        IReadOnlyDictionary<int, IReadOnlyList<int>> adjacency)
    {
        var result = adjacency.Keys.ToDictionary(index => index, _ => int.MaxValue);
        var queue = new Queue<int>();
        result[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in adjacency[current])
            {
                if (result[neighbor] <= result[current] + 1) continue;
                result[neighbor] = result[current] + 1;
                queue.Enqueue(neighbor);
            }
        }
        return result;
    }

    private static int Sign(float value) => value < 0 ? -1 : value > 0 ? 1 : 0;
}
