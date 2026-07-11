using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.PluginFields;
using GameCult.Eve.UnityScene.Fields;
using UnityEngine;
using UnityEngine.UI;

public sealed class AetheriaRenderSplatLayerRenderer : MonoBehaviour
{
    [SerializeField] private EveFieldsSplatLayerRenderer renderer;
    [SerializeField] private AetheriaRenderSplatLayerBinding[] bindings =
    {
        Binding(EveFieldsSplatLayerKeys.GravityHeight, "_AetheriaGravityHeight"),
        Binding(EveFieldsSplatLayerKeys.FogTint, "_NebulaTint"),
        Binding(EveFieldsSplatLayerKeys.FogSurfaceHeight, "_NebulaSurfaceHeight", 0.5f),
        Binding(EveFieldsSplatLayerKeys.FogPatchHeight, "_NebulaPatchHeight", 0.5f),
        Binding(EveFieldsSplatLayerKeys.FogPatch, "_NebulaPatch", 0.5f),
        Binding(EveFieldsSplatLayerKeys.Influence, "_AetheriaInfluence")
    };

    public bool TryGetTexture(string layerKey, out RenderTexture texture)
    {
        ResolveRenderer();
        if (renderer != null) return renderer.TryGetTexture(layerKey, out texture);
        texture = null;
        return false;
    }

    public void Render(AetheriaRuntimeRenderSplatsViewportDocument document, int width, int height)
    {
        if (document == null) return;
        ResolveRenderer();
        if (renderer == null) return;
        var targets = new EveFieldsSplatLayerTarget[bindings?.Length ?? 0];
        for (var index = 0; index < targets.Length; index++) targets[index] = bindings[index]?.CreateTarget();
        renderer.Render(document, targets, width, height);
        for (var index = 0; index < targets.Length; index++)
        {
            var binding = bindings[index];
            var target = targets[index];
            if (binding == null || target?.TargetTexture == null) continue;
            binding.TargetTexture = target.TargetTexture;
            ApplyBinding(binding, target.TargetTexture);
        }
    }

    private void ResolveRenderer()
    {
        if (renderer != null) return;
        renderer = GetComponent<EveFieldsSplatLayerRenderer>();
        if (renderer == null) renderer = gameObject.AddComponent<EveFieldsSplatLayerRenderer>();
    }

    private static AetheriaRenderSplatLayerBinding Binding(string layerKey, string globalTextureName, float scale = 1f) =>
        new AetheriaRenderSplatLayerBinding { LayerKey = layerKey, GlobalTextureName = globalTextureName, WidthScale = scale, HeightScale = scale };

    private static void ApplyBinding(AetheriaRenderSplatLayerBinding binding, RenderTexture texture)
    {
        if (binding.Display != null)
        {
            var material = binding.Display.material;
            if (material != null && !string.IsNullOrWhiteSpace(binding.MaterialTextureProperty)) material.SetTexture(binding.MaterialTextureProperty, texture);
            else binding.Display.material.mainTexture = texture;
        }
        if (!string.IsNullOrWhiteSpace(binding.GlobalTextureName)) Shader.SetGlobalTexture(binding.GlobalTextureName, texture);
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
    public float WidthScale = 1f;
    public float HeightScale = 1f;
    public bool UseMipMaps;
    public FilterMode FilterMode = FilterMode.Bilinear;
    [NonSerialized] public RenderTexture TargetTexture;

    public EveFieldsSplatLayerTarget CreateTarget() => new EveFieldsSplatLayerTarget
    {
        Enabled = Enabled,
        LayerKey = LayerKey,
        GraphicsFormatOverride = GraphicsFormatOverride,
        WidthScale = WidthScale,
        HeightScale = HeightScale,
        UseMipMaps = UseMipMaps,
        FilterMode = FilterMode,
        TargetTexture = TargetTexture
    };
}
