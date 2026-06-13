using Aetheria.State;
using Aetheria.State.Unity;
using GameCult.Aetheria.State.Unity;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

await using var client = await AetheriaRuntimeCatalogClient.OpenAsync(statePath);
var catalog = client.ReadCatalog();
var packageCatalog = AetheriaRuntimeCatalogStore.OpenReadOnly(statePath);
var surface = await client.ReadCatalogSurfaceAsync();

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

Console.WriteLine($"Aetheria Unity runtime catalog smoke passed: {statePath}");
Console.WriteLine($"Items/trade/equipment: {catalog.Items.Count}/{catalog.TradeItems.Count}/{catalog.EquipmentItems.Count}");
Console.WriteLine($"Shape mask sample: {shaped.Name} {shaped.ShapeWidth}x{shaped.ShapeHeight}/{shaped.ShapeCells.Count}");
Console.WriteLine($"Interior/hardpoint sample: {interior.Name} {interior.InteriorShapeCells.Count}; {hardpointHost.Name} {hardpoint.Type} {hardpoint.ShapeCells.Count}");
Console.WriteLine($"Behavior payload sample: {behaviorHost.Name} {behaviorPayload.Kind}/{behaviorPayload.Fields.Count}");
Console.WriteLine($"Behavior sample: {behaviorKind}");
Console.WriteLine($"Eve surface: {surface.Surface.Id}");
