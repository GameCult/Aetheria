using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
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
    public ActionGameManager GameManager;
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

    private static bool IsBodyKind(AetheriaRuntimeBodySnapshotCommit body, string kind)
    {
        return body != null && string.Equals(body.Kind ?? "", kind, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlanetBody(AetheriaRuntimeBodySnapshotCommit body)
    {
        return body != null &&
               !IsBodyKind(body, "asteroid_belt") &&
               !IsBodyKind(body, "gas_giant") &&
               !IsBodyKind(body, "sun");
    }

    private static bool HasHullType(AetheriaRuntimeEntitySnapshotCommit entity, HullType hullType)
    {
        var typedHull = ActionGameManager.RuntimeCatalog?.FindItem(entity?.HullItemKey ?? "");
        return typedHull != null &&
               string.Equals(typedHull.HullType, hullType.ToString(), StringComparison.Ordinal);
    }

    private void RenderZoneDetailsSurface(GalaxyZone zone)
    {
        _zoneDetailsSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _zoneDetailsSurfaceDocument,
            "Aetheria Sector Zone Details Surface",
            AetheriaRuntimeZoneDetailsSurfaceBuilder.Build(ProjectZoneDetailsSurfaceState(zone)),
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

    private AetheriaRuntimeZoneDetailsSurfaceState ProjectZoneDetailsSurfaceState(GalaxyZone zone)
    {
        var density = ActionGameManager.TryGetObservedGalaxy(out var observedGalaxy)
            ? saturate(observedGalaxy.Background.CloudDensity(zone.Position) / 2)
            : 0f;
        var radius = GameManager.Settings.ZoneSettings.ZoneRadius.Evaluate(density);
        var mass = GameManager.Settings.ZoneSettings.ZoneMass.Evaluate(density);
        var otherFactions = zone.Factions
            .Where(faction => !faction.HasSameKey(zone.Owner))
            .Select(faction => faction.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        var daemonZone = GameManager?.FindDaemonZoneSnapshot(zone?.ZoneIndex ?? -1);
        if (daemonZone == null)
        {
            return new AetheriaRuntimeZoneDetailsSurfaceState(
                zone.Name,
                zone.Owner?.Name ?? "None",
                ActionGameManager.RuntimePlayerSettings.Format(mass),
                ActionGameManager.RuntimePlayerSettings.Format(radius),
                otherFactions,
                hasContents: false,
                planets: "",
                asteroidBelts: "",
                gasGiants: "",
                stars: "",
                stations: "",
                turrets: "",
                ships: "",
                updatedAtUtc: DateTime.UtcNow.ToString("O"));
        }

        var bodies = daemonZone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>();
        var entities = daemonZone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
        return new AetheriaRuntimeZoneDetailsSurfaceState(
            zone.Name,
            zone.Owner?.Name ?? "None",
            ActionGameManager.RuntimePlayerSettings.Format(mass),
            ActionGameManager.RuntimePlayerSettings.Format(radius),
            otherFactions,
            hasContents: true,
            planets: bodies.Count(IsPlanetBody).ToString(),
            asteroidBelts: bodies.Count(body => IsBodyKind(body, "asteroid_belt")).ToString(),
            gasGiants: bodies.Count(body => IsBodyKind(body, "gas_giant")).ToString(),
            stars: bodies.Count(body => IsBodyKind(body, "sun")).ToString(),
            stations: entities.Count(entity => HasHullType(entity, HullType.Station)).ToString(),
            turrets: entities.Count(entity => HasHullType(entity, HullType.Turret)).ToString(),
            ships: entities.Count(entity => HasHullType(entity, HullType.Ship)).ToString(),
            updatedAtUtc: DateTime.UtcNow.ToString("O"));
    }

// private IEnumerator AnimatePath()
    // {
    //     var pathZones = ActionGameManager.CurrentSector.ExitPath;
    //     LegendPanel.SetActive(false);
    //     PathAnimationButton.gameObject.SetActive(false);
    //
    //     var revealCount = ActionGameManager.CurrentSector.Entrance.Distance[ActionGameManager.CurrentSector.Exit];
    //     Map.StartReveal(
    //         PathAnimationDuration / revealCount * (LinkAnimationDuration / (IconAnimationDuration + LinkAnimationDuration)),
    //         PathAnimationDuration / revealCount * (IconAnimationDuration / (IconAnimationDuration + LinkAnimationDuration)));
    //     MainCamera.enabled = false;
    //     SectorCamera.targetTexture = null;
    //     Canvas.gameObject.SetActive(false);
    //     SectorCamera.gameObject.SetActive(true);
    //         
    //     var pathAnimationLerp = 0f;
    //     while (pathAnimationLerp < 1)
    //     {
    //         var currentTargetZone = pathZones[(int) (pathZones.Length * pathAnimationLerp)];
    //         _position = lerp(_position, currentTargetZone.Position, PathAnimationDamping);
    //         pathAnimationLerp += Time.deltaTime / (PathAnimationDuration * PathAnimationDurationPadding);
    //         UpdateCamera();
    //         yield return null;
    //     }
    //     
    //     LegendPanel.SetActive(true);
    //     PathAnimationButton.gameObject.SetActive(true);
    // }
    
    private void OnEnable()
    {
        _init = true;
        SectorCamera.gameObject.SetActive(true);
        var currentZone = GameManager.CurrentDaemonGalaxyZone;
        _position = currentZone?.Position ?? float2.zero;
        _viewSize = .25f;
        
        Map.StartReveal(LinkAnimationDuration, IconAnimationDuration);
        if (currentZone != null)
            Map.MarkPlayerLocation(currentZone);
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
