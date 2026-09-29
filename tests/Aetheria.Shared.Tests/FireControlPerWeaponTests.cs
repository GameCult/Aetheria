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

    private static GameplaySettings TestSettings(float minHit) => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        TargetArmorInfoThreshold = .2f,
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
        public Ship Target;
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
        bool turret = false, float energy = 0f, float heat = 0f, float visibility = 0f, int magazine = 0, bool singleAmmoBurst = false, bool decoy = false, bool charged = false, bool auto = false)
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
        behavior.Velocity = Constant(0);
        behavior.Spread = Constant(0);
        behavior.DamageSpread = Constant(0);
        behavior.Penetration = Constant(0);
        behavior.Energy = Constant(energy);
        behavior.Heat = Constant(heat);
        behavior.Visibility = Constant(visibility);
        behavior.MagazineSize = magazine;
        behavior.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
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
                    DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
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
        var items = new ItemManager(cache, ledger, TestSettings(minHit), _ => { });
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

        var target = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = lot++ }, new EntitySettings());
        Assert.True(target.TryEquip(Make("Gun", lot++, 1), new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3(-53, 0, -101);
        target.Position = shooter.Position + float3(0, 0, targetRange);
        shooter.Target.Value = target;
        shooter.EntityInfoGathered[target] = 1f;
        shooter.SetIff(target, true);

        zone.Update(1f);

        rig.Zone = zone;
        rig.Shooter = shooter;
        rig.Target = target;
        return rig;
    }

    private static void Aim(Rig r, float2 direction) => r.Shooter.LookDirection = float3(direction.x, 0, direction.y);

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
        r.Zone.Agents.Add(new Minion(r.Shooter));

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
        r.Zone.Agents.Add(new Minion(r.Shooter));

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

        Assert.False(FireControl.AgentFires(r.Gun.Weapon, r.Shooter, target));
    }

    [Fact]
    public void AnAgentFiresAFusedWeaponAtAValidTargetWhateverItsHitChance()
    {
        // Accuracy 0 prices every direct hit at zero; the fused weapon is unmoved.
        var r = Build(SideMount, fuse: WeaponFuse.Proximity, blast: 4f, accuracy: 0f);

        Assert.True(FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target));
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

        Assert.Equal(fires, FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target));
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
        if (noTarget) r.Shooter.Target.Value = null;
        Aim(r, float2(0, 1));

        var d = FireControl.Inspect(r.Gun.Weapon, r.Shooter, r.Shooter.Target.Value);
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
        public int Sounds;
        public int Wears;
        public float Heat;
        public bool Visible;
        public int Announced;
        public int Shots;
        public bool CanFire;
    }

    // One discrete weapon, triggered once at no target, with every cost observable: a magazine, an energy charge,
    // a sound bank, wear events, heat and visibility. Range 29.5 refuses (arming 30); Range 40 fires.
    private Costs Pull(float range, bool single, bool viaTrigger, bool staleRange = false, bool auto = false)
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 30f, range: range, energy: 50f, heat: 5000f, visibility: 100f, magazine: 5, singleAmmoBurst: single, auto: auto);
        r.Shooter.Target.Value = null;
        Aim(r, float2(0, 1));
        var weapon = (InstantWeapon) r.Gun.Weapon;
        var capacitor = (InputCapacitor) typeof(InstantWeapon).GetField("_capacitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon);
        r.Gun.Item.SoundBank = new WwiseMetaSoundBank { IncludedEvents = new[] { new WwiseMetaObject { Id = 7, Name = "gun_fire" } } };
        var costs = new Costs();
        r.Gun.Item.AudioEvents.Subscribe(_ => costs.Sounds++);
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
        costs.Visible = r.Shooter.VisibilitySources.TryGetValue(weapon, out var v) && v > 1f;
        costs.CanFire = weapon.CanFire;
        return costs;
    }

    // Control: the same weapon with Range to spare pays every cost, so a free refusal below is not a fixture that
    // never spends. Then the refusal costs nothing: no ammo, no energy, no sound, no wear, no heat, no
    // visibility, no shot. `single` is a weapon that pays once per burst, at its first unrefused round rather than at each;
    // `viaTrigger` false authorises the burst directly (the way a trigger that passed leaves it) on a weapon whose
    // Range is short, so the Trigger check cannot be what stops the round and Execute must refuse it itself.
    // Kills, one per row of the assertions: a spend that precedes the refusal (Execute or Trigger), an
    // announcement, a sound, wear, heat or visibility left ahead of the `continue`.
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
        Assert.Equal(1, fired.Sounds);
        Assert.True(fired.Wears > 0);
        Assert.True(fired.Heat > 0f);
        Assert.True(fired.Visible);
        Assert.Equal(1, fired.Announced);

        var refused = Pull(29.5f, single, viaTrigger);
        Assert.Equal(0, refused.Shots);
        Assert.Equal(0, refused.Ammo);
        Assert.Equal(0f, refused.ChargeSpent);
        Assert.Equal(0, refused.Sounds);
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
        r.Shooter.Target.Value = null;
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

        var visible = r.Shooter.VisibilitySources.TryGetValue(weapon, out var v) && v > 1f;
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
        r.Shooter.Target.Value = null;
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

        Assert.Equal(fires, FireControl.AgentFires(r.Gun.Weapon, r.Shooter, r.Target));
    }

    // Combat's group pick counts only weapons that can fire: the refused gun's group out-damages the decoy's, so a
    // pick that counted it would select a group whose weapons are then never fired, and the decoy would stay silent.
    // Kills: Combat counting a refused weapon's damage toward its group.
    [Fact]
    public void CombatDoesNotPickAGroupWhoseWeaponIsRefused()
    {
        var r = Build(fuse: WeaponFuse.Proximity, blast: 30f, range: 29.5f, targetRange: 20f, decoy: true);
        Assert.True(r.Gun.Weapon.RangeDamagePerSecond(20f) > r.Decoy.Weapon.RangeDamagePerSecond(20f), "fixture: the refused gun is the stronger group");
        r.Zone.Agents.Add(new Minion(r.Shooter));

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
        Assert.True(r.Gun.Weapon.RangeDamagePerSecond(20f) > r.Decoy.Weapon.RangeDamagePerSecond(20f), "fixture: the short gun is the stronger group");
        r.Zone.Agents.Add(new Minion(r.Shooter));

        r.Zone.Update(1f);
        r.Zone.Update(1f);

        Assert.False(r.Gun.Weapon.Firing, "a weapon that cannot reach the target is never activated");
        Assert.True(r.Decoy.Weapon.Firing, "the group that reaches is the one picked");
    }

    // Combat's range test is Designated's, not a second one: a target exactly at max range is in reach (the old
    // private test was strict). Kills: a range comparison of Combat's own.
    [Fact]
    public void CombatCountsATargetExactlyAtMaxRangeAsInReach()
    {
        var r = Build(range: 60f, targetRange: 60f);
        // The agent is driven directly, not through the zone, so the shooter stays exactly at max range: the first
        // update leaves the root state for combat, the second is combat deciding.
        var agent = new Minion(r.Shooter);
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

        Assert.True(FireControl.AgentFires(atThreshold.Gun.Weapon, atThreshold.Shooter, atThreshold.Target));
        Assert.False(FireControl.AgentFires(overThreshold.Gun.Weapon, overThreshold.Shooter, overThreshold.Target));
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

    // A refused charged weapon never starts charging: no charge heat, no audio, no start event, and it can be
    // triggered again at once. The control (Range 40) charges, sounds and heats.
    // Kills: ChargedWeapon.Activate that does not ask FireControl.
    private (int Starts, int Sounds, float Heat, bool Charging) Charge(float range)
    {
        var r = Build(charged: true, fuse: WeaponFuse.Proximity, blast: 30f, range: range);
        r.Shooter.Target.Value = null;
        Aim(r, float2(0, 1));
        var weapon = (ChargedWeapon) r.Gun.Weapon;
        r.Gun.Item.SoundBank = new WwiseMetaSoundBank { IncludedEvents = new[] { new WwiseMetaObject { Id = 7, Name = "gun_charge_play" } } };
        var starts = 0;
        var sounds = 0;
        weapon.OnStartCharging += () => starts++;
        r.Gun.Item.AudioEvents.Subscribe(_ => sounds++);
        var heatBefore = r.Gun.Item.Temperature;

        weapon.Activate();
        for (var i = 0; i < 3; i++) r.Zone.Update(.1f);

        return (starts, sounds, r.Gun.Item.Temperature - heatBefore, weapon.Progress > 0f);
    }

    [Fact]
    public void ARefusedChargedWeaponNeverStartsCharging()
    {
        var fired = Charge(40f);
        Assert.Equal(1, fired.Starts);
        Assert.True(fired.Sounds > 0, "control: charging sounds");
        Assert.True(fired.Heat > 0f, "control: charging heats");

        var refused = Charge(29.5f);
        Assert.Equal(0, refused.Starts);
        Assert.Equal(0, refused.Sounds);
        Assert.True(fired.Heat - refused.Heat > 5f, $"a refused weapon adds no charge heat ({refused.Heat} vs {fired.Heat})");
    }
    // A triggered burst has not started its cooldown, but it is not ready either: pulled again before its first round
    // flies it would restart the burst. Kills: CanFire that reads the cooldown alone.
    [Fact]
    public void ATriggeredBurstIsNotReadyBeforeItsFirstRound()
    {
        var r = Build(range: 100f);
        r.Shooter.Target.Value = null;
        Aim(r, float2(0, 1));
        var weapon = (InstantWeapon) r.Gun.Weapon;
        r.Zone.Update(.01f); // resolves the gun's stats, which size the burst
        Assert.True(weapon.CanFire);

        weapon.Activate();

        Assert.False(weapon.CanFire);
    }
}
