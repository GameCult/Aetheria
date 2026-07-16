using System.IO;
using UnityEditor;
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
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Aetheria Flight";
            AssetDatabase.CreateAsset(profile, FlightProfilePath);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            // The fossil histogram exposure clamps average luminance to 2^-3 and
            // uses a 0.5 middle-grey key. At that dark-scene bound the resulting
            // exposure is 4x, which is the fixed URP equivalent available here.
            color.postExposure.Override(2f);
            color.contrast.Override(15f);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(3f);
            bloom.threshold.Override(1.5f);
            bloom.scatter.Override(1f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.4f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.1f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
