/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class ConstantWeapon : Weapon, IProgressBehavior, IEventBehavior
{
    private readonly float _ammoIntervalDuration;
    private int _ammo = 1;
    private float _ammoInterval;
    private float _reload;
    private bool _reloading;

    public override int Ammo
    {
        get => _ammo;
    }
    public float AmmoIntervalProgress => _ammoInterval;
    public float ReloadProgress => _reload;
    public bool Reloading => _reloading;

    public float Progress
    {
        get { return saturate(_reload); }
    }

    public override float DamagePerSecond => Damage;
    public override float RangeDamagePerSecond(float range)
    {
        return Damage * DamageCurve.Evaluate(saturate(unlerp(MinRange, Range, range)));
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

    public ConstantWeapon(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _ammoIntervalDuration = definition.Float(17, 1);
    }

    public ConstantWeapon(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _ammoIntervalDuration = definition.Float(17, 1);
    }

    public override bool Execute(float dt)
    {
        base.Execute(dt);
        if (_firing)
        {
            if (!Entity.TryConsumeEnergy(Energy * dt))
            {
                _firing = false;
                OnStopFiring?.Invoke();
                return false;
            }
            if (!string.IsNullOrWhiteSpace(AmmoItemKey))
            {
                if (_reloading)
                {
                    _reload -= dt / ReloadTime;
                    if (_reload < 0)
                    {
                        _reloading = false;
                        OnReloadComplete?.Invoke();
                    }
                    return false;
                }

                _ammoInterval -= dt / _ammoIntervalDuration;
                if (_ammoInterval < 0)
                {
                    _ammoInterval = 1;
                    if (MagazineSize > 1 && _ammo > 0) _ammo--;
                    else
                    {
                        var cargo = Entity.FindItemInCargo(AmmoItemKey);
                        if (cargo != null)
                        {
                            var item = cargo.GetFirstItem(AmmoItemKey);
                            if (item is SimpleCommodity simpleCommodity)
                                cargo.Remove(simpleCommodity, 1);

                            if(MagazineSize > 1)
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
            AddHeat(Heat * dt);
            Entity.VisibilitySources[this] = Visibility;
        }
        return true;
    }

    public override void Activate()
    {
        if(!_firing && !_reloading)
        {
            UpdateStats();
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

    public void RestoreRuntimeState(
        bool firing,
        int ammo,
        float ammoIntervalProgress,
        float reloadProgress,
        bool reloading)
    {
        base.RestoreRuntimeState(firing);
        _ammo = ammo;
        _ammoInterval = ammoIntervalProgress;
        _reload = reloadProgress;
        _reloading = reloading;
    }
}
