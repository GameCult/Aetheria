/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, 
 Union(0, typeof(InstantWeaponData)),
 Union(1, typeof(LauncherData)),
 Union(2, typeof(ConstantWeaponData)), 
 Union(3, typeof(ChargedWeaponData)), 
 RuntimeInspectable]
public abstract class WeaponData : BehaviorData
{
    [Inspectable, JsonProperty("damageType"), Key(1), RuntimeInspectable]
    public DamageType DamageType;

    [Inspectable, JsonProperty("damage"), Key(2), RuntimeInspectable]
    public PerformanceStat Damage = new PerformanceStat();

    [Inspectable, CultInspectorRange(0, 1), JsonProperty("penetration"), Key(3), RuntimeInspectable]
    public PerformanceStat Penetration = new PerformanceStat();

    [Inspectable, CultInspectorRange(0, 1), JsonProperty("damageSpread"), Key(4)]
    public PerformanceStat DamageSpread = new PerformanceStat();

    [Inspectable, JsonProperty("minRange"), Key(5)]
    public PerformanceStat MinRange = new PerformanceStat();

    [Inspectable, JsonProperty("range"), Key(6)]
    public PerformanceStat Range = new PerformanceStat();

    [InspectableAnimationCurve, JsonProperty("damageCurve"), Key(7)]
    public BezierCurve DamageCurve;
    
    [Inspectable, CultInspectorAssetGuid, JsonProperty("effect"), Key(8)]  
    public string EffectPrefab;
    
    [Inspectable, JsonProperty("energy"), Key(9), RuntimeInspectable]  
    public PerformanceStat Energy = new PerformanceStat();
    
    [Inspectable, JsonProperty("heat"), Key(10), RuntimeInspectable]  
    public PerformanceStat Heat = new PerformanceStat();
    
    [Inspectable, JsonProperty("visibility"), Key(11), RuntimeInspectable]  
    public PerformanceStat Visibility = new PerformanceStat();
    
    [Inspectable, JsonProperty("ammo"), Key(12), RuntimeInspectable]  
    public CultRecordRef<SimpleCommodityData> AmmoType;

    [Inspectable, JsonProperty("magSize"), Key(13)]
    public int MagazineSize;
    
    [Inspectable, JsonProperty("reloadTime"), Key(14)]  
    public float ReloadTime = 1;
    
    [Inspectable, JsonProperty("spread"), Key(15)]  
    public PerformanceStat Spread = new PerformanceStat();

    [Inspectable, JsonProperty("velocity"), Key(16)]
    public PerformanceStat Velocity = new PerformanceStat();
}

public abstract class Weapon : Behavior, IActivatedBehavior
{
    private WeaponData _data;

    public abstract float DamagePerSecond { get; }
    public abstract float RangeDamagePerSecond(float range);
    public abstract int Ammo { get; }
    public WeaponData WeaponData => _data;
    
    public float Damage { get; protected set; }
    public float Penetration { get; protected set; }
    public float DamageSpread { get; protected set; }
    public float MinRange { get; protected set; }
    public float Range { get; protected set; }
    public float Energy { get; protected set; }
    public float Heat { get; protected set; }
    public float Visibility { get; protected set; }
    public float Spread { get; protected set; }
    public float Velocity { get; protected set; }

    protected bool _firing;

    public bool Firing
    {
        get => _firing;
    }

    // Single shared fire gate for player and AI shooters alike: weapons are safed by the
    // shooter's OWN declared stance toward its target, not the target's stance toward the shooter.
    // With no target set, behaviour is unchanged (nothing to be safe about).
    public bool StanceAllowsFire => Entity.Target.Value == null || Entity.IsHostileTo(Entity.Target.Value);

    // Cut 3 (docs/fire-control-cut.md, Q2): player fire is arc-gated like everyone else's. This is the same
    // predicate (FireControl.InArc) Combat.cs and TurretController.cs gate AI and turret fire with, read at
    // the one point every shooter's trigger passes through (InstantWeapon.Trigger) -- not a parallel check, and
    // not the AI's own AgentMinHitProbability heuristic, which is a "worth it" decision layered above this
    // gate, not the gate itself. With no target set, behaviour is unchanged (nothing to bear on).
    // Cut 5, 5.4 (Soul finding 11): the point-blank special case this used to carry moved into InArc itself --
    // one bearing test, one owner, so every caller (not just this one) gets a point-blank shot that bears.
    public bool ArcAllowsFire
    {
        get
        {
            var target = Entity.Target.Value;
            if (target == null) return true;
            var toTarget = target.Position - Entity.Position;
            return FireControl.InArc(Item, toTarget);
        }
    }

    public Weapon(WeaponData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    
    public Weapon(WeaponData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    protected virtual void UpdateStats()
    {
        Damage = Evaluate(_data.Damage);
        Penetration = Evaluate(_data.Penetration);
        DamageSpread = Evaluate(_data.DamageSpread);
        MinRange = Evaluate(_data.MinRange);
        Range = Evaluate(_data.Range);
        Energy = Evaluate(_data.Energy);
        Heat = Evaluate(_data.Heat);
        Visibility = Evaluate(_data.Visibility);
        Spread = Evaluate(_data.Spread);
        Velocity = Evaluate(_data.Velocity);
    }

    public override bool Execute(float dt)
    {
        UpdateStats();
        return true;
    }

    public virtual void Activate()
    {
        _firing = true;
    }

    public virtual void Deactivate()
    {
        _firing = false;
    }
}
