/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
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
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;
using float2 = CultMath.float2;

public class ZoneRenderer : MonoBehaviour
{
    public Camera MainCamera;
    public Transform WormholePrefab;
    public float EntityFadeTime;
    public Transform FogCameraParent;
    public GameSettings Settings;
    public Transform ZoneRoot;
    public Transform SectorBrushes;
    // The ambient tint quad, sized from the zone's populated radius, never under SectorBrushes: FitAmbientLight.
    public Transform AmbientLight;
    public CinemachineVirtualCamera SceneCamera;
    public Camera[] FogCameras;
    public Camera[] MinimapCameras;
    public float FarPlaneDistanceMultiplier = 2;
    public InstancedMesh[] AsteroidMeshes;
    public int AsteroidSpritesheetWidth = 4;
    public int AsteroidSpritesheetHeight = 4;
    public LODHandler LODHandler;
    public Slime SlimeRenderer;
    public Camera SlimeGravityCamera;
    public ItemPickup SimpleCommodityPickup;
    public ItemPickup CompoundCommodityPickup;
    public ItemPickup GearPickup;
    public ItemPickup WeaponPickup;
    public MineInstance MinePrefab;
    public VapourCloudInstance CloudPrefab;
    public Material[] MapGravityMaterials;
    // The gravity field's material (Brushes/Gravity Field): GravityLowering makes the one quad that draws it.
    public Material GravityField;

    [Header("Tour")] public bool Tour;
    public float TourSwitchTime = 5f;
    public float TourFollowDistance = 30f;
    public float TourHeightOffset = 15;
    public float TourFollowOffsetDegrees;

    // public Mesh[] AsteroidMeshes;
    // public Material AsteroidMaterial;

    [Header("Prefabs")]
    // The map's belt sprites (Aetheria/MapBelt): MapPresenter draws every belt with it.
    public Material MapAsteroidMaterial;
    public PlanetObject Planet;
    public GasGiantObject GasGiant;
    public SunObject Sun;
    public Prototype CompassIconPrototype;

    [Header("Icons")]
    public Sprite OrbitalIcon;
    public Sprite WormholeIcon;

    [HideInInspector] public Dictionary<Entity, EntityInstance> EntityInstances = new Dictionary<Entity, EntityInstance>();
    [HideInInspector] public Dictionary<CultRecordKey, PlanetObject> Planets = new Dictionary<CultRecordKey, PlanetObject>();

    private Dictionary<CultRecordKey, InstancedMesh[]> _beltMeshes = new Dictionary<CultRecordKey, InstancedMesh[]>();
    private Dictionary<CultRecordKey, Matrix4x4[][]> _beltMatrices = new Dictionary<CultRecordKey, Matrix4x4[][]>();
    // Per rock: the mesh it takes and its slot in that mesh's matrix batch (ruling rock-meshes-by-size).
    private Dictionary<CultRecordKey, int[]> _beltMeshOf = new Dictionary<CultRecordKey, int[]>();
    private Dictionary<CultRecordKey, int[]> _beltSlotOf = new Dictionary<CultRecordKey, int[]>();
    // Cut 1 (docs/mining-cut.md): the renderer's own buffer, filled from Zone.EvaluateBelt each frame. No pose
    // is stored on AsteroidBelt any more.
    private Dictionary<CultRecordKey, float4[]> _beltTransforms = new Dictionary<CultRecordKey, float4[]>();
    private float _viewDistance;
    private GameObject _gravityField;
    private ComputeBuffer _gravityWells;
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

    public Dictionary<Wormhole, (GameObject gravity, CompassIcon icon)> WormholeInstances = new Dictionary<Wormhole, (GameObject, CompassIcon)>();
    private Dictionary<FloatingItem, ItemPickup> _floatingItems = new Dictionary<FloatingItem, ItemPickup>();
    private Dictionary<Mine, MineInstance> _mines = new Dictionary<Mine, MineInstance>();
    private Dictionary<VapourCloud, VapourCloudInstance> _clouds = new Dictionary<VapourCloud, VapourCloudInstance>();

