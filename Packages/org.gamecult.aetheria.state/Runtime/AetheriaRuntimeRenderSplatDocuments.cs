using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeRenderSplatChannels
    {
        public const int Visibility = 0;
        public const int Gravity = 1;
        public const int GravityWave = 2;
        public const int Influence = 3;
        public const int Tint = 4;
    }

    public static class AetheriaRuntimeRenderSplatLayerKeys
    {
        public const string GravityHeight = "gravity.height";
        public const string GravityWave = "gravity.wave";
        public const string Visibility = "visibility.mask";
        public const string FogSurfaceHeight = "fog.surface_height";
        public const string FogPatchHeight = "fog.patch_height";
        public const string FogPatch = "fog.patch";
        public const string FogTint = "fog.tint";
        public const string Influence = "influence.mask";
    }

    public static class AetheriaRuntimeRenderSplatBlendModes
    {
        public const string Add = "add";
        public const string Max = "max";
        public const string Alpha = "alpha";
    }

    public static class AetheriaRuntimeRenderSplatSourceKinds
    {
        public const int Constant = 0;
        public const int SimplexNoise = 1;
        public const int AnimatedSimplexNoise = 2;
    }

    public static class AetheriaRuntimeRenderSplatFalloffs
    {
        public const int Solid = 0;
        public const int Linear = 1;
        public const int Smooth = 2;
        public const int InverseSmooth = 3;
    }

    [CultDocument("gamecult.aetheria.render_splats_viewport", "gamecult.aetheria.render_splats_viewport.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeRenderSplatsViewportDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.RenderSplatsViewport;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; }

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public AetheriaRuntimeRtsViewportBounds Viewport { get; set; } = new AetheriaRuntimeRtsViewportBounds();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeRenderSplatLayerDefinition> Layers { get; set; } =
            Array.Empty<AetheriaRuntimeRenderSplatLayerDefinition>();

        [Key(9)]
        public AetheriaRuntimeRenderSplatSoa Splats { get; set; } = new AetheriaRuntimeRenderSplatSoa();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRenderSplatLayerDefinition
    {
        [Key(0)]
        public string LayerKey { get; set; } = "";

        [Key(1)]
        public string DisplayName { get; set; } = "";

        [Key(2)]
        public int Channel { get; set; }

        [Key(3)]
        public string BlendMode { get; set; } = AetheriaRuntimeRenderSplatBlendModes.Add;

        [Key(4)]
        public string GraphicsFormat { get; set; } = "R16_SFloat";

        [Key(5)]
        public bool ClearBeforeDraw { get; set; } = true;

        [Key(6)]
        public double ClearR { get; set; }

        [Key(7)]
        public double ClearG { get; set; }

        [Key(8)]
        public double ClearB { get; set; }

        [Key(9)]
        public double ClearA { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRenderSplatSoa
    {
        [Key(0)]
        public int Count { get; set; }

        [Key(1)]
        public IReadOnlyList<double> CenterX { get; set; } = Array.Empty<double>();

        [Key(2)]
        public IReadOnlyList<double> CenterY { get; set; } = Array.Empty<double>();

        [Key(3)]
        public IReadOnlyList<double> HalfExtentX { get; set; } = Array.Empty<double>();

        [Key(4)]
        public IReadOnlyList<double> HalfExtentY { get; set; } = Array.Empty<double>();

        [Key(5)]
        public IReadOnlyList<double> RotationCos { get; set; } = Array.Empty<double>();

        [Key(6)]
        public IReadOnlyList<double> RotationSin { get; set; } = Array.Empty<double>();

        [Key(7)]
        public IReadOnlyList<int> Channel { get; set; } = Array.Empty<int>();

        [Key(8)]
        public IReadOnlyList<int> Falloff { get; set; } = Array.Empty<int>();

        [Key(9)]
        public IReadOnlyList<double> ValueR { get; set; } = Array.Empty<double>();

        [Key(10)]
        public IReadOnlyList<double> ValueG { get; set; } = Array.Empty<double>();

        [Key(11)]
        public IReadOnlyList<double> ValueB { get; set; } = Array.Empty<double>();

        [Key(12)]
        public IReadOnlyList<double> ValueA { get; set; } = Array.Empty<double>();

        [Key(13)]
        public IReadOnlyList<string> SourceKey { get; set; } = Array.Empty<string>();

        [Key(14)]
        public IReadOnlyList<int> LayerIndex { get; set; } = Array.Empty<int>();

        [Key(15)]
        public IReadOnlyList<int> SourceKind { get; set; } = Array.Empty<int>();

        [Key(16)]
        public IReadOnlyList<double> FrequencyX { get; set; } = Array.Empty<double>();

        [Key(17)]
        public IReadOnlyList<double> FrequencyY { get; set; } = Array.Empty<double>();

        [Key(18)]
        public IReadOnlyList<double> PhaseX { get; set; } = Array.Empty<double>();

        [Key(19)]
        public IReadOnlyList<double> PhaseY { get; set; } = Array.Empty<double>();

        [Key(20)]
        public IReadOnlyList<double> AnimationSpeed { get; set; } = Array.Empty<double>();

        [Key(21)]
        public IReadOnlyList<double> SourceFlags { get; set; } = Array.Empty<double>();
    }
}
