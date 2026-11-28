using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Installed facilities run their recipes on screen (aetheria-living-world, cut facility-production): one cycle rule,
// Production.Advance; the inputs of a cycle are taken when it starts and a simple output is stored when it ends. The fixture seats
// a shipped facility design by hand on a station of its own, over the booted catalog (the recipes are the shipped ones), and steps
// Zone.Update by SimClock.GameStep, the on-screen clock.
public sealed class FacilityProductionTests : IDisposable
{
    private const double IronCycle = 600;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-facilityproduction-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly CultCache _scratch;

    public FacilityProductionTests()
    {
        Directory.CreateDirectory(_root);
        _cache = Open("catalog.cc");
        _scratch = Open("scratch.cc");
        Add(_cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _scratch.Dispose();
        Directory.Delete(_root, true);
    }

    private CultCache Open(string name)
    {
        var path = Path.Combine(_root, name);
        File.Copy(TestCatalog.Repo, path);
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(path), AetheriaStores.CatalogTypes);
        return cache;
    }

    private static Shape Solid(int width, int height)
    {
        var shape = new Shape(width, height);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // A hull with one 2x2 Facility row, a bay the size of one cell (a Pocket) and a bay of sixteen (a Crate), and goods to fill them.
    private static void Add(CultCache cache)
    {
        cache.Upsert(new HullData
        {
            Name = "Test Yard", HullType = HullType.Station, Shape = Solid(8, 8), Price = 1, Durability = 10, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Facility, Position = new int2(0, 0), Shape = Solid(2, 2), Transform = "Fa.1" } }
        });
        cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = Solid(4, 4), Price = 20, Durability = 4 });
        cache.Upsert(new CargoBayData { Name = "Pocket", Shape = new Shape(), InteriorShape = Solid(1, 1), Price = 5, Durability = 4 });
        cache.Upsert(new SimpleCommodityData { Name = "Slag", Shape = new Shape(), Price = 1, MaxStack = 100 });
        cache.Upsert(new ConsumableItemData { Name = "Facility Pill", Shape = new Shape(), Price = 1, Behaviors = { new FacilityData() } });
        // The shipped facility gear carries GearData's unauthored thermal defaults (0 to 100, best at 50) while every station cell starts
        // at 280, so a shipped facility is not Active (ThermalOnline) on a station that is not kept warm: a catalog fact this cut does
        // not write. The fixture's station has no heater and cools toward 0, so its private copy of the catalog gives facility gear a
        // band that holds every temperature a cell reaches, and the cycle rule is what these tests read.
        foreach (var facility in cache.GetAll<GearData>().Where(StationKinds.IsRecipeFacility).ToArray())
        {
            facility.MinimumTemperature = -1000;
            facility.MaximumTemperature = 1000;
            facility.OptimalTemperature = 0;
            facility.PlateauWidth = 1000;
            cache.Upsert(facility);
        }
    }

    private sealed class World
    {
        public ItemManager Items;
        public Zone Zone;
        public OrbitalEntity Station;
        public Facility Facility;
        public EquippableItem Unit;
        public EntityHold Hold;
    }

    private EquippableItem Mint(ItemManager items, CultCache cache, string design)
    {
        var data = cache.GetByName<EquippableItemData>(design);
        return new EquippableItem
        {
            Data = cache.RefOf<ItemData>(data),
            Durability = data.Durability,
            Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(data), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        };
    }

    // A station of Test Yard with the named facility on its row and the named bay.
    private World Build(string facility = "Refinery", string bay = "Crate")
    {
        var items = new ItemManager(1u, _cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var station = new OrbitalEntity(items, zone, Mint(items, _cache, "Test Yard"), default, new EntitySettings());
        Assert.True(station.TryEquip(Mint(items, _cache, facility), new int2(0, 0)));
        Assert.True(station.TryEquip(Mint(items, _cache, bay), new int2(4, 4)));
        zone.Admit(station, piloted: false);
        return Wrap(items, zone, station);
    }

    private static World Wrap(ItemManager items, Zone zone, OrbitalEntity station)
    {
        var item = station.Equipment.Single(equipped => equipped.Behaviors.Any(behavior => behavior is Facility));
        return new World
        {
            Items = items, Zone = zone, Station = station, Facility = item.Behaviors.OfType<Facility>().Single(),
            Unit = item.EquippableItem, Hold = new EntityHold(station)
        };
    }

    private SimpleCommodity Goods(string design, int units) =>
        new SimpleCommodity { Data = new CultRecordRef<ItemData>(_cache.RefOf<ItemData>(_cache.GetByName<SimpleCommodityData>(design)).Key), Quantity = units };

    private static SimpleCommodity Goods(CultRecordKey design, int units) =>
        new SimpleCommodity { Data = new CultRecordRef<ItemData>(design), Quantity = units };

    private static void Stock(World world, CultRecordKey design, int units) =>
        Assert.True(world.Station.CargoBays[0].TryStore(Goods(design, units)));

    private static void Step(World world, int steps)
    {
        for (var at = 0; at < steps; at++) world.Zone.Update(SimClock.GameStep);
    }

    // The steps whose sum of GameStep first reaches the cycle: the first of them also starts it.
    private static int StepsOf(double seconds) => (int) Math.Round(seconds / SimClock.GameStep);

    private CultRecordKey[] IronFillers => Recipes.Fillers(_cache, "iron ore");
    private CultRecordKey Iron => new CultRecordKey("commodity:iron");

    // What the station's bays hold and its facilities run, as bytes: nothing else of the entity is read.
    private static byte[] Contents(World world)
    {
        var pack = EntitySerializer.Pack(world.Station);
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        return MessagePackSerializer.Serialize((pack.Equipment, pack.CargoBays, pack.CargoContents), options);
    }

    private static int Held(World world, CultRecordKey design) => world.Hold.Held(design);

    [Fact]
    public void ARefineryTurnsOreIntoIron()
    {
        var world = Build();
        Stock(world, IronFillers[0], 3);
        Step(world, 1);
        Assert.Equal(0, Held(world, IronFillers[0]));
        Assert.Equal(RecipeData.KeyOf(Iron), world.Unit.Run.Recipe.Key);
        Assert.Equal(0, Held(world, Iron));

        Step(world, StepsOf(IronCycle) - 1);
        Assert.Equal(2, Held(world, Iron));
        Assert.Equal(0, Held(world, IronFillers[0]));
        Assert.Equal(0, world.Unit.Run.Progress);
        Assert.Equal(Stall.Inputs, world.Facility.Stall);
        Assert.Equal(1, world.Station.CargoBays[0].Cargo.Count);
    }

    [Fact]
    public void TakenInputsStayTaken()
    {
        var world = Build();
        Stock(world, IronFillers[0], 6);
        Step(world, 1);
        Assert.True(world.Hold.TryTake(new[] { (IronFillers[0], 3) }));
        Assert.Equal(0, Held(world, IronFillers[0]));

        Step(world, StepsOf(IronCycle) - 1);
        Assert.Equal(2, Held(world, Iron));
    }

    [Fact]
    public void ShortHoldWritesNothing()
    {
        var world = Build();
        Stock(world, IronFillers[0], 2);
        var before = Contents(world);
        Step(world, StepsOf(100));
        Assert.Null(world.Unit.Run);
        Assert.Equal(before, Contents(world));
        Assert.Equal(Stall.Inputs, world.Facility.Stall);
    }

    [Fact]
    public void FullHoldHoldsTheBatch()
    {
        var world = Build(bay: "Pocket");
        Stock(world, IronFillers[0], 3);
        Step(world, 1);
        Assert.Equal(0, Held(world, IronFillers[0]));
        var slag = Goods("Slag", 1);
        Assert.True(world.Station.CargoBays[0].TryStore(slag));

        Step(world, StepsOf(IronCycle) + 100);
        Assert.Equal(Stall.Room, world.Facility.Stall);
        Assert.Equal(IronCycle, world.Unit.Run.Progress);
        Assert.Equal(0, Held(world, Iron));
        Assert.Equal(0, Held(world, IronFillers[0]));

        world.Station.CargoBays[0].Remove(slag);
        Step(world, 1);
        Assert.Equal(2, Held(world, Iron));
        Assert.Equal(0, world.Unit.Run.Progress);
        Assert.Equal(Stall.Inputs, world.Facility.Stall);
    }

    [Fact]
    public void RoundRobinFromTheCursor()
    {
        var world = Build();
        var copperOre = Recipes.Fillers(_cache, "copper ore").Except(IronFillers).First();
        Stock(world, copperOre, 8);
        Stock(world, IronFillers[0], 6);
        var runList = Production.RunList(_cache, _cache.RefOf<ItemData>(_cache.GetByName<GearData>("Refinery")).Key);
        var copper = new CultRecordKey("commodity:copper");
        var cycles = new[] { 900.0, 600.0, 900.0, 600.0 };
        var produced = new List<(int copper, int iron)>();
        FacilityRun run = null;
        foreach (var span in cycles)
        {
            (run, _) = Production.Advance(_cache, runList, run, span, world.Hold);
            produced.Add((Held(world, copper), Held(world, Iron)));
        }
        Assert.Equal(new[] { (1, 0), (1, 2), (2, 2), (2, 4) }, produced);
    }

    [Fact]
    public void RolesFillInFillerOrder()
    {
        var world = Build();
        var fillers = IronFillers;
        var recipe = _cache.Get<RecipeData>(RecipeData.KeyOf(Iron));
        Stock(world, fillers[0], 4);
        Stock(world, fillers[1], 4);
        Assert.Equal(new[] { (fillers[0], 3) }, Production.Take(_cache, recipe, world.Hold));

        Assert.True(world.Hold.TryTake(new[] { (fillers[0], 2) }));
        Assert.Equal(new[] { (fillers[0], 2), (fillers[1], 1) }, Production.Take(_cache, recipe, world.Hold));
    }

    [Fact]
    public void RunListIsTheRule()
    {
        var expected = new Dictionary<string, int> { ["Refinery"] = 18, ["Munitions Line"] = 5, ["Algae Vats"] = 1,
            ["Fabrication Line"] = 0, ["Extraction Rig"] = 0, ["Volatiles Skimmer"] = 0 };
        foreach (var design in _cache.GetAll<GearData>().Where(StationKinds.IsRecipeFacility))
        {
            var key = _cache.RefOf(design).Key;
            var rule = _cache.GetAll<RecipeData>()
                .Where(recipe => recipe.Facility.IsSet() && recipe.Facility.Key.Equals(key))
                .Where(recipe => !Recipes.IsRaw(_cache, recipe) && _cache.Get(recipe.Output) is SimpleCommodityData)
                .OrderBy(recipe => RecipeData.KeyOf(recipe.Output.Key).Value, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(rule, Production.RunList(_cache, key));
            Assert.Equal(expected[design.Name], rule.Length);
        }

        foreach (var name in new[] { "Extraction Rig", "Volatiles Skimmer", "Fabrication Line" })
        {
            var world = Build(name);
            Step(world, 2);
            Assert.Equal(Stall.Unrunnable, world.Facility.Stall);
            Assert.Null(world.Unit.Run);
        }
    }

    [Fact]
    public void OneSpanEqualsManySteps()
    {
        var span = Build();
        var steps = Build();
        foreach (var world in new[] { span, steps }) Stock(world, IronFillers[0], 6);
        var runList = Production.RunList(_cache, _cache.RefOf<ItemData>(_cache.GetByName<GearData>("Refinery")).Key);

        var (run, _) = Production.Advance(_cache, runList, null, 2 * IronCycle, span.Hold);
        FacilityRun stepped = null;
        for (var at = 0; at < StepsOf(2 * IronCycle); at++)
            (stepped, _) = Production.Advance(_cache, runList, stepped, SimClock.GameStep, steps.Hold);

        Assert.Equal(4, Held(span, Iron));
        Assert.Equal(Held(span, Iron), Held(steps, Iron));
        Assert.Equal(Held(span, IronFillers[0]), Held(steps, IronFillers[0]));
        Assert.Equal((run.Recipe.Key, run.Progress), (stepped.Recipe.Key, stepped.Progress));
    }

    [Fact]
    public void RunSurvivesThePack()
    {
        var broken = Build();
        var whole = Build();
        foreach (var world in new[] { broken, whole }) Stock(world, IronFillers[0], 3);
        Step(broken, StepsOf(IronCycle) / 2);
        Step(whole, StepsOf(IronCycle) / 2);
        Assert.InRange(broken.Unit.Run.Progress, 1, IronCycle - 1);

        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var bytes = MessagePackSerializer.Serialize(EntitySerializer.Pack(broken.Station), options);
        var items = broken.Items;
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var station = (OrbitalEntity) EntitySerializer.Unpack(items, zone, MessagePackSerializer.Deserialize<EntityPack>(bytes, options));
        if (!zone.Entities.Contains(station)) zone.Admit(station, piloted: false);
        var restored = Wrap(items, zone, station);
        Assert.Equal(whole.Unit.Run.Progress, restored.Unit.Run.Progress);

        Step(restored, StepsOf(IronCycle) - StepsOf(IronCycle) / 2);
        Step(whole, StepsOf(IronCycle) - StepsOf(IronCycle) / 2);
        Assert.Equal(2, Held(restored, Iron));
        Assert.Equal((Held(whole, Iron), whole.Unit.Run.Progress), (Held(restored, Iron), restored.Unit.Run.Progress));
    }

    [Fact]
    public void TwoHistoriesAgree()
    {
        var first = Build();
        var second = Build();
        foreach (var world in new[] { first, second }) Stock(world, IronFillers[0], 6);
        foreach (var world in new[] { first, second }) Step(world, StepsOf(2 * IronCycle));
        Assert.Equal(4, Held(first, Iron));
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        Assert.Equal(MessagePackSerializer.Serialize(EntitySerializer.Pack(first.Station), options),
            MessagePackSerializer.Serialize(EntitySerializer.Pack(second.Station), options));
    }

    [Fact]
    public void NoFacilityOnAConsumable()
    {
        var world = Build();
        var pill = new ConsumableItem
        {
            Data = _cache.RefOf<ItemData>(_cache.GetByName<ConsumableItemData>("Facility Pill")),
            Lot = world.Items.Lots.Add(new Lot { Design = _cache.RefOf<ItemData>(_cache.GetByName<ConsumableItemData>("Facility Pill")),
                Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
        };
        var error = Assert.Throws<InvalidOperationException>(() => new ConsumableItemEffect(pill, world.Station));
        Assert.Contains("Facility Pill", error.Message);
    }
}
