using Aetheria.State;
using GameCult.Aetheria.State.Verse;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

var checks = new AuthoritySmokeChecks();
await checks.RunAsync();
Console.WriteLine("Authority smoke passed.");

internal sealed class AuthoritySmokeChecks
{
    private static readonly object ProcessOutputLock = new();
    private static readonly Dictionary<int, List<string>> ProcessOutput = new();

    public async Task RunAsync()
    {
        DefaultTrustedPolicyAcceptsCommand();
        DelegatedRuntimeAcceptsOnlyListedRuntime();
        HostAuthoritativeAcceptsOnlyHostRuntime();
        InterestLeaseAcceptsOnlyActiveMatchingLease();
        UnsupportedAuthorityModesReject();
        AuthorizedCommandsReportsRejectedIds();
        PreRejectedCommandsEnterFrameReceipts();
        ClientTargetCarriesRuntimeIdentity();
        TwoLocalRuntimeDelegatedPolicyHarness();
        RtsDocumentsBuildLocalViewportFromFrame();
        StarbridgeSessionSummaryProjectsScenarioFacts();
        await AetheriaClientStateDocumentsProjectAndSubmitAsync().ConfigureAwait(false);
        await DaemonOncePublishesStarbridgeSessionFactsAsync().ConfigureAwait(false);
        await SamePolicyDocumentCanBeLoadedByTwoNodesAsync().ConfigureAwait(false);
        await TwoDaemonProcessesApplyDelegatedPolicyAsync().ConfigureAwait(false);
        await ConcurrentDaemonsPublishRemoteFramesToLocalReplicasAsync().ConfigureAwait(false);
        TrustedCommittedFactsConvergeLocalRuns();
    }

    private static void DefaultTrustedPolicyAcceptsCommand()
    {
        var command = Command("raven-unity", "entity:raven");
        var decision = AetheriaRuntimeAuthorityRouter.Authorize(
            command,
            policy: null,
            leases: null,
            localRuntimeId: "aetheria-daemon");

        Require(decision.Authorized, "default trusted policy should accept trusted local commands");
        RequireEqual(AetheriaRuntimeAuthorityModes.AnyTrustedRuntime, decision.Mode, "default policy mode");
        RequireEqual("entity:raven", decision.SubjectKey, "default policy subject");
        RequireEqual(AetheriaRuntimeClaimKinds.Movement, decision.ClaimKind, "default policy claim");
    }

