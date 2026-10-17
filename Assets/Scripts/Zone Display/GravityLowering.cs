using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// The renderer's lowering of the zone's potential (aetheria-gravity, cut grid-lowering). Zone.GetHeight owns the drawn
/// height: this hands its inputs (each term's position, strength, softening, band edges and reference, and the
/// exaggeration) to Assets/Shaders/Gravity.cginc, which sums them per texel, and makes the one quad that carries the sum into every
/// camera that culls layer 9. Nothing here decides a height.
/// </summary>
public static class GravityLowering
{
    /// <summary>The layer the gravity cameras cull; nothing but the run-time field sits on it.</summary>
    public const int FieldLayer = 9;
    /// <summary>The quad's side in u: larger than any zone (10000) plus any view (4096); texels outside a camera cost nothing.</summary>
    public const float FieldExtent = 100000f;

    // Mirrors the term record of Gravity.cginc: float2 position, strength, softening, inner, outer, reference.
    [StructLayout(LayoutKind.Sequential)]
    struct TermRecord
    {
        public Vector2 Position;
        public float Strength;
        public float Softening;
        public float Inner;
        public float Outer;
        public float Reference;
    }

    static readonly int Stride = Marshal.SizeOf<TermRecord>();
    static readonly int WellsId = Shader.PropertyToID("_GravityWells");
    static readonly int CountId = Shader.PropertyToID("_GravityWellCount");
    static readonly int ExaggerationId = Shader.PropertyToID("_HeightExaggeration");
    static TermRecord[] _records = new TermRecord[0];

    /// <summary>
    /// Fills one record per term of the zone's WellTree (a null zone has none) and sets the shader globals. The buffer is
    /// the caller's, (re)allocated to the term count (at least one, so the global is always bound). Terms are lowered at
    /// their node's rail at the drawn time (DrawAhead.Time), the same T the planets and a ship's ride height use.
    /// </summary>
    public static void Lower(Zone zone, float lead, ref ComputeBuffer wells)
    {
        var count = zone?.Wells.Terms.Count ?? 0;
        var capacity = Mathf.Max(count, 1);
        if (wells == null || wells.count != capacity)
        {
            wells?.Release();
            wells = new ComputeBuffer(capacity, Stride);
        }

        if (_records.Length != capacity) _records = new TermRecord[capacity];
        for (var t = 0; t < count; t++)
        {
            var term = zone.Wells.Terms[t];
            var at = term.Node.IsSet() ? DrawAhead.Orbit(zone, term.Node, lead) : zone.Wells.Centre; // an unset node is the zone node, stationary at Centre
            _records[t] = new TermRecord
            {
                Position = new Vector2(at.x, at.y),
                Strength = term.Strength,
                Softening = term.Softening,
                Inner = term.Inner,
                Outer = term.Outer,
                Reference = term.Reference
            };
        }
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
