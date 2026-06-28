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

    }
}
