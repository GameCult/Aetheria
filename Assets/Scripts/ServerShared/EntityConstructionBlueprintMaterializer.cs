using System;
using System.Linq;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
using int2 = Unity.Mathematics.int2;

public static class EntityConstructionBlueprintCapture
{
    public static EntityConstructionBlueprint Capture(Entity entity)
    {
        EntityConstructionBlueprint blueprint;
        if (entity is OrbitalEntity orbital)
            blueprint = new OrbitalEntityConstructionBlueprint
            {
                OrbitKey = orbital.OrbitKey,
                SecurityLevel = orbital.SecurityLevel,
                SecurityRadius = orbital.SecurityRadius
            };
        else if (entity is Ship ship)
            blueprint = new ShipConstructionBlueprint
            {
                Position = AetheriaMath.ToUnity(ship.CultPosition),
                Direction = AetheriaMath.ToUnity(ship.CultDirection),
                IsPlayerShip = ship.IsPlayerShip
            };
        else throw new ArgumentException("Attempted to capture an instance of abstract class Entity!");

        blueprint.Settings = entity.Settings;
        
        blueprint.Hull = entity.Hull;
        blueprint.Name = entity.Name;
        blueprint.FactionKey = entity.Faction?.FactionKey ?? "";
        blueprint.Equipment = entity.Equipment.Select(e => (e.Position, e.EquippableItem)).ToArray();
        blueprint.CargoBays = entity.CargoBays.Select(e => (e.Position, e.EquippableItem)).ToArray();
        blueprint.DockingBays = entity.DockingBays.Select(e => (e.Position, e.EquippableItem)).ToArray();
        blueprint.DockingBayAssignments = entity.DockingBays.Select(x => entity.Children.IndexOf(x.DockedShip)).ToArray();
        blueprint.CargoContents = entity.CargoBays.Select(b => b.Cargo.Select(i => (i.Value, i.Key)).ToArray()).ToArray();
        blueprint.DockingBayContents = entity.DockingBays.Select(b => b.Cargo.Select(i => (i.Value, i.Key)).ToArray()).ToArray();
        blueprint.Children = entity.Children.Select(Capture).ToArray();
        blueprint.WeaponGroups = entity.WeaponGroups.Select(wg => wg.items.Select(item => entity.Equipment.IndexOf(item)).ToArray()).ToArray();
        return blueprint;
    }
}

public static class EntityConstructionBlueprintMaterializer
{
    public static Entity InstantiateAuthoritativeFromBlueprint(ItemManager itemManager, Zone zone, EntityConstructionBlueprint blueprint)
    {
        return BuildFromBlueprint(itemManager, zone, blueprint, false);
    }

    public static Entity MaterializeObservedFromBlueprint(ItemManager itemManager, Zone zone, EntityConstructionBlueprint blueprint)
    {
        return BuildFromBlueprint(itemManager, zone, blueprint, true);
    }

    private static Entity BuildFromBlueprint(ItemManager itemManager, Zone zone, EntityConstructionBlueprint blueprint, bool instantiate)
    {
        blueprint.Settings ??= itemManager.GameplaySettings.DefaultEntitySettings.Copy();
        return blueprint switch
        {
            ShipConstructionBlueprint shipBlueprint => BuildFromBlueprint(itemManager, zone, shipBlueprint, instantiate),
            OrbitalEntityConstructionBlueprint orbitalBlueprint => BuildFromBlueprint(itemManager, zone, orbitalBlueprint, instantiate),
            _ => null
        };
    }


    private static Ship BuildFromBlueprint(ItemManager itemManager, Zone zone, ShipConstructionBlueprint blueprint, bool instantiate)
    {
        
        var entity = new Ship(itemManager, zone, instantiate ? (EquippableItem) itemManager.Instantiate(blueprint.Hull) : blueprint.Hull, blueprint.Settings);
        Restore(itemManager, zone, blueprint, entity, instantiate);
        entity.CultPosition = AetheriaMath.ToCult(blueprint.Position);
        entity.CultDirection = AetheriaMath.ToCult(blueprint.Direction);
        entity.IsPlayerShip = blueprint.IsPlayerShip;
        return entity;
    }

    private static OrbitalEntity BuildFromBlueprint(ItemManager itemManager, Zone zone, OrbitalEntityConstructionBlueprint blueprint, bool instantiate)
    {
        var entity = new OrbitalEntity(itemManager, zone, instantiate ? (EquippableItem) itemManager.Instantiate(blueprint.Hull) : blueprint.Hull, blueprint.OrbitKey, blueprint.Settings);
        Restore(itemManager, zone, blueprint, entity, instantiate);
        entity.SecurityLevel = blueprint.SecurityLevel;
        entity.SecurityRadius = blueprint.SecurityRadius;
        if (blueprint.Story >= 0) entity.Story = zone.GalaxyZone.Locations[blueprint.Story];
        return entity;
    }

    private static void Restore(ItemManager itemManager, Zone zone, EntityConstructionBlueprint blueprint, Entity entity, bool instantiate = false)
    {
        entity.Name = blueprint.Name;
        entity.Faction = ResolveFaction(zone?.Galaxy, blueprint);
        entity.Children = blueprint.Children.Select(c =>
        {
            var child = BuildFromBlueprint(itemManager, zone, c, instantiate);
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

        entity.WeaponGroups = blueprint.WeaponGroups.Select(itemIndices =>
        {
            var items = itemIndices.Select(i => entity.Equipment[i]);
            return (items.Select(i => i.GetBehavior<Weapon>()).ToList(), items.ToList());
        }).ToArray();
    }

    private static Faction ResolveFaction(Galaxy galaxy, EntityConstructionBlueprint blueprint)
    {
        return galaxy == null
            ? null
            : galaxy.ResolveFactionByKey(blueprint.FactionKey);
    }
}

public class ShipConstructionBlueprint : EntityConstructionBlueprint
{
    public float3 Position;
    public float2 Direction;
    public bool IsPlayerShip;
}

public class OrbitalEntityConstructionBlueprint : EntityConstructionBlueprint
{
    public string OrbitKey = "";
    public int Story = -1;
    public SecurityLevel SecurityLevel;
    public float SecurityRadius;
}

public abstract class EntityConstructionBlueprint
{
    public string Name;
    public EquippableItem Hull;
    public (int2 position, EquippableItem item)[] Equipment;
    public (int2 position, EquippableItem item)[] CargoBays;
    public (int2 position, EquippableItem item)[] DockingBays;
    public int[] DockingBayAssignments;
    public (int2 position, ItemInstance item)[][] CargoContents;
    public (int2 position, ItemInstance item)[][] DockingBayContents;
    public EntityConstructionBlueprint[] Children;
    public EntitySettings Settings;
    public string FactionKey;
    public int[][] WeaponGroups;

    private int _price;

    public int Price(ItemManager itemManager)
    {
        if (_price != 0) return _price;
        
        _price = itemManager.GetPrice(Hull);

        foreach (var (_, item) in Equipment)
        {
            _price += itemManager.GetPrice(item);
        }
        foreach (var (_, item) in CargoBays)
        {
            _price += itemManager.GetPrice(item);
        }
        foreach (var (_, item) in DockingBays)
        {
            _price += itemManager.GetPrice(item);
        }

        foreach (var t in CargoContents)
        {
            foreach (var (_, item) in t)
            {
                _price += itemManager.GetPrice(item);
            }
        }

        foreach (var t in DockingBayContents)
        {
            foreach (var (_, item) in t)
            {
                _price += itemManager.GetPrice(item);
            }
        }

        return _price;
    }
}
