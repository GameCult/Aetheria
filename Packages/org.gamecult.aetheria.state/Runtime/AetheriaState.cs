using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public readonly struct AetheriaState
    {
        private readonly DirectoryInfo _gameDataDirectory;

        private AetheriaState(DirectoryInfo gameDataDirectory)
        {
            _gameDataDirectory = gameDataDirectory ?? throw new ArgumentNullException(nameof(gameDataDirectory));
        }

        public static AetheriaState At(DirectoryInfo gameDataDirectory)
        {
            return new AetheriaState(gameDataDirectory);
        }

        public AetheriaRuntimeStateBootReport Boot =>
            AetheriaRuntimeStateBoot.Inspect(_gameDataDirectory);

        public AetheriaClientTarget ClientTarget =>
            new AetheriaClientTarget(_gameDataDirectory);

        public bool TryReadDaemonFrame(out AetheriaRuntimeDaemonFrameDocument? frame)
        {
            var boot = Boot;
            return AetheriaRuntimeStateReader.TryReadDaemonFrame(boot.StateFilePath, out frame);
        }

        public AetheriaRuntimeVerseHostSettingsSnapshot ReadVerseHostSettings()
        {
            var boot = Boot;
            return AetheriaRuntimeStateReader.ReadVerseHostSettings(boot.StateFilePath);
        }
    }

    public readonly struct AetheriaClientTarget
    {
        private readonly DirectoryInfo _gameDataDirectory;

        internal AetheriaClientTarget(DirectoryInfo gameDataDirectory)
        {
            _gameDataDirectory = gameDataDirectory ?? throw new ArgumentNullException(nameof(gameDataDirectory));
        }

        public AetheriaRuntimeClientTargetDocument Refresh()
        {
            var paths = Paths();
            return AetheriaRuntimeClientTargetStore.ReadOrInitialize(paths.ClientTargetPath, paths.DefaultStateFilePath);
        }

        public AetheriaRuntimeClientTargetDocument DiscoverVerses()
        {
            var paths = Paths();
            return AetheriaRuntimeVerseDiscovery.RefreshClientTarget(paths.ClientTargetPath, paths.DefaultStateFilePath);
        }

        public AetheriaRuntimeClientTargetDocument SyncReplica()
        {
            var paths = Paths();
            return SyncReplica(paths.ClientTargetPath, paths.DefaultStateFilePath);
        }

        public AetheriaRuntimeClientTargetDocument CycleTransport()
        {
            return Edit(document =>
            {
                document.TargetKind = string.Equals(document.TargetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal)
                    ? AetheriaRuntimeClientTargetKinds.StateFile
                    : AetheriaRuntimeClientTargetKinds.CultMeshVerse;
                document.LastReplicaSyncError = "";
            });
        }

        public AetheriaRuntimeClientTargetDocument RequestTitle(string title)
        {
            return Edit(document => document.Title = title ?? "");
        }

        public AetheriaRuntimeClientTargetDocument RequestVerseId(string verseId)
        {
            var gameDataDirectory = _gameDataDirectory;
            return Edit(document =>
            {
                document.VerseId = verseId ?? "";
                document.ReplicaStateFilePath = AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, document.VerseId);
                document.LastReplicaSyncError = "";
            });
        }

        public AetheriaRuntimeClientTargetDocument RequestCultMeshAddress(string cultMeshAddress)
        {
            return Edit(document =>
            {
                document.CultMeshAddress = cultMeshAddress ?? "";
                document.LastReplicaSyncError = "";
            });
        }

        public AetheriaRuntimeClientTargetDocument RequestStateFilePath(string stateFilePath)
        {
            return Edit(document => document.StateFilePath = stateFilePath ?? "");
        }

        public AetheriaRuntimeClientTargetDocument RequestDiscoveryEndpoints(IEnumerable<string>? discoveryEndpoints)
        {
            return Edit(document =>
            {
                document.DiscoveryEndpoints = NormalizeDiscoveryEndpoints(discoveryEndpoints);
                document.LastDiscoveryError = "";
            });
        }

        public AetheriaRuntimeClientTargetDocument SelectDiscoveredVerse(
            string verseId,
            string title,
            string cultMeshAddress,
            IEnumerable<string>? discoveryEndpoints)
        {
            var gameDataDirectory = _gameDataDirectory;
            return Edit(document =>
            {
                document.TargetKind = AetheriaRuntimeClientTargetKinds.CultMeshVerse;
                document.Title = title ?? "";
                document.VerseId = verseId ?? "";
                document.CultMeshAddress = cultMeshAddress ?? "";
                document.ReplicaStateFilePath = AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, document.VerseId);
                document.DiscoveryEndpoints = NormalizeDiscoveryEndpoints(discoveryEndpoints);
                document.LastDiscoveryError = "";
                document.LastReplicaSyncError = "";
            });
        }

        private AetheriaRuntimeClientTargetDocument Edit(Action<AetheriaRuntimeClientTargetDocument> mutate)
        {
            if (mutate == null) throw new ArgumentNullException(nameof(mutate));

            var paths = Paths();
            return AetheriaRuntimeClientTargetStore.Update(
                paths.ClientTargetPath,
                paths.DefaultStateFilePath,
                document =>
                {
                    mutate(document);
                    if (string.IsNullOrWhiteSpace(document.TargetKind))
                        document.TargetKind = AetheriaRuntimeClientTargetKinds.StateFile;
                    if (string.IsNullOrWhiteSpace(document.StateFilePath))
                        document.StateFilePath = paths.DefaultStateFilePath;
                    document.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
                });
        }

        private AetheriaRuntimeClientTargetDocument SyncReplica(
            string clientTargetPath,
            string defaultStateFilePath)
        {
            var target = AetheriaRuntimeClientTargetStore.ReadOrInitialize(clientTargetPath, defaultStateFilePath);
            var syncedAtUtc = DateTime.UtcNow.ToString("O");
            try
            {
                var result = AetheriaRuntimeVerseReplicaBridge.Sync(_gameDataDirectory, target);
                return AetheriaRuntimeClientTargetStore.Update(
                    clientTargetPath,
                    defaultStateFilePath,
                    document =>
                    {
                        document.ReplicaStateFilePath = result.ReplicaStateFilePath;
                        document.LastReplicaSyncAtUtc = syncedAtUtc;
                        document.LastReplicaSyncError = "";
                        document.UpdatedAtUtc = syncedAtUtc;
                    });
            }
            catch (Exception ex)
            {
                AetheriaRuntimeClientTargetStore.Update(
                    clientTargetPath,
                    defaultStateFilePath,
                    document =>
                    {
                        document.LastReplicaSyncAtUtc = syncedAtUtc;
                        document.LastReplicaSyncError = ex.Message ?? ex.GetType().Name;
                        document.UpdatedAtUtc = syncedAtUtc;
                    });
                throw;
            }
        }

        private (string ClientTargetPath, string DefaultStateFilePath) Paths()
        {
            return (
                AetheriaRuntimeStateBoundary.GetClientTargetPath(_gameDataDirectory),
                AetheriaRuntimeStateBoundary.GetStateFilePath(_gameDataDirectory));
        }

        private static string[] NormalizeDiscoveryEndpoints(IEnumerable<string>? discoveryEndpoints)
        {
            return discoveryEndpoints?
                .Select(endpoint => endpoint?.Trim() ?? "")
                .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>();
        }

    }
}
