using Aetheria.State;
using Aetheria.State.Unity;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

await using var client = await AetheriaRuntimeCatalogClient.OpenAsync(statePath);
var catalog = client.ReadCatalog();
var surface = await client.ReadCatalogSurfaceAsync();

if (catalog.Items.Count != 115)
{
    throw new InvalidOperationException($"Expected 115 runtime catalog items, found {catalog.Items.Count}.");
}

if (catalog.TradeItems.Count == 0)
{
    throw new InvalidOperationException("Runtime catalog has no trade items.");
}

if (catalog.EquipmentItems.Count == 0)
{
    throw new InvalidOperationException("Runtime catalog has no equipment items.");
}

var behaviorKind = catalog.Items.SelectMany(item => item.BehaviorKinds).FirstOrDefault()
    ?? throw new InvalidOperationException("Runtime catalog has no behavior kind fingerprints.");
if (!catalog.FindItemsByBehavior(behaviorKind).Any())
{
    throw new InvalidOperationException($"Runtime catalog behavior lookup failed for {behaviorKind}.");
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

if (surface?.Schema != "gamecult.eve.surface.v1" ||
    surface.Surface.Id != AetheriaCatalogSurfaceProjector.SurfaceId)
{
    throw new InvalidOperationException("Runtime catalog client did not read the typed Eve surface.");
}

Console.WriteLine($"Aetheria Unity runtime catalog smoke passed: {statePath}");
Console.WriteLine($"Items/trade/equipment: {catalog.Items.Count}/{catalog.TradeItems.Count}/{catalog.EquipmentItems.Count}");
Console.WriteLine($"Behavior sample: {behaviorKind}");
Console.WriteLine($"Eve surface: {surface.Surface.Id}");
