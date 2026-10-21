using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using CultMath;

// A Loadout: a hull together with item designs or products equipped to specific cells (ruling boss-gear-is-a-loadout).
// A slot names a design, and names a product only where the loadout demands one; a design-only slot's product is chosen
// by whoever materializes it from the galaxy it is built in (ruling loadout-product-optional-context-chooses), which
// keeps a loadout portable across galaxies. A faction's boss flies one (Faction.BossLoadout). Authored catalog data:
// written in CultCache Studio or by the AetherDb and editor capture commands, read at runtime.
[CultDocument("aetheria.loadout", "1"), MessagePackObject]
public class Loadout
{
    [CultName, Key(0)] public string Name;
    [Key(1)] public CultRecordRef<HullData> Hull;
    [Key(2)] public List<LoadoutSlot> Slots = new List<LoadoutSlot>();
    [Key(3)] public int[][] WeaponGroups;          // indices into Slots
    [Key(4)] public CultRecordRef<FactionProductData> HullProduct; // unset: the hull's design alone, chosen at materialization
}

[MessagePackObject]
public class LoadoutSlot
{
    [Key(0)] public int2 Position;                 // hull cell passed to Entity.TryEquip(item, int2)
    [Key(1)] public ItemRotation Rotation;
    [Key(2)] public CultRecordRef<EquippableItemData> Design;
    [Key(3)] public CultRecordRef<FactionProductData> Product; // unset: design-only, chosen at materialization
}

// Why a faction has no boss Loadout to build; see Loadouts.BossOf.
public enum BossRefusal { None, Unset, Missing }

