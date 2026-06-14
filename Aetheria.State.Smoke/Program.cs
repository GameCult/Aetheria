using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;
using GameCult.Caching;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;

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

await using (var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-smoke"))
{
    await node.PutWorldAsync(new AetheriaWorldState
    {
        Name = "Aetheria",
        WorldId = "aetheria",
        SchemaEpoch = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    });

    await node.PutLegacyItemDefinitionAsync(
        new AetheriaItemDefinition
        {
            Name = "Smoke Aether Drive",
            Category = "ship-module",
            LegacyId = itemLegacyId,
            Description = "Typed CultCache smoke document for the rebuild spine.",
            Mass = 12.5,
            Volume = 4.0,
            Tags = ["smoke", "state-spine"]
        });

    await node.PutMigrationLedgerAsync(new AetheriaMigrationLedger
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

    await node.PutLegacyCatalogQuarantineAsync(new AetheriaLegacyCatalogQuarantine
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

    await node.PutLegacyCorporationAsync(new AetheriaCorporation
    {
        Name = "Smoke Faction",
        LegacyId = factionLegacyId,
        Description = "Typed faction/corporation document for legacy catalog migration smoke."
    });

    await node.PutLegacyNameFileAsync(new AetheriaNameFile
    {
        Name = "Smoke Names",
        LegacyId = nameFileLegacyId,
        NameCount = 2,
        SampleNames = ["Ada", "Grace"]
    });

    await node.PutCatalogSurfaceAsync(
        AetheriaCatalogSurfaceProjector.Build(node.ReadCatalogSnapshot(), now));

    var drainStatus = new AetheriaRuntimeCommitDrainStatus
    {
        RuntimeId = "smoke-runtime",
        StatePath = statePath,
        LastPollAtUtc = now,
        LastAppliedAtUtc = now,
        PendingBeforeApply = 1,
        CommandsApplied = 1,
        AppliedPlayerSettings = 1,
        Status = "ok"
    };
    await node.PutRuntimeCommitDrainStatusAsync(drainStatus);
    await node.PutOperationsSurfaceAsync(AetheriaOperationsSurfaceProjector.Build(drainStatus));
    await node.PutProviderAdvertisementAsync(AetheriaProviderAdvertisementProjector.Build(statePath, now));
    await node.PutRuntimeSessionAsync(new AetheriaRuntimeSession
    {
        RuntimeId = "smoke-runtime",
        Role = "state-smoke",
        StartedAtUtc = now,
        LastSeenAtUtc = now,
        Status = "running"
    });
    await node.PutOperationsSurfaceAsync(AetheriaOperationsSurfaceProjector.Build(
        drainStatus,
        runtimeSession: await node.GetRuntimeSessionAsync("smoke-runtime")));

    AetheriaRuntimeEveCommandLog.QueueCommand(
        statePath,
        new EveSurfaceCommandRequest(
            AetheriaProviderAdvertisementProjector.ProviderId,
            AetheriaCatalogSurfaceProjector.SurfaceId,
            "aetheria.catalog.refresh",
            new Dictionary<string, string> { ["source"] = "state-smoke" },
            DateTimeOffset.UtcNow,
            "aetheria-state-smoke"));
    AetheriaRuntimeEveCommandLog.QueueCommand(
        statePath,
        new EveSurfaceCommandRequest(
            AetheriaProviderAdvertisementProjector.ProviderId,
            AetheriaCatalogSurfaceProjector.SurfaceId,
            "aetheria.catalog.unknown",
            new Dictionary<string, string> { ["source"] = "state-smoke" },
            DateTimeOffset.UtcNow,
            "aetheria-state-smoke"));
    var eveCommandReport = await AetheriaEveCommandBridge.ApplyPendingAsync(node);
    var eveCommandStatus = new AetheriaEveCommandDrainStatus
    {
        RuntimeId = "smoke-runtime",
        StatePath = statePath,
        LastPollAtUtc = now,
        LastAcceptedAtUtc = now,
        PendingBeforeApply = 2,
        CommandsAccepted = eveCommandReport.AcceptedPaths.Length,
        CommandsRejected = eveCommandReport.RejectedCommands,
        AppliedCatalogRefreshes = eveCommandReport.AppliedCatalogRefreshes,
        AppliedOperationsRefreshes = eveCommandReport.AppliedOperationsRefreshes,
        LastRejectedCommand = eveCommandReport.LastRejectedCommand,
        LastRejectedReason = eveCommandReport.LastRejectedReason,
        Status = eveCommandReport.RejectedCommands > 0 ? "rejected" : "ok"
    };
    await node.PutEveCommandDrainStatusAsync(eveCommandStatus);
    await node.PutOperationsSurfaceAsync(AetheriaOperationsSurfaceProjector.Build(
        drainStatus,
        eveCommandStatus,
        await node.GetRuntimeSessionAsync("smoke-runtime")));

    await node.PutLoadoutTemplateAsync(loadoutKey, new AetheriaLoadoutTemplate
    {
        Name = "Smoke Aether Runner",
        OwnerPlayerKey = "player:smoke",
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        RootEntity = new AetheriaEntityLoadout
        {
            Name = "Smoke Aether Runner",
            Kind = "ship",
            FactionKey = AetheriaCatalogKeys.CorporationFromLegacyId(factionLegacyId).ToString(),
            Hull = new AetheriaLoadoutItem
            {
                ItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString(),
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
                        ItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString(),
                        Quality = 0.95,
                        Durability = 0.8
                    }
                }
            ],
            WeaponGroups = [[0]]
        }
    });

    await node.PutEntitySnapshotAsync(entityKey, new AetheriaEntitySnapshot
    {
        Name = "Smoke Aether Runner",
        Kind = "ship",
        Position = new AetheriaVector3 { X = 12.5, Y = 0.0, Z = -3.25 },
        Direction = new AetheriaVector2 { X = 0.0, Y = 1.0 },
        FactionKey = AetheriaCatalogKeys.CorporationFromLegacyId(factionLegacyId).ToString(),
        HullItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString(),
        Equipment =
        [
            new AetheriaEntityItemSlot
            {
                Position = new AetheriaGridCoord { X = 0, Y = 0 },
                ItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString(),
                Quality = 0.9,
                Durability = 0.8,
                Quantity = 1
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
                            ItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString(),
                            Quantity = 7
                        }
                    }
                ]
            }
        ],
        DockingBayAssignments = [-1],
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
                ResourceScannerTargetBodyId = "smoke:body",
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
                MiningToolAsteroidBeltId = "smoke:body",
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

    await node.PutZoneStateAsync(zoneKey, new AetheriaZoneState
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
                OrbitId = "smoke:orbit",
                ParentId = "smoke:parent-orbit",
                Distance = 100,
                Phase = 0.25,
                FixedPosition = new AetheriaVector2 { X = 5, Y = -6 }
            }
        ],
        Bodies =
        [
            new AetheriaBodySnapshot
            {
                BodyId = "smoke:body",
                Kind = "asteroid_belt",
                Name = "Smoke Belt",
                OrbitId = "smoke:orbit",
                Mass = 42,
                Resources =
                [
                    new AetheriaBodyResource
                    {
                        ItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString(),
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
                        RotationSpeed = 0.5
                    }
                ]
            }
        ]
    });

    await node.PutRunStateAsync(runKey, new AetheriaRunState
    {
        RunId = "smoke",
        IsTutorial = false,
        EntranceZoneIndex = 0,
        ExitZoneIndex = 1,
        CurrentZoneIndex = 0,
        CurrentZoneEntityIndex = 0,
        GenerationSeed = 424242,
        DiscoveredZoneIndices = [0],
        ZoneKeys = [zoneKey.ToString()],
        ActionBarBindings =
        [
            new AetheriaActionBarBinding
            {
                Kind = "weapon-group",
                WeaponGroup = 0
            }
        ],
        UpdatedAtUtc = now
    });

    await node.PutPlayerSettingsAsync(new AetheriaPlayerSettings
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

    await node.FlushAsync();
}

