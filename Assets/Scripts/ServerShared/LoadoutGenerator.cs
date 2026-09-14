using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CultMath;
using static CultMath.math;
using Random = CultMath.Random;

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

    public EntityPack GenerateShipLoadout(Predicate<HullData> hullFilter = null)
    {
        var (hullProduct, hullData) = RandomHull(HullType.Ship, hullFilter);
        if(hullData==null)
        {
            ItemManager.Log("Unable to generate ship loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateInstance(hullProduct) as EquippableItem;
        if(hull==null)
            ItemManager.Log("WHAT???");
        var entity = new Ship(ItemManager, null, hull, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        OutfitEntity(entity);
        return EntitySerializer.Pack(entity);
    }

    public OrbitalEntityPack GenerateTurretLoadout()
    {
        var (hullProduct, hullData) = RandomHull(HullType.Turret);
        if(hullData==null)
        {
            ItemManager.Log("Unable to generate turret loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateInstance(hullProduct) as EquippableItem;
        var entity = new OrbitalEntity(ItemManager, null, hull, Guid.Empty, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;
        OutfitEntity(entity);
        return EntitySerializer.Pack(entity) as OrbitalEntityPack;
    }

    public OrbitalEntityPack GenerateStationLoadout()
    {
        var (hullProduct, hullData) = RandomHull(HullType.Station);
        if(hullData==null)
        {
            ItemManager.Log("Unable to generate station loadout: no compatible hull found!");
            return null;
        }
        var hull = ItemManager.CreateInstance(hullProduct) as EquippableItem;
        var entity = new OrbitalEntity(ItemManager, null, hull, Guid.Empty, ItemManager.GameplaySettings.DefaultEntitySettings);
        entity.Faction = Faction;

        // Hardpoint gear goes in first so the docking bay can only take space that gear left free
        EquipHardpoints(entity);

        var emptyShape = entity.UnoccupiedSpace;

        var (dockingBayProduct, dockingBayData) = RandomProduct<DockingBayData>(2, item => item.Shape.FitsWithin(emptyShape, out _, out _), required: true);
        if (dockingBayData == null) throw new InvalidLoadoutException("No compatible docking bay found for station!");

        dockingBayData.Shape.FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var dockingBay = ItemManager.CreateInstance(dockingBayProduct) as EquippableItem;
        dockingBay.Rotation = cargoRotation;
        if (!entity.TryEquip(dockingBay, cargoPosition))
        {
            throw new InvalidLoadoutException("Failed to equip selected docking bay!");
        }

        FillInterior(entity);

        var cargo = entity.CargoBays.First();
        var inventory = RandomProducts<EquippableItemData>(16, 1,
            data => !(data is HullData hull && hull.HullType != HullType.Ship) && !(data is CargoBayData))
            .OrderByDescending(entry => entry.design.Shape.Coordinates.Length);
        foreach (var entry in inventory)
        {
            var instance = ItemManager.CreateInstance(entry.product);
            cargo.TryStore(instance);
        }

        entity.CanTow = hullData.CanTow;

        return EntitySerializer.Pack(entity) as OrbitalEntityPack;
    }

    // Every entity needs a hull, so hulls are always required
    public (FactionProductData product, HullData design) RandomHull(HullType type, Predicate<HullData> hullFilter = null)
    {
        return RandomProduct<HullData>(0, item =>
                (hullFilter?.Invoke(item) ?? true) &&
                item.HullType == type, required: true);
    }

    // What a faction makes is which products it has, so generation selects branded products rather than designs.
    // Products normally come only from manufacturers present in the galaxy and known to the zone's faction; a
    // required item falls back to any manufacturer when none of those make one, which means the product table
    // lacks variety, so it is logged rather than hidden.
    public (FactionProductData product, T design)[] RandomProducts<T>(int count, float sizeExponent, Predicate<T> filter = null, bool required = false) where T : EquippableItemData
    {
        var candidates = ItemManager.ItemData.GetAll<FactionProductData>()
            .Select(product => (product, design: ItemManager.ItemData.Get<CraftedItemData>(product.Design) as T))
            .Where(entry =>
                entry.design != null &&
                entry.design.Price > 0 &&
                entry.product.Manufacturer != Guid.Empty &&
                (filter?.Invoke(entry.design) ?? true))
            .ToArray();
        var available = candidates.Where(entry => IsAvailable(entry.product)).ToArray();
        var preferManufacturers = true;
        if (available.Length == 0 && required && candidates.Length > 0)
        {
            ItemManager.Log($"No {typeof(T).Name} product available from manufacturers known to {Faction?.Name ?? "this galaxy"}; using any manufacturer. The product table needs more variety.");
            available = candidates;
            preferManufacturers = false;
        }

        return available.WeightedRandomElements(ref Random, entry =>
                (preferManufacturers ? ManufacturerPreference(entry.product.Manufacturer) : 1) *
                pow(entry.design.Shape.Coordinates.Length, sizeExponent) / // Prioritize larger items
                pow(entry.design.Price, PriceExponent), // Penalize item price to a controllable degree
            count);
    }

    // No galaxy means no availability to filter by: every product is on offer. A fixture generates loadouts that
    // way, so a test can exercise placement and products without standing up a whole galaxy; no game path does.
    private bool IsAvailable(FactionProductData product) =>
        Galaxy == null || Galaxy.IsPrelude ||
        Galaxy.ContainsFaction(product.Manufacturer) && (Faction == null || Faction.Allegiance.ContainsKey(product.Manufacturer));

    // Prioritize products from the zone faction and its allies, penalizing distance to the manufacturer's headquarters
    private float ManufacturerPreference(Guid manufacturer)
    {
        if (Faction == null || Galaxy == null) return 1;
        var allegiance = manufacturer == Faction.ID ? 1 :
            Faction.Allegiance.TryGetValue(manufacturer, out var a) ? a : 0;
        var home = Galaxy.HomeZones.FirstOrDefault(h => h.Key.ID == manufacturer).Value;
        var distance = home != null && Zone?.Distance != null && Zone.Distance.TryGetValue(home, out var d) ? d : 0;
        return allegiance / (1 + distance);
    }

    public (FactionProductData product, T design) RandomProduct<T>(float sizeExponent, Predicate<T> filter = null, bool required = false) where T : EquippableItemData
    {
        return RandomProducts(1, sizeExponent, filter, required).FirstOrDefault();
    }

    public (FactionProductData product, T design) RandomProduct<T>(HardpointData hardpoint, float sizeExponent, Predicate<T> filter = null, bool required = false) where T : EquippableItemData
    {
        return RandomProduct<T>(sizeExponent, item => item.HardpointType == hardpoint.Type &&
                                  (filter?.Invoke(item) ?? true) &&
                                  item.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _) &&
                                  item.Shape.Coordinates.Length==hardpoint.Shape.Coordinates.Length, required);
    }

    private void OutfitEntity(Entity entity)
    {
        EquipHardpoints(entity);
        FillInterior(entity);
    }

    private void EquipHardpoints(Entity entity)
    {
        var hullData = ItemManager.GetData(entity.Hull) as HullData;
        foreach (var v in hullData.Shape.Coordinates) entity.HullConductivity[v.x, v.y] = true;
        var previousProducts = new List<(FactionProductData product, GearData design)>();
        foreach (var hardpoint in hullData.Hardpoints.OrderByDescending(h=>h.Shape.Coordinates.Length))
        {
            if (hardpoint.Type == HardpointType.ControlModule)
            {
                var (controllerProduct, controllerData) = RandomProduct<GearData>(hardpoint, 2,
                    item => item.Behaviors.Any(b => entity is Ship && b is CockpitData || entity is OrbitalEntity && b is TurretControllerData), required: true);
                if (controllerData == null)
                    throw new InvalidLoadoutException("No compatible controller found for entity!");
                var controller = ItemManager.CreateInstance(controllerProduct) as EquippableItem;
                if (!entity.TryEquip(controller))
                {
                    throw new InvalidLoadoutException($"Failed to equip selected {Enum.GetName(typeof(HardpointType), hardpoint.Type)}!");
                }
            }
            else
            {
                // If a previously selected product fits, use that one (this is why we must process larger hardpoints first)
                var entry = previousProducts
                    .FirstOrDefault(e => e.design.HardpointType == hardpoint.Type && e.design.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _));
                var previousItem = entity.Equipment.FirstOrDefault(item => item.Data == entry.design);
                if (entry.design == null) entry = RandomProduct<GearData>(hardpoint, 2);
                if (entry.design == null) ItemManager.Log($"No compatible item found for entity {Enum.GetName(typeof(HardpointType), hardpoint.Type)} hardpoint!");
                else
                {
                    var item = ItemManager.CreateInstance(entry.product) as EquippableItem;
                    // Matching hardpoints carry matching units: the same brand, workmanship, and parts
                    if (previousItem != null) CopyBuild(previousItem.EquippableItem, item);
                    if (!entity.TryEquip(item))
                    {
                        throw new InvalidLoadoutException($"Failed to equip selected {Enum.GetName(typeof(HardpointType), hardpoint.Type)}!");
                    }
                    previousProducts.Add(entry);
                }
            }
        }

    }

    // Interior gear may use any free interior cell, including empty hardpoint cells
    private void FillInterior(Entity entity)
    {
        var emptyShape = entity.UnoccupiedSpace;

        var (cargoProduct, cargoData) = RandomProduct<CargoBayData>(3, item =>
            !(item is DockingBayData) &&
            item.Shape.FitsWithin(emptyShape, out _, out _), required: true);
        if (cargoData == null) throw new InvalidLoadoutException("No compatible cargo bay found for entity!");

        cargoData.Shape.FitsWithin(emptyShape, out var cargoRotation, out var cargoPosition);
        var cargo = ItemManager.CreateInstance(cargoProduct) as EquippableItem;
        cargo.Rotation = cargoRotation;
        if (!entity.TryEquip(cargo, cargoPosition))
            throw new InvalidLoadoutException("Failed to equip selected cargo bay!");

        emptyShape = entity.UnoccupiedSpace;

        var (capacitorProduct, capacitorData) = RandomProduct<GearData>(2, item =>
            item.Behaviors.Any(b => b is CapacitorData) &&
            item.Shape.FitsWithin(emptyShape, out _, out _), required: true);
        if (capacitorData == null) throw new InvalidLoadoutException("No compatible capacitor found for entity!");

        capacitorData.Shape.FitsWithin(emptyShape, out var capacitorRotation, out var capacitorPosition);
        var capacitor = ItemManager.CreateInstance(capacitorProduct) as EquippableItem;
        capacitor.Rotation = capacitorRotation;
        if (!entity.TryEquip(capacitor, capacitorPosition))
            throw new InvalidLoadoutException("Failed to equip selected capacitor!");
    }

    // A second unit off the same line: same workmanship and the same parts, not a fresh roll.
    private static void CopyBuild(CraftedItemInstance from, CraftedItemInstance to)
    {
        to.Quality = from.Quality;
        to.Ingredients = from.Ingredients?
            .Select(fill => new RoleFill { Role = fill.Role, Quality = fill.Quality })
            .ToList() ?? new List<RoleFill>();
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
