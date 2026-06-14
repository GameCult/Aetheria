using Aetheria.State;
using Aetheria.State.Unity;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

await using var client = await AetheriaRuntimeCatalogClient.OpenAsync(statePath);
var catalog = client.ReadCatalog();
var packageCatalog = AetheriaRuntimeCatalogStore.OpenReadOnly(statePath);
var packageSurfaces = AetheriaRuntimeCatalogStore.ReadEveSurfaces(statePath);
var surface = await client.ReadCatalogSurfaceAsync();
var commitSmokeDirectory = Path.Combine(Path.GetTempPath(), "aetheria-state-unity-smoke", Guid.NewGuid().ToString("N"));
var commitSmokeStatePath = Path.Combine(commitSmokeDirectory, "aetheria-world.cc");
Directory.CreateDirectory(commitSmokeDirectory);

if (catalog.Items.Count != 115)
{
    throw new InvalidOperationException($"Expected 115 runtime catalog items, found {catalog.Items.Count}.");
}

if (packageCatalog.Items.Count != catalog.Items.Count ||
    packageCatalog.Corporations.Count != catalog.Corporations.Count ||
    packageCatalog.NameFiles.Count != catalog.NameFiles.Count)
{
    throw new InvalidOperationException(
        $"Package catalog store mismatch: {packageCatalog.Items.Count}/{packageCatalog.Corporations.Count}/{packageCatalog.NameFiles.Count} " +
        $"!= {catalog.Items.Count}/{catalog.Corporations.Count}/{catalog.NameFiles.Count}.");
}

if (catalog.TradeItems.Count == 0)
{
    throw new InvalidOperationException("Runtime catalog has no trade items.");
}

if (catalog.EquipmentItems.Count == 0)
{
    throw new InvalidOperationException("Runtime catalog has no equipment items.");
}

