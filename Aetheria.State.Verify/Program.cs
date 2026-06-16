using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

RequireGameplaySourcePurity(root);
RequirePackageSerializerBoundary(root);
RequireEveRuntimeBootstrap(root);
RequireNoRendererLocalConsole(root);
RequireNoRendererLocalDebugPanels(root);
RequireMainMenuSettingsCommit(root);
RequireMainMenuContinueRunState(root);
RequirePropertiesPanelReadOnlyInspector(root);
RequireRuntimeSimulationTuningCommits(root);
RequireHullConductivityCommitAuthority(root);
RequireInventoryEntityRenameCommitAuthority(root);
RequireWeaponGroupCommitAuthority(root);
RequireInventoryDoubleClickTransferCommitAuthority(root);
RequireLootPickupCommitAuthority(root);
RequireEntityDestroyedCommitAuthority(root);
RequireDroppedPickupCheckpointState(root);
RequireTradePurchaseCommitAuthority(root);
RequireInventoryLoadoutRestoreCommitAuthority(root);
RequireDockedCurrentShipCommitAuthority(root);

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
var behaviorItemRefsMissingItemKeys = items
    .SelectMany(item => item.BehaviorPayloads)
    .Sum(CountRequiredBehaviorItemRefsMissingItemKeys);
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
var thermalResilienceItems = items.Count(item => item.ThermalResilience > 0);
var audioStatItems = items.Count(item => item.AudioStats.Length > 0);
var audioStatCount = items.Sum(item => item.AudioStats.Length);
var consumableItems = items.Count(item => item.Category == AetheriaRuntimeItemCategories.Consumable);
var consumableDurationItems = items.Count(item => item.Category == AetheriaRuntimeItemCategories.Consumable && item.Duration > 0);
var consumableEffectivenessItems = items.Count(item => item.EffectivenessCurveKeys.Length > 0);
var actionBarIconItems = items.Count(item => !string.IsNullOrWhiteSpace(item.ActionBarIcon));
var dockingBayItems = items.Count(item => item.Category == AetheriaRuntimeItemCategories.DockingBay);
var dockingBayMaxSizeItems = items.Count(item =>
    item.Category == AetheriaRuntimeItemCategories.DockingBay &&
    item.DockingMaxSizeX > 0 &&
    item.DockingMaxSizeY > 0);
var describedCorporations = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.Description));
var corporationNameLinks = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId));
var corporationAllegianceEdges = corporations.Sum(corporation => corporation.Allegiances.Length);
var legacyDtoCategories = items
    .Where(item => item.Category.EndsWith("Data", StringComparison.Ordinal))
    .Select(item => $"{item.Name}:{item.Category}")
    .ToArray();

if (pricedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any prices.");
}

