using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

public static class EntitySerializer
{
    public static RuntimeEntityBlueprint CaptureBlueprint(Entity entity)
    {
        RuntimeEntityBlueprint blueprint;
        if (entity is OrbitalEntity orbital)
            blueprint = new RuntimeOrbitalEntityBlueprint
            {
                Orbit = orbital.OrbitData,
                SecurityLevel = orbital.SecurityLevel,
                SecurityRadius = orbital.SecurityRadius
            };
        else if (entity is Ship ship)
            blueprint = new RuntimeShipBlueprint
            {
                Position = ship.Position,
                Direction = ship.Direction,
                IsPlayerShip = ship.IsPlayerShip
            };
        else throw new ArgumentException("Attempted to capture an instance of abstract class Entity!");

        blueprint.Settings = entity.Settings;
        
        // Filter item behavior collections by those with any persistent behaviors
        // For each item create an object containing the item position and a list of persistent behaviors
        // Then turn that into a dictionary mapping from item position to an array of every behaviors persistent data
        blueprint.PersistedBehaviors = entity.Equipment
            .Where(item => item.Behaviors.Any(b=>b is IPersistentBehavior))
            .Select(item => new {equippable=item, behaviors = item.Behaviors
                .Where(b=>b is IPersistentBehavior)
                .Cast<IPersistentBehavior>()})
            .ToDictionary(x=> x.equippable.Position, x=>x.behaviors.Select(b => b.Store()).ToArray());

        blueprint.Hull = entity.Hull;
        blueprint.Name = entity.Name;
        blueprint.Faction = entity.Faction?.ID ?? Guid.Empty;
        blueprint.Equipment = entity.Equipment.Select(e => (e.Position, e.EquippableItem)).ToArray();
        blueprint.CargoBays = entity.CargoBays.Select(e => (e.Position, e.EquippableItem)).ToArray();
        blueprint.DockingBays = entity.DockingBays.Select(e => (e.Position, e.EquippableItem)).ToArray();
        blueprint.DockingBayAssignments = entity.DockingBays.Select(x => entity.Children.IndexOf(x.DockedShip)).ToArray();
        blueprint.CargoContents = entity.CargoBays.Select(b => b.Cargo.Select(i => (i.Value, i.Key)).ToArray()).ToArray();
        blueprint.DockingBayContents = entity.DockingBays.Select(b => b.Cargo.Select(i => (i.Value, i.Key)).ToArray()).ToArray();
        blueprint.Armor = entity.Armor;
        blueprint.Temperature = entity.Temperature;
        blueprint.Conductivity = entity.HullConductivity;
        blueprint.Children = entity.Children.Select(CaptureBlueprint).ToArray();
        blueprint.WeaponGroups = entity.WeaponGroups.Select(wg => wg.items.Select(item => entity.Equipment.IndexOf(item)).ToArray()).ToArray();
        return blueprint;
    }

    public static Entity InstantiateFromBlueprint(ItemManager itemManager, Zone zone, RuntimeEntityBlueprint blueprint, bool instantiate = false)
    {
        blueprint.Settings ??= itemManager.GameplaySettings.DefaultEntitySettings.Copy();
        return blueprint switch
        {
            RuntimeShipBlueprint shipBlueprint => InstantiateFromBlueprint(itemManager, zone, shipBlueprint, instantiate),
            RuntimeOrbitalEntityBlueprint orbitalBlueprint => InstantiateFromBlueprint(itemManager, zone, orbitalBlueprint, instantiate),
            _ => null
        };
    }


    private static Ship InstantiateFromBlueprint(ItemManager itemManager, Zone zone, RuntimeShipBlueprint blueprint, bool instantiate = false)
    {
        
        var entity = new Ship(itemManager, zone, instantiate ? (EquippableItem) itemManager.Instantiate(blueprint.Hull) : blueprint.Hull, blueprint.Settings);
        Restore(itemManager, zone, blueprint, entity, instantiate);
        entity.Position = blueprint.Position;
        entity.Direction = blueprint.Direction;
        entity.IsPlayerShip = blueprint.IsPlayerShip;
        return entity;
    }

    private static OrbitalEntity InstantiateFromBlueprint(ItemManager itemManager, Zone zone, RuntimeOrbitalEntityBlueprint blueprint, bool instantiate = false)
    {
        var entity = new OrbitalEntity(itemManager, zone, instantiate ? (EquippableItem) itemManager.Instantiate(blueprint.Hull) : blueprint.Hull, blueprint.Orbit, blueprint.Settings);
        Restore(itemManager, zone, blueprint, entity, instantiate);
        entity.SecurityLevel = blueprint.SecurityLevel;
        entity.SecurityRadius = blueprint.SecurityRadius;
        if (blueprint.Story >= 0) entity.Story = zone.GalaxyZone.Locations[blueprint.Story];
        return entity;
    }

