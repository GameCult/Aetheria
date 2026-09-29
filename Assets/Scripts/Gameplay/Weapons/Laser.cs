using UnityEngine;

// Cut 3 (docs/fire-control-cut.md): the raycast, shield branch and SendHit are deleted -- FireControl already
// rolled and, for a velocity-0 weapon like this one, already resolved this shot before the beam was ever
// drawn (a short flight commits and resolves in the same Zone.Update tick as Fire). The beam endpoint is
// presentation, rebuilt from the target's current position rather than a physics hit -- it no longer decides
// anything (R8).
public class Laser : MonoBehaviour
{
    public AnimationCurve IntensityCurve;
    public float Duration;
    public LineRenderer LineRenderer;

    public Entity SourceEntity { get; set; }
    public float Range { get; set; }

    // Cut 3: FireControl.Fire's ShotId. Unused by this simple endpoint-only presentation today, kept so a
    // future pass can draw the beam to the exact rolled cell instead of the target's overall position.
    public int ShotId { get; set; }
    public Transform TargetTransform { get; set; }

    private float _startTime;
    private readonly Vector3[] _zeros = {Vector3.zero, Vector3.zero};

    private void OnEnable()
    {
        _startTime = Time.time;
        LineRenderer.SetPositions(_zeros);
    }

    private void Update()
    {
        var lerp = (Time.time - _startTime) / Duration;
        if (lerp > 1)
        {
            GetComponent<Prototype>().ReturnToPool();
            return;
        }

        LineRenderer.SetPosition(0, transform.position);
        var endpoint = TargetTransform != null
            ? TargetTransform.position
            : transform.position + transform.forward * Range;
        LineRenderer.SetPosition(1, endpoint);

        LineRenderer.widthMultiplier = IntensityCurve.Evaluate(lerp);
    }
}
