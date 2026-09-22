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

// Cut 3 (docs/fire-control-cut.md): pins hit authority itself -- the roll's inputs, the commit horizon, frozen
// payloads (Q6), seeding (Q5), the arc gate on player fire (Q2), and the damage rule moved verbatim onto
// Entity. Builds its own shooter/target fixture rather than reusing Cut 1's or Cut 2's private helpers, the
// same convention those files established. Each test's own comment names the mutation that must kill it.
public sealed class FireAuthorityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-fireauthority-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireAuthorityTests() => Directory.CreateDirectory(_root);

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
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 1f,
        UnaidedAccuracy = .05f,
        // Cut 6d (docs/fire-control-cut.md): high enough that pOnHull saturates to 1 at this fixture's 5x5
        // hull's own centre of mass -- these tests pin the Accuracy cap and hit/miss timing, not the dart-
        // throw kernel, so the unaided floor here is authored to stay out of their way, the same reason
        // Spread is authored 0 in most of these fixtures to keep pSpread pinned at 1.
        UnaidedPrecision = 2f,
        AgentMinHitProbability = 0f
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
    }

    // A shooter (one Sensors-hardpoint weapon + a Tool-slot targeting system) and a target ship, both
    // activated in the same zone, the target `targetRange` metres dead ahead (well within the wide default
    // arc), fully detected and marked hostile so StanceAllowsFire and the reveal/detection gates never
    // interfere with a test that isn't about them.
    private Engagement Build(
        GameplaySettings settings,
        float damage = 10, float range = 1000, float minRange = 0, float velocity = 0,
        float spread = 0, float damageSpread = 0, float penetration = 0,
        // Cut 6d (docs/fire-control-cut.md): Precision now feeds HitProbability's pOnHull for every shot, aimed
        // or not (the dart-throw kernel's sigma), where it used to matter only for the coin-flip aimed-item
        // branch this cut deletes. The old default of 0 -- harmless when Precision only gated that branch --
        // would now flatten pOnHull to nearly nothing against this fixture's 5x5 hull (sigma = 1/Precision
        // balloons), starving every test in this file that doesn't care about aim placement. 1 keeps pOnHull
        // close to its ceiling for an unaimed shot at this hull's centre of mass without pinning it exactly.
        float accuracy = 1, float resolution = 1, float precision = 1, float tracking = 1000,
        float targetRange = 100, bool equipTargeting = true, float hullDurability = 1000, float armor = 0,
        Action<ItemManager, Ship, Ship> beforeActivate = null)
    {
        // 5x5 so InteriorCells (Shape.Shrink -- every cell with all 8 neighbours present) is the inner 3x3,
        // room enough for more than one Tool item (ShieldTakesHit needs a Reactor and a Shield both).
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = hullDurability, Mass = 1000, Armor = armor,
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
                Damage = Constant(damage), Range = Constant(range), MinRange = Constant(minRange),
                Velocity = Constant(velocity), Spread = Constant(spread), DamageSpread = Constant(damageSpread),
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
                Accuracy = Constant(accuracy), Resolution = Constant(resolution), Precision = Constant(precision), Tracking = Constant(tracking)
            } }
        });
        // Authored for every engagement (harmless unless equipped): a shield/reactor pair strong and fast
        // enough to fill from cold in a handful of ticks, for ShieldTakesHit. Catalog entries are looked up by
        // name through this same already-open CultCache handle, which does not see writes made to it after
        // this point without a reopen (GetByName is a build-time index) -- so these are authored up front
        // rather than upserted later from inside a test's beforeActivate.
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
            Behaviors = { new ShieldData { Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(1000), RefillDuration = Constant(.01f), RestoreDuration = Constant(.01f) } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        for (var i = 5; i <= 12; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        if (equipTargeting)
        {
            var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
            var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
            Assert.True(shooter.TryFindSpace(targeting, out var tpos));
            Assert.True(shooter.TryEquip(targeting, tpos));
        }

        var targetHull = new EquippableItem { Data = hullRef, Durability = hullDurability, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        // Entity._orderedEquipment is populated only by TryEquip -- a ship that never equips anything leaves
        // it null and Entity.Update throws regardless of this cut (FireControlTests' own fixture notes the
        // same rake). Equip a harmless gun so the target is an ordinarily-constructed ship.
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 9 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        // Entity.TryEquip refuses once an entity is active ("don't allow equipping while deployed") -- any
        // extra gear a test needs (a second aimable item, a shield, a reactor) must go on before Activate().
        beforeActivate?.Invoke(items, shooter, target);

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, targetRange);
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        // A zero-dt warm-up tick populates the resolved weapon/targeting stats (UpdateStats runs inside
        // Execute) before FireControl.Fire reads them, the same warm-up InputCapacitorTests uses.
        zone.Update(0f);

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon };
    }

    private static InstantWeaponData DataOf(EquippedItem weaponItem) =>
        (InstantWeaponData) weaponItem.Data.Behaviors.Single(b => b is InstantWeaponData);

    // R1/Q5: two zones built identically, seeded identically, fire the same 50 shots (instant, so each
    // commits and resolves in the tick it is stepped) and get byte-identical Hit/Cell/Shielded sequences.
    // Mutation: FireControl draws from `new Random()` instead of the shooter's ItemManager.Random.
    [Fact]
    public void RollsAreSeeded()
    {
        List<(bool Hit, bool Shielded, int2 Cell)> Run(uint seed)
        {
            var e = Build(TestSettings(), accuracy: .6f, precision: .5f);
            e.Items.Random = new Random(seed);
            var results = new List<(bool, bool, int2)>();
            e.Zone.ShotResolved.Subscribe(outcome => results.Add((outcome.Hit, outcome.Shielded, outcome.Cell)));
            for (var i = 0; i < 50; i++)
            {
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
                FireControl.Step(e.Zone, 0f);
            }
            return results;
        }

        var a = Run(777u);
        var b = Run(777u);
        Assert.Equal(a, b);
    }

    // FireControl.HitProbability, R1: zero with no draw when undetected or out of range; rises with info
    // toward Resolution; falls with Spread; caps at UnaidedAccuracy with no targeting system equipped.
    // Mutations: drop pSensor (probability stops depending on info); drop the range gate (probability nonzero
    // outside [MinRange,Range]); drop the UnaidedAccuracy cap (an unaided shooter rolls as well as Accuracy).
    [Fact]
    public void ProbabilityFollowsInputs()
    {
        var e = Build(TestSettings(), accuracy: .8f, resolution: 1f, spread: 0f, targetRange: 100, range: 200);

        // Undetected: below TargetDetectionInfoThreshold (.1) -- zero regardless of everything else.
        e.Shooter.EntityInfoGathered[e.Target] = 0f;
        Assert.Equal(0f, FireControl.HitProbability(e.Weapon, e.Shooter, e.Target));

        // Rising with info toward Resolution (1): more info, more probability, saturating at Resolution.
        e.Shooter.EntityInfoGathered[e.Target] = .2f;
        var low = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        e.Shooter.EntityInfoGathered[e.Target] = .6f;
        var mid = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        e.Shooter.EntityInfoGathered[e.Target] = 1f;
        var full = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        Assert.True(low < mid);
        Assert.True(mid <= full);
        Assert.True(full > 0f);

        // Out of range: zero.
        e.Target.Position = float3(0, 0, 500);
        Assert.Equal(0f, FireControl.HitProbability(e.Weapon, e.Shooter, e.Target));
        e.Target.Position = float3(0, 0, 100);

        // Falling with Spread: a wide-spread weapon on a small target scores lower than a zero-spread one.
        DataOf(e.WeaponItem).Spread = Constant(0f);
        e.Weapon.Execute(0f);
        var noSpread = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        DataOf(e.WeaponItem).Spread = Constant(60f);
        e.Weapon.Execute(0f);
        var wideSpread = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target);
        Assert.True(wideSpread < noSpread);

        // Capped at UnaidedAccuracy with no targeting system: a second engagement, otherwise identical, but
        // never equips one.
        var unaided = Build(TestSettings(), accuracy: .8f, resolution: 1f, spread: 0f, targetRange: 100, range: 200, equipTargeting: false);
        unaided.Shooter.EntityInfoGathered[unaided.Target] = 1f;
        Assert.Equal(unaided.Items.GameplaySettings.UnaidedAccuracy, FireControl.HitProbability(unaided.Weapon, unaided.Shooter, unaided.Target), 3);
    }

    // Cut 7 (docs/fire-control-cut.md): this was OutOfArcConsumesNoDraw, which asserted that a shot gated to
    // zero left ItemManager.Random untouched. Cut 6b (6.1) moved the roll onto a per-shot local generator, so
    // Commit stopped touching that stream on EVERY path and the old test passed for a reason that had nothing
    // to do with arcs -- it would have passed just as well with the target dead ahead, and no mutation of the
    // short-circuit it named could ever turn it red. Rewritten to pin the rule that is actually live: no
    // combat path draws from the shared stream, in arc or out. Mutation: restore the shared-stream read in
    // Commit; the in-arc case then goes red.
    [Theory]
    [InlineData(100f, 0f)]   // directly abeam: gated to zero, outside the default arc
    [InlineData(0f, 100f)]   // dead ahead: a real firing solution that actually rolls
    public void CombatNeverDrawsFromTheSharedStream(float x, float z)
    {
        var e = Build(TestSettings(), accuracy: 1, resolution: 1, spread: 0);
        e.Target.Position = float3(x, 0, z);

        const uint seed = 4242u;
        var untouched = new Random(seed);
        var expected = untouched.NextFloat();

        e.Items.Random = new Random(seed);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        FireControl.Step(e.Zone, 0f);
        var actual = e.Items.Random.NextFloat();

        Assert.Equal(expected, actual);
    }

    // R3: no durability change before the shot arrives (range/Velocity after this cut's fire), and a target
    // removed mid-flight takes none. Mutation: apply the outcome at fire/commit time instead of arrival.
    [Fact]
    public void ShotResolvesOnArrival()
    {
        var e = Build(TestSettings(), damage: 100, velocity: 10, accuracy: 1, resolution: 1, spread: 0, targetRange: 50);
        // range 50, velocity 10 -> flight time 5s, commit at 4.5s (.5s horizon), arrival at 5s.
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var before = e.Target.Hull.Durability;

        e.Zone.Update(4f); // still short of commit (4.5s)
        Assert.Equal(before, e.Target.Hull.Durability);

        // Cut 7 (docs/fire-control-cut.md, 7.2): an intermediate check between commit and arrival -- without
        // it, jumping straight from before-commit to past-arrival lets "apply as soon as committed, not only
        // once also arrived" (Step's arrival gate collapsed to just `if (shot.Committed)`) go unnoticed, since
        // a single wide Zone.Update spanning both CommitTime and ArrivalTime crosses both thresholds in one
        // call either way.
        e.Zone.Update(.55f); // t=4.55: past commit (4.5s), still short of arrival (5s)
        Assert.Equal(before, e.Target.Hull.Durability);

        e.Zone.Update(1f); // now past arrival
        Assert.True(e.Target.Hull.Durability < before);
    }

    // 0b table: a shot whose target left the zone resolves as a miss and is removed, taking nothing.
    [Fact]
    public void DeadEntityStopsTakingShots()
    {
        var e = Build(TestSettings(), damage: 100, velocity: 10, accuracy: 1, resolution: 1, spread: 0, targetRange: 50);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var before = e.Target.Hull.Durability;

        e.Zone.Entities.Remove(e.Target);
        e.Zone.Update(10f); // well past arrival

        Assert.Equal(before, e.Target.Hull.Durability);
        Assert.Empty(e.Zone.PendingShots);
    }

    // R4: ShotCommitted fires exactly CommitHorizon before ShotResolved, once per shot. Mutation: publish only
    // at arrival, or publish twice.
    [Fact]
    public void OutcomeCommitsBeforeImpact()
    {
        var e = Build(TestSettings(), velocity: 10, accuracy: 1, resolution: 1, spread: 0, targetRange: 50);
        var commits = new List<float>();
        var resolves = new List<float>();
        e.Zone.ShotCommitted.Subscribe(_ => commits.Add(e.Zone.Time));
        e.Zone.ShotResolved.Subscribe(_ => resolves.Add(e.Zone.Time));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        for (var i = 0; i < 100; i++) e.Zone.Update(.1f); // 10s of ticks, well past a 5s flight

        Assert.Single(commits);
        Assert.Single(resolves);
        Assert.Equal(e.Items.GameplaySettings.CommitHorizon, resolves[0] - commits[0], 2);
    }

    // R4's second half: deviation counts up to the commit horizon and not after. A target that jinks off the
    // predicted straight-line path before commit is judged on that jink; the identical jink after commit
    // changes nothing, because the outcome is already frozen. Mutation: measure deviation at arrival instead
    // of commit (both jinks would then matter), or measure it at fire (neither would).
    [Fact]
    public void EvasionCountsUntilCommitAndNotAfter()
    {
        // Tracking small enough that a real jink defeats it, Precision moot (no aim), full accuracy/detection.
        GameplaySettings Settings() => TestSettings();

        bool RunWithJink(float jinkTime, float commitHorizon)
        {
            var settings = Settings();
            settings.CommitHorizon = commitHorizon;
            var e = Build(settings, damage: 10, velocity: 10, accuracy: 1, resolution: 1, spread: 0, tracking: 1f, targetRange: 50);
            // Cut 5 (Soul's own named survivor): a stationary target left the map's declared mutation
            // (elapsed = shot.FlightTime in place of now - shot.FireTime) invisible, because
            // FireTargetVelocity * elapsed is identically zero either way. A real, nonzero velocity -- Ship.
            // Update already integrates Position.xz += Velocity * delta every tick on its own, no manual
            // tracking needed -- makes the mutation observable even with no jink at all: the mutant
            // mispredicts the intercept by Velocity * CommitHorizon, which the post-commit-jink assertion
            // below (expected to still hit) catches as a spurious miss.
            // Cut 11: advance the zone clock before firing. Build leaves it at 0, so every shot fired with
            // FireTime = 0 -- and `now - FireTime` equals `now + FireTime` when FireTime is 0. That is why the
            // moving target Cut 5 added still left this test's own declared mutation (measure elapsed wrongly)
            // alive: Stryker found `now + shot.FireTime` surviving.
            e.Zone.Update(2.5f);
            e.Target.Velocity = float2(4, 0);
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            var t = 0f;
            const float dt = .05f;
            var jinked = false;
            var before = e.Target.Hull.Durability;
            while (t < 6f && e.Zone.PendingShots.Count > 0)
            {
                if (!jinked && t >= jinkTime)
                {
                    e.Target.Position += float3(50, 0, 0); // far outside Tracking's forgiveness
                    jinked = true;
                }
                e.Zone.Update(dt);
                t += dt;
            }
            return e.Target.Hull.Durability < before; // true if the shot still landed
        }

        // Flight time 5s, horizon 0.5s -> commit at t=4.5s. A jink well before commit (t=1s) is judged.
        Assert.False(RunWithJink(jinkTime: 1f, commitHorizon: .5f));
        // The identical jink AFTER commit (t=4.6s, horizon .5s means commit at 4.5s) must not matter.
        Assert.True(RunWithJink(jinkTime: 4.6f, commitHorizon: .5f));
    }

    // R4's graceful degradation: a velocity-0 (or otherwise sub-horizon) weapon commits and resolves within
    // the same tick it fires, publishing both events, in order. Mutation: skip the commit for short flights
    // (only ShotResolved fires, or damage applies with no ShotCommitted at all).
    [Fact]
    public void ShortFlightCommitsAtFire()
    {
        var e = Build(TestSettings(), velocity: 0, accuracy: 1, resolution: 1, spread: 0, damage: 10);
        var order = new List<string>();
        e.Zone.ShotCommitted.Subscribe(_ => order.Add("committed"));
        e.Zone.ShotResolved.Subscribe(_ => order.Add("resolved"));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.016f); // one ordinary tick

        Assert.Equal(new[] { "committed", "resolved" }, order);
        Assert.Empty(e.Zone.PendingShots);
    }

    // R10/Q6: a shot's parameters freeze when the trigger is pulled. Changing the weapon's authored Damage
    // stat (and refreshing it into the live behaviour before arrival) must not change what an already-fired
    // shot deals. Mutation: re-evaluate Damage at arrival instead of using the frozen snapshot.
    [Fact]
    public void OutcomeIsSnapshotNotReread()
    {
        var e = Build(TestSettings(), damage: 10, velocity: 10, accuracy: 1, resolution: 1, spread: 0, targetRange: 50, armor: 0);
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var before = e.Target.Hull.Durability;

        // Mid-flight (before arrival at 5s): the gun's Damage stat collapses, and a fresh Execute re-reads it
        // into the live Weapon.Damage -- simulating the "power cut, heat spike" scenario R10 names.
        e.Zone.Update(2f);
        DataOf(e.WeaponItem).Damage = Constant(99999f);
        e.Weapon.Execute(0f);
        Assert.Equal(99999f, e.Weapon.Damage, 1);

        e.Zone.Update(4f); // past arrival
        var dealt = before - e.Target.Hull.Durability;
        Assert.True(dealt > 0f);
        Assert.True(dealt < 100f); // nowhere near the inflated 99999 value -- the original 10 landed
    }

    // R5's payoff: with Precision authored extremely tight (Cut 6d: a sigma a tiny fraction of one cell) and
    // p 1, only the aimed item's durability falls -- the dart-throw kernel still converges on a deterministic
    // single-cell pick when the group is that tight, so this stays a same-cell-every-time assertion rather
    // than a statistical one. Mutation: the draw ignores Aimed and always picks a uniform random hull cell.
    [Fact]
    public void AimedHitLandsOnSelectedItem()
    {
        EquippableItem aimedGear = null;
        var e = Build(TestSettings(), damage: 50, velocity: 0, accuracy: 1, resolution: 1, spread: 0, precision: 1000, armor: 0,
            beforeActivate: (items, shooter, target) =>
            {
                // A second, distinct interior item on the target to aim at, equipped before Activate() (Entity.
                // TryEquip refuses once an entity is active).
                var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
                aimedGear = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 5 };
                Assert.True(target.TryFindSpace(aimedGear, out var pos));
                Assert.True(target.TryEquip(aimedGear, pos));
            });
        var aimedItem = e.Target.Equipment.Single(x => x.EquippableItem == aimedGear);

        e.Shooter.EntityInfoGathered[e.Target] = 1f; // fully revealed
        Assert.True(e.Shooter.TrySelectTargetItem(aimedItem));

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.1f);

        Assert.True(aimedItem.EquippableItem.Durability < 1f);
    }

    // The damage rule moved verbatim (Entity.DamageSchematic/ApplyHit): damage above armor plus item
    // durability zeroes the item and the remainder hits the hull. Mutation: skip subtracting armor, or skip
    // passing the remainder on to the hull.
    [Fact]
    public void HardpointHitDamagesItemThenHull()
    {
        var cell = new int2(1, 1);
        EquippableItem occupant = null;
        var e = Build(TestSettings(), armor: 2, hullDurability: 1000,
            beforeActivate: (items, shooter, target) =>
            {
                // No item occupies (1,1) in this fixture's hull -- occupy it before Activate() so the
                // item-then-hull chain has an item to pass through at a controlled cell.
                var occupantRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
                occupant = new EquippableItem { Data = occupantRef, Durability = 3, Lot = 6 };
                Assert.True(target.TryEquip(occupant, cell));
            });
        var occupantItem = e.Target.Equipment.Single(x => x.EquippableItem == occupant);

        var beforeHull = e.Target.Hull.Durability;
        e.Target.ApplyHit(e.Shooter, cell, spread: 0, penetration: 0, damage: 10, hitDirection: float2(0, 1));

        Assert.Equal(0f, e.Target.Armor[cell.x, cell.y]); // 2 armor consumed
        Assert.Equal(0f, occupantItem.EquippableItem.Durability); // 3 item durability consumed
        Assert.Equal(beforeHull - 5f, e.Target.Hull.Durability, 2); // remaining 5 hits the hull
    }

    // R7: the penetration march is planar, rotated into the target's own facing. Firing straight into a
    // target's nose (hitDirection opposed to Direction) marches the hit shape toward the target's stern, along
    // +Y in this hull's local grid regardless of which way the entity happens to be facing in world space.
    // Mutation: skip rotating by Direction and march along the raw world hitDirection instead -- a target
    // facing anywhere other than the world +Z axis would then penetrate the wrong cells.
    [Fact]
    public void PenetrationMarchIsPlanar()
    {
        var e = Build(TestSettings(), armor: 5);
        e.Target.Direction = float2(1, 0); // facing world +X, not the default +Z

        var cell = new int2(1, 0);
        var hitDirection = normalize(float2(1, 0)); // the shot arrived travelling along the target's own forward
        e.Target.ApplyHit(e.Shooter, cell, spread: 0, penetration: 1.5f, damage: 30, hitDirection: hitDirection);

        // Rotating hitDirection into this Direction's frame (forward=(1,0), right=(0,-1)) makes the local
        // penetration vector (0,1): the march should have advanced from (1,0) into (1,1), consuming that
        // cell's armor too. Mutation: skip the rotation and march along the raw world hitDirection (1,0)
        // instead, which would consume (2,0) rather than (1,1).
        Assert.True(e.Target.Armor[1, 1] < e.Target.MaxArmor[1, 1]);
        Assert.Equal(e.Target.MaxArmor[2, 0], e.Target.Armor[2, 0]); // untouched -- proves it wasn't a world-space march
    }

    // The absorb rule now exists once, in FireControl: an active shield that CanTakeHit absorbs, and the
    // schematic is untouched. Mutation: remove the shield branch (the hit always reaches the hull).
    //
    // Cut 6d (docs/fire-control-cut.md): damage raised from 10 to 50, well above the Reactor/Shield gear's own
    // Durability of 10. Unaimed fire now lands via the dart-throw kernel instead of a uniform pick over the
    // whole hull, and Build's own default Precision groups shots tightly enough around this fixture's small
    // hull's centre of mass that the deterministic draw for this exact seed lands on one of the two equipped
    // Tool items. At damage 10 that coincidentally consumed the item's own durability with zero remainder,
    // which left the hull's durability unchanged with or without the shield mutation applied -- the shield
    // branch could be deleted outright and this test would not have noticed. Damage well past what any single
    // item on this hull could fully absorb makes the assertion mean what it says regardless of which cell the
    // kernel happens to land on.
    [Fact]
    public void ShieldTakesHit()
    {
        var e = Build(TestSettings(), damage: 50, velocity: 0, accuracy: 1, resolution: 1, spread: 0, armor: 0,
            beforeActivate: (items, shooter, target) =>
            {
                // The Reactor/Shield catalog entries are authored in Build's own upsert batch (see its
                // comment). Equipped before Activate() (Entity.TryEquip refuses once an entity is active).
                var reactorRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Reactor"));
                var reactor = new EquippableItem { Data = reactorRef, Durability = 10, Lot = 7 };
                Assert.True(target.TryFindSpace(reactor, out var rpos));
                Assert.True(target.TryEquip(reactor, rpos));

                var shieldRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Shield"));
                var shield = new EquippableItem { Data = shieldRef, Durability = 10, Lot = 8 };
                Assert.True(target.TryFindSpace(shield, out var spos));
                Assert.True(target.TryEquip(shield, spos));
            });
        e.Target.Shield.Item.Enabled.Value = true;

        for (var i = 0; i < 20; i++) e.Zone.Update(.1f); // charge the reserve
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 50));

        var beforeHull = e.Target.Hull.Durability;
        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(.1f);

        Assert.Equal(beforeHull, e.Target.Hull.Durability);
    }
}