    private static void Restore(ItemManager itemManager, Zone zone, RuntimeEntityBlueprint blueprint, Entity entity, bool instantiate = false)
    {
        entity.Name = blueprint.Name;
        entity.Faction = zone?.Galaxy?.ResolveFaction(blueprint.Faction);
        entity.Children = blueprint.Children.Select(c =>
        {
            var child = InstantiateFromBlueprint(itemManager, zone, c, instantiate);
            child.Parent = entity;
            return child;
        }).ToList();
        foreach (var (position, item) in blueprint.Equipment) entity.TryEquip(instantiate ? (EquippableItem) itemManager.Instantiate(item) : item, position);
        foreach (var (position, item) in blueprint.CargoBays) entity.TryEquip(instantiate ? (EquippableItem) itemManager.Instantiate(item) : item, position);
        foreach (var (position, item) in blueprint.DockingBays) entity.TryEquip(instantiate ? (EquippableItem) itemManager.Instantiate(item) : item, position);

        for (var i = 0; i < blueprint.DockingBayAssignments.Length; i++)
        {
            if (blueprint.DockingBayAssignments[i] != -1)
                entity.DockingBays[i].DockedShip = entity.Children[blueprint.DockingBayAssignments[i]] as Ship;
        }

        for (var bayIndex = 0; bayIndex < blueprint.CargoContents.Length; bayIndex++)
            if(entity.CargoBays.Count >= bayIndex + 1)
                foreach (var (position, item) in blueprint.CargoContents[bayIndex])
                    entity.CargoBays[bayIndex].TryStore(instantiate ? itemManager.Instantiate(item) : item, position);

        for (var bayIndex = 0; bayIndex < blueprint.DockingBayContents.Length; bayIndex++)
            if(entity.DockingBays.Count >= bayIndex + 1)
                foreach (var (position, item) in blueprint.DockingBayContents[bayIndex])
                    entity.DockingBays[bayIndex].TryStore(instantiate ? itemManager.Instantiate(item) : item, position);

        // Iterate only over the behaviors of items which contain persistent data
        // Filter the behaviors for each item to get the persistent ones, then cast them and combine with the persisted data array for that item
        foreach (var persistentBehaviorData in entity.Equipment
            .Where(item => blueprint.PersistedBehaviors.ContainsKey(item.Position))
            .SelectMany(item => item.Behaviors
                .Where(b=> b is IPersistentBehavior)
                .Cast<IPersistentBehavior>()
                .Zip(blueprint.PersistedBehaviors[item.Position], (behavior, data) => new{behavior, data})))
            persistentBehaviorData.behavior.Restore(persistentBehaviorData.data);

        if(!instantiate)
            entity.Temperature = blueprint.Temperature;
        
        if(!instantiate)
            entity.Armor = blueprint.Armor;
        
        var hullData = itemManager.GetData(entity.Hull);
        
        foreach(var v in hullData.Shape.Coordinates)
            entity.HullConductivity[v.x,v.y] = blueprint.Conductivity[v.x,v.y];

        entity.WeaponGroups = blueprint.WeaponGroups.Select(itemIndices =>
        {
            var items = itemIndices.Select(i => entity.Equipment[i]);
            return (items.Select(i => i.GetBehavior<Weapon>()).ToList(), items.ToList());
        }).ToArray();
    }
}

public class RuntimeShipBlueprint : RuntimeEntityBlueprint
{
    public float3 Position;
    public float2 Direction;
    public bool IsPlayerShip;
}

public class RuntimeOrbitalEntityBlueprint : RuntimeEntityBlueprint
{
    public Guid Orbit;
    public int Story = -1;
    public SecurityLevel SecurityLevel;
    public float SecurityRadius;
}

public abstract class RuntimeEntityBlueprint
{
    public string Name;
    public EquippableItem Hull;
    public (int2 position, EquippableItem item)[] Equipment;
    public (int2 position, EquippableItem item)[] CargoBays;
    public (int2 position, EquippableItem item)[] DockingBays;
    public Dictionary<int2, PersistentBehaviorData[]> PersistedBehaviors;
    public float[,] Temperature;
    public float[,] Armor;
    public bool2[,] Conductivity;
    public int[] DockingBayAssignments;
    public (int2 position, ItemInstance item)[][] CargoContents;
    public (int2 position, ItemInstance item)[][] DockingBayContents;
    public RuntimeEntityBlueprint[] Children;
    public EntitySettings Settings;
    public Guid Faction;
    public int[][] WeaponGroups;

    private int _price;

    public int Price(ItemManager itemManager)
    {
        if (_price != 0) return _price;
        
        var hullData = itemManager.GetData(Hull);
        _price = hullData.Price;

        foreach (var (_, item) in Equipment)
        {
            var itemData = itemManager.GetData(item);
            _price += itemData.Price;
        }
        foreach (var (_, item) in CargoBays)
        {
            var itemData = itemManager.GetData(item);
            _price += itemData.Price;
        }
        foreach (var (_, item) in DockingBays)
        {
            var itemData = itemManager.GetData(item);
            _price += itemData.Price;
        }

        foreach (var t in CargoContents)
        {
            foreach (var (_, item) in t)
            {
                var itemData = itemManager.GetData(item);
                if (item is SimpleCommodity s)
                    _price += itemData.Price * s.Quantity;
                else
                    _price += itemData.Price;
            }
        }

        foreach (var t in DockingBayContents)
        {
            foreach (var (_, item) in t)
            {
                var itemData = itemManager.GetData(item);
                if (item is SimpleCommodity s)
                    _price += itemData.Price * s.Quantity;
                else
                    _price += itemData.Price;
            }
        }

        return _price;
    }
}
