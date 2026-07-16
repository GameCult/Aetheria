using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeCargoTransactions
    {
        public static bool TryRemoveQuantity(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            string itemKey,
            int x,
            int y,
            int quantity,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = null!;
            var cargoContents = (entity?.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToArray();
            if (cargoIndex < 0 || cargoIndex >= cargoContents.Length || cargoContents[cargoIndex] == null)
                return false;
            var items = (cargoContents[cargoIndex].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var itemIndex = items.FindIndex(candidate => candidate?.Item != null &&
                string.Equals(candidate.Item.ItemKey, itemKey, StringComparison.Ordinal) &&
                candidate.X == x && candidate.Y == y);
            if (itemIndex < 0) return false;
            var source = items[itemIndex];
            var sourceQuantity = Math.Max(1, source.Item.Quantity);
            if (quantity <= 0 || quantity > sourceQuantity) return false;
            slot = Clone(source, quantity);
            if (quantity == sourceQuantity) items.RemoveAt(itemIndex);
            else source.Item.Quantity = sourceQuantity - quantity;
            cargoContents[cargoIndex] = new AetheriaRuntimeCargoBayLoadoutCommit { Items = items.ToArray() };
            entity!.CargoContents = cargoContents;
            return true;
        }

        public static bool TryFind(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string itemKey,
            out int cargoIndex,
            out int x,
            out int y)
        {
            var cargo = entity?.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            for (var bay = 0; bay < cargo.Count; bay++)
            foreach (var slot in cargo[bay]?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            {
                if (slot?.Item == null || !string.Equals(slot.Item.ItemKey, itemKey, StringComparison.Ordinal)) continue;
                cargoIndex = bay; x = slot.X; y = slot.Y; return true;
            }
            cargoIndex = x = y = -1;
            return false;
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit Clone(AetheriaRuntimeLoadoutItemSlotCommit source, int quantity) => new()
        {
            X = source.X, Y = source.Y, Rotation = source.Rotation ?? "None",
            Item = new AetheriaRuntimeLoadoutItemCommit
            {
                ItemKey = source.Item.ItemKey, Quality = source.Item.Quality, Durability = source.Item.Durability,
                Quantity = quantity, Enabled = source.Item.Enabled, OverrideShutdown = source.Item.OverrideShutdown,
                Temperature = source.Item.Temperature
            }
        };
    }
}
