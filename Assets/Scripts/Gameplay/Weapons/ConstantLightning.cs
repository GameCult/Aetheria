using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;

public class ConstantLightning : MonoBehaviour
{
    public LightningCompute Lightning;
    public float HitRadius;
    public AnimationCurve FadeCurve;
    public float StartWidth = 1;
    public float EndWidth = 1;
    public float FadeDuration;
    
    public Transform Barrel { get; set; }
    public float Damage { get; set; }
    public float Penetration { get; set; }
    public float Spread { get; set; }
    public DamageType DamageType { get; set; }
    public EntityInstance Source { get; set; }
    public float Range { get; set; }

    private bool _stopping;
    private float _startTime;

    private void OnEnable()
    {
        _stopping = false;
        Lightning.StartAnimation();
    }

    private void Update()
    {
        if (Barrel == null) return;
        if (_stopping)
        {
            var lerp = (Time.time - _startTime) / FadeDuration;
            Lightning.StartWidth = FadeCurve.Evaluate(lerp) * StartWidth;
            Lightning.EndWidth = FadeCurve.Evaluate(lerp) * EndWidth;
            
            if (lerp > 1)
            {
                GetComponent<Prototype>().ReturnToPool();
                return;
            }
        }
        else
        {
            Lightning.StartWidth = StartWidth;
            Lightning.EndWidth = EndWidth;
        }
        
        Lightning.FixedEndpoint = false;
        var hits = Physics.SphereCastAll(Barrel.position, HitRadius, Barrel.forward, Range, 1 | (1 << 17));
        foreach (var hit in hits)
        {
            var shield = hit.collider.GetComponent<ShieldManager>();
            if (shield && (shield.Entity.Shield != null && shield.Entity.Shield.Item.Active.Value && shield.Entity.Shield.CanTakeHit(DamageType, Damage)))
            {
                if (shield.Entity == Source.Entity) continue;
                shield.Entity.Shield.TakeHit(DamageType, Damage * Time.deltaTime);
                shield.ShowHit(hit.point, sqrt(Damage));
            }
            var hull = hit.collider.GetComponent<HullCollider>();
            if (hull && !(hull.Entity.Shield != null && hull.Entity.Shield.Item.Active.Value && hull.Entity.Shield.CanTakeHit(DamageType, Damage)))
            {
                if (hull.Entity == Source.Entity) continue;
                hull.SendHit(Damage * Time.deltaTime, Penetration, Spread, DamageType, Source.Entity, hit.textureCoord, Barrel.forward);
            }

            Lightning.FixedEndpoint = true;
            Lightning.EndPosition = hit.point;
            
            break;
        }
        if(!Lightning.FixedEndpoint)
            Lightning.EndPosition = Barrel.position + Barrel.forward * Range;

        Lightning.StartPosition = Barrel.position;
    }

    public void Stop()
    {
        _stopping = true;
        _startTime = Time.time;
    }
}
