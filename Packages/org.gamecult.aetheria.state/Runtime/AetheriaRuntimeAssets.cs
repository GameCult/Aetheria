using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeAssets
    {
        public static string ResolveEntityPrefabAssetRef(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            if (entity == null) return "";
            var hull = catalog?.FindItem(entity.HullItemKey ?? "");
            if (hull != null && !string.IsNullOrWhiteSpace(hull.HullPrefab))
                return HullPrefabAssetKey(hull.ItemKey);
            var kind = (entity.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.Contains("station")) return "prefab.entity.station";
            if (kind.Contains("projectile")) return "prefab.entity.projectile";
            if (kind.Contains("orbital")) return "prefab.entity.orbital";
            return string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase)
                ? "prefab.entity.player"
                : "prefab.entity.ship";
        }

        public static string HullPrefabAssetKey(string hullItemKey) =>
            string.IsNullOrWhiteSpace(hullItemKey) ? "" : $"prefab.hull.{hullItemKey.Trim()}";
        public static AetheriaRuntimeAssetManifestDocument ProjectManifest(
            AetheriaRuntimeCatalogSnapshot? catalog,
            string runId = "",
            string baseUri = "cultmesh://aetheria.local/assets")
        {
            var entries = new Dictionary<string, AetheriaRuntimeAssetManifestEntry>(StringComparer.Ordinal);
            Add(entries, EnvironmentReflectionCubemap());
            Add(entries, EnvironmentPostProcessProfile());
            Add(entries, EnvironmentGravityFogShader());
            Add(entries, EnvironmentDitherTexture());
            Add(entries, EnvironmentStardustComputeShader());
            Add(entries, EnvironmentStardustMaterial());
            Add(entries, EnvironmentStardustColorTexture());
            Add(entries, MapIcon("entity.player", "Player", "Sprites/Icons/Stroked/Ship"));
            Add(entries, MapIcon("entity.ship", "Ship", "Sprites/Icons/Stroked/Ship"));
            Add(entries, MapIcon("entity.orbital", "Orbital", "Sprites/Icons/Stroked/orbital"));
            Add(entries, MapIcon("entity.station", "Station", "Sprites/Icons/station1"));
            Add(entries, MapIcon("entity.projectile", "Projectile", "Sprites/Icons/Lightning Bolt"));
            Add(entries, MapIcon("body.planet", "Planet", "Sprites/Icons/Stroked/Planet"));
            Add(entries, MapIcon("body.sun", "Sun", "Sprites/Icons/Stroked/Sun"));
            Add(entries, MapIcon("body.asteroid", "Asteroid", "Sprites/Icons/Stroked/Planet"));
            Add(entries, MapPrefab("prefab.entity.player", "Player Ship", "Prefabs/Ships/Djinni", "player"));
            Add(entries, MapPrefab("prefab.entity.ship", "Ship", "Prefabs/Ships/Djinni", "ship"));
            Add(entries, MapPrefab("prefab.entity.station", "Station", "Prefabs/Stations/AsteroidOutpost", "station"));
            Add(entries, MapPrefab("prefab.entity.orbital", "Orbital", "Prefabs/Stations/Zenith", "orbital"));
            Add(entries, MapPrefab("prefab.effect.shot.bolt", "Bolt", "Prefabs/Lightning", "effect.shot.bolt"));
            Add(entries, MapPrefab("prefab.effect.impact.shield", "Shield impact", "Prefabs/Shield", "effect.impact.shield"));
            Add(entries, MapPrefab("prefab.effect.beam.tractor", "Tractor beam", "Prefabs/Tractor Beam", "effect.beam.tractor"));
            Add(entries, DestructionEffect());
            Add(entries, MapProjectPrefab("prefab.entity.pickup", "Pickup", "Assets/Prefabs/RPG/Pickups/Tetrahedron.prefab"));
            Add(entries, MapCelestialPrefab(
                "prefab.body.planet", "Planet", "Assets/Prefabs/RPG/Planets/Planet.prefab", "Terrain Mesh", "celestial.planet"));
            Add(entries, MapCelestialPrefab(
                "prefab.body.gas-giant", "Gas Giant", "Assets/Prefabs/RPG/Planets/Gas Giant.prefab", "Sphere", "celestial.gas-giant"));
            Add(entries, MapCelestialPrefab(
                "prefab.body.sun", "Sun", "Assets/Prefabs/RPG/Planets/Sun.prefab", "Sphere", "celestial.sun"));
            Add(entries, MapCelestialPrefab(
                "prefab.body.asteroid", "Asteroid",
                "Assets/Plugins/LowPoly_AsteroidsPack/Prefabs/Asteroid_Huge_v01.prefab", "", "celestial.asteroid"));
            Add(entries, MinePrefab());
            foreach (var profile in ThermalPresentationProfiles()) Add(entries, profile);
            foreach (var inventoryAsset in InventoryUiAssets())
                Add(entries, inventoryAsset);

            foreach (var item in catalog?.Items ?? Array.Empty<AetheriaRuntimeCatalogItem>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemKey))
                    continue;

                if (!string.IsNullOrWhiteSpace(item.HullPrefab))
                {
                    Add(entries, MapPrefab(
                        HullPrefabAssetKey(item.ItemKey),
                        string.IsNullOrWhiteSpace(item.Name) ? item.ItemKey : item.Name,
                        item.HullPrefab,
                        "entity.hull"));
                }

                var icon = AssetRefFromCatalogIcon(item.ActionBarIcon, $"item.{item.ItemKey}.icon");
                if (!string.IsNullOrWhiteSpace(icon.AssetKey))
                {
                    Add(entries, new AetheriaRuntimeAssetManifestEntry
                    {
                        Ref = icon,
                        Tags = new[] { "item", item.ItemKey, item.Category ?? "" }
                            .Where(tag => !string.IsNullOrWhiteSpace(tag))
                            .ToArray()
                    });
                }
            }

            return new AetheriaRuntimeAssetManifestDocument
            {
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                RunId = runId ?? "",
                BaseUri = string.IsNullOrWhiteSpace(baseUri) ? "cultmesh://aetheria.local/assets" : baseUri,
                Assets = entries.Values
                    .OrderBy(entry => entry.Ref.AssetKey, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        public static AetheriaRuntimeAssetRef ResolveEntityIcon(AetheriaRuntimeViewportObject? obj)
        {
            if (obj?.Controlled == true)
                return Sprite("map.entity.player", "Sprites/Icons/Stroked/Ship");

            var kind = (obj?.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.IndexOf("station", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.station", "Sprites/Icons/station1");
            if (kind.IndexOf("orbital", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.orbital", "Sprites/Icons/Stroked/orbital");
            if (kind.IndexOf("projectile", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.projectile", "Sprites/Icons/Lightning Bolt");

            return Sprite("map.entity.ship", "Sprites/Icons/Stroked/Ship");
        }

        public static AetheriaRuntimeAssetRef ResolveBodyIcon(AetheriaRuntimeBodyView? body)
        {
            var kind = (body?.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.IndexOf("sun", StringComparison.Ordinal) >= 0 ||
                kind.IndexOf("star", StringComparison.Ordinal) >= 0)
                return Sprite("map.body.sun", "Sprites/Icons/Stroked/Sun");
            if (kind.IndexOf("asteroid", StringComparison.Ordinal) >= 0)
                return Sprite("map.body.asteroid", "Sprites/Icons/Stroked/Planet");

            return Sprite("map.body.planet", "Sprites/Icons/Stroked/Planet");
        }

        public static AetheriaRuntimeAssetRef AssetRefFromCatalogIcon(
            string? catalogPath,
            string fallbackAssetKey)
        {
            if (string.IsNullOrWhiteSpace(catalogPath))
                return AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Texture);

            var resourcePath = NormalizeResourcePath(catalogPath ?? "");
            var assetKey = string.IsNullOrWhiteSpace(fallbackAssetKey)
                ? $"catalog.{resourcePath}".Replace('/', '.').Replace('\\', '.')
                : fallbackAssetKey;
            return CultMeshAssetRef(
                assetKey,
                AetheriaRuntimeAssetKinds.Texture,
                resourcePath,
                "image/*");
        }

        public static AetheriaRuntimeAssetRef InventoryCellBackgroundAtlas()
        {
            return Texture(
                "inventory.cell.background_atlas",
                "Sprites/Flat UI/Nodes/Nodes8BG");
        }

        public static AetheriaRuntimeAssetRef InventoryCellForegroundAtlas()
        {
            return Texture(
                "inventory.cell.foreground_atlas",
                "Sprites/Flat UI/Nodes/Nodes8");
        }

        public static AetheriaRuntimeAssetRef InventoryThermalLayerAtlas()
        {
            return Texture(
                "inventory.cell.thermal_layer_atlas",
                "Sprites/Flat UI/pipes");
        }

        public static IReadOnlyList<AetheriaRuntimeAssetManifestEntry> InventoryUiAssets()
        {
            return new[]
            {
                new AetheriaRuntimeAssetManifestEntry
                {
                    Ref = InventoryCellBackgroundAtlas(),
                    Tags = new[] { "inventory", "cell", "background", "atlas" }
                },
                new AetheriaRuntimeAssetManifestEntry
                {
                    Ref = InventoryCellForegroundAtlas(),
                    Tags = new[] { "inventory", "cell", "foreground", "atlas" }
                },
                new AetheriaRuntimeAssetManifestEntry
                {
                    Ref = InventoryThermalLayerAtlas(),
                    Tags = new[] { "inventory", "thermal", "layer", "atlas" }
                }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentReflectionCubemap()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "texture.environment.reflection",
                    Kind = AetheriaRuntimeAssetKinds.Texture,
                    Uri = CultMeshAssetUri("texture.environment.reflection"),
                    MimeType = "image/vnd.radiance",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Textures/studio2.hdr",
                        ["presentationRole"] = "environment.reflection"
                    }
                },
                Tags = new[] { "presentation", "environment", "reflection", "pre-generated" }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentGravityFogShader()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "shader.environment.gravity-fog",
                    Kind = AetheriaRuntimeAssetKinds.Shader,
                    Uri = CultMeshAssetUri("shader.environment.gravity-fog"),
                    MimeType = "application/vnd.unity.shader",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Shaders/Raymarching/CloudShader.shader",
                        ["presentationRole"] = "environment.gravity-fog.volume",
                        ["unity.volume.pass.raymarch"] = "0",
                        ["unity.volume.pass.temporal"] = "1",
                        ["unity.volume.pass.composite"] = "2",
                        ["unity.volume.quality.bootstrap"] = "ultra",
                        ["unity.volume.quality.ultra.keyword"] = "ULTRA_QUALITY",
                        ["unity.volume.quality.high.keyword"] = "HIGH_QUALITY",
                        ["unity.volume.quality.normal.keyword"] = "MEDIUM_QUALITY",
                        ["unity.volume.feature.flow.global.keyword"] = "FLOW_GLOBAL",
                        ["unity.volume.feature.noise.slope.keyword"] = "NOISE_SLOPE",
                        ["unity.volume.texturePort.surfaceHeight"] = "_NebulaSurfaceHeight",
                        ["unity.volume.texturePort.patchHeight"] = "_NebulaPatchHeight",
                        ["unity.volume.texturePort.patch"] = "_NebulaPatch",
                        ["unity.volume.texturePort.tint"] = "_NebulaTint",
                        ["unity.volume.texturePort.dither"] = "_DitheringTex",
                        ["unity.volume.texturePort.currentSample"] = "_UndersampleCloudTex",
                        ["unity.volume.texturePort.history"] = "_MainTex",
                        ["unity.volume.texturePort.cloud"] = "_CloudTex",
                        ["unity.volume.vectorPort.viewportTransform"] = "_GridTransform",
                        ["unity.volume.matrixPort.cameraInverseViewProjection"] = "_CamInvProj",
                        ["unity.volume.matrixPort.cameraToWorld"] = "_CamToWorld",
                        ["unity.volume.matrixPort.previousViewProjection"] = "_PrevVP",
                        ["unity.volume.matrixSemantic.previousViewProjection"] = "current-projection.previous-view.v1",
                        ["unity.volume.vectorPort.cameraProjectionExtents"] = "_ProjectionExtents",
                        ["unity.volume.floatPort.raymarchOffset"] = "_RaymarchOffset",
                        ["unity.volume.floatPort.resetHistory"] = "_ResetHistory",
                        ["unity.volume.vectorPort.ditherCoordinates"] = "_DitheringCoords",
                        ["unity.volume.floatPort.fillDensity"] = "_NebulaFillDensity",
                        ["unity.volume.floatPort.fillDistance"] = "_NebulaFillDistance",
                        ["unity.volume.floatPort.fillExponent"] = "_NebulaFillExponent",
                        ["unity.volume.floatPort.fillOffset"] = "_NebulaFillOffset",
                        ["unity.volume.floatPort.patchDensity"] = "_NebulaPatchDensity",
                        ["unity.volume.floatPort.floorOffset"] = "_NebulaFloorOffset",
                        ["unity.volume.floatPort.floorBlend"] = "_NebulaFloorBlend",
                        ["unity.volume.floatPort.patchBlend"] = "_NebulaPatchBlend",
                        ["unity.volume.floatPort.luminance"] = "_NebulaLuminance",
                        ["unity.volume.floatPort.extinction"] = "_ExtinctionCoefficient",
                        ["unity.volume.floatPort.tintLodExponent"] = "_TintLodExponent",
                        ["unity.volume.floatPort.safetyDistance"] = "_SafetyDistance",
                        ["unity.volume.floatPort.flowScale"] = "_FlowScale",
                        ["unity.volume.floatPort.flowAmplitude"] = "_FlowAmplitude",
                        ["unity.volume.floatPort.flowScroll"] = "_FlowScroll",
                        ["unity.volume.floatPort.flowPeriod"] = "_FlowPeriod",
                        ["unity.volume.floatPort.flowSlopeAmplitude"] = "_FlowSlopeAmplitude",
                        ["unity.volume.floatPort.flowSwirlAmplitude"] = "_FlowSwirlAmplitude",
                        ["unity.volume.floatPort.noiseScale"] = "_NebulaNoiseScale",
                        ["unity.volume.floatPort.noiseAmplitude"] = "_NebulaNoiseAmplitude",
                        ["unity.volume.floatPort.noiseExponent"] = "_NebulaNoiseExponent",
                        ["unity.volume.floatPort.noiseSpeed"] = "_NebulaNoiseSpeed",
                        ["unity.volume.floatPort.noiseSlopeExponent"] = "_NebulaNoiseSlopeExponent",
                        ["unity.volume.floatPort.dynamicSkyBoost"] = "_DynamicSkyBoost",
                        ["unity.volume.floatPort.dynamicLodHigh"] = "_DynamicLodHigh",
                        ["unity.volume.floatPort.dynamicLodLow"] = "_DynamicLodLow",
                        ["unity.volume.floatPort.dynamicIntensity"] = "_DynamicIntensity",
                        ["unity.volume.floatPort.compositeOpacity"] = "_CompositeOpacity"
                    }
                },
                Tags = new[] { "presentation", "environment", "gravity", "fog", "volume" }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentPostProcessProfile()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "profile.environment.flight",
                    Kind = AetheriaRuntimeAssetKinds.VolumeProfile,
                    Uri = CultMeshAssetUri("profile.environment.flight"),
                    MimeType = "application/vnd.unity.volume-profile",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Generated/Eve/Environment/Flight.asset",
                        ["presentationRole"] = "environment.post-process.flight",
                        ["sourceProfile"] = "Assets/Scenes/ARPG_Profiles/Postprocessing Profile.asset",
                        ["profileSemantics"] = "aces;bloom;fixed-dark-scene-exposure;contrast;vignette;grain"
                    }
                },
                Tags = new[] { "presentation", "environment", "post-process", "flight" }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentStardustComputeShader()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "compute.environment.stardust",
                    Kind = AetheriaRuntimeAssetKinds.ComputeShader,
                    Uri = CultMeshAssetUri("compute.environment.stardust"),
                    MimeType = "application/vnd.unity.compute-shader",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Shaders/Compute/Stardust/Stardust.compute",
                        ["presentationRole"] = "environment.stardust.update",
                        ["unity.particles.kernel.update"] = "UpdateParticles",
                        ["unity.particles.feature.flow.global.keyword"] = "FLOW_GLOBAL",
                        ["unity.particles.feature.noise.slope.keyword"] = "NOISE_SLOPE",
                        ["unity.particles.texturePort.surfaceHeight"] = "_NebulaSurfaceHeight",
                        ["unity.particles.texturePort.tint"] = "_NebulaTint",
                        ["unity.particles.texturePort.hue"] = "HueTexture",
                        ["unity.particles.bufferPort.particles"] = "particles",
                        ["unity.particles.vectorPort.viewportTransform"] = "_GridTransform",
                        ["unity.particles.vectorPort.timeVector"] = "_Time",
                        ["unity.particles.floatPort.time"] = "time",
                        ["unity.particles.floatPort.period"] = "period",
                        ["unity.particles.floatPort.spacing"] = "spacing",
                        ["unity.particles.floatPort.ceilingHeight"] = "ceilingHeight",
                        ["unity.particles.floatPort.floorHeight"] = "floorHeight",
                        ["unity.particles.floatPort.maximumSize"] = "maximumSize",
                        ["unity.particles.floatPort.minimumSize"] = "minimumSize",
                        ["unity.particles.floatPort.minHeadroom"] = "minHeadroom",
                        ["unity.particles.floatPort.maxHeadroom"] = "maxHeadroom",
                        ["unity.particles.floatPort.heightExponent"] = "heightExponent",
                        ["unity.particles.intPort.span"] = "span",
                        ["unity.particles.floatPort.fillDensity"] = "_NebulaFillDensity",
                        ["unity.particles.floatPort.fillDistance"] = "_NebulaFillDistance",
                        ["unity.particles.floatPort.fillExponent"] = "_NebulaFillExponent",
                        ["unity.particles.floatPort.fillOffset"] = "_NebulaFillOffset",
                        ["unity.particles.floatPort.floorOffset"] = "_NebulaFloorOffset",
                        ["unity.particles.floatPort.floorBlend"] = "_NebulaFloorBlend",
                        ["unity.particles.floatPort.luminance"] = "_NebulaLuminance",
                        ["unity.particles.floatPort.tintLodExponent"] = "_TintLodExponent",
                        ["unity.particles.floatPort.flowScale"] = "_FlowScale",
                        ["unity.particles.floatPort.flowAmplitude"] = "_FlowAmplitude",
                        ["unity.particles.floatPort.flowScroll"] = "_FlowScroll",
                        ["unity.particles.floatPort.flowPeriod"] = "_FlowPeriod",
                        ["unity.particles.floatPort.flowSlopeAmplitude"] = "_FlowSlopeAmplitude",
                        ["unity.particles.floatPort.flowSwirlAmplitude"] = "_FlowSwirlAmplitude",
                        ["unity.particles.floatPort.noiseScale"] = "_NebulaNoiseScale",
                        ["unity.particles.floatPort.noiseAmplitude"] = "_NebulaNoiseAmplitude",
                        ["unity.particles.floatPort.noiseExponent"] = "_NebulaNoiseExponent",
                        ["unity.particles.floatPort.noiseSpeed"] = "_NebulaNoiseSpeed",
                        ["unity.particles.floatPort.noiseSlopeExponent"] = "_NebulaNoiseSlopeExponent",
                        ["unity.particles.floatPort.dynamicSkyBoost"] = "_DynamicSkyBoost",
                        ["unity.particles.floatPort.dynamicLodHigh"] = "_DynamicLodHigh",
                        ["unity.particles.floatPort.dynamicLodLow"] = "_DynamicLodLow",
                        ["unity.particles.floatPort.dynamicIntensity"] = "_DynamicIntensity"
                    }
                },
                Tags = new[] { "presentation", "environment", "stardust", "particles", "compute" }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentStardustMaterial()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "material.environment.stardust",
                    Kind = AetheriaRuntimeAssetKinds.Material,
                    Uri = CultMeshAssetUri("material.environment.stardust"),
                    MimeType = "application/vnd.unity.material",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Shaders/Compute/Stardust/Stardust.mat",
                        ["presentationRole"] = "environment.stardust.render",
                        ["unity.particles.pass.render"] = "0",
                        ["unity.particles.bufferPort.particles"] = "particles",
                        ["unity.particles.bufferPort.quadPoints"] = "quadPoints",
                        ["unity.particles.texturePort.dither"] = "_DitheringTex",
                        ["unity.particles.vectorPort.ditherCoordinates"] = "_DitheringCoords",
                        ["unity.particles.intPort.frameIndex"] = "_FrameNumber"
                    }
                },
                Tags = new[] { "presentation", "environment", "stardust", "particles", "material" }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentStardustColorTexture()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "texture.environment.stardust-colors",
                    Kind = AetheriaRuntimeAssetKinds.Texture,
                    Uri = CultMeshAssetUri("texture.environment.stardust-colors"),
                    MimeType = "image/png",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Resources/Gradients/blackbody.png",
                        ["presentationRole"] = "environment.stardust.colors"
                    }
                },
                Tags = new[] { "presentation", "environment", "stardust", "pre-generated" }
            };
        }

        public static AetheriaRuntimeAssetManifestEntry EnvironmentDitherTexture()
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = "texture.environment.volume-dither",
                    Kind = AetheriaRuntimeAssetKinds.Texture,
                    Uri = CultMeshAssetUri("texture.environment.volume-dither"),
                    MimeType = "image/png",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = "Assets/Resources/LDR_LLL1_0.png",
                        ["presentationRole"] = "environment.volume.dither"
                    }
                },
                Tags = new[] { "presentation", "environment", "volume", "pre-generated" }
            };
        }

        private static AetheriaRuntimeAssetManifestEntry MapIcon(
            string key,
            string label,
            string resourcePath)
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = Sprite($"map.{key}", resourcePath),
                Tags = new[] { "map", "icon", label }
            };
        }

        private static AetheriaRuntimeAssetManifestEntry MapPrefab(
            string key,
            string label,
            string resourcePath,
            string presentationRole)
        {
            var asset = Prefab(key, resourcePath);
            asset.Metadata = asset.Metadata
                .Concat(new[]
                {
                    new KeyValuePair<string, string>("presentationRole", presentationRole ?? ""),
                    new KeyValuePair<string, string>("bundleAssetPath", PresentationPrefabPath(key))
                })
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = asset,
                Tags = new[] { "world", "prefab", label }
            };
        }

        private static AetheriaRuntimeAssetManifestEntry MapProjectPrefab(
            string key,
            string label,
            string unityAssetPath)
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = key,
                    Kind = AetheriaRuntimeAssetKinds.Prefab,
                    Uri = CultMeshAssetUri(key),
                    MimeType = "application/vnd.unity.prefab",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = (unityAssetPath ?? "").Replace('\\', '/'),
                        ["bundleAssetPath"] = PresentationPrefabPath(key)
                    }
                },
                Tags = new[] { "world", "prefab", label }
            };
        }

        private static AetheriaRuntimeAssetManifestEntry MapCelestialPrefab(
            string key,
            string label,
            string unityAssetPath,
            string presentationVisualPath,
            string presentationRole)
        {
            var entry = MapProjectPrefab(key, label, unityAssetPath);
            var metadata = entry.Ref.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            metadata["presentationRole"] = presentationRole ?? "";
            if (!string.IsNullOrWhiteSpace(presentationVisualPath))
                metadata["presentationVisualPath"] = presentationVisualPath;
            entry.Ref.Metadata = metadata;
            entry.Tags = new[] { "world", "prefab", "celestial", label };
            return entry;
        }

        private static AetheriaRuntimeAssetManifestEntry MapProjectAsset(
            string key, string kind, string label, string unityAssetPath, string presentationRole)
        {
            return new AetheriaRuntimeAssetManifestEntry
            {
                Ref = new AetheriaRuntimeAssetRef
                {
                    AssetKey = key, Kind = kind ?? "", Uri = CultMeshAssetUri(key),
                    MimeType = "application/vnd.unity.asset",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unityAssetPath"] = (unityAssetPath ?? "").Replace('\\', '/'),
                        ["presentationRole"] = presentationRole ?? ""
                    }
                },
                Tags = new[] { "presentation", "thermal", label ?? "" }
            };
        }

        private static IReadOnlyList<AetheriaRuntimeAssetManifestEntry> ThermalPresentationProfiles() =>
            new[]
            {
                MapProjectAsset("profile.thermal.heatstroke", AetheriaRuntimeAssetKinds.VolumeProfile,
                    "Heatstroke", "Assets/Generated/Eve/Thermal/Heatstroke.asset",
                    "post.thermal.heatstroke"),
                MapProjectAsset("profile.thermal.severe-heatstroke", AetheriaRuntimeAssetKinds.VolumeProfile,
                    "Severe heatstroke", "Assets/Generated/Eve/Thermal/Severe Heatstroke.asset",
                    "post.thermal.severe-heatstroke"),
                MapProjectAsset("profile.thermal.hypothermia", AetheriaRuntimeAssetKinds.VolumeProfile,
                    "Hypothermia", "Assets/Generated/Eve/Thermal/Hypothermia.asset",
                    "post.thermal.hypothermia"),
                MapProjectAsset("profile.thermal.severe-hypothermia", AetheriaRuntimeAssetKinds.VolumeProfile,
                    "Severe hypothermia", "Assets/Generated/Eve/Thermal/Severe Hypothermia.asset",
                    "post.thermal.severe-hypothermia"),
                MapProjectAsset("profile.death", AetheriaRuntimeAssetKinds.VolumeProfile,
                    "Death", "Assets/Generated/Eve/Thermal/Death.asset", "post.death")
            };

        private static AetheriaRuntimeAssetManifestEntry MinePrefab()
        {
            var entry = MapProjectPrefab(
                "prefab.entity.mine",
                "Mine",
                "Assets/Prefabs/RPG/Effects/Mine.prefab");
            entry.Ref.Metadata = new Dictionary<string, string>(entry.Ref.Metadata, StringComparer.Ordinal)
            {
                ["presentationRole"] = "physical-payload.mine",
                ["activePulseSeconds"] = "1",
                ["triggeredPulseSeconds"] = "0.25",
                ["activeEmission"] = "100",
                ["triggeredEmission"] = "1000"
            };
            return entry;
        }

        private static AetheriaRuntimeAssetManifestEntry DestructionEffect()
        {
            var entry = MapProjectPrefab(
                "prefab.effect.entity.destroyed",
                "Entity destruction",
                "Assets/Prefabs/Fire & Explosion Effects/Prefabs/BigExplosion.prefab");
            entry.Ref.Metadata = new Dictionary<string, string>(entry.Ref.Metadata, StringComparer.Ordinal)
            {
                ["presentationRole"] = "effect.feedback.entity.destroyed"
            };
            return entry;
        }

        private static AetheriaRuntimeAssetRef Sprite(string key, string resourcePath)
        {
            return CultMeshAssetRef(
                key,
                AetheriaRuntimeAssetKinds.Sprite,
                resourcePath,
                "image/*");
        }

        private static AetheriaRuntimeAssetRef Texture(string key, string resourcePath)
        {
            return CultMeshAssetRef(
                key,
                AetheriaRuntimeAssetKinds.Texture,
                resourcePath,
                "image/*");
        }

        private static AetheriaRuntimeAssetRef Prefab(string key, string resourcePath)
        {
            return CultMeshAssetRef(
                key,
                AetheriaRuntimeAssetKinds.Prefab,
                resourcePath,
                "application/vnd.unity.prefab");
        }

        private static AetheriaRuntimeAssetRef CultMeshAssetRef(
            string key,
            string kind,
            string resourcePath,
            string mimeType)
        {
            var normalizedResourcePath = NormalizeResourcePath(resourcePath);
            return new AetheriaRuntimeAssetRef
            {
                AssetKey = key ?? "",
                Kind = kind ?? "",
                Uri = CultMeshAssetUri(key),
                Transport = AetheriaRuntimeAssetTransports.CultMesh,
                MimeType = mimeType ?? "",
                Metadata = string.IsNullOrWhiteSpace(normalizedResourcePath)
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["resourcesPath"] = normalizedResourcePath
                    }
            };
        }

        private static string CultMeshAssetUri(string key)
        {
            var path = (key ?? "").Trim().Replace('.', '/').Replace('\\', '/').Trim('/');
            return $"cultmesh://aetheria/assets/{path}";
        }

        private static string PresentationPrefabPath(string key)
        {
            var fileName = string.Concat((key ?? "").Select(character =>
                char.IsLetterOrDigit(character) || character == '.' || character == '-'
                    ? character
                    : '_'));
            return $"Assets/Generated/Eve/ProviderPrefabs/{fileName}.prefab";
        }

        private static string NormalizeResourcePath(string path)
        {
            path ??= "";
            const string resourcesPrefix = "Assets/Resources/";
            if (path.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(resourcesPrefix.Length);

            var extensionIndex = path.LastIndexOf('.');
            if (extensionIndex > 0)
                path = path.Substring(0, extensionIndex);

            return path.Replace('\\', '/').Trim('/');
        }

        private static void Add(
            Dictionary<string, AetheriaRuntimeAssetManifestEntry> entries,
            AetheriaRuntimeAssetManifestEntry entry)
        {
            var key = entry.Ref?.AssetKey ?? "";
            if (key.Length == 0)
                return;

            entries[key] = entry;
        }
    }
}
