using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningGunManager : InstantWeaponEffectManager
{
    public Prototype Prototype;

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target, int shotId)
    {
        var p = Prototype.Instantiate<Lightning>();
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        p.ShotId = shotId;
        p.Barrel = barrel;
        p.Source = source;
        p.Range = weapon.Range;
        p.Target = target;
        p.Fire();
    }
}