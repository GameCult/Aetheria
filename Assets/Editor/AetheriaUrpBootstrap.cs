using UnityEditor;
using UnityEditor.SceneManagement;
using Aetheria.Rendering.PostProcessing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;
using System;

public static class AetheriaUrpBootstrap
{
    private const string SettingsFolder = "Assets/Settings/Rendering";
    private const string PipelineAssetPath = SettingsFolder + "/AetheriaURPAsset.asset";
    private const string RendererAssetPath = SettingsFolder + "/AetheriaUniversalRenderer.asset";
    private const string MainMenuVolumeProfilePath = SettingsFolder + "/AetheriaMainMenuVolumeProfile.asset";
    private const string DefaultPostProcessDataPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

    [InitializeOnLoadMethod]
    private static void EnsureAetheriaRendererFeaturesOnLoad()
    {
        EditorApplication.delayCall += EnsureUrpAssetsAndAssign;
    }

    [MenuItem("Aetheria/Rendering/Ensure URP Assets")]
    public static void EnsureUrpAssetsAndAssign()
    {
        EnsureFolder(SettingsFolder);

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
        }

        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset == null)
        {
            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
        }

        EnsureRendererFeature(
            rendererData,
            "AetheriaVolumeSamplingRendererFeature",
            "Aetheria Volume Sampling");
        EnsureRendererFeature(
            rendererData,
            "AetheriaVolumeCloudRendererFeature",
            "Aetheria Volume Clouds");
        EnsureRendererFeature(
            rendererData,
            "AetheriaStardustRendererFeature",
            "Aetheria Stardust");
        EnsureRendererFeature(
            rendererData,
            "Aetheria.Rendering.PostProcessing.AetheriaFogBlurRendererFeature",
            "Aetheria Fog Blur");
        EnsurePostProcessing(rendererData);
        var mainMenuVolumeProfile = EnsureMainMenuVolumeProfile();
        EnsureLoadedCameraPostProcessing();
        EnsureLoadedMainMenuVolumes(mainMenuVolumeProfile);

        GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        QualitySettings.renderPipeline = pipelineAsset;

        EditorUtility.SetDirty(pipelineAsset);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Aetheria URP asset assigned: {PipelineAssetPath}");
    }

    private static void EnsureRendererFeature(
        UniversalRendererData rendererData,
        string featureTypeName,
        string featureName)
    {
        var featureType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(featureTypeName))
            .FirstOrDefault(type => type != null);
        if (featureType == null || !typeof(ScriptableRendererFeature).IsAssignableFrom(featureType))
        {
            Debug.LogWarning($"Aetheria URP bootstrap could not find renderer feature type {featureTypeName}.");
            return;
        }

        var existing = AssetDatabase
            .LoadAllAssetsAtPath(RendererAssetPath)
            .OfType<ScriptableRendererFeature>()
            .FirstOrDefault(feature => feature.GetType() == featureType);
        if (existing == null)
        {
            existing = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
            existing.name = featureName;
            AssetDatabase.AddObjectToAsset(existing, rendererData);
        }

        if (!rendererData.rendererFeatures.Contains(existing))
            rendererData.rendererFeatures.Add(existing);
    }

    private static void EnsurePostProcessing(UniversalRendererData rendererData)
    {
        var serialized = new SerializedObject(rendererData);
        var postProcessData = serialized.FindProperty("postProcessData");
        if (postProcessData == null || postProcessData.objectReferenceValue != null)
            return;

        postProcessData.objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static VolumeProfile EnsureMainMenuVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(MainMenuVolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "AetheriaMainMenuVolumeProfile";
            AssetDatabase.CreateAsset(profile, MainMenuVolumeProfilePath);
        }

        EnsureVolumeComponent(profile, out Tonemapping tonemapping);
        tonemapping.active = true;
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;

        EnsureVolumeComponent(profile, out ColorAdjustments colorAdjustments);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = -1f;

        EnsureVolumeComponent(profile, out Bloom bloom);
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.5f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 3f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.9f;
        bloom.tint.overrideState = true;
        bloom.tint.value = Color.white;

        EnsureVolumeComponent(profile, out Vignette vignette);
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.15f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;

        EnsureVolumeComponent(profile, out AetheriaFogBlur fogBlur);
        fogBlur.active = true;
        fogBlur.focusDistance.overrideState = true;
        fogBlur.focusDistance.value = 10f;
        fogBlur.aperture.overrideState = true;
        fogBlur.aperture.value = 5.6f;
        fogBlur.focalLength.overrideState = true;
        fogBlur.focalLength.value = 50f;
        fogBlur.kernelSize.overrideState = true;
        fogBlur.kernelSize.value = (int)AetheriaFogBlurKernelSize.Medium;

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void EnsureVolumeComponent<T>(VolumeProfile profile, out T component)
        where T : VolumeComponent
    {
        if (!profile.TryGet(out component))
        {
            component = profile.Add<T>(true);
        }
    }

    private static void EnsureLoadedCameraPostProcessing()
    {
        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (camera == null || !camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded)
                continue;

            var hasAetheriaVolumeRenderer = camera.GetComponent<VolumeCloudRenderer>() != null;
            if (!camera.CompareTag("MainCamera") && !hasAetheriaVolumeRenderer)
                continue;

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            var changed = false;
            changed |= SetIfDifferent(() => data.renderPostProcessing, value => data.renderPostProcessing = value, true);
            changed |= SetIfDifferent<LayerMask>(
                () => data.volumeLayerMask,
                value => data.volumeLayerMask = value,
                LayerMask.GetMask("PostProcessing"));
            changed |= SetIfDifferent(() => data.volumeTrigger, value => data.volumeTrigger = value, camera.transform);
            changed |= SetIfDifferent(() => data.requiresDepthOption, value => data.requiresDepthOption = value, CameraOverrideOption.On);
            changed |= SetIfDifferent(() => data.stopNaN, value => data.stopNaN = value, true);
            changed |= SetIfDifferent(() => data.dithering, value => data.dithering = value, true);

            changed |= RemoveLegacyPostProcessLayer(camera.gameObject);

            if (!changed)
                continue;

            EditorUtility.SetDirty(camera.gameObject);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        }
    }

    private static bool RemoveLegacyPostProcessLayer(GameObject cameraObject)
    {
        var removed = false;
        foreach (var component in cameraObject.GetComponents<Component>())
        {
            if (component == null)
                continue;

            var type = component.GetType();
            if (type.FullName != "UnityEngine.Rendering.PostProcessing.PostProcessLayer")
                continue;

            UnityEngine.Object.DestroyImmediate(component);
            removed = true;
        }

        return removed;
    }

    private static void EnsureLoadedMainMenuVolumes(VolumeProfile mainMenuVolumeProfile)
    {
        if (mainMenuVolumeProfile == null)
            return;

        foreach (var volume in Resources.FindObjectsOfTypeAll<Volume>())
        {
            if (volume == null || !volume.gameObject.scene.IsValid() || !volume.gameObject.scene.isLoaded)
                continue;

            var isMainMenuVolume = volume.gameObject.name == "Postprocessing" &&
                volume.gameObject.scene.path.Replace('\\', '/').EndsWith("/Main Menu.unity", StringComparison.OrdinalIgnoreCase);
            if (!isMainMenuVolume)
                continue;

            var changed = false;
            changed |= SetIfDifferent(() => volume.isGlobal, value => volume.isGlobal = value, true);
            changed |= SetIfDifferent(() => volume.sharedProfile, value => volume.sharedProfile = value, mainMenuVolumeProfile);

            var postProcessingLayer = LayerMask.NameToLayer("PostProcessing");
            if (postProcessingLayer >= 0 && volume.gameObject.layer != postProcessingLayer)
            {
                volume.gameObject.layer = postProcessingLayer;
                changed = true;
            }

            if (!changed)
                continue;

            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(volume.gameObject);
            EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
        }
    }

    private static bool SetIfDifferent<T>(Func<T> get, Action<T> set, T value)
    {
        if (Equals(get(), value))
            return false;

        set(value);
        return true;
    }

    private static void EnsureFolder(string folderPath)
    {
        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
