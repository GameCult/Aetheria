using System;
using System.Collections;
using UnityEngine;
using CultMath;
using static CultMath.math;
using Random = UnityEngine.Random;
using static Noise1D;
using float3 = CultMath.float3;

// Cut 3 (docs/fire-control-cut.md): the raycast, shield branch and SendHit are deleted -- FireControl already
// decided this shot's fate before this object was ever spawned (R8). The homing/guidance flight (the whole
// point of this presentation) is untouched; children spawned on split carry no damage of their own to apply
// either, since they never did anything but inherit their parent's now-deleted fields.
public class GuidedProjectile : MonoBehaviour
{
    public Prototype HitEffect;
    public Prototype ChildProjectile;
    public int Children;
    public float SplitTime;
    public float SplitSeparationForwardness;
    public float SplitSeparationVelocity;
    public ParticleSystem Particles;
    public float FadeOutTime;
    public AnimationCurve ThrustCurve;
    public AnimationCurve GuidanceCurve;
    public AnimationCurve LiftCurve;
    public float Frequency;
    public float Thrust;
    public float TopSpeed;
    public Transform Source;
    public Func<Vector3> TargetPosition;

    private float _phase;
    private float _prevDist;
    private bool _active;
    private bool _alive;
    private Vector3 _targetVelocity;
    private Vector3 _previousTargetPosition;

    // Cut 3: FireControl.Fire's ShotId -- this missile's handle onto Zone.ShotCommitted/ShotResolved.
    public int ShotId { get; set; }
    public Transform Target { get; set; }
    public float3 StartPosition { get; set; }
    public float Range { get; set; }
    public Vector3 Velocity { get; set; }
    public Entity SourceEntity { get; set; }

    public event Action OnKill;

    void OnEnable()
    {
        _active = _alive = true;
        _phase = Random.value * 100;
        _prevDist = Single.MaxValue;
        Particles.startColor = Color.white;
        //var main = Particles.main;
        //main.startColor = new ParticleSystem.MinMaxGradient { mode = ParticleSystemGradientMode.Color, color = Color.white };
        Particles.Clear(true);
        Particles.Play(true);
    }
	
    void Update ()
    {
        if (SourceEntity == null) return;

        var t = transform;
        
        if (_active)
        {
            if (TargetPosition == null && !Target)
            {
                StartCoroutine(FadeOut());
                return;
            }
            var position = t.position.ToCultMath();

            var targetPosition = TargetPosition?.Invoke() ?? Target.position;
            _targetVelocity = lerp(_targetVelocity.ToCultMath(), (targetPosition - _previousTargetPosition).ToCultMath(), saturate(Time.deltaTime * 5)).ToUnity();
            _previousTargetPosition = targetPosition;
            targetPosition = first_order_intercept(position,float3.zero, TopSpeed, targetPosition.ToCultMath(), _targetVelocity.ToCultMath()).ToUnity();

            var diff = targetPosition - transform.position;
            var targetDist = diff.magnitude;
            var sourceDist = length(StartPosition.xz - position.xz);
            
            if (sourceDist > Range || dot(diff.ToCultMath(), Velocity.ToCultMath()) < 0)
            {
                StartCoroutine(FadeOut());
                if (HitEffect != null)
                {
                    var ht = HitEffect.Instantiate<Transform>();
                    ht.position = t.position;
                }
                return;
            }
            _prevDist = targetDist;
            
            var targetDistFlat = diff.Flatland().magnitude;
            var curveLerp = 1 - targetDistFlat / (sourceDist + targetDistFlat);
            var dir = diff.normalized;
            var right = cross(dir.ToCultMath(), float3(0, 1, 0));
            var up = cross(dir.ToCultMath(), right);

            if (Children > 0 && SplitTime < curveLerp)
            {
                for (int i = 0; i < Children; i++)
                {
                    var child = ChildProjectile.Instantiate<GuidedProjectile>();
                    child.transform.position = t.position;
                    child.StartPosition = StartPosition;
                    var randomDirection = normalize(Random.insideUnitCircle.ToCultMath());
                    var perpendicularRandom = randomDirection.x * right + randomDirection.y * up;
                    child.Velocity = (normalize(lerp(perpendicularRandom, dir.ToCultMath(), SplitSeparationForwardness)) * length(Velocity.ToCultMath()) * SplitSeparationVelocity).ToUnity();
                    child.Range = Range;
                    child.Source = Source;
                    child.Target = Target;
                    child.SourceEntity = SourceEntity;
                    child.GuidanceCurve = GuidanceCurve;
                    child.LiftCurve = LiftCurve;
                    child.ThrustCurve = ThrustCurve;
                    child.Thrust = Thrust;
                    child.TopSpeed = TopSpeed;
                    child.Frequency = Frequency;
                    child.Thrust = Thrust;
                }
                StartCoroutine(Kill());
                if (HitEffect != null)
                {
                    var ht = HitEffect.Instantiate<Transform>();
                    ht.position = t.position;
                }
            }
            
            var dodge = normalize(lerp(
                normalize(right * noise(Time.time * Frequency + _phase) + up * noise(Time.time * Frequency + (100 + _phase))),
                float3(0, 1, 0), LiftCurve.Evaluate(curveLerp))).ToUnity();
            var desired = Vector3.Slerp(dodge, dir, GuidanceCurve.Evaluate(curveLerp)).normalized * TopSpeed;
            var thrustCurve = ThrustCurve.Evaluate(curveLerp);
            var thrust = Thrust * thrustCurve;
            var c = Color.white * thrustCurve;
            c.a = 1;
            Particles.startColor = c;
            Velocity += (desired-Velocity).normalized * (thrust * Time.deltaTime);
        }

        if(_alive)
        {
            t.position += Velocity * Time.deltaTime;
        }
    }

    IEnumerator FadeOut()
    {
        _active = false;
        var startTime = Time.time;
        while (Time.time - startTime < FadeOutTime)
        {
            var lerp = 1 - (Time.time - startTime) / FadeOutTime;
            var c = Color.white * lerp;
            c.a = 1;
            Particles.startColor = c;
            //var main = Particles.main;
            //main.startColor = new ParticleSystem.MinMaxGradient { mode = ParticleSystemGradientMode.Color, color = Color.white * lerp };
            yield return null;
        }

        StartCoroutine(Kill());
    }

    IEnumerator Kill()
    {
        _active = false;
        _alive = false;
        Particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        var startTime = Time.time;
        var lifetime = Particles.main.startLifetime.constant;
        while (Time.time - startTime < lifetime)
        {
            yield return null;
        }
        OnKill?.Invoke();
        GetComponent<Prototype>().ReturnToPool();
    }
}