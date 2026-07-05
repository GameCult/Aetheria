/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using System.Collections.Generic;

/// <summary>
/// Drives the volume render.
/// </summary>
[ExecuteInEditMode]
[RequireComponent( typeof( Camera ) )]
public class VolumeSampling : MonoBehaviour
{
    private static readonly List<VolumeSampling> ActiveSamplers = new List<VolumeSampling>();
    public Transform GridMesh;
    public Camera GridCamera;
    public GameSettings Settings;

    public RenderTexture NebulaSurfaceHeight;
    public RenderTexture NebulaPatchHeight;
    public RenderTexture NebulaPatch;
    public RenderTexture NebulaTint;
    public AetheriaUnityRenderSplatViewportSource SplatViewportSource;
    public AetheriaRenderSplatLayerRenderer SplatLayerRenderer;
    public AetheriaSceneRenderSplatSource SceneSplatSource;

    [SerializeField]
    private bool useRuntimeRenderSplats;

    [SerializeField]
    private bool renderSplatsEveryFrame;

    [SerializeField]
    private float splatRenderIntervalSeconds = 0.25f;

    private MeshRenderer _gridMeshRenderer;
    private float _flowScroll;
    private Transform _gridTransform;
    private Camera _ownerCamera;
    private Vector2 _lastSplatCenter;
    private Vector2 _lastSplatSize;
    private Vector2 _publishedSplatCenter;
    private Vector2 _publishedSplatSize;
    private float _nextSplatRenderTime;
    private bool _hasRenderedSplats;
    private GlobalKeyword _keywordFlowGlobal;
    private GlobalKeyword _keywordFlowSlope;
    private GlobalKeyword _keywordNoiseSlope;

    public static void RenderForCamera(Camera camera)
    {
        if (camera == null)
            return;

        for (var i = 0; i < ActiveSamplers.Count; i++)
        {
            var sampler = ActiveSamplers[i];
            if (sampler != null && sampler.TargetsCamera(camera))
                sampler.RenderSamplingPass();
        }
    }

    private void OnEnable()
    {
        if (!ActiveSamplers.Contains(this))
            ActiveSamplers.Add(this);
    }

    private void OnDisable()
    {
        ActiveSamplers.Remove(this);
    }

    private void Start()
    {
        _ownerCamera = GetComponent<Camera>();
        _keywordFlowGlobal = GlobalKeyword.Create("FLOW_GLOBAL");
        _keywordFlowSlope = GlobalKeyword.Create("FLOW_SLOPE");
        _keywordNoiseSlope = GlobalKeyword.Create("NOISE_SLOPE");

        if (GridMesh != null)
            _gridMeshRenderer = GridMesh.GetComponent<MeshRenderer>();

        ResolveSplatRenderers();
    }

    void Update()
    {
        if(_gridTransform==null)
            _gridTransform = GridCamera?.transform;

        _flowScroll += Settings.DefaultEnvironment.Flow.GlobalScrollSpeed * Time.deltaTime;
        UpdateGridMeshBounds();
    }

    private bool TargetsCamera(Camera camera)
    {
        _ownerCamera ??= GetComponent<Camera>();
        return camera == _ownerCamera;
    }

    private void RenderSamplingPass()
    {
        if (_gridTransform == null)
            _gridTransform = GridCamera?.transform;

        RenderSplatTexturesIfNeeded();
        PublishSamplingGlobals();
    }

    private void UpdateGridMeshBounds()
    {
        var environment = Settings.DefaultEnvironment;

        if (GridMesh != null)
        {
            GridMesh.gameObject.SetActive(environment.Grid.Enabled);
            _gridMeshRenderer.bounds = new Bounds(
                new Vector3(_gridTransform.position.x, environment.Grid.Offset, _gridTransform.position.z),
                new Vector3(GridCamera.orthographicSize * 2, 1000, GridCamera.orthographicSize * 2));
            // GridMesh.position = new Vector3(_gridTransform.position.x, environment.Grid.Offset, _gridTransform.position.z);
            // GridMesh.localScale = new Vector3(GridCamera.orthographicSize, 1, GridCamera.orthographicSize);
        }
    }

