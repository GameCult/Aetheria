using System;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public sealed class AetheriaSceneRenderSplatSource : MonoBehaviour
{
    [SerializeField]
    private string documentName = "Scene Mock";

    [SerializeField]
    private AetheriaSceneRenderSplat[] splats =
    {
        AetheriaSceneRenderSplat.Animated(
            AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight,
            "scene.fog_surface",
            Vector2.zero,
            new Vector2(512, 512),
            Color.white,
            4,
            4,
            0.015f),
        AetheriaSceneRenderSplat.Animated(
            AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight,
            "scene.fog_patch_height",
            Vector2.zero,
            new Vector2(512, 512),
            Color.white,
            9,
            9,
            0.02f),
        AetheriaSceneRenderSplat.Animated(
            AetheriaRuntimeRenderSplatLayerKeys.FogPatch,
            "scene.fog_patch",
            Vector2.zero,
            new Vector2(512, 512),
            Color.white,
            6,
            6,
            0.01f)
    };

    [SerializeField]
    private AetheriaSceneRenderSplatFixture[] fixtures = Array.Empty<AetheriaSceneRenderSplatFixture>();

    public AetheriaRuntimeRenderSplatsViewportDocument BuildDocument(AetheriaRuntimeRtsViewportBounds viewport)
    {
        viewport ??= new AetheriaRuntimeRtsViewportBounds();
        var normalizedViewport = Normalize(viewport);
        var layers = BuildLayers();
        var layerIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < layers.Length; index++)
            layerIndices[layers[index].LayerKey] = index;

        var builder = new Builder();
        foreach (var splat in splats ?? Array.Empty<AetheriaSceneRenderSplat>())
            splat.AddTo(builder, layerIndices);
        foreach (var fixture in fixtures ?? Array.Empty<AetheriaSceneRenderSplatFixture>())
            fixture.AddTo(builder, layerIndices);

        return new AetheriaRuntimeRenderSplatsViewportDocument
        {
            PublishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            SimulationTimeSeconds = Application.isPlaying ? Time.timeAsDouble : 0,
            RunId = "scene",
            ZoneName = string.IsNullOrWhiteSpace(documentName) ? gameObject.scene.name : documentName,
            Viewport = normalizedViewport,
            Layers = layers,
            Splats = builder.Build()
        };
    }

    private static AetheriaRuntimeRtsViewportBounds Normalize(AetheriaRuntimeRtsViewportBounds viewport)
    {
        return new AetheriaRuntimeRtsViewportBounds
        {
            MinX = Math.Min(viewport.MinX, viewport.MaxX),
            MinY = Math.Min(viewport.MinY, viewport.MaxY),
            MaxX = Math.Max(viewport.MinX, viewport.MaxX),
            MaxY = Math.Max(viewport.MinY, viewport.MaxY)
        };
    }

    private static AetheriaRuntimeRenderSplatLayerDefinition[] BuildLayers()
    {
        return new[]
        {
            Layer(AetheriaRuntimeRenderSplatLayerKeys.GravityHeight, "Gravity Height", AetheriaRuntimeRenderSplatChannels.Gravity),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.GravityWave, "Gravity Wave", AetheriaRuntimeRenderSplatChannels.GravityWave),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.Visibility, "Visibility Mask", AetheriaRuntimeRenderSplatChannels.Visibility, AetheriaRuntimeRenderSplatBlendModes.Max),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight, "Fog Surface Height", AetheriaRuntimeRenderSplatChannels.Tint),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight, "Fog Patch Height", AetheriaRuntimeRenderSplatChannels.Tint),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.FogPatch, "Fog Patch", AetheriaRuntimeRenderSplatChannels.Tint, AetheriaRuntimeRenderSplatBlendModes.Max),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.FogTint, "Fog Tint", AetheriaRuntimeRenderSplatChannels.Tint, graphicsFormat: "B10G11R11_UFloatPack32"),
            Layer(AetheriaRuntimeRenderSplatLayerKeys.Influence, "Influence", AetheriaRuntimeRenderSplatChannels.Influence)
        };
    }

    private static AetheriaRuntimeRenderSplatLayerDefinition Layer(
        string key,
        string displayName,
        int channel,
        string blendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
        string graphicsFormat = "R16_SFloat")
    {
        return new AetheriaRuntimeRenderSplatLayerDefinition
        {
            LayerKey = key,
            DisplayName = displayName,
            Channel = channel,
            BlendMode = blendMode,
            GraphicsFormat = graphicsFormat
        };
    }

    public sealed class Builder
    {
        private readonly List<double> _centerX = new List<double>();
        private readonly List<double> _centerY = new List<double>();
        private readonly List<double> _halfExtentX = new List<double>();
        private readonly List<double> _halfExtentY = new List<double>();
        private readonly List<double> _rotationCos = new List<double>();
        private readonly List<double> _rotationSin = new List<double>();
        private readonly List<int> _channel = new List<int>();
        private readonly List<int> _falloff = new List<int>();
        private readonly List<double> _valueR = new List<double>();
        private readonly List<double> _valueG = new List<double>();
        private readonly List<double> _valueB = new List<double>();
        private readonly List<double> _valueA = new List<double>();
        private readonly List<string> _sourceKey = new List<string>();
        private readonly List<int> _layerIndex = new List<int>();
        private readonly List<int> _sourceKind = new List<int>();
        private readonly List<double> _frequencyX = new List<double>();
        private readonly List<double> _frequencyY = new List<double>();
        private readonly List<double> _phaseX = new List<double>();
        private readonly List<double> _phaseY = new List<double>();
        private readonly List<double> _animationSpeed = new List<double>();
        private readonly List<double> _sourceFlags = new List<double>();

        public void Add(AetheriaSceneRenderSplat splat, int layerIndex)
        {
            var rotationRadians = splat.RotationDegrees * Mathf.Deg2Rad;
            _layerIndex.Add(layerIndex);
            _centerX.Add(splat.Center.x);
            _centerY.Add(splat.Center.y);
            _halfExtentX.Add(Math.Max(0, splat.HalfExtent.x));
            _halfExtentY.Add(Math.Max(0, splat.HalfExtent.y));
            _rotationCos.Add(Math.Cos(rotationRadians));
            _rotationSin.Add(Math.Sin(rotationRadians));
            _channel.Add(splat.Channel);
            _falloff.Add(splat.Falloff);
            _valueR.Add(splat.Value.r);
            _valueG.Add(splat.Value.g);
            _valueB.Add(splat.Value.b);
            _valueA.Add(splat.Value.a);
            _sourceKey.Add(splat.SourceKey ?? "");
            _sourceKind.Add(splat.SourceKind);
            _frequencyX.Add(splat.Frequency.x);
            _frequencyY.Add(splat.Frequency.y);
            _phaseX.Add(splat.Phase.x);
            _phaseY.Add(splat.Phase.y);
            _animationSpeed.Add(splat.AnimationSpeed);
            _sourceFlags.Add(splat.SourceFlags);
        }

        public AetheriaRuntimeRenderSplatSoa Build()
        {
            return new AetheriaRuntimeRenderSplatSoa
            {
                Count = _centerX.Count,
                CenterX = _centerX.ToArray(),
                CenterY = _centerY.ToArray(),
                HalfExtentX = _halfExtentX.ToArray(),
                HalfExtentY = _halfExtentY.ToArray(),
                RotationCos = _rotationCos.ToArray(),
                RotationSin = _rotationSin.ToArray(),
                Channel = _channel.ToArray(),
                Falloff = _falloff.ToArray(),
                ValueR = _valueR.ToArray(),
                ValueG = _valueG.ToArray(),
                ValueB = _valueB.ToArray(),
                ValueA = _valueA.ToArray(),
                SourceKey = _sourceKey.ToArray(),
                LayerIndex = _layerIndex.ToArray(),
                SourceKind = _sourceKind.ToArray(),
                FrequencyX = _frequencyX.ToArray(),
                FrequencyY = _frequencyY.ToArray(),
                PhaseX = _phaseX.ToArray(),
                PhaseY = _phaseY.ToArray(),
                AnimationSpeed = _animationSpeed.ToArray(),
                SourceFlags = _sourceFlags.ToArray()
            };
        }
    }
}

