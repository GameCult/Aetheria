using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeCargoCapacityQueries
    {
        public static double Capacity(AetheriaRuntimeEntitySnapshotCommit? entity, AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null || catalog == null)
                return 0;
            var hull = catalog.FindItem(entity.HullItemKey ?? "");
            if (hull == null)
                return 0;
            return Math.Max(0, AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                hull.HullCapacity,
                new AetheriaRuntimeLoadoutItemCommit { ItemKey = hull.ItemKey, Quality = 1, Durability = 1, Enabled = true },
                1));
        }

        public static double Occupied(AetheriaRuntimeEntitySnapshotCommit? entity, AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null || catalog == null)
                return 0;
            var equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Sum(slot => Volume(slot?.Item, catalog));
            var cargo = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .SelectMany(bay => bay?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Sum(slot => Volume(slot?.Item, catalog));
            return equipment + cargo;
        }

        public static double Available(AetheriaRuntimeEntitySnapshotCommit? entity, AetheriaRuntimeCatalogSnapshot? catalog) =>
            Math.Max(0, Capacity(entity, catalog) - Occupied(entity, catalog));

        public static int UnitsThatFit(AetheriaRuntimeEntitySnapshotCommit? entity, AetheriaRuntimeCatalogSnapshot? catalog, string itemKey)
        {
            var volume = catalog?.FindItem(itemKey ?? "")?.Volume ?? 0;
            return volume <= 0 ? int.MaxValue : Math.Max(0, (int)Math.Floor(Available(entity, catalog) / volume));
        }

        private static double Volume(AetheriaRuntimeLoadoutItemCommit? item, AetheriaRuntimeCatalogSnapshot catalog) =>
            item == null ? 0 : Math.Max(0, catalog.FindItem(item.ItemKey ?? "")?.Volume ?? 0) * Math.Max(1, item.Quantity);
    }
}
