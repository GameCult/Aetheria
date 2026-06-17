using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
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
    private const string ZoneDetailsSurfaceType = "surface-state";
    private const string ZoneDetailsSurfaceSchema = "gamecult.eve.surface.v1";
    private const string ZoneDetailsSurfaceProviderId = "aetheria";
    private const string ZoneDetailsSurfaceProviderKind = "sector.map";
    private const string ZoneDetailsSurfaceId = "aetheria.sector_map.zone_details";
    private const string CloseZoneDetailsCommand = "aetheria.sector_map.zone_details.close";

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
        var document = ResolveZoneDetailsSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.FlexEnd;
        root.style.justifyContent = Justify.FlexStart;
        root.style.paddingTop = 24;
        root.style.paddingRight = 24;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.width = 360;
        shell.style.maxWidth = 420;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 18;
        shell.style.paddingRight = 18;
        shell.style.paddingTop = 18;
        shell.style.paddingBottom = 18;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildZoneDetailsSurfaceDefinition(zone), HandleZoneDetailsSurfaceCommand));
    }

    private void HandleZoneDetailsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, CloseZoneDetailsCommand, StringComparison.Ordinal))
        {
            HideZoneDetailsSurface();
            return;
        }

        Debug.LogWarning($"Unknown sector-map zone details command: {request.Command}");
    }

    private void HideZoneDetailsSurface()
    {
        if (_zoneDetailsSurfaceDocument == null)
            return;

        _zoneDetailsSurfaceDocument.rootVisualElement.Clear();
        _zoneDetailsSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveZoneDetailsSurfaceDocument()
    {
        if (_zoneDetailsSurfaceDocument != null)
            return _zoneDetailsSurfaceDocument;

        var host = new GameObject("Aetheria Sector Zone Details Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1000;
        host.SetActive(false);
        _zoneDetailsSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildZoneDetailsSurfaceDefinition(GalaxyZone zone)
    {
        var density = saturate(ActionGameManager.CurrentGalaxy.Background.CloudDensity(zone.Position) / 2);
        var radius = GameManager.Settings.ZoneSettings.ZoneRadius.Evaluate(density);
        var mass = GameManager.Settings.ZoneSettings.ZoneMass.Evaluate(density);
        var otherFactions = zone.Factions
            .Where(faction => !faction.HasSameKey(zone.Owner))
            .Select(faction => faction.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        var children = new List<EveSurfaceComponent>
        {
            Card(
                $"{ZoneDetailsSurfaceId}.card",
                zone.Name,
                Metric($"{ZoneDetailsSurfaceId}.owner", "Owner", zone.Owner?.Name ?? "None"),
                Metric($"{ZoneDetailsSurfaceId}.mass", "Mass", ActionGameManager.RuntimePlayerSettings.Format(mass)),
                Metric($"{ZoneDetailsSurfaceId}.radius", "Radius", ActionGameManager.RuntimePlayerSettings.Format(radius)))
        };

        if (otherFactions.Length > 0)
        {
            children.Add(Text(
                $"{ZoneDetailsSurfaceId}.factions",
                $"Factions Present: {string.Join(", ", otherFactions)}"));
        }

        if (zone.Contents == null)
        {
            children.Add(Text(
                $"{ZoneDetailsSurfaceId}.unvisited",
                "Has not been visited."));
        }
        else
        {
            var runtimeZone = zone.Contents;
            children.Add(Card(
                $"{ZoneDetailsSurfaceId}.contents",
                "Contents",
                Metric(
                    $"{ZoneDetailsSurfaceId}.planets",
                    "Planets",
                    runtimeZone.PlanetInstances.Values.Count(body => !(body is GasGiant)).ToString()),
                Metric(
                    $"{ZoneDetailsSurfaceId}.belts",
                    "Asteroid Belts",
                    runtimeZone.AsteroidBelts.Count.ToString()),
                Metric(
                    $"{ZoneDetailsSurfaceId}.giants",
                    "Gas Giants",
                    runtimeZone.PlanetInstances.Values.Count(body => body is GasGiant && !(body is Sun)).ToString()),
                Metric(
                    $"{ZoneDetailsSurfaceId}.stars",
                    "Stars",
                    runtimeZone.PlanetInstances.Values.Count(body => body is Sun).ToString()),
                Metric(
                    $"{ZoneDetailsSurfaceId}.stations",
                    "Stations",
                    runtimeZone.Entities.Count(entity => HasHullType(entity, HullType.Station)).ToString()),
                Metric(
                    $"{ZoneDetailsSurfaceId}.turrets",
                    "Turrets",
                    runtimeZone.Entities.Count(entity => HasHullType(entity, HullType.Turret)).ToString()),
                Metric(
                    $"{ZoneDetailsSurfaceId}.ships",
                    "Ships",
                    runtimeZone.Entities.Count(entity => HasHullType(entity, HullType.Ship)).ToString())));
        }

        children.Add(ButtonRow(
            $"{ZoneDetailsSurfaceId}.actions",
            Button($"{ZoneDetailsSurfaceId}.close", "Close", CloseZoneDetailsCommand)));

        return new EveSurfaceDocument(
            ZoneDetailsSurfaceType,
            ZoneDetailsSurfaceSchema,
            ZoneDetailsSurfaceProviderId,
            ZoneDetailsSurfaceProviderKind,
            zone.Name,
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                ZoneDetailsSurfaceId,
                Node(
                    $"{ZoneDetailsSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    children.ToArray()),
                Array.Empty<EveStyleToken>()),
            new[]
            {
                new EveCommandTemplate(CloseZoneDetailsCommand, "Close", "unity-uitoolkit")
            });
    }

    private static EveSurfaceComponent Card(
        string id,
        string title,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "card", new[] { ("title", title) }, children);
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", new[] { ("label", label), ("value", value) });
    }

    private static EveSurfaceComponent Text(string id, string value)
    {
        return Node(id, "text", new[] { ("value", value) });
    }

    private static EveSurfaceComponent Button(string id, string label, string command)
    {
        return Node(id, "control.button", new[] { ("label", label), ("command", command) });
    }

    private static EveSurfaceComponent ButtonRow(
        string id,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
    }

    private static EveSurfaceComponent Node(
        string id,
        string kind,
        IEnumerable<(string Key, string Value)> props,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceComponent(
            id,
            kind,
            props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
            children ?? Array.Empty<EveSurfaceComponent>());
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
            Destroy(_zoneDetailsSurfaceDocument.gameObject);
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
