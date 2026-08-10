using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

public class ProjectileManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;
    public bool InheritVelocity;

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        var p = ProjectilePrototype.Instantiate<Projectile>();
        var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
        var barrel = source.GetBarrel(hp);
        var angle = weapon.Spread / 2;
        p.SourceEntity = source.Entity;
        p.Velocity = Quaternion.Euler(
                         Random.Range(-angle, angle), 
                         Random.Range(-angle, angle), 
                         Random.Range(-angle, angle)) * 
                     barrel.forward * 
                     weapon.Velocity;
        p.StartPosition = p.transform.position = barrel.position + p.Velocity * (Random.value * Time.deltaTime);
        if(InheritVelocity)
            p.Velocity += new Vector3(source.Entity.Velocity.x, 0, source.Entity.Velocity.y);
        p.Damage = weapon.Damage;
        p.Range = weapon.Range;
        p.Penetration = weapon.Penetration;
        p.Spread = weapon.DamageSpread;
        p.DamageType = weapon.WeaponData.DamageType;
        p.Zone = source.Entity.Zone;
        p.AirburstDistance = target != null ? length(source.Entity.Position - target.Entity.Position) : (weapon.Range * .75f);
        p.Trail.Clear();
    }
}
