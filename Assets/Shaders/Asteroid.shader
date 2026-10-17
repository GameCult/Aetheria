Shader "Aetheria/Asteroid"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
    }
    
    CGINCLUDE
    
    #pragma multi_compile_instancing
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc" // for UnityObjectToWorldNormal
    #include_with_pragmas "Assets/Shaders/Volumetric.cginc"

    struct v2f
    {
        float2 uv : TEXCOORD0;
        half3 ambient : COLOR0;
        float4 vertex : SV_POSITION;
    };

    v2f vert (appdata_base v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
        o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
        o.uv = v.texcoord;
        // get vertex normal in world space
        half3 worldNormal = UnityObjectToWorldNormal(v.normal);
        
        o.ambient = VolumeSampleColorSimple(worldPos, worldNormal).rgb * 10;
        return o;
    }
    
    sampler2D _MainTex;

    fixed4 frag (v2f i) : SV_Target
    {
        // sample texture
        fixed4 col = tex2D(_MainTex, i.uv);
        // multiply by lighting
        col.rgb *= i.ambient;
        return col;
    }
    
    float4 frag_shadow(v2f i) : COLOR
    {
        SHADOW_CASTER_FRAGMENT(i)
    }
    
    ENDCG
    
    SubShader
    {
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
}