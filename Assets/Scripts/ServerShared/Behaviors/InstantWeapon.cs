using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class InstantWeaponData : WeaponData
{
    [Inspectable, JsonProperty("count"), Key(17)]
    public PerformanceStat Count = new PerformanceStat();

    [Inspectable, JsonProperty("burstTime"), Key(18)]
    public PerformanceStat BurstTime = new PerformanceStat();
    
    [Inspectable, JsonProperty("cooldown"), Key(19), RuntimeInspectable]
    public PerformanceStat Cooldown = new PerformanceStat();
    
    [Inspectable, JsonProperty("ammoInterval"), Key(20)]  
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

public class InstantWeapon : Weapon, IProgressBehavior, IEventBehavior, IPowerConsumer
{
    private InstantWeaponData _data;

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): replaces the direct Entity.TrySpendCapacitorCharge spend at
    // both Trigger() and Execute() below -- the named, temporary exception Cut 3 left standing.
    protected readonly InputCapacitor _capacitor = new InputCapacitor();

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
    // Cut 3 (docs/fire-control-cut.md): carries the ShotId FireControl.Fire assigned, so a Unity effect
    // manager can bind its presentation to the one shot it belongs to instead of applying damage itself.
    public event Action<int> OnFire;

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
        // Safed: shooter has a target and hasn't declared hostility toward it.
        if (!StanceAllowsFire) return;
        // Cut 3 (docs/fire-control-cut.md, Q2): arc-gated the same way AI and turret fire are (Weapon.
        // ArcAllowsFire), and applied here because this is the one place every shooter's trigger passes
        // through -- a player's action-bar Activate() reaches this exactly the same way Combat.cs's and
        // TurretController.cs's Activate() calls do.
        if (!ArcAllowsFire) return;

        // If 1 ammo is consumed per burst, perform ammo and energy consumption here
        // UseAmmo returns false when triggering reload; cancel firing if that is the case
        if(_data.SingleAmmoBurst && (!TrySpendActivationEnergy() || !UseAmmo())) return;
        
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

    // Cut 4 (docs/stats-and-power-cut.md §7 Q4): capacity and rate, resolved fresh rather than off the cached
    // Energy/Cooldown properties above -- Capacitor.ResolveCapacity's precedent (PowerBus.Step, which calls
    // PowerRequest below, runs before this behaviour's own Execute this tick, so those cached properties would
    // still hold last tick's value). Capacity is one shot's own energy cost, exactly what the two deleted
    // direct-spend call sites measured. Default rate keeps sustained fire exactly at Cooldown's pace when the
    // bus grants the full request; RateOverride lets an owner replace that (ChargedWeapon does).
    protected virtual float RateOverride => 0f;

    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): Count, Energy and
    // Cooldown are all registered request fields (StatValidation.PowerRequestFields) -- read nominally here so
    // the capacitor's own Capacity/Rate this feeds into PowerRequest below never depends on this tick's own
    // grant. Nothing here needs the real, curved value: PowerRequest only ever asks "how much would a full shot
    // cost," and the actual charge added back in Execute is separately scaled by Item.PowerSupply.
    protected void RefreshInputCapacitor()
    {
        var burstCount = max(1, (int) EvaluateNominalPower(_data.Count));
        var perShotEnergy = EvaluateNominalPower(_data.Energy) / burstCount;
        var cooldown = EvaluateNominalPower(_data.Cooldown);
        _capacitor.UpdateStats(perShotEnergy, cooldown, rateOverride: RateOverride);
    }

    // Cut 4: the request PowerBus needs before Execute runs, mirroring EnergyDraw's IPowerConsumer pattern.
    public float PowerRequest(float dt)
    {
        RefreshInputCapacitor();
        return _capacitor.RequestedFill(dt);
    }

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Low -- offense. The ruling's own example of what
    // a reactor throttle should sacrifice first.
    public int DefaultPowerTier => PowerTiers.Low;

    // Cut 4: whole-or-nothing against this behaviour's own input capacitor -- the operator's ruling ("no item
    // ever receives a fraction of a shot") means a fire attempt only ever succeeds at full charge, so the cost
    // spent is always exactly Capacity, never the possibly-stale cached Energy. A consumable-hosted instance
    // (Item == null) has no PowerBus entry (Behaviors.cs: "no PowerBus entry ... always succeeds"), so nothing
    // would ever fill this capacitor -- bypass it entirely rather than starving such an instance forever.
    protected bool TrySpendActivationEnergy() => Item == null || _capacitor.TrySpend(_capacitor.Capacity);

    private bool UseAmmo()
    {
        if (_data.MagazineSize <= 1) return true;
        
        if (_ammo > 0)
        {
            _ammo--;
            return true;
        }
        
        var hasAmmo = true;
        if (_data.AmmoType.IsSet())
        {
            var cargo = Entity.FindItemInCargo(_data.AmmoType.Key);
            if (cargo != null)
            {
                var item = cargo.ItemsOfType[_data.AmmoType.Key][0];
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
        // Cut 4: this tick's grant, read back via Item.PowerSupply -- Item is non-null here because a null-Item
        // instance never reaches PowerBus.Step (see TrySpendActivationEnergy) and so never accrues charge.
        if (Item != null)
            _capacitor.AddCharge(_capacitor.RequestedFill(dt) * Item.PowerSupply);
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
            if (!_data.SingleAmmoBurst && (!TrySpendActivationEnergy() || !UseAmmo()))
            {
                _burstRemaining = 0;
                return false;
            }
            
            _burstRemaining--;
            _burstTimer -= _burstInterval;
            // Cut 3: this is fire authority's one entry point. FireControl.Fire freezes the payload snapshot
            // (Q6 -- the gun's own stats at this exact instant, base.Execute(dt) above already refreshed them
            // this tick) and queues a PendingShot; nothing downstream re-evaluates a stat.
            OnFire?.Invoke(FireControl.Fire(this, Item, Entity));
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