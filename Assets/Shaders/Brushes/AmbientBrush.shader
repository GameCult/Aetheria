Shader "Brushes/Ambient Brush" {
Properties {
	_Color ("Color", Color) = (1,1,1,1)
	_Depth ("Depth", Float) = 0.5
	// The brightness at the populated edge relative to the centre: the shape of the fall, never an extent.
	_EdgeBrightness ("Edge Brightness", Range(0.0001, 0.9999)) = .12
	// The quad's radius in units of the populated radius. Written by ZoneRenderer.FitAmbientLight, never authored.
	_Reach ("Reach", Float) = 1
}

// A gaussian: log-brightness is quadratic in distance from the centre, so the fall is smooth everywhere and
// never reaches an edge or a cliff. x is the distance in units of the zone's populated radius.
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

			float4 _Color;
			float _Depth;
			float _EdgeBrightness;
			float _Reach;

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float x = length(i.texcoord - float2(.5, .5)) * 2 * _Reach;
				return _Depth * _Color * exp(-x * x * log(1 / _EdgeBrightness));
			}
			ENDCG
		}
	}
}
}
