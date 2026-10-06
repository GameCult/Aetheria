using UnityEngine;

public class ThrusterHardpoint : MonoBehaviour
{
    public MeshRenderer Emitter;

    // Points an exhaust's shape at a hardpoint's emitter, or at nothing when the ship has no such hardpoint. The emitter
    // carries the invisible material (alpha 0), so the particles never take their colour from its mesh: with Use Mesh
    // Colors on, Unity's default, they would all be born transparent.
    public static void WireExhaust(ParticleSystem exhaust, ThrusterHardpoint hardpoint)
    {
        var shape = exhaust.shape;
        shape.useMeshColors = false;
        shape.meshRenderer = hardpoint != null ? hardpoint.Emitter : null;
    }
}
