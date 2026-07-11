using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AetheriaUnityBuild
{
    public static void BuildWindows()
    {
        const string scenePath = "Assets/Aetheria.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Aetheria Unity Client").AddComponent<AetheriaUnityClient>();
        EditorSceneManager.SaveScene(scene, scenePath);
        Directory.CreateDirectory("Build/Windows");
        var report = BuildPipeline.BuildPlayer(
            new[] { scenePath },
            "Build/Windows/Aetheria.exe",
            BuildTarget.StandaloneWindows64,
            BuildOptions.Development);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.InvalidOperationException("Aetheria Unity build failed: " + report.summary.result);
    }
}
