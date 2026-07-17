using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSimulation
    {
        private const string Hull = "hull";
        private const string Shield = "shield";
        private const string Heat = "heat";
        private const string Armor = "armor";
        private const string MaximumArmor = "maximumArmor";

        public static void Step(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonIntentState intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            IAetheriaRuntimeWorldPhysics worldPhysics,
            AetheriaRuntimeCatalogSnapshot? catalog = null,
            long frameId = 0,
            double simulationTimeSeconds = 0,
            int simulationStepIndex = 0,
            AetheriaRuntimeDaemonRenderSettings? renderSettings = null)
        {
            if (run == null || deltaSeconds <= 0 || !AetheriaRuntimeRunLifecycle.IsActive(run))
                return;
            if (worldPhysics == null)
                throw new ArgumentNullException(nameof(worldPhysics));

            StepWormholeTransitions(
                run,
                intents == null
                    ? Array.Empty<AetheriaRuntimeDaemonWormholeIntent>()
                    : intents.Wormholes,
                deltaSeconds,
                settings,
                frameId);

            worldPhysics.RetainWorlds(
                run.RunId,
                (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .Select(zone => zone.ZoneIndex)
                .ToArray());

            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            {
                if (zone == null)
                    continue;

                var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null)
                    .ToArray();

                StepPickupLifetimes(run, zone, frameId, deltaSeconds);

                EnsureStats(entities, settings, catalog);
                AetheriaRuntimeVisibilitySimulation.BeginTick(
                    entities, deltaSeconds, settings.VisibilityDecay);
                foreach (var entity in entities)
                {
                    AetheriaRuntimeThermalSimulation.EnsureTopology(entity, catalog);
                    AetheriaRuntimeThermalSimulation.EnsureState(entity);
                    AetheriaRuntimeThermalSimulation.UpdateEquipmentStates(entity, catalog, deltaSeconds);
                    AetheriaRuntimeEnergySimulation.BeginTick(entity, catalog);
                }
                AetheriaRuntimeBehaviorSimulation.Step(
                    zone.ZoneIndex,
                    entities,
                    intents?.Behaviors,
                    catalog,
                    deltaSeconds);
                AetheriaRuntimeConsumableSimulation.StepZone(
                    run,
                    zone,
                    entities,
                    intents?.Consumables ?? Enumerable.Empty<AetheriaRuntimeDaemonConsumableIntent>(),
                    catalog,
                    frameId,
                    deltaSeconds);
                AetheriaRuntimeOrbitSimulation.StepZone(zone, entities, deltaSeconds);
                AetheriaRuntimeFlightSimulation.Step(
                    entities,
                    intents?.Movements,
                    catalog,
                    deltaSeconds,
                    settings.AetherTorqueMultiplier,
                    settings.AetherHeatMultiplier,
                    settings.TorqueFloor,
                    settings.TorqueMultiplier);
                StepTractorPower(entities, deltaSeconds);
                var worldStep = StepWorldPhysics(
                    run.RunId, frameId, simulationStepIndex, zone, entities, deltaSeconds, worldPhysics);
                ProjectEntityTerrainHeights(zone, entities, catalog, simulationTimeSeconds);
                ResolvePickupContacts(
                    run, zone, entities, worldStep.BeginContacts, worldPhysics, catalog, frameId);
                StepCombat(run, zone, entities, intents, deltaSeconds, settings, worldPhysics, catalog,
                    frameId, simulationTimeSeconds, simulationStepIndex);
                AetheriaRuntimeVisibilitySimulation.StepZone(zone, entities, catalog, renderSettings);
                var activeRenderSettings = renderSettings ?? AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;
                AetheriaRuntimeSensorSimulation.StepZone(
                    run,
                    zone,
                    entities,
                    intents?.SensorPings,
                    catalog,
                    deltaSeconds,
                    settings.TargetInfoDecay,
                    activeRenderSettings.TargetDetectionInfoThreshold,
                    frameId,
                    settings.SecureAreaRadiusMultiplier);
                AetheriaRuntimeMiningSimulation.Step(run, zone, entities, intents, catalog, frameId, simulationTimeSeconds, deltaSeconds);
                AetheriaRuntimeSurveySimulation.Step(run, zone, entities, intents, catalog, frameId, simulationTimeSeconds, deltaSeconds);
                FinalizeBehaviorChainsAndThermal(run, zone, entities, catalog, deltaSeconds, settings, frameId);
            }
        }

        private static void FinalizeBehaviorChainsAndThermal(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            long frameId)
        {
            foreach (var entity in entities)
            {
                AetheriaRuntimeEnergySimulation.StepRadiators(entity, catalog, deltaSeconds);
                AetheriaRuntimeBehaviorSimulation.CompleteDeferredChains(entity, catalog, deltaSeconds);
                AetheriaRuntimeEnergySimulation.SettleReactors(entity, catalog, deltaSeconds);
                AetheriaRuntimeThermalSimulation.Step(entity, deltaSeconds, catalog);
                var medical = AetheriaRuntimeThermalMedicalSimulation.Step(
                    entity, catalog, deltaSeconds, settings);
                PublishThermalMedicalEvents(run, zone, entity, medical, frameId);
                if (medical.Died)
                    CommitDestruction(run, zone, entity, -1,
                        $"thermal:{medical.DeathCause}", "", frameId, settings, medical.DeathCause);
                entity.IsActive = IsAlive(entity);
                if (!entity.IsActive)
                {
                    entity.VelocityX = 0;
                    entity.VelocityY = 0;
                    entity.TargetEntityIndex = -1;
                }
            }
        }

        private static void StepWormholeTransitions(
            AetheriaRuntimeRunCheckpointCommit run,
            IReadOnlyList<AetheriaRuntimeDaemonWormholeIntent> intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            long frameId)
        {
            foreach (var intent in intents ?? Array.Empty<AetheriaRuntimeDaemonWormholeIntent>())
            {
                if (intent == null || !TryResolveEntity(run, intent.ActorEntityKey, out var entity) ||
                    entity.WormholeTransition != null)
                    continue;
                entity.TargetEntityIndex = -1;
                entity.VelocityX = 0;
                entity.VelocityY = 0;
                entity.HelmStrafe = 0;
                entity.HelmForward = 0;
                entity.WormholeTransition = new AetheriaRuntimeWormholeTransitionCommit
                {
                    TransitionId = string.IsNullOrWhiteSpace(intent.CommandId)
                        ? $"wormhole:{entity.EntityId}:{frameId}"
                        : $"wormhole:{intent.CommandId}",
                    Phase = "entering",
                    SourceZoneIndex = intent.SourceZoneIndex,
                    TargetZoneIndex = intent.TargetZoneIndex,
                    EntryStartX = entity.PositionX,
                    EntryStartZ = entity.PositionZ,
                    EntryWormholeX = intent.EntryWormholeX,
                    EntryWormholeZ = intent.EntryWormholeZ,
                    ExitWormholeX = intent.ExitWormholeX,
                    ExitWormholeZ = intent.ExitWormholeZ,
                    StartedFrameId = frameId
                };
                AppendWormholeEvent(run, entity, entity.WormholeTransition, frameId, "wormhole.enter.started");
            }

            var transitioning = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity?.WormholeTransition != null)
                    .Select(entity => (Zone: zone, Entity: entity!)))
                .ToArray();
            foreach (var (zone, entity) in transitioning)
            {
                var transition = entity.WormholeTransition!;
                if (string.Equals(transition.Phase, "completed", StringComparison.Ordinal))
                {
                    entity.WormholeTransition = null;
                    continue;
                }
                transition.Progress = Math.Min(1, transition.Progress + deltaSeconds / settings.WormholeAnimationDuration);
                if (string.Equals(transition.Phase, "entering", StringComparison.Ordinal))
                {
                    var entrySpan = Math.Max(0.000001, 1 - settings.WormholeExitCurveStart);
                    var entry = SmootherStep(Math.Min(1, transition.Progress / entrySpan));
                    entity.PositionX = Lerp(transition.EntryStartX, transition.EntryWormholeX, entry);
                    entity.PositionZ = Lerp(transition.EntryStartZ, transition.EntryWormholeZ, entry);
                    transition.VisualDepthOffset = -settings.WormholeDepth * transition.Progress;
                    if (transition.Progress < 1)
                        continue;

                    var sourceEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(
                        run.RunId, zone.ZoneIndex, entity.EntityIndex);
                    if (!AetheriaRuntimeDaemonOperations.MoveEntityToZone(
                            run,
                            sourceEntityKey,
                            transition.TargetZoneIndex,
                            transition.ExitWormholeX,
                            transition.ExitWormholeZ,
                            out _))
                        throw new InvalidOperationException($"Wormhole transition '{transition.TransitionId}' could not transfer its entity.");
                    var direction = StableDirection(run.GenerationSeed, transition.TransitionId);
                    transition.ExitVelocityX = direction.X * settings.WormholeExitVelocity;
                    transition.ExitVelocityZ = direction.Y * settings.WormholeExitVelocity;
                    transition.Phase = "exiting";
                    transition.Progress = 0;
                    transition.VisualDepthOffset = -settings.WormholeDepth;
                    DiscoverArrival(run, transition.TargetZoneIndex);
                    AppendWormholeEvent(run, entity, transition, frameId, "wormhole.transferred");
                    continue;
                }

                if (!string.Equals(transition.Phase, "exiting", StringComparison.Ordinal))
                    continue;
                var exitSpan = Math.Max(0.000001, 1 - settings.WormholeExitCurveStart);
                var exit = SmootherStep(Math.Max(0, transition.Progress - settings.WormholeExitCurveStart) / exitSpan);
                var velocityLength = Math.Sqrt(
                    transition.ExitVelocityX * transition.ExitVelocityX +
                    transition.ExitVelocityZ * transition.ExitVelocityZ);
                var directionX = velocityLength <= 0.000001 ? 1 : transition.ExitVelocityX / velocityLength;
                var directionZ = velocityLength <= 0.000001 ? 0 : transition.ExitVelocityZ / velocityLength;
                entity.PositionX = transition.ExitWormholeX + directionX * exit * settings.WormholeExitRadius;
                entity.PositionZ = transition.ExitWormholeZ + directionZ * exit * settings.WormholeExitRadius;
                transition.VisualDepthOffset = -settings.WormholeDepth * (1 - transition.Progress);
                if (transition.Progress < 1)
                    continue;
                entity.DirectionX = directionX;
                entity.DirectionY = directionZ;
                entity.LookDirectionX = directionX;
                entity.LookDirectionY = directionZ;
                entity.VelocityX = transition.ExitVelocityX;
                entity.VelocityY = transition.ExitVelocityZ;
                AppendWormholeEvent(run, entity, transition, frameId, "wormhole.exit.completed");
                transition.Phase = "completed";
            }
        }

        private static bool TryResolveEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            out AetheriaRuntimeEntitySnapshotCommit entity)
        {
            entity = null!;
            if (!AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(entityKey, out var zoneIndex, out var entityIndex))
                return false;
            entity = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(zone => zone != null && zone.ZoneIndex == zoneIndex)?
                .Entities?.FirstOrDefault(candidate => candidate != null && candidate.EntityIndex == entityIndex)!;
            return entity != null;
        }

        private static void DiscoverArrival(AetheriaRuntimeRunCheckpointCommit run, int zoneIndex)
        {
            var discovered = new HashSet<int>(run.DiscoveredZoneIndices ?? Array.Empty<int>()) { zoneIndex };
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            foreach (var adjacent in zone?.AdjacentZoneIndices ?? Array.Empty<int>())
                discovered.Add(adjacent);
            run.DiscoveredZoneIndices = discovered.OrderBy(value => value).ToArray();
        }

        private static void AppendWormholeEvent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeWormholeTransitionCommit transition,
            long frameId,
            string kind)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"{transition.TransitionId}:{kind}",
                Kind = kind,
                FrameId = frameId,
                ZoneIndex = string.Equals(kind, "wormhole.enter.started", StringComparison.Ordinal)
                    ? transition.SourceZoneIndex
                    : transition.TargetZoneIndex,
                SourceEntityIndex = entity.EntityIndex,
                SubjectKey = transition.TransitionId,
                ScalarValue = transition.Progress,
                AuxiliaryValue = transition.VisualDepthOffset,
                PositionX = entity.PositionX,
                PositionZ = entity.PositionZ
            });
        }

        private static (double X, double Y) StableDirection(uint seed, string value)
        {
            var hash = seed == 0 ? 2166136261u : seed;
            foreach (var character in value ?? "")
            {
                hash ^= character;
                hash *= 16777619u;
            }
            var angle = hash / ((double)uint.MaxValue + 1) * Math.PI * 2;
            return (Math.Cos(angle), Math.Sin(angle));
        }

        private static double SmootherStep(double value)
        {
            value = Clamp01(value);
            return value * value * value * (value * (value * 6 - 15) + 10);
        }

        private static double Lerp(double from, double to, double value) => from + (to - from) * value;

        private static void StepPickupLifetimes(AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeZoneSnapshotCommit zone, long frameId, double deltaSeconds)
        {
            foreach (var pickup in zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                if (pickup != null) pickup.AgeSeconds += deltaSeconds;
            foreach (var pickup in (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null && pickup.AgeSeconds >= pickup.LifetimeSeconds))
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:pickup:{pickup.PickupIndex}:expired", Kind = "pickup.expired", FrameId = frameId, ZoneIndex = zone.ZoneIndex, PickupIndex = pickup.PickupIndex, ItemKey = pickup.Item?.ItemKey ?? "" });
            zone.DroppedPickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null && pickup.AgeSeconds < pickup.LifetimeSeconds)
                .ToArray();
        }

        private static void StepTractorPower(IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities, double deltaSeconds)
        {
            foreach (var entity in entities)
            {
                var delta = entity.TractorTargetPower - entity.TractorPower;
                entity.TractorPower += Math.Sign(delta) * Math.Min(
                    Math.Abs(delta), deltaSeconds * AetheriaRuntimeTractorMechanics.PowerRampPerSecond);
                entity.TractorPower = Clamp01(entity.TractorPower);
            }
        }

        private static AetheriaRuntimeWorldStep StepWorldPhysics(
            string runId,
            long frameId,
            int simulationStepIndex,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds,
            IAetheriaRuntimeWorldPhysics worldPhysics)
        {
            var physicalEntities = entities
                .Where(entity => entity.WormholeTransition == null)
                .ToArray();
            var result = worldPhysics.Step(runId, frameId, simulationStepIndex, zone, physicalEntities, deltaSeconds);
            var byIndex = physicalEntities.ToDictionary(entity => entity.EntityIndex);
            foreach (var body in result.Bodies)
            {
                if (!byIndex.TryGetValue(body.EntityIndex, out var entity)) continue;
                entity.PositionX = body.PositionX; entity.PositionZ = body.PositionZ;
                entity.VelocityX = body.VelocityX; entity.VelocityY = body.VelocityY;
                entity.DirectionX = body.DirectionX; entity.DirectionY = body.DirectionY;
            }
            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null).ToDictionary(pickup => pickup.PickupIndex);
            foreach (var body in result.Pickups)
            {
                if (!pickups.TryGetValue(body.PickupIndex, out var pickup)) continue;
                pickup.PositionX = body.PositionX; pickup.PositionZ = body.PositionZ;
                pickup.VelocityX = body.VelocityX; pickup.VelocityZ = body.VelocityZ;
            }
            foreach (var parent in entities)
            foreach (var childIndex in parent.ChildEntityIndices ?? Array.Empty<int>())
            {
                var child = entities.FirstOrDefault(value => value.EntityIndex == childIndex);
                if (child == null) continue;
                child.PositionX = parent.PositionX; child.PositionZ = parent.PositionZ;
                child.VelocityX = parent.VelocityX; child.VelocityY = parent.VelocityY;
            }
            return result;
        }

        private static void ProjectEntityTerrainHeights(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double simulationTimeSeconds)
        {
            var byIndex = entities.ToDictionary(entity => entity.EntityIndex);
            var parentByChild = new Dictionary<int, int>();
            foreach (var parent in entities)
            foreach (var childIndex in parent.ChildEntityIndices ?? Array.Empty<int>())
            {
                if (!parentByChild.TryAdd(childIndex, parent.EntityIndex))
                    throw new InvalidOperationException($"Entity {childIndex} has multiple parents in zone {zone.ZoneIndex}.");
            }

            var resolved = new Dictionary<int, double>();
            double Resolve(AetheriaRuntimeEntitySnapshotCommit entity, HashSet<int> path)
            {
                if (resolved.TryGetValue(entity.EntityIndex, out var height))
                    return height;
                if (!path.Add(entity.EntityIndex))
                    throw new InvalidOperationException($"Entity parent cycle contains {entity.EntityIndex} in zone {zone.ZoneIndex}.");

                if (parentByChild.TryGetValue(entity.EntityIndex, out var parentIndex) &&
                    byIndex.TryGetValue(parentIndex, out var parent))
                {
                    height = Resolve(parent, path);
                }
                else
                {
                    height = AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(
                        zone, entity.PositionX, entity.PositionZ, simulationTimeSeconds);
                    height += catalog?.FindItem(entity.HullItemKey ?? "")?.HullGridOffset ?? 0;
                }

                path.Remove(entity.EntityIndex);
                resolved[entity.EntityIndex] = height;
                return height;
            }

            foreach (var entity in entities)
                entity.PositionY = Resolve(entity, new HashSet<int>());
        }

        private static void ResolvePickupContacts(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            IReadOnlyList<AetheriaRuntimeWorldBeginContact> beginContacts,
            IAetheriaRuntimeWorldPhysics worldPhysics,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId)
        {
            var entitiesByIndex = entities
                .Where(value => value != null && value.IsActive)
                .ToDictionary(value => value.EntityIndex);
            foreach (var contact in (beginContacts ?? Array.Empty<AetheriaRuntimeWorldBeginContact>())
                .Where(value => value != null && value.PickupIndex >= 0 &&
                    !string.IsNullOrWhiteSpace(value.FactId)))
            {
                var entityIndex = contact.EntityAIndex >= 0 ? contact.EntityAIndex : contact.EntityBIndex;
                var contactEntityId = contact.EntityAIndex >= 0 ? contact.EntityAId : contact.EntityBId;
                entitiesByIndex.TryGetValue(entityIndex, out var entity);
                if (entity != null && !string.Equals(contactEntityId, entity.EntityId, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Ymir fact '{contact.FactId}' entity identity '{contactEntityId}' does not match " +
                        $"live entity {entityIndex} identity '{entity.EntityId}'.");
                var entityId = entity?.EntityId ?? contactEntityId;
                var priorReceipt = AetheriaRuntimePickupContactReceipts.Find(run, contact.FactId);
                if (priorReceipt != null)
                {
                    if (priorReceipt.ZoneIndex != zone.ZoneIndex ||
                        !string.Equals(priorReceipt.EntityId, entityId, StringComparison.Ordinal) ||
                        priorReceipt.PickupIndex != contact.PickupIndex)
                        throw new InvalidOperationException(
                            $"Ymir fact id '{contact.FactId}' was reused for a different pickup contact.");
                    continue;
                }
                var pickup = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                    .FirstOrDefault(value => value != null && value.PickupIndex == contact.PickupIndex);
                var itemKey = pickup?.Item?.ItemKey ?? "";
                var quantity = Math.Max(1, pickup?.Item?.Quantity ?? 1);
                var cargoQuantityBefore = AetheriaRuntimeCargoCapacityQueries.Quantity(entity);
                var result = entity == null
                    ? AetheriaRuntimePickupContactResult.Ignored
                    : AetheriaRuntimePickupTransactions.ApplyContact(zone, entity, contact, catalog);
                var kind = result == AetheriaRuntimePickupContactResult.Collected
                    ? "pickup.collected"
                    : result == AetheriaRuntimePickupContactResult.RejectedCapacity
                        ? "pickup.rejected"
                        : "pickup.ignored";
                if (result == AetheriaRuntimePickupContactResult.RejectedCapacity && pickup != null)
                {
                    var rejected = worldPhysics.ApplyPickupRejection(run.RunId, zone.ZoneIndex, contact);
                    pickup.PositionX = rejected.PositionX;
                    pickup.PositionZ = rejected.PositionZ;
                    pickup.VelocityX = rejected.VelocityX;
                    pickup.VelocityZ = rejected.VelocityZ;
                }
                AetheriaRuntimePickupContactReceipts.Append(run, new AetheriaRuntimePickupContactReceiptCommit
                {
                    FactId = contact.FactId,
                    FrameId = frameId,
                    ZoneIndex = zone.ZoneIndex,
                    EntityIndex = entityIndex,
                    EntityId = entityId,
                    PickupIndex = contact.PickupIndex,
                    Outcome = kind
                });
                if (result == AetheriaRuntimePickupContactResult.Ignored)
                    continue;
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"ymir-fact:{contact.FactId}:{kind}",
                    Kind = kind,
                    FrameId = frameId,
                    ZoneIndex = zone.ZoneIndex,
                    TargetEntityIndex = entityIndex,
                    PickupIndex = contact.PickupIndex,
                    ItemKey = itemKey,
                    ScalarValue = quantity,
                    AuxiliaryValue = cargoQuantityBefore,
                    Reason = result == AetheriaRuntimePickupContactResult.RejectedCapacity
                        ? "cargo-capacity"
                        : ""
                });
            }
        }

        private static void StepCombat(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonIntentState intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            IAetheriaRuntimeWorldPhysics worldPhysics,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId,
            double simulationTimeSeconds,
            int simulationStepIndex)
        {
            var dockedEntityIndices = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .SelectMany(entity => (entity.ChildEntityIndices ?? Array.Empty<int>())
                    .Concat((entity.DockingBayAssignments ?? Array.Empty<int>()).Where(index => index >= 0)))
                .ToHashSet();
            var combatEntities = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null && !dockedEntityIndices.Contains(entity.EntityIndex))
                .ToArray();
            var byIndex = combatEntities.ToDictionary(entity => entity.EntityIndex);
            foreach (var entity in entities)
                RefreshShieldProjection(entity, catalog);
            foreach (var attacker in combatEntities)
            {
                var requestedWeapons = ResolveWeaponTriggers(zone, attacker, entities, intents, catalog);
                StepDeployableWeapons(run, zone, attacker, requestedWeapons, deltaSeconds, settings, catalog, frameId);
                StepChargedWeapons(run, zone, attacker, byIndex, requestedWeapons, deltaSeconds,
                    settings, catalog, frameId);
                StepConstantWeapons(run, zone, byIndex, attacker, requestedWeapons, deltaSeconds,
                    settings, catalog, frameId);
                var weapons = ResolveWeapons(attacker, catalog, settings);
                foreach (var weapon in weapons)
                {
                    var autoTriggerReady =
                        string.Equals(weapon.State.BehaviorKind, AetheriaRuntimeBehaviorKinds.AutoWeapon,
                            StringComparison.Ordinal) &&
                        requestedWeapons.Contains(weapon.State.OwnerIndex, weapon.State.BehaviorIndex) &&
                        weapon.State.BurstRemaining <= 0 &&
                        !weapon.State.CoolingDown &&
                        !weapon.State.Reloading &&
                        weapon.State.CooldownProgress <= 0;
                    weapon.State.Firing = false;
                    if (requestedWeapons.ContainsPulse(weapon.State.OwnerIndex, weapon.State.BehaviorIndex) ||
                        autoTriggerReady)
                        weapon.State.TriggerPending = true;
                    if (weapon.State.Reloading)
                    {
                        weapon.State.ReloadProgress = Math.Max(0, weapon.State.ReloadProgress - deltaSeconds);
                        if (weapon.State.ReloadProgress <= 0)
                        {
                            weapon.State.Reloading = false;
                            weapon.State.Ammo = weapon.MagazineSize;
                            AppendWeaponEvent(run, zone, attacker, weapon, frameId, "weapon.reload.completed");
                        }
                    }
                    weapon.State.CooldownProgress = Math.Max(0, weapon.State.CooldownProgress - deltaSeconds);
                    weapon.State.CoolingDown = weapon.State.CooldownProgress > 0;
                }

                if (!IsAlive(attacker) ||
                    attacker.TargetEntityIndex < 0 ||
                    !byIndex.TryGetValue(attacker.TargetEntityIndex, out var target) ||
                    !IsAlive(target) ||
                    !Hostile(attacker, target))
                {
                    foreach (var weapon in weapons)
                    {
                        ResetWeaponLock(run, zone, attacker, weapon, frameId, "target-invalid");
                        weapon.State.TriggerPending = false;
                    }
                    continue;
                }

                foreach (var weapon in weapons)
                {
                    UpdateWeaponLock(run, zone, attacker, target, weapon, deltaSeconds, frameId);
                    if (!weapon.State.TriggerPending && weapon.State.BurstRemaining <= 0)
                        continue;
                    if (DistanceSq(attacker, target) > weapon.Range * weapon.Range)
                    {
                        weapon.State.LastRefusalReason = "out-of-range";
                        weapon.State.TriggerPending = false;
                        continue;
                    }
                    if (weapon.State.LockProgress <= 0.99 ||
                        weapon.State.Reloading)
                        continue;

                    if (weapon.State.BurstRemaining <= 0)
                    {
                        if (weapon.State.CooldownProgress > 0)
                            continue;
                        if (weapon.SingleAmmoBurst)
                        {
                            var triggerResult = CommitWeaponRound(attacker, weapon, catalog);
                            if (triggerResult == WeaponRoundResult.ReloadStarted)
                                AppendWeaponEvent(run, zone, attacker, weapon, frameId, "weapon.reload.started");
                            PublishWeaponRoundResult(run, zone, attacker, weapon, frameId, triggerResult);
                            if (triggerResult != WeaponRoundResult.Fired)
                            {
                                weapon.State.TriggerPending = false;
                                continue;
                            }
                        }
                        weapon.State.BurstRemaining = weapon.BurstCount;
                        weapon.State.BurstInterval = weapon.BurstTime / weapon.BurstCount;
                        weapon.State.BurstTimer = 0;
                        weapon.State.CoolingDown = true;
                        weapon.State.CooldownProgress = weapon.Cooldown;
                    }

                    weapon.State.BurstTimer += deltaSeconds;
                    while (weapon.State.BurstRemaining > 0 && weapon.State.BurstTimer > 0)
                    {
                        if (!weapon.SingleAmmoBurst)
                        {
                            var roundResult = CommitWeaponRound(attacker, weapon, catalog);
                            if (roundResult == WeaponRoundResult.ReloadStarted)
                                AppendWeaponEvent(run, zone, attacker, weapon, frameId, "weapon.reload.started");
                            PublishWeaponRoundResult(run, zone, attacker, weapon, frameId, roundResult);
                            if (roundResult != WeaponRoundResult.Fired)
                            {
                                weapon.State.BurstRemaining = 0;
                                break;
                            }
                        }
                        weapon.State.BurstRemaining--;
                        weapon.State.BurstTimer -= weapon.State.BurstInterval;
                        var shotId = NextShotId(zone, attacker, weapon);
                        CommitShotResolution(run, zone, attacker, target, weapon, shotId, frameId, catalog, settings);
                        AppendShotCommittedEvent(run, zone, attacker, target, weapon, shotId, frameId);
                        weapon.State.Firing = true;
                        weapon.State.TriggerPending = false;
                        AetheriaRuntimeThermalSimulation.AddHeatToEquipment(attacker, catalog,
                            weapon.State.OwnerIndex, weapon.Heat);
                        ApplyWeaponWear(attacker, weapon.State, 1);
                    }
                }
            }

            PreparePhysicalPayloads(zone, byIndex, deltaSeconds);
            var projectileStep = worldPhysics.StepPhysicalPayloads(
                run.RunId,
                frameId,
                simulationStepIndex,
                zone,
                entities,
                deltaSeconds);
            zone.PhysicalPayloads = projectileStep.PhysicalPayloads;
            TriggerExpiredDeployables(run, zone, byIndex, frameId, simulationTimeSeconds);
            foreach (var hit in projectileStep.Hits)
            {
                if (byIndex.TryGetValue(hit.TargetEntityIndex, out var target))
                {
                    AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"physical-payload:{hit.Payload.PayloadId}:contact", Kind = "physical-payload.contact", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = hit.Payload.SourceEntityIndex, TargetEntityIndex = target.EntityIndex, SubjectKey = hit.Payload.PayloadId, ItemKey = hit.Payload.PayloadKind, ScalarValue = hit.Payload.ContactMagnitude, PositionX = hit.PointX, PositionZ = hit.PointZ });
                    if (string.Equals(hit.Payload.PayloadKind, "mine", StringComparison.Ordinal) &&
                        hit.Payload.AgeSeconds >= hit.Payload.ActivationDelaySeconds &&
                        hit.Payload.TriggeredAtSeconds < 0)
                    {
                        hit.Payload.TriggeredAtSeconds = simulationTimeSeconds;
                        hit.Payload.TriggerReason = "proximity";
                        AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"physical-payload:{hit.Payload.PayloadId}:triggered", Kind = "deployable.triggered", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = hit.Payload.SourceEntityIndex, TargetEntityIndex = target.EntityIndex, SubjectKey = hit.Payload.PayloadId, ItemKey = hit.Payload.WeaponItemKey, PositionX = hit.Payload.PositionX, PositionZ = hit.Payload.PositionZ });
                    }
                }
            }
            ResolveDeployableDetonations(run, zone, combatEntities, frameId, simulationTimeSeconds, catalog, settings);

        }

        private static void StepDeployableWeapons(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            WeaponTriggerSet requestedWeapons,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId)
        {
            foreach (var deployable in ResolveDeployableWeapons(attacker, requestedWeapons, catalog, settings))
            {
                var weapon = deployable.Weapon;
                weapon.State.Firing = false;
                weapon.State.CooldownProgress = Math.Max(0, weapon.State.CooldownProgress - deltaSeconds);
                weapon.State.CoolingDown = weapon.State.CooldownProgress > 0;
                if (!IsAlive(attacker) || weapon.State.CooldownProgress > 0) continue;

                var result = CommitWeaponRound(attacker, weapon, catalog);
                PublishWeaponRoundResult(run, zone, attacker, weapon, frameId, result);
                if (result != WeaponRoundResult.Fired) continue;

                var direction = Normalize(attacker.DirectionX, attacker.DirectionY);
                if (Math.Abs(direction.X) + Math.Abs(direction.Y) < 0.0001) direction = (0, 1);
                var payloadId = NextShotId(zone, attacker, weapon);
                var payloads = (zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>()).ToList();
                payloads.Add(new AetheriaRuntimePhysicalPayloadCommit
                {
                    PayloadId = payloadId,
                    SourceEntityIndex = attacker.EntityIndex,
                    FactionKey = attacker.FactionKey ?? "",
                    PositionX = attacker.PositionX + direction.X * 2,
                    PositionY = attacker.PositionY,
                    PositionZ = attacker.PositionZ + direction.Y * 2,
                    DirectionX = direction.X,
                    DirectionY = direction.Y,
                    VelocityX = direction.X * weapon.ProjectileSpeed,
                    VelocityY = direction.Y * weapon.ProjectileSpeed,
                    Radius = 1,
                    LifetimeSeconds = deployable.LifetimeSeconds,
                    Active = true,
                    PayloadKind = "mine",
                    ActivationDelaySeconds = deployable.ActivationDelaySeconds,
                    TriggerRadius = deployable.TriggerRadius,
                    DetonationDelaySeconds = deployable.DetonationDelaySeconds,
                    BlastRadius = deployable.BlastRadius,
                    PayloadMagnitude = weapon.Damage,
                    MaximumSourceDistance = weapon.Range,
                    WeaponItemKey = weapon.ItemKey,
                    DamageType = weapon.DamageType,
                    Penetration = weapon.Penetration,
                    DamageSpread = weapon.DamageSpread
                });
                zone.PhysicalPayloads = payloads;
                weapon.State.Firing = true;
                weapon.State.CoolingDown = true;
                weapon.State.CooldownProgress = weapon.Cooldown;
                PublishWeaponVisibility(attacker, weapon);
                AetheriaRuntimeThermalSimulation.AddHeatToEquipment(attacker, catalog,
                    weapon.State.OwnerIndex, weapon.Heat);
                ApplyWeaponWear(attacker, weapon.State, 1);
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"physical-payload:{payloadId}:deployed", Kind = "deployable.deployed", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = attacker.EntityIndex, SubjectKey = payloadId, ItemKey = weapon.ItemKey, PositionX = attacker.PositionX, PositionZ = attacker.PositionZ });
            }
        }

        private static void PublishThermalMedicalEvents(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeThermalMedicalResult result,
            long frameId)
        {
            void Append(string kind, double exposure)
            {
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:{kind}",
                    Kind = kind, FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                    TargetEntityIndex = entity.EntityIndex, SubjectKey = entity.EntityId,
                    ScalarValue = exposure, AuxiliaryValue = result.CockpitTemperature,
                    PositionX = entity.PositionX, PositionZ = entity.PositionZ
                });
            }

            if (result.HeatstrokeRiskCrossed) Append("pilot.heatstroke.risk", entity.Heatstroke);
            if (result.HypothermiaRiskCrossed) Append("pilot.hypothermia.risk", entity.Hypothermia);
        }

        private static void ResolveDeployableDetonations(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            long frameId,
            double simulationTimeSeconds,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var survivors = new List<AetheriaRuntimePhysicalPayloadCommit>();
            foreach (var payload in zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>())
            {
                if (!string.Equals(payload.PayloadKind, "mine", StringComparison.Ordinal) ||
                    payload.TriggeredAtSeconds < 0 ||
                    simulationTimeSeconds < payload.TriggeredAtSeconds + payload.DetonationDelaySeconds)
                {
                    survivors.Add(payload);
                    continue;
                }

                foreach (var target in entities.Where(value => value.IsActive && value.EntityIndex != payload.SourceEntityIndex))
                {
                    var dx = target.PositionX - payload.PositionX;
                    var dz = target.PositionZ - payload.PositionZ;
                    if (dx * dx + dz * dz > payload.BlastRadius * payload.BlastRadius) continue;
                    var source = entities.FirstOrDefault(value => value.EntityIndex == payload.SourceEntityIndex);
                    var aliveBefore = IsAlive(target);
                    var damage = ResolveDamage(target, payload.PayloadMagnitude, payload.DamageType,
                        payload.Penetration, payload.DamageSpread, source,
                        payload.PositionX, payload.PositionZ, true, catalog);
                    if (damage.ShieldAbsorbedDamage > 0)
                        AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                        {
                            EventId = $"physical-payload:{payload.PayloadId}:shield:{target.EntityIndex}",
                            Kind = "shield.absorbed", FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                            SourceEntityIndex = payload.SourceEntityIndex, TargetEntityIndex = target.EntityIndex,
                            SubjectKey = payload.PayloadId, ItemKey = payload.WeaponItemKey,
                            ScalarValue = damage.ShieldAbsorbedDamage,
                            AuxiliaryValue = damage.ShieldHeatGenerated,
                            PositionX = payload.PositionX, PositionZ = payload.PositionZ
                        });
                    if (damage.TotalAppliedDamage > 0)
                        AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                        {
                            EventId = $"physical-payload:{payload.PayloadId}:damage:{target.EntityIndex}",
                            Kind = "entity.damaged", FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                            SourceEntityIndex = payload.SourceEntityIndex, TargetEntityIndex = target.EntityIndex,
                            SubjectKey = payload.PayloadId, ItemKey = payload.WeaponItemKey,
                            ScalarValue = damage.TotalAppliedDamage,
                            AuxiliaryValue = damage.HullAppliedDamage,
                            Reason = payload.DamageType,
                            PositionX = payload.PositionX, PositionZ = payload.PositionZ
                        });
                    PublishEquipmentDestroyedEvents(run, zone, target, damage,
                        payload.SourceEntityIndex, payload.PayloadId, frameId);
                    if (aliveBefore && (damage.CockpitDestroyed || !IsAlive(target)))
                        CommitDestruction(run, zone, target, payload.SourceEntityIndex,
                            payload.PayloadId, payload.WeaponItemKey, frameId, settings,
                            damage.CockpitDestroyed ? "cockpit-destroyed" : "hull-destroyed");
                }
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"physical-payload:{payload.PayloadId}:detonated", Kind = "deployable.detonated", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = payload.SourceEntityIndex, SubjectKey = payload.PayloadId, ItemKey = payload.WeaponItemKey, Reason = payload.TriggerReason, ScalarValue = payload.PayloadMagnitude, PositionX = payload.PositionX, PositionZ = payload.PositionZ });
            }
            zone.PhysicalPayloads = survivors;
        }

        private static void StepChargedWeapons(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            IReadOnlyDictionary<int, AetheriaRuntimeEntitySnapshotCommit> byIndex,
            WeaponTriggerSet requestedWeapons,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId)
        {
            foreach (var weapon in ResolveChargedWeapons(attacker, requestedWeapons, catalog, settings))
            {
                var state = weapon.Base.State;
                state.Firing = false;
                state.CooldownProgress = Math.Max(0, state.CooldownProgress - deltaSeconds);
                state.CoolingDown = state.CooldownProgress > 0;
                if (state.Reloading)
                {
                    state.ReloadProgress = Math.Max(0, state.ReloadProgress - deltaSeconds);
                    if (state.ReloadProgress <= 0)
                    {
                        state.Reloading = false;
                        state.Ammo = weapon.Base.MagazineSize;
                        AppendChargedEvent(run, zone, attacker, weapon, frameId, "weapon.reload.completed", "", state.Ammo);
                    }
                }

                if (weapon.Requested && IsAlive(attacker) && !state.Charging && !state.CoolingDown &&
                    !state.Reloading && state.BurstRemaining <= 0)
                {
                    state.Charging = true;
                    state.Charged = false;
                    state.Charge = 0;
                    state.ChargeHoldSeconds = 0;
                    state.ChargeRiskChecks = 0;
                    state.ChargeMalfunctionRisk = 0;
                    state.LockTargetEntityIndex = -1;
                    state.LastRefusalReason = "";
                    AppendChargedEvent(run, zone, attacker, weapon, frameId, "weapon.charge.started", "committed", 0);
                }

                byIndex.TryGetValue(attacker.TargetEntityIndex, out var solutionTarget);
                var hasSolution = solutionTarget != null && IsAlive(solutionTarget) && Hostile(attacker, solutionTarget) &&
                    DistanceSq(attacker, solutionTarget) <= weapon.Base.Range * weapon.Base.Range;
                if (state.Charging)
                {
                    var step = deltaSeconds / weapon.ChargeTime;
                    if (!state.Charged)
                    {
                        var energy = weapon.ChargeEnergy * step;
                        if (!CanSupplyEnergy(attacker, catalog, energy))
                        {
                            state.Charging = false;
                            state.Charge = 0;
                            PublishChargedRefusal(run, zone, attacker, weapon, frameId, "insufficient-charge-energy");
                            continue;
                        }
                        CommitEnergy(attacker, catalog, energy);
                        AetheriaRuntimeThermalSimulation.AddHeatToEquipment(attacker, catalog,
                            weapon.Base.State.OwnerIndex, weapon.ChargeHeat * step);
                    }
                    state.Charge += step;
                    if (!state.Charged && state.Charge >= 1)
                    {
                        state.Charged = true;
                        state.ChargeHoldSeconds = 0;
                        AppendChargedEvent(run, zone, attacker, weapon, frameId, "weapon.charge.ready", "holding", state.Charge);
                    }
                    if (state.Charged && hasSolution)
                    {
                        state.LockTargetEntityIndex = solutionTarget!.EntityIndex;
                        state.Charging = false;
                        state.ChargeHoldSeconds = 0;
                        state.ChargeRiskChecks = 0;
                        state.ChargeMalfunctionRisk = 0;
                        TriggerChargedShot(run, zone, attacker, weapon, frameId, catalog);
                    }
                    else if (state.Charged)
                    {
                        state.ChargeHoldSeconds += deltaSeconds;
                        var grace = Math.Max(0, weapon.FailureCharge - 1) * weapon.ChargeTime;
                        var overdue = Math.Max(0, state.ChargeHoldSeconds - grace);
                        var dueChecks = (int)Math.Floor(overdue);
                        while (state.ChargeRiskChecks < dueChecks && state.Charging)
                        {
                            state.ChargeRiskChecks++;
                            state.ChargeMalfunctionRisk = Clamp01(state.ChargeRiskChecks / weapon.ChargeTime);
                            if (ChargedMalfunctionRoll(run.GenerationSeed, attacker.EntityIndex,
                                    state.OwnerIndex, state.BehaviorIndex, state.ChargeRiskChecks) < state.ChargeMalfunctionRisk)
                            {
                                state.Charging = false;
                                state.Charged = false;
                                state.Charge = 0;
                                state.CoolingDown = true;
                                state.CooldownProgress = weapon.Base.Cooldown;
                                weapon.Item.Durability = Math.Max(0, weapon.Item.Durability - weapon.FailureDamage);
                                AppendChargedEvent(run, zone, attacker, weapon, frameId,
                                    "weapon.charge.malfunctioned", "hold-risk", state.ChargeMalfunctionRisk);
                            }
                        }
                    }
                }

                if (state.BurstRemaining > 0)
                    byIndex.TryGetValue(state.LockTargetEntityIndex, out solutionTarget);
                if (state.BurstRemaining <= 0 || solutionTarget == null || !IsAlive(solutionTarget))
                    continue;
                var shot = weapon.CommittedShot();
                state.BurstTimer += deltaSeconds;
                while (state.BurstRemaining > 0 && state.BurstTimer > 0)
                {
                    if (!shot.SingleAmmoBurst)
                    {
                        var result = CommitWeaponRound(attacker, shot, catalog);
                        PublishWeaponRoundResult(run, zone, attacker, shot, frameId, result);
                        if (result == WeaponRoundResult.ReloadStarted)
                            AppendWeaponEvent(run, zone, attacker, shot, frameId, "weapon.reload.started");
                        if (result != WeaponRoundResult.Fired) { state.BurstRemaining = 0; break; }
                    }
                    state.BurstRemaining--;
                    state.BurstTimer -= state.BurstInterval;
                    var shotId = NextShotId(zone, attacker, shot);
                    CommitShotResolution(run, zone, attacker, solutionTarget, shot, shotId, frameId, catalog, settings);
                    AppendShotCommittedEvent(run, zone, attacker, solutionTarget, shot, shotId, frameId);
                    state.Firing = true;
                    AetheriaRuntimeThermalSimulation.AddHeatToEquipment(attacker, catalog,
                        shot.State.OwnerIndex, shot.Heat);
                    ApplyWeaponWear(attacker, shot.State, 1);
                }
                if (state.BurstRemaining <= 0)
                {
                    state.Charged = false; state.Charge = 0; state.ChargeHoldSeconds = 0;
                    state.ChargeRiskChecks = 0; state.ChargeMalfunctionRisk = 0;
                }
            }
        }

        private static void TriggerChargedShot(AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone, AetheriaRuntimeEntitySnapshotCommit attacker,
            ResolvedChargedWeapon weapon, long frameId, AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var shot = weapon.CommittedShot();
            if (shot.SingleAmmoBurst)
            {
                var result = CommitWeaponRound(attacker, shot, catalog);
                PublishWeaponRoundResult(run, zone, attacker, shot, frameId, result);
                if (result == WeaponRoundResult.ReloadStarted)
                    AppendWeaponEvent(run, zone, attacker, shot, frameId, "weapon.reload.started");
                if (result != WeaponRoundResult.Fired) { weapon.Base.State.Charged = false; weapon.Base.State.Charge = 0; return; }
            }
            weapon.Base.State.BurstRemaining = shot.BurstCount;
            weapon.Base.State.BurstInterval = shot.BurstTime / shot.BurstCount;
            weapon.Base.State.BurstTimer = 0;
            weapon.Base.State.CoolingDown = true;
            weapon.Base.State.CooldownProgress = shot.Cooldown;
            AppendChargedEvent(run, zone, attacker, weapon, frameId, "weapon.charge.committed", "solution-acquired", 1);
        }

        private static void PublishChargedRefusal(AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone, AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedChargedWeapon weapon, long frameId, string reason)
        {
            if (string.Equals(weapon.Base.State.LastRefusalReason, reason, StringComparison.Ordinal)) return;
            weapon.Base.State.LastRefusalReason = reason;
            AppendChargedEvent(run, zone, entity, weapon, frameId, "weapon.fire.refused", reason, weapon.Base.State.Ammo);
        }

        private static void AppendChargedEvent(AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone, AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedChargedWeapon weapon, long frameId, string kind, string subject, double scalar)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:weapon:{weapon.Base.State.OwnerIndex}:{weapon.Base.State.BehaviorIndex}:{kind}",
                Kind = kind, FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = entity.EntityIndex, TargetEntityIndex = entity.TargetEntityIndex,
                SubjectKey = subject, ItemKey = weapon.Base.ItemKey, ScalarValue = scalar
            });
        }

        private static double ChargedMalfunctionRoll(uint seed, int entityIndex,
            int ownerIndex, int behaviorIndex, int check)
        {
            unchecked
            {
                uint hash = seed ^ (uint)entityIndex * 2246822519u ^ (uint)ownerIndex * 3266489917u ^
                            (uint)behaviorIndex * 668265263u ^ (uint)check * 374761393u;
                hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
                return (hash + 1.0) / (uint.MaxValue + 2.0);
            }
        }

        private static void StepConstantWeapons(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyDictionary<int, AetheriaRuntimeEntitySnapshotCommit> byIndex,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            WeaponTriggerSet requestedWeapons,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId)
        {
            foreach (var weapon in ResolveConstantWeapons(attacker, requestedWeapons, catalog, settings))
            {
                if (weapon.State.Reloading)
                {
                    weapon.State.ReloadProgress = Math.Max(0, weapon.State.ReloadProgress - deltaSeconds);
                    if (weapon.State.ReloadProgress <= 0)
                    {
                        weapon.State.Reloading = false;
                        weapon.State.Ammo = weapon.MagazineSize;
                        AppendConstantWeaponEvent(run, zone, attacker, weapon, frameId, "weapon.reload.completed", "");
                    }
                }

                byIndex.TryGetValue(attacker.TargetEntityIndex, out var selectedTarget);
                var validTarget = IsAlive(attacker) &&
                    attacker.TargetEntityIndex >= 0 && selectedTarget != null &&
                    IsAlive(selectedTarget) && Hostile(attacker, selectedTarget) &&
                    DistanceSq(attacker, selectedTarget) <= weapon.Range * weapon.Range;
                var shouldFire = weapon.Selected && validTarget && !weapon.State.Reloading;
                if (!shouldFire)
                {
                    StopConstantWeapon(run, zone, attacker, weapon, frameId,
                        weapon.State.Reloading ? "reload" : "inactive");
                    continue;
                }

                if (!weapon.State.Firing)
                {
                    weapon.State.Firing = true;
                    weapon.State.LastRefusalReason = "";
                    AppendConstantWeaponEvent(run, zone, attacker, weapon, frameId, "weapon.firing.started", "");
                }

                var energy = weapon.Energy * deltaSeconds;
                if (!CanSupplyEnergy(attacker, catalog, energy))
                {
                    PublishConstantWeaponRefusal(run, zone, attacker, weapon, frameId, "insufficient-energy");
                    StopConstantWeapon(run, zone, attacker, weapon, frameId, "insufficient-energy");
                    continue;
                }
                CommitEnergy(attacker, catalog, energy);

                if (!string.IsNullOrWhiteSpace(weapon.AmmoItemKey))
                {
                    weapon.State.AmmoIntervalProgress -= deltaSeconds / weapon.AmmoIntervalDuration;
                    if (weapon.State.AmmoIntervalProgress < 0)
                    {
                        weapon.State.AmmoIntervalProgress = 1;
                        if (weapon.MagazineSize > 1 && weapon.State.Ammo > 0)
                        {
                            weapon.State.Ammo--;
                        }
                        else if (!AetheriaRuntimeCargoTransactions.TryFind(attacker, weapon.AmmoItemKey,
                                     out var cargoIndex, out var x, out var y) ||
                                 !AetheriaRuntimeCargoTransactions.TryRemoveQuantity(
                                     attacker, cargoIndex, weapon.AmmoItemKey, x, y, 1, out _))
                        {
                            PublishConstantWeaponRefusal(run, zone, attacker, weapon, frameId, "no-ammunition");
                            continue;
                        }
                        else if (weapon.MagazineSize > 1)
                        {
                            weapon.State.Reloading = true;
                            weapon.State.ReloadProgress = weapon.ReloadTime;
                            AppendConstantWeaponEvent(run, zone, attacker, weapon, frameId, "weapon.reload.started", "");
                            StopConstantWeapon(run, zone, attacker, weapon, frameId, "reload");
                            continue;
                        }
                    }
                }

                weapon.State.LastRefusalReason = "";
                AetheriaRuntimeThermalSimulation.AddHeatToEquipment(attacker, catalog,
                    weapon.State.OwnerIndex, weapon.Heat * deltaSeconds);
                ApplyWeaponWear(attacker, weapon.State, deltaSeconds);
                var shot = weapon.ResolutionShot(deltaSeconds);
                var shotId = NextShotId(zone, attacker, shot);
                CommitShotResolution(run, zone, attacker, selectedTarget!, shot, shotId, frameId, catalog, settings);
                AppendShotCommittedEvent(run, zone, attacker, selectedTarget!, shot, shotId, frameId);
            }
        }

        private static void StopConstantWeapon(
            AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity, ResolvedConstantWeapon weapon,
            long frameId, string reason)
        {
            if (!weapon.State.Firing) return;
            weapon.State.Firing = false;
            AppendConstantWeaponEvent(run, zone, entity, weapon, frameId, "weapon.firing.stopped", reason);
        }

        private static void PublishConstantWeaponRefusal(
            AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity, ResolvedConstantWeapon weapon,
            long frameId, string reason)
        {
            if (string.Equals(weapon.State.LastRefusalReason, reason, StringComparison.Ordinal)) return;
            weapon.State.LastRefusalReason = reason;
            AppendConstantWeaponEvent(run, zone, entity, weapon, frameId, "weapon.fire.refused", reason);
        }

        private static void AppendConstantWeaponEvent(
            AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity, ResolvedConstantWeapon weapon,
            long frameId, string kind, string subject)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:weapon:{weapon.State.OwnerIndex}:{weapon.State.BehaviorIndex}:{kind}",
                Kind = kind, FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = entity.EntityIndex, TargetEntityIndex = entity.TargetEntityIndex,
                SubjectKey = subject, ItemKey = weapon.ItemKey, ScalarValue = weapon.State.Ammo
            });
        }

        private static void AppendWeaponEvent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedWeapon weapon,
            long frameId,
            string kind)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:weapon:{weapon.State.OwnerIndex}:{weapon.State.BehaviorIndex}:{kind}",
                Kind = kind,
                FrameId = frameId,
                ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = entity.EntityIndex,
                SubjectKey = weapon.ItemKey,
                ItemKey = weapon.ItemKey,
                ScalarValue = weapon.State.Ammo
            });
        }

        private static void PublishWeaponRoundResult(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedWeapon weapon,
            long frameId,
            WeaponRoundResult result)
        {
            var reason = result == WeaponRoundResult.InsufficientEnergy ? "insufficient-energy" :
                result == WeaponRoundResult.NoAmmo ? "no-ammunition" : "";
            if (string.IsNullOrEmpty(reason))
            {
                weapon.State.LastRefusalReason = "";
                return;
            }
            if (string.Equals(weapon.State.LastRefusalReason, reason, StringComparison.Ordinal))
                return;

            weapon.State.LastRefusalReason = reason;
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:weapon:{weapon.State.OwnerIndex}:{weapon.State.BehaviorIndex}:refused:{reason}",
                Kind = "weapon.fire.refused",
                FrameId = frameId,
                ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = entity.EntityIndex,
                TargetEntityIndex = entity.TargetEntityIndex,
                SubjectKey = reason,
                ItemKey = weapon.ItemKey,
                ScalarValue = weapon.State.Ammo
            });
        }

        private static void UpdateWeaponLock(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target,
            ResolvedWeapon weapon,
            double deltaSeconds,
            long frameId)
        {
            var weaponState = weapon.State;
            if (weaponState.LockTargetEntityIndex != target.EntityIndex)
            {
                ResetWeaponLock(run, zone, attacker, weapon, frameId, "target-changed");
                weaponState.LockTargetEntityIndex = target.EntityIndex;
            }

            var previousProgress = weaponState.LockProgress;
            var targetDirection = Normalize(target.PositionX - attacker.PositionX, target.PositionZ - attacker.PositionZ);
            var lookDirection = Normalize(attacker.DirectionX, attacker.DirectionY);
            var dot = Math.Max(-1.0, Math.Min(1.0,
                targetDirection.X * lookDirection.X + targetDirection.Y * lookDirection.Y));
            var angleDegrees = Math.Acos(dot) * 180.0 / Math.PI;
            if (angleDegrees >= weapon.LockAngleDegrees)
            {
                weaponState.LockProgress = Clamp01(
                    weaponState.LockProgress - deltaSeconds * weapon.LockDecayPerSecond);
                PublishWeaponLockTransitions(run, zone, attacker, target.EntityIndex, weapon,
                    previousProgress, weaponState.LockProgress, frameId, "angle");
                return;
            }

            var contact = (attacker.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.TargetEntityIndex == target.EntityIndex);
            var information = Clamp01(contact?.InfoGathered ?? 0);
            var directionalQuality = Math.Max(0, 1.0 - angleDegrees / 90.0);
            var acquisition =
                Math.Pow(directionalQuality, weapon.LockDirectionImpact) *
                deltaSeconds *
                weapon.LockSpeed *
                Math.Pow(information, weapon.LockSensorImpact);
            weaponState.LockProgress = Clamp01(weaponState.LockProgress + acquisition);
            PublishWeaponLockTransitions(run, zone, attacker, target.EntityIndex, weapon,
                previousProgress, weaponState.LockProgress, frameId, "acquiring");
        }

        private static void ResetWeaponLock(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            ResolvedWeapon weapon,
            long frameId,
            string reason)
        {
            var state = weapon.State;
            var previousProgress = state.LockProgress;
            var previousTarget = state.LockTargetEntityIndex;
            state.LockTargetEntityIndex = -1;
            state.LockProgress = 0;
            if (previousProgress > 0.99 && previousTarget >= 0)
                AppendWeaponLockEvent(run, zone, attacker, previousTarget, weapon, frameId,
                    "weapon.lock.lost", reason, previousProgress, 0);
        }

        private static void PublishWeaponLockTransitions(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            int targetEntityIndex,
            ResolvedWeapon weapon,
            double previousProgress,
            double currentProgress,
            long frameId,
            string reason)
        {
            if (previousProgress <= 0 && currentProgress > 0)
                AppendWeaponLockEvent(run, zone, attacker, targetEntityIndex, weapon, frameId,
                    "weapon.lock.started", reason, previousProgress, currentProgress);
            if (previousProgress <= 0.99 && currentProgress > 0.99)
                AppendWeaponLockEvent(run, zone, attacker, targetEntityIndex, weapon, frameId,
                    "weapon.lock.acquired", reason, previousProgress, currentProgress);
            else if (previousProgress > 0.99 && currentProgress <= 0.99)
                AppendWeaponLockEvent(run, zone, attacker, targetEntityIndex, weapon, frameId,
                    "weapon.lock.lost", reason, previousProgress, currentProgress);
        }

        private static void AppendWeaponLockEvent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            int targetEntityIndex,
            ResolvedWeapon weapon,
            long frameId,
            string kind,
            string reason,
            double previousProgress,
            double currentProgress)
        {
            var state = weapon.State;
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{attacker.EntityIndex}:weapon:{state.OwnerIndex}:{state.BehaviorIndex}:target:{targetEntityIndex}:{kind}",
                Kind = kind,
                FrameId = frameId,
                ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = attacker.EntityIndex,
                TargetEntityIndex = targetEntityIndex,
                ItemKey = weapon.ItemKey,
                SubjectKey = $"{state.OwnerKind}:{state.OwnerIndex}:{state.BehaviorIndex}",
                Reason = reason,
                ScalarValue = currentProgress,
                AuxiliaryValue = previousProgress
            });
        }

        private static void PreparePhysicalPayloads(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyDictionary<int, AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds)
        {
            var active = new List<AetheriaRuntimePhysicalPayloadCommit>();
            foreach (var projectile in zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>())
            {
                if (projectile == null || !projectile.Active)
                    continue;

                projectile.AgeSeconds += deltaSeconds;
                if (!string.Equals(projectile.PayloadKind, "mine", StringComparison.Ordinal) &&
                    projectile.AgeSeconds >= projectile.LifetimeSeconds)
                {
                    continue;
                }

                if (string.Equals(projectile.PayloadKind, "mine", StringComparison.Ordinal) &&
                    projectile.AgeSeconds >= projectile.ActivationDelaySeconds)
                {
                    projectile.Radius = Math.Max(1, projectile.TriggerRadius);
                    projectile.Stationary = true;
                    projectile.VelocityX = 0;
                    projectile.VelocityY = 0;
                }

                if (projectile.Guided &&
                    projectile.TargetEntityIndex >= 0 &&
                    entities.TryGetValue(projectile.TargetEntityIndex, out var target) &&
                    IsAlive(target))
                {
                    GuidePhysicalPayload(projectile, target);
                }

                active.Add(projectile);
            }
            zone.PhysicalPayloads = active;
        }

        private static void TriggerExpiredDeployables(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyDictionary<int, AetheriaRuntimeEntitySnapshotCommit> entities,
            long frameId,
            double simulationTimeSeconds)
        {
            foreach (var payload in zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>())
            {
                if (!string.Equals(payload.PayloadKind, "mine", StringComparison.Ordinal) ||
                    payload.TriggeredAtSeconds >= 0)
                    continue;

                var expired = payload.AgeSeconds >= payload.LifetimeSeconds;
                var outOfRange = payload.MaximumSourceDistance > 0 &&
                    entities.TryGetValue(payload.SourceEntityIndex, out var source) &&
                    PositionDistanceSq(payload.PositionX, payload.PositionZ, source.PositionX, source.PositionZ) >
                        payload.MaximumSourceDistance * payload.MaximumSourceDistance;
                if (!expired && !outOfRange)
                    continue;

                payload.TriggeredAtSeconds = simulationTimeSeconds;
                payload.DetonationDelaySeconds = 0;
                payload.TriggerReason = expired ? "lifetime" : "range";
                payload.Stationary = true;
                payload.VelocityX = 0;
                payload.VelocityY = 0;
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"physical-payload:{payload.PayloadId}:expired",
                    Kind = "deployable.expired",
                    FrameId = frameId,
                    ZoneIndex = zone.ZoneIndex,
                    SourceEntityIndex = payload.SourceEntityIndex,
                    SubjectKey = payload.PayloadId,
                    ItemKey = payload.WeaponItemKey,
                    Reason = payload.TriggerReason,
                    PositionX = payload.PositionX,
                    PositionZ = payload.PositionZ
                });
            }
        }

        private static double PositionDistanceSq(double leftX, double leftZ, double rightX, double rightZ)
        {
            var dx = leftX - rightX;
            var dz = leftZ - rightZ;
            return dx * dx + dz * dz;
        }

        private static void GuidePhysicalPayload(
            AetheriaRuntimePhysicalPayloadCommit projectile,
            AetheriaRuntimeEntitySnapshotCommit target)
        {
            var speed = Math.Sqrt(
                projectile.VelocityX * projectile.VelocityX +
                projectile.VelocityY * projectile.VelocityY);
            if (speed <= 0.0001)
                return;

            var x = target.PositionX - projectile.PositionX;
            var y = target.PositionZ - projectile.PositionZ;
            var magnitude = Math.Sqrt(x * x + y * y);
            if (magnitude <= 0.0001)
                return;

            projectile.DirectionX = x / magnitude;
            projectile.DirectionY = y / magnitude;
            projectile.VelocityX = projectile.DirectionX * speed;
            projectile.VelocityY = projectile.DirectionY * speed;
        }

        private static WeaponTriggerSet ResolveWeaponTriggers(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonIntentState intents,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var triggers = new WeaponTriggerSet();
            var groups = attacker.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
            var activeGroups = attacker.ActiveWeaponGroups ?? Array.Empty<bool>();
            for (var groupIndex = 0; groupIndex < groups.Count && groupIndex < activeGroups.Count; groupIndex++)
                if (activeGroups[groupIndex])
                    foreach (var equipmentIndex in groups[groupIndex] ?? Array.Empty<int>())
                        triggers.HoldEquipment(equipmentIndex);
            foreach (var intent in (intents == null
                    ? Enumerable.Empty<AetheriaRuntimeDaemonWeaponGroupIntent>()
                    : intents.WeaponGroups)
                .Where(intent => intent != null &&
                    ActorMatches(intent.ActorEntityKey, zone.ZoneIndex, attacker.EntityIndex) &&
                    intent.Fire &&
                    intent.Active))
            {
                if (intent.WeaponGroup < 0 || intent.WeaponGroup >= groups.Count)
                    continue;
                foreach (var equipmentIndex in groups[intent.WeaponGroup] ?? Array.Empty<int>())
                    triggers.PulseEquipment(equipmentIndex);
            }

            foreach (var request in AetheriaRuntimeTurretControllerSimulation.StepEntity(
                         attacker, entities, catalog))
                triggers.RequestBehavior(request.EquipmentIndex, request.BehaviorIndex);
            return triggers;
        }

        private static bool ActorMatches(string actorEntityKey, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(actorEntityKey, out var actorZoneIndex, out var actorEntityIndex) &&
            actorZoneIndex == zoneIndex && actorEntityIndex == entityIndex;

        private static IReadOnlyList<ResolvedConstantWeapon> ResolveConstantWeapons(
            AetheriaRuntimeEntitySnapshotCommit entity,
            WeaponTriggerSet requestedWeapons,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, AetheriaRuntimeBehaviorKinds.ConstantWeapon)
                .Select(behavior =>
                {
                    var magazineSize = Math.Max(0, (int)Math.Round(ReadNumber(behavior.Payload, 13)));
                    var state = EnsureWeaponState(entity, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                        behavior.EquipmentIndex, behavior.BehaviorIndex, behavior.Payload.Kind,
                        magazineSize > 1 ? magazineSize : 1,
                        settings);
                    return new ResolvedConstantWeapon(
                        state, behavior.Item.ItemKey,
                        requestedWeapons.Contains(behavior.EquipmentIndex, behavior.BehaviorIndex),
                        Math.Max(0, behavior.EvaluateStat(2)),
                        PositiveOr(behavior.EvaluateStat(6), 1),
                        Math.Max(0, behavior.EvaluateStat(9)),
                        Math.Max(0, behavior.EvaluateStat(10)),
                        Math.Max(0, behavior.EvaluateStat(11)),
                        ReadItemKey(behavior.Payload, 12), magazineSize,
                        PositiveOr(ReadNumber(behavior.Payload, 14), 1),
                        PositiveOr(ReadNumber(behavior.Payload, 17), 1),
                        Math.Max(0, behavior.EvaluateStat(5)),
                        Math.Max(0, behavior.EvaluateStat(15)));
                })
                .ToArray();
        }

        private static IReadOnlyList<ResolvedDeployableWeapon> ResolveDeployableWeapons(
            AetheriaRuntimeEntitySnapshotCommit entity,
            WeaponTriggerSet requestedWeapons,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, AetheriaRuntimeBehaviorKinds.DeployableWeapon)
                .Where(behavior => requestedWeapons.ContainsPulse(behavior.EquipmentIndex, behavior.BehaviorIndex))
                .Select(behavior => new ResolvedDeployableWeapon(
                    ResolveAuthoredWeapon(entity, behavior, settings),
                    Math.Max(0, ReadNumber(behavior.Payload, 26)),
                    PositiveOr(ReadNumber(behavior.Payload, 27), 30),
                    PositiveOr(behavior.EvaluateStat(28), 25),
                    Math.Max(0, ReadNumber(behavior.Payload, 29)),
                    PositiveOr(behavior.EvaluateStat(30), 25)))
                .ToArray();
        }

        private static void CommitShotResolution(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target,
            ResolvedWeapon weapon,
            string shotId,
            long frameId,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if ((run.ShotReceipts ?? Array.Empty<AetheriaRuntimeShotReceiptCommit>())
                .Any(value => value != null && string.Equals(value.ShotId, shotId, StringComparison.Ordinal)))
                return;

            PublishWeaponVisibility(attacker, weapon);

            var contact = (attacker.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .FirstOrDefault(value => value != null && value.TargetEntityIndex == target.EntityIndex);
            var information = Clamp01(contact?.InfoGathered ?? 0);
            var lockQuality = Clamp01(weapon.State.LockProgress);
            var distance = Math.Sqrt(DistanceSq(attacker, target));
            var rangeSpan = Math.Max(0.001, weapon.Range - weapon.MinRange);
            var normalizedRange = Clamp01((distance - weapon.MinRange) / rangeSpan);
            var rangeFactor = distance < weapon.MinRange || distance > weapon.Range ? 0 : 1 - normalizedRange * 0.35;
            var dx = target.PositionX - attacker.PositionX;
            var dz = target.PositionZ - attacker.PositionZ;
            var length = Math.Max(0.001, Math.Sqrt(dx * dx + dz * dz));
            var relativeX = target.VelocityX - attacker.VelocityX;
            var relativeZ = target.VelocityY - attacker.VelocityY;
            var transverse = Math.Abs(relativeX * (-dz / length) + relativeZ * (dx / length));
            var motionFactor = 1.0 / (1.0 + transverse / Math.Max(1, weapon.ProjectileSpeed));
            var dispersionFactor = 1.0 / (1.0 + weapon.Spread);
            var probability = weapon.Spread <= 0
                ? 1
                : Clamp01(information * (0.5 + 0.5 * lockQuality) * rangeFactor * motionFactor * dispersionFactor);
            var roll = ShotRoll(run.GenerationSeed, shotId, "hit");
            var hit = roll < probability;
            var angleRoll = ShotRoll(run.GenerationSeed, shotId, "impact-angle");
            var radiusRoll = ShotRoll(run.GenerationSeed, shotId, "impact-radius");
            var angle = angleRoll * Math.PI * 2;
            var targetRadius = string.Equals(target.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48.0 : 20.0;
            var impactRadius = hit
                ? Math.Sqrt(radiusRoll) * targetRadius
                : targetRadius + (0.25 + radiusRoll) * Math.Max(targetRadius, weapon.Spread * distance);
            var endpointX = target.PositionX + Math.Cos(angle) * impactRadius;
            var endpointZ = target.PositionZ + Math.Sin(angle) * impactRadius;
            var presentationKind = ResolveShotPresentationKind(weapon.State.BehaviorKind);
            var aliveBefore = IsAlive(target);
            var damage = hit ? ResolveDamage(target, weapon.Damage, weapon.DamageType,
                weapon.Penetration, weapon.DamageSpread, attacker, endpointX, endpointZ, false, catalog) : DamageResolution.None;

            AetheriaRuntimeShotReceipts.Append(run, new AetheriaRuntimeShotReceiptCommit
            {
                ShotId = shotId, FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = attacker.EntityIndex, TargetEntityIndex = target.EntityIndex,
                WeaponItemKey = weapon.ItemKey, WeaponOwnerIndex = weapon.State.OwnerIndex,
                WeaponBehaviorIndex = weapon.State.BehaviorIndex, ContactInformation = information,
                LockQuality = lockQuality, RangeFactor = rangeFactor, MotionFactor = motionFactor,
                DispersionFactor = dispersionFactor, HitProbability = probability, HitRoll = roll,
                Hit = hit, NominalDamage = weapon.Damage, AppliedDamage = damage.TotalAppliedDamage,
                Outcome = !hit ? "miss" : damage.ShieldAbsorbedDamage > 0 ? "shielded" : "hit",
                OriginX = attacker.PositionX, OriginZ = attacker.PositionZ,
                EndpointX = endpointX, EndpointZ = endpointZ,
                PresentationDurationSeconds = presentationKind == "stream"
                    ? 0.12
                    : distance / Math.Max(1, weapon.ProjectileSpeed),
                PresentationKind = presentationKind,
                ImpactAngleRoll = angleRoll, ImpactRadiusRoll = radiusRoll,
                ImpactKind = !hit ? "none" : damage.ShieldAbsorbedDamage > 0 ? "shield" :
                    damage.ArmorAppliedDamage > 0 ? "armor" : damage.EquipmentAppliedDamage > 0 ? "equipment" : "hull",
                PresentationIntensity = Math.Max(0.1, weapon.Damage),
                ShieldAbsorbedDamage = damage.ShieldAbsorbedDamage,
                HullAppliedDamage = damage.HullAppliedDamage,
                ShieldEnergyConsumed = damage.ShieldEnergyConsumed,
                ShieldHeatGenerated = damage.ShieldHeatGenerated,
                DamageType = weapon.DamageType,
                Penetration = weapon.Penetration,
                DamageSpread = weapon.DamageSpread,
                ArmorAppliedDamage = damage.ArmorAppliedDamage,
                EquipmentAppliedDamage = damage.EquipmentAppliedDamage,
                DamageCells = damage.Cells,
                GuidanceMode = weapon.Guidance?.Mode ?? "none",
                GuidanceCurve = weapon.Guidance?.GuidanceCurve ?? Array.Empty<AetheriaRuntimeCurveKey>(),
                GuidanceThrustCurve = weapon.Guidance?.ThrustCurve ?? Array.Empty<AetheriaRuntimeCurveKey>(),
                GuidanceLiftCurve = weapon.Guidance?.LiftCurve ?? Array.Empty<AetheriaRuntimeCurveKey>(),
                GuidanceThrust = weapon.Guidance?.Thrust ?? 0,
                GuidanceTopSpeed = weapon.Guidance?.TopSpeed ?? 0,
                GuidanceDodgeFrequency = weapon.Guidance?.DodgeFrequency ?? 0
            });
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"shot:{shotId}:resolved", Kind = "shot.resolved", FrameId = frameId,
                ZoneIndex = zone.ZoneIndex, SourceEntityIndex = attacker.EntityIndex,
                TargetEntityIndex = target.EntityIndex, SubjectKey = hit ? "hit" : "miss",
                ItemKey = weapon.ItemKey, ScalarValue = probability,
                PositionX = target.PositionX, PositionZ = target.PositionZ
            });
            if (damage.ShieldAbsorbedDamage > 0)
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"shot:{shotId}:shield", Kind = "shield.absorbed", FrameId = frameId,
                    ZoneIndex = zone.ZoneIndex, SourceEntityIndex = attacker.EntityIndex,
                    TargetEntityIndex = target.EntityIndex, SubjectKey = shotId,
                    ItemKey = weapon.ItemKey, ScalarValue = damage.ShieldAbsorbedDamage,
                    PositionX = endpointX, PositionZ = endpointZ
                });
            if (damage.TotalAppliedDamage > 0)
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"shot:{shotId}:damage", Kind = "entity.damaged", FrameId = frameId,
                    ZoneIndex = zone.ZoneIndex, SourceEntityIndex = attacker.EntityIndex,
                    TargetEntityIndex = target.EntityIndex, SubjectKey = shotId,
                    ItemKey = weapon.ItemKey, ScalarValue = damage.TotalAppliedDamage,
                    AuxiliaryValue = damage.HullAppliedDamage, Reason = weapon.DamageType,
                    PositionX = target.PositionX, PositionZ = target.PositionZ
                });
            PublishEquipmentDestroyedEvents(run, zone, target, damage,
                attacker.EntityIndex, shotId, frameId);
            if (aliveBefore && (damage.CockpitDestroyed || !IsAlive(target)))
                CommitDestruction(run, zone, target, attacker.EntityIndex, shotId, weapon.ItemKey,
                    frameId, settings, damage.CockpitDestroyed ? "cockpit-destroyed" : "hull-destroyed");
        }

        private static void PublishWeaponVisibility(
            AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedWeapon weapon)
        {
            if (weapon.Visibility <= 0)
                return;
            AetheriaRuntimeVisibilitySimulation.SetTransientSource(
                entity,
                $"weapon:{weapon.State.OwnerKind}:{weapon.State.OwnerIndex}:{weapon.State.BehaviorIndex}",
                weapon.Visibility);
        }

        private static void PublishEquipmentDestroyedEvents(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit target,
            DamageResolution damage,
            int sourceEntityIndex,
            string subjectKey,
            long frameId)
        {
            foreach (var equipmentIndex in damage.DestroyedEquipmentIndices)
            {
                var item = equipmentIndex >= 0 && equipmentIndex < (target.Equipment?.Count ?? 0)
                    ? target.Equipment[equipmentIndex]?.Item
                    : null;
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"{subjectKey}:equipment:{equipmentIndex}:destroyed",
                    Kind = "equipment.destroyed", FrameId = frameId, ZoneIndex = zone.ZoneIndex,
                    SourceEntityIndex = sourceEntityIndex, TargetEntityIndex = target.EntityIndex,
                    SubjectKey = subjectKey, ItemKey = item?.ItemKey ?? "",
                    ScalarValue = equipmentIndex, Reason = "durability-depleted",
                    PositionX = target.PositionX, PositionZ = target.PositionZ
                });
            }
        }

        private static string NextShotId(AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker, ResolvedWeapon weapon)
        {
            weapon.State.ShotSequence++;
            return $"shot:{zone.ZoneIndex}:{attacker.EntityIndex}:{weapon.State.OwnerIndex}:{weapon.State.BehaviorIndex}:{weapon.State.ShotSequence}";
        }

        private static void CommitDestruction(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit target,
            int sourceEntityIndex,
            string subjectKey,
            string weaponItemKey,
            long frameId,
            AetheriaRuntimeDaemonSimulationSettings settings,
            string causeOfDeath = "hull-destroyed")
        {
            if (!string.IsNullOrWhiteSpace(target.DestructionId)) return;

            var destructionId = $"destruction:{run.RunId}:{zone.ZoneIndex}:{target.EntityIndex}:{frameId}";
            target.DestructionId = destructionId;
            target.DestroyedFrameId = frameId;
            target.CauseOfDeath = causeOfDeath ?? "";
            target.IsActive = false;
            target.TargetEntityIndex = -1;
            target.TractorPower = 0;
            target.TractorTargetPower = 0;
            target.ActiveWeaponGroups = Array.Empty<bool>();
            foreach (var state in target.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
            {
                state.Firing = false;
                state.TriggerPending = false;
                state.BurstRemaining = 0;
                state.Charging = false;
                state.Charged = false;
                state.Charge = 0;
                state.ChargeHoldSeconds = 0;
                state.ChargeRiskChecks = 0;
                state.ChargeMalfunctionRisk = 0;
                state.LockTargetEntityIndex = -1;
                state.LockProgress = 0;
            }

            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>()).ToList();
            var nextPickupIndex = Math.Max(
                zone.NextPickupIndex,
                pickups.Select(value => value.PickupIndex).DefaultIfEmpty(-1).Max() + 1);
            void Drop(AetheriaRuntimeLoadoutItemCommit item, string sourceKind, int sourceIndex)
            {
                var velocity = DestructionVelocity(run.GenerationSeed, destructionId,
                    $"{sourceKind}:{sourceIndex}", settings.LootDropVelocity);
                var pickup = new AetheriaRuntimeDroppedPickupCommit
                {
                    PickupIndex = nextPickupIndex++, PositionX = target.PositionX, PositionY = target.PositionY,
                    PositionZ = target.PositionZ, VelocityX = velocity.X, VelocityY = velocity.Y,
                    VelocityZ = velocity.Z, Item = CloneItem(item), AgeSeconds = 0,
                    LifetimeSeconds = settings.PickupLifetimeSeconds
                };
                pickups.Add(pickup);
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"{destructionId}:pickup:{pickup.PickupIndex}", Kind = "pickup.dropped",
                    FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = sourceEntityIndex,
                    TargetEntityIndex = target.EntityIndex, PickupIndex = pickup.PickupIndex,
                    ItemKey = pickup.Item.ItemKey, ScalarValue = Math.Max(1, pickup.Item.Quantity),
                    SubjectKey = destructionId, Reason = sourceKind,
                    PositionX = target.PositionX, PositionZ = target.PositionZ
                });
            }

            var equipment = target.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var index = 0; index < equipment.Count; index++)
            {
                var slot = equipment[index];
                if (slot?.Item == null ||
                    ShotRoll(run.GenerationSeed, destructionId, $"equipment:{index}") >= settings.LootDropProbability)
                    continue;
                Drop(slot.Item, "equipment", index);
            }
            var cargoIndex = 0;
            foreach (var slot in (target.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .Where(bay => bay != null)
                .SelectMany(bay => bay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()))
            {
                if (slot?.Item != null) Drop(slot.Item, "cargo", cargoIndex);
                cargoIndex++;
            }
            zone.NextPickupIndex = nextPickupIndex;
            zone.DroppedPickups = pickups;

            target.Equipment = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            target.CargoContents = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            target.CargoBays = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            target.DockingBays = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            target.DockingBayContents = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            target.DockingBayAssignments = Array.Empty<int>();
            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null || entity.EntityIndex == target.EntityIndex) continue;
                if (entity.TargetEntityIndex == target.EntityIndex) entity.TargetEntityIndex = -1;
                entity.ChildEntityIndices = (entity.ChildEntityIndices ?? Array.Empty<int>())
                    .Where(index => index != target.EntityIndex).ToArray();
                entity.DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>())
                    .Select(index => index == target.EntityIndex ? -1 : index).ToArray();
                entity.Contacts = (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Where(contact => contact != null && contact.TargetEntityIndex != target.EntityIndex).ToArray();
            }
            var controlledEntityDestroyed =
                run.CurrentZoneIndex == zone.ZoneIndex &&
                TryParseEntityIndex(run.CurrentEntityKey, out var currentIndex) &&
                currentIndex == target.EntityIndex;
            var runFailed = false;
            if (controlledEntityDestroyed)
            {
                runFailed = AetheriaRuntimeRunLifecycle.Fail(run, target.CauseOfDeath, frameId);
                run.CurrentEntityKey = "";
            }

            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = destructionId, Kind = "entity.destroyed", FrameId = frameId,
                ZoneIndex = zone.ZoneIndex, SourceEntityIndex = sourceEntityIndex,
                TargetEntityIndex = target.EntityIndex, SubjectKey = subjectKey,
                ItemKey = weaponItemKey, Reason = target.CauseOfDeath,
                PositionX = target.PositionX, PositionZ = target.PositionZ
            });
            if (runFailed)
            {
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                {
                    EventId = $"run:{run.RunId}:failed",
                    Kind = "run.failed",
                    FrameId = frameId,
                    ZoneIndex = zone.ZoneIndex,
                    SourceEntityIndex = sourceEntityIndex,
                    TargetEntityIndex = target.EntityIndex,
                    SubjectKey = run.RunId,
                    Reason = target.CauseOfDeath,
                    PositionX = target.PositionX,
                    PositionZ = target.PositionZ
                });
            }
        }

        private static AetheriaRuntimeLoadoutItemCommit CloneItem(AetheriaRuntimeLoadoutItemCommit item) => new()
        {
            ItemKey = item.ItemKey ?? "", Quality = item.Quality, Durability = item.Durability,
            Quantity = item.Quantity, Enabled = item.Enabled, OverrideShutdown = item.OverrideShutdown,
            Temperature = item.Temperature
        };

        private static (double X, double Y, double Z) DestructionVelocity(
            uint seed, string destructionId, string itemKey, double speed)
        {
            var z = ShotRoll(seed, destructionId, $"{itemKey}:velocity-z") * 2 - 1;
            var angle = ShotRoll(seed, destructionId, $"{itemKey}:velocity-angle") * Math.PI * 2;
            var radius = Math.Sqrt(Math.Max(0, 1 - z * z));
            return (Math.Cos(angle) * radius * speed, z * speed, Math.Sin(angle) * radius * speed);
        }

        private static void AppendShotCommittedEvent(AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone, AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target, ResolvedWeapon weapon, string shotId, long frameId)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"shot:{shotId}:committed", Kind = "shot.committed", FrameId = frameId,
                ZoneIndex = zone.ZoneIndex, SourceEntityIndex = attacker.EntityIndex,
                TargetEntityIndex = target.EntityIndex, SubjectKey = shotId, ItemKey = weapon.ItemKey,
                ScalarValue = weapon.Damage, PositionX = attacker.PositionX, PositionZ = attacker.PositionZ
            });
        }

        private static double ShotRoll(uint seed, string shotId, string salt)
        {
            unchecked
            {
                uint hash = seed ^ 2166136261u;
                foreach (var c in (shotId ?? "") + ":" + (salt ?? "")) hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
                return (hash + 1.0) / (uint.MaxValue + 2.0);
            }
        }

        private static IReadOnlyList<ResolvedChargedWeapon> ResolveChargedWeapons(
            AetheriaRuntimeEntitySnapshotCommit entity, WeaponTriggerSet requestedWeapons,
            AetheriaRuntimeCatalogSnapshot? catalog, AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, AetheriaRuntimeBehaviorKinds.ChargedWeapon)
                .Where(value => string.Equals(value.Payload.Kind, AetheriaRuntimeBehaviorKinds.ChargedWeapon, StringComparison.Ordinal))
                .Select(value => new ResolvedChargedWeapon(
                    ResolveAuthoredWeapon(entity, value, settings), value.Item,
                    requestedWeapons.ContainsPulse(value.EquipmentIndex, value.BehaviorIndex),
                    PositiveOr(value.EvaluateStat(21), 1),
                    Math.Max(0, value.EvaluateStat(22)), Math.Max(0, value.EvaluateStat(23)),
                    ReadNumber(value.Payload, 25), PositiveOr(ReadNumber(value.Payload, 26), 1),
                    PositiveOr(ReadNumber(value.Payload, 27), 1), PositiveOr(ReadNumber(value.Payload, 29), 1),
                    PositiveOr(ReadNumber(value.Payload, 30), 1), PositiveOr(ReadNumber(value.Payload, 31), 1),
                    PositiveOr(ReadNumber(value.Payload, 32), 1)))
                .ToArray();
        }

        private static IReadOnlyList<ResolvedWeapon> ResolveWeapons(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var authored = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, AetheriaRuntimeBehaviorKinds.InstantWeapon)
                .Where(behavior => !string.Equals(behavior.Payload.Kind, AetheriaRuntimeBehaviorKinds.ChargedWeapon, StringComparison.Ordinal))
                .Where(behavior => !string.Equals(behavior.Payload.Kind, AetheriaRuntimeBehaviorKinds.DeployableWeapon, StringComparison.Ordinal))
                .Select(behavior => ResolveAuthoredWeapon(entity, behavior, settings))
                .ToArray();
            if (catalog != null)
                return authored;

            return new[] { ResolveFallbackWeapon(entity, settings) };
        }

        private static ResolvedWeapon ResolveAuthoredWeapon(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeEquippedBehavior behavior,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var state = EnsureWeaponState(entity, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                behavior.EquipmentIndex, behavior.BehaviorIndex, behavior.Payload.Kind,
                Math.Max(0, (int)Math.Round(ReadNumber(behavior.Payload, 13))), settings);
            return new ResolvedWeapon(
                state,
                behavior.Item.ItemKey,
                PositiveOr(behavior.EvaluateStat(2), ResolveProjectileDamage(entity, settings)) /
                    Math.Max(1, (int)Math.Round(behavior.EvaluateStat(17))),
                PositiveOr(behavior.EvaluateStat(6), settings.AttackRange),
                PositiveOr(behavior.EvaluateStat(19), settings.WeaponCooldownSeconds),
                PositiveOr(behavior.EvaluateStat(16), settings.ProjectileSpeed),
                Math.Max(0, behavior.EvaluateStat(10)) / Math.Max(1, (int)Math.Round(behavior.EvaluateStat(17))),
                Math.Max(0, behavior.EvaluateStat(9)) / Math.Max(1, (int)Math.Round(behavior.EvaluateStat(17))),
                ReadItemKey(behavior.Payload, 12),
                Math.Max(0, (int)Math.Round(ReadNumber(behavior.Payload, 13))),
                PositiveOr(ReadNumber(behavior.Payload, 14), settings.WeaponCooldownSeconds),
                Math.Max(1, (int)Math.Round(behavior.EvaluateStat(17))),
                Math.Max(0, behavior.EvaluateStat(18)),
                ReadBool(behavior.Payload, 20),
                PositiveOr(behavior.EvaluateStat(21), settings.WeaponLockSpeed),
                Math.Max(0, behavior.EvaluateStat(22)),
                PositiveOr(behavior.EvaluateStat(23), settings.WeaponLockAngleDegrees),
                PositiveOr(behavior.EvaluateStat(24), settings.WeaponLockDirectionImpact),
                Math.Max(0, behavior.EvaluateStat(25)),
                Math.Max(0, behavior.EvaluateStat(11)),
                Math.Max(0, behavior.EvaluateStat(5)),
                Math.Max(0, behavior.EvaluateStat(15)),
                ReadEnumName(behavior.Payload, 1, "Kinetic"),
                Math.Max(0, behavior.EvaluateStat(3)),
                Math.Max(0, behavior.EvaluateStat(4)),
                ResolveGuidedPresentation(behavior));
        }

        private static ResolvedGuidedPresentation? ResolveGuidedPresentation(
            AetheriaRuntimeEquippedBehavior behavior)
        {
            if (string.Equals(behavior.Payload.Kind, AetheriaRuntimeBehaviorKinds.Launcher, StringComparison.Ordinal))
                return new ResolvedGuidedPresentation(
                    "target-entity", ReadCurve(behavior.Payload, 26), ReadCurve(behavior.Payload, 27),
                    ReadCurve(behavior.Payload, 28), Math.Max(0, behavior.EvaluateStat(29)),
                    Math.Max(0, behavior.EvaluateStat(31)), Math.Max(0, ReadNumber(behavior.Payload, 30)));
            if (string.Equals(behavior.Payload.Kind, AetheriaRuntimeBehaviorKinds.GuidedWeapon, StringComparison.Ordinal))
                return new ResolvedGuidedPresentation(
                    "look-direction", ReadCurve(behavior.Payload, 21), ReadCurve(behavior.Payload, 22),
                    ReadCurve(behavior.Payload, 23), Math.Max(0, behavior.EvaluateStat(24)),
                    Math.Max(0, behavior.EvaluateStat(26)), Math.Max(0, ReadNumber(behavior.Payload, 25)));
            return null;
        }

        private static ResolvedWeapon ResolveFallbackWeapon(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var state = EnsureWeaponState(entity, "daemon-simulation", entity.EntityIndex, 0, "ProjectileWeapon", -1, settings);
            return new ResolvedWeapon(state,
                IsPlayerOwned(entity) ? "vanguard-bolt" : "raider-bolt",
                ResolveProjectileDamage(entity, settings), settings.AttackRange, settings.WeaponCooldownSeconds,
                settings.ProjectileSpeed, ResolveProjectileDamage(entity, settings) * settings.ProjectileHeatScale,
                0, "", -1, settings.WeaponCooldownSeconds,
                1, 0, false,
                settings.WeaponLockSpeed, settings.WeaponLockSensorImpact, settings.WeaponLockAngleDegrees,
                settings.WeaponLockDirectionImpact, settings.WeaponLockDecayPerSecond,
                0, 0, 0, "Kinetic", 0, 0);
        }

        private static AetheriaRuntimeWeaponStateCommit EnsureWeaponState(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string ownerKind,
            int ownerIndex,
            int behaviorIndex,
            string behaviorKind,
            int initialAmmo,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var states = (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>()).ToList();
            var state = states.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.OwnerKind, ownerKind, StringComparison.Ordinal) &&
                candidate.OwnerIndex == ownerIndex &&
                candidate.BehaviorIndex == behaviorIndex);
            if (state != null)
                return state;

            state = new AetheriaRuntimeWeaponStateCommit
            {
                OwnerKind = ownerKind,
                OwnerIndex = ownerIndex,
                BehaviorIndex = behaviorIndex,
                BehaviorKind = behaviorKind,
                Ammo = initialAmmo,
                BurstRemaining = 0,
                BurstInterval = settings.WeaponCooldownSeconds,
                LockTargetEntityIndex = -1
            };
            states.Add(state);
            entity.WeaponStates = states.ToArray();
            return state;
        }

        private static double ResolveProjectileDamage(
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return IsPlayerOwned(attacker) ? settings.PawnProjectileDamage : settings.RaiderProjectileDamage;
        }

        private static double PositiveOr(double value, double fallback) =>
            double.IsFinite(value) && value > 0 ? value : fallback;

        private static string ResolveShotPresentationKind(string behaviorKind)
        {
            if (string.Equals(behaviorKind, AetheriaRuntimeBehaviorKinds.GuidedWeapon, StringComparison.Ordinal))
                return "guided";
            if (string.Equals(behaviorKind, AetheriaRuntimeBehaviorKinds.ConstantWeapon, StringComparison.Ordinal))
                return "stream";
            if (string.Equals(behaviorKind, AetheriaRuntimeBehaviorKinds.Launcher, StringComparison.Ordinal))
                return "guided";
            return "bolt";
        }

        private static string ReadItemKey(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            var value = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value;
            return value?.ItemKeyValue ?? value?.StringValue ?? "";
        }

        private static double ReadNumber(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            return (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value?.NumberValue ?? 0;
        }

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadCurve(
            AetheriaRuntimeBehaviorPayload payload, int key)
        {
            var value = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value;
            var children = value?.Children ?? Array.Empty<AetheriaRuntimeBehaviorValue>();
            if (children.Count == 1 && children[0]?.Children?.Count > 0)
                children = children[0].Children;
            return children
                .Where(child => child?.Children != null && child.Children.Count >= 4)
                .Select(child => new AetheriaRuntimeCurveKey(
                    child.Children[0].NumberValue, child.Children[1].NumberValue,
                    child.Children[2].NumberValue, child.Children[3].NumberValue))
                .ToArray();
        }

        private static bool ReadBool(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            return (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value?.BoolValue ?? false;
        }

        private static string ReadEnumName(AetheriaRuntimeBehaviorPayload payload, int key, string fallback)
        {
            var value = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value;
            if (!string.IsNullOrWhiteSpace(value?.StringValue)) return value.StringValue;
            var names = new[] { "Kinetic", "Corrosive", "Electric", "Thermal", "Optical", "Ionizing" };
            var index = value == null ? -1 : (int)Math.Round(value.NumberValue);
            return index >= 0 && index < names.Length ? names[index] : fallback;
        }

        private static WeaponRoundResult CommitWeaponRound(
            AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedWeapon weapon,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (weapon.MagazineSize > 1 && weapon.State.Ammo <= 0)
            {
                if (!string.IsNullOrWhiteSpace(weapon.AmmoItemKey))
                {
                    if (!AetheriaRuntimeCargoTransactions.TryFind(entity, weapon.AmmoItemKey,
                            out var cargoIndex, out var x, out var y) ||
                        !AetheriaRuntimeCargoTransactions.TryRemoveQuantity(
                            entity, cargoIndex, weapon.AmmoItemKey, x, y, 1, out _))
                        return WeaponRoundResult.NoAmmo;
                }
                weapon.State.Reloading = true;
                weapon.State.ReloadProgress = weapon.ReloadTime;
                return WeaponRoundResult.ReloadStarted;
            }

            if (!CanSupplyEnergy(entity, catalog, weapon.Energy))
                return WeaponRoundResult.InsufficientEnergy;
            CommitEnergy(entity, catalog, weapon.Energy);
            if (weapon.MagazineSize > 1)
                weapon.State.Ammo--;
            return WeaponRoundResult.Fired;
        }

        private static bool CanSupplyEnergy(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double demand) =>
            AetheriaRuntimeEnergySimulation.CanSupply(entity, catalog, demand);

        private static void CommitEnergy(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double demand) =>
            AetheriaRuntimeEnergySimulation.TryConsume(entity, catalog, demand);

        private static void ApplyWeaponWear(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeWeaponStateCommit state, double multiplier)
        {
            if (state == null || state.OwnerKind != AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind) return;
            var wear = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .FirstOrDefault(value => value != null && value.EquipmentIndex == state.OwnerIndex)?.Wear ?? 0;
            AetheriaRuntimeThermalSimulation.ApplyWear(entity, state.OwnerIndex, wear * multiplier);
        }

        private sealed class WeaponTriggerSet
        {
            private readonly HashSet<int> _pulsedEquipment = new HashSet<int>();
            private readonly HashSet<int> _heldEquipment = new HashSet<int>();
            private readonly HashSet<(int EquipmentIndex, int BehaviorIndex)> _pulsedBehaviors =
                new HashSet<(int EquipmentIndex, int BehaviorIndex)>();
            private readonly HashSet<(int EquipmentIndex, int BehaviorIndex)> _heldBehaviors =
                new HashSet<(int EquipmentIndex, int BehaviorIndex)>();

            public void PulseEquipment(int equipmentIndex) =>
                _pulsedEquipment.Add(equipmentIndex);

            public void HoldEquipment(int equipmentIndex) =>
                _heldEquipment.Add(equipmentIndex);

            public void RequestBehavior(int equipmentIndex, int behaviorIndex)
            {
                _pulsedBehaviors.Add((equipmentIndex, behaviorIndex));
                _heldBehaviors.Add((equipmentIndex, behaviorIndex));
            }

            public bool ContainsPulse(int equipmentIndex, int behaviorIndex) =>
                _pulsedEquipment.Contains(equipmentIndex) ||
                _pulsedBehaviors.Contains((equipmentIndex, behaviorIndex));

            public bool Contains(int equipmentIndex, int behaviorIndex) =>
                ContainsPulse(equipmentIndex, behaviorIndex) ||
                _heldEquipment.Contains(equipmentIndex) ||
                _heldBehaviors.Contains((equipmentIndex, behaviorIndex));
        }

        private enum WeaponRoundResult { Fired, ReloadStarted, InsufficientEnergy, NoAmmo }

        private sealed class ResolvedWeapon
        {
            public ResolvedWeapon(AetheriaRuntimeWeaponStateCommit state, string itemKey, double damage, double range,
                double cooldown, double projectileSpeed, double heat, double energy, string ammoItemKey,
                int magazineSize, double reloadTime, int burstCount, double burstTime, bool singleAmmoBurst,
                double lockSpeed, double lockSensorImpact,
                double lockAngleDegrees, double lockDirectionImpact, double lockDecayPerSecond,
                double visibility = 0, double minRange = 0, double spread = 0, string damageType = "Kinetic",
                double penetration = 0, double damageSpread = 0,
                ResolvedGuidedPresentation? guidance = null)
            {
                State = state; ItemKey = itemKey; Damage = damage; Range = range; Cooldown = cooldown;
                ProjectileSpeed = projectileSpeed; Heat = heat; Energy = energy; AmmoItemKey = ammoItemKey;
                MagazineSize = magazineSize; ReloadTime = reloadTime; LockSpeed = lockSpeed;
                BurstCount = burstCount; BurstTime = burstTime; SingleAmmoBurst = singleAmmoBurst;
                LockSensorImpact = lockSensorImpact; LockAngleDegrees = lockAngleDegrees;
                LockDirectionImpact = lockDirectionImpact; LockDecayPerSecond = lockDecayPerSecond;
                Visibility = visibility; MinRange = minRange; Spread = spread;
                DamageType = damageType; Penetration = penetration; DamageSpread = damageSpread;
                Guidance = guidance;
            }

            public AetheriaRuntimeWeaponStateCommit State { get; }
            public string ItemKey { get; }
            public double Damage { get; }
            public double Range { get; }
            public double Cooldown { get; }
            public double ProjectileSpeed { get; }
            public double Heat { get; }
            public double Energy { get; }
            public string AmmoItemKey { get; }
            public int MagazineSize { get; }
            public double ReloadTime { get; }
            public int BurstCount { get; }
            public double BurstTime { get; }
            public bool SingleAmmoBurst { get; }
            public double LockSpeed { get; }
            public double LockSensorImpact { get; }
            public double LockAngleDegrees { get; }
            public double LockDirectionImpact { get; }
            public double LockDecayPerSecond { get; }
            public double Visibility { get; }
            public double MinRange { get; }
            public double Spread { get; }
            public string DamageType { get; }
            public double Penetration { get; }
            public double DamageSpread { get; }
            public ResolvedGuidedPresentation? Guidance { get; }
        }

        private sealed class ResolvedGuidedPresentation
        {
            public ResolvedGuidedPresentation(
                string mode,
                IReadOnlyList<AetheriaRuntimeCurveKey> guidanceCurve,
                IReadOnlyList<AetheriaRuntimeCurveKey> thrustCurve,
                IReadOnlyList<AetheriaRuntimeCurveKey> liftCurve,
                double thrust,
                double topSpeed,
                double dodgeFrequency)
            {
                Mode = mode ?? "none";
                GuidanceCurve = guidanceCurve ?? Array.Empty<AetheriaRuntimeCurveKey>();
                ThrustCurve = thrustCurve ?? Array.Empty<AetheriaRuntimeCurveKey>();
                LiftCurve = liftCurve ?? Array.Empty<AetheriaRuntimeCurveKey>();
                Thrust = thrust;
                TopSpeed = topSpeed;
                DodgeFrequency = dodgeFrequency;
            }

            public string Mode { get; }
            public IReadOnlyList<AetheriaRuntimeCurveKey> GuidanceCurve { get; }
            public IReadOnlyList<AetheriaRuntimeCurveKey> ThrustCurve { get; }
            public IReadOnlyList<AetheriaRuntimeCurveKey> LiftCurve { get; }
            public double Thrust { get; }
            public double TopSpeed { get; }
            public double DodgeFrequency { get; }
        }

        private sealed class ResolvedConstantWeapon
        {
            public ResolvedConstantWeapon(AetheriaRuntimeWeaponStateCommit state, string itemKey,
                bool selected, double damage, double range, double energy, double heat, double visibility, string ammoItemKey,
                int magazineSize, double reloadTime, double ammoIntervalDuration,
                double minRange, double spread)
            {
                State = state; ItemKey = itemKey; Selected = selected; Damage = damage; Range = range;
                Energy = energy; Heat = heat; Visibility = visibility; AmmoItemKey = ammoItemKey;
                MagazineSize = magazineSize; ReloadTime = reloadTime;
                AmmoIntervalDuration = ammoIntervalDuration; MinRange = minRange; Spread = spread;
            }

            public ResolvedWeapon ResolutionShot(double deltaSeconds) => new ResolvedWeapon(
                State, ItemKey, Damage * deltaSeconds, Range, 0, 1, 0, 0, "", 0, 0,
                1, 0, false, 1, 0, 180, 0, 0, Visibility, MinRange, Spread, "Kinetic", 0, 0);

            public AetheriaRuntimeWeaponStateCommit State { get; }
            public string ItemKey { get; }
            public bool Selected { get; }
            public double Damage { get; }
            public double Range { get; }
            public double Energy { get; }
            public double Heat { get; }
            public double Visibility { get; }
            public string AmmoItemKey { get; }
            public int MagazineSize { get; }
            public double ReloadTime { get; }
            public double AmmoIntervalDuration { get; }
            public double MinRange { get; }
            public double Spread { get; }
        }

        private sealed class ResolvedDeployableWeapon
        {
            public ResolvedDeployableWeapon(ResolvedWeapon weapon, double activationDelaySeconds,
                double lifetimeSeconds, double triggerRadius, double detonationDelaySeconds, double blastRadius)
            {
                Weapon = weapon;
                ActivationDelaySeconds = activationDelaySeconds;
                LifetimeSeconds = lifetimeSeconds;
                TriggerRadius = triggerRadius;
                DetonationDelaySeconds = detonationDelaySeconds;
                BlastRadius = blastRadius;
            }

            public ResolvedWeapon Weapon { get; }
            public double ActivationDelaySeconds { get; }
            public double LifetimeSeconds { get; }
            public double TriggerRadius { get; }
            public double DetonationDelaySeconds { get; }
            public double BlastRadius { get; }
        }

        private sealed class ResolvedChargedWeapon
        {
            public ResolvedChargedWeapon(ResolvedWeapon baseWeapon, AetheriaRuntimeLoadoutItemCommit item,
                bool requested, double chargeTime, double chargeEnergy, double chargeHeat,
                double failureCharge, double failureDamage, double damageMultiplier,
                double burstMultiplier, double visibilityMultiplier, double velocityMultiplier, double heatMultiplier)
            {
                Base = baseWeapon; Item = item; Requested = requested; ChargeTime = chargeTime;
                ChargeEnergy = chargeEnergy; ChargeHeat = chargeHeat; FailureCharge = failureCharge;
                FailureDamage = failureDamage; DamageMultiplier = damageMultiplier;
                BurstMultiplier = burstMultiplier; VisibilityMultiplier = visibilityMultiplier;
                VelocityMultiplier = velocityMultiplier;
                HeatMultiplier = heatMultiplier;
            }
            public ResolvedWeapon CommittedShot()
            {
                var count = Math.Max(1, (int)Math.Round(Base.BurstCount * BurstMultiplier));
                return new ResolvedWeapon(Base.State, Base.ItemKey,
                    Base.Damage * Base.BurstCount * DamageMultiplier / count, Base.Range, Base.Cooldown,
                    Base.ProjectileSpeed * VelocityMultiplier, Base.Heat * Base.BurstCount * HeatMultiplier / count,
                    Base.Energy * Base.BurstCount / count, Base.AmmoItemKey, Base.MagazineSize, Base.ReloadTime,
                    count, Base.BurstTime, Base.SingleAmmoBurst, Base.LockSpeed, Base.LockSensorImpact,
                    Base.LockAngleDegrees, Base.LockDirectionImpact, Base.LockDecayPerSecond,
                    Base.Visibility * VisibilityMultiplier,
                    Base.MinRange, Base.Spread, Base.DamageType, Base.Penetration, Base.DamageSpread);
            }
            public ResolvedWeapon Base { get; }
            public AetheriaRuntimeLoadoutItemCommit Item { get; }
            public bool Requested { get; }
            public double ChargeTime { get; }
            public double ChargeEnergy { get; }
            public double ChargeHeat { get; }
            public double FailureCharge { get; }
            public double FailureDamage { get; }
            public double DamageMultiplier { get; }
            public double BurstMultiplier { get; }
            public double VisibilityMultiplier { get; }
            public double VelocityMultiplier { get; }
            public double HeatMultiplier { get; }
        }

        private static void EnsureStats(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonSimulationSettings settings,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            foreach (var entity in entities)
            {
                if (entity.StatGrids == null || entity.StatGrids.Count == 0)
                {
                    entity.StatGrids = new[]
                    {
                        Stat(Hull, DefaultHull(entity, settings)),
                        Stat(Shield, DefaultShield(entity, settings)),
                        Stat(Heat, 0)
                    };
                }

                if (!entity.IsActive && GetStat(entity, Hull) > 0)
                    entity.IsActive = true;
                EnsureArmorState(entity, catalog);
            }
        }

        private static DamageResolution ResolveDamage(
            AetheriaRuntimeEntitySnapshotCommit target,
            double damage,
            string damageType,
            double penetration,
            double spread,
            AetheriaRuntimeEntitySnapshotCommit? source,
            double impactX,
            double impactZ,
            bool splash,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            damage = Math.Max(0, damage);
            if (damage <= 0)
                return DamageResolution.None;

            foreach (var shield in AetheriaRuntimeEquippedBehaviorQueries.FindOperational(target, catalog, "Shield"))
            {
                var efficiency = Math.Max(0.000001, shield.EvaluateStat(1));
                var energyUsage = Math.Max(0, shield.EvaluateStat(2));
                var energyDemand = damage * energyUsage;
                shield.State.ShieldEfficiency = efficiency;
                shield.State.ShieldEnergyUsage = energyUsage;
                if (!CanSupplyEnergy(target, catalog, energyDemand))
                    continue;

                CommitEnergy(target, catalog, energyDemand);
                var heat = damage / efficiency;
                AetheriaRuntimeThermalSimulation.AddHeat(target, heat);
                RefreshShieldProjection(target, catalog);
                return new DamageResolution(damage, 0, 0, 0, energyDemand, heat,
                    Array.Empty<AetheriaRuntimeDamageCellCommit>());
            }

            EnsureArmorState(target, catalog);
            var hull = catalog?.FindItem(target.HullItemKey ?? "");
            var cells = ResolveDamageCells(target, hull, impactX, impactZ, source, penetration, spread, splash);
            if (cells.Count == 0)
                cells.Add((0, 0));
            var armor = FindGrid(target, Armor);
            var perCell = damage / cells.Count;
            var armorApplied = 0.0;
            var equipmentApplied = 0.0;
            var hullRequested = 0.0;
            var receipts = new List<AetheriaRuntimeDamageCellCommit>(cells.Count);
            var destroyedEquipmentIndices = new HashSet<int>();
            var cockpitDestroyed = false;
            foreach (var cell in cells)
            {
                var remaining = perCell;
                var armorTaken = ApplyGridDamage(armor, cell.X, cell.Y, remaining);
                armorApplied += armorTaken;
                remaining -= armorTaken;
                var equipmentIndex = -1;
                var equipmentTaken = 0.0;
                if (remaining > 0.1)
                {
                    var equipment = FindEquipmentAt(target, catalog, cell.X, cell.Y);
                    if (equipment.Slot != null)
                    {
                        equipmentIndex = equipment.Index;
                        var durabilityBefore = equipment.Slot.Item.Durability;
                        equipmentTaken = Math.Min(Math.Max(0, equipment.Slot.Item.Durability), remaining);
                        equipment.Slot.Item.Durability = Math.Max(0, equipment.Slot.Item.Durability - equipmentTaken);
                        equipmentApplied += equipmentTaken;
                        remaining -= equipmentTaken;
                        if (durabilityBefore > 0.01 && equipment.Slot.Item.Durability <= 0.01)
                        {
                            destroyedEquipmentIndices.Add(equipmentIndex);
                            DeactivateDestroyedEquipment(target, equipmentIndex);
                            var catalogItem = catalog?.FindItem(equipment.Slot.Item.ItemKey ?? "");
                            cockpitDestroyed |= (catalogItem?.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
                                .Any(payload => payload != null &&
                                    AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(payload.Kind, "Cockpit"));
                        }
                    }
                }
                var cellHull = remaining > 0 ? remaining : 0;
                hullRequested += cellHull;
                receipts.Add(new AetheriaRuntimeDamageCellCommit
                {
                    X = cell.X, Y = cell.Y, ArmorAppliedDamage = armorTaken,
                    EquipmentIndex = equipmentIndex, EquipmentAppliedDamage = equipmentTaken,
                    HullAppliedDamage = cellHull
                });
            }
            var hullBefore = Math.Max(0, GetStat(target, Hull));
            var hullApplied = hullRequested > 0.1 ? Math.Min(hullBefore, hullRequested) : 0;
            if (hullRequested > 0 && hullApplied < hullRequested)
                foreach (var receipt in receipts)
                    receipt.HullAppliedDamage *= hullApplied / hullRequested;
            SetStat(target, Hull, hullBefore - hullApplied);
            return new DamageResolution(0, armorApplied, equipmentApplied, hullApplied, 0, 0, receipts,
                destroyedEquipmentIndices.OrderBy(value => value).ToArray(), cockpitDestroyed);
        }

        private static void DeactivateDestroyedEquipment(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int equipmentIndex)
        {
            foreach (var state in entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
            {
                if (!string.Equals(state.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                        StringComparison.Ordinal) || state.OwnerIndex != equipmentIndex)
                    continue;
                state.Firing = false;
                state.TriggerPending = false;
                state.BurstRemaining = 0;
                state.Charging = false;
                state.Charged = false;
                state.Charge = 0;
                state.LockProgress = 0;
                state.LockTargetEntityIndex = -1;
            }
        }

        private static void EnsureArmorState(AetheriaRuntimeEntitySnapshotCommit entity, AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var armorGrid = FindGrid(entity, Armor);
            var maximumArmorGrid = FindGrid(entity, MaximumArmor);
            if (armorGrid != null && maximumArmorGrid != null) return;
            var hull = catalog?.FindItem(entity.HullItemKey ?? "");
            if (hull == null || hull.ShapeWidth <= 0 || hull.ShapeHeight <= 0 || hull.ShapeCells == null || hull.ShapeCells.Count == 0)
                return;
            var values = new double[hull.ShapeWidth * hull.ShapeHeight];
            foreach (var cell in hull.ShapeCells)
                if (cell.X >= 0 && cell.Y >= 0 && cell.X < hull.ShapeWidth && cell.Y < hull.ShapeHeight)
                    values[cell.Y * hull.ShapeWidth + cell.X] = Math.Max(0, hull.HullArmor);
            foreach (var hardpoint in hull.Hardpoints ?? Array.Empty<AetheriaRuntimeHardpoint>())
                foreach (var cell in hardpoint.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                {
                    var x = hardpoint.PositionX + cell.X;
                    var y = hardpoint.PositionY + cell.Y;
                    if (x >= 0 && y >= 0 && x < hull.ShapeWidth && y < hull.ShapeHeight)
                        values[y * hull.ShapeWidth + x] += Math.Max(0, hardpoint.Armor);
                }
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            if (armorGrid == null)
                grids.Add(new AetheriaRuntimeEntityStatGridCommit { Name = Armor, Width = hull.ShapeWidth, Height = hull.ShapeHeight, Values = values });
            if (maximumArmorGrid == null)
                grids.Add(new AetheriaRuntimeEntityStatGridCommit { Name = MaximumArmor, Width = hull.ShapeWidth, Height = hull.ShapeHeight, Values = values.ToArray() });
            entity.StatGrids = grids;
        }

        private static List<(int X, int Y)> ResolveDamageCells(AetheriaRuntimeEntitySnapshotCommit target,
            AetheriaRuntimeCatalogItem? hull, double impactX, double impactZ,
            AetheriaRuntimeEntitySnapshotCommit? source, double penetration, double spread, bool splash)
        {
            var result = new HashSet<(int X, int Y)>();
            if (hull == null || hull.ShapeCells == null || hull.ShapeCells.Count == 0) return result.ToList();
            var forward = Normalize(target.DirectionX, target.DirectionY);
            if (Math.Abs(forward.X) + Math.Abs(forward.Y) < 0.001) forward = (0, 1);
            var right = (X: forward.Y, Y: -forward.X);
            var occupied = new HashSet<(int X, int Y)>(hull.ShapeCells.Select(cell => (cell.X, cell.Y)));
            if (splash && source != null)
            {
                var incoming = Normalize(target.PositionX - source.PositionX, target.PositionZ - source.PositionZ);
                var localIncoming = (X: incoming.X * right.X + incoming.Y * right.Y,
                    Y: incoming.X * forward.X + incoming.Y * forward.Y);
                var centerX = hull.ShapeCells.Average(cell => cell.X);
                var centerY = hull.ShapeCells.Average(cell => cell.Y);
                foreach (var cell in hull.ShapeCells)
                    if ((cell.X - centerX) * localIncoming.X + (cell.Y - centerY) * localIncoming.Y < 0)
                        result.Add((cell.X, cell.Y));
                return result.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
            }
            var dx = impactX - target.PositionX;
            var dz = impactZ - target.PositionZ;
            var localX = dx * right.X + dz * right.Y;
            var localY = dx * forward.X + dz * forward.Y;
            var radius = string.Equals(target.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48.0 : 20.0;
            var schematicX = (localX / (radius * 2) + 0.5) * Math.Max(1, hull.ShapeWidth);
            var schematicY = (localY / (radius * 2) + 0.5) * Math.Max(1, hull.ShapeHeight);
            var first = hull.ShapeCells.OrderBy(cell =>
                (cell.X - schematicX) * (cell.X - schematicX) + (cell.Y - schematicY) * (cell.Y - schematicY)).First();
            result.Add((first.X, first.Y));
            var hitHardpoint = (hull.Hardpoints ?? Array.Empty<AetheriaRuntimeHardpoint>())
                .FirstOrDefault(hardpoint => (hardpoint.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                    .Any(cell => hardpoint.PositionX + cell.X == first.X && hardpoint.PositionY + cell.Y == first.Y));
            if (hitHardpoint != null)
                foreach (var cell in hitHardpoint.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                    if (occupied.Contains((hitHardpoint.PositionX + cell.X, hitHardpoint.PositionY + cell.Y)))
                        result.Add((hitHardpoint.PositionX + cell.X, hitHardpoint.PositionY + cell.Y));
            for (var i = 0; i < (int)Math.Round(Math.Max(0, spread)); i++)
            {
                var expanded = result.ToArray();
                foreach (var cell in expanded)
                    foreach (var step in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                        if (occupied.Contains((cell.X + step.Item1, cell.Y + step.Item2)))
                            result.Add((cell.X + step.Item1, cell.Y + step.Item2));
            }
            if (penetration > 0.5 && source != null)
            {
                var travel = Normalize(target.PositionX - source.PositionX, target.PositionZ - source.PositionZ);
                var penetrationX = travel.X * right.X + travel.Y * right.Y;
                var penetrationY = travel.X * forward.X + travel.Y * forward.Y;
                var pointX = first.X + 0.5;
                var pointY = first.Y + 0.5;
                for (var distance = 0.5; distance < penetration; distance += 0.5)
                {
                    pointX += penetrationX * 0.5;
                    pointY += penetrationY * 0.5;
                    var cell = ((int)Math.Floor(pointX), (int)Math.Floor(pointY));
                    if (!occupied.Contains(cell)) break;
                    result.Add(cell);
                }
            }
            return result.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
        }

        private static (AetheriaRuntimeLoadoutItemSlotCommit? Slot, int Index) FindEquipmentAt(
            AetheriaRuntimeEntitySnapshotCommit entity, AetheriaRuntimeCatalogSnapshot? catalog, int x, int y)
        {
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var index = 0; index < equipment.Count; index++)
            {
                var slot = equipment[index];
                var item = catalog?.FindItem(slot.Item?.ItemKey ?? "");
                if (item?.ShapeCells == null) continue;
                var rotation = AetheriaRuntimeEquipmentGridGeometry.ParseRotation(slot.Rotation);
                if (AetheriaRuntimeEquipmentGridGeometry.RotatedCells(item, rotation)
                    .Any(cell => slot.X + cell.X == x && slot.Y + cell.Y == y)) return (slot, index);
            }
            return (null, -1);
        }

        private static AetheriaRuntimeEntityStatGridCommit? FindGrid(AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        private static double ApplyGridDamage(AetheriaRuntimeEntityStatGridCommit? grid, int x, int y, double damage)
        {
            if (grid == null || x < 0 || y < 0 || x >= grid.Width || y >= grid.Height) return 0;
            var values = (grid.Values ?? Array.Empty<double>()).ToArray();
            var index = y * grid.Width + x;
            if (index < 0 || index >= values.Length) return 0;
            var applied = Math.Min(Math.Max(0, values[index]), Math.Max(0, damage));
            values[index] -= applied;
            grid.Values = values;
            return applied;
        }

        private static void RefreshShieldProjection(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var shield = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, "Shield")
                .FirstOrDefault();
            if (shield == null)
            {
                SetStat(entity, Shield, 0);
                return;
            }

            var energyUsage = Math.Max(0, shield.EvaluateStat(2));
            var availableEnergy = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(value => value != null && value.BehaviorKind == "Capacitor")
                .Sum(value => Math.Max(0, value.CapacitorCharge));
            SetStat(entity, Shield, energyUsage <= 0 ? 1 : availableEnergy / energyUsage);
        }

        private readonly struct DamageResolution
        {
            public static DamageResolution None { get; } = new DamageResolution(0, 0, 0, 0, 0, 0,
                Array.Empty<AetheriaRuntimeDamageCellCommit>(), Array.Empty<int>(), false);

            public DamageResolution(double shieldAbsorbedDamage, double armorAppliedDamage,
                double equipmentAppliedDamage, double hullAppliedDamage,
                double shieldEnergyConsumed, double shieldHeatGenerated,
                IReadOnlyList<AetheriaRuntimeDamageCellCommit> cells,
                IReadOnlyList<int>? destroyedEquipmentIndices = null,
                bool cockpitDestroyed = false)
            {
                ShieldAbsorbedDamage = shieldAbsorbedDamage;
                ArmorAppliedDamage = armorAppliedDamage;
                EquipmentAppliedDamage = equipmentAppliedDamage;
                HullAppliedDamage = hullAppliedDamage;
                ShieldEnergyConsumed = shieldEnergyConsumed;
                ShieldHeatGenerated = shieldHeatGenerated;
                Cells = cells;
                DestroyedEquipmentIndices = destroyedEquipmentIndices ?? Array.Empty<int>();
                CockpitDestroyed = cockpitDestroyed;
            }

            public double ShieldAbsorbedDamage { get; }
            public double ArmorAppliedDamage { get; }
            public double EquipmentAppliedDamage { get; }
            public double HullAppliedDamage { get; }
            public double ShieldEnergyConsumed { get; }
            public double ShieldHeatGenerated { get; }
            public IReadOnlyList<AetheriaRuntimeDamageCellCommit> Cells { get; }
            public IReadOnlyList<int> DestroyedEquipmentIndices { get; }
            public bool CockpitDestroyed { get; }
            public double TotalAppliedDamage =>
                ArmorAppliedDamage + EquipmentAppliedDamage + HullAppliedDamage;
        }

        private static bool IsAlive(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return entity.IsActive && GetStat(entity, Hull) > 0;
        }

        private static bool IsPlayerOwned(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Hostile(
            AetheriaRuntimeEntitySnapshotCommit left,
            AetheriaRuntimeEntitySnapshotCommit right)
        {
            return IsPlayerOwned(left) != IsPlayerOwned(right) &&
                (string.Equals(left.FactionKey, "raider", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(right.FactionKey, "raider", StringComparison.OrdinalIgnoreCase));
        }

        private static double DefaultHull(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? (IsPlayerOwned(entity) ? settings.PlayerStationHull : settings.HostileStationHull)
                : string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)
                    ? settings.RaiderEntityHull
                    : settings.PlayerEntityHull;
        }

        private static double DefaultShield(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? settings.StationShield
                : settings.EntityShield;
        }

        private static double GetStat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
        {
            var grid = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            return grid?.Values?.FirstOrDefault() ?? 0;
        }

        private static void SetStat(AetheriaRuntimeEntitySnapshotCommit entity, string name, double value)
        {
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var index = grids.FindIndex(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            var grid = Stat(name, value);
            if (index >= 0)
                grids[index] = grid;
            else
                grids.Add(grid);
            entity.StatGrids = grids;
        }

        private static AetheriaRuntimeEntityStatGridCommit Stat(string name, double value)
        {
            return new AetheriaRuntimeEntityStatGridCommit
            {
                Name = name,
                Width = 1,
                Height = 1,
                Values = new[] { value }
            };
        }

        private static double DistanceSq(
            AetheriaRuntimeEntitySnapshotCommit left,
            AetheriaRuntimeEntitySnapshotCommit right)
        {
            var dx = right.PositionX - left.PositionX;
            var dy = right.PositionZ - left.PositionZ;
            return dx * dx + dy * dy;
        }

        private static (double X, double Y) Normalize(double x, double y)
        {
            var magnitude = Math.Sqrt(x * x + y * y);
            return magnitude <= 0.0001 ? (0, 0) : (x / magnitude, y / magnitude);
        }

        private static void Face(AetheriaRuntimeEntitySnapshotCommit entity, double x, double y)
        {
            var direction = Normalize(x, y);
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) <= 0.0001)
                return;

            entity.DirectionX = direction.X;
            entity.DirectionY = direction.Y;
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        private static bool TryParseEntityIndex(string? entityKey, out int entityIndex)
        {
            entityIndex = -1;
            if (string.IsNullOrWhiteSpace(entityKey))
                return false;

            var marker = entityKey.LastIndexOf(".entity.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return false;

            var start = marker + ".entity.".Length;
            var end = start;
            while (end < entityKey.Length && char.IsDigit(entityKey[end]))
                end++;

            return end > start && int.TryParse(entityKey.Substring(start, end - start), out entityIndex);
        }
    }
}
