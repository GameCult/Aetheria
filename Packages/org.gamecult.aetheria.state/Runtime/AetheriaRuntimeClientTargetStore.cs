using System;
using System.IO;
using GameCult.Caching;
using GameCult.Mesh;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeClientTargetKinds
    {
        public const string StateFile = "state-file";
        public const string CultMeshVerse = "cultmesh-verse";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeClientTargetDocument
    {
        public const string SchemaId = "gamecult.aetheria.runtime_client_target.v1";
        public const string SchemaName = "aetheria.runtime_client_target";
        public const string SchemaVersion = "aetheria.runtime_client_target.v1";
        public const string DocumentKey = "local:aetheria.runtime_client_target.v1";

        [Key(0)] public string Schema { get; set; } = SchemaId;
        [Key(1)] public string TargetKind { get; set; } = AetheriaRuntimeClientTargetKinds.StateFile;
        [Key(2)] public string Title { get; set; } = "Local Aetheria";
        [Key(3)] public string VerseId { get; set; } = "aetheria.local";
        [Key(4)] public string CultMeshAddress { get; set; } = "asgard.local.aetheria/eve";
        [Key(5)] public string StateFilePath { get; set; } = "";
        [Key(6)] public string UpdatedAtUtc { get; set; } = "";
        [Key(7)] public string[] DiscoveryEndpoints { get; set; } = Array.Empty<string>();
        [Key(8)] public AetheriaRuntimeDiscoveredVerse[] DiscoveredVerses { get; set; } = Array.Empty<AetheriaRuntimeDiscoveredVerse>();
        [Key(9)] public string LastDiscoveryAtUtc { get; set; } = "";
        [Key(10)] public string LastDiscoveryError { get; set; } = "";
        [Key(11)] public string ReplicaStateFilePath { get; set; } = "";
        [Key(12)] public string LastReplicaSyncAtUtc { get; set; } = "";
        [Key(13)] public string LastReplicaSyncError { get; set; } = "";
        [Key(14)] public string RuntimeId { get; set; } = "raven-unity";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDiscoveredVerse
    {
        [Key(0)] public string VerseId { get; set; } = "";
        [Key(1)] public string DisplayName { get; set; } = "";
        [Key(2)] public string AuthorityModel { get; set; } = "";
        [Key(3)] public string TransportVersion { get; set; } = "";
        [Key(4)] public string RulesHash { get; set; } = "";
        [Key(5)] public string Description { get; set; } = "";
        [Key(6)] public string[] DiscoveryEndpoints { get; set; } = Array.Empty<string>();
        [Key(7)] public string[] AuthorityRuntimeIds { get; set; } = Array.Empty<string>();
        [Key(8)] public string ParentVerseId { get; set; } = "";
    }

    public static class AetheriaRuntimeClientTargetStore
    {
        public static AetheriaRuntimeClientTargetDocument ReadOrInitialize(
            string clientTargetPath,
            string defaultStateFilePath)
        {
            if (File.Exists(clientTargetPath))
                return Read(clientTargetPath);

            var document = CreateDefault(defaultStateFilePath);
            Write(clientTargetPath, document);
            return document;
        }

        public static AetheriaRuntimeClientTargetDocument CreateDefault(string defaultStateFilePath)
        {
            var gameDataDirectory = ResolveGameDataDirectory(defaultStateFilePath);
            return new AetheriaRuntimeClientTargetDocument
            {
                Schema = AetheriaRuntimeClientTargetDocument.SchemaId,
                TargetKind = AetheriaRuntimeClientTargetKinds.StateFile,
                Title = "Local Aetheria",
                VerseId = "aetheria.local",
                CultMeshAddress = "asgard.local.aetheria/eve",
                RuntimeId = "raven-unity",
                StateFilePath = defaultStateFilePath ?? "",
                ReplicaStateFilePath = AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, "aetheria.local"),
                UpdatedAtUtc = DateTime.UtcNow.ToString("O")
            };
        }

        public static void Write(string clientTargetPath, AetheriaRuntimeClientTargetDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = AetheriaRuntimeClientTargetDocument.SchemaId;
            document.DiscoveryEndpoints ??= Array.Empty<string>();
            document.DiscoveredVerses ??= Array.Empty<AetheriaRuntimeDiscoveredVerse>();
            document.LastDiscoveryAtUtc ??= "";
            document.LastDiscoveryError ??= "";
            document.ReplicaStateFilePath = NormalizeReplicaStateFilePath(document, clientTargetPath, document.ReplicaStateFilePath);
            document.LastReplicaSyncAtUtc ??= "";
            document.LastReplicaSyncError ??= "";
            if (string.IsNullOrWhiteSpace(document.RuntimeId))
                document.RuntimeId = "raven-unity";
            foreach (var verse in document.DiscoveredVerses)
            {
                if (verse == null)
                    continue;

                verse.VerseId ??= "";
                verse.DisplayName ??= "";
                verse.AuthorityModel ??= "";
                verse.TransportVersion ??= "";
                verse.RulesHash ??= "";
                verse.Description ??= "";
                verse.DiscoveryEndpoints ??= Array.Empty<string>();
                verse.AuthorityRuntimeIds ??= Array.Empty<string>();
                verse.ParentVerseId ??= "";
            }

            document.UpdatedAtUtc = string.IsNullOrWhiteSpace(document.UpdatedAtUtc)
                ? DateTime.UtcNow.ToString("O")
                : document.UpdatedAtUtc;

            CultMesh.WriteSingleFileDocumentPayload(
                clientTargetPath,
                new CultRecordKey(AetheriaRuntimeClientTargetDocument.DocumentKey),
                new CultMeshSingleFileDocumentSchema(
                    AetheriaRuntimeClientTargetDocument.SchemaId,
                    AetheriaRuntimeClientTargetDocument.SchemaName,
                    AetheriaRuntimeClientTargetDocument.SchemaVersion),
                document.UpdatedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeClientTargetDocument Update(
            string clientTargetPath,
            string defaultStateFilePath,
            Action<AetheriaRuntimeClientTargetDocument> mutate)
        {
            if (mutate == null) throw new ArgumentNullException(nameof(mutate));

            var document = ReadOrInitialize(clientTargetPath, defaultStateFilePath);
            mutate(document);
            document.ReplicaStateFilePath = NormalizeReplicaStateFilePath(document, clientTargetPath, document.ReplicaStateFilePath);
            Write(clientTargetPath, document);
            return document;
        }

        public static AetheriaRuntimeClientTargetDocument Read(string clientTargetPath)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeClientTargetDocument>(
                CultMesh.ReadSingleFileDocumentPayload(
                    clientTargetPath,
                    new CultRecordKey(AetheriaRuntimeClientTargetDocument.DocumentKey),
                    AetheriaRuntimeClientTargetDocument.SchemaId));
        }

        private static string NormalizeReplicaStateFilePath(
            AetheriaRuntimeClientTargetDocument document,
            string clientTargetPath,
            string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return Path.GetFullPath(configuredPath);

            var gameDataDirectory = ResolveGameDataDirectoryFromClientTarget(clientTargetPath);
            return AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, document?.VerseId ?? "");
        }

        private static DirectoryInfo ResolveGameDataDirectory(string defaultStateFilePath)
        {
            var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(defaultStateFilePath) ? "." : defaultStateFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            return new DirectoryInfo(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        }

        private static DirectoryInfo ResolveGameDataDirectoryFromClientTarget(string clientTargetPath)
        {
            var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(clientTargetPath) ? "." : clientTargetPath);
            var directory = Path.GetDirectoryName(fullPath);
            return new DirectoryInfo(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        }
    }
}
