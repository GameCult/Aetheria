using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Mines in the sim (aetheria-release cut sim-mines): a mine layer lays a free-floating body in the zone that
// drifts, arms, triggers on a hostile hull its blast disc touches and detonates through FireControl.Detonate.
// Rulings: mines-float-like-loot, mine-trigger-iff (hostile trigger, faction-blind blast), mine-owner-leash
// (no leash), one-gravity-law. SchematicCellSize is 2 throughout so a missing cell-size division shows.
public sealed class MineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-mines-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public MineTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var c in _caches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static readonly Faction A = new Faction { Name = "A" };
    private static readonly Faction B = new Faction { Name = "B" };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static Shape Solid(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // A bottom row and a left column of a 5x5 grid: its centre of mass sits well away from its far end.
    private static Shape Ell()
    {
        var shape = new Shape(5, 5);
        for (var i = 0; i < 5; i++) { shape[new int2(i, 0)] = true; shape[new int2(0, i)] = true; }
        return shape;
    }

    private sealed class Lab
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Layer;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
        public MineLayerData Data;
        public WeaponItemData ItemData;
        public int Lot = 1;
        public EquippableItem Make(string name, float durability) => new EquippableItem
        {
            Data = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<GearData>(name)), Durability = durability, Lot = Lot++
        };
    }

    private Lab Build(float blast = 4f, float arming = 2f, float fuse = 2f, float life = 30f, float spread = 0f, float velocity = 0f,
        float damage = 100f, float range = 1000f, float cooldown = 1000f, Faction layerFaction = null,
        WeaponFuse? fuseKind = WeaponFuse.Proximity, bool well = false, float3? layerAt = null, int magazine = 100)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetArmorInfoThreshold = .2f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 2f,
            UnaidedAccuracy = .05f, UnaidedTracking = 10f, UnaidedPrecision = 1000f, AgentMinHitProbability = 0f
        };
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        HullData Hull(string name, Shape shape) => new HullData
        {
            Name = name, HullType = HullType.Ship, Shape = shape, Durability = 1000000, Mass = 1000, Armor = 0,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        cache.Upsert(Hull("Solid", Solid(5, 5)));
        cache.Upsert(Hull("Ell", Ell()));
        cache.Upsert(Hull("Layer", Solid(5, 5)));
        var data = new MineLayerData
        {
            ArmingDelay = arming, FuseDelay = fuse, Lifetime = life,
            Damage = Constant(damage), Range = Constant(range), MinRange = Constant(0), Velocity = Constant(velocity),
            Spread = Constant(spread), DamageSpread = Constant(0), Penetration = Constant(0), Count = Constant(1),
            BurstTime = Constant(0), Cooldown = Constant(cooldown), MagazineSize = magazine,
            DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
        };
        cache.Upsert(new WeaponItemData
        {
            Name = "Mine Layer", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Fuse = fuseKind, BlastRadius = blast, Behaviors = { data }
        });
        GearData Gear(string name, BehaviorData behavior, float durability) => new GearData
        {
            Name = name, Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = durability,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { behavior }
        };
        cache.Upsert(Gear("Targeting", new TargetingSystemData { Accuracy = Constant(1), Resolution = Constant(1000f), Precision = Constant(1000f), Tracking = Constant(1000000f) }, 1));
        cache.Upsert(Gear("Reactor", new ReactorData { Charge = Constant(1000), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) }, 10));
        cache.Upsert(Gear("Shield", new ShieldData { Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(1000000), RefillDuration = Constant(.01f), RestoreDuration = Constant(.01f) }, 10));
        cache.Upsert(Gear("TinyShield", new ShieldData { Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(.001f), RefillDuration = Constant(.01f), RestoreDuration = Constant(.01f) }, 10));
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 500; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var planets = well ? new PlanetSettings { ZoneDepth = 1000, ZoneDepthExponent = 1, GravityStrength = 1 } : new PlanetSettings();
        var zone = new Zone(items, planets, new ZonePack { Radius = well ? 1000 : 0 }, new GalaxyZone { Name = "Mines", Owner = null }, null);
        var lab = new Lab { Items = items, Zone = zone, Data = data, ItemData = cache.GetByName<WeaponItemData>("Mine Layer") };

        var layerRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Layer"));
        var layer = new Ship(items, zone, new EquippableItem { Data = layerRef, Durability = 1000000, Lot = lab.Lot++ }, new EntitySettings()) { Faction = layerFaction };
        var gun = new EquippableItem { Data = items.ItemData.RefOf<ItemData>(lab.ItemData), Durability = 1, Lot = lab.Lot++ };
        Assert.True(layer.TryEquip(gun, new int2(0, 0)));
        lab.WeaponItem = layer.Equipment.Single(x => x.EquippableItem == gun);
        lab.Weapon = (InstantWeapon) lab.WeaponItem.Behaviors.Single(b => b is Weapon);
        var targeting = lab.Make("Targeting", 1);
        Assert.True(layer.TryFindSpace(targeting, out var tpos));
        Assert.True(layer.TryEquip(targeting, tpos));
        zone.Entities.Add(layer);
        layer.Activate();
        layer.Position = layerAt ?? float3(-53, 0, -101);
        lab.Layer = layer;
        zone.Update(1f);
        return lab;
    }

    private static Ship Add(Lab lab, string hull, Faction faction, float3 at, string shield = null)
    {
        var items = lab.Items;
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>(hull));
        var ship = new Ship(items, lab.Zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = lab.Lot++ }, new EntitySettings()) { Faction = faction };
        if (shield != null)
        {
            var reactor = lab.Make("Reactor", 10);
            Assert.True(ship.TryFindSpace(reactor, out var rpos));
            Assert.True(ship.TryEquip(reactor, rpos));
            var gear = lab.Make(shield, 10);
            Assert.True(ship.TryFindSpace(gear, out var spos));
            Assert.True(ship.TryEquip(gear, spos));
        }
        lab.Zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = at;
        if (shield != null) ship.Shield.Item.Enabled.Value = true;
        return ship;
    }

    // One trigger pull: the layer lays through InstantWeapon -> FireControl.Fire, the production path.
    private static Mine LayOne(Lab lab, float dt = .1f)
    {
        var before = lab.Zone.Mines.Count;
        lab.Weapon.Activate();
        lab.Zone.Update(dt);
        Assert.Equal(before + 1, lab.Zone.Mines.Count);
        return lab.Zone.Mines.Last();
    }

    // A mine placed by hand through the public adder, for the geometry and drift tests.
    private static Mine Place(Lab lab, float2 at, float blast, float arming = 0f, float fuse = 100f, float life = 1000f,
        float2? velocity = null)
    {
        var mine = new Mine
        {
            Id = lab.Zone.NextBodyId(), Body = new KinematicBody { Position = at, Velocity = velocity ?? float2.zero },
            ArmingDelay = arming, FuseDelay = fuse, Lifetime = life, BlastRadius = blast, Damage = 100f,
            DamageType = DamageType.Kinetic, Layer = lab.Layer, LayerFaction = lab.Layer.Faction
        };
        lab.Zone.Lay(mine);
        return mine;
    }

    private static IEnumerable<float> Steps(float seconds, float dt) => Enumerable.Repeat(dt, (int) Math.Round(seconds / dt));

    [Theory]
    [InlineData(1f / 60f)]
    [InlineData(.1f)]
    public void AMineArmsAfterItsDelay(float dt)
    {
        var lab = Build(arming: 2f, fuse: 50f, life: 100f, layerFaction: A);
        Add(lab, "Solid", B, lab.Layer.Position);
        var mine = LayOne(lab, dt);
        var armAt = mine.LaidAt + 2.0;

        double? first = null;
        foreach (var step in Steps(4f, dt))
        {
            lab.Zone.Update(step);
            var now = (double) lab.Zone.Time;
            if (mine.TriggeredAt == null) Assert.True(now < armAt + 1e-3, "an armed mine with a hostile in its disc must trigger");
            else
            {
                first ??= now;
                Assert.True(now >= armAt - 1e-3, "a mine must not trigger before its arming delay");
            }
        }
        Assert.NotNull(first);
        Assert.InRange(first.Value, armAt - 1e-3, armAt + dt + 1e-3);
    }

    // The far end of the L is the one occupied cell inside the disc; its centre of mass is not.
    [Theory]
    [InlineData(5.4f, true)]
    [InlineData(5.6f, false)]
    public void ItTriggersOnAHullTheDiscTouches(float schematicX, bool touches)
    {
        var lab = Build(layerFaction: A);
        var ell = Add(lab, "Ell", B, float3(300, 0, 300));
        var at = ell.ToWorldPoint(float2(schematicX, 0));
        Assert.True(length(at - ell.Position.xz) > 2f, "the hull's centre of mass must lie outside the blast disc");

        var mine = Place(lab, at, 2f);
        lab.Zone.Update(.1f);
        lab.Zone.Update(.1f);

        Assert.Equal(touches, mine.TriggeredAt != null);
        Assert.Equal(touches, FireControl.Touches(ell, at, 2f));
        // Touches is Detonate's own coverage: the disc deals damage to the hull exactly when it touches it.
        var before = ell.Hull.Durability;
        FireControl.Detonate(lab.Zone, at, 2f, 100f, DamageType.Kinetic);
        Assert.Equal(touches, ell.Hull.Durability < before);
    }

    [Theory]
    [InlineData("A", "A", false)]  // the layer's faction mates never trigger it
    [InlineData("A", "B", true)]
    [InlineData("A", null, true)]  // a factionless hull is no faction mate of anyone
    [InlineData("B", "A", true)]
    [InlineData(null, null, true)]  // a factionless layer has no faction mates either
    [InlineData(null, "A", true)]
    public void OnlyHostilesTriggerAMine(string layerFaction, string other, bool triggers)
    {
        Faction Of(string name) => name == "A" ? A : name == "B" ? B : null;
        var lab = Build(arming: 1f, fuse: 50f, life: 100f, layerFaction: Of(layerFaction));
        Add(lab, "Solid", Of(other), lab.Layer.Position);
        var mine = LayOne(lab);

        foreach (var step in Steps(5f, .1f)) lab.Zone.Update(step);

        Assert.Equal(triggers, mine.TriggeredAt != null);
    }

    [Fact]
    public void TheLayerNeverTriggersItsOwnMineButItsBlastHurtsIt()
    {
        var lab = Build(arming: 1f, fuse: 1f, life: 4f, layerFaction: A, damage: 100f);
        var mine = LayOne(lab);
        var durability = lab.Layer.Hull.Durability;
        var removed = 0;
        using var sub = lab.Zone.Mines.ObserveRemove().Subscribe(_ => removed++);

        foreach (var step in Steps(3.5f, .1f))
        {
            lab.Zone.Update(step);
            Assert.Null(mine.TriggeredAt);
        }
        Assert.Equal(0, removed);
        Assert.Equal(durability, lab.Layer.Hull.Durability);

        foreach (var step in Steps(1f, .1f)) lab.Zone.Update(step);

        // The untriggered mine blew at its lifetime, and the blast asked no faction: the layer in its disc took it.
        Assert.Equal(1, removed);
        Assert.Empty(lab.Zone.Mines);
        Assert.True(lab.Layer.Hull.Durability < durability);
    }

    [Theory]
    [InlineData(.1f)]
    [InlineData(1f / 60f)]
    public void ATriggeredMineDetonatesAfterItsFuseDelay(float dt)
    {
        var lab = Build(arming: 1f, fuse: 2f, life: 100f, layerFaction: A, damage: 100f);
        var hostile = Add(lab, "Solid", B, lab.Layer.Position);
        var bystander = Add(lab, "Solid", A, lab.Layer.Position);
        var mine = LayOne(lab, dt);
        var removedAt = -1.0;
        using var sub = lab.Zone.Mines.ObserveRemove().Subscribe(_ => removedAt = lab.Zone.Time);

        var left = false;
        foreach (var step in Steps(5f, dt))
        {
            lab.Zone.Update(step);
            if (mine.TriggeredAt != null && !left)
            {
                // The trigger leaves the disc at once; the countdown does not care.
                hostile.Position = float3(5000, 0, 5000);
                left = true;
            }
        }

        Assert.NotNull(mine.TriggeredAt);
        Assert.InRange(removedAt, mine.TriggeredAt.Value + 2.0 - 1e-3, mine.TriggeredAt.Value + 2.0 + dt + 1e-3);
        Assert.Equal(1000000f, hostile.Hull.Durability);
        Assert.True(bystander.Hull.Durability < 1000000f, "the blast damages whoever the disc covers then, its own side included");
    }

    [Fact]
    public void AZeroFuseDelayDetonatesInTheTriggeringStep()
    {
        var lab = Build(arming: 1f, fuse: 0f, life: 100f, layerFaction: A);
        Add(lab, "Solid", B, lab.Layer.Position);
        var mine = LayOne(lab);
        var removedAt = -1.0;
        using var sub = lab.Zone.Mines.ObserveRemove().Subscribe(_ => removedAt = lab.Zone.Time);

        foreach (var step in Steps(3f, .1f)) lab.Zone.Update(step);

        Assert.NotNull(mine.TriggeredAt);
        Assert.InRange(removedAt, mine.LaidAt + 1.0 - 1e-3, mine.LaidAt + 1.1 + 1e-3);
        Assert.Equal(removedAt, mine.TriggeredAt.Value, 4);
    }

    [Fact]
    public void AnUntriggeredMineDetonatesAtItsLifetimeExactlyOnce()
    {
        var lab = Build(arming: 1f, fuse: 1f, life: 5f, layerFaction: A, damage: 100f);
        var ally = Add(lab, "Solid", A, lab.Layer.Position);
        var mine = LayOne(lab);
        var removals = new List<float>();
        using var sub = lab.Zone.Mines.ObserveRemove().Subscribe(_ => removals.Add(lab.Zone.Time));
        float? hurtAt = null;

        foreach (var step in Steps(8f, .1f))
        {
            lab.Zone.Update(step);
            if (hurtAt == null && ally.Hull.Durability < 1000000f) hurtAt = lab.Zone.Time;
        }

        Assert.Null(mine.TriggeredAt);
        Assert.Single(removals);
        Assert.InRange(removals[0], mine.LaidAt + 5.0 - 1e-3, mine.LaidAt + 5.1 + 1e-3);
        Assert.Equal(removals[0], hurtAt);
    }

    // Two zones built alike; one mine blast in the first, one direct Detonate at the same point and payload in the
    // second. Entities: the layer, wholly inside, at the edge (a proportionally smaller share), a shield that
    // absorbs, and a shield that breaks.
    private static List<Ship> Populate(Lab lab)
    {
        var at = lab.Layer.Position;
        return new List<Ship>
        {
            lab.Layer,
            Add(lab, "Solid", B, at + float3(1, 0, 1)),
            Add(lab, "Solid", B, at + float3(15, 0, 0)),
            Add(lab, "Solid", B, at + float3(0, 0, -14), "Shield"),
            Add(lab, "Solid", B, at + float3(-14, 0, 0), "TinyShield")
        };
    }

    [Fact]
    public void TheBlastIsDetonate()
    {
        var a = Build(blast: 12f, arming: 5f, life: 1f, damage: 5000f, layerFaction: A);
        var b = Build(blast: 12f, arming: 5f, life: 1f, damage: 5000f, layerFaction: A);
        var shipsA = Populate(a);
        var shipsB = Populate(b);
        var removed = new List<Mine>();
        using var sub = a.Zone.Mines.ObserveRemove().Subscribe(e => removed.Add(e.Value));

        LayOne(a);
        foreach (var step in Steps(2f, .1f)) a.Zone.Update(step);
        Assert.Single(removed);
        var mine = removed[0];
        FireControl.Detonate(b.Zone, mine.Body.Position, mine.BlastRadius, mine.Damage, mine.DamageType);

        for (var i = 0; i < shipsA.Count; i++)
        {
            Assert.Equal(shipsB[i].Hull.Durability, shipsA[i].Hull.Durability);
        }
        // Wholly inside and the layer are hurt; the edge ship is hurt less; the absorbing shield stops it; the
        // tiny shield breaks and its hull takes the share.
        Assert.True(shipsA[1].Hull.Durability < shipsA[2].Hull.Durability);
        Assert.True(shipsA[2].Hull.Durability < 1000000f);
        Assert.True(shipsA[0].Hull.Durability < 1000000f);
        Assert.Equal(1000000f, shipsA[3].Hull.Durability);
        Assert.True(shipsA[4].Hull.Durability < 1000000f);
    }

    [Fact]
    public void LayingFiresNoShot()
    {
        var lab = Build(spread: 40f, velocity: 5f, layerFaction: A, magazine: 5);
        lab.Layer.Velocity = float2(3, 1);
        float2 shooterVelocity = float2.zero, shooterAt = float2.zero;
        lab.Weapon.OnFire += _ => { shooterVelocity = lab.Layer.Velocity; shooterAt = lab.Layer.Position.xz; };
        var atLay = default(KinematicBody);
        using var sub = lab.Zone.Mines.ObserveAdd().Subscribe(e => atLay = e.Value.Body);
        var ammo = lab.Weapon.Ammo;
        Assert.True(lab.Weapon.CanFire);

        var mine = LayOne(lab);

        Assert.Empty(lab.Zone.PendingShots);
        Assert.Equal(ammo - 1, lab.Weapon.Ammo);
        Assert.False(lab.Weapon.CanFire);
        Assert.True(lab.Layer.VisibilitySources.ContainsKey(lab.Weapon));
        Assert.Same(lab.Layer, mine.Layer);
        Assert.Equal(shooterAt, atLay.Position);
        var relative = atLay.Velocity - shooterVelocity;
        Assert.Equal(5f, length(relative), 3);
        var mount = FireControl.MountDirection(lab.WeaponItem).xz;
        var angle = degrees(acos(clamp(dot(normalize(relative), mount), -1f, 1f)));
        Assert.InRange(angle, 0f, 20f + 1e-2f);
    }

    [Fact]
    public void ASpreadlessLayerLaysDownTheMount()
    {
        var lab = Build(spread: 0f, velocity: 5f, layerFaction: A);
        lab.Layer.Velocity = float2(3, 1);
        var shooterVelocity = float2.zero;
        lab.Weapon.OnFire += _ => shooterVelocity = lab.Layer.Velocity;
        var atLay = default(KinematicBody);
        using var sub = lab.Zone.Mines.ObserveAdd().Subscribe(e => atLay = e.Value.Body);

        LayOne(lab);

        var expected = shooterVelocity + FireControl.MountDirection(lab.WeaponItem).xz * 5f;
        Assert.Equal(expected.x, atLay.Velocity.x, 4);
        Assert.Equal(expected.y, atLay.Velocity.y, 4);
    }

    [Fact]
    public void LayingIsDeterministicAndFillsItsSpread()
    {
        var a = Build(spread: 40f, velocity: 5f, cooldown: .01f, layerFaction: A);
        var b = Build(spread: 40f, velocity: 5f, cooldown: .01f, layerFaction: A);
        var angles = new List<float>();
        using var sub = a.Zone.Mines.ObserveAdd().Subscribe(e =>
        {
            var v = normalize(e.Value.Body.Velocity);
            angles.Add(degrees(atan2(v.x, v.y)));
        });

        for (var i = 0; i < 24; i++)
        {
            // The item manager's own stream is drawn from between the lays in B: a mine's roll is its own.
            b.Items.Random.NextFloat();
            b.Items.Random.NextFloat();
            foreach (var lab in new[] { a, b })
            {
                lab.Weapon.Activate();
                lab.Zone.Update(.1f);
                lab.Zone.Update(.1f);
            }
            Assert.Equal(a.Zone.Mines.Count, b.Zone.Mines.Count);
            Assert.Equal(i + 1, a.Zone.Mines.Count);
            for (var m = 0; m < a.Zone.Mines.Count; m++)
            {
                Assert.Equal(a.Zone.Mines[m].Id, b.Zone.Mines[m].Id);
                Assert.Equal(a.Zone.Mines[m].Body.Position, b.Zone.Mines[m].Body.Position);
                Assert.Equal(a.Zone.Mines[m].Body.Velocity, b.Zone.Mines[m].Body.Velocity);
                Assert.Equal(a.Zone.Mines[m].LaidAt, b.Zone.Mines[m].LaidAt);
            }
        }

        Assert.Equal(24, angles.Count);
        Assert.True(angles.Distinct().Count() > 12, "every mine rolls its own angle");
        Assert.All(angles, x => Assert.InRange(x, -20f - 1e-2f, 20f + 1e-2f));
        Assert.True(angles.Max() > 10f && angles.Min() < -10f, "the angle spans +-Spread/2, not a narrower cone");
    }

    [Fact]
    public void ThePayloadIsFrozen()
    {
        var lab = Build(blast: 12f, arming: 2f, fuse: 2f, life: 3f, damage: 5000f, layerFaction: A);
        var twin = Build(blast: 12f, arming: 2f, fuse: 2f, life: 3f, damage: 5000f, layerFaction: A);
        var mine = LayOne(lab);
        var frozen = (mine.ArmingDelay, mine.FuseDelay, mine.Lifetime, mine.BlastRadius, mine.Damage, mine.DamageType, mine.Layer, mine.LayerFaction);
        var position = mine.Body.Position;

        lab.Data.ArmingDelay = 99f;
        lab.Data.FuseDelay = 99f;
        lab.Data.Lifetime = 999f;
        lab.Data.Damage = Constant(1f);
        lab.ItemData.BlastRadius = 1f;
        lab.Layer.Faction = B;
        lab.Zone.Update(.1f);
        lab.Zone.Update(.1f);

        Assert.Equal(frozen, (mine.ArmingDelay, mine.FuseDelay, mine.Lifetime, mine.BlastRadius, mine.Damage, mine.DamageType, mine.Layer, mine.LayerFaction));
        var durability = lab.Layer.Hull.Durability;
        foreach (var step in Steps(4f, .1f)) lab.Zone.Update(step);
        Assert.Empty(lab.Zone.Mines);

        var before = twin.Layer.Hull.Durability;
        FireControl.Detonate(twin.Zone, position, 12f, 5000f, DamageType.Kinetic);
        Assert.True(before > twin.Layer.Hull.Durability);
        Assert.Equal(before - twin.Layer.Hull.Durability, durability - lab.Layer.Hull.Durability, 2);
    }

    [Fact]
    public void AMineDriftsAsABody()
    {
        var lab = Build(well: true, layerAt: float3(400, 0, 0));
        var settings = lab.Items.GameplaySettings;
        var start = float2(400, 20);
        var launch = float2(1.5f, -.5f);
        var mine = Place(lab, start, 4f, arming: 1000f, life: 100000f, velocity: launch);
        var solo = new KinematicBody { Position = start, Velocity = launch };
        Assert.NotEqual(float2.zero, lab.Zone.GetForce(start));

        foreach (var step in Steps(3f, 1f / 30f))
        {
            lab.Zone.Update(step);
            solo.Step(step, lab.Zone.GetForce(solo.Position), settings);
            Assert.Equal(solo.Position.x, mine.Body.Position.x, 4);
            Assert.Equal(solo.Position.y, mine.Body.Position.y, 4);
            Assert.Equal(solo.Velocity.x, mine.Body.Velocity.x, 4);
            Assert.Equal(solo.Drift.x, mine.Body.Drift.x, 4);
            Assert.Equal(solo.Drift.y, mine.Body.Drift.y, 4);
        }
        Assert.True(length(solo.Drift) > 0f);
    }

    [Fact]
    public void TheLeashIsTheRuling()
    {
        var lab = Build(arming: 1f, fuse: 1f, life: 6f, range: 100f, layerFaction: A);
        var mine = LayOne(lab);
        var removed = 0;
        using var sub = lab.Zone.Mines.ObserveRemove().Subscribe(_ => removed++);

        foreach (var step in Steps(1.5f, .1f)) lab.Zone.Update(step);
        Assert.True(mine.Armed(lab.Zone.Time));
        // The layer flies far beyond its Range, then dies: the mine neither notices nor cares.
        lab.Layer.Position = float3(5000, 0, 5000);
        foreach (var step in Steps(1f, .1f)) lab.Zone.Update(step);
        lab.Layer.DamageHull(5000000f);
        Assert.DoesNotContain(lab.Layer, lab.Zone.Entities);
        foreach (var step in Steps(1.5f, .1f)) lab.Zone.Update(step);

        Assert.Equal(0, removed);
        Assert.Single(lab.Zone.Mines);
        Assert.Null(mine.TriggeredAt);

        foreach (var step in Steps(3f, .1f)) lab.Zone.Update(step);
        Assert.Equal(1, removed);
        Assert.Empty(lab.Zone.Mines);
    }

    [Fact]
    public void AgentsLayOnlyAtATarget()
    {
        var lab = Build(range: 100f, layerFaction: A);
        var near = Add(lab, "Solid", B, lab.Layer.Position + float3(0, 0, 40));
        var far = Add(lab, "Solid", B, lab.Layer.Position + float3(0, 0, 500));
        foreach (var target in new[] { near, far })
        {
            lab.Layer.SetTarget(target);
            lab.Layer.EntityInfoGathered[target] = 1f;
            lab.Layer.SetIff(target, true);
        }
        lab.Zone.Update(1f);

        Assert.False(FireControl.AgentFires(lab.Weapon, lab.Layer, null));
        Assert.False(FireControl.AgentFires(lab.Weapon, lab.Layer, far));
        Assert.True(FireControl.AgentFires(lab.Weapon, lab.Layer, near));
        Assert.False(FireControl.Refuses(lab.Weapon, lab.Layer));
    }

    [Fact]
    public void MinesAreNotSaved()
    {
        var lab = Build();
        LayOne(lab);

        var pack = lab.Zone.PackZone();
        var restored = new Zone(lab.Items, new PlanetSettings(), pack, new GalaxyZone { Name = "Mines", Owner = null }, null);

        Assert.Single(lab.Zone.Mines);
        Assert.Empty(restored.Mines);
    }

    [Fact]
    public void ALayerWithoutABlastRadiusLaysNothing()
    {
        var lab = Build(blast: 0f, layerFaction: A);
        var log = new List<string>();
        lab.Zone.Log = log.Add;

        lab.Weapon.Activate();
        lab.Zone.Update(.1f);

        Assert.Empty(lab.Zone.Mines);
        Assert.Single(log);
        Assert.Empty(lab.Zone.PendingShots);
    }
}
