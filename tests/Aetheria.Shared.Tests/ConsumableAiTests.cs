using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// AI pilots use consumables (aetheria-release cut consumable-ai r3; rulings sensor-stat-set item 4,
// lock-warning-strength, launcher-lock-solution-quality): CombatState vents when ONE Track paint in the ship's own
// IncomingPaints passes .5 and otherwise overdrives to close a target farther than twice its optimum range while
// heading within 15 degrees of it. It reads only its own ship's senses. A consumable is chosen by what its behaviours
// are, never by name, so the vent design is named "Overdrive" and the overdrive design "Coolant".
//
// The agent's one gun has Range 1000 and EffectiveRange 500 with nothing at maximum range, so its optimum range is
// about 500 while its shortest and longest gun range are both 1000: a threshold taken from either gun range is wrong
// and these tests can tell. The optimum is read by reflection, as FireControlTests does, and the thresholds are
// authored against it.
public sealed class ConsumableAiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-consumable-ai-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();
    private static readonly int2 HardpointCell = new int2(0, 0);
    private static readonly int2 HoldCell = new int2(1, 1);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public ConsumableAiTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var c in _caches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Const(float v) => new PerformanceStat { Min = v, Max = v };

    private sealed class Scene
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Agent, Prey, Locker;
        public EquippedCargoBay Hold;
        public ConsumableItemData Vent, Overdrive, Decoy, StackableVent;
        public CultRecordRef<Faction> Maker;
        public float Optimum;

        public void Store(ConsumableItemData design) =>
            Assert.True(Hold.TryStore((ItemInstance) Items.CreateInstance(Items.CreateLot(design, Maker, .5f))));

        public bool Active(ConsumableItemData design) => Agent.FindActiveConsumable(design) != null;
        public int Carried => Hold.Cargo.Keys.Count(i => i is ConsumableItem);

        public object State => typeof(Agent).GetField("_currentState", Private).GetValue(Zone.Agents.Single());
        public void AssertInCombat() => Assert.True(State is CombatState, $"the agent is in {State?.GetType().Name}");

        // The target at `distance` and `degrees` off the hull's heading, the agent at the origin heading (0, 1).
        public void Place(float distance, float degrees)
        {
            Agent.Position = float3.zero;
            Agent.Direction = float2(0, 1);
            var a = degrees * 0.0174533f;
            Prey.Position = float3(sin(a) * distance, 0, cos(a) * distance);
        }

        // Paints of the locker's launcher reaching the agent as the zone publishes them, then one tick in which the
        // agent decides. The agent reads them before the zone republishes (agents update, then PublishInbox).
        public void Tick(params (PaintMode mode, float strength)[] paints)
        {
            foreach (var (mode, strength) in paints)
                Agent.ReceivePaint(Locker, Locker.Equipment.Single(i => i.Behaviors.Any(b => b is LockWeapon)), mode, strength);
            Agent.PublishInbox();
            Zone.Update(.05f);
        }
    }

    private static GearData GunData() => new GearData
    {
        Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors =
        {
            new InstantWeaponData
            {
                Damage = Const(10), Range = Const(1000), MinRange = Const(0), Velocity = Const(0),
                Spread = Const(0), DamageSpread = Const(0), Penetration = Const(0), Count = Const(1),
                BurstTime = Const(0), Cooldown = Const(1),
                EffectiveRange = Const(500), DamageAtMaxRange = 0f
            }
        }
    };

    private Scene Build(bool carryVent = false, bool carryOverdrive = false, bool carryDecoy = false, int stackableVents = 0)
    {
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 1000000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
        });
        cache.Upsert(GunData());
        cache.Upsert(LockWarningTests.Launcher("Launcher", lockSpeed: .6f, sensorImpact: 1, lockAngle: 30, directionImpact: 1));
        var interior = new Shape(4, 1);
        foreach (var cell in interior.AllCoordinates) interior[cell] = true;
        cache.Upsert(new CargoBayData { Name = "Hold", Shape = new Shape(), InteriorShape = interior, Durability = 10 });
        // A vent named like the other kind, an overdrive named like it, a decoy, and a stackable vent.
        cache.Upsert(new ConsumableItemData
        {
            Name = "Overdrive", Duration = 100f, Shape = new Shape(),
            Behaviors = { new VapourDumpData { Radius = Const(20), Opacity = Const(.9f), Lifetime = Const(100) } }
        });
        cache.Upsert(new ConsumableItemData { Name = "Coolant", Duration = 100f, Shape = new Shape(), Behaviors = { new ThrottleLockData() } });
        cache.Upsert(new ConsumableItemData { Name = "Flare", Duration = 1f, Shape = new Shape() });
        cache.Upsert(new ConsumableItemData
        {
            Name = "StackVent", Duration = 100f, Stackable = true, Shape = new Shape(),
            Behaviors = { new VapourDumpData { Radius = Const(20), Opacity = Const(.9f), Lifetime = Const(100) } }
        });
        cache.FlushAsync().Wait();

        var settings = RunSaveTests.TestSettings();
        settings.TargetDetectionInfoThreshold = .1f;
        settings.TargetGearInfoThreshold = .8f;
        settings.FiringArc = 170;
        settings.CommitHorizon = .5f;
        settings.SchematicCellSize = 1f;
        settings.UnaidedAccuracy = .05f;
        settings.UnaidedTracking = 10f;
        settings.UnaidedPrecision = 2f;
        settings.AgentMinHitProbability = 0f;
        settings.BeamResolveInterval = .1f;
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
        var maker = cache.RefOf(cache.GetByName<Faction>("Maker"));
        EquippableItem Make<T>(string name) where T : CraftedItemData =>
            (EquippableItem) items.CreateInstance(items.CreateLot(cache.GetByName<T>(name), maker, .5f));
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        Ship NewShip(string gear)
        {
            var ship = new Ship(items, zone, Make<HullData>("Skiff"), new EntitySettings())
                { Faction = new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
            if (gear != null) Assert.True(ship.TryEquip(Make<GearData>(gear), HardpointCell));
            return ship;
        }

        var scene = new Scene
        {
            Items = items, Zone = zone, Maker = maker,
            Vent = cache.GetByName<ConsumableItemData>("Overdrive"),
            Overdrive = cache.GetByName<ConsumableItemData>("Coolant"),
            Decoy = cache.GetByName<ConsumableItemData>("Flare"),
            StackableVent = cache.GetByName<ConsumableItemData>("StackVent"),
            Agent = NewShip("Gun"), Prey = NewShip(null), Locker = NewShip("Launcher")
        };
        Assert.True(scene.Agent.TryEquip(Make<CargoBayData>("Hold"), HoldCell));
        scene.Hold = scene.Agent.CargoBays.Single();
        if (carryVent) scene.Store(scene.Vent);
        if (carryOverdrive) scene.Store(scene.Overdrive);
        if (carryDecoy) scene.Store(scene.Decoy);
        for (var i = 0; i < stackableVents; i++) scene.Store(scene.StackableVent);
        Assert.True(scene.Carried == (carryVent ? 1 : 0) + (carryOverdrive ? 1 : 0) + (carryDecoy ? 1 : 0) + stackableVents,
            $"carried {scene.Carried}, cargo entries {scene.Hold.Cargo.Count}: {string.Join(",", scene.Hold.Cargo.Keys.Select(k => k.GetType().Name))}");

        // The agent is driven by a Minion added by hand; the locker is a gun ship nobody pilots.
        zone.Admit(scene.Agent, piloted: false);
        zone.Admit(scene.Prey, piloted: false);
        zone.Admit(scene.Locker, piloted: false);
        zone.Agents.Add(new Minion(scene.Agent));
        zone.Update(0f); // brings the gear online
        scene.Agent.SetIff(scene.Prey, true);
        scene.Agent.EntityInfoGathered[scene.Prey] = 1f;
        scene.Agent.SetTarget(scene.Prey);
        scene.Locker.Position = float3(0, 0, -40);
        scene.Place(100f, 0f);
        zone.Update(.05f);
        zone.Update(.05f);
        scene.AssertInCombat();

        // The state sampled before the weapons' first stat evaluation: sample again over the live stats.
        typeof(CombatState).GetMethod("SampleDps", Private).Invoke(scene.State, null);
        scene.Optimum = (float) typeof(CombatState).GetField("_optimumRange", Private).GetValue(scene.State);
        Assert.InRange(scene.Optimum, 500f - 1000f / 32f, 500f + 1000f / 32f);
        return scene;
    }

    [Theory]
    [InlineData(.4f, false)]
    [InlineData(.49f, false)]
    [InlineData(.5f, false)]
    [InlineData(.51f, true)]
    [InlineData(.6f, true)]
    public void AnAgentVentsWhenLocked(float strength, bool vents)
    {
        var s = Build(carryVent: true, carryOverdrive: true);
        s.AssertInCombat();

        s.Tick((PaintMode.Track, strength));

        Assert.Equal(vents, s.Active(s.Vent));
        Assert.Equal(vents ? 1 : 2, s.Carried);
        Assert.False(s.Active(s.Overdrive));
    }

    [Fact]
    public void AnAgentVentsOnOneTrackPaintOnItself()
    {
        var s = Build(carryVent: true);
        s.AssertInCombat();

        s.Tick((PaintMode.Track, .3f), (PaintMode.Track, .3f)); // two weak locks never sum
        s.Tick((PaintMode.Search, .9f));
        s.Tick((PaintMode.Scan, .9f));
        Assert.False(s.Active(s.Vent));
        Assert.Equal(1, s.Carried);

        // The locker is a ship the agent has no information on, and one strong Track paint on it is enough.
        s.Tick((PaintMode.Track, .9f));
        Assert.True(s.Active(s.Vent));
        Assert.Equal(0, s.Carried);
    }

    // The production path: a real launcher on a ship the agent has never sensed locks it through zone ticks, and the
    // agent vents once the paint is published, with its own senses as the only input.
    [Fact]
    public void AnUnseenLockerThatLocksTheAgentMakesItVent()
    {
        var s = Build(carryVent: true);
        s.AssertInCombat();
        s.Locker.SetIff(s.Agent, true);
        s.Locker.EntityInfoGathered[s.Agent] = 1f;
        s.Locker.SetTarget(s.Agent);

        for (var i = 0; i < 4 && !s.Active(s.Vent); i++)
        {
            s.Place(100f, 0f);
            s.Locker.Aim = normalize(s.Agent.Position - s.Locker.Position);
            s.Zone.Update(1f);
        }

        Assert.True(s.Active(s.Vent));
        Assert.Equal(0, s.Carried);
        Assert.Equal(s.Prey, s.Agent.Target.Value.Entity);
    }

    [Fact]
    public void AnAgentThatIsLockedVentsInsteadOfOverdriving()
    {
        var s = Build(carryVent: true, carryOverdrive: true);
        s.AssertInCombat();
        s.Place(3 * s.Optimum, 0f);

        s.Tick((PaintMode.Track, .6f));

        Assert.True(s.Active(s.Vent));
        Assert.False(s.Active(s.Overdrive));
        Assert.Equal(1, s.Carried);
    }

    [Theory]
    [InlineData(0f, 2f, 20f, true)]
    [InlineData(14f, 2f, 20f, true)]
    [InlineData(16f, 2f, 20f, false)]
    [InlineData(30f, 2f, 20f, false)]
    [InlineData(180f, 2f, 20f, false)]
    [InlineData(0f, 2f, -20f, false)]
    [InlineData(0f, 1f, 0f, false)]
    public void AnAgentOverdrivesToClose(float degrees, float optimums, float offset, bool overdrives)
    {
        var s = Build(carryVent: true, carryOverdrive: true);
        s.AssertInCombat();
        s.Place(optimums * s.Optimum + offset, degrees);

        s.Tick();

        Assert.Equal(overdrives, s.Active(s.Overdrive));
        Assert.False(s.Active(s.Vent));
        Assert.Equal(overdrives ? 1 : 2, s.Carried);
    }

    [Fact]
    public void AnOverdriveIsNotStoppedByAWeakTrackPaint()
    {
        var s = Build(carryVent: true, carryOverdrive: true);
        s.AssertInCombat();
        s.Place(2 * s.Optimum + 20f, 0f);

        s.Tick((PaintMode.Track, .4f));

        Assert.True(s.Active(s.Overdrive));
        Assert.False(s.Active(s.Vent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAgentWithoutConsumablesDoesNothing(bool carryDecoy)
    {
        var s = Build(carryDecoy: carryDecoy);
        s.AssertInCombat();

        s.Tick((PaintMode.Track, .9f));
        s.Place(3 * s.Optimum, 0f);
        s.Tick();

        Assert.False(s.Active(s.Decoy));
        Assert.Equal(carryDecoy ? 1 : 0, s.Carried);
    }

    [Fact]
    public void ADeadOrDockedShipActivatesNothing()
    {
        var s = Build(carryVent: true, carryOverdrive: true);
        s.AssertInCombat();
        Assert.True(s.Zone.Remove(s.Agent)); // what death and docking do
        s.Agent.Deactivate();
        s.Place(3 * s.Optimum, 0f);

        s.Tick();
        s.Tick();

        Assert.Equal(2, s.Carried);
        Assert.False(s.Active(s.Vent));
        Assert.False(s.Active(s.Overdrive));
        Assert.False(s.Agent.TryActivateConsumable(s.Overdrive));
        Assert.Equal(2, s.Carried);
    }

    [Fact]
    public void AStackOfVentsIsSpentOneAtATime()
    {
        var s = Build(stackableVents: 3);
        s.AssertInCombat();

        s.Tick((PaintMode.Track, .6f));
        s.Tick((PaintMode.Track, .6f));
        s.Tick((PaintMode.Track, .6f));

        Assert.True(s.Active(s.StackableVent));
        Assert.Equal(2, s.Carried);
    }
}
