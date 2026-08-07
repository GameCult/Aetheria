using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;
using GameCult.Caching;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using GameCult.Networking;
using GameCult.Eve.Surface;
using EveProviderAdvertisementState = GameCult.Eve.Surface.EveProviderAdvertisementDocument;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var stateDirectory = Path.Combine(Path.GetTempPath(), "aetheria-state-smoke", Guid.NewGuid().ToString("N"));
var statePath = Path.Combine(stateDirectory, "aetheria-world.cc");
var now = DateTimeOffset.UtcNow.ToString("O");
var itemLegacyId = "smoke:aether-drive";
var factionLegacyId = "smoke:faction";
var nameFileLegacyId = "smoke:name-file";
var runKey = new CultRecordKey("run:smoke");
var loadoutKey = new CultRecordKey("loadout:smoke:aether-runner");
var zoneKey = new CultRecordKey("zone:smoke:0");
var entityKey = new CultRecordKey("entity:smoke:runner");
var itemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId);
var factionKey = AetheriaCatalogKeys.CorporationFromLegacyId(factionLegacyId);
var nameFileKey = AetheriaCatalogKeys.NameFileFromLegacyId(nameFileLegacyId);

await using (var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-smoke"))
{
    await node.MutableDocument<AetheriaWorldState>(AetheriaStateNode.WorldKey).ReplaceAsync(new AetheriaWorldState
    {
        Name = "Aetheria",
        WorldId = "aetheria",
        SchemaEpoch = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    });

    await node.MutableDocument<AetheriaItemDefinition>(itemKey)
        .ReplaceAsync(new AetheriaItemDefinition
        {
            Name = "Smoke Aether Drive",
            Category = "ship-module",
            LegacyId = itemLegacyId,
            Description = "Typed CultCache smoke document for the rebuild spine.",
            Mass = 12.5,
            Volume = 4.0,
            Tags = ["smoke", "state-spine"]
        });

    await node.MutableDocument<AetheriaMigrationLedger>(AetheriaStateNode.MigrationLedgerKey).ReplaceAsync(new AetheriaMigrationLedger
    {
        Source = LegacyMigrationBoundary.LegacyGameDataFile,
        SourceFingerprint = "smoke",
        LastMigrationAtUtc = now,
        Counts =
        [
            new AetheriaMigrationCount
            {
                DocumentType = "aetheria.item_definition.v1",
                Count = 1
            }
        ],
        Notes = ["Smoke proves the new state owner can write, flush, reopen, and read without old JSON/Rethink authority."]
    });

    await node.MutableDocument<AetheriaLegacyCatalogQuarantine>(AetheriaStateNode.LegacyCatalogQuarantineKey).ReplaceAsync(new AetheriaLegacyCatalogQuarantine
    {
        RootPath = root,
        CapturedAtUtc = now,
        CatalogFile = LegacyMigrationBoundary.LegacyGameDataFile,
        CatalogFingerprint = "smoke",
        CatalogBytes = 12,
        NameFiles =
        [
            new AetheriaLegacyCatalogFile
            {
                RelativePath = "GameData/NameFile/smoke.msgpack",
                Fingerprint = "smoke-name-file",
                Bytes = 4
            }
        ],
        Notes = ["Smoke proves legacy catalog quarantine state is typed and durable."]
    });

    await node.MutableDocument<AetheriaCorporation>(factionKey).ReplaceAsync(new AetheriaCorporation
    {
        Name = "Smoke Faction",
        LegacyId = factionLegacyId,
        Description = "Typed faction/corporation document for legacy catalog migration smoke."
    });

    await node.MutableDocument<AetheriaNameFile>(nameFileKey).ReplaceAsync(new AetheriaNameFile
    {
        Name = "Smoke Names",
        LegacyId = nameFileLegacyId,
        NameCount = 2,
        SampleNames = ["Ada", "Grace"]
    });

    await node.FlushAsync();
    await node.RefreshRuntimeCatalogAsync();
    var runtimeCatalog = await node.RuntimeCatalog().LatestAsync().ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.CatalogSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildCatalogSurface(runtimeCatalog, now));

    var verseHostSettings = AetheriaVerseHostSettingsNormalizer.Normalize(new AetheriaVerseHostSettings
    {
        LastUpdatedAtUtc = now
    });
    await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReplaceAsync(verseHostSettings);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildOperationsSurface(verseHostSettings: verseHostSettings));
    var providerAdvertisement = AetheriaEveSurfaceDocuments.BuildProviderAdvertisement(verseHostSettings, statePath, now);
    var pilotAdvertisement = providerAdvertisement.Surfaces.Single(surface =>
        surface.SurfaceId == AetheriaRuntimeDaemonGameSurfaceBuilder.PilotSurfaceId);
    if (pilotAdvertisement.WorldInteraction?.AssetManifestRecordRef !=
        AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString())
    {
        throw new InvalidOperationException("Pilot surface does not advertise its provider-owned asset catalog.");
    }
    await node.MutableDocument<EveProviderAdvertisementState>(AetheriaStateNode.ProviderAdvertisementSurfaceKey)
        .ReplaceAsync(providerAdvertisement);
    var daemonProvider = AetheriaRuntimeDaemonProviderAdvertisementDocument.Create(
        statePath,
        "smoke-daemon",
        "aetheria.local",
        "cultmesh://aetheria.local/eve/providers/aetheria.daemon");
    var daemonHealth = new AetheriaRuntimeDaemonHealthDocument
    {
        DaemonId = "smoke-daemon",
        VerseId = "aetheria.local",
        PublishedAtUtc = now,
        StatePath = statePath,
        FrameId = 7,
        ObservedCommandCount = 1,
        AppliedCommandCount = 2,
        RejectedCommandCount = 0,
        Status = "healthy",
        CommandBoundaryPath = AetheriaRuntimeStateBoundary.GetDaemonCommandBoundaryPath(statePath)
    };
    var daemonCommandBoundary = AetheriaRuntimeDaemonCommandBoundaryDocument.Create("smoke-daemon");
    var daemonFrame = AetheriaRuntimeDaemonFrameDocument.Create(
        new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "smoke-run",
            CurrentZoneIndex = 0,
            CurrentEntityKey = entityKey.ToString()
        },
        "smoke-daemon",
        "smoke-session",
        7,
        0.14,
        0.02);
    var daemonGameSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
        daemonFrame,
        daemonHealth,
        daemonCommandBoundary);
    var playableWorld = daemonGameSurface.Surface.Root.Children.Single(component =>
        component.Id == "aetheria.daemon.game.world");
    var expectedPlayableWorldProps = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cameraRig"] = "perspective.entity-forward-follow.v1",
        ["cameraTargetEntityId"] = entityKey.ToString(),
        ["cameraDistance"] = "30",
        ["cameraVerticalFieldOfViewDegrees"] = "60",
        ["cameraTargetScreenX"] = "0.64",
        ["cameraTargetScreenY"] = "0.19",
        ["cameraPositionDamping"] = "0",
        ["cameraNearClipPlane"] = "1",
        ["cameraFarClipPlane"] = "4096",
        ["ambientLightColor"] = "0.2,0.2,0.2",
        ["ambientLightIntensity"] = "1.46"
    };
    var playableWorldMismatches = expectedPlayableWorldProps
        .Where(expected =>
            !playableWorld.Props.TryGetValue(expected.Key, out var actual) ||
            !string.Equals(actual, expected.Value, StringComparison.Ordinal))
        .Select(expected =>
            $"{expected.Key}: expected '{expected.Value}', actual '{(playableWorld.Props.TryGetValue(expected.Key, out var actual) ? actual : "<missing>")}'")
        .ToArray();
    if (playableWorldMismatches.Length > 0)
    {
        throw new InvalidOperationException(
            $"Playable world did not publish the native camera and environment contract: {string.Join("; ", playableWorldMismatches)}");
    }
    var gravityFog = playableWorld.Children.Single(component =>
        component.Id == "aetheria.daemon.game.world.gravity-fog");
    var stardust = playableWorld.Children.Single(component =>
        component.Id == "aetheria.daemon.game.world.stardust");
    if (!gravityFog.Props["features"].Split(';').Contains("flow.value3d", StringComparer.Ordinal) ||
        !stardust.Props["features"].Split(';').Contains("flow.value3d", StringComparer.Ordinal))
    {
        throw new InvalidOperationException("Aetheria's default render settings did not select smooth Value3D flow.");
    }

    daemonFrame.RenderSettings = new AetheriaRuntimeDaemonRenderSettings(
        default,
        default,
        default,
        0,
        0,
        0,
        0,
        0,
        0,
        useValue3DFlow: false);
    var triangleSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
        daemonFrame,
        daemonHealth,
        daemonCommandBoundary);
    var triangleWorld = triangleSurface.Surface.Root.Children.Single(component =>
        component.Id == "aetheria.daemon.game.world");
    if (triangleWorld.Children
        .Where(component => component.Id is "aetheria.daemon.game.world.gravity-fog" or "aetheria.daemon.game.world.stardust")
        .Any(component => component.Props["features"].Split(';').Contains("flow.value3d", StringComparer.Ordinal)))
    {
        throw new InvalidOperationException("Triangle flow remained opted into the Value3D feature keyword.");
    }
    daemonFrame.RenderSettings = AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;

    var renderAssets = AetheriaRuntimeAssets.ProjectManifest(null);
    if (!renderAssets.Assets.Any(asset =>
            asset.Ref.AssetKey == "shader.environment.gravity-fog" &&
            asset.Ref.Metadata.TryGetValue("unity.volume.feature.flow.value3d.keyword", out var keyword) &&
            keyword == "FLOW_VALUE3D") ||
        !renderAssets.Assets.Any(asset =>
            asset.Ref.AssetKey == "compute.environment.stardust" &&
            asset.Ref.Metadata.TryGetValue("unity.particles.feature.flow.value3d.keyword", out var keyword) &&
            keyword == "FLOW_VALUE3D"))
    {
        throw new InvalidOperationException("Aetheria's asset catalog did not map smooth flow to FLOW_VALUE3D.");
    }
    await node.MutableDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement)
        .ReplaceAsync(daemonProvider);
    await node.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth)
        .ReplaceAsync(daemonHealth);
    await node.MutableDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary)
        .ReplaceAsync(daemonCommandBoundary);
    await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
        .ReplaceAsync(daemonFrame);
    await node.FlushAsync();
    using var daemonCommandClient = await AetheriaClient.OpenAsync(
        statePath,
        "aetheria-state-smoke-command-client",
        "smoke-session",
        startServer: false,
        pullOnOpen: true);
    daemonCommandClient.Control.SensorPing();

    await using (var commandVerifyNode = await AetheriaStateNode.OpenAsync(
                     statePath,
                     "aetheria-state-smoke-command-check"))
    {
        if (commandVerifyNode.Documents<AetheriaRuntimeDaemonCommandDocument>()
            .OrderBy(command => command.IssuedAtUtc ?? "", StringComparer.Ordinal)
            .ThenBy(command => command.CommandId ?? "", StringComparer.Ordinal)
            .All(command =>
                command.Kind != AetheriaRuntimeDaemonCommandKinds.SensorPing ||
                command.ClientId != "aetheria-state-smoke-command-client"))
        {
            throw new InvalidOperationException("AetheriaClient control submission did not appear as a typed daemon state record.");
        }
    }
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(daemonGameSurface));
    await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey("smoke-runtime")).ReplaceAsync(new AetheriaRuntimeSession
    {
        RuntimeId = "smoke-runtime",
        Role = "state-smoke",
        StartedAtUtc = now,
        LastSeenAtUtc = now,
        Status = "running"
    });
    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey).ReplaceAsync(AetheriaEveSurfaceDocuments.BuildOperationsSurface(
        verseHostSettings: verseHostSettings,
        runtimeSession: await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey("smoke-runtime")).ReadAsync()));

    await node.MutableDocument<AetheriaLoadoutTemplate>(loadoutKey).ReplaceAsync(new AetheriaLoadoutTemplate
    {
        Name = "Smoke Aether Runner",
        OwnerPlayerKey = "player:smoke",
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        RootEntity = new AetheriaEntityLoadout
        {
            Name = "Smoke Aether Runner",
            Kind = "ship",
            FactionKey = factionKey.ToString(),
            Hull = new AetheriaLoadoutItem
            {
                ItemKey = itemKey.ToString(),
                Quality = 1.0,
                Durability = 1.0
            },
            Equipment =
            [
                new AetheriaLoadoutItemSlot
                {
                    Position = new AetheriaGridCoord { X = 0, Y = 0 },
                    Rotation = "Clockwise",
                    Item = new AetheriaLoadoutItem
                    {
                        ItemKey = itemKey.ToString(),
                        Quality = 0.95,
                        Durability = 0.8,
                        Enabled = false,
                        OverrideShutdown = true
                    }
                }
            ],
            WeaponGroups = [[0]]
        }
    });

    await node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey).ReplaceAsync(new AetheriaHangarState
    {
        HangarId = "smoke",
        PlayerKey = "player:smoke",
        Revision = 7,
        Ships =
        [
            new AetheriaHangarShip
            {
                ShipId = "ship:smoke:aether-runner",
                HullItemKey = itemKey.ToString(),
                LoadoutTemplateKey = loadoutKey.ToString()
            },
            new AetheriaHangarShip
            {
                ShipId = "ship:smoke:starbridge",
                HullItemKey = itemKey.ToString(),
                LoadoutTemplateKey = loadoutKey.ToString()
            },
            new AetheriaHangarShip
            {
                ShipId = "ship:smoke:arena",
                HullItemKey = itemKey.ToString(),
                LoadoutTemplateKey = loadoutKey.ToString()
            }
        ],
        LoadoutTemplateKeys = [loadoutKey.ToString()],
        UpdatedAtUtc = now
    });
    var deploymentRequest = new AetheriaDeploymentRequest
    {
        RequestId = "request:smoke:terminus",
        PlayerKey = "player:smoke",
        Mode = AetheriaGameModes.Terminus,
        ShipId = "ship:smoke:aether-runner",
        LoadoutTemplateKey = loadoutKey.ToString(),
        ExpectedHangarRevision = 7,
        ModePolicyId = AetheriaModePolicies.TerminusLocal
    };
    var deployment = await AetheriaHangar.AdmitAsync(node, deploymentRequest, now);
    var duplicateDeployment = await AetheriaHangar.AdmitAsync(node, deploymentRequest, now);
    if (!deployment.Accepted ||
        deployment.DeploymentId != duplicateDeployment.DeploymentId ||
        deployment.HangarRevision != 8)
        throw new InvalidOperationException("Hangar deployment admission was not accepted exactly once.");

    foreach (var (mode, shipId, revision, policy) in new[]
             {
                 (AetheriaGameModes.Starbridge, "ship:smoke:starbridge", 8L, AetheriaModePolicies.StarbridgeMixed),
                 (AetheriaGameModes.Arena, "ship:smoke:arena", 9L, AetheriaModePolicies.ArenaServer)
             })
    {
        var modeDeployment = await AetheriaHangar.AdmitAsync(node, new AetheriaDeploymentRequest
        {
            RequestId = $"request:smoke:{mode}",
            PlayerKey = "player:smoke",
            Mode = mode,
            ShipId = shipId,
            LoadoutTemplateKey = loadoutKey.ToString(),
            ExpectedHangarRevision = revision,
            ModePolicyId = policy
        }, now);
        if (!modeDeployment.Accepted || modeDeployment.Mode != mode)
            throw new InvalidOperationException($"Hangar rejected the shared {mode} deployment boundary.");
    }

    var staleDeployment = AetheriaHangar.Admit(
        await node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey).ReadAsync()
            ?? throw new InvalidOperationException("Hangar disappeared during admission smoke."),
        new AetheriaDeploymentRequest
        {
            RequestId = "request:smoke:stale",
            PlayerKey = "player:smoke",
            Mode = AetheriaGameModes.Arena,
            ShipId = "ship:smoke:aether-runner",
            LoadoutTemplateKey = loadoutKey.ToString(),
            ExpectedHangarRevision = 7,
            ModePolicyId = AetheriaModePolicies.ArenaServer
        },
        await node.MutableDocument<AetheriaLoadoutTemplate>(loadoutKey).ReadAsync(),
        now);
    if (staleDeployment.Accepted || staleDeployment.Diagnostic != "hangar revision mismatch")
        throw new InvalidOperationException("Stale Hangar deployment bypassed revision admission.");

    await node.MutableDocument<AetheriaEntitySnapshot>(entityKey).ReplaceAsync(new AetheriaEntitySnapshot
    {
        Name = "Smoke Aether Runner",
        Kind = "ship",
        Position = new AetheriaVector3 { X = 12.5, Y = 0.0, Z = -3.25 },
        Direction = new AetheriaVector2 { X = 0.0, Y = 1.0 },
        LookDirection = new AetheriaVector2 { X = 0.8, Y = 0.6 },
        HelmInput = new AetheriaVector2 { X = -0.25, Y = 0.75 },
        FactionKey = factionKey.ToString(),
        HullItemKey = itemKey.ToString(),
        Equipment =
        [
            new AetheriaEntityItemSlot
            {
                Position = new AetheriaGridCoord { X = 0, Y = 0 },
                Rotation = "CounterClockwise",
                ItemKey = itemKey.ToString(),
                Quality = 0.9,
                Durability = 0.8,
                Quantity = 1,
                Enabled = false,
                OverrideShutdown = true
            }
        ],
        CargoContents =
        [
            new AetheriaCargoBayLoadout
            {
                Items =
                [
                    new AetheriaLoadoutItemSlot
                    {
                        Position = new AetheriaGridCoord { X = 2, Y = 3 },
                        Rotation = "Half",
                        Item = new AetheriaLoadoutItem
                        {
                            ItemKey = itemKey.ToString(),
                            Quantity = 7
                        }
                    }
                ]
            }
        ],
        DockingBayAssignments = [-1],
        Visibility = 12.75,
        VisibilitySourceCount = 3,
        Contacts =
        [
            new AetheriaEntityContactSnapshot
            {
                TargetEntityKey = entityKey.ToString(),
                InfoGathered = 0.85,
                Hostile = true,
                Visible = true
            }
        ],
        WeaponGroups =
        [
            new AetheriaWeaponGroupSnapshot
            {
                EquipmentIndices = [0]
            }
        ],
        WeaponStates =
        [
            new AetheriaWeaponStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 1,
                BehaviorKind = "LockWeapon",
                Ammo = 1,
                LockProgress = 0.65,
                LockTargetEntityKey = entityKey.ToString()
            }
        ],
        BehaviorStates =
        [
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 0,
                BehaviorKind = "AetherDrive",
                AetherDriveAxisX = 0.5,
                AetherDriveAxisY = -0.25,
                AetherDriveAxisZ = 0.75,
                AetherDriveThrustX = 12.0,
                AetherDriveThrustY = 6.0,
                AetherDriveThrustZ = 3.0,
                AetherDriveRpmX = 1200.0,
                AetherDriveRpmY = 900.0,
                AetherDriveRpmZ = 450.0,
                AetherDriveMaximumRpm = 2400.0,
                AetherDriveThrustDirectionX = 1.5,
                AetherDriveThrustDirectionY = -0.5
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 1,
                BehaviorKind = "ResourceScanner",
                ResourceScannerTargetBodyKey = "aetheria.body:smoke:body",
                ResourceScannerAsteroidIndex = 2,
                ResourceScannerScanTime = 1.25,
                ResourceScannerRange = 500.0,
                ResourceScannerMinimumDensity = 0.2,
                ResourceScannerScanDuration = 3.5
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 2,
                BehaviorKind = "MiningTool",
                MiningToolAsteroidBeltKey = "aetheria.body:smoke:body",
                MiningToolAsteroidIndex = 3,
                MiningToolRange = 275.0
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 3,
                BehaviorKind = "Thruster",
                ThrusterAxis = 0.8,
                ThrusterThrust = 125.0,
                ThrusterTorque = -0.4
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 4,
                BehaviorKind = "Shield",
                ShieldEfficiency = 0.7,
                ShieldEnergyUsage = 1.4
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 5,
                BehaviorKind = "VelocityLimit",
                VelocityLimit = 42.0
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 6,
                BehaviorKind = "Thermotoggle",
                ThermotoggleTargetTemperature = 315.5
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 7,
                BehaviorKind = "Switch",
                SwitchActivated = true
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 8,
                BehaviorKind = "Trigger",
                TriggerPulled = true
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 9,
                BehaviorKind = "StatModifier",
                StatModifierApplied = true,
                StatModifierExecuted = true,
                StatModifierTargetStatCount = 2
            },
            new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = "equipment",
                OwnerIndex = 0,
                BehaviorIndex = 10,
                BehaviorKind = "TurretController",
                TurretControllerWeaponCount = 2,
                TurretControllerShotSpeed = 80.0,
                TurretControllerPredictShots = true
            }
        ],
        StatGrids =
        [
            new AetheriaEntityStatGrid
            {
                Name = "armor",
                Width = 2,
                Height = 2,
                Values = [1.0, 0.9, 0.8, 0.7]
            }
        ]
    });

    await node.MutableDocument<AetheriaZoneState>(zoneKey).ReplaceAsync(new AetheriaZoneState
    {
        Name = "Smoke Zone",
        Position = new AetheriaVector2 { X = 4.0, Y = 8.0 },
        AdjacentZoneIndices = [1],
        FactionIndices = [0],
        OwnerFactionIndex = 0,
        EntityKeys = [entityKey.ToString()],
        Orbits =
        [
            new AetheriaOrbitSnapshot
            {
                OrbitKey = "smoke:orbit",
                ParentOrbitKey = "smoke:parent-orbit",
                Distance = 100,
                Phase = 0.25,
                FixedPosition = new AetheriaVector2 { X = 5, Y = -6 }
            }
        ],
        Bodies =
        [
            new AetheriaBodySnapshot
            {
                BodyKey = "smoke:body",
                Kind = "asteroid_belt",
                Name = "Smoke Belt",
                OrbitKey = "smoke:orbit",
                Mass = 42,
                Resources =
                [
                    new AetheriaBodyResource
                    {
                        ItemKey = itemKey.ToString(),
                        Amount = 3.5
                    }
                ],
                Asteroids =
                [
                    new AetheriaAsteroidSnapshot
                    {
                        Distance = 7,
                        Phase = 0.75,
                        Size = 2,
                        RotationSpeed = 0.5,
                        Damage = 1.25,
                        RespawnTimer = 6.5,
                        MiningAccumulators =
                        [
                            new AetheriaAsteroidMiningAccumulatorSnapshot
                            {
                                MinerEntityKey = entityKey.ToString(),
                                Amount = 0.75
                            }
                        ]
                    }
                ]
            }
        ]
    });

    await node.MutableDocument<AetheriaRunState>(runKey).ReplaceAsync(new AetheriaRunState
    {
        RunId = "smoke",
        IsTutorial = false,
        EntranceZoneIndex = 0,
        ExitZoneIndex = 1,
        CurrentZoneIndex = 0,
        CurrentEntityKey = entityKey.ToString(),
        GenerationSeed = 424242,
        DiscoveredZoneIndices = [0],
        ZoneKeys = [zoneKey.ToString()],
        UpdatedAtUtc = now
    });

    await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReplaceAsync(new AetheriaPlayerSettings
    {
        ActiveRunKey = runKey.ToString(),
        LastUpdatedAtUtc = now,
        PlayerName = "Smoke Pilot",
        TutorialPassed = true,
        StoryFileHashes =
        [
            new AetheriaStoryFileHash
            {
                StoryPath = "Narrative/smoke.ink",
                Hash = "sha256:smoke"
            }
        ],
        Gameplay = new AetheriaPlayerGameplaySettings
        {
            TemperatureUnit = "Celsius",
            SignificantDigits = 4
        },
        Graphics = new AetheriaPlayerGraphicsSettings
        {
            NebulaQuality = "High",
            ShowAsteroidsInMinimap = true
        },
        Input = new AetheriaPlayerInputSettings
        {
            BindingOverrides =
            [
                new AetheriaInputBindingOverride
                {
                    ActionName = "Thrust",
                    BindingIndex = 0,
                    BindingPath = "<Keyboard>/w"
                }
            ],
            ActionBarInputs = ["<Keyboard>/1", "<Mouse>/leftButton"]
        }
    });
    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.PlayerSettingsSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildPlayerSettingsSurface(
            await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync(),
            now));

    using var eveCommandClient = await AetheriaClient.OpenAsync(
        statePath,
        "aetheria-state-smoke-eve-command-client",
        startServer: false,
        pullOnOpen: true);
    await eveCommandClient.Ui.InputSettingsAsync(
        AetheriaRuntimeEveCommandKind.SetBindingOverride,
        new AetheriaRuntimeInputSettingsCommandBody
        {
            ActionName = "Thrust",
            BindingIndex = 0,
            InputSystemPath = "<Keyboard>/w",
            Enabled = true
        },
        "aetheria-state-smoke-eve-command-client");
    await using (var commandVerifyNode = await AetheriaStateNode.OpenAsync(
                     statePath,
                     "aetheria-state-smoke-eve-command-check"))
    {
        if (commandVerifyNode.Documents<AetheriaRuntimeEveCommandDocument>()
            .Select(AetheriaRuntimeEveCommandClient.NormalizeDocument)
            .OrderBy(command => command.IssuedAtUtc ?? "", StringComparer.Ordinal)
            .ThenBy(command => command.CommandId ?? "", StringComparer.Ordinal)
            .All(command =>
                command.Kind != AetheriaRuntimeEveCommandKind.SetBindingOverride ||
                command.ClientId != "aetheria-state-smoke-eve-command-client"))
        {
            throw new InvalidOperationException("AetheriaClient UI submission did not appear as a typed Eve state record.");
        }
    }
    await node.SubmitEveCommandAsync(AetheriaRuntimeEveCommandClient.ToDocument(
        AetheriaRuntimeEveCommands.SubmitCatalogCommand(
            statePath,
            AetheriaRuntimeEveCommandKind.CatalogRefresh,
            "aetheria-state-smoke")));
    await node.SubmitEveCommandAsync(AetheriaRuntimeEveCommandClient.ToDocument(
        AetheriaRuntimeEveCommands.SubmitPlayerSettingsCommand(
            statePath,
            AetheriaRuntimeEveCommandKind.IncrementSignificantDigits,
            new AetheriaRuntimePlayerSettingsCommandBody(),
            "aetheria-state-smoke")));
    await node.SubmitEveCommandAsync(AetheriaRuntimeEveCommandClient.ToDocument(
        AetheriaRuntimeEveCommands.SubmitPlayerSettingsCommand(
            statePath,
            AetheriaRuntimeEveCommandKind.ToggleShowAsteroidsInMinimap,
            new AetheriaRuntimePlayerSettingsCommandBody(),
            "aetheria-state-smoke")));
    await node.SubmitEveCommandAsync(new AetheriaRuntimeEveCommandDocument
    {
        Schema = AetheriaRuntimeEveCommandDocument.SchemaId,
        CommandId = Guid.NewGuid().ToString("N"),
        ProviderId = "aetheria",
        SurfaceId = AetheriaRuntimeCatalogCommands.SurfaceId,
        Command = "aetheria.catalog.unknown",
        Kind = AetheriaRuntimeEveCommandKind.Unknown,
        IssuedAtUtc = DateTime.UtcNow.ToString("O"),
        ClientId = "aetheria-state-smoke"
    });
    var eveCommandReport = await AetheriaEveCommandBridge.AcceptObservedAsync(node);
    var eveCommandStatus = new AetheriaEveCommandAcceptanceStatus
    {
        RuntimeId = "smoke-runtime",
        StatePath = statePath,
        LastPollAtUtc = now,
        LastAcceptedAtUtc = now,
        ObservedBeforeAccept = 4,
        CommandsAccepted = eveCommandReport.AcceptedCommandIds.Length,
        CommandsRejected = eveCommandReport.RejectedCommands,
        AppliedCatalogRefreshes = eveCommandReport.AcceptedCatalogRefreshes,
        AppliedOperationsRefreshes = eveCommandReport.AcceptedOperationsRefreshes,
        AppliedPlayerSettingsCommands = eveCommandReport.AcceptedPlayerSettingsCommands,
        AccountedCommandIds = eveCommandReport.AccountedCommandIds,
        LastRejectedCommand = eveCommandReport.LastRejectedCommand,
        LastRejectedReason = eveCommandReport.LastRejectedReason,
        Status = eveCommandReport.RejectedCommands > 0 ? "rejected" : "ok"
    };
    await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReplaceAsync(eveCommandStatus);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey).ReplaceAsync(AetheriaEveSurfaceDocuments.BuildOperationsSurface(
        eveCommandStatus,
        verseHostSettings: await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReadAsync(),
        runtimeSession: await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey("smoke-runtime")).ReadAsync()));

    await node.FlushAsync();
}
await using (var bootNode = await AetheriaStateNode.OpenAsync(
                 statePath,
                 "aetheria-state-smoke-daemon-boot",
                 hydrationProfile: AetheriaStateHydrationProfile.DaemonBoot))
{
    if (bootNode.Cache.Get<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest) == null)
        throw new InvalidOperationException("Daemon boot did not hydrate the authoritative restart frame.");
    if (bootNode.Cache.Get<AetheriaRunState>(runKey) != null ||
        bootNode.Cache.Get<AetheriaZoneState>(zoneKey) != null ||
        bootNode.Cache.Get<AetheriaEntitySnapshot>(entityKey) != null)
    {
        throw new InvalidOperationException("Daemon boot eagerly hydrated bootstrap run, zone, or entity records.");
    }

    await bootNode.Cache.PullBackingStoreRecordsAsync(metadata =>
        string.Equals(metadata.Key, runKey.ToString(), StringComparison.Ordinal) ||
        string.Equals(metadata.Key, zoneKey.ToString(), StringComparison.Ordinal) ||
        string.Equals(metadata.Key, entityKey.ToString(), StringComparison.Ordinal));
    if (bootNode.Cache.Get<AetheriaRunState>(runKey)?.RunId != "smoke" ||
        bootNode.Cache.Get<AetheriaZoneState>(zoneKey) == null ||
        bootNode.Cache.Get<AetheriaEntitySnapshot>(entityKey) == null)
    {
        throw new InvalidOperationException("Cold bootstrap records could not be pulled explicitly for frame recovery.");
    }
}
await using (var reopened = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-smoke-reopen"))
{
    var world = await reopened.MutableDocument<AetheriaWorldState>(AetheriaStateNode.WorldKey).ReadAsync();
    var item = await reopened.MutableDocument<AetheriaItemDefinition>(AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId)).ReadAsync();
    var faction = await reopened.MutableDocument<AetheriaCorporation>(AetheriaCatalogKeys.CorporationFromLegacyId(factionLegacyId)).ReadAsync();
    var nameFile = await reopened.MutableDocument<AetheriaNameFile>(AetheriaCatalogKeys.NameFileFromLegacyId(nameFileLegacyId)).ReadAsync();
    var quarantine = await reopened.MutableDocument<AetheriaLegacyCatalogQuarantine>(AetheriaStateNode.LegacyCatalogQuarantineKey).ReadAsync();
    var catalogSurface = await reopened.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.CatalogSurfaceKey).ReadAsync();
    var eveCommandStatus = await reopened.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync();
    var operationsSurface = await reopened.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey).ReadAsync();
    var playerSettingsSurface = await reopened.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.PlayerSettingsSurfaceKey).ReadAsync();
    var advertisement = await reopened.MutableDocument<EveProviderAdvertisementState>(AetheriaStateNode.ProviderAdvertisementSurfaceKey).ReadAsync();
    var daemonProvider = await reopened.MutableDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement)
        .ReadAsync();
    var daemonHealth = await reopened.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth)
        .ReadAsync();
    var daemonCommandBoundary = await reopened.MutableDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary)
        .ReadAsync();
    var daemonFrame = await reopened.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
        .ReadAsync();
    var daemonGameSurface = await reopened.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)
        .ReadAsync();
    var runtimeSession = await reopened.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey("smoke-runtime")).ReadAsync();
    var playerSettings = await reopened.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync();
    var loadout = await reopened.MutableDocument<AetheriaLoadoutTemplate>(loadoutKey).ReadAsync();
    var hangar = await reopened.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey).ReadAsync();
    var runState = await reopened.MutableDocument<AetheriaRunState>(runKey).ReadAsync();
    var zoneState = await reopened.MutableDocument<AetheriaZoneState>(zoneKey).ReadAsync();
    var entitySnapshot = await reopened.MutableDocument<AetheriaEntitySnapshot>(entityKey).ReadAsync();

    if (world?.WorldId != "aetheria")
    {
        throw new InvalidOperationException("World state did not survive flush/reopen.");
    }

    if (hangar?.Revision != 10 ||
        hangar.Deployments.Length != 3 ||
        hangar.Deployments.Select(value => value.Mode).Distinct().Count() != 3 ||
        hangar.Deployments.Any(value => !value.Accepted) ||
        hangar.Ships.Any(value => value.Status != AetheriaHangarShipStatuses.Deployed))
        throw new InvalidOperationException("Canonical Hangar deployment did not survive flush/reopen.");

    var hangarSurface = AetheriaRuntimeHangarSurfaceBuilder.Build(
        hangar,
        "ship:smoke:aether-runner",
        AetheriaGameModes.Arena,
        now);
    var hangarComponents = Flatten(hangarSurface.Surface.Root).ToArray();
    if (hangarSurface.Surface.Id != AetheriaRuntimeHangarCommands.SurfaceId ||
        !hangarSurface.Commands.Any(command => command.Command == AetheriaRuntimeHangarCommands.Launch) ||
        !hangarComponents.Any(component => component.Id == "aetheria.hangar.preview.slot") ||
        !hangarComponents.Any(component => component.Props.TryGetValue("targetSurfaceId", out var target) &&
                                           target == AetheriaRuntimeInventoryPanelSurfaceBuilder.SurfaceId) ||
        hangarComponents.Count(component => component.Id.StartsWith("aetheria.hangar.bay.", StringComparison.Ordinal)) != 3)
        throw new InvalidOperationException("Hangar surface did not expose preview, ship bays, loadout editor, and launch boundary.");

    if (item?.Name != "Smoke Aether Drive")
    {
        throw new InvalidOperationException("Item definition did not survive flush/reopen.");
    }

    if (faction?.LegacyId != factionLegacyId)
    {
        throw new InvalidOperationException("Faction/corporation document did not survive flush/reopen.");
    }

    if (nameFile?.NameCount != 2)
    {
        throw new InvalidOperationException("Name file document did not survive flush/reopen.");
    }

    if (quarantine?.CatalogFingerprint != "smoke" || quarantine.NameFiles.Length != 1)
    {
        throw new InvalidOperationException("Legacy catalog quarantine did not survive flush/reopen.");
    }

    if (catalogSurface == null ||
        catalogSurface.Surface.Id != AetheriaEveSurfaceDocuments.CatalogSurfaceId)
    {
        throw new InvalidOperationException("Eve catalog surface did not survive flush/reopen.");
    }

    if (eveCommandStatus?.CommandsAccepted != 3 ||
        eveCommandStatus.CommandsRejected != 1 ||
        eveCommandStatus.AppliedCatalogRefreshes != 1 ||
        eveCommandStatus.AppliedPlayerSettingsCommands != 2 ||
        !eveCommandStatus.LastRejectedReason.Contains("Unknown typed Eve command kind", StringComparison.Ordinal) ||
        operationsSurface?.Surface.Id != AetheriaEveSurfaceDocuments.OperationsSurfaceId ||
        !operationsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.operations.eveCommandAcceptance") ||
        operationsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.operations.commitDrain") ||
        !operationsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.operations.runtimeSession"))
    {
        throw new InvalidOperationException(
            "Eve request acceptance status or operations surface did not survive flush/reopen: " +
            $"accepted={eveCommandStatus?.CommandsAccepted}, " +
            $"rejected={eveCommandStatus?.CommandsRejected}, " +
            $"catalog={eveCommandStatus?.AppliedCatalogRefreshes}, " +
            $"settings={eveCommandStatus?.AppliedPlayerSettingsCommands}, " +
            $"reason='{eveCommandStatus?.LastRejectedReason}', " +
            $"surface='{operationsSurface?.Surface.Id}', " +
            $"children=[{string.Join(", ", operationsSurface?.Surface.Root.Children.Select(child => child.Id) ?? [])}].");
    }

    if (advertisement?.ProviderId != AetheriaEveSurfaceDocuments.ProviderId ||
        advertisement.Surfaces.Count < 7 ||
        !advertisement.Surfaces.Any(surface => surface.SurfaceId == AetheriaEveSurfaceDocuments.CatalogSurfaceId) ||
        !advertisement.Surfaces.Any(surface => surface.SurfaceId == AetheriaEveSurfaceDocuments.OperationsSurfaceId) ||
        !advertisement.Surfaces.Any(surface => surface.SurfaceId == AetheriaEveSurfaceDocuments.PlayerSettingsSurfaceId) ||
        !advertisement.Surfaces.Any(surface =>
            surface.SurfaceId == AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId &&
            surface.RecordRef == AetheriaEveSurfaceDocuments.DaemonGameSurfaceKey) ||
        !advertisement.Surfaces.Any(surface =>
            surface.SurfaceId == AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId &&
            surface.RecordRef == AetheriaEveSurfaceDocuments.DaemonGameTuiSurfaceKey) ||
        !advertisement.Surfaces.Any(surface =>
            surface.SurfaceId == AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId &&
            surface.RecordRef == AetheriaEveSurfaceDocuments.DaemonEditorSurfaceKey) ||
        !advertisement.Surfaces.Any(surface =>
            surface.SurfaceId == AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId &&
            surface.RecordRef == AetheriaEveSurfaceDocuments.DaemonEditorTuiSurfaceKey) ||
        !advertisement.Schemas.Contains("aetheria.runtime_session.v1") ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.ProviderAdvertisement) ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.Frame) ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.SoaView) ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.Health) ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.CommandBoundary) ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.GameSurface) ||
        !advertisement.Schemas.Contains(AetheriaRuntimeDaemonSchemas.EditorSurface) ||
        !advertisement.Witnesses.Any(witness =>
            witness.Kind == "cultcache" &&
            witness.Reference == statePath) ||
        !advertisement.Witnesses.Any(witness =>
            witness.Kind == "cultmesh-record" &&
            witness.Reference == AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement.ToString()) ||
        !advertisement.Witnesses.Any(witness =>
            witness.Kind == "cultmesh-record" &&
            witness.Reference == AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString()) ||
        !advertisement.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands" &&
            command.Transport == "cultmesh") ||
        !advertisement.Commands.Any(command => command.Command == "aetheria.player_settings.graphics.show_asteroids.toggle") ||
        !advertisement.Schemas.Contains(AetheriaEveCommandBridge.CommandSchema))
    {
        throw new InvalidOperationException("Aetheria Eve provider advertisement did not survive flush/reopen.");
    }

    if (daemonProvider?.DaemonId != "smoke-daemon" ||
        daemonHealth?.FrameId != 7 ||
        daemonCommandBoundary?.BoundaryId != "aetheria.daemon.commands" ||
        !daemonCommandBoundary.Commands.Any(command => command.Kind == AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup) ||
        daemonFrame?.FrameId != 7 ||
        daemonFrame.SessionId != "smoke-session" ||
        daemonGameSurface?.Surface.Id != AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId ||
        daemonGameSurface.Surface.Root.Id != "aetheria.daemon.game.root")
    {
        throw new InvalidOperationException("Daemon Verse API documents did not survive flush/reopen as typed CultCache records.");
    }

    var reopenedPlayableWorld = daemonGameSurface.Surface.Root.Children.Single(component =>
        component.Id == "aetheria.daemon.game.world");
    var expectedReopenedPlayableWorldProps = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cameraRig"] = "perspective.entity-forward-follow.v1",
        ["cameraTargetEntityId"] = entityKey.ToString(),
        ["cameraDistance"] = "30",
        ["cameraVerticalFieldOfViewDegrees"] = "60",
        ["cameraTargetScreenX"] = "0.64",
        ["cameraTargetScreenY"] = "0.19",
        ["cameraPositionDamping"] = "0",
        ["cameraNearClipPlane"] = "1",
        ["cameraFarClipPlane"] = "4096",
        ["ambientLightColor"] = "0.2,0.2,0.2",
        ["ambientLightIntensity"] = "1.46"
    };
    if (expectedReopenedPlayableWorldProps.Any(expected =>
            !reopenedPlayableWorld.Props.TryGetValue(expected.Key, out var actual) ||
            !string.Equals(actual, expected.Value, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("Playable world camera and environment contract did not survive flush/reopen.");
    }

    if (runtimeSession?.RuntimeId != "smoke-runtime" ||
        runtimeSession.Role != "state-smoke" ||
        runtimeSession.Status != "running")
    {
        throw new InvalidOperationException("Runtime session document did not survive flush/reopen.");
    }

    if (playerSettings?.ActiveRunKey != runKey.ToString() ||
        playerSettings.PlayerName != "Smoke Pilot" ||
        !playerSettings.TutorialPassed ||
        playerSettings.StoryFileHashes.Length != 1 ||
        playerSettings.Gameplay.SignificantDigits != 5 ||
        playerSettings.Graphics.NebulaQuality != "High" ||
        playerSettings.Graphics.ShowAsteroidsInMinimap ||
        playerSettings.Input.BindingOverrides.Length != 1 ||
        playerSettings.Input.ActionBarInputs.Length != 2)
    {
        throw new InvalidOperationException("Player settings did not survive flush/reopen.");
    }

    if (playerSettingsSurface?.Surface.Id != AetheriaEveSurfaceDocuments.PlayerSettingsSurfaceId ||
        !playerSettingsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.playerSettings.gameplay") ||
        !playerSettingsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.playerSettings.graphics"))
    {
        throw new InvalidOperationException("Player settings Eve surface did not survive flush/reopen.");
    }

    if (loadout?.RootEntity.Hull.ItemKey != AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString() ||
        loadout.RootEntity.Equipment.Length != 1 ||
        loadout.RootEntity.Equipment[0].Rotation != "Clockwise" ||
        loadout.RootEntity.Equipment[0].Item.Enabled ||
        !loadout.RootEntity.Equipment[0].Item.OverrideShutdown ||
        loadout.RootEntity.WeaponGroups.Length != 1)
    {
        throw new InvalidOperationException("Loadout template did not survive flush/reopen.");
    }

    if (runState?.RunId != "smoke" ||
        runState.GenerationSeed != 424242 ||
        runState.CurrentEntityKey != entityKey.ToString() ||
        runState.ZoneKeys.Length != 1)
    {
        throw new InvalidOperationException("Run state did not survive flush/reopen.");
    }

    if (zoneState?.EntityKeys.Length != 1 ||
        zoneState.EntityKeys[0] != entityKey.ToString() ||
        zoneState.Position.X != 4.0 ||
        zoneState.OwnerFactionIndex != 0 ||
        zoneState.Orbits.Length != 1 ||
        zoneState.Orbits[0].OrbitKey != "smoke:orbit" ||
        zoneState.Bodies.Length != 1 ||
        zoneState.Bodies[0].Kind != "asteroid_belt" ||
        zoneState.Bodies[0].Resources.Length != 1 ||
        zoneState.Bodies[0].Asteroids.Length != 1 ||
        zoneState.Bodies[0].Asteroids[0].Damage != 1.25 ||
        zoneState.Bodies[0].Asteroids[0].RespawnTimer != 6.5 ||
        zoneState.Bodies[0].Asteroids[0].MiningAccumulators.Length != 1 ||
        zoneState.Bodies[0].Asteroids[0].MiningAccumulators[0].MinerEntityKey != entityKey.ToString() ||
        zoneState.Bodies[0].Asteroids[0].MiningAccumulators[0].Amount != 0.75)
    {
        throw new InvalidOperationException("Zone state did not survive flush/reopen.");
    }

    if (entitySnapshot?.Kind != "ship" ||
        entitySnapshot.LookDirection.X != 0.8 ||
        entitySnapshot.LookDirection.Y != 0.6 ||
        entitySnapshot.HelmInput.X != -0.25 ||
        entitySnapshot.HelmInput.Y != 0.75 ||
        entitySnapshot.Equipment.Length != 1 ||
        entitySnapshot.Equipment[0].Quality != 0.9 ||
        entitySnapshot.Equipment[0].Durability != 0.8 ||
        entitySnapshot.Equipment[0].Quantity != 1 ||
        entitySnapshot.Equipment[0].Rotation != "CounterClockwise" ||
        entitySnapshot.Equipment[0].Enabled ||
        !entitySnapshot.Equipment[0].OverrideShutdown ||
        entitySnapshot.CargoContents.Length != 1 ||
        entitySnapshot.CargoContents[0].Items[0].Item.Quantity != 7 ||
        entitySnapshot.CargoContents[0].Items[0].Rotation != "Half" ||
        entitySnapshot.DockingBayAssignments.Length != 1 ||
        entitySnapshot.DockingBayAssignments[0] != -1 ||
        entitySnapshot.Visibility != 12.75 ||
        entitySnapshot.VisibilitySourceCount != 3 ||
        entitySnapshot.Contacts.Length != 1 ||
        entitySnapshot.Contacts[0].TargetEntityKey != entityKey.ToString() ||
        entitySnapshot.Contacts[0].InfoGathered != 0.85 ||
        !entitySnapshot.Contacts[0].Hostile ||
        !entitySnapshot.Contacts[0].Visible ||
        entitySnapshot.WeaponGroups.Length != 1 ||
        entitySnapshot.WeaponStates.Length != 1 ||
        entitySnapshot.WeaponStates[0].BehaviorKind != "LockWeapon" ||
        entitySnapshot.WeaponStates[0].LockProgress != 0.65 ||
        entitySnapshot.WeaponStates[0].LockTargetEntityKey != entityKey.ToString() ||
        entitySnapshot.BehaviorStates.Length != 11 ||
        entitySnapshot.BehaviorStates[0].BehaviorKind != "AetherDrive" ||
        entitySnapshot.BehaviorStates[0].AetherDriveAxisX != 0.5 ||
        entitySnapshot.BehaviorStates[0].AetherDriveRpmY != 900.0 ||
        entitySnapshot.BehaviorStates[0].AetherDriveMaximumRpm != 2400.0 ||
        entitySnapshot.BehaviorStates[1].BehaviorKind != "ResourceScanner" ||
        entitySnapshot.BehaviorStates[1].ResourceScannerTargetBodyKey != "aetheria.body:smoke:body" ||
        entitySnapshot.BehaviorStates[1].ResourceScannerAsteroidIndex != 2 ||
        entitySnapshot.BehaviorStates[1].ResourceScannerScanTime != 1.25 ||
        entitySnapshot.BehaviorStates[1].ResourceScannerScanDuration != 3.5 ||
        entitySnapshot.BehaviorStates[2].BehaviorKind != "MiningTool" ||
        entitySnapshot.BehaviorStates[2].MiningToolAsteroidBeltKey != "aetheria.body:smoke:body" ||
        entitySnapshot.BehaviorStates[2].MiningToolAsteroidIndex != 3 ||
        entitySnapshot.BehaviorStates[2].MiningToolRange != 275.0 ||
        entitySnapshot.BehaviorStates[3].BehaviorKind != "Thruster" ||
        entitySnapshot.BehaviorStates[3].ThrusterAxis != 0.8 ||
        entitySnapshot.BehaviorStates[3].ThrusterThrust != 125.0 ||
        entitySnapshot.BehaviorStates[3].ThrusterTorque != -0.4 ||
        entitySnapshot.BehaviorStates[4].BehaviorKind != "Shield" ||
        entitySnapshot.BehaviorStates[4].ShieldEfficiency != 0.7 ||
        entitySnapshot.BehaviorStates[4].ShieldEnergyUsage != 1.4 ||
        entitySnapshot.BehaviorStates[5].BehaviorKind != "VelocityLimit" ||
        entitySnapshot.BehaviorStates[5].VelocityLimit != 42.0 ||
        entitySnapshot.BehaviorStates[6].BehaviorKind != "Thermotoggle" ||
        entitySnapshot.BehaviorStates[6].ThermotoggleTargetTemperature != 315.5 ||
        entitySnapshot.BehaviorStates[7].BehaviorKind != "Switch" ||
        !entitySnapshot.BehaviorStates[7].SwitchActivated ||
        entitySnapshot.BehaviorStates[8].BehaviorKind != "Trigger" ||
        !entitySnapshot.BehaviorStates[8].TriggerPulled ||
        entitySnapshot.BehaviorStates[9].BehaviorKind != "StatModifier" ||
        !entitySnapshot.BehaviorStates[9].StatModifierApplied ||
        !entitySnapshot.BehaviorStates[9].StatModifierExecuted ||
        entitySnapshot.BehaviorStates[9].StatModifierTargetStatCount != 2 ||
        entitySnapshot.BehaviorStates[10].BehaviorKind != "TurretController" ||
        entitySnapshot.BehaviorStates[10].TurretControllerWeaponCount != 2 ||
        entitySnapshot.BehaviorStates[10].TurretControllerShotSpeed != 80.0 ||
        !entitySnapshot.BehaviorStates[10].TurretControllerPredictShots ||
        entitySnapshot.StatGrids.Length != 1 ||
        entitySnapshot.StatGrids[0].Values.Length != 4)
    {
        throw new InvalidOperationException("Entity snapshot did not survive flush/reopen.");
    }
}

var capabilityRun = new AetheriaRuntimeRunCheckpointCommit
{
    RunId = "capability-smoke",
    CurrentZoneIndex = 0,
    CurrentEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey("capability-smoke", 0, 7),
    Zones =
    [
        new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities =
            [
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 7,
                    EntityId = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey("capability-smoke", 0, 7),
                    WeaponGroups = [new[] { 2 }]
                }
            ]
        }
    ]
};
var capability = AetheriaRuntimeInputCapabilityDocument.FromFrame(new AetheriaRuntimeDaemonFrameDocument
{
    FrameId = 12,
    Run = capabilityRun
}).ToEveDocument();
var advertisedActionIds = capability.Actions.Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal);
if (!advertisedActionIds.Contains("weapon-group.0.fire") ||
    capability.DefaultProfiles.SelectMany(profile => profile.Bindings)
        .Any(binding => !advertisedActionIds.Contains(binding.ActionId)))
{
    throw new InvalidOperationException("Portable Eve input capability did not preserve weapon groups and valid default bindings.");
}

