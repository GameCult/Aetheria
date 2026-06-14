using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeCatalogSnapshot
    {
        private readonly Dictionary<string, AetheriaRuntimeCatalogItem> _itemsByLegacyId;
        private readonly Dictionary<string, AetheriaRuntimeCorporation> _corporationsByLegacyId;
        private readonly Dictionary<string, AetheriaRuntimeNameFile> _nameFilesByLegacyId;

        public AetheriaRuntimeCatalogSnapshot(
            AetheriaRuntimeCatalogItem[] items,
            AetheriaRuntimeCorporation[] corporations,
            AetheriaRuntimeNameFile[] nameFiles)
        {
            Items = items;
            Corporations = corporations;
            NameFiles = nameFiles;
            TradeItems = items.Where(item => item.Price > 0).ToArray();
            EquipmentItems = items.Where(item => !string.IsNullOrWhiteSpace(item.HardpointType)).ToArray();

            _itemsByLegacyId = items
                .Where(item => !string.IsNullOrWhiteSpace(item.LegacyId))
                .ToDictionary(item => item.LegacyId, StringComparer.OrdinalIgnoreCase);
            _corporationsByLegacyId = corporations
                .Where(corporation => !string.IsNullOrWhiteSpace(corporation.LegacyId))
                .ToDictionary(corporation => corporation.LegacyId, StringComparer.OrdinalIgnoreCase);
            _nameFilesByLegacyId = nameFiles
                .Where(nameFile => !string.IsNullOrWhiteSpace(nameFile.LegacyId))
                .ToDictionary(nameFile => nameFile.LegacyId, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<AetheriaRuntimeCatalogItem> Items { get; }

        public IReadOnlyList<AetheriaRuntimeCatalogItem> TradeItems { get; }

        public IReadOnlyList<AetheriaRuntimeCatalogItem> EquipmentItems { get; }

        public IReadOnlyList<AetheriaRuntimeCorporation> Corporations { get; }

        public IReadOnlyList<AetheriaRuntimeNameFile> NameFiles { get; }

        public AetheriaRuntimeCatalogItem? FindItemByLegacyId(string legacyId)
        {
            return TryGet(_itemsByLegacyId, legacyId);
        }

        public AetheriaRuntimeCorporation? FindCorporationByLegacyId(string legacyId)
        {
            return TryGet(_corporationsByLegacyId, legacyId);
        }

        public AetheriaRuntimeNameFile? FindNameFileByLegacyId(string legacyId)
        {
            return TryGet(_nameFilesByLegacyId, legacyId);
        }

        public IEnumerable<AetheriaRuntimeCatalogItem> FindItemsByBehavior(string behaviorKind)
        {
            return string.IsNullOrWhiteSpace(behaviorKind)
                ? Enumerable.Empty<AetheriaRuntimeCatalogItem>()
                : Items.Where(item => item.BehaviorKinds.Contains(behaviorKind, StringComparer.OrdinalIgnoreCase));
        }

        public IEnumerable<AetheriaRuntimeCatalogItem> FindItemsByHardpoint(string hardpointType)
        {
            return string.IsNullOrWhiteSpace(hardpointType)
                ? Enumerable.Empty<AetheriaRuntimeCatalogItem>()
                : Items.Where(item => string.Equals(item.HardpointType, hardpointType, StringComparison.OrdinalIgnoreCase));
        }

        public AetheriaRuntimeCorporation? GetManufacturer(AetheriaRuntimeCatalogItem item)
        {
            return FindCorporationByLegacyId(item.ManufacturerLegacyId);
        }

        public AetheriaRuntimeNameFile? GetNameFile(AetheriaRuntimeCorporation corporation)
        {
            return FindNameFileByLegacyId(corporation.GeonameFileLegacyId);
        }

        private static T? TryGet<T>(IReadOnlyDictionary<string, T> dictionary, string key) where T : class
        {
            return string.IsNullOrWhiteSpace(key) ? null : dictionary.TryGetValue(key, out var value) ? value : null;
        }
    }

    public sealed class AetheriaRuntimeCatalogItem
    {
        public AetheriaRuntimeCatalogItem(
            string legacyId,
            string name,
            string category,
            string description,
            string manufacturerLegacyId,
            int price,
            double mass,
            double volume,
            int shapeWidth,
            int shapeHeight,
            int occupiedCells,
            IReadOnlyList<AetheriaRuntimeShapeCell> shapeCells,
            int interiorShapeWidth,
            int interiorShapeHeight,
            int interiorOccupiedCells,
            IReadOnlyList<AetheriaRuntimeShapeCell> interiorShapeCells,
            IReadOnlyList<AetheriaRuntimeHardpoint> hardpoints,
            IReadOnlyList<AetheriaRuntimeBehaviorPayload> behaviorPayloads,
            string hardpointType,
            string hullType,
            IReadOnlyList<string> behaviorKinds,
            int maxStack,
            double durability,
            string weaponRange,
            string weaponCaliber,
            string weaponType,
            string weaponFireTypes,
            string weaponModifiers,
            double minimumTemperature,
            double maximumTemperature)
        {
            LegacyId = legacyId;
            Name = name;
            Category = category;
            Description = description;
            ManufacturerLegacyId = manufacturerLegacyId;
            Price = price;
            Mass = mass;
            Volume = volume;
            ShapeWidth = shapeWidth;
            ShapeHeight = shapeHeight;
            OccupiedCells = occupiedCells;
            ShapeCells = shapeCells;
            InteriorShapeWidth = interiorShapeWidth;
            InteriorShapeHeight = interiorShapeHeight;
            InteriorOccupiedCells = interiorOccupiedCells;
            InteriorShapeCells = interiorShapeCells;
            Hardpoints = hardpoints;
            BehaviorPayloads = behaviorPayloads;
            HardpointType = hardpointType;
            HullType = hullType;
            BehaviorKinds = behaviorKinds;
            MaxStack = maxStack;
            Durability = durability;
            WeaponRange = weaponRange;
            WeaponCaliber = weaponCaliber;
            WeaponType = weaponType;
            WeaponFireTypes = weaponFireTypes;
            WeaponModifiers = weaponModifiers;
            MinimumTemperature = minimumTemperature;
            MaximumTemperature = maximumTemperature;
        }

        public string LegacyId { get; }
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }
        public string ManufacturerLegacyId { get; }
        public int Price { get; }
        public double Mass { get; }
        public double Volume { get; }
        public int ShapeWidth { get; }
        public int ShapeHeight { get; }
        public int OccupiedCells { get; }
        public IReadOnlyList<AetheriaRuntimeShapeCell> ShapeCells { get; }
        public int InteriorShapeWidth { get; }
        public int InteriorShapeHeight { get; }
        public int InteriorOccupiedCells { get; }
        public IReadOnlyList<AetheriaRuntimeShapeCell> InteriorShapeCells { get; }
        public IReadOnlyList<AetheriaRuntimeHardpoint> Hardpoints { get; }
        public IReadOnlyList<AetheriaRuntimeBehaviorPayload> BehaviorPayloads { get; }
        public string HardpointType { get; }
        public string HullType { get; }
        public IReadOnlyList<string> BehaviorKinds { get; }
        public int MaxStack { get; }
        public double Durability { get; }
        public string WeaponRange { get; }
        public string WeaponCaliber { get; }
        public string WeaponType { get; }
        public string WeaponFireTypes { get; }
        public string WeaponModifiers { get; }
        public double MinimumTemperature { get; }
        public double MaximumTemperature { get; }
    }

    public sealed class AetheriaRuntimeShapeCell
    {
        public AetheriaRuntimeShapeCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    public sealed class AetheriaRuntimeBehaviorPayload
    {
        public AetheriaRuntimeBehaviorPayload(
            int unionKey,
            string kind,
            int group,
            IReadOnlyList<AetheriaRuntimeBehaviorField> fields)
        {
            UnionKey = unionKey;
            Kind = kind;
            Group = group;
            Fields = fields;
        }

        public int UnionKey { get; }

        public string Kind { get; }

        public int Group { get; }

        public IReadOnlyList<AetheriaRuntimeBehaviorField> Fields { get; }
    }

    public sealed class AetheriaRuntimeBehaviorField
    {
        public AetheriaRuntimeBehaviorField(int key, AetheriaRuntimeBehaviorValue value)
        {
            Key = key;
            Value = value;
        }

        public int Key { get; }

        public AetheriaRuntimeBehaviorValue Value { get; }
    }

    public sealed class AetheriaRuntimeBehaviorMapEntry
    {
        public AetheriaRuntimeBehaviorMapEntry(string key, AetheriaRuntimeBehaviorValue value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        public AetheriaRuntimeBehaviorValue Value { get; }
    }

    public sealed class AetheriaRuntimeBehaviorValue
    {
        public AetheriaRuntimeBehaviorValue(
            string kind,
            string stringValue,
            double numberValue,
            bool boolValue,
            string legacyIdValue,
            IReadOnlyList<AetheriaRuntimeBehaviorValue> children,
            IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> mapEntries)
        {
            Kind = kind;
            StringValue = stringValue;
            NumberValue = numberValue;
            BoolValue = boolValue;
            LegacyIdValue = legacyIdValue;
            Children = children;
            MapEntries = mapEntries;
        }

        public string Kind { get; }

        public string StringValue { get; }

        public double NumberValue { get; }

        public bool BoolValue { get; }

        public string LegacyIdValue { get; }

        public IReadOnlyList<AetheriaRuntimeBehaviorValue> Children { get; }

        public IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> MapEntries { get; }
    }

    public sealed class AetheriaRuntimeHardpoint
    {
        public AetheriaRuntimeHardpoint(
            string type,
            int positionX,
            int positionY,
            int shapeWidth,
            int shapeHeight,
            int occupiedCells,
            IReadOnlyList<AetheriaRuntimeShapeCell> shapeCells,
            string transform,
            string rotation,
            double armor)
        {
            Type = type;
            PositionX = positionX;
            PositionY = positionY;
            ShapeWidth = shapeWidth;
            ShapeHeight = shapeHeight;
            OccupiedCells = occupiedCells;
            ShapeCells = shapeCells;
            Transform = transform;
            Rotation = rotation;
            Armor = armor;
        }

        public string Type { get; }

        public int PositionX { get; }

        public int PositionY { get; }

        public int ShapeWidth { get; }

        public int ShapeHeight { get; }

        public int OccupiedCells { get; }

        public IReadOnlyList<AetheriaRuntimeShapeCell> ShapeCells { get; }

        public string Transform { get; }

        public string Rotation { get; }

        public double Armor { get; }
    }

    public sealed class AetheriaRuntimeCorporation
    {
        public AetheriaRuntimeCorporation(
            string legacyId,
            string name,
            string shortName,
            string description,
            string geonameFileLegacyId,
            string bossHullLegacyId,
            int influenceDistance,
            int allegianceCount,
            IReadOnlyList<AetheriaRuntimeCorporationAllegiance> allegiances)
        {
            LegacyId = legacyId;
            Name = name;
            ShortName = shortName;
            Description = description;
            GeonameFileLegacyId = geonameFileLegacyId;
            BossHullLegacyId = bossHullLegacyId;
            InfluenceDistance = influenceDistance;
            AllegianceCount = allegianceCount;
            Allegiances = allegiances;
        }

        public string LegacyId { get; }
        public string Name { get; }
        public string ShortName { get; }
        public string Description { get; }
        public string GeonameFileLegacyId { get; }
        public string BossHullLegacyId { get; }
        public int InfluenceDistance { get; }
        public int AllegianceCount { get; }
        public IReadOnlyList<AetheriaRuntimeCorporationAllegiance> Allegiances { get; }
    }

    public sealed class AetheriaRuntimeCorporationAllegiance
    {
        public AetheriaRuntimeCorporationAllegiance(string corporationLegacyId, double weight)
        {
            CorporationLegacyId = corporationLegacyId;
            Weight = weight;
        }

        public string CorporationLegacyId { get; }
        public double Weight { get; }
    }

    public sealed class AetheriaRuntimeNameFile
    {
        public AetheriaRuntimeNameFile(
            string legacyId,
            string name,
            int nameCount,
            IReadOnlyList<string> sampleNames,
            IReadOnlyList<string> names)
        {
            LegacyId = legacyId;
            Name = name;
            NameCount = nameCount;
            SampleNames = sampleNames;
            Names = names;
        }

        public string LegacyId { get; }
        public string Name { get; }
        public int NameCount { get; }
        public IReadOnlyList<string> SampleNames { get; }
        public IReadOnlyList<string> Names { get; }
    }

    public sealed class AetheriaRuntimePlayerSettingsSnapshot
    {
        public AetheriaRuntimePlayerSettingsSnapshot(
            string playerName,
            bool tutorialPassed,
            IReadOnlyList<AetheriaRuntimeStoryFileHash> storyFileHashes,
            string temperatureUnit,
            int significantDigits,
            string nebulaQuality,
            bool showAsteroidsInMinimap,
            IReadOnlyList<AetheriaRuntimeInputBindingOverride> bindingOverrides,
            IReadOnlyList<string> actionBarInputs)
        {
            PlayerName = playerName;
            TutorialPassed = tutorialPassed;
            StoryFileHashes = storyFileHashes;
            TemperatureUnit = temperatureUnit;
            SignificantDigits = significantDigits;
            NebulaQuality = nebulaQuality;
            ShowAsteroidsInMinimap = showAsteroidsInMinimap;
            BindingOverrides = bindingOverrides;
            ActionBarInputs = actionBarInputs;
        }

        public string PlayerName { get; }
        public bool TutorialPassed { get; }
        public IReadOnlyList<AetheriaRuntimeStoryFileHash> StoryFileHashes { get; }
        public string TemperatureUnit { get; }
        public int SignificantDigits { get; }
        public string NebulaQuality { get; }
        public bool ShowAsteroidsInMinimap { get; }
        public IReadOnlyList<AetheriaRuntimeInputBindingOverride> BindingOverrides { get; }
        public IReadOnlyList<string> ActionBarInputs { get; }
    }

    public sealed class AetheriaRuntimeStoryFileHash
    {
        public AetheriaRuntimeStoryFileHash(string storyPath, string hash)
        {
            StoryPath = storyPath;
            Hash = hash;
        }

        public string StoryPath { get; }
        public string Hash { get; }
    }

    public sealed class AetheriaRuntimeInputBindingOverride
    {
        public AetheriaRuntimeInputBindingOverride(string actionName, int bindingIndex, string bindingPath)
        {
            ActionName = actionName;
            BindingIndex = bindingIndex;
            BindingPath = bindingPath;
        }

        public string ActionName { get; }
        public int BindingIndex { get; }
        public string BindingPath { get; }
    }
}