if (legacyDtoCategories.Length > 0)
{
    throw new InvalidOperationException(
        "Typed item categories still contain legacy DTO class names: " +
        string.Join(", ", legacyDtoCategories.Take(5)));
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

if (thermalResilienceItems != items.Length)
{
    throw new InvalidOperationException(
        $"Typed item definitions did not import positive thermal resilience for every item: {thermalResilienceItems}/{items.Length}.");
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

if (behaviorItemRefsMissingItemKeys > 0)
{
    throw new InvalidOperationException(
        $"Typed behavior payload item refs missing item-key projections: {behaviorItemRefsMissingItemKeys}.");
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

if (dockingBayItems == 0 || dockingBayMaxSizeItems != dockingBayItems)
{
    throw new InvalidOperationException(
        $"Typed docking bay max-size import mismatch: dockingBays={dockingBayItems}, maxSizes={dockingBayMaxSizeItems}.");
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
if (string.IsNullOrWhiteSpace(manufacturedItem.ManufacturerKey) ||
    catalog.FindCorporation(manufacturedItem.ManufacturerKey) == null ||
    catalog.GetManufacturer(manufacturedItem) == null)
{
    throw new InvalidOperationException(
        $"Typed catalog manufacturer-key lookup failed for item {manufacturedItem.Name}.");
}

var corporationWithNames = corporations.FirstOrDefault(corporation =>
    !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId))
    ?? throw new InvalidOperationException("Cannot verify typed catalog name-file lookup: no linked corporation.");
if (string.IsNullOrWhiteSpace(corporationWithNames.GeonameFileKey) ||
    catalog.FindNameFile(corporationWithNames.GeonameFileKey) == null ||
    catalog.GetNameFile(corporationWithNames) == null)
{
    throw new InvalidOperationException(
        $"Typed catalog name-file-key lookup failed for corporation {corporationWithNames.Name}.");
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
Console.WriteLine($"Thermal resilience items: {thermalResilienceItems}");
Console.WriteLine($"Audio stat items/stats: {audioStatItems}/{audioStatCount}");
Console.WriteLine($"Consumable duration/effectiveness items: {consumableDurationItems}/{consumableEffectivenessItems} of {consumableItems}");
Console.WriteLine($"Shape masks: {shapedMaskItems}");
Console.WriteLine($"Interior masks/hardpoint hosts/hardpoints: {interiorShapeItems}/{hardpointHostItems}/{hardpointCount}");
Console.WriteLine($"Behavior payload items/payloads/fields/legacy refs: {behaviorPayloadItems}/{behaviorPayloadCount}/{behaviorFieldCount}/{behaviorLegacyRefCount}");
Console.WriteLine($"Behavior/hardpoint/hull/weapon items: {behaviorItems}/{hardpointItems}/{hullItems}/{weaponItems}");
Console.WriteLine($"Hull prefab items: {hullPrefabItems}");
Console.WriteLine($"Hull armor/physical facet items: {hullArmorItems}/{hullPhysicalFacetItems}");
Console.WriteLine($"Simple/compound commodity category items: {simpleCommodityCategoryItems}/{compoundCommodityCategoryItems}");
Console.WriteLine($"Durable hull/weapon items: {durableMaxDurabilityItems}/{maxDurabilityItems.Length}");
Console.WriteLine($"Thermal range/curve items: {importedThermalRangeItems}/{importedThermalCurveItems}");
Console.WriteLine($"Action-bar icon items: {actionBarIconItems}");
Console.WriteLine($"Docking bay max-size items: {dockingBayMaxSizeItems}/{dockingBayItems}");
Console.WriteLine($"Typed catalog trade items: {tradeItems.Length}");
Console.WriteLine($"Eve catalog surface: {surface.Surface.Id} ({surface.Surface.Root.Children.Length} root children)");
Console.WriteLine($"Corporations: {corporations.Length}");
Console.WriteLine($"Described/geoname-linked corporations: {describedCorporations}/{corporationNameLinks}");
Console.WriteLine($"Corporation allegiance edges: {corporationAllegianceEdges}");
Console.WriteLine($"Name files: {nameFiles.Length}");
Console.WriteLine("Live gameplay source purity: no serializer or legacy database symbols in Assets/Scripts");
Console.WriteLine("Package serializer boundary: MessagePack symbols remain in named CultCache transport files only");
Console.WriteLine("Eve runtime bootstrap: operations surface mounts through UI Toolkit presenter");
Console.WriteLine("Renderer-local console authority: deleted; UI commands flow through Eve command documents");
Console.WriteLine("Renderer-local debug panels: obsolete uGUI field tester authority is deleted");
Console.WriteLine("Main-menu settings authority: gameplay and graphics settings return through typed player-settings commits");
Console.WriteLine("Main-menu Continue authority: Continue selects typed run state instead of a null button");
Console.WriteLine("PropertiesPanel inspector authority: reflection inspection is read-only display");
Console.WriteLine("Runtime simulation tuning authority: UI writes flow through gameplay checkpoint commits");
Console.WriteLine("Hull conductivity authority: inventory UI toggles flow through gameplay checkpoint commits");
Console.WriteLine("Inventory entity rename authority: UI rename flows through gameplay checkpoint commits");
Console.WriteLine("Weapon group authority: UI assignment flows through gameplay checkpoint commits");
Console.WriteLine("Inventory transfer authority: UI transfer and drag/drop requests flow through gameplay checkpoint commits");
Console.WriteLine("Loot pickup authority: collision pickup requests flow through gameplay checkpoint commits");
Console.WriteLine("Entity destruction authority: hull-death observers flow through gameplay checkpoint commits");
Console.WriteLine("Dropped pickup state: zone checkpoints carry typed dropped-pickup snapshots and keyed live lowering");
Console.WriteLine("Trade purchase authority: UI buy requests flow through gameplay checkpoint commits");
Console.WriteLine("Inventory loadout restore authority: UI restore requests flow through gameplay checkpoint commits");
Console.WriteLine("Docked current-ship authority: UI selection requests flow through gameplay checkpoint commits");

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

static void RequireGameplaySourcePurity(string root)
{
    var gameplayRoot = Path.Combine(root, "Assets", "Scripts");
    if (!Directory.Exists(gameplayRoot))
    {
        throw new InvalidOperationException($"Cannot verify live gameplay source purity; missing path: {gameplayRoot}");
    }

    var forbiddenSymbols = new[]
    {
        "MessagePack",
        "MessagePackObject",
        "MessagePackSerializer",
        "MessagePackReader",
        "IMessagePackFormatter",
        "JsonConvert",
        "Newtonsoft",
        "JsonKnownTypes",
        "RethinkDB",
        "DatabaseEntry",
        "DatabaseLink",
        "[Union(",
        "[Key("
    };

    var hits = Directory.EnumerateFiles(gameplayRoot, "*.cs", SearchOption.AllDirectories)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .Take(10)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Live gameplay source still contains serializer or legacy database symbols: " +
            string.Join("; ", hits));
    }
}

static void RequirePackageSerializerBoundary(string root)
{
    var packageRuntimeRoot = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime");
    if (!Directory.Exists(packageRuntimeRoot))
    {
        throw new InvalidOperationException($"Cannot verify package serializer boundary; missing path: {packageRuntimeRoot}");
    }

    var allowedFiles = new HashSet<string>(StringComparer.Ordinal)
    {
        "AetheriaRuntimeCatalogStore.cs",
        "AetheriaRuntimePendingCultCacheStore.cs",
        "AetheriaRuntimeStateCommitDocument.cs",
        "AetheriaRuntimeEveCommandDocument.cs"
    };

    var serializerSymbols = new[]
    {
        "MessagePack",
        "MessagePackObject",
        "MessagePackSerializer",
        "MessagePackReader",
        "MessagePackWriter",
        "IMessagePackFormatter",
        "[Union(",
        "[Key("
    };

    var hits = Directory.EnumerateFiles(packageRuntimeRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !allowedFiles.Contains(Path.GetFileName(path)))
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => serializerSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .Take(10)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Package serializer symbols escaped the named CultCache transport boundary: " +
            string.Join("; ", hits));
    }
}

