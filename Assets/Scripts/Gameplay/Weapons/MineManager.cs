using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

public class MineManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;
    public bool InheritVelocity;

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
        if(InheritVelocity)
            p.GridObject.Velocity += new Vector3(source.Entity.Velocity.x, 0, source.Entity.Velocity.y);
        p.Damage = weapon.Damage;
        p.Range = weapon.Range;
        p.DamageType = weapon.WeaponData.DamageType;
        p.GridObject.Zone = source.Entity.Zone;
    }
}
