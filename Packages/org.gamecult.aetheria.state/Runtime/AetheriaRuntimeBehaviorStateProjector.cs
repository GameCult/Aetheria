using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeBehaviorStateProjector
    {
        public const string EquipmentOwnerKind = "equipment";

        public static void EnsureEquipmentBehaviorStates(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null)
                return;

            var existing = entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
            var projected = CreateEquipmentBehaviorStates(entity.Equipment, catalog);
            if (projected.Length == 0)
            {
                entity.BehaviorStates = existing
                    .Where(state => state != null && !string.Equals(state.OwnerKind, EquipmentOwnerKind, StringComparison.Ordinal))
                    .ToArray();
                return;
            }

            var existingByKey = existing
                .Where(state => state != null)
                .GroupBy(StateKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var nonEquipmentStates = existing
                .Where(state => state != null && !string.Equals(state.OwnerKind, EquipmentOwnerKind, StringComparison.Ordinal));
            var equipmentStates = projected
                .Select(state => existingByKey.TryGetValue(StateKey(state), out var current) ? current : state);
            entity.BehaviorStates = nonEquipmentStates
                .Concat(equipmentStates)
                .OrderBy(state => state.OwnerKind, StringComparer.Ordinal)
                .ThenBy(state => state.OwnerIndex)
                .ThenBy(state => state.BehaviorIndex)
                .ToArray();
        }

        public static AetheriaRuntimeBehaviorStateCommit[] CreateEquipmentBehaviorStates(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? equipment,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            catalog ??= new AetheriaRuntimeCatalogSnapshot();
            return (equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .SelectMany((slot, equipmentIndex) => CreateEquipmentBehaviorStates(slot?.Item, equipmentIndex, catalog))
                .ToArray();
        }

        private static IEnumerable<AetheriaRuntimeBehaviorStateCommit> CreateEquipmentBehaviorStates(
            AetheriaRuntimeLoadoutItemCommit? item,
            int equipmentIndex,
            AetheriaRuntimeCatalogSnapshot catalog)
        {
            if (item == null || equipmentIndex < 0)
                yield break;

            var typedItem = catalog.FindItem(item.ItemKey ?? "");
            var payloads = typedItem?.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>();
            for (var behaviorIndex = 0; behaviorIndex < payloads.Count; behaviorIndex++)
            {
                var payload = payloads[behaviorIndex];
                if (payload == null || string.IsNullOrWhiteSpace(payload.Kind))
                    continue;

                var state = new AetheriaRuntimeBehaviorStateCommit
                {
                    OwnerKind = EquipmentOwnerKind,
                    OwnerIndex = equipmentIndex,
                    BehaviorIndex = behaviorIndex,
                    BehaviorKind = payload.Kind
                };
                if (string.Equals(payload.Kind, "Thermotoggle", StringComparison.Ordinal))
                    state.ThermotoggleTargetTemperature = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                        .FirstOrDefault(field => field != null && field.Key == 1)?.Value?.NumberValue ?? 0;
                yield return state;
            }
        }

        private static string StateKey(AetheriaRuntimeBehaviorStateCommit? state)
        {
            if (state == null)
                return "";

            return string.Join(
                "/",
                state.OwnerKind ?? "",
                state.OwnerIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                state.BehaviorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                state.BehaviorKind ?? "");
        }
    }
}
