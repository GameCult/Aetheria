using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Builds the mine's prefab from the retired Mine Launcher's mesh, materials and pulse values, and hands it to the
// scene's ZoneRenderer. Run once (menu, or -executeMethod WireMines.Run in batchmode) and commit the diff.
public static class WireMines
{
    private const string PrefabPath = "Assets/Content/Prefabs/Mine.prefab";
    private const string ScenePath = "Assets/Scenes/ARPG.unity";

    // Mesh and materials of the deleted Mine Launcher prefab (git show 35ea0da4), by guid and local file id.
    private const string MeshGuid = "dab8e1a18a1b825458e8e6356cf00cd2";
    private const long MeshFileId = -5495902117074765545;
    private const string BigExplosionGuid = "e1b08032bec16ba4eb1ae23cbd572664";

    private static readonly (string guid, long fileId)[] Materials =
    {
        ("504c7bfe0d50ff64a88b8eac2ceb46f8", 3720744386649352057),
        ("a1d39ea984432bf44b4a0686afe2bf1a", 3720744386649352057),
        ("e879c37f3520bf14a8a6075581faf7b2", 2100000),
        ("58e16b7dbc1c31f4585e11544cdce127", 9178725544925813344),
    };

    [MenuItem("Aetheria/Wire Mines")]
    public static void Run()
    {
        var prefab = BuildPrefab();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var renderer = Object.FindFirstObjectByType<ZoneRenderer>(FindObjectsInactive.Include);
        if (renderer == null) throw new System.InvalidOperationException($"No ZoneRenderer in {ScenePath}");
        renderer.MinePrefab = prefab.GetComponent<MineInstance>();
        EditorUtility.SetDirty(renderer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"WireMines: {PrefabPath} built and assigned to ZoneRenderer.MinePrefab in {ScenePath}");
    }

    private static GameObject BuildPrefab()
    {
        var root = new GameObject("Mine");
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = Load<Mesh>(MeshGuid, MeshFileId);
            var meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = Materials.Select(m => Load<Material>(m.guid, m.fileId)).ToArray();

            var mine = root.AddComponent<MineInstance>();
            mine.MeshRenderer = meshRenderer;
            mine.EmissionSubmesh = 2;
            mine.ActiveCycleDuration = 1f;
            mine.ArmedCycleDuration = .25f;
            mine.ActiveEmission = 100f;
            mine.ArmedEmission = 1000f;
            mine.RotationSpeed = 1f;
            mine.GridOffset = 0f;
            mine.HitEffectScale = 10f;
            mine.HitEffect = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(BigExplosionGuid));
            if (mine.HitEffect == null) throw new System.InvalidOperationException("BigExplosion prefab did not load");
            // The retired launcher's pulse: bright, dipping to a tenth at mid-cycle, bright again.
            mine.EmissionCurve = new AnimationCurve(
                new Keyframe(0f, 1f, .051179443f, .051179443f, 0f, .24423963f),
                new Keyframe(.5f, .1f, 0f, 0f, .33333334f, .33333334f),
                new Keyframe(1f, 1f, 0f, 0f, .22580647f, 0f));

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static T Load<T>(string guid, long fileId) where T : Object
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) throw new System.InvalidOperationException($"No asset for guid {guid}");
        // A Substance archive's material is regenerated on import under another local id, so a path holding
        // exactly one T is that T whatever its id.
        var assets = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().ToArray();
        foreach (var asset in assets)
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long id) && id == fileId)
                return asset;
        if (assets.Length == 1) return assets[0];
        throw new System.InvalidOperationException($"No {typeof(T).Name} with file id {fileId} in {path}");
    }
}
