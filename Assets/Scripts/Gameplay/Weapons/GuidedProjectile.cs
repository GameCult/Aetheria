using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using static Noise1D;

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
    
    public Transform Target { get; set; }
    public Vector3 StartPosition { get; set; }
    public float Range { get; set; }
    public Vector3 Velocity { get; set; }
    public float ImpactIntensity { get; set; } = 1;
    public Entity SourceEntity { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }

    public event Action OnKill;

    void OnEnable()
    {
        _active = _alive = true;
        _phase = Random.value * 100;
        _prevDist = Single.MaxValue;
        SetParticleColor(Color.white);
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
            var position = t.position;

            var targetPosition = TargetPosition?.Invoke() ?? Target.position;
            _targetVelocity = Vector3.Lerp(_targetVelocity, targetPosition - _previousTargetPosition, Mathf.Clamp01(Time.deltaTime * 5));
            _previousTargetPosition = targetPosition;
            targetPosition = AetheriaMath.FirstOrderIntercept(position, Vector3.zero, TopSpeed, targetPosition, _targetVelocity);

            var diff = targetPosition - transform.position;
            var targetDist = diff.magnitude;
            var sourceDist = Vector2.Distance(new Vector2(StartPosition.x, StartPosition.z), new Vector2(position.x, position.z));
            
            if (sourceDist > Range || Vector3.Dot(diff, Velocity) < 0)
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
            var right = Vector3.Cross(dir, Vector3.up);
            var up = Vector3.Cross(dir, right);

            if (Children > 0 && SplitTime < curveLerp)
            {
                for (int i = 0; i < Children; i++)
                {
                    var child = ChildProjectile.Instantiate<GuidedProjectile>();
                    child.transform.position = t.position;
                    child.StartPosition = StartPosition;
                    var randomDirection = Random.insideUnitCircle.normalized;
                    var perpendicularRandom = randomDirection.x * right + randomDirection.y * up;
                    child.Velocity = Vector3.Lerp(perpendicularRandom, dir, SplitSeparationForwardness).normalized * Velocity.magnitude * SplitSeparationVelocity;
                    child.Range = Range;
                    child.ImpactIntensity = ImpactIntensity;
                    child.Source = Source;
                    child.Target = Target;
                    child.SourceEntity = SourceEntity;
                    child.ZoneRenderer = ZoneRenderer;
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
            
            var dodge = Vector3.Lerp(
                (right * noise(Time.time * Frequency + _phase) + up * noise(Time.time * Frequency + (100 + _phase))).normalized,
                Vector3.up,
                LiftCurve.Evaluate(curveLerp)).normalized;
            var desired = Vector3.Slerp(dodge, dir, GuidanceCurve.Evaluate(curveLerp)).normalized * TopSpeed;
            var thrustCurve = ThrustCurve.Evaluate(curveLerp);
            var thrust = Thrust * thrustCurve;
            var c = Color.white * thrustCurve;
            c.a = 1;
            SetParticleColor(c);
            Velocity += (desired-Velocity).normalized * (thrust * Time.deltaTime);
        }

        if(_alive)
        {
            if (AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls(
                    ZoneRenderer,
                    SourceEntity,
                    t.position,
                    Velocity,
                    Velocity.magnitude * Time.deltaTime,
                    0,
                    out var hits) &&
                hits.Count > 0)
            {
                var hit = hits[0];
                var hull = hit.Hull;
                var entity = hull.Entity;
                if (entity.Shield != null && entity.Shield.Item.Active.Value)
                {
                    hit.Shield?.ShowHit(hit.Point, Mathf.Max(0.01f, ImpactIntensity));
                }
                else
                {
                }

                transform.position = hit.Point;
                StartCoroutine(Kill());
                
                if (HitEffect != null)
                {
                    var ht = HitEffect.Instantiate<Transform>();
                    ht.SetParent(hull.transform);
                    ht.position = hit.Point;
                }

                return;
            }

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
            SetParticleColor(c);
            yield return null;
        }

        StartCoroutine(Kill());
    }

    private void SetParticleColor(Color color)
    {
        var main = Particles.main;
        main.startColor = color;
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
