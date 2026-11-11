// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Brushes/Simplex Brush"
{
Properties {
	_Depth ("Depth", Float) = 0.5
	_Power("Envelope Power", Float) = 1.25
	_Frequency("Frequency", Float) = 2
	_Speed("Speed", Float) = 1
	_Constant ("Constant", Float) = 0.5
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
	Blend One One
	ColorMask RGB
	Cull Off Lighting Off ZWrite Off
	
	SubShader {
		Pass {
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#include "UnityCG.cginc"
			#include "Packages/org.gamecult.cultmath/Shaders/CultMath.hlsl"

			float _Depth;
			float _Power;
			float _Frequency;
			float _Speed;
			float _Constant;
			
			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float2 texcoord1 : TEXCOORD1;
			};
			
			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				o.texcoord = v.texcoord;
				o.texcoord1 = mul(unity_ObjectToWorld, v.vertex).xz;
				return o;
			}
			

			float powerPulse( float x, float power )
			{
				x = saturate(abs(x));
				return pow((x + 1.0f) * (1.0f - x), power);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float dist = length(i.texcoord - float2(.5,.5));
				return powerPulse(dist * 2,_Power) * (_Depth * abs(cultmath_snoise(float3(i.texcoord1.xy*_Frequency,_Time.y*_Speed)))+_Constant);
			}
			ENDCG 
		}
	}	
}
}
