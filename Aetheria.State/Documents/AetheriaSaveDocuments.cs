using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.player_settings", "aetheria.player_settings.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaPlayerSettings
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria player settings";

    [Key(1)]
    [CultReference(typeof(AetheriaSavedRun))]
    public string ActiveRunKey { get; set; } = "";

    [Key(2)]
    public string LastUpdatedAtUtc { get; set; } = "";
}

[CultDocument("aetheria.saved_run", "aetheria.saved_run.v1")]
[MessagePackObject]
public sealed class AetheriaSavedRun
{
    [Key(0)]
    [CultName]
    public string RunId { get; set; } = "";

    [Key(1)]
    public bool IsTutorial { get; set; }

    [Key(2)]
    public int EntranceZoneIndex { get; set; }

    [Key(3)]
    public int ExitZoneIndex { get; set; }

    [Key(4)]
    public int CurrentZoneIndex { get; set; }

    [Key(5)]
    public int CurrentZoneEntityIndex { get; set; }

    [Key(6)]
    public int[] DiscoveredZoneIndices { get; set; } = [];

    [Key(7)]
    [CultReference(typeof(AetheriaSavedZone), many: true)]
    public string[] ZoneKeys { get; set; } = [];

    [Key(8)]
    public AetheriaActionBarBinding[] ActionBarBindings { get; set; } = [];

    [Key(9)]
    public AetheriaFactionRelationshipState[] FactionRelationships { get; set; } = [];

    [Key(10)]
    public string UpdatedAtUtc { get; set; } = "";
}

[CultDocument("aetheria.saved_zone", "aetheria.saved_zone.v1")]
[MessagePackObject]
public sealed class AetheriaSavedZone
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    public AetheriaVector2 Position { get; set; } = new();

    [Key(2)]
    public int[] AdjacentZoneIndices { get; set; } = [];

    [Key(3)]
    public int[] FactionIndices { get; set; } = [];

    [Key(4)]
    public int OwnerFactionIndex { get; set; } = -1;

    [Key(5)]
    [CultReference(typeof(AetheriaEntitySnapshot), many: true)]
    public string[] EntityKeys { get; set; } = [];
}

[CultDocument("aetheria.entity_snapshot", "aetheria.entity_snapshot.v1")]
[MessagePackObject]
public sealed class AetheriaEntitySnapshot
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    [CultIndex("kind")]
    public string Kind { get; set; } = "";

    [Key(2)]
    public AetheriaVector3 Position { get; set; } = new();

    [Key(3)]
    public AetheriaVector2 Direction { get; set; } = new();

    [Key(4)]
    public string FactionKey { get; set; } = "";

    [Key(5)]
    public string HullItemKey { get; set; } = "";

    [Key(6)]
    public AetheriaEntityItemSlot[] Equipment { get; set; } = [];

    [Key(7)]
    public AetheriaEntityItemSlot[] CargoBays { get; set; } = [];

    [Key(8)]
    public AetheriaEntityItemSlot[] DockingBays { get; set; } = [];

    [Key(9)]
    [CultReference(typeof(AetheriaEntitySnapshot), many: true)]
    public string[] ChildEntityKeys { get; set; } = [];

    [Key(10)]
    public AetheriaWeaponGroupSnapshot[] WeaponGroups { get; set; } = [];

    [Key(11)]
    public AetheriaEntityStatGrid[] StatGrids { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaEntityItemSlot
{
    [Key(0)]
    public AetheriaGridCoord Position { get; set; } = new();

    [Key(1)]
    public string ItemKey { get; set; } = "";
}

[MessagePackObject]
public sealed class AetheriaWeaponGroupSnapshot
{
    [Key(0)]
    public int[] EquipmentIndices { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaEntityStatGrid
{
    [Key(0)]
    public string Name { get; set; } = "";

    [Key(1)]
    public int Width { get; set; }

    [Key(2)]
    public int Height { get; set; }

    [Key(3)]
    public double[] Values { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaActionBarBinding
{
    [Key(0)]
    public string Kind { get; set; } = "";

    [Key(1)]
    public string TargetKey { get; set; } = "";

    [Key(2)]
    public int EquipmentIndex { get; set; } = -1;

    [Key(3)]
    public int BehaviorIndex { get; set; } = -1;

    [Key(4)]
    public int WeaponGroup { get; set; } = -1;
}

[MessagePackObject]
public sealed class AetheriaFactionRelationshipState
{
    [Key(0)]
    public string FactionKey { get; set; } = "";

    [Key(1)]
    public double Standing { get; set; }
}

[MessagePackObject]
public sealed class AetheriaVector2
{
    [Key(0)]
    public double X { get; set; }

    [Key(1)]
    public double Y { get; set; }
}

[MessagePackObject]
public sealed class AetheriaVector3
{
    [Key(0)]
    public double X { get; set; }

    [Key(1)]
    public double Y { get; set; }

    [Key(2)]
    public double Z { get; set; }
}

[MessagePackObject]
public sealed class AetheriaGridCoord
{
    [Key(0)]
    public int X { get; set; }

    [Key(1)]
    public int Y { get; set; }
}
