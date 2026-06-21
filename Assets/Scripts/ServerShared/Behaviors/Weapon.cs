/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using static CultMath.math;
using float4 = CultMath.float4;

public abstract class Weapon : Behavior, IActivatedBehavior
{
    private readonly PerformanceStat _damage;
    private readonly PerformanceStat _penetration;
    private readonly PerformanceStat _damageSpread;
    private readonly PerformanceStat _minRange;
    private readonly PerformanceStat _range;
    private readonly PerformanceStat _energy;
    private readonly PerformanceStat _heat;
    private readonly PerformanceStat _visibility;
    private readonly PerformanceStat _spread;
    private readonly PerformanceStat _velocity;
    private PerformanceStat _guidedProjectileThrust;
    private PerformanceStat _guidedProjectileVelocity;

    public abstract float DamagePerSecond { get; }
    public abstract float RangeDamagePerSecond(float range);
    public abstract int Ammo { get; }
    public DamageType DamageType { get; }
    public string EffectPrefab { get; }
    public string AmmoItemKey { get; }
    public int MagazineSize { get; }
    protected float ReloadTime { get; }
    protected BezierCurve DamageCurve { get; }
    public bool UsesAmmo => !string.IsNullOrWhiteSpace(AmmoItemKey);
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

    public Weapon(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _damage = definition.PerformanceStat(2, new PerformanceStat());
        _penetration = definition.PerformanceStat(3, new PerformanceStat());
        _damageSpread = definition.PerformanceStat(4, new PerformanceStat());
        _minRange = definition.PerformanceStat(5, new PerformanceStat());
        _range = definition.PerformanceStat(6, new PerformanceStat());
        _energy = definition.PerformanceStat(9, new PerformanceStat());
        _heat = definition.PerformanceStat(10, new PerformanceStat());
        _visibility = definition.PerformanceStat(11, new PerformanceStat());
        _spread = definition.PerformanceStat(15, new PerformanceStat());
        _velocity = definition.PerformanceStat(16, new PerformanceStat());
        DamageType = definition.Enum(1, default(DamageType));
        DamageCurve = definition.BezierCurve(7, null);
        EffectPrefab = definition.String(8);
        AmmoItemKey = definition.ItemKey(12);
        MagazineSize = definition.Int(13);
        ReloadTime = definition.Float(14, 1);
        RegisterWeaponStats();
        InitializeGuidedProjectileProfile(definition);
    }

    public Weapon(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _damage = definition.PerformanceStat(2, new PerformanceStat());
        _penetration = definition.PerformanceStat(3, new PerformanceStat());
        _damageSpread = definition.PerformanceStat(4, new PerformanceStat());
        _minRange = definition.PerformanceStat(5, new PerformanceStat());
        _range = definition.PerformanceStat(6, new PerformanceStat());
        _energy = definition.PerformanceStat(9, new PerformanceStat());
        _heat = definition.PerformanceStat(10, new PerformanceStat());
        _visibility = definition.PerformanceStat(11, new PerformanceStat());
        _spread = definition.PerformanceStat(15, new PerformanceStat());
        _velocity = definition.PerformanceStat(16, new PerformanceStat());
        DamageType = definition.Enum(1, default(DamageType));
        DamageCurve = definition.BezierCurve(7, null);
        EffectPrefab = definition.String(8);
        AmmoItemKey = definition.ItemKey(12);
        MagazineSize = definition.Int(13);
        ReloadTime = definition.Float(14, 1);
        RegisterWeaponStats();
        InitializeGuidedProjectileProfile(definition);
    }

    private void RegisterWeaponStats()
    {
        RegisterPerformanceStat(nameof(Damage), _damage);
        RegisterPerformanceStat(nameof(Penetration), _penetration);
        RegisterPerformanceStat(nameof(DamageSpread), _damageSpread);
        RegisterPerformanceStat(nameof(MinRange), _minRange);
        RegisterPerformanceStat(nameof(Range), _range);
        RegisterPerformanceStat(nameof(Energy), _energy);
        RegisterPerformanceStat(nameof(Heat), _heat);
        RegisterPerformanceStat(nameof(Visibility), _visibility);
        RegisterPerformanceStat(nameof(Spread), _spread);
        RegisterPerformanceStat(nameof(Velocity), _velocity);
    }

    private void InitializeGuidedProjectileProfile(RuntimeBehaviorDefinition definition)
    {
        if (string.Equals(definition.Kind, "Launcher"))
        {
            GuidedProjectileTargeting = GuidedProjectileTargetMode.TargetEntity;
            GuidedProjectileGuidanceCurve = definition.Float4Array(26, null);
            GuidedProjectileThrustCurve = definition.Float4Array(27, null);
            GuidedProjectileLiftCurve = definition.Float4Array(28, null);
            _guidedProjectileThrust = definition.PerformanceStat(29, new PerformanceStat());
            GuidedProjectileDodgeFrequency = definition.Float(30);
            _guidedProjectileVelocity = definition.PerformanceStat(31, new PerformanceStat());
        }
        else if (string.Equals(definition.Kind, "GuidedWeapon"))
        {
            GuidedProjectileTargeting = GuidedProjectileTargetMode.LookDirection;
            GuidedProjectileGuidanceCurve = definition.Float4Array(21, null);
            GuidedProjectileThrustCurve = definition.Float4Array(22, null);
            GuidedProjectileLiftCurve = definition.Float4Array(23, null);
            _guidedProjectileThrust = definition.PerformanceStat(24, new PerformanceStat());
            GuidedProjectileDodgeFrequency = definition.Float(25);
            _guidedProjectileVelocity = definition.PerformanceStat(26, new PerformanceStat());
        }

        RegisterPerformanceStat("Thrust", _guidedProjectileThrust);
        RegisterPerformanceStat("MissileVelocity", _guidedProjectileVelocity);
    }

    protected virtual void UpdateStats()
    {
        Damage = Evaluate(_damage);
        Penetration = Evaluate(_penetration);
        DamageSpread = Evaluate(_damageSpread);
        MinRange = Evaluate(_minRange);
        Range = Evaluate(_range);
        Energy = Evaluate(_energy);
        Heat = Evaluate(_heat);
        Visibility = Evaluate(_visibility);
        Spread = Evaluate(_spread);
        Velocity = Evaluate(_velocity);
    }

    protected float EvaluateRangeDamage(float range)
    {
        var normalizedRange = saturate(unlerp(MinRange, Range, range));
        return Evaluate(_damage, StatConditionMask.Range, normalizedRange) *
               DamageCurve.Evaluate(normalizedRange);
    }

    protected float EvaluateRangeDamage(float range, StatConditionMask condition, float value)
    {
        var normalizedRange = saturate(unlerp(MinRange, Range, range));
        return Evaluate(_damage, StatConditionMask.Range, normalizedRange, condition, value) *
               DamageCurve.Evaluate(normalizedRange);
    }

    protected float EvaluateDamage(StatConditionMask condition, float value)
    {
        return Evaluate(_damage, condition, value);
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

    public virtual void RestoreRuntimeState(bool firing)
    {
        _firing = firing;
    }

    public float EvaluateRange()
    {
        return Evaluate(_range);
    }

    public float EvaluateVelocity()
    {
        return Evaluate(_velocity);
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
