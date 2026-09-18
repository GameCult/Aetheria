using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;

// Implements the capability presenter interfaces (Assets/Scripts/Gameplay/CapabilityPresenters.cs) by
// delegating to its own pre-existing methods/fields; it decides nothing about capability rules,
// per docs/headless-playground-cut.md fork L. CapabilityPresentationBinder is what actually
// subscribes these methods to a CapabilityEvents source -- FieldDriver has no reference to one.
public class FieldDriver : MonoBehaviour, IAbsorbPresenter, IGrabPresenter, IMeleePresenter, IThrustPresenter
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
    
    [Inspectable]
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
    
    [Inspectable]
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

    [Inspectable]
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
    private ShieldEnvelope _envelope;
    private bool _loggedMissingEnvelope;
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
    private TendrilPhase _grabPhase;
    private Vector3 _tendrilBasePos;
    private Vector3 _tendrilBendTarget;
    private Vector3 _tendrilTargetPos;
    private float _fadePoint;

    // Named distinctly from the global GrabPhase (CapabilityEvents.cs): this is FieldDriver's own
    // internal tendril-animation timing, still owned here until a real pickup capability exists to
    // publish per-phase GrabEvent transitions (see Grab(GrabEvent, Transform, Vector3) below).
    private enum TendrilPhase
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
        _hitBuffer = new ComputeBuffer(MaxHits, 32);
        _envelope = GetComponent<ShieldEnvelope>();
    }

    // Cut 2 of docs/shield-panel-cut.md: FieldDriver no longer owns the ellipsoid envelope --
    // ShieldEnvelope does, and this transform's localScale is display geometry for the field mesh
    // only now. Absent an envelope (compatibility path only; every real carrier gets one in this
    // cut), fall back to the pre-Cut-2 formulas and log once rather than silently keeping a second
    // opinion about the envelope.
    private void WarnMissingEnvelopeOnce()
    {
        if (_loggedMissingEnvelope) return;
        _loggedMissingEnvelope = true;
        Debug.LogWarning("FieldDriver has no ShieldEnvelope; falling back to its own transform for the envelope.", this);
    }

    private Vector3 EnvelopeRadiiOrFallback()
    {
        if (_envelope != null) return _envelope.Radii;
        WarnMissingEnvelopeOnce();
        return transform.localScale;
    }

    // Cut 2 note on the two branches below NOT being the same computation shape: envelope.
    // ProjectToSurface returns a WORLD-space point (it transforms back through rotation and
    // translation -- Cut 3's panel presenter needs that). The fallback reproduces the pre-Cut-2
    // formula exactly instead, which stayed in LOCAL space with no rotation/translation applied,
    // because that is what the shader consumes here (_Hits is uploaded and read in the field
    // mesh's own local space, not world space). The two branches are only numerically identical
    // when the carrier's transform has zero position and identity rotation -- true of the one
    // FieldDriver carrier in the tree today (FieldShieldTest.unity's cubesphere), NOT true in
    // general. This is an existing characteristic of Cut 2 as scoped by the map (FieldDriver.cs:125
    // reads envelope.ProjectToSurface(position) directly, per the map's own Cut 2 text), not
    // something introduced by this fallback -- flagging it so nobody assumes the two branches are
    // interchangeable for a future non-origin, non-identity FieldDriver carrier.
    public void AddHit(float3 position, float3 direction, float magnitude)
    {
        if (_hits.Count >= MaxHits) return;
        Vector3 surfacePosition;
        if (_envelope != null)
            surfacePosition = _envelope.ProjectToSurface(position.ToUnity());
        else
        {
            WarnMissingEnvelopeOnce();
            surfacePosition = ShieldEnvelope.LegacyLocalSurfacePoint(transform, position.ToUnity());
        }
        var hit = new FieldHit
        {
            Position = surfacePosition.ToCultMath(),
            Direction = normalize((transform.rotation * direction.ToUnity()).ToCultMath()),
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
        _grabPhase = TendrilPhase.Extend;
        _grabObject = t;
        _grabObjectStartPos = t.position;
        _grabObjectVelocity = v;
        _grabObjectScale = t.localScale.x;
        _tendrilBasePos = _grabObjectStartTendrilBasePos = _collider.ClosestPoint(_grabObjectStartPos);
        _fadePoint = .999f;
    }

    // IAbsorbPresenter: delegates straight to the existing hit-buffer mechanism. e.DamageType is not
    // read -- the field shield has no per-damage-type visual today; it rides along in the event for
    // presentations that do vary by type (the mask itself is absorb-capability data, out of scope here).
    public void Absorb(AbsorbEvent e) => AddHit(e.Position, e.Direction, e.Magnitude);

    // IGrabPresenter: only the Started phase does anything today. FieldDriver still times the
    // extend/envelop/pull animation itself (TendrilPhase above, driven by GrabExtendTime/
    // GrabEnvelopTime/GrabPullTime) because no capability yet publishes those phase transitions;
    // once one does, this is where FieldDriver would react to Envelop/Pull/Completed/Cancelled too.
    public void Grab(GrabEvent e, Transform target, Vector3 initialVelocity)
    {
        if (e.Phase == GrabPhase.Started && target != null)
            GrabObject(target, initialVelocity);
    }

    // IMeleePresenter: the field shield's swing shape comes from its own authored curves
    // (MeleeRange/MeleeAngle/MeleeDuration/etc, tuned in the inspector), not from the event's
    // direction/range/arc/duration -- those exist for presentations that do read them.
    public void Melee(MeleeEvent e) => Melee();

    // IThrustPresenter: Throttle is not read; pushMag is derived from PlanarThrust in Update() below,
    // matching the field shield's existing behaviour.
    public void Thrust(ThrustEvent e)
    {
        Push = e.PlanarThrust;
        FrontTwist = e.Twist.x;
        RearTwist = e.Twist.y;
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
        
        var envelopeRadii = EnvelopeRadiiOrFallback();
        _field.SetVector("_InverseScale", new Vector4(1/envelopeRadii.x,1/envelopeRadii.y,1/envelopeRadii.z));
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
                TendrilPhase.Extend => GrabExtendTime,
                TendrilPhase.Envelop => GrabEnvelopTime,
                TendrilPhase.Pull => GrabPullTime,
                _ => throw new ArgumentOutOfRangeException()
            };
            if (_grabTime > 1)
            {
                _grabTime = 0;
                if (_grabPhase == TendrilPhase.Envelop)
                {
                    _grabObjectEndPos = _grabObject.position;
                }
                if (_grabPhase == TendrilPhase.Pull)
                {
                    Destroy(_grabObject.gameObject);
                    _grabObject = null;
                    _field.SetFloat("_TendrilInfluence", 0);
                }
                else _grabPhase++;
            }

            if (_grabObject != null)
            {
                _tendrilBasePos = damp(_tendrilBasePos.ToCultMath(), _grabObject.position.ToCultMath(), TendrilBaseDamping, Time.deltaTime).ToUnity();
                switch (_grabPhase)
                {
                    case TendrilPhase.Extend:
                        _tendrilBendTarget = _grabObjectStartPos;
                        _grabObject.position += _grabObjectVelocity * Time.deltaTime;
                        _tendrilTargetPos = lerp(
                            lerp(_grabObjectStartTendrilBasePos.ToCultMath(), _grabObjectStartPos.ToCultMath(), _grabTime), 
                            lerp(_grabObjectStartPos.ToCultMath(), _grabObject.position.ToCultMath(), _grabTime),
                            pow(_grabTime, TendrilExtensionExponent)).ToUnity();
                        _field.SetFloat("_TendrilInfluence", pow(_grabTime, TendrilExtendBaseAnimationExponent));
                        _field.SetFloat("_TendrilSize", lerp(TendrilBaseRadius/2,TendrilBaseRadius,pow(_grabTime, TendrilExtendBaseAnimationExponent)));
                        _field.SetFloat("_TendrilRadius", _grabObjectScale * pow(_grabTime, TendrilTipRadiusAnimationExponent) * TendrilTipRadius);
                        break;
                    case TendrilPhase.Envelop:
                        _tendrilBendTarget = _grabObjectStartPos;
                        _tendrilTargetPos = _grabObject.position += _grabObjectVelocity * Time.deltaTime * (1-_grabTime);
                        _field.SetFloat("_TendrilInfluence", 1);
                        _field.SetFloat("_TendrilSize", TendrilBaseRadius);
                        _field.SetFloat("_TendrilRadius", _grabObjectScale * TendrilTipRadius);
                        break;
                    case TendrilPhase.Pull:
                        _tendrilTargetPos = _grabObject.position = lerp(_grabObjectEndPos.ToCultMath(), transform.position.ToCultMath(), _grabTime*_grabTime).ToUnity();
                        _tendrilBendTarget = lerp(_grabObjectStartPos.ToCultMath(), _grabObjectEndPos.ToCultMath(), _grabTime*_grabTime).ToUnity();
                        if (_fadePoint > .99 && transform.InverseTransformPoint(_grabObject.position).sqrMagnitude < 1)
                            _fadePoint = _grabTime;
                        _field.SetFloat("_TendrilInfluence", pow(smoothstep(1, _fadePoint, _grabTime), TendrilFadeExponent));
                        _field.SetFloat("_TendrilSize", TendrilBaseRadius);
                        _field.SetFloat("_TendrilRadius", _grabObjectScale * TendrilTipRadius);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                var tendrilRadii = EnvelopeRadiiOrFallback();
                _field.SetVector("_TendrilBase", Vector3.Scale(transform.InverseTransformPoint(_tendrilBasePos).normalized, tendrilRadii));
                _field.SetVector("_TendrilBend", Vector3.Scale(transform.InverseTransformPoint(_tendrilBendTarget), tendrilRadii));
                _field.SetVector("_TendrilTarget", Vector3.Scale(transform.InverseTransformPoint(_tendrilTargetPos), tendrilRadii));
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
                var next = quadratic_bezier(_tendrilBasePos.ToCultMath(), _tendrilBendTarget.ToCultMath(), _tendrilTargetPos.ToCultMath(), l).ToUnity();
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
