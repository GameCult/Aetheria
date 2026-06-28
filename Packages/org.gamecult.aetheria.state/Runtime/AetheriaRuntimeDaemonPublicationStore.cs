using System;
using System.IO;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonPublicationStore
    {
        public static string GetProviderAdvertisementPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonProviderPath(stateFilePath);
        }

        public static string GetHealthPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonHealthPath(stateFilePath);
        }

        public static string GetVerseAuthorityPolicyPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetVerseAuthorityPolicyPath(stateFilePath);
        }

        public static string GetCommandBoundaryPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonCommandBoundaryPath(stateFilePath);
        }

        public static string GetAssetManifestPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonAssetManifestPath(stateFilePath);
        }

        public static string GetStarbridgeSessionSummaryPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonStarbridgeSessionSummaryPath(stateFilePath);
        }

        public static string GetGameSurfacePath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonGameSurfacePath(stateFilePath);
        }

        public static string GetGameTuiSurfacePath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonGameTuiSurfacePath(stateFilePath);
        }

        public static string GetEditorSurfacePath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonEditorSurfacePath(stateFilePath);
        }

        public static string GetEditorTuiSurfacePath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonEditorTuiSurfacePath(stateFilePath);
        }

        public static string PublishProviderAdvertisement(
            string stateFilePath,
            AetheriaRuntimeDaemonProviderAdvertisementDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeDaemonSchemas.ProviderAdvertisement;
            if (string.IsNullOrWhiteSpace(document.PublishedAtUtc))
                document.PublishedAtUtc = DateTime.UtcNow.ToString("O");

            var path = GetProviderAdvertisementPath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonProviderAdvertisement(path, document);
            return path;
        }

        public static string PublishHealth(string stateFilePath, AetheriaRuntimeDaemonHealthDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeDaemonSchemas.Health;
            if (string.IsNullOrWhiteSpace(document.PublishedAtUtc))
                document.PublishedAtUtc = DateTime.UtcNow.ToString("O");

            var path = GetHealthPath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonHealth(path, document);
            return path;
        }

        public static string PublishVerseAuthorityPolicy(
            string stateFilePath,
            AetheriaRuntimeVerseAuthorityPolicyDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeVerseAuthoritySchemas.Policy;
            if (string.IsNullOrWhiteSpace(document.UpdatedAtUtc))
                document.UpdatedAtUtc = DateTime.UtcNow.ToString("O");

            var path = GetVerseAuthorityPolicyPath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteVerseAuthorityPolicy(path, document);
            return path;
        }

        public static string PublishCommandBoundary(
            string stateFilePath,
            AetheriaRuntimeDaemonCommandBoundaryDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeDaemonSchemas.CommandBoundary;
            if (string.IsNullOrWhiteSpace(document.PublishedAtUtc))
                document.PublishedAtUtc = DateTime.UtcNow.ToString("O");

            var path = GetCommandBoundaryPath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonCommandBoundary(path, document);
            return path;
        }

        public static string PublishAssetManifest(
            string stateFilePath,
            AetheriaRuntimeAssetManifestDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeDaemonSchemas.AssetManifest;
            if (string.IsNullOrWhiteSpace(document.PublishedAtUtc))
                document.PublishedAtUtc = DateTime.UtcNow.ToString("O");

            var path = GetAssetManifestPath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteAssetManifest(path, document);
            return path;
        }

        public static string PublishStarbridgeSessionSummary(
            string stateFilePath,
            AetheriaRuntimeStarbridgeSessionSummaryDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary;
            if (string.IsNullOrWhiteSpace(document.PublishedAtUtc))
                document.PublishedAtUtc = DateTime.UtcNow.ToString("O");

            var path = GetStarbridgeSessionSummaryPath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteStarbridgeSessionSummary(path, document);
            return path;
        }

        public static string PublishGameSurface(string stateFilePath, AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var path = GetGameSurfacePath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonGameSurface(path, document);
            return path;
        }

        public static string PublishGameTuiSurface(string stateFilePath, AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var path = GetGameTuiSurfacePath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonGameSurface(path, document);
            return path;
        }

        public static string PublishEditorSurface(string stateFilePath, AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var path = GetEditorSurfacePath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonEditorSurface(path, document);
            return path;
        }

        public static string PublishEditorTuiSurface(string stateFilePath, AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var path = GetEditorTuiSurfacePath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonEditorSurface(path, document);
            return path;
        }

        public static bool TryReadProviderAdvertisement(
            string stateFilePath,
            out AetheriaRuntimeDaemonProviderAdvertisementDocument document)
        {
            var path = GetProviderAdvertisementPath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeDaemonProviderAdvertisementDocument();
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonProviderAdvertisement(path);
            return true;
        }

        public static bool TryReadHealth(string stateFilePath, out AetheriaRuntimeDaemonHealthDocument document)
        {
            var path = GetHealthPath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeDaemonHealthDocument();
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonHealth(path);
            return true;
        }

        public static bool TryReadVerseAuthorityPolicy(
            string stateFilePath,
            out AetheriaRuntimeVerseAuthorityPolicyDocument document)
        {
            var path = GetVerseAuthorityPolicyPath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeVerseAuthorityPolicyDocument();
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadVerseAuthorityPolicy(path);
            return true;
        }

        public static bool TryReadCommandBoundary(
            string stateFilePath,
            out AetheriaRuntimeDaemonCommandBoundaryDocument document)
        {
            var path = GetCommandBoundaryPath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeDaemonCommandBoundaryDocument();
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonCommandBoundary(path);
            return true;
        }

        public static bool TryReadAssetManifest(
            string stateFilePath,
            out AetheriaRuntimeAssetManifestDocument document)
        {
            var path = GetAssetManifestPath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeAssetManifestDocument();
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadAssetManifest(path);
            return true;
        }

        public static bool TryReadStarbridgeSessionSummary(
            string stateFilePath,
            out AetheriaRuntimeStarbridgeSessionSummaryDocument document)
        {
            var path = GetStarbridgeSessionSummaryPath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeStarbridgeSessionSummaryDocument();
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadStarbridgeSessionSummary(path);
            return true;
        }

        public static bool TryReadGameSurface(string stateFilePath, out AetheriaRuntimeSurfaceDocument document)
        {
            var path = GetGameSurfacePath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeSurfaceDocument(
                    "aetheria.daemon",
                    "game.daemon",
                    "",
                    0,
                    "",
                    new AetheriaRuntimeSurfaceTree(
                        AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
                        new AetheriaRuntimeSurfaceComponent(
                            "aetheria.daemon.game.empty",
                            "surface",
                            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                            Array.Empty<AetheriaRuntimeSurfaceComponent>()),
                        Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                    Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>());
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonGameSurface(path);
            return true;
        }

        public static bool TryReadGameTuiSurface(string stateFilePath, out AetheriaRuntimeSurfaceDocument document)
        {
            var path = GetGameTuiSurfacePath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeSurfaceDocument(
                    "aetheria.daemon",
                    "game.daemon",
                    "Aetheria Daemon",
                    0,
                    "",
                    new AetheriaRuntimeSurfaceTree(
                        AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId,
                        new AetheriaRuntimeSurfaceComponent(
                            "aetheria.daemon.game.tui.missing",
                            "surface",
                            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                            Array.Empty<AetheriaRuntimeSurfaceComponent>()),
                        Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                    Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>());
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonGameSurface(path);
            return true;
        }

        public static bool TryReadEditorSurface(string stateFilePath, out AetheriaRuntimeSurfaceDocument document)
        {
            var path = GetEditorSurfacePath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeSurfaceDocument(
                    "aetheria.daemon",
                    "editor.daemon",
                    "",
                    0,
                    "",
                    new AetheriaRuntimeSurfaceTree(
                        AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId,
                        new AetheriaRuntimeSurfaceComponent(
                            "aetheria.daemon.editor.empty",
                            "surface",
                            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                            Array.Empty<AetheriaRuntimeSurfaceComponent>()),
                        Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                    Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>());
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonEditorSurface(path);
            return true;
        }

        public static bool TryReadEditorTuiSurface(string stateFilePath, out AetheriaRuntimeSurfaceDocument document)
        {
            var path = GetEditorTuiSurfacePath(stateFilePath);
            if (!File.Exists(path))
            {
                document = new AetheriaRuntimeSurfaceDocument(
                    "aetheria.daemon",
                    "editor.daemon",
                    "Aetheria Daemon Editor",
                    0,
                    "",
                    new AetheriaRuntimeSurfaceTree(
                        AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId,
                        new AetheriaRuntimeSurfaceComponent(
                            "aetheria.daemon.editor.tui.missing",
                            "surface",
                            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                            Array.Empty<AetheriaRuntimeSurfaceComponent>()),
                        Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                    Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>());
                return false;
            }

            document = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonEditorSurface(path);
            return true;
        }
    }
}