static void RequireEveRuntimeBootstrap(string root)
{
    var bootstrapPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.eve-runtime",
        "Runtime",
        "AetheriaEveRuntimeBootstrap.cs");
    var presenterPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.eve-runtime",
        "Runtime",
        "AetheriaEveSurfacePresenter.cs");

    if (!File.Exists(bootstrapPath))
    {
        throw new InvalidOperationException("Aetheria Eve runtime bootstrap is missing.");
    }

    if (!File.Exists(presenterPath))
    {
        throw new InvalidOperationException("Aetheria Eve surface presenter is missing.");
    }

    var bootstrap = File.ReadAllText(bootstrapPath);
    if (!bootstrap.Contains("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) ||
        !bootstrap.Contains("DefaultSurfaceId = \"aetheria.operations\"", StringComparison.Ordinal) ||
        !bootstrap.Contains("AetheriaEveSurfacePresenter", StringComparison.Ordinal) ||
        !bootstrap.Contains("UIDocument", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve runtime bootstrap no longer mounts the operations surface through the UI Toolkit presenter.");
    }

    var presenter = File.ReadAllText(presenterPath);
    if (!presenter.Contains("AetheriaRuntimeEveCommandLog.QueueCommand", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter no longer queues renderer commands through the typed Eve command log.");
    }
}

static void RequireNoRendererLocalConsole(string root)
{
    var consoleFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "UI", "ConsoleController.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "ConsoleView.cs")
    };

    var existingConsoleFiles = consoleFiles
        .Where(File.Exists)
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();

    if (existingConsoleFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Renderer-local console source files still exist: " +
            string.Join(", ", existingConsoleFiles));
    }

    var forbiddenSymbols = new[]
    {
        "ConsoleController",
        "ConsoleView",
        "AddCommand("
    };

    var hits = Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.*", SearchOption.AllDirectories)
        .Where(path =>
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .Take(10)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Renderer-local console authority is still wired in Assets: " +
            string.Join("; ", hits));
    }
}

