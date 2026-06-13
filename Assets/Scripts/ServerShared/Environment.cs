using System;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using float2 = Unity.Mathematics.float2;

[Serializable]
public class ZoneEnvironment
{
    public NebulaSettings Nebula;
    public FlowSettings Flow;
    public NoiseSettings Noise;
    public AmbientLightingSettings Lighting;
    public GridSettings Grid;
}

[Serializable]
public class NebulaSettings
{
    public float FillDensity;
    public float FillDistance;
    public float FillExponent;
    public float FillOffset;
    public float PatchDensity;
    public float FloorOffset;
    public float FloorBlend;
    public float PatchBlend;
    public float Luminance;
    public float Extinction;
    public float TintLodExponent;
    public float SafetyDistance;
}

[Serializable]
public class GridSettings
{
    public bool Enabled;
    public float Offset;
}

[Serializable]
public class AmbientLightingSettings
{
    public float DynamicSkyBoost;
    public float DynamicLodHigh;
    public float DynamicLodLow;
    public float DynamicIntensity;
}

[Serializable]
public class FlowSettings
{
    public float GlobalScale;
    public float GlobalAmplitude;
    public float GlobalScrollSpeed;
    public float Period;
    public float SlopeAmplitude;
    public float SwirlAmplitude;
}

[Serializable]
public class NoiseSettings
{
    public float Scale;
    public float Amplitude;
    public float Exponent;
    public float Speed;
    public float SlopeExponent;
}

[Serializable]
public abstract class Brush
{
    public BrushLayer LayerMask;
    public float Cutoff;
    public float Depth;
    public float EnvelopeExponent;

    float powerPulse( float x, float power )
    {
        x = saturate(abs(x))-.001f;
        return pow((x + 1.0f) * (1.0f - x), power);
    }

    protected abstract float Evaluate(float2 world, float2 uv);

    public float Evaluate(float2 world, float2 pos, float2 radius)
    {
        var uv = (world - pos) / radius;
        float dist = length(uv)*2;
        float envelope = min(Cutoff, powerPulse(dist,EnvelopeExponent)) * smoothstep(1, .95f, dist);
        return Depth * Evaluate(world, uv) * envelope;
    }
}

[Serializable]
public class PowerBrush : Brush
{
    protected override float Evaluate(float2 world, float2 uv)
    {
        return 1;
    }
}

[Serializable]
public abstract class TextureBrush : Brush
{
    public float2 Frequency;
    public float2 Phase;
}

[Serializable]
public abstract class AnimatedBrush : TextureBrush
{
    public float AnimationSpeed;

    public float Time { get; set; }
}

[Serializable]
public class SimplexBrush : TextureBrush
{
    public bool AbsoluteValue;

    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = snoise(world * Frequency + Phase);
        return AbsoluteValue ? abs(noise) : noise;
    }
}

[Serializable]
public class AnimatedSimplexBrush : AnimatedBrush
{
    public bool AbsoluteValue;

    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = snoise(float3(float2(world * Frequency + Phase), AnimationSpeed * Time));
        return AbsoluteValue ? abs(noise) : noise;
    }
}

[Serializable]
public class RadialWaveBrush : TextureBrush
{
    public float WaveExponent;

    protected override float Evaluate(float2 world, float2 uv)
    {
        float dist = length(uv);
        float ang = atan2(uv.y,uv.x);
        return cos((ang + Phase.x) * Frequency.x * PI + (pow(dist, WaveExponent) + Phase.y) * Frequency.y);
    }
}
