using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Rendering.PostProcessing
{
    [Serializable, VolumeComponentMenu("Aetheria/Fog Blur")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class AetheriaFogBlur : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Texture whose red channel defines the fog blur mask.")]
        public TextureParameter blurMask = new TextureParameter(null, TextureDimension.Tex2D);

        [Min(0.1f), Tooltip("Distance to the point of focus.")]
        public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0.1f);

        [Tooltip("Ratio of aperture. Smaller values produce a shallower blur.")]
        public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, 0.05f, 32f);

        [Tooltip("Distance between lens and film. Larger values produce a shallower blur.")]
        public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 1f, 300f);

        [Tooltip("Maximum fog blur radius.")]
        public ClampedIntParameter kernelSize = new ClampedIntParameter((int)AetheriaFogBlurKernelSize.Medium, 0, 3);

        public bool IsActive()
        {
            return active && SystemInfo.graphicsShaderLevel >= 35;
        }

        public bool IsTileCompatible() => false;
    }
}
