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
        var slot = new Shape(1, 2);
        foreach (var cell in slot.AllCoordinates) slot[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Slot", Shape = new Shape(), InteriorShape = slot, Price = 5, Durability = 4 });
        var rod = new Shape(2, 1);
        foreach (var cell in rod.AllCoordinates) rod[cell] = true;
        _cache.Upsert(new SimpleCommodityData { Name = "Rod", Shape = rod, Price = 1, MaxStack = 1 });
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

    private SimpleCommodity Commodity(string design, int quantity, ItemRotation rotation = ItemRotation.None) =>
        new SimpleCommodity { Data = _cache.RefOf<ItemData>(_cache.GetByName<SimpleCommodityData>(design)), Quantity = quantity, Rotation = rotation };

    // Bays built directly, so a ship can hold several (TryEquip is not under test).
    private Ship ShipWithBays(params string[] bays)
    {
        var ship = new Ship(_items, _zone, Mint("Skiff"), new EntitySettings());
        for (var i = 0; i < bays.Length; i++)
            ship.CargoBays.Add(new EquippedCargoBay(_items, Mint(bays[i]), new int2(1 + i, 1), ship, $"bay{i}"));
        return ship;
    }

    // Off the origin, so a distance taken from the wrong sum or difference of positions shows.
    private void Place(Ship giver, Ship receiver)
    {
        giver.Position = new float3(-50, 0, 10);
        receiver.Position = giver.Position + new float3(5, 0, 0);
        _zone.Admit(giver, piloted: false);
        _zone.Admit(receiver, piloted: false);
    }

    private static int Stacks(EquippedCargoBay bay) => bay.Cargo.Keys.Count;

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
        // Off the origin, so a distance taken from the wrong sum or difference of positions shows.
        giver.Position = new float3(-50, 0, 10);
        receiver.Position = giver.Position + new float3(5, 0, 0);
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
        receiver.Position = giver.Position + new float3(range - 1, 0, 0);
        giver.Command.Give(receiver, ore, 10);
        Steps(10);
        Assert.Equal(2, Held(receiver, ore));
        Assert.Null(giver.LastGiveEnd);

        receiver.Position = giver.Position + new float3(range + 1, 0, 0);
        Steps(10);

        Assert.Equal(2, Held(receiver, ore));
        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.OutOfRange, giver.LastGiveEnd);
    }

    [Fact]
    public void AReceiverExactlyAtTheDockingDistanceIsStillAlongside()
    {
        var (giver, receiver, ore) = Alongside(10);
        receiver.Position = giver.Position + new float3(0, 0, _items.GameplaySettings.DockingDistance);
        giver.Command.Give(receiver, ore, 10);
        Steps(10);
        Assert.Equal(2, Held(receiver, ore));
        Assert.Null(giver.LastGiveEnd);
    }

    [Fact]
    public void AStepOfExactlyOneUnitMovesOneUnit()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);
        _zone.Update(1f / _items.GameplaySettings.TransferRate);
        Assert.Equal(1, Held(receiver, ore));
    }

    [Fact]
    public void ALongStepNeverMovesMoreThanTheQuantity()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 3);
        _zone.Update(5f);
        Assert.Equal(3, Held(receiver, ore));
        Assert.Equal(7, Held(giver, ore));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    [Fact]
    public void AGiveOfOneMovesOne()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 1);
        Steps(20);
        Assert.Equal(1, Held(receiver, ore));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    [Fact]
    public void AGiveOfACraftedItemTheGiverDoesNotHoldEndsNothingLeft()
    {
        var (giver, receiver, _) = Alongside(0);
        var gun = Mint("Gun");
        Assert.True(receiver.CargoBays[0].TryStore(gun));
        giver.Command.Give(receiver, gun, 1);
        Steps(20);
        Assert.Equal(GiveEnd.NothingLeft, giver.LastGiveEnd);
        Assert.True(receiver.CargoBays[0].Cargo.ContainsKey(gun));
    }

    [Fact]
    public void ALongStepAfterTheReceiverLeavesMovesNothing()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);
        Steps(1);
        _zone.Remove(receiver);

        _zone.Update(5f);

        Assert.Equal(0, Held(receiver, ore));
        Assert.Equal(10, Held(giver, ore));
        Assert.Equal(GiveEnd.ReceiverGone, giver.LastGiveEnd);
    }

    [Fact]
    public void ALongStepOutOfRangeMovesNothing()
    {
        var (giver, receiver, ore) = Alongside(10);
        giver.Command.Give(receiver, ore, 10);
        Steps(1);
        receiver.Position = giver.Position + new float3(0, 0, _items.GameplaySettings.DockingDistance + 1);

        _zone.Update(5f);

        Assert.Equal(0, Held(receiver, ore));
        Assert.Equal(GiveEnd.OutOfRange, giver.LastGiveEnd);
    }

    [Fact]
    public void GiveRangeIsMeasuredOnTheFlatPlane()
    {
        // The plane is xz: a ship far apart in y only is still alongside.
        var (giver, receiver, ore) = Alongside(10);
        receiver.Position = giver.Position + new float3(5, 1000, 0);
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
        third.Position = giver.Position + new float3(0, 0, 5);
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

    // ---- Quantities below one --------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ATransferBelowOneUnitChangesNothing(int quantity)
    {
        var from = ShipWith("Crate").CargoBays[0];
        var to = ShipWith("Crate").CargoBays[0];
        var ore = Commodity("Ore", 10);
        Assert.True(from.TryStore(ore));
        Assert.True(to.TryStore(Commodity("Ore", 3)));
        var mass = (from.Mass, to.Mass);
        var stacks = (Stacks(from), Stacks(to));

        Assert.False(from.TryTransferItem(to, ore, quantity));

        Assert.Equal(10, Units(from, ore));
        Assert.Equal(3, Units(to, ore));
        Assert.Equal(mass, (from.Mass, to.Mass));
        Assert.Equal(stacks, (Stacks(from), Stacks(to)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ARemovalBelowOneUnitTakesNothing(int quantity)
    {
        var bay = ShipWith("Crate").CargoBays[0];
        var ore = Commodity("Ore", 10);
        Assert.True(bay.TryStore(ore));
        var mass = bay.Mass;

        Assert.Null(bay.Remove(ore, quantity));

        Assert.Equal(10, Units(bay, ore));
        Assert.Equal(mass, bay.Mass);
        Assert.Equal(1, Stacks(bay));
    }

    // ---- The give: where units go, and when it ends (the soul-tv probes, adopted) ----------------------------

    // First receiver bay full, second has room: the unit goes to the second.
    [Fact]
    public void AGivenCommodityGoesToTheSecondBayWhenTheFirstIsFull()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Pocket", "Crate");
        var ore = Commodity("Ore", 10);
        Assert.True(giver.CargoBays[0].TryStore(ore));
        Assert.True(receiver.CargoBays[0].TryStore(Commodity("Slag", 5)));
        Place(giver, receiver);
        giver.Command.Give(receiver, ore, 3);

        Steps(40);

        Assert.Equal(3, Units(receiver.CargoBays[1], ore));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    // Two open receiver bays: one unit per move, not one per bay.
    [Fact]
    public void AGiveMovesOneUnitPerMoveWithTwoOpenBays()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Crate", "Crate");
        var ore = Commodity("Ore", 10);
        Assert.True(giver.CargoBays[0].TryStore(ore));
        Place(giver, receiver);
        giver.Command.Give(receiver, ore, 10);

        _zone.Update(1f);

        Assert.Equal(2, Held(receiver, ore));
        Assert.Equal(8, Held(giver, ore));
    }

    [Fact]
    public void AGivenCraftedItemGoesToTheSecondBayWhenTheFirstIsFull()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Pocket", "Crate");
        var gun = Mint("Gun");
        Assert.True(giver.CargoBays[0].TryStore(gun));
        Assert.True(receiver.CargoBays[0].TryStore(Commodity("Slag", 5)));
        Place(giver, receiver);
        giver.Command.Give(receiver, gun, 1);

        Steps(10);

        Assert.True(receiver.CargoBays[1].Cargo.ContainsKey(gun));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    [Fact]
    public void AGivenCraftedItemMovesOnceIntoTwoOpenBays()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Crate", "Crate");
        var gun = Mint("Gun");
        Assert.True(giver.CargoBays[0].TryStore(gun));
        Place(giver, receiver);
        giver.Command.Give(receiver, gun, 1);

        Steps(10);

        Assert.True(receiver.CargoBays[0].Cargo.ContainsKey(gun));
        Assert.Equal(GiveEnd.Done, giver.LastGiveEnd);
    }

    // A refused one-unit move of a whole one-unit stack re-files it at the end of ItemsOfType, so the next Source would be
    // a different stack. Rods are 2x1; the receiver's Slot (1x2) takes only the turned one. After the give has ended
    // ReceiverFull nothing may move.
    [Fact]
    public void NothingMovesAfterAGiveEndsReceiverFull()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Slot");
        var flat = Commodity("Rod", 1);
        Assert.True(giver.CargoBays[0].TryStore(flat));
        Assert.True(giver.CargoBays[0].TryStore(Commodity("Rod", 1, ItemRotation.Clockwise)));
        Place(giver, receiver);
        giver.Command.Give(receiver, flat, 2);

        _zone.Update(5f);

        Assert.Equal(GiveEnd.ReceiverFull, giver.LastGiveEnd);
        Assert.Equal(0, Held(receiver, flat));
        Assert.Equal(2, Held(giver, flat));
    }

    // Refused moves do not spend the order's units.
    [Fact]
    public void AnEndedOrderKeepsItsRemainingUnits()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Pocket");
        var ore = Commodity("Ore", 10);
        Assert.True(giver.CargoBays[0].TryStore(ore));
        Assert.True(receiver.CargoBays[0].TryStore(Commodity("Slag", 5)));
        Place(giver, receiver);
        giver.Command.Give(receiver, ore, 5);
        _zone.Update(.01f); // applies the command, earns no unit
        var order = giver.Giving;
        Assert.NotNull(order);

        _zone.Update(5f);

        Assert.Equal(GiveEnd.ReceiverFull, giver.LastGiveEnd);
        Assert.Equal(5, order.Remaining);
    }

    // A give of more than the hold has moves the real units and then has nothing left.
    [Fact]
    public void AGiveOfMoreThanTheHoldHasEndsNothingLeft()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Crate");
        var grit = Commodity("Grit", 2);
        Assert.True(giver.CargoBays[0].TryStore(grit));
        Place(giver, receiver);
        giver.Command.Give(receiver, grit, 3);

        _zone.Update(5f);

        Assert.Equal(2, Held(receiver, grit));
        Assert.Equal(GiveEnd.NothingLeft, giver.LastGiveEnd);
    }

    // Warping and dying both leave through Zone.Remove; docking has already ended the give GiverDocked.
    [Fact]
    public void AGiveEndsWhenItsGiverLeavesTheZone()
    {
        var giver = ShipWithBays("Crate");
        var receiver = ShipWithBays("Crate");
        var ore = Commodity("Ore", 10);
        Assert.True(giver.CargoBays[0].TryStore(ore));
        Place(giver, receiver);
        giver.Command.Give(receiver, ore, 10);
        _zone.Update(.5f);

        Assert.True(_zone.Remove(giver));

        Assert.Null(giver.Giving);
        Assert.Equal(GiveEnd.GiverGone, giver.LastGiveEnd);
    }

    // ---- The lot a purchase delivers ---------------------------------------------------------------------------

    [Fact]
    public void APurchaseChargesForEachLotItDelivers()
    {
        // Grit stacks hold two. Five units are three lots, and each lot is charged for what it delivers.
        var stock = Commodity("Grit", 5);
        var credits = 100;
        var delivered = new List<int>();

        var result = StationServices.TryBuyLots(_items, stock, 5, ref credits, lot => { delivered.Add(lot); return true; });

        Assert.Equal(BuyResult.Bought, result);
        Assert.Equal(new[] { 2, 2, 1 }, delivered);
        Assert.Equal(100 - 5, credits);
    }

    [Fact]
    public void ARefusedLotLeavesEarlierLotsBoughtAndChargesNothingMore()
    {
        var stock = Commodity("Grit", 5);
        var credits = 100;
        var lots = 0;

        var result = StationServices.TryBuyLots(_items, stock, 5, ref credits, lot => ++lots < 2);

        Assert.Equal(BuyResult.NoRoom, result);
        Assert.Equal(100 - 2, credits);
    }

    [Theory]
    [InlineData(-5, 1000, 1)]
    [InlineData(0, 1000, 1)]
    [InlineData(4, 1000, 4)]
    [InlineData(50, 1000, 10)]
    [InlineData(50, 21, 3)]
    [InlineData(5, 3, 1)]
    public void TheBuyQuantityIsClampedToWhatIsBuyable(int typed, int credits, int expected)
    {
        var stock = Commodity("Ore", 10); // 7 credits each
        Assert.Equal(expected, StationServices.BuyableQuantity(_items, stock, typed, credits));
    }
    // ---- The bay owns the quantity that may sit in it ---------------------------------------------------------

    [Theory]
    [InlineData("Ore", 0)]
    [InlineData("Ore", -3)]
    [InlineData("Ore", int.MinValue)]
    [InlineData("Grit", 3)] // MaxStack 2
    [InlineData("Ore", 101)]
    public void TheBayRefusesAStackBelowOneOrAboveMaxStack(string design, int quantity)
    {
        var bay = ShipWith("Crate").CargoBays[0];
        var massBefore = bay.Mass;

        Assert.False(bay.TryStore(Commodity(design, quantity)));
        Assert.False(bay.TryStore(Commodity(design, quantity), new int2(0, 0)));

        Assert.Empty(bay.Cargo);
        Assert.Equal(massBefore, bay.Mass);
    }

    [Fact]
    public void ANegativeStackNeverMergesIntoAHeldOne()
    {
        var bay = ShipWith("Crate").CargoBays[0];
        var held = Commodity("Ore", 5);
        Assert.True(bay.TryStore(held));
        var at = bay.Cargo[held];

        Assert.False(bay.TryStore(Commodity("Ore", -3), at));

        Assert.Equal(5, held.Quantity);
    }

    [Fact]
    public void AMergeThatOverflowsFillsTheHeldStackToMaxAndLeavesTheRestWithTheCaller()
    {
        var bay = ShipWith("Crate").CargoBays[0];
        var held = Commodity("Grit", 1);
        Assert.True(bay.TryStore(held));
        var incoming = Commodity("Grit", 2);

        Assert.False(bay.TryStore(incoming, bay.Cargo[held]));

        Assert.Equal(2, held.Quantity);
        Assert.Equal(1, incoming.Quantity);
    }

    [Fact]
    public void APurchaseAboveTheStationStockIsRefusedWithoutCharge()
    {
        // Real bays, Grit stacks hold two: a stock of 3 cannot be bought as 5. Before, the second lot took the 1-unit
        // remainder and was charged for 2.
        var station = Station().CargoBays[0];
        var player = ShipWith("Crate").CargoBays[0];
        var stock = Commodity("Grit", 2);
        Assert.True(station.TryStore(stock));
        var second = Commodity("Grit", 1);
        Assert.True(station.TryStore(second, new int2(1, 0)));
        var credits = 100;

        var result = StationServices.TryBuyLots(_items, stock, 5, ref credits, lot => station.TryTransferItem(player, stock, lot));

        Assert.Equal(BuyResult.NoRoom, result);
        Assert.Equal(100, credits);
        Assert.Equal(0, Units(player, stock));
    }

    [Fact]
    public void APurchaseOfAnEmptyStationStackChargesNothingAndDeliversNothing()
    {
        // An old save's zero stack, as the station's stock. The field clamps the typed quantity up to one unit.
        var stock = Commodity("Ore", 0);
        var credits = 100;
        var delivered = 0;

        var result = StationServices.TryBuyLots(_items, stock, StationServices.BuyableQuantity(_items, stock, 1, credits), ref credits,
            lot => { delivered += lot; return true; });

        Assert.Equal(BuyResult.NoRoom, result);
        Assert.Equal(100, credits);
        Assert.Equal(0, delivered);
    }

    [Fact]
    public void ThePurchaseOfTheWholeStockChargesTheUnitsTheBayMoved()
    {
        var station = Station().CargoBays[0];
        var player = ShipWith("Crate").CargoBays[0];
        var stock = Commodity("Grit", 2);
        Assert.True(station.TryStore(stock));
        var more = Commodity("Grit", 1);
        Assert.True(station.TryStore(more, new int2(1, 0)));
        var credits = 100;

        var result = StationServices.TryBuyLots(_items, stock, 2, ref credits, lot => station.TryTransferItem(player, stock, lot));

        Assert.Equal(BuyResult.Bought, result);
        Assert.Equal(98, credits);
        Assert.Equal(2, Units(player, stock));
    }

    [Fact]
    public void ARestoredBayDropsAStackBelowOneAndLogsWhy()
    {
        var log = new List<string>();
        var settings = AuthoredSettings.Load(RestoredHullsTests.FindRepoRoot()).Read<GameplaySettings>("GameplaySettings");
        var items = new ItemManager(1u, _cache, new ProvenanceLedger(), settings, log.Add);
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var ship = ShipWith("Crate");
        Assert.True(ship.CargoBays[0].TryStore(Commodity("Slag", 4)));
        var pack = (ShipPack) EntitySerializer.Pack(ship);
        // The typed -5 purchase of old saves: a negative stack in the hold, beside a good one.
        pack.CargoContents[0] = pack.CargoContents[0].Concat(new[] { (new int2(3, 3), (ItemInstance) Commodity("Ore", -5)) }).ToArray();

        var restored = (Ship) EntitySerializer.Unpack(items, zone, pack);

        var bay = restored.CargoBays[0];
        Assert.Equal(new[] { 4 }, bay.Cargo.Keys.OfType<SimpleCommodity>().Select(c => c.Quantity).ToArray());
        Assert.Contains(log, l => l.Contains("-5") && l.Contains("Ore"));
    }
}
