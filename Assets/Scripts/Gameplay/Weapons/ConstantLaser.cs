using UnityEngine;

public class ConstantLaser : MonoBehaviour
{
    public AnimationCurve StartCurve;
    public AnimationCurve EndCurve;
    public AnimationCurve IntensityCurve;
    public float WidthMultiplier = 1;
    public float StartDuration;
    public float FadeDuration;
    public float CycleDuration;
    public LineRenderer LineRenderer;
    
    public float ImpactIntensity { get; set; } = 1;
    public Entity SourceEntity { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }
    public float Range { get; set; }

    private float _intensity;
    private float _stopIntensity;
    private bool _starting;
    private bool _stopping;
    private float _startTime;
    private float _cycleStartTime;
    private readonly Vector3[] _zeros = {Vector3.zero, Vector3.zero};

    private void OnEnable()
    {
        _stopping = false;
        _starting = true;
        _startTime = Time.time;
        LineRenderer.SetPositions(_zeros);
    }

    private void Update()
    {
        if (_stopping)
        {
            var lerp = (Time.time - _startTime) / FadeDuration;
            LineRenderer.widthMultiplier = EndCurve.Evaluate(lerp) * _stopIntensity * WidthMultiplier;
            
            if (lerp > 1)
            {
                GetComponent<Prototype>().ReturnToPool();
                return;
            }
        }
        else
        {
            var lerp = (Time.time - _startTime) / StartDuration;
            if (lerp > 1)
            {
                if (_starting)
                {
                    _starting = false;
                    _cycleStartTime = Time.time;
                }
                _intensity = IntensityCurve.Evaluate((Time.time - _cycleStartTime) / CycleDuration % CycleDuration);
                LineRenderer.widthMultiplier = _intensity * WidthMultiplier;
            }
            else
            {
                LineRenderer.widthMultiplier = StartCurve.Evaluate(lerp) * WidthMultiplier;
            }
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
                    hit.Shield?.ShowHit(hit.Point, Mathf.Max(0.01f, ImpactIntensity));
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
    }

    public void Stop()
    {
        _stopping = true;
        _stopIntensity = _intensity;
        _startTime = Time.time;
    }
}
