using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class AetheriaVolumeCloudRendererFeature : ScriptableRendererFeature
{
    private VolumeCloudPass _pass;

    public override void Create()
    {
        _pass = new VolumeCloudPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null)
            Create();

        renderer.EnqueuePass(_pass);
    }

    private sealed class VolumeCloudPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            internal Camera Camera;
            internal VolumeCloudRenderer Renderer;
            internal TextureHandle ColorTarget;
            internal TextureHandle DepthTarget;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!VolumeCloudRenderer.TryGetRenderer(cameraData.camera, out var renderer))
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;
            if (!colorTarget.IsValid())
                return;

            using var builder = renderGraph.AddUnsafePass<PassData>("Aetheria Volume Clouds", out var passData);
            passData.Camera = cameraData.camera;
            passData.Renderer = renderer;
            passData.ColorTarget = colorTarget;
            passData.DepthTarget = resourceData.activeDepthTexture;

            builder.UseTexture(passData.ColorTarget, AccessFlags.ReadWrite);
            if (passData.DepthTarget.IsValid())
                builder.UseTexture(passData.DepthTarget, AccessFlags.Read);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                data.Renderer.RenderClouds(context.cmd, data.Camera, data.ColorTarget, data.DepthTarget);
            });
        }
    }
}
