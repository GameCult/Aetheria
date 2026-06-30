Shader "Aetheria/Volume Sun"
{
    Properties
    {
        _ColorRamp ("Color Ramp", 2D) = "white" {}
        _Offset ("Offset", CUBE) = "gray" {}
        _Albedo ("Albedo", CUBE) = "black" {}
        _RayStepSize ("Ray Step Size", Float) = 0.01
        _FirstOffsetDistance ("First Offset Distance", Float) = 0.01
        _FirstOffsetDepthExponent ("First Offset Depth Exponent", Float) = 1
        _SecondOffsetDistance ("Second Offset Distance", Float) = 0.01
        _SecondOffsetDepthExponent ("Second Offset Depth Exponent", Float) = 1
        _DensityDepthExponent ("Density Depth Exponent", Float) = 1
        _DensityAlbedoExponent ("Density Albedo Exponent", Float) = 1
        _NoiseFrequency ("Noise Frequency", Float) = 1
        _NoiseAmplitude ("Noise Amplitude", Float) = 1
        _NoiseSpeed ("Noise Speed", Float) = 1
        _Emission ("Emission", Float) = 1
        _Alpha ("Alpha Multiplier", Float) = 1
        _Glossiness ("Gloss", Range(0,1)) = 1
        _Metallic ("Metallic", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VolumeSun"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ColorRamp);
            SAMPLER(sampler_ColorRamp);
            TEXTURECUBE(_Offset);
            SAMPLER(sampler_Offset);
            TEXTURECUBE(_Albedo);
            SAMPLER(sampler_Albedo);

            CBUFFER_START(UnityPerMaterial)
                float _RayStepSize;
                float _FirstOffsetDistance;
                float _SecondOffsetDistance;
                float _FirstOffsetDepthExponent;
                float _SecondOffsetDepthExponent;
                float _LimbDarkening;
                float _DensityDepthExponent;
                float _DensityAlbedoExponent;
                float _Alpha;
                float _Emission;
                float4x4 _AlbedoRotation;
                float4x4 _FirstOffsetDomainRotation;
                float4x4 _FirstOffsetRotation;
                float4x4 _SecondOffsetDomainRotation;
                float4x4 _SecondOffsetRotation;
                float4 _LightingDirection;
                half _NoiseFrequency;
                half _NoiseAmplitude;
                half _NoiseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                return output;
            }

            float Tri(float x)
            {
                return abs(frac(x) - 0.5);
            }

            float3 Tri3(float3 p)
            {
                return float3(
                    Tri(p.z + Tri(p.y)),
                    Tri(p.z + Tri(p.x)),
                    Tri(p.y + Tri(p.x)));
            }

            float TriNoise3d(float3 p, float speed)
            {
                float z = 1.4;
                float result = 0.0;
                float3 bp = p;

                UNITY_UNROLL
                for (int i = 0; i <= 2; i++)
                {
                    float3 dg = Tri3(bp * 2.0);
                    p += dg + _Time.y * speed;
                    bp *= 1.8;
                    z *= 1.5;
                    p *= 1.2;
                    result += Tri(p.z + Tri(p.x + Tri(p.y))) / z;
                    bp += 0.14;
                }

                return result;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float rim = saturate(dot(input.viewDirWS, input.normalWS));
                float3 rayPos = input.normalWS;
                float3 rayStep = normalize(input.viewDirWS) * _RayStepSize * (2.0 - rim);
                float3 accum = 0.0;
                float alphaAccum = 0.0;

                UNITY_LOOP
                for (int i = 0; i < 32; i++)
                {
                    float elevation = length(rayPos);
                    float depth = 1.0 - elevation;
                    float turbulence = 1.25 - abs(rayPos.y) * 0.5;

                    float3 firstSamplePosition = mul((float3x3)_FirstOffsetDomainRotation, rayPos);
                    float3 offset = mul(
                        (float3x3)_FirstOffsetRotation,
                        normalize(SAMPLE_TEXTURECUBE_LOD(_Offset, sampler_Offset, firstSamplePosition, i / 8.0).rgb - float3(0.5, 0.5, 0.5)))
                        * _FirstOffsetDistance
                        * pow(max(elevation, 0.0001), _FirstOffsetDepthExponent)
                        * turbulence;

                    float3 secondSamplePosition = mul((float3x3)_SecondOffsetDomainRotation, rayPos + offset);
                    float3 offset2 = mul(
                        (float3x3)_SecondOffsetRotation,
                        normalize(SAMPLE_TEXTURECUBE_LOD(_Offset, sampler_Offset, secondSamplePosition, i / 16.0).rgb - float3(0.5, 0.5, 0.5)))
                        * _SecondOffsetDistance
                        * pow(max(elevation, 0.0001), _SecondOffsetDepthExponent)
                        * turbulence;

                    float3 albedoDirection = normalize(mul((float3x3)_AlbedoRotation, firstSamplePosition + offset2));
                    float albedo = SAMPLE_TEXTURECUBE_LOD(_Albedo, sampler_Albedo, albedoDirection, i / 8.0).r;
                    float noise = max(1.0 - TriNoise3d(secondSamplePosition * _NoiseFrequency, _NoiseSpeed) * _NoiseAmplitude, 0.01);
                    float density = pow(max(depth, 0.0), _DensityDepthExponent) * pow(max(albedo, 0.0), _DensityAlbedoExponent);

                    alphaAccum += density;
                    accum += SAMPLE_TEXTURE2D(_ColorRamp, sampler_ColorRamp, albedo.xx * noise).rgb * density * noise;
                    rayPos += rayStep;
                }

                clip(alphaAccum * _Alpha - 0.01);
                return float4(accum * _Emission, 1.0);
            }
            ENDHLSL
        }
    }
}
