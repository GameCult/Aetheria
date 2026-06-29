/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public class ZoneRenderer : MonoBehaviour
{
    public Camera MainCamera;
    public Transform WormholePrefab;
    public float EntityFadeTime;
    public Transform FogCameraParent;
    public BodySettingsCollection[] BodySettingsCollections;
    public Transform ZoneRoot;
    public Transform SectorBrushes;
    public MeshRenderer SectorBoundaryBrush;
    public CinemachineVirtualCamera SceneCamera;
    public Camera[] FogCameras;
    public Camera[] MinimapCameras;
    public bool UseRenderSplatTextureLayers = true;
    public Material FogMaterial;
    public float FogFarFadeFraction = .125f;
    public float FarPlaneDistanceMultiplier = 2;
    public InstancedMesh[] AsteroidMeshes;
    public int AsteroidSpritesheetWidth = 4;
    public int AsteroidSpritesheetHeight = 4;
    public LODHandler LODHandler;
    public Slime SlimeRenderer;
    public Camera SlimeGravityCamera;
    public GridObject SimpleCommodityPickup;
    public GridObject CompoundCommodityPickup;
    public GridObject GearPickup;
    public GridObject WeaponPickup;
    public Material[] MapGravityMaterials;

    [Header("Tour")] public bool Tour;
    public float TourSwitchTime = 5f;
    public float TourFollowDistance = 30f;
    public float TourHeightOffset = 15;
    public float TourFollowOffsetDegrees;

    // public Mesh[] AsteroidMeshes;
    // public Material AsteroidMaterial;

    [Header("Prefabs")]
    public MeshFilter AsteroidBeltUI;
    public PlanetObject Planet;
    public GasGiantObject GasGiant;
    public SunObject Sun;
    public Prototype CompassIconPrototype;

    [Header("Icons")]
    public Sprite OrbitalIcon;
    public Sprite WormholeIcon;

    [HideInInspector] public Dictionary<Entity, EntityInstance> EntityInstances = new Dictionary<Entity, EntityInstance>();

    private readonly Dictionary<int, EntityInstance> _entityInstancesByDaemonIndex = new Dictionary<int, EntityInstance>();
    private readonly Dictionary<string, PlanetObject> _bodyViewsByBodyKey = new Dictionary<string, PlanetObject>(StringComparer.Ordinal);
    private Dictionary<string, AsteroidBeltUI> _beltObjects = new Dictionary<string, AsteroidBeltUI>(StringComparer.Ordinal);
    private Dictionary<string, InstancedMesh[]> _beltMeshes = new Dictionary<string, InstancedMesh[]>(StringComparer.Ordinal);
    private Dictionary<string, Matrix4x4[][]> _beltMatrices = new Dictionary<string, Matrix4x4[][]>(StringComparer.Ordinal);
    private float _viewDistance;
    //private float _maxDepth;
    private float _minimapDistance;

    private float _tourTimer;
    private List<(Transform, Transform)> _tourPlanets = new List<(Transform, Transform)>();
    private CinemachineTransposer _transposer;
    private PlanetObject _root;
    private bool _rootFound;
    private Entity _perspectiveEntity;
    private AetheriaDaemonObserver _daemonObserver;
    private PlanetObject[] _suns;
    private bool _showAsteroidUI;
    private string _daemonCurrentEntityKey = "";
    private double _daemonSimulationTimeSeconds;
    private readonly List<AetheriaRuntimeDaemonBodyView> _daemonBodyViews = new List<AetheriaRuntimeDaemonBodyView>();
    private readonly HashSet<string> _daemonVisibleBodyKeys = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<AetheriaRuntimeZoneRenderBodyPose> _daemonBodyPoses = new List<AetheriaRuntimeZoneRenderBodyPose>();
    private readonly Dictionary<string, AetheriaRuntimeZoneRenderBodyPose> _daemonBodyPosesByBodyKey =
        new Dictionary<string, AetheriaRuntimeZoneRenderBodyPose>(StringComparer.Ordinal);
    private readonly List<AetheriaRuntimeZoneRenderAsteroidBeltPose> _daemonAsteroidBeltPoses =
        new List<AetheriaRuntimeZoneRenderAsteroidBeltPose>();
    private readonly List<AetheriaRuntimeZoneRenderAsteroidInstancePose> _visibleAsteroidInstancePoses =
        new List<AetheriaRuntimeZoneRenderAsteroidInstancePose>();
    private readonly List<AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkers =
        new List<AetheriaRuntimeDaemonCompassMarker>();
    private readonly Dictionary<int, AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkersByEntityIndex =
        new Dictionary<int, AetheriaRuntimeDaemonCompassMarker>();
    private readonly Dictionary<int, AetheriaRuntimeZoneTargetRow> _daemonTargetRowsByEntityIndex =
        new Dictionary<int, AetheriaRuntimeZoneTargetRow>();
    private readonly List<AetheriaRuntimeZoneContactRow> _daemonContactRows =
        new List<AetheriaRuntimeZoneContactRow>();
    private IReadOnlyDictionary<int, Entity> _observedEntitySnapshotsByDaemonIndex;
    private readonly List<int> _daemonPresentationEntityIndices = new List<int>();
    private readonly HashSet<int> _daemonPresentationEntityIndicesSet = new HashSet<int>();
    private readonly List<int> _daemonVisibleEntityIndices = new List<int>();
    private readonly HashSet<int> _daemonVisibleEntityIndicesSet = new HashSet<int>();
    private readonly HashSet<int> _visibleDaemonEntityIndices = new HashSet<int>();
    private readonly List<AetheriaRuntimeDaemonWormholeExit> _daemonWormholeExits =
        new List<AetheriaRuntimeDaemonWormholeExit>();
    private IReadOnlyList<AetheriaRuntimeZoneRenderBodyPose> _zoneRenderBodyPoses =
        Array.Empty<AetheriaRuntimeZoneRenderBodyPose>();
    private IReadOnlyList<AetheriaRuntimeZoneRenderAsteroidBeltPose> _zoneRenderAsteroidBeltPoses =
        Array.Empty<AetheriaRuntimeZoneRenderAsteroidBeltPose>();
    private IReadOnlyList<AetheriaRuntimeBodySnapshotCommit> _zoneRenderBodies =
        Array.Empty<AetheriaRuntimeBodySnapshotCommit>();
    private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
    private CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts;
    private AetheriaRuntimeRtsViewportBounds _objectsViewportBounds;
    private CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> _objectsViewport;

    public Dictionary<int, (GameObject gravity, CompassIcon icon)> WormholeInstances = new Dictionary<int, (GameObject, CompassIcon)>();
    private List<ItemPickup> _loot = new List<ItemPickup>();

    public IReadOnlyDictionary<int, EntityInstance> DaemonEntityInstances => _entityInstancesByDaemonIndex;
    public IReadOnlyList<ItemPickup> ActiveLoot => _loot;
    public AetheriaRuntimeDaemonRenderSettings RenderSettings { get; set; }
    private Func<AetheriaRuntimeLoadoutItemCommit, ItemInstance> _createDroppedPickupItem;

    public Entity PerspectiveEntity
    {
        get => _perspectiveEntity;
        set
        {
            _perspectiveEntity = value;
            _visibleDaemonEntityIndices.Clear();
            foreach (var entity in _entityInstancesByDaemonIndex.Values)
                entity.FadeOut(EntityFadeTime);
            RefreshDaemonVisibleEntityInstances();
        }
    }

    public float ViewDistance
    {
        set
        {
            _viewDistance = value;
            foreach (var camera in FogCameras ?? Array.Empty<Camera>())
                if (camera != null)
                    camera.orthographicSize = value;
            if (SceneCamera != null)
                SceneCamera.m_Lens.FarClipPlane = value * FarPlaneDistanceMultiplier;
            if (FogMaterial != null)
            {
                FogMaterial.SetFloat("_DepthCeiling", value);
                FogMaterial.SetFloat("_DepthBlend", FogFarFadeFraction * value);
            }
        }
    }

    public float MinimapDistance
    {
        set
        {
            _minimapDistance = value;
            foreach (var camera in MinimapCameras ?? Array.Empty<Camera>())
                if (camera != null)
                    camera.orthographicSize = value;
            if (RenderSettings != null)
                SetIconSize((float)RenderSettings.ResolveMinimapIconSize(value));
            // MinimapGravityQuad.transform.localScale = value * 2 * Vector3.one;
        }
    }

    public bool ShowAsteroidUI
    {
        get { return _showAsteroidUI; }
        set
        {
            if (value != _showAsteroidUI)
            {
                _showAsteroidUI = value;
                foreach (var beltUI in _beltObjects.Values)
                {
                    beltUI.Filter.gameObject.SetActive(_showAsteroidUI);
                }
            }
        }
    }

    public void SetIconSize(float size)
    {
        foreach(var entityInstance in _entityInstancesByDaemonIndex.Values) entityInstance.MapIcon.transform.localScale = Vector3.one * size;
        foreach(var planet in _bodyViewsByBodyKey.Values) planet.Icon.transform.localScale = Vector3.one * size;
    }

    public bool TryGetBodyView(string bodyKey, out PlanetObject bodyView)
    {
        return _bodyViewsByBodyKey.TryGetValue(bodyKey ?? "", out bodyView);
    }

    public bool TryGetEntityInstance(int daemonEntityIndex, out EntityInstance instance)
    {
        return _entityInstancesByDaemonIndex.TryGetValue(daemonEntityIndex, out instance);
    }

    public bool TryGetEntityInstance(Entity entity, out EntityInstance instance)
    {
        instance = null;
        return entity != null &&
               entity.DaemonEntityIndex >= 0 &&
               _entityInstancesByDaemonIndex.TryGetValue(entity.DaemonEntityIndex, out instance);
    }

    public bool TryGetDaemonTargetDistance(int daemonEntityIndex, out float distance)
    {
        distance = 0f;
        if (!_daemonTargetRowsByEntityIndex.TryGetValue(daemonEntityIndex, out var target))
        {
            return false;
        }

        distance = (float)target.Distance;
        return true;
    }

    public AetheriaRuntimeCatalogItem FindCatalogItem(ItemInstance item)
    {
        return ResolveCatalog()?.FindItem(item, x => x.ItemKey);
    }

    void Start()
    {
        var bigBounds = new Bounds(Vector3.zero, Vector3.one * 1024);
        foreach (var mesh in AsteroidMeshes) mesh.Mesh.bounds = bigBounds;
        ViewDistance = (float)RenderSettings.DefaultViewDistance;
        MinimapDistance = (float)RenderSettings.ResolveDefaultMinimapDistance();

        _tourTimer = TourSwitchTime;
        _transposer = SceneCamera.GetCinemachineComponent<CinemachineTransposer>();
        DisableLegacyTextureCameras();
    }

    public void LoadDaemonZoneView(
        IReadOnlyDictionary<int, Entity> observedEntitySnapshotsByDaemonIndex,
        AetheriaRuntimeZoneRenderDocument render)
    {
        ClearZone();
        _observedEntitySnapshotsByDaemonIndex = observedEntitySnapshotsByDaemonIndex;
        ApplyZoneRender(render);
        RefreshDaemonVisibleEntityInstances();
        SyncDaemonEntityInstances();

        foreach (var exit in _daemonWormholeExits)
            AddWormhole(exit);
    }

    public void ApplyZoneRender(AetheriaRuntimeZoneRenderDocument render)
    {
        _daemonCurrentEntityKey = render?.CurrentEntityKey ?? "";
        _daemonSimulationTimeSeconds = render?.SimulationTimeSeconds ?? 0;
        _zoneRenderBodyPoses = render?.BodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>();
        _zoneRenderAsteroidBeltPoses = render?.AsteroidBeltPoses ?? Array.Empty<AetheriaRuntimeZoneRenderAsteroidBeltPose>();
        _zoneRenderBodies = render?.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>();
        RefreshDaemonContactRows();
        var zoneRenderRadius = (float)Math.Max(0, render?.ZoneRenderRadius ?? 2000);
        SectorBrushes.localScale = zoneRenderRadius * 2 * Vector3.one;
        if (SlimeGravityCamera != null)
            SlimeGravityCamera.orthographicSize = zoneRenderRadius;
        SlimeRenderer.ZoneRadius = zoneRenderRadius;
        DisableLegacyTextureCameras();
        RefreshDaemonBodyPoses();
        RefreshDaemonAsteroidBeltPoses();
        SyncDaemonBodyViews();
        _daemonWormholeExits.Clear();
        foreach (var exit in render?.WormholeExits ?? Array.Empty<AetheriaRuntimeZoneRenderWormholeExit>())
        {
            _daemonWormholeExits.Add(new AetheriaRuntimeDaemonWormholeExit(
                exit.TargetZoneIndex,
                exit.DirectionX,
                exit.DirectionZ,
                exit.PositionX,
                exit.PositionZ));
        }
    }

    public void AddWormhole(AetheriaRuntimeDaemonWormholeExit exit)
    {
        var instance = Instantiate(WormholePrefab);
        instance.position = new Vector3((float)exit.PositionX, 0, (float)exit.PositionZ);
        var icon = CompassIconPrototype.Instantiate<CompassIcon>();
        icon.Icon.sprite = WormholeIcon;
        WormholeInstances[exit.TargetZoneIndex] = (instance.gameObject, icon);
    }

    public void ClearZone()
    {
        foreach (var wormhole in WormholeInstances.Values)
        {
            Destroy(wormhole.gravity);
            wormhole.icon.GetComponent<Prototype>().ReturnToPool();
        }
        WormholeInstances.Clear();

        foreach(var gridObject in _loot) 
            if(gridObject) Destroy(gridObject.gameObject);
        _loot.Clear();

        foreach (var entity in EntityInstances.Keys.ToArray())
        {
            Debug.Log($"Unloading entity {entity.Name} from rendered entity instances during clear.");
            UnloadEntity(entity);
        }
        _entityInstancesByDaemonIndex.Clear();

        if (_bodyViewsByBodyKey.Count > 0)
        {
            foreach (var planet in _bodyViewsByBodyKey.Values)
            {
                DestroyImmediate(planet.gameObject);
            }

            _bodyViewsByBodyKey.Clear();
            foreach (var beltObject in _beltObjects.Values)
            {
                Destroy(beltObject.Filter);
            }

            _beltObjects.Clear();
            _beltMeshes.Clear();
            _beltMatrices.Clear();
            _tourPlanets.Clear();
        }
    }

    void LoadEntity(Entity entity)
    {
        var typedHull = FindTypedZoneItem(entity.Hull);
        if (typedHull == null || string.IsNullOrWhiteSpace(typedHull.HullPrefab))
        {
            Debug.LogWarning($"Failed to instantiate {entity.Name} with missing typed hull prefab.");
            return;
        }

        EntityInstance instance;
        if (entity is Ship)
        {
            instance = Instantiate(UnityHelpers.LoadAsset<GameObject>(typedHull.HullPrefab), ZoneRoot).GetComponent<ShipInstance>();
            if (instance == null)
            {
                Debug.LogWarning($"Failed to instantiate {typedHull.Name} ship with invalid prefab: no ShipInstance component!");
                return;
            }
        }
        else
        {
            instance = Instantiate(UnityHelpers.LoadAsset<GameObject>(typedHull.HullPrefab), ZoneRoot).GetComponent<EntityInstance>();
            if (instance == null)
            {
                Debug.LogWarning($"Failed to instantiate {typedHull.Name} entity with invalid prefab: no EntityInstance component!");
                return;
            }
            if (string.Equals(typedHull.HullType, nameof(HullType.Station), StringComparison.Ordinal))
            {
                instance.CompassIcon = CompassIconPrototype.Instantiate<CompassIcon>();
                instance.CompassIcon.Icon.sprite = OrbitalIcon;
            }
        }

        instance.SetEntity(this, entity);
        
        EntityInstances.Add(entity, instance);
        if (entity.DaemonEntityIndex >= 0)
            _entityInstancesByDaemonIndex[entity.DaemonEntityIndex] = instance;
    }

    public void UnloadEntity(Entity entity)
    {
        foreach (var item in entity.Equipment)
        {
            foreach (var behavior in item.Behaviors)
            {
                if (behavior is IEventBehavior eventBehavior)
                    eventBehavior.ResetEvents();
            }
        }

        if (!EntityInstances.TryGetValue(entity, out var instance))
            return;

        Destroy(instance.gameObject);
        EntityInstances.Remove(entity);
        if (entity.DaemonEntityIndex >= 0)
            _entityInstancesByDaemonIndex.Remove(entity.DaemonEntityIndex);
    }

    void LoadAsteroidBelt(AetheriaRuntimeZoneRenderAsteroidBeltPose beltPose)
    {
        var bodyKey = beltPose.BodyKey;
        if (bodyKey.Length == 0)
            return;

        var meshes = AsteroidMeshes.ToList();
        while (meshes.Count > RenderSettings.AsteroidMeshCount)
            meshes.RemoveAt(Random.Range(0, meshes.Count));
        _beltMeshes[bodyKey] = meshes.ToArray();
        _beltMatrices[bodyKey] = new Matrix4x4[meshes.Count][];
        var asteroidCount = beltPose.AsteroidCount;
        var count = meshes.Count == 0 ? 0 : asteroidCount / meshes.Count;
        var remainder = asteroidCount - count * meshes.Count;
        for (int i = 0; i < meshes.Count; i++)
        {
            _beltMatrices[bodyKey][i] = new Matrix4x4[i < meshes.Count - 1 ? count : count + remainder];
        }

        var beltObject = Instantiate(AsteroidBeltUI, ZoneRoot);
        var belt = new AsteroidBeltUI(
            asteroidCount,
            (float)beltPose.Radius,
            beltObject,
            AsteroidSpritesheetWidth,
            AsteroidSpritesheetHeight,
            (float)RenderSettings.MinimapAsteroidSize);
        _beltObjects[bodyKey] = belt;

        LODHandler.FindPlanets();
    }

    void LoadPlanet(AetheriaRuntimeBodySnapshotCommit body)
    {
        var bodyKey = body.BodyKey ?? "";
        if (bodyKey.Length == 0)
            return;

        PlanetObject planet;
        var kind = body.Kind ?? "";
        var mass = (float)body.Mass;
        var isSun = string.Equals(kind, "sun", StringComparison.OrdinalIgnoreCase);
        var isGasGiant = isSun || string.Equals(kind, "gas_giant", StringComparison.OrdinalIgnoreCase);
        if (isGasGiant)
        {
            if (isSun)
            {
                planet = Instantiate(Sun, ZoneRoot);
                var sunObject = (SunObject) planet;
                var sunVisual = body.SunVisual ?? new AetheriaRuntimeSunVisualCommit();
                var lightRadius = (float)RenderSettings.ResolveLightRadius(mass) * (float)sunVisual.LightRadiusMultiplier;
                sunObject.Light.color = new Color((float)sunVisual.LightColorX, (float)sunVisual.LightColorY, (float)sunVisual.LightColorZ);
                sunObject.Light.range = lightRadius;
                sunObject.FogTint.transform.localScale = lightRadius * Vector3.one;
                sunObject.FogTint.material.SetColor(
                    "_Color",
                    new Color((float)sunVisual.FogTintColorX, (float)sunVisual.FogTintColorY, (float)sunVisual.FogTintColorZ));
            }
            else planet = Instantiate(GasGiant, ZoneRoot);

            var gas = (GasGiantObject) planet;
            var colors = ToUnityColors(body.GasGiantVisual?.Colors);
            gas.Body.material.SetTexture("_ColorRamp", colors.ToGradient(!isSun).ToTexture());
            gas.GravityWaves.transform.localScale = (float)body.GravityWaveRadius * Vector3.one;
            gas.GravityWaves.material.SetFloat("_Depth", (float)body.GravityWaveDepth);
            gas.GravityWaves.material.SetFloat("_Frequency", (float)RenderSettings.ResolveGravityWaveFrequency(mass));
        }
        else
        {
            planet = Instantiate(Planet, ZoneRoot);
            var possibleSettings = (BodySettingsCollections ?? Array.Empty<BodySettingsCollection>())
                .Where(p => p.MinimumMass < mass)
                .MaxBy(p => p.MinimumMass)?.BodySettings ?? Array.Empty<CelestialBodySettings>();
            if (possibleSettings.Length > 0)
                planet.Generator.body = possibleSettings[Random.Range(0, possibleSettings.Length)];
            //Debug.Log($"Generating planet with {mass} mass! Choosing {planet.Generator.body.name} settings!");
            //planet.Icon.material.mainTexture = planetInstance.Mass > Context.GlobalData.PlanetMass ? PlanetIcon : PlanetoidIcon;
        }

        var bodyRadius = (float)RenderSettings.ResolveBodyRadius(mass) * (float)body.BodyRadiusMultiplier;
        var gravityWellRadius = Mathf.Max(0f, (float)body.GravityInfluenceRadius);
        var gravityWellDepth = (float)body.GravityWellDepth;
        planet.Body.transform.localScale = bodyRadius * Vector3.one;
        planet.GravityWell.transform.localScale = gravityWellRadius * Vector3.one;
        // var depth = planetInstance.GravityWellDepth;
        // if (depth > _maxDepth) _maxDepth = depth;
        planet.GravityWell.material.SetFloat("_Depth", gravityWellDepth);
        planet.Icon.transform.position = new Vector3(0, -gravityWellDepth, 0);
        planet.Icon.transform.localScale = (float)RenderSettings.ResolveBodyIconSize(mass) * Vector3.one;


        _bodyViewsByBodyKey[bodyKey] = planet;
        if (!_rootFound)
        {
            _rootFound = true;
            _root = planet;
        }

        LODHandler.FindPlanets();
    }

    private static float4[] ToUnityColors(IReadOnlyList<AetheriaRuntimeColorCommit> colors)
    {
        var converted = (colors ?? Array.Empty<AetheriaRuntimeColorCommit>())
            .Where(color => color != null)
            .Select(color => new float4((float)color.X, (float)color.Y, (float)color.Z, (float)color.W))
            .ToArray();
        return converted.Length > 0
            ? converted
            : new[] { new float4(0.35f, 0.45f, 0.8f, 0), new float4(0.85f, 0.9f, 1f, 1) };
    }

    // private void Update()
    // {
    //     if (Tour)
    //     {
    //         _tourTimer -= UnityEngine.Time.deltaTime;
    //         if (_tourTimer < 0)
    //         {
    //             _tourTimer = TourSwitchTime;
    //             _tourIndex = (_tourIndex + 1) % _tourPlanets.Count;
    //             SceneCamera.Follow = _tourPlanets[_tourIndex].Item1;
    //             SceneCamera.LookAt = _tourPlanets[_tourIndex].Item2;
    //             if(_tourIndex==0) Debug.Log("Tour Complete!");
    //         }
    //         // if(_tourIndex>=0)
    //         // {
    //         //     var offset = (SceneCamera.Follow.position - SceneCamera.LookAt.position);
    //         //     offset.y = 0;
    //         //     offset = offset.normalized * TourFollowDistance;
    //         //     offset.y = TourHeightOffset;
    //         //     offset = Quaternion.AngleAxis(TourFollowOffsetDegrees, Vector3.up) * offset;
    //         //     _transposer.m_FollowOffset = offset;
    //         // }
    //     }
    // }

    void Update()
    {
        var maxDepth = 0f;
        foreach (var loot in _loot)
        {
            loot.ViewOrigin = PerspectiveEntity.CultPosition;
            loot.ViewDirection = PerspectiveEntity.CultLookDirection;
        }
        
        // if (SlimeRenderer.SpawnPositions.Length != _suns.Length)
        //     SlimeRenderer.SpawnPositions = new Vector2[_suns.Length];
        // for (var i = 0; i < _suns.Length; i++)
        // {
        //     SlimeRenderer.SpawnPositions[i] = _suns[i].Body.transform.position.Flatland();
        // }
        
        Shader.SetGlobalFloat("_AsteroidVerticalOffset", (float)RenderSettings.AsteroidVerticalOffset);
        RefreshDaemonBodyPoses();
        RefreshDaemonAsteroidBeltPoses();
        SyncDaemonBodyViews();
        RefreshDaemonVisibleEntityInstances();
        SyncDaemonEntityInstances();
        RefreshDaemonCompassMarkers();

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(MainCamera);
        bool isVisible(Bounds bounds) => GeometryUtility.TestPlanesAABB(planes, bounds);
        
        foreach (var beltPose in _daemonAsteroidBeltPoses)
        {
            var key = beltPose.BodyKey;
            if (string.IsNullOrWhiteSpace(key) ||
                !_beltMeshes.TryGetValue(key, out var meshes) ||
                !_beltMatrices.TryGetValue(key, out var matrices))
                continue;

            var center = new float2((float)beltPose.CenterX, (float)beltPose.CenterZ);
            var height = GetTerrainHeight(center);
            var beltIsVisible = isVisible(new Bounds(
                new Vector3(center.x,height,center.y),
                new Vector3((float)beltPose.Radius * 2,100,(float)beltPose.Radius * 2)));
            if (beltIsVisible || _showAsteroidUI)
            {
                _visibleAsteroidInstancePoses.Clear();
                _visibleAsteroidInstancePoses.AddRange(
                    beltPose.InstancePoses ?? Array.Empty<AetheriaRuntimeZoneRenderAsteroidInstancePose>());
            }

            if(beltIsVisible)
            {
                var tx = 0;
                for (int i = 0; i < meshes.Length; i++)
                {
                    for (int t = 0; t < matrices[i].Length; t++)
                    {
                        if (tx >= _visibleAsteroidInstancePoses.Count)
                        {
                            matrices[i][t] = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero);
                            continue;
                        }

                        var asteroidPose = _visibleAsteroidInstancePoses[tx];
                        matrices[i][t] = Matrix4x4.TRS(new Vector3((float)asteroidPose.PositionX,0,(float)asteroidPose.PositionZ),
                            Quaternion.Euler(
                                cos((float)asteroidPose.Rotation + (float)i / meshes.Length) * 100,
                                sin((float)asteroidPose.Rotation + (float)i / meshes.Length) * 100,
                                (float)tx / Math.Max(1, _visibleAsteroidInstancePoses.Count) * 360),
                            Vector3.one * (float)asteroidPose.Size);
                        tx++;
                    }

                    Graphics.DrawMeshInstanced(meshes[i].Mesh, 0, meshes[i].Material, matrices[i]);
                }
            }

            if(_showAsteroidUI)
                _beltObjects[key].Update(_visibleAsteroidInstancePoses, center, height, (float)beltPose.Radius);
        }

        foreach (var planet in _bodyViewsByBodyKey)
        {
            if (!_daemonBodyPosesByBodyKey.TryGetValue(planet.Key, out var pose))
                continue;

            var p = new float2((float)pose.CenterX, (float)pose.CenterZ);
            var height = GetTerrainHeight(p);
            if (-height > maxDepth) maxDepth = -height;
            planet.Value.transform.position = new Vector3(p.x, 0, p.y);
            var bodyRadius = planet.Value.Body.transform.localScale.x;
            planet.Value.Body.transform.localPosition = new Vector3(0, height + bodyRadius * 2, 0);
            if (planet.Value is GasGiantObject gasGiantObject)
            {
                gasGiantObject.GravityWaves.material.SetFloat("_Phase",
                    (float)(DaemonSimulationTimeSeconds * pose.GravityWaveSpeed));
                if (!string.Equals(pose.Kind, "sun", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = new float2((float)pose.ParentCenterX, (float)pose.ParentCenterZ);
                    var toParent = normalize(parent - p);
                    gasGiantObject.SunMaterial.LightingDirection = new Vector3(toParent.x, 0, toParent.y);
                }
            }
            else planet.Value.Body.transform.rotation *= Quaternion.AngleAxis((float)RenderSettings.PlanetRotationSpeed, Vector3.up);
        }

        foreach (var entityInstance in _entityInstancesByDaemonIndex.Values)
        {
            if(entityInstance.CompassIcon)
            {
                AetheriaRuntimeDaemonCompassMarker marker = default;
                var active = entityInstance.DaemonEntityIndex >= 0 &&
                    _daemonCompassMarkersByEntityIndex.TryGetValue(entityInstance.DaemonEntityIndex, out marker);
                entityInstance.CompassIcon.gameObject.SetActive(active);
                if (active)
                {
                    entityInstance.CompassIcon.transform.rotation = Quaternion.Euler(
                        0,
                        0,
                        Mathf.Atan2((float)marker.DeltaZ, (float)marker.DeltaX) * Mathf.Rad2Deg - 90);
                }
            }
        }

        foreach (var wormhole in WormholeInstances.Values)
        {
            var difference = wormhole.gravity.transform.position.Flatland() - (Vector2)AetheriaMath.ToUnity(PerspectiveEntity.CultPositionXZ);
            var distance = difference.magnitude;
            wormhole.icon.gameObject.SetActive(distance > _minimapDistance);
            wormhole.icon.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg - 90);
        }

        //var fogPos = FogCameraParent.position;
        SectorBoundaryBrush.material.SetFloat("_Power", (float)RenderSettings.ZoneBoundaryPower);
        SectorBoundaryBrush.material.SetFloat("_Depth", (float)RenderSettings.ZoneBoundaryDepth);
        var gravityBand = QueryGravityTerrainBand(
            RenderSettings.MinimapZoneGravityRange,
            maxDepth);
        foreach (var mat in MapGravityMaterials)
        {
            mat.SetFloat("_StartDepth", (float)gravityBand.StartDepth);
            mat.SetFloat("_DepthRange", (float)gravityBand.DepthRange);
        }
        //Shader.SetGlobalFloat("_GridOffset", Settings.PlanetSettings.ZoneBoundaryFog);
        // var gravPos = MinimapGravityQuad.transform.position;
        // gravPos.y = -Settings.PlanetSettings.ZoneDepth - _maxDepth;
        // MinimapGravityQuad.transform.position = gravPos;
        // MinimapTintQuad.transform.position = gravPos - Vector3.up*10;
    }

    private void SyncDaemonBodyViews()
    {
        _daemonBodyViews.Clear();
        var bodySnapshotsByBodyKey = _zoneRenderBodies
            .Where(body => body != null && !string.IsNullOrWhiteSpace(body.BodyKey))
            .ToDictionary(body => body.BodyKey, StringComparer.Ordinal);
        foreach (var bodyPose in _zoneRenderBodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>())
        {
            if (bodyPose == null ||
                string.IsNullOrWhiteSpace(bodyPose.BodyKey) ||
                !bodySnapshotsByBodyKey.TryGetValue(bodyPose.BodyKey, out var body))
            {
                continue;
            }

            _daemonBodyViews.Add(new AetheriaRuntimeDaemonBodyView(
                body,
                ToDaemonBodyPose(bodyPose),
                IsAsteroidBelt(body)));
        }
        var beltPosesByBodyKey = _daemonAsteroidBeltPoses
            .Where(pose => !string.IsNullOrWhiteSpace(pose.BodyKey))
            .ToDictionary(pose => pose.BodyKey, StringComparer.Ordinal);
        _daemonVisibleBodyKeys.Clear();
        foreach (var bodyView in _daemonBodyViews)
        {
            var body = bodyView.Body;
            var bodyKey = body.BodyKey ?? "";
            if (bodyKey.Length == 0)
                continue;

            _daemonVisibleBodyKeys.Add(bodyKey);
            if (_bodyViewsByBodyKey.ContainsKey(bodyKey) || _beltObjects.ContainsKey(bodyKey))
                continue;

            if (bodyView.IsAsteroidBelt)
            {
                if (beltPosesByBodyKey.TryGetValue(bodyKey, out var beltPose))
                    LoadAsteroidBelt(beltPose);
            }
            else
            {
                LoadPlanet(body);
            }
        }

        foreach (var bodyKey in _bodyViewsByBodyKey.Keys.ToArray())
        {
            if (!_daemonVisibleBodyKeys.Contains(bodyKey))
                UnloadBodyView(bodyKey);
        }

        foreach (var bodyKey in _beltObjects.Keys.ToArray())
        {
            if (!_daemonVisibleBodyKeys.Contains(bodyKey))
                UnloadBodyView(bodyKey);
        }

        _suns = _bodyViewsByBodyKey.Values.Where(p => p is SunObject).ToArray();
    }

    private void DisableLegacyTextureCameras()
    {
        if (!UseRenderSplatTextureLayers)
            return;

        foreach (var camera in FogCameras ?? Array.Empty<Camera>())
            if (camera != null)
                camera.gameObject.SetActive(false);

        if (SlimeGravityCamera != null)
            SlimeGravityCamera.gameObject.SetActive(false);
    }

    private AetheriaRuntimeXzRect ResolveDaemonRenderViewport()
    {
        var center = PerspectiveEntity?.CultPositionXZ ?? default;
        var range = Math.Max(
            Math.Max(_viewDistance, _minimapDistance),
            (float)RenderSettings.MinimapZoneGravityRange);
        range = Math.Max(range, 1f);
        return new AetheriaRuntimeXzRect(
            center.x - range,
            center.y - range,
            center.x + range,
            center.y + range);
    }

    private static AetheriaRuntimeRtsViewportBounds ToViewportBounds(AetheriaRuntimeXzRect viewport)
    {
        return new AetheriaRuntimeRtsViewportBounds
        {
            MinX = viewport.MinX,
            MinY = viewport.MinZ,
            MaxX = viewport.MaxX,
            MaxY = viewport.MaxZ
        };
    }

    private void UnloadBodyView(string bodyKey)
    {
        if (_bodyViewsByBodyKey.TryGetValue(bodyKey, out var bodyView))
        {
            DestroyImmediate(bodyView.gameObject);
            _bodyViewsByBodyKey.Remove(bodyKey);
        }

        if (_beltObjects.TryGetValue(bodyKey, out var beltObject))
        {
            Destroy(beltObject.Filter);
            _beltObjects.Remove(bodyKey);
        }

        _beltMeshes.Remove(bodyKey);
        _beltMatrices.Remove(bodyKey);
    }

    private void RefreshDaemonBodyPoses()
    {
        _daemonBodyPoses.Clear();
        _daemonBodyPoses.AddRange(_zoneRenderBodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>());
        _daemonBodyPosesByBodyKey.Clear();
        foreach (var pose in _daemonBodyPoses)
        {
            if (!string.IsNullOrWhiteSpace(pose.BodyKey))
                _daemonBodyPosesByBodyKey[pose.BodyKey] = pose;
        }
    }

    private void RefreshDaemonAsteroidBeltPoses()
    {
        _daemonAsteroidBeltPoses.Clear();
        _daemonAsteroidBeltPoses.AddRange(_zoneRenderAsteroidBeltPoses ?? Array.Empty<AetheriaRuntimeZoneRenderAsteroidBeltPose>());
    }

    private void RefreshDaemonCompassMarkers()
    {
        _daemonCompassMarkers.Clear();
        var observerEntityIndex = PerspectiveEntity?.DaemonEntityIndex ?? -1;
        var minimumInfoGathered = RenderSettings.TargetDetectionInfoThreshold;
        var requiredDistance = Math.Max(0, _minimapDistance);
        foreach (var contact in _daemonContactRows)
        {
            if (contact == null ||
                contact.ObserverEntityIndex != observerEntityIndex ||
                contact.InfoGathered <= minimumInfoGathered ||
                contact.Distance <= requiredDistance)
            {
                continue;
            }

            _daemonCompassMarkers.Add(new AetheriaRuntimeDaemonCompassMarker(
                contact.TargetEntityIndex,
                contact.TargetPositionX,
                contact.TargetPositionZ,
                contact.DeltaX,
                contact.DeltaZ,
                contact.Distance,
                contact.InfoGathered,
                contact.Hostile));
        }

        _daemonCompassMarkersByEntityIndex.Clear();
        foreach (var marker in _daemonCompassMarkers)
            _daemonCompassMarkersByEntityIndex[marker.TargetEntityIndex] = marker;
    }

    private void RefreshDaemonVisibleEntityInstances()
    {
        _daemonVisibleEntityIndices.Clear();
        var observerEntityIndex = PerspectiveEntity?.DaemonEntityIndex ?? -1;
        var minimumInfoGathered = RenderSettings.TargetDetectionInfoThreshold;
        if (observerEntityIndex >= 0)
            _daemonVisibleEntityIndices.Add(observerEntityIndex);
        foreach (var contact in _daemonContactRows)
        {
            if (contact != null &&
                contact.ObserverEntityIndex == observerEntityIndex &&
                contact.Visible &&
                contact.InfoGathered > minimumInfoGathered)
            {
                _daemonVisibleEntityIndices.Add(contact.TargetEntityIndex);
            }
        }

        _daemonVisibleEntityIndicesSet.Clear();
        foreach (var entityIndex in _daemonVisibleEntityIndices)
            _daemonVisibleEntityIndicesSet.Add(entityIndex);

        foreach (var entityInstance in _entityInstancesByDaemonIndex.Values)
        {
            var entityIndex = entityInstance.DaemonEntityIndex;
            var shouldBeVisible = entityIndex >= 0 && _daemonVisibleEntityIndicesSet.Contains(entityIndex);
            var isVisible = entityIndex >= 0 && _visibleDaemonEntityIndices.Contains(entityIndex);
            if (shouldBeVisible && !isVisible)
                entityInstance.FadeIn(EntityFadeTime);
            else if (!shouldBeVisible && isVisible)
                entityInstance.FadeOut(EntityFadeTime);
        }

        _visibleDaemonEntityIndices.Clear();
        foreach (var entityIndex in _daemonVisibleEntityIndicesSet)
            _visibleDaemonEntityIndices.Add(entityIndex);
    }

    private void SyncDaemonEntityInstances()
    {
        _daemonPresentationEntityIndices.Clear();
        var viewport = ResolveDaemonRenderViewport();
        if (!TryCollectDaemonPresentationEntityIndicesFromSoa(viewport))
        {
            var objects = ResolveObjectsViewport(viewport);
            foreach (var entity in objects?.Objects ?? Array.Empty<AetheriaRuntimeRtsViewportObject>())
            {
                if (entity != null && entity.EntityIndex >= 0)
                    _daemonPresentationEntityIndices.Add(entity.EntityIndex);
            }
        }

        _daemonPresentationEntityIndices.Sort();
        _daemonPresentationEntityIndicesSet.Clear();
        foreach (var entityIndex in _daemonPresentationEntityIndices)
            _daemonPresentationEntityIndicesSet.Add(entityIndex);

        foreach (var entityIndex in _daemonPresentationEntityIndices)
        {
            if (_entityInstancesByDaemonIndex.ContainsKey(entityIndex) ||
                _observedEntitySnapshotsByDaemonIndex == null ||
                !_observedEntitySnapshotsByDaemonIndex.TryGetValue(entityIndex, out var entity))
            {
                continue;
            }

            Debug.Log($"Loading entity {entity.Name} from daemon presentation query {entityIndex}");
            LoadEntity(entity);
        }

        foreach (var pair in _entityInstancesByDaemonIndex.ToArray())
        {
            if (!_daemonPresentationEntityIndicesSet.Contains(pair.Key))
                UnloadEntity(pair.Value.Entity);
        }
    }

    private bool TryCollectDaemonPresentationEntityIndicesFromSoa(AetheriaRuntimeXzRect viewport)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasRenderNativeView)
            return false;

        var view = observer.LastRenderNativeView;
        if (!view.IsCreated || !view.HasEntityIndex)
            return false;

        var minX = (float)Math.Min(viewport.MinX, viewport.MaxX);
        var maxX = (float)Math.Max(viewport.MinX, viewport.MaxX);
        var minZ = (float)Math.Min(viewport.MinZ, viewport.MaxZ);
        var maxZ = (float)Math.Max(viewport.MinZ, viewport.MaxZ);
        var count = Math.Min(view.Count, Math.Min(view.EntityIndex.Length, view.Position.Length));
        for (var i = 0; i < count; i++)
        {
            if (view.HasRenderVisibility && view.RenderVisibility[i] == 0)
                continue;

            var position = view.Position[i];
            if (position.x < minX || position.x > maxX || position.z < minZ || position.z > maxZ)
                continue;

            var entityIndex = view.EntityIndex[i];
            if (entityIndex >= 0)
                _daemonPresentationEntityIndices.Add(entityIndex);
        }

        return true;
    }

    private AetheriaDaemonObserver ResolveDaemonObserver()
    {
        if (_daemonObserver != null)
            return _daemonObserver;

        _daemonObserver = FindAnyObjectByType<AetheriaDaemonObserver>();
        return _daemonObserver;
    }

    private void RefreshDaemonContactRows()
    {
        _daemonTargetRowsByEntityIndex.Clear();
        _daemonContactRows.Clear();
        try
        {
            var contacts = ResolveZoneContacts();
            foreach (var target in contacts?.Targets ?? Array.Empty<AetheriaRuntimeZoneTargetRow>())
            {
                if (target != null && target.EntityIndex >= 0)
                    _daemonTargetRowsByEntityIndex[target.EntityIndex] = target;
            }

            foreach (var contact in contacts?.Contacts ?? Array.Empty<AetheriaRuntimeZoneContactRow>())
            {
                if (contact != null)
                    _daemonContactRows.Add(contact);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria zone contacts for renderer target distances: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        ClearClientCaches();
    }

    private float GetTerrainHeight(float2 position)
    {
        return (float)EvaluateGravityTerrainHeight(position.x, position.y);
    }

    private double DaemonSimulationTimeSeconds => _daemonSimulationTimeSeconds;

    private double EvaluateGravityTerrainHeight(double positionX, double positionZ)
    {
        var height = 0.0;
        foreach (var body in _zoneRenderBodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
        {
            if (body == null)
                continue;

            var dx = positionX - body.GravityInfluenceCenterX;
            var dz = positionZ - body.GravityInfluenceCenterZ;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            var radius = ResolveGravityRadius(body);
            if (radius > 0 && distance < radius && body.GravityWellDepth != 0)
            {
                height -= PowerPulse(
                    distance / radius,
                    Math.Max(0.0001, body.GravityDepthExponent)) * body.GravityWellDepth;
            }

            if (body.GravityWaveRadius > 0 && distance < body.GravityWaveRadius && body.GravityWaveDepth != 0)
            {
                height -= RadialWaves(
                    distance / body.GravityWaveRadius,
                    8.0,
                    1.25,
                    1.0,
                    DaemonSimulationTimeSeconds * body.GravityWaveSpeed) * body.GravityWaveDepth;
            }
        }

        return height;
    }

    private AetheriaRuntimeGravityTerrainBand QueryGravityTerrainBand(
        double minimapGravityRange,
        double maxDepth)
    {
        var terrainDepth = (_zoneRenderBodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            .Where(body => body != null)
            .Select(body => Math.Max(0, body.GravityWellDepth))
            .DefaultIfEmpty(0)
            .Max();
        var startDepth = Math.Min(terrainDepth, Math.Max(0, minimapGravityRange));
        return new AetheriaRuntimeGravityTerrainBand(
            startDepth,
            Math.Max(0, terrainDepth - startDepth) + maxDepth);
    }

    private static AetheriaRuntimeDaemonBodyPose ToDaemonBodyPose(AetheriaRuntimeZoneRenderBodyPose bodyPose)
    {
        if (bodyPose == null)
            return default;

        return new AetheriaRuntimeDaemonBodyPose(
            bodyPose.BodyKey,
            bodyPose.OrbitKey,
            bodyPose.ParentOrbitKey,
            bodyPose.Kind,
            bodyPose.CenterX,
            bodyPose.CenterZ,
            bodyPose.ParentCenterX,
            bodyPose.ParentCenterZ,
            bodyPose.GravityWaveSpeed);
    }

    private static bool IsAsteroidBelt(AetheriaRuntimeBodySnapshotCommit body)
    {
        return (body?.Kind ?? "").IndexOf("asteroid", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static double ResolveGravityRadius(AetheriaRuntimeBodySnapshotCommit body)
    {
        if (body != null && double.IsFinite(body.GravityInfluenceRadius) && body.GravityInfluenceRadius > 0)
            return body.GravityInfluenceRadius;

        return Math.Max(32, (body?.BodyRadiusMultiplier ?? 1) * 70);
    }

    private static double PowerPulse(double x, double exponent)
    {
        x *= 2.0;
        x = Math.Max(-1.0, Math.Min(1.0, x));
        return Math.Pow((x + 1.0) * (1.0 - x), exponent);
    }

    private static double RadialWaves(
        double x,
        double maskExponent,
        double sineExponent,
        double frequency,
        double phase)
    {
        return PowerPulse(x, maskExponent) *
               Math.Cos(Math.Pow(x * 2.0, sineExponent) * frequency + phase);
    }

    public void DestroyLoot(ItemPickup loot)
    {
        _loot.Remove(loot);
    }

    public void SetDroppedPickupItemFactory(Func<AetheriaRuntimeLoadoutItemCommit, ItemInstance> createDroppedPickupItem)
    {
        _createDroppedPickupItem = createDroppedPickupItem;
    }

    public void RestoreDroppedPickupsFromZoneRender(AetheriaRuntimeZoneRenderDocument render)
    {
        if (render == null)
            return;

        RestoreDroppedPickups(render.DroppedPickups);
    }

    private void RestoreDroppedPickups(IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> droppedPickups)
    {
        ClearRenderedLoot();
        foreach (var pickup in (droppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                     .Where(pickup => pickup != null)
                     .OrderBy(pickup => pickup.PickupIndex))
        {
            var item = _createDroppedPickupItem?.Invoke(pickup.Item);
            if (item == null)
                continue;

            DropItem(
                new Vector3((float)pickup.PositionX, (float)pickup.PositionY, (float)pickup.PositionZ),
                new Vector3((float)pickup.VelocityX, (float)pickup.VelocityY, (float)pickup.VelocityZ),
                item);
        }
    }

    private void ClearRenderedLoot()
    {
        if (ActiveLoot == null)
            return;

        foreach (var loot in ActiveLoot.ToArray())
        {
            if (loot == null)
                continue;

            DestroyLoot(loot);
            Destroy(loot.gameObject);
        }
    }

    public void DropItem(Vector3 position, Vector3 velocity, ItemInstance item)
    {
        var typedItem = FindTypedZoneItem(item);
        var gridObject = item switch
        {
            SimpleCommodity _ => Instantiate(SimpleCommodityPickup),
            CompoundCommodity _ => Instantiate(CompoundCommodityPickup),
            EquippableItem _ when IsWeaponPickup(typedItem) => Instantiate(WeaponPickup),
            EquippableItem _ => Instantiate(GearPickup),
            _ => throw new NotImplementedException()
        };
        var t = gridObject.transform;
        t.parent = ZoneRoot;
        t.position = position;
        gridObject.Velocity = velocity;
        var itemPickup = gridObject.gameObject.GetComponent<ItemPickup>();
        itemPickup.Item = item;
        itemPickup.ZoneRenderer = this;
        itemPickup.ScanLabel.text = typedItem?.Name ?? "Unknown Item";
        if (item is CraftedItemInstance craftedItemInstance)
        {
            var tradeValue = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
                typedItem,
                AetheriaRuntimeDaemonTradeItemQueries.CraftedItemCommit(
                    craftedItemInstance.ItemKey,
                    craftedItemInstance.Quality,
                    item is EquippableItem equippable ? equippable.Durability : 1f),
                ResolveCatalog()?.TradeValueSettings);
            var c = Color.white;
            if (!ColorUtility.TryParseHtmlString($"#{tradeValue.TierColorHex}", out c))
                c = Color.white;
            c.a = 0;
            itemPickup.ScanLabel.color = c;
        }
        else itemPickup.ScanLabel.color = new Color(.75f, .75f, .75f, 0);
        _loot.Add(itemPickup);
    }

    private static bool IsWeaponPickup(AetheriaRuntimeCatalogItem typedItem)
    {
        return typedItem != null && string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.Weapon, StringComparison.Ordinal);
    }

    private AetheriaRuntimeCatalogItem FindTypedZoneItem(ItemInstance item)
    {
        return ResolveCatalog()?.FindItem(item, x => x.ItemKey);
    }

    private AetheriaRuntimeCatalogSnapshot ResolveCatalog()
    {
        if (_catalog != null)
            return _catalog.Current;

        try
        {
            _catalog = AetheriaUnityRuntimeClientProvider
                .ReactiveCatalogSnapshot("unity-zone-renderer");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria runtime catalog for zone renderer: {ex.Message}");
        }

        return _catalog?.Current;
    }

    private AetheriaRuntimeZoneContactsDocument ResolveZoneContacts()
    {
        if (_zoneContacts != null)
            return _zoneContacts.Current;

        try
        {
            _zoneContacts = AetheriaUnityRuntimeClientProvider
                .ReactiveZoneContacts("unity-zone-renderer");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria zone contacts for renderer target distances: {ex.Message}");
        }

        return _zoneContacts?.Current;
    }

    private AetheriaRuntimeObjectsViewportDocument ResolveObjectsViewport(AetheriaRuntimeXzRect viewport)
    {
        var viewportBounds = ToViewportBounds(viewport);
        if (_objectsViewport != null && SameViewport(_objectsViewportBounds, viewportBounds))
            return _objectsViewport.Current;

        try
        {
            var nextObjectsViewport = AetheriaUnityRuntimeClientProvider
                .ReactiveObjectsViewport(
                    viewportBounds,
                    "unity-zone-renderer");
            _objectsViewport?.Dispose();
            _objectsViewportBounds = viewportBounds;
            _objectsViewport = nextObjectsViewport;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria objects viewport for zone renderer presentation: {ex.Message}");
        }

        return _objectsViewport?.Current;
    }

    private void ClearClientCaches()
    {
        _catalog?.Dispose();
        _zoneContacts?.Dispose();
        _objectsViewport?.Dispose();
        _catalog = null;
        _zoneContacts = null;
        _objectsViewport = null;
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
}


[Serializable]
public class InstancedMesh
{
    public Mesh Mesh;
    public Material Material;
}

public class AsteroidBeltUI
{
    public MeshFilter Filter;
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector2[] _uvs;
    private int[] _indices;
    private Mesh _mesh;
    private float _size;
    private float _scale;

    public AsteroidBeltUI(
        int asteroidCount,
        float radius,
        MeshFilter meshFilter,
        int spritesheetWidth,
        int spritesheetHeight,
        float scale)
    {
        Filter = meshFilter;
        asteroidCount = Math.Max(0, asteroidCount);
        _vertices = new Vector3[asteroidCount * 4];
        _normals = new Vector3[asteroidCount * 4];
        _uvs = new Vector2[asteroidCount * 4];
        _indices = new int[asteroidCount * 6];
        _scale = scale;

        var maxDist = Math.Max(0, radius);
        var spriteSize = float2(1f / spritesheetWidth, 1f / spritesheetHeight);
        // vertex order: bottom left, top left, top right, bottom right
        for (var i = 0; i < asteroidCount; i++)
        {
            var spriteX = Random.Range(0, spritesheetWidth);
            var spriteY = Random.Range(0, spritesheetHeight);

            _uvs[i * 4] = new Vector2(spriteX * spriteSize.x, spriteY * spriteSize.y);
            _uvs[i * 4 + 1] = new Vector2(spriteX * spriteSize.x, spriteY * spriteSize.y + spriteSize.y);
            _uvs[i * 4 + 2] = new Vector2(spriteX * spriteSize.x + spriteSize.x, spriteY * spriteSize.y + spriteSize.y);
            _uvs[i * 4 + 3] = new Vector2(spriteX * spriteSize.x + spriteSize.x, spriteY * spriteSize.y);

            _indices[i * 6] = i * 4;
            _indices[i * 6 + 1] = i * 4 + 1;
            _indices[i * 6 + 2] = i * 4 + 3;
            _indices[i * 6 + 3] = i * 4 + 3;
            _indices[i * 6 + 4] = i * 4 + 1;
            _indices[i * 6 + 5] = i * 4 + 2;
        }

        for (var i = 0; i < _normals.Length; i++)
        {
            _normals[i] = -Vector3.forward;
        }

        _mesh = new Mesh();
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.triangles = _indices;
        _mesh.normals = _normals;
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * maxDist);
        _size = maxDist;

        Filter.mesh = _mesh;
        //_collider.sharedMesh = _mesh;
    }

    public void Update(IReadOnlyList<AetheriaRuntimeZoneRenderAsteroidInstancePose> poses, float2 center, float height, float radius)
    {
        var count = Math.Min(poses.Count, _vertices.Length / 4);
        for (var i = 0; i < count; i++)
        {
            var pose = poses[i];
            var size = (float)pose.Size;
            var rotation = Quaternion.Euler(90, (float)pose.Rotation, 0);
            var position = new Vector3((float)pose.PositionX, height, (float)pose.PositionZ);
            _vertices[i * 4] = rotation * new Vector3(-size * _scale, -size * _scale, 0) + position;
            _vertices[i * 4 + 1] = rotation * new Vector3(-size * _scale, size * _scale, 0) + position;
            _vertices[i * 4 + 2] = rotation * new Vector3(size * _scale, size * _scale, 0) + position;
            _vertices[i * 4 + 3] = rotation * new Vector3(size * _scale, -size * _scale, 0) + position;
        }

        for (var i = count * 4; i < _vertices.Length; i++)
        {
            _vertices[i] = Vector3.zero;
        }

        var boundsRadius = Math.Max(_size, radius);
        _mesh.bounds = new Bounds(new Vector3(center.x, 0, center.y), Vector3.one * (boundsRadius * 2));
        _mesh.vertices = _vertices;
        //_collider.sharedMesh = _mesh;
    }
}