    private void PublishSamplingGlobals()
    {
        var environment = Settings.DefaultEnvironment;

        ResolveSplatViewport(out var center, out var size);
        _publishedSplatCenter = center;
        _publishedSplatSize = size;

        // Shader sampling must use the same world-space viewport that produced the splat textures.
        Shader.SetGlobalVector("_GridTransform", new Vector4(center.x, center.y, size.x));

        // Volumetric sampling parameters are used by several shaders
        Shader.SetGlobalTexture("_NebulaSurfaceHeight", NebulaSurfaceHeight);
        Shader.SetGlobalTexture("_NebulaPatchHeight", NebulaPatchHeight);
        Shader.SetGlobalTexture("_NebulaPatch", NebulaPatch);
        Shader.SetGlobalTexture("_NebulaTint", NebulaTint);
        
        Shader.SetGlobalFloat("_NebulaFillDensity", environment.Nebula.FillDensity);
        Shader.SetGlobalFloat("_NebulaFillDistance", environment.Nebula.FillDistance);
        Shader.SetGlobalFloat("_NebulaFillExponent", environment.Nebula.FillExponent);
        Shader.SetGlobalFloat("_NebulaFillOffset", environment.Nebula.FillOffset);
        Shader.SetGlobalFloat("_NebulaPatchDensity", environment.Nebula.PatchDensity);
        Shader.SetGlobalFloat("_NebulaFloorOffset", environment.Nebula.FloorOffset);
        Shader.SetGlobalFloat("_NebulaFloorBlend", environment.Nebula.FloorBlend);
        Shader.SetGlobalFloat("_NebulaPatchBlend", environment.Nebula.PatchBlend);
        Shader.SetGlobalFloat("_NebulaLuminance", environment.Nebula.Luminance);
        Shader.SetGlobalFloat("_ExtinctionCoefficient", environment.Nebula.Extinction);
        Shader.SetGlobalFloat("_TintLodExponent", environment.Nebula.TintLodExponent);
        Shader.SetGlobalFloat("_SafetyDistance", environment.Nebula.SafetyDistance);
        
        Shader.SetGlobalFloat("_DynamicSkyBoost", environment.Lighting.DynamicSkyBoost);
        Shader.SetGlobalFloat("_DynamicLodHigh", environment.Lighting.DynamicLodHigh);
        Shader.SetGlobalFloat("_DynamicLodLow", environment.Lighting.DynamicLodLow);
        Shader.SetGlobalFloat("_DynamicIntensity", environment.Lighting.DynamicIntensity);
        
        Shader.SetGlobalFloat("_NebulaNoiseScale", environment.Noise.Scale);
        Shader.SetGlobalFloat("_NebulaNoiseExponent", environment.Noise.Exponent);
        Shader.SetGlobalFloat("_NebulaNoiseAmplitude", environment.Noise.Amplitude);
        Shader.SetGlobalFloat("_NebulaNoiseSpeed", environment.Noise.Speed);
        Shader.SetGlobalFloat("_NebulaNoiseSlopeExponent", environment.Noise.SlopeExponent);
        
        Shader.SetGlobalFloat("_FlowScale", environment.Flow.GlobalScale);
        Shader.SetGlobalFloat("_FlowAmplitude", environment.Flow.GlobalAmplitude);
        Shader.SetGlobalFloat("_FlowSlopeAmplitude", environment.Flow.SlopeAmplitude);
        Shader.SetGlobalFloat("_FlowSwirlAmplitude", environment.Flow.SwirlAmplitude);
        Shader.SetGlobalFloat("_FlowScroll", _flowScroll);
        Shader.SetGlobalFloat("_FlowPeriod", environment.Flow.Period);
        
        Shader.SetKeyword(_keywordFlowGlobal, environment.Flow.GlobalAmplitude != 0);
        Shader.SetKeyword(_keywordFlowSlope, environment.Flow.SlopeAmplitude != 0 || environment.Flow.SwirlAmplitude != 0);
        Shader.SetKeyword(_keywordNoiseSlope, environment.Noise.SlopeExponent != 0);
    }

