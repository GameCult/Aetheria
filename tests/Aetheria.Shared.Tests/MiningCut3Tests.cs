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
        SchematicCellSize = 2f,
        FiringArc = 90f
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
        public GearData DimEye;
        public ZonePack Pack;
        public List<CultRecordKey> Belts = new List<CultRecordKey>();
        public List<FieldKindData> Kinds = new List<FieldKindData>();
    }

    // A belt around the sun: its chunks orbit the origin at their own distances, so at zone time 0 a chunk of
    // phase p sits at distance × (cos 2πp, sin 2πp). `kind` null leaves the belt kindless, for the zone to assign.
    private sealed class BeltSpec
    {
        public Asteroid[] Asteroids;
        public FieldKindData Kind;
    }

    private static Asteroid Rock(float distance, float phase = 0f, float size = .5f) =>
        new Asteroid { Distance = distance, Phase = phase, Size = size, RotationSpeed = .1f };

    private Scene BuildScene(FieldKindData[] kinds = null, params BeltSpec[] belts) => BuildSceneAt(float2.zero, kinds, belts);

    // The same scene with its sun, and so every belt centre, at `sunAt`.
    private Scene BuildSceneAt(float2 sunAt, FieldKindData[] kinds, params BeltSpec[] belts)
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
        // A far weaker sensor, for scenes of ships: a hull's own heat makes it hundreds of times brighter than a rock,
        // and the ordinary sensor would saturate on it at once.
        var dimEye = Gear("Dim eye", new SensorData
        {
            Sensitivity = Constant(.01f),
            SensitivityCurve = Falloff(),
            PingBoost = Constant(.02f),
            PingEnergy = Constant(0f),
            PingVisibility = Constant(5f),
            PingRange = Constant(300f),
            PingCooldown = Constant(4f)
        });
        cache.Upsert(dimEye);
        foreach (var kind in kinds ?? new FieldKindData[0]) cache.Upsert(kind);
        var items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { });

        var sunOrbit = cache.Upsert(new OrbitData { FixedPosition = sunAt }).Key;
        var sun = cache.Upsert(new SunData { Orbit = new CultRecordRef<OrbitData>(sunOrbit) }).Key;
        var pack = new ZonePack
        {
            Orbits = { new CultRecordRef<OrbitData>(sunOrbit) },
            Planets = { new CultRecordRef<BodyData>(sun) },
            Radius = 5000f,
            Mass = 10000f,
            Time = 0
        };
        var scene = new Scene { Cache = cache, Items = items, Hull = hull, Eye = eye, DimEye = dimEye, Pack = pack };
        scene.Kinds.AddRange(kinds ?? new FieldKindData[0]);
        foreach (var spec in belts)
        {
            var beltOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(sunOrbit), Distance = 150f }).Key;
            var belt = new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbit), Asteroids = spec.Asteroids };
            if (spec.Kind != null) belt.Kind = cache.RefOf(spec.Kind);
            var key = cache.Upsert(belt).Key;
            pack.Orbits.Add(new CultRecordRef<OrbitData>(beltOrbit));
            pack.Planets.Add(new CultRecordRef<BodyData>(key));
            scene.Belts.Add(key);
        }
        scene.Zone = new Zone(items, PlanetSettings(), pack, new GalaxyZone { Name = "MiningCut3", Owner = null }, null);
        return scene;
    }

    private static FieldKindData Kind(string name, float crossSection, float weight = 1f) =>
        new FieldKindData { Name = name, CrossSection = crossSection, GenerationWeight = weight };

    private static BeltSpec Belt(FieldKindData kind, params Asteroid[] asteroids) => new BeltSpec { Kind = kind, Asteroids = asteroids };

    // A chunk's planar position now (zone time never advances in these tests).
    private static float2 At(Scene s, ChunkId chunk) => s.Zone.ChunkPose(chunk.Field, chunk.Index).xy;

    // The rule as the map states it: kind cross-section × area in schematic cells (cell 2) × the light on it.
    private static float ExpectedVisibility(Scene s, ChunkId chunk, FieldKindData kind)
    {
        var cells = s.Zone.ChunkRadius(chunk) / 2f;
        return kind.CrossSection * PI * cells * cells * s.Zone.GetLight(At(s, chunk));
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
    // reflector of that cross-section (its own design, so ships in one scene can differ); each of `weaponRanges`
    // fits a gun of that range.
    private Ship SpawnShip(Scene s, float2 at, bool sensor = false, float crossSection = 0f, bool secondSensor = false, bool dimSensor = false, params float[] weaponRanges)
    {
        var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
        if (sensor) Assert.True(ship.TryEquip(Mint(s, s.Eye)));
        if (secondSensor) Assert.True(ship.TryEquip(Mint(s, s.Eye)));
        if (dimSensor) Assert.True(ship.TryEquip(Mint(s, s.DimEye)));
        if (crossSection > 0f)
        {
            var mirror = Gear($"Mirror {crossSection}", new ReflectorData { CrossSection = Constant(crossSection) });
            s.Cache.Upsert(mirror);
            Assert.True(ship.TryEquip(Mint(s, mirror)));
        }
        foreach (var range in weaponRanges)
        {
            var gun = Gear($"Gun {range}", new InstantWeaponData { Range = Constant(range) });
            s.Cache.Upsert(gun);
            Assert.True(ship.TryEquip(Mint(s, gun)));
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

    // The per-tick entity detection loop is unchanged by the extraction of its gain rule: a fixed scene (two ships
    // at different ranges and bearings, seen by a weak sensor so neither saturates, a ping part way through)
    // yields the same EntityInfoGathered trace, bit for bit, as it did before Sensor.Gain existed. The golden hash
    // was recorded against the pre-extraction Sensor.Execute (cb4a1dbd with this scene patched in).
    private const string Golden = "919B7EE7C5D1B40F774EE34283D62DE016862FE799B3EF16362D7FC75FD73C79";

    [Fact]
    public void SensorGainExtractionChangesNothing()
    {
        var s = BuildScene();
        var eyeAt = float2(0, 50);
        var nearAt = float2(30, 120);
        var farAt = float2(-150, 90);
        var observer = SpawnShip(s, eyeAt, dimSensor: true);
        var near = SpawnShip(s, nearAt, crossSection: .2f);
        var far = SpawnShip(s, farAt, crossSection: 9f);

        var trace = new List<byte>();
        var nearTrace = new List<float>();
        for (var tick = 0; tick < 60; tick++)
        {
            if (tick == 5) observer.Sensor.Ping();
            Tick((near, nearAt), (far, farAt), (observer, eyeAt));
            trace.AddRange(BitConverter.GetBytes(Bits(observer.EntityInfoGathered[near])));
            trace.AddRange(BitConverter.GetBytes(Bits(observer.EntityInfoGathered[far])));
            nearTrace.Add(observer.EntityInfoGathered[near]);
        }

        // Not a degenerate scene: both targets settle well short of saturation, and the ping visibly lifts the
        // near one above where it settles, so the trace carries the passive rule, the bearing and the ping.
        Assert.InRange(observer.EntityInfoGathered[near], .05f, .8f);
        Assert.InRange(observer.EntityInfoGathered[far], .05f, .8f);
        Assert.True(nearTrace.Max() > observer.EntityInfoGathered[near] + .1f, "the ping left no mark on the trace");
        var hash = Convert.ToHexString(SHA256.HashData(trace.ToArray()));
        Assert.True(hash == Golden, $"the EntityInfoGathered trace changed: hash {hash}");
    }

    // Q14 A's parity: one rule, two integrators. A ship carrying a reflector of the chunk's own cross-section
    // (the kind's per-cell reflectivity × the chunk's area in cells), held still where the chunk is, settles under
    // the per-tick loop at the value ChunkInfo returns on demand. The discrete loop's fixed point sits one decay
    // step below the continuous one, exactly: x* (1 - k dt). The observer is off the sensor's axis, so the
    // bearing term is live.
    [Fact]
    public void ChunkInfoMatchesAnEntityHeldStill()
    {
        var kind = Kind("Asteroid", 1f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var chunkAt = At(s, chunk);
        var eyeAt = float2(50, -150);
        var observer = SpawnShip(s, eyeAt, sensor: true);
        var cells = s.Zone.ChunkRadius(chunk) / 2f;
        var rock = SpawnShip(s, chunkAt, crossSection: kind.CrossSection * PI * cells * cells);

        for (var tick = 0; tick < 600; tick++) Tick((rock, chunkAt), (observer, eyeAt));

        var settled = observer.ChunkInfo(chunk);
        var perTick = observer.EntityInfoGathered[rock];
        Assert.InRange(settled, .12f, .9f); // visible, unsaturated: the balance, not the cap, decides it
        var discrete = settled * (1f - GameSettings().TargetInfoDecay * Dt);
        Assert.True(abs(perTick - discrete) <= 1e-3f * settled,
            $"per-tick loop settled at {perTick}, the on-demand value implies {discrete} (settled {settled})");
    }

    // A chunk outside every sun's light is dark: no visibility, no info, and it cannot be picked, while a lit
    // chunk of the same belt and kind can.
    [Fact]
    public void AChunkInDarknessIsNotVisible()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f), Rock(700f, .5f)));
        var lit = new ChunkId(s.Belts[0], 0);
        var dark = new ChunkId(s.Belts[0], 1);
        Assert.Equal(0f, s.Zone.GetLight(At(s, dark)));

        var inTheDark = SpawnShip(s, At(s, dark) + float2(0, -30), sensor: true, weaponRanges: 300f);
        Tick((inTheDark, At(s, dark) + float2(0, -30)));
        Assert.Equal(0f, s.Zone.ChunkVisibility(dark));
        Assert.Equal(0f, inTheDark.ChunkInfo(dark));
        Assert.False(inTheDark.SetTarget(dark));
        Assert.True(inTheDark.Target.Value.IsNone);
        var reachable = new List<ChunkId>();
        inTheDark.VisibleChunksInReach(reachable);
        Assert.DoesNotContain(dark, reachable);

        var inTheLight = SpawnShip(s, At(s, lit) + float2(0, -30), sensor: true);
        Tick((inTheLight, At(s, lit) + float2(0, -30))); // equipment comes online on its first tick
        Assert.True(inTheLight.SetTarget(lit));
        Assert.True(inTheLight.Target.Value.Equals(new TargetRef(lit)));
    }

    // Visibility is the kind's reflectivity × the chunk's area in cells × the light on it: linear in
    // cross-section, quadratic in radius, linear in light. Info follows visibility, so a brighter chunk is seen
    // further off.
    [Fact]
    public void ABrighterOrBiggerChunkIsSeenFurther()
    {
        var dim = Kind("Dim", 1f);
        var bright = Kind("Bright", 2f);
        var s = BuildScene(new[] { dim, bright },
            Belt(dim, Rock(150f, 0f, .2f), Rock(150f, .25f, .8f), Rock(250f, .5f, .2f)),
            Belt(bright, Rock(150f, 0f, .2f)));
        var small = new ChunkId(s.Belts[0], 0);
        var big = new ChunkId(s.Belts[0], 1);
        var far = new ChunkId(s.Belts[0], 2);
        var brightTwin = new ChunkId(s.Belts[1], 0);

        foreach (var (chunk, kind) in new[] { (small, dim), (big, dim), (far, dim), (brightTwin, bright) })
            Assert.Equal(ExpectedVisibility(s, chunk, kind), s.Zone.ChunkVisibility(chunk), 4);

        var vSmall = s.Zone.ChunkVisibility(small);
        Assert.Equal(2f, s.Zone.ChunkVisibility(brightTwin) / vSmall, 4);
        var radiusRatio = s.Zone.ChunkRadius(big) / s.Zone.ChunkRadius(small);
        Assert.True(radiusRatio > 1.5f);
        Assert.Equal(radiusRatio * radiusRatio, s.Zone.ChunkVisibility(big) / vSmall, 3);
        var lightRatio = s.Zone.GetLight(At(s, far)) / s.Zone.GetLight(At(s, small));
        Assert.InRange(lightRatio, .01f, .9f);
        Assert.Equal(lightRatio, s.Zone.ChunkVisibility(far) / vSmall, 3);

        // Seen further: from 180 away the dim twin is below the detection threshold and the bright one above it.
        var eyeAt = At(s, small) + float2(0, -180);
        var observer = SpawnShip(s, eyeAt, sensor: true);
        Tick((observer, eyeAt));
        Assert.Equal(2f, observer.ChunkInfo(brightTwin) / observer.ChunkInfo(small), 3);
        Assert.False(observer.ChunkVisible(small));
        Assert.True(observer.ChunkVisible(brightTwin));
    }

    // A worn chunk shrinks, so it dims by exactly its smaller area; a broken chunk has no size and is dark.
    [Fact]
    public void AWornChunkDims()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var whole = s.Zone.ChunkVisibility(chunk);
        var hitpoints = PlanetSettings().AsteroidHitpoints.Evaluate(.5f);

        Assert.False(s.Zone.Wear(chunk, hitpoints * .5f));
        var worn = s.Zone.ChunkVisibility(chunk);
        Assert.InRange(worn, whole * .05f, whole * .95f);
        Assert.Equal(ExpectedVisibility(s, chunk, kind), worn, 4);
    }

    [Fact]
    public void ABrokenChunkIsInvisible()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var eyeAt = At(s, chunk) + float2(0, -100);
        var observer = SpawnShip(s, eyeAt, sensor: true);
        Tick((observer, eyeAt));
        Assert.True(observer.ChunkVisible(chunk));

        Assert.True(s.Zone.Wear(chunk, PlanetSettings().AsteroidHitpoints.Evaluate(.5f) * 2f));
        Assert.Equal(0f, s.Zone.ChunkVisibility(chunk));
        Assert.Equal(0f, observer.ChunkInfo(chunk));
        Assert.False(observer.SetTarget(chunk));
    }

    // Q13 A: the reach for offering a chunk is the longest range among the entity's ACTIVE weapons. Two visible
    // chunks, 200 and about 300 away; guns of 250, 120 and 350 fitted in that order, the last switched off -- so the
    // last active gun is not the longest one.
    [Fact]
    public void CyclingReachIsTheLongestActiveWeaponRange()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind },
            Belt(kind, Rock(150f), Rock(length(float2(150, 100)), atan2(100f, 150f) / (2 * PI))));
        var near = new ChunkId(s.Belts[0], 0);
        var farther = new ChunkId(s.Belts[0], 1);
        var eyeAt = float2(150, -200);
        var observer = SpawnShip(s, eyeAt, sensor: true, weaponRanges: new[] { 250f, 120f, 350f });
        Tick((observer, eyeAt)); // weapons read their ranges when they run
        Assert.Equal(200f, length(At(s, near) - eyeAt), 2);
        Assert.Equal(300f, length(At(s, farther) - eyeAt), 2);
        Assert.True(observer.ChunkVisible(near));
        Assert.True(observer.ChunkVisible(farther));

        EquippedItem Gun(float range) => observer.Equipment.Single(item => item.Data.Name == $"Gun {range}");
        var offered = new List<ChunkId>();

        Gun(350f).Enabled.Value = false;
        observer.VisibleChunksInReach(offered);
        Assert.Equal(new[] { near }, offered);

        Gun(250f).Enabled.Value = false;
        observer.VisibleChunksInReach(offered);
        Assert.Empty(offered);

        Gun(350f).Enabled.Value = true;
        observer.VisibleChunksInReach(offered);
        Assert.Equal(2, offered.Count);
        Assert.Contains(near, offered);
        Assert.Contains(farther, offered);
    }

    // The chunk query returns exactly the chunks within range, from inside the belt's hole, from within the
    // annulus and from outside it -- the belt skip never drops a chunk the brute-force answer would keep -- and it
    // leaves out a broken chunk.
    [Fact]
    public void TheChunkQueryFindsExactlyTheChunksInRange()
    {
        var kind = Kind("Asteroid", 4f);
        var rocks = Enumerable.Range(0, 12).Select(i => Rock(200f + i * 15f, i / 12f)).ToArray();
        var centre = float2(400, -250); // off the origin, so a query that ignored the belt centre would disagree
        var s = BuildSceneAt(centre, new[] { kind }, Belt(kind, rocks));
        var belt = s.Belts[0];
        Assert.True(s.Zone.Wear(new ChunkId(belt, 5), 1e6f)); // broken

        var found = new List<ChunkId>();
        var sawSome = false;
        foreach (var (from, range) in new[]
                 {
                     (centre + float2(0, 0), 150f), (centre + float2(0, 0), 230f), (centre + float2(0, 0), 400f),
                     (centre + float2(220, 0), 40f), (centre + float2(-300, 50), 120f), (centre + float2(600, 0), 360f), (centre + float2(600, 0), 200f)
                 })
        {
            s.Zone.ChunksNear(from, range, found);
            var expected = Enumerable.Range(0, rocks.Length)
                .Where(i => i != 5 && length(At(s, new ChunkId(belt, i)) - from) <= range)
                .Select(i => new ChunkId(belt, i)).ToArray();
            Assert.Equal(expected, found);
            sawSome |= expected.Length > 0;
        }
        Assert.True(sawSome);
    }

    // The negative for Activate's "still tracked" reconciliation (Entity.Activate) and the per-tick lost-track
    // check: a visible chunk target survives ticks and a dock/undock, and reads its range through the chunk.
    [Fact]
    public void AChunkTargetSurvivesTheInfoPassWhileSeen()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var eyeAt = At(s, chunk) + float2(30, -100);
        var observer = SpawnShip(s, eyeAt, sensor: true);
        Tick((observer, eyeAt));
        Assert.True(observer.SetTarget(chunk));

        for (var tick = 0; tick < 5; tick++) Tick((observer, eyeAt));
        Assert.True(observer.Target.Value.Equals(new TargetRef(chunk)));
        Assert.Equal(length(float2(30, -100)), observer.TargetRange, 3);

        observer.Deactivate();
        observer.Activate();
        Assert.True(observer.Target.Value.Equals(new TargetRef(chunk)));
    }

    // The lost-track rule through the chunk path: a chunk target is dropped once this entity cannot see it --
    // on the next tick, or at Activate when it went dark while docked.
    [Fact]
    public void AChunkTargetIsDroppedWhenItGoesDark()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f), Rock(150f, .25f), Rock(150f, .5f)));
        var eyeAt = float2(0, 0);

        // Its own sensor goes off: nothing sees the chunk, and the next tick drops it.
        var blinded = SpawnShip(s, eyeAt, sensor: true);
        Tick((blinded, eyeAt));
        var first = new ChunkId(s.Belts[0], 0);
        Assert.True(blinded.SetTarget(first));
        blinded.Equipment.Single(item => item.Data.Name == "Eye").Enabled.Value = false;
        Tick((blinded, eyeAt));
        Assert.True(blinded.Target.Value.IsNone);

        // The chunk breaks: it is dark to everyone, and the next tick drops it.
        var watcher = SpawnShip(s, eyeAt, sensor: true);
        Tick((watcher, eyeAt));
        var second = new ChunkId(s.Belts[0], 1);
        Assert.True(watcher.SetTarget(second));
        Tick((watcher, eyeAt));
        Assert.False(watcher.Target.Value.IsNone);
        Assert.True(s.Zone.Wear(second, 1e6f));
        Tick((watcher, eyeAt));
        Assert.True(watcher.Target.Value.IsNone);

        // It breaks while the holder is docked: Activate drops it before any tick runs.
        var docked = SpawnShip(s, eyeAt, sensor: true);
        Tick((docked, eyeAt));
        var third = new ChunkId(s.Belts[0], 2);
        Assert.True(docked.SetTarget(third));
        docked.Deactivate();
        Assert.True(s.Zone.Wear(third, 1e6f));
        docked.Activate();
        Assert.True(docked.Target.Value.IsNone);
    }

    // One slot, two kinds, value equality: the same chunk is the same target, a chunk is never an entity, and an
    // empty target is None whichever way it was made.
    [Fact]
    public void TargetRefEquality()
    {
        var s = BuildScene();
        var ship = SpawnShip(s, float2(0, 0));
        var other = SpawnShip(s, float2(10, 0));
        var field = new CultRecordKey("field");
        var chunk = new ChunkId(field, 3);

        Assert.True(new TargetRef(chunk).Equals(new TargetRef(new ChunkId(new CultRecordKey("field"), 3))));
        Assert.Equal(new TargetRef(chunk).GetHashCode(), new TargetRef(new ChunkId(new CultRecordKey("field"), 3)).GetHashCode());
        Assert.False(new TargetRef(chunk).Equals(new TargetRef(new ChunkId(field, 4))));
        Assert.False(new TargetRef(chunk).Equals(new TargetRef(new ChunkId(new CultRecordKey("other"), 3))));

        Assert.True(new TargetRef(ship).Equals(new TargetRef(ship)));
        Assert.False(new TargetRef(ship).Equals(new TargetRef(other)));
        Assert.False(new TargetRef(ship).Equals(new TargetRef(chunk)));
        Assert.False(new TargetRef(chunk).Equals(new TargetRef(ship)));
        Assert.False(new TargetRef(ship).Equals(TargetRef.None));

        Assert.True(TargetRef.None.IsNone);
        Assert.True(new TargetRef((Entity) null).IsNone);
        Assert.True(new TargetRef((Entity) null).Equals(TargetRef.None));
        Assert.False(new TargetRef(default(ChunkId)).IsNone);
        Assert.False(new TargetRef(default(ChunkId)).Equals(TargetRef.None));
        Assert.False(new TargetRef(ship).IsNone);
        Assert.True(((object) new TargetRef(chunk)).Equals(new TargetRef(chunk)));
        Assert.Equal(new TargetRef(ship).GetHashCode(), new TargetRef(ship).GetHashCode());
        Assert.Equal(TargetRef.None.GetHashCode(), new TargetRef((Entity) null).GetHashCode());
    }

    // Q12 A: a launcher never locks a chunk. The same lock weapon, geometry and detection build lock on a
    // hostile ship; switched to a chunk, the lock drops to zero and stays there.
    [Fact]
    public void ChunkTargetIsNeverLocked()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var eyeAt = At(s, chunk) + float2(0, -100);
        var shooter = SpawnShip(s, eyeAt, sensor: true, weaponRanges: 300f);
        Tick((shooter, eyeAt));
        var enemy = SpawnShip(s, eyeAt + float2(0, 50));
        shooter.SetIff(enemy, true);
        shooter.EntityInfoGathered[enemy] = 1;
        shooter.LookDirection = float3(0, 0, 1);
        var launcher = new LockWeapon(new LockWeaponData
        {
            LockSpeed = Constant(.5f),
            LockAngle = Constant(180f),
            DirectionImpact = Constant(1f)
        }, shooter.Equipment.Single(item => item.Behaviors.Any(b => b is Weapon)));

        Assert.True(shooter.SetTarget(enemy));
        launcher.Execute(1f);
        Assert.True(launcher.Lock > 0f, "positive control: the launcher locks a hostile ship");

        Assert.True(shooter.SetTarget(chunk));
        launcher.Execute(1f);
        Assert.Equal(0f, launcher.Lock);
        launcher.Execute(1f);
        Assert.Equal(0f, launcher.Lock);
    }

    // Q16 B, the pick itself: a weighted choice by the belt's key -- the same key always gives the same kind, a
    // kind of weight zero is never chosen, and the others come up in proportion to their weights.
    [Fact]
    public void FieldKindAssignmentIsAWeightedPickByKey()
    {
        var light = Kind("Light", 1f, 1f);
        var never = Kind("Never", 1f, 0f);
        var middle = Kind("Middle", 1f, 2f);
        var heavy = Kind("Heavy", 1f, 3f);
        var s = BuildScene(new[] { light, never, middle, heavy });
        var counts = new Dictionary<string, int> { ["Light"] = 0, ["Never"] = 0, ["Middle"] = 0, ["Heavy"] = 0 };
        for (var i = 0; i < 2000; i++)
        {
            var key = new CultRecordKey($"belt-{i}");
            var picked = FieldKinds.Assign(key, s.Cache);
            Assert.True(picked.Key.Equals(FieldKinds.Assign(key, s.Cache).Key));
            counts[s.Cache.Get(picked).Name]++;
        }
        Assert.Equal(0, counts["Never"]);
        Assert.InRange(counts["Light"] / 2000f, 1f / 6 - .04f, 1f / 6 + .04f);
        Assert.InRange(counts["Middle"] / 2000f, 2f / 6 - .04f, 2f / 6 + .04f);
        Assert.InRange(counts["Heavy"] / 2000f, 3f / 6 - .04f, 3f / 6 + .04f);

        var empty = BuildScene(new[] { never });
        Assert.False(FieldKinds.Assign(new CultRecordKey("belt-0"), empty.Cache).IsSet());
    }

    // Q16 B, first load: a belt saved before field kinds existed gets its kind when a zone first loads it, by the
    // same function generation uses, and the assignment is saved -- so a later catalog edit that would pick a
    // different kind for that belt changes nothing about it.
    [Fact]
    public void FieldKindAssignmentIsOneFunction()
    {
        var a = Kind("A", 1f, 1f);
        var b = Kind("B", 2f, 1f);
        var s = BuildScene(new[] { a, b }, Belt(null, Rock(150f)));
        var beltKey = s.Belts[0];
        var belt = (AsteroidBeltData) s.Zone.Planets[beltKey];
        Assert.True(belt.Kind.IsSet());
        Assert.True(belt.Kind.Key.Equals(FieldKinds.Assign(beltKey, s.Cache).Key));
        var assigned = belt.Kind.Key;

        // Saved: a fresh open of the same stores reads the kind back.
        s.Cache.FlushAsync().Wait();
        s.Cache.Dispose();
        _caches.Remove(s.Cache);
        var dir = Path.Combine(_root, $"scene{_fixtureCount - 1}");
        var reopened = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        _caches.Add(reopened);
        Assert.True(((AsteroidBeltData) reopened.Get(beltKey)).Kind.Key.Equals(assigned));

        // Kept: a new, overwhelmingly weighted kind now wins the pick for this belt, yet loading it again leaves it.
        reopened.Upsert(Kind("Overwhelming", 9f, 1e6f));
        Assert.False(FieldKinds.Assign(beltKey, reopened).Key.Equals(assigned));
        var items = new ItemManager(reopened, new ProvenanceLedger(), GameSettings(), _ => { });
        var reloaded = new Zone(items, PlanetSettings(), s.Pack, new GalaxyZone { Name = "MiningCut3Reload", Owner = null }, null);
        Assert.True(((AsteroidBeltData) reloaded.Planets[beltKey]).Kind.Key.Equals(assigned));
    }

    // Q14 A with two sensors: each runs the per-tick rule on the same info, so each adds its gain and each applies
    // the decay. Two identical sensors settle exactly where one does -- the on-demand value divides the summed rate
    // by the number of sensors as well as by the decay.
    [Fact]
    public void ChunkInfoMatchesAnEntityHeldStillUnderTwoSensors()
    {
        var kind = Kind("Asteroid", 1f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var chunkAt = At(s, chunk);
        var eyeAt = float2(50, -150);
        var observer = SpawnShip(s, eyeAt, sensor: true, secondSensor: true);
        var cells = s.Zone.ChunkRadius(chunk) / 2f;
        var rock = SpawnShip(s, chunkAt, crossSection: kind.CrossSection * PI * cells * cells);

        for (var tick = 0; tick < 600; tick++) Tick((rock, chunkAt), (observer, eyeAt));

        var settled = observer.ChunkInfo(chunk);
        var perTick = observer.EntityInfoGathered[rock];
        Assert.InRange(settled, .12f, .9f);
        var discrete = settled * (1f - GameSettings().TargetInfoDecay * Dt);
        Assert.True(abs(perTick - discrete) <= 2e-3f * settled,
            $"per-tick loop settled at {perTick}, the on-demand value implies {discrete} (settled {settled})");
    }

    // The per-tick rule saturates info at 1, so the settled value is capped there too: a bright chunk close by is
    // fully known, not more than fully.
    [Fact]
    public void ChunkInfoIsCappedAtOne()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f)));
        var chunk = new ChunkId(s.Belts[0], 0);
        var eyeAt = At(s, chunk) + float2(0, -30);
        var observer = SpawnShip(s, eyeAt, sensor: true);
        Tick((observer, eyeAt));
        Assert.Equal(1f, observer.ChunkInfo(chunk));
    }

    // A chunk target bears through its own planar position: the arc gate passes a chunk ahead and refuses one off
    // to the side (a 90-degree arc facing +z), and a chunk has no stance to safe a weapon against.
    [Fact]
    public void AChunkTargetBearsThroughItsOwnPosition()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f, .25f), Rock(150f)));
        var ahead = new ChunkId(s.Belts[0], 0);  // (0, 150)
        var aside = new ChunkId(s.Belts[0], 1);  // (150, 0)
        var eyeAt = float2(-10, 50);             // ahead is at (10, 100) from here, aside at (160, -50)
        var shooter = SpawnShip(s, eyeAt, sensor: true, weaponRanges: 300f);
        Tick((shooter, eyeAt));
        var gun = shooter.Weapons.Single();

        Assert.True(shooter.SetTarget(ahead));
        Assert.True(gun.ArcAllowsFire);
        Assert.True(gun.StanceAllowsFire);

        Assert.True(shooter.SetTarget(aside));
        Assert.False(gun.ArcAllowsFire);
        Assert.True(gun.StanceAllowsFire);
    }

    // A writable copy of the shipped catalog plus a scratch run store, through a registry scoped to the shipped
    // assembly's own document types (RestoredHullsTests' composition): this test assembly's own catalog global
    // would otherwise be demanded of a catalog that was never authored with it.
    private static CultCache OpenShippedCatalogCopy(string catalogPath, string runPath)
    {
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttributes(typeof(CultDocumentAttribute), false).Length > 0));
        var cache = new CultCache(registry);
        cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(catalogPath, false), AetheriaStores.CatalogTypes);
        cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(runPath), AetheriaStores.RunTypes);
        return cache;
    }

    // Q16 B, generation: every belt ZoneGenerator makes carries its kind already, chosen by FieldKinds.Assign on
    // the belt's own key. Runs against a scratch copy of the shipped catalog with three kinds of different
    // weights, so a pick by any other rule would disagree on some belt.
    [Fact]
    public void GenerationChoosesTheKindByTheSameFunction()
    {
        var shipped = Path.Combine(RestoredHullsTests.FindRepoRoot(), "GameData", "Aetheria.cc");
        var belts = 0;
        for (uint seed = 1; seed <= 25 && belts < 3; seed++)
        {
            var dir = Path.Combine(_root, $"gen{seed}");
            Directory.CreateDirectory(dir);
            var catalog = Path.Combine(dir, "Aetheria.cc");
            File.Copy(shipped, catalog);
            using var cache = OpenShippedCatalogCopy(catalog, Path.Combine(dir, "run.cc"));
            foreach (var existing in cache.GetAll<FieldKindData>().ToArray())
            {
                existing.GenerationWeight = 0f;
                cache.Upsert(existing);
            }
            cache.Upsert(Kind("Gen A", 1f, 1f));
            cache.Upsert(Kind("Gen B", 1f, 2f));
            cache.Upsert(Kind("Gen C", 1f, 4f));

            Galaxy galaxy;
            try
            {
                galaxy = new Galaxy(RestoredHullsTests.TutorialGalaxySettings(), RestoredHullsTests.TutorialBackgroundSettings(),
                    RestoredHullsTests.TutorialNameSettings(), cache, new PlayerSettings(), new DirectoryInfo(dir), _ => { }, null, seed);
            }
            catch (Exception) { continue; } // Galaxy's own seed-dependent flakiness (RestoredHullsTests)
            var items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { });
            foreach (var zone in galaxy.Zones.Take(4))
            {
                var pack = ZoneGenerator.GenerateZone(items, RestoredHullsTests.TutorialZoneSettings(), galaxy, zone, isTutorial: zone == galaxy.Entrance);
                foreach (var body in pack.Planets)
                {
                    if (!(cache.Get(body) is AsteroidBeltData belt)) continue;
                    belts++;
                    Assert.True(belt.Kind.IsSet());
                    Assert.True(belt.Kind.Key.Equals(FieldKinds.Assign(body.Key, cache).Key));
                }
            }
        }
        Assert.True(belts > 0, "no seed generated a belt; the generation path went unexercised");
    }

    // ==== Mining Cut 3 fix batch (Soul's findings) ====

    // Q12 A ("launchers cannot mine") in the reach rule: reach counts only weapons that can mine (Weapon.CanMine).
    // Two visible rocks, 150 and 250 away. A launcher of range 400 alone is offered neither; a gun of range 200
    // beside it is offered the near one only, so the launcher's range still counts for nothing.
    [Fact]
    public void LaunchersDoNotCountTowardChunkReach()
    {
        var kind = Kind("Asteroid", 40f);
        var eyeAt = float2(150, -250);
        var nearAt = float2(150, -100);
        var s = BuildScene(new[] { kind }, Belt(kind, Rock(150f), Rock(length(nearAt), 1f + atan2(nearAt.y, nearAt.x) / (2 * PI))));
        var far = new ChunkId(s.Belts[0], 0);
        var near = new ChunkId(s.Belts[0], 1);
        Assert.Equal(250f, length(At(s, far) - eyeAt), 2);
        Assert.Equal(150f, length(At(s, near) - eyeAt), 2);
        var launcher = Gear("Launcher", new LockWeaponData { Range = Constant(400f), LockSpeed = Constant(.5f), LockAngle = Constant(30f), DirectionImpact = Constant(1f) });
        s.Cache.Upsert(launcher);
        var gun = Gear("Gun 200", new InstantWeaponData { Range = Constant(200f) });
        s.Cache.Upsert(gun);

        Ship Fit(params GearData[] weapons)
        {
            var ship = new Ship(s.Items, s.Zone, Mint(s, s.Hull), new EntitySettings());
            Assert.True(ship.TryEquip(Mint(s, s.Eye)));
            foreach (var weapon in weapons) Assert.True(ship.TryEquip(Mint(s, weapon)));
            s.Zone.Entities.Add(ship);
            Hold(ship, eyeAt);
            ship.LookDirection = float3(0, 0, 1);
            ship.Activate();
            Tick((ship, eyeAt)); // weapons read their ranges when they run
            return ship;
        }

        var offered = new List<ChunkId>();
        var launcherOnly = Fit(launcher);
        Assert.Equal(400f, launcherOnly.Weapons.Single().Range);
        Assert.True(launcherOnly.ChunkVisible(far));
        Assert.True(launcherOnly.ChunkVisible(near));
        launcherOnly.VisibleChunksInReach(offered);
        Assert.Empty(offered);

        var withGun = Fit(launcher, gun);
        withGun.VisibleChunksInReach(offered);
        Assert.Equal(new[] { near }, offered);
    }

    // Q13 A reads Active, not Enabled (Soul F8): a gun that is switched on but offline (worn out) adds no reach.
    [Fact]
    public void AnOfflineWeaponAddsNoReach()
    {
        var kind = Kind("Asteroid", 4f);
        var s = BuildScene(new[] { kind },
            Belt(kind, Rock(150f), Rock(length(float2(150, 100)), atan2(100f, 150f) / (2 * PI))));
        var near = new ChunkId(s.Belts[0], 0);
        var farther = new ChunkId(s.Belts[0], 1);
        var eyeAt = float2(150, -200);
        var observer = SpawnShip(s, eyeAt, sensor: true, weaponRanges: new[] { 250f, 350f });
        Tick((observer, eyeAt));
        var offered = new List<ChunkId>();
        observer.VisibleChunksInReach(offered);
        Assert.Equal(new[] { near, farther }, offered);

        var longGun = observer.Equipment.Single(item => item.Data.Name == "Gun 350");
        longGun.EquippableItem.Durability = 0f;
        Tick((observer, eyeAt));
        Assert.True(longGun.Enabled.Value);
        Assert.False(longGun.Active.Value);
        observer.VisibleChunksInReach(offered);
        Assert.Equal(new[] { near }, offered);
    }
}
