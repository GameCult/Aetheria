using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;

public class HitscanEffect : MonoBehaviour
{
    public float Duration;
    public AnimationCurve IntensityCurve;
    public LineRenderer Line;
    public ParticleSystem LineEffect;
    public Prototype HitEffect;
    
    public Zone Zone { get; set; }
    
    public float Damage { get; set; }
    public float Penetration { get; set; }
    public float Spread { get; set; }
    public DamageType DamageType { get; set; }
    public Entity SourceEntity { get; set; }
    public float Range { get; set; }
    
    private float _startTime;
    private bool _active = false;
    
    public void Fire()
    {
        _startTime = Time.time;
        var hitFound = false;
        var ray = new Ray(transform.position, transform.forward);
        foreach (var hit in Physics.RaycastAll(ray, Range, 1 | (1 << 17)))
        {
            var shield = hit.collider.GetComponent<ShieldManager>();
            if (shield)
            {
                if (!(shield.Entity.Shield != null && shield.Entity.Shield.Item.Active.Value && shield.Entity.Shield.CanTakeHit(DamageType, Damage))) continue;
                if (shield.Entity != SourceEntity)
                {
                    shield.Entity.Shield.TakeHit(DamageType, Damage);
                    shield.ShowHit(hit.point, sqrt(Damage));
                    hitFound = true;
                }
            }
            var hull = hit.collider.GetComponent<HullCollider>();
            if (hull)
            {
                if (hull.Entity != SourceEntity)
                {
                    hull.SendHit(Damage, Penetration, Spread, DamageType, SourceEntity, hit.textureCoord, transform.forward);
                    hitFound = true;
                }
            }
                
            if (hitFound && HitEffect != null)
            {
                var ht = HitEffect.Instantiate<Transform>();
                ht.SetParent(hit.collider.transform);
                ht.position = hit.point;
            }
            
            var length = (hit.point - transform.position).magnitude;
            Line.SetPosition(1, Vector3.forward * length);
            var emission = LineEffect.emission;
            emission.rateOverTimeMultiplier = length;
            var shape = LineEffect.shape;
            shape.position = Vector3.forward * (length / 2);
            shape.scale = Vector3.one * (length / 2);
        }
        if(!hitFound)
        {
            Line.SetPosition(1, Vector3.forward * Range);
            var emission = LineEffect.emission;
            emission.rateOverTimeMultiplier = Range;
            var shape = LineEffect.shape;
            shape.position = Vector3.forward * (Range / 2);
            shape.scale = Vector3.one * (Range / 2);
        }
        LineEffect.Play(true);
        _active = true;
    }

    void Update()
    {
        if (!_active) return;
        var lerp = (Time.time - _startTime) / Duration;
        if (lerp > 1)
        {
            GetComponent<Prototype>().ReturnToPool();
            return;
        }
        
        Line.widthMultiplier = IntensityCurve.Evaluate(lerp);
    }
}
