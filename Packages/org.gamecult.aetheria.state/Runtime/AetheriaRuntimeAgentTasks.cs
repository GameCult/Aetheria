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
            command.CommandId = string.Join(":", RuntimeId, task.TaskId, frameId.ToString(CultureInfo.InvariantCulture), phase);
            configure(command);
            return command;
        }

        private static void Complete(AetheriaRuntimeAgentTaskCommit task, AetheriaRuntimeEntitySnapshotCommit agent, long frameId)
        {
            task.Status = AetheriaRuntimeAgentTaskStatuses.Completed;
            task.CompletedFrameId = frameId;
            agent.AssignedAgentTaskId = "";
        }

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
