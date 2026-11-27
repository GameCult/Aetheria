using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Ruling aetheria-living-world:ruling:compound-commodities-stack-by-lot: units of one lot of a compound commodity stack up to
// the design's MaxStack, two lots never merge, a split carries its lot, and a save from before stacks reads one unit. One bay
// rule serves every stack (ICommodityStack.StacksWith), so these tests go through the bay's public entries only. The fixture
// is StationServicesTests's: a skiff, a station, a crate, plus one compound design (MaxStack 4) and two lots of it.
public sealed class CompoundStackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-compoundstacks-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly Zone _zone;
    private readonly CompoundCommodityData _gem;
    private readonly SimpleCommodityData _ore;
    private readonly int _lotA;
    private readonly int _lotB;
    private const float Dt = .1f;

    public CompoundStackTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true);
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        _cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = shape, Price = 1000, Durability = 10, Armor = 10 });
        _cache.Commit(batch => batch.Upsert(typeof(StationKindData), new StationKindData { Name = "Trade Hub" }, StationKindData.KeyOf("trade-hub")));
        _cache.Upsert(new HullData { Name = "Hub", HullType = HullType.Station, Kind = TestStationKinds.TradeHub, Shape = shape, Price = 5000, Durability = 10 });
        _cache.Upsert(new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 400, Durability = 8 });
        var bay = new Shape(4, 4);
        foreach (var cell in bay.AllCoordinates) bay[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = bay, Price = 20, Durability = 4 });
        var pocket = new Shape(1, 1);
        foreach (var cell in pocket.AllCoordinates) pocket[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Pocket", Shape = new Shape(), InteriorShape = pocket, Price = 5, Durability = 4 });
        _ore = new SimpleCommodityData { Name = "Ore", Shape = new Shape(), Price = 7, MaxStack = 10 };
        _cache.Upsert(_ore);
        _gem = new CompoundCommodityData { Name = "Gem", Shape = new Shape(), Price = 10, Mass = 2f, SpecificHeat = 3f, MaxStack = 4 };
        _cache.Upsert(_gem);
        _cache.FlushAsync().Wait();

        var settings = AuthoredSettings.Load(RestoredHullsTests.FindRepoRoot()).Read<GameplaySettings>("GameplaySettings");
        _items = new ItemManager(1u, _cache, new ProvenanceLedger(), settings, _ => { });
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        _lotA = _items.CreateLot(_gem, default, .9f);
        _lotB = _items.CreateLot(_gem, default, .6f);
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private EquippableItem Mint(string design)
    {
        var data = _cache.GetByName<EquippableItemData>(design);
        return new EquippableItem
        {
            Data = _cache.RefOf<ItemData>(data), Durability = data.Durability,
            Lot = _items.Lots.Add(new Lot { Design = _cache.RefOf<ItemData>(data), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        };
    }

    // Units of a lot as ItemManager mints them (one), then set to the stack the test wants.
    private CompoundCommodity Gem(int lot, int quantity)
    {
        var stack = Assert.IsType<CompoundCommodity>(_items.CreateInstance(lot));
        stack.Quantity = quantity;
        return stack;
    }

    private SimpleCommodity Ore(int quantity) =>
        new SimpleCommodity { Data = _cache.RefOf<ItemData>(_ore), Quantity = quantity };

    private Ship ShipWithBays(params string[] bays)
    {
        var ship = new Ship(_items, _zone, Mint("Skiff"), new EntitySettings());
        for (var i = 0; i < bays.Length; i++)
            ship.CargoBays.Add(new EquippedCargoBay(_items, Mint(bays[i]), new int2(1 + i, 1), ship, $"bay{i}"));
        return ship;
    }

    // A ship whose bay is equipped the way a save knows it.
    private Ship ShipWith(string bay)
    {
        var ship = new Ship(_items, _zone, Mint("Skiff"), new EntitySettings());
        Assert.True(ship.TryEquip(Mint(bay), new int2(1, 1)));
        return ship;
    }

    private OrbitalEntity Station()
    {
        var station = new OrbitalEntity(_items, _zone, Mint("Hub"), default, new EntitySettings());
        Assert.True(station.TryEquip(Mint("Crate"), new int2(1, 1)));
        return station;
    }

    private static CompoundCommodity[] Stacks(EquippedCargoBay bay) =>
        bay.Cargo.Keys.OfType<CompoundCommodity>().OrderBy(s => bay.Cargo[s].y).ThenBy(s => bay.Cargo[s].x).ToArray();

    private float Weight(EquippedCargoBay bay) => bay.Data.Mass + bay.Cargo.Keys.Sum(item => _items.GetMass(item));

    private float ThermalWeight(EquippedCargoBay bay) => bay.Data.Mass * bay.Data.SpecificHeat + bay.Cargo.Keys.Sum(item => _items.GetThermalMass(item));

    [Fact]
    public void UnitsOfOneLotMergeUpToMaxStack()
    {
        var bay = ShipWithBays("Crate").CargoBays[0];
        Assert.True(bay.TryStore(Gem(_lotA, 3)));
        Assert.True(bay.TryStore(Gem(_lotA, 2)));
        Assert.Equal(new[] { 1, 4 }, Stacks(bay).Select(s => s.Quantity).OrderBy(q => q));
        Assert.Equal(2, Stacks(bay).Length);
        Assert.All(Stacks(bay), s => Assert.Equal(_lotA, s.Lot));
        Assert.Equal(Weight(bay), bay.Mass, 3);
    }

    [Fact]
    public void TwoLotsOfOneDesignNeverMerge()
    {
        var pocket = ShipWithBays("Pocket").CargoBays[0];
        var a = Gem(_lotA, 2);
        Assert.True(pocket.TryStore(a));
        var b = Gem(_lotB, 1);
        Assert.False(pocket.TryFindSpace(b));
        Assert.False(pocket.TryStore(b));
        Assert.False(pocket.TryStore(b, new int2(0, 0)));
        Assert.Equal(2, a.Quantity);
        Assert.Equal(1, b.Quantity);

        var crate = ShipWithBays("Crate").CargoBays[0];
        Assert.True(crate.TryStore(Gem(_lotA, 2)));
        Assert.True(crate.TryStore(Gem(_lotB, 2)));
        Assert.Equal(new[] { _lotA, _lotB }, Stacks(crate).Select(s => s.Lot).OrderBy(l => l));
    }

    [Fact]
    public void ACompoundStackNeverMergesWithAnotherKind()
    {
        var pocket = ShipWithBays("Pocket").CargoBays[0];
        Assert.True(pocket.TryStore(Ore(3)));
        var gem = Gem(_lotA, 1);
        Assert.False(pocket.TryFindSpace(gem));
        Assert.False(pocket.TryStore(gem, new int2(0, 0)));

        var other = ShipWithBays("Pocket").CargoBays[0];
        Assert.True(other.TryStore(gem));
        Assert.False(other.TryFindSpace(Ore(1)));
        Assert.False(other.TryStore(Ore(1), new int2(0, 0)));
        Assert.False(other.TryStore(Mint("Gun"), new int2(0, 0)));
        Assert.False(gem.StacksWith(Mint("Gun")));
        Assert.False(gem.StacksWith(Ore(1)));
        Assert.False(Ore(1).StacksWith(gem));
        Assert.Equal(1, gem.Quantity);
    }

    [Fact]
    public void ASplitKeepsItsLotAndConservesMass()
    {
        var from = ShipWithBays("Crate").CargoBays[0];
        var to = ShipWithBays("Crate").CargoBays[0];
        var stack = Gem(_lotA, 4);
        Assert.True(from.TryStore(stack));
        var total = from.Mass + to.Mass;

        var part = Assert.IsType<CompoundCommodity>(from.Remove(stack, 2));
        Assert.Equal((_lotA, 2, 2), (part.Lot, part.Quantity, stack.Quantity));
        Assert.Equal(stack.Data.Key, part.Data.Key);
        Assert.Equal(Weight(from), from.Mass, 3);
        Assert.Equal(ThermalWeight(from), from.ThermalMass, 3);

        Assert.True(to.TryStore(part));
        Assert.Equal(Weight(to), to.Mass, 3);
        Assert.Equal(total, from.Mass + to.Mass, 3);
        Assert.Equal(4, from.Count(stack.Data.Key) + to.Count(stack.Data.Key));
    }

    private (Ship giver, Ship receiver) Alongside()
    {
        var giver = ShipWith("Crate");
        var receiver = ShipWith("Crate");
        giver.Position = new float3(-50, 0, 10);
        receiver.Position = giver.Position + new float3(5, 0, 0);
        _zone.Admit(giver, piloted: false);
        _zone.Admit(receiver, piloted: false);
        return (giver, receiver);
    }

    [Fact]
    public void AGiveMovesUnitsOfTheOrderedLotOnly()
    {
        var (giver, receiver) = Alongside();
        var a = Gem(_lotA, 3);
        var b = Gem(_lotB, 3);
        Assert.True(giver.CargoBays[0].TryStore(b));
        Assert.True(giver.CargoBays[0].TryStore(a));
        giver.Command.Give(receiver, Gem(_lotA, 1), 3);

        for (var i = 0; i < 40; i++) _zone.Update(Dt);

        var held = receiver.CargoBays[0];
        Assert.Equal(3, held.Cargo.Keys.OfType<CompoundCommodity>().Where(s => s.Lot == _lotA).Sum(s => s.Quantity));
        Assert.DoesNotContain(held.Cargo.Keys.OfType<CompoundCommodity>(), s => s.Lot == _lotB);
        Assert.Equal(3, giver.CargoBays[0].Cargo.Keys.OfType<CompoundCommodity>().Where(s => s.Lot == _lotB).Sum(s => s.Quantity));
        Assert.DoesNotContain(giver.CargoBays[0].Cargo.Keys.OfType<CompoundCommodity>(), s => s.Lot == _lotA);
    }

    [Fact]
    public void AStackIsPricedPerUnitAndSellsOnlyWhatTheStationCanHold()
    {
        var unit = Gem(_lotA, 1);
        var unitPrice = _items.GetPrice(unit);
        Assert.True(unitPrice > 0);
        Assert.Equal(unitPrice * 3, StationServices.BuyPrice(_items, unit, 3));
        var fraction = (double) _items.GameplaySettings.SellFraction;
        int Pays(int units) => (int) Math.Floor(unitPrice * units * fraction);
        Assert.Equal(Pays(3), StationServices.SellPrice(_items, Gem(_lotA, 3)));
        Assert.True(Pays(3) > 0);

        // Empty bay: the whole stack goes and is paid in full.
        var from = ShipWith("Crate").CargoBays[0];
        var stack = Gem(_lotA, 3);
        Assert.True(from.TryStore(stack));
        var station = Station();
        var credits = 100;
        Assert.Equal(3, StationServices.TrySellPart(from, station, stack, ref credits));
        Assert.Equal(100 + Pays(3), credits);
        Assert.Empty(from.Cargo);
        Assert.Equal(3, station.CargoBays[0].Count(_cache.RefOf<ItemData>(_gem).Key));

        // Part-full bay: the station's stack of the lot has room for one unit and every other cell holds a gun, so one unit
        // sells, is paid for alone, and two stay in the seller's hold.
        var second = Gem(_lotA, 3);
        Assert.True(from.TryStore(second));
        var hold = station.CargoBays[0];
        while (hold.TryStore(Mint("Gun"))) { }
        credits = 0;
        Assert.Equal(1, StationServices.TrySellPart(from, station, second, ref credits));
        Assert.Equal(Pays(1), credits);
        Assert.Equal(2, second.Quantity);
        Assert.Contains(second, from.Cargo.Keys);
        Assert.Equal(4, hold.Count(_cache.RefOf<ItemData>(_gem).Key));
        Assert.Equal(Weight(hold), hold.Mass, 3);
        Assert.Equal(Weight(from), from.Mass, 3);

        // Full bay: nothing moves and nothing is paid.
        credits = 0;
        Assert.Equal(0, StationServices.TrySellPart(from, station, second, ref credits));
        Assert.False(StationServices.TrySell(from, station, second, ref credits));
        Assert.Equal(0, credits);
        Assert.Equal(2, second.Quantity);
        Assert.Equal(4, hold.Count(_cache.RefOf<ItemData>(_gem).Key));
    }

    private byte[] PreCutUnit(int lot, MessagePackSerializerOptions options)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write(1); // Union(1, CompoundCommodity)
        writer.WriteArrayHeader(12);
        MessagePackSerializer.Serialize(ref writer, new CultRecordRef<ItemData>(_cache.RefOf(_gem).Key), options);
        MessagePackSerializer.Serialize(ref writer, ItemRotation.None, options);
        for (var slot = 2; slot < 11; slot++) writer.WriteNil();
        writer.Write(lot); // slot 11: Lot
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    // The hull's lot and the ship pack a one-ship save is made of, as FungibleCommodityTests builds them.
    private int _hullLot;

    // The fixture holds the catalog open, so a save reads its own copy of it.
    private CultCache Open(string tag)
    {
        var catalog = Path.Combine(_root, $"catalog-{tag}.cc");
        if (!File.Exists(catalog)) File.Copy(Path.Combine(_root, "Aetheria.cc"), catalog);
        return AetheriaStores.Open(catalog, Path.Combine(_root, $"run-{tag}.cc"), Path.Combine(_root, $"player-{tag}.cc"));
    }

    private ProvenanceLedger SaveAndReload(ProvenanceLedger live, string tag, params ItemInstance[] held)
    {
        _hullLot = live.Add(new Lot { Design = default, Origin = new Attributed(), Quality = 1f, Roles = new List<RoleFill>() });
        using (var cache = Open(tag))
        {
            var game = new SavedGame
            {
                InfluenceScale = 1,
                Factions = new CultRecordRef<Faction>[0], Relationships = new FactionRelationship[0],
                HomeZones = new Dictionary<int, int>(), BossZones = new Dictionary<int, int>(), DiscoveredZones = new[] { 0 },
                ActionBarBindings = new SavedActionBarBinding[0], Exit = -1
            };
            var pack = new ShipPack
            {
                Name = "ship", Hull = new EquippableItem { Lot = _hullLot },
                Equipment = Array.Empty<(int2, EquippableItem)>(), CargoBays = Array.Empty<(int2, EquippableItem)>(),
                DockingBays = Array.Empty<(int2, EquippableItem)>(),
                CargoContents = new[] { held.Select((s, i) => (new int2(i, 0), s)).ToArray() },
                DockingBayContents = Array.Empty<(int2, ItemInstance)[]>(), Children = Array.Empty<EntityPack>(),
                Temperature = new float[0, 0], Armor = new float[0, 0], Conductivity = new bool2[0, 0],
                DockingBayAssignments = Array.Empty<int>(), Settings = new EntitySettings(), WeaponGroups = Array.Empty<int[]>()
            };
            var zone = new SavedZone
            {
                Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                Contents = new ZonePack { Entities = new List<EntityPack> { pack } }
            };
            RunSave.Commit(cache, game, new[] { zone }, live);
        }
        using (var cache = Open(tag)) return RunSave.Lots(cache);
    }

    [Fact]
    public void APreCutCompoundUnitReadsAsOneAndSavesItsLot()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemInstance).Assembly);
        var old = Assert.IsType<CompoundCommodity>(MessagePackSerializer.Deserialize<ItemInstance>(PreCutUnit(_lotA, options), options));
        Assert.Equal((_lotA, 1), (old.Lot, old.Quantity));

        var bay = ShipWithBays("Pocket").CargoBays[0];
        Assert.True(bay.TryStore(old));
        Assert.True(bay.TryStore(Gem(_lotA, 1)));
        Assert.Equal(2, Assert.Single(Stacks(bay)).Quantity);

        // A new stack keeps its quantity and lot across the wire.
        var fresh = Gem(_lotB, 3);
        var again = Assert.IsType<CompoundCommodity>(MessagePackSerializer.Deserialize<ItemInstance>(MessagePackSerializer.Serialize<ItemInstance>(fresh, options), options));
        Assert.Equal((_lotB, 3), (again.Lot, again.Quantity));

        // The run save roots the lot of a stack once.
        var saved = SaveAndReload(_items.Lots, "precut", old);
        Assert.Contains(_lotA, saved.Lots.Keys);
        Assert.DoesNotContain(_lotB, saved.Lots.Keys);
    }

    [Fact]
    public void RestoreSplitsAnOversizeCompoundStackKeepingItsLot()
    {
        var ship = ShipWith("Crate");
        Assert.True(ship.CargoBays[0].TryStore(Gem(_lotA, 4)));
        var pack = EntitySerializer.Pack(ship);
        _gem.MaxStack = 3;
        var restored = EntitySerializer.Unpack(_items, _zone, pack);

        var stacks = restored.CargoBays.SelectMany(Stacks).ToArray();
        Assert.Equal(4, stacks.Sum(s => s.Quantity));
        Assert.True(stacks.Length >= 2);
        Assert.All(stacks, s => Assert.Equal(_lotA, s.Lot));
        Assert.All(stacks, s => Assert.True(s.Quantity <= 3));
    }

    [Fact]
    public void RestoreConsolidatesFragments()
    {
        var ship = ShipWith("Crate");
        var bay = ship.CargoBays[0];
        // Fragments of one design at four cells: the bay merges nothing at an empty cell, as a pre-merge save held them.
        var cell = 0;
        foreach (var quantity in new[] { 4, 4, 4, 5 }) Assert.True(bay.TryStore(Ore(quantity), new int2(cell++, 0)));
        // Two pre-cut units of lot A, and one of lot B.
        Assert.True(bay.TryStore(Gem(_lotA, 1), new int2(0, 1)));
        Assert.True(bay.TryStore(Gem(_lotA, 1), new int2(1, 1)));
        Assert.True(bay.TryStore(Gem(_lotB, 1), new int2(2, 1)));
        var mass = bay.Mass;
        var thermal = bay.ThermalMass;

        var restored = EntitySerializer.Unpack(_items, _zone, EntitySerializer.Pack(ship)).CargoBays[0];

        Assert.Equal(new[] { 7, 10 }, restored.Cargo.Keys.OfType<SimpleCommodity>().Select(s => s.Quantity).OrderBy(q => q));
        var gems = restored.Cargo.Keys.OfType<CompoundCommodity>().OrderBy(s => s.Lot).ToArray();
        Assert.Equal(new[] { (_lotA, 2), (_lotB, 1) }, gems.Select(s => (s.Lot, s.Quantity)));
        Assert.Equal(mass, restored.Mass, 3);
        Assert.Equal(thermal, restored.ThermalMass, 3);
        Assert.Equal(Weight(restored), restored.Mass, 3);
    }

    [Fact]
    public void CountCountsUnits()
    {
        var ship = ShipWithBays("Crate", "Pocket");
        Assert.True(ship.CargoBays[0].TryStore(Gem(_lotA, 3)));
        Assert.True(ship.CargoBays[0].TryStore(Gem(_lotB, 2)));
        Assert.True(ship.CargoBays[1].TryStore(Gem(_lotA, 1)));
        var gun = Mint("Gun");
        Assert.True(ship.CargoBays[0].TryStore(gun));
        var gem = _cache.RefOf<ItemData>(_gem).Key;

        Assert.Equal(5, ship.CargoBays[0].Count(gem));
        Assert.Equal(1, ship.CargoBays[1].Count(gem));
        Assert.Equal(6, ship.CountItemsInCargo(gem));
        Assert.Equal(1, ship.CountItemsInCargo(gun.Data.Key));
        Assert.Equal(0, ship.CountItemsInCargo(_cache.RefOf<ItemData>(_ore).Key));
    }
}
