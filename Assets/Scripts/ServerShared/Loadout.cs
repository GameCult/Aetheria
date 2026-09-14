using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using CultMath;

// A ship preset (a variant): item designs only, the hull's design, and per hull cell a design and its rotation. A design
// names no manufacturer; which product builds a slot is a fact of the galaxy the preset is materialized in, which keeps
// a preset portable across galaxies. Authored catalog data: written in CultCache Studio or by the editor capture
// command, read at runtime.
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

    // A preset's key derives from its name, so capturing the same name again addresses the same record.
    public static CultRecordKey KeyOf(string name) => new CultRecordKey("loadout:" + name);

    // Writes the preset to the catalog under KeyOf(Name). An existing preset of that name is replaced only when replace
    // is set; otherwise this throws and writes nothing. The commit is conditional on the record at that key being what
    // this cache holds, so it lands only this record onto the catalog file as it is on disk: no other in-memory catalog
    // state is written, and a preset changed on disk since load (Studio) is not clobbered. Returns false on that conflict.
    public static bool Commit(CultCache cache, Loadout loadout, bool replace)
    {
        var key = KeyOf(loadout.Name);
        var existing = cache.Get<Loadout>(key);
        if (existing != null && !replace)
            throw new InvalidOperationException($"Preset '{loadout.Name}' already exists; replace it explicitly.");
        return cache.Commit(batch =>
        {
            batch.Expect(key, existing);
            batch.Upsert(typeof(Loadout), loadout, key);
        });
    }

    // All-or-nothing: returns a ship only when every design resolved, had an available product and fitted, the loadout
    // has at most WeaponGroupCount weapon groups, and every group index names a weapon slot. Every failure is listed; on
    // any failure nothing is returned and the zone and cache are unchanged, but a failed fit has already drawn from
    // itemManager.Random. A design is built by its first available product in record-key order. The game passes
    // LoadoutGenerator.IsAvailable as isAvailable, with no fallback to any manufacturer. The ship always gets exactly
    // WeaponGroupCount groups; a loadout with fewer is padded with empty groups.
    public static Ship Materialize(ItemManager itemManager, Zone liveZone, Loadout loadout,
        Predicate<FactionProductData> isAvailable, List<string> failures)
    {
        var cache = itemManager.ItemData;
        var products = cache.GetAll<FactionProductData>().OrderBy(p => cache.RefOf(p).Key.Value, StringComparer.Ordinal).ToArray();
        var reported = failures.Count;

        FactionProductData Resolve<T>(CultRecordRef<T> design, string where) where T : EquippableItemData
        {
            var data = cache.Get(design);
            if (data == null)
            {
                failures.Add($"{where}: design {design.Key} is not in the catalog");
                return null;
            }

            var product = products.FirstOrDefault(p => p.Design.Key.Equals(design.Key) && isAvailable(p));
            if (product == null) failures.Add($"{where}: no available product of {data.Name}");
            return product;
        }

        var hullProduct = Resolve(loadout.Hull, "hull");
        var slotProducts = loadout.Slots.Select(slot => Resolve(slot.Design, Cell(slot))).ToArray();
        var groups = loadout.WeaponGroups ?? new int[0][];
        var groupCount = itemManager.GameplaySettings.WeaponGroupCount;
        if (groups.Length > groupCount) failures.Add($"weapon groups: {groups.Length} groups, at most {groupCount}");
        foreach (var index in groups.SelectMany(group => group))
        {
            if (index < 0 || index >= loadout.Slots.Count) failures.Add($"weapon group: no slot {index}");
            else if (cache.Get(loadout.Slots[index].Design) is { } design && !design.Behaviors.Any(behavior => behavior is WeaponData))
                failures.Add($"weapon group: {Cell(loadout.Slots[index])}: {design.Name} is not a weapon");
        }
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

        var built = new (List<Weapon> weapons, List<EquippedItem> items)[groupCount];
        for (var g = 0; g < groupCount; g++)
        {
            var items = g < groups.Length ? groups[g].Select(i => equipped[i]).ToList() : new List<EquippedItem>();
            built[g] = (items.Select(item => item.GetBehavior<Weapon>()).ToList(), items);
        }
        ship.WeaponGroups = built;
        return ship;
    }

    private static string Cell(LoadoutSlot slot) => $"slot {slot.Position.x},{slot.Position.y}";
}
