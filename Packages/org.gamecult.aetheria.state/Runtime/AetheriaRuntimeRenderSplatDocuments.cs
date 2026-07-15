#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    // Aetheria owns how simulation state becomes splats. The persisted document,
    // layer, viewport, and SoA wire types are owned by GameCult.Eve.PluginFields.
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
}
