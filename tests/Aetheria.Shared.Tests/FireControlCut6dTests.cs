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
using Random = CultMath.Random;

// Cut 6d (docs/fire-control-cut.md, "where a hit lands is a dart throw, not a coin flip"): pins the Gaussian
// kernel that replaces FireControl.Commit's coin flip -- one function, w(cell) = exp(-d^2/2*sigma^2), that
// HitProbability's pOnHull and Commit's cell draw both read. Builds its own fixtures, the same convention
// every earlier cut's test file establishes.
public sealed class FireControlCut6dTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut6d-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut6dTests() => Directory.CreateDirectory(_root);

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

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
        public HullData HullData;
    }

    // A shooter/target engagement over a caller-supplied hull shape, with a full-strength (Accuracy 1,
    // Resolution huge, Tracking huge, zero weapon Spread) targeting system so HitProbability collapses to
    // Accuracy * pSensor * pSpread * pOnHull = 1 * 1 * 1 * pOnHull at full info and zero deviation -- the one
    // factor this cut's tests care about isolated from the other three (the same "neutralise everything but
    // the term under test" convention FireControlCut6cTests.Build uses for Resolution).
    // markerCell, when given, equips the catalog's bare "Marker" Tool stub at that exact interior cell on the
    // target and selects it as the shooter's aim point -- done here, before Activate(), because Entity.TryEquip
    // refuses once an entity is active (the same reason every earlier cut's fixture equips extra items through
    // a beforeActivate hook rather than after Build returns).
    private Engagement Build(GameplaySettings settings, Shape hullShape, float precision, float targetRange = 100, int2? markerCell = null)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 100000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(10), Range = Constant(1000), MinRange = Constant(0),
                Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(0),
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
        // Durability far above anything a single shot deals: an aim point this cut's tests fire hundreds or
        // thousands of rounds at must survive being hit directly for the whole run, or Cut 5.5's "a destroyed
        // item drops out of the reveal ranking" rule pulls the aim out from under later shots mid-sample and
        // silently corrupts the landing distribution for everything fired after that point.
        cache.Upsert(new GearData
        {
            Name = "Marker", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 20; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        // Durability high enough to survive thousands of direct 10-damage hits over a test's whole sample --
        // several of this cut's tests fire that many shots to get a stable landing distribution, and an entity
        // whose hull dies partway through would both stop taking further hits and bias the remaining sample.
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(x => x.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 5 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        EquippableItem marker = null;
        if (markerCell != null)
        {
            var markerRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Marker"));
            marker = new EquippableItem { Data = markerRef, Durability = 1000000, Lot = 6 };
            Assert.True(target.TryEquip(marker, markerCell.Value));
        }

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, targetRange);
        shooter.Target.Value = target;
        shooter.SetIff(target, true);
        shooter.EntityInfoGathered[target] = 1f; // full reveal: pSensor = 1

        zone.Update(0f); // warm-up: resolves weapon/targeting stats before HitProbability/Fire read them

        if (marker != null)
        {
            var markerItem = target.Equipment.Single(x => x.EquippableItem == marker);
            Assert.True(shooter.TrySelectTargetItem(markerItem));
            Assert.Same(markerItem, shooter.ResolvedTargetItem);
        }

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon, HullData = hullData };
    }

    // Fires `count` shots and returns the resolved outcomes. Velocity 0 (the weapon fixture's own Constant(0))
    // gives every shot a zero flight time, so one small zone.Update both commits and resolves it -- no
    // deviation-over-time noise to control for, matching FireControlCut6cTests' own "commits and resolves in
    // this one call" convention.
    private List<ShotOutcome> FireMany(Engagement e, int count)
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

    // AimingAtTheSternHitsTheStern is superseded by FireControlCut12Tests.AimingAtAnItemCentresTheScatterOnItsLane
    // (docs/fire-control-cut.md, Cut 12.2): this fixture fired from the target's own stern at a stern-aimed
    // marker, so a placement rule that merely biases toward whatever is nearest the shooter -- not toward the
    // aim point specifically -- would also have passed it. The replacement fires from broadside and from dead
    // astern, and checks both a stern- and a bow-aimed shot from each, which a nearest-the-entry-side bug
    // cannot pass.

    // A 5x5 blob (x:0-4, y:0-4) plus a 3x3 "pod" (x:6-8, y:1-3) joined to it by a single-cell neck at (5,2) --
    // an appendage connected to the main mass at only one point, the schematic shape of a thin limb. The pod's
    // own centre (7,2) is technically an interior cell (Shape.Shrink needs a full 8-neighbour ring, which a
    // bare single isolated cell -- tried first -- can never satisfy: Tool gear can only equip to a cell fully
    // enclosed by the schematic, so a true one-cell-wide extremity can never itself carry an aimable item). The
    // pod supplies that ring locally while still being almost entirely surrounded by open space one ring
    // further out, so the kernel still prices it far worse than the blob's own, much better-supported centre.
    private static Shape BlobWithLimb(out int2 limb)
    {
        var shape = new Shape(9, 5);
        for (var x = 0; x < 5; x++)
        for (var y = 0; y < 5; y++)
            shape[new int2(x, y)] = true;
        shape[new int2(5, 2)] = true; // neck
        for (var x = 6; x < 9; x++)
        for (var y = 1; y < 4; y++)
            shape[new int2(x, y)] = true; // pod
        limb = new int2(7, 2); // pod centre -- the aim point
        return shape;
    }

    // ThinLimbCostsHitChance: HitProbability against an aim point on a single-cell extremity is strictly lower
    // than against the centre of mass, at the same Precision -- and the gap widens as Precision falls (within
    // the well-tuned band this campaign's catalog designs live in; both probabilities collapse toward each
    // other again at the extreme where sigma dwarfs the whole hull, which is not the regime any authored
    // design occupies). Mutation: drop pOnHull from the probability. Must die -- with pOnHull gone, aiming at
    // the limb and aiming at the centre of mass score identically (Accuracy * pSensor * pSpread, no spatial
    // term at all).
    // Cut 12.2 (docs/fire-control-cut.md): pOnHull is now the exact 1D integral of the Gaussian over the
    // target's lateral shadow rather than a 2D discrete-cell sum, but the shape of the rule is unchanged -- a
    // limb's shadow interval is a small sliver of the hull's own span, so far less of the Gaussian mass
    // centred on it actually falls on metal than when the same Gaussian is centred on the much wider mass of
    // cells around the centre of mass.
    [Fact]
    public void ThinLimbCostsHitChance()
    {
        var hullShape = BlobWithLimb(out var limb);

        float CenterProbability(float precision)
        {
            var e = Build(TestSettings(), hullShape, precision);
            return FireControl.HitProbability(e.Weapon, e.Shooter, e.Target); // unaimed: hull's own centre of mass
        }

        float LimbProbability(float precision)
        {
            var e = Build(TestSettings(), hullShape, precision, markerCell: limb);
            return FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        }

        var gaps = new List<(float Precision, float Gap)>();
        foreach (var precision in new[] { 1.5f, 1f, .6f })
        {
            var center = CenterProbability(precision);
            var onLimb = LimbProbability(precision);
            Assert.True(onLimb < center, $"at precision {precision}: expected aiming at the limb ({onLimb}) to score lower than the centre of mass ({center})");
            gaps.Add((precision, center - onLimb));
        }

        for (var i = 1; i < gaps.Count; i++)
            Assert.True(gaps[i].Gap > gaps[i - 1].Gap,
                $"expected the centre-vs-limb gap to widen as Precision falls from {gaps[i - 1].Precision} to {gaps[i].Precision}, " +
                $"got {gaps[i - 1].Gap} then {gaps[i].Gap}");
    }

    // PlacementAndProbabilityShareOneKernel is superseded by
    // FireControlCut12Tests.PlacementAndProbabilityShareOneSilhouette (docs/fire-control-cut.md, Cut 12.2):
    // the 2D discrete-cell kernel this test's ExpectedKernel replicated is gone -- Silhouette replaces it with
    // an exact 1D Gaussian integral over the hull's lateral shadow, which the new test recomputes
    // independently by numeric integration over a union of intervals it builds itself.

    // EveryHitLandsOnMetal is superseded by FireControlCut12Tests.HitsLandOnTheFacingEdge (docs/fire-control-cut.md,
    // Cut 12.2): the new test pins the stronger rule directly -- not merely "occupied," but "nothing occupied
    // precedes it in its own lane" -- recomputed independently from the outcome's own frozen Bearing/Lateral on
    // a concave (holed) hull.
}
