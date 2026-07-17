using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonOperationResult
    {
        public AetheriaRuntimeDaemonOperationResult(
            AetheriaRuntimeRunCheckpointCommit run,
            IReadOnlyList<string> appliedCommandIds,
            IReadOnlyList<string> rejectedCommandIds,
            AetheriaRuntimeDaemonIntentState? intents = null,
            IReadOnlyDictionary<string, string>? rejectedCommandReasons = null)
        {
            Run = run ?? new AetheriaRuntimeRunCheckpointCommit();
            AppliedCommandIds = appliedCommandIds ?? Array.Empty<string>();
            RejectedCommandIds = rejectedCommandIds ?? Array.Empty<string>();
            Intents = intents ?? new AetheriaRuntimeDaemonIntentState();
            RejectedCommandReasons = rejectedCommandReasons ?? new Dictionary<string, string>();
        }

        public AetheriaRuntimeRunCheckpointCommit Run { get; }
        public IReadOnlyList<string> AppliedCommandIds { get; }
        public IReadOnlyList<string> RejectedCommandIds { get; }
        public AetheriaRuntimeDaemonIntentState Intents { get; }
        public IReadOnlyDictionary<string, string> RejectedCommandReasons { get; }
    }

    public sealed class AetheriaRuntimeDaemonOperationContext
    {
        public const double DefaultDockingDistance = 25.0;
        public const double DefaultWormholeExitRadius = 50.0;

        public IReadOnlyList<AetheriaRuntimeLoadoutTemplateCommit> LoadoutTemplates { get; set; } =
            Array.Empty<AetheriaRuntimeLoadoutTemplateCommit>();

        public AetheriaRuntimeDaemonIntentState Intents { get; set; } = new AetheriaRuntimeDaemonIntentState();

        public double DockingDistance { get; set; } = AetheriaRuntimeDaemonOperationContext.DefaultDockingDistance;

        public double WormholeExitRadius { get; set; } = AetheriaRuntimeDaemonOperationContext.DefaultWormholeExitRadius;
        public double WormholeDistanceRatio { get; set; } =
            AetheriaRuntimeDaemonRenderSettings.AetheriaDefault.WormholeDistanceRatio;
        public AetheriaRuntimeCatalogSnapshot? Catalog { get; set; }

        internal string RejectionReason { get; private set; } = "";

        internal void ResetRejectionReason() => RejectionReason = "";

        internal bool Reject(string reason)
        {
            RejectionReason = reason ?? "";
            return false;
        }
    }

    public static class AetheriaRuntimeDaemonRejectionReasons
    {
        public const string InvalidCommandState = "invalid-command-state";
        public const string RunTerminal = "run-terminal";
        public const string AuthorityDenied = "authority-denied";
        public const string InvalidDockActor = "invalid-dock-actor";
        public const string InvalidDockTarget = "invalid-dock-target";
        public const string AlreadyDocked = "already-docked";
        public const string NoEligibleDockingBay = "no-eligible-docking-bay";
        public const string NotDocked = "not-docked";
        public const string MissingCockpit = "missing-cockpit";
        public const string MissingPropulsion = "missing-propulsion";
        public const string MissingReactor = "missing-reactor";
        public const string DockingBayCargoNotEmpty = "docking-bay-cargo-not-empty";
        public const string RefitRequiresDocked = "refit-requires-docked";
        public const string RefitAccessDenied = "refit-access-denied";
        public const string InvalidRefitSource = "invalid-refit-source";
        public const string InvalidRefitItem = "invalid-refit-item";
        public const string RefitItemMustBeSingle = "refit-item-must-be-single";
        public const string RefitBayNotEmpty = "refit-bay-not-empty";
        public const string RefitNoFit = "refit-no-fit";
        public const string InvalidCargoDestination = "invalid-cargo-destination";
        public const string InvalidCargoSource = "invalid-cargo-source";
        public const string CargoAccessDenied = "cargo-access-denied";
        public const string CargoNoFit = "cargo-no-fit";
        public const string CargoStackLimit = "cargo-stack-limit";
        public const string TradeRequiresDocked = "trade-requires-docked";
        public const string InvalidTradeSource = "invalid-trade-source";
        public const string TradeStationNoFit = "trade-station-no-fit";
        public const string InvalidTradePayout = "invalid-trade-payout";
    }

    public static class AetheriaRuntimeDaemonOperations
    {
        public static AetheriaRuntimeDaemonOperationResult Execute(
            AetheriaRuntimeRunCheckpointCommit run,
            IEnumerable<AetheriaRuntimeDaemonCommandDocument> commands)
        {
            return Execute(run, commands, new AetheriaRuntimeDaemonOperationContext());
        }

        public static AetheriaRuntimeDaemonOperationResult Execute(
            AetheriaRuntimeRunCheckpointCommit run,
            IEnumerable<AetheriaRuntimeDaemonCommandDocument> commands,
            AetheriaRuntimeDaemonOperationContext context)
        {
            run ??= new AetheriaRuntimeRunCheckpointCommit();
            context ??= new AetheriaRuntimeDaemonOperationContext();
            context.Intents = new AetheriaRuntimeDaemonIntentState();
            var applied = new List<string>();
            var rejected = new List<string>();
            var rejectedReasons = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var command in commands ?? Enumerable.Empty<AetheriaRuntimeDaemonCommandDocument>())
            {
                if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
                    continue;

                if (!AetheriaRuntimeRunLifecycle.IsActive(run))
                {
                    rejected.Add(command.CommandId);
                    rejectedReasons[command.CommandId] = AetheriaRuntimeDaemonRejectionReasons.RunTerminal;
                    continue;
                }

                context.ResetRejectionReason();
                if (ApplyOne(run, command, context))
                    applied.Add(command.CommandId);
                else
                {
                    rejected.Add(command.CommandId);
                    rejectedReasons[command.CommandId] = string.IsNullOrWhiteSpace(context.RejectionReason)
                        ? AetheriaRuntimeDaemonRejectionReasons.InvalidCommandState
                        : context.RejectionReason;
                }
            }

            return new AetheriaRuntimeDaemonOperationResult(
                run, applied, rejected, context.Intents, rejectedReasons);
        }

        private static bool ApplyOne(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            switch (command.Kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetTarget:
                    return ApplySetTarget(run, command);
                case AetheriaRuntimeDaemonCommandKinds.ClearTarget:
                    return ApplyClearTarget(run, command);
                case AetheriaRuntimeDaemonCommandKinds.TargetNearest:
                    return ApplyTargetCycle(run, command, TargetCycleMode.Nearest);
                case AetheriaRuntimeDaemonCommandKinds.TargetNext:
                    return ApplyTargetCycle(run, command, TargetCycleMode.Next);
                case AetheriaRuntimeDaemonCommandKinds.TargetPrevious:
                    return ApplyTargetCycle(run, command, TargetCycleMode.Previous);
                case AetheriaRuntimeDaemonCommandKinds.TargetReticle:
                    return ApplyTargetReticle(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetLookDirection:
                    return ApplySetLookDirection(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetTractorPower:
                    if (!IsNormalizedScalar(command.ScalarValue))
                        return false;

                    return ApplyCurrentEntity(run, command, entity =>
                        entity.TractorTargetPower = command.ScalarValue);
                case AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled:
                    return ApplyCurrentEntity(run, command, entity =>
                        entity.HeatsinksEnabled = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown:
                    return ApplyTargetOrCurrentEntity(run, command, entity =>
                        entity.OverrideShutdown = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance:
                    if (command.ScalarValue < 0.0 || command.ScalarValue > 1.0)
                        return false;

                    return ApplyTargetEntity(run, command.TargetEntityKey, entity =>
                        entity.ShutdownPerformance = command.ScalarValue);
                case AetheriaRuntimeDaemonCommandKinds.SetItemEnabled:
                    return ApplyCurrentEquipmentItem(run, command, item =>
                        item.Enabled = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled:
                    return ApplyToggleEquipmentBehaviorItem(run, command, "Shield", item =>
                        item.Enabled = !item.Enabled);
                case AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown:
                    return ApplyTargetEquipmentItem(run, command, item =>
                        item.OverrideShutdown = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership:
                    return ApplySetWeaponGroupMembership(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature:
                    return ApplySetThermotoggleTargetTemperature(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetEntityName:
                    return ApplyTargetEntity(run, command.TargetEntityKey, entity =>
                        entity.Name = command.TextValue ?? "");
                case AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip:
                    return ApplySetDockedCurrentShip(run, command.TargetEntityKey);
                case AetheriaRuntimeDaemonCommandKinds.TransferCargoItem:
                    return ApplyTransferCargoItem(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.EquipItem:
                    return ApplyEquipItem(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.StoreItem:
                    return ApplyStoreItem(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity:
                    return ApplyToggleHullConductivity(run, command);
                case AetheriaRuntimeDaemonCommandKinds.TradePurchase:
                    return ApplyTradePurchase(run, command, context.Catalog);
                case AetheriaRuntimeDaemonCommandKinds.TradeSale:
                    return ApplyTradeSale(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.RestoreLoadout:
                    return ApplyRestoreLoadout(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.SetMoveVector:
                    return ApplySetMoveVector(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup:
                    return ApplyFireWeaponGroup(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive:
                    return ApplySetWeaponGroupActive(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive:
                    return ApplySetBehaviorActive(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.ActivateConsumable:
                    return ApplyActivateConsumable(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.SensorPing:
                    return ApplySensorPing(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.Dock:
                    return ApplyDockIntent(run, command, context, context.DockingDistance);
                case AetheriaRuntimeDaemonCommandKinds.DockNearest:
                    return ApplyDockNearestIntent(run, command, context, context.DockingDistance);
                case AetheriaRuntimeDaemonCommandKinds.Undock:
                    return ApplyUndockIntent(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.Interact:
                    return ApplyInteractIntent(
                        run,
                        command,
                        context,
                        context.DockingDistance,
                        context.WormholeExitRadius,
                        context.WormholeDistanceRatio);
                case AetheriaRuntimeDaemonCommandKinds.EnterWormhole:
                    return ApplyEnterWormholeIntent(run, command, context.Intents,
                        context.WormholeExitRadius, context.WormholeDistanceRatio);
                case AetheriaRuntimeDaemonCommandKinds.TowToStation:
                    return ApplyTowToStation(run, command, context.DockingDistance);
                case AetheriaRuntimeDaemonCommandKinds.IssueAgentTask:
                    return ApplyIssueAgentTask(run, command);
                case AetheriaRuntimeDaemonCommandKinds.CancelAgentTask:
                    return ApplyCancelAgentTask(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetSimulationRate:
                    return IsSupportedSimulationRate(command.ScalarValue);
                case AetheriaRuntimeDaemonCommandKinds.AdvanceSimulationStep:
                    return true;
                default:
                    return false;
            }
        }

        public static readonly double[] SupportedSimulationRates = { 0, 0.25, 0.5, 1, 2, 4, 8, 16, 32, 64, 128 };

        public static bool IsSupportedSimulationRate(double value) =>
            SupportedSimulationRates.Any(rate => Math.Abs(value - rate) < 0.000001);

        public static bool RequiresSimulationStep(AetheriaRuntimeDaemonCommandKinds kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetMoveVector:
                case AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup:
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive:
                case AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive:
                case AetheriaRuntimeDaemonCommandKinds.ActivateConsumable:
                case AetheriaRuntimeDaemonCommandKinds.SensorPing:
                case AetheriaRuntimeDaemonCommandKinds.Dock:
                case AetheriaRuntimeDaemonCommandKinds.DockNearest:
                case AetheriaRuntimeDaemonCommandKinds.Undock:
                case AetheriaRuntimeDaemonCommandKinds.Interact:
                case AetheriaRuntimeDaemonCommandKinds.EnterWormhole:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ApplyIssueAgentTask(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var request = command.AgentTask ?? new AetheriaRuntimeAgentTaskCommand();
            var taskId = string.IsNullOrWhiteSpace(request.TaskId) ? command.CommandId : request.TaskId;
            var taskType = (request.TaskType ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(taskId) ||
                string.IsNullOrWhiteSpace(request.CorporationKey) ||
                !AetheriaRuntimeAgentTaskTypes.All.Contains(taskType, StringComparer.Ordinal) ||
                request.ZoneIndex < 0 ||
                !(run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).Any(zone =>
                    zone != null && zone.ZoneIndex == request.ZoneIndex) ||
                (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>()).Any(task =>
                    task != null && string.Equals(task.TaskId, taskId, StringComparison.Ordinal)))
            {
                return false;
            }
            if (string.Equals(taskType, AetheriaRuntimeAgentTaskTypes.Haul, StringComparison.Ordinal) &&
                (request.OriginEntityIndex < 0 || request.TargetEntityIndex < 0 ||
                 string.IsNullOrWhiteSpace(request.ItemKey) || request.Quantity <= 0))
            {
                return false;
            }
            if (string.Equals(taskType, AetheriaRuntimeAgentTaskTypes.Defend, StringComparison.Ordinal) &&
                !(request.TargetBodyKeys ?? Array.Empty<string>()).Any(key => !string.IsNullOrWhiteSpace(key)))
            {
                return false;
            }
            if (string.Equals(taskType, AetheriaRuntimeAgentTaskTypes.Tow, StringComparison.Ordinal) &&
                (request.TargetEntityIndex < 0 || string.IsNullOrWhiteSpace(request.OrbitParentKey) || request.OrbitDistance <= 0))
                return false;
            if (string.Equals(taskType, AetheriaRuntimeAgentTaskTypes.Attack, StringComparison.Ordinal) &&
                !CanIssueAttackTask(run, request))
                return false;

            run.AgentTasks = (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Concat(new[]
                {
                    new AetheriaRuntimeAgentTaskCommit
                    {
                        TaskId = taskId,
                        CorporationKey = request.CorporationKey ?? "",
                        TaskType = taskType,
                        Priority = request.Priority,
                        ZoneIndex = request.ZoneIndex,
                        TargetEntityIndex = request.TargetEntityIndex,
                        TargetPositionX = request.TargetPositionX,
                        TargetPositionZ = request.TargetPositionZ,
                        CompletionRadius = request.CompletionRadius > 0 ? request.CompletionRadius : 10,
                        WeaponGroup = request.WeaponGroup,
                        OriginEntityIndex = request.OriginEntityIndex,
                        ItemKey = request.ItemKey ?? "",
                        RequestedQuantity = request.Quantity,
                        Phase = string.Equals(taskType, AetheriaRuntimeAgentTaskTypes.Haul, StringComparison.Ordinal)
                            ? "pickup"
                            : "",
                        TargetBodyKeys = request.TargetBodyKeys ?? Array.Empty<string>(),
                        OrbitParentKey = request.OrbitParentKey ?? "",
                        OrbitDistance = request.OrbitDistance,
                        Status = AetheriaRuntimeAgentTaskStatuses.Queued
                    }
                })
                .ToArray();
            return true;
        }

        private static bool CanIssueAttackTask(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeAgentTaskCommand request)
        {
            if (request.TargetEntityIndex < 0 || request.WeaponGroup < 0)
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == request.ZoneIndex);
            var target = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.EntityIndex == request.TargetEntityIndex);
            if (target == null || !target.IsActive || !IsAttackHostile(request.CorporationKey, target.FactionKey))
                return false;

            return (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(candidate => candidate != null)
                .SelectMany(candidate => (candidate.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Select(entity => (ZoneIndex: candidate.ZoneIndex, Entity: entity)))
                .Any(candidate =>
                    candidate.Entity != null &&
                    candidate.Entity.IsActive &&
                    !IsCurrentEntity(run, candidate.ZoneIndex, candidate.Entity.EntityIndex) &&
                    string.Equals(candidate.Entity.FactionKey ?? "", request.CorporationKey ?? "", StringComparison.Ordinal) &&
                    (candidate.Entity.AgentTaskCapabilities ?? Array.Empty<string>())
                        .Contains(AetheriaRuntimeAgentTaskTypes.Attack, StringComparer.Ordinal) &&
                    request.WeaponGroup < (candidate.Entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>()).Count &&
                    (candidate.Entity.WeaponGroups[request.WeaponGroup] ?? Array.Empty<int>()).Count > 0);
        }

        private static bool IsAttackHostile(string? corporationKey, string? targetFactionKey)
        {
            var corporationIsPlayer = string.Equals(corporationKey, "player", StringComparison.OrdinalIgnoreCase);
            var targetIsPlayer = string.Equals(targetFactionKey, "player", StringComparison.OrdinalIgnoreCase);
            return corporationIsPlayer != targetIsPlayer &&
                (string.Equals(corporationKey, "raider", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(targetFactionKey, "raider", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int entityIndex)
        {
            return AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(
                    run.CurrentEntityKey,
                    out var currentZoneIndex,
                    out var currentEntityIndex) &&
                currentZoneIndex == zoneIndex &&
                currentEntityIndex == entityIndex;
        }

        private static bool ApplyCancelAgentTask(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var taskId = command.AgentTask?.TaskId ?? command.TextValue ?? "";
            var task = (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .FirstOrDefault(candidate => candidate != null && string.Equals(candidate.TaskId, taskId, StringComparison.Ordinal));
            if (task == null ||
                string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal) ||
                string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Cancelled, StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var entity in (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()))
            {
                if (string.Equals(entity.AssignedAgentTaskId, task.TaskId, StringComparison.Ordinal))
                    entity.AssignedAgentTaskId = "";
            }
            task.Status = AetheriaRuntimeAgentTaskStatuses.Cancelled;
            task.AssignedEntityIndex = -1;
            return true;
        }

        private static bool ApplySetTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var actorKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey
                : command.ActorEntityKey;
            if (!TryResolveEntity(run, actorKey, out var actorZone, out var actorIndex, out var actor) ||
                !TryResolveEntity(run, command.TargetEntityKey, out var targetZone, out var targetIndex, out _) ||
                actorZone != targetZone ||
                actorIndex == targetIndex)
            {
                return false;
            }

            if (!(actor.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Any(contact => contact != null && contact.TargetEntityIndex == targetIndex && contact.Visible))
            {
                return false;
            }

            actor.TargetEntityIndex = targetIndex;
            return true;
        }

        private enum TargetCycleMode
        {
            Nearest,
            Next,
            Previous
        }

        private static bool ApplyTargetCycle(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            TargetCycleMode mode)
        {
            var actorKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey
                : command.ActorEntityKey;
            if (!TryResolveEntity(run, actorKey, out var actorZone, out var actorIndex, out var actor))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == actorZone);
            if (zone == null)
                return false;

            var orderedTargets = VisibleHostileTargets(zone, actor, actorIndex)
                .OrderBy(target => target.DistanceSq)
                .ThenBy(target => target.EntityIndex)
                .ToArray();
            if (orderedTargets.Length == 0)
                return false;

            if (mode == TargetCycleMode.Nearest)
            {
                actor.TargetEntityIndex = orderedTargets[0].EntityIndex;
                return true;
            }

            var currentIndex = Array.FindIndex(
                orderedTargets,
                target => target.EntityIndex == actor.TargetEntityIndex);
            if (currentIndex < 0)
            {
                actor.TargetEntityIndex = orderedTargets[0].EntityIndex;
                return true;
            }

            var nextIndex = mode == TargetCycleMode.Next
                ? (currentIndex + 1 + orderedTargets.Length) % orderedTargets.Length
                : (currentIndex - 1 + orderedTargets.Length) % orderedTargets.Length;
            actor.TargetEntityIndex = orderedTargets[nextIndex].EntityIndex;
            return true;
        }

        private static bool ApplyTargetReticle(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var actorKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey
                : command.ActorEntityKey;
            if (!TryResolveEntity(run, actorKey, out var actorZone, out var actorIndex, out var actor))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == actorZone);
            if (zone == null)
                return false;

            var lookLength = Math.Sqrt(
                (command.DirectionX * command.DirectionX) +
                (command.DirectionY * command.DirectionY) +
                (command.PositionZ * command.PositionZ));
            if (lookLength <= double.Epsilon)
                return false;

            var lookX = command.DirectionX / lookLength;
            var lookY = command.DirectionY / lookLength;
            var lookZ = command.PositionZ / lookLength;
            var scoredTargets = VisibleHostileTargets(zone, actor, actorIndex)
                .Select(candidate =>
                {
                    var distance = Math.Sqrt(candidate.DistanceSq);
                    var dot = distance <= double.Epsilon
                        ? double.NegativeInfinity
                        : ((candidate.DeltaX / distance) * lookX) +
                          ((candidate.DeltaY / distance) * lookY) +
                          ((candidate.DeltaZ / distance) * lookZ);
                    return (candidate.EntityIndex, Dot: dot);
                })
                .Where(candidate => candidate.Dot > double.NegativeInfinity)
                .OrderByDescending(candidate => candidate.Dot)
                .ThenBy(candidate => candidate.EntityIndex)
                .ToArray();
            if (scoredTargets.Length == 0)
                return false;

            var target = scoredTargets[0];
            actor.TargetEntityIndex = actor.TargetEntityIndex == target.EntityIndex
                ? -1
                : target.EntityIndex;
            return true;
        }

        private static IEnumerable<(int EntityIndex, double DistanceSq, double DeltaX, double DeltaY, double DeltaZ)> VisibleHostileTargets(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit actor,
            int actorIndex)
        {
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            foreach (var contact in actor.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            {
                if (contact == null ||
                    !contact.Visible ||
                    !contact.Hostile ||
                    contact.TargetEntityIndex == actorIndex ||
                    contact.TargetEntityIndex < 0 ||
                    contact.TargetEntityIndex >= entities.Count)
                {
                    continue;
                }

                var target = entities[contact.TargetEntityIndex];
                if (target == null)
                    continue;

                var deltaX = target.PositionX - actor.PositionX;
                var deltaY = target.PositionY - actor.PositionY;
                var deltaZ = target.PositionZ - actor.PositionZ;
                yield return (
                    contact.TargetEntityIndex,
                    (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ),
                    deltaX,
                    deltaY,
                    deltaZ);
            }
        }

        private static bool ApplyClearTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            return ApplyCurrentEntity(run, command, entity => entity.TargetEntityIndex = -1);
        }

        private static bool ApplySetLookDirection(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!IsFinite(command.DirectionX) || !IsFinite(command.PositionZ))
                return false;
            var length = Math.Sqrt(command.DirectionX * command.DirectionX + command.PositionZ * command.PositionZ);
            if (length <= 0.000001)
                return false;

            return ApplyCurrentEntity(run, command, entity =>
            {
                entity.LookDirectionX = command.DirectionX / length;
                entity.LookDirectionY = command.PositionZ / length;
            });
        }

        private static bool ApplySetCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey)
        {
            if (!TryResolveEntity(run, entityKey, out var zoneIndex, out var entityIndex, out _))
                return false;

            run.CurrentZoneIndex = zoneIndex;
            run.CurrentEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, entityIndex);
            return true;
        }

        private static bool ApplySetDockedCurrentShip(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey)
        {
            if (!TryResolveEntity(run, entityKey, out var zoneIndex, out var entityIndex, out _))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var isDocked = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Any(entity =>
                    entity != null &&
                    entity.EntityIndex != entityIndex &&
                    (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(entityIndex));
            if (!isDocked)
                return false;

            run.CurrentZoneIndex = zoneIndex;
            run.CurrentEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, entityIndex);
            return true;
        }

        private static bool ApplyTargetOrCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeEntitySnapshotCommit> mutate)
        {
            return string.IsNullOrWhiteSpace(command.TargetEntityKey)
                ? ApplyCurrentEntity(run, command, mutate)
                : ApplyTargetEntity(run, command.TargetEntityKey, mutate);
        }

        private static bool ApplyCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeEntitySnapshotCommit> mutate)
        {
            var entityKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey
                : command.ActorEntityKey;
            return ApplyTargetEntity(run, entityKey, mutate);
        }

        private static bool ApplyCurrentEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            var entityKey = string.IsNullOrWhiteSpace(command.TargetEntityKey)
                ? command.ActorEntityKey
                : command.TargetEntityKey;
            if (string.IsNullOrWhiteSpace(entityKey))
                entityKey = run.CurrentEntityKey;

            return ApplyTargetEquipmentItem(run, entityKey, command.EquipmentIndex, mutate);
        }

        private static bool ApplyTargetEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            return ApplyTargetEquipmentItem(run, command.TargetEntityKey, command.EquipmentIndex, mutate);
        }

        private static bool ApplyTargetEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int equipmentIndex,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (equipmentIndex < 0 || equipmentIndex >= equipment.Count)
                return false;

            var item = equipment[equipmentIndex]?.Item;
            if (item == null)
                return false;

            mutate(item);
            return true;
        }

        private static bool ApplySetWeaponGroupMembership(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out _, out _, out var entity) ||
                command.EquipmentIndex < 0 ||
                command.WeaponGroup < 0)
            {
                return false;
            }

            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (command.EquipmentIndex >= equipment.Count ||
                equipment[command.EquipmentIndex]?.Item == null)
            {
                return false;
            }

            var sourceGroups = entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
            var groupCount = Math.Max(sourceGroups.Count, command.WeaponGroup + 1);
            var groups = new IReadOnlyList<int>[groupCount];
            for (var i = 0; i < groupCount; i++)
            {
                groups[i] = i < sourceGroups.Count
                    ? (sourceGroups[i] ?? Array.Empty<int>()).ToArray()
                    : Array.Empty<int>();
            }

            var members = groups[command.WeaponGroup]
                .Where(index => index != command.EquipmentIndex)
                .ToList();
            if (command.ScalarValue > 0.5)
                members.Add(command.EquipmentIndex);

            groups[command.WeaponGroup] = members
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            entity.WeaponGroups = groups;
            return true;
        }

        private static bool ApplySetThermotoggleTargetTemperature(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out _, out _, out var entity))
                return false;

            var behavior = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.OwnerKind, "equipment", StringComparison.Ordinal) &&
                    candidate.OwnerIndex == command.EquipmentIndex &&
                    candidate.BehaviorIndex == command.BehaviorIndex);
            if (behavior == null)
                return false;

            behavior.ThermotoggleTargetTemperature = command.ScalarValue;
            return true;
        }

        private static bool ApplyToggleEquipmentBehaviorItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            string behaviorKind,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            var entityKey = string.IsNullOrWhiteSpace(command.TargetEntityKey)
                ? command.ActorEntityKey
                : command.TargetEntityKey;
            if (string.IsNullOrWhiteSpace(entityKey))
                entityKey = run.CurrentEntityKey;

            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            var behavior = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.OwnerKind, "equipment", StringComparison.Ordinal) &&
                    string.Equals(candidate.BehaviorKind, behaviorKind, StringComparison.Ordinal));
            if (behavior == null || behavior.OwnerIndex < 0)
                return false;

            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (behavior.OwnerIndex >= equipment.Count)
                return false;

            var item = equipment[behavior.OwnerIndex]?.Item;
            if (item == null)
                return false;

            mutate(item);
            return true;
        }

        private static bool ApplyTransferCargoItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var transfer = command.CargoTransfer ?? new AetheriaRuntimeCargoTransferCommand();
            var quantity = transfer.Quantity > 0
                ? transfer.Quantity
                : (int)Math.Round(command.ScalarValue);
            if (!TryResolveEntity(run, transfer.OriginEntityKey, out _, out _, out var origin) ||
                !TryResolveEntity(run, transfer.DestinationEntityKey, out _, out _, out var destination))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoSource);
            if (!CanAccessCargoTransfer(run, command.ActorEntityKey, transfer.OriginEntityKey, origin,
                    transfer.DestinationEntityKey, destination))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.CargoAccessDenied);

            return AetheriaRuntimeRefitTransactions.TryTransferCargo(
                    origin,
                    transfer.OriginCargoIndex,
                    transfer.SourceX,
                    transfer.SourceY,
                    destination,
                    transfer.DestinationCargoIndex,
                    command.TextValue ?? "",
                    quantity,
                    transfer.DestinationX,
                    transfer.DestinationY,
                    transfer.HasDestinationPosition,
                    context.Catalog,
                    out var reason)
                || context.Reject(reason);
        }

        private static bool CanAccessCargoTransfer(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            string originEntityKey,
            AetheriaRuntimeEntitySnapshotCommit origin,
            string destinationEntityKey,
            AetheriaRuntimeEntitySnapshotCommit destination)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var actorZoneIndex, out _, out var actor))
                return false;
            var actorIsOrigin = ReferenceEquals(actor, origin);
            var actorIsDestination = ReferenceEquals(actor, destination);
            if (!actorIsOrigin && !actorIsDestination)
                return false;

            var other = actorIsOrigin ? destination : origin;
            var otherKey = actorIsOrigin ? destinationEntityKey : originEntityKey;
            if (ReferenceEquals(actor, other) ||
                (TryResolveDockParent(run, actorEntityKey, out _, out var actorParent) && ReferenceEquals(actorParent, other)) ||
                (TryResolveDockParent(run, otherKey, out _, out var otherParent) && ReferenceEquals(otherParent, actor)))
                return true;

            if (!TryResolveEntity(run, otherKey, out var otherZoneIndex, out _, out _) ||
                actorZoneIndex != otherZoneIndex)
                return false;

            var dx = actor.PositionX - other.PositionX;
            var dz = actor.PositionZ - other.PositionZ;
            var reach = CargoInteractionRadius(actor) + CargoInteractionRadius(other) + 10.0;
            return dx * dx + dz * dz <= reach * reach;
        }

        private static double CargoInteractionRadius(AetheriaRuntimeEntitySnapshotCommit entity) =>
            string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48.0 : 20.0;

        private static bool ApplyEquipItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var transfer = command.EquipmentTransfer ?? new AetheriaRuntimeEquipmentTransferCommand();
            var originKey = transfer.OriginEntityKey ?? "";
            var destinationKey = transfer.DestinationEntityKey ?? "";
            if (!TryResolveEntity(run, originKey, out _, out _, out var origin) ||
                !TryResolveEntity(run, destinationKey, out _, out _, out var destination))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource);
            if (!TryResolveDockParent(run, destinationKey, out _, out var parent))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.RefitRequiresDocked);

            var sourceKind = string.IsNullOrWhiteSpace(transfer.SourceKind)
                ? AetheriaRuntimeRefitSourceKinds.Cargo
                : transfer.SourceKind;
            var sourceIsCargo = string.Equals(sourceKind, AetheriaRuntimeRefitSourceKinds.Cargo, StringComparison.Ordinal);
            if ((!sourceIsCargo && !ReferenceEquals(origin, destination)) ||
                (sourceIsCargo &&
                 !ReferenceEquals(origin, destination) &&
                 !ReferenceEquals(origin, parent)))
            {
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.RefitAccessDenied);
            }

            return AetheriaRuntimeRefitTransactions.TryEquip(
                    origin,
                    sourceKind,
                    transfer.OriginIndex,
                    transfer.SourceX,
                    transfer.SourceY,
                    destination,
                    command.TextValue ?? "",
                    transfer.DestinationX,
                    transfer.DestinationY,
                    transfer.HasDestinationPosition,
                    context.Catalog,
                    out var reason)
                || context.Reject(reason);
        }

        private static bool ApplyStoreItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var store = command.StoreItem ?? new AetheriaRuntimeStoreItemCommand();
            var originKey = store.OriginEntityKey ?? "";
            var destinationKey = store.DestinationEntityKey ?? "";
            if (!TryResolveEntity(run, originKey, out _, out _, out var origin) ||
                !TryResolveEntity(run, destinationKey, out _, out _, out var destination))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource);
            if (!TryResolveDockParent(run, originKey, out _, out var parent))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.RefitRequiresDocked);
            if (!ReferenceEquals(destination, origin) && !ReferenceEquals(destination, parent))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.RefitAccessDenied);

            return AetheriaRuntimeRefitTransactions.TryStore(
                    origin,
                    string.IsNullOrWhiteSpace(store.SourceKind)
                        ? AetheriaRuntimeRefitSourceKinds.Equipment
                        : store.SourceKind,
                    store.SourceEquipmentIndex,
                    destination,
                    store.DestinationCargoIndex,
                    command.TextValue ?? "",
                    store.DestinationX,
                    store.DestinationY,
                    store.HasDestinationPosition,
                    context.Catalog,
                    out var reason)
                || context.Reject(reason);
        }

        private static bool ApplyToggleHullConductivity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out _, out _, out var entity))
                return false;

            var x = (int)command.PositionX;
            var y = (int)command.PositionY;
            var axis = (int)command.ScalarValue;
            if (axis < 0 || axis > 1)
                return false;

            var gridName = axis == 0 ? "hull_conductivity_x" : "hull_conductivity_y";
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToArray();
            var grid = grids.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.Name ?? "", gridName, StringComparison.Ordinal));
            if (grid == null ||
                x < 0 ||
                y < 0 ||
                x >= grid.Width ||
                y >= grid.Height)
            {
                return false;
            }

            var values = (grid.Values ?? Array.Empty<double>()).ToArray();
            var index = y * grid.Width + x;
            if (index < 0 || index >= values.Length)
                return false;

            values[index] = values[index] > 0.5 ? 0.0 : 1.0;
            grid.Values = values;
            entity.StatGrids = grids;
            return true;
        }

        private static bool ApplyTradePurchase(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var purchase = command.TradePurchase ?? new AetheriaRuntimeTradePurchaseCommand();
            var itemKey = purchase.ItemKey ?? "";
            var typedItem = catalog?.FindItem(itemKey);
            var quantity = Math.Max(1, purchase.Quantity);
            if (typedItem == null ||
                !TryResolveDockParent(run, run.CurrentEntityKey, out var dockParentKey, out var dockParent))
                return false;

            if (!TryFindStationStock(
                    dockParent,
                    itemKey,
                    purchase.StationCargoIndex,
                    purchase.SourceX,
                    purchase.SourceY,
                    out var stationCargoIndex,
                    out var stockSlot))
            {
                return false;
            }
            var stationEntity = dockParent;

            var unitPrice = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
                typedItem,
                stockSlot.Item,
                catalog.TradeValueSettings).Price;
            var totalPrice = checked(unitPrice * quantity);
            if (unitPrice < 0 || run.Credits < totalPrice)
                return false;

            var createsDockedShip = !string.IsNullOrWhiteSpace(typedItem.HullType);
            if (createsDockedShip)
            {
                if (quantity != 1 || !HasAvailableDockingBay(dockParent))
                    return false;
                if (!TryRemoveCargoItemQuantity(
                        stationEntity,
                        stationCargoIndex,
                        itemKey,
                        purchase.SourceX,
                        purchase.SourceY,
                        1,
                        out var purchasedHull))
                    return false;
                if (!ApplyCreateDockedShipPurchase(run, dockParentKey, itemKey, out var purchasedShipKey))
                {
                    AddCargoItem(stationEntity, stationCargoIndex, purchasedHull);
                    return false;
                }
                run.CurrentEntityKey = purchasedShipKey;
                run.Credits -= totalPrice;
                return true;
            }

            if (!TryResolveCargoBay(
                    run,
                    run.CurrentEntityKey,
                    Math.Max(0, purchase.TargetCargoIndex),
                    out var targetEntity,
                    out var targetCargoIndex,
                    out _) ||
                !IsEntityDockedAt(run, run.CurrentEntityKey, dockParent) ||
                quantity > AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(targetEntity, catalog, itemKey, targetCargoIndex))
            {
                return false;
            }

            if (!TryRemoveCargoItemQuantity(
                    stationEntity,
                    stationCargoIndex,
                    itemKey,
                    purchase.SourceX,
                    purchase.SourceY,
                    quantity,
                    out var purchasedSlot))
            {
                return false;
            }

            purchasedSlot.X = 0;
            purchasedSlot.Y = 0;
            AddCargoItem(targetEntity, targetCargoIndex, purchasedSlot);
            run.Credits -= totalPrice;
            return true;
        }

        private static bool ApplyTradeSale(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var sale = command.TradeSale ?? new AetheriaRuntimeTradeSaleCommand();
            var catalog = context.Catalog;
            if (catalog == null || sale.Quantity <= 0 || string.IsNullOrWhiteSpace(sale.ItemKey))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidTradeSource);

            if (!TryResolveEntity(run, run.CurrentEntityKey, out _, out _, out var seller) ||
                !TryResolveDockParent(run, run.CurrentEntityKey, out _, out var station) ||
                !IsEntityDockedAt(run, run.CurrentEntityKey, station))
            {
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.TradeRequiresDocked);
            }

            var sellerCargo = seller.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            if (sale.SourceCargoIndex < 0 || sale.SourceCargoIndex >= sellerCargo.Count ||
                !TryFindCargoItem(
                    sellerCargo[sale.SourceCargoIndex],
                    sale.ItemKey,
                    sale.SourceX,
                    sale.SourceY,
                    out var sourceSlot) ||
                sourceSlot.Item == null ||
                sale.Quantity > sourceSlot.Item.Quantity)
            {
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidTradeSource);
            }

            var typedItem = catalog.FindItem(sale.ItemKey);
            var unitPrice = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
                typedItem,
                sourceSlot.Item,
                catalog.TradeValueSettings).Price;
            var payout = (long)unitPrice * sale.Quantity;
            if (typedItem == null || unitPrice < 0 || payout < 0 || payout > int.MaxValue - (long)run.Credits)
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidTradePayout);

            var stationBayCount = Math.Min(
                station.CargoBays?.Count ?? 0,
                station.CargoContents?.Count ?? 0);
            for (var stationCargoIndex = 0; stationCargoIndex < stationBayCount; stationCargoIndex++)
            {
                if (!AetheriaRuntimeRefitTransactions.TryTransferCargo(
                        seller,
                        sale.SourceCargoIndex,
                        sale.SourceX,
                        sale.SourceY,
                        station,
                        stationCargoIndex,
                        sale.ItemKey,
                        sale.Quantity,
                        0,
                        0,
                        false,
                        catalog,
                        out _))
                {
                    continue;
                }

                run.Credits += (int)payout;
                return true;
            }

            return context.Reject(AetheriaRuntimeDaemonRejectionReasons.TradeStationNoFit);
        }

        private static bool TryResolveDockParent(
            AetheriaRuntimeRunCheckpointCommit run,
            string childEntityKey,
            out string parentEntityKey,
            out AetheriaRuntimeEntitySnapshotCommit parent)
        {
            parentEntityKey = "";
            parent = null!;
            if (!TryResolveEntity(run, childEntityKey, out var zoneIndex, out var childIndex, out _))
                return false;
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            parent = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(entity => entity != null &&
                    (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(childIndex))!;
            if (parent == null)
                return false;
            parentEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, parent.EntityIndex);
            return true;
        }

        private static bool IsEntityDockedAt(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            AetheriaRuntimeEntitySnapshotCommit parent)
        {
            return TryResolveEntity(run, entityKey, out _, out var entityIndex, out _) &&
                (parent.DockingBayAssignments ?? Array.Empty<int>()).Contains(entityIndex);
        }

        private static bool HasAvailableDockingBay(AetheriaRuntimeEntitySnapshotCommit parent)
        {
            var bays = parent.DockingBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            var assignments = parent.DockingBayAssignments ?? Array.Empty<int>();
            for (var index = 0; index < bays.Count; index++)
            {
                if (index >= assignments.Count || assignments[index] < 0)
                    return true;
            }
            return false;
        }

        private static bool TryFindCargoItem(
            AetheriaRuntimeCargoBayLoadoutCommit cargo,
            string itemKey,
            int x,
            int y,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = (cargo?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .FirstOrDefault(candidate => candidate?.Item != null &&
                    string.Equals(candidate.Item.ItemKey ?? "", itemKey, StringComparison.Ordinal) &&
                    candidate.X == x && candidate.Y == y)!;
            return slot != null;
        }

        private static bool TryFindStationStock(
            AetheriaRuntimeEntitySnapshotCommit station,
            string itemKey,
            int requestedCargoIndex,
            int requestedX,
            int requestedY,
            out int cargoIndex,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            cargoIndex = -1;
            slot = null!;
            var cargo = station.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            IEnumerable<int> indices = requestedCargoIndex >= 0 && requestedCargoIndex < cargo.Count
                ? new[] { requestedCargoIndex }
                : Enumerable.Range(0, cargo.Count);
            foreach (var index in indices)
            {
                if (TryFindCargoItem(cargo[index], itemKey, requestedX, requestedY, out slot))
                {
                    cargoIndex = index;
                    return true;
                }
            }
            return false;
        }

        private static bool ApplyCreateDockedShipPurchase(
            AetheriaRuntimeRunCheckpointCommit run,
            string dockParentKey,
            string itemKey,
            out string purchasedShipKey)
        {
            purchasedShipKey = "";
            if (string.IsNullOrWhiteSpace(itemKey) ||
                !TryResolveEntity(run, dockParentKey, out var zoneIndex, out var parentIndex, out var parent))
            {
                return false;
            }

            var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).ToArray();
            var zone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            var entityIndex = entities.Count;
            var ship = new AetheriaRuntimeEntitySnapshotCommit
            {
                EntityIndex = entityIndex,
                Name = string.IsNullOrWhiteSpace(itemKey) ? "Purchased Ship" : itemKey,
                Kind = "ship",
                HullItemKey = itemKey,
                DirectionX = parent.DirectionX,
                DirectionY = parent.DirectionY,
                PositionX = parent.PositionX,
                PositionY = parent.PositionY,
                PositionZ = parent.PositionZ,
                IsActive = true,
                TargetEntityIndex = -1,
                ShutdownPerformance = 0.25
            };
            entities.Add(ship);
            zone.Entities = entities.ToArray();
            run.Zones = zones;

            var childIndices = (parent.ChildEntityIndices ?? Array.Empty<int>()).ToList();
            if (!childIndices.Contains(entityIndex))
                childIndices.Add(entityIndex);
            parent.ChildEntityIndices = childIndices.ToArray();

            var assignments = (parent.DockingBayAssignments ?? Array.Empty<int>()).ToList();
            var assigned = false;
            for (var index = 0; index < assignments.Count; index++)
            {
                if (assignments[index] >= 0)
                    continue;

                assignments[index] = entityIndex;
                assigned = true;
                break;
            }

            if (!assigned)
                assignments.Add(entityIndex);
            parent.DockingBayAssignments = assignments.ToArray();

            purchasedShipKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, entityIndex);
            return true;
        }

        private static bool ApplyRestoreLoadout(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var restore = command.LoadoutRestore ?? new AetheriaRuntimeLoadoutRestoreCommand();
            var price = restore.Price;
            if (price < 0 || run.Credits < price)
                return false;

            var templateName = restore.TemplateName ?? "";
            if (string.IsNullOrWhiteSpace(command.TargetEntityKey) ||
                string.IsNullOrWhiteSpace(templateName))
            {
                return false;
            }

            var template = (context.LoadoutTemplates ?? Array.Empty<AetheriaRuntimeLoadoutTemplateCommit>())
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.Name ?? "", templateName, StringComparison.Ordinal));
            if (template == null)
                return false;

            if (!AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(command.TargetEntityKey, out var zoneIndex, out _))
                return false;

            var newEntityKey = run.AppendLoadoutTemplateToZone(
                zoneIndex,
                command.TargetEntityKey,
                template);
            if (string.IsNullOrWhiteSpace(newEntityKey))
                return false;

            run.CurrentEntityKey = newEntityKey;
            run.Credits -= price;
            return true;
        }

        private static bool ApplySetMoveVector(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out _, out _, out _) ||
                !IsNormalizedScalar(command.ScalarValue) ||
                !IsFinite(command.DirectionX) ||
                !IsFinite(command.DirectionY))
            {
                return false;
            }

            intents.Movements.RemoveAll(intent =>
                string.Equals(intent.ActorEntityKey, actor, StringComparison.Ordinal));
            intents.Movements.Add(new AetheriaRuntimeDaemonMovementIntent
            {
                ActorEntityKey = actor,
                DirectionX = command.DirectionX,
                DirectionY = command.DirectionY,
                Magnitude = command.ScalarValue
            });
            return true;
        }

        private static bool ApplyFireWeaponGroup(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out _, out _, out var entity) ||
                !HasWeaponGroup(entity, command.WeaponGroup))
            {
                return false;
            }

            intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
            {
                ActorEntityKey = actor,
                WeaponGroup = command.WeaponGroup,
                Fire = true,
                Active = true
            });
            return true;
        }

        private static bool ApplySetWeaponGroupActive(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out _, out _, out var entity) ||
                !HasWeaponGroup(entity, command.WeaponGroup))
            {
                return false;
            }

            intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
            {
                ActorEntityKey = actor,
                WeaponGroup = command.WeaponGroup,
                Active = command.ScalarValue > 0.5
            });
            return true;
        }

        private static bool ApplySetBehaviorActive(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) ||
                command.EquipmentIndex < 0 ||
                command.BehaviorIndex < 0 ||
                !TryResolveEntity(run, actor, out _, out _, out var entity) ||
                !HasEquipmentBehavior(entity, command.EquipmentIndex, command.BehaviorIndex))
            {
                return false;
            }

            intents.Behaviors.Add(new AetheriaRuntimeDaemonBehaviorIntent
            {
                ActorEntityKey = actor,
                EquipmentIndex = command.EquipmentIndex,
                BehaviorIndex = command.BehaviorIndex,
                Active = command.ScalarValue > 0.5,
                TargetBodyKey = command.TextValue ?? "",
                TargetAsteroidIndex = (int)command.PositionX
            });
            return true;
        }

        private static bool HasWeaponGroup(AetheriaRuntimeEntitySnapshotCommit entity, int weaponGroup)
        {
            var weaponGroups = entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
            if (weaponGroup >= 0 && weaponGroup < weaponGroups.Count)
                return true;
            return weaponGroup == 0 &&
                   (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                   .Any(state => state != null &&
                       string.Equals(state.BehaviorKind, "ProjectileWeapon", StringComparison.Ordinal));
        }

        private static bool HasEquipmentBehavior(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int equipmentIndex,
            int behaviorIndex)
        {
            return (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Any(candidate =>
                    candidate != null &&
                    string.Equals(candidate.OwnerKind, "equipment", StringComparison.Ordinal) &&
                    candidate.OwnerIndex == equipmentIndex &&
                    candidate.BehaviorIndex == behaviorIndex);
        }

        private static bool IsNormalizedScalar(double value)
        {
            return IsFinite(value) && value >= 0.0 && value <= 1.0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool ApplyActivateConsumable(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(command.TextValue))
                return false;

            intents.Consumables.Add(new AetheriaRuntimeDaemonConsumableIntent
            {
                ActorEntityKey = actor,
                ItemKey = command.TextValue ?? ""
            });
            return true;
        }

        private static bool ApplySensorPing(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            if (string.IsNullOrWhiteSpace(ResolveActorEntityKey(run, command)))
                return false;

            intents.SensorPingRequested = true;
            return true;
        }

        private static bool ApplyDockIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context,
            double maximumDistance)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockActor);
            if (string.IsNullOrWhiteSpace(command.TargetEntityKey))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockTarget);

            if (!ApplyDockState(run, actor, command.TargetEntityKey, maximumDistance, context))
                return false;

            context.Intents.Docking.Add(new AetheriaRuntimeDaemonDockingIntent
            {
                ActorEntityKey = actor,
                TargetEntityKey = command.TargetEntityKey ?? "",
                Dock = true
            });
            return true;
        }

        private static bool ApplyDockNearestIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context,
            double defaultDockingDistance)
        {
            var actorKey = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actorKey) ||
                !TryResolveEntity(run, actorKey, out _, out _, out _))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockActor);
            // DockingDistance is daemon-owned gameplay state. A client-provided scalar must
            // not widen or narrow the fossil's interaction rule.
            var maximumDistance = defaultDockingDistance;
            if (!TryFindFirstDockTarget(
                    run,
                    actorKey,
                    maximumDistance,
                    out var targetKey))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.NoEligibleDockingBay);

            command.TargetEntityKey = targetKey;
            return ApplyDockIntent(run, command, context, maximumDistance);
        }

        private static bool ApplyUndockIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockActor);

            if (!ApplyUndockState(run, actor, context))
                return false;

            context.Intents.Docking.Add(new AetheriaRuntimeDaemonDockingIntent
            {
                ActorEntityKey = actor,
                Undock = true
            });
            return true;
        }

        private static bool ApplyInteractIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context,
            double defaultDockingDistance,
            double defaultWormholeExitRadius,
            double wormholeDistanceRatio)
        {
            var actorKey = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actorKey) ||
                !TryResolveEntity(run, actorKey, out var zoneIndex, out var actorIndex, out _))
            {
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockActor);
            }

            if (IsChildReferencedInZone(run, zoneIndex, actorIndex))
                return ApplyUndockIntent(run, command, context);

            if (TryFindNearestWormholeTarget(
                    run,
                    actorKey,
                    ResolveInteractionDistance(command.PositionX, defaultWormholeExitRadius),
                    wormholeDistanceRatio,
                    out var targetZoneIndex,
                    out _,
                    out _))
            {
                command.TargetZoneIndex = targetZoneIndex;
                return ApplyEnterWormholeIntent(run, command, context.Intents,
                    defaultWormholeExitRadius, wormholeDistanceRatio);
            }

            return ApplyDockNearestIntent(run, command, context, defaultDockingDistance);
        }

        private static double ResolveInteractionDistance(double commandDistance, double defaultDistance)
        {
            if (IsFinite(commandDistance) && commandDistance > 0.0)
                return commandDistance;

            return IsFinite(defaultDistance) && defaultDistance > 0.0
                ? defaultDistance
                : double.PositiveInfinity;
        }

        private static bool TryFindFirstDockTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            double maxDistance,
            out string targetEntityKey)
        {
            targetEntityKey = "";
            if (!TryResolveEntity(run, actorEntityKey, out var zoneIndex, out var actorIndex, out var actor))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var maxDistanceSq = maxDistance > 0.0 ? maxDistance * maxDistance : double.PositiveInfinity;
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            for (var index = 0; index < entities.Count; index++)
            {
                var candidate = entities[index];
                if (candidate == null || candidate.EntityIndex == actorIndex || !HasAvailableDockingBay(candidate))
                    continue;

                var deltaX = candidate.PositionX - actor.PositionX;
                var deltaZ = candidate.PositionZ - actor.PositionZ;
                var distanceSq = (deltaX * deltaX) + (deltaZ * deltaZ);
                if (distanceSq >= maxDistanceSq)
                    continue;

                targetEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(
                    run.RunId, zoneIndex, candidate.EntityIndex);
                return true;
            }

            return false;
        }

        private static bool TryFindNearestWormholeTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            double maxDistance,
            double wormholeDistanceRatio,
            out int targetZoneIndex,
            out double entryX,
            out double entryY)
        {
            targetZoneIndex = -1;
            entryX = 0.0;
            entryY = 0.0;
            if (!TryResolveEntity(run, actorEntityKey, out var zoneIndex, out _, out var actor))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var maxDistanceSq = maxDistance > 0.0 ? maxDistance * maxDistance : double.PositiveInfinity;
            var closestDistanceSq = double.PositiveInfinity;
            foreach (var exit in AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
                run,
                zone,
                AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
                    zone, AetheriaRuntimeDaemonRenderQueries.DefaultZoneRenderRadius),
                wormholeDistanceRatio))
            {
                var deltaX = exit.PositionX - actor.PositionX;
                var deltaY = exit.PositionZ - actor.PositionZ;
                var distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSq >= maxDistanceSq || distanceSq >= closestDistanceSq)
                    continue;

                closestDistanceSq = distanceSq;
                targetZoneIndex = exit.TargetZoneIndex;
                entryX = exit.PositionX;
                entryY = exit.PositionZ;
            }

            return targetZoneIndex >= 0;
        }

        private static bool ApplyDockState(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            string targetEntityKey,
            double maximumDistance,
            AetheriaRuntimeDaemonOperationContext context)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var actorZoneIndex, out var actorIndex, out var actor))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockActor);
            if (
                !TryResolveEntity(run, targetEntityKey, out var targetZoneIndex, out var targetIndex, out var target))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockTarget);

            if (actorZoneIndex != targetZoneIndex || actorIndex == targetIndex)
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockTarget);

            if (IsChildReferencedInZone(run, actorZoneIndex, actorIndex))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.AlreadyDocked);

            var deltaX = target.PositionX - actor.PositionX;
            var deltaZ = target.PositionZ - actor.PositionZ;
            var maximumDistanceSq = maximumDistance > 0
                ? maximumDistance * maximumDistance
                : double.PositiveInfinity;
            if ((deltaX * deltaX) + (deltaZ * deltaZ) >= maximumDistanceSq)
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockTarget);

            var bays = target.DockingBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            var existingAssignments = target.DockingBayAssignments ?? Array.Empty<int>();
            var bayIndex = -1;
            for (var index = 0; index < bays.Count; index++)
            {
                if (index >= existingAssignments.Count || existingAssignments[index] < 0)
                {
                    bayIndex = index;
                    break;
                }
            }
            if (bayIndex < 0)
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.NoEligibleDockingBay);

            var childIndices = (target.ChildEntityIndices ?? Array.Empty<int>()).ToList();
            if (!childIndices.Contains(actorIndex))
                childIndices.Add(actorIndex);
            target.ChildEntityIndices = childIndices.ToArray();

            var assignments = Enumerable.Range(0, bays.Count)
                .Select(index => index < existingAssignments.Count ? existingAssignments[index] : -1)
                .ToArray();
            assignments[bayIndex] = actorIndex;
            target.DockingBayAssignments = assignments.ToArray();
            return true;
        }

        private static bool ApplyUndockState(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            AetheriaRuntimeDaemonOperationContext context)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var zoneIndex, out var actorIndex, out var actor))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidDockActor);
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            var parent = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(entity => entity != null &&
                    ((entity.ChildEntityIndices ?? Array.Empty<int>()).Contains(actorIndex) ||
                     (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(actorIndex)));
            if (parent == null)
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.NotDocked);

            var assignments = parent.DockingBayAssignments ?? Array.Empty<int>();
            var dockingBayIndex = -1;
            for (var index = 0; index < assignments.Count; index++)
            {
                if (assignments[index] == actorIndex)
                {
                    dockingBayIndex = index;
                    break;
                }
            }
            if (dockingBayIndex < 0)
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.NotDocked);
            if (!HasInstalledBehavior(actor, context.Catalog, "Cockpit"))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.MissingCockpit);
            if (!HasInstalledBehavior(actor, context.Catalog, "Thruster") &&
                !HasInstalledBehavior(actor, context.Catalog, "AetherDrive"))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.MissingPropulsion);
            if (!HasInstalledBehavior(actor, context.Catalog, "Reactor"))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.MissingReactor);

            var dockingBayContents = parent.DockingBayContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            if (dockingBayIndex < dockingBayContents.Count &&
                (dockingBayContents[dockingBayIndex]?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Any(slot => slot?.Item != null))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.DockingBayCargoNotEmpty);

            if (!RemoveChildReferenceFromZone(run, zoneIndex, actorIndex))
                return context.Reject(AetheriaRuntimeDaemonRejectionReasons.NotDocked);
            return true;
        }

        private static bool HasInstalledBehavior(
            AetheriaRuntimeEntitySnapshotCommit actor,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string behaviorKind) =>
            AetheriaRuntimeEquippedBehaviorQueries.Find(actor, catalog, behaviorKind).Count > 0;

        private static bool IsChildReferencedInZone(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int childEntityIndex)
        {
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;

                if ((entity.ChildEntityIndices ?? Array.Empty<int>()).Contains(childEntityIndex))
                    return true;

                if ((entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(childEntityIndex))
                    return true;
            }

            return false;
        }

        private static bool RemoveChildReferenceFromZone(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int childEntityIndex)
        {
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var removed = false;
            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;

                var childIndices = (entity.ChildEntityIndices ?? Array.Empty<int>()).ToList();
                if (childIndices.RemoveAll(index => index == childEntityIndex) > 0)
                {
                    entity.ChildEntityIndices = childIndices.ToArray();
                    removed = true;
                }

                var assignments = (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray();
                for (var index = 0; index < assignments.Length; index++)
                {
                    if (assignments[index] != childEntityIndex)
                        continue;

                    assignments[index] = -1;
                    removed = true;
                }

                entity.DockingBayAssignments = assignments;
            }

            return removed;
        }

        private static bool ApplyEnterWormholeIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents,
            double maximumDistance,
            double wormholeDistanceRatio)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || command.TargetZoneIndex < 0 ||
                !TryResolveEntity(run, actor, out var sourceZoneIndex, out var sourceEntityIndex, out var entity) ||
                IsChildReferencedInZone(run, sourceZoneIndex, sourceEntityIndex) ||
                intents.Wormholes.Any(intent => string.Equals(intent.ActorEntityKey, actor, StringComparison.Ordinal)) ||
                entity.WormholeTransition != null)
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == sourceZoneIndex);
            var exit = AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
                    run,
                    zone,
                    AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
                        zone, AetheriaRuntimeDaemonRenderQueries.DefaultZoneRenderRadius),
                    wormholeDistanceRatio)
                .FirstOrDefault(candidate => candidate.TargetZoneIndex == command.TargetZoneIndex);
            if (exit.TargetZoneIndex != command.TargetZoneIndex)
                return false;
            var dx = exit.PositionX - entity.PositionX;
            var dz = exit.PositionZ - entity.PositionZ;
            if (dx * dx + dz * dz >= maximumDistance * maximumDistance)
                return false;
            var targetZone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == command.TargetZoneIndex);
            var returnExit = AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
                    run,
                    targetZone,
                    AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
                        targetZone, AetheriaRuntimeDaemonRenderQueries.DefaultZoneRenderRadius),
                    wormholeDistanceRatio)
                .FirstOrDefault(candidate => candidate.TargetZoneIndex == sourceZoneIndex);
            if (returnExit.TargetZoneIndex != sourceZoneIndex)
                return false;

            intents.Wormholes.Add(new AetheriaRuntimeDaemonWormholeIntent
            {
                ActorEntityKey = actor,
                SourceZoneIndex = sourceZoneIndex,
                TargetZoneIndex = command.TargetZoneIndex,
                EntryWormholeX = exit.PositionX,
                EntryWormholeZ = exit.PositionZ,
                ExitWormholeX = returnExit.PositionX,
                ExitWormholeZ = returnExit.PositionZ,
                CommandId = command.CommandId ?? ""
            });
            return true;
        }

        private static bool ApplyTowToStation(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            double attachmentDistance)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out var actorZone, out _, out var towing) ||
                !TryResolveEntity(run, command.TargetEntityKey, out var stationZone, out var stationIndex, out var station) || actorZone != stationZone)
                return false;
            var children = (towing.ChildEntityIndices ?? Array.Empty<int>()).ToList();
            if (string.Equals(command.TextValue, "attach", StringComparison.Ordinal))
            {
                if (children.Contains(stationIndex) || Math.Pow(station.PositionX - towing.PositionX, 2) + Math.Pow(station.PositionZ - towing.PositionZ, 2) > attachmentDistance * attachmentDistance)
                    return false;
                children.Add(stationIndex); towing.ChildEntityIndices = children; station.OrbitKey = ""; station.PositionX = towing.PositionX; station.PositionZ = towing.PositionZ; return true;
            }
            if (!string.Equals(command.TextValue, "detach", StringComparison.Ordinal) || !children.Remove(stationIndex) || string.IsNullOrWhiteSpace(command.SubjectKey) || command.ScalarValue <= 0)
                return false;
            towing.ChildEntityIndices = children; station.PositionX = command.PositionX; station.PositionZ = command.PositionZ;
            var zone = run.Zones.First(value => value.ZoneIndex == actorZone);
            var orbitKey = $"tow:{command.CommandId}";
            zone.Orbits = (zone.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>()).Concat(new[] { new AetheriaRuntimeOrbitSnapshotCommit { OrbitKey = orbitKey, ParentOrbitKey = command.SubjectKey, Distance = command.ScalarValue, FixedPositionX = command.PositionX, FixedPositionY = command.PositionZ } }).ToArray();
            station.OrbitKey = orbitKey; return true;
        }

        internal static bool MoveEntityToZone(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int targetZoneIndex,
            double positionX,
            double positionY,
            out string movedEntityKey)
        {
            movedEntityKey = "";

            if (!AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(entityKey, out var sourceZoneIndex, out var sourceEntityIndex))
                return false;

            var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).ToList();
            var sourceZone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == sourceZoneIndex);
            if (sourceZone == null)
                return false;

            var sourceEntities = (sourceZone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            if (sourceEntityIndex < 0 || sourceEntityIndex >= sourceEntities.Count)
                return false;

            var movedEntity = sourceEntities[sourceEntityIndex];
            if (movedEntity == null)
                return false;

            if (IsChildReferencedInZone(run, sourceZoneIndex, sourceEntityIndex))
                return false;

            var movedCurrentEntity = AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(run.CurrentEntityKey, out var currentZoneIndex, out var currentEntityIndex) &&
                currentZoneIndex == sourceZoneIndex &&
                currentEntityIndex == sourceEntityIndex;

            RemoveChildReferenceFromZone(run, sourceZoneIndex, sourceEntityIndex);
            sourceEntities.RemoveAt(sourceEntityIndex);
            sourceZone.Entities = sourceEntities.ToArray();
            ReindexZoneAfterEntityRemoval(sourceZone, sourceEntityIndex);

            var targetZone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == targetZoneIndex);
            if (targetZone == null)
            {
                targetZone = new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = targetZoneIndex,
                    Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()
                };
                zones.Add(targetZone);
                run.Zones = zones.ToArray();
            }

            var targetEntities = (targetZone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            var targetEntityIndex = targetEntities.Count;
            movedEntity.EntityIndex = targetEntityIndex;
            movedEntity.TargetEntityIndex = -1;
            movedEntity.PositionX = positionX;
            movedEntity.PositionZ = positionY;
            targetEntities.Add(movedEntity);
            targetZone.Entities = targetEntities.ToArray();

            movedEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, targetZoneIndex, targetEntityIndex);
            if (movedCurrentEntity)
            {
                run.CurrentZoneIndex = targetZoneIndex;
                run.CurrentEntityKey = movedEntityKey;
            }
            else
            {
                ReindexCurrentEntityKeyAfterRemoval(run, sourceZoneIndex, sourceEntityIndex);
            }

            var discovered = (run.DiscoveredZoneIndices ?? Array.Empty<int>()).ToList();
            if (!discovered.Contains(targetZoneIndex))
            {
                discovered.Add(targetZoneIndex);
                run.DiscoveredZoneIndices = discovered.ToArray();
            }

            return true;
        }

        private static void ReindexZoneAfterEntityRemoval(AetheriaRuntimeZoneSnapshotCommit zone, int removedEntityIndex)
        {
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                if (entity == null)
                    continue;

                entity.EntityIndex = index;
                entity.TargetEntityIndex = ReindexEntityReference(entity.TargetEntityIndex, removedEntityIndex);
                entity.ChildEntityIndices = (entity.ChildEntityIndices ?? Array.Empty<int>())
                    .Select(childIndex => ReindexEntityReference(childIndex, removedEntityIndex))
                    .Where(childIndex => childIndex >= 0)
                    .ToArray();
                entity.DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>())
                    .Select(childIndex => ReindexEntityReference(childIndex, removedEntityIndex))
                    .ToArray();
                entity.Contacts = (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Select(contact =>
                    {
                        if (contact != null)
                            contact.TargetEntityIndex = ReindexEntityReference(contact.TargetEntityIndex, removedEntityIndex);
                        return contact;
                    })
                    .Where(contact => contact != null && contact.TargetEntityIndex >= 0)
                    .ToArray();
            }
        }

        private static void ReindexCurrentEntityKeyAfterRemoval(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int removedEntityIndex)
        {
            if (!AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(run.CurrentEntityKey, out var currentZoneIndex, out var currentEntityIndex) ||
                currentZoneIndex != zoneIndex)
            {
                return;
            }

            var reindexedCurrent = ReindexEntityReference(currentEntityIndex, removedEntityIndex);
            run.CurrentEntityKey = reindexedCurrent < 0
                ? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, reindexedCurrent);
        }

        private static bool ApplyTargetEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            Action<AetheriaRuntimeEntitySnapshotCommit> mutate)
        {
            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            mutate(entity);
            return true;
        }

        private static bool TryResolveEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            out int zoneIndex,
            out int entityIndex,
            out AetheriaRuntimeEntitySnapshotCommit entity)
        {
            entity = null!;
            zoneIndex = -1;
            entityIndex = -1;

            if (!AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(entityKey, out zoneIndex, out entityIndex))
                return false;

            var parsedZoneIndex = zoneIndex;
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == parsedZoneIndex);
            var entities = zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            if (entityIndex < 0 || entityIndex >= entities.Count)
                return false;

            entity = entities[entityIndex];
            return entity != null;
        }

        private static int ReindexEntityReference(int referencedEntityIndex, int removedEntityIndex)
        {
            if (referencedEntityIndex < 0)
                return referencedEntityIndex;

            if (referencedEntityIndex == removedEntityIndex)
                return -1;

            return referencedEntityIndex > removedEntityIndex
                ? referencedEntityIndex - 1
                : referencedEntityIndex;
        }

        private static string ResolveActorEntityKey(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            return string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey ?? ""
                : command.ActorEntityKey ?? "";
        }

        private static bool TryResolveCargoBay(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int cargoIndex,
            out AetheriaRuntimeEntitySnapshotCommit entity,
            out int resolvedCargoIndex,
            out AetheriaRuntimeCargoBayLoadoutCommit cargo)
        {
            entity = null!;
            cargo = null!;
            resolvedCargoIndex = -1;

            if (!TryResolveEntity(run, entityKey, out _, out _, out entity))
                return false;

            var cargoContents = entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            if (cargoIndex < 0 || cargoIndex >= cargoContents.Count)
                return false;

            cargo = cargoContents[cargoIndex];
            if (cargo == null)
                return false;

            resolvedCargoIndex = cargoIndex;
            return true;
        }

        private static bool TryRemoveCargoItem(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            string itemKey,
            int x,
            int y,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = null!;
            var cargoContents = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToArray();
            if (cargoIndex < 0 || cargoIndex >= cargoContents.Length || cargoContents[cargoIndex] == null)
                return false;

            var items = (cargoContents[cargoIndex].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var itemIndex = items.FindIndex(candidate => IsCargoSlotMatch(candidate, itemKey, x, y));
            if (itemIndex < 0)
                return false;

            slot = items[itemIndex];
            items.RemoveAt(itemIndex);
            cargoContents[cargoIndex] = new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = items.ToArray()
            };
            entity.CargoContents = cargoContents;
            return true;
        }

        private static bool TryRemoveCargoItemQuantity(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            string itemKey,
            int x,
            int y,
            int quantity,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            return AetheriaRuntimeCargoTransactions.TryRemoveQuantity(
                entity, cargoIndex, itemKey, x, y, quantity, out slot);
        }

        private static void AddCargoItem(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var cargoContents = EnsureCargoContents(entity, cargoIndex + 1);
            var cargo = cargoContents[cargoIndex] ?? new AetheriaRuntimeCargoBayLoadoutCommit();
            cargoContents[cargoIndex] = new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = (cargo.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    .Concat(new[] { slot })
                    .ToArray()
            };
            entity.CargoContents = cargoContents;
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit CloneSlot(AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var item = slot?.Item;
            return new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot?.X ?? 0,
                Y = slot?.Y ?? 0,
                Rotation = slot?.Rotation ?? "None",
                Item = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = item?.ItemKey ?? "",
                    Quality = item?.Quality ?? 1.0,
                    Durability = item?.Durability ?? 1.0,
                    Quantity = item?.Quantity ?? 1,
                    Enabled = item?.Enabled ?? true,
                    OverrideShutdown = item?.OverrideShutdown ?? false,
                    Temperature = item?.Temperature ?? 0
                }
            };
        }

        private static bool TryRemoveEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int equipmentIndex,
            string itemKey,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = null!;
            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            var equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            if (equipmentIndex < 0 || equipmentIndex >= equipment.Count)
                return false;

            slot = equipment[equipmentIndex];
            if (!IsItemMatch(slot?.Item, itemKey))
                return false;

            equipment.RemoveAt(equipmentIndex);
            entity.Equipment = equipment.ToArray();
            entity.WeaponGroups = (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>())
                    .Where(index => index != equipmentIndex)
                    .Select(index => index > equipmentIndex ? index - 1 : index)
                    .ToArray())
                .ToArray();
            return true;
        }

        private static void AddEquipmentItem(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            entity.Equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Concat(new[] { slot })
                .ToArray();
        }

        private static AetheriaRuntimeCargoBayLoadoutCommit[] EnsureCargoContents(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int count)
        {
            var source = entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            var cargoContents = new AetheriaRuntimeCargoBayLoadoutCommit[Math.Max(source.Count, count)];
            for (var i = 0; i < cargoContents.Length; i++)
            {
                cargoContents[i] = i < source.Count
                    ? source[i] ?? new AetheriaRuntimeCargoBayLoadoutCommit()
                    : new AetheriaRuntimeCargoBayLoadoutCommit();
            }

            return cargoContents;
        }

        private static bool IsCargoSlotMatch(
            AetheriaRuntimeLoadoutItemSlotCommit slot,
            string itemKey,
            int x,
            int y)
        {
            if (slot == null || !IsItemMatch(slot.Item, itemKey))
                return false;

            return x == int.MinValue ||
                   y == int.MinValue ||
                   (slot.X == x && slot.Y == y);
        }

        private static bool IsItemMatch(AetheriaRuntimeLoadoutItemCommit item, string itemKey)
        {
            return item != null &&
                   (string.IsNullOrWhiteSpace(itemKey) ||
                    string.Equals(item.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal));
        }

    }
}
