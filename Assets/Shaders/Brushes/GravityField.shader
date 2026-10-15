// Draws the zone's gravity: depth = -Zone.GetHeight at the world xz of every texel, into whichever gravity camera culls
// layer 9 (Surface Displacement, Slime Gravity, both Map Gravity). One quad covers the zone; the
// law lives in Gravity.cginc, which mirrors ServerShared's GravityField.
Shader "Brushes/Gravity Field" {
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
			#pragma target 4.5

			#include "UnityCG.cginc"
			#include "../Gravity.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 world : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.world = mul(unity_ObjectToWorld, v.vertex).xz;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				return (-gravityHeight(i.world)).xxxx;
			}
			ENDCG
		}
	}
}
}
