using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// The renderer's lowering of the zone's potential (aetheria-gravity, cut grid-lowering). Zone.GetHeight owns the drawn
/// height: this hands its inputs (each well's position, strength, core radius and reference, and the exaggeration) to
/// Assets/Shaders/Gravity.cginc, which sums them per texel, and makes the one quad that carries the sum into every
/// camera that culls layer 9. Nothing here decides a height.
/// </summary>
public static class GravityLowering
{
    /// <summary>The layer the gravity cameras cull; nothing but the run-time field sits on it.</summary>
    public const int FieldLayer = 9;
    /// <summary>The quad's side in u: larger than any zone (10000) plus any view (4096); texels outside a camera cost nothing.</summary>
    public const float FieldExtent = 100000f;

    // Mirrors the well record of Gravity.cginc: float2 position, strength, coreRadius, reference.
    [StructLayout(LayoutKind.Sequential)]
    struct WellRecord
    {
        public Vector2 Position;
        public float Strength;
        public float CoreRadius;
        public float Reference;
    }

    static readonly int Stride = Marshal.SizeOf<WellRecord>();
    static readonly int WellsId = Shader.PropertyToID("_GravityWells");
    static readonly int CountId = Shader.PropertyToID("_GravityWellCount");
    static readonly int ExaggerationId = Shader.PropertyToID("_HeightExaggeration");
    static WellRecord[] _records = new WellRecord[0];

    /// <summary>
    /// Fills one record per body of the zone (a null zone has none) and sets the shader globals. The buffer is the
    /// caller's, (re)allocated to the body count (at least one, so the global is always bound).
    /// </summary>
    public static void Lower(Zone zone, ref ComputeBuffer wells)
    {
        var count = zone?.PlanetInstances.Count ?? 0;
        var capacity = Mathf.Max(count, 1);
        if (wells == null || wells.count != capacity)
        {
            wells?.Release();
            wells = new ComputeBuffer(capacity, Stride);
        }

        if (_records.Length != capacity) _records = new WellRecord[capacity];
        var i = 0;
        if (zone != null)
            foreach (var body in zone.PlanetInstances.Values)
                _records[i++] = new WellRecord
                {
                    Position = new Vector2(body.Orbit.Position.x, body.Orbit.Position.y),
                    Strength = body.WellStrength,
                    CoreRadius = body.CoreRadius,
                    Reference = zone.WellReference(body)
                };
        wells.SetData(_records);

        Shader.SetGlobalBuffer(WellsId, wells);
        Shader.SetGlobalInt(CountId, count);
        Shader.SetGlobalFloat(ExaggerationId, zone?.Settings.HeightExaggeration ?? 1f);
    }

    /// <summary>The one quad of the field: flat in xz at y 0 on layer 9, drawn with the Gravity Field material.</summary>
    public static GameObject CreateField(Material material)
    {
        var field = new GameObject("Gravity Field") { layer = FieldLayer };
        field.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        field.transform.localScale = FieldExtent * Vector3.one;
        field.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = field.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return field;
    }
}
