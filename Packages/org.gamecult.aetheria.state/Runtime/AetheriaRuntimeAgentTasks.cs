using MessagePack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeAgentTaskTypes
    {
        public const string Mine = "mine";
        public const string Haul = "haul";
        public const string Tow = "tow";
        public const string Defend = "defend";
        public const string Attack = "attack";
        public const string Explore = "explore";

        public static readonly IReadOnlyList<string> All =
            new[] { Mine, Haul, Tow, Defend, Attack, Explore };
    }

    public static class AetheriaRuntimeAgentTaskStatuses
    {
        public const string Queued = "queued";
        public const string Assigned = "assigned";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";
        public const string Failed = "failed";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAgentTaskCommit
    {
        [Key(0)] public string TaskId { get; set; } = "";
        [Key(1)] public string CorporationKey { get; set; } = "";
        [Key(2)] public string TaskType { get; set; } = "";
        [Key(3)] public int Priority { get; set; }
        [Key(4)] public int ZoneIndex { get; set; } = -1;
        [Key(5)] public string Status { get; set; } = AetheriaRuntimeAgentTaskStatuses.Queued;
        [Key(6)] public int AssignedEntityIndex { get; set; } = -1;
        [Key(7)] public int TargetEntityIndex { get; set; } = -1;
        [Key(8)] public double TargetPositionX { get; set; }
        [Key(9)] public double TargetPositionZ { get; set; }
        [Key(10)] public double CompletionRadius { get; set; } = 10;
        [Key(11)] public int WeaponGroup { get; set; }
        [Key(12)] public long AssignedFrameId { get; set; } = -1;
        [Key(13)] public long CompletedFrameId { get; set; } = -1;
        [Key(14)] public int OriginEntityIndex { get; set; } = -1;
        [Key(15)] public string ItemKey { get; set; } = "";
        [Key(16)] public int RequestedQuantity { get; set; }
        [Key(17)] public int DeliveredQuantity { get; set; }
        [Key(18)] public int PendingQuantity { get; set; }
        [Key(19)] public string Phase { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAgentTaskCommand
    {
        [Key(0)] public string TaskId { get; set; } = "";
        [Key(1)] public string CorporationKey { get; set; } = "";
        [Key(2)] public string TaskType { get; set; } = "";
        [Key(3)] public int Priority { get; set; }
        [Key(4)] public int ZoneIndex { get; set; } = -1;
        [Key(5)] public int TargetEntityIndex { get; set; } = -1;
        [Key(6)] public double TargetPositionX { get; set; }
        [Key(7)] public double TargetPositionZ { get; set; }
        [Key(8)] public double CompletionRadius { get; set; } = 10;
        [Key(9)] public int WeaponGroup { get; set; }
        [Key(10)] public int OriginEntityIndex { get; set; } = -1;
        [Key(11)] public string ItemKey { get; set; } = "";
        [Key(12)] public int Quantity { get; set; }
    }

    public static class AetheriaRuntimeAgentScheduler
    {
        public const string RuntimeId = "aetheria.daemon.agent-scheduler";

        public static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> AssignAndPlan(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId)
        {
            if (run == null)
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();

            ReleaseInvalidAssignments(run);
            AssignQueuedTasks(run, frameId);
            return PlanAssignedTasks(run, frameId);
        }

        public static void Reconcile(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId,
            IReadOnlyCollection<string> appliedCommandIds,
            IReadOnlyCollection<string> rejectedCommandIds)
        {
            var applied = new HashSet<string>(appliedCommandIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var rejected = new HashSet<string>(rejectedCommandIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (var task in (run?.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null && string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Haul, StringComparison.Ordinal)))
            {
                var pickupId = CommandId(task, frameId, "pickup");
                var deliveryId = CommandId(task, frameId, "delivery");
                if (applied.Contains(pickupId))
                {
                    task.Phase = "delivery";
                    continue;
                }
                if (applied.Contains(deliveryId))
                {
                    task.DeliveredQuantity += task.PendingQuantity;
                    task.PendingQuantity = 0;
                    task.Phase = task.DeliveredQuantity >= task.RequestedQuantity ? "complete" : "pickup";
                    if (string.Equals(task.Phase, "complete", StringComparison.Ordinal))
                    {
                        var agent = FindEntity(run, task.ZoneIndex, task.AssignedEntityIndex);
                        if (agent != null)
                            Complete(task, agent, frameId);
                    }
                    continue;
                }
                if (rejected.Contains(pickupId) || rejected.Contains(deliveryId))
                    task.PendingQuantity = 0;
            }
        }

        private static void AssignQueuedTasks(AetheriaRuntimeRunCheckpointCommit run, long frameId)
        {
            var tasks = (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>()).Where(task => task != null).ToArray();
            var entities = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null)
                    .Select(entity => (zone.ZoneIndex, Entity: entity)))
                .ToArray();

            foreach (var corporation in tasks
                .Where(task => string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Queued, StringComparison.Ordinal))
                .GroupBy(task => task.CorporationKey ?? "", StringComparer.Ordinal))
            foreach (var typeAndZone in corporation.GroupBy(task => (TaskType: task.TaskType ?? "", task.ZoneIndex)))
            {
                var available = entities
                    .Where(pair => pair.Entity.IsActive && string.IsNullOrWhiteSpace(pair.Entity.AssignedAgentTaskId))
                    .Where(pair => string.Equals(pair.Entity.FactionKey ?? "", corporation.Key, StringComparison.Ordinal))
                    .Where(pair => pair.ZoneIndex == typeAndZone.Key.ZoneIndex)
                    .Where(pair => (pair.Entity.AgentTaskCapabilities ?? Array.Empty<string>()).Contains(typeAndZone.Key.TaskType, StringComparer.Ordinal))
                    .OrderBy(pair => pair.ZoneIndex)
                    .ThenBy(pair => pair.Entity.EntityIndex)
                    .ToArray();
                var orderedTasks = typeAndZone.OrderByDescending(task => task.Priority).ThenBy(task => task.TaskId, StringComparer.Ordinal);
                foreach (var assignment in orderedTasks.Zip(available, (task, agent) => (task, agent)))
                {
                    assignment.task.Status = AetheriaRuntimeAgentTaskStatuses.Assigned;
                    assignment.task.AssignedEntityIndex = assignment.agent.Entity.EntityIndex;
                    assignment.task.AssignedFrameId = frameId;
                    assignment.agent.Entity.AssignedAgentTaskId = assignment.task.TaskId;
                }
            }
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanAssignedTasks(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId)
        {
            var commands = new List<AetheriaRuntimeDaemonCommandDocument>();
            foreach (var task in (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null && string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Assigned, StringComparison.Ordinal)))
            {
                var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                    .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == task.ZoneIndex);
                var agent = zone?.Entities?.FirstOrDefault(entity => entity != null && entity.EntityIndex == task.AssignedEntityIndex);
                if (agent == null || !agent.IsActive)
                    continue;

                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Haul, StringComparison.Ordinal))
                {
                    commands.AddRange(PlanHaul(run, zone!, task, agent, frameId));
                    continue;
                }

                var target = task.TargetEntityIndex < 0
                    ? null
                    : zone!.Entities.FirstOrDefault(entity => entity != null && entity.EntityIndex == task.TargetEntityIndex);
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Attack, StringComparison.Ordinal) &&
                    (target == null || !target.IsActive))
                {
                    Complete(task, agent, frameId);
                    commands.Add(Movement(task, agent, frameId, 0, 0, 0));
                    continue;
                }
                var targetX = target?.PositionX ?? task.TargetPositionX;
                var targetZ = target?.PositionZ ?? task.TargetPositionZ;
                var dx = targetX - agent.PositionX;
                var dz = targetZ - agent.PositionZ;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance <= Math.Max(0.01, task.CompletionRadius) &&
                    !string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Attack, StringComparison.Ordinal))
                {
                    Complete(task, agent, frameId);
                    commands.Add(Movement(task, agent, frameId, 0, 0, 0));
                    continue;
                }

                var magnitude = distance <= 0.0001 ? 0 : 1;
                commands.Add(Movement(task, agent, frameId, dx, dz, magnitude));
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Attack, StringComparison.Ordinal) && target != null)
                {
                    commands.Add(Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.SetTarget, "target", command =>
                        command.TargetEntityKey = EntityKey(run, task.ZoneIndex, target.EntityIndex)));
                    commands.Add(Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, "fire", command =>
                        command.WeaponGroup = Math.Max(0, task.WeaponGroup)));
                }
            }
            return commands;
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanHaul(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId)
        {
            var pickup = !string.Equals(task.Phase, "delivery", StringComparison.Ordinal);
            var endpointIndex = pickup ? task.OriginEntityIndex : task.TargetEntityIndex;
            var endpoint = zone.Entities.FirstOrDefault(entity => entity != null && entity.EntityIndex == endpointIndex);
            if (endpoint == null)
            {
                Fail(task, agent);
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
            }

            var dx = endpoint.PositionX - agent.PositionX;
            var dz = endpoint.PositionZ - agent.PositionZ;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance > Math.Max(0.01, task.CompletionRadius))
                return new[] { Movement(task, agent, frameId, dx, dz, 1) };

            var source = pickup ? endpoint : agent;
            var destination = pickup ? agent : endpoint;
            var sourceSlot = (source.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .SelectMany((bay, bayIndex) => (bay?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    .Where(slot => string.Equals(slot?.Item?.ItemKey, task.ItemKey, StringComparison.Ordinal))
                    .Select(slot => (BayIndex: bayIndex, Slot: slot)))
                .FirstOrDefault();
            if (sourceSlot.Slot == null)
            {
                Fail(task, agent);
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
            }

            var remaining = task.RequestedQuantity - task.DeliveredQuantity;
            var quantity = Math.Min(remaining, Math.Max(1, sourceSlot.Slot.Item.Quantity));
            task.PendingQuantity = quantity;
            var transfer = Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.TransferCargoItem,
                pickup ? "pickup" : "delivery", command =>
                {
                    command.TextValue = task.ItemKey;
                    command.ScalarValue = quantity;
                    command.TargetEntityKey = EntityKey(run, task.ZoneIndex, destination.EntityIndex);
                    command.CargoTransfer = new AetheriaRuntimeCargoTransferCommand
                    {
                        OriginEntityKey = EntityKey(run, task.ZoneIndex, source.EntityIndex),
                        OriginCargoIndex = sourceSlot.BayIndex,
                        DestinationEntityKey = EntityKey(run, task.ZoneIndex, destination.EntityIndex),
                        DestinationCargoIndex = 0,
                        SourceX = sourceSlot.Slot.X,
                        SourceY = sourceSlot.Slot.Y,
                        Quantity = quantity
                    };
                });
            return new[]
            {
                Movement(task, agent, frameId, 0, 0, 0),
                transfer
            };
        }

        private static AetheriaRuntimeDaemonCommandDocument Movement(
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId,
            double dx,
            double dz,
            double magnitude)
        {
            return Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.SetMoveVector, "move", command =>
            {
                var length = Math.Sqrt(dx * dx + dz * dz);
                command.DirectionX = length <= 0.0001 ? 0 : dx / length;
                command.DirectionY = length <= 0.0001 ? 0 : dz / length;
                command.ScalarValue = magnitude;
            });
        }

        private static AetheriaRuntimeDaemonCommandDocument Command(
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId,
            AetheriaRuntimeDaemonCommandKinds kind,
            string phase,
            Action<AetheriaRuntimeDaemonCommandDocument> configure)
        {
            var actor = $"zone.{task.ZoneIndex}.entity.{agent.EntityIndex}";
            var command = AetheriaRuntimeDaemonCommandDocument.Create(kind, RuntimeId, "daemon-agent-scheduler", frameId, actor);
            command.CommandId = CommandId(task, frameId, phase);
            configure(command);
            return command;
        }

        private static void Complete(AetheriaRuntimeAgentTaskCommit task, AetheriaRuntimeEntitySnapshotCommit agent, long frameId)
        {
            task.Status = AetheriaRuntimeAgentTaskStatuses.Completed;
            task.CompletedFrameId = frameId;
            agent.AssignedAgentTaskId = "";
        }

        private static void Fail(AetheriaRuntimeAgentTaskCommit task, AetheriaRuntimeEntitySnapshotCommit agent)
        {
            task.Status = AetheriaRuntimeAgentTaskStatuses.Failed;
            task.PendingQuantity = 0;
            agent.AssignedAgentTaskId = "";
        }

        private static string CommandId(AetheriaRuntimeAgentTaskCommit task, long frameId, string phase) =>
            string.Join(":", RuntimeId, task.TaskId, frameId.ToString(CultureInfo.InvariantCulture), phase);

        private static AetheriaRuntimeEntitySnapshotCommit? FindEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int entityIndex) =>
            (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(zone => zone != null && zone.ZoneIndex == zoneIndex)
                ?.Entities?.FirstOrDefault(entity => entity != null && entity.EntityIndex == entityIndex);

        private static void ReleaseInvalidAssignments(AetheriaRuntimeRunCheckpointCommit run)
        {
            var activeTaskIds = (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null && string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Assigned, StringComparison.Ordinal))
                .Select(task => task.TaskId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var entity in (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()))
            {
                if (!string.IsNullOrWhiteSpace(entity.AssignedAgentTaskId) && !activeTaskIds.Contains(entity.AssignedAgentTaskId))
                    entity.AssignedAgentTaskId = "";
            }
        }

        private static string EntityKey(AetheriaRuntimeRunCheckpointCommit run, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, entityIndex);
    }
}
