using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Rendering.PostProcessing
{
    public sealed class AetheriaFogBlurRendererFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private Shader shader;

        private FogBlurPass _pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Aetheria/PostProcessing/FogBlur");

            _pass = new FogBlurPass(shader)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                Create();

            renderer.EnqueuePass(_pass);
        }

        private sealed class FogBlurPass : ScriptableRenderPass, IDisposable
        {
            private enum Pass
            {
                CoCCalculation,
                CoCTemporalFilter,
                DownsampleAndPrefilter,
                BokehSmallKernel,
                BokehMediumKernel,
                BokehLargeKernel,
                BokehVeryLargeKernel,
                PostFilter,
                Combine
            }

            private sealed class PassData
            {
                internal Material Material;
                internal Texture BlurMask;
                internal bool BlurMaskIsPackedCloud;
                internal TextureHandle Source;
                internal TextureHandle Destination;
                internal TextureHandle CoC;
                internal TextureHandle DoF;
                internal TextureHandle Temp;
                internal float Distance;
                internal float LensCoeff;
                internal float MaxCoC;
                internal float RcpMaxCoC;
                internal float RcpAspect;
                internal int KernelPass;
                internal int Width;
                internal int Height;
                internal int HalfWidth;
                internal int HalfHeight;
            }

            private const float FilmHeight = 0.024f;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Aetheria Fog Blur");
            private Material _material;

            public FogBlurPass(Shader shader)
            {
                requiresIntermediateTexture = true;

                if (shader != null && shader.isSupported)
                    _material = CoreUtils.CreateEngineMaterial(shader);
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                var fogBlur = VolumeManager.instance.stack.GetComponent<AetheriaFogBlur>();
                if (fogBlur == null || !fogBlur.IsActive())
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                var source = resourceData.activeColorTexture;
                if (!source.IsValid())
                    return;

                var blurMask = fogBlur.blurMask.value;
                var blurMaskIsPackedCloud = false;
                if (blurMask == null &&
                    VolumeCloudRenderer.TryGetRenderer(cameraData.camera, out var cloudRenderer))
                {
                    blurMask = cloudRenderer.DebugCurrentCloudBuffer;
                    blurMaskIsPackedCloud = blurMask != null;
                }

                if (blurMask == null)
                    return;

                var sourceDesc = source.GetDescriptor(renderGraph);
                var width = sourceDesc.width;
                var height = sourceDesc.height;
                if (width <= 0 || height <= 0)
                    return;

                var halfWidth = Mathf.Max(1, width / 2);
                var halfHeight = Mathf.Max(1, height / 2);

                var destinationDesc = MakeColorDescriptor(sourceDesc, width, height, sourceDesc.colorFormat, "_AetheriaFogBlurColor");
                var cocDesc = MakeColorDescriptor(sourceDesc, width, height, GraphicsFormat.R8_UNorm, "_AetheriaFogBlurCoC");
                var dofDesc = MakeColorDescriptor(sourceDesc, halfWidth, halfHeight, sourceDesc.colorFormat, "_AetheriaFogBlurDoF");
                var tempDesc = MakeColorDescriptor(sourceDesc, halfWidth, halfHeight, sourceDesc.colorFormat, "_AetheriaFogBlurTemp");

                var destination = renderGraph.CreateTexture(destinationDesc);
                var coc = renderGraph.CreateTexture(cocDesc);
                var dof = renderGraph.CreateTexture(dofDesc);
                var temp = renderGraph.CreateTexture(tempDesc);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Aetheria Fog Blur", out var passData, _profilingSampler))
                {
                    var scaledFilmHeight = FilmHeight * (height / 1080f);
                    var focalLengthMeters = fogBlur.focalLength.value / 1000f;
                    var focusDistance = Mathf.Max(fogBlur.focusDistance.value, focalLengthMeters);
                    var aspect = width / (float)height;
                    var coeff = focalLengthMeters * focalLengthMeters /
                        (fogBlur.aperture.value * (focusDistance - focalLengthMeters) * scaledFilmHeight * 2f);
                    var maxCoC = CalculateMaxCoCRadius((AetheriaFogBlurKernelSize)fogBlur.kernelSize.value, height);

                    passData.Material = _material;
                    passData.BlurMask = blurMask;
                    passData.BlurMaskIsPackedCloud = blurMaskIsPackedCloud;
                    passData.Source = source;
                    passData.Destination = destination;
                    passData.CoC = coc;
                    passData.DoF = dof;
                    passData.Temp = temp;
                    passData.Distance = focusDistance;
                    passData.LensCoeff = coeff;
                    passData.MaxCoC = maxCoC;
                    passData.RcpMaxCoC = 1f / maxCoC;
                    passData.RcpAspect = 1f / aspect;
                    passData.KernelPass = (int)Pass.BokehSmallKernel + fogBlur.kernelSize.value;
                    passData.Width = width;
                    passData.Height = height;
                    passData.HalfWidth = halfWidth;
                    passData.HalfHeight = halfHeight;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(destination, AccessFlags.Write);
                    builder.UseTexture(coc, AccessFlags.ReadWrite);
                    builder.UseTexture(dof, AccessFlags.ReadWrite);
                    builder.UseTexture(temp, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        Execute(data, CommandBufferHelpers.GetNativeCommandBuffer(context.cmd));
                    });
                }

                resourceData.cameraColor = destination;
            }

            private static TextureDesc MakeColorDescriptor(
                TextureDesc source,
                int width,
                int height,
                GraphicsFormat format,
                string name)
            {
                return new TextureDesc(source)
                {
                    width = width,
                    height = height,
                    format = format,
                    name = name,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    useMipMap = false,
                    autoGenerateMips = false,
                    anisoLevel = 0,
                    filterMode = FilterMode.Bilinear,
                    discardBuffer = false
                };
            }

            private static float CalculateMaxCoCRadius(AetheriaFogBlurKernelSize kernelSize, int screenHeight)
            {
                var radiusInPixels = (float)kernelSize * 4f + 6f;
                return Mathf.Min(0.05f, radiusInPixels / screenHeight);
            }

            private static void Execute(PassData data, CommandBuffer cmd)
            {
                var material = data.Material;
                material.SetFloat(ShaderIds.Distance, data.Distance);
                material.SetFloat(ShaderIds.LensCoeff, data.LensCoeff);
                material.SetFloat(ShaderIds.MaxCoC, data.MaxCoC);
                material.SetFloat(ShaderIds.RcpMaxCoC, data.RcpMaxCoC);
                material.SetFloat(ShaderIds.RcpAspect, data.RcpAspect);
                material.SetTexture(ShaderIds.DoFBlurTex, data.BlurMask);
                material.SetFloat(ShaderIds.DoFBlurTexPacked, data.BlurMaskIsPackedCloud ? 1f : 0f);

                SetMainTex(cmd, data.Source, data.Width, data.Height);
                Blitter.BlitCameraTexture(cmd, data.Source, data.CoC, material, (int)Pass.CoCCalculation);

                SetMainTex(cmd, data.Source, data.Width, data.Height);
                material.SetTexture(ShaderIds.CoCTex, data.CoC);
                Blitter.BlitCameraTexture(cmd, data.Source, data.DoF, material, (int)Pass.DownsampleAndPrefilter);

                SetMainTex(cmd, data.DoF, data.HalfWidth, data.HalfHeight);
                Blitter.BlitCameraTexture(cmd, data.DoF, data.Temp, material, data.KernelPass);

                SetMainTex(cmd, data.Temp, data.HalfWidth, data.HalfHeight);
                Blitter.BlitCameraTexture(cmd, data.Temp, data.DoF, material, (int)Pass.PostFilter);

                SetMainTex(cmd, data.Source, data.Width, data.Height);
                material.SetTexture(ShaderIds.DepthOfFieldTex, data.DoF);
                material.SetVector(ShaderIds.DepthOfFieldTexTexelSize, TexelSize(data.HalfWidth, data.HalfHeight));
                material.SetTexture(ShaderIds.CoCTex, data.CoC);
                Blitter.BlitCameraTexture(cmd, data.Source, data.Destination, material, (int)Pass.Combine);
            }

            private static void SetMainTex(CommandBuffer cmd, TextureHandle texture, int width, int height)
            {
                cmd.SetGlobalVector(ShaderIds.MainTexTexelSize, TexelSize(width, height));
            }

            private static Vector4 TexelSize(int width, int height)
            {
                return new Vector4(1f / width, 1f / height, width, height);
            }

            private static class ShaderIds
            {
                internal static readonly int MainTexTexelSize = Shader.PropertyToID("_MainTex_TexelSize");
                internal static readonly int DoFBlurTex = Shader.PropertyToID("_DoFBlurTex");
                internal static readonly int DoFBlurTexPacked = Shader.PropertyToID("_DoFBlurTexPacked");
                internal static readonly int DepthOfFieldTex = Shader.PropertyToID("_DepthOfFieldTex");
                internal static readonly int DepthOfFieldTexTexelSize = Shader.PropertyToID("_DepthOfFieldTex_TexelSize");
                internal static readonly int Distance = Shader.PropertyToID("_Distance");
                internal static readonly int LensCoeff = Shader.PropertyToID("_LensCoeff");
                internal static readonly int MaxCoC = Shader.PropertyToID("_MaxCoC");
                internal static readonly int RcpMaxCoC = Shader.PropertyToID("_RcpMaxCoC");
                internal static readonly int RcpAspect = Shader.PropertyToID("_RcpAspect");
                internal static readonly int CoCTex = Shader.PropertyToID("_CoCTex");
            }
        }
    }
}
