using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeEquipmentRotation
    {
        public static int QuarterTurns(string? value)
        {
            if (string.Equals(value, "CounterClockwise", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(value, "Half", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Reversed", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(value, "Clockwise", StringComparison.OrdinalIgnoreCase)) return 3;
            return 0;
        }

        public static (double X, double Y) RotateQuarter(double x, double y, int quarterTurns) =>
            quarterTurns switch
            {
                1 => (-y, x),
                2 => (-x, -y),
                3 => (y, -x),
                _ => (x, y)
            };
    }

    public sealed class AetheriaRuntimeEquippedBehavior
    {
        public AetheriaRuntimeEquippedBehavior(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            int equipmentIndex,
            int behaviorIndex,
            AetheriaRuntimeLoadoutItemSlotCommit slot,
            AetheriaRuntimeLoadoutItemCommit item,
            AetheriaRuntimeCatalogItem catalogItem,
            AetheriaRuntimeBehaviorPayload payload,
            AetheriaRuntimeBehaviorStateCommit state)
        {
            Entity = entity;
            Catalog = catalog;
            EquipmentIndex = equipmentIndex;
            BehaviorIndex = behaviorIndex;
            Slot = slot;
            Item = item;
            CatalogItem = catalogItem;
            Payload = payload;
            State = state;
        }

        public AetheriaRuntimeEntitySnapshotCommit Entity { get; }
        public AetheriaRuntimeCatalogSnapshot Catalog { get; }
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
            var baseline = AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                field?.Value,
                Item,
                Math.Max(0, Math.Min(1, thermalPerformance)));
            return AetheriaRuntimeBehaviorSimulation.Apply(
                Entity, Catalog, EquipmentIndex, Payload, fieldKey, baseline);
        }
    }

    public static class AetheriaRuntimeEquippedBehaviorQueries
    {
        public static IReadOnlyList<AetheriaRuntimeEquippedBehavior> Find(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string behaviorKind)
        {
            if (string.IsNullOrWhiteSpace(behaviorKind))
                return Array.Empty<AetheriaRuntimeEquippedBehavior>();

            return FindAll(entity, catalog)
                .Where(value => AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(
                    value.Payload.Kind, behaviorKind))
                .ToArray();
        }

        public static IReadOnlyList<AetheriaRuntimeEquippedBehavior> FindAll(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null || catalog == null)
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
                        !states.TryGetValue((equipmentIndex, behaviorIndex), out var state))
                    {
                        continue;
                    }
                    found.Add(new AetheriaRuntimeEquippedBehavior(
                        entity,
                        catalog,
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

        public static IReadOnlyList<AetheriaRuntimeEquippedBehavior> FindOperational(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string behaviorKind)
        {
            if (entity == null)
                return Array.Empty<AetheriaRuntimeEquippedBehavior>();

            var equipmentStates = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .Where(state => state != null)
                .ToDictionary(state => state.EquipmentIndex);
            return Find(entity, catalog, behaviorKind)
                .Where(value => value.Item.Enabled && value.Item.Durability > 0.01 &&
                    (!equipmentStates.TryGetValue(value.EquipmentIndex, out var state) || state.Online))
                .ToArray();
        }

        public static IReadOnlyList<AetheriaRuntimeEquippedBehavior> FindAllOperational(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null)
                return Array.Empty<AetheriaRuntimeEquippedBehavior>();

            var equipmentStates = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .Where(state => state != null)
                .ToDictionary(state => state.EquipmentIndex);
            return FindAll(entity, catalog)
                .Where(value => value.Item.Enabled && value.Item.Durability > 0.01 &&
                    (!equipmentStates.TryGetValue(value.EquipmentIndex, out var state) || state.Online))
                .ToArray();
        }

        public static IReadOnlyList<AetheriaRuntimeEquippedBehavior> FindExecuting(
            AetheriaRuntimeEntitySnapshotCommit? entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string behaviorKind) =>
            FindOperational(entity, catalog, behaviorKind)
                .Where(value => value.State.ChainReached)
                .ToArray();
    }
}
