#ifndef AETHERIA_FOG_BLUR_URP_INCLUDED
#define AETHERIA_FOG_BLUR_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#include "DiskKernels.hlsl"
#include "Assets/Shaders/PackFloat.cginc"

TEXTURE2D(_DoFBlurTex);
float _DoFBlurTexPacked;

TEXTURE2D_X(_CoCTex);
TEXTURE2D_X(_DepthOfFieldTex);

float4 _MainTex_TexelSize;
float4 _DepthOfFieldTex_TexelSize;
float _Distance;
float _LensCoeff;
float _MaxCoC;
float _RcpAspect;

half4 FragCoC(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

    half4 blur = SAMPLE_TEXTURE2D(_DoFBlurTex, sampler_LinearClamp, uv);
    if (_DoFBlurTexPacked > 0.5)
    {
        float distance;
        float density;
        unpack(blur.a, distance, density);

        float farDepth = max(distance - _Distance, 0.0);
        float farCoC = saturate(farDepth * max(_LensCoeff, 0.0025));
        float fogGate = smoothstep(0.02, 0.25, density);
        return 0.5 + farCoC * fogGate * 0.5;
    }

    return saturate(blur.r);
}

half4 FragTempFilter(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(input.texcoord));
}

half4 FragPrefilter(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
    float3 duv = _MainTex_TexelSize.xyx * float3(0.5, 0.5, -0.5);

    float2 uv0 = uv - duv.xy;
    float2 uv1 = uv - duv.zy;
    float2 uv2 = uv + duv.zy;
    float2 uv3 = uv + duv.xy;

    half3 c0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv0, _MainTex_TexelSize.xy)).rgb;
    half3 c1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv1, _MainTex_TexelSize.xy)).rgb;
    half3 c2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv2, _MainTex_TexelSize.xy)).rgb;
    half3 c3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv3, _MainTex_TexelSize.xy)).rgb;

    half coc0 = SAMPLE_TEXTURE2D_X(_CoCTex, sampler_LinearClamp, ClampUVForBilinear(uv0, _MainTex_TexelSize.xy)).r * 2.0 - 1.0;
    half coc1 = SAMPLE_TEXTURE2D_X(_CoCTex, sampler_LinearClamp, ClampUVForBilinear(uv1, _MainTex_TexelSize.xy)).r * 2.0 - 1.0;
    half coc2 = SAMPLE_TEXTURE2D_X(_CoCTex, sampler_LinearClamp, ClampUVForBilinear(uv2, _MainTex_TexelSize.xy)).r * 2.0 - 1.0;
    half coc3 = SAMPLE_TEXTURE2D_X(_CoCTex, sampler_LinearClamp, ClampUVForBilinear(uv3, _MainTex_TexelSize.xy)).r * 2.0 - 1.0;

    half3 avg = (c0 + c1 + c2 + c3) * 0.25;
    half cocMin = min(coc0, Min3(coc1, coc2, coc3));
    half cocMax = max(coc0, Max3(coc1, coc2, coc3));
    half coc = (-cocMin > cocMax ? cocMin : cocMax) * _MaxCoC;

    avg *= smoothstep(0.0, _MainTex_TexelSize.y * 2.0, abs(coc));
    return half4(avg, coc);
}

half4 FragBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
    half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv, _MainTex_TexelSize.xy));

    half4 acc = 0.0;
    UNITY_LOOP
    for (int si = 0; si < kSampleCount; si++)
    {
        float2 disp = kDiskKernel[si] * _MaxCoC;
        float dist = length(disp);
        float2 sampleUv = uv + float2(disp.x * _RcpAspect, disp.y);
        half4 sampleColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(sampleUv, _MainTex_TexelSize.xy));

        half farCoC = max(min(center.a, sampleColor.a), 0.0);
        half nearCoC = max(-sampleColor.a, 0.0);
        half margin = _MainTex_TexelSize.y * 2.0;
        half weight = max(
            saturate((farCoC - dist + margin) / margin),
            saturate((nearCoC - dist + margin) / margin));

        acc += half4(sampleColor.rgb, 1.0) * weight;
    }

    acc.rgb /= max(acc.a, 1e-4);
    acc.a = saturate(acc.a / max((half)kSampleCount, 1.0h));
    return acc;
}

half4 FragPostBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
    float4 duv = _MainTex_TexelSize.xyxy * float4(0.5, 0.5, -0.5, 0.0);

    half4 acc = 0.0;
    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv - duv.xy, _MainTex_TexelSize.xy));
    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv - duv.zy, _MainTex_TexelSize.xy));
    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv + duv.zy, _MainTex_TexelSize.xy));
    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv + duv.xy, _MainTex_TexelSize.xy));
    return acc * 0.25;
}

half4 FragCombine(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

    half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(uv, _MainTex_TexelSize.xy));
    half4 dof = SAMPLE_TEXTURE2D_X(_DepthOfFieldTex, sampler_LinearClamp, ClampUVForBilinear(uv, _DepthOfFieldTex_TexelSize.xy));
    half coc = SAMPLE_TEXTURE2D_X(_CoCTex, sampler_LinearClamp, ClampUVForBilinear(uv, _MainTex_TexelSize.xy)).r;
    coc = (coc - 0.5) * 2.0 * _MaxCoC;

    half farAlpha = smoothstep(_MainTex_TexelSize.y * 2.0, _MainTex_TexelSize.y * 4.0, coc);
    return lerp(color, half4(dof.rgb, color.a), farAlpha);
}

half4 FragDebugOverlay(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
    half coc = SAMPLE_TEXTURE2D_X(_CoCTex, sampler_LinearClamp, ClampUVForBilinear(uv, _MainTex_TexelSize.xy)).r;
    return half4(coc, coc, coc, 1.0);
}

#endif
