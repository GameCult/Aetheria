using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeBehaviorSimulation
    {
        public static void Step(
            int zoneIndex,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            IEnumerable<AetheriaRuntimeDaemonBehaviorIntent>? intents,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double deltaSeconds)
        {
            if (catalog == null || deltaSeconds <= 0)
                return;

            var intentArray = (intents ?? Enumerable.Empty<AetheriaRuntimeDaemonBehaviorIntent>()).ToArray();
            foreach (var entity in entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;

                AetheriaRuntimeBehaviorStateProjector.EnsureEquipmentBehaviorStates(entity, catalog);
                ApplyControlIntents(zoneIndex, entity, intentArray);
                ExecuteEquipmentChains(entity, catalog, deltaSeconds);
                ProjectCooldownProgress(entity);
            }
        }

        public static double Apply(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            int targetEquipmentIndex,
            AetheriaRuntimeBehaviorPayload targetBehavior,
            int targetFieldKey,
            double baseline)
        {
            var targetStatName = StatName(targetBehavior?.Kind, targetFieldKey);
            if (entity == null || catalog == null || targetEquipmentIndex < 0 ||
                targetBehavior == null || string.IsNullOrWhiteSpace(targetStatName))
                return baseline;

            double multiplier = 1;
            double constant = 0;
            foreach (var modifier in ActiveEquippedModifiers(entity, catalog)
                .Concat(ActiveConsumableModifiers(entity, catalog)))
            {
                if (!Targets(modifier.Payload, entity, catalog, targetEquipmentIndex, targetBehavior, targetStatName))
                    continue;
                if (IsMultiplier(modifier.Payload))
                    multiplier *= modifier.Value;
                else
                    constant += modifier.Value;
            }
            return baseline * multiplier + constant;
        }

        public static void ReportSpecializedResult(
            AetheriaRuntimeEquippedBehavior behavior,
            bool succeeded)
        {
            if (behavior?.State == null || !behavior.State.ChainReached)
                return;
            behavior.State.ChainSucceeded = succeeded;
        }

        public static void CompleteDeferredChains(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double deltaSeconds)
        {
            if (entity == null || catalog == null || deltaSeconds <= 0)
                return;

            var states = EquipmentStates(entity);
            var online = OnlineStates(entity);
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var equipmentIndex = 0; equipmentIndex < equipment.Count; equipmentIndex++)
            {
                var item = catalog.FindItem(equipment[equipmentIndex]?.Item?.ItemKey ?? "");
                var payloads = item?.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>();
                foreach (var group in Indexed(payloads).GroupBy(value => value.Payload.Group).OrderBy(value => value.Key))
                {
                    var entries = group.ToArray();
                    var deferredOffset = Array.FindIndex(entries, entry =>
                        states.TryGetValue((equipmentIndex, entry.Index), out var state) &&
                        state.ChainReached && IsDeferred(entry.Payload.Kind));
                    if (deferredOffset < 0)
                        continue;
                    var deferredState = states[(equipmentIndex, entries[deferredOffset].Index)];
                    if (!deferredState.ChainSucceeded || deferredState.ChainCompleted)
                        continue;
                    deferredState.ChainCompleted = true;

                    foreach (var entry in entries.Skip(deferredOffset + 1))
                    {
                        if (!states.TryGetValue((equipmentIndex, entry.Index), out var state))
                            break;
                        state.ChainReached = true;
                        if (IsDeferred(entry.Payload.Kind) ||
                            !ExecuteInline(entity, catalog, online, equipmentIndex, entry, state, deltaSeconds))
                            break;
                    }
                }
                ProjectModifierState(entity, catalog, equipmentIndex, payloads, states);
            }
            ProjectCooldownProgress(entity);
        }

        public static int CountTargets(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimeBehaviorPayload modifier)
        {
            var count = 0;
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var equipmentIndex = 0; equipmentIndex < equipment.Count; equipmentIndex++)
            {
                var item = catalog.FindItem(equipment[equipmentIndex]?.Item?.ItemKey ?? "");
                foreach (var behavior in item?.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
                {
                    if (behavior == null)
                        continue;
                    foreach (var stat in PerformanceStats(behavior.Kind))
                        if (Targets(modifier, entity, catalog, equipmentIndex, behavior, stat.Name))
                            count++;
                }
            }
            return count;
        }

        private static void ApplyControlIntents(
            int zoneIndex,
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyList<AetheriaRuntimeDaemonBehaviorIntent> intents)
        {
            var states = entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
            foreach (var intent in intents.Where(value => value != null &&
                ((AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(value.ActorEntityKey ?? "", out var actorZone, out var actorEntity) &&
                  actorZone == zoneIndex && actorEntity == entity.EntityIndex) ||
                 string.Equals(value.ActorEntityKey, entity.EntityId, StringComparison.Ordinal))))
            {
                var state = states.FirstOrDefault(value => value != null &&
                    string.Equals(value.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal) &&
                    value.OwnerIndex == intent.EquipmentIndex && value.BehaviorIndex == intent.BehaviorIndex);
                if (state == null)
                    continue;
                if (string.Equals(state.BehaviorKind, "Switch", StringComparison.Ordinal))
                    state.SwitchActivated = intent.Active;
                else if (string.Equals(state.BehaviorKind, "Trigger", StringComparison.Ordinal) && intent.Active)
                    state.TriggerPulled = true;
            }
        }

        private static void ExecuteEquipmentChains(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            double deltaSeconds)
        {
            var states = EquipmentStates(entity);
            foreach (var state in states.Values)
            {
                state.ChainReached = false;
                state.ChainSucceeded = false;
                state.ChainCompleted = false;
            }
            var online = OnlineStates(entity);
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var equipmentIndex = 0; equipmentIndex < equipment.Count; equipmentIndex++)
            {
                var equipped = equipment[equipmentIndex]?.Item;
                var item = catalog.FindItem(equipped?.ItemKey ?? "");
                var operational = equipped != null && equipped.Enabled && equipped.Durability > 0.01 &&
                    (!online.TryGetValue(equipmentIndex, out var equipmentState) || equipmentState.Online);
                var payloads = item?.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>();

                foreach (var entry in Indexed(payloads))
                    if (entry.Payload != null && string.Equals(entry.Payload.Kind, "StatModifier", StringComparison.Ordinal) &&
                        states.TryGetValue((equipmentIndex, entry.Index), out var modifierState))
                        modifierState.StatModifierExecuted = false;

                if (operational)
                foreach (var group in Indexed(payloads).GroupBy(value => value.Payload.Group).OrderBy(value => value.Key))
                foreach (var entry in group)
                {
                    if (!states.TryGetValue((equipmentIndex, entry.Index), out var state))
                        break;
                    state.ChainReached = true;
                    if (IsDeferred(entry.Payload.Kind) ||
                        !ExecuteInline(entity, catalog, online, equipmentIndex, entry, state, deltaSeconds))
                        break;
                }

                ProjectModifierState(entity, catalog, equipmentIndex, payloads, states);

                foreach (var entry in Indexed(payloads))
                {
                    if (entry.Payload == null || !string.Equals(entry.Payload.Kind, "Cooldown", StringComparison.Ordinal) ||
                        !states.TryGetValue((equipmentIndex, entry.Index), out var state))
                        continue;
                    var duration = Evaluate(entity, catalog, equipmentIndex, entry.Index, entry.Payload, state, 1);
                    state.CooldownProgress -= deltaSeconds / Math.Max(double.Epsilon, duration);
                }
            }
        }

        private static bool ExecuteInline(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            IReadOnlyDictionary<int, AetheriaRuntimeEquipmentStateCommit> online,
            int equipmentIndex,
            (AetheriaRuntimeBehaviorPayload Payload, int Index) entry,
            AetheriaRuntimeBehaviorStateCommit state,
            double deltaSeconds)
        {
            if (string.Equals(entry.Payload.Kind, "Switch", StringComparison.Ordinal) && !state.SwitchActivated)
                return false;
            if (string.Equals(entry.Payload.Kind, "Trigger", StringComparison.Ordinal))
            {
                if (!state.TriggerPulled)
                    return false;
                state.TriggerPulled = false;
            }
            if (string.Equals(entry.Payload.Kind, "Thermotoggle", StringComparison.Ordinal))
            {
                var temperature = AetheriaRuntimeThermalSimulation.EquipmentTemperature(entity, catalog, equipmentIndex);
                if (!((temperature < state.ThermotoggleTargetTemperature) ^ ReadBool(entry.Payload, 2)))
                    return false;
            }
            if (string.Equals(entry.Payload.Kind, "Cooldown", StringComparison.Ordinal))
            {
                if (state.CooldownProgress >= 0)
                    return false;
                state.CooldownProgress = 1;
            }
            if (string.Equals(entry.Payload.Kind, "EnergyDraw", StringComparison.Ordinal))
            {
                var demand = Evaluate(entity, catalog, equipmentIndex, entry.Index, entry.Payload, state, 1) *
                    (ReadBool(entry.Payload, 2) ? deltaSeconds : 1);
                if (!AetheriaRuntimeEnergySimulation.TryConsume(entity, catalog, demand))
                    return false;
            }
            if (string.Equals(entry.Payload.Kind, "Heat", StringComparison.Ordinal))
            {
                var heat = Evaluate(entity, catalog, equipmentIndex, entry.Index, entry.Payload, state, 1) *
                    (ReadBool(entry.Payload, 2) ? deltaSeconds : 1);
                AetheriaRuntimeThermalSimulation.AddHeatToEquipment(entity, catalog, equipmentIndex, heat);
            }
            if (string.Equals(entry.Payload.Kind, "ItemUsage", StringComparison.Ordinal))
            {
                var itemKey = ReadItemKey(entry.Payload, 1);
                if (string.IsNullOrWhiteSpace(itemKey) ||
                    !AetheriaRuntimeCargoTransactions.TryFind(entity, itemKey, out var cargoIndex, out var x, out var y) ||
                    !AetheriaRuntimeCargoTransactions.TryRemoveQuantity(entity, cargoIndex, itemKey, x, y, 1, out _))
                    return false;
            }
            if (string.Equals(entry.Payload.Kind, "Wear", StringComparison.Ordinal))
            {
                var wear = online.TryGetValue(equipmentIndex, out var wearState) ? wearState.Wear : 0;
                AetheriaRuntimeThermalSimulation.ApplyWear(
                    entity, equipmentIndex, wear * (ReadBool(entry.Payload, 1, true) ? deltaSeconds : 1));
            }
            if (string.Equals(entry.Payload.Kind, "StatModifier", StringComparison.Ordinal))
                state.StatModifierExecuted = true;
            return true;
        }

        private static bool IsDeferred(string? kind) =>
            string.Equals(kind, AetheriaRuntimeBehaviorKinds.Thruster, StringComparison.Ordinal) ||
            string.Equals(kind, AetheriaRuntimeBehaviorKinds.AetherDrive, StringComparison.Ordinal) ||
            string.Equals(kind, "Radiator", StringComparison.Ordinal) ||
            string.Equals(kind, "MiningTool", StringComparison.Ordinal) ||
            string.Equals(kind, "ResourceScanner", StringComparison.Ordinal);

        private static IEnumerable<(AetheriaRuntimeBehaviorPayload Payload, int Index)> Indexed(
            IReadOnlyList<AetheriaRuntimeBehaviorPayload> payloads) =>
            payloads.Select((payload, index) => (Payload: payload, Index: index))
                .Where(value => value.Payload != null);

        private static Dictionary<(int OwnerIndex, int BehaviorIndex), AetheriaRuntimeBehaviorStateCommit>
            EquipmentStates(AetheriaRuntimeEntitySnapshotCommit entity) =>
            (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
            .Where(value => value != null && string.Equals(value.OwnerKind,
                AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal))
            .ToDictionary(value => (value.OwnerIndex, value.BehaviorIndex));

        private static Dictionary<int, AetheriaRuntimeEquipmentStateCommit> OnlineStates(
            AetheriaRuntimeEntitySnapshotCommit entity) =>
            (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
            .Where(value => value != null).ToDictionary(value => value.EquipmentIndex);

        private static void ProjectModifierState(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            int equipmentIndex,
            IReadOnlyList<AetheriaRuntimeBehaviorPayload> payloads,
            IReadOnlyDictionary<(int OwnerIndex, int BehaviorIndex), AetheriaRuntimeBehaviorStateCommit> states)
        {
            foreach (var entry in Indexed(payloads))
            {
                if (!string.Equals(entry.Payload.Kind, "StatModifier", StringComparison.Ordinal) ||
                    !states.TryGetValue((equipmentIndex, entry.Index), out var state))
                    continue;
                state.StatModifierApplied = state.StatModifierExecuted;
                state.StatModifierTargetStatCount = CountTargets(entity, catalog, entry.Payload);
            }
        }

        private static double Evaluate(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            int equipmentIndex,
            int behaviorIndex,
            AetheriaRuntimeBehaviorPayload payload,
            AetheriaRuntimeBehaviorStateCommit state,
            int fieldKey)
        {
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (equipmentIndex < 0 || equipmentIndex >= equipment.Count || equipment[equipmentIndex]?.Item == null)
                return 0;
            var item = equipment[equipmentIndex].Item;
            var typed = catalog.FindItem(item.ItemKey ?? "");
            if (typed == null)
                return 0;
            var behavior = new AetheriaRuntimeEquippedBehavior(
                entity, catalog, equipmentIndex, behaviorIndex, equipment[equipmentIndex], item, typed, payload, state);
            var thermal = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .FirstOrDefault(value => value?.EquipmentIndex == equipmentIndex)?.ThermalPerformance ?? 1;
            return behavior.EvaluateStat(fieldKey, thermal);
        }

        private static void ProjectCooldownProgress(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            var retained = (entity.BehaviorProgress ?? Array.Empty<AetheriaRuntimeBehaviorProgressCommit>())
                .Where(value => value != null &&
                    !(string.Equals(value.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal) &&
                      string.Equals(value.BehaviorKind, "Cooldown", StringComparison.Ordinal)));
            var cooldowns = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(value => value != null &&
                    string.Equals(value.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal) &&
                    string.Equals(value.BehaviorKind, "Cooldown", StringComparison.Ordinal))
                .Select(value => new AetheriaRuntimeBehaviorProgressCommit
                {
                    OwnerKind = value.OwnerKind,
                    OwnerIndex = value.OwnerIndex,
                    BehaviorIndex = value.BehaviorIndex,
                    BehaviorKind = value.BehaviorKind,
                    Progress = Math.Max(0, Math.Min(1, value.CooldownProgress))
                });
            entity.BehaviorProgress = retained.Concat(cooldowns)
                .OrderBy(value => value.OwnerKind, StringComparer.Ordinal)
                .ThenBy(value => value.OwnerIndex)
                .ThenBy(value => value.BehaviorIndex)
                .ToArray();
        }

        private static IEnumerable<ModifierValue> ActiveEquippedModifiers(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog)
        {
            foreach (var behavior in AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, "StatModifier"))
            {
                if (!behavior.State.StatModifierApplied)
                    continue;
                var value = Field(behavior.Payload, 2)?.Value;
                var thermal = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                    .FirstOrDefault(state => state?.EquipmentIndex == behavior.EquipmentIndex)?.ThermalPerformance ?? 1;
                yield return new ModifierValue(behavior.Payload,
                    AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(value, behavior.Item, thermal));
            }
        }

        private static IEnumerable<ModifierValue> ActiveConsumableModifiers(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog)
        {
            foreach (var effect in entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
            {
                var item = catalog.FindItem(effect?.ItemKey ?? "");
                if (effect == null || item == null)
                    continue;
                var elapsed = effect.Duration <= 0 ? 1 : Math.Max(0, Math.Min(1,
                    (effect.Duration - effect.RemainingDuration) / effect.Duration));
                var effectiveness = item.EffectivenessCurveKeys == null || item.EffectivenessCurveKeys.Count == 0
                    ? 1 : AetheriaRuntimeDaemonItemStatQueries.SampleCurve(item.EffectivenessCurveKeys, elapsed);
                foreach (var entry in (item.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
                    .Select((payload, index) => (Payload: payload, Index: index)))
                {
                    var state = (effect.BehaviorStates ?? Array.Empty<AetheriaRuntimeConsumableBehaviorStateCommit>())
                        .FirstOrDefault(value => value?.BehaviorIndex == entry.Index);
                    if (entry.Payload != null && string.Equals(entry.Payload.Kind, "StatModifier", StringComparison.Ordinal) &&
                        state?.StatModifierApplied == true)
                        yield return new ModifierValue(entry.Payload,
                            AetheriaRuntimeDaemonItemStatQueries.EvaluateConsumablePerformanceStat(
                                Field(entry.Payload, 2)?.Value, effect.Quality, effectiveness));
                }
            }
        }

        private static bool Targets(
            AetheriaRuntimeBehaviorPayload modifier,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            int targetEquipmentIndex,
            AetheriaRuntimeBehaviorPayload targetBehavior,
            string targetStatName)
        {
            var reference = Field(modifier, 1)?.Value;
            var expectedKind = NormalizeKind(ChildString(reference, 1));
            if (!AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(targetBehavior?.Kind ?? "", expectedKind) ||
                !string.Equals(ChildString(reference, 2), targetStatName, StringComparison.Ordinal))
                return false;
            var requiredKind = NormalizeKind(ReadString(Field(modifier, 4)?.Value));
            if (string.IsNullOrWhiteSpace(requiredKind))
                return true;
            var targetItemKey = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .ElementAtOrDefault(targetEquipmentIndex)?.Item?.ItemKey ?? "";
            return (catalog.FindItem(targetItemKey)?.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
                .Any(value => AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(value?.Kind ?? "", requiredKind));
        }

        private static bool IsMultiplier(AetheriaRuntimeBehaviorPayload payload)
        {
            var value = Field(payload, 3)?.Value;
            return string.Equals(ReadString(value), "Multiplier", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(ReadString(value)) && Math.Round(value?.NumberValue ?? 0) == 1);
        }

        private static string NormalizeKind(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.EndsWith("Data", StringComparison.Ordinal)
                ? value.Substring(0, value.Length - 4) : value ?? "";

        private static string StatName(string? behaviorKind, int fieldKey) =>
            PerformanceStats(behaviorKind).FirstOrDefault(value => value.Key == fieldKey)?.Name ?? "";

        private static IEnumerable<AetheriaRuntimeBehaviorFieldMetadata> PerformanceStats(string? behaviorKind) =>
            AetheriaRuntimeBehaviorMetadataCatalog.Get(behaviorKind ?? "")?.DisplayFields?
                .Where(value => value.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat)
            ?? Enumerable.Empty<AetheriaRuntimeBehaviorFieldMetadata>();

        private static AetheriaRuntimeBehaviorField? Field(AetheriaRuntimeBehaviorPayload payload, int key) =>
            (payload?.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>()).FirstOrDefault(value => value?.Key == key);

        private static bool ReadBool(AetheriaRuntimeBehaviorPayload payload, int key, bool fallback = false)
        {
            var field = Field(payload, key);
            return field == null ? fallback : field.Value?.BoolValue ?? fallback;
        }

        private static string ReadItemKey(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            var value = Field(payload, key)?.Value;
            return !string.IsNullOrWhiteSpace(value?.ItemKeyValue)
                ? value.ItemKeyValue
                : ReadString(value);
        }

        private static string ChildString(AetheriaRuntimeBehaviorValue? value, int index) =>
            value != null && value.Children != null && value.Children.Count > index
                ? ReadString(value.Children[index]) : "";

        private static string ReadString(AetheriaRuntimeBehaviorValue? value) =>
            !string.IsNullOrWhiteSpace(value?.StringValue) ? value.StringValue :
            !string.IsNullOrWhiteSpace(value?.LegacyIdValue) ? value.LegacyIdValue :
            value?.ItemKeyValue ?? "";

        private readonly struct ModifierValue
        {
            public ModifierValue(AetheriaRuntimeBehaviorPayload payload, double value)
            {
                Payload = payload;
                Value = value;
            }

            public AetheriaRuntimeBehaviorPayload Payload { get; }
            public double Value { get; }
        }
    }
}
