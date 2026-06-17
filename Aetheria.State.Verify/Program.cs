using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

RequireGameplaySourcePurity(root);
RequirePackageSerializerBoundary(root);
RequireSharedEvePackagesImportedFromEveRepo(root);
RequireTypedPendingCommitKeys(root);
RequireTypedRuntimeFactionKeys(root);
RequireTypedGalaxyFactionRelationships(root);
RequireRuntimeCatalogKeyOnlyLookups(root);
RequireTypedBehaviorBodyKeys(root);
RequireTypedOrbitTaskKeys(root);
RequireTypedAgentTaskKeys(root);
RequireTypedOrbitalEntityOrbitKeys(root);
RequireTypedOrbitConsumerKeys(root);
RequireKeyedOrbitRuntimeWrappers(root);
RequireNativeZoneKeyResolution(root);
RequireTypedZoneRuntimeCollections(root);
RequireTypedZoneConstructionKeys(root);
RequireTypedZoneStateSnapshotKeys(root);
RequireTypedAsteroidZoneApi(root);
RequireTypedFactionShellLinks(root);
RequireFactionKeyIdentity(root);
RequireTypedRuntimeBehaviorCoverage(root);
RequireEveRuntimeBootstrap(root);
RequireNoRendererLocalConsole(root);
RequireNoRendererLocalDebugPanels(root);
RequireMainMenuSettingsCommit(root);
RequireMainMenuRootUsesEveSurface(root);
RequireMainMenuSettingsShellUsesEveSurface(root);
RequireConfirmationDialogOwnsMinimalPromptShell(root);
RequireMainMenuInputSettingsDelegateToRuntimeScreen(root);
RequireRuntimeInputScreenUsesEveSurface(root);
RequireActionGameManagerInputScreenUsesSharedFullscreenPrimitive(root);
RequireSectorMapZoneDetailsUseEveSurface(root);
RequireRuntimeMenuTabsUseEveSurface(root);
RequireInventoryShipSettingsUseEveSurface(root);
RequireInventoryCargoItemDetailsUseEveSurface(root);
RequireInventoryEquippedItemDetailsUseEveSurface(root);
RequireTradeCargoSelectorUseEveSurface(root);
RequireTradeFilterAndRowActionsUseEveSurface(root);
RequireTradeItemDetailsUseEveSurface(root);
RequireInventoryDropdownUseEveSurface(root);
RequireNoDeadPopupShells(root);
RequirePlayerSettingsEveSurface(root);
RequireVerseHostSettingsAuthority(root);
RequireClientTargetBootAuthority(root);
RequireVerseReplicaTool(root);
RequireVerseSettingsShellAndBridge(root);
RequireMainMenuVerseHostProjection(root);
RequireMainMenuContinueRunState(root);
RequireDeadPropertiesPanelShellDeleted(root);
RequireTypedBehaviorMetadataCoverage(root);
RequireNameToolsUsesUiToolkit(root);
RequireRuntimeStateReaderOwnsUnityStateAcquisition(root);
RequireNoDeadRuntimeProjectionCaches(root);
RequireNoDeadInspectorMetadata(root);
RequireRuntimeSimulationTuningCommits(root);
RequireHullConductivityCommitAuthority(root);
RequireInventoryEntityRenameCommitAuthority(root);
RequireWeaponGroupCommitAuthority(root);
RequireActionBarBindingCommitAuthority(root);
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
Console.WriteLine("Shared Eve package ownership: generic Unity Eve packages import from the neighboring Eve repo instead of local staged copies");
Console.WriteLine("Typed runtime behavior coverage: live behavior kinds have typed factory plus progress/state restore coverage");
Console.WriteLine("Zone construction/runtime key authority: body/orbit wrappers and task shells retain typed keys instead of GUID sidecars");
Console.WriteLine("Eve runtime bootstrap: operations surface mounts through UI Toolkit presenter");
Console.WriteLine("Renderer-local console authority: deleted; UI commands flow through Eve command documents");
Console.WriteLine("Renderer-local debug panels: obsolete uGUI field tester authority is deleted");
Console.WriteLine("Main-menu settings authority: player name, gameplay, and graphics settings return through typed player-settings commits");
Console.WriteLine("Main-menu root shell: root navigation lowers through an Eve UI Toolkit surface instead of the legacy PropertiesPanel/fade shell");
Console.WriteLine("Main-menu settings shell: settings/input subpages lower through Eve UI Toolkit surfaces, and the fake audio page is deleted until a typed audio owner exists");
Console.WriteLine("Confirmation dialog shell: runtime prompts no longer inherit the generic PropertiesPanel machinery");
Console.WriteLine("Main-menu input shell: the Eve input page delegates to the live runtime remap screen when that owner exists");
Console.WriteLine("Runtime input screen shell: input rebinding lowers through an Eve UI Toolkit surface instead of the old drag/drop uGUI screen");
Console.WriteLine("Runtime input-screen authority: hotkey and menu handoff share the same fullscreen-menu primitive");
Console.WriteLine("Sector-map zone details shell: zone inspection lowers through an Eve UI Toolkit surface instead of PropertiesPanel rows");
Console.WriteLine("Runtime menu tab shell: MenuPanel owns tab metadata and lowers navigation through an Eve UI Toolkit surface");
Console.WriteLine("Inventory ship-settings shell: background ship tuning lowers through an Eve UI Toolkit surface instead of PropertiesPanel.AddField");
Console.WriteLine("Inventory cargo-item shell: cargo item inspection lowers through an Eve UI Toolkit surface instead of PropertiesPanel.Inspect");
Console.WriteLine("Inventory equipped-item shell: equipped item inspection and weapon-group controls lower through an Eve UI Toolkit surface instead of PropertiesPanel.Inspect");
Console.WriteLine("Trade cargo-selector shell: target cargo selection lowers through an Eve UI Toolkit surface instead of ContextMenu.AddOption");
Console.WriteLine("Trade filter and row-action shells: filter selection and buy-quantity entry lower through Eve UI Toolkit surfaces instead of ContextMenu dropdowns");
Console.WriteLine("Trade item-details shell: typed item inspection lowers through an Eve UI Toolkit surface instead of PropertiesPanel.Inspect");
Console.WriteLine("Inventory dropdown shell: entity and loadout navigation lowers through an Eve UI Toolkit surface instead of ContextMenu.AddDropdown");
Console.WriteLine("Main-menu Continue authority: Continue selects typed run state instead of a null button");
Console.WriteLine("Generic popup inspector shell: PropertiesPanel, PropertiesList, and DropdownMenu are deleted from source and serialized assets");
Console.WriteLine("Typed behavior metadata authority: live heat/mining/thermotoggle payload kinds stay owned by package metadata");
Console.WriteLine("NameTools editor shell: the remaining name helper window lowers through UI Toolkit instead of IMGUI");
Console.WriteLine("Runtime state reader authority: Unity gameplay/UI read typed state through a shared runtime reader instead of direct store spelunking");
Console.WriteLine("Verse host authority: daemon-owned typed verse host settings now drive provider advertisement, operations telemetry, and served Verse discovery");
Console.WriteLine("Client target boot authority: Unity boot resolves the active Verse through a typed client target instead of local path folklore");
Console.WriteLine("Verse replica authority: remote client targets resolve to cache-only replica .cc files fed from the daemon");
Console.WriteLine("Verse settings shell: client target edits and Verse-host visibility commands lower through typed Eve surfaces");
Console.WriteLine("Main-menu Verse projection: the Unity Eve shell lowers daemon-owned verse identity instead of ad-libbing local menu copy");
Console.WriteLine("Runtime simulation tuning authority: UI writes flow through gameplay checkpoint commits");
Console.WriteLine("Hull conductivity authority: inventory UI toggles flow through gameplay checkpoint commits");
Console.WriteLine("Inventory entity rename authority: UI rename flows through gameplay checkpoint commits");
Console.WriteLine("Weapon group authority: UI assignment flows through gameplay checkpoint commits");
Console.WriteLine("Action-bar binding authority: drag/drop updates and typed run restore flow through gameplay checkpoint state");
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

static void RequireSharedEvePackagesImportedFromEveRepo(string root)
{
    var manifestPath = Path.Combine(root, "Packages", "manifest.json");
    var lockPath = Path.Combine(root, "Packages", "packages-lock.json");
    var unityFacadeProjectPath = Path.Combine(root, "Aetheria.State.Unity", "Aetheria.State.Unity.csproj");
    var localSurfacePath = Path.Combine(root, "Packages", "org.gamecult.eve.surface");
    var localUnityUiToolkitPath = Path.Combine(root, "Packages", "org.gamecult.eve.unity-uitoolkit");
    var evePackagesRoot = Path.GetFullPath(Path.Combine(root, "..", "Eve", "packages"));
    var upstreamSurfacePath = Path.Combine(evePackagesRoot, "org.gamecult.eve.surface", "package.json");
    var upstreamUnityUiToolkitPath = Path.Combine(evePackagesRoot, "org.gamecult.eve.unity-uitoolkit", "package.json");

    var manifest = File.Exists(manifestPath)
        ? File.ReadAllText(manifestPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; manifest.json is missing.");
    var packageLock = File.Exists(lockPath)
        ? File.ReadAllText(lockPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; packages-lock.json is missing.");
    var unityFacadeProject = File.Exists(unityFacadeProjectPath)
        ? File.ReadAllText(unityFacadeProjectPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; Aetheria.State.Unity.csproj is missing.");

    var requiredManifestSymbols = new[]
    {
        "\"org.gamecult.eve.surface\": \"file:../../Eve/packages/org.gamecult.eve.surface\"",
        "\"org.gamecult.eve.unity-uitoolkit\": \"file:../../Eve/packages/org.gamecult.eve.unity-uitoolkit\""
    };

    var missingManifestSymbols = requiredManifestSymbols
        .Where(symbol => !manifest.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingManifestSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria manifest no longer imports the shared Eve Unity packages from the neighboring Eve repo: " +
            string.Join(", ", missingManifestSymbols));
    }

    var requiredLockSymbols = new[]
    {
        "\"version\": \"file:../../Eve/packages/org.gamecult.eve.surface\"",
        "\"version\": \"file:../../Eve/packages/org.gamecult.eve.unity-uitoolkit\"",
        "\"source\": \"local\""
    };

    var missingLockSymbols = requiredLockSymbols
        .Where(symbol => !packageLock.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingLockSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity lockfile no longer records the shared Eve packages as sibling-repo local imports: " +
            string.Join(", ", missingLockSymbols));
    }

    if (Directory.Exists(localSurfacePath) || Directory.Exists(localUnityUiToolkitPath))
    {
        throw new InvalidOperationException(
            "Aetheria should not keep local staged copies of the shared Eve Unity packages under Packages/.");
    }

    if (!File.Exists(upstreamSurfacePath) || !File.Exists(upstreamUnityUiToolkitPath))
    {
        throw new InvalidOperationException(
            "The neighboring Eve repo is missing the shared Unity package roots Aetheria imports.");
    }

    if (!unityFacadeProject.Contains(@"..\..\Eve\packages\org.gamecult.eve.surface\Runtime\EveSurfaceDocument.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.Unity still points at the deleted local Eve surface package path.");
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
        "AetheriaRuntimeClientTargetStore.cs",
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

static void RequireTypedPendingCommitKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateCommitDocument.cs"),
        Path.Combine(root, "Aetheria.State", "AetheriaRuntimeCommitLogApplier.cs")
    };

    var forbiddenSymbols = new[]
    {
        "CorporationLegacyId",
        "OrbitLegacyId",
        "ParentLegacyId",
        "BodyLegacyId",
        "ResourceScannerTargetBodyId { get; set; }",
        "MiningToolAsteroidBeltId { get; set; }",
        "ReferenceKey(entity.FactionKey",
        "ReferenceKey(orbit.OrbitKey",
        "ReferenceKey(body.BodyKey",
        "ReferenceKey(relationship.FactionKey"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Pending runtime commit authority still accepts legacy ID fallback fields: " +
            string.Join("; ", hits));
    }
}

static void RequireTypedRuntimeFactionKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintProjector.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")
    };

    var forbiddenSymbols = new[]
    {
        "public Guid Faction",
        "blueprint.Faction =",
        "blueprint.Faction)",
        "blueprint.Faction,",
        "blueprint.Faction;",
        "ResolveFaction(blueprint.Faction",
        "LegacyId(blueprint.Faction",
        "ReferenceKey(blueprint.FactionKey",
        "CorporationKey(entity.Faction?.ID",
        "CorporationKey(pair.Key.ID",
        "private static string CorporationKey"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime faction commits must use typed FactionKey authority, not legacy GUID fallback: " +
            string.Join("; ", hits));
    }
}

static void RequireTypedGalaxyFactionRelationships(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Corporations.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Galaxy.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "LoadoutGenerator.cs")
    };

    var forbiddenSymbols = new[]
    {
        "Dictionary<Guid, float> Allegiance",
        ".Allegiance[",
        "ContainsFaction(Guid",
        "ResolveFaction(Guid",
        "CorporationLegacyId",
        "factionsByLegacyId",
        "_containedFactions"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Galaxy faction relationships must use typed corporation keys, not legacy GUID allegiance state: " +
            string.Join("; ", hits));
    }
}

static void RequireRuntimeCatalogKeyOnlyLookups(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs"),
        Path.Combine(root, "Aetheria.State.Unity.Smoke", "Program.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "ItemManager.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")
    };

    var forbiddenSymbols = new[]
    {
        "IRuntimeItemCatalogReader",
        "AetheriaRuntimeItemCatalog",
        "_itemsByLegacyId",
        "_corporationsByLegacyId",
        "_nameFilesByLegacyId",
        "FindItemByLegacyId",
        "FindCorporationByLegacyId",
        "FindNameFileByLegacyId",
        "public string LegacyId { get; }",
        "public string ManufacturerLegacyId",
        "public string GeonameFileLegacyId",
        "public string BossHullLegacyId",
        "public string CorporationLegacyId",
        ".ManufacturerLegacyId",
        ".GeonameFileLegacyId",
        ".BossHullLegacyId",
        ".CorporationLegacyId"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime catalog lookup authority must stay on the typed snapshot; legacy-ID indexes and redundant catalog adapters belong to migration boundaries: " +
            string.Join("; ", hits));
    }
}

static void RequireTypedBehaviorBodyKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs"),
        Path.Combine(root, "Aetheria.State", "AetheriaRuntimeCommitLogApplier.cs"),
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs"),
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs"),
        Path.Combine(root, "Aetheria.State.Smoke", "Program.cs"),
        Path.Combine(root, "Aetheria.State.Unity.Smoke", "Program.cs")
    };

    var forbiddenSymbols = new[]
    {
        "ResourceScannerTargetBodyId",
        "MiningToolAsteroidBeltId",
        "ParseLegacyGuidFromReferenceKey",
        "ParseLegacyIdFromReferenceKey",
        "ParseBodyGuidFromKey(",
        "private static string OrbitKey(Guid",
        "private static string BodyKey(Guid",
        "$\"aetheria.orbit:legacy:",
        "$\"aetheria.body:legacy:",
        "public Guid ScanTarget",
        "public Guid AsteroidBelt",
        "RestoreRuntimeState(Guid scanTarget",
        "RestoreRuntimeState(Guid asteroidBelt"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime behavior body references must stay on typed BodyKey surfaces, with Zone owning key resolution and behaviors no longer storing raw GUID body ids: " +
            string.Join("; ", hits));
    }

    var zoneSourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneSource = File.Exists(zoneSourcePath)
        ? File.ReadAllText(zoneSourcePath)
        : throw new InvalidOperationException("Cannot verify body/orbit key ownership; Zone source is missing.");
    var requiredZoneSymbols = new[]
    {
        "public const string OrbitKeyPrefix",
        "public const string BodyKeyPrefix",
        "public string BodyKey { get; }",
        "public string OrbitKey { get; }",
        "public string ParentOrbitKey { get; }",
        "public bool TryGetPlanet(string bodyKey, out Planet planet)",
        "public bool TryGetAsteroidBelt(string bodyKey, out AsteroidBelt belt)"
    };
    var missingZoneSymbols = requiredZoneSymbols
        .Where(symbol => !zoneSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingZoneSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone runtime body/orbit objects must own typed projection key surfaces: " +
            string.Join(", ", missingZoneSymbols));
    }

    var requiredBehaviorSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "ResourceScanner.cs")] = new[]
        {
            "private string _scanTargetBodyKey = \"\";",
            "public string ScanTargetBodyKey",
            "Entity.Zone.TryGetAsteroidBelt(ScanTargetBodyKey, out var belt)",
            "Entity.Zone.TryGetPlanet(ScanTargetBodyKey, out var planet)",
            "RestoreRuntimeState(",
            "string scanTargetBodyKey"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "MiningTool.cs")] = new[]
        {
            "public string AsteroidBeltBodyKey = \"\";",
            "Entity.Zone.TryGetAsteroidBelt(AsteroidBeltBodyKey, out var belt)",
            "Entity.Zone.AsteroidExists(AsteroidBeltBodyKey, Asteroid)",
            "Entity.Zone.MineAsteroid(",
            "AsteroidBeltBodyKey,",
            "string asteroidBeltBodyKey"
        }
    };
    var missingBehaviorSymbols = requiredBehaviorSymbols
        .Where(pair => !File.Exists(pair.Key) || pair.Value.Any(symbol => !File.ReadAllText(pair.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(pair =>
        {
            var text = File.Exists(pair.Key) ? File.ReadAllText(pair.Key) : "";
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();
    if (missingBehaviorSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ResourceScanner and MiningTool must own typed body-key runtime references rather than raw GUID fields: " +
            string.Join("; ", missingBehaviorSymbols));
    }
}

static void RequireNoDeadRuntimeProjectionCaches(string root)
{
    var path = Path.Combine(root, "Assets", "Scripts", "ServerShared", "RuntimeProjection", "ReflectionExtensions.cs");
    if (!File.Exists(path))
        return;

    var forbiddenSymbols = new[]
    {
        "GetAllInterfaceClasses(",
        "GetParentTypes(",
        "GetAllChildClasses(",
        "GetAllGenericChildClasses(",
        "IsAssignableToGenericType(",
        "GetFullName(",
        "InterfaceClasses",
        "ParentTypes",
        "ChildClasses",
        "GenericChildClasses",
        "AppDomain.CurrentDomain.GetAssemblies()"
    };

    var hits = File.ReadLines(path)
        .Select((line, index) => new { Line = line, LineNumber = index + 1 })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"Assets/Scripts/ServerShared/RuntimeProjection/ReflectionExtensions.cs:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Dead runtime-projection reflection caches should stay deleted; keep only the live string-formatting helpers: " +
            string.Join("; ", hits));
    }
}

static void RequireNoDeadInspectorMetadata(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "FieldDriver.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Corporations.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "AetherDrive.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "StatModifier.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "VelocityLimit.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "ExponentialCurve.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "RuntimeGeometry.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Properties Panel", "PropertiesPanel.cs")
    };

    var forbiddenSymbols = new[]
    {
        "InspectableAttribute",
        "PreferredInspectorAttribute",
        "InspectableTextAttribute",
        "InspectablePrefabAttribute",
        "InspectableTextureAttribute",
        "InspectableTextAssetAttribute",
        "InspectableTemperatureAttribute",
        "InspectableAnimationCurveAttribute",
        "InspectableColorAttribute",
        "InspectableSoundBankAttribute",
        "InspectableAudioParameterAttribute",
        "InspectableSchematicShapeAttribute",
        "InspectableEnumValuesAttribute",
        "InspectableRangedFloatAttribute",
        "InspectableRangedIntAttribute",
        "OrderAttribute",
        "EntityTypeRestrictionAttribute",
        "InspectorHeaderAttribute",
        "[Inspectable",
        "[InspectableText",
        "[InspectablePrefab",
        "[InspectableTexture",
        "[InspectableTextAsset",
        "[InspectableTemperature",
        "[InspectableAnimationCurve",
        "[InspectableColor",
        "[InspectableSoundBank",
        "[InspectableAudioParameter",
        "[InspectableSchematicShape",
        "[InspectableEnumValues",
        "[InspectableRangedFloat",
        "[InspectableRangedInt",
        "[Order(",
        "EntityTypeRestriction(",
        "[InspectorHeader("
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Dead inspector-specialization metadata should stay deleted; no generic Inspectable attribute path should remain in live runtime code: " +
            string.Join("; ", hits));
    }

    var deletedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "RuntimeProjection", "Attributes.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Attributes.cs")
    };

    var stillPresent = deletedFiles.Where(File.Exists).Select(path => Path.GetRelativePath(root, path)).ToArray();
    if (stillPresent.Length > 0)
    {
        throw new InvalidOperationException(
            "Dead inspector attribute definition files should be deleted once the generic reflection inspector path is gone: " +
            string.Join(", ", stillPresent));
    }
}

static void RequireTypedOrbitTaskKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "States", "MoveTo.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "PatrolOrbits.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "StationTowing.cs")
    };

    var forbiddenSymbols = new[]
    {
        "public Guid Orbit { get; set; }",
        "Guid[] Circuit",
        "public Guid OrbitParent",
        "Select(x => x.Key)"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Orbit-targeting agent tasks and move states must stay on typed OrbitKey surfaces, with Zone owning orbit-key resolution: " +
            string.Join("; ", hits));
    }

    var zoneSourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneSource = File.Exists(zoneSourcePath)
        ? File.ReadAllText(zoneSourcePath)
        : throw new InvalidOperationException("Cannot verify orbit-key task ownership; Zone source is missing.");
    var requiredZoneSymbols = new[]
    {
        "public bool TryGetOrbit(string orbitKey, out Orbit orbit)",
        "public float2 GetOrbitPosition(string orbitKey)"
    };
    var missingZoneSymbols = requiredZoneSymbols
        .Where(symbol => !zoneSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingZoneSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone must own typed orbit-key parsing and resolution for agent/runtime orbit movement: " +
            string.Join(", ", missingZoneSymbols));
    }

    var requiredTaskSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "States", "MoveTo.cs")] = new[]
        {
            "public string OrbitKey { get; set; } = \"\";",
            "_agent.Ship.Zone.GetOrbitPosition(OrbitKey)"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "PatrolOrbits.cs")] = new[]
        {
            "public string[] Circuit = Array.Empty<string>();",
            "public string CurrentTarget",
            "patrolMoveState.OrbitKey = CurrentTarget"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "StationTowing.cs")] = new[]
        {
            "public string OrbitParentKey = \"\";"
        }
    };
    var missingTaskSymbols = requiredTaskSymbols
        .Where(pair => !File.Exists(pair.Key) || pair.Value.Any(symbol => !File.ReadAllText(pair.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(pair =>
        {
            var text = File.Exists(pair.Key) ? File.ReadAllText(pair.Key) : "";
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();
    if (missingTaskSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Orbit-targeting agent tasks must retain typed orbit keys rather than raw GUIDs: " +
            string.Join("; ", missingTaskSymbols));
    }
}

static void RequireTypedAgentTaskKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "AgentTask.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "Survey.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "Mining.cs")
    };

    var forbiddenSymbols = new[]
    {
        "public Guid Zone",
        "List<Guid> Planets",
        "public Guid Asteroids"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Agent task shells must retain typed zone/body keys rather than raw GUID fields: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "AgentTask.cs")] = new[]
        {
            "public string ZoneKey = \"\";"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "Survey.cs")] = new[]
        {
            "public List<string> PlanetBodyKeys;"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Agents", "Tasks", "Mining.cs")] = new[]
        {
            "public string AsteroidBeltBodyKey = \"\";"
        }
    };

    var missing = requiredSymbols
        .Where(entry => !File.Exists(entry.Key) ||
                        entry.Value.Any(symbol => !File.ReadAllText(entry.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(entry => entry.Value
            .Where(symbol => !File.Exists(entry.Key) || !File.ReadAllText(entry.Key).Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{Path.GetRelativePath(root, entry.Key)}: missing {symbol}"))
        .ToArray();

    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            "Agent task shells must keep typed key placeholders on zone/body references: " +
            string.Join("; ", missing));
    }
}

static void RequireTypedOrbitalEntityOrbitKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "OrbitalEntity.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintProjector.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "LoadoutGenerator.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "ZoneGenerator.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs")
    };

    var forbiddenSymbols = new[]
    {
        "public Guid OrbitId;",
        "public Guid Orbit;",
        "Orbit = orbital.OrbitId",
        "blueprint.Orbit,",
        "Zone.Orbits[orbital.OrbitId]",
        "GetOrbitPosition(OrbitId)",
        "GetOrbitVelocity(OrbitId)",
        "new OrbitalEntity(ItemManager, null, hull, Guid.Empty"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Orbital runtime entities and construction blueprints must retain typed OrbitKey state, with Zone owning orbit-key resolution: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "OrbitalEntity.cs")] = new[]
        {
            "public string OrbitKey = \"\";",
            "OrbitKey = orbitKey ?? \"\";",
            "Zone.GetOrbitPosition(OrbitKey)",
            "Zone.GetOrbitVelocity(OrbitKey)"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintProjector.cs")] = new[]
        {
            "OrbitKey = orbital.OrbitKey,",
            "blueprint.OrbitKey",
            "public string OrbitKey = \"\";"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "LoadoutGenerator.cs")] = new[]
        {
            "new OrbitalEntity(ItemManager, null, hull, \"\", ItemManager.GameplaySettings.DefaultEntitySettings)"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "ZoneGenerator.cs")] = new[]
        {
            "turret.OrbitKey = turretOrbit.OrbitKey;",
            "station.OrbitKey = lagrangeOrbit.OrbitKey;"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")] = new[]
        {
            "Zone.TryGetOrbit(orbital.OrbitKey, out var orbit)",
            "planet => planet.OrbitKey == parentOrbitKey"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs")] = new[]
        {
            "public float2 GetOrbitVelocity(string orbitKey)"
        }
    };

    var missingSymbols = requiredSymbols
        .Where(pair => !File.Exists(pair.Key) || pair.Value.Any(symbol => !File.ReadAllText(pair.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(pair =>
        {
            var text = File.Exists(pair.Key) ? File.ReadAllText(pair.Key) : "";
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Orbital runtime seam must stay on typed orbit keys through the entity owner, blueprint projector, and docking path: " +
            string.Join("; ", missingSymbols));
    }
}

static void RequireTypedOrbitConsumerKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs"),
        Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "ResourceScanner.cs")
    };

    var forbiddenSymbols = new[]
    {
        "planet => planet.OrbitId == followOrbit",
        "planet => planet.OrbitId == rootOrbit",
        "body => body.OrbitId == orbit.ID",
        "body => body.Orbit == orbit.ID",
        "GetOrbitPosition(planetInstance.OrbitId)",
        "Zone.Orbits[planetInstance.OrbitId].Parent",
        "zone.Orbits[belt.Orbit]",
        "GetOrbitPosition(planet.OrbitId)"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Orbit consumer edges must read typed OrbitKey surfaces rather than wrapper GUID fields in gameplay, renderer, and sensor code: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")] = new[]
        {
            "Zone.Orbits.Values.MinBy(orbit => lengthsq(Zone.GetOrbitPosition(orbit.OrbitKey) - entityPosition))",
            "planet => planet.OrbitKey == followOrbit.OrbitKey",
            "planet => planet.OrbitKey == rootOrbit.OrbitKey",
            "Zone.TryGetOrbit(rootOrbit.ParentOrbitKey, out var parentOrbit)",
            "Zone.GetOrbitPosition(followOrbit.OrbitKey)",
            "Zone.GetOrbitPosition(rootOrbit.OrbitKey)"
        },
        [Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs")] = new[]
        {
            "body => body.OrbitKey == orbit.OrbitKey",
            "Zone.GetOrbitPosition(planetInstance.OrbitKey)",
            "Zone.GetOrbitPosition(planetInstance.Orbit.ParentOrbitKey)"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Behaviors", "ResourceScanner.cs")] = new[]
        {
            "Entity.Zone.GetOrbitPosition(planet.OrbitKey)"
        }
    };

    var missingSymbols = requiredSymbols
        .Where(pair => !File.Exists(pair.Key) || pair.Value.Any(symbol => !File.ReadAllText(pair.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(pair =>
        {
            var text = File.Exists(pair.Key) ? File.ReadAllText(pair.Key) : "";
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Orbit consumer edges must stay keyed at the public runtime wrapper surface: " +
            string.Join("; ", missingSymbols));
    }
}

static void RequireKeyedOrbitRuntimeWrappers(string root)
{
    var zoneSourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneSource = File.Exists(zoneSourcePath)
        ? File.ReadAllText(zoneSourcePath)
        : throw new InvalidOperationException("Cannot verify keyed orbit runtime wrappers; Zone source is missing.");

    var forbiddenSymbols = new[]
    {
        "public Guid OrbitId { get; }",
        "public Guid Orbit { get; }",
        "public Guid ID { get; }",
        "OrbitId = data.Orbit;",
        "Orbit = data.Orbit;",
        "AsteroidBelts[belt.ID] = new AsteroidBelt(belt);",
        "var orbit = Orbits[belt.Orbit];"
    };
    var hits = forbiddenSymbols
        .Where(symbol => zoneSource.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/ServerShared/Zone.cs: contains {symbol}")
        .ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Planet and asteroid-belt runtime wrappers must not publish wrapper orbit GUID fields once keyed/object orbit surfaces exist: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new[]
    {
        "public Orbit Orbit;",
        "public Orbit Orbit { get; }",
        "var runtimeBelt = new AsteroidBelt(belt, Orbits[belt.OrbitKey]);",
        "AsteroidBelts[runtimeBelt.BodyKey] = runtimeBelt;",
        "belt.NewOrbitPosition = GetOrbitPosition(belt.Orbit.ParentOrbitKey);",
        "public AsteroidBelt(AsteroidBeltConstructionData data, Orbit orbit)",
        "Orbit = orbit;"
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !zoneSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Planet and asteroid-belt runtime wrappers must keep orbit authority on Orbit objects plus OrbitKey surfaces: " +
            string.Join(", ", missingSymbols));
    }
}

static void RequireTypedFactionShellLinks(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Corporations.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Galaxy.cs")
    };

    var forbiddenSymbols = new[]
    {
        "Guid GeonameFile",
        "Guid BossHull",
        "GeonameFile =",
        "BossHull =",
        "ParseOptionalLegacyId",
        "GeonameFileLegacyId",
        "BossHullLegacyId",
        ".BossHull != Guid.Empty",
        "public Guid ID",
        "ID = ParseLegacyId",
        "ParseLegacyId(corporation.LegacyId"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Temporary Faction shell links must use typed key fields, not geoname/boss-hull legacy GUID projections: " +
            string.Join("; ", hits));
    }
}

static void RequireNativeZoneKeyResolution(string root)
{
    var zoneSourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneSource = File.Exists(zoneSourcePath)
        ? File.ReadAllText(zoneSourcePath)
        : throw new InvalidOperationException("Cannot verify native Zone key resolution; Zone source is missing.");

    var forbiddenSymbols = new[]
    {
        "public static Guid ParseOrbitGuid(string orbitKey)",
        "public static Guid ParseBodyGuid(string bodyKey)",
        "PlanetInstances.TryGetValue(ParseBodyGuid(bodyKey), out planet)",
        "AsteroidBelts.TryGetValue(ParseBodyGuid(bodyKey), out belt)",
        "Orbits.TryGetValue(ParseOrbitGuid(orbitKey), out orbit)",
        "return GetOrbitPosition(ParseOrbitGuid(orbitKey));",
        "return GetOrbitVelocity(ParseOrbitGuid(orbitKey));",
        "public float2 GetOrbitPosition(Guid orbitID)",
        "public float2 GetOrbitVelocity(Guid orbit)",
        "public string GetBodyKey(Guid bodyId)",
        "private readonly Dictionary<Guid, Planet> _planetsById = new Dictionary<Guid, Planet>();",
        "private readonly Dictionary<Guid, AsteroidBelt> _asteroidBeltsById = new Dictionary<Guid, AsteroidBelt>();"
    };
    var hits = forbiddenSymbols
        .Where(symbol => zoneSource.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/ServerShared/Zone.cs: contains {symbol}")
        .ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone key-facing APIs must resolve typed body/orbit keys natively instead of reparsing GUIDs from those keys: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new[]
    {
        "public Dictionary<string, Planet> PlanetInstances = new Dictionary<string, Planet>(StringComparer.Ordinal);",
        "public Dictionary<string, Orbit> Orbits = new Dictionary<string, Orbit>(StringComparer.Ordinal);",
        "public Dictionary<string, AsteroidBelt> AsteroidBelts = new Dictionary<string, AsteroidBelt>(StringComparer.Ordinal);",
        "PlanetInstances[runtimeSun.BodyKey] = runtimeSun;",
        "PlanetInstances[runtimeGas.BodyKey] = runtimeGas;",
        "PlanetInstances[runtimePlanet.BodyKey] = runtimePlanet;",
        "AsteroidBelts[runtimeBelt.BodyKey] = runtimeBelt;",
        "Orbits[runtimeOrbit.OrbitKey] = runtimeOrbit;",
        "return PlanetInstances.TryGetValue(bodyKey ?? \"\", out planet);",
        "return AsteroidBelts.TryGetValue(bodyKey ?? \"\", out belt);",
        "return Orbits.TryGetValue(orbitKey ?? \"\", out orbit);",
        "var parentPosition = string.IsNullOrWhiteSpace(orbit.ParentOrbitKey)",
        ": GetOrbitPosition(orbit.ParentOrbitKey);",
        "return TryGetOrbit(orbitKey, out var orbit) ? orbit.Velocity : float2.zero;",
        "var runtimeBelt = new AsteroidBelt(belt, Orbits[belt.OrbitKey]);",
        "var runtimeSun = new Sun(settings, sun, Orbits[planet.OrbitKey]);",
        "var runtimeGas = new GasGiant(settings, gas, Orbits[planet.OrbitKey]);",
        "var runtimePlanet = new Planet(settings, planet, Orbits[planet.OrbitKey]);",
        "BodyKey = data.BodyKey ?? \"\";",
        "OrbitKey = data.OrbitKey ?? \"\";",
        "ParentOrbitKey = data.ParentOrbitKey ?? \"\";"
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !zoneSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone must keep native key-indexed runtime lookup tables for body and orbit resolution: " +
            string.Join(", ", missingSymbols));
    }
}

static void RequireTypedZoneRuntimeCollections(string root)
{
    var zoneSourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneRendererPath = Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs");
    var zoneSource = File.Exists(zoneSourcePath)
        ? File.ReadAllText(zoneSourcePath)
        : throw new InvalidOperationException("Cannot verify typed Zone runtime collections; Zone source is missing.");
    var zoneRenderer = File.Exists(zoneRendererPath)
        ? File.ReadAllText(zoneRendererPath)
        : throw new InvalidOperationException("Cannot verify typed Zone runtime collections; ZoneRenderer source is missing.");

    var forbiddenZoneSymbols = new[]
    {
        "public Dictionary<Guid, Planet> PlanetInstances",
        "public Dictionary<Guid, Orbit> Orbits",
        "public Dictionary<Guid, AsteroidBelt> AsteroidBelts",
        "private HashSet<Guid> _updatedOrbits",
        "orbit.Value.Position = GetOrbitPosition(orbit.Key);"
    };
    var zoneHits = forbiddenZoneSymbols
        .Where(symbol => zoneSource.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/ServerShared/Zone.cs: contains {symbol}");

    var forbiddenRendererSymbols = new[]
    {
        "public Dictionary<Guid, PlanetObject> Planets",
        "private Dictionary<Guid, AsteroidBeltUI> _beltObjects",
        "private Dictionary<Guid, InstancedMesh[]> _beltMeshes",
        "private Dictionary<Guid, Matrix4x4[][]> _beltMatrices",
        "_beltMeshes[runtimeBelt.ID] =",
        "_beltObjects[runtimeBelt.ID] =",
        "Planets[planetInstance.ID] = planet;"
    };
    var rendererHits = forbiddenRendererSymbols
        .Where(symbol => zoneRenderer.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/Zone Display/ZoneRenderer.cs: contains {symbol}");

    var hits = zoneHits.Concat(rendererHits).ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone runtime collections and renderer caches must keep typed OrbitKey/BodyKey ownership rather than Guid dictionary keys: " +
            string.Join("; ", hits));
    }

    var requiredZoneSymbols = new[]
    {
        "public Dictionary<string, Planet> PlanetInstances = new Dictionary<string, Planet>(StringComparer.Ordinal);",
        "public Dictionary<string, Orbit> Orbits = new Dictionary<string, Orbit>(StringComparer.Ordinal);",
        "public Dictionary<string, AsteroidBelt> AsteroidBelts = new Dictionary<string, AsteroidBelt>(StringComparer.Ordinal);",
        "private HashSet<string> _updatedOrbits = new HashSet<string>(StringComparer.Ordinal);",
        "foreach (var orbit in Orbits.Values)",
        "orbit.Position = GetOrbitPosition(orbit.OrbitKey);"
    };
    var missingZoneSymbols = requiredZoneSymbols
        .Where(symbol => !zoneSource.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/ServerShared/Zone.cs: missing {symbol}");

    var requiredRendererSymbols = new[]
    {
        "public Dictionary<string, PlanetObject> Planets = new Dictionary<string, PlanetObject>(StringComparer.Ordinal);",
        "private Dictionary<string, AsteroidBeltUI> _beltObjects = new Dictionary<string, AsteroidBeltUI>(StringComparer.Ordinal);",
        "private Dictionary<string, InstancedMesh[]> _beltMeshes = new Dictionary<string, InstancedMesh[]>(StringComparer.Ordinal);",
        "private Dictionary<string, Matrix4x4[][]> _beltMatrices = new Dictionary<string, Matrix4x4[][]>(StringComparer.Ordinal);",
        "_beltMeshes[runtimeBelt.BodyKey] = meshes.ToArray();",
        "_beltObjects[runtimeBelt.BodyKey] = belt;",
        "Planets[planetInstance.BodyKey] = planet;"
    };
    var missingRendererSymbols = requiredRendererSymbols
        .Where(symbol => !zoneRenderer.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/Zone Display/ZoneRenderer.cs: missing {symbol}");

    var missing = missingZoneSymbols.Concat(missingRendererSymbols).ToArray();
    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone runtime collections and renderer caches must expose typed keyed ownership end to end: " +
            string.Join("; ", missing));
    }
}

static void RequireTypedZoneConstructionKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "ZoneConstructionData.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "ZoneGenerator.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs")
    };

    var forbiddenSymbols = new[]
    {
        "var runtimeBelt = new AsteroidBelt(belt, Orbits[OrbitKey(belt.Orbit)]);",
        "var runtimeSun = new Sun(settings, sun, Orbits[OrbitKey(planet.Orbit)]);",
        "var runtimeGas = new GasGiant(settings, gas, Orbits[OrbitKey(planet.Orbit)]);",
        "var runtimePlanet = new Planet(settings, planet, Orbits[OrbitKey(planet.Orbit)]);",
        "turret.OrbitKey = Zone.OrbitKey(turretOrbit.ID);",
        "station.OrbitKey = Zone.OrbitKey(lagrangeOrbit.ID);",
        "public Guid ID = Guid.NewGuid();",
        "public Guid Orbit;",
        "public Guid Parent;",
        "data.OrbitKey = Zone.OrbitKey(data.ID);",
        "planetData.BodyKey = Zone.BodyKey(planetData.ID);",
        "lagrangeOrbit.OrbitKey = Zone.OrbitKey(lagrangeOrbit.ID);",
        "turretOrbit.OrbitKey = Zone.OrbitKey(turretOrbit.ID);"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone construction and generation must hand orbit/body identity through typed key fields instead of recomputing runtime authority from legacy GUID refs: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "ZoneConstructionData.cs")] = new[]
        {
            "public string BodyKey = \"\";",
            "public string OrbitKey = \"\";",
            "public string ParentOrbitKey = \"\";"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "ZoneGenerator.cs")] = new[]
        {
            "OrbitKey = Zone.OrbitKey(Guid.NewGuid()),",
            "data.ParentOrbitKey = orbitInverseMap[data].Parent != null",
            "planetData.BodyKey = Zone.BodyKey(Guid.NewGuid());",
            "planetData.OrbitKey = orbitMap[planet].OrbitKey;",
            "planetData.Name = bodyName.Substring(0, Math.Min(8, bodyName.Length));",
            "var bodyName = planetData.BodyKey.Substring(planetData.BodyKey.LastIndexOf(':') + 1);",
            "OrbitKey = Zone.OrbitKey(Guid.NewGuid()),",
            "ParentOrbitKey = baseOrbit.ParentOrbitKey,",
            "ParentOrbitKey = orbit.ParentOrbitKey,",
            "turret.OrbitKey = turretOrbit.OrbitKey;",
            "station.OrbitKey = lagrangeOrbit.OrbitKey;"
        },
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs")] = new[]
        {
            "var runtimeBelt = new AsteroidBelt(belt, Orbits[belt.OrbitKey]);",
            "var runtimeSun = new Sun(settings, sun, Orbits[planet.OrbitKey]);",
            "var runtimeGas = new GasGiant(settings, gas, Orbits[planet.OrbitKey]);",
            "var runtimePlanet = new Planet(settings, planet, Orbits[planet.OrbitKey]);",
            "BodyKey = data.BodyKey ?? \"\";",
            "OrbitKey = data.OrbitKey ?? \"\";",
            "ParentOrbitKey = data.ParentOrbitKey ?? \"\";"
        }
    };

    var missingSymbols = requiredSymbols
        .Where(pair => !File.Exists(pair.Key) || pair.Value.Any(symbol => !File.ReadAllText(pair.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(pair =>
        {
            var text = File.Exists(pair.Key) ? File.ReadAllText(pair.Key) : "";
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone construction inputs and generator seams must retain typed orbit/body keys as the handoff authority: " +
            string.Join("; ", missingSymbols));
    }
}

static void RequireTypedZoneStateSnapshotKeys(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs"),
        Path.Combine(root, "Aetheria.State", "AetheriaRuntimeCommitLogApplier.cs"),
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs"),
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs")
    };

    var forbiddenSymbols = new[]
    {
        "public string OrbitId { get; set; } = \"\";",
        "public string ParentId { get; set; } = \"\";",
        "public string BodyId { get; set; } = \"\";",
        "public AetheriaRuntimeOrbitSnapshot(string orbitId, string parentId",
        "string bodyId,",
        "string orbitId,",
        "OrbitId = orbit.OrbitKey ?? \"\",",
        "ParentId = orbit.ParentOrbitKey ?? \"\",",
        "BodyId = body.BodyKey ?? \"\",",
        "var orbitId = ReadFieldString(ref reader, orbitFields, 0);",
        "var parentId = ReadFieldString(ref reader, orbitFields, 1);",
        "var bodyId = ReadFieldString(ref reader, bodyFields, 0);",
        "var orbitId = ReadFieldString(ref reader, bodyFields, 3);"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed zone-state documents and runtime snapshots must name orbit/body identity as keys, not legacy ids: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs")] = new[]
        {
            "public string OrbitKey { get; set; } = \"\";",
            "public string ParentOrbitKey { get; set; } = \"\";",
            "public string BodyKey { get; set; } = \"\";"
        },
        [Path.Combine(root, "Aetheria.State", "AetheriaRuntimeCommitLogApplier.cs")] = new[]
        {
            "OrbitKey = orbit.OrbitKey ?? \"\",",
            "ParentOrbitKey = orbit.ParentOrbitKey ?? \"\",",
            "BodyKey = body.BodyKey ?? \"\",",
            "OrbitKey = body.OrbitKey ?? \"\","
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs")] = new[]
        {
            "public AetheriaRuntimeOrbitSnapshot(string orbitKey, string parentOrbitKey",
            "OrbitKey = orbitKey;",
            "ParentOrbitKey = parentOrbitKey;",
            "public string OrbitKey { get; }",
            "public string ParentOrbitKey { get; }",
            "BodyKey = bodyKey;",
            "OrbitKey = orbitKey;",
            "public string BodyKey { get; }",
            "public string OrbitKey { get; }"
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs")] = new[]
        {
            "var orbitKey = ReadFieldString(ref reader, orbitFields, 0);",
            "var parentOrbitKey = ReadFieldString(ref reader, orbitFields, 1);",
            "var bodyKey = ReadFieldString(ref reader, bodyFields, 0);",
            "var orbitKey = ReadFieldString(ref reader, bodyFields, 3);",
            "new AetheriaRuntimeOrbitSnapshot(",
            "new AetheriaRuntimeBodySnapshot("
        }
    };

    var missingSymbols = requiredSymbols
        .Where(pair => !File.Exists(pair.Key) || pair.Value.Any(symbol => !File.ReadAllText(pair.Key).Contains(symbol, StringComparison.Ordinal)))
        .SelectMany(pair =>
        {
            var text = File.Exists(pair.Key) ? File.ReadAllText(pair.Key) : "";
            return pair.Value
                .Where(symbol => !text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: missing {symbol}");
        })
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed zone-state documents and runtime catalog readers must expose orbit/body key ownership explicitly: " +
            string.Join("; ", missingSymbols));
    }
}

static void RequireTypedAsteroidZoneApi(string root)
{
    var zoneSourcePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneSource = File.Exists(zoneSourcePath)
        ? File.ReadAllText(zoneSourcePath)
        : throw new InvalidOperationException("Cannot verify typed asteroid Zone API; Zone source is missing.");

    var forbiddenSymbols = new[]
    {
        "public int NearestAsteroid(Guid planetDataID, float2 position)",
        "public bool AsteroidExists(Guid planetDataID, int asteroid)",
        "public void MineAsteroid(Entity miner, Guid asteroidBelt, int asteroid, float damage, float efficiency, float penetration)",
        "BeltUpdates.Add(Task.Run(() => UpdateAsteroidTransforms(belt.Key)));",
        "private void UpdateAsteroidTransforms(Guid planetDataID)",
        "MineAsteroid(miner, belt.ID, asteroid, damage, efficiency, penetration);"
    };
    var hits = forbiddenSymbols
        .Where(symbol => zoneSource.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Assets/Scripts/ServerShared/Zone.cs: contains {symbol}")
        .ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone asteroid helpers must keep typed body-key or runtime-object authority rather than Guid overloads: " +
            string.Join("; ", hits));
    }

    var requiredSymbols = new[]
    {
        "foreach (var belt in AsteroidBelts.Values)",
        "BeltUpdates.Add(Task.Run(() => UpdateAsteroidTransforms(belt)));",
        "public int NearestAsteroid(string asteroidBeltKey, float2 position)",
        "if (!TryGetAsteroidBelt(asteroidBeltKey, out var belt))",
        "public bool AsteroidExists(string asteroidBeltKey, int asteroid)",
        "private void UpdateAsteroidTransforms(AsteroidBelt belt)",
        "public void MineAsteroid(Entity miner, string asteroidBeltKey, int asteroid, float damage, float efficiency, float penetration)"
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !zoneSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Zone asteroid helpers must route through typed body keys or runtime belt objects: " +
            string.Join(", ", missingSymbols));
    }
}

static void RequireFactionKeyIdentity(string root)
{
    var checkedFiles = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Corporations.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Entity.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Galaxy.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "Narrative", "ZoneConstraints.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorMap.cs")
    };

    var forbiddenSymbols = new[]
    {
        "ID.GetHashCode",
        "faction.ID == ID",
        "Faction.ID ==",
        "faction.ID ==",
        ".ID == TargetFaction",
        ".ID == GalaxyZone.Owner",
        ".ID != zone.Owner",
        "pair.Key.ID",
        "f.ID != zone.Owner",
        "Owner.ID ==",
        "zone.Owner == adjacentZone.Owner",
        "zone.Owner != mega"
    };

    var hits = checkedFiles
        .Where(File.Exists)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Faction identity must compare and order by FactionKey; Faction.ID is temporary projection residue only: " +
            string.Join("; ", hits));
    }
}

