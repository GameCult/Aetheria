using System;
using System.Collections.Generic;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public enum AetheriaRuntimeCommitKind
    {
        PlayerSettings,
        LoadoutTemplate,
        RunCheckpoint
    }

    public sealed class AetheriaRuntimeCommitEnvelope
    {
        public AetheriaRuntimeCommitEnvelope(
            string schema,
            AetheriaRuntimeCommitKind kind,
            string commandId,
            string createdAtUtc,
            string path)
        {
            Schema = schema;
            Kind = kind;
            CommandId = commandId;
            CreatedAtUtc = createdAtUtc;
            Path = path;
        }

        public string Schema { get; }
        public AetheriaRuntimeCommitKind Kind { get; }
        public string CommandId { get; }
        public string CreatedAtUtc { get; }
        public string Path { get; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStateCommitDocument
    {
        public const string SchemaId = "gamecult.aetheria.runtime_commit.v1";

        [Key(0)]
        public string Schema { get; set; } = SchemaId;

        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string CommandId { get; set; } = "";

        [Key(3)]
        public string CreatedAtUtc { get; set; } = "";

        [Key(4)]
        public AetheriaRuntimePlayerSettingsCommit? PlayerSettings { get; set; }

        [Key(5)]
        public AetheriaRuntimeLoadoutTemplateCommit? LoadoutTemplate { get; set; }

        [Key(6)]
        public AetheriaRuntimeRunCheckpointCommit? RunCheckpoint { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimePlayerSettingsCommit
    {
        [Key(0)]
        public string PlayerName { get; set; } = "";

        [Key(1)]
        public bool TutorialPassed { get; set; }

        [Key(2)]
        public IReadOnlyList<AetheriaRuntimeStoryFileHashCommit> StoryFileHashes { get; set; } = Array.Empty<AetheriaRuntimeStoryFileHashCommit>();

        [Key(3)]
        public string TemperatureUnit { get; set; } = "Celsius";

        [Key(4)]
        public int SignificantDigits { get; set; } = 3;

        [Key(5)]
        public string NebulaQuality { get; set; } = "Normal";

        [Key(6)]
        public bool ShowAsteroidsInMinimap { get; set; }

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeInputBindingCommit> BindingOverrides { get; set; } = Array.Empty<AetheriaRuntimeInputBindingCommit>();

        [Key(8)]
        public IReadOnlyList<string> ActionBarInputs { get; set; } = Array.Empty<string>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStoryFileHashCommit
    {
        [Key(0)]
        public string StoryPath { get; set; } = "";

        [Key(1)]
        public string Hash { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputBindingCommit
    {
        [Key(0)]
        public string ActionName { get; set; } = "";

        [Key(1)]
        public int BindingIndex { get; set; }

        [Key(2)]
        public string BindingPath { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeLoadoutTemplateCommit
    {
        [Key(0)]
        public string Name { get; set; } = "";

        [Key(1)]
        public string OwnerPlayerKey { get; set; } = "";

        [Key(2)]
        public AetheriaRuntimeEntityLoadoutCommit RootEntity { get; set; } = new AetheriaRuntimeEntityLoadoutCommit();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEntityLoadoutCommit
    {
        [Key(0)]
        public string Name { get; set; } = "";

        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string CorporationLegacyId { get; set; } = "";

        [Key(3)]
        public AetheriaRuntimeLoadoutItemCommit Hull { get; set; } = new AetheriaRuntimeLoadoutItemCommit();

        [Key(4)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> Equipment { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();

        [Key(5)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> CargoBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();

        [Key(6)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> DockingBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> CargoContents { get; set; } = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> DockingBayContents { get; set; } = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();

        [Key(9)]
        public IReadOnlyList<int> DockingBayAssignments { get; set; } = Array.Empty<int>();

        [Key(10)]
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; set; } = Array.Empty<IReadOnlyList<int>>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeEntityLoadoutCommit> Children { get; set; } = Array.Empty<AetheriaRuntimeEntityLoadoutCommit>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeLoadoutItemCommit
    {
        [Key(0)]
        public string ItemDefinitionLegacyId { get; set; } = "";

        [Key(1)]
        public double Quality { get; set; } = 1.0;

        [Key(2)]
        public double Durability { get; set; } = 1.0;

        [Key(3)]
        public int Quantity { get; set; } = 1;
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeLoadoutItemSlotCommit
    {
        [Key(0)]
        public int X { get; set; }

        [Key(1)]
        public int Y { get; set; }

        [Key(2)]
        public AetheriaRuntimeLoadoutItemCommit Item { get; set; } = new AetheriaRuntimeLoadoutItemCommit();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeCargoBayLoadoutCommit
    {
        [Key(0)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> Items { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRunCheckpointCommit
    {
        [Key(0)]
        public string RunId { get; set; } = "local";

        [Key(1)]
        public bool IsTutorial { get; set; }

        [Key(2)]
        public int EntranceZoneIndex { get; set; } = -1;

        [Key(3)]
        public int ExitZoneIndex { get; set; } = -1;

        [Key(4)]
        public int CurrentZoneIndex { get; set; } = -1;

        [Key(5)]
        public int CurrentZoneEntityIndex { get; set; } = -1;

        [Key(6)]
        public IReadOnlyList<int> DiscoveredZoneIndices { get; set; } = Array.Empty<int>();

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeZoneSnapshotCommit> Zones { get; set; } = Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeActionBarBindingCommit> ActionBarBindings { get; set; } = Array.Empty<AetheriaRuntimeActionBarBindingCommit>();

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeFactionRelationshipCommit> FactionRelationships { get; set; } = Array.Empty<AetheriaRuntimeFactionRelationshipCommit>();

        [Key(10)]
        public uint GenerationSeed { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeActionBarBindingCommit
    {
        [Key(0)]
        public string ControlPath { get; set; } = "";

        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string ItemDefinitionLegacyId { get; set; } = "";

        [Key(3)]
        public int EquipmentIndex { get; set; } = -1;

        [Key(4)]
        public int BehaviorIndex { get; set; } = -1;

        [Key(5)]
        public int WeaponGroup { get; set; } = -1;
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeFactionRelationshipCommit
    {
        [Key(0)]
        public string CorporationLegacyId { get; set; } = "";

        [Key(1)]
        public string Relationship { get; set; } = "";

        [Key(2)]
        public double Standing { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneSnapshotCommit
    {
        [Key(0)]
        public int ZoneIndex { get; set; } = -1;

        [Key(1)]
        public string Name { get; set; } = "";

        [Key(2)]
        public double PositionX { get; set; }

        [Key(3)]
        public double PositionY { get; set; }

        [Key(4)]
        public IReadOnlyList<int> AdjacentZoneIndices { get; set; } = Array.Empty<int>();

        [Key(5)]
        public IReadOnlyList<int> FactionIndices { get; set; } = Array.Empty<int>();

        [Key(6)]
        public int OwnerFactionIndex { get; set; } = -1;

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> Entities { get; set; } = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeOrbitSnapshotCommit> Orbits { get; set; } = Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>();

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeBodySnapshotCommit> Bodies { get; set; } = Array.Empty<AetheriaRuntimeBodySnapshotCommit>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeOrbitSnapshotCommit
    {
        [Key(0)]
        public string OrbitLegacyId { get; set; } = "";

        [Key(1)]
        public string ParentLegacyId { get; set; } = "";

        [Key(2)]
        public double Distance { get; set; }

        [Key(3)]
        public double Phase { get; set; }

        [Key(4)]
        public double FixedPositionX { get; set; }

        [Key(5)]
        public double FixedPositionY { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBodySnapshotCommit
    {
        [Key(0)]
        public string BodyLegacyId { get; set; } = "";

        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string Name { get; set; } = "";

        [Key(3)]
        public string OrbitLegacyId { get; set; } = "";

        [Key(4)]
        public double Mass { get; set; }

        [Key(5)]
        public IReadOnlyList<AetheriaRuntimeBodyResourceCommit> Resources { get; set; } = Array.Empty<AetheriaRuntimeBodyResourceCommit>();

        [Key(6)]
        public double BodyRadiusMultiplier { get; set; } = 1.0;

        [Key(7)]
        public double GravityRadiusMultiplier { get; set; } = 1.0;

        [Key(8)]
        public double GravityDepthMultiplier { get; set; } = 1.0;

        [Key(9)]
        public double GravityDepthExponent { get; set; } = 16.0;

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeAsteroidCommit> Asteroids { get; set; } = Array.Empty<AetheriaRuntimeAsteroidCommit>();

        [Key(11)]
        public AetheriaRuntimeGasGiantVisualCommit GasGiantVisual { get; set; } = new AetheriaRuntimeGasGiantVisualCommit();

        [Key(12)]
        public AetheriaRuntimeSunVisualCommit SunVisual { get; set; } = new AetheriaRuntimeSunVisualCommit();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBodyResourceCommit
    {
        [Key(0)]
        public string ItemDefinitionLegacyId { get; set; } = "";

        [Key(1)]
        public double Amount { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAsteroidCommit
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
    public sealed class AetheriaRuntimeGasGiantVisualCommit
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
        public IReadOnlyList<string> MaterialOverrides { get; set; } = Array.Empty<string>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeColorCommit> Colors { get; set; } = Array.Empty<AetheriaRuntimeColorCommit>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSunVisualCommit
    {
        [Key(0)]
        public double LightColorX { get; set; }

        [Key(1)]
        public double LightColorY { get; set; }

        [Key(2)]
        public double LightColorZ { get; set; }

        [Key(3)]
        public double FogTintColorX { get; set; }

        [Key(4)]
        public double FogTintColorY { get; set; }

        [Key(5)]
        public double FogTintColorZ { get; set; }

        [Key(6)]
        public double LightRadiusMultiplier { get; set; } = 1.0;
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeColorCommit
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

    [MessagePackObject]
    public sealed class AetheriaRuntimeEntitySnapshotCommit
    {
        [Key(0)]
        public int EntityIndex { get; set; } = -1;

        [Key(1)]
        public string Name { get; set; } = "";

        [Key(2)]
        public string Kind { get; set; } = "";

        [Key(3)]
        public double PositionX { get; set; }

        [Key(4)]
        public double PositionY { get; set; }

        [Key(5)]
        public double PositionZ { get; set; }

        [Key(6)]
        public double DirectionX { get; set; }

        [Key(7)]
        public double DirectionY { get; set; }

        [Key(8)]
        public string CorporationLegacyId { get; set; } = "";

        [Key(9)]
        public string HullItemDefinitionLegacyId { get; set; } = "";

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> Equipment { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> CargoBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> DockingBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();

        [Key(13)]
        public IReadOnlyList<int> ChildEntityIndices { get; set; } = Array.Empty<int>();

        [Key(14)]
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; set; } = Array.Empty<IReadOnlyList<int>>();

        [Key(15)]
        public IReadOnlyList<AetheriaRuntimeEntityStatGridCommit> StatGrids { get; set; } = Array.Empty<AetheriaRuntimeEntityStatGridCommit>();

        [Key(16)]
        public double VelocityX { get; set; }

        [Key(17)]
        public double VelocityY { get; set; }

        [Key(18)]
        public int TargetEntityIndex { get; set; } = -1;

        [Key(19)]
        public bool IsActive { get; set; }

        [Key(20)]
        public bool HeatsinksEnabled { get; set; }

        [Key(21)]
        public bool OverrideShutdown { get; set; }

        [Key(22)]
        public double TractorPower { get; set; }

        [Key(23)]
        public double Heatstroke { get; set; }

        [Key(24)]
        public double Hypothermia { get; set; }

        [Key(25)]
        public IReadOnlyList<AetheriaRuntimeActiveConsumableCommit> ActiveConsumables { get; set; } = Array.Empty<AetheriaRuntimeActiveConsumableCommit>();

        [Key(26)]
        public IReadOnlyList<AetheriaRuntimeBehaviorProgressCommit> BehaviorProgress { get; set; } = Array.Empty<AetheriaRuntimeBehaviorProgressCommit>();

        [Key(27)]
        public IReadOnlyList<AetheriaRuntimeWeaponStateCommit> WeaponStates { get; set; } = Array.Empty<AetheriaRuntimeWeaponStateCommit>();

        [Key(28)]
        public IReadOnlyList<AetheriaRuntimeBehaviorStateCommit> BehaviorStates { get; set; } = Array.Empty<AetheriaRuntimeBehaviorStateCommit>();

        [Key(29)]
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> CargoContents { get; set; } = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();

        [Key(30)]
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> DockingBayContents { get; set; } = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();

        [Key(31)]
        public IReadOnlyList<int> DockingBayAssignments { get; set; } = Array.Empty<int>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEntityStatGridCommit
    {
        [Key(0)]
        public string Name { get; set; } = "";

        [Key(1)]
        public int Width { get; set; }

        [Key(2)]
        public int Height { get; set; }

        [Key(3)]
        public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeActiveConsumableCommit
    {
        [Key(0)]
        public string ItemDefinitionLegacyId { get; set; } = "";

        [Key(1)]
        public double Quality { get; set; } = 1.0;

        [Key(2)]
        public double RemainingDuration { get; set; }

        [Key(3)]
        public double Duration { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBehaviorProgressCommit
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
    public sealed class AetheriaRuntimeWeaponStateCommit
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
        public int LockTargetEntityIndex { get; set; } = -1;
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBehaviorStateCommit
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
    }
}
