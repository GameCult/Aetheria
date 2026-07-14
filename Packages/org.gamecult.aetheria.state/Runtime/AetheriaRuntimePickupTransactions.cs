using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal enum AetheriaRuntimePickupContactResult
    {
        Ignored,
        Collected,
        RejectedCapacity
    }

    internal static class AetheriaRuntimePickupTransactions
    {
        public static AetheriaRuntimePickupContactResult ApplyContact(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeWorldContact contact,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (zone == null || entity == null || contact == null || !entity.IsActive ||
                contact.PickupIndex < 0 ||
                (contact.EntityAIndex != entity.EntityIndex && contact.EntityBIndex != entity.EntityIndex))
                return AetheriaRuntimePickupContactResult.Ignored;

            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>()).ToList();
            var index = pickups.FindIndex(pickup => pickup != null && pickup.PickupIndex == contact.PickupIndex);
            if (index < 0) return AetheriaRuntimePickupContactResult.Ignored;
            var pickup = pickups[index];
            var quantity = Math.Max(1, pickup.Item?.Quantity ?? 1);
            if (pickup.AgeSeconds >= pickup.LifetimeSeconds ||
                pickup.Item == null)
                return AetheriaRuntimePickupContactResult.Ignored;
            var cargoBayCount = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Select(slot => catalog?.FindItem(slot?.Item?.ItemKey ?? ""))
                .Count(item => item != null && item.InteriorOccupiedCells > 0);
            var cargoIndex = Enumerable.Range(0, cargoBayCount)
                .Where(index => quantity <= AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(
                    entity, catalog, pickup.Item.ItemKey ?? "", index))
                .DefaultIfEmpty(-1)
                .First();
            if (cargoIndex < 0)
            {
                var normalSign = contact.EntityAIndex == entity.EntityIndex ? 1.0 : -1.0;
                var length = Math.Sqrt(contact.NormalX * contact.NormalX + contact.NormalZ * contact.NormalZ);
                var normalX = length > 0.000001
                    ? contact.NormalX / length * normalSign
                    : pickup.PositionX >= entity.PositionX ? 1.0 : -1.0;
                var normalZ = length > 0.000001
                    ? contact.NormalZ / length * normalSign
                    : 0.0;
                pickup.VelocityX += normalX * 25.0;
                pickup.VelocityZ += normalZ * 25.0;
                return AetheriaRuntimePickupContactResult.RejectedCapacity;
            }
            var bays = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToList();
            while (bays.Count <= cargoIndex) bays.Add(new AetheriaRuntimeCargoBayLoadoutCommit());
            var slots = (bays[cargoIndex].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var existing = slots.FirstOrDefault(slot => string.Equals(slot?.Item?.ItemKey, pickup.Item.ItemKey, StringComparison.Ordinal));
            if (existing != null) existing.Item.Quantity += quantity;
            else slots.Add(new AetheriaRuntimeLoadoutItemSlotCommit { Item = pickup.Item });
            bays[cargoIndex].Items = slots; entity.CargoContents = bays;
            pickups.RemoveAt(index); zone.DroppedPickups = pickups;
            return AetheriaRuntimePickupContactResult.Collected;
        }
    }
}
