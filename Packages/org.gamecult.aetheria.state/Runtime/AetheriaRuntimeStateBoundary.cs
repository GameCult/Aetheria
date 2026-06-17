using System.IO;
using System.Linq;

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeStateBoundary
    {
        public const string RuntimeStateFileName = "aetheria-world.cc";
        public const string RuntimeClientTargetFileName = "aetheria-client.cc";
        public const string RuntimeReplicaDirectoryName = "Verses";
        public const string RuntimeStatePathOverrideEnvironmentVariable = "AETHERIA_STATE_PATH";
        public const string LegacyRuntimeStatePathOverrideEnvironmentVariable = "AETHERIA_EVE_STATE_PATH";

        public static string GetStateFilePath(DirectoryInfo gameDataDirectory)
        {
            return Path.Combine(gameDataDirectory.FullName, RuntimeStateFileName);
        }

        public static string GetClientTargetPath(DirectoryInfo gameDataDirectory)
        {
            return Path.Combine(gameDataDirectory.FullName, RuntimeClientTargetFileName);
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
            var configured = System.Environment.GetEnvironmentVariable(RuntimeStatePathOverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            return System.Environment.GetEnvironmentVariable(LegacyRuntimeStatePathOverrideEnvironmentVariable) ?? "";
        }
    }
}
