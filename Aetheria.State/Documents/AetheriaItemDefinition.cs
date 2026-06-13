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
}

[MessagePackObject]
public sealed class AetheriaShapeCell
{
    [Key(0)]
    public int X { get; set; }

    [Key(1)]
    public int Y { get; set; }
}
