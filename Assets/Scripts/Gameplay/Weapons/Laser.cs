using System;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    public AnimationCurve IntensityCurve;
    public float Duration;
    public LineRenderer LineRenderer;
    
    public float Damage { get; set; }
    public float Penetration { get; set; }
    public float Spread { get; set; }
    public DamageType DamageType { get; set; }
    public Entity SourceEntity { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }
    public float Range { get; set; }

    private float _startTime;
    private readonly Vector3[] _zeros = {Vector3.zero, Vector3.zero};

    private void OnEnable()
    {
        _startTime = Time.time;
        LineRenderer.SetPositions(_zeros);
    }

    private void Update()
    {
        var lerp = (Time.time - _startTime) / Duration;
        if (lerp > 1)
        {
            GetComponent<Prototype>().ReturnToPool();
            return;
        }
        
        LineRenderer.SetPosition(0, transform.position);
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
                    hit.Shield?.ShowHit(hit.Point, Mathf.Sqrt(Damage));
                    LineRenderer.SetPosition(1, hit.Point);
                    hitFound = true;
                    break;
                }

                LineRenderer.SetPosition(1, hit.Point);
                hitFound = true;
                break;
            }
        }

        if(!hitFound)
            LineRenderer.SetPosition(1, transform.position + transform.forward * Range);

        LineRenderer.widthMultiplier = IntensityCurve.Evaluate(lerp);
    }
}
