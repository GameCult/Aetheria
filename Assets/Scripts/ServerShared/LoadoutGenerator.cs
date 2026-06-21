using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using Random = CultMath.Random;

public class LoadoutGenerator
{
    private enum RuntimeItemCandidateKind
    {
        Equipment,
        Gear,
        CargoBay,
        DockingBay,
        Hull
    }

    public Random Random;
    public ItemManager ItemManager { get; }
    public AetheriaRuntimeCatalogSnapshot RuntimeCatalog { get; }
    public Galaxy Galaxy { get; }
    public GalaxyZone Zone { get; }
    public Faction Faction { get; }
    public float PriceExponent { get; }

    public LoadoutGenerator(
        ref Random random, 
        ItemManager itemManager, 
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Galaxy galaxy, 
        GalaxyZone zone, 
        Faction faction, 
        float priceExponent)
    {
        Random = random;
        ItemManager = itemManager;
        RuntimeCatalog = runtimeCatalog ?? throw new InvalidOperationException("Loadout generation requires the typed Aetheria runtime catalog.");
        Galaxy = galaxy;
        Zone = zone;
        Faction = faction;
        PriceExponent = priceExponent;
    }
    //
    // public LoadoutGenerator(
    //     ref Random random, 
    //     ItemManager itemManager, 
    //     Faction faction, 
    //     float priceExponent)
    // {
    //     Random = random;
    //     ItemManager = itemManager;
    //     Faction = faction;
    //     PriceExponent = priceExponent;
    // }
    
