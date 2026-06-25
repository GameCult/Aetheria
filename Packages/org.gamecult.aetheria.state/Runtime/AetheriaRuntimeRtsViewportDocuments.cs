using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [CultDocument("gamecult.aetheria.rts_viewport", "gamecult.aetheria.rts_viewport.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeRtsViewportDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.RtsViewport;

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
        public AetheriaRuntimeRtsViewportBounds Viewport { get; set; } = new AetheriaRuntimeRtsViewportBounds();

        [Key(9)]
        public IReadOnlyList<int> ControlledEntityIndices { get; set; } = Array.Empty<int>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeRtsViewportObject> Objects { get; set; } =
            Array.Empty<AetheriaRuntimeRtsViewportObject>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeRtsGravityInfluence> GravityInfluences { get; set; } =
            Array.Empty<AetheriaRuntimeRtsGravityInfluence>();

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeRtsBodyView> Bodies { get; set; } =
            Array.Empty<AetheriaRuntimeRtsBodyView>();
    }

    [CultDocument("gamecult.aetheria.objects_viewport", "gamecult.aetheria.objects_viewport.v1")]
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
        public AetheriaRuntimeRtsViewportBounds Viewport { get; set; } = new AetheriaRuntimeRtsViewportBounds();

        [Key(9)]
        public IReadOnlyList<int> ControlledEntityIndices { get; set; } = Array.Empty<int>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeRtsViewportObject> Objects { get; set; } =
            Array.Empty<AetheriaRuntimeRtsViewportObject>();
    }

    [CultDocument("gamecult.aetheria.gravity_viewport", "gamecult.aetheria.gravity_viewport.v1")]
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
        public AetheriaRuntimeRtsViewportBounds Viewport { get; set; } = new AetheriaRuntimeRtsViewportBounds();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeRtsGravityInfluence> GravityInfluences { get; set; } =
            Array.Empty<AetheriaRuntimeRtsGravityInfluence>();

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeRtsBodyView> Bodies { get; set; } =
            Array.Empty<AetheriaRuntimeRtsBodyView>();

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
        public AetheriaRuntimeRtsViewportObject? Entity { get; set; }

        [Key(9)]
        public AetheriaRuntimeRtsEntityStatus Status { get; set; } = new AetheriaRuntimeRtsEntityStatus();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Inventory { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Equipment { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Cargo { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();

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
        public AetheriaRuntimeRtsViewportObject? DockParent { get; set; }

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
        public AetheriaRuntimeRtsViewportObject? DockParent { get; set; }

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
        public IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> EntityFacades { get; set; } =
            Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();

        [Key(18)]
        public IReadOnlyList<AetheriaRuntimeOrbitSnapshotCommit> Orbits { get; set; } =
            Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>();

        [Key(19)]
        public IReadOnlyList<AetheriaRuntimeBodySnapshotCommit> Bodies { get; set; } =
            Array.Empty<AetheriaRuntimeBodySnapshotCommit>();

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
        public AetheriaRuntimeRtsViewportObject? Selected { get; set; }
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
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Items { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Equipment { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();

        [Key(8)]
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Cargo { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRtsViewportBounds
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
    public sealed class AetheriaRuntimeRtsViewportObject
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
        public AetheriaRuntimeRtsEntityStatus Status { get; set; } = new AetheriaRuntimeRtsEntityStatus();

        [Key(17)]
        public IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Inventory { get; set; } =
            Array.Empty<AetheriaRuntimeRtsInventoryItem>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRtsEntityStatus
    {
        [Key(0)]
        public double Hull { get; set; }

        [Key(1)]
        public double Shield { get; set; }

        [Key(2)]
        public double Heat { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRtsInventoryItem
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
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeRtsGravityInfluence
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
    public sealed class AetheriaRuntimeRtsBodyView
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
    }
}