// The owner of loadouts: capture from a live ship, the only writer, and the only builder of an entity from a loadout.
public static class Loadouts
{
    // Records the hull design and one slot per equipped item (gear, then cargo bays, then docking bays) with its cell,
    // rotation, design and product (unset for an unbranded unit). Entity.Equipment also holds the hull's own unit; that is
    // Hull, not a slot. Nothing else about the units: no quality, ingredients, cargo or state.
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
            HullProduct = ProductRef(itemManager, entity.Hull),
            Slots = items.Select(item => new LoadoutSlot
            {
                Position = item.Position,
                Rotation = item.EquippableItem.Rotation,
                Design = cache.RefOf(itemManager.GetData(item.EquippableItem)),
                Product = ProductRef(itemManager, item.EquippableItem)
            }).ToList(),
            WeaponGroups = entity.WeaponGroups
                .Select(group => group.items.Select(item => items.IndexOf(item)).ToArray())
                .ToArray()
        };
    }

    private static CultRecordRef<FactionProductData> ProductRef(ItemManager itemManager, CraftedItemInstance unit) =>
        itemManager.Brand(unit).Product is { } product ? itemManager.ItemData.RefOf(product) : default;

    // A loadout's key derives from its name, so capturing the same name again addresses the same record.
    public static CultRecordKey KeyOf(string name) => new CultRecordKey("loadout:" + name);

    // The one resolver of a faction's boss reference (ruling faction-boss-is-a-loadout-reference): the Loadout
    // Faction.BossLoadout names. The boss is never generated, so this reads nothing but the reference and the catalog. Null with
    // Unset when the faction sets none, and null with Missing when it sets one the catalog does not hold.
    public static Loadout BossOf(CultCache items, Faction faction, out BossRefusal refusal)
    {
        if (!faction.BossLoadout.IsSet())
        {
            refusal = BossRefusal.Unset;
            return null;
        }

        var loadout = items.Get(faction.BossLoadout);
        refusal = loadout == null ? BossRefusal.Missing : BossRefusal.None;
        return loadout;
    }

    // Names the faction's field, and echoes no key or name of the input.
    public static string Describe(BossRefusal refusal) => refusal switch
    {
        BossRefusal.Unset => "sets no Faction.BossLoadout",
        BossRefusal.Missing => "names a Faction.BossLoadout that is not in the catalog",
        _ => ""
    };

    // The only writer of Loadout records. It opens its own short-lived cache over the catalog file, writable, and disposes it
    // after the one commit. No other cache is touched, so no catalog instance play holds (or has mutated) can reach the
    // file; a cache that already has the catalog open sees the loadout only once it reloads.
    // The catalog is a stack of packages, the shipped content included (ruling catalog-is-a-package-stack), so a loadout may
    // name any design the stack holds; one whose package is later removed fails closed where it is built, with its reason logged.
    // Throws and writes nothing when the file is missing (capture never creates a catalog), when a loadout of this name
    // exists under another key, or when one exists under KeyOf(Name) and replace is not set. The commit is conditional
    // on the record at KeyOf(Name), so a loadout changed on disk since this open is not clobbered: returns false then.
    public static bool Commit(string catalogPath, Loadout loadout, bool replace)
    {
        if (!File.Exists(catalogPath))
            throw new InvalidOperationException($"Catalog {catalogPath} does not exist; a loadout commit never creates one.");
        using var cache = AetheriaStores.Open(catalogPath, catalogWritable: true);
        var key = KeyOf(loadout.Name);
        var namesake = cache.GetAll<Loadout>().FirstOrDefault(other => other.Name == loadout.Name && !cache.RefOf(other).Key.Equals(key));
        if (namesake != null)
            throw new InvalidOperationException($"Loadout '{loadout.Name}' already exists under key {cache.RefOf(namesake).Key}; rename or remove it first.");
        var existing = cache.Get<Loadout>(key);
        if (existing != null && !replace)
            throw new InvalidOperationException($"Loadout '{loadout.Name}' already exists; replace it explicitly.");
        return cache.Commit(batch =>
        {
            batch.Expect(key, existing);
            batch.Upsert(typeof(Loadout), loadout, key);
        });
    }

    // The quality a design no manufacturer makes is built at: the console give's.
    public const float UnbrandedQuality = .95f;

    // The seed of the scratch item manager a fit is tried on. It draws nothing, so any nonzero constant does.
    private const uint ScratchSeed = 1u;

    // All-or-nothing: returns an entity only when every design resolved and fitted, the loadout has at most
    // WeaponGroupCount weapon groups, and every group index names a weapon slot. Every failure of a stage is listed; on any
    // failure nothing is returned and the zone and cache are unchanged. The stages run in the order that puts refusal
    // first: what the catalog alone decides (designs, pinned products, hull kind, weapon groups), then whether the cells
    // fit, tried on a throwaway item manager with its own ledger and stream, and only then the choice of a design-only
    // slot's product (choose may draw) and the minting of lots. So a loadout that cannot be built mints no lot in the live
    // ledger and draws nothing from itemManager.Random or from choose. The one exception is a design-only slot with no
    // available product, which only choose can tell: it is found after an earlier slot's choice, and it too mints nothing.
    // How each design is built is Resolve's rule. A design-only slot is chosen by choose; a game
    // spawner is to pass LoadoutGenerator.ChooseProduct, with no fallback to any manufacturer; RunStart does, and so does the
    // boss in ZoneGenerator, which is built here too from its faction's Loadout (Loadouts.BossOf). The hull's type
    // picks the entity: a ship hull builds a Ship, a turret hull an OrbitalEntity with no orbit, which stays where it is
    // put; a station hull is refused. The entity always gets exactly WeaponGroupCount groups; a loadout with fewer is
    // padded with empty groups. A stated quality mints every lot of the fit at it, drawing nothing (a scenario states
    // one for a fit its checks rest on).
    public static Entity Materialize(ItemManager itemManager, Zone liveZone, Loadout loadout,
        Func<CraftedItemData, FactionProductData> choose, List<string> failures, float? quality = null)
    {
        var cache = itemManager.ItemData;
        var reported = failures.Count;

        var hullSettled = Settle(itemManager, loadout.Hull, loadout.HullProduct, "hull", failures);
        var hullData = cache.Get(loadout.Hull);
        if (hullData is { HullType: HullType.Station })
            failures.Add($"hull: {hullData.Name} is a station hull; a loadout builds a ship or a turret");
        var slotsSettled = loadout.Slots.Select(slot => Settle(itemManager, slot.Design, slot.Product, Cell(slot), failures)).ToArray();
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

        // The cells fit or they do not whatever brand a design-only slot takes: the shape is the design's. Tried on its own
        // item manager, so nothing is minted into the live ledger and nothing is drawn.
        var scratch = new ItemManager(ScratchSeed, cache, new ProvenanceLedger(), itemManager.GameplaySettings, _ => { });
        Assemble(scratch, null, hullData, loadout, Unbranded(scratch, hullSettled), slotsSettled.Select(settled => Unbranded(scratch, settled)).ToArray(), failures, out _);
        if (failures.Count > reported) return null;

        var hullBuild = Build(itemManager, hullSettled, choose, quality, failures);
        var slotBuilds = slotsSettled.Select(settled => Build(itemManager, settled, choose, quality, failures)).ToArray();
        if (failures.Count > reported) return null;

        var entity = Assemble(itemManager, liveZone, hullData, loadout, hullBuild, slotBuilds, failures, out var equipped);
        if (entity == null) return null;

        var built = new (List<Weapon> weapons, List<EquippedItem> items)[groupCount];
        for (var g = 0; g < groupCount; g++)
        {
            var items = g < groups.Length ? groups[g].Select(i => equipped[i]).ToList() : new List<EquippedItem>();
            built[g] = (items.Select(item => item.GetBehavior<Weapon>()).ToList(), items);
        }
        entity.WeaponGroups = built;
        return entity;
    }

    // The hull, then each slot's gear, made by the given builders and equipped to the loadout's cells: the one placement of
    // a loadout, run once on the throwaway manager to decide the fit and once on the live one to build. Null with every
    // misfit listed when any cell does not take its gear.
    private static Entity Assemble(ItemManager itemManager, Zone liveZone, HullData hullData, Loadout loadout,
        Func<CraftedItemInstance> hullBuild, Func<CraftedItemInstance>[] slotBuilds, List<string> failures, out EquippedItem[] equipped)
    {
        var reported = failures.Count;
        var cache = itemManager.ItemData;
        var hull = (EquippableItem) hullBuild();
        var settings = itemManager.GameplaySettings.DefaultEntitySettings;
        Entity entity = hullData.HullType == HullType.Turret
            ? new OrbitalEntity(itemManager, liveZone, hull, default, settings)
            : new Ship(itemManager, liveZone, hull, settings);
        equipped = new EquippedItem[loadout.Slots.Count];
        for (var i = 0; i < loadout.Slots.Count; i++)
        {
            var slot = loadout.Slots[i];
            var unit = (EquippableItem) slotBuilds[i]();
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
        return failures.Count > reported ? null : entity;
    }

    // What the catalog alone says about one design of a loadout: the design, and whether it pins a product, has no product
    // at all, or needs a chooser to name one. Nothing is minted and nothing drawn.
    private sealed class Settled
    {
        public CraftedItemData Data;
        public FactionProductData Pinned;   // the product the loadout names, if it does
        public bool Unbranded;              // a design no product makes
        public string Where;
    }

    // Null with the failure listed.
    private static Settled Settle<T>(ItemManager itemManager, CultRecordRef<T> design, CultRecordRef<FactionProductData> product,
        string where, List<string> failures) where T : CraftedItemData
    {
        var cache = itemManager.ItemData;
        var data = cache.Get(design);
        if (data == null)
        {
            failures.Add($"{where}: design {design.Key} is not in the catalog");
            return null;
        }

        if (product.IsSet())
        {
            var made = cache.Get(product);
            if (made == null)
            {
                failures.Add($"{where}: product {product.Key} is not in the catalog");
                return null;
            }
            if (!made.Design.Key.Equals(design.Key))
            {
                failures.Add($"{where}: product {made.Name} is not a product of {data.Name}");
                return null;
            }
            return new Settled { Data = data, Pinned = made, Where = where };
        }

        return new Settled
        {
            Data = data, Where = where,
            Unbranded = !cache.GetAll<FactionProductData>().Any(p => p.Design.Key.Equals(design.Key))
        };
    }

    // A design made with no product, at UnbrandedQuality: no maker, no draw. It is also the stand-in the fit trial builds
    // every design as, since a brand never changes a shape. A settled design that failed is never built.
    private static Func<CraftedItemInstance> Unbranded(ItemManager itemManager, Settled settled) =>
        () => itemManager.CreateInstance(itemManager.CreateLot(settled.Data, default, UnbrandedQuality));

    // How a settled design is built, or null with the failure listed; nothing is minted until the returned builder runs.
    // A design-only slot is where choose is asked, and may draw.
    private static Func<CraftedItemInstance> Build(ItemManager itemManager, Settled settled,
        Func<CraftedItemData, FactionProductData> choose, float? quality, List<string> failures)
    {
        if (settled == null) return null;
        if (settled.Unbranded)
            return () => itemManager.CreateInstance(itemManager.CreateLot(settled.Data, default, quality ?? UnbrandedQuality));
        var made = settled.Pinned;
        if (made == null)
        {
            made = choose(settled.Data);
            if (made == null)
            {
                failures.Add($"{settled.Where}: no available product of {settled.Data.Name}");
                return null;
            }
            if (!made.Design.Key.Equals(itemManager.ItemData.RefOf(settled.Data).Key))
            {
                failures.Add($"{settled.Where}: product {made.Name} is not a product of {settled.Data.Name}");
                return null;
            }
        }
        return () => itemManager.CreateInstance(quality is { } q ? itemManager.CreateLot(made, q) : itemManager.CreateLot(made));
    }

    // How one design is built, or null with the failure listed; nothing is minted until the returned builder runs. A
    // pinned product is built as named, whatever choose says; it must be a product of the design. A design with no
    // product at all is outside the economy (docs/scenarios-cut.md, Q3: generation, stock and sale all go through
    // products) and is built unbranded, with no maker, at UnbrandedQuality (a stated quality replaces it). Otherwise choose names the product, and
    // null (nothing available) or a product of another design is a failure.
    public static Func<CraftedItemInstance> Resolve<T>(ItemManager itemManager, CultRecordRef<T> design,
        CultRecordRef<FactionProductData> product, Func<CraftedItemData, FactionProductData> choose, string where,
        List<string> failures, float? quality = null) where T : CraftedItemData =>
        Build(itemManager, Settle(itemManager, design, product, where, failures), choose, quality, failures);

    private static string Cell(LoadoutSlot slot) => $"slot {slot.Position.x},{slot.Position.y}";
}
