using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Editor
{
    public static class EveEnvironmentProfileMigrator
    {
        public const string Root = "Assets/Generated/Eve/Environment";
        public const string FlightProfilePath = Root + "/Flight.asset";

        public static void EnsureGenerated()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.DeleteAsset(FlightProfilePath);
            var profile = VolumeProfileFactory.CreateVolumeProfileAtPath(FlightProfilePath);
            profile.name = "Aetheria Flight";

            var tonemapping = VolumeProfileFactory.CreateVolumeComponent<Tonemapping>(profile, true, false);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var color = VolumeProfileFactory.CreateVolumeComponent<ColorAdjustments>(profile, true, false);
            // Histogram exposure is lowered separately from the generic Eve
            // camera contract. The static profile must not become a second owner.
            color.postExposure.Override(0f);
            color.contrast.Override(15f);

            var bloom = VolumeProfileFactory.CreateVolumeComponent<Bloom>(profile, true, false);
            bloom.intensity.Override(3f);
            bloom.threshold.Override(1.5f);
            bloom.scatter.Override(1f);

            var vignette = VolumeProfileFactory.CreateVolumeComponent<Vignette>(profile, true, false);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.4f);

            var grain = VolumeProfileFactory.CreateVolumeComponent<FilmGrain>(profile, true, false);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.1f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
