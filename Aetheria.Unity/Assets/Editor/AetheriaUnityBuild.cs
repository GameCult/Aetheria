using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AetheriaUnityBuild
{
    public static void BuildWindows()
    {
        EnsureRenderPipeline();
        const string scenePath = "Assets/Aetheria.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Aetheria Unity Client").AddComponent<AetheriaUnityClient>();
        EditorSceneManager.SaveScene(scene, scenePath);
        Directory.CreateDirectory("Build/Windows");
        var report = BuildPipeline.BuildPlayer(
            new[] { scenePath },
            "Build/Windows/Aetheria.exe",
            BuildTarget.StandaloneWindows64,
            BuildOptions.None);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.InvalidOperationException("Aetheria Unity build failed: " + report.summary.result);
    }

    private static void EnsureRenderPipeline()
    {
        const string settingsDirectory = "Assets/Settings";
        const string rendererPath = settingsDirectory + "/AetheriaUniversalRenderer.asset";
        const string pipelinePath = settingsDirectory + "/AetheriaUniversalRenderPipeline.asset";
        if (!AssetDatabase.IsValidFolder(settingsDirectory))
            AssetDatabase.CreateFolder("Assets", "Settings");

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Aetheria Universal Renderer";
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            pipeline.name = "Aetheria Universal Render Pipeline";
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }

        var serialized = new SerializedObject(pipeline);
        var rendererList = serialized.FindProperty("m_RendererDataList");
        rendererList.arraySize = 1;
        rendererList.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
    }
}
