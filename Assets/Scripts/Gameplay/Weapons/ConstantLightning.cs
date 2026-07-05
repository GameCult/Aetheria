using UnityEngine;

public class ConstantLightning : MonoBehaviour
{
    public LightningCompute Lightning;
    public float HitRadius;
    public AnimationCurve FadeCurve;
    public float StartWidth = 1;
    public float EndWidth = 1;
    public float FadeDuration;
    
    public Transform Barrel { get; set; }
    public float ImpactIntensity { get; set; } = 1;
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
        if (AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls(
                Source?.ZoneRenderer,
                Source?.Entity,
                Barrel.position,
                Barrel.forward,
                Range,
                HitRadius,
                out var hits))
        {
            foreach (var hit in hits)
            {
                var hull = hit.Hull;
                var entity = hull.Entity;
                if (entity.Shield != null && entity.Shield.Item.Active.Value)
                {
                    hit.Shield?.ShowHit(hit.Point, Mathf.Max(0.01f, ImpactIntensity));
                }
                else
                {
                }

                Lightning.FixedEndpoint = true;
                Lightning.EndPosition = hit.Point;
            
                break;
            }
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
