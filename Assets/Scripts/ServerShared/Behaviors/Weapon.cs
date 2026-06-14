/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public abstract class WeaponData : BehaviorData
{
    [Inspectable, LegacyPayloadKey(1)]
    public DamageType DamageType;

    [Inspectable, LegacyPayloadKey(2)]
    public PerformanceStat Damage = new PerformanceStat();

    [InspectableRangedFloat(0,1), LegacyPayloadKey(3)]
    public PerformanceStat Penetration = new PerformanceStat();

    [InspectableRangedFloat(0,1), LegacyPayloadKey(4)]
    public PerformanceStat DamageSpread = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(5)]
    public PerformanceStat MinRange = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(6)]
    public PerformanceStat Range = new PerformanceStat();

    [InspectableAnimationCurve, LegacyPayloadKey(7)]
    public BezierCurve DamageCurve;

    [InspectablePrefab, LegacyPayloadKey(8)]
    public string EffectPrefab;

    [InspectablePrefab, LegacyPayloadKey(9)]
    public PerformanceStat Energy = new PerformanceStat();

    [InspectablePrefab, LegacyPayloadKey(10)]
    public PerformanceStat Heat = new PerformanceStat();

    [InspectablePrefab, LegacyPayloadKey(11)]
    public PerformanceStat Visibility = new PerformanceStat();

    [LegacyPayloadKey(12)]
    public Guid AmmoType;

    [InspectablePrefab, LegacyPayloadKey(13)]
    public int MagazineSize;

    [InspectablePrefab, LegacyPayloadKey(14)]
    public float ReloadTime = 1;

    [InspectablePrefab, LegacyPayloadKey(15)]
    public PerformanceStat Spread = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(16)]
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
