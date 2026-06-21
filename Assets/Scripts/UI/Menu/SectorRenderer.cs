using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Unity;
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

    private bool HasHullType(Entity entity, HullType hullType)
    {
        var typedItem = entity?.ItemManager.GetRuntimeItem(entity.Hull);
        return typedItem != null &&
               string.Equals(typedItem.HullType, hullType.ToString(), StringComparison.Ordinal);
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
        var density = saturate(ActionGameManager.ObservedGalaxy.Background.CloudDensity(zone.Position) / 2);
        var radius = GameManager.Settings.ZoneSettings.ZoneRadius.Evaluate(density);
        var mass = GameManager.Settings.ZoneSettings.ZoneMass.Evaluate(density);
        var otherFactions = zone.Factions
            .Where(faction => !faction.HasSameKey(zone.Owner))
            .Select(faction => faction.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        if (zone.Contents == null)
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

        var runtimeZone = zone.Contents;
        return new AetheriaRuntimeZoneDetailsSurfaceState(
            zone.Name,
            zone.Owner?.Name ?? "None",
            ActionGameManager.RuntimePlayerSettings.Format(mass),
            ActionGameManager.RuntimePlayerSettings.Format(radius),
            otherFactions,
            hasContents: true,
            planets: runtimeZone.PlanetInstances.Values.Count(body => !(body is GasGiant)).ToString(),
            asteroidBelts: runtimeZone.AsteroidBelts.Count.ToString(),
            gasGiants: runtimeZone.PlanetInstances.Values.Count(body => body is GasGiant && !(body is Sun)).ToString(),
            stars: runtimeZone.PlanetInstances.Values.Count(body => body is Sun).ToString(),
            stations: runtimeZone.Entities.Count(entity => HasHullType(entity, HullType.Station)).ToString(),
            turrets: runtimeZone.Entities.Count(entity => HasHullType(entity, HullType.Turret)).ToString(),
            ships: runtimeZone.Entities.Count(entity => HasHullType(entity, HullType.Ship)).ToString(),
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
        _position = GameManager.Zone.GalaxyZone.Position;
        _viewSize = .25f;
        
        Map.StartReveal(LinkAnimationDuration, IconAnimationDuration);
        Map.MarkPlayerLocation(GameManager.CurrentEntity.Zone.GalaxyZone);
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
