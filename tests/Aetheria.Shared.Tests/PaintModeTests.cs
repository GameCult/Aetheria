using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// A ping sweep warns every ship its wavefront crosses inside its beam, and allies hear the return silently (cut
// paint-modes; rulings sensor-stat-set items 3 and 4, illuminator-beamforming, shared-track-bearing-only). Every
// fixture drives the production path: real Sensors in real ships tick inside Zone.Update. The numbers a rule is a
// function of are authored here and the expectations are the arithmetic of those numbers: a ping credit is
// visibility x sensitivity x PingBoost x distance, with the subject at a constant visibility of 5 and no clouds.
// The passive curve is zero, so a track moves only by ping credit (or by the decay a test turns on).
public sealed class PaintModeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-paintmode-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 HardpointCell = new int2(0, 0);
    private CultCache _openCache;
    private ItemManager _items;
    private Zone _zone;
    private readonly List<Ship> _ships = new List<Ship>();

    public PaintModeTests()
    {
        Directory.CreateDirectory(_root);
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            cache.Upsert(new VerseGrammar { Revision = 1 });
            var hullShape = new Shape(3, 3);
            foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
            cache.Upsert(new HullData
            {
                Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 1000, Mass = 1000,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
            });
            cache.Upsert(SensorGear("Omni", sensitivity: 2f, pingBoost: .0001f, beamWidth: null));
            cache.Upsert(SensorGear("Beam90", sensitivity: 2f, pingBoost: .0001f, beamWidth: 90f));
            cache.Upsert(SensorGear("Ear", sensitivity: 3f, pingBoost: .0002f, beamWidth: null));
            cache.FlushAsync().Wait();
        }

        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        for (var lot = 1; lot < 12; lot++) ledger.Lots[lot] = new Lot { Origin = new Attributed(), Quality = 1 };
        _items = new ItemManager(_openCache, ledger, new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f,
            TargetArmorInfoThreshold = .2f,
            TargetGearInfoThreshold = .8f,
            FiringArc = 170,
            CommitHorizon = .5f,
            SchematicCellSize = 1f,
            UnaidedAccuracy = .05f,
            UnaidedTracking = 10f,
            UnaidedPrecision = 2f,
            AgentMinHitProbability = 0f,
            BeamResolveInterval = .1f,
            HeatRadiationMultiplier = 0f // the hull radiates nothing, so a ship's light is the one source the fixture adds
        }, _ => { });
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
    }

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Const(float v) => new PerformanceStat { Min = v, Max = v };

    private static GearData SensorGear(string name, float sensitivity, float pingBoost, float? beamWidth) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors =
        {
            new SensorData
            {
                Sensitivity = Const(sensitivity),
                SensitivityCurve = new BezierCurve { Keys = new[] { float4(0, 0, 0, 0), float4(1, 0, 0, 0) } }, // no passive gain
                PingBoost = Const(pingBoost), PingEnergy = Const(0), PingVisibility = Const(5), PingRange = Const(300),
                PingCooldown = Const(4),
                BeamWidth = beamWidth.HasValue ? Const(beamWidth.Value) : null
            }
        }
    };

    private int _nextLot = 2;

    // A ship at `position`; a ship without a `sensor` is a bare hull. Every ship is lit at a constant visibility of 5.
    private Ship NewShip(float3 position, string sensor = null, Faction faction = null, bool factionless = false)
    {
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>("Skiff"));
        var ship = new Ship(_items, _zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings())
            { Faction = factionless ? null : faction ?? new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
        if (sensor != null)
        {
            var gearRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<GearData>(sensor));
            Assert.True(ship.TryEquip(new EquippableItem { Data = gearRef, Durability = 1, Lot = _nextLot++ }, HardpointCell));
        }
        _zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        ship.SetSignature(new object(), SignatureChannel.Reflection, 5f, null);
        _zone.Update(0f); // brings the gear online
        _ships.Add(ship);
        return ship;
    }

    // One short tick so every sensor's cooldown is below zero and it may ping.
    private void Settle()
    {
        _zone.Update(.01f);
        foreach (var ship in _ships) Assert.Equal(5f, ship.Visibility, 3); // fixture: a constant, known light
    }

    private static Sensor SensorOf(Entity e) => e.GetBehavior<Sensor>();

    private static EquippedItem GearOf(Entity e) => e.Equipment.Single(i => i.Behaviors.Any(b => b is Sensor));

    private static float Track(Entity observer, Entity subject) =>
        observer.EntityInfoGathered.TryGetValue(subject, out var info) ? info : 0f;

    private void Tick(float dt) => _zone.Update(dt);

    // Pings and ticks until the wavefront has passed everything (the last step overshoots the range a little).
    // Returns, per ship, the paints held after each tick and the tick on which the wavefront first passed it.
    private (Dictionary<Ship, List<PaintWarning[]>> held, Dictionary<Ship, int> crossedAt) SweepAndWatch(Ship pinger, float dt)
    {
        var sensor = SensorOf(pinger);
        sensor.Ping();
        var held = _ships.ToDictionary(s => s, s => new List<PaintWarning[]>());
        var crossedAt = new Dictionary<Ship, int>();
        var ticks = (int)(2.4f / dt);
        for (var tick = 1; tick <= ticks; tick++)
        {
            Tick(dt);
            foreach (var ship in _ships)
            {
                held[ship].Add(ship.IncomingPaints.ToArray());
                var planar = length(ship.Position.xz - pinger.Position.xz);
                if (ship != pinger && !crossedAt.ContainsKey(ship) && sensor.PingRadius > planar) crossedAt[ship] = tick;
            }
        }
        return (held, crossedAt);
    }

    [Fact]
    public void APingWarnsEveryShipItSweeps()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var ahead = NewShip(float3(0, 0, 100));
        var behind = NewShip(float3(0, 0, -120));
        var sideAlly = NewShip(float3(80, 0, 0), faction: a.Faction); // an ally is swept like anyone
        var beyondRange = NewShip(float3(0, 0, 350));
        Settle();

        var (held, crossedAt) = SweepAndWatch(a, .1f);

        foreach (var swept in new[] { ahead, behind, sideAlly })
        {
            Assert.True(crossedAt.TryGetValue(swept, out var crossed), "fixture: the wavefront reaches the ship");
            // One paint, published on the tick after the crossing (list index crossed = tick crossed + 1), gone after.
            var counts = held[swept].Select(h => h.Length).ToArray();
            Assert.Equal(1, counts[crossed]);
            Assert.Equal(1, counts.Sum());
            var paint = held[swept][crossed].Single();
            Assert.Equal(PaintMode.Search, paint.Mode);
            Assert.Equal(0f, paint.Strength);
            Assert.Same(GearOf(a).Data, paint.EmitterClass);
            var toward = normalize((a.Position - swept.Position).xz);
            Assert.Equal(toward.x, paint.Bearing.x, 4);
            Assert.Equal(toward.y, paint.Bearing.y, 4);
        }

        Assert.False(crossedAt.ContainsKey(beyondRange), "fixture: the wavefront never reaches the far ship");
        Assert.All(held[beyondRange], h => Assert.Empty(h));
        Assert.All(held[a], h => Assert.Empty(h)); // a ship is not swept by its own ping
    }

    [Fact]
    public void APingOutsideItsBeamPaintsNothing()
    {
        // Beam 90 degrees facing +x: half-width 45.
        var a = NewShip(float3(0, 0, 0), "Beam90");
        a.Direction = float2(1, 0);
        float3 At(float degrees) => float3(100 * cos(degrees * .0174533f), 0, 100 * sin(degrees * .0174533f));
        var onAxis = NewShip(At(0));
        var justInside = NewShip(At(44));
        var justOutside = NewShip(At(46));
        var abeam = NewShip(At(90));
        var astern = NewShip(At(180));
        Settle();

        var (held, crossedAt) = SweepAndWatch(a, .1f);

        foreach (var ship in new[] { onAxis, justInside })
        {
            Assert.Equal(1, held[ship].Sum(h => h.Length));
            Assert.True(Track(a, ship) > 0f, "a ship in the beam is credited");
        }
        foreach (var ship in new[] { justOutside, abeam, astern })
        {
            Assert.True(crossedAt.ContainsKey(ship), "fixture: the wavefront reaches the ship's distance");
            Assert.Equal(0, held[ship].Sum(h => h.Length));
            Assert.Equal(0f, Track(a, ship));
        }
    }

    // The one credit a ping gives: 5 x 2 x .0001 x 100 = 0.1, whatever the tick length.
    [Theory]
    [InlineData(1f / 120f)]
    [InlineData(1f / 10f)]
    public void OneCreditPerSweep(float dt)
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var b = NewShip(float3(0, 0, 100));
        Settle();

        SweepAndWatch(a, dt);

        Assert.Equal(.1f, Track(a, b), 4);
    }

    [Fact]
    public void AnAllyHearsTheReturnSilently()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(30, 0, 140), "Ear", a.Faction);   // 50 from T; its own sensitivity and boost
        var d = NewShip(float3(30, 0, 100), "Ear");              // another faction
        var e = NewShip(float3(-30, 0, 130), "Ear", a.Faction);  // same faction but hostile to A
        e.SetIff(a, true);
        Settle();
        var cEmission = c.SignatureOn(SignatureChannel.Emission);

        var (held, _) = SweepAndWatch(a, .1f);

        // C: 5 x 3 x .0002 x 50 = .15, from C's own sensor and C's own distance; A's own credit is .1.
        Assert.Equal(.15f, Track(c, t), 4);
        Assert.Equal(.1f, Track(a, t), 4);
        // Nothing of C is emitted or painted: T's only paint is A's sweep.
        Assert.Equal(cEmission, c.SignatureOn(SignatureChannel.Emission));
        var paint = Assert.Single(held[t].SelectMany(h => h));
        Assert.Same(GearOf(a).Data, paint.EmitterClass);
        // A hostile or foreign listener hears nothing of A's ping.
        Assert.Equal(0f, Track(d, t));
        Assert.Equal(0f, Track(e, t));
    }

    // Two sweeps crossing one subject on one tick are two observations: C's own ping and A's ping heard as an ally.
    // A and C stand 100 from T, so both wavefronts pass it on the same tick.
    [Fact]
    public void ConcurrentSweepsCreditOncePerCrossing()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(100, 0, 100), "Ear", a.Faction);
        Settle();

        SensorOf(a).Ping();
        SensorOf(c).Ping();
        for (var tick = 0; tick < 24; tick++) Tick(.1f);

        Assert.Equal(2 * .3f, Track(c, t), 4); // 5 x 3 x .0002 x 100, its own ping and A's
        Assert.Equal(2 * .1f, Track(a, t), 4); // 5 x 2 x .0001 x 100, its own ping and C's
    }

    // Allegiance needs a faction: two factionless ships are not allies, so one hears nothing of the other's ping.
    [Fact]
    public void FactionlessShipsAreNotAllies()
    {
        var a = NewShip(float3(0, 0, 0), "Omni", factionless: true);
        var t = NewShip(float3(0, 0, 100));
        var drifter = NewShip(float3(30, 0, 140), "Ear", factionless: true);
        Settle();

        SweepAndWatch(a, .1f);

        Assert.Equal(.1f, Track(a, t), 4);
        Assert.Equal(0f, Track(drifter, t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KillingTheIlluminatorBlindsTheAlly(bool illuminatorDies)
    {
        _items.GameplaySettings.TargetInfoDecay = .5f;
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(30, 0, 140), "Ear", a.Faction);
        Settle();
        c.EntityInfoGathered[t] = .5f;

        SensorOf(a).Ping();
        var track = new List<float>();
        for (var tick = 1; tick <= 12; tick++)
        {
            Tick(.1f);
            if (tick == 2 && illuminatorDies)
            {
                Assert.True(SensorOf(a).PingRadius < 100f, "fixture: A dies before its wavefront reaches T");
                _zone.Entities.Remove(a);
            }
            track.Add(Track(c, t));
        }

        var rises = Enumerable.Range(1, track.Count - 1).Count(i => track[i] > track[i - 1]);
        if (illuminatorDies)
        {
            Assert.Equal(0, rises);
            Assert.True(track.Last() > 0f && track.Last() < .5f);
        }
        else Assert.Equal(1, rises); // the allied sweep crossing T is the one rise
    }
}
