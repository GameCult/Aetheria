/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using UnityEngine;
using CultMath;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using static CultMath.math;

/// <summary>
/// Drives the volume render.
/// </summary>
[ExecuteInEditMode]
[RequireComponent( typeof( Camera ) )]
public class VolumeSampling : MonoBehaviour
{
    public Transform GridMesh;
    public Camera GridCamera;
    public GameSettings Settings;

    public RenderTexture NebulaSurfaceHeight;
    public RenderTexture NebulaPatchHeight;
    public RenderTexture NebulaPatch;
    public RenderTexture NebulaTint;

    private MeshRenderer _gridMeshRenderer;
    private Transform _gridTransform;
    private GlobalKeyword _keywordNoiseSlope;

    private void Start()
    {
        _keywordNoiseSlope = GlobalKeyword.Create("NOISE_SLOPE");

        if (GridMesh != null)
            _gridMeshRenderer = GridMesh.GetComponent<MeshRenderer>();
    }

    void Update()
    {
        if(_gridTransform==null)
            _gridTransform = GridCamera?.transform;
        // Shader needs to know the position and scale of cameras used to render input textures
        if(GridCamera != null)
            Shader.SetGlobalVector("_GridTransform", new Vector4(_gridTransform.position.x,_gridTransform.position.z,GridCamera.orthographicSize*2));

        var environment = ActionGameManager.Instance?.CurrentEnvironment ?? Settings.DefaultEnvironment;

        if (GridMesh != null)
        {
            GridMesh.gameObject.SetActive(environment.Grid.Enabled);
            _gridMeshRenderer.bounds = new Bounds(
                new Vector3(_gridTransform.position.x, environment.Grid.Offset, _gridTransform.position.z),
                new Vector3(GridCamera.orthographicSize * 2, 1000, GridCamera.orthographicSize * 2));
            // GridMesh.position = new Vector3(_gridTransform.position.x, environment.Grid.Offset, _gridTransform.position.z);
            // GridMesh.localScale = new Vector3(GridCamera.orthographicSize, 1, GridCamera.orthographicSize);
        }
        
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
        
        // The flow is the zone's: its settings and its time. Without a zone (menus) the same law runs on the presentation clock.
        LowerFlow(ActionGameManager.Instance?.Zone?.Settings.Flow ?? Settings.PlanetSettings.Flow, FlowClock);
        Shader.SetKeyword(_keywordNoiseSlope, environment.Noise.SlopeExponent != 0);
    }

    // GlobalKeyword's constructor only looks a keyword up; Create registers it. The shaders' pragmas and this owner agree on the names.
    static readonly GlobalKeyword KeywordFlowGlobal = GlobalKeyword.Create("FLOW_GLOBAL");
    static readonly GlobalKeyword KeywordFlowSlope = GlobalKeyword.Create("FLOW_SLOPE");

    /// <summary>
    /// The time the flow runs on: the zone's own (so drift stops with the sim and follows sim speed), or on menus,
    /// where no zone exists, the presentation clock.
    /// </summary>
    public static double FlowClock => ActionGameManager.Instance?.Zone?.ExactTime ?? Time.timeSinceLevelLoad;

    /// <summary>
    /// The one place that sets the flow uniforms and keywords: every shader flow term derives from FlowSettings and
    /// the flow clock through FlowField, so no presenter keeps its own flow clock.
    /// </summary>
    public static void LowerFlow(FlowSettings flow, double flowClock)
    {
        Shader.SetGlobalFloat("_FlowScale", flow.GlobalScale);
        Shader.SetGlobalFloat("_FlowAmplitude", flow.GlobalAmplitude);
        Shader.SetGlobalFloat("_FlowSlopeAmplitude", flow.SlopeAmplitude);
        Shader.SetGlobalFloat("_FlowSwirlAmplitude", flow.SwirlAmplitude);
        var scroll = FlowField.Scroll(flow, flowClock);
        Shader.SetGlobalVector("_FlowScroll", new Vector4(scroll.x, scroll.y));
        Shader.SetGlobalFloat("_FlowPeriod", flow.Period);
        Shader.SetGlobalFloat("_FlowCycle", FlowField.Cycle(flowClock, flow.Period));

        Shader.SetKeyword(KeywordFlowGlobal, flow.GlobalAmplitude != 0);
        Shader.SetKeyword(KeywordFlowSlope, flow.SlopeAmplitude != 0 || flow.SwirlAmplitude != 0);
    }
}
