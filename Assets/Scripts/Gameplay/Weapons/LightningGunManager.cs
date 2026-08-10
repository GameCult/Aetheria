using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningGunManager : InstantWeaponEffectManager
{
    public Prototype Prototype;

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = Prototype.Instantiate<Lightning>();
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        p.Barrel = barrel;
        p.Source = source;
        p.Damage = weapon.Damage;
        p.Range = weapon.Range;
        p.Penetration = weapon.Penetration;
        p.Spread = weapon.DamageSpread;
        p.DamageType = weapon.WeaponData.DamageType;
        p.Target = target;
        p.Fire();
    }
}