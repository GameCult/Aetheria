Shader "Aetheria/PostProcessing/FogBlur"
{
    // SubShader with SM 5.0 support
    // DX11+, OpenGL 4.3+, OpenGL ES 3.1+AEP, Vulkan, consoles
    // Gather intrinsics are used to reduce texture sample count.
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0
        {
            Name "CoC Calculation"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragCoC
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 1
        {
            Name "CoC Temporal Filter"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex Vert
                #pragma fragment FragTempFilter
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 2
        {
            Name "Downsample and Prefilter"

            HLSLPROGRAM
                #pragma target 5.0
                #pragma vertex Vert
                #pragma fragment FragPrefilter
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 3
        {
            Name "Bokeh Filter (small)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_SMALL
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 4
        {
            Name "Bokeh Filter (medium)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_MEDIUM
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 5
        {
            Name "Bokeh Filter (large)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_LARGE
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 6
        {
            Name "Bokeh Filter (very large)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_VERYLARGE
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 7
        {
            Name "Postfilter"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragPostBlur
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 8
        {
            Name "Combine"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragCombine
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 9
        {
            Name "Debug Overlay"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragDebugOverlay
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }
    }

    // Fallback SubShader with SM 3.5
    // DX11+, OpenGL 3.2+, OpenGL ES 3+, Metal, Vulkan, consoles
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0
        {
            Name "CoC Calculation"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragCoC
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 1
        {
            Name "CoC Temporal Filter"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragTempFilter
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 2
        {
            Name "Downsample and Prefilter"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragPrefilter
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 3
        {
            Name "Bokeh Filter (small)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_SMALL
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 4
        {
            Name "Bokeh Filter (medium)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_MEDIUM
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 5
        {
            Name "Bokeh Filter (large)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_LARGE
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 6
        {
            Name "Bokeh Filter (very large)"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragBlur
                #define KERNEL_VERYLARGE
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 7
        {
            Name "Postfilter"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragPostBlur
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 8
        {
            Name "Combine"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragCombine
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }

        Pass // 9
        {
            Name "Debug Overlay"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment FragDebugOverlay
                #include "AetheriaFogBlurUrp.hlsl"
            ENDHLSL
        }
    }
}