    public EntityConstructionBlueprint GenerateShipLoadout(Predicate<AetheriaRuntimeCatalogItem> hullFilter = null)
    {
        var hullRow = RandomHull(HullType.Ship, hullFilter);
        if(hullRow==null)
        {
            ItemManager.Log("Unable to generate ship loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateEquippableInstance(hullRow);
        if(hull==null)
            ItemManager.Log("WHAT???");
        var entity = new Ship(ItemManager, null, hull, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        OutfitEntity(entity);
        return EntityConstructionBlueprintProjector.CaptureBlueprint(entity);
    }

    public OrbitalEntityConstructionBlueprint GenerateTurretLoadout()
    {
        var hullRow = RandomHull(HullType.Turret);
        if(hullRow==null)
        {
            ItemManager.Log("Unable to generate turret loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateEquippableInstance(hullRow);
        var entity = new OrbitalEntity(ItemManager, null, hull, "", ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        OutfitEntity(entity);
        return EntityConstructionBlueprintProjector.CaptureBlueprint(entity) as OrbitalEntityConstructionBlueprint;
    }

    public OrbitalEntityConstructionBlueprint GenerateStationLoadout()
    {
        var hullRow = RandomHull(HullType.Station);
        if(hullRow==null)
        {
            ItemManager.Log("Unable to generate station loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateEquippableInstance(hullRow);
        var entity = new OrbitalEntity(ItemManager, null, hull, "", ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        
        var emptyShape = entity.UnoccupiedSpace;
        
        var dockingBayRow = RandomCatalogItem(RuntimeItemCandidateKind.DockingBay, 2, item => FitsWithin(item, emptyShape));
        if (dockingBayRow == null) throw new InvalidLoadoutException("No compatible docking bay found for station!");

        ToShape(dockingBayRow).FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var dockingBay = ItemManager.CreateEquippableInstance(dockingBayRow);
        dockingBay.Rotation = cargoRotation;
        if (!entity.TryEquip(dockingBay, cargoPosition))
        {
            throw new InvalidLoadoutException("Failed to equip selected docking bay!");
        }
        
        OutfitEntity(entity);

        var cargo = entity.CargoBays.First();
        IEnumerable<AetheriaRuntimeCatalogItem> inventory = RandomCatalogItems(RuntimeItemCandidateKind.Equipment, 16, 1,
                item => item.Category != AetheriaRuntimeItemCategories.CargoBay && item.Category != AetheriaRuntimeItemCategories.DockingBay &&
                    (item.Category != AetheriaRuntimeItemCategories.Hull || item.HullType == nameof(HullType.Ship)));
        inventory = inventory
            .Where(item => item != null);
        inventory = inventory
            .OrderByDescending(item=>item.OccupiedCells);
        foreach (var item in inventory)
        {
            var instance = ItemManager.CreateEquippableInstance(item);
            cargo.TryStore(instance);
        }

        entity.CanTow = hullRow.HullCanTow;
        
        return EntityConstructionBlueprintProjector.CaptureBlueprint(entity) as OrbitalEntityConstructionBlueprint;
    }

    public AetheriaRuntimeCatalogItem RandomHull(HullType type, Predicate<AetheriaRuntimeCatalogItem> hullFilter = null)
    {
        return RandomCatalogItem(RuntimeItemCandidateKind.Hull, 0, item =>
            string.Equals(item.HullType, type.ToString(), StringComparison.Ordinal) &&
            (hullFilter?.Invoke(item) ?? true));
    }
    
    private AetheriaRuntimeCatalogItem[] RandomCatalogItems(
        RuntimeItemCandidateKind candidateKind,
        int count,
        float sizeExponent,
        Predicate<AetheriaRuntimeCatalogItem> typedFilter = null)
    {
        return RuntimeCatalog.EquipmentItems
            .Where(item => IsTypedCandidate(item, candidateKind))
            .Where(item =>
                item.Price > 0 &&
                !string.IsNullOrWhiteSpace(item.ManufacturerKey) &&
                (Galaxy.IsPrelude || Galaxy.ContainsFaction(item.ManufacturerKey) &&
                    (Faction == null || Faction.AllegianceByKey.ContainsKey(item.ManufacturerKey))) &&
                (typedFilter?.Invoke(item) ?? true))
            .WeightedRandomElements(ref Random, item =>
            {
                var manufacturerKey = item.ManufacturerKey;
                if (string.IsNullOrWhiteSpace(manufacturerKey))
                {
                    return 0;
                }

                var allegianceWeight = Faction == null
                    ? 1
                    : string.Equals(manufacturerKey, Faction.FactionKey, StringComparison.OrdinalIgnoreCase)
                        ? 1
                        : Faction.AllegianceByKey.TryGetValue(manufacturerKey, out var allegiance)
                            ? allegiance / ManufacturerDistancePenalty(manufacturerKey)
                            : 0;
                return allegianceWeight *
                       MathF.Pow(item.OccupiedCells, sizeExponent) / // Prioritize larger items
                       MathF.Pow(item.Price, PriceExponent); // Penalize item price to a controllable degree
            },
                count
            );
    }

    private static bool IsTypedCandidate(AetheriaRuntimeCatalogItem item, RuntimeItemCandidateKind candidateKind)
    {
        switch (candidateKind)
        {
            case RuntimeItemCandidateKind.Equipment:
                return !string.IsNullOrWhiteSpace(item.HardpointType);
            case RuntimeItemCandidateKind.Gear:
                return item.Category == AetheriaRuntimeItemCategories.Gear || item.Category == AetheriaRuntimeItemCategories.Weapon;
            case RuntimeItemCandidateKind.CargoBay:
                return item.Category == AetheriaRuntimeItemCategories.CargoBay || item.Category == AetheriaRuntimeItemCategories.DockingBay;
            case RuntimeItemCandidateKind.DockingBay:
                return item.Category == AetheriaRuntimeItemCategories.DockingBay;
            case RuntimeItemCandidateKind.Hull:
                return item.Category == AetheriaRuntimeItemCategories.Hull;
            default:
                throw new ArgumentOutOfRangeException(nameof(candidateKind), candidateKind, null);
        }
    }

    private float ManufacturerDistancePenalty(string manufacturerKey)
    {
        if (Galaxy == null || Zone == null || !Galaxy.ContainsFaction(manufacturerKey))
        {
            return 1;
        }

        var faction = Galaxy.ResolveFactionByKey(manufacturerKey);
        return faction != null && Galaxy.HomeZones.TryGetValue(faction, out var homeZone)
            ? Zone.Distance[homeZone]
            : 1;
    }

    private AetheriaRuntimeCatalogItem RandomCatalogItem(
        RuntimeItemCandidateKind candidateKind,
        float sizeExponent,
        Predicate<AetheriaRuntimeCatalogItem> typedFilter = null)
    {
        return RandomCatalogItems(candidateKind, 1, sizeExponent, typedFilter).FirstOrDefault();
    }
    
    private AetheriaRuntimeCatalogItem RandomCatalogItem(
        RuntimeItemCandidateKind candidateKind,
        AetheriaRuntimeHardpoint hardpoint,
        float sizeExponent,
        Predicate<AetheriaRuntimeCatalogItem> filter = null)
    {
        return RandomCatalogItem(
            candidateKind,
            sizeExponent,
            item => FitsHardpoint(item, hardpoint) && (filter?.Invoke(item) ?? true));
    }

    private static bool FitsHardpoint(AetheriaRuntimeCatalogItem item, AetheriaRuntimeHardpoint hardpoint)
    {
        return item != null &&
               hardpoint != null &&
               string.Equals(item.HardpointType, hardpoint.Type, StringComparison.Ordinal) &&
               item.OccupiedCells == hardpoint.OccupiedCells &&
               FitsWithin(item, ToShape(hardpoint), GetRotation(hardpoint));
    }

    private static bool HasBehaviorKind(AetheriaRuntimeCatalogItem item, string behaviorKind)
    {
        return !string.IsNullOrWhiteSpace(behaviorKind) &&
               item.BehaviorKinds.Contains(behaviorKind, StringComparer.Ordinal);
    }

    private static bool FitsWithin(AetheriaRuntimeCatalogItem item, Shape target)
    {
        if (item == null)
        {
            return false;
        }

        return ToShape(item).FitsWithin(target, out _, out _);
    }

    private static bool FitsWithin(AetheriaRuntimeCatalogItem item, Shape target, ItemRotation rotation)
    {
        if (item == null)
        {
            return false;
        }

        return ToShape(item).FitsWithin(target, rotation, out _);
    }

    private static Shape ToShape(AetheriaRuntimeCatalogItem item)
    {
        if (item == null)
        {
            throw new InvalidLoadoutException("Typed catalog shape was requested before a compatible item was selected.");
        }

        return ToShape(item.ShapeWidth, item.ShapeHeight, item.ShapeCells);
    }

    private static Shape ToShape(AetheriaRuntimeHardpoint hardpoint)
    {
        if (hardpoint == null)
        {
            throw new InvalidLoadoutException("Typed hardpoint shape was requested before a hardpoint was selected.");
        }

        return ToShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells);
    }

    private static Shape ToShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(Math.Max(width, 1), Math.Max(height, 1));
        foreach (var cell in cells)
        {
            shape.SetCell(cell.X, cell.Y, true);
        }

        return shape;
    }

    private static ItemRotation GetRotation(AetheriaRuntimeHardpoint hardpoint)
    {
        return Enum.TryParse(hardpoint.Rotation, out ItemRotation rotation)
            ? rotation
            : ItemRotation.None;
    }

    private void OutfitEntity(Entity entity)
    {
        var typedHull = ItemManager.GetRuntimeItem(entity.Hull);
        if (typedHull == null)
        {
            throw new InvalidLoadoutException("Selected hull is missing from the typed runtime catalog.");
        }

        foreach (var v in ToShape(typedHull).Coordinates) entity.HullConductivity[v.x, v.y] = true;
        var previousItems = new List<AetheriaRuntimeCatalogItem>();
        foreach (var hardpoint in typedHull.Hardpoints.OrderByDescending(h=>h.OccupiedCells))
        {
            if (hardpoint.Type == nameof(HardpointType.ControlModule))
            {
                var controllerBehaviorKind = entity is Ship
                    ? "Cockpit"
                    : entity is OrbitalEntity
                        ? "TurretController"
                        : null;
                var controllerRow = RandomCatalogItem(RuntimeItemCandidateKind.Gear, hardpoint, 2, item => HasBehaviorKind(item, controllerBehaviorKind));
                if (controllerRow == null)
                    throw new InvalidLoadoutException("No compatible controller found for entity!");
                var controller = ItemManager.CreateEquippableInstance(controllerRow);
                if (!entity.TryEquip(controller))
                {
                    throw new InvalidLoadoutException($"Failed to equip selected {hardpoint.Type}!");
                }
            }
            else
            {
                // If a previously selected item fits, use that one (this is why we must process larger hardpoints first)
                var itemRow = previousItems.FirstOrDefault(i => FitsHardpoint(i, hardpoint));
                var previousItem = itemRow == null
                    ? null
                    : entity.Equipment.FirstOrDefault(item => item.EquippableItem.ItemKey == itemRow.ItemKey);
                itemRow ??= RandomCatalogItem(RuntimeItemCandidateKind.Gear, hardpoint, 2);
                if (itemRow == null) ItemManager.Log($"No compatible item found for entity {hardpoint.Type} hardpoint!");
                else
                {
                    //throw new InvalidLoadoutException($"No compatible item found for entity {hardpoint.Type} hardpoint!");
                    EquippableItem item;
                    if(previousItem!=null)
                        item = ItemManager.CreateEquippableInstance(itemRow, previousItem.EquippableItem.Quality);
                    else item = ItemManager.CreateEquippableInstance(itemRow);
                    if (!entity.TryEquip(item))
                    {
                        throw new InvalidLoadoutException($"Failed to equip selected {hardpoint.Type}!");
                    }
                    previousItems.Add(itemRow);
                }
            }
        }

        var emptyShape = entity.UnoccupiedSpace;
        
        var cargoRow = RandomCatalogItem(RuntimeItemCandidateKind.CargoBay, 3, item => item.Category != AetheriaRuntimeItemCategories.DockingBay && FitsWithin(item, emptyShape));
        if (cargoRow == null) throw new InvalidLoadoutException("No compatible cargo bay found for entity!");

        ToShape(cargoRow).FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var cargo = ItemManager.CreateEquippableInstance(cargoRow);
        cargo.Rotation = cargoRotation;
        if (!entity.TryEquip(cargo, cargoPosition))
            throw new InvalidLoadoutException("Failed to equip selected cargo bay!");

        emptyShape = entity.UnoccupiedSpace;

        var capacitorRow = RandomCatalogItem(RuntimeItemCandidateKind.Gear, 2,
            item => item.BehaviorKinds.Contains("Capacitor", StringComparer.Ordinal) &&
                    FitsWithin(item, emptyShape));
        if (capacitorRow == null) throw new InvalidLoadoutException("No compatible capacitor found for entity!");

        ToShape(capacitorRow).FitsWithin(emptyShape, out var capacitorRotation, out var capacitorPosition);
        var capacitor = ItemManager.CreateEquippableInstance(capacitorRow);
        capacitor.Rotation = capacitorRotation;
        if (!entity.TryEquip(capacitor, capacitorPosition))
            throw new InvalidLoadoutException("Failed to equip selected capacitor!");
    }
}

public class InvalidLoadoutException : Exception
{
    public InvalidLoadoutException()
    {
    }

    public InvalidLoadoutException(string message)
        : base(message)
    {
    }

    public InvalidLoadoutException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
