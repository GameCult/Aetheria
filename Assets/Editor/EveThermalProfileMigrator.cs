using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Editor
{
    public static class EveThermalProfileMigrator
    {
        public const string Root = "Assets/Generated/Eve/Thermal";

        public static void EnsureGenerated()
        {
            Directory.CreateDirectory(Root);
            Create("Heatstroke", new Color(0.41509432f, 0.072190315f, 0), 0.66f,
                0.666f, 51, 2, 49, 30, 8, 0.5f, false);
            Create("Severe Heatstroke", new Color(0.41509432f, 0.072190315f, 0), 1,
                0.666f, 0, -3, 0, -36, 8, 1, true);
            Create("Hypothermia", new Color(0, 0.11663109f, 0.41568628f), 0.66f,
                0.666f, 51, 2, 49, 30, 8, 0.5f, false);
            Create("Severe Hypothermia", new Color(0, 0.24793181f, 0.33962262f), 1,
                0.666f, 0, -3, 0, -36, 8, 1, true);
            Create("Death", Color.black, 0.3f, 0.4f, 0, 1, 0, 0, 3, 0.25f, true,
                overrideVignetteColor: false, focalLength: 3);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Create(string name, Color vignetteColor, float vignetteIntensity,
            float vignetteSmoothness, float temperature, float exposure,
            float contrast, float saturation, float bloomIntensity, float grainIntensity, bool depthOfField,
            bool overrideVignetteColor = true, float focalLength = 39)
        {
            var path = $"{Root}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var vignette = profile.Add<Vignette>(true);
            vignette.color.overrideState = overrideVignetteColor;
            vignette.color.value = vignetteColor;
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(exposure);
            color.contrast.overrideState = contrast != 0;
            color.contrast.value = contrast;
            color.saturation.Override(saturation);
            if (temperature != 0)
            {
                var balance = profile.Add<WhiteBalance>(true);
                balance.temperature.Override(temperature);
            }

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(1.5f);
            bloom.scatter.Override(1f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(grainIntensity);

            if (depthOfField)
            {
                var focus = profile.Add<DepthOfField>(true);
                focus.mode.Override(DepthOfFieldMode.Bokeh);
                focus.focusDistance.Override(0.1f);
                focus.aperture.Override(0.1f);
                focus.focalLength.Override(focalLength);
            }

            EditorUtility.SetDirty(profile);
        }
    }
}
