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
using float3 = CultMath.float3;
using int2 = CultMath.int2;
using Random = CultMath.Random;

// Cut 9 (docs/fire-control-cut.md, "the die is not a die"): pins 9.1 (the hit roll must be uniform on
// [0,1), not a step function fixed by CombatSeed alone -- Cut 6b's own prescription, shipped through three
// cuts, never once measured), 9.2 (pOnHull must stay monotonic in Precision over the whole domain instead of
// collapsing to zero past a cliff no shipped hull's non-integral centre of mass can avoid) and 9.3 (a Target
// surviving a dock/die/undock cycle must not crash LockWeapon). Builds its own fixtures, the same convention
// every earlier cut's test file establishes.
public sealed class FireControlCut9Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut9-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut9Tests() => Directory.CreateDirectory(_root);

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

    // ---------------------------------------------------------------------------------------------------
    // 9.1: the die itself. No weapon or targeting gear -- these tests drive FireControl.Commit directly
    // through synthetic PendingShots (FireSynthetic below), bypassing Fire() entirely, so PBase and ShotId
    // are exact test inputs rather than products of another cut's own Accuracy/Resolution/Spread/pOnHull
    // arithmetic.
    // ---------------------------------------------------------------------------------------------------

    private sealed class DieEngagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
    }

    // zoneName seeds Zone.CombatSeed (StableHash of the GalaxyZone's Name) -- distinct names give distinct
    // zone seeds, the way TheDieIsUniform sweeps several of them.
    private DieEngagement BuildDieEngagement(GameplaySettings settings, string zoneName)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 100000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        // A bare, inert item -- not read by anything this test cares about, but Entity.Update's
        // `foreach (var item in _orderedEquipment)` NREs if an entity was never equipped with anything at
        // all (_orderedEquipment stays null until TryEquip/RemoveEquip first assign it), the same reason
        // FireControlCut8Tests.BuildLockScenario equips its own bystander with a token gun.
        cache.Upsert(new GearData
        {
            Name = "Marker", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1000000,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = zoneName, Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var markerRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Marker"));

        // Durability high enough to survive the thousands of direct hits TheDieIsUniform's bisection fires
        // at it -- an entity whose hull dies partway through would stop taking further hits and bias
        // whatever's left of the sample.
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());
        var shooterMarker = new EquippableItem { Data = markerRef, Durability = 1000000, Lot = 3 };
        Assert.True(shooter.TryFindSpace(shooterMarker, out var shooterMarkerPos));
        Assert.True(shooter.TryEquip(shooterMarker, shooterMarkerPos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = 2 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetMarker = new EquippableItem { Data = markerRef, Durability = 1000000, Lot = 4 };
        Assert.True(target.TryFindSpace(targetMarker, out var targetMarkerPos));
        Assert.True(target.TryEquip(targetMarker, targetMarkerPos));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, 100);

        return new DieEngagement { Items = items, Zone = zone, Shooter = shooter, Target = target };
    }

    // Fires one synthetic shot with an exact ShotId and PBase, bypassing FireControl.Fire's own Accuracy/
    // Resolution/Spread/pOnHull arithmetic -- 9.1's tests own the roll, not the factors that feed it.
    // FireTargetPosition/Velocity match the target's actual (stationary) position and FireTime equals the
    // zone's own time, so DeviationProbability commits to exactly 1 and PBase is the one number Commit's die
    // reads. CommitTime/ArrivalTime are set in the past so the shot commits and resolves inside the very
    // next zone.Update call, synchronously, before this method returns.
    private bool FireSynthetic(DieEngagement e, int shotId, float pBase, float precision = 1000f)
    {
        bool? hit = null;
        using var sub = e.Zone.ShotResolved.Subscribe(o =>
        {
            if (o.ShotId == shotId) hit = o.Hit;
        });
        var shot = new PendingShot
        {
            ShotId = shotId,
            Source = e.Shooter,
            Target = e.Target,
            Damage = 1f,
            Penetration = 0f,
            DamageSpread = 0f,
            DamageType = DamageType.Kinetic,
            PBase = pBase,
            Tracking = 1e6f,
            Precision = precision,
            FireTargetPosition = e.Target.Position,
            FireTargetVelocity = float3.zero,
            FireTime = e.Zone.Time,
            CommitTime = e.Zone.Time - 1f,
            ArrivalTime = e.Zone.Time - 1f
        };
        e.Zone.PendingShots.Add(shot);
        e.Zone.Update(0.001f);
        if (hit == null) throw new InvalidOperationException($"synthetic shot {shotId} did not resolve");
        return hit.Value;
    }

    // Bisects the exact roll (the p at which Hit flips from false to true) for one shot id: since Commit's
    // die is `hit = random.NextFloat() < p` with random seeded purely from (zone, shotId), the threshold p
    // this converges to *is* that shot's draw, recovered through the public Commit/ShotResolved surface
    // rather than by reaching into FireControl's private RNG plumbing.
    private float BisectRoll(DieEngagement e, int shotId)
    {
        float lo = 0f, hi = 1f;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2f;
            if (FireSynthetic(e, shotId, mid)) hi = mid; else lo = mid;
        }
        return (lo + hi) / 2f;
    }

    // 9.1 (Soul C1, blocking): the defect this whole cut exists for. A single xorshift round does not
    // diffuse a seed that differs from its neighbours only in its low bits (shot.ShotId), so every zone's
    // first draw was a near-constant function of ShotId, span ~0.125 -- a shot was a step function of p,
    // fixed at zone creation, not a roll. Mutation: delete the fmix32 finalizer FireControl.Commit applies
    // before constructing its Random. Must die on both assertions below (a mixed-in seed produces both a
    // wide roll span and a hit fraction that tracks p; the pre-fix code fails both).
    [Fact]
    public void TheDieIsUniform()
    {
        var settings = TestSettings();
        // Several distinct zone seeds (Zone.CombatSeed = GalaxyZone.Name.StableHash()) -- 9.1's defect is a
        // property of the diffusion, not of any one unlucky seed, so a single zone passing would prove
        // nothing about the fix.
        var zoneNames = new[] { "Cut9-Alpha", "Cut9-Beta", "Cut9-Gamma", "Cut9-Delta", "Cut9-Epsilon" };
        var intermediatePs = new[] { .3f, .6f };

        foreach (var zoneName in zoneNames)
        {
            var e = BuildDieEngagement(settings, zoneName);

            // Roll span across a dense run of *consecutive* shot ids -- 9.1's defect is specifically that
            // ShotId only ever perturbs the seed's low bits, so this is the range that actually exercises
            // it (a step of 877 up to 20000 flips high bits of ShotId itself and gets a wide span even
            // pre-fix, proving nothing about the diffusion).
            var rolls = new List<float>();
            for (var shotId = 0; shotId < 300; shotId++)
                rolls.Add(BisectRoll(e, shotId));
            var span = rolls.Max() - rolls.Min();
            Console.WriteLine($"[TheDieIsUniform] zone={zoneName} roll span={span:F4} min={rolls.Min():F4} max={rolls.Max():F4} n={rolls.Count}");
            Assert.True(span > 0.2f,
                $"zone {zoneName}: roll span {span:F4} across {rolls.Count} shot ids is not substantially more than 0.2 -- the die is still a step function fixed at zone creation");

            // Hit fraction at an intermediate p must track p, not sit near 0 or 1 (9.1's step-function
            // symptom: a zone either always hits or never hits once p crosses its one fixed threshold).
            foreach (var p in intermediatePs)
            {
                const int n = 500;
                var hits = 0;
                for (var shotId = 0; shotId < n; shotId++)
                    if (FireSynthetic(e, 1_000_000 + shotId, p)) hits++;
                var fraction = hits / (float) n;
                Console.WriteLine($"[TheDieIsUniform] zone={zoneName} p={p} hit fraction={fraction:F3} ({hits}/{n})");
                Assert.True(Math.Abs(fraction - p) < 0.08f,
                    $"zone {zoneName}, p={p}: observed hit fraction {fraction:F3} over {n} shots is not near p -- die is not uniform");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // 9.2: pOnHull must stay monotonic in Precision over the whole domain. A neutral shooter/target pair
    // (Accuracy 1, huge Resolution/Tracking, zero weapon Spread), the same isolation convention
    // FireControlCut6dTests.Build uses, so HitProbability's PBase collapses to exactly pOnHull.
    // ---------------------------------------------------------------------------------------------------

    private sealed class KernelEngagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public Weapon Weapon;
    }

    private KernelEngagement BuildKernelEngagement(GameplaySettings settings, Shape hullShape, float precision, float spread = 0)
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
                Velocity = Constant(0), Spread = Constant(spread), DamageSpread = Constant(0),
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
                // Cut 11: a finite Resolution, so pSensor actually varies with info. At 1000 it saturated to exactly 1
                // for any info above the detection threshold, which made `x * pSensor` and `x / pSensor` the same
                // number and let a mutant of Inspect's product survive HitProbabilityMatchesInspect.
                Accuracy = Constant(1f), Resolution = Constant(2f), Precision = Constant(precision), Tracking = Constant(100000f)
            } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 5; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Cut9-Kernel", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(x => x.EquippableItem == gun);
        var weapon = (Weapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 5 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, 100);
        shooter.Target.Value = target;
        shooter.SetIff(target, true);
        shooter.EntityInfoGathered[target] = 1f; // full reveal: pSensor = 1

        zone.Update(0f); // warm-up: resolves weapon/targeting stats before Inspect reads them

        return new KernelEngagement { Items = items, Zone = zone, Shooter = shooter, Target = target, Weapon = weapon };
    }

    // 9.2 (Soul C2, blocking): pOnHull collapsed to zero once sigma (1/Precision) fell below about half a
    // cell, because the discrete kernel sum badly undershoots the continuous 2*pi*sigma^2 denominator
    // against an aim point that is not exactly on a cell centre -- true of every shipped hull (no centre of
    // mass is integral). A 7x13 solid hull reproduces the same off-integer-centre shape without needing the
    // real catalog open in this project. Mutation: delete the sigma floor -- pOnHull at Precision 1000
    // collapses back toward zero without it.
    [Fact]
    public void POnHullNeverCollapsesAboveThePrecisionCliff()
    {
        var settings = TestSettings();
        var precisions = new[] { .1f, .3f, .6f, 1f, 1.19f, 2f, 3f, 4f, 5f, 6f, 8f, 10f, 15f, 25f, 50f, 100f, 250f, 500f, 1000f };
        // Even extents: CenterOfMass is the raw mean of integer cell coordinates (ItemData.cs), with no
        // +0.5 cell-centre offset -- an odd extent's mean lands exactly on an occupied cell (no cliff to
        // reproduce), while an even one lands on the half-cell boundary between two cells, the same
        // off-integer shape every shipped hull's real (irregular) footprint produces (LonginusX 2.5/6.697,
        // Zenith 5.5/5.5, Turret 3.5/3.5).
        var hullShape = SolidShape(6, 12);

        var samples = new List<(float Precision, float POnHull)>();
        foreach (var precision in precisions)
        {
            var e = BuildKernelEngagement(settings, hullShape, precision);
            var diagnostic = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
            samples.Add((precision, diagnostic.POnHull));
        }

        Console.WriteLine("[POnHullNeverCollapsesAboveThePrecisionCliff] " +
            string.Join(", ", samples.Select(s => $"P={s.Precision}:{s.POnHull:F4}")));

        foreach (var (precision, pOnHull) in samples)
            Assert.True(pOnHull > 0.05f, $"Precision {precision}: pOnHull collapsed to {pOnHull:F4} -- a weapon this well-aimed must still be able to hit the hull");

        // Monotonic non-collapse over the *whole* domain: once precision climbs (sigma shrinks) pOnHull must
        // not fall back toward zero the way the undamped continuous-integral denominator did past the cliff.
        // (Below Precision ~1, pOnHull legitimately rises as the kernel tightens onto the aim point; the
        // regression this pins is the second half of the curve heading back down to nothing.)
        var peakIndex = samples.Select(s => s.POnHull).ToList().IndexOf(samples.Max(s => s.POnHull));
        for (var i = peakIndex + 1; i < samples.Count; i++)
            Assert.True(samples[i].POnHull > samples[peakIndex].POnHull * 0.5f,
                $"pOnHull fell from {samples[peakIndex].POnHull:F4} at Precision {samples[peakIndex].Precision} to {samples[i].POnHull:F4} at Precision {samples[i].Precision} -- the cliff is back");
    }

    // ---------------------------------------------------------------------------------------------------
    // Cut 10 (docs/fire-control-cut.md): one fire-control model, shared at the level of its factors.
    // HitProbability is the hot path -- every AI and turret, per weapon, per tick -- and Inspect is the
    // debug HUD's view. The diagnostics refactor (72c0109c) kept them as one model by routing the hot path
    // THROUGH Inspect, which made every call build the diagnostic struct and run the aim-point and kernel
    // work before checking whether the target was even in range: 9.87 us and 448 B per call, measured by a
    // Soul pass, on paths that used to bail in nanoseconds. Both now call the same factor functions.
    // ---------------------------------------------------------------------------------------------------

    // The invariant the diagnostics refactor was protecting: the HUD can never report a probability the
    // simulation does not use. Inspect.PBase is HitProbability's own answer, so the gates and the product
    // have one owner; what is left to defend is that the factors the HUD displays are the ones the
    // simulation multiplies, and that a gate the HUD shows closed is one the simulation refuses on. Swept
    // across Precision, info levels and a spread cone that does not fill the silhouette -- every factor
    // strictly between 0 and 1 somewhere, so a factor dropped, doubled or divided in either caller shows.
    [Fact]
    public void TheHudShowsTheFactorsTheSimulationMultiplies()
    {
        var settings = TestSettings();
        var checkedConfigurations = 0;
        var sawPartialSpread = false;
        foreach (var precision in new[] { .3f, 1.19f, 5f })
        {
            // 6 cells wide at cell size 1 subtends ~3.4 degrees at 100 units, inside an 8-degree cone.
            var e = BuildKernelEngagement(settings, SolidShape(6, 12), precision, spread: 8);

            foreach (var info in new[] { .2f, .5f, 1f })
            {
                e.Shooter.EntityInfoGathered[e.Target] = info;
                var hud = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
                var hot = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
                Assert.True(hud.Visible && hud.InRange && hud.Locked && hud.InArc, $"info {info}: precondition, every gate open");
                Assert.True(hot > 0f, $"info {info}, Precision {precision}: precondition, a shot that can land");
                Assert.Equal(hot, hud.Accuracy * hud.PSensor * hud.PSpread * hud.POnHull, 5);
                sawPartialSpread |= hud.PSpread < .99f;
                checkedConfigurations++;
            }
            e.Shooter.EntityInfoGathered[e.Target] = 1f;

            void Closed(string label, Func<FireControlDiagnostic, bool> gate)
            {
                var hud = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
                Assert.False(gate(hud), $"{label}: the HUD must show this gate closed");
                Assert.Equal(0f, FireControl.HitProbability(e.Weapon, e.Shooter, e.Target));
                checkedConfigurations++;
            }

            e.Target.Position = float3(0, 0, 5000);
            Closed("out of range", d => d.InRange);
            e.Target.Position = float3(0, 0, -100);
            Closed("behind the mount", d => d.InArc);

            // A shooter far from the origin: at float3.zero `target - source` and `target + source` are the
            // same vector, so a sign mutant in the bearing only shows here (500 in range vs 1300 out).
            e.Shooter.Position = float3(600, 0, 0);
            e.Target.Position = float3(600, 0, 500);
            var far = FireControl.Inspect(e.Weapon, e.Shooter, e.Target);
            Assert.Equal(500f, far.Range, 3);
            Assert.True(far.InRange && FireControl.HitProbability(e.Weapon, e.Shooter, e.Target) > 0f, "shooter far from the origin");
            checkedConfigurations++;
            e.Shooter.Position = float3.zero;
            e.Target.Position = float3(0, 0, 100);

            e.Shooter.VisibleEntities.Remove(e.Target);
            Closed("not visible", d => d.Visible);

            Assert.False(FireControl.Inspect(e.Weapon, e.Shooter, null).HasTarget);
            Assert.Equal(0f, FireControl.HitProbability(e.Weapon, e.Shooter, null));
        }
        Assert.True(sawPartialSpread, "fixture: the spread cone must not fill the silhouette, or PSpread is 1 and proves nothing");
        Assert.Equal(21, checkedConfigurations); // 3 precisions x (3 info levels + out of range + behind + far shooter + not visible)
    }

    // The cost regression itself. A target out of range is the common case for every AI and turret, and it
    // must bail before any factor is computed. Mutation: route HitProbability back through Inspect; the
    // gated-out call then allocates the aim-point list and the kernel's arrays on every evaluation.
    [Fact]
    public void GatedOutHitProbabilityAllocatesNothing()
    {
        var e = BuildKernelEngagement(TestSettings(), SolidShape(6, 12), 1.19f);
        e.Target.Position = float3(0, 0, 5000); // out of range, still visible and in arc

        for (var i = 0; i < 100; i++) FireControl.HitProbability(e.Weapon, e.Shooter, e.Target); // JIT warm-up

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var i = 0; i < 1000; i++) sum += FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0f, sum);
        Assert.True(allocated == 0, $"1000 gated-out HitProbability calls allocated {allocated} bytes; the hot path must bail before any factor work");
    }

    // ---------------------------------------------------------------------------------------------------
    // 9.3: a Target surviving a dock/die/undock cycle must not crash LockWeapon. Same fixture shape as
    // FireControlCut8Tests.BuildLockScenario -- this is that cut's own crash, still reachable.
    // ---------------------------------------------------------------------------------------------------

    private sealed class LockScenario
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Locker;
        public Ship Bystander;
    }

    private LockScenario BuildLockScenario(GameplaySettings settings)
    {
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 1000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(new GearData
        {
            Name = "LockGun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new LockWeaponData
            {
                Damage = Constant(0), Range = Constant(1000), MinRange = Constant(0), Velocity = Constant(0),
                Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0), Count = Constant(1),
                BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } },
                LockSpeed = Constant(1), SensorImpact = Constant(1), LockAngle = Constant(170),
                DirectionImpact = Constant(1), Decay = Constant(1)
            } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Cut9-Lock", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var lockerHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 };
        var locker = new Ship(items, zone, lockerHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("LockGun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(locker.TryEquip(gun, new int2(0, 0)));

        var bystanderHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 3 };
        var bystander = new Ship(items, zone, bystanderHull, new EntitySettings());
        var bystanderGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("LockGun"));
        var bystanderGun = new EquippableItem { Data = bystanderGunRef, Durability = 1, Lot = 4 };
        Assert.True(bystander.TryEquip(bystanderGun, new int2(0, 0))); // _orderedEquipment must not be empty/null for Update to run

        zone.Entities.Add(locker);
        zone.Entities.Add(bystander);
        locker.Activate();
        bystander.Activate();

        locker.Position = float3.zero;
        bystander.Position = float3(0, 0, 100);
        locker.LookDirection = float3(0, 0, 1);
        locker.Target.Value = bystander;
        locker.EntityInfoGathered[bystander] = 1f;
        locker.SetIff(bystander, true);

        zone.Update(0f); // warm up resolved stats -- brings weapon Online/Active up

        return new LockScenario { Items = items, Zone = zone, Locker = locker, Bystander = bystander };
    }

    // 9.3 (Soul C3, high): Cut 8 fixed a real liveness defect (LockWeaponOnDeactivatedEntityDoesNotThrow)
    // but not the operator's actual crash -- that guard only ever runs on an *active* entity. The live
    // precondition is Target.Value set but absent from EntityInfoGathered on an active entity, and docking
    // still reaches it: Deactivate disposes the Zone.Entities.ObserveRemove subscription that is the only
    // thing that nulls a stale Target, and Activate reseeds EntityInfoGathered from the zone's *current*
    // membership without reconciling Target against it. Dock, let the target leave, undock: the first
    // LockWeapon tick throws. Mutation: delete Entity.Activate's Target reconciliation. Must die.
    [Fact]
    public void TargetSurvivingDockDieUndockDoesNotCrashLockWeapon()
    {
        var s = BuildLockScenario(TestSettings());
        s.Locker.Update(.1f); // lock behavior runs once while both are live and visible, same as normal play

        s.Locker.Deactivate(); // dock
        Assert.False(s.Locker.Active);

        // the target dies or leaves while the locker is inactive -- its own Zone.Entities.ObserveRemove
        // subscription (the only thing that would have nulled Target) was disposed by Deactivate above, so
        // nothing reacts to this removal yet.
        s.Zone.Entities.Remove(s.Bystander);

        s.Locker.Activate(); // undock
        Assert.True(s.Locker.Active);
        // Assert.Null(entity) would hand a live Entity into xUnit's reflection-based failure formatter,
        // which recurses through Zone -> Entities -> Ship -> Zone forever; a plain boolean with its own
        // message sidesteps that entirely.
        Assert.True(s.Locker.Target.Value == null, "the invariant: an active entity's Target is always a live entity it has info on -- a stale reference to the departed Bystander survived Activate's reconciliation");

        var ex = Record.Exception(() => s.Locker.Update(.1f));
        Assert.Null(ex);
    }
}
