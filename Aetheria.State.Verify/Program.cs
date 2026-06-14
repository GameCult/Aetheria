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
RequireCount(ledger, "aetheria.corporation.v2", corporations.Length);
RequireCount(ledger, "aetheria.name_file.v2", nameFiles.Length);

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

if (nameFiles.Any(nameFile => nameFile.Names.Length == 0 || nameFile.Names.Length != nameFile.NameCount))
{
    throw new InvalidOperationException("Typed name files did not import their full name arrays.");
}

var pricedItems = items.Count(item => item.Price > 0);
var manufacturedItems = items.Count(item => !string.IsNullOrWhiteSpace(item.ManufacturerLegacyId));
var specificHeatItems = items.Count(item => item.SpecificHeat > 0);
var conductiveItems = items.Count(item => item.Conductivity > 0);
var shapedItems = items.Count(item => item.ShapeWidth > 0 && item.ShapeHeight > 0 && item.OccupiedCells > 0);
var shapedMaskItems = items.Count(item => item.ShapeCells.Length > 0);
var interiorShapeItems = items.Count(item => item.InteriorShapeCells.Length > 0);
var hardpointHostItems = items.Count(item => item.Hardpoints.Length > 0);
var hardpointCount = items.Sum(item => item.Hardpoints.Length);
var behaviorItems = items.Count(item => item.BehaviorCount > 0 && item.BehaviorKinds.Length > 0);
var behaviorPayloadItems = items.Count(item => item.BehaviorPayloads.Length > 0);
var behaviorPayloadCount = items.Sum(item => item.BehaviorPayloads.Length);
var behaviorFieldCount = items.SelectMany(item => item.BehaviorPayloads).Sum(behavior => behavior.Fields.Length);
var behaviorLegacyRefCount = items
    .SelectMany(item => item.BehaviorPayloads)
    .SelectMany(behavior => behavior.Fields)
    .Count(field => ContainsBehaviorValueKind(field.Value, "legacy-id"));
var hardpointItems = items.Count(item => !string.IsNullOrWhiteSpace(item.HardpointType));
var hullItems = items.Count(item => !string.IsNullOrWhiteSpace(item.HullType));
var hullPrefabItems = items.Count(item => !string.IsNullOrWhiteSpace(item.HullPrefab));
var hullArmorItems = items.Count(item => !string.IsNullOrWhiteSpace(item.HullType) && item.HullArmor > 0);
var hullPhysicalFacetItems = items.Count(item =>
    !string.IsNullOrWhiteSpace(item.HullType) &&
    item.HullArmor > 0 &&
    item.HullDrag >= 0 &&
    !double.IsNaN(item.HullGridOffset));
var simpleCommodityCategoryItems = items.Count(item => !string.IsNullOrWhiteSpace(item.SimpleCommodityCategory));
var compoundCommodityCategoryItems = items.Count(item => !string.IsNullOrWhiteSpace(item.CompoundCommodityCategory));
var weaponItems = items.Count(item =>
    !string.IsNullOrWhiteSpace(item.WeaponType) &&
    !string.IsNullOrWhiteSpace(item.WeaponRange) &&
    !string.IsNullOrWhiteSpace(item.WeaponCaliber));
var maxDurabilityItems = items
    .Where(item => !string.IsNullOrWhiteSpace(item.HullType) || !string.IsNullOrWhiteSpace(item.WeaponType))
    .ToArray();
var durableMaxDurabilityItems = maxDurabilityItems.Count(item => item.Durability > 0);
var importedThermalRangeItems = items.Count(item => item.MaximumTemperature > item.MinimumTemperature);
var importedThermalCurveItems = items.Count(item => item.ThermalPerformanceCurveKeys.Length > 0);
var describedCorporations = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.Description));
var corporationNameLinks = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId));
var corporationAllegianceEdges = corporations.Sum(corporation => corporation.Allegiances.Length);

if (pricedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any prices.");
}

if (manufacturedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any manufacturer legacy IDs.");
}

if (specificHeatItems != items.Length)
{
    throw new InvalidOperationException(
        $"Typed item definitions did not import positive specific heat for every item: {specificHeatItems}/{items.Length}.");
}

