using System;
using System.IO;

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
            bool stateFileExists,
            bool supportsLocalStateFileRead,
            string failureMessage)
        {
            ClientTargetPath = clientTargetPath;
            TargetKind = targetKind;
            TargetSource = targetSource;
            Title = title;
            VerseId = verseId;
            CultMeshAddress = cultMeshAddress;
            StateFilePath = stateFilePath;
            StateFileExists = stateFileExists;
            SupportsLocalStateFileRead = supportsLocalStateFileRead;
            FailureMessage = failureMessage;
        }

        public string ClientTargetPath { get; }
        public string TargetKind { get; }
        public string TargetSource { get; }
        public string Title { get; }
        public string VerseId { get; }
        public string CultMeshAddress { get; }
        public string StateFilePath { get; }
        public bool StateFileExists { get; }
        public bool SupportsLocalStateFileRead { get; }
        public string FailureMessage { get; }

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

            if (!string.IsNullOrWhiteSpace(configuredOverride))
            {
                stateFilePath = Path.GetFullPath(configuredOverride);
            }
            else if (string.Equals(targetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal))
            {
                stateFilePath = "";
                supportsLocalStateFileRead = false;
                failureMessage =
                    $"Selected client Verse target '{BuildTargetLabel(targetTitle, targetVerseId)}' points at CultMesh address '{targetCultMeshAddress}', but Unity still boots from local typed state until daemon transport lands.";
            }
            else
            {
                var configuredStateFilePath = string.IsNullOrWhiteSpace(target.StateFilePath)
                    ? defaultStateFilePath
                    : target.StateFilePath;
                stateFilePath = Path.GetFullPath(configuredStateFilePath);
            }

            return new AetheriaRuntimeStateBootReport(
                clientTargetPath,
                targetKind,
                targetSource,
                targetTitle,
                targetVerseId,
                targetCultMeshAddress,
                stateFilePath,
                supportsLocalStateFileRead && File.Exists(stateFilePath),
                supportsLocalStateFileRead,
                failureMessage);
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