static void RequireNoRendererLocalDebugPanels(string root)
{
    var forbiddenFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "UI", "FieldTester.cs")
    };

    var existingFiles = forbiddenFiles
        .Where(File.Exists)
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();

    if (existingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Renderer-local debug panel source files still exist: " +
            string.Join(", ", existingFiles));
    }

    var hits = Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.*", SearchOption.AllDirectories)
        .Where(path =>
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => line.Line.Contains("FieldTester", StringComparison.Ordinal))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .Take(10)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Renderer-local FieldTester authority is still wired in Assets: " +
            string.Join("; ", hits));
    }
}

static void RequireMainMenuSettingsCommit(string root)
{
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu settings commit path; MainMenu.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify main-menu settings authority; ActionGameManager.cs is missing.");

    var requiredCommits = new[]
    {
        "CommitRuntimePlayerName",
        "CommitRuntimeTemperatureUnit",
        "CommitRuntimeSignificantDigits",
        "CommitRuntimeNebulaQuality",
        "CommitRuntimeShowAsteroidsInMinimap"
    };

    var missingCommits = requiredCommits
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingCommits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer owns complete runtime player-settings commit primitives: " +
            string.Join(", ", missingCommits));
    }

    var forbiddenSymbols = new[]
    {
        "ActionGameManager.RuntimePlayerSettings.Name =",
        "ActionGameManager.RuntimePlayerSettings.GameplaySettings.TemperatureUnit =",
        "ActionGameManager.RuntimePlayerSettings.GameplaySettings.SignificantDigits =",
        "ActionGameManager.RuntimePlayerSettings.GraphicsSettings.NebulaQuality =",
        "ActionGameManager.RuntimePlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap ="
    };

    var hits = File.ReadLines(mainMenuPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, mainMenuPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still owns direct RuntimePlayerSettings mutation: " +
            string.Join("; ", hits));
    }

    var missingUiCalls = requiredCommits
        .Where(symbol => !source.Contains("ActionGameManager." + symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingUiCalls.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer routes settings changes through ActionGameManager: " +
            string.Join(", ", missingUiCalls));
    }
}

static void RequireMainMenuContinueRunState(string root)
{
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var mainMenu = File.Exists(mainMenuPath)
        ? File.ReadAllText(mainMenuPath)
        : throw new InvalidOperationException("Cannot verify main-menu Continue path; MainMenu.cs is missing.");

    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify Continue run authority; ActionGameManager.cs is missing.");
    var packageSnapshotPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCatalogSnapshot.cs");
    var packageSnapshot = File.Exists(packageSnapshotPath)
        ? File.ReadAllText(packageSnapshotPath)
        : throw new InvalidOperationException("Cannot verify Continue entity identity; package runtime snapshots are missing.");
    var packageStorePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCatalogStore.cs");
    var packageStore = File.Exists(packageStorePath)
        ? File.ReadAllText(packageStorePath)
        : throw new InvalidOperationException("Cannot verify Continue entity readback; package runtime store is missing.");

    var requiredMenuSymbols = new[]
    {
        "LatestContinueRun",
        "AetheriaRuntimeCatalogStore",
        "ReadRunStates(ActionGameManager.RuntimeStateFilePath)",
        "ContinueGame(continueRun)",
        "ActionGameManager.ContinueRunState = run",
        "SceneManager.LoadScene(\"ARPG\")"
    };

    var missingMenuSymbols = requiredMenuSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu Continue no longer selects typed run state: " +
            string.Join(", ", missingMenuSymbols));
    }

    if (mainMenu.Contains("AddButton(\"Continue\", null)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("MainMenu Continue regressed to a null button.");
    }

    var requiredGameplaySymbols = new[]
    {
        "AetheriaRuntimeRunStateSnapshot ContinueRunState",
        "ResolveStartZone(continuingRun)",
        "if (continuingRun != null)",
        "RestoreCurrentEntityFromTypedRun(continuingRun)",
        "ReadEntitySnapshots(RuntimeStateFilePath)",
        "entity.RecordKey",
        "CreateEntityConstructionBlueprint(entitySnapshot, true)",
        "BindToEntity(entity)",
        "RestoreActiveConsumablesFromTypedEntitySnapshot(entity, entitySnapshot)",
        "entity.RestoreStatGrids(entitySnapshot.StatGrids)",
        "RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia)",
        "entity.HeatsinksEnabled = entitySnapshot.HeatsinksEnabled",
        "ContinueRunState = null",
        "RestoreDroppedPickupsFromTypedZoneState"
    };

    var missingGameplaySymbols = requiredGameplaySymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingGameplaySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer has the typed Continue boot path: " +
            string.Join(", ", missingGameplaySymbols));
    }

    var requiredPackageSymbols = new[]
    {
        "public string RecordKey",
        "ReadEntitySnapshotPayload(record.Key, record.Payload)"
    };

    var missingPackageSymbols = requiredPackageSymbols
        .Where(symbol =>
            !packageSnapshot.Contains(symbol, StringComparison.Ordinal) &&
            !packageStore.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingPackageSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity package entity readback no longer preserves record identity for Continue: " +
            string.Join(", ", missingPackageSymbols));
    }

    var entitySourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Entity.cs");
    var entitySource = File.Exists(entitySourcePath)
        ? File.ReadAllText(entitySourcePath)
        : throw new InvalidOperationException("Cannot verify Continue entity restore ownership; Entity.cs is missing.");
    var requiredEntityRestoreSymbols = new[]
    {
        "RestoreThermalExposure",
        "RestoreActiveConsumable",
        "RestoreStatGrids",
        "RestoreHullConductivityGrid",
        "new ConsumableItemEffect(item, this, remainingDuration, duration)"
    };
    var missingEntityRestoreSymbols = requiredEntityRestoreSymbols
        .Where(symbol => !entitySource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingEntityRestoreSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Entity no longer exposes narrow runtime-owned restore primitives for typed Continue state: " +
            string.Join(", ", missingEntityRestoreSymbols));
    }
}

