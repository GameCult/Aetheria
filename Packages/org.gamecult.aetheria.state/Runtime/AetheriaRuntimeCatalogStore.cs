using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeCatalogStore
    {
        private const string ItemDefinitionSchema = "aetheria.item_definition";
        private const string CorporationSchema = "aetheria.corporation";
        private const string NameFileSchema = "aetheria.name_file";

        public static AetheriaRuntimeCatalogSnapshot OpenReadOnly(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            var items = new List<AetheriaRuntimeCatalogItem>();
            var corporations = new List<AetheriaRuntimeCorporation>();
            var nameFiles = new List<AetheriaRuntimeNameFile>();

            foreach (var record in ReadRecords(stateFilePath))
            {
                if (!catalog.TryGetValue(record.SchemaId, out var schemaName))
                    continue;

                if (schemaName == ItemDefinitionSchema)
                    items.Add(ReadItem(record.Payload));
                else if (schemaName == CorporationSchema)
                    corporations.Add(ReadCorporation(record.Payload));
                else if (schemaName == NameFileSchema)
                    nameFiles.Add(ReadNameFile(record.Payload));
            }

            return new AetheriaRuntimeCatalogSnapshot(
                items.ToArray(),
                corporations.ToArray(),
                nameFiles.ToArray());
        }

        private static Dictionary<string, string> ReadSchemaCatalog(string stateFilePath)
        {
            if (!File.Exists(stateFilePath))
                throw new FileNotFoundException("Aetheria typed state file was not found.", stateFilePath);

            var reader = new MessagePackReader(File.ReadAllBytes(stateFilePath));
            var snapshotFields = reader.ReadArrayHeader();
            if (snapshotFields < 2)
                throw new InvalidDataException("CultCache snapshot is missing its embedded schema catalog.");

            reader.Skip();
            var schemaCount = reader.ReadArrayHeader();
            var catalog = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < schemaCount; index++)
            {
                var fieldCount = reader.ReadArrayHeader();
                var schemaId = fieldCount > 0 ? ReadString(ref reader) : "";
                var schemaName = fieldCount > 1 ? ReadString(ref reader) : "";
                for (var field = 2; field < fieldCount; field++)
                    reader.Skip();

                if (!string.IsNullOrWhiteSpace(schemaId) && !string.IsNullOrWhiteSpace(schemaName))
                    catalog[schemaId] = schemaName;
            }

            return catalog;
        }

        private static IReadOnlyList<PersistedRecord> ReadRecords(string stateFilePath)
        {
            var records = new List<PersistedRecord>();
            var reader = new MessagePackReader(File.ReadAllBytes(stateFilePath));
            var snapshotFields = reader.ReadArrayHeader();
            if (snapshotFields > 0) reader.Skip();
            if (snapshotFields > 1) reader.Skip();
            if (snapshotFields > 2)
            {
                var recordCount = reader.ReadArrayHeader();
                for (var index = 0; index < recordCount; index++)
                    records.Add(ReadPersistedRecord(ref reader));
            }

            var recordDirectory = stateFilePath + ".records";
            if (!Directory.Exists(recordDirectory))
                return records;

            foreach (var recordFile in Directory.EnumerateFiles(recordDirectory, "*.msgpack").OrderBy(path => path, StringComparer.Ordinal))
            {
                var recordReader = new MessagePackReader(File.ReadAllBytes(recordFile));
                records.Add(ReadPersistedRecord(ref recordReader));
            }

            return records;
        }

        private static PersistedRecord ReadPersistedRecord(ref MessagePackReader reader)
        {
            var fieldCount = reader.ReadArrayHeader();
            if (fieldCount > 0) reader.Skip();
            var schemaId = fieldCount > 1 ? ReadString(ref reader) : "";
            if (fieldCount > 2) reader.Skip();
            var payload = Array.Empty<byte>();
            if (fieldCount > 3)
            {
                var sequence = reader.ReadBytes();
                if (sequence.HasValue)
                    payload = sequence.Value.ToArray();
            }
            for (var field = 4; field < fieldCount; field++)
                reader.Skip();
            return new PersistedRecord(schemaId, payload);
        }

        private static AetheriaRuntimeCatalogItem ReadItem(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var category = ReadFieldString(ref reader, fields, 1);
            var legacyId = ReadFieldString(ref reader, fields, 2);
            var description = ReadFieldString(ref reader, fields, 3);
            var mass = ReadFieldDouble(ref reader, fields, 4);
            var volume = ReadFieldDouble(ref reader, fields, 5);
            SkipField(ref reader, fields, 6);
            var manufacturerLegacyId = ReadFieldString(ref reader, fields, 7);
            var price = ReadFieldInt32(ref reader, fields, 8);
            var shapeWidth = ReadFieldInt32(ref reader, fields, 9);
            var shapeHeight = ReadFieldInt32(ref reader, fields, 10);
            var occupiedCells = ReadFieldInt32(ref reader, fields, 11);
            var hardpointType = ReadFieldString(ref reader, fields, 12);
            var hullType = ReadFieldString(ref reader, fields, 13);
            var behaviorKinds = ReadFieldStringArray(ref reader, fields, 14);
            SkipField(ref reader, fields, 15);
            var maxStack = ReadFieldInt32(ref reader, fields, 16);
            SkipField(ref reader, fields, 17);
            SkipField(ref reader, fields, 18);
            var durability = ReadFieldDouble(ref reader, fields, 19);
            var weaponRange = ReadFieldString(ref reader, fields, 20);
            var weaponCaliber = ReadFieldString(ref reader, fields, 21);
            var weaponType = ReadFieldString(ref reader, fields, 22);
            var weaponFireTypes = ReadFieldString(ref reader, fields, 23);
            var weaponModifiers = ReadFieldString(ref reader, fields, 24);
            var shapeCells = ReadFieldShapeCells(ref reader, fields, 25);
            var interiorShapeWidth = ReadFieldInt32(ref reader, fields, 26);
            var interiorShapeHeight = ReadFieldInt32(ref reader, fields, 27);
            var interiorOccupiedCells = ReadFieldInt32(ref reader, fields, 28);
            var interiorShapeCells = ReadFieldShapeCells(ref reader, fields, 29);
            var hardpoints = ReadFieldHardpoints(ref reader, fields, 30);
            var behaviorPayloads = ReadFieldBehaviorPayloads(ref reader, fields, 31);
            SkipRemaining(ref reader, fields, 32);

            return new AetheriaRuntimeCatalogItem(
                legacyId,
                name,
                category,
                description,
                manufacturerLegacyId,
                price,
                mass,
                volume,
                shapeWidth,
                shapeHeight,
                occupiedCells,
                shapeCells,
                interiorShapeWidth,
                interiorShapeHeight,
                interiorOccupiedCells,
                interiorShapeCells,
                hardpoints,
                behaviorPayloads,
                hardpointType,
                hullType,
                behaviorKinds,
                maxStack,
                durability,
                weaponRange,
                weaponCaliber,
                weaponType,
                weaponFireTypes,
                weaponModifiers);
        }

        private static AetheriaRuntimeCorporation ReadCorporation(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var legacyId = ReadFieldString(ref reader, fields, 1);
            var shortName = ReadFieldString(ref reader, fields, 2);
            var description = ReadFieldString(ref reader, fields, 3);
            var geonameFileLegacyId = ReadFieldString(ref reader, fields, 4);
            var bossHullLegacyId = ReadFieldString(ref reader, fields, 5);
            var influenceDistance = ReadFieldInt32(ref reader, fields, 6);
            var allegianceCount = ReadFieldInt32(ref reader, fields, 7);
            SkipRemaining(ref reader, fields, 8);
            return new AetheriaRuntimeCorporation(
                legacyId,
                name,
                shortName,
                description,
                geonameFileLegacyId,
                bossHullLegacyId,
                influenceDistance,
                allegianceCount);
        }

        private static AetheriaRuntimeNameFile ReadNameFile(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var legacyId = ReadFieldString(ref reader, fields, 1);
            var nameCount = ReadFieldInt32(ref reader, fields, 2);
            var sampleNames = ReadFieldStringArray(ref reader, fields, 3);
            var names = ReadFieldStringArray(ref reader, fields, 4);
            SkipRemaining(ref reader, fields, 5);
            return new AetheriaRuntimeNameFile(legacyId, name, nameCount, sampleNames, names);
        }

        private static string ReadFieldString(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? "" : ReadString(ref reader);
        }

        private static int ReadFieldInt32(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadInt32();
        }

        private static double ReadFieldDouble(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadDouble();
        }

        private static IReadOnlyList<string> ReadFieldStringArray(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<string>();
            var count = reader.ReadArrayHeader();
            var values = new string[count];
            for (var item = 0; item < count; item++)
                values[item] = ReadString(ref reader);
            return values;
        }

        private static IReadOnlyList<AetheriaRuntimeShapeCell> ReadFieldShapeCells(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? Array.Empty<AetheriaRuntimeShapeCell>() : ReadShapeCells(ref reader);
        }

        private static IReadOnlyList<AetheriaRuntimeShapeCell> ReadShapeCells(ref MessagePackReader reader)
        {
            var count = reader.ReadArrayHeader();
            var cells = new AetheriaRuntimeShapeCell[count];
            for (var cell = 0; cell < count; cell++)
            {
                var fields = reader.ReadArrayHeader();
                var x = ReadFieldInt32(ref reader, fields, 0);
                var y = ReadFieldInt32(ref reader, fields, 1);
                SkipRemaining(ref reader, fields, 2);
                cells[cell] = new AetheriaRuntimeShapeCell(x, y);
            }

            return cells;
        }

        private static IReadOnlyList<AetheriaRuntimeHardpoint> ReadFieldHardpoints(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeHardpoint>();
            var count = reader.ReadArrayHeader();
            var hardpoints = new AetheriaRuntimeHardpoint[count];
            for (var hardpoint = 0; hardpoint < count; hardpoint++)
            {
                var hardpointFields = reader.ReadArrayHeader();
                var type = ReadFieldString(ref reader, hardpointFields, 0);
                var positionX = ReadFieldInt32(ref reader, hardpointFields, 1);
                var positionY = ReadFieldInt32(ref reader, hardpointFields, 2);
                var shapeWidth = ReadFieldInt32(ref reader, hardpointFields, 3);
                var shapeHeight = ReadFieldInt32(ref reader, hardpointFields, 4);
                var occupiedCells = ReadFieldInt32(ref reader, hardpointFields, 5);
                var shapeCells = ReadFieldShapeCells(ref reader, hardpointFields, 6);
                var transform = ReadFieldString(ref reader, hardpointFields, 7);
                var rotation = ReadFieldString(ref reader, hardpointFields, 8);
                var armor = ReadFieldDouble(ref reader, hardpointFields, 9);
                SkipRemaining(ref reader, hardpointFields, 10);
                hardpoints[hardpoint] = new AetheriaRuntimeHardpoint(
                    type,
                    positionX,
                    positionY,
                    shapeWidth,
                    shapeHeight,
                    occupiedCells,
                    shapeCells,
                    transform,
                    rotation,
                    armor);
            }

            return hardpoints;
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorPayload> ReadFieldBehaviorPayloads(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBehaviorPayload>();
            var count = reader.ReadArrayHeader();
            var payloads = new AetheriaRuntimeBehaviorPayload[count];
            for (var payload = 0; payload < count; payload++)
            {
                var payloadFields = reader.ReadArrayHeader();
                var unionKey = ReadFieldInt32(ref reader, payloadFields, 0);
                var kind = ReadFieldString(ref reader, payloadFields, 1);
                var group = ReadFieldInt32(ref reader, payloadFields, 2);
                var fieldsValue = ReadFieldBehaviorFields(ref reader, payloadFields, 3);
                SkipRemaining(ref reader, payloadFields, 4);
                payloads[payload] = new AetheriaRuntimeBehaviorPayload(unionKey, kind, group, fieldsValue);
            }

            return payloads;
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorField> ReadFieldBehaviorFields(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBehaviorField>();
            var count = reader.ReadArrayHeader();
            var values = new AetheriaRuntimeBehaviorField[count];
            for (var field = 0; field < count; field++)
            {
                var fieldCount = reader.ReadArrayHeader();
                var key = ReadFieldInt32(ref reader, fieldCount, 0);
                var value = ReadFieldBehaviorValue(ref reader, fieldCount, 1);
                SkipRemaining(ref reader, fieldCount, 2);
                values[field] = new AetheriaRuntimeBehaviorField(key, value);
            }

            return values;
        }

        private static AetheriaRuntimeBehaviorValue ReadFieldBehaviorValue(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields)
                return EmptyBehaviorValue();

            var valueFields = reader.ReadArrayHeader();
            var kind = ReadFieldString(ref reader, valueFields, 0);
            var stringValue = ReadFieldString(ref reader, valueFields, 1);
            var numberValue = ReadFieldDouble(ref reader, valueFields, 2);
            var boolValue = valueFields > 3 && reader.ReadBoolean();
            var legacyIdValue = ReadFieldString(ref reader, valueFields, 4);
            var children = ReadFieldBehaviorValues(ref reader, valueFields, 5);
            var mapEntries = ReadFieldBehaviorMapEntries(ref reader, valueFields, 6);
            SkipRemaining(ref reader, valueFields, 7);
            return new AetheriaRuntimeBehaviorValue(kind, stringValue, numberValue, boolValue, legacyIdValue, children, mapEntries);
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorValue> ReadFieldBehaviorValues(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBehaviorValue>();
            var count = reader.ReadArrayHeader();
            var values = new AetheriaRuntimeBehaviorValue[count];
            for (var value = 0; value < count; value++)
                values[value] = ReadFieldBehaviorValue(ref reader, 1, 0);
            return values;
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> ReadFieldBehaviorMapEntries(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBehaviorMapEntry>();
            var count = reader.ReadArrayHeader();
            var entries = new AetheriaRuntimeBehaviorMapEntry[count];
            for (var entry = 0; entry < count; entry++)
            {
                var entryFields = reader.ReadArrayHeader();
                var key = ReadFieldString(ref reader, entryFields, 0);
                var value = ReadFieldBehaviorValue(ref reader, entryFields, 1);
                SkipRemaining(ref reader, entryFields, 2);
                entries[entry] = new AetheriaRuntimeBehaviorMapEntry(key, value);
            }

            return entries;
        }

        private static AetheriaRuntimeBehaviorValue EmptyBehaviorValue()
        {
            return new AetheriaRuntimeBehaviorValue(
                "nil",
                "",
                0,
                false,
                "",
                Array.Empty<AetheriaRuntimeBehaviorValue>(),
                Array.Empty<AetheriaRuntimeBehaviorMapEntry>());
        }

        private static string ReadString(ref MessagePackReader reader)
        {
            return reader.ReadString() ?? "";
        }

        private static void SkipField(ref MessagePackReader reader, int fields, int index)
        {
            if (index < fields)
                reader.Skip();
        }

        private static void SkipRemaining(ref MessagePackReader reader, int fields, int firstUnhandledIndex)
        {
            for (var field = firstUnhandledIndex; field < fields; field++)
                reader.Skip();
        }

        private readonly struct PersistedRecord
        {
            public PersistedRecord(string schemaId, byte[] payload)
            {
                SchemaId = schemaId;
                Payload = payload;
            }

            public string SchemaId { get; }

            public byte[] Payload { get; }
        }
    }
}
