/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

Shader "Unlit/GalaxyMap"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		[HDR]_CloudColor ("Cloud Color", Color) = (1,1,1,1)
		CloudAmplitude("Cloud Amplitude", Float) = 1
		CloudExponent ("Cloud Exponent", Float) = 2
		_GlowColor ("GlowColor", Color) = (1,1,1,1)
		NoisePosition("NoisePosition", Float) = 0.5
		NoiseAmplitude("NoiseAmplitude", Float) = 0.5
		NoiseOffset("NoiseOffset", Float) = 0.5
		NoiseGain("NoiseGain", Float) = 1
		NoiseLacunarity("NoiseLacunarity", Float) = 2
		NoiseFrequency("NoiseFrequency", Float) = 1
		Extents("Extents", Vector) = (0,0,1,1)
		StarBoost("StarBoost", Float) = 0
		StarExponent("StarExponent", Float) = 1
	}
	SubShader
	{
	    //Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Tags { "RenderType"="Opaque" }
		LOD 100
        //Blend SrcAlpha OneMinusSrcAlpha
	    Lighting Off ZWrite Off

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
				float2 uv2 : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
		    fixed4 _CloudColor;
		    fixed4 _GlowColor;

			float CloudExponent;
	        float CloudAmplitude;
            float NoisePosition;
            float NoiseAmplitude;
            float NoiseOffset;
	        float NoiseGain;
	        float NoiseLacunarity;
	        float NoiseFrequency;
	        float StarBoost;
	        float StarExponent;
			float4 Extents;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				float2 uv = Extents.xy + v.uv * (Extents.zw-Extents.xy);
				o.uv = uv;
				o.uv2 = TRANSFORM_TEX(uv, _MainTex);
				return o;
			}
			
            // fractal sum, range -1.0 - 1.0
            float fBm(float2 p, int octaves)
            {
                float freq = NoiseFrequency, amp = .5;
                float sum = 0;	
                for(int i = 0; i < octaves; i++) 
                {
                	if(i<4)
						sum += (1-abs(cultmath_snoise(p * freq))) * amp;
                	else sum += abs(cultmath_snoise(p * freq)) * amp;
                    freq *= NoiseLacunarity;
                    amp *= NoiseGain;
                }
                return (sum + NoiseOffset)*NoiseAmplitude;
            }
			
			fixed4 frag (v2f i) : SV_Target
			{
			    float noise = fBm(i.uv + NoisePosition.xx, 10);
			    float gal = pow(noise, CloudExponent) * CloudAmplitude;
			    float stars = tex2D(_MainTex, i.uv2) * pow(noise, StarExponent) * StarBoost;
				return fixed4(max(gal*_CloudColor.rgb - _GlowColor.rgb, 0) + stars.xxx, gal);
			}
			ENDCG
		}
	}
}