static void RequireTypedRuntimeBehaviorCoverage(string root)
{
    var itemManagerPath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "ItemManager.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");

    var itemManager = File.Exists(itemManagerPath)
        ? File.ReadAllText(itemManagerPath)
        : throw new InvalidOperationException("Cannot verify typed runtime behavior coverage; ItemManager.cs is missing.");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify typed runtime behavior coverage; ActionGameManager.cs is missing.");

    var requiredFactoryMappings = new[]
    {
        "case \"AetherDrive\": return new AetherDrive(definition, item);",
        "case \"AutoWeapon\": return new AutoWeapon(definition, item);",
        "case \"Capacitor\": return new Capacitor(definition, item);",
        "case \"ChargedWeapon\": return new ChargedWeapon(definition, item);",
        "case \"Cockpit\": return new Cockpit(definition, item);",
        "case \"Cooldown\": return new Cooldown(definition, item);",
        "case \"ConstantWeapon\": return new ConstantWeapon(definition, item);",
        "case \"EnergyDraw\": return new EnergyDraw(definition, item);",
        "case \"GuidedWeapon\": return new InstantWeapon(definition, item);",
        "case \"Heat\": return new Heat(definition, item);",
        "case \"HeatStorage\": return new HeatStorage(definition, item);",
        "case \"InstantWeapon\": return new InstantWeapon(definition, item);",
        "case \"ItemUsage\": return new ItemUsage(definition, item);",
        "case \"Launcher\": return new LockWeapon(definition, item);",
        "case \"LockWeapon\": return new LockWeapon(definition, item);",
        "case \"MiningTool\": return new MiningTool(definition, item);",
        "case \"Radiator\": return new Radiator(definition, item);",
        "case \"Reactor\": return new Reactor(definition, item);",
        "case \"Reflector\": return new Reflector(definition, item);",
        "case \"ResourceScanner\": return new ResourceScanner(definition, item);",
        "case \"Sensor\": return new Sensor(definition, item);",
        "case \"Shield\": return new Shield(definition, item);",
        "case \"StatModifier\": return new StatModifier(definition, item);",
        "case \"Switch\": return new Switch(definition, item);",
        "case \"Thermotoggle\": return new Thermotoggle(definition, item);",
        "case \"Thruster\": return new Thruster(definition, item);",
        "case \"Trigger\": return new Trigger(definition, item);",
        "case \"TurretController\": return new TurretController(definition, item);",
        "case \"VelocityConversion\": return new VelocityConversion(definition, item);",
        "case \"VelocityLimit\": return new VelocityLimit(definition, item);",
        "case \"Visibility\": return new Visibility(definition, item);",
        "case \"Wear\": return new Wear(definition, item);",
        "case \"AetherDrive\": return new AetherDrive(definition, effect);",
        "case \"AutoWeapon\": return new InstantWeapon(definition, effect);",
        "case \"Capacitor\": return new Capacitor(definition, effect);",
        "case \"ChargedWeapon\": return new ChargedWeapon(definition, effect);",
        "case \"Cockpit\": return new Cockpit(definition, effect);",
        "case \"Cooldown\": return new Cooldown(definition, effect);",
        "case \"ConstantWeapon\": return new ConstantWeapon(definition, effect);",
        "case \"EnergyDraw\": return new EnergyDraw(definition, effect);",
        "case \"GuidedWeapon\": return new InstantWeapon(definition, effect);",
        "case \"Heat\": return new Heat(definition, effect);",
        "case \"HeatStorage\": return new HeatStorage(definition, effect);",
        "case \"InstantWeapon\": return new InstantWeapon(definition, effect);",
        "case \"ItemUsage\": return new ItemUsage(definition, effect);",
        "case \"Launcher\": return new LockWeapon(definition, effect);",
        "case \"LockWeapon\": return new LockWeapon(definition, effect);",
        "case \"MiningTool\": return new MiningTool(definition, effect);",
        "case \"Radiator\": return new Radiator(definition, effect);",
        "case \"Reactor\": return new Reactor(definition, effect);",
        "case \"Reflector\": return new Reflector(definition, effect);",
        "case \"ResourceScanner\": return new ResourceScanner(definition, effect);",
        "case \"Sensor\": return new Sensor(definition, effect);",
        "case \"Shield\": return new Shield(definition, effect);",
        "case \"StatModifier\": return new StatModifier(definition, effect);",
        "case \"Switch\": return new Switch(definition, effect);",
        "case \"Thermotoggle\": return new Thermotoggle(definition, effect);",
        "case \"Thruster\": return new Thruster(definition, effect);",
        "case \"Trigger\": return new Trigger(definition, effect);",
        "case \"TurretController\": return new TurretController(definition, effect);",
        "case \"VelocityConversion\": return new VelocityConversion(definition, effect);",
        "case \"VelocityLimit\": return new VelocityLimit(definition, effect);",
        "case \"Visibility\": return new Visibility(definition, effect);",
        "case \"Wear\": return new Wear(definition, effect);"
    };

    var missingFactoryMappings = requiredFactoryMappings
        .Where(symbol => !itemManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingFactoryMappings.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed runtime behavior factory no longer covers the live behavior payload kinds: " +
            string.Join(", ", missingFactoryMappings));
    }

    var requiredWeaponStateCoverage = new[]
    {
        "if (weapon is InstantWeapon instant)",
        "if (weapon is ChargedWeapon charged)",
        "if (weapon is ConstantWeapon constant)",
        "if (weapon is LockWeapon lockWeapon)",
        "if (weapon is LockWeapon lockWeapon)",
        "else if (weapon is ChargedWeapon chargedWeapon)",
        "else if (weapon is ConstantWeapon constantWeapon)",
        "else if (weapon is InstantWeapon instantWeapon)"
    };

    var missingWeaponStateCoverage = requiredWeaponStateCoverage
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingWeaponStateCoverage.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed weapon snapshot coverage no longer restores the live articulated weapon families: " +
            string.Join(", ", missingWeaponStateCoverage));
    }

    var requiredBehaviorStateCoverage = new[]
    {
        "if (!(behaviors[behaviorIndex] is IProgressBehavior progressBehavior))",
        "if (behavior is Sensor sensor)",
        "else if (behavior is Radiator radiator)",
        "else if (behavior is Reactor reactor)",
        "else if (behavior is Capacitor capacitor)",
        "else if (behavior is AetherDrive drive)",
        "else if (behavior is ResourceScanner resourceScanner)",
        "else if (behavior is MiningTool miningTool)",
        "else if (behavior is Thruster thruster)",
        "else if (behavior is Shield shield)",
        "else if (behavior is VelocityLimit velocityLimit)",
        "else if (behavior is Thermotoggle thermotoggle)",
        "else if (behavior is Switch switchBehavior)",
        "else if (behavior is Trigger trigger)",
        "else if (behavior is StatModifier statModifier)",
        "else if (behavior is TurretController turretController)",
        "case Sensor sensor:",
        "case Radiator radiator:",
        "case Reactor reactor:",
        "case Capacitor capacitor:",
        "case AetherDrive drive:",
        "case ResourceScanner resourceScanner:",
        "case MiningTool miningTool:",
        "case Thruster thruster:",
        "case Shield shield:",
        "case VelocityLimit velocityLimit:",
        "case Thermotoggle thermotoggle:",
        "case Switch switchBehavior:",
        "case Trigger trigger:",
        "case StatModifier statModifier:",
        "case TurretController turretController:"
    };

    var missingBehaviorStateCoverage = requiredBehaviorStateCoverage
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingBehaviorStateCoverage.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed runtime behavior snapshot coverage no longer owns the mutable non-weapon behavior families: " +
            string.Join(", ", missingBehaviorStateCoverage));
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
    var sharedCommandsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerSettingsCommands.cs");
    var surfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu settings commit path; MainMenu.cs is missing.");
    }
    if (!File.Exists(sharedCommandsPath))
    {
        throw new InvalidOperationException(
            "Cannot verify main-menu settings command contract; AetheriaRuntimePlayerSettingsCommands.cs is missing.");
    }
    if (!File.Exists(surfaceBuilderPath))
    {
        throw new InvalidOperationException(
            "Cannot verify main-menu settings surface contract; AetheriaRuntimePlayerSettingsSurfaceBuilder.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var sharedCommands = File.ReadAllText(sharedCommandsPath);
    var surfaceBuilder = File.ReadAllText(surfaceBuilderPath);
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
        "CommitRuntimeShowAsteroidsInMinimap",
        "CommitRuntimePlayerSettingsCommand"
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

    var requiredMainMenuAuthoritySymbols = new[]
    {
        "ActionGameManager.CommitRuntimePlayerSettingsCommand"
    };

    var missingUiCalls = requiredMainMenuAuthoritySymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingUiCalls.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer routes settings changes through ActionGameManager: " +
            string.Join(", ", missingUiCalls));
    }

    var requiredSharedCommands = new[]
    {
        "SetPlayerName",
        "CycleTemperatureUnit",
        "DecrementSignificantDigits",
        "IncrementSignificantDigits",
        "CycleNebulaQuality",
        "ToggleShowAsteroidsInMinimap"
    };

    var missingSharedCommands = requiredSharedCommands
        .Where(symbol => !sharedCommands.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSharedCommands.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared player-settings Eve command contract is incomplete: " +
            string.Join(", ", missingSharedCommands));
    }

    var requiredSurfaceBuilderSymbols = new[]
    {
        "AetheriaRuntimePlayerSettingsSurfaceBuilder",
        "AetheriaRuntimePlayerSettingsCommands.SetPlayerName",
        "\"control.text\"",
        "AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit",
        "AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits",
        "AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits",
        "AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality",
        "AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap"
    };

    var missingSurfaceBuilderSymbols = requiredSurfaceBuilderSymbols
        .Where(symbol => !surfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSurfaceBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared player-settings Eve surface builder is incomplete: " +
            string.Join(", ", missingSurfaceBuilderSymbols));
    }

    var requiredMainMenuSymbols = new[]
    {
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.Build",
        "new EveUiToolkitSurfaceLowerer",
        "ActionGameManager.CommitRuntimePlayerSettingsCommand(request.Command, request.Payload)"
    };

    var missingMainMenuSymbols = requiredMainMenuSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingMainMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer lowers the shared player-settings Eve surface contract: " +
            string.Join(", ", missingMainMenuSymbols));
    }

    var forbiddenMainMenuSymbols = new[]
    {
        "new TextField(\"Name\")",
        "ActionGameManager.CommitRuntimePlayerName(evt.newValue)"
    };

    var mainMenuHits = forbiddenMainMenuSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (mainMenuHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu should not keep renderer-local player-name input authority outside the shared Eve surface: " +
            string.Join(", ", mainMenuHits));
    }

    var forbiddenUiFieldSymbols = new[]
    {
        "AddField(\"Temperature Unit\"",
        "AddField(\"Significant Digits\"",
        "AddField(\"Nebula Quality\"",
        "AddField(\"Show Asteroids in Minimap\"",
        "ShowGameplaySettings()",
        "ShowGraphicsSettings()"
    };

    var uiFieldHits = File.ReadLines(mainMenuPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenUiFieldSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, mainMenuPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (uiFieldHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still uses legacy field widgets for Eve-owned gameplay/graphics settings: " +
            string.Join("; ", uiFieldHits));
    }
}

static void RequireMainMenuSettingsShellUsesEveSurface(string root)
{
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu settings shell; MainMenu.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var requiredSymbols = new[]
    {
        "RenderMenuSurface(",
        "BuildSettingsSurfaceDefinition()",
        "BuildVerseSettingsSurfaceDefinition()",
        "BuildInputSettingsSurfaceDefinition(",
        "HandleSettingsSurfaceCommand(",
        "HandleVerseSettingsSurfaceCommand(",
        "HandleInputSettingsSurfaceCommand(",
        "WithBackAction(",
        "ShowPlayerSettingsCommand",
        "ShowVerseSettingsCommand",
        "ShowInputSettingsCommand",
        "BackToMainCommand",
        "BackToSettingsCommand"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer lowers the settings shell through Eve surfaces: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "_nextMenu.panel.AddButton(\"Player Settings\"",
        "_nextMenu.panel.AddButton(\"Verse\"",
        "_nextMenu.panel.AddButton(\"Input\"",
        "_nextMenu.panel.AddButton(\"Audio\"",
        "BuildAudioSettingsSurfaceDefinition(",
        "HandleAudioSettingsSurfaceCommand(",
        "ShowAudioSettingsCommand",
        "_nextMenu.panel.Title.text = \"settings\"",
        "_nextMenu.panel.Title.text = TitleSubtitle(\"input\", \"settings\")",
        "_nextMenu.panel.Title.text = TitleSubtitle(\"audio\", \"settings\")"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still keeps a fake audio/settings shell or old PropertiesPanel settings shell alive: " +
            string.Join(", ", hits));
    }
}

static void RequireMainMenuRootUsesEveSurface(string root)
{
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu root shell; MainMenu.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var requiredSymbols = new[]
    {
        "MainSurfaceId",
        "ContinueRunCommand",
        "NewGameCommand",
        "ShowSettingsCommand",
        "QuitCommand",
        "BuildMainSurfaceDefinition(",
        "HandleMainSurfaceCommand(",
        "BuildMainSurfaceDefinition(",
        "LatestContinueRun(stateBoot)",
        "LatestVerseHostSettings(stateBoot)",
        "HideMenuSurface();"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer lowers the root shell through Eve surfaces: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "PanelPrototype",
        "FadeTime",
        "FadeDistance",
        "FadeAlphaExponent",
        "FadePositionExponent",
        "_currentMenu",
        "_nextMenu",
        "_fadeFromRight",
        "_fadeLerp",
        "_fading",
        "_panelPosition",
        "TitleSubtitle(",
        "IsMenuSurfaceVisible(",
        "_nextMenu.panel.AddButton(\"Continue\"",
        "_nextMenu.panel.AddButton(\"New Game\"",
        "_nextMenu.panel.AddButton(\"Settings\"",
        "_nextMenu.panel.AddButton(\"Quit\"",
        "Fade(true)",
        "Fade(false)"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still owns root navigation through the legacy PropertiesPanel/fade shell: " +
            string.Join(", ", hits));
    }

    var prefabPath = Path.Combine(root, "Assets", "Prefabs", "UI", "Main Menu Canvas.prefab");
    if (!File.Exists(prefabPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu prefab shell; Main Menu Canvas.prefab is missing.");
    }

    var prefab = File.ReadAllText(prefabPath);
    var forbiddenPrefabSymbols = new[]
    {
        "PanelPrototype:",
        "FadeTime:",
        "FadeDistance:",
        "FadeAlphaExponent:",
        "FadePositionExponent:"
    };

    var prefabHits = forbiddenPrefabSymbols
        .Where(symbol => prefab.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (prefabHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Main-menu prefab still serializes the dead root shell: " +
            string.Join(", ", prefabHits));
    }
}

static void RequireConfirmationDialogOwnsMinimalPromptShell(string root)
{
    var dialogPath = Path.Combine(root, "Assets", "Scripts", "UI", "ConfirmationDialog.cs");
    if (!File.Exists(dialogPath))
    {
        throw new InvalidOperationException("Cannot verify confirmation dialog shell; ConfirmationDialog.cs is missing.");
    }

    var source = File.ReadAllText(dialogPath);
    var requiredSymbols = new[]
    {
        "public class ConfirmationDialog : MonoBehaviour",
        "public TextMeshProUGUI Title;",
        "public RectTransform Content;",
        "public Property Property;",
        "public InputField InputField;",
        "public void Clear()",
        "public Property AddProperty(Func<string> read)",
        "public void AddField(string name, Func<string> read, Action<string> write)",
        "public void AddField(string name, Func<int> read, Action<int> write)"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ConfirmationDialog no longer owns the minimal prompt shell contract: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        ": PropertiesPanel",
        "OnPropertyAdded",
        "RefreshPropertyValues",
        "PropertyLabel",
        "Dropdown",
        "RangedFloatField",
        "BoolField",
        "EnumField",
        "StatSheet"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "ConfirmationDialog still drags generic PropertiesPanel machinery into the prompt shell: " +
            string.Join(", ", hits));
    }

    var prefabBlock = ReadSerializedMonoBehaviourBlock(
        Path.Combine(root, "Assets", "Prefabs", "UI", "Main Menu Canvas.prefab"),
        "83488dc4b5e58cd40b4d1c4ee98207b5");
    var sceneBlock = ReadSerializedMonoBehaviourBlock(
        Path.Combine(root, "Assets", "Scenes", "ARPG.unity"),
        "83488dc4b5e58cd40b4d1c4ee98207b5");

    var forbiddenSerializedFields = new[]
    {
        "Spacer:",
        "Dropdown:",
        "Section:",
        "List:",
        "PropertyLabel:",
        "Attribute:",
        "RangedFloatField:",
        "ProgressField:",
        "EnumField:",
        "BoolField:",
        "PropertyButton:",
        "ButtonField:",
        "IncrementField:",
        "StatSheet:",
        "CurveField:",
        "SelectedChild:",
        "GameManager:"
    };

    foreach (var (label, block) in new[] { ("Main Menu Canvas.prefab", prefabBlock), ("ARPG.unity", sceneBlock) })
    {
        var blockHits = forbiddenSerializedFields
            .Where(symbol => block.Contains(symbol, StringComparison.Ordinal))
            .ToArray();

        if (blockHits.Length > 0)
        {
            throw new InvalidOperationException(
                $"ConfirmationDialog still serializes dead PropertiesPanel fields in {label}: " +
                string.Join(", ", blockHits));
        }
    }
}

static string ReadSerializedMonoBehaviourBlock(string path, string scriptGuid)
{
    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"Cannot inspect serialized MonoBehaviour block; file is missing: {path}");
    }

    var lines = File.ReadAllLines(path);
    var scriptLine = $"guid: {scriptGuid}";
    for (var index = 0; index < lines.Length; index++)
    {
        if (!lines[index].Contains(scriptLine, StringComparison.Ordinal))
        {
            continue;
        }

        var start = index;
        while (start >= 0 && !lines[start].StartsWith("--- !u!114 ", StringComparison.Ordinal))
        {
            start--;
        }

        if (start < 0)
        {
            break;
        }

        var end = start + 1;
        while (end < lines.Length && !lines[end].StartsWith("--- !u!", StringComparison.Ordinal))
        {
            end++;
        }

        return string.Join(Environment.NewLine, lines.Skip(start).Take(end - start));
    }

    throw new InvalidOperationException(
        $"Cannot inspect serialized MonoBehaviour block for script guid {scriptGuid} in {path}.");
}

static void RequireMainMenuInputSettingsDelegateToRuntimeScreen(string root)
{
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu input delegation; MainMenu.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var requiredSymbols = new[]
    {
        "OpenRuntimeInputScreenCommand",
        "CanOpenRuntimeInputScreen()",
        "TryOpenRuntimeInputScreen()",
        "BuildInputSettingsSurfaceDefinition(CanOpenRuntimeInputScreen(), InGame)",
        "ActionGameManager.Instance.ShowInputScreenFromMenu();"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu input page no longer delegates to the live runtime remap screen: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "Typed input rebinding controls are not lowered through Eve yet.",
        "The live remapping screen still owns drag/drop rebinding and low-level InputSystem edits."
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu input page still reports the live remap owner as future work instead of delegating to it: " +
            string.Join(", ", hits));
    }
}

