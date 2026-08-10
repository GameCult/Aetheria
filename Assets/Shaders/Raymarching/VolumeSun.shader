// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Aetheria/Volume Sun"
{
	Properties
	{
		_ColorRamp ("Color Ramp", 2D) = "white" {}
		_Offset ("Offset", CUBE) = "gray" {}
		_Albedo ("Albedo", CUBE) = "black" {}
		_RayStepSize("Ray Step Size", Float) = .01
		_FirstOffsetDistance("First Offset Distance", Float) = .01
		_FirstOffsetDepthExponent("First Offset Depth Exponent", Float) = 1
		_SecondOffsetDistance("Second Offset Distance", Float) = .01
		_SecondOffsetDepthExponent("Second Offset Depth Exponent", Float) = 1
		_DensityDepthExponent("Density Depth Exponent", Float) = 1
		_DensityAlbedoExponent("Density Albedo Exponent", Float) = 1
		_NoiseFrequency("Noise Frequency", Float) = 1
		_NoiseAmplitude("Noise Amplitude", Float) = 1
		_NoiseSpeed("Noise Speed", Float) = 1
        _Emission("Emission", Float) = 1
        _Alpha("Alpha Multiplier", Float) = 1
        _Glossiness("Gloss", Range(0,1)) = 1
        _Metallic("Metallic", Range(0,1)) = 1
        //_EdgeTransparency("Edge Transparency", Float) = 1
	}
	
	SubShader
	{
        Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM

		#pragma surface surf Standard vertex:vert NoLighting noambient noforwardadd
		#pragma target 3.0
        #include "UnityPBSLighting.cginc"
        #include "Assets/Shaders/Dither Functions.cginc"

		sampler2D _ColorRamp;
		samplerCUBE _Offset;
		samplerCUBE _Albedo;
 
		struct Input {
			float4 screenPos;
			float3 worldNormal;
			float3 viewDir;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		float _RayStepSize;
		float _FirstOffsetDistance;
		float _SecondOffsetDistance;
		float _FirstOffsetDepthExponent;
		float _SecondOffsetDepthExponent;
		float _LimbDarkening;
		float _DensityDepthExponent;
		float _DensityAlbedoExponent;
		float _Alpha;
		float _Emission;

		float4x4 _AlbedoRotation;
		float4x4 _FirstOffsetDomainRotation;
		float4x4 _FirstOffsetRotation;
		float4x4 _SecondOffsetDomainRotation;
		float4x4 _SecondOffsetRotation;

		float4 _LightingDirection;
		half _NoiseFrequency;
		half _NoiseAmplitude;
		half _NoiseSpeed;

		void vert (inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input,o);
		}

		float tri(in float x){return abs(frac(x)-.5);}
		float3 tri3(in float3 p){return float3( tri(p.z+tri(p.y*1.)), tri(p.z+tri(p.x*1.)), tri(p.y+tri(p.x*1.)));}

		float triNoise3d(in float3 p, in float spd)
		{
		    float z=1.4;
			float rz = 0.;
		    float3 bp = p;
			for (float i=0.; i<=2.; i++ )
			{
		        float3 dg = tri3(bp*2.);
		        p += (dg+_Time.y*spd);

		        bp *= 1.8;
				z *= 1.5;
				p *= 1.2;
		        
		        rz+= (tri(p.z+tri(p.x+tri(p.y))))/z;
		        bp += 0.14;
			}
			return rz;
		}
		
		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float rim = saturate(dot(IN.viewDir,IN.worldNormal));
        	float2 screenPos = IN.screenPos.xy / IN.screenPos.w;
        	
			float3 rayPos = IN.worldNormal;
			float3 rayStep = normalize(IN.viewDir)*_RayStepSize*(2-rim);
			half dither = frac(tex2D(_DitheringTex, (screenPos + .5) * _DitheringCoords.xy).r + _FrameNumber * 1.61803398875);
			//float dither = tex2D(_DitheringTex, (screenPos + .5) * _DitheringCoords.xy + _DitheringCoords.zw).r * 2;
			rayPos += rayStep * dither;
        	
			float3 accum = 0;
        	float alphaAccum = 0;
			for(int i=0;i<32;i++){
				float elevation = length(rayPos);
				float depth = 1 - elevation;
				float turbulence = 1.25 - abs(rayPos.y) * .5;

				float3 firstSamplePosition = mul((float3x3)_FirstOffsetDomainRotation, rayPos);
				float3 offset =  mul((float3x3)_FirstOffsetRotation,
									 normalize(texCUBElod(_Offset, float4(firstSamplePosition, i/8)).rgb - float3(.5,.5,.5))) *
									 _FirstOffsetDistance * (pow(elevation,_FirstOffsetDepthExponent)) * turbulence;

				float3 secondSamplePosition = mul((float3x3)_SecondOffsetDomainRotation, rayPos + offset);
				float3 offset2 = mul((float3x3)_SecondOffsetRotation, 
									 normalize(texCUBElod(_Offset, float4(secondSamplePosition, i/16)).rgb - float3(.5,.5,.5))) * 
									 _SecondOffsetDistance * (pow(elevation,_SecondOffsetDepthExponent)) * turbulence;

				float albedo = texCUBElod (_Albedo, float4(mul((float3x3)_AlbedoRotation,normalize(firstSamplePosition + offset2)) ,i/8)).x;
				float noise = max(1 - (triNoise3d(secondSamplePosition * _NoiseFrequency, _NoiseSpeed)) * _NoiseAmplitude, 0.01);

				float density = pow(max(depth,0),_DensityDepthExponent) * pow(albedo,_DensityAlbedoExponent);
				alphaAccum += density;
				accum += tex2D(_ColorRamp, albedo.xx * noise) * density * noise;
				            
				rayPos += rayStep;
			}
        	ditherClip(screenPos, alphaAccum * _Alpha);

            o.Smoothness = _Glossiness;
        	o.Albedo = 0;
			o.Emission = accum*_Emission;
            o.Metallic = _Metallic;
			o.Alpha = 1;
		}
		ENDCG
	}
	FallBack "Standard"
}
