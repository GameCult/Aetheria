using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class LoadoutGenerator
{
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
    
    public RuntimeEntityBlueprint GenerateShipLoadout(Predicate<AetheriaRuntimeCatalogItem> hullFilter = null)
    {
        var hullData = RandomHull(HullType.Ship, hullFilter);
        if(hullData==null)
        {
            ItemManager.Log("Unable to generate ship loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateInstance(hullData) as EquippableItem;
        if(hull==null)
            ItemManager.Log("WHAT???");
        var entity = new Ship(ItemManager, null, hull, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        OutfitEntity(entity);
        return RuntimeEntityBlueprintProjector.CaptureBlueprint(entity);
    }

    public RuntimeOrbitalEntityBlueprint GenerateTurretLoadout()
    {
        var hullData = RandomHull(HullType.Turret);
        if(hullData==null)
        {
            ItemManager.Log("Unable to generate turret loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateInstance(hullData) as EquippableItem;
        var entity = new OrbitalEntity(ItemManager, null, hull, Guid.Empty, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        OutfitEntity(entity);
        return RuntimeEntityBlueprintProjector.CaptureBlueprint(entity) as RuntimeOrbitalEntityBlueprint;
    }

    public RuntimeOrbitalEntityBlueprint GenerateStationLoadout()
    {
        var hullData = RandomHull(HullType.Station);
        if(hullData==null)
        {
            ItemManager.Log("Unable to generate station loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateInstance(hullData) as EquippableItem;
        var entity = new OrbitalEntity(ItemManager, null, hull, Guid.Empty, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        
        var emptyShape = entity.UnoccupiedSpace;
        
        var dockingBayRow = RandomCatalogItem<DockingBayData>(2, item => FitsWithin(item, emptyShape));
        var dockingBayData = ProjectRuntimeItem<DockingBayData>(dockingBayRow);
        if (dockingBayData == null) throw new InvalidLoadoutException("No compatible docking bay found for station!");

        ToShape(dockingBayRow).FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var dockingBay = ItemManager.CreateInstance(dockingBayData) as EquippableItem;
        dockingBay.Rotation = cargoRotation;
        if (!entity.TryEquip(dockingBay, cargoPosition))
        {
            throw new InvalidLoadoutException("Failed to equip selected docking bay!");
        }
        
        OutfitEntity(entity);

        var cargo = entity.CargoBays.First();
        IEnumerable<EquippableItemData> inventory = RandomCatalogItems<EquippableItemData>(16, 1,
                item => item.Category != "CargoBayData" && item.Category != "DockingBayData" &&
                    (item.Category != "HullData" || item.HullType == nameof(HullType.Ship)));
        inventory = inventory
            .Select(ProjectRuntimeItem<EquippableItemData>)
            .Where(item => item != null);
        inventory = inventory
            .OrderByDescending(item=>item.Shape.Coordinates.Length);
        foreach (var item in inventory)
        {
            var instance = ItemManager.CreateInstance(item);
            cargo.TryStore(instance);
        }

        entity.CanTow = hullData.CanTow;
        
        return RuntimeEntityBlueprintProjector.CaptureBlueprint(entity) as RuntimeOrbitalEntityBlueprint;
    }

    public HullData RandomHull(HullType type, Predicate<AetheriaRuntimeCatalogItem> hullFilter = null)
    {
        var hullRow = RandomCatalogItem<HullData>(0, item =>
            string.Equals(item.HullType, type.ToString(), StringComparison.Ordinal) &&
            (hullFilter?.Invoke(item) ?? true));
        return ProjectRuntimeItem<HullData>(hullRow);
    }
    
    public AetheriaRuntimeCatalogItem[] RandomCatalogItems<T>(
        int count,
        float sizeExponent,
        Predicate<AetheriaRuntimeCatalogItem> typedFilter = null) where T : EquippableItemData
    {
        return RuntimeCatalog.EquipmentItems
            .Where(IsTypedCandidate<T>)
            .Where(item =>
                item.Price > 0 &&
                Guid.TryParse(item.ManufacturerLegacyId, out var manufacturer) &&
                manufacturer != Guid.Empty &&
                (Galaxy.IsPrelude || Galaxy.ContainsFaction(manufacturer) &&
                    (Faction == null || Faction.Allegiance.ContainsKey(manufacturer))) &&
                (typedFilter?.Invoke(item) ?? true))
            .WeightedRandomElements(ref Random, item =>
            {
                if (!Guid.TryParse(item.ManufacturerLegacyId, out var manufacturer))
                {
                    return 0;
                }

                var allegianceWeight = Faction == null
                    ? 1
                    : manufacturer == Faction.ID
                        ? 1
                        : Faction.Allegiance.TryGetValue(manufacturer, out var allegiance)
                            ? allegiance / ManufacturerDistancePenalty(manufacturer)
                            : 0;
                return allegianceWeight *
                       pow(item.OccupiedCells, sizeExponent) / // Prioritize larger items
                       pow(item.Price, PriceExponent); // Penalize item price to a controllable degree
            },
                count
            );
    }

    private static bool IsTypedCandidate<T>(AetheriaRuntimeCatalogItem item) where T : EquippableItemData
    {
        var requestedType = typeof(T);
        if (requestedType == typeof(EquippableItemData))
        {
            return !string.IsNullOrWhiteSpace(item.HardpointType);
        }

        if (requestedType == typeof(GearData))
        {
            return item.Category == "GearData" || item.Category == "WeaponItemData";
        }

        if (requestedType == typeof(CargoBayData))
        {
            return item.Category == "CargoBayData" || item.Category == "DockingBayData";
        }

        return string.Equals(item.Category, requestedType.Name, StringComparison.Ordinal);
    }

    private T ProjectRuntimeItem<T>(AetheriaRuntimeCatalogItem item) where T : EquippableItemData
    {
        if (item == null)
        {
            return null;
        }

        return Guid.TryParse(item.LegacyId, out var legacyId)
            ? ItemManager.GetRuntimeItemProjection<T>(legacyId)
            : null;
    }

    private float ManufacturerDistancePenalty(Guid manufacturer)
    {
        if (Galaxy == null || Zone == null || !Galaxy.ContainsFaction(manufacturer))
        {
            return 1;
        }

        var faction = Galaxy.ResolveFaction(manufacturer);
        return faction != null && Galaxy.HomeZones.TryGetValue(faction, out var homeZone)
            ? Zone.Distance[homeZone]
            : 1;
    }

    public AetheriaRuntimeCatalogItem RandomCatalogItem<T>(
        float sizeExponent,
        Predicate<AetheriaRuntimeCatalogItem> typedFilter = null) where T : EquippableItemData
    {
        return RandomCatalogItems<T>(1, sizeExponent, typedFilter).FirstOrDefault();
    }
    
    public AetheriaRuntimeCatalogItem RandomCatalogItem<T>(AetheriaRuntimeHardpoint hardpoint, float sizeExponent, Predicate<AetheriaRuntimeCatalogItem> filter = null) where T : EquippableItemData
    {
        return RandomCatalogItem<T>(
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
            shape[int2(cell.X, cell.Y)] = true;
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
                    ? nameof(CockpitData)
                    : entity is OrbitalEntity
                        ? nameof(TurretControllerData)
                        : null;
                var controllerRow = RandomCatalogItem<GearData>(hardpoint, 2, item => HasBehaviorKind(item, controllerBehaviorKind));
                var controllerData = ProjectRuntimeItem<GearData>(controllerRow);
                if (controllerData == null) 
                    throw new InvalidLoadoutException("No compatible controller found for entity!");
                var controller = ItemManager.CreateInstance(controllerData) as EquippableItem;
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
                    : Guid.TryParse(itemRow.LegacyId, out var previousItemId)
                        ? entity.Equipment.FirstOrDefault(item => item.EquippableItem.Data.ItemId == previousItemId)
                        : null;
                itemRow ??= RandomCatalogItem<GearData>(hardpoint, 2);
                var itemData = ProjectRuntimeItem<GearData>(itemRow);
                if (itemData == null) ItemManager.Log($"No compatible item found for entity {hardpoint.Type} hardpoint!");
                else
                {
                    //throw new InvalidLoadoutException($"No compatible item found for entity {hardpoint.Type} hardpoint!");
                    EquippableItem item;
                    if(previousItem!=null)
                        item = ItemManager.CreateInstance(itemData, previousItem.EquippableItem.Quality) as EquippableItem;
                    else item = ItemManager.CreateInstance(itemData) as EquippableItem;
                    if (!entity.TryEquip(item))
                    {
                        throw new InvalidLoadoutException($"Failed to equip selected {hardpoint.Type}!");
                    }
                    previousItems.Add(itemRow);
                }
            }
        }

        var emptyShape = entity.UnoccupiedSpace;
        
        var cargoRow = RandomCatalogItem<CargoBayData>(3, item => item.Category != "DockingBayData" && FitsWithin(item, emptyShape));
        var cargoData = ProjectRuntimeItem<CargoBayData>(cargoRow);
        if (cargoData == null) throw new InvalidLoadoutException("No compatible cargo bay found for entity!");

        ToShape(cargoRow).FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var cargo = ItemManager.CreateInstance(cargoData) as EquippableItem;
        cargo.Rotation = cargoRotation;
        if (!entity.TryEquip(cargo, cargoPosition))
            throw new InvalidLoadoutException("Failed to equip selected cargo bay!");

        emptyShape = entity.UnoccupiedSpace;

        var capacitorRow = RandomCatalogItem<GearData>(2,
            item => item.BehaviorKinds.Contains(nameof(CapacitorData), StringComparer.Ordinal) &&
                    FitsWithin(item, emptyShape));
        var capacitorData = ProjectRuntimeItem<GearData>(capacitorRow);
        if (capacitorData == null) throw new InvalidLoadoutException("No compatible capacitor found for entity!");

        ToShape(capacitorRow).FitsWithin(emptyShape, out var capacitorRotation, out var capacitorPosition);
        var capacitor = ItemManager.CreateInstance(capacitorData) as EquippableItem;
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
