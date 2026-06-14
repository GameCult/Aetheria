using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    internal static class AetheriaRuntimePendingCultCacheStore
    {
        private const string FormatVersion = "cultcache.store.v1";
        private const int StoreSnapshotFieldCount = 3;
        private const int SchemaCatalogEntryFieldCount = 7;
        private const int SchemaCatalogMemberFieldCount = 8;
        private const int PersistedRecordFieldCount = 4;

        public static void WriteStateCommit(string path, AetheriaRuntimeStateCommitDocument document)
        {
            WriteDocument(
                path,
                $"pending:aetheria.runtime_commit.{document.CommandId}.v1",
                AetheriaRuntimeStateCommitDocument.SchemaId,
                "aetheria.runtime_commit",
                "aetheria.runtime_commit.v1",
                document.CreatedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeStateCommitDocument ReadStateCommit(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeStateCommitDocument>(
                ReadDocumentPayload(path, AetheriaRuntimeStateCommitDocument.SchemaId));
        }

        public static void WriteEveCommand(string path, AetheriaRuntimeEveCommandDocument document)
        {
            WriteDocument(
                path,
                $"pending:gamecult.eve.command.{document.CommandId}.v1",
                AetheriaRuntimeEveCommandDocument.SchemaId,
                "gamecult.eve.command",
                "gamecult.eve.command.v1",
                document.IssuedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeEveCommandDocument ReadEveCommand(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeEveCommandDocument>(
                ReadDocumentPayload(path, AetheriaRuntimeEveCommandDocument.SchemaId));
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
                throw new InvalidDataException($"Pending CultCache document '{path}' is missing store fields.");
            }

            var formatVersion = reader.ReadString() ?? "";
            if (!formatVersion.StartsWith("cultcache.store.v1", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Pending document '{path}' is not a CultCache store.");
            }

            var schemaIds = ReadSchemaCatalog(ref reader);
            var recordCount = reader.ReadArrayHeader();
            if (recordCount != 1)
            {
                throw new InvalidDataException($"Pending CultCache document '{path}' must contain exactly one record.");
            }

            var recordFieldCount = reader.ReadArrayHeader();
            if (recordFieldCount < PersistedRecordFieldCount)
            {
                throw new InvalidDataException($"Pending CultCache record '{path}' is missing record fields.");
            }

            reader.Skip(); // key
            var schemaId = reader.ReadString() ?? "";
            reader.Skip(); // storedAt
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
                throw new InvalidDataException($"Pending document '{path}' has schema '{schemaId}', expected '{expectedSchemaId}'.");
            }

            if (!schemaIds.Contains(schemaId, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Pending document '{path}' does not publish schema '{schemaId}' in its CultCache catalog.");
            }

            return payload;
        }

        private static string[] ReadSchemaCatalog(ref MessagePackReader reader)
        {
            var count = reader.ReadArrayHeader();
            var schemaIds = new string[count];
            for (var index = 0; index < count; index++)
            {
                var fieldCount = reader.ReadArrayHeader();
                schemaIds[index] = fieldCount > 0 ? reader.ReadString() ?? "" : "";
                for (var field = 1; field < fieldCount; field++)
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
            writer.Write(ContentHash(payload));
            writer.Write("");
            writer.WriteArrayHeader(0);
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
            writer.Write(storedAt ?? "");
            writer.Write(payload);
        }

        private static string ContentHash(byte[] payload)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
        }

        private static void WriteFileAtomically(string path, byte[] payload)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            File.WriteAllBytes(tempPath, payload);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
    }
}
