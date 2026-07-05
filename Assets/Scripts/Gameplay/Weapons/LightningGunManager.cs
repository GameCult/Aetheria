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
        p.Range = weapon.Range;
        p.ImpactIntensity = 1;
        p.Target = target;
        p.Fire();
    }
}
