using System;
using System.IO;
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

    private AetheriaClient _client;
    private string _clientStatePath = "";
    private float _nextRefreshTime;
    private AetheriaRuntimeRenderSplatsViewportDocument _document;

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

    public bool RenderLatest()
    {
        ResolveRenderers();
        if (layerRenderer == null && rasterizer == null)
            return false;

        RefreshDocument(false);
        if (_document == null)
            return false;

        if (layerRenderer != null)
        {
            layerRenderer.Render(_document, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            return true;
        }

        rasterizer.Render(_document, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
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
        if (!force && Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
        try
        {
            _document = ResolveClient()
                .RenderSplatsViewportAsync(ResolveViewportBounds())
                .GetAwaiter()
                .GetResult();
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

    private AetheriaClient ResolveClient()
    {
        var gameDataDirectory = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "GameData"));
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(gameDataDirectory);
        if (_client != null && string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
            return _client;

        DisposeClient();
        _client = AetheriaClient
            .OpenLocalAsync(
                gameDataDirectory,
                "unity-render-splat-viewport",
                "local",
                pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        _clientStatePath = stateBoot.StateFilePath;
        return _client;
    }

    private void OnDestroy()
    {
        DisposeClient();
    }

    private void DisposeClient()
    {
        _client?.Dispose();
        _client = null;
        _clientStatePath = "";
    }
}
