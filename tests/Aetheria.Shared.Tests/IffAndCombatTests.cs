using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// Hand-testing combat needs entities to target and mark. This pins the IFF override rules on Entity
// (SetIff/IsHostileTo), the event-driven grudge behaviour for non-player entities, detection-gating of
// perceived stance, the shooter-stance weapon fire gate, and the neutral-wanderer faction pool in
// ZoneGenerator. No Zone/Galaxy save-load is exercised here; RunSaveTests covers that separately.
public sealed class IffAndCombatTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-iff-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    private static readonly int2 HardpointCell = new int2(0, 0);

    public IffAndCombatTests()
    {
        Directory.CreateDirectory(_root);
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 1,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
        });
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            Behaviors = { new InstantWeaponData() }
        });
        cache.FlushAsync().Wait();
    }

    private CultCache _openCache;

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    // A world with one empty zone (no faction/security machinery engaged: GalaxyZone.Owner is null and
    // every test ship is non-player, so PresencePermitted stays at its default true and derived hostility
    // between non-owner factions is always false). Individual tests layer overrides/factions on top.
    private (ItemManager items, Zone zone) BuildWorld()
    {
        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 }; // hull
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 }; // gun
        var items = new ItemManager(_openCache, ledger, TestSettings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        return (items, zone);
    }

    // As RunSaveTests.TestSettings, plus a real detection threshold: 0 would never cross (0 < 0 is
    // false), so the Detect() helper's 0 -> 1 EntityInfoGathered jump would silently do nothing.
    private static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .5f
    };

    private Ship NewShip(ItemManager items, Zone zone, Faction faction = null, bool isPlayerShip = false, bool equipGun = false)
    {
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Skiff"));
        var hull = new EquippableItem { Data = hullRef, Durability = 1, Lot = 1 };
        var ship = new Ship(items, zone, hull, new EntitySettings()) { Faction = faction, IsPlayerShip = isPlayerShip };

        if (equipGun)
        {
            var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
            var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
            Assert.True(ship.TryEquip(gun, HardpointCell));
        }

        zone.Entities.Add(ship);
        ship.Activate();
        return ship;
    }

    // Marks entity `observer` as having detected `target`, the same way Sensor ramps EntityInfoGathered
    // past TargetDetectionInfoThreshold in the real game.
    private static void Detect(Entity observer, Entity target) => observer.EntityInfoGathered[target] = 1;

    [Fact]
    public void OverrideDecidesOutrightOverDerivedRule()
    {
        var (items, zone) = BuildWorld();
        var a = NewShip(items, zone, new Faction { Name = "A" });
        var b = NewShip(items, zone, new Faction { Name = "B" });

        Assert.False(a.IsHostileTo(b)); // baseline: neither owns the zone, so derived hostility is false

        a.SetIff(b, true);

        Assert.True(a.IsHostileTo(b));
        Assert.True(a.EntityHostility[b]); // updated immediately, no Update() tick required
    }

    [Fact]
    public void ClearingOverrideRestoresDerivedRule()
    {
        var (items, zone) = BuildWorld();
        var a = NewShip(items, zone, new Faction { Name = "A" });
        var b = NewShip(items, zone, new Faction { Name = "B" });

        a.SetIff(b, true);
        Assert.True(a.IsHostileTo(b));

        a.SetIff(b, null);

        Assert.False(a.IsHostileTo(b));
        Assert.False(a.EntityHostility[b]);
    }

    [Fact]
    public void LeavingZoneClearsOverrides()
    {
        var (items, zone) = BuildWorld();
        var a = NewShip(items, zone, new Faction { Name = "A" });
        var b = NewShip(items, zone, new Faction { Name = "B" });

        a.SetIff(b, true);
        Assert.True(a.EntityHostility[b]);

        zone.Entities.Remove(b); // b leaves the zone: a's override on b must be cleared
        zone.Entities.Add(b); // b returns as a "new" contact from a's perspective

        Assert.False(a.EntityHostility[b]); // recomputed fresh from the derived (non-hostile) rule
        Assert.False(a.IsHostileTo(b));
    }

    [Fact]
    public void DerivedReciprocityDoesNotMirrorAnotherEntitysOverride()
    {
        var (items, zone) = BuildWorld();
        var a = NewShip(items, zone, new Faction { Name = "A" });
        var b = NewShip(items, zone, new Faction { Name = "B" });

        // B declares hostile toward A via override. Rule (b) (reciprocity-on-marked-hostile) is deleted:
        // A's own derived hostility toward B must not mirror B's override through the recursive
        // "other.IsHostileTo(this, true)" reciprocity check baked into the derived rule.
        b.SetIff(a, true);

        Assert.True(b.IsHostileTo(a)); // B's own stance: override decides
        Assert.False(a.IsHostileTo(b)); // A's derived stance is untouched by B's override
        Assert.False(a.EntityHostility[b]);
    }

    [Fact]
    public void NonPlayerEntityGrudgesOnlyWhileItDetectsTheHostileMarker()
    {
        var (items, zone) = BuildWorld();
        var player = NewShip(items, zone, new Faction { Name = "Player" }, isPlayerShip: true);
        var npc = NewShip(items, zone, new Faction { Name = "Npc" });

        player.SetIff(npc, true); // player declares hostile toward the npc
        Assert.True(player.IsHostileTo(npc));

        // The npc does not yet detect the player: the hostile stance must not grudge yet.
        Assert.False(npc.IsHostileTo(player));

        // Detecting the already-hostile player triggers the grudge immediately, event-driven, with no
        // Update() tick between detection and the assertion.
        Detect(npc, player);

        Assert.True(npc.IsHostileTo(player));
        Assert.True(npc.EntityHostility[player]);
    }

    [Fact]
    public void GrudgeTriggersImmediatelyWhenAlreadyDetectedAndMarkerTurnsHostile()
    {
        var (items, zone) = BuildWorld();
        var player = NewShip(items, zone, new Faction { Name = "Player" }, isPlayerShip: true);
        var npc = NewShip(items, zone, new Faction { Name = "Npc" });

        Detect(npc, player); // npc already sees the player, who is not yet hostile
        Assert.False(npc.IsHostileTo(player));

        player.SetIff(npc, true); // no Update() tick called before the assertion below

        Assert.True(npc.IsHostileTo(player));
    }

    [Fact]
    public void GrudgeIsStickyAndDoesNotForgiveWhenMarkerGoesNeutral()
    {
        var (items, zone) = BuildWorld();
        var player = NewShip(items, zone, new Faction { Name = "Player" }, isPlayerShip: true);
        var npc = NewShip(items, zone, new Faction { Name = "Npc" });

        Detect(npc, player);
        player.SetIff(npc, true);
        Assert.True(npc.IsHostileTo(player));

        player.SetIff(npc, false); // marker goes neutral

        Assert.True(npc.IsHostileTo(player)); // grudge never forgives on its own
    }

    [Fact]
    public void PlayerControlledEntitiesNeverAutoGrudge()
    {
        var (items, zone) = BuildWorld();
        var attacker = NewShip(items, zone, new Faction { Name = "Attacker" });
        var player = NewShip(items, zone, new Faction { Name = "Player" }, isPlayerShip: true);

        Detect(player, attacker);
        attacker.SetIff(player, true);

        Assert.False(player.IsHostileTo(attacker)); // no grudge subscription is ever wired for a player ship
    }

    [Fact]
    public void PerceivedStanceIsUnknownUntilDetectedThenReadsTheActualStance()
    {
        var (items, zone) = BuildWorld();
        var a = NewShip(items, zone, new Faction { Name = "A" });
        var b = NewShip(items, zone, new Faction { Name = "B" });

        b.SetIff(a, true); // B is hostile to A, but A hasn't detected B yet

        Assert.Null(a.PerceivedStanceOf(b));

        Detect(a, b);

        Assert.Equal(true, a.PerceivedStanceOf(b));
    }

    [Fact]
    public void WeaponDoesNotFireWhenShooterIsNotHostileToItsTarget()
    {
        var (items, zone) = BuildWorld();
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, equipGun: true);
        var target = NewShip(items, zone, new Faction { Name = "Target" });
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon)).Behaviors.Single(b => b is Weapon);

        shooter.Target.Value = target;
        shooter.SetIff(target, false); // explicitly neutral

        weapon.Activate();

        Assert.Equal(0f, weapon.Progress); // Trigger() was safed: no cooldown/burst was started
    }

    [Fact]
    public void WeaponFiresWhenShooterIsHostileToItsTarget()
    {
        var (items, zone) = BuildWorld();
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, equipGun: true);
        var target = NewShip(items, zone, new Faction { Name = "Target" });
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon)).Behaviors.Single(b => b is Weapon);

        shooter.Target.Value = target;
        shooter.SetIff(target, true);

        weapon.Activate();

        Assert.Equal(1f, weapon.Progress); // Trigger() ran: cooldown/burst started
    }

    [Fact]
    public void WeaponFiresWithNoTargetSet()
    {
        // Behaviour with no target is unchanged: StanceAllowsFire is vacuously true.
        var (items, zone) = BuildWorld();
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, equipGun: true);
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon)).Behaviors.Single(b => b is Weapon);

        weapon.Activate();

        Assert.Equal(1f, weapon.Progress);
    }

    [Fact]
    public void LockWeaponBuildsLockOnTheShootersOwnStanceNotTheTargets()
    {
        var (items, zone) = BuildWorld();
        // Player-controlled so detecting the target below cannot grudge shooter into hostility itself
        // (grudges are non-player-only) -- this test is isolated to the direction of the IsHostileTo call.
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, isPlayerShip: true, equipGun: true);
        var target = NewShip(items, zone, new Faction { Name = "Target" });

        // Target is hostile to shooter, but shooter itself is NOT hostile to target: with the corrected
        // direction (Entity.IsHostileTo(Entity.Target.Value)) no lock should build.
        target.SetIff(shooter, true);
        Assert.False(shooter.IsHostileTo(target));

        // Geometry and detection that WOULD build a full lock in one second if the hostility gate were
        // satisfied: target sits directly along the shooter's look direction, dead on angle, and fully
        // detected (EntityInfoGathered=1 saturates the sensor-impact term). This makes the assertion
        // below actually depend on the gate, instead of a coincidental zero from unset geometry.
        shooter.Position = float3(0, 0, 0);
        shooter.LookDirection = float3(0, 0, 1);
        target.Position = float3(0, 0, 10);
        shooter.EntityInfoGathered[target] = 1;

        var lockWeaponData = new LockWeaponData
        {
            LockSpeed = new PerformanceStat { Min = 1, Max = 1 },
            LockAngle = new PerformanceStat { Min = 180, Max = 180 },
            DirectionImpact = new PerformanceStat { Min = 1, Max = 1 }
        };
        var gunItem = shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon));
        var lockWeapon = new LockWeapon(lockWeaponData, gunItem);
        shooter.Target.Value = target;

        lockWeapon.Execute(1f);

        Assert.Equal(0f, lockWeapon.Lock);
    }

    [Fact]
    public void EligibleWandererFactionsExcludesOwnerAndNearest()
    {
        var owner = new Faction { Name = "Owner" };
        var nearest = new Faction { Name = "Nearest" };
        var third = new Faction { Name = "Third" };

        var eligible = ZoneGenerator.EligibleWandererFactions(new[] { owner, nearest, third }, owner, nearest);

        Assert.Equal(new[] { third }, eligible);
    }

    [Fact]
    public void EligibleWandererFactionsIsEmptyWhenNoFactionQualifies()
    {
        var owner = new Faction { Name = "Owner" };
        var nearest = new Faction { Name = "Nearest" };

        var eligible = ZoneGenerator.EligibleWandererFactions(new[] { owner, nearest }, owner, nearest);

        Assert.Empty(eligible);
    }
}
