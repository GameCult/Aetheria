using UnityEngine;

public class HitscanEffect : MonoBehaviour
{
    public float Duration;
    public AnimationCurve IntensityCurve;
    public LineRenderer Line;
    public ParticleSystem LineEffect;
    public Prototype HitEffect;
    
    public float ImpactIntensity { get; set; } = 1;
    public Entity SourceEntity { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }
    public float Range { get; set; }
    
    private float _startTime;
    private bool _active = false;
    
    public void Fire()
    {
        _startTime = Time.time;
        var hitFound = false;
        if (AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls(
                ZoneRenderer,
                SourceEntity,
                transform.position,
                transform.forward,
                Range,
                0,
                out var hits))
        {
            foreach (var hit in hits)
            {
                var hull = hit.Hull;
                var entity = hull.Entity;
                if (entity.Shield != null && entity.Shield.Item.Active.Value)
                {
                    hit.Shield?.ShowHit(hit.Point, Mathf.Max(0.01f, ImpactIntensity));
                    hitFound = true;
                }
                else
                {
                    hitFound = true;
                }
                
                if (hitFound && HitEffect != null)
                {
                    var ht = HitEffect.Instantiate<Transform>();
                    ht.SetParent(hull.transform);
                    ht.position = hit.Point;
                }

                var length = (hit.Point - transform.position).magnitude;
                Line.SetPosition(1, Vector3.forward * length);
                var emission = LineEffect.emission;
                emission.rateOverTimeMultiplier = length;
                var shape = LineEffect.shape;
                shape.position = Vector3.forward * (length / 2);
                shape.scale = Vector3.one * (length / 2);
                break;
            }
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