static void RequireRuntimeInputScreenUsesEveSurface(string root)
{
    var inputScreenPath = Path.Combine(root, "Assets", "Scripts", "UI", "InputScreen", "InputDisplayLayout.cs");
    var commandsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeInputSettingsCommands.cs");
    var builderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeInputSettingsSurfaceBuilder.cs");

    if (!File.Exists(inputScreenPath))
    {
        throw new InvalidOperationException("Cannot verify runtime input-screen Eve lowering; InputDisplayLayout.cs is missing.");
    }

    if (!File.Exists(commandsPath) || !File.Exists(builderPath))
    {
        throw new InvalidOperationException(
            "Cannot verify runtime input-screen Eve lowering; the shared input-settings Eve contract is missing.");
    }

    var inputScreen = File.ReadAllText(inputScreenPath);
    var commands = File.ReadAllText(commandsPath);
    var builder = File.ReadAllText(builderPath);

    var requiredInputScreenSymbols = new[]
    {
        "AetheriaRuntimeInputSettingsSurfaceBuilder.Build(",
        "AetheriaRuntimeInputSettingsCommands.BeginCapture",
        "AetheriaRuntimeInputSettingsCommands.ToggleActionBar",
        "new EveUiToolkitSurfaceLowerer()",
        "UIDocument",
        "ActionGameManager.CommitRuntimeInputBindingOverride",
        "ActionGameManager.CommitRuntimeActionBarInput",
        "action.ApplyBindingOverride",
        "new InputAction(\"Aetheria Input Capture\")",
        "HideLegacyChildren()"
    };

    var missingInputScreenSymbols = requiredInputScreenSymbols
        .Where(symbol => !inputScreen.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingInputScreenSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime input screen no longer lowers through the Eve UI Toolkit surface contract: " +
            string.Join(", ", missingInputScreenSymbols));
    }

    var forbiddenInputScreenSymbols = new[]
    {
        "OnPointerClickAsObservable",
        "BeginDragTrigger",
        "EndDragTrigger",
        "VerticalLayoutGroup",
        "LayoutRebuilder",
        "UILineRenderer",
        "Observable.NextFrame()",
        "DisplayLayout(_inputLayout)"
    };

    var survivingLegacySymbols = forbiddenInputScreenSymbols
        .Where(symbol => inputScreen.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (survivingLegacySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime input screen still carries the old drag/drop uGUI authority path: " +
            string.Join(", ", survivingLegacySymbols));
    }

    if (!commands.Contains("SurfaceId = \"aetheria.input_settings\"", StringComparison.Ordinal) ||
        !commands.Contains("BeginCapture", StringComparison.Ordinal) ||
        !commands.Contains("ToggleActionBar", StringComparison.Ordinal) ||
        !commands.Contains("public static bool IsKnown", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared input-settings command contract is incomplete.");
    }

    var requiredBuilderSymbols = new[]
    {
        "public sealed class AetheriaRuntimeInputSettingsSurfaceState",
        "public sealed class AetheriaRuntimeInputBindingSurfaceState",
        "public sealed class AetheriaRuntimeActionBarInputSurfaceState",
        "AetheriaRuntimeInputSettingsCommands.BeginCapture",
        "AetheriaRuntimeInputSettingsCommands.ToggleActionBar",
        "\"Low-level InputSystem edits flow through this Eve surface and queue typed player-settings commits.\""
    };

    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !builder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared input-settings Eve surface builder is incomplete: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireActionGameManagerInputScreenUsesSharedFullscreenPrimitive(string root)
{
    var managerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    if (!File.Exists(managerPath))
    {
        throw new InvalidOperationException("Cannot verify ActionGameManager input-screen delegation; ActionGameManager.cs is missing.");
    }

    var source = File.ReadAllText(managerPath);
    var requiredSymbols = new[]
    {
        "public bool CanShowInputScreenFromMenu()",
        "public void ShowInputScreenFromMenu()",
        "ShowFullscreenMenu(HelpScreen);",
        "private void ShowFullscreenMenu(GameObject menu)",
        "private void HideFullscreenMenu(GameObject menu)",
        "Input.Global.InputScreen.performed += context => ToggleFullscreenMenu(HelpScreen);"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer exposes the shared fullscreen primitive for the live input-screen owner: " +
            string.Join(", ", missingSymbols));
    }
}

static void RequireSectorMapZoneDetailsUseEveSurface(string root)
{
    var sectorRendererPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs");
    if (!File.Exists(sectorRendererPath))
    {
        throw new InvalidOperationException("Cannot verify sector-map zone details shell; SectorRenderer.cs is missing.");
    }

    var source = File.ReadAllText(sectorRendererPath);
    var requiredSymbols = new[]
    {
        "ZoneDetailsSurfaceId",
        "CloseZoneDetailsCommand",
        "RenderZoneDetailsSurface(",
        "HandleZoneDetailsSurfaceCommand(",
        "HideZoneDetailsSurface(",
        "ResolveZoneDetailsSurfaceDocument(",
        "BuildZoneDetailsSurfaceDefinition(",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "SectorRenderer no longer lowers zone details through an Eve surface shell: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "Properties.Clear();",
        "Properties.Title.text = zone.Name;",
        "Properties.AddProperty(\"Owner\"",
        "Properties.AddProperty(\"Mass\"",
        "Properties.AddProperty(\"Radius\"",
        "Properties.AddProperty(\"Planets\"",
        "Properties.AddProperty(\"Asteroid Belts\"",
        "Properties.AddProperty(\"Gas Giants\"",
        "Properties.AddProperty(\"Stars\"",
        "Properties.AddProperty(\"Stations\"",
        "Properties.AddProperty(\"Turrets\"",
        "Properties.AddProperty(\"Ships\""
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "SectorRenderer still owns zone details through the old PropertiesPanel path: " +
            string.Join(", ", hits));
    }
}

static void RequireRuntimeMenuTabsUseEveSurface(string root)
{
    var menuPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuPanel.cs");
    if (!File.Exists(menuPanelPath))
    {
        throw new InvalidOperationException("Cannot verify runtime menu tab shell; MenuPanel.cs is missing.");
    }

    var legacyTabButtonPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuTabButton.cs");
    if (File.Exists(legacyTabButtonPath))
    {
        throw new InvalidOperationException("MenuPanel tab metadata still has a surviving MenuTabButton component shell.");
    }

    var source = File.ReadAllText(menuPanelPath);
    var requiredSymbols = new[]
    {
        "MenuTabsSurfaceId",
        "MenuTabBinding",
        "TabBindings = Array.Empty<MenuTabBinding>();",
        "RenderTabSurface(",
        "HandleTabSurfaceCommand(",
        "ResolveTabSurfaceDocument(",
        "BuildTabSurfaceDefinition(",
        "ResolveVisibleTabs(",
        "GetTabLabel(",
        "GetTabCommand(",
        "new EveUiToolkitSurfaceLowerer()",
        "TabButtons.gameObject.SetActive(false)"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MenuPanel no longer lowers the runtime tab shell through Eve surfaces: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "MenuTabButton",
        "tabButton.Button.onClick.AddListener(",
        "tabButton.gameObject.SetActive(!tabButton.RequireDock || GameManager.DockedEntity != null);",
        "_tabs[MenuTab.Local].gameObject.SetActive("
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "MenuPanel still owns tab-shell behavior through the old MenuTabButton path: " +
            string.Join(", ", hits));
    }
}

static void RequireInventoryShipSettingsUseEveSurface(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    if (!File.Exists(inventoryMenuPath))
    {
        throw new InvalidOperationException("Cannot verify inventory ship-settings shell; InventoryMenu.cs is missing.");
    }

    var source = File.ReadAllText(inventoryMenuPath);
    var requiredSymbols = new[]
    {
        "ShipSettingsSurfaceId",
        "RenderCurrentShipSettingsSurface(",
        "HandleCurrentShipSettingsSurfaceCommand(",
        "ResolveShipSettingsSurfaceDocument(",
        "BuildCurrentShipSettingsSurfaceDefinition(",
        "DecrementShutdownThresholdCommand",
        "IncrementShutdownThresholdCommand",
        "ResetShutdownThresholdCommand",
        "CloseShipSettingsCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu no longer lowers current ship settings through an Eve surface shell: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "PropertiesPanel.AddField(\"Shutdown Threshold\"",
        "() => GameManager.CurrentEntity.Settings.ShutdownPerformance",
        "f => GameManager.CommitEntityShutdownPerformance(GameManager.CurrentEntity, f)"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still owns ship settings through the old PropertiesPanel field path: " +
            string.Join(", ", hits));
    }
}

static void RequireInventoryCargoItemDetailsUseEveSurface(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    if (!File.Exists(inventoryMenuPath))
    {
        throw new InvalidOperationException("Cannot verify inventory cargo-item shell; InventoryMenu.cs is missing.");
    }

    var source = File.ReadAllText(inventoryMenuPath);
    var requiredSymbols = new[]
    {
        "CargoItemDetailsSurfaceId",
        "RenderCargoItemDetailsSurface(",
        "HandleCargoItemDetailsSurfaceCommand(",
        "ResolveCargoItemDetailsSurfaceDocument(",
        "BuildCargoItemDetailsSurfaceDefinition(",
        "BuildCargoItemBehaviorCards(",
        "BuildCargoItemBehaviorMetric(",
        "CloseCargoItemDetailsCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu no longer lowers cargo-item inspection through an Eve surface shell: " +
            string.Join(", ", missingSymbols));
    }

    if (!source.Contains("RenderCargoItemDetailsSurface(item);", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryMenu cargo click path no longer routes item inspection through the Eve surface.");
    }
}

static void RequireInventoryEquippedItemDetailsUseEveSurface(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    if (!File.Exists(inventoryMenuPath))
    {
        throw new InvalidOperationException("Cannot verify inventory equipped-item shell; InventoryMenu.cs is missing.");
    }

    if (!File.Exists(actionGameManagerPath))
    {
        throw new InvalidOperationException("Cannot verify inventory equipped-item shell; ActionGameManager.cs is missing.");
    }

    var source = File.ReadAllText(inventoryMenuPath);
    var requiredSymbols = new[]
    {
        "EquippedItemDetailsSurfaceId",
        "RenderEquippedItemDetailsSurface(",
        "HandleEquippedItemDetailsSurfaceCommand(",
        "ResolveEquippedItemDetailsSurfaceDocument(",
        "BuildEquippedItemDetailsSurfaceDefinition(",
        "BuildEquippedItemControlCard(",
        "BuildEquippedItemWeaponGroupCard(",
        "BuildEquippedItemActionBarCards(",
        "BuildItemBehaviorCards(",
        "CommandButton(",
        "TextField(",
        "CloseEquippedItemDetailsCommand",
        "ToggleEquippedItemOverrideShutdownCommand",
        "SetEquippedItemTargetTemperatureCommand",
        "ToggleEquippedItemWeaponGroupCommand",
        "BindEquippedItemWeaponGroupCommand",
        "ClearEquippedItemActionBarBindingCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu no longer lowers equipped-item inspection through an Eve surface shell: " +
            string.Join(", ", missingSymbols));
    }

    if (!source.Contains("RenderEquippedItemDetailsSurface(item);", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryMenu equipped-item click path no longer routes inspection through the Eve surface.");
    }

    var forbiddenSymbols = new[]
    {
        "public PropertiesPanel PropertiesPanel;",
        "PropertiesPanel.GameManager = GameManager;",
        "PropertiesPanel.gameObject.SetActive(true);",
        "PropertiesPanel.Inspect(item);"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still owns equipped-item inspection through the old PropertiesPanel shell: " +
            string.Join(", ", hits));
    }

    var actionGameManagerSource = File.ReadAllText(actionGameManagerPath);
    var requiredActionBarSymbols = new[]
    {
        "GetActionBarSlotCount(",
        "GetActionBarSlotLabel(",
        "GetActionBarBindingLabel(",
        "CommitWeaponGroupActionBarBinding(",
        "CommitClearActionBarBinding(",
        "QueueRunCheckpoint(\"action-bar-binding\")"
    };

    var missingActionBarSymbols = requiredActionBarSymbols
        .Where(symbol => !actionGameManagerSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionBarSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer exposes the equipped-item action-bar binding authority surface: " +
            string.Join(", ", missingActionBarSymbols));
    }
}

static void RequireTradeCargoSelectorUseEveSurface(string root)
{
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade cargo-selector shell; TradeMenu.cs is missing.");
    }

    var source = File.ReadAllText(tradeMenuPath);
    var requiredSymbols = new[]
    {
        "CargoSelectorSurfaceId",
        "RenderCargoSelectorSurface(",
        "BuildCargoSelectionCommands(",
        "HandleCargoSelectorSurfaceCommand(",
        "ResolveCargoSelectorSurfaceDocument(",
        "BuildCargoSelectorSurfaceDefinition(",
        "CloseCargoSelectorCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu no longer lowers the cargo selector through an Eve surface shell: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "ContextMenu.AddOption(\"Docking Bay\"",
        "ContextMenu.AddOption($\"{ship.Name} Bay {bay.index+1}\""
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still owns cargo selection through the old context-menu option path: " +
            string.Join(", ", hits));
    }
}

static void RequireTradeFilterAndRowActionsUseEveSurface(string root)
{
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade filter and row-action shells; TradeMenu.cs is missing.");
    }

    var source = File.ReadAllText(tradeMenuPath);
    var requiredSymbols = new[]
    {
        "FilterSurfaceId",
        "RowActionSurfaceId",
        "RenderFilterSurface(",
        "BuildFilterSurfaceCommands(",
        "HandleFilterSurfaceCommand(",
        "ResolveFilterSurfaceDocument(",
        "BuildFilterSurfaceDefinition(",
        "RenderRowActionSurface(",
        "BuildRowActionSurfaceCommands(",
        "HandleRowActionSurfaceCommand(",
        "ResolveRowActionSurfaceDocument(",
        "BuildRowActionSurfaceDefinition(",
        "ShowBuyQuantityDialog(",
        "CloseFilterSurfaceCommand",
        "CloseRowActionSurfaceCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu no longer lowers filter and row-action shells through Eve surfaces: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "public ContextMenu ContextMenu;",
        "ContextMenu.Clear();",
        "ContextMenu.AddDropdown(",
        "ContextMenu.AddOption(",
        "ContextMenu.Show();"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still owns filter or row-action behavior through the old context-menu path: " +
            string.Join(", ", hits));
    }
}

static void RequireTradeItemDetailsUseEveSurface(string root)
{
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade item-details shell; TradeMenu.cs is missing.");
    }

    var source = File.ReadAllText(tradeMenuPath);
    var requiredSymbols = new[]
    {
        "TradeItemSurfaceId",
        "RenderTradeItemDetailsSurface(",
        "HandleTradeItemDetailsSurfaceCommand(",
        "ResolveTradeItemDetailsSurfaceDocument(",
        "BuildTradeItemDetailsSurfaceDefinition(",
        "BuildTradeItemBehaviorCards(",
        "BuildTradeItemBehaviorMetric(",
        "CloseTradeItemDetailsCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu no longer lowers typed item inspection through an Eve surface: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "public PropertiesPanel Properties;",
        "OnClick = () => Properties.Inspect(i.TypedItem)"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still owns typed item inspection through the old PropertiesPanel path: " +
            string.Join(", ", hits));
    }
}

static void RequireInventoryDropdownUseEveSurface(string root)
{
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    if (!File.Exists(inventoryPanelPath))
    {
        throw new InvalidOperationException("Cannot verify inventory dropdown shell; InventoryPanel.cs is missing.");
    }

    var source = File.ReadAllText(inventoryPanelPath);
    var requiredSymbols = new[]
    {
        "DropdownSurfaceId",
        "RenderDropdownSurface(",
        "BuildDropdownCommands(",
        "HandleDropdownSurfaceCommand(",
        "ResolveDropdownSurfaceDocument(",
        "BuildDropdownSurfaceDefinition(",
        "SaveLoadoutCommand",
        "CloseDropdownSurfaceCommand",
        "new EveUiToolkitSurfaceLowerer()"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel no longer lowers the dropdown shell through an Eve surface: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "ContextMenu.AddDropdown(entity.Name",
        "ContextMenu.AddOption(GameManager.DockingBay.Name",
        "ContextMenu.AddOption(\"Save Loadout\"",
        "ContextMenu.AddDropdown(\"Restore Loadout\"",
        "ContextMenu.Show();"
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still owns dropdown behavior through the old ContextMenu path: " +
            string.Join(", ", hits));
    }
}

