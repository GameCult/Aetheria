Shader "Aetheria/Stardust (Compute)"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        _DistanceSizeExponent ("Distance Size Exponent", Float) = 1
        _DistanceIntensityExponent ("Distance Intensity Exponent", Float) = 1
        _EmissionGain ("Emission Gain", Range(0, 1)) = 0.3
        _Power ("Power", Float) = 2
        [HideInInspector] _DitheringTex ("Dithering Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Stardust"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 position;
                float3 color;
                float size;
            };

            StructuredBuffer<Particle> particles;
            StructuredBuffer<float3> quadPoints;

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _EmissionGain;
                float _Power;
                float _DistanceSizeExponent;
                float _DistanceIntensityExponent;
            CBUFFER_END

            TEXTURE2D(_DitheringTex);
            SAMPLER(sampler_DitheringTex);
            float4 _DitheringCoords;
            int _FrameNumber;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings Vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Varyings output;

                float3 worldPosition = particles[instanceId].position;
                float dist = max(length(worldPosition - _WorldSpaceCameraPos.xyz), 0.001);
                float4 viewPosition = mul(UNITY_MATRIX_V, float4(worldPosition, 1.0));
                float3 quadPoint = float3(quadPoints[vertexId].xy, 0.0)
                    * particles[instanceId].size
                    * pow(dist, _DistanceSizeExponent)
                    / pow(100.0, _DistanceSizeExponent);

                output.positionCS = mul(UNITY_MATRIX_P, viewPosition + float4(quadPoint, 0.0));
                output.uv = quadPoints[vertexId].xy + 0.5;
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.color = float4(
                    particles[instanceId].color * _TintColor.rgb,
                    100.0 * pow(100.0, _DistanceIntensityExponent) / pow(dist, _DistanceIntensityExponent));

                return output;
            }

            float PowerPulse(float x, float power)
            {
                x = saturate(abs(x));
                return pow((x + 1.0) * (1.0 - x), power);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float emission = PowerPulse(length(input.uv - float2(0.5, 0.5)) * 2.0, _Power);
                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                half dither = frac(SAMPLE_TEXTURE2D(
                    _DitheringTex,
                    sampler_DitheringTex,
                    screenUv * _DitheringCoords.xy).r + _FrameNumber * 1.61803398875);
                float alpha = emission * input.color.a;
                clip(alpha - dither - 0.001 * (1.0 - ceil(alpha)));
                return input.color * emission * exp(_EmissionGain * 5.0);
            }
            ENDHLSL
        }
    }
}
