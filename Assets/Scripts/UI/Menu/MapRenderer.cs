/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using TMPro;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UI;
using static Unity.Mathematics.math;
using int2 = Unity.Mathematics.int2;

public class MapRenderer : MonoBehaviour
{
    public ZoneRenderer ZoneRenderer;
    public TextMeshProUGUI Title;
    public Camera MapOverlayCamera;
    public Camera GravityCamera;
    public Camera TintCamera;
    public Camera InfluenceCamera;
    public Image OverlayDisplay;
    public Image GravityDisplay;
    public Material GravityBackdropMaterial;
    public Image TintDisplay;
    public Image InfluenceDisplay;
    public AetheriaRenderSplatLayerRenderer SplatLayerRenderer;
    public AetheriaDaemonRenderAssetCatalog AssetCatalog;
    public RectTransform RtsIconRoot;
    public RawImage RtsIconPrototype;
    public float Scale;
    public float2 Position;
    public float IconSize = 1f/128;

    private RectTransform _rect;
    private RenderTexture _mapTexture;
    private int2 _size;
    private bool _init;
    private CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> _objectsViewport;
    private CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> _renderSplatsViewport;
    private float _nextViewportRefreshTime;
    private readonly List<RawImage> _rtsIconPool = new List<RawImage>();
    private string _clientStatePath = "";
    private CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings;
    
    void Start()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _init = true;
        SetCameraActive(MapOverlayCamera, false);
        SetCameraActive(GravityCamera, false);
        SetCameraActive(TintCamera, false);
        SetCameraActive(InfluenceCamera, false);
        if (OverlayDisplay != null)
            OverlayDisplay.gameObject.SetActive(false);
        if (GravityBackdropMaterial != null && GravityDisplay != null)
            GravityDisplay.material = GravityBackdropMaterial;
        EnsureRtsIconRoot();
        SetRtsIconsActive(true);
        RefreshViewportDocuments(force: true);
        
