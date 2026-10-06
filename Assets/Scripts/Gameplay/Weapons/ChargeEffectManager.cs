using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChargeEffectManager : MonoBehaviour
{
    public Prototype EffectPrototype;
    
    private Dictionary<ChargedWeapon, WeaponChargeEffect> ActiveEffects = new Dictionary<ChargedWeapon, WeaponChargeEffect>();
    
    public void StartCharging(ChargedWeapon weapon, EquippedItem item, EntityInstance source)
    {
        var effect = EffectPrototype.Instantiate<WeaponChargeEffect>();
        var hp = item.Hardpoint;
        var barrel = source.GetBarrel(hp);
        var t = effect.transform;
        t.SetParent(barrel);
        t.forward = barrel.forward;
        t.position = barrel.position;
        effect.Weapon = weapon;
        ActiveEffects[weapon] = effect;
    }
    
    public void StopCharging(ChargedWeapon weapon)
    {
        ActiveEffects[weapon].StopCharging();
    }

    public void Charged(ChargedWeapon weapon)
    {
        ActiveEffects[weapon].Charged();
    }

    public void Failed(ChargedWeapon weapon)
    {
        ActiveEffects[weapon].Failed();
    }
}