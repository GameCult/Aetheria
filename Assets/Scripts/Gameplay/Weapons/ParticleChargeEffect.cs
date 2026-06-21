using System.Collections;
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
        SetEmission(ChargeEffect, true);
        
        _overloaded = false;
        _charge = 0;
    }

    public override void StopCharging()
    {
        if (_overloaded)
            SetEmission(OverchargeEffect, false);
        else
            SetEmission(ChargeEffect, false);
        StartCoroutine(Kill());
    }

    public override void Charged()
    {
        SetEmission(ChargeEffect, false);
        OverchargeEffect.Play(true);
        SetEmission(OverchargeEffect, true);
        _overloaded = true;
    }

    public override void Failed()
    {
        SetEmission(OverchargeEffect, false);
        FailureEffect.Play(true);
    }

    private void Update()
    {
        if (Weapon == null) return;
        _charge = Weapon.Charge;

        if (!_overloaded)
        {
            var main = ChargeEffect.main;
            main.simulationSpeed = _charge;
        }
    }

    private static void SetEmission(ParticleSystem particleSystem, bool enabled)
    {
        var emission = particleSystem.emission;
        emission.enabled = enabled;
    }

    private IEnumerator Kill()
    {
        while (ChargeEffect.particleCount > 0 || OverchargeEffect.particleCount > 0 || FailureEffect.particleCount > 0)
        {
            yield return null;
        }
        GetComponent<Prototype>().ReturnToPool();
    }
}
