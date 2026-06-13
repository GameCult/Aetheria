using Aetheria.State;
using Aetheria.State.Documents;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

await using var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-verify");

var ledger = await node.GetMigrationLedgerAsync()
    ?? throw new InvalidOperationException("Missing typed migration ledger.");
var quarantine = await node.GetLegacyCatalogQuarantineAsync()
    ?? throw new InvalidOperationException("Missing legacy catalog quarantine document.");
var publishedSurface = await node.GetCatalogSurfaceAsync()
    ?? throw new InvalidOperationException("Missing Aetheria catalog Eve surface document.");

var items = node.Cache.GetAll<AetheriaItemDefinition>().ToArray();
var corporations = node.Cache.GetAll<AetheriaCorporation>().ToArray();
var nameFiles = node.Cache.GetAll<AetheriaNameFile>().ToArray();
var catalog = node.ReadCatalogSnapshot();
var surface = AetheriaCatalogSurfaceProjector.Build(catalog, DateTimeOffset.UtcNow.ToString("O"));

RequireCount(ledger, "aetheria.item_definition.v1", items.Length);
RequireCount(ledger, "aetheria.corporation.v1", corporations.Length);
RequireCount(ledger, "aetheria.name_file.v1", nameFiles.Length);

if (items.Length == 0)
{
    throw new InvalidOperationException("Typed state has no item definitions.");
}

if (corporations.Length == 0)
{
    throw new InvalidOperationException("Typed state has no corporations.");
}

if (nameFiles.Length == 0)
{
    throw new InvalidOperationException("Typed state has no name files.");
}

var pricedItems = items.Count(item => item.Price > 0);
var manufacturedItems = items.Count(item => !string.IsNullOrWhiteSpace(item.ManufacturerLegacyId));
var shapedItems = items.Count(item => item.ShapeWidth > 0 && item.ShapeHeight > 0 && item.OccupiedCells > 0);
var shapedMaskItems = items.Count(item => item.ShapeCells.Length > 0);
var behaviorItems = items.Count(item => item.BehaviorCount > 0 && item.BehaviorKinds.Length > 0);
var hardpointItems = items.Count(item => !string.IsNullOrWhiteSpace(item.HardpointType));
var hullItems = items.Count(item => !string.IsNullOrWhiteSpace(item.HullType));
var weaponItems = items.Count(item =>
    !string.IsNullOrWhiteSpace(item.WeaponType) &&
    !string.IsNullOrWhiteSpace(item.WeaponRange) &&
    !string.IsNullOrWhiteSpace(item.WeaponCaliber));
var describedCorporations = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.Description));
var corporationNameLinks = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId));

if (pricedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any prices.");
}

if (manufacturedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any manufacturer legacy IDs.");
}

if (shapedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any shape dimensions.");
}

if (shapedMaskItems != shapedItems)
{
    throw new InvalidOperationException(
        $"Typed item shape mask import mismatch: masks={shapedMaskItems}, shaped={shapedItems}.");
}

foreach (var item in items.Where(item => item.ShapeCells.Length > 0))
{
    if (item.ShapeCells.Length != item.OccupiedCells)
    {
        throw new InvalidOperationException(
            $"Typed item shape mask cell count mismatch for {item.Name}: cells={item.ShapeCells.Length}, occupied={item.OccupiedCells}.");
    }

    if (item.ShapeCells.Any(cell => cell.X < 0 || cell.Y < 0 || cell.X >= item.ShapeWidth || cell.Y >= item.ShapeHeight))
    {
        throw new InvalidOperationException($"Typed item shape mask has out-of-bounds cells for {item.Name}.");
    }
}

if (behaviorItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any behavior fingerprints.");
}

if (hardpointItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any hardpoint types.");
}

if (hullItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any hull types.");
}

if (weaponItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any weapon facets.");
}

if (describedCorporations == 0)
{
    throw new InvalidOperationException("Typed corporations did not import descriptions from legacy key 3.");
}

if (corporationNameLinks == 0)
{
    throw new InvalidOperationException("Typed corporations did not import geoname file legacy IDs.");
}

var tradeItems = catalog.TradeItems.ToArray();
if (tradeItems.Length != pricedItems)
{
    throw new InvalidOperationException(
        $"Typed catalog trade item query mismatch: query={tradeItems.Length}, priced={pricedItems}.");
}

var equipmentItems = catalog.EquipmentItems.ToArray();
if (equipmentItems.Length != hardpointItems)
{
    throw new InvalidOperationException(
        $"Typed catalog equipment item query mismatch: query={equipmentItems.Length}, hardpoint={hardpointItems}.");
}

var behaviorKind = items.SelectMany(item => item.BehaviorKinds).FirstOrDefault()
    ?? throw new InvalidOperationException("Cannot verify typed catalog behavior query: no behavior kinds.");
