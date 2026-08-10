Shader "Aetheria/GPU Lightning" {
Properties {
	_StartColor("StartColor", Color) = (1,1,1,1)
	_EndColor("EndColor", Color) = (0,0,0,1)
	_Intensity("Intensity", Float) = 1
	_LeaderIntensity("LeaderIntensity", Float) = 10
	_LeaderProgress("Leader Progress", Float) = 1
	_LeaderSize("Leader Size", Float) = .01
	_BranchIntensity("Branch Intensity", Float) = 1
}
   
SubShader {
Pass{
    Tags { "RenderType"="Opaque" }

	CGPROGRAM
	#pragma target 5.0

	#pragma vertex vert
	#pragma fragment frag

	#include "UnityCG.cginc"
	#include "LightningVertex.cginc"
    #include "Assets/Shaders/Dither Functions.cginc"

	float _Intensity;
	float _LeaderIntensity;
	float _LeaderProgress;
	float _LeaderSize;
	float _BranchIntensity;
	fixed4 _StartColor;
	fixed4 _EndColor;
	StructuredBuffer<uint> _IndexBuffer;
	StructuredBuffer<Vertex> _VertexBuffer;

	Vertex GetVertex(uint indexBufferIdx) {
		uint idx = _IndexBuffer[indexBufferIdx];
		return _VertexBuffer[idx];
	}

	struct vs_out {
		float4 pos : SV_POSITION;
		float4 col : COLOR;
		float2 uv  : TEXCOORD;
		float4 screenPos  : TEXCOORD1;
	};

	vs_out vert (uint id : SV_VertexID, uint iId : SV_InstanceID)
	{
		vs_out Out;
		Vertex vtx = GetVertex(id);

		Out.pos = UnityObjectToClipPos(float4(vtx.pos, 1.0));
		Out.uv = vtx.uv;
		Out.col = lerp(_StartColor, _EndColor, vtx.uv.x);
		Out.screenPos = ComputeScreenPos(float4(vtx.pos, 1.0));

		return Out;
	}

	fixed4 frag (vs_out In) : COLOR0
	{
		// Discard fragments that are further than the leader progress
		clip(_LeaderProgress-In.uv.x);
		
		// Simple quadratic curve "texture"
		float x = 2 * (In.uv.y % 1) - 1;
		float a = 1 - x * x;

		float i = In.uv.y > 1 ? _BranchIntensity : _Intensity;
		a *= lerp(i, _LeaderIntensity, smoothstep(_LeaderProgress - _LeaderSize * 1.5, _LeaderProgress - _LeaderSize, In.uv.x));
		
	    // Apply screen space dithering
	    float2 screenUV = In.screenPos.xy / In.screenPos.w;
        ditherClip(screenUV, a);
		
		return In.col * a;
	}

	ENDCG
   
   }
Pass{
    Name "ShadowCaster"
    Tags { "LightMode" = "ShadowCaster" }

	CGPROGRAM
	#pragma target 5.0

	#pragma vertex vert
	#pragma fragment frag

	#include "UnityCG.cginc"
	#include "LightningVertex.cginc"
    #include "Assets/Shaders/Dither Functions.cginc"

	float _LeaderProgress;
	float _LeaderSize;
	StructuredBuffer<uint> _IndexBuffer;
	StructuredBuffer<Vertex> _VertexBuffer;

	Vertex GetVertex(uint indexBufferIdx) {
		uint idx = _IndexBuffer[indexBufferIdx];
		return _VertexBuffer[idx];
	}

	struct vs_out {
		float4 pos : SV_POSITION;
		float4 col : COLOR;
		float2 uv  : TEXCOORD;
		float4 screenPos  : TEXCOORD1;
	};

	vs_out vert (uint id : SV_VertexID, uint iId : SV_InstanceID)
	{
		vs_out Out;
		Vertex vtx = GetVertex(id);

		Out.pos = UnityObjectToClipPos(float4(vtx.pos, 1.0));
		Out.uv = vtx.uv;
		Out.col = 1;
		Out.screenPos = ComputeScreenPos(float4(vtx.pos, 1.0));

		return Out;
	}

	fixed4 frag (vs_out In) : COLOR0
	{
		// Discard fragments that are further than the leader progress
		clip(_LeaderProgress-In.uv.x);
		
		// Simple quadratic curve "texture"
		float x = 2 * (In.uv.y % 1) - 1;
		float a = 1 - x * x;
		
	    // Apply screen space dithering
	    float2 screenUV = In.screenPos.xy / In.screenPos.w;
        ditherClip(screenUV, a);
		
        SHADOW_CASTER_FRAGMENT(In)
	}

	ENDCG
   
   }
}

Fallback Off
}