await using (var reopened = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-smoke-reopen"))
{
    var world = await reopened.GetWorldAsync();
    var item = await reopened.GetItemDefinitionByLegacyIdAsync(itemLegacyId);
    var faction = await reopened.GetCorporationByLegacyIdAsync(factionLegacyId);
    var nameFile = await reopened.GetNameFileByLegacyIdAsync(nameFileLegacyId);
    var quarantine = await reopened.GetLegacyCatalogQuarantineAsync();
    var catalogSurface = await reopened.GetCatalogSurfaceAsync();
    var drainStatus = await reopened.GetRuntimeCommitDrainStatusAsync();
    var eveCommandStatus = await reopened.GetEveCommandDrainStatusAsync();
    var operationsSurface = await reopened.GetOperationsSurfaceAsync();
    var advertisement = await reopened.GetProviderAdvertisementAsync();
    var runtimeSession = await reopened.GetRuntimeSessionAsync("smoke-runtime");
    var playerSettings = await reopened.GetPlayerSettingsAsync();
    var loadout = await reopened.GetLoadoutTemplateAsync(loadoutKey);
    var runState = await reopened.GetRunStateAsync(runKey);
    var zoneState = await reopened.GetZoneStateAsync(zoneKey);
    var entitySnapshot = await reopened.GetEntitySnapshotAsync(entityKey);

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

    if (catalogSurface?.Schema != "gamecult.eve.surface.v1" ||
        catalogSurface.Surface.Id != AetheriaCatalogSurfaceProjector.SurfaceId)
    {
        throw new InvalidOperationException("Eve catalog surface did not survive flush/reopen.");
    }

    if (drainStatus?.RuntimeId != "smoke-runtime" ||
        drainStatus.CommandsApplied != 1 ||
        eveCommandStatus?.CommandsAccepted != 1 ||
        eveCommandStatus.CommandsRejected != 1 ||
        eveCommandStatus.AppliedCatalogRefreshes != 1 ||
        !eveCommandStatus.LastRejectedReason.Contains("not advertised", StringComparison.Ordinal) ||
        operationsSurface?.Surface.Id != AetheriaOperationsSurfaceProjector.SurfaceId ||
        !operationsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.operations.eveCommandDrain") ||
        !operationsSurface.Surface.Root.Children.Any(child => child.Id == "aetheria.operations.runtimeSession"))
    {
        throw new InvalidOperationException("Runtime commit/Eve command drain status or operations surface did not survive flush/reopen.");
    }

    if (advertisement?.ProviderId != AetheriaProviderAdvertisementProjector.ProviderId ||
        advertisement.Surfaces.Length < 2 ||
        !advertisement.Surfaces.Any(surface => surface.SurfaceId == AetheriaCatalogSurfaceProjector.SurfaceId) ||
        !advertisement.Surfaces.Any(surface => surface.SurfaceId == AetheriaOperationsSurfaceProjector.SurfaceId) ||
        !advertisement.Schemas.Contains("aetheria.runtime_session.v1") ||
        !advertisement.Schemas.Contains(AetheriaEveCommandBridge.CommandSchema))
    {
        throw new InvalidOperationException("Aetheria Eve provider advertisement did not survive flush/reopen.");
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
        playerSettings.Gameplay.SignificantDigits != 4 ||
        playerSettings.Graphics.NebulaQuality != "High" ||
        playerSettings.Input.BindingOverrides.Length != 1 ||
        playerSettings.Input.ActionBarInputs.Length != 2)
    {
        throw new InvalidOperationException("Player settings did not survive flush/reopen.");
    }

    if (loadout?.RootEntity.Hull.ItemKey != AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString() ||
        loadout.RootEntity.Equipment.Length != 1 ||
        loadout.RootEntity.WeaponGroups.Length != 1)
    {
        throw new InvalidOperationException("Loadout template did not survive flush/reopen.");
    }

    if (runState?.RunId != "smoke" ||
        runState.GenerationSeed != 424242 ||
        runState.ZoneKeys.Length != 1 ||
        runState.ActionBarBindings.Length != 1)
    {
        throw new InvalidOperationException("Run state did not survive flush/reopen.");
    }

    if (zoneState?.EntityKeys.Length != 1 ||
        zoneState.EntityKeys[0] != entityKey.ToString() ||
        zoneState.Position.X != 4.0 ||
        zoneState.OwnerFactionIndex != 0 ||
        zoneState.Orbits.Length != 1 ||
        zoneState.Orbits[0].OrbitId != "smoke:orbit" ||
        zoneState.Bodies.Length != 1 ||
        zoneState.Bodies[0].Kind != "asteroid_belt" ||
        zoneState.Bodies[0].Resources.Length != 1 ||
        zoneState.Bodies[0].Asteroids.Length != 1)
    {
        throw new InvalidOperationException("Zone state did not survive flush/reopen.");
    }

    if (entitySnapshot?.Kind != "ship" ||
        entitySnapshot.Equipment.Length != 1 ||
        entitySnapshot.Equipment[0].Quality != 0.9 ||
        entitySnapshot.Equipment[0].Durability != 0.8 ||
        entitySnapshot.Equipment[0].Quantity != 1 ||
        entitySnapshot.CargoContents.Length != 1 ||
        entitySnapshot.CargoContents[0].Items[0].Item.Quantity != 7 ||
        entitySnapshot.DockingBayAssignments.Length != 1 ||
        entitySnapshot.DockingBayAssignments[0] != -1 ||
        entitySnapshot.WeaponGroups.Length != 1 ||
        entitySnapshot.WeaponStates.Length != 1 ||
        entitySnapshot.WeaponStates[0].BehaviorKind != "LockWeapon" ||
        entitySnapshot.WeaponStates[0].LockProgress != 0.65 ||
        entitySnapshot.WeaponStates[0].LockTargetEntityKey != entityKey.ToString() ||
        entitySnapshot.BehaviorStates.Length != 10 ||
        entitySnapshot.BehaviorStates[0].BehaviorKind != "AetherDrive" ||
        entitySnapshot.BehaviorStates[0].AetherDriveAxisX != 0.5 ||
        entitySnapshot.BehaviorStates[0].AetherDriveRpmY != 900.0 ||
        entitySnapshot.BehaviorStates[0].AetherDriveMaximumRpm != 2400.0 ||
        entitySnapshot.BehaviorStates[1].BehaviorKind != "ResourceScanner" ||
        entitySnapshot.BehaviorStates[1].ResourceScannerTargetBodyId != "smoke:body" ||
        entitySnapshot.BehaviorStates[1].ResourceScannerAsteroidIndex != 2 ||
        entitySnapshot.BehaviorStates[1].ResourceScannerScanTime != 1.25 ||
        entitySnapshot.BehaviorStates[1].ResourceScannerScanDuration != 3.5 ||
        entitySnapshot.BehaviorStates[2].BehaviorKind != "MiningTool" ||
        entitySnapshot.BehaviorStates[2].MiningToolAsteroidBeltId != "smoke:body" ||
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
        entitySnapshot.StatGrids.Length != 1 ||
        entitySnapshot.StatGrids[0].Values.Length != 4)
    {
        throw new InvalidOperationException("Entity snapshot did not survive flush/reopen.");
    }
}

Console.WriteLine($"Aetheria typed state smoke passed: {statePath}");
