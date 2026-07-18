using System;
using System.Collections;
using System.IO;
using GameCult.Eve.UnityScene;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class AetheriaUnityClient : MonoBehaviour
{
    private EveUnityCultMeshPlayableWorldProvider _provider;
    private EveUnityPlayableWorldClientBootstrap _bootstrap;
    private string _status = "Starting EveUnity...";

    private void Awake()
    {
        Application.runInBackground = true;
        var endpoint = Environment.GetEnvironmentVariable("EVEUNITY_RENDEZVOUS_ENDPOINT") ?? "rudp://127.0.0.1:3076";
        var surfaceId = Environment.GetEnvironmentVariable("EVEUNITY_SURFACE_ID") ?? "aetheria.pilot";
        var replicaPath = Path.Combine(Application.persistentDataPath, "aetheria-unity.cc");

        CreateView();
        gameObject.AddComponent<AetheriaUnityThermalPresentationSink>();
        _provider = gameObject.AddComponent<EveUnityCultMeshPlayableWorldProvider>();
        _provider.Configure(
            endpoint,
            replicaPath,
            providerId: "aetheria.daemon",
            surfaceId: surfaceId,
            requiredSurfaceKind: "interactive-world",
            clientRuntimeId: "aetheria-unity");
        _bootstrap = gameObject.AddComponent<EveUnityPlayableWorldClientBootstrap>();
        _bootstrap.ConfigureProvider(_provider);
        StartCoroutine(ConnectAfterFirstFrame());
    }

    private IEnumerator ConnectAfterFirstFrame()
    {
        yield return null;
        _status = "Connecting to the advertised Eve surface...";
        while (true)
        {
            var preparation = _provider.PrepareAsync();
            while (!preparation.IsCompleted)
                yield return null;
            Exception preparationError = null;
            try
            {
                preparation.GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                preparationError = error;
            }
            if (preparationError == null)
                break;
            _status = "Waiting for Aetheria daemon: " + preparationError.Message;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        var presentation = _bootstrap.Mount();
        _status = $"Connected: {_provider.Selection.ProviderId} / {_provider.Selection.SurfaceId} / {presentation.ActiveEntities} entities";
    }

    private static void CreateView()
    {
        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
            throw new InvalidOperationException("Aetheria Unity requires the project-configured Universal Render Pipeline asset.");
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 10000f;

        var lightObject = new GameObject("World Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12, 12, Math.Min(Screen.width - 24, 760), 54),
            _status + "\nWASD / left stick: move    Mouse 1 / controller action: activate");
    }
}