static void RequirePropertiesPanelReadOnlyInspector(string root)
{
    var propertiesPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Properties Panel", "PropertiesPanel.cs");
    if (!File.Exists(propertiesPanelPath))
    {
        throw new InvalidOperationException("Cannot verify PropertiesPanel inspector authority; PropertiesPanel.cs is missing.");
    }

    var source = File.ReadAllText(propertiesPanelPath);
    var forbiddenSymbols = new[]
    {
        "readWrite",
        "field.SetValue",
        "f => field.SetValue",
        "i => field.SetValue",
        "b => field.SetValue"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "PropertiesPanel reflection inspection still has renderer-local write authority: " +
            string.Join(", ", hits));
    }
}

static void RequireRuntimeSimulationTuningCommits(string root)
{
    var requiredActionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(requiredActionGameManagerPath)
        ? File.ReadAllText(requiredActionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify runtime simulation tuning authority; ActionGameManager.cs is missing.");

    var requiredCommitMethods = new[]
    {
        "CommitEntityOverrideShutdown",
        "CommitEquippedItemOverrideShutdown",
        "CommitThermotoggleTargetTemperature",
        "CommitEntityShutdownPerformance"
    };

    var missingMethods = requiredCommitMethods
        .Where(method => !actionGameManager.Contains(method, StringComparison.Ordinal))
        .ToArray();

    if (missingMethods.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime simulation tuning commit methods are missing from ActionGameManager: " +
            string.Join(", ", missingMethods));
    }

    var forbiddenUiWrites = new[]
    {
        "EquippableItem.OverrideShutdown =",
        "thermotoggle.TargetTemperature =",
        "Settings.ShutdownPerformance =",
        "CurrentEntity.OverrideShutdown ="
    };

    var uiRoot = Path.Combine(root, "Assets", "Scripts", "UI");
    var hits = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenUiWrites.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .Take(10)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime simulation tuning still has renderer-local UI write authority: " +
            string.Join("; ", hits));
    }
}

static void RequireHullConductivityCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify hull conductivity authority; ActionGameManager.cs is missing.");

    if (!actionGameManager.Contains("CommitHullConductivityToggle", StringComparison.Ordinal) ||
        !actionGameManager.Contains("QueueRunCheckpoint(\"hull-conductivity-toggle\")", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Hull conductivity no longer has a gameplay-owned checkpoint commit primitive.");
    }

    var uiRoot = Path.Combine(root, "Assets", "Scripts", "UI");
    var hits = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line =>
            line.Line.Contains("HullConductivity[", StringComparison.Ordinal) &&
            line.Line.Contains("=", StringComparison.Ordinal) &&
            !line.Line.Contains("=>", StringComparison.Ordinal))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .Take(10)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Hull conductivity still has renderer-local UI write authority: " +
            string.Join("; ", hits));
    }
}

static void RequireInventoryEntityRenameCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify inventory entity rename authority; ActionGameManager.cs is missing.");

    if (!actionGameManager.Contains("CommitEntityName", StringComparison.Ordinal) ||
        !actionGameManager.Contains("QueueRunCheckpoint(\"entity-name\")", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Entity rename no longer has a gameplay-owned checkpoint commit primitive.");
    }

    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify inventory entity rename authority; InventoryPanel.cs is missing.");

    if (inventoryPanel.Contains("_displayedEntity.Name =", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still renames entities directly instead of using the gameplay checkpoint commit primitive.");
    }

    if (!inventoryPanel.Contains("GameManager.CommitEntityName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer routes entity rename through ActionGameManager.");
    }
}

static void RequireWeaponGroupCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify weapon group authority; ActionGameManager.cs is missing.");

    if (!actionGameManager.Contains("CommitWeaponGroupMembership", StringComparison.Ordinal) ||
        !actionGameManager.Contains("QueueRunCheckpoint(\"weapon-group-membership\")", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Weapon group membership no longer has a gameplay-owned checkpoint commit primitive.");
    }

    var assignmentPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "WeaponGroupAssignment.cs");
    var assignment = File.Exists(assignmentPath)
        ? File.ReadAllText(assignmentPath)
        : throw new InvalidOperationException("Cannot verify weapon group authority; WeaponGroupAssignment.cs is missing.");

    var forbiddenSymbols = new[]
    {
        ".WeaponGroups[i1].items.Add",
        ".WeaponGroups[i1].items.Remove",
        ".WeaponGroups[i1].weapons.Add",
        ".WeaponGroups[i1].weapons.Remove"
    };

    var hits = forbiddenSymbols
        .Where(symbol => assignment.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "WeaponGroupAssignment still mutates weapon groups directly: " +
            string.Join(", ", hits));
    }

    if (!assignment.Contains("CommitWeaponGroupMembership", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("WeaponGroupAssignment no longer routes membership changes through ActionGameManager.");
    }
}

static void RequireInventoryDoubleClickTransferCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify inventory transfer authority; ActionGameManager.cs is missing.");

    var requiredCommits = new[]
    {
        "CommitCargoItemTransfer",
        "CommitCargoItemEquip",
        "CommitEquippedItemStore",
        "CommitEquippedItemEquip",
        "QueueRunCheckpoint(\"cargo-item-transfer\")",
        "QueueRunCheckpoint(\"cargo-item-equip\")",
        "QueueRunCheckpoint(\"equipped-item-store\")",
        "QueueRunCheckpoint(\"equipped-item-equip\")"
    };

    var missingCommits = requiredCommits
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingCommits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory transfer no longer has complete gameplay-owned checkpoint commit primitives: " +
            string.Join(", ", missingCommits));
    }

    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify inventory transfer authority; InventoryPanel.cs is missing.");

    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify inventory transfer authority; InventoryMenu.cs is missing.");

    var forbiddenSymbols = new[]
    {
        ".DropItem(",
        ".CargoBay.Remove(",
        ".TryUnequip(",
        ".TryEquip(",
        ".TryStore(",
        ".OriginInventory.Remove("
    };

    var hits = new[] { inventoryMenuPath, inventoryPanelPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory UI still owns direct inventory mutation: " +
            string.Join("; ", hits));
    }

    var requiredUiCalls = new[]
    {
        "GameManager.CommitCargoItemTransfer",
        "GameManager.CommitCargoItemEquip",
        "GameManager.CommitEquippedItemStore",
        "GameManager.CommitEquippedItemEquip"
    };

    var missingUiCalls = requiredUiCalls
        .Where(symbol =>
            !inventoryMenu.Contains(symbol, StringComparison.Ordinal) &&
            !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingUiCalls.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory UI no longer routes transfer requests through ActionGameManager: " +
            string.Join(", ", missingUiCalls));
    }
}