var shaped = catalog.Items.FirstOrDefault(item => item.ShapeCells.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed shape masks.");
var packageShaped = packageCatalog.FindItemByLegacyId(shaped.LegacyId);
if (packageShaped == null ||
    packageShaped.Name != shaped.Name ||
    packageShaped.ShapeCells.Count != shaped.ShapeCells.Count)
{
    throw new InvalidOperationException($"Package catalog store did not read the expected typed item payload for {shaped.Name}.");
}

if (shaped.ShapeCells.Count != shaped.OccupiedCells)
{
    throw new InvalidOperationException(
        $"Runtime shape mask count mismatch for {shaped.Name}: cells={shaped.ShapeCells.Count}, occupied={shaped.OccupiedCells}.");
}

if (shaped.ShapeCells.Any(cell => cell.X < 0 || cell.Y < 0 || cell.X >= shaped.ShapeWidth || cell.Y >= shaped.ShapeHeight))
{
    throw new InvalidOperationException($"Runtime shape mask has out-of-bounds cells for {shaped.Name}.");
}

var interior = catalog.Items.FirstOrDefault(item => item.InteriorShapeCells.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed interior shape masks.");
if (interior.InteriorShapeCells.Count != interior.InteriorOccupiedCells)
{
    throw new InvalidOperationException(
        $"Runtime interior shape mask count mismatch for {interior.Name}: cells={interior.InteriorShapeCells.Count}, occupied={interior.InteriorOccupiedCells}.");
}

var hardpointHost = catalog.Items.FirstOrDefault(item => item.Hardpoints.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed hardpoints.");
var hardpoint = hardpointHost.Hardpoints.First();
if (hardpoint.ShapeCells.Count != hardpoint.OccupiedCells)
{
    throw new InvalidOperationException(
        $"Runtime hardpoint shape count mismatch for {hardpointHost.Name}: cells={hardpoint.ShapeCells.Count}, occupied={hardpoint.OccupiedCells}.");
}

var behaviorKind = catalog.Items.SelectMany(item => item.BehaviorKinds).FirstOrDefault()
    ?? throw new InvalidOperationException("Runtime catalog has no behavior kind fingerprints.");
if (!catalog.FindItemsByBehavior(behaviorKind).Any())
{
    throw new InvalidOperationException($"Runtime catalog behavior lookup failed for {behaviorKind}.");
}

var behaviorHost = catalog.Items.FirstOrDefault(item => item.BehaviorPayloads.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed behavior payloads.");
if (!behaviorHost.BehaviorPayloads.All(payload => behaviorHost.BehaviorKinds.Contains(payload.Kind)))
{
    throw new InvalidOperationException(
        $"Runtime behavior payload kind index mismatch for {behaviorHost.Name}.");
}

var behaviorPayload = behaviorHost.BehaviorPayloads.First();
if (behaviorPayload.Fields.Count == 0)
{
    throw new InvalidOperationException($"Runtime behavior payload has no fields for {behaviorHost.Name}.");
}

var equipment = catalog.EquipmentItems.First();
if (!catalog.FindItemsByHardpoint(equipment.HardpointType).Any())
{
    throw new InvalidOperationException($"Runtime catalog hardpoint lookup failed for {equipment.HardpointType}.");
}

var manufactured = catalog.Items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.ManufacturerLegacyId))
    ?? throw new InvalidOperationException("Runtime catalog has no manufactured item.");
if (catalog.GetManufacturer(manufactured) == null)
{
    throw new InvalidOperationException($"Runtime catalog manufacturer lookup failed for {manufactured.Name}.");
}

var packageCorporation = packageCatalog.Corporations.FirstOrDefault(corporation => !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId));
if (packageCorporation == null)
{
    throw new InvalidOperationException("Package catalog store did not read any corporation name-file links.");
}

var packageNameFile = packageCatalog.GetNameFile(packageCorporation);
if (packageNameFile == null || packageNameFile.Names.Count == 0 || packageNameFile.Names.Count != packageNameFile.NameCount)
{
    throw new InvalidOperationException("Package catalog store did not read corporation/name-file links with full names.");
}

if (packageCorporation.Allegiances.Count == 0 || packageCorporation.Allegiances.Count != packageCorporation.AllegianceCount)
{
    throw new InvalidOperationException("Package catalog store did not read corporation allegiance edges.");
}

if (surface?.Schema != "gamecult.eve.surface.v1" ||
    surface.Surface.Id != AetheriaCatalogSurfaceProjector.SurfaceId)
{
    throw new InvalidOperationException("Runtime catalog client did not read the typed Eve surface.");
}

var packageSurface = packageSurfaces.FirstOrDefault(candidate => candidate.Surface.Id == AetheriaCatalogSurfaceProjector.SurfaceId);
if (packageSurface == null ||
    packageSurface.Schema != "gamecult.eve.surface.v1" ||
    packageSurface.ProviderId != "aetheria" ||
    packageSurface.Surface.Root.Kind != "surface" ||
    packageSurface.Surface.Root.Children.Count == 0 ||
    packageSurface.Commands.All(command => command.Command != "aetheria.catalog.refresh"))
{
    throw new InvalidOperationException("Package runtime store did not read the typed Eve surface contract from CultCache.");
}

try
{
    var commit = AetheriaRuntimeStateCommitLog.QueuePlayerSettings(
        commitSmokeStatePath,
        new AetheriaRuntimePlayerSettingsCommit
        {
            PlayerName = "Unity smoke",
            TutorialPassed = true,
            ActionBarInputs = new[] { "<Keyboard>/1" }
        });
    AetheriaRuntimeStateCommitLog.QueueLoadoutTemplate(
        commitSmokeStatePath,
        new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = "Unity Smoke Loadout",
            OwnerPlayerKey = "global:aetheria.player_settings.v1",
            RootEntity = new AetheriaRuntimeEntityLoadoutCommit
            {
                Name = "Unity Smoke Ship",
                Kind = "ship",
                CorporationLegacyId = "smoke:faction",
                Hull = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemDefinitionLegacyId = "smoke:hull",
                    Quality = 0.7,
                    Durability = 0.6
                },
                Equipment = new[]
                {
                    new AetheriaRuntimeLoadoutItemSlotCommit
                    {
                        X = 1,
                        Y = 2,
                        Item = new AetheriaRuntimeLoadoutItemCommit
                        {
                            ItemDefinitionLegacyId = "smoke:weapon",
                            Quality = 0.9,
                            Durability = 0.8
                        }
                    }
                },
                CargoContents = new[]
                {
                    new AetheriaRuntimeCargoBayLoadoutCommit
                    {
                        Items = new[]
                        {
                            new AetheriaRuntimeLoadoutItemSlotCommit
                            {
                                X = 3,
                                Y = 4,
                                Item = new AetheriaRuntimeLoadoutItemCommit
                                {
                                    ItemDefinitionLegacyId = "smoke:ore",
                                    Quantity = 5
                                }
                            }
                        }
                    }
                },
                DockingBayAssignments = new[] { -1 },
                WeaponGroups = new[] { new[] { 0 } }
            }
        });
    AetheriaRuntimeStateCommitLog.QueueRunCheckpoint(
        commitSmokeStatePath,
        new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "smoke-run",
            GenerationSeed = 424242,
            CurrentZoneIndex = 0,
            CurrentZoneEntityIndex = 0,
            DiscoveredZoneIndices = new[] { 0 },
            ActionBarBindings = new[]
            {
                new AetheriaRuntimeActionBarBindingCommit
                {
                    ControlPath = "<Keyboard>/1",
                    Kind = "weapon_group",
                    WeaponGroup = 0
                },
                new AetheriaRuntimeActionBarBindingCommit
                {
                    ControlPath = "<Keyboard>/2",
                    Kind = "gear",
                    ItemDefinitionLegacyId = "smoke:weapon",
                    EquipmentIndex = 0,
                    BehaviorIndex = 1
                }
            },
            FactionRelationships = new[]
            {
                new AetheriaRuntimeFactionRelationshipCommit
                {
                    CorporationLegacyId = "smoke:faction",
                    Relationship = "Friendly",
                    Standing = 3
                }
            },
            Zones = new[]
            {
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Name = "Unity Smoke Zone",
                    PositionX = 12.0,
                    PositionY = -4.0,
                    AdjacentZoneIndices = new[] { 1 },
                    OwnerFactionIndex = 0,
                    Orbits = new[]
                    {
                        new AetheriaRuntimeOrbitSnapshotCommit
                        {
                            OrbitLegacyId = "smoke:orbit",
                            ParentLegacyId = "smoke:parent-orbit",
                            Distance = 100.0,
                            Phase = 0.25,
                            FixedPositionX = 5.0,
                            FixedPositionY = -6.0
                        }
                    },
                    Bodies = new[]
                    {
                        new AetheriaRuntimeBodySnapshotCommit
                        {
                            BodyLegacyId = "smoke:body",
                            Kind = "asteroid_belt",
                            Name = "Smoke Belt",
                            OrbitLegacyId = "smoke:orbit",
                            Mass = 42.0,
                            Resources = new[]
                            {
                                new AetheriaRuntimeBodyResourceCommit
                                {
                                    ItemDefinitionLegacyId = "smoke:ore",
                                    Amount = 3.5
                                }
                            },
                            BodyRadiusMultiplier = 1.25,
                            GravityRadiusMultiplier = 2.0,
                            GravityDepthMultiplier = 0.5,
                            GravityDepthExponent = 12.0,
                            Asteroids = new[]
                            {
                                new AetheriaRuntimeAsteroidCommit
                                {
                                    Distance = 7.0,
                                    Phase = 0.75,
                                    Size = 2.0,
                                    RotationSpeed = 0.5
                                }
                            }
                        }
                    },
                    Entities = new[]
                    {
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 0,
                            Name = "Unity Smoke Ship",
                            Kind = "ship",
                            PositionX = 1.0,
                            PositionY = 2.0,
                            PositionZ = 3.0,
                            DirectionX = 0.0,
                            DirectionY = 1.0,
                            VelocityX = 4.0,
                            VelocityY = -2.0,
                            TargetEntityIndex = 0,
                            IsActive = true,
                            HeatsinksEnabled = false,
                            OverrideShutdown = true,
                            TractorPower = 12.5,
                            Heatstroke = 0.25,
                            Hypothermia = 0.125,
                            HullItemDefinitionLegacyId = "smoke:hull",
                            Equipment = new[]
                            {
                                new AetheriaRuntimeLoadoutItemSlotCommit
                                {
                                    X = 0,
                                    Y = 0,
                                    Item = new AetheriaRuntimeLoadoutItemCommit
                                    {
                                        ItemDefinitionLegacyId = "smoke:weapon",
                                        Quality = 0.9,
                                        Durability = 0.8
                                    }
                                }
                            },
                            CargoContents = new[]
                            {
                                new AetheriaRuntimeCargoBayLoadoutCommit
                                {
                                    Items = new[]
                                    {
                                        new AetheriaRuntimeLoadoutItemSlotCommit
                                        {
                                            X = 2,
                                            Y = 3,
                                            Item = new AetheriaRuntimeLoadoutItemCommit
                                            {
                                                ItemDefinitionLegacyId = "smoke:ore",
                                                Quantity = 7
                                            }
                                        }
                                    }
                                }
                            },
                            DockingBayAssignments = new[] { -1 },
                            WeaponGroups = new[] { new[] { 0 } },
                            ActiveConsumables = new[]
                            {
                                new AetheriaRuntimeActiveConsumableCommit
                                {
                                    ItemDefinitionLegacyId = "smoke:consumable",
                                    Quality = 0.75,
                                    RemainingDuration = 3.0,
                                    Duration = 5.0
                                }
                            },
                            BehaviorProgress = new[]
                            {
                                new AetheriaRuntimeBehaviorProgressCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 0,
                                    BehaviorIndex = 1,
                                    BehaviorKind = "Cooldown",
                                    Progress = 0.5
                                },
                                new AetheriaRuntimeBehaviorProgressCommit
                                {
                                    OwnerKind = "active_consumable",
                                    OwnerIndex = 0,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "Shield",
                                    Progress = 0.75
                                }
                            },
                            WeaponStates = new[]
                            {
                                new AetheriaRuntimeWeaponStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 0,
                                    BehaviorIndex = 1,
                                    BehaviorKind = "InstantWeapon",
                                    Firing = true,
                                    Ammo = 2,
                                    BurstRemaining = 3,
                                    BurstTimer = 0.25,
                                    BurstInterval = 0.125,
                                    CooldownProgress = 0.5,
                                    CoolingDown = true
                                },
                                new AetheriaRuntimeWeaponStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 1,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "ChargedWeapon",
                                    Ammo = 1,
                                    Charging = true,
                                    Charged = false,
                                    Charge = 0.8
                                },
                                new AetheriaRuntimeWeaponStateCommit
                                {
                                    OwnerKind = "active_consumable",
                                    OwnerIndex = 0,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "ConstantWeapon",
                                    Firing = true,
                                    Ammo = 1,
                                    Reloading = true,
                                    ReloadProgress = 0.4,
                                    AmmoIntervalProgress = 0.6
                                },
                                new AetheriaRuntimeWeaponStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 2,
                                    BehaviorIndex = 1,
                                    BehaviorKind = "LockWeapon",
                                    Ammo = 1,
                                    LockProgress = 0.65,
                                    LockTargetEntityIndex = 0
                                }
                            },
                            BehaviorStates = new[]
                            {
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 0,
                                    BehaviorIndex = 2,
                                    BehaviorKind = "Sensor",
                                    Pinging = true,
                                    PingCooldown = 0.25,
                                    PingLerp = 0.5,
                                    PingRadius = 1200.0,
                                    PingedEntityCount = 3
                                },
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 1,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "Radiator",
                                    RadiatorTemperature = 450.0,
                                    Emissivity = 0.8,
                                    PumpedHeat = 12.0,
                                    WasteHeat = 1.5,
                                    EnergyUsage = 2.25
                                },
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 2,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "Reactor",
                                    ReactorDraw = 4.5,
                                    ReactorLoadRatio = 1.25
                                },
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 3,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "Capacitor",
                                    CapacitorCharge = 7.5,
                                    CapacitorCapacity = 10.0,
                                    CapacitorEfficiency = 0.95
                                },
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 4,
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
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 5,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "ResourceScanner",
                                    ResourceScannerTargetBodyId = "smoke:body",
                                    ResourceScannerAsteroidIndex = 2,
                                    ResourceScannerScanTime = 1.25,
                                    ResourceScannerRange = 500.0,
                                    ResourceScannerMinimumDensity = 0.2,
                                    ResourceScannerScanDuration = 3.5
                                }
                            },
                            StatGrids = new[]
                            {
                                new AetheriaRuntimeEntityStatGridCommit
                                {
                                    Name = "temperature",
                                    Width = 2,
                                    Height = 1,
                                    Values = new[] { 280.0, 281.0 }
                                },
                                new AetheriaRuntimeEntityStatGridCommit
                                {
                                    Name = "hull_conductivity_x",
                                    Width = 2,
                                    Height = 1,
                                    Values = new[] { 1.0, 0.0 }
                                }
                            }
                        }
                    }
                }
            }
        });
    var pending = AetheriaRuntimeStateCommitLog.ReadPending(commitSmokeStatePath);
    if (pending.Count != 3 ||
        pending[0].Kind != AetheriaRuntimeCommitKind.PlayerSettings ||
        pending[0].Schema != AetheriaRuntimeStateCommitLog.CommitSchema ||
        pending[0].Path != commit.Path)
    {
        throw new InvalidOperationException("Runtime state commit log did not read the queued player settings command.");
    }

    var typedNodeApplied = false;
    await using (var commitNode = await AetheriaStateNode.OpenAsync(commitSmokeStatePath, "aetheria-unity-runtime-commit-smoke"))
    {
        var report = await AetheriaRuntimeCommitLogApplier.ApplyPendingAsync(commitNode);
        var settings = await commitNode.GetPlayerSettingsAsync();
        var loadout = await commitNode.GetLoadoutTemplateAsync(new("global:aetheria.loadout_template.unity-smoke-loadout.v1"));
        var run = await commitNode.GetRunStateAsync(new("global:aetheria.run_state.smoke-run.v1"));
        var zone = await commitNode.GetZoneStateAsync(new("global:aetheria.run_state.smoke-run.zone.0.v1"));
        var entity = await commitNode.GetEntitySnapshotAsync(new("global:aetheria.run_state.smoke-run.zone.0.entity.0.v1"));
        typedNodeApplied = report.AppliedPlayerSettings == 1 &&
            report.AppliedLoadoutTemplates == 1 &&
            report.AppliedRunCheckpoints == 1 &&
            settings?.PlayerName == "Unity smoke" &&
            settings.Input.ActionBarInputs.Length == 1 &&
            loadout?.Name == "Unity Smoke Loadout" &&
            loadout.RootEntity.Hull.ItemKey == "aetheria.item_definition:legacy:smoke:hull" &&
            loadout.RootEntity.Equipment.Length == 1 &&
            loadout.RootEntity.Equipment[0].Position.X == 1 &&
            loadout.RootEntity.CargoContents.Length == 1 &&
            loadout.RootEntity.CargoContents[0].Items[0].Item.Quantity == 5 &&
            run?.ZoneKeys.Length == 1 &&
            run.GenerationSeed == 424242 &&
            run.ActionBarBindings.Length == 2 &&
            run.ActionBarBindings[0].ControlPath == "<Keyboard>/1" &&
            run.ActionBarBindings[0].Kind == "weapon_group" &&
            run.ActionBarBindings[0].WeaponGroup == 0 &&
            run.ActionBarBindings[1].ControlPath == "<Keyboard>/2" &&
            run.ActionBarBindings[1].Kind == "gear" &&
            run.ActionBarBindings[1].TargetKey == "aetheria.item_definition:legacy:smoke:weapon" &&
            run.ActionBarBindings[1].EquipmentIndex == 0 &&
            run.ActionBarBindings[1].BehaviorIndex == 1 &&
            run.FactionRelationships.Length == 1 &&
            run.FactionRelationships[0].FactionKey == "aetheria.corporation:legacy:smoke:faction" &&
            run.FactionRelationships[0].Relationship == "Friendly" &&
            run.FactionRelationships[0].Standing == 3 &&
            zone?.EntityKeys.Length == 1 &&
            zone.Orbits.Length == 1 &&
            zone.Orbits[0].OrbitId == "smoke:orbit" &&
            zone.Orbits[0].FixedPosition.X == 5.0 &&
            zone.Bodies.Length == 1 &&
            zone.Bodies[0].Kind == "asteroid_belt" &&
            zone.Bodies[0].Resources.Length == 1 &&
            zone.Bodies[0].Resources[0].ItemKey == "aetheria.item_definition:legacy:smoke:ore" &&
            zone.Bodies[0].Asteroids.Length == 1 &&
            entity?.Equipment.Length == 1 &&
            entity.Equipment[0].Quality == 0.9 &&
            entity.Equipment[0].Durability == 0.8 &&
            entity.Equipment[0].Quantity == 1 &&
            entity.Velocity.X == 4.0 &&
            entity.Velocity.Y == -2.0 &&
            entity.TargetEntityKey == "global:aetheria.run_state.smoke-run.zone.0.entity.0.v1" &&
            entity.IsActive &&
            !entity.HeatsinksEnabled &&
            entity.OverrideShutdown &&
            entity.TractorPower == 12.5 &&
            entity.Heatstroke == 0.25 &&
            entity.Hypothermia == 0.125 &&
            entity.ActiveConsumables.Length == 1 &&
            entity.CargoContents.Length == 1 &&
            entity.CargoContents[0].Items[0].Item.Quantity == 7 &&
            entity.DockingBayAssignments.Length == 1 &&
            entity.DockingBayAssignments[0] == -1 &&
            entity.ActiveConsumables[0].ItemKey == "aetheria.item_definition:legacy:smoke:consumable" &&
            entity.ActiveConsumables[0].RemainingDuration == 3.0 &&
            entity.BehaviorProgress.Length == 2 &&
            entity.BehaviorProgress[0].OwnerKind == "equipment" &&
            entity.BehaviorProgress[0].BehaviorKind == "Cooldown" &&
            entity.BehaviorProgress[0].Progress == 0.5 &&
            entity.BehaviorProgress[1].OwnerKind == "active_consumable" &&
            entity.BehaviorProgress[1].Progress == 0.75 &&
            entity.WeaponStates.Length == 4 &&
            entity.WeaponStates[0].BehaviorKind == "InstantWeapon" &&
            entity.WeaponStates[0].Firing &&
            entity.WeaponStates[0].Ammo == 2 &&
            entity.WeaponStates[0].BurstRemaining == 3 &&
            entity.WeaponStates[0].CooldownProgress == 0.5 &&
            entity.WeaponStates[0].CoolingDown &&
            entity.WeaponStates[1].BehaviorKind == "ChargedWeapon" &&
            entity.WeaponStates[1].Charging &&
            entity.WeaponStates[1].Charge == 0.8 &&
            entity.WeaponStates[2].OwnerKind == "active_consumable" &&
            entity.WeaponStates[2].BehaviorKind == "ConstantWeapon" &&
            entity.WeaponStates[2].Reloading &&
            entity.WeaponStates[2].AmmoIntervalProgress == 0.6 &&
            entity.WeaponStates[3].BehaviorKind == "LockWeapon" &&
            entity.WeaponStates[3].LockProgress == 0.65 &&
            entity.WeaponStates[3].LockTargetEntityKey == "global:aetheria.run_state.smoke-run.zone.0.entity.0.v1" &&
            entity.BehaviorStates.Length == 6 &&
            entity.BehaviorStates[0].BehaviorKind == "Sensor" &&
            entity.BehaviorStates[0].Pinging &&
            entity.BehaviorStates[0].PingRadius == 1200.0 &&
            entity.BehaviorStates[1].BehaviorKind == "Radiator" &&
            entity.BehaviorStates[1].RadiatorTemperature == 450.0 &&
            entity.BehaviorStates[1].PumpedHeat == 12.0 &&
            entity.BehaviorStates[2].BehaviorKind == "Reactor" &&
            entity.BehaviorStates[2].ReactorLoadRatio == 1.25 &&
            entity.BehaviorStates[3].BehaviorKind == "Capacitor" &&
            entity.BehaviorStates[3].CapacitorCharge == 7.5 &&
            entity.BehaviorStates[3].CapacitorEfficiency == 0.95 &&
            entity.BehaviorStates[4].BehaviorKind == "AetherDrive" &&
            entity.BehaviorStates[4].AetherDriveAxisX == 0.5 &&
            entity.BehaviorStates[4].AetherDriveRpmY == 900.0 &&
            entity.BehaviorStates[4].AetherDriveMaximumRpm == 2400.0 &&
            entity.BehaviorStates[4].AetherDriveThrustDirectionY == -0.5 &&
            entity.BehaviorStates[5].BehaviorKind == "ResourceScanner" &&
            entity.BehaviorStates[5].ResourceScannerTargetBodyId == "smoke:body" &&
            entity.BehaviorStates[5].ResourceScannerAsteroidIndex == 2 &&
            entity.BehaviorStates[5].ResourceScannerScanTime == 1.25 &&
            entity.BehaviorStates[5].ResourceScannerScanDuration == 3.5 &&
            entity.WeaponGroups.Length == 1 &&
            entity.StatGrids.Length == 2 &&
            entity.StatGrids.Any(grid => grid.Name == "temperature" && grid.Values.Length == 2 && grid.Values[1] == 281.0) &&
            entity.StatGrids.Any(grid => grid.Name == "hull_conductivity_x" && grid.Values.Length == 2 && grid.Values[0] == 1.0) &&
            AetheriaRuntimeStateCommitLog.ReadPending(commitSmokeStatePath).Count == 0;
    }

    var packageSettings = AetheriaRuntimeCatalogStore.ReadPlayerSettings(commitSmokeStatePath);
    var packageLoadouts = AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(commitSmokeStatePath);
    var packageRuns = AetheriaRuntimeCatalogStore.ReadRunStates(commitSmokeStatePath);
    var packageZones = AetheriaRuntimeCatalogStore.ReadZoneStates(commitSmokeStatePath);
    var packageEntities = AetheriaRuntimeCatalogStore.ReadEntitySnapshots(commitSmokeStatePath);
    if (!typedNodeApplied ||
        packageSettings?.PlayerName != "Unity smoke" ||
        packageSettings.TutorialPassed != true ||
        packageSettings.ActionBarInputs.Count != 1 ||
        packageLoadouts.Count != 1 ||
        packageLoadouts[0].RootEntity.Hull.ItemKey != "aetheria.item_definition:legacy:smoke:hull" ||
        packageLoadouts[0].RootEntity.Equipment[0].X != 1 ||
        packageLoadouts[0].RootEntity.CargoContents[0].Items[0].Item.Quantity != 5 ||
        packageRuns.Count != 1 ||
        packageRuns[0].RunId != "smoke-run" ||
        packageRuns[0].GenerationSeed != 424242 ||
        packageRuns[0].ZoneKeys.Count != 1 ||
        packageRuns[0].ActionBarBindings.Count != 2 ||
        packageRuns[0].ActionBarBindings[1].TargetKey != "aetheria.item_definition:legacy:smoke:weapon" ||
        packageRuns[0].FactionRelationships.Count != 1 ||
        packageRuns[0].FactionRelationships[0].FactionKey != "aetheria.corporation:legacy:smoke:faction" ||
        packageZones.Count != 1 ||
        packageZones[0].Name != "Unity Smoke Zone" ||
        packageZones[0].EntityKeys.Count != 1 ||
        packageZones[0].Orbits.Count != 1 ||
        packageZones[0].Orbits[0].FixedPositionX != 5.0 ||
        packageZones[0].Bodies.Count != 1 ||
        packageZones[0].Bodies[0].ResourceCount != 1 ||
        packageZones[0].Bodies[0].AsteroidCount != 1 ||
        packageEntities.Count != 1 ||
        packageEntities[0].Name != "Unity Smoke Ship" ||
        packageEntities[0].Equipment[0].Quality != 0.9 ||
        packageEntities[0].Equipment[0].Durability != 0.8 ||
        packageEntities[0].Equipment[0].Quantity != 1 ||
        packageEntities[0].VelocityX != 4.0 ||
        packageEntities[0].VelocityY != -2.0 ||
        packageEntities[0].TargetEntityKey != "global:aetheria.run_state.smoke-run.zone.0.entity.0.v1" ||
        packageEntities[0].ActiveConsumables.Count != 1 ||
        packageEntities[0].CargoContents.Count != 1 ||
        packageEntities[0].CargoContents[0].Items[0].Item.Quantity != 7 ||
        packageEntities[0].DockingBayAssignments.Count != 1 ||
        packageEntities[0].DockingBayAssignments[0] != -1 ||
        packageEntities[0].ActiveConsumables[0].ItemKey != "aetheria.item_definition:legacy:smoke:consumable" ||
        packageEntities[0].BehaviorProgress.Count != 2 ||
        packageEntities[0].WeaponStates.Count != 4 ||
        packageEntities[0].WeaponStates[2].AmmoIntervalProgress != 0.6 ||
        packageEntities[0].WeaponStates[3].LockProgress != 0.65 ||
        packageEntities[0].WeaponStates[3].LockTargetEntityKey != "global:aetheria.run_state.smoke-run.zone.0.entity.0.v1" ||
        packageEntities[0].BehaviorStates.Count != 6 ||
        packageEntities[0].BehaviorStates[3].CapacitorCharge != 7.5 ||
        packageEntities[0].BehaviorStates[4].AetherDriveRpmY != 900.0 ||
        packageEntities[0].BehaviorStates[4].AetherDriveMaximumRpm != 2400.0 ||
        packageEntities[0].BehaviorStates[5].ResourceScannerTargetBodyId != "smoke:body" ||
        packageEntities[0].BehaviorStates[5].ResourceScannerScanDuration != 3.5 ||
        packageEntities[0].StatGrids.Count != 2 ||
        packageEntities[0].StatGrids.All(grid => grid.Name != "temperature" || grid.Values.Count != 2 || grid.Values[1] != 281.0))
    {
        throw new InvalidOperationException(
            "Runtime state commit log did not apply queued settings/loadout/run snapshots through the typed state node. " +
            $"typedNodeApplied={typedNodeApplied}, packageSettings={(packageSettings == null ? "null" : $"{packageSettings.PlayerName}/{packageSettings.TutorialPassed}/{packageSettings.ActionBarInputs.Count}")}, packageLoadouts={packageLoadouts.Count}, packageRuns={packageRuns.Count}, packageZones={packageZones.Count}, packageEntities={packageEntities.Count}");
    }

    var eveCommand = AetheriaRuntimeEveCommandLog.QueueCommand(
        commitSmokeStatePath,
        new EveSurfaceCommandRequest(
            "aetheria",
            "aetheria.catalog.operator",
            "aetheria.catalog.refresh",
            new Dictionary<string, string> { ["source"] = "unity-smoke" },
            DateTimeOffset.UtcNow,
            "aetheria-state-unity-smoke"));
    var evePending = AetheriaRuntimeEveCommandLog.ReadPending(commitSmokeStatePath);
    if (evePending.Count != 1 ||
        evePending[0].Schema != AetheriaRuntimeEveCommandLog.CommandSchema ||
        evePending[0].CommandId != eveCommand.CommandId ||
        evePending[0].ProviderId != "aetheria" ||
        evePending[0].SurfaceId != "aetheria.catalog.operator" ||
        evePending[0].Command != "aetheria.catalog.refresh" ||
        evePending[0].Payload["source"] != "unity-smoke" ||
        AetheriaRuntimeStateCommitLog.ReadPending(commitSmokeStatePath).Count != 0)
    {
        throw new InvalidOperationException("Runtime Eve command log did not preserve typed command envelopes separately from state commits.");
    }
}
finally
{
    if (Environment.GetEnvironmentVariable("AETHERIA_KEEP_SMOKE_STATE") == "1")
        Console.Error.WriteLine($"Kept smoke state: {commitSmokeDirectory}");
    else
        Directory.Delete(commitSmokeDirectory, true);
}

