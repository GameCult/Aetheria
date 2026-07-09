using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Mesh;
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
            string path,
            CultMeshOperationReceipt? receipt = null)
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
            Receipt = receipt ?? AetheriaRuntimeDaemonOperationIds.CreateReceipt(kind);
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
        public CultMeshOperationReceipt Receipt { get; }
        public string OperationId => Receipt.OperationId;
        public bool Accepted => Receipt.Accepted;
        public CultMeshRouteHint Route => Receipt.Route;
        public string? Diagnostic => Receipt.Diagnostic;

        public static implicit operator CultMeshOperationReceipt(AetheriaRuntimeDaemonCommandEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            return envelope.Receipt;
        }
    }

    public static class AetheriaRuntimeDaemonSchemas
    {
        public const string Frame = "gamecult.aetheria.daemon_frame.v1";
        public const string Command = "gamecult.aetheria.daemon_command.v1";
        public const string CommittedCommandFact = "gamecult.aetheria.committed_command_fact.v1";
        public const string GameViewport = "gamecult.aetheria.game_viewport.v1";
        public const string ObjectsViewport = "gamecult.aetheria.objects_viewport.v1";
        public const string GravityViewport = "gamecult.aetheria.gravity_viewport.v1";
        public const string RenderSplatsViewport = "gamecult.aetheria.render_splats_viewport.v1";
        public const string AssetManifest = "gamecult.aetheria.asset_manifest.v1";
        public const string CultMeshCdnAssetBlob = "gamecult.cultmesh.cdn.asset_blob.v1";
        public const string CurrentZone = "gamecult.aetheria.current_zone.v1";
        public const string CurrentEntity = "gamecult.aetheria.current_entity.v1";
        public const string CurrentDocking = "gamecult.aetheria.current_docking.v1";
        public const string ZoneContacts = "gamecult.aetheria.zone_contacts.v1";
        public const string StationRefit = "gamecult.aetheria.station_refit.v1";
        public const string SectorMap = "gamecult.aetheria.sector_map.v1";
        public const string ZoneDetails = "gamecult.aetheria.zone_details.v1";
        public const string ZoneRender = "gamecult.aetheria.zone_render.v1";
        public const string SelectedObject = "gamecult.aetheria.selected_object.v1";
        public const string Inventory = "gamecult.aetheria.inventory.v1";
        public const string StarbridgeScenario = "gamecult.aetheria.starbridge_scenario.v1";
        public const string StarbridgeSession = "gamecult.aetheria.starbridge_session.v1";
        public const string StarbridgeSessionSummary = "gamecult.aetheria.starbridge_session_summary.v1";
        public const string StarbridgePlayerSeat = "gamecult.aetheria.starbridge_player_seat.v1";
        public const string VerseAuthorityPolicy = AetheriaRuntimeVerseAuthoritySchemas.Policy;
        public const string AuthorityLease = AetheriaRuntimeVerseAuthoritySchemas.Lease;
        public const string SoaView = "gamecult.aetheria.daemon_soa_view.v1";
        public const string ProviderAdvertisement = "gamecult.aetheria.daemon_provider_advertisement.v1";
        public const string Health = "gamecult.aetheria.daemon_health.v1";
        public const string CommandBoundary = "gamecult.aetheria.daemon_command_boundary.v1";
        public const string EveCommandAcceptanceStatus = "aetheria.eve_command_acceptance_status.v1";
        public const string GameSurface = "gamecult.aetheria.daemon_game_surface.v1";
        public const string EditorSurface = "gamecult.aetheria.daemon_editor_surface.v1";
    }

    public static class AetheriaRuntimeDaemonOperationIds
    {
        public static string SetMoveVector => ForKind(AetheriaRuntimeDaemonCommandKinds.SetMoveVector);
        public static string SetTarget => ForKind(AetheriaRuntimeDaemonCommandKinds.SetTarget);

        public static string ForKind(AetheriaRuntimeDaemonCommandKinds kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetMoveVector:
                    return "gamecult.aetheria.pilot.set_move_vector.v1";
                case AetheriaRuntimeDaemonCommandKinds.SetTarget:
                    return "gamecult.aetheria.pilot.set_target.v1";
                case AetheriaRuntimeDaemonCommandKinds.None:
                    return "gamecult.aetheria.daemon.none.v1";
                default:
                    return "gamecult.aetheria.daemon." + ToSnakeCase(kind.ToString()) + ".v1";
            }
        }

        public static CultMeshOperationReceipt CreateReceipt(
            AetheriaRuntimeDaemonCommandKinds kind,
            bool accepted = true,
            CultMeshRouteHint? route = null,
            string? diagnostic = null)
        {
            return new CultMeshOperationReceipt(
                ForKind(kind),
                accepted,
                route ?? new CultMeshRouteHint(CultMeshLocalityKind.Network, "aetheria-daemon-command"),
                diagnostic);
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var chars = new List<char>(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        chars.Add('_');
                    chars.Add(char.ToLowerInvariant(c));
                }
                else
                {
                    chars.Add(c);
                }
            }

            return new string(chars.ToArray());
        }
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
        TargetReticle,
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
        PickUpLoot,
        RestoreLoadout,
        Dock,
        DockNearest,
        Undock,
        Interact,
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
        public string StateRecordRef { get; set; } = "";

        [Key(6)]
        public string FrameRecordRef { get; set; } = "";

        [Key(7)]
        public string SoaViewRecordRef { get; set; } = "";

        [Key(8)]
        public string HealthRecordRef { get; set; } = "";

        [Key(9)]
        public string CommandBoundaryRecordRef { get; set; } = "";

        [Key(10)]
        public string EveGuiSurfaceRecordRef { get; set; } = "";

        [Key(11)]
        public string EveTuiSurfaceRecordRef { get; set; } = "";

        [Key(12)]
        public string EveGuiSurfaceId { get; set; } = "aetheria.game";

        [Key(13)]
        public string EveTuiSurfaceId { get; set; } = "aetheria.game.tui";

        [Key(14)]
        public string EditorGuiSurfaceRecordRef { get; set; } = "";

        [Key(15)]
        public string EditorTuiSurfaceRecordRef { get; set; } = "";

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

        [Key(21)]
        public string AssetManifestRecordRef { get; set; } = "";

        [Key(22)]
        public IReadOnlyList<AetheriaRuntimeEveSurfaceAdvertisement> EveSurfaces { get; set; } =
            Array.Empty<AetheriaRuntimeEveSurfaceAdvertisement>();

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
                StateRecordRef = stateFilePath ?? "",
                FrameRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                SoaViewRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest.ToString(),
                HealthRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString(),
                AssetManifestRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString(),
                CommandBoundaryRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(),
                EveGuiSurfaceRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(),
                EveTuiSurfaceRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString(),
                EveGuiSurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
                EveTuiSurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId,
                EditorGuiSurfaceRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString(),
                EditorTuiSurfaceRecordRef = AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString(),
                EditorGuiSurfaceId = AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId,
                EditorTuiSurfaceId = AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId,
                CultMeshAddress = string.IsNullOrWhiteSpace(cultMeshAddress)
                    ? "cultmesh://aetheria.local/eve/providers/aetheria.daemon"
                    : cultMeshAddress,
                EveSurfaces = AetheriaRuntimeEveSurfaceCatalog.All,
                PublishedSchemas = new[]
                {
                    AetheriaRuntimeDaemonSchemas.Frame,
                    AetheriaRuntimeDaemonSchemas.SoaView,
                    AetheriaRuntimeDaemonSchemas.Health,
                    AetheriaRuntimeDaemonSchemas.CommandBoundary,
                    AetheriaRuntimeDaemonSchemas.ObjectsViewport,
                    AetheriaRuntimeDaemonSchemas.GravityViewport,
                    AetheriaRuntimeDaemonSchemas.RenderSplatsViewport,
                    AetheriaRuntimeDaemonSchemas.AssetManifest,
                    AetheriaRuntimeDaemonSchemas.CurrentZone,
                    AetheriaRuntimeDaemonSchemas.CurrentEntity,
                    AetheriaRuntimeDaemonSchemas.CurrentDocking,
                    AetheriaRuntimeDaemonSchemas.ZoneContacts,
                    AetheriaRuntimeDaemonSchemas.StationRefit,
                    AetheriaRuntimeDaemonSchemas.SectorMap,
                    AetheriaRuntimeDaemonSchemas.ZoneDetails,
                    AetheriaRuntimeDaemonSchemas.ZoneRender,
                    AetheriaRuntimeDaemonSchemas.SelectedObject,
                    AetheriaRuntimeDaemonSchemas.Inventory,
                    AetheriaRuntimeDaemonSchemas.GameSurface,
                    AetheriaRuntimeDaemonSchemas.EditorSurface,
                    "gamecult.eve.surface.v1",
                    AetheriaRuntimeDaemonSchemas.Command,
                    AetheriaRuntimeDaemonSchemas.StarbridgeScenario,
                    AetheriaRuntimeDaemonSchemas.StarbridgeSession,
                    AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                    AetheriaRuntimeDaemonSchemas.StarbridgePlayerSeat,
                    AetheriaRuntimeDaemonSchemas.VerseAuthorityPolicy,
                    AetheriaRuntimeDaemonSchemas.AuthorityLease
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

        [Key(14)]
        public IReadOnlyList<string> CumulativeAppliedCommandIds { get; set; } = Array.Empty<string>();

        [Key(15)]
        public IReadOnlyList<string> CumulativeRejectedCommandIds { get; set; } = Array.Empty<string>();

        [Key(16)]
        public IReadOnlyList<string> ImportedFactIds { get; set; } = Array.Empty<string>();

        [Key(17)]
        public IReadOnlyList<string> RejectedImportedFactIds { get; set; } = Array.Empty<string>();

        [Key(18)]
        public IReadOnlyList<string> DuplicateImportedFactIds { get; set; } = Array.Empty<string>();

        [Key(19)]
        public IReadOnlyList<string> CumulativeImportedFactIds { get; set; } = Array.Empty<string>();

        [Key(20)]
        public IReadOnlyList<string> CumulativeRejectedImportedFactIds { get; set; } = Array.Empty<string>();

        [Key(21)]
        public AetheriaRuntimeDaemonRenderSettings RenderSettings { get; set; } =
            AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;

        [Key(22)]
        public AetheriaRuntimeDaemonSimulationSettings SimulationSettings { get; set; } =
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault;

        public static AetheriaRuntimeDaemonFrameDocument Create(
            AetheriaRuntimeRunCheckpointCommit run,
            string daemonId,
            string sessionId,
            long frameId,
            double simulationTimeSeconds,
            double fixedDeltaSeconds,
            bool isAuthoritative = true,
            string stateSource = "daemon",
            AetheriaRuntimeDaemonRenderSettings? renderSettings = null,
            AetheriaRuntimeDaemonSimulationSettings? simulationSettings = null)
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
                Run = run ?? new AetheriaRuntimeRunCheckpointCommit(),
                RenderSettings = renderSettings ?? AetheriaRuntimeDaemonRenderSettings.AetheriaDefault,
                SimulationSettings = simulationSettings ?? AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault
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

        [Key(27)]
        public string AuthorRuntimeId { get; set; } = "";

        [Key(28)]
        public string SubjectKey { get; set; } = "";

        [Key(29)]
        public string ClaimKind { get; set; } = "";

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
                ActorEntityKey = actorEntityKey ?? "",
                AuthorRuntimeId = clientId ?? "",
                SubjectKey = actorEntityKey ?? "",
                ClaimKind = AetheriaRuntimeAuthorityRouter.ResolveClaimKind(kind)
            };
        }
    }

    public static class AetheriaRuntimeCommandFactOutcomes
    {
        public const string Applied = "applied";
        public const string Rejected = "rejected";
    }

    [CultDocument("gamecult.aetheria.committed_command_fact", "gamecult.aetheria.committed_command_fact.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeCommittedCommandFactDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.CommittedCommandFact;

        [Key(1)]
        public string FactId { get; set; } = "";

        [Key(2)]
        public string VerseId { get; set; } = "aetheria.local";

        [Key(3)]
        public string SourceRuntimeId { get; set; } = "";

        [Key(4)]
        public string SourceDaemonId { get; set; } = "";

        [Key(5)]
        public string SessionId { get; set; } = "";

        [Key(6)]
        public long SourceFrameId { get; set; } = -1;

        [Key(7)]
        public string CommandId { get; set; } = "";

        [Key(8)]
        public string SubjectKey { get; set; } = "";

        [Key(9)]
        public string ClaimKind { get; set; } = "";

        [Key(10)]
        public AetheriaRuntimeDaemonCommandKinds CommandKind { get; set; } = AetheriaRuntimeDaemonCommandKinds.None;

        [Key(11)]
        public string Outcome { get; set; } = AetheriaRuntimeCommandFactOutcomes.Applied;

        [Key(12)]
        public string CommittedAtUtc { get; set; } = "";

        [Key(13)]
        public AetheriaRuntimeDaemonCommandDocument Command { get; set; } = new AetheriaRuntimeDaemonCommandDocument();

        public static AetheriaRuntimeCommittedCommandFactDocument FromAppliedCommand(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonCommandDocument command,
            string verseId)
        {
            return FromCommand(frame, command, verseId, AetheriaRuntimeCommandFactOutcomes.Applied);
        }

        public static AetheriaRuntimeCommittedCommandFactDocument FromRejectedCommand(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonCommandDocument command,
            string verseId)
        {
            return FromCommand(frame, command, verseId, AetheriaRuntimeCommandFactOutcomes.Rejected);
        }

        public static string CreateRecordKey(string factId)
        {
            return $"daemon:facts:{StableToken(factId)}:{AetheriaRuntimeDaemonSchemas.CommittedCommandFact}";
        }

        private static AetheriaRuntimeCommittedCommandFactDocument FromCommand(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonCommandDocument command,
            string verseId,
            string outcome)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            command ??= new AetheriaRuntimeDaemonCommandDocument();
            var sourceRuntimeId = string.IsNullOrWhiteSpace(command.AuthorRuntimeId)
                ? command.ClientId ?? ""
                : command.AuthorRuntimeId;
            var subjectKey = AetheriaRuntimeAuthorityRouter.ResolveSubjectKey(command);
            var claimKind = AetheriaRuntimeAuthorityRouter.ResolveClaimKind(command.Kind);
            return new AetheriaRuntimeCommittedCommandFactDocument
            {
                FactId = string.Join(
                    ":",
                    "fact",
                    string.IsNullOrWhiteSpace(frame.DaemonId) ? "daemon" : frame.DaemonId,
                    Math.Max(frame.FrameId, 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    string.IsNullOrWhiteSpace(command.CommandId) ? Guid.NewGuid().ToString("N") : command.CommandId,
                    string.IsNullOrWhiteSpace(outcome) ? AetheriaRuntimeCommandFactOutcomes.Applied : outcome),
                VerseId = string.IsNullOrWhiteSpace(verseId) ? "aetheria.local" : verseId,
                SourceRuntimeId = sourceRuntimeId,
                SourceDaemonId = frame.DaemonId ?? "",
                SessionId = frame.SessionId ?? command.SessionId ?? "",
                SourceFrameId = frame.FrameId,
                CommandId = command.CommandId ?? "",
                SubjectKey = subjectKey,
                ClaimKind = claimKind,
                CommandKind = command.Kind,
                Outcome = string.IsNullOrWhiteSpace(outcome) ? AetheriaRuntimeCommandFactOutcomes.Applied : outcome,
                CommittedAtUtc = DateTime.UtcNow.ToString("O"),
                Command = command
            };
        }

        private static string StableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            var chars = value
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();
            var token = new string(chars).Trim('-').ToLowerInvariant();
            while (token.Contains("--", StringComparison.Ordinal))
                token = token.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(token) ? "empty" : token;
        }
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
