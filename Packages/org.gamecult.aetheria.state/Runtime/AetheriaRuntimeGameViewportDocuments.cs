using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Eve.PluginFields;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [CultDocument("gamecult.aetheria.game_viewport", "gamecult.aetheria.game_viewport.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeGameViewportDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.GameViewport;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; }

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(8)]
        public AetheriaRuntimeViewportBounds Viewport { get; set; } = new AetheriaRuntimeViewportBounds();

        [Key(9)]
        public IReadOnlyList<int> ControlledEntityIndices { get; set; } = Array.Empty<int>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeViewportObject> Objects { get; set; } =
            Array.Empty<AetheriaRuntimeViewportObject>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeGravityInfluence> GravityInfluences { get; set; } =
            Array.Empty<AetheriaRuntimeGravityInfluence>();

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeBodyView> Bodies { get; set; } =
            Array.Empty<AetheriaRuntimeBodyView>();
    }

    [CultDocument("gamecult.fields.objects", "gamecult.fields.objects.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeObjectsViewportDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.ObjectsViewport;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; }

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(8)]
        public AetheriaRuntimeViewportBounds Viewport { get; set; } = new AetheriaRuntimeViewportBounds();

        [Key(9)]
        public IReadOnlyList<int> ControlledEntityIndices { get; set; } = Array.Empty<int>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeViewportObject> Objects { get; set; } =
            Array.Empty<AetheriaRuntimeViewportObject>();
    }

    [CultDocument("gamecult.fields.gravity", "gamecult.fields.gravity.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeGravityViewportDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.GravityViewport;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; }

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public AetheriaRuntimeViewportBounds Viewport { get; set; } = new AetheriaRuntimeViewportBounds();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeGravityInfluence> GravityInfluences { get; set; } =
            Array.Empty<AetheriaRuntimeGravityInfluence>();

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeBodyView> Bodies { get; set; } =
            Array.Empty<AetheriaRuntimeBodyView>();

        [Key(10)]
        public double TerrainRadius { get; set; }

        [Key(11)]
        public double TerrainDepth { get; set; }

        [Key(12)]
        public double TerrainDepthExponent { get; set; } = 1.0;

        [Key(13)]
        public double TerrainWaveFrequency { get; set; } = 1.0;
    }

    [CultDocument("gamecult.aetheria.current_zone", "gamecult.aetheria.current_zone.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeCurrentZoneDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.CurrentZone;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public double PositionX { get; set; }

        [Key(8)]
        public double PositionY { get; set; }

        [Key(9)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(10)]
        public IReadOnlyList<int> AdjacentZoneIndices { get; set; } = Array.Empty<int>();
    }

    [CultDocument("gamecult.aetheria.current_entity", "gamecult.aetheria.current_entity.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeCurrentEntityDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.CurrentEntity;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string EntityKey { get; set; } = "";

        [Key(7)]
        public int EntityIndex { get; set; } = -1;

        [Key(8)]
        public AetheriaRuntimeViewportObject? Entity { get; set; }

        [Key(9)]
        public AetheriaRuntimeEntityStatus Status { get; set; } = new AetheriaRuntimeEntityStatus();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Inventory { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Equipment { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Cargo { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();

        [Key(13)]
        public double ShutdownPerformance { get; set; }

        [Key(14)]
        public AetheriaRuntimeCurrentEntityHudStatus Hud { get; set; } = new AetheriaRuntimeCurrentEntityHudStatus();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeCurrentEntityHudStatus
    {
        [Key(0)]
        public bool OverrideShutdown { get; set; }

        [Key(1)]
        public bool ShieldActive { get; set; }

        [Key(2)]
        public bool HeatsinksEnabled { get; set; }

        [Key(3)]
        public double Heatstroke { get; set; }

        [Key(4)]
        public double Hypothermia { get; set; }

        [Key(5)]
        public double Visibility { get; set; }

        [Key(6)]
        public double HullDurabilityRatio { get; set; }

        [Key(7)]
        public double RadiatorTemperatureMinimum { get; set; }

        [Key(8)]
        public double RadiatorTemperatureMaximum { get; set; }

        [Key(9)]
        public int RadiatorCount { get; set; }

        [Key(10)]
        public double SensorCooldown { get; set; }

        [Key(11)]
        public double ReactorDraw { get; set; }

        [Key(12)]
        public double CapacitorCharge { get; set; }

        [Key(13)]
        public double CapacitorCapacity { get; set; }

        [Key(14)]
        public double AetherDriveRpmX { get; set; }

        [Key(15)]
        public double AetherDriveRpmY { get; set; }

        [Key(16)]
        public double AetherDriveRpmZ { get; set; }

        [Key(17)]
        public double AetherDriveMaximumRpm { get; set; }

        [Key(18)]
        public double MeanTemperature { get; set; }

        [Key(19)]
        public double MinimumTemperature { get; set; }

        [Key(20)]
        public double MaximumTemperature { get; set; }

        [Key(21)]
        public double ThermalVisibility { get; set; }
    }

    [CultDocument("gamecult.aetheria.current_docking", "gamecult.aetheria.current_docking.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeCurrentDockingDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.CurrentDocking;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(7)]
        public int CurrentEntityIndex { get; set; } = -1;

        [Key(8)]
        public bool IsDocked { get; set; }

        [Key(9)]
        public string DockParentEntityKey { get; set; } = "";

        [Key(10)]
        public int DockParentEntityIndex { get; set; } = -1;

        [Key(11)]
        public int DockingBayIndex { get; set; } = -1;

        [Key(12)]
        public AetheriaRuntimeViewportObject? DockParent { get; set; }

        [Key(13)]
        public string DockParentOrbitKey { get; set; } = "";

        [Key(14)]
        public string DockParentParentOrbitKey { get; set; } = "";

        [Key(15)]
        public string DockParentParentBodyKey { get; set; } = "";
    }

    [CultDocument("gamecult.aetheria.zone_contacts", "gamecult.aetheria.zone_contacts.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneContactsDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.ZoneContacts;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeZoneTargetRow> Targets { get; set; } =
            Array.Empty<AetheriaRuntimeZoneTargetRow>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeZoneContactRow> Contacts { get; set; } =
            Array.Empty<AetheriaRuntimeZoneContactRow>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneTargetRow
    {
        [Key(0)]
        public int EntityIndex { get; set; } = -1;

        [Key(1)]
        public int TargetEntityIndex { get; set; } = -1;

        [Key(2)]
        public double TargetPositionX { get; set; }

        [Key(3)]
        public double TargetPositionY { get; set; }

        [Key(4)]
        public double TargetPositionZ { get; set; }

        [Key(5)]
        public double DeltaX { get; set; }

        [Key(6)]
        public double DeltaY { get; set; }

        [Key(7)]
        public double DeltaZ { get; set; }

        [Key(8)]
        public double Distance { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneContactRow
    {
        [Key(0)]
        public int ObserverEntityIndex { get; set; } = -1;

        [Key(1)]
        public int TargetEntityIndex { get; set; } = -1;

        [Key(2)]
        public double InfoGathered { get; set; }

        [Key(3)]
        public bool Hostile { get; set; }

        [Key(4)]
        public bool Visible { get; set; }

        [Key(5)]
        public double TargetPositionX { get; set; }

        [Key(6)]
        public double TargetPositionY { get; set; }

        [Key(7)]
        public double TargetPositionZ { get; set; }

        [Key(8)]
        public double DeltaX { get; set; }

        [Key(9)]
        public double DeltaY { get; set; }

        [Key(10)]
        public double DeltaZ { get; set; }

        [Key(11)]
        public double Distance { get; set; }

        [Key(12)]
        public int PrimarySensorSourceEntityIndex { get; set; } = -1;
    }

    [CultDocument("gamecult.aetheria.station_refit", "gamecult.aetheria.station_refit.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeStationRefitDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.StationRefit;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(7)]
        public int CurrentEntityIndex { get; set; } = -1;

        [Key(8)]
        public bool IsDocked { get; set; }

        [Key(9)]
        public string DockParentEntityKey { get; set; } = "";

        [Key(10)]
        public int DockParentEntityIndex { get; set; } = -1;

        [Key(11)]
        public int DockingBayIndex { get; set; } = -1;

        [Key(12)]
        public AetheriaRuntimeViewportObject? DockParent { get; set; }

        [Key(13)]
        public IReadOnlyList<AetheriaRuntimeStationRefitEntityOption> AvailableEntities { get; set; } =
            Array.Empty<AetheriaRuntimeStationRefitEntityOption>();

        [Key(14)]
        public int Credits { get; set; }

        [Key(15)]
        public IReadOnlyList<AetheriaRuntimeStationStockItem> StationStock { get; set; } =
            Array.Empty<AetheriaRuntimeStationStockItem>();

        [Key(16)]
        public IReadOnlyList<AetheriaRuntimeStationDockingBayRow> DockingBays { get; set; } =
            Array.Empty<AetheriaRuntimeStationDockingBayRow>();

        [Key(17)]
        public IReadOnlyList<AetheriaRuntimeStationLoadoutRestoreOption> LoadoutRestoreOptions { get; set; } =
            Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>();

        [Key(18)]
        public IReadOnlyList<AetheriaRuntimeStationCargoTargetRow> CargoTargets { get; set; } =
            Array.Empty<AetheriaRuntimeStationCargoTargetRow>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStationStockItem
    {
        [Key(0)]
        public string ItemKey { get; set; } = "";

        [Key(1)]
        public int Quantity { get; set; } = 1;

        [Key(2)]
        public double Quality { get; set; } = 1;

        [Key(3)]
        public double Durability { get; set; } = 1;

        [Key(4)]
        public int CargoBayIndex { get; set; } = -1;

        [Key(5)]
        public int X { get; set; } = -1;

        [Key(6)]
        public int Y { get; set; } = -1;

        [Key(7)]
        public int Price { get; set; }

        [Key(8)]
        public bool CanAfford { get; set; }

        [Key(9)]
        public int OwnedQuantity { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStationRefitEntityOption
    {
        [Key(0)]
        public string EntityKey { get; set; } = "";

        [Key(1)]
        public int EntityIndex { get; set; } = -1;

        [Key(2)]
        public string DisplayName { get; set; } = "";

        [Key(3)]
        public string Kind { get; set; } = "";

        [Key(4)]
        public bool IsCurrentEntity { get; set; }

        [Key(5)]
        public bool IsPlayerShip { get; set; }

        [Key(6)]
        public int CargoBayCount { get; set; }

        [Key(7)]
        public int DockingBayIndex { get; set; } = -1;

        [Key(8)]
        public string HullItemKey { get; set; } = "";

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeStationStockItem> CargoItems { get; set; } =
            Array.Empty<AetheriaRuntimeStationStockItem>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStationDockingBayRow
    {
        [Key(0)]
        public int DockingBayIndex { get; set; } = -1;

        [Key(1)]
        public string ItemKey { get; set; } = "";

        [Key(2)]
        public int X { get; set; } = -1;

        [Key(3)]
        public int Y { get; set; } = -1;

        [Key(4)]
        public string OccupiedEntityKey { get; set; } = "";

        [Key(5)]
        public int OccupiedEntityIndex { get; set; } = -1;

        [Key(6)]
        public string OccupiedEntityName { get; set; } = "";

        [Key(7)]
        public string OccupiedHullItemKey { get; set; } = "";

        [Key(8)]
        public bool OccupiedByCurrentEntity { get; set; }

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeStationStockItem> CargoItems { get; set; } =
            Array.Empty<AetheriaRuntimeStationStockItem>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStationLoadoutRestoreOption
    {
        [Key(0)]
        public int TemplateIndex { get; set; } = -1;

        [Key(1)]
        public string TemplateName { get; set; } = "";

        [Key(2)]
        public string TargetEntityKey { get; set; } = "";

        [Key(3)]
        public int Price { get; set; }

        [Key(4)]
        public bool CanRestore { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStationCargoTargetRow
    {
        [Key(0)]
        public int TargetIndex { get; set; } = -1;

        [Key(1)]
        public AetheriaRuntimeTradeCargoTargetKind Kind { get; set; } =
            AetheriaRuntimeTradeCargoTargetKind.Unknown;

        [Key(2)]
        public string Label { get; set; } = "";

        [Key(3)]
        public string EntityKey { get; set; } = "";

        [Key(4)]
        public int BayIndex { get; set; } = -1;

        [Key(5)]
        public bool IsCurrent { get; set; }

        [Key(6)]
        public bool IsPlayerShip { get; set; }

        [Key(7)]
        public string HullItemKey { get; set; } = "";

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeStationStockItem> CargoItems { get; set; } =
            Array.Empty<AetheriaRuntimeStationStockItem>();
    }

    [CultDocument("gamecult.aetheria.sector_map", "gamecult.aetheria.sector_map.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeSectorMapDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.SectorMap;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int CurrentZoneIndex { get; set; } = -1;

        [Key(6)]
        public int EntranceZoneIndex { get; set; } = -1;

        [Key(7)]
        public int ExitZoneIndex { get; set; } = -1;

        [Key(8)]
        public IReadOnlyList<int> DiscoveredZoneIndices { get; set; } = Array.Empty<int>();

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeSectorMapZone> Zones { get; set; } =
            Array.Empty<AetheriaRuntimeSectorMapZone>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeSectorMapLink> Links { get; set; } =
            Array.Empty<AetheriaRuntimeSectorMapLink>();

        [Key(11)]
        public bool IsTutorial { get; set; }

        [Key(12)]
        public uint GenerationSeed { get; set; }

        [Key(13)]
        public IReadOnlyList<AetheriaRuntimeFactionRelationshipCommit> FactionRelationships { get; set; } =
            Array.Empty<AetheriaRuntimeFactionRelationshipCommit>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSectorMapZone
    {
        [Key(0)]
        public int ZoneIndex { get; set; } = -1;

        [Key(1)]
        public string Name { get; set; } = "";

        [Key(2)]
        public double X { get; set; }

        [Key(3)]
        public double Y { get; set; }

        [Key(4)]
        public int OwnerFactionIndex { get; set; } = -1;

        [Key(5)]
        public IReadOnlyList<int> FactionIndices { get; set; } = Array.Empty<int>();

        [Key(6)]
        public IReadOnlyList<int> AdjacentZoneIndices { get; set; } = Array.Empty<int>();

        [Key(7)]
        public bool Discovered { get; set; }

        [Key(8)]
        public bool Current { get; set; }

        [Key(9)]
        public bool Entrance { get; set; }

        [Key(10)]
        public bool Exit { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSectorMapLink
    {
        [Key(0)]
        public int FromZoneIndex { get; set; } = -1;

        [Key(1)]
        public int ToZoneIndex { get; set; } = -1;

        [Key(2)]
        public bool Discovered { get; set; }
    }

    [CultDocument("gamecult.aetheria.zone_details", "gamecult.aetheria.zone_details.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneDetailsDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.ZoneDetails;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public double Mass { get; set; }

        [Key(8)]
        public double Radius { get; set; }

        [Key(9)]
        public IReadOnlyList<string> BodyKinds { get; set; } = Array.Empty<string>();

        [Key(10)]
        public IReadOnlyList<string> EntityHullItemKeys { get; set; } = Array.Empty<string>();

        [Key(11)]
        public bool HasContents { get; set; }
    }

    [CultDocument("gamecult.aetheria.zone_render", "gamecult.aetheria.zone_render.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneRenderDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.ZoneRender;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(3)]
        public double SimulationTimeSeconds { get; set; }

        [Key(4)]
        public string RunId { get; set; } = "";

        [Key(5)]
        public int ZoneIndex { get; set; } = -1;

        [Key(6)]
        public string ZoneName { get; set; } = "";

        [Key(7)]
        public string CurrentEntityKey { get; set; } = "";

        [Key(8)]
        public double ZoneRenderRadius { get; set; }

        [Key(9)]
        public int Credits { get; set; }

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeZoneRenderAdjacentZone> AdjacentZones { get; set; } =
            Array.Empty<AetheriaRuntimeZoneRenderAdjacentZone>();

        [Key(13)]
        public IReadOnlyList<AetheriaRuntimeZoneRenderBodyPose> BodyPoses { get; set; } =
            Array.Empty<AetheriaRuntimeZoneRenderBodyPose>();

        [Key(14)]
        public IReadOnlyList<AetheriaRuntimeZoneRenderAsteroidBeltPose> AsteroidBeltPoses { get; set; } =
            Array.Empty<AetheriaRuntimeZoneRenderAsteroidBeltPose>();

        [Key(15)]
        public IReadOnlyList<AetheriaRuntimeZoneRenderWormholeExit> WormholeExits { get; set; } =
            Array.Empty<AetheriaRuntimeZoneRenderWormholeExit>();

        [Key(16)]
        public IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> DroppedPickups { get; set; } =
            Array.Empty<AetheriaRuntimeDroppedPickupCommit>();

        [Key(17)]
        public IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> EntitySnapshots { get; set; } =
            Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();

        [Key(18)]
        public IReadOnlyList<AetheriaRuntimeOrbitSnapshotCommit> Orbits { get; set; } =
            Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>();

        [Key(19)]
        public IReadOnlyList<AetheriaRuntimeBodySnapshotCommit> Bodies { get; set; } =
            Array.Empty<AetheriaRuntimeBodySnapshotCommit>();

        [Key(20)]
        public IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> PhysicalPayloads { get; set; } =
            Array.Empty<AetheriaRuntimePhysicalPayloadCommit>();

        public static string EntityRecordKey(string runId, int zoneIndex, int entityIndex)
        {
            return string.IsNullOrWhiteSpace(runId)
                ? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(runId, zoneIndex, entityIndex);
        }

        public AetheriaRuntimeEntitySnapshot[] CreateEntitySnapshots()
        {
            return CreateEntitySnapshots(RunId, ZoneIndex, EntitySnapshots);
        }

        public static AetheriaRuntimeEntitySnapshot[] CreateEntitySnapshots(
            string runId,
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            if (zone == null)
                return Array.Empty<AetheriaRuntimeEntitySnapshot>();

            return CreateEntitySnapshots(runId, zone.ZoneIndex, zone.Entities);
        }

        public static AetheriaRuntimeEntitySnapshot[] CreateEntitySnapshots(
            string runId,
            int zoneIndex,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit>? entities)
        {
            return (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .Select(entity => CreateEntitySnapshot(runId, zoneIndex, entity))
                .ToArray();
        }

        private static AetheriaRuntimeEntitySnapshot CreateEntitySnapshot(
            string runId,
            int zoneIndex,
            AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return new AetheriaRuntimeEntitySnapshot(
                EntityRecordKey(runId, zoneIndex, entity.EntityIndex),
                entity.EntityIndex,
                entity.Name ?? "",
                entity.Kind ?? "",
                entity.PositionX,
                entity.PositionY,
                entity.PositionZ,
                entity.DirectionX,
                entity.DirectionY,
                entity.FactionKey ?? "",
                entity.HullItemKey ?? "",
                CreateEntityItemSlots(entity.Equipment),
                CreateEntityItemSlots(entity.CargoBays),
                CreateEntityItemSlots(entity.DockingBays),
                (entity.ChildEntityIndices ?? Array.Empty<int>())
                    .Where(index => index >= 0)
                    .Select(index => EntityRecordKey(runId, zoneIndex, index))
                    .ToArray(),
                (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                    .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                    .ToArray(),
                (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                    .Select(grid => new AetheriaRuntimeEntityStatGridSnapshot(
                        grid.Name ?? "",
                        grid.Width,
                        grid.Height,
                        (grid.Values ?? Array.Empty<double>()).ToArray()))
                    .ToArray(),
                entity.VelocityX,
                entity.VelocityY,
                entity.TargetEntityIndex < 0 ? "" : EntityRecordKey(runId, zoneIndex, entity.TargetEntityIndex),
                entity.IsActive,
                entity.HeatsinksEnabled,
                entity.OverrideShutdown,
                entity.TractorPower,
                entity.Heatstroke,
                entity.Hypothermia,
                (entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                    .Select(consumable => new AetheriaRuntimeActiveConsumableSnapshot(
                        consumable.ItemKey ?? "",
                        consumable.Quality,
                        consumable.RemainingDuration,
                        consumable.Duration))
                    .ToArray(),
                (entity.BehaviorProgress ?? Array.Empty<AetheriaRuntimeBehaviorProgressCommit>())
                    .Select(progress => new AetheriaRuntimeBehaviorProgressSnapshot(
                        progress.OwnerKind ?? "",
                        progress.OwnerIndex,
                        progress.BehaviorIndex,
                        progress.BehaviorKind ?? "",
                        progress.Progress))
                    .ToArray(),
                CreateWeaponStates(runId, zoneIndex, entity.WeaponStates),
                CreateBehaviorStates(entity.BehaviorStates),
                CreateCargoBays(entity.CargoContents),
                CreateCargoBays(entity.DockingBayContents),
                (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
                entity.Visibility,
                entity.VisibilitySourceCount,
                (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Where(contact => contact != null && contact.TargetEntityIndex >= 0)
                    .Select(contact => new AetheriaRuntimeEntityContactSnapshot(
                        EntityRecordKey(runId, zoneIndex, contact.TargetEntityIndex),
                        contact.InfoGathered,
                        contact.Hostile,
                        contact.Visible))
                    .ToArray(),
                entity.ShutdownPerformance);
        }

        private static AetheriaRuntimeEntityItemSlotSnapshot[] CreateEntityItemSlots(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
        {
            return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot?.Item != null)
                .Select(slot => new AetheriaRuntimeEntityItemSlotSnapshot(
                    slot.X,
                    slot.Y,
                    slot.Item!.ItemKey ?? "",
                    slot.Item.Quality,
                    slot.Item.Durability,
                    slot.Item.Quantity,
                    slot.Item.Enabled,
                    slot.Item.OverrideShutdown,
                    slot.Item.Temperature))
                .ToArray();
        }

        private static AetheriaRuntimeCargoBayLoadoutSnapshot[] CreateCargoBays(
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? cargoBays)
        {
            return (cargoBays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .Select(bay => new AetheriaRuntimeCargoBayLoadoutSnapshot(
                    CreateLoadoutSlots(bay?.Items)))
                .ToArray();
        }

        private static AetheriaRuntimeLoadoutItemSlotSnapshot[] CreateLoadoutSlots(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
        {
            return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot?.Item != null)
                .Select(slot => new AetheriaRuntimeLoadoutItemSlotSnapshot(
                    slot.X,
                    slot.Y,
                    new AetheriaRuntimeLoadoutItemSnapshot(
                        slot.Item!.ItemKey ?? "",
                        slot.Item.Quality,
                        slot.Item.Durability,
                        slot.Item.Quantity,
                        slot.Item.Enabled,
                        slot.Item.OverrideShutdown,
                        slot.Item.Temperature)))
                .ToArray();
        }

        private static AetheriaRuntimeWeaponStateSnapshot[] CreateWeaponStates(
            string runId,
            int zoneIndex,
            IReadOnlyList<AetheriaRuntimeWeaponStateCommit>? weaponStates)
        {
            return (weaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                .Where(state => state != null)
                .Select(state => new AetheriaRuntimeWeaponStateSnapshot(
                    state.OwnerKind ?? "",
                    state.OwnerIndex,
                    state.BehaviorIndex,
                    state.BehaviorKind ?? "",
                    state.Firing,
                    state.Ammo,
                    state.BurstRemaining,
                    state.BurstTimer,
                    state.BurstInterval,
                    state.CooldownProgress,
                    state.CoolingDown,
                    state.Charging,
                    state.Charged,
                    state.Charge,
                    state.Reloading,
                    state.ReloadProgress,
                    state.AmmoIntervalProgress,
                    state.LockProgress,
                    state.LockTargetEntityIndex < 0 ? "" : EntityRecordKey(runId, zoneIndex, state.LockTargetEntityIndex),
                    state.ChargeHoldSeconds,
                    state.ChargeRiskChecks,
                    state.ChargeMalfunctionRisk))
                .ToArray();
        }

        private static AetheriaRuntimeBehaviorStateSnapshot[] CreateBehaviorStates(
            IReadOnlyList<AetheriaRuntimeBehaviorStateCommit>? behaviorStates)
        {
            return (behaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(state => state != null)
                .Select(state => new AetheriaRuntimeBehaviorStateSnapshot(
                    state.OwnerKind ?? "",
                    state.OwnerIndex,
                    state.BehaviorIndex,
                    state.BehaviorKind ?? "",
                    state.Pinging,
                    state.PingCooldown,
                    state.PingLerp,
                    state.PingRadius,
                    state.PingedEntityCount,
                    state.RadiatorTemperature,
                    state.Emissivity,
                    state.PumpedHeat,
                    state.WasteHeat,
                    state.EnergyUsage,
                    state.ReactorDraw,
                    state.ReactorLoadRatio,
                    state.CapacitorCharge,
                    state.CapacitorCapacity,
                    state.CapacitorEfficiency,
                    state.AetherDriveAxisX,
                    state.AetherDriveAxisY,
                    state.AetherDriveAxisZ,
                    state.AetherDriveThrustX,
                    state.AetherDriveThrustY,
                    state.AetherDriveThrustZ,
                    state.AetherDriveRpmX,
                    state.AetherDriveRpmY,
                    state.AetherDriveRpmZ,
                    state.AetherDriveMaximumRpm,
                    state.AetherDriveThrustDirectionX,
                    state.AetherDriveThrustDirectionY,
                    state.ResourceScannerTargetBodyKey ?? "",
                    state.ResourceScannerAsteroidIndex,
                    state.ResourceScannerScanTime,
                    state.ResourceScannerRange,
                    state.ResourceScannerMinimumDensity,
                    state.ResourceScannerScanDuration,
                    state.MiningToolAsteroidBeltKey ?? "",
                    state.MiningToolAsteroidIndex,
                    state.MiningToolRange,
                    state.ThrusterAxis,
                    state.ThrusterThrust,
                    state.ThrusterTorque,
                    state.ShieldEfficiency,
                    state.ShieldEnergyUsage,
                    state.VelocityLimit,
                    state.ThermotoggleTargetTemperature,
                    state.SwitchActivated,
                    state.TriggerPulled,
                    state.StatModifierApplied,
                    state.StatModifierExecuted,
                    state.StatModifierTargetStatCount,
                    state.TurretControllerWeaponCount,
                    state.TurretControllerShotSpeed,
                    state.TurretControllerPredictShots))
                .ToArray();
        }

    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneRenderAdjacentZone
    {
        [Key(0)]
        public int ZoneIndex { get; set; } = -1;

        [Key(1)]
        public double X { get; set; }

        [Key(2)]
        public double Y { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneRenderWormholeExit
    {
        [Key(0)]
        public int TargetZoneIndex { get; set; } = -1;

        [Key(1)]
        public double DirectionX { get; set; }

        [Key(2)]
        public double DirectionZ { get; set; }

        [Key(3)]
        public double PositionX { get; set; }

        [Key(4)]
        public double PositionZ { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneRenderBodyPose
    {
        [Key(0)]
        public string BodyKey { get; set; } = "";

        [Key(1)]
        public string OrbitKey { get; set; } = "";

        [Key(2)]
        public string ParentOrbitKey { get; set; } = "";

        [Key(3)]
        public string Kind { get; set; } = "";

        [Key(4)]
        public double CenterX { get; set; }

        [Key(5)]
        public double CenterZ { get; set; }

        [Key(6)]
        public double ParentCenterX { get; set; }

        [Key(7)]
        public double ParentCenterZ { get; set; }

        [Key(8)]
        public double GravityWaveSpeed { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneRenderAsteroidBeltPose
    {
        [Key(0)]
        public string BodyKey { get; set; } = "";

        [Key(1)]
        public string OrbitKey { get; set; } = "";

        [Key(2)]
        public double CenterX { get; set; }

        [Key(3)]
        public double CenterZ { get; set; }

        [Key(4)]
        public double Radius { get; set; }

        [Key(5)]
        public int AsteroidCount { get; set; }

        [Key(6)]
        public IReadOnlyList<AetheriaRuntimeZoneRenderAsteroidInstancePose> InstancePoses { get; set; } =
            Array.Empty<AetheriaRuntimeZoneRenderAsteroidInstancePose>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeZoneRenderAsteroidInstancePose
    {
        [Key(0)]
        public string BodyKey { get; set; } = "";

        [Key(1)]
        public int AsteroidIndex { get; set; }

        [Key(2)]
        public double PositionX { get; set; }

        [Key(3)]
        public double PositionZ { get; set; }

        [Key(4)]
        public double Rotation { get; set; }

        [Key(5)]
        public double Size { get; set; }
    }

    [CultDocument("gamecult.aetheria.selected_object", "gamecult.aetheria.selected_object.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeSelectedObjectDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.SelectedObject;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string RunId { get; set; } = "";

        [Key(3)]
        public int ZoneIndex { get; set; }

        [Key(4)]
        public int EntityIndex { get; set; }

        [Key(5)]
        public AetheriaRuntimeViewportObject? Selected { get; set; }
    }

    [CultDocument("gamecult.aetheria.inventory", "gamecult.aetheria.inventory.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeInventoryDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.Inventory;

        [Key(1)]
        public long FrameId { get; set; }

        [Key(2)]
        public string RunId { get; set; } = "";

        [Key(3)]
        public int ZoneIndex { get; set; }

        [Key(4)]
        public int EntityIndex { get; set; }

        [Key(5)]
        public string EntityKey { get; set; } = "";

        [Key(6)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Items { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Equipment { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Cargo { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeViewportBounds : IEveFieldsViewport
    {
        [Key(0)]
        public double MinX { get; set; }

        [Key(1)]
        public double MinY { get; set; }

        [Key(2)]
        public double MaxX { get; set; }

        [Key(3)]
        public double MaxY { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeViewportObject
    {
        [Key(0)]
        public int EntityIndex { get; set; }

        [Key(1)]
        public string EntityKey { get; set; } = "";

        [Key(2)]
        public string DisplayName { get; set; } = "";

        [Key(3)]
        public string Kind { get; set; } = "";

        [Key(4)]
        public string FactionKey { get; set; } = "";

        [Key(5)]
        public double X { get; set; }

        [Key(6)]
        public double Y { get; set; }

        [Key(7)]
        public double Z { get; set; }

        [Key(8)]
        public double DirectionX { get; set; }

        [Key(9)]
        public double DirectionY { get; set; }

        [Key(10)]
        public double VelocityX { get; set; }

        [Key(11)]
        public double VelocityY { get; set; }

        [Key(12)]
        public bool Controlled { get; set; }

        [Key(13)]
        public int TargetEntityIndex { get; set; } = -1;

        [Key(14)]
        public bool IsActive { get; set; }

        [Key(15)]
        public double Visibility { get; set; }

        [Key(16)]
        public AetheriaRuntimeEntityStatus Status { get; set; } = new AetheriaRuntimeEntityStatus();

        [Key(17)]
        public IReadOnlyList<AetheriaRuntimeInventoryItem> Inventory { get; set; } =
            Array.Empty<AetheriaRuntimeInventoryItem>();

        [Key(18)]
        public AetheriaRuntimeAssetRef IconAsset { get; set; } =
            AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Sprite);
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEntityStatus
    {
        [Key(0)]
        public double Hull { get; set; }

        [Key(1)]
        public double Shield { get; set; }

        [Key(2)]
        public double Heat { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInventoryItem
    {
        [Key(0)]
        public string Source { get; set; } = "";

        [Key(1)]
        public string ItemKey { get; set; } = "";

        [Key(2)]
        public int Quantity { get; set; }

        [Key(3)]
        public double Quality { get; set; }

        [Key(4)]
        public double Durability { get; set; }

        [Key(5)]
        public bool Enabled { get; set; }

        [Key(6)]
        public int SourceIndex { get; set; } = -1;

        [Key(7)]
        public int X { get; set; } = -1;

        [Key(8)]
        public int Y { get; set; } = -1;

        [Key(9)]
        public AetheriaRuntimeAssetRef IconAsset { get; set; } =
            AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Texture);
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeGravityInfluence
    {
        [Key(0)]
        public string BodyKey { get; set; } = "";

        [Key(1)]
        public string OrbitKey { get; set; } = "";

        [Key(2)]
        public string Kind { get; set; } = "";

        [Key(3)]
        public double X { get; set; }

        [Key(4)]
        public double Y { get; set; }

        [Key(5)]
        public double Radius { get; set; }

        [Key(6)]
        public double GravityDepth { get; set; }

        [Key(7)]
        public double GravityDepthExponent { get; set; }

        [Key(8)]
        public double WaveRadius { get; set; }

        [Key(9)]
        public double WaveDepth { get; set; }

        [Key(10)]
        public double WaveSpeed { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeBodyView
    {
        [Key(0)]
        public string BodyKey { get; set; } = "";

        [Key(1)]
        public string OrbitKey { get; set; } = "";

        [Key(2)]
        public string Name { get; set; } = "";

        [Key(3)]
        public string Kind { get; set; } = "";

        [Key(4)]
        public double X { get; set; }

        [Key(5)]
        public double Y { get; set; }

        [Key(6)]
        public double Radius { get; set; }

        [Key(7)]
        public bool IsAsteroidBelt { get; set; }

        [Key(8)]
        public AetheriaRuntimeBodySnapshotCommit Body { get; set; } =
            new AetheriaRuntimeBodySnapshotCommit();

        [Key(9)]
        public AetheriaRuntimeAssetRef IconAsset { get; set; } =
            AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Sprite);

        [Key(10)]
        public double IconSize { get; set; }
    }
}