static void RequireNoDeadPopupShells(string root)
{
    var deletedShellPaths = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "UI", "ContextMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "ContextMenu.cs.meta"),
        Path.Combine(root, "Assets", "Scripts", "UI", "ContextMenuOption.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "ContextMenuOption.cs.meta"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Context Menu.prefab"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Context Menu.prefab.meta"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Context Menu Option.prefab"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Context Menu Option.prefab.meta")
    };

    var survivingShells = deletedShellPaths
        .Where(File.Exists)
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();

    if (survivingShells.Length > 0)
    {
        throw new InvalidOperationException(
            "The dead ContextMenu popup shell still survives in source or prefab assets: " +
            string.Join(", ", survivingShells));
    }

    var sourceChecks = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs")] = new[]
        {
            "public ContextMenu ContextMenu;"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")] = new[]
        {
            "public ContextMenu Context;",
            "public DropdownMenu Dropdown;"
        },
        [Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs")] = new[]
        {
            "public PropertiesPanel Properties;",
            "Properties.gameObject.SetActive(false);"
        }
    };

    var survivingSymbols = sourceChecks
        .SelectMany(entry =>
        {
            var text = File.Exists(entry.Key) ? File.ReadAllText(entry.Key) : "";
            return entry.Value
                .Where(symbol => text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, entry.Key)}: {symbol}");
        })
        .ToArray();

    if (survivingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Renderer-local popup shell authority still survives in live source: " +
            string.Join("; ", survivingSymbols));
    }

    var serializedChecks = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scenes", "ARPG.unity")] = new[]
        {
            "ContextMenu: {",
            "propertyPath: ContextMenu",
            "Context: {fileID:",
            "Properties: {fileID:"
        },
        [Path.Combine(root, "Assets", "Prefabs", "UI", "Inventory.prefab")] = new[]
        {
            "ContextMenu: {"
        }
    };

    var survivingSerializedShells = serializedChecks
        .SelectMany(entry =>
        {
            var text = File.Exists(entry.Key) ? File.ReadAllText(entry.Key) : "";
            return entry.Value
                .Where(symbol => text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, entry.Key)}: {symbol}");
        })
        .ToArray();

    if (survivingSerializedShells.Length > 0)
    {
        throw new InvalidOperationException(
            "Scene or prefab YAML still serializes the deleted popup shell or orphan PropertiesPanel links: " +
            string.Join("; ", survivingSerializedShells));
    }
}

static void RequirePlayerSettingsEveSurface(string root)
{
    var projectorPath = Path.Combine(root, "Aetheria.State", "AetheriaPlayerSettingsSurfaceProjector.cs");
    var bridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var providerPath = Path.Combine(root, "Aetheria.State", "AetheriaProviderAdvertisementProjector.cs");
    var serverPath = Path.Combine(root, "Economy.Server", "Program.cs");
    var sharedCommandsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerSettingsCommands.cs");
    var surfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.cs");

    if (!File.Exists(projectorPath))
    {
        throw new InvalidOperationException("Player settings Eve surface projector is missing.");
    }

    var projector = File.ReadAllText(projectorPath);
    var requiredProjectorSymbols = new[]
    {
        "AetheriaRuntimePlayerSettingsCommands.SurfaceId",
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.Build",
        "AetheriaRuntimePlayerSettingsSurfaceState",
        "settings.ActiveRunKey"
    };

    var missingProjectorSymbols = requiredProjectorSymbols
        .Where(symbol => !projector.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingProjectorSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Player settings Eve surface projector is missing required typed controls: " +
            string.Join(", ", missingProjectorSymbols));
    }

    var bridge = File.Exists(bridgePath)
        ? File.ReadAllText(bridgePath)
        : throw new InvalidOperationException("Player settings Eve command bridge is missing.");
    var provider = File.Exists(providerPath)
        ? File.ReadAllText(providerPath)
        : throw new InvalidOperationException("Player settings provider advertisement projector is missing.");
    var server = File.Exists(serverPath)
        ? File.ReadAllText(serverPath)
        : throw new InvalidOperationException("Economy.Server program is missing.");
    var sharedCommands = File.Exists(sharedCommandsPath)
        ? File.ReadAllText(sharedCommandsPath)
        : throw new InvalidOperationException("Shared player-settings Eve command contract is missing.");
    var surfaceBuilder = File.Exists(surfaceBuilderPath)
        ? File.ReadAllText(surfaceBuilderPath)
        : throw new InvalidOperationException("Shared player-settings Eve surface builder is missing.");

    if (!bridge.Contains("AppliedPlayerSettingsCommands", StringComparison.Ordinal) ||
        !bridge.Contains("ApplyPlayerSettingsCommandAsync", StringComparison.Ordinal) ||
        !bridge.Contains("PutPlayerSettingsSurfaceAsync", StringComparison.Ordinal) ||
        !bridge.Contains("SetPlayerName", StringComparison.Ordinal) ||
        !bridge.Contains("command.Payload.TryGetValue(\"value\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve command bridge no longer owns the typed player-settings surface mutation path.");
    }

    if (!provider.Contains("AetheriaPlayerSettingsSurfaceProjector.SurfaceId", StringComparison.Ordinal) ||
        !provider.Contains("AetheriaRuntimePlayerSettingsCommands.Refresh", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Provider advertisement no longer publishes the player-settings Eve surface and commands.");
    }

    if (!server.Contains("PublishPlayerSettingsSurfaceAsync", StringComparison.Ordinal) ||
        !server.Contains("PutPlayerSettingsSurfaceAsync", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Economy.Server no longer republishes the provider-owned player-settings Eve surface.");
    }

    if (!sharedCommands.Contains("SurfaceId = \"aetheria.player_settings\"", StringComparison.Ordinal) ||
        !sharedCommands.Contains("public static bool IsKnown", StringComparison.Ordinal) ||
        !sharedCommands.Contains("SetPlayerName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared player-settings Eve command contract is missing the surface id or command registry helper.");
    }

    if (!surfaceBuilder.Contains("public static class AetheriaRuntimePlayerSettingsSurfaceBuilder", StringComparison.Ordinal) ||
        !surfaceBuilder.Contains("\"control.text\"", StringComparison.Ordinal) ||
        !surfaceBuilder.Contains("SetPlayerName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared player-settings Eve surface builder no longer owns the portable settings surface contract.");
    }
}

static void RequireVerseHostSettingsAuthority(string root)
{
    var settingsPath = Path.Combine(root, "Aetheria.State", "Documents", "AetheriaVerseHostSettings.cs");
    var normalizerPath = Path.Combine(root, "Aetheria.State", "AetheriaVerseHostSettingsNormalizer.cs");
    var verseCatalogProjectorPath = Path.Combine(root, "Aetheria.State", "AetheriaVerseCatalogProjector.cs");
    var verseDiscoveryHostPath = Path.Combine(root, "Aetheria.State", "AetheriaVerseDiscoveryHost.cs");
    var registryPath = Path.Combine(root, "Aetheria.State", "AetheriaDocumentRegistry.cs");
    var nodePath = Path.Combine(root, "Aetheria.State", "AetheriaStateNode.cs");
    var providerPath = Path.Combine(root, "Aetheria.State", "AetheriaProviderAdvertisementProjector.cs");
    var operationsPath = Path.Combine(root, "Aetheria.State", "AetheriaOperationsSurfaceProjector.cs");
    var serverPath = Path.Combine(root, "Economy.Server", "Program.cs");
    var serverSettingsPath = Path.Combine(root, "Economy.Server", "appsettings.json");
    var applyPendingPath = Path.Combine(root, "Aetheria.State.ApplyPending", "Program.cs");

    var requiredFiles = new[]
    {
        settingsPath,
        normalizerPath,
        verseCatalogProjectorPath,
        verseDiscoveryHostPath,
        registryPath,
        nodePath,
        providerPath,
        operationsPath,
        serverPath,
        serverSettingsPath,
        applyPendingPath
    };

    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse host authority cut is incomplete; missing files: " + string.Join(", ", missingFiles));
    }

    var settings = File.ReadAllText(settingsPath);
    var normalizer = File.ReadAllText(normalizerPath);
    var verseCatalogProjector = File.ReadAllText(verseCatalogProjectorPath);
    var verseDiscoveryHost = File.ReadAllText(verseDiscoveryHostPath);
    var registry = File.ReadAllText(registryPath);
    var node = File.ReadAllText(nodePath);
    var provider = File.ReadAllText(providerPath);
    var operations = File.ReadAllText(operationsPath);
    var server = File.ReadAllText(serverPath);
    var serverSettings = File.ReadAllText(serverSettingsPath);
    var applyPending = File.ReadAllText(applyPendingPath);

    var requiredSettingsSymbols = new[]
    {
        "[CultDocument(\"aetheria.verse_host_settings\", \"aetheria.verse_host_settings.v1\")]",
        "public sealed class AetheriaVerseHostSettings",
        "public string VerseId",
        "public string CultMeshAddress",
        "public string Visibility"
    };
    var missingSettingsSymbols = requiredSettingsSymbols
        .Where(symbol => !settings.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSettingsSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed verse-host settings document is missing required authority fields: " +
            string.Join(", ", missingSettingsSymbols));
    }

    if (!normalizer.Contains("public static AetheriaVerseHostSettings Normalize", StringComparison.Ordinal) ||
        !normalizer.Contains("public static bool Equivalent", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Verse-host settings normalizer is missing normalize/equivalence ownership.");
    }

    var requiredVerseCatalogProjectorSymbols = new[]
    {
        "public static class AetheriaVerseCatalogProjector",
        "CultMeshVerseDescriptor",
        "BuildDiscoveryEndpoint",
        "CultMeshVerseDescriptor.ComputeRulesHash",
        "cultnet://"
    };
    var missingVerseCatalogProjectorSymbols = requiredVerseCatalogProjectorSymbols
        .Where(symbol => !verseCatalogProjector.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingVerseCatalogProjectorSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse catalog projector no longer derives public CultMesh discovery from typed host settings: " +
            string.Join(", ", missingVerseCatalogProjectorSymbols));
    }

    var requiredVerseDiscoveryHostSymbols = new[]
    {
        "public sealed class AetheriaVerseDiscoveryHost",
        "AetheriaVerseCatalogProjector.Build(normalized)",
        "CultMesh.CreateVerseCatalog()",
        "CultMesh.ServeVerseCatalog(_node.MeshNode, _catalog)",
        "normalized.Visibility"
    };
    var missingVerseDiscoveryHostSymbols = requiredVerseDiscoveryHostSymbols
        .Where(symbol => !verseDiscoveryHost.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingVerseDiscoveryHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse discovery host no longer gates served Verse catalogs through the typed host settings: " +
            string.Join(", ", missingVerseDiscoveryHostSymbols));
    }

    if (!registry.Contains("CultNetDocumentBinding.ForDocument<AetheriaVerseHostSettings>", StringComparison.Ordinal) ||
        !registry.Contains("typeof(AetheriaVerseHostSettings)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria document registry does not register typed verse-host settings.");
    }

    if (!node.Contains("PutVerseHostSettingsAsync", StringComparison.Ordinal) ||
        !node.Contains("GetVerseHostSettingsAsync", StringComparison.Ordinal) ||
        !node.Contains("public CultMeshNode MeshNode => _node;", StringComparison.Ordinal) ||
        !node.Contains("global:aetheria.verse_host_settings.v1", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state node does not expose typed verse-host settings get/put ports.");
    }

    var forbiddenHardcodedProviderSymbols = new[]
    {
        "VerseId = \"aetheria.local\"",
        "RootVerse = \"asgard\"",
        "CanonicalService = \"asgard.aetheria\"",
        "LocatedService = \"asgard.local.aetheria\"",
        "CultMeshAddress = \"asgard.local.aetheria/eve\""
    };
    var survivingHardcodedProviderSymbols = forbiddenHardcodedProviderSymbols
        .Where(symbol => provider.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingHardcodedProviderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Provider advertisement still hardcodes daemon verse identity: " +
            string.Join(", ", survivingHardcodedProviderSymbols));
    }

    if (!provider.Contains("AetheriaVerseHostSettings settings", StringComparison.Ordinal) ||
        !provider.Contains("AetheriaVerseHostSettingsNormalizer.Normalize(settings)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Provider advertisement no longer derives Verse identity from typed daemon settings.");
    }

    if (!operations.Contains("AetheriaVerseHostSettings? verseHostSettings = null", StringComparison.Ordinal) ||
        !operations.Contains("AetheriaVerseHostSettingsNormalizer.Normalize(verseHostSettings)", StringComparison.Ordinal) ||
        !operations.Contains("\"aetheria.operations.verseHost\"", StringComparison.Ordinal) ||
        !operations.Contains("Metric(\"verseHost.visibility\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Operations surface does not expose the typed daemon Verse-host telemetry card.");
    }

    var requiredServerSymbols = new[]
    {
        "EnsureVerseHostSettingsAsync",
        "AetheriaVerseDiscoveryHost",
        "RefreshVerseDiscoveryAsync",
        "LoadVerseHostOverrides",
        "GetVerseHostSettingsAsync",
        "PutVerseHostSettingsAsync",
        "AetheriaProviderAdvertisementProjector.Build(verseHostSettings, node.StatePath, updatedAtUtc)"
    };
    var missingServerSymbols = requiredServerSymbols
        .Where(symbol => !server.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingServerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Economy.Server is missing typed verse-host startup authority: " +
            string.Join(", ", missingServerSymbols));
    }

    var requiredServerSettingsSymbols = new[]
    {
        "\"Aetheria\"",
        "\"VerseHost\"",
        "\"VerseId\"",
        "\"CultMeshAddress\"",
        "\"Visibility\""
    };
    var missingServerSettingsSymbols = requiredServerSettingsSymbols
        .Where(symbol => !serverSettings.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingServerSettingsSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Economy.Server appsettings are missing the daemon Verse-host section: " +
            string.Join(", ", missingServerSettingsSymbols));
    }

    if (!applyPending.Contains("EnsureVerseHostSettingsAsync", StringComparison.Ordinal) ||
        !applyPending.Contains("AetheriaProviderAdvertisementProjector.Build(verseHostSettings, statePath, now)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.ApplyPending does not preserve typed verse-host projection authority.");
    }
}

