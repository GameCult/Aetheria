using System;
using System.Collections;
using System.Collections.Generic;
using static CultMath.math;

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
        return EvaluateRangeDamage(range, StatConditionMask.Charge, saturate(_charge)) *
               lerp(1, _chargeFiringDamageMultiplier, saturate(_charge)) *
               _chargeFiringDamageMultiplier /
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

    public ChargedWeapon(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _chargeTime = definition.PerformanceStat(21, new PerformanceStat());
        _chargeEnergy = definition.PerformanceStat(22, new PerformanceStat());
        _chargeHeat = definition.PerformanceStat(23, new PerformanceStat());
        _canFireEarly = definition.Bool(24);
        _failureCharge = definition.Float(25);
        _failureDamage = definition.Float(26, 1);
        _chargeFiringDamageMultiplier = definition.Float(27, 1);
        _chargeFiringSpreadMultiplier = definition.Float(28, 1);
        _chargeFiringBurstCountMultiplier = definition.Float(29, 1);
        _chargeFiringVisibilityMultiplier = definition.Float(30, 1);
        _chargeFiringVelocityMultiplier = definition.Float(31, 1);
        _chargeFiringHeatMultiplier = definition.Float(32, 1);
        RegisterChargedWeaponStats();
    }

    public ChargedWeapon(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _chargeTime = definition.PerformanceStat(21, new PerformanceStat());
        _chargeEnergy = definition.PerformanceStat(22, new PerformanceStat());
        _chargeHeat = definition.PerformanceStat(23, new PerformanceStat());
        _canFireEarly = definition.Bool(24);
        _failureCharge = definition.Float(25);
        _failureDamage = definition.Float(26, 1);
        _chargeFiringDamageMultiplier = definition.Float(27, 1);
        _chargeFiringSpreadMultiplier = definition.Float(28, 1);
        _chargeFiringBurstCountMultiplier = definition.Float(29, 1);
        _chargeFiringVisibilityMultiplier = definition.Float(30, 1);
        _chargeFiringVelocityMultiplier = definition.Float(31, 1);
        _chargeFiringHeatMultiplier = definition.Float(32, 1);
        RegisterChargedWeaponStats();
    }

    private void RegisterChargedWeaponStats()
    {
        RegisterPerformanceStat(nameof(ChargeTime), _chargeTime);
        RegisterPerformanceStat(nameof(ChargeEnergy), _chargeEnergy);
        RegisterPerformanceStat(nameof(ChargeHeat), _chargeHeat);
    }

    protected override void UpdateStats()
    {
        base.UpdateStats();
        ChargeTime = Evaluate(_chargeTime);
        ChargeEnergy = Evaluate(_chargeEnergy);
        ChargeHeat = Evaluate(_chargeHeat);
        Damage = EvaluateDamage(StatConditionMask.Charge, saturate(_charge)) *
                 lerp(1, _chargeFiringDamageMultiplier, saturate(_charge));
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

    public void RestoreRuntimeState(
        bool firing,
        int ammo,
        int burstRemaining,
        float burstTimer,
        float burstInterval,
        float cooldownProgress,
        bool coolingDown,
        bool charging,
        bool charged,
        float charge)
    {
        base.RestoreRuntimeState(
            firing,
            ammo,
            burstRemaining,
            burstTimer,
            burstInterval,
            cooldownProgress,
            coolingDown);
        _charging = charging;
        _charged = charged;
        _charge = charge;
    }
}
