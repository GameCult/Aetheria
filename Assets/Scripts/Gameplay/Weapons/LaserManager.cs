using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = ProjectilePrototype.Instantiate<Laser>();
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        var t = p.transform;
        t.SetParent(barrel);
        t.forward = barrel.forward;
        t.position = barrel.position;
        p.SourceEntity = source.Entity;
        p.Damage = weapon.Damage;
        p.Range = weapon.Range;
        p.Penetration = weapon.Penetration;
        p.Spread = weapon.DamageSpread;
        p.DamageType = weapon.WeaponData.DamageType;
    }
}
