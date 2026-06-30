using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class AetheriaStardustRendererFeature : ScriptableRendererFeature
{
    private StardustPass _pass;

    public override void Create()
    {
        _pass = new StardustPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null)
            Create();

        renderer.EnqueuePass(_pass);
    }

    private sealed class StardustPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            internal Camera Camera;
            internal TextureHandle ColorTarget;
            internal TextureHandle DepthTarget;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;
            if (!colorTarget.IsValid())
                return;

            using var builder = renderGraph.AddUnsafePass<PassData>("Aetheria Stardust", out var passData);
            passData.Camera = cameraData.camera;
            passData.ColorTarget = colorTarget;
            passData.DepthTarget = resourceData.activeDepthTexture;

            builder.UseTexture(passData.ColorTarget, AccessFlags.ReadWrite);
            if (passData.DepthTarget.IsValid())
                builder.UseTexture(passData.DepthTarget, AccessFlags.Read);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                Stardust.RenderForCamera(context.cmd, data.Camera, data.ColorTarget, data.DepthTarget);
            });
        }
    }
}
