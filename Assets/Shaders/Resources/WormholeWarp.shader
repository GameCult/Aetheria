// The screen-space transition into a wormhole: a radial lensing pull toward the wormhole, a swirl and a small chromatic
// split, all scaled by one strength. Lives under Resources so the player build always carries it (WormholeWarp.cs finds
// it with Shader.Find).
Shader "Hidden/Aetheria/WormholeWarp"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex VertDefault
                #pragma fragment Frag
                #include "../CustomDoF/StdLib.hlsl"

                TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
                float2 _Center;   // viewport uv of the wormhole
                float _Strength;  // 0..1
                float _Aspect;    // width / height

                // The colour the pixel shows when the screen is pulled toward the centre by pull, turned by swirl.
                half3 Warp(float2 uv, float pull, float swirl)
                {
                    float2 d = (uv - _Center) * float2(_Aspect, 1);
                    float s, c;
                    sincos(swirl, s, c);
                    d = float2(c * d.x - s * d.y, s * d.x + c * d.y) * (1 + pull);
                    float2 src = _Center + d / float2(_Aspect, 1);
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, src).rgb;
                }

                float4 Frag(VaryingsDefault i) : SV_Target
                {
                    float2 uv = UnityStereoTransformScreenSpaceTex(i.texcoordStereo);
                    float2 d = (uv - _Center) * float2(_Aspect, 1);
                    float r = length(d);
                    float well = exp(-r * 2.2);                       // 1 at the wormhole, fading out across the screen
                    float pull = _Strength * (0.35 + 1.4 * well);
                    float swirl = _Strength * 2.4 * well * well;
                    float split = 0.06 * _Strength * well;

                    half3 col;
                    col.r = Warp(uv, pull + split, swirl).r;
                    col.g = Warp(uv, pull, swirl).g;
                    col.b = Warp(uv, pull - split, swirl).b;

                    // The throat darkens as the dive deepens.
                    col *= 1 - saturate(_Strength * _Strength * well * 1.1);
                    return float4(col, 1);
                }
            ENDHLSL
        }
    }
}
