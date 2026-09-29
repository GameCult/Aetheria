using UnityEngine;

public class HitscanManager : InstantWeaponEffectManager
{
    public Prototype Prototype;
    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target, int shotId)
    {
        var p = Prototype.Instantiate<HitscanEffect>();
        p.SourceEntity = source.Entity;
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        var t = p.transform;
        t.SetParent(barrel, false);
        t.localRotation = Quaternion.identity;
        t.localPosition = Vector3.zero;
        // t.position = barrel.position;
        // t.forward = barrel.forward;
        p.ShotId = shotId;
        p.Range = weapon.Range;
        p.TargetTransform = target != null ? target.transform : null;
        p.Fire();
    }
}