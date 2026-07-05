using GameCult.Aetheria.State.Verse;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public sealed class AetheriaRenderSplatRasterizer : MonoBehaviour
{
    [SerializeField]
    private Material splatMaterial;

    [SerializeField]
    private RenderTexture targetTexture;

    [SerializeField]
    private GraphicsFormat targetFormat = GraphicsFormat.R16_SFloat;

    [SerializeField]
    private int width = 512;

    [SerializeField]
    private int height = 512;

    [SerializeField]
    private int channelFilter = -1;

    [SerializeField]
    private int layerFilter = -1;

    [SerializeField]
    private int materialPass;

    [SerializeField]
    private Color clearColor = Color.clear;

    [SerializeField]
    private bool clearBeforeDraw = true;

    private readonly AetheriaRenderSplatBuffer _buffer = new AetheriaRenderSplatBuffer();
    private CommandBuffer _commandBuffer;
    private RenderTexture _ownedTargetTexture;
    private Material _ownedSplatMaterial;
    private static readonly int SplatBufferPropertyId =
        Shader.PropertyToID(AetheriaRenderSplatBuffer.SplatBufferPropertyName);
    private static readonly int SplatCountPropertyId =
        Shader.PropertyToID(AetheriaRenderSplatBuffer.SplatCountPropertyName);
    private static readonly int ViewportToClipPropertyId =
        Shader.PropertyToID(AetheriaRenderSplatBuffer.ViewportToClipPropertyName);
    private static readonly int ChannelFilterPropertyId =
        Shader.PropertyToID(AetheriaRenderSplatBuffer.ChannelFilterPropertyName);

    public RenderTexture TargetTexture => targetTexture != null ? targetTexture : _ownedTargetTexture;
    public int ChannelFilter
    {
        get => channelFilter;
        set => channelFilter = value;
    }

    public int LayerFilter
    {
        get => layerFilter;
        set => layerFilter = value;
    }

    public int LastDrawnCount { get; private set; }

    public RenderTexture Render(
        AetheriaRuntimeRenderSplatsViewportDocument document,
        int overrideWidth = 0,
        int overrideHeight = 0)
    {
        if (document == null)
        {
            LastDrawnCount = 0;
            return TargetTexture;
        }

        var output = EnsureTarget(overrideWidth, overrideHeight);
        var material = ResolveMaterial();
        if (output == null || material == null)
        {
            LastDrawnCount = 0;
            return output;
        }

        _buffer.Upload(document.Splats, channelFilter, layerFilter);
        LastDrawnCount = _buffer.Count;
        if (!_buffer.HasGpuBuffer)
        {
            if (clearBeforeDraw)
                Clear(output);
            return output;
        }

        _commandBuffer ??= new CommandBuffer { name = "Aetheria Render Splats" };
        _commandBuffer.Clear();
        _commandBuffer.SetRenderTarget(output);
        if (clearBeforeDraw)
            _commandBuffer.ClearRenderTarget(false, true, clearColor);

        _commandBuffer.SetGlobalBuffer(SplatBufferPropertyId, _buffer.Buffer);
        _commandBuffer.SetGlobalInt(SplatCountPropertyId, _buffer.Count);
        _commandBuffer.SetGlobalInt(ChannelFilterPropertyId, channelFilter);
        _commandBuffer.SetGlobalMatrix(ViewportToClipPropertyId, BuildViewportToClip(document.Viewport));
        _commandBuffer.DrawProcedural(
            Matrix4x4.identity,
            material,
            math.max(0, materialPass),
            MeshTopology.Triangles,
            6,
            _buffer.Count);
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        return output;
    }

    public RenderTexture Render(
        AetheriaRuntimeRenderSplatsViewportDocument document,
        int overrideWidth,
        int overrideHeight,
        int overrideChannelFilter)
    {
        var previousChannelFilter = channelFilter;
        channelFilter = overrideChannelFilter;
        try
        {
            return Render(document, overrideWidth, overrideHeight);
        }
        finally
        {
            channelFilter = previousChannelFilter;
        }
    }

    public RenderTexture RenderLayer(
        AetheriaRuntimeRenderSplatsViewportDocument document,
        int overrideWidth,
        int overrideHeight,
        int overrideLayerFilter,
        int overrideMaterialPass,
        GraphicsFormat overrideTargetFormat,
        Color overrideClearColor)
    {
        var previousLayerFilter = layerFilter;
        var previousMaterialPass = materialPass;
        var previousTargetFormat = targetFormat;
        var previousClearColor = clearColor;
        layerFilter = overrideLayerFilter;
        materialPass = overrideMaterialPass;
        targetFormat = overrideTargetFormat;
        clearColor = overrideClearColor;
        try
        {
            return Render(document, overrideWidth, overrideHeight);
        }
        finally
        {
            layerFilter = previousLayerFilter;
            materialPass = previousMaterialPass;
            targetFormat = previousTargetFormat;
            clearColor = previousClearColor;
        }
    }

    public RenderTexture RenderLayerToTarget(
        AetheriaRuntimeRenderSplatsViewportDocument document,
        RenderTexture output,
        int overrideLayerFilter,
        int overrideMaterialPass,
        Color overrideClearColor)
    {
        var previousTargetTexture = targetTexture;
        var previousLayerFilter = layerFilter;
        var previousMaterialPass = materialPass;
        var previousClearColor = clearColor;
        targetTexture = output;
        layerFilter = overrideLayerFilter;
        materialPass = overrideMaterialPass;
        clearColor = overrideClearColor;
        try
        {
            return Render(document, output != null ? output.width : 0, output != null ? output.height : 0);
        }
        finally
        {
            targetTexture = previousTargetTexture;
            layerFilter = previousLayerFilter;
            materialPass = previousMaterialPass;
            clearColor = previousClearColor;
        }
    }

    private Material ResolveMaterial()
    {
        if (splatMaterial != null)
            return splatMaterial;

        if (_ownedSplatMaterial != null)
            return _ownedSplatMaterial;

        var shader = Shader.Find("Aetheria/Render Splats");
        if (shader == null)
            return null;

        _ownedSplatMaterial = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };
        return _ownedSplatMaterial;
    }

    private RenderTexture EnsureTarget(int overrideWidth, int overrideHeight)
    {
        if (targetTexture != null)
            return targetTexture;

        var targetWidth = math.max(1, overrideWidth > 0 ? overrideWidth : width);
        var targetHeight = math.max(1, overrideHeight > 0 ? overrideHeight : height);
        if (_ownedTargetTexture != null &&
            _ownedTargetTexture.width == targetWidth &&
            _ownedTargetTexture.height == targetHeight &&
            _ownedTargetTexture.graphicsFormat == targetFormat)
        {
            return _ownedTargetTexture;
        }

        ReleaseOwnedTarget();
        var descriptor = new RenderTextureDescriptor(targetWidth, targetHeight)
        {
            depthBufferBits = 0,
            graphicsFormat = targetFormat,
            msaaSamples = 1,
            sRGB = false,
            useMipMap = false,
            autoGenerateMips = false
        };
        _ownedTargetTexture = new RenderTexture(descriptor)
        {
            name = "Aetheria Render Splats",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _ownedTargetTexture.Create();
        return _ownedTargetTexture;
    }

    private void Clear(RenderTexture output)
    {
        _commandBuffer ??= new CommandBuffer { name = "Aetheria Render Splats" };
        _commandBuffer.Clear();
        _commandBuffer.SetRenderTarget(output);
        _commandBuffer.ClearRenderTarget(false, true, clearColor);
        Graphics.ExecuteCommandBuffer(_commandBuffer);
    }

    private static Matrix4x4 BuildViewportToClip(AetheriaRuntimeViewportBounds viewport)
    {
        viewport ??= new AetheriaRuntimeViewportBounds();
        var minX = (float)math.min(viewport.MinX, viewport.MaxX);
        var minY = (float)math.min(viewport.MinY, viewport.MaxY);
        var maxX = (float)math.max(viewport.MinX, viewport.MaxX);
        var maxY = (float)math.max(viewport.MinY, viewport.MaxY);
        var width = math.max(0.0001f, maxX - minX);
        var height = math.max(0.0001f, maxY - minY);

        return new Matrix4x4(
            new Vector4(2f / width, 0, 0, 0),
            new Vector4(0, 2f / height, 0, 0),
            new Vector4(0, 0, 1, 0),
            new Vector4(-(maxX + minX) / width, -(maxY + minY) / height, 0, 1));
    }

    private void OnDisable()
    {
        _buffer.Dispose();
        _commandBuffer?.Release();
        _commandBuffer = null;
        DestroyOwnedMaterial();
        ReleaseOwnedTarget();
        LastDrawnCount = 0;
    }

    private void DestroyOwnedMaterial()
    {
        if (_ownedSplatMaterial == null)
            return;

        Destroy(_ownedSplatMaterial);
        _ownedSplatMaterial = null;
    }

    private void ReleaseOwnedTarget()
    {
        if (_ownedTargetTexture == null)
            return;

        _ownedTargetTexture.Release();
        Destroy(_ownedTargetTexture);
        _ownedTargetTexture = null;
    }
}