await ProveDirectSoaPublicationPipeline();

Console.WriteLine($"Aetheria typed state smoke passed: {statePath}");

static async Task ProveDirectSoaPublicationPipeline()
{
    using var cache = new CultCache();
    var frame = new AetheriaRuntimeDaemonFrameDocument
    {
        DaemonId = "aetheria-daemon",
        SessionId = "soa-pipeline-smoke",
        FrameId = 41,
        SimulationTimeSeconds = 4,
        Run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "soa-pipeline-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey("soa-pipeline-smoke", 0, 12),
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Entities =
                    [
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 12,
                            EntityId = "ship:soa-witness",
                            Name = "SoA Witness",
                            Kind = "ship",
                            FactionKey = "faction:smoke",
                            PositionX = 1.25,
                            PositionY = -2.5,
                            PositionZ = 7.75,
                            IsActive = true
                        }
                    ],
                    DroppedPickups =
                    [
                        new AetheriaRuntimeDroppedPickupCommit
                        {
                            PickupIndex = 3,
                            PositionX = 4,
                            PositionY = 5,
                            PositionZ = 6
                        }
                    ],
                    PhysicalPayloads =
                    [
                        new AetheriaRuntimePhysicalPayloadCommit
                        {
                            PayloadId = "mine:soa-witness",
                            PayloadKind = "mine",
                            FactionKey = "faction:smoke",
                            PositionX = 9,
                            PositionZ = 11,
                            Radius = 2,
                            Active = true,
                            Stationary = true
                        }
                    ],
                    Bodies =
                    [
                        new AetheriaRuntimeBodySnapshotCommit
                        {
                            BodyKey = "body:soa-sun",
                            Name = "SoA Sun",
                            Kind = "sun",
                            Mass = 100,
                            BodyRadiusMultiplier = 1,
                            GravityInfluenceCenterX = 20,
                            GravityInfluenceCenterZ = 30
                        },
                        new AetheriaRuntimeBodySnapshotCommit
                        {
                            BodyKey = "body:soa-belt",
                            Name = "SoA Belt",
                            Kind = "asteroid_belt",
                            GravityInfluenceCenterX = 40,
                            GravityInfluenceCenterZ = 50,
                            Asteroids =
                            [
                                new AetheriaRuntimeAsteroidCommit
                                {
                                    Distance = 5,
                                    Phase = 0,
                                    Size = 2,
                                    RotationSpeed = 0.25
                                }
                            ]
                        }
                    ]
                }
            ]
        }
    };

    using var liveBodies = new CultMeshNetworkBodyStore();
    using var publisher = new AetheriaRuntimeDaemonSoaFramePublisher(liveBodies, producerEpoch: 9);
    var built = publisher.BuildCurrentZoneEntities(frame);
    var payloadIdentity = built.View.Identities.SingleOrDefault(identity =>
        string.Equals(identity.EntityId, "soa-pipeline-smoke:zone:0:physical-payload:mine:soa-witness", StringComparison.Ordinal));
    var pickupIdentity = built.View.Identities.Single(identity =>
        string.Equals(identity.EntityId, "pickup:0:3", StringComparison.Ordinal));
    var sunIdentity = built.View.Identities.Single(identity =>
        string.Equals(identity.Kind, "celestial.sun", StringComparison.Ordinal));
    var asteroidIdentity = built.View.Identities.Single(identity =>
        string.Equals(identity.Kind, "celestial.asteroid", StringComparison.Ordinal));
    if (payloadIdentity == null || !string.Equals(payloadIdentity.AssetRef, "prefab.entity.mine", StringComparison.Ordinal) ||
        !string.Equals(payloadIdentity.Kind, "physical-payload", StringComparison.Ordinal) || payloadIdentity.Selectable ||
        payloadIdentity.EntityIndex >= -1 || pickupIdentity.EntityIndex >= -1 ||
        payloadIdentity.EntityIndex == pickupIdentity.EntityIndex ||
        sunIdentity.AssetRef != "prefab.body.sun" || asteroidIdentity.AssetRef != "prefab.body.asteroid" ||
        sunIdentity.Selectable || asteroidIdentity.Selectable ||
        new[] { payloadIdentity.EntityIndex, pickupIdentity.EntityIndex, sunIdentity.EntityIndex, asteroidIdentity.EntityIndex }
            .Distinct().Count() != 4 ||
        built.View.Columns.Any(column => column.ElementCount != 5))
        throw new InvalidOperationException("Physical payload did not enter the authoritative SoA generation with its provider asset identity.");
    if (cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest) != null)
        throw new InvalidOperationException("SoA view became visible before its body publication.");

    var published = await publisher.PublishAsync(built);
    if (cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest) != null)
        throw new InvalidOperationException("Building a CultMesh body publication made the Eve view visible before commit.");
    if (cache.GetAll<CultMeshCdnArtifactManifest>().Any() || cache.GetAll<CultMeshCdnArtifactChunk>().Any())
        throw new InvalidOperationException("Hot SoA body bytes were retained as snapshot-addressable CDN state.");

    frame.FrameId++;
    var nextBuilt = publisher.BuildCurrentZoneEntities(frame);
    var nextPayloadIdentity = nextBuilt.View.Identities.Single(identity => identity.EntityId == payloadIdentity.EntityId);
    var nextPickupIdentity = nextBuilt.View.Identities.Single(identity => identity.EntityId == pickupIdentity.EntityId);
    var nextSunIdentity = nextBuilt.View.Identities.Single(identity => identity.EntityId == sunIdentity.EntityId);
    var nextAsteroidIdentity = nextBuilt.View.Identities.Single(identity => identity.EntityId == asteroidIdentity.EntityId);
    if (nextPayloadIdentity.EntityIndex != payloadIdentity.EntityIndex ||
        nextPickupIdentity.EntityIndex != pickupIdentity.EntityIndex ||
        nextSunIdentity.EntityIndex != sunIdentity.EntityIndex ||
        nextAsteroidIdentity.EntityIndex != asteroidIdentity.EntityIndex)
        throw new InvalidOperationException("Synthetic SoA identity changed across immutable generations in one producer epoch.");
    var next = await publisher.PublishAsync(nextBuilt);
    if (published.Body.RecordKey.Equals(next.Body.RecordKey))
        throw new InvalidOperationException("Distinct SoA generations collided on one CultMesh body publication key.");

    await cache.UpsertAsync(
        published.Body,
        new CultRecordHandle<CultMeshBodyPublicationDocument>(published.Body.RecordKey));
    await cache.UpsertAsync(
        next.Body,
        new CultRecordHandle<CultMeshBodyPublicationDocument>(next.Body.RecordKey));
    await cache.UpsertAsync(
        next.Body,
        new CultRecordHandle<CultMeshBodyPublicationDocument>(
            CultMeshBodyPublicationDocument.CreateLatestRecordKey(next.Body.BodyId)));
    if (cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest) != null)
        throw new InvalidOperationException("Publishing the body record alone made the Eve view visible.");
    await cache.UpsertAsync(
        published.View,
        new CultRecordHandle<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest));

    var bodyHandle = new CultMeshBodyPublicationHandle(
        published.View.Buffers[0].BufferId,
        published.View.ProducerEpoch,
        published.View.Sequence);
    var body = cache.Get<CultMeshBodyPublicationDocument>(bodyHandle.RecordKey)
        ?? throw new InvalidOperationException("Typed CultMesh body publication did not roundtrip through CultCache.");
    bodyHandle.Validate(body);
    var latest = cache.Get<CultMeshBodyPublicationDocument>(
        CultMeshBodyPublicationDocument.CreateLatestRecordKey(body.BodyId));
    if (latest == null || latest.Sequence != next.Body.Sequence || body.Sequence == latest.Sequence)
        throw new InvalidOperationException("The view generation was substituted by the latest CultMesh body publication.");
    var view = cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest)
        ?? throw new InvalidOperationException("Typed Eve SoA view did not become visible after body publication.");
    var localRepresentation = body.Representations.SingleOrDefault(candidate =>
        candidate.TransportKind == CultMeshBodyTransportKind.SharedMemory);
    var networkRepresentation = body.Representations.SingleOrDefault(candidate =>
        candidate.TransportKind == CultMeshBodyTransportKind.Network);
    if (localRepresentation == null || networkRepresentation == null ||
        body.Representations.Any(candidate => candidate.TransportKind == CultMeshBodyTransportKind.SharedFileMapping))
        throw new InvalidOperationException("Direct SoA publication did not expose shared memory plus direct network representations.");

    var request = new CultMeshBodyValidationRequest
    {
        BodyId = body.BodyId,
        SchemaId = body.SchemaId,
        LayoutVersion = body.LayoutVersion,
        ProducerEpoch = body.ProducerEpoch,
        Sequence = body.Sequence,
        Capacity = body.Capacity,
        AccessMode = CultMeshBodyAccessMode.ReadOnly,
        NowUtc = DateTimeOffset.UtcNow
    };
    using var localLease = new CultMeshBodyPublicationResolver(new CultMeshBodyTransportService(
        new ICultMeshBodyTransportAdapter[]
        {
            new CultMeshSharedMemoryBodyAdapter(),
            new CultMeshNetworkBodyAdapter(descriptor => ReadLiveBody(liveBodies, descriptor))
        },
        (producerId, _) => producerId == AetheriaRuntimeDaemonSoaFramePublisher.ProducerId))
        .ResolveReadOnly(body, request);
    using var networkLease = new CultMeshBodyPublicationResolver(new CultMeshBodyTransportService(
        new ICultMeshBodyTransportAdapter[]
        {
            new CultMeshNetworkBodyAdapter(descriptor => ReadLiveBody(liveBodies, descriptor))
        },
        (producerId, _) => producerId == AetheriaRuntimeDaemonSoaFramePublisher.ProducerId))
        .ResolveReadOnly(body, request);

    var localEntityIndex = ReadInt32(view, localLease, "entity.index", 0);
    var networkEntityIndex = ReadInt32(view, networkLease, "entity.index", 0);
    var localPosition = ReadFloat3(view, localLease, "transform.position", 0);
    var networkPosition = ReadFloat3(view, networkLease, "transform.position", 0);
    var localPayloadEntityIndex = ReadInt32(view, localLease, "entity.index", 2);
    var networkPayloadEntityIndex = ReadInt32(view, networkLease, "entity.index", 2);
    var localPayloadPosition = ReadFloat3(view, localLease, "transform.position", 2);
    var networkPayloadPosition = ReadFloat3(view, networkLease, "transform.position", 2);
    var localSunPosition = ReadFloat3(view, localLease, "transform.position", 3);
    var networkSunPosition = ReadFloat3(view, networkLease, "transform.position", 3);
    var localAsteroidPosition = ReadFloat3(view, localLease, "transform.position", 4);
    var networkAsteroidPosition = ReadFloat3(view, networkLease, "transform.position", 4);
    var identity = view.Identities.Single(candidate => candidate.Index == localEntityIndex);
    if (localLease.TransportKind != CultMeshBodyTransportKind.SharedMemory ||
        networkLease.TransportKind != CultMeshBodyTransportKind.Network ||
        localEntityIndex != 12 || networkEntityIndex != localEntityIndex ||
        localPosition != (1.25f, -2.5f, 7.75f) || networkPosition != localPosition ||
        localPayloadEntityIndex != payloadIdentity.EntityIndex ||
        networkPayloadEntityIndex != localPayloadEntityIndex ||
        localPayloadPosition != (9f, 0f, 11f) || networkPayloadPosition != localPayloadPosition ||
        localSunPosition.X != 20f || localSunPosition.Z != 30f || networkSunPosition != localSunPosition ||
        localAsteroidPosition.X != 45f || localAsteroidPosition.Z != 50f ||
        networkAsteroidPosition != localAsteroidPosition ||
        identity.EntityId != "ship:soa-witness" || identity.Label != "SoA Witness")
        throw new InvalidOperationException("Local and network SoA views were not logically equivalent.");

    frame.Run.Zones[0].PhysicalPayloads[0].Active = false;
    var withoutPayload = publisher.BuildCurrentZoneEntities(frame);
    if (withoutPayload.View.Identities.Any(candidate => candidate.EntityId == payloadIdentity.EntityId) ||
        withoutPayload.View.Identities.Single(candidate => candidate.EntityId == pickupIdentity.EntityId).EntityIndex != pickupIdentity.EntityIndex)
        throw new InvalidOperationException("Inactive physical payload remained in SoA or disturbed a surviving synthetic identity.");
}

