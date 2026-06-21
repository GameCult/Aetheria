using System;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
using unitynoise = Unity.Mathematics.noise;

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
        x = Saturate(MathF.Abs(x))-.001f;
        return MathF.Pow((x + 1.0f) * (1.0f - x), power);
    }

    protected abstract float Evaluate(float2 world, float2 uv);

    public float Evaluate(float2 world, float2 pos, float2 radius)
    {
        var uv = (world - pos) / radius;
        float dist = Length(uv)*2;
        float envelope = MathF.Min(Cutoff, powerPulse(dist,EnvelopeExponent)) * SmoothStep(1, .95f, dist);
        return Depth * Evaluate(world, uv) * envelope;
    }

    private static float Length(float2 value)
    {
        return MathF.Sqrt(value.x * value.x + value.y * value.y);
    }

    private static float Saturate(float value)
    {
        return value < 0 ? 0 : value > 1 ? 1 : value;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Saturate((x - edge0) / (edge1 - edge0));
        return t * t * (3.0f - 2.0f * t);
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
        var noise = unitynoise.snoise(world * Frequency + Phase);
        return AbsoluteValue ? MathF.Abs(noise) : noise;
    }
}

[Serializable]
public class AnimatedSimplexBrush : AnimatedBrush
{
    public bool AbsoluteValue;

    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = unitynoise.snoise(new float3(world * Frequency + Phase, AnimationSpeed * Time));
        return AbsoluteValue ? MathF.Abs(noise) : noise;
    }
}

[Serializable]
public class RadialWaveBrush : TextureBrush
{
    public float WaveExponent;

    protected override float Evaluate(float2 world, float2 uv)
    {
        float dist = MathF.Sqrt(uv.x * uv.x + uv.y * uv.y);
        float ang = MathF.Atan2(uv.y,uv.x);
        return MathF.Cos((ang + Phase.x) * Frequency.x * MathF.PI + (MathF.Pow(dist, WaveExponent) + Phase.y) * Frequency.y);
    }
}
