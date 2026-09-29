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
        p.Range = item.Evaluate(data.Range);
        // Cut 4 (docs/fire-control-cut.md): FireControl already rolls this weapon's damage; the presentation
        // only needs to know where to draw the bolt.
        p.TargetTransform = target != null ? target.transform : null;
        _bolts.Add(item, p);
    }

    public override void StopFiring(EquippedItem item)
    {
        _bolts[item].Stop();
        _bolts.Remove(item);
    }
}
