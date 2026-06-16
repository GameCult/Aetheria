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
RequirePropertiesPanelReadOnlyInspector(root);
RequireRuntimeSimulationTuningCommits(root);
RequireHullConductivityCommitAuthority(root);
RequireInventoryEntityRenameCommitAuthority(root);
RequireWeaponGroupCommitAuthority(root);
RequireInventoryDoubleClickTransferCommitAuthority(root);

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
Console.WriteLine("PropertiesPanel inspector authority: reflection inspection is read-only display");
Console.WriteLine("Runtime simulation tuning authority: UI writes flow through gameplay checkpoint commits");
Console.WriteLine("Hull conductivity authority: inventory UI toggles flow through gameplay checkpoint commits");
Console.WriteLine("Inventory entity rename authority: UI rename flows through gameplay checkpoint commits");
Console.WriteLine("Weapon group authority: UI assignment flows through gameplay checkpoint commits");
Console.WriteLine("Inventory transfer authority: UI transfer and drag/drop requests flow through gameplay checkpoint commits");

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
    if (!source.Contains("private void CommitRuntimeSettingsAndReturn()", StringComparison.Ordinal) ||
        !source.Contains("ActionGameManager.QueueRuntimePlayerSettingsCommit();", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu no longer exposes a shared typed player-settings commit return path.");
    }

    var graphicsSettings = ExtractMethodBody(source, "ShowGraphicsSettings");
    if (!graphicsSettings.Contains("CommitRuntimeSettingsAndReturn", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu graphics settings can return without queuing the typed player-settings commit.");
    }

    var gameplaySettings = ExtractMethodBody(source, "ShowGameplaySettings");
    if (!gameplaySettings.Contains("CommitRuntimeSettingsAndReturn", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu gameplay settings can return without queuing the typed player-settings commit.");
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

static string ExtractMethodBody(string source, string methodName)
{
    var signature = "private void " + methodName + "()";
    var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0)
        throw new InvalidOperationException($"Cannot find MainMenu.{methodName} for settings authority verification.");

    var braceIndex = source.IndexOf('{', signatureIndex);
    if (braceIndex < 0)
        throw new InvalidOperationException($"Cannot find MainMenu.{methodName} body for settings authority verification.");

    var depth = 0;
    for (var index = braceIndex; index < source.Length; index++)
    {
        if (source[index] == '{')
            depth++;
        else if (source[index] == '}')
        {
            depth--;
            if (depth == 0)
                return source.Substring(braceIndex, index - braceIndex + 1);
        }
    }

    throw new InvalidOperationException($"MainMenu.{methodName} body is not balanced.");
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
