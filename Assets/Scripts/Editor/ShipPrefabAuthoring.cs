using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// New ship models own their presentation anchors. The catalog still owns the schematic and gameplay rules.
public static class ShipPrefabAuthoring
{
    public const string ModelDirectory = "Assets/Models/Ships/";
    public const string PrefabDirectory = "Assets/Content/Prefabs/Ships/";

    private const string ShieldPath = "Assets/Content/Prefabs/Shield.prefab";
    private const string TractorPath = "Assets/Content/Prefabs/Tractor Beam.prefab";
    private const string PingPath = "Assets/Content/Prefabs/PingEffect.prefab";
    private const string ExplosionPath = "Assets/Prefabs/Fire & Explosion Effects/Prefabs/BigExplosion.prefab";
    private const string InvisiblePath = "Assets/Materials/Invisible.mat";
    private const string MapMaterialPath = "Assets/Materials/UI/Map Icons.mat";

    [MenuItem("Aetheria/Build Ship Prefab From Selected Model")]
    private static void BuildSelected()
    {
        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        try { Debug.Log($"Ship prefab built: {Build(path)}"); }
        catch (Exception ex) { Debug.LogError($"Ship prefab build failed for {path}: {ex.Message}"); }
    }

    [MenuItem("Aetheria/Build Ship Prefab From Selected Model", true)]
    private static bool CanBuildSelected() => IsShipModel(AssetDatabase.GetAssetPath(Selection.activeObject));

    public static bool IsShipModel(string path) =>
        !string.IsNullOrEmpty(path) && path.StartsWith(ModelDirectory, StringComparison.Ordinal) &&
        path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

    public static string Build(string modelPath)
    {
        if (!IsShipModel(modelPath)) throw new InvalidOperationException($"Expected an FBX in {ModelDirectory}");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) throw new InvalidOperationException($"Could not load model {modelPath}");

