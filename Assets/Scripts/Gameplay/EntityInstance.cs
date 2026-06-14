using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using UniRx;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using Random = UnityEngine.Random;

public class EntityInstance : MonoBehaviour
{
    public MeshRenderer MapIcon;
    public Transform InfluencePrefab;
    public Transform PingPrefab;
    public Material InvisibleMaterial;
    public static Transform EffectManagerParent;
    public ShieldManager Shield;
    public HullCollider[] HullColliders;

    public Transform[] EquipmentHardpoints;
    public RadiatorHardpoint[] RadiatorHardpoints;
    public ThrusterHardpoint[] ThrusterHardpoints;
    public WeaponHardpoint[] WeaponHardpoints;
    public ArticulationPoint[] ArticulationPoints;

    public GameObject DestroyEffect;
    
    public event Action OnFadedOut;
    public event Action OnFadedIn;
    
    private List<Material> _fadeMaterials = new List<Material>();
    private Dictionary<MeshRenderer, Material[]> _meshes = new Dictionary<MeshRenderer, Material[]>();
    private List<(MeshRenderer mesh, int submesh, Material material)> _nonFadedSubmeshes = new List<(MeshRenderer mesh, int submesh, Material material)>();
    private List<IDisposable> _subscriptions = new List<IDisposable>();
    private float _fade = 0;
    private bool _fading;
    private bool _fadingIn;
    private bool _fadedElementsVisible = false;
    private bool _unfadedElementsVisible = false;
    private float _fadeTime;
    private bool _destroyed;
    private (Transform transform, MeshRenderer meshRenderer) _currentPing;
    private float _pingBrightness;
    private Sensor _sensor;
    private Transform _influenceInstance;

    //private List<(GameObject source, EquippedItem item)> _audioSources = new List<(GameObject source, EquippedItem item)>();
    // private (Reactor reactor, GameObject sfxSource) _reactor;
    // private Dictionary<Radiator, GameObject> _radiatorSfx = new Dictionary<Radiator, GameObject>();
    
    private static Dictionary<string, InstantWeaponEffectManager> _instantWeaponManagers = new Dictionary<string, InstantWeaponEffectManager>();
    private static Dictionary<string, ConstantWeaponEffectManager> _constantWeaponManagers = new Dictionary<string, ConstantWeaponEffectManager>();

    public static void ClearWeaponManagers()
    {
        _instantWeaponManagers.Clear();
        _constantWeaponManagers.Clear();
    }

    private static AetheriaRuntimeCatalogItem FindTypedHull(ItemInstance hull)
    {
        var itemId = hull?.ItemId ?? Guid.Empty;
        return itemId == Guid.Empty
            ? null
            : ActionGameManager.RuntimeCatalog?.FindItemByLegacyId(itemId.ToString("D"));
    }

    private static Shape ToShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(width, height);
        foreach (var cell in cells)
        {
            if (cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height)
                shape[int2(cell.X, cell.Y)] = true;
        }

