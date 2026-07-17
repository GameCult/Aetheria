using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;

public static class AetheriaDaemonRegularRunWriter
{
    public static async Task<AetheriaDaemonWrittenRun> WriteAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCatalogSnapshot catalog,
        string now,
        uint generationSeed,
        AetheriaDaemonRegularTopologySettings? topologySettings = null)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (generationSeed == 0) throw new ArgumentOutOfRangeException(nameof(generationSeed));

        var factions = AetheriaDaemonRegularTopologyGenerator.ResolveFossilFactions(catalog);
        var world = AetheriaDaemonTutorialWorldGenerator.GenerateRegular(
            factions, generationSeed, topologySettings);
        var materialized = AetheriaDaemonTutorialWorldMaterializer.Materialize(
            world, factions, catalog, isPrelude: false, identityPrefix: "regular");
        var runId = $"sector-{generationSeed:x8}";
        var runKey = RunKey(runId);
        var corporationIndices = (catalog.Corporations ?? Array.Empty<AetheriaRuntimeCorporation>())
            .Select((corporation, index) => (corporation.CorporationKey, index))
            .Where(value => !string.IsNullOrWhiteSpace(value.CorporationKey))
            .ToDictionary(value => value.CorporationKey, value => value.index, StringComparer.Ordinal);
        var zoneKeys = materialized.Topology.Zones
            .OrderBy(zone => zone.ZoneIndex)
            .Select(zone => ZoneKey(runId, zone.ZoneIndex))
            .ToArray();
        var playerEntityKey = EntityKey(runId, materialized.PlayerZoneIndex, materialized.PlayerEntityIndex);

        foreach (var zone in materialized.Zones.Values.OrderBy(zone => zone.Topology.ZoneIndex))
        {
            var entityKeys = Enumerable.Range(0, zone.Entities.Length)
                .Select(index => EntityKey(runId, zone.Topology.ZoneIndex, index))
                .ToArray();
            await node.MutableDocument<AetheriaZoneState>(new CultRecordKey(ZoneKey(runId, zone.Topology.ZoneIndex)))
                .ReplaceAsync(new AetheriaZoneState
                {
                    Name = zone.Topology.Name,
                    Position = new AetheriaVector2 { X = zone.Topology.X, Y = zone.Topology.Y },
                    AdjacentZoneIndices = zone.Topology.AdjacentZoneIndices.ToArray(),
                    FactionIndices = zone.Topology.FactionKeys.Select(key => corporationIndices[key]).ToArray(),
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
            RunId = runId,
            IsTutorial = false,
            GameMode = AetheriaGameSessionState.AetheriaMode,
            EntranceZoneIndex = materialized.Topology.EntranceZoneIndex,
            ExitZoneIndex = materialized.Topology.ExitZoneIndex,
            CurrentZoneIndex = materialized.PlayerZoneIndex,
            DiscoveredZoneIndices = materialized.Topology.DiscoveredZoneIndices.ToArray(),
            ZoneKeys = zoneKeys,
            FactionRelationships = materialized.Topology.HomeZoneByFactionKey.Keys
                .Select(key => new AetheriaFactionRelationshipState
                {
                    FactionKey = key,
                    Relationship = "Neutral",
                    Standing = 0
                })
                .ToArray(),
            GenerationSeed = generationSeed,
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
            .ReplaceAsync(settings).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
        return new AetheriaDaemonWrittenRun(runId, runKey, playerEntityKey, false)
        {
            SessionMode = AetheriaGameSessionState.AetheriaMode
        };
    }

    public static string RunKey(string runId) => $"global:aetheria.run_state.{runId}.v1";
    public static string ZoneKey(string runId, int zoneIndex) =>
        $"global:aetheria.zone_state.{runId}.{zoneIndex}.v1";
    public static string EntityKey(string runId, int zoneIndex, int entityIndex) =>
        $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
}
