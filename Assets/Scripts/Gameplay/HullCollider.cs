using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using float2 = Unity.Mathematics.float2;

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
            TexCoord = texCoord,
            Direction = direction
        });
    }
    
    public void SendSplash(float damage, DamageType damageType, Entity source, Vector3 direction)
    {
        Splash.OnNext(new HullSplashEventArgs
        {
            Damage = damage,
            DamageType = damageType,
            Source = source,
            Direction = direction
        });
    }

    private void OnCollisionEnter(Collision other)
    {
        Entity.Velocity += ((float3) other.impulse).xz / Time.deltaTime;
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