if (conductiveItems != items.Length)
{
    throw new InvalidOperationException(
        $"Typed item definitions did not import positive conductivity for every item: {conductiveItems}/{items.Length}.");
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

foreach (var item in items.Where(item => item.InteriorShapeCells.Length > 0))
{
    if (item.InteriorShapeCells.Length != item.InteriorOccupiedCells)
    {
        throw new InvalidOperationException(
            $"Typed item interior shape mask count mismatch for {item.Name}: cells={item.InteriorShapeCells.Length}, occupied={item.InteriorOccupiedCells}.");
    }

    if (item.InteriorShapeCells.Any(cell =>
            cell.X < 0 || cell.Y < 0 || cell.X >= item.InteriorShapeWidth || cell.Y >= item.InteriorShapeHeight))
    {
        throw new InvalidOperationException($"Typed item interior shape mask has out-of-bounds cells for {item.Name}.");
    }
}

if (interiorShapeItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any interior shape masks.");
}

if (hardpointHostItems == 0 || hardpointCount == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any hull hardpoints.");
}

var missingMaxDurability = maxDurabilityItems
    .Where(item => item.Durability <= 0)
    .Select(item => item.Name)
    .ToArray();
if (missingMaxDurability.Length > 0)
{
    throw new InvalidOperationException(
        $"Typed hull/weapon max durability missing for: {string.Join(", ", missingMaxDurability)}.");
}

if (importedThermalRangeItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any thermal ranges.");
}

if (importedThermalCurveItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any thermal performance curves.");
}

foreach (var item in items.Where(item => item.ThermalPerformanceCurveKeys.Length > 0))
{
    if (item.ThermalPerformanceCurveKeys.Any(key => key.Time < 0 || key.Time > 1))
    {
        throw new InvalidOperationException($"Typed thermal performance curve has out-of-range key time for {item.Name}.");
    }
}

foreach (var item in items.Where(item => item.Hardpoints.Length > 0))
{
    foreach (var hardpoint in item.Hardpoints)
    {
        if (string.IsNullOrWhiteSpace(hardpoint.Type))
        {
            throw new InvalidOperationException($"Typed item hardpoint has no type for {item.Name}.");
        }

        if (hardpoint.ShapeCells.Length != hardpoint.OccupiedCells)
        {
            throw new InvalidOperationException(
                $"Typed item hardpoint shape count mismatch for {item.Name}: cells={hardpoint.ShapeCells.Length}, occupied={hardpoint.OccupiedCells}.");
        }

        if (hardpoint.ShapeCells.Any(cell =>
                cell.X < 0 || cell.Y < 0 || cell.X >= hardpoint.ShapeWidth || cell.Y >= hardpoint.ShapeHeight))
        {
            throw new InvalidOperationException($"Typed item hardpoint shape has out-of-bounds cells for {item.Name}.");
        }

        // Legacy content includes at least one overhanging hardpoint. Preserve the
        // payload faithfully here; content repair belongs to a separate pass.
    }
}

if (behaviorItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any behavior fingerprints.");
}

if (behaviorPayloadItems != behaviorItems || behaviorPayloadCount == 0 || behaviorFieldCount == 0)
{
    throw new InvalidOperationException(
        $"Typed behavior payload import mismatch: payloadItems={behaviorPayloadItems}, behaviorItems={behaviorItems}, payloads={behaviorPayloadCount}, fields={behaviorFieldCount}.");
}

foreach (var item in items.Where(item => item.BehaviorPayloads.Length > 0))
{
    if (item.BehaviorPayloads.Length != item.BehaviorCount)
    {
        throw new InvalidOperationException(
            $"Typed item behavior payload count mismatch for {item.Name}: payloads={item.BehaviorPayloads.Length}, count={item.BehaviorCount}.");
    }

    foreach (var behavior in item.BehaviorPayloads)
    {
        if (string.IsNullOrWhiteSpace(behavior.Kind) || behavior.UnionKey < 0 || behavior.Fields.Length == 0)
        {
            throw new InvalidOperationException($"Typed item behavior payload is incomplete for {item.Name}.");
        }
    }
}

if (hardpointItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any hardpoint types.");
}

if (hullItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any hull types.");
}

if (hullPrefabItems != hullItems)
{
    throw new InvalidOperationException($"Typed hull prefab import mismatch: hulls={hullItems}, prefabs={hullPrefabItems}.");
}

if (hullArmorItems != hullItems || hullPhysicalFacetItems != hullItems)
{
    throw new InvalidOperationException(
        $"Typed hull physical facet import mismatch: hulls={hullItems}, armor={hullArmorItems}, physical={hullPhysicalFacetItems}.");
}

if (simpleCommodityCategoryItems == 0)
{
    throw new InvalidOperationException("Typed simple commodity category import produced no categories.");
}

if (compoundCommodityCategoryItems == 0)
{
    throw new InvalidOperationException("Typed compound commodity category import produced no categories.");
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

if (corporationAllegianceEdges == 0 || corporations.Any(corporation => corporation.Allegiances.Length != corporation.AllegianceCount))
{
    throw new InvalidOperationException("Typed corporations did not import full allegiance edges.");
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
Console.WriteLine($"Priced/manufactured/specific-heat/conductive/shaped items: {pricedItems}/{manufacturedItems}/{specificHeatItems}/{conductiveItems}/{shapedItems}");
Console.WriteLine($"Shape masks: {shapedMaskItems}");
Console.WriteLine($"Interior masks/hardpoint hosts/hardpoints: {interiorShapeItems}/{hardpointHostItems}/{hardpointCount}");
Console.WriteLine($"Behavior payload items/payloads/fields/legacy refs: {behaviorPayloadItems}/{behaviorPayloadCount}/{behaviorFieldCount}/{behaviorLegacyRefCount}");
Console.WriteLine($"Behavior/hardpoint/hull/weapon items: {behaviorItems}/{hardpointItems}/{hullItems}/{weaponItems}");
Console.WriteLine($"Hull prefab items: {hullPrefabItems}");
Console.WriteLine($"Hull armor/physical facet items: {hullArmorItems}/{hullPhysicalFacetItems}");
Console.WriteLine($"Simple/compound commodity category items: {simpleCommodityCategoryItems}/{compoundCommodityCategoryItems}");
Console.WriteLine($"Durable hull/weapon items: {durableMaxDurabilityItems}/{maxDurabilityItems.Length}");
Console.WriteLine($"Thermal range/curve items: {importedThermalRangeItems}/{importedThermalCurveItems}");
Console.WriteLine($"Typed catalog trade items: {tradeItems.Length}");
Console.WriteLine($"Eve catalog surface: {surface.Surface.Id} ({surface.Surface.Root.Children.Length} root children)");
Console.WriteLine($"Corporations: {corporations.Length}");
Console.WriteLine($"Described/geoname-linked corporations: {describedCorporations}/{corporationNameLinks}");
Console.WriteLine($"Corporation allegiance edges: {corporationAllegianceEdges}");
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

static bool ContainsBehaviorValueKind(AetheriaBehaviorValue value, string kind)
{
    return string.Equals(value.Kind, kind, StringComparison.OrdinalIgnoreCase) ||
           value.Children.Any(child => ContainsBehaviorValueKind(child, kind)) ||
           value.MapEntries.Any(entry => ContainsBehaviorValueKind(entry.Value, kind));
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
