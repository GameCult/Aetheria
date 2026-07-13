using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using UnityEditor;
using UnityEngine;

namespace Aetheria.Editor
{
    public static class EveAssetBundleBuilder
    {
        public const string BundleName = "aetheria-world";

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64);
        }

        public static void VerifyWindows()
        {
            var output = ResolveOutput(BuildTarget.StandaloneWindows64);
            VerifyBundle(Path.Combine(output, BundleName));
        }

        private static void Build(BuildTarget target)
        {
            EveThermalProfileMigrator.EnsureGenerated();
            var output = ResolveOutput(target);
            Directory.CreateDirectory(output);

            var assets = AetheriaRuntimeAssets.ProjectManifest(null).Assets;
            foreach (var entry in assets.Where(entry =>
                         string.Equals(entry.Ref.Kind, AetheriaRuntimeAssetKinds.Prefab, StringComparison.Ordinal)))
                BuildPresentationPrefab(entry);

            var assetNames = assets
                .Select(entry => entry.Ref.Metadata.TryGetValue("bundleAssetPath", out var presentationPath)
                    ? presentationPath
                    : entry.Ref.Metadata.TryGetValue("unityAssetPath", out var explicitPath)
                        ? explicitPath
                        : "")
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (assetNames.Length == 0)
                throw new InvalidOperationException("Aetheria Eve bundle has no provider Unity assets.");

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

            VerifyBundle(Path.Combine(output, BundleName));
            Console.WriteLine($"Aetheria Eve AssetBundle: {Path.Combine(output, BundleName)}");
        }

        private static string ResolveOutput(BuildTarget target)
        {
            var output = Environment.GetEnvironmentVariable("AETHERIA_EVE_BUNDLE_OUTPUT");
            return string.IsNullOrWhiteSpace(output)
                ? Path.GetFullPath(Path.Combine("Build", "EveAssets", target.ToString()))
                : output;
        }

        private static void BuildPresentationPrefab(AetheriaRuntimeAssetManifestEntry entry)
        {
            if (!entry.Ref.Metadata.TryGetValue("bundleAssetPath", out var outputPath))
                throw new InvalidOperationException($"Prefab {entry.Ref.AssetKey} has no presentation bundle path.");

            var sourcePath = entry.Ref.Metadata.TryGetValue("unityAssetPath", out var explicitPath)
                ? explicitPath
                : entry.Ref.Metadata.TryGetValue("resourcesPath", out var resourcesPath)
                    ? $"Assets/Resources/{resourcesPath}.prefab"
                    : "";
            if (string.IsNullOrWhiteSpace(sourcePath) || AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
                throw new InvalidOperationException($"Prefab {entry.Ref.AssetKey} source does not exist: {sourcePath}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Assets/Generated/Eve/ProviderPrefabs");
            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                StripNonPresentationScripts(root);
                VerifyPrefab(root, outputPath);
                PrefabUtility.SaveAsPrefabAsset(root, outputPath, out var saved);
                if (!saved)
                    throw new InvalidOperationException($"Could not save presentation prefab {outputPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var savedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            VerifyPrefab(savedRoot, outputPath);
        }

        private static void StripNonPresentationScripts(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                foreach (var behaviour in transform.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && !IsPresentationAssembly(behaviour.GetType().Assembly.GetName().Name))
                        UnityEngine.Object.DestroyImmediate(behaviour, true);
                }
            }
        }

        private static bool IsPresentationAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("Unity", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("GameCult.Eve", StringComparison.Ordinal);
        }

        private static void VerifyPrefab(GameObject root, string assetPath)
        {
            if (root == null)
                throw new InvalidOperationException($"Presentation prefab could not be loaded: {assetPath}");

            var violations = new List<string>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing > 0)
                    violations.Add($"{transform.name}: {missing} missing script(s)");
                foreach (var behaviour in transform.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && !IsPresentationAssembly(behaviour.GetType().Assembly.GetName().Name))
                        violations.Add($"{transform.name}: {behaviour.GetType().FullName}");
                }
            }
            if (violations.Count > 0)
                throw new InvalidOperationException(
                    $"Presentation prefab {assetPath} contains forbidden scripts:\n{string.Join("\n", violations)}");
        }

        private static void VerifyBundle(string bundlePath)
        {
            if (!File.Exists(bundlePath))
                throw new InvalidOperationException($"Aetheria Eve bundle does not exist: {bundlePath}");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
                throw new InvalidOperationException($"Aetheria Eve bundle could not be loaded: {bundlePath}");
            try
            {
                foreach (var assetPath in bundle.GetAllAssetNames().Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                    VerifyPrefab(bundle.LoadAsset<GameObject>(assetPath), assetPath);
            }
            finally
            {
                bundle.Unload(true);
            }
        }
    }
}
