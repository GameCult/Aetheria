using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{    [MessagePackObject]
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

        [Key(12)]
        public string FactionKey { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeLoadoutItemCommit
    {
        [Key(1)]
        public double Quality { get; set; } = 1.0;

        [Key(2)]
        public double Durability { get; set; } = 1.0;

        [Key(3)]
        public int Quantity { get; set; } = 1;

        [Key(4)]
        public bool Enabled { get; set; } = true;

        [Key(5)]
        public string ItemKey { get; set; } = "";

        [Key(6)]
        public bool OverrideShutdown { get; set; }

        [Key(7)]
        public double Temperature { get; set; }
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
        public IReadOnlyList<int> DiscoveredZoneIndices { get; set; } = Array.Empty<int>();

        [Key(6)]
        public IReadOnlyList<AetheriaRuntimeZoneSnapshotCommit> Zones { get; set; } = Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeFactionRelationshipCommit> FactionRelationships { get; set; } = Array.Empty<AetheriaRuntimeFactionRelationshipCommit>();

        [Key(9)]
        public uint GenerationSeed { get; set; }

        [Key(10)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(11)]
        public int Credits { get; set; }

        public AetheriaRuntimeLoadoutTemplateCommit CreateLoadoutTemplate(string entityKey)
        {
            return TryParseEntityKey(entityKey, out var zoneIndex, out var entityIndex)
                ? CreateLoadoutTemplate(zoneIndex, entityIndex)
                : new AetheriaRuntimeLoadoutTemplateCommit();
        }

        public AetheriaRuntimeLoadoutTemplateCommit CreateLoadoutTemplate(int zoneIndex, int entityIndex)
        {
            var zone = (Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            var entities = zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var entity = entities.FirstOrDefault(candidate => candidate != null && candidate.EntityIndex == entityIndex);
            if (entity == null)
                return new AetheriaRuntimeLoadoutTemplateCommit();

            return new AetheriaRuntimeLoadoutTemplateCommit
            {
                Name = entity.Name ?? "",
                OwnerPlayerKey = "global:aetheria.player_settings.v1",
                RootEntity = CreateEntityLoadout(entity, entities)
            };
        }

        public string AppendLoadoutTemplateToZone(
            int zoneIndex,
            string parentEntityKey,
            AetheriaRuntimeLoadoutTemplateCommit template)
        {
            if (template?.RootEntity == null)
                return "";

            var zones = (Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).ToArray();
            var zone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return "";

            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            var parentIndex = TryParseEntityKey(parentEntityKey, out var parentZoneIndex, out var parsedParentIndex) &&
                              parentZoneIndex == zoneIndex
                ? parsedParentIndex
                : -1;
            var rootIndex = AppendEntity(entities, template.RootEntity, template.Name);

            if (parentIndex >= 0 && parentIndex < entities.Count)
            {
                var children = (entities[parentIndex].ChildEntityIndices ?? Array.Empty<int>()).ToList();
                if (!children.Contains(rootIndex))
                    children.Add(rootIndex);
                entities[parentIndex].ChildEntityIndices = children.ToArray();
            }

            zone.Entities = entities.ToArray();
            Zones = zones;
            return EntityRecordKey(zoneIndex, rootIndex);
        }

        public string EntityRecordKey(int zoneIndex, int entityIndex)
        {
            return EntityRecordKey(RunId, zoneIndex, entityIndex);
        }

        public static string EntityRecordKey(string runId, int zoneIndex, int entityIndex)
        {
            return $"global:aetheria.run_state.{(string.IsNullOrWhiteSpace(runId) ? "local" : runId)}.zone.{zoneIndex}.entity.{entityIndex}.v1";
        }

        public static bool TryParseEntityKey(string entityKey, out int zoneIndex, out int entityIndex)
        {
            zoneIndex = -1;
            entityIndex = -1;
            if (string.IsNullOrWhiteSpace(entityKey))
                return false;

            var parts = entityKey.Split('.');
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], "zone", StringComparison.Ordinal) &&
                    int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out zoneIndex))
                {
                    continue;
                }

                if (string.Equals(parts[i], "entity", StringComparison.Ordinal) &&
                    int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out entityIndex))
                {
                    continue;
                }
            }

            return zoneIndex >= 0 && entityIndex >= 0;
        }

        private static AetheriaRuntimeEntityLoadoutCommit CreateEntityLoadout(
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> zoneEntities)
        {
            var childIndices = (entity.ChildEntityIndices ?? Array.Empty<int>())
                .Where(index => index >= 0)
                .ToArray();
            var children = childIndices
                .Select(index => zoneEntities.FirstOrDefault(candidate => candidate != null && candidate.EntityIndex == index))
                .Where(child => child != null)
                .Select(child => CreateEntityLoadout(child!, zoneEntities))
                .ToArray();
            var childIndexByEntityIndex = childIndices
                .Select((entityIndex, childIndex) => new { entityIndex, childIndex })
                .ToDictionary(pair => pair.entityIndex, pair => pair.childIndex);

            return new AetheriaRuntimeEntityLoadoutCommit
            {
                Name = entity.Name ?? "",
                Kind = string.IsNullOrWhiteSpace(entity.Kind) ? "ship" : entity.Kind,
                FactionKey = entity.FactionKey ?? "",
                Hull = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = entity.HullItemKey ?? "",
                    Quality = 1.0,
                    Durability = 1.0,
                    Quantity = 1,
                    Enabled = true
                },
                Equipment = CloneSlots(entity.Equipment),
                CargoBays = CloneSlots(entity.CargoBays),
                DockingBays = CloneSlots(entity.DockingBays),
                CargoContents = CloneCargo(entity.CargoContents),
                DockingBayContents = CloneCargo(entity.DockingBayContents),
                DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>())
                    .Select(index => childIndexByEntityIndex.TryGetValue(index, out var childIndex) ? childIndex : -1)
                    .ToArray(),
                WeaponGroups = (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                    .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                    .ToArray(),
                Children = children
            };
        }

        private static int AppendEntity(
            List<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeEntityLoadoutCommit loadout,
            string templateName)
        {
            var entityIndex = entities.Count;
            var entity = new AetheriaRuntimeEntitySnapshotCommit
            {
                EntityIndex = entityIndex,
                Name = string.IsNullOrWhiteSpace(loadout.Name) ? templateName ?? "" : loadout.Name,
                Kind = string.IsNullOrWhiteSpace(loadout.Kind) ? "ship" : loadout.Kind,
                DirectionX = 0,
                DirectionY = 1,
                IsActive = true,
                HullItemKey = loadout.Hull?.ItemKey ?? "",
                FactionKey = loadout.FactionKey ?? "",
                Equipment = CloneSlots(loadout.Equipment),
                CargoBays = CloneSlots(loadout.CargoBays),
                DockingBays = CloneSlots(loadout.DockingBays),
                CargoContents = CloneCargo(loadout.CargoContents),
                DockingBayContents = CloneCargo(loadout.DockingBayContents),
                DockingBayAssignments = (loadout.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
                WeaponGroups = (loadout.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                    .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                    .ToArray(),
                TargetEntityIndex = -1,
                ShutdownPerformance = 0.25
            };
            entities.Add(entity);

            var childIndices = new List<int>();
            foreach (var child in loadout.Children ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>())
            {
                if (child == null)
                    continue;
                childIndices.Add(AppendEntity(entities, child, ""));
            }

            entity.ChildEntityIndices = childIndices.ToArray();
            return entityIndex;
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit[] CloneSlots(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
        {
            return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot != null)
                .Select(CloneSlot)
                .ToArray();
        }

        private static AetheriaRuntimeCargoBayLoadoutCommit[] CloneCargo(
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? cargo)
        {
            return (cargo ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .Select(bay => new AetheriaRuntimeCargoBayLoadoutCommit
                {
                    Items = CloneSlots(bay?.Items)
                })
                .ToArray();
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit CloneSlot(AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            return new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.X,
                Y = slot.Y,
                Item = CloneItem(slot.Item)
            };
        }

        private static AetheriaRuntimeLoadoutItemCommit CloneItem(AetheriaRuntimeLoadoutItemCommit? item)
        {
            return new AetheriaRuntimeLoadoutItemCommit
            {
                ItemKey = item?.ItemKey ?? "",
                Quality = item?.Quality ?? 1.0,
                Durability = item?.Durability ?? 1.0,
                Quantity = item?.Quantity ?? 1,
                Enabled = item?.Enabled ?? true,
                OverrideShutdown = item?.OverrideShutdown ?? false,
                Temperature = item?.Temperature ?? 0
            };
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeFactionRelationshipCommit
    {
        [Key(1)]
        public string Relationship { get; set; } = "";

        [Key(2)]
        public double Standing { get; set; }

        [Key(3)]
        public string FactionKey { get; set; } = "";
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

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> DroppedPickups { get; set; } = Array.Empty<AetheriaRuntimeDroppedPickupCommit>();

        [Key(11)]
        public double GravityTerrainRadius { get; set; }

        [Key(12)]
        public double GravityTerrainDepth { get; set; }

        [Key(13)]
        public double GravityTerrainDepthExponent { get; set; } = 1.0;

        [Key(14)]
        public double GravityTerrainBoundaryFog { get; set; }

        [Key(15)]
        public double GravityTerrainWaveFrequency { get; set; } = 1.0;

        [Key(16)]
        public double SimulationTimeSeconds { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDroppedPickupCommit
    {
        [Key(0)]
        public int PickupIndex { get; set; } = -1;

        [Key(1)]
        public double PositionX { get; set; }

        [Key(2)]
        public double PositionY { get; set; }

        [Key(3)]
        public double PositionZ { get; set; }

        [Key(4)]
        public double VelocityX { get; set; }

        [Key(5)]
        public double VelocityY { get; set; }

        [Key(6)]
        public double VelocityZ { get; set; }

        [Key(7)]
        public AetheriaRuntimeLoadoutItemCommit Item { get; set; } = new AetheriaRuntimeLoadoutItemCommit();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeOrbitSnapshotCommit
    {
        [Key(2)]
        public double Distance { get; set; }

        [Key(3)]
        public double Phase { get; set; }

        [Key(4)]
        public double FixedPositionX { get; set; }

        [Key(5)]
        public double FixedPositionY { get; set; }

        [Key(6)]
        public string OrbitKey { get; set; } = "";

        [Key(7)]
        public string ParentOrbitKey { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBodySnapshotCommit
    {
        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string Name { get; set; } = "";

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

        [Key(13)]
        public string BodyKey { get; set; } = "";

        [Key(14)]
        public string OrbitKey { get; set; } = "";

        [Key(15)]
        public double GravityInfluenceCenterX { get; set; } = double.NaN;

        [Key(16)]
        public double GravityInfluenceCenterZ { get; set; } = double.NaN;

        [Key(17)]
        public double GravityInfluenceRadius { get; set; }

        [Key(18)]
        public double GravityWellDepth { get; set; }

        [Key(19)]
        public double GravityWaveRadius { get; set; }

        [Key(20)]
        public double GravityWaveDepth { get; set; }

        [Key(21)]
        public double GravityWaveSpeed { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBodyResourceCommit
    {
        [Key(1)]
        public double Amount { get; set; }

        [Key(2)]
        public string ItemKey { get; set; } = "";
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

        [Key(4)]
        public double Damage { get; set; }

        [Key(5)]
        public double RespawnTimer { get; set; }

        [Key(6)]
        public IReadOnlyList<AetheriaRuntimeAsteroidMiningAccumulatorCommit> MiningAccumulators { get; set; } = Array.Empty<AetheriaRuntimeAsteroidMiningAccumulatorCommit>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAsteroidMiningAccumulatorCommit
    {
        [Key(0)]
        public int MinerEntityIndex { get; set; } = -1;

        [Key(1)]
        public double Amount { get; set; }
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

        [Key(32)]
        public double Visibility { get; set; }

        [Key(33)]
        public int VisibilitySourceCount { get; set; }

        [Key(34)]
        public IReadOnlyList<AetheriaRuntimeEntityContactCommit> Contacts { get; set; } = Array.Empty<AetheriaRuntimeEntityContactCommit>();

        [Key(35)]
        public string HullItemKey { get; set; } = "";

        [Key(36)]
        public string FactionKey { get; set; } = "";

        [Key(37)]
        public double ShutdownPerformance { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEntityContactCommit
    {
        [Key(0)]
        public int TargetEntityIndex { get; set; } = -1;

        [Key(1)]
        public double InfoGathered { get; set; }

        [Key(2)]
        public bool Hostile { get; set; }

        [Key(3)]
        public bool Visible { get; set; }
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
        [Key(1)]
        public double Quality { get; set; } = 1.0;

        [Key(2)]
        public double RemainingDuration { get; set; }

        [Key(3)]
        public double Duration { get; set; }

        [Key(4)]
        public string ItemKey { get; set; } = "";
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

        [Key(49)]
        public bool StatModifierApplied { get; set; }

        [Key(50)]
        public bool StatModifierExecuted { get; set; }

        [Key(51)]
        public int StatModifierTargetStatCount { get; set; }

        [Key(52)]
        public int TurretControllerWeaponCount { get; set; }

        [Key(53)]
        public double TurretControllerShotSpeed { get; set; }

        [Key(54)]
        public bool TurretControllerPredictShots { get; set; }

        [Key(55)]
        public string ResourceScannerTargetBodyKey { get; set; } = "";

        [Key(56)]
        public string MiningToolAsteroidBeltKey { get; set; } = "";
    }
}
