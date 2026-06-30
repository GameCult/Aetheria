using System;
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
    private AetheriaUnityRtsViewportDocuments _viewportDocuments;
    private AetheriaRuntimeRenderSplatsViewportDocument _fallbackRenderSplatsViewport;
    private string _lastWarningMessage = "";

    public RenderTexture TargetTexture
    {
        get
        {
            if (layerRenderer != null && layerRenderer.TryGetTexture(layerKey, out var texture))
                return texture;

            return rasterizer != null ? rasterizer.TargetTexture : null;
        }
    }

    public AetheriaRuntimeRenderSplatsViewportDocument CurrentDocument =>
        _viewportDocuments?.CurrentRenderSplatsViewport ?? _fallbackRenderSplatsViewport;

    public bool RenderInLateUpdate
    {
        get => renderInLateUpdate;
        set => renderInLateUpdate = value;
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
        var document = _viewportDocuments?.CurrentRenderSplatsViewport ?? _fallbackRenderSplatsViewport;
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
        if (!force && _viewportDocuments != null && _viewportDocuments.Matches(viewport))
            return;

        if (!force && Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
        try
        {
            var nextDocuments = AetheriaUnityRuntimeClientProvider
                .RenderSplatViewportDocuments(viewport, "unity-render-splat-viewport");
            ClearViewportDocument();
            _viewportDocuments = nextDocuments;
            _fallbackRenderSplatsViewport = null;
            _lastWarningMessage = "";
        }
        catch (Exception ex)
        {
            ClearViewportDocument();
            _fallbackRenderSplatsViewport = EmptyRenderSplatsViewport(viewport);
            WarnOnce($"Failed to read Aetheria render splats from local Verse state: {ex.Message}");
        }
    }

    private void WarnOnce(string message)
    {
        if (string.Equals(_lastWarningMessage, message, StringComparison.Ordinal))
            return;

        _lastWarningMessage = message;
        Debug.LogWarning(message);
    }

    private static AetheriaRuntimeRenderSplatsViewportDocument EmptyRenderSplatsViewport(
        AetheriaRuntimeRtsViewportBounds viewport)
    {
        return new AetheriaRuntimeRenderSplatsViewportDocument
        {
            PublishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Viewport = viewport ?? new AetheriaRuntimeRtsViewportBounds()
        };
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
        _viewportDocuments?.Dispose();
        _viewportDocuments = null;
    }

}
