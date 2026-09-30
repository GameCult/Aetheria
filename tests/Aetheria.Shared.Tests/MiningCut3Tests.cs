/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Cut 3 (docs/mining-cut-refresh.md): a chunk is a target, detected by reflected light. One detection gain rule
// (Sensor.Gain) serves both the per-tick entity loop and the on-demand chunk query, and the chunk query returns
// the value the per-tick rule settles at (Q14 A). A belt's field kind is chosen once and saved (Q16 B).
public sealed class MiningCut3Tests : IDisposable
{
    private const float Dt = .05f;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-miningcut3-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();
    private int _fixtureCount;

    public MiningCut3Tests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static GameplaySettings GameSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        // Settings.asset's own values for the three detection tunables.
        VisibilityDecay = .5f,
        TargetInfoDecay = .5f,
        TargetDetectionInfoThreshold = .1f,
        SchematicCellSize = 2f
    };

    // Flat terrain (no zone depth, no gravity wells), one sun whose light reaches 1000 units, and the chunk
    // curves MiningCut2Tests uses, so sizes and wear vary over the fixture's range.
    private static PlanetSettings PlanetSettings() => new PlanetSettings
    {
        OrbitPeriod = new ExponentialCurve { Multiplier = 0.6f, Exponent = 1.3f, Constant = 40f },
        AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
        AsteroidHitpoints = new ExponentialLerp { Minimum = 20f, Maximum = 220f, Exponent = 1.6f },
        AsteroidRespawnTime = new ExponentialLerp { Minimum = 8f, Maximum = 96f, Exponent = 1.2f },
        BodyRadius = new ExponentialCurve(),
        GravityRadius = new ExponentialCurve(),
        GravityDepth = new ExponentialCurve(),
        WaveDepth = new ExponentialCurve(),
        WaveRadius = new ExponentialCurve(),
        WaveSpeed = new ExponentialCurve(),
        WaveFrequency = new ExponentialCurve(),
        LightRadius = new ExponentialCurve { Constant = 1000f }
    };

    // A sensitivity that falls off away from the sensor's facing, so the angle term is exercised, not a constant.
    private static BezierCurve Falloff() => new BezierCurve
    {
        Keys = new[] { new float4(0, 1, 0, 0), new float4(1, .3f, 0, 0) }
    };

    private sealed class Scene
    {
        public CultCache Cache;
        public ItemManager Items;
        public Zone Zone;
        public HullData Hull;
        public GearData Eye;
    }

    private Scene BuildScene()
    {
        var dir = Path.Combine(_root, $"scene{_fixtureCount++}");
        Directory.CreateDirectory(dir);
        var cache = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hull = new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10, Mass = 1000 };
        cache.Upsert(hull);
        var eye = Gear("Eye", new SensorData
        {
            Sensitivity = Constant(3f),
            SensitivityCurve = Falloff(),
            PingBoost = Constant(.02f),
            PingEnergy = Constant(0f),
            PingVisibility = Constant(5f),
            PingRange = Constant(300f),
            PingCooldown = Constant(4f)
        });
        cache.Upsert(eye);
        var items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { });

        var sunOrbit = cache.Upsert(new OrbitData()).Key;
        var sun = cache.Upsert(new SunData { Orbit = new CultRecordRef<OrbitData>(sunOrbit) }).Key;
        var pack = new ZonePack
        {
            Orbits = { new CultRecordRef<OrbitData>(sunOrbit) },
            Planets = { new CultRecordRef<BodyData>(sun) },
            Radius = 5000f,
            Mass = 10000f,
            Time = 0
        };
        var zone = new Zone(items, PlanetSettings(), pack, new GalaxyZone { Name = "MiningCut3", Owner = null }, null);
        return new Scene { Cache = cache, Items = items, Zone = zone, Hull = hull, Eye = eye };
    }

    private static GearData Gear(string name, BehaviorData behavior) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors = { behavior }
    };

    private static EquippableItem Mint(Scene s, ItemData design) => new EquippableItem
    {
        Data = s.Cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = s.Items.Lots.Add(new Lot { Design = s.Cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    // A ship held at a planar position, facing +z. `sensor` fits the scene's sensor; `crossSection` fits a
    // reflector of that cross-section (its own design, so ships in one scene can differ).
    private Ship SpawnShip(Scene s, float2 at, bool sensor = false, float crossSection = 0f)
    {
        var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
        if (sensor) Assert.True(ship.TryEquip(Mint(s, s.Eye)));
        if (crossSection > 0f)
        {
            var mirror = Gear($"Mirror {crossSection}", new ReflectorData { CrossSection = Constant(crossSection) });
            s.Cache.Upsert(mirror);
            Assert.True(ship.TryEquip(Mint(s, mirror)));
        }
        s.Zone.Entities.Add(ship);
        Hold(ship, at);
        ship.LookDirection = float3(0, 0, 1);
        ship.Activate();
        return ship;
    }

    private static void Hold(Entity entity, float2 at)
    {
        entity.Position = float3(at.x, entity.Position.y, at.y);
        entity.Velocity = float2.zero;
        entity.Direction = float2(0, 1);
    }

    // One simulation tick for the given entities, each held in place, in order. Zone time does not advance, so
    // chunk poses and sun positions stay where they are.
    private static void Tick(params (Entity entity, float2 at)[] held)
    {
        foreach (var (entity, at) in held)
        {
            Hold(entity, at);
            entity.Update(Dt);
        }
    }

    private static uint Bits(float value) => BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);

    // The per-tick entity detection loop is unchanged by the extraction of its gain rule: a fixed scene (two
    // reflecting ships at different ranges and bearings, a ping part way through) yields the same
    // EntityInfoGathered trace, bit for bit, as it did before Sensor.Gain existed. The golden hash was recorded
    // against the pre-extraction Sensor.Execute.
    private const string Golden = "6C23DF2584646E976A11CB0B8013A18499F5DCCABD86C7A840F201899984D737";

    [Fact]
    public void SensorGainExtractionChangesNothing()
    {
        var s = BuildScene();
        var eyeAt = float2(0, 50);
        var nearAt = float2(30, 120);
        var farAt = float2(-200, 90);
        var observer = SpawnShip(s, eyeAt, sensor: true);
        var near = SpawnShip(s, nearAt, crossSection: 40f);
        var far = SpawnShip(s, farAt, crossSection: 400f);

        var trace = new List<byte>();
        for (var tick = 0; tick < 60; tick++)
        {
            if (tick == 5) observer.Sensor.Ping();
            Tick((near, nearAt), (far, farAt), (observer, eyeAt));
            trace.AddRange(BitConverter.GetBytes(Bits(observer.EntityInfoGathered[near])));
            trace.AddRange(BitConverter.GetBytes(Bits(observer.EntityInfoGathered[far])));
        }

        // Not a degenerate scene: both targets gathered some info, neither saturated.
        Assert.InRange(observer.EntityInfoGathered[near], .01f, .99f);
        Assert.InRange(observer.EntityInfoGathered[far], .01f, .99f);
        var hash = Convert.ToHexString(SHA256.HashData(trace.ToArray()));
        Assert.True(hash == Golden, $"the EntityInfoGathered trace changed: hash {hash}");
    }
}
