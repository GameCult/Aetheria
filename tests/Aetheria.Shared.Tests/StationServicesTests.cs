using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// What a station pays and charges, and the commits that move goods and credits. The price rules are pinned against the
// authored gameplay settings, so the defaults a Settings.asset without the new fields keeps are the ones tested.
public sealed class StationServicesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stationservices-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly Zone _zone;

    public StationServicesTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true);
        _cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hardpoint = new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() };
        _cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = shape, Price = 1000, Durability = 10, Armor = 10, Hardpoints = { hardpoint } });
        _cache.Upsert(new HullData { Name = "Hub", HullType = HullType.Station, Shape = shape, Price = 5000, Durability = 10 });
        _cache.Upsert(new HullData { Name = "Bare Hub", HullType = HullType.Station, Shape = shape, Price = 5000, Durability = 10 });
        _cache.Upsert(new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 400, Durability = 8 });
        _cache.Upsert(new GearData { Name = "Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 90, Durability = 0 });
        var bay = new Shape(4, 4);
        foreach (var cell in bay.AllCoordinates) bay[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = bay, Price = 20, Durability = 4 });
        _cache.Upsert(new DockingBayData { Name = "Berth", Shape = new Shape(), InteriorShape = bay, MaxSize = new int2(2, 2), Price = 60, Durability = 6 });
        _cache.Upsert(new SimpleCommodityData { Name = "Ore", Shape = new Shape(), Price = 7, MaxStack = 100 });
        _cache.FlushAsync().Wait();

        var settings = AuthoredSettings.Load(RestoredHullsTests.FindRepoRoot()).Read<GameplaySettings>("GameplaySettings");
        _items = new ItemManager(_cache, new ProvenanceLedger(), settings, _ => { });
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private EquippableItem Mint(string design, float? durability = null)
    {
        var data = _cache.GetByName<EquippableItemData>(design);
        return new EquippableItem
        {
            Data = _cache.RefOf<ItemData>(data),
            Durability = durability ?? data.Durability,
            Lot = _items.Lots.Add(new Lot { Design = _cache.RefOf<ItemData>(data), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        };
    }

    private SimpleCommodity Ore(int quantity) => new SimpleCommodity { Data = _cache.RefOf<ItemData>(_cache.GetByName<SimpleCommodityData>("Ore")), Quantity = quantity };

    // A ship with a cargo bay.
    private Ship Ship(float? hullDurability = null)
    {
        var ship = new Ship(_items, _zone, Mint("Skiff", hullDurability), new EntitySettings());
        Assert.True(ship.TryEquip(Mint("Crate"), new int2(1, 1)));
        return ship;
    }

    // A station whose one cargo bay can take the goods, or one with no cargo bay at all.
    private OrbitalEntity Station(bool withBay = true)
    {
        var station = new OrbitalEntity(_items, _zone, Mint(withBay ? "Hub" : "Bare Hub"), default, new EntitySettings());
        if (withBay) Assert.True(station.TryEquip(Mint("Crate"), new int2(1, 1)));
        return station;
    }

    [Fact]
    public void SettingsDefaultsAreHalfAndAQuarter()
    {
        Assert.Equal(.5f, _items.GameplaySettings.SellFraction);
        Assert.Equal(.25f, _items.GameplaySettings.RepairFraction);
        Assert.Equal(15000000, _items.GameplaySettings.StartingCredits);
    }

    [Fact]
    public void BuyPrice_is_the_price_and_the_sale_never_beats_it()
    {
        Assert.Equal(_items.GetPrice(Mint("Gun")), StationServices.BuyPrice(_items, Mint("Gun")));
        // A worn gun costs what a new one does to buy, and pays less to sell.
        Assert.Equal(StationServices.BuyPrice(_items, Mint("Gun")), StationServices.BuyPrice(_items, Mint("Gun", 4)));
        // A commodity is priced per unit, times the units asked for.
        Assert.Equal(7, StationServices.BuyPrice(_items, Ore(10)));
        Assert.Equal(70, StationServices.BuyPrice(_items, Ore(1), 10));
        Assert.Equal(int.MaxValue, StationServices.BuyPrice(_items, Ore(1), int.MaxValue));
        Assert.True(StationServices.SellPrice(_items, Mint("Gun")) < StationServices.BuyPrice(_items, Mint("Gun")));
        Assert.Throws<ArgumentException>(() => StationServices.BuyPrice(_items, null));
    }

    [Fact]
    public void SellPrice_scales_with_condition()
    {
        var price = _items.GetPrice(Mint("Gun"));
        Assert.True(price > 100, "the fixture's gun must have a price large enough to show a half");
        Assert.Equal((int) Math.Floor(price * .5), StationServices.SellPrice(_items, Mint("Gun")));
        Assert.Equal((int) Math.Floor(price * .25), StationServices.SellPrice(_items, Mint("Gun", 4)));
        Assert.Equal(0, StationServices.SellPrice(_items, Mint("Gun", 0)));

        // A design with no durability does not wear, so it sells as new however its instance reads.
        Assert.Equal((int) Math.Floor(_items.GetPrice(Mint("Lamp")) * .5), StationServices.SellPrice(_items, Mint("Lamp", 0)));
        // Past design durability is no better than new.
        Assert.Equal(StationServices.SellPrice(_items, Mint("Gun")), StationServices.SellPrice(_items, Mint("Gun", 80)));
        // A commodity has no wear: its price per unit times the quantity, at the fraction, rounded down.
        Assert.Equal(35, StationServices.SellPrice(_items, Ore(10)));
        Assert.Equal(3, StationServices.SellPrice(_items, Ore(1)));
        // A stack large enough that a scaled price shows: 7 * 100 * .5 is exactly 350, and a 2% larger fraction pays 357.
        Assert.Equal(350, StationServices.SellPrice(_items, Ore(100)));
    }

    [Fact]
    public void TrySell_moves_and_pays_or_refuses()
    {
        var ship = Ship();
        var from = ship.CargoBays.Single();
        var gun = Mint("Gun", 4);
        var ore = Ore(10);
        Assert.True(from.TryStore(gun));
        Assert.True(from.TryStore(ore));
        var station = Station();
        var credits = 1000;

        Assert.True(StationServices.TrySell(from, station, gun, ref credits));
        Assert.Equal(1000 + StationServices.SellPrice(_items, Mint("Gun", 4)), credits);
        Assert.DoesNotContain(gun, from.Cargo.Keys);
        Assert.Contains(gun, station.CargoBays.Single().Cargo.Keys);

        credits = 1000;
        Assert.True(StationServices.TrySell(from, station, ore, ref credits));
        Assert.Equal(1035, credits);
        Assert.Empty(from.Cargo.Keys.OfType<SimpleCommodity>());
        Assert.Equal(10, station.CargoBays.Single().Cargo.Keys.OfType<SimpleCommodity>().Sum(c => c.Quantity));

        // A station with nowhere to put it takes nothing and pays nothing.
        var other = Mint("Gun");
        Assert.True(from.TryStore(other));
        var packed = from.Cargo.Count;
        credits = 1000;
        Assert.False(StationServices.TrySell(from, Station(withBay: false), other, ref credits));
        Assert.Equal(1000, credits);
        Assert.Equal(packed, from.Cargo.Count);
        Assert.Contains(other, from.Cargo.Keys);
    }

    // A station's first bay being full is no reason to refuse: the goods go to whichever bay takes them.
    [Fact]
    public void TrySell_uses_any_station_bay_that_takes_the_goods()
    {
        var ship = Ship();
        var from = ship.CargoBays.Single();
        var gun = Mint("Gun");
        var ore = Ore(10);
        Assert.True(from.TryStore(gun));
        Assert.True(from.TryStore(ore));
        var station = Station();
        Assert.True(station.TryEquip(Mint("Crate"), new int2(2, 2)));
        var full = station.CargoBays[0];
        var open = station.CargoBays[1];
        while (full.TryStore(Mint("Gun"))) { }

        var credits = 0;
        Assert.True(StationServices.TrySell(from, station, gun, ref credits));
        Assert.True(StationServices.TrySell(from, station, ore, ref credits));
        Assert.Contains(gun, open.Cargo.Keys);
        Assert.Contains(ore, open.Cargo.Keys);
    }

    [Fact]
    public void Repair_restores_and_charges()
    {
        var whole = Ship();
        Assert.True(whole.TryEquip(Mint("Gun")));
        Assert.Equal(0, StationServices.RepairCost(_items, whole));
        var credits = 50;
        Assert.True(StationServices.TryRepair(whole, ref credits));
        Assert.Equal(50, credits);

        var ship = Ship(hullDurability: 5);
        Assert.True(ship.TryEquip(Mint("Gun", 2)));
        var cost = StationServices.RepairCost(_items, ship);
        var hull = (int) Math.Ceiling(.5 * _items.GetPrice(ship.Hull) * .5);
        var gun = (int) Math.Ceiling(.75 * _items.GetPrice(Mint("Gun")) * .5);
        Assert.Equal(hull + gun, cost);

        var shortOfCost = cost - 1;
        Assert.False(StationServices.TryRepair(ship, ref shortOfCost));
        Assert.Equal(cost - 1, shortOfCost);
        Assert.Equal(5, ship.Hull.Durability);
        Assert.Equal(2, ship.Equipment.Single(e => e.EquippableItem != ship.Hull).EquippableItem.Durability);

        var exact = cost;
        Assert.True(StationServices.TryRepair(ship, ref exact));
        Assert.Equal(0, exact);
        Assert.Equal(10, ship.Hull.Durability);
        Assert.Equal(8, ship.Equipment.Single(e => e.EquippableItem != ship.Hull).EquippableItem.Durability);
        Assert.Equal(0, StationServices.RepairCost(_items, ship));
    }

    [Fact]
    public void Repair_counts_docking_bays_as_gear()
    {
        var ship = Ship();
        Assert.True(ship.TryEquip(Mint("Berth"), new int2(2, 2)));
        var berth = ship.DockingBays.Single().EquippableItem;
        berth.Durability = 3;
        Assert.True(StationServices.RepairCost(_items, ship) > 0, "a worn docking bay costs to mend");
        var credits = 1000000;
        Assert.True(StationServices.TryRepair(ship, ref credits));
        Assert.Equal(6, berth.Durability);
    }

    [Fact]
    public void Repair_counts_cargo_bays_as_gear()
    {
        var ship = Ship();
        ship.CargoBays.Single().EquippableItem.Durability = 1;
        Assert.True(StationServices.RepairCost(_items, ship) > 0, "a worn cargo bay costs to mend");
        var credits = 1000000;
        Assert.True(StationServices.TryRepair(ship, ref credits));
        Assert.Equal(4, ship.CargoBays.Single().EquippableItem.Durability);
    }

    [Fact]
    public void Repair_restores_armour_at_a_price()
    {
        var ship = Ship();
        Assert.Equal(0, StationServices.RepairCost(_items, ship));
        ship.Armor[2, 2] = 0;
        var cells = ship.MaxArmor.Length;
        var share = 10.0 / (10.0 * cells);
        var cost = StationServices.RepairCost(_items, ship);
        Assert.Equal((int) Math.Ceiling(share * _items.GetPrice(ship.Hull) * .5), cost);
        Assert.True(cost > 0, "armour-only wear offers a repair");

        var short1 = cost - 1;
        Assert.False(StationServices.TryRepair(ship, ref short1));
        Assert.Equal(0, ship.Armor[2, 2]);

        var exact = cost;
        Assert.True(StationServices.TryRepair(ship, ref exact));
        Assert.Equal(0, exact);
        Assert.Equal(10, ship.Armor[2, 2]);
        Assert.Equal(0, StationServices.RepairCost(_items, ship));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    public void Repairing_then_selling_never_nets_credits(float repairFraction)
    {
        _items.GameplaySettings.RepairFraction = repairFraction;
        for (var durability = 0; durability < 8; durability++)
        {
            var ship = Ship();
            var gun = Mint("Gun", durability);
            Assert.True(ship.TryEquip(gun));
            var sellWorn = StationServices.SellPrice(_items, gun);
            var cost = StationServices.RepairCost(_items, ship);
            var credits = cost;
            Assert.True(StationServices.TryRepair(ship, ref credits));
            Assert.Equal(0, credits);
            var sellFixed = StationServices.SellPrice(_items, gun);
            Assert.True(sellFixed - cost <= sellWorn, $"durability {durability}: repair {cost} adds {sellFixed - sellWorn}");
        }
    }

    [Fact]
    public void Buy_is_affordable_at_exactly_the_credits_held()
    {
        var gun = Mint("Gun");
        var price = StationServices.BuyPrice(_items, gun);
        var delivered = 0;
        var credits = price - 1;
        Assert.Equal(BuyResult.ShortOfCredits, StationServices.TryBuy(_items, gun, 1, ref credits, () => { delivered++; return true; }));
        Assert.Equal(price - 1, credits);
        Assert.Equal(0, delivered);

        credits = price;
        Assert.Equal(BuyResult.Bought, StationServices.TryBuy(_items, gun, 1, ref credits, () => { delivered++; return true; }));
        Assert.Equal(0, credits);
        Assert.Equal(1, delivered);

        credits = price + 5;
        Assert.Equal(BuyResult.NoRoom, StationServices.TryBuy(_items, gun, 1, ref credits, () => false));
        Assert.Equal(price + 5, credits);

        var ore = Ore(10);
        credits = 70;
        Assert.Equal(BuyResult.Bought, StationServices.TryBuy(_items, ore, 10, ref credits, () => true));
        Assert.Equal(0, credits);
    }
}
