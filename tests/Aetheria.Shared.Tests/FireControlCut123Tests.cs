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
    private Engagement Build(
        GameplaySettings settings, Shape hullShape, float precision,
        float penetration = 0, float damageSpread = 0, float2? targetFacing = null,
        (int2 Cell, float Durability)[] markers = null, int2? cockpitCell = null, float cockpitDurability = 40)
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
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 24; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
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

    // No cell is struck by two lanes of one shot: heavy uniform armour so every lane's own impact cell fires
    // exactly one ArmorDamage event if reached, and a cell reached by two lanes would fire twice. Reverting the
    // lane spacing to one cell (Apply's pre-fix-batch spelling) fails this at both bearings, because a 45-degree
    // (or |bx| > |by|) shadow is wider than one cell and adjacent one-cell-spaced lanes then share cells.
    [Fact]
    public void NoCellIsStruckByTwoLanesAtAnAngledBearing()
    {
        void Check(float2 travelDirection)
        {
            var shape = SolidShape(9, 9);
            var e = Build(TestSettings(), shape, precision: 1f, penetration: 0f, damageSpread: 2f); // 5 lanes
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;
            foreach (var c in shape.Coordinates) { e.Target.Armor[c.x, c.y] = 1000f; e.Target.MaxArmor[c.x, c.y] = 1000f; }

            var hits = new List<int2>();
            using var s = e.Target.ArmorDamage.Subscribe(x => hits.Add(x.pos));
            var outcome = FireUntilHit(e, damageOverride: 50f);
            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);

            Assert.True(hits.Count > 1, $"expected several lanes to find metal, got {hits.Count}");
            Assert.Equal(hits.Count, hits.Distinct().Count()); // no cell fires ArmorDamage twice
        }

        Check(normalize(float2(1, 1))); // 45 degrees
        Check(normalize(float2(2, 1))); // |bx| > |by|
    }

    // Mirror-image shots (opposite bearing across the hull's own vertical symmetry axis) deliver, on average,
    // the same total hull damage. Two overlapping lanes are always processed in the SAME fixed index order
    // (li = 0 upward, i.e. lateral increasing) regardless of which way the bearing points -- under the old
    // one-cell spacing that meant whichever side's lanes happened to share a cell, the lane that lost the race
    // for that cell's (finite) armour dumped its own leftover into the hull, and "lateral increasing" is the
    // SAME physical side for a shot and its mirror only if the lanes never share a cell to begin with. Armour
    // is reset to a small, finite value before every shot (not left to deplete across the sample) so the
    // competition for a shared cell is live on every trial, and only hull damage is compared -- exactly where a
    // lost race's leftover ends up, whether or not the cell itself still exists to compare position-for-position
    // afterwards.
    [Fact]
    public void MirrorImageShotsProduceStatisticallyMirroredHullDamage()
    {
        var shape = SolidShape(9, 9);
        var b = normalize(float2(1, 1));
        var bm = float2(-b.x, b.y); // mirrored across the hull's own vertical (x) symmetry axis
        const int hitsWanted = 400;

        double SampleHullTotal(float2 travelDirection)
        {
            var e = Build(TestSettings(), shape, precision: 1f, penetration: 0f, damageSpread: 2f);
            e.Shooter.Position = e.Target.Position - float3(travelDirection.x, 0, travelDirection.y) * 100f;

            double hullTotal = 0;
            using var s = e.Target.HullDamage.Subscribe(x => hullTotal += x);
            var got = 0;
            for (var attempt = 0; attempt < 4000 && got < hitsWanted; attempt++)
            {
                foreach (var c in shape.Coordinates) { e.Target.Armor[c.x, c.y] = 8f; e.Target.MaxArmor[c.x, c.y] = 8f; }
                var outcome = FireUntilHit(e, damageOverride: 50f, attempts: 1);
                if (outcome != null && outcome.Hit) got++;
            }
            Assert.True(got >= hitsWanted, $"fixture: needed {hitsWanted} hits, got {got}");
            return hullTotal;
        }

        var hullA = SampleHullTotal(b);
        var hullM = SampleHullTotal(bm);

        Assert.True(Math.Abs(hullA - hullM) < 0.15 * Math.Max(hullA, hullM),
            $"mirror shots should deliver the same total hull damage on average: {hullA} vs {hullM}");
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
}