Console.WriteLine($"Aetheria Unity runtime catalog smoke passed: {statePath}");
Console.WriteLine($"Items/trade/equipment: {catalog.Items.Count}/{catalog.TradeItems.Count}/{catalog.EquipmentItems.Count}");
Console.WriteLine($"Shape mask sample: {shaped.Name} {shaped.ShapeWidth}x{shaped.ShapeHeight}/{shaped.ShapeCells.Count}");
Console.WriteLine($"Interior/hardpoint sample: {interior.Name} {interior.InteriorShapeCells.Count}; {hardpointHost.Name} {hardpoint.Type} {hardpoint.ShapeCells.Count}");
Console.WriteLine($"Behavior payload sample: {behaviorHost.Name} {behaviorPayload.Kind}/{behaviorPayload.Fields.Count}");
Console.WriteLine($"Behavior sample: {behaviorKind}");
Console.WriteLine($"Eve surface: {surface.Surface.Id}");
Console.WriteLine($"Package Eve surfaces: {packageSurfaces.Count}");
Console.WriteLine("Runtime state commit log smoke: settings, loadouts, action-bar bindings, faction relationships, and run zone/entity snapshots queued, applied, and cleared");
Console.WriteLine("Runtime Eve command log smoke: surface command queued separately from state commits");
