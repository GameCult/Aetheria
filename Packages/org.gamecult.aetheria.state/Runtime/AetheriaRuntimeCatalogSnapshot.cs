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
            double specificHeat,
            double conductivity,
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
            bool stackable,
            double duration,
            double durability,
            string weaponRange,
            string weaponCaliber,
            string weaponType,
            string weaponFireTypes,
            string weaponModifiers,
            double minimumTemperature,
            double maximumTemperature,
            IReadOnlyList<AetheriaRuntimeCurveKey> thermalPerformanceCurveKeys,
            string hullPrefab,
            double thermalResilience,
            double hullGridOffset,
            double hullArmor,
            double hullDrag,
            bool hullCanTow,
            int dockingMaxSizeX,
            int dockingMaxSizeY,
            string actionBarIcon,
            IReadOnlyList<AetheriaRuntimeAudioStat> audioStats,
            IReadOnlyList<AetheriaRuntimeCurveKey> effectivenessCurveKeys,
            string simpleCommodityCategory,
            string compoundCommodityCategory)
        {
            LegacyId = legacyId;
            Name = name;
            Category = category;
            Description = description;
            ManufacturerLegacyId = manufacturerLegacyId;
            Price = price;
            Mass = mass;
            SpecificHeat = specificHeat;
            Conductivity = conductivity;
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
            Stackable = stackable;
            Duration = duration;
            Durability = durability;
            WeaponRange = weaponRange;
            WeaponCaliber = weaponCaliber;
            WeaponType = weaponType;
            WeaponFireTypes = weaponFireTypes;
            WeaponModifiers = weaponModifiers;
            MinimumTemperature = minimumTemperature;
            MaximumTemperature = maximumTemperature;
            ThermalPerformanceCurveKeys = thermalPerformanceCurveKeys;
            HullPrefab = hullPrefab;
            ThermalResilience = thermalResilience;
            HullGridOffset = hullGridOffset;
            HullArmor = hullArmor;
            HullDrag = hullDrag;
            HullCanTow = hullCanTow;
            DockingMaxSizeX = dockingMaxSizeX;
            DockingMaxSizeY = dockingMaxSizeY;
            ActionBarIcon = actionBarIcon;
            AudioStats = audioStats;
            EffectivenessCurveKeys = effectivenessCurveKeys;
            SimpleCommodityCategory = simpleCommodityCategory;
            CompoundCommodityCategory = compoundCommodityCategory;
        }

        public string LegacyId { get; }
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }
        public string ManufacturerLegacyId { get; }
        public int Price { get; }
        public double Mass { get; }
        public double SpecificHeat { get; }
        public double Conductivity { get; }
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
        public bool Stackable { get; }
        public double Duration { get; }
        public double Durability { get; }
        public string WeaponRange { get; }
        public string WeaponCaliber { get; }
        public string WeaponType { get; }
        public string WeaponFireTypes { get; }
        public string WeaponModifiers { get; }
        public double MinimumTemperature { get; }
        public double MaximumTemperature { get; }
        public IReadOnlyList<AetheriaRuntimeCurveKey> ThermalPerformanceCurveKeys { get; }
        public string HullPrefab { get; }
        public double ThermalResilience { get; }
        public double HullGridOffset { get; }
        public double HullArmor { get; }
        public double HullDrag { get; }
        public bool HullCanTow { get; }
        public int DockingMaxSizeX { get; }
        public int DockingMaxSizeY { get; }
        public string ActionBarIcon { get; }
        public IReadOnlyList<AetheriaRuntimeAudioStat> AudioStats { get; }
        public IReadOnlyList<AetheriaRuntimeCurveKey> EffectivenessCurveKeys { get; }
        public string SimpleCommodityCategory { get; }
        public string CompoundCommodityCategory { get; }
    }

    public sealed class AetheriaRuntimeAudioStat
    {
        public AetheriaRuntimeAudioStat(uint parameter, AetheriaRuntimePerformanceStat stat)
        {
            Parameter = parameter;
            Stat = stat;
        }

        public uint Parameter { get; }
        public AetheriaRuntimePerformanceStat Stat { get; }
    }

    public sealed class AetheriaRuntimePerformanceStat
    {
        public AetheriaRuntimePerformanceStat(
            double min,
            double max,
            double heatExponentMultiplier,
            double durabilityExponentMultiplier,
            double qualityExponent)
        {
            Min = min;
            Max = max;
            HeatExponentMultiplier = heatExponentMultiplier;
            DurabilityExponentMultiplier = durabilityExponentMultiplier;
            QualityExponent = qualityExponent;
        }

        public double Min { get; }
        public double Max { get; }
        public double HeatExponentMultiplier { get; }
        public double DurabilityExponentMultiplier { get; }
        public double QualityExponent { get; }
    }

    public sealed class AetheriaRuntimeCurveKey
    {
        public AetheriaRuntimeCurveKey(double time, double value, double inTangent, double outTangent)
        {
            Time = time;
            Value = value;
            InTangent = inTangent;
            OutTangent = outTangent;
        }

        public double Time { get; }
        public double Value { get; }
        public double InTangent { get; }
        public double OutTangent { get; }
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

    public sealed class AetheriaRuntimeLoadoutTemplateSnapshot
    {
        public AetheriaRuntimeLoadoutTemplateSnapshot(
            string name,
            string ownerPlayerKey,
            AetheriaRuntimeEntityLoadoutSnapshot rootEntity,
            string createdAtUtc,
            string updatedAtUtc)
        {
            Name = name;
            OwnerPlayerKey = ownerPlayerKey;
            RootEntity = rootEntity;
            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = updatedAtUtc;
        }

        public string Name { get; }
        public string OwnerPlayerKey { get; }
        public AetheriaRuntimeEntityLoadoutSnapshot RootEntity { get; }
        public string CreatedAtUtc { get; }
        public string UpdatedAtUtc { get; }
    }

    public sealed class AetheriaRuntimeEntityLoadoutSnapshot
    {
        public AetheriaRuntimeEntityLoadoutSnapshot(
            string name,
            string kind,
            string factionKey,
            AetheriaRuntimeLoadoutItemSnapshot hull,
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> equipment,
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> cargoBays,
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> dockingBays,
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> cargoContents,
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> dockingBayContents,
            IReadOnlyList<int> dockingBayAssignments,
            IReadOnlyList<IReadOnlyList<int>> weaponGroups,
            IReadOnlyList<AetheriaRuntimeEntityLoadoutSnapshot> children)
        {
            Name = name;
            Kind = kind;
            FactionKey = factionKey;
            Hull = hull;
            Equipment = equipment;
            CargoBays = cargoBays;
            DockingBays = dockingBays;
            CargoContents = cargoContents;
            DockingBayContents = dockingBayContents;
            DockingBayAssignments = dockingBayAssignments;
            WeaponGroups = weaponGroups;
            Children = children;
        }

        public string Name { get; }
        public string Kind { get; }
        public string FactionKey { get; }
        public AetheriaRuntimeLoadoutItemSnapshot Hull { get; }
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> Equipment { get; }
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> CargoBays { get; }
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> DockingBays { get; }
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> CargoContents { get; }
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> DockingBayContents { get; }
        public IReadOnlyList<int> DockingBayAssignments { get; }
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; }
        public IReadOnlyList<AetheriaRuntimeEntityLoadoutSnapshot> Children { get; }
    }

    public sealed class AetheriaRuntimeLoadoutItemSnapshot
    {
        public AetheriaRuntimeLoadoutItemSnapshot(string itemKey, double quality, double durability, int quantity)
        {
            ItemKey = itemKey;
            Quality = quality;
            Durability = durability;
            Quantity = quantity;
        }

        public string ItemKey { get; }
        public double Quality { get; }
        public double Durability { get; }
        public int Quantity { get; }
    }

    public sealed class AetheriaRuntimeLoadoutItemSlotSnapshot
    {
        public AetheriaRuntimeLoadoutItemSlotSnapshot(int x, int y, AetheriaRuntimeLoadoutItemSnapshot item)
        {
            X = x;
            Y = y;
            Item = item;
        }

        public int X { get; }
        public int Y { get; }
        public AetheriaRuntimeLoadoutItemSnapshot Item { get; }
    }

    public sealed class AetheriaRuntimeCargoBayLoadoutSnapshot
    {
        public AetheriaRuntimeCargoBayLoadoutSnapshot(IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> items)
        {
            Items = items;
        }

        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> Items { get; }
    }

    public sealed class AetheriaRuntimeRunStateSnapshot
    {
        public AetheriaRuntimeRunStateSnapshot(
            string runId,
            bool isTutorial,
            int entranceZoneIndex,
            int exitZoneIndex,
            int currentZoneIndex,
            int currentZoneEntityIndex,
            IReadOnlyList<int> discoveredZoneIndices,
            IReadOnlyList<string> zoneKeys,
            IReadOnlyList<AetheriaRuntimeActionBarBindingSnapshot> actionBarBindings,
            IReadOnlyList<AetheriaRuntimeFactionRelationshipSnapshot> factionRelationships,
            string updatedAtUtc)
        {
            RunId = runId;
            IsTutorial = isTutorial;
            EntranceZoneIndex = entranceZoneIndex;
            ExitZoneIndex = exitZoneIndex;
            CurrentZoneIndex = currentZoneIndex;
            CurrentZoneEntityIndex = currentZoneEntityIndex;
            DiscoveredZoneIndices = discoveredZoneIndices;
            ZoneKeys = zoneKeys;
            ActionBarBindings = actionBarBindings;
            FactionRelationships = factionRelationships;
            UpdatedAtUtc = updatedAtUtc;
        }

        public string RunId { get; }
        public bool IsTutorial { get; }
        public int EntranceZoneIndex { get; }
        public int ExitZoneIndex { get; }
        public int CurrentZoneIndex { get; }
        public int CurrentZoneEntityIndex { get; }
        public IReadOnlyList<int> DiscoveredZoneIndices { get; }
        public IReadOnlyList<string> ZoneKeys { get; }
        public IReadOnlyList<AetheriaRuntimeActionBarBindingSnapshot> ActionBarBindings { get; }
        public IReadOnlyList<AetheriaRuntimeFactionRelationshipSnapshot> FactionRelationships { get; }
        public string UpdatedAtUtc { get; }
    }

    public sealed class AetheriaRuntimeZoneStateSnapshot
    {
        public AetheriaRuntimeZoneStateSnapshot(
            string name,
            double positionX,
            double positionY,
            IReadOnlyList<int> adjacentZoneIndices,
            IReadOnlyList<int> factionIndices,
            int ownerFactionIndex,
            IReadOnlyList<string> entityKeys,
            IReadOnlyList<AetheriaRuntimeOrbitSnapshot> orbits,
            IReadOnlyList<AetheriaRuntimeBodySnapshot> bodies)
        {
            Name = name;
            PositionX = positionX;
            PositionY = positionY;
            AdjacentZoneIndices = adjacentZoneIndices;
            FactionIndices = factionIndices;
            OwnerFactionIndex = ownerFactionIndex;
            EntityKeys = entityKeys;
            Orbits = orbits;
            Bodies = bodies;
        }

        public string Name { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public IReadOnlyList<int> AdjacentZoneIndices { get; }
        public IReadOnlyList<int> FactionIndices { get; }
        public int OwnerFactionIndex { get; }
        public IReadOnlyList<string> EntityKeys { get; }
        public IReadOnlyList<AetheriaRuntimeOrbitSnapshot> Orbits { get; }
        public IReadOnlyList<AetheriaRuntimeBodySnapshot> Bodies { get; }
    }

    public sealed class AetheriaRuntimeEntitySnapshot
    {
        public AetheriaRuntimeEntitySnapshot(
            string name,
            string kind,
            double positionX,
            double positionY,
            double positionZ,
            double directionX,
            double directionY,
            string factionKey,
            string hullItemKey,
            IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> equipment,
            IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> cargoBays,
            IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> dockingBays,
            IReadOnlyList<string> childEntityKeys,
            IReadOnlyList<IReadOnlyList<int>> weaponGroups,
            IReadOnlyList<AetheriaRuntimeEntityStatGridSnapshot> statGrids,
            double velocityX,
            double velocityY,
            string targetEntityKey,
            bool isActive,
            bool heatsinksEnabled,
            bool overrideShutdown,
            double tractorPower,
            double heatstroke,
            double hypothermia,
            IReadOnlyList<AetheriaRuntimeActiveConsumableSnapshot> activeConsumables,
            IReadOnlyList<AetheriaRuntimeBehaviorProgressSnapshot> behaviorProgress,
            IReadOnlyList<AetheriaRuntimeWeaponStateSnapshot> weaponStates,
            IReadOnlyList<AetheriaRuntimeBehaviorStateSnapshot> behaviorStates)
        {
            Name = name;
            Kind = kind;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            DirectionX = directionX;
            DirectionY = directionY;
            FactionKey = factionKey;
            HullItemKey = hullItemKey;
            Equipment = equipment;
            CargoBays = cargoBays;
            DockingBays = dockingBays;
            ChildEntityKeys = childEntityKeys;
            WeaponGroups = weaponGroups;
            StatGrids = statGrids;
            VelocityX = velocityX;
            VelocityY = velocityY;
            TargetEntityKey = targetEntityKey;
            IsActive = isActive;
            HeatsinksEnabled = heatsinksEnabled;
            OverrideShutdown = overrideShutdown;
            TractorPower = tractorPower;
            Heatstroke = heatstroke;
            Hypothermia = hypothermia;
            ActiveConsumables = activeConsumables;
            BehaviorProgress = behaviorProgress;
            WeaponStates = weaponStates;
            BehaviorStates = behaviorStates;
        }

        public string Name { get; }
        public string Kind { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public double PositionZ { get; }
        public double DirectionX { get; }
        public double DirectionY { get; }
        public string FactionKey { get; }
        public string HullItemKey { get; }
        public IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> Equipment { get; }
        public IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> CargoBays { get; }
        public IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> DockingBays { get; }
        public IReadOnlyList<string> ChildEntityKeys { get; }
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; }
        public IReadOnlyList<AetheriaRuntimeEntityStatGridSnapshot> StatGrids { get; }
        public double VelocityX { get; }
        public double VelocityY { get; }
        public string TargetEntityKey { get; }
        public bool IsActive { get; }
        public bool HeatsinksEnabled { get; }
        public bool OverrideShutdown { get; }
        public double TractorPower { get; }
        public double Heatstroke { get; }
        public double Hypothermia { get; }
        public IReadOnlyList<AetheriaRuntimeActiveConsumableSnapshot> ActiveConsumables { get; }
        public IReadOnlyList<AetheriaRuntimeBehaviorProgressSnapshot> BehaviorProgress { get; }
        public IReadOnlyList<AetheriaRuntimeWeaponStateSnapshot> WeaponStates { get; }
        public IReadOnlyList<AetheriaRuntimeBehaviorStateSnapshot> BehaviorStates { get; }
    }

    public sealed class AetheriaRuntimeActionBarBindingSnapshot
    {
        public AetheriaRuntimeActionBarBindingSnapshot(string controlPath, string kind, string targetKey, int equipmentIndex, int behaviorIndex, int weaponGroup)
        {
            ControlPath = controlPath;
            Kind = kind;
            TargetKey = targetKey;
            EquipmentIndex = equipmentIndex;
            BehaviorIndex = behaviorIndex;
            WeaponGroup = weaponGroup;
        }

        public string ControlPath { get; }
        public string Kind { get; }
        public string TargetKey { get; }
        public int EquipmentIndex { get; }
        public int BehaviorIndex { get; }
        public int WeaponGroup { get; }
    }

    public sealed class AetheriaRuntimeFactionRelationshipSnapshot
    {
        public AetheriaRuntimeFactionRelationshipSnapshot(string factionKey, string relationship, double standing)
        {
            FactionKey = factionKey;
            Relationship = relationship;
            Standing = standing;
        }

        public string FactionKey { get; }
        public string Relationship { get; }
        public double Standing { get; }
    }

    public sealed class AetheriaRuntimeOrbitSnapshot
    {
        public AetheriaRuntimeOrbitSnapshot(string orbitId, string parentId, double distance, double phase, double fixedPositionX, double fixedPositionY)
        {
            OrbitId = orbitId;
            ParentId = parentId;
            Distance = distance;
            Phase = phase;
            FixedPositionX = fixedPositionX;
            FixedPositionY = fixedPositionY;
        }

        public string OrbitId { get; }
        public string ParentId { get; }
        public double Distance { get; }
        public double Phase { get; }
        public double FixedPositionX { get; }
        public double FixedPositionY { get; }
    }

    public sealed class AetheriaRuntimeBodySnapshot
    {
        public AetheriaRuntimeBodySnapshot(string bodyId, string kind, string name, string orbitId, double mass, int resourceCount, int asteroidCount)
        {
            BodyId = bodyId;
            Kind = kind;
            Name = name;
            OrbitId = orbitId;
            Mass = mass;
            ResourceCount = resourceCount;
            AsteroidCount = asteroidCount;
        }

        public string BodyId { get; }
        public string Kind { get; }
        public string Name { get; }
        public string OrbitId { get; }
        public double Mass { get; }
        public int ResourceCount { get; }
        public int AsteroidCount { get; }
    }

    public sealed class AetheriaRuntimeEntityItemSlotSnapshot
    {
        public AetheriaRuntimeEntityItemSlotSnapshot(int x, int y, string itemKey)
        {
            X = x;
            Y = y;
            ItemKey = itemKey;
        }

        public int X { get; }
        public int Y { get; }
        public string ItemKey { get; }
    }

    public sealed class AetheriaRuntimeEntityStatGridSnapshot
    {
        public AetheriaRuntimeEntityStatGridSnapshot(string name, int width, int height, IReadOnlyList<double> values)
        {
            Name = name;
            Width = width;
            Height = height;
            Values = values;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<double> Values { get; }
    }

    public sealed class AetheriaRuntimeActiveConsumableSnapshot
    {
        public AetheriaRuntimeActiveConsumableSnapshot(string itemKey, double quality, double remainingDuration, double duration)
        {
            ItemKey = itemKey;
            Quality = quality;
            RemainingDuration = remainingDuration;
            Duration = duration;
        }

        public string ItemKey { get; }
        public double Quality { get; }
        public double RemainingDuration { get; }
        public double Duration { get; }
    }

    public sealed class AetheriaRuntimeBehaviorProgressSnapshot
    {
        public AetheriaRuntimeBehaviorProgressSnapshot(string ownerKind, int ownerIndex, int behaviorIndex, string behaviorKind, double progress)
        {
            OwnerKind = ownerKind;
            OwnerIndex = ownerIndex;
            BehaviorIndex = behaviorIndex;
            BehaviorKind = behaviorKind;
            Progress = progress;
        }

        public string OwnerKind { get; }
        public int OwnerIndex { get; }
        public int BehaviorIndex { get; }
        public string BehaviorKind { get; }
        public double Progress { get; }
    }

    public sealed class AetheriaRuntimeWeaponStateSnapshot
    {
        public AetheriaRuntimeWeaponStateSnapshot(
            string ownerKind,
            int ownerIndex,
            int behaviorIndex,
            string behaviorKind,
            bool firing,
            int ammo,
            int burstRemaining,
            double burstTimer,
            double burstInterval,
            double cooldownProgress,
            bool coolingDown,
            bool charging,
            bool charged,
            double charge,
            bool reloading,
            double reloadProgress,
            double ammoIntervalProgress)
        {
            OwnerKind = ownerKind;
            OwnerIndex = ownerIndex;
            BehaviorIndex = behaviorIndex;
            BehaviorKind = behaviorKind;
            Firing = firing;
            Ammo = ammo;
            BurstRemaining = burstRemaining;
            BurstTimer = burstTimer;
            BurstInterval = burstInterval;
            CooldownProgress = cooldownProgress;
            CoolingDown = coolingDown;
            Charging = charging;
            Charged = charged;
            Charge = charge;
            Reloading = reloading;
            ReloadProgress = reloadProgress;
            AmmoIntervalProgress = ammoIntervalProgress;
        }

        public string OwnerKind { get; }
        public int OwnerIndex { get; }
        public int BehaviorIndex { get; }
        public string BehaviorKind { get; }
        public bool Firing { get; }
        public int Ammo { get; }
        public int BurstRemaining { get; }
        public double BurstTimer { get; }
        public double BurstInterval { get; }
        public double CooldownProgress { get; }
        public bool CoolingDown { get; }
        public bool Charging { get; }
        public bool Charged { get; }
        public double Charge { get; }
        public bool Reloading { get; }
        public double ReloadProgress { get; }
        public double AmmoIntervalProgress { get; }
    }

    public sealed class AetheriaRuntimeBehaviorStateSnapshot
    {
        public AetheriaRuntimeBehaviorStateSnapshot(
            string ownerKind,
            int ownerIndex,
            int behaviorIndex,
            string behaviorKind,
            bool pinging,
            double pingCooldown,
            double pingLerp,
            double pingRadius,
            int pingedEntityCount,
            double radiatorTemperature,
            double emissivity,
            double pumpedHeat,
            double wasteHeat,
            double energyUsage,
            double reactorDraw,
            double reactorLoadRatio,
            double capacitorCharge,
            double capacitorCapacity,
            double capacitorEfficiency)
        {
            OwnerKind = ownerKind;
            OwnerIndex = ownerIndex;
            BehaviorIndex = behaviorIndex;
            BehaviorKind = behaviorKind;
            Pinging = pinging;
            PingCooldown = pingCooldown;
            PingLerp = pingLerp;
            PingRadius = pingRadius;
            PingedEntityCount = pingedEntityCount;
            RadiatorTemperature = radiatorTemperature;
            Emissivity = emissivity;
            PumpedHeat = pumpedHeat;
            WasteHeat = wasteHeat;
            EnergyUsage = energyUsage;
            ReactorDraw = reactorDraw;
            ReactorLoadRatio = reactorLoadRatio;
            CapacitorCharge = capacitorCharge;
            CapacitorCapacity = capacitorCapacity;
            CapacitorEfficiency = capacitorEfficiency;
        }

        public string OwnerKind { get; }
        public int OwnerIndex { get; }
        public int BehaviorIndex { get; }
        public string BehaviorKind { get; }
        public bool Pinging { get; }
        public double PingCooldown { get; }
        public double PingLerp { get; }
        public double PingRadius { get; }
        public int PingedEntityCount { get; }
        public double RadiatorTemperature { get; }
        public double Emissivity { get; }
        public double PumpedHeat { get; }
        public double WasteHeat { get; }
        public double EnergyUsage { get; }
        public double ReactorDraw { get; }
        public double ReactorLoadRatio { get; }
        public double CapacitorCharge { get; }
        public double CapacitorCapacity { get; }
        public double CapacitorEfficiency { get; }
    }
}