static byte[] ReadLiveBody(CultMeshNetworkBodyStore bodies, CultMeshBodyDescriptor descriptor)
{
    var request = new CultMeshBodyReadRequestMessage
    {
        MessageId = Guid.NewGuid().ToString("N"),
        CapabilityToken = descriptor.CapabilityToken,
        BodyId = descriptor.BodyId,
        BodySchemaId = descriptor.SchemaId,
        LayoutVersion = descriptor.LayoutVersion,
        ProducerEpoch = descriptor.ProducerEpoch,
        Sequence = descriptor.Sequence,
        ExpectedSizeBytes = descriptor.ByteSize,
        SemanticHash = descriptor.SemanticHash
    };
    if (!bodies.TryRead(request, DateTimeOffset.UtcNow, out _, out var body))
        throw new InvalidOperationException("Ephemeral SoA network generation could not be resolved.");
    return body;
}

static IEnumerable<AetheriaRuntimeSurfaceComponent> Flatten(AetheriaRuntimeSurfaceComponent component)
{
    yield return component;
    foreach (var child in component.Children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>())
    foreach (var descendant in Flatten(child))
        yield return descendant;
}

static int ReadInt32(EveEntitySoaViewDocument view, ICultMeshBodyReadLease lease, string semantic, int row)
{
    var column = view.Columns.Single(candidate => candidate.Semantic == semantic);
    var buffer = view.Buffers.Single(candidate => candidate.BufferId == column.BufferId);
    return lease.ReadInt32(buffer.ByteOffset + column.ByteOffset + (long)row * column.ElementStride);
}

static (float X, float Y, float Z) ReadFloat3(
    EveEntitySoaViewDocument view,
    ICultMeshBodyReadLease lease,
    string semantic,
    int row)
{
    var column = view.Columns.Single(candidate => candidate.Semantic == semantic);
    var buffer = view.Buffers.Single(candidate => candidate.BufferId == column.BufferId);
    var offset = buffer.ByteOffset + column.ByteOffset + (long)row * column.ElementStride;
    return (lease.ReadSingle(offset), lease.ReadSingle(offset + 4), lease.ReadSingle(offset + 8));
}
