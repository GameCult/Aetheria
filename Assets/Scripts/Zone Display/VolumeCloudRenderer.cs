using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// Based on github.com/yangrc1234/VolumeCloud
/// Generate halton sequence.
/// code from unity post-processing stack.
/// </summary>
public class HaltonSequence
{
    public int radix = 3;
    private int storedIndex = 0;
    public float Get() {
        float result = 0f;
        float fraction = 1f / (float)radix;
        int index = storedIndex;
        while (index > 0) {
            result += (float)(index % radix) * fraction;

            index /= radix;
            fraction /= (float)radix;
        }
        storedIndex++;
        return result;
    }
}

/// <summary>
/// Cloud renderer post processing.
/// </summary>
[ExecuteInEditMode,RequireComponent(typeof(Camera))]
public class VolumeCloudRenderer : EffectBase
{
    private static readonly Dictionary<Camera, VolumeCloudRenderer> RenderersByCamera = new Dictionary<Camera, VolumeCloudRenderer>();

    [Header("Render Settings")]
    [Range(0, 2)]
    public int downSample = 1;
    public Quality quality;

    private Material mat;
    private RenderTexture[] fullBuffer;
    private int fullBufferIndex;
    private RenderTexture undersampleBuffer;
    private Matrix4x4 prevV;
    private Camera mcam;
    private HaltonSequence sequence = new HaltonSequence() { radix = 3 };
    private const int TemporalHistoryVersion = 4;
    private int appliedTemporalHistoryVersion = -1;
    // The index of 4x4 pixels.
    private int frameIndex = 0;
    private bool firstFrame = true;
    private AetheriaClientState _runtimeState;
    private int _debugRenderCount;
    private int _debugLastRenderFrame = -1;

    [SerializeField]
    private Shader cloudShader;

    [SerializeField]
    private Texture2D ditherTexture;

    public static bool TryGetRenderer(Camera camera, out VolumeCloudRenderer renderer)
    {
        renderer = null;
        return camera != null
            && RenderersByCamera.TryGetValue(camera, out renderer)
            && renderer != null
            && renderer.isActiveAndEnabled;
    }

    public int DebugRenderCount => _debugRenderCount;
    public int DebugLastRenderFrame => _debugLastRenderFrame;
    public RenderTexture DebugUndersampleBuffer => undersampleBuffer;
    public RenderTexture DebugHistoryBuffer => fullBuffer != null && fullBuffer.Length > 0 ? fullBuffer[fullBufferIndex] : null;
    public RenderTexture DebugCurrentCloudBuffer => fullBuffer != null && fullBuffer.Length > 1 ? fullBuffer[fullBufferIndex ^ 1] : null;
    public bool DebugFirstFrame => firstFrame;

    private void OnEnable()
    {
        var camera = GetComponent<Camera>();
        if (camera != null)
            RenderersByCamera[camera] = this;
        ResetTemporalHistory();
    }

    private void OnDisable()
    {
        var camera = GetComponent<Camera>();
        if (camera != null && RenderersByCamera.TryGetValue(camera, out var renderer) && renderer == this)
            RenderersByCamera.Remove(camera);
    }

    void EnsureMaterial(bool force = false) {
        if (cloudShader == null)
            cloudShader = Shader.Find("Aetheria/CloudShader");

        if (mat == null || force) {
            mat = new Material(cloudShader);
            ResetTemporalHistory();
        }
    }

    private void ResetTemporalHistory()
    {
        firstFrame = true;
        appliedTemporalHistoryVersion = TemporalHistoryVersion;
        if (fullBuffer != null)
        {
            for (var i = 0; i < fullBuffer.Length; i++)
            {
                if (fullBuffer[i] != null)
                    fullBuffer[i].DiscardContents();
            }
        }
    }

    private void OnDestroy() {
        if (this.fullBuffer != null) {
            for (int i = 0; i < fullBuffer.Length; i++) {
                fullBuffer[i].Release();
                fullBuffer[i] = null;
            }
        }
        if (this.undersampleBuffer != null) {
            this.undersampleBuffer.Release();
            this.undersampleBuffer = null;
        }
    }

    private void Start() {
        this.EnsureMaterial(true);
        if (Application.isPlaying)
            quality = ResolveNebulaQuality(quality);
    }

    private Quality ResolveNebulaQuality(Quality fallback)
    {
        var snapshot = ResolvePlayerSettings();
        return Enum.TryParse(snapshot?.NebulaQuality, true, out Quality resolved)
            ? resolved
            : fallback;
    }

    private AetheriaRuntimePlayerSettingsDocument ResolvePlayerSettings()
    {
        try
        {
            _runtimeState ??= AetheriaUnityRuntimeClientProvider.RuntimeState("unity-volume-cloud-renderer");
            return _runtimeState.PlayerSettings.Latest();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria player settings for volume cloud renderer: {ex.Message}");
        }

        return null;
    }

