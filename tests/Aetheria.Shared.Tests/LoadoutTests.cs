using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using Random = CultMath.Random;

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

    // The game's shape in every build, the editor included: a read-only catalog with the run and player stores.
    private CultCache Open() => AetheriaStores.Open(Catalog, Run, Player);

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
        using (var cache = Open())
        {
            var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
            hand = Armed(cache);
            var failures = new List<string>();
            var ship = Build(items, hand, failures);
            Assert.Empty(failures);
            Assert.NotNull(ship);
            Assert.True(Loadouts.Commit(Catalog, Loadouts.Capture(items, ship, hand.Name), replace: false));
        }

        using (var cache = Open())
        {
            var loaded = Assert.Single(cache.GetAll<Loadout>());
            Assert.Equal(Loadouts.KeyOf(hand.Name), cache.RefOf(loaded).Key);
            Assert.Equal(hand.Name, loaded.Name);
            Assert.Equal(hand.Hull.Key, loaded.Hull.Key);
            Assert.Equal(hand.Slots.Select(Describe), loaded.Slots.Select(Describe));
            Assert.Equal(hand.WeaponGroups, loaded.WeaponGroups);

            var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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
        using (var cache = Open())
        {
            Assert.True(Loadouts.Commit(Catalog, HandBuilt(cache), replace: false));
            var before = Hash(Catalog);
            var error = Assert.Throws<InvalidOperationException>(() => Loadouts.Commit(Catalog, Armed(cache, "Skiff build"), replace: false));
            Assert.Contains("Skiff build", error.Message);
            Assert.Equal(before, Hash(Catalog));

            Assert.True(Loadouts.Commit(Catalog, Armed(cache, "Skiff build"), replace: true));
        }

        using (var cache = Open())
            Assert.Equal(6, Assert.Single(cache.GetAll<Loadout>()).WeaponGroups.Length);
    }

    // Play mutates live catalog instances; a preset commit must land only itself.
    [Fact]
    public void PresetCommitWritesNoOtherInMemoryCatalogState()
    {
        using (var cache = Open())
        {
            cache.GetByName<Faction>("Maker").ShortName = "XXX";
            Assert.True(Loadouts.Commit(Catalog, HandBuilt(cache), replace: false));
            cache.FlushAsync().Wait();
        }

        using (var cache = Open())
        {
            Assert.Equal("MKR", cache.GetByName<Faction>("Maker").ShortName);
            Assert.Single(cache.GetAll<Loadout>());
        }
    }

    // The process cache holds the catalog read-only while capture writes through its own cache: the capture lands, and
    // the process cache keeps its instances, gains nothing and stays clean until it reloads.
    [Fact]
    public void CaptureWhileTheCatalogIsHeldReadOnlyLeavesTheProcessCacheUnchanged()
    {
        using (var cache = Open())
        {
            var maker = cache.GetByName<Faction>("Maker");
            Assert.True(Loadouts.Commit(Catalog, HandBuilt(cache), replace: false));
            Assert.Empty(cache.GetAll<Loadout>());
            Assert.Same(maker, cache.GetByName<Faction>("Maker"));
            Assert.False(cache.IsDirty);
            Assert.Throws<InvalidOperationException>(() => cache.Upsert(new Faction { Name = "Usurper" }));
        }

        using (var cache = Open())
            Assert.Equal("Skiff build", Assert.Single(cache.GetAll<Loadout>()).Name);
    }

    // Capture never creates a catalog: a missing file refuses, and no file or directory appears.
    [Fact]
    public void CaptureWithAMissingCatalogRefusesAndCreatesNothing()
    {
        var absent = Path.Combine(_root, "absent", "Aetheria.cc");
        using var cache = Open();
        var error = Assert.Throws<InvalidOperationException>(() => Loadouts.Commit(absent, HandBuilt(cache), replace: false));
        Assert.Contains("does not exist", error.Message);
        Assert.False(Directory.Exists(Path.GetDirectoryName(absent)));
    }

    // A preset authored in Studio has a minted key; capturing its name refuses, replace or not, and names that key.
    [Fact]
    public void SameNameUnderAnotherKeyRefuses()
    {
        using (var writer = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            writer.Upsert(HandBuilt(writer));
            writer.FlushAsync().Wait();
        }
        var before = Hash(Catalog);

        using (var cache = Open())
        {
            var studioKey = cache.RefOf(Assert.Single(cache.GetAll<Loadout>())).Key;
            Assert.NotEqual(Loadouts.KeyOf("Skiff build"), studioKey);
            foreach (var replace in new[] { false, true })
            {
                var error = Assert.Throws<InvalidOperationException>(() => Loadouts.Commit(Catalog, Armed(cache, "Skiff build"), replace));
                Assert.Contains(studioKey.Value, error.Message);
            }
        }

        Assert.Equal(before, Hash(Catalog));
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
            Assert.True(Loadouts.Commit(Catalog, HandBuilt(cache), replace: false));
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
            var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var clash = HandBuilt(cache);
        clash.Slots[1].Position = HardpointCell;
        var failures = new List<string>();
        Assert.Null(Build(items, clash, failures));
        var failure = Assert.Single(failures);
        Assert.Contains("slot 0,0: Crate does not fit", failure);
    }

    // Two new makers, each with one Lamp product, upserted into the live cache so its enumeration order (z before a)
    // disagrees with record-key order; a reopened snapshot could already hand them back sorted. Brand() requires at
    // most one product per (maker, design), so each gets its own maker rather than sharing "Maker".
    [Fact]
    public void FirstAvailableProductInKeyOrderBuildsTheSlot()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var lamp = new CultRecordRef<CraftedItemData>(cache.RefOf(cache.GetByName<GearData>("Lamp")).Key);
        var zMaker = cache.Upsert(new Faction { Name = "Zeta", ShortName = "ZET" });
        var aMaker = cache.Upsert(new Faction { Name = "Alpha", ShortName = "ALP" });
        cache.Commit(batch => batch.Upsert(typeof(FactionProductData), new FactionProductData { Name = "lamp-z", Design = lamp, Manufacturer = zMaker }, new CultRecordKey("lamp-z")));
        cache.Commit(batch => batch.Upsert(typeof(FactionProductData), new FactionProductData { Name = "lamp-a", Design = lamp, Manufacturer = aMaker }, new CultRecordKey("lamp-a")));
        Assert.Equal(new[] { "lamp-z", "lamp-a" }, cache.GetAll<FactionProductData>().Select(p => p.Name).Where(name => name.StartsWith("lamp-")));

        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var hand = HandBuilt(cache);
        string LampProduct(Predicate<FactionProductData> available, List<string> failures)
        {
            var ship = Loadouts.Materialize(items, null, hand, available, failures);
            if (ship == null) return null;
            var unit = ship.Equipment.Single(item => item.EquippableItem != ship.Hull && item.Position.Equals(HardpointCell)).EquippableItem;
            return items.Brand(unit).Product.Name;
        }

        var failures = new List<string>();
        Assert.Equal("lamp-a", LampProduct(p => p.Name != "Lamp by Maker", failures));
        Assert.Equal("lamp-z", LampProduct(p => p.Name != "Lamp by Maker" && p.Name != "lamp-a", failures));
        Assert.Empty(failures);
        Assert.Null(LampProduct(p => !p.Name.StartsWith("Lamp") && !p.Name.StartsWith("lamp"), failures));
        Assert.Contains("slot 0,0: no available product of Lamp", Assert.Single(failures));
    }

    // F2: this test must not enumerate reloaded instances through EntitySerializer.Items, the GC root walk under
    // test. It hand-mints one real lot per EntityPack root (Hull, Equipment, CargoBays, DockingBays, CargoContents,
    // Children, DockingBayContents) so the expected lot ids are known independently of any enumerator, and reads
    // the reloaded pack's own fields directly. Skipping any one root in Items/Reachable drops that root's lot from
    // the stored ledger, which RunSave.Lots' indexer throws loudly on.
    [Fact]
    public void MaterializedLotsSurviveSaveAndReload()
    {
        CultRecordKey maker;
        int[] expectedLots;
        using (var cache = AetheriaStores.Open(Catalog, Run, Player, catalogWritable: true))
        {
            maker = cache.RefOf(cache.GetByName<Faction>("Maker")).Key;
            var dockingBay = cache.Upsert(new DockingBayData { Name = "Bay", Shape = new Shape(), InteriorShape = new Shape(), Price = 20 });
            cache.Upsert(new FactionProductData { Name = "Bay by Maker", Design = new CultRecordRef<CraftedItemData>(dockingBay.Key), Manufacturer = cache.RefOf(cache.GetByName<Faction>("Maker")) });
            cache.FlushAsync().Wait();

            var skiff = cache.GetAll<FactionProductData>().Single(p => p.Name == "Skiff by Maker");
            var lamp = cache.GetAll<FactionProductData>().Single(p => p.Name == "Lamp by Maker");
            var crate = cache.GetAll<FactionProductData>().Single(p => p.Name == "Crate by Maker");
            var gun = cache.GetAll<FactionProductData>().Single(p => p.Name == "Gun by Maker");
            var bay = cache.GetAll<FactionProductData>().Single(p => p.Name == "Bay by Maker");

            var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });

            EquippableItem Mint(FactionProductData product) => (EquippableItem) items.CreateInstance(product);

            var hull = Mint(skiff);
            var equipment = Mint(lamp);
            var cargoBay = Mint(crate);
            var dockingBayUnit = Mint(bay);
            var cargoContent = Mint(gun);
            var dockingBayContent = Mint(lamp);
            var childHull = Mint(skiff);

            var childPack = new ShipPack
            {
                Name = "child", Hull = childHull,
                Equipment = Array.Empty<(int2, EquippableItem)>(), CargoBays = Array.Empty<(int2, EquippableItem)>(),
                DockingBays = Array.Empty<(int2, EquippableItem)>(), CargoContents = Array.Empty<(int2, ItemInstance)[]>(),
                DockingBayContents = Array.Empty<(int2, ItemInstance)[]>(), Children = Array.Empty<EntityPack>(),
                Temperature = new float[0, 0],
                Armor = new float[0, 0], Conductivity = new bool2[0, 0], DockingBayAssignments = Array.Empty<int>(),
                Settings = new EntitySettings(), WeaponGroups = Array.Empty<int[]>()
            };
            var rootPack = new ShipPack
            {
                Name = "root", Hull = hull,
                Equipment = new[] { (default(int2), equipment) },
                CargoBays = new[] { (default(int2), cargoBay) },
                DockingBays = new[] { (default(int2), dockingBayUnit) },
                CargoContents = new[] { new[] { (default(int2), (ItemInstance) cargoContent) } },
                DockingBayContents = new[] { new[] { (default(int2), (ItemInstance) dockingBayContent) } },
                Children = new EntityPack[] { childPack },
                DockingBayAssignments = new[] { 0 },
                Temperature = new float[0, 0],
                Armor = new float[0, 0], Conductivity = new bool2[0, 0],
                Settings = new EntitySettings(), WeaponGroups = Array.Empty<int[]>()
            };

            expectedLots = new[]
            {
                hull.Lot, equipment.Lot, cargoBay.Lot, dockingBayUnit.Lot, cargoContent.Lot, dockingBayContent.Lot, childHull.Lot
            };

            var game = new SavedGame
            {
                Factions = Array.Empty<CultRecordRef<Faction>>(),
                Relationships = Array.Empty<FactionRelationship>(),
                HomeZones = new Dictionary<int, int>(),
                BossZones = new Dictionary<int, int>(),
                DiscoveredZones = new[] { 0 },
                ActionBarBindings = new SavedActionBarBinding[0],
                Exit = -1
            };
            var zones = new[]
            {
                new SavedZone
                {
                    Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                    Contents = new ZonePack { Entities = new List<EntityPack> { rootPack } }
                }
            };
            RunSave.Commit(cache, game, zones, items.Lots);
        }

        using (var cache = Open())
        {
            var run = cache.GetGlobal<SavedGame>();
            var zone = cache.Get(run.Zones[0]);
            var reloaded = (ShipPack) zone.Contents.Entities[0];
            var reloadedChild = (ShipPack) reloaded.Children[0];
            var lots = RunSave.Lots(cache);

            Assert.Equal(expectedLots.OrderBy(l => l), new[]
            {
                reloaded.Hull.Lot, reloaded.Equipment[0].item.Lot, reloaded.CargoBays[0].item.Lot,
                reloaded.DockingBays[0].item.Lot, ((CraftedItemInstance) reloaded.CargoContents[0][0].item).Lot,
                ((CraftedItemInstance) reloaded.DockingBayContents[0][0].item).Lot, reloadedChild.Hull.Lot
            }.OrderBy(l => l));

            foreach (var lotId in expectedLots)
            {
                var lot = lots[lotId];
                var attributed = Assert.IsType<Attributed>(lot.Origin);
                Assert.Equal(maker, attributed.Faction.Key);
            }
        }
    }

    // FillInterior requires a capacitor product; this fixture hull adds one alongside a 4x4 hull with two identical
    // Sensors hardpoints.
    [Fact]
    public void MatchingHardpointsShareALot()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        var hullShape = new Shape(4, 4);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hull = cache.Upsert(new HullData
        {
            Name = "Twin", HullType = HullType.Ship, Shape = hullShape, Price = 100,
            Hardpoints =
            {
                new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = new int2(1, 0), Shape = new Shape() }
            }
        });
        var capacitor = cache.Upsert(new GearData { Name = "Cap", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new CapacitorData() } });
        foreach (var (name, design) in new[] { ("Twin by Maker", hull.Key), ("Cap by Maker", capacitor.Key) })
            cache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = maker });
        cache.FlushAsync().Wait();

        var random = new Random(1);
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
        var pack = generator.GenerateShipLoadout(candidate => candidate.Name == "Twin");
        Assert.NotNull(pack);

        var hardpointItems = pack.Equipment
            .Where(e => (cache.Get(e.item.Data) as GearData)?.Hardpoint == HardpointType.Sensors)
            .ToArray();
        Assert.Equal(2, hardpointItems.Length);
        Assert.Equal(hardpointItems[0].item.Lot, hardpointItems[1].item.Lot);
    }

    // A Lamp lot with Quality .2 and one role fill (lens .9): a stat reading that role gets .9, one reading none
    // gets .2, and GetTier reads .2.
    [Fact]
    public void StatsReadTheLot()
    {
        using var cache = Open();
        var settings = RunSaveTests.TestSettings();
        // Two tiers with distinct quality thresholds: a single-tier fixture cannot fail "GetTier ignores lot
        // quality", since every lot would resolve to the only tier regardless of its own Quality.
        settings.Tiers = new[]
        {
            new RarityTier { Name = "Common", Quality = .2f, Rarity = 0, Color = new float3(1, 1, 1) },
            new RarityTier { Name = "Rare", Quality = .5f, Rarity = 1, Color = new float3(1, 1, 1) }
        };
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
        var lamp = cache.GetByName<GearData>("Lamp");
        var commonLot = items.Lots.Add(new Lot
        {
            Design = cache.RefOf<ItemData>(lamp),
            Origin = new Attributed(),
            Quality = .2f,
            Roles = new List<RoleFill> { new RoleFill { Role = "lens", Quality = .9f } }
        });
        var rareLot = items.Lots.Add(new Lot
        {
            Design = cache.RefOf<ItemData>(lamp),
            Origin = new Attributed(),
            Quality = .6f,
            Roles = new List<RoleFill>()
        });
        var instance = (EquippableItem) items.CreateInstance(commonLot);
        var rareInstance = (EquippableItem) items.CreateInstance(rareLot);

        var withRole = new PerformanceStat { FromRole = "lens", Min = 0, Max = 1, QualityExponent = 1 };
        var noRole = new PerformanceStat { Min = 0, Max = 1, QualityExponent = 1 };
        Assert.Equal(.9f, items.Evaluate(withRole, instance), 3);
        Assert.Equal(.2f, items.Evaluate(noRole, instance), 3);
        Assert.Equal(.2f, items.GetTier(instance).tier.Quality, 3);
        Assert.Equal(.5f, items.GetTier(rareInstance).tier.Quality, 3);

        // Minting a second instance from an existing lot must not re-roll the lot's quality.
        var secondInstance = (EquippableItem) items.CreateInstance(commonLot);
        Assert.Equal(.2f, items.Lots[commonLot].Quality, 3);
        Assert.Equal(.2f, items.GetTier(secondInstance).tier.Quality, 3);
    }

    // F4: GetPrice reads the lot's quality through GameplaySettings.QualityPriceModifier.
    [Fact]
    public void GetPriceReadsLotQuality()
    {
        using var cache = Open();
        var settings = RunSaveTests.TestSettings();
        settings.QualityPriceModifier = new ExponentialLerp { Minimum = 0, Maximum = 1, Exponent = 1 };
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
        var lamp = cache.GetByName<GearData>("Lamp");
        var lowLot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(lamp), Origin = new Attributed(), Quality = .2f, Roles = new List<RoleFill>() });
        var highLot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(lamp), Origin = new Attributed(), Quality = .8f, Roles = new List<RoleFill>() });
        var low = (EquippableItem) items.CreateInstance(lowLot);
        var high = (EquippableItem) items.CreateInstance(highLot);
        Assert.True(items.GetPrice(high) > items.GetPrice(low));
    }

    // F4: the durability exponent path in ItemManager.Evaluate reads the lot's quality (GameplaySettings.
    // DurabilityQuality{Min,Max,Exponent}), independently of the quality-for-role term (zeroed here via
    // QualityExponent 0).
    [Fact]
    public void EvaluateDurabilityExponentReadsLotQuality()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var durable = cache.Upsert(new GearData { Name = "Durable", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10, Durability = 100 });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var lowLot = items.Lots.Add(new Lot { Design = new CultRecordRef<ItemData>(durable.Key), Origin = new Attributed(), Quality = .1f, Roles = new List<RoleFill>() });
        var highLot = items.Lots.Add(new Lot { Design = new CultRecordRef<ItemData>(durable.Key), Origin = new Attributed(), Quality = .9f, Roles = new List<RoleFill>() });
        var low = (EquippableItem) items.CreateInstance(lowLot);
        var high = (EquippableItem) items.CreateInstance(highLot);
        low.Durability = items.GetData(low).Durability / 2;
        high.Durability = items.GetData(high).Durability / 2;

        var stat = new PerformanceStat { Min = 0, Max = 1, QualityExponent = 0, DurabilityExponentMultiplier = 1 };
        Assert.NotEqual(items.Evaluate(stat, low), items.Evaluate(stat, high));
    }

    // F4: EquippedItem's thermal exponent (Entity.cs's EquippedItem constructor) reads the lot's quality
    // (GameplaySettings.ThermalQuality{Min,Max,Exponent}), through a real equip on a live entity.
    [Fact]
    public void EquippedItemThermalExponentReadsLotQuality()
    {
        using var cache = Open();
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        var hullData = cache.GetByName<HullData>("Skiff");
        var lampData = cache.GetByName<GearData>("Lamp");

        // TestSettings has one tier, so materializing through a Loadout always rolls the same quality; mint the
        // gear's lot directly at a chosen quality and equip it onto a bare ship instead.
        Ship BuildWithGearQuality(float quality)
        {
            var hullItem = (EquippableItem) items.CreateInstance(items.CreateLot(hullData, maker, .5f));
            var ship = new Ship(items, null, hullItem, new EntitySettings());
            var gearItem = (EquippableItem) items.CreateInstance(items.CreateLot(lampData, maker, quality));
            Assert.True(ship.TryEquip(gearItem, HardpointCell));
            return ship;
        }

        var lowEquipped = BuildWithGearQuality(.1f).Equipment.Single(e => e.Data is GearData);
        var highEquipped = BuildWithGearQuality(.9f).Equipment.Single(e => e.Data is GearData);
        Assert.NotEqual(lowEquipped.ThermalExponent, highEquipped.ThermalExponent);
    }

    // F4: CreateLot(product) fills each of the design's roles from the product's own per-role spread
    // (FactionProductData.Roles), not the design-wide default.
    [Fact]
    public void CreateLotFillsRolesFromProductSpread()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        var lensGear = cache.Upsert(new GearData
        {
            Name = "Lensed", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Roles = { new ItemRole { Name = "lens" } }
        });
        cache.Upsert(new FactionProductData
        {
            Name = "Lensed by Maker", Design = new CultRecordRef<CraftedItemData>(lensGear.Key), Manufacturer = maker,
            Roles = { new ProductRole { Role = "lens", Mean = .9f, StandardDeviation = 0 } }
        });
        cache.FlushAsync().Wait();

        var product = cache.GetAll<FactionProductData>().Single(p => p.Name == "Lensed by Maker");
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var lotId = items.CreateLot(product);
        var roleFill = items.Lots[lotId].Roles.Single(r => r.Role == "lens");
        Assert.Equal(.9f, roleFill.Quality, 3);
    }

    // F4: Brand reads the maker faction off a Produced origin, not only Attributed.
    [Fact]
    public void BrandReadsProducedFaction()
    {
        using var cache = Open();
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var lamp = cache.GetByName<GearData>("Lamp");
        var lotId = items.Lots.Add(new Lot
        {
            Design = cache.RefOf<ItemData>(lamp),
            Origin = new Produced { Faction = maker, Station = 1, Facility = 2, Inputs = Array.Empty<int>() },
            Quality = .5f, Roles = new List<RoleFill>()
        });
        var instance = (EquippableItem) items.CreateInstance(lotId);
        var (brandMaker, _) = items.Brand(instance);
        Assert.NotNull(brandMaker);
        Assert.Equal(maker.Key, cache.RefOf(brandMaker).Key);
    }

    [Fact]
    public void OutOfRangeWeaponGroupIndexBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var lampGrouped = WithDesign(Armed(cache), 0, cache, "Lamp");
        var failures = new List<string>();
        Assert.Null(Build(items, lampGrouped, failures));
        Assert.Equal("weapon group: slot 0,0: Lamp is not a weapon", Assert.Single(failures));
    }

    [Fact]
    public void MoreWeaponGroupsThanTheGameHasBuildsNothing()
    {
        using var cache = Open();
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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
        var items = new ItemManager(cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
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
