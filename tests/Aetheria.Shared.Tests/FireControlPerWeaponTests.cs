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

// Operator rulings 2026-09-30 (fuse batch 5): AI and turrets decide per weapon and a fused weapon needs a
// designated target; the HUD forecast shows a fused weapon's outcome and reads the same decision Fire flies; a
// refused round is free, for a discrete weapon and for a beam. Builds its own fixture, the convention every
// fire-control test file follows: its shooter carries several mounts and can hold either kind of weapon.
public sealed class FireControlPerWeaponTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolperweapon-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlPerWeaponTests() => Directory.CreateDirectory(_root);

    private readonly List<CultCache> _openCaches = new List<CultCache>();

    public void Dispose()
    {
        foreach (var c in _openCaches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings(float minHit, float orbitalLayerHeight) => new GameplaySettings
    {
        OrbitalLayerHeight = orbitalLayerHeight,
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        TargetGearInfoThreshold = .8f,
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 2f,
        UnaidedAccuracy = .05f,
        UnaidedTracking = 10f,
        UnaidedPrecision = 1000f,
        AgentMinHitProbability = minHit,
        BeamResolveInterval = .1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static Shape SolidShape(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    private sealed class Gun
    {
        public EquippedItem Item;
        public Weapon Weapon;
    }

    private sealed class Rig
    {
        public Zone Zone;
        public Ship Shooter;
        public Entity Target;
        public List<Gun> Guns = new List<Gun>();
        public Gun Gun => Guns[0];
        public Gun Decoy;
    }

    // Mount 0 (None, arc 0) bears on a target dead ahead; (Clockwise, arc 0) is a side mount the default 170
    // degree arc cannot bring to bear on it. Fused: Proximity with `blast`, which is also its arming distance: blast 30
    // arms at 30, so a Range of 29.5 refuses and 40 fires.
    private Rig Build(
        (ItemRotation Rotation, float Arc)[] mounts = null, bool beam = false, WeaponFuse? fuse = null, float blast = 0f,
        float range = 100f, float targetRange = 60f, float accuracy = 1f, float minHit = .2f, bool lockWeapon = false,
        bool turret = false, float energy = 0f, float heat = 0f, float visibility = 0f, int magazine = 0, bool singleAmmoBurst = false, bool decoy = false, bool charged = false, bool auto = false,
        float effective = float.PositiveInfinity, float damageAtMax = 0f, float velocity = 0f, float targetLayerHeight = 0f)
    {
        mounts ??= new[] { (ItemRotation.None, 0f) };
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = SolidShape(5, 4), Durability = 1000000, Mass = 1000, Armor = 0,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        var shooterHullData = new HullData
        {
            Name = "ShooterHull", HullType = HullType.Ship, Shape = SolidShape(5, 5), Durability = 1000000, Mass = 1000
        };
        for (var i = 0; i < mounts.Length; i++)
            shooterHullData.Hardpoints.Add(new HardpointData
            {
                Type = HardpointType.Sensors, Position = new int2(i, 0), Shape = new Shape(),
                Rotation = mounts[i].Rotation, FiringArc = mounts[i].Arc
            });
        // A second weapon type on its own mount (a group of its own, since groups are per item type), dead ahead
        // and unfused, weaker than the gun: what a group pick falls back to when the gun's group is refused.
        if (decoy)
            shooterHullData.Hardpoints.Add(new HardpointData
            {
                Type = HardpointType.Sensors, Position = new int2(mounts.Length, 0), Shape = new Shape(),
                Rotation = ItemRotation.None, FiringArc = 0f
            });

        WeaponData behavior;
        if (beam)
            behavior = new ConstantWeaponData();
        else if (lockWeapon)
            behavior = new LockWeaponData
            {
                LockSpeed = Constant(1000), SensorImpact = Constant(1), LockAngle = Constant(0), DirectionImpact = Constant(1), Decay = Constant(1),
                Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1)
            };
        else if (charged)
            behavior = new ChargedWeaponData
            {
                Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1), SingleAmmoBurst = singleAmmoBurst,
                ChargeTime = Constant(1), ChargeEnergy = Constant(0), ChargeHeat = Constant(5000), CanFireEarly = true
            };
        else if (auto)
            behavior = new AutoWeaponData { Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1), SingleAmmoBurst = singleAmmoBurst };
        else
            behavior = new InstantWeaponData { Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1), SingleAmmoBurst = singleAmmoBurst };
        behavior.Damage = Constant(100);
        behavior.Range = Constant(range);
        behavior.MinRange = Constant(0);
        behavior.EffectiveRange = Constant(effective);
        behavior.DamageAtMaxRange = damageAtMax;
        behavior.Velocity = Constant(velocity);
        behavior.Spread = Constant(0);
        behavior.DamageSpread = Constant(0);
        behavior.Penetration = Constant(0);
        behavior.Energy = Constant(energy);
        behavior.Heat = Constant(heat);
        behavior.Visibility = Constant(visibility);
        behavior.MagazineSize = magazine;

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(hullData);
        cache.Upsert(shooterHullData);
        cache.Upsert(new WeaponItemData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Fuse = fuse, BlastRadius = blast,
            Behaviors = { behavior }
        });
        cache.Upsert(new WeaponItemData
        {
            Name = "Decoy", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors =
            {
                new InstantWeaponData
                {
                    Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1), Damage = Constant(50), Range = Constant(100),
                    MinRange = Constant(0), Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(0), Penetration = Constant(0),
                    Energy = Constant(0), Heat = Constant(0), Visibility = Constant(0),
                }
            }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData { Accuracy = Constant(accuracy), Resolution = Constant(1000f), Precision = Constant(1000f), Tracking = Constant(1000000f) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ReactorData { Charge = Constant(1000), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Turret", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TurretControllerData() }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 100; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(1u, cache, ledger, TestSettings(minHit, targetLayerHeight), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "PerWeapon", Owner = null }, null);

        EquippableItem Make(string name, int lot, float durability) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<ItemData>(name)), Durability = durability, Lot = lot
        };

        var lot = 1;
        var shooterHullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("ShooterHull"));
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooter = new Ship(items, zone, new EquippableItem { Data = shooterHullRef, Durability = 1000000, Lot = lot++ }, new EntitySettings());
        var rig = new Rig();
        for (var i = 0; i < mounts.Length; i++)
        {
            var gun = Make("Gun", lot++, 1);
            Assert.True(shooter.TryEquip(gun, new int2(i, 0)));
            var item = shooter.Equipment.Single(x => x.EquippableItem == gun);
            rig.Guns.Add(new Gun { Item = item, Weapon = (Weapon) item.Behaviors.Single(b => b is Weapon) });
        }
        if (decoy)
        {
            var d = Make("Decoy", lot++, 1);
            Assert.True(shooter.TryEquip(d, new int2(mounts.Length, 0)));
            var item = shooter.Equipment.Single(x => x.EquippableItem == d);
            rig.Decoy = new Gun { Item = item, Weapon = (Weapon) item.Behaviors.Single(b => b is Weapon) };
        }
        var targeting = Make("Targeting", lot++, 1);
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));
        if (true)
        {
            var reactor = Make("Reactor", lot++, 10);
            Assert.True(shooter.TryFindSpace(reactor, out var rpos));
            Assert.True(shooter.TryEquip(reactor, rpos));
        }
        if (turret)
        {
            var t = Make("Turret", lot++, 1);
            Assert.True(shooter.TryFindSpace(t, out var upos));
            Assert.True(shooter.TryEquip(t, upos));
        }

        // A target layer height rides the target on the orbital layer at that height (the shooter is a ship, on the ship layer),
        // so the two sit at different heights.
        var targetItem = new EquippableItem { Data = hullRef, Durability = 1000000, Lot = lot++ };
        Entity target = targetLayerHeight != 0f
            ? new OrbitalEntity(items, zone, targetItem, default, new EntitySettings())
            : new Ship(items, zone, targetItem, new EntitySettings());
        Assert.True(target.TryEquip(Make("Gun", lot++, 1), new int2(0, 0)));

        zone.Admit(shooter, piloted: false);
        zone.Admit(target, piloted: false);

        shooter.Position = float3(-53, 0, -101);
        target.Position = shooter.Position + float3(0, 0, targetRange);
        shooter.SetTarget(target);
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        zone.Update(1f);

        rig.Zone = zone;
        rig.Shooter = shooter;
        rig.Target = target;
        return rig;
    }

    private static void Aim(Rig r, float2 direction) => r.Shooter.Aim = float3(direction.x, 0, direction.y);

    private static readonly (ItemRotation, float)[] SideThenForward = { (ItemRotation.Clockwise, 0f), (ItemRotation.None, 0f) };
    private static readonly (ItemRotation, float)[] SideMount = { (ItemRotation.Clockwise, 0f) };

    // ==== Ruling 1: per weapon, and a fused weapon needs a designated target ====

    // A weapon group shares a trigger, not a verdict: two guns of one type, the first on a side mount that cannot
    // bear on the target and the second dead ahead. Only the second fires. Kills: the group's verdict read off its
    // first weapon (Combat's old `testWeapon`), which silences the group.
    [Fact]
    public void CombatDecidesEachWeaponOfAGroupForItself()
    {
        var r = Build(SideThenForward);
        var group = r.Shooter.WeaponGroups.Single(g => g.weapons.Count == 2).weapons;
        Assert.True(ReferenceEquals(group[0], r.Guns[0].Weapon), "fixture: the side mount leads the group");
        r.Zone.Agents.Add(new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant));

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.False(r.Guns[0].Weapon.Firing, "the side-mounted gun cannot bear");
        Assert.True(r.Guns[1].Weapon.Firing, "the forward gun bears and is worth firing");
    }

    // A fused weapon fires whenever its target is designated, whatever the arc: the same group, now fused, fires
    // both guns, the side mount included. Kills: Combat reading HitProbability (zero out of arc) for a fused weapon.
    [Fact]
    public void CombatFiresAFusedWeaponAtADesignatedTargetOutOfArc()
    {
        var r = Build(SideThenForward, fuse: WeaponFuse.Proximity, blast: 4f);
        r.Zone.Agents.Add(new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant));

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.True(r.Guns[0].Weapon.Firing, "a designated target is enough, arc or no");
        Assert.True(r.Guns[1].Weapon.Firing);
    }

    // The turret makes the same per-weapon decision. Kills: TurretController reading HitProbability, or its own
    // range test, for a fused weapon.
    [Fact]
    public void ATurretFiresAFusedWeaponAtADesignatedTargetOutOfArc()
    {
        var r = Build(SideMount, fuse: WeaponFuse.Proximity, blast: 4f, turret: true);

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.True(r.Gun.Weapon.Firing, "the turret must fire a fused weapon at a designated target out of arc");
    }

    // The turret keeps the AgentMinHitProbability threshold for a direct-hit weapon: a shot priced near .5 is held at a
    // threshold of .6 and taken at .4. Kills: the turret's fire threshold dropped to zero or fixed to a constant.
    [Theory]
    [InlineData(.6f, false)]
    [InlineData(.4f, true)]
    public void ATurretKeepsTheGlobalHitProbabilityThresholdForADirectHitWeapon(float minHit, bool fires)
    {
        var r = Build(accuracy: .5f, minHit: minHit, turret: true);
        var p = FireControl.HitProbability(r.Gun.Weapon, r.Shooter, r.Target);
        Assert.True(p > .4f && p < .6f, $"fixture: the shot must price near .5, priced {p}");

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.Equal(fires, r.Gun.Weapon.Firing);
    }

    // The same threshold half a hundredth either side of the shot's price. Kills: the turret's threshold loosened or
    // tightened by a tenth or a hundredth.
    [Theory]
    [InlineData(.005f, false)]
    [InlineData(-.005f, true)]
    public void ATurretHoldsAShotJustUnderTheThresholdAndTakesOneJustOver(float thresholdAbovePrice, bool fires)
    {
        var probe = Build(accuracy: .5f, minHit: 0f, turret: true);
        var price = FireControl.HitProbability(probe.Gun.Weapon, probe.Shooter, probe.Target);
        var r = Build(accuracy: .5f, minHit: price + thresholdAbovePrice, turret: true);
        Assert.Equal(price, FireControl.HitProbability(r.Gun.Weapon, r.Shooter, r.Target), 5);

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.Equal(fires, r.Gun.Weapon.Firing);
    }

    // A flight's members hold off at 1.1 x their longest gun when the doctrine authors no standoff (every authored
    // doctrine leaves it 0), and at the authored distance otherwise. Guns of Range r and 2r on one ship, at two r and two authored values. Kills: the factor replaced by an offset or an affine rule, the
    // default zeroed, inverted, taken from the shortest gun, or the authored value ignored.
    [Theory]
    [InlineData(50f, 0f, 110f)]
    [InlineData(200f, 0f, 220f)]
    [InlineData(50f, 40f, 40f)]
    [InlineData(50f, 250f, 250f)]
    public void AFlightHoldsOffAtTheDefaultOrAuthoredStandoff(float range, float holdStandoff, float expected)
    {
        var r = Build(range: range, decoy: true);
        r.Shooter.SetIff(r.Target, null);
        if (!r.Shooter.VisibleEnemies.Contains(r.Target)) r.Shooter.VisibleEnemies.Add(r.Target);
        var settings = r.Shooter.ItemManager.GameplaySettings;
        var flight = new Flight(new FactionDoctrine
        {
            EngageOn = EngageOn.Detection, Grace = 5, Combatant = new RoleDoctrine { MinHitProbability = .2f, HoldStandoff = holdStandoff }
        }, settings);
        var agent = new Minion(r.Shooter, flight.Combatant);
        flight.Join(agent, new PatrolOrbitsTask());

        flight.Update(.1f);

        Assert.Equal(expected, ((FollowTask) agent.Task).Standoff, 2);
    }

    // The AI aims at the height a target rides at (Entity.RideHeight: field plus the layer the target's kind rides on),
    // not at the plane and not at the shooter's own layer. A target on a layer 40 u up against a shooter on the ship
    // layer, a shot fast enough to be predicted: the turret's and Combat's aim point at the target's stepped pose.
    // Kills: either aim site spelling its own offset (dropped, added to, scaled, or the shooter's layer).
    [Fact]
    public void ATurretAimsAtTheHeightATargetRidesAt()
    {
        var r = Build(turret: true, velocity: 500f, targetLayerHeight: 40f);
        r.Zone.Update(1f);

        AssertAimsAtTarget(r);
    }

    [Fact]
    public void CombatAimsAtTheHeightATargetRidesAt()
    {
        var r = Build(velocity: 500f, targetLayerHeight: 40f);
        r.Zone.Agents.Add(new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant));
        r.Zone.Update(1f);
        r.Zone.Update(1f);

        AssertAimsAtTarget(r);
    }

    private static void AssertAimsAtTarget(Rig r)
    {
        var toTarget = r.Target.Position - r.Shooter.Position;
        Assert.True(abs(toTarget.y) > 30f, "fixture: the raised hull is well above the shooter");
        var expected = normalize(toTarget);
        Assert.Equal(expected.x, r.Shooter.Aim.x, 3);
        Assert.Equal(expected.y, r.Shooter.Aim.y, 3);
        Assert.Equal(expected.z, r.Shooter.Aim.z, 3);
    }

    public enum Withheld { NoTarget, OutOfRange, NotVisible, Unlocked }

    // The AI never fires a fused weapon at nothing: a missing target, one out of range, one not visible and one
    // an unlocked LockWeapon holds no lock on are each no reason to fish. The direct-hit control fires (true) on
    // the same rig once the target is valid. Kills: `designated` dropped from the fused branch (null included).
    [Theory]
    [InlineData(Withheld.NoTarget)]
    [InlineData(Withheld.OutOfRange)]
    [InlineData(Withheld.NotVisible)]
    [InlineData(Withheld.Unlocked)]
    public void AnAgentNeverFiresAFusedWeaponAtNothing(Withheld reason)
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 4f, range: 100f,
            targetRange: reason == Withheld.OutOfRange ? 150f : 60f, lockWeapon: reason == Withheld.Unlocked);
        var target = r.Target;
        if (reason == Withheld.NoTarget) target = null;
        if (reason == Withheld.NotVisible) r.Shooter.VisibleEntities.Remove(r.Target);

        Assert.False(FireControl.AgentFires(r.Gun.Weapon, r.Shooter, target, r.Shooter.ItemManager.GameplaySettings.AgentMinHitProbability));
    }

    [Fact]
    public void AnAgentFiresAFusedWeaponAtAValidTargetWhateverItsHitChance()
    {
        // Accuracy 0 prices every direct hit at zero; the fused weapon is unmoved.
        var r = Build(SideMount, fuse: WeaponFuse.Proximity, blast: 4f, accuracy: 0f);

        Assert.True(FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target, r.Shooter.ItemManager.GameplaySettings.AgentMinHitProbability));
    }

    // A weapon that fires at what it hits keeps the AgentMinHitProbability threshold, per weapon.
    // Kills: the threshold dropped for a direct-hit weapon, or applied to a fused one.
    [Theory]
    [InlineData(.5f, .2f, true)]
    [InlineData(.5f, .9f, false)]
    public void AnAgentKeepsTheHitProbabilityThresholdForADirectHitWeapon(float accuracy, float minHit, bool fires)
    {
        var r = Build(accuracy: accuracy, minHit: minHit);
        var p = FireControl.HitProbability(r.Gun.Weapon, r.Shooter, r.Target);
        Assert.True(p > .3f && p < .7f, $"fixture: the shot must price near the accuracy, priced {p}");

        Assert.Equal(fires, FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target, r.Shooter.ItemManager.GameplaySettings.AgentMinHitProbability));
    }

    // ==== Ruling 2: the HUD forecast is Fire's own decision ====

    // Inspect's Outcome and BurstReach against what Fire then does with the same rig: Refused iff Fire returns 0,
    // and a Burst's reach is the planar distance to the burst point Fire froze. Rows: a target beyond the arming
    // distance (burst at its range), one inside it (pushed out to 30), no target (max range), out of arc, a
    // Range that cannot reach the arming distance, and a weapon without a fuse.
    // Kills: an Inspect that computes its own reach (ignoring the arming push or the Range clamp).
    [Theory]
    [InlineData(60f, 100f, false, false, FireOutcome.Burst, 60f)]
    [InlineData(20f, 100f, false, false, FireOutcome.Burst, 30f)]
    [InlineData(60f, 40f, true, false, FireOutcome.Burst, 40f)]
    [InlineData(60f, 100f, false, true, FireOutcome.Burst, 60f)]
    [InlineData(60f, 29.5f, false, false, FireOutcome.Refused, 0f)]
    [InlineData(60f, 29.5f, true, false, FireOutcome.Refused, 0f)]
    public void TheForecastIsTheOutcomeFireDelivers(float targetRange, float range, bool noTarget, bool outOfArc, FireOutcome outcome, float reach)
    {
        var r = Build(outOfArc ? SideMount : null, fuse: WeaponFuse.Proximity, blast: 30f, range: range, targetRange: targetRange);
        if (noTarget) r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));

        var d = FireControl.Inspect(r.Gun.Weapon, r.Shooter, r.Shooter.Target.Value.Entity);
        var shotId = FireControl.Fire(r.Gun.Weapon, r.Gun.Item, r.Shooter);

        Assert.Equal(outcome, d.Outcome);
        Assert.Equal(outcome != FireOutcome.Refused, shotId != 0);
        if (outcome == FireOutcome.Refused) return;
        Assert.Equal(reach, d.BurstReach, 2);
        var shot = SafeAssert.OnlyShot(r.Zone);
        Assert.Equal(d.BurstReach, length((shot.BurstPosition - shot.FireOrigin).xz), 2);
    }

    // A weapon without a fuse forecasts a direct hit and keeps its percentages. Kills: Solve calling everything a burst.
    [Fact]
    public void AWeaponWithoutAFuseForecastsADirectHit()
    {
        var r = Build();

        var d = FireControl.Inspect(r.Gun.Weapon, r.Shooter, r.Target);

        Assert.Equal(FireOutcome.Direct, d.Outcome);
        Assert.True(d.PBase > 0f);
    }

    // ==== Ruling 3: a refused round is free ====

    private sealed class Costs
    {
        public int Ammo;
        public float ChargeSpent;
        public int Wears;
        public float Heat;
        public bool Visible;
        public int Announced;
        public int Shots;
        public bool CanFire;
    }

    // One discrete weapon, triggered once at no target, with every cost observable: a magazine, an energy charge,
    // wear events, heat and visibility. Range 29.5 refuses (arming 30); Range 40 fires.
    private Costs Pull(float range, bool single, bool viaTrigger, bool staleRange = false, bool auto = false)
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 30f, range: range, energy: 50f, heat: 5000f, visibility: 100f, magazine: 5, singleAmmoBurst: single, auto: auto);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (InstantWeapon) r.Gun.Weapon;
        var capacitor = (InputCapacitor) typeof(InstantWeapon).GetField("_capacitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon);
        var costs = new Costs();
        r.Zone.ShotCommitted.Subscribe(_ => costs.Shots++); // a fused round with no flight commits and resolves in its own tick
        r.Shooter.ItemDamage.Subscribe(t => { if (t.Item1 == r.Gun.Item) costs.Wears++; });
        weapon.OnFire += _ => costs.Announced++;
        capacitor.AddCharge(1000f); // a full buffer, so the first round can be paid for
        var chargeBefore = capacitor.Charge;
        var heatBefore = r.Gun.Item.Temperature;
        var ammoBefore = weapon.Ammo;
        Assert.True(chargeBefore > 0f && ammoBefore == 5, "fixture: a full buffer and magazine");

        // The Range the previous tick's Execute left, before this tick refreshes it: a power-dependent Range that
        // has since fallen. Set through the property's own setter, so the trigger sees a Range the round will not.
        if (staleRange) typeof(Weapon).GetProperty("Range").GetSetMethod(true).Invoke(weapon, new object[] { 100f });
        if (auto)
        {
            // AutoWeapon's own trigger: a held weapon whose cooldown has run out retriggers inside Execute.
            typeof(Weapon).GetField("_firing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, true);
            typeof(InstantWeapon).GetField("_cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, -1f);
        }
        else if (viaTrigger) weapon.Activate();
        else typeof(InstantWeapon).GetField("_burstRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, 1);
        r.Zone.Update(.01f);

        costs.Ammo = ammoBefore - weapon.Ammo;
        costs.ChargeSpent = chargeBefore - capacitor.Charge;
        costs.Heat = r.Gun.Item.Temperature - heatBefore;
        costs.Visible = r.Shooter.VisibilitySources.TryGetValue(weapon, out var v) && v.Strength > 1f;
        costs.CanFire = weapon.CanFire;
        return costs;
    }

    // Control: the same weapon with Range to spare pays every cost, so a free refusal below is not a fixture that
    // never spends. Then the refusal costs nothing: no ammo, no energy, no announcement, no wear, no heat, no
    // visibility, no shot. `single` is a weapon that pays once per burst, at its first unrefused round rather than at each;
    // `viaTrigger` false authorises the burst directly (the way a trigger that passed leaves it) on a weapon whose
    // Range is short, so the Trigger check cannot be what stops the round and Execute must refuse it itself.
    // Kills, one per row of the assertions: a spend that precedes the refusal (Execute or Trigger), an
    // announcement (what a presenter hears), wear, heat or visibility left ahead of the `continue`.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ARefusedDiscreteRoundCostsNothing(bool single, bool viaTrigger)
    {
        var fired = Pull(40f, single, viaTrigger);
        Assert.True(fired.Shots == 1, $"shots {fired.Shots}");
        Assert.True(fired.Ammo == 1, $"ammo spent {fired.Ammo}");
        Assert.True(fired.ChargeSpent > 1f, "a fired round spends its energy");
        Assert.True(fired.Wears > 0);
        Assert.True(fired.Heat > 0f);
        Assert.True(fired.Visible);
        Assert.Equal(1, fired.Announced);

        var refused = Pull(29.5f, single, viaTrigger);
        Assert.Equal(0, refused.Shots);
        Assert.Equal(0, refused.Ammo);
        Assert.Equal(0f, refused.ChargeSpent);
        Assert.Equal(0, refused.Wears);
        Assert.True(fired.Heat - refused.Heat > 5f, $"a refused round adds no heat ({refused.Heat} vs {fired.Heat})");
        Assert.False(refused.Visible);
        Assert.Equal(0, refused.Announced);
    }

    // The same rule for a beam: a fused beam whose arming distance exceeds Range fires nothing and draws
    // nothing. The control (Range 40) rolls shots, wears, draws power and shows; the refused beam does none of
    // that, asks the bus for nothing, and never starts (ARefusedBeamNeverStarts).
    // Kills: ConstantWeapon spending (power request, wear, heat, visibility) before the refusal, or not
    // consulting FireControl at all.
    private (int Shots, int Wears, float Request, bool Visible, float Heat, int Stops, bool Firing) Burn(float range)
    {
        var r = Build(beam: true, fuse: WeaponFuse.Proximity, blast: 30f, range: range, energy: 50f, heat: 5000f, visibility: 100f);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (ConstantWeapon) r.Gun.Weapon;
        var wears = 0;
        var stops = 0;
        var shots = 0;
        r.Zone.ShotResolved.Subscribe(_ => shots++);
        r.Shooter.ItemDamage.Subscribe(t => { if (t.Item1 == r.Gun.Item) wears++; });
        weapon.OnStopFiring += () => stops++;
        var heatBefore = r.Gun.Item.Temperature;

        weapon.Activate();
        var request = weapon.PowerRequest(.1f);
        for (var i = 0; i < 5; i++) r.Zone.Update(.1f);

        var visible = r.Shooter.VisibilitySources.TryGetValue(weapon, out var v) && v.Strength > 1f;
        return (shots, wears, request, visible, r.Gun.Item.Temperature - heatBefore, stops, weapon.Firing);
    }

    [Fact]
    public void ARefusedBeamCostsNothing()
    {
        var fired = Burn(40f);
        Assert.True(fired.Shots > 0, "control: the beam rolls shots");
        Assert.True(fired.Wears > 0);
        Assert.True(fired.Request > 0f);
        Assert.True(fired.Visible);
        Assert.True(fired.Heat > 0f);

        var refused = Burn(29.5f);
        Assert.Equal(0, refused.Shots);
        Assert.Equal(0, refused.Wears);
        Assert.Equal(0f, refused.Request);
        Assert.False(refused.Visible);
        Assert.True(fired.Heat - refused.Heat > 5f, $"a refused beam adds no heat ({refused.Heat} vs {fired.Heat})");
        Assert.True(refused.Stops == 0 && !refused.Firing, "a refused beam never starts, so it never has to stop");
    }

    // A beam is a stream of rounds, so a refused beam never starts: OnStartFiring drives the beam's visuals and
    // audio, and starting it for Execute to stop a tick later would flash a beam that fires nothing. The control
    // (Range 40) starts once and keeps firing. Kills: Activate that starts a beam without asking FireControl.
    [Theory]
    [InlineData(40f, 1)]
    [InlineData(29.5f, 0)]
    public void ARefusedBeamNeverStarts(float range, int starts)
    {
        var r = Build(beam: true, fuse: WeaponFuse.Proximity, blast: 30f, range: range);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (ConstantWeapon) r.Gun.Weapon;
        var started = 0;
        weapon.OnStartFiring += () => started++;

        weapon.Activate();
        for (var i = 0; i < 5; i++) r.Zone.Update(.1f);

        Assert.Equal(starts, started);
        Assert.Equal(starts == 1, weapon.Firing);
    }

    // A refused weapon is not one an agent can use: a fused weapon whose arming distance (30) exceeds its Range
    // (29.5) is not fired at a target inside that Range, where the same weapon with Range to spare is.
    // Kills: AgentFires answering "designated" without asking whether Solve refuses.
    [Theory]
    [InlineData(29.5f, false)]
    [InlineData(40f, true)]
    public void AnAgentNeverFiresARefusedFusedWeapon(float range, bool fires)
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 30f, range: range, targetRange: 20f);

        Assert.Equal(fires, FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target, r.Shooter.ItemManager.GameplaySettings.AgentMinHitProbability));
    }

    // Combat's group pick counts only weapons that can fire: the refused gun's group out-damages the decoy's, so a
    // pick that counted it would select a group whose weapons are then never fired, and the decoy would stay silent.
    // Kills: Combat counting a refused weapon's damage toward its group.
    [Fact]
    public void CombatDoesNotPickAGroupWhoseWeaponIsRefused()
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 30f, range: 29.5f, targetRange: 20f, decoy: true);
        Assert.True(r.Gun.Weapon.RangeDamagePerSecond(20f) > r.Decoy.Weapon.RangeDamagePerSecond(20f), "fixture: the refused gun is the stronger group");
        r.Zone.Agents.Add(new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant));

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.False(r.Gun.Weapon.Firing, "a refused weapon is never activated");
        Assert.True(r.Decoy.Weapon.Firing, "the group that can fire is the one picked");
    }

    // Combat's group pick counts only weapons whose target is designated: the stronger gun's Range (10) does not reach the
    // target (20), so its group is not picked and the decoy's, which does reach, is. Kills: Combat asking
    // Designated nothing (a group of weapons that cannot reach the target selected).
    [Fact]
    public void CombatDoesNotPickAGroupWhoseWeaponCannotReachTheTarget()
    {
        var r = Build(range: 10f, targetRange: 20f, decoy: true);
        Assert.True(r.Gun.Weapon.RangeDamagePerSecond(5f) > r.Decoy.Weapon.RangeDamagePerSecond(5f), "fixture: the short gun is the stronger group");
        Assert.Equal(0f, r.Gun.Weapon.RangeDamagePerSecond(20f)); // the damage function already reads 0 beyond Range
        r.Zone.Agents.Add(new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant));

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.False(r.Gun.Weapon.Firing, "a weapon that cannot reach the target is never activated");
        Assert.True(r.Decoy.Weapon.Firing, "the group that reaches is the one picked");
    }

    // Combat's group pick reads the falloff at the target's real distance. The gun falls off from 10 to 0 at 100, the decoy
    // is flat at 50: at 60 the gun makes 44 (the decoy is picked) while at half that distance it makes 78 and at double it
    // makes 0; with the gun flat to 40 it makes 67 at 60 (picked) and 0 at double. The fixture states both neighbours so a
    // pick made at a scaled distance reads the other way. Kills: the group's DPS read at a distance other than the target's.
    [Theory]
    [InlineData(10f, false)]
    [InlineData(40f, true)]
    public void CombatPicksTheGroupStrongestAtTheTargetsDistance(float effective, bool gunPicked)
    {
        var r = Build(range: 100f, targetRange: 60f, effective: effective, decoy: true);
        var decoy = r.Decoy.Weapon.RangeDamagePerSecond(60f);
        Assert.Equal(gunPicked, r.Gun.Weapon.RangeDamagePerSecond(60f) > decoy);
        Assert.True(r.Gun.Weapon.RangeDamagePerSecond(effective == 10f ? 30f : 120f) > decoy == !gunPicked, "fixture: the scaled distance reads the other way");
        r.Zone.Agents.Add(new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant));

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.Equal(gunPicked, r.Gun.Weapon.Firing);
        Assert.Equal(!gunPicked, r.Decoy.Weapon.Firing);
    }

    // Combat's range test is Designated's, not a second one: a target exactly at max range is in reach (the old
    // private test was strict). Kills: a range comparison of Combat's own.
    [Fact]
    public void CombatCountsATargetExactlyAtMaxRangeAsInReach()
    {
        var r = Build(range: 60f, targetRange: 60f);
        // The agent is driven directly, not through the zone, so the shooter stays exactly at max range: the first
        // update leaves the root state for combat, the second is combat deciding.
        var agent = new Minion(r.Shooter, FactionDoctrine.Default(r.Shooter.ItemManager.GameplaySettings).Combatant);
        agent.Update(.01f);
        agent.Update(.01f);

        Assert.True(r.Gun.Weapon.Firing);
    }

    // The trigger's threshold is inclusive: a shot priced exactly at AgentMinHitProbability is worth taking, and
    // one priced a hair under it is not. Kills: `>` for `>=`.
    [Fact]
    public void AnAgentFiresAtExactlyTheHitProbabilityThreshold()
    {
        var probe = Build(accuracy: .5f);
        var price = FireControl.HitProbability(probe.Gun.Weapon, probe.Shooter, probe.Target);
        Assert.True(price > 0f);

        var atThreshold = Build(accuracy: .5f, minHit: price);
        var overThreshold = Build(accuracy: .5f, minHit: MathF.BitIncrement(price));

        Assert.True(FireControl.AgentFires(atThreshold.Gun.Weapon, atThreshold.Shooter, atThreshold.Target, atThreshold.Shooter.ItemManager.GameplaySettings.AgentMinHitProbability));
        Assert.False(FireControl.AgentFires(overThreshold.Gun.Weapon, overThreshold.Shooter, overThreshold.Target, overThreshold.Shooter.ItemManager.GameplaySettings.AgentMinHitProbability));
    }

    // The threshold is the doctrine's, passed in: the settings global (zero here) decides nothing for a pilot. Kills: a
    // reintroduced read of AgentMinHitProbability inside AgentFires.
    [Fact]
    public void TheDoctrinesThresholdDecidesFire()
    {
        var r = Build(accuracy: .5f, minHit: 0f);
        var price = FireControl.HitProbability(r.Gun.Weapon, r.Shooter, r.Target);
        Assert.True(price > 0f);

        Assert.True(FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target, price));
        Assert.False(FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target, MathF.BitIncrement(price)));
    }

    // A long and a short weapon: the exponent biases the preferred range outward. Kills: the optimum read from the
    // settings global instead of the member's RoleDoctrine.
    [Fact]
    public void TheDoctrinesExponentMovesTheOptimumRange()
    {
        var r = Build(range: 400f, decoy: true);
        float Optimum(float exponent)
        {
            var combat = new CombatState(new Minion(r.Shooter, new RoleDoctrine { RangeExponent = exponent, MinHitProbability = .2f }));
            combat.Update(0f); // the optimum is derived at the first Update
            return combat.OptimumRange;
        }

        Assert.True(Optimum(1f) - Optimum(0f) > 100f);
    }

    // The range-sample offset is the pilot's own draw (zone seed, its id), never the item stream's: the same pilot
    // samples the same optimum however many items were minted before it spawned, and two pilots sample their own.
    // Kills: the offset drawn from ItemManager.Random; the agent id dropped from the draw.
    [Fact]
    public void AnAgentsSampleOffsetIsItsOwnAndNotTheItemStreams()
    {
        var r = Build(range: 400f, decoy: true);
        float Optimum(uint id, int itemsMintedFirst)
        {
            for (var i = 0; i < itemsMintedFirst; i++) r.Shooter.ItemManager.Random.NextFloat();
            var combat = new CombatState(new Minion(r.Shooter, new RoleDoctrine { RangeExponent = 1f, MinHitProbability = .2f }) { Id = id });
            combat.Update(0f);
            return combat.OptimumRange;
        }

        Assert.Equal(Optimum(3, 0), Optimum(3, 0));
        Assert.Equal(Optimum(3, 0), Optimum(3, 17));
        Assert.True(Enumerable.Range(1, 8).Select(i => Optimum((uint) i, 0)).Distinct().Count() > 1,
            "pilots with different ids sample different offsets");

        // Whose dice: each sampled range is the pilot's offset plus a whole step of the sample grid, so the optimum
        // minus the offset from CombatSampleStream lands on the grid for every id; an offset from another stream
        // (the loot dice, say) leaves a remainder. Kills: the offset drawn from another stream.
        var min = r.Shooter.Weapons.Min(w => w.MinRange);
        var step = (r.Shooter.Weapons.Max(w => w.Range) - min) / 32f; // Combat.DPS_SAMPLE_COUNT
        foreach (var id in Enumerable.Range(1, 16).Select(i => (uint) i))
        {
            var offset = SimulationDice.For(r.Zone.CombatSeed, SimulationDice.CombatSampleStream, id).NextFloat(step);
            var cells = (Optimum(id, 0) - offset - min) / step;
            Assert.True(MathF.Abs(cells - MathF.Round(cells)) < .01f, $"id {id}: optimum is {cells} steps from the pilot's own offset");
        }

        // The other direction: sampling the optimum takes nothing from the item stream, so how a pilot fights never moves
        // what is minted next. Kills: an AI draw from ItemManager.Random.
        var items = r.Shooter.ItemManager;
        var before = items.Random;
        Optimum(5, 0);
        Assert.Equal(before.NextFloat(), items.Random.NextFloat());
    }

    // A round refused on a Range that fell between the trigger and the round is still free: the burst is paid
    // (SingleAmmoBurst) at its first round that is not refused, judged with the Range that round flies with.
    // The trigger saw a stale Range of 100; the round's Range is 29.5, under the arming distance. The refusal
    // starts no cooldown either (operator ruling 2026-09-30): the weapon can fire again at once, while the
    // control (Range 40) fired and is cooling down. Covers the player's trigger and AutoWeapon's own.
    // Kills: paying for the burst at the trigger; starting the cooldown at the trigger (Trigger or AutoWeapon).
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ARefusedBurstThatPassedTheTriggerOnAStaleRangeCostsNothing(bool single, bool auto)
    {
        var fired = Pull(40f, single, viaTrigger: true, staleRange: true, auto: auto);
        Assert.Equal(1, fired.Shots);
        Assert.False(fired.CanFire, "control: a fired burst starts the cooldown");

        var refused = Pull(29.5f, single, viaTrigger: true, staleRange: true, auto: auto);

        Assert.Equal(0, refused.Shots);
        Assert.Equal(0, refused.Ammo);
        Assert.Equal(0f, refused.ChargeSpent);
        Assert.Equal(0, refused.Announced);
        Assert.True(refused.CanFire, "a burst that fired nothing leaves no cooldown");
    }

    // A refused charged weapon never starts charging: no charge heat, no start event a presenter could sound, and it can be
    // triggered again at once. The control (Range 40) charges and heats.
    // Kills: ChargedWeapon.Activate that does not ask FireControl.
    private (int Starts, float Heat, bool Charging) Charge(float range)
    {
        var r = Build(charged: true, fuse: WeaponFuse.Proximity, blast: 30f, range: range);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (ChargedWeapon) r.Gun.Weapon;
        var starts = 0;
        weapon.OnStartCharging += () => starts++;
        var heatBefore = r.Gun.Item.Temperature;

        weapon.Activate();
        for (var i = 0; i < 3; i++) r.Zone.Update(.1f);

        return (starts, r.Gun.Item.Temperature - heatBefore, weapon.Progress > 0f);
    }

    [Fact]
    public void ARefusedChargedWeaponNeverStartsCharging()
    {
        var fired = Charge(40f);
        Assert.Equal(1, fired.Starts);
        Assert.True(fired.Heat > 0f, "control: charging heats");

        var refused = Charge(29.5f);
        Assert.Equal(0, refused.Starts);
        Assert.True(fired.Heat - refused.Heat > 5f, $"a refused weapon adds no charge heat ({refused.Heat} vs {fired.Heat})");
    }
    // A triggered burst has not started its cooldown, but it is not ready either: pulled again before its first round
    // flies it would restart the burst. Kills: CanFire that reads the cooldown alone.
    [Fact]
    public void ATriggeredBurstIsNotReadyBeforeItsFirstRound()
    {
        var r = Build(range: 100f);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (InstantWeapon) r.Gun.Weapon;
        r.Zone.Update(.01f); // resolves the gun's stats, which size the burst
        Assert.True(weapon.CanFire);

        weapon.Activate();

        Assert.False(weapon.CanFire);
    }

    // A charge released with a burst pending has not started its cooldown, but it is not ready either: pressed
    // again before that burst's first round it would start a second charge over it. Kills: ChargedWeapon.Activate
    // that reads the cooldown alone instead of CanFire.
    [Fact]
    public void AChargedWeaponPressedAgainBeforeItsReleasedRoundFliesStartsNoSecondCharge()
    {
        var r = Build(charged: true, range: 100f);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (ChargedWeapon) r.Gun.Weapon;
        var starts = 0;
        weapon.OnStartCharging += () => starts++;
        r.Zone.Update(.01f); // resolves the stats
        weapon.Activate();
        for (var i = 0; i < 15; i++) r.Zone.Update(.1f);
        Assert.Equal(1, starts);

        weapon.Deactivate();
        weapon.Activate();
        r.Zone.Update(.1f);

        Assert.Equal(1, starts);
        Assert.False((bool) typeof(ChargedWeapon).GetField("_charging", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon));
    }

    // Holding the trigger fires once per Cooldown: the round that starts the cooldown pays for the tick it flew in.
    // Cooldown 1 at .25 s ticks is a shot every 5 ticks, 12 in 15 s. And a burst whose every round is refused
    // leaves no cooldown: the same held trigger is ready every tick. Kills: a first round that starts the cooldown
    // at 1 after the tick's decrement (a shot every 6 ticks, 10 in 15 s); a cooldown left by a refused burst.
    [Theory]
    [InlineData(100f, 12)]
    [InlineData(29.5f, 0)]
    public void AHeldTriggerFiresOncePerCooldownAndARefusedBurstLeavesNone(float range, int shots)
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 30f, range: range);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (InstantWeapon) r.Gun.Weapon;
        var fired = 0;
        weapon.OnFire += _ => fired++;
        r.Zone.Update(.25f); // resolves the stats
        var readyTicks = 0;

        for (var i = 0; i < 60; i++)
        {
            weapon.Activate();
            r.Zone.Update(.25f);
            if (weapon.CanFire) readyTicks++;
        }

        Assert.Equal(shots, fired);
        if (shots == 0) Assert.Equal(60, readyTicks);
    }

    // Kills: Trigger that leaves _burstStarted true from the previous burst, so the next burst reads as started
    // (and ready) before its first round has flown.
    [Fact]
    public void ABurstTriggeredAfterAFiredOneIsNotReadyBeforeItsFirstRound()
    {
        var r = Build(range: 100f);
        r.Shooter.SetTarget(TargetRef.None);
        Aim(r, float2(0, 1));
        var weapon = (InstantWeapon) r.Gun.Weapon;
        r.Zone.Update(.25f);
        weapon.Activate();
        r.Zone.Update(.25f);
        Assert.False(weapon.CanFire, "control: the first burst fired and is cooling");
        for (var i = 0; i < 10 && !weapon.CanFire; i++) r.Zone.Update(.25f);
        Assert.True(weapon.CanFire, "fixture: the cooldown expired");

        weapon.Activate();

        Assert.False(weapon.CanFire);
    }
}
