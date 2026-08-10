using System;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using float2 = Unity.Mathematics.float2;

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ZoneEnvironment
{
    [JsonProperty("nebula"), Key(0)] public NebulaSettings Nebula;
    [JsonProperty("flow"), Key(1)] public FlowSettings Flow;
    [JsonProperty("noise"), Key(2)] public NoiseSettings Noise;
    [JsonProperty("lighting"), Key(3)] public AmbientLightingSettings Lighting;
    [JsonProperty("grid"), Key(4)] public GridSettings Grid;
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class NebulaSettings
{
    [JsonProperty("fillDensity"), Key(0)] public float FillDensity;
    [JsonProperty("fillDistance"), Key(1)] public float FillDistance;
    [JsonProperty("fillExponent"), Key(2)] public float FillExponent;
    [JsonProperty("fillOffset"), Key(12)] public float FillOffset;
    [JsonProperty("cloudDensity"), Key(3)] public float PatchDensity;
    [JsonProperty("fogOffset"), Key(4)] public float FloorOffset;
    [JsonProperty("fogBlend"), Key(5)] public float FloorBlend;
    [JsonProperty("cloudBlend"), Key(6)] public float PatchBlend;
    [JsonProperty("luminance"), Key(7)] public float Luminance;
    [JsonProperty("extinction"), Key(13)] public float Extinction;
    //[JsonProperty("tintExponent"), Key(8)] public float TintExponent;
    [JsonProperty("tintLodExponent"), Key(9)] public float TintLodExponent;
    [JsonProperty("safetyDistance"), Key(10)] public float SafetyDistance;
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GridSettings
{
    [JsonProperty("enabled"), Key(0)] public bool Enabled;
    [JsonProperty("offset"), Key(1)] public float Offset;
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AmbientLightingSettings
{
    [JsonProperty("dynamicSkyBoost"), Key(0)] public float DynamicSkyBoost;
    [JsonProperty("dynamicLodHigh"), Key(1)] public float DynamicLodHigh;
    [JsonProperty("dynamicLodLow"), Key(2)] public float DynamicLodLow;
    [JsonProperty("dynamicIntensity"), Key(3)] public float DynamicIntensity;
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FlowSettings
{
    [JsonProperty("scale"), Key(0)] public float GlobalScale;
    [JsonProperty("amplitudeGlobal"), Key(1)] public float GlobalAmplitude;
    [JsonProperty("scrollSpeed"), Key(2)] public float GlobalScrollSpeed;
    [JsonProperty("period"), Key(3)] public float Period;
    [JsonProperty("amplitudeSlope"), Key(4)] public float SlopeAmplitude;
    [JsonProperty("amplitudeSwirl"), Key(5)] public float SwirlAmplitude;
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class NoiseSettings
{
    [JsonProperty("scale"), Key(0)] public float Scale;
    [JsonProperty("amplitude"), Key(1)] public float Amplitude;
    [JsonProperty("exponent"), Key(2)] public float Exponent;
    [JsonProperty("speed"), Key(3)] public float Speed;
    [JsonProperty("slopeExponent"), Key(4)] public float SlopeExponent;
}

[Union(0, typeof(PowerBrush)), 
 Union(1, typeof(SimplexBrush)), 
 Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public abstract class Brush
{
    [JsonProperty("layerMask"), Key(0)] public BrushLayer LayerMask;
    [JsonProperty("cutoff"), Key(1)] public float Cutoff;
    [JsonProperty("depth"), Key(2)] public float Depth;
    [JsonProperty("envelopeExponent"), Key(3)] public float EnvelopeExponent;
    
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

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PowerBrush : Brush
{
    protected override float Evaluate(float2 world, float2 uv)
    {
        return 1;
    }
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public abstract class TextureBrush : Brush
{
    [JsonProperty("frequency"), Key(4)] public float2 Frequency;
    [JsonProperty("phase"), Key(5)] public float2 Phase;
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public abstract class AnimatedBrush : TextureBrush
{
    [JsonProperty("animationSpeed"), Key(6)] public float AnimationSpeed;
    
    [IgnoreMember]
    public float Time { get; set; }
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class SimplexBrush : TextureBrush
{
    [JsonProperty("absolute"), Key(6)] public bool AbsoluteValue;
    
    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = snoise(world * Frequency + Phase);
        return AbsoluteValue ? abs(noise) : noise;
    }
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AnimatedSimplexBrush : AnimatedBrush
{
    [JsonProperty("absolute"), Key(7)] public bool AbsoluteValue;
    
    protected override float Evaluate(float2 world, float2 uv)
    {
        var noise = snoise(float3(float2(world * Frequency + Phase), AnimationSpeed * Time));
        return AbsoluteValue ? abs(noise) : noise;
    }
}

[Serializable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class RadialWaveBrush : TextureBrush
{
    [JsonProperty("waveExponent"), Key(7)] public float WaveExponent;
    
    protected override float Evaluate(float2 world, float2 uv)
    {
        float dist = length(uv);
        float ang = atan2(uv.y,uv.x);
        return cos((ang + Phase.x) * Frequency.x * PI + (pow(dist, WaveExponent) + Phase.y) * Frequency.y);
    }
}
