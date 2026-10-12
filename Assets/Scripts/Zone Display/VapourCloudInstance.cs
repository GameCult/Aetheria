using CultMath;
using CultMath.UnityBridge;
using UnityEngine;

// How one vented vapour cloud looks: a fog-bank brush painted into the Patch layer, which the volumetric pass reads as
// patch thickness. Zone owns every cloud fact (existence, body, radius, opacity); this reads them and draws, and writes
// nothing back (rulings vapour-cloud-obscuration, unity-presents-only-r3). The brush's depth thins with the cloud's
// opacity, so the bank fades to nothing at the cloud's lifetime.
public class VapourCloudInstance : MonoBehaviour
{
    // The world height the scene's patch brushes sit at (Depth Micro), inside the Patch camera's clip range.
    public float PatchHeight = -5f;

    private static readonly int Depth = Shader.PropertyToID("_Depth");

    private Zone _zone;
    private VapourCloud _cloud;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;
    private float _authoredDepth;

    public void Bind(Zone zone, VapourCloud cloud)
    {
        _zone = zone;
        _cloud = cloud;
        _renderer = GetComponent<MeshRenderer>();
        _block = new MaterialPropertyBlock();
        _authoredDepth = _renderer.sharedMaterial.GetFloat(Depth);
        Place();
    }

    private void Update() => Place();

    private void Place()
    {
        var p = DrawAhead.Position(_cloud.Body, ActionGameManager.Clock.Lead);
        transform.position = new Vector3(p.x, PatchHeight, p.y);
        transform.localScale = Vector3.one * (2f * _cloud.Radius);

        // A cloud with no opacity left is already gone from the sim; a zero Opacity would divide by zero.
        var thinning = _cloud.Opacity > 0f ? _cloud.OpacityAt(_zone.Time) / _cloud.Opacity : 0f;
        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(Depth, _authoredDepth * thinning);
        _renderer.SetPropertyBlock(_block);
    }
}
