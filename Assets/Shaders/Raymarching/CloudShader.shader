// Based on github.com/yangrc1234/VolumeCloud

Shader "Aetheria/CloudShader"
{
	Properties
	{
		_MainTex("MainTex",2D) = "white"{}
	}

		SubShader
		{
			Cull Off ZWrite Off ZTest Always
			Pass
			{
			CGPROGRAM
			#pragma target 5.0
			#pragma multi_compile LOW_QUALITY MEDIUM_QUALITY HIGH_QUALITY ULTRA_QUALITY	//Higher quality uses more samples.
			#pragma vertex vert
			#pragma fragment frag


#if defined(ULTRA_QUALITY)
			#define SAMPLE_COUNT 256
#endif	
#if defined(HIGH_QUALITY)
			#define SAMPLE_COUNT 128
#endif	
#if defined(MEDIUM_QUALITY)
			#define SAMPLE_COUNT 64
#endif	
#if defined(LOW_QUALITY)
			#define SAMPLE_COUNT 32
#endif

			#include_with_pragmas "Assets/Shaders/Volumetric.cginc"
			#include "UnityCG.cginc"
			#include "Assets/Shaders/PackFloat.cginc"
			sampler2D _CameraDepthTexture;
			float _RaymarchOffset;	//raymarch offset by halton sequence, [0,1]
			float4 _ProjectionExtents;
			sampler2D _DitheringTex;
			float4 _DitheringCoords;
			uniform float4x4 _CamInvProj;

			float _ExtinctionCoefficient;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Interpolator {
				float4 vertex : SV_POSITION;
				float4 screenPos : TEXCOORD0;
				float2 vsray : TEXCOORD1;
			};

			Interpolator vert (appdata v)
			{
				Interpolator o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				v.vertex.z = 0.5;
				o.screenPos = ComputeScreenPos(o.vertex);
				o.vsray = (2.0 * v.uv - 1.0) * _ProjectionExtents.xy + _ProjectionExtents.zw;
				return o;
			}

			struct RaymarchStatus {
				float3 intensity;
				float depth;
				float depthweightsum;
				float intTransmittance;
			};

			void InitRaymarchStatus(inout RaymarchStatus result){
				result.intTransmittance = 1.0f;
				result.intensity = 0.0f;
				result.depthweightsum = 0.00001f;
				result.depth = 0.0f;
			}

			void IntegrateRaymarch(float3 startPos, float3 rayPos, float fade, float stepsize, inout RaymarchStatus result){
				float4 c = VolumeSampleColor(rayPos);
				float density = c.a;
				if (density <= 0.0f)
				{
					result.intTransmittance = 0;
					return;
				}
				float extinction = _ExtinctionCoefficient * density / (1-fade);

				float clampedExtinction = max(extinction, 1e-7);
				float transmittance = exp(-extinction * stepsize);
				
				float3 luminance = c.rgb * _NebulaLuminance;
				float3 integScatt = (luminance - luminance * transmittance) / clampedExtinction;
				float depthWeight = result.intTransmittance * (1-transmittance);		//Is it a better idea to use (1-transmittance) * intTransmittance as depth weight?

				result.intensity += result.intTransmittance * integScatt;
				result.depth += depthWeight * length(rayPos - startPos);
				result.depthweightsum += depthWeight;
				result.intTransmittance *= transmittance;
			}
			 
			float GetDensity(float3 startPos, float3 dir, float maxSampleDistance, float raymarchOffset, out float3 intensity,out float depth) {
				float raymarchDistance = 0;
				float totalRaymarchDistance = _ProjectionParams.z - _ProjectionParams.y;

				RaymarchStatus result;
				InitRaymarchStatus(result);

				[loop]
				for (int j = 1; j < SAMPLE_COUNT; j++) {
					float prevRayDist = raymarchDistance;
					raymarchDistance = _ProjectionParams.y + pow((j+raymarchOffset)/SAMPLE_COUNT,2) * totalRaymarchDistance;
					if(raymarchDistance > maxSampleDistance) break;
					float step = raymarchDistance - prevRayDist;
					float3 rayPos = startPos + dir * raymarchDistance;
					float fade = smoothstep(_ProjectionParams.z*.8,_ProjectionParams.z, raymarchDistance);
					IntegrateRaymarch(startPos, rayPos, fade, step, result);
					if (result.intTransmittance < 0.01f) {
						result.intTransmittance = 0;
						break;
					}
				}

				depth = result.depth / result.depthweightsum / _ProjectionParams.z;
				if (depth == 0.0f) {
					depth = maxSampleDistance;
				}
				intensity = result.intensity;
				return (1.0f - result.intTransmittance);	
			}

	
			float3 DepthToWorld(float2 uv, float depth) {
				float z = (1-depth) * 2.0 - 1.0;

				float4 clipSpacePosition = float4(uv * 2.0 - 1.0, z, 1.0);

				float4 viewSpacePosition = mul(_CamInvProj,clipSpacePosition);
				viewSpacePosition /= viewSpacePosition.w;

				float4 worldSpacePosition = mul(unity_ObjectToWorld,viewSpacePosition);

				return worldSpacePosition.xyz;
			}
			
			float GetRaymarchEndFromSceneDepth(float sceneDepth, out float raymarchEnd) {
				raymarchEnd = sceneDepth * _ProjectionParams.z;	//raymarch to scene depth.
				return sceneDepth<.99;
			}

			float4 frag (Interpolator i) : SV_Target
			{
				float3 vspos = float3(i.vsray, 1.0);
				float4 worldPos = mul(unity_CameraToWorld,float4(vspos,1.0));
				worldPos /= worldPos.w;
				float4 screenPos = UNITY_PROJ_COORD( i.screenPos );
				float depthSample = tex2Dproj( _CameraDepthTexture, screenPos ).r;
				float3 worldDepth = DepthToWorld(screenPos, depthSample);
				float raymarchEnd = length(worldDepth-worldPos.xyz);
				float raymarchStart = _ProjectionParams.y;
				
				//float sceneDepth = Linear01Depth(depthSample);
				//bool occluded = GetRaymarchEndFromSceneDepth(sceneDepth, raymarchEnd);
				float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);

				float2 screenUV = i.screenPos.xy / i.screenPos.w;

				//float blue = tex2D(_DitheringTex, screenPos * _DitheringCoords.xy + _DitheringCoords.zw).r;
				float dither = tex2D(_DitheringTex, screenUV * _DitheringCoords.xy).r;
				float offset = -fmod(_RaymarchOffset + dither, 1.0f);			//final offset combined. The value will be multiplied by sample step in GetDensity.

				float3 intensity;
				float distance;
				//TODO: sceneDepth here is distance in camera z-axis, but the parameter should be radial distance.
				float density = GetDensity(_WorldSpaceCameraPos, viewDir, raymarchEnd, offset, /*out*/intensity, /*out*/distance);
				if(depthSample > .99) density = 1;
				return float4(intensity, pack(distance, density));
			}

			ENDCG
		}

			//Pass 2, blend undersampled image with history buffer to new buffer.
			Pass{
				CGPROGRAM
				#pragma target 5.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile LOW_QUALITY MEDIUM_QUALITY HIGH_QUALITY

				#include "UnityCG.cginc"
				#include "Assets/Shaders/PackFloat.cginc"
				
				sampler2D _MainTex;						//history buffer.
				float4 _MainTex_TexelSize;
				sampler2D _UndersampleCloudTex;			//current undersampled tex.
				float4 _UndersampleCloudTex_TexelSize;

				float4x4 _PrevVP;	//View projection matrix of last frame. Used to temporal reprojection.

				//These values are needed for doing extra raymarch when out of bound.
				sampler2D _CameraDepthTexture;
				float4 _ProjectionExtents;

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float4 vertex : SV_POSITION;
					float2 uv : TEXCOORD0;
					float2 vsray : TEXCOORD1;
					float4 screenPos : TEXCOORD2;
				};

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;
					o.vsray = (2.0 * v.uv - 1.0) * _ProjectionExtents.xy + _ProjectionExtents.zw;
					o.screenPos = ComputeScreenPos(o.vertex);
					return o;
				}

				//Get uv of wspos in history buffer.
				float2 PrevUV(float4 wspos, out half outOfBound) {
					float4 prevUV = mul(_PrevVP, wspos);
					prevUV.xy = 0.5 * (prevUV.xy / prevUV.w) + 0.5;
					half oobmax = max(0.0 - prevUV.x, 0.0 - prevUV.y);
					half oobmin = max(prevUV.x - 1.0, prevUV.y - 1.0);
					outOfBound = step(0, max(oobmin, oobmax));
					return prevUV;
				}

				//Code from https://zhuanlan.zhihu.com/p/64993622. Do AABB clip in TAA(clip to center).
				float4 ClipAABB(float4 aabbMin, float4 aabbMax, float4 prevSample)
				{
					// note: only clips towards aabb center (but fast!)
					float4 p_clip = 0.5 * (aabbMax + aabbMin);
					float4 e_clip = 0.5 * (aabbMax - aabbMin);

					float4 v_clip = prevSample - p_clip;
					float4 v_unit = v_clip / e_clip;
					float4 a_unit = abs(v_unit);
					float ma_unit = max(max(a_unit.x, max(a_unit.y, a_unit.z)), a_unit.w);

					if (ma_unit > 1.0)
						return p_clip + v_clip / ma_unit;
					else
						return prevSample;// point inside aabb
				}
				
				float4 frag(v2f i) : SV_Target
				{
					float3 vspos = float3(i.vsray, 1.0);
					// float4 worldPos = mul(unity_CameraToWorld, float4(vspos, 1.0f));
					// worldPos /= worldPos.w;
					float4 raymarchResult = tex2D(_UndersampleCloudTex, i.uv);
					float distance;
					float density;
					float density2;
					unpack(raymarchResult.a, distance, density);
					distance *= _ProjectionParams.z;
					//float intensity = raymarchResult.x;
					
					{	//Do temporal reprojection and clip things.
						half outOfBound;
						float2 prevUV = PrevUV(mul(unity_CameraToWorld, float4(normalize(vspos) * distance, 1.0)), outOfBound);	//find uv in history buffer.
					
						float4 prevSample = tex2D(_MainTex, prevUV);
						float2 xoffset = float2(_UndersampleCloudTex_TexelSize.x, 0.0f);
						float2 yoffset = float2(0.0f, _UndersampleCloudTex_TexelSize.y);
					
						float4 m1 = 0.0f, m2 = 0.0f;
						//The loop below calculates mean and variance used to calculate AABB.
						[unroll]
						for (int x = -1; x <= 1; x ++) {
							[unroll]
							for (int y = -1; y <= 1; y ++ ) {
								float4 val;
								if (x == 0 && y == 0) {
									val = float4(raymarchResult.rgb, distance);
								}
								else {
									val = tex2Dlod(_UndersampleCloudTex, float4(i.uv + xoffset * x + yoffset * y, 0.0, 0.0));
									float distance2;
									unpack(val.a, distance2, density2);
									val = float4(val.rgb, distance2);
								}
								m1 += val;
								m2 += val * val;
							}
						}
						//Code from https://zhuanlan.zhihu.com/p/64993622.
						float gamma = 0.5f;
						float4 mu = m1 / 9;
						float4 sigma = sqrt(abs(m2 / 9 - mu * mu));
						float4 minc = mu - gamma * sigma;
						float4 maxc = mu + gamma * sigma;
						prevSample = ClipAABB(minc, maxc, prevSample);	
					
						//Blend
						raymarchResult = lerp(float4(prevSample.rgb, density), float4(raymarchResult.rgb, density2), max(0.05f, outOfBound));
					}
					return 	raymarchResult;
				}
				ENDCG
			}

			//Pass3, Blend final cloud image with final image.
			Pass{
				Cull Off ZWrite Off ZTest Always
				CGPROGRAM
				#pragma target 5.0
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"

				sampler2D _MainTex;	//Final image without cloud.
				sampler2D _CloudTex;	//The full resolution cloud tex we generated.
				sampler2D _CameraDepthTexture;
				float4 _ProjectionExtents;

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

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;
					return o;
				}
				
				half4 frag(v2f i) : SV_Target
				{
					//float3 vspos = float3(i.vsray, 1.0);
					//float4 worldPos = mul(unity_CameraToWorld,float4(vspos,1.0));

					half4 mcol = tex2D(_MainTex,i.uv);
					float4 currSample = tex2D(_CloudTex, i.uv);

					return half4(mcol.rgb * (1 - currSample.a) + currSample.rgb, 1);
				}
					ENDCG
				}
	}
}
