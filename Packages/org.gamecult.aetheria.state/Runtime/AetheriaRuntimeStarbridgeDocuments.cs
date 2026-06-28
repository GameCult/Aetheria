using System;
using System.Linq;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [CultDocument("gamecult.aetheria.starbridge_scenario", "gamecult.aetheria.starbridge_scenario.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeScenarioDocument
    {
        [Key(0)] public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.StarbridgeScenario;
        [Key(1)] public string ScenarioId { get; set; } = "";
        [Key(2)] public string DisplayName { get; set; } = "";
        [Key(3)] public string StartingBaseKey { get; set; } = "";
        [Key(4)] public AetheriaRuntimeStarbridgeStationStockItem[] StationStock { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeStationStockItem>();
        [Key(5)] public string[] AvailableShipKeys { get; set; } = Array.Empty<string>();
        [Key(6)] public AetheriaRuntimeStarbridgeWaveDefinition[] Waves { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeWaveDefinition>();
        [Key(7)] public string[] AttackerMixKeys { get; set; } = Array.Empty<string>();
        [Key(8)] public string[] RecoveredTechnologyPoolKeys { get; set; } = Array.Empty<string>();
        [Key(9)] public AetheriaRuntimeStarbridgeRuntimeRole[] RuntimeRoles { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeRuntimeRole>();
    }

    [CultDocument("gamecult.aetheria.starbridge_session", "gamecult.aetheria.starbridge_session.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeSessionDocument
    {
        [Key(0)] public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.StarbridgeSession;
        [Key(1)] public string SessionId { get; set; } = "";
        [Key(2)] public string ScenarioId { get; set; } = "";
        [Key(3)] public string RunId { get; set; } = "";
        [Key(4)] public string BaseEntityKey { get; set; } = "";
        [Key(5)] public string StationEntityKey { get; set; } = "";
        [Key(6)] public string Phase { get; set; } = "setup";
        [Key(7)] public int CurrentWaveIndex { get; set; }
        [Key(8)] public AetheriaRuntimeStarbridgeRuntimeRole[] RuntimeRoles { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeRuntimeRole>();
    }

    [CultDocument("gamecult.aetheria.starbridge_session_summary", "gamecult.aetheria.starbridge_session_summary.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeSessionSummaryDocument
    {
        [Key(0)] public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary;
        [Key(1)] public long FrameId { get; set; }
        [Key(2)] public string PublishedAtUtc { get; set; } = "";
        [Key(3)] public string SessionId { get; set; } = "";
        [Key(4)] public string ScenarioId { get; set; } = "";
        [Key(5)] public string ScenarioName { get; set; } = "";
        [Key(6)] public string RunId { get; set; } = "";
        [Key(7)] public int ZoneIndex { get; set; }
        [Key(8)] public string ZoneName { get; set; } = "";
        [Key(9)] public string Phase { get; set; } = "setup";
        [Key(10)] public int CurrentWaveIndex { get; set; }
        [Key(11)] public AetheriaRuntimeStarbridgeBaseStatus BaseStatus { get; set; } =
            new AetheriaRuntimeStarbridgeBaseStatus();
        [Key(12)] public AetheriaRuntimeStarbridgeStationStockItem[] StationStock { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeStationStockItem>();
        [Key(13)] public AetheriaRuntimeStarbridgeWaveForecast[] WaveForecast { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeWaveForecast>();
        [Key(14)] public AetheriaRuntimeStarbridgeRuntimeRole[] RuntimeRoles { get; set; } =
            Array.Empty<AetheriaRuntimeStarbridgeRuntimeRole>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeStationStockItem
    {
        [Key(0)] public string ItemKey { get; set; } = "";
        [Key(1)] public int Quantity { get; set; }
        [Key(2)] public double Quality { get; set; }
        [Key(3)] public double Durability { get; set; }
        [Key(4)] public string Source { get; set; } = "station";
        [Key(5)] public AetheriaRuntimeAssetRef IconAsset { get; set; } =
            AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Texture);
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeWaveDefinition
    {
        [Key(0)] public int WaveIndex { get; set; }
        [Key(1)] public string DisplayName { get; set; } = "";
        [Key(2)] public string[] AttackerKeys { get; set; } = Array.Empty<string>();
        [Key(3)] public double StartsAfterSeconds { get; set; }
        [Key(4)] public string BossKey { get; set; } = "";
        [Key(5)] public string[] RecoveredTechnologyKeys { get; set; } = Array.Empty<string>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeWaveForecast
    {
        [Key(0)] public int WaveIndex { get; set; }
        [Key(1)] public string DisplayName { get; set; } = "";
        [Key(2)] public string[] AttackerKeys { get; set; } = Array.Empty<string>();
        [Key(3)] public string BossKey { get; set; } = "";
        [Key(4)] public string[] RecoveredTechnologyKeys { get; set; } = Array.Empty<string>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeRuntimeRole
    {
        [Key(0)] public string RuntimeId { get; set; } = "";
        [Key(1)] public string Role { get; set; } = "";
        [Key(2)] public string EntityKey { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgeBaseStatus
    {
        [Key(0)] public string EntityKey { get; set; } = "";
        [Key(1)] public string DisplayName { get; set; } = "";
        [Key(2)] public double Hull { get; set; }
        [Key(3)] public double Shield { get; set; }
        [Key(4)] public double Heat { get; set; }
        [Key(5)] public bool IsActive { get; set; }
    }

    public static class AetheriaRuntimeStarbridgeProjection
    {
        public static AetheriaRuntimeStarbridgeSessionSummaryDocument ProjectSessionSummary(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeStarbridgeScenarioDocument? scenario = null,
            AetheriaRuntimeStarbridgeSessionDocument? session = null,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = CurrentZone(run);
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var runId = string.IsNullOrWhiteSpace(run.RunId) ? "local-starbridge" : run.RunId;
            var scenarioId = FirstNonEmpty(session?.ScenarioId, scenario?.ScenarioId, "starbridge.local");
            var baseEntity = ResolveBaseEntity(entities, runId, zone.ZoneIndex, session?.BaseEntityKey, scenario?.StartingBaseKey);

            return new AetheriaRuntimeStarbridgeSessionSummaryDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SessionId = FirstNonEmpty(session?.SessionId, frame.SessionId, "starbridge-session"),
                ScenarioId = scenarioId,
                ScenarioName = FirstNonEmpty(scenario?.DisplayName, scenarioId),
                RunId = runId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                Phase = FirstNonEmpty(session?.Phase, "setup"),
                CurrentWaveIndex = Math.Max(0, session?.CurrentWaveIndex ?? 0),
                BaseStatus = ToBaseStatus(baseEntity, runId, zone.ZoneIndex),
                StationStock = ResolveStationStock(scenario, session, baseEntity, catalog),
                WaveForecast = ResolveWaveForecast(scenario, session),
                RuntimeRoles = ResolveRuntimeRoles(scenario, session)
            };
        }

        private static AetheriaRuntimeZoneSnapshotCommit CurrentZone(AetheriaRuntimeRunCheckpointCommit run)
        {
            var zones = run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
            return zones.FirstOrDefault(zone => zone.ZoneIndex == run.CurrentZoneIndex) ??
                zones.FirstOrDefault() ??
                new AetheriaRuntimeZoneSnapshotCommit();
        }

        private static AetheriaRuntimeEntitySnapshotCommit? ResolveBaseEntity(
            System.Collections.Generic.IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            string runId,
            int zoneIndex,
            string? sessionBaseKey,
            string? scenarioBaseKey)
        {
            var keys = new[] { sessionBaseKey, scenarioBaseKey }
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToArray();
            foreach (var entity in entities)
            {
                var entityKey = BuildEntityKey(runId, zoneIndex, entity.EntityIndex);
                if (keys.Any(key => string.Equals(key, entityKey, StringComparison.Ordinal) ||
                    string.Equals(key, entity.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return entity;
                }
            }

            return entities.FirstOrDefault(entity => IsBaseLike(entity)) ??
                entities.FirstOrDefault(entity => string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBaseLike(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            var kind = entity.Kind ?? "";
            var name = entity.Name ?? "";
            return kind.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                kind.IndexOf("station", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("station", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AetheriaRuntimeStarbridgeBaseStatus ToBaseStatus(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            string runId,
            int zoneIndex)
        {
            if (entity == null)
                return new AetheriaRuntimeStarbridgeBaseStatus();

            return new AetheriaRuntimeStarbridgeBaseStatus
            {
                EntityKey = BuildEntityKey(runId, zoneIndex, entity.EntityIndex),
                DisplayName = entity.Name ?? "",
                Hull = Stat(entity, "hull"),
                Shield = Stat(entity, "shield"),
                Heat = Stat(entity, "heat"),
                IsActive = entity.IsActive
            };
        }

        private static AetheriaRuntimeStarbridgeStationStockItem[] ResolveStationStock(
            AetheriaRuntimeStarbridgeScenarioDocument? scenario,
            AetheriaRuntimeStarbridgeSessionDocument? session,
            AetheriaRuntimeEntitySnapshotCommit? baseEntity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (scenario?.StationStock?.Length > 0)
                return scenario.StationStock.Select(item => WithIconAsset(item, catalog)).ToArray();

            if (baseEntity == null)
                return Array.Empty<AetheriaRuntimeStarbridgeStationStockItem>();

            return (baseEntity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .SelectMany(bay => bay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Select(slot => slot.Item ?? new AetheriaRuntimeLoadoutItemCommit())
                .Where(item => !string.IsNullOrWhiteSpace(item.ItemKey))
                .Select(item => new AetheriaRuntimeStarbridgeStationStockItem
                {
                    ItemKey = item.ItemKey ?? "",
                    Quantity = item.Quantity,
                    Quality = item.Quality,
                    Durability = item.Durability,
                    Source = string.IsNullOrWhiteSpace(session?.StationEntityKey) ? "base-cargo" : session!.StationEntityKey,
                    IconAsset = ResolveItemIconAsset(item.ItemKey, catalog)
                })
                .ToArray();
        }

        private static AetheriaRuntimeStarbridgeStationStockItem WithIconAsset(
            AetheriaRuntimeStarbridgeStationStockItem item,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (item == null)
                return new AetheriaRuntimeStarbridgeStationStockItem();

            item.IconAsset = ResolveItemIconAsset(item.ItemKey, catalog);
            return item;
        }

        private static AetheriaRuntimeAssetRef ResolveItemIconAsset(
            string? itemKey,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            itemKey ??= "";
            var typedItem = catalog?.FindItem(itemKey);
            var icon = AetheriaRuntimeAssets.AssetRefFromCatalogIcon(
                typedItem?.ActionBarIcon,
                $"item.{itemKey}.icon");
            return string.IsNullOrWhiteSpace(icon.AssetKey)
                ? AetheriaRuntimeAssetRef.FromKey(
                    $"item.{itemKey}.icon",
                    AetheriaRuntimeAssetKinds.Texture,
                    $"item.{itemKey}.icon")
                : icon;
        }

        private static AetheriaRuntimeStarbridgeWaveForecast[] ResolveWaveForecast(
            AetheriaRuntimeStarbridgeScenarioDocument? scenario,
            AetheriaRuntimeStarbridgeSessionDocument? session)
        {
            var currentWaveIndex = Math.Max(0, session?.CurrentWaveIndex ?? 0);
            return (scenario?.Waves ?? Array.Empty<AetheriaRuntimeStarbridgeWaveDefinition>())
                .Where(wave => wave.WaveIndex >= currentWaveIndex)
                .OrderBy(wave => wave.WaveIndex)
                .Take(3)
                .Select(wave => new AetheriaRuntimeStarbridgeWaveForecast
                {
                    WaveIndex = wave.WaveIndex,
                    DisplayName = wave.DisplayName ?? "",
                    AttackerKeys = wave.AttackerKeys ?? Array.Empty<string>(),
                    BossKey = wave.BossKey ?? "",
                    RecoveredTechnologyKeys = wave.RecoveredTechnologyKeys ?? Array.Empty<string>()
                })
                .ToArray();
        }

        private static AetheriaRuntimeStarbridgeRuntimeRole[] ResolveRuntimeRoles(
            AetheriaRuntimeStarbridgeScenarioDocument? scenario,
            AetheriaRuntimeStarbridgeSessionDocument? session)
        {
            if (session?.RuntimeRoles?.Length > 0)
                return session.RuntimeRoles;
            return scenario?.RuntimeRoles ?? Array.Empty<AetheriaRuntimeStarbridgeRuntimeRole>();
        }

        private static double Stat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
        {
            var grid = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            return grid?.Values?.FirstOrDefault() ?? 0;
        }

        private static string BuildEntityKey(string runId, int zoneIndex, int entityIndex)
        {
            return $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value!;
            }

            return "";
        }
    }
}
