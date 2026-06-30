using System;
using System.IO;
using System.Text;
using GameCult.Aetheria.State.Verse;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AetheriaRenderProbe
{
    private const string RelativeOutputDirectory = "Temp/AetheriaRenderProbe";

    private static string ProjectRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static string OutputDirectory =>
        Path.Combine(ProjectRoot, RelativeOutputDirectory);

    private static string RequestPath =>
        Path.Combine(OutputDirectory, "request.txt");

    static AetheriaRenderProbe()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (!File.Exists(RequestPath))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        File.Delete(RequestPath);
        EditorApplication.delayCall += CaptureRequested;
    }

    private static void CaptureRequested()
    {
        try
        {
            Capture();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(
                Path.Combine(OutputDirectory, "error.txt"),
                error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Aetheria/Rendering/Capture Render Probe")]
    public static void Capture()
    {
        Directory.CreateDirectory(OutputDirectory);

        var summary = new StringBuilder();
        summary.AppendLine($"CapturedAtUtc: {DateTime.UtcNow:O}");
        summary.AppendLine($"ActiveScene: {SceneManager.GetActiveScene().path}");
        summary.AppendLine($"Playing: {EditorApplication.isPlaying}");
        summary.AppendLine();

        var camera = ResolveCamera();
        summary.AppendLine(camera != null
            ? $"Camera: {camera.name} orthographic={camera.orthographic} size={camera.orthographicSize} pixel={camera.pixelWidth}x{camera.pixelHeight}"
            : "Camera: <none>");

        if (camera != null)
            VolumeSampling.RenderForCamera(camera);

        DumpVolumeSamplers(summary);
        DumpVolumeCloudRenderers(camera, summary);
        DumpGlobalTexture(summary, "_NebulaSurfaceHeight", "nebula-surface-height.png");
        DumpGlobalTexture(summary, "_NebulaPatchHeight", "nebula-patch-height.png");
        DumpGlobalTexture(summary, "_NebulaPatch", "nebula-patch.png");
        DumpGlobalTexture(summary, "_NebulaTint", "nebula-tint.png");
        DumpGlobalTexture(summary, "_AetheriaGravityHeight", "gravity-height.png");

        if (camera != null)
            DumpCamera(camera, summary);

        File.WriteAllText(Path.Combine(OutputDirectory, "summary.txt"), summary.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Aetheria render probe wrote {Path.GetFullPath(OutputDirectory)}");
    }

    private static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (camera == null ||
                !camera.gameObject.scene.IsValid() ||
                !camera.gameObject.scene.isLoaded ||
                !camera.enabled)
            {
                continue;
            }

            return camera;
        }

        return null;
    }

    private static void DumpVolumeCloudRenderers(Camera camera, StringBuilder summary)
    {
        summary.AppendLine();
        summary.AppendLine("VolumeCloudRenderers:");

        foreach (var renderer in Resources.FindObjectsOfTypeAll<VolumeCloudRenderer>())
        {
            if (renderer == null ||
                !renderer.gameObject.scene.IsValid() ||
                !renderer.gameObject.scene.isLoaded)
            {
                continue;
            }

            summary.AppendLine($"- {renderer.name} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy}");
            summary.AppendLine($"  registeredForCamera={VolumeCloudRenderer.TryGetRenderer(camera, out var registered) && registered == renderer}");
            summary.AppendLine($"  renderCount={renderer.DebugRenderCount} lastFrame={renderer.DebugLastRenderFrame} firstFrame={renderer.DebugFirstFrame}");
            summary.AppendLine($"  Undersample={TextureFacts(renderer.DebugUndersampleBuffer)}");
            summary.AppendLine($"  History={TextureFacts(renderer.DebugHistoryBuffer)}");
            summary.AppendLine($"  CurrentCloud={TextureFacts(renderer.DebugCurrentCloudBuffer)}");

            if (renderer.DebugUndersampleBuffer != null)
                SaveTexture(renderer.DebugUndersampleBuffer, Path.Combine(OutputDirectory, "cloud-undersample.png"));
            if (renderer.DebugHistoryBuffer != null)
                SaveTexture(renderer.DebugHistoryBuffer, Path.Combine(OutputDirectory, "cloud-history.png"));
            if (renderer.DebugCurrentCloudBuffer != null)
                SaveTexture(renderer.DebugCurrentCloudBuffer, Path.Combine(OutputDirectory, "cloud-current.png"));
        }
    }

    private static void DumpVolumeSamplers(StringBuilder summary)
    {
        summary.AppendLine();
        summary.AppendLine("VolumeSamplers:");

        foreach (var sampler in Resources.FindObjectsOfTypeAll<VolumeSampling>())
        {
            if (sampler == null ||
                !sampler.gameObject.scene.IsValid() ||
                !sampler.gameObject.scene.isLoaded)
            {
                continue;
            }

            summary.AppendLine($"- {sampler.name} enabled={sampler.enabled} active={sampler.gameObject.activeInHierarchy}");
            summary.AppendLine($"  GridCamera={NameOf(sampler.GridCamera)} GridMesh={NameOf(sampler.GridMesh)}");
            summary.AppendLine($"  SceneSplatSource={NameOf(sampler.SceneSplatSource)} SplatLayerRenderer={NameOf(sampler.SplatLayerRenderer)}");
            summary.AppendLine($"  Surface={TextureFacts(sampler.NebulaSurfaceHeight)}");
            summary.AppendLine($"  PatchHeight={TextureFacts(sampler.NebulaPatchHeight)}");
            summary.AppendLine($"  Patch={TextureFacts(sampler.NebulaPatch)}");
            summary.AppendLine($"  Tint={TextureFacts(sampler.NebulaTint)}");
            DumpSamplerEnvironment(sampler, summary);

            if (sampler.SceneSplatSource != null)
                DumpSceneSplatDocument(sampler, summary);
        }
    }

    private static void DumpSamplerEnvironment(VolumeSampling sampler, StringBuilder summary)
    {
        var settings = sampler.Settings;
        var environment = settings != null ? settings.DefaultEnvironment : null;
        summary.AppendLine($"  Settings={NameOf(settings)}");
        if (environment == null)
        {
            summary.AppendLine("  Environment=<none>");
            return;
        }

        summary.AppendLine(
            $"  Nebula FillDensity={environment.Nebula.FillDensity} FillDistance={environment.Nebula.FillDistance} FillExponent={environment.Nebula.FillExponent} FillOffset={environment.Nebula.FillOffset}");
        summary.AppendLine(
            $"  Nebula FloorOffset={environment.Nebula.FloorOffset} FloorBlend={environment.Nebula.FloorBlend} PatchDensity={environment.Nebula.PatchDensity} PatchBlend={environment.Nebula.PatchBlend}");
        summary.AppendLine(
            $"  Nebula Luminance={environment.Nebula.Luminance} Extinction={environment.Nebula.Extinction} TintLodExponent={environment.Nebula.TintLodExponent} SafetyDistance={environment.Nebula.SafetyDistance}");
        summary.AppendLine(
            $"  Lighting DynamicSkyBoost={environment.Lighting.DynamicSkyBoost} DynamicLodHigh={environment.Lighting.DynamicLodHigh} DynamicLodLow={environment.Lighting.DynamicLodLow} DynamicIntensity={environment.Lighting.DynamicIntensity}");
        summary.AppendLine(
            $"  Noise Scale={environment.Noise.Scale} Amplitude={environment.Noise.Amplitude} Exponent={environment.Noise.Exponent} Speed={environment.Noise.Speed} SlopeExponent={environment.Noise.SlopeExponent}");
        summary.AppendLine(
            $"  Flow GlobalScale={environment.Flow.GlobalScale} GlobalAmplitude={environment.Flow.GlobalAmplitude} GlobalScrollSpeed={environment.Flow.GlobalScrollSpeed} Period={environment.Flow.Period} SlopeAmplitude={environment.Flow.SlopeAmplitude} SwirlAmplitude={environment.Flow.SwirlAmplitude}");
        summary.AppendLine(
            $"  Grid Enabled={environment.Grid.Enabled} Offset={environment.Grid.Offset}");
    }

    private static void DumpSceneSplatDocument(VolumeSampling sampler, StringBuilder summary)
    {
        var size = sampler.GridCamera != null
            ? new Vector2(sampler.GridCamera.orthographicSize * 2f, sampler.GridCamera.orthographicSize * 2f)
            : new Vector2(1024f, 1024f);
        var centerTransform = sampler.GridCamera != null ? sampler.GridCamera.transform : sampler.transform;
        var center = centerTransform != null
            ? new Vector2(centerTransform.position.x, centerTransform.position.z)
            : Vector2.zero;
        var half = size * 0.5f;
        var document = sampler.SceneSplatSource.BuildDocument(new AetheriaRuntimeRtsViewportBounds
        {
            MinX = center.x - half.x,
            MinY = center.y - half.y,
            MaxX = center.x + half.x,
            MaxY = center.y + half.y
        });

        summary.AppendLine($"  SceneDocument layers={document.Layers?.Count ?? 0} splats={document.Splats?.Count ?? 0}");
    }

    private static void DumpGlobalTexture(StringBuilder summary, string globalName, string fileName)
    {
        var texture = Shader.GetGlobalTexture(globalName);
        summary.AppendLine($"{globalName}: {TextureFacts(texture)}");
        if (texture != null)
            SaveTexture(texture, Path.Combine(OutputDirectory, fileName));
    }

    private static void DumpCamera(Camera camera, StringBuilder summary)
    {
        var width = Mathf.Max(1, camera.pixelWidth > 0 ? camera.pixelWidth : 1920);
        var height = Mathf.Max(1, camera.pixelHeight > 0 ? camera.pixelHeight : 1080);
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            SaveRenderTexture(target, Path.Combine(OutputDirectory, "camera-render.png"));
            summary.AppendLine($"CameraRender: {width}x{height} -> camera-render.png");
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void SaveTexture(Texture texture, string path)
    {
        var width = Mathf.Max(1, texture.width);
        var height = Mathf.Max(1, texture.height);
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, target);
            SaveRenderTexture(target, path);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void SaveRenderTexture(RenderTexture target, string path)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
    }

    private static string NameOf(UnityEngine.Object value)
    {
        return value != null ? value.name : "<none>";
    }

    private static string TextureFacts(Texture texture)
    {
        if (texture == null)
            return "<none>";

        return $"{texture.name} {texture.width}x{texture.height} {texture.GetType().Name}";
    }
}
