/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Cut 12.4(b) (docs/fire-control-cut.md, "The detonation primitive"): behavioural tests for Detonate -- the
// blast-as-area owner that replaces FireControl.Splash and Entity.Absorb -- and for Apply's switch on the
// frozen Fuse (null/Proximity/Contact/Delayed). Builds its own fixtures, the convention every cut's test file
// follows, with the rules this cut's own tests must obey: SchematicCellSize 2 throughout (the 12.3 fixture's
// cell size of 1 would hide a missing `/ SchematicCellSize`), the target off the origin, and a hull whose
// centre of mass is not an integer cell.
public sealed partial class FireControlCut124Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut124-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut124Tests() => Directory.CreateDirectory(_root);

    private readonly List<CultCache> _openCaches = new List<CultCache>();

    public void Dispose()
    {
        foreach (var c in _openCaches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings(float commitHorizon = .5f) => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        TargetArmorInfoThreshold = .2f,
        TargetGearInfoThreshold = .8f,
        FiringArc = 170,
        CommitHorizon = commitHorizon,
        SchematicCellSize = 2f,
        UnaidedAccuracy = .05f,
        UnaidedTracking = 10f,
        UnaidedPrecision = 1000f,
        AgentMinHitProbability = 0f,
        BeamResolveInterval = .1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // 5 wide x 4 tall: centre of mass (2, 1.5) -- not an integer cell, the fixture rule this whole file obeys.
    private static Shape SolidShape(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // The interior cell (1,1) of a 3x3 solid hull, ringed by its 8 border cells -- Shape.Shrink needs all 8
    // neighbours present, which only the centre of a 3x3 has (the same fixture 12.3's BuriedCockpitNeedsPenetration
    // and PenetratorBurrowsBeforeItBursts both need: an item reachable only by penetrating the ring).

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
        public HullData HullData;
        public EquippedItem Cockpit;
        public Dictionary<string, EquippedItem> Custom = new Dictionary<string, EquippedItem>();
    }

    // The gun's behaviour data: a plain InstantWeaponData, or a LockWeaponData whose lock either grows to full
    // (LockAngle 180 admits any bearing) or never grows (LockAngle 0 admits none, so it only decays). A stat
    // term on Range lets a test scale it below its authored value through the power supply.
    private static InstantWeaponData WeaponBehavior(float damage, float range, StatSource? rangeTerm, float velocity, float penetration,
        bool lockWeapon, bool lockAcquires)
    {
        var data = lockWeapon ? new LockWeaponData
        {
            LockSpeed = Constant(1000), SensorImpact = Constant(1), LockAngle = Constant(lockAcquires ? 180 : 0),
            DirectionImpact = Constant(1), Decay = Constant(1)
        } : new InstantWeaponData();
        data.Damage = Constant(damage);
        data.Range = rangeTerm == null ? Constant(range)
            : new PerformanceStat { Min = range, Max = range, Terms = { new StatTerm { Source = rangeTerm.Value, Exponent = 1f } } };
        data.MinRange = Constant(0);
        data.Velocity = Constant(velocity);
        data.Spread = Constant(0);
        data.DamageSpread = Constant(0);
        data.Penetration = Constant(penetration);
        data.Count = Constant(1);
        data.BurstTime = Constant(0);
        data.Cooldown = Constant(1000);
        data.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };
        return data;
    }

    private Engagement Build(
        GameplaySettings settings, Shape hullShape,
        float2? targetFacing = null, float targetRange = 100f, float velocity = 0f,
        WeaponFuse? fuse = null, float blastRadius = 0f, float penetration = 0f, float damage = 500f,
        WeaponModifiers modifiers = WeaponModifiers.None,
        bool equipShield = false, float shieldCapacity = 1000f, bool shieldActive = true,
        int2? cockpitCell = null, float cockpitDurability = 1000f,
        (string Name, Shape Shape, int2 Cell, float Durability)[] custom = null,
        float accuracy = 1f, int2? gunCell = null, float weaponRange = 100000f,
        float hardpointArc = 0f, StatSource? rangeTerm = null, bool lockWeapon = false, bool lockAcquires = true)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 1000000, Mass = 1000, Armor = 0,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = gunCell ?? new int2(0, 0), Shape = new Shape() } }
        };
        var shooterHullData = new HullData
        {
            Name = "ShooterHull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 1000000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape(), FiringArc = hardpointArc } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(shooterHullData);
        cache.Upsert(new WeaponItemData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Fuse = fuse, BlastRadius = blastRadius, WeaponModifiers = modifiers,
            Behaviors = { WeaponBehavior(damage, weaponRange, rangeTerm, velocity, penetration, lockWeapon, lockAcquires) }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData { Accuracy = Constant(accuracy), Resolution = Constant(1000f), Precision = Constant(1000f), Tracking = Constant(1000000f) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Cockpit", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new CockpitData() }
        });
        cache.Upsert(new GearData
        {
            Name = "Marker", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        var barShape = new Shape(2, 1);
        foreach (var v in barShape.AllCoordinates) barShape[v] = true;
        cache.Upsert(new GearData
        {
            Name = "Bar", Hardpoint = HardpointType.Tool, Shape = barShape, Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ReactorData { Charge = Constant(1000), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Shield", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ShieldData { Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(shieldCapacity), RefillDuration = Constant(.01f), RestoreDuration = Constant(.01f) } }
        });
        if (custom != null)
            foreach (var cu in custom)
                cache.Upsert(new GearData { Name = cu.Name, Hardpoint = HardpointType.Tool, Shape = cu.Shape, Durability = 1000000,
                    MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000 });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 500; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Cut124", Owner = null }, null);

        EquippableItem Make(string name, int lot, float durability) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>(name)), Durability = durability, Lot = lot
        };
        EquippableItem MakeWeapon(int lot) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<WeaponItemData>("Gun")), Durability = 1, Lot = lot
        };

        var lot = 1;
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("ShooterHull"));
        var shooter = new Ship(items, zone, new EquippableItem { Data = shooterHullRef, Durability = 1000000, Lot = lot++ }, new EntitySettings());
        var gun = MakeWeapon(lot++);
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(x => x.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);
        var targeting = Make("Targeting", lot++, 1);
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var target = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = lot++ }, new EntitySettings());
        var targetGun = MakeWeapon(lot++);
        Assert.True(target.TryEquip(targetGun, gunCell ?? new int2(0, 0)));

        EquippedItem cockpit = null;
        if (cockpitCell != null)
        {
            var ci = Make("Cockpit", lot++, cockpitDurability);
            Assert.True(target.TryEquip(ci, cockpitCell.Value), $"cockpit must fit at {cockpitCell.Value}");
            cockpit = target.Equipment.Single(x => x.EquippableItem == ci);
        }

        var customItems = new Dictionary<string, EquippedItem>();
        if (custom != null)
            foreach (var cu in custom)
            {
                var ci0 = Make(cu.Name, lot++, cu.Durability);
                Assert.True(target.TryEquip(ci0, cu.Cell), $"custom {cu.Name} must fit at {cu.Cell}");
                customItems[cu.Name] = target.Equipment.Single(x => x.EquippableItem == ci0);
            }

        if (equipShield)
        {
            var reactor = Make("Reactor", lot++, 10);
            Assert.True(target.TryFindSpace(reactor, out var rpos));
            Assert.True(target.TryEquip(reactor, rpos));
            var shield = Make("Shield", lot++, 10);
            Assert.True(target.TryFindSpace(shield, out var spos));
            Assert.True(target.TryEquip(shield, spos));
        }

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        // Off-origin (the fixture rule): the shooter sits away from the world origin, and the target sits
        // `targetRange` ahead of it along the shooter's own +z, so a stationary target's TravelDirection is
        // exactly (0,1) when targetFacing is left at its default.
        shooter.Position = float3(-53, 0, -101);
        target.Position = shooter.Position + float3(0, 0, targetRange);
        if (targetFacing != null) target.Direction = normalize(targetFacing.Value);
        shooter.SetTarget(target);
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        if (equipShield) target.Shield.Item.Enabled.Value = shieldActive;

        zone.Update(1f); // fire times nonzero, same convention 12.3's fixture uses

        return new Engagement
        {
            Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon,
            HullData = hullData, Cockpit = cockpit, Custom = customItems
        };
    }

    // Numeric cross-check, independent of FireControl's own exact-overlap implementation: the area of a disc
    // (centred at world point (cx,cy), radius r) that lies inside the axis-aligned rectangle [xlo,xhi]x[ylo,yhi],
    // by fine grid sampling. Used only to compute an EXPECTED value from first principles; the tests that need
    // exact values instead (conservation, exact half-splits) get them from geometry, not sampling.
    private static double DiscAreaInRect(double cx, double cy, double r, double xlo, double xhi, double ylo, double yhi, int samples = 1000)
    {
        var bxlo = Math.Max(xlo, cx - r);
        var bxhi = Math.Min(xhi, cx + r);
        var bylo = Math.Max(ylo, cy - r);
        var byhi = Math.Min(yhi, cy + r);
        if (bxhi <= bxlo || byhi <= bylo) return 0;
        long inside = 0;
        var dx = (bxhi - bxlo) / samples;
        var dy = (byhi - bylo) / samples;
        for (var i = 0; i < samples; i++)
        {
            var x = bxlo + (i + .5) * dx;
            var dxc = x - cx;
            for (var j = 0; j < samples; j++)
            {
                var y = bylo + (j + .5) * dy;
                var dyc = y - cy;
                if (dxc * dxc + dyc * dyc <= r * r) inside++;
            }
        }
        return inside * dx * dy;
    }

    private static double DiscAreaInCell(double cx, double cy, double r, int2 cell, int samples = 1000) =>
        DiscAreaInRect(cx, cy, r, cell.x - .5, cell.x + .5, cell.y - .5, cell.y + .5, samples);

    // ==== Geometry ====

    // TheDiscIsConservedOverTheGrid: on a large solid, armourless, itemless hull that contains the disc
    // entirely, the total hull damage delivered equals `damage`, at three different disc placements.
    // Kills: sampling in place of the exact overlap; the normaliser (2r)^2 in place of pi*r^2; a bounding box
    // that clips the rim cells; a dropped chord breakpoint -- any of these would move the total away from
    // `damage` even though the whole disc lies on metal.
    [Theory]
    [InlineData(0f, 0f, 0.3f)]      // radius under one cell, centred on a cell's own integer coordinate
    [InlineData(.5f, .5f, 1.7f)]    // centred on a cell corner
    [InlineData(.37f, -.21f, 1.2f)] // an arbitrary off-grid centre
    public void TheDiscIsConservedOverTheGrid(float ox, float oy, float radius)
    {
        var e = Build(TestSettings(), SolidShape(21, 21));
        var centreCell = new int2(10, 10); // well inside the 21x21 grid at any of the radii above
        var worldCentre = e.Target.Position.xz + float2(ox, oy) * e.Items.GameplaySettings.SchematicCellSize;
        // Shift so the schematic centre lands at centreCell + (ox,oy): CenterOfMass is (10,10) for a 21x21
        // solid hull, so the untranslated target position already maps there.
        var before = e.Target.Hull.Durability;

        FireControl.Detonate(e.Zone, worldCentre, radius * e.Items.GameplaySettings.SchematicCellSize, 100f, DamageType.Kinetic);

        var delivered = before - e.Target.Hull.Durability;
        Assert.Equal(100f, delivered, 2);
    }

    // EachCellTakesItsShareOfTheDisc: a solid hull with no items, and per-cell armour large enough that
    // ArmorAbsorb never depletes it -- so the ArmorDamage event for a cell reports that cell's raw share.
    // Pass: the named cell's reported share matches damage * overlap/(pi r^2), overlap integrated numerically
    // by the test. Kills: an even split over the covered cells (Splash's rule), and a share decided by the
    // cell's distance from the centre rather than its overlap area.
    [Fact]
    public void EachCellTakesItsShareOfTheDisc()
    {
        var e = Build(TestSettings(), SolidShape(9, 9));
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1e6f; e.Target.MaxArmor[c.x, c.y] = 1e6f; }
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        var com = e.HullData.Shape.CenterOfMass; // (4,4) for a 9x9 solid hull
        var namedCell = new int2(5, 4); // one cell off-centre laterally
        var radius = 2.3; // world units under cellSize*4, comfortably covering several cells

        var deposits = new Dictionary<int2, float>();
        using (e.Target.ArmorDamage.Subscribe(x => deposits[x.pos] = x.damage))
        {
            FireControl.Detonate(e.Zone, e.Target.Position.xz, (float) radius, 1000f, DamageType.Kinetic);
        }

        var expectedOverlap = DiscAreaInCell(com.x, com.y, radius / cellSize, namedCell);
        var expectedShare = 1000.0 * expectedOverlap / (Math.PI * (radius / cellSize) * (radius / cellSize));
        Assert.True(deposits.TryGetValue(namedCell, out var actual), "named cell took no share at all");
        Assert.Equal(expectedShare, actual, 1);
    }

    // TheShareOffTheHullIsLost (was ExternalBlastWastesMostOfItsEnergy): the disc is centred exactly on the
    // hull's own edge, so exactly half its area sits off the grid by construction (no numeric integration
    // needed -- a disc split by a straight line through its centre halves exactly). Pass: the delivered total
    // is half the blast's damage. Kills: renormalising the shares over the occupied cells (which would deliver
    // the full damage).
    [Fact]
    public void TheShareOffTheHullIsLost()
    {
        var e = Build(TestSettings(), SolidShape(5, 4)); // x: 0..4 (CoM.x = 2), left edge at schematic x = -0.5
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        // World point at schematic (-0.5, 1.5): CoM.y = 1.5 (the fixture's own non-integer centre), 2.5 cells
        // to port of CoM.x -- exactly the hull's left edge, with default facing (0,1) so world x is schematic x.
        var worldCentre = e.Target.Position.xz + float2(-2.5f * cellSize, 0);
        var before = e.Target.Hull.Durability;

        // Radius 0.5 cells: the on-hull half never reaches the next cell boundary at schematic x = 0.5.
        FireControl.Detonate(e.Zone, worldCentre, .5f * cellSize, 100f, DamageType.Kinetic);

        var delivered = before - e.Target.Hull.Durability;
        Assert.Equal(50f, delivered, 1);
    }

    // CandidatesIncludeHullsWhoseCentreIsOutsideTheRadius: a long thin hull whose centre of mass lies well
    // beyond the blast radius, while its near end lies inside. Kills: Splash's own centre-distance cull.
    [Fact]
    public void CandidatesIncludeHullsWhoseCentreIsOutsideTheRadius()
    {
        var e = Build(TestSettings(), SolidShape(1, 20)); // CoM at schematic (0, 9.5)
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        // The near end (schematic y = 0) is `9.5 * cellSize` world units from CoM; the blast sits right there,
        // far outside a radius that would ever reach the centre.
        var nearEndWorld = e.Target.Position.xz + float2(0, -9.5f * cellSize);
        var before = e.Target.Hull.Durability;

        FireControl.Detonate(e.Zone, nearEndWorld, cellSize, 100f, DamageType.Kinetic);

        Assert.True(e.Target.Hull.Durability < before, "the near end of a long hull must take damage even though its centre is far outside the radius");
    }

    // Per-cell armour large enough that ArmorAbsorb never depletes it: every ArmorDamage event then reports its
    // cell's raw share, whether or not an item sits on the cell.
    private static void ArmourEverything(Engagement e)
    {
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1e6f; e.Target.MaxArmor[c.x, c.y] = 1e6f; }
    }

    // The independent expectation: `damage` shared over a disc of radius rCells centred at schematic (cx,cy) in
    // proportion to numeric overlap with each occupied cell.
    private static double ExpectedDelivered(Shape shape, double cx, double cy, double rCells, double damage)
    {
        var overlap = 0.0;
        foreach (var c in shape.Coordinates) overlap += DiscAreaInCell(cx, cy, rCells, c);
        return damage * overlap / (Math.PI * rCells * rCells);
    }

    // ==== 12.4 fix batch F1: a disc tangent to a cell edge must not count the whole square ====

    // A cell edge exactly r from the centre, with the piece's midpoint at 0, used to fall through
    // `h < yhi` / `-h > ylo` into the "whole rectangle" branch, so the square counted in full. BlastRadius 1
    // world unit is half a cell (edges at +-.5 from an integer centre of mass), 3 is one and a half (edges at
    // +-.5 and +-1.5): both are tangent to cell edges. Measured through Fire before the fix: 127.32 and 100.80
    // delivered of 100.
    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void ATangentDiscDeliversExactlyItsDamageThroughFire(float blastRadius)
    {
        var e = Build(TestSettings(), SolidShape(5, 5), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: blastRadius, damage: 100f);
        ArmourEverything(e);
        var total = 0f;
        using (e.Target.ArmorDamage.Subscribe(x => total += x.damage))
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.Equal(100f, total, 2);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void ATangentDiscDeliversExactlyItsDamageThroughDetonate(float radius)
    {
        var e = Build(TestSettings(), SolidShape(5, 5));
        ArmourEverything(e);
        var total = 0f;
        using (e.Target.ArmorDamage.Subscribe(x => total += x.damage))
            FireControl.Detonate(e.Zone, e.Target.Position.xz, radius, 100f, DamageType.Kinetic);
        Assert.Equal(100f, total, 2);
    }

    // ==== 12.4 fix batch F2: the exact overlap is the only decider of "the disc touches the hull" ====

    // An L-shaped hull whose centre of mass sits far from the far end of its long arm. The blast lands on that
    // arm's end, 14 world units from the centre of mass -- beyond radius + half the bounding box's diagonal
    // measured from it, which the old candidate cull used, so the cull threw away a blast that overlaps a
    // cell. Expected value: a numeric overlap of the cells, not the code under test.
    [Fact]
    public void ABlastAtTheFarEndOfAnLShapedHullLands()
    {
        var s = new Shape(10, 4);
        foreach (var c in s.AllCoordinates) s[c] = c.x < 4 || c.y == 0;
        var e = Build(TestSettings(), s);
        ArmourEverything(e);
        var com = e.HullData.Shape.CenterOfMass;
        var p = float2(9.9f, 0f);
        var expected = ExpectedDelivered(e.HullData.Shape, p.x, p.y, 1.0, 100.0);
        Assert.True(expected > 10.0, $"fixture: the blast must overlap the hull, expected {expected}");

        var total = 0f;
        using (e.Target.ArmorDamage.Subscribe(x => total += x.damage))
            FireControl.Detonate(e.Zone, e.Target.Position.xz + (p - com) * 2f, 2f, 100f, DamageType.Kinetic);

        Assert.Equal(expected, total, 1);
    }

    // The same rule on the shipped hulls: a blast just grazing the corner farthest from the centre of mass. The
    // corner of the shipped Longinus reaches past the old cull's box-half-diagonal bound.
    [Theory]
    [InlineData("Longinus")]
    public void AGrazingBlastAtAShippedHullsFarthestCornerLands(string hullName)
    {
        Shape shape;
        var cache = OpenReadOnlyRealCatalog(TestCatalog.Repo, out _);
        try { shape = cache.GetByName<HullData>(hullName).Shape; }
        finally { cache.Dispose(); }

        var com = shape.CenterOfMass;
        var far = float2.zero;
        var best = -1f;
        foreach (var c in shape.Coordinates)
            foreach (var (dx, dy) in new[] { (-.5f, -.5f), (.5f, -.5f), (-.5f, .5f), (.5f, .5f) })
            {
                var corner = float2(c.x + dx, c.y + dy);
                var d = length(corner - com);
                if (d > best) { best = d; far = corner; }
            }
        // One cell of radius, centred .9 cell outward of the corner: the disc reaches the corner by .1 cell.
        var centre = far + normalize(far - com) * .9f;

        var e = Build(TestSettings(), shape, gunCell: shape.Coordinates[0]);
        ArmourEverything(e);
        var expected = ExpectedDelivered(shape, centre.x, centre.y, 1.0, 100.0);
        Assert.True(expected > .05, $"fixture: the blast must graze the hull, expected {expected}");

        var total = 0f;
        using (e.Target.ArmorDamage.Subscribe(x => total += x.damage))
            FireControl.Detonate(e.Zone, e.Target.Position.xz + (centre - com) * 2f, 2f, 100f, DamageType.Kinetic);

        Assert.Equal(expected, total, 1);
    }

    // ==== 12.4 fix batch F3: a lethal blast must not break the entity enumeration ====

    private Ship AddShip(Engagement e, float3 position, float hullDurability, int lot)
    {
        var hullRef = e.Items.ItemData.RefOf<ItemData>(e.Items.ItemData.GetByName<HullData>("Hull"));
        var ship = new Ship(e.Items, e.Zone, new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = lot }, new EntitySettings());
        var marker = new EquippableItem
        {
            Data = e.Items.ItemData.RefOf<ItemData>(e.Items.ItemData.GetByName<GearData>("Marker")), Durability = 1000000, Lot = lot + 1
        };
        Assert.True(ship.TryEquip(marker, new int2(1, 1)));
        e.Zone.Entities.Add(ship);
        ship.Activate();
        ship.Position = position;
        return ship;
    }

    // A blast that kills an entity mid-pass: death removes it from Zone.Entities, and the next MoveNext of the
    // live enumeration threw. The shot was never removed, so it detonated again the next tick (the probe saw
    // 2.69 then 5.38 on the target). Pass: nothing throws, the shot resolves and leaves exactly once, and the
    // target is damaged exactly once.
    [Fact]
    public void ALethalBlastResolvesOnceAndBreaksNothing()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f);
        var near = AddShip(e, e.Target.Position + float3(8, 0, 0), 1f, 300);
        var far = AddShip(e, e.Shooter.Position + float3(-5000, 0, 0), 1f, 302);

        var resolved = 0;
        var targetDamage = 0f;
        using var r = e.Zone.ShotResolved.Subscribe(_ => resolved++);
        using var a = e.Target.ArmorDamage.Subscribe(x => targetDamage += x.damage);
        using var h = e.Target.HullDamage.Subscribe(x => targetDamage += x);
        using var i = e.Target.ItemDamage.Subscribe(x => targetDamage += x.damage);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        SafeAssert.NoShots(e.Zone);
        Assert.Equal(1, resolved);
        SafeAssert.NotIn(e.Zone, near); // fixture: the blast really was lethal to a ship mid-pass
        SafeAssert.In(e.Zone, far);
        Assert.True(targetDamage > 0f, "fixture: the target must be inside the blast");
        var afterFirstTick = targetDamage;

        e.Zone.Update(.01f);

        Assert.Equal(1, resolved);
        Assert.Equal(afterFirstTick, targetDamage, 4);
    }

    // AOneBlastKillsEveryShipInsideIt: three ships, each dead to one hit, inside one blast. Each takes hull
    // damage exactly once and dies exactly once, and the shot leaves the queue once. Kills: the snapshot loop
    // breaking after the first kill (the other two would never be touched).
    [Fact]
    public void AOneBlastKillsEveryShipInsideIt()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f);
        var ships = new[]
        {
            AddShip(e, e.Target.Position + float3(8, 0, 0), 1f, 300),
            AddShip(e, e.Target.Position + float3(-8, 0, 0), 1f, 302),
            AddShip(e, e.Target.Position + float3(0, 0, 8), 1f, 304),
        };
        var hullHits = new int[3];
        var deaths = new int[3];
        var subs = new List<IDisposable>();
        for (var n = 0; n < 3; n++)
        {
            var k = n;
            subs.Add(ships[k].HullDamage.Subscribe(_ => hullHits[k]++));
            subs.Add(ships[k].Death.Subscribe(_ => deaths[k]++));
        }
        var resolved = 0;
        subs.Add(e.Zone.ShotResolved.Subscribe(_ => resolved++));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        foreach (var s in subs) s.Dispose();

        Assert.Equal(new[] { 1, 1, 1 }, hullHits);
        Assert.Equal(new[] { 1, 1, 1 }, deaths);
        foreach (var s in ships) Assert.False(e.Zone.Entities.Contains(s), "a killed ship must have left the zone");
        SafeAssert.NoShots(e.Zone);
        Assert.Equal(1, resolved);
    }

    // AShieldlessShipOutsideTheDiscIsNotTouched: nothing of any kind reaches an entity the disc does not
    // overlap. Kills: an uncovered entity falling through to the damage call (`covered ??= new List`).
    [Fact]
    public void AShieldlessShipOutsideTheDiscIsNotTouched()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 3f, damage: 100f);
        var outside = AddShip(e, e.Target.Position + float3(60, 0, 0), 1f, 300);
        var touched = 0;
        using var a = outside.ArmorDamage.Subscribe(_ => touched++);
        using var h = outside.HullDamage.Subscribe(_ => touched++);
        using var i = outside.ItemDamage.Subscribe(_ => touched++);
        var targetHit = 0f;
        using var t = e.Target.HullDamage.Subscribe(x => targetHit += x);
        var armourHit = 0f;
        using var ta = e.Target.ArmorDamage.Subscribe(x => armourHit += x.damage);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.Equal(0, touched);
        Assert.Equal(1f, outside.Hull.Durability);
        Assert.True(e.Zone.Entities.Contains(outside));
        Assert.True(targetHit + armourHit > 0f, "fixture: the blast must land on the target");
    }

    // ADestroyedCockpitRaisesDeathOnce: the blast destroys the cockpit, Death removes and deactivates the
    // entity, and the blast's own DamageHull then still runs; no subscriber sees a second Death.
    [Fact]
    public void ADestroyedCockpitRaisesDeathOnce()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), cockpitCell: new int2(1, 1), cockpitDurability: 10f);
        e.Target.Hull.Durability = 10f; // fragile enough that the blast's own hull damage is lethal too
        var deaths = new List<CauseOfDeath>();
        using var d = e.Target.Death.Subscribe(deaths.Add);
        var hullEvents = 0;
        using var h = e.Target.HullDamage.Subscribe(_ => hullEvents++);

        FireControl.Detonate(e.Zone, e.Target.Position.xz, .6f, 5000f, DamageType.Kinetic);

        Assert.Equal(new[] { CauseOfDeath.CockpitDestroyed }, deaths);
        Assert.False(e.Zone.Entities.Contains(e.Target), "the dead target must have left the zone");
        Assert.True(hullEvents >= 1, "fixture: the blast's hull damage still lands after the cockpit dies");
        Assert.True(e.Target.Hull.Durability < .01f, "fixture: and that hull damage is itself lethal");
    }

    // ADirectHullKillRaisesDeathOnce: the non-blast path (Entity.DamageHull straight, as a projectile hit does)
    // raises Death once, and further damage to the corpse does not raise it again.
    [Fact]
    public void ADirectHullKillRaisesDeathOnce()
    {
        var e = Build(TestSettings(), SolidShape(3, 3));
        var deaths = 0;
        using var d = e.Target.Death.Subscribe(_ => deaths++);

        e.Target.DamageHull(2000000f);
        e.Target.DamageHull(5f);

        Assert.Equal(1, deaths);
    }

    // ==== Absorption order ====

    // ABlastIsAbsorbedThroughArmourFirstPerCell (replaces AbsorbSpendsArmourBeforeTheItemOnTheSameCell): a disc
    // wholly inside one armoured item cell, so its share is the whole damage -- the three damage levels of the
    // superseded test. Kills: the item absorbing before the armour, and armour being skipped.
    [Fact]
    public void ABlastIsAbsorbedThroughArmourFirstPerCell()
    {
        var cell = new int2(1, 1);
        Engagement Fresh() => Build(TestSettings(), SolidShape(3, 3), custom: new[] { ("Marker", new Shape(), cell, 50f) });

        void Reset(Engagement e) { e.Target.Armor[cell.x, cell.y] = 10f; e.Target.MaxArmor[cell.x, cell.y] = 10f; e.Custom["Marker"].EquippableItem.Durability = 50f; }
        var cellSize = 2f;
        var worldCentre = Fresh().Target.Position.xz; // recomputed fresh below per case (CoM == cell (1,1) for a 3x3 hull)

        {
            var e = Fresh(); Reset(e);
            FireControl.Detonate(e.Zone, e.Target.Position.xz, .3f * cellSize, 5f, DamageType.Kinetic); // below armour
            Assert.Equal(5f, e.Target.Armor[cell.x, cell.y], 2);
            Assert.Equal(50f, e.Custom["Marker"].EquippableItem.Durability, 2);
            Assert.Equal(1000000f, e.Target.Hull.Durability, 1);
        }
        {
            var e = Fresh(); Reset(e);
            FireControl.Detonate(e.Zone, e.Target.Position.xz, .3f * cellSize, 25f, DamageType.Kinetic); // between armour and armour+durability
            Assert.Equal(0f, e.Target.Armor[cell.x, cell.y], 2);
            Assert.Equal(35f, e.Custom["Marker"].EquippableItem.Durability, 2); // 25 - 10 armour = 15 excess
            Assert.Equal(1000000f, e.Target.Hull.Durability, 1);
        }
        {
            var e = Fresh(); Reset(e);
            FireControl.Detonate(e.Zone, e.Target.Position.xz, .3f * cellSize, 80f, DamageType.Kinetic); // above both
            Assert.Equal(0f, e.Target.Armor[cell.x, cell.y], 2);
            Assert.Equal(0f, e.Custom["Marker"].EquippableItem.Durability, 2);
            Assert.Equal(1000000f - 20f, e.Target.Hull.Durability, 1); // 80 - 10 armour - 50 item = 20
        }
    }

    // ArmourProtectsOnlyItsOwnCellsShare: a 2x1 item with armour on one cell only, and a disc symmetric about
    // the seam so both cells take the same share. Pass: the item takes (shareA - armourA)+ + shareB. Kills:
    // pooling the raw shares and then applying armour to the pool (which would let cell A's plate eat cell B's
    // share).
    [Fact]
    public void ArmourProtectsOnlyItsOwnCellsShare()
    {
        var cellA = new int2(1, 1);
        var cellB = new int2(2, 1); // Bar occupies (1,1)-(2,1)
        var e = Build(TestSettings(), SolidShape(4, 3), custom: new[] { ("Bar", BarShape(), cellA, 1000f) });
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        var com = e.HullData.Shape.CenterOfMass;

        e.Target.Armor[cellA.x, cellA.y] = 100f; e.Target.MaxArmor[cellA.x, cellA.y] = 100f;
        e.Target.Armor[cellB.x, cellB.y] = 0f; e.Target.MaxArmor[cellB.x, cellB.y] = 0f;

        // Centred exactly on the seam between the two cells (schematic x = 1.5), same y as both cells: by
        // symmetry shareA == shareB exactly, no numeric integration needed.
        var worldCentre = e.Target.Position.xz + (float2(1.5f, cellA.y) - com) * cellSize;
        var radius = 1f; // world units -- stays within the seam's own pair of cells (each 1 cell wide)

        // Armour (100) exceeds a symmetric share (damage/2), so pick damage large enough that shareA - armourA
        // is positive: shareA ~= damage/2 (by symmetry, exactly, regardless of the exact overlap fraction --
        // both cells see the identical geometry mirrored across the seam).
        FireControl.Detonate(e.Zone, worldCentre, radius, 400f, DamageType.Kinetic);

        var shareEach = 200f; // damage/2, by the seam symmetry
        var expectedItemLoss = Math.Max(0f, shareEach - 100f) + shareEach; // (shareA - armourA)+ + shareB
        Assert.Equal(1000f - expectedItemLoss, e.Custom["Bar"].EquippableItem.Durability, 1);
        Assert.Equal(0f, e.Target.Armor[cellA.x, cellA.y], 1); // cell A's own plate is spent by its own share (200 > 100), same as any single-cell absorb
    }

    private static Shape BarShape()
    {
        var shape = new Shape(2, 1);
        foreach (var v in shape.AllCoordinates) shape[v] = true;
        return shape;
    }

    // AMultiCellItemAbsorbsItsCoveredCellsAsOnePool (the ProbeC2 geometry): a 2x1 item with no armour, and a
    // disc straddling its seam that puts less than .1 on each cell but more than .1 on the two together. Pass:
    // the item absorbs the sum. Kills: resolving the item per cell in any form -- Absorb resurrected, ItemAbsorb
    // called per deposit, or a pool that resolves before every covered cell has deposited.
    [Fact]
    public void AMultiCellItemAbsorbsItsCoveredCellsAsOnePool()
    {
        var cellA = new int2(1, 1);
        var cellB = new int2(2, 1);
        var e = Build(TestSettings(), SolidShape(4, 3), custom: new[] { ("Bar", BarShape(), cellA, 50f) });
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;

        // Centred exactly on the seam, radius small enough that the whole disc is contained within the two
        // cells combined (radius 0.4 cells < 0.5 cells' worth of margin either side of the seam).
        var worldCentre = e.Target.Position.xz + (float2(1.5f, cellA.y) - e.HullData.Shape.CenterOfMass) * cellSize;
        var radiusCells = .4f;
        // Full disc retained (contained within the pair of cells), split evenly by the seam symmetry: total
        // share = damage exactly, .08 on each side when damage = .16.
        FireControl.Detonate(e.Zone, worldCentre, radiusCells * cellSize, .16f, DamageType.Kinetic);

        Assert.Equal(50f - .16f, e.Custom["Bar"].EquippableItem.Durability, 3);
    }

    // ItemDamageFiresOncePerCoveredCell: the same pooled geometry as above. Pass: the number of ItemDamage
    // events equals the number of covered cells with a positive post-armour deposit (two), each carrying that
    // cell's own deposit. Kills: one event per item, and events for zero-deposit cells.
    [Fact]
    public void ItemDamageFiresOncePerCoveredCell()
    {
        var cellA = new int2(1, 1);
        var e = Build(TestSettings(), SolidShape(4, 3), custom: new[] { ("Bar", BarShape(), cellA, 50f) });
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        var worldCentre = e.Target.Position.xz + (float2(1.5f, cellA.y) - e.HullData.Shape.CenterOfMass) * cellSize;

        var events = new List<float>();
        using (e.Target.ItemDamage.Subscribe(x => events.Add(x.damage)))
        {
            FireControl.Detonate(e.Zone, worldCentre, .4f * cellSize, .16f, DamageType.Kinetic);
        }

        Assert.Equal(2, events.Count);
        Assert.All(events, amt => Assert.Equal(.08f, amt, 2));
    }

    // ==== Symmetry and frame ====

    // MirroredBlastsDoMirroredDamage: a hull, its armour and its items all mirror-symmetric about the bow
    // axis, facing (2,1)/sqrt(5). Two independent engagements, blasted at world points mirrored across the
    // target's own bow axis -- the mirror computed here from the facing, not through ToSchematic. Pass:
    // per-cell armour and item durability mirror. Kills: the +.5 cell convention returning in the overlap, a
    // missing or wrong CenterOfMass anchor, and any scheduling rule that depends on enumeration order.
    [Fact]
    public void MirroredBlastsDoMirroredDamage()
    {
        var facing = normalize(float2(2, 1));
        var markerLeft = new int2(1, 1);
        var markerRight = new int2(3, 1); // mirror pair about x = 2 on a 5-wide hull

        Engagement Fresh()
        {
            var e = Build(TestSettings(), SolidShape(5, 3), targetFacing: facing,
                custom: new[] { ("MarkerL", new Shape(), markerLeft, 200f), ("MarkerR", new Shape(), markerRight, 200f) });
            foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 40f; e.Target.MaxArmor[c.x, c.y] = 40f; }
            return e;
        }

        // Reflect a world-space offset across the target's own bow axis (its forward direction), computed here
        // independently of Entity.ToSchematic: reflecting across the line through the origin along `forward`
        // negates the lateral (starboard) component and keeps the along-bow component.
        float2 Mirror(float2 offset, float2 forward)
        {
            var along = dot(offset, forward) * forward;
            var lateral = offset - along;
            return along - lateral;
        }

        var e1 = Fresh();
        var e2 = Fresh();
        var cellSize = e1.Items.GameplaySettings.SchematicCellSize;
        var offset = float2(6f, 3f); // an arbitrary world offset from the target, not aligned to any axis
        var mirroredOffset = Mirror(offset, facing);

        FireControl.Detonate(e1.Zone, e1.Target.Position.xz + offset, 3f * cellSize, 300f, DamageType.Kinetic);
        FireControl.Detonate(e2.Zone, e2.Target.Position.xz + mirroredOffset, 3f * cellSize, 300f, DamageType.Kinetic);

        foreach (var c in e1.HullData.Shape.Coordinates)
        {
            var mirroredCell = new int2(4 - c.x, c.y);
            Assert.Equal(e1.Target.Armor[c.x, c.y], e2.Target.Armor[mirroredCell.x, mirroredCell.y], 1);
        }
        Assert.Equal(e1.Custom["MarkerL"].EquippableItem.Durability, e2.Custom["MarkerR"].EquippableItem.Durability, 1);
        Assert.Equal(e1.Custom["MarkerR"].EquippableItem.Durability, e2.Custom["MarkerL"].EquippableItem.Durability, 1);
        Assert.Equal(e1.Target.Hull.Durability, e2.Target.Hull.Durability, 1);
    }

    // ABlastDamagesTheCellsNearestIt. This succeeds SplashDamagesTheSideTheBlastCameFrom
    // (FireControlCut11Tests.cs) and SplashIsDirectional (FireControlCut4Tests.cs). Kills: a flip of
    // ToSchematicPoint's right axis, an x/y swap, and a missing `/ SchematicCellSize`.
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(2f, 1f)]
    [InlineData(-.6f, .8f)]
    public void ABlastDamagesTheCellsNearestIt(float fx, float fy)
    {
        var facing = normalize(float2(fx, fy));
        var e = Build(TestSettings(), SolidShape(5, 4), targetFacing: facing);
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }

        // Starboard computed here independently of FireControl -- the facing rotated clockwise.
        var starboard = float2(facing.y, -facing.x);
        var port = -starboard;
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        var blastWorld = e.Target.Position.xz + float3(port.x, 0, port.y).xz * 10f;

        var damaged = new HashSet<int2>();
        using (e.Target.ArmorDamage.Subscribe(x => damaged.Add(x.pos)))
        {
            FireControl.Detonate(e.Zone, blastWorld, 7f, 500f, DamageType.Kinetic);
        }

        // Port cells (schematic x < CoM.x) must take damage; starboard cells (schematic x > CoM.x) must not.
        // The centre column (x == CoM.x, only when CoM.x is an integer) is excluded from the assertion since
        // it can legitimately fall on either side depending on float rounding.
        var com = e.HullData.Shape.CenterOfMass;
        var portDamaged = false;
        foreach (var c in e.HullData.Shape.Coordinates)
        {
            if (c.x < com.x - .5f) { if (damaged.Contains(c)) portDamaged = true; }
            if (c.x > com.x + .5f) Assert.False(damaged.Contains(c), $"facing ({fx},{fy}): starboard cell {c} took damage from a port blast");
        }
        Assert.True(portDamaged, $"facing ({fx},{fy}): no port cell took damage");
    }

    // ==== Shields (Q12-9 = A) ====

    // ShieldPaysForWhatReachesIt: the reserve drops by the entity's covered share, not by the whole blast. The
    // disc is centred exactly on the hull's edge (as in TheShareOffTheHullIsLost), so exactly half the raw
    // damage is the covered share -- a shield capacity between the covered share and the raw damage tells the
    // two apart cleanly. Kills: charging the full damage.
    [Fact]
    public void ShieldPaysForWhatReachesIt()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), equipShield: true, shieldCapacity: 70f);
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f); // charge the reserve to full
        var cellSize = e.Items.GameplaySettings.SchematicCellSize;
        var worldCentre = e.Target.Position.xz + float2(-2.5f * cellSize, 0);

        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 70f));  // fixture: the reserve is exactly full
        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 70.5f));

        // damage 100, half retained (50) -- 50 <= capacity 70, so a correct implementation absorbs it whole and
        // never breaks; a "charge the full 100" mutant would find 100 > 70 and break the shield instead.
        var cellEvents = 0;
        using var a = e.Target.ArmorDamage.Subscribe(_ => cellEvents++);
        FireControl.Detonate(e.Zone, worldCentre, .5f * cellSize, 100f, DamageType.Kinetic);

        Assert.False(e.Target.Shield.Broken);
        Assert.Equal(0, cellEvents); // the shield took it all, nothing reached a cell
        // The reserve dropped by the covered share and by nothing else: 70 - 50 = 20 left. It is read through the
        // public query as a threshold probe, since no recharge runs between Detonate and here.
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 19.9f));
        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 20.1f));
    }

    // An entity the disc does not touch has nothing to decide: it is skipped whole, its active shield is not
    // consulted and nothing is dealt. The blast lands 20 cells away from a shielded hull.
    [Fact]
    public void AnUntouchedEntitysShieldIsNotConsulted()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), equipShield: true, shieldCapacity: 70f);
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f);
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 70f));
        var events = 0;
        using var a = e.Target.ArmorDamage.Subscribe(_ => events++);
        using var h = e.Target.HullDamage.Subscribe(_ => events++);

        FireControl.Detonate(e.Zone, e.Target.Position.xz + float2(40f, 0f), 2f, 1000f, DamageType.Kinetic);

        Assert.Equal(0, events);
        Assert.False(e.Target.Shield.Broken);
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 70f));
    }

    // ABlastShotsShieldIsDecidedAtDetonation (Q12-9 = A): Commit decides no shield for a shot that carries a
    // fuse, whatever the shield's own state at commit -- Detonate decides every blast's shield, live. Pinned
    // directly on the field Commit would otherwise set: ShotOutcome.Shielded/ShieldBroken must stay false for a
    // fused shot even when the shield could have absorbed the whole raw damage at commit. Kills: Commit's
    // shield block left ungated for fused shots.
    [Fact]
    public void ABlastShotsShieldIsDecidedAtDetonation()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, targetRange: 100,
            fuse: WeaponFuse.Contact, blastRadius: 4, damage: 50,
            equipShield: true, shieldCapacity: 1000f); // capacity far exceeds the raw damage
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f);
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 50f)); // the shield COULD absorb the whole shot

        ShotOutcome outcome = null;
        using var s = e.Zone.ShotCommitted.Subscribe(o => outcome = o);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);
        Assert.False(outcome.Shielded);
        Assert.False(outcome.ShieldBroken);
    }

    // The other half of Q12-9 = A: the shield's state at arrival, not at commit, decides. A contact shot in
    // 2 s of flight commits 1.5 s in; the shield is flipped between commit and arrival, and the blast (radius
    // 15 cells, so the whole 20-cell hull lies inside the disc and the covered share is exactly
    // damage * 20 / (pi * 15^2)) must follow the state it finds. The reserve is read through the public
    // CanTakeHit as a threshold probe: nothing recharges between Detonate and the read.
    [Fact]
    public void AShieldRaisedBetweenCommitAndArrivalAbsorbsTheBlast()
    {
        const float capacity = 1000f;
        var share = 50f * 20f / (PI * 15f * 15f);
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 50, targetRange: 100,
            fuse: WeaponFuse.Contact, blastRadius: 30f, damage: 50f,
            equipShield: true, shieldCapacity: capacity, shieldActive: false);
        ArmourEverything(e);
        var cellDamage = 0f;
        using var a = e.Target.ArmorDamage.Subscribe(x => cellDamage += x.damage);
        using var h = e.Target.HullDamage.Subscribe(x => cellDamage += x);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        while (!e.Zone.PendingShots.Any(s => s.ShotId == shotId && s.Committed)) e.Zone.Update(.05f);
        Assert.True(committed.Hit);
        Assert.False(committed.Shielded);
        Assert.False(committed.ShieldBroken);

        e.Target.Shield.Item.Enabled.Value = true; // up before arrival, down at commit
        for (var i = 0; i < 4; i++) e.Zone.Update(.05f);
        Assert.True(e.Zone.PendingShots.Any(s => s.ShotId == shotId), "fixture: the shot must still be in flight");
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, capacity - .1f), "fixture: the reserve must be full");
        while (e.Zone.PendingShots.Any(s => s.ShotId == shotId)) e.Zone.Update(.05f);

        Assert.Equal(0f, cellDamage); // the shield decided at arrival, so no cell took anything
        Assert.False(e.Target.Shield.Broken);
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, capacity - share - .05f));
        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, capacity - share + .05f));
    }

    // And the reverse: up at commit, down at arrival -- the blast finds no shield and lands on the cells, and
    // the reserve is not charged.
    [Fact]
    public void AShieldDroppedBetweenCommitAndArrivalDoesNotAbsorbTheBlast()
    {
        const float capacity = 1000f;
        var share = 50f * 20f / (PI * 15f * 15f);
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 50, targetRange: 100,
            fuse: WeaponFuse.Contact, blastRadius: 30f, damage: 50f,
            equipShield: true, shieldCapacity: capacity);
        ArmourEverything(e);
        for (var i = 0; i < 30; i++) e.Zone.Update(.1f);
        var armour = 0f;
        using var a = e.Target.ArmorDamage.Subscribe(x => armour += x.damage);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        while (!e.Zone.PendingShots.Any(s => s.ShotId == shotId && s.Committed)) e.Zone.Update(.05f);
        Assert.False(committed.Shielded);

        e.Target.Shield.Item.Enabled.Value = false;
        while (e.Zone.PendingShots.Any(s => s.ShotId == shotId)) e.Zone.Update(.05f);

        Assert.Equal(share, armour, 2); // the whole covered share landed on the cells
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, capacity - .1f)); // and the reserve was not charged
    }

    // ==== Fuses (through Fire, SchematicCellSize 2) ====

    // PenetratorBurrowsBeforeItBursts, which pins Q12-8 in both halves. A 3x3 solid hull's only interior cell
    // (1,1) carries a Cockpit item, ringed by the hull's own 8 border cells.
    [Fact]
    public void PenetratorBurrowsBeforeItBursts()
    {
        // With a delayed fuse and a radius under half a cell, the cockpit takes the whole damage, and the
        // armour of every ring cell on the lane is unchanged. Nothing is absorbed along the burrow.
        {
            var e = Build(TestSettings(), SolidShape(3, 3), penetration: 1.5f, fuse: WeaponFuse.Delayed, blastRadius: .3f, damage: 40f,
                cockpitCell: new int2(1, 1), cockpitDurability: 1000f);
            foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }
            e.Target.Armor[1, 1] = 0f; e.Target.MaxArmor[1, 1] = 0f; // the cockpit's own cell carries no armour

            var before = e.Cockpit.EquippableItem.Durability;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);

            Assert.Equal(before - 40f, e.Cockpit.EquippableItem.Durability, 1);
            foreach (var c in e.HullData.Shape.Coordinates)
                if (!(c.x == 1 && c.y == 1))
                    Assert.Equal(1000f, e.Target.Armor[c.x, c.y], 1);
        }

        // A contact fuse with the same stats leaves the cockpit untouched (P sits at the impact cell, on the
        // ring, never reaching the buried cockpit).
        {
            var e = Build(TestSettings(), SolidShape(3, 3), penetration: 1.5f, fuse: WeaponFuse.Contact, blastRadius: .3f, damage: 40f,
                cockpitCell: new int2(1, 1), cockpitDurability: 1000f);
            var before = e.Cockpit.EquippableItem.Durability;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
            Assert.Equal(before, e.Cockpit.EquippableItem.Durability);
        }

        // With the fuse removed and everything else held, the shot is a dumb AP round: the lane's armour is
        // spent in order, and the cockpit takes only what the ring left (12.3). Damage in a line.
        {
            var e = Build(TestSettings(), SolidShape(3, 3), penetration: 2f, fuse: null, blastRadius: 0f, damage: 40f,
                cockpitCell: new int2(1, 1), cockpitDurability: 1000f);
            foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 5f; e.Target.MaxArmor[c.x, c.y] = 5f; }
            e.Target.Armor[1, 1] = 0f; e.Target.MaxArmor[1, 1] = 0f;

            var before = e.Cockpit.EquippableItem.Durability;
            ShotOutcome outcome = null;
            for (var attempt = 0; attempt < 200 && (outcome == null || !outcome.Hit); attempt++)
            {
                using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
                e.Zone.Update(.01f);
            }
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            Assert.Equal(before - 35f, e.Cockpit.EquippableItem.Durability, 1); // 40 - the front ring cell's 5 armour
        }
    }

    // TheFusePointStaysOnMetal: a delayed fuse with a penetration deeper than the hull clamps to the far edge.
    [Fact]
    public void TheFusePointStaysOnMetal()
    {
        var e = Build(TestSettings(), SolidShape(1, 5), penetration: 100f, fuse: WeaponFuse.Delayed, blastRadius: .3f, damage: 100f);
        // The far edge of a 1x5 hull, straight ahead of the shooter (bearing (0,1)), is schematic y = 4.5 (the
        // exit of cell (0,4)); the fuse point clamps there instead of past the hull. Schematic x is the hull's
        // own CenterOfMass.x (0, the only column).
        var comX = e.HullData.Shape.CenterOfMass.x;
        var rCells = .3 / e.Items.GameplaySettings.SchematicCellSize; // BlastRadius is in world units

        // Independent expectation, from the test's own area integral centred on the far edge.
        var expected = 0f;
        foreach (var c in e.HullData.Shape.Coordinates)
            expected += (float) (100.0 * DiscAreaInCell(comX, 4.5, rCells, c) / (Math.PI * rCells * rCells));

        var before = e.Target.Hull.Durability;
        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
        {
            using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);
        var delivered = before - e.Target.Hull.Durability;
        Assert.Equal(expected, delivered, 0);
    }

    // TheFusePointStaysOnMetal, the concave half: a 1x5 column with cell (0,2) missing. A lane's walk ends at
    // its first gap, so the reached metal is cells (0,0) and (0,1) whatever the penetration, and the fuse point
    // must clamp to the last reached cell's exit (schematic y 1.5, the metal's edge) rather than sit at
    // entry + penetration inside the gap (y 2.4 at penetration 2.9) or on the far segment the walk never
    // reached (y 2.7 at 3.2). Entries run from -.5, so at penetration 1.5 the point is y 1.0, inside cell (0,1),
    // with the disc wholly on metal.
    [Theory]
    [InlineData(1.5f, 1.0)]
    [InlineData(2.9f, 1.5)]
    [InlineData(3.2f, 1.5)]
    public void TheFusePointStaysOnMetalAcrossAConcaveGap(float penetration, double pointY)
    {
        var shape = SolidShape(1, 5);
        shape[new int2(0, 2)] = false;
        var e = Build(TestSettings(), shape, penetration: penetration, fuse: WeaponFuse.Delayed, blastRadius: .3f, damage: 100f);
        var comX = e.HullData.Shape.CenterOfMass.x;
        var rCells = .3 / e.Items.GameplaySettings.SchematicCellSize;
        var expected = ExpectedDelivered(e.HullData.Shape, comX, pointY, rCells, 100.0);

        var before = e.Target.Hull.Durability;
        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
        {
            using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);
        Assert.Equal(expected, before - e.Target.Hull.Durability, 0);
    }

    // A contact or delayed blast needs a committed hit, exactly like a direct hit's own gate: on a miss nothing
    // happens -- no blast, no IncomingHit. Accuracy 0 pins the roll to a miss. Kills: ApplyBlastHit without its
    // `Outcome.Hit` guard, which would detonate at a point derived from a miss's empty geometry.
    [Theory]
    [InlineData(WeaponFuse.Contact)]
    [InlineData(WeaponFuse.Delayed)]
    public void AContactOrDelayedBlastOnAMissDoesNothing(WeaponFuse fuse)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), fuse: fuse, blastRadius: 30f, penetration: 1f, damage: 500f, accuracy: 0f);
        ArmourEverything(e);
        var before = e.Target.Hull.Durability;
        var events = 0;
        using var a = e.Target.ArmorDamage.Subscribe(_ => events++);
        using var h = e.Target.HullDamage.Subscribe(_ => events++);
        using var i = e.Target.ItemDamage.Subscribe(_ => events++);
        using var hit = e.Target.IncomingHit.Subscribe(_ => events++);
        ShotOutcome outcome = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(outcome);
        Assert.False(outcome.Hit); // fixture: a guaranteed miss
        Assert.Equal(ShotResult.Miss, outcome.Result);
        SafeAssert.NoShots(e.Zone);
        Assert.Equal(0, events);
        Assert.Equal(before, e.Target.Hull.Durability);
    }

    // Operator ruling 2026-09-30: "fused weapons without a lock explode at max range." A fused shot with no
    // target bursts at the weapon's Range along its aim (Entity.Aim), whatever its fuse. The fixture
    // aims the shooter along (2,1), a non-axis facing; witnesses sit at the burst point and beside it. Radius 4
    // with the witness hull 5x4 cells of 2 world units: a witness centred on the burst point is covered, and
    // one 30 units off the aim line is not.
    private const float NoLockRange = 60f;

    private static float3 AimLine(Engagement e)
    {
        var aim = normalize(float2(2, 1));
        return e.Shooter.Position + float3(aim.x, 0, aim.y) * NoLockRange;
    }

    [Theory]
    [InlineData(WeaponFuse.Proximity)]
    [InlineData(WeaponFuse.Contact)]
    [InlineData(WeaponFuse.Delayed)]
    public void AFusedShotWithNoTargetBurstsAtMaxRangeAlongTheAimLine(WeaponFuse fuse)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: fuse, blastRadius: 4f, penetration: 1f, damage: 100f,
            weaponRange: NoLockRange);
        Aim(e, float2(2, 1));
        e.Shooter.SetTarget(TargetRef.None);
        var burst = AimLine(e);
        var onLine = AddShip(e, burst, 1f, 300);
        var offLine = AddShip(e, burst + float3(-30, 0, 30), 1f, 302);
        var shooterHit = 0f;
        using var a = e.Shooter.ArmorDamage.Subscribe(x => shooterHit += x.damage);
        using var h = e.Shooter.HullDamage.Subscribe(x => shooterHit += x);
        using var i = e.Shooter.ItemDamage.Subscribe(x => shooterHit += x.damage);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.Equal(0f, shooterHit);
        SafeAssert.NoShots(e.Zone);
        Assert.False(e.Zone.Entities.Contains(onLine), "the blast must reach the burst point (fixture: 1 durability, so any damage kills it)");
        Assert.True(e.Zone.Entities.Contains(offLine), "the blast must not reach 42 units off the aim line");
    }

    // The no-lock shot flies to max range at the weapon's Velocity, like a targeted shot flies to its intercept.
    [Fact]
    public void ANoLockFusedShotFliesToMaxRangeAtItsVelocity()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        Aim(e, float2(2, 1));
        e.Shooter.SetTarget(TargetRef.None);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(NoLockRange / 20f, shot.ArrivalTime - shot.FireTime, 4);
        var burst = AimLine(e);
        Assert.Equal(burst.x, shot.BurstPosition.x, 3);
        Assert.Equal(burst.z, shot.BurstPosition.z, 3);
    }

    // A targeted shot is unchanged. Expected values computed at the base commit (b66ba524, before the no-lock
    // rule), not read back from this code: the target's total damage from a proximity blast at the intercept.
    [Fact]
    public void ATargetedFusedShotIsUnchangedByTheNoLockRule()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f);
        var damage = 0f;
        using var a = e.Target.ArmorDamage.Subscribe(x => damage += x.damage);
        using var h = e.Target.HullDamage.Subscribe(x => damage += x);
        using var i = e.Target.ItemDamage.Subscribe(x => damage += x.damage);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(e.Target.Position.x, shot.BurstPosition.x, 3);
        Assert.Equal(e.Target.Position.z, shot.BurstPosition.z, 3);
        e.Zone.Update(.01f);

        Assert.Equal(5.65884256f, damage, 3);
    }

    // ==== Operator rulings 2026-09-30 (Soul's pass on the no-lock fix): aim, invalid target, contact, shooter ====

    private static void Aim(Engagement e, float2 direction) => e.Shooter.Aim = float3(direction.x, 0, direction.y);

    private static float3 PointAlong(Engagement e, float2 direction, float distance) =>
        e.Shooter.Position + float3(direction.x, 0, direction.y) * distance;

    private static bool Alive(Engagement e, Entity entity) => e.Zone.Entities.Contains(entity);

    // Ruling 1: an aim beyond the mount's arc flies along the nearest edge of that arc. The mount is (0,1), the
    // arc 170 degrees (edge 85), and the aim (+-1,-1) sits 135 degrees off it; the witness on the edge dies and
    // the one under the unclamped aim does not (they are 50 units apart). Kills: no clamp; the turn taken the
    // wrong way round (the mirrored edge holds no witness).
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void ANoLockShotOutsideTheMountArcFliesAlongTheNearestEdgeOfIt(float side)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(side, -1));
        var edge = float2(side * sin(radians(85f)), cos(radians(85f)));
        var onEdge = AddShip(e, PointAlong(e, edge, NoLockRange), 1f, 300);
        var underAim = AddShip(e, PointAlong(e, normalize(float2(side, -1)), NoLockRange), 1f, 302);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.False(Alive(e, onEdge), "the blast must land on the arc's edge");
        Assert.True(Alive(e, underAim), "the blast must not follow an aim the arc does not allow");
    }

    // Ruling 1: an aim inside the arc is used as it is (unnormalised here), and a shooter aiming at nothing
    // (Aim zero) fires down its mount. Kills: reading MountDirection instead of the aim; a missing
    // zero-aim fallback, which would normalise to NaN and burst nowhere.
    [Fact]
    public void ANoLockShotFliesAlongTheAimAndDownTheMountWhenNothingIsAimed()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        var mount = float2(0, 1);
        var aim = normalize(float2(2, 1));
        var onAim = AddShip(e, PointAlong(e, aim, NoLockRange), 1f, 300);
        var onMount = AddShip(e, PointAlong(e, mount, NoLockRange), 1f, 302);

        Aim(e, float2(6, 3));
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        Assert.False(Alive(e, onAim), "the blast must follow the aim");
        Assert.True(Alive(e, onMount), "the mount direction is not the aim");

        Aim(e, float2(0, 0));
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        Assert.False(Alive(e, onMount), "with nothing aimed the blast follows the mount");
    }

    public enum Invalid { OutOfRange, NotVisible, Unlocked }

    // Ruling 2: a selected target that is out of range, not visible or not locked is not valid targeting data,
    // and counts as no lock -- the round bursts at max range along the aim, and the target is left alone. Kills:
    // reading "no lock" as Target == null alone (the round would burst at the target's intercept, 100 / 40 / 40
    // units out); dropping the range, visibility or lock gate from PFire's `designated`. The lock row needs a
    // LockWeapon whose lock never grows; its control, below, is the same gun with the lock acquired. (A closed
    // arc is not an invalid target: see AFusedShotAtATargetOutsideTheArc...)
    [Theory]
    [InlineData(Invalid.OutOfRange)]
    [InlineData(Invalid.NotVisible)]
    [InlineData(Invalid.Unlocked)]
    public void AnInvalidTargetCountsAsNoLock(Invalid reason)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange, targetRange: reason == Invalid.OutOfRange ? 100f : 40f,
            lockWeapon: reason == Invalid.Unlocked, lockAcquires: false);
        var aim = normalize(float2(2, 1));
        if (reason == Invalid.NotVisible) e.Shooter.VisibleEntities.Remove(e.Target);
        Aim(e, aim);
        var burst = PointAlong(e, aim, NoLockRange);
        var onAim = AddShip(e, burst, 1f, 300);
        var targetBefore = e.Target.Hull.Durability;

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == null, "an invalid target must not ride with the round"); // not Assert.Null: a failure would format the entity graph and overflow the stack
        Assert.Equal(burst.x, shot.BurstPosition.x, 3);
        Assert.Equal(burst.z, shot.BurstPosition.z, 3);
        e.Zone.Update(.01f);

        Assert.False(Alive(e, onAim), "the blast must land at max range along the aim");
        Assert.Equal(targetBefore, e.Target.Hull.Durability);
    }

    // The lock row's control: the same LockWeapon once its lock has grown carries its target to the intercept.
    // Kills: a fixture whose lock could never grow (which would make the unlocked row above pass for nothing).
    [Fact]
    public void ALockedTargetRidesWithTheRound()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange, targetRange: 40f, lockWeapon: true, lockAcquires: true);
        Aim(e, float2(0, 1));
        e.Zone.Update(.1f);
        Assert.True(((LockWeapon) e.Weapon).IsLocked, "fixture: the lock must have grown");

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == e.Target, "a locked, visible, in-range, in-arc target rides with the round");
        Assert.Equal(e.Target.Position.z, shot.BurstPosition.z, 3);
    }

    // The out-of-arc ruling (operator, 2026-09-30): a fused round at a target it holds valid data on but cannot
    // bear on flies along the aim clamped to the mount's arc and bursts at the TARGET'S range, not at max range.
    // The mount is flipped to (0,-1) so the target 40 ahead of the shooter is 180 degrees off it, in range,
    // visible and unlocked by nothing: the arc is the only closed gate. The aim (1,1) is 135 degrees off the
    // mount, so it clamps to the 85-degree edge on its own side. Three witnesses: on the edge at 40 (dies), on
    // the edge at max range 60 (lives: the burst is at target range), under the unclamped aim at 40 (lives: the
    // clamp holds). Kills: dropping `inArc` from PFire (the target would ride and burst at 40 along the target
    // line); bursting at Range instead of the target's range; skipping the clamp. Contact is left out: its round
    // stops on the first hull it meets, and these witnesses are hulls.
    [Theory]
    [InlineData(WeaponFuse.Proximity)]
    [InlineData(WeaponFuse.Delayed)]
    public void AFusedShotAtATargetOutsideTheArcBurstsAtTheTargetsRangeAlongTheClampedAim(WeaponFuse fuse)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: fuse, blastRadius: 4f, penetration: 1f, damage: 100f,
            weaponRange: NoLockRange, targetRange: 40f);
        e.Shooter.Direction = float2(0, -1);
        var look = normalize(float2(1, 1));
        Aim(e, look);
        var mount = float2(0, -1);
        var edge = float2(mount.x * cos(radians(85f)) - mount.y * sin(radians(85f)), mount.x * sin(radians(85f)) + mount.y * cos(radians(85f)));
        var atTargetRange = AddShip(e, PointAlong(e, edge, 40f), 1f, 300);
        var atMaxRange = AddShip(e, PointAlong(e, edge, NoLockRange), 1f, 302);
        var underAim = AddShip(e, PointAlong(e, look, 40f), 1f, 304);
        var targetBefore = e.Target.Hull.Durability;

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == null, "a target the weapon cannot bear on must not ride with the round");
        var burst = PointAlong(e, edge, 40f);
        Assert.Equal(burst.x, shot.BurstPosition.x, 3);
        Assert.Equal(burst.z, shot.BurstPosition.z, 3);
        e.Zone.Update(.01f);

        Assert.False(Alive(e, atTargetRange), "the blast must land at the target's range along the clamped aim");
        Assert.True(Alive(e, atMaxRange), "the round bursts at the target's range, not at max range");
        Assert.True(Alive(e, underAim), "the aim is clamped to the arc's edge");
        Assert.Equal(targetBefore, e.Target.Hull.Durability);
    }

    // The trigger and Fire share one bearing test (InArc) and differ only in the fused weapon's exemption, so
    // this pins the pair at the layer a player uses: Activate. With the target out of arc and the weapon
    // otherwise clear, a fused weapon fires a round; the same weapon without a fuse stays silent. Kills:
    // removing the exemption from FireControl.ArcPermitsFire (the fused row goes silent); removing the arc gate
    // itself (the plain row fires); widening the exemption to a weapon with a blast radius but no fuse.
    [Theory]
    [InlineData(WeaponFuse.Proximity, true)]
    [InlineData(null, false)]
    public void OnlyAFusedWeaponFiresAtATargetOutsideItsArc(WeaponFuse? fuse, bool fires)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: fuse, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange, targetRange: 40f);
        e.Shooter.Direction = float2(0, -1);
        Aim(e, float2(0, 1)); // outside the flipped mount's arc too: no solution, and the aim is not free
        Assert.Equal(fires, e.Weapon.ArcAllowsFire);
        var committed = 0;
        using var c = e.Zone.ShotCommitted.Subscribe(_ => committed++);

        e.Weapon.Activate();
        for (var i = 0; i < 5; i++) e.Zone.Update(.01f);

        Assert.Equal(fires ? 1 : 0, committed);
    }

    // ==== Operator ruling 2026-09-30, batch 4: the arming distance ====
    //
    // A round never bursts closer than its blast radius (its arming distance), measured from the shooter's
    // position and blind to hull size: the fixture's shooter hull is a 5x5 block of 2-unit cells, and a blast of 30
    // arms at 30, not 30 plus whatever the hull reaches. A shooter close to its own burst takes the splash.

    private static float Planar(float3 from, float3 to) => length((to - from).xz);

    // A target inside the arming distance is not detonated on: the round flies on to the arming distance, exactly
    // the blast radius from the shooter (35 would be the hull-inclusive figure this replaced). An in-arc target
    // stays the round's target and the burst is pushed out along the line to it; the out-of-arc row is a target 10
    // away behind the mount, inside the blast radius, with the mount flipped so the arc is closed: no target rides
    // and the burst is on the aim. Kills: no arming distance; a hull term in it; an in-arc round demoted to no
    // target; an out-of-arc round that keeps one.
    [Theory]
    [InlineData(5f, false)]
    [InlineData(10f, true)]
    public void ACloseBurstIsPushedOutToTheBlastRadius(float targetRange, bool outOfArc)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f,
            weaponRange: 60f, targetRange: targetRange);
        var aim = float2(0, 1);
        if (outOfArc)
        {
            e.Shooter.Direction = float2(0, -1);
            aim = float2(0, -1);
        }
        Aim(e, aim);
        var burst = PointAlong(e, aim, 30f);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True((shot.Target == e.Target) == !outOfArc, "only a round with a target it can bear on keeps it");
        Assert.Equal(burst.x, shot.BurstPosition.x, 2);
        Assert.Equal(burst.z, shot.BurstPosition.z, 2);
    }

    // A round pushed out to its arming distance is still a targeted round (operator ruling 2026-09-30): if its target
    // leaves the zone before it resolves, it does not burst. The target is 5 units away, inside a blast of 30, and
    // the round flies 1.5 seconds; the target is gone after the first tick. The round resolves as a miss with no
    // burst point, and the shooter standing inside the would-be disc is untouched. Kills: demoting a close round
    // to no target (the round then bursts on schedule and hurts the shooter).
    [Fact]
    public void APushedOutRoundWhoseTargetLeavesTheZoneDoesNotBurst()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f,
            weaponRange: 60f, targetRange: 5f);
        Aim(e, float2(0, 1));
        var hurt = 0f;
        using var a = e.Shooter.ArmorDamage.Subscribe(x => hurt += x.damage);
        using var h = e.Shooter.HullDamage.Subscribe(x => hurt += x);
        using var i = e.Shooter.ItemDamage.Subscribe(x => hurt += x.damage);
        ShotOutcome resolved = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => resolved = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        Assert.True(SafeAssert.OnlyShot(e.Zone).Target == e.Target, "fixture: the pushed-out round keeps its target");
        e.Zone.Update(.01f);
        e.Zone.Entities.Remove(e.Target);
        e.Zone.Update(3f);

        SafeAssert.NoShots(e.Zone);
        Assert.NotNull(resolved);
        Assert.Equal(ShotResult.Miss, resolved.Result);
        Assert.False(resolved.HasBurstPoint, "a round whose target is gone does not burst");
        Assert.Equal(0f, hurt);
    }

    public enum Publication { Committed, Resolved }

    // A presentation observer that throws costs its own notification and nothing more: the shot's queue state is
    // settled before anything is published, so it is not committed or resolved a second time on every later tick
    // (the queue used to wedge, re-throwing forever). The first tick throws; the second does not, the shot is gone
    // or committed exactly once. Kills: publishing before the shot is committed in the list or removed from it.
    [Theory]
    [InlineData(Publication.Committed)]
    [InlineData(Publication.Resolved)]
    public void AThrowingObserverCannotWedgeTheShotQueue(Publication throwsOn)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f);
        var commits = 0;
        var resolutions = 0;
        using var c = e.Zone.ShotCommitted.Subscribe(_ => { commits++; if (throwsOn == Publication.Committed) throw new InvalidOperationException("observer"); });
        using var r = e.Zone.ShotResolved.Subscribe(_ => { resolutions++; if (throwsOn == Publication.Resolved) throw new InvalidOperationException("observer"); });
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        Assert.ThrowsAny<Exception>(() => e.Zone.Update(.01f));
        var second = Record.Exception(() => e.Zone.Update(.01f));

        Assert.Null(second);
        Assert.Equal(1, commits);
        Assert.True(resolutions <= 1, $"resolved {resolutions} times");
    }

    // The same for the miss a departed target publishes: the shot is removed before that notification, so an
    // observer that throws on it cannot keep the shot in the queue to be cancelled, and thrown at, on every tick.
    // Kills: publishing the miss before removing the shot.
    [Fact]
    public void AThrowingObserverOfADepartedTargetsMissCannotWedgeTheShotQueue()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        using var c = e.Zone.ShotCommitted.Subscribe(_ => throw new InvalidOperationException("observer"));
        e.Zone.Entities.Remove(e.Target);

        Assert.ThrowsAny<Exception>(() => e.Zone.Update(.01f));
        var second = Record.Exception(() => e.Zone.Update(.01f));

        Assert.Null(second);
        SafeAssert.NoShots(e.Zone);
    }

    public enum Observer { HullDamage, ArmorDamage, IncomingHit }

    // Nothing the queue does is left to a user callback that may throw: a shot is out of the queue before its
    // damage is applied, so a damage observer that throws costs the rest of that blast, once, and the shot is not
    // applied again on every later tick (its blast, and the observer, used to repeat forever). The observer throws
    // on its first call; the following tick is clean and the observer has been called exactly once. Contact for
    // the hit report (a roll can miss, so it retries), Proximity for the damage events.
    // Kills: applying the shot's damage before removing it from the queue.
    [Theory]
    [InlineData(Observer.HullDamage)]
    [InlineData(Observer.ArmorDamage)]
    [InlineData(Observer.IncomingHit)]
    public void AThrowingDamageObserverIsCalledOnceAndCannotWedgeTheShotQueue(Observer observer)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: observer == Observer.IncomingHit ? WeaponFuse.Contact : WeaponFuse.Proximity,
            blastRadius: 4f, penetration: 1f, damage: 100f);
        if (observer == Observer.ArmorDamage) ArmourEverything(e);
        var calls = 0;
        void Throw() { calls++; throw new InvalidOperationException("observer"); }
        using var a = observer == Observer.ArmorDamage ? e.Target.ArmorDamage.Subscribe(_ => Throw()) : Disposable.Empty;
        using var h = observer == Observer.HullDamage ? e.Target.HullDamage.Subscribe(_ => Throw()) : Disposable.Empty;
        using var i = observer == Observer.IncomingHit ? e.Target.IncomingHit.Subscribe(_ => Throw()) : Disposable.Empty;

        for (var attempt = 0; attempt < 60 && calls == 0; attempt++)
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            Record.Exception(() => e.Zone.Update(.01f));
        }
        Assert.Equal(1, calls);
        var later = Record.Exception(() => e.Zone.Update(.01f));

        Assert.Null(later);
        Assert.Equal(1, calls);
        SafeAssert.NoShots(e.Zone);
    }

    // A round pushed out to its arming distance flies the arming distance: its arrival is that far off, not the
    // target's own (5 units, a quarter second) range. 30 units at 20 units/s is 1.5 seconds. Kills: a flight
    // distance of the target's range for a pushed-out round.
    [Fact]
    public void APushedOutRoundDoesNotArriveBeforeItHasFlownTheArmingDistance()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f,
            weaponRange: 60f, targetRange: 5f);
        Aim(e, float2(0, 1));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == e.Target, "fixture: the pushed-out round keeps its target");
        Assert.Equal(1.5f, shot.ArrivalTime - shot.FireTime, 3);
    }

    // The same for a no-lock round: a target 10 out behind a mount whose arc is closed is designated but out of
    // arc, so the round flies the aim to the target's range, inside the blast radius, and is pushed out to 30. It
    // arrives after 30 units at 20 units/s, not after the 10 it was aimed at. Kills: a no-lock flight distance
    // that ignores the arming distance.
    [Fact]
    public void APushedOutNoLockRoundDoesNotArriveBeforeItHasFlownTheArmingDistance()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f,
            weaponRange: 60f, targetRange: 10f);
        e.Shooter.Direction = float2(0, -1);
        Aim(e, float2(0, -1));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == null, "fixture: out of arc, the round carries no target");
        Assert.Equal(1.5f, shot.ArrivalTime - shot.FireTime, 3);
    }

    // What the shooter's own hull takes from one round at `targetRange`, counting only rounds that hit when the
    // fuse needs a hit (a roll can miss, so it retries).
    private static float ShooterSplash(Engagement e, bool needsHit)
    {
        var taken = 0f;
        using var a = e.Shooter.ArmorDamage.Subscribe(x => taken += x.damage);
        using var h = e.Shooter.HullDamage.Subscribe(x => taken += x);
        using var i = e.Shooter.ItemDamage.Subscribe(x => taken += x.damage);
        ShotOutcome outcome = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);
        for (var attempt = 0; attempt < 60; attempt++)
        {
            taken = 0f;
            outcome = null;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
            if (!needsHit || outcome != null && outcome.Hit) return taken;
        }
        throw new InvalidOperationException("no round hit in 60 attempts");
    }

    // The arming ruling holds for every fuse, at the point a contact or delayed round detonates on a hull: no
    // burst nearer to the shooter than the blast radius. A round that hits a target inside the radius (5 out), or
    // one whose near face is inside it (a target 32 out has its face at 28), is detonated on at the radius (30),
    // so the shooter takes what any burst 30 out on its line deals: the amount a proximity round at exactly 30
    // deals. Kills: a hull-point detonation that ignores the arming distance (the shooter then took 7.07 from a
    // blast 30 at 5 out, more than the armed burst).
    [Theory]
    [InlineData(WeaponFuse.Contact, 5f)]
    [InlineData(WeaponFuse.Delayed, 5f)]
    [InlineData(WeaponFuse.Contact, 32f)]
    public void AContactOrDelayedBlastNeverDetonatesNearerThanTheArmingDistance(WeaponFuse fuse, float targetRange)
    {
        var armed = ShooterSplash(Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, penetration: 1f,
            damage: 100f, weaponRange: 60f, targetRange: 30f), needsHit: false);
        Assert.True(armed > 0f, "fixture: the shooter's own hull takes splash from a burst at the radius");

        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: fuse, blastRadius: 30f, penetration: 1f,
            damage: 100f, weaponRange: 60f, targetRange: targetRange);
        var splash = ShooterSplash(e, needsHit: true);

        Assert.Equal(armed, splash, 3);
    }

    // A target beyond the arming distance is not pushed anywhere: the round still rides it and bursts at its
    // intercept. Kills: demoting every fused round to the aim.
    [Fact]
    public void ATargetBeyondTheArmingDistanceStillRidesWithTheRound()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f,
            weaponRange: 60f, targetRange: 40f);
        Aim(e, float2(0, 1));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == e.Target, "a target the disc clears the shooter to reach is still the round's target");
        Assert.Equal(40f, Planar(e.Shooter.Position, shot.BurstPosition), 2);
    }

    // A contact round stops on the first hull it crosses, but never nearer than its arming distance: a blocker
    // whose near face is 2 units out (centred 6) is passed, and the round bursts at its blast radius, 4. Kills: a
    // contact point taken as the hull face regardless of arming.
    [Fact]
    public void AContactRoundNeverStopsNearerThanItsArmingDistance()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f, weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        AddShip(e, PointAlong(e, float2(0, 1), 6f), 1f, 300);
        ShotOutcome outcome = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => outcome = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(outcome);
        Assert.Equal(e.Shooter.Position.z + 4f, outcome.BurstPoint.y, 2);
    }

    // A round fired from inside another hull is already in metal: its contact point is its arming distance. The
    // host is a column with a 30-unit gap after the cell the muzzle sits in (cells 0-2 and 18-21, the muzzle in
    // cell 2), so a contact search that skips the cell the origin is inside finds the far block instead of the
    // muzzle's own. Kills: reading a cell's Entry for its Exit (Soul's M7).
    [Fact]
    public void AContactRoundFiredFromInsideAnotherHullBurstsAtTheArmingDistance()
    {
        var column = new Shape(1, 22);
        foreach (var y in new[] { 0, 1, 2, 18, 19, 20, 21 }) column[new int2(0, y)] = true;
        var e = Build(TestSettings(), column, velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f, weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var offset = e.Target.ToWorldPoint(float2(0, 2)) - e.Target.Position.xz;
        e.Target.Position = float3(e.Shooter.Position.x - offset.x, 0, e.Shooter.Position.z - offset.y);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(committed);
        Assert.Equal(e.Shooter.Position.z + 4f, committed.BurstPoint.y, 2);
    }

    // A fused round is refused, per round and at the moment Fire reads Range, when its arming distance (the blast
    // radius, 30) exceeds that Range: 29.5 refuses, 30.01 fires, and a power-starved Range (authored 100, forced to about
    // 10 the way Soul's probe did after the trigger has passed) refuses although the authored figure clears
    // it. Fire returns 0 and queues nothing. Kills: a gate that reads the authored Range; `>=` for `>`.
    [Theory]
    [InlineData(29.5f, false, false)]
    [InlineData(30.01f, false, true)]
    [InlineData(100f, true, false)]
    public void AFusedRoundWhoseArmingDistanceExceedsRangeIsRefusedByFire(float range, bool starvedOfPower, bool fires)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f,
            weaponRange: range, rangeTerm: starvedOfPower ? StatSource.PowerSupply : (StatSource?) null);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        if (starvedOfPower)
        {
            typeof(EquippedItem).GetProperty("PowerSupply").GetSetMethod(true).Invoke(e.WeaponItem, new object[] { .1f });
            e.Shooter.Resolver.InvalidateSource(e.WeaponItem, StatSource.PowerSupply);
            e.Weapon.GetType().GetMethod("UpdateStats", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(e.Weapon, null);
            Assert.True(e.Weapon.Range < 30f, "fixture: the runtime Range must have fallen below the arming distance");
        }

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        Assert.Equal(fires, shotId != 0);
        Assert.Equal(fires ? 1 : 0, e.Zone.PendingShots.Count);
    }

    // The boundary itself: a Range exactly equal to the arming distance (the blast radius) fires. Kills: `>=` for
    // `>`.
    [Fact]
    public void ARangeExactlyEqualToTheArmingDistanceFires()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f, weaponRange: 30f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));

        Assert.True(FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter) != 0, "a Range equal to the arming distance must fire");
    }

    // The same refusal through the weapon a player uses: a refused round sounds nothing (OnFire never runs) and
    // queues nothing, and the same weapon with Range to spare fires once. Kills: Execute still announcing a
    // refused round (Fire's 0 handed to the presentation as a shot id).
    [Theory]
    [InlineData(29.5f, 0)]
    [InlineData(40f, 1)]
    public void ARefusedRoundIsNotAnnounced(float range, int fired)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f, weaponRange: range);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var announced = 0;
        var committed = 0;
        e.Weapon.OnFire += _ => announced++;
        using var c = e.Zone.ShotCommitted.Subscribe(_ => committed++);

        e.Weapon.Activate();
        for (var i = 0; i < 5; i++) e.Zone.Update(.01f);

        Assert.Equal(fired, announced);
        Assert.Equal(fired, committed);
    }

    // Planar, everywhere the gate, the clamp and the burst meet (R7). A target 50 units above the plane at a
    // planar range of 40 is 64 units away in three dimensions: inside a Range of 60 by the plane, outside it by
    // the straight line. It is designated, rides with the round, and the HUD's diagnostic agrees. Kills: a 3D
    // length in PFire's range or Inspect's.
    [Fact]
    public void ATargetsHeightDoesNotMoveTheRangeGate()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: 60f, targetRange: 40f);
        e.Target.Position += float3(0, 50, 0);
        Aim(e, float2(0, 1));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.True(shot.Target == e.Target, "the target is inside Range in the plane, so it is valid data");
        Assert.Equal(40f, Planar(e.Shooter.Position, shot.BurstPosition), 2);
        var diagnostic = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
        Assert.True(diagnostic.Designated, "the HUD's designation is planar too");
        Assert.Equal(40f, diagnostic.Range, 2);
    }

    // The F4 clamp is planar as well: the target's height does not change how far the burst is pulled back.
    [Fact]
    public void TheRangeClampIgnoresTheTargetsHeight()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: 100f, targetRange: 90f);
        e.Target.Position += float3(0, 200, 0);
        e.Target.Velocity = float2(6f, 8f);
        var intercept = FireControl.PredictedIntercept(e.Weapon, e.Shooter, e.Target);
        Assert.True(Planar(e.Shooter.Position, intercept) > 100f, "fixture: the intercept must lie beyond Range in the plane");

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(100f, Planar(e.Shooter.Position, shot.BurstPosition), 2);
    }

    // Nothing bursts beyond max range. A target receding faster than the round would be led past Range by the
    // intercept; the burst is pulled back along the line to the intercept, to exactly Range. The target moves
    // (6,8), so the direction is off-axis and a clamp that moved the point along the wrong line shows.
    // Kills: no clamp (the probe burst 180 units out on Range 100); a clamp on one axis only.
    [Fact]
    public void ATargetedProximityBurstIsPulledBackToMaxRange()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: 100f, targetRange: 90f);
        e.Target.Velocity = float2(6f, 8f);
        var intercept = FireControl.PredictedIntercept(e.Weapon, e.Shooter, e.Target);
        var origin = e.Shooter.Position;
        var toIntercept = normalize((intercept - origin).xz);
        Assert.True(length((intercept - origin).xz) > 100f, "fixture: the intercept must lie beyond Range");

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);

        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(origin.x + toIntercept.x * 100f, shot.BurstPosition.x, 2);
        Assert.Equal(origin.z + toIntercept.y * 100f, shot.BurstPosition.z, 2);
    }

    // What a proximity round's commit says. A round whose roll missed still detonates, so it commits as a Burst
    // at the intercept, and only a roll that landed is a Hit; a contact or delayed round detonates only on a hit,
    // so its guaranteed miss (accuracy 0) stays a Miss and explodes nowhere. Kills: typing a proximity miss as a
    // Miss (the HUD said MISS over a real explosion); typing every proximity round a Burst (no Hit among the
    // twelve); dropping the burst point from a Hit (it would detonate at the origin).
    [Fact]
    public void AProximityRoundThatMissesCommitsAsABurstAtTheIntercept()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f, accuracy: 0f);
        var damage = 0f;
        using var a = e.Target.ArmorDamage.Subscribe(x => damage += x.damage);
        using var h = e.Target.HullDamage.Subscribe(x => damage += x);
        using var i = e.Target.ItemDamage.Subscribe(x => damage += x.damage);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(committed);
        Assert.Equal(ShotResult.Burst, committed.Result);
        Assert.Equal(e.Target.Position.x, committed.BurstPoint.x, 3);
        Assert.Equal(e.Target.Position.z, committed.BurstPoint.y, 3);
        Assert.Equal(5.65884256f, damage, 3);
    }

    [Fact]
    public void AProximityRoundThatLandsCommitsAsAHitAndStillDetonates()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f);
        var results = new List<ShotOutcome>();
        using var r = e.Zone.ShotResolved.Subscribe(o => results.Add(o));
        var damage = 0f;
        using var a = e.Target.ArmorDamage.Subscribe(x => damage += x.damage);
        using var h = e.Target.HullDamage.Subscribe(x => damage += x);
        using var i = e.Target.ItemDamage.Subscribe(x => damage += x.damage);

        for (var n = 0; n < 12; n++) FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.Equal(12, results.Count);
        Assert.True(results.All(o => o.Result != ShotResult.Miss), "a proximity round always detonates, so it is never a Miss");
        Assert.True(results.Any(o => o.Result == ShotResult.Hit), "the stationary, fully-accurate target must be hit at least once");
        Assert.True(results.All(o => abs(o.BurstPoint.x - e.Target.Position.x) < 1e-3f && abs(o.BurstPoint.y - e.Target.Position.z) < 1e-3f),
            "a hit detonates at the intercept too, not at the origin");
        Assert.True(results.All(o => o.HasBurstPoint), "a proximity outcome names its burst point, hit or not: presentation retargets to it");
        Assert.True(damage > 0f, "the blasts must have landed");
    }

    [Theory]
    [InlineData(WeaponFuse.Contact)]
    [InlineData(WeaponFuse.Delayed)]
    public void AContactOrDelayedRoundThatMissesIsAMissAndDoesNotDetonate(WeaponFuse fuse)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: fuse, blastRadius: 30f, penetration: 1f, damage: 100f, accuracy: 0f);
        var bystander = AddShip(e, e.Target.Position + float3(8, 0, 0), 1f, 300);
        ShotOutcome resolved = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => resolved = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(resolved);
        Assert.Equal(ShotResult.Miss, resolved.Result);
        Assert.False(resolved.HasBurstPoint, "a contact or delayed round has no frozen burst point");
        Assert.True(Alive(e, bystander), "a missed contact or delayed round detonates nowhere");
    }

    // A targeted fused round commits at its horizon, not at Fire: the target's facing is read late (Bearing
    // timing), and only a round with no target commits at once. 100 units at 20/s is a 5 s flight, so with the
    // 0.5 s horizon it commits at 4.5 s. Kills: CommitTime = now for every fused shot (the round would be
    // committed after the first tick).
    [Fact]
    public void ATargetedFusedShotCommitsAtItsHorizonNotAtFire()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = SafeAssert.OnlyShot(e.Zone);
        Assert.Equal(shot.FireTime + 4.5f, shot.CommitTime, 3);
        e.Zone.Update(.01f);
        Assert.False(e.Zone.PendingShots[0].Committed, "a targeted round reads the target's facing late");
        e.Zone.Update(4.5f);
        Assert.True(e.Zone.PendingShots[0].Committed, "and commits once its horizon is reached, with the round still in flight");
    }

    // Ruling 3 (operator, 2026-09-30): a no-lock round of any fuse stops on the first hull its aim line crosses
    // before max range and detonates there; with the line clear it bursts at max range. The blocker's near face
    // sits 21 units out (a 5x4-cell hull of 2-unit cells centred 25 out), the witness at max range (60). Kills: a
    // fuse that ignores hulls (a Proximity or Delayed round used to fly on to max range).
    [Theory]
    [InlineData(WeaponFuse.Contact)]
    [InlineData(WeaponFuse.Proximity)]
    [InlineData(WeaponFuse.Delayed)]
    public void ANoLockRoundOfAnyFuseStopsOnTheFirstHullItCrosses(WeaponFuse fuse)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: fuse, blastRadius: 4f, penetration: 1f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var blocker = AddShip(e, PointAlong(e, float2(0, 1), 25f), 1f, 300);
        var atMaxRange = AddShip(e, PointAlong(e, float2(0, 1), NoLockRange), 1f, 302);
        ShotOutcome outcome = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => outcome = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(outcome);
        const float expectedReach = 21f;
        Assert.Equal(e.Shooter.Position.z + expectedReach, outcome.BurstPoint.y, 2);
        Assert.Equal(e.Shooter.Position.x, outcome.BurstPoint.x, 2);
        Assert.False(Alive(e, blocker));
        Assert.True(Alive(e, atMaxRange), "the witness at max range is beyond the stop");
    }

    // Ruling 3: a hull beyond max range is not on the round's way, and the shooter's own hull, which the line
    // starts inside, is never a candidate. Here the target (100 out) lies on the line of a 60-range Contact
    // round. Kills: a burst point beyond the max-range point; a Contact search that admits the shooter.
    [Fact]
    public void AContactRoundWithNoLockBurstsAtMaxRangeWhenNoHullIsInReach()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var targetBefore = e.Target.Hull.Durability;
        var shooterHit = 0f;
        using var a = e.Shooter.ArmorDamage.Subscribe(x => shooterHit += x.damage);
        using var h = e.Shooter.HullDamage.Subscribe(x => shooterHit += x);
        using var i = e.Shooter.ItemDamage.Subscribe(x => shooterHit += x.damage);
        ShotOutcome outcome = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => outcome = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.Equal(e.Shooter.Position.z + NoLockRange, outcome.BurstPoint.y, 2);
        Assert.Equal(targetBefore, e.Target.Hull.Durability);
        Assert.Equal(0f, shooterHit);
    }

    // Ruling 3: a Contact round bursting short of max range arrives when it has flown that far -- 21 of 60 units
    // at 20 units/s is 1.05 s into a 3 s flight -- and the commit says so through ArrivalIn. Kills: an arrival
    // left at the max-range time (the round would still be pending at 1.2 s, and ArrivalIn would read ~3).
    [Fact]
    public void AContactBurstShortOfMaxRangeArrivesWhenTheRoundHasFlownThatFar()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var blocker = AddShip(e, PointAlong(e, float2(0, 1), 25f), 1f, 300);
        ShotOutcome outcome = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => outcome = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        Assert.NotNull(outcome);
        Assert.Equal(1.05f - .01f, outcome.ArrivalIn, 2);
        Assert.True(Alive(e, blocker));

        e.Zone.Update(1.1f);
        SafeAssert.NoShots(e.Zone);
        Assert.False(Alive(e, blocker));
    }

    // Ruling 4: a round outlives its shooter. A no-lock proximity round whose shooter leaves the zone in flight
    // still detonates at max range, whether the shooter goes before or after the commit; a targeted proximity
    // round does the same at its intercept, for the same damage a shooter who stayed would have dealt (the value
    // measured for ATargetedFusedShotIsUnchangedByTheNoLockRule at b66ba524, 5.65884256). Kills: Step
    // cancelling a shot whose source has left the zone.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ANoLockRoundStillBurstsAfterItsShooterLeavesTheZone(int updatesBeforeLeaving)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var witness = AddShip(e, PointAlong(e, float2(0, 1), NoLockRange), 1f, 300);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        for (var i = 0; i < updatesBeforeLeaving; i++) e.Zone.Update(.01f);
        e.Zone.Entities.Remove(e.Shooter);
        e.Zone.Update(4f);

        SafeAssert.NoShots(e.Zone);
        Assert.False(Alive(e, witness), "the round must still burst at max range");
    }

    [Fact]
    public void ATargetedFusedRoundStillBurstsAfterItsShooterLeavesTheZone()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Proximity, blastRadius: 30f, damage: 100f);
        var damage = 0f;
        using var a = e.Target.ArmorDamage.Subscribe(x => damage += x.damage);
        using var h = e.Target.HullDamage.Subscribe(x => damage += x);
        using var i = e.Target.ItemDamage.Subscribe(x => damage += x.damage);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        e.Zone.Entities.Remove(e.Shooter);
        e.Zone.Update(6f);

        SafeAssert.NoShots(e.Zone);
        Assert.Equal(5.65884256f, damage, 3);
    }

    // Ruling 4, direct hits: rounds fired at a target and in flight when their shooter leaves still hit it. A dozen
    // shots each roll their own dice (seeded by shot id), and with a stationary target and a full-accuracy
    // targeting system nearly all land; the test needs only that all resolve and some hit, and that each hit
    // still names the departed shooter (identity carried, never asked anything).
    [Fact]
    public void ADirectRoundStillHitsAfterItsShooterLeavesTheZone()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, damage: 5f);
        var resolved = new List<ShotOutcome>();
        using var r = e.Zone.ShotResolved.Subscribe(o => resolved.Add(o));
        var struck = 0;
        using var s = e.Target.IncomingHit.Subscribe(src => { if (src == e.Shooter) struck++; });

        for (var i = 0; i < 12; i++) FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        e.Zone.Entities.Remove(e.Shooter);
        e.Zone.Update(6f);

        SafeAssert.NoShots(e.Zone);
        Assert.Equal(12, resolved.Count);
        Assert.True(resolved.Any(o => o.Hit), "at least one round must land on the stationary target");
        Assert.Equal(resolved.Count(o => o.Hit), struck);
    }

    // Ruling 6: what a commit came to is typed. A no-lock burst is neither a hit nor a miss, and reads as neither.
    // Kills: MakeOutcome collapsing a burst back to a miss (the debug HUD read MISS for a real explosion).
    [Fact]
    public void ANoLockBurstCommitsAsABurstNotAMiss()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, fuse: WeaponFuse.Proximity, blastRadius: 4f, damage: 100f,
            weaponRange: NoLockRange);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(2, 1));
        ShotOutcome committed = null, resolved = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);
        using var r = e.Zone.ShotResolved.Subscribe(o => resolved = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(committed);
        Assert.Equal(ShotResult.Burst, committed.Result);
        Assert.True(committed.HasBurstPoint);
        Assert.False(committed.Hit);
        var burst = PointAlong(e, normalize(float2(2, 1)), NoLockRange);
        Assert.Equal(burst.x, committed.BurstPoint.x, 3);
        Assert.Equal(burst.z, committed.BurstPoint.y, 3);
        Assert.True(ReferenceEquals(committed, resolved), "the resolution republishes the commit unchanged");
    }

    // ==== Soul's probes on hands/fuse-fix2 (2026-09-30), adopted as tests ====

    private static float2 Dir(float degreesFromMount) => float2(sin(radians(degreesFromMount)), cos(radians(degreesFromMount)));

    private static float2 Clamped(Engagement e, float2 look)
    {
        Aim(e, look);
        return FireControl.AimDirection(e.WeaponItem, e.Shooter);
    }

    private static void Near(float2 expected, float2 actual, int digits = 3)
    {
        Assert.Equal(expected.x, actual.x, digits);
        Assert.Equal(expected.y, actual.y, digits);
    }

    // AimDirection at the arc's edges: the mount is (0,1) and the arc 170 degrees, so the edge is 85. On the edge
    // and inside are untouched, just outside turns to the edge on its own side, straight behind turns to the
    // positive edge, a zero aim (and one too short to normalise) is the mount, and a NaN aim never poisons the
    // result. Kills: a clamp that turns the wrong way; an edge off by a degree; a missing zero-aim fallback.
    [Fact]
    public void TheAimClampHoldsAtTheEdgesOfTheArc()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 4f, weaponRange: 60f);
        Near(Dir(85), Clamped(e, Dir(85)));
        Near(Dir(85), Clamped(e, Dir(85.5f)));
        Near(Dir(-85), Clamped(e, Dir(-86)));
        Near(Dir(84), Clamped(e, Dir(84)));
        Near(Dir(0), Clamped(e, Dir(0)));
        Near(Dir(85), Clamped(e, Dir(180 - 1e-3f)), 2);
        Near(float2(0, 1), Clamped(e, float2(0, 0)));
        Near(float2(0, 1), Clamped(e, float2(5e-4f, 0)));
        Near(Dir(85), Clamped(e, float2(2e-3f, 0)));
        e.Shooter.Aim = float3(0, 1, 0);
        Near(float2(0, 1), FireControl.AimDirection(e.WeaponItem, e.Shooter));
        e.Shooter.Aim = float3(float.NaN, 0, float.NaN);
        var nan = FireControl.AimDirection(e.WeaponItem, e.Shooter);
        Assert.False(float.IsNaN(nan.x) || float.IsNaN(nan.y), "a NaN aim must not produce a NaN flight direction");
    }

    [Fact]
    public void AFullCircleMountPassesEveryAim()
    {
        var settings = TestSettings();
        settings.FiringArc = 360;
        var e = Build(settings, SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 4f, weaponRange: 60f);
        Near(float2(0, -1), Clamped(e, float2(0, -3)));
        Near(normalize(float2(-1, -1)), Clamped(e, float2(-5, -5)));
    }

    [Fact]
    public void AZeroWidthArcIsTheMount()
    {
        var settings = TestSettings();
        settings.FiringArc = 0;
        var e = Build(settings, SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 4f, weaponRange: 60f);
        foreach (var d in new[] { 0f, 1e-3f, 30f, 90f, 179f, -120f })
            Near(float2(0, 1), Clamped(e, Dir(d)), 4);
    }

    // The hardpoint's authored arc overrides the default (90 here, so the edge is 45), and the arc turns with the
    // mount: a shooter facing (1,1) has its edge 45 degrees counter-clockwise of that when aimed along -x.
    [Fact]
    public void TheHardpointsOwnArcAndTheRotatedMountSetTheEdge()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 4f, weaponRange: 60f, hardpointArc: 90f);
        Near(Dir(45), Clamped(e, float2(1, 0)));
        Near(Dir(-45), Clamped(e, Dir(-100)));
        e.Shooter.Direction = normalize(float2(1, 1));
        var mount = normalize(float2(1, 1));
        var edgeCcw = float2(mount.x * cos(radians(45)) - mount.y * sin(radians(45)), mount.x * sin(radians(45)) + mount.y * cos(radians(45)));
        Near(edgeCcw, Clamped(e, float2(-1, 0)));
    }

    // A contact round with no lock commits at Fire, against the poses of that tick: a hull that sails onto the
    // aim line afterwards is not in its way, and one that leaves it afterwards does not un-stop it. Kills:
    // committing later than Fire (the blocker that arrives would stop the round); re-deciding the burst point
    // from live poses.
    [Fact]
    public void AContactRoundIgnoresAHullThatCrossesItsLineAfterFire()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f, weaponRange: 60f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var blocker = AddShip(e, PointAlong(e, float2(0, 1), 25f) + float3(300, 0, 0), 1f, 300);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        Assert.Equal(ShotResult.Burst, committed.Result);
        Assert.Equal(e.Shooter.Position.z + 60f, committed.BurstPoint.y, 2);
        blocker.Position = PointAlong(e, float2(0, 1), 25f);
        e.Zone.Update(4f);

        SafeAssert.NoShots(e.Zone);
        Assert.True(Alive(e, blocker), "the round flew through the hull that arrived after Fire and burst at max range");
    }

    [Fact]
    public void AContactRoundStillBurstsAtAHullThatLeavesItsLineAfterFire()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f, weaponRange: 60f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var blocker = AddShip(e, PointAlong(e, float2(0, 1), 25f), 1f, 300);
        var atMaxRange = AddShip(e, PointAlong(e, float2(0, 1), 60f), 1f, 302);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);
        Assert.Equal(e.Shooter.Position.z + 21f, committed.BurstPoint.y, 2);
        blocker.Position += float3(300, 0, 0);
        e.Zone.Update(4f);

        Assert.True(Alive(e, blocker), "the hull moved away; the round still burst at its old face");
        Assert.True(Alive(e, atMaxRange), "and did not continue to max range");
    }

    // The contact point is measured from where the round left (FireOrigin), not from where its shooter is when
    // the commit tick runs. Unblocked, a max-range burst point is frozen at Fire either way; with a blocker 21
    // units out the origin matters. Kills: Commit reading the shooter's live position (Soul's M6).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AShooterMovingAfterFireDoesNotMoveTheContactPoint(bool withBlocker)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f, weaponRange: 60f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var origin = e.Shooter.Position;
        if (withBlocker) AddShip(e, PointAlong(e, float2(0, 1), 25f), 1f, 300);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Shooter.Position += float3(25f, 0, 10f);
        e.Zone.Update(.01f);

        Assert.Equal(origin.x, committed.BurstPoint.x, 2);
        Assert.Equal(origin.z + (withBlocker ? 21f : 60f), committed.BurstPoint.y, 2);
    }

    // A hull wholly behind the muzzle is not on the round's way. Kills: admitting cells whose exit lies behind
    // the origin (the round would burst at the hull behind it, at distance zero).
    [Fact]
    public void AContactRoundIgnoresAHullBehindTheMuzzle()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: WeaponFuse.Contact, blastRadius: 4f, damage: 100f, weaponRange: 60f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        AddShip(e, PointAlong(e, float2(0, -1), 25f), 1f, 300);
        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.Equal(e.Shooter.Position.z + 60f, committed.BurstPoint.y, 2);
    }

    // The intent ruling: a round fired at a valid target that has left the zone does not burst, at either stage
    // (before or after its commit), whatever its fuse -- it resolves as a Miss, and nothing standing beside where
    // the target was, nor the shooter, takes blast damage. Kills: a round that detonates at its frozen burst point
    // after its target is gone.
    [Theory]
    [InlineData(WeaponFuse.Proximity, false)]
    [InlineData(WeaponFuse.Contact, false)]
    [InlineData(WeaponFuse.Delayed, false)]
    [InlineData(WeaponFuse.Proximity, true)]
    [InlineData(WeaponFuse.Contact, true)]
    [InlineData(WeaponFuse.Delayed, true)]
    public void ARoundWhoseTargetLeavesTheZoneDoesNotBurst(WeaponFuse fuse, bool committedFirst)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: fuse, blastRadius: 30f, penetration: 1f, damage: 500f);
        var bystanders = new Entity[]
        {
            AddShip(e, e.Target.Position + float3(8, 0, 0), 1f, 300),
            AddShip(e, e.Target.Position + float3(-8, 0, 12), 1f, 302)
        };
        var damage = 0f;
        var subscriptions = new List<IDisposable>();
        foreach (var o in bystanders.Append(e.Shooter))
        {
            subscriptions.Add(o.ArmorDamage.Subscribe(x => damage += x.damage));
            subscriptions.Add(o.HullDamage.Subscribe(x => damage += x));
            subscriptions.Add(o.ItemDamage.Subscribe(x => damage += x.damage));
        }
        var resolved = new List<ShotOutcome>();
        subscriptions.Add(e.Zone.ShotResolved.Subscribe(o => resolved.Add(o)));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(committedFirst ? 4.6f : .01f);
        Assert.Equal(committedFirst, e.Zone.PendingShots[0].Committed);
        e.Zone.Entities.Remove(e.Target);
        e.Zone.Update(6f);

        foreach (var s in subscriptions) s.Dispose();
        SafeAssert.NoShots(e.Zone);
        Assert.True(bystanders.All(b => Alive(e, b)), "no bystander may take blast damage");
        Assert.Equal(0f, damage);
        Assert.Equal(ShotResult.Miss, SafeAssert.Only(resolved).Result);
    }

    // A no-lock round outlives a shooter destroyed outright in flight, for every fuse: Death removes the shooter
    // and the round still bursts at max range and resolves once as a Burst.
    [Theory]
    [InlineData(WeaponFuse.Proximity)]
    [InlineData(WeaponFuse.Contact)]
    [InlineData(WeaponFuse.Delayed)]
    public void ANoLockRoundStillBurstsAfterItsShooterIsDestroyed(WeaponFuse fuse)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 20f, fuse: fuse, blastRadius: 4f, damage: 100f, weaponRange: 60f);
        e.Shooter.SetTarget(TargetRef.None);
        Aim(e, float2(0, 1));
        var witness = AddShip(e, PointAlong(e, float2(0, 1), 60f), 1f, 300);
        var resolved = new List<ShotOutcome>();
        using var r = e.Zone.ShotResolved.Subscribe(o => resolved.Add(o));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Shooter.DamageHull(1e9f);
        e.Zone.Update(4f);

        Assert.False(Alive(e, e.Shooter));
        Assert.False(Alive(e, witness), "the round must still burst at max range");
        Assert.Equal(ShotResult.Burst, SafeAssert.Only(resolved).Result);
    }

    // ToWorldPoint and ToSchematicPoint are inverses, and ToWorldPoint puts one cell to starboard of the centre
    // of mass two world units along the starboard axis (right = (forward.y, -forward.x), spelled out here). At
    // facing (0,1) a flipped right axis is still masked by a centre-lane point; (2,1) is not.
    [Fact]
    public void ToWorldPointIsTheInverseOfToSchematicPointAtANonAxisFacing()
    {
        var facing = normalize(float2(2, 1));
        var e = Build(TestSettings(), SolidShape(5, 4), targetFacing: facing);
        var t = e.Target;
        var com = e.HullData.Shape.CenterOfMass;
        var starboard = float2(facing.y, -facing.x);

        var world = t.Position.xz + float2(7f, -3f);
        var back = t.ToWorldPoint(t.ToSchematicPoint(world));
        Assert.Equal(world.x, back.x, 3);
        Assert.Equal(world.y, back.y, 3);

        var oneToStarboard = t.ToWorldPoint(com + float2(1, 0));
        Assert.Equal(t.Position.x + 2f * starboard.x, oneToStarboard.x, 3);
        Assert.Equal(t.Position.z + 2f * starboard.y, oneToStarboard.y, 3);
        var oneToBow = t.ToWorldPoint(com + float2(0, 1));
        Assert.Equal(t.Position.x + 2f * facing.x, oneToBow.x, 3);
        Assert.Equal(t.Position.z + 2f * facing.y, oneToBow.y, 3);
    }

    // A contact blast on a hull at facing (2,1): the shot enters over the starboard side, off the hull's own
    // centre line, so the blast point's lateral offset from the centre of mass is not zero and a flipped right
    // axis in ToWorldPoint would mirror it onto the far side. Placement is asserted per cell: the impact cell
    // takes a share, and no cell farther than one from it takes anything (radius .15 cells). Kills: a flip of
    // ToWorldPoint's right axis, which the host's round trip would otherwise hide from every total.
    [Fact]
    public void AContactBlastLandsOnTheCellItHitAtANonAxisFacing()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), targetFacing: float2(2, 1), fuse: WeaponFuse.Contact, blastRadius: .3f, damage: 100f);
        ArmourEverything(e);
        var deposits = new Dictionary<int2, float>();
        using var a = e.Target.ArmorDamage.Subscribe(x => deposits[x.pos] = (deposits.TryGetValue(x.pos, out var d) ? d : 0f) + x.damage);
        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
        {
            using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);

        Assert.True(deposits.ContainsKey(outcome.Cell), $"the impact cell {outcome.Cell} took nothing; damaged: {string.Join(",", deposits.Keys)}");
        foreach (var cell in deposits.Keys)
            Assert.True(Math.Abs(cell.x - outcome.Cell.x) <= 1 && Math.Abs(cell.y - outcome.Cell.y) <= 1,
                $"cell {cell} took damage far from the impact cell {outcome.Cell}");
    }

    // TurningAfterCommitDoesNotMoveTheBlastOnItsHost: a contact-fuse shot. After commit and before arrival, the
    // host turns and moves. Pass: the host's per-cell damage equals that of the same shot with no turn. Kills:
    // taking P from the live bearing, or from Outcome.Cell's centre in place of the lane point.
    [Fact]
    public void TurningAfterCommitDoesNotMoveTheBlastOnItsHost()
    {
        Dictionary<int2, float> ArmorDamageByCell(Action<Engagement> afterCommit)
        {
            var e = Build(TestSettings(commitHorizon: .5f), SolidShape(5, 4), velocity: 50, targetRange: 100,
                fuse: WeaponFuse.Contact, blastRadius: 3f, damage: 100f);
            foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }

            // Per cell, not a total: a blast that lands on the wrong cells deals the same total.
            var perCell = new Dictionary<int2, float>();
            using var a = e.Target.ArmorDamage.Subscribe(x => perCell[x.pos] = (perCell.TryGetValue(x.pos, out var d) ? d : 0f) + x.damage);

            var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            // Step to just past commit (flight time 2s, commit horizon 0.5s -> commits at t ~= 1.5s).
            while (!e.Zone.PendingShots.Any(s => s.ShotId == shotId && s.Committed))
                e.Zone.Update(.05f);

            afterCommit?.Invoke(e);

            while (e.Zone.PendingShots.Any(s => s.ShotId == shotId))
                e.Zone.Update(.05f);

            return perCell;
        }

        var noTurn = ArmorDamageByCell(null);
        var turned = ArmorDamageByCell(e =>
        {
            e.Target.Direction = normalize(float2(1, 0));
            e.Target.Position += float3(50, 0, -30);
        });

        Assert.NotEmpty(noTurn);
        Assert.Equal(noTurn.Keys.OrderBy(k => k.x).ThenBy(k => k.y), turned.Keys.OrderBy(k => k.x).ThenBy(k => k.y));
        foreach (var cell in noTurn.Keys) Assert.Equal(noTurn[cell], turned[cell], 2);
    }

    // LabelsDoNotDecideBehaviour: a weapon labelled Airburst with no fuse resolves as a direct hit, and a
    // weapon with Fuse = Proximity and no label detonates.
    [Fact]
    public void LabelsDoNotDecideBehaviour()
    {
        {
            var e = Build(TestSettings(), SolidShape(5, 4), fuse: null, blastRadius: 0f, damage: 50f,
                modifiers: WeaponModifiers.Airburst);
            var before = e.Target.Hull.Durability;
            var armorEvents = 0;
            using var a = e.Target.ArmorDamage.Subscribe(_ => armorEvents++);
            ShotOutcome outcome = null;
            for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
            {
                using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
                e.Zone.Update(.01f);
            }
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            Assert.True(armorEvents > 0); // a direct hit -- one lane's cells, never an area
        }
        {
            var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 20f, damage: 500f,
                modifiers: WeaponModifiers.None);
            var before = e.Target.Hull.Durability;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
            Assert.True(e.Target.Hull.Durability < before);
        }
    }

    // AFuseWithoutARadiusIsADirectHit: Fuse = Contact with a null or zero radius does lane damage.
    [Fact]
    public void AFuseWithoutARadiusIsADirectHit()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Contact, blastRadius: 0f, damage: 50f);
        var armorEvents = 0;
        using var a = e.Target.ArmorDamage.Subscribe(_ => armorEvents++);
        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
        {
            using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);
        Assert.Equal(ShotResult.Hit, outcome.Result);
        Assert.True(armorEvents > 0);
        var shot = e.Zone.PendingShots.Count == 0 ? null : (PendingShot?) e.Zone.PendingShots[0];
        Assert.True(!shot.HasValue, "the shot must be resolved and removed"); // resolved and removed -- confirms it went through Apply's null-fuse path to completion
    }

    // ARadiusWithoutAFuseIsADirectHit (Soul F1): the symmetric inert half -- BlastRadius > 0 but the catalog
    // record carries no Fuse. Fire freezes Fuse as null regardless of the radius, so this resolves as a direct
    // hit exactly like AFuseWithoutARadiusIsADirectHit above. Kills: Fire freezing "detonates" from BlastRadius
    // alone, without also requiring a Fuse.
    [Fact]
    public void ARadiusWithoutAFuseIsADirectHit()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), fuse: null, blastRadius: 40f, damage: 50f);
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        Assert.Null(shot.Fuse);
        Assert.Equal(40f, shot.BlastRadius);

        var armorEvents = 0;
        using var a = e.Target.ArmorDamage.Subscribe(_ => armorEvents++);
        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
        {
            using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);
        Assert.True(armorEvents > 0); // a direct hit -- not spread over a disc
    }

    // AContactBlastReportsTheHit: IncomingHit fires once, for the host, on a committed contact hit, and never
    // for a proximity burst.
    [Fact]
    public void AContactBlastReportsTheHit()
    {
        {
            var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Contact, blastRadius: 3f, damage: 50f);
            var hits = 0;
            using var h = e.Target.IncomingHit.Subscribe(_ => hits++);
            ShotOutcome outcome = null;
            for (var attempt = 0; attempt < 60 && (outcome == null || !outcome.Hit); attempt++)
            {
                using var s = e.Zone.ShotResolved.Subscribe(o => outcome = o);
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
                e.Zone.Update(.01f);
            }
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            Assert.Equal(1, hits);
        }
        {
            var e = Build(TestSettings(), SolidShape(5, 4), fuse: WeaponFuse.Proximity, blastRadius: 20f, damage: 50f);
            var hits = 0;
            using var h = e.Target.IncomingHit.Subscribe(_ => hits++);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
            Assert.Equal(0, hits);
        }
    }

    // Fire freezes a proximity shot's burst point at the predicted intercept of a moving target -- where a shot
    // of the weapon's own velocity, fired now, meets the target -- not at the target's position now. The
    // expectation is solved here from first principles: |d + v t| = s t for the flight time t, planar, the
    // shooter's own motion never fed in. Target 100 ahead, drifting (30,0) against a 50 unit/s shot: t = 2.5 s,
    // so the burst sits 75 units to the side of where the target is at Fire.
    // Kills: BurstPosition frozen at the target's current position (PredictedIntercept never consulted).
    [Fact]
    public void ProximityBurstPositionIsThePredictedInterceptForAMovingTarget()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 50, targetRange: 100,
            fuse: WeaponFuse.Proximity, blastRadius: 3f, damage: 50f);
        var v = float2(30f, 0f);
        e.Target.Velocity = v;

        var s = e.Shooter.Position.xz;
        var d = e.Target.Position.xz - s;
        var a = dot(v, v) - 50f * 50f;
        var b = 2f * dot(d, v);
        var c = dot(d, d);
        var t = (-b - sqrt(b * b - 4f * a * c)) / (2f * a); // a < 0: the positive root
        Assert.Equal(2.5f, t, 3);
        var expected = e.Target.Position.xz + v * t;

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = e.Zone.PendingShots.Single(x => x.ShotId == shotId);

        Assert.Equal(expected.x, shot.BurstPosition.x, 2);
        Assert.Equal(expected.y, shot.BurstPosition.z, 2);
        Assert.True(length(shot.BurstPosition.xz - e.Target.Position.xz) > 70f, "the burst must lead the target, not sit on it");
    }

    // Detonate has no radius guard: a nonpositive radius covers no cell (a disc with no area overlaps nothing),
    // so it delivers nothing and touches no shield.
    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void DetonateCannotBeReachedWithANonPositiveRadius(float radius)
    {
        var e = Build(TestSettings(), SolidShape(5, 4));
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 100f; e.Target.MaxArmor[c.x, c.y] = 100f; }
        var before = e.Target.Hull.Durability;

        FireControl.Detonate(e.Zone, e.Target.Position.xz, radius, 1000f, DamageType.Kinetic);

        Assert.Equal(before, e.Target.Hull.Durability);
        Assert.Equal(100f, e.Target.Armor[2, 1], 2);
    }

    // ==== Commit's probes: the shipped catalog is neutral, and the field survives a full write/reopen. ====

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }

    private static CultCache OpenReadOnlyRealCatalog(string catalogPath, out SingleFileMessagePackBackingStore store)
    {
        var registry = TestCatalog.Registry();
        var cache = new CultCache(registry);
        store = new SingleFileMessagePackBackingStore(catalogPath, true);
        cache.AddBackingStore(store, AetheriaStores.CatalogTypes);
        return cache;
    }

    // Every WeaponItemData in the shipped catalog has a null BlastRadius and a null Fuse, and the weapon
    // schema's own migration report is Exact. (Until the scenarios product-gap pass rewrote the catalog on
    // 2026-09-30 it was CompatibleDrift with only slot 32 defaulted: the file predated BlastRadius and Fuse.)
    [Fact]
    public void ShippedCatalogIsNeutralOnBlastRadiusAndFuse()
    {
        var gameData = TestCatalog.Repo;
        var cache = OpenReadOnlyRealCatalog(gameData, out var store);
        try
        {
            var weapons = cache.GetAll<WeaponItemData>().ToList();
            Assert.NotEmpty(weapons);
            // A mine layer is the one shipped weapon that carries a blast: its radius is the mine's (MineLauncherCatalogTests).
            foreach (var w in weapons.Where(w => !w.Behaviors.OfType<MineLayerData>().Any()))
            {
                Assert.Null(w.BlastRadius);
                Assert.Null(w.Fuse);
            }

            var reports = store.LastSchemaMigrationReports.Where(r => r.LocalSchemaName == "aetheria.weaponitemdata").ToList();
            Assert.NotEmpty(reports);
            foreach (var report in reports)
            {
                Assert.Equal(CultSchemaMigrationKind.Exact, report.Kind);
                Assert.Empty(report.DefaultedMissingSlots);
                Assert.Empty(report.IgnoredExtraSlots);
            }
        }
        finally
        {
            cache.Dispose();
        }
    }

    // A WeaponItemData round-trip for each WeaponFuse value and null, crossed with radius null, 0 and
    // positive, through a written and reopened temporary copy of the shipped catalog.
    [Fact]
    public void WeaponFuseAndBlastRadiusRoundTripThroughAWrittenCatalog()
    {
        var src = TestCatalog.Repo;
        var fuseValues = new WeaponFuse?[] { null, WeaponFuse.Contact, WeaponFuse.Proximity, WeaponFuse.Delayed };
        var radiusValues = new float?[] { null, 0f, 12.5f };

        foreach (var fuse in fuseValues)
        foreach (var radius in radiusValues)
        {
            var tmp = Path.Combine(_root, "roundtrip-" + Guid.NewGuid().ToString("N") + ".cc");
            File.Copy(src, tmp);
            try
            {
                {
                    var cache = OpenReadOnlyRealCatalog(tmp, out _);
                    cache.Dispose();
                }
                var writeCache = new CultCache(TestCatalog.Registry());
                writeCache.AddBackingStore(new SingleFileMessagePackBackingStore(tmp, false), AetheriaStores.CatalogTypes);
                var w = writeCache.GetAll<WeaponItemData>().OrderBy(x => x.Name).First();
                w.Fuse = fuse;
                w.BlastRadius = radius;
                writeCache.Upsert(w);
                writeCache.FlushAsync().Wait();
                writeCache.Dispose();

                var readCache = OpenReadOnlyRealCatalog(tmp, out _);
                var reopened = readCache.GetAll<WeaponItemData>().OrderBy(x => x.Name).First();
                Assert.Equal(fuse, reopened.Fuse);
                Assert.Equal(radius, reopened.BlastRadius);
                readCache.Dispose();
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }
}
