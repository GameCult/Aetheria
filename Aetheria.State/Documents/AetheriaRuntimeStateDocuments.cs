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
    [CultReference(typeof(AetheriaRunState))]
    public string ActiveRunKey { get; set; } = "";

    [Key(2)]
    public string LastUpdatedAtUtc { get; set; } = "";

    [Key(3)]
    public string PlayerName { get; set; } = "";

    [Key(4)]
    public bool TutorialPassed { get; set; }

    [Key(5)]
    public AetheriaStoryFileHash[] StoryFileHashes { get; set; } = [];

    [Key(6)]
    public AetheriaPlayerGameplaySettings Gameplay { get; set; } = new();

    [Key(7)]
    public AetheriaPlayerGraphicsSettings Graphics { get; set; } = new();

    [Key(8)]
    public AetheriaPlayerInputSettings Input { get; set; } = new();
}

[MessagePackObject]
public sealed class AetheriaStoryFileHash
{
    [Key(0)]
    public string StoryPath { get; set; } = "";

    [Key(1)]
    public string Hash { get; set; } = "";
}

[MessagePackObject]
public sealed class AetheriaPlayerGameplaySettings
{
    [Key(0)]
    public string TemperatureUnit { get; set; } = "Celsius";

    [Key(1)]
    public int SignificantDigits { get; set; } = 3;
}

[MessagePackObject]
public sealed class AetheriaPlayerGraphicsSettings
{
    [Key(0)]
    public string NebulaQuality { get; set; } = "Normal";

    [Key(1)]
    public bool ShowAsteroidsInMinimap { get; set; }
}

[MessagePackObject]
public sealed class AetheriaPlayerInputSettings
{
    [Key(0)]
    public AetheriaInputBindingOverride[] BindingOverrides { get; set; } = [];

    [Key(1)]
    public string[] ActionBarInputs { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaInputBindingOverride
{
    [Key(0)]
    public string ActionName { get; set; } = "";

    [Key(1)]
    public int BindingIndex { get; set; }

    [Key(2)]
    public string BindingPath { get; set; } = "";
}

[CultDocument("aetheria.loadout_template", "aetheria.loadout_template.v1")]
[MessagePackObject]
public sealed class AetheriaLoadoutTemplate
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    public string OwnerPlayerKey { get; set; } = "";

    [Key(2)]
    public AetheriaEntityLoadout RootEntity { get; set; } = new();

    [Key(3)]
    public string CreatedAtUtc { get; set; } = "";

    [Key(4)]
    public string UpdatedAtUtc { get; set; } = "";
}

[MessagePackObject]
public sealed class AetheriaEntityLoadout
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    public string Kind { get; set; } = "";

    [Key(2)]
    public string FactionKey { get; set; } = "";

    [Key(3)]
    public AetheriaLoadoutItem Hull { get; set; } = new();

    [Key(4)]
    public AetheriaLoadoutItemSlot[] Equipment { get; set; } = [];

    [Key(5)]
    public AetheriaLoadoutItemSlot[] CargoBays { get; set; } = [];

    [Key(6)]
    public AetheriaLoadoutItemSlot[] DockingBays { get; set; } = [];

    [Key(7)]
    public AetheriaCargoBayLoadout[] CargoContents { get; set; } = [];

    [Key(8)]
    public AetheriaCargoBayLoadout[] DockingBayContents { get; set; } = [];

    [Key(9)]
    public int[] DockingBayAssignments { get; set; } = [];

    [Key(10)]
    public int[][] WeaponGroups { get; set; } = [];

    [Key(11)]
    public AetheriaEntityLoadout[] Children { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaLoadoutItem
{
    [Key(0)]
    [CultReference(typeof(AetheriaItemDefinition))]
    public string ItemKey { get; set; } = "";

    [Key(1)]
    public double Quality { get; set; } = 1.0;

    [Key(2)]
    public double Durability { get; set; } = 1.0;

    [Key(3)]
    public int Quantity { get; set; } = 1;
}

[MessagePackObject]
public sealed class AetheriaLoadoutItemSlot
{
    [Key(0)]
    public AetheriaGridCoord Position { get; set; } = new();

    [Key(1)]
    public AetheriaLoadoutItem Item { get; set; } = new();
}

[MessagePackObject]
public sealed class AetheriaCargoBayLoadout
{
    [Key(0)]
    public AetheriaLoadoutItemSlot[] Items { get; set; } = [];
}

[CultDocument("aetheria.run_state", "aetheria.run_state.v1")]
[MessagePackObject]
public sealed class AetheriaRunState
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
    [CultReference(typeof(AetheriaZoneState), many: true)]
    public string[] ZoneKeys { get; set; } = [];

    [Key(8)]
    public AetheriaActionBarBinding[] ActionBarBindings { get; set; } = [];

    [Key(9)]
    public AetheriaFactionRelationshipState[] FactionRelationships { get; set; } = [];

    [Key(10)]
    public string UpdatedAtUtc { get; set; } = "";
}

[CultDocument("aetheria.zone_state", "aetheria.zone_state.v1")]
[MessagePackObject]
public sealed class AetheriaZoneState
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
    public string ControlPath { get; set; } = "";

    [Key(1)]
    public string Kind { get; set; } = "";

    [Key(2)]
    public string TargetKey { get; set; } = "";

    [Key(3)]
    public int EquipmentIndex { get; set; } = -1;

    [Key(4)]
    public int BehaviorIndex { get; set; } = -1;

    [Key(5)]
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