    private static void ClientTargetCarriesRuntimeIdentity()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var gameData = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"aetheria-client-target-{smokeId}"));
        try
        {
            var state = AetheriaState.At(gameData);
            var target = state.ClientTarget.Refresh();
            RequireEqual("raven-unity", target.RuntimeId, "client target default runtime id");

            target = state.ClientTarget.RequestRuntimeId("raven-test-runtime");
            RequireEqual("raven-test-runtime", target.RuntimeId, "client target should persist requested runtime id");

            var boot = AetheriaRuntimeStateBoot.Inspect(gameData);
            RequireEqual("raven-test-runtime", boot.RuntimeId, "boot report should expose client target runtime id");

            var previousRuntimeOverride = Environment.GetEnvironmentVariable(AetheriaRuntimeStateBoundary.RuntimeIdOverrideEnvironmentVariable);
            var previousUnityOverride = Environment.GetEnvironmentVariable(AetheriaRuntimeStateBoundary.UnityRuntimeIdOverrideEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(AetheriaRuntimeStateBoundary.RuntimeIdOverrideEnvironmentVariable, "raven-env-runtime");
                Environment.SetEnvironmentVariable(AetheriaRuntimeStateBoundary.UnityRuntimeIdOverrideEnvironmentVariable, "ignored-unity-runtime");
                boot = AetheriaRuntimeStateBoot.Inspect(gameData);
                RequireEqual("raven-env-runtime", boot.RuntimeId, "generic runtime id override should win");

                Environment.SetEnvironmentVariable(AetheriaRuntimeStateBoundary.RuntimeIdOverrideEnvironmentVariable, "");
                boot = AetheriaRuntimeStateBoot.Inspect(gameData);
                RequireEqual("ignored-unity-runtime", boot.RuntimeId, "Unity runtime id override should apply when generic override is absent");
            }
            finally
            {
                Environment.SetEnvironmentVariable(AetheriaRuntimeStateBoundary.RuntimeIdOverrideEnvironmentVariable, previousRuntimeOverride);
                Environment.SetEnvironmentVariable(AetheriaRuntimeStateBoundary.UnityRuntimeIdOverrideEnvironmentVariable, previousUnityOverride);
            }
        }
        finally
        {
            try
            {
                gameData.Delete(recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void DelegatedRuntimeAcceptsOnlyListedRuntime()
    {
        var policy = Policy(
            AetheriaRuntimeAuthorityModes.AnyTrustedRuntime,
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "raven.movement",
                SubjectPrefix = "entity:raven",
                ClaimKinds = [AetheriaRuntimeClaimKinds.Movement],
                Mode = AetheriaRuntimeAuthorityModes.DelegatedRuntime,
                RuntimeIds = ["raven-unity"],
                Priority = 10
            });

        var accepted = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("raven-unity", "entity:raven"),
            policy,
            leases: null,
            localRuntimeId: "starfire-rts");
        var rejected = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("starfire-rts", "entity:raven"),
            policy,
            leases: null,
            localRuntimeId: "starfire-rts");

        Require(accepted.Authorized, "delegated runtime should accept listed runtime id");
        Require(!rejected.Authorized, "delegated runtime should reject unlisted runtime id");
        RequireEqual("delegated-runtime-required", rejected.Reason, "delegated rejection reason");
    }

    private static void HostAuthoritativeAcceptsOnlyHostRuntime()
    {
        var policy = Policy(AetheriaRuntimeAuthorityModes.HostAuthoritative);

        var accepted = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("host-daemon", "entity:raven"),
            policy,
            leases: null,
            localRuntimeId: "host-daemon");
        var rejected = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("raven-unity", "entity:raven"),
            policy,
            leases: null,
            localRuntimeId: "host-daemon");

        Require(accepted.Authorized, "host-authoritative should accept host runtime");
        Require(!rejected.Authorized, "host-authoritative should reject non-host runtime");
        RequireEqual("host-authority-required", rejected.Reason, "host-authoritative rejection reason");
    }

    private static void InterestLeaseAcceptsOnlyActiveMatchingLease()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = Policy(
            AetheriaRuntimeAuthorityModes.AnyTrustedRuntime,
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "hostile.combat.lease",
                SubjectPrefix = "entity:hostile",
                ClaimKinds = [AetheriaRuntimeClaimKinds.Combat],
                Mode = AetheriaRuntimeAuthorityModes.InterestLease,
                LeaseScope = "combat",
                Priority = 20
            });

        var leases = new[]
        {
            new AetheriaRuntimeAuthorityLeaseDocument
            {
                LeaseId = "lease:starfire:hostile",
                RuntimeId = "starfire-rts",
                SubjectPrefix = "entity:hostile",
                ClaimKinds = [AetheriaRuntimeClaimKinds.Combat],
                Scope = "combat",
                ValidFromUtc = now.AddSeconds(-5).ToString("O"),
                ExpiresAtUtc = now.AddSeconds(30).ToString("O")
            }
        };

        var accepted = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("starfire-rts", "entity:hostile:001", AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup),
            policy,
            leases,
            localRuntimeId: "raven-unity");
        var rejected = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("raven-unity", "entity:hostile:001", AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup),
            policy,
            leases,
            localRuntimeId: "raven-unity");

        Require(accepted.Authorized, "interest lease should accept active matching lease");
        Require(!rejected.Authorized, "interest lease should reject missing runtime lease");
        RequireEqual("authority-lease-required", rejected.Reason, "interest lease rejection reason");
    }

    private static void UnsupportedAuthorityModesReject()
    {
        var policy = Policy(AetheriaRuntimeAuthorityModes.WitnessQuorum);
        var decision = AetheriaRuntimeAuthorityRouter.Authorize(
            Command("raven-unity", "entity:raven"),
            policy,
            leases: null,
            localRuntimeId: "raven-unity");

        Require(!decision.Authorized, "witness quorum should be represented but fail closed");
        RequireEqual("authority-mode-not-implemented", decision.Reason, "unsupported mode rejection reason");
    }

    private static void AuthorizedCommandsReportsRejectedIds()
    {
        var policy = Policy(AetheriaRuntimeAuthorityModes.HostAuthoritative);
        var host = Command("host-daemon", "entity:raven");
        var client = Command("raven-unity", "entity:raven");
        var rejectedIds = new List<string>();

        var accepted = AetheriaRuntimeAuthorityRouter.AuthorizedCommands(
            [host, client],
            policy,
            leases: null,
            localRuntimeId: "host-daemon",
            rejectedCommandIds: rejectedIds);

        RequireEqual(1, accepted.Count, "accepted command count");
        RequireEqual(host.CommandId, accepted[0].CommandId, "accepted command id");
        RequireEqual(1, rejectedIds.Count, "rejected command count");
        RequireEqual(client.CommandId, rejectedIds[0], "rejected command id");
    }

    private static void PreRejectedCommandsEnterFrameReceipts()
    {
        var command = Command("raven-unity", "entity:raven");
        var result = AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-authority-smoke.cc"),
            new AetheriaRuntimeRunCheckpointCommit(),
            new AetheriaRuntimeDaemonTickOptions
            {
                DaemonId = "host-daemon",
                SessionId = "authority-smoke",
                FrameId = 42,
                SimulationTimeSeconds = 1,
                FixedDeltaSeconds = 0.02,
                ObservedCommands = Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                PreRejectedCommandIds = [command.CommandId],
                BuildPublications = false
            });

        Require(result.Frame.RejectedCommandIds.Contains(command.CommandId), "pre-rejected command should enter frame rejection receipts");
        Require(result.Frame.AccountedCommandIds.Contains(command.CommandId), "pre-rejected command should enter accounted receipts");
    }

    private static void TwoLocalRuntimeDelegatedPolicyHarness()
    {
        const string ravenRuntime = "raven-unity";
        const string starfireRuntime = "starfire-rts";
        var ravenKey = EntityKey("coop-smoke", 0, 0);
        var hostileKey = EntityKey("coop-smoke", 0, 1);
        var policy = CoopPolicy();

        var ravenByRaven = MovementCommand(ravenRuntime, ravenKey, directionX: 1, directionY: 0, magnitude: 1);
        var hostileByRaven = MovementCommand(ravenRuntime, hostileKey, directionX: 0, directionY: 1, magnitude: 1);
        var ravenFrame = TickWithPolicy(
            "raven-local",
            InitialCoopRun(),
            policy,
            [ravenByRaven, hostileByRaven]);

        Require(ravenFrame.AppliedCommandIds.Contains(ravenByRaven.CommandId), "raven node should apply Raven-authored Raven movement");
        Require(ravenFrame.RejectedCommandIds.Contains(hostileByRaven.CommandId), "raven node should reject Raven-authored hostile movement");
        Require(ravenFrame.AccountedCommandIds.Contains(ravenByRaven.CommandId), "raven node should account applied Raven command");
        Require(ravenFrame.AccountedCommandIds.Contains(hostileByRaven.CommandId), "raven node should account rejected hostile command");

        var ravenByStarfire = MovementCommand(starfireRuntime, ravenKey, directionX: -1, directionY: 0, magnitude: 1);
        var hostileByStarfire = MovementCommand(starfireRuntime, hostileKey, directionX: 0, directionY: -1, magnitude: 1);
        var starfireFrame = TickWithPolicy(
            "starfire-local",
            InitialCoopRun(),
            policy,
            [ravenByStarfire, hostileByStarfire]);

        Require(starfireFrame.RejectedCommandIds.Contains(ravenByStarfire.CommandId), "starfire node should reject Starfire-authored Raven movement");
        Require(starfireFrame.AppliedCommandIds.Contains(hostileByStarfire.CommandId), "starfire node should apply Starfire-authored hostile movement");
        Require(starfireFrame.AccountedCommandIds.Contains(ravenByStarfire.CommandId), "starfire node should account rejected Raven command");
        Require(starfireFrame.AccountedCommandIds.Contains(hostileByStarfire.CommandId), "starfire node should account applied hostile command");
    }

    private static void RtsDocumentsBuildLocalViewportFromFrame()
    {
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "rts-documents-smoke",
            CurrentZoneIndex = 0,
            EntranceZoneIndex = 0,
            ExitZoneIndex = 1,
            DiscoveredZoneIndices = [0],
            CurrentEntityKey = EntityKey("rts-documents-smoke", 0, 0),
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Name = "Document Zone",
                    PositionX = 12,
                    PositionY = 34,
                    AdjacentZoneIndices = [1],
                    Entities =
                    [
                        SnapshotEntity(
                            0,
                            "Raven",
                            "player",
                            x: 0,
                            z: 0,
                            visibility: 80,
                            hull: 120,
                            cargoItem: "reactor-cell"),
                        SnapshotEntity(
                            1,
                            "Starfire",
                            "player",
                            x: 240,
                            z: 0,
                            visibility: 160,
                            hull: 140,
                            cargoItem: "sensor-pack"),
                        SnapshotEntity(
                            2,
                            "Raider In Shared Sight",
                            "raider",
                            x: 360,
                            z: 0,
                            visibility: 60,
                            hull: 70,
                            cargoItem: "scrap"),
                        SnapshotEntity(
                            3,
                            "Outside Viewport",
                            "raider",
                            x: 900,
                            z: 0,
                            visibility: 60,
                            hull: 70,
                            cargoItem: "scrap")
                    ],
                    Bodies =
                    [
                        new AetheriaRuntimeBodySnapshotCommit
                        {
                            BodyKey = "body:near",
                            OrbitKey = "orbit:near",
                            Name = "Gravity Brush",
                            Kind = "planet",
                            GravityInfluenceCenterX = 390,
                            GravityInfluenceCenterZ = 0,
                            GravityInfluenceRadius = 75,
                            GravityWellDepth = 3,
                            GravityDepthExponent = 2,
                            GravityWaveRadius = 32,
                            GravityWaveDepth = 0.5,
                            GravityWaveSpeed = 1
                        },
                        new AetheriaRuntimeBodySnapshotCommit
                        {
                            BodyKey = "body:far",
                            OrbitKey = "orbit:far",
                            Name = "Too Far",
                            Kind = "planet",
                            GravityInfluenceCenterX = 1200,
                            GravityInfluenceCenterZ = 0,
                            GravityInfluenceRadius = 50
                        }
                    ]
                },
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 1,
                    Name = "Document Neighbor",
                    PositionX = 72,
                    PositionY = 34,
                    AdjacentZoneIndices = [0],
                    OwnerFactionIndex = 2,
                    FactionIndices = [2]
                }
            ]
        };
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            run,
            "documents-daemon",
            "documents-session",
            12,
            0.24,
            0.02);

        var viewportDocument = AetheriaRuntimeGameDocuments.Viewport(
            frame,
            new AetheriaRuntimeViewportBounds
            {
                MinX = 500,
                MinY = -100,
                MaxX = -50,
                MaxY = 100
            });
        var objectsViewport = AetheriaRuntimeGameDocuments.ObjectsViewport(
            frame,
            viewportDocument.Viewport);
        var gravityViewport = AetheriaRuntimeGameDocuments.GravityViewport(
            frame,
            viewportDocument.Viewport);
        var currentZone = AetheriaRuntimeGameDocuments.CurrentZone(frame);
        var sectorMap = AetheriaRuntimeGameDocuments.SectorMap(frame);

        RequireEqual(AetheriaRuntimeDaemonSchemas.GameViewport, viewportDocument.Schema, "viewport document schema");
        RequireEqual(AetheriaRuntimeDaemonSchemas.ObjectsViewport, objectsViewport.Schema, "objects viewport document schema");
        RequireEqual(AetheriaRuntimeDaemonSchemas.GravityViewport, gravityViewport.Schema, "gravity viewport document schema");
        RequireEqual(AetheriaRuntimeDaemonSchemas.CurrentZone, currentZone.Schema, "current-zone document schema");
        RequireEqual(AetheriaRuntimeDaemonSchemas.SectorMap, sectorMap.Schema, "sector-map document schema");
        RequireEqual("Document Zone", currentZone.ZoneName, "current-zone document zone name");
        RequireEqual(12.0, currentZone.PositionX, "current-zone document position x");
        RequireEqual(34.0, currentZone.PositionY, "current-zone document position y");
        RequireEqual(2, sectorMap.Zones.Count, "sector-map document zone count");
        Require(sectorMap.Zones.Any(zone => zone.ZoneIndex == 0 && zone.Current && zone.Entrance && zone.Discovered), "sector-map should mark current entrance zone");
        Require(sectorMap.Zones.Any(zone => zone.ZoneIndex == 1 && zone.Exit && !zone.Discovered), "sector-map should mark undiscovered exit zone");
        Require(sectorMap.Links.Any(link => link.FromZoneIndex == 0 && link.ToZoneIndex == 1 && !link.Discovered), "sector-map should include normalized topology link");
        RequireEqual(12L, viewportDocument.FrameId, "viewport document frame id");
        RequireEqual(-50.0, viewportDocument.Viewport.MinX, "normalized viewport min x");
        RequireEqual(500.0, viewportDocument.Viewport.MaxX, "normalized viewport max x");
        Require(viewportDocument.ControlledEntityIndices.SequenceEqual([0, 1]), "viewport document should expose controlled unit set");
        Require(viewportDocument.Objects.Any(item => item.DisplayName == "Raider In Shared Sight"), "viewport document should use union of controlled unit visibility");
        Require(!viewportDocument.Objects.Any(item => item.DisplayName == "Outside Viewport"), "viewport document should exclude objects outside viewport");
        Require(objectsViewport.Objects.Select(item => item.EntityIndex).SequenceEqual(viewportDocument.Objects.Select(item => item.EntityIndex)), "objects viewport should match composed map object set");
        var raven = viewportDocument.Objects.First(item => item.DisplayName == "Raven");
        RequireEqual(120.0, raven.Status.Hull, "viewport document should include entity status");
        Require(raven.Inventory.Any(item => item.ItemKey == "reactor-cell" && item.Source == "cargo"), "viewport document should include inventory");
        Require(viewportDocument.GravityInfluences.Any(item => item.BodyKey == "body:near"), "viewport document should include intersecting gravity influence");
        Require(!viewportDocument.GravityInfluences.Any(item => item.BodyKey == "body:far"), "viewport document should exclude non-intersecting gravity influence");
        Require(gravityViewport.GravityInfluences.Select(item => item.BodyKey).SequenceEqual(viewportDocument.GravityInfluences.Select(item => item.BodyKey)), "gravity viewport should match composed map gravity set");
    }

    private static void StarbridgeSessionSummaryProjectsScenarioFacts()
    {
        var baseEntity = SnapshotEntity(
            4,
            "Starbridge Base",
            "player",
            x: 0,
            z: 0,
            visibility: 500,
            hull: 850,
            cargoItem: "coolant-beam");
        baseEntity.Kind = "station";

        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "starbridge-session-smoke",
            CurrentZoneIndex = 0,
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Name = "Starbridge Defense",
                    Entities =
                    [
                        baseEntity,
                        SnapshotEntity(5, "Pilot Raven", "player", 120, 0, 300, 140, "repair-pod")
                    ]
                }
            ]
        };
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            run,
            "starbridge-daemon",
            "starbridge-smoke-session",
            19,
            0.38,
            0.02);
        var scenario = new AetheriaRuntimeStarbridgeScenarioDocument
        {
            ScenarioId = "starbridge.frontier-fabricator",
            DisplayName = "Frontier Fabricator Defense",
            StartingBaseKey = EntityKey("starbridge-session-smoke", 0, 4),
            StationStock =
            [
                new AetheriaRuntimeStarbridgeStationStockItem
                {
                    ItemKey = "coolant-beam",
                    Quantity = 2,
                    Quality = 0.75,
                    Durability = 1
                }
            ],
            Waves =
            [
                new AetheriaRuntimeStarbridgeWaveDefinition
                {
                    WaveIndex = 0,
                    DisplayName = "Scout Probe",
                    AttackerKeys = ["scout"],
                    BossKey = "scout-captain",
                    RecoveredTechnologyKeys = ["sensor-calibration"]
                },
                new AetheriaRuntimeStarbridgeWaveDefinition
                {
                    WaveIndex = 1,
                    DisplayName = "Bomber Line",
                    AttackerKeys = ["bomber", "skirmisher"],
                    BossKey = "bomber-frame",
                    RecoveredTechnologyKeys = ["missile-rack-burst"]
                }
            ],
            RuntimeRoles =
            [
                new AetheriaRuntimeStarbridgeRuntimeRole
                {
                    RuntimeId = "starfire-rts",
                    Role = "commander"
                },
                new AetheriaRuntimeStarbridgeRuntimeRole
                {
                    RuntimeId = "raven-unity",
                    Role = "pilot",
                    EntityKey = EntityKey("starbridge-session-smoke", 0, 5)
                }
            ]
        };
        var session = new AetheriaRuntimeStarbridgeSessionDocument
        {
            SessionId = "starbridge-smoke-session",
            ScenarioId = scenario.ScenarioId,
            RunId = run.RunId,
            BaseEntityKey = EntityKey("starbridge-session-smoke", 0, 4),
            StationEntityKey = EntityKey("starbridge-session-smoke", 0, 4),
            Phase = "pre-wave",
            CurrentWaveIndex = 1
        };

        var summary = AetheriaRuntimeStarbridgeDocuments.SessionSummary(frame, scenario, session);

        RequireEqual(AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary, summary.Schema, "starbridge session summary schema");
        RequireEqual("starbridge.frontier-fabricator", summary.ScenarioId, "starbridge scenario id");
        RequireEqual("Frontier Fabricator Defense", summary.ScenarioName, "starbridge scenario name");
        RequireEqual("pre-wave", summary.Phase, "starbridge session phase");
        RequireEqual("Starbridge Base", summary.BaseStatus.DisplayName, "starbridge base status name");
        RequireEqual(850.0, summary.BaseStatus.Hull, "starbridge base hull");
        Require(summary.StationStock.Any(item => item.ItemKey == "coolant-beam"), "starbridge station stock should project scenario stock");
        Require(summary.WaveForecast.Length == 1 && summary.WaveForecast[0].DisplayName == "Bomber Line", "starbridge wave forecast should start at current wave");
        Require(summary.RuntimeRoles.Any(role => role.RuntimeId == "starfire-rts" && role.Role == "commander"), "starbridge runtime roles should project scenario roles");

        var gameSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            frame,
            new AetheriaRuntimeDaemonHealthDocument
            {
                DaemonId = "starbridge-daemon",
                VerseId = "aetheria.starbridge-smoke",
                Status = "healthy"
            },
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("starbridge-daemon"),
            summary);
        RequireSurfaceMetric(gameSurface, "aetheria.daemon.game.starbridge.scenario", "Frontier Fabricator Defense", "daemon game surface starbridge scenario");
        RequireSurfaceMetric(gameSurface, "aetheria.daemon.game.starbridge.base", "Starbridge Base", "daemon game surface starbridge base");
        RequireSurfaceMetric(gameSurface, "aetheria.daemon.game.starbridge.stock.0.item", "coolant-beam", "daemon game surface starbridge stock");
        RequireSurfaceMetric(gameSurface, "aetheria.daemon.game.starbridge.wave.0.name", "Bomber Line", "daemon game surface starbridge wave forecast");
        RequireSurfaceMetric(gameSurface, "aetheria.daemon.game.starbridge.role.0.runtime", "starfire-rts", "daemon game surface starbridge runtime role");
    }

    private static async Task AetheriaClientStateDocumentsProjectAndSubmitAsync()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var statePath = Path.Combine(Path.GetTempPath(), $"aetheria-client-state-documents-{smokeId}.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "client-state-documents-smoke",
                CurrentZoneIndex = 0,
                CurrentEntityKey = EntityKey("client-state-documents-smoke", 0, 0),
                Zones =
                [
                    new AetheriaRuntimeZoneSnapshotCommit
                    {
                        ZoneIndex = 0,
                        Name = "Client State Documents Zone",
                        Entities =
                        [
                            SnapshotEntity(0, "Document Raven", "player", 0, 0, 600, 140, "document-cargo"),
                            SnapshotEntity(1, "Document Raider", "raider", 100, 0, 120, 80, "raider-cargo")
                        ]
                    }
                ]
            },
            "state-documents-daemon",
            "state-documents-session",
            7,
            0.14,
            0.02);
        var health = new AetheriaRuntimeDaemonHealthDocument
        {
            DaemonId = "state-documents-daemon",
            VerseId = "aetheria.state-documents-smoke",
            FrameId = frame.FrameId,
            Status = "healthy",
            Transport = "cultcache-witness"
        };
        var policy = AetheriaRuntimeVerseAuthorityPolicyDocument.TrustedCoop(
            "aetheria.state-documents-smoke",
            "state-documents-daemon");
        var scenario = new AetheriaRuntimeStarbridgeScenarioDocument
        {
            ScenarioId = "starbridge.state-documents",
            DisplayName = "Document Starbridge",
            StartingBaseKey = EntityKey("client-state-documents-smoke", 0, 0),
            StationStock =
            [
                new AetheriaRuntimeStarbridgeStationStockItem
                {
                    ItemKey = "document-cargo",
                    Quantity = 3,
                    Quality = 1,
                    Durability = 1
                }
            ],
            Waves =
            [
                new AetheriaRuntimeStarbridgeWaveDefinition
                {
                    WaveIndex = 0,
                    DisplayName = "Document Wave",
                    AttackerKeys = ["raider"],
                    BossKey = "document-boss"
                }
            ]
        };
        var session = new AetheriaRuntimeStarbridgeSessionDocument
        {
            SessionId = "state-documents-session",
            ScenarioId = "starbridge.state-documents",
            BaseEntityKey = EntityKey("client-state-documents-smoke", 0, 0),
            Phase = "pre-wave"
        };

        using (var writer = await AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "state-documents-writer", startServer: false, pullOnOpen: false)
            .ConfigureAwait(false))
        {
            await writer.Database
                .PutAsync(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest, frame)
                .ConfigureAwait(false);
            await writer.Database
                .PutAsync(AetheriaRuntimeVerseRecordKeys.DaemonHealth, health)
                .ConfigureAwait(false);
            await writer.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)
                .ReplaceAsync(policy)
                .ConfigureAwait(false);
            await writer.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest)
                .ReplaceAsync(scenario)
                .ConfigureAwait(false);
            await writer.MutableDocument<AetheriaRuntimeStarbridgeSessionDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest)
                .ReplaceAsync(session)
                .ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        using var client = await AetheriaClient
            .OpenAsync(statePath, "raven-unity", sessionId: "state-documents-session", pullOnOpen: true)
            .ConfigureAwait(false);
        var state = client.State;
        var viewportBounds = new AetheriaRuntimeViewportBounds { MinX = -20, MinY = -20, MaxX = 150, MaxY = 20 };
        var viewport = await state
            .GameViewport(viewportBounds)
            .LatestAsync()
            .ConfigureAwait(false);
        Require(viewport.Objects.Any(item => item.DisplayName == "Document Raven"), "managed map document should include controlled object");
        Require(viewport.Objects.Any(item => item.DisplayName == "Document Raider"), "managed map document should include visible hostile object");
        var objectsViewport = await state
            .ObjectsViewport(viewportBounds)
            .LatestAsync()
            .ConfigureAwait(false);
        RequireEqual(AetheriaRuntimeDaemonSchemas.ObjectsViewport, objectsViewport.Schema, "managed objects viewport document schema");
        Require(objectsViewport.Objects.Any(item => item.DisplayName == "Document Raven"), "managed objects viewport document should include controlled object");
        Require(objectsViewport.Objects.Any(item => item.DisplayName == "Document Raider"), "managed objects viewport document should include visible hostile object");
        var gravityViewport = await state
            .GravityViewport(viewportBounds)
            .LatestAsync()
            .ConfigureAwait(false);
        RequireEqual(AetheriaRuntimeDaemonSchemas.GravityViewport, gravityViewport.Schema, "managed gravity viewport document schema");

        var currentZone = await state.CurrentZone.LatestAsync().ConfigureAwait(false);
        RequireEqual(AetheriaRuntimeDaemonSchemas.CurrentZone, currentZone.Schema, "managed current-zone document schema");
        RequireEqual("Client State Documents Zone", currentZone.ZoneName, "managed current-zone document name");

        var sectorMap = await state.SectorMap.LatestAsync().ConfigureAwait(false);
        RequireEqual(AetheriaRuntimeDaemonSchemas.SectorMap, sectorMap.Schema, "managed sector-map document schema");
        Require(sectorMap.Zones.Any(zone => zone.ZoneIndex == currentZone.ZoneIndex && zone.Current), "managed sector-map document should include current zone marker");

        var selected = await state.SelectedObject(0).LatestAsync().ConfigureAwait(false);
        RequireEqual(AetheriaRuntimeDaemonSchemas.SelectedObject, selected.Schema, "managed selected-object document schema");
        Require(selected.Selected?.DisplayName == "Document Raven", "managed selected-object document should resolve entity");

        var inventory = await state.Inventory(0).LatestAsync().ConfigureAwait(false);
        RequireEqual(AetheriaRuntimeDaemonSchemas.Inventory, inventory.Schema, "managed inventory document schema");
        Require(inventory.Cargo.Any(item => item.ItemKey == "document-cargo"), "managed inventory document should expose cargo");

        var starbridgeSummary = await state.StarbridgeSummary.LatestAsync().ConfigureAwait(false);
        RequireEqual("Document Starbridge", starbridgeSummary.ScenarioName, "managed starbridge summary document should project scenario name");
        RequireEqual("pre-wave", starbridgeSummary.Phase, "managed starbridge summary document should project session phase");
        Require(starbridgeSummary.StationStock.Any(item => item.ItemKey == "document-cargo"), "managed starbridge summary document should expose station stock");

        var healthDocument = await state.Health.LatestAsync().ConfigureAwait(false);
        Require(healthDocument?.Status == "healthy", "managed daemon health document should read local health");

        var authorityPolicy = await state.AuthorityPolicy.LatestAsync().ConfigureAwait(false);
        RequireEqual("aetheria.trusted-coop.v1", authorityPolicy?.PolicyId, "managed authority policy document id");

        var command = client.SetMoveVector(1, 0, 0.5);
        RequireEqual(AetheriaRuntimeDaemonCommandKinds.SetMoveVector, command.Kind, "managed client command kind");
        RequireEqual("raven-unity", command.ClientId, "managed client command client id");
        RequireEqual(frame.FrameId, command.ObservedFrameId, "managed client command observed frame id");

        using var reader = await AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "state-documents-reader", startServer: false, pullOnOpen: true)
            .ConfigureAwait(false);
        var stored = await reader.Database
            .GetAsync<AetheriaRuntimeDaemonCommandDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommand(command.CommandId))
            .ConfigureAwait(false);
        Require(stored != null, "managed client command should be stored as typed daemon command document");
        RequireEqual(AetheriaRuntimeClaimKinds.Movement, stored!.ClaimKind, "managed client command claim kind");
    }

    private static async Task DaemonOncePublishesStarbridgeSessionFactsAsync()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var statePath = Path.Combine(Path.GetTempPath(), $"aetheria-starbridge-daemon-{smokeId}.cc");
        var clientCultMeshPort = GetFreeUdpPort();
        const string runtimeId = "starbridge-daemon-smoke";

        await RunDaemonOnceAsync(statePath, runtimeId, clientCultMeshPort).ConfigureAwait(false);

        using var client = await AetheriaClient
            .OpenAsync(statePath, "starbridge-smoke-client", sessionId: "authority-smoke", pullOnOpen: true)
            .ConfigureAwait(false);
        var summary = await client.State.StarbridgeSummary.LatestAsync().ConfigureAwait(false);

        RequireEqual(AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary, summary.Schema, "daemon starbridge summary schema");
        RequireEqual("starbridge.frontier-fabricator", summary.ScenarioId, "daemon starbridge scenario id");
        RequireEqual("Frontier Fabricator Defense", summary.ScenarioName, "daemon starbridge scenario name");
        RequireEqual("authority-smoke", summary.SessionId, "daemon starbridge session id");
        RequireEqual("pre-wave", summary.Phase, "daemon starbridge phase");
        RequireEqual("Anchor Station", summary.BaseStatus.DisplayName, "daemon starbridge base name");
        Require(summary.StationStock.Any(item => item.ItemKey == "repair-parts"), "daemon starbridge summary should expose station stock");
        Require(summary.WaveForecast.Any(wave => wave.DisplayName == "Scout Probe"), "daemon starbridge summary should expose wave forecast");
        Require(summary.RuntimeRoles.Any(role => role.RuntimeId == "starfire-rts" && role.Role == "commander"), "daemon starbridge summary should expose commander role");
        Require(summary.RuntimeRoles.Any(role => role.RuntimeId == "raven-unity" && role.Role == "pilot"), "daemon starbridge summary should expose pilot role");
    }

    private static async Task SamePolicyDocumentCanBeLoadedByTwoNodesAsync()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var ravenPath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-raven-{smokeId}.cc");
        var starfirePath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-starfire-{smokeId}.cc");
        var policy = CoopPolicy();

        await using var ravenNode = await AetheriaStateNode.OpenAsync(
            ravenPath,
            runtimeId: "raven-local",
            startServer: false,
            enableDurableShardLogs: false).ConfigureAwait(false);
        await using var starfireNode = await AetheriaStateNode.OpenAsync(
            starfirePath,
            runtimeId: "starfire-local",
            startServer: false,
            enableDurableShardLogs: false).ConfigureAwait(false);

        await ravenNode.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)
            .ReplaceAsync(policy)
            .ConfigureAwait(false);
        await starfireNode.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)
            .ReplaceAsync(policy)
            .ConfigureAwait(false);

        var ravenPolicy = await ravenNode.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy).ReadAsync().ConfigureAwait(false);
        var starfirePolicy = await starfireNode.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy).ReadAsync().ConfigureAwait(false);

        Require(ravenPolicy != null, "raven node should load authority policy");
        Require(starfirePolicy != null, "starfire node should load authority policy");
        RequireEqual(policy.PolicyId, ravenPolicy!.PolicyId, "raven loaded policy id");
        RequireEqual(policy.PolicyId, starfirePolicy!.PolicyId, "starfire loaded policy id");
        RequireEqual(policy.Rules.Length, ravenPolicy.Rules.Length, "raven loaded rule count");
        RequireEqual(policy.Rules.Length, starfirePolicy.Rules.Length, "starfire loaded rule count");
    }

    private static async Task TwoDaemonProcessesApplyDelegatedPolicyAsync()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var policy = CoopPolicy();
        var ravenKey = EntityKey("coop-smoke", 0, 0);
        var hostileKey = EntityKey("coop-smoke", 0, 1);

        var ravenByRaven = MovementCommand("raven-unity", ravenKey, directionX: 1, directionY: 0, magnitude: 1);
        var hostileByRaven = MovementCommand("raven-unity", hostileKey, directionX: 0, directionY: 1, magnitude: 1);
        var ravenStatePath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-raven-daemon-{smokeId}.cc");
        await SeedDaemonStateAsync(
            ravenStatePath,
            "raven-local",
            policy,
            [ravenByRaven, hostileByRaven]).ConfigureAwait(false);
        await RunDaemonOnceAsync(ravenStatePath, "raven-local", clientCultMeshPort: 41076).ConfigureAwait(false);
        var ravenFrame = await ReadPublishedFrameAsync(ravenStatePath, "raven-local").ConfigureAwait(false);

        Require(ravenFrame.AppliedCommandIds.Contains(ravenByRaven.CommandId), "raven daemon should apply Raven-authored Raven movement");
        Require(ravenFrame.RejectedCommandIds.Contains(hostileByRaven.CommandId), "raven daemon should reject Raven-authored hostile movement");
        Require(ravenFrame.AccountedCommandIds.Contains(ravenByRaven.CommandId), "raven daemon should account applied command");
        Require(ravenFrame.AccountedCommandIds.Contains(hostileByRaven.CommandId), "raven daemon should account rejected command");

        var ravenByStarfire = MovementCommand("starfire-rts", ravenKey, directionX: -1, directionY: 0, magnitude: 1);
        var hostileByStarfire = MovementCommand("starfire-rts", hostileKey, directionX: 0, directionY: -1, magnitude: 1);
        var starfireStatePath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-starfire-daemon-{smokeId}.cc");
        await SeedDaemonStateAsync(
            starfireStatePath,
            "starfire-local",
            policy,
            [ravenByStarfire, hostileByStarfire]).ConfigureAwait(false);
        await RunDaemonOnceAsync(starfireStatePath, "starfire-local", clientCultMeshPort: 41077).ConfigureAwait(false);
        var starfireFrame = await ReadPublishedFrameAsync(starfireStatePath, "starfire-local").ConfigureAwait(false);

        Require(starfireFrame.RejectedCommandIds.Contains(ravenByStarfire.CommandId), "starfire daemon should reject Starfire-authored Raven movement");
        Require(starfireFrame.AppliedCommandIds.Contains(hostileByStarfire.CommandId), "starfire daemon should apply Starfire-authored hostile movement");
        Require(starfireFrame.AccountedCommandIds.Contains(ravenByStarfire.CommandId), "starfire daemon should account rejected command");
        Require(starfireFrame.AccountedCommandIds.Contains(hostileByStarfire.CommandId), "starfire daemon should account applied command");
    }

    private static async Task ConcurrentDaemonsPublishRemoteFramesToLocalReplicasAsync()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var policy = CoopPolicy();
        var ravenKey = EntityKey("coop-smoke", 0, 0);
        var hostileKey = EntityKey("coop-smoke", 0, 1);
        var ravenStatePath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-raven-live-{smokeId}.cc");
        var starfireStatePath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-starfire-live-{smokeId}.cc");
        var ravenReplicaPath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-raven-replica-{smokeId}.cc");
        var starfireReplicaPath = Path.Combine(Path.GetTempPath(), $"aetheria-authority-starfire-replica-{smokeId}.cc");
        var ravenPort = GetFreeUdpPort();
        var starfirePort = GetFreeUdpPort();

        var ravenByRaven = MovementCommand("raven-unity", ravenKey, directionX: 1, directionY: 0, magnitude: 1);
        var hostileByRaven = MovementCommand("raven-unity", hostileKey, directionX: 0, directionY: 1, magnitude: 1);
        await SeedDaemonStateAsync(
            ravenStatePath,
            "raven-local",
            policy,
            [ravenByRaven, hostileByRaven]).ConfigureAwait(false);

        var ravenByStarfire = MovementCommand("starfire-rts", ravenKey, directionX: -1, directionY: 0, magnitude: 1);
        var hostileByStarfire = MovementCommand("starfire-rts", hostileKey, directionX: 0, directionY: -1, magnitude: 1);
        await SeedDaemonStateAsync(
            starfireStatePath,
            "starfire-local",
            policy,
            [ravenByStarfire, hostileByStarfire]).ConfigureAwait(false);

        using var ravenDaemon = StartDaemonProcess(
            ravenStatePath,
            "raven-local",
            ravenPort,
            once: false,
            peerEndpoints: [$"rudp://127.0.0.1:{starfirePort}"]);
        using var starfireDaemon = StartDaemonProcess(
            starfireStatePath,
            "starfire-local",
            starfirePort,
            once: false,
            peerEndpoints: [$"rudp://127.0.0.1:{ravenPort}"]);
        try
        {
            var ravenRemoteFrame = await WaitForReplicaFrameAsync(
                $"rudp://127.0.0.1:{ravenPort}",
                starfireReplicaPath,
                "starfire-observes-raven",
                [ravenDaemon, starfireDaemon],
                frame =>
                    string.Equals(frame.DaemonId, "raven-local", StringComparison.Ordinal) &&
                    frame.CumulativeAppliedCommandIds.Contains(ravenByRaven.CommandId) &&
                    frame.CumulativeRejectedCommandIds.Contains(hostileByRaven.CommandId),
                "Raven frame with Raven movement applied and hostile movement rejected").ConfigureAwait(false);
            var starfireRemoteFrame = await WaitForReplicaFrameAsync(
                $"rudp://127.0.0.1:{starfirePort}",
                ravenReplicaPath,
                "raven-observes-starfire",
                [ravenDaemon, starfireDaemon],
                frame =>
                    string.Equals(frame.DaemonId, "starfire-local", StringComparison.Ordinal) &&
                    frame.CumulativeAppliedCommandIds.Contains(hostileByStarfire.CommandId) &&
                    frame.CumulativeRejectedCommandIds.Contains(ravenByStarfire.CommandId),
                "Starfire frame with hostile movement applied and Raven movement rejected").ConfigureAwait(false);

            RequireEqual("raven-local", ravenRemoteFrame.DaemonId, "Starfire local replica should observe Raven daemon id");
            Require(ravenRemoteFrame.CumulativeAppliedCommandIds.Contains(ravenByRaven.CommandId), "Starfire local replica should observe Raven-applied Raven command");
            Require(ravenRemoteFrame.CumulativeRejectedCommandIds.Contains(hostileByRaven.CommandId), "Starfire local replica should observe Raven rejection receipt");
            var starfireObservedRavenFacts = await WaitForReplicaFactsAsync(
                $"rudp://127.0.0.1:{ravenPort}",
                starfireReplicaPath,
                "starfire-observes-raven-facts",
                [ravenDaemon, starfireDaemon],
                facts =>
                    facts.Any(fact =>
                        string.Equals(fact.SourceDaemonId, "raven-local", StringComparison.Ordinal) &&
                        string.Equals(fact.CommandId, ravenByRaven.CommandId, StringComparison.Ordinal) &&
                        string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal)) &&
                    facts.Any(fact =>
                        string.Equals(fact.SourceDaemonId, "raven-local", StringComparison.Ordinal) &&
                        string.Equals(fact.CommandId, hostileByRaven.CommandId, StringComparison.Ordinal) &&
                        string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Rejected, StringComparison.Ordinal)),
                "Raven applied and rejected committed facts").ConfigureAwait(false);
            Require(
                starfireObservedRavenFacts.Any(fact =>
                    string.Equals(fact.SourceDaemonId, "raven-local", StringComparison.Ordinal) &&
                    string.Equals(fact.CommandId, ravenByRaven.CommandId, StringComparison.Ordinal) &&
                    string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal)),
                "Starfire local replica should observe Raven committed movement fact");
            Require(
                starfireObservedRavenFacts.Any(fact =>
                    string.Equals(fact.SourceDaemonId, "raven-local", StringComparison.Ordinal) &&
                    string.Equals(fact.CommandId, hostileByRaven.CommandId, StringComparison.Ordinal) &&
                    string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Rejected, StringComparison.Ordinal)),
                "Starfire local replica should observe Raven policy rejection fact");

            RequireEqual("starfire-local", starfireRemoteFrame.DaemonId, "Raven local replica should observe Starfire daemon id");
            Require(starfireRemoteFrame.CumulativeAppliedCommandIds.Contains(hostileByStarfire.CommandId), "Raven local replica should observe Starfire-applied hostile command");
            Require(starfireRemoteFrame.CumulativeRejectedCommandIds.Contains(ravenByStarfire.CommandId), "Raven local replica should observe Starfire rejection receipt");
            var ravenObservedStarfireFacts = await WaitForReplicaFactsAsync(
                $"rudp://127.0.0.1:{starfirePort}",
                ravenReplicaPath,
                "raven-observes-starfire-facts",
                [ravenDaemon, starfireDaemon],
                facts =>
                    facts.Any(fact =>
                        string.Equals(fact.SourceDaemonId, "starfire-local", StringComparison.Ordinal) &&
                        string.Equals(fact.CommandId, hostileByStarfire.CommandId, StringComparison.Ordinal) &&
                        string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal)) &&
                    facts.Any(fact =>
                        string.Equals(fact.SourceDaemonId, "starfire-local", StringComparison.Ordinal) &&
                        string.Equals(fact.CommandId, ravenByStarfire.CommandId, StringComparison.Ordinal) &&
                        string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Rejected, StringComparison.Ordinal)),
                "Starfire applied and rejected committed facts").ConfigureAwait(false);
            Require(
                ravenObservedStarfireFacts.Any(fact =>
                    string.Equals(fact.SourceDaemonId, "starfire-local", StringComparison.Ordinal) &&
                    string.Equals(fact.CommandId, hostileByStarfire.CommandId, StringComparison.Ordinal) &&
                    string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal)),
                "Raven local replica should observe Starfire committed hostile fact");
            Require(
                ravenObservedStarfireFacts.Any(fact =>
                    string.Equals(fact.SourceDaemonId, "starfire-local", StringComparison.Ordinal) &&
                    string.Equals(fact.CommandId, ravenByStarfire.CommandId, StringComparison.Ordinal) &&
                    string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Rejected, StringComparison.Ordinal)),
                "Raven local replica should observe Starfire policy rejection fact");

            var ravenMovementFactId = starfireObservedRavenFacts.First(fact =>
                string.Equals(fact.SourceDaemonId, "raven-local", StringComparison.Ordinal) &&
                string.Equals(fact.CommandId, ravenByRaven.CommandId, StringComparison.Ordinal) &&
                string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal)).FactId;
            var starfireHostileFactId = ravenObservedStarfireFacts.First(fact =>
                string.Equals(fact.SourceDaemonId, "starfire-local", StringComparison.Ordinal) &&
                string.Equals(fact.CommandId, hostileByStarfire.CommandId, StringComparison.Ordinal) &&
                string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal)).FactId;

            var starfireConvergedFrame = await WaitForLocalFrameAsync(
                starfireStatePath,
                "starfire-local-converges-raven",
                [ravenDaemon, starfireDaemon],
                frame =>
                    frame.CumulativeImportedFactIds.Contains(ravenMovementFactId) &&
                    Entity(frame.Run, ravenKey).PositionX > 0,
                "Starfire live daemon imports Raven movement fact and moves Raven locally").ConfigureAwait(false);
            Require(
                starfireConvergedFrame.CumulativeImportedFactIds.Any(),
                "Starfire live daemon should record imported remote fact ids");

            var ravenConvergedFrame = await WaitForLocalFrameAsync(
                ravenStatePath,
                "raven-local-converges-starfire",
                [ravenDaemon, starfireDaemon],
                frame =>
                    frame.CumulativeImportedFactIds.Contains(starfireHostileFactId) &&
                    Entity(frame.Run, hostileKey).PositionZ < 0,
                "Raven live daemon imports Starfire hostile movement fact and moves hostile locally").ConfigureAwait(false);
            Require(
                ravenConvergedFrame.CumulativeImportedFactIds.Any(),
                "Raven live daemon should record imported remote fact ids");
        }
        finally
        {
            StopProcess(ravenDaemon);
            StopProcess(starfireDaemon);
        }
    }

    private static async Task<IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument>> ReadFactsAsync(
        string statePath,
        string runtimeId)
    {
        await using var node = await AetheriaStateNode.OpenAsync(
            statePath,
            runtimeId,
            startServer: false,
            enableDurableShardLogs: false).ConfigureAwait(false);
        return node.Documents<AetheriaRuntimeCommittedCommandFactDocument>()
            .OrderBy(fact => fact.CommittedAtUtc ?? "", StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId ?? "", StringComparer.Ordinal)
            .ToArray();
    }

    private static void TrustedCommittedFactsConvergeLocalRuns()
    {
        var smokeId = Guid.NewGuid().ToString("N");
        var policy = CoopPolicyWithMetadata();
        var ravenKey = EntityKey("coop-smoke", 0, 0);
        var hostileKey = EntityKey("coop-smoke", 0, 1);

        var ravenMovement = MovementCommand("raven-unity", ravenKey, directionX: 1, directionY: 0, magnitude: 1);
        var ravenSourceFrame = TickWithPolicy(
            "raven-local",
            InitialCoopFactRun(),
            policy,
            [ravenMovement]);
        var ravenMovementFact = AetheriaRuntimeCommittedCommandFactDocument.FromAppliedCommand(
            ravenSourceFrame,
            ravenMovement,
            policy.VerseId);

        var starfireImport = AetheriaRuntimeCommittedFactImporter.ImportIntoFrame(
            Path.Combine(Path.GetTempPath(), $"aetheria-authority-facts-starfire-{smokeId}.cc"),
            AetheriaRuntimeDaemonFrameDocument.Create(
                InitialCoopFactRun(),
                "starfire-local",
                "authority-smoke",
                frameId: 0,
                simulationTimeSeconds: 0,
                fixedDeltaSeconds: 0.02),
            [ravenMovementFact],
            policy,
            leases: null,
            localRuntimeId: "starfire-local",
            daemonId: "starfire-local",
            sessionId: "authority-smoke",
            verseId: policy.VerseId);

        Require(starfireImport.AcceptedFactIds.Contains(ravenMovementFact.FactId), "Starfire should accept Raven's committed Raven movement fact");
        Require(!starfireImport.RejectedFactIds.Contains(ravenMovementFact.FactId), "Starfire should not reject Raven's authorized movement fact");
        Require(Entity(starfireImport.Run, ravenKey).PositionX > 0, "Starfire local run should converge Raven movement into local state");

        var hostileRename = MetadataNameCommand("starfire-rts", hostileKey, "Starfire Marked Hostile");
        var starfireSourceFrame = TickWithPolicy(
            "starfire-local",
            InitialCoopFactRun(),
            policy,
            [hostileRename]);
        var hostileRenameFact = AetheriaRuntimeCommittedCommandFactDocument.FromAppliedCommand(
            starfireSourceFrame,
            hostileRename,
            policy.VerseId);

        var ravenImport = AetheriaRuntimeCommittedFactImporter.ImportIntoFrame(
            Path.Combine(Path.GetTempPath(), $"aetheria-authority-facts-raven-{smokeId}.cc"),
            AetheriaRuntimeDaemonFrameDocument.Create(
                InitialCoopFactRun(),
                "raven-local",
                "authority-smoke",
                frameId: 0,
                simulationTimeSeconds: 0,
                fixedDeltaSeconds: 0.02),
            [hostileRenameFact],
            policy,
            leases: null,
            localRuntimeId: "raven-local",
            daemonId: "raven-local",
            sessionId: "authority-smoke",
            verseId: policy.VerseId);

        Require(ravenImport.AcceptedFactIds.Contains(hostileRenameFact.FactId), "Raven should accept Starfire's hostile metadata fact");
        RequireEqual("Starfire Marked Hostile", Entity(ravenImport.Run, hostileKey).Name, "Raven local run should converge Starfire hostile metadata into local state");

        var unauthorizedRename = MetadataNameCommand("raven-unity", hostileKey, "Raven Should Not Own This");
        var unauthorizedFact = AetheriaRuntimeCommittedCommandFactDocument.FromAppliedCommand(
            ravenSourceFrame,
            unauthorizedRename,
            policy.VerseId);
        var rejectedImport = AetheriaRuntimeCommittedFactImporter.ImportIntoFrame(
            Path.Combine(Path.GetTempPath(), $"aetheria-authority-facts-rejected-{smokeId}.cc"),
            AetheriaRuntimeDaemonFrameDocument.Create(
                InitialCoopFactRun(),
                "starfire-local",
                "authority-smoke",
                frameId: 0,
                simulationTimeSeconds: 0,
                fixedDeltaSeconds: 0.02),
            [unauthorizedFact],
            policy,
            leases: null,
            localRuntimeId: "starfire-local",
            daemonId: "starfire-local",
            sessionId: "authority-smoke",
            verseId: policy.VerseId);

        Require(rejectedImport.RejectedFactIds.Contains(unauthorizedFact.FactId), "Starfire should reject Raven-authored hostile metadata fact");
        RequireEqual("Starfire Hostile", Entity(rejectedImport.Run, hostileKey).Name, "Rejected facts should not mutate local state");
    }

    private static async Task SeedDaemonStateAsync(
        string statePath,
        string runtimeId,
        AetheriaRuntimeVerseAuthorityPolicyDocument policy,
        IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> commands)
    {
        await using var node = await AetheriaStateNode.OpenAsync(
            statePath,
            runtimeId,
            startServer: false,
            enableDurableShardLogs: false).ConfigureAwait(false);

        await node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)
            .ReplaceAsync(policy)
            .ConfigureAwait(false);
        foreach (var command in commands)
            await node.SubmitDaemonCommandAsync(command).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);

        await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
            .ReplaceAsync(AetheriaRuntimeDaemonFrameDocument.Create(
                InitialCoopRun(),
                runtimeId,
                "authority-smoke",
                frameId: -1,
                simulationTimeSeconds: 0,
                fixedDeltaSeconds: 0.02))
            .ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
    }

    private static async Task RunDaemonOnceAsync(
        string statePath,
        string runtimeId,
        int clientCultMeshPort)
    {
        using var process = StartDaemonProcess(statePath, runtimeId, clientCultMeshPort, once: true);
        await WaitForProcessSuccessAsync(process, runtimeId, TimeSpan.FromSeconds(90)).ConfigureAwait(false);
    }

    private static Process StartDaemonProcess(
        string statePath,
        string runtimeId,
        int clientCultMeshPort,
        bool once)
    {
        return StartDaemonProcess(statePath, runtimeId, clientCultMeshPort, once, peerEndpoints: null);
    }

    private static Process StartDaemonProcess(
        string statePath,
        string runtimeId,
        int clientCultMeshPort,
        bool once,
        IReadOnlyList<string>? peerEndpoints)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        var executablePath = Path.Combine(
            repoRoot,
            "Aetheria.State.Daemon",
            "bin",
            "Debug",
            "net10.0",
            "Aetheria.State.Daemon.exe");
        var dllPath = Path.Combine(
            repoRoot,
            "Aetheria.State.Daemon",
            "bin",
            "Debug",
            "net10.0",
            "Aetheria.State.Daemon.dll");
        var projectPath = Path.Combine(
            repoRoot,
            "Aetheria.State.Daemon",
            "Aetheria.State.Daemon.csproj");
        var executableExists = File.Exists(executablePath);
        var dllExists = File.Exists(dllPath);
        var commandPrefix = executableExists
            ? Quote(executablePath)
            : dllExists
                ? "dotnet " + Quote(dllPath)
                : "dotnet run --project " + Quote(projectPath) + " --";
        var arguments = string.Join(
            " ",
            "--state",
            Quote(statePath),
            "--daemon-id",
            runtimeId,
            "--verse-id",
            "aetheria.coop-smoke",
            "--session-id",
            "authority-smoke",
            "--client-cultmesh-port",
            clientCultMeshPort.ToString(),
            "--api-publication-interval-ms",
            "50",
            string.Join(" ", (peerEndpoints ?? Array.Empty<string>())
                .Select(endpoint => "--peer-cultmesh-endpoint " + Quote(endpoint))),
            once ? "--once" : "");
        var startInfo = executableExists
            ? new ProcessStartInfo(executablePath, arguments)
            : dllExists
                ? new ProcessStartInfo("dotnet", Quote(dllPath) + " " + arguments)
                : new ProcessStartInfo("dotnet", "run --project " + Quote(projectPath) + " -- " + arguments);
        startInfo.WorkingDirectory = repoRoot;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        if (string.IsNullOrWhiteSpace(commandPrefix))
        {
            throw new InvalidOperationException("Cannot resolve daemon command.");
        }

        var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start Aetheria daemon child process.");
        CaptureProcessOutput(process, runtimeId);
        return process;
    }

    private static async Task WaitForProcessSuccessAsync(
        Process process,
        string runtimeId,
        TimeSpan timeout)
    {
        var waitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitTask, Task.Delay(timeout))
            .ConfigureAwait(false);
        if (completed != waitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"Aetheria daemon child process timed out for {runtimeId}.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Aetheria daemon child process failed for {runtimeId} with exit code {process.ExitCode}.\n{GetProcessOutputTail(process)}");
        }
    }

    private static void CaptureProcessOutput(Process process, string runtimeId)
    {
        lock (ProcessOutputLock)
            ProcessOutput[process.Id] = new List<string>();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                AppendProcessOutput(process.Id, $"{runtimeId} stdout: {eventArgs.Data}");
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                AppendProcessOutput(process.Id, $"{runtimeId} stderr: {eventArgs.Data}");
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private static void AppendProcessOutput(int processId, string line)
    {
        lock (ProcessOutputLock)
        {
            if (!ProcessOutput.TryGetValue(processId, out var lines))
            {
                lines = new List<string>();
                ProcessOutput[processId] = lines;
            }

            lines.Add(line);
            if (lines.Count > 80)
                lines.RemoveRange(0, lines.Count - 80);
        }
    }

    private static string GetProcessOutputTail(Process process)
    {
        lock (ProcessOutputLock)
        {
            if (!ProcessOutput.TryGetValue(process.Id, out var lines) || lines.Count == 0)
                return "No child output captured.";

            var builder = new StringBuilder();
            foreach (var line in lines)
                builder.AppendLine(line);
            return builder.ToString();
        }
    }

    private static async Task<AetheriaRuntimeDaemonFrameDocument> WaitForReplicaFrameAsync(
        string endpoint,
        string replicaPath,
        string runtimeId,
        IReadOnlyList<Process> childProcesses,
        Func<AetheriaRuntimeDaemonFrameDocument, bool> isReady,
        string expectedFrame)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        Exception? lastError = null;
        AetheriaRuntimeDaemonFrameDocument? lastFrame = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await AetheriaVerseReplica.SyncScopedSnapshotAsync(
                    replicaPath,
                    endpoint,
                    runtimeId,
                    schemaIds:
                    [
                        AetheriaRuntimeDaemonSchemas.Frame,
                        AetheriaRuntimeDaemonSchemas.CommittedCommandFact
                    ]).ConfigureAwait(false);
                await using var node = await AetheriaStateNode.OpenAsync(
                    replicaPath,
                    runtimeId,
                    startServer: false,
                    enableDurableShardLogs: false).ConfigureAwait(false);
                var frame = await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest).ReadAsync().ConfigureAwait(false);
                if (frame != null)
                {
                    lastFrame = frame;
                }

                if (frame != null && frame.FrameId >= 0 && isReady(frame))
                    return frame;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for replica frame from {endpoint}. Expected {expectedFrame}." +
            $" Requested schemas=[{AetheriaRuntimeDaemonSchemas.Frame},{AetheriaRuntimeDaemonSchemas.CommittedCommandFact}]." +
            (lastFrame == null ? "" : $" Last frame: daemon={lastFrame.DaemonId}, frame={lastFrame.FrameId}, applied=[{string.Join(",", lastFrame.AppliedCommandIds)}], rejected=[{string.Join(",", lastFrame.RejectedCommandIds)}], cumulativeApplied=[{string.Join(",", lastFrame.CumulativeAppliedCommandIds)}], cumulativeRejected=[{string.Join(",", lastFrame.CumulativeRejectedCommandIds)}].") +
            ProcessDiagnostics(childProcesses) +
            (lastError == null ? "" : $" Last error: {lastError.GetType().Name}: {lastError.Message}"));
    }

    private static async Task<IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument>> WaitForReplicaFactsAsync(
        string endpoint,
        string replicaPath,
        string runtimeId,
        IReadOnlyList<Process> childProcesses,
        Func<IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument>, bool> isReady,
        string expectedFacts)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        Exception? lastError = null;
        IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument> lastFacts = Array.Empty<AetheriaRuntimeCommittedCommandFactDocument>();
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await AetheriaVerseReplica.SyncScopedSnapshotAsync(
                    replicaPath,
                    endpoint,
                    runtimeId,
                    schemaIds:
                    [
                        AetheriaRuntimeDaemonSchemas.Frame,
                        AetheriaRuntimeDaemonSchemas.CommittedCommandFact
                    ]).ConfigureAwait(false);
                var facts = await ReadFactsAsync(replicaPath, runtimeId).ConfigureAwait(false);
                lastFacts = facts;
                if (isReady(facts))
                    return facts;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for committed facts from {endpoint}. Expected {expectedFacts}." +
            $" Requested schemas=[{AetheriaRuntimeDaemonSchemas.Frame},{AetheriaRuntimeDaemonSchemas.CommittedCommandFact}]." +
            $" Last facts=[{string.Join(",", lastFacts.Select(fact => $"{fact.SourceDaemonId}:{fact.CommandId}:{fact.Outcome}"))}]." +
            ProcessDiagnostics(childProcesses) +
            (lastError == null ? "" : $" Last error: {lastError.GetType().Name}: {lastError.Message}"));
    }

    private static async Task<AetheriaRuntimeDaemonFrameDocument> WaitForLocalFrameAsync(
        string statePath,
        string runtimeId,
        IReadOnlyList<Process> childProcesses,
        Func<AetheriaRuntimeDaemonFrameDocument, bool> isReady,
        string expectedFrame)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        Exception? lastError = null;
        AetheriaRuntimeDaemonFrameDocument? lastFrame = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var frame = await TryReadPublishedFrameAsync(statePath, runtimeId).ConfigureAwait(false);
                if (frame != null)
                {
                    lastFrame = frame;
                    if (isReady(frame))
                        return frame;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for local daemon frame at {statePath}. Expected {expectedFrame}." +
            (lastFrame == null ? "" : $" Last frame: daemon={lastFrame.DaemonId}, frame={lastFrame.FrameId}, imported=[{string.Join(",", lastFrame.ImportedFactIds)}], cumulativeImported=[{string.Join(",", lastFrame.CumulativeImportedFactIds)}], rejectedImported=[{string.Join(",", lastFrame.CumulativeRejectedImportedFactIds)}].") +
            ProcessDiagnostics(childProcesses) +
            (lastError == null ? "" : $" Last error: {lastError.GetType().Name}: {lastError.Message}"));
    }

    private static async Task<AetheriaRuntimeDaemonFrameDocument> ReadPublishedFrameAsync(
        string statePath,
        string runtimeId)
    {
        var frame = await TryReadPublishedFrameAsync(statePath, runtimeId).ConfigureAwait(false);
        if (frame == null)
            throw new InvalidOperationException($"No managed latest daemon frame is published at {statePath}.");

        return frame;
    }

    private static async Task<AetheriaRuntimeDaemonFrameDocument?> TryReadPublishedFrameAsync(
        string statePath,
        string runtimeId)
    {
        await using var node = await AetheriaStateNode.OpenAsync(
            statePath,
            runtimeId,
            startServer: false,
            enableDurableShardLogs: false).ConfigureAwait(false);
        return await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest).ReadAsync().ConfigureAwait(false);
    }

    private static string ProcessDiagnostics(IReadOnlyList<Process> childProcesses)
    {
        var lines = new List<string>();
        foreach (var process in childProcesses ?? Array.Empty<Process>())
        {
            try
            {
                lines.Add(process.HasExited
                    ? $" Child pid={process.Id} exited code={process.ExitCode}."
                    : $" Child pid={process.Id} still running.");
                lines.Add(Environment.NewLine);
                lines.Add(GetProcessOutputTail(process));
            }
            catch (Exception ex)
            {
                lines.Add($" Child diagnostics failed: {ex.GetType().Name}: {ex.Message}.");
            }
        }

        return lines.Count == 0 ? "" : string.Concat(lines);
    }

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        try
        {
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private static AetheriaRuntimeDaemonFrameDocument TickWithPolicy(
        string daemonId,
        AetheriaRuntimeRunCheckpointCommit run,
        AetheriaRuntimeVerseAuthorityPolicyDocument policy,
        IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> observedCommands)
    {
        var rejectedIds = new List<string>();
        var authorizedCommands = AetheriaRuntimeAuthorityRouter.AuthorizedCommands(
            observedCommands,
            policy,
            leases: null,
            localRuntimeId: daemonId,
            rejectedCommandIds: rejectedIds);

        var result = AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), $"aetheria-authority-smoke-{daemonId}.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                DaemonId = daemonId,
                SessionId = "authority-smoke",
                VerseId = policy.VerseId,
                FrameId = 1,
                SimulationTimeSeconds = 0.02,
                FixedDeltaSeconds = 0.02,
                ObservedCommands = authorizedCommands,
                PreRejectedCommandIds = rejectedIds,
                BuildPublications = false
            });

        return result.Frame;
    }

    private static AetheriaRuntimeVerseAuthorityPolicyDocument Policy(
        string defaultMode,
        params AetheriaRuntimeAuthorityRule[] rules)
    {
        return new AetheriaRuntimeVerseAuthorityPolicyDocument
        {
            VerseId = "aetheria.test",
            HostRuntimeId = "host-daemon",
            DefaultMode = defaultMode,
            Rules = rules
        };
    }

    private static AetheriaRuntimeVerseAuthorityPolicyDocument CoopPolicy()
    {
        var ravenKey = EntityKey("coop-smoke", 0, 0);
        var hostileKey = EntityKey("coop-smoke", 0, 1);
        var policy = Policy(
            AetheriaRuntimeAuthorityModes.HostAuthoritative,
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "raven.runtime.owns.raven.movement",
                SubjectPrefix = ravenKey,
                ClaimKinds = [AetheriaRuntimeClaimKinds.Movement],
                Mode = AetheriaRuntimeAuthorityModes.DelegatedRuntime,
                RuntimeIds = ["raven-unity"],
                Priority = 100
            },
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "starfire.runtime.owns.hostile.movement",
                SubjectPrefix = hostileKey,
                ClaimKinds = [AetheriaRuntimeClaimKinds.Movement],
                Mode = AetheriaRuntimeAuthorityModes.DelegatedRuntime,
                RuntimeIds = ["starfire-rts"],
                Priority = 100
            });
        policy.VerseId = "aetheria.coop-smoke";
        policy.PolicyId = "aetheria.coop-smoke.delegated.v1";
        return policy;
    }

    private static AetheriaRuntimeVerseAuthorityPolicyDocument CoopPolicyWithMetadata()
    {
        var policy = CoopPolicy();
        var ravenKey = EntityKey("coop-smoke", 0, 0);
        var hostileKey = EntityKey("coop-smoke", 0, 1);
        policy.Rules = policy.Rules
            .Concat(new[]
            {
                new AetheriaRuntimeAuthorityRule
                {
                    RuleId = "raven.runtime.owns.raven.metadata",
                    SubjectPrefix = ravenKey,
                    ClaimKinds = [AetheriaRuntimeClaimKinds.Metadata],
                    Mode = AetheriaRuntimeAuthorityModes.DelegatedRuntime,
                    RuntimeIds = ["raven-unity"],
                    Priority = 90
                },
                new AetheriaRuntimeAuthorityRule
                {
                    RuleId = "starfire.runtime.owns.hostile.metadata",
                    SubjectPrefix = hostileKey,
                    ClaimKinds = [AetheriaRuntimeClaimKinds.Metadata],
                    Mode = AetheriaRuntimeAuthorityModes.DelegatedRuntime,
                    RuntimeIds = ["starfire-rts"],
                    Priority = 90
                }
            })
            .ToArray();
        return policy;
    }

    private static AetheriaRuntimeDaemonCommandDocument Command(
        string runtimeId,
        string subjectKey,
        AetheriaRuntimeDaemonCommandKinds kind = AetheriaRuntimeDaemonCommandKinds.SetMoveVector)
    {
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            kind,
            runtimeId,
            sessionId: "authority-smoke",
            observedFrameId: 0,
            actorEntityKey: subjectKey);
        command.SubjectKey = subjectKey;
        command.AuthorRuntimeId = runtimeId;
        return command;
    }

    private static AetheriaRuntimeDaemonCommandDocument MovementCommand(
        string runtimeId,
        string subjectKey,
        double directionX,
        double directionY,
        double magnitude)
    {
        var command = Command(runtimeId, subjectKey, AetheriaRuntimeDaemonCommandKinds.SetMoveVector);
        command.DirectionX = directionX;
        command.DirectionY = directionY;
        command.ScalarValue = magnitude;
        return command;
    }

    private static AetheriaRuntimeDaemonCommandDocument MetadataNameCommand(
        string runtimeId,
        string subjectKey,
        string name)
    {
        var command = Command(runtimeId, subjectKey, AetheriaRuntimeDaemonCommandKinds.SetEntityName);
        command.TargetEntityKey = subjectKey;
        command.TextValue = name;
        command.ClaimKind = AetheriaRuntimeClaimKinds.Metadata;
        return command;
    }

    private static AetheriaRuntimeRunCheckpointCommit InitialCoopRun()
    {
        return new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "coop-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = EntityKey("coop-smoke", 0, 0),
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Name = "Co-op Smoke Zone",
                    Entities =
                    [
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 0,
                            Name = "Raven Pilot",
                            Kind = "ship",
                            FactionKey = "player",
                            IsActive = true
                        },
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 1,
                            Name = "Starfire Hostile",
                            Kind = "ship",
                            FactionKey = "player",
                            IsActive = true
                        }
                    ]
                }
            ]
        };
    }

    private static AetheriaRuntimeRunCheckpointCommit InitialCoopFactRun()
    {
        var run = InitialCoopRun();
        var zone = run.Zones[0];
        zone.Entities[0].FactionKey = "player";
        zone.Entities[1].FactionKey = "raider";
        return run;
    }

    private static AetheriaRuntimeEntitySnapshotCommit Entity(
        AetheriaRuntimeRunCheckpointCommit run,
        string entityKey)
    {
        var parts = entityKey.Split('.');
        var zoneIndex = Array.IndexOf(parts, "zone") >= 0 && int.TryParse(parts[Array.IndexOf(parts, "zone") + 1], out var parsedZone)
            ? parsedZone
            : 0;
        var entityIndex = Array.IndexOf(parts, "entity") >= 0 && int.TryParse(parts[Array.IndexOf(parts, "entity") + 1], out var parsedEntity)
            ? parsedEntity
            : 0;
        var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .First(candidate => candidate.ZoneIndex == zoneIndex);
        return (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            .First(candidate => candidate.EntityIndex == entityIndex);
    }

    private static AetheriaRuntimeEntitySnapshotCommit SnapshotEntity(
        int entityIndex,
        string name,
        string factionKey,
        double x,
        double z,
        double visibility,
        double hull,
        string cargoItem)
    {
        return new AetheriaRuntimeEntitySnapshotCommit
        {
            EntityIndex = entityIndex,
            Name = name,
            Kind = "ship",
            FactionKey = factionKey,
            IsActive = true,
            PositionX = x,
            PositionZ = z,
            DirectionX = 1,
            DirectionY = 0,
            Visibility = visibility,
            TargetEntityIndex = -1,
            StatGrids =
            [
                new AetheriaRuntimeEntityStatGridCommit
                {
                    Name = "hull",
                    Width = 1,
                    Height = 1,
                    Values = [hull]
                },
                new AetheriaRuntimeEntityStatGridCommit
                {
                    Name = "shield",
                    Width = 1,
                    Height = 1,
                    Values = [25]
                },
                new AetheriaRuntimeEntityStatGridCommit
                {
                    Name = "heat",
                    Width = 1,
                    Height = 1,
                    Values = [4]
                }
            ],
            CargoContents =
            [
                new AetheriaRuntimeCargoBayLoadoutCommit
                {
                    Items =
                    [
                        new AetheriaRuntimeLoadoutItemSlotCommit
                        {
                            Item = new AetheriaRuntimeLoadoutItemCommit
                            {
                                ItemKey = cargoItem,
                                Quantity = 1,
                                Quality = 1,
                                Durability = 1,
                                Enabled = true
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static string EntityKey(string runId, int zoneIndex, int entityIndex)
    {
        return $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void RequireSurfaceMetric(
        AetheriaRuntimeSurfaceDocument surface,
        string componentId,
        string expectedValue,
        string message)
    {
        var component = Flatten(surface.Surface.Root)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, componentId, StringComparison.Ordinal));
        if (component == null)
            throw new InvalidOperationException($"{message}: missing component {componentId}");
        if (!component.Props.TryGetValue("value", out var actualValue) ||
            !string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message}: expected {expectedValue}, got {actualValue ?? "(missing)"}");
        }
    }

    private static IEnumerable<AetheriaRuntimeSurfaceComponent> Flatten(AetheriaRuntimeSurfaceComponent component)
    {
        yield return component;
        foreach (var child in component.Children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>())
        {
            foreach (var nested in Flatten(child))
                yield return nested;
        }
    }
}
