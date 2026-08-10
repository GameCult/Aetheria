using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ChargedWeaponData : InstantWeaponData
{
    [Inspectable, JsonProperty("chargeTime"), Key(21), RuntimeInspectable]
    public PerformanceStat ChargeTime = new PerformanceStat();
    
    [Inspectable, JsonProperty("chargeEnergy"), Key(22), RuntimeInspectable]
    public PerformanceStat ChargeEnergy = new PerformanceStat();
    
    [Inspectable, JsonProperty("chargeHeat"), Key(23), RuntimeInspectable]
    public PerformanceStat ChargeHeat = new PerformanceStat();

    [Inspectable, JsonProperty("canFireEarly"), Key(24)]
    public bool CanFireEarly;

    [Inspectable, JsonProperty("failureCharge"), Key(25)]
    public float FailureCharge;

    [Inspectable, JsonProperty("failureDamage"), Key(26)]
    public float FailureDamage = 1;

    [Inspectable, JsonProperty("chargeDamage"), Key(27)]
    public float ChargeFiringDamageMultiplier = 1;

    [Inspectable, JsonProperty("chargeSpread"), Key(28)]
    public float ChargeFiringSpreadMultiplier = 1;

    [Inspectable, JsonProperty("chargeBurstCount"), Key(29)]
    public float ChargeFiringBurstCountMultiplier = 1;

    [Inspectable, JsonProperty("chargeVisibility"), Key(30)]
    public float ChargeFiringVisibilityMultiplier = 1;

    [Inspectable, JsonProperty("chargeVelocity"), Key(31)]
    public float ChargeFiringVelocityMultiplier = 1;

    [Inspectable, JsonProperty("chargeHeatMul"), Key(32)]
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
    private ChargedWeaponData _data;
    private bool _charging;
    private bool _charged;
    private float _charge;
    private float _progress;
    
    public float ChargeTime { get; protected set; }
    public float ChargeEnergy { get; protected set; }
    public float ChargeHeat { get; protected set; }
    
    public override float DamagePerSecond => Damage * _data.ChargeFiringDamageMultiplier / (Cooldown + ChargeTime);
    public override float RangeDamagePerSecond(float range)
    {
        return Damage *
               _data.ChargeFiringDamageMultiplier *
               _data.DamageCurve.Evaluate(saturate(unlerp(MinRange, Range, range))) /
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
    
    public ChargedWeapon(ChargedWeaponData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    
    public ChargedWeapon(ChargedWeaponData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    protected override void UpdateStats()
    {
        base.UpdateStats();
        ChargeTime = Evaluate(_data.ChargeTime);
        ChargeEnergy = Evaluate(_data.ChargeEnergy);
        ChargeHeat = Evaluate(_data.ChargeHeat);
        Damage *= lerp(1, _data.ChargeFiringDamageMultiplier, saturate(_charge));
        Heat *= lerp(1, _data.ChargeFiringHeatMultiplier, saturate(_charge));
        Spread *= lerp(1, _data.ChargeFiringSpreadMultiplier, saturate(_charge));
        BurstCount *= lerp(1, _data.ChargeFiringBurstCountMultiplier, saturate(_charge));
        Visibility *= lerp(1, _data.ChargeFiringVisibilityMultiplier, saturate(_charge));
        Velocity *= lerp(1, _data.ChargeFiringVelocityMultiplier, saturate(_charge));
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
            if (_data.FailureCharge > 1 && _charge > _data.FailureCharge)
            {
                _charging = false;
                _cooldown = 1;
                _coolingDown = true;
                _charge = 0;
                OnFailed?.Invoke();
                Item.FireAudioEvent(ChargedWeaponAudioEvent.Fail);
                CauseDamage(_data.FailureDamage);
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
            if (_data.CanFireEarly || _charge > 1)
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

