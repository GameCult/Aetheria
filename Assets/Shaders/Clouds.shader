Shader "Unlit/Clouds"
{
    Properties
    {
		_Color ("Color", Color) = (1,1,1,1)
		NoisePosition("NoisePosition", Float) = 0.5
		NoiseAmplitude("NoiseAmplitude", Float) = 0.5
		NoiseOffset("NoiseOffset", Float) = 0.5
		NoiseGain("NoiseGain", Float) = 1
		NoiseLacunarity("NoiseLacunarity", Float) = 2
		NoiseFrequency("NoiseFrequency", Float) = 1
		ParallaxLacunarity("ParallaxLacunarity", Float) = 2
		ParallaxDirection("ParallaxDirection", Vector) = (0,0,0,0)
		Parallax("Parallax", Float) = 1
		Speed("Speed", Float) = 1
		FadeDistance("FadeDistance", Float) = 100
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
	    Cull Off Lighting Off ZWrite Off
        Blend One One
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
			#include "Packages/org.gamecult.cultmath/Shaders/CultMath.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            half4 _Color;
            float NoisePosition;
            float NoiseAmplitude;
            float NoiseOffset;
	        float NoiseGain;
	        float NoiseLacunarity;
	        float NoiseFrequency;
	        float ParallaxLacunarity;
	        float3 ParallaxDirection;
	        float Parallax;
	        float Speed;
	        float FadeDistance;
	        
            // fractal sum, range -1.0 - 1.0
            float fbm(float3 p)
            {
                float freq = NoiseFrequency, amp = .5;
                float parallax = Parallax;
                float sum = 0;	
                for(int i = 0; i < 6; i++) 
                {
                    sum += cultmath_snoise(p * freq + Parallax * ParallaxDirection) * amp;
                    parallax /= ParallaxLacunarity;
                    freq *= NoiseLacunarity;
                    amp *= NoiseGain;
                }
                return (sum + NoiseOffset)*NoiseAmplitude;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = mul (unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 p = float3(i.uv,_Time.x*Speed);
                float noise = saturate((1-abs(cultmath_snoise(p*NoiseFrequency/2))) * fbm(p));
                return noise * _Color * (1-length(i.uv)/FadeDistance);
            }
            ENDCG
        }
    }
}
