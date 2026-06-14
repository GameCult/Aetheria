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

    [Key(11)]
    public uint GenerationSeed { get; set; }
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

    [Key(6)]
    public AetheriaOrbitSnapshot[] Orbits { get; set; } = [];

    [Key(7)]
    public AetheriaBodySnapshot[] Bodies { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaOrbitSnapshot
{
    [Key(0)]
    public string OrbitId { get; set; } = "";

    [Key(1)]
    public string ParentId { get; set; } = "";

    [Key(2)]
    public double Distance { get; set; }

    [Key(3)]
    public double Phase { get; set; }

    [Key(4)]
    public AetheriaVector2 FixedPosition { get; set; } = new();
}

[MessagePackObject]
public sealed class AetheriaBodySnapshot
{
    [Key(0)]
    public string BodyId { get; set; } = "";

    [Key(1)]
    public string Kind { get; set; } = "";

    [Key(2)]
    public string Name { get; set; } = "";

    [Key(3)]
    public string OrbitId { get; set; } = "";

    [Key(4)]
    public double Mass { get; set; }

    [Key(5)]
    public AetheriaBodyResource[] Resources { get; set; } = [];

    [Key(6)]
    public double BodyRadiusMultiplier { get; set; } = 1.0;

    [Key(7)]
    public double GravityRadiusMultiplier { get; set; } = 1.0;

    [Key(8)]
    public double GravityDepthMultiplier { get; set; } = 1.0;

    [Key(9)]
    public double GravityDepthExponent { get; set; } = 16.0;

    [Key(10)]
    public AetheriaAsteroidSnapshot[] Asteroids { get; set; } = [];

    [Key(11)]
    public AetheriaGasGiantVisualState GasGiantVisual { get; set; } = new();

    [Key(12)]
    public AetheriaSunVisualState SunVisual { get; set; } = new();
}

[MessagePackObject]
public sealed class AetheriaBodyResource
{
    [Key(0)]
    public string ItemKey { get; set; } = "";

    [Key(1)]
    public double Amount { get; set; }
}

[MessagePackObject]
public sealed class AetheriaAsteroidSnapshot
{
    [Key(0)]
    public double Distance { get; set; }

    [Key(1)]
    public double Phase { get; set; }

    [Key(2)]
    public double Size { get; set; }

    [Key(3)]
    public double RotationSpeed { get; set; }
}

[MessagePackObject]
public sealed class AetheriaGasGiantVisualState
{
    [Key(0)]
    public double FirstOffsetDomainRotationSpeed { get; set; } = 1.0;

    [Key(1)]
    public double FirstOffsetRotationSpeed { get; set; } = 1.0;

    [Key(2)]
    public double SecondOffsetDomainRotationSpeed { get; set; } = 1.0;

    [Key(3)]
    public double SecondOffsetRotationSpeed { get; set; } = 1.0;

    [Key(4)]
    public double AlbedoRotationSpeed { get; set; } = 1.0;

    [Key(5)]
    public double WaveRadiusMultiplier { get; set; } = 1.0;

    [Key(6)]
    public double WaveDepthMultiplier { get; set; } = 1.0;

    [Key(7)]
    public double WaveDepthExponent { get; set; } = 8.0;

    [Key(8)]
    public double WaveSpeedMultiplier { get; set; } = 8.0;

    [Key(9)]
    public string[] MaterialOverrides { get; set; } = [];

    [Key(10)]
    public AetheriaColor[] Colors { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaSunVisualState
{
    [Key(0)]
    public AetheriaVector3 LightColor { get; set; } = new();

    [Key(1)]
    public AetheriaVector3 FogTintColor { get; set; } = new();

    [Key(2)]
    public double LightRadiusMultiplier { get; set; } = 1.0;
}

[MessagePackObject]
public sealed class AetheriaColor
{
    [Key(0)]
    public double X { get; set; }

    [Key(1)]
    public double Y { get; set; }

    [Key(2)]
    public double Z { get; set; }

    [Key(3)]
    public double W { get; set; }
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

    [Key(12)]
    public AetheriaVector2 Velocity { get; set; } = new();

    [Key(13)]
    [CultReference(typeof(AetheriaEntitySnapshot))]
    public string TargetEntityKey { get; set; } = "";

    [Key(14)]
    public bool IsActive { get; set; }

    [Key(15)]
    public bool HeatsinksEnabled { get; set; }

    [Key(16)]
    public bool OverrideShutdown { get; set; }

    [Key(17)]
    public double TractorPower { get; set; }

    [Key(18)]
    public double Heatstroke { get; set; }

    [Key(19)]
    public double Hypothermia { get; set; }

    [Key(20)]
    public AetheriaActiveConsumableSnapshot[] ActiveConsumables { get; set; } = [];

    [Key(21)]
    public AetheriaBehaviorProgressSnapshot[] BehaviorProgress { get; set; } = [];

    [Key(22)]
    public AetheriaWeaponStateSnapshot[] WeaponStates { get; set; } = [];

    [Key(23)]
    public AetheriaBehaviorStateSnapshot[] BehaviorStates { get; set; } = [];

    [Key(24)]
    public AetheriaCargoBayLoadout[] CargoContents { get; set; } = [];

    [Key(25)]
    public AetheriaCargoBayLoadout[] DockingBayContents { get; set; } = [];

    [Key(26)]
    public int[] DockingBayAssignments { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaEntityItemSlot
{
    [Key(0)]
    public AetheriaGridCoord Position { get; set; } = new();

    [Key(1)]
    public string ItemKey { get; set; } = "";

    [Key(2)]
    public double Quality { get; set; } = 1.0;

    [Key(3)]
    public double Durability { get; set; } = 1.0;

    [Key(4)]
    public int Quantity { get; set; } = 1;
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
public sealed class AetheriaActiveConsumableSnapshot
{
    [Key(0)]
    [CultReference(typeof(AetheriaItemDefinition))]
    public string ItemKey { get; set; } = "";

    [Key(1)]
    public double Quality { get; set; } = 1.0;

    [Key(2)]
    public double RemainingDuration { get; set; }

    [Key(3)]
    public double Duration { get; set; }
}

[MessagePackObject]
public sealed class AetheriaBehaviorProgressSnapshot
{
    [Key(0)]
    public string OwnerKind { get; set; } = "";

    [Key(1)]
    public int OwnerIndex { get; set; } = -1;

    [Key(2)]
    public int BehaviorIndex { get; set; } = -1;

    [Key(3)]
    public string BehaviorKind { get; set; } = "";

    [Key(4)]
    public double Progress { get; set; }
}

[MessagePackObject]
public sealed class AetheriaWeaponStateSnapshot
{
    [Key(0)]
    public string OwnerKind { get; set; } = "";

    [Key(1)]
    public int OwnerIndex { get; set; } = -1;

    [Key(2)]
    public int BehaviorIndex { get; set; } = -1;

    [Key(3)]
    public string BehaviorKind { get; set; } = "";

    [Key(4)]
    public bool Firing { get; set; }

    [Key(5)]
    public int Ammo { get; set; }

    [Key(6)]
    public int BurstRemaining { get; set; }

    [Key(7)]
    public double BurstTimer { get; set; }

    [Key(8)]
    public double BurstInterval { get; set; }

    [Key(9)]
    public double CooldownProgress { get; set; }

    [Key(10)]
    public bool CoolingDown { get; set; }

    [Key(11)]
    public bool Charging { get; set; }

    [Key(12)]
    public bool Charged { get; set; }

    [Key(13)]
    public double Charge { get; set; }

    [Key(14)]
    public bool Reloading { get; set; }

    [Key(15)]
    public double ReloadProgress { get; set; }

    [Key(16)]
    public double AmmoIntervalProgress { get; set; }

    [Key(17)]
    public double LockProgress { get; set; }

    [Key(18)]
    public string LockTargetEntityKey { get; set; } = "";
}

[MessagePackObject]
public sealed class AetheriaBehaviorStateSnapshot
{
    [Key(0)]
    public string OwnerKind { get; set; } = "";

    [Key(1)]
    public int OwnerIndex { get; set; } = -1;

    [Key(2)]
    public int BehaviorIndex { get; set; } = -1;

    [Key(3)]
    public string BehaviorKind { get; set; } = "";

    [Key(4)]
    public bool Pinging { get; set; }

    [Key(5)]
    public double PingCooldown { get; set; }

    [Key(6)]
    public double PingLerp { get; set; }

    [Key(7)]
    public double PingRadius { get; set; }

    [Key(8)]
    public int PingedEntityCount { get; set; }

    [Key(9)]
    public double RadiatorTemperature { get; set; }

    [Key(10)]
    public double Emissivity { get; set; }

    [Key(11)]
    public double PumpedHeat { get; set; }

    [Key(12)]
    public double WasteHeat { get; set; }

    [Key(13)]
    public double EnergyUsage { get; set; }

    [Key(14)]
    public double ReactorDraw { get; set; }

    [Key(15)]
    public double ReactorLoadRatio { get; set; }

    [Key(16)]
    public double CapacitorCharge { get; set; }

    [Key(17)]
    public double CapacitorCapacity { get; set; }

    [Key(18)]
    public double CapacitorEfficiency { get; set; }

    [Key(19)]
    public double AetherDriveAxisX { get; set; }

    [Key(20)]
    public double AetherDriveAxisY { get; set; }

    [Key(21)]
    public double AetherDriveAxisZ { get; set; }

    [Key(22)]
    public double AetherDriveThrustX { get; set; }

    [Key(23)]
    public double AetherDriveThrustY { get; set; }

    [Key(24)]
    public double AetherDriveThrustZ { get; set; }

    [Key(25)]
    public double AetherDriveRpmX { get; set; }

    [Key(26)]
    public double AetherDriveRpmY { get; set; }

    [Key(27)]
    public double AetherDriveRpmZ { get; set; }

    [Key(28)]
    public double AetherDriveMaximumRpm { get; set; }

    [Key(29)]
    public double AetherDriveThrustDirectionX { get; set; }

    [Key(30)]
    public double AetherDriveThrustDirectionY { get; set; }

    [Key(31)]
    public string ResourceScannerTargetBodyId { get; set; } = "";

    [Key(32)]
    public int ResourceScannerAsteroidIndex { get; set; } = -1;

    [Key(33)]
    public double ResourceScannerScanTime { get; set; }

    [Key(34)]
    public double ResourceScannerRange { get; set; }

    [Key(35)]
    public double ResourceScannerMinimumDensity { get; set; }

    [Key(36)]
    public double ResourceScannerScanDuration { get; set; }

    [Key(37)]
    public string MiningToolAsteroidBeltId { get; set; } = "";

    [Key(38)]
    public int MiningToolAsteroidIndex { get; set; } = -1;

    [Key(39)]
    public double MiningToolRange { get; set; }

    [Key(40)]
    public double ThrusterAxis { get; set; }

    [Key(41)]
    public double ThrusterThrust { get; set; }

    [Key(42)]
    public double ThrusterTorque { get; set; }

    [Key(43)]
    public double ShieldEfficiency { get; set; }

    [Key(44)]
    public double ShieldEnergyUsage { get; set; }

    [Key(45)]
    public double VelocityLimit { get; set; }

    [Key(46)]
    public double ThermotoggleTargetTemperature { get; set; }

    [Key(47)]
    public bool SwitchActivated { get; set; }

    [Key(48)]
    public bool TriggerPulled { get; set; }
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
    public string Relationship { get; set; } = "";

    [Key(2)]
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
