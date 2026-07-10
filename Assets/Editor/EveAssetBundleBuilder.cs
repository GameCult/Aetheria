using System;
using System.IO;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using UnityEditor;

namespace Aetheria.Editor
{
    public static class EveAssetBundleBuilder
    {
        public const string BundleName = "aetheria-world";

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64);
        }

        private static void Build(BuildTarget target)
        {
            var output = Environment.GetEnvironmentVariable("AETHERIA_EVE_BUNDLE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine("Build", "EveAssets", target.ToString()));
            Directory.CreateDirectory(output);

            var assetNames = AetheriaRuntimeAssets.ProjectManifest(null).Assets
                .Where(entry => string.Equals(entry.Ref.Kind, AetheriaRuntimeAssetKinds.Prefab, StringComparison.Ordinal))
                .Select(entry => entry.Ref.Metadata.TryGetValue("resourcesPath", out var path)
                    ? $"Assets/Resources/{path}.prefab"
                    : "")
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (assetNames.Length == 0)
                throw new InvalidOperationException("Aetheria Eve bundle has no provider prefab assets.");

            var manifest = BuildPipeline.BuildAssetBundles(
                output,
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = BundleName,
                        assetNames = assetNames
                    }
                },
                BuildAssetBundleOptions.None,
                target);
            if (manifest == null || !manifest.GetAllAssetBundles().Contains(BundleName, StringComparer.Ordinal))
                throw new InvalidOperationException("Unity did not emit the Aetheria Eve world bundle.");

            Console.WriteLine($"Aetheria Eve AssetBundle: {Path.Combine(output, BundleName)}");
        }
    }
}