static void RequireTradePurchaseCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify trade purchase authority; ActionGameManager.cs is missing.");

    var requiredCommits = new[]
    {
        "CommitTradePurchase",
        "QueueRunCheckpoint(\"trade-purchase\")",
        "Credits -=",
        "new Ship("
    };

    var missingCommits = requiredCommits
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingCommits.Length > 0)
    {
        throw new InvalidOperationException(
            "Trade purchase no longer has a gameplay-owned checkpoint commit primitive: " +
            string.Join(", ", missingCommits));
    }

    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var tradeMenu = File.Exists(tradeMenuPath)
        ? File.ReadAllText(tradeMenuPath)
        : throw new InvalidOperationException("Cannot verify trade purchase authority; TradeMenu.cs is missing.");

    var forbiddenSymbols = new[]
    {
        "GameManager.Credits -=",
        "Inventory.TryTransferItem(",
        "new Ship(",
        ".SetParent(GameManager.DockedEntity)"
    };

    var hits = File.ReadLines(tradeMenuPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, tradeMenuPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still owns direct purchase mutation: " +
            string.Join("; ", hits));
    }

    if (!tradeMenu.Contains("GameManager.CommitTradePurchase", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("TradeMenu no longer routes purchases through ActionGameManager.");
    }
}

static void RequireLootPickupCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify loot pickup authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "CommitLootPickup",
        "QueueRunCheckpoint(\"loot-pickup\")",
        ".TryStore(item)"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Loot pickup no longer has a gameplay-owned checkpoint commit primitive: " +
            string.Join(", ", missingSymbols));
    }

    var shieldManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ShieldManager.cs");
    var shieldManager = File.Exists(shieldManagerPath)
        ? File.ReadAllText(shieldManagerPath)
        : throw new InvalidOperationException("Cannot verify loot pickup authority; ShieldManager.cs is missing.");

    var forbiddenSymbols = new[]
    {
        ".TryStore(",
        ".CargoBays.Any("
    };

    var hits = File.ReadLines(shieldManagerPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, shieldManagerPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "ShieldManager still owns direct loot cargo mutation: " +
            string.Join("; ", hits));
    }

    if (!shieldManager.Contains("ActionGameManager.Instance.CommitLootPickup", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("ShieldManager no longer routes loot pickup through ActionGameManager.");
    }
}

static void RequireEntityDestroyedCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify entity destruction authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "CommitEntityDestroyed",
        "QueueRunCheckpoint(\"entity-destroyed\")",
        "entity.Zone.Entities.Remove(entity)",
        "zoneRenderer.DropItem("
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Entity destruction no longer has a gameplay-owned checkpoint commit primitive: " +
            string.Join(", ", missingSymbols));
    }

    var entityInstancePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "EntityInstance.cs");
    var entityInstance = File.Exists(entityInstancePath)
        ? File.ReadAllText(entityInstancePath)
        : throw new InvalidOperationException("Cannot verify entity destruction authority; EntityInstance.cs is missing.");

    var forbiddenSymbols = new[]
    {
        "ZoneRenderer.DropItem(",
        "entity.Zone.Entities.Remove(entity)",
        "Random.onUnitSphere",
        "LootDropProbability"
    };

    var hits = File.ReadLines(entityInstancePath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, entityInstancePath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "EntityInstance still owns direct destruction/drop mutation: " +
            string.Join("; ", hits));
    }

    if (!entityInstance.Contains("ActionGameManager.Instance?.CommitEntityDestroyed", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("EntityInstance no longer routes destruction through ActionGameManager.");
    }
}

