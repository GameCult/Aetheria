/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

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

// Cut 12.3 (docs/fire-control-cut.md, "How a direct hit travels"): a resolved hit's damage now travels down
// its own lane -- armour, then the occupying item, then whatever is left to the hull, in order -- replacing
// Entity.DamageSchematic's even split and Entity.ApplyHit's 0.5-step march (both deleted by this cut). Builds
// its own fixtures, the convention every cut's test file follows. Every armour value this file cares about is
// set directly on Entity.Armor after Build, overriding the fixture's own flat HullData.Armor -- the only way
// to get a non-uniform plate without a second hull-authoring path.
public sealed class FireControlCut123Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut123-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut123Tests() => Directory.CreateDirectory(_root);

    private readonly List<CultCache> _openCaches = new List<CultCache>();

    public void Dispose()
    {
        foreach (var c in _openCaches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings() => new GameplaySettings
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
        UnaidedPrecision = .3f,
        AgentMinHitProbability = 0f,
        BeamResolveInterval = .1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static Shape SolidShape(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
        public HullData HullData;
        public EquippedItem[] Markers;
        public EquippedItem Cockpit;
    }

    // A shooter off the origin (Cut 11.3), a target over a caller-supplied hull shape, straight ahead so the
    // default facing (0,1) makes the shot travel exactly (0,1) in world XZ (velocity 0 -- PredictedIntercept
    // returns target.Position exactly, and the shooter/target share the same world X) -- the same clean
    // geometry FireControlCut12Tests' own SigmaFloor fixture relies on. A full-strength targeting system
    // (Accuracy 1, Resolution huge) so only the fixture's own geometry decides the numbers.
    private static Shape LShape() { var s = new Shape(2, 2); s[new int2(0, 0)] = true; s[new int2(1, 0)] = true; s[new int2(0, 1)] = true; return s; }

    private Engagement Build(
        GameplaySettings settings, Shape hullShape, float precision,
        float penetration = 0, float damageSpread = 0, float2? targetFacing = null,
        (int2 Cell, float Durability)[] markers = null, int2? cockpitCell = null, float cockpitDurability = 40,
        (int2 Cell, float Durability)[] bars = null, (int2 Cell, float Durability)[] lBars = null)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 100000, Mass = 1000, Armor = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        var shooterHullData = new HullData
        {
            Name = "ShooterHull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 100000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(shooterHullData);
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(5), Range = Constant(1000), MinRange = Constant(0),
                Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(damageSpread),
                Penetration = Constant(penetration), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData
            {
                Accuracy = Constant(1f), Resolution = Constant(1000f), Precision = Constant(precision), Tracking = Constant(100000f)
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Marker", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.Upsert(new GearData
        {
            Name = "Cockpit", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new CockpitData() }
        });
        // A multi-cell item (2x1), for the proportional-absorption fix batch's own fixtures -- the only shape
        // that can ever be shared between two lanes (cells are disjoint between lanes; only an item spanning
        // several cells can straddle two of them).
        cache.Upsert(new GearData
        {
            Name = "Bar", Hardpoint = HardpointType.Tool, Shape = SolidShape(2, 1), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        // A non-convex multi-cell item (an L: three cells of a 2x2 box), for F8's model sweep -- a straight
        // lane can cross an L's two arms non-contiguously, which a plain 2x1 bar can never do.
        cache.Upsert(new GearData
        {
            Name = "LBar", Hardpoint = HardpointType.Tool, Shape = LShape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 500; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 }; // F8: shared-item fixtures equip more items (bars) than earlier cuts needed
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Cut123", Owner = null }, null);

        EquippableItem Make(string name, int lot, float durability) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>(name)), Durability = durability, Lot = lot
        };

        var lot = 1;
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("ShooterHull"));
        var shooter = new Ship(items, zone, new EquippableItem { Data = shooterHullRef, Durability = 1000000, Lot = lot++ }, new EntitySettings());
        var gun = Make("Gun", lot++, 1);
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(x => x.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);
        var targeting = Make("Targeting", lot++, 1);
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var target = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = lot++ }, new EntitySettings());

        var targetGun = Make("Gun", lot++, 1);
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        var markerItems = markers?.Select(m =>
        {
            var mi = Make("Marker", lot++, m.Durability);
            Assert.True(target.TryEquip(mi, m.Cell), $"marker must fit at {m.Cell}");
            return target.Equipment.Single(x => x.EquippableItem == mi);
        }).ToArray();

        if (bars != null)
            foreach (var bar in bars)
            {
                var bi = Make("Bar", lot++, bar.Durability);
                Assert.True(target.TryEquip(bi, bar.Cell), $"bar must fit at {bar.Cell}");
            }
        if (lBars != null)
            foreach (var bar in lBars)
            {
                var bi = Make("LBar", lot++, bar.Durability);
                Assert.True(target.TryEquip(bi, bar.Cell), $"L-bar must fit at {bar.Cell}");
            }

        EquippedItem cockpit = null;
        if (cockpitCell != null)
        {
            var ci = Make("Cockpit", lot++, cockpitDurability);
            Assert.True(target.TryEquip(ci, cockpitCell.Value), $"cockpit must fit at {cockpitCell.Value}");
            cockpit = target.Equipment.Single(x => x.EquippableItem == ci);
        }

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        // Cut 11.3: the shooter sits off the origin.
        shooter.Position = float3(37, 0, -11);
        target.Position = float3(37, 0, 89); // 100 units straight ahead of the shooter -- travel direction is exactly (0,1)
        if (targetFacing != null) target.Direction = targetFacing.Value;
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        zone.Update(1f); // fire times nonzero (Cut 11.3)

        return new Engagement
        {
            Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon,
            HullData = hullData, Markers = markerItems, Cockpit = cockpit
        };
    }

    // Fires until a hit lands, retrying with a fresh shot each time (a miss already resolved and left the
    // pending queue empty) -- the roll can still miss outright, and none of these tests care which attempt
    // actually connects.
    private static ShotOutcome FireUntilHit(Engagement e, float? damageOverride = null, int attempts = 60)
    {
        ShotOutcome outcome = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);
        for (var i = 0; i < attempts && (outcome == null || !outcome.Hit); i++)
        {
            outcome = null;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter, damageOverride);
            e.Zone.Update(.01f); // velocity 0 -> zero flight time, commits and resolves in this tick
        }
        return outcome;
    }

    // DamageIsAbsorbedInOrderAlongTheRay replaces FireAuthorityTests.HardpointHitDamagesItemThenHull and
    // PenetrationMarchIsPlanar (both called the deleted Entity.ApplyHit directly). A 3x3 solid hull's only
    // interior cell (1,1) -- Shape.Shrink needs all 8 neighbours present -- carries a Tool item, so the marker
    // must sit on the centre column; every cell of that column shares the SAME lateral shadow (ell has only an
    // x component at this bearing, and every column-1 cell has x=1), so once a shot's committed Lateral falls
    // in that column's own interval, its lane is the full 3-cell column in near-to-far order -- (1,0) is always
    // the impact cell there, regardless of precision. Retries until a hit lands on that column (the other two
    // columns' own shadows are adjacent, not overlapping, so this is just "which column the draw picked", not a
    // low-probability corner). Damage 30, penetration 3: a 10-armour front cell, then the marker, then a
    // 50-armour soft cell the remainder never reaches.
    // Kills: an even split (would put damage/3 = 10 on every cell, exactly canceling the front armour by
    // coincidence but consuming only 10 of the marker's durability and 10 off the soft cell's -- distinguishing
    // both); items absorbing before armour (irrelevant here since the front cell carries no item, but the
    // marker cell's own Absorb call still checks armour(0) before the item); no carry-forward (would re-deal
    // the full 30 fresh to the marker instead of the 20 that survived the front cell, and would put 30 on the
    // soft cell instead of 0).
    [Fact]
    public void DamageIsAbsorbedInOrderAlongTheRay()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, penetration: 3f,
            markers: new[] { (new int2(1, 1), 1000000f) });
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 0f; e.Target.MaxArmor[c.x, c.y] = 0f; }
        e.Target.Armor[1, 0] = 10f; e.Target.MaxArmor[1, 0] = 10f; // front: a plate
        e.Target.Armor[1, 2] = 50f; e.Target.MaxArmor[1, 2] = 50f; // soft cell: armoured but never reached

        var marker = e.Markers[0];
        var markerDurabilityBefore = marker.EquippableItem.Durability;
        var hullBefore = e.Target.Hull.Durability;

        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 200 && (outcome == null || !outcome.Hit || outcome.Cell.x != 1); attempt++)
            outcome = FireUntilHit(e, damageOverride: 30f, attempts: 1);
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit && outcome.Cell.x == 1, "fixture: needed a hit on the centre column within the attempt budget");
        Assert.Equal(new int2(1, 0), outcome.Cell); // fixture precondition: (1,0) is always this column's impact cell

        Assert.Equal(0f, e.Target.Armor[1, 0]); // 10 armour fully consumed
        Assert.Equal(markerDurabilityBefore - 20f, marker.EquippableItem.Durability, 2); // the carried-forward 20
        Assert.Equal(50f, e.Target.Armor[1, 2]); // untouched -- the remainder never reached it
        Assert.Equal(hullBefore, e.Target.Hull.Durability); // nothing left over for the hull
    }

    // ArmourFacesTheShot: only the bow half (y >= height/2) of a 1x9 hull carries a plate, penetration 0 so only
    // the impact cell can ever record a hit. For each of three facings, including one where |fx| > |fy|, the
    // shooter is repositioned so the shot's own TravelDirection is EXACTLY that facing -- the same relationship
    // FireControlCut12Tests.TurningArmourIntoTheShotTakesItOnTheArmour uses (Direction = travelDirection exposes
    // the stern; Direction = -travelDirection exposes the bow), just repeated at three different WORLD
    // directions instead of one, so the bearing this produces is exactly (0,1) or (0,-1) in schematic space
    // every time (R7: the schematic frame is planar for any world facing, not only the default one) -- with no
    // diagonal bearing in play, there is no lane-selection ambiguity to fight, only Entity.ToSchematic's own
    // rotation to check.
    [Fact]
    public void ArmourFacesTheShot()
    {
        var shape = SolidShape(1, 9);
        var e = Build(TestSettings(), shape, precision: .4f, penetration: 0f);
        for (var y = 0; y < shape.Height; y++)
        {
            e.Target.Armor[0, y] = y >= 5 ? 1f : 0f;
            e.Target.MaxArmor[0, y] = e.Target.Armor[0, y];
        }

        void CheckOneDirection(float2 travelDirection, bool bowFacesShot)
        {
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;
            e.Target.Direction = bowFacesShot ? -travelDirection : travelDirection;

            var actualTravel = FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target);
            Assert.Equal(travelDirection.x, actualTravel.x, 3); // fixture precondition
            Assert.Equal(travelDirection.y, actualTravel.y, 3);

            var hitCells = new List<int2>();
            using var s = e.Target.ArmorDamage.Subscribe(x => hitCells.Add(x.pos));
            for (var i = 0; i < 60; i++) { FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter); e.Zone.Update(.01f); }

            Assert.True(hitCells.Count > 5, $"expected several hits for travel direction {travelDirection}, bow facing {bowFacesShot}, got {hitCells.Count}");
            foreach (var c in hitCells)
                if (bowFacesShot) Assert.True(c.y >= 5, $"bow should face the shot, but {c} is on the stern half");
                else Assert.True(c.y < 5, $"stern should face the shot, but {c} is on the bow half");
        }

        void Check(float2 travelDirection)
        {
            travelDirection = normalize(travelDirection);
            CheckOneDirection(travelDirection, bowFacesShot: true);
            CheckOneDirection(travelDirection, bowFacesShot: false);
        }

        Check(float2(0, 1));
        Check(float2(.8f, .6f));
        Check(float2(.9f, .3f)); // |fx| > |fy|
    }

    // BuriedCockpitNeedsPenetration: a 3x3 solid hull's only interior cell (1,1) -- Shape.Shrink needs all 8
    // neighbours present, which only the centre of a 3x3 has -- carries a Cockpit item, ringed by the hull's
    // own 8 border cells. Firing straight down any of the 4 axis-aligned bearings crosses exactly one ring
    // cell before the cockpit (and one more after, unread since nothing needs to go further).
    [Fact]
    public void BuriedCockpitNeedsPenetration()
    {
        float RunOnce(float2 facing, float penetration, float damage, float ringArmor)
        {
            var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, penetration: penetration,
                targetFacing: facing, cockpitCell: new int2(1, 1), cockpitDurability: 1000f);
            foreach (var c in e.HullData.Shape.Coordinates)
            {
                var isRing = c.x != 1 || c.y != 1;
                e.Target.Armor[c.x, c.y] = isRing ? ringArmor : 0f;
                e.Target.MaxArmor[c.x, c.y] = e.Target.Armor[c.x, c.y];
            }
            var before = e.Cockpit.EquippableItem.Durability;
            var outcome = FireUntilHit(e, damageOverride: damage);
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit, $"fixture: facing {facing} must eventually hit");
            return before - e.Cockpit.EquippableItem.Durability;
        }

        // Penetration 0: only the ring's own facing cell is ever reached -- the cockpit, one cell further in,
        // must be untouched from all four axis-aligned bearings.
        foreach (var facing in new[] { float2(0, 1), float2(0, -1), float2(1, 0), float2(-1, 0) })
            Assert.Equal(0f, RunOnce(facing, penetration: 0f, damage: 40f, ringArmor: 5f));

        // A penetration that clears the ring (one cell, entry difference ~1): the cockpit takes exactly the
        // damage minus what the ring absorbed.
        var lost = RunOnce(float2(0, 1), penetration: 2f, damage: 40f, ringArmor: 5f);
        Assert.Equal(35f, lost, 2);

        // Enough damage, with the ring cleared, destroys the cockpit and fires Death with CockpitDestroyed.
        {
            var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, penetration: 2f,
                targetFacing: float2(0, 1), cockpitCell: new int2(1, 1), cockpitDurability: 20f);
            foreach (var c in e.HullData.Shape.Coordinates)
            {
                var isRing = c.x != 1 || c.y != 1;
                e.Target.Armor[c.x, c.y] = isRing ? 0f : 0f;
                e.Target.MaxArmor[c.x, c.y] = 0f;
            }
            CauseOfDeath? death = null;
            using var d = e.Target.Death.Subscribe(cod => death = cod);
            var outcome = FireUntilHit(e, damageOverride: 100f);
            Assert.NotNull(outcome);
            Assert.Equal(CauseOfDeath.CockpitDestroyed, death);
        }
    }

    // SpreadWidensTheImpactAcrossLanes: spread 1 (n=1, three parallel lanes one cell apart) at penetration 0 --
    // a 3-wide, 2-tall hull, stern (y=0) facing the shot (default facing == travel direction, Cut 12's own
    // TurningArmourIntoTheShotTakesItOnTheArmour convention). Retries until the committed impact lands on the
    // centre column (x=1), so the three lanes are exactly this hull's three columns, each with its own
    // facing-edge cell at y=0.
    [Fact]
    public void SpreadWidensTheImpactAcrossLanes()
    {
        var e = Build(TestSettings(), SolidShape(3, 2), precision: .6f, penetration: 0f, damageSpread: 1f);
        for (var x = 0; x < 3; x++)
        for (var y = 0; y < 2; y++)
        {
            e.Target.Armor[x, y] = 0f;
            e.Target.MaxArmor[x, y] = 0f;
        }

        ShotOutcome outcome = null;
        var hitCells = new List<(int2 pos, float damage)>();
        using (e.Target.ArmorDamage.Subscribe(x => hitCells.Add(x)))
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                hitCells.Clear();
                outcome = FireUntilHit(e, attempts: 1);
                if (outcome != null && outcome.Hit && outcome.Cell.x == 1) break;
            }
        }

        Assert.NotNull(outcome);
        Assert.True(outcome.Hit && outcome.Cell.x == 1, "fixture: needed a hit landing on the centre column within the attempt budget");

        // Exactly the three facing-edge cells (y=0), damage/3 each; nothing behind them (y=1).
        var byCell = hitCells.GroupBy(h => h.pos).ToDictionary(g => g.Key, g => g.Sum(h => h.damage));
        Assert.Equal(3, byCell.Count);
        foreach (var x in new[] { 0, 1, 2 })
        {
            Assert.True(byCell.TryGetValue(new int2(x, 0), out var d), $"expected a hit on facing cell ({x},0)");
            Assert.Equal(5f / 3f, d, 3); // weapon Damage is 5 (this fixture's Gun, no override)
            Assert.False(byCell.ContainsKey(new int2(x, 1)), $"cell ({x},1) is behind the facing edge and must be untouched");
        }
    }

    // ARayThroughACornerCrossesTheCornerCell: the diagonal case the old 0.5-step march (Entity.ApplyHit,
    // deleted by this cut) could skip. A bearing angled off both axes and a lateral offset comfortably inside
    // one cell's own shadow (not on a shared boundary) crosses several cells; the exact slab walk must not
    // drop one of them the way a fixed-step Euclidean march could stumble at a shared corner.
    [Fact]
    public void ARayThroughACornerCrossesTheCornerCell()
    {
        var shape = SolidShape(3, 3);
        var hull = new HullData { Name = "Diagonal", HullType = HullType.Ship, Shape = shape };
        var b = normalize(float2(1, 1));
        var buffer = new LaneCell[shape.Coordinates.Length];

        var count = FireControl.Lane(hull, b, -0.1f, buffer);
        var cells = Enumerable.Range(0, count).Select(i => buffer[i].Cell).ToList();

        Assert.Contains(new int2(0, 0), cells);
        Assert.Contains(new int2(1, 1), cells);
        Assert.Contains(new int2(2, 2), cells);
        Assert.True(cells.IndexOf(new int2(0, 0)) < cells.IndexOf(new int2(1, 1)));
        Assert.True(cells.IndexOf(new int2(1, 1)) < cells.IndexOf(new int2(2, 2)));
    }

    // LaneRemainderReachesTheHull (Q12-3 = A): a ray that exits a thin, bare hull -- both cells reached, no
    // armour anywhere -- delivers its whole remainder to Hull.Durability, not to nowhere.
    [Fact]
    public void LaneRemainderReachesTheHull()
    {
        var e = Build(TestSettings(), SolidShape(1, 2), precision: 1f, penetration: 5f);
        e.Target.Armor[0, 0] = 0f; e.Target.MaxArmor[0, 0] = 0f;
        e.Target.Armor[0, 1] = 0f; e.Target.MaxArmor[0, 1] = 0f;
        // Every fixture's target carries a bystander Gun at (0,0) (Entity.Update NREs on an entity that never
        // equipped anything) -- irrelevant to this test, but its own 1 durability would otherwise eat 1 of the
        // 40 damage as a confound. Zeroing it here removes that, matching what "no armour or item anywhere"
        // actually needs.
        e.Target.GearOccupancy[0, 0].EquippableItem.Durability = 0f;

        var hullBefore = e.Target.Hull.Durability;
        var outcome = FireUntilHit(e, damageOverride: 40f);
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);

        Assert.Equal(hullBefore - 40f, e.Target.Hull.Durability, 2);
    }

    // Stryker survivor triage (this cut's own changed lines): Entity.Absorb's `d > 0f` guard boundary-flipped to
    // `d >= 0f` survived -- at exactly 0 incoming, real code emits nothing ("only for incoming > 0"), the mutant
    // would additionally fire ArmorDamage(cell, 0). Not a rare float coincidence: a spent lane (rem already 0)
    // walks past every remaining cell in FireControl.Apply's own loop, so this is the ordinary "nothing left"
    // case, not an edge case.
    [Fact]
    public void AbsorbEmitsNothingAtExactlyZeroIncomingDamage()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, markers: new[] { (new int2(1, 1), 50f) });
        e.Target.Armor[1, 1] = 5f; e.Target.MaxArmor[1, 1] = 5f;
        var marker = e.Markers[0];

        var armorEvents = 0;
        var itemEvents = 0;
        using var a = e.Target.ArmorDamage.Subscribe(_ => armorEvents++);
        using var i = e.Target.ItemDamage.Subscribe(_ => itemEvents++);

        var remainder = e.Target.Absorb(new int2(1, 1), 0f);

        Assert.Equal(0f, remainder);
        Assert.Equal(0, armorEvents);
        Assert.Equal(0, itemEvents);
        Assert.Equal(5f, e.Target.Armor[1, 1]);
        Assert.Equal(50f, marker.EquippableItem.Durability);
    }

    // Stryker survivor: Entity.Absorb's `d > 0.1f` item-phase guard boundary-flipped to `d >= 0.1f` survived --
    // at exactly 0.1f remaining after armor, real code leaves the item untouched (the remainder returns to the
    // caller instead), the mutant would additionally spend it on the item.
    [Fact]
    public void AbsorbLeavesTheItemUntouchedAtExactlyPointOneRemaining()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, markers: new[] { (new int2(1, 1), 50f) });
        e.Target.Armor[1, 1] = 0f; e.Target.MaxArmor[1, 1] = 0f; // bare, so the whole .1f reaches the item check
        var marker = e.Markers[0];

        var itemEvents = 0;
        using var i = e.Target.ItemDamage.Subscribe(_ => itemEvents++);

        var remainder = e.Target.Absorb(new int2(1, 1), 0.1f);

        Assert.Equal(0.1f, remainder, 3);
        Assert.Equal(0, itemEvents);
        Assert.Equal(50f, marker.EquippableItem.Durability);
    }

    // Stryker survivor: Entity.DamageHull's `damage > .1f` guard boundary-flipped to `damage >= .1f` survived --
    // at exactly 0.1f, real code does nothing; the mutant would subtract it from Hull.Durability and fire
    // HullDamage.
    [Fact]
    public void DamageHullDoesNothingAtExactlyPointOne()
    {
        var e = Build(TestSettings(), SolidShape(1, 2), precision: 1f);
        var before = e.Target.Hull.Durability;
        var events = 0;
        using var h = e.Target.HullDamage.Subscribe(_ => events++);

        e.Target.DamageHull(0.1f);

        Assert.Equal(0, events);
        Assert.Equal(before, e.Target.Hull.Durability);
    }

    // Stryker survivor: FireControl.Apply's `target.IncomingHit.OnNext(shot.Source)` (moved here from the
    // deleted Entity.ApplyHit's own first line) survived a Statement-to-";" mutation -- nothing asserted that
    // it still fires on a resolved, unshielded hit.
    [Fact]
    public void ApplyFiresIncomingHitOnAResolvedHit()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f);
        Entity source = null;
        using var s = e.Target.IncomingHit.Subscribe(src => source = src);

        var outcome = FireUntilHit(e, damageOverride: 5f);
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);
        Assert.Same(e.Shooter, source);
    }

    // Stryker survivors: FireControl.Apply's two `walked[li] > 0` checks (metalLanes counting, and the
    // absorb-loop entry) both boundary-flipped to `walked[li] >= 0` survived -- since a lane count can never be
    // negative, `>= 0` is always true, so BOTH mutants make every lane count as "met metal" and enter the
    // absorb loop, even a lane that is entirely off the hull. A 1-wide hull with spread 1 (3 lanes: centre plus
    // one on each side) puts the two side lanes off-hull by construction -- only the centre lane can ever find
    // metal. The metalLanes mutant would divide the shot's damage by 3 instead of 1 (a wrong, smaller number
    // reaching the one real cell); the loop-entry mutant would additionally walk the two off-hull lanes with
    // `walked[li]` still correctly 0 iterations internally, but still fall through to `hullDamage += rem`
    // (rem never consumed), leaking a full extra damagePerLane share into the hull for each miss.
    [Fact]
    public void LanesThatMissTheHullNeitherDiluteTheSplitNorLeakDamage()
    {
        var e = Build(TestSettings(), SolidShape(1, 9), precision: .4f, penetration: 0f, damageSpread: 1f);
        for (var y = 0; y < 9; y++) { e.Target.Armor[0, y] = 1000f; e.Target.MaxArmor[0, y] = 1000f; }

        var hullBefore = e.Target.Hull.Durability;
        var hits = new List<(int2 pos, float damage)>();
        using var s = e.Target.ArmorDamage.Subscribe(x => hits.Add(x));

        var outcome = FireUntilHit(e, damageOverride: 30f);
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);

        Assert.Single(hits); // only the centre lane's own impact cell ever records armour damage
        Assert.Equal(30f, hits[0].damage, 2); // the WHOLE damage, not damage/3 -- metalLanes counted only the hit
        Assert.Equal(hullBefore, e.Target.Hull.Durability); // nothing leaked to the hull from the off-hull lanes
    }

    // Stryker survivor: FireControl.Apply's penetration cutoff (`Entry - impactEntry >= shot.Penetration`)
    // boundary-flipped to `>` survived -- not a rare float coincidence: an axis-aligned lane's entry values are
    // exact integers (schematic cell spacing), so penetration authored as an exact integer routinely lands
    // exactly on a cell's own entry difference. Penetration 2 exactly against a 3-cell column (entries ~0,1,2):
    // the third cell sits exactly AT the boundary and must be excluded ("< penetration" is reached; the mutant
    // would let it through).
    // F3 fix batch (Soul, 2026-09-25): the marker at (1,1) used to carry 1e6 durability, so it alone absorbed
    // the whole 30 damage before the walk could ever reach (1,2) -- the boundary cell took 0 whether the `>`
    // mutant let it through or not, so the test could not fail. The marker now carries only 10, leaving a
    // remainder large enough that (1,2) would visibly lose armour if the walk reached it.
    [Fact]
    public void PenetrationExcludesACellExactlyAtItsOwnBoundary()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, penetration: 2f,
            markers: new[] { (new int2(1, 1), 10f) });
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 0f; e.Target.MaxArmor[c.x, c.y] = 0f; }
        e.Target.Armor[1, 2] = 50f; e.Target.MaxArmor[1, 2] = 50f; // exactly at entry-diff 2 == penetration
        var marker = e.Markers[0];

        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 200 && (outcome == null || !outcome.Hit || outcome.Cell.x != 1); attempt++)
            outcome = FireUntilHit(e, damageOverride: 30f, attempts: 1);
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit && outcome.Cell.x == 1, "fixture: needed a hit on the centre column within the attempt budget");
        Assert.Equal(new int2(1, 0), outcome.Cell);

        Assert.Equal(0f, marker.EquippableItem.Durability); // fixture: the marker's own 10 is fully spent, 20 left over
        Assert.Equal(50f, e.Target.Armor[1, 2]); // exactly at entry-diff 2 -- excluded, not "< penetration" -- the
                                                  // remaining 20 would otherwise have visibly damaged it
    }

    // F3: a non-integer penetration depth. Entries down an axis-aligned column are exact integers, so a
    // fractional penetration is never itself a boundary -- this pins the ordinary "< penetration" comparison at
    // a depth that isn't a round number, independent of the exact-integer boundary case above.
    [Fact]
    public void PenetrationExcludesCellsBeyondANonIntegerDepth()
    {
        var e = Build(TestSettings(), SolidShape(1, 4), precision: 1f, penetration: 1.5f);
        e.Target.Armor[0, 0] = 5f; e.Target.MaxArmor[0, 0] = 5f; // entry-diff 0: always reached
        e.Target.Armor[0, 1] = 5f; e.Target.MaxArmor[0, 1] = 5f; // entry-diff 1: within 1.5, reached
        e.Target.Armor[0, 2] = 50f; e.Target.MaxArmor[0, 2] = 50f; // entry-diff 2: beyond 1.5, excluded
        e.Target.Armor[0, 3] = 50f; e.Target.MaxArmor[0, 3] = 50f; // entry-diff 3: beyond 1.5, excluded
        e.Target.GearOccupancy[0, 0].EquippableItem.Durability = 0f; // remove the bystander Gun's own 1 durability

        var hullBefore = e.Target.Hull.Durability;
        var outcome = FireUntilHit(e, damageOverride: 30f);
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit);

        Assert.Equal(0f, e.Target.Armor[0, 0]); // 5 consumed
        Assert.Equal(0f, e.Target.Armor[0, 1]); // 5 consumed
        Assert.Equal(50f, e.Target.Armor[0, 2]); // untouched -- beyond the 1.5 depth
        Assert.Equal(50f, e.Target.Armor[0, 3]); // untouched -- beyond the 1.5 depth
        Assert.Equal(hullBefore - 20f, e.Target.Hull.Durability, 2); // 30 - 5 - 5 left over for the hull
    }

    // F4: a U-shaped hull, notched. Two arms (x=0 and x=2) rise from a connecting base (y=0); the left arm has
    // a gap at y=1 -- present at y=0,2,3, missing at y=1 (so the lane hits the base at y=0, a gap at y=1, then
    // the rest of the arm at y=2,3). An AP round (no blast) with ample penetration must stop at that first gap
    // and leave the far side of the notch (y=2,3, "the far prong") untouched. Kills the Stryker survivor that
    // deletes Lane's own contiguity break (`FireControl.cs:~895`, M9): without it, Lane would keep walking past
    // the gap and Apply would spend damage on cells the real rule says are unreachable.
    private static Shape NotchedU()
    {
        var shape = new Shape(3, 4);
        shape[new int2(0, 0)] = true; shape[new int2(0, 2)] = true; shape[new int2(0, 3)] = true; // left arm, notch at y=1
        for (var y = 0; y < 4; y++) shape[new int2(2, y)] = true; // right arm, solid
        shape[new int2(1, 0)] = true; // the base connecting the two arms
        return shape;
    }

    [Fact]
    public void AnAPRoundStopsAtTheFirstGapAndLeavesTheFarSideOfTheNotchUntouched()
    {
        var e = Build(TestSettings(), NotchedU(), precision: 1f, penetration: 10f); // ample: more than the whole hull's depth
        foreach (var c in e.HullData.Shape.Coordinates) { e.Target.Armor[c.x, c.y] = 0f; e.Target.MaxArmor[c.x, c.y] = 0f; }
        e.Target.Armor[0, 0] = 5f; e.Target.MaxArmor[0, 0] = 5f; // near side of the notch
        e.Target.Armor[0, 2] = 1000f; e.Target.MaxArmor[0, 2] = 1000f; // far side -- must stay untouched
        e.Target.Armor[0, 3] = 1000f; e.Target.MaxArmor[0, 3] = 1000f; // far side -- must stay untouched
        e.Target.GearOccupancy[0, 0].EquippableItem.Durability = 0f; // remove the bystander Gun's own 1 durability

        // The hull's other arm (x=2) carries no armour, so a stray hit landing there while retrying for the
        // notched column would leak its own damage straight to the hull -- tracked here per-attempt (reset each
        // retry) so only the accepted attempt's own contribution is asserted, not a cross-attempt total.
        var hullThisAttempt = 0f;
        ShotOutcome outcome = null;
        using (e.Target.HullDamage.Subscribe(x => hullThisAttempt += x))
        {
            for (var attempt = 0; attempt < 200 && (outcome == null || !outcome.Hit || outcome.Cell.x != 0); attempt++)
            {
                hullThisAttempt = 0f;
                outcome = FireUntilHit(e, damageOverride: 40f, attempts: 1);
            }
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit && outcome.Cell.x == 0, "fixture: needed a hit on the notched arm within the attempt budget");
        Assert.Equal(new int2(0, 0), outcome.Cell); // fixture precondition: the base is always this lane's impact cell

        Assert.Equal(0f, e.Target.Armor[0, 0]); // 5 consumed
        Assert.Equal(1000f, e.Target.Armor[0, 2]); // untouched -- beyond the first gap
        Assert.Equal(1000f, e.Target.Armor[0, 3]); // untouched -- beyond the first gap
        Assert.Equal(35f, hullThisAttempt, 2); // 40 - 5 left over for the hull, not spent past the gap
    }

    // F5: side-lane remainders must reach the hull too, axis-aligned so the lanes share no cells and lane
    // order cannot matter (Q12-3 = A, "the lane remainder goes into the hull where the lane ends"). A 3x2
    // hull, spread 1 (three one-cell-wide columns), armour only on the facing row: every lane's remainder
    // survives the facing cell and must land in the hull. Kills the Stryker survivor that guards the
    // hull-remainder add with `if (li == n)` (only the centre lane's remainder would count).
    [Fact]
    public void EverySideLanesRemainderReachesTheHullNotOnlyTheCentreLanes()
    {
        var e = Build(TestSettings(), SolidShape(3, 2), precision: .6f, penetration: 2f, damageSpread: 1f);
        for (var x = 0; x < 3; x++)
        for (var y = 0; y < 2; y++)
        {
            e.Target.Armor[x, y] = y == 0 ? 1f : 0f;
            e.Target.MaxArmor[x, y] = 1f;
            var occ = e.Target.GearOccupancy[x, y];
            if (occ != null) occ.EquippableItem.Durability = 0f; // remove the bystander Gun's own 1 durability
        }

        // Tracked per-attempt (reset each retry), the same reasoning as the notched-U test above: a stray hit on
        // the wrong lateral offset still spends real damage on this hull, and would otherwise contaminate a
        // before/after total taken across every retry.
        var hullThisAttempt = 0f;
        ShotOutcome outcome = null;
        using (e.Target.HullDamage.Subscribe(x => hullThisAttempt += x))
        {
            for (var attempt = 0; attempt < 200 && (outcome == null || !outcome.Hit || outcome.Cell.x != 1); attempt++)
            {
                hullThisAttempt = 0f;
                outcome = FireUntilHit(e, damageOverride: 15f, attempts: 1);
            }
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit && outcome.Cell.x == 1, "fixture: needed a hit landing on the centre column within the attempt budget");

        // Each lane: 15/3 = 5 per lane, minus the facing cell's 1 armour = 4 left over; three lanes -> 12.
        Assert.Equal(12f, hullThisAttempt, 2);
    }

    // F6: armour and an occupying item on the SAME cell -- no existing fixture combines them, so "armour
    // absorbs first" (Entity.Absorb) was only ever exercised with one or the other. Direct calls to Absorb,
    // matching this file's own existing style for pinning its per-cell arithmetic (AbsorbEmitsNothingAt...,
    // AbsorbLeavesTheItemUntouchedAt...). Kills a swap of the armour and item blocks in Absorb.
    [Fact]
    public void AbsorbSpendsArmourBeforeTheItemOnTheSameCell()
    {
        var e = Build(TestSettings(), SolidShape(3, 3), precision: 1f, markers: new[] { (new int2(1, 1), 50f) });
        var marker = e.Markers[0];
        void Reset() { e.Target.Armor[1, 1] = 10f; e.Target.MaxArmor[1, 1] = 10f; marker.EquippableItem.Durability = 50f; }

        Reset();
        var lessThanArmour = e.Target.Absorb(new int2(1, 1), 5f);
        Assert.Equal(0f, lessThanArmour);
        Assert.Equal(5f, e.Target.Armor[1, 1]); // 5 of the 10 spent
        Assert.Equal(50f, marker.EquippableItem.Durability); // item untouched -- armour alone covered it

        Reset();
        var moreThanArmourLessThanBoth = e.Target.Absorb(new int2(1, 1), 25f);
        Assert.Equal(0f, moreThanArmourLessThanBoth);
        Assert.Equal(0f, e.Target.Armor[1, 1]); // the whole 10 spent
        Assert.Equal(35f, marker.EquippableItem.Durability, 2); // exactly the 15 excess

        Reset();
        var moreThanBoth = e.Target.Absorb(new int2(1, 1), 80f);
        Assert.Equal(20f, moreThanBoth, 2); // exactly 80 - 10 - 50
        Assert.Equal(0f, e.Target.Armor[1, 1]);
        Assert.Equal(0f, marker.EquippableItem.Durability);
    }

    // F7: direct-hit frame handedness at |fx| > |fy| is otherwise only guarded by Cut 11's Splash tests, which
    // 12.4 deletes. A target facing (2,1) normalized (|fx| > |fy|), shot from its own starboard side (computed
    // here independently of Entity.ToSchematic, as the standard right-perpendicular of the facing -- the same
    // relationship ToSchematic is supposed to implement, not a call into it). Only starboard-half cells (x >=
    // width/2 in the hull's own schematic frame) carry armour; port cells (x < width/2) do not. Kills a flip of
    // ToSchematic's right axis when |fx| > |fy| (M6a).
    [Fact]
    public void ADirectHitFromStarboardDamagesStarboardCellsWhenFacingIsMostlyLateral()
    {
        var shape = SolidShape(5, 5);
        var facing = normalize(float2(2, 1)); // |fx| > |fy|
        var e = Build(TestSettings(), shape, precision: .4f, penetration: 0f, targetFacing: facing);

        // Starboard is the standard right-perpendicular of the facing (forward.y, -forward.x) -- computed here,
        // not read from Entity.ToSchematic.
        var starboardWorld = float2(facing.y, -facing.x);
        e.Shooter.Position = e.Target.Position + float3(starboardWorld.x, 0, starboardWorld.y) * 100f; // shooter sits to starboard

        for (var x = 0; x < 5; x++)
        for (var y = 0; y < 5; y++)
        {
            var starboard = x >= 3; // starboard half; x == 2 (the middle column) is left ambiguous and unarmoured
            e.Target.Armor[x, y] = x <= 1 ? 0f : starboard ? 1f : 0f;
            e.Target.MaxArmor[x, y] = 1f;
        }

        var hitCells = new List<int2>();
        using var s = e.Target.ArmorDamage.Subscribe(x => hitCells.Add(x.pos));
        for (var i = 0; i < 80; i++) { FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter); e.Zone.Update(.01f); }

        Assert.True(hitCells.Count > 5, $"expected several hits, got {hitCells.Count}");
        foreach (var c in hitCells)
            Assert.True(c.x >= 3, $"starboard should face the shot, but {c} is on the port side");
    }

    // ===================== F1: an independent direct-hit model at angled bearings =====================
    // Every existing test through Fire commits at (0,+-1) -- ARayThroughACornerCrossesTheCornerCell calls Lane
    // directly instead. This model is an exact double-precision ray/box walk, independent of
    // FireControl.Lane/AlongBearing/SlabAxis, ported from the Soul probe (SoulApply123.cs) that verified this
    // cut's arithmetic against production to 14,984/15,000 shots (the 16 mismatches were a float boundary).
    // Used only to compute these tests' own expected values -- never to call production.
    private struct MCell { public int2 C; public double En, Ex; }

    private static List<MCell> ModelLane(Shape shape, double bx, double by, double s)
    {
        double lx = -by, ly = bx;
        var all = new List<MCell>();
        foreach (var c in shape.Coordinates)
        {
            double t0 = double.NegativeInfinity, t1 = double.PositiveInfinity; var ok = true;
            void Ax(double d, double p0, double lo, double hi)
            {
                if (!ok) return;
                if (Math.Abs(d) < 1e-12) { if (!(p0 > lo && p0 < hi)) ok = false; return; }
                var a = (lo - p0) / d; var q = (hi - p0) / d;
                t0 = Math.Max(t0, Math.Min(a, q)); t1 = Math.Min(t1, Math.Max(a, q));
            }
            Ax(bx, s * lx, c.x - .5, c.x + .5);
            Ax(by, s * ly, c.y - .5, c.y + .5);
            if (ok && t1 - t0 > 1e-7) all.Add(new MCell { C = c, En = t0, Ex = t1 });
        }
        all.Sort((p, q) => p.En.CompareTo(q.En));
        var walked = new List<MCell>();
        for (var i = 0; i < all.Count; i++)
        {
            if (i > 0 && all[i].En > all[i - 1].Ex + 1e-4) break;
            walked.Add(all[i]);
        }
        return walked;
    }

    private static double[,] ToD(float[,] a)
    {
        var d = new double[a.GetLength(0), a.GetLength(1)];
        for (var i = 0; i < a.GetLength(0); i++) for (var j = 0; j < a.GetLength(1); j++) d[i, j] = a[i, j];
        return d;
    }

    private sealed class DirectHitModel { public double[,] Armor; public Dictionary<EquippedItem, double> Items; public double Hull; }

    // Single lane (spread 0 -- F2's lane-spacing ruling only concerns spread >= 1): armour, then the occupying
    // item, then the hull, near-to-far, clipped to the penetration depth from the impact cell's own entry.
    private static DirectHitModel ModelDirectHit(Engagement e, Shape shape, double[,] armorBefore,
        Dictionary<EquippedItem, double> itemsBefore, double bx, double by, double s, double damage, double penetration)
    {
        var r = new DirectHitModel { Armor = (double[,]) armorBefore.Clone(), Items = new Dictionary<EquippedItem, double>(itemsBefore) };
        var lane = ModelLane(shape, bx, by, s);
        if (lane.Count == 0) { r.Hull = damage; return r; }
        var rem = damage; var en0 = lane[0].En;
        for (var i = 0; i < lane.Count; i++)
        {
            if (i > 0 && lane[i].En - en0 >= penetration) break;
            var c = lane[i].C;
            if (rem > 0) { var a = r.Armor[c.x, c.y]; r.Armor[c.x, c.y] = Math.Max(a - rem, 0); rem = Math.Max(rem - a, 0); }
            if (rem > .1)
            {
                var item = e.Target.GearOccupancy[c.x, c.y];
                if (item != null) { var d = r.Items[item]; r.Items[item] = Math.Max(d - rem, 0); rem = Math.Max(rem - d, 0); }
            }
        }
        r.Hull = rem;
        return r;
    }

    // F1: a full per-cell parity check against the independent model above, at three angled bearings -- 45
    // degrees, a shallow angle (within about 11 degrees of an axis) and one with |bx| > |by|. Kills a 0.5-cell
    // walk (M4), a mirrored bearing when |bx| > |by| (M6b) and a bearing snapped to the nearest axis (M20): all
    // three produce a visibly wrong set of cells or a wrong entry order against this model at these bearings.
    [Fact]
    public void DirectHitAtAnAngledBearingMatchesAnIndependentModel()
    {
        void Check(float2 travelDirection, int seed)
        {
            var shape = SolidShape(5, 5);
            var e = Build(TestSettings(), shape, precision: 1f, penetration: 3f);
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;

            var rng = new System.Random(seed);
            foreach (var c in shape.Coordinates)
            {
                var a = (float) Math.Round(rng.NextDouble() * 6, 2);
                e.Target.Armor[c.x, c.y] = a; e.Target.MaxArmor[c.x, c.y] = Math.Max(a, 1f);
            }
            // Equipment includes the ship's own EquippedHull -- excluded here, or randomising its durability
            // would corrupt Hull.Durability itself, not a schematic-cell item.
            foreach (var it in e.Target.Equipment)
                if (it.EquippableItem != e.Target.Hull)
                    it.EquippableItem.Durability = (float) Math.Round(rng.NextDouble() * 15, 2);

            var armorBefore = ToD(e.Target.Armor);
            var itemsBefore = e.Target.Equipment.Where(x => x.EquippableItem != e.Target.Hull)
                .ToDictionary(x => x, x => (double) x.EquippableItem.Durability);

            ShotOutcome outcome = null;
            double hullEv = 0;
            using (e.Target.HullDamage.Subscribe(x => hullEv += x))
            {
                for (var attempt = 0; attempt < 400 && (outcome == null || !outcome.Hit); attempt++)
                    outcome = FireUntilHit(e, damageOverride: 40f, attempts: 1);
            }
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            // fixture precondition: default facing makes the schematic bearing equal the world travel direction.
            var actualTravel = FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target);
            Assert.Equal(travelDirection.x, actualTravel.x, 3);
            Assert.Equal(travelDirection.y, actualTravel.y, 3);

            var model = ModelDirectHit(e, shape, armorBefore, itemsBefore, outcome.Bearing.x, outcome.Bearing.y, outcome.Lateral, 40, 3);

            foreach (var c in shape.Coordinates)
                Assert.Equal(model.Armor[c.x, c.y], e.Target.Armor[c.x, c.y], 2);
            foreach (var kv in model.Items)
                Assert.Equal(kv.Value, (double) kv.Key.EquippableItem.Durability, 2);
            Assert.Equal(model.Hull, hullEv, 2);
        }

        Check(normalize(float2(1, 1)), seed: 1); // 45 degrees
        Check(normalize(float2(.2f, 1)), seed: 2); // shallow: ~11 degrees off the y-axis
        Check(normalize(float2(2, 1)), seed: 3); // |bx| > |by|
    }

    // F1: the spec's own diagonal case (Probe1's corner-crossing lane), through the full Fire -> Commit ->
    // Apply path rather than a direct Lane call. Retries until a hit's own ArmorDamage events show the lane
    // walking exactly the three corner cells in order -- kills the same M4/M6b/M20 mutants as above from a
    // different angle: a 0.5-cell march, a mirrored bearing or an axis-snapped bearing would each produce some
    // other event sequence (or none matching), and 400 attempts would never find this exact one.
    [Fact]
    public void DiagonalDirectHitReachesEveryCornerCellInOrderThroughFireCommitApply()
    {
        var shape = SolidShape(3, 3);
        var e = Build(TestSettings(), shape, precision: 1f, penetration: 100f); // ample: never cuts the walk short
        e.Shooter.Position = e.Target.Position - float3(1, 0, 1) * (100f / (float) Math.Sqrt(2)); // exactly 45 degrees
        e.Target.GearOccupancy[0, 0].EquippableItem.Durability = 0f; // remove the bystander Gun's own 1 durability

        // Everything bare except the three corner cells (light armour, so damage passes through every one of
        // them into whatever comes next): a bearing this close to 45 degrees admits the diagonal's own row/column
        // neighbours too (their shadow overlaps the corner at this angle -- confirmed by sweeping FireControl.Lane
        // directly over s), so the walked lane is usually five cells, not three; the spec only requires the
        // three corner cells to be present and in that relative order (Probe1's own "reaches (2,2)"), the same
        // standard ARayThroughACornerCrossesTheCornerCell already pins for a direct Lane call.
        void ResetArmor()
        {
            foreach (var c in shape.Coordinates) { e.Target.Armor[c.x, c.y] = 0f; e.Target.MaxArmor[c.x, c.y] = 0f; }
            e.Target.Armor[0, 0] = 5f; e.Target.MaxArmor[0, 0] = 5f;
            e.Target.Armor[1, 1] = 5f; e.Target.MaxArmor[1, 1] = 5f;
            e.Target.Armor[2, 2] = 5f; e.Target.MaxArmor[2, 2] = 5f;
        }
        ResetArmor();

        var events = new List<int2>();
        using var sub = e.Target.ArmorDamage.Subscribe(x => events.Add(x.pos));
        var found = false;
        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 400 && !found; attempt++)
        {
            events.Clear();
            outcome = FireUntilHit(e, damageOverride: 40f, attempts: 1);
            var i0 = events.IndexOf(new int2(0, 0));
            var i1 = events.IndexOf(new int2(1, 1));
            var i2 = events.IndexOf(new int2(2, 2));
            found = outcome != null && outcome.Hit && i0 >= 0 && i1 > i0 && i2 > i1;
            if (!found) ResetArmor(); // a hit landing off the diagonal may have spent some of these cells' armour
        }
        Assert.True(found, "fixture: needed a hit whose lane reaches (0,0), then (1,1), then (2,2), in that order");

        Assert.Equal(0f, e.Target.Armor[0, 0]);
        Assert.Equal(0f, e.Target.Armor[1, 1]);
        Assert.Equal(0f, e.Target.Armor[2, 2]);
    }

    // ===================== F2: spread lanes diffuse sideways instead of overlapping =====================
    // Operator ruling (2026-09-25): "I would prefer if the overlapping damage cells were diffused sideways to
    // thicken the line rather than doubling up." FireControl.Apply now spaces lanes by the shadow width a cell
    // casts at the committed bearing rather than by one cell. These tests pin the resulting BEHAVIOUR -- no
    // cell struck twice, mirrored damage, a footprint that widens at an angle -- not the spacing value itself
    // (operator, correcting an earlier framing: "test behavior, not shape").

    // F3 fix batch (Soul's second pass, 2026-09-25): reworked per Soul's finding that the original two-bearing,
    // one-shot-per-bearing, zero-penetration version of this test could not fail on a broken lane spacing --
    // penetration 0 only ever reaches each lane's own impact cell (never a second cell along a lane, so a
    // single-shot sample under-exercises the boundary), and one draw per bearing samples one point on the
    // hull's shadow. (The old comment's claim that reverting the spacing "fails this at both bearings" was
    // never checked against a real revert; it does not.) Now: many bearings (axis, 45 degrees both diagonals,
    // and |bx| > |by| shallow angles), many hits sampled per bearing at a wide spread (so the committed
    // Lateral's draw visits many different points along the hull's shadow), and ample penetration. Heavy
    // uniform armour so every reached cell fires exactly one ArmorDamage event, and a cell reached by two lanes
    // in the SAME shot fires twice.
    // F1's own structural fix (`FireControl.Lanes`, now built on top of `FireControl.Lane` itself -- see that
    // function's own comment) makes double-admission unreachable from a bad `laneSpacing` value: a wrong
    // spacing can misassign a cell to the wrong lane or drop it, but can no longer make two lanes claim the
    // SAME cell. Hand verification (fc123fix2-mut.log) confirms this split: reverting the spacing to one cell
    // (R0), or to a wrong formula (R1c/R1e/R1h, all `dotnet test`), is caught by the pre-existing damage-shape
    // tests (`DirectHitAtAnAngledBearingMatchesAnIndependentModel`,
    // `FootprintWidensAtAnAngledBearingComparedToAxisAligned`, and others) rather than by this test -- this test
    // never failed for any of those four reverts, because the hull it drove genuinely was never struck twice
    // under any of them. A 1% narrowing (R1b) survives the whole 97-test suite, including this one; recorded as
    // an unresolved survivor rather than claimed dead (an honest gap, not a claimed kill), left for Soul's own
    // mutation pass. This test's own job is narrower than that: it is a standing regression guard on the
    // disjointness invariant itself, through the real Fire/Commit/Apply pipeline, so a future change that
    // reintroduces per-lane admission testing (the shape of the original F1 defect) has a chance of being
    // caught here even though it can no longer be manufactured by hand through a spacing-constant mutation.

    // ===================== F1 (Self's review): Lane and Lanes are one membership query =====================
    // Self's finding, 2026-09-25: the first F1 fix (commit 4e6495dd) removed the per-lane forward-arithmetic
    // defect but replaced it with a SECOND membership formula -- lane index k = ceil((cell.Lo - centreLateral)
    // / laneSpacing), agreeing with FireControl.Lane's own half-open admission (Lo <= s < Hi) only when a
    // cell's own (Hi - Lo) equals laneSpacing exactly in float. `FireControl.Lanes` is now built entirely out
    // of `FireControl.Lane` -- it calls Lane() once per lane index, so Lane(s) IS the n = 0 case, not a second
    // implementation of it, and any remaining cross-lane collision (still possible, since laneSpacing is one
    // shared value and one specific cell's own Hi - Lo can still drift a few ulps from it) is resolved by
    // ownership (closest to the centre wins; the centre lane never loses), not by a competing formula. This
    // test pins that contract directly through the PUBLIC FireControl.Lanes and FireControl.Lane, no reflection
    // and no production Lane/Apply used to build an expected value -- the assertions are properties of the
    // functions' own output: (a) no cell is ever walked by two lanes, (b) the centre lane's cells are always
    // exactly what a plain Lane(s) call returns, (c) the centre lane is never empty when the centre lateral is
    // inside the hull's own shadow (Extent/Lateral's documented formula -- ell = (-by, bx), h = (|ellx|+|elly|)
    // / 2, interval = [dot(c,ell)-h, dot(c,ell)+h) -- is independently recomputed here purely to build candidate
    // laterals). Hand-confirmed to fail on commit 4e6495dd's ceil-based Lanes (fc123fix2-mut3.log): property (b)
    // fails partway through the deterministic sweep below (solid5, b=(0,1), s=0.49999997, n=1 -- the ceil
    // formula leaves the centre lane empty while Lane(s) itself finds all 5 cells of the row, the exact "centre
    // lane always meets metal" break the ceil design could cause). Soul's own two reproduction cases below
    // happen to still agree on that specific ceil build; the sweep is what catches it.
    [Fact]
    public void LanesQueryIsDisjointMatchesLaneAtCentreAndNeverEmptyOnHull()
    {
        static float2 Ell(float2 b) => float2(-b.y, b.x);
        static (float Lo, float Hi) CellExtent(int2 c, float2 ell)
        {
            var h = (Math.Abs(ell.x) + Math.Abs(ell.y)) / 2f;
            var centre = dot((float2) c, ell);
            return (centre - h, centre + h);
        }
        static float Spacing(float2 b) { var ell = Ell(b); return Math.Abs(ell.x) + Math.Abs(ell.y); }

        void Check(string name, Shape shape, float2 b, float s, int n)
        {
            var hull = new HullData { Name = name, HullType = HullType.Ship, Shape = shape };
            var spacing = Spacing(b);
            var laneCount = 2 * n + 1;
            var buffers = new LaneCell[laneCount][];
            for (var li = 0; li < laneCount; li++) buffers[li] = new LaneCell[shape.Coordinates.Length];
            var walked = FireControl.Lanes(hull, b, s, n, spacing, buffers);

            // (a) no cell appears in two lanes.
            var seen = new HashSet<int2>();
            for (var li = 0; li < laneCount; li++)
                for (var i = 0; i < walked[li]; i++)
                    Assert.True(seen.Add(buffers[li][i].Cell),
                        $"{name} b=({b.x:R},{b.y:R}) s={s:R} n={n}: cell {buffers[li][i].Cell} walked by two lanes (lane {li - n})");

            // (b) the centre lane equals a plain Lane(s) call.
            var solo = new LaneCell[shape.Coordinates.Length];
            var soloWalked = FireControl.Lane(hull, b, s, solo);
            Assert.True(soloWalked == walked[n], $"{name} b=({b.x:R},{b.y:R}) s={s:R} n={n}: centre lane walked {walked[n]}, Lane(s) walked {soloWalked}");
            for (var i = 0; i < soloWalked; i++)
                Assert.Equal(solo[i].Cell, buffers[n][i].Cell);

            // (c) the centre lane is never empty when s is inside the hull's own shadow.
            var ell = Ell(b);
            var insideShadow = shape.Coordinates.Any(c => { var (lo, hi) = CellExtent(c, ell); return s >= lo && s < hi; });
            if (insideShadow)
                Assert.True(walked[n] > 0, $"{name} b=({b.x:R},{b.y:R}) s={s:R} n={n}: centre lane empty although s is inside the hull's own shadow");
        }

        // Soul's own concrete reproductions (SoulApply123b.Disjoint, 2026-09-25).
        Check("solid5", SolidShape(5, 5), float2(0, 1), -0.50000006f, 1);
        Check("solid9", SolidShape(9, 9), normalize(float2(-0.69478226f, 0.7192201f)), -5.619354f, 1);

        // A deterministic sweep at every cell's own shadow edge (Lo and Hi) and its immediate float neighbours,
        // across several hulls, bearings (exact axis, 45 degrees, |bx| > |by|, and Soul's own angled example),
        // and spreads 1-2.
        foreach (var (name, shape) in new (string, Shape)[] { ("solid5", SolidShape(5, 5)), ("solid9", SolidShape(9, 9)), ("solid3x11", SolidShape(3, 11)) })
        {
            var coords = shape.Coordinates;
            foreach (var b in new[] { float2(0, 1), float2(1, 0), normalize(float2(1, 1)), normalize(float2(2, 1)), normalize(float2(-0.69478226f, 0.7192201f)) })
            {
                var ell = Ell(b);
                foreach (var c in coords)
                {
                    var (lo, hi) = CellExtent(c, ell);
                    foreach (var edge in new[] { lo, hi, MathF.BitIncrement(lo), MathF.BitDecrement(lo), MathF.BitIncrement(hi), MathF.BitDecrement(hi) })
                        for (var n = 1; n <= 2; n++)
                            Check(name, shape, b, edge, n);
                }
            }
        }
    }

    // F1 (Self's review): the committed Cell (Commit's own placement) is the first cell Apply damages in the
    // centre lane -- end to end through Fire -> Commit -> Apply, no reflection. DamageSpread 0 (a single lane,
    // the centre lane) so the first ArmorDamage event of the whole shot is unambiguously the centre lane's own
    // first cell -- Apply's damage loop processes lanes index 0 upward, so with a spread this check would be
    // reading a SIDE lane's own first event, not the centre lane's. Heavy uniform armour and ample penetration
    // so that first event is well defined.
    [Fact]
    public void CommittedCellIsTheFirstCellTheCentreLaneDamages()
    {
        var bearings = new[]
        {
            float2(0, 1), normalize(float2(1, 1)), normalize(float2(-1, 1)),
            normalize(float2(2, 1)), normalize(float2(1, 2))
        };
        foreach (var travelDirection in bearings)
        {
            var shape = SolidShape(9, 9);
            var e = Build(TestSettings(), shape, precision: 1f, penetration: 100f, damageSpread: 0f);
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;
            foreach (var c in shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }

            var firstHit = new int2?[1];
            using var s = e.Target.ArmorDamage.Subscribe(x => { if (firstHit[0] == null) firstHit[0] = x.pos; });
            var outcome = FireUntilHit(e, damageOverride: 50f);
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            Assert.NotNull(firstHit[0]);
            Assert.Equal(outcome.Cell, firstHit[0].Value);
        }
    }
    [Fact]
    public void NoCellIsStruckByTwoLanesInOneShotAtAnyBearing()
    {
        // Within the fixture's own FiringArc (170 degrees, i.e. +-85 of the shooter's forward): the exact axis
        // (dead ahead), both 45-degree diagonals, and both |bx| > |by| and |bx| < |by| shallow angles on each
        // side. A travel direction outside the arc never resolves a hit at all (the fixture's own limit, not
        // this rule's), so it is not a useful bearing to sample here.
        var bearings = new[]
        {
            float2(0, 1), // exact axis
            normalize(float2(1, 1)), normalize(float2(-1, 1)), // 45 degrees
            normalize(float2(2, 1)), normalize(float2(-2, 1)), // |bx| > |by|
            normalize(float2(1, 2)), normalize(float2(-1, 2)) // |bx| < |by|
        };
        const int hitsWanted = 300;

        foreach (var travelDirection in bearings)
        {
            var shape = SolidShape(9, 9);
            var e = Build(TestSettings(), shape, precision: .02f, penetration: 100f, damageSpread: 2f); // ample penetration, 5 lanes; a wide spread samples many points along the shadow
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;

            var thisShot = new List<int2>();
            using var s = e.Target.ArmorDamage.Subscribe(x => thisShot.Add(x.pos));
            ShotOutcome outcome = null;
            using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);

            var got = 0;
            for (var attempt = 0; attempt < 20000 && got < hitsWanted; attempt++)
            {
                foreach (var c in shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }
                thisShot.Clear();
                outcome = null;
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter, damageOverride: 50f);
                e.Zone.Update(.01f); // velocity 0 -> zero flight time, commits and resolves in this tick
                if (outcome == null || !outcome.Hit) continue;
                got++;
                Assert.Equal(thisShot.Count, thisShot.Distinct().Count()); // no cell fires ArmorDamage twice within this shot
            }
            Assert.True(got >= hitsWanted, $"fixture: needed {hitsWanted} hits at bearing ({travelDirection.x:R},{travelDirection.y:R}), got {got}");
        }
    }

    // The footprint widens at an angled bearing rather than concentrating: the same spread, on the same hull,
    // covers more lateral distance at 45 degrees than axis-aligned. Reverting the lane spacing to one cell
    // removes this widening (every bearing gets the same one-cell spacing regardless of angle).
    [Fact]
    public void FootprintWidensAtAnAngledBearingComparedToAxisAligned()
    {
        double Span(float2 travelDirection)
        {
            var shape = SolidShape(9, 9);
            var e = Build(TestSettings(), shape, precision: 1f, penetration: 0f, damageSpread: 2f);
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;
            foreach (var c in shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }

            var hits = new List<int2>();
            using var s = e.Target.ArmorDamage.Subscribe(x => hits.Add(x.pos));
            var outcome = FireUntilHit(e, damageOverride: 50f);
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            Assert.True(hits.Count >= 3, $"fixture: needed several lanes to find metal, got {hits.Count}");

            var b = normalize(travelDirection);
            var ell = float2(-b.y, b.x);
            var projections = hits.Select(c => (double) dot((float2) c, ell)).ToList();
            return projections.Max() - projections.Min();
        }

        var axisSpan = Span(float2(0, 1));
        var angledSpan = Span(normalize(float2(1, 1)));

        Assert.True(angledSpan > axisSpan * 1.2, $"the footprint should widen at an angled bearing: axis {axisSpan}, 45deg {angledSpan}");
    }

    // ===================== F8: proportional absorption when a multi-cell item is shared between lanes =====================
    // Operator ruling (2026-09-25, docs/fire-control-cut.md, Cut 12.3 status): "proportional absorption; go."
    // When two or more lanes of one shot reach the same multi-cell item, the item absorbs their COMBINED
    // incoming damage (up to its own remaining durability) and each lane's leftover is in proportion to what
    // it brought -- replacing the old behaviour where whichever lane was processed first (always the
    // lower-indexed, physically left-of-travel lane) drained the item and every other lane inherited whatever
    // it left behind.

    // F8: the ruling's own literal case, through Fire/Commit/Apply (no reflection). Tool-hardpoint items
    // (Bar, Marker) can only occupy INTERIOR cells (Entity.ItemFits, `hullData.InteriorCells[itemCoord]` --
    // Shape.Shrink needs all 8 neighbours present, the same constraint DamageIsAbsorbedInOrderAlongTheRay's own
    // comment already notes for a single-cell marker on a 3x3 hull). A marker sitting BEHIND the shared item
    // needs its own interior row too, so this fixture is 4 cells deep: SolidShape(5,4)'s two interior rows are
    // y=1 (the Bar, columns 1-3) and y=2 (one marker behind each of the Bar's own two cells). Note
    // Entity.ItemDamage reports each lane's own INCOMING contribution, not the clamped/actual-absorbed amount
    // (the same convention a single Absorb call already has -- an overkill hit against a nearly-dead item still
    // reports the whole swing), so it cannot distinguish proportional from sequential absorption by itself;
    // the item's own durability (the ACTUAL total absorbed) and each marker's own durability (the ACTUAL
    // leftover that reached it) are the two numbers this test reads.
    // With damageSpread 1 retried until the committed impact is column 2 (the middle of the three interior
    // columns), the lanes are columns {1,2,3}: the item's own two cells (1,1)/(2,1) are each a DIFFERENT lane's
    // own facing cell, so armour placed directly on them (5 and 15) gives the two lanes different post-armour
    // remainders even though Apply splits the shot's damage evenly per lane before armour: D1 = 30-5 = 25
    // (column 1), D2 = 30-15 = 15 (column 2). Column 3 carries no item, an unrelated private lane.
    private (float leftoverLow, float leftoverHigh, float absorbed) RunSharedItemCase(float itemDurability)
    {
        var e = Build(TestSettings(), SolidShape(5, 4), precision: .6f, penetration: 2.5f, damageSpread: 1f,
            bars: new[] { (new int2(1, 1), itemDurability) },
            markers: new[] { (new int2(1, 2), 1000f), (new int2(2, 2), 1000f) });
        var bar = e.Target.GearOccupancy[1, 1]; // (1,1) and (2,1) are the same Bar

        void ResetState()
        {
            for (var x = 0; x < 5; x++)
            for (var y = 0; y < 4; y++)
            {
                e.Target.Armor[x, y] = 0f;
                e.Target.MaxArmor[x, y] = 20f;
            }
            e.Target.Armor[1, 1] = 5f;
            e.Target.Armor[2, 1] = 15f;
            bar.EquippableItem.Durability = itemDurability;
            e.Markers[0].EquippableItem.Durability = 1000f; // behind (1,1)
            e.Markers[1].EquippableItem.Durability = 1000f; // behind (2,1)
            e.Target.GearOccupancy[0, 0].EquippableItem.Durability = 0f; // remove the bystander Gun's own 1 durability
        }

        ShotOutcome outcome = null;
        for (var attempt = 0; attempt < 400 && (outcome == null || !outcome.Hit || outcome.Cell.x != 2); attempt++)
        {
            ResetState();
            outcome = FireUntilHit(e, damageOverride: 90f, attempts: 1);
        }
        Assert.NotNull(outcome);
        Assert.True(outcome.Hit && outcome.Cell.x == 2, "fixture: needed a hit on the centre interior column within the attempt budget");

        var leftover1 = 1000f - e.Markers[0].EquippableItem.Durability; // column 1's own leftover (D1=25)
        var leftover2 = 1000f - e.Markers[1].EquippableItem.Durability; // column 2's own leftover (D2=15)
        var absorbed = itemDurability - bar.EquippableItem.Durability;
        return leftover1 <= leftover2 ? (leftover1, leftover2, absorbed) : (leftover2, leftover1, absorbed);
    }

    // Item durability 30 < D1+D2 (40): the item takes all 30 of its own durability (absorbed = min(40,30)); the
    // combined 10 leftover splits 25*(10/40)=6.25 and 15*(10/40)=3.75 -- proportional to what each lane
    // brought, not to which lane Apply happens to visit first. Under the pre-fix-batch code (sequential, one
    // lane draining the item before the other carries on) the two leftovers are not this pair at all -- see
    // MirrorSymmetryHoldsWithASharedMultiCellItem below for the hand-confirmed pre-fix numbers on this exact
    // shape of fixture.
    [Fact]
    public void ProportionalAbsorptionSplitsTheLeftoverBetweenTheLanes()
    {
        var (leftoverLow, leftoverHigh, absorbed) = RunSharedItemCase(itemDurability: 30f);
        Assert.Equal(3.75f, leftoverLow, 2); // 15 * (10/40)
        Assert.Equal(6.25f, leftoverHigh, 2); // 25 * (10/40)
        Assert.Equal(30f, absorbed, 2); // the item's own durability, fully spent
    }

    // Item durability 50 >= D1+D2 (40): the item takes everything either lane brought; nothing continues past
    // it into either marker.
    [Fact]
    public void ProportionalAbsorptionLeavesNothingWhenTheItemAbsorbsEverything()
    {
        var (leftoverLow, leftoverHigh, absorbed) = RunSharedItemCase(itemDurability: 50f);
        Assert.Equal(0f, leftoverLow, 2);
        Assert.Equal(0f, leftoverHigh, 2);
        Assert.Equal(40f, absorbed, 2);
    }

    // F8: mirror symmetry with a multi-cell item, deterministic. Two fixtures on SolidShape(9,4) (interior rows
    // y=1, the Bar's own row, and y=2, the marker row behind it -- see RunSharedItemCase above), fired straight
    // ahead (bearing (0,1)) at damageSpread 1, each retried until the committed impact is the true centre
    // column (x=4, as CommittedCellIsTheFirstCellTheCentreLaneDamages already relies on for this bearing), so
    // the three lanes are always columns {3,4,5}. Fixture B's item and armour are fixture A's own layout
    // reflected about column 4 (mirror(x) = 8-x) -- A's Bar spans {3,4} (centre lane shares it with the LEFT
    // side lane); B's spans {4,5} (centre lane shares it with the RIGHT side lane), the mirror image of A's
    // placement. Column 4 is the self-mirror point and carries the same armour (15) on both; column 3 (A,
    // private) mirrors column 5 (B, private), both armour 2 (irrelevant to the assertion, just present so that
    // lane is metal).
    //
    // Compares the SORTED list of each fixture's own two marker leftovers -- not the bearing or the lateral
    // draw (which would also have to mirror the float lane-spacing arithmetic to stay exact). A's two D values
    // are 25 (column 3) and 15 (column 4); B's are 15 (column 4) and 25 (column 5) -- the same two D values on
    // both fixtures, so the proportional rule (sum then split, independent of which physical lane is which)
    // gives the SAME sorted leftover pair on both. Item durability 20 (not equal to either D -- 25 was tried
    // first and turned out degenerate: durability exactly equal to D_high made both fixtures land on {0,15} by
    // coincidence under the pre-fix code too, since one contributor is always driven to exactly 0 either way).
    // Hand-confirmed against commit d1cc047a (this cut's own parent, before the fix batch) on this exact
    // fixture shape at itemDurability 20: A's leftovers sorted {0, 20} vs B's sorted {5, 15} -- different, so
    // the assertion below fails there. See this cut's own report for the raw per-fixture numbers.
    [Fact]
    public void MirrorSymmetryHoldsWithASharedMultiCellItem()
    {
        const float itemDurability = 20f;

        (float low, float high) RunFixture(bool mirrored)
        {
            var barAnchor = mirrored ? new int2(4, 1) : new int2(3, 1);
            var markerCells = mirrored
                ? new[] { (new int2(4, 2), 1000f), (new int2(5, 2), 1000f) }
                : new[] { (new int2(3, 2), 1000f), (new int2(4, 2), 1000f) };
            var e = Build(TestSettings(), SolidShape(9, 4), precision: .6f, penetration: 2.5f, damageSpread: 1f,
                bars: new[] { (barAnchor, itemDurability) }, markers: markerCells);
            var bar = e.Target.GearOccupancy[barAnchor.x, barAnchor.y];

            void ResetState()
            {
                for (var x = 0; x < 9; x++)
                for (var y = 0; y < 4; y++)
                {
                    e.Target.Armor[x, y] = 0f;
                    e.Target.MaxArmor[x, y] = 20f;
                }
                if (!mirrored) { e.Target.Armor[3, 1] = 5f; e.Target.Armor[4, 1] = 15f; e.Target.Armor[5, 1] = 2f; }
                else { e.Target.Armor[4, 1] = 15f; e.Target.Armor[5, 1] = 5f; e.Target.Armor[3, 1] = 2f; }
                bar.EquippableItem.Durability = itemDurability;
                e.Markers[0].EquippableItem.Durability = 1000f;
                e.Markers[1].EquippableItem.Durability = 1000f;
                e.Target.GearOccupancy[0, 0].EquippableItem.Durability = 0f; // remove the bystander Gun's own 1 durability
            }

            ShotOutcome outcome = null;
            for (var attempt = 0; attempt < 600 && (outcome == null || !outcome.Hit || outcome.Cell.x != 4); attempt++)
            {
                ResetState();
                outcome = FireUntilHit(e, damageOverride: 90f, attempts: 1);
            }
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit && outcome.Cell.x == 4, "fixture: needed a hit on the true centre column within the attempt budget");

            var l0 = 1000f - e.Markers[0].EquippableItem.Durability;
            var l1 = 1000f - e.Markers[1].EquippableItem.Durability;
            return l0 <= l1 ? (l0, l1) : (l1, l0);
        }

        var a = RunFixture(mirrored: false);
        var b = RunFixture(mirrored: true);

        Assert.Equal(a.low, b.low, 2);
        Assert.Equal(a.high, b.high, 2);
    }

    // F8 (Soul's owed test, "Owed with the pending ruling's batch" in docs/fire-control-cut.md, Cut 12.3
    // status): many axis-aligned spread-n shots on a wide hull, each must strike exactly 2n+1 facing cells --
    // no column dropped. This is a standing regression guard on the lane-spacing invariant itself (independent
    // of the proportional-absorption fix batch's own item coordination), pinned through the real
    // Fire/Commit/Apply pipeline. Hand-confirmed to fail with R1b (lane spacing narrowed by 1%,
    // `laneSpacing = (shadowExtent.Hi - shadowExtent.Lo) * .99f` at FireControl.cs) because that narrowing
    // eventually drops a column at a spacing-boundary lateral -- see this cut's own report for the exact
    // FireControl.cs edit and count.
    [Fact]
    public void EveryAxisAlignedSpreadShotStrikesExactlyTwoNPlusOneFacingCells()
    {
        var e = Build(TestSettings(), SolidShape(15, 2), precision: .02f, penetration: 0f, damageSpread: 3f); // n=3 -> 7 lanes
        for (var x = 0; x < 15; x++)
        for (var y = 0; y < 2; y++)
        {
            e.Target.Armor[x, y] = 1000f;
            e.Target.MaxArmor[x, y] = 1000f;
        }

        var got = 0;
        var shortfalls = 0;
        for (var attempt = 0; attempt < 8000 && got < 400; attempt++)
        {
            var hits = new List<int2>();
            using var s = e.Target.ArmorDamage.Subscribe(x => hits.Add(x.pos));
            var outcome = FireUntilHit(e, damageOverride: 70f, attempts: 1);
            if (outcome == null || !outcome.Hit) continue;
            // Only impacts safely inside the hull count: within 3 columns of either edge, a side lane
            // legitimately runs off the hull (fewer than 7 facing columns is correct there, not a spacing
            // defect) -- restricting to the interior keeps this test about the spacing invariant alone.
            if (outcome.Cell.x < 3 || outcome.Cell.x > 11) continue;
            got++;
            if (hits.Select(h => h.x).Distinct().Count() != 7) shortfalls++; // 2*3+1 facing columns, one hit each
        }
        Assert.True(got >= 400, $"fixture: needed 400 interior-column hits, got {got}");
        Assert.Equal(0, shortfalls);
    }

    // ===================== F8: the model sweep, extended with the proportional rule =====================
    // Ports SoulApply123b's own independent model (armour, then item, then hull, near-to-far per lane,
    // ModelLane above -- the exact double-precision ray/box walk F1's own DirectHitAtAnAngledBearingMatches...
    // test already uses) and extends it with the proportional multi-lane item rule this cut adds: an item
    // touched by more than one lane absorbs their SUMMED post-armour incoming (up to its own durability), each
    // lane's leftover in proportion to what it brought. Structured the same way as FireControl.Apply's own new
    // code (a survey of which items are geometrically shared, then an AdvanceLane/Resolve loop) -- not because
    // a model must mirror production, but because it is the simplest correct way to get an item's resolution
    // order-independent of which lane the loop visits first, which is exactly the property under test. Compares
    // against production Apply through Fire/Commit/Apply (no reflection), over a sweep of shapes, bearings
    // (axis and angled) and item shapes (2x1 bars and non-convex L-shaped items, LShape above).
    private static (double[,] armor, Dictionary<EquippedItem, double> items, double hull) ModelWithSharedItems(
        Entity target, Shape shape, double[,] armorBefore, Dictionary<EquippedItem, double> itemsBefore,
        double bx, double by, double s, double damage, double penetration, int n)
    {
        var armor = (double[,]) armorBefore.Clone();
        var items = new Dictionary<EquippedItem, double>(itemsBefore);
        var laneSpacing = Math.Abs(bx) + Math.Abs(by);
        var laneCount = 2 * n + 1;
        var lanes = new List<MCell>[laneCount];
        for (var li = 0; li < laneCount; li++) lanes[li] = ModelLane(shape, bx, by, s + (li - n) * laneSpacing);
        var metal = lanes.Count(l => l.Count > 0);
        var per = damage / metal;

        var pointer = new int[laneCount];
        var rem = new double[laneCount];
        var finished = new bool[laneCount];
        var blocked = new bool[laneCount];
        for (var li = 0; li < laneCount; li++) { finished[li] = lanes[li].Count == 0; rem[li] = per; }

        // Survey: which items are geometrically reachable by more than one distinct lane, independent of any
        // remaining damage -- exactly the question FireControl.Apply's own survey asks.
        var lanesOf = new Dictionary<EquippedItem, List<int>>();
        for (var li = 0; li < laneCount; li++)
        {
            if (lanes[li].Count == 0) continue;
            var en0 = lanes[li][0].En;
            for (var i = 0; i < lanes[li].Count; i++)
            {
                if (i > 0 && lanes[li][i].En - en0 >= penetration) break;
                var item = target.GearOccupancy[lanes[li][i].C.x, lanes[li][i].C.y];
                if (item == null) continue;
                if (!lanesOf.TryGetValue(item, out var list)) lanesOf[item] = list = new List<int>();
                if (!list.Contains(li)) list.Add(li);
            }
        }
        var sharedSet = new HashSet<EquippedItem>(lanesOf.Where(kv => kv.Value.Count > 1).Select(kv => kv.Key));
        var remainingContributors = sharedSet.ToDictionary(it => it, it => lanesOf[it].Count);
        var pool = sharedSet.ToDictionary(it => it, it => new List<(int lane, double amount)>());
        var resolved = new HashSet<EquippedItem>();
        var hull = 0.0;

        void FinishLane(int li) { finished[li] = true; hull += rem[li]; }

        void Resolve(EquippedItem item)
        {
            var contributions = pool[item];
            var total = contributions.Sum(c => c.amount);
            var before = items[item];
            var absorbed = Math.Min(total, before);
            items[item] = Math.Max(before - absorbed, 0);
            var fraction = total > 0 ? absorbed / total : 0;
            foreach (var (lane, amount) in contributions) rem[lane] = amount - amount * fraction;
            resolved.Add(item);
            foreach (var (lane, _) in contributions)
            {
                blocked[lane] = false;
                pointer[lane]++;
                if (pointer[lane] >= lanes[lane].Count) FinishLane(lane);
            }
        }

        bool AdvanceLane(int li)
        {
            var moved = false;
            while (!finished[li] && !blocked[li])
            {
                var i = pointer[li];
                if (i > 0 && lanes[li][i].En - lanes[li][0].En >= penetration) { FinishLane(li); moved = true; break; }
                var c = lanes[li][i].C;
                if (rem[li] > 0) { var a = armor[c.x, c.y]; armor[c.x, c.y] = Math.Max(a - rem[li], 0); rem[li] = Math.Max(rem[li] - a, 0); }
                moved = true;
                var item = target.GearOccupancy[c.x, c.y];
                if (item != null && rem[li] > .1 && sharedSet.Contains(item) && !resolved.Contains(item))
                {
                    pool[item].Add((li, rem[li]));
                    remainingContributors[item]--;
                    blocked[li] = true;
                    if (remainingContributors[item] == 0) Resolve(item);
                    break;
                }
                if (item != null && rem[li] > .1)
                {
                    var d = items[item];
                    items[item] = Math.Max(d - rem[li], 0);
                    rem[li] = Math.Max(rem[li] - d, 0);
                }
                pointer[li]++;
                if (pointer[li] >= lanes[li].Count) { FinishLane(li); break; }
            }
            return moved;
        }

        bool AllFinished() { for (var li = 0; li < laneCount; li++) if (!finished[li]) return false; return true; }

        while (!AllFinished())
        {
            var progressed = false;
            for (var li = 0; li < laneCount; li++)
                if (!finished[li] && !blocked[li] && AdvanceLane(li)) progressed = true;
            if (!progressed)
            {
                // Deadlock fallback (the "opposite order" case, docs/fire-control-cut.md Cut 12.3 status): not
                // exercised by this sweep's own single-shared-item fixtures, kept only for parity with
                // production's own tie-break so an unexpectedly non-convex draw cannot hang the model.
                EquippedItem victim = null;
                foreach (var item in sharedSet)
                    if (!resolved.Contains(item) && pool[item].Count > 0) { victim = item; break; }
                if (victim == null) break;
                Resolve(victim);
            }
        }

        return (armor, items, hull);
    }

    [Fact]
    public void ModelSweepAgreesWithProductionIncludingSharedMultiCellItems()
    {
        var rng = new System.Random(20260925);
        // Height 4: an L-shaped item's 2x2 bounding box needs TWO consecutive interior rows (rows 1 and 2 for
        // height 4 -- Entity.ItemFits requires every one of an item's own cells to be an interior cell, the
        // same constraint RunSharedItemCase's own comment explains); a plain 2x1 bar only needs one but is kept
        // on the same row for a uniform itemY across both item shapes.
        var shapes = new[] { SolidShape(9, 4), SolidShape(7, 4) };
        var bearings = new[] { float2(0, 1), normalize(float2(1, 1)), normalize(float2(2, 1)) };
        var trials = 0;

        foreach (var shape in shapes)
        foreach (var travelDirection in bearings)
        {
            for (var t = 0; t < 15; t++)
            {
                var useLBar = t % 2 == 0;
                var itemX = 1 + rng.Next(shape.Width - 3);
                const int itemY = 1; // the only row that is interior with room for a 2-row-tall L above it (height 4)
                var durability = (float) (5 + rng.NextDouble() * 20);
                var e = useLBar
                    ? Build(TestSettings(), shape, precision: 1f, penetration: 3f, damageSpread: 2f,
                        lBars: new[] { (new int2(itemX, itemY), durability) })
                    : Build(TestSettings(), shape, precision: 1f, penetration: 3f, damageSpread: 2f,
                        bars: new[] { (new int2(itemX, itemY), durability) });
                e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;

                foreach (var c in shape.Coordinates)
                {
                    var a = (float) Math.Round(rng.NextDouble() * 6, 2);
                    e.Target.Armor[c.x, c.y] = a; e.Target.MaxArmor[c.x, c.y] = Math.Max(a, 1f);
                }

                var armorBefore = ToD(e.Target.Armor);
                var itemsBefore = e.Target.Equipment.Where(x => x.EquippableItem != e.Target.Hull)
                    .ToDictionary(x => x, x => (double) x.EquippableItem.Durability);

                ShotOutcome outcome = null;
                for (var attempt = 0; attempt < 400 && (outcome == null || !outcome.Hit); attempt++)
                    outcome = FireUntilHit(e, damageOverride: 40f, attempts: 1);
                if (outcome == null || !outcome.Hit) continue; // fixture: rare miss exhaustion, skip this draw
                trials++;

                const int n = 2; // damageSpread 2 -> floor(2.5) = 2
                var model = ModelWithSharedItems(e.Target, shape, armorBefore, itemsBefore,
                    outcome.Bearing.x, outcome.Bearing.y, outcome.Lateral, 40, 3, n);

                // Absolute tolerance rather than a fixed decimal-place rounding: the model runs in double and
                // production in float, so a value that lands within a float ULP or two of a rounding boundary
                // (e.g. 1.54999995 vs 1.55000019) can round to different digits at 1-2 decimal places despite
                // agreeing far more closely than that -- exactly the kind of float-vs-double drift F1's own
                // DirectHitAtAnAngledBearingMatchesAnIndependentModel test already tolerates with its own `, 2`.
                foreach (var c in shape.Coordinates)
                    Assert.True(Math.Abs(model.armor[c.x, c.y] - e.Target.Armor[c.x, c.y]) < .1,
                        $"armor{c}: model {model.armor[c.x, c.y]:F4} production {e.Target.Armor[c.x, c.y]:F4}");
                foreach (var kv in model.items)
                    Assert.True(Math.Abs(kv.Value - (double) kv.Key.EquippableItem.Durability) < .1,
                        $"item#{kv.Key.GetHashCode()}: model {kv.Value:F4} production {kv.Key.EquippableItem.Durability:F4}");
            }
        }
        Assert.True(trials >= 80, $"fixture: needed at least 80 of the 90 possible resolved trials, got {trials}"); // 2 shapes * 3 bearings * 15 draws
    }
}
