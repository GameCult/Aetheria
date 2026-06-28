#ifndef AETHERIA_RENDER_SPLATS_CORE_INCLUDED
#define AETHERIA_RENDER_SPLATS_CORE_INCLUDED

struct AetheriaSplat
{
    float4 centerHalfExtent;
    float4 rotationChannelFalloff;
    float4 layerSource;
    float4 sourceFrequencyPhase;
    float4 value;
};

StructuredBuffer<AetheriaSplat> _AetheriaSplats;
float4x4 _AetheriaViewportToClip;
int _AetheriaSplatCount;
int _AetheriaChannelFilter;
float _ValueScale;

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 2 - 1;
}

struct Varyings
{
    float4 position : SV_POSITION;
    float2 localUv : TEXCOORD0;
    nointerpolation uint instanceId : TEXCOORD1;
};

Varyings Vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    static const float2 corners[6] =
    {
        float2(-1, -1),
        float2(1, -1),
        float2(1, 1),
        float2(-1, -1),
        float2(1, 1),
        float2(-1, 1)
    };

    AetheriaSplat splat = _AetheriaSplats[instanceId];
    float2 local = corners[vertexId];
    float2 scaled = local * splat.centerHalfExtent.zw;
    float c = splat.rotationChannelFalloff.x;
    float s = splat.rotationChannelFalloff.y;
    float2 world = splat.centerHalfExtent.xy + float2(
        scaled.x * c - scaled.y * s,
        scaled.x * s + scaled.y * c);

    Varyings output;
    output.position = mul(_AetheriaViewportToClip, float4(world, 0, 1));
    output.localUv = local;
    output.instanceId = instanceId;
    return output;
}

float ResolveFalloff(float2 localUv, int falloff)
{
    float distance01 = saturate(length(localUv));
    if (falloff == 0)
        return 1;
    if (falloff == 1)
        return saturate(1 - distance01);
    if (falloff == 3)
    {
        float t = smoothstep(0, 1, distance01);
        return t;
    }

    return 1 - smoothstep(0, 1, distance01);
}

float4 Frag(Varyings input) : SV_Target
{
    AetheriaSplat splat = _AetheriaSplats[input.instanceId];
    int channel = (int)round(splat.rotationChannelFalloff.z);
    if (_AetheriaChannelFilter >= 0 && channel != _AetheriaChannelFilter)
        discard;

    int falloff = (int)round(splat.rotationChannelFalloff.w);
    float alpha = ResolveFalloff(input.localUv, falloff);
    clip(alpha - 0.0001);

    int sourceKind = (int)round(splat.layerSource.y);
    float source = 1;
    if (sourceKind == 1 || sourceKind == 2)
    {
        float2 frequency = splat.sourceFrequencyPhase.xy;
        float2 phase = splat.sourceFrequencyPhase.zw;
        float timeOffset = sourceKind == 2 ? _Time.y * splat.layerSource.z : 0;
        source = valueNoise(input.localUv * frequency + phase + timeOffset);
        if (splat.layerSource.w != 0)
            source = abs(source);
    }

    return splat.value * (alpha * source * _ValueScale);
}

#endif
