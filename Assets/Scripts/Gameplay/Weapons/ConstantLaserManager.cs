using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstantLaserManager : ConstantWeaponEffectManager
{
    public Prototype LaserPrototype;

    private Dictionary<EquippedItem, ConstantLaser> _lasers = new Dictionary<EquippedItem, ConstantLaser>();
    
    public override void StartFiring(WeaponData data, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = LaserPrototype.Instantiate<ConstantLaser>();
        p.SourceEntity = source.Entity;
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        p.Range = item.Evaluate(data.Range);
        var t = p.transform;
        t.SetParent(barrel);
        t.forward = barrel.forward;
        t.position = barrel.position;
        // Cut 4 (docs/fire-control-cut.md): FireControl already rolls this weapon's damage; the presentation
        // only needs to know where to draw the beam.
        p.TargetTransform = target != null ? target.transform : null;
        _lasers.Add(item, p);
    }

    public override void StopFiring(EquippedItem item)
    {
        _lasers[item].Stop();
        _lasers.Remove(item);
    }
}
