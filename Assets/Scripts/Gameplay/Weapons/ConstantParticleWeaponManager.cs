using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstantParticleWeaponManager : ConstantWeaponEffectManager
{
    public Prototype WeaponPrototype;

    private Dictionary<EquippedItem, ConstantParticleWeapon> _weapons = new Dictionary<EquippedItem, ConstantParticleWeapon>();
    
    public override void StartFiring(WeaponData data, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = WeaponPrototype.Instantiate<ConstantParticleWeapon>();
        p.Source = source;
        p.Target = target;
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        var t = p.transform;
        t.SetParent(barrel);
        t.forward = barrel.forward;
        t.position = barrel.position;
        p.Damage = item.Evaluate(data.Damage);
        p.DamageType = data.DamageType;
        _weapons.Add(item, p);
        p.Initialize();
    }

    public override void StopFiring(EquippedItem item)
    {
        _weapons[item].Stop();
        _weapons.Remove(item);
    }
}
