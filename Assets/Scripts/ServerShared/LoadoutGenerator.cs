using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class LoadoutGenerator
{
    public Random Random;
    public ItemManager ItemManager { get; }
    public Galaxy Galaxy { get; }
    public GalaxyZone Zone { get; }
    public Faction Faction { get; }
    public float PriceExponent { get; }

    public LoadoutGenerator(
        ref Random random, 
        ItemManager itemManager, 
        Galaxy galaxy, 
        GalaxyZone zone, 
        Faction faction, 
        float priceExponent)
    {
        Random = random;
        ItemManager = itemManager;
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
    
    public EntityPack GenerateShipLoadout(Predicate<HullData> hullFilter = null)
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
        return EntitySerializer.Pack(entity);
    }

    public OrbitalEntityPack GenerateTurretLoadout()
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
        return EntitySerializer.Pack(entity) as OrbitalEntityPack;
    }

    public OrbitalEntityPack GenerateStationLoadout()
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
        
        var dockingBayData = RandomItem<DockingBayData>(2, item => item.Shape.FitsWithin(emptyShape, out _, out _));
        if (dockingBayData == null) throw new InvalidLoadoutException("No compatible docking bay found for station!");

        dockingBayData.Shape.FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var dockingBay = ItemManager.CreateInstance(dockingBayData) as EquippableItem;
        dockingBay.Rotation = cargoRotation;
        if (!entity.TryEquip(dockingBay, cargoPosition))
        {
            throw new InvalidLoadoutException("Failed to equip selected docking bay!");
        }
        
        OutfitEntity(entity);

        var cargo = entity.CargoBays.First();
        IEnumerable<EquippableItemData> inventory = RandomItems<EquippableItemData>(16, 1, 
            data => !(data is HullData hull && hull.HullType != HullType.Ship) && !(data is CargoBayData));
        inventory = inventory
            .OrderByDescending(item=>item.Shape.Coordinates.Length);
        foreach (var item in inventory)
        {
            var instance = ItemManager.CreateInstance(item);
            cargo.TryStore(instance);
        }

        entity.CanTow = hullData.CanTow;
        
        return EntitySerializer.Pack(entity) as OrbitalEntityPack;
    }

    public HullData RandomHull(HullType type, Predicate<HullData> hullFilter = null)
    {
        return RandomItem<HullData>(0, item => 
                (hullFilter?.Invoke(item) ?? true) &&
                item.HullType == type);
    }
    
    public T[] RandomItems<T>(int count, float sizeExponent, Predicate<T> filter = null) where T : EquippableItemData
    {
        return ItemManager.ItemData.GetAll<T>()
            .Where(item => 
                item.Price > 0 &&
                item.Manufacturer != Guid.Empty &&
                (Galaxy.IsPrelude || Galaxy.ContainsFaction(item.Manufacturer) &&
                    (Faction == null || Faction.Allegiance.ContainsKey(item.Manufacturer))) &&
                (filter?.Invoke(item) ?? true))
            .WeightedRandomElements(ref Random, item =>
                    Faction == null ? 1 : 
                        (item.Manufacturer == Faction.ID ? 1 : Faction.Allegiance.ContainsKey(item.Manufacturer) ? Faction.Allegiance[item.Manufacturer] : 0.0f / // Prioritize items from allied manufacturers
                            (Galaxy?.ContainsFaction(item.Manufacturer) ?? false ? Zone?.Distance[Galaxy.HomeZones[ItemManager.ItemData.Get<Faction>(item.Manufacturer)]] ?? 1 : 1)) * // Penalize distance to manufacturer headquarters
                    pow(item.Shape.Coordinates.Length, sizeExponent) / // Prioritize larger items
                    pow(item.Price, PriceExponent), // Penalize item price to a controllable degree
                count
            );
    }

    public T RandomItem<T>(float sizeExponent, Predicate<T> filter = null) where T : EquippableItemData
    {
        return RandomItems(1, sizeExponent, filter).FirstOrDefault();
    }
    
    public T RandomItem<T>(HardpointData hardpoint, float sizeExponent, Predicate<T> filter = null) where T : EquippableItemData
    {
        return RandomItem<T>(sizeExponent, item => item.HardpointType == hardpoint.Type &&
                                  (filter?.Invoke(item) ?? true) && 
                                  item.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _) &&
                                  item.Shape.Coordinates.Length==hardpoint.Shape.Coordinates.Length);
    }

    private void OutfitEntity(Entity entity)
    {
        var hullData = ItemManager.GetData(entity.Hull) as HullData;
        foreach (var v in hullData.Shape.Coordinates) entity.HullConductivity[v.x, v.y] = true;
        var previousItems = new List<EquippableItemData>();
        foreach (var hardpoint in hullData.Hardpoints.OrderByDescending(h=>h.Shape.Coordinates.Length))
        {
            if (hardpoint.Type == HardpointType.ControlModule)
            {
                var controllerData = RandomItem<GearData>(hardpoint, 2,
                    item => item.Behaviors.Any(b => entity is Ship && b is CockpitData || entity is OrbitalEntity && b is TurretControllerData));
                if (controllerData == null) 
                    throw new InvalidLoadoutException("No compatible controller found for entity!");
                var controller = ItemManager.CreateInstance(controllerData) as EquippableItem;
                if (!entity.TryEquip(controller))
                {
                    throw new InvalidLoadoutException($"Failed to equip selected {Enum.GetName(typeof(HardpointType), hardpoint.Type)}!");
                }
            }
            else
            {
                // If a previously selected item fits, use that one (this is why we must process larger hardpoints first)
                var itemData = previousItems
                    .FirstOrDefault(i => i.HardpointType == hardpoint.Type && i.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _));
                var previousItem = entity.Equipment.FirstOrDefault(item => item.Data == itemData);
                itemData ??= RandomItem<GearData>(hardpoint, 2);
                if (itemData == null) ItemManager.Log($"No compatible item found for entity {Enum.GetName(typeof(HardpointType), hardpoint.Type)} hardpoint!");
                else
                {
                    //throw new InvalidLoadoutException($"No compatible item found for entity {Enum.GetName(typeof(HardpointType), hardpoint.Type)} hardpoint!");
                    EquippableItem item;
                    if(previousItem!=null)
                        item = ItemManager.CreateInstance(itemData, previousItem.EquippableItem.Quality) as EquippableItem;
                    else item = ItemManager.CreateInstance(itemData) as EquippableItem;
                    if (!entity.TryEquip(item))
                    {
                        throw new InvalidLoadoutException($"Failed to equip selected {Enum.GetName(typeof(HardpointType), hardpoint.Type)}!");
                    }
                    previousItems.Add(itemData);
                }
            }
        }

        var emptyShape = entity.UnoccupiedSpace;
        
        var cargoData = RandomItem<CargoBayData>(3, item =>
            !(item is DockingBayData) &&
            item.Shape.FitsWithin(emptyShape, out _, out _));
        if (cargoData == null) throw new InvalidLoadoutException("No compatible cargo bay found for entity!");

        cargoData.Shape.FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var cargo = ItemManager.CreateInstance(cargoData) as EquippableItem;
        cargo.Rotation = cargoRotation;
        if (!entity.TryEquip(cargo, cargoPosition))
            throw new InvalidLoadoutException("Failed to equip selected cargo bay!");

        emptyShape = entity.UnoccupiedSpace;

        var capacitorData = RandomItem<GearData>(2, item =>
            item.Behaviors.Any(b => b is CapacitorData) &&
            item.Shape.FitsWithin(emptyShape, out _, out _));
        if (capacitorData == null) throw new InvalidLoadoutException("No compatible capacitor found for entity!");

        capacitorData.Shape.FitsWithin(emptyShape, out var capacitorRotation, out var capacitorPosition);
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