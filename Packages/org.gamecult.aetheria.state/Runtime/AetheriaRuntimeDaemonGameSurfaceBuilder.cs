using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

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
            string activeMainMenuSurfaceId = AetheriaRuntimeMainMenuCommands.RootSurfaceId,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
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
                    frame.SimulationSettings,
                    catalog),
                CockpitOverlay(entity, target, frame.SimulationSettings),
                FeedbackStream(run, frame.FrameId),
                ShotReceiptStream(run),
                GravityFieldSurface("aetheria.daemon.game.field"),
                MainMenuOverlay("aetheria.daemon.game.main_menu", activeMainMenuSurfaceId),
                Hidden(Node(
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
                    Metric("aetheria.daemon.game.frame.rejected", "Rejected", health.RejectedCommandCount.ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.FrameRejectedCommands))),
                Hidden(Node(
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
                    Metric("aetheria.daemon.game.player.weaponGroups", "Weapon Groups", Count(entity?.WeaponGroups).ToString(CultureInfo.InvariantCulture), AetheriaRuntimeDaemonStateRefs.CurrentWeaponGroupCount))),
                Hidden(Node(
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
                        CommandButton("aetheria.daemon.game.commands.ping", "Sensor Ping", AetheriaRuntimeDaemonCommandKinds.SensorPing))))
            };
            if (entity != null)
                surfaceChildren.Add(Node(
                    "aetheria.daemon.game.weapon-state",
                    "state.group",
                    Array.Empty<(string Key, string Value)>(),
                    WeaponStateItems(entity, run, zone)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.daemon",
                title: "Aetheria Daemon",
                version: frame.FrameId,
                updatedAtUtc: frame.PublishedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    PilotSurfaceRoot(
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
                BuildStarbridgeSessionCard(starbridge),
                BuildStarbridgeStationStockCard(starbridge),
                BuildStarbridgeWaveForecastCard(starbridge),
                BuildStarbridgeRuntimeRolesCard(starbridge));

            return new AetheriaRuntimeSurfaceDocument(
                "aetheria",
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

        private static AetheriaRuntimeSurfaceComponent BuildStarbridgeSessionCard(
            AetheriaRuntimeStarbridgeSessionSummaryDocument starbridge)
        {
            starbridge ??= new AetheriaRuntimeStarbridgeSessionSummaryDocument();
            return Node(
                "aetheria.starbridge.commander.session",
                "card",
                new[] { ("title", "Starbridge Session") },
                Metric("aetheria.starbridge.commander.session.scenario", "Scenario", starbridge.ScenarioName),
                Metric("aetheria.starbridge.commander.session.id", "Session", starbridge.SessionId),
                Metric("aetheria.starbridge.commander.session.phase", "Phase", starbridge.Phase),
                Metric("aetheria.starbridge.commander.session.wave", "Wave",
                    starbridge.CurrentWaveIndex.ToString(CultureInfo.InvariantCulture)),
                Metric("aetheria.starbridge.commander.session.zone", "Zone", starbridge.ZoneName),
                Metric("aetheria.starbridge.commander.session.base", "Base", starbridge.BaseStatus?.DisplayName ?? ""),
                Metric("aetheria.starbridge.commander.session.base_hull", "Base Hull",
                    FormatNumber(starbridge.BaseStatus?.Hull ?? 0)),
                Metric("aetheria.starbridge.commander.session.base_shield", "Base Shield",
                    FormatNumber(starbridge.BaseStatus?.Shield ?? 0)),
                Metric("aetheria.starbridge.commander.session.base_heat", "Base Heat",
                    FormatNumber(starbridge.BaseStatus?.Heat ?? 0)));
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
                        ("auxiliaryValue", FormatNumber(value.AuxiliaryValue)),
                        ("cargoQuantityBefore", PickupCargoQuantity(value, after: false)),
                        ("cargoQuantityAfter", PickupCargoQuantity(value, after: true)),
                        ("subjectKey", value.SubjectKey), ("reason", value.Reason),
                        ("position", string.Join(",", new[] { FormatNumber(value.PositionX), FormatNumber(value.PositionZ) })),
                        ("currentFrameId", frameId.ToString(CultureInfo.InvariantCulture))
                    }))
                .ToArray();
            return Node("aetheria.daemon.game.feedback", "feedback.stream", new[] { ("retainedCount", events.Length.ToString(CultureInfo.InvariantCulture)) }, events);
        }

        private static string PickupCargoQuantity(AetheriaRuntimeGameEventCommit gameEvent, bool after)
        {
            if (!string.Equals(gameEvent.Kind, "pickup.collected", StringComparison.Ordinal) &&
                !string.Equals(gameEvent.Kind, "pickup.rejected", StringComparison.Ordinal))
                return "";
            var before = Math.Max(0, gameEvent.AuxiliaryValue);
            var current = string.Equals(gameEvent.Kind, "pickup.collected", StringComparison.Ordinal)
                ? before + Math.Max(0, gameEvent.ScalarValue)
                : before;
            return FormatNumber(after ? current : before);
        }

        private static AetheriaRuntimeSurfaceComponent ShotReceiptStream(AetheriaRuntimeRunCheckpointCommit run)
        {
            var receipts = (run.ShotReceipts ?? Array.Empty<AetheriaRuntimeShotReceiptCommit>())
                .Where(value => value != null)
                .OrderBy(value => value.FrameId)
                .ThenBy(value => value.ShotId, StringComparer.Ordinal)
                .Select(value => Node(
                    $"aetheria.daemon.game.shots.{SurfaceToken(value.ShotId)}",
                    "shot.receipt",
                    new[]
                    {
                        ("shotId", value.ShotId), ("outcome", value.Outcome),
                        ("frameId", value.FrameId.ToString(CultureInfo.InvariantCulture)),
                        ("zoneIndex", value.ZoneIndex.ToString(CultureInfo.InvariantCulture)),
                        ("sourceEntityIndex", value.SourceEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("targetEntityIndex", value.TargetEntityIndex.ToString(CultureInfo.InvariantCulture)),
                        ("itemKey", value.WeaponItemKey),
                        ("contactInformation", FormatNumber(value.ContactInformation)),
                        ("lockQuality", FormatNumber(value.LockQuality)),
                        ("rangeFactor", FormatNumber(value.RangeFactor)),
                        ("motionFactor", FormatNumber(value.MotionFactor)),
                        ("dispersionFactor", FormatNumber(value.DispersionFactor)),
                        ("hitProbability", FormatNumber(value.HitProbability)),
                        ("hitRoll", FormatNumber(value.HitRoll)),
                        ("hit", value.Hit ? "true" : "false"),
                        ("nominalDamage", FormatNumber(value.NominalDamage)),
                        ("appliedDamage", FormatNumber(value.AppliedDamage)),
                        ("shieldAbsorbedDamage", FormatNumber(value.ShieldAbsorbedDamage)),
                        ("hullAppliedDamage", FormatNumber(value.HullAppliedDamage)),
                        ("shieldEnergyConsumed", FormatNumber(value.ShieldEnergyConsumed)),
                        ("shieldHeatGenerated", FormatNumber(value.ShieldHeatGenerated)),
                        ("damageType", value.DamageType),
                        ("penetration", FormatNumber(value.Penetration)),
                        ("damageSpread", FormatNumber(value.DamageSpread)),
                        ("armorAppliedDamage", FormatNumber(value.ArmorAppliedDamage)),
                        ("equipmentAppliedDamage", FormatNumber(value.EquipmentAppliedDamage)),
                        ("damageCellCount", (value.DamageCells?.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
                        ("origin", string.Join(",", FormatNumber(value.OriginX), FormatNumber(value.OriginZ))),
                        ("endpoint", string.Join(",", FormatNumber(value.EndpointX), FormatNumber(value.EndpointZ))),
                        ("presentationDuration", FormatNumber(value.PresentationDurationSeconds)),
                        ("presentationKind", value.PresentationKind),
                        ("presentationIntensity", FormatNumber(value.PresentationIntensity)),
                        ("impactKind", value.ImpactKind),
                        ("impactAngleRoll", FormatNumber(value.ImpactAngleRoll)),
                        ("impactRadiusRoll", FormatNumber(value.ImpactRadiusRoll))
                    }))
                .ToArray();
            return Node("aetheria.daemon.game.shots", "shot.receipt-stream",
                new[] { ("retainedCount", receipts.Length.ToString(CultureInfo.InvariantCulture)) }, receipts);
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
                ["entityViewPointerId"] = AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest.ToString(),
                ["entityViewSchema"] = EveEntitySoaViewDocument.SchemaId,
                ["zoneRenderPointerId"] = AetheriaRuntimeVerseRecordKeys.ZoneRenderLatest.ToString(),
                ["zoneRenderSchema"] = AetheriaRuntimeDaemonSchemas.ZoneRender,
                ["assetManifest"] = AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString(),
                ["inputCapability"] = AetheriaRuntimeVerseRecordKeys.PilotInputCapability.ToString(),
                ["inputProfile"] = "rts.pointer-keyboard.v1",
                ["cameraRig"] = "rts.top-down.v1",
                ["zoneIndex"] = zone.ZoneIndex.ToString(CultureInfo.InvariantCulture),
                ["runId"] = run.RunId ?? ""
            };
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "world.scene2d",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
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
            AetheriaRuntimeDaemonSimulationSettings simulationSettings,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            run ??= new AetheriaRuntimeRunCheckpointCommit();
            zone ??= new AetheriaRuntimeZoneSnapshotCommit();
            var playerEntityId = PlayableWorldEntityId(run, zone, currentEntityKey);
            var playerEntityIndex = TryParseEntityIndex(playerEntityId);
            var dockParent = FindDockParent(zone, playerEntityIndex);
            var isDocked = dockParent != null;
            var cameraTargetEntityId = isDocked
                ? run.EntityRecordKey(zone.ZoneIndex, dockParent!.EntityIndex)
                : playerEntityId;
            var stellarAmbient = ResolveStellarAmbient(zone);
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = string.IsNullOrWhiteSpace(zone.Name) ? "Aetheria World" : zone.Name,
                ["statePointerId"] = AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                ["entityViewPointerId"] = AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest.ToString(),
                ["entityViewSchema"] = EveEntitySoaViewDocument.SchemaId,
                ["zoneRenderPointerId"] = AetheriaRuntimeVerseRecordKeys.ZoneRenderLatest.ToString(),
                ["zoneRenderSchema"] = AetheriaRuntimeDaemonSchemas.ZoneRender,
                ["assetManifest"] = AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString(),
                ["inputCapability"] = AetheriaRuntimeVerseRecordKeys.PilotInputCapability.ToString(),
                ["inputProfile"] = "arpg.pointer-keyboard.v1",
                ["cameraRig"] = isDocked
                    ? "planar.top-down-follow.v1"
                    : "perspective.entity-forward-follow.v1",
                ["cameraLookAt"] = isDocked ? "" : "aim.convergence-point.v1",
                ["cameraDistance"] = isDocked ? "70" : "30",
                ["cameraVerticalFieldOfViewDegrees"] = "60",
                ["cameraTargetScreenX"] = isDocked ? "0.66" : "0.64",
                // Cinemachine serializes screen Y from the top edge; Eve camera
                // surfaces use Unity viewport coordinates measured from the bottom.
                ["cameraTargetScreenY"] = isDocked ? "0.45" : "0.19",
                ["cameraPositionDamping"] = isDocked ? "2" : "0",
                ["cameraNearClipPlane"] = isDocked ? "0.3" : "1",
                ["cameraFarClipPlane"] = isDocked ? "2048" : "4096",
                ["ambientLightColor"] = string.Join(",", F(stellarAmbient.X), F(stellarAmbient.Y), F(stellarAmbient.Z)),
                ["ambientLightIntensity"] = "1.46",
                ["reflectionAssetRef"] = "texture.environment.reflection",
                ["reflectionIntensity"] = "1",
                ["postProcessProfileAssetRef"] = "profile.environment.flight",
                ["colorGradingSpace"] = "hdr-before-tonemap.v1",
                ["exposureMode"] = "histogram.v1",
                ["exposureLowPercent"] = "47.37294",
                ["exposureHighPercent"] = "99",
                ["exposureMinimumEv"] = "-3",
                ["exposureMaximumEv"] = "0.3",
                ["exposureKeyValue"] = "0.5",
                ["exposureAdaptation"] = "progressive",
                ["exposureSpeedUp"] = "2",
                ["exposureSpeedDown"] = "1",
                ["cameraReconstruction"] = "temporal-reprojection.v1",
                ["temporalQuality"] = "high",
                ["temporalHistoryBlend"] = "0.99",
                ["temporalJitterScale"] = "0.1",
                ["temporalSharpening"] = "0",
                ["viewId"] = "pilot",
                ["excludedRenderChannels"] = "map",
                ["playerEntityId"] = playerEntityId,
                ["cameraTargetEntityId"] = cameraTargetEntityId,
                ["subjectVisible"] = isDocked ? "false" : "true",
                ["movementEnabled"] = isDocked ? "false" : "true",
                ["presentationMode"] = isDocked ? "docked" : "world",
                ["movementCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetMoveVector),
                ["lookCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetLookDirection),
                ["lookModel"] = "planar-yaw.v1",
                ["lookSensitivityRadians"] = "-0.001",
                ["focusCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.TargetNearest),
                ["targetCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetTarget),
                ["actionCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup),
                ["tractorCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.SetTractorPower),
                ["dockCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.DockNearest),
                ["undockCommand"] = CommandName(AetheriaRuntimeDaemonCommandKinds.Undock),
                ["zoneIndex"] = zone.ZoneIndex.ToString(CultureInfo.InvariantCulture),
                ["runId"] = run.RunId ?? ""
            };

            var presentationChildren = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null && entity.IsActive)
                .Select(entity => PlayableEntityPresentation(
                    entity, run, zone, currentEntityKey, simulationSettings, catalog))
                .ToList();
            var combatPresentation = CombatPresentation(run, zone, playerEntityId, simulationSettings);
            if (combatPresentation != null)
                presentationChildren.Insert(0, combatPresentation);
            var tractorPresentation = TractorPresentation(run, zone, playerEntityId);
            if (tractorPresentation != null)
                presentationChildren.Insert(0, tractorPresentation);
            var aimPresentation = AimPresentation(run, zone, playerEntityId);
            if (aimPresentation != null)
                presentationChildren.Insert(0, aimPresentation);
            presentationChildren.Insert(0, GravityFogVolume("aetheria.daemon.game.world.gravity-fog"));
            presentationChildren.Insert(1, StardustParticles("aetheria.daemon.game.world.stardust"));

            return new AetheriaRuntimeSurfaceComponent(
                id,
                "world.scene3d",
                props,
                presentationChildren,
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

        private static AetheriaRuntimeSurfaceComponent GravityFogVolume(string id)
        {
            var viewport = DefaultViewport();
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = "Gravity-shaped volumetric fog",
                ["documentRef"] = ViewportDocumentId("aetheria.viewport.render_splats", viewport),
                ["documentSchema"] = AetheriaRuntimeDaemonSchemas.RenderSplatsViewport,
                ["materialAssetRef"] = "shader.environment.gravity-fog",
                ["renderChannel"] = "world.transparent",
                ["compositeMode"] = "premultiplied-alpha",
                ["quality"] = "high",
                ["features"] = "flow.global;noise.slope",
                ["textureWidth"] = "1024",
                ["textureHeight"] = "1024",
                ["downsample"] = "0",
                ["layerBindings"] = "fog.surface_height=surfaceHeight;fog.patch_height=patchHeight;fog.patch=patch;fog.tint=tint",
                ["layerTargetDescriptors"] = "fog.surface_height=2,2,false,bilinear;" +
                    "fog.patch_height=0.25,0.25,false,bilinear;" +
                    "fog.patch=0.5,0.5,false,bilinear;" +
                    "fog.tint=0.5,0.5,true,trilinear",
                ["assetTextureBindings"] = "texture.environment.volume-dither=dither",
                ["viewportTextureScaleBindings"] = "ditherCoordinates=dither",
                ["viewportAnchor"] = "active-camera.xz",
                ["span"] = "256",
                ["cellWorldSize"] = "6",
                ["gravityTexelsPerCell"] = "8",
                ["viewportSnapLayer"] = "fog.surface_height",
                ["viewportSnapTexels"] = "8",
                ["documentFloatBindings"] = "simulationTimeSeconds=flowScroll,0.025,0",
                ["floatParameters"] = "fillDensity=0.000000001;fillDistance=120;fillExponent=5;fillOffset=70;" +
                    "patchDensity=0.35;floorOffset=-20;floorBlend=10;patchBlend=25;luminance=1;extinction=0.5;" +
                    "tintLodExponent=-0.45;safetyDistance=30;flowScale=512;flowAmplitude=15;flowPeriod=8;" +
                    "flowSlopeAmplitude=0;flowSwirlAmplitude=0;noiseScale=414.2167;noiseAmplitude=-36.17;" +
                    "noiseExponent=-0.25;noiseSpeed=0.0025;noiseSlopeExponent=0.15;dynamicSkyBoost=2;" +
                    "dynamicLodHigh=7;dynamicLodLow=2;dynamicIntensity=0.5;compositeOpacity=1"
            };
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "field.volume3d",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(props));
        }

        private static AetheriaRuntimeSurfaceComponent StardustParticles(string id)
        {
            var viewport = DefaultViewport();
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = "Gravity-field stardust",
                ["documentRef"] = ViewportDocumentId("aetheria.viewport.render_splats", viewport),
                ["documentSchema"] = AetheriaRuntimeDaemonSchemas.RenderSplatsViewport,
                ["computeProgramAssetRef"] = "compute.environment.stardust",
                ["materialAssetRef"] = "material.environment.stardust",
                ["renderChannel"] = "world.transparent",
                ["features"] = "flow.global;noise.slope",
                ["span"] = "256",
                ["threadGroupSize"] = "128",
                ["particleStrideBytes"] = "28",
                ["textureWidth"] = "1024",
                ["textureHeight"] = "1024",
                ["layerBindings"] = "fog.surface_height=surfaceHeight;fog.tint=tint",
                ["layerTargetDescriptors"] = "fog.surface_height=2,2,false,bilinear;" +
                    "fog.tint=0.5,0.5,true,trilinear",
                ["assetTextureBindings"] = "texture.environment.stardust-colors=hue",
                ["materialAssetTextureBindings"] = "texture.environment.volume-dither=dither",
                ["materialViewportTextureScaleBindings"] = "ditherCoordinates=dither",
                ["materialRenderFrameIndexPort"] = "frameIndex",
                ["viewportAnchor"] = "active-camera.xz",
                ["cellWorldSize"] = "6",
                ["gravityTexelsPerCell"] = "8",
                ["viewportSnapLayer"] = "fog.surface_height",
                ["viewportSnapTexels"] = "8",
                ["documentFloatBindings"] = "simulationTimeSeconds=time,1,0;" +
                    "simulationTimeSeconds=flowScroll,0.025,0",
                ["documentTimeVectorPort"] = "timeVector",
                ["floatParameters"] = "period=2;minimumSize=0.25;maximumSize=0.75;spacing=6;" +
                    "ceilingHeight=0;floorHeight=-10;minHeadroom=25;maxHeadroom=100;heightExponent=3;" +
                    "fillDensity=0.000000001;fillDistance=120;fillExponent=5;fillOffset=70;" +
                    "floorOffset=-20;floorBlend=10;luminance=1;" +
                    "tintLodExponent=-0.45;flowScale=512;flowAmplitude=15;flowPeriod=8;" +
                    "flowSlopeAmplitude=0;flowSwirlAmplitude=0;noiseScale=414.2167;" +
                    "noiseAmplitude=-36.17;noiseExponent=-0.25;noiseSpeed=0.0025;" +
                    "noiseSlopeExponent=0.15;dynamicSkyBoost=2;dynamicLodHigh=7;" +
                    "dynamicLodLow=2;dynamicIntensity=0.5"
            };
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "field.particles3d",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(props));
        }

        private static AetheriaRuntimeSurfaceComponent? AimPresentation(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string playerEntityId)
        {
            var player = FindCurrentEntity(run, zone);
            if (player == null)
                return null;

            var target = FindTargetEntity(zone, player);
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlledEntityId"] = playerEntityId,
                ["controlledEntityIndex"] = player.EntityIndex.ToString(CultureInfo.InvariantCulture),
                ["convergenceTargetEntityId"] = target == null
                    ? ""
                    : run.EntityRecordKey(zone.ZoneIndex, target.EntityIndex),
                ["viewDotRole"] = "aim.marker.view-direction",
                ["minimumConvergenceDistance"] = "50",
                ["viewDotRadius"] = "0.8"
            };
            return Node("aetheria.daemon.game.world.aim", "aim.presentation", props.Select(value => (value.Key, value.Value)).ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent? TractorPresentation(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string playerEntityId)
        {
            var player = FindCurrentEntity(run, zone);
            if (player == null)
                return null;

            return Node(
                "aetheria.daemon.game.world.tractor",
                "beam.presentation",
                new[]
                {
                    ("sourceEntityId", playerEntityId),
                    ("sourceEntityIndex", player.EntityIndex.ToString(CultureInfo.InvariantCulture)),
                    ("assetRole", "effect.beam.tractor"),
                    ("directionMode", "source-forward.v1"),
                    ("renderChannel", "world.effects"),
                    ("activationActionId", "pilot.scoop"),
                    ("power", FormatNumber(player.TractorPower)),
                    ("activationThreshold", FormatNumber(AetheriaRuntimeTractorMechanics.ActivationThreshold)),
                    ("radius", FormatNumber(AetheriaRuntimeTractorMechanics.Radius)),
                    ("maximumDistance", FormatNumber(AetheriaRuntimeTractorMechanics.Distance))
                });
        }

        private static AetheriaRuntimeSurfaceComponent? CombatPresentation(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string playerEntityId,
            AetheriaRuntimeDaemonSimulationSettings simulationSettings)
        {
            var player = FindCurrentEntity(run, zone);
            if (player == null)
                return null;

            var target = FindTargetEntity(zone, player);
            var targetEntityId = target == null
                ? ""
                : run.EntityRecordKey(zone.ZoneIndex, target.EntityIndex);
            var contact = target == null
                ? null
                : (player.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .FirstOrDefault(value => value != null && value.TargetEntityIndex == target.EntityIndex);
            var lockProgress = target == null
                ? 0
                : (player.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                    .Where(value => value != null && value.LockTargetEntityIndex == target.EntityIndex)
                    .Select(value => value.LockProgress)
                    .DefaultIfEmpty(0)
                    .Max();
            var shield = target == null ? 0 : EntityStat(target, "shield");
            var hull = target == null ? 0 : EntityStat(target, "hull");
            var props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlledEntityId"] = playerEntityId,
                ["controlledEntityIndex"] = player.EntityIndex.ToString(CultureInfo.InvariantCulture),
                ["selectedTargetEntityId"] = targetEntityId,
                ["selectedTargetEntityIndex"] = (target?.EntityIndex ?? -1).ToString(CultureInfo.InvariantCulture),
                ["targetVisible"] = contact?.Visible == true ? "true" : "false",
                ["targetHostile"] = contact?.Hostile == true ? "true" : "false",
                ["contactInformation"] = FormatNumber(contact?.InfoGathered ?? 0),
                ["shieldRatio"] = FormatRatio(shield, target == null ? 0 : MaximumShield(target, simulationSettings)),
                ["hullRatio"] = FormatRatio(hull, target == null ? 0 : MaximumHull(target, simulationSettings)),
                ["lockProgress"] = FormatNumber(lockProgress),
                ["reticleRole"] = "combat.reticle.selected",
                ["lockRole"] = "combat.reticle.lock",
                ["shieldMeterRole"] = "combat.meter.shield",
                ["hullMeterRole"] = "combat.meter.hull",
                ["hitMarkerRole"] = "combat.marker.hit",
                ["lockDisplayThreshold"] = "0.01",
                ["hitMarkerDurationSeconds"] = "0.25",
                ["radialFillMinimum"] = "0.25",
                ["radialFillMaximum"] = "0.75"
            };
            return Node("aetheria.daemon.game.world.combat", "combat.presentation", props.Select(value => (value.Key, value.Value)).ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent PlayableEntityPresentation(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            string currentEntityKey,
            AetheriaRuntimeDaemonSimulationSettings simulationSettings,
            AetheriaRuntimeCatalogSnapshot? catalog)
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
            var armorGrid = EntityGrid(entity, "armor");
            var maximumArmorGrid = EntityGrid(entity, "maximumArmor");
            var armor = armorGrid?.Values?.Sum() ?? 0;
            var maximumArmor = maximumArmorGrid?.Values?.Sum() ?? 0;
            var minimumTemperature = EntityStat(entity, AetheriaRuntimeThermalSimulation.MinimumTemperatureGrid);
            var maximumTemperature = EntityStat(entity, AetheriaRuntimeThermalSimulation.MaximumTemperatureGrid);
            var thermalVisibility = EntityStat(entity, "thermal-visibility");
            var maximumHull = MaximumHull(entity, simulationSettings);
            var maximumShield = MaximumShield(entity, simulationSettings);
            var behaviorStates = entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
            var capacitorCharge = behaviorStates.Where(value => value != null && value.BehaviorKind == "Capacitor")
                .Sum(value => Math.Max(0, value.CapacitorCharge));
            var capacitorCapacity = behaviorStates.Where(value => value != null && value.BehaviorKind == "Capacitor")
                .Sum(value => Math.Max(0, value.CapacitorCapacity));
            var reactorDraw = behaviorStates.Where(value => value != null && value.BehaviorKind == "Reactor")
                .Sum(value => value.ReactorDraw);
            var reactorLoad = behaviorStates.Where(value => value != null && value.BehaviorKind == "Reactor")
                .Select(value => value.ReactorLoadRatio).DefaultIfEmpty(0).Max();
            var radiatorTemperature = behaviorStates.Where(value => value != null && value.BehaviorKind == "Radiator")
                .Select(value => value.RadiatorTemperature).DefaultIfEmpty(0).Max();
            var equipmentStates = entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>();
            var minimumThermalPerformance = equipmentStates.Where(value => value != null)
                .Select(value => value.ThermalPerformance).DefaultIfEmpty(1).Min();
            var maximumWear = equipmentStates.Where(value => value != null)
                .Select(value => value.Wear).DefaultIfEmpty(0).Max();
            var offlineEquipmentCount = equipmentStates.Count(value => value != null && !value.Online);
            var cockpitTemperature = EntityStat(entity, "cockpit-temperature");
            var thermalRiskThreshold = simulationSettings.SevereThermalRiskThreshold;
            var heatstrokePostWeight = thermalRiskThreshold <= 0 ? 1 :
                Math.Max(0, Math.Min(1, entity.Heatstroke / thermalRiskThreshold));
            var severeHeatstrokeWeight = thermalRiskThreshold >= 1 ? 0 :
                Math.Max(0, Math.Min(1,
                    (entity.Heatstroke - thermalRiskThreshold) / (1 - thermalRiskThreshold)));
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
                ["assetRef"] = PlayableWorldAssetRef(entity, catalog),
                ["hull"] = FormatNumber(hull),
                ["maximumHull"] = FormatNumber(maximumHull),
                ["hullRatio"] = FormatRatio(hull, maximumHull),
                ["armor"] = FormatNumber(armor),
                ["maximumArmor"] = FormatNumber(maximumArmor),
                ["armorRatio"] = FormatRatio(armor, maximumArmor),
                ["armorGridWidth"] = (armorGrid?.Width ?? 0).ToString(CultureInfo.InvariantCulture),
                ["armorGridHeight"] = (armorGrid?.Height ?? 0).ToString(CultureInfo.InvariantCulture),
                ["armorGrid"] = string.Join(",", (armorGrid?.Values ?? Array.Empty<double>()).Select(FormatNumber)),
                ["shield"] = FormatNumber(shield),
                ["maximumShield"] = FormatNumber(maximumShield),
                ["shieldRatio"] = FormatRatio(shield, maximumShield),
                ["capacitorCharge"] = FormatNumber(capacitorCharge),
                ["capacitorCapacity"] = FormatNumber(capacitorCapacity),
                ["capacitorRatio"] = FormatRatio(capacitorCharge, capacitorCapacity),
                ["reactorDraw"] = FormatNumber(reactorDraw),
                ["reactorLoadRatio"] = FormatNumber(reactorLoad),
                ["radiatorTemperature"] = FormatNumber(radiatorTemperature),
                ["radiatorVisibility"] = FormatNumber(EntityStat(entity, "radiator-visibility")),
                ["minimumEquipmentThermalPerformance"] = FormatNumber(minimumThermalPerformance),
                ["maximumEquipmentWear"] = FormatNumber(maximumWear),
                ["offlineEquipmentCount"] = offlineEquipmentCount.ToString(CultureInfo.InvariantCulture),
                ["heat"] = FormatNumber(heat),
                ["meanTemperature"] = FormatNumber(heat),
                ["minimumTemperature"] = FormatNumber(minimumTemperature),
                ["maximumTemperature"] = FormatNumber(maximumTemperature),
                ["thermalVisibility"] = FormatNumber(thermalVisibility),
                ["cockpitTemperature"] = FormatNumber(cockpitTemperature),
                ["heatstroke"] = FormatRatio(entity.Heatstroke, 1),
                ["hypothermia"] = FormatRatio(entity.Hypothermia, 1),
                ["heatstrokeRisk"] = entity.Heatstroke > thermalRiskThreshold ? "true" : "false",
                ["hypothermiaRisk"] = entity.Hypothermia > thermalRiskThreshold ? "true" : "false",
                ["heatstrokePostWeight"] = FormatNumber(heatstrokePostWeight),
                ["severeHeatstrokeWeight"] = FormatNumber(severeHeatstrokeWeight),
                ["heatstrokePhasingFloor"] = "0",
                ["heatstrokePhasingFrequency"] = "5",
                ["deathTransitionSeconds"] = "1",
                ["causeOfDeath"] = entity.CauseOfDeath ?? "",
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
            return new AetheriaRuntimeSurfaceComponent(
                $"aetheria.daemon.game.world.entity.{entity.EntityIndex}",
                "entity.presentation",
                props,
                string.Equals(entityId, playerEntityId, StringComparison.Ordinal)
                    ? WeaponStateItems(entity, run, zone)
                    : Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static AetheriaRuntimeSurfaceComponent[] WeaponStateItems(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            return (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                .Where(value => value != null)
                .Select(value =>
                {
                    var itemKey = value.OwnerKind == AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind &&
                                  value.OwnerIndex >= 0 && value.OwnerIndex < (entity.Equipment?.Count ?? 0)
                        ? entity.Equipment[value.OwnerIndex]?.Item?.ItemKey ?? ""
                        : "";
                    var targetEntityId = value.LockTargetEntityIndex < 0
                        ? ""
                        : run.EntityRecordKey(zone.ZoneIndex, value.LockTargetEntityIndex);
                    return Node(
                        $"aetheria.daemon.game.world.entity.{entity.EntityIndex}.weapon.{SurfaceToken(value.OwnerKind)}.{value.OwnerIndex}.{value.BehaviorIndex}",
                        "weapon.state",
                        new[]
                        {
                            ("ownerKind", value.OwnerKind),
                            ("ownerIndex", value.OwnerIndex.ToString(CultureInfo.InvariantCulture)),
                            ("behaviorIndex", value.BehaviorIndex.ToString(CultureInfo.InvariantCulture)),
                            ("behaviorKind", value.BehaviorKind),
                            ("itemKey", itemKey),
                            ("firing", value.Firing ? "true" : "false"),
                            ("triggerPending", value.TriggerPending ? "true" : "false"),
                            ("ammo", value.Ammo.ToString(CultureInfo.InvariantCulture)),
                            ("ammoIntervalProgress", FormatNumber(value.AmmoIntervalProgress)),
                            ("burstRemaining", value.BurstRemaining.ToString(CultureInfo.InvariantCulture)),
                            ("burstProgress", FormatNumber(value.BurstTimer)),
                            ("cooldownProgress", FormatNumber(value.CooldownProgress)),
                            ("coolingDown", value.CoolingDown ? "true" : "false"),
                            ("charging", value.Charging ? "true" : "false"),
                            ("charged", value.Charged ? "true" : "false"),
                            ("charge", FormatNumber(value.Charge)),
                            ("reloading", value.Reloading ? "true" : "false"),
                            ("reloadProgress", FormatNumber(value.ReloadProgress)),
                            ("lockProgress", FormatNumber(value.LockProgress)),
                            ("targetEntityId", targetEntityId),
                            ("lastRefusalReason", value.LastRefusalReason),
                            ("chargeHoldSeconds", FormatNumber(value.ChargeHoldSeconds)),
                            ("chargeRiskChecks", value.ChargeRiskChecks.ToString(CultureInfo.InvariantCulture)),
                            ("chargeMalfunctionRisk", FormatNumber(value.ChargeMalfunctionRisk))
                        });
                })
                .ToArray();
        }

        private static AetheriaRuntimeSurfaceComponent CockpitOverlay(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeEntitySnapshotCommit? target,
            AetheriaRuntimeDaemonSimulationSettings simulationSettings)
        {
            var hull = entity == null ? 0 : EntityStat(entity, "hull");
            var shield = entity == null ? 0 : EntityStat(entity, "shield");
            var maximumHull = entity == null ? 0 : MaximumHull(entity, simulationSettings);
            var maximumShield = entity == null ? 0 : MaximumShield(entity, simulationSettings);
            var behaviorStates = entity?.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
            var capacitorCharge = behaviorStates
                .Where(value => value != null && value.BehaviorKind == "Capacitor")
                .Sum(value => Math.Max(0, value.CapacitorCharge));
            var capacitorCapacity = behaviorStates
                .Where(value => value != null && value.BehaviorKind == "Capacitor")
                .Sum(value => Math.Max(0, value.CapacitorCapacity));
            var weaponCooldown = (entity?.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                .Select(value => value?.CooldownProgress ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            var targetLock = target == null
                ? 0
                : (entity?.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                    .Where(value => value != null && value.LockTargetEntityIndex == target.EntityIndex)
                    .Select(value => value.LockProgress)
                    .DefaultIfEmpty(0)
                    .Max();
            var targetHull = target == null ? 0 : EntityStat(target, "hull");
            var targetShield = target == null ? 0 : EntityStat(target, "shield");
            var panelStyle = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["background"] = "#07131ACC",
                ["color"] = "#D9F8FF",
                ["borderColor"] = "#4CCFE8AA",
                ["borderWidth"] = "1",
                ["borderRadius"] = "4",
                ["fontSize"] = "13"
            };

            var systems = StyledNode(
                "aetheria.daemon.game.cockpit.systems",
                "pane",
                new[] { ("title", string.IsNullOrWhiteSpace(entity?.Name) ? "PILOT" : entity!.Name.ToUpperInvariant()) },
                Layout(("width", "280"), ("padding", "12")),
                panelStyle,
                Progress("aetheria.daemon.game.cockpit.hull", "HULL", FormatRatio(hull, maximumHull)),
                Progress("aetheria.daemon.game.cockpit.shield", "SHIELD", FormatRatio(shield, maximumShield)),
                Progress("aetheria.daemon.game.cockpit.capacitor", "CAPACITOR", FormatRatio(capacitorCharge, capacitorCapacity)),
                Metric("aetheria.daemon.game.cockpit.temperature", "COCKPIT TEMP", FormatNumber(entity == null ? 0 : EntityStat(entity, "cockpit-temperature"))),
                Metric("aetheria.daemon.game.cockpit.cargo", "CARGO BAYS", Count(entity?.CargoContents).ToString(CultureInfo.InvariantCulture)));

            var weapons = StyledNode(
                "aetheria.daemon.game.cockpit.weapons",
                "pane",
                new[] { ("title", "WEAPONS") },
                Layout(("width", "240"), ("padding", "12")),
                panelStyle,
                Progress("aetheria.daemon.game.cockpit.weaponCooldown", "COOLDOWN", weaponCooldown.ToString("0.###", CultureInfo.InvariantCulture)),
                Metric("aetheria.daemon.game.cockpit.weaponGroups", "GROUPS", Count(entity?.WeaponGroups).ToString(CultureInfo.InvariantCulture)),
                Metric("aetheria.daemon.game.cockpit.target", "TARGET", string.IsNullOrWhiteSpace(target?.Name) ? "NO TARGET" : target!.Name),
                Progress("aetheria.daemon.game.cockpit.targetLock", "TARGET LOCK", FormatNumber(targetLock)),
                Progress("aetheria.daemon.game.cockpit.targetShield", "TARGET SHIELD", FormatRatio(targetShield, target == null ? 0 : MaximumShield(target, simulationSettings))),
                Progress("aetheria.daemon.game.cockpit.targetHull", "TARGET HULL", FormatRatio(targetHull, target == null ? 0 : MaximumHull(target, simulationSettings))));

            return StyledNode(
                "aetheria.daemon.game.cockpit",
                "pane",
                new[] { ("role", "pilot.cockpit") },
                Layout(
                    ("position", "absolute"),
                    ("inset", "0"),
                    ("direction", "row"),
                    ("alignItems", "flex-end"),
                    ("justifyContent", "space-between"),
                    ("padding", "24")),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["background"] = "transparent",
                    ["color"] = "#D9F8FF"
                },
                systems,
                weapons);
        }

        private static string PlayableWorldAssetRef(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
            => AetheriaRuntimeAssets.ResolveEntityPrefabAssetRef(entity, catalog);

        private static double EntityStat(AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .FirstOrDefault(grid => string.Equals(grid?.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Values?.FirstOrDefault() ?? 0;

        private static AetheriaRuntimeEntityStatGridCommit? EntityGrid(
            AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .FirstOrDefault(grid => string.Equals(grid?.Name, name, StringComparison.OrdinalIgnoreCase));

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
            // Stardust owns a 256-cell camera-relative lattice at six world units
            // per cell. Gravity is rasterized at eight texels per Stardust cell,
            // so the 1536-square viewport maps exactly to the 2048-square surface
            // target without either lattice crawling under the other.
            return new AetheriaRuntimeViewportBounds
            {
                MinX = -768,
                MinY = -768,
                MaxX = 768,
                MaxY = 768
            };
        }

        private static (double X, double Y, double Z) ResolveStellarAmbient(
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var suns = (zone?.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .Where(body => body != null &&
                    string.Equals(body.Kind, "sun", StringComparison.OrdinalIgnoreCase) &&
                    body.SunVisual != null)
                .Select(body => new
                {
                    Visual = body.SunVisual,
                    Weight = Math.Pow(Math.Max(0, body.Mass), 0.25) *
                        Math.Max(0.01, body.SunVisual.LightRadiusMultiplier)
                })
                .Where(value => value.Weight > 0)
                .ToArray();
            if (suns.Length == 0)
                return (0.2, 0.2, 0.2);

            var weight = suns.Sum(value => value.Weight);
            return (
                suns.Sum(value => Math.Max(0, value.Visual.LightColorX) * value.Weight) / weight,
                suns.Sum(value => Math.Max(0, value.Visual.LightColorY) * value.Weight) / weight,
                suns.Sum(value => Math.Max(0, value.Visual.LightColorZ) * value.Weight) / weight);
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

        private static AetheriaRuntimeEntitySnapshotCommit? FindDockParent(
            AetheriaRuntimeZoneSnapshotCommit zone,
            int childEntityIndex)
        {
            if (childEntityIndex < 0)
                return null;

            return (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(entity => entity != null &&
                    (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(childEntityIndex));
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

        private static AetheriaRuntimeSurfaceComponent Progress(string id, string label, string ratio)
        {
            return Node(
                id,
                "progress",
                new[]
                {
                    ("label", label),
                    ("ratio", ratio ?? "0"),
                    ("value", ratio ?? "0")
                });
        }

        private static AetheriaRuntimeSurfaceComponent StyledNode(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            IReadOnlyDictionary<string, string> layout,
            IReadOnlyDictionary<string, string> style,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            var values = props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal);
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                values,
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(values),
                Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                layout,
                style);
        }

        private static AetheriaRuntimeSurfaceComponent Hidden(AetheriaRuntimeSurfaceComponent component)
        {
            return new AetheriaRuntimeSurfaceComponent(
                component.Id,
                component.Kind,
                component.Props,
                component.Children,
                component.StateBindings,
                component.EmbeddedDocuments,
                Layout(("display", "none")),
                component.Style);
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

        private static AetheriaRuntimeSurfaceComponent PilotSurfaceRoot(
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
                    ("height", "100%"),
                    ("minHeight", "100%")),
                new Dictionary<string, string>
                {
                    ["background"] = "transparent"
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
