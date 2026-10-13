using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

/// <summary>
/// The global flow law: the one definition of the drift that stardust and nebula follow and that the sim knows.
/// A pure function of the zone's FlowSettings, a position, a scroll and a downhill gradient; nothing here keeps
/// a clock. Zone.Flow evaluates it at zone time. Assets/Shaders/Volumetric.cginc (globalFlow, slopeFlow) is its
/// HLSL lowering, driven by VolumeSampling.LowerFlow and pinned to it by Aetheria.EditorTools.FlowFieldParityVerify.
/// </summary>
public static class FlowField
{
    // The second octave of the noise samples at position * Golden (Volumetric.cginc Tri3D).
    const float Golden = 1.61803398875f;

    // The scroll of the noise field, in field units: speed times zone time, for each of the two octaves, as a phase.
    // Every term of Tri3D reaches the scrolled axis through frac, so a phase is only meaningful modulo 1: the fold
    // happens here in double, where zone time is exact, and the float that reaches the shaders is in [0, 1) however
    // old the zone is. x is the first octave's phase, y the second's (the second octave's axis is scaled by Golden).
    public static float2 Scroll(FlowSettings settings, double zoneTime)
    {
        var scroll = (double) settings.GlobalScrollSpeed * zoneTime;
        return float2((float) frac(scroll), (float) frac(scroll * Golden));
    }

    // The phase of a cycle of `period` seconds at zone time, in [0, 1). The nebula's flow-advected noise cycles and
    // Stardust's particle lifetimes take their phase from here, so drift follows sim time and stops with the sim.
    public static float Cycle(double zoneTime, float period) => period == 0f ? 0f : (float) frac(zoneTime / period);

    // The procedural term (Volumetric.cginc globalFlow): a Tri3D field of position / scale, scrolled along y.
    // Skipped when its amplitude is zero, as the shader's FLOW_GLOBAL keyword skips it.
    public static float3 Global(FlowSettings settings, float3 position, float2 scroll)
    {
        if (settings.GlobalAmplitude == 0f) return float3.zero;
        var field = position / settings.GlobalScale;
        var sample = Tri3D(field - float3(0f, scroll.x, 0f), field * Golden - float3(0f, scroll.y, 0f)) * settings.GlobalAmplitude;
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

    public static float3 At(FlowSettings settings, float3 position, float2 scroll, float2 downhill) =>
        Global(settings, position, scroll) + Slope(settings, downhill);

    // Volumetric.cginc tri / tri3 / Tri3D, term for term.
    static float Tri(float x) => abs(frac(x) - .5f);

    static float3 Tri3(float3 p) =>
        float3(Tri(p.z + Tri(p.y)), Tri(p.z + Tri(p.x)), Tri(p.y + Tri(p.x)));

    // The two octaves' positions are given already scrolled (see Scroll).
    static float3 Tri3D(float3 p, float3 pGolden)
    {
        var t1 = normalize(Tri3(p));
        var t2 = normalize(Tri3(pGolden));
        return cross(t1, t2);
    }
}
