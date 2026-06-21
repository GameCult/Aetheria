using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonCommandEnvelope
    {
        public AetheriaRuntimeDaemonCommandEnvelope(
            string schema,
            string commandId,
            string clientId,
            string issuedAtUtc,
            string sessionId,
            long observedFrameId,
            AetheriaRuntimeDaemonCommandKinds kind,
            string actorEntityKey,
            string path)
        {
            Schema = schema;
            CommandId = commandId;
            ClientId = clientId;
            IssuedAtUtc = issuedAtUtc;
            SessionId = sessionId;
            ObservedFrameId = observedFrameId;
            Kind = kind;
            ActorEntityKey = actorEntityKey;
            Path = path;
        }

        public string Schema { get; }
        public string CommandId { get; }
        public string ClientId { get; }
        public string IssuedAtUtc { get; }
        public string SessionId { get; }
        public long ObservedFrameId { get; }
        public AetheriaRuntimeDaemonCommandKinds Kind { get; }
        public string ActorEntityKey { get; }
        public string Path { get; }
    }

    public static class AetheriaRuntimeDaemonSchemas
    {
        public const string Frame = "gamecult.aetheria.daemon_frame.v1";
        public const string Command = "gamecult.aetheria.daemon_command.v1";
        public const string SoaView = "gamecult.aetheria.daemon_soa_view.v1";
        public const string ProviderAdvertisement = "gamecult.aetheria.daemon_provider_advertisement.v1";
        public const string Health = "gamecult.aetheria.daemon_health.v1";
        public const string CommandBoundary = "gamecult.aetheria.daemon_command_boundary.v1";
        public const string GameSurface = "gamecult.aetheria.daemon_game_surface.v1";
        public const string EditorSurface = "gamecult.aetheria.daemon_editor_surface.v1";
    }

    public enum AetheriaRuntimeDaemonCommandKinds
    {
        None = 0,
        Ping,
        SetTarget,
        ClearTarget,
        TargetNearest,
        TargetNext,
        TargetPrevious,
        SetMoveVector,
        SetLookDirection,
        SetTractorPower,
        FireWeaponGroup,
        SetWeaponGroupActive,
        SetWeaponGroupMembership,
        SetBehaviorActive,
        ActivateConsumable,
        SensorPing,
        SetItemEnabled,
        SetItemOverrideShutdown,
        SetThermotoggleTargetTemperature,
        SetActionBarBinding,
        ClearActionBarBinding,
        PickUpLoot,
        RestoreLoadout,
        Dock,
        DockNearest,
        Undock,
        SetDockedCurrentShip,
        TowToStation,
        EnterWormhole,
        TradePurchase,
        TransferCargoItem,
        EquipItem,
        StoreItem,
        ToggleHullConductivity,
        SetEntityName,
        DestroyEntity,
        SetOverrideShutdown,
        SetHeatsinksEnabled,
        SetShutdownPerformance,
        ToggleShieldEnabled
    }

    [CultDocument("gamecult.aetheria.daemon_provider_advertisement", "gamecult.aetheria.daemon_provider_advertisement.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonProviderAdvertisementDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.ProviderAdvertisement;

        [Key(1)]
        public string VerseId { get; set; } = "aetheria.local";

        [Key(2)]
        public string ProviderId { get; set; } = "aetheria.daemon";

        [Key(3)]
        public string DaemonId { get; set; } = "aetheria-daemon";

        [Key(4)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(5)]
        public string StateWitnessPath { get; set; } = "";

        [Key(6)]
        public string FrameWitnessPath { get; set; } = "";

        [Key(7)]
        public string SoaWitnessPath { get; set; } = "";

        [Key(8)]
        public string HealthWitnessPath { get; set; } = "";

        [Key(9)]
        public string CommandBoundaryWitnessPath { get; set; } = "";

        [Key(10)]
        public string EveGuiSurfaceWitnessPath { get; set; } = "";

        [Key(11)]
        public string EveTuiSurfaceWitnessPath { get; set; } = "";

        [Key(12)]
        public string EveGuiSurfaceId { get; set; } = "aetheria.game";

        [Key(13)]
        public string EveTuiSurfaceId { get; set; } = "aetheria.game.tui";

        [Key(14)]
        public string EditorGuiSurfaceWitnessPath { get; set; } = "";

        [Key(15)]
        public string EditorTuiSurfaceWitnessPath { get; set; } = "";

        [Key(16)]
        public string EditorGuiSurfaceId { get; set; } = "aetheria.daemon.editor";

        [Key(17)]
        public string EditorTuiSurfaceId { get; set; } = "aetheria.daemon.editor.tui";

        [Key(18)]
        public IReadOnlyList<string> PublishedSchemas { get; set; } = Array.Empty<string>();

        [Key(19)]
        public IReadOnlyList<string> CommandBoundaryIds { get; set; } = Array.Empty<string>();

        [Key(20)]
        public string CultMeshAddress { get; set; } = "cultmesh://aetheria.local/eve/providers/aetheria.daemon";

        public static AetheriaRuntimeDaemonProviderAdvertisementDocument Create(
            string stateFilePath,
            string daemonId,
            string verseId,
            string cultMeshAddress)
        {
            return new AetheriaRuntimeDaemonProviderAdvertisementDocument
            {
                VerseId = string.IsNullOrWhiteSpace(verseId) ? "aetheria.local" : verseId,
                DaemonId = string.IsNullOrWhiteSpace(daemonId) ? "aetheria-daemon" : daemonId,
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                StateWitnessPath = stateFilePath ?? "",
                FrameWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonFramePath(stateFilePath ?? ""),
                SoaWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(stateFilePath ?? ""),
                HealthWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonHealthPath(stateFilePath ?? ""),
                CommandBoundaryWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonCommandBoundaryPath(stateFilePath ?? ""),
                EveGuiSurfaceWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonGameSurfacePath(stateFilePath ?? ""),
                EveTuiSurfaceWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonGameTuiSurfacePath(stateFilePath ?? ""),
                EveGuiSurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
                EveTuiSurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId,
                EditorGuiSurfaceWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonEditorSurfacePath(stateFilePath ?? ""),
                EditorTuiSurfaceWitnessPath = AetheriaRuntimeStateBoundary.GetDaemonEditorTuiSurfacePath(stateFilePath ?? ""),
                EditorGuiSurfaceId = AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId,
                EditorTuiSurfaceId = AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId,
                CultMeshAddress = string.IsNullOrWhiteSpace(cultMeshAddress)
                    ? "cultmesh://aetheria.local/eve/providers/aetheria.daemon"
                    : cultMeshAddress,
                PublishedSchemas = new[]
                {
                    AetheriaRuntimeDaemonSchemas.Frame,
                    AetheriaRuntimeDaemonSchemas.SoaView,
                    AetheriaRuntimeDaemonSchemas.Health,
                    AetheriaRuntimeDaemonSchemas.CommandBoundary,
                    AetheriaRuntimeDaemonSchemas.GameSurface,
                    AetheriaRuntimeDaemonSchemas.EditorSurface,
                    AetheriaRuntimeDaemonSchemas.Command
                },
                CommandBoundaryIds = new[] { "aetheria.daemon.commands" }
            };
        }
    }

    [CultDocument("gamecult.aetheria.daemon_health", "gamecult.aetheria.daemon_health.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonHealthDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.Health;

        [Key(1)]
        public string DaemonId { get; set; } = "aetheria-daemon";

        [Key(2)]
        public string VerseId { get; set; } = "aetheria.local";

        [Key(3)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(4)]
        public string StatePath { get; set; } = "";

        [Key(5)]
        public long FrameId { get; set; }

        [Key(6)]
        public int ObservedCommandCount { get; set; }

        [Key(7)]
        public int AppliedCommandCount { get; set; }

        [Key(8)]
        public int RejectedCommandCount { get; set; }

        [Key(9)]
        public string Status { get; set; } = "healthy";

        [Key(10)]
        public string PublicationSource { get; set; } = "daemon-published";

        [Key(11)]
        public string Transport { get; set; } = "cultcache-witness";

        [Key(12)]
        public string CommandBoundaryPath { get; set; } = "";
    }

    [CultDocument("gamecult.aetheria.daemon_command_boundary", "gamecult.aetheria.daemon_command_boundary.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonCommandBoundaryDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.CommandBoundary;

        [Key(1)]
        public string BoundaryId { get; set; } = "aetheria.daemon.commands";

        [Key(2)]
        public string DaemonId { get; set; } = "aetheria-daemon";

        [Key(3)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(4)]
        public string CommandSchema { get; set; } = AetheriaRuntimeDaemonSchemas.Command;

        [Key(5)]
        public string ReceiptSchema { get; set; } = AetheriaRuntimeDaemonSchemas.Frame;

        [Key(6)]
        public string Delivery { get; set; } = "cultnet-document-put";

        [Key(7)]
        public IReadOnlyList<AetheriaRuntimeDaemonCommandBoundaryEntry> Commands { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonCommandBoundaryEntry>();

        public static AetheriaRuntimeDaemonCommandBoundaryDocument Create(string daemonId)
        {
            return new AetheriaRuntimeDaemonCommandBoundaryDocument
            {
                DaemonId = string.IsNullOrWhiteSpace(daemonId) ? "aetheria-daemon" : daemonId,
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                Commands = Enum
                    .GetValues(typeof(AetheriaRuntimeDaemonCommandKinds))
                    .Cast<AetheriaRuntimeDaemonCommandKinds>()
                    .Where(kind => kind != AetheriaRuntimeDaemonCommandKinds.None)
                    .Select(kind => new AetheriaRuntimeDaemonCommandBoundaryEntry
                    {
                        Kind = kind,
                        CommandBody = CommandBodyFor(kind),
                        Authority = "local-player-input",
                        Receipt = "applied-or-rejected-command-id-in-frame"
                    })
                    .ToArray()
            };
        }

        private static string CommandBodyFor(AetheriaRuntimeDaemonCommandKinds kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding:
                    return nameof(AetheriaRuntimeActionBarBindingCommand);
                case AetheriaRuntimeDaemonCommandKinds.TransferCargoItem:
                    return nameof(AetheriaRuntimeCargoTransferCommand);
                case AetheriaRuntimeDaemonCommandKinds.TradePurchase:
                    return nameof(AetheriaRuntimeTradePurchaseCommand);
                case AetheriaRuntimeDaemonCommandKinds.PickUpLoot:
                    return nameof(AetheriaRuntimeLootPickupCommand);
                case AetheriaRuntimeDaemonCommandKinds.RestoreLoadout:
                    return nameof(AetheriaRuntimeLoadoutRestoreCommand);
                case AetheriaRuntimeDaemonCommandKinds.EquipItem:
                    return nameof(AetheriaRuntimeEquipmentTransferCommand);
                case AetheriaRuntimeDaemonCommandKinds.StoreItem:
                    return nameof(AetheriaRuntimeStoreItemCommand);
                default:
                    return nameof(AetheriaRuntimeDaemonCommandDocument);
            }
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonCommandBoundaryEntry
    {
        [Key(0)]
        public AetheriaRuntimeDaemonCommandKinds Kind { get; set; }

        [Key(1)]
        public string CommandBody { get; set; } = "";

        [Key(2)]
        public string Authority { get; set; } = "";

        [Key(3)]
        public string Receipt { get; set; } = "";
    }

    [CultDocument("gamecult.aetheria.daemon_frame", "gamecult.aetheria.daemon_frame.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonFrameDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.Frame;

        [Key(1)]
        public string DaemonId { get; set; } = "local";

        [Key(2)]
        public string SessionId { get; set; } = "local";

        [Key(3)]
        public long FrameId { get; set; }

        [Key(4)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(5)]
        public double SimulationTimeSeconds { get; set; }

        [Key(6)]
        public double FixedDeltaSeconds { get; set; }

        [Key(7)]
        public bool IsAuthoritative { get; set; } = true;

        [Key(8)]
        public string StateSource { get; set; } = "daemon";

        [Key(9)]
        public AetheriaRuntimeRunCheckpointCommit Run { get; set; } = new AetheriaRuntimeRunCheckpointCommit();

        [Key(10)]
        public IReadOnlyList<string> Capabilities { get; set; } = Array.Empty<string>();

        [Key(11)]
        public IReadOnlyList<string> AppliedCommandIds { get; set; } = Array.Empty<string>();

        [Key(12)]
        public IReadOnlyList<string> RejectedCommandIds { get; set; } = Array.Empty<string>();

        [Key(13)]
        public IReadOnlyList<string> AccountedCommandIds { get; set; } = Array.Empty<string>();

        public static AetheriaRuntimeDaemonFrameDocument Create(
            AetheriaRuntimeRunCheckpointCommit run,
            string daemonId,
            string sessionId,
            long frameId,
            double simulationTimeSeconds,
            double fixedDeltaSeconds,
            bool isAuthoritative = true,
            string stateSource = "daemon")
        {
            return new AetheriaRuntimeDaemonFrameDocument
            {
                DaemonId = string.IsNullOrWhiteSpace(daemonId) ? "local" : daemonId,
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId,
                FrameId = frameId,
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                SimulationTimeSeconds = simulationTimeSeconds,
                FixedDeltaSeconds = fixedDeltaSeconds,
                IsAuthoritative = isAuthoritative,
                StateSource = string.IsNullOrWhiteSpace(stateSource) ? "daemon" : stateSource,
                Run = run ?? new AetheriaRuntimeRunCheckpointCommit()
            };
        }
    }

    [CultDocument("gamecult.aetheria.daemon_command", "gamecult.aetheria.daemon_command.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonCommandDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.Command;

        [Key(1)]
        public string CommandId { get; set; } = "";

        [Key(2)]
        public string ClientId { get; set; } = "";

        [Key(3)]
        public string IssuedAtUtc { get; set; } = "";

        [Key(4)]
        public string SessionId { get; set; } = "";

        [Key(5)]
        public long ObservedFrameId { get; set; } = -1;

        [Key(6)]
        public AetheriaRuntimeDaemonCommandKinds Kind { get; set; } = AetheriaRuntimeDaemonCommandKinds.None;

        [Key(7)]
        public string ActorEntityKey { get; set; } = "";

        [Key(8)]
        public string TargetEntityKey { get; set; } = "";

        [Key(9)]
        public int TargetZoneIndex { get; set; } = -1;

        [Key(10)]
        public int EquipmentIndex { get; set; } = -1;

        [Key(11)]
        public int BehaviorIndex { get; set; } = -1;

        [Key(12)]
        public int WeaponGroup { get; set; } = -1;

        [Key(13)]
        public double PositionX { get; set; }

        [Key(14)]
        public double PositionY { get; set; }

        [Key(15)]
        public double PositionZ { get; set; }

        [Key(16)]
        public double DirectionX { get; set; }

        [Key(17)]
        public double DirectionY { get; set; }

        [Key(18)]
        public double ScalarValue { get; set; }

        [Key(19)]
        public string TextValue { get; set; } = "";

        [Key(20)]
        public AetheriaRuntimeActionBarBindingCommand ActionBarBinding { get; set; } = new AetheriaRuntimeActionBarBindingCommand();

        [Key(21)]
        public AetheriaRuntimeCargoTransferCommand CargoTransfer { get; set; } = new AetheriaRuntimeCargoTransferCommand();

        [Key(22)]
        public AetheriaRuntimeTradePurchaseCommand TradePurchase { get; set; } = new AetheriaRuntimeTradePurchaseCommand();

        [Key(23)]
        public AetheriaRuntimeLootPickupCommand LootPickup { get; set; } = new AetheriaRuntimeLootPickupCommand();

        [Key(24)]
        public AetheriaRuntimeLoadoutRestoreCommand LoadoutRestore { get; set; } = new AetheriaRuntimeLoadoutRestoreCommand();

        [Key(25)]
        public AetheriaRuntimeEquipmentTransferCommand EquipmentTransfer { get; set; } = new AetheriaRuntimeEquipmentTransferCommand();

        [Key(26)]
        public AetheriaRuntimeStoreItemCommand StoreItem { get; set; } = new AetheriaRuntimeStoreItemCommand();

        public static AetheriaRuntimeDaemonCommandDocument Create(
            AetheriaRuntimeDaemonCommandKinds kind,
            string clientId,
            string sessionId,
            long observedFrameId,
            string actorEntityKey)
        {
            return new AetheriaRuntimeDaemonCommandDocument
            {
                CommandId = Guid.NewGuid().ToString("N"),
                ClientId = clientId ?? "",
                IssuedAtUtc = DateTime.UtcNow.ToString("O"),
                SessionId = sessionId ?? "",
                ObservedFrameId = observedFrameId,
                Kind = kind,
                ActorEntityKey = actorEntityKey ?? ""
            };
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeActionBarBindingCommand
    {
        [Key(0)]
        public string Kind { get; set; } = "";

        [Key(1)]
        public string ItemKey { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeCargoTransferCommand
    {
        [Key(0)]
        public string OriginEntityKey { get; set; } = "";

        [Key(1)]
        public int OriginCargoIndex { get; set; } = -1;

        [Key(2)]
        public string DestinationEntityKey { get; set; } = "";

        [Key(3)]
        public int DestinationCargoIndex { get; set; } = -1;

        [Key(4)]
        public int SourceX { get; set; } = int.MinValue;

        [Key(5)]
        public int SourceY { get; set; } = int.MinValue;

        [Key(6)]
        public int DestinationX { get; set; }

        [Key(7)]
        public int DestinationY { get; set; }

        [Key(8)]
        public bool HasDestinationPosition { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeTradePurchaseCommand
    {
        [Key(0)]
        public string PurchaseKind { get; set; } = "";

        [Key(1)]
        public string ItemKey { get; set; } = "";

        [Key(2)]
        public int Quantity { get; set; } = 1;

        [Key(3)]
        public int UnitPrice { get; set; }

        [Key(4)]
        public int TotalPrice { get; set; }

        [Key(5)]
        public string StationEntityKey { get; set; } = "";

        [Key(6)]
        public int StationCargoIndex { get; set; } = -1;

        [Key(7)]
        public string TargetEntityKey { get; set; } = "";

        [Key(8)]
        public int TargetCargoIndex { get; set; } = -1;

        [Key(9)]
        public int SourceX { get; set; } = int.MinValue;

        [Key(10)]
        public int SourceY { get; set; } = int.MinValue;

        [Key(11)]
        public bool CreatesDockedShip { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeLootPickupCommand
    {
        [Key(0)]
        public string ItemKey { get; set; } = "";

        [Key(1)]
        public int Quantity { get; set; } = 1;

        [Key(2)]
        public double PositionX { get; set; }

        [Key(3)]
        public double PositionY { get; set; }

        [Key(4)]
        public double PositionZ { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeLoadoutRestoreCommand
    {
        [Key(0)]
        public string DockedEntityKey { get; set; } = "";

        [Key(1)]
        public string TemplateName { get; set; } = "";

        [Key(2)]
        public int Price { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEquipmentTransferCommand
    {
        [Key(0)]
        public string SourceKind { get; set; } = "";

        [Key(1)]
        public string OriginEntityKey { get; set; } = "";

        [Key(2)]
        public int OriginIndex { get; set; } = -1;

        [Key(3)]
        public string DestinationEntityKey { get; set; } = "";

        [Key(4)]
        public int SourceX { get; set; } = int.MinValue;

        [Key(5)]
        public int SourceY { get; set; } = int.MinValue;

        [Key(6)]
        public int DestinationX { get; set; }

        [Key(7)]
        public int DestinationY { get; set; }

        [Key(8)]
        public bool HasDestinationPosition { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStoreItemCommand
    {
        [Key(0)]
        public string OriginEntityKey { get; set; } = "";

        [Key(1)]
        public int SourceEquipmentIndex { get; set; } = -1;

        [Key(2)]
        public string DestinationEntityKey { get; set; } = "";

        [Key(3)]
        public int DestinationCargoIndex { get; set; } = -1;

        [Key(4)]
        public int DestinationX { get; set; }

        [Key(5)]
        public int DestinationY { get; set; }

        [Key(6)]
        public bool HasDestinationPosition { get; set; }
    }
}
