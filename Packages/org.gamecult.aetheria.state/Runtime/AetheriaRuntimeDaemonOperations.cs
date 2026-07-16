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
            AetheriaRuntimeDaemonIntentState? intents = null)
        {
            Run = run ?? new AetheriaRuntimeRunCheckpointCommit();
            AppliedCommandIds = appliedCommandIds ?? Array.Empty<string>();
            RejectedCommandIds = rejectedCommandIds ?? Array.Empty<string>();
            Intents = intents ?? new AetheriaRuntimeDaemonIntentState();
        }

        public AetheriaRuntimeRunCheckpointCommit Run { get; }
        public IReadOnlyList<string> AppliedCommandIds { get; }
        public IReadOnlyList<string> RejectedCommandIds { get; }
        public AetheriaRuntimeDaemonIntentState Intents { get; }
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
        public AetheriaRuntimeCatalogSnapshot? Catalog { get; set; }
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

            foreach (var command in commands ?? Enumerable.Empty<AetheriaRuntimeDaemonCommandDocument>())
            {
                if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
                    continue;

                if (ApplyOne(run, command, context))
                    applied.Add(command.CommandId);
                else
                    rejected.Add(command.CommandId);
            }

            return new AetheriaRuntimeDaemonOperationResult(run, applied, rejected, context.Intents);
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
                    return ApplyTransferCargoItem(run, command, context.Catalog);
                case AetheriaRuntimeDaemonCommandKinds.EquipItem:
                    return ApplyEquipItem(run, command);
                case AetheriaRuntimeDaemonCommandKinds.StoreItem:
                    return ApplyStoreItem(run, command);
                case AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity:
                    return ApplyToggleHullConductivity(run, command);
                case AetheriaRuntimeDaemonCommandKinds.TradePurchase:
                    return ApplyTradePurchase(run, command, context.Catalog);
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
                    return ApplyDockIntent(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.DockNearest:
                    return ApplyDockNearestIntent(run, command, context.Intents, context.DockingDistance);
                case AetheriaRuntimeDaemonCommandKinds.Undock:
                    return ApplyUndockIntent(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.Interact:
                    return ApplyInteractIntent(
                        run,
                        command,
                        context.Intents,
                        context.DockingDistance,
                        context.WormholeExitRadius);
                case AetheriaRuntimeDaemonCommandKinds.EnterWormhole:
                    return ApplyEnterWormholeIntent(run, command, context.Intents);
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
                entity.DirectionX = command.DirectionX / length;
                entity.DirectionY = command.PositionZ / length;
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
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var transfer = command.CargoTransfer ?? new AetheriaRuntimeCargoTransferCommand();
            if (!TryResolveCargoBay(
                    run,
                    transfer.OriginEntityKey,
                    transfer.OriginCargoIndex,
                    out var originEntity,
                    out var originCargoIndex,
                    out _) ||
                !TryResolveCargoBay(
                    run,
                    transfer.DestinationEntityKey,
                    transfer.DestinationCargoIndex,
                    out var destinationEntity,
                    out var destinationCargoIndex,
                    out _))
            {
                return false;
            }

            if (!transfer.HasDestinationPosition &&
                string.Equals(transfer.OriginEntityKey ?? "", transfer.DestinationEntityKey ?? "", StringComparison.Ordinal) &&
                transfer.OriginCargoIndex == transfer.DestinationCargoIndex)
            {
                return false;
            }

            var sourceX = transfer.SourceX;
            var sourceY = transfer.SourceY;
            var quantity = transfer.Quantity > 0
                ? transfer.Quantity
                : (int)Math.Round(command.ScalarValue);
            if (quantity <= 0 || quantity > AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(destinationEntity, catalog, command.TextValue, destinationCargoIndex))
                return false;
            if (!TryRemoveCargoItemQuantity(
                    originEntity,
                    originCargoIndex,
                    command.TextValue,
                    sourceX,
                    sourceY,
                    quantity,
                    out var slot))
            {
                return false;
            }

            slot.X = transfer.HasDestinationPosition
                ? transfer.DestinationX
                : slot.X;
            slot.Y = transfer.HasDestinationPosition
                ? transfer.DestinationY
                : slot.Y;
            AddCargoItem(destinationEntity, destinationCargoIndex, slot);
            return true;
        }

        private static bool ApplyEquipItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var transfer = command.EquipmentTransfer ?? new AetheriaRuntimeEquipmentTransferCommand();
            var sourceKind = transfer.SourceKind ?? "";
            var originEntityKey = transfer.OriginEntityKey ?? "";
            var originIndex = transfer.OriginIndex;
            var sourceX = transfer.SourceX;
            var sourceY = transfer.SourceY;

            AetheriaRuntimeLoadoutItemSlotCommit slot;
            if (string.Equals(sourceKind, "equipment", StringComparison.Ordinal))
            {
                if (!TryRemoveEquipmentItem(run, originEntityKey, originIndex, command.TextValue, out slot))
                    return false;
            }
            else
            {
                if (!TryResolveCargoBay(run, originEntityKey, originIndex, out var originEntity, out var originCargoIndex, out _) ||
                    !TryRemoveCargoItem(originEntity, originCargoIndex, command.TextValue, sourceX, sourceY, out slot))
                {
                    return false;
                }
            }

            if (!TryResolveEntity(run, transfer.DestinationEntityKey, out _, out _, out var destinationEntity))
                return false;

            slot.X = transfer.HasDestinationPosition
                ? transfer.DestinationX
                : slot.X;
            slot.Y = transfer.HasDestinationPosition
                ? transfer.DestinationY
                : slot.Y;
            AddEquipmentItem(destinationEntity, slot);
            return true;
        }

        private static bool ApplyStoreItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var store = command.StoreItem ?? new AetheriaRuntimeStoreItemCommand();
            if (!TryRemoveEquipmentItem(
                    run,
                    store.OriginEntityKey,
                    store.SourceEquipmentIndex,
                    command.TextValue,
                    out var slot) ||
                !TryResolveCargoBay(
                    run,
                    store.DestinationEntityKey,
                    store.DestinationCargoIndex,
                    out var destinationEntity,
                    out var destinationCargoIndex,
                    out _))
            {
                return false;
            }

            slot.X = store.HasDestinationPosition
                ? store.DestinationX
                : slot.X;
            slot.Y = store.HasDestinationPosition
                ? store.DestinationY
                : slot.Y;
            AddCargoItem(destinationEntity, destinationCargoIndex, slot);
            return true;
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

        private static bool HasAvailableDockingBay(AetheriaRuntimeEntitySnapshotCommit parent) =>
            (parent.DockingBayAssignments ?? Array.Empty<int>()).Any(index => index < 0);

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
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(command.TargetEntityKey))
                return false;

            if (!ApplyDockState(run, actor, command.TargetEntityKey))
                return false;

            intents.Docking.Add(new AetheriaRuntimeDaemonDockingIntent
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
            AetheriaRuntimeDaemonIntentState intents,
            double defaultDockingDistance)
        {
            var actorKey = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actorKey) ||
                !TryFindNearestDockTarget(
                    run,
                    actorKey,
                    ResolveInteractionDistance(command.ScalarValue, defaultDockingDistance),
                    out var targetKey))
            {
                return false;
            }

            command.TargetEntityKey = targetKey;
            return ApplyDockIntent(run, command, intents);
        }

        private static bool ApplyUndockIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor))
                return false;

            if (!ApplyUndockState(run, actor))
                return false;

            intents.Docking.Add(new AetheriaRuntimeDaemonDockingIntent
            {
                ActorEntityKey = actor,
                Undock = true
            });
            return true;
        }

        private static bool ApplyInteractIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents,
            double defaultDockingDistance,
            double defaultWormholeExitRadius)
        {
            var actorKey = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actorKey) ||
                !TryResolveEntity(run, actorKey, out var zoneIndex, out var actorIndex, out _))
            {
                return false;
            }

            if (IsChildReferencedInZone(run, zoneIndex, actorIndex))
                return ApplyUndockIntent(run, command, intents);

            if (TryFindNearestWormholeTarget(
                    run,
                    actorKey,
                    ResolveInteractionDistance(command.PositionX, defaultWormholeExitRadius),
                    out var targetZoneIndex,
                    out var entryX,
                    out var entryY))
            {
                command.TargetZoneIndex = targetZoneIndex;
                command.PositionX = entryX;
                command.PositionY = entryY;
                return ApplyEnterWormholeIntent(run, command, intents);
            }

            return ApplyDockNearestIntent(run, command, intents, defaultDockingDistance);
        }

        private static double ResolveInteractionDistance(double commandDistance, double defaultDistance)
        {
            if (IsFinite(commandDistance) && commandDistance > 0.0)
                return commandDistance;

            return IsFinite(defaultDistance) && defaultDistance > 0.0
                ? defaultDistance
                : double.PositiveInfinity;
        }

        private static bool TryFindNearestDockTarget(
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
            var closestDistanceSq = double.PositiveInfinity;
            var closestIndex = -1;
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            for (var index = 0; index < entities.Count; index++)
            {
                var candidate = entities[index];
                if (candidate == null || index == actorIndex)
                    continue;

                var deltaX = candidate.PositionX - actor.PositionX;
                var deltaY = candidate.PositionY - actor.PositionY;
                var distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSq >= maxDistanceSq || distanceSq >= closestDistanceSq)
                    continue;

                closestDistanceSq = distanceSq;
                closestIndex = index;
            }

            if (closestIndex < 0)
                return false;

            targetEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, closestIndex);
            return true;
        }

        private static bool TryFindNearestWormholeTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            double maxDistance,
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
            foreach (var adjacentZoneIndex in zone.AdjacentZoneIndices ?? Array.Empty<int>())
            {
                var candidate = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                    .FirstOrDefault(candidateZone => candidateZone != null && candidateZone.ZoneIndex == adjacentZoneIndex);
                if (candidate == null)
                    continue;

                var deltaX = candidate.PositionX - actor.PositionX;
                var deltaY = candidate.PositionY - actor.PositionZ;
                var distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSq >= maxDistanceSq || distanceSq >= closestDistanceSq)
                    continue;

                closestDistanceSq = distanceSq;
                targetZoneIndex = adjacentZoneIndex;
                entryX = candidate.PositionX;
                entryY = candidate.PositionY;
            }

            return targetZoneIndex >= 0;
        }

        private static bool ApplyDockState(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            string targetEntityKey)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var actorZoneIndex, out var actorIndex, out _) ||
                !TryResolveEntity(run, targetEntityKey, out var targetZoneIndex, out var targetIndex, out var target))
            {
                return false;
            }

            if (actorZoneIndex != targetZoneIndex || actorIndex == targetIndex)
                return false;

            if (IsChildReferencedInZone(run, actorZoneIndex, actorIndex))
                return false;

            RemoveChildReferenceFromZone(run, actorZoneIndex, actorIndex);

            var childIndices = (target.ChildEntityIndices ?? Array.Empty<int>()).ToList();
            if (!childIndices.Contains(actorIndex))
                childIndices.Add(actorIndex);
            target.ChildEntityIndices = childIndices.ToArray();

            var assignments = (target.DockingBayAssignments ?? Array.Empty<int>()).ToList();
            var assigned = false;
            for (var index = 0; index < assignments.Count; index++)
            {
                if (assignments[index] >= 0)
                    continue;

                assignments[index] = actorIndex;
                assigned = true;
                break;
            }

            if (!assigned)
                assignments.Add(actorIndex);
            target.DockingBayAssignments = assignments.ToArray();
            return true;
        }

        private static bool ApplyUndockState(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var zoneIndex, out var actorIndex, out var actor))
                return false;
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            var parent = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(entity => entity != null &&
                    ((entity.ChildEntityIndices ?? Array.Empty<int>()).Contains(actorIndex) ||
                     (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(actorIndex)));
            if (parent == null || !RemoveChildReferenceFromZone(run, zoneIndex, actorIndex))
                return false;

            var directionLength = Math.Sqrt(
                parent.DirectionX * parent.DirectionX + parent.DirectionY * parent.DirectionY);
            var directionX = directionLength < 0.001 ? 0 : parent.DirectionX / directionLength;
            var directionZ = directionLength < 0.001 ? 1 : parent.DirectionY / directionLength;
            actor.PositionX = parent.PositionX + directionX * 72;
            actor.PositionZ = parent.PositionZ + directionZ * 72;
            actor.VelocityX = 0;
            actor.VelocityY = 0;
            actor.DirectionX = directionX;
            actor.DirectionY = directionZ;
            return true;
        }

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
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || command.TargetZoneIndex < 0)
                return false;

            if (!MoveEntityToZone(run, actor, command.TargetZoneIndex, command.PositionX, command.PositionY, out var movedEntityKey))
                return false;

            intents.Wormholes.Add(new AetheriaRuntimeDaemonWormholeIntent
            {
                ActorEntityKey = movedEntityKey,
                TargetZoneIndex = command.TargetZoneIndex,
                PositionX = command.PositionX,
                PositionY = command.PositionY
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

        private static bool MoveEntityToZone(
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
