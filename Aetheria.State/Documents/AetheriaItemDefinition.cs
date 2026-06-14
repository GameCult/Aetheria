using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.item_definition", "aetheria.item_definition.v1")]
[MessagePackObject]
public sealed class AetheriaItemDefinition
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    [CultIndex("category")]
    public string Category { get; set; } = "uncategorized";

    [Key(2)]
    [CultIndex("legacyId")]
    public string LegacyId { get; set; } = "";

    [Key(3)]
    public string Description { get; set; } = "";

    [Key(4)]
    public double Mass { get; set; }

    [Key(5)]
    public double Volume { get; set; }

    [Key(6)]
    public string[] Tags { get; set; } = [];

    [Key(7)]
    [CultIndex("manufacturerLegacyId")]
    public string ManufacturerLegacyId { get; set; } = "";

    [Key(8)]
    public int Price { get; set; }

    [Key(9)]
    public int ShapeWidth { get; set; }

    [Key(10)]
    public int ShapeHeight { get; set; }

    [Key(11)]
    public int OccupiedCells { get; set; }

    [Key(12)]
    [CultIndex("hardpointType")]
    public string HardpointType { get; set; } = "";

    [Key(13)]
    [CultIndex("hullType")]
    public string HullType { get; set; } = "";

    [Key(14)]
    public string[] BehaviorKinds { get; set; } = [];

    [Key(15)]
    public int BehaviorCount { get; set; }

    [Key(16)]
    public int MaxStack { get; set; }

    [Key(17)]
    public bool Stackable { get; set; }

    [Key(18)]
    public double Duration { get; set; }

    [Key(19)]
    public double Durability { get; set; }

    [Key(20)]
    public string WeaponRange { get; set; } = "";

    [Key(21)]
    public string WeaponCaliber { get; set; } = "";

    [Key(22)]
    public string WeaponType { get; set; } = "";

    [Key(23)]
    public string WeaponFireTypes { get; set; } = "";

    [Key(24)]
    public string WeaponModifiers { get; set; } = "";

    [Key(25)]
    public AetheriaShapeCell[] ShapeCells { get; set; } = [];

    [Key(26)]
    public int InteriorShapeWidth { get; set; }

    [Key(27)]
    public int InteriorShapeHeight { get; set; }

    [Key(28)]
    public int InteriorOccupiedCells { get; set; }

    [Key(29)]
    public AetheriaShapeCell[] InteriorShapeCells { get; set; } = [];

    [Key(30)]
    public AetheriaItemHardpoint[] Hardpoints { get; set; } = [];

    [Key(31)]
    public AetheriaBehaviorPayload[] BehaviorPayloads { get; set; } = [];

    [Key(32)]
    public double MinimumTemperature { get; set; }

    [Key(33)]
    public double MaximumTemperature { get; set; }

    [Key(34)]
    public AetheriaCurveKey[] ThermalPerformanceCurveKeys { get; set; } = [];

    [Key(35)]
    public string HullPrefab { get; set; } = "";

    [Key(36)]
    public string SimpleCommodityCategory { get; set; } = "";

    [Key(37)]
    public string CompoundCommodityCategory { get; set; } = "";

    [Key(38)]
    public double SpecificHeat { get; set; } = 1;

    [Key(39)]
    public double Conductivity { get; set; } = 1;

    [Key(40)]
    public double HullGridOffset { get; set; }

    [Key(41)]
    public double HullArmor { get; set; }

    [Key(42)]
    public double HullDrag { get; set; }

    [Key(43)]
    public bool HullCanTow { get; set; }
}

[MessagePackObject]
public sealed class AetheriaShapeCell
{
    [Key(0)]
    public int X { get; set; }

    [Key(1)]
    public int Y { get; set; }
}

[MessagePackObject]
public sealed class AetheriaItemHardpoint
{
    [Key(0)]
    public string Type { get; set; } = "";

    [Key(1)]
    public int PositionX { get; set; }

    [Key(2)]
    public int PositionY { get; set; }

    [Key(3)]
    public int ShapeWidth { get; set; }

    [Key(4)]
    public int ShapeHeight { get; set; }

    [Key(5)]
    public int OccupiedCells { get; set; }

    [Key(6)]
    public AetheriaShapeCell[] ShapeCells { get; set; } = [];

    [Key(7)]
    public string Transform { get; set; } = "";

    [Key(8)]
    public string Rotation { get; set; } = "";

    [Key(9)]
    public double Armor { get; set; }
}

[MessagePackObject]
public sealed class AetheriaCurveKey
{
    [Key(0)]
    public double Time { get; set; }

    [Key(1)]
    public double Value { get; set; }

    [Key(2)]
    public double InTangent { get; set; }

    [Key(3)]
    public double OutTangent { get; set; }
}

[MessagePackObject]
public sealed class AetheriaBehaviorPayload
{
    [Key(0)]
    public int UnionKey { get; set; }

    [Key(1)]
    public string Kind { get; set; } = "";

    [Key(2)]
    public int Group { get; set; }

    [Key(3)]
    public AetheriaBehaviorField[] Fields { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaBehaviorField
{
    [Key(0)]
    public int Key { get; set; }

    [Key(1)]
    public AetheriaBehaviorValue Value { get; set; } = new();
}

[MessagePackObject]
public sealed class AetheriaBehaviorMapEntry
{
    [Key(0)]
    public string Key { get; set; } = "";

    [Key(1)]
    public AetheriaBehaviorValue Value { get; set; } = new();
}

[MessagePackObject]
public sealed class AetheriaBehaviorValue
{
    [Key(0)]
    public string Kind { get; set; } = "nil";

    [Key(1)]
    public string StringValue { get; set; } = "";

    [Key(2)]
    public double NumberValue { get; set; }

    [Key(3)]
    public bool BoolValue { get; set; }

    [Key(4)]
    public string LegacyIdValue { get; set; } = "";

    [Key(5)]
    public AetheriaBehaviorValue[] Children { get; set; } = [];

    [Key(6)]
    public AetheriaBehaviorMapEntry[] MapEntries { get; set; } = [];
}
