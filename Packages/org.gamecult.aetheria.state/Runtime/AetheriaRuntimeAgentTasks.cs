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
        [Key(20)] public IReadOnlyList<string> TargetBodyKeys { get; set; } = Array.Empty<string>();
        [Key(21)] public int CircuitIndex { get; set; }
        [Key(22)] public string OrbitParentKey { get; set; } = "";
        [Key(23)] public double OrbitDistance { get; set; }
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
        [Key(13)] public IReadOnlyList<string> TargetBodyKeys { get; set; } = Array.Empty<string>();
        [Key(14)] public string OrbitParentKey { get; set; } = "";
        [Key(15)] public double OrbitDistance { get; set; }
    }

    public static class AetheriaRuntimeAgentScheduler
    {
        public const string RuntimeId = "aetheria.daemon.agent-scheduler";

        public static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> AssignAndPlan(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId,
            AetheriaRuntimeCatalogSnapshot? catalog = null,
            double simulationTimeSeconds = 0,
            AetheriaRuntimeDaemonSimulationSettings? simulationSettings = null)
        {
            if (run == null)
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();

            ReleaseInvalidAssignments(run);
            AssignQueuedTasks(run, frameId);
            var commands = PlanAssignedTasks(
                run,
                frameId,
                catalog,
                simulationTimeSeconds,
                simulationSettings ?? AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault).ToList();
            commands.AddRange(PlanIdleReturns(run, frameId));
            return commands;
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
            foreach (var task in (run?.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null && string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Mine, StringComparison.Ordinal)))
            {
                if (!rejected.Contains(CommandId(task, frameId, "offload")))
                    continue;
                var agent = FindEntity(run, task.ZoneIndex, task.AssignedEntityIndex);
                if (agent != null)
                    Complete(task, agent, frameId);
            }
            foreach (var task in (run?.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null && string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Tow, StringComparison.Ordinal)))
            {
                var detach = CommandId(task, frameId, "detach");
                var agent = FindEntity(run, task.ZoneIndex, task.AssignedEntityIndex);
                if (agent == null) continue;
                if (applied.Contains(detach)) Complete(task, agent, frameId);
                else if (rejected.Contains(detach)) Fail(task, agent);
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
            foreach (var taskType in corporation.GroupBy(task => task.TaskType ?? "", StringComparer.Ordinal))
            {
                var available = entities
                    .Where(pair => pair.Entity.IsActive && string.IsNullOrWhiteSpace(pair.Entity.AssignedAgentTaskId))
                    .Where(pair => !IsCurrentEntity(run, pair.ZoneIndex, pair.Entity.EntityIndex))
                    .Where(pair => string.Equals(pair.Entity.FactionKey ?? "", corporation.Key, StringComparison.Ordinal))
                    .Where(pair => (pair.Entity.AgentTaskCapabilities ?? Array.Empty<string>()).Contains(taskType.Key, StringComparer.Ordinal))
                    .OrderBy(pair => pair.ZoneIndex)
                    .ThenBy(pair => pair.Entity.EntityIndex)
                    .ToList();
                foreach (var task in taskType.OrderByDescending(task => task.Priority).ThenBy(task => task.TaskId, StringComparer.Ordinal))
                {
                    var assignment = available
                        .Select(agent => (Agent: agent, Route: FindZoneRoute(run, agent.ZoneIndex, task.ZoneIndex)))
                        .Where(candidate => candidate.Route.Count > 0)
                        .OrderBy(candidate => candidate.Route.Count)
                        .ThenBy(candidate => candidate.Agent.Entity.EntityIndex)
                        .FirstOrDefault();
                    if (assignment.Route == null || assignment.Route.Count == 0)
                        continue;
                    available.Remove(assignment.Agent);
                    task.Status = AetheriaRuntimeAgentTaskStatuses.Assigned;
                    task.AssignedEntityIndex = assignment.Agent.Entity.EntityIndex;
                    task.AssignedFrameId = frameId;
                    assignment.Agent.Entity.AssignedAgentTaskId = task.TaskId;
                }
            }
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanAssignedTasks(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double simulationTimeSeconds,
            AetheriaRuntimeDaemonSimulationSettings simulationSettings)
        {
            var commands = new List<AetheriaRuntimeDaemonCommandDocument>();
            foreach (var task in (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null && string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Assigned, StringComparison.Ordinal)))
            {
                var assignment = FindAssignedEntity(run, task);
                if (assignment.Entity == null || assignment.Zone == null || !assignment.Entity.IsActive)
                    continue;
                if (IsCurrentEntity(run, assignment.Zone.ZoneIndex, assignment.Entity.EntityIndex))
                    continue;
                task.AssignedEntityIndex = assignment.Entity.EntityIndex;
                if (assignment.Zone.ZoneIndex != task.ZoneIndex)
                {
                    var route = FindZoneRoute(run, assignment.Zone.ZoneIndex, task.ZoneIndex);
                    if (route.Count < 2)
                        continue;
                    var settings = AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;
                    var exit = AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
                            run,
                            assignment.Zone,
                            AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
                                assignment.Zone, AetheriaRuntimeDaemonRenderQueries.DefaultZoneRenderRadius),
                            settings.WormholeDistanceRatio)
                        .First(candidate => candidate.TargetZoneIndex == route[1]);
                    var travelDx = exit.PositionX - assignment.Entity.PositionX;
                    var travelDz = exit.PositionZ - assignment.Entity.PositionZ;
                    var travelDistance = Math.Sqrt(travelDx * travelDx + travelDz * travelDz);
                    if (travelDistance > AetheriaRuntimeDaemonOperationContext.DefaultWormholeExitRadius * 0.8)
                    {
                        commands.Add(MovementFromZone(task, assignment.Zone.ZoneIndex, assignment.Entity, frameId, travelDx, travelDz));
                    }
                    else
                    {
                        var travel = AetheriaRuntimeDaemonCommandDocument.Create(
                            AetheriaRuntimeDaemonCommandKinds.EnterWormhole,
                            RuntimeId,
                            "daemon-agent-scheduler",
                            frameId,
                            $"zone.{assignment.Zone.ZoneIndex}.entity.{assignment.Entity.EntityIndex}");
                        travel.CommandId = CommandId(task, frameId, "travel");
                        travel.TargetZoneIndex = route[1];
                        commands.Add(travel);
                    }
                    continue;
                }
                var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                    .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == task.ZoneIndex);
                var agent = assignment.Entity;
                if (agent == null || !agent.IsActive)
                    continue;

                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Haul, StringComparison.Ordinal))
                {
                    commands.AddRange(PlanHaul(run, zone!, task, agent, frameId));
                    continue;
                }
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Mine, StringComparison.Ordinal))
                {
                    commands.AddRange(PlanMining(zone!, task, agent, frameId, catalog, simulationTimeSeconds));
                    continue;
                }
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Explore, StringComparison.Ordinal) &&
                    (task.TargetBodyKeys ?? Array.Empty<string>()).Any(key => !string.IsNullOrWhiteSpace(key)))
                {
                    commands.AddRange(PlanSurvey(run, zone!, task, agent, frameId, catalog, simulationTimeSeconds));
                    continue;
                }
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Defend, StringComparison.Ordinal))
                {
                    commands.AddRange(PlanPatrol(zone!, task, agent, frameId));
                    continue;
                }
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Tow, StringComparison.Ordinal))
                {
                    commands.AddRange(PlanTow(run, zone!, task, agent, frameId));
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
                if (string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Attack, StringComparison.Ordinal) && target != null)
                {
                    commands.AddRange(PlanAttack(
                        run, task, agent, target, frameId, dx, dz, distance, catalog, simulationSettings));
                    continue;
                }
                if (distance <= Math.Max(0.01, task.CompletionRadius) &&
                    !string.Equals(task.TaskType, AetheriaRuntimeAgentTaskTypes.Attack, StringComparison.Ordinal))
                {
                    Complete(task, agent, frameId);
                    commands.Add(Movement(task, agent, frameId, 0, 0, 0));
                    continue;
                }

                var magnitude = distance <= 0.0001 ? 0 : 1;
                commands.Add(Movement(task, agent, frameId, dx, dz, magnitude));
            }
            return commands;
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanAttack(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            AetheriaRuntimeEntitySnapshotCommit target,
            long frameId,
            double dx,
            double dz,
            double distance,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var weapons = ResolveAgentWeapons(agent, catalog);
            if (weapons.Count == 0)
            {
                Fail(task, agent);
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
            }

            var optimum = SampleOptimumRange(run, task, agent, weapons, settings);
            var toTarget = Normalize(dx, dz, 0, 1);
            var targetForward = Normalize(target.DirectionX, target.DirectionY, 0, 1);
            var targetRight = new Vector2(targetForward.Y, -targetForward.X);
            var targetPortAlignment = Dot(targetRight, toTarget);
            var targetForeAlignment = Dot(new Vector2(-targetForward.X, -targetForward.Y), toTarget);
            var optimumRangeDelta = Math.Abs(optimum - distance);
            var forwardness = Clamp01(optimumRangeDelta / settings.AgentMaxForwardDistance) * settings.AgentForwardLerp;
            if (targetForeAlignment > 0)
                forwardness = Lerp(forwardness, 1, targetPortAlignment * targetPortAlignment);
            var lateral = RotateQuarter(toTarget, targetPortAlignment > 0 ? 3 : 1);
            var radial = distance > optimum ? toTarget : new Vector2(-toTarget.X, -toTarget.Y);
            var movementDirection = Normalize(
                Lerp(lateral.X, radial.X, forwardness),
                Lerp(lateral.Y, radial.Y, forwardness),
                lateral.X,
                lateral.Y);
            task.Phase = distance > optimum ? "closing-range" : distance < optimum ? "opening-range" : "orbiting";

            var selected = SelectHighestDpsGroup(weapons, distance);
            if (selected >= 0)
                task.WeaponGroup = selected;
            var aim = selected >= 0
                ? InterceptDirection(agent, target, weapons.First(value => value.GroupIndex == selected))
                : toTarget;
            var commands = new List<AetheriaRuntimeDaemonCommandDocument>
            {
                Movement(task, agent, frameId, movementDirection.X, movementDirection.Y, 1),
                Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.SetLookDirection, "look", command =>
                {
                    command.DirectionX = aim.X;
                    command.PositionZ = aim.Y;
                }),
                Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.SetTarget, "target", command =>
                    command.TargetEntityKey = EntityKey(run, task.ZoneIndex, target.EntityIndex))
            };
            var fireGroups = weapons
                .Where(value => value.Locking && distance > value.MinRange && distance < value.Range)
                .Select(value => value.GroupIndex)
                .Distinct()
                .ToList();
            if (selected >= 0 && HardpointFaces(agent, weapons.First(value => value.GroupIndex == selected), aim))
                fireGroups.Add(selected);
            foreach (var group in fireGroups.Distinct().OrderBy(value => value))
                commands.Add(Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup,
                    $"fire-{group}", command => command.WeaponGroup = group));
            return commands;
        }

        private static IReadOnlyList<AgentWeapon> ResolveAgentWeapons(
            AetheriaRuntimeEntitySnapshotCommit agent,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (catalog == null)
                return Array.Empty<AgentWeapon>();
            var groups = agent.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
            return AetheriaRuntimeEquippedBehaviorQueries.FindOperational(agent, catalog, "Weapon")
                .Where(value =>
                    AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(value.Payload.Kind, AetheriaRuntimeBehaviorKinds.InstantWeapon) ||
                    AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(value.Payload.Kind, "ConstantWeapon"))
                .Select(value =>
                {
                    var group = Enumerable.Range(0, groups.Count)
                        .Where(index => (groups[index] ?? Array.Empty<int>()).Contains(value.EquipmentIndex))
                        .DefaultIfEmpty(-1)
                        .First();
                    return new AgentWeapon(
                        agent,
                        value,
                        group,
                        Math.Max(0, value.EvaluateStat(5)),
                        Math.Max(0, value.EvaluateStat(6)),
                        Math.Max(0, value.EvaluateStat(16)),
                        Math.Max(0.000001, value.EvaluateStat(19)),
                        AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(value.Payload.Kind, "ConstantWeapon"),
                        AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(value.Payload.Kind, "LockWeapon"),
                        AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(value.Payload.Kind, AetheriaRuntimeBehaviorKinds.ChargedWeapon));
                })
                .Where(value => value.GroupIndex >= 0 && value.Range > value.MinRange)
                .ToArray();
        }

        private static double SampleOptimumRange(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            IReadOnlyList<AgentWeapon> weapons,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if (weapons.Count == 0)
                return 0;
            // Preserve the fossil CombatState interval: both ends come from authored maximum ranges.
            var minimum = weapons.Min(value => value.Range);
            var maximum = weapons.Max(value => value.Range);
            var span = Math.Max(0, maximum - minimum);
            var count = Math.Max(1, settings.AgentDpsSampleCount);
            var step = span / count;
            var offset = StableUnit(run.GenerationSeed, task.TaskId, agent.EntityId) * step;
            var optimum = minimum;
            var optimumScore = 0.0;
            for (var index = 0; index < count; index++)
            {
                var range = offset + minimum + span * index / count;
                var score = weapons.Sum(value => RangeDamagePerSecond(value, range));
                score *= Math.Pow(Math.Max(0, range), settings.AgentRangeExponent);
                if (score <= optimumScore)
                    continue;
                optimumScore = score;
                optimum = range;
            }
            return optimum;
        }

        private static int SelectHighestDpsGroup(
            IReadOnlyList<AgentWeapon> weapons,
            double range)
        {
            return weapons
                .Where(value => !value.Locking && WeaponReady(value))
                .Where(value => range > value.MinRange && range < value.Range)
                .GroupBy(value => value.GroupIndex)
                .Select(group => (Group: group.Key, Dps: group.Sum(value => RangeDamagePerSecond(value, range))))
                .Where(value => value.Dps > 0.1)
                .OrderByDescending(value => value.Dps)
                .ThenBy(value => value.Group)
                .Select(value => value.Group)
                .DefaultIfEmpty(-1)
                .First();
        }

        private static bool WeaponReady(AgentWeapon weapon)
        {
            if (weapon.Constant)
                return true;
            var state = FindWeaponState(weapon);
            return state == null || (!state.CoolingDown && !state.Reloading);
        }

        private static AetheriaRuntimeWeaponStateCommit? FindWeaponState(AgentWeapon weapon) =>
            (weapon.Owner.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
            .FirstOrDefault(value => value != null &&
                value.OwnerKind == AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind &&
                value.OwnerIndex == weapon.Behavior.EquipmentIndex &&
                value.BehaviorIndex == weapon.Behavior.BehaviorIndex);

        private static double RangeDamagePerSecond(AgentWeapon weapon, double range)
        {
            if (range <= weapon.MinRange || range >= weapon.Range)
                return 0;
            var normalized = Clamp01((range - weapon.MinRange) / Math.Max(0.000001, weapon.Range - weapon.MinRange));
            var damageField = (weapon.Behavior.Payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(value => value != null && value.Key == 2)?.Value;
            var thermal = weapon.Owner.EquipmentStates?.FirstOrDefault(value =>
                value != null && value.EquipmentIndex == weapon.Behavior.EquipmentIndex)?.ThermalPerformance ?? 1;
            var damage = AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                damageField,
                AetheriaRuntimeDaemonItemStatQueries.ConditionsFor(
                    weapon.Behavior.Item,
                    heat: thermal,
                    charge: WeaponCharge(weapon),
                    range: normalized));
            damage *= SampleDamageCurve(weapon.Behavior.Payload, normalized);
            if (weapon.Constant)
                return damage;
            if (!weapon.Charged)
                return damage / weapon.Cooldown;
            var charge = WeaponCharge(weapon);
            var multiplier = PositiveNumber(weapon.Behavior.Payload, 27, 1);
            var chargeTime = Math.Max(0.000001, weapon.Behavior.EvaluateStat(21));
            return damage * Lerp(1, multiplier, charge) * multiplier / (weapon.Cooldown + chargeTime);
        }

        private static double WeaponCharge(AgentWeapon weapon)
        {
            if (!weapon.Charged)
                return 1;
            return Clamp01(FindWeaponState(weapon)?.Charge ?? 0);
        }

        private static double PositiveNumber(AetheriaRuntimeBehaviorPayload payload, int key, double fallback)
        {
            var value = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value?.NumberValue ?? fallback;
            return value > 0 ? value : fallback;
        }

        private static double SampleDamageCurve(AetheriaRuntimeBehaviorPayload payload, double normalizedRange)
        {
            var value = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == 7)?.Value;
            var keys = value?.Children != null && value.Children.Count > 0 && value.Children[0].Children.Count > 0
                ? value.Children[0].Children
                : value?.Children ?? Array.Empty<AetheriaRuntimeBehaviorValue>();
            var curve = keys
                .Where(key => key?.Children != null && key.Children.Count >= 4)
                .Select(key => new AetheriaRuntimeCurveKey(
                    key.Children[0].NumberValue,
                    key.Children[1].NumberValue,
                    key.Children[2].NumberValue,
                    key.Children[3].NumberValue))
                .ToArray();
            return curve.Length == 0 ? 1 : AetheriaRuntimeDaemonItemStatQueries.SampleCurve(curve, normalizedRange);
        }

        private static Vector2 InterceptDirection(
            AetheriaRuntimeEntitySnapshotCommit agent,
            AetheriaRuntimeEntitySnapshotCommit target,
            AgentWeapon weapon)
        {
            if (weapon.Velocity <= 1)
                return Normalize(target.PositionX - agent.PositionX, target.PositionZ - agent.PositionZ, 0, 1);
            var relative = new CultMath.float3(
                (float)(target.PositionX - agent.PositionX),
                0,
                (float)(target.PositionZ - agent.PositionZ));
            var velocity = new CultMath.float3((float)target.VelocityX, 0, (float)target.VelocityY);
            var time = CultMath.math.first_order_intercept_time((float)weapon.Velocity, relative, velocity);
            return Normalize(relative.x + velocity.x * time, relative.z + velocity.z * time, relative.x, relative.z);
        }

        private static bool HardpointFaces(
            AetheriaRuntimeEntitySnapshotCommit agent,
            AgentWeapon weapon,
            Vector2 aim)
        {
            var forward = Normalize(agent.DirectionX, agent.DirectionY, 0, 1);
            var hardpoint = RotateQuarter(
                forward,
                AetheriaRuntimeEquipmentRotation.QuarterTurns(weapon.Behavior.Slot?.Rotation));
            return Dot(hardpoint, aim) > 0.99;
        }

        private static Vector2 RotateQuarter(Vector2 value, int rotation)
        {
            var rotated = AetheriaRuntimeEquipmentRotation.RotateQuarter(value.X, value.Y, rotation);
            return new Vector2(rotated.X, rotated.Y);
        }

        private static double StableUnit(uint seed, string taskId, string entityId)
        {
            var hash = seed == 0 ? 2166136261u : seed;
            foreach (var character in (taskId ?? "") + "\n" + (entityId ?? ""))
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash / ((double)uint.MaxValue + 1);
        }

        private static double Dot(Vector2 left, Vector2 right) => left.X * right.X + left.Y * right.Y;
        private static double Lerp(double from, double to, double value) => from + (to - from) * Clamp01(value);
        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
        private static Vector2 Normalize(double x, double y, double fallbackX, double fallbackY)
        {
            var length = Math.Sqrt(x * x + y * y);
            if (length <= 0.000001)
            {
                var fallbackLength = Math.Sqrt(fallbackX * fallbackX + fallbackY * fallbackY);
                return fallbackLength <= 0.000001
                    ? new Vector2(0, 1)
                    : new Vector2(fallbackX / fallbackLength, fallbackY / fallbackLength);
            }
            return new Vector2(x / length, y / length);
        }

        private readonly struct Vector2
        {
            public Vector2(double x, double y) { X = x; Y = y; }
            public double X { get; }
            public double Y { get; }
        }

        private sealed class AgentWeapon
        {
            public AgentWeapon(
                AetheriaRuntimeEntitySnapshotCommit owner,
                AetheriaRuntimeEquippedBehavior behavior,
                int groupIndex,
                double minRange,
                double range,
                double velocity,
                double cooldown,
                bool constant,
                bool locking,
                bool charged)
            {
                Owner = owner;
                Behavior = behavior;
                GroupIndex = groupIndex;
                MinRange = minRange;
                Range = range;
                Velocity = velocity;
                Cooldown = cooldown;
                Constant = constant;
                Locking = locking;
                Charged = charged;
            }

            public AetheriaRuntimeEquippedBehavior Behavior { get; }
            public AetheriaRuntimeEntitySnapshotCommit Owner { get; }
            public int GroupIndex { get; }
            public double MinRange { get; }
            public double Range { get; }
            public double Velocity { get; }
            public double Cooldown { get; }
            public bool Constant { get; }
            public bool Locking { get; }
            public bool Charged { get; }
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanIdleReturns(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId)
        {
            var commands = new List<AetheriaRuntimeDaemonCommandDocument>();
            var locations = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null)
                    .Select(entity => (Zone: zone, Entity: entity)))
                .ToArray();
            var byId = locations
                .Where(value => !string.IsNullOrWhiteSpace(value.Entity.EntityId))
                .ToDictionary(value => value.Entity.EntityId, StringComparer.Ordinal);
            foreach (var worker in locations
                .Where(value => value.Entity.IsActive)
                .Where(value => !IsCurrentEntity(run, value.Zone.ZoneIndex, value.Entity.EntityIndex))
                .Where(value => (value.Entity.AgentTaskCapabilities ?? Array.Empty<string>()).Count > 0)
                .Where(value => string.IsNullOrWhiteSpace(value.Entity.AssignedAgentTaskId))
                .Where(value => !string.IsNullOrWhiteSpace(value.Entity.HomeEntityId)))
            {
                if (!byId.TryGetValue(worker.Entity.HomeEntityId, out var home) || !home.Entity.IsActive)
                    continue;
                if (worker.Zone.ZoneIndex != home.Zone.ZoneIndex)
                {
                    var route = FindZoneRoute(run, worker.Zone.ZoneIndex, home.Zone.ZoneIndex);
                    if (route.Count < 2)
                        continue;
                    var settings = AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;
                    var exit = AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
                            run,
                            worker.Zone,
                            AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
                                worker.Zone, AetheriaRuntimeDaemonRenderQueries.DefaultZoneRenderRadius),
                            settings.WormholeDistanceRatio)
                        .First(candidate => candidate.TargetZoneIndex == route[1]);
                    var dx = exit.PositionX - worker.Entity.PositionX;
                    var dz = exit.PositionZ - worker.Entity.PositionZ;
                    if (Math.Sqrt(dx * dx + dz * dz) > AetheriaRuntimeDaemonOperationContext.DefaultWormholeExitRadius * 0.8)
                        commands.Add(IdleMovement(worker.Zone.ZoneIndex, worker.Entity, frameId, dx, dz, "home-approach-wormhole"));
                    else
                        commands.Add(IdleCommand(worker.Zone.ZoneIndex, worker.Entity, frameId,
                            AetheriaRuntimeDaemonCommandKinds.EnterWormhole, "home-travel", command => command.TargetZoneIndex = route[1]));
                    continue;
                }
                if ((home.Entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(worker.Entity.EntityIndex))
                    continue;
                var homeDx = home.Entity.PositionX - worker.Entity.PositionX;
                var homeDz = home.Entity.PositionZ - worker.Entity.PositionZ;
                var dockingContactDistance = InteractionRadius(home.Entity) + InteractionRadius(worker.Entity);
                if (Math.Sqrt(homeDx * homeDx + homeDz * homeDz) > dockingContactDistance + 0.5)
                    commands.Add(IdleMovement(worker.Zone.ZoneIndex, worker.Entity, frameId, homeDx, homeDz, "home-approach"));
                else
                    commands.Add(IdleCommand(worker.Zone.ZoneIndex, worker.Entity, frameId,
                        AetheriaRuntimeDaemonCommandKinds.Dock, "home-dock",
                        command => command.TargetEntityKey = EntityKey(run, home.Zone.ZoneIndex, home.Entity.EntityIndex)));
            }
            return commands;
        }

        private static bool IsCurrentEntity(AetheriaRuntimeRunCheckpointCommit run, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(run.CurrentEntityKey, out var currentZoneIndex, out var currentEntityIndex) &&
            currentZoneIndex == zoneIndex && currentEntityIndex == entityIndex;

        private static AetheriaRuntimeDaemonCommandDocument IdleMovement(
            int zoneIndex,
            AetheriaRuntimeEntitySnapshotCommit entity,
            long frameId,
            double dx,
            double dz,
            string phase)
        {
            var length = Math.Sqrt(dx * dx + dz * dz);
            return IdleCommand(zoneIndex, entity, frameId, AetheriaRuntimeDaemonCommandKinds.SetMoveVector, phase, command =>
            {
                SetLocalHelmAxes(command, entity, dx, dz, length <= 0.0001 ? 0 : 1);
            });
        }

        private static AetheriaRuntimeDaemonCommandDocument IdleCommand(
            int zoneIndex,
            AetheriaRuntimeEntitySnapshotCommit entity,
            long frameId,
            AetheriaRuntimeDaemonCommandKinds kind,
            string phase,
            Action<AetheriaRuntimeDaemonCommandDocument> configure)
        {
            var command = AetheriaRuntimeDaemonCommandDocument.Create(
                kind, RuntimeId, "daemon-agent-scheduler", frameId, $"zone.{zoneIndex}.entity.{entity.EntityIndex}");
            command.CommandId = string.Join(":", RuntimeId, entity.EntityId, frameId.ToString(CultureInfo.InvariantCulture), phase);
            configure(command);
            return command;
        }

        private static (AetheriaRuntimeZoneSnapshotCommit? Zone, AetheriaRuntimeEntitySnapshotCommit? Entity) FindAssignedEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeAgentTaskCommit task)
        {
            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            foreach (var entity in zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                if (entity != null && string.Equals(entity.AssignedAgentTaskId, task.TaskId, StringComparison.Ordinal))
                    return (zone, entity);
            return (null, null);
        }

        private static IReadOnlyList<int> FindZoneRoute(AetheriaRuntimeRunCheckpointCommit run, int source, int target)
        {
            if (source == target)
                return new[] { source };
            var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .ToDictionary(zone => zone.ZoneIndex);
            if (!zones.ContainsKey(source) || !zones.ContainsKey(target))
                return Array.Empty<int>();
            var previous = new Dictionary<int, int> { [source] = source };
            var queue = new Queue<int>();
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var adjacent in zones[current].AdjacentZoneIndices ?? Array.Empty<int>())
                {
                    if (!zones.ContainsKey(adjacent) || previous.ContainsKey(adjacent))
                        continue;
                    previous[adjacent] = current;
                    if (adjacent == target)
                    {
                        var route = new List<int> { target };
                        for (var cursor = target; cursor != source; cursor = previous[cursor])
                            route.Add(previous[cursor]);
                        route.Reverse();
                        return route;
                    }
                    queue.Enqueue(adjacent);
                }
            }
            return Array.Empty<int>();
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanMining(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double simulationTimeSeconds)
        {
            var home = zone.Entities.FirstOrDefault(entity => entity != null && entity.EntityIndex == task.OriginEntityIndex);
            if (string.Equals(task.Phase, "offload", StringComparison.Ordinal) ||
                AetheriaRuntimeCargoCapacityQueries.Available(agent, catalog) < 1)
            {
                if (home == null)
                {
                    Fail(task, agent);
                    return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
                }
                task.Phase = "offload";
                var homeDx = home.PositionX - agent.PositionX;
                var homeDz = home.PositionZ - agent.PositionZ;
                if (Math.Max(0, Math.Sqrt(homeDx * homeDx + homeDz * homeDz) - InteractionRadius(agent) - InteractionRadius(home)) > Math.Max(0.01, task.CompletionRadius))
                    return new[] { Movement(task, agent, frameId, homeDx, homeDz, 1) };

                var commodity = (agent.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                    .SelectMany((bay, bayIndex) => (bay?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                        .Where(slot => slot?.Item != null && !string.IsNullOrWhiteSpace(catalog?.FindItem(slot.Item.ItemKey)?.SimpleCommodityCategory))
                        .Select(slot => (BayIndex: bayIndex, Slot: slot)))
                    .FirstOrDefault();
                if (commodity.Slot == null)
                {
                    task.Phase = "mining";
                    return new[] { Movement(task, agent, frameId, 0, 0, 0) };
                }
                var quantity = Math.Min(commodity.Slot.Item.Quantity,
                    AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(home, catalog, commodity.Slot.Item.ItemKey));
                if (quantity <= 0)
                {
                    Complete(task, agent, frameId);
                    return new[] { Movement(task, agent, frameId, 0, 0, 0) };
                }
                return new[]
                {
                    Movement(task, agent, frameId, 0, 0, 0),
                    Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.TransferCargoItem, "offload", command =>
                    {
                        command.TextValue = commodity.Slot.Item.ItemKey;
                        command.ScalarValue = quantity;
                        command.TargetEntityKey = $"zone.{task.ZoneIndex}.entity.{home.EntityIndex}";
                        command.CargoTransfer = new AetheriaRuntimeCargoTransferCommand
                        {
                            OriginEntityKey = $"zone.{task.ZoneIndex}.entity.{agent.EntityIndex}",
                            OriginCargoIndex = commodity.BayIndex,
                            DestinationEntityKey = $"zone.{task.ZoneIndex}.entity.{home.EntityIndex}",
                            DestinationCargoIndex = 0,
                            SourceX = commodity.Slot.X,
                            SourceY = commodity.Slot.Y,
                            Quantity = quantity
                        };
                    })
                };
            }
            var tool = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(agent, catalog, "MiningTool").FirstOrDefault();
            var bodyKey = (task.TargetBodyKeys ?? Array.Empty<string>()).FirstOrDefault() ?? "";
            var body = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && string.Equals(candidate.BodyKey, bodyKey, StringComparison.Ordinal));
            if (tool == null || body == null)
            {
                Fail(task, agent);
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
            }

            var asteroid = AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(zone, bodyKey, simulationTimeSeconds)
                .Where(pose => pose.AsteroidIndex >= 0 && pose.AsteroidIndex < body.Asteroids.Count && body.Asteroids[pose.AsteroidIndex].RespawnTimer <= 0)
                .OrderBy(pose => Math.Pow(pose.PositionX - agent.PositionX, 2) + Math.Pow(pose.PositionZ - agent.PositionZ, 2))
                .FirstOrDefault();
            if (asteroid.BodyKey == null)
                return new[] { Movement(task, agent, frameId, 0, 0, 0) };

            var dx = asteroid.PositionX - agent.PositionX;
            var dz = asteroid.PositionZ - agent.PositionZ;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            var range = Math.Max(0.01, tool.EvaluateStat(4));
            if (distance > range)
                return new[] { Movement(task, agent, frameId, dx, dz, 1) };

            task.Phase = "mining";
            return new[]
            {
                Movement(task, agent, frameId, 0, 0, 0),
                Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive, "mine", command =>
                {
                    command.EquipmentIndex = tool.EquipmentIndex;
                    command.BehaviorIndex = tool.BehaviorIndex;
                    command.ScalarValue = 1;
                    command.TextValue = bodyKey;
                    command.PositionX = asteroid.AsteroidIndex;
                })
            };
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanSurvey(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double simulationTimeSeconds)
        {
            var scanner = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(agent, catalog, "ResourceScanner").FirstOrDefault();
            if (scanner == null) { Fail(task, agent); return Array.Empty<AetheriaRuntimeDaemonCommandDocument>(); }
            var minimumDensity = scanner.EvaluateStat(2);
            var surveyed = new HashSet<string>((run.CorporationSurveys ?? Array.Empty<AetheriaRuntimeCorporationSurveyCommit>())
                .Where(value => string.Equals(value.CorporationKey, task.CorporationKey, StringComparison.Ordinal) && value.DensityFloor + 0.5 >= minimumDensity)
                .Select(value => value.BodyKey), StringComparer.Ordinal);
            var targets = (task.TargetBodyKeys ?? Array.Empty<string>()).Where(key => !surveyed.Contains(key)).ToArray();
            if (targets.Length == 0) { Complete(task, agent, frameId); return new[] { Movement(task, agent, frameId, 0, 0, 0) }; }

            var candidates = targets.SelectMany(key =>
            {
                var body = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>()).FirstOrDefault(value => value != null && string.Equals(value.BodyKey, key, StringComparison.Ordinal));
                if (body == null) return Array.Empty<(string Key, int Asteroid, double X, double Z)>();
                if (string.Equals(body.Kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase))
                    return AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(zone, key, simulationTimeSeconds)
                        .Select(pose => (Key: key, Asteroid: pose.AsteroidIndex, X: pose.PositionX, Z: pose.PositionZ)).ToArray();
                var pose = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone).FirstOrDefault(value => string.Equals(value.BodyKey, key, StringComparison.Ordinal));
                return string.IsNullOrWhiteSpace(pose.BodyKey)
                    ? Array.Empty<(string Key, int Asteroid, double X, double Z)>()
                    : new[] { (Key: key, Asteroid: -1, X: pose.CenterX, Z: pose.CenterZ) };
            }).OrderBy(value => Math.Pow(value.X - agent.PositionX, 2) + Math.Pow(value.Z - agent.PositionZ, 2)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(candidates.Key)) { Fail(task, agent); return Array.Empty<AetheriaRuntimeDaemonCommandDocument>(); }
            var dx = candidates.X - agent.PositionX; var dz = candidates.Z - agent.PositionZ;
            if (Math.Sqrt(dx * dx + dz * dz) >= Math.Max(0.01, scanner.EvaluateStat(1)))
                return new[] { Movement(task, agent, frameId, dx, dz, 1) };
            task.Phase = "scanning";
            return new[]
            {
                Movement(task, agent, frameId, 0, 0, 0),
                Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive, "scan", command =>
                {
                    command.EquipmentIndex = scanner.EquipmentIndex; command.BehaviorIndex = scanner.BehaviorIndex;
                    command.ScalarValue = 1; command.TextValue = candidates.Key; command.PositionX = candidates.Asteroid;
                })
            };
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
            var distance = Math.Max(0, Math.Sqrt(dx * dx + dz * dz) - InteractionRadius(agent) - InteractionRadius(endpoint));
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

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanTow(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId)
        {
            var station = zone.Entities.FirstOrDefault(value => value != null && value.EntityIndex == task.TargetEntityIndex);
            var parentBody = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .FirstOrDefault(value => value != null && string.Equals(value.BodyKey, task.OrbitParentKey, StringComparison.Ordinal));
            var parent = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
                .FirstOrDefault(value => string.Equals(value.BodyKey, task.OrbitParentKey, StringComparison.Ordinal));
            if (station == null || parentBody == null || string.IsNullOrWhiteSpace(parent.BodyKey) || string.IsNullOrWhiteSpace(parentBody.OrbitKey)) { Fail(task, agent); return Array.Empty<AetheriaRuntimeDaemonCommandDocument>(); }
            var attached = (agent.ChildEntityIndices ?? Array.Empty<int>()).Contains(station.EntityIndex);
            if (!attached)
            {
                var dx = station.PositionX - agent.PositionX; var dz = station.PositionZ - agent.PositionZ;
                if (Math.Sqrt(dx * dx + dz * dz) > Math.Max(0.01, task.CompletionRadius))
                    return new[] { Movement(task, agent, frameId, dx, dz, 1) };
                task.Phase = "pickup";
                return new[] { Movement(task, agent, frameId, 0, 0, 0), Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.TowToStation, "attach", command => { command.TargetEntityKey = EntityKey(run, task.ZoneIndex, station.EntityIndex); command.TextValue = "attach"; }) };
            }
            task.Phase = "delivery";
            var radialX = agent.PositionX - parent.CenterX; var radialZ = agent.PositionZ - parent.CenterZ;
            var length = Math.Sqrt(radialX * radialX + radialZ * radialZ);
            if (length < 0.001) { radialX = 1; radialZ = 0; length = 1; }
            var destinationX = parent.CenterX + radialX / length * task.OrbitDistance;
            var destinationZ = parent.CenterZ + radialZ / length * task.OrbitDistance;
            var dxDelivery = destinationX - agent.PositionX; var dzDelivery = destinationZ - agent.PositionZ;
            if (Math.Sqrt(dxDelivery * dxDelivery + dzDelivery * dzDelivery) > Math.Max(0.01, task.CompletionRadius))
                return new[] { Movement(task, agent, frameId, dxDelivery, dzDelivery, 1) };
            task.Phase = "detach";
            return new[] { Movement(task, agent, frameId, 0, 0, 0), Command(task, agent, frameId, AetheriaRuntimeDaemonCommandKinds.TowToStation, "detach", command => { command.TargetEntityKey = EntityKey(run, task.ZoneIndex, station.EntityIndex); command.TextValue = "detach"; command.SubjectKey = parentBody.OrbitKey; command.ScalarValue = task.OrbitDistance; command.PositionX = destinationX; command.PositionZ = destinationZ; }) };
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> PlanPatrol(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeAgentTaskCommit task,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId)
        {
            var circuit = (task.TargetBodyKeys ?? Array.Empty<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToArray();
            if (circuit.Length == 0)
            {
                Fail(task, agent);
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
            }

            task.CircuitIndex = ((task.CircuitIndex % circuit.Length) + circuit.Length) % circuit.Length;
            var body = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
                .FirstOrDefault(candidate => string.Equals(candidate.BodyKey, circuit[task.CircuitIndex], StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(body.BodyKey))
            {
                Fail(task, agent);
                return Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
            }

            var dx = body.CenterX - agent.PositionX;
            var dz = body.CenterZ - agent.PositionZ;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance <= Math.Max(0.01, task.CompletionRadius))
            {
                task.CircuitIndex = (task.CircuitIndex + 1) % circuit.Length;
                var next = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
                    .First(candidate => string.Equals(candidate.BodyKey, circuit[task.CircuitIndex], StringComparison.Ordinal));
                dx = next.CenterX - agent.PositionX;
                dz = next.CenterZ - agent.PositionZ;
            }
            return new[] { Movement(task, agent, frameId, dx, dz, 1) };
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
                SetLocalHelmAxes(command, agent, dx, dz, magnitude);
            });
        }

        private static AetheriaRuntimeDaemonCommandDocument MovementFromZone(
            AetheriaRuntimeAgentTaskCommit task,
            int zoneIndex,
            AetheriaRuntimeEntitySnapshotCommit agent,
            long frameId,
            double dx,
            double dz)
        {
            var command = AetheriaRuntimeDaemonCommandDocument.Create(
                AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
                RuntimeId,
                "daemon-agent-scheduler",
                frameId,
                $"zone.{zoneIndex}.entity.{agent.EntityIndex}");
            command.CommandId = CommandId(task, frameId, "travel-approach");
            var length = Math.Sqrt(dx * dx + dz * dz);
            SetLocalHelmAxes(command, agent, dx, dz, length <= 0.0001 ? 0 : 1);
            return command;
        }

        private static void SetLocalHelmAxes(
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeEntitySnapshotCommit entity,
            double worldX,
            double worldZ,
            double magnitude)
        {
            var desiredLength = Math.Sqrt(worldX * worldX + worldZ * worldZ);
            if (desiredLength <= 0.0001 || magnitude <= 0)
            {
                command.DirectionX = 0;
                command.DirectionY = 0;
                command.ScalarValue = 0;
                return;
            }

            var forwardLength = Math.Sqrt(
                entity.DirectionX * entity.DirectionX + entity.DirectionY * entity.DirectionY);
            var forwardX = forwardLength <= 0.0001 ? 0 : entity.DirectionX / forwardLength;
            var forwardZ = forwardLength <= 0.0001 ? 1 : entity.DirectionY / forwardLength;
            var desiredX = worldX / desiredLength;
            var desiredZ = worldZ / desiredLength;
            var rightX = forwardZ;
            var rightZ = -forwardX;
            command.DirectionX = desiredX * rightX + desiredZ * rightZ;
            command.DirectionY = desiredX * forwardX + desiredZ * forwardZ;
            command.ScalarValue = Math.Min(1, magnitude);
        }

        private static double InteractionRadius(AetheriaRuntimeEntitySnapshotCommit entity) =>
            string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48 : 20;

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
            var tasks = (run.AgentTasks ?? Array.Empty<AetheriaRuntimeAgentTaskCommit>())
                .Where(task => task != null)
                .ToArray();
            var entities = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .ToArray();
            var activeTaskIds = tasks
                .Where(task => task != null && string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Assigned, StringComparison.Ordinal))
                .Select(task => task.TaskId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                if (!string.IsNullOrWhiteSpace(entity.AssignedAgentTaskId) &&
                    (!entity.IsActive || !activeTaskIds.Contains(entity.AssignedAgentTaskId)))
                    entity.AssignedAgentTaskId = "";
            }
            foreach (var task in tasks.Where(task => string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Assigned, StringComparison.Ordinal)))
            {
                var carriers = entities
                    .Where(entity => entity.IsActive && string.Equals(entity.AssignedAgentTaskId, task.TaskId, StringComparison.Ordinal))
                    .OrderBy(entity => entity.EntityIndex)
                    .ToArray();
                if (carriers.Length == 0)
                {
                    task.Status = AetheriaRuntimeAgentTaskStatuses.Queued;
                    task.AssignedEntityIndex = -1;
                    task.AssignedFrameId = -1;
                    continue;
                }
                task.AssignedEntityIndex = carriers[0].EntityIndex;
                foreach (var duplicate in carriers.Skip(1))
                    duplicate.AssignedAgentTaskId = "";
            }
        }

        private static string EntityKey(AetheriaRuntimeRunCheckpointCommit run, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run.RunId, zoneIndex, entityIndex);
    }
}
