/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ConstantWeaponData : WeaponData
{
    [InspectablePrefab, JsonProperty("ammoInterval"), Key(17)]  
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

public class ConstantWeapon : Weapon, IProgressBehavior, IEventBehavior
{
    private ConstantWeaponData _data;
    private int _ammo = 1;
    private float _ammoInterval;
    private float _reload;
    private bool _reloading;
    
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
    
    public void ResetEvents()
    {
        OnReloadBegin = null;
        OnReloadComplete = null;
        OnStartFiring = null;
        OnStopFiring = null;
    }

    public ConstantWeapon(ConstantWeaponData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public ConstantWeapon(ConstantWeaponData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        base.Execute(dt);
        if (_firing)
        {
            if (!Entity.TryConsumeEnergy(Evaluate(_data.Energy) * dt))
            {
                _firing = false;
                OnStopFiring?.Invoke();
                return false;
            }
            if (_data.AmmoType != Guid.Empty)
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
                        var cargo = Entity.FindItemInCargo(_data.AmmoType);
                        if (cargo != null)
                        {
                            var item = cargo.ItemsOfType[_data.AmmoType][0];
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