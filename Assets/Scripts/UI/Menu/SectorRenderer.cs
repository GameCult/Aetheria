using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public class SectorRenderer : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    public ClickRaycaster Raycaster;
    public Canvas Canvas;
    public SectorMap Map;
    public Camera SectorCamera;
    public MeshRenderer SectorBackgroundRenderer;
    public float ZoomSpeed;
    public float MinViewSize = .1f;
    public float MaxViewSize = 2;
    // public float PathAnimationDamping = .01f;
    // public float PathAnimationDuration = 30;
    // public float PathAnimationDurationPadding = 1.1f;
    public GameObject LegendPanel;
    public float LinkAnimationDuration;
    public float IconAnimationDuration;

    private float2 _startMousePosition;
    private float2 _startMapPosition;

    private Transform _sectorBackgroundTransform;
    private Transform _sectorCameraTransform;
    private UnityEngine.UI.Image _outputImage;
    private RenderTexture _outputTexture;
    private int2 _size;
    private bool _init;
    private float _aspectRatio;
    private float _sectorBackgroundDepth;
    private float _sectorCameraDepth;
    private UIDocument _zoneDetailsSurfaceDocument;
    private string _clientStatePath = "";
    private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
    private CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings;
    private CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> _sectorMap;
    private CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> _currentZone;
    private int _zoneDetailsIndex = -1;
    private CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> _zoneDetails;
    private readonly AetheriaEveUnitySurfaceChrome _zoneDetailsSurfaceChrome = new AetheriaEveUnitySurfaceChrome
    {
        RootAlignItems = Align.FlexEnd,
        RootPaddingTop = 24f,
        RootPaddingRight = 24f,
        Width = 360f,
        MinWidth = 0f,
        MaxWidth = 420f,
        PaddingLeft = 18f,
        PaddingRight = 18f,
        PaddingTop = 18f,
        PaddingBottom = 18f
    };
    
    private float2 _position = float2(0.5f);
    private float _viewSize = .5f;
    
    void Start()
    {
        _outputImage = GetComponent<UnityEngine.UI.Image>();
        _sectorBackgroundTransform = SectorBackgroundRenderer.transform;
        _sectorBackgroundDepth = _sectorBackgroundTransform.position.z;
        _sectorCameraTransform = SectorCamera.transform;
        _sectorCameraDepth = _sectorCameraTransform.position.z;
        Raycaster.OnClickMiss += data =>
        {
            HideZoneDetailsSurface();
        };
        Map.ZoneClicked.Subscribe(zone =>
        {
            RenderZoneDetailsSurface(zone);
        });

        // PathAnimationButton.onClick.AddListener(() =>
        // {
        //     Map.StartCoroutine(AnimatePath());
        // });
    }

    private void RenderZoneDetailsSurface(int zoneIndex)
    {
        _zoneDetailsSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _zoneDetailsSurfaceDocument,
            "Aetheria Sector Zone Details Surface",
            AetheriaRuntimeZoneDetailsSurfaceBuilder.Build(ProjectZoneDetailsSurfaceState(zoneIndex)),
            HandleZoneDetailsSurfaceCommand,
            _zoneDetailsSurfaceChrome);
    }

    private void HandleZoneDetailsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeZoneDetailsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown sector-map zone details command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeZoneDetailsCommandKind.Close)
        {
            HideZoneDetailsSurface();
            return;
        }

        Debug.LogWarning($"Unknown sector-map zone details command: {request?.Command}");
    }

    private void HideZoneDetailsSurface()
    {
        if (_zoneDetailsSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_zoneDetailsSurfaceDocument);
    }

    private AetheriaRuntimeZoneDetailsSurfaceState ProjectZoneDetailsSurfaceState(int zoneIndex)
    {
        var sectorZone = ResolveSectorZone(zoneIndex);
        var zoneDetails = ResolveZoneDetails(zoneIndex);
        var ownerFactionIndex = sectorZone?.OwnerFactionIndex ?? -1;
        var otherFactions = (sectorZone?.FactionIndices ?? Array.Empty<int>())
            .Where(index => index >= 0 && index != ownerFactionIndex)
            .Distinct()
            .Select(FormatFaction)
            .ToArray();

        var daemonProjection = AetheriaRuntimeZoneDetailsSurfaceBuilder.ProjectDaemonZone(
            zoneDetails,
            ResolveHullType);
        return AetheriaRuntimeZoneDetailsSurfaceBuilder.Project(
            ResolveZoneName(sectorZone, zoneDetails),
            ownerFactionIndex >= 0 ? FormatFaction(ownerFactionIndex) : "None",
            FormatValue((float)daemonProjection.Mass),
            FormatValue((float)daemonProjection.Radius),
            otherFactions,
            daemonProjection.Bodies,
            daemonProjection.Entities,
            daemonProjection.HasContents,
            updatedAtUtc: DateTime.UtcNow.ToString("O"));
    }

    private string ResolveZoneName(
        AetheriaRuntimeSectorMapZone sectorZone,
        AetheriaRuntimeZoneDetailsDocument zoneDetails)
    {
        if (!string.IsNullOrWhiteSpace(zoneDetails?.ZoneName))
            return zoneDetails.ZoneName;

        if (!string.IsNullOrWhiteSpace(sectorZone?.Name))
            return sectorZone.Name;

        return sectorZone == null ? "Unknown" : $"Zone {sectorZone.ZoneIndex}";
    }

    private AetheriaRuntimeSectorMapZone ResolveSectorZone(int zoneIndex)
    {
        if (zoneIndex < 0)
            return null;

        try
        {
            _sectorMap ??= ResolveClient()
                .State.Reactive<AetheriaRuntimeSectorMapDocument>();
            var sectorMap = _sectorMap?.Current;
            return (sectorMap?.Zones ?? Array.Empty<AetheriaRuntimeSectorMapZone>())
                .FirstOrDefault(zone => zone.ZoneIndex == zoneIndex);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria sector map zone from local Verse state: {ex.Message}");
            return null;
        }
    }

    private static string FormatFaction(int factionIndex)
    {
        return factionIndex < 0 ? "None" : $"Faction {factionIndex}";
    }

    private AetheriaRuntimeZoneDetailsDocument ResolveZoneDetails(int zoneIndex)
    {
        if (zoneIndex < 0)
            return null;

        if (_zoneDetails != null && _zoneDetailsIndex == zoneIndex)
            return _zoneDetails.Current;

        try
        {
            var nextZoneDetails = ResolveClient()
                .State.Reactive<AetheriaRuntimeZoneDetailsDocument>(zoneIndex);
            _zoneDetails?.Dispose();
            _zoneDetailsIndex = zoneIndex;
            _zoneDetails = nextZoneDetails;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria sector zone details from local Verse state: {ex.Message}");
        }

        return _zoneDetails?.Current;
    }

    private string ResolveHullType(string hullItemKey)
    {
        var typedHull = ResolveCatalog()?.FindItem(hullItemKey ?? "");
        return typedHull?.HullType ?? "";
    }

    private AetheriaRuntimeCatalogSnapshot ResolveCatalog()
    {
        if (_catalog != null)
            return _catalog.Current;

        try
        {
            _catalog = ResolveClient().State.Reactive<AetheriaRuntimeCatalogSnapshot>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria sector catalog from local Verse state: {ex.Message}");
        }

        return _catalog?.Current;
    }

    private AetheriaRuntimePlayerSettingsDocument ResolvePlayerSettings()
    {
        if (_playerSettings != null)
            return _playerSettings.Current;

        try
        {
            _playerSettings = ResolveClient()
                .State.Reactive<AetheriaRuntimePlayerSettingsDocument>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria sector player settings from local Verse state: {ex.Message}");
        }

        return _playerSettings?.Current;
    }

    private string FormatValue(float value)
    {
        var digits = ResolvePlayerSettings()?.SignificantDigits ?? 3;
        var magnitude = value == 0f ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1;
        digits -= magnitude;
        if (digits < 0)
            digits = 0;

        var formatted = value.ToString($"N{digits}");
        var decimalSeparator = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        return formatted.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
            ? formatted.TrimEnd('0').TrimEnd(decimalSeparator)
            : formatted;
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
        {
            _clientStatePath = stateBoot.StateFilePath;
            ClearClientCaches();
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot, "unity-sector-renderer");
    }

    private void ClearClientCaches()
    {
        _catalog?.Dispose();
        _playerSettings?.Dispose();
        _sectorMap?.Dispose();
        _currentZone?.Dispose();
        _zoneDetails?.Dispose();
        _catalog = null;
        _playerSettings = null;
        _sectorMap = null;
        _currentZone = null;
        _zoneDetails = null;
        _zoneDetailsIndex = -1;
    }
    private void OnEnable()
    {
        _init = true;
        SectorCamera.gameObject.SetActive(true);
        var currentZone = ResolveCurrentZone();
        _position = currentZone == null
            ? float2.zero
            : new float2((float)currentZone.PositionX, (float)currentZone.PositionY);
        _viewSize = .25f;
        
        Map.StartReveal(LinkAnimationDuration, IconAnimationDuration);
        if (currentZone != null)
            Map.TryMarkPlayerLocation(currentZone.ZoneIndex);
    }

    private AetheriaRuntimeCurrentZoneDocument ResolveCurrentZone()
    {
        try
        {
            _currentZone ??= ResolveClient()
                .State.Reactive<AetheriaRuntimeCurrentZoneDocument>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria sector current zone from local Verse state: {ex.Message}");
        }

        return _currentZone?.Current;
    }

    private void OnDisable()
    {
        HideZoneDetailsSurface();
        if (_outputTexture != null)
        {
            _outputTexture.Release();
            _outputTexture = null;
        }
        SectorCamera.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ClearClientCaches();

        if (_zoneDetailsSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_zoneDetailsSurfaceDocument);
            _zoneDetailsSurfaceDocument = null;
        }
    }

    void LateUpdate()
    {
        var size = int2(Screen.width, Screen.height);
        if (_init || size.x != _size.x || size.y != _size.y)
        {
            _aspectRatio = (float) size.x / size.y;
            _size = size;
            if (_outputTexture != null)
            {
                _outputTexture.Release();
            }
            _outputTexture = new RenderTexture(_size.x, _size.y, 0, RenderTextureFormat.Default);
            SectorCamera.targetTexture = _outputTexture;
            _outputImage.material.SetTexture("_DetailTex", _outputTexture);
        }

        UpdateCamera();
    }

    void UpdateCamera()
    {
        var halfSize = _viewSize / 2;
        var bounds = float4(
            _position.x - _aspectRatio * halfSize, 
            _position.y - halfSize, 
            _position.x + _aspectRatio * halfSize, 
            _position.y + halfSize);
        _sectorBackgroundTransform.position = new Vector3(_position.x, _position.y, _sectorBackgroundDepth);
        _sectorBackgroundTransform.localScale = new Vector3(_aspectRatio * _viewSize, _viewSize);
        _sectorCameraTransform.position = new Vector3(_position.x, _position.y, _sectorCameraDepth);
        SectorCamera.orthographicSize = halfSize;
        SectorBackgroundRenderer.material.SetVector("Extents", bounds);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _startMousePosition = eventData.position;
        _startMapPosition = _position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _position = _startMapPosition - ((float2)eventData.position - _startMousePosition) / _size.y * _viewSize;
    }

    public void OnScroll(PointerEventData eventData)
    {
        var mapCenter = float2((float)Screen.width / 2, (float)Screen.height / 2);
        var oldPointerPosition = _position + ((float2)eventData.position - mapCenter) / Screen.height * _viewSize;
        _viewSize = clamp(_viewSize * (1 - eventData.scrollDelta.y * ZoomSpeed), MinViewSize, MaxViewSize);
        var pointerPosition = _position + ((float2)eventData.position - mapCenter) / Screen.height * _viewSize;
        _position += oldPointerPosition - pointerPosition;
    }
}
