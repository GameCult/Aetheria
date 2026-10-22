using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Goods moving bay to bay between ships and stations: the one conserving commit (EquippedCargoBay.TryTransferItem) that
// the docked trade, the sale and the give all go through. Fixture as StationServicesTests: authored settings, Ore.
public sealed class CargoTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-cargotransfer-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly Zone _zone;

    public CargoTransferTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true);
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        _cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = shape, Price = 1000, Durability = 10, Armor = 10 });
        _cache.Upsert(new HullData { Name = "Hub", HullType = HullType.Station, Shape = shape, Price = 5000, Durability = 10 });
        var bay = new Shape(4, 4);
        foreach (var cell in bay.AllCoordinates) bay[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = bay, Price = 20, Durability = 4 });
        var pocket = new Shape(1, 1);
        foreach (var cell in pocket.AllCoordinates) pocket[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Pocket", Shape = new Shape(), InteriorShape = pocket, Price = 5, Durability = 4 });
        _cache.Upsert(new SimpleCommodityData { Name = "Ore", Shape = new Shape(), Price = 7, MaxStack = 100 });
        _cache.Upsert(new SimpleCommodityData { Name = "Slag", Shape = new Shape(), Price = 1, MaxStack = 100 });
        _cache.FlushAsync().Wait();

        var settings = AuthoredSettings.Load(RestoredHullsTests.FindRepoRoot()).Read<GameplaySettings>("GameplaySettings");
        _items = new ItemManager(1u, _cache, new ProvenanceLedger(), settings, _ => { });
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
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
            Data = _cache.RefOf<ItemData>(data),
            Durability = data.Durability,
            Lot = _items.Lots.Add(new Lot { Design = _cache.RefOf<ItemData>(data), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        };
    }

    private SimpleCommodity Commodity(string design, int quantity) =>
        new SimpleCommodity { Data = _cache.RefOf<ItemData>(_cache.GetByName<SimpleCommodityData>(design)), Quantity = quantity };

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

    // The units of the same design as `like` that the bay holds, over every stack.
    private static int Units(EquippedCargoBay bay, SimpleCommodity like) =>
        bay.Cargo.Keys.OfType<SimpleCommodity>().Where(c => c.Data.Key.Equals(like.Data.Key)).Sum(c => c.Quantity);

    [Fact]
    public void PartialCommodityTransferConserves()
    {
        var from = ShipWith("Crate").CargoBays[0];
        var to = ShipWith("Crate").CargoBays[0];
        var ore = Commodity("Ore", 10);
        Assert.True(from.TryStore(ore));
        var massBefore = from.Mass + to.Mass;

        Assert.True(from.TryTransferItem(to, ore, 4));
        Assert.True(from.TryTransferItem(to, ore, 4));

        Assert.Equal(2, Units(from, ore));
        Assert.Equal(8, Units(to, ore));
        Assert.Empty(from.Cargo.Keys.Intersect(to.Cargo.Keys));
        Assert.Equal(massBefore, from.Mass + to.Mass, 3);
    }

    [Fact]
    public void ABoughtPartOfAStationStackLeavesTheRestWithTheStation()
    {
        // TradeMenu.Buy delivers by this exact commit: the station's stack to the player's bay, a part at a time.
        var station = Station();
        var stationBay = station.CargoBays[0];
        var player = ShipWith("Crate").CargoBays[0];
        var stock = Commodity("Ore", 10);
        Assert.True(stationBay.TryStore(stock));
        var credits = 1000;

        Assert.Equal(BuyResult.Bought, StationServices.TryBuy(_items, stock, 4, ref credits, () => stationBay.TryTransferItem(player, stock, 4)));
        Assert.Equal(BuyResult.Bought, StationServices.TryBuy(_items, stock, 4, ref credits, () => stationBay.TryTransferItem(player, stock, 4)));

        Assert.Equal(1000 - 2 * 4 * 7, credits);
        Assert.Equal(2, Units(stationBay, stock));
        Assert.Equal(8, Units(player, stock));
        Assert.Empty(stationBay.Cargo.Keys.Intersect(player.Cargo.Keys));
    }

    [Fact]
    public void RefusedCommodityTransferChangesNothing()
    {
        var from = ShipWith("Crate").CargoBays[0];
        var to = ShipWith("Pocket").CargoBays[0];
        var ore = Commodity("Ore", 10);
        Assert.True(from.TryStore(ore));
        Assert.True(to.TryStore(Commodity("Slag", 5)));
        var at = from.Cargo[ore];
        var massBefore = from.Mass + to.Mass;

        Assert.False(from.TryTransferItem(to, ore, 4));

        Assert.Equal(10, ore.Quantity);
        Assert.Equal(at, from.Cargo[ore]);
        Assert.Equal(10, Units(from, ore));
        Assert.Equal(0, Units(to, ore));
        Assert.Equal(massBefore, from.Mass + to.Mass, 3);
    }

    [Fact]
    public void ATransferThatNeedsMoreRoomThanTheStackHasLeftChangesNothing()
    {
        // The receiver's only stack of Ore has room for 2 of the 4: the move is whole or nothing.
        var from = ShipWith("Crate").CargoBays[0];
        var to = ShipWith("Pocket").CargoBays[0];
        var ore = Commodity("Ore", 10);
        Assert.True(from.TryStore(ore));
        Assert.True(to.TryStore(Commodity("Ore", 98)));

        Assert.False(from.TryTransferItem(to, ore, 4));

        Assert.Equal(10, Units(from, ore));
        Assert.Equal(98, Units(to, ore));
    }
}
