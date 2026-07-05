using System.IO;
using System.Linq;

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeStateBoundary
    {
        public const string RuntimeStateFileName = "aetheria-world.cc";
        public const string RuntimeClientTargetFileName = "aetheria-client.cc";
        public const string RuntimeReplicaDirectoryName = "Verses";
        public const string RuntimeDaemonFrameFileSuffix = ".daemon.frame.cc";
        public const string RuntimeDaemonSoaViewFileSuffix = ".daemon.soa.cc";
        public const string RuntimeDaemonAssetManifestFileSuffix = ".daemon.assets.cc";
        public const string RuntimeDaemonProviderFileSuffix = ".daemon.provider.cc";
        public const string RuntimeDaemonHealthFileSuffix = ".daemon.health.cc";
        public const string RuntimeVerseAuthorityPolicyFileSuffix = ".authority.policy.cc";
        public const string RuntimeDaemonCommandBoundaryFileSuffix = ".daemon.commands.cc";
        public const string RuntimeDaemonStarbridgeSessionSummaryFileSuffix = ".daemon.starbridge.session.cc";
        public const string RuntimeDaemonGameSurfaceFileSuffix = ".daemon.game.eve.cc";
        public const string RuntimeDaemonGameTuiSurfaceFileSuffix = ".daemon.game.tui.cc";
        public const string RuntimeDaemonEditorSurfaceFileSuffix = ".daemon.editor.eve.cc";
        public const string RuntimeDaemonEditorTuiSurfaceFileSuffix = ".daemon.editor.tui.cc";
        public const string RuntimeStatePathOverrideEnvironmentVariable = "AETHERIA_STATE_PATH";
        public const string RuntimeIdOverrideEnvironmentVariable = "AETHERIA_RUNTIME_ID";
        public const string UnityRuntimeIdOverrideEnvironmentVariable = "AETHERIA_UNITY_RUNTIME_ID";

        public static string GetStateFilePath(DirectoryInfo gameDataDirectory)
        {
            return Path.Combine(gameDataDirectory.FullName, RuntimeStateFileName);
        }

        public static string GetClientTargetPath(DirectoryInfo gameDataDirectory)
        {
            return Path.Combine(gameDataDirectory.FullName, RuntimeClientTargetFileName);
        }

        public static string GetDaemonFramePath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonFrameFileSuffix;
        }

        public static string GetDaemonSoaViewPath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonSoaViewFileSuffix;
        }

        public static string GetDaemonAssetManifestPath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonAssetManifestFileSuffix;
        }

        public static string GetDaemonProviderPath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonProviderFileSuffix;
        }

        public static string GetDaemonHealthPath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonHealthFileSuffix;
        }

        public static string GetVerseAuthorityPolicyPath(string stateFilePath)
        {
            return stateFilePath + RuntimeVerseAuthorityPolicyFileSuffix;
        }

        public static string GetDaemonCommandBoundaryPath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonCommandBoundaryFileSuffix;
        }

        public static string GetDaemonStarbridgeSessionSummaryPath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonStarbridgeSessionSummaryFileSuffix;
        }

        public static string GetDaemonGameSurfacePath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonGameSurfaceFileSuffix;
        }

        public static string GetDaemonGameTuiSurfacePath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonGameTuiSurfaceFileSuffix;
        }

        public static string GetDaemonEditorSurfacePath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonEditorSurfaceFileSuffix;
        }

        public static string GetDaemonEditorTuiSurfacePath(string stateFilePath)
        {
            return stateFilePath + RuntimeDaemonEditorTuiSurfaceFileSuffix;
        }

        public static string GetReplicaStateFilePath(DirectoryInfo gameDataDirectory, string verseId)
        {
            var safeVerseId = string.IsNullOrWhiteSpace(verseId)
                ? "unknown-verse"
                : new string((verseId ?? "")
                    .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.'
                        ? ch
                        : '-')
                    .ToArray())
                    .Trim('-');

            if (string.IsNullOrWhiteSpace(safeVerseId))
                safeVerseId = "unknown-verse";

            return Path.Combine(gameDataDirectory.FullName, RuntimeReplicaDirectoryName, $"{safeVerseId}.cc");
        }

        public static string ResolveStatePathOverride()
        {
            return System.Environment.GetEnvironmentVariable(RuntimeStatePathOverrideEnvironmentVariable) ?? "";
        }

        public static string ResolveRuntimeIdOverride()
        {
            var configured = System.Environment.GetEnvironmentVariable(RuntimeIdOverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            return System.Environment.GetEnvironmentVariable(UnityRuntimeIdOverrideEnvironmentVariable) ?? "";
        }
    }
}