static void RequireDroppedPickupCheckpointState(string root)
{
    var requiredFiles = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateCommitDocument.cs")] = new[]
        {
            "AetheriaRuntimeDroppedPickupCommit",
            "DroppedPickups"
        },
        [Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs")] = new[]
        {
            "AetheriaDroppedPickupSnapshot",
            "DroppedPickups"
        },
        [Path.Combine(root, "Aetheria.State", "AetheriaRuntimeCommitLogApplier.cs")] = new[]
        {
            "ToDroppedPickups",
            "DroppedPickups = ToDroppedPickups(zone.DroppedPickups)"
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs")] = new[]
        {
            "ReadZoneStatePayload(record.Key, record.Payload)",
            "ReadFieldDroppedPickups",
            "AetheriaRuntimeDroppedPickupSnapshot"
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs")] = new[]
        {
            "public string RecordKey",
            "AetheriaRuntimeDroppedPickupSnapshot",
            "public IReadOnlyList<AetheriaRuntimeDroppedPickupSnapshot> DroppedPickups"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")] = new[]
        {
            "ProjectDroppedPickups",
            "DroppedPickups = ProjectDroppedPickups(zone)",
            "RestoreDroppedPickupsFromTypedZoneState",
            "AetheriaRuntimeCatalogStore.ReadZoneStates(RuntimeStateFilePath)",
            "ZoneRenderer.DropItem("
        },
        [Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs")] = new[]
        {
            "ActiveLoot"
        },
        [Path.Combine(root, "Aetheria.State.Unity.Smoke", "Program.cs")] = new[]
        {
            "new AetheriaRuntimeDroppedPickupCommit",
            "packageZones[0].RecordKey",
            "packageZones[0].DroppedPickups"
        }
    };

    var missing = requiredFiles
        .SelectMany(pair =>
        {
            if (!File.Exists(pair.Key))
                return new[] { $"{Path.GetRelativePath(root, pair.Key)}: missing file" };

            var text = File.ReadAllText(pair.Key);
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();

    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            "Dropped pickup checkpoint state is incomplete: " +
            string.Join("; ", missing));
    }
}

static void RequireInventoryLoadoutRestoreCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "CommitRuntimeLoadoutRestore",
        "EntityConstructionBlueprintProjector.InstantiateFromBlueprint",
        "QueueRunCheckpoint(\"loadout-restore\")",
        "Credits -="
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Loadout restore no longer has a gameplay-owned checkpoint commit primitive: " +
            string.Join(", ", missingSymbols));
    }

    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; InventoryPanel.cs is missing.");

    var forbiddenSymbols = new[]
    {
        "EntityConstructionBlueprintProjector.InstantiateFromBlueprint",
        "GameManager.Credits -=",
        ".SetParent(GameManager.DockedEntity)"
    };

    var hits = File.ReadLines(inventoryPanelPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, inventoryPanelPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still owns direct loadout restore mutation: " +
            string.Join("; ", hits));
    }

    if (!inventoryPanel.Contains("GameManager.CommitRuntimeLoadoutRestore", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer routes loadout restore through ActionGameManager.");
    }
}

static void RequireDockedCurrentShipCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify docked current-ship authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "CommitDockedCurrentShip",
        "QueueRunCheckpoint(\"docked-current-ship\")",
        "DockingBay.DockedShip = ship"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Docked current-ship selection no longer has a gameplay-owned checkpoint commit primitive: " +
            string.Join(", ", missingSymbols));
    }

    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var forbiddenSymbols = new[]
    {
        "GameManager.CurrentEntity =",
        "GameManager.DockingBay.DockedShip ="
    };

    var hits = File.ReadLines(inventoryPanelPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, inventoryPanelPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still owns direct docked current-ship mutation: " +
            string.Join("; ", hits));
    }

    var inventoryPanel = File.ReadAllText(inventoryPanelPath);
    if (!inventoryPanel.Contains("GameManager.CommitDockedCurrentShip", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer routes current-ship selection through ActionGameManager.");
    }
}

static bool ContainsBehaviorValueKind(AetheriaBehaviorValue value, string kind)
{
    return string.Equals(value.Kind, kind, StringComparison.OrdinalIgnoreCase) ||
           value.Children.Any(child => ContainsBehaviorValueKind(child, kind)) ||
           value.MapEntries.Any(entry => ContainsBehaviorValueKind(entry.Value, kind));
}

static int CountRequiredBehaviorItemRefsMissingItemKeys(AetheriaBehaviorPayload payload)
{
    return payload.Fields.Count(field =>
        IsBehaviorItemRefField(payload.Kind, field.Key) &&
        IsNonEmptyLegacyRefMissingItemKey(field.Value));
}

static bool IsBehaviorItemRefField(string behaviorKind, int fieldKey)
{
    return (string.Equals(behaviorKind, "ItemUsage", StringComparison.OrdinalIgnoreCase) && fieldKey == 1) ||
           (IsWeaponBehavior(behaviorKind) && fieldKey == 12);
}

static bool IsWeaponBehavior(string behaviorKind)
{
    return behaviorKind is "GuidedWeapon" or "InstantWeapon" or "ConstantWeapon" or "ChargedWeapon" or "AutoWeapon" or "LockWeapon";
}

static bool IsNonEmptyLegacyRefMissingItemKey(AetheriaBehaviorValue value)
{
    return string.Equals(value.Kind, "legacy-id", StringComparison.OrdinalIgnoreCase) &&
           !IsEmptyLegacyId(value.LegacyIdValue) &&
           string.IsNullOrWhiteSpace(value.ItemKeyValue);
}

static bool IsEmptyLegacyId(string legacyId)
{
    return string.IsNullOrWhiteSpace(legacyId) ||
           (Guid.TryParse(legacyId, out var parsed) && parsed == Guid.Empty);
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
