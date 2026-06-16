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
            return ReadStateCommitDocument(ReadDocumentPayload(path, AetheriaRuntimeStateCommitDocument.SchemaId));
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

        private static AetheriaRuntimeStateCommitDocument ReadStateCommitDocument(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fieldCount = reader.ReadArrayHeader();
            var document = new AetheriaRuntimeStateCommitDocument
            {
                Schema = ReadFieldString(ref reader, fieldCount, 0, AetheriaRuntimeStateCommitDocument.SchemaId),
                Kind = ReadFieldString(ref reader, fieldCount, 1, ""),
                CommandId = ReadFieldString(ref reader, fieldCount, 2, ""),
                CreatedAtUtc = ReadFieldString(ref reader, fieldCount, 3, ""),
                PlayerSettings = ReadFieldDocument<AetheriaRuntimePlayerSettingsCommit>(ref reader, fieldCount, 4),
                LoadoutTemplate = ReadFieldDocument<AetheriaRuntimeLoadoutTemplateCommit>(ref reader, fieldCount, 5),
                RunCheckpoint = ReadFieldRunCheckpoint(ref reader, fieldCount, 6)
            };

            for (var index = 7; index < fieldCount; index++)
            {
                reader.Skip();
            }

            return document;
        }

        private static T? ReadFieldDocument<T>(ref MessagePackReader reader, int fields, int index)
            where T : class
        {
            if (index >= fields)
            {
                return null;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return null;
            }

            return MessagePackSerializer.Deserialize<T>(ReadRawBytes(ref reader));
        }

        private static AetheriaRuntimeRunCheckpointCommit? ReadFieldRunCheckpoint(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields)
            {
                return null;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return null;
            }

            return ReadRunCheckpoint(ReadRawBytes(ref reader));
        }

        private static AetheriaRuntimeRunCheckpointCommit ReadRunCheckpoint(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();

            var runId = ReadFieldString(ref reader, fields, 0, "local");
            var isTutorial = ReadFieldBool(ref reader, fields, 1, false);
            var entranceZoneIndex = ReadFieldInt32(ref reader, fields, 2, -1);
            var exitZoneIndex = ReadFieldInt32(ref reader, fields, 3, -1);
            var currentZoneIndex = ReadFieldInt32(ref reader, fields, 4, -1);

            string currentEntityKey;
            int[] discoveredZoneIndices;
            AetheriaRuntimeZoneSnapshotCommit[] zones;
            AetheriaRuntimeActionBarBindingCommit[] actionBarBindings;
            AetheriaRuntimeFactionRelationshipCommit[] factionRelationships;
            uint generationSeed;

            if (fields >= 12)
            {
                var legacyCurrentZoneEntityIndex = ReadFieldInt32(ref reader, fields, 5, -1);
                discoveredZoneIndices = ReadFieldArray<int>(ref reader, fields, 6);
                zones = ReadFieldArray<AetheriaRuntimeZoneSnapshotCommit>(ref reader, fields, 7);
                actionBarBindings = ReadFieldArray<AetheriaRuntimeActionBarBindingCommit>(ref reader, fields, 8);
                factionRelationships = ReadFieldArray<AetheriaRuntimeFactionRelationshipCommit>(ref reader, fields, 9);
                generationSeed = ReadFieldUInt32(ref reader, fields, 10, 0);
                currentEntityKey = ReadFieldString(ref reader, fields, 11, "");
                if (string.IsNullOrWhiteSpace(currentEntityKey))
                {
                    currentEntityKey = LegacyCurrentEntityKey(runId, currentZoneIndex, legacyCurrentZoneEntityIndex);
                }
            }
            else
            {
                discoveredZoneIndices = ReadFieldArray<int>(ref reader, fields, 5);
                zones = ReadFieldArray<AetheriaRuntimeZoneSnapshotCommit>(ref reader, fields, 6);
                actionBarBindings = ReadFieldArray<AetheriaRuntimeActionBarBindingCommit>(ref reader, fields, 7);
                factionRelationships = ReadFieldArray<AetheriaRuntimeFactionRelationshipCommit>(ref reader, fields, 8);
                generationSeed = ReadFieldUInt32(ref reader, fields, 9, 0);
                currentEntityKey = ReadFieldString(ref reader, fields, 10, "");
            }

            return new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = runId,
                IsTutorial = isTutorial,
                EntranceZoneIndex = entranceZoneIndex,
                ExitZoneIndex = exitZoneIndex,
                CurrentZoneIndex = currentZoneIndex,
                DiscoveredZoneIndices = discoveredZoneIndices,
                Zones = zones,
                ActionBarBindings = actionBarBindings,
                FactionRelationships = factionRelationships,
                GenerationSeed = generationSeed,
                CurrentEntityKey = currentEntityKey
            };
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

        private static string ReadFieldString(ref MessagePackReader reader, int fields, int index, string fallback)
        {
            return index >= fields ? fallback : reader.ReadString() ?? fallback;
        }

        private static int ReadFieldInt32(ref MessagePackReader reader, int fields, int index, int fallback)
        {
            if (index >= fields)
            {
                return fallback;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return fallback;
            }

            return reader.ReadInt32();
        }

        private static uint ReadFieldUInt32(ref MessagePackReader reader, int fields, int index, uint fallback)
        {
            if (index >= fields)
            {
                return fallback;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return fallback;
            }

            return reader.ReadUInt32();
        }

        private static bool ReadFieldBool(ref MessagePackReader reader, int fields, int index, bool fallback)
        {
            if (index >= fields)
            {
                return fallback;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return fallback;
            }

            return reader.ReadBoolean();
        }

        private static T[] ReadFieldArray<T>(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields)
            {
                return Array.Empty<T>();
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return Array.Empty<T>();
            }

            return MessagePackSerializer.Deserialize<T[]>(ReadRawBytes(ref reader)) ?? Array.Empty<T>();
        }

        private static byte[] ReadRawBytes(ref MessagePackReader reader)
        {
            return reader.ReadRaw().ToArray();
        }

        private static string LegacyCurrentEntityKey(string runId, int zoneIndex, int entityIndex)
        {
            if (string.IsNullOrWhiteSpace(runId) || zoneIndex < 0 || entityIndex < 0)
            {
                return "";
            }

            return $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
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
