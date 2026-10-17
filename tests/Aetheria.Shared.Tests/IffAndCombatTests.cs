using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using UniRx;
using Xunit;
using static CultMath.math;

// Hand-testing combat needs entities to target and mark. This pins the IFF override rules on Entity
// (SetIff/IsHostileTo), grudges from acts (a lock), belief from telemetry received with the track,
// the shooter-stance weapon fire gate, and the neutral-wanderer faction pool in
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
        cache.Upsert(new VerseGrammar { Revision = 1 });
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
            Behaviors = { new InstantWeaponData { Count = new PerformanceStat { Min = 1, Max = 1 }, Cooldown = new PerformanceStat { Min = 1, Max = 1 } } }
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
    internal (ItemManager items, Zone zone) BuildWorld()
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

    internal Ship NewShip(ItemManager items, Zone zone, Faction faction = null, bool isPlayerShip = false, bool equipGun = false, bool piloted = false)
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

        zone.Admit(ship, piloted);
        return ship;
    }

    // Marks entity `observer` as having detected `target`, the same way Sensor ramps EntityInfoGathered
    // past TargetDetectionInfoThreshold in the real game.
    internal static void Detect(Entity observer, Entity target) => observer.EntityInfoGathered[target] = 1;

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

        zone.Remove(b); // b leaves the zone: a's override on b must be cleared
        b.Deactivate();
        zone.Admit(b, piloted: false); // b returns as a "new" contact from a's perspective

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

    // A lock: the locker paints the victim in Track mode and the victim's inbox is published, the production path a lock takes.
    private static void Lock(Entity locker, Entity victim)
    {
        victim.ReceivePaint(locker, null, PaintMode.Track, 1f);
        victim.PublishInbox();
    }

    [Fact]
    public void ALockProvokesASearchPingDoesNot()
    {
        var (items, zone) = BuildWorld();
        var locker = NewShip(items, zone, new Faction { Name = "Locker" });
        var pinger = NewShip(items, zone, new Faction { Name = "Pinger" });
        var npc = NewShip(items, zone, new Faction { Name = "Npc" });

        npc.ReceivePaint(pinger, null, PaintMode.Search, 1f);
        npc.PublishInbox();
        Assert.Null(npc.IffOverride(pinger));

        Lock(locker, npc);

        Assert.True(npc.IffOverride(locker));
        Assert.True(npc.IsHostileTo(locker));
        Assert.True(npc.EntityHostility[locker]); // the cache follows at once
    }

    [Fact]
    public void NoActNoGrudge()
    {
        var (items, zone) = BuildWorld();
        var player = NewShip(items, zone, new Faction { Name = "Player" }, isPlayerShip: true);
        var npc = NewShip(items, zone, new Faction { Name = "Npc" });

        player.SetIff(npc, true); // the player's own stance toward the npc
        Detect(npc, player);
        for (var i = 0; i < 5; i++) zone.Update(.1f);

        Assert.Null(npc.IffOverride(player));
        Assert.False(npc.IsHostileTo(player));
    }

    [Fact]
    public void AGrudgeIsStickyAndDoesNotForgive()
    {
        var (items, zone) = BuildWorld();
        var locker = NewShip(items, zone, new Faction { Name = "Locker" });
        var npc = NewShip(items, zone, new Faction { Name = "Npc" });

        Lock(locker, npc);
        for (var i = 0; i < 5; i++) zone.Update(.1f); // the lock is over: no more paints

        Assert.True(npc.IffOverride(locker));
        Assert.True(npc.IsHostileTo(locker));
    }

    [Fact]
    public void PlayerControlledEntitiesNeverAutoGrudge()
    {
        var (items, zone) = BuildWorld();
        var attacker = NewShip(items, zone, new Faction { Name = "Attacker" });
        var player = NewShip(items, zone, new Faction { Name = "Player" }, isPlayerShip: true);

        Lock(attacker, player);

        Assert.Null(player.IffOverride(attacker)); // a player's stance is the operator's call
        Assert.False(player.IsHostileTo(attacker));
    }

    [Fact]
    public void ProvocationReachesTheSide()
    {
        var (items, zone) = BuildWorld();
        var side = new Faction { Name = "Side" };
        var locker = NewShip(items, zone, new Faction { Name = "Locker" });
        var victim = NewShip(items, zone, side);
        var mate = NewShip(items, zone, side);
        var friend = NewShip(items, zone, side);
        var player = NewShip(items, zone, side, isPlayerShip: true);
        var stranger = NewShip(items, zone, new Faction { Name = "Stranger" });
        friend.SetIff(locker, false);

        Lock(locker, victim);

        Assert.True(victim.IffOverride(locker));
        Assert.True(mate.IffOverride(locker));
        Assert.False(friend.IffOverride(locker)); // an override already held stands
        Assert.Null(player.IffOverride(locker));
        Assert.Null(stranger.IffOverride(locker));
    }

    // A zone owned by `owner`, a patrol of the owner, and a stranger of another faction the owner does not permit.
    private (ItemManager items, Zone zone, Ship patrol, Ship stranger) Owned(Faction strangerFaction)
    {
        var (items, zone) = BuildWorld();
        var owner = new Faction { Name = "Owner" };
        zone.GalaxyZone.Owner = owner;
        var patrol = NewShip(items, zone, owner);
        var stranger = NewShip(items, zone, strangerFaction);
        stranger.PresencePermitted = UniRx.Observable.Return(false).ToReadOnlyReactiveProperty();
        return (items, zone, patrol, stranger);
    }

    [Fact]
    public void TelemetryIsReceivedWithTheTrack()
    {
        var x = new Faction { Name = "X" };
        var (items, zone, patrol, stranger) = Owned(x);
        var unaffiliated = NewShip(items, zone, null);
        unaffiliated.PresencePermitted = UniRx.Observable.Return(false).ToReadOnlyReactiveProperty();

        zone.Update(.1f);
        Assert.False(patrol.TryGetTelemetry(stranger, out _), "unknown before the track");
        Assert.False(patrol.IsHostileTo(stranger), "unknown is never hostile");
        Assert.Empty(patrol.VisibleEnemies);

        Detect(patrol, stranger);
        Detect(patrol, unaffiliated);

        Assert.True(patrol.TryGetTelemetry(stranger, out var claimed));
        Assert.Same(x, claimed);
        Assert.True(patrol.TryGetTelemetry(unaffiliated, out var none));
        Assert.Null(none); // received, and it claims no faction
        Assert.True(patrol.IsHostileTo(stranger));
        Assert.Equal(1, patrol.VisibleEnemies.Count(e => e == stranger)); // filed hostile at once, listed once
        zone.Update(.01f);
        Assert.Equal(1, patrol.VisibleEnemies.Count(e => e == stranger)); // still listed once after the per-tick refresh
        Assert.DoesNotContain(stranger, patrol.VisibleFriendlies);
    }

    [Fact]
    public void AlliesAreKnownAsTruth()
    {
        var (items, zone, patrol, _) = Owned(new Faction { Name = "X" });
        var ally = NewShip(items, zone, patrol.Faction);
        ally.PresencePermitted = UniRx.Observable.Return(false).ToReadOnlyReactiveProperty();

        Detect(patrol, ally); // telemetry would read a trespasser

        Assert.False(patrol.IsHostileTo(ally));
        Assert.Contains(ally, patrol.VisibleFriendlies);
    }

    [Fact]
    public void TelemetryEndsWithTheContact()
    {
        var (items, zone, patrol, stranger) = Owned(new Faction { Name = "X" });
        Detect(patrol, stranger);
        Assert.True(patrol.IsHostileTo(stranger), "fixture");

        patrol.EntityInfoGathered[stranger] = 0; // the track is lost and no reveal remembers it
        zone.Update(.1f);

        Assert.False(patrol.TryGetTelemetry(stranger, out _));
        Assert.False(patrol.IsHostileTo(stranger));

        Detect(patrol, stranger);
        Assert.True(patrol.TryGetTelemetry(stranger, out _), "fixture: received again at the next track");
        patrol.Deactivate();
        Assert.False(patrol.TryGetTelemetry(stranger, out _));
    }

    [Fact]
    public void WeaponDoesNotFireWhenShooterIsNotHostileToItsTarget()
    {
        var (items, zone) = BuildWorld();
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, equipGun: true);
        var target = NewShip(items, zone, new Faction { Name = "Target" });
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon)).Behaviors.Single(b => b is Weapon);

        shooter.SetTarget(target);
        shooter.SetIff(target, false); // explicitly neutral

        weapon.Execute(.01f); // resolves the gun's stats: a burst has no rounds before the first tick
        weapon.Activate();
        weapon.Execute(.01f);

        Assert.True(weapon.CanFire); // Trigger() was safed: no burst fired, so no cooldown
    }

    [Fact]
    public void WeaponFiresWhenShooterIsHostileToItsTarget()
    {
        var (items, zone) = BuildWorld();
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, equipGun: true);
        var target = NewShip(items, zone, new Faction { Name = "Target" });
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon)).Behaviors.Single(b => b is Weapon);

        shooter.SetTarget(target);
        shooter.SetIff(target, true);

        weapon.Execute(.01f); // resolves the gun's stats: a burst has no rounds before the first tick
        weapon.Activate();
        weapon.Execute(.01f);

        Assert.False(weapon.CanFire); // Trigger() ran: the burst fired and the cooldown runs
    }

    [Fact]
    public void WeaponFiresWithNoTargetSet()
    {
        // Behaviour with no target is unchanged: StanceAllowsFire is vacuously true.
        var (items, zone) = BuildWorld();
        var shooter = NewShip(items, zone, new Faction { Name = "Shooter" }, equipGun: true);
        var weapon = (InstantWeapon) shooter.Equipment.Single(e => e.Behaviors.Any(b => b is Weapon)).Behaviors.Single(b => b is Weapon);

        weapon.Execute(.01f); // resolves the gun's stats: a burst has no rounds before the first tick
        weapon.Activate();
        weapon.Execute(.01f);

        Assert.False(weapon.CanFire); // Trigger() ran: the burst fired and the cooldown runs
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
        shooter.Aim = float3(0, 0, 1);
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
        shooter.SetTarget(target);

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
