using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cut 4 (docs/fire-control-cut.md): the raycast, shield branch and SendHit are deleted -- FireControl already
// decided this shot's fate before Fire() was ever called (R8), the same as Laser/Lightning (Cut 3). What is
// left is pure endpoint presentation: draw to the target's current position when there is one, out to Range
// otherwise.
public class HitscanEffect : MonoBehaviour
{
    public float Duration;
    public AnimationCurve IntensityCurve;
    public LineRenderer Line;
    public ParticleSystem LineEffect;
    public Prototype HitEffect;

    // Cut 4: FireControl.Fire's ShotId -- unused by this endpoint-only presentation today, kept so a future
    // pass can draw to the exact rolled cell instead of the target's overall position (Laser.ShotId).
    public int ShotId { get; set; }
    public Entity SourceEntity { get; set; }
    public float Range { get; set; }
    public Transform TargetTransform { get; set; }

    private float _startTime;
    private bool _active = false;

    public void Fire()
    {
        _startTime = Time.time;
        var length = TargetTransform != null
            ? Vector3.Distance(transform.position, TargetTransform.position)
            : Range;

        Line.SetPosition(1, Vector3.forward * length);
        var emission = LineEffect.emission;
        emission.rateOverTimeMultiplier = length;
        var shape = LineEffect.shape;
        shape.position = Vector3.forward * (length / 2);
        shape.scale = Vector3.one * (length / 2);

        LineEffect.Play(true);
        _active = true;
    }

    void Update()
    {
        if (!_active) return;
        var lerp = (Time.time - _startTime) / Duration;
        if (lerp > 1)
        {
            GetComponent<Prototype>().ReturnToPool();
            return;
        }
        
        Line.widthMultiplier = IntensityCurve.Evaluate(lerp);
    }
}
