using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Eve.Surface;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeCatalogStore
    {
        private const string ItemDefinitionSchema = "aetheria.item_definition";
        private const string CorporationSchema = "aetheria.corporation";
        private const string NameFileSchema = "aetheria.name_file";
        private const string EveSurfaceSchema = "gamecult.eve.surface";
        private const string PlayerSettingsSchema = "aetheria.player_settings";
        private const string PlayerSettingsKey = "global:aetheria.player_settings.v1";

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

        public static IReadOnlyList<EveSurfaceDocument> ReadEveSurfaces(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            var surfaces = new List<EveSurfaceDocument>();
            foreach (var record in ReadRecords(stateFilePath))
            {
                if (!catalog.TryGetValue(record.SchemaId, out var schemaName) || schemaName != EveSurfaceSchema)
                    continue;

                surfaces.Add(ReadEveSurface(record.Payload));
            }

            return surfaces;
        }

        public static AetheriaRuntimePlayerSettingsSnapshot? ReadPlayerSettings(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            AetheriaRuntimePlayerSettingsSnapshot? settings = null;
            foreach (var record in ReadRecords(stateFilePath))
            {
                if (record.Key != PlayerSettingsKey ||
                    !catalog.TryGetValue(record.SchemaId, out var schemaName) ||
                    schemaName != PlayerSettingsSchema)
                    continue;

                settings = ReadPlayerSettingsPayload(record.Payload);
            }

            return settings;
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
            var key = fieldCount > 0 ? ReadString(ref reader) : "";
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
            return new PersistedRecord(key, schemaId, payload);
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
            var minimumTemperature = ReadFieldDouble(ref reader, fields, 32);
            var maximumTemperature = ReadFieldDouble(ref reader, fields, 33);
            var thermalPerformanceCurveKeys = ReadFieldCurveKeys(ref reader, fields, 34);
            var hullPrefab = ReadFieldString(ref reader, fields, 35);
            var simpleCommodityCategory = ReadFieldString(ref reader, fields, 36);
            var compoundCommodityCategory = ReadFieldString(ref reader, fields, 37);
            var specificHeat = ReadFieldDouble(ref reader, fields, 38, 1);
            var conductivity = ReadFieldDouble(ref reader, fields, 39, 1);
            var hullGridOffset = ReadFieldDouble(ref reader, fields, 40);
            var hullArmor = ReadFieldDouble(ref reader, fields, 41);
            var hullDrag = ReadFieldDouble(ref reader, fields, 42);
            var hullCanTow = ReadFieldBool(ref reader, fields, 43);
            var dockingMaxSizeX = ReadFieldInt32(ref reader, fields, 44);
            var dockingMaxSizeY = ReadFieldInt32(ref reader, fields, 45);
            var actionBarIcon = ReadFieldString(ref reader, fields, 46);
            SkipRemaining(ref reader, fields, 47);

            return new AetheriaRuntimeCatalogItem(
                legacyId,
                name,
                category,
                description,
                manufacturerLegacyId,
                price,
                mass,
                specificHeat,
                conductivity,
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
                weaponModifiers,
                minimumTemperature,
                maximumTemperature,
                thermalPerformanceCurveKeys,
                hullPrefab,
                hullGridOffset,
                hullArmor,
                hullDrag,
                hullCanTow,
                dockingMaxSizeX,
                dockingMaxSizeY,
                actionBarIcon,
                simpleCommodityCategory,
                compoundCommodityCategory);
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
            SkipFields(ref reader, fields, 8, 11);
            var allegiances = ReadFieldCorporationAllegiances(ref reader, fields, 11);
            SkipRemaining(ref reader, fields, 12);
            return new AetheriaRuntimeCorporation(
                legacyId,
                name,
                shortName,
                description,
                geonameFileLegacyId,
                bossHullLegacyId,
                influenceDistance,
                allegianceCount,
                allegiances);
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

        private static EveSurfaceDocument ReadEveSurface(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var type = ReadFieldString(ref reader, fields, 0);
            var schema = ReadFieldString(ref reader, fields, 1);
            var providerId = ReadFieldString(ref reader, fields, 2);
            var providerKind = ReadFieldString(ref reader, fields, 3);
            var title = ReadFieldString(ref reader, fields, 4);
            var version = ReadFieldInt64(ref reader, fields, 5);
            var updatedAtUtc = ReadFieldString(ref reader, fields, 6);
            var surface = ReadFieldEveSurfaceTree(ref reader, fields, 7);
            var commands = ReadFieldEveCommandTemplates(ref reader, fields, 8);
            SkipRemaining(ref reader, fields, 9);
            return new EveSurfaceDocument(type, schema, providerId, providerKind, title, version, updatedAtUtc, surface, commands);
        }

        private static AetheriaRuntimePlayerSettingsSnapshot ReadPlayerSettingsPayload(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            SkipFields(ref reader, fields, 0, 3);
            var playerName = ReadFieldString(ref reader, fields, 3);
            var tutorialPassed = ReadFieldBool(ref reader, fields, 4);
            var storyFileHashes = ReadFieldStoryFileHashes(ref reader, fields, 5);
            var gameplay = ReadFieldPlayerGameplaySettings(ref reader, fields, 6);
            var graphics = ReadFieldPlayerGraphicsSettings(ref reader, fields, 7);
            var input = ReadFieldPlayerInputSettings(ref reader, fields, 8);
            SkipRemaining(ref reader, fields, 9);
            return new AetheriaRuntimePlayerSettingsSnapshot(
                playerName,
                tutorialPassed,
                storyFileHashes,
                gameplay.TemperatureUnit,
                gameplay.SignificantDigits,
                graphics.NebulaQuality,
                graphics.ShowAsteroidsInMinimap,
                input.BindingOverrides,
                input.ActionBarInputs);
        }

        private static string ReadFieldString(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? "" : ReadString(ref reader);
        }

        private static int ReadFieldInt32(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadInt32();
        }

        private static bool ReadFieldBool(ref MessagePackReader reader, int fields, int index)
        {
            return index < fields && reader.ReadBoolean();
        }

        private static long ReadFieldInt64(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadInt64();
        }

        private static double ReadFieldDouble(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadDouble();
        }

        private static double ReadFieldDouble(ref MessagePackReader reader, int fields, int index, double fallback)
        {
            return index >= fields ? fallback : reader.ReadDouble();
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

        private static IReadOnlyList<AetheriaRuntimeStoryFileHash> ReadFieldStoryFileHashes(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeStoryFileHash>();
            var count = reader.ReadArrayHeader();
            var hashes = new AetheriaRuntimeStoryFileHash[count];
            for (var hash = 0; hash < count; hash++)
            {
                var hashFields = reader.ReadArrayHeader();
                var storyPath = ReadFieldString(ref reader, hashFields, 0);
                var value = ReadFieldString(ref reader, hashFields, 1);
                SkipRemaining(ref reader, hashFields, 2);
                hashes[hash] = new AetheriaRuntimeStoryFileHash(storyPath, value);
            }

            return hashes;
        }

        private static PlayerGameplaySettings ReadFieldPlayerGameplaySettings(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new PlayerGameplaySettings("Celsius", 3);
            var gameplayFields = reader.ReadArrayHeader();
            var temperatureUnit = ReadFieldString(ref reader, gameplayFields, 0);
            var significantDigits = ReadFieldInt32(ref reader, gameplayFields, 1);
            SkipRemaining(ref reader, gameplayFields, 2);
            return new PlayerGameplaySettings(temperatureUnit, significantDigits);
        }

        private static PlayerGraphicsSettings ReadFieldPlayerGraphicsSettings(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new PlayerGraphicsSettings("Normal", false);
            var graphicsFields = reader.ReadArrayHeader();
            var nebulaQuality = ReadFieldString(ref reader, graphicsFields, 0);
            var showAsteroidsInMinimap = ReadFieldBool(ref reader, graphicsFields, 1);
            SkipRemaining(ref reader, graphicsFields, 2);
            return new PlayerGraphicsSettings(nebulaQuality, showAsteroidsInMinimap);
        }

        private static PlayerInputSettings ReadFieldPlayerInputSettings(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new PlayerInputSettings(Array.Empty<AetheriaRuntimeInputBindingOverride>(), Array.Empty<string>());
            var inputFields = reader.ReadArrayHeader();
            var bindings = ReadFieldInputBindings(ref reader, inputFields, 0);
            var actionBarInputs = ReadFieldStringArray(ref reader, inputFields, 1);
            SkipRemaining(ref reader, inputFields, 2);
            return new PlayerInputSettings(bindings, actionBarInputs);
        }

        private static IReadOnlyList<AetheriaRuntimeInputBindingOverride> ReadFieldInputBindings(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeInputBindingOverride>();
            var count = reader.ReadArrayHeader();
            var bindings = new AetheriaRuntimeInputBindingOverride[count];
            for (var binding = 0; binding < count; binding++)
            {
                var bindingFields = reader.ReadArrayHeader();
                var actionName = ReadFieldString(ref reader, bindingFields, 0);
                var bindingIndex = ReadFieldInt32(ref reader, bindingFields, 1);
                var bindingPath = ReadFieldString(ref reader, bindingFields, 2);
                SkipRemaining(ref reader, bindingFields, 3);
                bindings[binding] = new AetheriaRuntimeInputBindingOverride(actionName, bindingIndex, bindingPath);
            }

            return bindings;
        }

        private static IReadOnlyList<AetheriaRuntimeShapeCell> ReadFieldShapeCells(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? Array.Empty<AetheriaRuntimeShapeCell>() : ReadShapeCells(ref reader);
        }

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadFieldCurveKeys(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeCurveKey>();
            var count = reader.ReadArrayHeader();
            var keys = new AetheriaRuntimeCurveKey[count];
            for (var key = 0; key < count; key++)
            {
                var keyFields = reader.ReadArrayHeader();
                var time = ReadFieldDouble(ref reader, keyFields, 0);
                var value = ReadFieldDouble(ref reader, keyFields, 1);
                var inTangent = ReadFieldDouble(ref reader, keyFields, 2);
                var outTangent = ReadFieldDouble(ref reader, keyFields, 3);
                SkipRemaining(ref reader, keyFields, 4);
                keys[key] = new AetheriaRuntimeCurveKey(time, value, inTangent, outTangent);
            }

            return keys;
        }

        private static IReadOnlyList<AetheriaRuntimeCorporationAllegiance> ReadFieldCorporationAllegiances(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeCorporationAllegiance>();
            var count = reader.ReadArrayHeader();
            var allegiances = new AetheriaRuntimeCorporationAllegiance[count];
            for (var allegiance = 0; allegiance < count; allegiance++)
            {
                var allegianceFields = reader.ReadArrayHeader();
                var corporationLegacyId = ReadFieldString(ref reader, allegianceFields, 0);
                var weight = ReadFieldDouble(ref reader, allegianceFields, 1);
                SkipRemaining(ref reader, allegianceFields, 2);
                allegiances[allegiance] = new AetheriaRuntimeCorporationAllegiance(corporationLegacyId, weight);
            }

            return allegiances;
        }

        private static EveSurfaceTree ReadFieldEveSurfaceTree(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields)
                return new EveSurfaceTree("", EmptyEveComponent(), Array.Empty<EveStyleToken>());

            var surfaceFields = reader.ReadArrayHeader();
            var id = ReadFieldString(ref reader, surfaceFields, 0);
            var root = ReadFieldEveComponent(ref reader, surfaceFields, 1);
            var styles = ReadFieldEveStyleTokens(ref reader, surfaceFields, 2);
            SkipRemaining(ref reader, surfaceFields, 3);
            return new EveSurfaceTree(id, root, styles);
        }

        private static EveSurfaceComponent ReadFieldEveComponent(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields)
                return EmptyEveComponent();

            var componentFields = reader.ReadArrayHeader();
            var id = ReadFieldString(ref reader, componentFields, 0);
            var kind = ReadFieldString(ref reader, componentFields, 1);
            var props = ReadFieldStringMap(ref reader, componentFields, 2);
            var children = ReadFieldEveComponents(ref reader, componentFields, 3);
            SkipRemaining(ref reader, componentFields, 4);
            return new EveSurfaceComponent(id, kind, props, children);
        }

        private static IReadOnlyList<EveSurfaceComponent> ReadFieldEveComponents(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<EveSurfaceComponent>();
            var count = reader.ReadArrayHeader();
            var components = new EveSurfaceComponent[count];
            for (var component = 0; component < count; component++)
                components[component] = ReadFieldEveComponent(ref reader, 1, 0);
            return components;
        }

        private static IReadOnlyDictionary<string, string> ReadFieldStringMap(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return EmptyStringMap();
            var count = reader.ReadMapHeader();
            if (count == 0) return EmptyStringMap();
            var map = new Dictionary<string, string>(count, StringComparer.Ordinal);
            for (var entry = 0; entry < count; entry++)
            {
                var key = ReadString(ref reader);
                var value = ReadString(ref reader);
                if (!string.IsNullOrWhiteSpace(key))
                    map[key] = value;
            }

            return map;
        }

        private static IReadOnlyList<EveStyleToken> ReadFieldEveStyleTokens(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<EveStyleToken>();
            var count = reader.ReadArrayHeader();
            var tokens = new EveStyleToken[count];
            for (var token = 0; token < count; token++)
            {
                var tokenFields = reader.ReadArrayHeader();
                var name = ReadFieldString(ref reader, tokenFields, 0);
                var value = ReadFieldString(ref reader, tokenFields, 1);
                SkipRemaining(ref reader, tokenFields, 2);
                tokens[token] = new EveStyleToken(name, value);
            }

            return tokens;
        }

        private static IReadOnlyList<EveCommandTemplate> ReadFieldEveCommandTemplates(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<EveCommandTemplate>();
            var count = reader.ReadArrayHeader();
            var commands = new EveCommandTemplate[count];
            for (var command = 0; command < count; command++)
            {
                var commandFields = reader.ReadArrayHeader();
                var name = ReadFieldString(ref reader, commandFields, 0);
                var label = ReadFieldString(ref reader, commandFields, 1);
                var transport = ReadFieldString(ref reader, commandFields, 2);
                SkipRemaining(ref reader, commandFields, 3);
                commands[command] = new EveCommandTemplate(name, label, transport);
            }

            return commands;
        }

        private static EveSurfaceComponent EmptyEveComponent()
        {
            return new EveSurfaceComponent("", "", EmptyStringMap(), Array.Empty<EveSurfaceComponent>());
        }

        private static IReadOnlyDictionary<string, string> EmptyStringMap()
        {
            return new Dictionary<string, string>(0, StringComparer.Ordinal);
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

        private static void SkipFields(ref MessagePackReader reader, int fields, int firstIndex, int stopBeforeIndex)
        {
            for (var field = firstIndex; field < fields && field < stopBeforeIndex; field++)
                reader.Skip();
        }

        private static void SkipRemaining(ref MessagePackReader reader, int fields, int firstUnhandledIndex)
        {
            for (var field = firstUnhandledIndex; field < fields; field++)
                reader.Skip();
        }

        private readonly struct PersistedRecord
        {
            public PersistedRecord(string key, string schemaId, byte[] payload)
            {
                Key = key;
                SchemaId = schemaId;
                Payload = payload;
            }

            public string Key { get; }

            public string SchemaId { get; }

            public byte[] Payload { get; }
        }

        private readonly struct PlayerGameplaySettings
        {
            public PlayerGameplaySettings(string temperatureUnit, int significantDigits)
            {
                TemperatureUnit = temperatureUnit;
                SignificantDigits = significantDigits;
            }

            public string TemperatureUnit { get; }

            public int SignificantDigits { get; }
        }

        private readonly struct PlayerGraphicsSettings
        {
            public PlayerGraphicsSettings(string nebulaQuality, bool showAsteroidsInMinimap)
            {
                NebulaQuality = nebulaQuality;
                ShowAsteroidsInMinimap = showAsteroidsInMinimap;
            }

            public string NebulaQuality { get; }

            public bool ShowAsteroidsInMinimap { get; }
        }

        private readonly struct PlayerInputSettings
        {
            public PlayerInputSettings(
                IReadOnlyList<AetheriaRuntimeInputBindingOverride> bindingOverrides,
                IReadOnlyList<string> actionBarInputs)
            {
                BindingOverrides = bindingOverrides;
                ActionBarInputs = actionBarInputs;
            }

            public IReadOnlyList<AetheriaRuntimeInputBindingOverride> BindingOverrides { get; }

            public IReadOnlyList<string> ActionBarInputs { get; }
        }
    }
}
