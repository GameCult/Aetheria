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
    AetheriaRuntimeStateCommitLog.QueueRunCheckpoint(
        commitSmokeStatePath,
        new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "smoke-run",
            CurrentZoneIndex = 0,
            CurrentZoneEntityIndex = 0,
            DiscoveredZoneIndices = new[] { 0 },
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
                            WeaponGroups = new[] { new[] { 0 } }
                        }
                    }
                }
            }
        });
    var pending = AetheriaRuntimeStateCommitLog.ReadPending(commitSmokeStatePath);
    if (pending.Count != 2 ||
        pending[0].Kind != AetheriaRuntimeCommitKind.PlayerSettings ||
        pending[0].Schema != AetheriaRuntimeStateCommitLog.CommitSchema ||
        pending[0].Path != commit.Path)
    {
        throw new InvalidOperationException("Runtime state commit log did not read the queued player settings command.");
    }

    await using var commitNode = await AetheriaStateNode.OpenAsync(commitSmokeStatePath, "aetheria-unity-runtime-commit-smoke");
    var report = await AetheriaRuntimeCommitLogApplier.ApplyPendingAsync(commitNode);
    var settings = await commitNode.GetPlayerSettingsAsync();
    var run = await commitNode.GetRunStateAsync(new("global:aetheria.run_state.smoke-run.v1"));
    var zone = await commitNode.GetZoneStateAsync(new("global:aetheria.run_state.smoke-run.zone.0.v1"));
    var entity = await commitNode.GetEntitySnapshotAsync(new("global:aetheria.run_state.smoke-run.zone.0.entity.0.v1"));
    if (report.AppliedPlayerSettings != 1 ||
        report.AppliedRunCheckpoints != 1 ||
        settings?.PlayerName != "Unity smoke" ||
        settings.Input.ActionBarInputs.Length != 1 ||
        run?.ZoneKeys.Length != 1 ||
        zone?.EntityKeys.Length != 1 ||
        entity?.Equipment.Length != 1 ||
        entity.WeaponGroups.Length != 1 ||
        AetheriaRuntimeStateCommitLog.ReadPending(commitSmokeStatePath).Count != 0)
    {
        throw new InvalidOperationException("Runtime state commit log did not apply queued settings/run snapshots through the typed state node.");
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
Console.WriteLine("Runtime state commit log smoke: settings and run zone/entity snapshots queued, applied, and cleared");
Console.WriteLine("Runtime Eve command log smoke: surface command queued separately from state commits");
