using System;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds the impact-feedback tuning object to ARPG.unity and the impulse listener to its follow camera, then saves the
// scene. Idempotent: a scene that already has them is left as it is.
//
//   Unity -batchmode -projectPath <project> -executeMethod WireImpactFeedback.Run -logFile <log>
//
// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure. The menu item runs the same wiring.
public static class WireImpactFeedback
{
    private const string ScenePath = "Assets/Scenes/ARPG.unity";
    // The weapons' own hit effect (the nested prefab AC5's Projectile uses as HitEffect): no new art.
    private const string HullEffectPath = "Assets/Prefabs/Fire & Explosion Effects/Prefabs/TinyExplosion.prefab";

    [MenuItem("Aetheria/Wire Impact Feedback")]
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
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = UnityEngine.Object.FindFirstObjectByType<ActionGameManager>();
        if (manager == null || manager.FollowCamera == null) throw new InvalidOperationException("ARPG.unity has no ActionGameManager with a FollowCamera.");

        if (manager.FollowCamera.GetComponent<CinemachineImpulseListener>() == null)
            manager.FollowCamera.gameObject.AddComponent<CinemachineImpulseListener>();

        if (UnityEngine.Object.FindFirstObjectByType<ImpactFeedback>() == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HullEffectPath);
            if (prefab == null) throw new InvalidOperationException($"No prefab at {HullEffectPath}.");
            var root = new GameObject("Impact Feedback");
            var feedback = root.AddComponent<ImpactFeedback>();
            var effect = (GameObject) PrefabUtility.InstantiatePrefab(prefab, root.transform);
            feedback.HullEffect = effect.GetComponent<Prototype>();
            if (feedback.HullEffect == null) throw new InvalidOperationException("The hull effect prefab has no Prototype.");
            EditorUtility.SetDirty(feedback);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The scene did not save.");
        Debug.Log("WIRE_IMPACT_FEEDBACK ok");
    }
}
