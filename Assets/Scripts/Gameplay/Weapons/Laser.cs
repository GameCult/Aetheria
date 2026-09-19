using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CultMath.math;

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
        bool hitFound = false;
        foreach (var hit in Physics.RaycastAll(new Ray(transform.position, transform.forward), Range, 1 | (1 << 17)))
        {
            var shield = hit.collider.GetComponent<ShieldManager>();
            if (shield)
            {
                var shieldBehavior = shield.Entity.Shield;
                var shieldActive = shieldBehavior != null && shieldBehavior.Item.Active.Value;
                var shieldAbsorbs = shieldActive && shieldBehavior.CanTakeHit(DamageType, Damage);
                // F5 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): CanTakeHit is a pure query now -- read
                // once into shieldAbsorbs, and break the shield exactly once, right here, at the point this hit
                // is actually decided to route past it (to the hull collider RaycastAll returns further down
                // this same loop) instead of being absorbed. The hull branch below re-queries CanTakeHit safely
                // -- it is side-effect-free -- and will see Broken already true.
                if (shieldActive && !shieldAbsorbs) shieldBehavior.Break();
                if (!shieldAbsorbs) continue;
                if (shield.Entity != SourceEntity)
                {
                    shieldBehavior.TakeHit(DamageType, Damage);
                    shield.ShowHit(hit.point, sqrt(Damage));
                    LineRenderer.SetPosition(1, hit.point);
                    hitFound = true;
                    break;
                }
            }
            var hull = hit.collider.GetComponent<HullCollider>();
            if (hull && !(hull.Entity.Shield != null && hull.Entity.Shield.Item.Active.Value && hull.Entity.Shield.CanTakeHit(DamageType, Damage)))
            {
                if (hull.Entity != SourceEntity)
                {
                    hull.SendHit(Damage * (Time.deltaTime / Duration), Penetration, Spread, DamageType, SourceEntity, hit.textureCoord, transform.forward);
                    LineRenderer.SetPosition(1, hit.point);
                    hitFound = true;
                    break;
                }
            }
        }
        if(!hitFound)
            LineRenderer.SetPosition(1, transform.position + transform.forward * Range);

        LineRenderer.widthMultiplier = IntensityCurve.Evaluate(lerp);
    }
}
