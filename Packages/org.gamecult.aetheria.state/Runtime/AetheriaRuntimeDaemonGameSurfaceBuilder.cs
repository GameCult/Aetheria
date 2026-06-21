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
            AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            health ??= new AetheriaRuntimeDaemonHealthDocument();
            commandBoundary ??= AetheriaRuntimeDaemonCommandBoundaryDocument.Create(frame.DaemonId);

            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = FindCurrentZone(run);
            var entity = FindCurrentEntity(run, zone);
            var target = FindTargetEntity(zone, entity);
            var entityName = string.IsNullOrWhiteSpace(entity?.Name) ? "(no current entity)" : entity!.Name;

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
                        Node(
                            "aetheria.daemon.game.frame",
                            "card",
                            new[] { ("title", "Daemon Frame") },
                            Metric("aetheria.daemon.game.frame.daemon", "Daemon", frame.DaemonId),
                            Metric("aetheria.daemon.game.frame.verse", "Verse", health.VerseId),
                            Metric("aetheria.daemon.game.frame.frameId", "Frame", frame.FrameId.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.frame.time", "Time", frame.SimulationTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.frame.status", "Status", health.Status),
                            Metric("aetheria.daemon.game.frame.observed_commands", "Observed Commands", health.ObservedCommandCount.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.frame.applied", "Applied", health.AppliedCommandCount.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.frame.rejected", "Rejected", health.RejectedCommandCount.ToString(CultureInfo.InvariantCulture))),
                        Node(
                            "aetheria.daemon.game.player",
                            "card",
                            new[] { ("title", "Current Entity") },
                            Metric("aetheria.daemon.game.player.run", "Run", run.RunId),
                            Metric("aetheria.daemon.game.player.zone", "Zone", run.CurrentZoneIndex.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.player.key", "Entity Key", run.CurrentEntityKey),
                            Metric("aetheria.daemon.game.player.name", "Name", entityName),
                            Metric("aetheria.daemon.game.player.position", "Position", FormatPosition(entity)),
                            Metric("aetheria.daemon.game.player.target", "Target", string.IsNullOrWhiteSpace(target?.Name) ? "(none)" : target!.Name),
                            Metric("aetheria.daemon.game.player.equipment", "Equipment", Count(entity?.Equipment).ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.player.cargo", "Cargo Bays", Count(entity?.CargoContents).ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.game.player.weaponGroups", "Weapon Groups", Count(entity?.WeaponGroups).ToString(CultureInfo.InvariantCulture))),
                        Node(
                            "aetheria.daemon.game.commands",
                            "card",
                            new[] { ("title", "Typed Command Boundary") },
                            Metric(
                                "aetheria.daemon.game.commands.boundary",
                                "Boundary",
                                commandBoundary.BoundaryId),
                            Metric(
                                "aetheria.daemon.game.commands.count",
                                "Commands",
                                Count(commandBoundary.Commands).ToString(CultureInfo.InvariantCulture)),
                            Row(
                                "aetheria.daemon.game.commands.primary",
                                CommandButton("aetheria.daemon.game.commands.move", "Move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector),
                                CommandButton("aetheria.daemon.game.commands.target", "Target Nearest", AetheriaRuntimeDaemonCommandKinds.TargetNearest),
                                CommandButton("aetheria.daemon.game.commands.fire", "Fire", AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup),
                                CommandButton("aetheria.daemon.game.commands.ping", "Sensor Ping", AetheriaRuntimeDaemonCommandKinds.SensorPing)))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commandBoundary.Commands
                    .Select(entry => new AetheriaRuntimeSurfaceCommandTemplate(
                        CommandName(entry.Kind),
                        Label(entry.Kind),
                        "cultmesh"))
                    .ToArray());
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

        private static string CommandName(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return "aetheria.daemon.commands." + kind;
        }

        private static string Label(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return kind.ToString();
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value ?? "") });
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
