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
    public RectTransform MapIconRoot;
    public RawImage MapIconPrototype;
    public float Scale;
    public float2 Position;
    private AetheriaClientState _runtimeState;
    public float IconSize = 1f/128;

    private RectTransform _rect;
    private RenderTexture _mapTexture;
    private int2 _size;
    private bool _init;
    private AetheriaUnityGameViewportDocuments _viewportDocuments;
    private float _nextViewportRefreshTime;
    private readonly List<RawImage> _mapIconPool = new List<RawImage>();
    
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
        EnsureMapIconRoot();
        SetMapIconsActive(true);
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
        SetMapIconsActive(false);
        
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
        RenderMapIcons();
    }

    private void RefreshViewportDocuments(bool force)
    {
        if (!force && Time.unscaledTime < _nextViewportRefreshTime)
            return;

        _nextViewportRefreshTime = Time.unscaledTime + .5f;
        try
        {
            var viewport = ResolveViewportBounds();
            ClearViewportCaches();
            _viewportDocuments = AetheriaUnityRuntimeClientProvider
                .MapViewportDocuments(viewport, "unity-map-renderer");

            var objectsViewport = _viewportDocuments?.CurrentObjectsViewport;
            var zoneName = string.IsNullOrWhiteSpace(objectsViewport?.ZoneName)
                ? "Unknown"
                : objectsViewport.ZoneName;
            Title.text = $"Zone: {zoneName}";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria map viewport from Verse state: {ex.Message}");
            Title.text = "Zone: Unknown";
        }
    }

    private void RenderSplatLayers()
    {
        var renderSplatsViewport = _viewportDocuments?.CurrentRenderSplatsViewport;
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

    private void RenderMapIcons()
    {
        EnsureMapIconRoot();
        if (MapIconRoot == null)
            return;

        var objects = _viewportDocuments?.CurrentObjectsViewport?.Objects ?? Array.Empty<AetheriaRuntimeViewportObject>();
        for (var i = 0; i < objects.Count; i++)
        {
            var icon = ResolveMapIcon(i);
            ApplyMapIcon(icon, objects[i]);
        }

        for (var i = objects.Count; i < _mapIconPool.Count; i++)
            if (_mapIconPool[i] != null)
                _mapIconPool[i].gameObject.SetActive(false);
    }

    private RawImage ResolveMapIcon(int index)
    {
        while (_mapIconPool.Count <= index)
        {
            RawImage icon;
            if (MapIconPrototype != null)
            {
                icon = Instantiate(MapIconPrototype, MapIconRoot);
            }
            else
            {
                var go = new GameObject("Map Viewport Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(MapIconRoot, false);
                icon = go.GetComponent<RawImage>();
                icon.texture = Texture2D.whiteTexture;
                icon.raycastTarget = false;
            }

            _mapIconPool.Add(icon);
        }

        return _mapIconPool[index];
    }

    private void ApplyMapIcon(RawImage icon, AetheriaRuntimeViewportObject obj)
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
        var viewport = _viewportDocuments?.CurrentObjectsViewport?.Viewport ?? ResolveViewportBounds();
        var minX = Math.Min(viewport.MinX, viewport.MaxX);
        var maxX = Math.Max(viewport.MinX, viewport.MaxX);
        var minY = Math.Min(viewport.MinY, viewport.MaxY);
        var maxY = Math.Max(viewport.MinY, viewport.MaxY);
        var width = Math.Max(0.0001, maxX - minX);
        var height = Math.Max(0.0001, maxY - minY);
        var rootRect = MapIconRoot != null ? MapIconRoot.rect : new Rect(0, 0, _size.x, _size.y);
        var u = (float)((worldX - minX) / width);
        var v = (float)((worldY - minY) / height);
        return new Vector2(
            (u - 0.5f) * rootRect.width,
            (v - 0.5f) * rootRect.height);
    }

    private static Color ResolveIconColor(AetheriaRuntimeViewportObject obj)
    {
        var color = obj.Controlled
            ? new Color(0.3f, 0.9f, 1.0f, 1.0f)
            : Color.HSVToRGB(frac((obj.FactionKey ?? obj.Kind ?? "").GetHashCode() * 0.0137f), 0.55f, 0.95f);
        color.a = Mathf.Clamp01(Mathf.Max(0.25f, (float)obj.Visibility));
        return color;
    }

    private void EnsureMapIconRoot()
    {
        if (MapIconRoot != null)
            return;

        var parent = GravityDisplay != null
            ? GravityDisplay.transform.parent
            : transform;
        var go = new GameObject("Map Viewport Icons", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        MapIconRoot = go.GetComponent<RectTransform>();
        MapIconRoot.anchorMin = Vector2.zero;
        MapIconRoot.anchorMax = Vector2.one;
        MapIconRoot.offsetMin = Vector2.zero;
        MapIconRoot.offsetMax = Vector2.zero;
        MapIconRoot.SetAsLastSibling();
    }

    private void SetMapIconsActive(bool active)
    {
        if (MapIconRoot != null)
            MapIconRoot.gameObject.SetActive(active);
    }

    private AetheriaRuntimeViewportBounds ResolveViewportBounds()
    {
        var screenHeight = _size.y <= 0 ? Math.Max(1, Screen.height) : _size.y;
        var screenWidth = _size.x <= 0 ? Math.Max(1, Screen.width) : _size.x;
        var halfHeight = screenHeight * Scale * .5f;
        var halfWidth = screenWidth * Scale * .5f;
        return new AetheriaRuntimeViewportBounds
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
            _runtimeState ??= AetheriaUnityRuntimeClientProvider.RuntimeState("unity-map-renderer");
            return _runtimeState.PlayerSettings.Latest()?.ShowAsteroidsInMinimap ?? false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria map graphics settings from Verse state: {ex.Message}");
            return false;
        }
    }

    private void ClearClientCaches()
    {
        ClearViewportCaches();
    }

    private void ClearViewportCaches()
    {
        _viewportDocuments?.Dispose();
        _viewportDocuments = null;
    }

    private void OnDestroy()
    {
        ClearClientCaches();
    }
}
