using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// Loadouts are ship presets: item designs only, authored into the catalog, materialized all-or-nothing against it.
// The fixture hull is 3x3: InteriorCells is Shape.Shrink(), so a 2x2 hull has no interior cell a cargo bay could use.
public sealed class LoadoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-loadout-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private string Run => Path.Combine(_root, "run.cc");
    private string Player => Path.Combine(_root, "player.cc");

    private static readonly int2 HardpointCell = new int2(0, 0);
    private static readonly int2 InteriorCell = new int2(1, 1);

    public LoadoutTests()
    {
        Directory.CreateDirectory(_root);
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var maker = cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });

        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hull = cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Price = 100,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
        });
        var gear = cache.Upsert(new GearData { Name = "Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10 });
        var cargo = cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = new Shape(), Price = 5 });
        var gun = cache.Upsert(new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1, Behaviors = { new InstantWeaponData() } });
        cache.Upsert(new GearData { Name = "Orphan", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1 });

        foreach (var (name, design) in new[] { ("Skiff by Maker", hull.Key), ("Lamp by Maker", gear.Key), ("Crate by Maker", cargo.Key), ("Gun by Maker", gun.Key) })
            cache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = maker });
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    // The game's shape: a read-only catalog with the run and player stores.
    private CultCache Open() => AetheriaStores.Open(Catalog, Run, Player);

    // The editor's shape: the catalog writable, as the capture command sees it.
    private CultCache OpenAuthoring() => AetheriaStores.Open(Catalog, Run, Player, catalogWritable: true);

    // Gear on the hull's hardpoint cell, cargo on an interior cell, and two empty weapon groups (fewer than the game's six).
    private static Loadout HandBuilt(CultCache cache, string name = "Skiff build") => new Loadout
    {
        Name = name,
        Hull = cache.RefOf(cache.GetByName<HullData>("Skiff")),
        Slots =
        {
            new LoadoutSlot { Position = HardpointCell, Design = cache.RefOf<EquippableItemData>(cache.GetByName<GearData>("Lamp")) },
            new LoadoutSlot { Position = InteriorCell, Design = cache.RefOf<EquippableItemData>(cache.GetByName<CargoBayData>("Crate")) }
        },
        WeaponGroups = new[] { new int[0], new int[0] }
    };

    // HandBuilt with the Gun on the hardpoint, alone in the first of the game's six weapon groups.
    private static Loadout Armed(CultCache cache, string name = "Armed build")
    {
        var armed = HandBuilt(cache, name);
        armed.Slots[0].Design = cache.RefOf<EquippableItemData>(cache.GetByName<GearData>("Gun"));
        armed.WeaponGroups = Enumerable.Range(0, 6).Select(g => g == 0 ? new[] { 0 } : new int[0]).ToArray();
        return armed;
    }

    private static Loadout WithDesign(Loadout loadout, int slot, CultCache cache, string gear)
    {
        loadout.Slots[slot].Design = cache.RefOf<EquippableItemData>(cache.GetByName<GearData>(gear));
        return loadout;
    }

    // Capture from a built ship, commit to the catalog, reopen fresh read-only: the preset and the ship it builds match.
    [Fact]
    public void CapturedPresetRoundTripsThroughTheCatalog()
    {
        Loadout hand;
        using (var cache = OpenAuthoring())
        {
            var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
            hand = Armed(cache);
            var failures = new List<string>();
            var ship = Build(items, hand, failures);
            Assert.Empty(failures);
            Assert.NotNull(ship);
            Assert.True(Loadouts.Commit(cache, Loadouts.Capture(items, ship, hand.Name), replace: false));
        }

        using (var cache = Open())
        {
            var loaded = Assert.Single(cache.GetAll<Loadout>());
            Assert.Equal(Loadouts.KeyOf(hand.Name), cache.RefOf(loaded).Key);
            Assert.Equal(hand.Name, loaded.Name);
            Assert.Equal(hand.Hull.Key, loaded.Hull.Key);
            Assert.Equal(hand.Slots.Select(Describe), loaded.Slots.Select(Describe));
            Assert.Equal(hand.WeaponGroups, loaded.WeaponGroups);

            var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
            var failures = new List<string>();
            var ship = Build(items, loaded, failures);
            Assert.Empty(failures);
            Assert.Equal(hand.Slots.Select(slot => (slot.Position, slot.Design.Key)),
                ship.Equipment.Concat<EquippedItem>(ship.CargoBays)
                    .Where(item => item != ship.EquippedHull)
                    .Select(item => (item.Position, cache.RefOf(items.GetData(item.EquippableItem)).Key)));
        }

        Assert.Single(SchemaNames(Catalog), name => name == "aetheria.loadout");
        Assert.True(!File.Exists(Player) || !SchemaNames(Player).Contains("aetheria.loadout"));
    }

    // Recapturing a name refuses without replace and leaves the file untouched; with replace it replaces, never duplicates.
    [Fact]
    public void SameNameCaptureReplacesOnlyWhenAsked()
    {
        using (var cache = OpenAuthoring())
        {
            Assert.True(Loadouts.Commit(cache, HandBuilt(cache), replace: false));
            var before = Hash(Catalog);
            var error = Assert.Throws<InvalidOperationException>(() => Loadouts.Commit(cache, Armed(cache, "Skiff build"), replace: false));
            Assert.Contains("Skiff build", error.Message);
            Assert.Equal(before, Hash(Catalog));
            Assert.Equal(2, Assert.Single(cache.GetAll<Loadout>()).WeaponGroups.Length);

            Assert.True(Loadouts.Commit(cache, Armed(cache, "Skiff build"), replace: true));
            Assert.Equal(6, Assert.Single(cache.GetAll<Loadout>()).WeaponGroups.Length);
        }

        using (var cache = Open())
            Assert.Equal(6, Assert.Single(cache.GetAll<Loadout>()).WeaponGroups.Length);
    }

    // The editor's catalog is writable while play mutates live catalog instances; a preset commit must land only itself.
    [Fact]
    public void PresetCommitWritesNoOtherInMemoryCatalogState()
    {
        using (var cache = OpenAuthoring())
        {
            cache.GetByName<Faction>("Maker").ShortName = "XXX";
            Assert.True(Loadouts.Commit(cache, HandBuilt(cache), replace: false));
        }

        using (var cache = Open())
        {
            Assert.Equal("MKR", cache.GetByName<Faction>("Maker").ShortName);
            Assert.Single(cache.GetAll<Loadout>());
        }
    }

    [Fact]
    public void LoadoutHoldsNoRunOrGalaxyRefs()
    {
        var forbidden = new[] { typeof(ItemInstance), typeof(EntityPack), typeof(FactionProductData), typeof(Faction) }
            .Concat(AetheriaStores.RunTypes)
            .ToArray();
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>(new[] { typeof(Loadout) });
        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (!seen.Add(type)) continue;
            Assert.DoesNotContain(forbidden, bad => bad.IsAssignableFrom(type));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(CultRecordRef<>))
            {
                Assert.True(typeof(EquippableItemData).IsAssignableFrom(type.GetGenericArguments()[0]), $"{type} names a non-design record");
                continue;
            }
            if (type.IsArray) pending.Enqueue(type.GetElementType());
            foreach (var argument in type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes) pending.Enqueue(argument);
            if (type.Assembly == typeof(Loadout).Assembly)
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    pending.Enqueue(field.FieldType);
        }

        using (var cache = OpenAuthoring())
            Loadouts.Commit(cache, HandBuilt(cache), replace: false);
        Assert.Contains("aetheria.loadout", SchemaNames(Catalog));
        Assert.True(!File.Exists(Run) || SchemaNames(Run).Length == 0);
        Assert.True(!File.Exists(Player) || SchemaNames(Player).Length == 0);
    }

    [Fact]
    public void MissingDesignReports()
    {
        using (var cache = Open())
            cache.Commit(batch => batch.Upsert(new PlayerSettings()));
        var before = new[] { Catalog, Run, Player }.Select(Hash).ToArray();

        using (var cache = Open())
        {
            var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
            var missing = HandBuilt(cache);
            missing.Slots[1].Design = new CultRecordRef<EquippableItemData>(new CultRecordKey("absent-design"));
            var failures = new List<string>();
            Assert.Null(Build(items, missing, failures));
            var failure = Assert.Single(failures);
            Assert.Contains("1,1", failure);
            Assert.Contains("absent-design", failure);

            missing.Slots.Add(new LoadoutSlot { Position = new int2(2, 2), Design = cache.RefOf<EquippableItemData>(cache.GetByName<GearData>("Orphan")) });
            failures.Clear();
            Assert.Null(Build(items, missing, failures));
            Assert.Equal(2, failures.Count);
            Assert.Contains("slot 1,1", failures[0]);
            Assert.Contains("slot 2,2", failures[1]);
            Assert.Contains("no available product of Orphan", failures[1]);
        }

        Assert.Equal(before, new[] { Catalog, Run, Player }.Select(Hash).ToArray());
    }

    [Fact]
    public void FailedFitBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var clash = HandBuilt(cache);
        clash.Slots[1].Position = HardpointCell;
        var failures = new List<string>();
        Assert.Null(Build(items, clash, failures));
        var failure = Assert.Single(failures);
        Assert.Contains("slot 0,0: Crate does not fit", failure);
    }

    // Two more Lamp products, upserted into the live cache so its enumeration order (z before a) disagrees with
    // record-key order; a reopened snapshot could already hand them back sorted.
    [Fact]
    public void FirstAvailableProductInKeyOrderBuildsTheSlot()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        var lamp = new CultRecordRef<CraftedItemData>(cache.RefOf(cache.GetByName<GearData>("Lamp")).Key);
        foreach (var key in new[] { "lamp-z", "lamp-a" })
            cache.Commit(batch => batch.Upsert(typeof(FactionProductData), new FactionProductData { Name = key, Design = lamp, Manufacturer = maker }, new CultRecordKey(key)));
        Assert.Equal(new[] { "lamp-z", "lamp-a" }, cache.GetAll<FactionProductData>().Select(p => p.Name).Where(name => name.StartsWith("lamp-")));

        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var hand = HandBuilt(cache);
        string LampProduct(Predicate<FactionProductData> available, List<string> failures)
        {
            var ship = Loadouts.Materialize(items, null, hand, available, failures);
            return ship == null ? null : cache.Get(ship.Equipment.Single(item => item.EquippableItem != ship.Hull && item.Position.Equals(HardpointCell)).EquippableItem.Product).Name;
        }

        var failures = new List<string>();
        Assert.Equal("lamp-a", LampProduct(p => p.Name != "Lamp by Maker", failures));
        Assert.Equal("lamp-z", LampProduct(p => p.Name != "Lamp by Maker" && p.Name != "lamp-a", failures));
        Assert.Empty(failures);
        Assert.Null(LampProduct(p => !p.Name.StartsWith("Lamp") && !p.Name.StartsWith("lamp"), failures));
        Assert.Contains("slot 0,0: no available product of Lamp", Assert.Single(failures));
    }

    [Fact]
    public void OutOfRangeWeaponGroupIndexBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var stale = Armed(cache);
        stale.WeaponGroups = new[] { new[] { 0, 2 }, new[] { -1 } };
        var failures = new List<string>();
        Assert.Null(Build(items, stale, failures));
        Assert.Equal(new[] { "weapon group: no slot 2", "weapon group: no slot -1" }, failures);
    }

    [Fact]
    public void UnequippingAGroupedWeaponRemovesItFromItsGroups()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var ship = Build(items, Armed(cache), new List<string>());
        var item = Assert.Single(ship.WeaponGroups[0].items);
        Assert.NotNull(item.GetBehavior<Weapon>());

        Assert.NotNull(ship.TryUnequip(item));
        Assert.All(ship.WeaponGroups, group => Assert.Empty(group.items));
        Assert.All(ship.WeaponGroups, group => Assert.Empty(group.weapons));
    }

    // A group index on a non-weapon slot would put a null weapon in the group; it is refused before anything is built.
    [Fact]
    public void NonWeaponGroupIndexBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var lampGrouped = WithDesign(Armed(cache), 0, cache, "Lamp");
        var failures = new List<string>();
        Assert.Null(Build(items, lampGrouped, failures));
        Assert.Equal("weapon group: slot 0,0: Lamp is not a weapon", Assert.Single(failures));
    }

    [Fact]
    public void MoreWeaponGroupsThanTheGameHasBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var wide = Armed(cache);
        wide.WeaponGroups = wide.WeaponGroups.Append(new[] { 0 }).ToArray();
        var failures = new List<string>();
        Assert.Null(Build(items, wide, failures));
        Assert.Equal("weapon groups: 7 groups, at most 6", Assert.Single(failures));
    }

    // Fewer groups, or none, still give the ship exactly WeaponGroupCount groups, so GenerateWeaponGroups and G1-G6
    // bindings can index every one.
    [Fact]
    public void FewerWeaponGroupsArePaddedToTheGameCount()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var armed = Armed(cache);
        armed.WeaponGroups = new[] { new[] { 0 } };
        foreach (var groups in new[] { armed.WeaponGroups, null })
        {
            armed.WeaponGroups = groups;
            var failures = new List<string>();
            var ship = Build(items, armed, failures);
            Assert.Empty(failures);
            Assert.Equal(6, ship.WeaponGroups.Length);
            Assert.All(ship.WeaponGroups.Skip(1), group => Assert.Empty(group.items));
            Assert.Equal(groups == null ? 0 : 1, ship.WeaponGroups[0].items.Count);
            ship.GenerateWeaponGroups();
        }
    }

    // Everything available: for tests about placement and failure lists.
    private static Ship Build(ItemManager items, Loadout loadout, List<string> failures) =>
        Loadouts.Materialize(items, null, loadout, _ => true, failures);

    private static (int2, ItemRotation, CultRecordKey) Describe(LoadoutSlot slot) => (slot.Position, slot.Rotation, slot.Design.Key);

    private static string Hash(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "absent";

    private static string[] SchemaNames(string path)
    {
        var snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path));
        return snapshot.Records
            .Select(record => snapshot.SchemaCatalog.Single(entry => entry.SchemaId == record.SchemaId).SchemaName)
            .ToArray();
    }
}
