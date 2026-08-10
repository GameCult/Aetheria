Shader "Aetheria/Stardust (Compute)" 
{
	Properties {
		[HDR] _TintColor("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		_DistanceSizeExponent("Distance Size Exponent", Float) = 1
		_DistanceIntensityExponent("Distance Intensity Exponent", Float) = 1
		_EmissionGain("Emission Gain", Range(0, 1)) = 0.3
		_Power("Power", Float) = 2
	}

	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
		Pass {
			
			CGPROGRAM
			#pragma vertex particle_vertex
			#pragma fragment frag
			
            #pragma target 5.0
            #include "UnityCG.cginc"
			#include "Assets/Shaders/Dither Functions.cginc"
            
            struct Particle
            {
                float3 position;
                float3 color;
                float size;
            };
                        
            StructuredBuffer<Particle> particles;
            StructuredBuffer<float3> quadPoints;
            
            float4 _TintColor;
            float _EmissionGain;
			float _Power;
			float _DistanceSizeExponent;
			float _DistanceIntensityExponent;
            
            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };
            
            //Vertex shader with no inputs
            //Uses the system values SV_VertexID and SV_InstanceID to read from compute buffers
            v2f particle_vertex(uint id : SV_VertexID, uint inst : SV_InstanceID)
            {
                v2f o;
                
                //Only transform world pos by view matrix
                //To Create a billboarding effect
                float3 worldPosition = particles[inst].position;
                float dist = length(worldPosition - _WorldSpaceCameraPos);
                
                float4 cameraSpacePosition = mul(UNITY_MATRIX_V, float4(worldPosition, 1.0f));
                //cameraSpacePosition.w /= dist;
                
                float3 quadPoint = float3(quadPoints[id].xy, 0.0f) * particles[inst].size * pow(dist, _DistanceSizeExponent) / pow(100, _DistanceSizeExponent);
                o.pos = mul(UNITY_MATRIX_P, cameraSpacePosition + float4(quadPoint, 0.0f));
                
                //Shift coordinates for uvs
                o.uv = quadPoints[id] + 0.5f;
                
                //transfer color of particle and global tint to vertex
                o.color = float4(particles[inst].color * _TintColor.rgb, 100 * pow(100, _DistanceIntensityExponent) / pow(dist, _DistanceIntensityExponent));
				o.screenPos = ComputeScreenPos(o.pos);
                
                return o;
            }
		
			float powerPulse( float x, float power )
			{
				x = saturate(abs(x));
				return pow((x + 1.0f) * (1.0f - x), power);
			}
            
            float4 frag(v2f i) : COLOR
            {
				float emission = powerPulse(length(i.uv-float2(.5,.5))*2,_Power);
			    float2 screenUV = i.screenPos.xy / i.screenPos.w;
	            ditherClip(screenUV, emission * i.color.a);
                return i.color * emission * (exp(_EmissionGain * 5.0f));
            }
			ENDCG
		}
		Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
 
            CGPROGRAM
			#pragma vertex particle_vertex
			#pragma fragment frag
			
            #pragma target 5.0
            #include "UnityCG.cginc"
			#include "Assets/Shaders/Dither Functions.cginc"
            
            struct Particle
            {
                float3 position;
                float3 color;
                float size;
            };
                        
            StructuredBuffer<Particle> particles;
            StructuredBuffer<float3> quadPoints;
            
            float4 _TintColor;
            float _EmissionGain;
			float _Power;
			float _DistanceSizeExponent;
			float _DistanceIntensityExponent;
            
            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };
            
            //Vertex shader with no inputs
            //Uses the system values SV_VertexID and SV_InstanceID to read from compute buffers
            v2f particle_vertex(uint id : SV_VertexID, uint inst : SV_InstanceID)
            {
                v2f o;
                
                //Only transform world pos by view matrix
                //To Create a billboarding effect
                float3 worldPosition = particles[inst].position;
                float dist = length(worldPosition - _WorldSpaceCameraPos);
                
                float4 cameraSpacePosition = mul(UNITY_MATRIX_V, float4(worldPosition, 1.0f));
                //cameraSpacePosition.w /= dist;
                
                float3 quadPoint = float3(quadPoints[id].xy, 0.0f) * particles[inst].size * pow(dist, _DistanceSizeExponent) / pow(100, _DistanceSizeExponent);
                o.pos = mul(UNITY_MATRIX_P, cameraSpacePosition + float4(quadPoint, 0.0f));
                
                //Shift coordinates for uvs
                o.uv = quadPoints[id] + 0.5f;
                
                //transfer color of particle and global tint to vertex
                o.color = float4(particles[inst].color * _TintColor.rgb, 100 * pow(100, _DistanceIntensityExponent) / pow(dist, _DistanceIntensityExponent));
				o.screenPos = ComputeScreenPos(o.pos);
                
                return o;
            }
		
			float powerPulse( float x, float power )
			{
				x = saturate(abs(x));
				return pow((x + 1.0f) * (1.0f - x), power);
			}
            
            float4 frag(v2f i) : COLOR
            {
				float emission = powerPulse(length(i.uv-float2(.5,.5))*2,_Power);
			    float2 screenUV = i.screenPos.xy / i.screenPos.w;
	            ditherClip(screenUV, emission * i.color.a);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
	}
}