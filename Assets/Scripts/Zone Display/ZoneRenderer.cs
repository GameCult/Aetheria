/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
using UniRx;
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
    [HideInInspector] public Dictionary<string, PlanetObject> Planets = new Dictionary<string, PlanetObject>(StringComparer.Ordinal);

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
    private IDisposable[] _perspectiveSubscriptions = new IDisposable[2];
    private PlanetObject[] _suns;
    private bool _showAsteroidUI;
    private AetheriaRuntimeZoneSnapshotCommit _daemonZoneSnapshot;
    private readonly List<AetheriaRuntimeDaemonBodyPose> _daemonBodyPoses = new List<AetheriaRuntimeDaemonBodyPose>();
    private readonly Dictionary<string, AetheriaRuntimeDaemonBodyPose> _daemonBodyPosesByBodyKey =
        new Dictionary<string, AetheriaRuntimeDaemonBodyPose>(StringComparer.Ordinal);
    private readonly List<AetheriaRuntimeDaemonAsteroidBeltPose> _daemonAsteroidBeltPoses =
        new List<AetheriaRuntimeDaemonAsteroidBeltPose>();
    private readonly Dictionary<string, AetheriaRuntimeDaemonAsteroidBeltPose> _daemonAsteroidBeltPosesByBodyKey =
        new Dictionary<string, AetheriaRuntimeDaemonAsteroidBeltPose>(StringComparer.Ordinal);
    private readonly List<AetheriaRuntimeDaemonAsteroidInstancePose> _visibleAsteroidInstancePoses =
        new List<AetheriaRuntimeDaemonAsteroidInstancePose>();

    public Dictionary<Wormhole, (GameObject gravity, CompassIcon icon)> WormholeInstances = new Dictionary<Wormhole, (GameObject, CompassIcon)>();
    private List<ItemPickup> _loot = new List<ItemPickup>();

    public Zone Zone { get; private set; }
    public IReadOnlyList<ItemPickup> ActiveLoot => _loot;
    public ItemManager ItemManager { get; set; }

    public Entity PerspectiveEntity
    {
        get => _perspectiveEntity;
        set
        {
            //if (_perspectiveEntity == value) return;
            _perspectiveEntity = value;
            _perspectiveSubscriptions[0]?.Dispose();
            _perspectiveSubscriptions[1]?.Dispose();
            if (value == null)
            {
                foreach (var e in EntityInstances.Values)
                    e.FadeOut(EntityFadeTime);
            }
            else
            {
                foreach (var entity in EntityInstances.Values)
                    entity.FadeOut(EntityFadeTime);
                foreach (var entity in value.VisibleEntities)
                    EntityInstances[entity].FadeIn(EntityFadeTime);
                EntityInstances[value].FadeIn(EntityFadeTime);
                _perspectiveSubscriptions[0] = value.VisibleEntities.ObserveAdd()
                    .Subscribe(add => EntityInstances[add.Value].FadeIn(EntityFadeTime));
                _perspectiveSubscriptions[1] = value.VisibleEntities.ObserveRemove()
                    .Where(removeEvent => EntityInstances.ContainsKey(removeEvent.Value))
                    .Subscribe(removeEvent => EntityInstances[removeEvent.Value].FadeOut(EntityFadeTime));
            }
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
            SetIconSize(value * Settings.MinimapIconSize);
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
        foreach(var entityInstance in EntityInstances.Values) entityInstance.MapIcon.transform.localScale = Vector3.one * size;
        foreach(var planet in Planets.Values) planet.Icon.transform.localScale = Vector3.one * size;
    }

    void Start()
    {
        var bigBounds = new Bounds(Vector3.zero, Vector3.one * 1024);
        foreach (var mesh in AsteroidMeshes) mesh.Mesh.bounds = bigBounds;
        ViewDistance = Settings.DefaultViewDistance;
        MinimapDistance = Settings.MinimapZoomLevels[Settings.DefaultMinimapZoom];

        _tourTimer = TourSwitchTime;
        _transposer = SceneCamera.GetCinemachineComponent<CinemachineTransposer>();
    }

    public void LoadZone(Zone zone, AetheriaRuntimeZoneSnapshotCommit daemonZone = null)
    {
        ClearZone();
        Zone = zone;
        _daemonZoneSnapshot = daemonZone;
        SectorBrushes.localScale = zone.Radius * 2 * Vector3.one;
        SlimeGravityCamera.orthographicSize = zone.Radius;
        SlimeRenderer.ZoneRadius = zone.Radius;
        RefreshDaemonBodyPoses();
        foreach (var pose in _daemonBodyPoses)
        {
            if (zone.PlanetInstances.TryGetValue(pose.BodyKey, out var planet))
            {
                LoadPlanet(planet);
                continue;
            }

            if (zone.AsteroidBelts.TryGetValue(pose.BodyKey, out var belt))
                LoadAsteroidBelt(belt);
        }

        _suns = Planets.Values.Where(p => p is SunObject).ToArray();

        var entitiesByDaemonIndex = new Dictionary<int, Entity>();
        foreach (var entity in zone.Entities)
        {
            if (entity != null && entity.DaemonEntityIndex >= 0)
                entitiesByDaemonIndex[entity.DaemonEntityIndex] = entity;
        }
        foreach (var entitySnapshot in daemonZone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
        {
            if (entitySnapshot != null &&
                entitiesByDaemonIndex.TryGetValue(entitySnapshot.EntityIndex, out var entity))
            {
                Debug.Log($"Loading entity {entity.Name} from daemon entity snapshot {entitySnapshot.EntityIndex}");
                LoadEntity(entity);
            }
        }

        if (zone.GalaxyZone != null)
        {
            foreach (var adjacentZone in zone.GalaxyZone.AdjacentZones)
            {
                var dir = normalize(adjacentZone.Position - zone.GalaxyZone.Position);
                AddWormhole(new Wormhole
                {
                    Target = adjacentZone,
                    Position = AetheriaMath.ToCult(dir * zone.Radius * Settings.WormholeDistanceRatio)
                });
            }
        }
    }

    public void AddWormhole(Wormhole wormhole)
    {
        var instance = Instantiate(WormholePrefab);
        instance.position = new Vector3(wormhole.Position.x, 0, wormhole.Position.y);
        var icon = CompassIconPrototype.Instantiate<CompassIcon>();
        icon.Icon.sprite = WormholeIcon;
        WormholeInstances.Add(wormhole, (instance.gameObject, icon));
    }

    public void ClearZone()
    {
        if (Zone == null) return;
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

        if (Planets.Count > 0)
        {
            foreach (var planet in Planets.Values)
            {
                DestroyImmediate(planet.gameObject);
            }

            Planets.Clear();
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
            ItemManager.Log($"Failed to instantiate {entity.Name} with missing typed hull prefab.");
            return;
        }

        EntityInstance instance;
        if (entity is Ship)
        {
            instance = Instantiate(UnityHelpers.LoadAsset<GameObject>(typedHull.HullPrefab), ZoneRoot).GetComponent<ShipInstance>();
            if (instance == null)
            {
                ItemManager.Log($"Failed to instantiate {typedHull.Name} ship with invalid prefab: no ShipInstance component!");
                return;
            }
        }
        else
        {
            instance = Instantiate(UnityHelpers.LoadAsset<GameObject>(typedHull.HullPrefab), ZoneRoot).GetComponent<EntityInstance>();
            if (instance == null)
            {
                ItemManager.Log($"Failed to instantiate {typedHull.Name} entity with invalid prefab: no EntityInstance component!");
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
    }

    void LoadAsteroidBelt(AsteroidBelt runtimeBelt)
    {
        var meshes = AsteroidMeshes.ToList();
        while (meshes.Count > Settings.AsteroidMeshCount)
            meshes.RemoveAt(Random.Range(0, meshes.Count));
        _beltMeshes[runtimeBelt.BodyKey] = meshes.ToArray();
        _beltMatrices[runtimeBelt.BodyKey] = new Matrix4x4[meshes.Count][];
        var count = runtimeBelt.AsteroidCount / meshes.Count;
        var remainder = runtimeBelt.AsteroidCount - count * meshes.Count;
        for (int i = 0; i < meshes.Count; i++)
        {
            _beltMatrices[runtimeBelt.BodyKey][i] = new Matrix4x4[i < meshes.Count - 1 ? count : count + remainder];
        }

        var beltObject = Instantiate(AsteroidBeltUI, ZoneRoot);
        var belt = new AsteroidBeltUI(Zone,
            runtimeBelt,
            beltObject,
            AsteroidSpritesheetWidth,
            AsteroidSpritesheetHeight,
            Settings.MinimapAsteroidSize);
        _beltObjects[runtimeBelt.BodyKey] = belt;

        LODHandler.FindPlanets();
    }

    void LoadPlanet(Planet planetInstance)
    {
        PlanetObject planet;
        if (planetInstance is GasGiant gasGiant)
        {
            if (planetInstance is Sun sun)
            {
                planet = Instantiate(Sun, ZoneRoot);
                var sunObject = (SunObject) planet;
                sunObject.Light.color = sun.LightColor.ToColor();
                sunObject.Light.range = sun.LightRadius;
                sunObject.FogTint.transform.localScale = sun.LightRadius * Vector3.one;
                sunObject.FogTint.material.SetColor("_Color", sun.FogTintColor.ToColor());
            }
            else planet = Instantiate(GasGiant, ZoneRoot);

            var gas = (GasGiantObject) planet;
            gas.Body.material.SetTexture("_ColorRamp", gasGiant.Colors.ToGradient(!(planetInstance is Sun)).ToTexture());
            gas.GravityWaves.transform.localScale = gasGiant.GravityWavesRadius * Vector3.one;
            gas.GravityWaves.material.SetFloat("_Depth", gasGiant.GravityWavesDepth);
            gas.GravityWaves.material.SetFloat("_Frequency", Settings.PlanetSettings.WaveFrequency.Evaluate(planetInstance.Mass));
        }
        else
        {
            planet = Instantiate(Planet, ZoneRoot);
            var possibleSettings = Settings.BodySettingsCollections
                .Where(p => p.MinimumMass < planetInstance.Mass)
                .MaxBy(p => p.MinimumMass).BodySettings;
            planet.Generator.body = possibleSettings[Random.Range(0, possibleSettings.Length)];
            //Debug.Log($"Generating planet with {planetInstance.Mass} mass! Choosing {planet.Generator.body.name} settings!");
            //planet.Icon.material.mainTexture = planetInstance.Mass > Context.GlobalData.PlanetMass ? PlanetIcon : PlanetoidIcon;
        }

        planet.Body.transform.localScale = planetInstance.BodyRadius * Vector3.one;
        planet.GravityWell.transform.localScale = planetInstance.GravityWellRadius * Vector3.one;
        // var depth = planetInstance.GravityWellDepth;
        // if (depth > _maxDepth) _maxDepth = depth;
        planet.GravityWell.material.SetFloat("_Depth", planetInstance.GravityWellDepth);
        planet.Icon.transform.position = new Vector3(0, -planetInstance.GravityWellDepth, 0);
        planet.Icon.transform.localScale = Settings.IconSize.Evaluate(planetInstance.Mass) * Vector3.one;


        Planets[planetInstance.BodyKey] = planet;
        if (!_rootFound)
        {
            _rootFound = true;
            _root = planet;
        }

        LODHandler.FindPlanets();
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

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(MainCamera);
        bool isVisible(Bounds bounds) => GeometryUtility.TestPlanesAABB(planes, bounds);
        
        foreach (var (key, belt) in Zone.AsteroidBelts)
        {
            if (!_daemonAsteroidBeltPosesByBodyKey.TryGetValue(key, out var beltPose))
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
                    Zone?.Time ?? 0,
                    _visibleAsteroidInstancePoses);
            }

            if(beltIsVisible)
            {
                var meshes = _beltMeshes[key];
                var matrices = _beltMatrices[key];
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

        foreach (var planet in Planets)
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
                    Zone.Time * (float)pose.GravityWaveSpeed);
                if (!string.Equals(pose.Kind, "sun", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = new float2((float)pose.ParentCenterX, (float)pose.ParentCenterZ);
                    var toParent = normalize(parent - p);
                    gasGiantObject.SunMaterial.LightingDirection = new Vector3(toParent.x, 0, toParent.y);
                }
            }
            else planet.Value.Body.transform.rotation *= Quaternion.AngleAxis(Settings.PlanetRotationSpeed, Vector3.up);
        }

        foreach (var entityInstance in EntityInstances.Values)
        {
            if(entityInstance.CompassIcon)
            {
                var difference = entityInstance.Entity.CultPositionXZ - PerspectiveEntity.CultPositionXZ;
                var distance = CultMath.math.length(difference);
                
                entityInstance.CompassIcon.gameObject.SetActive(
                    PerspectiveEntity.EntityInfoGathered.ContainsKey(entityInstance.Entity) && 
                    PerspectiveEntity.EntityInfoGathered[entityInstance.Entity] > Settings.GameplaySettings.TargetDetectionInfoThreshold &&
                    distance > _minimapDistance);
                entityInstance.CompassIcon.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg - 90);
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
        _daemonAsteroidBeltPosesByBodyKey.Clear();
        foreach (var pose in _daemonAsteroidBeltPoses)
        {
            if (!string.IsNullOrWhiteSpace(pose.BodyKey))
                _daemonAsteroidBeltPosesByBodyKey[pose.BodyKey] = pose;
        }
    }

    private float GetTerrainHeight(float2 position)
    {
        return (float)AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(
            _daemonZoneSnapshot,
            position.x,
            position.y,
            Zone?.Time ?? 0);
    }

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
        gridObject.Zone = Zone;
        t.position = position;
        gridObject.Velocity = velocity;
        var itemPickup = gridObject.gameObject.GetComponent<ItemPickup>();
        itemPickup.Item = item;
        itemPickup.ZoneRenderer = this;
        itemPickup.ScanLabel.text = typedItem?.Name ?? "Unknown Item";
        if (item is CraftedItemInstance craftedItemInstance)
        {
            var c = ItemManager.GetTier(craftedItemInstance).tier.Color.ToColor();
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
    private Zone _zone;
    private AsteroidBelt _belt;
    private float _scale;

    public AsteroidBeltUI(Zone zone,
        AsteroidBelt belt,
        MeshFilter meshFilter,
        int spritesheetWidth,
        int spritesheetHeight,
        float scale)
    {
        _belt = belt;
        _zone = zone;
        Filter = meshFilter;
        _vertices = new Vector3[_belt.AsteroidCount * 4];
        _normals = new Vector3[_belt.AsteroidCount * 4];
        _uvs = new Vector2[_belt.AsteroidCount * 4];
        _indices = new int[_belt.AsteroidCount * 6];
        _scale = scale;

        var maxDist = 0f;
        var spriteSize = float2(1f / spritesheetWidth, 1f / spritesheetHeight);
        // vertex order: bottom left, top left, top right, bottom right
        for (var i = 0; i < belt.AsteroidCount; i++)
        {
            var asteroid = belt.GetAsteroid(i);
            if (asteroid.Distance > maxDist)
                maxDist = asteroid.Distance;
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
