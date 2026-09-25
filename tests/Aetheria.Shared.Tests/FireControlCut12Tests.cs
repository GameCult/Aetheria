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

    // S1 fix batch: a non-convex hull for the empty-lane sweep -- a solid square with its centre cell punched
    // out, the same "holed7" shape Soul's own EdgeSweep probe used.
    private static Shape HoledShape(int w, int h)
    {
        var shape = SolidShape(w, h);
        shape[new int2(w / 2, h / 2)] = false;
        return shape;
    }

    // A unit direction at `d` degrees, matching Soul's own probe convention (SoulProbe122b.Deg) -- used here to
    // name an exact facing/bearing for a crash repro.
    private static float2 Deg(double d) => float2((float) Math.Sin(d * Math.PI / 180), (float) Math.Cos(d * Math.PI / 180));

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
        public EquippedItem BigMarker;
        public int2[] BigMarkerCells;
        public EquippedItem LMarker;
        public int2[] LMarkerCells;
    }

    // A shooter off the origin (Cut 11.3), a target over a caller-supplied hull shape, a full-strength
    // targeting system (Accuracy 1, Resolution huge, Tracking huge) so only the geometry under test moves the
    // number, and an optional shield. markerCells equips the catalog's bare "Marker" Tool stub at each given
    // interior cell -- returned in the same order -- for tests that need to name a specific cell (bow, stern).
    private Engagement Build(
        GameplaySettings settings, Shape hullShape, float precision,
        float velocity = 0, float spread = 0, float2? targetFacing = null,
        bool equipShield = false, float shieldCapacity = 30,
        int2[] markerCells = null, float accuracy = 1f, float penetration = 0, bool equipBigMarker = false,
        // S1 fix batch (Hands, 2026-09-25): a caller-chosen zone name -- Cut 6.1's own convention is one fixed
        // name so a fixture is replayable byte-for-byte, but Soul's own crash repros (Zone.CombatSeed is
        // StableHash(name)) each need their OWN exact name to reproduce a specific die roll.
        string zoneName = "Cut12",
        // S7 fix batch (Hands, 2026-09-25): Tracking authored through Build, like Precision and Accuracy already
        // are, instead of a caller patching PendingShot.Tracking after Fire (TheHudEstimateIsTheCommitPrice's own
        // former shape) -- one authoring path for every frozen shooter stat.
        float tracking = 100000f,
        // S8 fix batch (Hands, 2026-09-25): an L-shaped (3-cell, not 2x2) Tool item -- its true centroid differs
        // from both any single cell and the bounding-box centre a mutant could substitute (N11), which a
        // rectangular BigMarker cannot distinguish.
        bool equipLMarker = false)
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
                Accuracy = Constant(accuracy), Resolution = Constant(1000f), Precision = Constant(precision), Tracking = Constant(tracking)
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
        // F7 (Soul's fix batch, 2026-09-24): a 2x2 Tool item -- every "Marker" fixture elsewhere is the
        // catalog's 1x1 stub, where ResolveAimPoint's average (sum of cells / cell count) can't be
        // distinguished from summing then multiplying, or from just reading the first cell.
        var bigMarkerShape = new Shape(2, 2);
        foreach (var c in bigMarkerShape.AllCoordinates) bigMarkerShape[c] = true;
        cache.Upsert(new GearData
        {
            Name = "BigMarker", Hardpoint = HardpointType.Tool, Shape = bigMarkerShape, Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        // S8 fix batch: an L-tromino (3 of a 2x2's 4 cells) -- its centroid ((0,0)+(1,0)+(0,1))/3 = (1/3,1/3) is
        // neither any one of its own cells nor the bounding box's centre (.5,.5), so a fixture built from it can
        // tell a true centroid apart from both.
        var lMarkerShape = new Shape(2, 2) { [new int2(0, 0)] = true, [new int2(1, 0)] = true, [new int2(0, 1)] = true };
        cache.Upsert(new GearData
        {
            Name = "LMarker", Hardpoint = HardpointType.Tool, Shape = lMarkerShape, Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 24; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        // Cut 6.1's own convention (FireControlCut6Tests.cs:62-63): every engagement shares one fixed
        // GalaxyZone name, so every Zone this fixture builds shares one CombatSeed and every statistical test
        // is replayable byte-for-byte across runs. A GUID-suffixed name here (this file's own earlier defect)
        // reseeded the die on every run, which is exactly what made TurningArmourIntoTheShotTakesItOnTheArmour
        // flaky (F2, Soul's fix batch, 2026-09-24).
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = zoneName, Owner = null }, null);

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

        EquippedItem bigMarker = null;
        int2[] bigMarkerCells = null;
        if (equipBigMarker)
        {
            var bm = Make("BigMarker", lot++, 1000000);
            Assert.True(target.TryFindSpace(bm, out var bmPos));
            Assert.True(target.TryEquip(bm, bmPos));
            bigMarker = target.Equipment.Single(x => x.EquippableItem == bm);
            bigMarkerCells = hullShape.Coordinates.Where(v => target.GearOccupancy[v.x, v.y] == bigMarker).ToArray();
            Assert.Equal(4, bigMarkerCells.Length); // fixture precondition: the full 2x2 footprint landed
        }

        EquippedItem lMarker = null;
        int2[] lMarkerCells = null;
        if (equipLMarker)
        {
            var lm = Make("LMarker", lot++, 1000000);
            Assert.True(target.TryFindSpace(lm, out var lmPos));
            Assert.True(target.TryEquip(lm, lmPos));
            lMarker = target.Equipment.Single(x => x.EquippableItem == lm);
            lMarkerCells = hullShape.Coordinates.Where(v => target.GearOccupancy[v.x, v.y] == lMarker).ToArray();
            Assert.Equal(3, lMarkerCells.Length); // fixture precondition: the full 3-cell L footprint landed
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
            HullData = hullData, Markers = markers, BigMarker = bigMarker, BigMarkerCells = bigMarkerCells,
            LMarker = lMarker, LMarkerCells = lMarkerCells
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

    // Stryker survivors on this cut's own changed lines (2026-09-24 run): TravelDirection's point-blank
    // ternary (both directions), and Fire's target-null guard around it, were never actually exercised at a
    // shooter/target pair where the real direction could not coincidentally equal the (0,1) fallback. Kills:
    // forcing either ternary branch; skipping the null-target guard (which would NRE on PredictedIntercept).
    [Fact]
    public void TravelDirectionMatchesThePredictedInterceptAndFallsBackAtPointBlank()
    {
        var e = Build(TestSettings(), SolidShape(5, 5), precision: 1f);

        // A diagonal placement -- neither axis-aligned with the fallback (0,1), so a mutant that always
        // returns the fallback (or always skips the null-target guard) cannot pass by coincidence.
        e.Shooter.Position = float3(10, 0, -5);
        e.Target.Position = float3(40, 0, 65);
        var expected = normalize((e.Target.Position - e.Shooter.Position).xz);
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        Assert.Equal(expected.x, pending.TravelDirection.x, 4);
        Assert.Equal(expected.y, pending.TravelDirection.y, 4);

        // Point-blank: shooter and target at the exact same position (weapon Velocity 0, so PredictedIntercept
        // returns target.Position exactly) -- TravelDirection must fall back rather than normalize a zero
        // vector into NaN.
        e.Target.Position = e.Shooter.Position;
        var pointBlankId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var pointBlank = e.Zone.PendingShots.Single(s => s.ShotId == pointBlankId);
        Assert.Equal(0f, pointBlank.TravelDirection.x);
        Assert.Equal(1f, pointBlank.TravelDirection.y);

        // No target: Fire's own null-target branch must use the fallback directly, not call TravelDirection
        // (which reads target.Position and would NRE).
        e.Shooter.Target.Value = null;
        var noTargetId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var noTarget = e.Zone.PendingShots.Single(s => s.ShotId == noTargetId);
        Assert.Equal(0f, noTarget.TravelDirection.x);
        Assert.Equal(1f, noTarget.TravelDirection.y);
    }

    // Stryker survivor: diagnostic.PFire (Accuracy * PSensor, an optional presentation field) was never
    // checked at a PSensor strictly between 0 and 1, so Accuracy * PSensor and Accuracy / PSensor (the
    // mutant) were indistinguishable at PSensor == 1 (the fixture's usual full-reveal convention). Resolution
    // 1000 gives an razor-thin demand window (ceiling ~= .1009 against a .1 threshold); info exactly halfway
    // through it gives PSensor exactly .5.
    [Fact]
    public void DiagnosticPFireIsAccuracyTimesPSensor()
    {
        var e = Build(TestSettings(), SolidShape(6, 12), precision: .6f);
        e.Shooter.EntityInfoGathered[e.Target] = .10045f; // halfway through the [.1, .1009] demand window
        var d = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
        Assert.True(d.PSensor > .05f && d.PSensor < .95f, $"fixture: PSensor ({d.PSensor}) must be strictly between 0 and 1");
        Assert.Equal(d.Accuracy * d.PSensor, d.PFire, 4);
    }

    // Stryker survivors: CommitProbability's own product (PFire * pDeviation * PSpread * pOnHull) was only
    // ever exercised with pDeviation exactly 1 (a stationary target relative to its fire-time projection),
    // which makes "* pDeviation" and "/ pDeviation" (and boundary flips around 0) indistinguishable. A
    // synthetic PendingShot (bypassing Fire, the same convention FireControlCut9Tests.FireSynthetic uses)
    // pins every factor to a distinct, non-1 value and checks the exact product against an independent
    // recomputation.
    [Fact]
    public void CommitProbabilityIsTheExactProductOfItsFourFactors()
    {
        var e = Build(TestSettings(), SolidShape(7, 9), precision: .5f, spread: 6f);
        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);

        // A jink between fire and commit gives pDeviation a real, non-1 value distinct from PFire and PSpread.
        pending.FireTime = e.Zone.Time - 1f;
        pending.PFire = .8f;
        pending.Tracking = 20f;
        e.Zone.PendingShots[e.Zone.PendingShots.FindIndex(s => s.ShotId == shotId)] = pending;
        e.Target.Position += float3(6, 0, 0); // jink after the (frozen) fire-time projection

        var p = FireControl.CommitProbability(pending, e.Zone.Time, out var sil);

        var pDeviation = FireControl.DeviationProbability(pending, e.Zone.Time, out var deviation);
        Assert.True(deviation > 0f, "fixture: the jink must actually move the target off its projection");
        Assert.True(pDeviation > .05f && pDeviation < .95f, $"fixture: pDeviation ({pDeviation}) must be strictly between 0 and 1");
        Assert.True(sil.POnHull > .05f && sil.POnHull < .95f, $"fixture: pOnHull ({sil.POnHull}) must be strictly between 0 and 1");
        var pSpread = e.HullData.Shape.Width > 0
            ? Math.Min(1f, (float) (180.0 / Math.PI * Math.Atan(.5 * sil.Span / pending.FireRange)) / (pending.Spread / 2f))
            : 1f;
        var expected = pending.PFire * pDeviation * pSpread * sil.POnHull;
        Assert.Equal(expected, p, 4);
    }

    // S4 fix batch (Hands, 2026-09-25): F8 (Soul's earlier fix batch, 2026-09-24) moved range/flightTime
    // computation ahead of the visibility gate, so Fire's own flight time reflects the real distance even when
    // PFire prices the shot at 0 -- but nothing behavioural pinned that. N4 (Soul's mutation set: "range after
    // visibility gate", reverting F8) survived every existing test. A shot at a target the shooter cannot
    // currently see must still resolve, as a miss (PFire gates it to 0), at the arrival time the real
    // range/velocity imply -- not immediately (range 0) and not never (flight time 0 means instant resolve).
    [Fact]
    public void InvisibleTargetStillResolvesAtItsRealFlightTime()
    {
        var e = Build(TestSettings(), SolidShape(5, 5), precision: 1f, velocity: 20); // range 100 -> 5s flight
        e.Shooter.VisibleEntities.Remove(e.Target);

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        Assert.Equal(0f, pending.PFire); // fixture precondition: the visibility gate is actually closed
        var expectedArrival = pending.FireTime + 100f / 20f; // the real range (100) over the real velocity (20)
        Assert.Equal(expectedArrival, pending.ArrivalTime, 3);

        ShotOutcome outcome = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);
        e.Zone.Update(4.9f);
        Assert.Null(outcome); // not yet: N4's bug (range 0) would have resolved this instantly
        e.Zone.Update(.2f); // past the real 5s arrival
        Assert.NotNull(outcome);
        Assert.False(outcome.Hit, "fixture: PFire 0 (not visible) must resolve as a miss");
    }

    // Stryker survivors: ResolveAimPoint's fallback (aimed but currently occupying zero cells -> hull centre
    // of mass) and its multi-cell average were both untested -- every existing marker fixture uses the
    // catalog's 1x1 "Marker" stub, where sum/length and sum*length agree at length 1, and no fixture ever
    // destroys or unequips an aimed item before firing.
    [Fact]
    public void AimPointFallsBackWhenTheAimedItemNoLongerOccupiesAnyCell()
    {
        var aim = new int2(2, 2);
        var e = Build(TestSettings(), SolidShape(6, 6), precision: 1000f, markerCells: new[] { aim });
        var aimed = e.Markers[0];
        Assert.True(e.Shooter.TrySelectTargetItem(aimed));

        // The aimed item is still equipped (Entity.TryUnequip refuses on an active entity, and reveal/aim
        // selection reads Equipment, not GearOccupancy) but no longer occupies any hull cell -- the shape a
        // destroyed or relocated item's footprint would leave. CellsOf then returns an empty (non-null) array,
        // and ResolveAimPoint must fall back to the hull's own centre of mass rather than average zero cells
        // (0/0 = NaN).
        e.Target.GearOccupancy[aim.x, aim.y] = null;

        var outcomes = FireMany(e, 400);
        var hits = outcomes.Where(o => o.Hit).ToList();
        Assert.True(hits.Count > 50, $"expected a healthy number of hits, got {hits.Count}");
        foreach (var outcome in hits)
            Assert.True(e.HullData.Shape[outcome.Cell], $"hit landed on {outcome.Cell}, off the hull's own schematic -- the aim point fallback produced NaN");
    }

    // F7 (Soul's fix batch, 2026-09-24): ResolveAimPoint's average was only ever exercised with a 1x1 item
    // (every other fixture's "Marker"), where sum/length and sum*length agree and "first cell" and "average"
    // are the same cell. A 2x2 "BigMarker" gives a genuine 4-cell footprint: its true centroid is checked two
    // ways -- pOnHull does not collapse (the aim point is on the hull, not scaled off it by sum*length), and,
    // from broadside, the empirical mean lateral offset of hits centres on the real centroid's own lane, not
    // on any single one of its four cells.
    [Fact]
    public void AimingAtAnItemBiggerThanOneCellCentresOnItsCentroid()
    {
        var e = Build(TestSettings(), SolidShape(9, 9), precision: 1f, targetFacing: float2(1, 0), equipBigMarker: true);
        Assert.True(e.Shooter.TrySelectTargetItem(e.BigMarker));

        var travelDirection = FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target);
        var bearing = normalize(e.Target.ToSchematic(travelDirection));
        var ell = float2(-bearing.y, bearing.x);
        var centroid = e.BigMarkerCells.Aggregate(float2.zero, (t, c) => t + (float2) c) / e.BigMarkerCells.Length;
        var expectedA = dot(centroid, ell);

        // pOnHull must not collapse -- a sum*length aim point would be scaled several cells off the hull
        // entirely, driving pOnHull toward zero even at Precision 1 (sigma 1, comfortably inside a 9x9 hull).
        var hud = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
        Assert.True(hud.POnHull > .3f, $"pOnHull ({hud.POnHull}) collapsed -- the aim point is likely off the hull");

        var outcomes = FireMany(e, 3000);
        var hits = outcomes.Where(o => o.Hit).ToList();
        Assert.True(hits.Count > 300, $"expected a healthy number of hits, got {hits.Count}");

        var empiricalMeanLateral = hits.Average(o => (double) o.Lateral);
        // Sigma is 1 cell; a "first cell only" aim point sits at least .5 cell off the true centroid on a 2x2
        // footprint, well outside this tolerance for a few-hundred-shot mean.
        Assert.True(Math.Abs(empiricalMeanLateral - expectedA) < .25,
            $"empirical mean lateral offset {empiricalMeanLateral:F3} does not track the centroid's own lane {expectedA:F3}");
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

    // S1 (Soul's second pass, 2026-09-25): at a non-axis-aligned bearing, a lateral draw exactly at an
    // interval's Lo used to touch only a cell's corner -- Lane's own shadow prefilter admitted the cell but the
    // independent SlabAlongB slab test rejected it on float rounding, Lane returned zero cells, and Commit's
    // own guard threw InvalidOperationException out of Zone.Update (R3 violation: a hit that already passed
    // its roll must always land on metal). Soul reproduced this live, not through reflection into a private
    // method: zone name Cut12-edge-68442709 (StableHash seeds Zone.CombatSeed, so this exact name reproduces
    // the exact die roll), a 7x9 hull, Precision 2, target facing 1 degree, shot 1. Fixed by deleting the
    // second, disagreeing gate (this fix batch's FireControl.cs change) -- this pins the exact repro against a
    // real Commit, not a reflection call, and checks the placement lands where the roll priced it.
    [Fact]
    public void ShotAtBearingLoTouchingOnlyACellCornerHitsMetal()
    {
        var e = Build(TestSettings(), SolidShape(7, 9), precision: 2f, targetFacing: Deg(1), zoneName: "Cut12-edge-68442709");

        ShotOutcome outcome = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.01f);

        Assert.NotNull(outcome);
        Assert.True(outcome.Hit, "fixture: this exact seed must roll a hit (this is the repro, not a general property)");
        Assert.True(e.HullData.Shape[outcome.Cell], $"hit landed on {outcome.Cell}, not on the hull's own schematic");

        var ell = float2(-outcome.Bearing.y, outcome.Bearing.x);
        var h = (Math.Abs(ell.x) + Math.Abs(ell.y)) / 2f;
        var centre = dot((float2) outcome.Cell, ell);
        Assert.True(outcome.Lateral >= centre - h && outcome.Lateral < centre + h,
            $"hit cell {outcome.Cell}'s own projected lateral interval [{centre - h},{centre + h}) does not contain the drawn Lateral {outcome.Lateral}");
    }

    // S1: the same class of empty-lane crash on the ruling's own TurningArmourIntoTheShotTakesItOnTheArmour
    // geometry (3x11 hull, velocity 20, Precision .4, the fire-then-turn sequence that fixture uses) --
    // Soul's search found a zone name (Cut12-edge-64078070) whose draw lands on an interval edge after the
    // target turns its bow into the shot.
    [Fact]
    public void ShotAtBearingLoOnTurningArmourHitsMetal()
    {
        var e = Build(TestSettings(), SolidShape(3, 11), precision: .4f, velocity: 20, zoneName: "Cut12-edge-64078070");

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var pending = e.Zone.PendingShots.Single(s => s.ShotId == shotId);
        var travelDirection = pending.TravelDirection;

        // At Fire: bow faces the same way the shot travels (stern exposed). Before commit: turn the bow to
        // face the incoming shot head-on, TurningArmourIntoTheShotTakesItOnTheArmour's own sequence.
        e.Target.Direction = travelDirection;
        e.Zone.Update(1f);
        e.Target.Direction = -travelDirection;

        ShotOutcome outcome = null;
        using var r = e.Zone.ShotResolved.Subscribe(o => outcome = o);
        e.Zone.Update(4f); // past commit (4.5s), short of arrival (5s)
        e.Zone.Update(2f); // past arrival

        Assert.NotNull(outcome);
        Assert.True(outcome.Hit, "fixture: this exact seed/sequence must roll a hit (this is the repro, not a general property)");
        Assert.True(e.HullData.Shape[outcome.Cell], $"hit landed on {outcome.Cell}, not on the hull's own schematic");
        Assert.True(outcome.Cell.y >= 5.5f, $"expected the hit to land on the bow row (y>=5.5), got cell {outcome.Cell}");

        var ell = float2(-outcome.Bearing.y, outcome.Bearing.x);
        var h = (Math.Abs(ell.x) + Math.Abs(ell.y)) / 2f;
        var centre = dot((float2) outcome.Cell, ell);
        Assert.True(outcome.Lateral >= centre - h && outcome.Lateral < centre + h,
            $"hit cell {outcome.Cell}'s own projected lateral interval [{centre - h},{centre + h}) does not contain the drawn Lateral {outcome.Lateral}");
    }

    // S1 sweep: across many bearings and precisions, on hulls including a non-convex (holed) one, Lane must
    // find at least one occupied cell for every lateral offset at an interval's own Lo, and just below its Hi
    // -- the exact edges LateralDraw's own half-open clamp can produce (u=0 draws Lo; u near 1 draws just
    // below Hi). An empty lane at either edge is precisely the class of crash S1 fixed; this is the general
    // property the two named-seed repros above are specific instances of.
    [Fact]
    public void LaneNeverEmptiesAtAnIntervalEdge()
    {
        var shapes = new[] { SolidShape(7, 9), SolidShape(3, 11), HoledShape(7, 7) };
        var precisions = new[] { .3f, .5f, 1f, 2f };
        var failures = new List<string>();

        foreach (var shape in shapes)
        {
            var hull = new HullData { Name = "Sweep", HullType = HullType.Ship, Shape = shape };
            var buffer = new LaneCell[shape.Coordinates.Length];
            for (var deg = 0; deg < 360; deg += 3)
            {
                var b = Deg(deg);
                foreach (var precision in precisions)
                {
                    var sil = FireControl.Silhouette(null, hull, null, b, precision);
                    for (var k = 0; k < sil.Count; k++)
                    {
                        var iv = sil.Intervals[k];
                        if (FireControl.Lane(hull, b, iv.Lo, buffer) == 0)
                            failures.Add($"{shape.Width}x{shape.Height} deg={deg} p={precision} k={k} at Lo={iv.Lo:R}");
                        var justBelowHi = Math.Max(iv.Lo, iv.Hi - 1e-4f - 1e-5f);
                        if (FireControl.Lane(hull, b, justBelowHi, buffer) == 0)
                            failures.Add($"{shape.Width}x{shape.Height} deg={deg} p={precision} k={k} just below Hi={iv.Hi:R}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} empty lanes at an interval edge, e.g.: {string.Join("; ", failures.Take(5))}");
    }

    // Entity.ApplyHit's own penetration march, replicated independently (not by calling ApplyHit or reading
    // its source beyond the documented convention 12.2 explicitly leaves untouched: cell centres at
    // +.5, .5-unit steps, hullData.Shape[int2(point)] as the stop condition). Used to compute, from a frozen
    // Bearing, the full set of cells a march would consume -- independent of which bearing (committed or
    // live) the test feeds it.
    private static HashSet<int2> ExpectedMarchCells(HullData hull, int2 cell, float2 bearing, float penetration)
    {
        var cells = new HashSet<int2> { cell };
        if (penetration <= .5f) return cells;
        var vector = normalize(bearing);
        var point = (float2) cell + float2(.5f);
        var distance = 0f;
        while (distance < penetration && hull.Shape[int2(point)])
        {
            distance += .5f;
            cells.Add(int2(point));
            point += vector * .5f;
        }
        return cells;
    }

    // TurningAfterCommitChangesNothing (R4, closes L464/L465): after commit, turning the target and moving the
    // shooter must not change where the damage actually lands -- Apply reads no live position or facing.
    // F5 (Soul's fix batch, 2026-09-24): the original fixture fired with Penetration 0, so ApplyHit's march
    // (Entity.cs, gated at `penetration > .5f`) never ran and never actually read the bearing Apply passed it
    // -- a mutant that fed Apply a live (post-turn) bearing instead of the committed one survived (M2a) because
    // nothing downstream of Cell ever consumed Bearing. Penetration 3 marches several cells deep, so the full
    // damaged-cell set is asserted against the committed Bearing/Lateral, not the live one.
    [Fact]
    public void TurningAfterCommitChangesNothing()
    {
        var settings = TestSettings();
        var e = Build(settings, SolidShape(7, 7), precision: .5f, velocity: 20, penetration: 3); // 5s flight, commits at 4.5s

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

        var expectedFromCommitted = ExpectedMarchCells(e.HullData, committed.Cell, committed.Bearing, 3f);

        var before = (float[,]) e.Target.Armor.Clone();

        // After commit, before arrival: the target turns 90 degrees and the shooter moves.
        var postTurnFacing = float2(1, 0);
        e.Target.Direction = postTurnFacing;
        e.Shooter.Position += float3(500, 0, 500);

        // Fixture precondition: the (wrong) march a live-bearing bug would produce must actually differ from
        // the committed one, or this test cannot tell the two apart. "Live" here means exactly what a mutant
        // recomputing the bearing fresh at Apply time would read: the current TravelDirection (now stale,
        // post-move) folded through the target's NEW facing -- both public FireControl functions, not a
        // hand-rolled copy of the frame math.
        var liveTravelDirection = FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target);
        var wrongBearing = normalize(e.Target.ToSchematic(liveTravelDirection));
        var expectedFromLive = ExpectedMarchCells(e.HullData, committed.Cell, wrongBearing, 3f);
        Assert.NotEqual(expectedFromCommitted, expectedFromLive);

        e.Zone.Update(2f); // past arrival (5s)

        foreach (var cell in expectedFromCommitted)
            Assert.True(e.Target.Armor[cell.x, cell.y] < before[cell.x, cell.y],
                $"cell {cell} is on the committed march and must have taken damage, regardless of the post-commit turn");

        foreach (var cell in expectedFromLive)
            if (!expectedFromCommitted.Contains(cell))
                Assert.Equal(before[cell.x, cell.y], e.Target.Armor[cell.x, cell.y]);
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

    // F3 (Soul's fix batch, 2026-09-24): every 12.2 fixture, and every shipped hull (Soul's own 360-degree
    // sweep), projects to exactly ONE merged interval at its test bearing -- LateralDraw's walk past the
    // first interval (the `cumulative += mass` loop continuation) and a Silhouette built with the wrong sigma
    // (M12) were both unreachable. This hull is two 2-wide prongs (x in {0,1} and {5,6}, all rows) separated
    // by a real gap (x in 2..4, unoccupied) -- a dead-ahead bearing (ell=(-1,0)) gives two disjoint shadow
    // intervals, symmetric around the hull's own centre of mass (3,2), which sits in the gap.
    [Fact]
    public void TwoProngHullSplitsTheScatterAcrossBothIntervals()
    {
        var shape = new Shape(7, 5);
        foreach (var x in new[] { 0, 1, 5, 6 })
        for (var y = 0; y < 5; y++)
            shape[new int2(x, y)] = true;

        const float sigma = 1.5f;
        const float precision = 1f / sigma;
        var e = Build(TestSettings(), shape, precision); // unaimed: centre of mass (3,2)

        // The bearing a shot would freeze, read directly (not through an actual Fire call, which would leave
        // a stray shot in the queue for FireMany to double-count).
        var travelDirection = FireControl.TravelDirection(e.Weapon, e.Shooter, e.Target);
        var bearing = normalize(e.Target.ToSchematic(travelDirection));
        var ell = float2(-bearing.y, bearing.x);
        var a = dot(shape.CenterOfMass, ell);

        // Independent shadow (raw per-cell intervals, sorted and merged), the same convention
        // PlacementAndProbabilityShareOneSilhouette uses.
        var raw = shape.Coordinates
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
        Assert.Equal(2, merged.Count); // fixture precondition: a real two-interval shadow

        double Mass((float Lo, float Hi) iv)
        {
            const int steps = 4000;
            var width = (iv.Hi - iv.Lo) / steps;
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
        var mass0 = Mass(merged[0]);
        var mass1 = Mass(merged[1]);
        var totalMass = mass0 + mass1;
        Assert.True(totalMass > .05 && totalMass < .95, $"fixture: total pOnHull ({totalMass}) must be non-degenerate");
        Assert.True(Math.Abs(mass0 - mass1) / totalMass < .05, "fixture: the two prongs must be near-symmetric for a clean 50/50 check");

        var outcomes = FireMany(e, 8000);
        var hits = outcomes.Where(o => o.Hit).ToList();
        Assert.True(hits.Count > 800, $"expected a healthy number of hits, got {hits.Count}");

        var inProngA = hits.Count(o => o.Cell.x <= 1);
        var inProngB = hits.Count(o => o.Cell.x >= 5);
        Assert.Equal(hits.Count, inProngA + inProngB); // every hit lands in one prong or the other, never the gap
        var empiricalShareA = inProngA / (double) hits.Count;
        var expectedShareA = mass0 / totalMass;
        Assert.True(Math.Abs(empiricalShareA - expectedShareA) < .05,
            $"prong A's empirical share {empiricalShareA:F3} does not match its analytic share {expectedShareA:F3}");

        // S2 fix batch (Hands, 2026-09-25): within-prong SHAPE, pinned by ratio, not just direction. Prong A is
        // x in {0,1}, projected lateral centres 0 and -1 respectively (ell=(-1,0)), a=-3. The old
        // "atNearColumn > atFarColumn" check survived M12/M12b (an entire second silhouette built at sigma
        // floor .5 or at sigma*sqrt(2)) and the sigma/sqrt(2) Stryker mutants at :~753/:~750, because every one
        // of those wrong sigmas still puts more mass on the nearer column -- direction alone can't tell a
        // sigma of 1.5 from a sigma of 1.5*sqrt(2). The RATIO can: Soul's own figures are ~2.9 at the true
        // sigma against ~1.73 at sigma*sqrt(2), well separated at 8000 shots. The near and far columns' own raw
        // (unmerged) intervals are exactly the two ends of `raw` for prong A, reused here rather than
        // recomputed.
        var nearInterval = raw.First(iv => Math.Abs(iv.Lo - (-1.5f)) < 1e-3f); // x=1: [-1.5,-0.5)
        var farInterval = raw.First(iv => Math.Abs(iv.Lo - (-0.5f)) < 1e-3f);  // x=0: [-0.5,0.5)
        var nearMass = Mass(nearInterval);
        var farMass = Mass(farInterval);
        var analyticRatio = nearMass / farMass;
        Assert.True(analyticRatio > 2.3, $"fixture: analytic near/far ratio ({analyticRatio:F3}) must be well clear of the wrong-sigma ratio (~1.73) to actually distinguish the two");

        var atNearColumn = hits.Count(o => o.Cell.x == 1);
        var atFarColumn = hits.Count(o => o.Cell.x == 0);
        Assert.True(atNearColumn > 0 && atFarColumn > 0, $"expected hits on both columns, got near={atNearColumn} far={atFarColumn}");
        var empiricalRatio = atNearColumn / (double) atFarColumn;
        Assert.True(Math.Abs(empiricalRatio - analyticRatio) < .6,
            $"empirical near/far ratio {empiricalRatio:F3} ({atNearColumn}/{atFarColumn}) does not track the analytic ratio {analyticRatio:F3} -- a sigma perturbed by sqrt(2) would give ~1.73 instead");
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
    // Commit itself rolls against are the same number at the commit tick.
    // F4 (Soul's fix batch, 2026-09-24): the original test called CommitProbability twice and compared the
    // two calls to each other -- a self-consistency check that never observed what Commit's own roll actually
    // uses. Survivors: Commit applying PSpread twice (M15), and Commit rolling against PFire x pOnHull alone,
    // dropping deviation and PSpread entirely (M16) -- both invisible to a test that never rolls anything.
    // This version fires many shots against one fixed, non-trivial configuration (nonzero weapon Spread, and
    // a target jinked off its fire-time projection so pDeviation is strictly less than 1) and checks the
    // EMPIRICAL hit fraction over the real Commit path against the HUD's own forecast, with a tolerance sized
    // from the sample (3 sigma of a Bernoulli(p, n)).
    [Fact]
    public void TheHudEstimateIsTheCommitPrice()
    {
        var e = Build(TestSettings(), SolidShape(5, 7), precision: .5f, velocity: 40, spread: 6f); // 2.5s flight, commits at 2.0s
        var origin = e.Target.Position;
        const int shots = 800;

        var pendingSnapshots = new List<PendingShot>();
        for (var i = 0; i < shots; i++)
        {
            e.Target.Position = origin; // reset before each Fire so every shot freezes the same FireTargetPosition
            var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(.001f);
            // The fixture's own Targeting system freezes Tracking = 100000 (full-strength isolation, Build's
            // own convention) -- against that, a jink small enough to keep the target on this 5x7 hull barely
            // registers (pDeviation ~= .99996). Lower the frozen Tracking after the fact, the same way
            // CommitProbabilityIsTheExactProductOfItsFourFactors overrides a frozen field, so the jink below
            // produces a pDeviation strictly between 0 and 1.
            var index = e.Zone.PendingShots.FindIndex(s => s.ShotId == shotId);
            var shot = e.Zone.PendingShots[index];
            shot.Tracking = 6f;
            e.Zone.PendingShots[index] = shot;
            pendingSnapshots.Add(shot);
        }

        // One jink, after every shot is queued, before any of them commit -- pDeviation < 1 for all of them,
        // uniformly (FireTargetVelocity is 0, so DeviationProbability's projection never drifts with elapsed
        // time; every shot sees the same deviation regardless of the small firing-order time skew above).
        e.Target.Position = origin + float3(4, 0, 0);

        var hits = 0;
        var commits = 0;
        using var c = e.Zone.ShotCommitted.Subscribe(o => { commits++; if (o.Hit) hits++; });
        e.Zone.Update(2.1f); // past every shot's 2.0s commit, short of the 2.5s arrival

        Assert.Equal(shots, commits);

        var expected = FireControl.CommitProbability(pendingSnapshots[0], e.Zone.Time, out var sil);
        Assert.True(sil.POnHull > .02f && sil.POnHull < .98f, $"fixture: pOnHull ({sil.POnHull}) must be non-degenerate");
        var pDeviation = FireControl.DeviationProbability(pendingSnapshots[0], e.Zone.Time, out var deviation);
        Assert.True(deviation > 0f, "fixture: the jink must actually move the target off its projection");
        Assert.True(pDeviation > .02f && pDeviation < .98f, $"fixture: pDeviation ({pDeviation}) must be strictly between 0 and 1");
        Assert.True(expected > .02f && expected < .98f, $"fixture: the commit price ({expected}) must be strictly between 0 and 1");

        var empirical = hits / (double) shots;
        // 3 standard deviations of a Bernoulli(expected, shots) draw, so this fails on the real bug (a
        // dropped or duplicated factor) far more often than on sampling noise.
        var tolerance = 3.0 * Math.Sqrt(expected * (1 - expected) / shots);
        Assert.True(Math.Abs(empirical - expected) < tolerance,
            $"empirical hit rate {empirical:F4} over {shots} shots is not within {tolerance:F4} of the HUD's forecast {expected:F4}");
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
