using Aetheria.State.Documents;

namespace Aetheria.State.Unity;

public sealed class AetheriaRuntimeCatalogSnapshot
{
    private readonly Dictionary<string, AetheriaRuntimeCatalogItem> _itemsByLegacyId;
    private readonly Dictionary<string, AetheriaRuntimeCorporation> _corporationsByLegacyId;
    private readonly Dictionary<string, AetheriaRuntimeNameFile> _nameFilesByLegacyId;

    private AetheriaRuntimeCatalogSnapshot(
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

    public static AetheriaRuntimeCatalogSnapshot FromCatalog(AetheriaCatalogSnapshot catalog)
    {
        return new AetheriaRuntimeCatalogSnapshot(
            catalog.Items.Select(AetheriaRuntimeCatalogItem.FromState).ToArray(),
            catalog.Corporations.Select(AetheriaRuntimeCorporation.FromState).ToArray(),
            catalog.NameFiles.Select(AetheriaRuntimeNameFile.FromState).ToArray());
    }

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
            ? []
            : Items.Where(item => item.BehaviorKinds.Contains(behaviorKind, StringComparer.OrdinalIgnoreCase));
    }

    public IEnumerable<AetheriaRuntimeCatalogItem> FindItemsByHardpoint(string hardpointType)
    {
        return string.IsNullOrWhiteSpace(hardpointType)
            ? []
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
        string hardpointType,
        string hullType,
        IReadOnlyList<string> behaviorKinds,
        int maxStack,
        double durability,
        string weaponRange,
        string weaponCaliber,
        string weaponType,
        string weaponFireTypes,
        string weaponModifiers)
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

    public static AetheriaRuntimeCatalogItem FromState(AetheriaItemDefinition item)
    {
        return new AetheriaRuntimeCatalogItem(
            item.LegacyId,
            item.Name,
            item.Category,
            item.Description,
            item.ManufacturerLegacyId,
            item.Price,
            item.Mass,
            item.Volume,
            item.ShapeWidth,
            item.ShapeHeight,
            item.OccupiedCells,
            item.HardpointType,
            item.HullType,
            item.BehaviorKinds,
            item.MaxStack,
            item.Durability,
            item.WeaponRange,
            item.WeaponCaliber,
            item.WeaponType,
            item.WeaponFireTypes,
            item.WeaponModifiers);
    }
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
        int allegianceCount)
    {
        LegacyId = legacyId;
        Name = name;
        ShortName = shortName;
        Description = description;
        GeonameFileLegacyId = geonameFileLegacyId;
        BossHullLegacyId = bossHullLegacyId;
        InfluenceDistance = influenceDistance;
        AllegianceCount = allegianceCount;
    }

    public string LegacyId { get; }
    public string Name { get; }
    public string ShortName { get; }
    public string Description { get; }
    public string GeonameFileLegacyId { get; }
    public string BossHullLegacyId { get; }
    public int InfluenceDistance { get; }
    public int AllegianceCount { get; }

    public static AetheriaRuntimeCorporation FromState(AetheriaCorporation corporation)
    {
        return new AetheriaRuntimeCorporation(
            corporation.LegacyId,
            corporation.Name,
            corporation.ShortName,
            corporation.Description,
            corporation.GeonameFileLegacyId,
            corporation.BossHullLegacyId,
            corporation.InfluenceDistance,
            corporation.AllegianceCount);
    }
}

public sealed class AetheriaRuntimeNameFile
{
    public AetheriaRuntimeNameFile(
        string legacyId,
        string name,
        int nameCount,
        IReadOnlyList<string> sampleNames)
    {
        LegacyId = legacyId;
        Name = name;
        NameCount = nameCount;
        SampleNames = sampleNames;
    }

    public string LegacyId { get; }
    public string Name { get; }
    public int NameCount { get; }
    public IReadOnlyList<string> SampleNames { get; }

    public static AetheriaRuntimeNameFile FromState(AetheriaNameFile nameFile)
    {
        return new AetheriaRuntimeNameFile(
            nameFile.LegacyId,
            nameFile.Name,
            nameFile.NameCount,
            nameFile.SampleNames);
    }
}
