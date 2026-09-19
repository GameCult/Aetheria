using UnityEngine;

// Cut 3 (docs/fire-control-cut.md): the SphereCast, shield branch and SendHit are deleted -- FireControl
// already rolled and resolved this shot before Fire() below ever ran (a velocity-0 weapon like this one
// commits and resolves in the same tick it fires). The endpoint capture stays as a visual: it now just tracks
// the target's transform instead of a physics hit (R8).
public class Lightning : MonoBehaviour
{
    public LightningCompute LightningCompute;
    public float HitRadius;

    public EntityInstance Source { get; set; }
    public float Range { get; set; }
    public EntityInstance Target { get; set; }
    public Transform Barrel { get; set; }

    // Cut 3: FireControl.Fire's ShotId. Unused by this simple endpoint-only presentation today, kept so a
    // future pass can draw to the exact rolled cell instead of the target's overall position.
    public int ShotId { get; set; }

    private Vector3 _endpoint;

    public void Fire()
    {
        LightningCompute.OnLeaderComplete = null;
        LightningCompute.FixedEndpoint = Target != null;
        LightningCompute.OnPulseComplete = () =>
            GetComponent<Prototype>().ReturnToPool();
        LightningCompute.StartAnimation();
        _endpoint = Barrel.position + Barrel.forward * Range;
    }

    private void Update()
    {
        if (Barrel == null) return;

        if (Target != null) _endpoint = Target.transform.position;

        LightningCompute.EndPosition = _endpoint;
        LightningCompute.StartPosition = Barrel.position;
    }
}