        // If hiding minimap asteroids, turn them back on for the map screen
        if (!ResolveShowAsteroidsInMinimap())
            ZoneRenderer.ShowAsteroidUI = true;
    }

    private void OnDisable()
    {
        ClearViewportCaches();
        if (_mapTexture != null)
        {
            ReleaseTextures();
        }
        SetCameraActive(MapOverlayCamera, false);
        SetCameraActive(GravityCamera, false);
        SetCameraActive(TintCamera, false);
        SetCameraActive(InfluenceCamera, false);
        if (OverlayDisplay != null)
            OverlayDisplay.gameObject.SetActive(false);
        SetRtsIconsActive(false);
        
        // If hiding minimap asteroids, turn them back off when leaving the map screen
        if (!ResolveShowAsteroidsInMinimap())
            ZoneRenderer.ShowAsteroidUI = false;
    }

    void ReleaseTextures()
    {
        _mapTexture.Release();
        _mapTexture = null;
    }

    void LateUpdate()
    {
        var size = int2(Screen.width, Screen.height);
        if (_init || size.x != _size.x || size.y != _size.y)
        {
            _init = false;
            _size = size;
            if (_mapTexture != null)
            {
                ReleaseTextures();
            }
            
        }

        GravityDisplay.material.SetFloat("_Scale", Scale / 2);
        RefreshViewportDocuments(force: false);
        RenderSplatLayers();
        RenderRtsIcons();
    }

    private void RefreshViewportDocuments(bool force)
    {
        if (!force && Time.unscaledTime < _nextViewportRefreshTime)
            return;

        _nextViewportRefreshTime = Time.unscaledTime + .5f;
        try
        {
            var client = ResolveClient();
            var viewport = ResolveViewportBounds();
            ClearViewportCaches();
            _objectsViewport = client
                .State
                .Document<AetheriaRuntimeObjectsViewportDocument>(viewport)
                .Reactive();
            _renderSplatsViewport = client
                .State
                .Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport)
                .Reactive();

            var objectsViewport = _objectsViewport?.Current;
            var zoneName = string.IsNullOrWhiteSpace(objectsViewport?.ZoneName)
                ? "Unknown"
                : objectsViewport.ZoneName;
            Title.text = $"Zone: {zoneName}";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria map viewport from local Verse state: {ex.Message}");
            Title.text = "Zone: Unknown";
        }
    }

    private void RenderSplatLayers()
    {
        var renderSplatsViewport = _renderSplatsViewport?.Current;
        if (renderSplatsViewport == null)
            return;

        if (SplatLayerRenderer == null)
            SplatLayerRenderer = GetComponent<AetheriaRenderSplatLayerRenderer>() ??
                gameObject.AddComponent<AetheriaRenderSplatLayerRenderer>();

        SplatLayerRenderer.Render(renderSplatsViewport, _size.x, _size.y);
        ApplyLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.GravityHeight, GravityDisplay);
        ApplyLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.FogTint, TintDisplay);
        ApplyLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.Influence, InfluenceDisplay);
    }

    private void ApplyLayerTexture(string layerKey, Image display)
    {
        if (display == null ||
            SplatLayerRenderer == null ||
            !SplatLayerRenderer.TryGetTexture(layerKey, out var texture) ||
            texture == null)
        {
            return;
        }

        display.material.SetTexture("_DetailTex", texture);
    }

    private static void SetCameraActive(Camera camera, bool active)
    {
        if (camera != null)
            camera.gameObject.SetActive(active);
    }

    private void RenderRtsIcons()
    {
        EnsureRtsIconRoot();
        if (RtsIconRoot == null)
            return;

        var objects = _objectsViewport?.Current?.Objects ?? Array.Empty<AetheriaRuntimeRtsViewportObject>();
        for (var i = 0; i < objects.Count; i++)
        {
            var icon = ResolveRtsIcon(i);
            ApplyRtsIcon(icon, objects[i]);
        }

        for (var i = objects.Count; i < _rtsIconPool.Count; i++)
            if (_rtsIconPool[i] != null)
                _rtsIconPool[i].gameObject.SetActive(false);
    }

    private RawImage ResolveRtsIcon(int index)
    {
        while (_rtsIconPool.Count <= index)
        {
            RawImage icon;
            if (RtsIconPrototype != null)
            {
                icon = Instantiate(RtsIconPrototype, RtsIconRoot);
            }
            else
            {
                var go = new GameObject("RTS Map Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(RtsIconRoot, false);
                icon = go.GetComponent<RawImage>();
                icon.texture = Texture2D.whiteTexture;
                icon.raycastTarget = false;
            }

            _rtsIconPool.Add(icon);
        }

        return _rtsIconPool[index];
    }

    private void ApplyRtsIcon(RawImage icon, AetheriaRuntimeRtsViewportObject obj)
    {
        if (icon == null || obj == null)
            return;

        icon.gameObject.SetActive(true);
        if (AssetCatalog != null && AssetCatalog.TryResolveTexture(obj.IconAsset, out var texture))
            icon.texture = texture;
        icon.color = ResolveIconColor(obj);
        var rectTransform = icon.rectTransform;
        var pixelSize = Mathf.Max(4f, IconSize * (obj.Controlled ? 1.25f : 1f));
        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(pixelSize, pixelSize);
        rectTransform.anchoredPosition = WorldToMapPosition(obj.X, obj.Y);
        rectTransform.localRotation = Quaternion.Euler(
            0,
            0,
            Mathf.Atan2((float)obj.DirectionY, (float)obj.DirectionX) * Mathf.Rad2Deg - 90f);
    }

    private Vector2 WorldToMapPosition(double worldX, double worldY)
    {
        var viewport = _objectsViewport?.Current?.Viewport ?? ResolveViewportBounds();
        var minX = Math.Min(viewport.MinX, viewport.MaxX);
        var maxX = Math.Max(viewport.MinX, viewport.MaxX);
        var minY = Math.Min(viewport.MinY, viewport.MaxY);
        var maxY = Math.Max(viewport.MinY, viewport.MaxY);
        var width = Math.Max(0.0001, maxX - minX);
        var height = Math.Max(0.0001, maxY - minY);
        var rootRect = RtsIconRoot != null ? RtsIconRoot.rect : new Rect(0, 0, _size.x, _size.y);
        var u = (float)((worldX - minX) / width);
        var v = (float)((worldY - minY) / height);
        return new Vector2(
            (u - 0.5f) * rootRect.width,
            (v - 0.5f) * rootRect.height);
    }

    private static Color ResolveIconColor(AetheriaRuntimeRtsViewportObject obj)
    {
        var color = obj.Controlled
            ? new Color(0.3f, 0.9f, 1.0f, 1.0f)
            : Color.HSVToRGB(frac((obj.FactionKey ?? obj.Kind ?? "").GetHashCode() * 0.0137f), 0.55f, 0.95f);
        color.a = Mathf.Clamp01(Mathf.Max(0.25f, (float)obj.Visibility));
        return color;
    }

    private void EnsureRtsIconRoot()
    {
        if (RtsIconRoot != null)
            return;

        var parent = GravityDisplay != null
            ? GravityDisplay.transform.parent
            : transform;
        var go = new GameObject("RTS Command Icons", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RtsIconRoot = go.GetComponent<RectTransform>();
        RtsIconRoot.anchorMin = Vector2.zero;
        RtsIconRoot.anchorMax = Vector2.one;
        RtsIconRoot.offsetMin = Vector2.zero;
        RtsIconRoot.offsetMax = Vector2.zero;
        RtsIconRoot.SetAsLastSibling();
    }

    private void SetRtsIconsActive(bool active)
    {
        if (RtsIconRoot != null)
            RtsIconRoot.gameObject.SetActive(active);
    }

    private AetheriaRuntimeRtsViewportBounds ResolveViewportBounds()
    {
        var screenHeight = _size.y <= 0 ? Math.Max(1, Screen.height) : _size.y;
        var screenWidth = _size.x <= 0 ? Math.Max(1, Screen.width) : _size.x;
        var halfHeight = screenHeight * Scale * .5f;
        var halfWidth = screenWidth * Scale * .5f;
        return new AetheriaRuntimeRtsViewportBounds
        {
            MinX = Position.x - halfWidth,
            MinY = Position.y - halfHeight,
            MaxX = Position.x + halfWidth,
            MaxY = Position.y + halfHeight
        };
    }

    private bool ResolveShowAsteroidsInMinimap()
    {
        try
        {
            _playerSettings ??= ResolveClient()
                .State.Document<AetheriaRuntimePlayerSettingsDocument>().Reactive();
            return _playerSettings?.Current?.ShowAsteroidsInMinimap ?? false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria map graphics settings from local Verse state: {ex.Message}");
            return false;
        }
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
        {
            _clientStatePath = stateBoot.StateFilePath;
            ClearClientCaches();
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(
            stateBoot,
            "unity-map-renderer");
    }

    private void ClearClientCaches()
    {
        ClearViewportCaches();
        _playerSettings?.Dispose();
        _playerSettings = null;
    }

    private void ClearViewportCaches()
    {
        _objectsViewport?.Dispose();
        _renderSplatsViewport?.Dispose();
        _objectsViewport = null;
        _renderSplatsViewport = null;
    }

    private void OnDestroy()
    {
        ClearClientCaches();
    }
}
