using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeEquippedBehavior
    {
        public AetheriaRuntimeEquippedBehavior(
            int equipmentIndex,
            int behaviorIndex,
            AetheriaRuntimeLoadoutItemSlotCommit slot,
            AetheriaRuntimeLoadoutItemCommit item,
            AetheriaRuntimeCatalogItem catalogItem,
            AetheriaRuntimeBehaviorPayload payload,
            AetheriaRuntimeBehaviorStateCommit state)
        {
            EquipmentIndex = equipmentIndex;
            BehaviorIndex = behaviorIndex;
            Slot = slot;
            Item = item;
            CatalogItem = catalogItem;
            Payload = payload;
            State = state;
        }

        public int EquipmentIndex { get; }
        public int BehaviorIndex { get; }
        public AetheriaRuntimeLoadoutItemSlotCommit Slot { get; }
        public AetheriaRuntimeLoadoutItemCommit Item { get; }
        public AetheriaRuntimeCatalogItem CatalogItem { get; }
        public AetheriaRuntimeBehaviorPayload Payload { get; }
        public AetheriaRuntimeBehaviorStateCommit State { get; }

        public double EvaluateStat(int fieldKey, double thermalPerformance = 1.0)
        {
            var field = (Payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(candidate => candidate != null && candidate.Key == fieldKey);
            return AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                field?.Value,
                Item,
                Math.Max(0, Math.Min(1, thermalPerformance)));
        }
    }

    public static class AetheriaRuntimeEquippedBehaviorQueries
    {
        public static IReadOnlyList<AetheriaRuntimeEquippedBehavior> Find(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string behaviorKind)
        {
            if (entity == null || catalog == null || string.IsNullOrWhiteSpace(behaviorKind))
                return Array.Empty<AetheriaRuntimeEquippedBehavior>();

            AetheriaRuntimeBehaviorStateProjector.EnsureEquipmentBehaviorStates(entity, catalog);
            var states = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(state => state != null && string.Equals(state.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal))
                .ToDictionary(state => (state.OwnerIndex, state.BehaviorIndex));
            var found = new List<AetheriaRuntimeEquippedBehavior>();
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var equipmentIndex = 0; equipmentIndex < equipment.Count; equipmentIndex++)
            {
                var item = equipment[equipmentIndex]?.Item;
                var catalogItem = catalog.FindItem(item?.ItemKey ?? "");
                if (item == null || catalogItem == null)
                    continue;

                var payloads = catalogItem.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>();
                for (var behaviorIndex = 0; behaviorIndex < payloads.Count; behaviorIndex++)
                {
                    var payload = payloads[behaviorIndex];
                    if (payload == null ||
                        !AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(payload.Kind, behaviorKind) ||
                        !states.TryGetValue((equipmentIndex, behaviorIndex), out var state))
                    {
                        continue;
                    }
                    found.Add(new AetheriaRuntimeEquippedBehavior(
                        equipmentIndex,
                        behaviorIndex,
                        equipment[equipmentIndex],
                        item,
                        catalogItem,
                        payload,
                        state));
                }
            }
            return found;
        }
    }
}
