using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonGameSurfaceBuilder
    {
        public const string PilotSurfaceId = "aetheria.pilot";
        public const string CommanderSurfaceId = "aetheria.starbridge.commander";
        public const string SurfaceId = PilotSurfaceId;
        public const string TuiSurfaceId = "aetheria.pilot.tui";

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonHealthDocument health,
            AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary,
            string activeMainMenuSurfaceId = AetheriaRuntimeMainMenuCommands.RootSurfaceId)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            health ??= new AetheriaRuntimeDaemonHealthDocument();
            commandBoundary ??= AetheriaRuntimeDaemonCommandBoundaryDocument.Create(frame.DaemonId);
            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = FindCurrentZone(run);
            var entity = FindCurrentEntity(run, zone);
            var target = FindTargetEntity(zone, entity);
            var entityName = string.IsNullOrWhiteSpace(entity?.Name) ? "(no current entity)" : entity!.Name;
            activeMainMenuSurfaceId = NormalizeMainMenuSurfaceId(activeMainMenuSurfaceId);
            var surfaceChildren = new List<AetheriaRuntimeSurfaceComponent>
            {
                PlayableWorldSurface(
                    "aetheria.daemon.game.world",
                    run,
                    zone,
                    run.CurrentEntityKey,
                    frame.SimulationSettings),
                FeedbackStream(run, frame.FrameId),
                GravityFieldSurface("aetheria.daemon.game.field"),
                MainMenuOverlay("aetheria.daemon.game.main_menu", activeMainMenuSurfaceId),
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
                        CommandButton("aetheria.daemon.game.commands.tractor", "Scoop", AetheriaRuntimeDaemonCommandKinds.SetTractorPower),
                        CommandButton("aetheria.daemon.game.commands.dock", "Dock", AetheriaRuntimeDaemonCommandKinds.DockNearest),
                        CommandButton("aetheria.daemon.game.commands.undock", "Undock", AetheriaRuntimeDaemonCommandKinds.Undock),
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
                    SurfaceRoot(
                        "aetheria.daemon.game.root",
                        surfaceChildren.ToArray()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commandBoundary.Commands
                    .Where(entry => AetheriaRuntimeDaemonSurfaceCommandCatalog.IsArgumentlessCommand(entry.Kind))
                    .Select(entry => new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(entry.Kind),
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.Label(entry.Kind),
                        "cultmesh"))
                    .Concat(new[]
                    {
                        new AetheriaRuntimeSurfaceCommandTemplate("aetheria.main_menu.root.continue", "Continue", "cultmesh"),
                        new AetheriaRuntimeSurfaceCommandTemplate("aetheria.main_menu.root.new_game", "New Game", "cultmesh"),
                        new AetheriaRuntimeSurfaceCommandTemplate("aetheria.main_menu.root.show_settings", "Settings", "cultmesh"),
                        new AetheriaRuntimeSurfaceCommandTemplate("aetheria.main_menu.root.quit", "Quit", "cultmesh")
                    })
                    .ToArray());
        }

        public static AetheriaRuntimeSurfaceDocument BuildCommander(
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

            var root = SurfaceRoot(
                "aetheria.starbridge.commander.root",
                StrategicWorldSurface("aetheria.starbridge.commander.world", run, zone),
                BuildAgentRoster(run),
                BuildAgentTaskBoard(run),
                BuildSurveyKnowledge(run),
                BuildStarbridgeStationStockCard(starbridge),
                BuildStarbridgeWaveForecastCard(starbridge),
                BuildStarbridgeRuntimeRolesCard(starbridge));

            return new AetheriaRuntimeSurfaceDocument(
                "aetheria.daemon",
                "game.daemon",
                "Starbridge Commander",
                frame.FrameId,
                frame.PublishedAtUtc,
                new AetheriaRuntimeSurfaceTree(
                    CommanderSurfaceId,
                    root,
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commandBoundary.Commands
                    .Where(entry => AetheriaRuntimeDaemonSurfaceCommandCatalog.IsArgumentlessCommand(entry.Kind))
                    .Select(entry => new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(entry.Kind),
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.Label(entry.Kind),
                        "cultmesh"))
                    .Concat(new[]
                    {
                        new AetheriaRuntimeSurfaceCommandTemplate("aetheria.daemon.issue_agent_task", "Issue Task", "cultmesh"),
                        new AetheriaRuntimeSurfaceCommandTemplate("aetheria.daemon.cancel_agent_task", "Cancel Task", "cultmesh")
                    })
                    .ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent BuildAgentTaskBoard(AetheriaRuntimeRunCheckpointCommit run)
        {
            var tasks = (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null)
                .OrderBy(task => TaskStatusOrder(task.Status))
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => task.TaskId, StringComparer.Ordinal)
                .Select(task => Node(
                    $"aetheria.starbridge.commander.tasks.{SurfaceToken(task.TaskId)}",
                    "list.item",
                    new[]
                    {
                        ("label", task.TaskType),
                        ("taskId", task.TaskId),
                        ("corporation", task.CorporationKey),
                        ("status", task.Status),
                        ("priority", task.Priority.ToString(CultureInfo.InvariantCulture)),
                        ("zoneIndex", task.ZoneIndex.ToString(CultureInfo.InvariantCulture)),
                        ("assignedEntityIndex", task.AssignedEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("targetEntityIndex", task.TargetEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("originEntityIndex", task.OriginEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("itemKey", task.ItemKey),
                        ("requestedQuantity", task.RequestedQuantity.ToString(CultureInfo.InvariantCulture)),
                        ("deliveredQuantity", task.DeliveredQuantity.ToString(CultureInfo.InvariantCulture)),
                        ("phase", task.Phase),
                        ("targetBodyKeys", string.Join(",", task.TargetBodyKeys ?? Array.Empty<string>())),
                        ("circuitIndex", task.CircuitIndex.ToString(CultureInfo.InvariantCulture)),
                        ("orbitParentKey", task.OrbitParentKey),
                        ("orbitDistance", task.OrbitDistance.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("targetPosition", string.Join(",", new[]
                        {
                            task.TargetPositionX.ToString("0.###", CultureInfo.InvariantCulture),
                            task.TargetPositionZ.ToString("0.###", CultureInfo.InvariantCulture)
                        })),
                        ("cancelCommand", "aetheria.daemon.cancel_agent_task")
                    }))
                .ToArray();
            return Node(
                "aetheria.starbridge.commander.tasks",
                "list",
                new[]
                {
                    ("title", "Orders"),
                    ("issueCommand", "aetheria.daemon.issue_agent_task"),
                    ("taskTypes", string.Join(",", AetheriaRuntimeAgentTaskTypes.All))
                },
                tasks);
        }

        private static AetheriaRuntimeSurfaceComponent BuildAgentRoster(AetheriaRuntimeRunCheckpointCommit run)
        {
            var agents = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null && (entity.AgentTaskCapabilities ?? Array.Empty<string>()).Count > 0)
                    .Select(entity => (Zone: zone, Entity: entity)))
                .OrderBy(value => value.Entity.Name, StringComparer.Ordinal)
                .Select(value =>
                {
                    var controlled = AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(
                        run.CurrentEntityKey, out var controlledZone, out var controlledEntity) &&
                        controlledZone == value.Zone.ZoneIndex && controlledEntity == value.Entity.EntityIndex;
                    var home = FindEntityById(run, value.Entity.HomeEntityId);
                    var atHome = home.Entity != null && home.Zone != null &&
                        home.Zone.ZoneIndex == value.Zone.ZoneIndex &&
                        (home.Entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(value.Entity.EntityIndex);
                    var hasHome = !string.IsNullOrWhiteSpace(value.Entity.HomeEntityId);
                    var status = !value.Entity.IsActive ? "offline" : controlled ? "manual" :
                        !string.IsNullOrWhiteSpace(value.Entity.AssignedAgentTaskId) ? "working" : atHome ? "home" : hasHome ? "returning" : "idle";
                    var detail = !string.IsNullOrWhiteSpace(value.Entity.AssignedAgentTaskId)
                        ? $"Assigned {value.Entity.AssignedAgentTaskId}"
                        : atHome ? "Docked at home" : controlled ? "Manual helm" : hasHome ? "Returning home" : "Awaiting orders";
                    return Node(
                        $"aetheria.starbridge.commander.agents.{SurfaceToken(value.Entity.EntityId)}",
                        "agent.item",
                        new[]
                        {
                            ("label", value.Entity.Name),
                            ("status", status),
                            ("detail", detail),
                            ("badges", string.Join(",", value.Entity.AgentTaskCapabilities ?? Array.Empty<string>())),
                            ("entityId", value.Entity.EntityId),
                            ("zoneIndex", value.Zone.ZoneIndex.ToString(CultureInfo.InvariantCulture)),
                            ("entityIndex", value.Entity.EntityIndex.ToString(CultureInfo.InvariantCulture)),
                            ("capabilities", string.Join(",", value.Entity.AgentTaskCapabilities ?? Array.Empty<string>())),
                            ("assignedTaskId", value.Entity.AssignedAgentTaskId),
                            ("homeEntityId", value.Entity.HomeEntityId),
                            ("controlled", controlled ? "true" : "false"),
                            ("active", value.Entity.IsActive ? "true" : "false"),
                            ("atHome", atHome ? "true" : "false")
                        });
                })
                .ToArray();
            return Node("aetheria.starbridge.commander.agents", "agent.roster", new[] { ("title", "Workers") }, agents);
        }

        private static (AetheriaRuntimeZoneSnapshotCommit? Zone, AetheriaRuntimeEntitySnapshotCommit? Entity) FindEntityById(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
                return (null, null);
            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            foreach (var entity in zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                if (entity != null && string.Equals(entity.EntityId, entityId, StringComparison.Ordinal))
                    return (zone, entity);
            return (null, null);
        }

        private static AetheriaRuntimeSurfaceComponent FeedbackStream(AetheriaRuntimeRunCheckpointCommit run, long frameId)
        {
            var events = (run.GameEvents ?? Array.Empty<AetheriaRuntimeGameEventCommit>())
                .Where(value => value != null)
                .OrderBy(value => value.FrameId)
                .ThenBy(value => value.EventId, StringComparer.Ordinal)
                .Select(value => Node(
                    $"aetheria.daemon.game.feedback.{SurfaceToken(value.EventId)}",
                    "feedback.event",
                    new[]
                    {
                        ("eventId", value.EventId), ("eventKind", value.Kind),
                        ("frameId", value.FrameId.ToString(CultureInfo.InvariantCulture)),
                        ("zoneIndex", value.ZoneIndex.ToString(CultureInfo.InvariantCulture)),
                        ("sourceEntityIndex", value.SourceEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("targetEntityIndex", value.TargetEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("pickupIndex", value.PickupIndex.ToString(CultureInfo.InvariantCulture)),
                        ("itemKey", value.ItemKey), ("scalarValue", FormatNumber(value.ScalarValue)),
                        ("subjectKey", value.SubjectKey),
                        ("position", string.Join(",", new[] { FormatNumber(value.PositionX), FormatNumber(value.PositionZ) })),
                        ("currentFrameId", frameId.ToString(CultureInfo.InvariantCulture))
                    }))
                .ToArray();
            return Node("aetheria.daemon.game.feedback", "feedback.stream", new[] { ("retainedCount", events.Length.ToString(CultureInfo.InvariantCulture)) }, events);
        }

        private static AetheriaRuntimeSurfaceComponent BuildSurveyKnowledge(AetheriaRuntimeRunCheckpointCommit run)
        {
            var entries = (run.CorporationSurveys ?? Array.Empty<AetheriaRuntimeCorporationSurveyCommit>())
                .Where(value => value != null)
                .OrderBy(value => value.CorporationKey, StringComparer.Ordinal)
                .ThenBy(value => value.BodyKey, StringComparer.Ordinal)
                .Select(value => Node(
                    $"aetheria.starbridge.commander.surveys.{SurfaceToken(value.CorporationKey)}.{SurfaceToken(value.BodyKey)}",
                    "list.item",
                    new[]
                    {
                        ("label", value.BodyKey),
                        ("corporation", value.CorporationKey),
                        ("bodyKey", value.BodyKey),
                        ("densityFloor", value.DensityFloor.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("completedFrameId", value.CompletedFrameId.ToString(CultureInfo.InvariantCulture))
                    }))
                .ToArray();
            return Node("aetheria.starbridge.commander.surveys", "list", new[] { ("title", "Survey Knowledge") }, entries);
        }

        private static int TaskStatusOrder(string status) =>
            string.Equals(status, AetheriaRuntimeAgentTaskStatuses.Assigned, StringComparison.Ordinal) ? 0 :
            string.Equals(status, AetheriaRuntimeAgentTaskStatuses.Queued, StringComparison.Ordinal) ? 1 : 2;

        private static string SurfaceToken(string value) => new string(
            (value ?? "").Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

        private static AetheriaRuntimeSurfaceComponent StrategicWorldSurface(
            string id,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = string.IsNullOrWhiteSpace(zone.Name) ? "Starbridge" : zone.Name,
                ["statePointerId"] = AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                ["assetManifest"] = AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString(),
                ["inputCapability"] = AetheriaRuntimeVerseRecordKeys.PilotInputCapability.ToString(),
                ["inputProfile"] = "rts.pointer-keyboard.v1",
                ["cameraRig"] = "rts.top-down.v1",
                ["zoneIndex"] = zone.ZoneIndex.ToString(CultureInfo.InvariantCulture),
                ["runId"] = run.RunId ?? ""
            };
            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null && entity.IsActive)
                .Select(entity => PlayableWorldEntity(
                    entity,
                    run,
                    zone,
                    run.CurrentEntityKey,
                    AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault))
                .ToArray();
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "world.scene2d",
                props,
                entities,
                AetheriaRuntimeSurfaceStateBindings.FromProps(props),
                Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                Layout(("position", "absolute"), ("inset", "0"), ("width", "100%"), ("height", "100%")),
                new Dictionary<string, string> { ["background"] = "transparent" });
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

        private static AetheriaRuntimeSurfaceComponent PlayableWorldSurface(
            string id,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string currentEntityKey,
            AetheriaRuntimeDaemonSimulationSettings simulationSettings)
        {
            run ??= new AetheriaRuntimeRunCheckpointCommit();
            zone ??= new AetheriaRuntimeZoneSnapshotCommit();
            var playerEntityId = PlayableWorldEntityId(run, zone, currentEntityKey);
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = string.IsNullOrWhiteSpace(zone.Name) ? "Aetheria World" : zone.Name,
                ["statePointerId"] = AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                ["assetManifest"] = AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString(),
                ["inputProfile"] = "arpg.pointer-keyboard.v1",
                ["cameraRig"] = "arpg.orbital-follow.v1",
                ["viewId"] = "pilot",
                ["playerEntityId"] = playerEntityId,
                ["movementCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetMoveVector),
                ["focusCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.TargetNearest),
                ["targetCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetTarget),
                ["actionCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup),
                ["tractorCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetTractorPower),
                ["dockCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.DockNearest),
                ["undockCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.Undock),
                ["zoneIndex"] = zone.ZoneIndex.ToString(CultureInfo.InvariantCulture),
                ["runId"] = run.RunId ?? ""
            };

            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(candidate => candidate != null && candidate.IsActive)
                .Select(candidate => PlayableWorldEntity(
                    candidate,
                    run,
                    zone,
                    currentEntityKey,
                    simulationSettings))
                .Concat((zone.Projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>())
                    .Where(projectile => projectile != null && projectile.LifetimeSeconds > 0)
                    .Select(projectile => PlayableWorldProjectile(projectile, run, zone)))
                .Concat((zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                    .Where(pickup => pickup != null && pickup.AgeSeconds < pickup.LifetimeSeconds)
                    .Select(pickup => PlayableWorldPickup(pickup, run, zone)))
                .ToArray();

            return new AetheriaRuntimeSurfaceComponent(
                id,
                "world.scene3d",
                props,
                entities,
                AetheriaRuntimeSurfaceStateBindings.FromProps(props),
                Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                Layout(
                    ("position", "absolute"),
                    ("top", "0"),
                    ("right", "0"),
                    ("bottom", "0"),
                    ("left", "0"),
                    ("width", "100%"),
                    ("height", "100%")),
                new Dictionary<string, string>
                {
                    ["background"] = "transparent"
                });
        }

        private static AetheriaRuntimeSurfaceComponent PlayableWorldEntity(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string currentEntityKey,
            AetheriaRuntimeDaemonSimulationSettings simulationSettings)
        {
            var entityId = run.EntityRecordKey(zone.ZoneIndex, entity.EntityIndex);
            var playerEntityId = PlayableWorldEntityId(run, zone, currentEntityKey);
            var player = FindCurrentEntity(run, zone);
            var targetEntityId = entity.TargetEntityIndex < 0
                ? ""
                : run.EntityRecordKey(zone.ZoneIndex, entity.TargetEntityIndex);
            var hull = EntityStat(entity, "hull");
            var shield = EntityStat(entity, "shield");
            var heat = EntityStat(entity, "heat");
            var minimumTemperature = EntityStat(entity, AetheriaRuntimeThermalSimulation.MinimumTemperatureGrid);
            var maximumTemperature = EntityStat(entity, AetheriaRuntimeThermalSimulation.MaximumTemperatureGrid);
            var thermalVisibility = EntityStat(entity, "thermal-visibility");
            var maximumHull = MaximumHull(entity, simulationSettings);
            var maximumShield = MaximumShield(entity, simulationSettings);
            var weaponCooldown = (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                .Select(state => state?.CooldownProgress ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["entityId"] = entityId,
                ["entityKind"] = entity.Kind ?? "",
                ["label"] = string.IsNullOrWhiteSpace(entity.Name) ? entityId : entity.Name,
                ["faction"] = entity.FactionKey ?? "",
                ["providerEntityId"] = entity.EntityId ?? "",
                ["homeEntityId"] = entity.HomeEntityId ?? "",
                ["agentCapabilities"] = string.Join(",", entity.AgentTaskCapabilities ?? Array.Empty<string>()),
                ["assignedTaskId"] = entity.AssignedAgentTaskId ?? "",
                ["assetRef"] = PlayableWorldAssetRef(entity),
                ["position"] = FormatPosition(entity),
                ["rotationY"] = HeadingYaw(entity).ToString("0.###", CultureInfo.InvariantCulture),
                ["radius"] = PlayableWorldRadius(entity).ToString("0.###", CultureInfo.InvariantCulture),
                ["hull"] = FormatNumber(hull),
                ["maximumHull"] = FormatNumber(maximumHull),
                ["hullRatio"] = FormatRatio(hull, maximumHull),
                ["shield"] = FormatNumber(shield),
                ["maximumShield"] = FormatNumber(maximumShield),
                ["shieldRatio"] = FormatRatio(shield, maximumShield),
                ["heat"] = FormatNumber(heat),
                ["meanTemperature"] = FormatNumber(heat),
                ["minimumTemperature"] = FormatNumber(minimumTemperature),
                ["maximumTemperature"] = FormatNumber(maximumTemperature),
                ["thermalVisibility"] = FormatNumber(thermalVisibility),
                ["heatstroke"] = FormatRatio(entity.Heatstroke, 1),
                ["hypothermia"] = FormatRatio(entity.Hypothermia, 1),
                ["visibility"] = FormatNumber(entity.Visibility),
                ["targetEntityId"] = targetEntityId,
                ["targetedByPlayer"] = player != null && player.TargetEntityIndex == entity.EntityIndex ? "true" : "false",
                ["weaponCooldown"] = FormatNumber(weaponCooldown),
                ["selectable"] = "true",
                ["controllable"] = string.Equals(entityId, playerEntityId, StringComparison.Ordinal) ? "true" : "false",
                ["focusCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.TargetNearest),
                ["moveCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetMoveVector),
                ["targetCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetTarget),
                ["actionCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup)
            };
            if (entity.LoadoutGeneration != null)
            {
                props["loadoutGenerationSeed"] = entity.LoadoutGeneration.Seed.ToString(CultureInfo.InvariantCulture);
                props["loadoutGenerationZone"] = entity.LoadoutGeneration.SourceZoneIndex.ToString(CultureInfo.InvariantCulture);
                props["loadoutAvailabilityFaction"] = entity.LoadoutGeneration.AvailabilityFactionKey ?? "";
                props["loadoutPriceExponent"] = entity.LoadoutGeneration.PriceExponent.ToString("0.###", CultureInfo.InvariantCulture);
            }

            return new AetheriaRuntimeSurfaceComponent(
                $"aetheria.daemon.game.world.entity.{entity.EntityIndex}",
                "world.entity3d",
                props,
                LoadoutGenerationItems(entity));
        }

        private static AetheriaRuntimeSurfaceComponent[] LoadoutGenerationItems(
            AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return (entity.LoadoutGeneration?.Selections ?? Array.Empty<AetheriaRuntimeLoadoutGenerationSelectionCommit>())
                .Select((value, index) => new AetheriaRuntimeSurfaceComponent(
                    $"aetheria.daemon.game.world.entity.{entity.EntityIndex}.loadout.{index}",
                    "loadout.item",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["label"] = value.ItemKey ?? "",
                        ["status"] = value.Role ?? "",
                        ["detail"] = $"{value.ManufacturerKey} | {value.Price} credits | distance {value.ManufacturerDistance}",
                        ["badges"] = $"allegiance:{value.Allegiance:0.###}",
                        ["itemKey"] = value.ItemKey ?? "",
                        ["manufacturerKey"] = value.ManufacturerKey ?? "",
                        ["price"] = value.Price.ToString(CultureInfo.InvariantCulture),
                        ["manufacturerDistance"] = value.ManufacturerDistance.ToString(CultureInfo.InvariantCulture),
                        ["allegiance"] = value.Allegiance.ToString("0.###", CultureInfo.InvariantCulture)
                    },
                    Array.Empty<AetheriaRuntimeSurfaceComponent>()))
                .ToArray();
        }

        private static AetheriaRuntimeSurfaceComponent PlayableWorldProjectile(
            AetheriaRuntimeProjectileCommit projectile,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var projectileId = $"{run.RunId}:zone:{zone.ZoneIndex}:projectile:{projectile.ProjectileId}";
            var targetEntityId = projectile.TargetEntityIndex < 0
                ? ""
                : run.EntityRecordKey(zone.ZoneIndex, projectile.TargetEntityIndex);
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["entityId"] = projectileId,
                ["entityKind"] = "projectile",
                ["label"] = projectile.WeaponKind ?? "Projectile",
                ["faction"] = "",
                ["assetRef"] = "prefab.entity.projectile",
                ["position"] = string.Join(",", new[]
                {
                    projectile.PositionX.ToString("0.###", CultureInfo.InvariantCulture),
                    "0",
                    projectile.PositionZ.ToString("0.###", CultureInfo.InvariantCulture)
                }),
                ["rotationY"] = HeadingDegrees(projectile.VelocityX, projectile.VelocityY).ToString("0.###", CultureInfo.InvariantCulture),
                ["radius"] = Math.Max(0.01, projectile.Radius).ToString("0.###", CultureInfo.InvariantCulture),
                ["sourceEntityId"] = run.EntityRecordKey(zone.ZoneIndex, projectile.SourceEntityIndex),
                ["targetEntityId"] = targetEntityId,
                ["damage"] = FormatNumber(projectile.Damage),
                ["remainingLifetime"] = FormatNumber(projectile.LifetimeSeconds),
                ["selectable"] = "false",
                ["controllable"] = "false"
            };
            return new AetheriaRuntimeSurfaceComponent(
                $"aetheria.daemon.game.world.projectile.{projectile.ProjectileId}",
                "world.entity3d",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static AetheriaRuntimeSurfaceComponent PlayableWorldPickup(
            AetheriaRuntimeDroppedPickupCommit pickup,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var pickupId = $"{run.RunId}:zone:{zone.ZoneIndex}:pickup:{pickup.PickupIndex}";
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["entityId"] = pickupId,
                ["entityKind"] = "pickup",
                ["label"] = pickup.Item?.ItemKey ?? "Pickup",
                ["itemKey"] = pickup.Item?.ItemKey ?? "",
                ["quantity"] = Math.Max(1, pickup.Item?.Quantity ?? 1).ToString(CultureInfo.InvariantCulture),
                ["assetRef"] = "prefab.entity.pickup",
                ["position"] = string.Join(",", new[] { pickup.PositionX.ToString("0.###", CultureInfo.InvariantCulture), pickup.PositionY.ToString("0.###", CultureInfo.InvariantCulture), pickup.PositionZ.ToString("0.###", CultureInfo.InvariantCulture) }),
                ["velocity"] = string.Join(",", new[] { pickup.VelocityX.ToString("0.###", CultureInfo.InvariantCulture), pickup.VelocityY.ToString("0.###", CultureInfo.InvariantCulture), pickup.VelocityZ.ToString("0.###", CultureInfo.InvariantCulture) }),
                ["radius"] = "5",
                ["age"] = FormatNumber(pickup.AgeSeconds),
                ["lifetime"] = FormatNumber(pickup.LifetimeSeconds),
                ["remainingLifetime"] = FormatNumber(Math.Max(0, pickup.LifetimeSeconds - pickup.AgeSeconds)),
                ["selectable"] = "false",
                ["controllable"] = "false"
            };
            return new AetheriaRuntimeSurfaceComponent(
                $"aetheria.daemon.game.world.pickup.{pickup.PickupIndex}",
                "world.entity3d",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static string PlayableWorldAssetRef(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            if (entity == null)
                return "";

            var kind = (entity.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.Contains("station"))
                return "prefab.entity.station";
            if (kind.Contains("projectile"))
                return "prefab.entity.projectile";
            if (kind.Contains("orbital"))
                return "prefab.entity.orbital";
            if (string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase))
                return "prefab.entity.player";
            return "prefab.entity.ship";
        }

        private static double PlayableWorldRadius(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            var kind = (entity.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.Contains("station"))
                return 48.0;
            if (kind.Contains("projectile"))
                return Math.Max(1.0, entity.Visibility > 0.0 ? entity.Visibility : 3.0);
            return 12.0;
        }

        private static double HeadingYaw(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            if (entity == null)
                return 0.0;
            if (Math.Abs(entity.DirectionX) <= 0.0001 && Math.Abs(entity.DirectionY) <= 0.0001)
                return 0.0;
            return HeadingDegrees(entity.DirectionX, entity.DirectionY);
        }

        private static double HeadingDegrees(double x, double y) =>
            Math.Atan2(x, y) * (180.0 / Math.PI);

        private static double EntityStat(AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .FirstOrDefault(grid => string.Equals(grid?.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Values?.FirstOrDefault() ?? 0;

        private static double MaximumHull(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings) =>
            string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? (string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase)
                    ? settings.PlayerStationHull
                    : settings.HostileStationHull)
                : string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)
                    ? settings.RaiderEntityHull
                    : settings.PlayerEntityHull;

        private static double MaximumShield(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings) =>
            string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? settings.StationShield
                : settings.EntityShield;

        private static string FormatRatio(double value, double maximum) =>
            (maximum <= 0 ? 0 : Math.Max(0, Math.Min(1, value / maximum)))
            .ToString("0.###", CultureInfo.InvariantCulture);

        private static AetheriaRuntimeSurfaceComponent GravityFieldSurface(string id)
        {
            var viewport = DefaultViewport();
            var renderSplatsDocumentId = ViewportDocumentId("aetheria.viewport.render_splats", viewport);
            var gravityDocumentId = ViewportDocumentId("aetheria.viewport.gravity", viewport);
            var objectsDocumentId = ViewportDocumentId("aetheria.viewport.objects", viewport);
            var props = new[]
            {
                ("label", "Aetheria level field surface"),
                ("minX", F(viewport.MinX)),
                ("minY", F(viewport.MinY)),
                ("maxX", F(viewport.MaxX)),
                ("maxY", F(viewport.MaxY)),
                ("fieldModel", "gamecult.fields.surface2d.v1"),
                ("scalarField", "gravity.height"),
                ("scalarFieldSlot", "gravity"),
                ("scalarFieldSchemaId", AetheriaRuntimeDaemonSchemas.GravityViewport),
                ("scalarFieldDefaultVisualizer", "isolines.branchless"),
                ("scalarFieldVisualizers", "isolines.branchless,height-shade,probe"),
                ("scalarFieldLineInterval", "11.4398025"),
                ("scalarFieldBaseColor", "0.002,0.006,0.012"),
                ("scalarFieldGlowColor", "0.018,0.050,0.075"),
                ("scalarFieldLowLineColor", "0.000,0.340,0.520"),
                ("scalarFieldHighLineColor", "1.450,0.300,0.050"),
                ("scalarFieldLowAngleColor", "0.060,0.160,0.240"),
                ("scalarFieldHighAngleColor", "1.100,0.240,0.040"),
                ("vectorField", "nebula.tint"),
                ("vectorFieldSlot", "renderSplats"),
                ("vectorFieldLayer", AetheriaRuntimeRenderSplatLayerKeys.FogTint),
                ("vectorFieldSchemaId", AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                ("vectorFieldDefaultVisualizer", "color.powerpulse"),
                ("vectorFieldVisualizers", "color.powerpulse,probe"),
                ("vectorFieldTintScale", "0.45"),
                ("objectFieldSlot", "objects"),
                ("objectFieldSchemaId", AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                ("bodyIconMinPx", "24"),
                ("sunIconMinPx", "34"),
                ("bodyIconScale", "0.48"),
                ("sunIconScale", "0.72"),
                ("bodyLabelColor", "rgba(226, 244, 255, 0.82)"),
                ("objectLabelFont", "700 12px Ubuntu, system-ui, sans-serif"),
                ("objectLabelStroke", "rgba(0, 0, 0, 0.72)"),
                ("objectLabelStrokeWidth", "3"),
                ("objectControlledColor", "rgba(122, 240, 255, {alpha})"),
                ("objectRaiderColor", "rgba(255, 143, 74, {alpha})"),
                ("objectNeutralColor", "rgba(232, 232, 224, {alpha})"),
                ("objectDefaultColor", "rgba(214, 244, 255, {alpha})"),
                ("shipIconSizePx", "22"),
                ("remoteShipIconSizePx", "18"),
                ("stationIconSizePx", "34"),
                ("renderSplatsDocumentId", renderSplatsDocumentId),
                ("renderSplatsSchemaId", AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                ("gravityDocumentId", gravityDocumentId),
                ("gravitySchemaId", AetheriaRuntimeDaemonSchemas.GravityViewport),
                ("objectsDocumentId", objectsDocumentId),
                ("objectsSchemaId", AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                ("samplesX", "196"),
                ("shader", "aetheria.field-surface2d.v1"),
                ("stateRefreshMs", "50")
            };
            var normalizedProps = props.ToDictionary(prop => prop.Item1, prop => prop.Item2 ?? "", StringComparer.Ordinal);
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "field.surface2d",
                normalizedProps,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(normalizedProps),
                new[]
                {
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "renderSplats",
                        renderSplatsDocumentId,
                        AetheriaRuntimeDaemonSchemas.RenderSplatsViewport,
                        "data"),
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "gravity",
                        gravityDocumentId,
                        AetheriaRuntimeDaemonSchemas.GravityViewport,
                        "data"),
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "objects",
                        objectsDocumentId,
                        AetheriaRuntimeDaemonSchemas.ObjectsViewport,
                        "data")
                },
                Layout(
                    ("position", "absolute"),
                    ("top", "0"),
                    ("right", "0"),
                    ("bottom", "0"),
                    ("left", "0"),
                    ("width", "100%"),
                    ("height", "100%")),
                null);
        }

        private static AetheriaRuntimeSurfaceComponent MainMenuOverlay(
            string id,
            string activeSurfaceId)
        {
            activeSurfaceId = NormalizeMainMenuSurfaceId(activeSurfaceId);
            var activeSurfaceRecordRef = $"eve:surface:{activeSurfaceId}";
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "surface.slot",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["slotId"] = "mainMenuPanel",
                    ["documentId"] = activeSurfaceRecordRef,
                    ["schemaId"] = "gamecult.eve.surface.v1",
                    ["presentationKind"] = "menu.overlay"
                },
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(new Dictionary<string, string>(StringComparer.Ordinal)),
                new[]
                {
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "mainMenuPanel",
                        activeSurfaceRecordRef,
                        "gamecult.eve.surface.v1",
                        "menu.overlay")
                },
                Layout(
                    ("position", "absolute"),
                    ("top", "0"),
                    ("right", "0"),
                    ("bottom", "0"),
                    ("left", "0"),
                    ("width", "100%"),
                    ("height", "100%"),
                    ("zIndex", "4"),
                    ("pointerEvents", "auto")),
                new Dictionary<string, string>
                {
                    ["background"] = "rgba(0,0,0,0)"
                });
        }

        private static string NormalizeMainMenuSurfaceId(string surfaceId)
        {
            switch (surfaceId ?? "")
            {
                case AetheriaRuntimeMainMenuCommands.SettingsSurfaceId:
                case AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId:
                case AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId:
                case AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId:
                    return surfaceId;
                default:
                    return AetheriaRuntimeMainMenuCommands.RootSurfaceId;
            }
        }

        private static AetheriaRuntimeViewportBounds DefaultViewport()
        {
            return new AetheriaRuntimeViewportBounds
            {
                MinX = -1500,
                MinY = -1000,
                MaxX = 1500,
                MaxY = 1000
            };
        }

        private static string PlayableWorldEntityId(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string currentEntityKey)
        {
            if (run == null)
                return currentEntityKey ?? "";

            if (AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(
                    currentEntityKey ?? "",
                    out var zoneIndex,
                    out var entityIndex) &&
                IsPlayerEntity(zone, entityIndex))
            {
                return run.EntityRecordKey(zoneIndex, entityIndex);
            }

            var parsedEntityIndex = TryParseEntityIndex(currentEntityKey ?? "");
            if (parsedEntityIndex >= 0 && IsPlayerEntity(zone, parsedEntityIndex))
                return run.EntityRecordKey(zone?.ZoneIndex ?? run.CurrentZoneIndex, parsedEntityIndex);

            var player = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(IsControllablePlayerEntity);
            return player == null
                ? currentEntityKey ?? ""
                : run.EntityRecordKey(zone?.ZoneIndex ?? run.CurrentZoneIndex, player.EntityIndex);
        }

        private static bool IsPlayerEntity(AetheriaRuntimeZoneSnapshotCommit zone, int entityIndex)
        {
            return (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Any(entity => entity != null &&
                    entity.EntityIndex == entityIndex &&
                    IsControllablePlayerEntity(entity));
        }

        private static bool IsControllablePlayerEntity(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return entity != null &&
                entity.IsActive &&
                !string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase);
        }

        private static string ViewportDocumentId(string prefix, AetheriaRuntimeViewportBounds viewport)
        {
            var normalized = AetheriaRuntimeGameDocuments.Normalize(viewport);
            return string.Join(
                ".",
                prefix,
                ViewportToken(normalized.MinX),
                ViewportToken(normalized.MinY),
                ViewportToken(normalized.MaxX),
                ViewportToken(normalized.MaxY));
        }

        private static string ViewportToken(double value)
        {
            return value
                .ToString("0.###", CultureInfo.InvariantCulture)
                .Replace('-', 'n')
                .Replace('.', 'p');
        }

        private static IReadOnlyDictionary<string, string> Layout(params (string Key, string Value)[] values)
        {
            return values.ToDictionary(value => value.Item1, value => value.Item2 ?? "", StringComparer.Ordinal);
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
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

        private static AetheriaRuntimeSurfaceComponent TextNode(
            string id,
            string value,
            string role,
            IReadOnlyDictionary<string, string> layout,
            IReadOnlyDictionary<string, string> style)
        {
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["value"] = value ?? "",
                ["role"] = role ?? "text"
            };
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "text",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(props),
                Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                layout,
                style);
        }

        private static AetheriaRuntimeSurfaceComponent MenuButton(string id, string label, string command)
        {
            return Node(
                id,
                "control.button",
                new[]
                {
                    ("label", label),
                    ("command", command)
                });
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

        private static AetheriaRuntimeSurfaceComponent SurfaceRoot(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "surface",
                props,
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(props),
                Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                Layout(
                    ("position", "relative"),
                    ("overflow", "hidden"),
                    ("width", "100%"),
                    ("height", "100vh"),
                    ("minHeight", "100vh")),
                new Dictionary<string, string>
                {
                    ["background"] = "#020606"
                });
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
