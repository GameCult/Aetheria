Shader "Aetheria/PostProcessing/TacticalFade"
{
    // Lerps the scene colour toward a flat background by Strength: the scene fades out as the tactical view comes in.
    // The schematic camera renders after the post stack, so this never dims it.
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment Frag
                #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

                TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
                float _Strength;
                float4 _Background;

                float4 Frag(VaryingsDefault i) : SV_Target
                {
                    float4 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoordStereo);
                    return float4(lerp(scene.rgb, _Background.rgb, saturate(_Strength)), scene.a);
                }
            ENDHLSL
        }
    }
}
