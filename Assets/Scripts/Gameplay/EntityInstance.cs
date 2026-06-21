using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using UniRx;
using UnityEngine;
using cfloat3 = CultMath.float3;

public class EntityInstance : MonoBehaviour
{
    public MeshRenderer MapIcon;
    public Transform InfluencePrefab;
    public Material InvisibleMaterial;
    public ShieldManager Shield;
    public HullCollider[] HullColliders;

    public Transform[] EquipmentHardpoints;
    public RadiatorHardpoint[] RadiatorHardpoints;
    public ThrusterHardpoint[] ThrusterHardpoints;
    public WeaponHardpoint[] WeaponHardpoints;
    public ArticulationPoint[] ArticulationPoints;

    
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
    private Transform _influenceInstance;

    //private List<(GameObject source, EquippedItem item)> _audioSources = new List<(GameObject source, EquippedItem item)>();
    // private (Reactor reactor, GameObject sfxSource) _reactor;
    // private Dictionary<Radiator, GameObject> _radiatorSfx = new Dictionary<Radiator, GameObject>();
    
    private static AetheriaRuntimeCatalogItem FindTypedHull(ItemInstance hull)
    {
        return ActionGameManager.RuntimeCatalog?.FindItem(hull?.ItemKey ?? "");
    }

    private static Vector2 ToVector2(CultMath.float2 value) => new Vector2(value.x, value.y);
    private static Vector2 ToVector2(Unity.Mathematics.float2 value) => new Vector2(value.x, value.y);

    private static cfloat3 ToCult(Vector3 value) => new cfloat3(value.x, value.y, value.z);

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

        if(Shield)
            Shield.Entity = entity;
        foreach (var hullCollider in HullColliders) hullCollider.Entity = entity;

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

        LookAtPoint = new GameObject($"{entity.Name} Look Point").transform;
        
        foreach (var articulationPoint in ArticulationPoints)
        {
            articulationPoint.Target = LookAtPoint;
        }

        if (entity is OrbitalEntity orbital && orbital.IsSecureArea)
        {
            _influenceInstance = Instantiate(InfluencePrefab);
            _influenceInstance.transform.localScale = Vector3.one * orbital.SecurityRadius;
        }
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
            Entity.HardpointTransforms[x.Key] = (
                ToCult(x.Value[0].position),
                ToCult(x.Value[0].forward));
        }

        var entityPosition = (Vector3)AetheriaMath.ToUnity(Entity.CultPosition);
        var entityLookDirection = (Vector3)AetheriaMath.ToUnity(Entity.CultLookDirection);

        LookAtPoint.position = transform.position + entityLookDirection *
            (Entity.Target.Value != null ? Mathf.Max(Entity.TargetRange, Entity.ItemManager.GameplaySettings.ConvergenceMinimumDistance) : 10000);
        LocalSpace.localPosition = transform.position = entityPosition;
        if (_influenceInstance)
            _influenceInstance.position = new Vector3(entityPosition.x, 0, entityPosition.z);
    }

    public virtual void OnDestroy()
    {
        if (_influenceInstance) Destroy(_influenceInstance.gameObject);
        Destroy(LocalSpace.gameObject);
        foreach(var x in _subscriptions)
            x.Dispose();
    }
}
