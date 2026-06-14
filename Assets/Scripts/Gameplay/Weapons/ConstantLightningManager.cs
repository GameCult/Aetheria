using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ConstantLightningManager : ConstantWeaponEffectManager
{
    public Prototype LightningPrototype;

    private Dictionary<EquippedItem, ConstantLightning> _bolts = new Dictionary<EquippedItem, ConstantLightning>();
    
    public override void StartFiring(ConstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = LightningPrototype.Instantiate<ConstantLightning>();
        p.Source = source;
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        p.Barrel = barrel;
        p.Damage = weapon.Damage;
        p.Range = weapon.Range;
        p.Penetration = weapon.Penetration;
        p.Spread = weapon.DamageSpread;
        p.DamageType = weapon.DamageType;
        _bolts.Add(item, p);
    }

    public override void StopFiring(EquippedItem item)
    {
        _bolts[item].Stop();
        _bolts.Remove(item);
    }
}
