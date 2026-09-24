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

// Cut 12.2 (docs/fire-control-cut.md, "Where a hit lands"): a hit's placement is priced and drawn from one
// silhouette, read at the bearing the target's facing implies AT COMMIT, not at fire. Builds its own
// fixtures, the convention every cut's test file follows. Fixtures follow 11.3: the shooter sits off the
// origin, fire times are nonzero, facings include ones where |fx| > |fy|, and weapon spread is nonzero
// wherever PSpread is asserted.
public sealed class FireControlCut12Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut12-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut12Tests() => Directory.CreateDirectory(_root);

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
    }

    // A shooter off the origin (Cut 11.3), a target over a caller-supplied hull shape, a full-strength
    // targeting system (Accuracy 1, Resolution huge, Tracking huge) so only the geometry under test moves the
    // number, and an optional shield. markerCells equips the catalog's bare "Marker" Tool stub at each given
    // interior cell -- returned in the same order -- for tests that need to name a specific cell (bow, stern).
    private Engagement Build(
        GameplaySettings settings, Shape hullShape, float precision,
        float velocity = 0, float spread = 0, float2? targetFacing = null,
        bool equipShield = false, float shieldCapacity = 30,
        int2[] markerCells = null)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 100000, Mass = 1000, Armor = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        // The shooter's own hull is a fixed, always-roomy shape -- distinct from the target's caller-supplied
        // hullShape, which some tests (BroadsideIsEasierThanHeadOn's 2-wide hull, TheSigmaFloorHoldsAtHalfACell's
        // 1-wide lane) deliberately make too narrow to have any interior at all, so a Tool item (the shooter's
        // own Targeting system) could never fit on it.
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
                Velocity = Constant(velocity), Spread = Constant(spread), DamageSpread = Constant(0),
                Penetration = Constant(0), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
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
        cache.Upsert(new GearData
        {
            Name = "Marker", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 24; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Cut12-" + Guid.NewGuid().ToString("N"), Owner = null }, null);

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

        // A bystander item on the Hardpoint slot every hull shape has (position (0,0) needs no Shrink-interior,
        // unlike a Tool) -- Entity.Update's `foreach (var item in _orderedEquipment)` NREs if an entity was
        // never equipped with anything at all (_orderedEquipment stays null until TryEquip first assigns it),
        // the same reason FireControlCut9Tests.BuildLockScenario equips its own bystander with a token gun.
        var targetGun = Make("Gun", lot++, 1);
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        var markers = markerCells?.Select(cell =>
        {
            var m = Make("Marker", lot++, 1000000);
            Assert.True(target.TryEquip(m, cell), $"marker must fit at {cell}");
            return target.Equipment.Single(x => x.EquippableItem == m);
        }).ToArray();

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

        // Cut 11.3: the shooter sits off the origin -- a shooter at float3.zero makes `target - source` and
        // `target + source` the same vector, hiding a whole class of sign mutants.
        shooter.Position = float3(37, 0, -11);
        target.Position = float3(37, 0, 89); // 100 units straight ahead of the shooter
        if (targetFacing != null) target.Direction = targetFacing.Value;
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);
        if (equipShield)
        {
            target.Shield.Item.Enabled.Value = true;
            for (var i = 0; i < 20; i++) zone.Update(.1f); // charge from empty (Cut 11's own Charge() convention)
        }

        // Cut 11.3: fire times are nonzero -- warm up past a bare zone.Update(0f) so FireTime is never 0.
        zone.Update(1f);

        return new Engagement
        {
            Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon,
            HullData = hullData, Markers = markers
        };
    }

    private static List<ShotOutcome> FireMany(Engagement e, int count)
    {
        var outcomes = new List<ShotOutcome>();
        e.Zone.ShotResolved.Subscribe(o => outcomes.Add(o));
        for (var i = 0; i < count; i++)
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.01f);
        }
        return outcomes;
    }

    // Independent reimplementation of FireControl.Lane's slab test, from the spec's own formulas, over the
    // hull's public Shape.Coordinates -- used to check production's placement against an outcome's own frozen
    // Bearing/Lateral without calling FireControl.Lane itself.
    private static int2 NearestOccupiedCellAtLateral(HullData hull, float2 bearing, float lateral)
    {
        var ell = float2(-bearing.y, bearing.x);
        var h = (Math.Abs(ell.x) + Math.Abs(ell.y)) / 2f;
        int2? best = null;
        var bestEntry = float.PositiveInfinity;

        foreach (var c in hull.Shape.Coordinates)
        {
            var centre = dot((float2) c, ell);
            if (lateral < centre - h || lateral >= centre + h) continue;

            var cf = (float2) c;
            var entry = float.NegativeInfinity;
            var exit = float.PositiveInfinity;
            var valid = true;
            foreach (var axis in new[] { 0, 1 })
            {
                var bAxis = axis == 0 ? bearing.x : bearing.y;
                var cAxis = axis == 0 ? cf.x : cf.y;
                var val = axis == 0 ? lateral * ell.x : lateral * ell.y;
                var lo = cAxis - .5f;
                var hi = cAxis + .5f;
                if (Math.Abs(bAxis) < 1e-9f)
                {
                    if (val < lo || val > hi) valid = false;
                    continue;
                }
                var t1 = (lo - val) / bAxis;
                var t2 = (hi - val) / bAxis;
                entry = Math.Max(entry, Math.Min(t1, t2));
                exit = Math.Min(exit, Math.Max(t1, t2));
            }
            if (!valid || entry > exit) continue;

            if (entry < bestEntry)
            {
                bestEntry = entry;
                best = c;
            }
        }

        Assert.True(best != null, "fixture: no occupied cell reached at this lateral offset");
        return best.Value;
    }

    // BroadsideIsEasierThanHeadOn: kills the bearing being ignored and the old max(W,H) bound. A 2x12 hull is
    // much easier to hit broadside (its long axis facing the shot) than head-on (its short axis facing it).
    [Fact]
    public void BroadsideIsEasierThanHeadOn()
    {
        float PAt(float2 facing)
        {
            var e = Build(TestSettings(), SolidShape(2, 12), precision: .5f, spread: 4, targetFacing: facing);
            return FireControl.Inspect(e.Weapon, e.Shooter, e.Target).PSpread;
        }

        float POnHullAt(float2 facing)
        {
            var e = Build(TestSettings(), SolidShape(2, 12), precision: .5f, targetFacing: facing);
            return FireControl.Inspect(e.Weapon, e.Shooter, e.Target).POnHull;
        }

        // The shooter fires from world +z toward the target (TravelDirection ~ world +z, since the target sits
        // straight ahead). Facing (0,1) (bow toward the shooter, forward parallel to travel) puts the ship's
        // SHORT axis (width 2) across the bearing -- head-on. Facing (1,0) puts its LONG axis (height 12)
        // across the bearing -- broadside.
        var headOnPSpread = PAt(float2(0, 1));
        var broadsidePSpread = PAt(float2(1, 0));
        Assert.True(broadsidePSpread > headOnPSpread,
            $"broadside PSpread ({broadsidePSpread}) must exceed head-on PSpread ({headOnPSpread})");

        var headOnPOnHull = POnHullAt(float2(0, 1));
        var broadsidePOnHull = POnHullAt(float2(1, 0));
        Assert.True(broadsidePOnHull > headOnPOnHull,
            $"broadside pOnHull ({broadsidePOnHull}) must exceed head-on pOnHull ({headOnPOnHull})");
    }

    // TurningArmourIntoTheShotTakesItOnTheArmour (the ruling's own case): a slow shot at a target whose stern
    // is exposed at Fire. The target turns its armoured bow to face the incoming shot (present the bow toward
    // where the shot is travelling FROM) before the shot commits. Every hit must land on the bow row --
    // Bearing timing reads the facing live, at Commit, not frozen at Fire.
    [Fact]
    public void TurningArmourIntoTheShotTakesItOnTheArmour()
    {
        var settings = TestSettings();
        // Flight time well above CommitHorizon (.5s): range 100 at velocity 20 -> 5s flight, commits at 4.5s.
        var e = Build(settings, SolidShape(3, 11), precision: .4f, velocity: 20, targetFacing: null);

        var outcomes = new List<ShotOutcome>();
        using var r = e.Zone.ShotResolved.Subscribe(outcomes.Add);

        // Repeat the same fire-then-turn sequence several times (the roll can still miss outright) and check
        // the set of hits, not one shot.
        for (var i = 0; i < 60; i++)
        {
            var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
            var travelDirection = pending.TravelDirection;

            // At Fire: bow faces the same way the shot travels -- the stern is exposed to the incoming shot.
            e.Target.Direction = travelDirection;
            e.Zone.Update(1f); // short of commit (4.5s)

            // Before commit: turn the bow to face the incoming shot head-on.
            e.Target.Direction = -travelDirection;
            e.Zone.Update(4f); // past commit (4.5s), short of arrival (5s)
            e.Zone.Update(2f); // past arrival
        }

        var hits = outcomes.Where(o => o.Hit).ToList();
        Assert.True(hits.Count > 10, $"expected a healthy number of hits to check, got {hits.Count}");
        foreach (var outcome in hits)
            Assert.True(outcome.Cell.y >= 5.5f, $"expected every hit to land on the bow row (y>=5.5), got cell {outcome.Cell}");
    }

    // TurningAfterCommitChangesNothing (R4, closes L464/L465): after commit, turning the target and moving the
    // shooter must not change where the damage actually lands -- Apply reads no live position or facing. The
    // armor cell that actually takes damage is checked against an independent lane recompute from the
    // committed Bearing/Lateral, not from the post-turn facing.
    [Fact]
    public void TurningAfterCommitChangesNothing()
    {
        var settings = TestSettings();
        var e = Build(settings, SolidShape(7, 7), precision: .5f, velocity: 20); // 5s flight, commits at 4.5s

        ShotOutcome committed = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);
        // The roll can still miss outright -- retry (a fresh shot each time; the previous miss already
        // resolved and left the pending queue empty) until one hits.
        for (var attempt = 0; attempt < 30 && (committed == null || !committed.Hit); attempt++)
        {
            committed = null;
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(4.6f); // past commit (4.5s), short of arrival (5s)
        }
        Assert.NotNull(committed);
        Assert.True(committed.Hit, "precondition: this fixture's shot must hit");

        var expectedCell = NearestOccupiedCellAtLateral(e.HullData, committed.Bearing, committed.Lateral);
        Assert.Equal(expectedCell, committed.Cell); // sanity: production's own Lane agrees with the independent recompute

        var before = (float[,]) e.Target.Armor.Clone();

        // After commit, before arrival: the target turns 90 degrees and the shooter moves.
        e.Target.Direction = float2(1, 0);
        e.Shooter.Position += float3(500, 0, 500);

        e.Zone.Update(2f); // past arrival (5s)

        Assert.True(e.Target.Armor[expectedCell.x, expectedCell.y] < before[expectedCell.x, expectedCell.y],
            "the committed impact cell must be the one that actually took damage, regardless of the post-commit turn");
    }

    // PlacementAndProbabilityShareOneSilhouette replaces 6d's PlacementAndProbabilityShareOneKernel: pins that
    // CommitProbability's number and Commit's own placement are read from the exact same Silhouette. A
    // diagonal bearing (|fx| ~ |fy|) makes individual cells' raw shadow intervals overlap heavily before
    // merging, so a skipped merge (double-counted mass) or a swapped ell/b would show up here.
    [Fact]
    public void PlacementAndProbabilityShareOneSilhouette()
    {
        var settings = TestSettings();
        const float precision = .45f;
        var hullShape = SolidShape(7, 9);
        // Aim off the hull's own centre of mass (near a corner) so the Gaussian's mass is not symmetric
        // inside the shadow -- a perturbed sigma or a mis-signed ell moves the analytic and reported numbers
        // apart even though both would otherwise report something close to 1.
        var aim = new int2(1, 1);
        var e = Build(settings, hullShape, precision, spread: 0, targetFacing: normalize(float2(1f, 1f)),
            markerCells: new[] { aim });
        Assert.True(e.Shooter.TrySelectTargetItem(e.Markers[0]));

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        Assert.Same(e.Markers[0], pending.Aimed);

        var bearing = normalize(e.Target.ToSchematic(pending.TravelDirection));
        var ell = float2(-bearing.y, bearing.x);
        var sigma = Math.Max(1f / precision, .5f);
        var aimPoint = (float2) aim; // ResolveAimPoint on a single-cell marker: that cell itself
        var a = dot(aimPoint, ell);

        // Independent shadow: raw per-cell intervals from the hull's own public Shape.Coordinates, sorted and
        // merged by this test, not by calling FireControl.Silhouette.
        var raw = e.HullData.Shape.Coordinates
            .Select(c => dot((float2) c, ell))
            .Select(centre => (Lo: centre - (Math.Abs(ell.x) + Math.Abs(ell.y)) / 2f, Hi: centre + (Math.Abs(ell.x) + Math.Abs(ell.y)) / 2f))
            .OrderBy(iv => iv.Lo)
            .ToList();
        var merged = new List<(float Lo, float Hi)>();
        foreach (var iv in raw)
        {
            if (merged.Count > 0 && iv.Lo <= merged[merged.Count - 1].Hi)
                merged[merged.Count - 1] = (merged[merged.Count - 1].Lo, Math.Max(merged[merged.Count - 1].Hi, iv.Hi));
            else
                merged.Add(iv);
        }

        // Numeric integral of the Gaussian pdf over the merged union -- independent of erf, and of any private
        // FireControl helper.
        double Mass((float Lo, float Hi) iv)
        {
            const int steps = 4000;
            var width = (iv.Hi - iv.Lo) / steps;
            if (width <= 0) return 0;
            double sum = 0;
            for (var i = 0; i <= steps; i++)
            {
                var x = iv.Lo + i * width;
                var z = (x - a) / sigma;
                var pdf = Math.Exp(-z * z / 2.0) / (sigma * Math.Sqrt(2.0 * Math.PI));
                sum += pdf * (i == 0 || i == steps ? .5 : 1.0);
            }
            return sum * width;
        }
        var expectedPOnHull = merged.Sum(Mass);
        Assert.True(expectedPOnHull > .05 && expectedPOnHull < .95, $"fixture: pOnHull ({expectedPOnHull}) must be non-degenerate to exercise the merge, got a saturated or collapsed value");

        var commitP = FireControl.CommitProbability(pending, e.Zone.Time, out var sil);
        // Fixture isolation: PFire = Accuracy(1) x PSensor(1, full reveal) = 1; pDeviation = 1 (target hasn't
        // moved since FireTargetPosition was frozen); PSpread = 1 (Spread 0's own early-out). So the reported
        // commit price collapses to exactly pOnHull, the number this test computed independently.
        Assert.Equal((float) expectedPOnHull, commitP, 3);
        Assert.Equal((float) expectedPOnHull, sil.POnHull, 3);

        // Empirical: the hit rate and the per-interval Lateral shares both match the analytic union.
        const int shots = 4000;
        var outcomes = FireMany(e, shots);
        var hits = outcomes.Where(o => o.Hit).ToList();
        Assert.True(hits.Count > shots * expectedPOnHull * .5, $"expected roughly {shots * expectedPOnHull:0} hits, got {hits.Count}");
        Assert.True(Math.Abs(hits.Count / (double) shots - expectedPOnHull) < .05, $"empirical hit rate {hits.Count / (double) shots:F3} does not track analytic pOnHull {expectedPOnHull:F3}");

        foreach (var (lo, hi) in merged)
        {
            var expectedShare = Mass((lo, hi)) / expectedPOnHull;
            if (expectedShare < .05) continue; // too little mass to measure reliably over 4000 shots
            var empiricalShare = hits.Count(o => o.Lateral >= lo - 1e-3f && o.Lateral < hi + 1e-3f) / (double) hits.Count;
            Assert.True(Math.Abs(expectedShare - empiricalShare) < .1, $"interval [{lo:F2},{hi:F2}): expected share {expectedShare:F3}, empirical {empiricalShare:F3}");
        }
    }

    // HitsLandOnTheFacingEdge supersedes EveryHitLandsOnMetal: on a concave (holed) hull, every hit's Cell is
    // occupied, AND nothing occupied precedes it in its own lane -- the test recomputes the lane from the
    // outcome's own frozen Bearing/Lateral, independently of FireControl.Lane, and checks production picked
    // the nearest cell, not merely an occupied one.
    [Fact]
    public void HitsLandOnTheFacingEdge()
    {
        var shape = new Shape(7, 7);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        shape[new int2(3, 3)] = false; // the hole

        var aim = new int2(1, 1);
        var e = Build(TestSettings(), shape, precision: .3f, targetFacing: normalize(float2(.8f, .6f)), markerCells: new[] { aim });
        Assert.True(e.Shooter.TrySelectTargetItem(e.Markers[0]));

        var outcomes = FireMany(e, 1500);
        var hits = outcomes.Where(o => o.Hit).ToList();
        Assert.True(hits.Count > 100, $"expected a healthy number of hits to check, got {hits.Count}");

        foreach (var outcome in hits)
        {
            Assert.True(shape[outcome.Cell], $"hit landed on {outcome.Cell}, not part of the hull's own schematic");
            var expected = NearestOccupiedCellAtLateral(e.HullData, outcome.Bearing, outcome.Lateral);
            Assert.Equal(expected, outcome.Cell);
        }
    }

    // AimingAtAnItemCentresTheScatterOnItsLane closes L585/L587 and replaces AimingAtTheSternHitsTheStern,
    // whose fixture fired from the target's own stern and would pass for the wrong reason (any placement rule
    // that merely biases toward whatever is nearest the shooter, rather than toward the aim point specifically,
    // would also pass a stern-fired, stern-aimed shot). From broadside, hits aimed at the stern land mostly in
    // the stern half and hits aimed at the bow mostly in the bow half; from dead astern, hits aimed at the bow
    // land on the stern edge (the near edge, since the aim point pulls the group toward the bow but every cell
    // between the stern edge and the bow marker is on the lane first).
    [Fact]
    public void AimingAtAnItemCentresTheScatterOnItsLane()
    {
        var settings = TestSettings();
        var hullShape = SolidShape(3, 11);
        var bow = new int2(1, 9);   // y=9: firmly in the bow half (y>=5.5)
        var stern = new int2(1, 1); // y=1: firmly in the stern half

        double SternFraction(float2 facing, int2 aimCell)
        {
            var e = Build(settings, hullShape, precision: .6f, targetFacing: facing, markerCells: new[] { aimCell });
            var aimed = e.Markers[0];
            Assert.True(e.Shooter.TrySelectTargetItem(aimed));
            Assert.Same(aimed, e.Shooter.ResolvedTargetItem);

            var outcomes = FireMany(e, 600);
            var hits = outcomes.Where(o => o.Hit).ToList();
            Assert.True(hits.Count > 100, $"expected a healthy number of hits, got {hits.Count}");
            return hits.Count(o => o.Cell.y < 5.5f) / (double) hits.Count;
        }

        // Broadside: forward perpendicular to the shot's travel direction (world ~+z), so both bow and stern
        // are roughly equidistant from the shooter and the aim point, not the entry side, drives the scatter.
        var sternAimedBroadside = SternFraction(float2(1, 0), stern);
        var bowAimedBroadside = SternFraction(float2(1, 0), bow);
        Assert.True(sternAimedBroadside > .7, $"broadside, aimed at the stern: expected mostly stern hits, got sternFraction={sternAimedBroadside}");
        Assert.True(bowAimedBroadside < .3, $"broadside, aimed at the bow: expected mostly bow hits, got sternFraction={bowAimedBroadside}");

        // Dead astern: forward parallel to the shot's travel direction (bow points away from the shooter), so
        // the shot enters through the stern regardless of aim -- aiming at the bow still lands on the near
        // (stern) edge, because the stern cells are on the lane first.
        var bowAimedAstern = SternFraction(float2(0, 1), bow);
        Assert.True(bowAimedAstern > .7, $"dead astern, aimed at the bow: expected the near (stern) edge to still take the hits, got sternFraction={bowAimedAstern}");
    }

    // TheHudEstimateIsTheCommitPrice: for an in-flight shot, the value the HUD's own estimate (through
    // FireControl.CommitProbability, the function ActionGameManager's pending-shot line now calls) and the p
    // Commit itself rolls against are the same number at the commit tick. Kills a second formula.
    [Fact]
    public void TheHudEstimateIsTheCommitPrice()
    {
        var e = Build(TestSettings(), SolidShape(5, 7), precision: .5f, velocity: 20); // 5s flight
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(1f); // still short of the 4.5s commit

        var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        var hudEstimate = FireControl.CommitProbability(pending, e.Zone.Time, out _);

        ShotOutcome committed = null;
        var draws = 0;
        using var c = e.Zone.ShotCommitted.Subscribe(o => { committed = o; draws++; });
        e.Zone.Update(4f); // now past commit
        Assert.NotNull(committed);
        Assert.Equal(1, draws);

        // The commit tick's own price, recomputed with the pending shot as it stood before Commit mutated its
        // Committed flag -- CommitProbability is pure, so calling it again after the fact at the same `now`
        // reproduces the exact p Commit rolled against.
        var commitTickEstimate = FireControl.CommitProbability(pending, 1f + 4f, out _);
        Assert.Equal(hudEstimate, commitTickEstimate, 5); // the target never moved or turned between the two reads
    }

    // TheSigmaFloorHoldsAtHalfACell: pOnHull at Precision 1000 equals pOnHull at Precision 2 on a one-cell
    // lane. Cut 12.2's exact integral no longer collapses on its own (POnHullNeverCollapsesAboveThePrecisionCliff
    // no longer catches a deleted floor), so this is the test that kills "delete SigmaFloor" -- without the
    // floor, sigma at Precision 1000 would be 0.001, and the Gaussian would collapse almost entirely inside a
    // single cell's own width rather than sharing the whole one-cell lane the way Precision 2 (sigma .5,
    // already at the floor) does.
    [Fact]
    public void TheSigmaFloorHoldsAtHalfACell()
    {
        // A 1-wide, 9-tall hull: every occupied cell's shadow along a dead-ahead bearing collapses onto the
        // same single-cell-wide lane (ell has one axis zero, so h and the per-cell interval width degenerate
        // to the schematic's own cell width) -- "a one-cell lane" per the spec's own name for this test. A
        // 1-wide hull has no interior cell at all (Shrink needs a full ring), so this fires unaimed -- the
        // hull's own centre of mass, (0,4), is already the middle of the lane.
        var shape = SolidShape(1, 9);

        float POnHullAt(float precision)
        {
            var e = Build(TestSettings(), shape, precision);
            return FireControl.Inspect(e.Weapon, e.Shooter, e.Target).POnHull;
        }

        var at2 = POnHullAt(2f);
        var at1000 = POnHullAt(1000f);
        Assert.Equal(at2, at1000, 4);
    }

    // ShieldIsOmnidirectional: an active shield absorbs the same shot identically from bow, stern and beam --
    // the shield gate in Commit reads only DamageType/Damage, never the bearing this cut introduces.
    [Fact]
    public void ShieldIsOmnidirectional()
    {
        var settings = TestSettings();
        bool AbsorbsFrom(float2 facing)
        {
            var e = Build(settings, SolidShape(5, 5), precision: 1f, targetFacing: facing, equipShield: true, shieldCapacity: 1000);
            var before = e.Target.Hull.Durability;
            var outcomes = FireMany(e, 50);
            var hits = outcomes.Where(o => o.Hit).ToList();
            Assert.True(hits.Count > 5, $"expected a healthy number of hits, got {hits.Count}");
            Assert.True(hits.All(o => o.Shielded), "every hit must be absorbed by the shield regardless of facing");
            Assert.Equal(before, e.Target.Hull.Durability);
            return true;
        }

        Assert.True(AbsorbsFrom(float2(0, 1)));  // bow toward the shot
        Assert.True(AbsorbsFrom(float2(0, -1))); // stern toward the shot
        Assert.True(AbsorbsFrom(float2(1, 0)));  // beam
    }
}