    // The one owner of map-space projection: every map icon and belt sprite is drawn by it.
    public MapPresenter Map { get; private set; }
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
        }
    }

    public float MinimapDistance
    {
        set
        {
            _minimapDistance = value;
            foreach (var camera in MinimapCameras)
                camera.orthographicSize = value;
            Map.SetMinimapScale(value * Settings.MinimapIconSize);
            // MinimapGravityQuad.transform.localScale = value * 2 * Vector3.one;
        }
    }

    void Awake()
    {
        Map = new MapPresenter(MapGravityMaterials, MapAsteroidMaterial, AsteroidSpritesheetWidth,
            AsteroidSpritesheetHeight, Settings.MinimapAsteroidSize);
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

    // The ambient is a gaussian: its brightness at the populated edge is the material's _EdgeBrightness (a shape, authored), and
    // the quad reaches out to where its brightest channel falls to AmbientFloor. Fog Tint is B10G11R11_UFloatPack32 and cannot
    // follow a gaussian into its tail: measured, the drawn value stops following it near 4 floors and holds a plateau of
    // about 3.2e-5 before dropping to zero (AmbientEdgeVerify asserts the format and measures it). A quad sized to 1e-5 of the
    // centre therefore ended in a plateau and a step to zero; sized to the floor it ends at the last value the format holds,
    // so the one step to dark is that value. The one writer of the quad's size and _Reach, and the one place the verify
    // reads the sizing from.
    public const float AmbientFloor = 1f / (1 << 15);

    public static void FitAmbientLight(Transform light, Zone zone)
    {
        var renderer = light.GetComponent<MeshRenderer>();
        var material = renderer.sharedMaterial;
        var tint = material.GetColor("_Color").linear;
        var centre = material.GetFloat("_Depth") * Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
        var reach = Mathf.Sqrt(Mathf.Log(centre / AmbientFloor) / Mathf.Log(1f / material.GetFloat("_EdgeBrightness")));
        light.localScale = 2f * reach * zone.PopulatedRadius * Vector3.one;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetFloat("_Reach", reach);
        renderer.SetPropertyBlock(block);
    }

    public void LoadZone(Zone zone)
    {
        ClearZone();
        Zone = zone;
        SectorBrushes.localScale = zone.Pack.Radius * 2 * Vector3.one;
        FitAmbientLight(AmbientLight, zone);
        SlimeGravityCamera.orthographicSize = zone.Pack.Radius;
        SlimeRenderer.ZoneRadius = zone.Pack.Radius;
        if (_gravityField == null) _gravityField = GravityLowering.CreateField(GravityField);
        foreach (var p in zone.Planets)
            LoadPlanet(p.Key, p.Value);

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

        // Every mine, whoever laid it, is drawn through this one subscription. A mine leaves Zone.Mines only by
        // its blast, so a removal plays the blast; ClearZone below destroys the rest without one.
        foreach (var mine in zone.Mines) LoadMine(zone, mine);
        _zoneSubscriptions.Add(zone.Mines.ObserveAdd().Subscribe(e => LoadMine(zone, e.Value)));
        _zoneSubscriptions.Add(zone.Mines.ObserveRemove().Subscribe(e =>
        {
            if (!_mines.Remove(e.Value, out var instance)) return;
            instance.Blast();
        }));

        // Every floating item, whoever released it (a death drop, jettisoned cargo), is drawn through this one
        // subscription. An item leaves Zone.FloatingItems by expiry or by pickup, and either way its pickup is gone.
        foreach (var floating in zone.FloatingItems) LoadFloatingItem(zone, floating);
        _zoneSubscriptions.Add(zone.FloatingItems.ObserveAdd().Subscribe(e => LoadFloatingItem(zone, e.Value)));
        _zoneSubscriptions.Add(zone.FloatingItems.ObserveRemove().Subscribe(e =>
        {
            if (_floatingItems.Remove(e.Value, out var pickup) && pickup) Destroy(pickup.gameObject);
        }));

        // Every vapour cloud, whoever vented it, is drawn through this one subscription. A cloud leaves Zone.Clouds
        // when its opacity has reached nothing, so a removal destroys its fog bank with nothing left to show.
        foreach (var cloud in zone.Clouds) LoadCloud(zone, cloud);
        _zoneSubscriptions.Add(zone.Clouds.ObserveAdd().Subscribe(e => LoadCloud(zone, e.Value)));
        _zoneSubscriptions.Add(zone.Clouds.ObserveRemove().Subscribe(e =>
        {
            if (_clouds.Remove(e.Value, out var instance)) Destroy(instance.gameObject);
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

            // The run's exit gate is presentation only: whether it opens is RunGoal.ExitOpen, read when it is used.
            if (zone.GalaxyZone == zone.Galaxy.Exit)
                AddWormhole(new Wormhole
                {
                    Exit = true,
                    Position = RunGoal.ExitGatePosition(zone, Settings.WormholeDistanceRatio)
                });
        }
    }

    private void LoadMine(Zone zone, Mine mine)
    {
        var instance = Instantiate(MinePrefab);
        instance.Bind(zone, mine, ItemManager.GameplaySettings);
        _mines.Add(mine, instance);
    }

    private void LoadFloatingItem(Zone zone, FloatingItem floating)
    {
        var prefab = floating.Item switch
        {
            SimpleCommodity _ => SimpleCommodityPickup,
            CompoundCommodity _ => CompoundCommodityPickup,
            EquippableItem equippableItem when ItemManager.GetData(equippableItem) is WeaponItemData => WeaponPickup,
            EquippableItem _ => GearPickup,
            _ => throw new NotImplementedException()
        };
        var pickup = Instantiate(prefab, ZoneRoot);
        pickup.Bind(zone, floating, ItemManager.GameplaySettings);
        pickup.ScanLabel.text = ItemManager.Title(floating.Item);
        if (floating.Item is CraftedItemInstance craftedItemInstance)
        {
            var c = ItemManager.GetTier(craftedItemInstance).tier.Color.ToColor();
            c.a = 0;
            pickup.ScanLabel.color = c;
        }
        else pickup.ScanLabel.color = new Color(.75f, .75f, .75f, 0);
        _floatingItems.Add(floating, pickup);
    }

    private void LoadCloud(Zone zone, VapourCloud cloud)
    {
        var instance = Instantiate(CloudPrefab);
        instance.Bind(zone, cloud);
        _clouds.Add(cloud, instance);
    }

    public void AddWormhole(Wormhole wormhole)
    {
        var instance = Instantiate(WormholePrefab);
        instance.position = new Vector3(wormhole.Position.x, 0, wormhole.Position.y);
        var icon = CompassIconPrototype.Instantiate<CompassIcon>();
        icon.Icon.sprite = WormholeIcon;
        WormholeInstances.Add(wormhole, (instance.gameObject, icon));
    }

    void OnDestroy()
    {
        _gravityWells?.Release();
        _gravityWells = null;
    }

    public void ClearZone()
    {
        if (Zone == null) return;
        _gravityWells?.Release();
        _gravityWells = null;
        foreach (var wormhole in WormholeInstances.Values)
        {
            Destroy(wormhole.gravity);
            wormhole.icon.GetComponent<Prototype>().ReturnToPool();
        }
        WormholeInstances.Clear();

        foreach (var subscription in _zoneSubscriptions)
            subscription.Dispose();
        _zoneSubscriptions.Clear();
        
        foreach (var instance in _mines.Values)
            if (instance) Destroy(instance.gameObject);
        _mines.Clear();

        foreach (var instance in _clouds.Values)
            if (instance) Destroy(instance.gameObject);
        _clouds.Clear();

        foreach (var pickup in _floatingItems.Values)
            if (pickup) Destroy(pickup.gameObject);
        _floatingItems.Clear();

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
            Map.Clear();
            _beltMeshes.Clear();
            _beltMatrices.Clear();
            _beltMeshOf.Clear();
            _beltSlotOf.Clear();
            _beltTransforms.Clear();
            _tourPlanets.Clear();
        }
    }

    void LoadEntity(Entity entity)
    {
        var hullData = ItemManager.GetData(entity.Hull) as HullData;
        GameObject Body() => hullData.Visual.IsSet()
            ? ShipModShips.Instantiate(hullData, ZoneRoot)
            : Instantiate(EngineAssets.Load<GameObject>(hullData.Prefab), ZoneRoot);
        EntityInstance instance;
        if (entity is Ship)
        {
            instance = Body().GetComponent<ShipInstance>();
            if (instance == null)
            {
                ItemManager.Log($"Failed to instantiate {hullData.Name} ship with invalid prefab: no ShipInstance component!");
                return;
            }
        }
        else
        {
            instance = Body().GetComponent<EntityInstance>();
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

        Map.Unregister(entity);
        Destroy(EntityInstances[entity].gameObject);
        EntityInstances.Remove(entity);
    }

    void LoadPlanet(CultRecordKey key, BodyData planetData)
    {
        if (planetData is AsteroidBeltData beltData)
        {
            var meshes = AsteroidMeshes.ToList();
            while (meshes.Count > Settings.AsteroidMeshCount)
                meshes.RemoveAt(Random.Range(0, meshes.Count));
            // Ruling rock-meshes-by-size: meshes in ascending measured radius, rocks ranked by unworn size take them in that order.
            meshes.Sort((a, b) => a.Radius.CompareTo(b.Radius));
            _beltMeshes[key] = meshes.ToArray();
            _beltTransforms[key] = new float4[beltData.Asteroids.Length];
            var meshOf = DrawAhead.AssignRockMeshes(Zone.AsteroidBelts[key], Zone.Settings, meshes.Count);
            var slotOf = new int[meshOf.Length];
            var perMesh = new int[meshes.Count];
            for (int i = 0; i < meshOf.Length; i++) slotOf[i] = perMesh[meshOf[i]]++;
            _beltMeshOf[key] = meshOf;
            _beltSlotOf[key] = slotOf;
            _beltMatrices[key] = perMesh.Select(n => new Matrix4x4[n]).ToArray();

            Map.RegisterBelt(key, _beltTransforms[key]);
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
                    var sun = Zone.PlanetInstances[key] as Sun;
                    sunObject.Light.color = sunData.LightColor.ToColor();
                    sunObject.Light.range = sun.LightRadius;
                    sunObject.FogTint.transform.localScale = sun.LightRadius * Vector3.one;
                    sunObject.FogTint.material.SetColor("_Color", sunData.FogTintColor.ToColor());
                }
                else planet = Instantiate(GasGiant, ZoneRoot);

                var gas = (GasGiantObject) planet;
                gas.Body.material.SetTexture("_ColorRamp", gasGiantData.Colors.ToGradient(!(planetData is SunData)).ToTexture());
                // gasGiantData.AlbedoRotationSpeed.Subscribe(f => gas.SunMaterial.AlbedoRotationSpeed = f);
                // gasGiantData.FirstOffsetRotationSpeed.Subscribe(f => gas.SunMaterial.FirstOffsetRotationSpeed = f);
                // gasGiantData.SecondOffsetRotationSpeed.Subscribe(f => gas.SunMaterial.SecondOffsetRotationSpeed = f);
                // gasGiantData.FirstOffsetDomainRotationSpeed.Subscribe(f => gas.SunMaterial.FirstOffsetDomainRotationSpeed = f);
                // gasGiantData.SecondOffsetDomainRotationSpeed.Subscribe(f => gas.SunMaterial.SecondOffsetDomainRotationSpeed = f);
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

            var planetInstance = Zone.PlanetInstances[key];
            planet.Body.transform.localScale = planetInstance.BodyRadius * Vector3.one;
            Map.RegisterPlanet(key, planet.Icon, planet.transform);


            Planets[key] = planet;
            if (!_rootFound)
            {
                _rootFound = true;
                _root = planet;
            }
        }

        LODHandler.FindPlanets();
    }

    void LateUpdate()
    {
        Map.Draw(Zone, ActionGameManager.Clock.Lead);
    }

    void Update()
    {
        var lead = ActionGameManager.Clock.Lead;
        GravityLowering.Lower(Zone, lead, ref _gravityWells);
        foreach (var loot in _floatingItems.Values)
        {
            loot.ViewOrigin = DrawAhead.Position(PerspectiveEntity, lead);
            loot.ViewDirection = PerspectiveEntity.Aim;
        }
        
        // if (SlimeRenderer.SpawnPositions.Length != _suns.Length)
        //     SlimeRenderer.SpawnPositions = new Vector2[_suns.Length];
        // for (var i = 0; i < _suns.Length; i++)
        // {
        //     SlimeRenderer.SpawnPositions[i] = _suns[i].Body.transform.position.Flatland();
        // }
        

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(MainCamera);
        bool isVisible(Bounds bounds) => GeometryUtility.TestPlanesAABB(planes, bounds);
        
        foreach (var (key, belt) in Zone.AsteroidBelts)
        {
            // Cut 1 (docs/mining-cut.md): belts have no stored pose any more. The renderer fills its own buffer
            // from Zone.EvaluateBelt, at the drawn time (DrawAhead.Time), every frame.
            var orbitPosition = DrawAhead.Orbit(Zone, Zone.Orbits[belt.Data.Orbit.Key].Data.Parent.Key, lead);
            var height = DrawAhead.Height(Zone, orbitPosition, lead);
            var transforms = _beltTransforms[key];
            Zone.EvaluateBelt(key, transforms, DrawAhead.Time(Zone, lead));
            if(isVisible(new Bounds(
                new Vector3(orbitPosition.x,height,orbitPosition.y),
                new Vector3(belt.Radius * 2,100,belt.Radius * 2))))
            {
                var meshes = _beltMeshes[key];
                var matrices = _beltMatrices[key];
                var meshOf = _beltMeshOf[key];
                var slotOf = _beltSlotOf[key];
                for (int tx = 0; tx < transforms.Length; tx++)
                {
                    var transform = transforms[tx];
                    var i = meshOf[tx];
                    matrices[i][slotOf[tx]] = Matrix4x4.TRS(DrawAhead.ChunkAt(Zone, ItemManager.GameplaySettings, transform, lead).ToUnity(),
                        Quaternion.Euler(
                            cos(transform.z + (float)i / meshes.Length) * 100,
                            sin(transform.z + (float)i / meshes.Length) * 100,
                            (float)tx / transforms.Length * 360),
                        Vector3.one * DrawAhead.RockScale(transform.w, meshes[i].Radius));
                }

                for (int i = 0; i < meshes.Length; i++)
                    Graphics.DrawMeshInstanced(meshes[i].Mesh, 0, meshes[i].Material, matrices[i]);
            }
        }

        foreach (var planet in Planets)
        {
            var planetInstance = Zone.PlanetInstances[planet.Key];
            var p = DrawAhead.Orbit(Zone, planetInstance.BodyData.Orbit.Key, lead);
            var height = DrawAhead.Height(Zone, p, lead);
            planet.Value.transform.position = new Vector3(p.x, 0, p.y);
            planet.Value.Body.transform.localPosition = new Vector3(0, height + planetInstance.BodyRadius * 2, 0);
            if (planet.Value is GasGiantObject gasGiantObject)
            {
                if (!(planet.Value is SunObject))
                {
                    var toParent = normalize(DrawAhead.Orbit(Zone, Zone.Orbits[planetInstance.BodyData.Orbit.Key].Data.Parent.Key, lead) - p);
                    gasGiantObject.SunMaterial.LightingDirection = new Vector3(toParent.x, 0, toParent.y);
                }
            }
            else planet.Value.Body.transform.rotation *= Quaternion.AngleAxis(Settings.PlanetRotationSpeed, Vector3.up);
        }

        foreach (var entityInstance in EntityInstances.Values)
        {
            if(entityInstance.CompassIcon)
            {
                var difference = DrawAhead.Position(entityInstance.Entity, lead).xz - DrawAhead.Position(PerspectiveEntity, lead).xz;
                var distance = length(difference);
                
                entityInstance.CompassIcon.gameObject.SetActive(
                    PerspectiveEntity.ContactOf(entityInstance.Entity).Tracked &&
                    distance > _minimapDistance);
                entityInstance.CompassIcon.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg - 90);
            }
        }

        foreach (var wormhole in WormholeInstances.Values)
        {
            var difference = wormhole.gravity.transform.position.Flatland() - DrawAhead.Position(PerspectiveEntity, lead).xz.ToUnity();
            var distance = difference.magnitude;
            wormhole.icon.gameObject.SetActive(distance > _minimapDistance);
            wormhole.icon.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg - 90);
        }

        // var gravPos = MinimapGravityQuad.transform.position;
        // MinimapGravityQuad.transform.position = gravPos;
        // MinimapTintQuad.transform.position = gravPos - Vector3.up*10;
    }
}


[Serializable]
public class InstancedMesh
{
    public Mesh Mesh;
    public Material Material;
    // The mesh's farthest vertex from its origin, measured by Aetheria.EditorTools.RockMeshRadius and verified against the mesh;
    // the renderer scales the mesh by gameplay radius over this.
    public float Radius;
}
