using System;
using System.Collections;
using System.Collections.Generic;
using static CultMath.math;

public class InstantWeapon : Weapon, IProgressBehavior, IEventBehavior
{
    private readonly PerformanceStat _count;
    private readonly PerformanceStat _burstTime;
    private readonly PerformanceStat _cooldownDuration;
    private readonly bool _singleAmmoBurst;

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
        return EvaluateRangeDamage(range) / Cooldown;
    }

    public override int Ammo
    {
        get => _ammo;
    }
    public int BurstRemaining => _burstRemaining;
    public float BurstTimer => _burstTimer;
    public float BurstInterval => _burstInterval;
    public float CooldownProgress => _cooldown;
    public bool CoolingDown => _coolingDown;
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

    public InstantWeapon(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _count = definition.PerformanceStat(17, new PerformanceStat());
        _burstTime = definition.PerformanceStat(18, new PerformanceStat());
        _cooldownDuration = definition.PerformanceStat(19, new PerformanceStat());
        _singleAmmoBurst = definition.Bool(20);
        _ammo = MagazineSize;
        RegisterInstantWeaponStats();
    }

    public InstantWeapon(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _count = definition.PerformanceStat(17, new PerformanceStat());
        _burstTime = definition.PerformanceStat(18, new PerformanceStat());
        _cooldownDuration = definition.PerformanceStat(19, new PerformanceStat());
        _singleAmmoBurst = definition.Bool(20);
        _ammo = MagazineSize;
        RegisterInstantWeaponStats();
    }

    private void RegisterInstantWeaponStats()
    {
        RegisterPerformanceStat("Count", _count);
        RegisterPerformanceStat(nameof(BurstTime), _burstTime);
        RegisterPerformanceStat(nameof(Cooldown), _cooldownDuration);
    }

    protected void Trigger()
    {
        // If 1 ammo is consumed per burst, perform ammo and energy consumption here
        // UseAmmo returns false when triggering reload; cancel firing if that is the case
        if(_singleAmmoBurst && (!Entity.TryConsumeEnergy(Energy) || !UseAmmo())) return;

        _burstRemaining = (int) BurstCount;
        _burstInterval = BurstTime / _burstRemaining;
        _burstTimer = 0;
        _cooldown = 1;
        _coolingDown = true;
    }

    protected override void UpdateStats()
    {
        base.UpdateStats();
        BurstCount = Evaluate(_count);
        BurstTime = Evaluate(_burstTime);
        Cooldown = Evaluate(_cooldownDuration);

        Damage /= (int) BurstCount;
        Heat /= (int) BurstCount;
        Energy /= (int) BurstCount;
    }

    private bool UseAmmo()
    {
        if (MagazineSize <= 1) return true;

        if (_ammo > 0)
        {
            _ammo--;
            return true;
        }

        var hasAmmo = true;
        if (!string.IsNullOrWhiteSpace(AmmoItemKey))
        {
            var cargo = Entity.FindItemInCargo(AmmoItemKey);
            if (cargo != null)
            {
                var item = cargo.GetFirstItem(AmmoItemKey);
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
            _cooldown -= dt / (MagazineSize > 0 && _ammo == 0 ? ReloadTime : Cooldown);
            if (_cooldown < 0)
            {
                _coolingDown = false;
                if (MagazineSize > 0 && _ammo == 0)
                {
                    _ammo = MagazineSize;
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
            if (!_singleAmmoBurst && (!Entity.TryConsumeEnergy(Energy) || !UseAmmo()))
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

    public void RestoreRuntimeState(
        bool firing,
        int ammo,
        int burstRemaining,
        float burstTimer,
        float burstInterval,
        float cooldownProgress,
        bool coolingDown)
    {
        base.RestoreRuntimeState(firing);
        _ammo = ammo;
        _burstRemaining = burstRemaining;
        _burstTimer = burstTimer;
        _burstInterval = burstInterval;
        _cooldown = cooldownProgress;
        _coolingDown = coolingDown;
    }
}
