using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public class SectorRenderer : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    public ClickRaycaster Raycaster;
    public PropertiesPanel Properties;
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
    private Image _outputImage;
    private RenderTexture _outputTexture;
    private int2 _size;
    private bool _init;
    private float _aspectRatio;
    private float _sectorBackgroundDepth;
    private float _sectorCameraDepth;
    
    private float2 _position = float2(0.5f);
    private float _viewSize = .5f;
    
    void Start()
    {
        _outputImage = GetComponent<Image>();
        _sectorBackgroundTransform = SectorBackgroundRenderer.transform;
        _sectorBackgroundDepth = _sectorBackgroundTransform.position.z;
        _sectorCameraTransform = SectorCamera.transform;
        _sectorCameraDepth = _sectorCameraTransform.position.z;
        Raycaster.OnClickMiss += data =>
        {
            Properties.gameObject.SetActive(false);
        };
        Map.ZoneClicked.Subscribe(zone =>
        {
            Properties.gameObject.SetActive(true);
            Properties.Clear();
            Properties.Title.text = zone.Name;
            Properties.AddProperty("Owner", () => zone.Owner?.Name ?? "None");
            var otherFactions = zone.Factions
                .Where(f => f.ID != zone.Owner?.ID)
                .Select(f=>f.Name).ToArray();
            if (otherFactions.Length > 0)
                Properties.AddProperty("Factions Present", () => string.Join(", ", otherFactions));
            var density = saturate(ActionGameManager.CurrentGalaxy.Background.CloudDensity(zone.Position)/2);
            var radius = GameManager.Settings.ZoneSettings.ZoneRadius.Evaluate(density);
            var mass = GameManager.Settings.ZoneSettings.ZoneMass.Evaluate(density);
            Properties.AddProperty("Mass", () => ActionGameManager.PlayerSettings.Format(mass));
            Properties.AddProperty("Radius", () => ActionGameManager.PlayerSettings.Format(radius));
            if (zone.PackedContents == null)
            {
                Properties.AddProperty(() => "Has not been visited");
            }
            else
            {
                var planetCount = zone.PackedContents.Planets.Count(body => body is PlanetData).ToString();
                Properties.AddProperty("Planets", () => planetCount);

                var beltCount = zone.PackedContents.Planets.Count(body => body is AsteroidBeltData).ToString();
                Properties.AddProperty("Asteroid Belts", () => beltCount);

                var giantCount = zone.PackedContents.Planets.Count(body => body is GasGiantData && !(body is SunData)).ToString();
                Properties.AddProperty("Gas Giants", () => giantCount);

                var starCount = zone.PackedContents.Planets.Count(body => body is SunData).ToString();
                Properties.AddProperty("Stars", () => starCount);
                
                var stationCount = zone.PackedContents.Entities
                    .Count(entity => ((HullData) entity.Hull.Data.Value).HullType == HullType.Station)
                    .ToString();
                Properties.AddProperty("Stations", () => stationCount);
                
                var turretCount = zone.PackedContents.Entities
                    .Count(entity => ((HullData) entity.Hull.Data.Value).HullType == HullType.Turret)
                    .ToString();
                Properties.AddProperty("Turrets", () => turretCount);
                
                var shipCount = zone.PackedContents.Entities
                    .Count(entity => ((HullData) entity.Hull.Data.Value).HullType == HullType.Ship)
                    .ToString();
                Properties.AddProperty("Ships", () => shipCount);
            }
        });

        // PathAnimationButton.onClick.AddListener(() =>
        // {
        //     Map.StartCoroutine(AnimatePath());
        // });
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
        if (_outputTexture != null)
        {
            _outputTexture.Release();
            _outputTexture = null;
        }
        SectorCamera.gameObject.SetActive(false);
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
