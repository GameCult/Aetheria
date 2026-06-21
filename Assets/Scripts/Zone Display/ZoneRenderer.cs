/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
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
    public GameSettings Settings;
    public Transform ZoneRoot;
    public Transform SectorBrushes;
    public MeshRenderer SectorBoundaryBrush;
    public CinemachineVirtualCamera SceneCamera;
    public Camera[] FogCameras;
    public Camera[] MinimapCameras;
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
    private PlanetObject[] _suns;
    private bool _showAsteroidUI;
    private AetheriaRuntimeRunCheckpointCommit _daemonRunSnapshot;
    private AetheriaRuntimeZoneSnapshotCommit _daemonZoneSnapshot;
    private readonly List<AetheriaRuntimeDaemonBodyView> _daemonBodyViews = new List<AetheriaRuntimeDaemonBodyView>();
    private readonly List<AetheriaRuntimeDaemonBodyPose> _daemonBodyPoses = new List<AetheriaRuntimeDaemonBodyPose>();
    private readonly Dictionary<string, AetheriaRuntimeDaemonBodyPose> _daemonBodyPosesByBodyKey =
        new Dictionary<string, AetheriaRuntimeDaemonBodyPose>(StringComparer.Ordinal);
    private readonly List<AetheriaRuntimeDaemonAsteroidBeltPose> _daemonAsteroidBeltPoses =
        new List<AetheriaRuntimeDaemonAsteroidBeltPose>();
    private readonly List<AetheriaRuntimeDaemonAsteroidInstancePose> _visibleAsteroidInstancePoses =
        new List<AetheriaRuntimeDaemonAsteroidInstancePose>();
    private readonly List<AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkers =
        new List<AetheriaRuntimeDaemonCompassMarker>();
    private readonly Dictionary<int, AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkersByEntityIndex =
        new Dictionary<int, AetheriaRuntimeDaemonCompassMarker>();
    private readonly List<int> _daemonVisibleEntityIndices = new List<int>();
    private readonly HashSet<int> _daemonVisibleEntityIndicesSet = new HashSet<int>();
    private readonly HashSet<int> _visibleDaemonEntityIndices = new HashSet<int>();
    private readonly List<AetheriaRuntimeDaemonWormholeExit> _daemonWormholeExits =
        new List<AetheriaRuntimeDaemonWormholeExit>();

    public Dictionary<int, (GameObject gravity, CompassIcon icon)> WormholeInstances = new Dictionary<int, (GameObject, CompassIcon)>();
    private List<ItemPickup> _loot = new List<ItemPickup>();

    public IReadOnlyDictionary<int, EntityInstance> DaemonEntityInstances => _entityInstancesByDaemonIndex;
    public IReadOnlyList<ItemPickup> ActiveLoot => _loot;
    public AetheriaRuntimeDaemonRenderSettings RenderSettings { get; set; }

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
            foreach (var camera in FogCameras)
                camera.orthographicSize = value;
            SceneCamera.m_Lens.FarClipPlane = value * FarPlaneDistanceMultiplier;
            FogMaterial.SetFloat("_DepthCeiling", value);
            FogMaterial.SetFloat("_DepthBlend", FogFarFadeFraction * value);
        }
    }

    public float MinimapDistance
    {
        set
        {
            _minimapDistance = value;
            foreach (var camera in MinimapCameras)
                camera.orthographicSize = value;
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
        if (!AetheriaRuntimeDaemonRenderQueries.TryQueryEntityTarget(
                _daemonZoneSnapshot,
                daemonEntityIndex,
                out var target))
        {
            return false;
        }

        distance = (float)target.Distance;
        return true;
    }

    void Start()
    {
        var bigBounds = new Bounds(Vector3.zero, Vector3.one * 1024);
        foreach (var mesh in AsteroidMeshes) mesh.Mesh.bounds = bigBounds;
        ViewDistance = (float)RenderSettings.DefaultViewDistance;
        MinimapDistance = (float)RenderSettings.ResolveDefaultMinimapDistance();

        _tourTimer = TourSwitchTime;
        _transposer = SceneCamera.GetCinemachineComponent<CinemachineTransposer>();
    }

    public void LoadDaemonZoneView(
        IReadOnlyDictionary<int, Entity> observedEntityFacadesByDaemonIndex,
        AetheriaRuntimeZoneSnapshotCommit daemonZone = null,
        AetheriaRuntimeRunCheckpointCommit daemonRun = null)
    {
        ClearZone();
        ApplyDaemonFrame(daemonZone, daemonRun);
        RefreshDaemonBodyViews();
        RefreshDaemonBodyPoses();
        RefreshDaemonAsteroidBeltPoses();
        var beltPosesByBodyKey = _daemonAsteroidBeltPoses
            .Where(pose => !string.IsNullOrWhiteSpace(pose.BodyKey))
            .ToDictionary(pose => pose.BodyKey, StringComparer.Ordinal);
        foreach (var bodyView in _daemonBodyViews)
        {
            var body = bodyView.Body;

            if (bodyView.IsAsteroidBelt)
            {
                if (beltPosesByBodyKey.TryGetValue(body.BodyKey ?? "", out var beltPose))
                    LoadAsteroidBelt(beltPose);
            }
            else
            {
                LoadPlanet(body);
            }
        }

        _suns = _bodyViewsByBodyKey.Values.Where(p => p is SunObject).ToArray();

        foreach (var entitySnapshot in daemonZone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
        {
            if (entitySnapshot != null &&
                observedEntityFacadesByDaemonIndex != null &&
                observedEntityFacadesByDaemonIndex.TryGetValue(entitySnapshot.EntityIndex, out var entity))
            {
                Debug.Log($"Loading entity {entity.Name} from daemon entity snapshot {entitySnapshot.EntityIndex}");
                LoadEntity(entity);
            }
        }

        foreach (var exit in _daemonWormholeExits)
            AddWormhole(exit);
    }

    public void ApplyDaemonFrame(
        AetheriaRuntimeZoneSnapshotCommit daemonZone,
        AetheriaRuntimeRunCheckpointCommit daemonRun)
    {
        _daemonRunSnapshot = daemonRun;
        _daemonZoneSnapshot = daemonZone;
        var zoneRenderRadius = (float)AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
            _daemonZoneSnapshot,
            2000);
        SectorBrushes.localScale = zoneRenderRadius * 2 * Vector3.one;
        SlimeGravityCamera.orthographicSize = zoneRenderRadius;
        SlimeRenderer.ZoneRadius = zoneRenderRadius;
        AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
            _daemonRunSnapshot,
            _daemonZoneSnapshot,
            zoneRenderRadius,
            RenderSettings.WormholeDistanceRatio,
            _daemonWormholeExits);
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

    void LoadAsteroidBelt(AetheriaRuntimeDaemonAsteroidBeltPose beltPose)
    {
        var bodyKey = beltPose.BodyKey;
        if (bodyKey.Length == 0)
            return;

        var meshes = AsteroidMeshes.ToList();
        while (meshes.Count > Settings.AsteroidMeshCount)
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
                var lightRadius = Settings.PlanetSettings.LightRadius.Evaluate(mass) * (float)sunVisual.LightRadiusMultiplier;
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
            gas.GravityWaves.material.SetFloat("_Frequency", Settings.PlanetSettings.WaveFrequency.Evaluate(mass));
        }
        else
        {
            planet = Instantiate(Planet, ZoneRoot);
            var possibleSettings = Settings.BodySettingsCollections
                .Where(p => p.MinimumMass < mass)
                .MaxBy(p => p.MinimumMass).BodySettings;
            planet.Generator.body = possibleSettings[Random.Range(0, possibleSettings.Length)];
            //Debug.Log($"Generating planet with {mass} mass! Choosing {planet.Generator.body.name} settings!");
            //planet.Icon.material.mainTexture = planetInstance.Mass > Context.GlobalData.PlanetMass ? PlanetIcon : PlanetoidIcon;
        }

        var bodyRadius = Settings.PlanetSettings.BodyRadius.Evaluate(mass) * (float)body.BodyRadiusMultiplier;
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
        
        Shader.SetGlobalFloat("_AsteroidVerticalOffset", ActionGameManager.Instance.Settings.PlanetSettings.AsteroidVerticalOffset);
        RefreshDaemonBodyPoses();
        RefreshDaemonAsteroidBeltPoses();
        RefreshDaemonVisibleEntityInstances();
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
                AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(
                    _daemonZoneSnapshot,
                    key,
                    DaemonSimulationTimeSeconds,
                    _visibleAsteroidInstancePoses);
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
            else planet.Value.Body.transform.rotation *= Quaternion.AngleAxis(Settings.PlanetRotationSpeed, Vector3.up);
        }

        foreach (var entityInstance in _entityInstancesByDaemonIndex.Values)
        {
            if(entityInstance.CompassIcon)
            {
                var active = entityInstance.DaemonEntityIndex >= 0 &&
                    _daemonCompassMarkersByEntityIndex.TryGetValue(entityInstance.DaemonEntityIndex, out var marker);
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
        SectorBoundaryBrush.material.SetFloat("_Power", Settings.PlanetSettings.ZoneDepthExponent);
        SectorBoundaryBrush.material.SetFloat("_Depth", Settings.PlanetSettings.ZoneDepth + Settings.PlanetSettings.ZoneBoundaryFog);
        var gravityBand = AetheriaRuntimeDaemonRenderQueries.QueryGravityTerrainBand(
            _daemonZoneSnapshot,
            Settings.MinimapZoneGravityRange,
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

    private void RefreshDaemonBodyViews()
    {
        AetheriaRuntimeDaemonRenderQueries.QueryBodyViews(_daemonZoneSnapshot, _daemonBodyViews);
    }

    private void RefreshDaemonBodyPoses()
    {
        AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(_daemonZoneSnapshot, _daemonBodyPoses);
        _daemonBodyPosesByBodyKey.Clear();
        foreach (var pose in _daemonBodyPoses)
        {
            if (!string.IsNullOrWhiteSpace(pose.BodyKey))
                _daemonBodyPosesByBodyKey[pose.BodyKey] = pose;
        }
    }

    private void RefreshDaemonAsteroidBeltPoses()
    {
        AetheriaRuntimeDaemonRenderQueries.QueryAsteroidBeltPoses(_daemonZoneSnapshot, _daemonAsteroidBeltPoses);
    }

    private void RefreshDaemonCompassMarkers()
    {
        AetheriaRuntimeDaemonRenderQueries.QueryCompassMarkers(
            _daemonZoneSnapshot,
            PerspectiveEntity?.DaemonEntityIndex ?? -1,
            RenderSettings.TargetDetectionInfoThreshold,
            _minimapDistance,
            _daemonCompassMarkers);
        _daemonCompassMarkersByEntityIndex.Clear();
        foreach (var marker in _daemonCompassMarkers)
            _daemonCompassMarkersByEntityIndex[marker.TargetEntityIndex] = marker;
    }

    private void RefreshDaemonVisibleEntityInstances()
    {
        AetheriaRuntimeDaemonRenderQueries.QueryVisibleEntityIndices(
            _daemonZoneSnapshot,
            PerspectiveEntity?.DaemonEntityIndex ?? -1,
            RenderSettings.TargetDetectionInfoThreshold,
            _daemonVisibleEntityIndices);
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

    private float GetTerrainHeight(float2 position)
    {
        return (float)AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(
            _daemonZoneSnapshot,
            position.x,
            position.y,
            DaemonSimulationTimeSeconds);
    }

    private double DaemonSimulationTimeSeconds => _daemonZoneSnapshot?.SimulationTimeSeconds ?? 0;

    public void DestroyLoot(ItemPickup loot)
    {
        _loot.Remove(loot);
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
            var tradeProjection = AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(
                typedItem,
                AetheriaRuntimeDaemonTradeItemQueries.CraftedItemCommit(
                    craftedItemInstance.ItemKey,
                    craftedItemInstance.Quality,
                    item is EquippableItem equippable ? equippable.Durability : 1f),
                ActionGameManager.Instance?.ObservedTradeValueSettings());
            var c = Color.white;
            if (!ColorUtility.TryParseHtmlString($"#{tradeProjection.TierColorHex}", out c))
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

    private static AetheriaRuntimeCatalogItem FindTypedZoneItem(ItemInstance item)
    {
        return ActionGameManager.RuntimeCatalog?.FindItem(item?.ItemKey ?? "");
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

    public void Update(IReadOnlyList<AetheriaRuntimeDaemonAsteroidInstancePose> poses, float2 center, float height, float radius)
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
