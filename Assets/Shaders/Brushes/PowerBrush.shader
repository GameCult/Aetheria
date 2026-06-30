Shader "Brushes/Power Brush"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Depth ("Depth", Float) = 0.5
        _Power ("Power", Float) = 2
        _Cutoff ("Cutoff", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PowerBrush"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ColorMask RGB
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Depth;
                half _Power;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float PowerPulse(float x, float power)
            {
                x = saturate(abs(x)) - 0.001;
                return pow((x + 1.0) * (1.0 - x), power);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float dist = length(input.uv - float2(0.5, 0.5)) * 2.0;
                return _Depth * min(_Cutoff, PowerPulse(dist, _Power)) * _Color * smoothstep(1.0, 0.95, dist);
            }
            ENDHLSL
        }
    }
}
