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
            buildCache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
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

        zone.Entities.Add(ship);
        ship.Activate();

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

    // The cut's real deliverable: Entity.HardpointTransforms is gone, so a Minion can step through a full
    // Zone.Update tick and reach the fire decision without Unity ever having run. Before this cut,
    // Combat.cs:100-102 threw KeyNotFoundException on the first combat tick headless, because the AI's aim
    // direction was a readback from a renderer that never ran. Mutation: restore the HardpointTransforms
    // read in Behavior.Direction / Combat.cs -- this throws again outside Unity.
    [Fact]
    public void CombatStateStepsHeadless()
    {
        var (items, shooter, weapons) = BuildShipWithHardpoints((ItemRotation.None, 0f));
        var weaponData = (InstantWeaponData) weapons[0].Data.Behaviors.Single(b => b is InstantWeaponData);
        weaponData.Damage = new PerformanceStat { Min = 10, Max = 10 };
        weaponData.Cooldown = new PerformanceStat { Min = 1, Max = 1 };
        weaponData.Range = new PerformanceStat { Min = 1000, Max = 1000 };
        weaponData.MinRange = new PerformanceStat { Min = 0, Max = 0 };
        weaponData.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };

        var zone = shooter.Zone;
        var targetHullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("TestHull"));
        var target = new Ship(items, zone, new EquippableItem { Data = targetHullRef, Durability = 1, Lot = 1 }, new EntitySettings());
        // Entity._orderedEquipment is populated only by TryEquip (Entity.cs:813); a ship that never equips
        // anything leaves it null, and Entity.Update (:932) throws on that regardless of this cut. Equip a
        // gun so the target is a real, ordinarily-constructed ship rather than a workaround-shaped one.
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        Assert.True(target.TryEquip(new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 1 }, new int2(0, 0)));
        zone.Entities.Add(target);
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, 10); // dead ahead of the None-rotation hardpoint, well within range and arc

        shooter.Target.Value = target;
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

        var ex = Record.Exception(() =>
        {
            zone.Update(1f); // tick 1: transitions the Minion into CombatState and evaluates weapon stats for the first time
            zone.Update(1f); // tick 2: CombatState.Update reaches FireControl.InArc -- the line that used to throw headless
        });

        Assert.Null(ex);
        var weapon = shooter.Weapons.Single();
        Assert.True(weapon.Firing); // arc + range satisfied: the fight actually decided to fire, not just avoided a crash
    }
}
