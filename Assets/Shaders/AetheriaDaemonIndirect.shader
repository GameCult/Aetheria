Shader "Aetheria/Daemon Indirect"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            half4 _BaseColor;
            StructuredBuffer<float4x4> _AetheriaObjectToWorld;
            int _AetheriaInstanceCount;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                const uint instanceCount = (uint)max(1, _AetheriaInstanceCount);
                const uint instanceId = min(input.instanceId, instanceCount - 1);
                const float4x4 objectToWorld = _AetheriaObjectToWorld[instanceId];
                const float3 worldPosition = mul(objectToWorld, float4(input.positionOS, 1.0)).xyz;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            StructuredBuffer<float4x4> _AetheriaObjectToWorld;
            int _AetheriaInstanceCount;

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceId : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                const uint instanceCount = (uint)max(1, _AetheriaInstanceCount);
                const uint instanceId = min(input.instanceId, instanceCount - 1);
                const float4x4 objectToWorld = _AetheriaObjectToWorld[instanceId];
                const float3 worldPosition = mul(objectToWorld, float4(input.vertex, 1.0)).xyz;

                output.vertex = mul(UNITY_MATRIX_VP, float4(worldPosition, 1.0));
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return tex2D(_BaseMap, input.uv) * _BaseColor;
            }
            ENDCG
        }
    }
}
