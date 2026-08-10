using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class InstantWeaponData : WeaponData
{
    [Inspectable, JsonProperty("count"), Key(17)]
    public PerformanceStat Count = new PerformanceStat();

    [Inspectable, JsonProperty("burstTime"), Key(18)]
    public PerformanceStat BurstTime = new PerformanceStat();
    
    [Inspectable, JsonProperty("cooldown"), Key(19), RuntimeInspectable]
    public PerformanceStat Cooldown = new PerformanceStat();
    
    [InspectablePrefab, JsonProperty("ammoInterval"), Key(20)]  
    public bool SingleAmmoBurst;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new InstantWeapon(this, item);
    }
}

public class InstantWeapon : Weapon, IProgressBehavior, IEventBehavior
{
    private InstantWeaponData _data;

    protected int _burstRemaining;
    private float _burstTimer;
    private float _burstInterval;
    protected float _cooldown; // Normalized
    private int _ammo = 0;
    protected bool _coolingDown;

    public float BurstCount { get; protected set; }
    public float BurstTime { get; protected set; }
    public float Cooldown { get; protected set; }
    public virtual bool CanFire
    {
        get => !_coolingDown;
    }

    public override float DamagePerSecond => Damage / Cooldown;
    public override float RangeDamagePerSecond(float range)
    {
        return Damage * _data.DamageCurve.Evaluate(saturate(unlerp(MinRange, Range, range))) / Cooldown;
    }

    public override int Ammo
    {
        get => _ammo;
    }
    public virtual float Progress => saturate(_cooldown);

    public event Action OnReloadBegin;
    public event Action OnReloadComplete;
    public event Action OnCooldownComplete;
    public event Action OnFire;

    public virtual void ResetEvents()
    {
        OnReloadBegin = null;
        OnReloadComplete = null;
        OnCooldownComplete = null;
        OnFire = null;
    }

    public InstantWeapon(InstantWeaponData data, EquippedItem item) : base(data, item)
    {
        _data = data;
        _ammo = data.MagazineSize;
    }

    public InstantWeapon(InstantWeaponData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        _ammo = data.MagazineSize;
    }

    protected void Trigger()
    {
        // If 1 ammo is consumed per burst, perform ammo and energy consumption here
        // UseAmmo returns false when triggering reload; cancel firing if that is the case
        if(_data.SingleAmmoBurst && (!Entity.TryConsumeEnergy(Energy) || !UseAmmo())) return;
        
        _burstRemaining = (int) BurstCount;
        _burstInterval = BurstTime / _burstRemaining;
        _burstTimer = 0;
        _cooldown = 1;
        _coolingDown = true;
    }

    protected override void UpdateStats()
    {
        base.UpdateStats();
        BurstCount = Evaluate(_data.Count);
        BurstTime = Evaluate(_data.BurstTime);
        Cooldown = Evaluate(_data.Cooldown);

        Damage /= (int) BurstCount;
        Heat /= (int) BurstCount;
        Energy /= (int) BurstCount;
    }

    private bool UseAmmo()
    {
        if (_data.MagazineSize <= 1) return true;
        
        if (_ammo > 0)
        {
            _ammo--;
            return true;
        }
        
        var hasAmmo = true;
        if (_data.AmmoType != Guid.Empty)
        {
            var cargo = Entity.FindItemInCargo(_data.AmmoType);
            if (cargo != null)
            {
                var item = cargo.ItemsOfType[_data.AmmoType][0];
                if (item is SimpleCommodity simpleCommodity)
                    cargo.Remove(simpleCommodity, 1);
            }
            else hasAmmo = false;
        }
        if(hasAmmo)
        {
            OnReloadBegin?.Invoke();
            _cooldown = 1;
            _coolingDown = true;
            _firing = false;
        }
        _burstRemaining = 0;
        return false;

    }

    public override bool Execute(float dt)
    {
        base.Execute(dt);
        if (_coolingDown)
        {
            _cooldown -= dt / (_data.MagazineSize > 0 && _ammo == 0 ? _data.ReloadTime : Cooldown);
            if (_cooldown < 0)
            {
                _coolingDown = false;
                if (_data.MagazineSize > 0 && _ammo == 0)
                {
                    _ammo = _data.MagazineSize;
                    OnReloadComplete?.Invoke();
                }
                else
                    OnCooldownComplete?.Invoke();
            }
        }

        var firedThisFrame = false;
        _burstTimer += dt;
        while (_burstRemaining > 0 && _burstTimer > 0)
        {
            // If multiple ammo is consumed per burst, perform ammo and energy consumption here
            // UseAmmo returns false when triggering reload; cancel firing if that is the case
            if (!_data.SingleAmmoBurst && (!Entity.TryConsumeEnergy(Energy) || !UseAmmo()))
            {
                _burstRemaining = 0;
                return false;
            }
            
            _burstRemaining--;
            _burstTimer -= _burstInterval;
            OnFire?.Invoke();
            if(!firedThisFrame)
            {
                Item.FireAudioEvent(WeaponAudioEvent.Fire);
                firedThisFrame = true;
            }
            CauseWearDamage(1);
            AddHeat(Heat);
            Entity.VisibilitySources[this] = Visibility;
        }
        return true;
    }

    public override void Activate()
    {
        if(CanFire)
            Trigger();
        base.Activate();
    }
}