static void RequireClientTargetBootAuthority(string root)
{
    var unityFacadeProjectPath = Path.Combine(root, "Aetheria.State.Unity", "Aetheria.State.Unity.csproj");
    var boundaryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateBoundary.cs");
    var bootPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateBoot.cs");
    var clientTargetStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetStore.cs");
    var verseDiscoveryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseDiscovery.cs");
    var bootstrapPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveRuntimeBootstrap.cs");
    var presenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");

    var requiredFiles = new[]
    {
        unityFacadeProjectPath,
        boundaryPath,
        bootPath,
        clientTargetStorePath,
        verseDiscoveryPath,
        bootstrapPath,
        presenterPath,
        actionGameManagerPath,
        mainMenuPath
    };

    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Client target boot authority cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var unityFacadeProject = File.ReadAllText(unityFacadeProjectPath);
    var boundary = File.ReadAllText(boundaryPath);
    var boot = File.ReadAllText(bootPath);
    var clientTargetStore = File.ReadAllText(clientTargetStorePath);
    var verseDiscovery = File.ReadAllText(verseDiscoveryPath);
    var bootstrap = File.ReadAllText(bootstrapPath);
    var presenter = File.ReadAllText(presenterPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var mainMenu = File.ReadAllText(mainMenuPath);

    var requiredUnityFacadeSymbols = new[]
    {
        "AetheriaRuntimeClientTargetStore.cs",
        "AetheriaRuntimeVerseDiscovery.cs",
        "AetheriaRuntimeStateBoundary.cs",
        "AetheriaRuntimeStateBoot.cs"
    };
    var missingUnityFacadeSymbols = requiredUnityFacadeSymbols
        .Where(symbol => !unityFacadeProject.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingUnityFacadeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Unity.csproj no longer compiles the shared client-target boot files: " +
            string.Join(", ", missingUnityFacadeSymbols));
    }

    var requiredBoundarySymbols = new[]
    {
        "RuntimeClientTargetFileName = \"aetheria-client.cc\"",
        "RuntimeReplicaDirectoryName = \"Verses\"",
        "RuntimeStatePathOverrideEnvironmentVariable = \"AETHERIA_STATE_PATH\"",
        "LegacyRuntimeStatePathOverrideEnvironmentVariable = \"AETHERIA_EVE_STATE_PATH\"",
        "GetClientTargetPath",
        "GetReplicaStateFilePath",
        "ResolveStatePathOverride"
    };
    var missingBoundarySymbols = requiredBoundarySymbols
        .Where(symbol => !boundary.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBoundarySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime state boundary no longer owns the client target file and override resolution: " +
            string.Join(", ", missingBoundarySymbols));
    }

    var requiredClientTargetSymbols = new[]
    {
        "public static class AetheriaRuntimeClientTargetKinds",
        "public sealed class AetheriaRuntimeClientTargetDocument",
        "SchemaId = \"gamecult.aetheria.runtime_client_target.v1\"",
        "TargetKind",
        "StateFilePath",
        "CultMeshAddress",
        "DiscoveryEndpoints",
        "DiscoveredVerses",
        "LastDiscoveryAtUtc",
        "LastDiscoveryError",
        "ReplicaStateFilePath",
        "ReadOrInitialize",
        "CreateDefault",
        "Write(string clientTargetPath, AetheriaRuntimeClientTargetDocument document)",
        "Read(string clientTargetPath)"
    };
    var missingClientTargetSymbols = requiredClientTargetSymbols
        .Where(symbol => !clientTargetStore.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientTargetSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed client target store is incomplete: " +
            string.Join(", ", missingClientTargetSymbols));
    }

    var requiredBootSymbols = new[]
    {
        "ClientTargetPath",
        "TargetKind",
        "TargetSource",
        "SupportsLocalStateFileRead",
        "FailureMessage",
        "DiscoveryEndpoints",
        "DiscoveredVerses",
        "LastDiscoveryAtUtc",
        "LastDiscoveryError",
        "ReplicaStateFilePath",
        "TargetLabel",
        "AetheriaRuntimeClientTargetStore.ReadOrInitialize",
        "AetheriaRuntimeStateBoundary.ResolveStatePathOverride()",
        "AetheriaRuntimeClientTargetKinds.CultMeshVerse",
        "GetReplicaStateFilePath",
        "Sync the local replica",
        "state-path-override",
        "client-target"
    };
    var missingBootSymbols = requiredBootSymbols
        .Where(symbol => !boot.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBootSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime boot no longer resolves the active Verse through the client target owner: " +
            string.Join(", ", missingBootSymbols));
    }

    var requiredVerseDiscoverySymbols = new[]
    {
        "CultMesh.CreateVerseCatalog()",
        "CultMesh.CreateVerseDiscoveryClient()",
        "DiscoverAsync(catalog, endpoints)",
        "DiscoveredVerses",
        "LastDiscoveryAtUtc",
        "LastDiscoveryError"
    };
    var missingVerseDiscoverySymbols = requiredVerseDiscoverySymbols
        .Where(symbol => !verseDiscovery.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingVerseDiscoverySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime Verse discovery helper no longer refreshes the typed client-target catalog through CultMesh discovery: " +
            string.Join(", ", missingVerseDiscoverySymbols));
    }

    var requiredActionGameManagerSymbols = new[]
    {
        "AetheriaRuntimeStateBoot.Inspect(GameDataDirectory)",
        "Aetheria runtime target: {stateBoot.TargetLabel} via {stateBoot.TargetKind} ({stateBoot.TargetSource})",
        "!stateBoot.SupportsLocalStateFileRead"
    };
    var missingActionGameManagerSymbols = requiredActionGameManagerSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingActionGameManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer boots gameplay through the shared client-target report: " +
            string.Join(", ", missingActionGameManagerSymbols));
    }

    var requiredPresenterSymbols = new[]
    {
        "AetheriaRuntimeStateBoot.Inspect(gameDataDirectory, stateFilePathOverride)",
        "!stateBoot.SupportsLocalStateFileRead",
        "stateBoot.StateFileExists"
    };
    var missingPresenterSymbols = requiredPresenterSymbols
        .Where(symbol => !presenter.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingPresenterSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter no longer mounts through the shared client-target boot report: " +
            string.Join(", ", missingPresenterSymbols));
    }

    if (bootstrap.Contains("StatePathEnvironmentVariable", StringComparison.Ordinal) ||
        bootstrap.Contains("presenter.StateFilePathOverride =", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve runtime bootstrap still tries to own state-path override resolution.");
    }

    var requiredMainMenuSymbols = new[]
    {
        "CurrentStateBoot()",
        "LatestContinueRun(AetheriaRuntimeStateBootReport stateBoot)",
        "LatestVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)",
        "stateBoot.FailureMessage",
        "stateBoot.DiscoveryEndpoints",
        "stateBoot.DiscoveredVerses",
        "stateBoot.LastDiscoveryAtUtc",
        "stateBoot.LastDiscoveryError",
        "stateBoot.ReplicaStateFilePath",
        "\"Client Target\"",
        "\"Transport\"",
        "\"Target Source\""
    };
    var missingMainMenuSymbols = requiredMainMenuSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingMainMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer lowers the shared client-target boot state through Eve: " +
            string.Join(", ", missingMainMenuSymbols));
    }

    var forbiddenDirectPathSymbols = new Dictionary<string, string[]>
    {
        [actionGameManagerPath] = new[]
        {
            "AetheriaRuntimeStateBoundary.GetStateFilePath(GameDataDirectory)"
        },
        [presenterPath] = new[]
        {
            "AetheriaRuntimeStateBoundary.GetStateFilePath(gameDataDirectory)"
        },
        [bootstrapPath] = new[]
        {
            "StatePathOverride()"
        }
    };

    var hits = forbiddenDirectPathSymbols
        .SelectMany(entry =>
        {
            var source = File.ReadAllText(entry.Key);
            return entry.Value
                .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, entry.Key)} -> {symbol}");
        })
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity boot still contains direct local state-path owners instead of the shared client target: " +
            string.Join("; ", hits));
    }
}

static void RequireVerseReplicaTool(string root)
{
    var replicaToolProjectPath = Path.Combine(root, "Aetheria.State.Replica", "Aetheria.State.Replica.csproj");
    var replicaToolProgramPath = Path.Combine(root, "Aetheria.State.Replica", "Program.cs");
    var replicaHostPath = Path.Combine(root, "Aetheria.State", "AetheriaVerseReplica.cs");
    var stateReadmePath = Path.Combine(root, "Aetheria.State", "README.md");

    var requiredFiles = new[]
    {
        replicaToolProjectPath,
        replicaToolProgramPath,
        replicaHostPath,
        stateReadmePath
    };

    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse replica tool cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var replicaToolProject = File.ReadAllText(replicaToolProjectPath);
    var replicaToolProgram = File.ReadAllText(replicaToolProgramPath);
    var replicaHost = File.ReadAllText(replicaHostPath);
    var stateReadme = File.ReadAllText(stateReadmePath);

    var requiredProjectSymbols = new[]
    {
        "<ProjectReference Include=\"..\\Aetheria.State\\Aetheria.State.csproj\" />",
        "<ProjectReference Include=\"..\\Aetheria.State.Unity\\Aetheria.State.Unity.csproj\" />"
    };
    var missingProjectSymbols = requiredProjectSymbols
        .Where(symbol => !replicaToolProject.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingProjectSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse replica tool project is incomplete: " +
            string.Join(", ", missingProjectSymbols));
    }

    var requiredProgramSymbols = new[]
    {
        "sync",
        "follow",
        "AetheriaVerseReplica",
        "GetReplicaStateFilePath",
        "--endpoint",
        "--replica"
    };
    var missingProgramSymbols = requiredProgramSymbols
        .Where(symbol => !replicaToolProgram.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingProgramSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse replica tool program is incomplete: " +
            string.Join(", ", missingProgramSymbols));
    }

    var requiredReplicaHostSymbols = new[]
    {
        "public static class AetheriaVerseReplica",
        "SyncSnapshotAsync",
        "RunReplicaAsync",
        "CultNetSchemaShardSnapshotFetcher",
        "CultNetSchemaShardLogFetcher",
        "CultNetShardReplicator",
        "ApplyShardSnapshotResponseAsync"
    };
    var missingReplicaHostSymbols = requiredReplicaHostSymbols
        .Where(symbol => !replicaHost.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingReplicaHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse replica host is incomplete: " +
            string.Join(", ", missingReplicaHostSymbols));
    }

    if (!stateReadme.Contains("Aetheria.State.Replica", StringComparison.Ordinal) ||
        !stateReadme.Contains("GameData\\Verses\\<verse>.cc", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State README does not describe the cache-only Verse replica workflow.");
    }
}

