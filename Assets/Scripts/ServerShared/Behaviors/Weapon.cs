/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public abstract class WeaponConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public DamageType DamageType;

    [Inspectable]
    public PerformanceStat Damage = new PerformanceStat();

    [InspectableRangedFloat(0,1)]
    public PerformanceStat Penetration = new PerformanceStat();

    [InspectableRangedFloat(0,1)]
    public PerformanceStat DamageSpread = new PerformanceStat();

    [Inspectable]
    public PerformanceStat MinRange = new PerformanceStat();

    [Inspectable]
    public PerformanceStat Range = new PerformanceStat();

    [InspectableAnimationCurve]
    public BezierCurve DamageCurve;

    [InspectablePrefab]
    public string EffectPrefab;

    [InspectablePrefab]
    public PerformanceStat Energy = new PerformanceStat();

    [InspectablePrefab]
    public PerformanceStat Heat = new PerformanceStat();

    [InspectablePrefab]
    public PerformanceStat Visibility = new PerformanceStat();
    public Guid AmmoType;

    [InspectablePrefab]
    public int MagazineSize;

    [InspectablePrefab]
    public float ReloadTime = 1;

    [InspectablePrefab]
    public PerformanceStat Spread = new PerformanceStat();

    [Inspectable]
    public PerformanceStat Velocity = new PerformanceStat();
}

public abstract class Weapon : Behavior, IActivatedBehavior
{
    private WeaponConfig _data;
    private PerformanceStat _guidedProjectileThrust;
    private PerformanceStat _guidedProjectileVelocity;

    public abstract float DamagePerSecond { get; }
    public abstract float RangeDamagePerSecond(float range);
    public abstract int Ammo { get; }
    public DamageType DamageType { get; }
    public string EffectPrefab { get; }
    public Guid AmmoType { get; }
    public int MagazineSize { get; }
    public bool UsesAmmo => AmmoType != Guid.Empty;
    public GuidedProjectileTargetMode GuidedProjectileTargeting { get; private set; }
    public bool HasGuidedProjectileProfile => GuidedProjectileTargeting != GuidedProjectileTargetMode.None;
    public float4[] GuidedProjectileGuidanceCurve { get; private set; }
    public float4[] GuidedProjectileLiftCurve { get; private set; }
    public float4[] GuidedProjectileThrustCurve { get; private set; }
    public float GuidedProjectileDodgeFrequency { get; private set; }

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

    public Weapon(WeaponConfig data, EquippedItem item) : base(data, item)
    {
        _data = data;
        DamageType = data.DamageType;
        EffectPrefab = data.EffectPrefab ?? "";
        AmmoType = data.AmmoType;
        MagazineSize = data.MagazineSize;
        InitializeGuidedProjectileProfile(data);
    }

    public Weapon(WeaponConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        DamageType = data.DamageType;
        EffectPrefab = data.EffectPrefab ?? "";
        AmmoType = data.AmmoType;
        MagazineSize = data.MagazineSize;
        InitializeGuidedProjectileProfile(data);
    }

    private void InitializeGuidedProjectileProfile(WeaponConfig data)
    {
        if (data is LauncherConfig launcher)
        {
            GuidedProjectileTargeting = GuidedProjectileTargetMode.TargetEntity;
            GuidedProjectileGuidanceCurve = launcher.GuidanceCurve;
            GuidedProjectileLiftCurve = launcher.LiftCurve;
            GuidedProjectileThrustCurve = launcher.ThrustCurve;
            GuidedProjectileDodgeFrequency = launcher.DodgeFrequency;
            _guidedProjectileThrust = launcher.Thrust;
            _guidedProjectileVelocity = launcher.MissileVelocity;
        }
        else if (data is GuidedWeaponConfig guidance)
        {
            GuidedProjectileTargeting = GuidedProjectileTargetMode.LookDirection;
            GuidedProjectileGuidanceCurve = guidance.GuidanceCurve;
            GuidedProjectileLiftCurve = guidance.LiftCurve;
            GuidedProjectileThrustCurve = guidance.ThrustCurve;
            GuidedProjectileDodgeFrequency = guidance.DodgeFrequency;
            _guidedProjectileThrust = guidance.Thrust;
            _guidedProjectileVelocity = guidance.MissileVelocity;
        }
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

    public float EvaluateRange()
    {
        return Evaluate(_data.Range);
    }

    public float EvaluateVelocity()
    {
        return Evaluate(_data.Velocity);
    }

    public float EvaluateGuidedProjectileThrust()
    {
        return _guidedProjectileThrust == null ? 0 : Evaluate(_guidedProjectileThrust);
    }

    public float EvaluateGuidedProjectileVelocity()
    {
        return _guidedProjectileVelocity == null ? 0 : Evaluate(_guidedProjectileVelocity);
    }
}

public enum GuidedProjectileTargetMode
{
    None,
    TargetEntity,
    LookDirection
}
