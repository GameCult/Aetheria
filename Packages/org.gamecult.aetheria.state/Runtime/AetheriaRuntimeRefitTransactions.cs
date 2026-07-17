using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeRefitSourceKinds
    {
        public const string Cargo = "cargo";
        public const string Equipment = "equipment";
        public const string CargoBay = "cargo-bay";
        public const string DockingBay = "docking-bay";
    }

    public static class AetheriaRuntimeEquipmentGridGeometry
    {
        public static IReadOnlyList<(int X, int Y)> RotatedCells(
            AetheriaRuntimeCatalogItem item,
            int rotation)
        {
            if (item == null)
                return Array.Empty<(int X, int Y)>();
            return (item.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                .Select(cell => rotation switch
                {
                    1 => (item.ShapeHeight - 1 - cell.Y, cell.X),
                    2 => (item.ShapeWidth - 1 - cell.X, item.ShapeHeight - 1 - cell.Y),
                    3 => (cell.Y, item.ShapeWidth - 1 - cell.X),
                    _ => (cell.X, cell.Y)
                })
                .ToArray();
        }

        public static int ParseRotation(string? value)
        {
            if (string.Equals(value, "Clockwise", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Rotate90", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(value, "Half", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Reversed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Rotate180", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(value, "CounterClockwise", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Left", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Rotate270", StringComparison.OrdinalIgnoreCase)) return 3;
            return 0;
        }

        public static string RotationName(int rotation) => (((rotation % 4) + 4) % 4) switch
        {
            1 => "Clockwise",
            2 => "Half",
            3 => "CounterClockwise",
            _ => "None"
        };
    }

    public static class AetheriaRuntimeRefitTransactions
    {
        public static bool TryTransferCargo(
            AetheriaRuntimeEntitySnapshotCommit origin,
            int originCargoIndex,
            int sourceX,
            int sourceY,
            AetheriaRuntimeEntitySnapshotCommit destination,
            int destinationCargoIndex,
            string itemKey,
            int quantity,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition,
            AetheriaRuntimeCatalogSnapshot? catalog,
            out string rejectionReason)
        {
            rejectionReason = "";
            if (origin == null || destination == null || catalog == null || quantity <= 0)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoSource, out rejectionReason);

            var originPlan = new EntityPlan(origin);
            var destinationPlan = ReferenceEquals(origin, destination)
                ? originPlan
                : new EntityPlan(destination);
            if (ReferenceEquals(originPlan, destinationPlan) &&
                originCargoIndex == destinationCargoIndex &&
                !hasDestinationPosition)
            {
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoDestination, out rejectionReason);
            }
            if (originCargoIndex < 0 || originCargoIndex >= originPlan.CargoContents.Count)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoSource, out rejectionReason);
            if (destinationCargoIndex < 0 ||
                destinationCargoIndex >= destinationPlan.CargoBays.Count ||
                destinationCargoIndex >= destinationPlan.CargoContents.Count)
            {
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoDestination, out rejectionReason);
            }

            var sourceItems = originPlan.CargoContents[originCargoIndex];
            var sourceIndex = sourceItems.FindIndex(slot =>
                slot?.Item != null &&
                string.Equals(slot.Item.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal) &&
                slot.X == sourceX && slot.Y == sourceY);
            if (sourceIndex < 0 || sourceItems[sourceIndex].Item == null ||
                quantity > Math.Max(0, sourceItems[sourceIndex].Item.Quantity))
            {
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoSource, out rejectionReason);
            }

            if (ReferenceEquals(originPlan, destinationPlan) &&
                originCargoIndex == destinationCargoIndex &&
                hasDestinationPosition &&
                sourceX == destinationX && sourceY == destinationY)
            {
                return true;
            }

            var sourceSlot = sourceItems[sourceIndex];
            var moved = CloneSlot(sourceSlot);
            moved.Item.Quantity = quantity;
            if (quantity == sourceSlot.Item.Quantity)
                sourceItems.RemoveAt(sourceIndex);
            else
                sourceSlot.Item.Quantity -= quantity;

            var item = catalog.FindItem(itemKey ?? "");
            var bay = catalog.FindItem(destinationPlan.CargoBays[destinationCargoIndex].Item?.ItemKey ?? "");
            if (item == null || bay == null || Cells(item.ShapeCells).Count == 0)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoDestination, out rejectionReason);

            var destinationItems = destinationPlan.CargoContents[destinationCargoIndex];
            if (hasDestinationPosition)
            {
                var stack = destinationItems.FirstOrDefault(slot =>
                    SlotOccupies(slot, destinationX, destinationY, catalog) &&
                    string.Equals(slot.Item?.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal));
                if (stack != null)
                {
                    var maxStack = Math.Max(1, item.MaxStack);
                    if (!item.Stackable || stack.Item.Quantity + quantity > maxStack)
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.CargoStackLimit, out rejectionReason);
                    stack.Item.Quantity += quantity;
                }
                else if (!TryPlaceCargoSlot(
                             destinationItems,
                             bay,
                             item,
                             moved,
                             destinationX,
                             destinationY,
                             true,
                             catalog))
                {
                    return Reject(AetheriaRuntimeDaemonRejectionReasons.CargoNoFit, out rejectionReason);
                }
            }
            else
            {
                var remaining = quantity;
                if (item.Stackable)
                {
                    var maxStack = Math.Max(1, item.MaxStack);
                    foreach (var stack in destinationItems.Where(slot =>
                                 slot?.Item != null &&
                                 string.Equals(slot.Item.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal)))
                    {
                        var transferred = Math.Min(remaining, Math.Max(0, maxStack - stack.Item.Quantity));
                        stack.Item.Quantity += transferred;
                        remaining -= transferred;
                        if (remaining == 0) break;
                    }
                }

                if (remaining > 0)
                {
                    moved.Item.Quantity = remaining;
                    if (!TryPlaceCargoSlot(destinationItems, bay, item, moved, 0, 0, false, catalog))
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.CargoNoFit, out rejectionReason);
                }
            }

            originPlan.ApplyCargoOnly();
            if (!ReferenceEquals(originPlan, destinationPlan))
                destinationPlan.ApplyCargoOnly();
            return true;
        }

        public static bool TryEquip(
            AetheriaRuntimeEntitySnapshotCommit origin,
            string sourceKind,
            int sourceIndex,
            int sourceX,
            int sourceY,
            AetheriaRuntimeEntitySnapshotCommit destination,
            string itemKey,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition,
            AetheriaRuntimeCatalogSnapshot? catalog,
            out string rejectionReason)
        {
            rejectionReason = "";
            if (origin == null || destination == null || catalog == null)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);

            var originPlan = new EntityPlan(origin);
            var destinationPlan = ReferenceEquals(origin, destination)
                ? originPlan
                : new EntityPlan(destination);
            if (!originPlan.TryRemove(
                    sourceKind,
                    sourceIndex,
                    sourceX,
                    sourceY,
                    itemKey,
                    out var source,
                    out rejectionReason))
            {
                return false;
            }

            var item = catalog.FindItem(source.Slot.Item?.ItemKey ?? "");
            if (item == null || source.Slot.Item == null)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitItem, out rejectionReason);
            if (source.Slot.Item.Quantity != 1)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.RefitItemMustBeSingle, out rejectionReason);

            if (!TryFindEquipmentPlacement(
                    destination,
                    destinationPlan,
                    item,
                    source.Slot,
                    destinationX,
                    destinationY,
                    hasDestinationPosition,
                    catalog,
                    out var placed,
                    out rejectionReason))
            {
                return false;
            }

            destinationPlan.AddInstalled(
                placed,
                item.Category,
                source.PreservedWeaponGroups,
                ReferenceEquals(originPlan, destinationPlan) ? source.OriginalEquipmentIndex : -1);
            originPlan.Apply();
            if (!ReferenceEquals(originPlan, destinationPlan))
                destinationPlan.Apply();
            return true;
        }

        public static bool TryStore(
            AetheriaRuntimeEntitySnapshotCommit origin,
            string sourceKind,
            int sourceIndex,
            AetheriaRuntimeEntitySnapshotCommit destination,
            int destinationCargoIndex,
            string itemKey,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition,
            AetheriaRuntimeCatalogSnapshot? catalog,
            out string rejectionReason)
        {
            rejectionReason = "";
            if (origin == null || destination == null || catalog == null)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);

            var originPlan = new EntityPlan(origin);
            var destinationPlan = ReferenceEquals(origin, destination)
                ? originPlan
                : new EntityPlan(destination);
            if (!originPlan.TryRemove(
                    sourceKind,
                    sourceIndex,
                    int.MinValue,
                    int.MinValue,
                    itemKey,
                    out var source,
                    out rejectionReason))
            {
                return false;
            }

            if (ReferenceEquals(originPlan, destinationPlan) &&
                string.Equals(sourceKind, AetheriaRuntimeRefitSourceKinds.CargoBay, StringComparison.Ordinal) &&
                sourceIndex < destinationCargoIndex)
            {
                destinationCargoIndex--;
            }

            var item = catalog.FindItem(source.Slot.Item?.ItemKey ?? "");
            if (item == null || source.Slot.Item == null)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitItem, out rejectionReason);
            if (destinationCargoIndex < 0 || destinationCargoIndex >= destinationPlan.CargoBays.Count)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoDestination, out rejectionReason);

            var bayItem = catalog.FindItem(destinationPlan.CargoBays[destinationCargoIndex].Item?.ItemKey ?? "");
            if (bayItem == null || destinationCargoIndex >= destinationPlan.CargoContents.Count)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidCargoDestination, out rejectionReason);

            var occupied = OccupiedCargoCells(destinationPlan.CargoContents[destinationCargoIndex], catalog);
            var rotation = ParseRotation(source.Slot.Rotation);
            if (!TryFindPlacement(
                    Cells(bayItem.InteriorShapeCells),
                    Math.Max(0, bayItem.InteriorShapeWidth),
                    Math.Max(0, bayItem.InteriorShapeHeight),
                    item,
                    rotation,
                    occupied,
                    destinationX,
                    destinationY,
                    hasDestinationPosition,
                    out var x,
                    out var y))
            {
                return Reject(AetheriaRuntimeDaemonRejectionReasons.RefitNoFit, out rejectionReason);
            }

            var placed = CloneSlot(source.Slot);
            placed.X = x;
            placed.Y = y;
            destinationPlan.CargoContents[destinationCargoIndex].Add(placed);
            originPlan.Apply();
            if (!ReferenceEquals(originPlan, destinationPlan))
                destinationPlan.Apply();
            return true;
        }

        private static bool TryFindEquipmentPlacement(
            AetheriaRuntimeEntitySnapshotCommit destination,
            EntityPlan plan,
            AetheriaRuntimeCatalogItem item,
            AetheriaRuntimeLoadoutItemSlotCommit source,
            int requestedX,
            int requestedY,
            bool hasRequestedPosition,
            AetheriaRuntimeCatalogSnapshot catalog,
            out AetheriaRuntimeLoadoutItemSlotCommit placed,
            out string rejectionReason)
        {
            placed = null!;
            rejectionReason = "";
            var hull = catalog.FindItem(destination.HullItemKey ?? "");
            if (hull == null || Cells(item.ShapeCells).Count == 0)
                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitItem, out rejectionReason);

            var occupied = OccupiedEquipmentCells(plan, catalog);
            var hardpointItem = !string.IsNullOrWhiteSpace(item.HardpointType);
            int x;
            int y;
            int rotation;
            if (hardpointItem)
            {
                var candidates = (hull.Hardpoints ?? Array.Empty<AetheriaRuntimeHardpoint>())
                    .Where(hardpoint => hardpoint != null &&
                        string.Equals(hardpoint.Type, item.HardpointType, StringComparison.Ordinal))
                    .Where(hardpoint => !hasRequestedPosition ||
                        (hardpoint.PositionX == requestedX && hardpoint.PositionY == requestedY));
                var fit = candidates
                    .Select(hardpoint => new
                    {
                        Hardpoint = hardpoint,
                        Rotation = ParseRotation(hardpoint.Rotation),
                        Target = Cells(hardpoint.ShapeCells)
                    })
                    .FirstOrDefault(candidate => RotatedCells(item, candidate.Rotation)
                        .All(cell => candidate.Target.Contains(cell) &&
                            !occupied.Contains((candidate.Hardpoint.PositionX + cell.X, candidate.Hardpoint.PositionY + cell.Y))));
                if (fit == null)
                    return Reject(AetheriaRuntimeDaemonRejectionReasons.RefitNoFit, out rejectionReason);
                x = fit.Hardpoint.PositionX;
                y = fit.Hardpoint.PositionY;
                rotation = fit.Rotation;
            }
            else
            {
                var hardpointCells = (hull.Hardpoints ?? Array.Empty<AetheriaRuntimeHardpoint>())
                    .Where(hardpoint => hardpoint != null)
                    .SelectMany(hardpoint => (hardpoint.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                        .Select(cell => (hardpoint.PositionX + cell.X, hardpoint.PositionY + cell.Y)))
                    .ToHashSet();
                occupied.UnionWith(hardpointCells);
                rotation = ParseRotation(source.Rotation);
                if (!TryFindPlacement(
                        Cells(hull.InteriorShapeCells),
                        Math.Max(0, hull.InteriorShapeWidth),
                        Math.Max(0, hull.InteriorShapeHeight),
                        item,
                        rotation,
                        occupied,
                        requestedX,
                        requestedY,
                        hasRequestedPosition,
                        out x,
                        out y))
                {
                    return Reject(AetheriaRuntimeDaemonRejectionReasons.RefitNoFit, out rejectionReason);
                }
            }

            placed = CloneSlot(source);
            placed.X = x;
            placed.Y = y;
            placed.Rotation = RotationName(rotation);
            return true;
        }

        private static bool TryPlaceCargoSlot(
            List<AetheriaRuntimeLoadoutItemSlotCommit> destinationItems,
            AetheriaRuntimeCatalogItem bay,
            AetheriaRuntimeCatalogItem item,
            AetheriaRuntimeLoadoutItemSlotCommit slot,
            int requestedX,
            int requestedY,
            bool hasRequestedPosition,
            AetheriaRuntimeCatalogSnapshot catalog)
        {
            var occupied = OccupiedCargoCells(destinationItems, catalog);
            var rotation = ParseRotation(slot.Rotation);
            if (!TryFindPlacement(
                    Cells(bay.InteriorShapeCells),
                    Math.Max(0, bay.InteriorShapeWidth),
                    Math.Max(0, bay.InteriorShapeHeight),
                    item,
                    rotation,
                    occupied,
                    requestedX,
                    requestedY,
                    hasRequestedPosition,
                    out var x,
                    out var y))
            {
                return false;
            }
            slot.X = x;
            slot.Y = y;
            destinationItems.Add(slot);
            return true;
        }

        private static bool SlotOccupies(
            AetheriaRuntimeLoadoutItemSlotCommit? slot,
            int x,
            int y,
            AetheriaRuntimeCatalogSnapshot catalog)
        {
            var item = catalog.FindItem(slot?.Item?.ItemKey ?? "");
            return slot != null && item != null &&
                RotatedCells(item, ParseRotation(slot.Rotation))
                    .Any(cell => slot.X + cell.X == x && slot.Y + cell.Y == y);
        }

        private static bool TryFindPlacement(
            HashSet<(int X, int Y)> target,
            int width,
            int height,
            AetheriaRuntimeCatalogItem item,
            int rotation,
            HashSet<(int X, int Y)> occupied,
            int requestedX,
            int requestedY,
            bool hasRequestedPosition,
            out int x,
            out int y)
        {
            x = 0;
            y = 0;
            var cells = RotatedCells(item, rotation).ToArray();
            if (cells.Length == 0 || target.Count == 0)
                return false;

            bool Fits(int candidateX, int candidateY) =>
                cells.All(cell => target.Contains((candidateX + cell.X, candidateY + cell.Y)) &&
                    !occupied.Contains((candidateX + cell.X, candidateY + cell.Y)));
            if (hasRequestedPosition)
            {
                if (!Fits(requestedX, requestedY))
                    return false;
                x = requestedX;
                y = requestedY;
                return true;
            }

            for (var candidateY = 0; candidateY < height; candidateY++)
            for (var candidateX = 0; candidateX < width; candidateX++)
            {
                if (!Fits(candidateX, candidateY))
                    continue;
                x = candidateX;
                y = candidateY;
                return true;
            }
            return false;
        }

        private static HashSet<(int X, int Y)> OccupiedEquipmentCells(
            EntityPlan plan,
            AetheriaRuntimeCatalogSnapshot catalog) =>
            plan.Equipment.Concat(plan.CargoBays).Concat(plan.DockingBays)
                .Where(slot => slot?.Item != null)
                .SelectMany(slot =>
                {
                    var item = catalog.FindItem(slot.Item.ItemKey ?? "");
                    return item == null
                        ? Array.Empty<(int X, int Y)>()
                        : RotatedCells(item, ParseRotation(slot.Rotation))
                            .Select(cell => (slot.X + cell.X, slot.Y + cell.Y));
                })
                .ToHashSet();

        private static HashSet<(int X, int Y)> OccupiedCargoCells(
            IEnumerable<AetheriaRuntimeLoadoutItemSlotCommit> slots,
            AetheriaRuntimeCatalogSnapshot catalog) =>
            (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot?.Item != null)
                .SelectMany(slot =>
                {
                    var item = catalog.FindItem(slot.Item.ItemKey ?? "");
                    return item == null
                        ? Array.Empty<(int X, int Y)>()
                        : RotatedCells(item, ParseRotation(slot.Rotation))
                            .Select(cell => (slot.X + cell.X, slot.Y + cell.Y));
                })
                .ToHashSet();

        private static HashSet<(int X, int Y)> Cells(IReadOnlyList<AetheriaRuntimeShapeCell>? cells) =>
            (cells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                .Select(cell => (cell.X, cell.Y))
                .ToHashSet();

        private static IEnumerable<(int X, int Y)> RotatedCells(AetheriaRuntimeCatalogItem item, int rotation)
            => AetheriaRuntimeEquipmentGridGeometry.RotatedCells(item, rotation);

        private static int ParseRotation(string? value) =>
            AetheriaRuntimeEquipmentGridGeometry.ParseRotation(value);

        private static string RotationName(int rotation) =>
            AetheriaRuntimeEquipmentGridGeometry.RotationName(rotation);

        private static AetheriaRuntimeLoadoutItemSlotCommit CloneSlot(AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var item = slot.Item;
            return new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.X,
                Y = slot.Y,
                Rotation = slot.Rotation ?? "None",
                Item = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = item?.ItemKey ?? "",
                    Quality = item?.Quality ?? 1,
                    Durability = item?.Durability ?? 1,
                    Quantity = item?.Quantity ?? 1,
                    Enabled = item?.Enabled ?? true,
                    OverrideShutdown = item?.OverrideShutdown ?? false,
                    Temperature = item?.Temperature ?? 0
                }
            };
        }

        private static bool Reject(string reason, out string rejectionReason)
        {
            rejectionReason = reason;
            return false;
        }

        private sealed class SourceSelection
        {
            public AetheriaRuntimeLoadoutItemSlotCommit Slot { get; set; } = null!;
            public IReadOnlyList<int> PreservedWeaponGroups { get; set; } = Array.Empty<int>();
            public int OriginalEquipmentIndex { get; set; } = -1;
        }

        private sealed class EntityPlan
        {
            private readonly AetheriaRuntimeEntitySnapshotCommit _entity;
            private readonly List<List<int>> _weaponGroups;
            private readonly List<int> _equipmentOrigins;

            public EntityPlan(AetheriaRuntimeEntitySnapshotCommit entity)
            {
                _entity = entity;
                Equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    .Select(CloneSlot).ToList();
                _equipmentOrigins = Enumerable.Range(0, Equipment.Count).ToList();
                CargoBays = (entity.CargoBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    .Select(CloneSlot).ToList();
                DockingBays = (entity.DockingBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    .Select(CloneSlot).ToList();
                CargoContents = CloneContents(entity.CargoContents, CargoBays.Count);
                DockingContents = CloneContents(entity.DockingBayContents, DockingBays.Count);
                DockingAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>()).ToList();
                while (DockingAssignments.Count < DockingBays.Count) DockingAssignments.Add(-1);
                _weaponGroups = (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                    .Select(group => (group ?? Array.Empty<int>()).ToList())
                    .ToList();
            }

            public List<AetheriaRuntimeLoadoutItemSlotCommit> Equipment { get; }
            public List<AetheriaRuntimeLoadoutItemSlotCommit> CargoBays { get; }
            public List<AetheriaRuntimeLoadoutItemSlotCommit> DockingBays { get; }
            public List<List<AetheriaRuntimeLoadoutItemSlotCommit>> CargoContents { get; }
            public List<List<AetheriaRuntimeLoadoutItemSlotCommit>> DockingContents { get; }
            public List<int> DockingAssignments { get; }

            public bool TryRemove(
                string sourceKind,
                int sourceIndex,
                int sourceX,
                int sourceY,
                string itemKey,
                out SourceSelection source,
                out string rejectionReason)
            {
                source = null!;
                rejectionReason = "";
                if (string.Equals(sourceKind, AetheriaRuntimeRefitSourceKinds.Cargo, StringComparison.Ordinal))
                {
                    if (sourceIndex < 0 || sourceIndex >= CargoContents.Count)
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    var itemIndex = CargoContents[sourceIndex].FindIndex(slot => Matches(slot, itemKey, sourceX, sourceY));
                    if (itemIndex < 0)
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    source = new SourceSelection { Slot = CloneSlot(CargoContents[sourceIndex][itemIndex]) };
                    CargoContents[sourceIndex].RemoveAt(itemIndex);
                    return true;
                }

                if (string.Equals(sourceKind, AetheriaRuntimeRefitSourceKinds.Equipment, StringComparison.Ordinal))
                {
                    var originalEquipmentIndex = sourceIndex >= 0 && sourceIndex < _equipmentOrigins.Count
                        ? _equipmentOrigins[sourceIndex]
                        : -1;
                    if (!TryTake(Equipment, sourceIndex, itemKey, out var slot))
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    _equipmentOrigins.RemoveAt(sourceIndex);
                    var preservedGroups = _weaponGroups
                        .Select((group, index) => (group, index))
                        .Where(value => value.group.Contains(sourceIndex))
                        .Select(value => value.index)
                        .ToArray();
                    foreach (var group in _weaponGroups)
                    {
                        group.RemoveAll(index => index == sourceIndex);
                        for (var index = 0; index < group.Count; index++)
                            if (group[index] > sourceIndex) group[index]--;
                    }
                    source = new SourceSelection
                    {
                        Slot = CloneSlot(slot),
                        PreservedWeaponGroups = preservedGroups,
                        OriginalEquipmentIndex = originalEquipmentIndex
                    };
                    return true;
                }

                if (string.Equals(sourceKind, AetheriaRuntimeRefitSourceKinds.CargoBay, StringComparison.Ordinal))
                {
                    if (sourceIndex < 0 || sourceIndex >= CargoBays.Count || sourceIndex >= CargoContents.Count)
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    if (CargoContents[sourceIndex].Count > 0)
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.RefitBayNotEmpty, out rejectionReason);
                    if (!TryTake(CargoBays, sourceIndex, itemKey, out var slot))
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    CargoContents.RemoveAt(sourceIndex);
                    source = new SourceSelection { Slot = CloneSlot(slot) };
                    return true;
                }

                if (string.Equals(sourceKind, AetheriaRuntimeRefitSourceKinds.DockingBay, StringComparison.Ordinal))
                {
                    if (sourceIndex < 0 || sourceIndex >= DockingBays.Count)
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    if ((sourceIndex < DockingContents.Count && DockingContents[sourceIndex].Count > 0) ||
                        (sourceIndex < DockingAssignments.Count && DockingAssignments[sourceIndex] >= 0))
                    {
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.RefitBayNotEmpty, out rejectionReason);
                    }
                    if (!TryTake(DockingBays, sourceIndex, itemKey, out var slot))
                        return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
                    if (sourceIndex < DockingContents.Count) DockingContents.RemoveAt(sourceIndex);
                    if (sourceIndex < DockingAssignments.Count) DockingAssignments.RemoveAt(sourceIndex);
                    source = new SourceSelection { Slot = CloneSlot(slot) };
                    return true;
                }

                return Reject(AetheriaRuntimeDaemonRejectionReasons.InvalidRefitSource, out rejectionReason);
            }

            public void AddInstalled(
                AetheriaRuntimeLoadoutItemSlotCommit slot,
                string? category,
                IReadOnlyList<int> preservedWeaponGroups,
                int originalEquipmentIndex)
            {
                if (string.Equals(category, AetheriaRuntimeItemCategories.CargoBay, StringComparison.Ordinal))
                {
                    CargoBays.Add(slot);
                    CargoContents.Add(new List<AetheriaRuntimeLoadoutItemSlotCommit>());
                    return;
                }
                if (string.Equals(category, AetheriaRuntimeItemCategories.DockingBay, StringComparison.Ordinal))
                {
                    DockingBays.Add(slot);
                    DockingContents.Add(new List<AetheriaRuntimeLoadoutItemSlotCommit>());
                    DockingAssignments.Add(-1);
                    return;
                }

                var equipmentIndex = Equipment.Count;
                Equipment.Add(slot);
                _equipmentOrigins.Add(originalEquipmentIndex);
                foreach (var groupIndex in preservedWeaponGroups)
                    if (groupIndex >= 0 && groupIndex < _weaponGroups.Count)
                        _weaponGroups[groupIndex].Add(equipmentIndex);
            }

            public void Apply()
            {
                _entity.Equipment = Equipment.ToArray();
                _entity.CargoBays = CargoBays.ToArray();
                _entity.DockingBays = DockingBays.ToArray();
                _entity.CargoContents = CargoContents
                    .Select(items => new AetheriaRuntimeCargoBayLoadoutCommit { Items = items.ToArray() })
                    .ToArray();
                _entity.DockingBayContents = DockingContents
                    .Select(items => new AetheriaRuntimeCargoBayLoadoutCommit { Items = items.ToArray() })
                    .ToArray();
                _entity.DockingBayAssignments = DockingAssignments.ToArray();
                _entity.WeaponGroups = _weaponGroups
                    .Select(group => (IReadOnlyList<int>)group.Distinct().OrderBy(index => index).ToArray())
                    .ToArray();
                RemapEquipmentOwnedState();
            }

            private void RemapEquipmentOwnedState()
            {
                var newIndices = _equipmentOrigins
                    .Select((originalIndex, newIndex) => (originalIndex, newIndex))
                    .Where(value => value.originalIndex >= 0)
                    .ToDictionary(value => value.originalIndex, value => value.newIndex);

                _entity.EquipmentStates = (_entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                    .Where(state => state != null && newIndices.ContainsKey(state.EquipmentIndex))
                    .Select(state =>
                    {
                        state.EquipmentIndex = newIndices[state.EquipmentIndex];
                        return state;
                    })
                    .ToArray();
                _entity.BehaviorStates = (_entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                    .Where(state => state != null &&
                        (!string.Equals(state.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal) ||
                         newIndices.ContainsKey(state.OwnerIndex)))
                    .Select(state =>
                    {
                        if (string.Equals(state.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal))
                            state.OwnerIndex = newIndices[state.OwnerIndex];
                        return state;
                    })
                    .ToArray();
                _entity.WeaponStates = (_entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                    .Where(state => state != null &&
                        (!string.Equals(state.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal) ||
                         newIndices.ContainsKey(state.OwnerIndex)))
                    .Select(state =>
                    {
                        if (string.Equals(state.OwnerKind, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal))
                            state.OwnerIndex = newIndices[state.OwnerIndex];
                        return state;
                    })
                    .ToArray();
            }

            public void ApplyCargoOnly()
            {
                _entity.CargoContents = CargoContents
                    .Select(items => new AetheriaRuntimeCargoBayLoadoutCommit { Items = items.ToArray() })
                    .ToArray();
            }

            private static List<List<AetheriaRuntimeLoadoutItemSlotCommit>> CloneContents(
                IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? contents,
                int count)
            {
                var result = (contents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                    .Select(bay => (bay?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                        .Select(CloneSlot).ToList())
                    .ToList();
                while (result.Count < count) result.Add(new List<AetheriaRuntimeLoadoutItemSlotCommit>());
                return result;
            }

            private static bool TryTake(
                List<AetheriaRuntimeLoadoutItemSlotCommit> slots,
                int index,
                string itemKey,
                out AetheriaRuntimeLoadoutItemSlotCommit slot)
            {
                slot = null!;
                if (index < 0 || index >= slots.Count || !Matches(slots[index], itemKey, int.MinValue, int.MinValue))
                    return false;
                slot = slots[index];
                slots.RemoveAt(index);
                return true;
            }

            private static bool Matches(
                AetheriaRuntimeLoadoutItemSlotCommit? slot,
                string itemKey,
                int x,
                int y) =>
                slot?.Item != null &&
                string.Equals(slot.Item.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal) &&
                (x == int.MinValue || slot.X == x) &&
                (y == int.MinValue || slot.Y == y);
        }
    }
}
