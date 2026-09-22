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

// Cut 11 (docs/fire-control-cut.md): behavioural tests for what a Stryker run against FireControl.cs found
// undefended. Every test here states a rule as behaviour, observed through the public surface, and is the
// test whose absence let a real mutant survive. Builds its own fixtures, the convention every cut's test
// file follows.
public sealed class FireControlCut11Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut11-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut11Tests() => Directory.CreateDirectory(_root);

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
        UnaidedPrecision = 2f,
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
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
        public EquippedItem PortMarker;
        public EquippedItem StarboardMarker;
    }

    // A shooter and a target. The shooter sits OFF the origin on purpose: a shooter at float3.zero makes
    // `target - source` and `target + source` the same vector, which is exactly how four position mutants in
    // FireControl.cs survived every earlier suite.
    private Engagement Build(
        string zoneName = "Cut11",
        float damage = 5, float velocity = 0, float accuracy = 1,
        bool equipShield = false, float shieldCapacity = 30,
        bool markers = false)
    {
        // 5 wide x 3 tall: schematic x runs port (0) to starboard (4). Tool gear may only occupy interior
        // cells, which on this hull is the middle row x = 1..3, so the markers sit at (1, 1) and (3, 1) --
        // one cell either side of the centre of mass (2, 1).
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(5, 3), Durability = 100000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(2, 2), Shape = new Shape() } }
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
                Damage = Constant(damage), Range = Constant(1000), MinRange = Constant(0),
                Velocity = Constant(velocity), Spread = Constant(0), DamageSpread = Constant(0),
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
                Accuracy = Constant(accuracy), Resolution = Constant(1000), Precision = Constant(1), Tracking = Constant(100000)
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
            Name = "Marker", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 100,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 16; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, TestSettings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = zoneName, Owner = null }, null);

        EquippableItem Make(string name, int lot, float durability) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>(name)), Durability = durability, Lot = lot
        };

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooter = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 100000, Lot = 1 }, new EntitySettings());
        var gun = Make("Gun", 2, 1);
        Assert.True(shooter.TryEquip(gun, new int2(2, 2)));
        var weaponItem = shooter.Equipment.Single(x => x.EquippableItem == gun);
        var targeting = Make("Targeting", 3, 1);
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var target = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 100000, Lot = 4 }, new EntitySettings());
        Assert.True(target.TryEquip(Make("Gun", 5, 1), new int2(2, 2)));

        EquippedItem port = null, starboard = null;
        if (markers)
        {
            var p = Make("Marker", 6, 100);
            var s = Make("Marker", 7, 100);
            Assert.True(target.TryEquip(p, new int2(1, 1)), "port marker must fit an interior cell");
            Assert.True(target.TryEquip(s, new int2(3, 1)), "starboard marker must fit an interior cell");
            port = target.Equipment.Single(x => x.EquippableItem == p);
            starboard = target.Equipment.Single(x => x.EquippableItem == s);
        }

        if (equipShield)
        {
            var reactor = Make("Reactor", 8, 10);
            Assert.True(target.TryFindSpace(reactor, out var rpos));
            Assert.True(target.TryEquip(reactor, rpos));
            var shield = Make("Shield", 9, 10);
            Assert.True(target.TryFindSpace(shield, out var spos));
            Assert.True(target.TryEquip(shield, spos));
        }

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3(37, 0, -11);
        target.Position = float3(37, 0, 89); // 100 units straight ahead of the shooter
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);
        if (equipShield) target.Shield.Item.Enabled.Value = true;

        zone.Update(0f);

        return new Engagement
        {
            Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem,
            Weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon),
            PortMarker = port, StarboardMarker = starboard
        };
    }

    private static void Charge(Engagement e)
    {
        for (var i = 0; i < 20; i++) e.Zone.Update(.1f);
    }

    // ---- The shield pays for what it absorbs. ----

    // Deleting Shield.TakeHit from FireControl.Apply survived every earlier suite, because they asserted only
    // that an absorbed hit left the hull untouched -- never that the shield paid for it. A shield that absorbs
    // must eventually run dry. Stepped with dt = 0 so the (near-instant) refill cannot top the reserve back up
    // between shots. Mutation: delete TakeHit from the discrete path; the reserve never drains.
    [Fact]
    public void DiscreteShieldAbsorptionDrainsTheReserve()
    {
        var e = Build(damage: 5, equipShield: true, shieldCapacity: 30);
        Charge(e);
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 5));
        var hull = e.Target.Hull.Durability;

        var shots = 0;
        while (e.Target.Shield.CanTakeHit(DamageType.Kinetic, 5) && shots < 200)
        {
            FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
            e.Zone.Update(0f);
            shots++;
        }

        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 5)); // the reserve ran dry
        Assert.False(e.Target.Shield.Broken);                             // by paying, not by breaking
        Assert.Equal(hull, e.Target.Hull.Durability);                     // and every hit was absorbed
    }

    // The same rule on Splash's path, whose absorb branch had no test coverage at all. Mutation: delete
    // TakeHit from Splash's absorb branch.
    [Fact]
    public void SplashShieldAbsorptionDrainsTheReserve()
    {
        var e = Build(equipShield: true, shieldCapacity: 30);
        Charge(e);
        Assert.True(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 5));
        var hull = e.Target.Hull.Durability;

        var blasts = 0;
        while (e.Target.Shield.CanTakeHit(DamageType.Kinetic, 5) && blasts < 200)
        {
            FireControl.Splash(e.Zone, e.Target.Position, radius: 20, damage: 5, damageType: DamageType.Kinetic);
            blasts++;
        }

        Assert.False(e.Target.Shield.CanTakeHit(DamageType.Kinetic, 5));
        Assert.False(e.Target.Shield.Broken);
        Assert.Equal(hull, e.Target.Hull.Durability);
    }

    // ---- A shot whose target is gone resolves as a miss (Soul C4). ----

    // The 0b table says a shot whose target has left the zone resolves as a miss "whichever stage it is at."
    // Step only rewrote the outcome on the uncommitted branch, so a hit committed inside the horizon and then
    // orphaned by the target dying was republished as a hit on ShotResolved -- a hit marker on a corpse. The
    // committed outcome is not mutated (R4 keeps it immutable); the resolution is published as its own miss.
    [Fact]
    public void ShotAtTargetThatLeavesResolvesAsMiss()
    {
        var e = Build(velocity: 20); // 100 units at 20 u/s: flight 5 s, commits at 4.5 s
        ShotOutcome committed = null, resolved = null;
        using var c = e.Zone.ShotCommitted.Subscribe(o => committed = o);
        using var r = e.Zone.ShotResolved.Subscribe(o => resolved = o);

        FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        e.Zone.Update(4.7f); // past the commit, short of arrival
        Assert.NotNull(committed);
        Assert.True(committed.Hit, "precondition: this fixture's shot must commit as a hit");
        Assert.Null(resolved);

        e.Zone.Entities.Remove(e.Target); // the target is gone before the shot arrives
        e.Zone.Update(1f);

        Assert.NotNull(resolved);
        Assert.Equal(committed.ShotId, resolved.ShotId);
        Assert.False(resolved.Hit, "a shot whose target left the zone must resolve as a miss, not publish its committed hit");
        Assert.True(committed.Hit, "the committed outcome itself is not rewritten (R4)");
    }

    // ---- Splash damages the side the blast came from. ----

    // The operator's "damage on the wrong side of the ship." Flipping the sign in Splash's
    // `right = float2(forward.y, -forward.x)` survived every earlier suite, and it survives this test too at
    // the default facing (0, 1): the flip only changes which side is hit when a ship faces more along world x
    // than z. So the target is spun through facings where the flip bites as well as ones where it does not,
    // and blasted from each side in turn. The blast side is computed here independently of FireControl --
    // starboard is the facing rotated clockwise -- so this is not the code checking itself.
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(.8f, .6f)]
    [InlineData(-.8f, -.6f)]
    [InlineData(-.6f, .8f)]
    public void SplashDamagesTheSideTheBlastCameFrom(float fx, float fy)
    {
        foreach (var fromStarboard in new[] { true, false })
        {
            var e = Build(markers: true);
            var facing = normalize(float2(fx, fy));
            e.Target.Direction = facing;
            var starboard = float2(facing.y, -facing.x);
            var side = fromStarboard ? starboard : -starboard;
            var blast = e.Target.Position + float3(side.x, 0, side.y) * 10f;

            FireControl.Splash(e.Zone, blast, radius: 20, damage: 10, damageType: DamageType.Kinetic);

            var nearMarker = fromStarboard ? e.StarboardMarker : e.PortMarker;
            var farMarker = fromStarboard ? e.PortMarker : e.StarboardMarker;
            var label = $"facing ({fx}, {fy}), blast from {(fromStarboard ? "starboard" : "port")}";
            Assert.True(nearMarker.EquippableItem.Durability < 100, $"{label}: the marker facing the blast took no damage");
            Assert.Equal(100f, farMarker.EquippableItem.Durability);
        }
    }

    // ---- Zones roll differently. ----

    // The roll is a function of (zone, shot id). Replacing `CombatSeed *` with `/` in Commit's seed drops the
    // zone out of it -- integer division by a large constant is zero for almost every seed -- so every zone
    // rolls the same sequence. That survived, because the die test only checks uniformity within one zone.
    // Same shot ids in two zones must not produce the same hit sequence.
    [Fact]
    public void DifferentZonesRollDifferently()
    {
        bool[] Sequence(string zoneName)
        {
            var e = Build(zoneName: zoneName, accuracy: .5f);
            var p = FireControl.HitProbability(e.Weapon, e.Shooter, e.Target); // accuracy times the other factors
            var hits = new List<bool>();
            using var r = e.Zone.ShotResolved.Subscribe(o => hits.Add(o.Hit));
            for (var i = 0; i < 400; i++)
            {
                FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
                e.Zone.Update(0f);
            }
            Assert.Equal(400, hits.Count);
            var fraction = hits.Count(h => h) / 400f;
            Assert.True(Math.Abs(fraction - p) < .08f, $"zone {zoneName}: hit fraction {fraction:F3} is not near p = {p:F3}");
            return hits.ToArray();
        }

        var a = Sequence("Rhea");
        var b = Sequence("Mimas");
        var differ = a.Zip(b, (x, y) => x != y).Count(d => d) / 400f;
        // Two independent rolls at p ~ .4 disagree 2p(1-p) ~ 48% of the time; identical seeds disagree never.
        Assert.True(differ > .3f, $"only {differ:P1} of shots differ between zones -- the zone is not reaching the roll");
    }
}
