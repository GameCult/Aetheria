using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using cfloat3 = CultMath.float3;

public class GuidedProjectileManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    public Subject<(Entity source, Transform target, GuidedProjectile missile)> OnFireGuided = new Subject<(Entity source, Transform target, GuidedProjectile missile)>();

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        if(!weapon.HasGuidedProjectileProfile)
        {
            Debug.LogError($"Weapon {item.RuntimeItem?.Name ?? "Unknown item"} linked to {name} effect, but has no guided projectile profile!");
            return;
        }

        if(weapon.GuidedProjectileTargeting == GuidedProjectileTargetMode.TargetEntity)
        {
            if (target == null) return;
            var p = ProjectilePrototype.Instantiate<GuidedProjectile>();
            p.Source = source.transform;
            p.SourceEntity = source.Entity;
            p.Target = target.transform;
            p.Frequency = weapon.GuidedProjectileDodgeFrequency;
            var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
            var barrel = source.GetBarrel(hp);
            p.StartPosition = p.transform.position = barrel.position;
            p.Damage = weapon.Damage;
            p.Range = weapon.Range;
            p.Penetration = weapon.Penetration;
            p.Spread = weapon.DamageSpread;
            p.DamageType = weapon.DamageType;
            p.GuidanceCurve = weapon.GuidedProjectileGuidanceCurve.ToCurve();
            p.LiftCurve = weapon.GuidedProjectileLiftCurve.ToCurve();
            p.ThrustCurve = weapon.GuidedProjectileThrustCurve.ToCurve();
            p.Velocity = barrel.forward * weapon.Velocity;
            p.Thrust = weapon.EvaluateGuidedProjectileThrust();
            p.TopSpeed = weapon.EvaluateGuidedProjectileVelocity();
            OnFireGuided.OnNext((source.Entity, target.transform, p));
        }
        else if(weapon.GuidedProjectileTargeting == GuidedProjectileTargetMode.LookDirection)
        {
            var p = ProjectilePrototype.Instantiate<GuidedProjectile>();
            p.Source = source.transform;
            p.SourceEntity = source.Entity;
            p.Frequency = weapon.GuidedProjectileDodgeFrequency;
            var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
            var barrel = source.GetBarrel(hp);
            p.StartPosition = p.transform.position = barrel.position;
            p.Damage = weapon.Damage;
            p.Range = weapon.Range;
            p.Penetration = weapon.Penetration;
            p.Spread = weapon.DamageSpread;
            p.DamageType = weapon.DamageType;
            p.GuidanceCurve = weapon.GuidedProjectileGuidanceCurve.ToCurve();
            p.LiftCurve = weapon.GuidedProjectileLiftCurve.ToCurve();
            p.ThrustCurve = weapon.GuidedProjectileThrustCurve.ToCurve();
            p.Velocity = barrel.forward * weapon.Velocity;
            p.Thrust = weapon.EvaluateGuidedProjectileThrust();
            p.TopSpeed = weapon.EvaluateGuidedProjectileVelocity();
            p.TargetPosition = () =>
            {
                var lookPosition = source.LookAtPoint.position;
                var lookPoint = new cfloat3(lookPosition.x, lookPosition.y, lookPosition.z);
                var lookDistance = CultMath.math.length(lookPoint - source.Entity.CultPosition);
                return (Vector3)AetheriaMath.ToUnity(source.Entity.CultPosition + lookDistance * source.Entity.CultLookDirection);
            };
        }
    }
}
