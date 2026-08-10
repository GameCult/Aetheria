/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
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
    [HideInInspector] public Dictionary<Guid, PlanetObject> Planets = new Dictionary<Guid, PlanetObject>();

    private Dictionary<Guid, AsteroidBeltUI> _beltObjects = new Dictionary<Guid, AsteroidBeltUI>();
    private Dictionary<Guid, InstancedMesh[]> _beltMeshes = new Dictionary<Guid, InstancedMesh[]>();
    private Dictionary<Guid, Matrix4x4[][]> _beltMatrices = new Dictionary<Guid, Matrix4x4[][]>();
    private float _viewDistance;
    //private float _maxDepth;
    private float _minimapDistance;

    private int _tourIndex = -1;
    private float _tourTimer;
    private List<(Transform, Transform)> _tourPlanets = new List<(Transform, Transform)>();
    private CinemachineTransposer _transposer;
    private PlanetObject _root;
    private bool _rootFound;
    private Entity _perspectiveEntity;
    private IDisposable[] _perspectiveSubscriptions = new IDisposable[2];
    private List<IDisposable> _zoneSubscriptions = new List<IDisposable>();
    private PlanetObject[] _suns;
    private bool _showAsteroidUI;

    public Dictionary<Wormhole, (GameObject gravity, CompassIcon icon)> WormholeInstances = new Dictionary<Wormhole, (GameObject, CompassIcon)>();
    private List<ItemPickup> _loot = new List<ItemPickup>();

    public Zone Zone { get; private set; }
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

    public void LoadZone(Zone zone)
    {
        ClearZone();
        Zone = zone;
        SectorBrushes.localScale = zone.Pack.Radius * 2 * Vector3.one;
        SlimeGravityCamera.orthographicSize = zone.Pack.Radius;
        SlimeRenderer.ZoneRadius = zone.Pack.Radius;
        foreach (var p in zone.Planets.Values)
            LoadPlanet(p);

        _suns = Planets.Values.Where(p => p is SunObject).ToArray();

        // foreach (var x in Planets)
        // {
        //     if (!(zone.Planets[x.Key] is GasGiantData))
        //     {
        //         
        //     }
        // }

        foreach (var entity in zone.Entities)
        {
            Debug.Log($"Loading entity {entity.Name} from existing zone entity collection");
            LoadEntity(entity);
        }

        _zoneSubscriptions.Add(zone.Entities.ObserveAdd().Subscribe(e =>
        {
            //Debug.Log($"Loading entity {e.Value.Name} from zone add event");
            LoadEntity(e.Value);
        }));
        _zoneSubscriptions.Add(zone.Entities.ObserveRemove().Subscribe(e =>
        {
            //Debug.Log($"Unloading entity {e.Value.Name} from zone remove event");
            UnloadEntity(e.Value);
        }));

        if (zone.GalaxyZone != null)
        {
            foreach (var adjacentZone in zone.GalaxyZone.AdjacentZones)
            {
                var dir = normalize(adjacentZone.Position - zone.GalaxyZone.Position);
                AddWormhole(new Wormhole
                {
                    Target = adjacentZone,
                    Position = dir * zone.Pack.Radius * Settings.WormholeDistanceRatio
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

        foreach (var subscription in _zoneSubscriptions)
            subscription.Dispose();
        _zoneSubscriptions.Clear();
        
        foreach(var gridObject in _loot) 
            if(gridObject) Destroy(gridObject.gameObject);
        _loot.Clear();

        foreach (var entity in Zone.Entities)
        {
            Debug.Log($"Unloading entity {entity.Name} from entities remaining during clear!");
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
        var hullData = ItemManager.GetData(entity.Hull) as HullData;
        EntityInstance instance;
        if (entity is Ship)
        {
            instance = Instantiate(UnityHelpers.LoadAsset<GameObject>(hullData.Prefab), ZoneRoot).GetComponent<ShipInstance>();
            if (instance == null)
            {
                ItemManager.Log($"Failed to instantiate {hullData.Name} ship with invalid prefab: no ShipInstance component!");
                return;
            }
        }
        else
        {
            instance = Instantiate(UnityHelpers.LoadAsset<GameObject>(hullData.Prefab), ZoneRoot).GetComponent<EntityInstance>();
            if (instance == null)
            {
                ItemManager.Log($"Failed to instantiate {hullData.Name} entity with invalid prefab: no EntityInstance component!");
                return;
            }
            if (entity.HullData.HullType == HullType.Station)
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

        Destroy(EntityInstances[entity].gameObject);
        EntityInstances.Remove(entity);
    }

    void LoadPlanet(BodyData planetData)
    {
        if (planetData is AsteroidBeltData beltData)
        {
            var meshes = AsteroidMeshes.ToList();
            while (meshes.Count > Settings.AsteroidMeshCount)
                meshes.RemoveAt(Random.Range(0, meshes.Count));
            _beltMeshes[planetData.ID] = meshes.ToArray();
            _beltMatrices[planetData.ID] = new Matrix4x4[meshes.Count][];
            var count = beltData.Asteroids.Length / meshes.Count;
            var remainder = beltData.Asteroids.Length - count * meshes.Count;
            for (int i = 0; i < meshes.Count; i++)
            {
                _beltMatrices[planetData.ID][i] = new Matrix4x4[i < meshes.Count - 1 ? count : count + remainder];
            }

            var beltObject = Instantiate(AsteroidBeltUI, ZoneRoot);
            var collider = beltObject.GetComponent<MeshCollider>();
            var belt = new AsteroidBeltUI(Zone,
                Zone.AsteroidBelts[beltData.ID],
                beltObject,
                collider,
                AsteroidSpritesheetWidth,
                AsteroidSpritesheetHeight,
                Settings.MinimapAsteroidSize);
            _beltObjects[beltData.ID] = belt;
        }
        else
        {
            PlanetObject planet;
            if (planetData is GasGiantData gasGiantData)
            {
                if (planetData is SunData sunData)
                {
                    planet = Instantiate(Sun, ZoneRoot);
                    var sunObject = (SunObject) planet;
                    var sun = Zone.PlanetInstances[planetData.ID] as Sun;
                    sunObject.Light.color = sunData.LightColor.ToColor();
                    sunObject.Light.range = sun.LightRadius;
                    sunObject.FogTint.transform.localScale = sun.LightRadius * Vector3.one;
                    sunObject.FogTint.material.SetColor("_Color", sunData.FogTintColor.ToColor());
                }
                else planet = Instantiate(GasGiant, ZoneRoot);

                var gas = (GasGiantObject) planet;
                var gasGiant = Zone.PlanetInstances[planetData.ID] as GasGiant;
                gas.Body.material.SetTexture("_ColorRamp", gasGiantData.Colors.ToGradient(!(planetData is SunData)).ToTexture());
                // gasGiantData.AlbedoRotationSpeed.Subscribe(f => gas.SunMaterial.AlbedoRotationSpeed = f);
                // gasGiantData.FirstOffsetRotationSpeed.Subscribe(f => gas.SunMaterial.FirstOffsetRotationSpeed = f);
                // gasGiantData.SecondOffsetRotationSpeed.Subscribe(f => gas.SunMaterial.SecondOffsetRotationSpeed = f);
                // gasGiantData.FirstOffsetDomainRotationSpeed.Subscribe(f => gas.SunMaterial.FirstOffsetDomainRotationSpeed = f);
                // gasGiantData.SecondOffsetDomainRotationSpeed.Subscribe(f => gas.SunMaterial.SecondOffsetDomainRotationSpeed = f);
                gas.GravityWaves.transform.localScale = gasGiant.GravityWavesRadius * Vector3.one;
                gas.GravityWaves.material.SetFloat("_Depth", gasGiant.GravityWavesDepth);
                gas.GravityWaves.material.SetFloat("_Frequency", Settings.PlanetSettings.WaveFrequency.Evaluate(planetData.Mass));
                //gas.WaveScroll.Speed = Properties.GravitySettings.WaveSpeed.Evaluate(f);
            }
            else
            {
                planet = Instantiate(Planet, ZoneRoot);
                var possibleSettings = Settings.BodySettingsCollections
                    .Where(p => p.MinimumMass < planetData.Mass)
                    .MaxBy(p => p.MinimumMass).BodySettings;
                planet.Generator.body = possibleSettings[Random.Range(0, possibleSettings.Length)];
                //Debug.Log($"Generating planet with {planetData.Mass} mass! Choosing {planet.Generator.body.name} settings!");
                //planet.Icon.material.mainTexture = planetData.Mass > Context.GlobalData.PlanetMass ? PlanetIcon : PlanetoidIcon;
            }

            var planetInstance = Zone.PlanetInstances[planetData.ID];
            planet.Body.transform.localScale = planetInstance.BodyRadius * Vector3.one;
            planet.GravityWell.transform.localScale = planetInstance.GravityWellRadius * Vector3.one;
            // var depth = planetInstance.GravityWellDepth;
            // if (depth > _maxDepth) _maxDepth = depth;
            planet.GravityWell.material.SetFloat("_Depth", planetInstance.GravityWellDepth);
            planet.Icon.transform.position = new Vector3(0, -planetInstance.GravityWellDepth, 0);
            planet.Icon.transform.localScale = Settings.IconSize.Evaluate(planetInstance.BodyData.Mass) * Vector3.one;


            Planets[planetData.ID] = planet;
            if (!_rootFound)
            {
                _rootFound = true;
                _root = planet;
            }
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
            loot.ViewOrigin = PerspectiveEntity.Position;
            loot.ViewDirection = PerspectiveEntity.LookDirection;
        }
        
        // if (SlimeRenderer.SpawnPositions.Length != _suns.Length)
        //     SlimeRenderer.SpawnPositions = new Vector2[_suns.Length];
        // for (var i = 0; i < _suns.Length; i++)
        // {
        //     SlimeRenderer.SpawnPositions[i] = _suns[i].Body.transform.position.Flatland();
        // }
        
        Shader.SetGlobalFloat("_AsteroidVerticalOffset", ActionGameManager.Instance.Settings.PlanetSettings.AsteroidVerticalOffset);

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(MainCamera);
        bool isVisible(Bounds bounds) => GeometryUtility.TestPlanesAABB(planes, bounds);
        
        foreach (var (key, belt) in Zone.AsteroidBelts)
        {
            var height = Zone.GetHeight(belt.OrbitPosition);
            if(isVisible(new Bounds(
                new Vector3(belt.OrbitPosition.x,height,belt.OrbitPosition.y),
                new Vector3(belt.Radius * 2,100,belt.Radius * 2))))
            {
                var meshes = _beltMeshes[key];
                var matrices = _beltMatrices[key];
                var count = belt.Transforms.Length / meshes.Length;
                for (int i = 0; i < meshes.Length; i++)
                {
                    for (int t = 0; t < matrices[i].Length; t++)
                    {
                        var tx = t + i * count;
                        var transform = belt.Transforms[tx];
                        matrices[i][t] = Matrix4x4.TRS(new Vector3(transform.x,0,transform.y),
                            Quaternion.Euler(
                                cos(transform.z + (float)i / meshes.Length) * 100,
                                sin(transform.z + (float)i / meshes.Length) * 100,
                                (float)tx / belt.Transforms.Length * 360),
                            Vector3.one * transform.w);
                    }

                    Graphics.DrawMeshInstanced(meshes[i].Mesh, 0, meshes[i].Material, matrices[i]);
                }
            }

            if(_showAsteroidUI)
                _beltObjects[key].Update(belt.Transforms, height);
        }

        foreach (var planet in Planets)
        {
            var planetInstance = Zone.PlanetInstances[planet.Key];
            var p = Zone.GetOrbitPosition(planetInstance.BodyData.Orbit);
            var height = Zone.GetHeight(p);
            if (-height > maxDepth) maxDepth = -height;
            planet.Value.transform.position = new Vector3(p.x, 0, p.y);
            planet.Value.Body.transform.localPosition = new Vector3(0, height + planetInstance.BodyRadius * 2, 0);
            if (planet.Value is GasGiantObject gasGiantObject)
            {
                gasGiantObject.GravityWaves.material.SetFloat("_Phase",
                    Zone.Time * ((GasGiant) Zone.PlanetInstances[planet.Key]).GravityWavesSpeed);
                if (!(planet.Value is SunObject))
                {
                    var toParent = normalize(Zone.GetOrbitPosition(Zone.Orbits[planetInstance.BodyData.Orbit].Data.Parent) - p);
                    gasGiantObject.SunMaterial.LightingDirection = new Vector3(toParent.x, 0, toParent.y);
                }
            }
            else planet.Value.Body.transform.rotation *= Quaternion.AngleAxis(Settings.PlanetRotationSpeed, Vector3.up);
        }

        foreach (var entityInstance in EntityInstances.Values)
        {
            if(entityInstance.CompassIcon)
            {
                var difference = entityInstance.Entity.Position.xz - PerspectiveEntity.Position.xz;
                var distance = length(difference);
                
                entityInstance.CompassIcon.gameObject.SetActive(
                    PerspectiveEntity.EntityInfoGathered.ContainsKey(entityInstance.Entity) && 
                    PerspectiveEntity.EntityInfoGathered[entityInstance.Entity] > Settings.GameplaySettings.TargetDetectionInfoThreshold &&
                    distance > _minimapDistance);
                entityInstance.CompassIcon.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg - 90);
            }
        }

        foreach (var wormhole in WormholeInstances.Values)
        {
            var difference = wormhole.gravity.transform.position.Flatland() - (Vector2)PerspectiveEntity.Position.xz;
            var distance = difference.magnitude;
            wormhole.icon.gameObject.SetActive(distance > _minimapDistance);
            wormhole.icon.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg - 90);
        }

        //var fogPos = FogCameraParent.position;
        SectorBoundaryBrush.material.SetFloat("_Power", Settings.PlanetSettings.ZoneDepthExponent);
        SectorBoundaryBrush.material.SetFloat("_Depth", Settings.PlanetSettings.ZoneDepth + Settings.PlanetSettings.ZoneBoundaryFog);
        var startDepth = Zone.PowerPulse(Settings.MinimapZoneGravityRange, Settings.PlanetSettings.ZoneDepthExponent) *
                          Settings.PlanetSettings.ZoneDepth;
        var depthRange = Settings.PlanetSettings.ZoneDepth - startDepth + maxDepth;
        foreach (var mat in MapGravityMaterials)
        {
            mat.SetFloat("_StartDepth", startDepth);
            mat.SetFloat("_DepthRange", depthRange);
        }
        //Shader.SetGlobalFloat("_GridOffset", Settings.PlanetSettings.ZoneBoundaryFog);
        // var gravPos = MinimapGravityQuad.transform.position;
        // gravPos.y = -Settings.PlanetSettings.ZoneDepth - _maxDepth;
        // MinimapGravityQuad.transform.position = gravPos;
        // MinimapTintQuad.transform.position = gravPos - Vector3.up*10;
    }

    public void DestroyLoot(ItemPickup loot)
    {
        _loot.Remove(loot);
    }

    public void DropItem(Vector3 position, Vector3 velocity, ItemInstance item)
    {
        var gridObject = item switch
        {
            SimpleCommodity _ => Instantiate(SimpleCommodityPickup),
            CompoundCommodity _ => Instantiate(CompoundCommodityPickup),
            EquippableItem equippableItem when ItemManager.GetData(equippableItem) is WeaponItemData => Instantiate(WeaponPickup),
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
        itemPickup.ScanLabel.text = item.Data.Value.Name;
        if (item is CraftedItemInstance craftedItemInstance)
        {
            var c = ItemManager.GetTier(craftedItemInstance).tier.Color.ToColor();
            c.a = 0;
            itemPickup.ScanLabel.color = c;
        }
        else itemPickup.ScanLabel.color = new Color(.75f, .75f, .75f, 0);
        gridObject.gameObject.AddComponent<TimedDestroy>().Duration = Settings.PickupLifetime;
        _loot.Add(itemPickup);
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
    private MeshCollider _collider;
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector2[] _uvs;
    private int[] _indices;
    private Guid _orbitParent;
    private Mesh _mesh;
    private float _size;
    private Zone _zone;
    private AsteroidBelt _belt;
    private float _scale;

    public AsteroidBeltUI(Zone zone,
        AsteroidBelt belt,
        MeshFilter meshFilter,
        MeshCollider collider,
        int spritesheetWidth,
        int spritesheetHeight,
        float scale)
    {
        _belt = belt;
        _zone = zone;
        Filter = meshFilter;
        _collider = collider;
        var orbit = zone.Orbits[belt.Data.Orbit];
        _orbitParent = orbit.Data.Parent;
        _vertices = new Vector3[_belt.Data.Asteroids.Length * 4];
        _normals = new Vector3[_belt.Data.Asteroids.Length * 4];
        _uvs = new Vector2[_belt.Data.Asteroids.Length * 4];
        _indices = new int[_belt.Data.Asteroids.Length * 6];
        _scale = scale;

        var maxDist = 0f;
        var spriteSize = float2(1f / spritesheetWidth, 1f / spritesheetHeight);
        // vertex order: bottom left, top left, top right, bottom right
        for (var i = 0; i < belt.Data.Asteroids.Length; i++)
        {
            if (belt.Data.Asteroids[i].Distance > maxDist)
                maxDist = belt.Data.Asteroids[i].Distance;
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

    public void Update(float4[] transforms, float height)
    {
        var parentPosition = _belt.OrbitPosition;
        for (var i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            var rotation = Quaternion.Euler(90, transform.z, 0);
            var position = new Vector3(transform.x, height, transform.y);
            _vertices[i * 4] = rotation * new Vector3(-transform.w * _scale, -transform.w * _scale, 0) + position;
            _vertices[i * 4 + 1] = rotation * new Vector3(-transform.w * _scale, transform.w * _scale, 0) + position;
            _vertices[i * 4 + 2] = rotation * new Vector3(transform.w * _scale, transform.w * _scale, 0) + position;
            _vertices[i * 4 + 3] = rotation * new Vector3(transform.w * _scale, -transform.w * _scale, 0) + position;
        }

        _mesh.bounds = new Bounds(new Vector3(parentPosition.x, 0, parentPosition.y), Vector3.one * (_size * 2));
        _mesh.vertices = _vertices;
        //_collider.sharedMesh = _mesh;
    }
}
