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
        _cache.Upsert(new SimpleCommodityData { Name = "Grit", Shape = new Shape(), Price = 1, MaxStack = 2 });
        _cache.Upsert(new SimpleCommodityData { Name = "Slag", Shape = new Shape(), Price = 1, MaxStack = 100 });
        _cache.Upsert(new DockingBayData { Name = "Berth", Shape = new Shape(), InteriorShape = bay, MaxSize = new int2(2, 2), Price = 60, Durability = 6 });
        _cache.Upsert(new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 400, Durability = 8 });
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

    // ---- The give verb ------------------------------------------------------------------------------------------

    private const float Dt = .1f;

    // Two ships in the zone, alongside each other, the giver holding `ore` units of Ore.
    private (Ship Giver, Ship Receiver, SimpleCommodity Stack) Alongside(int ore, string receiverBay = "Crate")
    {
        var giver = ShipWith("Crate");
        var receiver = ShipWith(receiverBay);
        var stack = Commodity("Ore", ore);
        if (ore > 0) Assert.True(giver.CargoBays[0].TryStore(stack));
        giver.Position = new float3(0, 0, 0);
        receiver.Position = new float3(5, 0, 0);
        _zone.Admit(giver, piloted: false);
        _zone.Admit(receiver, piloted: false);
        return (giver, receiver, stack);
    }

    private static int Held(Entity entity, SimpleCommodity like) => entity.CargoBays.Sum(bay => Units(bay, like));

    private void Steps(int count)
    {
        for (var i = 0; i < count; i++) _zone.Update(Dt);
    }

    [Fact]
    public void GiveMovesAtTheRate()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);

        for (var i = 1; i <= 10; i++)
        {
            Steps(1);
            Assert.Equal(10, Held(giver, ore) + Held(receiver, ore));
        }

        // Rate 2 for one second: exactly two units, not one and not three.
        Assert.Equal(2, Held(receiver, ore));
        Assert.Equal(8, Held(giver, ore));
        Assert.NotNull(giver.Giving);
    }

    [Fact]
    public void GiveEndsDoneAtItsQuantity()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 3);

        Steps(40);

        Assert.Equal(3, Held(receiver, ore));
        Assert.Equal(7, Held(giver, ore));
        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    [Fact]
    public void GiveEndsOutOfRange()
    {
        var (giver, receiver, ore) = Alongside(10);
        var range = _items.GameplaySettings.DockingDistance;
        receiver.Position = new float3(range - 1, 0, 0);
        giver.Command.Give(receiver, ore, 10);
        Steps(10);
        Assert.Equal(2, Held(receiver, ore));
        Assert.Null(giver.LastGiveEnd);

        receiver.Position = new float3(range + 1, 0, 0);
        Steps(10);

        Assert.Equal(2, Held(receiver, ore));
        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.OutOfRange, giver.LastGiveEnd);
    }

    [Fact]
    public void GiveRangeIsMeasuredOnTheFlatPlane()
    {
        // The plane is xz: a ship far apart in y only is still alongside.
        var (giver, receiver, ore) = Alongside(10);
        receiver.Position = new float3(5, 1000, 0);
        giver.Command.Give(receiver, ore, 10);
        Steps(10);
        Assert.Equal(2, Held(receiver, ore));
    }

    [Fact]
    public void GiveEndsWhenTheReceiverLeaves()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);
        Steps(10);
        Assert.Equal(2, Held(receiver, ore));

        _zone.Remove(receiver);
        Steps(10);

        Assert.Equal(8, Held(giver, ore));
        Assert.Equal(2, Held(receiver, ore));
        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.ReceiverGone, giver.LastGiveEnd);
    }

    [Fact]
    public void DockingEndsTheGiversGive()
    {
        var (giver, receiver, ore) = Alongside(10);
        var station = new OrbitalEntity(_items, _zone, Mint("Hub"), default, new EntitySettings());
        Assert.True(station.TryEquip(Mint("Berth"), new int2(1, 1)));
        giver.Command.Give(receiver, ore, 10);
        Steps(10);

        Assert.NotNull(station.TryDock(giver));
        Steps(10);

        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.GiverDocked, giver.LastGiveEnd);
        Assert.Equal(2, Held(receiver, ore));
    }

    [Fact]
    public void GiveEndsOnAFullHold()
    {
        var (giver, receiver, ore) = Alongside(10, "Pocket");
        Assert.True(receiver.CargoBays[0].TryStore(Commodity("Slag", 5)));
        giver.Command.Give(receiver, ore, 10);

        Steps(20);

        Assert.Equal(10, Held(giver, ore));
        Assert.Equal(0, Held(receiver, ore));
        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.ReceiverFull, giver.LastGiveEnd);
    }

    [Fact]
    public void GiveEndsWhenNothingIsLeft()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 50);

        Steps(100);

        Assert.Equal(10, Held(receiver, ore));
        Assert.Equal(0, Held(giver, ore));
        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.NothingLeft, giver.LastGiveEnd);
    }

    [Fact]
    public void AGiveDrawsOnEveryStackOfTheDesign()
    {
        // Grit stacks two to a stack, so three units are two stacks: the order is for the design, not the stack named.
        var (giver, receiver, _) = Alongside(0);
        var grit = Commodity("Grit", 2);
        Assert.True(giver.CargoBays[0].TryStore(grit));
        Assert.True(giver.CargoBays[0].TryStore(Commodity("Grit", 1)));
        Assert.Equal(2, giver.CargoBays[0].ItemsOfType[grit.Data.Key].Count);
        giver.Command.Give(receiver, grit, 3);

        Steps(40);

        Assert.Equal(3, Held(receiver, grit));
        Assert.Equal(0, Held(giver, grit));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    [Fact]
    public void AGivenCraftedItemIsThatInstance()
    {
        var (giver, receiver, _) = Alongside(0);
        var first = Mint("Gun");
        var second = Mint("Gun");
        Assert.True(giver.CargoBays[0].TryStore(first));
        Assert.True(giver.CargoBays[0].TryStore(second));
        giver.Command.Give(receiver, second, 5);

        Steps(20);

        Assert.True(receiver.CargoBays[0].Cargo.ContainsKey(second));
        Assert.False(receiver.CargoBays[0].Cargo.ContainsKey(first));
        Assert.True(giver.CargoBays[0].Cargo.ContainsKey(first));
        Assert.False(giver.CargoBays[0].Cargo.ContainsKey(second));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    [Fact]
    public void TheLastGivePressInAStepWins()
    {
        var (giver, receiver, ore) = Alongside(10);
        var third = ShipWith("Crate");
        third.Position = new float3(0, 0, 5);
        _zone.Admit(third, piloted: false);

        // A give then a stop leaves nothing, and nothing ended.
        giver.Command.Give(receiver, ore, 10);
        giver.Command.StopGiving();
        Steps(1);
        Assert.Null(giver.Giving);
        Assert.Null(giver.LastGiveEnd);

        // A stop then a give leaves the give.
        giver.Command.StopGiving();
        giver.Command.Give(receiver, ore, 10);
        Steps(1);
        Assert.Same(receiver, giver.Giving.Receiver);

        // A second give in the same step replaces the first before it starts.
        giver.Command.Give(receiver, ore, 10);
        giver.Command.Give(third, ore, 10);
        Steps(1);
        Assert.Same(third, giver.Giving.Receiver);
        Assert.Equal(GiveEnd.Stopped, giver.LastGiveEnd);

        // A give in a later step replaces the one under way, which ends Stopped.
        giver.Command.Give(receiver, ore, 10);
        Steps(1);
        Assert.Same(receiver, giver.Giving.Receiver);
    }

    [Fact]
    public void StoppingEndsTheGiveAndKeepsWhatMoved()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);
        Steps(10);
        giver.Command.StopGiving();
        Steps(10);

        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.Stopped, giver.LastGiveEnd);
        Assert.Equal(2, Held(receiver, ore));
        Assert.Equal(8, Held(giver, ore));
    }

    [Fact]
    public void AGiveToOneselfOrOfNothingIsIgnored()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);
        Steps(1);
        var order = giver.Giving;

        giver.Command.Give(giver, ore, 5);
        giver.Command.Give(receiver, ore, 0);
        giver.Command.Give(receiver, ore, -3);
        giver.Command.Give(null, ore, 5);
        giver.Command.Give(receiver, null, 5);
        Steps(1);

        Assert.Same(order, giver.Giving);
        Assert.Null(giver.LastGiveEnd);
    }

    [Fact]
    public void AGiverStaysInTheZone()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Designate(new TargetRef(receiver));
        receiver.Command.Designate(new TargetRef(giver));
        giver.Command.Give(receiver, ore, 10);

        Steps(20);

        Assert.True(_zone.Entities.Contains(giver));
        Assert.True(_zone.Entities.Contains(receiver));
        Assert.Same(receiver, giver.Target.Value.Entity);
        Assert.Same(giver, receiver.Target.Value.Entity);
    }

    [Fact]
    public void NoOrderMovesNothing()
    {
        // No order, no work: a zone of ships that are not giving moves nothing.
        var (giver, receiver, ore) = Alongside(10);
        Steps(20);
        Assert.Equal(10, Held(giver, ore));
        Assert.Equal(0, Held(receiver, ore));
        Assert.Null(giver.LastGiveEnd);
    }
}
