using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using UnityEngine;
using Object = UnityEngine.Object;

// Mod ships in the running game. Preload imports each catalog hull's GLB once, at boot, into an inactive assembled
// prototype keyed by the hull's record key; Instantiate then clones it synchronously, so LoadEntity never loads async.
// The prototypes are cache-only: rebuilt every boot from the catalog, the mod GLBs and the template.
public static class ShipModShips
{
    private static readonly Dictionary<CultRecordKey, GameObject> Prototypes = new Dictionary<CultRecordKey, GameObject>();
    private static CultCache _catalog;

    // Set by Preload; complete when every prototype is ready, faulted when any mod ship failed to import or assemble.
    public static Task Loading { get; private set; } = Task.CompletedTask;

    public static Task Preload(CultCache catalog, string modsRoot)
    {
        Loading = PreloadAsync(catalog, modsRoot);
        Loading.ContinueWith(task => Debug.LogException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
        return Loading;
    }

    private static async Task PreloadAsync(CultCache catalog, string modsRoot)
    {
        _catalog = catalog;
        var hulls = catalog.GetAll<HullData>().Where(hull => hull.Visual.IsSet()).ToArray();
        if (hulls.Length == 0) return;
        var template = EngineAssets.Load<ShipModTemplate>(ShipModTemplate.Key);
        if (template == null)
            throw new InvalidOperationException($"The ship mod template prefab ({ShipModTemplate.Key}) is not loadable.");
        var root = new GameObject("Mod Ship Prototypes");
        root.SetActive(false);
        // DontDestroyOnLoad is play-mode only; the edit-mode play smoke builds the same prototypes
        // without it, and nothing there loads scenes.
        if (Application.isPlaying) Object.DontDestroyOnLoad(root);
        foreach (var hull in hulls)
        {
            var package = ShipModCatalog.PackageOf(catalog, hull, modsRoot);
            var visual = await ShipModVisual.LoadAsync(package, root.transform);
            try
            {
                visual.Root.SetActive(false);
                Assemble(visual, package.Hull, package.Visual, template);
                Prototypes[catalog.RefOf(hull).Key] = visual.Root;
            }
            catch
            {
                visual.Destroy();
                throw;
            }
        }
    }

    // A live ship body for a mod hull. Refuses loudly when the prototype is missing: nothing is loaded here.
    public static GameObject Instantiate(HullData hull, Transform parent)
    {
        if (!Loading.IsCompletedSuccessfully)
            throw new InvalidOperationException($"{hull.Name}: mod ships are not preloaded ({Loading.Status}).");
        if (!Prototypes.TryGetValue(_catalog.RefOf(hull).Key, out var prototype))
            throw new InvalidOperationException($"{hull.Name}: no mod ship prototype for this hull.");
        var body = Object.Instantiate(prototype, parent, false);
        body.SetActive(true);
        return body;
    }

    // Fills a ShipInstance on the visual's root from its anchors, as ShipModPlan says; the shared effects come from the
    // template. Nothing here decides a slot: the plan does. The schematic lines stay in the prototype but are hidden in
    // world; the in-world ship is the GLB.
    public static ShipInstance Assemble(ShipModVisual.Instance visual, HullData hull, ShipAuthoring ship, ShipModTemplate template)
    {
        var plan = ShipModPlan.Build(hull, ship);
        var minimap = LayerMask.NameToLayer("Minimap");
        var combat = LayerMask.NameToLayer("Combat");
        if (minimap < 0 || combat < 0) throw new InvalidOperationException("Minimap or Combat layer is missing");

        // The name a catalog hardpoint matches at runtime is its anchor id.
        Transform Node(string id)
        {
            var node = visual.Anchors[id];
            node.name = id;
            return node;
        }
        MeshRenderer Renderer(string id)
        {
            var found = Node(id).GetComponentInChildren<MeshRenderer>(true);
            if (found == null) throw new InvalidOperationException($"{ship.Id}: anchor {id} needs a mesh in its GLB node.");
            return found;
        }

        var mapIcon = Renderer(plan.MapIcon);
        mapIcon.sharedMaterial = template.MapIcon;
        mapIcon.gameObject.layer = minimap;

        var hullNode = Node(plan.HullCollider);
        var hullFilter = hullNode.GetComponent<MeshFilter>();
        var hullMesh = hullFilter != null ? hullFilter.sharedMesh : null;
        if (hullMesh == null)
            throw new InvalidOperationException($"{ship.Id}: hull-collider anchor {plan.HullCollider} needs a mesh in its GLB node.");
        if (!hullMesh.isReadable)
            throw new InvalidOperationException($"{ship.Id}: the hull-collider mesh imported unreadable, so it cannot back a MeshCollider.");
        hullNode.gameObject.layer = combat;
        var hullRenderer = hullNode.GetComponent<MeshRenderer>();
        if (hullRenderer != null) hullRenderer.forceRenderingOff = true;
        var collider = AddOrGet<MeshCollider>(hullNode.gameObject);
        collider.sharedMesh = hullMesh;
        collider.convex = true;
        var surface = AddOrGet<HullCollider>(hullNode.gameObject);

        var shieldMarker = Node(plan.Shield);
        var scale = shieldMarker.localScale;
        if (!(scale.x > 0 && scale.y > 0 && scale.z > 0))
            throw new InvalidOperationException($"{ship.Id}: the shield anchor scale must have three positive radii.");
        var shield = Object.Instantiate(template.Shield.GetComponent<ShieldManager>(), shieldMarker, false);
        Reset(shield.transform);
        AddOrGet<ShieldEnvelope>(shield.gameObject);
        var tractor = Object.Instantiate(template.TractorBeam.GetComponent<TractorBeam>(), Node(plan.Tractor), false);
        Reset(tractor.transform);

        var thrusters = plan.Thrusters.Select(id =>
        {
            // The thruster's mesh is its exhaust's emission surface, never drawn (ruling thrusters-radiators-are-meshes).
            // The invisible material is serialized, so it survives the prototype's clone, and EntityInstance keeps it as
            // this renderer's visible material; forceRenderingOff would not survive Instantiate.
            var hardpoint = AddOrGet<ThrusterHardpoint>(Node(id).gameObject);
            hardpoint.Emitter = Renderer(id);
            hardpoint.Emitter.sharedMaterials = Enumerable.Repeat(template.Invisible, hardpoint.Emitter.sharedMaterials.Length).ToArray();
            return hardpoint;
        }).ToArray();
        var radiators = plan.Radiators.Select(id =>
        {
            var hardpoint = AddOrGet<RadiatorHardpoint>(Node(id).gameObject);
            hardpoint.Mesh = Renderer(id);
            return hardpoint;
        }).ToArray();
        var weapons = plan.Weapons.Select(weapon =>
        {
            var hardpoint = AddOrGet<WeaponHardpoint>(Node(weapon.Mount).gameObject);
            hardpoint.FiringPoint = weapon.Muzzles.Select(Node).ToArray();
            return hardpoint;
        }).ToArray();

        var lines = visual.Root.transform.Find("Schematic Lines");
        if (lines != null) lines.gameObject.SetActive(false);

        var instance = AddOrGet<ShipInstance>(visual.Root);
        instance.MapIcon = mapIcon;
        instance.InfluencePrefab = null;
        instance.PingPrefab = template.Ping.transform;
        instance.InvisibleMaterial = template.Invisible;
        instance.Shield = shield;
        instance.HullColliders = new[] { surface };
        instance.ThrusterHardpoints = thrusters;
        instance.WeaponHardpoints = weapons;
        instance.RadiatorHardpoints = radiators;
        instance.ArticulationPoints = Array.Empty<ArticulationPoint>();
        instance.DestroyEffect = template.DestroyEffect;
        instance.TractorBeam = tractor;
        return instance;
    }

    private static void Reset(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    private static T AddOrGet<T>(GameObject gameObject) where T : Component =>
        gameObject.TryGetComponent<T>(out var found) ? found : gameObject.AddComponent<T>();
}