static void RequireVerseSettingsShellAndBridge(string root)
{
    var unityFacadeProjectPath = Path.Combine(root, "Aetheria.State.Unity", "Aetheria.State.Unity.csproj");
    var stateProjectPath = Path.Combine(root, "Aetheria.State", "Aetheria.State.csproj");
    var clientTargetCommandsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetCommands.cs");
    var clientTargetSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetSurfaceBuilder.cs");
    var verseHostCommandsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseHostCommands.cs");
    var clientTargetStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetStore.cs");
    var verseDiscoveryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseDiscovery.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var commandBridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var drainStatusPath = Path.Combine(root, "Aetheria.State", "Documents", "AetheriaEveCommandDrainStatus.cs");
    var operationsProjectorPath = Path.Combine(root, "Aetheria.State", "AetheriaOperationsSurfaceProjector.cs");
    var applyPendingPath = Path.Combine(root, "Aetheria.State.ApplyPending", "Program.cs");
    var economyServerPath = Path.Combine(root, "Economy.Server", "Program.cs");

    var requiredFiles = new[]
    {
        unityFacadeProjectPath,
        stateProjectPath,
        clientTargetCommandsPath,
        clientTargetSurfaceBuilderPath,
        verseHostCommandsPath,
        clientTargetStorePath,
        verseDiscoveryPath,
        mainMenuPath,
        commandBridgePath,
        drainStatusPath,
        operationsProjectorPath,
        applyPendingPath,
        economyServerPath
    };

    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse settings shell/bridge cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var unityFacadeProject = File.ReadAllText(unityFacadeProjectPath);
    var stateProject = File.ReadAllText(stateProjectPath);
    var clientTargetCommands = File.ReadAllText(clientTargetCommandsPath);
    var clientTargetSurfaceBuilder = File.ReadAllText(clientTargetSurfaceBuilderPath);
    var verseHostCommands = File.ReadAllText(verseHostCommandsPath);
    var clientTargetStore = File.ReadAllText(clientTargetStorePath);
    var verseDiscovery = File.ReadAllText(verseDiscoveryPath);
    var mainMenu = File.ReadAllText(mainMenuPath);
    var commandBridge = File.ReadAllText(commandBridgePath);
    var drainStatus = File.ReadAllText(drainStatusPath);
    var operationsProjector = File.ReadAllText(operationsProjectorPath);
    var applyPending = File.ReadAllText(applyPendingPath);
    var economyServer = File.ReadAllText(economyServerPath);

    var requiredUnityFacadeSymbols = new[]
    {
        "AetheriaRuntimeClientTargetCommands.cs",
        "AetheriaRuntimeClientTargetSurfaceBuilder.cs",
        "AetheriaRuntimeVerseDiscovery.cs"
    };
    var missingUnityFacadeSymbols = requiredUnityFacadeSymbols
        .Where(symbol => !unityFacadeProject.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingUnityFacadeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Unity.csproj does not compile the Verse settings shell files: " +
            string.Join(", ", missingUnityFacadeSymbols));
    }

    if (!stateProject.Contains("AetheriaRuntimeVerseHostCommands.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.csproj does not compile the Verse-host Eve command contract.");
    }

    var requiredClientTargetCommandSymbols = new[]
    {
        "SurfaceId = \"aetheria.client_target\"",
        "CycleTargetKind",
        "SetTitle",
        "SetVerseId",
        "SetCultMeshAddress",
        "SetStateFilePath",
        "SetDiscoveryEndpoints",
        "DiscoverVerses",
        "SelectDiscoveredVerse",
        "IsKnown"
    };
    var missingClientTargetCommandSymbols = requiredClientTargetCommandSymbols
        .Where(symbol => !clientTargetCommands.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientTargetCommandSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Client-target command contract is incomplete: " +
            string.Join(", ", missingClientTargetCommandSymbols));
    }

    var requiredVerseHostCommandSymbols = new[]
    {
        "SurfaceId = \"aetheria.verse_host_settings\"",
        "CycleVisibility",
        "Refresh",
        "IsKnown"
    };
    var missingVerseHostCommandSymbols = requiredVerseHostCommandSymbols
        .Where(symbol => !verseHostCommands.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingVerseHostCommandSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse-host command contract is incomplete: " +
            string.Join(", ", missingVerseHostCommandSymbols));
    }

    var requiredSurfaceBuilderSymbols = new[]
    {
        "public sealed class AetheriaRuntimeClientTargetSurfaceState",
        "\"Client Target\"",
        "\"Target Fields\"",
        "\"Verse Discovery\"",
        "\"Daemon Verse Host\"",
        "\"Replica State File\"",
        "AetheriaRuntimeClientTargetCommands.SetStateFilePath",
        "AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints",
        "AetheriaRuntimeClientTargetCommands.DiscoverVerses",
        "AetheriaRuntimeClientTargetCommands.SelectDiscoveredVerse",
        "AetheriaRuntimeVerseHostCommands.CycleVisibility",
        "AetheriaRuntimeVerseHostCommands.Refresh"
    };
    var missingSurfaceBuilderSymbols = requiredSurfaceBuilderSymbols
        .Where(symbol => !clientTargetSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSurfaceBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Client-target Eve surface builder is incomplete: " +
            string.Join(", ", missingSurfaceBuilderSymbols));
    }

    if (!clientTargetStore.Contains("Update(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Client-target store no longer exposes typed update ownership for local target edits.");
    }

    var requiredVerseDiscoverySymbols = new[]
    {
        "CultMesh.CreateVerseCatalog()",
        "CultMesh.CreateVerseDiscoveryClient()",
        "DiscoverAsync(catalog, endpoints)"
    };
    var missingVerseDiscoverySymbols = requiredVerseDiscoverySymbols
        .Where(symbol => !verseDiscovery.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingVerseDiscoverySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse discovery helper no longer routes scans through the CultMesh catalog client: " +
            string.Join(", ", missingVerseDiscoverySymbols));
    }

    var requiredMainMenuSymbols = new[]
    {
        "VerseSettingsShellSurfaceId",
        "ShowVerseSettingsCommand",
        "ShowVerseSettingsSurface()",
        "HandleVerseSettingsSurfaceCommand(EveSurfaceCommandRequest request)",
        "BuildVerseSettingsSurfaceDefinition()",
        "AetheriaRuntimeClientTargetSurfaceBuilder.Build(",
        "TryCommitClientTargetCommand(",
        "TryQueueVerseHostCommand(",
        "AetheriaRuntimeClientTargetStore.Update(",
        "AetheriaRuntimeVerseDiscovery.RefreshClientTarget(",
        "ParseDiscoveryEndpoints(",
        "new EveSurfaceCommandRequest(",
        "AetheriaRuntimeVerseHostCommands.SurfaceId"
    };
    var missingMainMenuSymbols = requiredMainMenuSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingMainMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer owns the typed Verse settings shell handoff: " +
            string.Join(", ", missingMainMenuSymbols));
    }

    var requiredBridgeSymbols = new[]
    {
        "AppliedVerseHostCommands",
        "AetheriaRuntimeVerseHostCommands.Refresh",
        "AetheriaRuntimeVerseHostCommands.CycleVisibility",
        "ApplyVerseHostCommandAsync",
        "PutVerseHostSettingsAsync",
        "PutProviderAdvertisementAsync",
        "AetheriaOperationsSurfaceProjector.Build(",
        "AetheriaRuntimeVerseHostCommands.IsKnown(command)"
    };
    var missingBridgeSymbols = requiredBridgeSymbols
        .Where(symbol => !commandBridge.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBridgeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Eve command bridge no longer routes Verse-host commands through the provider owner: " +
            string.Join(", ", missingBridgeSymbols));
    }

    var requiredDrainSymbols = new[]
    {
        "AppliedVerseHostCommands"
    };
    var missingDrainSymbols = requiredDrainSymbols
        .Where(symbol => !drainStatus.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDrainSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Eve command drain status no longer records Verse-host command counts.");
    }

    if (!operationsProjector.Contains("Verse Host Commands", StringComparison.Ordinal) ||
        !operationsProjector.Contains("AppliedVerseHostCommands", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Operations surface no longer projects Verse-host Eve command counts.");
    }

    if (!applyPending.Contains("AppliedVerseHostCommands = eveReport.AppliedVerseHostCommands", StringComparison.Ordinal) ||
        !applyPending.Contains("Eve verse host commands: {eveReport.AppliedVerseHostCommands}", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.ApplyPending no longer publishes Verse-host Eve command totals.");
    }

    if (!economyServer.Contains("AppliedVerseHostCommands = report.AppliedVerseHostCommands", StringComparison.Ordinal) ||
        !economyServer.Contains("verseHost={report.AppliedVerseHostCommands}", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Economy.Server no longer publishes Verse-host Eve command totals.");
    }
}

static void RequireMainMenuVerseHostProjection(string root)
{
    var packageSnapshotPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs");
    var packageStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs");
    var runtimeStateReaderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateReader.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");

    var requiredFiles = new[]
    {
        packageSnapshotPath,
        packageStorePath,
        runtimeStateReaderPath,
        mainMenuPath
    };

    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Main-menu Verse projection cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var packageSnapshot = File.ReadAllText(packageSnapshotPath);
    var packageStore = File.ReadAllText(packageStorePath);
    var runtimeStateReader = File.ReadAllText(runtimeStateReaderPath);
    var mainMenu = File.ReadAllText(mainMenuPath);

    var requiredSnapshotSymbols = new[]
    {
        "public sealed class AetheriaRuntimeVerseHostSettingsSnapshot",
        "public string VerseId",
        "public string CultMeshAddress",
        "public string Visibility"
    };
    var missingSnapshotSymbols = requiredSnapshotSymbols
        .Where(symbol => !packageSnapshot.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSnapshotSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime snapshot does not expose typed verse-host settings: " +
            string.Join(", ", missingSnapshotSymbols));
    }

    var requiredStoreSymbols = new[]
    {
        "VerseHostSettingsSchema = \"aetheria.verse_host_settings\"",
        "VerseHostSettingsKey = \"global:aetheria.verse_host_settings.v1\"",
        "ReadVerseHostSettings(string stateFilePath)",
        "ReadVerseHostSettingsPayload",
        "AetheriaRuntimeVerseHostSettingsSnapshot"
    };
    var missingStoreSymbols = requiredStoreSymbols
        .Where(symbol => !packageStore.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingStoreSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime store does not preserve typed verse-host read authority: " +
            string.Join(", ", missingStoreSymbols));
    }

    if (!runtimeStateReader.Contains("ReadVerseHostSettings", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state reader no longer exposes typed verse-host settings.");
    }

    var requiredMainMenuSymbols = new[]
    {
        "LatestVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)",
        "AetheriaRuntimeStateReader.ReadVerseHostSettings(stateBoot.StateFilePath)",
        "\"Client Target\"",
        "\"Transport\"",
        "\"Target Source\"",
        "\"Verse\"",
        "\"Visibility\"",
        "\"CultMesh\"",
        "The client target chooses which Verse it follows; game truth belongs to the daemon serving"
    };
    var missingMainMenuSymbols = requiredMainMenuSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingMainMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer lowers daemon-owned Verse identity through its Eve shell: " +
            string.Join(", ", missingMainMenuSymbols));
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
    var runtimeStateReaderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeStateReader.cs");
    var runtimeStateReader = File.Exists(runtimeStateReaderPath)
        ? File.ReadAllText(runtimeStateReaderPath)
        : throw new InvalidOperationException("Cannot verify Continue entity readback; shared runtime state reader is missing.");
    var packageStorePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCatalogStore.cs");
    var packageStore = File.Exists(packageStorePath)
        ? File.ReadAllText(packageStorePath)
        : throw new InvalidOperationException("Cannot verify Continue entity payload readback; package runtime store is missing.");

    var requiredMenuSymbols = new[]
    {
        "LatestContinueRun",
        "AetheriaRuntimeStateReader",
        "ReadRunStates(stateBoot.StateFilePath)",
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

    if (!runtimeStateReader.Contains("ReadRunStates", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state reader no longer exposes typed run-state lookup for Continue.");
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
        "RestoreEntityGraphFromTypedRun(continuingRun)",
        "ReadEntitySnapshots(RuntimeStateFilePath)",
        "entity.RecordKey",
        "run.CurrentEntityKey",
        "ResolveCurrentEntityRecordKey(run)",
        "ReplaceZoneEntitiesFromTypedSnapshots",
        "Zone.Agents.Clear()",
        "FlattenEntityGraph(Zone)",
        "RestoreChildAndDockingRelationships",
        "RestoreCurrentEntityBinding",
        "RestoreEntityContactsFromTypedSnapshot",
        "entity.Target.Value = target",
        "string.Equals(entitySnapshot.RecordKey, currentEntityKey",
        "RestoreCurrentEntityBinding(currentEntity, actionBarBindings)",
        "RestoreActiveConsumablesFromTypedEntitySnapshot(entity, entitySnapshot)",
        "RestoreRuntimeBehaviorStateFromTypedSnapshot(entity, entitySnapshot, restoredEntities)",
        "ResolveRuntimeBehavior(entity, weaponState.OwnerKind, weaponState.OwnerIndex, weaponState.BehaviorIndex)",
        "lockWeapon.RestoreRuntimeState(",
        "drive.RestoreRuntimeState(",
        "resourceScanner.RestoreRuntimeState(",
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

    var forbiddenGameplaySymbols = new[]
    {
        "if (run == null ||\r\n            run.CurrentZoneIndex < 0 ||\r\n            run.CurrentZoneEntityIndex < 0)",
        "var currentEntityKey = $\"{zoneEntityKeyPrefix}{run.CurrentZoneEntityIndex}.v1\""
    };

    var forbiddenGameplayHits = forbiddenGameplaySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (forbiddenGameplayHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still reconstructs Continue current-entity ownership from integer slot state: " +
            string.Join(", ", forbiddenGameplayHits));
    }

    var canonicalRunStatePath = Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs");
    var canonicalRunState = File.Exists(canonicalRunStatePath)
        ? File.ReadAllText(canonicalRunStatePath)
        : throw new InvalidOperationException("Cannot verify canonical run-state authority; AetheriaRuntimeStateDocuments.cs is missing.");

    if (canonicalRunState.Contains("CurrentZoneEntityIndex", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Canonical typed run state still exposes integer current-entity slot authority.");
    }

    if (packageSnapshot.Contains("CurrentZoneEntityIndex", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity package runtime run snapshot still exposes integer current-entity slot authority.");
    }

    var pendingCommitPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateCommitDocument.cs");
    var pendingCommit = File.Exists(pendingCommitPath)
        ? File.ReadAllText(pendingCommitPath)
        : throw new InvalidOperationException("Cannot verify pending commit authority; AetheriaRuntimeStateCommitDocument.cs is missing.");
    if (pendingCommit.Contains("CurrentZoneEntityIndex", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Pending runtime commit transport still exposes integer current-entity slot authority.");
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

static void RequireDeadPropertiesPanelShellDeleted(string root)
{
    var deletedShellPaths = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "UI", "DropdownMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "DropdownMenu.cs.meta"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Properties Panel", "PropertiesPanel.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Properties Panel", "PropertiesPanel.cs.meta"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Properties Panel", "PropertiesList.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Properties Panel", "PropertiesList.cs.meta"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Dropdown Menu.prefab"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Dropdown Menu.prefab.meta"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Properties.prefab"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Properties.prefab.meta"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Property List.prefab"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Property List.prefab.meta"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Property List 1.prefab"),
        Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Property List 1.prefab.meta")
    };

    var survivingShells = deletedShellPaths
        .Where(File.Exists)
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();

    if (survivingShells.Length > 0)
    {
        throw new InvalidOperationException(
            "The dead generic popup inspector shell still survives in source or prefab assets: " +
            string.Join(", ", survivingShells));
    }

    var guidChecks = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Assets", "Scenes", "ARPG.unity")] = new[]
        {
            "8c2bf4a7080061d42a87046c37bf0c60",
            "2544c9c8b6358c54ea58c5fb33bb48b7",
            "848ae49aff071f5458dcc0322b8b84eb"
        },
        [Path.Combine(root, "Assets", "Scenes", "FieldShieldTest.unity")] = new[]
        {
            "8c2bf4a7080061d42a87046c37bf0c60",
            "2544c9c8b6358c54ea58c5fb33bb48b7",
            "848ae49aff071f5458dcc0322b8b84eb"
        },
        [Path.Combine(root, "Assets", "Prefabs", "UI", "Main Menu Canvas.prefab")] = new[]
        {
            "8c2bf4a7080061d42a87046c37bf0c60",
            "2544c9c8b6358c54ea58c5fb33bb48b7",
            "848ae49aff071f5458dcc0322b8b84eb"
        }
    };

    var survivingGuidLinks = guidChecks
        .SelectMany(entry =>
        {
            var text = File.Exists(entry.Key) ? File.ReadAllText(entry.Key) : "";
            return entry.Value
                .Where(guid => text.Contains(guid, StringComparison.Ordinal))
                .Select(guid => $"{Path.GetRelativePath(root, entry.Key)}: {guid}");
        })
        .ToArray();

    if (survivingGuidLinks.Length > 0)
    {
        throw new InvalidOperationException(
            "Scene or prefab YAML still serializes the deleted generic popup inspector shell: " +
            string.Join("; ", survivingGuidLinks));
    }
}

static void RequireTypedBehaviorMetadataCoverage(string root)
{
    var metadataPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeBehaviorMetadata.cs");
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");

    var metadata = File.Exists(metadataPath)
        ? File.ReadAllText(metadataPath)
        : throw new InvalidOperationException("Cannot verify typed behavior metadata coverage; AetheriaRuntimeBehaviorMetadata.cs is missing.");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify typed behavior metadata coverage; InventoryMenu.cs is missing.");
    var tradeMenu = File.Exists(tradeMenuPath)
        ? File.ReadAllText(tradeMenuPath)
        : throw new InvalidOperationException("Cannot verify typed behavior metadata coverage; TradeMenu.cs is missing.");

    var requiredMetadataSymbols = new[]
    {
        "AetheriaRuntimeBehaviorFieldValueKind.Temperature",
        "Behavior(\"Heat\", \"\", Stat(\"Heat\", 1))",
        "Behavior(\"MiningTool\", \"\", Stat(\"DamagePerSecond\", 1), Stat(\"Efficiency\", 2), Stat(\"Penetration\", 3), Stat(\"Range\", 4))",
        "Behavior(\"Switch\", \"\")",
        "Behavior(\"Thermotoggle\", \"\", Temperature(\"TargetTemperature\", 1))",
        "Behavior(\"Trigger\", \"\")",
        "Temperature(\"TemperatureFloor\", 3)"
    };

    var missingMetadataSymbols = requiredMetadataSymbols
        .Where(symbol => !metadata.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingMetadataSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed behavior metadata no longer covers live heat/mining/thermotoggle payloads and switch/trigger shells: " +
            string.Join(", ", missingMetadataSymbols));
    }

    var requiredUiSymbols = new[]
    {
        "AetheriaRuntimeBehaviorFieldValueKind.Temperature",
        "FormatTemperature"
    };

    var missingInventorySymbols = requiredUiSymbols
        .Where(symbol => !inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingInventorySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu no longer renders typed temperature-bearing behavior metadata: " +
            string.Join(", ", missingInventorySymbols));
    }

    var missingTradeSymbols = requiredUiSymbols
        .Where(symbol => !tradeMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTradeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu no longer renders typed temperature-bearing behavior metadata: " +
            string.Join(", ", missingTradeSymbols));
    }
}

static void RequireNameToolsUsesUiToolkit(string root)
{
    var nameToolsPath = Path.Combine(root, "Assets", "Scripts", "Editor", "NameTools.cs");
    var source = File.Exists(nameToolsPath)
        ? File.ReadAllText(nameToolsPath)
        : throw new InvalidOperationException("Cannot verify NameTools editor shell; NameTools.cs is missing.");

    var requiredSymbols = new[]
    {
        "using UnityEditor.UIElements;",
        "using UnityEngine.UIElements;",
        "private void CreateGUI()",
        "new ObjectField(\"Name File\")",
        "new IntegerField(",
        "new Toggle(\"Strip Number Tokens\")",
        "new Button(CleanNameFile)",
        "new Button(ProcessNameFile)",
        "new Button(GenerateName)"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "NameTools no longer owns a UI Toolkit editor shell for the remaining name helper workflow: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "void OnGUI()",
        "EditorGUILayout.",
        "GUILayout."
    };

    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "NameTools should not regress to IMGUI editor widgets: " +
            string.Join(", ", hits));
    }
}

static void RequireRuntimeStateReaderOwnsUnityStateAcquisition(string root)
{
    var runtimeStateReaderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateReader.cs");
    var unityFacadeProjectPath = Path.Combine(root, "Aetheria.State.Unity", "Aetheria.State.Unity.csproj");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var eveSurfacePresenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");

    var requiredPaths = new[]
    {
        runtimeStateReaderPath,
        unityFacadeProjectPath,
        actionGameManagerPath,
        mainMenuPath,
        eveSurfacePresenterPath
    };

    var missingPaths = requiredPaths
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();

    if (missingPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime state reader authority cannot be verified because required files are missing: " +
            string.Join(", ", missingPaths));
    }

    var runtimeStateReader = File.ReadAllText(runtimeStateReaderPath);
    var unityFacadeProject = File.ReadAllText(unityFacadeProjectPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var mainMenu = File.ReadAllText(mainMenuPath);
    var eveSurfacePresenter = File.ReadAllText(eveSurfacePresenterPath);

    var requiredReaderSymbols = new[]
    {
        "public static class AetheriaRuntimeStateReader",
        "OpenRuntimeCatalog",
        "ReadPlayerSettings",
        "ReadVerseHostSettings",
        "ReadLoadoutTemplates",
        "ReadRunStates",
        "ReadZoneStates",
        "ReadEntitySnapshots",
        "ReadEveSurface"
    };

    var missingReaderSymbols = requiredReaderSymbols
        .Where(symbol => !runtimeStateReader.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingReaderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime state reader is incomplete: " +
            string.Join(", ", missingReaderSymbols));
    }

    if (!unityFacadeProject.Contains("AetheriaRuntimeStateReader.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.Unity.csproj does not include the shared runtime state reader.");
    }

    var requiredActionGameManagerSymbols = new[]
    {
        "AetheriaRuntimeStateReader.ReadPlayerSettings",
        "AetheriaRuntimeStateReader.ReadLoadoutTemplates",
        "AetheriaRuntimeStateReader.OpenRuntimeCatalog",
        "AetheriaRuntimeStateReader.ReadZoneStates",
        "AetheriaRuntimeStateReader.ReadEntitySnapshots"
    };

    var missingActionGameManagerSymbols = requiredActionGameManagerSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionGameManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager does not route typed state reads through the shared runtime reader: " +
            string.Join(", ", missingActionGameManagerSymbols));
    }

    if (!mainMenu.Contains("AetheriaRuntimeStateReader", StringComparison.Ordinal) ||
        !mainMenu.Contains("ReadRunStates(stateBoot.StateFilePath)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu no longer routes Continue-run lookup through the shared runtime state reader.");
    }

    if (!eveSurfacePresenter.Contains("AetheriaRuntimeStateReader.ReadEveSurface", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve surface presenter no longer routes provider surface lookup through the shared runtime state reader.");
    }

    var forbiddenDirectStoreSymbols = new Dictionary<string, string[]>
    {
        [actionGameManagerPath] = new[]
        {
            "AetheriaRuntimeCatalogStore.ReadPlayerSettings",
            "AetheriaRuntimeCatalogStore.ReadLoadoutTemplates",
            "AetheriaRuntimeCatalogStore.OpenReadOnly",
            "AetheriaRuntimeCatalogStore.ReadZoneStates",
            "AetheriaRuntimeCatalogStore.ReadEntitySnapshots"
        },
        [mainMenuPath] = new[]
        {
            "AetheriaRuntimeCatalogStore.ReadRunStates",
            "AetheriaRuntimeCatalogStore.ReadVerseHostSettings"
        },
        [eveSurfacePresenterPath] = new[]
        {
            "AetheriaRuntimeCatalogStore.ReadEveSurfaces"
        }
    };

    var directStoreHits = forbiddenDirectStoreSymbols
        .SelectMany(entry =>
        {
            var source = File.ReadAllText(entry.Key);
            return entry.Value
                .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, entry.Key)} -> {symbol}");
        })
        .ToArray();

    if (directStoreHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay/UI still reads typed state directly from the raw store instead of the shared runtime state reader: " +
            string.Join("; ", directStoreHits));
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

    if (actionGameManager.Contains("WeaponGroupDragObject", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Weapon-group action-bar binding still keeps the dead drag-object path alive instead of routing through live gameplay APIs.");
    }

    var assignmentPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "WeaponGroupAssignment.cs");
    var elementPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "WeaponGroupElement.cs");
    var weaponGroupsPrefabPath = Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Fancy", "Weapon Groups.prefab");
    var weaponGroupPrefabPath = Path.Combine(root, "Assets", "Prefabs", "UI", "Properties Panel", "Fancy", "Weapon Group.prefab");

    var survivingLegacyPaths = new[]
    {
        assignmentPath,
        elementPath,
        weaponGroupsPrefabPath,
        weaponGroupPrefabPath
    }
        .Where(File.Exists)
        .ToArray();

    if (survivingLegacyPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Legacy inventory weapon-group uGUI shells still survive after the Eve surface cut: " +
            string.Join(", ", survivingLegacyPaths.Select(path => Path.GetRelativePath(root, path))));
    }
}

static void RequireActionBarBindingCommitAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify action-bar binding authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "CommitActionBarBinding(",
        "QueueRunCheckpoint(\"action-bar-binding\")",
        "RestoreActionBarBindingsFromTypedRun(",
        "ApplyActionBarBindings(",
        "CommitActionBarBinding(slot, dragAction)"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Action-bar binding no longer has a gameplay-owned checkpoint/restore path: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "var newbinds = Enumerable.Range(0, 64)",
        ".Zip(\r\n                    bindings,"
    };

    var legacyHits = forbiddenSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (legacyHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Action-bar binding still contains the old unconditional weapon-group overwrite path: " +
            string.Join(", ", legacyHits));
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
            "AetheriaRuntimeStateReader.ReadZoneStates(RuntimeStateFilePath)",
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
