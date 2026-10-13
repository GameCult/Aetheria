using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

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
            var twinShape = new Shape(3, 3);
            foreach (var cell in twinShape.AllCoordinates) twinShape[cell] = true;
            cache.Upsert(new HullData
            {
                Name = "Twin", HullType = HullType.Ship, Shape = twinShape, Durability = 1000, Mass = 1000,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() },
                               new HardpointData { Type = HardpointType.Sensors, Position = new int2(1, 0), Shape = new Shape() } }
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
                PingBoost = Const(pingBoost), PingEnergy = Const(0), PingRange = Const(300),
                PingCooldown = Const(4),
                BeamWidth = beamWidth.HasValue ? Const(beamWidth.Value) : null
            }
        }
    };

    private int _nextLot = 2;

    // A ship at `position`; a ship without a `sensor` is a bare hull. Every ship is lit at a constant visibility of 5.
    private Ship NewShip(float3 position, string sensor = null, Faction faction = null, bool factionless = false, string hull = "Skiff", string sensor2 = null, Zone zone = null)
    {
        zone ??= _zone;
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>(hull));
        var ship = new Ship(_items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings())
            { Faction = factionless ? null : faction ?? new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
        if (sensor != null)
        {
            var gearRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<GearData>(sensor));
            Assert.True(ship.TryEquip(new EquippableItem { Data = gearRef, Durability = 1, Lot = _nextLot++ }, HardpointCell));
        }
        if (sensor2 != null)
        {
            var gearRef2 = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<GearData>(sensor2));
            Assert.True(ship.TryEquip(new EquippableItem { Data = gearRef2, Durability = 1, Lot = _nextLot++ }, new int2(1, 0)));
        }
        zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        ship.SetSignature(new object(), SignatureChannel.Reflection, 5f, null);
        zone.Update(0f); // brings the gear online
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

        var (held, _) = SweepAndWatch(a, .1f);

        // C hears the return bistatically, at the geometric mean of A's range to T (100) and its own (50), with its own
        // sensor: 5 x 3 x .0002 x sqrt(100 x 50) = .21213. A's own credit is .1.
        Assert.Equal(.21213f, Track(c, t), 4);
        Assert.Equal(.1f, Track(a, t), 4);
        // Nothing of C is emitted or painted: T's only paint is A's sweep.
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
        // A pinging ship is not a subject of its own ping, wherever its wavefront starts: A sees C once (A's own
        // sweep), at C's light of 5 (reflected; a ping adds none) and 100 x sqrt 2 away; and C sees A once.
        var apart = length(float2(100, 100));
        Assert.Equal(5 * 2 * .0001f * apart, Track(a, c), 4);
        Assert.Equal(5 * 3 * .0002f * apart, Track(c, a), 4);
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

    private static void Near(float expected, float actual, float tolerance = 1e-3f) =>
        Assert.True(abs(expected - actual) <= tolerance, $"expected {expected}, got {actual}");

    // Ships moving relative to the wavefront are crossed exactly once, on the first tick it is past them, whatever
    // their speed: 48 subjects 150-225 out, their positions set before each .1 tick, an omni ping of range 300 (its
    // radius is 300 x sqrt(.05 x tick) while it runs, 21 ticks). Inward ships close the gap faster than a tick's step
    // can bracket; outward ones flee near the front's own speed.
    [Theory]
    [InlineData(-85f)]
    [InlineData(-60f)]
    [InlineData(60f)]
    public void AMovingSubjectIsCrossedOnce(float outwardSpeed)
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        const int count = 48;
        const float dt = .1f;
        var subjects = new List<(Ship ship, float3 start, float3 outward)>();
        for (var i = 0; i < count; i++)
        {
            var angle = i * 2 * PI / count;
            var outward = float3(cos(angle), 0, sin(angle));
            var start = outward * (150f + 100f * (i % 4) / 4f); // phases differ
            subjects.Add((NewShip(start), start, outward));
        }
        Settle();

        var paints = subjects.ToDictionary(s => s.ship, _ => 0);
        var crossedAtDistance = new Dictionary<Ship, float>();
        SensorOf(a).Ping();
        for (var tick = 1; tick <= 24; tick++)
        {
            foreach (var (ship, start, outward) in subjects) ship.Position = start + outward * outwardSpeed * tick * dt;
            Tick(dt);
            foreach (var (ship, start, outward) in subjects)
            {
                var held = ship.IncomingPaints.Count;
                paints[ship] += held;
                // A paint is published on the tick after the crossing: the ship was then where the previous tick put it.
                if (held > 0) crossedAtDistance[ship] = length((start + outward * outwardSpeed * (tick - 1) * dt).xz);
            }
        }

        Assert.All(paints.Values, painted => Assert.True(painted <= 1, "a ping crosses a ship at most once"));
        if (outwardSpeed < 0) Assert.All(paints.Values, painted => Assert.Equal(1, painted));
        Assert.Contains(paints.Values, painted => painted == 1); // fixture: the sweep reaches ships
        foreach (var (ship, _, _) in subjects)
            Near(crossedAtDistance.TryGetValue(ship, out var distance) ? .001f * distance : 0f, Track(a, ship));
    }

    // C hears A's ping returned off T: A's wavefront crossed T at 100, C stands 50 from T, so the credit is at the
    // geometric mean range, 5 x 3 x .0002 x sqrt(100 x 50) = .21213. It lands on the tick after the crossing (tick
    // 3) and does not depend on the order the two ships tick in, or on A still being in the zone.
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public void AnAllysCreditIsTheSameInEveryListOrder(bool allyFirst, int removePingerAfterTick)
    {
        Ship a, c;
        if (allyFirst)
        {
            c = NewShip(float3(30, 0, 140), "Ear");
            a = NewShip(float3(0, 0, 0), "Omni", c.Faction);
        }
        else
        {
            a = NewShip(float3(0, 0, 0), "Omni");
            c = NewShip(float3(30, 0, 140), "Ear", a.Faction);
        }
        var t = NewShip(float3(0, 0, 100));
        Settle();

        SensorOf(a).Ping();
        for (var tick = 1; tick <= 24; tick++)
        {
            Tick(.1f);
            if (tick == removePingerAfterTick) _zone.Entities.Remove(a);
            Near(tick <= 3 ? 0f : .21213f, Track(c, t));
            Assert.Equal(tick == 4 ? 1 : 0, t.IncomingPaints.Count);
        }
    }

    // A sensor that stops executing leaves no echo: killed (disabled) after the tick its wavefront crossed T, the
    // one return it posted is heard once; before that tick, there is nothing to hear and T is never painted.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void AStoppedIlluminatorLeavesNoEcho(int disableAfterTick)
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var c = NewShip(float3(30, 0, 140), "Ear", a.Faction);
        var t = NewShip(float3(0, 0, 100));
        Settle();

        SensorOf(a).Ping();
        var track = new List<float>();
        var painted = 0;
        for (var tick = 1; tick <= 14; tick++)
        {
            Tick(.1f);
            if (tick == disableAfterTick) GearOf(a).Enabled.Value = false;
            track.Add(Track(c, t));
            painted += t.IncomingPaints.Count;
        }

        if (disableAfterTick == 3)
        {
            Assert.Equal(1, painted);
            Assert.Equal(1, Enumerable.Range(1, track.Count - 1).Count(i => track[i] > track[i - 1]));
            Assert.All(track, value => Assert.True(value <= .21213f + 1e-3f));
            Near(.21213f, track.Last());
        }
        else
        {
            Assert.Equal(0, painted);
            Assert.All(track, value => Assert.Equal(0f, value));
        }
    }

    // The oval of Cassini: an ally hears the return while the illuminated range (A to T, 100) times its own (ally to
    // T) is under PingRange squared (300^2 = 90000). Inside it, the credit is at the geometric mean range.
    [Theory]
    [InlineData(30f, 140f)]    // Rr 50: 5000
    [InlineData(0f, 990f)]     // Rr 890: 89000
    [InlineData(0f, 1010f)]    // Rr 910: 91000, outside
    public void AnAllyHearsOnlyInsideTheBistaticOval(float allyX, float allyZ)
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(allyX, 0, allyZ), "Ear", a.Faction);
        Settle();
        var illuminated = 100f;
        var received = length(float2(allyX, allyZ - 100f));

        var (held, _) = SweepAndWatch(a, .1f);

        Assert.Equal(1, held[t].Sum(h => h.Length));
        Near(.1f, Track(a, t));
        Near(illuminated * received < 300f * 300f ? .003f * sqrt(illuminated * received) : 0f, Track(c, t));
    }

    // A vapour cloud thins both legs of the echo (ruling vapour-cloud-obscuration); the pinger's paint on the
    // subject is published whatever the cloud (map R3). A at the origin, T 100 ahead, C at (60, 0, 100).
    // Leg 0: an opaque cloud on A to T. Leg 1: an opaque cloud on C to T. Leg 2: a half-opaque cloud on A to T.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CloudsThinBothLegsOfTheEcho(int leg)
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(60, 0, 100), "Ear", a.Faction);
        var at = leg == 1 ? float2(30, 100) : float2(0, 50);
        _zone.Vent(new VapourCloud { Body = new KinematicBody { Position = at }, Radius = 10f, Opacity = leg == 2 ? .5f : 1f, Lifetime = 1e7f });
        Settle();

        SensorOf(a).Ping();
        var painted = 0;
        float illuminatedLeg = 0f, receiveLeg = 0f;
        for (var tick = 1; tick <= 24; tick++)
        {
            Tick(.1f);
            painted += t.IncomingPaints.Count;
            if (tick == 3) illuminatedLeg = _zone.Obscuration(a.Position.xz, t.Position.xz);
            if (tick == 4) receiveLeg = _zone.Obscuration(c.Position.xz, t.Position.xz);
        }

        Assert.Equal(1, painted);
        if (leg == 0)
        {
            Near(0f, Track(a, t));
            Near(0f, Track(c, t));
        }
        else if (leg == 1)
        {
            Near(.1f, Track(a, t));
            Near(0f, Track(c, t));
        }
        else
        {
            Assert.InRange(illuminatedLeg, .01f, .99f); // fixture: the cloud really thins the leg
            Assert.Equal(1f, receiveLeg, 3);
            Near(.003f * sqrt(100f * 60f) * sqrt(illuminatedLeg * receiveLeg), Track(c, t));
        }
    }

    // The front has passed S, and S was abeam of a narrow beam: it joined the swept set uncredited. S then drifts
    // into the beam behind the front; that is not a crossing.
    [Fact]
    public void ADriftIntoTheBeamBehindTheFrontIsNotACrossing()
    {
        var a = NewShip(float3(0, 0, 0), "Beam90");
        a.Direction = float2(1, 0);
        var s = NewShip(float3(0, 0, 100));
        Settle();

        SensorOf(a).Ping();
        var painted = 0;
        for (var tick = 1; tick <= 24; tick++)
        {
            Tick(.1f);
            painted += s.IncomingPaints.Count;
            if (tick == 3)
            {
                Assert.True(SensorOf(a).PingRadius > 100f, "fixture: the front has passed S");
                s.Position = float3(100, 0, 0);
            }
        }

        Assert.Equal(0, painted);
        Assert.Equal(0f, Track(a, s));
    }

    [Fact]
    public void AShipArrivingAfterThePingIsNotSwept()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        Settle();
        SensorOf(a).Ping();
        for (var tick = 1; tick <= 24; tick++) Tick(.1f);
        Assert.True(SensorOf(a).PingRadius >= 299f, "fixture: the ping has run its course");

        var arrival = NewShip(float3(0, 0, 100));
        var painted = 0;
        for (var tick = 1; tick <= 5; tick++)
        {
            Tick(.1f);
            painted += arrival.IncomingPaints.Count;
        }

        Assert.Equal(0, painted);
        Assert.Equal(0f, Track(a, arrival));
    }

    // The swept set is per ping: a second ping crosses the same ship again, paints it again and credits again.
    [Fact]
    public void ASecondPingCrossesTheSameShipAgain()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        Settle();
        var painted = 0;
        for (var ping = 1; ping <= 2; ping++)
        {
            SensorOf(a).Ping();
            for (var tick = 1; tick <= 45; tick++) // the ping runs 21 ticks, the cooldown is 4 seconds
            {
                Tick(.1f);
                painted += t.IncomingPaints.Count;
            }
            Near(.1f * ping, Track(a, t));
        }
        Assert.Equal(2, painted);
    }

    // The ping's last step reaches PingRange; a ship in that step's band (the front is at 292.4 before it and 300
    // after) is still swept: the step that ends the ping sweeps.
    [Fact]
    public void AShipInTheEndingStepsBandIsStillSwept()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var s = NewShip(float3(0, 0, 295));
        Settle();
        SensorOf(a).Ping();
        var paints = 0;
        for (var tick = 1; tick <= 26; tick++) { Tick(.1f); paints += s.IncomingPaints.Count; }
        Assert.Equal(1, paints);
        Near(5 * 2 * .0001f * 295, Track(a, s));
    }

    // An ally holding two returns off one subject (two pingers on one allied ship) credits each: 2 x 5 x 3 x .0002 x
    // sqrt(100 x 50).
    [Fact]
    public void AnAllyCreditsEveryReturnItHolds()
    {
        var a = NewShip(float3(0, 0, 0), "Omni", hull: "Twin", sensor2: "Omni");
        var c = NewShip(float3(30, 0, 140), "Ear", a.Faction);
        var t = NewShip(float3(0, 0, 100));
        Settle();
        foreach (var s in a.GetBehaviors<Sensor>()) s.Ping();
        for (var tick = 1; tick <= 12; tick++) Tick(.1f);
        Near(2 * 5 * 3 * .0002f * sqrt(100f * 50f), Track(c, t));
    }

    // Ticks until the pinger has credited the subject (the crossing tick, on which the returns are posted to allies).
    private void TickToTheCrossing(Ship pinger, Ship subject)
    {
        for (var tick = 1; tick <= 40 && Track(pinger, subject) == 0f; tick++) Tick(.1f);
        Assert.True(Track(pinger, subject) > 0f);
    }

    // A return posted to an inactive ally is dropped: the ally, active again, hears nothing of that ping.
    [Fact]
    public void AnInactiveAllyIsPostedNoReturn()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(30, 0, 140), "Ear", a.Faction);
        Settle();
        c.Deactivate();
        SensorOf(a).Ping();
        TickToTheCrossing(a, t);
        c.Activate();
        Tick(.1f);
        Assert.Equal(0f, Track(c, t));
    }

    // Deactivating a ship clears the returns it holds: readmitted, it hears nothing of a ping it was docked through.
    [Fact]
    public void ADeactivatedAllyKeepsNoReturnsForItsNextAdmission()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        var c = NewShip(float3(30, 0, 140), "Ear", a.Faction);
        Settle();
        SensorOf(a).Ping();
        TickToTheCrossing(a, t); // the crossing tick posted a return into c
        c.Deactivate();
        c.Activate();
        Tick(.1f);
        Assert.Equal(0f, Track(c, t));
    }

    // A ping belongs to the zone it began in: a pinger that moves to another zone mid-ping ends it, and the ships of
    // the new zone inside the radius it had reached are not swept.
    [Fact]
    public void APingEndsWhenThePingerLeavesItsZone()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var z2 = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Zone 2", Owner = null }, null);
        var near = NewShip(float3(0, 0, 50), zone: z2);
        var far = NewShip(float3(0, 0, 250), zone: z2);
        Settle();
        SensorOf(a).Ping();
        for (var tick = 1; tick <= 3; tick++) Tick(.1f);
        Assert.True(SensorOf(a).PingRadius > 50f); // the front had passed 50 m by the move
        a.Deactivate(); _zone.Entities.Remove(a); a.Zone = z2; z2.Admit(a, false);
        var paints = 0;
        for (var tick = 1; tick <= 24; tick++) { z2.Update(.1f); paints += near.IncomingPaints.Count + far.IncomingPaints.Count; }
        Assert.Equal(0, paints);
        Assert.Equal(0f, Track(a, near));
        Assert.Equal(0f, Track(a, far));
    }

    // The swept set is the zone members the front has crossed: a subject that leaves after its crossing and returns
    // while the front is still past it is crossed again, as a ship arriving inside the front is.
    [Fact]
    public void ASubjectThatLeavesAndReturnsIsCrossedAgain()
    {
        var a = NewShip(float3(0, 0, 0), "Omni");
        var t = NewShip(float3(0, 0, 100));
        Settle();
        SensorOf(a).Ping();
        var paints = 0;
        for (var tick = 1; tick <= 30; tick++)
        {
            if (tick == 5) { _zone.Entities.Remove(t); t.Deactivate(); }
            if (tick == 8) { _zone.Entities.Add(t); t.Activate(); }
            Tick(.1f);
            paints += t.IncomingPaints.Count;
        }
        Assert.Equal(2, paints);
        Near(.1f, Track(a, t));
    }

    private static MessagePackSerializerOptions WireOptions => CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);

    // BeamWidth is slot 12 of the sensor record, the fifth union arm of BehaviorData, and the JSON property
    // beamWidth. Read as bytes, so a renumbered key or a renamed property cannot pass because writer and reader move together.
    [Fact]
    public void TheBeamWidthIsKey12OfUnion5()
    {
        var data = new SensorData { BeamWidth = Const(90) };
        var bytes = MessagePackSerializer.Serialize<BehaviorData>(data, WireOptions);

        var reader = new MessagePackReader(bytes);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(5, reader.ReadInt32());
        Assert.Equal(14, reader.ReadArrayHeader());
        for (var slot = 0; slot < 12; slot++) reader.Skip();
        Assert.Equal(90f, MessagePackSerializer.Deserialize<PerformanceStat>(ref reader, WireOptions).Min);

        var json = JObject.Parse(JsonConvert.SerializeObject(data));
        Assert.Equal(90f, json["beamWidth"].ToObject<PerformanceStat>().Min);
    }
}
