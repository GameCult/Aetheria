using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal enum AetheriaRuntimePickupProximityResult
    {
        Ignored,
        Collected,
        RejectedCapacity
    }

    internal static class AetheriaRuntimePickupTransactions
    {
        public static AetheriaRuntimePickupProximityResult ApplyProximity(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            int pickupIndex,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (zone == null || entity == null || !entity.IsActive || pickupIndex < 0)
                return AetheriaRuntimePickupProximityResult.Ignored;

            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>()).ToList();
            var index = pickups.FindIndex(pickup => pickup != null && pickup.PickupIndex == pickupIndex);
            if (index < 0) return AetheriaRuntimePickupProximityResult.Ignored;
            var pickup = pickups[index];
            var quantity = Math.Max(1, pickup.Item?.Quantity ?? 1);
            if (pickup.AgeSeconds >= pickup.LifetimeSeconds ||
                pickup.Item == null)
                return AetheriaRuntimePickupProximityResult.Ignored;
            var cargoBayCount = entity.CargoBays?.Count ?? 0;
            var cargoIndex = Enumerable.Range(0, cargoBayCount)
                .Where(index => quantity <= AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(
                    entity, catalog, pickup.Item.ItemKey ?? "", index))
                .DefaultIfEmpty(-1)
                .First();
            if (cargoIndex < 0)
                return AetheriaRuntimePickupProximityResult.RejectedCapacity;
            var bays = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToList();
            while (bays.Count <= cargoIndex) bays.Add(new AetheriaRuntimeCargoBayLoadoutCommit());
            var slots = (bays[cargoIndex].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var existing = slots.FirstOrDefault(slot => string.Equals(slot?.Item?.ItemKey, pickup.Item.ItemKey, StringComparison.Ordinal));
            if (existing != null) existing.Item.Quantity += quantity;
            else slots.Add(new AetheriaRuntimeLoadoutItemSlotCommit { Item = pickup.Item });
            bays[cargoIndex].Items = slots; entity.CargoContents = bays;
            pickups.RemoveAt(index); zone.DroppedPickups = pickups;
            return AetheriaRuntimePickupProximityResult.Collected;
        }
    }
}
