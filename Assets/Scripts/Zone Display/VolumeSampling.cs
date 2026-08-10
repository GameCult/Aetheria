/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;

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
    private float _flowScroll;
    private Transform _gridTransform;
    private GlobalKeyword _keywordFlowGlobal;
    private GlobalKeyword _keywordFlowSlope;
    private GlobalKeyword _keywordNoiseSlope;

    private void Start()
    {
        _keywordFlowGlobal = GlobalKeyword.Create("FLOW_GLOBAL");
        _keywordFlowSlope = GlobalKeyword.Create("FLOW_SLOPE");
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
        
        Shader.SetGlobalFloat("_FlowScale", environment.Flow.GlobalScale);
        Shader.SetGlobalFloat("_FlowAmplitude", environment.Flow.GlobalAmplitude);
        Shader.SetGlobalFloat("_FlowSlopeAmplitude", environment.Flow.SlopeAmplitude);
        Shader.SetGlobalFloat("_FlowSwirlAmplitude", environment.Flow.SwirlAmplitude);
        _flowScroll += environment.Flow.GlobalScrollSpeed * Time.deltaTime;
        Shader.SetGlobalFloat("_FlowScroll", _flowScroll);
        Shader.SetGlobalFloat("_FlowPeriod", environment.Flow.Period);
        
        Shader.SetKeyword(_keywordFlowGlobal, environment.Flow.GlobalAmplitude != 0);
        Shader.SetKeyword(_keywordFlowSlope, environment.Flow.SlopeAmplitude != 0 || environment.Flow.SwirlAmplitude != 0);
        Shader.SetKeyword(_keywordNoiseSlope, environment.Noise.SlopeExponent != 0);
    }
}
