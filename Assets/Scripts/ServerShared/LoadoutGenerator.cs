using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameCult.Caching;
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
        var entity = new OrbitalEntity(ItemManager, null, hull, default, ItemManager.GameplaySettings.DefaultEntitySettings);
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
        var entity = new OrbitalEntity(ItemManager, null, hull, default, ItemManager.GameplaySettings.DefaultEntitySettings);
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

        // Every station carries a heater (operator, 2026-09-30): an idle station has no other heat source and cools
        // toward freezing and invisibility. A heater is general gear whose thermostat runs its heat only below a
        // target, the composition a cockpit uses: a low-pass Thermotoggle, then Heat, in its behaviours.
        emptyShape = entity.UnoccupiedSpace;
        var (heaterProduct, heaterData) = RandomProduct<GearData>(2, item => IsHeater(item) && item.Shape.FitsWithin(emptyShape, out _, out _), required: true);
        if (heaterData == null) throw new InvalidLoadoutException("No compatible heater found for station!");

        heaterData.Shape.FitsWithin(emptyShape, out var heaterRotation, out var heaterPosition);
        var heater = ItemManager.CreateInstance(heaterProduct) as EquippableItem;
        heater.Rotation = heaterRotation;
        if (!entity.TryEquip(heater, heaterPosition))
            throw new InvalidLoadoutException("Failed to equip selected heater!");

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

    private static bool IsHeater(GearData item) =>
        item.HardpointType == HardpointType.Tool &&
        item.Behaviors.Any(b => b is ThermotoggleData { HighPass: false }) &&
        item.Behaviors.Any(b => b is HeatData);

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
        var (available, preferManufacturers) = AvailableProducts(filter, required);
        return available.WeightedRandomElements(ref Random, entry =>
                (preferManufacturers ? ManufacturerPreference(entry.product.Manufacturer) : 1) *
                pow(entry.design.Shape.Coordinates.Length, sizeExponent) / // Prioritize larger items
                pow(entry.design.Price, PriceExponent), // Penalize item price to a controllable degree
            count);
    }

    // The products this faction can be offered for a design kind, and whether manufacturer preference applies (it does
    // not once a required item has fallen back to any manufacturer). The one availability rule for every selection.
    private ((FactionProductData product, T design)[] available, bool preferManufacturers) AvailableProducts<T>(Predicate<T> filter, bool required) where T : EquippableItemData
    {
        var hulls = ItemManager.ItemData.GetAll<HullData>().ToArray();
        var candidates = ItemManager.ItemData.GetAll<FactionProductData>()
            .Select(product => (product, design: ItemManager.ItemData.Get(product.Design) as T))
            .Where(entry =>
                entry.design != null &&
                entry.design.Price > 0 &&
                entry.product.Manufacturer.IsSet() &&
                HasHome(entry.design, hulls) &&
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

        return (available, preferManufacturers);
    }

    // A faction's boss: its BossHull when the faction sets one and a product sells it, else the most expensive ship hull
    // the faction can be offered, with the gap logged. Null only when no ship hull is on offer at all.
    public EntityPack GenerateBossLoadout()
    {
        var bossHull = Faction != null && Faction.BossHull.IsSet() ? ItemManager.ItemData.Get(Faction.BossHull) : null;
        if (bossHull != null)
        {
            var boss = GenerateShipLoadout(hull => hull == bossHull);
            if (boss != null) return boss;
            ItemManager.Log($"{Faction.Name}'s BossHull {bossHull.Name} is not sold by any product; its boss flies the most expensive ship hull instead.");
        }
        else
            ItemManager.Log($"{Faction?.Name ?? "This faction"} has no BossHull; its boss flies the most expensive ship hull instead.");

        var (available, _) = AvailableProducts<HullData>(hull => hull.HullType == HullType.Ship, required: true);
        if (available.Length == 0) return null;
        var priciest = available.OrderByDescending(entry => entry.design.Price).ThenBy(entry => entry.design.Name, StringComparer.Ordinal).First().design;
        return GenerateShipLoadout(hull => hull == priciest);
    }

    // Hardpoint gear that no hull in the catalog can mount has no home (operator, 2026-09-30: "we'll need to author a
    // bunch more hulls before all the gear variety in the game has a home"): generation never offers it, so no station
    // stocks it. It gains one the moment a hull with a hardpoint that takes it exists. Tool gear goes in any interior.
    public static bool HasHome(EquippableItemData design, HullData[] hulls) =>
        design.HardpointType == HardpointType.Tool || design.HardpointType == HardpointType.Hull ||
        hulls.Any(hull => hull.Hardpoints.Any(hardpoint => hardpoint.Takes(design)));

    // No galaxy, or no faction, means no allegiance to filter by: every product is on offer. A fixture generates
    // loadouts that way, so a test can exercise placement and products without standing up a whole galaxy; a scenario
    // stage has no faction and reaches every maker. Loadouts.Materialize takes availability as a predicate; RunStart,
    // the game's one preset spawner, passes this, so presets and generation share one availability rule. A faction
    // always reaches its own manufacturer's gear, and otherwise gear made by a manufacturer its allegiance names
    // (operator, 2026-09-30: allegiance lists only other factions). Whether that manufacturer is present in the galaxy
    // does not matter (operator, 2026-10-06: a faction need not be present for its gear to be available).
    public bool IsAvailable(FactionProductData product) =>
        Galaxy == null || Faction == null ||
        ItemManager.ItemData.Get(product.Manufacturer) == Faction || Faction.Allegiance.ContainsKey(product.Manufacturer);

    // Prioritize products from the zone faction and its allies, penalizing distance to the manufacturer's headquarters.
    // A manufacturer with no home zone in this galaxy weighs as if its headquarters lay one jump beyond the farthest
    // zone this zone reaches: reachable through allegiance, least preferred.
    public float ManufacturerPreference(CultRecordRef<Faction> manufacturer)
    {
        if (Faction == null || Galaxy == null) return 1;
        var maker = ItemManager.ItemData.Get(manufacturer);
        var allegiance = maker == Faction ? 1 :
            Faction.Allegiance.TryGetValue(manufacturer, out var a) ? a : 0;
        var home = maker != null && Galaxy.HomeZones.TryGetValue(maker, out var h) ? h : null;
        var distance = Zone?.Distance == null ? 0 :
            home != null && Zone.Distance.TryGetValue(home, out var d) ? d :
            home == null && Zone.Distance.Count > 0 ? Zone.Distance.Values.Max() + 1 : 0;
        return allegiance / (1 + distance);
    }

    public (FactionProductData product, T design) RandomProduct<T>(float sizeExponent, Predicate<T> filter = null, bool required = false) where T : EquippableItemData
    {
        return RandomProducts(1, sizeExponent, filter, required).FirstOrDefault();
    }

    public (FactionProductData product, T design) RandomProduct<T>(HardpointData hardpoint, float sizeExponent, Predicate<T> filter = null, bool required = false) where T : EquippableItemData
    {
        // Every candidate passes the one fit rule, HardpointData.Takes. Generation prefers gear that fills the hardpoint
        // and falls back to anything that fits only when nothing that fills it is on offer.
        var filling = RandomProduct<T>(sizeExponent, item => hardpoint.IsFilledBy(item) && (filter?.Invoke(item) ?? true));
        if (filling.design != null) return filling;
        return RandomProduct<T>(sizeExponent, item => hardpoint.Takes(item) && (filter?.Invoke(item) ?? true), required);
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
                    .FirstOrDefault(e => hardpoint.Takes(e.design));
                var previousItem = entity.Equipment.FirstOrDefault(item => item.Data == entry.design);
                if (entry.design == null) entry = RandomProduct<GearData>(hardpoint, 2);
                if (entry.design == null) ItemManager.Log($"No compatible item found for entity {Enum.GetName(typeof(HardpointType), hardpoint.Type)} hardpoint!");
                else
                {
                    // Matching hardpoints carry matching units: same lot
                    var item = (previousItem != null
                        ? ItemManager.CreateInstance(previousItem.EquippableItem.Lot)
                        : ItemManager.CreateInstance(entry.product)) as EquippableItem;
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

        // Cut 2 (docs/fire-control-cut.md, Q4): required equipment for anything that can fire -- unaided
        // fire is a last resort, not an alternative loadout choice. EquipHardpoints runs before FillInterior
        // (OutfitEntity), so entity.Weapons is already populated here. Zenith and anything else with no
        // weapon hardpoints gets none.
        //
        // Cut 6c, 6c.2: this used to be `required: true` and throw InvalidLoadoutException when no seller was
        // available -- a galaxy that happened not to draw a targeting design's manufacturer failed entity
        // generation outright, a single point of failure the ten-seed smoke never sampled. It degrades instead:
        // required: false, so RandomProducts' own manufacturer-widening fallback never fires and an unmet
        // requirement returns null quietly rather than searching outside the galaxy for one. An entity that
        // ends up with no targeting system fires through FireControl's unaided fallback
        // (GameplaySettings.UnaidedTracking, Cut 5.1) rather than not firing at all -- that floor is what makes
        // this degrade safe. The gap is logged so it surfaces as content debt (AetherDb loadout reports it per
        // seed) instead of vanishing silently.
        if (entity.Weapons.Any())
        {
            emptyShape = entity.UnoccupiedSpace;

            var (targetingProduct, targetingData) = RandomProduct<GearData>(2, item =>
                item.Behaviors.Any(b => b is TargetingSystemData) &&
                item.Shape.FitsWithin(emptyShape, out _, out _));
            if (targetingData == null)
            {
                ItemManager.Log("No targeting system available for armed entity; it will fire unaided.");
            }
            else
            {
                targetingData.Shape.FitsWithin(emptyShape, out var targetingRotation, out var targetingPosition);
                var targetingSystem = ItemManager.CreateInstance(targetingProduct) as EquippableItem;
                targetingSystem.Rotation = targetingRotation;
                if (!entity.TryEquip(targetingSystem, targetingPosition))
                    throw new InvalidLoadoutException("Failed to equip selected targeting system!");
            }
        }
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
