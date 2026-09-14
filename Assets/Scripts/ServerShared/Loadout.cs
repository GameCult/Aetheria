using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using CultMath;

// A ship build as item designs only: the hull's design, and per hull cell a design and its rotation. A design names no
// manufacturer; which product builds a slot is a fact of the galaxy the loadout is materialized in, which keeps a
// loadout portable across galaxies. Player-store state: a loadout outlives runs.
[CultDocument("aetheria.loadout", "1"), MessagePackObject]
public class Loadout
{
    [CultName, Key(0)] public string Name;
    [Key(1)] public CultRecordRef<HullData> Hull;
    [Key(2)] public List<LoadoutSlot> Slots = new List<LoadoutSlot>();
    [Key(3)] public int[][] WeaponGroups;          // indices into Slots
}

[MessagePackObject]
public class LoadoutSlot
{
    [Key(0)] public int2 Position;                 // hull cell passed to Entity.TryEquip(item, int2)
    [Key(1)] public ItemRotation Rotation;
    [Key(2)] public CultRecordRef<EquippableItemData> Design;
}

// The owner of loadouts: capture from a live ship, the only writer, and the only builder of a ship from a loadout.
public static class Loadouts
{
    // Records the hull design and one slot per equipped item (gear, then cargo bays, then docking bays) with its cell,
    // rotation and design. Entity.Equipment also holds the hull's own unit; that is Hull, not a slot. Nothing about the
    // units themselves: no product, quality, ingredients, cargo or state.
    public static Loadout Capture(ItemManager itemManager, Entity entity, string name)
    {
        var cache = itemManager.ItemData;
        var items = entity.Equipment
            .Concat<EquippedItem>(entity.CargoBays)
            .Concat(entity.DockingBays)
            .Where(item => item.EquippableItem != entity.Hull)
            .Distinct()
            .ToList();
        return new Loadout
        {
            Name = name,
            Hull = cache.RefOf((HullData) itemManager.GetData(entity.Hull)),
            Slots = items.Select(item => new LoadoutSlot
            {
                Position = item.Position,
                Rotation = item.EquippableItem.Rotation,
                Design = cache.RefOf(itemManager.GetData(item.EquippableItem))
            }).ToList(),
            WeaponGroups = entity.WeaponGroups
                .Select(group => group.items.Select(item => items.IndexOf(item)).ToArray())
                .ToArray()
        };
    }

    // One Commit to the player store. A loadout with the same name is replaced under its existing key; names are
    // unique by construction because GetByName throws on duplicates.
    public static void Save(CultCache cache, Loadout loadout)
    {
        var existing = cache.GetByName<Loadout>(loadout.Name);
        var key = existing == null ? (CultRecordKey?) null : cache.TryGetHandle(existing)?.Key;
        cache.Commit(batch => batch.Upsert(typeof(Loadout), loadout, key));
    }

    // All-or-nothing: returns a ship only when every design resolved, had an available product and fitted. Every
    // failing slot is listed; on any failure nothing is returned and nothing outside this call has changed.
    // Availability is LoadoutGenerator.IsAvailable, with no fallback to any manufacturer.
    public static Ship Materialize(ItemManager itemManager, Galaxy galaxy, GalaxyZone zone, Faction stationFaction,
        Zone liveZone, Loadout loadout, List<string> failures)
    {
        var cache = itemManager.ItemData;
        var availability = new LoadoutGenerator(ref itemManager.Random, itemManager, galaxy, zone, stationFaction, 0);
        var products = cache.GetAll<FactionProductData>().ToArray();
        var reported = failures.Count;

        FactionProductData Resolve<T>(CultRecordRef<T> design, string where) where T : EquippableItemData
        {
            var data = cache.Get(design);
            if (data == null)
            {
                failures.Add($"{where}: design {design.Key} is not in the catalog");
                return null;
            }

            var product = products.FirstOrDefault(p => p.Design.Key.Equals(design.Key) && availability.IsAvailable(p));
            if (product == null) failures.Add($"{where}: no available product of {data.Name}");
            return product;
        }

        var hullProduct = Resolve(loadout.Hull, "hull");
        var slotProducts = loadout.Slots.Select(slot => Resolve(slot.Design, Cell(slot))).ToArray();
        if (failures.Count > reported) return null;

        var hull = (EquippableItem) itemManager.CreateInstance(hullProduct);
        var ship = new Ship(itemManager, liveZone, hull, itemManager.GameplaySettings.DefaultEntitySettings);
        var equipped = new EquippedItem[loadout.Slots.Count];
        for (var i = 0; i < loadout.Slots.Count; i++)
        {
            var slot = loadout.Slots[i];
            var unit = (EquippableItem) itemManager.CreateInstance(slotProducts[i]);
            unit.Rotation = slot.Rotation;
            if (!ship.TryEquip(unit, slot.Position))
            {
                failures.Add($"{Cell(slot)}: {cache.Get(slot.Design).Name} does not fit");
                continue;
            }

            equipped[i] = ship.Equipment
                .Concat<EquippedItem>(ship.CargoBays)
                .Concat(ship.DockingBays)
                .First(item => item.EquippableItem == unit);
        }
        if (failures.Count > reported) return null;

        ship.WeaponGroups = (loadout.WeaponGroups ?? new int[0][])
            .Select(indices =>
            {
                var items = indices.Select(i => equipped[i]).ToList();
                return (items.Select(item => item.GetBehavior<Weapon>()).ToList(), items);
            })
            .ToArray();
        return ship;
    }

    // The hull design's price plus each slot design's price.
    public static int Price(ItemManager itemManager, Loadout loadout)
    {
        var cache = itemManager.ItemData;
        return (cache.Get(loadout.Hull)?.Price ?? 0) + loadout.Slots.Sum(slot => cache.Get(slot.Design)?.Price ?? 0);
    }

    private static string Cell(LoadoutSlot slot) => $"slot {slot.Position.x},{slot.Position.y}";
}
