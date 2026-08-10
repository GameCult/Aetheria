Shader "Brushes/Power Brush" {
Properties {
	_Color ("Color", Color) = (1,1,1,1)
	_Depth ("Depth", Float) = 0.5
	_Power("Power", Float) = 2
	_Cutoff("Cutoff", Range(0,1)) = 1
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

			fixed4 _Color;
			float _Depth;
			half _Power;
			half _Cutoff;
			
			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};
			
			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				o.texcoord = v.texcoord;
				return o;
			}

			float powerPulse( float x, float power )
			{
				x = saturate(abs(x))-.001;
				return pow((x + 1.0f) * (1.0f - x), power);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float dist = length(i.texcoord-float2(.5,.5))*2;
				return _Depth * min(_Cutoff, powerPulse(dist,_Power)) * _Color * smoothstep(1, .95, dist);
			}
			ENDCG 
		}
	}	
}
}
