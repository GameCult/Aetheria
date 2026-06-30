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
            Add(entries, MapIcon("entity.player", "Player", "Sprites/Map/player"));
            Add(entries, MapIcon("entity.ship", "Ship", "Sprites/Map/ship"));
            Add(entries, MapIcon("entity.orbital", "Orbital", "Sprites/Map/orbital"));
            Add(entries, MapIcon("entity.station", "Station", "Sprites/Map/station"));
            Add(entries, MapIcon("body.planet", "Planet", "Sprites/Map/planet"));
            Add(entries, MapIcon("body.sun", "Sun", "Sprites/Map/sun"));
            Add(entries, MapIcon("body.asteroid", "Asteroid", "Sprites/Map/asteroid"));
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
                return Sprite("map.entity.player", "Sprites/Map/player");

            var kind = (obj?.Kind ?? "").Trim().ToLowerInvariant();
            if (kind.IndexOf("station", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.station", "Sprites/Map/station");
            if (kind.IndexOf("orbital", StringComparison.Ordinal) >= 0)
                return Sprite("map.entity.orbital", "Sprites/Map/orbital");

            return Sprite("map.entity.ship", "Sprites/Map/ship");
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
            return AetheriaRuntimeAssetRef.FromKey(
                assetKey,
                AetheriaRuntimeAssetKinds.Texture,
                $"resources://{resourcePath}",
                AetheriaRuntimeAssetTransports.Resources,
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
            return AetheriaRuntimeAssetRef.FromKey(
                key,
                AetheriaRuntimeAssetKinds.Sprite,
                $"resources://{NormalizeResourcePath(resourcePath)}",
                AetheriaRuntimeAssetTransports.Resources,
                "image/*");
        }

        private static AetheriaRuntimeAssetRef Texture(string key, string resourcePath)
        {
            return AetheriaRuntimeAssetRef.FromKey(
                key,
                AetheriaRuntimeAssetKinds.Texture,
                $"resources://{NormalizeResourcePath(resourcePath)}",
                AetheriaRuntimeAssetTransports.Resources,
                "image/*");
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
