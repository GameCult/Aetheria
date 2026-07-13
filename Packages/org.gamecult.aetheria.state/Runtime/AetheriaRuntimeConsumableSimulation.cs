using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeConsumableSimulation
    {
        public static void Step(
            AetheriaRuntimeRunCheckpointCommit run,
            IEnumerable<AetheriaRuntimeDaemonConsumableIntent> intents,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId,
            double deltaSeconds)
        {
            if (run == null || deltaSeconds <= 0)
                return;

            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                StepZone(run, zone, zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>(), intents, catalog, frameId, deltaSeconds);
        }

        public static void StepZone(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            IEnumerable<AetheriaRuntimeDaemonConsumableIntent> intents,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId,
            double deltaSeconds)
        {
            if (run == null || zone == null || deltaSeconds <= 0)
                return;

            var intentIndex = 0;
            foreach (var intent in intents ?? Enumerable.Empty<AetheriaRuntimeDaemonConsumableIntent>())
            {
                if (AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(intent?.ActorEntityKey ?? "", out var intentZone, out _) &&
                    intentZone == zone.ZoneIndex)
                    Activate(run, intent, catalog, frameId, intentIndex);
                intentIndex++;
            }

            foreach (var entity in entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;

                var active = (entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                    .Where(effect => effect != null)
                    .ToArray();
                for (var index = 0; index < active.Length; index++)
                {
                    ExecuteBehaviors(run, zone, entity, active[index], catalog, frameId, index, deltaSeconds);
                    active[index].RemainingDuration -= deltaSeconds;
                    if (active[index].RemainingDuration < 0)
                    {
                        AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
                        {
                            EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:consumable:{index}:expired",
                            Kind = "consumable.expired",
                            FrameId = frameId,
                            ZoneIndex = zone.ZoneIndex,
                            SourceEntityIndex = entity.EntityIndex,
                            ItemKey = active[index].ItemKey
                        });
                    }
                }
                entity.ActiveConsumables = active.Where(effect => effect.RemainingDuration >= 0).ToArray();
            }
        }

        private static void ExecuteBehaviors(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeActiveConsumableCommit effect,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId,
            int effectIndex,
            double deltaSeconds)
        {
            var item = catalog?.FindItem(effect.ItemKey);
            if (item == null)
            {
                BehaviorStopped(run, zone, entity, effect, frameId, effectIndex, -1, "missing-catalog-item");
                return;
            }

            var elapsed = effect.Duration <= 0
                ? 1
                : Math.Max(0, Math.Min(1, (effect.Duration - effect.RemainingDuration) / effect.Duration));
            var effectiveness = item.EffectivenessCurveKeys == null || item.EffectivenessCurveKeys.Count == 0
                ? 1
                : AetheriaRuntimeDaemonItemStatQueries.SampleCurve(item.EffectivenessCurveKeys, elapsed);
            var payloads = item.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>();
            for (var behaviorIndex = 0; behaviorIndex < payloads.Count; behaviorIndex++)
            {
                var payload = payloads[behaviorIndex];
                if (payload == null)
                    continue;
                if (ExecuteBehavior(entity, effect, payload, catalog, effectiveness, deltaSeconds, out var reason))
                    continue;

                BehaviorStopped(run, zone, entity, effect, frameId, effectIndex, behaviorIndex, reason);
                break;
            }
        }

        private static bool ExecuteBehavior(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeActiveConsumableCommit effect,
            AetheriaRuntimeBehaviorPayload payload,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double effectiveness,
            double deltaSeconds,
            out string reason)
        {
            reason = "";
            switch (payload.Kind)
            {
                case "EnergyDraw":
                {
                    var demand = Evaluate(payload, 1, effect, effectiveness) * (ReadBool(payload, 2) ? deltaSeconds : 1);
                    if (AetheriaRuntimeEnergySimulation.TryConsume(entity, catalog, demand))
                        return true;
                    reason = "insufficient-energy";
                    return false;
                }
                case "Heat":
                    // The fossil only added heat to equipped items; consumable heat was intentionally a no-op.
                    return true;
                case "ItemUsage":
                {
                    var itemKey = ReadItemKey(payload, 1);
                    if (!string.IsNullOrWhiteSpace(itemKey) &&
                        AetheriaRuntimeCargoTransactions.TryFind(entity, itemKey, out var cargoIndex, out var x, out var y) &&
                        AetheriaRuntimeCargoTransactions.TryRemoveQuantity(entity, cargoIndex, itemKey, x, y, 1, out _))
                        return true;
                    reason = "missing-required-item";
                    return false;
                }
                case "Wear":
                    return true;
                default:
                    reason = "unsupported-behavior:" + (payload.Kind ?? "");
                    return false;
            }
        }

        private static double Evaluate(
            AetheriaRuntimeBehaviorPayload payload,
            int fieldKey,
            AetheriaRuntimeActiveConsumableCommit effect,
            double effectiveness) =>
            AetheriaRuntimeDaemonItemStatQueries.EvaluateConsumablePerformanceStat(
                Field(payload, fieldKey)?.Value,
                effect.Quality,
                effectiveness);

        private static bool ReadBool(AetheriaRuntimeBehaviorPayload payload, int fieldKey) =>
            Field(payload, fieldKey)?.Value?.BoolValue ?? false;

        private static string ReadItemKey(AetheriaRuntimeBehaviorPayload payload, int fieldKey)
        {
            var value = Field(payload, fieldKey)?.Value;
            return value?.ItemKeyValue ?? value?.StringValue ?? "";
        }

        private static AetheriaRuntimeBehaviorField? Field(AetheriaRuntimeBehaviorPayload payload, int fieldKey) =>
            (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == fieldKey);

        private static void BehaviorStopped(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeActiveConsumableCommit effect,
            long frameId,
            int effectIndex,
            int behaviorIndex,
            string reason)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:consumable:{effectIndex}:behavior:{behaviorIndex}:stopped",
                Kind = "consumable.behavior.stopped",
                FrameId = frameId,
                ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = entity.EntityIndex,
                ItemKey = effect.ItemKey,
                ScalarValue = behaviorIndex,
                Reason = reason
            });
        }

        public static bool CanActivate(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string itemKey)
        {
            var item = catalog?.FindItem(itemKey);
            return entity != null && item != null &&
                string.Equals(item.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal) &&
                item.Duration > 0 &&
                (item.Stackable || !(entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                    .Any(active => active != null && string.Equals(active.ItemKey, itemKey, StringComparison.Ordinal))) &&
                AetheriaRuntimeCargoTransactions.TryFind(entity, itemKey, out _, out _, out _);
        }

        private static void Activate(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonConsumableIntent? intent,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId,
            int intentIndex)
        {
            var itemKey = intent?.ItemKey ?? "";
            if (!TryResolveActor(run, intent?.ActorEntityKey, out var zone, out var entity))
            {
                Refuse(run, frameId, intentIndex, zone, entity, itemKey, "actor-unavailable");
                return;
            }

            var item = catalog?.FindItem(itemKey);
            if (item == null || !string.Equals(item.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal))
            {
                Refuse(run, frameId, intentIndex, zone, entity, itemKey, "not-consumable");
                return;
            }
            if (item.Duration <= 0)
            {
                Refuse(run, frameId, intentIndex, zone, entity, itemKey, "invalid-duration");
                return;
            }
            if (!item.Stackable && (entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                .Any(active => active != null && string.Equals(active.ItemKey, itemKey, StringComparison.Ordinal)))
            {
                Refuse(run, frameId, intentIndex, zone, entity, itemKey, "already-active");
                return;
            }
            if (!AetheriaRuntimeCargoTransactions.TryFind(entity, itemKey, out var cargoIndex, out var x, out var y) ||
                !AetheriaRuntimeCargoTransactions.TryRemoveQuantity(entity, cargoIndex, itemKey, x, y, 1, out var consumed))
            {
                Refuse(run, frameId, intentIndex, zone, entity, itemKey, "missing-cargo");
                return;
            }

            entity.ActiveConsumables = (entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                .Where(active => active != null)
                .Append(new AetheriaRuntimeActiveConsumableCommit
                {
                    ItemKey = item.ItemKey,
                    Quality = consumed.Item.Quality,
                    Duration = item.Duration,
                    RemainingDuration = item.Duration
                })
                .ToArray();
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:consumable:{intentIndex}:activated",
                Kind = "consumable.activated",
                FrameId = frameId,
                ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = entity.EntityIndex,
                ItemKey = item.ItemKey,
                ScalarValue = consumed.Item.Quality,
                AuxiliaryValue = item.Duration
            });
        }

        private static bool TryResolveActor(
            AetheriaRuntimeRunCheckpointCommit run,
            string? actorEntityKey,
            out AetheriaRuntimeZoneSnapshotCommit? zone,
            out AetheriaRuntimeEntitySnapshotCommit? entity)
        {
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(actorEntityKey ?? "", out var actorZoneIndex, out var actorEntityIndex);
            foreach (var candidateZone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            foreach (var candidate in candidateZone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                var canonicalKey = candidate == null
                    ? ""
                    : run.EntityRecordKey(candidateZone.ZoneIndex, candidate.EntityIndex);
                if (candidate != null &&
                    (string.Equals(canonicalKey, actorEntityKey, StringComparison.Ordinal) ||
                     (candidateZone.ZoneIndex == actorZoneIndex && candidate.EntityIndex == actorEntityIndex)))
                {
                    zone = candidateZone;
                    entity = candidate;
                    return true;
                }
            }
            zone = null;
            entity = null;
            return false;
        }

        private static void Refuse(
            AetheriaRuntimeRunCheckpointCommit run,
            long frameId,
            int intentIndex,
            AetheriaRuntimeZoneSnapshotCommit? zone,
            AetheriaRuntimeEntitySnapshotCommit? entity,
            string itemKey,
            string reason)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"frame:{frameId}:consumable:{intentIndex}:refused",
                Kind = "consumable.activation.refused",
                FrameId = frameId,
                ZoneIndex = zone?.ZoneIndex ?? -1,
                SourceEntityIndex = entity?.EntityIndex ?? -1,
                ItemKey = itemKey,
                Reason = reason
            });
        }
    }
}