        return shape;
    }

    public CompassIcon CompassIcon { get; set; }
    public Dictionary<HardpointData, Transform[]> Barrels { get; private set; }
    public Dictionary<HardpointData, int> BarrelIndices { get; private set; }
    public Dictionary<Radiator, MeshRenderer> RadiatorMeshes { get; private set; }
    public Transform LookAtPoint { get; private set; }
    public Entity Entity { get; private set; }
    public ZoneRenderer ZoneRenderer { get; private set; }
    public Transform LocalSpace { get; private set; }
    public bool Visible
    {
        get => _fade > 0.01f;
    }

    private void Awake()
    {
        LocalSpace = new GameObject().transform;
        var meshes = gameObject.GetComponentsInChildren<MeshRenderer>();
        var materials = new List<(Material material, List<(MeshRenderer renderer, int index)> submeshes)>();
        foreach (var mesh in meshes)
        {
            _meshes.Add(mesh, mesh.sharedMaterials);
            for (var i = 0; i < mesh.sharedMaterials.Length; i++)
            {
                var material = mesh.sharedMaterials[i];
                if (material.shader.FindPropertyIndex("_Fade") >= 0)
                {
                    var match = materials.FirstOrDefault(lm => lm.material == material);
                    if(match.material==null || material.shader.FindPropertyIndex("_EmissionFresnel") >= 0)
                    {
                        match = (material, new List<(MeshRenderer renderer, int index)>());
                        materials.Add(match);
                    }
                    match.submeshes.Add((mesh, i));
                }
                else
                {
                    _nonFadedSubmeshes.Add((mesh, i, material));
                    _meshes[mesh][i] = InvisibleMaterial;
                }
            }
        }

        foreach (var (material, submeshes) in materials)
        {
            var materialInstance = Instantiate(material);
            _fadeMaterials.Add(materialInstance);
            foreach (var (mesh, index) in submeshes)
            {
                _meshes[mesh][index] = materialInstance;
            }
            materialInstance.SetFloat("_Fade", 0);
        }

        foreach (var mesh in _meshes.Keys) mesh.enabled = false;

        foreach (var mesh in _meshes) mesh.Key.sharedMaterials = mesh.Value;
    }

    protected virtual void ShowUnfadedElements()
    {
        _unfadedElementsVisible = true;
        foreach (var (mesh, submesh, material) in _nonFadedSubmeshes) _meshes[mesh][submesh] = material;
        foreach (var mesh in _meshes) mesh.Key.sharedMaterials = mesh.Value;
    }

    protected virtual void HideUnfadedElements()
    {
        _unfadedElementsVisible = false;
        foreach (var (mesh, submesh, material) in _nonFadedSubmeshes) _meshes[mesh][submesh] = InvisibleMaterial;
        foreach (var mesh in _meshes) mesh.Key.sharedMaterials = mesh.Value;
    }

    public void FadeIn(float time)
    {
        if (_fade > .99f)
        {
            _fading = false;
            return;
        }
        _fadeTime = time;
        _fading = true;
        _fadingIn = true;
    }

    public void FadeOut(float time)
    {
        if (_fade < .01f)
        {
            _fading = false;
            return;
        }
        _fadeTime = time;
        _fading = true;
        _fadingIn = false;
    }

    public virtual void SetEntity(ZoneRenderer zoneRenderer, Entity entity)
    {
        gameObject.name = entity.Name;
        LocalSpace.gameObject.name = $"{entity.Name} Sim Space";
        LocalSpace.SetParent(transform.parent);
        Entity = entity;
        ZoneRenderer = zoneRenderer;
        var typedHull = FindTypedHull(entity.Hull);
        if (typedHull == null || typedHull.ShapeWidth <= 0 || typedHull.ShapeHeight <= 0 || typedHull.ShapeCells.Count == 0)
        {
            Debug.LogError($"Cannot bind entity instance for {entity.Name}: missing typed hull shape.");
            return;
        }

        var hullShape = ToShape(typedHull.ShapeWidth, typedHull.ShapeHeight, typedHull.ShapeCells);
        var typedHardpoints = typedHull.Hardpoints.ToArray();

        if(Shield)
            Shield.Entity = entity;
        foreach (var hullCollider in HullColliders) hullCollider.Entity = entity;

        foreach (var item in entity.Equipment)
        {
            foreach (var behavior in item.Behaviors)
            {
                if (behavior is Sensor sensor)
                {
                    _sensor = sensor;
                    sensor.OnPingStart += () =>
                    {
                        var pingInstance = Instantiate(PingPrefab);
                        var pingMesh = pingInstance.GetComponent<MeshRenderer>();
                        pingInstance.position = entity.Position;
                        _pingBrightness = pingMesh.material.GetFloat("_Depth");
                        _currentPing = (pingInstance, pingMesh);
                    };
                    sensor.OnPingEnd += OnSensorPingEnd;
                }
                
                if (behavior is InstantWeapon instantWeapon)
                {
                    var effectPrefab = instantWeapon.EffectPrefab;
                    if (!_instantWeaponManagers.ContainsKey(effectPrefab))
                    {
                        var managerPrefab = UnityHelpers.LoadAsset<InstantWeaponEffectManager>(effectPrefab);
                        if(managerPrefab)
                        {
                            _instantWeaponManagers.Add(effectPrefab, Instantiate(managerPrefab, EffectManagerParent));
                        }
                        else Debug.LogError($"No InstantWeaponEffectManager prefab found at path {effectPrefab}");
                    }

                    instantWeapon.OnFire += () => 
                        _instantWeaponManagers[effectPrefab].Fire(instantWeapon, item, this, entity.Target.Value != null && ZoneRenderer.EntityInstances.ContainsKey(entity.Target.Value) ? ZoneRenderer.EntityInstances[entity.Target.Value] : null);

                    if (behavior is ChargedWeapon chargedWeapon)
                    {
                        var chargeManager = _instantWeaponManagers[effectPrefab].GetComponent<ChargeEffectManager>();
                        if (chargeManager)
                        {
                            chargedWeapon.OnStartCharging += () => chargeManager.StartCharging(chargedWeapon, item, this);
                            chargedWeapon.OnStopCharging += () => chargeManager.StopCharging(chargedWeapon);
                            chargedWeapon.OnCharged += () => chargeManager.Charged(chargedWeapon);
                            chargedWeapon.OnFailed += () => chargeManager.Failed(chargedWeapon);
                        }
                    }
                }

                if (behavior is ConstantWeapon constantWeapon)
                {
                    var effectPrefab = constantWeapon.EffectPrefab;
                    if (!_constantWeaponManagers.ContainsKey(effectPrefab))
                    {
                        var managerPrefab = UnityHelpers.LoadAsset<ConstantWeaponEffectManager>(effectPrefab);
                        if(managerPrefab)
                        {
                            _constantWeaponManagers.Add(effectPrefab, Instantiate(managerPrefab, EffectManagerParent));
                        }
                        else Debug.LogError($"No ConstantWeaponEffectManager prefab found at path {effectPrefab}");
                    }

                    constantWeapon.OnStartFiring += () =>
                        _constantWeaponManagers[effectPrefab].StartFiring(constantWeapon, item, this, entity.Target.Value != null ? ZoneRenderer.EntityInstances[entity.Target.Value] : null);
                    constantWeapon.OnStopFiring += () => 
                        _constantWeaponManagers[effectPrefab].StopFiring(item);
                }
            }
            
        }
        RadiatorMeshes = new Dictionary<Radiator, MeshRenderer>();
        Barrels = new Dictionary<HardpointData, Transform[]>();
        BarrelIndices = new Dictionary<HardpointData, int>();
        foreach (var radiator in entity.GetBehaviors<Radiator>())
        {
            var hp = Entity.Hardpoints[radiator.Item.Position.x, radiator.Item.Position.y];
            if (hp != null && hp.Type == HardpointType.Radiator)
            {
                var mesh = RadiatorHardpoints.FirstOrDefault(x => x.name == hp.Transform);
                if (mesh)
                {
                    RadiatorMeshes.Add(radiator, mesh.Mesh);
                }
            }
        }
        foreach (var hp in Entity.Hardpoints.Cast<HardpointData>().Where(hp => hp != null).Distinct())
        {
            if(hp.Type == HardpointType.Ballistic || hp.Type == HardpointType.Energy || hp.Type == HardpointType.Launcher)
            {
                var whp = WeaponHardpoints.FirstOrDefault(x => x.name == hp.Transform);
                if (whp)
                {
                    Barrels.Add(hp, whp.FiringPoint);
                    BarrelIndices.Add(hp, 0);
                }
            }
        }

        void DamageSchematic(float damage, Shape hitShape)
        {
            foreach (var v in hitShape.Coordinates)
                hitShape[v] = hitShape[v] && hullShape[v];

            float hullDamage = 0;
            var damagePerCell = damage / hitShape.Coordinates.Length;
            foreach (var v in hitShape.Coordinates)
            {
                var d = damagePerCell;
                
                // Subtract surface damage from armor, passing on the remainder to the item and then to the hull
                var prev = entity.Armor[v.x, v.y];
                entity.Armor[v.x, v.y] = max(prev - d, 0);
                entity.ArmorDamage.OnNext((v, d));
                d = max(d - prev, 0);

                if (d > 0.1f)
                {
                    var item = entity.GearOccupancy[v.x, v.y];
                    if (item != null)
                    {
                        prev = item.EquippableItem.Durability;
                        item.EquippableItem.Durability = max(prev - d, 0);
                        entity.ItemDamage.OnNext((item, d));
                        d = max(d - prev, 0);
                    }
                }

                hullDamage += d;
            }

            if(hullDamage > .1f)
            {
                entity.Hull.Durability -= hullDamage;
                entity.HullDamage.OnNext(hullDamage);
            }
        }

        foreach (var collider in HullColliders)
        {
            collider.Splash.Subscribe(splash =>
            {
                var hitShape = new Shape(hullShape.Width, hullShape.Height);
                foreach (var v in hullShape.Coordinates)
                {
                    var localHitDirection = transform.InverseTransformDirection(splash.Direction);
                    var direction = normalize(float2(localHitDirection.x, localHitDirection.z));
                    var cellDot = dot(normalize(v - hullShape.CenterOfMass), direction);
                    if (cellDot < 0) hitShape[v] = true;
                }
                DamageSchematic(splash.Damage, hitShape);
            });
            
            collider.Hit.Subscribe(hit =>
            {
                Entity.IncomingHit.OnNext(hit.Source);
                var hardpointIndex = (int) hit.TexCoord.x - 1;
                
                var hitShape = new Shape(hullShape.Width, hullShape.Height);

                // U coordinate between 0-1 indicates a hit that didn't land directly on a hardpoint
                // Find the 2D position of the hit scaled to the schematic
                float2 hitPos = float2.zero;
                if (hardpointIndex < 0 || hardpointIndex >= typedHardpoints.Length)
                {
                    hitPos = float2(hit.TexCoord.x * hullShape.Width, hit.TexCoord.y * hullShape.Height);
                    // Search all schematic border cells for the cell which is closest to the hit position
                    var hitCell = int2(-1);
                    var distance = float.MaxValue;
                    foreach (var v in hullShape.Coordinates)
                    {
                        var cellDist = lengthsq(hitPos - v);
                        if (cellDist < distance)
                        {
                            distance = cellDist;
                            hitCell = v;
                            hitPos = v + float2(.5f);
                        }
                    }

                    hitShape[hitCell] = true;
                }
                else
                {
                    // Collider UV coordinates starting with 1 correspond to hardpoint index
                    var hardpoint = typedHardpoints[hardpointIndex];
                    
                    // Obtain the hull coordinates of all cells occupied by the hardpoint
                    var hardpointShape = ToShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells);
                    var hardpointCells = hullShape.Inset(hardpointShape, int2(hardpoint.PositionX, hardpoint.PositionY));
                    hitPos = hardpointCells.CenterOfMass;
                    foreach (var v in hardpointCells.Coordinates)
                        hitShape[v] = true;
                }
                
                for (int i = 0; i < Mathf.RoundToInt(hit.Spread); i++)
                {
                    hitShape = hitShape.Expand();
                }

                if (hit.Penetration > .5f)
                {
                    // Find the local 2D vector corresponding to the direction of the incoming hit
                    var localHitDirection = transform.InverseTransformDirection(hit.Direction);
                    var penetrationVector = normalize(float2(localHitDirection.x, localHitDirection.z));
                    // TODO: Bresenham's line algorithm
                    // March a ray through the ship from the hit position
                    var penetrationPoint = hitPos;
                    var penetrationDistance = 0f;
                    while (penetrationDistance < hit.Penetration && hullShape[int2(penetrationPoint)])
                    {
                        penetrationDistance += .5f;
                        hitShape[int2(penetrationPoint)] = true;
                        penetrationPoint += penetrationVector * .5f;
                    }
                }
                
                DamageSchematic(hit.Damage, hitShape);
            });
        }

        LookAtPoint = new GameObject($"{entity.Name} Look Point").transform;
        
        foreach (var articulationPoint in ArticulationPoints)
        {
            articulationPoint.Target = LookAtPoint;
        }

        _subscriptions.Add(Entity.HullDamage.Subscribe(_ =>
        {
            if (Entity.Hull.Durability < .01f)
            {
                if (!this) return;
                _destroyed = true;
                foreach (var gear in Entity.Equipment)
                {
                    if (gear != Entity.EquippedHull && Random.value < ZoneRenderer.Settings.LootDropProbability)
                    {
                        ZoneRenderer.DropItem(
                            Entity.Position, 
                            Random.onUnitSphere * ZoneRenderer.Settings.LootDropVelocity, 
                            gear.EquippableItem);
                    }
                }

                foreach (var cargo in Entity.CargoBays)
                {
                    foreach (var item in cargo.Cargo.Keys)
                    {
                        ZoneRenderer.DropItem(
                            Entity.Position, 
                            Random.onUnitSphere * ZoneRenderer.Settings.LootDropVelocity, 
                            item);
                    }
                }
                if (DestroyEffect != null)
                {
                    var t = Instantiate(DestroyEffect).transform;
                    t.position = transform.position;
                }
                entity.Zone.Entities.Remove(entity);
            }
        }));

        if (entity is OrbitalEntity orbital && orbital.IsSecureArea)
        {
            _influenceInstance = Instantiate(InfluencePrefab);
            _influenceInstance.transform.localScale = Vector3.one * orbital.SecurityRadius;
        }
    }

    private void OnSensorPingEnd()
    {
        Destroy(_currentPing.transform.gameObject);
    }

    public Transform GetBarrel(HardpointData hardpoint)
    {
        if (Barrels.ContainsKey(hardpoint))
        {
            var barrel = Barrels[hardpoint][BarrelIndices[hardpoint]];
            BarrelIndices[hardpoint] = (BarrelIndices[hardpoint] + 1) % Barrels[hardpoint].Length;
            return barrel;
        }

        return transform;
    }

    public virtual void Update()
    {
        if (_currentPing.transform)
        {
            _currentPing.transform.localScale = _sensor.PingRadius * Vector3.one;
            _currentPing.meshRenderer.material.SetFloat("_Depth", _pingBrightness * _sensor.PingBrightness);
        }
        if (_fading)
        {
            if (_fadingIn)
            {
                _fade += Time.deltaTime / _fadeTime;
                if (!_fadedElementsVisible && _fade > .01f)
                {
                    _fadedElementsVisible = true;
                    foreach (var mesh in _meshes.Keys) mesh.enabled = true;
                }
                if (_fade > 1)
                {
                    _fade = 1;
                    _fading = false;
                    OnFadedIn?.Invoke();
                    OnFadedIn = null;
                    ShowUnfadedElements();
                }
                foreach(var material in _fadeMaterials) material.SetFloat("_Fade", _fade);
            }
            else
            {
                _fade -= Time.deltaTime / _fadeTime;
                if(_unfadedElementsVisible && _fade < .99f) HideUnfadedElements();
                if (_fade < 0)
                {
                    _fadedElementsVisible = false;
                    foreach (var mesh in _meshes.Keys) mesh.enabled = false;
                    _fade = 0;
                    _fading = false;
                    OnFadedOut?.Invoke();
                    OnFadedOut = null;
                }
                foreach(var material in _fadeMaterials) material.SetFloat("_Fade", _fade);
            }
        }
        
        foreach (var x in RadiatorMeshes)
        {
            x.Value.material.SetFloat("_Emission", Entity.ItemManager.GameplaySettings.TemperatureEmissionCurve.Evaluate(x.Key.RadiatorTemperature));
        }

        foreach (var x in Barrels)
        {
            Entity.HardpointTransforms[x.Key] = (x.Value[0].position, x.Value[0].forward);
        }

        LookAtPoint.position = transform.position + (Vector3) Entity.LookDirection * 
            (Entity.Target.Value != null ? max(Entity.TargetRange,Entity.ItemManager.GameplaySettings.ConvergenceMinimumDistance) : 10000);
        LocalSpace.localPosition = transform.position = Entity.Position;
        if (_influenceInstance)
            _influenceInstance.position = new Vector3(Entity.Position.x, 0, Entity.Position.z);
    }

    public virtual void OnDestroy()
    {
        if(_sensor!=null)
            _sensor.OnPingEnd -= OnSensorPingEnd;
        if (_influenceInstance) Destroy(_influenceInstance.gameObject);
        Destroy(LocalSpace.gameObject);
        foreach(var x in _subscriptions)
            x.Dispose();
    }
}
