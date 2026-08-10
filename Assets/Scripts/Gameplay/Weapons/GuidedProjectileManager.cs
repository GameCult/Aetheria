using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

public class GuidedProjectileManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    public Subject<(Entity source, Transform target, GuidedProjectile missile)> OnFireGuided = new Subject<(Entity source, Transform target, GuidedProjectile missile)>();

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target)
    {
        if(weapon.Data is LauncherData launcher)
        {
            if (target == null) return;
            var p = ProjectilePrototype.Instantiate<GuidedProjectile>();
            p.Source = source.transform;
            p.SourceEntity = source.Entity;
            p.Target = target.transform;
            p.Frequency = launcher.DodgeFrequency;
            var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
            var barrel = source.GetBarrel(hp);
            p.StartPosition = p.transform.position = barrel.position;
            p.Damage = weapon.Damage;
            p.Range = weapon.Range;
            p.Penetration = weapon.Penetration;
            p.Spread = weapon.DamageSpread;
            p.DamageType = weapon.WeaponData.DamageType;
            p.GuidanceCurve = launcher.GuidanceCurve.ToCurve();
            p.LiftCurve = launcher.LiftCurve.ToCurve();
            p.ThrustCurve = launcher.ThrustCurve.ToCurve();
            p.Velocity = barrel.forward * weapon.Velocity;
            p.Thrust = item.Evaluate(launcher.Thrust);
            p.TopSpeed = item.Evaluate(launcher.MissileVelocity);
            OnFireGuided.OnNext((source.Entity, target.transform, p));
        }
        else if(weapon.Data is GuidedWeaponData guidance)
        {
            var p = ProjectilePrototype.Instantiate<GuidedProjectile>();
            p.Source = source.transform;
            p.SourceEntity = source.Entity;
            p.Frequency = guidance.DodgeFrequency;
            var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
            var barrel = source.GetBarrel(hp);
            p.StartPosition = p.transform.position = barrel.position;
            p.Damage = weapon.Damage;
            p.Range = weapon.Range;
            p.Penetration = weapon.Penetration;
            p.Spread = weapon.DamageSpread;
            p.DamageType = weapon.WeaponData.DamageType;
            p.GuidanceCurve = guidance.GuidanceCurve.ToCurve();
            p.LiftCurve = guidance.LiftCurve.ToCurve();
            p.ThrustCurve = guidance.ThrustCurve.ToCurve();
            p.Velocity = barrel.forward * weapon.Velocity;
            p.Thrust = item.Evaluate(guidance.Thrust);
            p.TopSpeed = item.Evaluate(guidance.MissileVelocity);
            p.TargetPosition = () => source.Entity.Position + length( (float3)source.LookAtPoint.position - source.Entity.Position) * source.Entity.LookDirection;
        }
        else Debug.LogError($"Weapon {item.Data.Name} linked to {name} effect, but is not a Launcher!");
    }
}