[Serializable]
public sealed class AetheriaSceneRenderSplat
{
    public string LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogTint;
    public string SourceKey = "";
    public Vector2 Center;
    public Vector2 HalfExtent = Vector2.one * 32;
    public float RotationDegrees;
    public int Channel = AetheriaRuntimeRenderSplatChannels.Tint;
    public int Falloff = AetheriaRuntimeRenderSplatFalloffs.Smooth;
    public Color Value = Color.white;
    public int SourceKind = AetheriaRuntimeRenderSplatSourceKinds.Constant;
    public Vector2 Frequency = Vector2.one;
    public Vector2 Phase;
    public float AnimationSpeed;
    public float SourceFlags;

    public static AetheriaSceneRenderSplat Animated(
        string layerKey,
        string sourceKey,
        Vector2 center,
        Vector2 halfExtent,
        Color value,
        float frequencyX,
        float frequencyY,
        float animationSpeed)
    {
        return new AetheriaSceneRenderSplat
        {
            LayerKey = layerKey,
            SourceKey = sourceKey,
            Center = center,
            HalfExtent = halfExtent,
            Channel = AetheriaRuntimeRenderSplatChannels.Tint,
            Falloff = AetheriaRuntimeRenderSplatFalloffs.Solid,
            Value = value,
            SourceKind = AetheriaRuntimeRenderSplatSourceKinds.AnimatedSimplexNoise,
            Frequency = new Vector2(frequencyX, frequencyY),
            AnimationSpeed = animationSpeed,
            SourceFlags = 1
        };
    }

    public void AddTo(AetheriaSceneRenderSplatSource.Builder builder, IReadOnlyDictionary<string, int> layerIndices)
    {
        if (builder == null || !layerIndices.TryGetValue(LayerKey ?? "", out var layerIndex))
            return;

        builder.Add(this, layerIndex);
    }
}

[Serializable]
public sealed class AetheriaSceneRenderSplatFixture
{
    public Transform Transform;
    public AetheriaSceneRenderSplat Splat = new AetheriaSceneRenderSplat();

    public void AddTo(AetheriaSceneRenderSplatSource.Builder builder, IReadOnlyDictionary<string, int> layerIndices)
    {
        if (Transform == null || Splat == null)
            return;

        var previousCenter = Splat.Center;
        Splat.Center = new Vector2(Transform.position.x, Transform.position.z);
        try
        {
            Splat.AddTo(builder, layerIndices);
        }
        finally
        {
            Splat.Center = previousCenter;
        }
    }
}
