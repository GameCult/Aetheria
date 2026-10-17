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
using UniRx;
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
// The observer senses after every ship has updated, so the zone's list order never decides when an act is revealed.
public sealed partial class ContactRevealTests : IDisposable
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
            // Layout-aim: a hull whose mounts are bigger than what is fitted to them. The reactor mount covers the 2x2 block at
            // (3,3), off the origin and against the hull's far edge, while the reactor is the one cell at (3,3), so the mount's
            // centre (3.5, 3.5) is not the reactor's (3, 3) and the two price a shot apart.
            var bayBlock = new Shape(2, 2);
            foreach (var cell in bayBlock.AllCoordinates) bayBlock[cell] = true;
            var bayHull = new Shape(5, 5);
            foreach (var cell in bayHull.AllCoordinates) bayHull[cell] = true;
            cache.Upsert(new HullData
            {
                Name = "Bay", HullType = HullType.Ship, Shape = bayHull, Durability = 1000000, Mass = 1000,
                Hardpoints =
                {
                    new HardpointData { Type = HardpointType.Reactor, Position = new int2(3, 3), Shape = bayBlock },
                    new HardpointData { Type = HardpointType.Shield, Position = new int2(3, 0), Shape = new Shape() },
                    new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 3), Shape = new Shape() }
                }
            });
            // Layout-aim: a hull off the diagonal with two mounts of one type. The first Sensors mount covers (0,0) and (1,0), the
            // second (2,3) and (3,3): a transposed position gives another cell, and a rule that matches mounts by type cannot
            // tell the two apart.
            var wingMount = new Shape(2, 1);
            foreach (var cell in wingMount.AllCoordinates) wingMount[cell] = true;
            var wingHull = new Shape(5, 5);
            foreach (var cell in wingHull.AllCoordinates) wingHull[cell] = true;
            cache.Upsert(new HullData
            {
                Name = "Wing", HullType = HullType.Ship, Shape = wingHull, Durability = 1000000, Mass = 1000,
                Hardpoints =
                {
                    new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = wingMount },
                    new HardpointData { Type = HardpointType.Sensors, Position = new int2(2, 3), Shape = wingMount }
                }
            });
            var bayReactor = Tool("BayReactor", new ReactorData { Charge = Const(1000), Efficiency = Const(1), OverloadEfficiency = Const(1), ThrottlingFactor = Const(2) });
            bayReactor.Hardpoint = HardpointType.Reactor;
            cache.Upsert(bayReactor);
            cache.Upsert(Eye("Eye", sensitivity: 2f));
            cache.Upsert(Eye("Ear", sensitivity: 4f));
            // Narrow beams (10 degrees, facing +z) so a hearer off the beam is never painted: the ping is heard at its
            // PingRange (300) over the intercept factor.
            cache.Upsert(Eye("Shout", sensitivity: 2f, interceptFactor: 2f, beamWidth: 10f));
            cache.Upsert(Eye("Roar", sensitivity: 2f, interceptFactor: .5f, beamWidth: 10f));
            // The catalog's sensor alpha (AetherDb sensor-stat-set): .5 to 2 over one Quality term on the equipped lot's
            // emission-discipline role, exponent log2(3) so a middling part (.5) reads 1.
            var disciplined = Eye("Disciplined", sensitivity: 2f);
            disciplined.Roles.Add(new ItemRole { Name = "emission-discipline" });
            disciplined.Behaviors.OfType<SensorData>().Single().InterceptFactor = DisciplineStat();
            cache.Upsert(disciplined);
            var disciplinedScanner = Tool("DisciplinedScanner", ScannerBehavior(range: 200f, scanTime: 2f));
            disciplinedScanner.Roles.Add(new ItemRole { Name = "emission-discipline" });
            disciplinedScanner.Behaviors.OfType<ScannerData>().Single().InterceptFactor = DisciplineStat();
            cache.Upsert(disciplinedScanner);
            cache.Upsert(Listener("Listener", 3f));
            cache.Upsert(new ConsumableItemData { Name = "Earpiece", Duration = 30f, Shape = new Shape(), Behaviors = { new ListenerData { Multiple = Const(3f) } } });
            cache.Upsert(Listener("Wide", 5f));
            cache.Upsert(Listener("Narrow", 1.5f));
            cache.Upsert(Listener("Hush", .5f));
            cache.Upsert(new ConsumableItemData { Name = "HushEarpiece", Duration = 30f, Shape = new Shape(), Behaviors = { new ListenerData { Multiple = Const(.5f) } } });
            cache.Upsert(new ConsumableItemData { Name = "WideEarpiece", Duration = 30f, Shape = new Shape(), Behaviors = { new ListenerData { Multiple = Const(5f) } } });
            cache.Upsert(new ConsumableItemData { Name = "NarrowEarpiece", Duration = 30f, Shape = new Shape(), Behaviors = { new ListenerData { Multiple = Const(1.5f) } } });
            cache.Upsert(Tool("Reactor", new ReactorData { Charge = Const(1000), Efficiency = Const(1), OverloadEfficiency = Const(1), ThrottlingFactor = Const(2) }));
            cache.Upsert(Tool("Targeting", new TargetingSystemData { Accuracy = Const(1), Resolution = Const(1000f), Precision = Const(1000f), Tracking = Const(1000000f) }));
            cache.Upsert(Tool("Thruster", new ThrusterData { Thrust = Const(100), Visibility = Const(10), Heat = Const(0), EnergyUsage = Const(10) }));
            cache.Upsert(Tool("Radiator", new RadiatorData
            {
                Emissivity = Const(3), PumpedHeat = Const(0), TemperatureFloor = 0, WasteHeat = Const(0), EnergyUsage = Const(0), ThermalMass = Const(1000)
            }));
            // Launch suppressors (aetheria-ew): one solid cell each, so the heat they pay has a cell to land in. Rated 400.
            cache.Upsert(Suppressor("Suppressor", factor: .1f, heatPerUnit: 2f));
            cache.Upsert(Suppressor("SuppressorCold", factor: .1f, heatPerUnit: 0f));
            cache.Upsert(Suppressor("SuppressorOpen", factor: 1f, heatPerUnit: 2f));
            cache.Upsert(Suppressor("SuppressorHalf", factor: .5f, heatPerUnit: 1f));
            cache.Upsert(Suppressor("SuppressorFifth", factor: .2f, heatPerUnit: 10f));
            cache.Upsert(Suppressor("SuppressorBright", factor: 3f, heatPerUnit: 2f));
            cache.Upsert(Suppressor("SuppressorNegative", factor: -1f, heatPerUnit: 2f));
            var gun = Armed(new InstantWeaponData { Count = Const(1), BurstTime = Const(0), Cooldown = Const(1) }, 5);
            cache.Upsert(new WeaponItemData
            {
                Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
                Behaviors = { gun }
            });
            cache.Upsert(new WeaponItemData
            {
                Name = "Beam", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
                Behaviors = { Armed(new ConstantWeaponData(), 0) }
            });
            // Weapons authored so the rated figure is a function of every stat: a falling damage curve, a Range span, a
            // MinRange above 0, burst and charge spans (a stat with no terms resolves to its Max live).
            cache.Upsert(SpannedWeapon("SpanGun", Spanned(new InstantWeaponData { BurstTime = Const(0) })));
            cache.Upsert(SpannedWeapon("SpanCharged", Spanned(new ChargedWeaponData
            {
                BurstTime = Const(0), ChargeTime = Span(1, 3), ChargeEnergy = Const(1), ChargeHeat = Const(0), ChargeFiringDamageMultiplier = 2f
            })));
            cache.Upsert(SpannedWeapon("SpanBeam", Spanned(new ConstantWeaponData())));
            cache.Upsert(Locker("Locker", interceptFactor: null));
            cache.Upsert(Locker("Hushed", interceptFactor: 4f));
            // Scanners (cut inspection-scanner): range 200 and a two second scan; "QuietScanner" is heard at a quarter of it.
            cache.Upsert(Tool("Scanner", ScannerBehavior(range: 200f, scanTime: 2f)));
            cache.Upsert(Tool("QuietScanner", ScannerBehavior(range: 200f, scanTime: 2f, interceptFactor: 4f)));
            cache.Upsert(new ConsumableItemData
            {
                Name = "ScannerCharge", Duration = 30f, Shape = new Shape(),
                Behaviors = { ScannerBehavior(range: 200f, scanTime: 2f) }
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

    // The weapon fields every fixture weapon shares; a gun and a beam differ only in their behaviour type and magazine.
    private static WeaponData Armed(WeaponData weapon, int magazine)
    {
        weapon.Damage = Const(100);
        weapon.Range = Const(40);
        weapon.MinRange = Const(0);
        weapon.Velocity = Const(0);
        weapon.Spread = Const(0);
        weapon.DamageSpread = Const(0);
        weapon.Penetration = Const(0);
        weapon.Energy = Const(50);
        weapon.Heat = Const(0);
        weapon.Visibility = Const(5);
        weapon.MagazineSize = magazine;
        return weapon;
    }

    private static WeaponData Spanned(WeaponData weapon)
    {
        Armed(weapon, 5);
        weapon.Damage = Span(10, 100);
        weapon.MinRange = Span(0, 20);
        weapon.Range = Span(50, 120);
        weapon.EffectiveRange = Span(30, 60);
        weapon.DamageAtMaxRange = .2f;
        if (weapon is InstantWeaponData instant)
        {
            instant.Count = Span(1, 2);
            instant.Cooldown = Span(8, 2);
        }
        return weapon;
    }

    private static WeaponItemData SpannedWeapon(string name, WeaponData weapon) => new WeaponItemData
    {
        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
        Behaviors = { weapon }
    };

    private static GearData Suppressor(string name, float factor, float heatPerUnit)
    {
        var cell = new Shape(1, 1);
        cell[new int2(0, 0)] = true;
        return new GearData
        {
            Name = name, Hardpoint = HardpointType.Tool, Shape = cell, Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new LaunchSuppressorData { Factor = Const(factor), HeatPerUnit = Const(heatPerUnit), RatedTemperature = Const(400f) } }
        };
    }

    // A launcher with Range 1000, heard at that Range over its intercept factor while it paints.
    private static GearData Locker(string name, float? interceptFactor) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors =
        {
            new LauncherData
            {
                Damage = Const(0), Range = Const(1000), MinRange = Const(0), Velocity = Const(0),
                Spread = Const(0), DamageSpread = Const(0), Penetration = Const(0), Count = Const(1),
                BurstTime = Const(0), Cooldown = Const(1000),
                LockSpeed = Const(.6f), SensorImpact = Const(1), LockAngle = Const(30), DirectionImpact = Const(1), Decay = Const(.1f),
                InterceptFactor = interceptFactor.HasValue ? Const(interceptFactor.Value) : null
            },
            // A munition design carries one of each part (missile-stats); the reveal tests read none of them.
            new MunitionHullData(), new MunitionThrusterData(), new MunitionSeekerData(), new MunitionControlData(), new MunitionFuelData()
        }
    };

    private static SensorData SensorBehavior(float sensitivity, float? interceptFactor = null, float? beamWidth = null) => new SensorData
    {
        Sensitivity = Const(sensitivity),
        SensitivityCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }, // the same gain at every bearing
        PingBoost = Const(.0001f), PingEnergy = Const(0), PingRange = Const(300), PingCooldown = Const(4),
        InterceptFactor = interceptFactor.HasValue ? Const(interceptFactor.Value) : null,
        BeamWidth = beamWidth.HasValue ? Const(beamWidth.Value) : null
    };

    private static GearData Eye(string name, float sensitivity, float? interceptFactor = null, float? beamWidth = null) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors = { SensorBehavior(sensitivity, interceptFactor, beamWidth) }
    };

    // The catalog's intercept factor (AetherDb sensor-stat-set and inspection-scanner): .5 to 2 over one Quality term on the
    // lot's emission-discipline, exponent log2(3).
    private static PerformanceStat DisciplineStat() => new PerformanceStat
    {
        Min = .5f, Max = 2f,
        Terms = { new StatTerm { Source = StatSource.Quality, Exponent = MathF.Log(3f) / MathF.Log(2f), Role = "emission-discipline" } }
    };

    private static GearData Tool(string name, BehaviorData behavior) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors = { behavior }
    };

    private EquippableItem Make(string name) => new EquippableItem
    {
        Data = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<ItemData>(name)), Durability = 10,
        Lot = _preparedLots.Remove(name, out var prepared) ? prepared : _nextLot++
    };

    // Lots a test built through the ItemManager for the next mount of that name, ahead of Add (a ship takes no gear once deployed).
    private readonly Dictionary<string, int> _preparedLots = new Dictionary<string, int>();

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
        _zone.Admit(ship, piloted: false);
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

    // ---- AFiredGunIsAimableAndItsTwinIsNot ----

    // Two guns of one design: firing one makes that gun, and only that gun, something the observer can select and
    // keep aimed at. The book is per item, not per design or per hardpoint type.
    [Fact]
    public void AFiredGunIsAimableAndItsTwinIsNot()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Assert.Same(guns[0].Data, guns[1].Data); // the same design
        o.SetTarget(e);
        Assert.False(FireControl.IsRevealed(o, guns[0]));
        Assert.False(o.TrySelectTargetItem(guns[0]));

        Fire(e, guns[0]);
        o.SetTarget(e);

        Assert.True(FireControl.IsRevealed(o, guns[0]));
        Assert.False(FireControl.IsRevealed(o, guns[1]));
        Assert.False(o.TrySelectTargetItem(guns[1]));
        Assert.True(o.TrySelectTargetItem(guns[0]));
        for (var i = 0; i < 300; i++) Tick(.1f);
        Assert.Same(guns[0], o.ResolvedTargetItem);
        Assert.False(FireControl.IsRevealed(o, guns[1]));
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
        var o = Add(float3(0, 0, 0)); // deaf: no active sensor, so the reveal is the paint's alone, never the lock's emission heard
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

    // Ruling consumable-signature-null-until-reveal: a consumable-hosted signature names no item (Item null, as
    // hull radiation does), so however bright and near it reveals nothing; the reveal cut leaves what it names to
    // follow-up reveal-names-consumable-signature.
    [Fact]
    public void ANullItemSignatureFromASourceThatIsNotTheHullRevealsNothing()
    {
        var e = Add(float3(0, 0, 20));
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var data = _openCache.GetByName<ConsumableItemData>("Pinger");
        e.ActivateConsumable(Assert.IsType<ConsumableItem>(_items.CreateInstance(_items.CreateLot(data, _maker, .5f))));
        var source = e.FindActiveConsumable(data).Behaviors.Single();
        e.SetSignature(source, SignatureChannel.Reflection, 1000f, null);

        Tick(.01f);

        Assert.True(e.VisibilitySources.ContainsKey(source), "fixture: the consumable's light is on");
        Assert.Empty(o.RevealsOf(e));
    }

    // A ping is the pinger's act on its beam, not light (R1): it adds nothing to the pinger's Visibility, so it raises
    // no observer's track of the pinger. The control run is the same scene without the Ping() call.
    [Fact]
    public void APingNoLongerBrightensThePinger()
    {
        var (pingedLight, pingedTrack) = RunPinger(ping: true);
        var (controlLight, controlTrack) = RunPinger(ping: false);

        Assert.Equal(controlLight, pingedLight);
        Assert.Equal(controlTrack, pingedTrack, 6);
    }

    // P is past the ping's radius from O; its Visibility is read on the call's own tick and on each tick after.
    private static (float[] visibility, float track) RunPinger(bool ping)
    {
        using var t = new ContactRevealTests();
        var p = t.Add(float3(0, 0, 1000), new[] { "Eye" });
        var o = t.Add(float3(0, 0, 0), new[] { "Eye" });
        var sensor = p.Equipment.SelectMany(i => i.Behaviors).OfType<Sensor>().Single();
        var fired = false;
        sensor.OnPingStart += () => fired = true;
        t.Tick(.1f); // a sensor's first tick opens its ping cooldown; Ping() is refused before it
        if (ping) sensor.Ping();
        Assert.Equal(ping, fired); // fixture: the ping started exactly when asked, so the run is not a control in disguise
        var light = new List<float> { p.Visibility };
        for (var tick = 0; tick < 10; tick++)
        {
            t.Tick(.1f);
            light.Add(p.Visibility);
        }
        return (light.ToArray(), Track(o, p));
    }

    // Two items revealed at different times: the contact's bearing, while untracked, is the freshest reveal's.
    [Fact]
    public void TheFreshestRevealGivesTheBearing()
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        e.Position = float3(-50, 0, 0);
        Fire(e, guns[1]);

        var contact = o.ContactOf(e);

        Assert.Equal(2, o.RevealsOf(e).Count());
        Assert.False(contact.Tracked);
        AssertBearing(-1f, 0f, contact.Bearing);
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

    // ---- A subject leaves ----

    private Action SenseOf(Entity observer) =>
        (Action) Delegate.CreateDelegate(typeof(Action), observer, typeof(Entity).GetMethod("Sense", BindingFlags.Instance | BindingFlags.NonPublic));

    // The ways a ship leaves a zone: removed by anything, the warp path (Deactivate, then Remove) and death.
    private static void Leave(Zone zone, Entity e, string how)
    {
        switch (how)
        {
            case "remove": zone.Remove(e); break;
            case "warp": e.Deactivate(); zone.Remove(e); break;
            case "death": e.Hull.Durability = 0f; e.HullDamage.OnNext(1f); break;
            default: throw new Exception("fixture: " + how);
        }
        Assert.False(zone.Entities.Contains(e), "fixture: the subject left the zone");
    }

    // Leaves the ship showing exactly `strength` of light: whatever its guns left glowing is put out first.
    private static void ShowOnly(Entity e, float strength, EquippedItem item)
    {
        foreach (var source in e.VisibilitySources.Keys.ToArray()) e.SetSignature(source, SignatureChannel.Reflection, 0f, item);
        e.SetSignature(new object(), SignatureChannel.Reflection, strength, item);
        Assert.Equal(strength, e.Visibility, 4);
    }

    private static float Freshest(Entity observer, Entity subject) => observer.RevealsOf(subject).Max(r => r.Time);

    // Ruling reveal-ends-on-perception: a ship leaving the zone does not by itself empty any book. Out of this
    // observer's sight (the light it showed at removal is below the observer's gate) its reveals stay, unrefreshed,
    // and the contact stays Detected on the freshest bearing, exactly until that bearing is BearingMemory old; then
    // the book drops the ship. A live quiet ship's old reveal is another matter: it stays in the book.
    [Fact]
    public void AnUnseenDepartureKeepsTheContactUntilTheBearingLapses()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var q = Add(float3(40, 0, 100), new[] { "Gun" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        Fire(q, Guns(q)[0]);
        var memory = _items.GameplaySettings.BearingMemory;
        var times = o.RevealsOf(e).ToDictionary(r => r.Item, r => r.Time);
        var freshest = times.Values.Max();
        var bearing = o.ContactOf(e).Bearing;
        ShowOnly(e, 1f, guns[0]);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: its last light is below o's gate");
        Assert.Equal(2, times.Count);

        _zone.Remove(e);
        Assert.Equal(2, o.RevealsOf(e).Count());

        var lapsed = false;
        var ticks = 0;
        while (!lapsed && ticks++ < 100)
        {
            Tick(.1f);
            var within = _zone.Time - freshest < memory;
            lapsed = !within;
            Assert.Equal(within, o.RevealsOf(e).Count() == 2); // exactly while the bearing is remembered
            Assert.Equal(within, o.ContactOf(e).Detected);
            Assert.Equal(within ? 1 : 0, RevealBookSubjects(o) - 1); // the quiet live ship's key stays either way
            if (!within) continue;
            var contact = o.ContactOf(e);
            Assert.True(contact.Classified && !contact.Tracked);
            AssertBearing(bearing.x, bearing.y, contact.Bearing);
            foreach (var reveal in o.RevealsOf(e)) Assert.Equal(times[reveal.Item], reveal.Time); // never refreshed
        }

        Assert.True(lapsed, "fixture: the bearing lapsed");
        Assert.Empty(o.RevealsOf(e));
        Assert.False(o.ContactOf(e).Detected);
        Assert.Equal(1, RevealBookSubjects(o)); // only the live ship's key

        for (var i = 0; i < 30; i++) Tick(.2f); // q, live and quiet, was revealed 6 s ago, far more than twice the memory
        Assert.Single(o.RevealsOf(q));
        Assert.False(o.ContactOf(q).Detected);
    }

    // The same departure seen: every way a ship leaves is one departure fact, judged when the observer next senses.
    // A strong ear (sensitivity 4) and a weak eye (2) at the same place, listed on either side of the ship, both hold
    // both reveals right after the removal; after the next tick only the observer whose gate the ship's light at
    // removal passes (signature 2 x info 8/d = .16 against .08) has let them go.
    [Theory]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    [InlineData("warp", false)]
    [InlineData("death", false)]
    [InlineData("death", true)]
    public void ASeenDepartureEndsEveryRevealOfTheSubject(string how, bool strongFirst)
    {
        Entity strong = null, weak = null;
        if (strongFirst) strong = Add(float3(-1, 0, 0), new[] { "Ear" });
        else weak = Add(float3(1, 0, 0), new[] { "Eye" });
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        if (strongFirst) weak = Add(float3(1, 0, 0), new[] { "Eye" });
        else strong = Add(float3(-1, 0, 0), new[] { "Ear" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        foreach (var observer in new[] { strong, weak }) Assert.Equal(2, observer.RevealsOf(e).Count());
        ShowOnly(e, 2f, guns[0]);
        var threshold = _items.GameplaySettings.TargetDetectionInfoThreshold;
        Assert.True(e.Visibility * strong.PassiveInfo(e.Position.xz, 1f) > threshold, "fixture: the ear passes the gate");
        Assert.True(e.Visibility * weak.PassiveInfo(e.Position.xz, 1f) <= threshold, "fixture: the eye fails it");

        Leave(_zone, e, how);

        Assert.Equal(2, strong.RevealsOf(e).Count()); // nothing ends at the removal itself
        Assert.Equal(2, weak.RevealsOf(e).Count());
        Tick(.1f);

        Assert.Empty(strong.RevealsOf(e));
        Assert.Equal(0, RevealBookSubjects(strong));
        Assert.False(strong.ContactOf(e).Detected);
        Assert.Equal(2, weak.RevealsOf(e).Count());
        Assert.True(weak.ContactOf(e).Detected);
    }

    // The gate at its edge, at one fixed distance: the light at removal a hair above the observer's threshold ends
    // the reveals, a hair below does not. The factor is the production gate's own: threshold over the observer's
    // passive info on the spot where the ship was.
    [Theory]
    [InlineData(1.01f, true)]
    [InlineData(.99f, false)]
    public void ADepartureEndsRevealsOnlyAboveTheLightActGate(float against, bool ended)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        Fire(e, Guns(e)[0]);
        Tick(.01f);
        Assert.Single(o.RevealsOf(e));
        var gate = _items.GameplaySettings.TargetDetectionInfoThreshold / o.PassiveInfo(e.Position.xz, 1f);
        ShowOnly(e, gate * against, Guns(e)[0]);

        _zone.Remove(e);
        Tick(.1f);

        Assert.Equal(ended ? 0 : 1, o.RevealsOf(e).Count());
    }

    // The departure records the subject's whole Visibility: no channel is excepted. The light at removal sits on each
    // channel in turn, a hair above the observer's gate, and ends the reveals whichever channel carries it.
    [Theory]
    [InlineData(SignatureChannel.Thermal)]
    [InlineData(SignatureChannel.DrivePlume)]
    [InlineData(SignatureChannel.Reflection)]
    [InlineData(SignatureChannel.LaunchFlash)]
    public void ADepartureCountsLightOnEveryChannel(SignatureChannel channel)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        Fire(e, Guns(e)[0]);
        Tick(.01f);
        Assert.Single(o.RevealsOf(e));
        var gate = _items.GameplaySettings.TargetDetectionInfoThreshold / o.PassiveInfo(e.Position.xz, 1f);
        foreach (var source in e.VisibilitySources.Keys.ToArray()) e.SetSignature(source, SignatureChannel.Reflection, 0f, Guns(e)[0]);
        e.SetSignature(new object(), channel, gate * 1.01f, Guns(e)[0]);

        _zone.Remove(e);
        Tick(.1f);

        Assert.Empty(o.RevealsOf(e));
    }

    // The hull's own radiation names no item, and the departure still counts it: the light at removal sits on an item-less
    // entry alone, a hair above the observer's gate (ends the reveals) or a hair below (ends nothing).
    [Theory]
    [InlineData(1.01f, true)]
    [InlineData(.99f, false)]
    public void ADepartureCountsTheHullsOwnLight(float against, bool ended)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        Fire(e, Guns(e)[0]);
        Tick(.01f);
        Assert.Single(o.RevealsOf(e));
        var gate = _items.GameplaySettings.TargetDetectionInfoThreshold / o.PassiveInfo(e.Position.xz, 1f);
        foreach (var source in e.VisibilitySources.Keys.ToArray()) e.SetSignature(source, SignatureChannel.Reflection, 0f, Guns(e)[0]);
        e.SetSignature(new object(), SignatureChannel.Thermal, gate * against, null);
        Assert.Equal(gate * against, e.Visibility, 4);
        Assert.All(e.VisibilitySources.Values.Where(v => v.Strength > 0f), v => Assert.Null(v.Item)); // fixture: the deciding light names no item

        _zone.Remove(e);
        Tick(.1f);

        Assert.Equal(ended ? 0 : 1, o.RevealsOf(e).Count());
    }

    // The gate is strict, as the light-act gate is: a light at removal that lands exactly on the threshold ends nothing.
    // At 32 the observer's info per unit light is 4 / 32, a power of two, so threshold x 8 is the light whose product
    // with it is exactly the threshold.
    [Fact]
    public void ADepartureExactlyAtTheLightActGateEndsNothing()
    {
        var e = Add(float3(0, 0, 32), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        Fire(e, Guns(e)[0]);
        Tick(.01f);
        Assert.Single(o.RevealsOf(e));
        var threshold = _items.GameplaySettings.TargetDetectionInfoThreshold;
        Assert.Equal(.125f, o.PassiveInfo(e.Position.xz, 1f));
        ShowOnly(e, threshold * 8f, Guns(e)[0]);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) == threshold, "fixture: the ship's light at removal is exactly the gate");

        _zone.Remove(e);
        Tick(.1f);

        Assert.Single(o.RevealsOf(e));
    }

    // A ship removed and put back before anyone senses has not left: its reveals stand, and its next act refreshes them.
    [Fact]
    public void AReAdmittedSubjectHasNotDeparted()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        var at = Fire(e, gun);
        Tick(1f); // the gun cools down
        ShowOnly(e, 1000f, gun); // bright enough for any gate

        _zone.Remove(e);
        e.Deactivate();
        _zone.Admit(e, piloted: false);
        Tick(.1f);

        Assert.Equal(at, Assert.Single(o.RevealsOf(e)).Time);
        var later = Fire(e, gun);
        Assert.True(later > at);
        Assert.Equal(later, Assert.Single(o.RevealsOf(e)).Time);
    }

    private static int RevealBookSubjects(Entity observer) =>
        ((System.Collections.IDictionary) typeof(Entity).GetField("_reveals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(observer)).Count;

    // Ruling contact-reads-book-aim-reads-live: what an observer knows of a ship comes from its own book. A gun
    // destroyed by a third party out of its sight changes none of its levels and stays in its reveal list, while
    // the aiming read, which asks about the item now, refuses the dead gun and still serves the live one. No Sense
    // runs after the destruction here; the unseen case with a Sense is AnIdentifiedObserverSeesGearDestroyed's
    // below-threshold case.
    [Fact]
    public void GearDestroyedOutOfSightLeavesWhatTheObserverKnowsAlone()
    {
        var e = Add(float3(0, 0, 150), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        var before = o.ContactOf(e);
        Assert.True(before.Detected && before.Classified && !before.Tracked, "fixture: a bearing and a class, no track");
        Assert.True(FireControl.IsRevealed(o, guns[0]) && FireControl.IsRevealed(o, guns[1]), "fixture: both guns are aimable");

        guns[0].EquippableItem.Durability = 0f; // destroyed by someone else; o saw nothing

        Assert.Equal(2, o.RevealsOf(e).Count());
        var after = o.ContactOf(e);
        Assert.True(after.Detected);
        Assert.True(after.Classified);
        Assert.Equal(before.Bearing.x, after.Bearing.x);
        Assert.Equal(before.Bearing.y, after.Bearing.y);
        Assert.False(FireControl.IsRevealed(o, guns[0]));
        Assert.True(FireControl.IsRevealed(o, guns[1]));

        guns[1].EquippableItem.Durability = 0f; // the last of its gear dies unseen too

        Assert.Equal(2, o.RevealsOf(e).Count());
        Assert.True(o.ContactOf(e).Detected);
        Assert.True(o.ContactOf(e).Classified);
    }

    // Ruling reveal-ends-on-perception: an observer that resolves a ship's gear (its track at TargetGearInfoThreshold)
    // perceives an item of it destroyed, and the reveal of that item ends in the same Sense pass. One that does not
    // resolve it perceives nothing.
    [Theory]
    [InlineData(.8f, 1, 1, 1)]
    [InlineData(.8f, 2, 0, 0)]
    [InlineData(.79f, 1, 2, 1)]
    [InlineData(.79f, 2, 2, 1)]
    public void AnIdentifiedObserverSeesGearDestroyed(float info, int destroyed, int remaining, int classifiedKeys)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        o.VisibleEntities.Add(e); // tracked, so what the track holds is not the question
        o.EntityInfoGathered[e] = info;
        var sense = SenseOf(o);
        for (var i = 0; i < destroyed; i++) guns[i].EquippableItem.Durability = 0f;

        sense();

        Assert.Equal(remaining, o.RevealsOf(e).Count());
        var contact = o.ContactOf(e);
        Assert.True(contact.Detected);
        Assert.Equal(remaining > 0, contact.Classified);
        Assert.Equal(remaining > 0 ? classifiedKeys : 0, RevealBookSubjects(o));
        if (remaining == 1 && info >= .8f) Assert.Same(guns[1], o.RevealsOf(e).Single().Item);
    }

    // The same through a real tick: a gun destroyed while its flash still shows, seen by an observer that identifies
    // the ship, is not in the book after the tick.
    [Fact]
    public void ADestroyedGunStillFlashingIsNotRecordedByAnIdentifyingObserver()
    {
        var e = Add(float3(0, 0, 60), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        e.SetSignature(new object(), SignatureChannel.Reflection, 1000f, guns[0]); // the dying gun's glare outlasts the tick
        guns[0].EquippableItem.Durability = 0f;
        o.EntityInfoGathered[e] = 1f; // o's sensor keeps it at about 1 across the tick (decay .5 x dt)

        Tick(.01f);

        Assert.True(Track(o, e) >= .8f, "fixture: o still identifies e");

        Assert.DoesNotContain(o.RevealsOf(e), r => r.Item == guns[0]);
        Assert.Contains(o.RevealsOf(e), r => r.Item == guns[1]);
    }

    // A paint inboxed in the tick its emitter leaves the zone is published the next tick, and is no longer the act of
    // a subject in the zone: it warns for its tick and neither creates nor refreshes a reveal. The reveal the
    // locker's earlier paint made stays what it was, as a departure out of sight (below the observer's gate) leaves it.
    [Fact]
    public void APaintFromAShipThatHasLeftTheZoneRevealsNothing()
    {
        var locker = Add(float3(0, 0, 200), new[] { "Locker" });
        var o = Add(float3(0, 0, 0)); // deaf: no active sensor, so the reveal is the paint's alone, never the lock's emission heard
        locker.SetIff(o, true);
        locker.EntityInfoGathered[o] = 1f;
        locker.SetTarget(o);
        locker.Aim = normalize(o.Position - locker.Position);
        for (var tick = 0; tick < 10 && !o.RevealsOf(locker).Any(); tick++) Tick(.5f);
        var first = Assert.Single(o.RevealsOf(locker)); // fixture: the lock paints every tick, and the first one is recorded
        Assert.True(locker.Visibility * o.PassiveInfo(locker.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: its departure is below o's gate");

        _zone.Remove(locker); // dies, warps or docks while its latest paint is still in o's inbox
        var inboxed = ((System.Collections.IList) typeof(Entity).GetField("_paintInbox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o)).Count;
        Assert.True(inboxed > 0 || o.IncomingPaints.Count > 0, "fixture: a paint is in flight");
        Tick(.1f);

        Assert.Single(o.IncomingPaints); // fixture: the stale paint was published
        var after = Assert.Single(o.RevealsOf(locker));
        Assert.Equal(first.Time, after.Time);
        Assert.Equal(1, RevealBookSubjects(o));
    }

    // Ruling sensor-stat-set 4: a painted target always learns the emitter's class, whichever the mode. A ping
    // crossing the observer (a Search paint) reveals the pinger's sensor item, with channel Emission and the
    // bearing of the pinger.
    [Fact]
    public void ASearchPaintRevealsThePingersSensor()
    {
        var p = Add(float3(0, 0, 100), new[] { "Eye" });
        var o = Add(float3(0, 0, 0)); // deaf: the reveal is the paint's alone, never the ping's emission heard
        var item = p.Equipment.Single(i => i.Behaviors.Any(b => b is Sensor));
        var sensor = (Sensor) item.Behaviors.Single(b => b is Sensor);
        Tick(.01f);
        sensor.Ping();

        var modes = new List<PaintMode>();
        for (var tick = 0; tick < 30; tick++)
        {
            Tick(.1f);
            modes.AddRange(o.IncomingPaints.Select(paint => paint.Mode));
        }

        Assert.Equal(new[] { PaintMode.Search }, modes); // fixture: the sweep painted o once, as a search
        var reveal = Assert.Single(o.RevealsOf(p));
        Assert.Same(item, reveal.Item);
        Assert.Equal(SignatureChannel.Emission, reveal.Channel);
        AssertBearing(0f, 1f, reveal.Bearing);
    }

    // A search paint (a ping crossing the observer) from a pinger that has left the zone before the paint is
    // published warns once and creates no reveal and no key.
    [Fact]
    public void ASearchPaintFromAPingerThatHasLeftCreatesNoReveal()
    {
        var p = Add(float3(0, 0, 100), new[] { "Eye" });
        var o = Add(float3(0, 0, 0)); // deaf: it heard nothing of the sweep while the pinger stood in the zone
        var sensor = (Sensor) p.Equipment.Single(i => i.Behaviors.Any(b => b is Sensor)).Behaviors.Single(b => b is Sensor);
        Tick(.01f);
        sensor.Ping();
        var inbox = (System.Collections.IList) typeof(Entity).GetField("_paintInbox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        for (var tick = 0; tick < 100 && inbox.Count == 0; tick++) Tick(.1f);
        Assert.NotEmpty(inbox); // fixture: the ping crossed o and its paint waits to be published

        _zone.Remove(p);
        Tick(.1f);

        Assert.Single(o.IncomingPaints);
        Assert.Equal(PaintMode.Search, o.IncomingPaints.Single().Mode);
        Assert.Empty(o.RevealsOf(p));
        Assert.Equal(0, RevealBookSubjects(o));
        Assert.False(o.ContactOf(p).Detected);
    }

    // A ship out of the zone (docked, destroyed, warped) is no longer published to, so it holds no warnings: the one it
    // held when it left is gone, and every reader sees an empty inbox.
    [Fact]
    public void AShipOutOfTheZoneHoldsNoWarnings()
    {
        var p = Add(float3(0, 0, 100), new[] { "Eye" });
        var o = Add(float3(0, 0, 0));
        var sensor = (Sensor) p.Equipment.Single(i => i.Behaviors.Any(b => b is Sensor)).Behaviors.Single(b => b is Sensor);
        Tick(.01f);
        sensor.Ping();
        for (var tick = 0; tick < 100 && o.IncomingPaints.Count == 0; tick++) Tick(.1f);
        Assert.Single(o.IncomingPaints); // fixture: the ping crossed o

        _zone.Remove(o);
        for (var tick = 0; tick < 5; tick++)
        {
            Tick(.1f);
            Assert.Empty(o.IncomingPaints);
        }
    }

    // An observer's own gear is not another ship's act: a ship that fires its own gun, with its own sensor at the
    // very place of the light, records nothing of itself, while a neighbour does record the gun.
    [Fact]
    public void AnObserverNeverRecordsItsOwnGear()
    {
        var self = Add(float3(0, 0, 0), new[] { "Gun", "Eye" });
        var neighbour = Add(float3(0, 0, 30), new[] { "Eye" });

        Fire(self, Guns(self)[0]);

        Assert.Single(neighbour.RevealsOf(self)); // fixture: the gun's light reaches a sensor
        Assert.Empty(self.RevealsOf(self));
        Assert.False(self.ContactOf(self).Detected);
    }

    // The zone's list order is not a rule of the game: two observers equidistant from a gun, one listed before the
    // gun's ship and one after, record the same reveal with the same bearing and the same clock. The ship may be
    // moving: the bearing is that of the ship where the tick left it, whatever the order the zone lists them in.
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(30f, -20f)]
    [InlineData(-45f, 10f)]
    public void AnActIsRevealedOnItsOwnTickWhateverTheListOrder(float vx, float vz)
    {
        var before = Add(float3(-30, 0, 40), new[] { "Eye" });
        var e = Add(float3(30, 0, 40), new[] { "Gun" });
        var after = Add(float3(0, 0, 0), new[] { "Eye" });
        var start = e.Position;
        e.Velocity = float2(vx, vz);

        var at = Fire(e, Guns(e)[0]);

        if (vx != 0f || vz != 0f) Assert.True(length(e.Position - start) > 1f, "fixture: e moved during the firing tick");
        foreach (var observer in new Entity[] { before, after })
        {
            var reveal = Assert.Single(observer.RevealsOf(e));
            Assert.Equal(at, reveal.Time);
            var toward = normalize((e.Position - observer.Position).xz);
            AssertBearing(toward.x, toward.y, reveal.Bearing);
        }
    }

    // ---- A quiet ship ----

    // Ruling quiet-gear-identifiable-hull-layout-known: a ship that has shown nothing is Detected while it is
    // tracked and Identified once the track reaches the gear threshold, with no act in any book; with no reveal
    // it is not Classified.
    [Theory]
    [InlineData(.05f, false, false)]
    [InlineData(.5f, true, false)]
    [InlineData(.79f, true, false)]
    [InlineData(.8f, true, true)]
    [InlineData(1f, true, true)]
    public void AQuietTrackedShipIsDetectedAndIdentifiedByTheTrackAlone(float info, bool tracked, bool identified)
    {
        var e = Add(float3(30, 0, 40), new[] { "Gun" }); // armed, but it never fires
        var o = Add(float3(0, 0, 0), new[] { "Eye" });

        o.EntityInfoGathered[e] = info;

        Assert.Empty(o.RevealsOf(e));
        var contact = o.ContactOf(e);
        Assert.Equal(tracked, o.VisibleEntities.Contains(e));
        Assert.Equal(tracked, contact.Tracked);
        Assert.Equal(tracked, contact.Detected);
        Assert.False(contact.Classified);
        Assert.Equal(identified, contact.Identified);
        Assert.Equal(info, contact.Info);
        if (tracked) AssertBearing(.6f, .8f, contact.Bearing);
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

    // A destroyed line, not a destroyed zero: Durability below .01 is destroyed and .01 or more is not, for the
    // observer that identifies the ship.
    [Theory]
    [InlineData(.009f, true)]
    [InlineData(.011f, false)]
    public void AnItemIsDestroyedBelowTheDurabilityLine(float durability, bool destroyed)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        o.VisibleEntities.Add(e);
        o.EntityInfoGathered[e] = 1f;
        guns[0].EquippableItem.Durability = durability;

        SenseOf(o)();

        Assert.Equal(destroyed, !o.RevealsOf(e).Any(r => r.Item == guns[0]));
        Assert.Contains(o.RevealsOf(e), r => r.Item == guns[1]);
    }

    // ---- Departure notes ----

    // A kill inside FireControl.Step happens after this tick's Sense: the observers' next Sense reads it, whatever
    // order the zone lists them in.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AKillInsideFireControlStepIsReadByTheNextSense(bool observerFirst)
    {
        Entity strong = null;
        if (observerFirst) strong = Add(float3(-1, 0, 0), new[] { "Ear" });
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        var killer = Add(float3(60, 0, 100), new[] { "Gun" });
        if (!observerFirst) strong = Add(float3(-1, 0, 0), new[] { "Ear" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        Tick(1f);
        var killed = false;
        void Kill(ShotOutcome _)
        {
            if (killed || !_zone.Entities.Contains(e)) return;
            killed = true;
            ShowOnly(e, 2f, guns[0]); // lit at the moment it dies, as the ear's gate sees it
            Assert.True(e.Visibility * strong.PassiveInfo(e.Position.xz, 1f) > _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: the ear passes the gate");
            e.Hull.Durability = 0f;
            e.HullDamage.OnNext(1f);
        }
        UniRx.ObservableExtensions.Subscribe(_zone.ShotCommitted, Kill);
        UniRx.ObservableExtensions.Subscribe(_zone.ShotResolved, Kill);
        var killerGun = (InstantWeapon) Guns(killer)[0].Behaviors.Single(b => b is InstantWeapon);
        ((InputCapacitor) typeof(InstantWeapon).GetField("_capacitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(killerGun)).AddCharge(1000f);
        killer.SetTarget(TargetRef.None);
        killerGun.Activate();
        for (var ticks = 0; !killed && ticks < 20; ticks++) Tick(.1f);
        Assert.True(killed, "fixture: the kill lands inside a tick");
        Assert.False(_zone.Entities.Contains(e), "fixture: the kill took the subject out of the zone");

        Assert.Equal(2, strong.RevealsOf(e).Count()); // this tick's Sense came before the kill

        Tick(.1f);

        Assert.Empty(strong.RevealsOf(e));
        Assert.False(strong.ContactOf(e).Detected);
    }

    // The note of a removal is read by exactly one Sense: an observer that could not see the departure on that tick
    // does not judge it again later from where it then is.
    [Fact]
    public void ADepartureIsJudgedOnTheTickAfterItsRemovalAndNeverAgain()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        Fire(e, gun);
        ShowOnly(e, 1f, gun);
        var threshold = _items.GameplaySettings.TargetDetectionInfoThreshold;
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= threshold, "fixture: from where o is, the departure is below the gate");
        _zone.Remove(e);
        Tick(.1f);
        Assert.Single(o.RevealsOf(e));

        o.Position = float3(0, 0, 90); // now o is close enough to have seen it go
        Assert.True(e.Visibility * o.PassiveInfo(float2(0, 100), 1f) > threshold, "fixture: from here the same departure would pass the gate");
        Tick(.1f);

        Assert.Single(o.RevealsOf(e));
        Assert.True(o.ContactOf(e).Detected);
    }

    // Every departure of a tick is judged, whatever order they were removed in.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryDepartureOfATickIsJudged(bool firstThenSecond)
    {
        var o = Add(float3(-1, 0, 0), new[] { "Ear" });
        var a = Add(float3(0, 0, 100), new[] { "Gun" });
        var b = Add(float3(0, 0, -100), new[] { "Gun" });
        Fire(a, Guns(a)[0]);
        Fire(b, Guns(b)[0]);
        Assert.Single(o.RevealsOf(a));
        Assert.Single(o.RevealsOf(b));
        ShowOnly(a, 2f, Guns(a)[0]);
        ShowOnly(b, 2f, Guns(b)[0]);
        var threshold = _items.GameplaySettings.TargetDetectionInfoThreshold;
        Assert.True(a.Visibility * o.PassiveInfo(a.Position.xz, 1f) > threshold && b.Visibility * o.PassiveInfo(b.Position.xz, 1f) > threshold, "fixture: both departures pass the gate");

        _zone.Remove(firstThenSecond ? a : b);
        _zone.Remove(firstThenSecond ? b : a);
        Tick(.1f);

        Assert.Empty(o.RevealsOf(a));
        Assert.Empty(o.RevealsOf(b));
        Assert.Equal(0, RevealBookSubjects(o));
    }

    // Nothing the observer keeps for a pass reaches a ship that has left: a ship with one gun ended as destroyed and
    // one aged out by the prune cannot be reached from the observer (its fields, collections and closures, not the
    // zone it shares with everyone) once the ship is gone and its memory has lapsed.
    [Fact]
    public void ADepartedSubjectIsUnreachableFromTheObserverAfterThePrune()
    {
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var e = Add(float3(0, 0, 100), new[] { "Gun", "Gun" });
        var guns = Guns(e);
        Fire(e, guns[0]);
        Fire(e, guns[1]);
        Tick(1f);
        o.EntityInfoGathered[e] = 1f;
        ShowOnly(e, 0f, guns[1]); // its departure is below the observer's gate: the other gun's reveal waits for the prune
        guns[0].EquippableItem.Durability = 0f;
        Tick(.01f);
        Assert.Same(guns[1], Assert.Single(o.RevealsOf(e)).Item); // fixture: one gun ended as destroyed, one waiting
        e.Deactivate(); // the warp path: the ship's own subscriptions to the observer go with it
        _zone.Remove(e);
        for (var i = 0; i < 40; i++) Tick(.1f); // past BearingMemory (2 s)
        Assert.Equal(0, RevealBookSubjects(o)); // fixture: pruned

        foreach (var held in new object[] { e, guns[0], guns[1] })
            Assert.Null(PathFrom(o, held));
    }

    // Cut contact-hud: selection tells the selected ship nothing. A ship that selects another, which is neither painted
    // by it nor sees it, leaves no fact on the selected one: no paint in its inbox, no contact, no reveal.
    [Fact]
    public void NothingReadsSelection()
    {
        var selector = Add(float3(0, 0, 200), new[] { "Gun" });
        var selected = Add(float3(0, 0, 0)); // deaf and unseen: no sensor, nothing it could hear
        selector.SetTarget(selected);
        Assert.True(ReferenceEquals(selected, selector.Target.Value.Entity)); // fixture: it is selected

        var paints = 0;
        for (var i = 0; i < 20; i++)
        {
            Tick(.1f);
            paints += selected.IncomingPaints.Count; // a warning lives one tick, so it is read on every tick
        }

        Assert.Equal(0, paints);
        Assert.False(selected.ContactOf(selector).Detected);
        Assert.Equal(0, selected.RevealedSubjects.Count);
    }

    // The one list of remembered contacts: the subjects whose reveals the observer holds. A tracked subject with no
    // reveal is not in it; a revealed subject that leaves unseen stays until its bearing is BearingMemory old, then
    // goes; Deactivate empties it.
    [Fact]
    public void RevealedSubjectsListsTheBook()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var tracked = Add(float3(50, 0, 0));
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var gun = Guns(e)[0];
        o.EntityInfoGathered[tracked] = 1f;
        Assert.True(o.ContactOf(tracked).Tracked); // fixture: tracked, no reveal
        Assert.Equal(0, o.RevealedSubjects.Count); // counts, not the collection: a failure message would print whole entities

        Fire(e, gun);
        Assert.True(o.RevealedSubjects.Count == 1 && o.RevealedSubjects.Contains(e));

        var q = Add(float3(40, 0, 100), new[] { "Gun" });
        Fire(q, Guns(q)[0]);
        Assert.True(o.RevealedSubjects.Count == 2 && o.RevealedSubjects.Contains(e) && o.RevealedSubjects.Contains(q)); // two subjects at once: both listed

        var freshest = Freshest(o, e);
        ShowOnly(e, 1f, gun);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: its last light is below o's gate");
        _zone.Remove(e);
        var memory = _items.GameplaySettings.BearingMemory;
        var lapsed = false;
        for (var i = 0; i < 200 && !lapsed; i++)
        {
            Tick(.1f);
            lapsed = _zone.Time - freshest >= memory;
            Assert.Equal(!lapsed, o.RevealedSubjects.Contains(e)); // exactly while the bearing is remembered
            Assert.True(o.RevealedSubjects.Contains(q), "the other subject stays listed while e departs");
        }
        Assert.True(lapsed, "fixture: the bearing lapsed");
        Assert.True(o.RevealedSubjects.Count == 1 && o.RevealedSubjects.Contains(q)); // only the departed one dropped
        o.Deactivate();
        Assert.Equal(0, o.RevealedSubjects.Count); // counts, not the collection: a failure message would print whole entities
    }

    // The hostile subjects held only by a bearing (what bearing markers and the music show): a hostile that fired and is
    // untracked is listed; the same kind of subject tracked is not; a non-hostile that fired is not; one that departs
    // unseen is listed exactly until its bearing is BearingMemory old; Deactivate empties it.
    [Fact]
    public void RememberedHostilesAreBearingOnlyHostiles()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var t = Add(float3(50, 0, 0), new[] { "Gun" });
        var n = Add(float3(40, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        o.SetIff(e, true);
        o.SetIff(t, true);
        o.SetIff(n, false);
        var gun = Guns(e)[0];
        Fire(e, gun);
        Fire(n, Guns(n)[0]);
        Fire(t, Guns(t)[0]);
        Assert.False(o.ContactOf(e).Tracked, "fixture: held by a bearing only");
        Assert.True(o.RevealedSubjects.Contains(n) && o.IsHostileTo(n) == false, "fixture: n is revealed and not hostile");

        Assert.True(o.RememberedHostiles.Contains(e), "a hostile that fired, untracked");
        Assert.False(o.RememberedHostiles.Contains(n), "a non-hostile that fired");
        Assert.True(o.RememberedHostiles.Contains(t), "fixture: t is untracked so far");
        o.EntityInfoGathered[t] = 1f;
        Assert.True(o.ContactOf(t).Tracked, "fixture: t is tracked now");
        Assert.False(o.RememberedHostiles.Contains(t), "the same hostile tracked is a hostile indicator's");

        var freshest = Freshest(o, e);
        ShowOnly(e, 1f, gun);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: its last light is below o's gate");
        _zone.Remove(e);
        Assert.True(o.IsHostileTo(e), "the override outlives the departure while the book remembers the subject; nothing restates it");
        var memory = _items.GameplaySettings.BearingMemory;
        var lapsed = false;
        for (var i = 0; i < 200 && !lapsed; i++)
        {
            Tick(.1f);
            lapsed = _zone.Time - freshest >= memory;
            Assert.Equal(!lapsed, o.RememberedHostiles.Contains(e)); // exactly while the bearing is remembered
        }
        Assert.True(lapsed, "fixture: the bearing lapsed");
        Assert.False(o.IsHostileTo(e), "the book dropped the subject, and the override went with it");
        o.Deactivate();
        Assert.Equal(0, o.RememberedHostiles.Count());
    }

    // A hostile that fired and stayed in the zone, dark: the book keeps it (an in-zone subject is never pruned), so only
    // ContactOf's Detected ends its listing, exactly when its bearing is BearingMemory old.
    [Fact]
    public void ALapsedHostileInTheZoneIsNotRemembered()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        o.SetIff(e, true);
        var gun = Guns(e)[0];
        Fire(e, gun);
        var freshest = Freshest(o, e);
        ShowOnly(e, 1f, gun);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: its last light is below o's gate");
        Assert.True(o.RememberedHostiles.Contains(e), "fixture: listed while young");
        var memory = _items.GameplaySettings.BearingMemory;
        var lapsed = false;
        for (var i = 0; i < 200 && !lapsed; i++)
        {
            Tick(.1f);
            lapsed = _zone.Time - freshest >= memory;
            Assert.Equal(!lapsed, o.RememberedHostiles.Contains(e)); // exactly while the bearing is remembered
        }
        Assert.True(lapsed, "fixture: the bearing lapsed");
        Assert.True(o.RevealedSubjects.Contains(e), "fixture: the in-zone subject is still in the book");
        Assert.False(o.ContactOf(e).Detected, "fixture: the contact is no longer Detected");
        Assert.False(o.RememberedHostiles.Contains(e));
    }

    // A remembered hostile is decided by the book, never by where the subject stands: loud pingers 500 to 600 away on three
    // sides are heard (Roar: 300 over .5) and listed, each at the bearing it was heard on.
    [Fact]
    public void RememberedHostilesDoNotDependOnDistance()
    {
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var spots = new[] { float3(550, 0, 0), float3(-520, 0, 100), float3(0, 0, 590) };
        var pingers = spots.Select(spot => Add(spot, new[] { "Roar" })).ToArray();
        foreach (var p in pingers) o.SetIff(p, true);
        foreach (var p in pingers) PingAt(p);

        foreach (var p in pingers)
        {
            Assert.False(o.ContactOf(p).Tracked, "fixture: held by a bearing only");
            Assert.True(length(p.Position - o.Position) > 500f, "fixture: far from the observer");
            Assert.True(o.RememberedHostiles.Contains(p));
            var toward = normalize((p.Position - o.Position).xz);
            AssertBearing(toward.x, toward.y, o.ContactOf(p).Bearing);
        }
        Assert.Equal(3, o.RememberedHostiles.Count());
    }

    // The observer's stance is its own live decision, not something it saw: toggled after the last act, while the subject is
    // remembered and untracked, the listing follows at once, in both directions.
    [Fact]
    public void AStanceToggledAfterTheLastActShowsAtOnce()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var q = Add(float3(40, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        o.SetIff(e, true);
        o.SetIff(q, false);
        Fire(e, Guns(e)[0]);
        Fire(q, Guns(q)[0]);
        ShowOnly(e, 1f, Guns(e)[0]);
        ShowOnly(q, 1f, Guns(q)[0]);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: e unseen");
        Assert.True(q.Visibility * o.PassiveInfo(q.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: q unseen");
        Assert.True(o.RememberedHostiles.Contains(e) && !o.RememberedHostiles.Contains(q), "fixture: stances as made");

        o.SetIff(e, false);
        o.SetIff(q, true);
        Tick(.1f);
        Assert.True(o.ContactOf(e).Detected && !o.ContactOf(e).Tracked && o.ContactOf(q).Detected && !o.ContactOf(q).Tracked, "fixture: both still remembered by bearing");
        Assert.False(o.RememberedHostiles.Contains(e), "hostile to neutral after the act");
        Assert.True(o.RememberedHostiles.Contains(q), "neutral to hostile after the act");
    }

    // A hostile held only by the observer's override that departs keeps its marker until the bearing lapses and not a tick
    // longer, and its override is gone with the book entry; a stance for a subject the book never held goes at once.
    [Fact]
    public void ADepartedOverrideHostileKeepsItsMarkerThenLosesItsStance()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var never = Add(float3(40, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        o.SetIff(e, true);
        o.SetIff(never, true);
        Fire(e, Guns(e)[0]);
        var freshest = Freshest(o, e);
        ShowOnly(e, 1f, Guns(e)[0]);
        _zone.Remove(never);
        Assert.False(o.IsHostileTo(never), "a subject the book never held loses its stance on leaving");
        _zone.Remove(e);
        Assert.True(o.RememberedHostiles.Contains(e), "marker kept on departure");
        var memory = _items.GameplaySettings.BearingMemory;
        var lapsed = false;
        for (var i = 0; i < 200 && !lapsed; i++)
        {
            Tick(.1f);
            lapsed = _zone.Time - freshest >= memory;
            Assert.Equal(!lapsed, o.RememberedHostiles.Contains(e));
            Assert.Equal(!lapsed, o.IsHostileTo(e));
        }
        Assert.True(lapsed, "fixture: the bearing lapsed");
    }

    // The memory window is exclusive: at Zone.Time - freshest == BearingMemory the bearing has lapsed (Detected, whether the
    // subject stays or departs), and a departed subject's book entry (and the stance with it) goes on that tick. The sighting is on a coarse float time (.5) so
    // that freshest + memory is representable and a step can land on it exactly.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheMemoryWindowIsExclusiveAtTheEquality(bool departs)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        o.SetIff(e, true);
        var gun = Guns(e)[0];
        Tick((float) (.5 - (double) .1f));
        Fire(e, gun);
        var freshest = Freshest(o, e);
        Assert.Equal(.5f, freshest);
        ShowOnly(e, 1f, gun);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: unseen");
        if (departs) _zone.Remove(e);
        var memory = _items.GameplaySettings.BearingMemory;
        for (var i = 0; i < 1000 && _zone.Time - freshest + .3f < memory; i++) Tick(.1f);
        Assert.True(o.RememberedHostiles.Contains(e), "fixture: still listed");
        var exact = _zone.ExactTime;
        var d = (float) (memory - (exact - freshest));
        var found = false;
        for (var k = 0; k < 400 && !found; k++)
            foreach (var sign in new[] { 1, -1 })
            {
                var c = d;
                for (var j = 0; j < k; j++) c = sign > 0 ? MathF.BitIncrement(c) : MathF.BitDecrement(c);
                if ((float) (exact + c) - freshest == memory) { d = c; found = true; break; }
            }
        Assert.True(found, "fixture: a step landing exactly on the window edge exists");
        Tick(d);
        Assert.Equal(memory, _zone.Time - freshest);
        Assert.False(o.RememberedHostiles.Contains(e));
        Assert.False(o.ContactOf(e).Detected);
        if (departs)
        {
            Assert.False(o.RevealedSubjects.Contains(e), "the departed subject leaves the book at the edge");
            Assert.False(o.IsHostileTo(e), "the book dropped the departed subject at the edge, and the stance with it");
        }
    }

    // A stance lasts the sitting: a hostile still in the zone whose every revealed item the observer perceives destroyed
    // leaves the book, and the observer's decision about it stays.
    [Fact]
    public void AStanceOutlastsTheBookEntryOfASubjectStillInTheZone()
    {
        var e = Add(float3(0, 0, 60), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        o.SetIff(e, true);
        var gun = Guns(e)[0];
        Fire(e, gun);
        Assert.Single(o.RevealsOf(e));
        o.EntityInfoGathered[e] = 1f;
        gun.EquippableItem.Durability = 0f;

        Tick(.01f);

        Assert.Empty(o.RevealsOf(e)); // fixture: o perceived the destruction and the book dropped the subject
        Assert.True(_zone.Entities.Contains(e), "fixture: the subject is still in the zone");
        Assert.True(o.IsHostileTo(e));
    }

    // The first field path from `root` to `target`, not entering the zone or the item manager, or null.
    private static string PathFrom(object root, object target)
    {
        var seen = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance) { root };
        var queue = new Queue<(object node, string path)>();
        queue.Enqueue((root, "observer"));
        while (queue.Count > 0)
        {
            var (node, path) = queue.Dequeue();
            if (ReferenceEquals(node, target)) return path;
            var type = node.GetType();
            if (node is Zone || node is ItemManager || node is Type || node is string || type.IsPrimitive || type.IsEnum
                || type.Namespace == "System.Reflection") continue;
            IEnumerable<(object value, string name)> children;
            if (node is Array array)
                children = array.Cast<object>().Select((v, i) => (v, "[" + i + "]"));
            else
                children = Fields(type).Select(f => (f.GetValue(node), "." + f.Name));
            foreach (var (value, name) in children)
                if (value != null && seen.Add(value)) queue.Enqueue((value, path + name));
        }
        return null;
    }

    private static IEnumerable<FieldInfo> Fields(Type type)
    {
        for (; type != null; type = type.BaseType)
            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!f.FieldType.IsPointer) yield return f;
    }

    // ---- The one rule for info per signature ----

    [Fact]
    public void PassiveInfoAveragesTheActiveSensors()
    {
        var one = Add(float3(0, 0, 0), new[] { "Eye" });
        var two = Add(float3(500, 0, 0), new[] { "Eye", "Ear" });
        var none = Add(float3(1000, 0, 0));

        // sum(s) / (d n k): 2 / (100 x .5) with one sensor, (2 + 4) / (100 x 2 x .5) with two; linear in the signature.
        Assert.Equal(.04f, one.PassiveInfo(float2(0, 100), 1f), 5);
        Assert.Equal(.12f, one.PassiveInfo(float2(0, 100), 3f), 5);
        Assert.Equal(.06f, two.PassiveInfo(float2(500, 100), 1f), 5);
        Assert.Equal(0f, none.PassiveInfo(float2(1000, 100), 1f));
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
        // One ship holds a lock paint on another, so the observer's sense hears an emission on every pass (the observer's
        // own Add runs the zone's last update, which is where the emission is made; no update follows).
        var painted = Add(float3(40, 0, 300));
        var painter = Add(float3(0, 0, 300), new[] { "Locker" });
        painter.SetIff(painted, true);
        painter.EntityInfoGathered[painted] = 1f;
        painter.SetTarget(painted);
        painter.Aim = float3(1, 0, 0);
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        var sense = SenseOf(o);
        for (var i = 0; i < 10; i++) sense();
        Assert.Equal(19, _zone.Entities.Where(x => x != o && x != painter && x != painted).Count(x => o.RevealsOf(x).Any())); // fixture: every ship is revealed
        Assert.Single(o.RevealsOf(painter)); // fixture: the lock's emission is heard
        // One of them is identified by o and its gun is destroyed while it still glows, so the pass ends that reveal
        // and the flash cannot re-enter; one has left, quiet, so its entries await the prune and its departure sits
        // in the zone's list.
        var identified = gunItems.Select(g => _zone.Entities.Single(x => x.Equipment.Contains(g))).First();
        o.EntityInfoGathered[identified] = 1f;
        gunItems[0].EquippableItem.Durability = 0f;
        sense();
        Assert.Empty(o.RevealsOf(identified)); // fixture: its gun is destroyed, ended, and still lit
        Assert.Contains(identified.VisibilitySources.Values, v => v.Item == gunItems[0] && v.Strength > 0f);
        var departed = _zone.Entities.Single(x => x.Equipment.Contains(gunItems[18]));
        foreach (var source in departed.VisibilitySources.Keys.ToArray()) departed.SetSignature(source, SignatureChannel.Reflection, 0f, gunItems[18]);
        _zone.Remove(departed);
        Assert.Single((System.Collections.IList) typeof(Zone).GetProperty("Departures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_zone));
        sense();
        Assert.Single(o.RevealsOf(departed)); // fixture: awaiting its prune

        // The smallest of several rounds is the steady-state allocation (FireControlCut12Tests, the Lane and
        // Silhouette pin): a one-off JIT or tier-up allocation lands in one round of a busy full-suite run, a
        // per-call allocation lands in every round.
        var smallest = long.MaxValue;
        for (var round = 0; round < 20 && smallest != 0; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 600; i++) sense();
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0L, smallest);
    }

    // A docked subject leaves the zone parented to its dock and rejoins on undock: the observer's decision about it, hostile or
    // neutral, outlives the lapse of the bearing, and is still there when it rejoins.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ADockedSubjectKeepsTheObserversStanceAfterTheBearingLapses(bool hostile)
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var dock = Add(float3(200, 0, 0));
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        // The decision is the observer's own override, read back as itself (no telemetry decides here: the subject is never tracked).
        o.SetIff(e, hostile);
        Assert.Equal(hostile, o.IffOverride(e)); // fixture: the decision is stored
        var gun = Guns(e)[0];
        Fire(e, gun);
        ShowOnly(e, 1f, gun);
        Assert.True(e.Visibility * o.PassiveInfo(e.Position.xz, 1f) <= _items.GameplaySettings.TargetDetectionInfoThreshold, "fixture: unseen");
        e.Parent = dock;
        _zone.Remove(e);
        for (var i = 0; i < 100 && o.RevealedSubjects.Contains(e); i++) Tick(.1f);
        Assert.False(o.RevealedSubjects.Contains(e), "fixture: the book dropped the docked subject");
        Assert.Equal(hostile, o.IffOverride(e));
        e.Parent = null;
        _zone.Admit(e, piloted: false);
        Assert.Equal(hostile, o.IffOverride(e));
        Assert.Equal(hostile, o.IsHostileTo(e));
    }

    // The zone owner does not permit `e` here. Set directly: Update re-derives the security level from the ship's location.
    private static void Forbid(Entity e) => e.PresencePermitted = UniRx.Observable.Return(false).ToReadOnlyReactiveProperty();

    // Ruling ew-telemetry-with-the-track: a contact held by a bearing only has no allegiance; it gets one at its first track.
    [Fact]
    public void ABearingOnlyContactHasNoAllegiance()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        _zone.GalaxyZone.Owner = o.Faction;
        Forbid(e);
        var gun = Guns(e)[0];
        Fire(e, gun);
        ShowOnly(e, 1f, gun);
        Tick(.1f);
        Assert.True(o.RevealedSubjects.Contains(e) && !o.ContactOf(e).Tracked, "fixture: held by a bearing only");

        Assert.False(o.IsHostileTo(e));
        Assert.False(o.TryGetTelemetry(e, out _));
        Assert.DoesNotContain(e, o.RememberedHostiles);

        o.EntityInfoGathered[e] = 1f;

        Assert.True(o.TryGetTelemetry(e, out var claimed) && claimed == e.Faction);
        Assert.True(o.IsHostileTo(e));
        Assert.Equal(1, o.VisibleEnemies.Count(x => x == e));
    }

    // The belief outlives the track only as long as a reveal younger than BearingMemory, and goes with the book.
    [Fact]
    public void TelemetryEndsWhenTheBearingIsForgotten()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        _zone.GalaxyZone.Owner = o.Faction;
        Forbid(e);
        var gun = Guns(e)[0];
        Fire(e, gun);
        ShowOnly(e, 1f, gun);
        o.EntityInfoGathered[e] = 1f;
        Assert.True(o.IsHostileTo(e), "fixture: tracked, believed hostile");

        o.EntityInfoGathered[e] = 0f; // the track is lost; the reveal is young
        Tick(.01f);
        Assert.False(o.ContactOf(e).Tracked, "fixture");
        Assert.True(o.IsHostileTo(e), "a young reveal keeps the belief");
        Assert.Contains(e, o.RememberedHostiles);

        for (var i = 0; i < 100 && o.RevealedSubjects.Contains(e); i++) Tick(.1f);
        Assert.False(o.RevealedSubjects.Contains(e), "fixture: the book dropped the subject");
        Assert.False(o.TryGetTelemetry(e, out _));
        Assert.False(o.IsHostileTo(e));
    }

    [Fact]
    public void TelemetryGoesWithADepartedSubjectsBook()
    {
        var e = Add(float3(0, 0, 100), new[] { "Gun" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });
        _zone.GalaxyZone.Owner = o.Faction;
        Forbid(e);
        var gun = Guns(e)[0];
        Fire(e, gun);
        ShowOnly(e, 1f, gun);
        o.EntityInfoGathered[e] = 1f;
        Assert.True(o.TryGetTelemetry(e, out _), "fixture");

        _zone.Remove(e);
        for (var i = 0; i < 100 && o.RevealedSubjects.Contains(e); i++) Tick(.1f);

        Assert.False(o.RevealedSubjects.Contains(e), "fixture: the book dropped the departed subject");
        Assert.False(o.TryGetTelemetry(e, out _));
    }

    private static int PaintInbox(Entity e) => ((System.Collections.ICollection) typeof(Entity).GetField("_paintInbox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(e)).Count;

    // A paint still unpublished in an observer's inbox when the observer leaves the zone is gone: it does not arrive as a
    // stale burst when the observer is admitted again.
    [Fact]
    public void AnObserverLeavingTheZoneLosesItsPendingPaints()
    {
        var p = Add(float3(0, 0, 100), new[] { "Eye" });
        var o = Add(float3(0, 0, 0));
        var sensor = (Sensor) p.Equipment.Single(i => i.Behaviors.Any(b => b is Sensor)).Behaviors.Single(b => b is Sensor);
        Tick(.01f);
        sensor.Ping();
        var pending = false;
        for (var tick = 0; tick < 400 && !pending; tick++) { Tick(.02f); pending = PaintInbox(o) > 0; }
        Assert.True(pending, "fixture: a paint is pending in o's inbox, not yet published");
        _zone.Remove(o);
        Assert.Equal(0, PaintInbox(o));
        Tick(.1f);
        _zone.Admit(o, piloted: false);
        Tick(.02f);
        Assert.Equal(0, o.IncomingPaints.Count);
    }
}
