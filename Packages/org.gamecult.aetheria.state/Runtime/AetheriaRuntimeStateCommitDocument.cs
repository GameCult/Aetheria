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
    }
}
