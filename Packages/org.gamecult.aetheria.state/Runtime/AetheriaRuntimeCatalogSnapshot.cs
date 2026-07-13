using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [CultDocument("gamecult.aetheria.runtime_catalog", "gamecult.aetheria.runtime_catalog.v1")]
    public sealed class AetheriaRuntimeCatalogSnapshot
    {
        public const string SchemaId = "gamecult.aetheria.runtime_catalog.v1";
        public const string DocumentName = "aetheria.runtime_catalog";

        public AetheriaRuntimeCatalogSnapshot()
        {
        }

        public AetheriaRuntimeCatalogSnapshot(
            AetheriaRuntimeCatalogItem[] items,
            AetheriaRuntimeCorporation[] corporations,
            AetheriaRuntimeNameFile[] nameFiles,
            AetheriaRuntimeTradeValueSettings? tradeValueSettings = null)
        {
            Items = items ?? Array.Empty<AetheriaRuntimeCatalogItem>();
            Corporations = corporations ?? Array.Empty<AetheriaRuntimeCorporation>();
            NameFiles = nameFiles ?? Array.Empty<AetheriaRuntimeNameFile>();
            TradeValueSettings = tradeValueSettings ?? AetheriaRuntimeTradeValueSettings.Default;
        }

        [Key(0)]
        [CultName]
        public string CatalogId { get; set; } = DocumentName;

        [Key(1)]
        public IReadOnlyList<AetheriaRuntimeCatalogItem> Items { get; set; } = Array.Empty<AetheriaRuntimeCatalogItem>();

        [IgnoreMember]
        public IReadOnlyList<AetheriaRuntimeCatalogItem> TradeItems =>
            Items.Where(item => item.Price > 0).ToArray();

        [IgnoreMember]
        public IReadOnlyList<AetheriaRuntimeCatalogItem> EquipmentItems =>
            Items.Where(item => !string.IsNullOrWhiteSpace(item.HardpointType)).ToArray();

        [Key(2)]
        public IReadOnlyList<AetheriaRuntimeCorporation> Corporations { get; set; } = Array.Empty<AetheriaRuntimeCorporation>();

        [Key(3)]
        public IReadOnlyList<AetheriaRuntimeNameFile> NameFiles { get; set; } = Array.Empty<AetheriaRuntimeNameFile>();

        [Key(4)]
        public AetheriaRuntimeTradeValueSettings TradeValueSettings { get; set; } = AetheriaRuntimeTradeValueSettings.Default;

        public AetheriaRuntimeCatalogItem? FindItem(string itemKey)
        {
            return TryGet(Items, itemKey, item => item.ItemKey);
        }

        public AetheriaRuntimeCatalogItem? FindItem<T>(T? item, Func<T, string?> itemKey) where T : class
        {
            if (item == null || itemKey == null)
                return null;

            return FindItem(itemKey(item) ?? "");
        }

        public AetheriaRuntimeCorporation? FindCorporation(string corporationKey)
        {
            return TryGet(Corporations, corporationKey, corporation => corporation.CorporationKey);
        }

        public AetheriaRuntimeNameFile? FindNameFile(string nameFileKey)
        {
            return TryGet(NameFiles, nameFileKey, nameFile => nameFile.NameFileKey);
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
            return FindCorporation(item.ManufacturerKey);
        }

        public AetheriaRuntimeNameFile? GetNameFile(AetheriaRuntimeCorporation corporation)
        {
            return FindNameFile(corporation.GeonameFileKey);
        }

        private static T? TryGet<T>(IEnumerable<T> values, string key, Func<T, string> getKey) where T : class
        {
            return string.IsNullOrWhiteSpace(key)
                ? null
                : values.FirstOrDefault(value => string.Equals(getKey(value), key, StringComparison.OrdinalIgnoreCase));
        }
    }

    [CultDocument("gamecult.aetheria.loadout_templates", "gamecult.aetheria.loadout_templates.v1")]
    public sealed class AetheriaRuntimeLoadoutTemplatesDocument
    {
        public const string SchemaId = "gamecult.aetheria.loadout_templates.v1";
        public const string DocumentName = "aetheria.loadout_templates";

        public AetheriaRuntimeLoadoutTemplatesDocument()
        {
        }

        public AetheriaRuntimeLoadoutTemplatesDocument(
            IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>? templates = null)
        {
            Templates = templates ?? Array.Empty<AetheriaRuntimeLoadoutTemplateSnapshot>();
        }

        [Key(0)]
        [CultName]
        public string CatalogId { get; set; } = DocumentName;

        [Key(1)]
        public IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot> Templates { get; set; } =
            Array.Empty<AetheriaRuntimeLoadoutTemplateSnapshot>();
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeCatalogItem
    {
        public AetheriaRuntimeCatalogItem(
            string itemKey,
            string name,
            string category,
            string description,
            string manufacturerKey,
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
            string compoundCommodityCategory,
            AetheriaRuntimeBehaviorValue? hullCapacity = null)
        {
            ItemKey = itemKey;
            Name = name;
            Category = category;
            Description = description;
            ManufacturerKey = manufacturerKey;
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
            HullCapacity = hullCapacity ?? new AetheriaRuntimeBehaviorValue(
                "", "", 0, false, "", "",
                Array.Empty<AetheriaRuntimeBehaviorValue>(),
                Array.Empty<AetheriaRuntimeBehaviorMapEntry>());
        }

        public string ItemKey { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string ManufacturerKey { get; set; }
        public int Price { get; set; }
        public double Mass { get; set; }
        public double SpecificHeat { get; set; }
        public double Conductivity { get; set; }
        public double Volume { get; set; }
        public AetheriaRuntimeBehaviorValue HullCapacity { get; set; }
        public int ShapeWidth { get; set; }
        public int ShapeHeight { get; set; }
        public int OccupiedCells { get; set; }
        public IReadOnlyList<AetheriaRuntimeShapeCell> ShapeCells { get; set; }
        public int InteriorShapeWidth { get; set; }
        public int InteriorShapeHeight { get; set; }
        public int InteriorOccupiedCells { get; set; }
        public IReadOnlyList<AetheriaRuntimeShapeCell> InteriorShapeCells { get; set; }
        public IReadOnlyList<AetheriaRuntimeHardpoint> Hardpoints { get; set; }
        public IReadOnlyList<AetheriaRuntimeBehaviorPayload> BehaviorPayloads { get; set; }
        public string HardpointType { get; set; }
        public string HullType { get; set; }
        public IReadOnlyList<string> BehaviorKinds { get; set; }
        public int MaxStack { get; set; }
        public bool Stackable { get; set; }
        public double Duration { get; set; }
        public double Durability { get; set; }
        public string WeaponRange { get; set; }
        public string WeaponCaliber { get; set; }
        public string WeaponType { get; set; }
        public string WeaponFireTypes { get; set; }
        public string WeaponModifiers { get; set; }
        public double MinimumTemperature { get; set; }
        public double MaximumTemperature { get; set; }
        public IReadOnlyList<AetheriaRuntimeCurveKey> ThermalPerformanceCurveKeys { get; set; }
        public string HullPrefab { get; set; }
        public double ThermalResilience { get; set; }
        public double HullGridOffset { get; set; }
        public double HullArmor { get; set; }
        public double HullDrag { get; set; }
        public bool HullCanTow { get; set; }
        public int DockingMaxSizeX { get; set; }
        public int DockingMaxSizeY { get; set; }
        public string ActionBarIcon { get; set; }
        public IReadOnlyList<AetheriaRuntimeAudioStat> AudioStats { get; set; }
        public IReadOnlyList<AetheriaRuntimeCurveKey> EffectivenessCurveKeys { get; set; }
        public string SimpleCommodityCategory { get; set; }
        public string CompoundCommodityCategory { get; set; }

        public bool TryGetHardpointType<TEnum>(out TEnum hardpointType) where TEnum : struct
        {
            hardpointType = default;
            return !string.IsNullOrWhiteSpace(HardpointType) &&
                   Enum.TryParse(HardpointType, true, out hardpointType);
        }

        public bool TryGetSimpleCommodityCategory<TEnum>(out TEnum category) where TEnum : struct
        {
            category = default;
            return !string.IsNullOrWhiteSpace(SimpleCommodityCategory) &&
                   Enum.TryParse(SimpleCommodityCategory, true, out category);
        }

        public bool TryGetCompoundCommodityCategory<TEnum>(out TEnum category) where TEnum : struct
        {
            category = default;
            return !string.IsNullOrWhiteSpace(CompoundCommodityCategory) &&
                   Enum.TryParse(CompoundCommodityCategory, true, out category);
        }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeAudioStat
    {
        public AetheriaRuntimeAudioStat(uint parameter, AetheriaRuntimePerformanceStat stat)
        {
            Parameter = parameter;
            Stat = stat;
        }

        public uint Parameter { get; set; }
        public AetheriaRuntimePerformanceStat Stat { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimePerformanceStat
    {
        public AetheriaRuntimePerformanceStat(
            double min,
            double max,
            double heatExponentMultiplier,
            double durabilityExponentMultiplier,
            double qualityExponent,
            AetheriaRuntimeStatRecipe? recipe)
        {
            Min = min;
            Max = max;
            HeatExponentMultiplier = heatExponentMultiplier;
            DurabilityExponentMultiplier = durabilityExponentMultiplier;
            QualityExponent = qualityExponent;
            Recipe = recipe;
        }

        public double Min { get; set; }
        public double Max { get; set; }
        public double HeatExponentMultiplier { get; set; }
        public double DurabilityExponentMultiplier { get; set; }
        public double QualityExponent { get; set; }
        public AetheriaRuntimeStatRecipe? Recipe { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeStatRecipe
    {
        public AetheriaRuntimeStatRecipe(double baseValue, IReadOnlyList<AetheriaRuntimeStatRecipeModifier> modifiers)
        {
            BaseValue = baseValue;
            Modifiers = modifiers ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>();
        }

        public double BaseValue { get; set; }
        public IReadOnlyList<AetheriaRuntimeStatRecipeModifier> Modifiers { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeStatRecipeModifier
    {
        public AetheriaRuntimeStatRecipeModifier(
            string condition,
            string operation,
            double amount,
            IReadOnlyList<AetheriaRuntimeCurveKey> curveKeys,
            bool enabled)
        {
            Condition = condition ?? "";
            Operation = operation ?? "";
            Amount = amount;
            CurveKeys = curveKeys ?? Array.Empty<AetheriaRuntimeCurveKey>();
            Enabled = enabled;
        }

        public string Condition { get; set; }
        public string Operation { get; set; }
        public double Amount { get; set; }
        public IReadOnlyList<AetheriaRuntimeCurveKey> CurveKeys { get; set; }
        public bool Enabled { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeCurveKey
    {
        public AetheriaRuntimeCurveKey(double time, double value, double inTangent, double outTangent)
        {
            Time = time;
            Value = value;
            InTangent = inTangent;
            OutTangent = outTangent;
        }

        public double Time { get; set; }
        public double Value { get; set; }
        public double InTangent { get; set; }
        public double OutTangent { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeShapeCell
    {
        public AetheriaRuntimeShapeCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }

        public int Y { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeBehaviorPayload
    {
        public AetheriaRuntimeBehaviorPayload(
            int unionKey,
            string kind,
            int group,
            IReadOnlyList<AetheriaRuntimeBehaviorField> fields,
            string behaviorId = "")
        {
            UnionKey = unionKey;
            Kind = kind;
            Group = group;
            Fields = fields;
            BehaviorId = behaviorId;
        }

        public int UnionKey { get; set; }

        public string Kind { get; set; }

        public int Group { get; set; }

        public IReadOnlyList<AetheriaRuntimeBehaviorField> Fields { get; set; }
        public string BehaviorId { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeBehaviorField
    {
        public AetheriaRuntimeBehaviorField(int key, AetheriaRuntimeBehaviorValue value)
        {
            Key = key;
            Value = value;
        }

        public int Key { get; set; }

        public AetheriaRuntimeBehaviorValue Value { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeBehaviorMapEntry
    {
        public AetheriaRuntimeBehaviorMapEntry(string key, AetheriaRuntimeBehaviorValue value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }

        public AetheriaRuntimeBehaviorValue Value { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeBehaviorValue
    {
        public AetheriaRuntimeBehaviorValue(
            string kind,
            string stringValue,
            double numberValue,
            bool boolValue,
            string legacyIdValue,
            string itemKeyValue,
            IReadOnlyList<AetheriaRuntimeBehaviorValue> children,
            IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> mapEntries)
        {
            Kind = kind;
            StringValue = stringValue;
            NumberValue = numberValue;
            BoolValue = boolValue;
            LegacyIdValue = legacyIdValue;
            ItemKeyValue = itemKeyValue;
            Children = children;
            MapEntries = mapEntries;
        }

        public string Kind { get; set; }

        public string StringValue { get; set; }

        public double NumberValue { get; set; }

        public bool BoolValue { get; set; }

        public string LegacyIdValue { get; set; }

        public string ItemKeyValue { get; set; }

        public IReadOnlyList<AetheriaRuntimeBehaviorValue> Children { get; set; }

        public IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> MapEntries { get; set; }
    }

    [MessagePackObject(true)]
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

        public string Type { get; set; }

        public int PositionX { get; set; }

        public int PositionY { get; set; }

        public int ShapeWidth { get; set; }

        public int ShapeHeight { get; set; }

        public int OccupiedCells { get; set; }

        public IReadOnlyList<AetheriaRuntimeShapeCell> ShapeCells { get; set; }

        public string Transform { get; set; }

        public string Rotation { get; set; }

        public double Armor { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeCorporation
    {
        public AetheriaRuntimeCorporation(
            string corporationKey,
            string name,
            string shortName,
            string description,
            string geonameFileKey,
            string bossHullItemKey,
            int influenceDistance,
            int allegianceCount,
            IReadOnlyList<AetheriaRuntimeCorporationAllegiance> allegiances)
        {
            CorporationKey = corporationKey;
            Name = name;
            ShortName = shortName;
            Description = description;
            GeonameFileKey = geonameFileKey;
            BossHullItemKey = bossHullItemKey;
            InfluenceDistance = influenceDistance;
            AllegianceCount = allegianceCount;
            Allegiances = allegiances;
        }

        public string CorporationKey { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Description { get; set; }
        public string GeonameFileKey { get; set; }
        public string BossHullItemKey { get; set; }
        public int InfluenceDistance { get; set; }
        public int AllegianceCount { get; set; }
        public IReadOnlyList<AetheriaRuntimeCorporationAllegiance> Allegiances { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeCorporationAllegiance
    {
        public AetheriaRuntimeCorporationAllegiance(string corporationKey, double weight)
        {
            CorporationKey = corporationKey;
            Weight = weight;
        }

        public string CorporationKey { get; set; }
        public double Weight { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeNameFile
    {
        public AetheriaRuntimeNameFile(
            string nameFileKey,
            string name,
            int nameCount,
            IReadOnlyList<string> sampleNames,
            IReadOnlyList<string> names)
        {
            NameFileKey = nameFileKey;
            Name = name;
            NameCount = nameCount;
            SampleNames = sampleNames;
            Names = names;
        }

        public string NameFileKey { get; set; }
        public string Name { get; set; }
        public int NameCount { get; set; }
        public IReadOnlyList<string> SampleNames { get; set; }
        public IReadOnlyList<string> Names { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimePlayerSettingsSnapshot
    {
        public AetheriaRuntimePlayerSettingsSnapshot(
            string playerName,
            bool tutorialPassed,
            IReadOnlyList<AetheriaRuntimeStoryFileHash> storyFileHashes,
            string temperatureUnit,
            int significantDigits,
            double defaultShutdownPerformance,
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
            DefaultShutdownPerformance = defaultShutdownPerformance;
            NebulaQuality = nebulaQuality;
            ShowAsteroidsInMinimap = showAsteroidsInMinimap;
            BindingOverrides = bindingOverrides;
            ActionBarInputs = actionBarInputs;
        }

        public string PlayerName { get; set; }
        public bool TutorialPassed { get; set; }
        public IReadOnlyList<AetheriaRuntimeStoryFileHash> StoryFileHashes { get; set; }
        public string TemperatureUnit { get; set; }
        public int SignificantDigits { get; set; }
        public double DefaultShutdownPerformance { get; set; }
        public string NebulaQuality { get; set; }
        public bool ShowAsteroidsInMinimap { get; set; }
        public IReadOnlyList<AetheriaRuntimeInputBindingOverride> BindingOverrides { get; set; }
        public IReadOnlyList<string> ActionBarInputs { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeVerseHostSettingsSnapshot
    {
        public AetheriaRuntimeVerseHostSettingsSnapshot(
            string serviceId,
            string verseId,
            string rootVerse,
            string canonicalService,
            string locatedService,
            string cultMeshAddress,
            string title,
            string visibility,
            string lastUpdatedAtUtc)
        {
            ServiceId = serviceId;
            VerseId = verseId;
            RootVerse = rootVerse;
            CanonicalService = canonicalService;
            LocatedService = locatedService;
            CultMeshAddress = cultMeshAddress;
            Title = title;
            Visibility = visibility;
            LastUpdatedAtUtc = lastUpdatedAtUtc;
        }

        public string ServiceId { get; set; }
        public string VerseId { get; set; }
        public string RootVerse { get; set; }
        public string CanonicalService { get; set; }
        public string LocatedService { get; set; }
        public string CultMeshAddress { get; set; }
        public string Title { get; set; }
        public string Visibility { get; set; }
        public string LastUpdatedAtUtc { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeStoryFileHash
    {
        public AetheriaRuntimeStoryFileHash(string storyPath, string hash)
        {
            StoryPath = storyPath;
            Hash = hash;
        }

        public string StoryPath { get; set; }
        public string Hash { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeInputBindingOverride
    {
        public AetheriaRuntimeInputBindingOverride(string actionName, int bindingIndex, string bindingPath)
        {
            ActionName = actionName;
            BindingIndex = bindingIndex;
            BindingPath = bindingPath;
        }

        public string ActionName { get; set; }
        public int BindingIndex { get; set; }
        public string BindingPath { get; set; }
    }

    [MessagePackObject(true)]
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

        public string Name { get; set; }
        public string OwnerPlayerKey { get; set; }
        public AetheriaRuntimeEntityLoadoutSnapshot RootEntity { get; set; }
        public string CreatedAtUtc { get; set; }
        public string UpdatedAtUtc { get; set; }
    }

    [MessagePackObject(true)]
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

        public string Name { get; set; }
        public string Kind { get; set; }
        public string FactionKey { get; set; }
        public AetheriaRuntimeLoadoutItemSnapshot Hull { get; set; }
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> Equipment { get; set; }
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> CargoBays { get; set; }
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> DockingBays { get; set; }
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> CargoContents { get; set; }
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> DockingBayContents { get; set; }
        public IReadOnlyList<int> DockingBayAssignments { get; set; }
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; set; }
        public IReadOnlyList<AetheriaRuntimeEntityLoadoutSnapshot> Children { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeLoadoutItemSnapshot
    {
        public AetheriaRuntimeLoadoutItemSnapshot(
            string itemKey,
            double quality,
            double durability,
            int quantity,
            bool enabled,
            bool overrideShutdown,
            double temperature = 0)
        {
            ItemKey = itemKey;
            Quality = quality;
            Durability = durability;
            Quantity = quantity;
            Enabled = enabled;
            OverrideShutdown = overrideShutdown;
            Temperature = temperature;
        }

        public string ItemKey { get; set; }
        public double Quality { get; set; }
        public double Durability { get; set; }
        public int Quantity { get; set; }
        public bool Enabled { get; set; }
        public bool OverrideShutdown { get; set; }
        public double Temperature { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeLoadoutItemSlotSnapshot
    {
        public AetheriaRuntimeLoadoutItemSlotSnapshot(int x, int y, AetheriaRuntimeLoadoutItemSnapshot item)
        {
            X = x;
            Y = y;
            Item = item;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public AetheriaRuntimeLoadoutItemSnapshot Item { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeCargoBayLoadoutSnapshot
    {
        public AetheriaRuntimeCargoBayLoadoutSnapshot(IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> items)
        {
            Items = items;
        }

        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> Items { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeEntitySnapshot
    {
        public AetheriaRuntimeEntitySnapshot(
            string recordKey,
            int entityIndex,
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
            IReadOnlyList<AetheriaRuntimeBehaviorStateSnapshot> behaviorStates,
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> cargoContents,
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> dockingBayContents,
            IReadOnlyList<int> dockingBayAssignments,
            double visibility,
            int visibilitySourceCount,
            IReadOnlyList<AetheriaRuntimeEntityContactSnapshot> contacts,
            double shutdownPerformance)
        {
            RecordKey = recordKey;
            EntityIndex = entityIndex;
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
            CargoContents = cargoContents;
            DockingBayContents = dockingBayContents;
            DockingBayAssignments = dockingBayAssignments;
            Visibility = visibility;
            VisibilitySourceCount = visibilitySourceCount;
            Contacts = contacts;
            ShutdownPerformance = shutdownPerformance;
        }

        public string RecordKey { get; set; }
        public int EntityIndex { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
        public string FactionKey { get; set; }
        public string HullItemKey { get; set; }
        public IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> Equipment { get; set; }
        public IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> CargoBays { get; set; }
        public IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> DockingBays { get; set; }
        public IReadOnlyList<string> ChildEntityKeys { get; set; }
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; set; }
        public IReadOnlyList<AetheriaRuntimeEntityStatGridSnapshot> StatGrids { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public string TargetEntityKey { get; set; }
        public bool IsActive { get; set; }
        public bool HeatsinksEnabled { get; set; }
        public bool OverrideShutdown { get; set; }
        public double TractorPower { get; set; }
        public double Heatstroke { get; set; }
        public double Hypothermia { get; set; }
        public IReadOnlyList<AetheriaRuntimeActiveConsumableSnapshot> ActiveConsumables { get; set; }
        public IReadOnlyList<AetheriaRuntimeBehaviorProgressSnapshot> BehaviorProgress { get; set; }
        public IReadOnlyList<AetheriaRuntimeWeaponStateSnapshot> WeaponStates { get; set; }
        public IReadOnlyList<AetheriaRuntimeBehaviorStateSnapshot> BehaviorStates { get; set; }
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> CargoContents { get; set; }
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> DockingBayContents { get; set; }
        public IReadOnlyList<int> DockingBayAssignments { get; set; }
        public double Visibility { get; set; }
        public int VisibilitySourceCount { get; set; }
        public IReadOnlyList<AetheriaRuntimeEntityContactSnapshot> Contacts { get; set; }
        public double ShutdownPerformance { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeEntityContactSnapshot
    {
        public AetheriaRuntimeEntityContactSnapshot(string targetEntityKey, double infoGathered, bool hostile, bool visible)
        {
            TargetEntityKey = targetEntityKey;
            InfoGathered = infoGathered;
            Hostile = hostile;
            Visible = visible;
        }

        public string TargetEntityKey { get; set; }
        public double InfoGathered { get; set; }
        public bool Hostile { get; set; }
        public bool Visible { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeEntityItemSlotSnapshot
    {
        public AetheriaRuntimeEntityItemSlotSnapshot(
            int x,
            int y,
            string itemKey,
            double quality,
            double durability,
            int quantity,
            bool enabled,
            bool overrideShutdown,
            double temperature = 0)
        {
            X = x;
            Y = y;
            ItemKey = itemKey;
            Quality = quality;
            Durability = durability;
            Quantity = quantity;
            Enabled = enabled;
            OverrideShutdown = overrideShutdown;
            Temperature = temperature;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public string ItemKey { get; set; }
        public double Quality { get; set; }
        public double Durability { get; set; }
        public int Quantity { get; set; }
        public bool Enabled { get; set; }
        public bool OverrideShutdown { get; set; }
        public double Temperature { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeEntityStatGridSnapshot
    {
        public AetheriaRuntimeEntityStatGridSnapshot(string name, int width, int height, IReadOnlyList<double> values)
        {
            Name = name;
            Width = width;
            Height = height;
            Values = values;
        }

        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public IReadOnlyList<double> Values { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeActiveConsumableSnapshot
    {
        public AetheriaRuntimeActiveConsumableSnapshot(
            string itemKey,
            double quality,
            double remainingDuration,
            double duration,
            string effectId = "",
            IReadOnlyList<AetheriaRuntimeConsumableBehaviorStateSnapshot>? behaviorStates = null)
        {
            ItemKey = itemKey;
            Quality = quality;
            RemainingDuration = remainingDuration;
            Duration = duration;
            EffectId = effectId;
            BehaviorStates = behaviorStates ?? Array.Empty<AetheriaRuntimeConsumableBehaviorStateSnapshot>();
        }

        public string ItemKey { get; set; }
        public double Quality { get; set; }
        public double RemainingDuration { get; set; }
        public double Duration { get; set; }
        public string EffectId { get; set; }
        public IReadOnlyList<AetheriaRuntimeConsumableBehaviorStateSnapshot> BehaviorStates { get; set; }
    }

    [MessagePackObject(true)]
    public sealed class AetheriaRuntimeConsumableBehaviorStateSnapshot
    {
        public AetheriaRuntimeConsumableBehaviorStateSnapshot(
            int behaviorIndex,
            string behaviorKind,
            double scalarState,
            string behaviorId = "")
        {
            BehaviorIndex = behaviorIndex;
            BehaviorKind = behaviorKind;
            ScalarState = scalarState;
            BehaviorId = behaviorId;
        }

        public int BehaviorIndex { get; set; }
        public string BehaviorKind { get; set; }
        public double ScalarState { get; set; }
        public string BehaviorId { get; set; }
    }

    [MessagePackObject(true)]
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

        public string OwnerKind { get; set; }
        public int OwnerIndex { get; set; }
        public int BehaviorIndex { get; set; }
        public string BehaviorKind { get; set; }
        public double Progress { get; set; }
    }

    [MessagePackObject(true)]
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
            double ammoIntervalProgress,
            double lockProgress,
            string lockTargetEntityKey,
            double chargeHoldSeconds,
            int chargeRiskChecks,
            double chargeMalfunctionRisk)
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
            LockProgress = lockProgress;
            LockTargetEntityKey = lockTargetEntityKey;
            ChargeHoldSeconds = chargeHoldSeconds;
            ChargeRiskChecks = chargeRiskChecks;
            ChargeMalfunctionRisk = chargeMalfunctionRisk;
        }

        public string OwnerKind { get; set; }
        public int OwnerIndex { get; set; }
        public int BehaviorIndex { get; set; }
        public string BehaviorKind { get; set; }
        public bool Firing { get; set; }
        public int Ammo { get; set; }
        public int BurstRemaining { get; set; }
        public double BurstTimer { get; set; }
        public double BurstInterval { get; set; }
        public double CooldownProgress { get; set; }
        public bool CoolingDown { get; set; }
        public bool Charging { get; set; }
        public bool Charged { get; set; }
        public double Charge { get; set; }
        public bool Reloading { get; set; }
        public double ReloadProgress { get; set; }
        public double AmmoIntervalProgress { get; set; }
        public double LockProgress { get; set; }
        public string LockTargetEntityKey { get; set; }
        public double ChargeHoldSeconds { get; set; }
        public int ChargeRiskChecks { get; set; }
        public double ChargeMalfunctionRisk { get; set; }
    }

    [MessagePackObject(true)]
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
            double capacitorEfficiency,
            double aetherDriveAxisX,
            double aetherDriveAxisY,
            double aetherDriveAxisZ,
            double aetherDriveThrustX,
            double aetherDriveThrustY,
            double aetherDriveThrustZ,
            double aetherDriveRpmX,
            double aetherDriveRpmY,
            double aetherDriveRpmZ,
            double aetherDriveMaximumRpm,
            double aetherDriveThrustDirectionX,
            double aetherDriveThrustDirectionY,
            string resourceScannerTargetBodyKey,
            int resourceScannerAsteroidIndex,
            double resourceScannerScanTime,
            double resourceScannerRange,
            double resourceScannerMinimumDensity,
            double resourceScannerScanDuration,
            string miningToolAsteroidBeltKey,
            int miningToolAsteroidIndex,
            double miningToolRange,
            double thrusterAxis,
            double thrusterThrust,
            double thrusterTorque,
            double shieldEfficiency,
            double shieldEnergyUsage,
            double velocityLimit,
            double thermotoggleTargetTemperature,
            bool switchActivated,
            bool triggerPulled,
            bool statModifierApplied,
            bool statModifierExecuted,
            int statModifierTargetStatCount,
            int turretControllerWeaponCount,
            double turretControllerShotSpeed,
            bool turretControllerPredictShots)
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
            AetherDriveAxisX = aetherDriveAxisX;
            AetherDriveAxisY = aetherDriveAxisY;
            AetherDriveAxisZ = aetherDriveAxisZ;
            AetherDriveThrustX = aetherDriveThrustX;
            AetherDriveThrustY = aetherDriveThrustY;
            AetherDriveThrustZ = aetherDriveThrustZ;
            AetherDriveRpmX = aetherDriveRpmX;
            AetherDriveRpmY = aetherDriveRpmY;
            AetherDriveRpmZ = aetherDriveRpmZ;
            AetherDriveMaximumRpm = aetherDriveMaximumRpm;
            AetherDriveThrustDirectionX = aetherDriveThrustDirectionX;
            AetherDriveThrustDirectionY = aetherDriveThrustDirectionY;
            ResourceScannerTargetBodyKey = resourceScannerTargetBodyKey;
            ResourceScannerAsteroidIndex = resourceScannerAsteroidIndex;
            ResourceScannerScanTime = resourceScannerScanTime;
            ResourceScannerRange = resourceScannerRange;
            ResourceScannerMinimumDensity = resourceScannerMinimumDensity;
            ResourceScannerScanDuration = resourceScannerScanDuration;
            MiningToolAsteroidBeltKey = miningToolAsteroidBeltKey;
            MiningToolAsteroidIndex = miningToolAsteroidIndex;
            MiningToolRange = miningToolRange;
            ThrusterAxis = thrusterAxis;
            ThrusterThrust = thrusterThrust;
            ThrusterTorque = thrusterTorque;
            ShieldEfficiency = shieldEfficiency;
            ShieldEnergyUsage = shieldEnergyUsage;
            VelocityLimit = velocityLimit;
            ThermotoggleTargetTemperature = thermotoggleTargetTemperature;
            SwitchActivated = switchActivated;
            TriggerPulled = triggerPulled;
            StatModifierApplied = statModifierApplied;
            StatModifierExecuted = statModifierExecuted;
            StatModifierTargetStatCount = statModifierTargetStatCount;
            TurretControllerWeaponCount = turretControllerWeaponCount;
            TurretControllerShotSpeed = turretControllerShotSpeed;
            TurretControllerPredictShots = turretControllerPredictShots;
        }

        public string OwnerKind { get; set; }
        public int OwnerIndex { get; set; }
        public int BehaviorIndex { get; set; }
        public string BehaviorKind { get; set; }
        public bool Pinging { get; set; }
        public double PingCooldown { get; set; }
        public double PingLerp { get; set; }
        public double PingRadius { get; set; }
        public int PingedEntityCount { get; set; }
        public double RadiatorTemperature { get; set; }
        public double Emissivity { get; set; }
        public double PumpedHeat { get; set; }
        public double WasteHeat { get; set; }
        public double EnergyUsage { get; set; }
        public double ReactorDraw { get; set; }
        public double ReactorLoadRatio { get; set; }
        public double CapacitorCharge { get; set; }
        public double CapacitorCapacity { get; set; }
        public double CapacitorEfficiency { get; set; }
        public double AetherDriveAxisX { get; set; }
        public double AetherDriveAxisY { get; set; }
        public double AetherDriveAxisZ { get; set; }
        public double AetherDriveThrustX { get; set; }
        public double AetherDriveThrustY { get; set; }
        public double AetherDriveThrustZ { get; set; }
        public double AetherDriveRpmX { get; set; }
        public double AetherDriveRpmY { get; set; }
        public double AetherDriveRpmZ { get; set; }
        public double AetherDriveMaximumRpm { get; set; }
        public double AetherDriveThrustDirectionX { get; set; }
        public double AetherDriveThrustDirectionY { get; set; }
        public string ResourceScannerTargetBodyKey { get; set; }
        public int ResourceScannerAsteroidIndex { get; set; }
        public double ResourceScannerScanTime { get; set; }
        public double ResourceScannerRange { get; set; }
        public double ResourceScannerMinimumDensity { get; set; }
        public double ResourceScannerScanDuration { get; set; }
        public string MiningToolAsteroidBeltKey { get; set; }
        public int MiningToolAsteroidIndex { get; set; }
        public double MiningToolRange { get; set; }
        public double ThrusterAxis { get; set; }
        public double ThrusterThrust { get; set; }
        public double ThrusterTorque { get; set; }
        public double ShieldEfficiency { get; set; }
        public double ShieldEnergyUsage { get; set; }
        public double VelocityLimit { get; set; }
        public double ThermotoggleTargetTemperature { get; set; }
        public bool SwitchActivated { get; set; }
        public bool TriggerPulled { get; set; }
        public bool StatModifierApplied { get; set; }
        public bool StatModifierExecuted { get; set; }
        public int StatModifierTargetStatCount { get; set; }
        public int TurretControllerWeaponCount { get; set; }
        public double TurretControllerShotSpeed { get; set; }
        public bool TurretControllerPredictShots { get; set; }
    }
}
