using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
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
    
    public Zone Zone { get; set; }
    
    public Vector3 StartPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public float Damage { get; set; }
    public float Penetration { get; set; }
    public float Spread { get; set; }
    public DamageType DamageType { get; set; }
    public Entity SourceEntity { get; set; }
    public float Range { get; set; }

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
            Velocity *= max(0, 1 - Drag * Time.deltaTime);
            var forward = Velocity.normalized;
            t.forward = forward;
            var ray = new Ray(position, Velocity);
            foreach (var hit in Physics.RaycastAll(ray, Velocity.magnitude * Time.deltaTime, 1 | (1 << 17)))
            {
                var shield = hit.collider.GetComponent<ShieldManager>();
                var hull = hit.collider.GetComponent<HullCollider>();
                if (shield)
                {
                    if (!(shield.Entity.Shield != null && shield.Entity.Shield.Item.Active.Value && shield.Entity.Shield.CanTakeHit(DamageType, Damage))) continue;
                    if (shield.Entity != SourceEntity)
                    {
                        shield.Entity.Shield.TakeHit(DamageType, Damage*DirectHitDamageMultiplier);
                        shield.ShowHit(hit.point, sqrt(Damage * DirectHitDamageMultiplier));
                    }
                }
                else if (hull && !(hull.Entity.Shield != null && hull.Entity.Shield.Item.Active.Value && hull.Entity.Shield.CanTakeHit(DamageType, Damage)))
                {
                    if (hull.Entity != SourceEntity)
                    {
                        hull.SendHit(Damage*DirectHitDamageMultiplier, Penetration, Spread, DamageType, SourceEntity, hit.textureCoord, forward);
                        transform.position = hit.point;
                        StartCoroutine(Kill());
                    }
                }
                else
                {
                    StartCoroutine(Kill());
                    return;
                }
                
                if (HitEffect != null)
                {
                    var ht = HitEffect.Instantiate<Transform>();
                    ht.SetParent(hit.collider.transform);
                    ht.position = hit.point;
                    return;
                }
            }
            
            transform.position += Velocity * Time.deltaTime;
            var distanceTraveled = (transform.position - StartPosition).magnitude;
            if(distanceTraveled > Range)
                StartCoroutine(Kill());
            if (AirburstRange > 1 && distanceTraveled > AirburstDistance)
            {
                StartCoroutine(Kill());
                var ht = HitEffect.Instantiate<Transform>();
                ht.position = t.position;
                foreach (var collider in Physics.OverlapSphere(t.position, AirburstRange, 1))
                {
                    var hull = collider.GetComponent<HullCollider>();
                    if (hull)
                    {
                        hull.SendSplash(Damage, DamageType, SourceEntity, (collider.transform.position - t.position).normalized);
                    }
                }
            }
        }
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