    private void RenderSplatTexturesIfNeeded()
    {
        ResolveSplatRenderers();
        if (SplatLayerRenderer == null)
            return;

        ResolveSplatViewport(out var center, out var size);
        var document = ResolveSceneSplatDocument(center, size);
        if (document == null && useRuntimeRenderSplats)
            document = ResolveRuntimeSplatDocument(center, size);
        if (document == null)
            return;

        var viewportChanged = !_hasRenderedSplats ||
            !Approximately(center, _lastSplatCenter) ||
            !Approximately(size, _lastSplatSize);
        var intervalElapsed = Time.unscaledTime >= _nextSplatRenderTime;
        if (!renderSplatsEveryFrame && !viewportChanged && !intervalElapsed)
            return;

        SplatLayerRenderer.Render(document, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
        _lastSplatCenter = center;
        _lastSplatSize = size;
        _publishedSplatCenter = center;
        _publishedSplatSize = size;
        _nextSplatRenderTime = Time.unscaledTime + Mathf.Max(0.02f, splatRenderIntervalSeconds);
        _hasRenderedSplats = true;

        if (SplatLayerRenderer == null)
            return;

        NebulaSurfaceHeight = ResolveLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight, NebulaSurfaceHeight);
        NebulaPatchHeight = ResolveLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight, NebulaPatchHeight);
        NebulaPatch = ResolveLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.FogPatch, NebulaPatch);
        NebulaTint = ResolveLayerTexture(AetheriaRuntimeRenderSplatLayerKeys.FogTint, NebulaTint);
    }

    private AetheriaRuntimeRenderSplatsViewportDocument ResolveSceneSplatDocument(Vector2 center, Vector2 size)
    {
        if (SceneSplatSource == null)
            SceneSplatSource = GetComponent<AetheriaSceneRenderSplatSource>();
        return SceneSplatSource != null
            ? SceneSplatSource.BuildDocument(BuildViewportBounds(center, size))
            : null;
    }

    private AetheriaRuntimeRenderSplatsViewportDocument ResolveRuntimeSplatDocument(Vector2 center, Vector2 size)
    {
        if (SplatViewportSource == null)
            return null;

        SplatViewportSource.SetViewport(center, size);
        return SplatViewportSource.CurrentDocument;
    }

    private void ResolveSplatViewport(out Vector2 center, out Vector2 size)
    {
        size = GridCamera != null
            ? new Vector2(GridCamera.orthographicSize * 2, GridCamera.orthographicSize * 2)
            : new Vector2(1024, 1024);
        center = _gridTransform != null
            ? new Vector2(_gridTransform.position.x, _gridTransform.position.z)
            : Vector2.zero;
    }

    private static AetheriaRuntimeViewportBounds BuildViewportBounds(Vector2 center, Vector2 size)
    {
        var halfSize = size * 0.5f;
        return new AetheriaRuntimeViewportBounds
        {
            MinX = center.x - halfSize.x,
            MinY = center.y - halfSize.y,
            MaxX = center.x + halfSize.x,
            MaxY = center.y + halfSize.y
        };
    }

    private static bool Approximately(Vector2 lhs, Vector2 rhs)
    {
        return Mathf.Abs(lhs.x - rhs.x) < 0.001f &&
            Mathf.Abs(lhs.y - rhs.y) < 0.001f;
    }

    private RenderTexture ResolveLayerTexture(string layerKey, RenderTexture fallback)
    {
        return SplatLayerRenderer.TryGetTexture(layerKey, out var texture) && texture != null
            ? texture
            : fallback;
    }

    private void ResolveSplatRenderers()
    {
        if (SplatLayerRenderer == null)
            SplatLayerRenderer = GetComponent<AetheriaRenderSplatLayerRenderer>();
        if (SplatLayerRenderer == null)
            SplatLayerRenderer = gameObject.AddComponent<AetheriaRenderSplatLayerRenderer>();

        if (!useRuntimeRenderSplats)
            return;

        if (SplatViewportSource == null)
            SplatViewportSource = GetComponent<AetheriaUnityRenderSplatViewportSource>();
        if (SplatViewportSource == null)
            SplatViewportSource = gameObject.AddComponent<AetheriaUnityRenderSplatViewportSource>();
        SplatViewportSource.RenderInLateUpdate = false;
    }
}
