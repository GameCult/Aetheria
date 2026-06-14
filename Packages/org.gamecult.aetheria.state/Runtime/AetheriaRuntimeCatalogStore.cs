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
        private const string LoadoutTemplateSchema = "aetheria.loadout_template";
        private const string RunStateSchema = "aetheria.run_state";
        private const string ZoneStateSchema = "aetheria.zone_state";
        private const string EntitySnapshotSchema = "aetheria.entity_snapshot";
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

        public static IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot> ReadLoadoutTemplates(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            var loadouts = new List<AetheriaRuntimeLoadoutTemplateSnapshot>();
            foreach (var record in ReadRecords(stateFilePath))
            {
                if (!catalog.TryGetValue(record.SchemaId, out var schemaName) ||
                    schemaName != LoadoutTemplateSchema)
                    continue;

                loadouts.Add(ReadLoadoutTemplatePayload(record.Payload));
            }

            return loadouts
                .OrderBy(loadout => loadout.Name, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<AetheriaRuntimeRunStateSnapshot> ReadRunStates(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            var runs = new List<AetheriaRuntimeRunStateSnapshot>();
            foreach (var record in ReadRecords(stateFilePath))
            {
                if (!catalog.TryGetValue(record.SchemaId, out var schemaName) ||
                    schemaName != RunStateSchema)
                    continue;

                runs.Add(ReadRunStatePayload(record.Payload));
            }

            return runs.OrderBy(run => run.RunId, StringComparer.Ordinal).ToArray();
        }

        public static IReadOnlyList<AetheriaRuntimeZoneStateSnapshot> ReadZoneStates(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            var zones = new List<AetheriaRuntimeZoneStateSnapshot>();
            foreach (var record in ReadRecords(stateFilePath))
            {
                if (!catalog.TryGetValue(record.SchemaId, out var schemaName) ||
                    schemaName != ZoneStateSchema)
                    continue;

                zones.Add(ReadZoneStatePayload(record.Payload));
            }

            return zones.OrderBy(zone => zone.Name, StringComparer.Ordinal).ToArray();
        }

        public static IReadOnlyList<AetheriaRuntimeEntitySnapshot> ReadEntitySnapshots(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var catalog = ReadSchemaCatalog(stateFilePath);
            var entities = new List<AetheriaRuntimeEntitySnapshot>();
            foreach (var record in ReadRecords(stateFilePath))
            {
                if (!catalog.TryGetValue(record.SchemaId, out var schemaName) ||
                    schemaName != EntitySnapshotSchema)
                    continue;

                entities.Add(ReadEntitySnapshotPayload(record.Payload));
            }

            return entities.OrderBy(entity => entity.Name, StringComparer.Ordinal).ToArray();
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
            var stackable = ReadFieldBool(ref reader, fields, 17);
            var duration = ReadFieldDouble(ref reader, fields, 18);
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
            var thermalResilience = ReadFieldDouble(ref reader, fields, 47, 1);
            var audioStats = ReadFieldAudioStats(ref reader, fields, 48);
            var effectivenessCurveKeys = ReadFieldCurveKeys(ref reader, fields, 49);
            SkipRemaining(ref reader, fields, 50);

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
                stackable,
                duration,
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
                thermalResilience,
                hullGridOffset,
                hullArmor,
                hullDrag,
                hullCanTow,
                dockingMaxSizeX,
                dockingMaxSizeY,
                actionBarIcon,
                audioStats,
                effectivenessCurveKeys,
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

        private static AetheriaRuntimeLoadoutTemplateSnapshot ReadLoadoutTemplatePayload(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var ownerPlayerKey = ReadFieldString(ref reader, fields, 1);
            var rootEntity = ReadFieldEntityLoadout(ref reader, fields, 2);
            var createdAtUtc = ReadFieldString(ref reader, fields, 3);
            var updatedAtUtc = ReadFieldString(ref reader, fields, 4);
            SkipRemaining(ref reader, fields, 5);
            return new AetheriaRuntimeLoadoutTemplateSnapshot(
                name,
                ownerPlayerKey,
                rootEntity,
                createdAtUtc,
                updatedAtUtc);
        }

        private static AetheriaRuntimeRunStateSnapshot ReadRunStatePayload(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var runId = ReadFieldString(ref reader, fields, 0);
            var isTutorial = ReadFieldBool(ref reader, fields, 1);
            var entranceZoneIndex = ReadFieldInt32(ref reader, fields, 2);
            var exitZoneIndex = ReadFieldInt32(ref reader, fields, 3);
            var currentZoneIndex = ReadFieldInt32(ref reader, fields, 4);
            var currentZoneEntityIndex = ReadFieldInt32(ref reader, fields, 5);
            var discoveredZoneIndices = ReadFieldInt32Array(ref reader, fields, 6);
            var zoneKeys = ReadFieldStringArray(ref reader, fields, 7);
            var actionBarBindings = ReadFieldActionBarBindings(ref reader, fields, 8);
            var factionRelationships = ReadFieldFactionRelationships(ref reader, fields, 9);
            var updatedAtUtc = ReadFieldString(ref reader, fields, 10);
            var generationSeed = ReadFieldUInt32(ref reader, fields, 11);
            SkipRemaining(ref reader, fields, 12);
            return new AetheriaRuntimeRunStateSnapshot(
                runId,
                isTutorial,
                entranceZoneIndex,
                exitZoneIndex,
                currentZoneIndex,
                currentZoneEntityIndex,
                discoveredZoneIndices,
                zoneKeys,
                actionBarBindings,
                factionRelationships,
                updatedAtUtc,
                generationSeed);
        }

        private static AetheriaRuntimeZoneStateSnapshot ReadZoneStatePayload(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var position = ReadFieldVector2(ref reader, fields, 1);
            var adjacentZoneIndices = ReadFieldInt32Array(ref reader, fields, 2);
            var factionIndices = ReadFieldInt32Array(ref reader, fields, 3);
            var ownerFactionIndex = ReadFieldInt32(ref reader, fields, 4);
            var entityKeys = ReadFieldStringArray(ref reader, fields, 5);
            var orbits = ReadFieldOrbitSnapshots(ref reader, fields, 6);
            var bodies = ReadFieldBodySnapshots(ref reader, fields, 7);
            SkipRemaining(ref reader, fields, 8);
            return new AetheriaRuntimeZoneStateSnapshot(
                name,
                position.X,
                position.Y,
                adjacentZoneIndices,
                factionIndices,
                ownerFactionIndex,
                entityKeys,
                orbits,
                bodies);
        }

        private static AetheriaRuntimeEntitySnapshot ReadEntitySnapshotPayload(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var kind = ReadFieldString(ref reader, fields, 1);
            var position = ReadFieldVector3(ref reader, fields, 2);
            var direction = ReadFieldVector2(ref reader, fields, 3);
            var factionKey = ReadFieldString(ref reader, fields, 4);
            var hullItemKey = ReadFieldString(ref reader, fields, 5);
            var equipment = ReadFieldEntityItemSlots(ref reader, fields, 6);
            var cargoBays = ReadFieldEntityItemSlots(ref reader, fields, 7);
            var dockingBays = ReadFieldEntityItemSlots(ref reader, fields, 8);
            var childEntityKeys = ReadFieldStringArray(ref reader, fields, 9);
            var weaponGroups = ReadFieldEntityWeaponGroups(ref reader, fields, 10);
            var statGrids = ReadFieldEntityStatGrids(ref reader, fields, 11);
            var velocity = ReadFieldVector2(ref reader, fields, 12);
            var targetEntityKey = ReadFieldString(ref reader, fields, 13);
            var isActive = ReadFieldBool(ref reader, fields, 14);
            var heatsinksEnabled = ReadFieldBool(ref reader, fields, 15);
            var overrideShutdown = ReadFieldBool(ref reader, fields, 16);
            var tractorPower = ReadFieldDouble(ref reader, fields, 17);
            var heatstroke = ReadFieldDouble(ref reader, fields, 18);
            var hypothermia = ReadFieldDouble(ref reader, fields, 19);
            var activeConsumables = ReadFieldActiveConsumables(ref reader, fields, 20);
            var behaviorProgress = ReadFieldBehaviorProgress(ref reader, fields, 21);
            var weaponStates = ReadFieldWeaponStates(ref reader, fields, 22);
            var behaviorStates = ReadFieldBehaviorStates(ref reader, fields, 23);
            var cargoContents = ReadFieldCargoBayLoadouts(ref reader, fields, 24);
            var dockingBayContents = ReadFieldCargoBayLoadouts(ref reader, fields, 25);
            var dockingBayAssignments = ReadFieldInt32Array(ref reader, fields, 26);
            var visibility = ReadFieldDouble(ref reader, fields, 27);
            var visibilitySourceCount = ReadFieldInt32(ref reader, fields, 28);
            var contacts = ReadFieldEntityContacts(ref reader, fields, 29);
            SkipRemaining(ref reader, fields, 30);
            return new AetheriaRuntimeEntitySnapshot(
                name,
                kind,
                position.X,
                position.Y,
                position.Z,
                direction.X,
                direction.Y,
                factionKey,
                hullItemKey,
                equipment,
                cargoBays,
                dockingBays,
                childEntityKeys,
                weaponGroups,
                statGrids,
                velocity.X,
                velocity.Y,
                targetEntityKey,
                isActive,
                heatsinksEnabled,
                overrideShutdown,
                tractorPower,
                heatstroke,
                hypothermia,
                activeConsumables,
                behaviorProgress,
                weaponStates,
                behaviorStates,
                cargoContents,
                dockingBayContents,
                dockingBayAssignments,
                visibility,
                visibilitySourceCount,
                contacts);
        }

        private static IReadOnlyList<AetheriaRuntimeEntityContactSnapshot> ReadFieldEntityContacts(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeEntityContactSnapshot>();
            var count = reader.ReadArrayHeader();
            var contacts = new AetheriaRuntimeEntityContactSnapshot[count];
            for (var contact = 0; contact < count; contact++)
            {
                var contactFields = reader.ReadArrayHeader();
                var targetEntityKey = ReadFieldString(ref reader, contactFields, 0);
                var infoGathered = ReadFieldDouble(ref reader, contactFields, 1);
                var hostile = ReadFieldBool(ref reader, contactFields, 2);
                var visible = ReadFieldBool(ref reader, contactFields, 3);
                SkipRemaining(ref reader, contactFields, 4);
                contacts[contact] = new AetheriaRuntimeEntityContactSnapshot(targetEntityKey, infoGathered, hostile, visible);
            }

            return contacts;
        }

        private static Vector2Value ReadFieldVector2(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new Vector2Value(0, 0);
            var vectorFields = reader.ReadArrayHeader();
            var x = ReadFieldDouble(ref reader, vectorFields, 0);
            var y = ReadFieldDouble(ref reader, vectorFields, 1);
            SkipRemaining(ref reader, vectorFields, 2);
            return new Vector2Value(x, y);
        }

        private static Vector3Value ReadFieldVector3(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new Vector3Value(0, 0, 0);
            var vectorFields = reader.ReadArrayHeader();
            var x = ReadFieldDouble(ref reader, vectorFields, 0);
            var y = ReadFieldDouble(ref reader, vectorFields, 1);
            var z = ReadFieldDouble(ref reader, vectorFields, 2);
            SkipRemaining(ref reader, vectorFields, 3);
            return new Vector3Value(x, y, z);
        }

        private static IReadOnlyList<AetheriaRuntimeActionBarBindingSnapshot> ReadFieldActionBarBindings(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeActionBarBindingSnapshot>();
            var count = reader.ReadArrayHeader();
            var bindings = new AetheriaRuntimeActionBarBindingSnapshot[count];
            for (var binding = 0; binding < count; binding++)
            {
                var bindingFields = reader.ReadArrayHeader();
                var controlPath = ReadFieldString(ref reader, bindingFields, 0);
                var kind = ReadFieldString(ref reader, bindingFields, 1);
                var targetKey = ReadFieldString(ref reader, bindingFields, 2);
                var equipmentIndex = ReadFieldInt32(ref reader, bindingFields, 3);
                var behaviorIndex = ReadFieldInt32(ref reader, bindingFields, 4);
                var weaponGroup = ReadFieldInt32(ref reader, bindingFields, 5);
                SkipRemaining(ref reader, bindingFields, 6);
                bindings[binding] = new AetheriaRuntimeActionBarBindingSnapshot(
                    controlPath,
                    kind,
                    targetKey,
                    equipmentIndex,
                    behaviorIndex,
                    weaponGroup);
            }

            return bindings;
        }

        private static IReadOnlyList<AetheriaRuntimeFactionRelationshipSnapshot> ReadFieldFactionRelationships(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeFactionRelationshipSnapshot>();
            var count = reader.ReadArrayHeader();
            var relationships = new AetheriaRuntimeFactionRelationshipSnapshot[count];
            for (var relationshipIndex = 0; relationshipIndex < count; relationshipIndex++)
            {
                var relationshipFields = reader.ReadArrayHeader();
                var factionKey = ReadFieldString(ref reader, relationshipFields, 0);
                var relationship = ReadFieldString(ref reader, relationshipFields, 1);
                var standing = ReadFieldDouble(ref reader, relationshipFields, 2);
                SkipRemaining(ref reader, relationshipFields, 3);
                relationships[relationshipIndex] = new AetheriaRuntimeFactionRelationshipSnapshot(factionKey, relationship, standing);
            }

            return relationships;
        }

        private static IReadOnlyList<AetheriaRuntimeOrbitSnapshot> ReadFieldOrbitSnapshots(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeOrbitSnapshot>();
            var count = reader.ReadArrayHeader();
            var orbits = new AetheriaRuntimeOrbitSnapshot[count];
            for (var orbit = 0; orbit < count; orbit++)
            {
                var orbitFields = reader.ReadArrayHeader();
                var orbitId = ReadFieldString(ref reader, orbitFields, 0);
                var parentId = ReadFieldString(ref reader, orbitFields, 1);
                var distance = ReadFieldDouble(ref reader, orbitFields, 2);
                var phase = ReadFieldDouble(ref reader, orbitFields, 3);
                var fixedPosition = ReadFieldVector2(ref reader, orbitFields, 4);
                SkipRemaining(ref reader, orbitFields, 5);
                orbits[orbit] = new AetheriaRuntimeOrbitSnapshot(
                    orbitId,
                    parentId,
                    distance,
                    phase,
                    fixedPosition.X,
                    fixedPosition.Y);
            }

            return orbits;
        }

        private static IReadOnlyList<AetheriaRuntimeBodySnapshot> ReadFieldBodySnapshots(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBodySnapshot>();
            var count = reader.ReadArrayHeader();
            var bodies = new AetheriaRuntimeBodySnapshot[count];
            for (var body = 0; body < count; body++)
            {
                var bodyFields = reader.ReadArrayHeader();
                var bodyId = ReadFieldString(ref reader, bodyFields, 0);
                var kind = ReadFieldString(ref reader, bodyFields, 1);
                var name = ReadFieldString(ref reader, bodyFields, 2);
                var orbitId = ReadFieldString(ref reader, bodyFields, 3);
                var mass = ReadFieldDouble(ref reader, bodyFields, 4);
                var resourceCount = CountAndSkipFieldArray(ref reader, bodyFields, 5);
                SkipFields(ref reader, bodyFields, 6, 10);
                var asteroidSummary = ReadFieldAsteroidSummary(ref reader, bodyFields, 10);
                SkipRemaining(ref reader, bodyFields, 11);
                bodies[body] = new AetheriaRuntimeBodySnapshot(
                    bodyId,
                    kind,
                    name,
                    orbitId,
                    mass,
                    resourceCount,
                    asteroidSummary.Count,
                    asteroidSummary.DamagedCount,
                    asteroidSummary.RespawningCount,
                    asteroidSummary.MiningAccumulatorCount);
            }

            return bodies;
        }

        private static AsteroidSummary ReadFieldAsteroidSummary(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new AsteroidSummary(0, 0, 0, 0);
            var count = reader.ReadArrayHeader();
            var damagedCount = 0;
            var respawningCount = 0;
            var miningAccumulatorCount = 0;
            for (var asteroid = 0; asteroid < count; asteroid++)
            {
                var asteroidFields = reader.ReadArrayHeader();
                SkipFields(ref reader, asteroidFields, 0, 4);
                if (ReadFieldDouble(ref reader, asteroidFields, 4) > 0) damagedCount++;
                if (ReadFieldDouble(ref reader, asteroidFields, 5) > 0) respawningCount++;
                miningAccumulatorCount += CountAndSkipFieldArray(ref reader, asteroidFields, 6);
                SkipRemaining(ref reader, asteroidFields, 7);
            }

            return new AsteroidSummary(count, damagedCount, respawningCount, miningAccumulatorCount);
        }

        private readonly struct AsteroidSummary
        {
            public AsteroidSummary(int count, int damagedCount, int respawningCount, int miningAccumulatorCount)
            {
                Count = count;
                DamagedCount = damagedCount;
                RespawningCount = respawningCount;
                MiningAccumulatorCount = miningAccumulatorCount;
            }

            public int Count { get; }
            public int DamagedCount { get; }
            public int RespawningCount { get; }
            public int MiningAccumulatorCount { get; }
        }

        private static IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> ReadFieldEntityItemSlots(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeEntityItemSlotSnapshot>();
            var count = reader.ReadArrayHeader();
            var slots = new AetheriaRuntimeEntityItemSlotSnapshot[count];
            for (var slot = 0; slot < count; slot++)
            {
                var slotFields = reader.ReadArrayHeader();
                var position = ReadFieldGridCoord(ref reader, slotFields, 0);
                var itemKey = ReadFieldString(ref reader, slotFields, 1);
                var quality = ReadFieldDouble(ref reader, slotFields, 2, 1);
                var durability = ReadFieldDouble(ref reader, slotFields, 3, 1);
                var quantity = ReadFieldInt32(ref reader, slotFields, 4);
                SkipRemaining(ref reader, slotFields, 5);
                slots[slot] = new AetheriaRuntimeEntityItemSlotSnapshot(
                    position.X,
                    position.Y,
                    itemKey,
                    quality,
                    durability,
                    quantity <= 0 ? 1 : quantity);
            }

            return slots;
        }

        private static IReadOnlyList<AetheriaRuntimeEntityStatGridSnapshot> ReadFieldEntityStatGrids(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeEntityStatGridSnapshot>();
            var count = reader.ReadArrayHeader();
            var grids = new AetheriaRuntimeEntityStatGridSnapshot[count];
            for (var grid = 0; grid < count; grid++)
            {
                var gridFields = reader.ReadArrayHeader();
                var name = ReadFieldString(ref reader, gridFields, 0);
                var width = ReadFieldInt32(ref reader, gridFields, 1);
                var height = ReadFieldInt32(ref reader, gridFields, 2);
                var values = ReadFieldDoubleArray(ref reader, gridFields, 3);
                SkipRemaining(ref reader, gridFields, 4);
                grids[grid] = new AetheriaRuntimeEntityStatGridSnapshot(name, width, height, values);
            }

            return grids;
        }

        private static IReadOnlyList<IReadOnlyList<int>> ReadFieldEntityWeaponGroups(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<IReadOnlyList<int>>();
            var count = reader.ReadArrayHeader();
            var groups = new IReadOnlyList<int>[count];
            for (var group = 0; group < count; group++)
            {
                var groupFields = reader.ReadArrayHeader();
                groups[group] = ReadFieldInt32Array(ref reader, groupFields, 0);
                SkipRemaining(ref reader, groupFields, 1);
            }

            return groups;
        }

        private static IReadOnlyList<AetheriaRuntimeActiveConsumableSnapshot> ReadFieldActiveConsumables(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeActiveConsumableSnapshot>();
            var count = reader.ReadArrayHeader();
            var consumables = new AetheriaRuntimeActiveConsumableSnapshot[count];
            for (var consumable = 0; consumable < count; consumable++)
            {
                var consumableFields = reader.ReadArrayHeader();
                var itemKey = ReadFieldString(ref reader, consumableFields, 0);
                var quality = ReadFieldDouble(ref reader, consumableFields, 1, 1);
                var remainingDuration = ReadFieldDouble(ref reader, consumableFields, 2);
                var duration = ReadFieldDouble(ref reader, consumableFields, 3);
                SkipRemaining(ref reader, consumableFields, 4);
                consumables[consumable] = new AetheriaRuntimeActiveConsumableSnapshot(itemKey, quality, remainingDuration, duration);
            }

            return consumables;
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorProgressSnapshot> ReadFieldBehaviorProgress(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBehaviorProgressSnapshot>();
            var count = reader.ReadArrayHeader();
            var progressRows = new AetheriaRuntimeBehaviorProgressSnapshot[count];
            for (var progress = 0; progress < count; progress++)
            {
                var progressFields = reader.ReadArrayHeader();
                var ownerKind = ReadFieldString(ref reader, progressFields, 0);
                var ownerIndex = ReadFieldInt32(ref reader, progressFields, 1);
                var behaviorIndex = ReadFieldInt32(ref reader, progressFields, 2);
                var behaviorKind = ReadFieldString(ref reader, progressFields, 3);
                var progressValue = ReadFieldDouble(ref reader, progressFields, 4);
                SkipRemaining(ref reader, progressFields, 5);
                progressRows[progress] = new AetheriaRuntimeBehaviorProgressSnapshot(
                    ownerKind,
                    ownerIndex,
                    behaviorIndex,
                    behaviorKind,
                    progressValue);
            }

            return progressRows;
        }

        private static IReadOnlyList<AetheriaRuntimeWeaponStateSnapshot> ReadFieldWeaponStates(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeWeaponStateSnapshot>();
            var count = reader.ReadArrayHeader();
            var weaponStates = new AetheriaRuntimeWeaponStateSnapshot[count];
            for (var weapon = 0; weapon < count; weapon++)
            {
                var weaponFields = reader.ReadArrayHeader();
                var ownerKind = ReadFieldString(ref reader, weaponFields, 0);
                var ownerIndex = ReadFieldInt32(ref reader, weaponFields, 1);
                var behaviorIndex = ReadFieldInt32(ref reader, weaponFields, 2);
                var behaviorKind = ReadFieldString(ref reader, weaponFields, 3);
                var firing = ReadFieldBool(ref reader, weaponFields, 4);
                var ammo = ReadFieldInt32(ref reader, weaponFields, 5);
                var burstRemaining = ReadFieldInt32(ref reader, weaponFields, 6);
                var burstTimer = ReadFieldDouble(ref reader, weaponFields, 7);
                var burstInterval = ReadFieldDouble(ref reader, weaponFields, 8);
                var cooldownProgress = ReadFieldDouble(ref reader, weaponFields, 9);
                var coolingDown = ReadFieldBool(ref reader, weaponFields, 10);
                var charging = ReadFieldBool(ref reader, weaponFields, 11);
                var charged = ReadFieldBool(ref reader, weaponFields, 12);
                var charge = ReadFieldDouble(ref reader, weaponFields, 13);
                var reloading = ReadFieldBool(ref reader, weaponFields, 14);
                var reloadProgress = ReadFieldDouble(ref reader, weaponFields, 15);
                var ammoIntervalProgress = ReadFieldDouble(ref reader, weaponFields, 16);
                var lockProgress = ReadFieldDouble(ref reader, weaponFields, 17);
                var lockTargetEntityKey = ReadFieldString(ref reader, weaponFields, 18);
                SkipRemaining(ref reader, weaponFields, 19);
                weaponStates[weapon] = new AetheriaRuntimeWeaponStateSnapshot(
                    ownerKind,
                    ownerIndex,
                    behaviorIndex,
                    behaviorKind,
                    firing,
                    ammo,
                    burstRemaining,
                    burstTimer,
                    burstInterval,
                    cooldownProgress,
                    coolingDown,
                    charging,
                    charged,
                    charge,
                    reloading,
                    reloadProgress,
                    ammoIntervalProgress,
                    lockProgress,
                    lockTargetEntityKey);
            }

            return weaponStates;
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorStateSnapshot> ReadFieldBehaviorStates(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeBehaviorStateSnapshot>();
            var count = reader.ReadArrayHeader();
            var behaviorStates = new AetheriaRuntimeBehaviorStateSnapshot[count];
            for (var behavior = 0; behavior < count; behavior++)
            {
                var behaviorFields = reader.ReadArrayHeader();
                var ownerKind = ReadFieldString(ref reader, behaviorFields, 0);
                var ownerIndex = ReadFieldInt32(ref reader, behaviorFields, 1);
                var behaviorIndex = ReadFieldInt32(ref reader, behaviorFields, 2);
                var behaviorKind = ReadFieldString(ref reader, behaviorFields, 3);
                var pinging = ReadFieldBool(ref reader, behaviorFields, 4);
                var pingCooldown = ReadFieldDouble(ref reader, behaviorFields, 5);
                var pingLerp = ReadFieldDouble(ref reader, behaviorFields, 6);
                var pingRadius = ReadFieldDouble(ref reader, behaviorFields, 7);
                var pingedEntityCount = ReadFieldInt32(ref reader, behaviorFields, 8);
                var radiatorTemperature = ReadFieldDouble(ref reader, behaviorFields, 9);
                var emissivity = ReadFieldDouble(ref reader, behaviorFields, 10);
                var pumpedHeat = ReadFieldDouble(ref reader, behaviorFields, 11);
                var wasteHeat = ReadFieldDouble(ref reader, behaviorFields, 12);
                var energyUsage = ReadFieldDouble(ref reader, behaviorFields, 13);
                var reactorDraw = ReadFieldDouble(ref reader, behaviorFields, 14);
                var reactorLoadRatio = ReadFieldDouble(ref reader, behaviorFields, 15);
                var capacitorCharge = ReadFieldDouble(ref reader, behaviorFields, 16);
                var capacitorCapacity = ReadFieldDouble(ref reader, behaviorFields, 17);
                var capacitorEfficiency = ReadFieldDouble(ref reader, behaviorFields, 18);
                var aetherDriveAxisX = ReadFieldDouble(ref reader, behaviorFields, 19);
                var aetherDriveAxisY = ReadFieldDouble(ref reader, behaviorFields, 20);
                var aetherDriveAxisZ = ReadFieldDouble(ref reader, behaviorFields, 21);
                var aetherDriveThrustX = ReadFieldDouble(ref reader, behaviorFields, 22);
                var aetherDriveThrustY = ReadFieldDouble(ref reader, behaviorFields, 23);
                var aetherDriveThrustZ = ReadFieldDouble(ref reader, behaviorFields, 24);
                var aetherDriveRpmX = ReadFieldDouble(ref reader, behaviorFields, 25);
                var aetherDriveRpmY = ReadFieldDouble(ref reader, behaviorFields, 26);
                var aetherDriveRpmZ = ReadFieldDouble(ref reader, behaviorFields, 27);
                var aetherDriveMaximumRpm = ReadFieldDouble(ref reader, behaviorFields, 28);
                var aetherDriveThrustDirectionX = ReadFieldDouble(ref reader, behaviorFields, 29);
                var aetherDriveThrustDirectionY = ReadFieldDouble(ref reader, behaviorFields, 30);
                var resourceScannerTargetBodyId = ReadFieldString(ref reader, behaviorFields, 31);
                var resourceScannerAsteroidIndex = ReadFieldInt32(ref reader, behaviorFields, 32);
                var resourceScannerScanTime = ReadFieldDouble(ref reader, behaviorFields, 33);
                var resourceScannerRange = ReadFieldDouble(ref reader, behaviorFields, 34);
                var resourceScannerMinimumDensity = ReadFieldDouble(ref reader, behaviorFields, 35);
                var resourceScannerScanDuration = ReadFieldDouble(ref reader, behaviorFields, 36);
                var miningToolAsteroidBeltId = ReadFieldString(ref reader, behaviorFields, 37);
                var miningToolAsteroidIndex = ReadFieldInt32(ref reader, behaviorFields, 38);
                var miningToolRange = ReadFieldDouble(ref reader, behaviorFields, 39);
                var thrusterAxis = ReadFieldDouble(ref reader, behaviorFields, 40);
                var thrusterThrust = ReadFieldDouble(ref reader, behaviorFields, 41);
                var thrusterTorque = ReadFieldDouble(ref reader, behaviorFields, 42);
                var shieldEfficiency = ReadFieldDouble(ref reader, behaviorFields, 43);
                var shieldEnergyUsage = ReadFieldDouble(ref reader, behaviorFields, 44);
                var velocityLimit = ReadFieldDouble(ref reader, behaviorFields, 45);
                var thermotoggleTargetTemperature = ReadFieldDouble(ref reader, behaviorFields, 46);
                var switchActivated = ReadFieldBool(ref reader, behaviorFields, 47);
                var triggerPulled = ReadFieldBool(ref reader, behaviorFields, 48);
                var statModifierApplied = ReadFieldBool(ref reader, behaviorFields, 49);
                var statModifierExecuted = ReadFieldBool(ref reader, behaviorFields, 50);
                var statModifierTargetStatCount = ReadFieldInt32(ref reader, behaviorFields, 51);
                var turretControllerWeaponCount = ReadFieldInt32(ref reader, behaviorFields, 52);
                var turretControllerShotSpeed = ReadFieldDouble(ref reader, behaviorFields, 53);
                var turretControllerPredictShots = ReadFieldBool(ref reader, behaviorFields, 54);
                SkipRemaining(ref reader, behaviorFields, 55);
                behaviorStates[behavior] = new AetheriaRuntimeBehaviorStateSnapshot(
                    ownerKind,
                    ownerIndex,
                    behaviorIndex,
                    behaviorKind,
                    pinging,
                    pingCooldown,
                    pingLerp,
                    pingRadius,
                    pingedEntityCount,
                    radiatorTemperature,
                    emissivity,
                    pumpedHeat,
                    wasteHeat,
                    energyUsage,
                    reactorDraw,
                    reactorLoadRatio,
                    capacitorCharge,
                    capacitorCapacity,
                    capacitorEfficiency,
                    aetherDriveAxisX,
                    aetherDriveAxisY,
                    aetherDriveAxisZ,
                    aetherDriveThrustX,
                    aetherDriveThrustY,
                    aetherDriveThrustZ,
                    aetherDriveRpmX,
                    aetherDriveRpmY,
                    aetherDriveRpmZ,
                    aetherDriveMaximumRpm,
                    aetherDriveThrustDirectionX,
                    aetherDriveThrustDirectionY,
                    resourceScannerTargetBodyId,
                    resourceScannerAsteroidIndex,
                    resourceScannerScanTime,
                    resourceScannerRange,
                    resourceScannerMinimumDensity,
                    resourceScannerScanDuration,
                    miningToolAsteroidBeltId,
                    miningToolAsteroidIndex,
                    miningToolRange,
                    thrusterAxis,
                    thrusterThrust,
                    thrusterTorque,
                    shieldEfficiency,
                    shieldEnergyUsage,
                    velocityLimit,
                    thermotoggleTargetTemperature,
                    switchActivated,
                    triggerPulled,
                    statModifierApplied,
                    statModifierExecuted,
                    statModifierTargetStatCount,
                    turretControllerWeaponCount,
                    turretControllerShotSpeed,
                    turretControllerPredictShots);
            }

            return behaviorStates;
        }

        private static string ReadFieldString(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? "" : ReadString(ref reader);
        }

        private static int ReadFieldInt32(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadInt32();
        }

        private static uint ReadFieldUInt32(ref MessagePackReader reader, int fields, int index)
        {
            return index >= fields ? 0 : reader.ReadUInt32();
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

        private static AetheriaRuntimeEntityLoadoutSnapshot ReadFieldEntityLoadout(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return EmptyEntityLoadout();
            return ReadEntityLoadout(ref reader);
        }

        private static IReadOnlyList<AetheriaRuntimeEntityLoadoutSnapshot> ReadFieldEntityLoadouts(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeEntityLoadoutSnapshot>();
            var count = reader.ReadArrayHeader();
            var entities = new AetheriaRuntimeEntityLoadoutSnapshot[count];
            for (var entity = 0; entity < count; entity++)
                entities[entity] = ReadEntityLoadout(ref reader);
            return entities;
        }

        private static AetheriaRuntimeEntityLoadoutSnapshot ReadEntityLoadout(ref MessagePackReader reader)
        {
            var fields = reader.ReadArrayHeader();
            var name = ReadFieldString(ref reader, fields, 0);
            var kind = ReadFieldString(ref reader, fields, 1);
            var factionKey = ReadFieldString(ref reader, fields, 2);
            var hull = ReadFieldLoadoutItem(ref reader, fields, 3);
            var equipment = ReadFieldLoadoutItemSlots(ref reader, fields, 4);
            var cargoBays = ReadFieldLoadoutItemSlots(ref reader, fields, 5);
            var dockingBays = ReadFieldLoadoutItemSlots(ref reader, fields, 6);
            var cargoContents = ReadFieldCargoBayLoadouts(ref reader, fields, 7);
            var dockingBayContents = ReadFieldCargoBayLoadouts(ref reader, fields, 8);
            var dockingBayAssignments = ReadFieldInt32Array(ref reader, fields, 9);
            var weaponGroups = ReadFieldInt32Arrays(ref reader, fields, 10);
            var children = ReadFieldEntityLoadouts(ref reader, fields, 11);
            SkipRemaining(ref reader, fields, 12);
            return new AetheriaRuntimeEntityLoadoutSnapshot(
                name,
                kind,
                factionKey,
                hull,
                equipment,
                cargoBays,
                dockingBays,
                cargoContents,
                dockingBayContents,
                dockingBayAssignments,
                weaponGroups,
                children);
        }

        private static AetheriaRuntimeEntityLoadoutSnapshot EmptyEntityLoadout()
        {
            return new AetheriaRuntimeEntityLoadoutSnapshot(
                "",
                "",
                "",
                EmptyLoadoutItem(),
                Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>(),
                Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>(),
                Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>(),
                Array.Empty<AetheriaRuntimeCargoBayLoadoutSnapshot>(),
                Array.Empty<AetheriaRuntimeCargoBayLoadoutSnapshot>(),
                Array.Empty<int>(),
                Array.Empty<IReadOnlyList<int>>(),
                Array.Empty<AetheriaRuntimeEntityLoadoutSnapshot>());
        }

        private static AetheriaRuntimeLoadoutItemSnapshot ReadFieldLoadoutItem(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return EmptyLoadoutItem();
            var itemFields = reader.ReadArrayHeader();
            var itemKey = ReadFieldString(ref reader, itemFields, 0);
            var quality = ReadFieldDouble(ref reader, itemFields, 1, 1);
            var durability = ReadFieldDouble(ref reader, itemFields, 2, 1);
            var quantity = ReadFieldInt32(ref reader, itemFields, 3);
            SkipRemaining(ref reader, itemFields, 4);
            return new AetheriaRuntimeLoadoutItemSnapshot(itemKey, quality, durability, quantity);
        }

        private static AetheriaRuntimeLoadoutItemSnapshot EmptyLoadoutItem()
        {
            return new AetheriaRuntimeLoadoutItemSnapshot("", 1, 1, 1);
        }

        private static IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> ReadFieldLoadoutItemSlots(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>();
            var count = reader.ReadArrayHeader();
            var slots = new AetheriaRuntimeLoadoutItemSlotSnapshot[count];
            for (var slot = 0; slot < count; slot++)
                slots[slot] = ReadLoadoutItemSlot(ref reader);
            return slots;
        }

        private static AetheriaRuntimeLoadoutItemSlotSnapshot ReadLoadoutItemSlot(ref MessagePackReader reader)
        {
            var slotFields = reader.ReadArrayHeader();
            var position = ReadFieldGridCoord(ref reader, slotFields, 0);
            var item = ReadFieldLoadoutItem(ref reader, slotFields, 1);
            SkipRemaining(ref reader, slotFields, 2);
            return new AetheriaRuntimeLoadoutItemSlotSnapshot(position.X, position.Y, item);
        }

        private static GridCoord ReadFieldGridCoord(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return new GridCoord(0, 0);
            var coordFields = reader.ReadArrayHeader();
            var x = ReadFieldInt32(ref reader, coordFields, 0);
            var y = ReadFieldInt32(ref reader, coordFields, 1);
            SkipRemaining(ref reader, coordFields, 2);
            return new GridCoord(x, y);
        }

        private static IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> ReadFieldCargoBayLoadouts(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeCargoBayLoadoutSnapshot>();
            var count = reader.ReadArrayHeader();
            var bays = new AetheriaRuntimeCargoBayLoadoutSnapshot[count];
            for (var bay = 0; bay < count; bay++)
            {
                var bayFields = reader.ReadArrayHeader();
                var items = ReadFieldLoadoutItemSlots(ref reader, bayFields, 0);
                SkipRemaining(ref reader, bayFields, 1);
                bays[bay] = new AetheriaRuntimeCargoBayLoadoutSnapshot(items);
            }

            return bays;
        }

        private static IReadOnlyList<int> ReadFieldInt32Array(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<int>();
            var count = reader.ReadArrayHeader();
            var values = new int[count];
            for (var item = 0; item < count; item++)
                values[item] = reader.ReadInt32();
            return values;
        }

        private static IReadOnlyList<IReadOnlyList<int>> ReadFieldInt32Arrays(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<IReadOnlyList<int>>();
            var count = reader.ReadArrayHeader();
            var values = new IReadOnlyList<int>[count];
            for (var item = 0; item < count; item++)
            {
                var groupCount = reader.ReadArrayHeader();
                var group = new int[groupCount];
                for (var groupItem = 0; groupItem < groupCount; groupItem++)
                    group[groupItem] = reader.ReadInt32();
                values[item] = group;
            }

            return values;
        }

        private static IReadOnlyList<double> ReadFieldDoubleArray(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<double>();
            var count = reader.ReadArrayHeader();
            var values = new double[count];
            for (var item = 0; item < count; item++)
                values[item] = reader.ReadDouble();
            return values;
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

        private static IReadOnlyList<AetheriaRuntimeAudioStat> ReadFieldAudioStats(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return Array.Empty<AetheriaRuntimeAudioStat>();
            var count = reader.ReadArrayHeader();
            var audioStats = new AetheriaRuntimeAudioStat[count];
            for (var audioStat = 0; audioStat < count; audioStat++)
            {
                var audioStatFields = reader.ReadArrayHeader();
                var parameter = ReadFieldUInt32(ref reader, audioStatFields, 0);
                var stat = ReadFieldPerformanceStat(ref reader, audioStatFields, 1);
                SkipRemaining(ref reader, audioStatFields, 2);
                audioStats[audioStat] = new AetheriaRuntimeAudioStat(parameter, stat);
            }

            return audioStats;
        }

        private static AetheriaRuntimePerformanceStat ReadFieldPerformanceStat(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return EmptyPerformanceStat();
            var statFields = reader.ReadArrayHeader();
            var min = ReadFieldDouble(ref reader, statFields, 0);
            var max = ReadFieldDouble(ref reader, statFields, 1);
            var heatExponentMultiplier = ReadFieldDouble(ref reader, statFields, 2);
            var durabilityExponentMultiplier = ReadFieldDouble(ref reader, statFields, 3);
            var qualityExponent = ReadFieldDouble(ref reader, statFields, 4);
            SkipRemaining(ref reader, statFields, 5);
            return new AetheriaRuntimePerformanceStat(
                min,
                max,
                heatExponentMultiplier,
                durabilityExponentMultiplier,
                qualityExponent);
        }

        private static AetheriaRuntimePerformanceStat EmptyPerformanceStat()
        {
            return new AetheriaRuntimePerformanceStat(0, 0, 0, 0, 0);
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

        private static int CountAndSkipFieldArray(ref MessagePackReader reader, int fields, int index)
        {
            if (index >= fields) return 0;
            var count = reader.ReadArrayHeader();
            for (var item = 0; item < count; item++)
                reader.Skip();
            return count;
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

        private readonly struct Vector2Value
        {
            public Vector2Value(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }
        }

        private readonly struct Vector3Value
        {
            public Vector3Value(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X { get; }

            public double Y { get; }

            public double Z { get; }
        }

        private readonly struct GridCoord
        {
            public GridCoord(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }

            public int Y { get; }
        }
    }
}
