using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class MineManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = ProjectilePrototype.Instantiate<Mine>();
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        var angle = weapon.Spread / 2;
        p.Source = source;
        p.transform.position = barrel.position;
        p.GridObject.Velocity = Quaternion.Euler(
                                   Random.Range(-angle, angle),
                                   Random.Range(-angle, angle),
                               Random.Range(-angle, angle)) *
                               barrel.forward *
                               weapon.Velocity;
        p.Damage = weapon.Damage;
        p.Range = weapon.Range;
        p.DamageType = weapon.DamageType;
        p.GridObject.Zone = source.Entity.Zone;
    }
}