    public void RenderClouds(UnsafeCommandBuffer cmd, Camera camera, TextureHandle colorTarget, TextureHandle depthTarget)
    {
        mcam = GetComponent<Camera>();
        if (camera != mcam || cloudShader == null)
            return;

        _debugRenderCount++;
        _debugLastRenderFrame = Time.frameCount;

        var width = mcam.pixelWidth >> downSample;
        var height = mcam.pixelHeight >> downSample;
        if (width <= 0 || height <= 0)
            return;

        EnsureMaterial();
        EnsureDitherTexture();

        if (appliedTemporalHistoryVersion != TemporalHistoryVersion)
            ResetTemporalHistory();

        EnsureArray(ref fullBuffer, 2);
        firstFrame |= EnsureRenderTarget(ref fullBuffer[0], width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);
        firstFrame |= EnsureRenderTarget(ref fullBuffer[1], width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);
        firstFrame |= EnsureRenderTarget(ref undersampleBuffer, width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);

        frameIndex = (frameIndex + 1) % 16;
        fullBufferIndex = (fullBufferIndex + 1) % 2;

        ApplyQualityKeywords();

        var gpuProjection = GL.GetGPUProjectionMatrix(mcam.projectionMatrix, true);
        mat.SetMatrix("_CamInvProj", (gpuProjection * mcam.worldToCameraMatrix).inverse);
        mat.SetVector("_ProjectionExtents", mcam.GetProjectionExtents());
        mat.SetFloat("_RaymarchOffset", sequence.Get());
        mat.SetVector("_TexelSize", undersampleBuffer.texelSize);
        mat.SetFloat("_ResetHistory", firstFrame ? 1.0f : 0.0f);

        mat.SetTexture("_UndersampleCloudTex", undersampleBuffer);
        mat.SetMatrix("_PrevVP", gpuProjection * prevV);

        cmd.SetRenderTarget(undersampleBuffer);
        cmd.SetViewport(new Rect(0, 0, width, height));
        cmd.ClearRenderTarget(false, true, Color.clear);
        cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1);

        mat.SetTexture("_MainTex", firstFrame ? undersampleBuffer : fullBuffer[fullBufferIndex]);

        cmd.SetRenderTarget(fullBuffer[fullBufferIndex ^ 1]);
        cmd.SetViewport(new Rect(0, 0, width, height));
        cmd.ClearRenderTarget(false, true, Color.clear);
        cmd.DrawProcedural(Matrix4x4.identity, mat, 1, MeshTopology.Triangles, 3, 1);

        mat.SetTexture("_CloudTex", fullBuffer[fullBufferIndex ^ 1]);

        if (depthTarget.IsValid())
            cmd.SetRenderTarget(colorTarget, depthTarget);
        else
            cmd.SetRenderTarget(colorTarget);
        cmd.SetViewport(mcam.pixelRect);
        cmd.DrawProcedural(Matrix4x4.identity, mat, 2, MeshTopology.Triangles, 3, 1);

        prevV = mcam.worldToCameraMatrix;
        firstFrame = false;
    }

    private void ApplyQualityKeywords()
    {
        if (firstFrame || quality == Quality.Ultra) {
            mat.EnableKeyword("ULTRA_QUALITY");
            mat.DisableKeyword("HIGH_QUALITY");
            mat.DisableKeyword("MEDIUM_QUALITY");
            mat.DisableKeyword("LOW_QUALITY");
        } else if (quality == Quality.High) {
            mat.DisableKeyword("ULTRA_QUALITY");
            mat.EnableKeyword("HIGH_QUALITY");
            mat.DisableKeyword("MEDIUM_QUALITY");
            mat.DisableKeyword("LOW_QUALITY");
        } else if (quality == Quality.Normal) {
            mat.DisableKeyword("ULTRA_QUALITY");
            mat.DisableKeyword("HIGH_QUALITY");
            mat.EnableKeyword("MEDIUM_QUALITY");
            mat.DisableKeyword("LOW_QUALITY");
        } else if (quality == Quality.Low) {
            mat.DisableKeyword("ULTRA_QUALITY");
            mat.DisableKeyword("HIGH_QUALITY");
            mat.DisableKeyword("MEDIUM_QUALITY");
            mat.EnableKeyword("LOW_QUALITY");
        }
    }

    private void EnsureDitherTexture()
    {
        if (ditherTexture == null)
            ditherTexture = Resources.Load<Texture2D>("LDR_LLL1_0");

        if (ditherTexture == null)
            return;

        Shader.SetGlobalTexture("_DitheringTex", ditherTexture);
        Shader.SetGlobalVector("_DitheringCoords", new Vector4(
            (float)Math.Max(1, Screen.width) / ditherTexture.width,
            (float)Math.Max(1, Screen.height) / ditherTexture.height,
            0,
            0));
    }

}
