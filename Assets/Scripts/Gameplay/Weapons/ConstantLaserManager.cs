using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstantLaserManager : ConstantWeaponEffectManager
{
    public Prototype LaserPrototype;

    private Dictionary<EquippedItem, ConstantLaser> _lasers = new Dictionary<EquippedItem, ConstantLaser>();
    
    public override void StartFiring(ConstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = LaserPrototype.Instantiate<ConstantLaser>();
        p.SourceEntity = source.Entity;
        p.ZoneRenderer = source.ZoneRenderer;
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        p.Range = weapon.Range;
        var t = p.transform;
        t.SetParent(barrel);
        t.forward = barrel.forward;
        t.position = barrel.position;
        p.Damage = weapon.Damage;
        p.Penetration = weapon.Penetration;
        p.Spread = weapon.DamageSpread;
        p.DamageType = weapon.DamageType;
        _lasers.Add(item, p);
    }

    public override void StopFiring(EquippedItem item)
    {
        _lasers[item].Stop();
        _lasers.Remove(item);
    }
}
