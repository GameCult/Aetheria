using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeAssets
    {
        public static AetheriaRuntimeAssetManifestDocument ProjectManifest(
            AetheriaRuntimeCatalogSnapshot? catalog,
            string runId = "",
            string baseUri = "cultmesh://aetheria.local/assets")
        {
            var entries = new Dictionary<string, AetheriaRuntimeAssetManifestEntry>(StringComparer.Ordinal);
            Add(entries, MapIcon("entity.player", "Player", "Sprites/Icons/Stroked/Ship"));
            Add(entries, MapIcon("entity.ship", "Ship", "Sprites/Icons/Stroked/Ship"));
            Add(entries, MapIcon("entity.orbital", "Orbital", "Sprites/Icons/Stroked/orbital"));
            Add(entries, MapIcon("entity.station", "Station", "Sprites/Icons/station1"));
            Add(entries, MapIcon("body.planet", "Planet", "Sprites/Icons/Stroked/Planet"));
            Add(entries, MapIcon("body.sun", "Sun", "Sprites/Icons/Stroked/Sun"));
            Add(entries, MapIcon("body.asteroid", "Asteroid", "Sprites/Icons/Stroked/Planet"));
            foreach (var inventoryAsset in InventoryUiAssets())
                Add(entries, inventoryAsset);

            foreach (var item in catalog?.Items ?? Array.Empty<AetheriaRuntimeCatalogItem>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemKey))
                    continue;

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

        public static AetheriaRuntimeAssetRef ResolveEntityIcon(AetheriaRuntimeRtsViewportObject? obj)
        {
            if (obj?.Controlled == true)
                return Sprite("map.entity.player", "Sprites/Icons/Stroked/Ship");

            var kind = (obj?.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.IndexOf("station", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.station", "Sprites/Icons/station1");
            if (kind.IndexOf("orbital", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.orbital", "Sprites/Icons/Stroked/orbital");

            return Sprite("map.entity.ship", "Sprites/Icons/Stroked/Ship");
        }

        public static AetheriaRuntimeAssetRef ResolveBodyIcon(AetheriaRuntimeRtsBodyView? body)
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
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["resourcePath"] = normalizedResourcePath,
                    ["sourceUri"] = $"resources://{normalizedResourcePath}",
                    ["sourceTransport"] = AetheriaRuntimeAssetTransports.Resources
                }
            };
        }

        private static string CultMeshAssetUri(string key)
        {
            var path = (key ?? "").Trim().Replace('.', '/').Replace('\\', '/').Trim('/');
            return $"cultmesh://aetheria/assets/{path}";
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
