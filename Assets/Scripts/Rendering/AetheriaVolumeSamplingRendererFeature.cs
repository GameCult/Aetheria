using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class AetheriaVolumeSamplingRendererFeature : ScriptableRendererFeature
{
    private VolumeSamplingPass _pass;

    public override void Create()
    {
        _pass = new VolumeSamplingPass
        {
            renderPassEvent = RenderPassEvent.BeforeRendering
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null)
            Create();

        renderer.EnqueuePass(_pass);
    }

    private sealed class VolumeSamplingPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            internal UnityEngine.Camera Camera;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddUnsafePass<PassData>("Aetheria Volume Sampling", out var passData);
            var cameraData = frameData.Get<UniversalCameraData>();
            passData.Camera = cameraData.camera;
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                VolumeSampling.RenderForCamera(data.Camera);
            });
        }
    }
}
