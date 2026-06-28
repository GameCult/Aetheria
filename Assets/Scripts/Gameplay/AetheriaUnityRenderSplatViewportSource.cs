using System;
using GameCult.Mesh;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public sealed class AetheriaUnityRenderSplatViewportSource : MonoBehaviour
{
    [SerializeField]
    private AetheriaRenderSplatRasterizer rasterizer;

    [SerializeField]
    private AetheriaRenderSplatLayerRenderer layerRenderer;

    [SerializeField]
    private string layerKey = AetheriaRuntimeRenderSplatLayerKeys.GravityHeight;

    [SerializeField]
    private Vector2 position;

    [SerializeField]
    private Vector2 size = new Vector2(1024, 1024);

    [SerializeField]
    private float refreshIntervalSeconds = 0.5f;

    [SerializeField]
    private bool renderInLateUpdate = true;

    private float _nextRefreshTime;
    private AetheriaRuntimeRtsViewportBounds _viewport;
    private CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> _renderSplatsViewport;

    public RenderTexture TargetTexture
    {
        get
        {
            if (layerRenderer != null && layerRenderer.TryGetTexture(layerKey, out var texture))
                return texture;

            return rasterizer != null ? rasterizer.TargetTexture : null;
        }
    }

    private void Reset()
    {
        rasterizer = GetComponent<AetheriaRenderSplatRasterizer>();
        layerRenderer = GetComponent<AetheriaRenderSplatLayerRenderer>();
    }

    private void LateUpdate()
    {
        if (renderInLateUpdate)
            RenderLatest();
    }

    private void OnDisable()
    {
        ClearViewportDocument();
    }

    public bool RenderLatest()
    {
        ResolveRenderers();
        if (layerRenderer == null && rasterizer == null)
            return false;

        RefreshDocument(false);
        var document = _renderSplatsViewport?.Current;
        if (document == null)
            return false;

        if (layerRenderer != null)
        {
            layerRenderer.Render(document, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            return true;
        }

        rasterizer.Render(document, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
        return true;
    }

    private void ResolveRenderers()
    {
        if (layerRenderer == null)
            layerRenderer = GetComponent<AetheriaRenderSplatLayerRenderer>();
        if (rasterizer == null)
            rasterizer = GetComponent<AetheriaRenderSplatRasterizer>();

        if (layerRenderer == null && rasterizer == null)
            layerRenderer = gameObject.AddComponent<AetheriaRenderSplatLayerRenderer>();
    }

    public void SetLayerKey(string value)
    {
        layerKey = string.IsNullOrWhiteSpace(value)
            ? AetheriaRuntimeRenderSplatLayerKeys.GravityHeight
            : value;
    }

    public void SetViewport(Vector2 center, Vector2 viewportSize)
    {
        position = center;
        size = viewportSize;
        RefreshDocument(true);
    }

    private void RefreshDocument(bool force)
    {
        var viewport = ResolveViewportBounds();
        if (!force && _renderSplatsViewport != null && SameViewport(_viewport, viewport))
            return;

        if (!force && Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
        try
        {
            var nextRenderSplatsViewport = ResolveClient()
                .Aetheria()
                .Viewports
                .ReactiveRenderSplats(viewport);
            ClearViewportDocument();
            _viewport = viewport;
            _renderSplatsViewport = nextRenderSplatsViewport;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria render splats from local Verse state: {ex.Message}");
        }
    }

    private AetheriaRuntimeRtsViewportBounds ResolveViewportBounds()
    {
        var halfSize = size * 0.5f;
        return new AetheriaRuntimeRtsViewportBounds
        {
            MinX = position.x - halfSize.x,
            MinY = position.y - halfSize.y,
            MaxX = position.x + halfSize.x,
            MaxY = position.y + halfSize.y
        };
    }

    private void ClearViewportDocument()
    {
        _renderSplatsViewport?.Dispose();
        _renderSplatsViewport = null;
    }

    private static bool SameViewport(
        AetheriaRuntimeRtsViewportBounds left,
        AetheriaRuntimeRtsViewportBounds right)
    {
        if (left == null || right == null)
            return false;

        return Mathf.Approximately(left.MinX, right.MinX) &&
            Mathf.Approximately(left.MinY, right.MinY) &&
            Mathf.Approximately(left.MaxX, right.MaxX) &&
            Mathf.Approximately(left.MaxY, right.MaxY);
    }

    private AetheriaClient ResolveClient()
    {
        return AetheriaUnityRuntimeClientProvider.ResolveClient(
            AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory),
            "unity-render-splat-viewport");
    }

}
