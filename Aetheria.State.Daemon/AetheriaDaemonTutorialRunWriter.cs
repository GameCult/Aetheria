using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;

public sealed record AetheriaDaemonWrittenRun(
    string RunId,
    string RunKey,
    string CurrentEntityKey,
    bool IsTutorial);

public static class AetheriaDaemonTutorialRunWriter
{
    public const string RunId = "tutorial";
    public const uint GenerationSeed = 0xA37E_2026u;

    public static async Task<AetheriaDaemonWrittenRun> WriteAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCatalogSnapshot catalog,
        string now)
    {
        var factions = AetheriaDaemonTutorialTopologyGenerator.ResolveFossilFactions(catalog);
        var world = AetheriaDaemonTutorialWorldGenerator.Generate(factions, GenerationSeed);
        var materialized = AetheriaDaemonTutorialWorldMaterializer.Materialize(world, factions, catalog);
        var runKey = RunKey();
        var zoneKeys = materialized.Topology.Zones
            .OrderBy(zone => zone.ZoneIndex)
            .Select(zone => ZoneKey(zone.ZoneIndex))
            .ToArray();
        var corporationIndices = (catalog.Corporations ?? Array.Empty<AetheriaRuntimeCorporation>())
            .Select((corporation, index) => (corporation.CorporationKey, index))
            .Where(value => !string.IsNullOrWhiteSpace(value.CorporationKey))
            .ToDictionary(value => value.CorporationKey, value => value.index, StringComparer.Ordinal);
        var playerEntityKey = EntityKey(materialized.PlayerZoneIndex, materialized.PlayerEntityIndex);

        foreach (var zone in materialized.Zones.Values.OrderBy(zone => zone.Topology.ZoneIndex))
        {
            var entityKeys = Enumerable.Range(0, zone.Entities.Length)
                .Select(index => EntityKey(zone.Topology.ZoneIndex, index))
                .ToArray();
            await node.MutableDocument<AetheriaZoneState>(new CultRecordKey(ZoneKey(zone.Topology.ZoneIndex)))
                .ReplaceAsync(new AetheriaZoneState
                {
                    Name = zone.Topology.Name,
                    Position = new AetheriaVector2 { X = zone.Topology.X, Y = zone.Topology.Y },
                    AdjacentZoneIndices = zone.Topology.AdjacentZoneIndices.ToArray(),
                    FactionIndices = zone.Topology.FactionKeys
                        .Select(key => corporationIndices[key])
                        .ToArray(),
                    OwnerFactionIndex = string.IsNullOrWhiteSpace(zone.Topology.OwnerFactionKey)
                        ? -1
                        : corporationIndices[zone.Topology.OwnerFactionKey],
                    EntityKeys = entityKeys,
                    Orbits = zone.Orbits,
                    Bodies = zone.CelestialPlan.Bodies,
                    GravityTerrainRadius = zone.CelestialPlan.Radius,
                    GravityTerrainDepth = zone.CelestialPlan.GravityTerrainDepth,
                    GravityTerrainDepthExponent = zone.CelestialPlan.GravityTerrainDepthExponent,
                    GravityTerrainBoundaryFog = zone.CelestialPlan.GravityTerrainBoundaryFog,
                    GravityTerrainWaveFrequency = 1,
                    DroppedPickups = [],
                    NextPickupIndex = 0
                }).ConfigureAwait(false);
            for (var entityIndex = 0; entityIndex < zone.Entities.Length; entityIndex++)
            {
                await node.MutableDocument<AetheriaEntitySnapshot>(new CultRecordKey(entityKeys[entityIndex]))
                    .ReplaceAsync(zone.Entities[entityIndex])
                    .ConfigureAwait(false);
            }
        }

        await node.MutableDocument<AetheriaRunState>(new CultRecordKey(runKey)).ReplaceAsync(new AetheriaRunState
        {
            RunId = RunId,
            IsTutorial = true,
            EntranceZoneIndex = materialized.Topology.EntranceZoneIndex,
            ExitZoneIndex = -1,
            CurrentZoneIndex = materialized.PlayerZoneIndex,
            DiscoveredZoneIndices = materialized.Topology.DiscoveredZoneIndices.ToArray(),
            ZoneKeys = zoneKeys,
            FactionRelationships = factions.Select(faction => new AetheriaFactionRelationshipState
            {
                FactionKey = faction.CorporationKey,
                Relationship = "Neutral",
                Standing = 0
            }).ToArray(),
            GenerationSeed = GenerationSeed,
            CurrentEntityKey = playerEntityKey,
            LifecyclePhase = AetheriaRuntimeRunLifecycle.Active,
            TerminalFrameId = -1,
            AgentTasks = materialized.Zones.Values
                .OrderBy(zone => zone.Topology.ZoneIndex)
                .SelectMany(zone => zone.AgentTasks)
                .ToArray(),
            UpdatedAtUtc = now
        }).ConfigureAwait(false);

        var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
            .ReadAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        settings.ActiveRunKey = runKey;
        settings.PlayerName = string.IsNullOrWhiteSpace(settings.PlayerName) ? "Pilot" : settings.PlayerName;
        settings.LastUpdatedAtUtc = now;
        await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
            .ReplaceAsync(settings)
            .ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
        return new AetheriaDaemonWrittenRun(RunId, runKey, playerEntityKey, true);
    }

    public static string RunKey() => $"global:aetheria.run_state.{RunId}.v1";
    public static string ZoneKey(int zoneIndex) => $"global:aetheria.zone_state.{RunId}.{zoneIndex}.v1";
    public static string EntityKey(int zoneIndex, int entityIndex) =>
        $"global:aetheria.run_state.{RunId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
}
