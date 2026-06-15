using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class ChargedWeaponConfig : InstantWeaponConfig
{
    [Inspectable]
    public PerformanceStat ChargeTime = new PerformanceStat();

    [Inspectable]
    public PerformanceStat ChargeEnergy = new PerformanceStat();

    [Inspectable]
    public PerformanceStat ChargeHeat = new PerformanceStat();

    [Inspectable]
    public bool CanFireEarly;

    [Inspectable]
    public float FailureCharge;

    [Inspectable]
    public float FailureDamage = 1;

    [Inspectable]
    public float ChargeFiringDamageMultiplier = 1;

    [Inspectable]
    public float ChargeFiringSpreadMultiplier = 1;

    [Inspectable]
    public float ChargeFiringBurstCountMultiplier = 1;

    [Inspectable]
    public float ChargeFiringVisibilityMultiplier = 1;

    [Inspectable]
    public float ChargeFiringVelocityMultiplier = 1;

    [Inspectable]
    public float ChargeFiringHeatMultiplier = 1;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new ChargedWeapon(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new ChargedWeapon(this, item);
    }
}

public class ChargedWeapon : InstantWeapon
{
    private readonly PerformanceStat _chargeTime;
    private readonly PerformanceStat _chargeEnergy;
    private readonly PerformanceStat _chargeHeat;
    private readonly bool _canFireEarly;
    private readonly float _failureCharge;
    private readonly float _failureDamage;
    private readonly float _chargeFiringDamageMultiplier;
    private readonly float _chargeFiringSpreadMultiplier;
    private readonly float _chargeFiringBurstCountMultiplier;
    private readonly float _chargeFiringVisibilityMultiplier;
    private readonly float _chargeFiringVelocityMultiplier;
    private readonly float _chargeFiringHeatMultiplier;
    private bool _charging;
    private bool _charged;
    private float _charge;
    private float _progress;

    public float ChargeTime { get; protected set; }
    public float ChargeEnergy { get; protected set; }
    public float ChargeHeat { get; protected set; }

    public override float DamagePerSecond => Damage * _chargeFiringDamageMultiplier / (Cooldown + ChargeTime);
    public override float RangeDamagePerSecond(float range)
    {
        return Damage *
               _chargeFiringDamageMultiplier *
               DamageCurve.Evaluate(saturate(unlerp(MinRange, Range, range))) /
               (Cooldown + ChargeTime);
    }

    public event Action OnStartCharging;
    public event Action OnStopCharging;
    public event Action OnCharged;
    public event Action OnFailed;

    public override void ResetEvents()
    {
        base.ResetEvents();
        OnStartCharging = null;
        OnStopCharging = null;
        OnCharged = null;
        OnFailed = null;
    }

    public override float Progress => saturate(_charging ? _charge : _cooldown);

    public float Charge
    {
        get => _charge;
    }
    public bool Charging => _charging;
    public bool Charged => _charged;

    public ChargedWeapon(ChargedWeaponConfig data, EquippedItem item) : base(data, item)
    {
        _chargeTime = data.ChargeTime;
        _chargeEnergy = data.ChargeEnergy;
        _chargeHeat = data.ChargeHeat;
        _canFireEarly = data.CanFireEarly;
        _failureCharge = data.FailureCharge;
        _failureDamage = data.FailureDamage;
        _chargeFiringDamageMultiplier = data.ChargeFiringDamageMultiplier;
        _chargeFiringSpreadMultiplier = data.ChargeFiringSpreadMultiplier;
        _chargeFiringBurstCountMultiplier = data.ChargeFiringBurstCountMultiplier;
        _chargeFiringVisibilityMultiplier = data.ChargeFiringVisibilityMultiplier;
        _chargeFiringVelocityMultiplier = data.ChargeFiringVelocityMultiplier;
        _chargeFiringHeatMultiplier = data.ChargeFiringHeatMultiplier;
    }

    public ChargedWeapon(ChargedWeaponConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _chargeTime = data.ChargeTime;
        _chargeEnergy = data.ChargeEnergy;
        _chargeHeat = data.ChargeHeat;
        _canFireEarly = data.CanFireEarly;
        _failureCharge = data.FailureCharge;
        _failureDamage = data.FailureDamage;
        _chargeFiringDamageMultiplier = data.ChargeFiringDamageMultiplier;
        _chargeFiringSpreadMultiplier = data.ChargeFiringSpreadMultiplier;
        _chargeFiringBurstCountMultiplier = data.ChargeFiringBurstCountMultiplier;
        _chargeFiringVisibilityMultiplier = data.ChargeFiringVisibilityMultiplier;
        _chargeFiringVelocityMultiplier = data.ChargeFiringVelocityMultiplier;
        _chargeFiringHeatMultiplier = data.ChargeFiringHeatMultiplier;
    }

    protected override void UpdateStats()
    {
        base.UpdateStats();
        ChargeTime = Evaluate(_chargeTime);
        ChargeEnergy = Evaluate(_chargeEnergy);
        ChargeHeat = Evaluate(_chargeHeat);
        Damage *= lerp(1, _chargeFiringDamageMultiplier, saturate(_charge));
        Heat *= lerp(1, _chargeFiringHeatMultiplier, saturate(_charge));
        Spread *= lerp(1, _chargeFiringSpreadMultiplier, saturate(_charge));
        BurstCount *= lerp(1, _chargeFiringBurstCountMultiplier, saturate(_charge));
        Visibility *= lerp(1, _chargeFiringVisibilityMultiplier, saturate(_charge));
        Velocity *= lerp(1, _chargeFiringVelocityMultiplier, saturate(_charge));
    }

    public override bool Execute(float dt)
    {
        if (_charging)
        {
            _charge += dt / ChargeTime;
            Item.SetAudioParameter(SpecialAudioParameter.ChargeLevel, saturate(_charge));
            if (!_charged)
            {
                AddHeat(ChargeHeat * (dt / ChargeTime));
                if(_charge > 1)
                {
                    _charged = true;
                    OnCharged?.Invoke();
                }
            }
            if (_failureCharge > 1 && _charge > _failureCharge)
            {
                _charging = false;
                _cooldown = 1;
                _coolingDown = true;
                _charge = 0;
                OnFailed?.Invoke();
                Item.FireAudioEvent(ChargedWeaponAudioEvent.Fail);
                CauseDamage(_failureDamage);
            }
        }
        return base.Execute(dt);
    }

    public override void Activate()
    {
        if(!_charging && !_coolingDown)
        {
            OnStartCharging?.Invoke();
            Item.FireAudioEvent(ChargedWeaponAudioEvent.Start);
            _charging = true;
            _charged = false;
        }
    }

    public override void Deactivate()
    {
        if (_charging)
        {
            if (_canFireEarly || _charge > 1)
            {
                Trigger();
                _charge = 0;
            }
            OnStopCharging?.Invoke();
            Item.FireAudioEvent(ChargedWeaponAudioEvent.Stop);
            _charging = false;
        }
    }
}

