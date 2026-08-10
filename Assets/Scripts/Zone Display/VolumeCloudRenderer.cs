using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
[ImageEffectAllowedInSceneView]
[ExecuteInEditMode,RequireComponent(typeof(Camera))]
[ImageEffectOpaque]
public class VolumeCloudRenderer : EffectBase
{
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
    // The index of 4x4 pixels.
    private int frameIndex = 0;
    private bool firstFrame = true;

    [SerializeField]
    private Shader cloudShader;

    void EnsureMaterial(bool force = false) {
        if (mat == null || force) {
            mat = new Material(cloudShader);
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
            quality = ActionGameManager.PlayerSettings.GraphicsSettings.NebulaQuality;
    }

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        if (cloudShader == null) {
            Graphics.Blit(source, destination);
            return;
        }

        mcam = GetComponent<Camera>();
        var width = mcam.pixelWidth >> downSample;
        var height = mcam.pixelHeight >> downSample;

        this.EnsureMaterial();

        EnsureArray(ref fullBuffer, 2);
        firstFrame |= EnsureRenderTarget(ref fullBuffer[0], width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);
        firstFrame |= EnsureRenderTarget(ref fullBuffer[1], width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);
        firstFrame |= EnsureRenderTarget(ref undersampleBuffer, width , height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);

        frameIndex = (frameIndex + 1)% 16;
        fullBufferIndex = (fullBufferIndex + 1) % 2;

        /* Some code is from playdead TAA. */

        //1. Pass1, Render a undersampled buffer. The buffer is dithered using blue noise and halton sequence.
        //If it's first frame, force a high quality sample to make the initial history buffer good enough.
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

        mat.SetMatrix("_CamInvProj", (Camera.current.projectionMatrix * Camera.current.worldToCameraMatrix).inverse);
        mat.SetVector("_ProjectionExtents", mcam.GetProjectionExtents());
        mat.SetFloat("_RaymarchOffset", sequence.Get());
        mat.SetVector("_TexelSize", undersampleBuffer.texelSize);

        Graphics.Blit(null, undersampleBuffer, mat, 0);

        //2. Pass 2, blend undersampled image with history buffer to new buffer.
        mat.SetTexture("_UndersampleCloudTex", undersampleBuffer);
        mat.SetMatrix("_PrevVP", GL.GetGPUProjectionMatrix(mcam.projectionMatrix,false) * prevV);
        mat.SetVector("_ProjectionExtents", mcam.GetProjectionExtents());

        if (firstFrame) {   //Wait, is this the first frame? If it is, the history buffer is empty yet. It will cause glitch if we use it directly. Fill it using the undersample buffer.
            Graphics.Blit(undersampleBuffer, fullBuffer[fullBufferIndex]);
        }
        Graphics.Blit(fullBuffer[fullBufferIndex], fullBuffer[fullBufferIndex ^ 1], mat, 1);

        //3. Pass3, Calculate lighting, blend final cloud image with final image.
        mat.SetTexture("_CloudTex", fullBuffer[fullBufferIndex ^ 1]);
        Graphics.Blit(source, destination, mat, 2);

        //4. Cleanup
        prevV = mcam.worldToCameraMatrix;
        firstFrame = false;
    }
}