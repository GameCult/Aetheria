using System;
using System.Collections;
using UnityEngine;
using CultMath;
using CultMath.UnityBridge;
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
    private float _spawnTime;
    private Vector3 _targetVelocity;
    private Vector3 _previousTargetPosition;

    // Cut 3: FireControl.Fire's ShotId -- this missile's handle onto Zone.ShotCommitted/ShotResolved.
    public int ShotId { get; set; }
    public Transform Target { get; set; }
    public float3 StartPosition { get; set; }
    public float Range { get; set; }
    public Vector3 Velocity { get; set; }
    public Entity SourceEntity { get; set; }

    // How long after spawn the round keeps its binding to the simulation's verdict: the shot's own flight time (the
    // manager sets it from the committed shot's ArrivalTime), so a round that faded out early still lives to show
    // the detonation the simulation resolves at arrival. 0 (the default) kills the round when its fade ends.
    public float BindingLifetime { get; set; }

    public event Action OnKill;

    // The simulation's verdict on this round, bound by GuidedProjectileManager to Zone.ShotCommitted and
    // ShotResolved for its ShotId: this object flies and looks, and neither decides where the round bursts nor when.
    // The one owner of the subscription's lifetime: a new binding disposes the one it replaces, Kill disposes it,
    // and so does destruction, so a projectile torn down with its scene never leaves an observer on the zone's
    // subjects to touch a destroyed transform.
    public IDisposable Binding
    {
        get => _binding;
        set
        {
            _binding?.Dispose();
            _binding = value;
        }
    }

    private IDisposable _binding;

    void OnDestroy() => Binding = null;

    // The commit named a burst point: fly to it, whatever this round was homing on (a target it would have
    // passed, or a Range clamp it would have overshot). The point is a static target, so the old target's motion
    // is dropped with it: the jump to the new point must not feed the guidance a target velocity.
    public void BurstAt(Vector3 point)
    {
        Target = null;
        TargetPosition = () => point;
        _previousTargetPosition = point;
        _targetVelocity = Vector3.zero;
    }

    // The resolution. A round that detonated (a hit, or a burst) bursts now, at the simulation's burst point when
    // it has one; a round the simulation resolved without a detonation (a contact or delayed round that missed,
    // one whose target is gone) just falls away, with no explosion to show.
    public void Resolve(bool detonated, Vector3? point)
    {
        // A round that already faded out (overshot, out of flight) still shows the simulation's detonation: only a
        // round that has been killed is done. The fade is stopped so it cannot Kill the round a second time.
        if (!_alive) return;
        if (!detonated)
        {
            if (_active) StartCoroutine(FadeOut());
            return;
        }
        StopAllCoroutines();
        if (point.HasValue) transform.position = point.Value;
        if (HitEffect != null)
        {
            var ht = HitEffect.Instantiate<Transform>();
            ht.position = transform.position;
        }
        StartCoroutine(Kill());
    }

    void OnEnable()
    {
        _active = _alive = true;
        _spawnTime = Time.time;
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
            
            // Out of range, or flown past its target: the round falls away. Only the simulation's detonation
            // (Resolve) shows an explosion; a round that merely ran out of flight has none.
            if (sourceDist > Range || dot(diff.ToCultMath(), Velocity.ToCultMath()) < 0)
            {
                StartCoroutine(FadeOut());
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

        // Faded, but the simulation's verdict has not necessarily arrived: wait for it until the shot's flight is over.
        while (Time.time - _spawnTime < BindingLifetime) yield return null;
        StartCoroutine(Kill());
    }

    IEnumerator Kill()
    {
        Binding = null;
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