if (!catalog.FindItemsByBehavior(behaviorKind).Any())
{
    throw new InvalidOperationException($"Typed catalog behavior query failed for {behaviorKind}.");
}

var hardpointType = items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.HardpointType))?.HardpointType
    ?? throw new InvalidOperationException("Cannot verify typed catalog hardpoint query: no hardpoint types.");
if (!catalog.FindItemsByHardpoint(hardpointType).Any())
{
    throw new InvalidOperationException($"Typed catalog hardpoint query failed for {hardpointType}.");
}

var manufacturedItem = items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.ManufacturerLegacyId))
    ?? throw new InvalidOperationException("Cannot verify typed catalog manufacturer lookup: no manufactured item.");
if (catalog.GetManufacturer(manufacturedItem) == null)
{
    throw new InvalidOperationException(
        $"Typed catalog manufacturer lookup failed for item {manufacturedItem.Name}.");
}

var corporationWithNames = corporations.FirstOrDefault(corporation =>
    !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId))
    ?? throw new InvalidOperationException("Cannot verify typed catalog name-file lookup: no linked corporation.");
if (catalog.GetNameFile(corporationWithNames) == null)
{
    throw new InvalidOperationException(
        $"Typed catalog name-file lookup failed for corporation {corporationWithNames.Name}.");
}

if (surface.Schema != "gamecult.eve.surface.v1" ||
    surface.Surface.Root.Kind != "surface" ||
    surface.Surface.Root.Children.Length == 0)
{
    throw new InvalidOperationException("Aetheria catalog Eve surface projection is not renderable.");
}

if (publishedSurface.Schema != surface.Schema ||
    publishedSurface.Surface.Id != AetheriaCatalogSurfaceProjector.SurfaceId ||
    publishedSurface.Surface.Root.Kind != "surface")
{
    throw new InvalidOperationException("Published Aetheria catalog Eve surface is not the expected typed surface.");
}

await RequireLegacyLookupAsync(
    items[0].LegacyId,
    () => node.GetItemDefinitionByLegacyIdAsync(items[0].LegacyId),
    "item definition");
await RequireLegacyLookupAsync(
    corporations[0].LegacyId,
    () => node.GetCorporationByLegacyIdAsync(corporations[0].LegacyId),
    "corporation");
await RequireLegacyLookupAsync(
    nameFiles[0].LegacyId,
    () => node.GetNameFileByLegacyIdAsync(nameFiles[0].LegacyId),
    "name file");

if (string.IsNullOrWhiteSpace(quarantine.CatalogFingerprint))
{
    throw new InvalidOperationException("Legacy catalog quarantine has no catalog fingerprint.");
}

Console.WriteLine($"Aetheria typed state verify passed: {statePath}");
Console.WriteLine($"Catalog fingerprint: {quarantine.CatalogFingerprint}");
Console.WriteLine($"Item definitions: {items.Length}");
Console.WriteLine($"Priced/manufactured/shaped items: {pricedItems}/{manufacturedItems}/{shapedItems}");
Console.WriteLine($"Shape masks: {shapedMaskItems}");
Console.WriteLine($"Behavior/hardpoint/hull/weapon items: {behaviorItems}/{hardpointItems}/{hullItems}/{weaponItems}");
Console.WriteLine($"Typed catalog trade items: {tradeItems.Length}");
Console.WriteLine($"Eve catalog surface: {surface.Surface.Id} ({surface.Surface.Root.Children.Length} root children)");
Console.WriteLine($"Corporations: {corporations.Length}");
Console.WriteLine($"Described/geoname-linked corporations: {describedCorporations}/{corporationNameLinks}");
Console.WriteLine($"Name files: {nameFiles.Length}");

static void RequireCount(AetheriaMigrationLedger ledger, string documentType, int actual)
{
    var expected = ledger.Counts.FirstOrDefault(count => count.DocumentType == documentType)?.Count;
    if (expected == null)
    {
        throw new InvalidOperationException($"Migration ledger is missing count for {documentType}.");
    }

    if (expected.Value != actual)
    {
        throw new InvalidOperationException(
            $"Migration ledger count mismatch for {documentType}: ledger={expected.Value}, actual={actual}.");
    }
}

static async Task RequireLegacyLookupAsync<T>(
    string legacyId,
    Func<Task<T?>> lookup,
    string label) where T : class
{
    if (string.IsNullOrWhiteSpace(legacyId))
    {
        throw new InvalidOperationException($"Cannot verify {label}: legacy id is empty.");
    }

    var value = await lookup().ConfigureAwait(false);
    if (value == null)
    {
        throw new InvalidOperationException($"Typed legacy-id lookup failed for {label} {legacyId}.");
    }
}