        var shipName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
        var outputPath = PrefabDirectory + shipName + ".prefab";
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) throw new InvalidOperationException($"Could not instantiate model {modelPath}");
        try
        {
            instance.name = shipName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Configure(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath, out var saved);
            if (!saved) throw new InvalidOperationException($"Unity did not save {outputPath}");
            return outputPath;
        }
        finally { Object.DestroyImmediate(instance); }
    }

    // Takes an imported model instance. Every ship-specific reference comes from its named hierarchy.
    public static void Configure(GameObject root)
    {
        var nodes = root.GetComponentsInChildren<Transform>(true);
        var named = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!Reserved(node.name)) continue;
            if (!named.TryAdd(node.name, node))
                throw new InvalidOperationException($"Duplicate reserved name {node.name}");
        }

        Transform Required(string name) => named.TryGetValue(name, out var found)
            ? found : throw new InvalidOperationException($"Missing {name}");

        var mapIcon = Required("SHIP.MapIcon").GetComponent<MeshRenderer>();
        if (mapIcon == null) throw new InvalidOperationException("SHIP.MapIcon needs a mesh renderer");
        var hull = Required("SHIP.HullCollider");
        var hullFilter = hull.GetComponent<MeshFilter>();
        var hullMesh = hullFilter != null ? hullFilter.sharedMesh : null;
        if (hullMesh == null) throw new InvalidOperationException("SHIP.HullCollider needs a mesh");
        var shieldMarker = Required("SHIP.Shield");
        var tractorMarker = Required("SHIP.TractorBeam");
        if (!PositiveScale(shieldMarker.localScale))
            throw new InvalidOperationException("SHIP.Shield scale must have three positive radii");

        var shieldPrefab = LoadComponent<ShieldManager>(ShieldPath);
        var tractorPrefab = LoadComponent<TractorBeam>(TractorPath);
        var ping = LoadComponent<Transform>(PingPath);
        var explosion = LoadAsset<GameObject>(ExplosionPath);
        var invisible = LoadAsset<Material>(InvisiblePath);
        var mapMaterial = LoadAsset<Material>(MapMaterialPath);
        var minimapLayer = LayerMask.NameToLayer("Minimap");
        var combatLayer = LayerMask.NameToLayer("Combat");
        if (minimapLayer < 0 || combatLayer < 0) throw new InvalidOperationException("Minimap or Combat layer is missing");

        var equipment = new List<Transform>();
        var thrusters = new List<ThrusterHardpoint>();
        var weapons = new List<WeaponHardpoint>();
        var radiators = new List<RadiatorHardpoint>();
        var pivots = new List<ArticulationPoint>();
        foreach (var pair in named.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var name = pair.Key;
            var node = pair.Value;
            if (name.StartsWith("HP.", StringComparison.Ordinal))
            {
                var parts = name.Split('.');
                if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[2]))
                    throw new InvalidOperationException($"Invalid hardpoint name {name}");
                equipment.Add(node);
                switch (parts[1])
                {
                    case "Equipment": break;
                    case "Thruster":
                        var emitter = DirectChild(node, "Emitter")?.GetComponent<MeshRenderer>();
                        if (emitter == null) throw new InvalidOperationException($"{name} needs an Emitter mesh child");
                        AddOrGet<ThrusterHardpoint>(node.gameObject).Emitter = emitter;
                        thrusters.Add(node.GetComponent<ThrusterHardpoint>());
                        break;
                    case "Weapon":
                        var muzzles = node.Cast<Transform>()
                            .Where(x => x.name.StartsWith("Muzzle.", StringComparison.Ordinal))
                            .Select(x => (node: x, number: MuzzleNumber(x.name)))
                            .OrderBy(x => x.number).ToArray();
                        if (muzzles.Length == 0 || muzzles.Select(x => x.number).Distinct().Count() != muzzles.Length)
                            throw new InvalidOperationException($"{name} needs uniquely numbered Muzzle.<n> children");
                        AddOrGet<WeaponHardpoint>(node.gameObject).FiringPoint = muzzles.Select(x => x.node).ToArray();
                        weapons.Add(node.GetComponent<WeaponHardpoint>());
                        break;
                    case "Radiator":
                        var mesh = DirectChild(node, "RadiatorMesh")?.GetComponent<MeshRenderer>();
                        if (mesh == null) throw new InvalidOperationException($"{name} needs a RadiatorMesh child");
                        AddOrGet<RadiatorHardpoint>(node.gameObject).Mesh = mesh;
                        radiators.Add(node.GetComponent<RadiatorHardpoint>());
                        break;
                    default: throw new InvalidOperationException($"Unknown hardpoint role in {name}");
                }
            }
            else if (name.StartsWith("Pivot.", StringComparison.Ordinal))
            {
                var values = name.Split('.');
                if (values.Length != 7) throw new InvalidOperationException($"Invalid pivot name {name}");
                var pivot = AddOrGet<ArticulationPoint>(node.gameObject);
                pivot.Group = ParseInt(values[1], name);
                pivot.YawMin = ParseInt(values[2], name);
                pivot.YawMax = ParseInt(values[3], name);
                pivot.PitchMin = ParseInt(values[4], name);
                pivot.PitchMax = ParseInt(values[5], name);
                pivot.Speed = ParseInt(values[6], name);
                if (pivot.YawMin > pivot.YawMax || pivot.PitchMin > pivot.PitchMax || pivot.Speed <= 0)
                    throw new InvalidOperationException($"Invalid articulation range or speed in {name}");
                pivots.Add(pivot);
            }
            else if (name.StartsWith("SHIP.", StringComparison.Ordinal) &&
                     name != "SHIP.MapIcon" && name != "SHIP.HullCollider" &&
                     name != "SHIP.Shield" && name != "SHIP.TractorBeam")
                throw new InvalidOperationException($"Unknown ship anchor {name}");
        }
        if (equipment.Count == 0) throw new InvalidOperationException("Ship has no HP.* hardpoints");

        mapIcon.sharedMaterial = mapMaterial;
        mapIcon.gameObject.layer = minimapLayer;
        hull.gameObject.layer = combatLayer;
        var hullRenderer = hull.GetComponent<MeshRenderer>();
        if (hullRenderer != null) hullRenderer.forceRenderingOff = true;
        var collider = AddOrGet<MeshCollider>(hull.gameObject);
        collider.sharedMesh = hullMesh;
        collider.convex = true;
        var hullSurface = AddOrGet<HullCollider>(hull.gameObject);

        var shield = (ShieldManager)PrefabUtility.InstantiatePrefab(shieldPrefab, shieldMarker);
        shield.transform.localPosition = Vector3.zero;
        shield.transform.localRotation = Quaternion.identity;
        if (shield.GetComponent<ShieldEnvelope>() == null) shield.gameObject.AddComponent<ShieldEnvelope>();
        var tractor = (TractorBeam)PrefabUtility.InstantiatePrefab(tractorPrefab, tractorMarker);
        tractor.transform.localPosition = Vector3.zero;
        tractor.transform.localRotation = Quaternion.identity;

        var ship = AddOrGet<ShipInstance>(root);
        ship.MapIcon = mapIcon;
        ship.InfluencePrefab = null;
        ship.PingPrefab = ping;
        ship.InvisibleMaterial = invisible;
        ship.Shield = shield;
        ship.HullColliders = new[] { hullSurface };
        ship.EquipmentHardpoints = equipment.ToArray();
        ship.ThrusterHardpoints = thrusters.ToArray();
        ship.WeaponHardpoints = weapons.ToArray();
        ship.RadiatorHardpoints = radiators.ToArray();
        ship.ArticulationPoints = pivots.ToArray();
        ship.DestroyEffect = explosion;
        ship.TractorBeam = tractor;
    }

    private static bool Reserved(string name) => name.StartsWith("HP.", StringComparison.Ordinal) ||
        name.StartsWith("Pivot.", StringComparison.Ordinal) || name.StartsWith("SHIP.", StringComparison.Ordinal);

    private static bool PositiveScale(Vector3 scale) => scale.x > 0 && scale.y > 0 && scale.z > 0;

    private static Transform DirectChild(Transform parent, string name) =>
        parent.Cast<Transform>().FirstOrDefault(x => x.name == name);

    private static int MuzzleNumber(string name)
    {
        if (!int.TryParse(name.Substring("Muzzle.".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            throw new InvalidOperationException($"Invalid muzzle name {name}");
        return n;
    }

    private static int ParseInt(string part, string name)
    {
        if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"Invalid numeric field in {name}");
        return value;
    }

    private static T AddOrGet<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static T LoadAsset<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new InvalidOperationException($"Missing shared asset {path}");
    }

    private static T LoadComponent<T>(string path) where T : Component
    {
        var prefab = LoadAsset<GameObject>(path);
        var component = prefab.GetComponent<T>();
        return component != null ? component : throw new InvalidOperationException($"{path} needs {typeof(T).Name}");
    }
}

// Model import finishes before generated prefabs read its hierarchy. Invalid exports leave the last valid prefab.
public sealed class ShipModelImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        var models = imported.Concat(moved).Where(ShipPrefabAuthoring.IsShipModel).Distinct().ToArray();
        if (models.Length == 0) return;
        EditorApplication.delayCall += () =>
        {
            foreach (var model in models)
            {
                try { Debug.Log($"Ship prefab built: {ShipPrefabAuthoring.Build(model)}"); }
                catch (Exception ex) { Debug.LogError($"Ship prefab build failed for {model}: {ex.Message}"); }
            }
        };
    }
}
