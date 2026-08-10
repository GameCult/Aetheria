using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleChargeEffect : WeaponChargeEffect
{
    public ParticleSystem ChargeEffect;
    public ParticleSystem OverchargeEffect;
    public ParticleSystem FailureEffect;

    private float _charge;
    private bool _overloaded;

    private void OnEnable()
    {
        ChargeEffect.Stop(true);
        OverchargeEffect.Stop(true);
        FailureEffect.Stop(true);
        ChargeEffect.Clear(true);
        OverchargeEffect.Clear(true);
        FailureEffect.Clear(true);
        ChargeEffect.Play(true);
        ChargeEffect.enableEmission = true;
        
        _overloaded = false;
        _charge = 0;
    }

    public override void StopCharging()
    {
        if (_overloaded)
            OverchargeEffect.enableEmission = false;
        else
            ChargeEffect.enableEmission = false;
        StartCoroutine(Kill());
    }

    public override void Charged()
    {
        ChargeEffect.enableEmission = false;
        OverchargeEffect.Play(true);
        OverchargeEffect.enableEmission = true;
        _overloaded = true;
    }

    public override void Failed()
    {
        OverchargeEffect.enableEmission = false;
        FailureEffect.Play(true);
    }

    private void Update()
    {
        if (Weapon == null) return;
        _charge = Weapon.Charge;

        if (!_overloaded)
        {
            ChargeEffect.playbackSpeed = _charge;
        }
    }

    IEnumerator Kill()
    {
        while (ChargeEffect.particleCount > 0 || OverchargeEffect.particleCount > 0 || FailureEffect.particleCount > 0)
        {
            yield return null;
        }
        GetComponent<Prototype>().ReturnToPool();
    }
}
