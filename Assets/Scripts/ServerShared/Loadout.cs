using System;
using System.Collections.Generic;
using System.IO;
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

// The owner of loadouts: capture from a live ship, the only writer, and the only builder of an entity from a loadout.
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

    // The only writer of presets. It opens its own short-lived cache over the catalog file, writable, and disposes it
    // after the one commit. No other cache is touched, so no catalog instance play holds (or has mutated) can reach the
    // file; a cache that already has the catalog open sees the preset only once it reloads.
    // Throws and writes nothing when the loadout names a mod design (hull or slot), when the file is missing (capture never creates a catalog), when a preset of this name
    // exists under another key, or when one exists under KeyOf(Name) and replace is not set. The commit is conditional
    // on the record at KeyOf(Name), so a preset changed on disk since this open is not clobbered: returns false then.
    public static bool Commit(string catalogPath, Loadout loadout, bool replace)
    {
        var modDesigns = new[] { loadout.Hull.Key }.Concat(loadout.Slots.Select(slot => slot.Design.Key)).Where(ShipModCatalog.IsModKey).Select(key => key.Value).Distinct().ToArray();
        if (modDesigns.Length > 0)
            throw new InvalidOperationException($"Preset '{loadout.Name}' names mod designs ({string.Join(", ", modDesigns)}); the shipped catalog never references a mod, so a mod ship cannot be captured as a preset.");
        if (!File.Exists(catalogPath))
            throw new InvalidOperationException($"Catalog {catalogPath} does not exist; a preset capture never creates one.");
        using var cache = AetheriaStores.Open(catalogPath, catalogWritable: true);
        var key = KeyOf(loadout.Name);
        var namesake = cache.GetAll<Loadout>().FirstOrDefault(other => other.Name == loadout.Name && !cache.RefOf(other).Key.Equals(key));
        if (namesake != null)
            throw new InvalidOperationException($"Preset '{loadout.Name}' already exists under key {cache.RefOf(namesake).Key}; rename or remove it first.");
        var existing = cache.Get<Loadout>(key);
        if (existing != null && !replace)
            throw new InvalidOperationException($"Preset '{loadout.Name}' already exists; replace it explicitly.");
        return cache.Commit(batch =>
        {
            batch.Expect(key, existing);
            batch.Upsert(typeof(Loadout), loadout, key);
        });
    }

    // The quality a design no manufacturer makes is built at: the console give's.
    public const float UnbrandedQuality = .95f;

    // All-or-nothing: returns an entity only when every design resolved and fitted, the loadout has at most
    // WeaponGroupCount weapon groups, and every group index names a weapon slot. Every failure is listed; on any failure
    // nothing is returned and the zone and cache are unchanged, but a failed fit has already drawn from
    // itemManager.Random. How each design is built is Resolve's rule. A game spawner is to pass
    // LoadoutGenerator.IsAvailable as isAvailable, with no fallback to any manufacturer; RunStart does. The hull's type
    // picks the entity: a ship hull builds a Ship, a turret hull an OrbitalEntity with no orbit, which stays where it is
    // put; a station hull is refused. The entity always gets exactly WeaponGroupCount groups; a loadout with fewer is
    // padded with empty groups. A failed materialization leaves any minted lots in the live ledger; they are not roots,
    // so the next commit drops them.
    public static Entity Materialize(ItemManager itemManager, Zone liveZone, Loadout loadout,
        Predicate<FactionProductData> isAvailable, List<string> failures)
    {
        var cache = itemManager.ItemData;
        var products = ProductsInKeyOrder(cache);
        var reported = failures.Count;

        var hullBuild = Resolve(itemManager, products, loadout.Hull, isAvailable, "hull", failures);
        var hullData = cache.Get(loadout.Hull);
        if (hullData is { HullType: HullType.Station })
            failures.Add($"hull: {hullData.Name} is a station hull; a preset builds a ship or a turret");
        var slotBuilds = loadout.Slots.Select(slot => Resolve(itemManager, products, slot.Design, isAvailable, Cell(slot), failures)).ToArray();
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

        var hull = hullBuild();
        var settings = itemManager.GameplaySettings.DefaultEntitySettings;
        Entity entity = hullData.HullType == HullType.Turret
            ? new OrbitalEntity(itemManager, liveZone, hull, default, settings)
            : new Ship(itemManager, liveZone, hull, settings);
        var equipped = new EquippedItem[loadout.Slots.Count];
        for (var i = 0; i < loadout.Slots.Count; i++)
        {
            var slot = loadout.Slots[i];
            var unit = slotBuilds[i]();
            unit.Rotation = slot.Rotation;
            if (!entity.TryEquip(unit, slot.Position))
            {
                failures.Add($"{Cell(slot)}: {cache.Get(slot.Design).Name} does not fit");
                continue;
            }

            equipped[i] = entity.Equipment
                .Concat<EquippedItem>(entity.CargoBays)
                .Concat(entity.DockingBays)
                .First(item => item.EquippableItem == unit);
        }
        if (failures.Count > reported) return null;

        var built = new (List<Weapon> weapons, List<EquippedItem> items)[groupCount];
        for (var g = 0; g < groupCount; g++)
        {
            var items = g < groups.Length ? groups[g].Select(i => equipped[i]).ToList() : new List<EquippedItem>();
            built[g] = (items.Select(item => item.GetBehavior<Weapon>()).ToList(), items);
        }
        entity.WeaponGroups = built;
        return entity;
    }

    // How one design is built, or null with the failure listed; nothing is minted until the returned builder runs. A
    // design is built by its first available product in record-key order. A design with no product at all is outside
    // the economy (docs/scenarios-cut.md, Q3: generation, stock and sale all go through products) and is built
    // unbranded, with no maker, at UnbrandedQuality. A design whose products are all unavailable is not built.
    public static Func<EquippableItem> Resolve<T>(ItemManager itemManager, CultRecordRef<T> design,
        Predicate<FactionProductData> isAvailable, string where, List<string> failures) where T : EquippableItemData =>
        Resolve(itemManager, ProductsInKeyOrder(itemManager.ItemData), design, isAvailable, where, failures);

    private static Func<EquippableItem> Resolve<T>(ItemManager itemManager, FactionProductData[] products,
        CultRecordRef<T> design, Predicate<FactionProductData> isAvailable, string where, List<string> failures)
        where T : EquippableItemData
    {
        var data = itemManager.ItemData.Get(design);
        if (data == null)
        {
            failures.Add($"{where}: design {design.Key} is not in the catalog");
            return null;
        }

        var made = products.Where(p => p.Design.Key.Equals(design.Key)).ToArray();
        if (made.Length == 0)
            return () => (EquippableItem) itemManager.CreateInstance(itemManager.CreateLot(data, default, UnbrandedQuality));

        var product = made.FirstOrDefault(p => isAvailable(p));
        if (product == null)
        {
            failures.Add($"{where}: no available product of {data.Name}");
            return null;
        }
        return () => (EquippableItem) itemManager.CreateInstance(product);
    }

    private static FactionProductData[] ProductsInKeyOrder(CultCache cache) =>
        cache.GetAll<FactionProductData>().OrderBy(p => cache.RefOf(p).Key.Value, StringComparer.Ordinal).ToArray();

    private static string Cell(LoadoutSlot slot) => $"slot {slot.Position.x},{slot.Position.y}";
}
