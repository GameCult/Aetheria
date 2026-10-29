/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

Shader "Aetheria/Dithered Particles"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    
    CGINCLUDE

    #include "UnityCG.cginc"
    #include "Dither Functions.cginc"
    
    struct appdata_particles {
        float4 vertex : POSITION;
        float4 color : COLOR;
        float4 texcoord : TEXCOORD0;
    };

    struct v2f
    {
        float4 position : SV_POSITION;
        float4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float2 ditherOffset : TEXCOORD1;
        float4 screenPos : TEXCOORD2;
    };

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _Color;
    float _Cutoff;

    v2f vert(appdata_particles v)
    {
        v2f o;
        o.position = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
        o.ditherOffset = v.texcoord.zw;
        o.color = v.color;
        o.screenPos = ComputeScreenPos(o.position);
        return o;
    }

    float4 frag(v2f i) : COLOR
    {
        float4 c = tex2D(_MainTex, i.texcoord) * _Color * i.color;
        // Apply screen space dithering
		float2 screenUV = i.screenPos.xy / i.screenPos.w;
        ditherClip(screenUV + i.ditherOffset, c.a);
        return c;
    }

    // The particle in the G-buffer: black, unreflective, emissive, so deferred lighting adds nothing and its depth is in the
    // depth texture the nebula march fogs by. A forward pass draws after that texture is taken, so the fog paints over it.
    struct GBufferOut
    {
        half4 albedo : SV_Target0;
        half4 specular : SV_Target1;
        half4 normal : SV_Target2;
        half4 emission : SV_Target3;
    };

    GBufferOut frag_deferred(v2f i)
    {
        float4 c = tex2D(_MainTex, i.texcoord) * _Color * i.color;
        float2 screenUV = i.screenPos.xy / i.screenPos.w;
        ditherClip(screenUV + i.ditherOffset, c.a);
        GBufferOut o;
        o.albedo = 0;
        o.specular = 0;
        o.normal = half4(.5, 1, .5, 1);
        float3 light = c.rgb;
        #ifndef UNITY_HDR_ON
        light = exp2(-light);
        #endif
        o.emission = half4(light, 1);
        return o;
    }
    
    float4 frag_shadow(v2f i) : COLOR
    {
        float4 c = tex2D(_MainTex, i.texcoord) * _Color.a * i.color.a;
        // Apply screen space dithering
		float2 screenUV = i.screenPos.xy / i.screenPos.w;
        ditherClip(screenUV + i.ditherOffset, c.a);
        SHADOW_CASTER_FRAGMENT(i)
    }

    ENDCG

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "DEFERRED"
            Tags { "LightMode" = "Deferred" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_deferred
            #pragma multi_compile ___ UNITY_HDR_ON
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_shadow
            ENDCG
        }
    } 
    FallBack "Diffuse"
}
