using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

RequireGameplaySourcePurity(root);
RequirePackageSerializerBoundary(root);
RequireSharedEvePackagesImportedFromEveRepo(root);
RequireSharedRuntimeSurfaceCommandsUseCultMeshTransport(root);
RequireSharedRuntimeSurfacesUseClientNeutralVocabulary(root);
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
RequireDaemonRenderQueryAuthority(root);
RequireTypedZoneConstructionKeys(root);
RequireTypedZoneStateSnapshotKeys(root);
RequireTypedAsteroidZoneApi(root);
RequireTypedFactionShellLinks(root);
RequireFactionKeyIdentity(root);
RequireTypedRuntimeBehaviorCoverage(root);
RequireEveRuntimeBootstrap(root);
RequireNoRendererLocalConsole(root);
RequireNoRendererLocalDebugPanels(root);
RequireMainMenuSettingsCommands(root);
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
RequireTradeItemValuesUseRuntimeQueries(root);
RequireItemTierProjectionUsesRuntimeQueries(root);
RequireInventoryDropdownUseEveSurface(root);
RequireNoDeadPopupShells(root);
RequirePlayerSettingsEveSurface(root);
RequireVerseHostSettingsAuthority(root);
RequireClientTargetBootAuthority(root);
RequireVerseReplicaTool(root);
RequireVerseSettingsShellAndBridge(root);
RequireTypedStatRecipeOperations(root);
RequireTypedDaemonCommandPayloads(root);
RequireUnityPublicRequestVocabulary(root);
RequireDaemonVersePublication(root);
RequireTypedEveCommandBodies(root);
RequireMainMenuVerseHostProjection(root);
RequireMainMenuContinueRunState(root);
RequireUnityObserverDoesNotTickLocalSimulation(root);
RequireUnityDoesNotCallSharedSimulationTicks(root);
RequireUnityPhysicsIsNotGameplayAuthority(root);
RequireDeadPropertiesPanelShellDeleted(root);
RequireTypedBehaviorMetadataCoverage(root);
RequireNameToolsUsesUiToolkit(root);
RequireRuntimeStateReaderOwnsUnityStateAcquisition(root);
RequireNoDeadRuntimeProjectionCaches(root);
RequireNoDeadInspectorMetadata(root);
RequireDaemonHostDoesNotDrainRuntimeCommits(root);
RequireRuntimeSimulationTuningRequests(root);
RequireHullConductivityRequestAuthority(root);
RequireInventoryEntityRenameRequestAuthority(root);
RequireWeaponGroupRequestAuthority(root);
RequireActionBarBindingRequestAuthority(root);
RequireInventoryDoubleClickTransferRequestAuthority(root);
RequireLootPickupRequestAuthority(root);
RequireEntityDestroyedRequestAuthority(root);
RequireDroppedPickupCheckpointState(root);
RequireTradePurchaseRequestAuthority(root);
RequireInventoryLoadoutSaveRequestAuthority(root);
RequireInventoryLoadoutRestoreRequestAuthority(root);
RequireDockedCurrentShipRequestAuthority(root);

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
Console.WriteLine("Eve runtime bootstrap: daemon-published game surface mounts through UI Toolkit presenter");
Console.WriteLine("Renderer-local console authority: deleted; UI commands flow through Eve command documents");
Console.WriteLine("Renderer-local debug panels: obsolete uGUI field tester authority is deleted");
Console.WriteLine("Main-menu settings authority: player name, gameplay, and graphics settings send typed Eve commands");
Console.WriteLine("Main-menu root shell: root navigation lowers through an Eve UI Toolkit surface instead of the legacy PropertiesPanel/fade shell");
Console.WriteLine("Main-menu settings shell: settings/input subpages lower through Eve UI Toolkit surfaces, and the fake audio page is deleted until a typed audio owner exists");
Console.WriteLine("Confirmation dialog shell: runtime prompts no longer inherit the generic PropertiesPanel machinery");
Console.WriteLine("Main-menu input shell: the Eve input page delegates to the live runtime remap screen when that owner exists");
Console.WriteLine("Runtime input screen shell: input rebinding lowers through an Eve UI Toolkit surface instead of the old drag/drop uGUI screen");
Console.WriteLine("Runtime input-screen authority: hotkey and menu handoff share the same fullscreen-menu primitive");
Console.WriteLine("Sector-map zone details shell: shared zone detail surface lowers selected zone state through Eve UI Toolkit");
Console.WriteLine("Runtime menu tab shell: shared runtime tab surface lowers MenuPanel tab metadata through Eve UI Toolkit");
Console.WriteLine("Inventory ship-settings shell: shared ship settings surface lowers selected ship state through Eve UI Toolkit");
Console.WriteLine("Inventory cargo-item shell: shared cargo detail surface lowers selected item state through Eve UI Toolkit");
Console.WriteLine("Inventory equipped-item shell: shared equipped item surface lowers selected equipment state and controls through Eve UI Toolkit");
Console.WriteLine("Trade cargo-selector shell: shared cargo selector surface lowers target cargo options through Eve UI Toolkit");
Console.WriteLine("Trade filter and row-action shells: shared trade interaction surfaces lower filter and row actions through Eve UI Toolkit");
Console.WriteLine("Trade item-details shell: shared trade item surface lowers selected market item state through Eve UI Toolkit");
Console.WriteLine("Inventory dropdown shell: shared inventory dropdown surface lowers entity and loadout navigation through Eve UI Toolkit");
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
Console.WriteLine("Unity observer authority: gameplay scenes restore daemon frames and do not tick local simulation");
Console.WriteLine("Shared simulation authority: Zone/Entity/Agent ticks stay out of Unity gameplay callers");
Console.WriteLine("Ymir physics authority: gameplay code uses Ymir queries and has no Unity collision callback fallback");
Console.WriteLine("Runtime simulation tuning authority: UI sends daemon operations instead of local simulation mutation");
Console.WriteLine("Hull conductivity authority: inventory UI sends daemon operations for toggles instead of checkpoint rewrites");
Console.WriteLine("Inventory entity rename authority: UI sends daemon operations for renames instead of local graph edits");
Console.WriteLine("Weapon group authority: UI sends daemon operations for assignments instead of checkpoint rewrites");
Console.WriteLine("Action-bar binding authority: drag/drop sends daemon operations and restores from daemon frames");
Console.WriteLine("Inventory transfer authority: UI transfer and drag/drop send daemon operations");
Console.WriteLine("Loot pickup authority: collision pickup sends daemon operations instead of local pickup disposal");
Console.WriteLine("Entity destruction authority: hull-death observers send daemon operations instead of local graph deletion");
Console.WriteLine("Dropped pickup state: daemon frames carry typed dropped-pickup snapshots and keyed live lowering");
Console.WriteLine("Trade purchase authority: UI buy requests send daemon operations instead of checkpoint rewrites");
Console.WriteLine("Inventory loadout restore authority: UI restore requests send daemon operations instead of local mutation");
Console.WriteLine("Docked current-ship authority: UI selection requests send daemon operations instead of checkpoint rewrites");

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
    var unityGeneratedProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var unityAsmdefPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "GameCult.Aetheria.State.Unity.asmdef");
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
    var unityGeneratedProject = File.Exists(unityGeneratedProjectPath)
        ? File.ReadAllText(unityGeneratedProjectPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; GameCult.Aetheria.State.Unity.csproj is missing.");
    var unityAsmdef = File.Exists(unityAsmdefPath)
        ? File.ReadAllText(unityAsmdefPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; GameCult.Aetheria.State.Unity.asmdef is missing.");

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

    if (!unityGeneratedProject.Contains("GameCult.Eve.Surface.csproj", StringComparison.Ordinal) ||
        !unityGeneratedProject.Contains("<Name>GameCult.Eve.Surface</Name>", StringComparison.Ordinal) ||
        !unityAsmdef.Contains("\"GameCult.Eve.Surface\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.Unity no longer references the shared Eve surface package assembly.");
    }
}

static void RequireSharedRuntimeSurfaceCommandsUseCultMeshTransport(string root)
{
    var runtimePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime");
    if (!Directory.Exists(runtimePath))
    {
        throw new InvalidOperationException(
            "Shared runtime surface command transport cannot be verified because the state runtime package is missing.");
    }

    var runtimeSources = Directory.EnumerateFiles(runtimePath, "*.cs", SearchOption.TopDirectoryOnly)
        .ToDictionary(
            path => Path.GetRelativePath(root, path),
            File.ReadAllText,
            StringComparer.Ordinal);
    var combinedRuntime = string.Join("\n", runtimeSources.Values);

    if (!combinedRuntime.Contains("public const string CultMeshTransport = \"cultmesh\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime surface command templates do not expose a neutral CultMesh transport constant.");
    }

    var unityToolkitTransportHits = runtimeSources
        .Where(pair => pair.Value.Contains("\"unity-uitoolkit\"", StringComparison.Ordinal))
        .Select(pair => pair.Key)
        .ToArray();
    if (unityToolkitTransportHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime surface command templates still advertise Unity UI Toolkit as the transport owner: " +
            string.Join(", ", unityToolkitTransportHits));
    }

    Console.WriteLine("Shared runtime surface commands: command templates advertise CultMesh transport instead of Unity UI Toolkit");
}

