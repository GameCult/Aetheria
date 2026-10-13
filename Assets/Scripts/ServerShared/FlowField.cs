using CultMath;
using static CultMath.math;

/// <summary>
/// The global flow law: the one definition of the drift that stardust and nebula follow and that the sim knows.
/// A pure function of the zone's FlowSettings, a position, a scroll and a downhill gradient; nothing here keeps
/// a clock. Zone.Flow evaluates it at zone time. Assets/Shaders/Volumetric.cginc (globalFlow, slopeFlow) is its
/// HLSL lowering, driven by VolumeSampling.LowerFlow and pinned to it by Aetheria.EditorTools.FlowFieldParityVerify.
/// </summary>
public static class FlowField
{
    // The scroll offset of the noise field, in field units: speed times zone time and nothing else.
    public static float Scroll(FlowSettings settings, double zoneTime) => (float) (settings.GlobalScrollSpeed * zoneTime);

    // The procedural term (Volumetric.cginc globalFlow): a Tri3D field of position / scale, scrolled along y.
    // Skipped when its amplitude is zero, as the shader's FLOW_GLOBAL keyword skips it.
    public static float3 Global(FlowSettings settings, float3 position, float scroll)
    {
        if (settings.GlobalAmplitude == 0f) return float3.zero;
        var sample = Tri3D(position / settings.GlobalScale - float3(0f, scroll, 0f)) * settings.GlobalAmplitude;
        sample.y *= .5f;
        return sample;
    }

    // The slope term (Volumetric.cginc slopeFlow): swirl around and slide along the gravity gradient.
    // `downhill` is the DOWNHILL gradient, -grad(height) in world xz. The shader's texel gradient is that same
    // vector (getUV negates the world x and z axes), and Zone.GetNormal's xz is proportional to it.
    // Skipped when both amplitudes are zero (the FLOW_SLOPE keyword), and zero for a zero gradient.
    public static float3 Slope(FlowSettings settings, float2 downhill)
    {
        if (settings.SlopeAmplitude == 0f && settings.SwirlAmplitude == 0f) return float3.zero;
        if (dot(downhill, downhill) == 0f) return float3.zero;
        var direction = normalize(downhill);
        return float3(direction.y, 0f, -direction.x) * settings.SwirlAmplitude
             + float3(direction.x, 0f, direction.y) * settings.SlopeAmplitude;
    }

    public static float3 At(FlowSettings settings, float3 position, double zoneTime, float2 downhill) =>
        Global(settings, position, Scroll(settings, zoneTime)) + Slope(settings, downhill);

    // Volumetric.cginc tri / tri3 / Tri3D, term for term.
    static float Tri(float x) => abs(frac(x) - .5f);

    static float3 Tri3(float3 p) =>
        float3(Tri(p.z + Tri(p.y)), Tri(p.z + Tri(p.x)), Tri(p.y + Tri(p.x)));

    static float3 Tri3D(float3 p)
    {
        var t1 = normalize(Tri3(p));
        var t2 = normalize(Tri3(p * 1.61803398875f));
        return cross(t1, t2);
    }
}
