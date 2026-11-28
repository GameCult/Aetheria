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

    public FacilityProductionTests()
    {
        Directory.CreateDirectory(_root);
        _cache = Open("catalog.cc");
        Add(_cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
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

    // The shipped catalog as it boots, a shipped station hull with no heater, the shipped Refinery on its Facility row: an hour of sim
    // time (the station's cells cool toward 0 meanwhile) turns 15 ore into 5 cycles of iron, one cycle fewer than the hour holds.
    [Fact]
    public void TheShippedRefineryRunsAnHourOnAHeaterlessShippedStation()
    {
        using var shipped = Open("shipped.cc");
        var items = new ItemManager(1u, shipped, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var station = new OrbitalEntity(items, zone, Mint(items, shipped, "Zenith"), default, new EntitySettings());
        Assert.True(station.TryEquip(Mint(items, shipped, "Refinery")));
        var bay = shipped.GetAll<CargoBayData>().Where(data => !(data is DockingBayData)).OrderByDescending(data => data.InteriorShape.Coordinates.Length).ThenBy(data => data.Name, StringComparer.Ordinal).First();
        Assert.True(station.TryEquip(Mint(items, shipped, bay.Name)));
        Assert.DoesNotContain(station.Equipment.Select(equipped => equipped.Data), data => data is GearData gear && gear.Behaviors.Any(behavior => behavior is HeatData));
        zone.Admit(station, piloted: false);
        var world = Wrap(items, zone, station);
        // An ore only iron takes, so no other recipe competes for it in the round robin.
        var others = shipped.GetAll<RecipeData>().SelectMany(recipe => Recipes.RoleInputsOf(shipped, recipe)).Select(role => role.Name).Where(role => role != "iron ore").Distinct();
        var ore = Recipes.Fillers(shipped, "iron ore").Except(others.SelectMany(role => Recipes.Fillers(shipped, role))).First();
        var maxStack = items.MaxStack(Goods(ore, 1));
        for (var left = 15; left > 0; left -= maxStack) Assert.True(station.CargoBays[0].TryStore(Goods(ore, Math.Min(left, maxStack))));

        Step(world, StepsOf(3600));
        Assert.Equal(10, world.Hold.Held(new CultRecordKey("commodity:iron")));
        Assert.Equal(0, world.Hold.Held(ore));
        Assert.True(world.Station.Equipment.Single(equipped => equipped.Behaviors.Any(behavior => behavior is Facility)).Active.Value);
    }

    [Fact]
    public void AnIdleFacilityTriesAgainWhenTheHoldChanges()
    {
        var world = Build();
        Stock(world, IronFillers[0], 2);
        Step(world, 3);
        Assert.Equal(Stall.Inputs, world.Facility.Stall);
        Assert.Null(world.Unit.Run);

        Stock(world, IronFillers[0], 1);
        Step(world, 1);
        Assert.Equal(RecipeData.KeyOf(Iron), world.Unit.Run.Recipe.Key);
        Assert.Equal(0, Held(world, IronFillers[0]));
    }

    // The entity's cargo revision moves on every kind of change to what its bays hold (a stack stored, merged, split or removed, a unit
    // stored or removed, a bay seated or removed) and on nothing else.
    [Fact]
    public void TheCargoRevisionCountsEveryChangeToWhatTheBaysHold()
    {
        var world = Build();
        var bay = world.Station.CargoBays[0];
        var at = world.Station.CargoRevision;
        Assert.Equal(at, world.Hold.Revision);
        void Moved() { Assert.True(world.Station.CargoRevision > at); at = world.Station.CargoRevision; }
        Assert.True(bay.TryStore(Goods("Slag", 5)));
        Moved();
        Assert.True(bay.TryStore(Goods("Slag", 5)));
        Moved();
        var stack = bay.Cargo.Keys.Single();
        Assert.NotNull(bay.Remove(stack, 3));
        Moved();
        Assert.Equal(7, bay.Count(stack.Data.Key));
        Assert.Equal(at, world.Station.CargoRevision);
        bay.Remove(stack);
        Moved();
        var unit = Mint(world.Items, _cache, "Pocket");
        Assert.True(bay.TryStore(unit));
        Moved();
        bay.Remove(unit);
        Moved();
        world.Station.Deactivate();
        var pocket = world.Station.TryEquip(Mint(world.Items, _cache, "Pocket"), new int2(6, 6));
        Assert.True(pocket);
        Moved();
        Assert.NotNull(world.Station.TryUnequip(world.Station.CargoBays[1]));
        Moved();
    }

    // Soul's sequence: a take fails with 2 ore held; the station is deactivated, its empty second bay removed, one ore stored, and it
    // is activated again. The hold's revision is never the one the failure recorded, so the facility takes the 3 ore.
    [Fact]
    public void AFacilityTakesWhatIsStockedAfterABayLeaves()
    {
        var world = Build();
        world.Station.Deactivate();
        Assert.True(world.Station.TryEquip(Mint(world.Items, _cache, "Pocket"), new int2(6, 6)));
        world.Station.Activate();
        Stock(world, IronFillers[0], 2);
        Step(world, 3);
        Assert.Equal(Stall.Inputs, world.Facility.Stall);

        world.Station.Deactivate();
        Assert.NotNull(world.Station.TryUnequip(world.Station.CargoBays[1]));
        Stock(world, IronFillers[0], 1);
        world.Station.Activate();
        Step(world, 3);
        Assert.NotNull(world.Unit.Run);
        Assert.Equal(0, Held(world, IronFillers[0]));
    }

    // An idle facility whose take failed does not try again until the hold changes: the failed take is recorded.
    [Fact]
    public void AnIdleFacilityDoesNotRetryUntilTheHoldChanges()
    {
        var world = Build();
        Stock(world, IronFillers[0], 2);
        Step(world, 20);
        Assert.Equal(1, world.Facility.Attempts);
        Stock(world, IronFillers[0], 1);
        Step(world, 1);
        Assert.Equal(2, world.Facility.Attempts);
        Assert.NotNull(world.Unit.Run);
    }

    // A save written mid-cycle, then a catalog that no longer has the recipe: the run is dropped, the inputs it took stay consumed, and
    // the facility starts its next recipe. Nothing throws on any step.
    [Fact]
    public void ASaveNamingADeletedRecipeIsDroppedNotThrown()
    {
        var world = Build();
        var copperOre = Recipes.Fillers(_cache, "copper ore").Except(IronFillers).First();
        Stock(world, IronFillers[0], 3);
        Step(world, 101);
        Assert.Equal(RecipeData.KeyOf(Iron), world.Unit.Run.Recipe.Key);
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var bytes = MessagePackSerializer.Serialize(EntitySerializer.Pack(world.Station), options);

        _cache.Remove(RecipeData.KeyOf(Iron));
        var zone = new Zone(world.Items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var station = (OrbitalEntity) EntitySerializer.Unpack(world.Items, zone, MessagePackSerializer.Deserialize<EntityPack>(bytes, options));
        if (!zone.Entities.Contains(station)) zone.Admit(station, piloted: false);
        var restored = Wrap(world.Items, zone, station);
        Assert.Equal(RecipeData.KeyOf(Iron), restored.Unit.Run.Recipe.Key);

        Stock(restored, copperOre, 4);
        Step(restored, 2);
        Assert.Equal(RecipeData.KeyOf(new CultRecordKey("commodity:copper")), restored.Unit.Run.Recipe.Key);
        Assert.Equal(0, Held(restored, copperOre));
        Assert.Equal(0, Held(restored, Iron));
        Assert.Equal(0, Held(restored, IronFillers[0]));
    }

    private void Edit(CultRecordKey recipe, Action<RecipeData> change)
    {
        var held = _cache.Get<RecipeData>(recipe);
        change(held);
        _cache.Upsert(typeof(RecipeData), held, recipe);
    }

    private static T Within<T>(Func<T> work)
    {
        var task = System.Threading.Tasks.Task.Run(work);
        Assert.True(task.Wait(TimeSpan.FromSeconds(20)), "Advance did not return: it spins on a stale run list");
        return task.Result;
    }

    // A run list built before the catalog changed names a recipe the catalog no longer holds, or holds as a raw one: nothing starts
    // from it, the stock is untouched, the call returns, and the recipes that still run still start.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStaleRunListStartsNothingAndReturns(bool removed)
    {
        var world = Build();
        var runList = Production.RunList(_cache, _cache.RefOf<ItemData>(_cache.GetByName<GearData>("Refinery")).Key);
        Assert.Contains(runList, recipe => recipe.Output.Key.Equals(Iron));
        var ironKey = RecipeData.KeyOf(Iron);
        if (removed) _cache.Remove(ironKey);
        else Edit(ironKey, recipe => recipe.RoleInputs.Clear());
        Stock(world, IronFillers[0], 3);

        var (run, stall) = Within(() => Production.Advance(_cache, runList, null, 1200, world.Hold));
        Assert.Null(run);
        Assert.Equal(Stall.Inputs, stall);
        Assert.Equal(3, Held(world, IronFillers[0]));
        Assert.Equal(0, Held(world, Iron));

        var copperOre = Recipes.Fillers(_cache, "copper ore").Except(IronFillers).First();
        Stock(world, copperOre, 4);
        (run, _) = Within(() => Production.Advance(_cache, runList, null, 10, world.Hold));
        Assert.Equal(RecipeData.KeyOf(new CultRecordKey("commodity:copper")), run.Recipe.Key);
    }

    // A running cycle whose recipe the catalog changed under it is dropped, whatever the change: the recipe removed, made raw (it takes
    // nothing now) or given a compound output (a graded output needs a lot). The facility's list is rebuilt and it moves on.
    [Theory]
    [InlineData("removed")]
    [InlineData("raw")]
    [InlineData("compound")]
    public void ARunningCycleWhoseRecipeStoppedRunningIsDropped(string change)
    {
        var world = Build();
        Stock(world, IronFillers[0], 3);
        Step(world, 101);
        Assert.Equal(RecipeData.KeyOf(Iron), world.Unit.Run.Recipe.Key);
        var ironKey = RecipeData.KeyOf(Iron);
        if (change == "removed") _cache.Remove(ironKey);
        else if (change == "raw") Edit(ironKey, recipe => recipe.RoleInputs.Clear());
        else Edit(ironKey, recipe => recipe.Output = new CultRecordRef<ItemData>(_cache.RefOf<ItemData>(_cache.GetAll<CompoundCommodityData>().First()).Key));

        Step(world, 2);
        Assert.Null(world.Unit.Run);
        Assert.Equal(0, Held(world, Iron));
        Assert.Equal(0, Held(world, IronFillers[0]));
        Stock(world, IronFillers[0], 3);
        Step(world, 2);
        Assert.Null(world.Unit.Run);
    }

    // Recipes.Fillers reads an index built once per catalog; it is rebuilt when a recipe changes.
    [Fact]
    public void FillersFollowTheCatalogThroughAChange()
    {
        var roles = _cache.GetAll<RecipeData>().SelectMany(recipe => recipe.Fills).Distinct().ToArray();
        foreach (var role in roles)
        {
            var scan = _cache.GetAll<RecipeData>()
                .Where(recipe => recipe.Fills.Contains(role) && Recipes.IsCommodity(_cache.Get(recipe.Output)))
                .Select(recipe => recipe.Output.Key).OrderBy(key => key.Value, StringComparer.Ordinal).ToArray();
            Assert.Equal(scan, Recipes.Fillers(_cache, role));
        }

        var slag = _cache.RefOf<ItemData>(_cache.GetByName<SimpleCommodityData>("Slag"));
        Assert.DoesNotContain(slag.Key, Recipes.Fillers(_cache, "iron ore"));
        _cache.Upsert(typeof(RecipeData), new RecipeData { Output = slag, Fills = { "iron ore" } }, RecipeData.KeyOf(slag.Key));
        Assert.Contains(slag.Key, Recipes.Fillers(_cache, "iron ore"));
        _cache.Remove(RecipeData.KeyOf(slag.Key));
        Assert.DoesNotContain(slag.Key, Recipes.Fillers(_cache, "iron ore"));
        Assert.Empty(Recipes.Fillers(_cache, "no such role"));
    }
}
