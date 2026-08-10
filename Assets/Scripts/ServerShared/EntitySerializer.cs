using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Unity.Mathematics;

public static class EntitySerializer
{
    public static EntityPack Pack(Entity entity)
    {
        EntityPack pack;
        if (entity is OrbitalEntity orbital)
            pack = new OrbitalEntityPack
            {
                Orbit = orbital.OrbitData,
                SecurityLevel = orbital.SecurityLevel,
                SecurityRadius = orbital.SecurityRadius
            };
        else if (entity is Ship ship)
            pack = new ShipPack
            {
                Position = ship.Position,
                Direction = ship.Direction,
                IsPlayerShip = ship.IsPlayerShip
            };
        else throw new ArgumentException("Attempted to pack an instance of abstract class Entity!");

        pack.Settings = entity.Settings;
        
        // Filter item behavior collections by those with any persistent behaviors
        // For each item create an object containing the item position and a list of persistent behaviors
        // Then turn that into a dictionary mapping from item position to an array of every behaviors persistent data
        pack.PersistedBehaviors = entity.Equipment
            .Where(item => item.Behaviors.Any(b=>b is IPersistentBehavior))
            .Select(item => new {equippable=item, behaviors = item.Behaviors
                .Where(b=>b is IPersistentBehavior)
                .Cast<IPersistentBehavior>()})
            .ToDictionary(x=> x.equippable.Position, x=>x.behaviors.Select(b => b.Store()).ToArray());

        pack.Hull = entity.Hull;
        pack.Name = entity.Name;
        pack.Faction = entity.Faction?.ID ?? Guid.Empty;
        pack.Equipment = entity.Equipment.Select(e => (e.Position, e.EquippableItem)).ToArray();
        pack.CargoBays = entity.CargoBays.Select(e => (e.Position, e.EquippableItem)).ToArray();
        pack.DockingBays = entity.DockingBays.Select(e => (e.Position, e.EquippableItem)).ToArray();
        pack.DockingBayAssignments = entity.DockingBays.Select(x => entity.Children.IndexOf(x.DockedShip)).ToArray();
        pack.CargoContents = entity.CargoBays.Select(b => b.Cargo.Select(i => (i.Value, i.Key)).ToArray()).ToArray();
        pack.DockingBayContents = entity.DockingBays.Select(b => b.Cargo.Select(i => (i.Value, i.Key)).ToArray()).ToArray();
        pack.Armor = entity.Armor;
        pack.Temperature = entity.Temperature;
        pack.Conductivity = entity.HullConductivity;
        pack.Children = entity.Children.Select(Pack).ToArray();
        pack.WeaponGroups = entity.WeaponGroups.Select(wg => wg.items.Select(item => entity.Equipment.IndexOf(item)).ToArray()).ToArray();
        return pack;
    }

    public static Entity Unpack(ItemManager itemManager, Zone zone, EntityPack pack, bool instantiate = false)
    {
        pack.Settings ??= MessagePackSerializer.Deserialize<EntitySettings>(
            MessagePackSerializer.Serialize(itemManager.GameplaySettings.DefaultEntitySettings));
        return pack switch
        {
            ShipPack shipPack => Unpack(itemManager, zone, shipPack, instantiate),
            OrbitalEntityPack orbitalEntityPack => Unpack(itemManager, zone, orbitalEntityPack, instantiate),
            _ => null
        };
    }


    private static Ship Unpack(ItemManager itemManager, Zone zone, ShipPack pack, bool instantiate = false)
    {
        
        var entity = new Ship(itemManager, zone, instantiate ? (EquippableItem) itemManager.Instantiate(pack.Hull) : pack.Hull, pack.Settings);
        Restore(itemManager, zone, pack, entity, instantiate);
        entity.Position = pack.Position;
        entity.Direction = pack.Direction;
        entity.IsPlayerShip = pack.IsPlayerShip;
        return entity;
    }

    private static OrbitalEntity Unpack(ItemManager itemManager, Zone zone, OrbitalEntityPack pack, bool instantiate = false)
    {
        var entity = new OrbitalEntity(itemManager, zone, instantiate ? (EquippableItem) itemManager.Instantiate(pack.Hull) : pack.Hull, pack.Orbit, pack.Settings);
        Restore(itemManager, zone, pack, entity, instantiate);
        entity.SecurityLevel = pack.SecurityLevel;
        entity.SecurityRadius = pack.SecurityRadius;
        if (pack.Story >= 0) entity.Story = zone.GalaxyZone.Locations[pack.Story];
        return entity;
    }

