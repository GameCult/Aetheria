using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimePickupTransactions
    {
        public static bool TryCollect(AetheriaRuntimeZoneSnapshotCommit zone, AetheriaRuntimeEntitySnapshotCommit entity,
            int pickupIndex, AetheriaRuntimeCatalogSnapshot? catalog, bool requireRange = true,
            double collectionRange = 25)
        {
            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>()).ToList();
            var index = pickups.FindIndex(pickup => pickup != null && pickup.PickupIndex == pickupIndex);
            if (index < 0) return false;
            var pickup = pickups[index];
            var quantity = Math.Max(1, pickup.Item?.Quantity ?? 1);
            if (pickup.AgeSeconds >= pickup.LifetimeSeconds ||
                (requireRange && Math.Pow(pickup.PositionX - entity.PositionX, 2) + Math.Pow(pickup.PositionZ - entity.PositionZ, 2) > collectionRange * collectionRange) ||
                quantity > AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(entity, catalog, pickup.Item?.ItemKey ?? ""))
                return false;
            var bays = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToList();
            if (bays.Count == 0) bays.Add(new AetheriaRuntimeCargoBayLoadoutCommit());
            var slots = (bays[0].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var existing = slots.FirstOrDefault(slot => string.Equals(slot?.Item?.ItemKey, pickup.Item.ItemKey, StringComparison.Ordinal));
            if (existing != null) existing.Item.Quantity += quantity;
            else slots.Add(new AetheriaRuntimeLoadoutItemSlotCommit { Item = pickup.Item });
            bays[0].Items = slots; entity.CargoContents = bays;
            pickups.RemoveAt(index); zone.DroppedPickups = pickups;
            return true;
        }
    }
}
