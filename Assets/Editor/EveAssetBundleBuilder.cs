using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Editor
{
    public static class EveAssetBundleBuilder
    {
        public const string BundleName = "aetheria-world";
        private const string ProviderMaterialsRoot = "Assets/Generated/Eve/ProviderMaterials";

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
            EveEnvironmentProfileMigrator.EnsureGenerated();
            var output = ResolveOutput(target);
            Directory.CreateDirectory(output);

            var catalog = AetheriaRuntimeCatalogStore.OpenReadOnly(
                Path.GetFullPath(Path.Combine("GameData", "aetheria-world.cc")));
            var assets = AetheriaRuntimeAssets.ProjectManifest(catalog).Assets;
            foreach (var entry in assets.Where(entry =>
                         string.Equals(entry.Ref.Kind, AetheriaRuntimeAssetKinds.Prefab, StringComparison.Ordinal)))
                BuildPresentationPrefab(entry);
            AssetDatabase.SaveAssets();

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
            var authoredStardustMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Shaders/Compute/Stardust/Stardust.mat");
            AetheriaStardustContinuityVerifier.VerifyTemporalDitherMaterial(
                authoredStardustMaterial,
                requireAuthoredSource: true);
            var authoredGravityFogShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Shaders/Raymarching/CloudShader.shader");
            AetheriaGravityFogVerifier.VerifyCameraProjection(
                authoredGravityFogShader,
                requireAuthoredSource: true);

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
                KeepAdvertisedPresentationVisual(root, entry);
                ExtractExternalizedEffects(root, entry);
                StripNonPresentationScripts(root);
                StripPresentationPhysics(root);
                NormalizePresentationMaterials(root);
                VerifyExternalizedShipEffects(root, entry);
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

        private static void KeepAdvertisedPresentationVisual(
            GameObject root,
            AetheriaRuntimeAssetManifestEntry entry)
        {
            if (!entry.Ref.Metadata.TryGetValue("presentationVisualPath", out var visualPath) ||
                string.IsNullOrWhiteSpace(visualPath))
                return;

            var visual = root.transform.Find(visualPath);
            if (visual == null)
                throw new InvalidOperationException(
                    $"Prefab {entry.Ref.AssetKey} has no advertised presentation visual '{visualPath}'.");

            foreach (var child in root.transform.Cast<Transform>().Where(child => child != visual).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject, true);
        }

        private static void ExtractExternalizedEffects(
            GameObject root,
            AetheriaRuntimeAssetManifestEntry entry)
        {
            var assetKey = entry?.Ref?.AssetKey ?? "";
            var presentationRole = entry?.Ref?.Metadata != null &&
                entry.Ref.Metadata.TryGetValue("presentationRole", out var role)
                    ? role ?? ""
                    : "";
            var strictLegacyShip =
                string.Equals(presentationRole, "player", StringComparison.Ordinal) ||
                string.Equals(presentationRole, "ship", StringComparison.Ordinal);
            var catalogHull = string.Equals(presentationRole, "entity.hull", StringComparison.Ordinal);
            if (!strictLegacyShip && !catalogHull)
                return;

            var tractorEffects = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(behaviour => behaviour != null &&
                    string.Equals(behaviour.GetType().Name, "TractorBeam", StringComparison.Ordinal))
                .Select(behaviour => behaviour.gameObject)
                .Distinct()
                .ToArray();
            if (tractorEffects.Length == 0 && strictLegacyShip)
                throw new InvalidOperationException(
                    $"Ship presentation source '{assetKey}' has no embedded tractor effect to externalize.");

            foreach (var effect in tractorEffects)
                UnityEngine.Object.DestroyImmediate(effect, true);

            var embeddedShieldVisuals = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMaterials.Any(material =>
                    material != null && string.Equals(
                        material.shader?.name,
                        "Aetheria/Shield",
                        StringComparison.Ordinal)))
                .Select(renderer => renderer.gameObject)
                .Distinct()
                .ToArray();
            if (embeddedShieldVisuals.Length == 0 && strictLegacyShip)
                throw new InvalidOperationException(
                    $"Ship presentation source '{assetKey}' has no embedded shield visual to externalize.");

            foreach (var visual in embeddedShieldVisuals)
                UnityEngine.Object.DestroyImmediate(visual, true);
        }

        private static void VerifyExternalizedShipEffects(
            GameObject root,
            AetheriaRuntimeAssetManifestEntry entry)
        {
            var presentationRole = entry?.Ref?.Metadata != null &&
                entry.Ref.Metadata.TryGetValue("presentationRole", out var role)
                    ? role ?? ""
                    : "";
            if (!string.Equals(presentationRole, "player", StringComparison.Ordinal) &&
                !string.Equals(presentationRole, "ship", StringComparison.Ordinal) &&
                !string.Equals(presentationRole, "entity.hull", StringComparison.Ordinal))
                return;

            var surviving = root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer != null &&
                    string.Equals(renderer.gameObject.name, "Shield Visual", StringComparison.Ordinal));
            if (surviving != null)
                throw new InvalidOperationException(
                    $"Ship presentation source '{entry.Ref.AssetKey}' retained an always-on shield envelope.");
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

        private static void StripPresentationPhysics(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider, true);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body, true);
        }

        private static void NormalizePresentationMaterials(GameObject root)
        {
            Directory.CreateDirectory(ProviderMaterialsRoot);
            var generated = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    var source = materials[index];
                    var particle = renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer;
                    if (source == null)
                    {
                        var defaultKey = particle ? "default-particle" : "default-lit";
                        if (!generated.TryGetValue(defaultKey, out var defaultMaterial))
                        {
                            defaultMaterial = CreateDefaultPresentationMaterial(particle, defaultKey);
                            generated.Add(defaultKey, defaultMaterial);
                        }
                        materials[index] = defaultMaterial;
                        continue;
                    }
                    if (IsUniversalMaterial(source))
                        continue;

                    var unlit = particle || IsUnlitMaterial(source);
                    var key = PresentationMaterialKey(source, particle, unlit);
                    if (!generated.TryGetValue(key, out var replacement))
                    {
                        replacement = CreatePresentationMaterial(source, particle, unlit, key);
                        generated.Add(key, replacement);
                    }
                    materials[index] = replacement;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material CreateDefaultPresentationMaterial(bool particle, string key)
        {
            var shaderName = particle
                ? "Universal Render Pipeline/Particles/Unlit"
                : "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException($"Aetheria provider presentation shader is unavailable: {shaderName}");

            var assetPath = $"{ProviderMaterialsRoot}/{key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                var clean = new Material(shader);
                EditorUtility.CopySerialized(clean, material);
                UnityEngine.Object.DestroyImmediate(clean);
            }
            material.name = "Eve URP Default";
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", particle ? 1f : 0f);
            if (particle)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreatePresentationMaterial(Material source, bool particle, bool unlit, string key)
        {
            var shaderName = particle
                ? "Universal Render Pipeline/Particles/Unlit"
                : unlit
                    ? "Universal Render Pipeline/Unlit"
                    : "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException($"Aetheria provider presentation shader is unavailable: {shaderName}");

            var assetPath = $"{ProviderMaterialsRoot}/{key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                var clean = new Material(shader);
                EditorUtility.CopySerialized(clean, material);
                UnityEngine.Object.DestroyImmediate(clean);
            }

            material.name = source.name + " (Eve URP)";
            CopyTexture(source, material, "_BaseMap", "_BaseMap", "_MainTex");
            CopyColorOrWhite(source, material, "_BaseColor", "_BaseColor", "_Color", "_TintColor");
            CopyFloat(source, material, "_Metallic", "_Metallic");
            CopyFloat(source, material, "_Smoothness", "_Smoothness", "_Glossiness");
            CopyTexture(source, material, "_MetallicGlossMap", "_MetallicGlossMap");
            CopyTexture(source, material, "_BumpMap", "_BumpMap");
            CopyFloat(source, material, "_BumpScale", "_BumpScale");
            CopyTexture(source, material, "_OcclusionMap", "_OcclusionMap");
            CopyFloat(source, material, "_OcclusionStrength", "_OcclusionStrength");
            CopyTexture(source, material, "_EmissionMap", "_EmissionMap");
            // The fossil hull shader's _EdgeColor is a view-dependent Fresnel tint, not
            // whole-surface emission. Promoting its HDR edge values to URP emission
            // poisons exposure and makes the converted hull appear black.
            var emission = FirstNonBlackColor(source, "_EmissionColor");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0.001f ||
                (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null))
                material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)
                material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") != null)
                material.EnableKeyword("_METALLICSPECGLOSSMAP");

            ConfigureSurface(source, material, particle);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSurface(Material source, Material target, bool particle)
        {
            var transparent = particle || source.renderQueue >= (int)RenderQueue.Transparent ||
                (source.HasProperty("_Mode") && source.GetFloat("_Mode") > 0.5f) ||
                string.Equals(source.GetTag("RenderType", false, ""), "Transparent", StringComparison.OrdinalIgnoreCase);
            if (!target.HasProperty("_Surface"))
                return;

            var shaderName = source.shader?.name ?? "";
            var sourceBlend = source.HasProperty("_SrcBlend") ? (BlendMode)source.GetFloat("_SrcBlend") : BlendMode.SrcAlpha;
            var destinationBlend = source.HasProperty("_DstBlend") ? (BlendMode)source.GetFloat("_DstBlend") : BlendMode.OneMinusSrcAlpha;
            var hasSavedMode = TryGetSavedFloat(source, "_Mode", out var savedMode);
            var additive = shaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (hasSavedMode && savedMode >= 3.5f) ||
                (transparent && destinationBlend == BlendMode.One);
            var premultiplied = !additive &&
                (shaderName.IndexOf("Premultiply", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (transparent && sourceBlend == BlendMode.One && destinationBlend == BlendMode.OneMinusSrcAlpha));
            target.SetFloat("_Surface", transparent ? 1f : 0f);
            if (target.HasProperty("_Blend"))
                target.SetFloat("_Blend", additive ? 2f : premultiplied ? 1f : 0f);
            target.SetFloat("_SrcBlend", transparent
                ? (float)(premultiplied ? BlendMode.One : BlendMode.SrcAlpha)
                : (float)BlendMode.One);
            target.SetFloat("_DstBlend", transparent
                ? (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha)
                : (float)BlendMode.Zero);
            target.SetFloat("_ZWrite", transparent ? 0f : 1f);
            target.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            if (transparent)
                target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else
                target.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private static bool TryGetSavedFloat(Material material, string propertyName, out float value)
        {
            var serialized = new SerializedObject(material);
            var floats = serialized.FindProperty("m_SavedProperties.m_Floats");
            if (floats != null)
            {
                for (var index = 0; index < floats.arraySize; index++)
                {
                    var entry = floats.GetArrayElementAtIndex(index);
                    if (!string.Equals(entry.FindPropertyRelative("first")?.stringValue, propertyName, StringComparison.Ordinal))
                        continue;
                    value = entry.FindPropertyRelative("second").floatValue;
                    return true;
                }
            }
            value = 0f;
            return false;
        }

        private static bool IsUniversalMaterial(Material material) =>
            material.shader != null && material.shader.isSupported &&
            (material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
             string.Equals(material.GetTag("RenderPipeline", true, ""), "UniversalPipeline", StringComparison.Ordinal));

        private static bool IsUnlitMaterial(Material material)
        {
            var shaderName = material.shader?.name ?? "";
            return shaderName.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   shaderName.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PresentationMaterialKey(Material material, bool particle, bool unlit)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long localId))
                throw new InvalidOperationException($"Provider material has no stable asset identity: {material.name}");
            return $"{guid}-{localId}-{(particle ? "particle" : unlit ? "unlit" : "lit")}";
        }

        private static void CopyTexture(Material source, Material target, string targetProperty, params string[] sourceProperties)
        {
            if (!target.HasProperty(targetProperty)) return;
            foreach (var sourceProperty in sourceProperties)
            {
                if (!TryGetSourceTexture(source, sourceProperty, out var texture, out var scale, out var offset)) continue;
                target.SetTexture(targetProperty, texture);
                target.SetTextureScale(targetProperty, scale);
                target.SetTextureOffset(targetProperty, offset);
                return;
            }
        }

        private static void CopyColor(Material source, Material target, string targetProperty, params string[] sourceProperties)
        {
            if (!target.HasProperty(targetProperty)) return;
            foreach (var sourceProperty in sourceProperties)
            {
                if (!TryGetSourceColor(source, sourceProperty, out var color)) continue;
                target.SetColor(targetProperty, color);
                return;
            }
        }

        private static void CopyColorOrWhite(Material source, Material target, string targetProperty, params string[] sourceProperties)
        {
            if (!target.HasProperty(targetProperty)) return;
            foreach (var sourceProperty in sourceProperties)
            {
                if (!TryGetSourceColor(source, sourceProperty, out var color)) continue;
                target.SetColor(targetProperty, color);
                return;
            }
            target.SetColor(targetProperty, Color.white);
        }

        private static Color FirstNonBlackColor(Material source, params string[] sourceProperties)
        {
            foreach (var sourceProperty in sourceProperties)
            {
                if (!TryGetSourceColor(source, sourceProperty, out var color)) continue;
                if (color.maxColorComponent > 0.001f) return color;
            }
            return Color.black;
        }

        private static void CopyFloat(Material source, Material target, string targetProperty, params string[] sourceProperties)
        {
            if (!target.HasProperty(targetProperty)) return;
            foreach (var sourceProperty in sourceProperties)
            {
                if (!TryGetSourceFloat(source, sourceProperty, out var value)) continue;
                target.SetFloat(targetProperty, value);
                return;
            }
        }

        private static bool TryGetSourceFloat(Material material, string propertyName, out float value)
        {
            if (material.HasProperty(propertyName))
            {
                value = material.GetFloat(propertyName);
                return true;
            }
            return TryGetSavedFloat(material, propertyName, out value);
        }

        private static bool TryGetSourceColor(Material material, string propertyName, out Color value)
        {
            if (material.HasProperty(propertyName))
            {
                value = material.GetColor(propertyName);
                return true;
            }

            var serialized = new SerializedObject(material);
            var colors = serialized.FindProperty("m_SavedProperties.m_Colors");
            if (colors != null)
            {
                for (var index = 0; index < colors.arraySize; index++)
                {
                    var entry = colors.GetArrayElementAtIndex(index);
                    if (!string.Equals(entry.FindPropertyRelative("first")?.stringValue, propertyName, StringComparison.Ordinal))
                        continue;
                    value = entry.FindPropertyRelative("second").colorValue;
                    return true;
                }
            }
            value = Color.black;
            return false;
        }

        private static bool TryGetSourceTexture(
            Material material,
            string propertyName,
            out Texture texture,
            out Vector2 scale,
            out Vector2 offset)
        {
            if (material.HasProperty(propertyName))
            {
                texture = material.GetTexture(propertyName);
                scale = material.GetTextureScale(propertyName);
                offset = material.GetTextureOffset(propertyName);
                if (texture != null)
                    return true;
            }

            var serialized = new SerializedObject(material);
            var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
            if (textures != null)
            {
                for (var index = 0; index < textures.arraySize; index++)
                {
                    var entry = textures.GetArrayElementAtIndex(index);
                    if (!string.Equals(entry.FindPropertyRelative("first")?.stringValue, propertyName, StringComparison.Ordinal))
                        continue;
                    var value = entry.FindPropertyRelative("second");
                    texture = value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
                    scale = value.FindPropertyRelative("m_Scale").vector2Value;
                    offset = value.FindPropertyRelative("m_Offset").vector2Value;
                    return texture != null;
                }
            }
            texture = null;
            scale = Vector2.one;
            offset = Vector2.zero;
            return false;
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
                foreach (var material in transform.GetComponents<Renderer>()
                             .SelectMany(renderer => renderer.sharedMaterials))
                {
                    if (material == null)
                        violations.Add($"{transform.name}: null material");
                    else if (!IsUniversalMaterial(material))
                        violations.Add($"{transform.name}: unsupported presentation shader {material.shader?.name ?? "<null>"}");
                }
                if (transform.GetComponent<Collider>() != null || transform.GetComponent<Rigidbody>() != null)
                    violations.Add($"{transform.name}: gameplay physics component");
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
                var assetNames = bundle.GetAllAssetNames();
                foreach (var assetPath in assetNames.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                    VerifyPrefab(bundle.LoadAsset<GameObject>(assetPath), assetPath);
                var reflection = LoadAuthoredAsset<Cubemap>(bundle, assetNames, "Assets/Textures/studio2.hdr");
                if (reflection == null)
                    throw new InvalidOperationException("Aetheria Eve bundle has no provider reflection cubemap.");
                var postProcess = LoadAuthoredAsset<VolumeProfile>(bundle, assetNames, EveEnvironmentProfileMigrator.FlightProfilePath);
                VerifyFlightPostProcessProfile(postProcess);
                var gravityFog = LoadAuthoredAsset<Shader>(bundle, assetNames, "Assets/Shaders/Raymarching/CloudShader.shader");
                if (gravityFog == null || !gravityFog.isSupported)
                    throw new InvalidOperationException("Aetheria Eve bundle has no supported gravity-fog volume shader.");
                AetheriaGravityFogVerifier.VerifyCameraProjection(
                    gravityFog,
                    requireAuthoredSource: false);
                var dither = LoadAuthoredAsset<Texture2D>(bundle, assetNames, "Assets/Resources/LDR_LLL1_0.png");
                if (dither == null)
                    throw new InvalidOperationException("Aetheria Eve bundle has no pre-generated volume dither texture.");
                var stardustCompute = LoadAuthoredAsset<ComputeShader>(bundle, assetNames,
                    "Assets/Shaders/Compute/Stardust/Stardust.compute");
                if (stardustCompute == null || !stardustCompute.HasKernel("UpdateParticles"))
                    throw new InvalidOperationException("Aetheria Eve bundle has no supported Stardust update program.");
                var stardustMaterial = LoadAuthoredAsset<Material>(bundle, assetNames,
                    "Assets/Shaders/Compute/Stardust/Stardust.mat");
                if (stardustMaterial == null || stardustMaterial.shader == null || !stardustMaterial.shader.isSupported)
                    throw new InvalidOperationException("Aetheria Eve bundle has no supported Stardust render material.");
                AetheriaStardustContinuityVerifier.VerifyTemporalDitherMaterial(
                    stardustMaterial,
                    requireAuthoredSource: false);
                var stardustColors = LoadAuthoredAsset<Texture2D>(bundle, assetNames,
                    "Assets/Resources/Gradients/blackbody.png");
                if (stardustColors == null)
                    throw new InvalidOperationException("Aetheria Eve bundle has no pre-generated Stardust color texture.");
                AetheriaStardustContinuityVerifier.VerifyOneCellShift(stardustCompute);
            }
            finally
            {
                bundle.Unload(true);
            }
        }

        private static T LoadAuthoredAsset<T>(AssetBundle bundle, IEnumerable<string> assetNames, string authoredPath)
            where T : UnityEngine.Object
        {
            var nativePath = assetNames.FirstOrDefault(path =>
                string.Equals(path, authoredPath, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(nativePath) ? null : bundle.LoadAsset<T>(nativePath);
        }

        private static void VerifyFlightPostProcessProfile(VolumeProfile profile)
        {
            if (profile == null)
                throw new InvalidOperationException("Aetheria Eve bundle has no provider flight post-process profile.");
            if (!profile.TryGet(out Tonemapping tonemapping) ||
                !tonemapping.mode.overrideState ||
                tonemapping.mode.value != TonemappingMode.ACES)
                throw new InvalidOperationException("Aetheria flight profile has no serialized ACES tonemapping component.");
            if (!profile.TryGet(out ColorAdjustments color) ||
                !color.postExposure.overrideState ||
                !Mathf.Approximately(color.postExposure.value, 0f) ||
                !color.contrast.overrideState ||
                !Mathf.Approximately(color.contrast.value, 15f))
                throw new InvalidOperationException("Aetheria flight profile has no neutral exposure/contrast component.");
            if (!profile.TryGet(out Bloom bloom) ||
                !bloom.intensity.overrideState ||
                !Mathf.Approximately(bloom.intensity.value, 3f) ||
                !bloom.threshold.overrideState ||
                !Mathf.Approximately(bloom.threshold.value, 1.5f))
                throw new InvalidOperationException("Aetheria flight profile has no serialized bloom component.");
            if (!profile.TryGet(out Vignette vignette) ||
                !vignette.intensity.overrideState ||
                !Mathf.Approximately(vignette.intensity.value, 0.3f))
                throw new InvalidOperationException("Aetheria flight profile has no serialized vignette component.");
            if (!profile.TryGet(out FilmGrain grain) ||
                !grain.intensity.overrideState ||
                !Mathf.Approximately(grain.intensity.value, 0.1f))
                throw new InvalidOperationException("Aetheria flight profile has no serialized film-grain component.");
        }
    }
}