    private static void Restore(ItemManager itemManager, Zone zone, EntityPack pack, Entity entity, bool instantiate = false)
    {
        entity.Name = pack.Name;
        entity.Faction = itemManager.ItemData.Get<Faction>(pack.Faction);
        entity.Children = pack.Children.Select(c =>
        {
            var child = Unpack(itemManager, zone, c, instantiate);
            child.Parent = entity;
            return child;
        }).ToList();
        foreach (var (position, item) in pack.Equipment) entity.TryEquip(instantiate ? (EquippableItem) itemManager.Instantiate(item) : item, position);
        foreach (var (position, item) in pack.CargoBays) entity.TryEquip(instantiate ? (EquippableItem) itemManager.Instantiate(item) : item, position);
        foreach (var (position, item) in pack.DockingBays) entity.TryEquip(instantiate ? (EquippableItem) itemManager.Instantiate(item) : item, position);

        for (var i = 0; i < pack.DockingBayAssignments.Length; i++)
        {
            if (pack.DockingBayAssignments[i] != -1)
                entity.DockingBays[i].DockedShip = entity.Children[pack.DockingBayAssignments[i]] as Ship;
        }

        for (var bayIndex = 0; bayIndex < pack.CargoContents.Length; bayIndex++)
            if(entity.CargoBays.Count >= bayIndex + 1)
                foreach (var (position, item) in pack.CargoContents[bayIndex])
                    entity.CargoBays[bayIndex].TryStore(instantiate ? itemManager.Instantiate(item) : item, position);

        for (var bayIndex = 0; bayIndex < pack.DockingBayContents.Length; bayIndex++)
            if(entity.DockingBays.Count >= bayIndex + 1)
                foreach (var (position, item) in pack.DockingBayContents[bayIndex])
                    entity.DockingBays[bayIndex].TryStore(instantiate ? itemManager.Instantiate(item) : item, position);

        // Iterate only over the behaviors of items which contain persistent data
        // Filter the behaviors for each item to get the persistent ones, then cast them and combine with the persisted data array for that item
        foreach (var persistentBehaviorData in entity.Equipment
            .Where(item => pack.PersistedBehaviors.ContainsKey(item.Position))
            .SelectMany(item => item.Behaviors
                .Where(b=> b is IPersistentBehavior)
                .Cast<IPersistentBehavior>()
                .Zip(pack.PersistedBehaviors[item.Position], (behavior, data) => new{behavior, data})))
            persistentBehaviorData.behavior.Restore(persistentBehaviorData.data);

        if(!instantiate)
            entity.Temperature = pack.Temperature;
        
        if(!instantiate)
            entity.Armor = pack.Armor;
        
        var hullData = itemManager.GetData(entity.Hull);
        
        foreach(var v in hullData.Shape.Coordinates)
            entity.HullConductivity[v.x,v.y] = pack.Conductivity[v.x,v.y];

        entity.WeaponGroups = pack.WeaponGroups.Select(itemIndices =>
        {
            var items = itemIndices.Select(i => entity.Equipment[i]);
            return (items.Select(i => i.GetBehavior<Weapon>()).ToList(), items.ToList());
        }).ToArray();
    }
}

[MessagePackObject]
public class SavedStory
{
    [Key(0)] public string StoryJson;
    [Key(1)] public string StateJson;
}

[MessagePackObject]
public class ShipPack : EntityPack
{
    [Key(17)] public float3 Position;
    [Key(18)] public float2 Direction;
    [Key(19)] public bool IsPlayerShip;
}

[MessagePackObject]
public class OrbitalEntityPack : EntityPack
{
    [Key(17)] public Guid Orbit;
    [Key(18)] public int Story = -1;
    [Key(19)] public SecurityLevel SecurityLevel;
    [Key(20)] public float SecurityRadius;
}

[MessagePackObject, 
 Union(0, typeof(OrbitalEntityPack)),
 Union(1, typeof(ShipPack))]
public abstract class EntityPack
{
    [Key(0)] public string Name;
    [Key(1)] public EquippableItem Hull;
    [Key(2)] public (int2 position, EquippableItem item)[] Equipment;
    [Key(3)] public (int2 position, EquippableItem item)[] CargoBays;
    [Key(4)] public (int2 position, EquippableItem item)[] DockingBays;
    [Key(5)] public Dictionary<int2, PersistentBehaviorData[]> PersistedBehaviors;
    [Key(6)] public float[,] Temperature;
    [Key(7)] public float[,] Armor;
    [Key(8)] public bool2[,] Conductivity;
    [Key(10)] public int[] DockingBayAssignments;
    [Key(11)] public (int2 position, ItemInstance item)[][] CargoContents;
    [Key(12)] public (int2 position, ItemInstance item)[][] DockingBayContents;
    [Key(13)] public EntityPack[] Children;
    [Key(14)] public EntitySettings Settings;
    [Key(15)] public Guid Faction;
    [Key(16)] public int[][] WeaponGroups;

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
                var itemData = item.Data.Value;
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
                var itemData = item.Data.Value;
                if (item is SimpleCommodity s)
                    _price += itemData.Price * s.Quantity;
                else
                    _price += itemData.Price;
            }
        }

        return _price;
    }
}
