using Aetheria.State;
using Aetheria.State.Documents;

internal static class AetheriaDaemonNativeCatalog
{
    private const string ZenithHullLegacyId = "82efc0a5-1ba5-4ff3-a281-b2e6e247521d";
    private const string MediumDockingBayLegacyId = "3e930a2c-ac72-4385-98aa-1c5b0b90db46";
    private const string DockyardHullLegacyId = "ca098005-8cc8-47f4-be99-7bc842805359";
    private const string DockyardBayLegacyId = "8ec30f8d-8536-48b4-bd64-65f29f229895";

    public static string DockyardHullItemKey => ItemKey(DockyardHullLegacyId);
    public static string DockyardBayItemKey => ItemKey(DockyardBayLegacyId);

    public static async Task EnsureAsync(AetheriaStateNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var zenith = await node.MutableDocument<AetheriaItemDefinition>(
                AetheriaCatalogKeys.ItemDefinitionFromLegacyId(ZenithHullLegacyId))
            .ReadAsync()
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("The daemon-native dockyard requires the canonical Zenith station hull.");
        var mediumBay = await node.MutableDocument<AetheriaItemDefinition>(
                AetheriaCatalogKeys.ItemDefinitionFromLegacyId(MediumDockingBayLegacyId))
            .ReadAsync()
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("The daemon-native dockyard requires the canonical medium docking bay.");

        await node.MutableDocument<AetheriaItemDefinition>(
                AetheriaCatalogKeys.ItemDefinitionFromLegacyId(DockyardHullLegacyId))
            .ReplaceAsync(CreateDockyardHull(zenith))
            .ConfigureAwait(false);
        await node.MutableDocument<AetheriaItemDefinition>(
                AetheriaCatalogKeys.ItemDefinitionFromLegacyId(DockyardBayLegacyId))
            .ReplaceAsync(CreateDockyardBay(mediumBay))
            .ConfigureAwait(false);
    }

    private static AetheriaItemDefinition CreateDockyardHull(AetheriaItemDefinition source)
    {
        const int width = 24;
        const int height = 24;
        var hardpoints = Enumerable.Range(0, 6)
            .SelectMany(index => new[]
            {
                Hardpoint("DockingBay", index * 4, 0, 4, 4, source.HullArmor),
                Hardpoint("DockingBay", index * 4, 20, 4, 4, source.HullArmor)
            })
            .Concat(new[]
            {
                Hardpoint("Reactor", 10, 10, 4, 4, source.HullArmor),
                Hardpoint("Radiator", 8, 10, 2, 2, source.HullArmor),
                Hardpoint("Radiator", 14, 10, 2, 2, source.HullArmor),
                Hardpoint("Radiator", 10, 14, 2, 2, source.HullArmor),
                Hardpoint("Radiator", 12, 14, 2, 2, source.HullArmor),
                Hardpoint("Sensors", 11, 16, 2, 2, source.HullArmor)
            })
            .ToArray();
        var hullCells = RectangleCells(0, 0, width, height);
        var interiorCells = RectangleCells(1, 1, width - 2, height - 2);

        var hull = Copy(source, DockyardHullLegacyId, "Zenith Dockyard");
        hull.Description = "A high-capacity Zenith station variant built around twelve dedicated docking berths.";
        hull.Mass = source.Mass * 4.5;
        hull.Volume = hullCells.Length;
        hull.Price = checked(source.Price * 5);
        hull.ShapeWidth = width;
        hull.ShapeHeight = height;
        hull.OccupiedCells = hullCells.Length;
        hull.ShapeCells = hullCells;
        hull.InteriorShapeWidth = width;
        hull.InteriorShapeHeight = height;
        hull.InteriorOccupiedCells = interiorCells.Length;
        hull.InteriorShapeCells = interiorCells;
        hull.Hardpoints = hardpoints;
        hull.HullDrag = source.HullDrag * 1.75;
        hull.Tags = Tags(
            source.Tags,
            "origin:aetheria-daemon",
            "station-role:dockyard",
            "docking-capacity:12",
            $"presentation-hull-item:{ItemKey(ZenithHullLegacyId)}");
        return hull;
    }

    private static AetheriaItemDefinition CreateDockyardBay(AetheriaItemDefinition source)
    {
        var bay = Copy(source, DockyardBayLegacyId, "Dockyard Berth");
        bay.Description = "A medium docking bay packaged for a dedicated station docking hardpoint.";
        bay.HardpointType = "DockingBay";
        bay.Price = checked(source.Price * 2);
        bay.Tags = Tags(source.Tags, "origin:aetheria-daemon", "station-role:dockyard");
        return bay;
    }

