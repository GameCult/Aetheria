using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ConstantLightningManager : ConstantWeaponEffectManager
{
    public Prototype LightningPrototype;

    private Dictionary<EquippedItem, ConstantLightning> _bolts = new Dictionary<EquippedItem, ConstantLightning>();
    
    public override void StartFiring(WeaponData data, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = LightningPrototype.Instantiate<ConstantLightning>();
        p.Source = source;
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        p.Barrel = barrel;
        p.Damage = item.Evaluate(data.Damage);
        p.Range = item.Evaluate(data.Range);
        p.Penetration = item.Evaluate(data.Penetration);
        p.Spread = item.Evaluate(data.DamageSpread);
        p.DamageType = data.DamageType;
        _bolts.Add(item, p);
    }

    public override void StopFiring(EquippedItem item)
    {
        _bolts[item].Stop();
        _bolts.Remove(item);
    }
}
