using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Strips the deleted integrator component from the pickup prefabs and points ZoneRenderer's four pickup fields in ARPG.unity at
// each prefab's ItemPickup, then saves the prefabs and the scene. Idempotent: a prefab with no missing script is left as
// it is, and the fields are set to the same values again.
//
//   Unity -batchmode -projectPath <project> -executeMethod WireFloatingItems.Run -logFile <log>
//
// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure. The menu item runs the same wiring.
public static class WireFloatingItems
{
    private const string ScenePath = "Assets/Scenes/ARPG.unity";
    private const string PickupFolder = "Assets/Prefabs/RPG/Pickups/";
    // The field each prefab filled before this cut (read from ARPG.unity at the cut's base). Icosahedron fills none.
    private static readonly (string Field, string Prefab)[] Fields =
    {
        ("SimpleCommodityPickup", "Tetrahedron"),
        ("CompoundCommodityPickup", "Hexahedron"),
        ("GearPickup", "Octahedron"),
        ("WeaponPickup", "Dodecahedron"),
    };
    private static readonly string[] Prefabs = { "Tetrahedron", "Hexahedron", "Octahedron", "Dodecahedron", "Icosahedron" };

    [MenuItem("Aetheria/Wire Floating Items")]
    public static void Menu() => Wire();

    public static void Run()
    {
        try
        {
            Wire();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Wire()
    {
        foreach (var name in Prefabs)
        {
            var path = PickupFolder + name + ".prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
                if (contents.GetComponent<ItemPickup>() == null) throw new InvalidOperationException($"{path} has no ItemPickup.");
                if (removed > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log($"WIRE_FLOATING_ITEMS {name} removed={removed}");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var renderer = UnityEngine.Object.FindFirstObjectByType<ZoneRenderer>();
        if (renderer == null) throw new InvalidOperationException("ARPG.unity has no ZoneRenderer.");
        var serialized = new SerializedObject(renderer);
        foreach (var (field, prefab) in Fields)
        {
            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupFolder + prefab + ".prefab").GetComponent<ItemPickup>();
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"ZoneRenderer has no {field}.");
            property.objectReferenceValue = pickup;
        }
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The scene did not save.");
        Debug.Log("WIRE_FLOATING_ITEMS ok");
    }
}
