using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;
using GameCult.Caching;

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var statePath = AetheriaStatePaths.ResolveDefaultStatePath(root);
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
                ItemKey = AetheriaCatalogKeys.ItemDefinitionFromLegacyId(itemLegacyId).ToString()
            }
        ],
        WeaponGroups =
        [
            new AetheriaWeaponGroupSnapshot
            {
                EquipmentIndices = [0]
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
        EntityKeys = [entityKey.ToString()]
    });

    await node.PutRunStateAsync(runKey, new AetheriaRunState
    {
        RunId = "smoke",
        IsTutorial = false,
        EntranceZoneIndex = 0,
        ExitZoneIndex = 1,
        CurrentZoneIndex = 0,
        CurrentZoneEntityIndex = 0,
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
        runState.ZoneKeys.Length != 1 ||
        runState.ActionBarBindings.Length != 1)
    {
        throw new InvalidOperationException("Run state did not survive flush/reopen.");
    }

    if (zoneState?.EntityKeys.Length != 1 ||
        zoneState.EntityKeys[0] != entityKey.ToString() ||
        zoneState.Position.X != 4.0 ||
        zoneState.OwnerFactionIndex != 0)
    {
        throw new InvalidOperationException("Zone state did not survive flush/reopen.");
    }

    if (entitySnapshot?.Kind != "ship" ||
        entitySnapshot.Equipment.Length != 1 ||
        entitySnapshot.WeaponGroups.Length != 1 ||
        entitySnapshot.StatGrids.Length != 1 ||
        entitySnapshot.StatGrids[0].Values.Length != 4)
    {
        throw new InvalidOperationException("Entity snapshot did not survive flush/reopen.");
    }
}

Console.WriteLine($"Aetheria typed state smoke passed: {statePath}");
