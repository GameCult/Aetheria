using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// Loadouts hold only item designs, live in the player store, and materialize all-or-nothing against the catalog.
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
        cache.Upsert(new GearData { Name = "Orphan", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1 });

        foreach (var (name, design) in new[] { ("Skiff by Maker", hull.Key), ("Lamp by Maker", gear.Key), ("Crate by Maker", cargo.Key) })
            cache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = maker });
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private CultCache Open() => AetheriaStores.Open(Catalog, Run, Player);

    // Gear on the hull's hardpoint cell, cargo on an interior cell, and the gear alone in the first weapon group.
    private static Loadout HandBuilt(CultCache cache, string name = "Skiff build") => new Loadout
    {
        Name = name,
        Hull = cache.RefOf(cache.GetByName<HullData>("Skiff")),
        Slots =
        {
            new LoadoutSlot { Position = HardpointCell, Design = cache.RefOf<EquippableItemData>(cache.GetByName<GearData>("Lamp")) },
            new LoadoutSlot { Position = InteriorCell, Design = cache.RefOf<EquippableItemData>(cache.GetByName<CargoBayData>("Crate")) }
        },
        WeaponGroups = new[] { new[] { 0 }, new int[0] }
    };

    [Fact]
    public void LoadoutRoundTripsThroughAFreshCache()
    {
        Loadout hand;
        using (var cache = Open())
        {
            var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
            hand = HandBuilt(cache);
            var failures = new List<string>();
            var ship = Build(items,hand, failures);
            Assert.Empty(failures);
            Assert.NotNull(ship);
            Loadouts.Save(cache, Loadouts.Capture(items, ship, hand.Name));
            Loadouts.Save(cache, Loadouts.Capture(items, ship, hand.Name));
        }

        using (var cache = Open())
        {
            Assert.Single(cache.GetAll<Loadout>());
            var loaded = cache.GetByName<Loadout>(hand.Name);
            Assert.Equal(hand.Hull.Key, loaded.Hull.Key);
            Assert.Equal(hand.Slots.Select(Describe), loaded.Slots.Select(Describe));
            Assert.Equal(hand.WeaponGroups, loaded.WeaponGroups);

            var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
            var failures = new List<string>();
            var ship = Build(items,loaded, failures);
            Assert.Empty(failures);
            Assert.Equal(hand.Slots.Select(slot => (slot.Position, slot.Design.Key)),
                ship.Equipment.Concat<EquippedItem>(ship.CargoBays)
                    .Where(item => item != ship.EquippedHull)
                    .Select(item => (item.Position, cache.RefOf(items.GetData(item.EquippableItem)).Key)));
        }

        Assert.Single(SchemaNames(Player), name => name == "aetheria.loadout");
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

        using (var cache = Open())
            Loadouts.Save(cache, HandBuilt(cache));
        Assert.Contains("aetheria.loadout", SchemaNames(Player));
        Assert.True(!File.Exists(Run) || SchemaNames(Run).Length == 0);
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
            Assert.Null(Build(items,missing, failures));
            var failure = Assert.Single(failures);
            Assert.Contains("1,1", failure);
            Assert.Contains("absent-design", failure);

            missing.Slots.Add(new LoadoutSlot { Position = new int2(2, 2), Design = cache.RefOf<EquippableItemData>(cache.GetByName<GearData>("Orphan")) });
            failures.Clear();
            Assert.Null(Build(items,missing, failures));
            Assert.Equal(2, failures.Count);
            Assert.Contains("slot 1,1", failures[0]);
            Assert.Contains("slot 2,2", failures[1]);
            Assert.Contains("no available product of Orphan", failures[1]);
        }

        Assert.Equal(before, new[] { Catalog, Run, Player }.Select(Hash).ToArray());
    }

    [Fact]
    public void FailedFitBuildsNothingAndChargesNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var clash = HandBuilt(cache);
        clash.Slots[1].Position = HardpointCell;
        var failures = new List<string>();
        var credits = 1000;
        Assert.Null(Loadouts.Materialize(items, null, clash, _ => true, ref credits, failures));
        Assert.Equal(1000, credits);
        var failure = Assert.Single(failures);
        Assert.Contains("slot 0,0: Crate does not fit", failure);
    }

    [Fact]
    public void PriceIsTheSumOfDesignPricesAndIsCharged()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var hand = HandBuilt(cache);
        Assert.Equal(100 + 10 + 5, Loadouts.Price(items, hand));
        var credits = 1000;
        Assert.NotNull(Loadouts.Materialize(items, null, hand, _ => true, ref credits, new List<string>()));
        Assert.Equal(1000 - 115, credits);
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
            var credits = 1000;
            var ship = Loadouts.Materialize(items, null, hand, available, ref credits, failures);
            return ship == null ? null : cache.Get(ship.Equipment.Single(item => item.EquippableItem != ship.Hull && item.Position.Equals(HardpointCell)).EquippableItem.Product).Name;
        }

        var failures = new List<string>();
        Assert.Equal("lamp-a", LampProduct(p => p.Name != "Lamp by Maker", failures));
        Assert.Equal("lamp-z", LampProduct(p => p.Name != "Lamp by Maker" && p.Name != "lamp-a", failures));
        Assert.Empty(failures);
        Assert.Null(LampProduct(p => !p.Name.StartsWith("Lamp") && !p.Name.StartsWith("lamp"), failures));
        Assert.Contains("slot 0,0: no available product of Lamp", Assert.Single(failures));
    }

    // Entity.TryUnequip leaves a non-weapon item in its weapon group; Capture must not record it.
    [Fact]
    public void CaptureSkipsGroupedItemsNoLongerEquipped()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var ship = Build(items, HandBuilt(cache), new List<string>());
        Assert.NotNull(ship.TryUnequip(Assert.Single(ship.WeaponGroups[0].items)));
        Assert.NotEmpty(ship.WeaponGroups[0].items);

        var captured = Loadouts.Capture(items, ship, "stripped");
        Assert.Single(captured.Slots);
        Assert.All(captured.WeaponGroups, group => Assert.Empty(group));
        var failures = new List<string>();
        Assert.NotNull(Build(items, captured, failures));
        Assert.Empty(failures);
    }

    [Fact]
    public void OutOfRangeWeaponGroupIndexBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var stale = HandBuilt(cache);
        stale.WeaponGroups = new[] { new[] { 0, 2 }, new[] { -1 } };
        var failures = new List<string>();
        var credits = 1000;
        Assert.Null(Loadouts.Materialize(items, null, stale, _ => true, ref credits, failures));
        Assert.Equal(1000, credits);
        Assert.Equal(new[] { "weapon group: no slot 2", "weapon group: no slot -1" }, failures);
    }

    [Fact]
    public void OneCreditShortBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var hand = HandBuilt(cache);
        var failures = new List<string>();
        var credits = Loadouts.Price(items, hand) - 1;
        Assert.Null(Loadouts.Materialize(items, null, hand, _ => true, ref credits, failures));
        Assert.Equal(114, credits);
        Assert.Equal("cannot afford 115 credits", Assert.Single(failures));
    }

    [Fact]
    public void UnequippingAGroupedWeaponRemovesItFromItsGroups()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var gun = new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1, Behaviors = { new InstantWeaponData() } };
        cache.Commit(batch => batch.Upsert(typeof(GearData), gun, new CultRecordKey("gun")));
        cache.Commit(batch => batch.Upsert(typeof(FactionProductData), new FactionProductData
        {
            Name = "Gun by Maker", Design = new CultRecordRef<CraftedItemData>(new CultRecordKey("gun")),
            Manufacturer = cache.RefOf(cache.GetByName<Faction>("Maker"))
        }, new CultRecordKey("gun-by-maker")));

        var items = new ItemManager(cache, RunSaveTests.TestSettings(), _ => { });
        var armed = HandBuilt(cache);
        armed.Slots[0].Design = new CultRecordRef<EquippableItemData>(new CultRecordKey("gun"));
        var ship = Build(items, armed, new List<string>());
        var item = Assert.Single(ship.WeaponGroups[0].items);
        Assert.NotNull(item.GetBehavior<Weapon>());

        Assert.NotNull(ship.TryUnequip(item));
        Assert.All(ship.WeaponGroups, group => Assert.Empty(group.items));
        Assert.All(ship.WeaponGroups, group => Assert.Empty(group.weapons));
    }

    // Everything available and affordable: for tests about placement and failure lists.
    private static Ship Build(ItemManager items, Loadout loadout, List<string> failures)
    {
        var credits = int.MaxValue;
        return Loadouts.Materialize(items, null, loadout, _ => true, ref credits, failures);
    }

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
