using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

// Visibility cut 3 (rulings visibility-per-capability-reveal, quiet-gear-identifiable-hull-layout-known,
// vapour-cloud-obscuration, ally-echo-bistatic-oval): an observer records what each ship's items have shown it, item
// by item with a bearing, and derives the contact's level from its track and those reveals. Every fixture drives
// the production path: real ships with real guns, thrusters, radiators and launchers, ticking inside Zone.Update.
// The numbers a rule is a function of are authored here and the expectations are their arithmetic. The observer's
// one passive sensor has sensitivity 2 and a flat curve, so a signature of strength S at planar distance d settles
// its info at S x 2 / (d x TargetInfoDecay), and is revealed exactly when that exceeds TargetDetectionInfoThreshold
// (.1). Signatures decay at once (VisibilityDecay 100), so an act is a single tick's light and builds no track.
// Subjects are added before the observer: they update first, so the observer senses the light on the tick it is made.
public sealed class ContactRevealTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-contact-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private CultCache _openCache;
    private ItemManager _items;
    private Zone _zone;
    private CultRecordRef<Faction> _maker;
    private int _nextLot = 1000;

    public ContactRevealTests()
    {
        Directory.CreateDirectory(_root);
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
            var solid = new Shape(5, 5);
            foreach (var cell in solid.AllCoordinates) solid[cell] = true;
            cache.Upsert(new HullData
            {
                Name = "Rack", HullType = HullType.Ship, Shape = solid, Durability = 1000000, Mass = 1000,
                Hardpoints =
                {
                    new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() },
                    new HardpointData { Type = HardpointType.Sensors, Position = new int2(1, 0), Shape = new Shape() },
                    new HardpointData { Type = HardpointType.Sensors, Position = new int2(2, 0), Shape = new Shape() }
                }
            });
            cache.Upsert(Eye("Eye", sensitivity: 2f));
            cache.Upsert(Eye("Ear", sensitivity: 4f));
            cache.Upsert(Tool("Reactor", new ReactorData { Charge = Const(1000), Efficiency = Const(1), OverloadEfficiency = Const(1), ThrottlingFactor = Const(2) }));
            cache.Upsert(Tool("Targeting", new TargetingSystemData { Accuracy = Const(1), Resolution = Const(1000f), Precision = Const(1000f), Tracking = Const(1000000f) }));
            cache.Upsert(Tool("Thruster", new ThrusterData { Thrust = Const(100), Visibility = Const(10), Heat = Const(0), EnergyUsage = Const(10) }));
            cache.Upsert(Tool("Radiator", new RadiatorData
            {
                Emissivity = Const(3), PumpedHeat = Const(0), TemperatureFloor = 0, WasteHeat = Const(0), EnergyUsage = Const(0), ThermalMass = Const(1000)
            }));
            var gun = new InstantWeaponData { Count = Const(1), BurstTime = Const(0), Cooldown = Const(1) };
            gun.Damage = Const(100);
            gun.Range = Const(40);
            gun.MinRange = Const(0);
            gun.Velocity = Const(0);
            gun.Spread = Const(0);
            gun.DamageSpread = Const(0);
            gun.Penetration = Const(0);
            gun.Energy = Const(50);
            gun.Heat = Const(0);
            gun.Visibility = Const(5);
            gun.MagazineSize = 5;
            gun.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };
            cache.Upsert(new WeaponItemData
            {
                Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
                Behaviors = { gun }
            });
            cache.Upsert(new GearData
            {
                Name = "Locker", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Behaviors =
                {
                    new LauncherData
                    {
                        Damage = Const(0), Range = Const(1000), MinRange = Const(0), Velocity = Const(0),
                        Spread = Const(0), DamageSpread = Const(0), Penetration = Const(0), Count = Const(1),
                        BurstTime = Const(0), Cooldown = Const(1000),
                        DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
                        LockSpeed = Const(.6f), SensorImpact = Const(1), LockAngle = Const(30), DirectionImpact = Const(1), Decay = Const(.1f)
                    }
                }
            });
            cache.Upsert(new ConsumableItemData
            {
                Name = "Pinger", Duration = 30f, Shape = new Shape(),
                Behaviors = { SensorBehavior(sensitivity: 2f) }
            });
            cache.FlushAsync().Wait();
        }

        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        for (var lot = 1000; lot < 2000; lot++) ledger.Lots[lot] = new Lot { Origin = new Attributed(), Quality = 1 };
        _items = new ItemManager(_openCache, ledger, new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f,
            TargetArmorInfoThreshold = .2f,
            TargetGearInfoThreshold = .8f,
            TargetInfoDecay = .5f,
            VisibilityDecay = 100f,
            BearingMemory = 2f,
            FiringArc = 170,
            CommitHorizon = .5f,
            SchematicCellSize = 2f,
            UnaidedAccuracy = .05f,
            UnaidedTracking = 10f,
            UnaidedPrecision = 1000f,
            AgentMinHitProbability = 0f,
            BeamResolveInterval = .1f,
            HeatRadiationMultiplier = 0f, // a hull radiates nothing unless a test says so
            HeatRadiationExponent = 1f
        }, _ => { });
        _maker = _openCache.RefOf(_openCache.GetByName<Faction>("Maker"));
        _zone = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
    }

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Const(float v) => new PerformanceStat { Min = v, Max = v };

    private static SensorData SensorBehavior(float sensitivity) => new SensorData
    {
        Sensitivity = Const(sensitivity),
        SensitivityCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }, // the same gain at every bearing
        PingBoost = Const(.0001f), PingEnergy = Const(0), PingVisibility = Const(5), PingRange = Const(300), PingCooldown = Const(4)
    };

    private static GearData Eye(string name, float sensitivity) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors = { SensorBehavior(sensitivity) }
    };

    private static GearData Tool(string name, BehaviorData behavior) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors = { behavior }
    };

    private EquippableItem Make(string name) => new EquippableItem
    {
        Data = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<ItemData>(name)), Durability = 10, Lot = _nextLot++
    };

    // A ship at `position` with `mounts` on its sensor hardpoints in order, the tools on free cells and a thruster
    // on the centre column. A ship with a gun also gets the reactor and targeting a gun needs to fire.
    private Ship Add(float3 position, string[] mounts = null, string[] tools = null, bool thruster = false)
    {
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>("Rack"));
        var ship = new Ship(_items, _zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = _nextLot++ }, new EntitySettings())
            { Faction = new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
        var cell = 0;
        foreach (var mount in mounts ?? new string[0]) Assert.True(ship.TryEquip(Make(mount), new int2(cell++, 0)));
        if (thruster) Assert.True(ship.TryEquip(Make("Thruster"), new int2(2, 1)));
        foreach (var name in (tools ?? new string[0]).Concat(mounts != null && mounts.Contains("Gun") ? new[] { "Targeting", "Reactor" } : new string[0]))
        {
            var item = Make(name);
            Assert.True(ship.TryFindSpace(item, out var at));
            Assert.True(ship.TryEquip(item, at));
        }
        _zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        ship.Aim = float3(0, 0, 1);
        _zone.Update(0f); // brings the gear online
        return ship;
    }

    private void Tick(float dt) => _zone.Update(dt);

    private static EquippedItem[] Guns(Entity e) => e.Equipment.Where(i => i.Behaviors.Any(b => b is InstantWeapon && !(b is LockWeapon))).ToArray();

    // Fires the given gun and ticks until its light shows; returns the zone clock of that tick.
    private float Fire(Ship ship, EquippedItem item)
    {
        var gun = (InstantWeapon) item.Behaviors.Single(b => b is InstantWeapon);
        var capacitor = (InputCapacitor) typeof(InstantWeapon).GetField("_capacitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gun);
        capacitor.AddCharge(1000f);
        ship.SetTarget(TargetRef.None);
        gun.Activate();
        for (var i = 0; i < 10; i++)
        {
            Tick(.1f);
            if (ship.VisibilitySources.ContainsKey(gun)) return _zone.Time;
        }
        throw new Exception("fixture: the gun fires");
    }

    private static float Track(Entity observer, Entity subject) =>
        observer.EntityInfoGathered.TryGetValue(subject, out var info) ? info : 0f;

    private static void AssertBearing(float x, float y, float2 bearing)
    {
        Assert.Equal(x, bearing.x, 4);
        Assert.Equal(y, bearing.y, 4);
    }

    private static VapourCloud Hand(Zone zone, float2 at, float radius, float opacity)
    {
        var cloud = new VapourCloud { Body = new KinematicBody { Position = at }, Radius = radius, Opacity = opacity, Lifetime = 1000f };
        zone.Vent(cloud);
        return cloud;
    }

    // ---- AFiredGunIsRevealedWithItsBearing ----

    [Fact]
    public void AFiredGunIsRevealedWithItsBearing()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Assert.Empty(o.RevealsOf(e)); // nothing shown yet

        var at = Fire(e, guns[0]);

        var reveal = Assert.Single(o.RevealsOf(e));
        Assert.Same(guns[0], reveal.Item);
        Assert.Equal(SignatureChannel.LaunchFlash, reveal.Channel);
        AssertBearing(.6f, .8f, reveal.Bearing); // normalize((E - O).xz)
        Assert.Equal(at, reveal.Time);
        Assert.DoesNotContain(o.RevealsOf(e), r => r.Item == guns[1]); // the unfired second gun
        Assert.Empty(e.RevealsOf(o)); // a ship is not told what it shows
    }

    // ---- AnActAboveTheReachRevealsNothing ----

    // Info settles at S x 2 / (d k): with S = 5 the act is revealed inside d = 100 / k and not beyond it, whatever k is.
    [Theory]
    [InlineData(.5f, 190f, true)]
    [InlineData(.5f, 210f, false)]
    [InlineData(.25f, 390f, true)]
    [InlineData(.25f, 410f, false)]
    public void AnActAboveTheReachRevealsNothing(float infoDecay, float distance, bool revealed)
    {
        _items.GameplaySettings.TargetInfoDecay = infoDecay;
        var e = Add(float3(0, 0, distance), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });

        Fire(e, Guns(e)[0]);

        Assert.Equal(revealed ? 1 : 0, o.RevealsOf(e).Count());
    }

    // ---- AFogBankHidesTheAct ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFogBankHidesTheAct(bool fog)
    {
        var e = Add(float3(0, 0, 150), new[] { "Gun" }); // 5 x 2 / (150 x .5) = .133 clear
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        if (fog) Hand(_zone, float2(0, 75), 5f, .6f); // a bank on the sight line passes .4 of the light: .053

        Fire(e, Guns(e)[0]);

        Assert.Equal(fog ? 0 : 1, o.RevealsOf(e).Count());
    }

    // ---- ABurnRevealsTheDriveAndOpenRadiatorsRevealThemselves ----

    [Fact]
    public void ABurnRevealsTheDriveAndOpenRadiatorsRevealThemselves()
    {
        // The hull radiates .25 a border cell at any temperature; the radiator adds .25 x 3.
        _items.GameplaySettings.HeatRadiationMultiplier = .25f;
        _items.GameplaySettings.HeatRadiationExponent = 0f;
        var burner = Add(float3(0, 0, 100), tools: new[] { "Reactor", "Targeting" }, thruster: true); // 10 x 4 / 100 = .4
        var glow = Add(float3(20, 0, 0), tools: new[] { "Reactor", "Radiator" });                               // .75 x 4 / 20 = .15
        var bare = Add(float3(0, 0, -10));                                                              // the hull alone, brighter than any act
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var thrusterItem = burner.Equipment.Single(i => i.Behaviors.Any(b => b is Thruster));
        thrusterItem.UpdatePerformance();

        burner.MovementDirection = float2(0, -1);
        Tick(.1f);

        var plume = Assert.Single(o.RevealsOf(burner));
        Assert.Same(thrusterItem, plume.Item);
        Assert.Equal(SignatureChannel.DrivePlume, plume.Channel);
        var radiator = Assert.Single(o.RevealsOf(glow));
        Assert.Same(glow.Equipment.Single(i => i.Behaviors.Any(b => b is Radiator)), radiator.Item);
        Assert.Equal(SignatureChannel.Thermal, radiator.Channel);
        Assert.True(bare.Visibility > 1f, "fixture: the bare hull is lit");
        Assert.Empty(o.RevealsOf(bare));
    }

    // ---- APaintRevealsTheEmitterEvenUnseen ----

    [Fact]
    public void APaintRevealsTheEmitterEvenUnseen()
    {
        var locker = Add(float3(0, 0, 200), new[] { "Locker" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        locker.SetIff(o, true);
        locker.EntityInfoGathered[o] = 1f;
        locker.SetTarget(o);
        locker.Aim = normalize(o.Position - locker.Position);

        Tick(.5f);
        Tick(.5f);

        Assert.Single(o.IncomingPaints); // fixture: the lock painted it
        Assert.Equal(0f, Track(o, locker)); // unseen
        var reveal = Assert.Single(o.RevealsOf(locker));
        Assert.Same(locker.Equipment.Single(i => i.Behaviors.Any(b => b is LockWeapon)), reveal.Item);
        Assert.Equal(SignatureChannel.Emission, reveal.Channel);
        AssertBearing(0f, 1f, reveal.Bearing);
        Assert.False(o.ContactOf(locker).Tracked);
    }

    [Fact]
    public void AConsumableBorneEmitterRecordsNothing()
    {
        var p = Add(float3(0, 0, 100));
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var data = _openCache.GetByName<ConsumableItemData>("Pinger");
        p.ActivateConsumable(Assert.IsType<ConsumableItem>(_items.CreateInstance(_items.CreateLot(data, _maker, .5f))));
        var sensor = (Sensor) p.FindActiveConsumable(data).Behaviors.Single(b => b is Sensor);
        Tick(.01f);
        sensor.Ping();

        var paints = 0;
        for (var tick = 0; tick < 30; tick++)
        {
            Tick(.1f);
            paints += o.IncomingPaints.Count;
        }

        Assert.Equal(1, paints); // fixture: the sweep painted the observer
        Assert.Empty(o.RevealsOf(p));
    }

    // ---- ALaunchGivesABearingNotATrack ----

    [Fact]
    public void AnActGivesABearingNotATrack()
    {
        var e = Add(float3(0, 0, 150), new[] { "Gun" });  // 5 x 2 / (150 x .5) = .133: revealed, but one tick of light builds no track
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        var at = Fire(e, gun);
        e.Position = float3(150, 0, 0); // it slips away: the reveal keeps the bearing of the act

        var seen = o.ContactOf(e);
        Assert.True(Track(o, e) < .1f, "fixture: no track");
        Assert.True(seen.Detected);
        Assert.True(seen.Classified);
        Assert.False(seen.Tracked);
        Assert.False(seen.Identified);
        AssertBearing(0f, 1f, seen.Bearing);

        // Younger than BearingMemory (2 s): still Detected. Older: not, and the gun is still known.
        while (_zone.Time - at < 1.5f) Tick(.1f);
        Assert.True(o.ContactOf(e).Detected);
        while (_zone.Time - at < 2.5f) Tick(.1f);
        var later = o.ContactOf(e);
        Assert.False(later.Detected);
        Assert.False(later.Classified);
        Assert.False(later.Tracked);
        Assert.Same(gun, Assert.Single(o.RevealsOf(e)).Item);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(5f)]
    public void TheBearingMemoryIsAGameplaySetting(float memory)
    {
        _items.GameplaySettings.BearingMemory = memory;
        var e = Add(float3(0, 0, 150), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var at = Fire(e, Guns(e)[0]);

        while (_zone.Time - at < memory - .3f) Tick(.1f);
        Assert.True(o.ContactOf(e).Detected);
        while (_zone.Time - at < memory + .3f) Tick(.1f);
        Assert.False(o.ContactOf(e).Detected);
    }

    // ---- ContactLevels ----

    // E fires from (30, 40) and moves to (-50, 0). Tracked is exactly membership of VisibleEntities, Identified
    // exactly a track at or above TargetGearInfoThreshold; the bearing is the true one while tracked and the act's
    // while the ship is only remembered.
    [Theory]
    [InlineData(.05f, false, false)]
    [InlineData(.1f, false, false)]
    [InlineData(.5f, true, false)]
    [InlineData(.79f, true, false)]
    [InlineData(.8f, true, true)]
    [InlineData(1f, true, true)]
    public void ContactLevels(float info, bool tracked, bool identified)
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        Fire(e, Guns(e)[0]);
        e.Position = float3(-50, 0, 0);

        o.EntityInfoGathered[e] = info;

        var contact = o.ContactOf(e);
        Assert.Equal(o.VisibleEntities.Contains(e), contact.Tracked);
        Assert.Equal(tracked, contact.Tracked);
        Assert.Equal(identified, contact.Identified);
        Assert.True(contact.Detected);
        Assert.True(contact.Classified);
        Assert.Equal(info, contact.Info);
        if (tracked) AssertBearing(-1f, 0f, contact.Bearing);
        else AssertBearing(.6f, .8f, contact.Bearing);
    }

    [Fact]
    public void AShipNothingHasShownIsNotDetected()
    {
        var e = Add(float3(0, 0, 150));
        var o = Add(float3(0, 0, 0), new[] { "Eye" });

        var contact = o.ContactOf(e);

        Assert.False(contact.Detected);
        Assert.False(contact.Tracked);
        Assert.False(contact.Classified);
        Assert.False(contact.Identified);
    }

    // ---- RevealsLeaveWithTheSubjectAndTheObserver ----

    [Fact]
    public void RevealsLeaveWithTheSubject()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        Assert.Equal(2, o.RevealsOf(e).Count());

        guns[0].EquippableItem.Durability = 0f; // destroyed

        Assert.Same(guns[1], Assert.Single(o.RevealsOf(e)).Item);

        _zone.Entities.Remove(e);
        _zone.Entities.Add(e);

        Assert.Empty(o.RevealsOf(e)); // gone with the subject, and not back with it
    }

    [Fact]
    public void RevealsLeaveWithTheObserver()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        Fire(e, Guns(e)[0]);
        Assert.Single(o.RevealsOf(e));

        o.Deactivate();

        Assert.Empty(o.RevealsOf(e));
    }

    // ---- The one rule for info per signature ----

    [Fact]
    public void PassiveInfoPerSignatureAveragesTheActiveSensors()
    {
        var one = Add(float3(0, 0, 0), new[] { "Eye" });
        var two = Add(float3(500, 0, 0), new[] { "Eye", "Ear" });
        var none = Add(float3(1000, 0, 0));

        // sum(s) / (d n k): 2 / (100 x .5) with one sensor, (2 + 4) / (100 x 2 x .5) with two.
        Assert.Equal(.04f, one.PassiveInfoPerSignature(float2(0, 100)), 5);
        Assert.Equal(.06f, two.PassiveInfoPerSignature(float2(500, 100)), 5);
        Assert.Equal(0f, none.PassiveInfoPerSignature(float2(1000, 100)));
    }

    // ---- SenseCostIsBounded ----

    [Fact]
    public void SenseAllocatesNothingOnceWarm()
    {
        var gunItems = new List<EquippedItem>();
        for (var i = 0; i < 19; i++)
        {
            var ship = Add(float3(i * 3, 0, 10 + i), new[] { "Gun" });
            var item = Guns(ship)[0];
            for (var s = 0; s < 10; s++) ship.SetSignature(new object(), SignatureChannel.Reflection, 1000f, item);
            gunItems.Add(item);
        }
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var sense = (Action) Delegate.CreateDelegate(typeof(Action), o, typeof(Entity).GetMethod("Sense", BindingFlags.Instance | BindingFlags.NonPublic));
        for (var i = 0; i < 10; i++) sense();
        Assert.Equal(19, _zone.Entities.Where(x => x != o).Count(x => o.RevealsOf(x).Any())); // fixture: every ship is revealed

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 600; i++) sense();
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0L, after - before);
    }
}