    private static AetheriaItemDefinition Copy(AetheriaItemDefinition source, string legacyId, string name) => new()
    {
        Name = name,
        Category = source.Category,
        LegacyId = legacyId,
        Description = source.Description,
        Mass = source.Mass,
        Volume = source.Volume,
        Tags = (source.Tags ?? []).ToArray(),
        ManufacturerLegacyId = source.ManufacturerLegacyId,
        Price = source.Price,
        ShapeWidth = source.ShapeWidth,
        ShapeHeight = source.ShapeHeight,
        OccupiedCells = source.OccupiedCells,
        HardpointType = source.HardpointType,
        HullType = source.HullType,
        BehaviorKinds = (source.BehaviorKinds ?? []).ToArray(),
        BehaviorCount = source.BehaviorCount,
        MaxStack = source.MaxStack,
        Stackable = source.Stackable,
        Duration = source.Duration,
        Durability = source.Durability,
        WeaponRange = source.WeaponRange,
        WeaponCaliber = source.WeaponCaliber,
        WeaponType = source.WeaponType,
        WeaponFireTypes = source.WeaponFireTypes,
        WeaponModifiers = source.WeaponModifiers,
        ShapeCells = Clone(source.ShapeCells),
        InteriorShapeWidth = source.InteriorShapeWidth,
        InteriorShapeHeight = source.InteriorShapeHeight,
        InteriorOccupiedCells = source.InteriorOccupiedCells,
        InteriorShapeCells = Clone(source.InteriorShapeCells),
        Hardpoints = (source.Hardpoints ?? []).Select(Clone).ToArray(),
        BehaviorPayloads = source.BehaviorPayloads ?? [],
        MinimumTemperature = source.MinimumTemperature,
        MaximumTemperature = source.MaximumTemperature,
        ThermalPerformanceCurveKeys = source.ThermalPerformanceCurveKeys ?? [],
        HullPrefab = source.HullPrefab,
        SimpleCommodityCategory = source.SimpleCommodityCategory,
        CompoundCommodityCategory = source.CompoundCommodityCategory,
        SpecificHeat = source.SpecificHeat,
        Conductivity = source.Conductivity,
        HullGridOffset = source.HullGridOffset,
        HullArmor = source.HullArmor,
        HullDrag = source.HullDrag,
        HullCanTow = source.HullCanTow,
        DockingMaxSizeX = source.DockingMaxSizeX,
        DockingMaxSizeY = source.DockingMaxSizeY,
        ActionBarIcon = source.ActionBarIcon,
        ThermalResilience = source.ThermalResilience,
        AudioStats = source.AudioStats ?? [],
        EffectivenessCurveKeys = source.EffectivenessCurveKeys ?? []
    };

    private static AetheriaItemHardpoint Clone(AetheriaItemHardpoint source) => new()
    {
        Type = source.Type,
        PositionX = source.PositionX,
        PositionY = source.PositionY,
        ShapeWidth = source.ShapeWidth,
        ShapeHeight = source.ShapeHeight,
        OccupiedCells = source.OccupiedCells,
        ShapeCells = Clone(source.ShapeCells),
        Transform = source.Transform,
        Rotation = source.Rotation,
        Armor = source.Armor
    };

    private static AetheriaItemHardpoint Hardpoint(
        string type,
        int x,
        int y,
        int width,
        int height,
        double armor) => new()
    {
        Type = type,
        PositionX = x,
        PositionY = y,
        ShapeWidth = width,
        ShapeHeight = height,
        OccupiedCells = width * height,
        ShapeCells = RectangleCells(0, 0, width, height),
        Rotation = "None",
        Armor = armor
    };

    private static AetheriaShapeCell[] RectangleCells(int x, int y, int width, int height) =>
        Enumerable.Range(y, height)
            .SelectMany(row => Enumerable.Range(x, width)
                .Select(column => new AetheriaShapeCell { X = column, Y = row }))
            .ToArray();

    private static AetheriaShapeCell[] Clone(IEnumerable<AetheriaShapeCell>? cells) =>
        (cells ?? []).Select(cell => new AetheriaShapeCell { X = cell.X, Y = cell.Y }).ToArray();

    private static string[] Tags(IEnumerable<string>? tags, params string[] added) =>
        (tags ?? []).Concat(added).Distinct(StringComparer.Ordinal).ToArray();

    private static string ItemKey(string legacyId) => $"aetheria.item_definition:legacy:{legacyId}";
}
