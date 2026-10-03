using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ShipModPreview
{
    private const string TemplatePath = "Assets/Content/Prefabs/ShipModTemplate.prefab";

    // Batch proof: -executeMethod ShipModPreview.Smoke -shipModPath <package/ship.cc> [-assemble]
    // -assemble also builds the game's ShipInstance from the package (ShipModShips.Assemble) and checks it against the
    // plan and against a clone, the way Instantiate makes one. That needs a GLB with real meshes on the map-icon,
    // hull-collider and emitter anchors; a nodes-only fixture fails it on purpose.
    public static async void Smoke()
    {
        var args = Environment.GetCommandLineArgs();
        var option = Array.IndexOf(args, "-shipModPath");
        if (option < 0 || option + 1 >= args.Length)
        {
            Debug.LogError("-shipModPath <package/ship.cc> is required");
            EditorApplication.Exit(1);
            return;
        }
        ShipModVisual.Instance visual = null;
        try
        {
            var package = ShipModCatalog.ReadPackage(args[option + 1]);
            visual = await ShipModVisual.LoadAsync(package, null);
            if (visual.Anchors.Count == 0 || visual.LineMesh == null || visual.LineMesh.vertexCount == 0)
                throw new InvalidOperationException("Mod visual did not import anchors and line points.");
            Debug.Log($"SHIP_MOD_VISUAL_SMOKE anchors={visual.Anchors.Count} points={visual.LineMesh.vertexCount} segments={visual.LineMesh.GetIndexCount(0) / 2}");
            if (Array.IndexOf(args, "-assemble") >= 0) CheckAssembly(visual, package);
            visual.Destroy();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            visual?.Destroy();
            EditorApplication.Exit(1);
        }
    }

    private static void CheckAssembly(ShipModVisual.Instance visual, ShipModCatalog.Package package)
    {
        var template = AssetDatabase.LoadAssetAtPath<ShipModTemplate>(TemplatePath);
        if (template == null) throw new InvalidOperationException($"{TemplatePath} is missing or has no ShipModTemplate.");
        var plan = ShipModPlan.Build(package.Hull, package.Visual);
        var ship = ShipModShips.Assemble(visual, package.Hull, package.Visual, template);
        CheckShip(ship, plan, "assembled");

        // Instantiate clones the assembled root, and Unity must remap every array reference into the clone.
        var clone = UnityEngine.Object.Instantiate(ship.gameObject).GetComponent<ShipInstance>();
        try { CheckShip(clone, plan, "clone"); }
        finally { UnityEngine.Object.DestroyImmediate(clone.gameObject); }
        Debug.Log($"SHIP_MOD_ASSEMBLY_SMOKE thrusters={plan.Thrusters.Length} " +
                  $"weapons={plan.Weapons.Length} radiators={plan.Radiators.Length}");
    }

    private static void CheckShip(ShipInstance ship, ShipModPlan plan, string what)
    {
        void Require(bool ok, string claim) { if (!ok) throw new InvalidOperationException($"{what}: {claim}"); }
        bool Inside(Component part) => part != null && part.transform.IsChildOf(ship.transform);
        Require(ship.HullColliders.Length == 1 && Inside(ship.HullColliders[0]) &&
                ship.HullColliders[0].GetComponent<MeshCollider>() != null, "one hull collider with a MeshCollider");
        Require(ship.HullColliders[0].GetComponentsInChildren<Renderer>(true).All(renderer =>
                    renderer.sharedMaterials.All(material => material == ship.InvisibleMaterial)),
                "a hull collider that draws nothing");
        Require(Inside(ship.MapIcon) && Inside(ship.Shield) && Inside(ship.TractorBeam), "map icon, shield and tractor inside the ship");
        Require(ship.ThrusterHardpoints.Select(node => node.name).SequenceEqual(plan.Thrusters) &&
                ship.ThrusterHardpoints.All(node => Inside(node) && Inside(node.Emitter) &&
                    node.Emitter.sharedMaterials.All(material => material == ship.InvisibleMaterial)),
                "thruster hardpoints with unrendered emitter meshes");
        Require(ship.RadiatorHardpoints.Select(node => node.name).SequenceEqual(plan.Radiators) &&
                ship.RadiatorHardpoints.All(node => Inside(node) && Inside(node.Mesh)), "radiator hardpoints with meshes");
        Require(ship.WeaponHardpoints.Select(node => node.name).SequenceEqual(plan.Weapons.Select(weapon => weapon.Mount)) &&
                ship.WeaponHardpoints.Zip(plan.Weapons, (node, weapon) =>
                    Inside(node) && node.FiringPoint.Select(point => point.name).SequenceEqual(weapon.Muzzles) &&
                    node.FiringPoint.All(Inside)).All(ok => ok), "weapon hardpoints with their muzzles in order");
    }

    [MenuItem("Aetheria/Preview Mod Ship Package")]
    private static async void Open()
    {
        var path = EditorUtility.OpenFilePanel("Ship authoring record", "GameData/Mods", "cc");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var visual = await ShipModVisual.LoadAsync(ShipModCatalog.ReadPackage(path), null);
            Undo.RegisterCreatedObjectUndo(visual.Root, "Preview mod ship");
            Selection.activeGameObject = visual.Root;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"Previewed {path}: {visual.Anchors.Count} anchors, {visual.LineMesh?.vertexCount ?? 0} line points.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}
