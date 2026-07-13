using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using Random = CultMath.Random;

public sealed class AetheriaDaemonLoadoutGenerator
{
    private readonly AetheriaRuntimeCatalogSnapshot _catalog;
    private readonly Random _random;
    private readonly uint _seed;
    private readonly int _zoneIndex;
    private readonly IReadOnlyDictionary<string, int> _homeZones;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> _adjacency;
    private readonly double _priceExponent;

    public AetheriaDaemonLoadoutGenerator(
        AetheriaRuntimeCatalogSnapshot catalog,
        uint seed,
        int zoneIndex,
        IReadOnlyDictionary<string, int> homeZones,
        IReadOnlyDictionary<int, IReadOnlyList<int>> adjacency,
        double priceExponent = 0.5)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _seed = seed == 0 ? 1u : seed;
        _random = new Random(_seed);
        _zoneIndex = zoneIndex;
        _homeZones = homeZones ?? throw new ArgumentNullException(nameof(homeZones));
        _adjacency = adjacency ?? throw new ArgumentNullException(nameof(adjacency));
        _priceExponent = priceExponent;
    }

    public AetheriaDaemonLoadout Build(
        string entityKind,
        string availabilityFactionKey,
        IReadOnlyList<string> scenarioCargo)
    {
        var hullType = string.Equals(entityKind, "station", StringComparison.OrdinalIgnoreCase) ? "Station" : "Ship";
        var hull = Pick(availabilityFactionKey, 0, item =>
            string.Equals(item.Category, AetheriaRuntimeItemCategories.Hull, StringComparison.Ordinal) &&
            string.Equals(item.HullType, hullType, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No available {hullType} hull for faction {availabilityFactionKey}.");

        var occupied = new HashSet<(int X, int Y)>();
        foreach (var hardpoint in hull.Hardpoints ?? Array.Empty<AetheriaRuntimeHardpoint>())
        foreach (var cell in hardpoint.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
            occupied.Add((hardpoint.PositionX + cell.X, hardpoint.PositionY + cell.Y));
        var slots = new List<AetheriaEntityItemSlot>();
        if (string.Equals(entityKind, "station", StringComparison.OrdinalIgnoreCase))
            AddFreeSpaceItem(hull, availabilityFactionKey, occupied, slots, 2,
                item => string.Equals(item.Category, AetheriaRuntimeItemCategories.DockingBay, StringComparison.Ordinal));

        var previous = new List<AetheriaRuntimeCatalogItem>();
        foreach (var hardpoint in (hull.Hardpoints ?? Array.Empty<AetheriaRuntimeHardpoint>())
            .OrderByDescending(value => value.OccupiedCells))
        {
            var controllerKind = string.Equals(hardpoint.Type, "ControlModule", StringComparison.Ordinal)
                ? string.Equals(entityKind, "ship", StringComparison.OrdinalIgnoreCase) ? "Cockpit" : "TurretController"
                : "";
            var item = previous.FirstOrDefault(candidate => FitsHardpoint(candidate, hardpoint));
            item ??= Pick(availabilityFactionKey, 2, candidate =>
                IsGear(candidate) && FitsHardpoint(candidate, hardpoint) &&
                (controllerKind.Length == 0 || HasBehavior(candidate, controllerKind)));
            if (item == null)
            {
                if (controllerKind.Length > 0)
                    throw new InvalidOperationException($"No available {controllerKind} fits {hardpoint.Type}.");
                continue;
            }

            slots.Add(Slot(hardpoint.PositionX, hardpoint.PositionY, item.ItemKey));
            Reserve(occupied, item, hardpoint.PositionX, hardpoint.PositionY, ParseRotation(hardpoint.Rotation));
            previous.Add(item);
        }

        var cargoBay = AddFreeSpaceItem(hull, availabilityFactionKey, occupied, slots, 3, item =>
            string.Equals(item.Category, AetheriaRuntimeItemCategories.CargoBay, StringComparison.Ordinal) &&
            item.InteriorOccupiedCells >= 8);
        AddFreeSpaceItem(hull, availabilityFactionKey, occupied, slots, 2, item =>
            IsGear(item) && HasBehavior(item, "Reactor"));
        AddFreeSpaceItem(hull, availabilityFactionKey, occupied, slots, 2, item =>
            IsGear(item) && HasBehavior(item, "Capacitor"));

        var cargo = PackCargo(cargoBay, availabilityFactionKey, scenarioCargo,
            string.Equals(entityKind, "station", StringComparison.OrdinalIgnoreCase));
        var sensorArrayCount = string.Equals(entityKind, "station", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        for (var sensorIndex = 0; sensorIndex < sensorArrayCount; sensorIndex++)
            TryAddFreeSpaceItem(hull, availabilityFactionKey, occupied, slots, 2, item =>
                IsGear(item) && HasBehavior(item, "Sensor"));
        var selectedKeys = new[] { (Role: "hull", Key: hull.ItemKey) }
            .Concat(slots.Select(value => (Role: "equipment", Key: value.ItemKey)))
            .Concat(cargo.Select(value => (Role: "cargo", Key: value.Item.ItemKey)));
        var receipt = new AetheriaLoadoutGenerationReceipt
        {
            Seed = _seed,
            SourceZoneIndex = _zoneIndex,
            AvailabilityFactionKey = availabilityFactionKey,
            PriceExponent = _priceExponent,
            Selections = selectedKeys.Select(value => Selection(value.Role, value.Key, availabilityFactionKey)).ToArray()
        };
        var equipment = slots.ToArray();
        var defaultWeaponGroup = equipment
            .Select((slot, index) => (slot, index))
            .Where(value => IsWeapon(_catalog.FindItem(value.slot.ItemKey)))
            .Select(value => value.index)
            .ToArray();
        var weaponGroups = defaultWeaponGroup.Length == 0
            ? Array.Empty<int[]>()
            : new[] { defaultWeaponGroup };
        return new AetheriaDaemonLoadout(hull.ItemKey, equipment, cargo, weaponGroups, receipt);
    }

    private AetheriaLoadoutGenerationSelection Selection(string role, string itemKey, string factionKey)
    {
        var item = _catalog.FindItem(itemKey) ?? throw new InvalidOperationException($"Generated item {itemKey} left the catalog.");
        var faction = _catalog.FindCorporation(factionKey);
        var allegiance = string.Equals(item.ManufacturerKey, factionKey, StringComparison.Ordinal)
            ? 1
            : (faction?.Allegiances ?? Array.Empty<AetheriaRuntimeCorporationAllegiance>())
                .FirstOrDefault(value => value.CorporationKey == item.ManufacturerKey)?.Weight ?? 0;
        return new AetheriaLoadoutGenerationSelection
        {
            Role = role,
            ItemKey = item.ItemKey,
            ManufacturerKey = item.ManufacturerKey,
            Price = item.Price,
            ManufacturerDistance = _homeZones.TryGetValue(item.ManufacturerKey, out var home) ? Distance(_zoneIndex, home) : 1,
            Allegiance = allegiance
        };
    }

    private AetheriaRuntimeCatalogItem AddFreeSpaceItem(
        AetheriaRuntimeCatalogItem hull,
        string factionKey,
        HashSet<(int X, int Y)> occupied,
        List<AetheriaEntityItemSlot> slots,
        double sizeExponent,
        Func<AetheriaRuntimeCatalogItem, bool> filter)
    {
        var candidates = Available(factionKey)
            .Where(filter)
            .Select(item => (Item: item, Fit: FindFit(hull, item, occupied)))
            .Where(value => value.Fit.HasValue)
            .ToArray();
        var selected = PickWeighted(candidates, sizeExponent, factionKey,
            value => value.Item);
        if (selected.Item == null || !selected.Fit.HasValue)
            throw new InvalidOperationException("No available mandatory equipment fits the remaining hull space.");
        var fit = selected.Fit.Value;
        slots.Add(Slot(fit.X, fit.Y, selected.Item.ItemKey));
        Reserve(occupied, selected.Item, fit.X, fit.Y, fit.Rotation);
        return selected.Item;
    }

    private AetheriaRuntimeCatalogItem? TryAddFreeSpaceItem(
        AetheriaRuntimeCatalogItem hull,
        string factionKey,
        HashSet<(int X, int Y)> occupied,
        List<AetheriaEntityItemSlot> slots,
        double sizeExponent,
        Func<AetheriaRuntimeCatalogItem, bool> filter)
    {
        var candidates = Available(factionKey)
            .Where(filter)
            .Select(item => (Item: item, Fit: FindFit(hull, item, occupied)))
            .Where(value => value.Fit.HasValue)
            .ToArray();
        var selected = PickWeighted(candidates, sizeExponent, factionKey, value => value.Item);
        if (selected.Item == null || !selected.Fit.HasValue)
            return null;
        var fit = selected.Fit.Value;
        slots.Add(Slot(fit.X, fit.Y, selected.Item.ItemKey));
        Reserve(occupied, selected.Item, fit.X, fit.Y, fit.Rotation);
        return selected.Item;
    }

    private AetheriaLoadoutItemSlot[] PackCargo(
        AetheriaRuntimeCatalogItem cargoBay,
        string factionKey,
        IReadOnlyList<string> scenarioCargo,
        bool includeStationInventory)
    {
        var candidates = new List<AetheriaRuntimeCatalogItem>();
        foreach (var key in scenarioCargo ?? Array.Empty<string>())
        {
            var item = _catalog.FindItem(key);
            if (item != null) candidates.Add(item);
        }
        if (includeStationInventory)
        {
            var pool = Available(factionKey)
                .Where(item => !string.Equals(item.Category, AetheriaRuntimeItemCategories.CargoBay, StringComparison.Ordinal) &&
                    !string.Equals(item.Category, AetheriaRuntimeItemCategories.DockingBay, StringComparison.Ordinal) &&
                    (!string.Equals(item.Category, AetheriaRuntimeItemCategories.Hull, StringComparison.Ordinal) ||
                     string.Equals(item.HullType, "Ship", StringComparison.Ordinal)))
                .ToList();
            for (var draw = 0; draw < 16 && pool.Count > 0; draw++)
            {
                var selected = PickWeighted(pool, 1, factionKey, item => item);
                if (selected == null) break;
                candidates.Add(selected);
                pool.Remove(selected);
            }
        }

        var interiorCells = Cells(cargoBay.InteriorShapeCells);
        var width = Math.Max(1, cargoBay.InteriorShapeWidth);
        var height = Math.Max(1, cargoBay.InteriorShapeHeight);
        var occupied = new HashSet<(int X, int Y)>();
        var packed = new List<AetheriaLoadoutItemSlot>();
        foreach (var item in candidates.OrderByDescending(value => value.OccupiedCells))
        {
            var fit = FindFit(interiorCells, width, height, item, occupied);
            if (!fit.HasValue) continue;
            Reserve(occupied, item, fit.Value.X, fit.Value.Y, fit.Value.Rotation);
            packed.Add(CargoSlot(fit.Value.X, fit.Value.Y, item.ItemKey));
        }
        return packed.ToArray();
    }

    private AetheriaRuntimeCatalogItem? Pick(
        string factionKey,
        double sizeExponent,
        Func<AetheriaRuntimeCatalogItem, bool> filter)
    {
        return PickWeighted(Available(factionKey).Where(filter).ToArray(), sizeExponent, factionKey, item => item);
    }

    private T PickWeighted<T>(
        IReadOnlyList<T> values,
        double sizeExponent,
        string factionKey,
        Func<T, AetheriaRuntimeCatalogItem> itemOf)
    {
        if (values.Count == 0) return default!;
        var weights = values.Select(value => Weight(itemOf(value), factionKey, sizeExponent)).ToArray();
        var total = weights.Sum();
        if (total <= 0) return default!;
        var cursor = _random.NextFloat() * total;
        for (var i = 0; i < values.Count; i++)
        {
            cursor -= weights[i];
            if (cursor <= 0) return values[i];
        }
        return values[^1];
    }

    private IEnumerable<AetheriaRuntimeCatalogItem> Available(string factionKey)
    {
        var faction = _catalog.Corporations.FirstOrDefault(value =>
            string.Equals(value.CorporationKey, factionKey, StringComparison.Ordinal));
        if (faction == null) yield break;
        var allegiances = (faction.Allegiances ?? Array.Empty<AetheriaRuntimeCorporationAllegiance>())
            .ToDictionary(value => value.CorporationKey, value => value.Weight, StringComparer.Ordinal);
        foreach (var item in _catalog.EquipmentItems)
        {
            if (item.Price <= 0 || string.IsNullOrWhiteSpace(item.ManufacturerKey) ||
                !_homeZones.ContainsKey(item.ManufacturerKey) || !allegiances.ContainsKey(item.ManufacturerKey))
                continue;
            yield return item;
        }
    }

    private double Weight(AetheriaRuntimeCatalogItem item, string factionKey, double sizeExponent)
    {
        var faction = _catalog.Corporations.First(value => value.CorporationKey == factionKey);
        var allegiance = string.Equals(item.ManufacturerKey, factionKey, StringComparison.Ordinal)
            ? 1
            : faction.Allegiances.First(value => value.CorporationKey == item.ManufacturerKey).Weight /
              Math.Max(1, Distance(_zoneIndex, _homeZones[item.ManufacturerKey]));
        return allegiance * Math.Pow(Math.Max(1, item.OccupiedCells), sizeExponent) /
            Math.Pow(item.Price, _priceExponent);
    }

    private int Distance(int start, int target)
    {
        if (start == target) return 1;
        var visited = new HashSet<int> { start };
        var queue = new Queue<(int Zone, int Distance)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var adjacent in _adjacency.TryGetValue(current.Zone, out var values) ? values : Array.Empty<int>())
            {
                if (!visited.Add(adjacent)) continue;
                if (adjacent == target) return current.Distance + 1;
                queue.Enqueue((adjacent, current.Distance + 1));
            }
        }
        return 1;
    }

    private static bool FitsHardpoint(AetheriaRuntimeCatalogItem item, AetheriaRuntimeHardpoint hardpoint)
    {
        if (!string.Equals(item.HardpointType, hardpoint.Type, StringComparison.Ordinal) ||
            item.OccupiedCells != hardpoint.OccupiedCells)
            return false;
        var target = Cells(hardpoint.ShapeCells);
        return RotatedCells(item, ParseRotation(hardpoint.Rotation)).All(target.Contains);
    }

    private static (int X, int Y, int Rotation)? FindFit(
        AetheriaRuntimeCatalogItem hull,
        AetheriaRuntimeCatalogItem item,
        HashSet<(int X, int Y)> occupied)
    {
        return FindFit(Cells(hull.ShapeCells), Math.Max(1, hull.ShapeWidth), Math.Max(1, hull.ShapeHeight), item, occupied);
    }

    private static (int X, int Y, int Rotation)? FindFit(
        HashSet<(int X, int Y)> targetCells,
        int width,
        int height,
        AetheriaRuntimeCatalogItem item,
        HashSet<(int X, int Y)> occupied)
    {
        for (var rotation = 0; rotation < 4; rotation++)
        {
            var cells = RotatedCells(item, rotation).ToArray();
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (cells.All(cell => targetCells.Contains((cell.X + x, cell.Y + y)) &&
                    !occupied.Contains((cell.X + x, cell.Y + y))))
                    return (x, y, rotation);
            }
        }
        return null;
    }

    private static void Reserve(HashSet<(int X, int Y)> occupied, AetheriaRuntimeCatalogItem item, int x, int y, int rotation)
    {
        foreach (var cell in RotatedCells(item, rotation)) occupied.Add((cell.X + x, cell.Y + y));
    }

    private static HashSet<(int X, int Y)> Cells(IReadOnlyList<AetheriaRuntimeShapeCell>? cells) =>
        (cells ?? Array.Empty<AetheriaRuntimeShapeCell>()).Select(value => (value.X, value.Y)).ToHashSet();

    private static IEnumerable<(int X, int Y)> RotatedCells(AetheriaRuntimeCatalogItem item, int rotation)
    {
        foreach (var cell in item.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
        {
            yield return rotation switch
            {
                1 => (item.ShapeHeight - 1 - cell.Y, cell.X),
                2 => (item.ShapeWidth - 1 - cell.X, item.ShapeHeight - 1 - cell.Y),
                3 => (cell.Y, item.ShapeWidth - 1 - cell.X),
                _ => (cell.X, cell.Y)
            };
        }
    }

    private static int ParseRotation(string value) => value switch
    {
        "Clockwise" or "Right" or "Rotate90" => 1,
        "Half" or "Rotate180" => 2,
        "CounterClockwise" or "Left" or "Rotate270" => 3,
        _ => 0
    };

    private static bool HasBehavior(AetheriaRuntimeCatalogItem item, string kind) =>
        (item.BehaviorKinds ?? Array.Empty<string>()).Contains(kind, StringComparer.Ordinal);

    private static bool IsGear(AetheriaRuntimeCatalogItem item) =>
        string.Equals(item.Category, AetheriaRuntimeItemCategories.Gear, StringComparison.Ordinal) ||
        string.Equals(item.Category, AetheriaRuntimeItemCategories.Weapon, StringComparison.Ordinal);

    private static bool IsWeapon(AetheriaRuntimeCatalogItem? item) =>
        item != null &&
        (string.Equals(item.Category, AetheriaRuntimeItemCategories.Weapon, StringComparison.Ordinal) ||
         (item.BehaviorKinds ?? Array.Empty<string>()).Any(kind =>
             kind.Contains("Weapon", StringComparison.Ordinal)));

    private static AetheriaEntityItemSlot Slot(int x, int y, string itemKey) => new()
    {
        Position = new AetheriaGridCoord { X = x, Y = y }, ItemKey = itemKey,
        Quality = 1, Durability = 1, Quantity = 1, Enabled = true
    };

    private static AetheriaLoadoutItemSlot CargoSlot(int index, string itemKey) =>
        CargoSlot(index % 4, index / 4, itemKey);

    private static AetheriaLoadoutItemSlot CargoSlot(int x, int y, string itemKey) => new()
    {
        Position = new AetheriaGridCoord { X = x, Y = y },
        Item = new AetheriaLoadoutItem { ItemKey = itemKey, Quality = 1, Durability = 1, Quantity = 1, Enabled = true }
    };
}

public sealed record AetheriaDaemonLoadout(
    string HullItemKey,
    AetheriaEntityItemSlot[] Equipment,
    AetheriaLoadoutItemSlot[] Cargo,
    int[][] WeaponGroups,
    AetheriaLoadoutGenerationReceipt Receipt);
