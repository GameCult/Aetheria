/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;

// Cut 1 (docs/fire-control-cut.md): pins mount direction, arc authoring/defaulting and the headless
// engagement this cut exists to unbreak. Builds its own hull/catalog (the IffAndCombatTests Skiff/Gun
// fixture is fixed to one hardpoint) rather than reusing those private helpers.
public sealed class FireControlTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrol-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlTests() => Directory.CreateDirectory(_root);

    private CultCache _openCache;

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .5f,
        FiringArc = 120
    };

    // A hull with one weapon hardpoint per (rotation, firingArc) pair, laid out along the X axis of a 1xN
    // shape so each gets its own cell. FiringArc 0 on a hardpoint means "use GameplaySettings.FiringArc".
    private (ItemManager items, Ship ship, EquippedItem[] weapons) BuildShipWithHardpoints(
        params (ItemRotation rotation, float firingArc)[] hardpoints)
    {
        var hullShape = new Shape(Math.Max(hardpoints.Length, 3), 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        // Mass defaults to 0 (ItemData.cs:285), which zeroes ThermalMass (Entity.cs MapEntity) and NaNs
        // UpdateTemperature -- author a real mass so the entity's own temperature stays a real number.
        var hullData = new HullData { Name = "TestHull", HullType = HullType.Ship, Shape = hullShape, Durability = 1, Mass = 1000 };
        for (var i = 0; i < hardpoints.Length; i++)
            hullData.Hardpoints.Add(new HardpointData
            {
                Type = HardpointType.Sensors, Position = new int2(i, 0), Shape = new Shape(),
                Rotation = hardpoints[i].rotation, FiringArc = hardpoints[i].firingArc
            });

        using (var buildCache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            buildCache.Upsert(new VerseGrammar { Revision = 1 });
            buildCache.Upsert(hullData);
            buildCache.Upsert(new GearData
            {
                // The default [0,100] temperature bounds (ItemData.cs:379-382) don't necessarily cover
                // wherever this hull's heat model settles after a tick: author a wide plateau so
                // EquippableItemData.Performance stays 1 and the item actually comes Online.
                Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Behaviors = { new InstantWeaponData() }
            });
            // A second design, so a target can carry two guns whose true figures differ (claim tests).
            buildCache.Upsert(new GearData
            {
                Name = "WeakGun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Behaviors = { new InstantWeaponData() }
            });
            buildCache.FlushAsync().Wait();
        }

        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 }; // hull
        for (var i = 0; i < hardpoints.Length; i++)
            ledger.Lots[2 + i] = new Lot { Origin = new Attributed(), Quality = 1 }; // guns
        var items = new ItemManager(_openCache, ledger, TestSettings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("TestHull"));
        var hull = new EquippableItem { Data = hullRef, Durability = 1, Lot = 1 };
        var ship = new Ship(items, zone, hull, new EntitySettings());

        // Entity.MapEntity equips the hull itself into Equipment at int2.zero, so hardpoint 0 (also at
        // (0,0) here) shares a cell with the hull's own equipped-item entry: match guns by identity, not
        // position.
        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var weapons = new EquippedItem[hardpoints.Length];
        for (var i = 0; i < hardpoints.Length; i++)
        {
            var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 + i };
            Assert.True(ship.TryEquip(gun, new int2(i, 0)));
            weapons[i] = ship.Equipment.Single(e => e.EquippableItem == gun);
        }

        zone.Admit(ship, piloted: false);

        return (items, ship, weapons);
    }

    // R6: mount direction is the item's authored rotation, not the hull's facing. A target 90 degrees to
    // starboard bears on the Clockwise hardpoint's weapon and not the None hardpoint's, and vice versa dead
    // ahead. Mutation: make InArc read Entity.Direction instead of the item's rotation, or swap Rotate's
    // Clockwise/CounterClockwise cases -- either breaks one of the four assertions below.
    [Fact]
    public void ArcFollowsMountRotation()
    {
        var (_, _, weapons) = BuildShipWithHardpoints((ItemRotation.None, 0f), (ItemRotation.Clockwise, 0f));
        var forward = weapons[0];
        var starboardMounted = weapons[1];

        var deadAhead = float3(0, 0, 1);
        var abeamStarboard = float3(1, 0, 0);

        Assert.True(FireControl.InArc(forward, deadAhead));
        Assert.False(FireControl.InArc(forward, abeamStarboard));
        Assert.True(FireControl.InArc(starboardMounted, abeamStarboard));
        Assert.False(FireControl.InArc(starboardMounted, deadAhead));
    }

    // FiringArc is full width: at the default 120, a target 59 degrees off the mount bears and one at 61
    // does not. Mutation: compare against cos(radians(arc)) instead of cos(radians(arc/2)).
    [Fact]
    public void ArcBoundaryIsHalfWidth()
    {
        var (_, _, weapons) = BuildShipWithHardpoints((ItemRotation.None, 0f));
        var weapon = weapons[0];

        float3 AtAngle(float degrees)
        {
            var r = radians(degrees);
            return float3(sin(r), 0, cos(r));
        }

        Assert.True(FireControl.InArc(weapon, AtAngle(59)));
        Assert.False(FireControl.InArc(weapon, AtAngle(61)));
    }

    // R7: the simulation is 2D. A target inside the arc horizontally but far above the firer still bears.
    // Mutation: normalize and dot the full 3D vector instead of zeroing height first.
    [Fact]
    public void ArcIsPlanar()
    {
        var (_, _, weapons) = BuildShipWithHardpoints((ItemRotation.None, 0f));
        var weapon = weapons[0];

        var highButAhead = float3(0, 1000, 1);

        Assert.True(FireControl.InArc(weapon, highButAhead));
    }

    // The per-hardpoint override wins over the default, and an authored 0 means "use the default," not
    // "zero-degree arc." A hardpoint authored FiringArc: 360 bears 170 degrees off; a hardpoint left at 0
    // falls back to GameplaySettings.FiringArc (120) and fails there, but still bears a target only 5
    // degrees off. Mutation: ignore HardpointData.FiringArc entirely (both hardpoints fail at 170), or
    // treat an authored 0 as a literal zero-width arc (the second hardpoint then fails even at 5 degrees).
    [Fact]
    public void HardpointOverrideBeatsDefault()
    {
        var (_, _, weapons) = BuildShipWithHardpoints((ItemRotation.None, 360f), (ItemRotation.None, 0f));
        var turret = weapons[0];
        var defaultArc = weapons[1];

        float3 AtAngle(float degrees)
        {
            var r = radians(degrees);
            return float3(sin(r), 0, cos(r));
        }

        Assert.True(FireControl.InArc(turret, AtAngle(170)));
        Assert.False(FireControl.InArc(defaultArc, AtAngle(170)));
        Assert.True(FireControl.InArc(defaultArc, AtAngle(5)));
    }

    // A shooter with one in-range, in-arc gun, a detected target at `targetPosition`, and a Minion driving the shooter.
    private (Ship shooter, Ship target, Zone zone) BuildCombatEncounter(float3 targetPosition, float projectileSpeed = 0f, bool twoTargetGuns = false)
    {
        var mounts = twoTargetGuns ? new[] { (ItemRotation.None, 0f), (ItemRotation.None, 0f) } : new[] { (ItemRotation.None, 0f) };
        var (items, shooter, weapons) = BuildShipWithHardpoints(mounts);
        var weaponData = (InstantWeaponData) weapons[0].Data.Behaviors.Single(b => b is InstantWeaponData);
        weaponData.Damage = new PerformanceStat { Min = 10, Max = 10 };
        weaponData.Cooldown = new PerformanceStat { Min = 1, Max = 1 };
        weaponData.Range = new PerformanceStat { Min = 1000, Max = 1000 };
        weaponData.MinRange = new PerformanceStat { Min = 0, Max = 0 };
        weaponData.Velocity = new PerformanceStat { Min = projectileSpeed, Max = projectileSpeed };
        weaponData.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };

        var zone = shooter.Zone;
        var targetHullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("TestHull"));
        var target = new Ship(items, zone, new EquippableItem { Data = targetHullRef, Durability = 1, Lot = 1 }, new EntitySettings());
        // Entity._orderedEquipment is populated only by TryEquip (Entity.cs:813); a ship that never equips
        // anything leaves it null, and Entity.Update (:932) throws on that regardless of this cut. Equip a
        // gun so the target is a real, ordinarily-constructed ship rather than a workaround-shaped one.
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        Assert.True(target.TryEquip(new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 1 }, new int2(0, 0)));
        if (twoTargetGuns)
        {
            // Both true guns are finite, authored figures: the strong one rates 10, the weak one 1.
            weaponData.Count = new PerformanceStat { Min = 1, Max = 1 };
            var weakRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("WeakGun"));
            var weakData = (InstantWeaponData) items.ItemData.GetByName<GearData>("WeakGun").Behaviors.Single();
            weakData.Damage = new PerformanceStat { Min = 1, Max = 1 };
            weakData.Count = new PerformanceStat { Min = 1, Max = 1 };
            weakData.Cooldown = new PerformanceStat { Min = 1, Max = 1 };
            weakData.Range = new PerformanceStat { Min = 1000, Max = 1000 };
            weakData.MinRange = new PerformanceStat { Min = 0, Max = 0 };
            weakData.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };
            Assert.True(target.TryEquip(new EquippableItem { Data = weakRef, Durability = 1, Lot = 2 }, new int2(1, 0)));
        }
        zone.Admit(target, piloted: false);

        shooter.Position = float3.zero;
        target.Position = targetPosition;

        shooter.SetTarget(target);
        // Cut 3: CombatState now gates on FireControl.HitProbability, which is zero for an undetected target
        // (the target is not in VisibleEntities) regardless of arc -- cross TargetDetectionInfoThreshold
        // directly, the same way a Sensor's own info accrual eventually would.
        shooter.EntityInfoGathered[target] = 1f;
        // This fixture's shooter carries no targeting system, so Accuracy falls back to UnaidedAccuracy (Q4:
        // authored deliberately bad). This test's own concern is the headless pipeline reaching a fire
        // decision at all (Cut 1's regression), not AgentMinHitProbability tuning -- zero the threshold so an
        // unaided shot still counts as "worth it."
        items.GameplaySettings.AgentMinHitProbability = 0f;
        zone.Agents.Add(new Minion(shooter));
        return (shooter, target, zone);
    }

    // The cut's real deliverable: Entity.HardpointTransforms is gone, so a Minion can step through a full
    // Zone.Update tick and reach the fire decision without Unity ever having run. Before this cut,
    // Combat.cs:100-102 threw KeyNotFoundException on the first combat tick headless, because the AI's aim
    // direction was a readback from a renderer that never ran. Mutation: restore the HardpointTransforms
    // read in Behavior.Direction / Combat.cs -- this throws again outside Unity.
    [Fact]
    public void CombatStateStepsHeadless()
    {
        // dead ahead of the None-rotation hardpoint, well within range and arc
        var (shooter, _, zone) = BuildCombatEncounter(float3(0, 0, 10));

        var ex = Record.Exception(() =>
        {
            zone.Update(1f); // tick 1: transitions the Minion into CombatState and evaluates weapon stats for the first time
            zone.Update(1f); // tick 2: CombatState.Update reaches FireControl.InArc -- the line that used to throw headless
        });

        Assert.Null(ex);
        var weapon = shooter.Weapons.Single();
        Assert.True(weapon.Firing); // arc + range satisfied: the fight actually decided to fire, not just avoided a crash
    }

    // Combat picks a target item only among the items its ship holds a reveal of: the target's gun is in range and
    // the target is fully tracked, yet nothing is aimed at until the shooter has seen the gun act, and a destroyed
    // gun is cleared by Combat's own choice. Mutation:
    // Combat tests weapons without IsRevealed, or IsRevealed falls back to the track.
    [Fact]
    public void TheAIAimsOnlyAtWhatItSaw()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 10));
        var targetGun = target.Equipment.Single(x => x.Behaviors.Any(b => b is Weapon));

        zone.Update(1f);
        zone.Update(1f);
        Assert.Null(shooter.TargetItem.Value);

        shooter.RecordReveal(target, targetGun, SignatureChannel.LaunchFlash, targetGun.Data);
        zone.Update(1f);

        Assert.Same(targetGun, shooter.TargetItem.Value);

        // The gun is destroyed: nothing qualifies any more, so Combat clears the aim point itself rather than
        // leaving the dead gun in the raw field.
        targetGun.EquippableItem.Durability = 0f;
        zone.Update(1f);

        Assert.Null(shooter.TargetItem.Value);
    }

    // A design as an observer might be told it is: an instant weapon with rated figures, held in memory (a claim is data,
    // not a catalog item). Rated damage per second at any range is damage / cooldown (one round, flat curve).
    private static GearData ClaimedDesign(string name, float damage, float range = 1000f, BezierCurve curve = null) => new GearData
    {
        Name = name,
        Behaviors =
        {
            new InstantWeaponData
            {
                Damage = new PerformanceStat { Min = 0, Max = damage }, Count = new PerformanceStat { Min = 1, Max = 1 },
                Cooldown = new PerformanceStat { Min = 1, Max = 1 }, Range = new PerformanceStat { Min = 0, Max = range },
                MinRange = new PerformanceStat { Min = 0, Max = 0 },
                DamageCurve = curve ?? new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            }
        }
    };

    // Ruling ew-aim: the AI classifies from its book, not the target's gear (aetheria-ew, classify-from-belief). A gun
    // the target claims is not a weapon is not something Combat picks as a weapon, though the gun is truly one and
    // the shooter has seen it act; clearing the claim and seeing it act again restores the choice. Mutation: Combat
    // walks target.Equipment (or reveal.Item's true behaviours) instead of RevealedWeapons.
    [Fact]
    public void TheAIChoosesFromWhatItBelieves()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 10));
        var targetGun = target.Equipment.Single(x => x.Behaviors.Any(b => b is Weapon));

        target.Claim(targetGun, new GearData { Name = "NotAGun" });
        shooter.RecordReveal(target, targetGun, SignatureChannel.LaunchFlash, target.ClaimOf(targetGun));
        zone.Update(1f);
        zone.Update(1f);
        Assert.Null(shooter.TargetItem.Value);

        target.Claim(targetGun, null);
        shooter.RecordReveal(target, targetGun, SignatureChannel.LaunchFlash, target.ClaimOf(targetGun));
        zone.Update(1f);
        Assert.Same(targetGun, shooter.TargetItem.Value);
    }

    // Ruling ew-claim-weapon-measure-ruled (rated-design): the AI ranks revealed weapons by their claimed design's
    // rated figures, so the truly weaker gun claimed as the stronger design is the one chosen, and the unclaimed pair
    // picks the truly stronger. Mutation: rank by the true weapon's live figure, or by the carrier's stats.
    [Fact]
    public void AWeakerGunClaimedAsAStrongerDesignIsChosen()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 10), twoTargetGuns: true);
        var strong = target.Equipment.Single(x => x.Data.Name == "Gun");
        var weak = target.Equipment.Single(x => x.Data.Name == "WeakGun");

        void See()
        {
            shooter.RecordReveal(target, strong, SignatureChannel.LaunchFlash, target.ClaimOf(strong));
            shooter.RecordReveal(target, weak, SignatureChannel.LaunchFlash, target.ClaimOf(weak));
            zone.Update(1f);
            zone.Update(1f);
        }

        See();
        Assert.Same(strong, shooter.TargetItem.Value); // unclaimed: the truly stronger gun (rated 10 against 1)

        target.Claim(strong, ClaimedDesign("Pea", 2f));
        target.Claim(weak, ClaimedDesign("Cannon", 90f));
        See();
        Assert.Same(weak, shooter.TargetItem.Value);
    }

    // The ranking reads the claimed design at the distance to the target: a short-range design that out-damages a flat one
    // up close is worthless at 10 when its Range is 12, and the weaker flat design is chosen. Mutation: Combat halves
    // targetDistance (the short design then rates 6.2 at 5 and wins), or the rated read ignores the range.
    [Fact]
    public void TheAIRanksClaimsAtTheTargetsDistance()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 10), twoTargetGuns: true);
        var strong = target.Equipment.Single(x => x.Data.Name == "Gun");
        var weak = target.Equipment.Single(x => x.Data.Name == "WeakGun");
        var falling = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 0, 0, 0) } };

        target.Claim(strong, ClaimedDesign("Dart", 10f, range: 12f, curve: falling)); // ~.7 at 10, ~6.2 at 5
        target.Claim(weak, ClaimedDesign("Slug", 5f));                                // 5 at any range
        shooter.RecordReveal(target, strong, SignatureChannel.LaunchFlash, target.ClaimOf(strong));
        shooter.RecordReveal(target, weak, SignatureChannel.LaunchFlash, target.ClaimOf(weak));
        zone.Update(1f);
        zone.Update(1f);

        Assert.Same(weak, shooter.TargetItem.Value);
    }

    // Ruling ew-claim-weapon-measure-ruled: no observer reads a target weapon's live state. Two unclaimed guns whose
    // rated order (10 against 1) is the reverse of their live order (the strong one worn down to a hundredth) are
    // ranked by the design, so the aim stays on the rated stronger gun. Mutation: rank an unclaimed reveal by the
    // true weapon's live RangeDamagePerSecond.
    [Fact]
    public void TheAIRanksByTheDesignNotTheTargetsLiveGun()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 10), twoTargetGuns: true);
        var strong = target.Equipment.Single(x => x.Data.Name == "Gun");
        var weak = target.Equipment.Single(x => x.Data.Name == "WeakGun");
        var strongData = (InstantWeaponData) strong.Data.Behaviors.Single(b => b is InstantWeaponData);
        target.Resolver.AttachModifier(strong, strongData.Damage, "worn", StatModifierType.Multiplier, .01f);

        shooter.RecordReveal(target, strong, SignatureChannel.LaunchFlash, target.ClaimOf(strong));
        shooter.RecordReveal(target, weak, SignatureChannel.LaunchFlash, target.ClaimOf(weak));
        zone.Update(1f);
        zone.Update(1f);

        var liveStrong = strong.Behaviors.OfType<Weapon>().Single().RangeDamagePerSecond(10f);
        var liveWeak = weak.Behaviors.OfType<Weapon>().Single().RangeDamagePerSecond(10f);
        Assert.True(liveStrong < liveWeak); // the fixture: live and rated disagree
        Assert.Same(strong, shooter.TargetItem.Value);
    }

    // Ties are explicit: two revealed weapons of equal rated figure are taken in the subject's equipment order, whatever
    // order the shooter saw them in. Mutation: ties go to the last (>=), or the walk follows the book's slot order.
    [Fact]
    public void EqualClaimsAreTakenInTheTargetsEquipmentOrder()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 10), twoTargetGuns: true);
        var first = target.Equipment.Single(x => x.Data.Name == "Gun");
        var second = target.Equipment.Single(x => x.Data.Name == "WeakGun");
        Assert.True(target.Equipment.IndexOf(first) < target.Equipment.IndexOf(second));
        var design = ClaimedDesign("Twin", 5f);
        target.Claim(first, design);
        target.Claim(second, design);

        shooter.RecordReveal(target, second, SignatureChannel.LaunchFlash, target.ClaimOf(second)); // seen first
        shooter.RecordReveal(target, first, SignatureChannel.LaunchFlash, target.ClaimOf(first));
        zone.Update(1f);
        zone.Update(1f);

        Assert.Equal(new[] { first, second }, shooter.RevealedWeapons(target).Select(r => r.Item).ToArray());
        Assert.Same(first, shooter.TargetItem.Value);
    }

    // Controls cut (helm split): Combat aims the shooter at the intercept (Aim) and commands the hull toward it
    // through the one steering law (Turn); it never turns the hull by writing Aim.
    [Fact]
    public void CombatAimsAtTheInterceptAndTurnsTowardIt()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(10, 0, 10)); // 45 degrees to starboard, in arc
        var direction = shooter.Direction;

        zone.Update(1f);
        zone.Update(1f);

        var bearing = normalize(target.Position.xz - shooter.Position.xz);
        var aim = normalize(shooter.Aim.xz);
        Assert.True(dot(aim, bearing) > .999f, "Aim points at the stationary target's intercept");
        Assert.Equal(direction, shooter.Direction); // no thrusters on this hull: only Turn was commanded
        Assert.Equal(Steering.Toward(shooter, shooter.Aim.xz), shooter.Turn);
        Assert.True(abs(shooter.Turn) > .5f);
    }

    // Against a target that moves, Combat aims at where the round will meet it (FireControl.PredictedIntercept),
    // not where the target is: the aim leads, and the hull's Turn follows the intercept bearing.
    [Fact]
    public void CombatLeadsAMovingTarget()
    {
        var (shooter, target, zone) = BuildCombatEncounter(float3(0, 0, 100), projectileSpeed: 20f);
        zone.Update(1f); // transitions the Minion into CombatState and evaluates weapon stats

        // Agents update before entities, so Combat reads exactly this state on the next tick.
        target.Position = float3(0, 0, 100);
        target.Velocity = float2(4, 0);
        var weapon = shooter.Weapons.Single();
        var intercept = normalize((FireControl.PredictedIntercept(weapon, shooter, target) - shooter.Position).xz);
        var bearing = normalize(target.Position.xz - shooter.Position.xz);
        Assert.True(dot(intercept, bearing) < cos(radians(5f)), "the fixture really leads the target");

        zone.Update(1f);

        var aim = normalize(shooter.Aim.xz);
        Assert.True(dot(aim, intercept) > .9999f, $"aim {shooter.Aim}");
        Assert.Equal(Steering.Toward(shooter, intercept), shooter.Turn);
        Assert.True(shooter.Turn > 0f);
    }

    // A Minion whose target goes away returns to its root state, which pilots nothing: the hull must stop
    // turning rather than keep the rate Combat last commanded.
    [Fact]
    public void AMinionThatLosesItsTargetStopsTurning()
    {
        var (shooter, _, zone) = BuildCombatEncounter(float3(10, 0, 10));
        zone.Update(1f);
        zone.Update(1f);
        Assert.True(abs(shooter.Turn) > .5f, "Combat commanded a turn");

        shooter.SetTarget(TargetRef.None);
        zone.Update(1f); // Combat has no target and the Minion transitions to its root state
        zone.Update(1f);

        Assert.Equal(0f, shooter.Turn);
    }
}
