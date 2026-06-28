using System;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

public sealed class AetheriaRenderSplatLayerRenderer : MonoBehaviour
{
    [SerializeField]
    private AetheriaRenderSplatRasterizer rasterizer;

    [SerializeField]
    private AetheriaRenderSplatLayerBinding[] bindings =
    {
        new AetheriaRenderSplatLayerBinding
        {
            LayerKey = AetheriaRuntimeRenderSplatLayerKeys.GravityHeight,
            MaterialTextureProperty = "_DetailTex",
            GlobalTextureName = "_AetheriaGravityHeight"
        },
        new AetheriaRenderSplatLayerBinding
        {
            LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogTint,
            MaterialTextureProperty = "_DetailTex",
            GlobalTextureName = "_NebulaTint"
        },
        new AetheriaRenderSplatLayerBinding
        {
            LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight,
            GlobalTextureName = "_NebulaSurfaceHeight",
            WidthScale = 0.5f,
            HeightScale = 0.5f
        },
        new AetheriaRenderSplatLayerBinding
        {
            LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight,
            GlobalTextureName = "_NebulaPatchHeight",
            WidthScale = 0.5f,
            HeightScale = 0.5f
        },
        new AetheriaRenderSplatLayerBinding
        {
            LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogPatch,
            GlobalTextureName = "_NebulaPatch",
            WidthScale = 0.5f,
            HeightScale = 0.5f
        },
        new AetheriaRenderSplatLayerBinding
        {
            LayerKey = AetheriaRuntimeRenderSplatLayerKeys.Influence,
            MaterialTextureProperty = "_DetailTex",
            GlobalTextureName = "_AetheriaInfluence"
        }
    };

    private readonly Dictionary<string, RenderTexture> _texturesByLayerKey =
        new Dictionary<string, RenderTexture>(StringComparer.Ordinal);

    private void Reset()
    {
        rasterizer = GetComponent<AetheriaRenderSplatRasterizer>();
    }

    public bool TryGetTexture(string layerKey, out RenderTexture texture)
    {
        return _texturesByLayerKey.TryGetValue(layerKey ?? "", out texture);
    }

    public void Render(
        AetheriaRuntimeRenderSplatsViewportDocument document,
        int width,
        int height)
    {
        if (document == null)
            return;

        ResolveRasterizer();
        if (rasterizer == null)
            return;

        var layers = document.Layers ?? Array.Empty<AetheriaRuntimeRenderSplatLayerDefinition>();
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            if (layer == null || string.IsNullOrWhiteSpace(layer.LayerKey))
                continue;

            foreach (var binding in bindings ?? Array.Empty<AetheriaRenderSplatLayerBinding>())
            {
                if (binding == null ||
                    !binding.Enabled ||
                    !string.Equals(binding.LayerKey, layer.LayerKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var texture = EnsureTexture(binding, layer, width, height);
                rasterizer.RenderLayerToTarget(
                    document,
                    texture,
                    layerIndex,
                    ResolveMaterialPass(layer.BlendMode),
                    ResolveClearColor(layer));
                ApplyBinding(binding, texture);
                _texturesByLayerKey[layer.LayerKey] = texture;
            }
        }
    }

    private void ResolveRasterizer()
    {
        if (rasterizer != null)
            return;

        rasterizer = GetComponent<AetheriaRenderSplatRasterizer>();
        if (rasterizer == null)
            rasterizer = gameObject.AddComponent<AetheriaRenderSplatRasterizer>();
    }

    private RenderTexture EnsureTexture(
        AetheriaRenderSplatLayerBinding binding,
        AetheriaRuntimeRenderSplatLayerDefinition layer,
        int width,
        int height)
    {
        var targetWidth = math.max(1, Mathf.RoundToInt(width * Mathf.Max(0.01f, binding.WidthScale)));
        var targetHeight = math.max(1, Mathf.RoundToInt(height * Mathf.Max(0.01f, binding.HeightScale)));
        var format = ResolveGraphicsFormat(binding.GraphicsFormatOverride, layer.GraphicsFormat);
        var existing = binding.TargetTexture;
        if (existing != null &&
            existing.width == targetWidth &&
            existing.height == targetHeight &&
            existing.graphicsFormat == format)
        {
            return existing;
        }

        if (existing != null)
        {
            existing.Release();
            Destroy(existing);
        }

        var descriptor = new RenderTextureDescriptor(targetWidth, targetHeight)
        {
            depthBufferBits = 0,
            graphicsFormat = format,
            msaaSamples = 1,
            sRGB = false,
            useMipMap = binding.UseMipMaps,
            autoGenerateMips = binding.UseMipMaps
        };
        binding.TargetTexture = new RenderTexture(descriptor)
        {
            name = $"Aetheria {layer.LayerKey}",
            filterMode = binding.FilterMode,
            wrapMode = TextureWrapMode.Clamp
        };
        binding.TargetTexture.Create();
        return binding.TargetTexture;
    }

    private static GraphicsFormat ResolveGraphicsFormat(string bindingOverride, string layerFormat)
    {
        var value = string.IsNullOrWhiteSpace(bindingOverride) ? layerFormat : bindingOverride;
        return Enum.TryParse(value, out GraphicsFormat format)
            ? format
            : GraphicsFormat.R16_SFloat;
    }

    private static int ResolveMaterialPass(string blendMode)
    {
        switch (blendMode)
        {
            case AetheriaRuntimeRenderSplatBlendModes.Max:
                return 1;
            case AetheriaRuntimeRenderSplatBlendModes.Alpha:
                return 2;
            default:
                return 0;
        }
    }

    private static Color ResolveClearColor(AetheriaRuntimeRenderSplatLayerDefinition layer)
    {
        return new Color(
            (float)layer.ClearR,
            (float)layer.ClearG,
            (float)layer.ClearB,
            (float)layer.ClearA);
    }

    private static void ApplyBinding(AetheriaRenderSplatLayerBinding binding, RenderTexture texture)
    {
        if (binding.Display != null)
        {
            var material = binding.Display.material;
            if (material != null && !string.IsNullOrWhiteSpace(binding.MaterialTextureProperty))
                material.SetTexture(binding.MaterialTextureProperty, texture);
            else
                binding.Display.material.mainTexture = texture;
        }

        if (!string.IsNullOrWhiteSpace(binding.GlobalTextureName))
            Shader.SetGlobalTexture(binding.GlobalTextureName, texture);
    }

    private void OnDisable()
    {
        foreach (var binding in bindings ?? Array.Empty<AetheriaRenderSplatLayerBinding>())
        {
            if (binding?.TargetTexture == null)
                continue;

            binding.TargetTexture.Release();
            Destroy(binding.TargetTexture);
            binding.TargetTexture = null;
        }

        _texturesByLayerKey.Clear();
    }
}

[Serializable]
public sealed class AetheriaRenderSplatLayerBinding
{
    public bool Enabled = true;
    public string LayerKey = "";
    public Image Display;
    public string MaterialTextureProperty = "_DetailTex";
    public string GlobalTextureName = "";
    public string GraphicsFormatOverride = "";
    public float WidthScale = 1.0f;
    public float HeightScale = 1.0f;
    public bool UseMipMaps;
    public FilterMode FilterMode = FilterMode.Bilinear;
    [NonSerialized]
    public RenderTexture TargetTexture;
}
