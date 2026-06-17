using System;
using System.IO;
using System.Linq;

namespace GameCult.Aetheria.State.Unity
{
    public readonly struct AetheriaRuntimeStateBootReport
    {
        public AetheriaRuntimeStateBootReport(
            string clientTargetPath,
            string targetKind,
            string targetSource,
            string title,
            string verseId,
            string cultMeshAddress,
            string stateFilePath,
            string replicaStateFilePath,
            bool stateFileExists,
            bool supportsLocalStateFileRead,
            string failureMessage,
            string[] discoveryEndpoints,
            AetheriaRuntimeDiscoveredVerse[] discoveredVerses,
            string lastDiscoveryAtUtc,
            string lastDiscoveryError)
        {
            ClientTargetPath = clientTargetPath;
            TargetKind = targetKind;
            TargetSource = targetSource;
            Title = title;
            VerseId = verseId;
            CultMeshAddress = cultMeshAddress;
            StateFilePath = stateFilePath;
            ReplicaStateFilePath = replicaStateFilePath;
            StateFileExists = stateFileExists;
            SupportsLocalStateFileRead = supportsLocalStateFileRead;
            FailureMessage = failureMessage;
            DiscoveryEndpoints = discoveryEndpoints ?? Array.Empty<string>();
            DiscoveredVerses = discoveredVerses ?? Array.Empty<AetheriaRuntimeDiscoveredVerse>();
            LastDiscoveryAtUtc = lastDiscoveryAtUtc ?? "";
            LastDiscoveryError = lastDiscoveryError ?? "";
        }

        public string ClientTargetPath { get; }
        public string TargetKind { get; }
        public string TargetSource { get; }
        public string Title { get; }
        public string VerseId { get; }
        public string CultMeshAddress { get; }
        public string StateFilePath { get; }
        public string ReplicaStateFilePath { get; }
        public bool StateFileExists { get; }
        public bool SupportsLocalStateFileRead { get; }
        public string FailureMessage { get; }
        public string[] DiscoveryEndpoints { get; }
        public AetheriaRuntimeDiscoveredVerse[] DiscoveredVerses { get; }
        public string LastDiscoveryAtUtc { get; }
        public string LastDiscoveryError { get; }

        public string TargetLabel =>
            string.IsNullOrWhiteSpace(Title)
                ? (string.IsNullOrWhiteSpace(VerseId) ? "Unknown Verse" : VerseId)
                : (string.IsNullOrWhiteSpace(VerseId) || string.Equals(Title, VerseId, StringComparison.Ordinal)
                    ? Title
                    : $"{Title} ({VerseId})");
    }

    public static class AetheriaRuntimeStateBoot
    {
        public static AetheriaRuntimeStateBootReport Inspect(
            DirectoryInfo gameDataDirectory,
            string explicitStateFilePathOverride = "")
        {
            if (gameDataDirectory == null) throw new ArgumentNullException(nameof(gameDataDirectory));

            var defaultStateFilePath = AetheriaRuntimeStateBoundary.GetStateFilePath(gameDataDirectory);
            var clientTargetPath = AetheriaRuntimeStateBoundary.GetClientTargetPath(gameDataDirectory);
            var target = AetheriaRuntimeClientTargetStore.ReadOrInitialize(clientTargetPath, defaultStateFilePath);

            var configuredOverride = string.IsNullOrWhiteSpace(explicitStateFilePathOverride)
                ? AetheriaRuntimeStateBoundary.ResolveStatePathOverride()
                : explicitStateFilePathOverride;

            var targetSource = string.IsNullOrWhiteSpace(configuredOverride)
                ? "client-target"
                : "state-path-override";
            var targetKind = target.TargetKind ?? "";
            var targetTitle = target.Title ?? "";
            var targetVerseId = target.VerseId ?? "";
            var targetCultMeshAddress = target.CultMeshAddress ?? "";
            var supportsLocalStateFileRead = true;
            var failureMessage = "";
            string stateFilePath;
            string replicaStateFilePath;

            if (!string.IsNullOrWhiteSpace(configuredOverride))
            {
                stateFilePath = Path.GetFullPath(configuredOverride);
                replicaStateFilePath = target.ReplicaStateFilePath ?? "";
            }
            else if (string.Equals(targetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal))
            {
                replicaStateFilePath = string.IsNullOrWhiteSpace(target.ReplicaStateFilePath)
                    ? AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, targetVerseId)
                    : Path.GetFullPath(target.ReplicaStateFilePath);
                stateFilePath = replicaStateFilePath;
                supportsLocalStateFileRead = File.Exists(replicaStateFilePath);
                failureMessage = supportsLocalStateFileRead
                    ? ""
                    : $"Selected client Verse target '{BuildTargetLabel(targetTitle, targetVerseId)}' follows remote CultMesh endpoint '{targetCultMeshAddress}'. Sync the local replica at '{replicaStateFilePath}' before booting Unity from that Verse.";
            }
            else
            {
                var configuredStateFilePath = string.IsNullOrWhiteSpace(target.StateFilePath)
                    ? defaultStateFilePath
                    : target.StateFilePath;
                stateFilePath = Path.GetFullPath(configuredStateFilePath);
                replicaStateFilePath = string.IsNullOrWhiteSpace(target.ReplicaStateFilePath)
                    ? AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, targetVerseId)
                    : Path.GetFullPath(target.ReplicaStateFilePath);
            }

            return new AetheriaRuntimeStateBootReport(
                clientTargetPath,
                targetKind,
                targetSource,
                targetTitle,
                targetVerseId,
                targetCultMeshAddress,
                stateFilePath,
                replicaStateFilePath,
                supportsLocalStateFileRead && File.Exists(stateFilePath),
                supportsLocalStateFileRead,
                failureMessage,
                target.DiscoveryEndpoints?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>(),
                target.DiscoveredVerses ?? Array.Empty<AetheriaRuntimeDiscoveredVerse>(),
                target.LastDiscoveryAtUtc ?? "",
                target.LastDiscoveryError ?? "");
        }

        private static string BuildTargetLabel(string title, string verseId)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.IsNullOrWhiteSpace(verseId) ? "Unknown Verse" : verseId;

            return string.IsNullOrWhiteSpace(verseId) || string.Equals(title, verseId, StringComparison.Ordinal)
                ? title
                : $"{title} ({verseId})";
        }
    }
}
