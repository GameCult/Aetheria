/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ConstantWeaponData : WeaponData
{
    [Inspectable, JsonProperty("ammoInterval"), Key(17)]  
    public float AmmoInterval = 1;
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new ConstantWeapon(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new ConstantWeapon(this, item);
    }
}

public class ConstantWeapon : Weapon, IProgressBehavior, IEventBehavior, IPowerConsumer
{
    private ConstantWeaponData _data;
    private int _ammo = 1;
    private float _ammoInterval;
    private float _reload;
    private bool _reloading;
    // Cut 4 (docs/fire-control-cut.md): accumulates while firing; rolled off every time it reaches
    // GameplaySettings.BeamResolveInterval, through FireControl.Fire/Step exactly like a discrete shot.
    private float _beamTimer;
    
    public override int Ammo
    {
        get => _ammo;
    }
    
    public float Progress
    {
        get { return saturate(_reload); }
    }
    
    public override float DamagePerSecond => Damage;
    public override float RangeDamagePerSecond(float range)
    {
        return Damage * _data.DamageCurve.Evaluate(saturate(unlerp(MinRange, Range, range)));
    }

    public event Action OnReloadBegin;
    public event Action OnReloadComplete;
    public event Action OnStartFiring;
    public event Action OnStopFiring;
    // Cut 4 (docs/fire-control-cut.md): carries the ShotId each beam roll's FireControl.Fire assigned, the
    // same handle InstantWeapon.OnFire gives discrete shots. No presentation binds to it yet -- kept for a
    // future pass, same as Laser.ShotId.
    public event Action<int> OnBeamShot;

    public void ResetEvents()
    {
        OnReloadBegin = null;
        OnReloadComplete = null;
        OnStartFiring = null;
        OnStopFiring = null;
        OnBeamShot = null;
    }

    public ConstantWeapon(ConstantWeaponData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public ConstantWeapon(ConstantWeaponData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Cut 3 (docs/stats-and-power-cut.md): the request the bus needs before Execute runs -- what continuing to
    // fire this tick would cost, or nothing when not firing or safed. _firing is set externally (Activate/
    // Deactivate) before Entity.Update calls PowerBus.Step, so it is already current when this runs.
    //
    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): Energy is a registered
    // request field (StatValidation.PowerRequestFields) -- read nominally, same reasoning as every other
    // IPowerConsumer in this cut. Damage (the field Cut 7 curves) is a separate stat base.Execute reads with the
    // real Evaluate.
    // Cut 5 (docs/fire-control-cut.md, 5.3, Soul finding 9): StanceAllowsFire alone let a side-mounted beam
    // fire forward -- ArcAllowsFire joins it here, matching InstantWeapon.cs's player arc gate (Q2: manual and
    // programmatic are one truth, and a continuous weapon is no exception).
    public float PowerRequest(float dt) => _firing && StanceAllowsFire && ArcAllowsFire ? EvaluateNominalPower(_data.Energy) * dt : 0f;

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Low -- offense, same as InstantWeapon.
    public int DefaultPowerTier => PowerTiers.Low;

    public override bool Execute(float dt)
    {
        base.Execute(dt);
        if (_firing && !(StanceAllowsFire && ArcAllowsFire))
        {
            // Safed: shooter has a target and hasn't declared hostility toward it, or the target no longer
            // bears (5.3: a beam obeys its arc exactly as InstantWeapon's trigger does).
            _firing = false;
            OnStopFiring?.Invoke();
            return false;
        }
        if (_firing)
        {
            // Cut 7 (docs/stats-and-power-target.md): base.Execute(dt) above already re-read Damage through
            // Evaluate(_data.Damage), so a Damage stat carrying a PowerSupply term already fires this tick for
            // less under a partial grant -- the weapon no longer safes itself off (the "flicking off gear" bug
            // the ruling names) just because the grant fell short of 1. Only true zero supply still stops it.
            if (Item != null && Item.PowerSupply <= 1e-4f)
            {
                _firing = false;
                OnStopFiring?.Invoke();
                return false;
            }
            if (_data.AmmoType.IsSet())
            {
                if (_reloading)
                {
                    _reload -= dt / _data.ReloadTime;
                    if (_reload < 0)
                    {
                        _reloading = false;
                        OnReloadComplete?.Invoke();
                    }
                    return false;
                }
                
                _ammoInterval -= dt / _data.AmmoInterval;
                if (_ammoInterval < 0)
                {
                    _ammoInterval = 1;
                    if (_data.MagazineSize > 1 && _ammo > 0) _ammo--;
                    else
                    {
                        var cargo = Entity.FindItemInCargo(_data.AmmoType.Key);
                        if (cargo != null)
                        {
                            var item = cargo.ItemsOfType[_data.AmmoType.Key][0];
                            if (item is SimpleCommodity simpleCommodity)
                                cargo.Remove(simpleCommodity, 1);
                            
                            if(_data.MagazineSize > 1)
                            {
                                _reloading = true;
                                _reload = 1;
                                OnReloadBegin?.Invoke();

                                _firing = false;
                                OnStopFiring?.Invoke();
                            }
                        }
                        return false;
                    }
                }
            }

            CauseWearDamage(dt);
            AddHeat(Evaluate(_data.Heat) * dt);
            Entity.VisibilitySources[this] = Evaluate(_data.Visibility);

            // Cut 4 (docs/fire-control-cut.md): a beam is a sequence of rolls, not a continuous truth -- one
            // FireControl.Fire per BeamResolveInterval, for that interval's worth of Damage, through the same
            // freeze-and-queue path a discrete shot uses (a beam's authored Velocity is 0, so the flight
            // commits and resolves in this same tick, R4's short-flight degradation).
            var interval = Entity.ItemManager.GameplaySettings.BeamResolveInterval;
            _beamTimer += dt;
            while (_beamTimer >= interval)
            {
                _beamTimer -= interval;
                // FireControl.Fire must run whether or not anything is listening: `OnBeamShot?.Invoke(FireControl.Fire(...))`
                // looks equivalent but is not -- C#'s null-conditional short-circuits the whole expression,
                // argument included, when OnBeamShot has no subscriber (true today, R9's "no design uses it
                // yet"), so the roll would silently never happen. Evaluate it into a local first.
                var shotId = FireControl.Fire(this, Item, Entity, Damage * interval);
                OnBeamShot?.Invoke(shotId);
            }
        }
        return true;
    }

    public override void Activate()
    {
        if(!_firing && !_reloading)
        {
            _firing = true;
            OnStartFiring?.Invoke();
        }
    }

    public override void Deactivate()
    {
        if (_firing)
        {
            _firing = false;
            OnStopFiring?.Invoke();
        }
    }
}