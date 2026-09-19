using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cut 4 (docs/fire-control-cut.md): the raycast, shield branch and SendHit are deleted -- FireControl already
// rolls this weapon's damage once per GameplaySettings.BeamResolveInterval (ConstantWeapon.Execute) before
// this beam is ever drawn (R8). What is left is pure endpoint presentation, the same convention Laser.cs
// (Cut 3) uses for a discrete shot: draw to the target's current position, or out to Range with no target.
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

    public Entity SourceEntity { get; set; }
    public float Range { get; set; }
    public Transform TargetTransform { get; set; }

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
        LineRenderer.SetPosition(1, TargetTransform != null
            ? TargetTransform.position
            : transform.position + transform.forward * Range);
    }

    public void Stop()
    {
        _stopping = true;
        _stopIntensity = _intensity;
        _startTime = Time.time;
    }
}
