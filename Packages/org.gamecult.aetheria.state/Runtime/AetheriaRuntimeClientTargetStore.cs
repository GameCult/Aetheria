using System;
using System.Buffers;
using System.IO;
using System.Linq;
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
        private const string FormatVersion = "cultcache.store.v1";
        private const int StoreSnapshotFieldCount = 3;
        private const int SchemaCatalogEntryFieldCount = 7;
        private const int SchemaCatalogMemberFieldCount = 8;
        private const int PersistedRecordFieldCount = 4;

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

            WriteDocument(
                clientTargetPath,
                AetheriaRuntimeClientTargetDocument.DocumentKey,
                AetheriaRuntimeClientTargetDocument.SchemaId,
                AetheriaRuntimeClientTargetDocument.SchemaName,
                AetheriaRuntimeClientTargetDocument.SchemaVersion,
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
                ReadDocumentPayload(clientTargetPath, AetheriaRuntimeClientTargetDocument.SchemaId));
        }

        private static void WriteDocument(
            string path,
            string key,
            string schemaId,
            string schemaName,
            string schemaVersion,
            string storedAt,
            byte[] payload)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);

            writer.WriteArrayHeader(StoreSnapshotFieldCount);
            writer.Write(FormatVersion);
            writer.WriteArrayHeader(1);
            WriteSchemaCatalogEntry(ref writer, schemaId, schemaName, schemaVersion, payload);
            writer.WriteArrayHeader(1);
            WritePersistedRecord(ref writer, key, schemaId, storedAt, payload);
            writer.Flush();

            WriteFileAtomically(path, buffer.WrittenSpan.ToArray());
        }

        private static byte[] ReadDocumentPayload(string path, string expectedSchemaId)
        {
            var reader = new MessagePackReader(File.ReadAllBytes(path));
            var fieldCount = reader.ReadArrayHeader();
            if (fieldCount < StoreSnapshotFieldCount)
            {
                throw new InvalidDataException($"Client target CultCache document '{path}' is missing store fields.");
            }

            var formatVersion = reader.ReadString() ?? "";
            if (!formatVersion.StartsWith(FormatVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Client target '{path}' is not a CultCache store.");
            }

            var schemaIds = ReadSchemaCatalog(ref reader);
            var recordCount = reader.ReadArrayHeader();
            if (recordCount != 1)
            {
                throw new InvalidDataException($"Client target CultCache document '{path}' must contain exactly one record.");
            }

            var recordFieldCount = reader.ReadArrayHeader();
            if (recordFieldCount < PersistedRecordFieldCount)
            {
                throw new InvalidDataException($"Client target CultCache record '{path}' is missing record fields.");
            }

            reader.Skip();
            var schemaId = reader.ReadString() ?? "";
            reader.Skip();
            var payload = reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
            for (var index = PersistedRecordFieldCount; index < recordFieldCount; index++)
            {
                reader.Skip();
            }

            for (var index = StoreSnapshotFieldCount; index < fieldCount; index++)
            {
                reader.Skip();
            }

            if (!string.Equals(schemaId, expectedSchemaId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Client target '{path}' has schema '{schemaId}', expected '{expectedSchemaId}'.");
            }

            if (Array.IndexOf(schemaIds, schemaId) < 0)
            {
                throw new InvalidDataException($"Client target '{path}' does not publish schema '{schemaId}' in its CultCache catalog.");
            }

            return payload;
        }

        private static string[] ReadSchemaCatalog(ref MessagePackReader reader)
        {
            var schemaCount = reader.ReadArrayHeader();
            var schemaIds = new string[schemaCount];
            for (var index = 0; index < schemaCount; index++)
            {
                var fieldCount = reader.ReadArrayHeader();
                if (fieldCount < SchemaCatalogEntryFieldCount)
                {
                    throw new InvalidDataException("Client target CultCache schema entry is missing fields.");
                }

                schemaIds[index] = reader.ReadString() ?? "";
                reader.Skip();
                reader.Skip();
                reader.Skip();
                reader.Skip();
                reader.Skip();

                var memberCount = reader.ReadArrayHeader();
                for (var member = 0; member < memberCount; member++)
                {
                    var memberFieldCount = reader.ReadArrayHeader();
                    for (var field = 0; field < Math.Max(memberFieldCount, SchemaCatalogMemberFieldCount); field++)
                    {
                        if (field < memberFieldCount)
                            reader.Skip();
                    }
                }

                for (var field = SchemaCatalogEntryFieldCount; field < fieldCount; field++)
                {
                    reader.Skip();
                }
            }

            return schemaIds;
        }

        private static void WriteSchemaCatalogEntry(
            ref MessagePackWriter writer,
            string schemaId,
            string schemaName,
            string schemaVersion,
            byte[] payload)
        {
            writer.WriteArrayHeader(SchemaCatalogEntryFieldCount);
            writer.Write(schemaId);
            writer.Write(schemaName);
            writer.Write(schemaVersion);
            writer.Write("Runtime client target");
            writer.Write(DateTime.UtcNow.ToString("O"));
            writer.Write(StableHash(payload));
            writer.WriteArrayHeader(0);
        }

        private static void WritePersistedRecord(
            ref MessagePackWriter writer,
            string key,
            string schemaId,
            string storedAt,
            byte[] payload)
        {
            writer.WriteArrayHeader(PersistedRecordFieldCount);
            writer.Write(key);
            writer.Write(schemaId);
            writer.Write(storedAt);
            writer.Write(payload);
        }

        private static string StableHash(byte[] payload)
        {
            if (payload.Length == 0)
                return "empty";

            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(payload);
            return BitConverter.ToString(hash).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        }

        private static void WriteFileAtomically(string path, byte[] bytes)
        {
            var tempPath = path + ".tmp";
            File.WriteAllBytes(tempPath, bytes);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
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
