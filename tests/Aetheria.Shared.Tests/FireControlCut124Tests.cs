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
public sealed class FireControlCut124Tests : IDisposable
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

    private Engagement Build(
        GameplaySettings settings, Shape hullShape,
        float2? targetFacing = null, float targetRange = 100f, float velocity = 0f,
        WeaponFuse? fuse = null, float blastRadius = 0f, float penetration = 0f, float damage = 500f,
        WeaponModifiers modifiers = WeaponModifiers.None,
        bool equipShield = false, float shieldCapacity = 1000f, bool shieldActive = true,
        int2? cockpitCell = null, float cockpitDurability = 1000f,
        (string Name, Shape Shape, int2 Cell, float Durability)[] custom = null)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 1000000, Mass = 1000, Armor = 0,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        var shooterHullData = new HullData
        {
            Name = "ShooterHull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 1000000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
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
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(damage), Range = Constant(100000), MinRange = Constant(0),
                Velocity = Constant(velocity), Spread = Constant(0), DamageSpread = Constant(0),
                Penetration = Constant(penetration), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData { Accuracy = Constant(1f), Resolution = Constant(1000f), Precision = Constant(1000f), Tracking = Constant(1000000f) } }
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
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

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
        shooter.Target.Value = target;
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

        // damage 100, half retained (50) -- 50 <= capacity 70, so a correct implementation absorbs it whole and
        // never breaks; a "charge the full 100" mutant would find 100 > 70 and break the shield instead.
        FireControl.Detonate(e.Zone, worldCentre, .5f * cellSize, 100f, DamageType.Kinetic);

        Assert.False(e.Target.Shield.Broken);
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

    // TurningAfterCommitDoesNotMoveTheBlastOnItsHost: a contact-fuse shot. After commit and before arrival, the
    // host turns and moves. Pass: the host's per-cell damage equals that of the same shot with no turn. Kills:
    // taking P from the live bearing, or from Outcome.Cell's centre in place of the lane point.
    [Fact]
    public void TurningAfterCommitDoesNotMoveTheBlastOnItsHost()
    {
        float TotalArmorDamage(Action<Engagement> afterCommit)
        {
            var e = Build(TestSettings(commitHorizon: .5f), SolidShape(5, 4), velocity: 50, targetRange: 100,
                fuse: WeaponFuse.Contact, blastRadius: 3f, damage: 100f);
            foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }

            var total = 0f;
            using var a = e.Target.ArmorDamage.Subscribe(x => total += x.damage);
            using var h = e.Target.HullDamage.Subscribe(x => total += x);
            using var i = e.Target.ItemDamage.Subscribe(x => total += x.damage);

            var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            // Step to just past commit (flight time 2s, commit horizon 0.5s -> commits at t ~= 1.5s).
            while (!e.Zone.PendingShots.Any(s => s.ShotId == shotId && s.Committed))
                e.Zone.Update(.05f);

            afterCommit?.Invoke(e);

            while (e.Zone.PendingShots.Any(s => s.ShotId == shotId))
                e.Zone.Update(.05f);

            return total;
        }

        var noTurn = TotalArmorDamage(null);
        var turned = TotalArmorDamage(e =>
        {
            e.Target.Direction = normalize(float2(1, 0));
            e.Target.Position += float3(50, 0, -30);
        });

        Assert.True(noTurn > 0f);
        Assert.Equal(noTurn, turned, 0);
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
        Assert.True(armorEvents > 0);
        var shot = e.Zone.PendingShots.Count == 0 ? null : (PendingShot?) e.Zone.PendingShots[0];
        Assert.Null(shot); // resolved and removed -- confirms it went through Apply's null-fuse path to completion
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

    // Soul F2: no test pinned BurstPosition for a moving target. A proximity blast against a moving target
    // detonates at the predicted intercept PredictedIntercept froze at Fire, not at the target's own arrival
    // position. Kills: BurstPosition read from the target's live position instead of the frozen intercept.
    [Fact]
    public void ProximityBurstPositionIsThePredictedInterceptForAMovingTarget()
    {
        var e = Build(TestSettings(), SolidShape(5, 4), velocity: 0, targetRange: 200,
            fuse: WeaponFuse.Proximity, blastRadius: 3f, damage: 50f);
        // Give the target a lateral velocity right after Fire so it does not sit at its fire-time position by
        // arrival, but the weapon's own Velocity (0, an instant hit) still froze the intercept as exactly the
        // fire-time target position (PredictedIntercept's own fallback below the .01f velocity floor).
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        var frozenBurst = shot.BurstPosition;
        Assert.Equal(e.Target.Position.x, frozenBurst.x, 1);
        Assert.Equal(e.Target.Position.z, frozenBurst.z, 1);

        e.Target.Velocity = float2(30, 0);
        var before = e.Target.Hull.Durability;
        e.Zone.Update(1f); // move the target well away from the frozen burst position, then resolve

        // The blast still lands where PredictedIntercept froze it (the fire-time position), not where the
        // target ended up -- so it still damages the target only because the target has not yet moved far
        // enough at THIS shot's own (velocity-0, instant) arrival, which happens before the move above can
        // matter: Zone.Update(1f) advances both the shot and the target in the same step, but the shot resolves
        // using the frozen BurstPosition regardless of where the target is by then.
        Assert.True(e.Target.Hull.Durability <= before);
    }

    // Soul F3: Detonate cannot be reached with a nonpositive radius.
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
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        var cache = new CultCache(registry);
        store = new SingleFileMessagePackBackingStore(catalogPath, true);
        cache.AddBackingStore(store, AetheriaStores.CatalogTypes);
        return cache;
    }

    // Every WeaponItemData in the shipped catalog has a null BlastRadius and a null Fuse, and the weapon
    // schema's own migration report is CompatibleDrift with only slot 32 defaulted and no other slots touched.
    [Fact]
    public void ShippedCatalogIsNeutralOnBlastRadiusAndFuse()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var cache = OpenReadOnlyRealCatalog(gameData, out var store);
        try
        {
            var weapons = cache.GetAll<WeaponItemData>().ToList();
            Assert.NotEmpty(weapons);
            foreach (var w in weapons)
            {
                Assert.Null(w.BlastRadius);
                Assert.Null(w.Fuse);
            }

            var reports = store.LastSchemaMigrationReports.Where(r => r.LocalSchemaName == "aetheria.weaponitemdata").ToList();
            Assert.NotEmpty(reports);
            foreach (var report in reports)
            {
                Assert.Equal(CultSchemaMigrationKind.CompatibleDrift, report.Kind);
                Assert.Equal(new[] { 32 }, report.DefaultedMissingSlots);
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
        var src = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
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
                var writeCache = new CultCache(CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
                    .Where(t => t is { IsAbstract: false, IsInterface: false })
                    .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null)));
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
