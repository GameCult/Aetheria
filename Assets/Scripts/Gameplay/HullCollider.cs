using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;
using UnityEngine;
using float2 = CultMath.float2;

// Cut 3 (docs/fire-control-cut.md) FORK, reported rather than worked around silently: the map's Cut 3 deletion
// list calls for Hit, Splash, SendHit and SendSplash to go away here outright. They cannot, yet, without
// breaking the build -- ConstantLaser.cs, ConstantLightning.cs, ConstantParticleWeapon.cs, HitscanEffect.cs
// and Mine.cs (Cut 4's "unused kinds," R9) still call hull.SendHit/SendSplash from their own live Physics
// queries, and Cut 4 is the cut that migrates them. Deleting these methods now is a compile break in
// out-of-scope files, not a behaviour change in this cut's own scope.
//
// What Cut 3 actually owns and has delivered: nothing in Assets/Scripts/Gameplay/Weapons/{Projectile,
// GuidedProjectile,Laser,Lightning}.cs calls SendHit/SendSplash any more (see those files), and
// EntityInstance.cs no longer subscribes to Hit/Splash at all -- so for the four weapon kinds this cut owns,
// these events have no subscriber and decide nothing. They fire into the void until Cut 4 deletes them
// together with their last remaining callers. OnCollisionEnter stays regardless: ship-on-ship collision
// impulse is a deferred Unity-physics surface, not part of fire control.
public class HullCollider : MonoBehaviour
{
    public Subject<HullHitEventArgs> Hit = new Subject<HullHitEventArgs>();
    public Subject<HullSplashEventArgs> Splash = new Subject<HullSplashEventArgs>();

    public Entity Entity { get; set; }

    public void SendHit(float damage, float penetration, float spread, DamageType damageType, Entity source, Vector2 texCoord, Vector3 direction)
    {
        Hit.OnNext(new HullHitEventArgs
        {
            Damage = damage,
            Penetration = penetration,
            Spread = spread,
            DamageType = damageType,
            Source = source,
            TexCoord = texCoord.ToCultMath(),
            Direction = direction.ToCultMath()
        });
    }

    public void SendSplash(float damage, DamageType damageType, Entity source, Vector3 direction)
    {
        Splash.OnNext(new HullSplashEventArgs
        {
            Damage = damage,
            DamageType = damageType,
            Source = source,
            Direction = direction.ToCultMath()
        });
    }

    private void OnCollisionEnter(Collision other)
    {
        Entity.Velocity += other.impulse.ToCultMath().xz / Time.deltaTime;
    }

    public class HullHitEventArgs
    {
        public float Damage;
        public float Penetration;
        public float Spread;
        public DamageType DamageType;
        public Entity Source;
        public float2 TexCoord;
        public float3 Direction;
    }

    public class HullSplashEventArgs
    {
        public float Damage;
        public DamageType DamageType;
        public Entity Source;
        public float3 Direction;
    }
}