static void RequireSharedRuntimeSurfacesUseClientNeutralVocabulary(string root)
{
    var runtimePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime");
    if (!Directory.Exists(runtimePath))
    {
        throw new InvalidOperationException(
            "Shared runtime surface vocabulary cannot be verified because the state runtime package is missing.");
    }

    var forbiddenSurfaceVocabulary = new[]
    {
        "Unity supplies",
        "Unity projects",
        "Unity reads",
        "Unity must observe",
        "Unity runtime shells"
    };
    var hits = Directory.EnumerateFiles(runtimePath, "*.cs", SearchOption.TopDirectoryOnly)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSurfaceVocabulary.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime surfaces still describe their contract as Unity-owned instead of client-neutral CultMesh API: " +
            string.Join("; ", hits));
    }

    Console.WriteLine("Shared runtime surfaces: surface copy uses client-neutral observer vocabulary");
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
        "AetheriaRuntimeCultCacheDocumentStore.cs",
        "AetheriaRuntimeCommandPort.cs",
        "AetheriaRuntimeSnapshotDocuments.cs",
        "AetheriaRuntimeEveCommandDocument.cs",
        "AetheriaRuntimeDaemonDocuments.cs",
        "AetheriaRuntimeDaemonSoaDocuments.cs"
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
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs")
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
            "FindCurrentDaemonZoneSnapshot()",
            "daemonZone?.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>()",
            "string.Equals(orbit.OrbitKey ?? \"\", orbital.OrbitKey, StringComparison.Ordinal)",
            "string.Equals(body.OrbitKey ?? \"\", parentOrbitKey, StringComparison.Ordinal)"
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
        [Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs")] = new[]
        {
            "private readonly List<AetheriaRuntimeDaemonBodyView> _daemonBodyViews",
            "AetheriaRuntimeDaemonRenderQueries.QueryBodyViews(_daemonZoneSnapshot, _daemonBodyViews);",
            "foreach (var bodyView in _daemonBodyViews)",
            "foreach (var pose in _daemonBodyPoses)",
            "beltPosesByBodyKey.TryGetValue(body.BodyKey ?? \"\", out var beltPose)",
            "_daemonBodyPosesByBodyKey.TryGetValue(planet.Key, out var pose)",
            "var p = new float2((float)pose.CenterX, (float)pose.CenterZ);",
            "var parent = new float2((float)pose.ParentCenterX, (float)pose.ParentCenterZ);"
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
        "orbit.Value.Position = GetOrbitPosition(orbit.Key);",
        "public int QueryGravityInfluenceBrushes(",
        "AetheriaGravityInfluenceBrush"
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
        "Planets[planetInstance.ID] = planet;",
        "public Dictionary<string, PlanetObject> Planets",
        "private readonly List<AetheriaGravityInfluenceBrush> _visibleGravityBrushes",
        "public IReadOnlyList<AetheriaGravityInfluenceBrush> VisibleGravityBrushes",
        "Zone.QueryGravityInfluenceBrushes(viewportMin, viewportMax, _visibleGravityBrushes);"
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
        "private readonly Dictionary<string, PlanetObject> _bodyViewsByBodyKey = new Dictionary<string, PlanetObject>(StringComparer.Ordinal);",
        "public bool TryGetBodyView(string bodyKey, out PlanetObject bodyView)",
        "private Dictionary<string, AsteroidBeltUI> _beltObjects = new Dictionary<string, AsteroidBeltUI>(StringComparer.Ordinal);",
        "private Dictionary<string, InstancedMesh[]> _beltMeshes = new Dictionary<string, InstancedMesh[]>(StringComparer.Ordinal);",
        "private Dictionary<string, Matrix4x4[][]> _beltMatrices = new Dictionary<string, Matrix4x4[][]>(StringComparer.Ordinal);",
        "_beltMeshes[bodyKey] = meshes.ToArray();",
        "_beltObjects[bodyKey] = belt;",
        "_bodyViewsByBodyKey[bodyKey] = planet;"
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

static void RequireDaemonRenderQueryAuthority(string root)
{
    var queryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonRenderQueries.cs");
    var querySource = File.Exists(queryPath)
        ? File.ReadAllText(queryPath)
        : throw new InvalidOperationException("Daemon render queries must live in the shared Aetheria runtime package, not in Unity ZoneRenderer.");

    var packageSnapshotPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs");
    var packageSnapshot = File.Exists(packageSnapshotPath)
        ? File.ReadAllText(packageSnapshotPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; package snapshot documents are missing.");

    var indirectRendererPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonIndirectRenderer.cs");
    var indirectRenderer = File.Exists(indirectRendererPath)
        ? File.ReadAllText(indirectRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaDaemonIndirectRenderer.cs is missing.");

    var zoneRendererPath = Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs");
    var zoneRenderer = File.Exists(zoneRendererPath)
        ? File.ReadAllText(zoneRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; ZoneRenderer.cs is missing.");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; ActionGameManager.cs is missing.");

    var canonicalSnapshotPath = Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs");
    var canonicalSnapshot = File.Exists(canonicalSnapshotPath)
        ? File.ReadAllText(canonicalSnapshotPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; canonical runtime state documents are missing.");

    var requiredQuerySymbols = new[]
    {
        "public readonly struct AetheriaRuntimeDaemonRenderSettings",
        "public readonly struct AetheriaRuntimeExponentialCurve",
        "public double ConvergenceMinimumDistance { get; }",
        "public double HypothermiaTemperature { get; }",
        "public double HeatstrokeTemperature { get; }",
        "public double SevereHeatstrokeRiskThreshold { get; }",
        "public double TargetDetectionInfoThreshold { get; }",
        "public double LockIndicatorNoiseAmplitude { get; }",
        "public double NormalizeThermalRisk(double temperature)",
        "public double NormalizeHeatstrokePost(double heatstroke)",
        "public double NormalizeSevereHeatstrokePost(double heatstroke)",
        "public double NormalizeDetectionProgress(double infoGathered)",
        "public double NormalizeTargetVisibilityFill(double infoGathered)",
        "public double NormalizeVisibilityToTargetFill(double infoGathered)",
        "public double ResolveLockIndicatorNoiseAmplitude(double lockProgress)",
        "public double ResolveLockIndicatorNoiseFrequency(double lockProgress)",
        "public double ResolveLockSpinSpeed(double lockProgress)",
        "public AetheriaRuntimeExponentialCurve TemperatureEmissionCurve { get; }",
        "public AetheriaRuntimeExponentialLerp LockIndicatorFrequency { get; }",
        "public AetheriaRuntimeExponentialLerp LockSpinSpeed { get; }",
        "public static AetheriaRuntimeGravityInfluenceBrush[] QueryGravityInfluences(",
        "AetheriaRuntimeZoneSnapshotCommit? zone",
        "public static int QueryGravityInfluences(",
        "List<AetheriaRuntimeGravityInfluenceBrush> brushes",
        "public static AetheriaRuntimeDaemonRenderGroupDocument[] QueryRenderGroups(",
        "AetheriaRuntimeDaemonSoaViewIndex? index",
        "AetheriaRuntimeXzRect",
        "IntersectsCircle(viewport, center.x, center.z, radius)",
        "IntersectsBounds(group, minX, minY, minZ, maxX, maxY, maxZ)",
        "TryResolveBodyCenter(body, orbitPositions, out var center)",
        "public static double EvaluateGravityTerrainHeight(",
        "public static double ResolveZoneRenderRadius(",
        "public static AetheriaRuntimeGravityTerrainBand QueryGravityTerrainBand(",
        "zone.GravityTerrainRadius",
        "zone.GravityTerrainWaveFrequency",
        "public static AetheriaRuntimeDaemonBodyPose[] QueryBodyPoses(",
        "public static int QueryBodyPoses(",
        "new AetheriaRuntimeDaemonBodyPose(",
        "AetheriaRuntimeDaemonBodyView",
        "public static AetheriaRuntimeDaemonBodyView[] QueryBodyViews(",
        "public static int QueryBodyViews(",
        "AetheriaRuntimeDaemonAsteroidBeltPose",
        "public static AetheriaRuntimeDaemonAsteroidBeltPose[] QueryAsteroidBeltPoses(",
        "public static int QueryAsteroidBeltPoses(",
        "AetheriaRuntimeDaemonAsteroidInstancePose",
        "public static AetheriaRuntimeDaemonAsteroidInstancePose[] QueryAsteroidInstancePoses(",
        "public static int QueryAsteroidInstancePoses(",
        "TryResolveAsteroidBeltCenter(body, orbitPositions, orbits, out var center)",
        "ResolveAsteroidBeltRadius(body)",
        "AetheriaRuntimeDaemonCompassMarker",
        "public static AetheriaRuntimeDaemonCompassMarker[] QueryCompassMarkers(",
        "public static int QueryCompassMarkers(",
        "public static int[] QueryVisibleEntityIndices(",
        "public static int QueryVisibleEntityIndices(",
        "AetheriaRuntimeDaemonEntityContact",
        "public static bool TryQueryEntityContact(",
        "public static AetheriaRuntimeDaemonEntityContact[] QueryEntityContacts(",
        "public static int QueryEntityContacts(",
        "AetheriaRuntimeDaemonEntityTarget",
        "public static bool TryQueryEntityTarget(",
        "AetheriaRuntimeDaemonWormholeExit",
        "public static AetheriaRuntimeDaemonWormholeExit[] QueryWormholeExits(",
        "public static int QueryWormholeExits(",
        "BuildZoneMap(run)",
        "BuildEntityMap(zone)"
    };

    var missingQuerySymbols = requiredQuerySymbols
        .Where(symbol => !querySource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingQuerySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon render query API must expose gravity-in-rect and render-group bounds queries over daemon snapshot/SoA data: " +
            string.Join(", ", missingQuerySymbols));
    }

    var requiredSnapshotSymbols = new[]
    {
        "public double GravityInfluenceCenterX",
        "public double GravityInfluenceCenterZ",
        "public double GravityInfluenceRadius",
        "public double GravityWellDepth",
        "public double GravityWaveRadius",
        "public double GravityWaveDepth",
        "public double GravityWaveSpeed",
        "public double GravityTerrainRadius",
        "public double GravityTerrainDepth",
        "public double GravityTerrainDepthExponent",
        "public double GravityTerrainBoundaryFog",
        "public double GravityTerrainWaveFrequency",
        "public double SimulationTimeSeconds"
    };

    var missingPackageSnapshotSymbols = requiredSnapshotSymbols
        .Where(symbol => !packageSnapshot.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeSnapshotDocuments.cs: missing {symbol}");
    var missingCanonicalSnapshotSymbols = requiredSnapshotSymbols
        .Where(symbol => !canonicalSnapshot.Contains(symbol, StringComparison.Ordinal))
        .Select(symbol => $"Aetheria.State/Documents/AetheriaRuntimeStateDocuments.cs: missing {symbol}");
    var missingSnapshotSymbols = missingPackageSnapshotSymbols.Concat(missingCanonicalSnapshotSymbols).ToArray();
    if (missingSnapshotSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon body snapshots must publish resolved gravity influence fields so render clients do not reconstruct Unity gravity state: " +
            string.Join("; ", missingSnapshotSymbols));
    }

    var requiredRendererSymbols = new[]
    {
        "private readonly List<AetheriaRuntimeDaemonRenderGroupDocument> _visibleRenderGroups",
        "AetheriaRuntimeDaemonRenderQueries.QueryRenderGroups(",
        "TryGetCameraQueryBounds(out var min, out var max)",
        "return index.RenderGroups;"
    };
    var missingRendererSymbols = requiredRendererSymbols
        .Where(symbol => !indirectRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingRendererSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity's daemon indirect renderer must query daemon SoA render groups by camera bounds before lowering draw calls: " +
            string.Join(", ", missingRendererSymbols));
    }

    if (zoneRenderer.Contains("Zone.GetHeight(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must evaluate gravity terrain from daemon render queries instead of the mirrored Unity Zone height evaluator.");
    }

    if (zoneRenderer.Contains("public Zone Zone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("public void LoadZone(", StringComparison.Ordinal) ||
        zoneRenderer.Contains("public Dictionary<string, PlanetObject> Planets", StringComparison.Ordinal) ||
        zoneRenderer.Contains("public ItemManager ItemManager", StringComparison.Ordinal) ||
        actionGameManager.Contains("ZoneRenderer.ItemManager", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must expose daemon-view rendering concepts instead of public mirrored Zone/ItemManager ownership.");
    }

    if (zoneRenderer.Contains("ItemManager.Log(", StringComparison.Ordinal) ||
        zoneRenderer.Contains("ItemManager.Get", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must not depend on ItemManager for renderer diagnostics or item projection.");
    }

    if (zoneRenderer.Contains("Zone.PowerPulse(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon gravity terrain bands instead of calling mirrored Unity Zone gravity math.");
    }

    if (zoneRenderer.Contains("Settings.PlanetSettings.GravityRadius.Evaluate(", StringComparison.Ordinal) ||
        zoneRenderer.Contains("Settings.PlanetSettings.GravityDepth.Evaluate(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must lower daemon-authored gravity influence radius/depth instead of reconstructing gravity brushes from Unity settings.");
    }

    if (zoneRenderer.Contains("Zone.GetOrbitPosition(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must position bodies from daemon render body poses instead of the mirrored Unity Zone orbit evaluator.");
    }

    if (zoneRenderer.Contains("Zone.Time", StringComparison.Ordinal) ||
        zoneRenderer.Contains("Zone?.Time", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must sample animation and terrain from daemon simulation time instead of mirrored Unity Zone time.");
    }

    if (zoneRenderer.Contains("Zone.PlanetInstances[planet.Key]", StringComparison.Ordinal) ||
        zoneRenderer.Contains("zone.PlanetInstances.TryGetValue(pose.BodyKey, out var planet)", StringComparison.Ordinal) ||
        zoneRenderer.Contains("LoadPlanet(planet)", StringComparison.Ordinal) ||
        zoneRenderer.Contains("zone.AsteroidBelts.TryGetValue(body.BodyKey", StringComparison.Ordinal) ||
        zoneRenderer.Contains("LoadAsteroidBelt(belt)", StringComparison.Ordinal) ||
        zoneRenderer.Contains("new Wormhole", StringComparison.Ordinal) ||
        zoneRenderer.Contains("zone.Galaxy", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer frame updates must read body render state from daemon body poses instead of indexed mirrored Unity planets.");
    }

    if (zoneRenderer.Contains("foreach (var orbit in zone.Orbits.Values)", StringComparison.Ordinal) ||
        zoneRenderer.Contains("FirstOrDefault(body => body.OrbitKey == orbit.OrbitKey)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must instantiate body views from daemon body poses keyed by body id instead of rebuilding a Unity orbit hierarchy.");
    }

    if (zoneRenderer.Contains("belt.OrbitPosition", StringComparison.Ordinal) ||
        zoneRenderer.Contains("belt.Radius", StringComparison.Ordinal) ||
        zoneRenderer.Contains("belt.Transforms", StringComparison.Ordinal) ||
        zoneRenderer.Contains("foreach (var (key, belt) in Zone.AsteroidBelts)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer frame updates must read asteroid belt and instance poses from daemon render queries instead of mirrored Unity asteroid belts.");
    }

    if (zoneRenderer.Contains("_zoneSubscriptions", StringComparison.Ordinal) ||
        zoneRenderer.Contains("zone.Entities.ObserveAdd()", StringComparison.Ordinal) ||
        zoneRenderer.Contains("zone.Entities.ObserveRemove()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must reconcile rendered entities from daemon frame snapshots instead of subscribing to mirrored Unity entity collection mutations.");
    }

    if (zoneRenderer.Contains("foreach (var entity in legacyEntityFacadeZone.Entities)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must use the daemon-indexed observed facade projection supplied by gameplay instead of enumerating Unity Zone.Entities.");
    }

    if (zoneRenderer.Contains("_legacyEntityFacadeZone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("_legacyEntityFacadeZone?.Radius", StringComparison.Ordinal) ||
        zoneRenderer.Contains("_legacyLootFacadeZone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("Zone legacyLootFacadeZone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("gridObject.Zone = ", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must not keep Unity Zone facade handles; daemon snapshots and observed facade projections own renderer input.");
    }

    if (zoneRenderer.Contains("PerspectiveEntity.EntityInfoGathered", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon compass markers instead of reading mirrored Unity entity visibility state.");
    }

    if (zoneRenderer.Contains("VisibleEntities", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon visible entity indices instead of subscribing to mirrored Unity visibility collections.");
    }

    if (zoneRenderer.Contains("AdjacentZones", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon wormhole exits instead of enumerating mirrored GalaxyZone adjacency.");
    }

    var requiredZoneRendererSymbols = new[]
    {
        "public AetheriaRuntimeDaemonRenderSettings RenderSettings { get; set; }",
        "private AetheriaRuntimeRunCheckpointCommit _daemonRunSnapshot;",
        "private AetheriaRuntimeZoneSnapshotCommit _daemonZoneSnapshot;",
        "private readonly List<AetheriaRuntimeDaemonBodyView> _daemonBodyViews",
        "private readonly List<AetheriaRuntimeDaemonBodyPose> _daemonBodyPoses",
        "private readonly Dictionary<string, AetheriaRuntimeDaemonBodyPose> _daemonBodyPosesByBodyKey",
        "private readonly Dictionary<string, PlanetObject> _bodyViewsByBodyKey",
        "public bool TryGetBodyView(string bodyKey, out PlanetObject bodyView)",
        "private readonly List<AetheriaRuntimeDaemonAsteroidBeltPose> _daemonAsteroidBeltPoses",
        "private readonly List<AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkers",
        "private readonly Dictionary<int, AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkersByEntityIndex",
        "private readonly List<int> _daemonVisibleEntityIndices",
        "private readonly HashSet<int> _daemonVisibleEntityIndicesSet",
        "private readonly HashSet<int> _visibleDaemonEntityIndices",
        "private readonly List<AetheriaRuntimeDaemonWormholeExit> _daemonWormholeExits",
        "public Dictionary<int, (GameObject gravity, CompassIcon icon)> WormholeInstances",
        "public void LoadDaemonZoneView(",
        "IReadOnlyDictionary<int, Entity> observedEntityFacadesByDaemonIndex,",
        "AetheriaRuntimeRunCheckpointCommit daemonRun = null",
        "public void ApplyDaemonFrame(",
        "AetheriaRuntimeZoneSnapshotCommit daemonZone,",
        "AetheriaRuntimeRunCheckpointCommit daemonRun)",
        "_daemonZoneSnapshot = daemonZone;",
        "_daemonRunSnapshot = daemonRun;",
        "AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(",
        "AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(",
        "            2000);",
        "AetheriaRuntimeDaemonRenderQueries.QueryGravityTerrainBand(",
        "private readonly List<AetheriaRuntimeDaemonAsteroidInstancePose> _visibleAsteroidInstancePoses",
        "foreach (var bodyView in _daemonBodyViews)",
        "if (bodyView.IsAsteroidBelt)",
        "AetheriaRuntimeDaemonRenderQueries.QueryBodyViews(_daemonZoneSnapshot, _daemonBodyViews);",
        "foreach (var pose in _daemonBodyPoses)",
        "LoadPlanet(body)",
        "void LoadPlanet(AetheriaRuntimeBodySnapshotCommit body)",
        "beltPosesByBodyKey.TryGetValue(body.BodyKey ?? \"\", out var beltPose)",
        "LoadAsteroidBelt(beltPose)",
        "void LoadAsteroidBelt(AetheriaRuntimeDaemonAsteroidBeltPose beltPose)",
        "foreach (var entitySnapshot in daemonZone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())",
        "observedEntityFacadesByDaemonIndex.TryGetValue(entitySnapshot.EntityIndex, out var entity)",
        "private readonly Dictionary<int, EntityInstance> _entityInstancesByDaemonIndex",
        "public IReadOnlyDictionary<int, EntityInstance> DaemonEntityInstances => _entityInstancesByDaemonIndex;",
        "public bool TryGetEntityInstance(int daemonEntityIndex, out EntityInstance instance)",
        "public bool TryGetEntityInstance(Entity entity, out EntityInstance instance)",
        "public bool TryGetDaemonTargetDistance(int daemonEntityIndex, out float distance)",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityTarget(",
        "foreach (var entity in EntityInstances.Keys.ToArray())",
        "AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(_daemonZoneSnapshot, _daemonBodyPoses);",
        "AetheriaRuntimeDaemonRenderQueries.QueryAsteroidBeltPoses(_daemonZoneSnapshot, _daemonAsteroidBeltPoses);",
        "foreach (var beltPose in _daemonAsteroidBeltPoses)",
        "AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(",
        "AetheriaRuntimeDaemonRenderQueries.QueryCompassMarkers(",
        "AetheriaRuntimeDaemonRenderQueries.QueryVisibleEntityIndices(",
        "AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(",
        "AddWormhole(exit)",
        "public void AddWormhole(AetheriaRuntimeDaemonWormholeExit exit)",
        "private double DaemonSimulationTimeSeconds => _daemonZoneSnapshot?.SimulationTimeSeconds ?? 0;",
        "_daemonCompassMarkersByEntityIndex.TryGetValue(entityInstance.DaemonEntityIndex, out var marker)",
        "_daemonBodyPosesByBodyKey.TryGetValue(planet.Key, out var pose)",
        "pose.GravityWaveSpeed"
    };
    var missingZoneRendererSymbols = requiredZoneRendererSymbols
        .Where(symbol => !zoneRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingZoneRendererSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ZoneRenderer must lower daemon-authored gravity terrain snapshots instead of recomputing render height through Zone: " +
            string.Join(", ", missingZoneRendererSymbols));
    }

    var entityInstancePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "EntityInstance.cs");
    var entityInstance = File.Exists(entityInstancePath)
        ? File.ReadAllText(entityInstancePath)
        : throw new InvalidOperationException("Cannot verify EntityInstance convergence authority; source file is missing.");
    if (entityInstance.Contains("Entity.Target.Value != null ? Mathf.Max(Entity.TargetRange", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "EntityInstance convergence distance must read daemon target projection through ZoneRenderer instead of facade Entity.Target.");
    }

    if (entityInstance.Contains("Entity.ItemManager.GameplaySettings", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "EntityInstance render code must use daemon render settings instead of drilling through the observed Entity ItemManager facade.");
    }

    var requiredEntityInstanceSymbols = new[]
    {
        "ZoneRenderer.RenderSettings.TemperatureEmissionCurve.Evaluate(",
        "ZoneRenderer.TryGetDaemonTargetDistance(DaemonEntityIndex, out var daemonTargetDistance)",
        "Mathf.Max(daemonTargetDistance, (float)ZoneRenderer.RenderSettings.ConvergenceMinimumDistance)",
        "LookAtPoint.position = transform.position + entityLookDirection * lookAtDistance;"
    };

    var missingEntityInstanceSymbols = requiredEntityInstanceSymbols
        .Where(symbol => !entityInstance.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingEntityInstanceSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "EntityInstance no longer resolves convergence from daemon target projection: " +
            string.Join(", ", missingEntityInstanceSymbols));
    }

    var schematicDisplayPath = Path.Combine(root, "Assets", "Scripts", "UI", "HUD", "SchematicDisplay.cs");
    var schematicDisplay = File.Exists(schematicDisplayPath)
        ? File.ReadAllText(schematicDisplayPath)
        : throw new InvalidOperationException("Cannot verify HUD thermal presentation authority; SchematicDisplay.cs is missing.");
    if (schematicDisplay.Contains("Settings.GameplaySettings.HypothermiaTemperature", StringComparison.Ordinal) ||
        schematicDisplay.Contains("Settings.GameplaySettings.HeatstrokeTemperature", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "SchematicDisplay must normalize cockpit thermal risk through shared daemon render settings instead of Unity GameSettings.");
    }

    if (!schematicDisplay.Contains("ZoneRenderer.RenderSettings.NormalizeThermalRisk(", StringComparison.Ordinal) ||
        !actionGameManager.Contains("Settings.GameplaySettings.HypothermiaTemperature", StringComparison.Ordinal) ||
        !actionGameManager.Contains("Settings.GameplaySettings.HeatstrokeTemperature", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "HUD thermal presentation no longer flows through the shared daemon render settings bridge.");
    }

    var requiredActionGameManagerRenderSymbols = new[]
    {
        "renderSettings.NormalizeDetectionProgress(",
        "renderSettings.NormalizeHeatstrokePost(",
        "renderSettings.NormalizeSevereHeatstrokePost(",
        "renderSettings.NormalizeTargetVisibilityFill(",
        "renderSettings.NormalizeVisibilityToTargetFill(",
        "renderSettings.ResolveLockIndicatorNoiseAmplitude(",
        "renderSettings.ResolveLockIndicatorNoiseFrequency(",
        "renderSettings.ResolveLockSpinSpeed(",
        "new AetheriaRuntimeExponentialLerp(",
        "Settings.GameplaySettings.TargetDetectionInfoThreshold",
        "Settings.GameplaySettings.SevereHeatstrokeRiskThreshold",
        "Settings.GameplaySettings.LockIndicatorNoiseAmplitude"
    };

    var missingActionGameManagerRenderSymbols = requiredActionGameManagerRenderSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingActionGameManagerRenderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer bridges Unity render tuning through shared daemon render settings: " +
            string.Join(", ", missingActionGameManagerRenderSymbols));
    }

    var forbiddenRenderLoopSymbols = new[]
    {
        "Settings.GameplaySettings.TargetDetectionInfoThreshold",
        "Settings.GameplaySettings.SevereHeatstrokeRiskThreshold",
        "Settings.GameplaySettings.LockIndicatorNoiseAmplitude",
        "Settings.GameplaySettings.LockIndicatorFrequency",
        "Settings.GameplaySettings.LockSpinSpeed"
    };
    var renderLoopHits = FindMethodScopedLineHits(actionGameManager, forbiddenRenderLoopSymbols)
        .Where(hit => hit.MethodName is "Update" or "UpdateTargetIndicators")
        .ToArray();
    if (renderLoopHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager render loops must interpret observed state through shared daemon render settings instead of Unity GameSettings: " +
            string.Join(", ", renderLoopHits.Select(hit => $"{hit.MethodName}:{hit.LineNumber}:{hit.Line.Trim()}")));
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
            "public string OrbitKey { get; }",
            "GravityInfluenceCenterX = gravityInfluenceCenterX;",
            "GravityInfluenceCenterZ = gravityInfluenceCenterZ;",
            "GravityInfluenceRadius = gravityInfluenceRadius;",
            "GravityWellDepth = gravityWellDepth;",
            "GravityWaveRadius = gravityWaveRadius;",
            "GravityWaveDepth = gravityWaveDepth;",
            "GravityWaveSpeed = gravityWaveSpeed;",
            "GravityTerrainRadius = gravityTerrainRadius;",
            "GravityTerrainDepth = gravityTerrainDepth;",
            "GravityTerrainDepthExponent = gravityTerrainDepthExponent;",
            "GravityTerrainBoundaryFog = gravityTerrainBoundaryFog;",
            "GravityTerrainWaveFrequency = gravityTerrainWaveFrequency;"
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs")] = new[]
        {
            "var orbitKey = ReadFieldString(ref reader, orbitFields, 0);",
            "var parentOrbitKey = ReadFieldString(ref reader, orbitFields, 1);",
            "var bodyKey = ReadFieldString(ref reader, bodyFields, 0);",
            "var orbitKey = ReadFieldString(ref reader, bodyFields, 3);",
            "var gravityInfluenceCenterX = ReadFieldDouble(ref reader, bodyFields, 13, double.NaN);",
            "var gravityInfluenceCenterZ = ReadFieldDouble(ref reader, bodyFields, 14, double.NaN);",
            "var gravityInfluenceRadius = ReadFieldDouble(ref reader, bodyFields, 15);",
            "var gravityWellDepth = ReadFieldDouble(ref reader, bodyFields, 16);",
            "var gravityWaveRadius = ReadFieldDouble(ref reader, bodyFields, 17);",
            "var gravityWaveDepth = ReadFieldDouble(ref reader, bodyFields, 18);",
            "var gravityWaveSpeed = ReadFieldDouble(ref reader, bodyFields, 19);",
            "var gravityTerrainRadius = ReadFieldDouble(ref reader, fields, 9);",
            "var gravityTerrainDepth = ReadFieldDouble(ref reader, fields, 10);",
            "var gravityTerrainDepthExponent = ReadFieldDouble(ref reader, fields, 11, 1.0);",
            "var gravityTerrainBoundaryFog = ReadFieldDouble(ref reader, fields, 12);",
            "var gravityTerrainWaveFrequency = ReadFieldDouble(ref reader, fields, 13, 1.0);",
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
    var daemonSurfaceCommandsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeDaemonSurfaceCommands.cs");

    if (!File.Exists(bootstrapPath))
    {
        throw new InvalidOperationException("Aetheria Eve runtime bootstrap is missing.");
    }

    if (!File.Exists(presenterPath))
    {
        throw new InvalidOperationException("Aetheria Eve surface presenter is missing.");
    }

    if (!File.Exists(daemonSurfaceCommandsPath))
    {
        throw new InvalidOperationException("Aetheria daemon surface command router is missing.");
    }

    var bootstrap = File.ReadAllText(bootstrapPath);
    if (!bootstrap.Contains("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) ||
        !bootstrap.Contains("DefaultSurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId", StringComparison.Ordinal) ||
        !bootstrap.Contains("AetheriaEveSurfacePresenter", StringComparison.Ordinal) ||
        !bootstrap.Contains("UIDocument", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve runtime bootstrap no longer mounts the daemon-published game surface through the UI Toolkit presenter.");
    }

    var presenter = File.ReadAllText(presenterPath);
    var daemonSurfaceCommands = File.ReadAllText(daemonSurfaceCommandsPath);
    var requiredPresenterSymbols = new[]
    {
        "private string surfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeStateReader.ReadEveSurface(statePath, surfaceId)",
        "AetheriaRuntimeStateReader.ReadEveSurface(stateBoot.StateFilePath, surfaceId)",
        "private bool ShouldMountSurface(",
        "_mountedSurfaceVersion != surface.Version",
        "private static readonly AetheriaEveUnitySurfaceChrome RootOnlyChrome",
        "UseShell = false",
        "MountSurface(string statePath, EveSurfaceDocument surface)",
        "AetheriaEveUnitySurfaceHost.Render(",
        "AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(statePath, request, out var daemonEnvelope)",
        "AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand("
    };
    var missingPresenterSymbols = requiredPresenterSymbols
        .Where(symbol => !presenter.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingPresenterSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter no longer lowers refreshed daemon/CultMesh surfaces through typed Eve commands: " +
            string.Join(", ", missingPresenterSymbols));
    }

    if (presenter.Contains("new EveUiToolkitSurfaceLowerer", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter still lowers daemon-published surfaces directly instead of delegating to the shared Unity Eve surface host.");
    }

    var daemonSubmitIndex = presenter.IndexOf(
        "AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(statePath, request, out var daemonEnvelope)",
        StringComparison.Ordinal);
    var eveSubmitIndex = presenter.IndexOf(
        "AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand(",
        StringComparison.Ordinal);
    if (daemonSubmitIndex < 0 || eveSubmitIndex < 0 || daemonSubmitIndex > eveSubmitIndex)
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter must route daemon-published surface commands to the typed daemon boundary before falling back to Eve requests.");
    }

    var requiredDaemonSurfaceCommandSymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonSurfaceCommands",
        "EveSurfaceCommandRequest request",
        "request.ProviderId, \"aetheria.daemon\"",
        "AetheriaRuntimeStateReader.TryReadObservedDaemonState(stateFilePath, out var observed)",
        "new AetheriaRuntimeDaemonOperationClient(",
        "AetheriaRuntimeDaemonSurfaceCommandCatalog.TrySubmitArgumentless(",
        "AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix"
    };
    var missingDaemonSurfaceCommandSymbols = requiredDaemonSurfaceCommandSymbols
        .Where(symbol => !daemonSurfaceCommands.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonSurfaceCommandSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing no longer submits typed daemon operations: " +
            string.Join(", ", missingDaemonSurfaceCommandSymbols));
    }

    var forbiddenDaemonSurfaceCommandSymbols = new[]
    {
        "request.Payload.TryGetValue",
        "ApplyPayload(",
        "ReadInt(",
        "ReadDouble(",
        "ReadString(",
        "\"commandKind\""
    };
    var daemonSurfaceCommandHits = forbiddenDaemonSurfaceCommandSymbols
        .Where(symbol => daemonSurfaceCommands.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (daemonSurfaceCommandHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing must lower command ids to typed daemon documents, not decode string payload maps: " +
            string.Join(", ", daemonSurfaceCommandHits));
    }

    if (daemonSurfaceCommands.Contains("AetheriaRuntimeCommandSubmitter.TrySubmitDaemonCommand(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing still calls the generic daemon submitter instead of the typed daemon operation client.");
    }

    if (daemonSurfaceCommands.Contains("TrySendCommandKind(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing still uses a generic command-kind submission back door instead of explicit typed operations.");
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

static void RequireMainMenuSettingsCommands(string root)
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
        throw new InvalidOperationException("Cannot verify main-menu settings command path; MainMenu.cs is missing.");
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

    var forbiddenSymbols = new[]
    {
        "ActionGameManager.RuntimePlayerSettings.Name =",
        "ActionGameManager.RuntimePlayerSettings.GameplaySettings.TemperatureUnit =",
        "ActionGameManager.RuntimePlayerSettings.GameplaySettings.SignificantDigits =",
        "ActionGameManager.RuntimePlayerSettings.GraphicsSettings.NebulaQuality =",
        "ActionGameManager.RuntimePlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap =",
        "ActionGameManager.RequestRuntimePlayerSettingsCommand",
        "ActionGameManager.RequestRuntimePlayerName",
        "ActionGameManager.RequestRuntimeTemperatureUnit",
        "ActionGameManager.RequestRuntimeSignificantDigits",
        "ActionGameManager.RequestRuntimeNebulaQuality",
        "ActionGameManager.RequestRuntimeShowAsteroidsInMinimap"
    };

    var actionGameManagerForbiddenSymbols = new[]
    {
        "RequestRuntimePlayerSettingsCommand",
        "RequestRuntimePlayerName",
        "RequestRuntimeTemperatureUnit",
        "RequestRuntimeSignificantDigits",
        "RequestRuntimeNebulaQuality",
        "RequestRuntimeShowAsteroidsInMinimap"
    };

    var hits = File.ReadLines(mainMenuPath)
        .Select((line, index) => new { Path = mainMenuPath, LineNumber = index + 1, Line = line })
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Concat(File.ReadLines(actionGameManagerPath)
            .Select((line, index) => new { Path = actionGameManagerPath, LineNumber = index + 1, Line = line })
            .Where(line => actionGameManagerForbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal))))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity still owns direct player-settings mutation instead of sending Eve commands: " +
            string.Join("; ", hits));
    }

    var requiredMainMenuAuthoritySymbols = new[]
    {
        "SendKnownAetheriaEveCommand(request, \"player-settings\")",
        "AetheriaRuntimeMainMenuCommandKind.PlayerSettingsCommand",
        "AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand(",
        "stateBoot.StateFilePath",
        "\"unity-main-menu\""
    };

    var missingUiCalls = requiredMainMenuAuthoritySymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingUiCalls.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer routes settings changes through the Aetheria Eve command boundary: " +
            string.Join(", ", missingUiCalls));
    }

    var forbiddenSubmitAcceptanceSymbols = new[]
    {
        "private static bool TrySendKnownAetheriaEveCommand(",
        "if (!TrySendKnownAetheriaEveCommand(request, \"player-settings\"))"
    };
    var submitAcceptanceHits = forbiddenSubmitAcceptanceSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (submitAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu player-settings command lowering still treats Eve submission as local acceptance state: " +
            string.Join(", ", submitAcceptanceHits));
    }

    if (source.Contains("TrySendPlayerSettingsCommand(", StringComparison.Ordinal) ||
        source.Contains("CommandKindForSurface(request)", StringComparison.Ordinal) ||
        source.Contains("new AetheriaRuntimePlayerSettingsCommandBody", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu still decodes player-settings Eve payloads locally instead of delegating to the typed command client.");
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
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildPlayerSettingsShell(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectPlayerSettings(",
        "LatestPlayerSettings(CurrentStateBoot())",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaRuntimeMainMenuCommandKind.PlayerSettingsCommand",
        "SendKnownAetheriaEveCommand(request, \"player-settings\")"
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
        "ActionGameManager.RequestRuntimePlayerName(evt.newValue)",
        "TryQueuePlayerSettingsCommand(",
        "TryQueueAetheriaEveCommand(",
        "AetheriaRuntimeEveCommandLog.QueueCommand(",
        "BuildPlayerSettingsSurfaceDefinition()",
        "ProjectPlayerSettingsSurfaceState(",
        "new AetheriaRuntimePlayerSettingsSurfaceState(",
        "ResolveMenuSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer",
        "host.AddComponent<UIDocument>",
        "new EveSurfaceCommandRequest("
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
    var mainMenuSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeMainMenuSurfaceBuilder.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu settings shell; MainMenu.cs is missing.");
    }
    if (!File.Exists(mainMenuSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu settings shell; AetheriaRuntimeMainMenuSurfaceBuilder.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var mainMenuSurfaceBuilder = File.ReadAllText(mainMenuSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderMenuSurface(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildPlayerSettingsShell(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectPlayerSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildVerseSettingsShell(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectVerseSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(",
        "HandleSettingsSurfaceCommand(",
        "HandleVerseSettingsSurfaceCommand(",
        "HandleInputSettingsSurfaceCommand(",
        "AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeMainMenuCommandKind.ShowPlayerSettings",
        "AetheriaRuntimeMainMenuCommandKind.ShowVerseSettings",
        "AetheriaRuntimeMainMenuCommandKind.ShowInputSettings",
        "AetheriaRuntimeMainMenuCommandKind.BackToMain",
        "AetheriaRuntimeMainMenuCommandKind.BackToSettings",
        "AetheriaRuntimeMainMenuCommandKind.OpenRuntimeInputScreen",
        "LatestPlayerSettings(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_menuSurfaceDocument)"
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
        "BuildSettingsSurfaceDefinition(",
        "BuildInputSettingsSurfaceDefinition(",
        "BuildPlayerSettingsSurfaceDefinition(",
        "BuildVerseSettingsSurfaceDefinition(",
        "private static EveSurfaceComponent Button(",
        "private static EveSurfaceComponent Card(",
        "_nextMenu.panel.AddButton(\"Player Settings\"",
        "_nextMenu.panel.AddButton(\"Verse\"",
        "_nextMenu.panel.AddButton(\"Input\"",
        "_nextMenu.panel.AddButton(\"Audio\"",
        "BuildAudioSettingsSurfaceDefinition(",
        "HandleAudioSettingsSurfaceCommand(",
        "ShowAudioSettingsCommand",
        "ResolveMenuSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer",
        "host.AddComponent<UIDocument>",
        "switch (request.Command)",
        "string.Equals(request.Command, AetheriaRuntimeMainMenuCommands.BackToSettings",
        "string.Equals(request.Command, AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen",
        "TrySendVerseHostCommand(request.Command)",
        "CommandKindForSurface(request)",
        "AetheriaRuntimeClientTargetCommands.IsKnown(request.Command",
        "ProjectPlayerSettingsSurfaceState(",
        "ProjectVerseSettingsSurfaceState(",
        "ProjectMainMenuSurfaceState(",
        "new AetheriaRuntimePlayerSettingsSurfaceState(",
        "new AetheriaRuntimeClientTargetSurfaceState(",
        "new AetheriaRuntimeMainMenuSurfaceState(",
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

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeMainMenuSurfaceBuilder",
        "public static class AetheriaRuntimeMainMenuCommands",
        "BuildSettings(",
        "BuildInputSettings(",
        "BuildPlayerSettingsShell(",
        "BuildVerseSettingsShell(",
        "ProjectRoot(",
        "ProjectPlayerSettings(",
        "ProjectVerseSettings(",
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(state, version)",
        "AetheriaRuntimeClientTargetSurfaceBuilder.Build(state, version)",
        "WithBackAction(",
        "AetheriaRuntimeMainMenuCommands.BackToSettings",
        "public enum AetheriaRuntimeMainMenuCommandKind",
        "public readonly struct AetheriaRuntimeMainMenuCommand",
        "public static class AetheriaRuntimeMainMenuSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeMainMenuCommands.ShowPlayerSettings",
        "AetheriaRuntimeMainMenuCommands.ShowVerseSettings",
        "AetheriaRuntimeMainMenuCommands.ShowInputSettings",
        "AetheriaRuntimeMainMenuCommands.BackToSettings"
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !mainMenuSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared main-menu surface builder no longer owns the settings/input shell contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireMainMenuRootUsesEveSurface(string root)
{
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var mainMenuSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeMainMenuSurfaceBuilder.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu root shell; MainMenu.cs is missing.");
    }
    if (!File.Exists(mainMenuSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu root shell; AetheriaRuntimeMainMenuSurfaceBuilder.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var mainMenuSurfaceBuilder = File.ReadAllText(mainMenuSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(",
        "AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeMainMenuCommandKind.ContinueRun",
        "AetheriaRuntimeMainMenuCommandKind.NewGame",
        "AetheriaRuntimeMainMenuCommandKind.ShowSettings",
        "AetheriaRuntimeMainMenuCommandKind.Quit",
        "HandleMainSurfaceCommand(",
        "LatestDaemonFrame(stateBoot)",
        "LatestVerseHostSettings(stateBoot)",
        "LatestPlayerSettings(stateBoot)",
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
        "BuildMainSurfaceDefinition(",
        "private static EveSurfaceDocument BuildMenuSurfaceDocument(",
        "switch (request.Command)",
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

    var requiredBuilderSymbols = new[]
    {
        "BuildRoot(",
        "AetheriaRuntimeMainMenuCommands.RootSurfaceId",
        "AetheriaRuntimeMainMenuCommands.ContinueRun",
        "AetheriaRuntimeMainMenuCommands.NewGame",
        "AetheriaRuntimeMainMenuCommands.ShowSettings",
        "AetheriaRuntimeMainMenuCommands.Quit",
        "AetheriaRuntimeMainMenuSurfaceState",
        "public enum AetheriaRuntimeMainMenuCommandKind",
        "public readonly struct AetheriaRuntimeMainMenuCommand",
        "public static class AetheriaRuntimeMainMenuSurfaceCommands",
        "public static bool TryRead("
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !mainMenuSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared main-menu surface builder no longer owns the root shell contract: " +
            string.Join(", ", missingBuilderSymbols));
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
    var mainMenuSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeMainMenuSurfaceBuilder.cs");
    if (!File.Exists(mainMenuPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu input delegation; MainMenu.cs is missing.");
    }
    if (!File.Exists(mainMenuSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify main-menu input delegation; AetheriaRuntimeMainMenuSurfaceBuilder.cs is missing.");
    }

    var source = File.ReadAllText(mainMenuPath);
    var mainMenuSurfaceBuilder = File.ReadAllText(mainMenuSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "AetheriaRuntimeMainMenuCommandKind.OpenRuntimeInputScreen",
        "CanOpenRuntimeInputScreen()",
        "TryOpenRuntimeInputScreen()",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(",
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

    if (!mainMenuSurfaceBuilder.Contains("AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen", StringComparison.Ordinal) ||
        !mainMenuSurfaceBuilder.Contains("CanOpenRuntimeInputScreen", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared main-menu input surface no longer exposes the runtime input-screen handoff.");
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
        "AetheriaRuntimeInputSettingsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeInputSettingsCommandKind.BeginCapture",
        "AetheriaRuntimeInputSettingsCommandKind.ToggleActionBar",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_surfaceDocument)",
        "UIDocument",
        "ActionGameManager.RequestRuntimeInputBindingOverride",
        "ActionGameManager.RequestRuntimeActionBarInput",
        "action.ApplyBindingOverride",
        "new InputAction(\"Aetheria Input Capture\")",
        "AetheriaRuntimeInputSettingsSurfaceBuilder.IsSupportedCapturePath(",
        "AetheriaRuntimeInputSettingsSurfaceBuilder.Project(",
        "new AetheriaRuntimeObservedInputBinding(",
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
        "DisplayLayout(_inputLayout)",
        "EnsureSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "AetheriaRuntimeStateCommitLog.QueuePlayerSettings",
        "ActionGameManager.CommitRuntimeInputBindingOverride",
        "ActionGameManager.CommitRuntimeActionBarInput",
        "BeginCapture(request.Payload)",
        "ToggleActionBarInput(request.Payload)",
        "private void BeginCapture(IReadOnlyDictionary<string, string> payload)",
        "private void ToggleActionBarInput(IReadOnlyDictionary<string, string> payload)",
        "request.Payload",
        "private static readonly string[] DefaultActionBarCandidatePaths",
        "private static bool IsSupportedCapturePath(string path)",
        "new AetheriaRuntimeInputSettingsSurfaceState(",
        "new AetheriaRuntimeInputBindingSurfaceState(",
        "new AetheriaRuntimeActionBarInputSurfaceState(",
        "AetheriaRuntimeInputSettingsSurfaceBuilder.ProjectActionBarInputs(",
        "AetheriaRuntimeInputSettingsSurfaceBuilder.ProjectActionBarCandidates(",
        "new SortedDictionary<string, string>(StringComparer.Ordinal)",
        "private IReadOnlyList<AetheriaRuntimeInputPathSurfaceLabel> ProjectActionBarCandidates("
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
        !commands.Contains("SetBindingOverride", StringComparison.Ordinal) ||
        !commands.Contains("SetActionBarEnabled", StringComparison.Ordinal) ||
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
        "public sealed class AetheriaRuntimeInputPathSurfaceLabel",
        "public sealed class AetheriaRuntimeObservedInputBinding",
        "public static readonly IReadOnlyList<string> DefaultActionBarCandidatePaths",
        "public static readonly IReadOnlyList<AetheriaRuntimeInputPathSurfaceLabel> DefaultActionBarCandidateInputPaths",
        "public static bool IsSupportedCapturePath(string path)",
        "public static AetheriaRuntimeInputSettingsSurfaceState Project(",
        "public static IReadOnlyList<AetheriaRuntimeInputBindingSurfaceState> ProjectBindingInputs(",
        "public static IReadOnlyList<AetheriaRuntimeInputPathSurfaceLabel> ProjectActionBarCandidates(",
        "public static IReadOnlyList<AetheriaRuntimeActionBarInputSurfaceState> ProjectActionBarInputs(",
        "new SortedDictionary<string, string>(StringComparer.Ordinal)",
        "public enum AetheriaRuntimeInputSettingsCommandKind",
        "public readonly struct AetheriaRuntimeInputSettingsSurfaceCommand",
        "public static class AetheriaRuntimeInputSettingsSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeInputSettingsCommands.BeginCapture",
        "AetheriaRuntimeInputSettingsCommands.ToggleActionBar",
        "ReadString(request, \"actionName\")",
        "ReadInt(request, \"bindingIndex\", -1)",
        "ReadString(request, \"inputPath\")",
        "ReadBool(request, \"enabled\", false)",
        "\"Low-level InputSystem edits flow through this Eve surface as typed input-setting requests.\""
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

    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify runtime input-screen authority; ActionGameManager.cs is missing.");

    var requiredActionGameManagerSymbols = new[]
    {
        "SendRuntimeInputSettingsCommand(",
        "AetheriaRuntimeEveCommands.TrySendInputSettingsCommand",
        "AetheriaRuntimeEveCommandKind.SetBindingOverride",
        "AetheriaRuntimeEveCommandKind.SetActionBarEnabled"
    };

    var missingActionGameManagerSymbols = requiredActionGameManagerSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionGameManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime input writes no longer send explicit Eve input-setting commands: " +
            string.Join(", ", missingActionGameManagerSymbols));
    }

    var forbiddenActionGameManagerSymbols = new[]
    {
        "public static void CommitRuntimeInputBindingOverride",
        "public static void CommitRuntimeActionBarInput",
        "TrySendRuntimeInputSettingsCommand(",
        "private static bool TrySendRuntimeInputSettingsCommand(",
        "RuntimePlayerSettings.InputSettings.SetBindingOverride(actionName, bindingIndex, inputSystemPath)",
        "RuntimePlayerSettings.InputSettings.SetActionBarInputEnabled(inputSystemPath, enabled)"
    };

    var survivingActionGameManagerSymbols = forbiddenActionGameManagerSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (survivingActionGameManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime input settings are still using Unity-local mutation authority instead of typed Eve requests: " +
            string.Join(", ", survivingActionGameManagerSymbols));
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
    var zoneDetailsSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeZoneDetailsSurfaceBuilder.cs");
    if (!File.Exists(sectorRendererPath))
    {
        throw new InvalidOperationException("Cannot verify sector-map zone details shell; SectorRenderer.cs is missing.");
    }
    if (!File.Exists(zoneDetailsSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify sector-map zone details shell; shared runtime zone details surface builder is missing.");
    }

    var source = File.ReadAllText(sectorRendererPath);
    var zoneDetailsSurfaceBuilder = File.ReadAllText(zoneDetailsSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderZoneDetailsSurface(",
        "HandleZoneDetailsSurfaceCommand(",
        "HideZoneDetailsSurface(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_zoneDetailsSurfaceDocument)",
        "AetheriaRuntimeZoneDetailsSurfaceBuilder.Build(ProjectZoneDetailsSurfaceState(",
        "AetheriaRuntimeZoneDetailsSurfaceBuilder.ProjectDaemonZone(",
        "AetheriaRuntimeZoneDetailsSurfaceBuilder.Project(",
        "ProjectZoneDetailsSurfaceState(",
        "GameManager.TryGetObservedZoneSnapshot(zone?.ZoneIndex ?? -1, out var daemonZone)",
        "AetheriaRuntimeZoneDetailsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeZoneDetailsCommandKind.Close"
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
        "BuildZoneDetailsSurfaceDefinition(",
        "ZoneDetailsSurfaceId",
        "CloseZoneDetailsCommand",
        "private const string ZoneDetailsSurfaceType",
        "ResolveZoneDetailsSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
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
        "Properties.AddProperty(\"Ships\"",
        "GameManager.Settings.ZoneSettings.ZoneRadius.Evaluate(",
        "GameManager.Settings.ZoneSettings.ZoneMass.Evaluate(",
        "ProjectZoneBodies(",
        "ProjectZoneEntities(",
        "new AetheriaRuntimeZoneDetailsBodyProjection(",
        "new AetheriaRuntimeZoneDetailsEntityProjection(",
        "runtimeZone.PlanetInstances",
        "runtimeZone.AsteroidBelts",
        "runtimeZone.Entities",
        "new AetheriaRuntimeZoneDetailsSurfaceState(",
        "private static bool IsBodyKind(",
        "private static bool IsPlanetBody(",
        "private static bool HasHullType(",
        "string.Equals(request.Command, AetheriaRuntimeZoneDetailsSurfaceBuilder.Close"
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

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeZoneDetailsSurfaceBuilder",
        "public sealed class AetheriaRuntimeZoneDetailsDaemonProjection",
        "public sealed class AetheriaRuntimeZoneDetailsBodyProjection",
        "public sealed class AetheriaRuntimeZoneDetailsEntityProjection",
        "public static AetheriaRuntimeZoneDetailsDaemonProjection ProjectDaemonZone(",
        "Math.Max(0, zone.GravityTerrainRadius)",
        ".Sum(body => body.Mass)",
        "public static AetheriaRuntimeZoneDetailsSurfaceState Project(",
        "private static bool IsBodyKind(",
        "private static bool IsPlanetBody(",
        "private static bool HasHullType(",
        "public const string SurfaceId = \"aetheria.sector_map.zone_details\"",
        "public const string Close = \"aetheria.sector_map.zone_details.close\"",
        "public enum AetheriaRuntimeZoneDetailsCommandKind",
        "public readonly struct AetheriaRuntimeZoneDetailsCommand",
        "public static class AetheriaRuntimeZoneDetailsSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeZoneDetailsSurfaceState",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "providerKind: \"sector.map\"",
        "Factions Present",
        "Has not been visited.",
        "Asteroid Belts"
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !zoneDetailsSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime zone details surface builder no longer owns the sector-map zone shell contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireRuntimeMenuTabsUseEveSurface(string root)
{
    var menuPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuPanel.cs");
    var localMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "LocalMenu.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var menuTabsSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.cs");
    if (!File.Exists(menuPanelPath))
    {
        throw new InvalidOperationException("Cannot verify runtime menu tab shell; MenuPanel.cs is missing.");
    }
    if (!File.Exists(localMenuPath))
    {
        throw new InvalidOperationException("Cannot verify runtime local-menu shell; LocalMenu.cs is missing.");
    }
    if (!File.Exists(actionGameManagerPath))
    {
        throw new InvalidOperationException("Cannot verify runtime menu tab shell; ActionGameManager.cs is missing.");
    }
    if (!File.Exists(menuTabsSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify runtime menu tab shell; AetheriaRuntimeMenuTabsSurfaceBuilder.cs is missing.");
    }

    var legacyTabButtonPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuTabButton.cs");
    if (File.Exists(legacyTabButtonPath))
    {
        throw new InvalidOperationException("MenuPanel tab metadata still has a surviving MenuTabButton component shell.");
    }

    var source = File.ReadAllText(menuPanelPath);
    var localMenu = File.ReadAllText(localMenuPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var surfaceHostPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveUnitySurfaceHost.cs");
    if (!File.Exists(surfaceHostPath))
    {
        throw new InvalidOperationException("Cannot verify runtime menu tab shell; AetheriaEveUnitySurfaceHost.cs is missing.");
    }

    var menuTabsSurfaceBuilder = File.ReadAllText(menuTabsSurfaceBuilderPath);
    var surfaceHost = File.ReadAllText(surfaceHostPath);
    var requiredSymbols = new[]
    {
        "MenuTabBinding",
        "TabBindings = Array.Empty<MenuTabBinding>();",
        "RenderTabSurface(",
        "HandleTabSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_tabSurfaceDocument)",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.Build(ProjectTabSurface())",
        "ProjectTabSurface(",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.Project(",
        "new AetheriaRuntimeMenuTabProjectionOption(",
        "ResolveVisibleTabs(",
        "GameManager.IsObservedDocked",
        "GameManager.TryGetObservedDockedLocalStory(out _)",
        "GetTabLabel(",
        "ToRuntimeTabKey(",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.NormalizeTabKey(tab.ToString())",
        "AetheriaRuntimeMenuTabsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeMenuTabCommandKind.SelectTab",
        "string.Equals(command.TabKey, ToRuntimeTabKey(tab), StringComparison.Ordinal)",
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

    var requiredHostSymbols = new[]
    {
        "public static class AetheriaEveUnitySurfaceHost",
        "public static UIDocument Render(",
        "new EveUiToolkitSurfaceLowerer()",
        "shell.Add(lowerer.Lower(surface, commandHandler))"
    };

    var missingHostSymbols = requiredHostSymbols
        .Where(symbol => !surfaceHost.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaEveUnitySurfaceHost no longer owns the shared Unity Eve lowering path: " +
            string.Join(", ", missingHostSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "MenuTabButton",
        "tabButton.Button.onClick.AddListener(",
        "tabButton.gameObject.SetActive(!tabButton.RequireDock || GameManager.DockedEntity != null);",
        "_tabs[MenuTab.Local].gameObject.SetActive(",
        "BuildTabSurfaceDefinition(",
        "private static EveSurfaceComponent Button(",
        "private static EveSurfaceComponent Text(",
        "private static EveSurfaceComponent Node(",
        "ResolveTabSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "string.Equals(request.Command, AetheriaRuntimeMenuTabsSurfaceBuilder.CommandFor(",
        "ProjectTabSurfaceState(",
        "new AetheriaRuntimeMenuTabSurfaceEntry(",
        "private static string TabKey("
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

    var requiredProjectionBuilderSymbols = new[]
    {
        "public sealed class AetheriaRuntimeMenuTabProjectionOption",
        "public static string NormalizeTabKey(string tabKey)",
        "public static AetheriaRuntimeMenuTabsSurfaceState Project(",
        "public int Order { get; }",
        ".OrderBy(tab => tab.Order)",
        "new AetheriaRuntimeMenuTabSurfaceEntry(",
        "string.Equals(key, normalizedCurrent, StringComparison.Ordinal)"
    };
    var missingProjectionBuilderSymbols = requiredProjectionBuilderSymbols
        .Where(symbol => !menuTabsSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingProjectionBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime menu tab surface builder no longer owns tab projection semantics: " +
            string.Join(", ", missingProjectionBuilderSymbols));
    }

    var requiredDockedStoryObserverSymbols = new[]
    {
        "public bool IsObservedDocked => TryGetObservedDockingBay(out _);",
        "public bool TryGetObservedDockingBay(out EquippedDockingBay dockingBay)",
        "TryResolveDaemonDockingBay(CurrentEntity, out var dockParent, out var resolvedDockingBay)",
        "public bool TryGetObservedDockedLocalStory(out LocationStory story)",
        "TryGetObservedDockedLocalStory(out _currentLocation)"
    };
    var dockedStoryObserverCorpus = actionGameManager + "\n" + localMenu;
    var missingDockedStoryObserverSymbols = requiredDockedStoryObserverSymbols
        .Where(symbol => !dockedStoryObserverCorpus.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDockedStoryObserverSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime menu/local story visibility no longer flows through daemon-observed dock state: " +
            string.Join(", ", missingDockedStoryObserverSymbols));
    }

    if (source.Contains("GameManager.DockedEntity != null", StringComparison.Ordinal) ||
        source.Contains("GameManager.DockedEntity as OrbitalEntity", StringComparison.Ordinal) ||
        localMenu.Contains("ActionGameManager.Instance.DockedEntity", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Runtime menu/local UI must not inspect DockedEntity directly for dock/story visibility; ask the gameplay observer.");
    }

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeMenuTabsSurfaceBuilder",
        "public const string SurfaceId = \"aetheria.runtime_menu.tabs\"",
        "CommandFor(string tabKey)",
        "public enum AetheriaRuntimeMenuTabCommandKind",
        "public readonly struct AetheriaRuntimeMenuTabCommand",
        "public static class AetheriaRuntimeMenuTabsSurfaceCommands",
        "public static bool TryRead(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "AetheriaRuntimeMenuTabsSurfaceState",
        "AetheriaRuntimeMenuTabSurfaceEntry"
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !menuTabsSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime menu tab surface builder no longer owns the tab shell contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireInventoryShipSettingsUseEveSurface(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var shipSettingsSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeShipSettingsSurfaceBuilder.cs");
    if (!File.Exists(inventoryMenuPath))
    {
        throw new InvalidOperationException("Cannot verify inventory ship-settings shell; InventoryMenu.cs is missing.");
    }
    if (!File.Exists(shipSettingsSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify inventory ship-settings shell; shared runtime ship settings surface builder is missing.");
    }

    var source = File.ReadAllText(inventoryMenuPath);
    var shipSettingsSurfaceBuilder = File.ReadAllText(shipSettingsSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderCurrentShipSettingsSurface(",
        "HandleCurrentShipSettingsSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_shipSettingsSurfaceDocument)",
        "AetheriaRuntimeShipSettingsSurfaceBuilder.Build(ProjectCurrentShipSettingsSurface(",
        "AetheriaRuntimeShipSettingsSurfaceBuilder.Project(",
        "AetheriaRuntimeShipSettingsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeShipSettingsCommandKind.DecrementShutdownThreshold",
        "AetheriaRuntimeShipSettingsCommandKind.IncrementShutdownThreshold",
        "AetheriaRuntimeShipSettingsCommandKind.ResetShutdownThreshold",
        "AetheriaRuntimeShipSettingsCommandKind.Close",
        "GameManager.TryGetObservedCurrentEntity(out var currentEntity)",
        "GameManager.TryGetObservedCurrentEntity(out var entity)"
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
        "BuildCurrentShipSettingsSurfaceDefinition(",
        "ShipSettingsSurfaceId",
        "DecrementShutdownThresholdCommand",
        "IncrementShutdownThresholdCommand",
        "ResetShutdownThresholdCommand",
        "CloseShipSettingsCommand",
        "private const string ShipSettingsSurfaceType",
        "ResolveShipSettingsSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "PropertiesPanel.AddField(\"Shutdown Threshold\"",
        "() => GameManager.CurrentEntity.Settings.ShutdownPerformance",
        "f => GameManager.RequestEntityShutdownPerformance(GameManager.CurrentEntity, f)",
        "switch (request.Command)",
        "var entity = GameManager.CurrentEntity",
        "GameManager.CurrentEntity == null",
        "RenderCurrentShipSettingsSurface(GameManager.CurrentEntity)",
        "new AetheriaRuntimeShipSettingsSurfaceState("
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

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeShipSettingsSurfaceBuilder",
        "public const string SurfaceId = \"aetheria.inventory.current_ship_settings\"",
        "public const string DecrementShutdownThreshold",
        "public const string IncrementShutdownThreshold",
        "public const string ResetShutdownThreshold",
        "public const string Close",
        "public enum AetheriaRuntimeShipSettingsCommandKind",
        "public readonly struct AetheriaRuntimeShipSettingsCommand",
        "public static class AetheriaRuntimeShipSettingsSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeShipSettingsSurfaceState",
        "public static AetheriaRuntimeShipSettingsSurfaceState Project(",
        "public static AetheriaRuntimeSurfaceDocument Build("
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !shipSettingsSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime ship settings surface builder no longer owns the ship-settings shell contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireInventoryCargoItemDetailsUseEveSurface(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var surfaceDocumentPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.cs");
    var daemonItemStatQueriesPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeDaemonItemStatQueries.cs");
    var eveUnitySurfaceHostPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.eve-runtime",
        "Runtime",
        "AetheriaEveUnitySurfaceHost.cs");
    var runtimeEveSurfaceAdapterPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeEveSurfaceAdapter.cs");
    var cargoItemSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.cs");
    var equippedItemSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.cs");
    var unityProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var requiredFiles = new[]
    {
        inventoryMenuPath,
        actionGameManagerPath,
        surfaceDocumentPath,
        daemonItemStatQueriesPath,
        eveUnitySurfaceHostPath,
        runtimeEveSurfaceAdapterPath,
        cargoItemSurfaceBuilderPath,
        equippedItemSurfaceBuilderPath,
        unityProjectPath
    };
    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify inventory cargo-item shell; required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var source = File.ReadAllText(inventoryMenuPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var surfaceDocument = File.ReadAllText(surfaceDocumentPath);
    var daemonItemStatQueries = File.ReadAllText(daemonItemStatQueriesPath);
    var unityProject = File.ReadAllText(unityProjectPath);
    var eveUnitySurfaceHost = File.ReadAllText(eveUnitySurfaceHostPath);
    var runtimeEveSurfaceAdapter = File.ReadAllText(runtimeEveSurfaceAdapterPath);
    var cargoItemSurfaceBuilder = File.ReadAllText(cargoItemSurfaceBuilderPath);
    var equippedItemSurfaceBuilder = File.ReadAllText(equippedItemSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderCargoItemDetailsSurface(",
        "HandleCargoItemDetailsSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_cargoItemDetailsSurfaceDocument)",
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Build(ProjectCargoItemDetailsSurface(",
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Project(",
        "ProjectCargoItemObservation(",
        "AetheriaRuntimeCargoItemDetailsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeCargoItemDetailsCommandKind.Close"
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

    var runtimeStateReaderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeStateReader.cs");
    var runtimeStateReader = File.Exists(runtimeStateReaderPath)
        ? File.ReadAllText(runtimeStateReaderPath)
        : throw new InvalidOperationException("Cannot verify item stat state-ref authority; AetheriaRuntimeStateReader.cs is missing.");

    if (!runtimeStateReader.Contains("TryResolveDaemonItemStatRef(", StringComparison.Ordinal) ||
        !runtimeStateReader.Contains("AetheriaRuntimeDaemonItemStatQueries.TryReadItemStatRef(", StringComparison.Ordinal) ||
        !runtimeStateReader.Contains("AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(", StringComparison.Ordinal) ||
        !runtimeStateReader.Contains("item.Temperature", StringComparison.Ordinal) ||
        !runtimeStateReader.Contains("FindDaemonItem(", StringComparison.Ordinal) ||
        !cargoItemSurfaceBuilder.Contains("AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(", StringComparison.Ordinal) ||
        source.Contains("ResolveInventorySurfaceStateRef", StringComparison.Ordinal) ||
        source.Contains("TryResolveInventorySurfaceStateRef", StringComparison.Ordinal) ||
        source.Contains("private static bool TryReadItemStatRef(", StringComparison.Ordinal) ||
        source.Contains("DecodeRefToken", StringComparison.Ordinal) ||
        source.Contains("GameManager.ItemManager.GetTier", StringComparison.Ordinal) ||
        source.Contains("GameManager.ItemManager.Evaluate", StringComparison.Ordinal) ||
        actionGameManager.Contains("ObservedItemStat(", StringComparison.Ordinal) ||
        actionGameManager.Contains("ItemManager.Evaluate(stat", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryMenu must project current item stats through shared daemon item-stat queries instead of direct Unity ItemManager stat authority.");
    }

    if (!surfaceDocument.Contains("public static class AetheriaRuntimeSurfaceStateRefs", StringComparison.Ordinal) ||
        !surfaceDocument.Contains("public const string Source = \"stateRef\"", StringComparison.Ordinal) ||
        !surfaceDocument.Contains("public const string Value = \"valueRef\"", StringComparison.Ordinal) ||
        !daemonItemStatQueries.Contains("public const string StateRefPrefix = \"aetheria.state/items\"", StringComparison.Ordinal) ||
        !daemonItemStatQueries.Contains("public static string ItemStatRef(", StringComparison.Ordinal) ||
        !daemonItemStatQueries.Contains("public static bool TryReadItemStatRef(", StringComparison.Ordinal) ||
        !daemonItemStatQueries.Contains("public string ValueRef =>", StringComparison.Ordinal) ||
        !unityProject.Contains("AetheriaRuntimeDaemonItemStatQueries.cs", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("Func<string, string> stateRefResolver", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("ContainsStateRefs(surface)", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("CreateDefaultStateRefResolver()", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceStateRefResolver(stateBoot.StateFilePath)", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("public static EveSurfaceDocument ResolveStateRefs(", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("ResolvePropRefs(props, stateRefResolver)", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("ResolvePropRef(props, AetheriaRuntimeSurfaceStateRefs.Source, \"value\", stateRefResolver)", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("IsStatePointerProp(prop.Key)", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("ResolvePointerValueKey(refProp.Key)", StringComparison.Ordinal) ||
        !cargoItemSurfaceBuilder.Contains("public string ValueRef { get; }", StringComparison.Ordinal) ||
        !cargoItemSurfaceBuilder.Contains("props.Add(AetheriaRuntimeSurfaceStateRefs.ValueRef(valueRef))", StringComparison.Ordinal) ||
        !equippedItemSurfaceBuilder.Contains("public string ValueRef { get; }", StringComparison.Ordinal) ||
        !equippedItemSurfaceBuilder.Contains("props.Add(AetheriaRuntimeSurfaceStateRefs.ValueRef(valueRef))", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve/CultUI item stat surfaces must expose typed daemon state refs that the UI runtime can resolve.");
    }

    var forbiddenSymbols = new[]
    {
        "BuildCargoItemDetailsSurfaceDefinition(",
        "BuildCargoItemBehaviorCards(",
        "BuildCargoItemBehaviorMetric(",
        "CargoItemDetailsSurfaceId",
        "CloseCargoItemDetailsCommand",
        "ResolveCargoItemDetailsSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "string.Equals(request.Command, AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Close",
        "new AetheriaRuntimeCargoItemDetailsSurfaceState(",
        "ProjectCargoItemDetailsSurfaceState(",
        "ProjectCargoItemBehaviorSections(",
        "ProjectCargoItemBehaviorMetric("
    };
    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still owns cargo-item inspection surface construction instead of projecting shared runtime state: " +
            string.Join(", ", hits));
    }

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeCargoItemDetailsSurfaceBuilder",
        "public const string SurfaceId = \"aetheria.inventory.cargo_item_details\"",
        "public const string Close = \"aetheria.inventory.cargo_item_details.close\"",
        "public enum AetheriaRuntimeCargoItemDetailsCommandKind",
        "public readonly struct AetheriaRuntimeCargoItemDetailsCommand",
        "public static class AetheriaRuntimeCargoItemDetailsSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeCargoItemDetailsSurfaceState",
        "AetheriaRuntimeCargoItemObservation",
        "AetheriaRuntimeCargoItemSection",
        "AetheriaRuntimeCargoItemMetric",
        "public static AetheriaRuntimeCargoItemDetailsSurfaceState Project(",
        "ProjectBehaviorSections(",
        "ProjectBehaviorMetric(",
        "public static AetheriaRuntimeSurfaceDocument Build("
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !cargoItemSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared cargo-item detail surface builder no longer owns the cargo inspection contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireInventoryEquippedItemDetailsUseEveSurface(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var equippedItemSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.cs");
    var stateProjectPath = Path.Combine(root, "Aetheria.State", "Aetheria.State.csproj");
    var unityProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var requiredFiles = new[]
    {
        inventoryMenuPath,
        actionGameManagerPath,
        equippedItemSurfaceBuilderPath,
        stateProjectPath,
        unityProjectPath
    };
    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify inventory equipped-item shell; required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var source = File.ReadAllText(inventoryMenuPath);
    var requiredSymbols = new[]
    {
        "RenderEquippedItemDetailsSurface(",
        "HandleEquippedItemDetailsSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_equippedItemDetailsSurfaceDocument)",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Build(ProjectEquippedItemDetailsSurface(",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Project(",
        "ProjectEquippedItemObservation(",
        "ProjectEquippedItemTemperatureControls(",
        "ProjectEquippedItemWeaponGroupControls(",
        "ProjectEquippedItemActionBarSlots(",
        "AetheriaRuntimeEquippedItemDetailsSurfaceCommands.TryRead(request, out var command)",
        "switch (command.Kind)",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.Close",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleOverrideShutdown",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleWeaponGroup",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.BindWeaponGroup",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.ClearActionBarBinding"
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

    var forbiddenSymbols = new[]
    {
        "EquippedItemDetailsSurfaceId",
        "BuildEquippedItemDetailsSurfaceDefinition(",
        "BuildEquippedItemControlCard(",
        "BuildEquippedItemWeaponGroupCard(",
        "BuildEquippedItemActionBarCards(",
        "BuildItemBehaviorCards(",
        "BuildItemBehaviorMetric(",
        "CloseEquippedItemDetailsCommand",
        "ToggleEquippedItemOverrideShutdownCommand",
        "SetEquippedItemTargetTemperatureCommand",
        "ToggleEquippedItemWeaponGroupCommand",
        "BindEquippedItemWeaponGroupCommand",
        "ClearEquippedItemActionBarBindingCommand",
        "ResolveEquippedItemDetailsSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "new AetheriaRuntimeEquippedItemDetailsSurfaceState(",
        "ProjectEquippedItemDetailsSurfaceState(",
        "ProjectEquippedItemBehaviorSections(",
        "ProjectEquippedItemBehaviorMetric(",
        "new AetheriaRuntimeEquippedItemSection(",
        "new AetheriaRuntimeEquippedItemMetric(",
        "private static EveSurfaceComponent Card(",
        "private static EveSurfaceComponent Metric(",
        "private static EveSurfaceComponent Text(",
        "private static EveSurfaceComponent Button(",
        "private static EveSurfaceComponent CommandButton(",
        "private static EveSurfaceComponent TextField(",
        "private static EveSurfaceComponent ButtonRow(",
        "private static EveSurfaceComponent Node(",
        "TryReadPayloadInt(",
        "TryReadPayloadFloat(",
        "request.Payload"
    };
    var forbiddenHits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still owns the equipped-item surface contract locally: " +
            string.Join(", ", forbiddenHits));
    }

    if (!source.Contains("RenderEquippedItemDetailsSurface(item);", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryMenu equipped-item click path no longer routes inspection through the Eve surface.");
    }

    var legacyShellSymbols = new[]
    {
        "public PropertiesPanel PropertiesPanel;",
        "PropertiesPanel.GameManager = GameManager;",
        "PropertiesPanel.gameObject.SetActive(true);",
        "PropertiesPanel.Inspect(item);"
    };

    var hits = legacyShellSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still owns equipped-item inspection through the old PropertiesPanel shell: " +
            string.Join(", ", hits));
    }

    var equippedItemSurfaceBuilder = File.ReadAllText(equippedItemSurfaceBuilderPath);
    var stateProject = File.ReadAllText(stateProjectPath);
    var unityProject = File.ReadAllText(unityProjectPath);
    var requiredBuilderSymbols = new[]
    {
        "public sealed class AetheriaRuntimeEquippedItemDetailsSurfaceState",
        "public sealed class AetheriaRuntimeEquippedItemObservation",
        "public sealed class AetheriaRuntimeEquippedItemSection",
        "public sealed class AetheriaRuntimeEquippedItemMetric",
        "public sealed class AetheriaRuntimeEquippedItemControl",
        "public sealed class AetheriaRuntimeEquippedItemTemperatureControl",
        "public sealed class AetheriaRuntimeEquippedItemActionBarSlot",
        "public enum AetheriaRuntimeEquippedItemDetailsCommandKind",
        "public readonly struct AetheriaRuntimeEquippedItemDetailsCommand",
        "public static class AetheriaRuntimeEquippedItemDetailsSurfaceCommands",
        "public static bool TryRead(",
        "public const string SurfaceId = \"aetheria.inventory.equipped_item_details\"",
        "public const string Close = \"aetheria.inventory.equipped_item_details.close\"",
        "public const string ToggleOverrideShutdown = \"aetheria.inventory.equipped_item_details.override_shutdown.toggle\"",
        "public const string SetTargetTemperature = \"aetheria.inventory.equipped_item_details.target_temperature.set\"",
        "public const string ToggleWeaponGroup = \"aetheria.inventory.equipped_item_details.weapon_group.toggle\"",
        "public const string BindWeaponGroup = \"aetheria.inventory.equipped_item_details.weapon_group.bind\"",
        "public const string ClearActionBarBinding = \"aetheria.inventory.equipped_item_details.action_bar.clear\"",
        "public static AetheriaRuntimeEquippedItemDetailsSurfaceState Project(",
        "ProjectBehaviorSections(",
        "ProjectBehaviorMetric(",
        "AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "new AetheriaRuntimeSurfaceCommandTemplate(Close",
        "new AetheriaRuntimeSurfaceCommandTemplate(ToggleOverrideShutdown",
        "new AetheriaRuntimeSurfaceCommandTemplate(SetTargetTemperature",
        "new AetheriaRuntimeSurfaceCommandTemplate(ToggleWeaponGroup",
        "new AetheriaRuntimeSurfaceCommandTemplate(BindWeaponGroup",
        "new AetheriaRuntimeSurfaceCommandTemplate(ClearActionBarBinding",
        "ReadInt(request, \"behaviorIndex\", -1)",
        "ReadFloat(request, \"value\", 0f)",
        "ReadInt(request, \"group\", -1)",
        "ReadInt(request, \"slot\", -1)",
        "\"control.text\"",
        "\"inventory.menu\""
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !equippedItemSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared equipped-item detail surface builder no longer owns the equipped-item inspection contract: " +
            string.Join(", ", missingBuilderSymbols));
    }

    var stateProjectIncludesRuntimePackage = stateProject.Contains(@"Runtime\*.cs", StringComparison.Ordinal);
    if ((!stateProjectIncludesRuntimePackage &&
            !stateProject.Contains("AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.cs", StringComparison.Ordinal)) ||
        !unityProject.Contains("AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared equipped-item surface builder is not included in both runtime project surfaces.");
    }

    var actionGameManagerSource = File.ReadAllText(actionGameManagerPath);
    var requiredActionBarSymbols = new[]
    {
        "GetActionBarSlotCount(",
        "GetActionBarSlotLabel(",
        "GetActionBarBindingLabel(",
        "RequestWeaponGroupActionBarBinding(",
        "RequestClearActionBarBinding(",
        "TryRequestDaemonActionBarBinding(",
        "TryRequestDaemonActionBarBindingClear("
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
    var tradeCargoSelectorSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade cargo-selector shell; TradeMenu.cs is missing.");
    }
    if (!File.Exists(tradeCargoSelectorSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify trade cargo-selector shell; shared runtime trade cargo selector surface builder is missing.");
    }

    var source = File.ReadAllText(tradeMenuPath);
    var tradeCargoSelectorSurfaceBuilder = File.ReadAllText(tradeCargoSelectorSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderCargoSelectorSurface(",
        "_cargoSelectorSurfaceProjection = ProjectTradeCargoSelectorSurface();",
        "HandleCargoSelectorSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_cargoSelectorSurfaceDocument)",
        "AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Build(_cargoSelectorSurfaceProjection.State)",
        "ProjectTradeCargoSelectorSurface(",
        "AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Project(",
        "_cargoSelectorSurfaceProjection?.TryResolve(command.Command, out var selection) == true",
        "ApplyCargoSelection(",
        "new AetheriaRuntimeTradeCargoProjectionOption(",
        "AetheriaRuntimeTradeCargoTargetKind.DockingBay",
        "AetheriaRuntimeTradeCargoTargetKind.ShipBay",
        "AetheriaRuntimeTradeCargoSelectorSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeTradeCargoSelectorCommandKind.Close",
        "AetheriaRuntimeTradeCargoSelectorCommandKind.Select",
        "CountAvailablePlayerShips(",
        "GameManager.TryGetObservedDockingBay(out var dockingBay)",
        "GameManager.ObservedAvailableEntities()"
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
        "private const string CargoSelectorSurfaceType",
        "private const string CargoSelectorSurfaceId",
        "CloseCargoSelectorCommand",
        "BuildCargoSelectorSurfaceDefinition(",
        "ResolveCargoSelectorSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "ContextMenu.AddOption(\"Docking Bay\"",
        "ContextMenu.AddOption($\"{ship.Name} Bay {bay.index+1}\"",
        "private static EveSurfaceComponent",
        "new EveSurfaceComponent(",
        "string.Equals(request.Command, AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Close",
        "_cargoSelectionCommands.TryGetValue(request.Command",
        "private readonly Dictionary<string, (EquippedCargoBay Cargo, string Label)> _cargoSelectionCommands",
        "BuildCargoSelectionCommands(",
        "ProjectTradeCargoSelectorSurfaceState(",
        "GameManager.DockedEntity.Children",
        "GameManager.CurrentEntity.Parent.Children",
        "GameManager.DockedEntity == null",
        "GameManager.DockingBay",
        "GameManager.AvailableEntities()"
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

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeTradeCargoSelectorSurfaceBuilder",
        "public const string SurfaceId = \"aetheria.trade.target_cargo_selector\"",
        "public const string Close = \"aetheria.trade.target_cargo_selector.close\"",
        "public const string DockingBay = \"aetheria.trade.target_cargo_selector.docking_bay\"",
        "public enum AetheriaRuntimeTradeCargoSelectorCommandKind",
        "public readonly struct AetheriaRuntimeTradeCargoSelectorCommand",
        "public static class AetheriaRuntimeTradeCargoSelectorSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeTradeCargoSelectorSurfaceState",
        "AetheriaRuntimeTradeCargoTargetOption",
        "AetheriaRuntimeTradeCargoProjectionOption",
        "AetheriaRuntimeTradeCargoTargetKind",
        "AetheriaRuntimeTradeCargoSelection",
        "AetheriaRuntimeTradeCargoSelectorSurfaceProjection",
        "public static string ShipBayCommand(",
        "public static AetheriaRuntimeTradeCargoSelectorSurfaceProjection Project(",
        "public bool TryResolve(string command, out AetheriaRuntimeTradeCargoSelection selection)",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "providerKind: \"trade.menu\"",
        "The observing client projects available cargo targets; the shared runtime surface owns the cargo selector contract."
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !tradeCargoSelectorSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime trade cargo selector surface builder no longer owns the trade cargo-selector shell contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireTradeFilterAndRowActionsUseEveSurface(string root)
{
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var tradeInteractionSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade filter and row-action shells; TradeMenu.cs is missing.");
    }
    if (!File.Exists(tradeInteractionSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify trade filter and row-action shells; shared runtime trade interaction surface builder is missing.");
    }

    var source = File.ReadAllText(tradeMenuPath);
    var tradeInteractionSurfaceBuilder = File.ReadAllText(tradeInteractionSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderFilterSurface(",
        "_filterSurfaceProjection = ProjectTradeFilterSurface();",
        "HandleFilterSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_filterSurfaceDocument)",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildFilter(_filterSurfaceProjection.State)",
        "ProjectTradeFilterSurface(",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.ProjectFilters(",
        "_filterSurfaceProjection?.TryResolve(command.Command, out var selection) == true",
        "ExecuteTradeFilterSelection(",
        "new AetheriaRuntimeTradeFilterOption(",
        "RenderRowActionSurface(",
        "HandleRowActionSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.Hide(_rowActionSurfaceDocument)",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.ProjectRowActions(",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildRowActions(_rowActionSurfaceProjection.State)",
        "_rowActionSurfaceProjection?.TryResolve(command.Command, out var selection) == true",
        "new AetheriaRuntimeTradeRowActionOption(index, action.Label)",
        "ShowBuyQuantityDialog(",
        "AetheriaRuntimeTradeInteractionSurfaceCommands.TryReadFilter(request, out var command)",
        "AetheriaRuntimeTradeInteractionSurfaceCommands.TryReadRowAction(request, out var command)",
        "AetheriaRuntimeTradeInteractionCommandKind.Close",
        "AetheriaRuntimeTradeInteractionCommandKind.Select"
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
        "private const string FilterSurfaceId",
        "private const string RowActionSurfaceId",
        "CloseFilterSurfaceCommand",
        "CloseRowActionSurfaceCommand",
        "BuildFilterSurfaceDefinition(",
        "BuildRowActionSurfaceDefinition(",
        "ResolveFilterSurfaceDocument(",
        "ResolveRowActionSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "public ContextMenu ContextMenu;",
        "ContextMenu.Clear();",
        "ContextMenu.AddDropdown(",
        "ContextMenu.AddOption(",
        "ContextMenu.Show();",
        "private static EveSurfaceComponent",
        "new EveSurfaceComponent(",
        "string.Equals(request.Command, AetheriaRuntimeTradeInteractionSurfaceBuilder.CloseFilter",
        "string.Equals(request.Command, AetheriaRuntimeTradeInteractionSurfaceBuilder.CloseRowAction",
        "_filterSurfaceCommands.TryGetValue(request.Command",
        "private readonly Dictionary<string, Action> _filterSurfaceCommands",
        "BuildFilterSurfaceCommands(",
        "ProjectTradeFilterSurfaceState(",
        "AddTradeFilterGroup(",
        "_rowActionSurfaceCommands.TryGetValue(request.Command",
        "private readonly Dictionary<string, Action> _rowActionSurfaceCommands",
        "BuildRowActionSurfaceCommands(",
        "ProjectTradeRowActionSurfaceState("
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

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeTradeInteractionSurfaceBuilder",
        "public const string FilterSurfaceId = \"aetheria.trade.filter_selector\"",
        "public const string CloseFilter = \"aetheria.trade.filter_selector.close\"",
        "public const string RowActionSurfaceId = \"aetheria.trade.row_actions\"",
        "public const string CloseRowAction = \"aetheria.trade.row_actions.close\"",
        "public enum AetheriaRuntimeTradeInteractionCommandKind",
        "public readonly struct AetheriaRuntimeTradeInteractionCommand",
        "public static class AetheriaRuntimeTradeInteractionSurfaceCommands",
        "public static bool TryReadFilter(",
        "public static bool TryReadRowAction(",
        "AetheriaRuntimeTradeFilterSurfaceState",
        "AetheriaRuntimeTradeFilterOption",
        "AetheriaRuntimeTradeFilterSelectionKind",
        "AetheriaRuntimeTradeFilterSelection",
        "AetheriaRuntimeTradeFilterSurfaceProjection",
        "AetheriaRuntimeTradeRowActionSurfaceState",
        "AetheriaRuntimeTradeRowActionOption",
        "AetheriaRuntimeTradeRowActionSelection",
        "AetheriaRuntimeTradeRowActionSurfaceProjection",
        "AetheriaRuntimeTradeSurfaceGroup",
        "AetheriaRuntimeTradeSurfaceOption",
        "public static string HardpointFilterCommand(",
        "public static string RowActionCommand(",
        "public static AetheriaRuntimeTradeFilterSurfaceProjection ProjectFilters(",
        "public bool TryResolve(string command, out AetheriaRuntimeTradeFilterSelection selection)",
        "public static AetheriaRuntimeTradeRowActionSurfaceProjection ProjectRowActions(",
        "public bool TryResolve(string command, out AetheriaRuntimeTradeRowActionSelection selection)",
        "public static AetheriaRuntimeSurfaceDocument BuildFilter(",
        "public static AetheriaRuntimeSurfaceDocument BuildRowActions(",
        "providerKind: \"trade.menu\""
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !tradeInteractionSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime trade interaction surface builder no longer owns the trade filter/row-action shell contracts: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireTradeItemDetailsUseEveSurface(string root)
{
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var tradeItemDetailsSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeTradeItemDetailsSurfaceBuilder.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade item-details shell; TradeMenu.cs is missing.");
    }
    if (!File.Exists(tradeItemDetailsSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify trade item-details shell; shared runtime trade item details surface builder is missing.");
    }

    var source = File.ReadAllText(tradeMenuPath);
    var tradeItemDetailsSurfaceBuilder = File.ReadAllText(tradeItemDetailsSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderTradeItemDetailsSurface(",
        "HandleTradeItemDetailsSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_tradeItemSurfaceDocument)",
        "AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Build(ProjectTradeItemDetailsSurface(",
        "AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Project(",
        "AetheriaRuntimeTradeItemDetailsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeTradeItemDetailsCommandKind.Close"
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
        "TradeItemSurfaceId",
        "CloseTradeItemDetailsCommand",
        "BuildTradeItemDetailsSurfaceDefinition(",
        "BuildTradeItemBehaviorCards(",
        "BuildTradeItemBehaviorMetric(",
        "ResolveTradeItemDetailsSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIDocument>",
        "public PropertiesPanel Properties;",
        "OnClick = () => Properties.Inspect(i.TypedItem)",
        "private static EveSurfaceComponent",
        "new EveSurfaceComponent(",
        "string.Equals(request.Command, AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Close",
        "new AetheriaRuntimeTradeItemDetailsSurfaceState(",
        "ProjectTradeItemDetailsSurfaceState(",
        "ProjectTradeItemBehaviorSections(",
        "ProjectTradeItemBehaviorMetric("
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

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeTradeItemDetailsSurfaceBuilder",
        "public const string SurfaceId = \"aetheria.trade.item_details\"",
        "public const string Close = \"aetheria.trade.item_details.close\"",
        "public enum AetheriaRuntimeTradeItemDetailsCommandKind",
        "public readonly struct AetheriaRuntimeTradeItemDetailsCommand",
        "public static class AetheriaRuntimeTradeItemDetailsSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeTradeItemDetailsSurfaceState",
        "AetheriaRuntimeTradeItemSection",
        "AetheriaRuntimeTradeItemMetric",
        "public static AetheriaRuntimeTradeItemDetailsSurfaceState Project(",
        "ProjectBehaviorSections(",
        "ProjectBehaviorMetric(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "providerKind: \"trade.menu\"",
        "The observing client supplies the selected market row; the shared runtime surface owns trade item inspection layout."
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !tradeItemDetailsSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime trade item details surface builder no longer owns the trade item-details shell contract: " +
            string.Join(", ", missingBuilderSymbols));
    }
}

static void RequireTradeItemValuesUseRuntimeQueries(string root)
{
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var tradeQueriesPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeDaemonTradeItemQueries.cs");
    if (!File.Exists(tradeMenuPath))
    {
        throw new InvalidOperationException("Cannot verify trade item value projection; TradeMenu.cs is missing.");
    }
    if (!File.Exists(tradeQueriesPath))
    {
        throw new InvalidOperationException("Cannot verify trade item value projection; shared runtime trade item queries are missing.");
    }

    var tradeMenu = File.ReadAllText(tradeMenuPath);
    var tradeQueries = File.ReadAllText(tradeQueriesPath);
    var requiredTradeMenuSymbols = new[]
    {
        "ProjectTradeItem(item)",
        "ProjectTradeItemCommit(",
        "GameManager.ObservedTradeValueSettings()",
        "AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(",
        "AetheriaRuntimeTradeItemProjection TradeProjection",
        "public int Price => TradeProjection.Price",
        "public string TierColorHex => TradeProjection.TierColorHex"
    };
    var missingTradeMenuSymbols = requiredTradeMenuSymbols
        .Where(symbol => !tradeMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTradeMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu no longer projects trade item price/tier values through shared daemon runtime queries: " +
            string.Join(", ", missingTradeMenuSymbols));
    }

    var forbiddenTradeMenuSymbols = new[]
    {
        "GameManager.ItemManager.GetTier",
        "GameManager.ItemManager.GameplaySettings.QualityPriceModifier",
        "_itemManager.GameplaySettings.QualityPriceModifier",
        "private readonly ItemManager _itemManager",
        "new TradeRow(item, FindTypedTradeItem(item), GameManager.ItemManager)"
    };
    var tradeMenuHits = forbiddenTradeMenuSymbols
        .Where(symbol => tradeMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (tradeMenuHits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still asks Unity ItemManager for trade item value projection: " +
            string.Join(", ", tradeMenuHits));
    }

    var requiredQuerySymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonTradeItemQueries",
        "public readonly struct AetheriaRuntimeTradeItemProjection",
        "public sealed class AetheriaRuntimeTradeValueSettings",
        "public readonly struct AetheriaRuntimeItemRarityTier",
        "public readonly struct AetheriaRuntimeExponentialLerp",
        "public static AetheriaRuntimeTradeItemProjection ProjectTradeItem(",
        "settings.QualityPriceModifier.Evaluate(quality.Value) * typedItem.Price",
        "SelectTier(settings.Tiers, quality.Value)",
        "AetheriaRuntimeDaemonItemStatQueries.ItemCommit("
    };
    var missingQuerySymbols = requiredQuerySymbols
        .Where(symbol => !tradeQueries.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingQuerySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime trade item queries no longer own price/tier projection: " +
            string.Join(", ", missingQuerySymbols));
    }
}

static void RequireItemTierProjectionUsesRuntimeQueries(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var zoneRendererPath = Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs");
    var tradeQueriesPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeDaemonTradeItemQueries.cs");

    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify item tier projection; ActionGameManager.cs is missing.");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify item tier projection; InventoryMenu.cs is missing.");
    var zoneRenderer = File.Exists(zoneRendererPath)
        ? File.ReadAllText(zoneRendererPath)
        : throw new InvalidOperationException("Cannot verify item tier projection; ZoneRenderer.cs is missing.");
    var tradeQueries = File.Exists(tradeQueriesPath)
        ? File.ReadAllText(tradeQueriesPath)
        : throw new InvalidOperationException("Cannot verify item tier projection; runtime trade item queries are missing.");

    if (actionGameManager.Contains("ObservedItemTier(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager still exposes item tier projection as a Unity ItemManager bridge.");
    }

    var forbiddenUnityTierSymbols = new[]
    {
        "GameManager.ObservedItemTier",
        "ItemManager.GetTier(",
        "GameManager.ItemManager.GetTier"
    };
    var hits = new[] { inventoryMenuPath, zoneRendererPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenUnityTierSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity UI/rendering still projects item tiers through ItemManager instead of shared runtime queries: " +
            string.Join("; ", hits));
    }

    if (!inventoryMenu.Contains("FormatItemTier(", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("tradeProjection.TierName", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("tradeProjection.Upgrades", StringComparison.Ordinal) ||
        !zoneRenderer.Contains("AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(", StringComparison.Ordinal) ||
        !zoneRenderer.Contains("tradeProjection.TierColorHex", StringComparison.Ordinal) ||
        !tradeQueries.Contains("public string TierName { get; }", StringComparison.Ordinal) ||
        !tradeQueries.Contains("public string TierColorHex { get; }", StringComparison.Ordinal) ||
        !tradeQueries.Contains("public int Upgrades { get; }", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Item tier labels and pickup colors must be projected through shared runtime trade item query results.");
    }
}

static void RequireInventoryDropdownUseEveSurface(string root)
{
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryDropdownSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeInventoryDropdownSurfaceBuilder.cs");
    if (!File.Exists(inventoryPanelPath))
    {
        throw new InvalidOperationException("Cannot verify inventory dropdown shell; InventoryPanel.cs is missing.");
    }
    if (!File.Exists(inventoryMenuPath))
    {
        throw new InvalidOperationException("Cannot verify inventory dropdown shell; InventoryMenu.cs is missing.");
    }
    if (!File.Exists(inventoryDropdownSurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify inventory dropdown shell; shared runtime inventory dropdown surface builder is missing.");
    }

    var source = File.ReadAllText(inventoryPanelPath);
    var inventoryMenu = File.ReadAllText(inventoryMenuPath);
    var inventoryDropdownSurfaceBuilder = File.ReadAllText(inventoryDropdownSurfaceBuilderPath);
    var requiredSymbols = new[]
    {
        "RenderDropdownSurface(",
        "ProjectDropdownSurface(",
        "_dropdownSurfaceProjection = ProjectDropdownSurface();",
        "HandleDropdownSurfaceCommand(",
        "ExecuteDropdownSelection(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_dropdownSurfaceDocument)",
        "AetheriaRuntimeInventoryDropdownSurfaceBuilder.Build(_dropdownSurfaceProjection.State)",
        "AetheriaRuntimeInventoryDropdownSurfaceBuilder.Project(",
        "AetheriaRuntimeInventoryDropdownSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeInventoryDropdownCommandKind.Close",
        "AetheriaRuntimeInventoryDropdownCommandKind.Select",
        "_dropdownSurfaceProjection?.TryResolve(command.Command, out var selection) == true",
        "AetheriaRuntimeInventoryDropdownSelectionKind.EntityBay",
        "AetheriaRuntimeInventoryDropdownSelectionKind.Loadout",
        "new AetheriaRuntimeInventoryDropdownEntityOption(",
        "new AetheriaRuntimeInventoryDropdownLoadoutOption(",
        "GameManager.TryGetObservedDockingBay(out var dockingBay)",
        "GameManager.ObservedAvailableEntities()",
        "GameManager.ObservedLoadoutTemplates()"
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
        "DropdownSurfaceId",
        "CloseDropdownSurfaceCommand",
        "SaveLoadoutCommand",
        "BuildDropdownSurfaceDefinition(",
        "private const string DropdownSurfaceType",
        "ResolveDropdownSurfaceDocument(",
        "new EveUiToolkitSurfaceLowerer()",
        "host.AddComponent<UIE.UIDocument>",
        "ContextMenu.AddDropdown(entity.Name",
        "ContextMenu.AddOption(GameManager.DockingBay.Name",
        "ContextMenu.AddOption(\"Save Loadout\"",
        "ContextMenu.AddDropdown(\"Restore Loadout\"",
        "ContextMenu.Show();",
        "string.Equals(request.Command, AetheriaRuntimeInventoryDropdownSurfaceBuilder.Close",
        "_dropdownCommands.TryGetValue(request.Command",
        "private readonly Dictionary<string, Action> _dropdownCommands",
        "BuildDropdownCommands(",
        "ProjectDropdownSurfaceState(",
        "GameManager.DockingBay",
        "GameManager.AvailableEntities()",
        "GameManager.LoadoutTemplates"
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

    if (!inventoryMenu.Contains("GameManager.TryGetObservedDockingBay(out var dockingBay)", StringComparison.Ordinal) ||
        inventoryMenu.Contains("GameManager.DockingBay", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryMenu must open against daemon-observed docking bay state instead of peeking GameManager.DockingBay directly.");
    }

    var requiredBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeInventoryDropdownSurfaceBuilder",
        "public const string SurfaceId = \"aetheria.inventory.panel.dropdown\"",
        "public const string Close = \"aetheria.inventory.panel.dropdown.close\"",
        "public const string SaveLoadout = \"aetheria.inventory.panel.dropdown.save_loadout\"",
        "public const string DockingBay = \"aetheria.inventory.panel.dropdown.docking_bay\"",
        "public enum AetheriaRuntimeInventoryDropdownCommandKind",
        "public readonly struct AetheriaRuntimeInventoryDropdownCommand",
        "public static class AetheriaRuntimeInventoryDropdownSurfaceCommands",
        "public static bool TryRead(",
        "AetheriaRuntimeInventoryDropdownSurfaceState",
        "AetheriaRuntimeInventoryDropdownGroup",
        "AetheriaRuntimeInventoryDropdownOption",
        "AetheriaRuntimeInventoryDropdownEntityOption",
        "AetheriaRuntimeInventoryDropdownBayOption",
        "AetheriaRuntimeInventoryDropdownLoadoutOption",
        "AetheriaRuntimeInventoryDropdownSelectionKind",
        "AetheriaRuntimeInventoryDropdownSurfaceProjection",
        "public static AetheriaRuntimeInventoryDropdownSurfaceProjection Project(",
        "public bool TryResolve(",
        "public static string EntityEquipmentCommand(",
        "public static string EntityBayCommand(",
        "public static string EntityCommand(",
        "public static string LoadoutCommand(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "providerKind: \"inventory.panel\"",
        "The observing client projects available inventory navigation; the shared runtime surface owns the dropdown contract."
    };
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !inventoryDropdownSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime inventory dropdown surface builder no longer owns the inventory dropdown shell contract: " +
            string.Join(", ", missingBuilderSymbols));
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
    var daemonHostPath = Path.Combine(root, "Aetheria.State.Daemon", "Program.cs");
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
    var daemonHost = File.Exists(daemonHostPath)
        ? File.ReadAllText(daemonHostPath)
        : throw new InvalidOperationException("Aetheria.State.Daemon program is missing.");
    var sharedCommands = File.Exists(sharedCommandsPath)
        ? File.ReadAllText(sharedCommandsPath)
        : throw new InvalidOperationException("Shared player-settings Eve command contract is missing.");
    var surfaceBuilder = File.Exists(surfaceBuilderPath)
        ? File.ReadAllText(surfaceBuilderPath)
        : throw new InvalidOperationException("Shared player-settings Eve surface builder is missing.");

    if (!bridge.Contains("AcceptedPlayerSettingsCommands", StringComparison.Ordinal) ||
        !bridge.Contains("ExecutePlayerSettingsCommandAsync", StringComparison.Ordinal) ||
        !bridge.Contains("PutPlayerSettingsSurfaceAsync", StringComparison.Ordinal) ||
        !bridge.Contains("SetPlayerName", StringComparison.Ordinal) ||
        !bridge.Contains("command.PlayerSettings.PlayerName", StringComparison.Ordinal))
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

    if (!daemonHost.Contains("PutPlayerSettingsSurfaceAsync", StringComparison.Ordinal) ||
        !daemonHost.Contains("AetheriaPlayerSettingsSurfaceProjector.Build", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon no longer republishes the provider-owned player-settings Eve surface.");
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

static void RequireDaemonHostDoesNotDrainRuntimeCommits(string root)
{
    var economyServerPath = Path.Combine(root, "Economy" + ".Server", "Program.cs");
    var economyServerProjectPath = Path.Combine(root, "Economy" + ".Server", "Economy" + ".Server.csproj");
    var daemonHostPath = Path.Combine(root, "Aetheria.State.Daemon", "Program.cs");
    var drainCommandsPath = Path.Combine(root, "Aetheria.State.DrainCommands", "Program.cs");
    var packageCommitLogPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeStateCommitLog.cs");
    var generatedUnityProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    if (File.Exists(economyServerPath) || File.Exists(economyServerProjectPath))
    {
        throw new InvalidOperationException("Economy" + ".Server still exists beside the first-class Aetheria.State.Daemon host.");
    }

    var daemonHost = File.Exists(daemonHostPath)
        ? File.ReadAllText(daemonHostPath)
        : throw new InvalidOperationException("Cannot verify daemon host command authority; Aetheria.State.Daemon program is missing.");
    var generatedUnityProject = File.Exists(generatedUnityProjectPath)
        ? File.ReadAllText(generatedUnityProjectPath)
        : throw new InvalidOperationException("Cannot verify daemon host command authority; GameCult.Aetheria.State.Unity.csproj is missing.");

    if (File.Exists(drainCommandsPath) ||
        File.Exists(Path.Combine(root, "Aetheria.State.DrainCommands", "Aetheria.State.DrainCommands.csproj")))
    {
        throw new InvalidOperationException(
            "Aetheria.State.DrainCommands still exists as a sidecar command applicator; command acceptance belongs inside the Verse daemon.");
    }

    if (File.Exists(packageCommitLogPath) ||
        generatedUnityProject.Contains("AetheriaRuntimeStateCommitLog.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity package still exposes the legacy runtime state commit log instead of typed Eve/daemon requests.");
    }

    var forbiddenSymbols = new[]
    {
        "AetheriaLegacyRuntimeSnapshotImporter.ImportPendingAsync",
        "DrainCommandsRuntimeCommitsAsync",
        "CountPendingRuntimeCommits",
        "Applied pending Aetheria runtime commits",
        "AetheriaRuntimeCommitDrainStatus",
        "PutRuntimeCommitDrainStatusAsync",
        "GetRuntimeCommitDrainStatusAsync",
        "PublishRuntimeCommitDrainDisabledAsync",
        "disabled-daemon-authority"
    };

    var hits = forbiddenSymbols
        .SelectMany(symbol => new[]
        {
            (Path: daemonHostPath, Symbol: symbol, Hit: daemonHost.Contains(symbol, StringComparison.Ordinal))
        })
        .Where(entry => entry.Hit)
        .Select(entry => $"{Path.GetRelativePath(root, entry.Path)}: {entry.Symbol}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "First-party daemon drains still apply legacy runtime commits instead of daemon/Eve command authority: " +
            string.Join(", ", hits));
    }

    var requiredSymbols = new[]
    {
        "AetheriaEveCommandBridge.AcceptObservedAsync",
        "AppliedInputSettingsCommands = report.AcceptedInputSettingsCommands",
        "AppliedLoadoutTemplateCommands = report.AcceptedLoadoutTemplateCommands"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !daemonHost.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon no longer advertises daemon authority while accepting observed Eve command documents: " +
            string.Join(", ", missingSymbols));
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
    var daemonHostPath = Path.Combine(root, "Aetheria.State.Daemon", "Program.cs");

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
        daemonHostPath
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
    var daemonHost = File.ReadAllText(daemonHostPath);

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

    var requiredDaemonHostSymbols = new[]
    {
        "EnsureVerseHostSettingsAsync",
        "AetheriaVerseDiscoveryHost",
        "discoveryHost.Update",
        "ReadOption(args, \"--verse-id\")",
        "ReadOption(args, \"--cultmesh-address\")",
        "GetVerseHostSettingsAsync",
        "PutVerseHostSettingsAsync",
        "AetheriaProviderAdvertisementProjector.Build(verseHost, node.StatePath, updatedAtUtc)"
    };
    var missingDaemonHostSymbols = requiredDaemonHostSymbols
        .Where(symbol => !daemonHost.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon is missing typed verse-host startup authority: " +
            string.Join(", ", missingDaemonHostSymbols));
    }
}

static void RequireClientTargetBootAuthority(string root)
{
    var unityPackageProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var boundaryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateBoundary.cs");
    var bootPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateBoot.cs");
    var clientTargetStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetStore.cs");
    var clientTargetCommandsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetCommands.cs");
    var aetheriaStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaState.cs");
    var verseDiscoveryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseDiscovery.cs");
    var replicaBridgePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseReplicaBridge.cs");
    var mainMenuSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeMainMenuSurfaceBuilder.cs");
    var bootstrapPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveRuntimeBootstrap.cs");
    var presenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");

    var requiredFiles = new[]
    {
        unityPackageProjectPath,
        boundaryPath,
        bootPath,
        clientTargetStorePath,
        clientTargetCommandsPath,
        aetheriaStatePath,
        verseDiscoveryPath,
        replicaBridgePath,
        mainMenuSurfaceBuilderPath,
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

    var unityPackageProject = File.ReadAllText(unityPackageProjectPath);
    var boundary = File.ReadAllText(boundaryPath);
    var boot = File.ReadAllText(bootPath);
    var clientTargetStore = File.ReadAllText(clientTargetStorePath);
    var clientTargetCommands = File.ReadAllText(clientTargetCommandsPath);
    var aetheriaState = File.ReadAllText(aetheriaStatePath);
    var verseDiscovery = File.ReadAllText(verseDiscoveryPath);
    var replicaBridge = File.ReadAllText(replicaBridgePath);
    var mainMenuSurfaceBuilder = File.ReadAllText(mainMenuSurfaceBuilderPath);
    var bootstrap = File.ReadAllText(bootstrapPath);
    var presenter = File.ReadAllText(presenterPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var mainMenu = File.ReadAllText(mainMenuPath);

    var requiredUnityPackageProjectSymbols = new[]
    {
        "AetheriaRuntimeClientTargetStore.cs",
        "AetheriaState.cs",
        "AetheriaRuntimeVerseDiscovery.cs",
        "AetheriaRuntimeStateBoundary.cs",
        "AetheriaRuntimeStateBoot.cs",
        "AetheriaRuntimeVerseReplicaBridge.cs"
    };
    var missingUnityPackageProjectSymbols = requiredUnityPackageProjectSymbols
        .Where(symbol => !unityPackageProject.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingUnityPackageProjectSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "GameCult.Aetheria.State.Unity.csproj no longer compiles the shared client-target boot files: " +
            string.Join(", ", missingUnityPackageProjectSymbols));
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
        "LastReplicaSyncAtUtc",
        "LastReplicaSyncError",
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
        "LastReplicaSyncAtUtc",
        "LastReplicaSyncError",
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

    var requiredAetheriaStateSymbols = new[]
    {
        "public readonly struct AetheriaState",
        "public static AetheriaState At(DirectoryInfo gameDataDirectory)",
        "public AetheriaClientTarget ClientTarget",
        "public readonly struct AetheriaClientTarget",
        "public AetheriaRuntimeClientTargetDocument Refresh()",
        "public AetheriaRuntimeClientTargetDocument DiscoverVerses()",
        "public AetheriaRuntimeClientTargetDocument SyncReplica()",
        "public AetheriaRuntimeClientTargetDocument CycleTransport()",
        "public AetheriaRuntimeClientTargetDocument RequestTitle(string title)",
        "public AetheriaRuntimeClientTargetDocument RequestVerseId(string verseId)",
        "public AetheriaRuntimeClientTargetDocument RequestCultMeshAddress(string cultMeshAddress)",
        "public AetheriaRuntimeClientTargetDocument RequestStateFilePath(string stateFilePath)",
        "public AetheriaRuntimeClientTargetDocument RequestDiscoveryEndpoints(IEnumerable<string>? discoveryEndpoints)",
        "public AetheriaRuntimeClientTargetDocument SelectDiscoveredVerse(",
        "AetheriaRuntimeStateBoundary.GetClientTargetPath(_gameDataDirectory)",
        "AetheriaRuntimeStateBoundary.GetStateFilePath(_gameDataDirectory)",
        "AetheriaRuntimeVerseDiscovery.RefreshClientTarget(",
        "AetheriaRuntimeVerseReplicaBridge.Sync(",
        "AetheriaRuntimeClientTargetStore.Update(",
        "NormalizeDiscoveryEndpoints(",
        "GetReplicaStateFilePath(gameDataDirectory, document.VerseId)"
    };
    var missingAetheriaStateSymbols = requiredAetheriaStateSymbols
        .Where(symbol => !aetheriaState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingAetheriaStateSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria state sugar no longer owns typed client-target mutation: " +
            string.Join(", ", missingAetheriaStateSymbols));
    }

    if (aetheriaState.Contains("public bool TryApply(AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        aetheriaState.Contains("public AetheriaRuntimeClientTargetDocument Apply(AetheriaClientTargetOperation", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes Apply vocabulary for client-target edits; Unity should request typed target changes.");
    }

    if (aetheriaState.Contains("public bool TryRequest(AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        aetheriaState.Contains("public AetheriaRuntimeClientTargetDocument Request(AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        clientTargetCommands.Contains("public readonly struct AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        clientTargetCommands.Contains("public enum AetheriaClientTargetOperationKind", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria client-target sugar still exposes generic operation-bag dispatch instead of native request methods plus a surface adapter.");
    }

    var forbiddenClientTargetSetters = new[]
    {
        "public AetheriaRuntimeClientTargetDocument SetTitle(",
        "public AetheriaRuntimeClientTargetDocument SetVerseId(",
        "public AetheriaRuntimeClientTargetDocument SetCultMeshAddress(",
        "public AetheriaRuntimeClientTargetDocument SetStateFilePath(",
        "public AetheriaRuntimeClientTargetDocument SetDiscoveryEndpoints("
    };
    var clientTargetSetterHits = forbiddenClientTargetSetters
        .Where(symbol => aetheriaState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (clientTargetSetterHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes Set vocabulary for client-target edits; Unity should request typed target changes: " +
            string.Join(", ", clientTargetSetterHits));
    }

    if (aetheriaState.Contains("DaemonOperations", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar regressed to command-applier naming instead of native state handles.");
    }

    if (aetheriaState.Contains("IReadOnlyDictionary<string, string>", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar exposes generic dictionary payload plumbing instead of typed client-target operations.");
    }

    if (aetheriaState.Contains("EveSurfaceCommandRequest", StringComparison.Ordinal) ||
        aetheriaState.Contains("ReadPayloadValue(", StringComparison.Ordinal) ||
        aetheriaState.Contains("request.Payload", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes Eve request payload plumbing instead of typed client-target operations.");
    }

    var requiredReplicaBridgeSymbols = new[]
    {
        "public static class AetheriaRuntimeVerseReplicaBridge",
        "AetheriaRuntimeVerseReplicaSyncResult",
        "Aetheria.State.Replica",
        "Sync(",
        "ProcessStartInfo",
        "AetheriaRuntimeStateBoundary.GetReplicaStateFilePath"
    };
    var missingReplicaBridgeSymbols = requiredReplicaBridgeSymbols
        .Where(symbol => !replicaBridge.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingReplicaBridgeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime Verse replica bridge is incomplete: " +
            string.Join(", ", missingReplicaBridgeSymbols));
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
        "LatestDaemonFrame(AetheriaRuntimeStateBootReport stateBoot)",
        "LatestVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)",
        "AetheriaState.At(ActionGameManager.GameDataDirectory)",
        ".ClientTarget",
        "RequestClientTargetCommand(request)",
        "AetheriaRuntimeClientTargetSurfaceCommands.TryRequest(",
        "LatestPlayerSettings(AetheriaRuntimeStateBootReport stateBoot)",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectVerseSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings("
    };
    var requiredMainMenuBuilderSymbols = new[]
    {
        "AetheriaRuntimeMainMenuSurfaceState",
        "ProjectVerseSettings(",
        "stateBoot.FailureMessage",
        "stateBoot.DiscoveryEndpoints",
        "stateBoot.DiscoveredVerses",
        "stateBoot.LastDiscoveryAtUtc",
        "stateBoot.LastDiscoveryError",
        "stateBoot.ReplicaStateFilePath",
        "stateBoot.LastReplicaSyncAtUtc",
        "stateBoot.LastReplicaSyncError",
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
    var missingMainMenuBuilderSymbols = requiredMainMenuBuilderSymbols
        .Where(symbol => !mainMenuSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingMainMenuBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared main-menu surface builder no longer lowers the client-target boot state through Eve: " +
            string.Join(", ", missingMainMenuBuilderSymbols));
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
        },
        [mainMenuPath] = new[]
        {
            "AetheriaRuntimeStateBoundary.",
            "AetheriaRuntimeClientTargetStore.",
            "AetheriaRuntimeVerseDiscovery.RefreshClientTarget(",
            "AetheriaRuntimeVerseReplicaBridge.Sync(",
            "AetheriaRuntimeClientTarget.",
            "DaemonOperations",
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
    var unityPackageProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var stateProjectPath = Path.Combine(root, "Aetheria.State", "Aetheria.State.csproj");
    var clientTargetCommandsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetCommands.cs");
    var aetheriaStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaState.cs");
    var clientTargetSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetSurfaceBuilder.cs");
    var verseHostCommandsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseHostCommands.cs");
    var clientTargetStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeClientTargetStore.cs");
    var verseDiscoveryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseDiscovery.cs");
    var replicaBridgePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseReplicaBridge.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var commandBridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var acceptanceStatusPath = Path.Combine(root, "Aetheria.State", "Documents", "AetheriaEveCommandAcceptanceStatus.cs");
    var operationsProjectorPath = Path.Combine(root, "Aetheria.State", "AetheriaOperationsSurfaceProjector.cs");
    var daemonHostPath = Path.Combine(root, "Aetheria.State.Daemon", "Program.cs");

    var requiredFiles = new[]
    {
        unityPackageProjectPath,
        stateProjectPath,
        clientTargetCommandsPath,
        aetheriaStatePath,
        clientTargetSurfaceBuilderPath,
        verseHostCommandsPath,
        clientTargetStorePath,
        verseDiscoveryPath,
        replicaBridgePath,
        mainMenuPath,
        commandBridgePath,
        acceptanceStatusPath,
        operationsProjectorPath,
        daemonHostPath
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

    var unityPackageProject = File.ReadAllText(unityPackageProjectPath);
    var stateProject = File.ReadAllText(stateProjectPath);
    var clientTargetCommands = File.ReadAllText(clientTargetCommandsPath);
    var aetheriaState = File.ReadAllText(aetheriaStatePath);
    var clientTargetSurfaceBuilder = File.ReadAllText(clientTargetSurfaceBuilderPath);
    var verseHostCommands = File.ReadAllText(verseHostCommandsPath);
    var clientTargetStore = File.ReadAllText(clientTargetStorePath);
    var verseDiscovery = File.ReadAllText(verseDiscoveryPath);
    var replicaBridge = File.ReadAllText(replicaBridgePath);
    var mainMenu = File.ReadAllText(mainMenuPath);
    var commandBridge = File.ReadAllText(commandBridgePath);
    var acceptanceStatus = File.ReadAllText(acceptanceStatusPath);
    var operationsProjector = File.ReadAllText(operationsProjectorPath);
    var daemonHost = File.ReadAllText(daemonHostPath);

    var requiredUnityPackageProjectSymbols = new[]
    {
        "AetheriaRuntimeClientTargetCommands.cs",
        "AetheriaState.cs",
        "AetheriaRuntimeClientTargetSurfaceBuilder.cs",
        "AetheriaRuntimeVerseDiscovery.cs",
        "AetheriaRuntimeVerseReplicaBridge.cs"
    };
    var missingUnityPackageProjectSymbols = requiredUnityPackageProjectSymbols
        .Where(symbol => !unityPackageProject.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingUnityPackageProjectSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "GameCult.Aetheria.State.Unity.csproj does not compile the Verse settings shell files: " +
            string.Join(", ", missingUnityPackageProjectSymbols));
    }

    var stateProjectIncludesRuntimePackage = stateProject.Contains(@"Runtime\*.cs", StringComparison.Ordinal);
    if (!stateProjectIncludesRuntimePackage &&
        !stateProject.Contains("AetheriaRuntimeVerseHostCommands.cs", StringComparison.Ordinal))
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
        "SyncReplica",
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
        "\"Replica Sync\"",
        "AetheriaRuntimeClientTargetCommands.SetStateFilePath",
        "AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints",
        "AetheriaRuntimeClientTargetCommands.DiscoverVerses",
        "AetheriaRuntimeClientTargetCommands.SelectDiscoveredVerse",
        "AetheriaRuntimeClientTargetCommands.SyncReplica",
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

    var requiredAetheriaStateSymbols = new[]
    {
        "public readonly struct AetheriaState",
        "public AetheriaClientTarget ClientTarget",
        "public readonly struct AetheriaClientTarget",
        "public AetheriaRuntimeClientTargetDocument Refresh()",
        "public AetheriaRuntimeClientTargetDocument DiscoverVerses()",
        "public AetheriaRuntimeClientTargetDocument SyncReplica()",
        "public AetheriaRuntimeClientTargetDocument CycleTransport()",
        "public AetheriaRuntimeClientTargetDocument RequestTitle(string title)",
        "public AetheriaRuntimeClientTargetDocument RequestVerseId(string verseId)",
        "public AetheriaRuntimeClientTargetDocument RequestCultMeshAddress(string cultMeshAddress)",
        "public AetheriaRuntimeClientTargetDocument RequestStateFilePath(string stateFilePath)",
        "public AetheriaRuntimeClientTargetDocument RequestDiscoveryEndpoints(IEnumerable<string>? discoveryEndpoints)",
        "public AetheriaRuntimeClientTargetDocument SelectDiscoveredVerse(",
        "AetheriaRuntimeClientTargetStore.Update(",
        "AetheriaRuntimeVerseDiscovery.RefreshClientTarget(",
        "AetheriaRuntimeVerseReplicaBridge.Sync(",
        "NormalizeDiscoveryEndpoints("
    };
    var missingAetheriaStateSymbols = requiredAetheriaStateSymbols
        .Where(symbol => !aetheriaState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingAetheriaStateSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria state sugar no longer owns the Verse settings mutation bridge: " +
            string.Join(", ", missingAetheriaStateSymbols));
    }

    if (aetheriaState.Contains("public bool TryApply(AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        aetheriaState.Contains("public AetheriaRuntimeClientTargetDocument Apply(AetheriaClientTargetOperation", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes Apply vocabulary for Verse settings edits; Unity should request typed target changes.");
    }

    if (aetheriaState.Contains("public bool TryRequest(AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        aetheriaState.Contains("public AetheriaRuntimeClientTargetDocument Request(AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        clientTargetCommands.Contains("public readonly struct AetheriaClientTargetOperation", StringComparison.Ordinal) ||
        clientTargetCommands.Contains("public enum AetheriaClientTargetOperationKind", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Verse settings sugar still exposes generic operation-bag dispatch instead of native request methods plus a surface adapter.");
    }

    var forbiddenClientTargetSetters = new[]
    {
        "public AetheriaRuntimeClientTargetDocument SetTitle(",
        "public AetheriaRuntimeClientTargetDocument SetVerseId(",
        "public AetheriaRuntimeClientTargetDocument SetCultMeshAddress(",
        "public AetheriaRuntimeClientTargetDocument SetStateFilePath(",
        "public AetheriaRuntimeClientTargetDocument SetDiscoveryEndpoints("
    };
    var clientTargetSetterHits = forbiddenClientTargetSetters
        .Where(symbol => aetheriaState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (clientTargetSetterHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes Set vocabulary for Verse settings edits; Unity should request typed target changes: " +
            string.Join(", ", clientTargetSetterHits));
    }

    if (aetheriaState.Contains("DaemonOperations", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar regressed to command-applier naming instead of native state handles.");
    }

    if (aetheriaState.Contains("IReadOnlyDictionary<string, string>", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar exposes generic dictionary payload plumbing instead of typed client-target operations.");
    }

    if (aetheriaState.Contains("EveSurfaceCommandRequest", StringComparison.Ordinal) ||
        aetheriaState.Contains("ReadPayloadValue(", StringComparison.Ordinal) ||
        aetheriaState.Contains("request.Payload", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes Eve request payload plumbing instead of typed client-target operations.");
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

    var requiredReplicaBridgeSymbols = new[]
    {
        "public static class AetheriaRuntimeVerseReplicaBridge",
        "ProcessStartInfo",
        "Aetheria.State.Replica",
        "Sync("
    };
    var missingReplicaBridgeSymbols = requiredReplicaBridgeSymbols
        .Where(symbol => !replicaBridge.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingReplicaBridgeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse replica bridge no longer exposes the sync transport used by the Unity shell: " +
            string.Join(", ", missingReplicaBridgeSymbols));
    }

    var requiredMainMenuSymbols = new[]
    {
        "AetheriaRuntimeMainMenuCommandKind.ShowVerseSettings",
        "ShowVerseSettingsSurface()",
        "HandleVerseSettingsSurfaceCommand(EveSurfaceCommandRequest request)",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildVerseSettingsShell(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectVerseSettings(",
        "AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeMainMenuCommandKind.ClientTargetCommand",
        "AetheriaRuntimeMainMenuCommandKind.VerseHostCommand",
        "RequestClientTargetCommand(request)",
        "SendKnownAetheriaEveCommand(request, \"Verse-host\")",
        "AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand(",
        "AetheriaState.At(ActionGameManager.GameDataDirectory)",
        ".ClientTarget",
        "AetheriaRuntimeClientTargetSurfaceCommands.TryRequest("
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

    var forbiddenSubmitAcceptanceSymbols = new[]
    {
        "private static bool TryRequestClientTargetCommand(",
        "private static bool TrySendKnownAetheriaEveCommand(",
        "if (TryRequestClientTargetCommand(request))",
        "if (TrySendKnownAetheriaEveCommand(request, \"Verse-host\"))"
    };
    var submitAcceptanceHits = forbiddenSubmitAcceptanceSymbols
        .Where(symbol => mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (submitAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu Verse settings command lowering still treats submission as local acceptance state: " +
            string.Join(", ", submitAcceptanceHits));
    }

    var forbiddenMainMenuMutationSymbols = new[]
    {
        "AetheriaRuntimeClientTargetStore.",
        "AetheriaRuntimeVerseDiscovery.RefreshClientTarget(",
        "AetheriaRuntimeVerseReplicaBridge.Sync(",
        "AetheriaRuntimeStateBoundary.",
        "AetheriaRuntimeClientTarget.",
        "DaemonOperations",
        "ReadPayloadValue(",
        "ParseDiscoveryEndpoints(",
        "request.Payload",
        "IReadOnlyDictionary<string, string>",
        "clientTarget.SetTitle(",
        "clientTarget.SetVerseId(",
        "clientTarget.SetCultMeshAddress(",
        "clientTarget.SetStateFilePath(",
        "clientTarget.SetDiscoveryEndpoints(",
        "clientTarget.DiscoverVerses()",
        "clientTarget.SelectDiscoveredVerse(",
        "clientTarget.SyncReplica()",
        "TryQueueVerseHostCommand(",
        "TrySendVerseHostCommand(request.Command)",
        "CommandKindForSurface(request)",
        "AetheriaRuntimeClientTargetCommands.IsKnown(request.Command",
        "AetheriaRuntimeVerseHostCommands.IsKnown(request.Command",
        "new EveSurfaceCommandRequest(",
        "AetheriaRuntimeEveCommandLog.Queue",
    };
    var mainMenuMutationHits = forbiddenMainMenuMutationSymbols
        .Where(symbol => mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (mainMenuMutationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still mutates the client target directly instead of dispatching through the runtime daemon operations: " +
            string.Join(", ", mainMenuMutationHits));
    }

    var requiredBridgeSymbols = new[]
    {
        "AcceptedVerseHostCommands",
        "AetheriaRuntimeEveCommandKind.VerseHostRefresh",
        "AetheriaRuntimeEveCommandKind.CycleVerseHostVisibility",
        "ExecuteVerseHostCommandAsync",
        "PutVerseHostSettingsAsync",
        "PutProviderAdvertisementAsync",
        "AetheriaOperationsSurfaceProjector.Build(",
        "switch (command.Kind)"
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

    var requiredAcceptanceSymbols = new[]
    {
        "AetheriaEveCommandAcceptanceStatus",
        "aetheria.eve_command_acceptance_status.v1",
        "ObservedBeforeAccept",
        "AppliedVerseHostCommands"
    };
    var missingAcceptanceSymbols = requiredAcceptanceSymbols
        .Where(symbol => !acceptanceStatus.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingAcceptanceSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Eve command acceptance status no longer records Verse-host command counts: " +
            string.Join(", ", missingAcceptanceSymbols));
    }

    if (File.Exists(Path.Combine(root, "Aetheria.State", "Documents", "AetheriaEveCommandDrainStatus.cs")) ||
        acceptanceStatus.Contains("AetheriaEveCommandDrainStatus", StringComparison.Ordinal) ||
        acceptanceStatus.Contains("aetheria.eve_command_drain_status", StringComparison.Ordinal) ||
        daemonHost.Contains("DrainEveCommandsAsync", StringComparison.Ordinal) ||
        daemonHost.Contains("PutEveCommandDrainStatusAsync", StringComparison.Ordinal) ||
        daemonHost.Contains("GetEveCommandDrainStatusAsync", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve command acceptance regressed to drain/queue vocabulary; the daemon should accept observed typed command records.");
    }

    if (acceptanceStatus.Contains("public int PendingBeforeApply", StringComparison.Ordinal) &&
        !acceptanceStatus.Contains("[IgnoreMember]", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve command acceptance exposes the legacy PendingBeforeApply schema shim as serialized command state.");
    }

    var pendingBeforeApplyUseSources = new Dictionary<string, string>
    {
        ["Aetheria daemon host"] = daemonHost,
        ["Aetheria Eve command bridge"] = commandBridge,
        ["operations projector"] = operationsProjector
    };
    var pendingBeforeApplyUses = pendingBeforeApplyUseSources
        .Where(source => source.Value.Contains("PendingBeforeApply", StringComparison.Ordinal))
        .Select(source => source.Key)
        .ToArray();
    if (pendingBeforeApplyUses.Length > 0)
    {
        throw new InvalidOperationException(
            "Live Eve command acceptance code still uses the legacy PendingBeforeApply shim instead of ObservedBeforeAccept: " +
            string.Join(", ", pendingBeforeApplyUses));
    }

    if (!operationsProjector.Contains("Verse Host Commands", StringComparison.Ordinal) ||
        !operationsProjector.Contains("AppliedVerseHostCommands", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Operations surface no longer projects Verse-host Eve command counts.");
    }

    if (!commandBridge.Contains("report.AcceptedVerseHostCommands++", StringComparison.Ordinal) ||
        !daemonHost.Contains("AppliedVerseHostCommands = report.AcceptedVerseHostCommands", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon command acceptance no longer publishes Verse-host Eve command totals.");
    }
}

static void RequireTypedStatRecipeOperations(string root)
{
    var statRecipesPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStatRecipes.cs");
    var legacyApplierPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStatRecipeCommandApplier.cs");
    var catalogStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs");
    var testsPath = Path.Combine(root, "Assets", "Scripts", "Tests", "StatRecipeSurfaceCommandTests.cs");

    if (!File.Exists(statRecipesPath))
        throw new InvalidOperationException("Typed stat recipe operations are missing.");
    if (File.Exists(legacyApplierPath))
        throw new InvalidOperationException("Legacy stat recipe command applier still exists.");
    if (!File.Exists(catalogStorePath) || !File.Exists(testsPath))
        throw new InvalidOperationException("Cannot verify typed stat recipe operations; required files are missing.");

    var statRecipes = File.ReadAllText(statRecipesPath);
    var catalogStore = File.ReadAllText(catalogStorePath);
    var tests = File.ReadAllText(testsPath);

    var requiredTypedSymbols = new[]
    {
        "public static class AetheriaRuntimeStatRecipes",
        "public static AetheriaRuntimeStatRecipeSurfaceState Refresh(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SelectStat(",
        "public static AetheriaRuntimeStatRecipeSurfaceState AddStat(",
        "public static AetheriaRuntimeStatRecipeSurfaceState RemoveStat(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SetStatName(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SetBaseValue(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SetConditionEnabled(",
        "public static AetheriaRuntimeStatRecipeSurfaceState ToggleCondition(",
        "public static AetheriaRuntimeStatRecipeSurfaceState CycleInfluenceOperation(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SetInfluenceAmount(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SetInfluenceCurve(",
        "public static AetheriaRuntimeStatRecipeSurfaceState SetPreviewCondition("
    };
    var missingTypedSymbols = requiredTypedSymbols
        .Where(symbol => !statRecipes.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTypedSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed stat recipe operations are incomplete: " +
            string.Join(", ", missingTypedSymbols));
    }

    var forbiddenPublicStringlySymbols = new[]
    {
        "public static AetheriaRuntimeStatRecipeSurfaceState Apply",
        "public static AetheriaRuntimeStatRecipeSurfaceState ApplyCommand",
        "public static class AetheriaRuntimeStatRecipeDaemonOperations",
        "public static AetheriaRuntimeStatRecipeSurfaceState",
        "IReadOnlyDictionary<string, string>? payload"
    };
    var publicStringlyHits = forbiddenPublicStringlySymbols
        .Where(symbol =>
            symbol == "public static AetheriaRuntimeStatRecipeSurfaceState"
                ? statRecipes.Contains("public static AetheriaRuntimeStatRecipeSurfaceState Apply(", StringComparison.Ordinal)
                : statRecipes.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicStringlyHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Stat recipe client API still exposes stringly command/payload operations: " +
            string.Join(", ", publicStringlyHits));
    }

    var forbiddenCatalogDecoderSymbols = new[]
    {
        "DrainCommandsStatRecipeCommand(",
        "AetheriaRuntimeStatRecipes.ApplyCommand(",
        "ReadPayloadDouble(",
        "ReadPayloadBool("
    };
    var catalogDecoderHits = forbiddenCatalogDecoderSymbols
        .Where(symbol => catalogStore.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (catalogDecoderHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Catalog store still replays stringly stat recipe command payloads: " +
            string.Join(", ", catalogDecoderHits));
    }

    if (tests.Contains("AetheriaRuntimeStatRecipeDaemonOperations", StringComparison.Ordinal) ||
        tests.Contains("AetheriaRuntimeStatRecipeCommands.", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Stat recipe tests still exercise string command constants instead of typed operations.");
    }
}

static void RequireTypedDaemonCommandPayloads(string root)
{
    var daemonDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonDocuments.cs");
    var daemonClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperationClient.cs");
    var daemonRuntimeOperationsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperations.cs");
    var legacyDaemonApplierPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonCommandApplier.cs");
    var stateNodePath = Path.Combine(root, "Aetheria.State", "AetheriaStateNode.cs");
    var observerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonObserver.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var testsPath = Path.Combine(root, "Assets", "Scripts", "Tests", "DaemonRuntimeDocumentTests.cs");
    var daemonSurfaceCommandsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSurfaceCommands.cs");

    if (File.Exists(legacyDaemonApplierPath))
        throw new InvalidOperationException("Legacy daemon command applier still exists; daemon operations must own execution.");

    var requiredFiles = new[] { daemonDocumentsPath, daemonClientPath, daemonRuntimeOperationsPath, stateNodePath, observerPath, actionGameManagerPath, testsPath, daemonSurfaceCommandsPath };
    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed daemon command payloads cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var daemonDocuments = File.ReadAllText(daemonDocumentsPath);
    var daemonClient = File.ReadAllText(daemonClientPath);
    var daemonOperationsSource = File.ReadAllText(daemonRuntimeOperationsPath);
    var stateNode = File.ReadAllText(stateNodePath);
    var observer = File.ReadAllText(observerPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var tests = File.ReadAllText(testsPath);
    var daemonSurfaceCommands = File.ReadAllText(daemonSurfaceCommandsPath);

    var requiredPayloadTypes = new[]
    {
        "public enum AetheriaRuntimeDaemonCommandKinds",
        "public sealed class AetheriaRuntimeActionBarBindingCommand",
        "public sealed class AetheriaRuntimeCargoTransferCommand",
        "public sealed class AetheriaRuntimeTradePurchaseCommand",
        "public sealed class AetheriaRuntimeLootPickupCommand",
        "public sealed class AetheriaRuntimeLoadoutRestoreCommand",
        "public sealed class AetheriaRuntimeEquipmentTransferCommand",
        "public sealed class AetheriaRuntimeStoreItemCommand"
    };
    var missingPayloadTypes = requiredPayloadTypes
        .Where(symbol => !daemonDocuments.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingPayloadTypes.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon command document is missing typed command payloads: " +
            string.Join(", ", missingPayloadTypes));
    }

    var liveSources = new Dictionary<string, string>
    {
        ["daemon documents"] = daemonDocuments,
        ["daemon operation client"] = daemonClient,
        ["daemon operations"] = daemonOperationsSource,
        ["state node"] = stateNode,
        ["Unity daemon observer"] = observer,
        ["ActionGameManager"] = actionGameManager,
        ["daemon command tests"] = tests,
        ["daemon surface commands"] = daemonSurfaceCommands
    };
    foreach (var source in liveSources)
    {
        var forbidden = new[]
            {
                "Dictionary<string, string> Payload",
                ".Payload[",
                "ReadPayloadInt(",
                "ReadPayloadDouble(",
                "ReadPayloadBool(",
                "Payload(command,"
            }
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .ToArray();
        if (forbidden.Length > 0)
        {
            throw new InvalidOperationException(
                $"{source.Key} still uses stringly daemon command payloads: " +
                string.Join(", ", forbidden));
        }
    }

    var forbiddenDaemonKindSymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonCommandKinds",
        "public const string SetTarget",
        "Queue(\r\n            string kind",
        "Create(\r\n            string kind"
    };
    var daemonKindSources = new Dictionary<string, string>
    {
        ["daemon documents"] = daemonDocuments,
        ["daemon operation client"] = daemonClient,
        ["Unity daemon observer"] = observer
    };
    foreach (var source in daemonKindSources)
    {
        var forbidden = forbiddenDaemonKindSymbols
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .ToArray();
        if (forbidden.Length > 0)
        {
            throw new InvalidOperationException(
                $"{source.Key} still exposes daemon command kind as strings: " +
                string.Join(", ", forbidden));
        }
    }

    if (!daemonDocuments.Contains("public AetheriaRuntimeDaemonCommandKinds Kind { get; set; }", StringComparison.Ordinal) ||
        !daemonDocuments.Contains("public AetheriaRuntimeDaemonCommandKinds Kind { get; }", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon command document/envelope must expose typed enum command kinds.");
    }

    var requiredClientSymbols = new[]
    {
        "public const string DefaultClientId = \"aetheria-daemon-client\"",
        "ClientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId",
        "private AetheriaRuntimeDaemonCommandEnvelope Send(AetheriaRuntimeDaemonCommandDocument command)",
        "ReadObservedDaemonCommands()",
        "Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState, AetheriaRuntimeDaemonCommandEnvelope> submit",
        "return Send((client, observed) => client.SetTarget(observed, targetEntityKey));",
        "return Send((client, observed) => client.TransferCargoItem(",
        "command.ActionBarBinding.Kind",
        "command.CargoTransfer.OriginEntityKey",
        "command.TradePurchase.TotalPrice",
        "command.LootPickup.ItemKey",
        "command.LoadoutRestore.TemplateName",
        "command.EquipmentTransfer.SourceKind",
        "command.StoreItem.SourceEquipmentIndex"
    };
    var daemonOperationsUnity = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonOperations.cs"));
    var clientAndObserver = daemonClient + "\n" + observer + "\n" + stateNode + "\n" + daemonOperationsUnity;
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !clientAndObserver.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon command producers are not filling typed payloads: " +
            string.Join(", ", missingClientSymbols));
    }

    var forbiddenQueueSymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonCommandLog",
        "internal static class AetheriaRuntimeDaemonCommandLog",
        "public AetheriaRuntimeDaemonCommandEnvelope Queue(",
        "public static AetheriaRuntimeDaemonCommandEnvelope QueueCommand(",
        "AetheriaRuntimeDaemonCommandLog.QueueCommand(",
        "AetheriaRuntimeDaemonCommandLog.",
        "AetheriaRuntimeDaemonCommandInbox",
        "ReadPending(",
        ".daemon.pending",
        "stateFilePath + \".daemon.commands\"",
        "_operationClient.Queue("
    };
    var daemonQueueSources = new Dictionary<string, string>
    {
        ["daemon operation client"] = daemonClient,
        ["state node"] = stateNode,
        ["Unity daemon observer"] = observer,
        ["ActionGameManager"] = actionGameManager,
        ["daemon command tests"] = tests
    };
    var survivingQueueSymbols = daemonQueueSources
        .SelectMany(source => forbiddenQueueSymbols
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{source.Key}: {symbol}"))
        .ToArray();

    if (survivingQueueSymbols.Length > 0 ||
        observer.Contains("_operationClient.Queue(", StringComparison.Ordinal) ||
        daemonClient.Contains("AetheriaRuntimeDaemonCommandLog.", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity-facing daemon operation clients still expose queue semantics instead of sending typed daemon operations: " +
            string.Join(", ", survivingQueueSymbols));
    }

    var forbiddenUnityDocumentSubmitSymbols = new[]
    {
        "SendOperation(AetheriaRuntimeDaemonCommandKinds",
        "Action<AetheriaRuntimeDaemonCommandDocument>",
        "_operationClient.Create(",
        "_operationClient.TrySend(command",
        "command.TargetEntityKey =",
        "command.ActionBarBinding.",
        "command.CargoTransfer.",
        "command.TradePurchase.",
        "command.LootPickup.",
        "command.LoadoutRestore.",
        "command.EquipmentTransfer.",
        "command.StoreItem."
    };
    var unityDocumentSubmitHits = new Dictionary<string, string>
    {
        ["Unity daemon observer"] = observer,
        ["Unity daemon operations"] = daemonOperationsUnity
    }
        .SelectMany(source => forbiddenUnityDocumentSubmitSymbols
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{source.Key}: {symbol}"))
        .ToArray();
    if (unityDocumentSubmitHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity daemon operation adapters still fill command documents instead of delegating to typed runtime operations: " +
            string.Join(", ", unityDocumentSubmitHits));
    }

    var forbiddenPublicDocumentSubmitSymbols = new[]
    {
        "public AetheriaRuntimeDaemonCommandEnvelope Send(",
        "public bool TrySend(\r\n            AetheriaRuntimeDaemonCommandKinds",
        "public bool TrySend(\r\n            AetheriaRuntimeDaemonCommandDocument",
        "public AetheriaRuntimeDaemonCommandDocument Create("
    };
    var publicDocumentSubmitHits = forbiddenPublicDocumentSubmitSymbols
        .Where(symbol => daemonClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicDocumentSubmitHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon operation client still exposes public document-shaped submission APIs instead of typed operations: " +
            string.Join(", ", publicDocumentSubmitHits));
    }

    var forbiddenClientDocumentBuilderSymbols = new[]
    {
        "TrySendCommandKind(",
        "Action<AetheriaRuntimeDaemonCommandDocument>",
        "configure?.Invoke(command)",
        "Send(AetheriaRuntimeDaemonCommandKinds"
    };
    var clientDocumentBuilderHits = forbiddenClientDocumentBuilderSymbols
        .Where(symbol => daemonClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (clientDocumentBuilderHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon operation client still routes typed operations through mutable document builder callbacks: " +
            string.Join(", ", clientDocumentBuilderHits));
    }

    var forbiddenSharedDaemonClientDefaults = new[]
    {
        "string clientId = \"unity-observer\"",
        "? \"unity-observer\" :",
        "? \"unity-uitoolkit\" : request.ClientId"
    };
    var sharedDaemonDefaultSources = new Dictionary<string, string>
    {
        ["daemon operation client"] = daemonClient,
        ["daemon surface commands"] = daemonSurfaceCommands
    };
    var sharedDaemonDefaultHits = sharedDaemonDefaultSources
        .SelectMany(source => forbiddenSharedDaemonClientDefaults
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{source.Key}: {symbol}"))
        .ToArray();
    if (sharedDaemonDefaultHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared daemon command clients still use Unity-specific fallback client ids: " +
            string.Join(", ", sharedDaemonDefaultHits));
    }

    if (tests.Contains("AetheriaRuntimeDaemonCommandLog.", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon command tests still reach through the old command log instead of the typed command client/inbox.");
    }

    if (actionGameManager.Contains("local mutation suppressed", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager still treats failed daemon submission as a local-mutation fallback.");
    }

    var failedSubmitSuccessHits = System.Text.RegularExpressions.Regex
        .Matches(
            actionGameManager,
            "Failed to send Aetheria daemon[\\s\\S]*?return true;",
            System.Text.RegularExpressions.RegexOptions.Multiline)
        .Cast<System.Text.RegularExpressions.Match>()
        .Where(match => !match.Value.Contains("return false;", StringComparison.Ordinal))
        .Select(match => match.Value.Split('\n').FirstOrDefault()?.Trim() ?? "daemon submit catch")
        .Take(5)
        .ToArray();
    if (failedSubmitSuccessHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager reports failed daemon submissions as successful commands: " +
            string.Join("; ", failedSubmitSuccessHits));
    }
}

static void RequireUnityPublicRequestVocabulary(string root)
{
    var checkedRoots = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay"),
        Path.Combine(root, "Assets", "Scripts", "UI"),
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime")
    };

    var commitPattern = new System.Text.RegularExpressions.Regex(
        "\\bCommit[A-Z][A-Za-z0-9_]*\\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    var hits = checkedRoots
        .Where(Directory.Exists)
        .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
        .Where(path => !path.Contains(Path.Combine("ServerShared", "NIH"), StringComparison.Ordinal))
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .SelectMany(line => commitPattern.Matches(line.Line)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(match => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {match.Value}"))
        .Take(20)
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity-facing Aetheria APIs must say Request/Submit, not Commit; Unity is an input provider, not state authority: " +
            string.Join("; ", hits));
    }
}

static void RequireDaemonVersePublication(string root)
{
    var daemonDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonDocuments.cs");
    var daemonTickRunnerPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonTickRunner.cs");
    var daemonPublicationStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonPublicationStore.cs");
    var daemonSoaDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSoaDocuments.cs");
    var daemonStateRefsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonStateRefs.cs");
    var daemonGameSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonGameSurfaceBuilder.cs");
    var daemonEditorSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonEditorSurfaceBuilder.cs");
    var daemonSurfaceCommandCatalogPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSurfaceCommandCatalog.cs");
    var daemonHostProjectPath = Path.Combine(root, "Aetheria.State.Daemon", "Aetheria.State.Daemon.csproj");
    var daemonHostProgramPath = Path.Combine(root, "Aetheria.State.Daemon", "Program.cs");
    var documentRegistryPath = Path.Combine(root, "Aetheria.State", "AetheriaDocumentRegistry.cs");
    var stateNodePath = Path.Combine(root, "Aetheria.State", "AetheriaStateNode.cs");
    var daemonSurfaceProjectorPath = Path.Combine(root, "Aetheria.State", "AetheriaRuntimeEveSurfaceStateProjector.cs");
    var providerAdvertisementPath = Path.Combine(root, "Aetheria.State", "AetheriaProviderAdvertisementProjector.cs");
    var documentStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCultCacheDocumentStore.cs");
    var boundaryPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateBoundary.cs");
    var testsPath = Path.Combine(root, "Assets", "Scripts", "Tests", "DaemonRuntimeDocumentTests.cs");
    var smokePath = Path.Combine(root, "Aetheria.State.Smoke", "Program.cs");
    var notePath = Path.Combine(root, "Aetheria.State", "docs", "verse-daemon-shape.md");

    var requiredFiles = new[]
    {
        daemonDocumentsPath,
        daemonTickRunnerPath,
        daemonPublicationStorePath,
        daemonSoaDocumentsPath,
        daemonStateRefsPath,
        daemonGameSurfaceBuilderPath,
        daemonEditorSurfaceBuilderPath,
        daemonSurfaceCommandCatalogPath,
        daemonHostProjectPath,
        daemonHostProgramPath,
        documentRegistryPath,
        stateNodePath,
        daemonSurfaceProjectorPath,
        providerAdvertisementPath,
        documentStorePath,
        boundaryPath,
        testsPath,
        smokePath,
        notePath
    };
    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria daemon Verse publication cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var daemonDocuments = File.ReadAllText(daemonDocumentsPath);
    var daemonTickRunner = File.ReadAllText(daemonTickRunnerPath);
    var daemonPublicationStore = File.ReadAllText(daemonPublicationStorePath);
    var daemonSoaDocuments = File.ReadAllText(daemonSoaDocumentsPath);
    var daemonStateRefs = File.ReadAllText(daemonStateRefsPath);
    var daemonGameSurfaceBuilder = File.ReadAllText(daemonGameSurfaceBuilderPath);
    var daemonEditorSurfaceBuilder = File.ReadAllText(daemonEditorSurfaceBuilderPath);
    var daemonSurfaceCommandCatalog = File.ReadAllText(daemonSurfaceCommandCatalogPath);
    var daemonHostProject = File.ReadAllText(daemonHostProjectPath);
    var daemonHostProgram = File.ReadAllText(daemonHostProgramPath);
    var documentRegistry = File.ReadAllText(documentRegistryPath);
    var stateNode = File.ReadAllText(stateNodePath);
    var daemonSurfaceProjector = File.ReadAllText(daemonSurfaceProjectorPath);
    var providerAdvertisement = File.ReadAllText(providerAdvertisementPath);
    var documentStore = File.ReadAllText(documentStorePath);
    var boundary = File.ReadAllText(boundaryPath);
    var tests = File.ReadAllText(testsPath);
    var smoke = File.ReadAllText(smokePath);
    var note = File.ReadAllText(notePath);

    var requiredDocumentSymbols = new[]
    {
        "public const string ProviderAdvertisement = \"gamecult.aetheria.daemon_provider_advertisement.v1\"",
        "public const string Health = \"gamecult.aetheria.daemon_health.v1\"",
        "public const string CommandBoundary = \"gamecult.aetheria.daemon_command_boundary.v1\"",
        "public const string GameSurface = \"gamecult.aetheria.daemon_game_surface.v1\"",
        "public const string EditorSurface = \"gamecult.aetheria.daemon_editor_surface.v1\"",
        "public sealed class AetheriaRuntimeDaemonProviderAdvertisementDocument",
        "public sealed class AetheriaRuntimeDaemonHealthDocument",
        "public sealed class AetheriaRuntimeDaemonCommandBoundaryDocument",
        "public sealed class AetheriaRuntimeDaemonCommandBoundaryEntry",
        "AetheriaRuntimeDaemonCommandKinds Kind",
        "AetheriaRuntimeCargoTransferCommand",
        "[CultDocument(\"gamecult.aetheria.daemon_provider_advertisement\", \"gamecult.aetheria.daemon_provider_advertisement.v1\")]",
        "[CultDocument(\"gamecult.aetheria.daemon_health\", \"gamecult.aetheria.daemon_health.v1\")]",
        "[CultDocument(\"gamecult.aetheria.daemon_command_boundary\", \"gamecult.aetheria.daemon_command_boundary.v1\")]",
        "[CultDocument(\"gamecult.aetheria.daemon_frame\", \"gamecult.aetheria.daemon_frame.v1\")]",
        "[CultDocument(\"gamecult.aetheria.daemon_command\", \"gamecult.aetheria.daemon_command.v1\")]",
        "EveGuiSurfaceWitnessPath",
        "EveTuiSurfaceWitnessPath",
        "EditorGuiSurfaceWitnessPath",
        "EditorTuiSurfaceWitnessPath"
    };
    var missingDocumentSymbols = requiredDocumentSymbols
        .Where(symbol => !daemonDocuments.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Verse publication documents are incomplete: " +
            string.Join(", ", missingDocumentSymbols));
    }

    if (!daemonSoaDocuments.Contains("[CultDocument(\"gamecult.aetheria.daemon_soa_view\", \"gamecult.aetheria.daemon_soa_view.v1\")]", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Daemon SoA view is no longer a registered CultCache/CultNet document.");
    }

    var requiredBoundarySymbols = new[]
    {
        "RuntimeDaemonProviderFileSuffix",
        "RuntimeDaemonHealthFileSuffix",
        "RuntimeDaemonCommandBoundaryFileSuffix",
        "RuntimeDaemonGameSurfaceFileSuffix",
        "RuntimeDaemonGameTuiSurfaceFileSuffix",
        "RuntimeDaemonEditorSurfaceFileSuffix",
        "RuntimeDaemonEditorTuiSurfaceFileSuffix",
        "GetDaemonProviderPath(",
        "GetDaemonHealthPath(",
        "GetDaemonCommandBoundaryPath(",
        "GetDaemonGameSurfacePath(",
        "GetDaemonGameTuiSurfacePath(",
        "GetDaemonEditorSurfacePath(",
        "GetDaemonEditorTuiSurfacePath("
    };
    var missingBoundarySymbols = requiredBoundarySymbols
        .Where(symbol => !boundary.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBoundarySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Verse publication paths are incomplete: " +
            string.Join(", ", missingBoundarySymbols));
    }

    var requiredStoreSymbols = new[]
    {
        "WriteDaemonProviderAdvertisement(",
        "ReadDaemonProviderAdvertisement(",
        "WriteDaemonHealth(",
        "ReadDaemonHealth(",
        "WriteDaemonCommandBoundary(",
        "ReadDaemonCommandBoundary(",
        "WriteDaemonGameSurface(",
        "ReadDaemonGameSurface(",
        "WriteDaemonEditorSurface(",
        "ReadDaemonEditorSurface(",
        "PublishProviderAdvertisement(",
        "PublishHealth(",
        "PublishCommandBoundary(",
        "PublishGameSurface(",
        "PublishGameTuiSurface(",
        "PublishEditorSurface(",
        "PublishEditorTuiSurface(",
        "TryReadProviderAdvertisement(",
        "TryReadHealth(",
        "TryReadCommandBoundary(",
        "TryReadGameSurface(",
        "TryReadGameTuiSurface(",
        "TryReadEditorSurface(",
        "TryReadEditorTuiSurface("
    };
    var storeSource = documentStore + "\n" + daemonPublicationStore;
    var missingStoreSymbols = requiredStoreSymbols
        .Where(symbol => !storeSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingStoreSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Verse publication store is incomplete: " +
            string.Join(", ", missingStoreSymbols));
    }

    var requiredDaemonRegistrySymbols = new[]
    {
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(registry)",
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonHealthDocument>(registry)",
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(registry)",
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonFrameDocument>(registry)",
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonSoaViewDocument>(registry)",
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandDocument>(registry)",
        "typeof(AetheriaRuntimeDaemonProviderAdvertisementDocument)",
        "typeof(AetheriaRuntimeDaemonHealthDocument)",
        "typeof(AetheriaRuntimeDaemonCommandBoundaryDocument)",
        "typeof(AetheriaRuntimeDaemonFrameDocument)",
        "typeof(AetheriaRuntimeDaemonSoaViewDocument)",
        "typeof(AetheriaRuntimeDaemonCommandDocument)"
    };
    var missingDaemonRegistrySymbols = requiredDaemonRegistrySymbols
        .Where(symbol => !documentRegistry.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonRegistrySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon API documents are not registered as CultCache/CultNet records: " +
            string.Join(", ", missingDaemonRegistrySymbols));
    }

    var requiredDaemonNodeSymbols = new[]
    {
        "PutDaemonProviderAdvertisementAsync(",
        "GetDaemonProviderAdvertisementAsync(",
        "PutDaemonHealthAsync(",
        "GetDaemonHealthAsync(",
        "PutDaemonCommandBoundaryAsync(",
        "GetDaemonCommandBoundaryAsync(",
        "PutDaemonFrameAsync(",
        "GetDaemonFrameAsync(",
        "PutDaemonSoaViewAsync(",
        "GetDaemonSoaViewAsync(",
        "PutDaemonGameSurfaceAsync(",
        "GetDaemonGameSurfaceAsync(",
        "PutDaemonGameTuiSurfaceAsync(",
        "GetDaemonGameTuiSurfaceAsync(",
        "PutDaemonEditorSurfaceAsync(",
        "GetDaemonEditorSurfaceAsync(",
        "PutDaemonEditorTuiSurfaceAsync(",
        "GetDaemonEditorTuiSurfaceAsync("
    };
    var missingDaemonNodeSymbols = requiredDaemonNodeSymbols
        .Where(symbol => !stateNode.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonNodeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaStateNode no longer exposes daemon Verse API records ergonomically: " +
            string.Join(", ", missingDaemonNodeSymbols));
    }

    if (!daemonSurfaceProjector.Contains("public static EveSurfaceState ToState(AetheriaRuntimeSurfaceDocument document)", StringComparison.Ordinal) ||
        !daemonSurfaceProjector.Contains("EveCommandTemplate", StringComparison.Ordinal) ||
        !daemonSurfaceProjector.Contains("EveSurfaceComponent", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Daemon Eve surface projector no longer lowers daemon surfaces into registered Eve surface state.");
    }

    if (!daemonPublicationStore.Contains("GetCommandBoundaryPath(", StringComparison.Ordinal) ||
        !daemonDocuments.Contains("AetheriaRuntimeDaemonCommandKinds", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon command boundary no longer exposes typed daemon command metadata.");
    }

    var forbiddenDaemonQueueSymbols = new[]
    {
        "public static AetheriaRuntimeDaemonCommandEnvelope QueueCommand(",
        "AetheriaRuntimeDaemonCommandLog.",
        "AetheriaRuntimeDaemonCommandLog.QueueCommand(",
        "var envelope = AetheriaRuntimeDaemonCommandLog.QueueCommand",
        ".daemon.pending",
        "ReadPending("
    };
    var daemonQueueSource = daemonDocuments + "\n" + daemonTickRunner + "\n" + daemonPublicationStore + "\n" + tests;
    var survivingDaemonQueueSymbols = forbiddenDaemonQueueSymbols
        .Where(symbol => daemonQueueSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingDaemonQueueSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon command boundary still exposes mailbox queue semantics: " +
            string.Join(", ", survivingDaemonQueueSymbols));
    }

    if (daemonDocuments.Contains("PendingBeforeApply", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon health still exposes the legacy PendingBeforeApply queue vocabulary instead of observed Verse command documents.");
    }

    var requiredProviderAdvertisementSymbols = new[]
    {
        "private const string DaemonCommandBoundaryId = \"aetheria.daemon.commands\"",
        "private const string DaemonWitnessTransport = \"cultcache-witness\"",
        "AetheriaRuntimeDaemonSchemas.ProviderAdvertisement",
        "AetheriaRuntimeDaemonSchemas.Frame",
        "AetheriaRuntimeDaemonSchemas.SoaView",
        "AetheriaRuntimeDaemonSchemas.Health",
        "AetheriaRuntimeDaemonSchemas.CommandBoundary",
        "AetheriaRuntimeDaemonSchemas.GameSurface",
        "AetheriaRuntimeDaemonSchemas.EditorSurface",
        "AetheriaRuntimeDaemonSchemas.Command",
        "AetheriaRuntimeStateBoundary.GetDaemonProviderPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonFramePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonHealthPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonCommandBoundaryPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonGameSurfacePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonGameTuiSurfacePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonEditorSurfacePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonEditorTuiSurfacePath(statePath)",
        "public const string DaemonGameSurfaceKey = \"eve:surface:aetheria.daemon.game\"",
        "public const string DaemonGameTuiSurfaceKey = \"eve:surface:aetheria.daemon.game.tui\"",
        "public const string DaemonEditorSurfaceKey = \"eve:surface:aetheria.daemon.editor\"",
        "public const string DaemonEditorTuiSurfaceKey = \"eve:surface:aetheria.daemon.editor.tui\"",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId"
    };
    var missingProviderAdvertisementSymbols = requiredProviderAdvertisementSymbols
        .Where(symbol => !providerAdvertisement.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingProviderAdvertisementSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Odin-visible provider advertisement no longer points at daemon-owned Verse witnesses: " +
            string.Join(", ", missingProviderAdvertisementSymbols));
    }

    var requiredTickSymbols = new[]
    {
        "VerseId",
        "CultMeshAddress",
        "AetheriaRuntimeDaemonCommandBoundaryDocument.Create",
        "AetheriaRuntimeDaemonProviderAdvertisementDocument.Create",
        "AetheriaRuntimeDaemonPublicationStore.PublishCommandBoundary",
        "AetheriaRuntimeDaemonPublicationStore.PublishProviderAdvertisement",
        "AetheriaRuntimeDaemonPublicationStore.PublishHealth",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.Build",
        "AetheriaRuntimeCatalogStore.ProjectStatRecipeSurfaceDocument(stateFilePath)",
        "AetheriaRuntimeDaemonPublicationStore.PublishGameSurface",
        "AetheriaRuntimeDaemonPublicationStore.PublishGameTuiSurface",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.Build",
        "AetheriaRuntimeDaemonPublicationStore.PublishEditorSurface",
        "AetheriaRuntimeDaemonPublicationStore.PublishEditorTuiSurface",
        "ObservedCommands",
        "AccountedCommandIds",
        "frame.AccountedCommandIds = accountedBeforeTick",
        "ObservedCommandCount = observedCommands.Length",
        "PublicationSource = \"daemon-published\""
    };
    var missingTickSymbols = requiredTickSymbols
        .Where(symbol => !daemonTickRunner.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTickSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon tick does not publish Verse-facing provider/health/command-boundary records: " +
            string.Join(", ", missingTickSymbols));
    }

    var requiredDaemonHostSymbols = new[]
    {
        "<OutputType>Exe</OutputType>",
        "ProjectReference Include=\"..\\Aetheria.State\\Aetheria.State.csproj\"",
        "AetheriaStateNode.OpenAsync(",
        "startServer: true",
        "new AetheriaVerseDiscoveryHost(node)",
        "discoveryHost.Update(",
        "PublishDaemonApiDocumentsAsync(node, result)",
        "PutDaemonFrameAsync(result.Frame)",
        "PutDaemonGameSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(gameSurface))",
        "PutDaemonEditorTuiSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(editorTuiSurface))",
        "AetheriaEveCommandBridge.AcceptObservedAsync(",
        "ReadObservedDaemonCommands()",
        "AccountedCommandIds = currentFrame?.AccountedCommandIds ?? Array.Empty<string>()",
        "AetheriaRuntimeDaemonTickRunner.Tick(",
        "AetheriaProviderAdvertisementProjector.Build(verseHost, node.StatePath, updatedAtUtc)",
        "AetheriaOperationsSurfaceProjector.Build(eveStatus, verseHost, runtimeSession)",
        "AetheriaPlayerSettingsSurfaceProjector.Build(playerSettings, playerSettingsUpdatedAt)",
        "Role = \"verse-daemon\"",
        "Console.CancelKeyPress",
        "Aetheria Verse daemon is running"
    };
    var daemonHostSource = daemonHostProject + "\n" + daemonHostProgram;
    var missingDaemonHostSymbols = requiredDaemonHostSymbols
        .Where(symbol => !daemonHostSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon no longer has Odin/VoidBot-shaped Verse daemon host authority: " +
            string.Join(", ", missingDaemonHostSymbols));
    }

    var forbiddenDaemonHostSymbols = new[]
    {
        "AetheriaRuntimeDaemonCommandLog.",
        "DeleteProcessedCommands",
        "new HttpListener",
        "UseUrls(",
        "MapGet(",
        "JsonSerializer.Serialize"
    };
    if (daemonHostSource.Contains("KeepAccountedCommandRecords", StringComparison.Ordinal) ||
        daemonHostSource.Contains("--keep-accounted-command-records", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon still exposes command-retention toggles after adopting receipt-ledger command accounting.");
    }

    if (stateNode.Contains("DeleteDaemonCommandAsync(", StringComparison.Ordinal) ||
        daemonHostSource.Contains("DeleteAccountedDaemonCommandsAsync", StringComparison.Ordinal) ||
        daemonHostSource.Contains("DeleteDaemonCommandAsync(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon command documents are still consumed by deletion instead of accounted by frame receipts.");
    }

    var survivingDaemonHostSymbols = forbiddenDaemonHostSymbols
        .Where(symbol => daemonHostSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingDaemonHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon has drifted into private command-log/dashboard ownership instead of CultMesh Verse publication: " +
            string.Join(", ", survivingDaemonHostSymbols));
    }

    var requiredGameSurfaceSymbols = new[]
    {
        "public const string SurfaceId = \"aetheria.game\"",
        "public const string TuiSurfaceId = \"aetheria.game.tui\"",
        "AetheriaRuntimeDaemonFrameDocument frame",
        "AetheriaRuntimeDaemonHealthDocument health",
        "AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary",
        "AetheriaRuntimeSurfaceStateRefs.SourceRef(stateRef)",
        "AetheriaRuntimeDaemonStateRefs.CurrentEntityName",
        "AetheriaRuntimeDaemonStateRefs.CurrentTargetName",
        "\"game.daemon\"",
        "\"Typed Command Boundary\"",
        "\"commandBody\"",
        "nameof(AetheriaRuntimeDaemonCommandDocument)",
        "AetheriaRuntimeDaemonCommandKinds.SetMoveVector"
    };
    var missingGameSurfaceSymbols = requiredGameSurfaceSymbols
        .Where(symbol => !daemonGameSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingGameSurfaceSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon game Eve surface builder is incomplete: " +
            string.Join(", ", missingGameSurfaceSymbols));
    }

    var requiredDaemonStateRefSymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonStateRefs",
        "public const string Prefix = \"aetheria.daemon\"",
        "public const string CurrentEntityName = Prefix + \"/current/entityName\"",
        "public const string CurrentTargetName = Prefix + \"/current/targetName\"",
        "public const string FrameId = Prefix + \"/frame/frameId\"",
        "public const string CommandCount = Prefix + \"/commands/count\""
    };
    var missingDaemonStateRefSymbols = requiredDaemonStateRefSymbols
        .Where(symbol => !daemonStateRefs.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonStateRefSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Eve surfaces no longer expose a shared typed state-ref vocabulary: " +
            string.Join(", ", missingDaemonStateRefSymbols));
    }

    var requiredSurfaceCommandCatalogSymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonSurfaceCommandCatalog",
        "public const string CommandPrefix = \"aetheria.daemon.commands.\"",
        "public static IReadOnlyList<AetheriaRuntimeDaemonCommandKinds> ArgumentlessCommands",
        "public static bool IsArgumentlessCommand(",
        "public static bool TrySubmitArgumentless(",
        "AetheriaRuntimeDaemonCommandKinds.SensorPing => client.SensorPing(observed)"
    };
    var missingSurfaceCommandCatalogSymbols = requiredSurfaceCommandCatalogSymbols
        .Where(symbol => !daemonSurfaceCommandCatalog.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSurfaceCommandCatalogSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon surface command catalog no longer owns the argumentless CultMesh command set: " +
            string.Join(", ", missingSurfaceCommandCatalogSymbols));
    }

    var surfaceCommandTemplateSources = new Dictionary<string, string>
    {
        ["daemon game surface"] = daemonGameSurfaceBuilder,
        ["daemon editor surface"] = daemonEditorSurfaceBuilder
    };
    var overBroadSurfaceCommandTemplates = surfaceCommandTemplateSources
        .Where(source =>
            !source.Value.Contains("AetheriaRuntimeDaemonSurfaceCommandCatalog.IsArgumentlessCommand(entry.Kind)", StringComparison.Ordinal) ||
            !source.Value.Contains("AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(entry.Kind)", StringComparison.Ordinal))
        .Select(source => source.Key)
        .ToArray();
    if (overBroadSurfaceCommandTemplates.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Eve surfaces still advertise command-boundary entries that are not directly submittable surface commands: " +
            string.Join(", ", overBroadSurfaceCommandTemplates));
    }

    var requiredEditorSurfaceSymbols = new[]
    {
        "public const string SurfaceId = \"aetheria.daemon.editor\"",
        "public const string TuiSurfaceId = \"aetheria.daemon.editor.tui\"",
        "AetheriaRuntimeDaemonProviderAdvertisementDocument provider",
        "AetheriaRuntimeDaemonHealthDocument health",
        "AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary",
        "\"editor.daemon\"",
        "\"Verse Provider\"",
        "\"Witnesses\"",
        "\"Designer Surfaces\"",
        "AetheriaRuntimeSurfaceDocument",
        "\"Typed Commands\"",
        "AetheriaRuntimeDaemonCommandBoundaryEntry"
    };
    var missingEditorSurfaceSymbols = requiredEditorSurfaceSymbols
        .Where(symbol => !daemonEditorSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingEditorSurfaceSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon editor Eve surface builder is incomplete: " +
            string.Join(", ", missingEditorSurfaceSymbols));
    }

    var requiredTestSymbols = new[]
    {
        "TryReadProviderAdvertisement(",
        "TryReadHealth(",
        "TryReadCommandBoundary(",
        "TryReadGameSurface(",
        "TryReadEditorSurface(",
        "TryReadGameTuiSurface(",
        "TryReadEditorTuiSurface(",
        "AetheriaRuntimeDaemonSchemas.ProviderAdvertisement",
        "AetheriaRuntimeDaemonSchemas.CommandBoundary",
        "AetheriaRuntimeDaemonSchemas.Health",
        "AetheriaRuntimeDaemonSchemas.GameSurface",
        "AetheriaRuntimeDaemonSchemas.EditorSurface",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeStatRecipeCommands.SurfaceId",
        "AetheriaRuntimeDaemonCommandKinds.TransferCargoItem",
        "nameof(AetheriaRuntimeCargoTransferCommand)"
    };
    var missingTestSymbols = requiredTestSymbols
        .Where(symbol => !tests.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTestSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Verse publication tests are incomplete: " +
            string.Join(", ", missingTestSymbols));
    }

    var requiredSmokeSymbols = new[]
    {
        "PutDaemonProviderAdvertisementAsync(daemonProvider)",
        "PutDaemonHealthAsync(daemonHealth)",
        "PutDaemonCommandBoundaryAsync(daemonCommandBoundary)",
        "AetheriaCommandPort.OpenAsync(",
        "daemonCommandPort.SubmitDaemonCommandAsync(",
        "eveCommandPort.SubmitEveCommandAsync(",
        "PutDaemonFrameAsync(daemonFrame)",
        "PutDaemonGameSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(daemonGameSurface))",
        "GetDaemonProviderAdvertisementAsync()",
        "GetDaemonHealthAsync()",
        "GetDaemonCommandBoundaryAsync()",
        "GetDaemonFrameAsync()",
        "GetDaemonGameSurfaceAsync()",
        "DaemonGameSurfaceKey",
        "DaemonGameTuiSurfaceKey",
        "DaemonEditorSurfaceKey",
        "DaemonEditorTuiSurfaceKey"
    };
    var missingSmokeSymbols = requiredSmokeSymbols
        .Where(symbol => !smoke.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSmokeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "State smoke no longer proves daemon Verse API records round-trip through CultCache: " +
            string.Join(", ", missingSmokeSymbols));
    }

    var requiredNoteSymbols = new[]
    {
        "Odin is the all-seer, not the owner.",
        "Idunn keeps the daemon alive from daemon-published health and command-boundary records.",
        "Bifrost hosts the MCP crossing for Codex and other xeno agents.",
        "The Aetheria daemon remains the side-effect owner.",
        "Witness-authoritative networking sharpens this rule.",
        "Unity should not be a dumb terminal;",
        "local projection caches, prediction state, render-native SoA views, and eventually immutable witness observations",
        "Observation, prediction, consensus candidates, and committed facts are separate stages.",
        "it must not mutate canonical Aetheria state or promote a local projection cache into authority.",
        "gamecult.aetheria.daemon_provider_advertisement.v1",
        "gamecult.aetheria.daemon_health.v1",
        "gamecult.aetheria.daemon_command_boundary.v1",
        "gamecult.aetheria.daemon_frame.v1",
        "gamecult.aetheria.daemon_soa_view.v1",
        "gamecult.aetheria.daemon_game_surface.v1",
        "gamecult.aetheria.daemon_editor_surface.v1",
        "gamecult.eve.provider_advertisement.v1` is the Odin-visible Eve provider card",
        "gamecult.aetheria.daemon_provider_advertisement.v1` is the daemon-owned Aetheria runtime contract",
        "The bridge layer must point at daemon-owned witnesses instead of becoming a second source of truth.",
        "Long term, Odin should discover the daemon-owned provider advertisement and interface bindings directly",
        "Queues are an implementation detail.",
        "Eve commands and daemon commands are typed `gamecult.eve.command.v1` and `gamecult.aetheria.daemon_command.v1` records in the Aetheria state graph",
        "`AetheriaCommandPort` is the neutral command submission implementation for typed command records.",
        "Client-side command lowerers may use typed runtime clients over that same port.",
        "`CommandLog`, `Inbox`, `mailbox`, `.eve.commands`, `.daemon.commands`, `.cc.pending`, or `" + "Pending" + "CultCacheStore` in Unity-facing daemon/Eve command code",
        "Do not add a private HTTP dashboard, Unity-only inspector, JSON status blob, or agent-specific daemon wrapper as canonical truth."
    };
    var missingNoteSymbols = requiredNoteSymbols
        .Where(symbol => !note.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingNoteSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon Verse shape note no longer records the Odin/VoidBot daemon authority contract: " +
            string.Join(", ", missingNoteSymbols));
    }
}

static void RequireTypedEveCommandBodies(string root)
{
    var eveCommandDocumentPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveCommandDocument.cs");
    var eveCommandClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveCommandClient.cs");
    var runtimeCommandPortPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCommandPort.cs");
    var eveCommandBridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var stateNodePath = Path.Combine(root, "Aetheria.State", "AetheriaStateNode.cs");
    var documentRegistryPath = Path.Combine(root, "Aetheria.State", "AetheriaDocumentRegistry.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var evePresenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");

    var requiredFiles = new[] { eveCommandDocumentPath, eveCommandClientPath, runtimeCommandPortPath, eveCommandBridgePath, stateNodePath, documentRegistryPath, actionGameManagerPath, mainMenuPath, evePresenterPath };
    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Typed Eve command bodies cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var eveCommandDocument = File.ReadAllText(eveCommandDocumentPath);
    var eveCommandClient = File.ReadAllText(eveCommandClientPath);
    var normalizedEveCommandClient = eveCommandClient.Replace("\r\n", "\n", StringComparison.Ordinal);
    var runtimeCommandPort = File.ReadAllText(runtimeCommandPortPath);
    var eveCommandBridge = File.ReadAllText(eveCommandBridgePath);
    var stateNode = File.ReadAllText(stateNodePath);
    var documentRegistry = File.ReadAllText(documentRegistryPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var evePresenter = File.ReadAllText(evePresenterPath);

    var requiredDocumentSymbols = new[]
    {
        "public enum AetheriaRuntimeEveCommandKind",
        "public sealed class AetheriaRuntimePlayerSettingsCommandBody",
        "public sealed class AetheriaRuntimeInputSettingsCommandBody",
        "public AetheriaRuntimeEveCommandKind Kind",
        "public AetheriaRuntimePlayerSettingsCommandBody PlayerSettings",
        "public AetheriaRuntimeInputSettingsCommandBody InputSettings",
        "public AetheriaRuntimeLoadoutTemplateCommit? LoadoutTemplate",
        "[CultDocument(\"gamecult.eve.command\", \"gamecult.eve.command.v1\")]"
    };
    var missingDocumentSymbols = requiredDocumentSymbols
        .Where(symbol => !eveCommandDocument.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve submitted command document is missing typed command bodies: " +
            string.Join(", ", missingDocumentSymbols));
    }

    var forbiddenDocumentSymbols = new[]
    {
        "IReadOnlyDictionary<string, string> Payload",
        "Dictionary<string, string> Payload",
        "SubmitSurfaceRequest(",
        "CreateSurfaceRequestCommand("
    };
    var forbiddenDocumentHits = forbiddenDocumentSymbols
        .Where(symbol => eveCommandDocument.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenDocumentHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve submitted command document still persists string payload maps: " +
            string.Join(", ", forbiddenDocumentHits));
    }

    var requiredTypedCommandSymbols = new[]
    {
        "public static class AetheriaRuntimeEveCommands",
        "public static class AetheriaRuntimeEveCommandClient",
        "public sealed class AetheriaRuntimeCommandPort",
        "public sealed class AetheriaCommandPort",
        "namespace Aetheria.State",
        "namespace GameCult.Aetheria.State.Verse",
        "global::Aetheria.State.AetheriaCommandPort",
        "public const string DefaultRuntimeId = \"aetheria-command-client\"",
        "string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId",
        "internal Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(",
        "internal Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(",
        "internal static class AetheriaRuntimeCommandSubmitter",
        "internal static bool TrySubmitEveCommand(",
        "internal static bool TrySubmitDaemonCommand(",
        "TrySubmitEveCommand(",
        "TrySubmitDaemonCommand(",
        "CreatePlayerSettingsCommand(",
        "CreateInputSettingsCommand(",
        "CreateCatalogCommand(",
        "CreateOperationsCommand(",
        "CreateVerseHostCommand(",
        "CreateLoadoutTemplateCommand(",
        "TrySendPlayerSettingsCommand(",
        "TrySendInputSettingsCommand(",
        "TrySendVerseHostCommand(",
        "TrySendLoadoutTemplateCommand(",
        "TrySendKnownSurfaceCommand(",
        "TryCreateKnownSurfaceCommand(",
        "CreateVerseHostCommand(CommandKindForSurface(request), clientId)",
        "CommandKindForSurface(",
        "CommandText(",
        "ToDocument(",
        "AcceptObservedAsync(",
        "switch (command.Kind)",
        "node.ReadObservedEveCommands()",
        "AccountedCommandIds",
        "SubmitEveCommandAsync(",
        "ReadObservedEveCommands(",
        "EveCommandKey(",
        "CultNetDocumentBinding.ForDocument<AetheriaRuntimeEveCommandDocument>",
        "typeof(AetheriaRuntimeEveCommandDocument)",
        "SubmitPlayerSettingsCommand(",
        "command.PlayerSettings.PlayerName",
        "command.InputSettings.ActionName",
        "command.InputSettings.InputSystemPath",
        "AetheriaRuntimeEveCommands.TrySendInputSettingsCommand(",
        "AetheriaRuntimeEveCommands.TrySendLoadoutTemplateCommand(",
        "AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand("
    };
    var mainMenu = File.ReadAllText(mainMenuPath);
    var typedCommandSources = eveCommandClient + "\n" + runtimeCommandPort + "\n" + eveCommandBridge + "\n" + stateNode + "\n" + documentRegistry + "\n" + actionGameManager + "\n" + mainMenu + "\n" + evePresenter;
    if (typedCommandSources.Contains("DeleteEveCommandAsync(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve command documents are still consumed by deletion instead of accounted by acceptance receipts.");
    }

    if (eveCommandBridge.Contains("AcceptedPaths", StringComparison.Ordinal) ||
        eveCommandBridge.Contains("RejectedPaths", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve command acceptance report still exposes path vocabulary instead of command-id receipts.");
    }

    var missingTypedCommandSymbols = requiredTypedCommandSymbols
        .Where(symbol => !typedCommandSources.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTypedCommandSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve command append/apply path is missing typed command body usage: " +
            string.Join(", ", missingTypedCommandSymbols));
    }

    var unityNamedRuntimeSources = new Dictionary<string, string>
    {
        ["Aetheria.State.Daemon"] = File.Exists(Path.Combine(root, "Aetheria.State.Daemon", "Program.cs"))
            ? File.ReadAllText(Path.Combine(root, "Aetheria.State.Daemon", "Program.cs"))
            : "",
        ["Aetheria.State"] = string.Join(
            "\n",
            Directory.EnumerateFiles(Path.Combine(root, "Aetheria.State"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)),
        ["org.gamecult.aetheria.state/Runtime"] = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime"),
                    "*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText))
    };
    var unityNamedRuntimeHits = unityNamedRuntimeSources
        .Where(source => source.Value.Contains("GameCult.Aetheria.State.Unity", StringComparison.Ordinal))
        .Select(source => source.Key)
        .ToArray();
    if (unityNamedRuntimeHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon/shared Aetheria runtime contracts still live under a Unity-named API namespace: " +
            string.Join(", ", unityNamedRuntimeHits));
    }

    var forbiddenSharedCommandRuntimeIds = new[]
    {
        "string runtimeId = \"unity-input-provider\"",
        "? \"unity-input-provider\" : runtimeId"
    };
    var sharedCommandRuntimeIdHits = forbiddenSharedCommandRuntimeIds
        .Where(symbol => runtimeCommandPort.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (sharedCommandRuntimeIdHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared command runtime still uses Unity-specific fallback runtime ids: " +
            string.Join(", ", sharedCommandRuntimeIdHits));
    }

    if (runtimeCommandPort.Contains("public static class AetheriaRuntimeCommandSubmitter", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared command runtime still exposes the generic transport submitter publicly instead of routing callers through typed clients.");
    }

    var forbiddenRuntimePortSubmitMethods = new[]
    {
        "public Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(",
        "public Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync("
    };
    var publicRuntimePortSubmitHits = forbiddenRuntimePortSubmitMethods
        .Where(symbol => runtimeCommandPort.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicRuntimePortSubmitHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime command port still exposes raw document submission publicly instead of routing callers through typed clients: " +
            string.Join(", ", publicRuntimePortSubmitHits));
    }

    var forbiddenPublicSubmitterMethods = new[]
    {
        "public static bool TrySubmitDaemonCommand(",
        "public static bool TrySubmitEveCommand("
    };
    var publicSubmitterMethodHits = forbiddenPublicSubmitterMethods
        .Where(symbol => runtimeCommandPort.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicSubmitterMethodHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime command submitter still marks generic submit helpers public instead of keeping them internal: " +
            string.Join(", ", publicSubmitterMethodHits));
    }

    var forbiddenUnitySubmitterSymbols = new[]
    {
        "AetheriaRuntimeCommandSubmitter.TrySubmitEveCommand(",
        "AetheriaRuntimeCommandSubmitter.TrySubmitDaemonCommand(",
        "AetheriaRuntimeEveCommandClient.ToDocument("
    };
    var unitySubmitterSources = new Dictionary<string, string>
    {
        [Path.GetRelativePath(root, actionGameManagerPath)] = actionGameManager,
        [Path.GetRelativePath(root, mainMenuPath)] = mainMenu,
        [Path.GetRelativePath(root, evePresenterPath)] = evePresenter
    };
    var unitySubmitterHits = unitySubmitterSources
        .SelectMany(pair => forbiddenUnitySubmitterSymbols
            .Where(symbol => pair.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{pair.Key}: {symbol}"))
        .ToArray();
    if (unitySubmitterHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity-facing Eve command code still assembles transport documents instead of using typed send helpers: " +
            string.Join(", ", unitySubmitterHits));
    }

    var forbiddenGenericSurfaceCommandSymbols = new[]
    {
        "SubmitSurfaceRequest(",
        "CreateSurfaceRequestCommand("
    };
    var genericSurfaceCommandHits = forbiddenGenericSurfaceCommandSymbols
        .Where(symbol => typedCommandSources.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (genericSurfaceCommandHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve command edge still exposes a generic surface-command submission back door: " +
            string.Join(", ", genericSurfaceCommandHits));
    }

    var forbiddenStringCommandOverloads = new[]
    {
        "SubmitPlayerSettingsCommand(\n            string stateFilePath,\n            string command",
        "SubmitInputSettingsCommand(\n            string stateFilePath,\n            string command",
        "SubmitCatalogCommand(\n            string stateFilePath,\n            string command",
        "SubmitOperationsCommand(\n            string stateFilePath,\n            string command",
        "SubmitVerseHostCommand(\n            string stateFilePath,\n            string command",
        "TrySendInputSettingsCommand(\n            string stateFilePath,\n            string command",
        "TrySendVerseHostCommand(\n            string stateFilePath,\n            string command",
        "CreateCatalogCommand(\n            string command",
        "CreateOperationsCommand(\n            string command",
        "CreateVerseHostCommand(\n            string command"
    };
    var survivingStringCommandOverloads = forbiddenStringCommandOverloads
        .Where(symbol => normalizedEveCommandClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingStringCommandOverloads.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Eve command client still exposes public string command overloads instead of typed command kinds: " +
            string.Join(", ", survivingStringCommandOverloads));
    }

    var forbiddenQueueSymbols = new[]
    {
        "public static class AetheriaRuntimeEveCommandLog",
        "internal static class AetheriaRuntimeEveCommandLog",
        "public static AetheriaRuntimeEveCommandEnvelope QueueCommand(",
        "public static AetheriaRuntimeEveCommandEnvelope QueuePlayerSettingsCommand(",
        "public static AetheriaRuntimeEveCommandEnvelope QueueInputSettingsCommand(",
        "public static AetheriaRuntimeEveCommandEnvelope QueueCatalogCommand(",
        "public static AetheriaRuntimeEveCommandEnvelope QueueOperationsCommand(",
        "public static AetheriaRuntimeEveCommandEnvelope QueueVerseHostCommand(",
        "public static AetheriaRuntimeEveCommandEnvelope QueueLoadoutTemplateCommand(",
        "private static AetheriaRuntimeEveCommandEnvelope QueueTypedCommand(",
        "AetheriaRuntimeEveCommandLog.",
        "AetheriaRuntimeEveCommandLog.Queue",
        "AetheriaRuntimeEveCommandInbox",
        "GetInboxDirectory(",
        "Read" + "Submitted(",
        ".eve.commands",
        "ReadPending(",
        ".eve.pending"
    };
    var forbiddenQueueSources = new Dictionary<string, string>
    {
        ["Eve command client"] = eveCommandClient,
        ["Eve command bridge"] = eveCommandBridge,
        ["Aetheria state node"] = stateNode,
        ["ActionGameManager"] = actionGameManager,
        ["MainMenu"] = mainMenu,
        ["Eve presenter"] = evePresenter
    };
    var survivingQueueSymbols = forbiddenQueueSources
        .SelectMany(source => forbiddenQueueSymbols
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{source.Key}: {symbol}"))
        .ToArray();
    if (survivingQueueSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve command edge still exposes queue-shaped public API: " +
            string.Join(", ", survivingQueueSymbols));
    }

    var rendererLogSources = new Dictionary<string, string>
    {
        ["ActionGameManager"] = actionGameManager,
        ["MainMenu"] = mainMenu,
        ["Eve presenter"] = evePresenter
    };
    var rendererLogHits = rendererLogSources
        .Where(source => source.Value.Contains("AetheriaRuntimeEveCommandLog.", StringComparison.Ordinal))
        .Select(source => $"{source.Key}: AetheriaRuntimeEveCommandLog.")
        .ToArray();
    if (rendererLogHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Renderer code still speaks to the Eve mailbox log instead of the typed command port: " +
            string.Join(", ", rendererLogHits));
    }

    if (actionGameManager.Contains("TryQueueRuntimeEveCommand(", StringComparison.Ordinal) ||
        actionGameManager.Contains("new EveSurfaceCommandRequest(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager still manufactures generic Eve surface command payloads instead of typed Eve command bodies.");
    }
}

static void RequireMainMenuVerseHostProjection(string root)
{
    var packageSnapshotPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs");
    var packageStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs");
    var runtimeStateReaderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateReader.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var mainMenuSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeMainMenuSurfaceBuilder.cs");

    var requiredFiles = new[]
    {
        packageSnapshotPath,
        packageStorePath,
        runtimeStateReaderPath,
        mainMenuPath,
        mainMenuSurfaceBuilderPath
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
    var mainMenuSurfaceBuilder = File.ReadAllText(mainMenuSurfaceBuilderPath);

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
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot("
    };
    var requiredBuilderSymbols = new[]
    {
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
    var missingBuilderSymbols = requiredBuilderSymbols
        .Where(symbol => !mainMenuSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared main-menu surface builder no longer lowers daemon-owned Verse identity through its Eve shell: " +
            string.Join(", ", missingBuilderSymbols));
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
        "LatestDaemonFrame",
        "AetheriaRuntimeStateReader",
        "TryReadDaemonFrame(stateBoot.StateFilePath, out var frame)",
        "frame.IsAuthoritative",
        "ContinueGame()",
        "ActionGameManager.ObservedGalaxy = Galaxy.ProjectObservedDaemonRun(",
        "SceneManager.LoadScene(\"ARPG\")"
    };

    var missingMenuSymbols = requiredMenuSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu Continue no longer selects an authoritative daemon frame: " +
            string.Join(", ", missingMenuSymbols));
    }

    if (mainMenu.Contains("AddButton(\"Continue\", null)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("MainMenu Continue regressed to a null button.");
    }

    var requiredGameplaySymbols = new[]
    {
        "ApplyLatestAuthoritativeDaemonFrame()",
        "TryRestoreEntityGraphFromDaemonRun(observed.Run)",
        "if (string.IsNullOrWhiteSpace(run.RunId))",
        "Authoritative daemon frame does not identify a run id.",
        "Authoritative daemon frame has no zone snapshot",
        "entity.RecordKey",
        "run.CurrentEntityKey",
        "CreateDaemonEntitySnapshots(runId, daemonZone)",
        "CanApplyDaemonEntitySnapshotsInPlace",
        "ZoneRenderer?.ApplyDaemonFrame(daemonZone, run)",
        "ApplyDaemonEntitySnapshotsInPlace",
        "PrepareObservedDaemonZoneContext(targetZone, daemonZone)",
        "_observedZoneContextsByDaemonIndex",
        "ReplaceObservedEntityFacadesFromTypedSnapshots",
        "RestoreCurrentEntityBinding",
        "RestoreCurrentEntityBinding(currentEntity, actionBarBindings)",
        "RestoreActiveConsumablesFromTypedEntitySnapshot(entity, entitySnapshot)",
        "RestoreRuntimeBehaviorStateFromTypedSnapshot(entity, entitySnapshot, restoredEntities)",
        "ResolveRuntimeBehavior(entity, weaponState.OwnerKind, weaponState.OwnerIndex, weaponState.BehaviorIndex)",
        "lockWeapon.RestoreRuntimeState(",
        "drive.RestoreRuntimeState(",
        "resourceScanner.RestoreRuntimeState(",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityContact(",
        "AetheriaRuntimeDaemonRenderQueries.QueryEntityContacts(",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityTarget(",
        "GetObservedTarget(CurrentEntity)",
        "ReconcileVisibleTargetIndicators();",
        "GetObservedInfoGathered(CurrentEntity, target)",
        "IsObservedHostileContact(CurrentEntity, observedTarget)",
        "_observedEntityFacadesByRecordKey",
        "_observedEntityFacadesByDaemonIndex",
        "RebuildObservedEntityFacadeIndex();",
        "ZoneRenderer?.LoadDaemonZoneView(_observedEntityFacadesByDaemonIndex, daemonZone, run)",
        "entity.RestoreStatGrids(entitySnapshot.StatGrids)",
        "RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia)",
        "entity.HeatsinksEnabled = entitySnapshot.HeatsinksEnabled",
        "RestoreDroppedPickupsFromDaemonZoneState",
        "TryGetDaemonEntitySnapshot(",
        "TryGetDaemonParentSnapshot(",
        "TryResolveDaemonDockingBay(",
        "IsCurrentEntityObservedUndocked(",
        "parentSnapshot.DockingBayAssignments",
        "ObservedAvailableEntities(",
        "TryGetObservedCurrentEntity(out var currentEntity)",
        "parentSnapshot.ChildEntityIndices",
        "TryGetObservedEntityFacade(childEntityIndex, out var entity)",
        "FindTypedRuntimeItem(",
        "RuntimeCatalog?.FindItem(item?.ItemKey ?? \"\")",
        "var typedItem = FindTypedRuntimeItem(item?.EquippableItem)",
        "var targetHull = FindTypedRuntimeItem(target.Hull)"
    };

    if (actionGameManager.Contains("_authoritativeDaemonEntities", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity entity wrappers are observed daemon facades, not authoritative state; keep the daemon as authority.");
    }

    if (actionGameManager.Contains("ItemManager.GetRuntimeItem", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager still resolves item metadata through Unity ItemManager instead of the runtime catalog.");
    }

    var missingGameplaySymbols = requiredGameplaySymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingGameplaySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer has the daemon-frame Continue boot path: " +
            string.Join(", ", missingGameplaySymbols));
    }

    var forbiddenGameplaySymbols = new[]
    {
        "if (run == null ||\r\n            run.CurrentZoneIndex < 0 ||\r\n            run.CurrentZoneEntityIndex < 0)",
        "var currentEntityKey = $\"{zoneEntityKeyPrefix}{run.CurrentZoneEntityIndex}.v1\"",
        "TargetVisibilityFill.fillAmount = Mathf.Lerp(.25f, .75f, (CurrentEntity.EntityInfoGathered[target] - threshold) / (1 - threshold));",
        "VisibilityToTargetFill.fillAmount = Mathf.Lerp(.25f, .75f, target.EntityInfoGathered[CurrentEntity] / threshold);",
        "indicator.Key.EntityInfoGathered[CurrentEntity]",
        "CurrentEntity.Target.Value.IsHostileTo(CurrentEntity)",
        "CurrentEntity.VisibleEnemies.ObserveAdd()",
        "CurrentEntity.VisibleEnemies.ObserveRemove()",
        "CurrentEntity.VisibleFriendlies.ObserveAdd()",
        "CurrentEntity.VisibleFriendlies.ObserveRemove()",
        "foreach (var hostile in CurrentEntity.VisibleEnemies)",
        "foreach (var friendly in CurrentEntity.VisibleFriendlies)",
        "dockedEntitySnapshot.ChildEntityIndices",
        "UpdateTargetPanel(CurrentEntity.Target.Value)",
        "var target = CurrentEntity.Target.Value;",
        "TargetIndicator.Target = CurrentEntity.Target.Value.Position",
        "indicator.Target = CurrentEntity.Target.Value.Position",
        "foreach (var bay in CurrentEntity.Parent.DockingBays)",
        "foreach (var entity in DockedEntity.Children)",
        "CurrentEntity != null && CurrentEntity.Parent == null",
        "CurrentEntity !=null && CurrentEntity.Parent==null",
        "CurrentEntity == null || CurrentEntity.Parent != null",
        "return observer?.Target.Value;",
        "observer.EntityInfoGathered.TryGetValue(target, out var infoGathered)",
        "return target != null && target.IsHostileTo(observer);",
        "RestoreEntityContactsFromTypedSnapshot",
        "entity.Target.Value = target",
        "entity.EntityInfoGathered[target]",
        "entity.EntityHostility[target]",
        "entity.VisibleEntities.Add(target)",
        "entity.VisibleEnemies.Add(target)",
        "entity.VisibleFriendlies.Add(target)",
        "currentEntity.Parent.DockingBays",
        "currentEntity.Parent is OrbitalEntity",
        "DoDock(currentEntity.Parent, dockingBay)",
        "RestoreChildAndDockingRelationships",
        "child.SetParent(parent)",
        "ship.SetParent(parent)",
        "dockingBay.DockedShip = ship",
        "Zone.Entities.Contains(ship)"
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

    var snapshotDocumentPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs");
    var snapshotDocument = File.Exists(snapshotDocumentPath)
        ? File.ReadAllText(snapshotDocumentPath)
        : throw new InvalidOperationException("Cannot verify daemon snapshot authority; AetheriaRuntimeSnapshotDocuments.cs is missing.");
    if (snapshotDocument.Contains("CurrentZoneEntityIndex", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon runtime snapshot transport still exposes integer current-entity slot authority.");
    }

    var forbiddenLegacyCommitSymbols = new[]
    {
        "public enum AetheriaRuntimeCommitKind",
        "public sealed class AetheriaRuntimeCommitEnvelope",
        "public sealed class AetheriaRuntimeStateCommitDocument",
        "internal enum AetheriaRuntimeCommitKind",
        "internal sealed class AetheriaRuntimeCommitEnvelope",
        "internal sealed class AetheriaRuntimeStateCommitDocument"
    };
    var legacyCommitHits = forbiddenLegacyCommitSymbols
        .Where(symbol => snapshotDocument.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (legacyCommitHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Legacy runtime commit envelope leaked back into the daemon snapshot contract: " +
            string.Join(", ", legacyCommitHits));
    }

    var forbiddenLegacyFiles = new[]
    {
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateCommitDocument.cs"),
        Path.Combine(root, "Aetheria.State", "AetheriaLegacyRuntimeSnapshotImporter.cs")
    };
    var existingLegacyFiles = forbiddenLegacyFiles
        .Where(File.Exists)
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (existingLegacyFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Legacy runtime commit compatibility files must stay deleted: " +
            string.Join(", ", existingLegacyFiles));
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

static void RequireUnityObserverDoesNotTickLocalSimulation(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var galaxyPath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Galaxy.cs");
    var daemonDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonDocuments.cs");
    var daemonOperationClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperationClient.cs");
    var daemonRuntimeOperationsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperations.cs");
    var daemonIntentPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonIntentState.cs");
    var daemonObserverPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonObserver.cs");
    var daemonGameplayOperationsPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonOperations.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; ActionGameManager.cs is missing.");
    var mainMenu = File.Exists(mainMenuPath)
        ? File.ReadAllText(mainMenuPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; MainMenu.cs is missing.");
    var galaxy = File.Exists(galaxyPath)
        ? File.ReadAllText(galaxyPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; Galaxy.cs is missing.");
    var daemonDocuments = File.Exists(daemonDocumentsPath)
        ? File.ReadAllText(daemonDocumentsPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; daemon command documents are missing.");
    var daemonOperationClient = File.Exists(daemonOperationClientPath)
        ? File.ReadAllText(daemonOperationClientPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; daemon operation client is missing.");
    var daemonOperationsSource = File.Exists(daemonRuntimeOperationsPath)
        ? File.ReadAllText(daemonRuntimeOperationsPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; daemon operations is missing.");
    var daemonIntent = File.Exists(daemonIntentPath)
        ? File.ReadAllText(daemonIntentPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; daemon intent state is missing.");
    var daemonObserver = File.Exists(daemonObserverPath)
        ? File.ReadAllText(daemonObserverPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaDaemonObserver.cs is missing.");
    var daemonOperations = File.Exists(daemonGameplayOperationsPath)
        ? File.ReadAllText(daemonGameplayOperationsPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaDaemonOperations.cs is missing.");

    var forbiddenGameplaySymbols = new[]
    {
        "CurrentGalaxy",
        "Zone.Update(Time.deltaTime)",
        "Zone.Update(",
        "CurrentEntity.Update(",
        ".Update(Time.deltaTime)",
        "Death.Subscribe(Die)",
        "private void Die(CauseOfDeath",
        "ZoneGenerator.GenerateZone",
        "TryDock(",
        "TryUndock(",
        "IntroCutscene(",
        "IntroDuration",
        "ship.CultPositionXZ =",
        "ship.CultDirection =",
        "ship.CultVelocity =",
        "CurrentEntity.Zone.Entities.Remove(CurrentEntity)",
        "CurrentEntity.Zone = Zone",
        "Zone.Entities.Add(CurrentEntity)",
        "CurrentEntity.CultPositionXZ = TowingStation.CultPositionXZ",
        "PopulateLevel(TowingStation.Zone.GalaxyZone)",
        "TowingStation.Zone?.GalaxyZone",
        "Zone.PlanetInstances.Values.FirstOrDefault",
        "Zone.TryGetOrbit(orbital.OrbitKey",
        "ZoneRenderer.Planets",
        "RestoreDaemonAsteroidRuntimeState",
        "belt.Damage.Clear()",
        "belt.RespawnTimers.Clear()",
        "AetheriaRuntimeStateCommitLog.QueueRunCheckpoint",
        "AetheriaRuntimeStateCommitLog.QueuePlayerSettings",
        "AetheriaRuntimeStateCommitLog.QueueLoadoutTemplate"
    };

    var forbiddenGameplayHits = forbiddenGameplaySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (forbiddenGameplayHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay still owns local simulation or legacy write fallback instead of daemon observation: " +
            string.Join(", ", forbiddenGameplayHits));
    }

    if (daemonObserver.Contains("AetheriaRuntimeCommandSubmitter.TrySubmitDaemonCommand(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity daemon observer still calls the generic command submitter instead of the typed daemon operation client.");
    }

    var requiredGameplaySymbols = new[]
    {
        "ApplyLatestAuthoritativeDaemonFrame()",
        "ResolveDaemonObserver()",
        "public bool TryGetObservedZoneSnapshot(int daemonZoneIndex, out AetheriaRuntimeZoneSnapshotCommit snapshot)",
        "public bool TryGetObservedRunZone(out GalaxyZone galaxyZone)",
        "snapshot.ZoneIndex == daemonZoneIndex",
        "FindObservedGalaxyZone(run.CurrentZoneIndex)",
        "observed.IsAuthoritative",
        "TryRestoreEntityGraphFromDaemonRun(observed.Run)",
        "CreateDaemonZoneConstructionBlueprint(daemonZone)",
        "PrepareObservedDaemonZoneContext(targetZone, daemonZone)",
        "_observedZoneContextsByDaemonIndex",
        "ZoneRenderer?.LoadDaemonZoneView(_observedEntityFacadesByDaemonIndex, daemonZone, run)",
        "ZoneRenderer?.ApplyDaemonFrame(daemonZone, run)",
        "CreateDaemonEntitySnapshots(runId, daemonZone)",
        "FindCurrentDaemonZoneSnapshot()",
        "daemonZone?.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>()",
        "ZoneRenderer.TryGetBodyView(parentOrbitPlanetBodyKey, out var parentBodyView)",
        "ResolveDaemonObserver()?.LastObservedState?.Run",
        "ReplaceObservedEntityFacadesFromTypedSnapshots",
        "EntityConstructionBlueprintProjector.ProjectObservedFromBlueprint",
        "public static Galaxy ObservedGalaxy",
        "TryRequestDaemonMoveVector",
        "TryRequestDaemonLookDirection",
        "TryRequestDaemonTractorPower",
        "observer.Operations.SetMoveVector",
        "observer.Operations.SetLookDirection",
        "observer.Operations.SetTractorPower"
    };

    var missingGameplaySymbols = requiredGameplaySymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingGameplaySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay no longer behaves as a daemon-frame observer and command lowerer: " +
            string.Join(", ", missingGameplaySymbols));
    }

    var mapRendererPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MapRenderer.cs");
    var mapRenderer = File.Exists(mapRendererPath)
        ? File.ReadAllText(mapRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon current-zone UI projection; MapRenderer.cs is missing.");
    var sectorRendererPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs");
    var sectorRenderer = File.Exists(sectorRendererPath)
        ? File.ReadAllText(sectorRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon current-zone UI projection; SectorRenderer.cs is missing.");
    var sectorMapPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorMap.cs");
    var sectorMap = File.Exists(sectorMapPath)
        ? File.ReadAllText(sectorMapPath)
        : throw new InvalidOperationException("Cannot verify daemon current-zone UI projection; SectorMap.cs is missing.");

    if (mapRenderer.Contains("GameManager.Zone.GalaxyZone", StringComparison.Ordinal) ||
        sectorRenderer.Contains("GameManager.Zone.GalaxyZone", StringComparison.Ordinal) ||
        sectorRenderer.Contains("GameManager.CurrentEntity.Zone.GalaxyZone", StringComparison.Ordinal) ||
        mapRenderer.Contains("GameManager.CurrentDaemonGalaxyZone", StringComparison.Ordinal) ||
        sectorRenderer.Contains("GameManager.CurrentDaemonGalaxyZone", StringComparison.Ordinal) ||
        mapRenderer.Contains("GameManager.ZoneRenderer", StringComparison.Ordinal) ||
        actionGameManager.Contains("public GalaxyZone CurrentDaemonGalaxyZone", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Map and sector UI must read current-zone identity from the daemon-observed run, not Unity's mirrored Zone hierarchy.");
    }

    if (!actionGameManager.Contains("public static bool TryGetObservedGalaxy(out Galaxy galaxy)", StringComparison.Ordinal) ||
        !actionGameManager.Contains("public bool TryGetObservedZoneSnapshot(int daemonZoneIndex, out AetheriaRuntimeZoneSnapshotCommit snapshot)", StringComparison.Ordinal) ||
        !actionGameManager.Contains("public bool TryGetObservedRunZone(out GalaxyZone galaxyZone)", StringComparison.Ordinal) ||
        !mapRenderer.Contains("GameManager.TryGetObservedRunZone(out var currentZone)", StringComparison.Ordinal) ||
        !sectorRenderer.Contains("GameManager.TryGetObservedRunZone(out var currentZone)", StringComparison.Ordinal) ||
        !sectorMap.Contains("ActionGameManager.TryGetObservedGalaxy(out var observedGalaxy)", StringComparison.Ordinal) ||
        !sectorRenderer.Contains("AetheriaRuntimeZoneDetailsSurfaceBuilder.ProjectDaemonZone(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Map and sector UI must request projected galaxy/zone data through explicit observed daemon boundaries.");
    }

    if (sectorMap.Contains("ActionGameManager.ObservedGalaxy", StringComparison.Ordinal) ||
        sectorRenderer.Contains("ActionGameManager.ObservedGalaxy", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Map and sector UI must not read the raw ObservedGalaxy projection directly.");
    }

    var requiredDaemonControlValidationSymbols = new[]
    {
        "case AetheriaRuntimeDaemonCommandKinds.SetTractorPower:",
        "!IsNormalizedScalar(command.ScalarValue)",
        "private static bool IsNormalizedScalar(double value)",
        "!IsFinite(command.DirectionX)",
        "!IsFinite(command.DirectionY)"
    };
    var missingDaemonControlValidationSymbols = requiredDaemonControlValidationSymbols
        .Where(symbol => !daemonOperationsSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonControlValidationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon continuous controls no longer validate normalized movement and tractor inputs: " +
            string.Join(", ", missingDaemonControlValidationSymbols));
    }

    if (actionGameManager.Contains("EntityConstructionBlueprintProjector.InstantiateAuthoritativeFromBlueprint", StringComparison.Ordinal) ||
        actionGameManager.Contains("EntityConstructionBlueprintProjector.InstantiateFromBlueprint", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity frame application must project observed daemon state, not instantiate authoritative gameplay entities.");
    }

    if (actionGameManager.Contains("galaxyZone.Contents", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity gameplay must not cache observed runtime zone context on the projected GalaxyZone model.");
    }

    if (actionGameManager.Contains("ObservedGalaxy.Zones[run.CurrentZoneIndex]", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity gameplay must resolve observed zones by daemon zone identity, not projected array slot.");
    }

    if (actionGameManager.Contains("FindDaemonZoneSnapshot(", StringComparison.Ordinal) ||
        sectorRenderer.Contains("FindDaemonZoneSnapshot(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity UI must query daemon zone snapshots by daemon zone identity instead of passing projected zone objects.");
    }

    if (galaxy.Contains("public Zone Contents", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Projected GalaxyZone must not own Unity runtime Zone contents; observed zone context belongs to the client lowerer.");
    }

    var requiredObservedZoneIdentitySymbols = new[]
    {
        "public int ZoneIndex = -1;",
        "ZoneIndex = zone.ZoneIndex,",
        "new GalaxyZone { ZoneIndex = index, Position = v }"
    };
    var missingObservedZoneIdentitySymbols = requiredObservedZoneIdentitySymbols
        .Where(symbol => !galaxy.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedZoneIdentitySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Projected GalaxyZone no longer preserves daemon zone identity: " +
            string.Join(", ", missingObservedZoneIdentitySymbols));
    }

    var contextPrepRendererLoads = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "LoadDaemonZoneView(", "BindToEntity(" })
        .Where(hit => hit.MethodName == "PrepareObservedDaemonZoneContext")
        .Select(hit => $"{hit.MethodName}:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (contextPrepRendererLoads.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity zone context prep must not load renderer instances before observed daemon facades have been rebuilt: " +
            string.Join("; ", contextPrepRendererLoads));
    }

    var unauthorizedEntityProjectionHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "Zone.Entities.Add(", "Zone.Entities.Remove(", "Zone.Agents.Clear()" })
        .Select(hit => $"{hit.MethodName}:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (unauthorizedEntityProjectionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay must not rebuild renderer-local Zone entity membership while lowering daemon snapshots: " +
            string.Join("; ", unauthorizedEntityProjectionHits));
    }

    var zonePath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "Zone.cs");
    var zoneSource = File.Exists(zonePath)
        ? File.ReadAllText(zonePath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; Zone.cs is missing.");
    var projectorPath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintProjector.cs");
    var projectorSource = File.Exists(projectorPath)
        ? File.ReadAllText(projectorPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; EntityConstructionBlueprintProjector.cs is missing.");

    var requiredProjectionBoundarySymbols = new[]
    {
        "public static Galaxy ProjectObservedDaemonRun",
        "private Galaxy(",
        "AetheriaRuntimeRunCheckpointCommit run,",
        "public static Entity InstantiateAuthoritativeFromBlueprint",
        "public static Entity ProjectObservedFromBlueprint",
        "private static Entity BuildFromBlueprint",
        "EntityConstructionBlueprintProjector.InstantiateAuthoritativeFromBlueprint(_itemManager, this, entityBlueprint)"
    };
    var projectionBoundaryCorpus = projectorSource + "\n" + zoneSource + "\n" + galaxy;
    var missingProjectionBoundarySymbols = requiredProjectionBoundarySymbols
        .Where(symbol => !projectionBoundaryCorpus.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingProjectionBoundarySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Entity projection no longer separates authoritative construction from observed daemon lowering: " +
            string.Join(", ", missingProjectionBoundarySymbols));
    }

    if (galaxy.Contains("public Galaxy(\r\n        AetheriaRuntimeRunCheckpointCommit run,", StringComparison.Ordinal) ||
        galaxy.Contains("public Galaxy(\n        AetheriaRuntimeRunCheckpointCommit run,", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon-observed galaxy projection must be reached through ProjectObservedDaemonRun, not a public constructor.");
    }

    var forbiddenDaemonQueueLanguage = new[]
    {
        "observer.Queue",
        "_lastQueuedDaemon",
        "_hasQueuedDaemon",
        "Failed to queue Aetheria daemon",
        "Queued Aetheria daemon"
    };
    var forbiddenDaemonQueueHits = forbiddenDaemonQueueLanguage
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenDaemonQueueHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay still uses queue-shaped daemon language instead of typed sent operations: " +
            string.Join(", ", forbiddenDaemonQueueHits));
    }

    if (daemonObserver.Contains("public AetheriaRuntimeDaemonCommandEnvelope Queue", StringComparison.Ordinal) ||
        daemonObserver.Contains("Queued Aetheria daemon command", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaDaemonObserver still exposes queue-shaped public daemon APIs instead of typed Operations.");
    }

    var requiredDaemonNavigationAuthoritySymbols = new[]
    {
        "ApplyEnterWormholeIntent(run, command, context.Intents)",
        "ApplyTowToStationIntent(run, command, context.Intents)",
        "MoveEntityToZone(run, actor, command.TargetZoneIndex",
        "run.CurrentZoneIndex = targetZoneIndex",
        "run.CurrentEntityKey = movedEntityKey",
        "run.DiscoveredZoneIndices = discovered.ToArray()"
    };
    var missingDaemonNavigationAuthoritySymbols = requiredDaemonNavigationAuthoritySymbols
        .Where(symbol => !daemonOperationsSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonNavigationAuthoritySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon navigation commands no longer mutate canonical run state inside the daemon operation layer: " +
            string.Join(", ", missingDaemonNavigationAuthoritySymbols));
    }

    var requiredSentOperationSymbols = new[]
    {
        "_lastSentDaemonMoveVector",
        "_lastSentDaemonLookDirection",
        "_lastSentDaemonTractorPower",
        "_hasSentDaemonMoveVector",
        "_hasSentDaemonLookDirection",
        "_hasSentDaemonTractorPower",
        "Failed to send Aetheria daemon movement operation",
        "Failed to send Aetheria daemon look operation",
        "Failed to send Aetheria daemon tractor-power operation"
    };
    var missingSentOperationSymbols = requiredSentOperationSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSentOperationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay no longer describes daemon input as sent typed operations: " +
            string.Join(", ", missingSentOperationSymbols));
    }

    var requiredDockingLoweringSymbols = new[]
    {
        "public void RequestDock()",
        "public void RequestUndock()",
        "private void RequestInteract()",
        "public void RequestTowToStation()",
        "TryRequestDaemonDock()",
        "TryRequestDaemonUndock()",
        "TryRequestDaemonInteract()",
        "TryRequestDaemonTowToStation()",
        "observer.Operations.DockNearest(Settings.GameplaySettings.DockingDistance)",
        "observer.Operations.Undock()",
        "observer.Operations.Interact(",
        "observer.Operations.TowToStation(",
        "ResolveObservedEntityZoneIndex(TowingStation)",
        "TowingStation.CultPositionXZ.x",
        "TowingStation.CultPositionXZ.y"
    };

    var missingDockingLoweringSymbols = requiredDockingLoweringSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingDockingLoweringSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity docking/towing UI no longer lowers intent through daemon commands: " +
            string.Join(", ", missingDockingLoweringSymbols));
    }

    var forbiddenUnityDockingAuthoritySymbols = new[]
    {
        "public void Dock()",
        "public void Undock()",
        "public void TowShip()",
        "Missing cockpit component",
        "Missing thruster component",
        "Missing reactor component",
        "Must empty docking bay",
        "ZoneRenderer.WormholeInstances.Keys",
        "RequestEnterWormhole(wormhole);"
    };
    var unityDockingAuthorityHits = forbiddenUnityDockingAuthoritySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityDockingAuthorityHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity docking/towing still exposes imperative local authority instead of daemon-bound requests: " +
            string.Join(", ", unityDockingAuthorityHits));
    }

    var undockLocalAuthorityHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "CurrentEntity?.Parent == null" })
        .Where(hit => hit.MethodName == "TryRequestDaemonUndock")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (undockLocalAuthorityHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity undock request still rejects through renderer-local parent state instead of daemon acceptance: " +
            string.Join("; ", undockLocalAuthorityHits));
    }

    var dockLocalAuthorityHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "CurrentEntity?.Parent != null" })
        .Where(hit => hit.MethodName == "TryRequestDaemonDock")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (dockLocalAuthorityHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity dock request still rejects through renderer-local parent state instead of daemon acceptance: " +
            string.Join("; ", dockLocalAuthorityHits));
    }

    var dockTargetSelectionHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "FindDaemonDockTarget", "Zone.Entities", "CultPositionXZ - CurrentEntity.CultPositionXZ" })
        .Where(hit => hit.MethodName == "TryRequestDaemonDock" || hit.MethodName == "FindDaemonDockTarget")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (dockTargetSelectionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity dock request still selects docking targets from renderer-local zone state instead of daemon acceptance: " +
            string.Join("; ", dockTargetSelectionHits));
    }

    var requiredDaemonDockNearestSymbols = new[]
    {
        "AetheriaRuntimeDaemonCommandKinds.DockNearest",
        "AetheriaRuntimeDaemonCommandKinds.Interact",
        "public AetheriaRuntimeDaemonCommandEnvelope DockNearest(",
        "public AetheriaRuntimeDaemonCommandEnvelope Interact(",
        "ApplyDockNearestIntent(run, command, context.Intents)",
        "ApplyInteractIntent(run, command, context.Intents)",
        "TryFindNearestDockTarget(run, actorKey, command.ScalarValue, out var targetKey)",
        "TryFindNearestWormholeTarget("
    };
    var daemonDockNearestSources = daemonDocuments + "\n" + daemonOperationClient + "\n" + daemonOperationsSource;
    var missingDaemonDockNearestSymbols = requiredDaemonDockNearestSymbols
        .Where(symbol => !daemonDockNearestSources.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonDockNearestSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon no longer owns nearest docking target selection: " +
            string.Join(", ", missingDaemonDockNearestSymbols));
    }

    var movementLocalAuthorityHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "CurrentEntity?.Parent != null" })
        .Where(hit => hit.MethodName == "TryRequestDaemonEnterWormhole" || hit.MethodName == "TryRequestDaemonTowToStation")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (movementLocalAuthorityHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity movement requests still reject through renderer-local parent state instead of daemon acceptance: " +
            string.Join("; ", movementLocalAuthorityHits));
    }

    var requiredShieldToggleSymbols = new[]
    {
        "TryRequestDaemonShieldToggle",
        "observer.Operations.ToggleShieldEnabled()",
        "AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled",
        "ApplyToggleEquipmentBehaviorItem(run, command, \"Shield\""
    };
    var missingShieldToggleSymbols = requiredShieldToggleSymbols
        .Where(symbol =>
            !actionGameManager.Contains(symbol, StringComparison.Ordinal) &&
            !daemonOperationsSource.Contains(symbol, StringComparison.Ordinal) &&
            !daemonDocuments.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingShieldToggleSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shield input no longer lowers through a semantic daemon shield command: " +
            string.Join(", ", missingShieldToggleSymbols));
    }

    var forbiddenUnityShieldAuthoritySymbols = new[]
    {
        "RequestShieldEnabled(",
        "TryRequestDaemonShieldEnabled(",
        "CurrentEntity.Shield.Item.Enabled",
        "var shieldItem = CurrentEntity?.Shield?.Item",
        "CurrentEntity.Equipment.IndexOf(shieldItem)"
    };
    var shieldAuthorityHits = forbiddenUnityShieldAuthoritySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (shieldAuthorityHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity shield input still derives equipment authority from renderer-local shield components: " +
            string.Join(", ", shieldAuthorityHits));
    }

    var forbiddenActionBarIntentAcceptanceSymbols = new[]
    {
        "observer == null || !observer.HasAuthoritativeState || equipmentIndex < 0 || behaviorIndex < 0",
        "observer == null || !observer.HasAuthoritativeState || weaponGroup < 0"
    };
    var actionBarIntentAcceptanceHits = forbiddenActionBarIntentAcceptanceSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (actionBarIntentAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity action-bar intent requests still reject through renderer-local index checks instead of daemon authority: " +
            string.Join(", ", actionBarIntentAcceptanceHits));
    }

    var requiredTargetAuthoritySymbols = new[]
    {
        "ApplySetTarget(run, command)",
        "ApplyTargetCycle(run, command, TargetCycleMode.Nearest)",
        "ApplyTargetCycle(run, command, TargetCycleMode.Next)",
        "ApplyTargetCycle(run, command, TargetCycleMode.Previous)",
        "ApplyTargetReticle(run, command)",
        "VisibleHostileTargets(zone, actor, actorIndex)",
        "actor.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>()",
        "contact.TargetEntityIndex == targetIndex && contact.Visible"
    };
    var missingTargetAuthoritySymbols = requiredTargetAuthoritySymbols
        .Where(symbol => !daemonOperationsSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTargetAuthoritySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon target selection no longer validates visibility through authoritative contact state: " +
            string.Join(", ", missingTargetAuthoritySymbols));
    }

    var requiredUnityTargetRequestSymbols = new[]
    {
        "RequestTargetNearest()",
        "RequestTargetNext()",
        "RequestTargetPrevious()",
        "RequestTargetReticle()",
        "observer.Operations.TargetNearest()",
        "observer.Operations.TargetNext()",
        "observer.Operations.TargetPrevious()",
        "observer.Operations.TargetReticle("
    };
    var unityTargetRequestSources = actionGameManager + "\n" + daemonOperations;
    var missingUnityTargetRequestSymbols = requiredUnityTargetRequestSymbols
        .Where(symbol => !unityTargetRequestSources.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingUnityTargetRequestSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity target cycling no longer lowers through typed daemon requests: " +
            string.Join(", ", missingUnityTargetRequestSymbols));
    }

    var forbiddenUnityTargetCycleSymbols = new[]
    {
        ".MaxBy(x => CultMath.math.length(x.CultPosition - CurrentEntity.CultPosition))",
        ".OrderBy(x => CultMath.math.length(x.CultPosition - CurrentEntity.CultPosition))",
        ".MaxBy(x => CultMath.math.dot(",
        "Array.IndexOf(targets"
    };
    var unityTargetCycleHits = forbiddenUnityTargetCycleSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityTargetCycleHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity target cycling still orders or selects targets from renderer-local entity state instead of daemon contacts: " +
            string.Join(", ", unityTargetCycleHits));
    }

    var requiredIntentAuthoritySymbols = new[]
    {
        "!HasWeaponGroup(entity, command.WeaponGroup)",
        "!HasEquipmentBehavior(entity, command.EquipmentIndex, command.BehaviorIndex)",
        "private static bool HasWeaponGroup(AetheriaRuntimeEntitySnapshotCommit entity, int weaponGroup)",
        "private static bool HasEquipmentBehavior("
    };
    var missingIntentAuthoritySymbols = requiredIntentAuthoritySymbols
        .Where(symbol => !daemonOperationsSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingIntentAuthoritySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon simulation intents no longer validate weapon-group and behavior indices against authoritative entity state: " +
            string.Join(", ", missingIntentAuthoritySymbols));
    }

    var forbiddenUnityInputApplySymbols = new[]
    {
        "ApplyEnterWormhole(",
        "ApplyLookDirection(",
        "ApplyTractorPower(",
        "ApplyTargetSelection(",
        "ApplyOverrideShutdown(",
        "ApplySensorPing(",
        "ApplyHeatsinksEnabled(",
        "ApplyShieldEnabled(",
        "ApplyDock(",
        "ApplyUndock("
    };
    var unityInputApplyHits = forbiddenUnityInputApplySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityInputApplyHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity input still exposes Apply vocabulary for daemon-bound requests: " +
            string.Join(", ", unityInputApplyHits));
    }

    var requiredTowDaemonSymbols = new[]
    {
        "TowToStation",
        "AetheriaRuntimeDaemonCommandKinds.TowToStation",
        "ApplyTowToStationIntent",
        "MoveEntityToZone(run, actor, command.TargetZoneIndex, command.PositionX, command.PositionY, out var movedEntityKey)",
        "intents.Towing.Add",
        "AetheriaRuntimeDaemonTowIntent",
        "public AetheriaRuntimeDaemonCommandEnvelope TowToStation("
    };

    var missingTowDaemonSymbols = requiredTowDaemonSymbols
        .Where(symbol =>
            !daemonDocuments.Contains(symbol, StringComparison.Ordinal) &&
            !daemonOperationsSource.Contains(symbol, StringComparison.Ordinal) &&
            !daemonIntent.Contains(symbol, StringComparison.Ordinal) &&
            !daemonObserver.Contains(symbol, StringComparison.Ordinal) &&
            !daemonOperations.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingTowDaemonSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Towing is not fully owned by daemon command state: " +
            string.Join(", ", missingTowDaemonSymbols));
    }

    var forbiddenMenuSymbols = new[]
    {
        "new Galaxy(Settings",
        "new Galaxy(settings",
        "new Galaxy(",
        "SectorGenerationSettings",
        "RunGenerator",
        "ZoneGenerator.GenerateZone"
    };

    var forbiddenMenuHits = forbiddenMenuSymbols
        .Where(symbol => mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (forbiddenMenuHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still has local run generation fallback on the gameplay boot path: " +
            string.Join(", ", forbiddenMenuHits));
    }

    var requiredMenuSymbols = new[]
    {
        "TryStartDaemonObservedGame",
        "TryReadDaemonFrame(stateBoot.StateFilePath, out var frame)",
        "frame.IsAuthoritative",
        "ActionGameManager.ObservedGalaxy = Galaxy.ProjectObservedDaemonRun(",
        "frame.Run",
        "SceneManager.LoadScene(\"ARPG\")"
    };

    var missingMenuSymbols = requiredMenuSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu no longer boots gameplay strictly from an authoritative daemon frame: " +
            string.Join(", ", missingMenuSymbols));
    }
}

static void RequireUnityDoesNotCallSharedSimulationTicks(string root)
{
    var checkedRoots = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay"),
        Path.Combine(root, "Assets", "Scripts", "UI")
    };

    var forbiddenCallerSymbols = new[]
    {
        "Zone.Update(",
        ".Zone.Update(",
        "CurrentEntity.Update(",
        ".CurrentEntity.Update(",
        "entity.Update(delta",
        "entity.Update(Time.deltaTime",
        "ship.Update(delta",
        "ship.Update(Time.deltaTime",
        "agent.Update(delta",
        "agent.Update(Time.deltaTime",
        "equippedItem.Update(delta",
        "equippedItem.Update(Time.deltaTime",
        "alwaysUpdatedBehavior.Update(delta",
        "alwaysUpdatedBehavior.Update(Time.deltaTime"
    };

    var hits = checkedRoots
        .Where(Directory.Exists)
        .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenCallerSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay/UI still calls shared simulation tick methods instead of observing daemon frames: " +
            string.Join("; ", hits));
    }

    var daemonTickRunnerPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeDaemonTickRunner.cs");
    var daemonTickRunner = File.Exists(daemonTickRunnerPath)
        ? File.ReadAllText(daemonTickRunnerPath)
        : throw new InvalidOperationException("Cannot verify shared simulation authority; daemon tick runner is missing.");

    var requiredDaemonTickSymbols = new[]
    {
        "AetheriaRuntimeDaemonTickRunner",
        "Tick(",
        "AetheriaRuntimeDaemonFrameDocument.Create",
        "AetheriaRuntimeDaemonFrameStore.PublishFrame",
        "AetheriaRuntimeDaemonOperations.Execute("
    };

    var missingDaemonTickSymbols = requiredDaemonTickSymbols
        .Where(symbol => !daemonTickRunner.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingDaemonTickSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared simulation ticks no longer have a daemon-owned frame publication path: " +
            string.Join(", ", missingDaemonTickSymbols));
    }
}

static void RequireUnityPhysicsIsNotGameplayAuthority(string root)
{
    var checkedRoots = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay"),
        Path.Combine(root, "Assets", "Scripts", "ServerShared")
    };

    var forbiddenSymbols = new[]
    {
        "OnTriggerEnter",
        "OnTriggerStay",
        "OnTriggerExit",
        "OnCollisionEnter",
        "OnCollisionStay",
        "OnCollisionExit",
        "OnControllerColliderHit",
        "Physics.Raycast",
        "Physics.SphereCast",
        "Physics.BoxCast",
        "Physics.CapsuleCast",
        "Physics.OverlapSphere",
        "Physics.OverlapBox",
        "Physics.OverlapCapsule",
        "Physics.CheckSphere",
        "Physics.CheckBox",
        "Physics.CheckCapsule",
        "Rigidbody",
        "Rigidbody2D",
        "Collider2D"
    };

    var hits = checkedRoots
        .Where(Directory.Exists)
        .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity physics/collision callbacks are forbidden as gameplay authority; route queries through Ymir: " +
            string.Join("; ", hits));
    }

    var ymirBridgePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Physics", "AetheriaYmirPhysicsBridge.cs");
    var projectilePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "Projectile.cs");
    var projectileManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "ProjectileManager.cs");
    var guidedProjectilePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "GuidedProjectile.cs");
    var hitscanPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "HitscanEffect.cs");
    var hitscanManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "HitscanManager.cs");
    var clickRaycasterPath = Path.Combine(root, "Assets", "Scripts", "UI", "ClickRaycaster.cs");

    var requiredSymbols = new Dictionary<string, string[]>
    {
        [ymirBridgePath] = new[]
        {
            "public string ServiceUrl",
            "public string OverlapSphereUrl",
            "public string OverlapCircleUrl",
            "public string CastCircleUrl",
            "public string CastSphereUrl",
            "TryStepProjectile(",
            "TryCastZoneHulls(",
            "TryBuildDaemonWorld("
        },
        [projectilePath] = new[]
        {
            "AetheriaYmirPhysicsBridge.Instance.TryStepProjectile",
            "projectile killed instead of falling back to Unity physics."
        },
        [guidedProjectilePath] = new[]
        {
            "AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls"
        },
        [hitscanPath] = new[]
        {
            "AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls"
        },
        [clickRaycasterPath] = new[]
        {
            "AetheriaYmirPhysicsBridge.Instance.TryCastClickables"
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
            "Ymir gameplay query bridge is incomplete: " +
            string.Join("; ", missingSymbols));
    }

    var forbiddenWeaponZoneHandles = new Dictionary<string, string[]>
    {
        [projectilePath] = new[] { "public Zone Zone", "Zone { get; set; }" },
        [projectileManagerPath] = new[] { "p.Zone =", "source.Entity.Zone" },
        [hitscanPath] = new[] { "public Zone Zone", "Zone { get; set; }" },
        [hitscanManagerPath] = new[] { "p.Zone =", "source.Entity.Zone" }
    };
    var weaponZoneHandleHits = forbiddenWeaponZoneHandles
        .Where(pair => File.Exists(pair.Key))
        .SelectMany(pair =>
        {
            var text = File.ReadAllText(pair.Key);
            return pair.Value
                .Where(symbol => text.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, pair.Key)}: {symbol}");
        })
        .ToArray();

    if (weaponZoneHandleHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity weapon effects must query Ymir/renderer state directly instead of carrying renderer-local Zone handles: " +
            string.Join("; ", weaponZoneHandleHits));
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
    var cargoBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.cs");
    var equippedBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.cs");
    var tradeItemBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeTradeItemDetailsSurfaceBuilder.cs");
    var cargoBuilder = File.Exists(cargoBuilderPath)
        ? File.ReadAllText(cargoBuilderPath)
        : throw new InvalidOperationException("Cannot verify typed behavior metadata coverage; cargo item details builder is missing.");
    var equippedBuilder = File.Exists(equippedBuilderPath)
        ? File.ReadAllText(equippedBuilderPath)
        : throw new InvalidOperationException("Cannot verify typed behavior metadata coverage; equipped item details builder is missing.");
    var tradeItemBuilder = File.Exists(tradeItemBuilderPath)
        ? File.ReadAllText(tradeItemBuilderPath)
        : throw new InvalidOperationException("Cannot verify typed behavior metadata coverage; trade item details builder is missing.");

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

    var inventoryBehaviorProjection = cargoBuilder + "\n" + equippedBuilder;
    var missingInventorySymbols = requiredUiSymbols
        .Where(symbol => !inventoryBehaviorProjection.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingInventorySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory item surface builders no longer render typed temperature-bearing behavior metadata: " +
            string.Join(", ", missingInventorySymbols));
    }

    var missingTradeSymbols = requiredUiSymbols
        .Where(symbol => !tradeItemBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTradeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Trade item surface builder no longer renders typed temperature-bearing behavior metadata: " +
            string.Join(", ", missingTradeSymbols));
    }

    if (inventoryMenu.Contains("ProjectCargoItemBehaviorMetric(", StringComparison.Ordinal) ||
        inventoryMenu.Contains("ProjectEquippedItemBehaviorMetric(", StringComparison.Ordinal) ||
        tradeMenu.Contains("ProjectTradeItemBehaviorMetric(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity menus must not own typed behavior metric projection after runtime surface builders take that role.");
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
    var runtimeEveSurfaceAdapterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveSurfaceAdapter.cs");
    var unityPackageProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var eveSurfacePresenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");

    var requiredPaths = new[]
    {
        runtimeStateReaderPath,
        runtimeEveSurfaceAdapterPath,
        unityPackageProjectPath,
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
    var runtimeEveSurfaceAdapter = File.ReadAllText(runtimeEveSurfaceAdapterPath);
    var unityPackageProject = File.ReadAllText(unityPackageProjectPath);
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
        "ReadEveSurface",
        "TryReadDaemonGameSurface",
        "TryReadDaemonGameTuiSurface",
        "TryReadDaemonEditorSurface",
        "TryReadDaemonEditorTuiSurface",
        "ResolveEveSurfaceStateRef",
        "TryResolveEveSurfaceStateRef",
        "TryResolveDaemonStateRef",
        "TryResolveDaemonItemStatRef",
        "AetheriaRuntimeDaemonStateRefs.Prefix",
        "AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix",
        "AetheriaRuntimeDaemonStateRefs.CurrentEntityName",
        "AetheriaRuntimeDaemonPublicationStore.TryReadGameSurface",
        "AetheriaRuntimeDaemonPublicationStore.TryReadGameTuiSurface",
        "AetheriaRuntimeDaemonPublicationStore.TryReadEditorSurface",
        "AetheriaRuntimeDaemonPublicationStore.TryReadEditorTuiSurface",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId",
        "ToResolvedEveSurfaceDocument(stateFilePath, surface)",
        "ResolveSurfaceStateRefs(stateFilePath, surface)",
        "public static Func<string, string> CreateEveSurfaceStateRefResolver(string stateFilePath)",
        "CreateStateRefResolver(string stateFilePath)",
        "FindDaemonItem(",
        "AetheriaRuntimeEveSurfaceAdapter.ResolveStateRefs(",
        "AetheriaRuntimeEveSurfaceAdapter.EmptySurface(surfaceId)"
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

    if (!unityPackageProject.Contains("AetheriaRuntimeStateReader.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "GameCult.Aetheria.State.Unity.csproj does not include the shared runtime state reader.");
    }

    if (!unityPackageProject.Contains("AetheriaRuntimeDaemonStateRefs.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "GameCult.Aetheria.State.Unity.csproj does not include the shared daemon state-ref vocabulary.");
    }

    var requiredAdapterSymbols = new[]
    {
        "public static class AetheriaRuntimeEveSurfaceAdapter",
        "public static EveSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)",
        "public static EveSurfaceDocument ResolveStateRefs(",
        "ResolvePropRefs(props, stateRefResolver)",
        "ResolvePropRef(props, AetheriaRuntimeSurfaceStateRefs.Source, \"value\", stateRefResolver)",
        "IsStatePointerProp(prop.Key)",
        "ResolvePointerValueKey(refProp.Key)",
        "public static EveSurfaceDocument EmptySurface(string surfaceId)",
        "new EveSurfaceDocument(",
        "new EveSurfaceComponent("
    };
    var missingAdapterSymbols = requiredAdapterSymbols
        .Where(symbol => !runtimeEveSurfaceAdapter.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingAdapterSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime Eve surface adapter is incomplete: " +
            string.Join(", ", missingAdapterSymbols));
    }

    if (!unityPackageProject.Contains("AetheriaRuntimeEveSurfaceAdapter.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "GameCult.Aetheria.State.Unity.csproj does not include the shared runtime Eve surface adapter.");
    }

    var daemonObserverPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonObserver.cs");
    var daemonObserver = File.Exists(daemonObserverPath)
        ? File.ReadAllText(daemonObserverPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaDaemonObserver.cs is missing.");

    var requiredActionGameManagerSymbols = new[]
    {
        "AetheriaRuntimeStateReader.ReadPlayerSettings",
        "AetheriaRuntimeStateReader.ReadLoadoutTemplates",
        "AetheriaRuntimeStateReader.OpenRuntimeCatalog",
        "ResolveDaemonObserver()",
        "LastObservedState"
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

    if (!daemonObserver.Contains("AetheriaRuntimeStateReader.TryReadObservedDaemonState", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaDaemonObserver no longer routes observed daemon state through the shared runtime state reader.");
    }

    if (!mainMenu.Contains("AetheriaRuntimeStateReader", StringComparison.Ordinal) ||
        !mainMenu.Contains("TryReadDaemonFrame(stateBoot.StateFilePath, out var frame)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu no longer routes daemon-frame lookup through the shared runtime state reader.");
    }

    if (!eveSurfacePresenter.Contains("AetheriaRuntimeStateReader.ReadEveSurface", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve surface presenter no longer routes provider surface lookup through the shared runtime state reader.");
    }

    if (!eveSurfacePresenter.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceStateRefResolver(statePath)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve surface presenter no longer resolves provider state refs through the shared runtime state reader.");
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

    var unitySurfaceLowererFiles = Directory
        .EnumerateFiles(Path.Combine(root, "Assets", "Scripts", "UI"), "*.cs", SearchOption.AllDirectories)
        .ToArray();
    var forbiddenLocalSurfaceAdapterSymbols = new[]
    {
        "private static EveSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)",
        "private static EveSurfaceComponent ToEveSurfaceComponent(AetheriaRuntimeSurfaceComponent component)",
        "new EveSurfaceDocument(\r\n            \"surface-state\"",
        "new EveCommandTemplate(command.Command, command.Label, command.Transport)"
    };
    var localSurfaceAdapterHits = unitySurfaceLowererFiles
        .SelectMany(path =>
        {
            var source = File.ReadAllText(path);
            return forbiddenLocalSurfaceAdapterSymbols
                .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
                .Select(symbol => $"{Path.GetRelativePath(root, path)} -> {symbol}");
        })
        .ToArray();
    if (localSurfaceAdapterHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity UI lowerers still duplicate runtime-to-Eve surface conversion instead of using the shared adapter: " +
            string.Join("; ", localSurfaceAdapterHits));
    }
}

static void RequireRuntimeSimulationTuningRequests(string root)
{
    var requiredActionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(requiredActionGameManagerPath)
        ? File.ReadAllText(requiredActionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify runtime simulation tuning authority; ActionGameManager.cs is missing.");

    var requiredRequestMethods = new[]
    {
        "RequestEntityOverrideShutdown",
        "RequestEquippedItemOverrideShutdown",
        "RequestThermotoggleTargetTemperature",
        "RequestEntityShutdownPerformance"
    };

    var missingMethods = requiredRequestMethods
        .Where(method => !actionGameManager.Contains(method, StringComparison.Ordinal))
        .ToArray();

    if (missingMethods.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime simulation tuning request methods are missing from ActionGameManager: " +
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

    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify runtime simulation tuning authority; InventoryMenu.cs is missing.");
    var shutdownRequest = inventoryMenu.IndexOf("GameManager.RequestEntityShutdownPerformance", StringComparison.Ordinal);
    var staleShipSettingsRender = inventoryMenu.IndexOf("RenderCurrentShipSettingsSurface(entity);", shutdownRequest, StringComparison.Ordinal);
    if (shutdownRequest >= 0 && staleShipSettingsRender >= 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still redraws ship settings immediately after submitting daemon shutdown-performance requests.");
    }

    var normalizedInventoryMenu = inventoryMenu.Replace("\r\n", "\n", StringComparison.Ordinal);
    var forbiddenEquippedItemTuningRedraws = new[]
    {
        "GameManager.RequestEquippedItemOverrideShutdown(item, !item.EquippableItem.OverrideShutdown);\n                RenderEquippedItemDetailsSurface(item);",
        "GameManager.RequestThermotoggleTargetTemperature(thermotoggle, command.TargetTemperature);\n                    RenderEquippedItemDetailsSurface(item);"
    };
    var equippedItemTuningRedrawHits = forbiddenEquippedItemTuningRedraws
        .Where(symbol => normalizedInventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (equippedItemTuningRedrawHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still redraws equipped-item tuning surfaces immediately after daemon submission.");
    }

    var forbiddenUnityAcceptanceSymbols = new[]
    {
        "entity?.Settings == null",
        "entity.Settings == null"
    };
    var unityAcceptanceHits = FindMethodScopedLineHits(actionGameManager, forbiddenUnityAcceptanceSymbols)
        .Where(hit => hit.MethodName == "RequestEntityShutdownPerformance")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (unityAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime tuning requests still reject through renderer-local settings checks instead of daemon authority: " +
            string.Join(", ", unityAcceptanceHits));
    }

    var daemonOperationsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperations.cs");
    var daemonOperations = File.Exists(daemonOperationsPath)
        ? File.ReadAllText(daemonOperationsPath)
        : throw new InvalidOperationException("Cannot verify runtime simulation tuning authority; AetheriaRuntimeDaemonOperations.cs is missing.");
    var requiredDaemonValidationSymbols = new[]
    {
        "case AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance:",
        "command.ScalarValue < 0.0 || command.ScalarValue > 1.0"
    };
    var missingDaemonValidationSymbols = requiredDaemonValidationSymbols
        .Where(symbol => !daemonOperations.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonValidationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon runtime tuning no longer validates shutdown-performance range: " +
            string.Join(", ", missingDaemonValidationSymbols));
    }
}

static void RequireHullConductivityRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify hull conductivity authority; ActionGameManager.cs is missing.");

    if (!actionGameManager.Contains("RequestHullConductivityToggle", StringComparison.Ordinal) ||
        !actionGameManager.Contains("TryRequestDaemonHullConductivityToggle", StringComparison.Ordinal) ||
        !actionGameManager.Contains("Operations.ToggleHullConductivity", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Hull conductivity no longer has a typed daemon request primitive.");
    }

    var forbiddenUnityAcceptanceSymbols = new[]
    {
        "entity?.HullConductivity == null",
        "position.x >= entity.HullConductivity.GetLength(0)",
        "position.y >= entity.HullConductivity.GetLength(1)",
        "axis < 0",
        "axis > 1"
    };
    var unityAcceptanceHits = forbiddenUnityAcceptanceSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity hull conductivity requests still reject through renderer-local grid bounds instead of daemon authority: " +
            string.Join(", ", unityAcceptanceHits));
    }

    if (actionGameManager.Contains("public bool RequestHullConductivityToggle(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Hull conductivity request API still exposes submission as public acceptance state.");
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

    var submissionAcceptanceHits = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line =>
            line.Line.Contains("if (GameManager.RequestHullConductivityToggle", StringComparison.Ordinal) ||
            line.Line.Contains("if (RequestHullConductivityToggle", StringComparison.Ordinal) ||
            line.Line.Contains("RefreshCells(new []{v,int2", StringComparison.Ordinal))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (submissionAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Hull conductivity UI still treats daemon submission as accepted cell state: " +
            string.Join("; ", submissionAcceptanceHits));
    }
}

static void RequireInventoryEntityRenameRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify inventory entity rename authority; ActionGameManager.cs is missing.");

    if (!actionGameManager.Contains("RequestEntityName", StringComparison.Ordinal) ||
        !actionGameManager.Contains("TryRequestDaemonEntityName", StringComparison.Ordinal) ||
        !actionGameManager.Contains("Operations.SetEntityName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Entity rename no longer has a typed daemon request primitive.");
    }

    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify inventory entity rename authority; InventoryPanel.cs is missing.");

    if (inventoryPanel.Contains("_displayedEntity.Name =", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still renames entities directly instead of using the typed daemon request primitive.");
    }

    if (!inventoryPanel.Contains("GameManager.RequestEntityName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer routes entity rename through ActionGameManager.");
    }

    var renameRequest = inventoryPanel.IndexOf("GameManager.RequestEntityName", StringComparison.Ordinal);
    var titleRefreshAfterRename = inventoryPanel.IndexOf("Title.text = _displayedEntity.Name", renameRequest, StringComparison.Ordinal);
    if (renameRequest >= 0 && titleRefreshAfterRename >= 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still refreshes entity title immediately after submitting a daemon rename request.");
    }
}

static void RequireWeaponGroupRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify weapon group authority; ActionGameManager.cs is missing.");

    if (!actionGameManager.Contains("RequestWeaponGroupMembership", StringComparison.Ordinal) ||
        !actionGameManager.Contains("TryRequestDaemonWeaponGroupMembership", StringComparison.Ordinal) ||
        !actionGameManager.Contains("Operations.SetWeaponGroupMembership", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Weapon group membership no longer has a typed daemon request primitive.");
    }

    if (actionGameManager.Contains("WeaponGroupDragObject", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Weapon-group action-bar binding still keeps the dead drag-object path alive instead of routing through live gameplay APIs.");
    }

    var forbiddenLocalAcceptanceSymbols = new[]
    {
        "item?.Entity?.WeaponGroups == null",
        "groupIndex >= item.Entity.WeaponGroups.Length",
        "groupIndex < 0",
        "item.GetBehavior<Weapon>()"
    };
    var localAcceptanceHits = forbiddenLocalAcceptanceSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (localAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity weapon-group membership still rejects through renderer-local weapon/group checks instead of daemon acceptance: " +
            string.Join(", ", localAcceptanceHits));
    }

    var forbiddenPublicAcceptanceApis = new[]
    {
        "public bool RequestWeaponGroupMembership("
    };
    var publicAcceptanceApiHits = forbiddenPublicAcceptanceApis
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicAcceptanceApiHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Weapon-group request APIs still expose submission as public acceptance state: " +
            string.Join(", ", publicAcceptanceApiHits));
    }

    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify weapon group authority; InventoryMenu.cs is missing.");
    var forbiddenUiSubmissionAcceptanceSymbols = new[]
    {
        "if (GameManager.RequestWeaponGroupMembership(",
        "Unable to toggle equipped-item weapon group membership."
    };
    var uiSubmissionAcceptanceHits = forbiddenUiSubmissionAcceptanceSymbols
        .Where(symbol => inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (uiSubmissionAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still treats weapon-group membership submission as accepted equipment state: " +
            string.Join(", ", uiSubmissionAcceptanceHits));
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

static void RequireActionBarBindingRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify action-bar binding authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "RequestActionBarBinding(",
        "TryRequestDaemonActionBarBinding(",
        "TryRequestDaemonActionBarBindingClear(",
        "RequestActionBarConsumable(",
        "RequestActionBarBehavior(",
        "RequestActionBarWeaponGroup(",
        "RestoreActionBarBindingsFromTypedRun(",
        "ApplyActionBarBindings(",
        "RequestActionBarBinding(slot, dragAction)"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Action-bar binding no longer has a typed daemon request and observed restore path: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "var newbinds = Enumerable.Range(0, 64)",
        ".Zip(\r\n                    bindings,",
        "groupIndex >= CurrentEntity.WeaponGroups.Length",
        "ApplyDefaultActionBarBindings",
        "ProjectActionBarBindings",
        "ProjectActionBarBinding(slot, slot?.Binding)",
        "Enumerable.Range(0, CurrentEntity.WeaponGroups.Length)",
        "slot == null || groupIndex < 0"
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

    var forbiddenPublicAcceptanceApis = new[]
    {
        "public bool RequestWeaponGroupActionBarBinding(",
        "public bool RequestClearActionBarBinding(",
        "public bool TryRequestDaemonActionBarConsumable(",
        "public bool TryRequestDaemonActionBarBehavior(",
        "public bool TryRequestDaemonActionBarWeaponGroup("
    };
    var publicAcceptanceApiHits = forbiddenPublicAcceptanceApis
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicAcceptanceApiHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionBar binding request APIs still expose submission as public acceptance state: " +
            string.Join(", ", publicAcceptanceApiHits));
    }

    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify action-bar binding authority; InventoryMenu.cs is missing.");
    var forbiddenUiAcceptanceSymbols = new[]
    {
        "GameManager.RequestWeaponGroupActionBarBinding(command.SlotIndex, command.GroupIndex))",
        "GameManager.RequestClearActionBarBinding(command.SlotIndex))",
        "Unable to bind equipped-item weapon group to action bar.",
        "Unable to clear equipped-item action bar binding."
    };
    var uiAcceptanceHits = forbiddenUiAcceptanceSymbols
        .Where(symbol => inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (uiAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still treats equipped-item action-bar command submission as accepted binding state: " +
            string.Join(", ", uiAcceptanceHits));
    }

    var actionBarSlotPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionBarSlot.cs");
    var actionBarSlot = File.Exists(actionBarSlotPath)
        ? File.ReadAllText(actionBarSlotPath)
        : throw new InvalidOperationException("Cannot verify action-bar activation authority; ActionBarSlot.cs is missing.");
    var forbiddenSlotSymbols = new[]
    {
        "TryRequestDaemonActionBarConsumable(",
        "TryRequestDaemonActionBarBehavior(",
        "TryRequestDaemonActionBarWeaponGroup("
    };
    var slotHits = forbiddenSlotSymbols
        .Where(symbol => actionBarSlot.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (slotHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionBarSlot still calls daemon transport helpers directly instead of submit-only request APIs: " +
            string.Join(", ", slotHits));
    }
}

static void RequireInventoryDoubleClickTransferRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify inventory transfer authority; ActionGameManager.cs is missing.");

    var requiredRequests = new[]
    {
        "RequestCargoItemTransfer",
        "RequestCargoItemEquip",
        "RequestEquippedItemStore",
        "RequestEquippedItemEquip",
        "TryRequestDaemonCargoItemTransfer",
        "TryRequestDaemonCargoItemEquip",
        "TryRequestDaemonEquippedItemStore",
        "TryRequestDaemonEquippedItemEquip"
    };

    var missingRequests = requiredRequests
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingRequests.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory transfer no longer has complete typed daemon request primitives: " +
            string.Join(", ", missingRequests));
    }

    var forbiddenUnityInventoryAcceptanceSymbols = new[]
    {
        "TryFindSpace(",
        "ItemFits(",
        "Cargo.ContainsKey(",
        "Equipment.Contains(",
        "origin == destination",
        "ReferenceEquals(equippedItem.Entity, origin)",
        "!origin.Cargo.TryGetValue(item, out var sourcePosition)"
    };
    var unityInventoryAcceptanceHits = forbiddenUnityInventoryAcceptanceSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityInventoryAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity inventory request code still rejects requests through renderer-local capacity or membership checks: " +
            string.Join(", ", unityInventoryAcceptanceHits));
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

    var forbiddenInventoryUiAcceptanceSymbols = new[]
    {
        ".ItemFits(",
        ".TryFindSpace("
    };
    var inventoryUiAcceptanceHits = File.ReadLines(inventoryPanelPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenInventoryUiAcceptanceSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, inventoryPanelPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();
    if (inventoryUiAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still rejects transfer placement through renderer-local fit checks instead of daemon authority: " +
            string.Join("; ", inventoryUiAcceptanceHits));
    }

    var forbiddenInventorySubmissionAcceptanceSymbols = new[]
    {
        "Unable to move item!",
        "Verify that cargo bays are empty before un-equipping them.",
        "var success = RequestDraggedItemTo",
        "var submitted = RequestDraggedItemTo",
        "if (RequestCargoItemTransfer(",
        "if (RequestEquippedItemTransfer(",
        "ShowUnableToSubmitItemMoveRequestDialog("
    };
    var inventorySubmissionAcceptanceHits = new[] { inventoryMenuPath, inventoryPanelPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenInventorySubmissionAcceptanceSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();
    if (inventorySubmissionAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory UI still treats daemon transfer submission as accepted inventory movement: " +
            string.Join("; ", inventorySubmissionAcceptanceHits));
    }

    var forbiddenPublicAcceptanceApis = new[]
    {
        "public bool RequestCargoItemTransfer(",
        "public bool RequestCargoItemEquip(",
        "public bool RequestEquippedItemStore(",
        "public bool RequestEquippedItemEquip(",
        "private bool RequestDraggedItemToEntity(",
        "private bool RequestDraggedItemToCargo(",
        "private bool RequestCargoItemTransfer(",
        "private bool RequestEquippedItemTransfer(",
        "public bool EndDrag(",
        "Func<DragObject, bool>",
        "RegisterDragTarget(Func<DragObject, bool>"
    };
    var publicAcceptanceApiHits = new[] { actionGameManagerPath, inventoryMenuPath, inventoryPanelPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenPublicAcceptanceApis.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();
    if (publicAcceptanceApiHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory drag/request APIs still expose submission as public acceptance state: " +
            string.Join("; ", publicAcceptanceApiHits));
    }

    var requiredUiCalls = new[]
    {
        "GameManager.RequestCargoItemTransfer",
        "GameManager.RequestCargoItemEquip",
        "GameManager.RequestEquippedItemStore",
        "GameManager.RequestEquippedItemEquip"
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

static void RequireTradePurchaseRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify trade purchase authority; ActionGameManager.cs is missing.");

    var requiredRequests = new[]
    {
        "RequestTradePurchase",
        "TryRequestDaemonTradePurchase",
        "Operations.TradePurchase"
    };

    var missingRequests = requiredRequests
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingRequests.Length > 0)
    {
        throw new InvalidOperationException(
            "Trade purchase no longer has a typed daemon request primitive: " +
            string.Join(", ", missingRequests));
    }

    var forbiddenUnityTradeAcceptanceSymbols = new[]
    {
        "price > Credits",
        "totalPrice > Credits",
        "targetCargo.TryFindSpace("
    };
    var unityTradeAcceptanceHits = forbiddenUnityTradeAcceptanceSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityTradeAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity trade/loadout request code still rejects requests through renderer-local credits or cargo-capacity checks: " +
            string.Join(", ", unityTradeAcceptanceHits));
    }

    var daemonOperationsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperations.cs");
    var daemonOperations = File.Exists(daemonOperationsPath)
        ? File.ReadAllText(daemonOperationsPath)
        : throw new InvalidOperationException("Cannot verify trade purchase authority; daemon operations are missing.");
    var requiredDaemonShipPurchaseSymbols = new[]
    {
        "ApplyCreateDockedShipPurchase(",
        "purchase.CreatesDockedShip",
        "run.CurrentEntityKey = purchasedShipKey",
        "HullItemKey = itemKey"
    };
    var missingDaemonShipPurchaseSymbols = requiredDaemonShipPurchaseSymbols
        .Where(symbol => !daemonOperations.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonShipPurchaseSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon trade purchase no longer materializes purchased docked ships as typed run state: " +
            string.Join(", ", missingDaemonShipPurchaseSymbols));
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

    var forbiddenLocalAcceptanceSymbols = new[]
    {
        "price > GameManager.Credits",
        "totalPrice > GameManager.Credits",
        "GameManager.Credits / price",
        "min(quantity, simpleCommodity.Quantity)",
        "quantity = min(q, simpleCommodity.Quantity)",
        "\"Insufficient Credits!\"",
        "\"Insufficient Cargo Space!\"",
        "\"Unable to create ship!\""
    };

    var localAcceptanceHits = File.ReadLines(tradeMenuPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenLocalAcceptanceSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, tradeMenuPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (localAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still rejects purchases through renderer-local acceptance checks instead of daemon authority: " +
            string.Join("; ", localAcceptanceHits));
    }

    if (!tradeMenu.Contains("GameManager.RequestTradePurchase", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("TradeMenu no longer routes purchases through ActionGameManager.");
    }

    var forbiddenSubmissionAcceptanceSymbols = new[]
    {
        "if (!GameManager.RequestTradePurchase(",
        "\"Purchase request rejected!\"",
        "public bool RequestTradePurchase("
    };
    var submissionAcceptanceHits = new[] { tradeMenuPath, actionGameManagerPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenSubmissionAcceptanceSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (submissionAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still treats daemon purchase submission as accepted or rejected purchase state: " +
            string.Join("; ", submissionAcceptanceHits));
    }

    var firstTradeRequest = tradeMenu.IndexOf("GameManager.RequestTradePurchase", StringComparison.Ordinal);
    var firstCreditRefreshAfterRequest = tradeMenu.IndexOf("UpdateCreditsLabel();", firstTradeRequest, StringComparison.Ordinal);
    if (firstTradeRequest >= 0 && firstCreditRefreshAfterRequest >= 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still refreshes projected credits immediately after submitting a daemon purchase request instead of waiting for daemon-observed state.");
    }
}

static void RequireLootPickupRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify loot pickup authority; ActionGameManager.cs is missing.");

    if (actionGameManager.Contains("CommitLootPickup", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loot pickup should not have a Unity-owned gameplay mutation primitive; daemon snapshots own pickup state.");
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

    if (shieldManager.Contains("ActionGameManager.Instance.CommitLootPickup", StringComparison.Ordinal))
        throw new InvalidOperationException("ShieldManager still routes loot pickup through Unity gameplay authority.");
}

static void RequireEntityDestroyedRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify entity destruction authority; ActionGameManager.cs is missing.");

    if (actionGameManager.Contains("CommitEntityDestroyed", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Entity destruction should not have a Unity-owned gameplay mutation primitive; daemon frames own destruction and drops.");
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

    if (entityInstance.Contains("ActionGameManager.Instance?.CommitEntityDestroyed", StringComparison.Ordinal))
        throw new InvalidOperationException("EntityInstance still routes destruction through Unity gameplay authority.");
}

static void RequireDroppedPickupCheckpointState(string root)
{
    var requiredFiles = new Dictionary<string, string[]>
    {
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs")] = new[]
        {
            "AetheriaRuntimeDroppedPickupCommit",
            "DroppedPickups",
            "public double Temperature { get; set; }"
        },
        [Path.Combine(root, "Aetheria.State", "Documents", "AetheriaRuntimeStateDocuments.cs")] = new[]
        {
            "AetheriaDroppedPickupSnapshot",
            "DroppedPickups",
            "public double Temperature { get; set; }"
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs")] = new[]
        {
            "ReadZoneStatePayload(record.Key, record.Payload)",
            "ReadFieldDroppedPickups",
            "AetheriaRuntimeDroppedPickupSnapshot",
            "var temperature = ReadFieldDouble(ref reader"
        },
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogSnapshot.cs")] = new[]
        {
            "public string RecordKey",
            "AetheriaRuntimeDroppedPickupSnapshot",
            "public IReadOnlyList<AetheriaRuntimeDroppedPickupSnapshot> DroppedPickups",
            "public double Temperature { get; }"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")] = new[]
        {
            "RestoreDroppedPickupsFromDaemonZoneState",
            "ZoneRenderer.DropItem("
        },
        [Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs")] = new[]
        {
            "ActiveLoot"
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

static void RequireInventoryLoadoutSaveRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify loadout save authority; ActionGameManager.cs is missing.");
    var eveCommandDocumentPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeEveCommandDocument.cs");
    var loadoutCommandsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeLoadoutTemplateCommands.cs");
    var eveBridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var runtimeStateMapperPath = Path.Combine(root, "Aetheria.State", "AetheriaRuntimeStateMapper.cs");
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");

    var requiredFiles = new[] { eveCommandDocumentPath, loadoutCommandsPath, eveBridgePath, runtimeStateMapperPath, inventoryPanelPath };
    var missingFiles = requiredFiles.Where(path => !File.Exists(path)).ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify loadout save authority; missing files: " +
            string.Join(", ", missingFiles.Select(path => Path.GetRelativePath(root, path))));
    }

    var eveCommandDocument = File.ReadAllText(eveCommandDocumentPath);
    var loadoutCommands = File.ReadAllText(loadoutCommandsPath);
    var eveBridge = File.ReadAllText(eveBridgePath);
    var runtimeStateMapper = File.ReadAllText(runtimeStateMapperPath);
    var inventoryPanel = File.ReadAllText(inventoryPanelPath);

    var requiredActionSymbols = new[]
    {
        "RequestLoadoutTemplateSave(Entity entity)",
        "ProjectLoadoutTemplate(Entity entity)",
        "ProjectEntityLoadout(Entity entity)",
        "ProjectSlots(IEnumerable<EquippedItem> slots)",
        "ProjectCargoBays(IEnumerable<EquippedCargoBay> bays)",
        "SendRuntimeLoadoutTemplateCommand(loadout",
        "private static void SendRuntimeLoadoutTemplateCommand(",
        "AetheriaRuntimeEveCommands.TrySendLoadoutTemplateCommand",
    };
    var missingActionSymbols = requiredActionSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingActionSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Loadout save no longer sends a typed Eve loadout-template command: " +
            string.Join(", ", missingActionSymbols));
    }

    var forbiddenActionSymbols = new[]
    {
        "AetheriaRuntimeStateCommitLog.QueueLoadoutTemplate",
        "TrySendRuntimeLoadoutTemplateCommand(",
        "private static bool TrySendRuntimeLoadoutTemplateCommand(",
        "RequestLoadoutTemplateSave(EntityConstructionBlueprint blueprint)"
    };
    var forbiddenActionHits = forbiddenActionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenActionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still saves loadout templates through local commit or submission-acceptance authority: " +
            string.Join(", ", forbiddenActionHits));
    }

    if (!inventoryPanel.Contains("GameManager.RequestLoadoutTemplateSave(_displayedEntity)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("EntityConstructionBlueprintProjector.CaptureBlueprint(_displayedEntity)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still captures a Unity EntityConstructionBlueprint before submitting a typed loadout-template save.");
    }

    var requiredBridgeSymbols = new[]
    {
        "AetheriaRuntimeEveCommandKind.SaveLoadoutTemplate",
        "ExecuteLoadoutTemplateCommandAsync",
        "command.LoadoutTemplate",
        "AetheriaRuntimeStateMapper.ToLoadoutTemplate",
        "AetheriaRuntimeStateMapper.LoadoutKey"
    };
    var missingBridgeSymbols = requiredBridgeSymbols
        .Where(symbol => !eveBridge.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBridgeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve command bridge no longer owns typed loadout-template persistence: " +
            string.Join(", ", missingBridgeSymbols));
    }

    if (!eveCommandDocument.Contains("AetheriaRuntimeLoadoutTemplateCommit? LoadoutTemplate", StringComparison.Ordinal) ||
        !loadoutCommands.Contains("SurfaceId = \"aetheria.loadout_templates\"", StringComparison.Ordinal) ||
        !loadoutCommands.Contains("Save = \"aetheria.loadout_templates.save\"", StringComparison.Ordinal) ||
        !runtimeStateMapper.Contains("public static AetheriaLoadoutTemplate ToLoadoutTemplate", StringComparison.Ordinal) ||
        !runtimeStateMapper.Contains("public static CultRecordKey LoadoutKey", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared loadout-template Eve command contract is incomplete.");
    }
}

static void RequireInventoryLoadoutRestoreRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; ActionGameManager.cs is missing.");
    var tradeQueriesPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonTradeItemQueries.cs");
    var tradeQueries = File.Exists(tradeQueriesPath)
        ? File.ReadAllText(tradeQueriesPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; runtime trade item queries are missing.");

    var requiredSymbols = new[]
    {
        "RequestRuntimeLoadoutRestore",
        "RequestDaemonLoadoutRestore",
        "Operations.RestoreLoadout"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Loadout restore no longer has a typed daemon request primitive: " +
            string.Join(", ", missingSymbols));
    }

    if (actionGameManager.Contains("price > Credits", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity loadout restore still rejects requests through renderer-local credits instead of daemon acceptance.");
    }

    if (!actionGameManager.Contains("ObservedTradeValueSettings()", StringComparison.Ordinal) ||
        !actionGameManager.Contains("AetheriaRuntimeDaemonTradeItemQueries.TryProjectLoadoutTemplatePrice(", StringComparison.Ordinal) ||
        actionGameManager.Contains("blueprint.Price(ItemManager)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must price loadout restore requests through shared runtime loadout queries instead of blueprint/ItemManager value authority.");
    }

    var forbiddenActionSymbols = new[]
    {
        "TryRequestDaemonLoadoutRestore",
        "private bool TryRequestDaemonLoadoutRestore("
    };
    var forbiddenActionHits = forbiddenActionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenActionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity loadout restore still exposes daemon request submission as local acceptance state: " +
            string.Join(", ", forbiddenActionHits));
    }

    var daemonOperationsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperations.cs");
    var daemonOperations = File.Exists(daemonOperationsPath)
        ? File.ReadAllText(daemonOperationsPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; daemon operations are missing.");
    var missingTemplateCheck = daemonOperations.IndexOf("if (template == null)", StringComparison.Ordinal);
    var creditMutation = daemonOperations.IndexOf("run.Credits -= price", StringComparison.Ordinal);
    if (missingTemplateCheck < 0 ||
        creditMutation < 0 ||
        missingTemplateCheck > creditMutation)
    {
        throw new InvalidOperationException(
            "Daemon loadout restore must reject missing templates before charging credits or mutating run state.");
    }

    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; InventoryPanel.cs is missing.");

    var forbiddenSymbols = new[]
    {
        "EntityConstructionBlueprintProjector.InstantiateFromBlueprint",
        "EntityConstructionBlueprintProjector.ProjectObservedFromBlueprint",
        "EntityConstructionBlueprintProjector.InstantiateAuthoritativeFromBlueprint",
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

    var forbiddenLocalAcceptanceSymbols = new[]
    {
        "price < GameManager.Credits",
        "price <= GameManager.Credits",
        "price > GameManager.Credits",
        "price >= GameManager.Credits"
    };
    var localAcceptanceHits = File.ReadLines(inventoryPanelPath)
        .Select((line, index) => new { LineNumber = index + 1, Line = line })
        .Where(line => forbiddenLocalAcceptanceSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, inventoryPanelPath)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (localAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still filters loadout restore through renderer-local credit checks instead of daemon acceptance: " +
            string.Join("; ", localAcceptanceHits));
    }

    if (!inventoryPanel.Contains("GameManager.RequestRuntimeLoadoutRestore", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer routes loadout restore through ActionGameManager.");
    }

    if (!inventoryPanel.Contains("AetheriaRuntimeDaemonTradeItemQueries.TryProjectLoadoutTemplatePrice(", StringComparison.Ordinal) ||
        inventoryPanel.Contains("blueprint.Price(GameManager.ItemManager)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel must label loadout restore options through shared runtime loadout price queries instead of Unity ItemManager pricing.");
    }

    if (!tradeQueries.Contains("public static bool TryProjectLoadoutTemplatePrice(", StringComparison.Ordinal) ||
        !tradeQueries.Contains("TryProjectEntityLoadoutPrice(", StringComparison.Ordinal) ||
        !tradeQueries.Contains("typedItem.Stackable", StringComparison.Ordinal) ||
        !tradeQueries.Contains("typedItem.Price * Math.Max(1, item.Quantity)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime trade item queries must own typed loadout template price projection.");
    }

    if (!actionGameManager.Contains("public IEnumerable<AetheriaRuntimeLoadoutTemplateSnapshot> ObservedLoadoutTemplates()", StringComparison.Ordinal) ||
        actionGameManager.Contains("public List<AetheriaRuntimeLoadoutTemplateSnapshot> LoadoutTemplates", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.LoadoutTemplates", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel must enumerate daemon-observed loadout templates through ActionGameManager.ObservedLoadoutTemplates().");
    }

    if (actionGameManager.Contains("RequestRuntimeLoadoutRestore(AetheriaRuntimeLoadoutTemplateSnapshot template, out Entity", StringComparison.Ordinal) ||
        actionGameManager.Contains("public bool RequestRuntimeLoadoutRestore(", StringComparison.Ordinal) ||
        inventoryPanel.Contains("RequestRuntimeLoadoutRestore(template, out", StringComparison.Ordinal) ||
        inventoryPanel.Contains("RequestRuntimeLoadoutRestore(loadoutEntry.template, out", StringComparison.Ordinal) ||
        inventoryPanel.Contains("Display(entity)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout restore is still pretending daemon submission synchronously yields accepted Unity state.");
    }
}

static void RequireDockedCurrentShipRequestAuthority(string root)
{
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify docked current-ship authority; ActionGameManager.cs is missing.");

    var requiredSymbols = new[]
    {
        "RequestDockedCurrentShip",
        "TryRequestDaemonDockedCurrentShip",
        "Operations.SetDockedCurrentShip"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Docked current-ship selection no longer has a typed daemon request primitive: " +
            string.Join(", ", missingSymbols));
    }

    if (actionGameManager.Contains("DockedEntity.Children.Contains(ship)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity docked current-ship selection still rejects through renderer-local child membership instead of daemon acceptance.");
    }

    var forbiddenUnityAcceptanceSymbols = new[]
    {
        "!ship.IsPlayerShip",
        "DockedEntity == null",
        "DockingBay == null"
    };
    var unityAcceptanceHits = FindMethodScopedLineHits(actionGameManager, forbiddenUnityAcceptanceSymbols)
        .Where(hit => hit.MethodName == "RequestDockedCurrentShip")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (unityAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity docked current-ship selection still rejects through renderer-local player/docking state instead of daemon acceptance: " +
            string.Join(", ", unityAcceptanceHits));
    }

    if (actionGameManager.Contains("public bool RequestDockedCurrentShip(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Docked current-ship request API still exposes submission as public acceptance state.");
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
    if (!inventoryPanel.Contains("GameManager.RequestDockedCurrentShip", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer routes current-ship selection through ActionGameManager.");
    }

    if (!inventoryPanel.Contains("GameManager.TryGetObservedCurrentEntity(out var currentEntity)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.CurrentEntity", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel must compare current-ship UI state through daemon-observed current entity state instead of peeking GameManager.CurrentEntity directly.");
    }

    if (inventoryPanel.Contains("Current.targetGraphic.color = ToggleEnabledColor;", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still paints current-ship selection as accepted immediately after daemon submission.");
    }
}

static IEnumerable<(string MethodName, int LineNumber, string Line)> FindMethodScopedLineHits(
    string source,
    IReadOnlyList<string> symbols)
{
    var currentMethod = "<top-level>";
    var methodDepth = 0;
    var pendingMethod = "";
    var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    for (var index = 0; index < lines.Length; index++)
    {
        var line = lines[index];
        var trimmed = line.Trim();
        var declaredMethod = TryReadMethodName(trimmed);
        if (!string.IsNullOrWhiteSpace(declaredMethod))
            pendingMethod = declaredMethod;

        foreach (var symbol in symbols)
        {
            if (line.Contains(symbol, StringComparison.Ordinal))
                yield return (currentMethod, index + 1, line);
        }

        var opens = line.Count(character => character == '{');
        var closes = line.Count(character => character == '}');
        if (!string.IsNullOrWhiteSpace(pendingMethod) && opens > 0)
        {
            currentMethod = pendingMethod;
            pendingMethod = "";
            methodDepth = opens - closes;
            if (methodDepth <= 0)
            {
                currentMethod = "<top-level>";
                methodDepth = 0;
            }
            continue;
        }

        if (methodDepth <= 0)
            continue;

        methodDepth += opens - closes;
        if (methodDepth <= 0)
        {
            currentMethod = "<top-level>";
            methodDepth = 0;
        }
    }
}

static string TryReadMethodName(string trimmedLine)
{
    if (!StartsWithMethodAccessModifier(trimmedLine) ||
        !trimmedLine.Contains('(') ||
        trimmedLine.Contains("=>", StringComparison.Ordinal) ||
        trimmedLine.Contains("=", StringComparison.Ordinal))
    {
        return "";
    }

    var beforeParameters = trimmedLine.Substring(0, trimmedLine.IndexOf('(')).Trim();
    var tokens = beforeParameters.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    return tokens.Length == 0 ? "" : tokens[^1];
}

static bool StartsWithMethodAccessModifier(string trimmedLine)
{
    return trimmedLine.StartsWith("private ", StringComparison.Ordinal) ||
           trimmedLine.StartsWith("public ", StringComparison.Ordinal) ||
           trimmedLine.StartsWith("protected ", StringComparison.Ordinal) ||
           trimmedLine.StartsWith("internal ", StringComparison.Ordinal) ||
           trimmedLine.StartsWith("static ", StringComparison.Ordinal);
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
