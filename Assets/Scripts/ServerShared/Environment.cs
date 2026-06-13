using System;
using MessagePack;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using float2 = Unity.Mathematics.float2;

[Serializable, MessagePackObject]
public class ZoneEnvironment
{
    [Key(0)] public NebulaSettings Nebula;
    [Key(1)] public FlowSettings Flow;
    [Key(2)] public NoiseSettings Noise;
    [Key(3)] public AmbientLightingSettings Lighting;
    [Key(4)] public GridSettings Grid;
}

[Serializable, MessagePackObject]
public class NebulaSettings
{
    [Key(0)] public float FillDensity;
    [Key(1)] public float FillDistance;
    [Key(2)] public float FillExponent;
    [Key(12)] public float FillOffset;
    [Key(3)] public float PatchDensity;
    [Key(4)] public float FloorOffset;
    [Key(5)] public float FloorBlend;
    [Key(6)] public float PatchBlend;
    [Key(7)] public float Luminance;
    [Key(13)] public float Extinction;
    //[Key(8)] public float TintExponent;
    [Key(9)] public float TintLodExponent;
    [Key(10)] public float SafetyDistance;
}

[Serializable, MessagePackObject]
public class GridSettings
{
    [Key(0)] public bool Enabled;
    [Key(1)] public float Offset;
}

[Serializable, MessagePackObject]
public class AmbientLightingSettings
{
    [Key(0)] public float DynamicSkyBoost;
    [Key(1)] public float DynamicLodHigh;
    [Key(2)] public float DynamicLodLow;
    [Key(3)] public float DynamicIntensity;
}

[Serializable, MessagePackObject]
public class FlowSettings
{
    [Key(0)] public float GlobalScale;
    [Key(1)] public float GlobalAmplitude;
    [Key(2)] public float GlobalScrollSpeed;
    [Key(3)] public float Period;
    [Key(4)] public float SlopeAmplitude;
    [Key(5)] public float SwirlAmplitude;
}

[Serializable, MessagePackObject]
public class NoiseSettings
{
    [Key(0)] public float Scale;
    [Key(1)] public float Amplitude;
    [Key(2)] public float Exponent;
    [Key(3)] public float Speed;
    [Key(4)] public float SlopeExponent;
}

[Union(0, typeof(PowerBrush)),
 Union(1, typeof(SimplexBrush)),
 Serializable, MessagePackObject]
public abstract class Brush
{
    [Key(0)] public BrushLayer LayerMask;
    [Key(1)] public float Cutoff;
    [Key(2)] public float Depth;
    [Key(3)] public float EnvelopeExponent;

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

[Serializable, MessagePackObject]
public class PowerBrush : Brush
{
    protected override float Evaluate(float2 world, float2 uv)
    {
        return 1;
    }
}

[Serializable, MessagePackObject]
public abstract class TextureBrush : Brush
{
    [Key(4)] public float2 Frequency;
    [Key(5)] public float2 Phase;
}

[Serializable, MessagePackObject]
public abstract class AnimatedBrush : TextureBrush
{
    [Key(6)] public float AnimationSpeed;

    [IgnoreMember]
    public float Time { get; set; }
}

[Serializable, MessagePackObject]
public class SimplexBrush : TextureBrush
{
    [Key(6)] public bool AbsoluteValue;

    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = snoise(world * Frequency + Phase);
        return AbsoluteValue ? abs(noise) : noise;
    }
}

[Serializable, MessagePackObject]
public class AnimatedSimplexBrush : AnimatedBrush
{
    [Key(7)] public bool AbsoluteValue;

    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = snoise(float3(float2(world * Frequency + Phase), AnimationSpeed * Time));
        return AbsoluteValue ? abs(noise) : noise;
    }
}

[Serializable, MessagePackObject]
public class RadialWaveBrush : TextureBrush
{
    [Key(7)] public float WaveExponent;

    protected override float Evaluate(float2 world, float2 uv)
    {
        float dist = length(uv);
        float ang = atan2(uv.y,uv.x);
        return cos((ang + Phase.x) * Frequency.x * PI + (pow(dist, WaveExponent) + Phase.y) * Frequency.y);
    }
}
