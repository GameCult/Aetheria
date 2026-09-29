using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using CultMath;
using CultMath.UnityBridge;
using UnityEngine;
using static CultMath.math;

public class GuidedProjectileManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    public Subject<(Entity source, Transform target, GuidedProjectile missile)> OnFireGuided = new Subject<(Entity source, Transform target, GuidedProjectile missile)>();

    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target, int shotId)
    {
        if(weapon.Data is LauncherData launcher)
        {
            // A round the simulation fired at no target (FireControl.Fire: a fused round with no valid targeting
            // data, or out of arc) flies to the point it bursts at; one with no target and no burst is not a shot.
            PendingShot shot = default;
            if (target == null && !(source.Entity.Zone.TryGetShot(shotId, out shot) && shot.Fuse != null)) return;
            var burst = shot.BurstPosition.ToUnity();
            var p = ProjectilePrototype.Instantiate<GuidedProjectile>();
            p.ShotId = shotId;
            p.Source = source.transform;
            p.SourceEntity = source.Entity;
            if (target != null) p.Target = target.transform;
            else p.TargetPosition = () => burst;
            p.Frequency = launcher.DodgeFrequency;
            var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
            var barrel = source.GetBarrel(hp);
            p.StartPosition = (p.transform.position = barrel.position).ToCultMath();
            p.Range = weapon.Range;
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
            p.ShotId = shotId;
            p.Source = source.transform;
            p.SourceEntity = source.Entity;
            p.Frequency = guidance.DodgeFrequency;
            var hp = source.Entity.Hardpoints[item.Position.x, item.Position.y];
            var barrel = source.GetBarrel(hp);
            p.StartPosition = (p.transform.position = barrel.position).ToCultMath();
            p.Range = weapon.Range;
            p.GuidanceCurve = guidance.GuidanceCurve.ToCurve();
            p.LiftCurve = guidance.LiftCurve.ToCurve();
            p.ThrustCurve = guidance.ThrustCurve.ToCurve();
            p.Velocity = barrel.forward * weapon.Velocity;
            p.Thrust = item.Evaluate(guidance.Thrust);
            p.TopSpeed = item.Evaluate(guidance.MissileVelocity);
            p.TargetPosition = () => (source.Entity.Position + length(source.LookAtPoint.position.ToCultMath() - source.Entity.Position) * source.Entity.LookDirection).ToUnity();
        }
        else Debug.LogError($"Weapon {item.Data.Name} linked to {name} effect, but is not a Launcher!");
    }
}
