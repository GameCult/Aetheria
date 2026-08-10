using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class FieldDriver : MonoBehaviour
{
    public Camera Camera;
    public float2 Push;
    public float FrontTwist;
    public float RearTwist;
    [Inspectable]
    public float TestMagnitude;
    [Inspectable]
    public float FlowSpeed;
    [Inspectable]
    public float FlowSpeedThrottleExponent;
    [Inspectable]
    public float WaveThrottleExponent;
    
    public int MaxHits;
    [Inspectable]
    public ExponentialCurve MagnitudeTimeScaling;
    
    [Inspectable, InspectorHeader("Melee")]
    public float MeleeRange;
    [Inspectable]
    public float MeleeRangeExponent;
    [Inspectable]
    public float MeleeFlatness;
    [Inspectable]
    public float MeleeShaping;
    [Inspectable]
    public float MeleeDuration;
    [Inspectable]
    public float MeleeAngle;
    [Inspectable]
    public float MeleeAngleExponent;
    
    [Inspectable, InspectorHeader("Tendril")]
    public float TendrilBaseRadius = 3;
    [Inspectable]
    public float TendrilTipRadius = .1f;
    [Inspectable]
    public float TendrilExtensionExponent = .75f;
    [Inspectable]
    public float TendrilExtendBaseAnimationExponent = .5f;
    [Inspectable]
    public float TendrilBaseDamping = 2;
    [Inspectable]
    public float TendrilTipRadiusAnimationExponent = 2f;
    [Inspectable]
    public float TendrilFadeExponent = 2;
    // [Inspectable]
    // public float GrabScaleAnimationExponent = .5f;

    [Inspectable, InspectorHeader("Grab")]
    public float GrabExtendTime;
    [Inspectable]
    public float GrabEnvelopTime;
    [Inspectable]
    public float GrabPullTime;
    
    [Inspectable]
    public RectTransform.Axis RefractionAxis = RectTransform.Axis.Horizontal;
    
    private Material _field;
    private ComputeBuffer _hitBuffer;
    private List<FieldHit> _hits = new List<FieldHit>();
    private float _waveOffset;
    private float _meleeTime;
    private bool _meleeActive;
    private MeshCollider _collider;
    private Transform _grabObject;
    private Vector3 _grabObjectStartPos;
    private Vector3 _grabObjectStartTendrilBasePos;
    private Vector3 _grabObjectEndPos;
    private Vector3 _grabObjectVelocity;
    private float _grabObjectScale;
    private float _grabTime;
    private GrabPhase _grabPhase;
    private Vector3 _tendrilBasePos;
    private Vector3 _tendrilBendTarget;
    private Vector3 _tendrilTargetPos;
    private float _fadePoint;

    private enum GrabPhase
    {
        Extend,
        Envelop,
        Pull
    }

    private struct FieldHit
    {
        public float3 Position;
        public float3 Direction;
        public float Magnitude;
        public float Time;
    }
    
    void Start()
    {
        _collider = GetComponent<MeshCollider>();
        _field = GetComponent<MeshRenderer>().material;
        var clickableCollider = GetComponent<ClickableCollider>();
        if(clickableCollider!=null)
        {
            clickableCollider.OnClick += (_, _, ray, hit) =>
            {
                AddHit(hit.point, ray.direction, TestMagnitude);
            };
        }
        _hitBuffer = new ComputeBuffer(MaxHits, 32);
    }

    public void AddHit(float3 position, float3 direction, float magnitude)
    {
        if (_hits.Count >= MaxHits) return;
        var hit = new FieldHit
        {
            Position = Vector3.Scale(normalize(transform.InverseTransformPoint(position)), transform.localScale),
            Direction = normalize(transform.rotation * direction),
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

    public bool CanGrab => _grabObject == null;

    public void GrabObject(Transform t, Vector3 v)
    {
        _grabPhase = GrabPhase.Extend;
        _grabObject = t;
        _grabObjectStartPos = t.position;
        _grabObjectVelocity = v;
        _grabObjectScale = t.localScale.x;
        _tendrilBasePos = _grabObjectStartTendrilBasePos = _collider.ClosestPoint(_grabObjectStartPos);
        _fadePoint = .999f;
    }

    void Update()
    {

        var pushMag = min(length(Push), 1);

        _waveOffset = frac(_waveOffset + Time.deltaTime * FlowSpeed * pow(max(pushMag, max(abs(FrontTwist), abs(RearTwist))), FlowSpeedThrottleExponent));

        var refractionRotation = Matrix4x4.Rotate(Camera.transform.rotation).inverse;
        if (RefractionAxis == RectTransform.Axis.Vertical) refractionRotation = 
            Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)) * refractionRotation;

        _field.SetFloat("_WaveOffset", _waveOffset * PI * 2);
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
                _field.SetVector("_MeleeDirection", Quaternion.AngleAxis(sign(_meleeTime*2-1) * MeleeAngle * pow(abs(_meleeTime * 2 - 1), MeleeAngleExponent), Vector3.up) * -Vector3.forward);
                _field.SetFloat("_MeleeDisplacement", pow(1 - 2 * abs(_meleeTime - .5f), MeleeRangeExponent) * MeleeRange);
                _field.SetFloat("_MeleeShape", MeleeShaping);
                _field.SetFloat("_MeleeFlattening", MeleeFlatness);
            }
            else
            {
                _meleeActive = false;
                _field.SetFloat("_MeleeDisplacement", 0);
            }
        }
        
        
        if(_grabObject != null)
        {
            _grabTime += Time.deltaTime / _grabPhase switch
            {
                GrabPhase.Extend => GrabExtendTime,
                GrabPhase.Envelop => GrabEnvelopTime,
                GrabPhase.Pull => GrabPullTime,
                _ => throw new ArgumentOutOfRangeException()
            };
            if (_grabTime > 1)
            {
                _grabTime = 0;
                if (_grabPhase == GrabPhase.Envelop)
                {
                    _grabObjectEndPos = _grabObject.position;
                }
                if (_grabPhase == GrabPhase.Pull)
                {
                    Destroy(_grabObject.gameObject);
                    _grabObject = null;
                    _field.SetFloat("_TendrilInfluence", 0);
                }
                else _grabPhase++;
            }

            if (_grabObject != null)
            {
                _tendrilBasePos = AetheriaMath.Damp(_tendrilBasePos, _grabObject.position, TendrilBaseDamping, Time.deltaTime);
                switch (_grabPhase)
                {
                    case GrabPhase.Extend:
                        _tendrilBendTarget = _grabObjectStartPos;
                        _grabObject.position += _grabObjectVelocity * Time.deltaTime;
                        _tendrilTargetPos = lerp(
                            lerp(_grabObjectStartTendrilBasePos, _grabObjectStartPos, _grabTime), 
                            lerp(_grabObjectStartPos, _grabObject.position, _grabTime),
                            pow(_grabTime, TendrilExtensionExponent));
                        _field.SetFloat("_TendrilInfluence", pow(_grabTime, TendrilExtendBaseAnimationExponent));
                        _field.SetFloat("_TendrilSize", lerp(TendrilBaseRadius/2,TendrilBaseRadius,pow(_grabTime, TendrilExtendBaseAnimationExponent)));
                        _field.SetFloat("_TendrilRadius", _grabObjectScale * pow(_grabTime, TendrilTipRadiusAnimationExponent) * TendrilTipRadius);
                        break;
                    case GrabPhase.Envelop:
                        _tendrilBendTarget = _grabObjectStartPos;
                        _tendrilTargetPos = _grabObject.position += _grabObjectVelocity * Time.deltaTime * (1-_grabTime);
                        _field.SetFloat("_TendrilInfluence", 1);
                        _field.SetFloat("_TendrilSize", TendrilBaseRadius);
                        _field.SetFloat("_TendrilRadius", _grabObjectScale * TendrilTipRadius);
                        break;
                    case GrabPhase.Pull:
                        _tendrilTargetPos = _grabObject.position = lerp(_grabObjectEndPos, transform.position, _grabTime*_grabTime);
                        _tendrilBendTarget = lerp(_grabObjectStartPos, _grabObjectEndPos, _grabTime*_grabTime);
                        if (_fadePoint > .99 && transform.InverseTransformPoint(_grabObject.position).sqrMagnitude < 1)
                            _fadePoint = _grabTime;
                        _field.SetFloat("_TendrilInfluence", pow(smoothstep(1, _fadePoint, _grabTime), TendrilFadeExponent));
                        _field.SetFloat("_TendrilSize", TendrilBaseRadius);
                        _field.SetFloat("_TendrilRadius", _grabObjectScale * TendrilTipRadius);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                _field.SetVector("_TendrilBase", Vector3.Scale(transform.InverseTransformPoint(_tendrilBasePos).normalized, transform.localScale));
                _field.SetVector("_TendrilBend", Vector3.Scale(transform.InverseTransformPoint(_tendrilBendTarget), transform.localScale));
                _field.SetVector("_TendrilTarget", Vector3.Scale(transform.InverseTransformPoint(_tendrilTargetPos), transform.localScale));
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

    private const int GIZMO_STEPS = 16;
    private void OnDrawGizmosSelected()
    {
        if (_grabObject != null)
        {
            Vector3 previous = _tendrilBasePos;
            for (int i = 1; i <= GIZMO_STEPS; i++)
            {
                var l = (float)i / GIZMO_STEPS;
                var next = AetheriaMath.GetQuadraticSplinePosition(_tendrilBasePos, _tendrilBendTarget, _tendrilTargetPos, l);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }

    private void OnDestroy()
    {
        _hitBuffer.Dispose();
    }
}
