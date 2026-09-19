using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CultMath;
using static CultMath.math;
using Random = UnityEngine.Random;
using static CultMath.math;

public class Lightning : MonoBehaviour
{
    public LightningCompute LightningCompute;
    public float HitRadius;
    
    public float Damage { get; set; }
    public float Penetration { get; set; }
    public float Spread { get; set; }
    public DamageType DamageType { get; set; }
    public EntityInstance Source { get; set; }
    public float Range { get; set; }
    public EntityInstance Target { get; set; }
    public Transform Barrel { get; set; }

    private bool _colliderHit;
    private Vector3 _colliderLocalPosition;
    private Transform _colliderTransform;
    private Vector3 _endpoint;

    public void Fire()
    {
        _colliderHit = false;
        LightningCompute.OnLeaderComplete = null;
        LightningCompute.FixedEndpoint = false;
        var hits = Physics.SphereCastAll(Barrel.position, HitRadius, Barrel.forward, Range, 1 | (1 << 17));
        foreach (var hit in hits)
        {
            var shield = hit.collider.GetComponent<ShieldManager>();
            if (shield)
            {
                var shieldBehavior = shield.Entity.Shield;
                var shieldActive = shieldBehavior != null && shieldBehavior.Item.Active.Value;
                var shieldAbsorbs = shieldActive && shieldBehavior.CanTakeHit(DamageType, Damage);
                // F5 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): CanTakeHit is a pure query now -- read
                // once into shieldAbsorbs, and break the shield exactly once, right here, at the point this hit
                // is actually decided to route past it (to the hull collider SphereCastAll returns as a
                // separate entry further down this same loop) instead of being absorbed. The hull branch below
                // re-queries CanTakeHit safely -- it is side-effect-free -- and will see Broken already true.
                if (shieldActive && !shieldAbsorbs) shieldBehavior.Break();
                if (shieldAbsorbs)
                {
                    if (shield.Entity == Source.Entity) continue;
                    LightningCompute.OnLeaderComplete = () =>
                    {
                        shieldBehavior.TakeHit(DamageType, Damage);
                        shield.ShowHit(hit.point, sqrt(Damage));
                    };
                }
            }
            var hull = hit.collider.GetComponent<HullCollider>();
            if (hull && !(hull.Entity.Shield != null && hull.Entity.Shield.Item.Active.Value && hull.Entity.Shield.CanTakeHit(DamageType, Damage)))
            {
                if (hull.Entity == Source.Entity) continue;
                LightningCompute.OnLeaderComplete = () => 
                    hull.SendHit(Damage, Penetration, Spread, DamageType, Source.Entity, hit.textureCoord, Barrel.forward);
            }

            _colliderHit = true;
            _colliderLocalPosition = hit.collider.transform.InverseTransformPoint(hit.point);
            _colliderTransform = hit.collider.transform;
            LightningCompute.FixedEndpoint = true;
            break;
        }
        LightningCompute.OnPulseComplete = () => 
            GetComponent<Prototype>().ReturnToPool();
        LightningCompute.StartAnimation();
        _endpoint = Barrel.position + Barrel.forward * Range;
    }

    private void Update()
    {
        if (Barrel == null) return;

        if (_colliderHit)
        {
            if (_colliderTransform) _endpoint = _colliderTransform.TransformPoint(_colliderLocalPosition);
        }

        LightningCompute.EndPosition = _endpoint;
        LightningCompute.StartPosition = Barrel.position;
    }
}
