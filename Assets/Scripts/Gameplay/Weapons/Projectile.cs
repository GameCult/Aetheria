using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Projectile : MonoBehaviour
{
    public TrailRenderer Trail;
    public float Gravity;
    public float Drag = .1f;
    public Prototype HitEffect;
    
    public float AirburstDistance;
    public float AirburstRange;
    public float DirectHitDamageMultiplier = 1;
    
    private bool _alive;
    
    public Vector3 StartPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public float Damage { get; set; }
    public float Penetration { get; set; }
    public float Spread { get; set; }
    public DamageType DamageType { get; set; }
    public Entity SourceEntity { get; set; }
    public EntityInstance TargetInstance { get; set; }
    public float Range { get; set; }
    public float YmirWorldTime { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void OnEnable()
    {
        _alive = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (SourceEntity == null) return;
        
        if(_alive)
        {
            var t = transform;
            var position = t.position;
            Velocity -= Vector3.up * (Gravity * Time.deltaTime);
            Velocity *= Mathf.Max(0, 1 - Drag * Time.deltaTime);
            var forward = Velocity.normalized;
            t.forward = forward;
            if (!AetheriaYmirPhysicsBridge.Instance.TryStepProjectile(this, Time.deltaTime, out var ymirStep))
            {
                Debug.LogWarning("Ymir projectile step unavailable; projectile killed instead of falling back to Unity physics.");
                StartCoroutine(Kill());
                return;
            }

            YmirWorldTime += Time.deltaTime;
            Velocity = ymirStep.Velocity;
            t.position = ymirStep.Position;
            if (ymirStep.HasHit)
            {
                ApplyYmirHit(ymirStep, forward);
                return;
            }

            var distanceTraveled = (transform.position - StartPosition).magnitude;
            if(distanceTraveled > Range)
                StartCoroutine(Kill());
            if (AirburstRange > 1 && distanceTraveled > AirburstDistance)
            {
                StartCoroutine(Kill());
                if (HitEffect != null)
                {
                    var ht = HitEffect.Instantiate<Transform>();
                    ht.position = t.position;
                }

                if (AetheriaYmirPhysicsBridge.Instance.TryOverlapTargetHulls(TargetInstance, t.position, AirburstRange, out var hits))
                {
                    foreach (var hit in hits)
                    {
                        var direction = (hit.Hull.transform.position - t.position).normalized;
                    }
                }
            }
        }
    }

    private void ApplyYmirHit(AetheriaYmirProjectileStep step, Vector3 forward)
    {
        var hull = step.Hit.Hull;
        if (hull == null || hull.Entity == SourceEntity)
            return;

        if (hull.Entity.Shield != null &&
            hull.Entity.Shield.Item.Active.Value)
        {
        }
        else
        {
        }

        transform.position = step.Hit.Point;
        if (HitEffect != null)
        {
            var ht = HitEffect.Instantiate<Transform>();
            ht.SetParent(hull.transform);
            ht.position = step.Hit.Point;
        }

        StartCoroutine(Kill());
    }

    IEnumerator Kill()
    {
        _alive = false;
        var startTime = Time.time;
        var lifetime = Trail.time;
        while (Time.time - startTime < lifetime)
            yield return null;
        GetComponent<Prototype>().ReturnToPool();
    }
}
