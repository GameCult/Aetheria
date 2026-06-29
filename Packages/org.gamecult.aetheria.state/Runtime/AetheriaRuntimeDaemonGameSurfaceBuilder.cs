using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonGameSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.game";
        public const string TuiSurfaceId = "aetheria.game.tui";

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonHealthDocument health,
            AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary,
            AetheriaRuntimeStarbridgeSessionSummaryDocument? starbridge = null)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            health ??= new AetheriaRuntimeDaemonHealthDocument();
            commandBoundary ??= AetheriaRuntimeDaemonCommandBoundaryDocument.Create(frame.DaemonId);
            starbridge ??= AetheriaRuntimeStarbridgeDocuments.SessionSummary(frame);

            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = FindCurrentZone(run);
            var entity = FindCurrentEntity(run, zone);
            var target = FindTargetEntity(zone, entity);
            var entityName = string.IsNullOrWhiteSpace(entity?.Name) ? "(no current entity)" : entity!.Name;
            var surfaceChildren = new List<AetheriaRuntimeSurfaceComponent>
            {
                Node(
                    "aetheria.daemon.game.frame",
                    "card",
                    new[] { ("title", "Daemon Frame") },
                    Metric("aetheria.daemon.game.frame.daemon", "Daemon", frame.DaemonId, AetheriaRuntimeDaemonStateRefs.FrameDaemonId),
                    Metric("aetheria.daemon.game.frame.verse", "Verse", health.VerseId, AetheriaRuntimeDaemonStateRefs.FrameVerseId),
                    Metric("aetheria.daemon.game.frame.frameId", "Frame", frame.FrameId.ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.FrameId),
                    Metric("aetheria.daemon.game.frame.time", "Time", frame.SimulationTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.FrameTime),
                    Metric("aetheria.daemon.game.frame.status", "Status", health.Status, AetheriaRuntimeDaemonStateRefs.FrameStatus),
                    Metric("aetheria.daemon.game.frame.observed_commands", "Observed Commands", health.ObservedCommandCount.ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.FrameObservedCommands),
                    Metric("aetheria.daemon.game.frame.applied", "Applied", health.AppliedCommandCount.ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.FrameAppliedCommands),
                    Metric("aetheria.daemon.game.frame.rejected", "Rejected", health.RejectedCommandCount.ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.FrameRejectedCommands)),
                Node(
                    "aetheria.daemon.game.starbridge",
                    "card",
                    new[] { ("title", "Starbridge Session") },
                    Metric("aetheria.daemon.game.starbridge.scenario", "Scenario", starbridge.ScenarioName),
                    Metric("aetheria.daemon.game.starbridge.session", "Session", starbridge.SessionId),
                    Metric("aetheria.daemon.game.starbridge.phase", "Phase", starbridge.Phase),
                    Metric("aetheria.daemon.game.starbridge.wave", "Wave", starbridge.CurrentWaveIndex.ToString(CultureInfo.InvariantCulture)),
                    Metric("aetheria.daemon.game.starbridge.zone", "Zone", starbridge.ZoneName),
                    Metric("aetheria.daemon.game.starbridge.base", "Base", starbridge.BaseStatus?.DisplayName ?? ""),
                    Metric("aetheria.daemon.game.starbridge.base_hull", "Base Hull", FormatNumber(starbridge.BaseStatus?.Hull ?? 0)),
                    Metric("aetheria.daemon.game.starbridge.base_shield", "Base Shield", FormatNumber(starbridge.BaseStatus?.Shield ?? 0)),
                    Metric("aetheria.daemon.game.starbridge.base_heat", "Base Heat", FormatNumber(starbridge.BaseStatus?.Heat ?? 0))),
                BuildStarbridgeStationStockCard(starbridge),
                BuildStarbridgeWaveForecastCard(starbridge),
                BuildStarbridgeRuntimeRolesCard(starbridge),
                Node(
                    "aetheria.daemon.game.player",
                    "card",
                    new[] { ("title", "Current Entity") },
                    Metric("aetheria.daemon.game.player.run", "Run", run.RunId, AetheriaRuntimeDaemonStateRefs.CurrentRunId),
                    Metric("aetheria.daemon.game.player.zone", "Zone", run.CurrentZoneIndex.ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.CurrentZoneIndex),
                    Metric("aetheria.daemon.game.player.key", "Entity Key", run.CurrentEntityKey, AetheriaRuntimeDaemonStateRefs.CurrentEntityKey),
                    Metric("aetheria.daemon.game.player.name", "Name", entityName, AetheriaRuntimeDaemonStateRefs.CurrentEntityName),
                    Metric("aetheria.daemon.game.player.position", "Position", FormatPosition(entity), AetheriaRuntimeDaemonStateRefs.CurrentEntityPosition),
                    Metric("aetheria.daemon.game.player.target", "Target", string.IsNullOrWhiteSpace(target?.Name) ? "(none)" : target!.Name, AetheriaRuntimeDaemonStateRefs.CurrentTargetName),
                    Metric("aetheria.daemon.game.player.equipment", "Equipment", Count(entity?.Equipment).ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.CurrentEquipmentCount),
                    Metric("aetheria.daemon.game.player.cargo", "Cargo Bays", Count(entity?.CargoContents).ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.CurrentCargoBayCount),
                    Metric("aetheria.daemon.game.player.weaponGroups", "Weapon Groups", Count(entity?.WeaponGroups).ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.CurrentWeaponGroupCount)),
                Node(
                    "aetheria.daemon.game.commands",
                    "card",
                    new[] { ("title", "Typed Command Boundary") },
                    Metric(
                        "aetheria.daemon.game.commands.boundary",
                        "Boundary",
                        commandBoundary.BoundaryId,
                        AetheriaRuntimeDaemonStateRefs.CommandBoundaryId),
                    Metric(
                        "aetheria.daemon.game.commands.count",
                        "Commands",
                        Count(commandBoundary.Commands).ToString(CultureInfo.InvariantCulture),
                        AetheriaRuntimeDaemonStateRefs.CommandCount),
                    Row(
                        "aetheria.daemon.game.commands.primary",
                        CommandButton("aetheria.daemon.game.commands.move", "Move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector),
                        CommandButton("aetheria.daemon.game.commands.target", "Target Nearest", AetheriaRuntimeDaemonCommandKinds.TargetNearest),
                        CommandButton("aetheria.daemon.game.commands.fire", "Fire", AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup),
                        CommandButton("aetheria.daemon.game.commands.ping", "Sensor Ping", AetheriaRuntimeDaemonCommandKinds.SensorPing)))
            };

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria.daemon",
                providerKind: "game.daemon",
                title: "Aetheria Daemon",
                version: frame.FrameId,
                updatedAtUtc: frame.PublishedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        "aetheria.daemon.game.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        surfaceChildren.ToArray()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commandBoundary.Commands
                    .Where(entry => AetheriaRuntimeDaemonSurfaceCommandCatalog.IsArgumentlessCommand(entry.Kind))
                    .Select(entry => new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(entry.Kind),
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.Label(entry.Kind),
                        "cultmesh"))
                    .ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent BuildStarbridgeStationStockCard(
            AetheriaRuntimeStarbridgeSessionSummaryDocument summary)
        {
            var stockRows = (summary.StationStock ?? Array.Empty<AetheriaRuntimeStarbridgeStationStockItem>())
                .Take(6)
                .Select((item, index) => Row(
                    $"aetheria.daemon.game.starbridge.stock.{index}",
                    Metric($"aetheria.daemon.game.starbridge.stock.{index}.item", "Item", item.ItemKey),
                    Metric($"aetheria.daemon.game.starbridge.stock.{index}.quantity", "Qty", item.Quantity.ToString(CultureInfo.InvariantCulture)),
                    Metric($"aetheria.daemon.game.starbridge.stock.{index}.quality", "Quality", FormatNumber(item.Quality))))
                .DefaultIfEmpty(Text("aetheria.daemon.game.starbridge.stock.empty", "No station stock published."))
                .ToArray();

            return Node(
                "aetheria.daemon.game.starbridge.stock",
                "card",
                new[] { ("title", "Station Stock") },
                stockRows);
        }

        private static AetheriaRuntimeSurfaceComponent BuildStarbridgeWaveForecastCard(
            AetheriaRuntimeStarbridgeSessionSummaryDocument summary)
        {
            var waveRows = (summary.WaveForecast ?? Array.Empty<AetheriaRuntimeStarbridgeWaveForecast>())
                .Take(4)
                .Select((wave, index) => Row(
                    $"aetheria.daemon.game.starbridge.wave.{index}",
                    Metric($"aetheria.daemon.game.starbridge.wave.{index}.name", "Wave", wave.DisplayName),
                    Metric($"aetheria.daemon.game.starbridge.wave.{index}.attackers", "Attackers", Join(wave.AttackerKeys)),
                    Metric($"aetheria.daemon.game.starbridge.wave.{index}.boss", "Boss", wave.BossKey),
                    Metric($"aetheria.daemon.game.starbridge.wave.{index}.tech", "Tech", Join(wave.RecoveredTechnologyKeys))))
                .DefaultIfEmpty(Text("aetheria.daemon.game.starbridge.wave.empty", "No wave forecast published."))
                .ToArray();

            return Node(
                "aetheria.daemon.game.starbridge.waves",
                "card",
                new[] { ("title", "Wave Forecast") },
                waveRows);
        }

        private static AetheriaRuntimeSurfaceComponent BuildStarbridgeRuntimeRolesCard(
            AetheriaRuntimeStarbridgeSessionSummaryDocument summary)
        {
            var roleRows = (summary.RuntimeRoles ?? Array.Empty<AetheriaRuntimeStarbridgeRuntimeRole>())
                .Take(8)
                .Select((role, index) => Row(
                    $"aetheria.daemon.game.starbridge.role.{index}",
                    Metric($"aetheria.daemon.game.starbridge.role.{index}.runtime", "Runtime", role.RuntimeId),
                    Metric($"aetheria.daemon.game.starbridge.role.{index}.role", "Role", role.Role),
                    Metric($"aetheria.daemon.game.starbridge.role.{index}.entity", "Entity", role.EntityKey)))
                .DefaultIfEmpty(Text("aetheria.daemon.game.starbridge.role.empty", "No runtime roles published."))
                .ToArray();

            return Node(
                "aetheria.daemon.game.starbridge.roles",
                "card",
                new[] { ("title", "Runtime Roles") },
                roleRows);
        }

        private static AetheriaRuntimeZoneSnapshotCommit FindCurrentZone(AetheriaRuntimeRunCheckpointCommit run)
        {
            return run.Zones.FirstOrDefault(zone => zone.ZoneIndex == run.CurrentZoneIndex)
                ?? run.Zones.FirstOrDefault()
                ?? new AetheriaRuntimeZoneSnapshotCommit();
        }

        private static AetheriaRuntimeEntitySnapshotCommit? FindCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var entityIndex = TryParseEntityIndex(run.CurrentEntityKey);
            if (entityIndex >= 0)
                return zone.Entities.FirstOrDefault(entity => entity.EntityIndex == entityIndex);

            return zone.Entities.FirstOrDefault();
        }

        private static AetheriaRuntimeEntitySnapshotCommit? FindTargetEntity(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit? entity)
        {
            if (entity == null || entity.TargetEntityIndex < 0)
                return null;

            return zone.Entities.FirstOrDefault(candidate => candidate.EntityIndex == entity.TargetEntityIndex);
        }

        private static int TryParseEntityIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return -1;

            var marker = ".entity.";
            var markerIndex = key.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return -1;

            var start = markerIndex + marker.Length;
            var end = start;
            while (end < key.Length && char.IsDigit(key[end]))
                end++;

            return int.TryParse(key.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : -1;
        }

        private static string FormatPosition(AetheriaRuntimeEntitySnapshotCommit? entity)
        {
            if (entity == null)
                return "(none)";

            return string.Join(", ", new[]
            {
                entity.PositionX.ToString("0.###", CultureInfo.InvariantCulture),
                entity.PositionY.ToString("0.###", CultureInfo.InvariantCulture),
                entity.PositionZ.ToString("0.###", CultureInfo.InvariantCulture)
            });
        }

        private static int Count<T>(IReadOnlyCollection<T>? values)
        {
            return values?.Count ?? 0;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Join(IReadOnlyList<string>? values)
        {
            return values == null || values.Count == 0
                ? ""
                : string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string CommandName(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(kind);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value, string stateRef = "")
        {
            var props = string.IsNullOrWhiteSpace(stateRef)
                ? new[] { ("label", label), ("value", value ?? "") }
                : new[]
                {
                    ("label", label),
                    ("value", value ?? ""),
                    AetheriaRuntimeSurfaceStateRefs.SourceRef(stateRef)
                };
            return Node(id, "metric", props);
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent CommandButton(
            string id,
            string label,
            AetheriaRuntimeDaemonCommandKinds kind)
        {
            return Node(
                id,
                "control.button",
                new[]
                {
                    ("label", label),
                    ("command", CommandName(kind)),
                    ("commandBody", nameof(AetheriaRuntimeDaemonCommandDocument))
                });
        }

        private static AetheriaRuntimeSurfaceComponent Row(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }
}
