using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;
using GameCult.Caching;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
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
    await node.MutableDocument<EveProviderAdvertisementState>(AetheriaStateNode.ProviderAdvertisementSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildProviderAdvertisement(verseHostSettings, statePath, now));
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

    await node.MutableDocument<AetheriaEntitySnapshot>(entityKey).ReplaceAsync(new AetheriaEntitySnapshot
    {
        Name = "Smoke Aether Runner",
        Kind = "ship",
        Position = new AetheriaVector3 { X = 12.5, Y = 0.0, Z = -3.25 },
        Direction = new AetheriaVector2 { X = 0.0, Y = 1.0 },
        FactionKey = factionKey.ToString(),
        HullItemKey = itemKey.ToString(),
        Equipment =
        [
            new AetheriaEntityItemSlot
            {
                Position = new AetheriaGridCoord { X = 0, Y = 0 },
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
    var runState = await reopened.MutableDocument<AetheriaRunState>(runKey).ReadAsync();
    var zoneState = await reopened.MutableDocument<AetheriaZoneState>(zoneKey).ReadAsync();
    var entitySnapshot = await reopened.MutableDocument<AetheriaEntitySnapshot>(entityKey).ReadAsync();

    if (world?.WorldId != "aetheria")
    {
        throw new InvalidOperationException("World state did not survive flush/reopen.");
    }

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
        entitySnapshot.Equipment.Length != 1 ||
        entitySnapshot.Equipment[0].Quality != 0.9 ||
        entitySnapshot.Equipment[0].Durability != 0.8 ||
        entitySnapshot.Equipment[0].Quantity != 1 ||
        entitySnapshot.Equipment[0].Enabled ||
        !entitySnapshot.Equipment[0].OverrideShutdown ||
        entitySnapshot.CargoContents.Length != 1 ||
        entitySnapshot.CargoContents[0].Items[0].Item.Quantity != 7 ||
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
                    ]
                }
            ]
        }
    };

    using var publisher = new AetheriaRuntimeDaemonSoaFramePublisher(cache, producerEpoch: 9);
    var built = publisher.BuildCurrentZoneEntities(frame);
    if (cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest) != null)
        throw new InvalidOperationException("SoA view became visible before its body publication.");

    var published = await publisher.PublishAsync(built);
    if (cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest) != null)
        throw new InvalidOperationException("Building a CultMesh body publication made the Eve view visible before commit.");

    await cache.UpsertAsync(
        published.Body,
        new CultRecordHandle<CultMeshBodyPublicationDocument>(published.Body.RecordKey));
    if (cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest) != null)
        throw new InvalidOperationException("Publishing the body record alone made the Eve view visible.");
    await cache.UpsertAsync(
        published.View,
        new CultRecordHandle<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest));

    var body = cache.Get<CultMeshBodyPublicationDocument>(published.Body.RecordKey)
        ?? throw new InvalidOperationException("Typed CultMesh body publication did not roundtrip through CultCache.");
    var view = cache.Get<EveEntitySoaViewDocument>(AetheriaRuntimeVerseRecordKeys.EveEntitySoaViewLatest)
        ?? throw new InvalidOperationException("Typed Eve SoA view did not become visible after body publication.");
    if (body.PreferredLocal.TransportKind != CultMeshBodyTransportKind.SharedMemory ||
        body.NetworkFallback.TransportKind != CultMeshBodyTransportKind.Network ||
        body.PreferredLocal.TransportKind == CultMeshBodyTransportKind.SharedFileMapping ||
        body.NetworkFallback.TransportKind == CultMeshBodyTransportKind.SharedFileMapping)
        throw new InvalidOperationException("Direct SoA publication used an Aetheria file-mapping convention.");

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
            new CultMeshNetworkBodyAdapter(new CultMeshNetworkBodyResolver(cache).CreateFetchDelegate())
        },
        (producerId, _) => producerId == AetheriaRuntimeDaemonSoaFramePublisher.ProducerId))
        .ResolveReadOnly(body, request);
    using var networkLease = new CultMeshBodyPublicationResolver(new CultMeshBodyTransportService(
        new ICultMeshBodyTransportAdapter[]
        {
            new CultMeshNetworkBodyAdapter(new CultMeshNetworkBodyResolver(cache).CreateFetchDelegate())
        },
        (producerId, _) => producerId == AetheriaRuntimeDaemonSoaFramePublisher.ProducerId))
        .ResolveReadOnly(body, request);

    var localEntityIndex = ReadInt32(view, localLease, "entity.index", 0);
    var networkEntityIndex = ReadInt32(view, networkLease, "entity.index", 0);
    var localPosition = ReadFloat3(view, localLease, "transform.position", 0);
    var networkPosition = ReadFloat3(view, networkLease, "transform.position", 0);
    var identity = view.Identities.Single(candidate => candidate.Index == localEntityIndex);
    if (localLease.TransportKind != CultMeshBodyTransportKind.SharedMemory ||
        networkLease.TransportKind != CultMeshBodyTransportKind.Network ||
        localEntityIndex != 12 || networkEntityIndex != localEntityIndex ||
        localPosition != (1.25f, -2.5f, 7.75f) || networkPosition != localPosition ||
        identity.EntityId != "ship:soa-witness" || identity.Label != "SoA Witness")
        throw new InvalidOperationException("Local and network SoA views were not logically equivalent.");
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
