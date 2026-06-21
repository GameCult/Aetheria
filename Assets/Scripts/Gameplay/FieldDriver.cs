using System;
using System.Collections.Generic;
using UnityEngine;

public class FieldDriver : MonoBehaviour
{
    public Camera Camera;
    public Vector2 Push;
    public float FrontTwist;
    public float RearTwist;
    public float TestMagnitude;
    public float FlowSpeed;
    public float FlowSpeedThrottleExponent;
    public float WaveThrottleExponent;
    
    public int MaxHits;
    public ExponentialCurve MagnitudeTimeScaling;
    
    public float MeleeRange;
    public float MeleeRangeExponent;
    public float MeleeFlatness;
    public float MeleeShaping;
    public float MeleeDuration;
    public float MeleeAngle;
    public float MeleeAngleExponent;
    
    public RectTransform.Axis RefractionAxis = RectTransform.Axis.Horizontal;
    
    private Material _field;
    private ComputeBuffer _hitBuffer;
    private List<FieldHit> _hits = new List<FieldHit>();
    private float _waveOffset;
    private float _meleeTime;
    private bool _meleeActive;

    private struct FieldHit
    {
        public Vector3 Position;
        public Vector3 Direction;
        public float Magnitude;
        public float Time;
    }
    
    void Start()
    {
        _field = GetComponent<MeshRenderer>().material;
        var clickableCollider = GetComponent<ClickableCollider>();
        if(clickableCollider!=null)
        {
            clickableCollider.OnClick += (_, _, ray, hit) =>
            {
                AddHit(hit.Point, ray.direction, TestMagnitude);
            };
        }
        _hitBuffer = new ComputeBuffer(MaxHits, 32);
    }

    public void AddHit(Vector3 position, Vector3 direction, float magnitude)
    {
        if (_hits.Count >= MaxHits) return;
        var hit = new FieldHit
        {
            Position = Vector3.Scale(transform.InverseTransformPoint(position).normalized, transform.localScale),
            Direction = (transform.rotation * direction).normalized,
            Magnitude = magnitude,
            Time = 0
        };
        //Debug.Log($"Received hit: Position={hit.Position}, Direction={hit.Direction}");
        _hits.Add(hit);
    }

    public int HitCount => _hits.Count;

    public void Melee()
    {
        _meleeActive = true;
        _meleeTime = 0;
    }

    void Update()
    {

        var pushMag = Mathf.Min(Push.magnitude, 1);

        _waveOffset = Mathf.Repeat(
            _waveOffset + Time.deltaTime * FlowSpeed *
            Mathf.Pow(Mathf.Max(pushMag, Mathf.Max(Mathf.Abs(FrontTwist), Mathf.Abs(RearTwist))), FlowSpeedThrottleExponent),
            1);

        var refractionRotation = Matrix4x4.Rotate(Camera.transform.rotation).inverse;
        if (RefractionAxis == RectTransform.Axis.Vertical) refractionRotation = 
            Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)) * refractionRotation;

        _field.SetFloat("_WaveOffset", _waveOffset * Mathf.PI * 2);
        _field.SetFloat("_Push", pushMag);
        _field.SetVector("_PushDirection", new Vector4(-Push.x, 0, -Push.y));
        _field.SetFloat("_TwistFront", FrontTwist);
        _field.SetFloat("_TwistRear", RearTwist);
        
        _field.SetVector("_InverseScale", new Vector4(1/transform.localScale.x,1/transform.localScale.y,1/transform.localScale.z));
        _field.SetMatrix("_ReflRotate", refractionRotation);

        for (int i = 0; i < _hits.Count; i++)
        {
            var hit = _hits[i];
            hit.Time += Time.deltaTime / MagnitudeTimeScaling.Evaluate(_hits[i].Magnitude);
            _hits[i] = hit;
            if (hit.Time > 1)
            {
                _hits.RemoveAt(i);
                i--;
            }
        }
        _hitBuffer.SetData(_hits);
        _field.SetBuffer("_Hits", _hitBuffer);
        _field.SetInt("_HitCount", _hits.Count);

        if (_meleeActive)
        {
            _meleeTime += Time.deltaTime / MeleeDuration;
            if (_meleeTime < 1)
            {
                _field.SetVector("_MeleeDirection", Quaternion.AngleAxis(Mathf.Sign(_meleeTime * 2 - 1) * MeleeAngle * Mathf.Pow(Mathf.Abs(_meleeTime * 2 - 1), MeleeAngleExponent), Vector3.up) * -Vector3.forward);
                _field.SetFloat("_MeleeDisplacement", Mathf.Pow(1 - 2 * Mathf.Abs(_meleeTime - .5f), MeleeRangeExponent) * MeleeRange);
                _field.SetFloat("_MeleeShape", MeleeShaping);
                _field.SetFloat("_MeleeFlattening", MeleeFlatness);
            }
            else
            {
                _meleeActive = false;
                _field.SetFloat("_MeleeDisplacement", 0);
            }
        }
    }
    
    private float almostIdentity( float x )
    {
        return x*x*(2.0f-x);
    }

    private float smooth(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }

    private float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3.0f - 2.0f * t);
    }

    private void OnDestroy()
    {
        _hitBuffer.Dispose();
    }
}
