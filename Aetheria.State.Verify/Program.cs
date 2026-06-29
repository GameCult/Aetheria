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
RequireStationRefitDockingBaysUseTypedProjection(root);
RequireNoDeadPopupShells(root);
RequirePlayerSettingsEveSurface(root);
RequireVerseHostSettingsAuthority(root);
RequireClientTargetBootAuthority(root);
RequireVerseReplicaTool(root);
RequireVerseSettingsShellAndBridge(root);
RequireTypedStatRecipeOperations(root);
RequireTypedDaemonCommandPayloads(root);
RequireUnityPublicRequestVocabulary(root);
RequireCatalogSurfaceUsesManagedRuntimeCatalog(root);
RequireDaemonVersePublication(root);
RequireUnityRuntimeCatalogClientUsesManagedDocument(root);
RequireAetheriaRuntimeVerseClientContract(root);
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
RequireInventoryProjectionSlotIdentity(root);
RequireInventoryValidationUsesManagedTypedDocuments(root);
RequireMenuDockingUsesManagedTypedSnapshot(root);
RequireUnitySharedDocumentAccessorErgonomics(root);
RequireUnityViewportAndMapReadsUseManagedAccessors(root);
RequireAetheriaManagedStateAccessorsCoverDomainDocuments(root);
RequireInventoryLoadoutSaveRequestAuthority(root);
RequireInventoryLoadoutRestoreRequestAuthority(root);
RequireDockedCurrentShipRequestAuthority(root);
RequireAuthoritySmokeUsesManagedPointers(root);
RequireAetheriaStateNodeUsesManagedPointers(root);

await using var node = await AetheriaStateNode.OpenAsync(
    statePath,
    "aetheria-state-verify",
    enableDurableShardLogs: false);

var ledger = await node.MutableDocument<AetheriaMigrationLedger>(AetheriaStateNode.MigrationLedgerKey).ReadAsync()
    ?? throw new InvalidOperationException("Missing typed migration ledger.");
var quarantine = await node.MutableDocument<AetheriaLegacyCatalogQuarantine>(AetheriaStateNode.LegacyCatalogQuarantineKey).ReadAsync()
    ?? throw new InvalidOperationException("Missing legacy catalog quarantine document.");
var publishedSurface = await node.CatalogSurface().LatestAsync().ConfigureAwait(false);
if (publishedSurface == null)
    throw new InvalidOperationException("Missing managed Aetheria catalog Eve surface document.");

var items = node.Cache.GetAll<AetheriaItemDefinition>().ToArray();
var corporations = node.Cache.GetAll<AetheriaCorporation>().ToArray();
var nameFiles = node.Cache.GetAll<AetheriaNameFile>().ToArray();
var catalog = await node.RuntimeCatalog().LatestAsync().ConfigureAwait(false);
var surface = AetheriaCatalogSurfaceProjector.Build(catalog, DateTimeOffset.UtcNow.ToString("O"));
var tradeValuePolicy = await node.MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey).ReadAsync()
    ?? throw new InvalidOperationException("Missing authored trade value policy document.");
await RequireTradeValuePolicyEveCommandPersistsAsync();
var runtimeCatalog = catalog;

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
if (tradeValuePolicy.QualityPriceModifier == null ||
    tradeValuePolicy.QualityPriceModifier.Exponent <= 0 ||
    tradeValuePolicy.QualityPriceModifier.Maximum <= tradeValuePolicy.QualityPriceModifier.Minimum ||
    tradeValuePolicy.Tiers.Length == 0)
{
    throw new InvalidOperationException("Authored trade value policy is missing quality pricing or rarity tiers.");
}

if (runtimeCatalog.TradeValueSettings.Tiers.Count != tradeValuePolicy.Tiers.Length ||
    Math.Abs(runtimeCatalog.TradeValueSettings.QualityPriceModifier.Exponent - tradeValuePolicy.QualityPriceModifier.Exponent) > 0.0001)
{
    throw new InvalidOperationException("Runtime catalog did not read the authored trade value policy.");
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

var behaviorKind = catalog.Items.SelectMany(item => item.BehaviorKinds).FirstOrDefault()
    ?? throw new InvalidOperationException("Cannot verify typed catalog behavior query: no behavior kinds.");
if (!catalog.FindItemsByBehavior(behaviorKind).Any())
{
    throw new InvalidOperationException($"Typed catalog behavior query failed for {behaviorKind}.");
}

var hardpointType = catalog.Items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.HardpointType))?.HardpointType
    ?? throw new InvalidOperationException("Cannot verify typed catalog hardpoint query: no hardpoint types.");
if (!catalog.FindItemsByHardpoint(hardpointType).Any())
{
    throw new InvalidOperationException($"Typed catalog hardpoint query failed for {hardpointType}.");
}

var typedHardpointItem = catalog.Items.FirstOrDefault(item => item.TryGetHardpointType<AetheriaVerifyHardpointType>(out _))
    ?? throw new InvalidOperationException("Typed catalog item hardpoint enum accessor failed.");
var typedSimpleCommodityItem = catalog.Items.FirstOrDefault(item => item.TryGetSimpleCommodityCategory<AetheriaVerifySimpleCommodityCategory>(out _))
    ?? throw new InvalidOperationException("Typed catalog item simple commodity enum accessor failed.");
var typedCompoundCommodityItem = catalog.Items.FirstOrDefault(item => item.TryGetCompoundCommodityCategory<AetheriaVerifyCompoundCommodityCategory>(out _))
    ?? throw new InvalidOperationException("Typed catalog item compound commodity enum accessor failed.");
if (string.IsNullOrWhiteSpace(typedHardpointItem.HardpointType) ||
    string.IsNullOrWhiteSpace(typedSimpleCommodityItem.SimpleCommodityCategory) ||
    string.IsNullOrWhiteSpace(typedCompoundCommodityItem.CompoundCommodityCategory))
{
    throw new InvalidOperationException("Typed catalog item enum accessors returned an item without its source field.");
}

var manufacturedItem = catalog.Items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.ManufacturerKey))
    ?? throw new InvalidOperationException("Cannot verify typed catalog manufacturer lookup: no manufactured item.");
if (string.IsNullOrWhiteSpace(manufacturedItem.ManufacturerKey) ||
    catalog.FindCorporation(manufacturedItem.ManufacturerKey) == null ||
    catalog.GetManufacturer(manufacturedItem) == null)
{
    throw new InvalidOperationException(
        $"Typed catalog manufacturer-key lookup failed for item {manufacturedItem.Name}.");
}

var corporationWithNames = catalog.Corporations.FirstOrDefault(corporation =>
    !string.IsNullOrWhiteSpace(corporation.GeonameFileKey))
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
    () => node.MutableDocument<AetheriaItemDefinition>(AetheriaCatalogKeys.ItemDefinitionFromLegacyId(items[0].LegacyId)).ReadAsync(),
    "item definition");
await RequireLegacyLookupAsync(
    corporations[0].LegacyId,
    () => node.MutableDocument<AetheriaCorporation>(AetheriaCatalogKeys.CorporationFromLegacyId(corporations[0].LegacyId)).ReadAsync(),
    "corporation");
await RequireLegacyLookupAsync(
    nameFiles[0].LegacyId,
    () => node.MutableDocument<AetheriaNameFile>(AetheriaCatalogKeys.NameFileFromLegacyId(nameFiles[0].LegacyId)).ReadAsync(),
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
Console.WriteLine("Verse client state authority: Unity gameplay/UI read typed state through the shared Verse client instead of direct store spelunking");
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
Console.WriteLine("Action-bar binding authority: Unity owns local input bindings; activations send daemon operations");
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
    var upstreamSurfaceDocumentPath = Path.Combine(
        evePackagesRoot,
        "org.gamecult.eve.surface",
        "Runtime",
        "EveSurfaceDocument.cs");
    var upstreamSurfaceAsmdefPath = Path.Combine(
        evePackagesRoot,
        "org.gamecult.eve.surface",
        "Runtime",
        "GameCult.Eve.Surface.asmdef");
    var upstreamUnityUiToolkitPath = Path.Combine(evePackagesRoot, "org.gamecult.eve.unity-uitoolkit", "package.json");
    var upstreamUnityUiToolkitLowererPath = Path.Combine(
        evePackagesRoot,
        "org.gamecult.eve.unity-uitoolkit",
        "Runtime",
        "EveUiToolkitSurfaceLowerer.cs");
    var runtimeAdapterPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeEveSurfaceAdapter.cs");
    var runtimeSurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerSettingsSurfaceBuilder.cs");
    var runtimeCultCacheDocumentStorePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCultCacheDocumentStore.cs");
    var runtimeSurfaceStateProjectorPath = Path.Combine(
        root,
        "Aetheria.State",
        "AetheriaRuntimeEveSurfaceStateProjector.cs");
    var playerSettingsSurfaceProjectorPath = Path.Combine(
        root,
        "Aetheria.State",
        "AetheriaPlayerSettingsSurfaceProjector.cs");
    var catalogSurfaceProjectorPath = Path.Combine(
        root,
        "Aetheria.State",
        "AetheriaCatalogSurfaceProjector.cs");
    var operationsSurfaceProjectorPath = Path.Combine(
        root,
        "Aetheria.State",
        "AetheriaOperationsSurfaceProjector.cs");

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
    var upstreamSurfaceDocument = File.Exists(upstreamSurfaceDocumentPath)
        ? File.ReadAllText(upstreamSurfaceDocumentPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; upstream EveSurfaceDocument.cs is missing.");
    var upstreamSurfaceAsmdef = File.Exists(upstreamSurfaceAsmdefPath)
        ? File.ReadAllText(upstreamSurfaceAsmdefPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; upstream GameCult.Eve.Surface.asmdef is missing.");
    var upstreamUnityUiToolkitLowerer = File.Exists(upstreamUnityUiToolkitLowererPath)
        ? File.ReadAllText(upstreamUnityUiToolkitLowererPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; upstream EveUiToolkitSurfaceLowerer.cs is missing.");
    var runtimeAdapter = File.Exists(runtimeAdapterPath)
        ? File.ReadAllText(runtimeAdapterPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaRuntimeEveSurfaceAdapter.cs is missing.");
    var runtimeSurfaceBuilder = File.Exists(runtimeSurfaceBuilderPath)
        ? File.ReadAllText(runtimeSurfaceBuilderPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaRuntimePlayerSettingsSurfaceBuilder.cs is missing.");
    var runtimeCultCacheDocumentStore = File.Exists(runtimeCultCacheDocumentStorePath)
        ? File.ReadAllText(runtimeCultCacheDocumentStorePath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaRuntimeCultCacheDocumentStore.cs is missing.");
    var runtimeSurfaceStateProjector = File.Exists(runtimeSurfaceStateProjectorPath)
        ? File.ReadAllText(runtimeSurfaceStateProjectorPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaRuntimeEveSurfaceStateProjector.cs is missing.");
    var playerSettingsSurfaceProjector = File.Exists(playerSettingsSurfaceProjectorPath)
        ? File.ReadAllText(playerSettingsSurfaceProjectorPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaPlayerSettingsSurfaceProjector.cs is missing.");
    var catalogSurfaceProjector = File.Exists(catalogSurfaceProjectorPath)
        ? File.ReadAllText(catalogSurfaceProjectorPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaCatalogSurfaceProjector.cs is missing.");
    var operationsSurfaceProjector = File.Exists(operationsSurfaceProjectorPath)
        ? File.ReadAllText(operationsSurfaceProjectorPath)
        : throw new InvalidOperationException("Cannot verify shared Eve package ownership; AetheriaOperationsSurfaceProjector.cs is missing.");

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

    if (!upstreamSurfaceAsmdef.Contains("\"GameCult.Mesh\"", StringComparison.Ordinal) ||
        !upstreamSurfaceDocument.Contains("using GameCult.Mesh;", StringComparison.Ordinal) ||
        !upstreamSurfaceDocument.Contains("public IReadOnlyList<CultMeshStateBindingDescriptor> StateBindings", StringComparison.Ordinal) ||
        !upstreamSurfaceDocument.Contains("public CultMeshOperationBindingDescriptor Operation", StringComparison.Ordinal) ||
        !upstreamSurfaceDocument.Contains("public CultMeshOperationInvocationDescriptor Operation", StringComparison.Ordinal) ||
        !upstreamSurfaceDocument.Contains("public CultMeshOperationPayload Payload", StringComparison.Ordinal) ||
        !upstreamSurfaceDocument.Contains("public string Command => Operation.OperationId", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The shared Eve surface package no longer exposes first-class CultMesh state bindings and operation descriptors.");
    }

    if (upstreamSurfaceDocument.Contains("EveCommandTemplate(string command", StringComparison.Ordinal) ||
        upstreamSurfaceDocument.Contains("string command,\r\n            IReadOnlyDictionary<string, string> payload", StringComparison.Ordinal) ||
        upstreamSurfaceDocument.Contains("CultMeshOperationInvocationDescriptor operation,\r\n            IReadOnlyDictionary<string, string> payload", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The shared Eve surface package has reintroduced raw command or dictionary payload request constructors.");
    }

    if (!upstreamUnityUiToolkitLowerer.Contains("ResolveOperation(document, command)", StringComparison.Ordinal) ||
        !upstreamUnityUiToolkitLowerer.Contains("CultMesh.OperationInvocation(template.Operation)", StringComparison.Ordinal) ||
        !upstreamUnityUiToolkitLowerer.Contains("CultMesh.OperationPayload(component.Props)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The shared Eve Unity UI Toolkit lowerer no longer emits CultMesh operation invocation descriptors and payloads.");
    }

    if (runtimeSurfaceBuilder.Contains("public sealed class AetheriaRuntimeSurfaceStateBinding", StringComparison.Ordinal) ||
        !runtimeSurfaceBuilder.Contains("public IReadOnlyList<CultMeshStateBindingDescriptor> StateBindings", StringComparison.Ordinal) ||
        !runtimeSurfaceBuilder.Contains("public static CultMeshStateBindingDescriptor ForDaemonStateRef(", StringComparison.Ordinal) ||
        !runtimeSurfaceBuilder.Contains("public CultMeshOperationBindingDescriptor Operation", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime surfaces should use CultMesh state and operation descriptors directly instead of local live binding DTOs.");
    }

    if (!runtimeAdapter.Contains("component.StateBindings.Select(ToCultMeshStateBinding).ToArray()", StringComparison.Ordinal) ||
        !runtimeAdapter.Contains("private static CultMeshStateBindingDescriptor ToCultMeshStateBinding(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime surfaces no longer hand CultMesh state bindings to the shared Eve component model.");
    }

    if (!runtimeSurfaceBuilder.Contains("CultMesh.StateBindingRecord(binding)", StringComparison.Ordinal) ||
        !runtimeAdapter.Contains("CultMesh.StateBindingRecord(", StringComparison.Ordinal) ||
        !runtimeAdapter.Contains(".ToBinding()", StringComparison.Ordinal) ||
        runtimeSurfaceBuilder.Contains("CultMesh.RouteRecord(binding.RouteHint)", StringComparison.Ordinal) ||
        runtimeAdapter.Contains("CultMesh.RouteRecord(binding.RouteKind, binding.RouteDescription)", StringComparison.Ordinal) ||
        runtimeSurfaceBuilder.Contains("ParseRouteKind(", StringComparison.Ordinal) ||
        runtimeAdapter.Contains("ParseRouteKind(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime surfaces reintroduced local state binding flattening; state binding persistence belongs to CultMesh.StateBindingRecord.");
    }

    var surfaceCommandProjectors = string.Join(
        "\n",
        runtimeSurfaceStateProjector,
        playerSettingsSurfaceProjector,
        catalogSurfaceProjector,
        operationsSurfaceProjector);
    if (!runtimeCultCacheDocumentStore.Contains("CultMesh.OperationBindingRecord(command.Operation)", StringComparison.Ordinal) ||
        !runtimeCultCacheDocumentStore.Contains("CultMesh.OperationBindingRecord(", StringComparison.Ordinal) ||
        !runtimeCultCacheDocumentStore.Contains("routeDescription).ToBinding()", StringComparison.Ordinal) ||
        !runtimeAdapter.Contains("CultMesh.OperationBindingRecord(", StringComparison.Ordinal) ||
        !runtimeAdapter.Contains("command.SchemaId", StringComparison.Ordinal) ||
        !surfaceCommandProjectors.Contains("CultMesh.OperationBindingRecord(", StringComparison.Ordinal) ||
        runtimeCultCacheDocumentStore.Contains("new CultMeshOperationBindingDescriptor(", StringComparison.Ordinal) ||
        runtimeAdapter.Contains("new CultMeshOperationBindingDescriptor(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime surfaces reintroduced local operation binding flattening; surface command persistence belongs to CultMesh.OperationBindingRecord.");
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
        "AetheriaRuntimeVerseClient.cs",
        "AetheriaRuntimeSnapshotDocuments.cs",
        "AetheriaRuntimeEveCommandDocument.cs",
        "AetheriaRuntimeEveSurfaceState.cs",
        "AetheriaRuntimeDaemonDocuments.cs",
        "AetheriaRuntimeDaemonSoaDocuments.cs",
        "AetheriaRuntimeRtsViewportDocuments.cs",
        "AetheriaRuntimeAssetDocuments.cs",
        "AetheriaRuntimeRenderSplatDocuments.cs",
        "AetheriaRuntimeSettingsDocuments.cs",
        "AetheriaRuntimeStarbridgeDocuments.cs",
        "AetheriaRuntimeStarbridgePlayerSeatDocuments.cs",
        "AetheriaRuntimeVerseAuthorityPolicy.cs"
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
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintMaterializer.cs"),
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
    var runtimeProjectionDirectory = Path.Combine(root, "Assets", "Scripts", "ServerShared", "RuntimeProjection");
    if (Directory.Exists(runtimeProjectionDirectory))
    {
        var survivors = Directory.EnumerateFileSystemEntries(runtimeProjectionDirectory)
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        throw new InvalidOperationException(
            "Dead runtime-projection helper directory should stay deleted; move live helpers under explicit runtime owners: " +
            string.Join(", ", survivors));
    }

    var selectionExtensionsPath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "EnumerableSelectionExtensions.cs");
    var sharedProjectPath = Path.Combine(root, "Aetheria.Shared.Unity.csproj");
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var selectionExtensions = File.Exists(selectionExtensionsPath)
        ? File.ReadAllText(selectionExtensionsPath)
        : "";
    var sharedProject = File.Exists(sharedProjectPath)
        ? File.ReadAllText(sharedProjectPath)
        : "";
    var tradeMenu = File.Exists(tradeMenuPath)
        ? File.ReadAllText(tradeMenuPath)
        : "";

    if (!selectionExtensions.Contains("public static T MaxBy<T, U>", StringComparison.Ordinal) ||
        !selectionExtensions.Contains("public static T MinBy<T, U>", StringComparison.Ordinal) ||
        !sharedProject.Contains(@"Assets\Scripts\ServerShared\EnumerableSelectionExtensions.cs", StringComparison.Ordinal) ||
        sharedProject.Contains("RuntimeProjection", StringComparison.Ordinal) ||
        tradeMenu.Contains(".FormatTypeName()", StringComparison.Ordinal) ||
        !tradeMenu.Contains("private static string FormatTypeName(string typeName)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Live RuntimeProjection remnants must move into explicit owners: enumerable selection helpers under ServerShared and trade labels inside TradeMenu.");
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
        Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintMaterializer.cs"),
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
        [Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintMaterializer.cs")] = new[]
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
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityCurrentEntityPresentation.cs")] = new[]
        {
            "AetheriaRuntimeZoneRenderDocument zoneRender",
            "zoneRender?.BodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>()",
            "string.Equals(body.OrbitKey ?? \"\", orbital.OrbitKey, StringComparison.Ordinal)",
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
            "private void SyncDaemonBodyViews()",
            "private IReadOnlyList<AetheriaRuntimeBodySnapshotCommit> _zoneRenderBodies",
            "_zoneRenderBodies = render?.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>();",
            "foreach (var bodyPose in _zoneRenderBodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>())",
            "ResolveDaemonRenderViewport()",
            "private AetheriaRuntimeXzRect ResolveDaemonRenderViewport()",
            "private void UnloadBodyView(string bodyKey)",
            "foreach (var bodyView in _daemonBodyViews)",
            "foreach (var pose in _daemonBodyPoses)",
            "beltPosesByBodyKey.TryGetValue(bodyKey, out var beltPose)",
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
    var targetPresentationPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityTargetPresentation.cs");
    var targetPresentation = File.Exists(targetPresentationPath)
        ? File.ReadAllText(targetPresentationPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaUnityTargetPresentation.cs is missing.");
    var pilotFrameControllerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotFrameController.cs");
    var pilotFrameController = File.Exists(pilotFrameControllerPath)
        ? File.ReadAllText(pilotFrameControllerPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaUnityPilotFrameController.cs is missing.");
    var gameplayInputShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayInputShell.cs");
    var gameplayInputShell = File.Exists(gameplayInputShellPath)
        ? File.ReadAllText(gameplayInputShellPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaUnityGameplayInputShell.cs is missing.");
    var renderSettingsBridgePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityRenderSettingsBridge.cs");
    var renderSettingsBridge = File.Exists(renderSettingsBridgePath)
        ? File.ReadAllText(renderSettingsBridgePath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaUnityRenderSettingsBridge.cs is missing.");
    var cockpitHudShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityCockpitHudShell.cs");
    var cockpitHudShell = File.Exists(cockpitHudShellPath)
        ? File.ReadAllText(cockpitHudShellPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaUnityCockpitHudShell.cs is missing.");
    var gameplayBootShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayBootShell.cs");
    var gameplayBootShell = File.Exists(gameplayBootShellPath)
        ? File.ReadAllText(gameplayBootShellPath)
        : throw new InvalidOperationException("Cannot verify daemon render query authority; AetheriaUnityGameplayBootShell.cs is missing.");
    var unityRenderPresentation = actionGameManager + "\n" + targetPresentation + "\n" + pilotFrameController + "\n" + gameplayInputShell + "\n" + renderSettingsBridge + "\n" + cockpitHudShell + "\n" + gameplayBootShell;

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
        "public double HeatstrokePhasingFloor { get; }",
        "public double HeatstrokePhasingFrequency { get; }",
        "public double TargetSpottedBlinkFrequency { get; }",
        "public double TargetSpottedBlinkOffset { get; }",
        "public IReadOnlyList<double> MinimapZoomLevels { get; }",
        "public int DefaultMinimapZoom { get; }",
        "public double WormholeDistanceRatio { get; }",
        "public double DefaultViewDistance { get; }",
        "public double MinimapIconScale { get; }",
        "public double MinimapAsteroidSize { get; }",
        "public AetheriaRuntimeExponentialCurve BodyIconSizeCurve { get; }",
        "public double MinimapZoneGravityRange { get; }",
        "public double AsteroidVerticalOffset { get; }",
        "public double PlanetRotationSpeed { get; }",
        "public double ZoneBoundaryPower { get; }",
        "public double ZoneBoundaryDepth { get; }",
        "public int AsteroidMeshCount { get; }",
        "public AetheriaRuntimeExponentialCurve BodyRadiusCurve { get; }",
        "public AetheriaRuntimeExponentialCurve LightRadiusCurve { get; }",
        "public AetheriaRuntimeExponentialCurve GravityWaveFrequencyCurve { get; }",
        "public int ResolveDefaultMinimapZoomIndex()",
        "public int ResolveNextMinimapZoomIndex(int currentIndex)",
        "public double ResolveMinimapDistance(int zoomIndex)",
        "public double ResolveDefaultMinimapDistance()",
        "public double ResolveMinimapIconSize(double minimapDistance)",
        "public double ResolveBodyIconSize(double mass)",
        "public double ResolveBodyRadius(double mass)",
        "public double ResolveLightRadius(double mass)",
        "public double ResolveGravityWaveFrequency(double mass)",
        "public double NormalizeThermalRisk(double temperature)",
        "public double NormalizeHeatstrokePost(double heatstroke)",
        "public double NormalizeSevereHeatstrokePost(double heatstroke)",
        "public double ResolveSevereHeatstrokePostWeight(double heatstroke, double timeSeconds)",
        "public double NormalizeDetectionProgress(double infoGathered)",
        "public bool ResolveTargetSpottedFillEnabled(double infoGathered, double timeSeconds)",
        "public double NormalizeTargetVisibilityFill(double infoGathered)",
        "public double NormalizeVisibilityToTargetFill(double infoGathered)",
        "public double NormalizeTargetStatusFill(double normalizedValue)",
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
        "AetheriaRuntimeXzRect viewport)",
        "public static int QueryBodyViews(",
        "IntersectsCircle(",
        "Math.Max(ResolveGravityRadius(body), ResolveWaveRadius(body))",
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
        "public static int[] QueryPresentationEntityIndices(",
        "public static int QueryPresentationEntityIndices(",
        "TryParseEntityIndex(run?.CurrentEntityKey)",
        "ContainsPoint(viewport, entity.PositionX, entity.PositionZ)",
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

    if (zoneRenderer.Contains("Settings.GameplaySettings.TargetDetectionInfoThreshold", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon visibility through shared render settings instead of Unity GameplaySettings.");
    }

    if (ContainsUnitySettingsMember(zoneRenderer, "MinimapZoomLevels") ||
        ContainsUnitySettingsMember(zoneRenderer, "DefaultMinimapZoom") ||
        ContainsUnitySettingsMember(zoneRenderer, "MinimapIconSize") ||
        ContainsUnitySettingsMember(zoneRenderer, "DefaultViewDistance") ||
        ContainsUnitySettingsMember(zoneRenderer, "MinimapAsteroidSize") ||
        ContainsUnitySettingsMember(zoneRenderer, "IconSize") ||
        ContainsUnitySettingsMember(zoneRenderer, "MinimapZoneGravityRange") ||
        ContainsUnitySettingsMember(zoneRenderer, "PlanetRotationSpeed") ||
        ContainsUnitySettingsMember(zoneRenderer, "AsteroidMeshCount"))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must initialize view/minimap presentation through shared daemon render settings instead of Unity GameSettings.");
    }

    if (zoneRenderer.Contains(" Settings.WormholeDistanceRatio", StringComparison.Ordinal) ||
        zoneRenderer.Contains("(Settings.WormholeDistanceRatio", StringComparison.Ordinal) ||
        zoneRenderer.Contains(", Settings.WormholeDistanceRatio", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon wormhole exits through shared render settings instead of Unity GameSettings.");
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
            "ZoneRenderer must use the daemon-indexed observed entity projection supplied by gameplay instead of enumerating Unity Zone.Entities.");
    }

    if (zoneRenderer.Contains("_legacyEntityFacadeZone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("_legacyEntityFacadeZone?.Radius", StringComparison.Ordinal) ||
        zoneRenderer.Contains("_legacyLootFacadeZone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("Zone legacyLootFacadeZone", StringComparison.Ordinal) ||
        zoneRenderer.Contains("gridObject.Zone = ", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must not keep Unity Zone facade handles; daemon snapshots and observed entity projections own renderer input.");
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
        "private string _daemonCurrentEntityKey = \"\";",
        "private double _daemonSimulationTimeSeconds;",
        "private readonly List<AetheriaRuntimeDaemonBodyView> _daemonBodyViews",
        "private IReadOnlyList<AetheriaRuntimeBodySnapshotCommit> _zoneRenderBodies",
        "private readonly List<AetheriaRuntimeZoneRenderBodyPose> _daemonBodyPoses",
        "private readonly Dictionary<string, AetheriaRuntimeZoneRenderBodyPose> _daemonBodyPosesByBodyKey",
        "private readonly Dictionary<string, PlanetObject> _bodyViewsByBodyKey",
        "public bool TryGetBodyView(string bodyKey, out PlanetObject bodyView)",
        "private readonly List<AetheriaRuntimeZoneRenderAsteroidBeltPose> _daemonAsteroidBeltPoses",
        "private readonly List<AetheriaRuntimeZoneRenderAsteroidInstancePose> _visibleAsteroidInstancePoses",
        "private readonly List<AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkers",
        "private readonly Dictionary<int, AetheriaRuntimeDaemonCompassMarker> _daemonCompassMarkersByEntityIndex",
        "private readonly Dictionary<int, AetheriaRuntimeZoneTargetRow> _daemonTargetRowsByEntityIndex",
        "private readonly List<AetheriaRuntimeZoneContactRow> _daemonContactRows",
        "private IReadOnlyDictionary<int, Entity> _observedEntitySnapshotsByDaemonIndex",
        "private readonly List<int> _daemonPresentationEntityIndices",
        "private readonly HashSet<int> _daemonPresentationEntityIndicesSet",
        "private readonly List<int> _daemonVisibleEntityIndices",
        "private readonly HashSet<int> _daemonVisibleEntityIndicesSet",
        "private readonly HashSet<int> _visibleDaemonEntityIndices",
        "private AetheriaDaemonObserver _daemonObserver;",
        "private readonly List<AetheriaRuntimeDaemonWormholeExit> _daemonWormholeExits",
        "render?.WormholeExits ?? Array.Empty<AetheriaRuntimeZoneRenderWormholeExit>()",
        "_daemonWormholeExits.Add(new AetheriaRuntimeDaemonWormholeExit(",
        "public Dictionary<int, (GameObject gravity, CompassIcon icon)> WormholeInstances",
        "public void LoadDaemonZoneView(",
        "IReadOnlyDictionary<int, Entity> observedEntitySnapshotsByDaemonIndex,",
        "AetheriaRuntimeZoneRenderDocument render)",
        "public BodySettingsCollection[] BodySettingsCollections;",
        "public void ApplyZoneRender(AetheriaRuntimeZoneRenderDocument render)",
        "_daemonCurrentEntityKey = render?.CurrentEntityKey ?? \"\";",
        "_daemonSimulationTimeSeconds = render?.SimulationTimeSeconds ?? 0;",
        "_zoneRenderBodies = render?.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>();",
        "_observedEntitySnapshotsByDaemonIndex = observedEntitySnapshotsByDaemonIndex;",
        "render?.ZoneRenderRadius ?? 2000",
        "private readonly HashSet<string> _daemonVisibleBodyKeys",
        "private void SyncDaemonBodyViews()",
        "foreach (var bodyView in _daemonBodyViews)",
        "if (bodyView.IsAsteroidBelt)",
        "foreach (var bodyPose in _zoneRenderBodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>())",
        "ResolveDaemonRenderViewport()",
        "private AetheriaRuntimeXzRect ResolveDaemonRenderViewport()",
        "private static AetheriaRuntimeRtsViewportBounds ToViewportBounds(AetheriaRuntimeXzRect viewport)",
        "private void UnloadBodyView(string bodyKey)",
        "foreach (var pose in _daemonBodyPoses)",
        "LoadPlanet(body)",
        "void LoadPlanet(AetheriaRuntimeBodySnapshotCommit body)",
        "beltPosesByBodyKey.TryGetValue(bodyKey, out var beltPose)",
        "LoadAsteroidBelt(beltPose)",
        "void LoadAsteroidBelt(AetheriaRuntimeZoneRenderAsteroidBeltPose beltPose)",
        "private void SyncDaemonEntityInstances()",
        "TryCollectDaemonPresentationEntityIndicesFromSoa(viewport)",
        "private bool TryCollectDaemonPresentationEntityIndicesFromSoa(AetheriaRuntimeXzRect viewport)",
        "observer.LastRenderNativeView",
        "!view.IsCreated || !view.HasEntityIndex",
        "view.HasRenderVisibility && view.RenderVisibility[i] == 0",
        "var position = view.Position[i];",
        "var entityIndex = view.EntityIndex[i];",
        "private AetheriaDaemonObserver ResolveDaemonObserver()",
        "FindAnyObjectByType<AetheriaDaemonObserver>()",
        "ResolveObjectsViewport(viewport)",
        "private AetheriaRuntimeObjectsViewportDocument ResolveObjectsViewport(AetheriaRuntimeXzRect viewport)",
        ".Document<AetheriaRuntimeObjectsViewportDocument>(viewportBounds)",
        ".Reactive();",
        "_objectsViewport?.Current",
        "foreach (var entity in objects?.Objects ?? Array.Empty<AetheriaRuntimeRtsViewportObject>())",
        "_observedEntitySnapshotsByDaemonIndex.TryGetValue(entityIndex, out var entity)",
        "Loading entity {entity.Name} from daemon presentation query",
        "UnloadEntity(pair.Value.Entity)",
        "private readonly Dictionary<int, EntityInstance> _entityInstancesByDaemonIndex",
        "public IReadOnlyDictionary<int, EntityInstance> DaemonEntityInstances => _entityInstancesByDaemonIndex;",
        "public bool TryGetEntityInstance(int daemonEntityIndex, out EntityInstance instance)",
        "public bool TryGetEntityInstance(Entity entity, out EntityInstance instance)",
        "public bool TryGetDaemonTargetDistance(int daemonEntityIndex, out float distance)",
        "_daemonTargetRowsByEntityIndex.TryGetValue(daemonEntityIndex, out var target)",
        "foreach (var entity in EntityInstances.Keys.ToArray())",
        "_daemonBodyPoses.AddRange(_zoneRenderBodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>());",
        "_daemonAsteroidBeltPoses.AddRange(_zoneRenderAsteroidBeltPoses ?? Array.Empty<AetheriaRuntimeZoneRenderAsteroidBeltPose>());",
        "foreach (var beltPose in _daemonAsteroidBeltPoses)",
        "beltPose.InstancePoses ?? Array.Empty<AetheriaRuntimeZoneRenderAsteroidInstancePose>()",
        "private void RefreshDaemonContactRows()",
        ".State",
        ".Document<AetheriaRuntimeZoneContactsDocument>().Reactive()",
        "private void RefreshDaemonCompassMarkers()",
        "private void RefreshDaemonVisibleEntityInstances()",
        "PowerPulse(",
        "RadialWaves(",
        "RenderSettings.TargetDetectionInfoThreshold",
        "RenderSettings.DefaultViewDistance",
        "RenderSettings.MinimapAsteroidSize",
        "RenderSettings.ResolveDefaultMinimapDistance()",
        "RenderSettings.ResolveMinimapIconSize(value)",
        "RenderSettings.ResolveBodyIconSize(mass)",
        "RenderSettings.MinimapZoneGravityRange",
        "RenderSettings.AsteroidVerticalOffset",
        "RenderSettings.PlanetRotationSpeed",
        "RenderSettings.ZoneBoundaryPower",
        "RenderSettings.ZoneBoundaryDepth",
        "RenderSettings.AsteroidMeshCount",
        "RenderSettings.ResolveBodyRadius(mass)",
        "RenderSettings.ResolveLightRadius(mass)",
        "RenderSettings.ResolveGravityWaveFrequency(mass)",
        "AddWormhole(exit)",
        "public void AddWormhole(AetheriaRuntimeDaemonWormholeExit exit)",
        "private double DaemonSimulationTimeSeconds => _daemonSimulationTimeSeconds;",
        "_daemonCompassMarkersByEntityIndex.TryGetValue(entityInstance.DaemonEntityIndex",
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

    if (zoneRenderer.Contains("public GameSettings Settings", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer still accepts the whole Unity GameSettings object instead of explicit renderer asset/tuning inputs.");
    }

    if (zoneRenderer.Contains("foreach (var entitySnapshot in daemonZone?.Entities", StringComparison.Ordinal) ||
        zoneRenderer.Contains("Loading entity {entity.Name} from daemon entity snapshot", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer must query daemon presentation entities instead of loading every daemon entity snapshot into a mirrored Unity level.");
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

    if (schematicDisplay.Contains("ActionGameManager.Instance.ZoneRenderer", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "SchematicDisplay must not reach through ActionGameManager.Instance.ZoneRenderer for HUD presentation settings.");
    }

    var requiredSchematicHudSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog",
        "CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity",
        "ResolveCatalog()",
        "ResolveCurrentEntityHudStatus()",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "ResolveClient().State.Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "ResolveClient().State.Document<AetheriaRuntimeCurrentEntityDocument>().Reactive()",
        "return _currentEntity?.Current?.Hud ?? new AetheriaRuntimeCurrentEntityHudStatus();",
        "_currentEntity?.Dispose()",
        "hud.OverrideShutdown",
        "hud.HeatsinksEnabled",
        "hud.Heatstroke",
        "hud.Hypothermia",
        "hud.Visibility",
        "hud.HullDurabilityRatio"
    };
    var missingSchematicHudSymbols = requiredSchematicHudSymbols
        .Where(symbol => !schematicDisplay.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSchematicHudSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "SchematicDisplay no longer renders player HUD facts from the typed current-entity document: " +
            string.Join(", ", missingSchematicHudSymbols));
    }

    var forbiddenSchematicHudSymbols = new[]
    {
        "_entity.OverrideShutdown",
        "_entity.HeatsinksEnabled",
        "_entity.Heatstroke",
        "_entity.Hypothermia"
    };
    var presentForbiddenSchematicHudSymbols = forbiddenSchematicHudSymbols
        .Where(symbol => schematicDisplay.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (presentForbiddenSchematicHudSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "SchematicDisplay player HUD facts must not come from the Unity entity facade: " +
            string.Join(", ", presentForbiddenSchematicHudSymbols));
    }

    if (!schematicDisplay.Contains("public void SetRenderSettings(AetheriaRuntimeDaemonRenderSettings renderSettings)", StringComparison.Ordinal) ||
        !schematicDisplay.Contains("_renderSettings?.NormalizeThermalRisk(", StringComparison.Ordinal) ||
        !gameplayBootShell.Contains("CockpitHudShell.SetRenderSettings(ZoneRenderer.RenderSettings)", StringComparison.Ordinal) ||
        !cockpitHudShell.Contains("public sealed class AetheriaUnityCockpitHudShell", StringComparison.Ordinal) ||
        !cockpitHudShell.Contains("public void SetRenderSettings(AetheriaRuntimeDaemonRenderSettings renderSettings)", StringComparison.Ordinal) ||
        !cockpitHudShell.Contains("SchematicDisplay?.SetRenderSettings(renderSettings)", StringComparison.Ordinal) ||
        !cockpitHudShell.Contains("TargetSchematicDisplay?.SetRenderSettings(renderSettings)", StringComparison.Ordinal) ||
        !renderSettingsBridge.Contains("settings.GameplaySettings.HypothermiaTemperature", StringComparison.Ordinal) ||
        !renderSettingsBridge.Contains("settings.GameplaySettings.HeatstrokeTemperature", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "HUD thermal presentation no longer flows through the shared daemon render settings bridge.");
    }

    var requiredActionGameManagerRenderSymbols = new[]
    {
        "public static class AetheriaUnityRenderSettingsBridge",
        "public static AetheriaRuntimeDaemonRenderSettings Build(",
        "AetheriaUnityRenderSettingsBridge.Build(",
        "public sealed class AetheriaUnityCockpitHudShell",
        "public sealed class AetheriaUnityGameplayBootShell",
        "CockpitHudShell.SetRenderSettings(ZoneRenderer.RenderSettings)",
        "renderSettings.NormalizeDetectionProgress(",
        "renderSettings.ResolveTargetSpottedFillEnabled(",
        "renderSettings.NormalizeHeatstrokePost(",
        "renderSettings.ResolveSevereHeatstrokePostWeight(",
        "renderSettings.NormalizeTargetVisibilityFill(",
        "renderSettings.NormalizeVisibilityToTargetFill(",
        "renderSettings.NormalizeTargetStatusFill(",
        "renderSettings.ResolveLockIndicatorNoiseAmplitude(",
        "renderSettings.ResolveLockIndicatorNoiseFrequency(",
        "renderSettings.ResolveLockSpinSpeed(",
        "ZoneRenderer.RenderSettings.ResolveDefaultMinimapZoomIndex()",
        "ZoneRenderer.RenderSettings.ResolveNextMinimapZoomIndex(_zoomLevelIndex)",
        "ZoneRenderer.RenderSettings.ResolveMinimapDistance(_zoomLevelIndex)",
        "new AetheriaRuntimeExponentialLerp(",
        "settings.GameplaySettings.TargetDetectionInfoThreshold",
        "settings.GameplaySettings.SevereHeatstrokeRiskThreshold",
        "settings.GameplaySettings.LockIndicatorNoiseAmplitude",
        "settings.HeatstrokePhasingFloor",
        "settings.HeatstrokePhasingFrequency",
        "TargetSpottedBlinkFrequency",
        "TargetSpottedBlinkOffset",
        "settings.MinimapZoomLevels",
        "settings.DefaultMinimapZoom",
        "settings.WormholeDistanceRatio",
        "settings.DefaultViewDistance",
        "settings.MinimapIconSize",
        "settings.MinimapAsteroidSize",
        "settings.IconSize",
        "settings.MinimapZoneGravityRange",
        "settings.PlanetSettings.AsteroidVerticalOffset",
        "settings.PlanetRotationSpeed",
        "settings.PlanetSettings.ZoneDepthExponent",
        "settings.PlanetSettings.ZoneDepth + settings.PlanetSettings.ZoneBoundaryFog",
        "settings.AsteroidMeshCount",
        "settings.PlanetSettings.BodyRadius",
        "settings.PlanetSettings.LightRadius",
        "settings.PlanetSettings.WaveFrequency",
        "ZoneRenderer.BodySettingsCollections = Settings.BodySettingsCollections;"
    };

    var missingActionGameManagerRenderSymbols = requiredActionGameManagerRenderSymbols
        .Where(symbol => !unityRenderPresentation.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingActionGameManagerRenderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity target/render presentation no longer bridges Unity render tuning through shared daemon render settings: " +
            string.Join(", ", missingActionGameManagerRenderSymbols));
    }

    var zoneRendererPresentationSettings = new Dictionary<string, string>
    {
        ["ActionGameManager.Instance.Settings.PlanetSettings.AsteroidVerticalOffset"] = "ActionGameManager.Instance.Settings.PlanetSettings.AsteroidVerticalOffset",
        ["PlanetRotationSpeed"] = "Settings.PlanetRotationSpeed",
        ["Settings.PlanetSettings.ZoneDepthExponent"] = "Settings.PlanetSettings.ZoneDepthExponent",
        ["Settings.PlanetSettings.ZoneDepth + Settings.PlanetSettings.ZoneBoundaryFog"] = "Settings.PlanetSettings.ZoneDepth + Settings.PlanetSettings.ZoneBoundaryFog",
        ["AsteroidMeshCount"] = "Settings.AsteroidMeshCount",
        ["Settings.PlanetSettings.BodyRadius"] = "Settings.PlanetSettings.BodyRadius",
        ["Settings.PlanetSettings.LightRadius"] = "Settings.PlanetSettings.LightRadius",
        ["Settings.PlanetSettings.WaveFrequency"] = "Settings.PlanetSettings.WaveFrequency"
    };
    var survivingZoneRendererPresentationSettings = zoneRendererPresentationSettings
        .Where(symbol =>
            symbol.Key == "PlanetRotationSpeed" || symbol.Key == "AsteroidMeshCount"
                ? ContainsUnitySettingsMember(zoneRenderer, symbol.Key)
                : zoneRenderer.Contains(symbol.Key, StringComparison.Ordinal))
        .Select(symbol => symbol.Value)
        .ToArray();
    if (survivingZoneRendererPresentationSettings.Length > 0)
    {
        throw new InvalidOperationException(
            "ZoneRenderer still reads zone presentation tuning from Unity GameSettings instead of shared daemon render settings: " +
            string.Join(", ", survivingZoneRendererPresentationSettings));
    }

    var forbiddenRenderLoopSymbols = new[]
    {
        "Settings.GameplaySettings.TargetDetectionInfoThreshold",
        "Settings.GameplaySettings.SevereHeatstrokeRiskThreshold",
        "Settings.GameplaySettings.LockIndicatorNoiseAmplitude",
        "Settings.GameplaySettings.LockIndicatorFrequency",
        "Settings.GameplaySettings.LockSpinSpeed",
        "Settings.HeatstrokePhasingFloor",
        "Settings.HeatstrokePhasingFrequency",
        "TargetSpottedBlinkFrequency",
        "TargetSpottedBlinkOffset",
        "Mathf.Lerp(.25f, .75f,"
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

    var minimapBootHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "Settings.MinimapZoomLevels", "Settings.DefaultMinimapZoom" })
        .Where(hit => hit.MethodName == "Start")
        .ToArray();
    if (minimapBootHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager must bootstrap minimap zoom through shared daemon render settings instead of indexing Unity GameSettings directly: " +
            string.Join(", ", minimapBootHits.Select(hit => $"{hit.MethodName}:{hit.LineNumber}:{hit.Line.Trim()}")));
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
        Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs")
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
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs")] = new[]
        {
            "public sealed class AetheriaRuntimeZoneSnapshotCommit",
            "public sealed class AetheriaRuntimeOrbitSnapshotCommit",
            "public sealed class AetheriaRuntimeBodySnapshotCommit",
            "public string OrbitKey { get; set; } = \"\";",
            "public string ParentOrbitKey { get; set; } = \"\";",
            "public string BodyKey { get; set; } = \"\";",
            "public double GravityInfluenceCenterX { get; set; }",
            "public double GravityInfluenceCenterZ { get; set; }",
            "public double GravityInfluenceRadius { get; set; }",
            "public double GravityWellDepth { get; set; }",
            "public double GravityWaveRadius { get; set; }",
            "public double GravityWaveDepth { get; set; }",
            "public double GravityWaveSpeed { get; set; }",
            "public double GravityTerrainRadius { get; set; }",
            "public double GravityTerrainDepth { get; set; }",
            "public double GravityTerrainDepthExponent { get; set; }",
            "public double GravityTerrainBoundaryFog { get; set; }",
            "public double GravityTerrainWaveFrequency { get; set; }"
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
            "Typed zone-state documents and managed runtime snapshot commits must expose orbit/body key ownership explicitly: " +
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
    var observedEntityRestorerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedEntityRestorer.cs");

    var itemManager = File.Exists(itemManagerPath)
        ? File.ReadAllText(itemManagerPath)
        : throw new InvalidOperationException("Cannot verify typed runtime behavior coverage; ItemManager.cs is missing.");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify typed runtime behavior coverage; ActionGameManager.cs is missing.");
    var observedEntityRestorer = File.Exists(observedEntityRestorerPath)
        ? File.ReadAllText(observedEntityRestorerPath)
        : throw new InvalidOperationException("Cannot verify typed runtime behavior coverage; AetheriaUnityObservedEntityRestorer.cs is missing.");
    var runtimeRestoreSource = actionGameManager + "\n" + observedEntityRestorer;

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
        .Where(symbol => !runtimeRestoreSource.Contains(symbol, StringComparison.Ordinal))
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
        .Where(symbol => !runtimeRestoreSource.Contains(symbol, StringComparison.Ordinal))
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
        "ReadDaemonSurface(statePath)",
        "ReadDaemonSurface(stateBoot.StateFilePath)",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(",
        "CultMeshReactiveDocument<global::Aetheria.State.Documents.EveSurfaceState>",
        "ResolveReactiveDaemonSurfaceState(",
        "client.State.Document<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Game).Reactive()",
        "client.State.Document<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.GameTui).Reactive()",
        "client.State.Document<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Editor).Reactive()",
        "client.State.Document<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.EditorTui).Reactive()",
        "DisposeReactiveSurfaceState()",
        "AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(",
        "private bool ShouldMountSurface(",
        "_mountedSurfaceVersion != surface.Version",
        "private static readonly AetheriaEveUnitySurfaceChrome RootOnlyChrome",
        "UseShell = false",
        "MountSurface(string statePath, EveSurfaceDocument surface)",
        "AetheriaEveUnitySurfaceHost.Render(",
        "AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(ResolveClient(statePath), request, out var daemonEnvelope)",
        ".Ui.SurfaceCommandAsync("
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

    if (presenter.Contains("AetheriaRuntimeStateReader.ReadEveSurface", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter still mounts daemon surfaces through file-reader lookup instead of the shared AetheriaClient facade.");
    }

    if (presenter.Contains(".DaemonGameSurfaceAsync()", StringComparison.Ordinal) ||
        presenter.Contains(".DaemonGameTuiSurfaceAsync()", StringComparison.Ordinal) ||
        presenter.Contains(".DaemonEditorSurfaceAsync()", StringComparison.Ordinal) ||
        presenter.Contains(".DaemonEditorTuiSurfaceAsync()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.ReactiveGameSurface()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.ReactiveGameTuiSurface()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.ReactiveEditorSurface()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.ReactiveEditorTuiSurface()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.GameSurface.LatestAsync()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.GameTuiSurface.LatestAsync()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.EditorSurface.LatestAsync()", StringComparison.Ordinal) ||
        presenter.Contains("client.State.Daemon.EditorTuiSurface.LatestAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter still reads daemon surfaces through one-shot compatibility helpers instead of managed reactive document handles.");
    }

    if (presenter.Contains("new EveUiToolkitSurfaceLowerer", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve presenter still lowers daemon-published surfaces directly instead of delegating to the shared Unity Eve surface host.");
    }

    var daemonSubmitIndex = presenter.IndexOf(
        "AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(ResolveClient(statePath), request, out var daemonEnvelope)",
        StringComparison.Ordinal);
    var eveSubmitIndex = presenter.IndexOf(
        ".Ui.SurfaceCommandAsync(",
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
        "AetheriaClient",
        "client.CurrentDaemonFrame()",
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

    if (daemonSurfaceCommands.Contains("AetheriaRuntimeStateReader.TryReadDaemonRenderView", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("client.State.ReactiveDaemonFrame()", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("TryReactiveDaemonSoaView(client)", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("client.State.ReactiveZoneRender()", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("client.CurrentObservedDaemon()", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("client.State.CurrentObservedDaemon()", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("client.State.ReactiveObservedDaemon()", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("observedState.TryCurrent(out var current)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing still reads observed daemon state through an aggregate reader instead of direct managed typed documents.");
    }

    if (daemonSurfaceCommands.Contains(".ReadAsync(client.State)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing still samples observed daemon state through a one-shot compatibility helper instead of a managed reactive document.");
    }

    if (daemonSurfaceCommands.Contains("ObserveAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing still reads observed daemon state through the AetheriaClient compatibility helper.");
    }

    if (daemonSurfaceCommands.Contains(".OpenAsync(", StringComparison.Ordinal) ||
        daemonSurfaceCommands.Contains("string stateFilePath", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon Eve surface command routing still hides the managed client boundary behind a state-file-path shortcut.");
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
        "public static RuntimePlayerSettings RuntimePlayerSettings",
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
        ".Ui.SurfaceCommandAsync(request, \"unity-main-menu\")",
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
        "ResolvePlayerSettings(CurrentStateBoot())",
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
        "ResolvePlayerSettings(",
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
        "ResolveSectorMap(stateBoot)",
        "ResolveVerseHostSettings(stateBoot)",
        "ResolvePlayerSettings(stateBoot)",
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
        "public void AddField(string name, Func<int> read, Action<int> write)",
        "public void SetInputGate(Action enableGlobalInput, Action disableGlobalInput)"
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
        "StatSheet",
        "ActionGameManager.Instance"
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
        "SetRuntimeInputScreenShell(Func<bool> canOpenRuntimeInputScreen, Action openRuntimeInputScreen)",
        "CanOpenRuntimeInputScreen()",
        "TryOpenRuntimeInputScreen()",
        "AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(",
        "AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(",
        "_canOpenRuntimeInputScreen?.Invoke() == true",
        "_openRuntimeInputScreen?.Invoke();"
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
        "The live remapping screen still owns drag/drop rebinding and low-level InputSystem edits.",
        "ActionGameManager.Instance.ShowInputScreenFromMenu();",
        "ActionGameManager.Instance.CanShowInputScreenFromMenu()"
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
        ".Ui.InputSettingsAsync(command, body, \"unity-input-screen\")",
        "AetheriaRuntimeInputSettingsCommandBody",
        "AetheriaRuntimeEveCommandKind.SetBindingOverride",
        "AetheriaRuntimeEveCommandKind.SetActionBarEnabled",
        "AetheriaClient",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(",
        ".State",
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

    RequireReactiveTypedDocumentAccess(
        inputScreen,
        "InputDisplayLayout",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

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
        "PlayerSettingsAsync()",
        "VerseHostSettingsAsync()",
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

    var requiredInputSettingsSubmitSymbols = new[]
    {
        "SendInputSettingsCommand(",
        ".Ui.InputSettingsAsync(",
        "AetheriaRuntimeEveCommandKind.SetBindingOverride",
        "AetheriaRuntimeEveCommandKind.SetActionBarEnabled"
    };

    var missingInputSettingsSubmitSymbols = requiredInputSettingsSubmitSymbols
        .Where(symbol => !inputScreen.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingInputSettingsSubmitSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime input writes no longer send explicit Eve input-setting commands: " +
            string.Join(", ", missingInputSettingsSubmitSymbols));
    }

    var forbiddenActionGameManagerSymbols = new[]
    {
        "RequestRuntimeInputBindingOverride",
        "RequestRuntimeActionBarInput",
        "SendRuntimeInputSettingsCommand(",
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
    var sceneWiringPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplaySceneWiring.cs");
    var menuShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityMenuShell.cs");
    var gameplayInputShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayInputShell.cs");
    if (!File.Exists(managerPath))
    {
        throw new InvalidOperationException("Cannot verify ActionGameManager input-screen delegation; ActionGameManager.cs is missing.");
    }
    if (!File.Exists(sceneWiringPath))
    {
        throw new InvalidOperationException("Cannot verify Unity scene wiring input-screen delegation; AetheriaUnityGameplaySceneWiring.cs is missing.");
    }
    if (!File.Exists(menuShellPath))
    {
        throw new InvalidOperationException("Cannot verify Unity menu shell input-screen delegation; AetheriaUnityMenuShell.cs is missing.");
    }
    if (!File.Exists(gameplayInputShellPath))
    {
        throw new InvalidOperationException("Cannot verify Unity gameplay input shell delegation; AetheriaUnityGameplayInputShell.cs is missing.");
    }

    var source = File.ReadAllText(managerPath);
    var sceneWiring = File.ReadAllText(sceneWiringPath);
    var menuShell = File.ReadAllText(menuShellPath);
    var gameplayInputShell = File.ReadAllText(gameplayInputShellPath);
    var requiredManagerSymbols = new[]
    {
        "SceneWiring.ConfigureRuntimeInputScreenShell(MenuShell)",
        "GameplayInputShell.Bootstrap();"
    };

    var missingManagerSymbols = requiredManagerSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer wires the Unity menu shell as the live input-screen owner: " +
            string.Join(", ", missingManagerSymbols));
    }

    var requiredSceneWiringSymbols = new[]
    {
        "public sealed class AetheriaUnityGameplaySceneWiring",
        "public void ConfigureRuntimeInputScreenShell(AetheriaUnityMenuShell menuShell)",
        "MainMenu?.SetRuntimeInputScreenShell(menuShell.CanOpenRuntimeInputScreen, menuShell.ShowRuntimeInputScreen)"
    };

    var missingSceneWiringSymbols = requiredSceneWiringSymbols
        .Where(symbol => !sceneWiring.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSceneWiringSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplaySceneWiring no longer wires the Unity menu shell as the live input-screen owner: " +
            string.Join(", ", missingSceneWiringSymbols));
    }

    var requiredShellSymbols = new[]
    {
        "public sealed class AetheriaUnityMenuShell",
        "public bool CanOpenRuntimeInputScreen()",
        "public void ShowRuntimeInputScreen()",
        "public void ToggleFullscreenMenu(GameObject menu)",
        "private void ShowFullscreenMenu(GameObject menu)",
        "private void HideFullscreenMenu(GameObject menu)"
    };

    var missingShellSymbols = requiredShellSymbols
        .Where(symbol => !menuShell.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingShellSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityMenuShell no longer owns the shared fullscreen primitive for the live input-screen owner: " +
            string.Join(", ", missingShellSymbols));
    }

    var requiredGameplayInputShellSymbols = new[]
    {
        "public sealed class AetheriaUnityGameplayInputShell",
        "public AetheriaUnityMenuShell MenuShell { get; set; }",
        "Input.Global.InputScreen.performed += context => MenuShell.ToggleFullscreenMenu(MenuShell.HelpScreen);",
        "Input.Global.ZoneMap.performed += context =>",
        "Input.Player.TargetNearest.performed += context => PilotOperationController.RequestTargetNearest();",
        "private void RegisterActionBarInput()",
        "DragSession.RegisterTarget(dragAction => ActionBarPresentation.RequestBinding(slot, dragAction));"
    };

    var missingGameplayInputShellSymbols = requiredGameplayInputShellSymbols
        .Where(symbol => !gameplayInputShell.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingGameplayInputShellSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplayInputShell no longer owns Unity input callbacks and routes them through typed shell dependencies: " +
            string.Join(", ", missingGameplayInputShellSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "public bool CanShowInputScreenFromMenu()",
        "public void ShowInputScreenFromMenu()",
        "private bool CanOpenRuntimeInputScreen()",
        "private void ShowRuntimeInputScreen()",
        "private void ShowFullscreenMenu(GameObject menu)",
        "private void HideFullscreenMenu(GameObject menu)",
        "Input.Global.InputScreen.performed += context",
        "Input.Global.ZoneMap.performed += context",
        "Input.Player.TargetNearest.performed += context",
        "new InputAction(binding: controlPath)",
        "OnPointerEnterAsObservable()"
    };
    var hits = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (hits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still exposes runtime input-screen menu handoff as public manager API: " +
            string.Join(", ", hits));
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
        "AetheriaUnityRuntimeClientProvider.ResolveClient(",
        ".State",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        ".Document<AetheriaRuntimeZoneDetailsDocument>(zoneIndex)",
        ".Reactive();",
        "_zoneDetails?.Current",
        "AetheriaClient",
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

    RequireReactiveTypedDocumentAccess(
        source,
        "SectorRenderer",
        "AetheriaRuntimeSectorMapDocument",
        "_sectorMap",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "AetheriaRuntimeSectorMapSession",
        ".ObserveSectorMap()");
    RequireReactiveTypedDocumentAccess(
        source,
        "SectorRenderer",
        "AetheriaRuntimeCurrentZoneDocument",
        "_currentZone",
        ".Document<AetheriaRuntimeCurrentZoneDocument>().Reactive()",
        "AetheriaRuntimeCurrentZoneSession",
        ".ObserveZone()");
    RequireReactiveTypedDocumentAccess(
        source,
        "SectorRenderer",
        "AetheriaRuntimeZoneDetailsDocument",
        "_zoneDetails",
        ".Document<AetheriaRuntimeZoneDetailsDocument>(zoneIndex).Reactive()",
        "AetheriaRuntimeZoneDetailsSession",
        ".ObserveZone(zoneIndex)");
    RequireReactiveTypedDocumentAccess(
        source,
        "SectorRenderer",
        "AetheriaRuntimeCatalogSnapshot",
        "_catalog",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "AetheriaRuntimeCatalogSession",
        "ResolveClient().State.ObserveCatalog()");
    RequireReactiveTypedDocumentAccess(
        source,
        "SectorRenderer",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

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
        "OpenRuntimeCatalog()",
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
    var localStorySurfaceBuilderPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeLocalStorySurfaceBuilder.cs");
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
    if (!File.Exists(localStorySurfaceBuilderPath))
    {
        throw new InvalidOperationException("Cannot verify runtime local story shell; AetheriaRuntimeLocalStorySurfaceBuilder.cs is missing.");
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
    var localStorySurfaceBuilder = File.ReadAllText(localStorySurfaceBuilderPath);
    var surfaceHost = File.ReadAllText(surfaceHostPath);
    var requiredSymbols = new[]
    {
        "MenuTabBinding",
        "TabBindings = Array.Empty<MenuTabBinding>();",
        "RenderTabSurface(",
        "HandleTabSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_tabSurfaceDocument)",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.Build(ComposeTabSurface())",
        "ComposeTabSurface(",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.Compose(",
        "new AetheriaRuntimeMenuTabModelOption(",
        "ResolveVisibleTabs(",
        "SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)",
        "AetheriaRuntimeCurrentDockingDocument",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking",
        "_currentDocking ??= ResolveClient().State.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()",
        "docking = _currentDocking.Current",
        "docking.IsDocked",
        "AetheriaClient",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(",
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
        "ResolveClient().State.CurrentDocking()",
        "AetheriaRuntimeObservedDockingState",
        "ProjectTabSurfaceState(",
        "ProjectTabSurface(",
        "AetheriaRuntimeMenuTabsSurfaceBuilder.Project(",
        "new AetheriaRuntimeMenuTabProjectionOption(",
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

    if (source.Contains("GameManager.IsObservedDocked", StringComparison.Ordinal) ||
        source.Contains("GameManager.TryGetObservedDockedLocalStory", StringComparison.Ordinal) ||
        source.Contains("AetheriaClientReactiveDockingState _dockingState", StringComparison.Ordinal) ||
        source.Contains("private AetheriaClientDockingSnapshot ResolveCurrentDocking()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MenuPanel tab visibility must read managed typed current-docking state instead of direct docking caches or ActionGameManager observed adapters.");
    }

    var requiredProjectionBuilderSymbols = new[]
    {
        "public sealed class AetheriaRuntimeMenuTabModelOption",
        "public static string NormalizeTabKey(string tabKey)",
        "public static AetheriaRuntimeMenuTabsSurfaceState Compose(",
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
        "TryResolveDockedLocalStory(out _currentLocation)",
        "private bool TryResolveDockedLocalStory(out LocationStory story)",
        "private bool TryResolveObservedDockingIndex(out AetheriaUnityObservedDockingIndex dockingIndex)",
        "SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)",
        "dockingIndex.TryResolveCurrentDockingBay(out var dockingBay)",
        "dockingBay?.Entity is not OrbitalEntity { Story: { } dockedStory }",
        "AetheriaRuntimeLocalStorySurfaceBuilder.Build(ProjectStorySurface())",
        "AetheriaRuntimeLocalStorySurfaceBuilder.Project(",
        "AetheriaRuntimeLocalStorySurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeLocalStoryCommandKind.Continue",
        "AetheriaRuntimeLocalStoryCommandKind.Choose",
        "new AetheriaRuntimeLocalStoryChoiceState(",
        "unity-runtime-local-story"
    };
    var dockedStoryObserverCorpus = localMenu;
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
        localMenu.Contains("AetheriaClientReactiveDockingState _dockingState", StringComparison.Ordinal) ||
        localMenu.Contains("private bool TryResolveDockingState(out AetheriaClientDockingSnapshot dockingState)", StringComparison.Ordinal) ||
        localMenu.Contains("ActionGameManager.Instance.DockedEntity", StringComparison.Ordinal) ||
        localMenu.Contains("TryGetObservedDockedLocalStory", StringComparison.Ordinal) ||
        actionGameManager.Contains("TryGetObservedDockedLocalStory", StringComparison.Ordinal) ||
        actionGameManager.Contains("IsObservedDocked", StringComparison.Ordinal) ||
        localMenu.Contains("Instantiate(ChoicePrefab", StringComparison.Ordinal) ||
        localMenu.Contains("choiceInstance.Button.onClick.AddListener", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Runtime menu/local UI must not inspect DockedEntity or rebuild legacy prefab buttons; use typed docking and the Eve local-story surface.");
    }

    var requiredLocalStoryBuilderSymbols = new[]
    {
        "public static class AetheriaRuntimeLocalStorySurfaceBuilder",
        "public const string SurfaceId = \"aetheria.runtime_menu.local_story\"",
        "public const string Continue = \"aetheria.runtime_menu.local_story.continue\"",
        "ChoiceCommandFor(int choiceIndex)",
        "public static AetheriaRuntimeLocalStorySurfaceState Project(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "public enum AetheriaRuntimeLocalStoryCommandKind",
        "public readonly struct AetheriaRuntimeLocalStoryCommand",
        "public static class AetheriaRuntimeLocalStorySurfaceCommands",
        "AetheriaRuntimeLocalStoryChoiceState",
        "AetheriaRuntimeLocalStorySurfaceState"
    };
    var missingLocalStoryBuilderSymbols = requiredLocalStoryBuilderSymbols
        .Where(symbol => !localStorySurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingLocalStoryBuilderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime local story surface builder no longer owns the local story shell contract: " +
            string.Join(", ", missingLocalStoryBuilderSymbols));
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
        "AetheriaRuntimeShipSettingsSurfaceBuilder.Build(ComposeCurrentShipSettingsSurface(",
        "AetheriaRuntimeShipSettingsSurfaceBuilder.Compose(",
        "AetheriaRuntimeShipSettingsSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeShipSettingsCommandKind.DecrementShutdownThreshold",
        "AetheriaRuntimeShipSettingsCommandKind.IncrementShutdownThreshold",
        "AetheriaRuntimeShipSettingsCommandKind.ResetShutdownThreshold",
        "AetheriaRuntimeShipSettingsCommandKind.Close",
        "AetheriaRuntimeShipSettingsSurfaceCommands.ResolveShutdownPerformance(",
        "ResolveDefaultShutdownPerformance()",
        "ResolvePlayerSettings()?.DefaultShutdownPerformance",
        "TryResolveCurrentEntityDocument(out var currentEntity)",
        "SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)",
        "private AetheriaRuntimeCurrentEntityDocument _shipSettingsCurrentEntity;",
        "!TryResolveCurrentEntityDocument(out var latestCurrentEntity)",
        "RequestEntityShutdownPerformance(",
        "latestCurrentEntity.EntityKey",
        "(float)latestCurrentEntity.ShutdownPerformance",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity",
        "ResolveCurrentEntity()",
        "currentEntity = ResolveCurrentEntity();"
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
        "ShutdownThresholdStep",
        "Mathf.Clamp01(entity.Settings.ShutdownPerformance",
        "Mathf.Clamp01(GameManager.Settings.GameplaySettings.DefaultShutdownPerformance",
        "GameManager.Settings.GameplaySettings.DefaultShutdownPerformance",
        "var entity = GameManager.CurrentEntity",
        "GameManager.CurrentEntity == null",
        "GameManager.TryGetObservedCurrentEntity(",
        "RenderCurrentShipSettingsSurface(GameManager.CurrentEntity)",
        "private Entity _shipSettingsEntity",
        "var entity = _shipSettingsEntity;",
        "entity == null || !IsCurrentEntity(entity)",
        "entity.Settings.ShutdownPerformance",
        "new AetheriaRuntimeShipSettingsSurfaceState(",
        "ProjectCurrentShipSettingsSurface(",
        "AetheriaRuntimeShipSettingsSurfaceBuilder.Project("
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
        "public static AetheriaRuntimeShipSettingsSurfaceState Compose(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "public static float ResolveShutdownPerformance(",
        "public static float ClampShutdownPerformance("
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
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Build(ComposeCargoItemDetailsSurface(",
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Compose(",
        "ComposeCargoItemObservation(",
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

    var runtimeStateRefResolverPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeStateRefResolver.cs");
    var runtimeStateRefResolver = File.Exists(runtimeStateRefResolverPath)
        ? File.ReadAllText(runtimeStateRefResolverPath)
        : throw new InvalidOperationException("Cannot verify item stat state-ref authority; AetheriaRuntimeStateRefResolver.cs is missing.");

    if (!runtimeStateRefResolver.Contains("TryResolveDaemonItemStatRef(", StringComparison.Ordinal) ||
        !runtimeStateRefResolver.Contains("AetheriaRuntimeDaemonItemStatQueries.TryReadItemStatRef(", StringComparison.Ordinal) ||
        !runtimeStateRefResolver.Contains("AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(", StringComparison.Ordinal) ||
        !runtimeStateRefResolver.Contains("item.Temperature", StringComparison.Ordinal) ||
        !runtimeStateRefResolver.Contains("FindDaemonItem(", StringComparison.Ordinal) ||
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
        !eveUnitySurfaceHost.Contains("CultMeshStateRefResolver stateRefResolver", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("ContainsStateRefs(surface)", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("prop.Key.EndsWith(\"Ref\", StringComparison.Ordinal)", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("CreateDefaultStateRefResolver()", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains("AetheriaUnityRuntimeClientProvider", StringComparison.Ordinal) ||
        !eveUnitySurfaceHost.Contains(".CreateEveSurfaceCultMeshStateRefResolver()", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("public static EveSurfaceDocument ResolveStateRefs(", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("ResolvePropRefs(props, resolveStateRef)", StringComparison.Ordinal) ||
        !runtimeEveSurfaceAdapter.Contains("ResolvePropRef(props, AetheriaRuntimeSurfaceStateRefs.Source, \"value\", resolveStateRef)", StringComparison.Ordinal) ||
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
        "ProjectCargoItemBehaviorMetric(",
        "ProjectCargoItemDetailsSurface(",
        "ProjectCargoItemObservation(",
        "AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Project("
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
        "public static AetheriaRuntimeCargoItemDetailsSurfaceState Compose(",
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
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Build(ComposeEquippedItemDetailsSurface(",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Compose(",
        "ComposeEquippedItemObservation(",
        "ComposeEquippedItemTemperatureControls(",
        "ComposeEquippedItemWeaponGroupControls(",
        "AetheriaRuntimeEquippedItemDetailsSurfaceCommands.TryRead(request, out var command)",
        "switch (command.Kind)",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.Close",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleOverrideShutdown",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleWeaponGroup"
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
        "ProjectEquippedItemActionBarSlots(",
        "BuildItemBehaviorCards(",
        "BuildItemBehaviorMetric(",
        "CloseEquippedItemDetailsCommand",
        "ToggleEquippedItemOverrideShutdownCommand",
        "SetEquippedItemTargetTemperatureCommand",
        "ToggleEquippedItemWeaponGroupCommand",
        "BindEquippedItemWeaponGroupCommand",
        "ClearEquippedItemActionBarBindingCommand",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.BindWeaponGroup",
        "AetheriaRuntimeEquippedItemDetailsCommandKind.ClearActionBarBinding",
        "RequestWeaponGroupActionBarBinding(",
        "RequestClearActionBarBinding(",
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
        "request.Payload",
        "ProjectEquippedItemDetailsSurface(",
        "ProjectEquippedItemObservation(",
        "ProjectEquippedItemTemperatureControls(",
        "ProjectEquippedItemWeaponGroupControls(",
        "AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Project("
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
        "public enum AetheriaRuntimeEquippedItemDetailsCommandKind",
        "public readonly struct AetheriaRuntimeEquippedItemDetailsCommand",
        "public static class AetheriaRuntimeEquippedItemDetailsSurfaceCommands",
        "public static bool TryRead(",
        "public const string SurfaceId = \"aetheria.inventory.equipped_item_details\"",
        "public const string Close = \"aetheria.inventory.equipped_item_details.close\"",
        "public const string ToggleOverrideShutdown = \"aetheria.inventory.equipped_item_details.override_shutdown.toggle\"",
        "public const string SetTargetTemperature = \"aetheria.inventory.equipped_item_details.target_temperature.set\"",
        "public const string ToggleWeaponGroup = \"aetheria.inventory.equipped_item_details.weapon_group.toggle\"",
        "public static AetheriaRuntimeEquippedItemDetailsSurfaceState Compose(",
        "ProjectBehaviorSections(",
        "ProjectBehaviorMetric(",
        "AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "new AetheriaRuntimeSurfaceCommandTemplate(Close",
        "new AetheriaRuntimeSurfaceCommandTemplate(ToggleOverrideShutdown",
        "new AetheriaRuntimeSurfaceCommandTemplate(SetTargetTemperature",
        "new AetheriaRuntimeSurfaceCommandTemplate(ToggleWeaponGroup",
        "ReadInt(request, \"behaviorIndex\", -1)",
        "ReadFloat(request, \"value\", 0f)",
        "ReadInt(request, \"group\", -1)",
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

    var forbiddenBuilderActionBarSymbols = new[]
    {
        "AetheriaRuntimeEquippedItemActionBarSlot",
        "ActionBarSlots",
        "BindWeaponGroup",
        "ClearActionBarBinding",
        "ReadInt(request, \"slot\", -1)",
        "action_bar"
    };
    var builderActionBarHits = forbiddenBuilderActionBarSymbols
        .Where(symbol => equippedItemSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (builderActionBarHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared equipped-item detail surface still exposes Unity-local action-bar binding concepts: " +
            string.Join(", ", builderActionBarHits));
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
    var gameplaySceneWiringPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplaySceneWiring.cs");
    var gameplaySceneWiring = File.Exists(gameplaySceneWiringPath)
        ? File.ReadAllText(gameplaySceneWiringPath)
        : throw new InvalidOperationException("Cannot verify action-bar scene wiring; AetheriaUnityGameplaySceneWiring.cs is missing.");
    var actionBarPresentationPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityActionBarPresentation.cs");
    var actionBarPresentation = File.Exists(actionBarPresentationPath)
        ? File.ReadAllText(actionBarPresentationPath)
        : throw new InvalidOperationException("Cannot verify equipped-item action-bar presentation; AetheriaUnityActionBarPresentation.cs is missing.");

    var requiredActionBarPresentationSymbols = new[]
    {
        "public sealed class AetheriaUnityActionBarPresentation",
        "public int SlotCount",
        "public string SlotLabel(int slotIndex)",
        "public string BindingLabel(int slotIndex)",
        "public bool TryResolveControlPath(int slotIndex, out string controlPath)",
        "public void RequestBinding(ActionBarSlot slot, DragObject dragAction)",
        "public bool RequestWeaponGroupBinding(int slotIndex, int groupIndex)",
        "public bool ClearBinding(int slotIndex)",
        "public void ApplyLocalBindings()",
        "public void ApplyBindings(",
        "private AetheriaUnityActionBarBinding CreateBindingCommit(",
        "private void SetLocalBinding(AetheriaUnityActionBarBinding binding)"
    };

    var missingActionBarPresentationSymbols = requiredActionBarPresentationSymbols
        .Where(symbol => !actionBarPresentation.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionBarPresentationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Equipped-item action-bar presentation reads must live outside ActionGameManager: " +
            string.Join(", ", missingActionBarPresentationSymbols));
    }

    var requiredActionBarInjectionSymbols = new[]
    {
        "private readonly AetheriaUnityActionBarPresentation _actionBarPresentation",
        "SceneWiring.ConfigureActionBarPresentation(",
        "_actionBarPresentation,"
    };
    var missingActionBarInjectionSymbols = requiredActionBarInjectionSymbols
        .Where(symbol => !actionGameManagerSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionBarInjectionSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager must inject action-bar presentation into the inventory shell instead of exposing manager read APIs: " +
            string.Join(", ", missingActionBarInjectionSymbols));
    }

    var requiredActionBarSceneWiringSymbols = new[]
    {
        "public void ConfigureActionBarPresentation(",
        "actionBarPresentation?.Bind(",
        "resolveActionBarClient);",
        "Inventory?.SetActionBarPresentation(actionBarPresentation)"
    };
    var missingActionBarSceneWiringSymbols = requiredActionBarSceneWiringSymbols
        .Where(symbol => !gameplaySceneWiring.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionBarSceneWiringSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplaySceneWiring must bind action-bar presentation into the inventory shell instead of leaving ActionGameManager as the owner: " +
            string.Join(", ", missingActionBarSceneWiringSymbols));
    }

    var forbiddenActionBarManagerReadSymbols = new[]
    {
        "public int GetActionBarSlotCount(",
        "public string GetActionBarSlotLabel(",
        "public string GetActionBarBindingLabel(",
        "public bool TryResolveActionBarSlotControlPath("
    };
    var actionBarManagerReadHits = forbiddenActionBarManagerReadSymbols
        .Where(symbol => actionGameManagerSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (actionBarManagerReadHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still exposes action-bar presentation reads: " +
            string.Join(", ", actionBarManagerReadHits));
    }

    var requiredActionBarInventorySymbols = new[]
    {
        "SetActionBarPresentation(AetheriaUnityActionBarPresentation actionBarPresentation)",
        "ResolveClient()"
    };

    var missingActionBarInventorySymbols = requiredActionBarInventorySymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionBarInventorySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu no longer submits equipped-item action-bar bindings through the typed AetheriaClient facade: " +
            string.Join(", ", missingActionBarInventorySymbols));
    }

    var forbiddenInventoryActionBarDaemonSymbols = new[]
    {
        "operations => operations.SetActionBarBinding(",
        "operations => operations.ClearActionBarBinding(controlPath)",
        "SetActionBarBinding(",
        "ClearActionBarBinding(controlPath)"
    };
    var inventoryActionBarDaemonHits = forbiddenInventoryActionBarDaemonSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (inventoryActionBarDaemonHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still treats local action-bar mapping as a daemon equipped-item command: " +
            string.Join(", ", inventoryActionBarDaemonHits));
    }

    var forbiddenInventoryActionBarManagerReadSymbols = new[]
    {
        "GameManager.GetActionBarSlotCount(",
        "GameManager.GetActionBarSlotLabel(",
        "GameManager.GetActionBarBindingLabel(",
        "GameManager.TryResolveActionBarSlotControlPath("
    };
    var inventoryActionBarManagerReadHits = forbiddenInventoryActionBarManagerReadSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (inventoryActionBarManagerReadHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still reads action-bar presentation through ActionGameManager: " +
            string.Join(", ", inventoryActionBarManagerReadHits));
    }

    var forbiddenActionBarManagerSymbols = new[]
    {
        "RequestWeaponGroupActionBarBinding(",
        "RequestClearActionBarBinding(",
        "TryRequestDaemonActionBarBinding(",
        "TryRequestDaemonActionBarBindingClear(",
        "RequestActionBarBinding(",
        "CreateActionBarBinding(",
        "CreateActionBarBindingCommit(",
        "ResolveActionBarClient()?.Operations.SetActionBarBinding("
    };
    var survivingActionBarManagerSymbols = forbiddenActionBarManagerSymbols
        .Where(symbol => actionGameManagerSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingActionBarManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns equipped-item action-bar binding authority instead of InventoryMenu's typed facade path: " +
            string.Join(", ", survivingActionBarManagerSymbols));
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
        "_cargoSelectorSurfaceModel = ComposeTradeCargoSelectorSurface();",
        "HandleCargoSelectorSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_cargoSelectorSurfaceDocument)",
        "AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Build(_cargoSelectorSurfaceModel.State)",
        "ComposeTradeCargoSelectorSurface(",
        "AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Compose(",
        "_cargoSelectorSurfaceModel?.TryResolve(command.Command, out var selection) == true",
        "ApplyCargoSelection(",
        "new AetheriaRuntimeTradeCargoModelOption(",
        "AetheriaRuntimeTradeCargoTargetKind.DockingBay",
        "AetheriaRuntimeTradeCargoTargetKind.ShipBay",
        "AetheriaRuntimeTradeCargoSelectorSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeTradeCargoSelectorCommandKind.Close",
        "AetheriaRuntimeTradeCargoSelectorCommandKind.Select",
        "SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)",
        "AetheriaRuntimeCurrentDockingDocument",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking",
        "_currentDocking ??= ResolveClient().State.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()",
        "docking = _currentDocking.Current",
        "CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit",
        "_stationRefit = ResolveClient().State.Document<AetheriaRuntimeStationRefitDocument>().Reactive()",
        "return _stationRefit?.Current",
        "SetTargetCargo(",
        "selection.EntityKey",
        "OwnedQuantity",
        "CargoTargets",
        "AetheriaRuntimeStationCargoTargetRow"
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
        "ProjectTradeCargoSelectorSurface(",
        "_cargoSelectorSurfaceProjection",
        "AetheriaRuntimeObservedDockingState",
        "ResolveClient().State.CurrentDocking()",
        "docking.Refit",
        "GameManager.DockedEntity.Children",
        "GameManager.CurrentEntity.Parent.Children",
        "GameManager.DockingBay",
        "GameManager.AvailableEntities()",
        "AetheriaClientReactiveDockingState _reactiveDockingState",
        "AetheriaClientDockingSnapshot _dockingState",
        "private AetheriaClientDockingSnapshot ResolveDockingState()",
        "_cargoSelectorStationRefitEntities",
        "stationRefit.AvailableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>()",
        "stationRefit.StationStock ?? Array.Empty<AetheriaRuntimeStationStockItem>()"
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

    var forbiddenTradeCountingSymbols = new[]
    {
        "CountAvailablePlayerShips(",
        "CountTargetCargoItems(",
        "ItemsOfType"
    };
    var tradeCountingHits = forbiddenTradeCountingSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (tradeCountingHits.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu still computes trade ownership locally instead of reading typed station stock facts: " +
            string.Join(", ", tradeCountingHits));
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
        "AetheriaRuntimeTradeCargoModelOption",
        "AetheriaRuntimeTradeCargoTargetKind",
        "AetheriaRuntimeTradeCargoSelection",
        "AetheriaRuntimeTradeCargoSelectorSurfaceModel",
        "public static string ShipBayCommand(",
        "public static AetheriaRuntimeTradeCargoSelectorSurfaceModel Compose(",
        "public bool TryResolve(string command, out AetheriaRuntimeTradeCargoSelection selection)",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "providerKind: \"trade.menu\"",
        "The observing client lists available cargo targets; the shared runtime surface owns the cargo selector contract."
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
        "_filterSurfaceModel = ComposeTradeFilterSurface();",
        "HandleFilterSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_filterSurfaceDocument)",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildFilter(_filterSurfaceModel.State)",
        "ComposeTradeFilterSurface(",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.ComposeFilters(",
        "_filterSurfaceModel?.TryResolve(command.Command, out var selection) == true",
        "ExecuteTradeFilterSelection(",
        "new AetheriaRuntimeTradeFilterOption(",
        "RenderRowActionSurface(",
        "HandleRowActionSurfaceCommand(",
        "AetheriaEveUnitySurfaceHost.Hide(_rowActionSurfaceDocument)",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.ComposeRowActions(",
        "AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildRowActions(_rowActionSurfaceModel.State)",
        "_rowActionSurfaceModel?.TryResolve(command.Command, out var selection) == true",
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
        "_filterSurfaceProjection",
        "private readonly Dictionary<string, Action> _filterSurfaceCommands",
        "BuildFilterSurfaceCommands(",
        "ProjectTradeFilterSurfaceState(",
        "ProjectTradeFilterSurface(",
        "AddTradeFilterGroup(",
        "_rowActionSurfaceCommands.TryGetValue(request.Command",
        "_rowActionSurfaceProjection",
        "private readonly Dictionary<string, Action> _rowActionSurfaceCommands",
        "BuildRowActionSurfaceCommands(",
        "ProjectTradeRowActionSurfaceState(",
        "ProjectRowActions("
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
        "AetheriaRuntimeTradeFilterSurfaceModel",
        "AetheriaRuntimeTradeRowActionSurfaceState",
        "AetheriaRuntimeTradeRowActionOption",
        "AetheriaRuntimeTradeRowActionSelection",
        "AetheriaRuntimeTradeRowActionSurfaceModel",
        "AetheriaRuntimeTradeSurfaceGroup",
        "AetheriaRuntimeTradeSurfaceOption",
        "public static string HardpointFilterCommand(",
        "public static string RowActionCommand(",
        "public static AetheriaRuntimeTradeFilterSurfaceModel ComposeFilters(",
        "public bool TryResolve(string command, out AetheriaRuntimeTradeFilterSelection selection)",
        "public static AetheriaRuntimeTradeRowActionSurfaceModel ComposeRowActions(",
        "public bool TryResolve(string command, out AetheriaRuntimeTradeRowActionSelection selection)",
        "public static AetheriaRuntimeSurfaceDocument BuildFilter(",
        "public static AetheriaRuntimeSurfaceDocument BuildRowActions(",
        "providerKind: \"trade.menu\"",
        "The observing client lists available trade filters; the shared runtime surface owns the filter selector contract.",
        "The observing client lists available row actions; the shared runtime surface owns the row action contract."
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
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var catalogSnapshotPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCatalogSnapshot.cs");
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
    if (!File.Exists(inventoryPanelPath))
    {
        throw new InvalidOperationException("Cannot verify typed catalog item enum accessors; InventoryPanel.cs is missing.");
    }
    if (!File.Exists(catalogSnapshotPath))
    {
        throw new InvalidOperationException("Cannot verify typed catalog item enum accessors; AetheriaRuntimeCatalogSnapshot.cs is missing.");
    }
    if (!File.Exists(tradeQueriesPath))
    {
        throw new InvalidOperationException("Cannot verify trade item value projection; shared runtime trade item queries are missing.");
    }

    var tradeMenu = File.ReadAllText(tradeMenuPath);
    var inventoryPanel = File.ReadAllText(inventoryPanelPath);
    var catalogSnapshot = File.ReadAllText(catalogSnapshotPath);
    var tradeQueries = File.ReadAllText(tradeQueriesPath);
    var requiredTradeMenuSymbols = new[]
    {
        "ProjectTradeItem(stock, typedItem)",
        "ProjectTradeItemCommit(",
        "ResolveCatalog()?.TradeValueSettings",
        "AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(",
        "AetheriaRuntimeTradeItemProjection TradeProjection",
        "public int Price => TradeProjection.Price",
        "public string TierColorHex => TradeProjection.TierColorHex",
        "x.TypedItem.TryGetSimpleCommodityCategory(",
        "x.TypedItem.TryGetCompoundCommodityCategory(",
        "x.TypedItem.TryGetHardpointType(",
        "row.TypedItem.TryGetSimpleCommodityCategory(out SimpleCommodityCategory _)"
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
        "GameManager.ObservedTradeValueSettings()",
        "AetheriaUnityProjectionSettings.TradeValueSettings",
        "private readonly ItemManager _itemManager",
        "new TradeRow(item, FindTypedTradeItem(item), GameManager.ItemManager)",
        "TryGetTypedSimpleCommodityCategory(",
        "TryGetTypedCompoundCommodityCategory(",
        "TryGetTypedHardpoint("
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

    var requiredCatalogItemEnumSymbols = new[]
    {
        "public bool TryGetHardpointType<TEnum>(out TEnum hardpointType) where TEnum : struct",
        "public bool TryGetSimpleCommodityCategory<TEnum>(out TEnum category) where TEnum : struct",
        "public bool TryGetCompoundCommodityCategory<TEnum>(out TEnum category) where TEnum : struct",
        "public AetheriaRuntimeCatalogItem? FindItem<T>(T? item, Func<T, string?> itemKey) where T : class",
        "Enum.TryParse(HardpointType, true, out hardpointType)",
        "Enum.TryParse(SimpleCommodityCategory, true, out category)",
        "Enum.TryParse(CompoundCommodityCategory, true, out category)"
    };
    var missingCatalogItemEnumSymbols = requiredCatalogItemEnumSymbols
        .Where(symbol => !catalogSnapshot.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingCatalogItemEnumSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeCatalogItem must own typed enum accessors for catalog category/hardpoint fields: " +
            string.Join(", ", missingCatalogItemEnumSymbols));
    }

    if (!inventoryPanel.Contains("FindTypedInventoryItem(item)?.TryGetHardpointType(out HardpointType typedHardpoint) == true", StringComparison.Ordinal) ||
        inventoryPanel.Contains("TryGetTypedHardpointType(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel must ask AetheriaRuntimeCatalogItem for typed hardpoint classification instead of parsing catalog strings locally.");
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
        "ComposeDropdownSurface(",
        "_dropdownSurfaceModel = ComposeDropdownSurface();",
        "HandleDropdownSurfaceCommand(",
        "ExecuteDropdownSelection(",
        "AetheriaEveUnitySurfaceHost.RenderRuntime(",
        "AetheriaEveUnitySurfaceHost.Hide(_dropdownSurfaceDocument)",
        "AetheriaRuntimeInventoryDropdownSurfaceBuilder.Build(_dropdownSurfaceModel.State)",
        "AetheriaRuntimeInventoryDropdownSurfaceBuilder.Compose(",
        "AetheriaRuntimeInventoryDropdownSurfaceCommands.TryRead(request, out var command)",
        "AetheriaRuntimeInventoryDropdownCommandKind.Close",
        "AetheriaRuntimeInventoryDropdownCommandKind.Select",
        "_dropdownSurfaceModel?.TryResolve(command.Command, out var selection) == true",
        "AetheriaRuntimeInventoryDropdownSelectionKind.EntityBay",
        "AetheriaRuntimeInventoryDropdownSelectionKind.Loadout",
        "new AetheriaRuntimeInventoryDropdownEntityOption(",
        "new AetheriaRuntimeInventoryDropdownLoadoutOption(",
        "selection.EntityKey",
        "_displayedEntityKey",
        "_displayedCargoEntityKey",
        "TryResolveStationRefitEntity(string entityKey",
        "SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)",
        "_observedEntityIndex.TryResolveEntityByRecordKey",
        "TryResolveObservedDockingIndex(out var dockingIndex)",
        "dockingIndex.TryResolveCurrentDockingBay(out var resolvedDockingBay)",
        "ResolveStationRefitDocument()",
        "currentEntityKey = ResolveCurrentEntity()?.EntityKey ?? \"\"",
        "LoadoutRestoreOptions"
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
        "GameManager.ObservedAvailableEntities()",
        "GameManager.TryGetObservedDockingBay(",
        "string.Equals(request.Command, AetheriaRuntimeInventoryDropdownSurfaceBuilder.Close",
        "_dropdownCommands.TryGetValue(request.Command",
        "private readonly Dictionary<string, Action> _dropdownCommands",
        "BuildDropdownCommands(",
        "ProjectDropdownSurfaceState(",
        "ProjectDropdownSurface(",
        "_dropdownSurfaceProjection",
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

    if (!inventoryDropdownSurfaceBuilder.Contains("public string EntityKey { get; }", StringComparison.Ordinal) ||
        !inventoryDropdownSurfaceBuilder.Contains("entityKey: entity.EntityKey", StringComparison.Ordinal) ||
        !source.Contains("TryResolveStationRefitEntity(selection.EntityKey", StringComparison.Ordinal) ||
        !source.Contains("TryResolveCurrentDockingBayRow(out var currentDockingBay)", StringComparison.Ordinal) ||
        !source.Contains("TryResolveObservedDockingIndex(out var dockingIndex)", StringComparison.Ordinal) ||
        !source.Contains("TryResolveCurrentDockingBay(out var dockingBay)", StringComparison.Ordinal) ||
        !source.Contains("dockingIndex.TryResolveCurrentDockingBay(out var resolvedDockingBay)", StringComparison.Ordinal) ||
        !source.Contains("ResolveStationRefitDocument()", StringComparison.Ordinal) ||
        !source.Contains("currentEntityKey = ResolveCurrentEntity()?.EntityKey ?? \"\"", StringComparison.Ordinal) ||
        !source.Contains("currentDockingBay.DockingBayIndex", StringComparison.Ordinal) ||
        !source.Contains("ResolveStationRefit()?.DockParentEntityKey", StringComparison.Ordinal) ||
        source.Contains("var hasDockingBay = TryGetTypedCurrentDockingBayFacade", StringComparison.Ordinal) ||
        source.Contains("TryResolveCurrentDockingBayFacade", StringComparison.Ordinal) ||
        source.Contains("ResolveDockingState()?.CurrentDocking", StringComparison.Ordinal) ||
        source.Contains("AetheriaClientReactiveDockingState _reactiveDockingState", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel dropdown must carry typed entity identity through the shared Eve surface and must project docking display state from StationRefitAsync docking-bay rows.");
    }

    if (!inventoryMenu.Contains("TryResolveCurrentEntity(out var currentEntity)", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("TryResolveCurrentDockingBay(out var dockingBay)", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("TryResolveObservedDockingIndex(out var dockingIndex)", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("dockingIndex.TryResolveCurrentDockingBay(out var resolvedDockingBay)", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("currentEntityKey = ResolveCurrentEntity()?.EntityKey ?? \"\"", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("currentEntity = ResolveCurrentEntity();", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("ResolveStationRefitDocument()", StringComparison.Ordinal) ||
        inventoryMenu.Contains("TryGetTypedCurrentDockingBayFacade", StringComparison.Ordinal) ||
        inventoryMenu.Contains("TryResolveCurrentEntityFacade", StringComparison.Ordinal) ||
        inventoryMenu.Contains("ResolveDockingState()?.CurrentDocking", StringComparison.Ordinal) ||
        inventoryMenu.Contains("AetheriaClientReactiveDockingState _reactiveDockingState", StringComparison.Ordinal) ||
        inventoryMenu.Contains("_observedEntityIndex.TryResolveDockingBayByRecordKey", StringComparison.Ordinal) ||
        inventoryMenu.Contains("GameManager.TryGetObservedDockingBay(", StringComparison.Ordinal) ||
        inventoryMenu.Contains("GameManager.DockingBay", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryMenu must resolve typed current entity/refit state through managed sessions before adapting docking state to Unity objects.");
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
        "AetheriaRuntimeInventoryDropdownSurfaceModel",
        "public string EntityKey { get; }",
        "entityKey: entity.EntityKey",
        "public static AetheriaRuntimeInventoryDropdownSurfaceModel Compose(",
        "public bool TryResolve(",
        "public static string EntityEquipmentCommand(",
        "public static string EntityBayCommand(",
        "public static string EntityCommand(",
        "public static string LoadoutCommand(",
        "public static AetheriaRuntimeSurfaceDocument Build(",
        "providerKind: \"inventory.panel\"",
        "The observing client lists available inventory navigation; the shared runtime surface owns the dropdown contract."
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

static void RequireStationRefitDockingBaysUseTypedProjection(string root)
{
    var runtimeDocumentsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeRtsViewportDocuments.cs");
    var runtimeProjectionPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeRtsProjection.cs");
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");

    var runtimeDocuments = File.Exists(runtimeDocumentsPath)
        ? File.ReadAllText(runtimeDocumentsPath)
        : throw new InvalidOperationException("Cannot verify station-refit docking-bay projection; runtime viewport documents are missing.");
    var runtimeProjection = File.Exists(runtimeProjectionPath)
        ? File.ReadAllText(runtimeProjectionPath)
        : throw new InvalidOperationException("Cannot verify station-refit docking-bay projection; runtime projection source is missing.");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify station-refit docking-bay projection; InventoryPanel.cs is missing.");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify station-refit docking-bay projection; InventoryMenu.cs is missing.");

    var requiredDocumentSymbols = new[]
    {
        "public IReadOnlyList<AetheriaRuntimeStationDockingBayRow> DockingBays",
        "public sealed class AetheriaRuntimeStationDockingBayRow",
        "public int DockingBayIndex",
        "public string OccupiedEntityKey",
        "public int OccupiedEntityIndex",
        "public string OccupiedEntityName",
        "public string OccupiedHullItemKey",
        "public bool OccupiedByCurrentEntity",
        "public IReadOnlyList<AetheriaRuntimeStationStockItem> CargoItems",
        "public IReadOnlyList<AetheriaRuntimeStationCargoTargetRow> CargoTargets",
        "public sealed class AetheriaRuntimeStationCargoTargetRow",
        "public AetheriaRuntimeTradeCargoTargetKind Kind",
        "public int TargetIndex",
        "public string EntityKey",
        "public int BayIndex",
        "public int Price",
        "public bool CanAfford",
        "public int OwnedQuantity"
    };
    var missingDocumentSymbols = requiredDocumentSymbols
        .Where(symbol => !runtimeDocuments.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "StationRefitAsync no longer exposes typed docking-bay rows: " +
            string.Join(", ", missingDocumentSymbols));
    }

    var requiredProjectionSymbols = new[]
    {
        "var dockingBays = parent == null",
        "DockingBays = dockingBays",
        "ProjectStationDockingBays(context, parent, currentEntityIndex)",
        "private static IReadOnlyList<AetheriaRuntimeStationDockingBayRow> ProjectStationDockingBays(",
        "dockParent.DockingBays",
        "dockParent.DockingBayAssignments",
        "dockParent.DockingBayContents",
        "OccupiedEntityIndex = assignedEntity?.EntityIndex ?? assignedEntityIndex",
        "OccupiedEntityKey = assignedEntity == null",
        "BuildEntityKey(context.RunId, context.Zone.ZoneIndex, assignedEntity.EntityIndex)",
        "OccupiedEntityName = assignedEntity?.Name",
        "OccupiedHullItemKey = assignedEntity?.HullItemKey",
        "OccupiedByCurrentEntity = assignedEntityIndex >= 0 && assignedEntityIndex == currentEntityIndex",
        "CargoItems = dockingBayIndex < contents.Count",
        "ProjectStationStock(contents[dockingBayIndex], dockingBayIndex)",
        "var cargoTargets = parent == null",
        "CargoTargets = cargoTargets",
        "ProjectStationCargoTargets(parentKey, dockingBayIndex, dockingBays, availableEntities)",
        "private static IReadOnlyList<AetheriaRuntimeStationCargoTargetRow> ProjectStationCargoTargets(",
        "AetheriaRuntimeTradeCargoTargetKind.DockingBay",
        "AetheriaRuntimeTradeCargoTargetKind.ShipBay",
        "currentDockingBay?.CargoItems",
        "entity.CargoItems ?? Array.Empty<AetheriaRuntimeStationStockItem>()",
        "ProjectStationStockTradeFacts(",
        "private static IReadOnlyList<AetheriaRuntimeStationStockItem> ProjectStationStockTradeFacts(",
        "private static int ProjectStationStockPrice(",
        "private static int CountStationOwnedQuantity(",
        "AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(",
        "CanAfford = price >= 0 && credits >= price",
        "OwnedQuantity = CountStationOwnedQuantity"
    };
    var missingProjectionSymbols = requiredProjectionSymbols
        .Where(symbol => !runtimeProjection.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingProjectionSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "StationRefitAsync docking-bay rows are no longer projected from daemon station state: " +
            string.Join(", ", missingProjectionSymbols));
    }

    var requiredPanelSymbols = new[]
    {
        "TryResolveCurrentDockingBayRow(out var currentDockingBay)",
        "AetheriaRuntimeStationDockingBayRow dockingBay",
        "ResolveStationRefitDocument()",
        "refit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>()",
        "currentDockingBay.DockingBayIndex"
    };
    var missingPanelSymbols = requiredPanelSymbols
        .Where(symbol => !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingPanelSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel must validate current docking-bay display through StationRefitAsync typed docking-bay rows: " +
            string.Join(", ", missingPanelSymbols));
    }

    var requiredMenuSymbols = new[]
    {
        "TryResolveCurrentDockingBayRow(out AetheriaRuntimeStationDockingBayRow dockingBay)",
        "AetheriaRuntimeStationDockingBayRow dockingBay",
        "ResolveStationRefitDocument()",
        "refit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>()"
    };
    var missingMenuSymbols = requiredMenuSymbols
        .Where(symbol => !inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingMenuSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu must validate current docking-bay display through StationRefitAsync typed docking-bay rows: " +
            string.Join(", ", missingMenuSymbols));
    }

    var forbiddenMenuSymbols = new[]
    {
        "GameManager.TryGetObservedDockingBay(",
        "GameManager.DockingBay"
    };
    var forbiddenHits = forbiddenMenuSymbols
        .Where(symbol => inventoryPanel.Contains(symbol, StringComparison.Ordinal) || inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (forbiddenHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory docking-bay display still asks ActionGameManager for broad docking state instead of typed station-refit rows: " +
            string.Join(", ", forbiddenHits));
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
        !bridge.Contains("MutableDocument<EveSurfaceState>(AetheriaStateNode.PlayerSettingsSurfaceKey)", StringComparison.Ordinal) ||
        !bridge.Contains(".ReplaceAsync(AetheriaPlayerSettingsSurfaceProjector.Build", StringComparison.Ordinal) ||
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

    if (!daemonHost.Contains("MutableDocument<EveSurfaceState>(AetheriaStateNode.PlayerSettingsSurfaceKey)", StringComparison.Ordinal) ||
        !daemonHost.Contains(".ReplaceAsync(AetheriaPlayerSettingsSurfaceProjector.Build", StringComparison.Ordinal) ||
        !daemonHost.Contains("AetheriaPlayerSettingsSurfaceProjector.Build", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon no longer republishes the provider-owned player-settings Eve surface.");
    }

    if (bridge.Contains("PlayerSettingsSurface()", StringComparison.Ordinal) ||
        daemonHost.Contains("PlayerSettingsSurface()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Player-settings Eve publication still uses named AetheriaStateNode surface helpers instead of generic mutable typed documents.");
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

static void RequireInventoryProjectionSlotIdentity(string root)
{
    var projectionPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeRtsProjection.cs");
    var viewportDocumentsPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeRtsViewportDocuments.cs");

    if (!File.Exists(projectionPath))
    {
        throw new InvalidOperationException("Cannot verify inventory projection slot identity; AetheriaRuntimeRtsProjection.cs is missing.");
    }
    if (!File.Exists(viewportDocumentsPath))
    {
        throw new InvalidOperationException("Cannot verify inventory projection slot identity; AetheriaRuntimeRtsViewportDocuments.cs is missing.");
    }

    var projection = File.ReadAllText(projectionPath);
    var viewportDocuments = File.ReadAllText(viewportDocumentsPath);

    var requiredProjectionSymbols = new[]
    {
        "AddSlot(items, \"equipment\", equipmentIndex, equipment[equipmentIndex])",
        "AddSlot(items, \"cargo\", cargoBayIndex, slot)",
        "SourceIndex = sourceIndex",
        "X = slot.X",
        "Y = slot.Y"
    };
    var missingProjectionSymbols = requiredProjectionSymbols
        .Where(symbol => !projection.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingProjectionSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory projection no longer carries enough typed slot identity for cargo/equipment operations: " +
            string.Join(", ", missingProjectionSymbols));
    }

    var requiredDocumentSymbols = new[]
    {
        "public sealed class AetheriaRuntimeRtsInventoryItem",
        "[Key(6)]",
        "public int SourceIndex { get; set; } = -1;",
        "[Key(7)]",
        "public int X { get; set; } = -1;",
        "[Key(8)]",
        "public int Y { get; set; } = -1;"
    };
    var missingDocumentSymbols = requiredDocumentSymbols
        .Where(symbol => !viewportDocuments.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory item document no longer publishes cargo/equipment slot identity to CultMesh clients: " +
            string.Join(", ", missingDocumentSymbols));
    }
}

static void RequireInventoryValidationUsesManagedTypedDocuments(string root)
{
    var clientStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaClientState.cs");
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");

    if (!File.Exists(clientStatePath) ||
        !File.Exists(inventoryMenuPath) ||
        !File.Exists(inventoryPanelPath))
    {
        throw new InvalidOperationException("Cannot verify managed inventory document access; expected client state and inventory UI sources are missing.");
    }

    var clientState = File.ReadAllText(clientStatePath);
    if (!clientState.Contains("public CultMeshDocumentHandle<TDocument> Document<TDocument>()", StringComparison.Ordinal) ||
        !clientState.Contains("public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)", StringComparison.Ordinal) ||
        clientState.Contains("public CultMeshReactiveDocument<TDocument> Reactive<TDocument>(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose typed document handles for current entity, station refit, and indexed inventory documents while leaving reactivity to CultMesh handles.");
    }

    var sources = new Dictionary<string, string>
    {
        ["InventoryMenu.cs"] = File.ReadAllText(inventoryMenuPath),
        ["InventoryPanel.cs"] = File.ReadAllText(inventoryPanelPath)
    };
    foreach (var (name, source) in sources)
    {
        var compact = CompactSource(source);
        var requiredSymbols = new[]
        {
            "ResolveCurrentEntity()",
            "ResolveStationRefitDocument()",
            "ResolveInventory(entityIndex)",
            "_inventory?.Current"
        };
        var missingSymbols = requiredSymbols
            .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
            .ToArray();
        var requiredCompactedSymbols = new[]
        {
            ".Document<AetheriaRuntimeCurrentEntityDocument>().Reactive()",
            ".Document<AetheriaRuntimeStationRefitDocument>().Reactive()",
            ".Document<AetheriaRuntimeInventoryDocument>(entityIndex).Reactive()"
        };
        missingSymbols = missingSymbols
            .Concat(requiredCompactedSymbols.Where(symbol => !compact.Contains(symbol, StringComparison.Ordinal)))
            .ToArray();
        if (missingSymbols.Length > 0)
        {
            throw new InvalidOperationException(
                $"{name} no longer validates inventory slots through managed typed client documents: " +
                string.Join(", ", missingSymbols));
        }

        var forbiddenCompactedSymbols = new[]
        {
            "AetheriaRuntimeCurrentEntitySession",
            "AetheriaRuntimeStationRefitSession",
            "AetheriaRuntimeInventorySession",
            ".ObserveEntity()",
            ".ObserveStationRefit()",
            ".ObserveInventory(entityIndex)",
            ".Latest<AetheriaRuntimeCurrentEntityDocument>()",
            ".Latest<AetheriaRuntimeStationRefitDocument>()",
            ".Details.Latest<AetheriaRuntimeInventoryDocument>(entityIndex)",
            ".State.Current.Entity.LatestAsync()",
            ".State.StationRefit.LatestAsync()",
            ".Details.Inventory(entityIndex).LatestAsync()"
        }
            .Select(CompactSource)
            .ToArray();
        var hits = forbiddenCompactedSymbols
            .Where(symbol => compact.Contains(symbol, StringComparison.Ordinal))
            .ToArray();
        if (hits.Length > 0)
        {
            throw new InvalidOperationException(
                $"{name} still routes inventory validation through session wrappers instead of direct managed reactive typed documents.");
        }

        RequireReactiveTypedDocumentAccess(
            source,
            name,
            "AetheriaRuntimeCurrentEntityDocument",
            "_currentEntity",
            ".Document<AetheriaRuntimeCurrentEntityDocument>().Reactive()",
            "AetheriaRuntimeCurrentEntitySession",
            ".ObserveEntity()");
        RequireReactiveTypedDocumentAccess(
            source,
            name,
            "AetheriaRuntimeStationRefitDocument",
            "_stationRefit",
            ".Document<AetheriaRuntimeStationRefitDocument>().Reactive()",
            "AetheriaRuntimeStationRefitSession",
            ".ObserveStationRefit()");
        RequireReactiveTypedDocumentAccess(
            source,
            name,
            "AetheriaRuntimeInventoryDocument",
            "_inventory",
            ".Document<AetheriaRuntimeInventoryDocument>(entityIndex).Reactive()",
            "AetheriaRuntimeInventorySession",
            ".ObserveInventory(entityIndex)");
    }

    Console.WriteLine("Inventory validation: cargo/equipment checks read managed typed client documents instead of manual handle walks");
}

static void RequireMenuDockingUsesManagedTypedSnapshot(string root)
{
    var clientStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaClientState.cs");
    if (!File.Exists(clientStatePath))
        throw new InvalidOperationException("Cannot verify managed docking snapshot access; AetheriaClientState.cs is missing.");

    var clientState = File.ReadAllText(clientStatePath);
    var requiredClientSymbols = new[]
    {
        "public CultMeshDocumentHandle<AetheriaRuntimeStationRefitDocument> StationRefit { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeCurrentDockingDocument> CurrentDockingDocument { get; }",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>()"
    };
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose managed typed docking documents directly for Unity menu surfaces: " +
            string.Join(", ", missingClientSymbols));
    }

    var forbiddenClientSymbols = new[]
    {
        "AetheriaClientDockingState",
        "AetheriaClientDockingSnapshot",
        "AetheriaClientReactiveDockingState",
        "AetheriaClientCurrentState",
        "LatestDockingState",
        "ReactiveDockingState"
    };
    var forbiddenClientHits = forbiddenClientSymbols
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenClientHits.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState still exposes aggregate docking snapshot wrappers instead of direct managed typed documents: " +
            string.Join(", ", forbiddenClientHits));
    }

    if (clientState.Contains("public AetheriaRuntimeObservedDockingState? CurrentDocking(", StringComparison.Ordinal) ||
        clientState.Contains("AetheriaRuntimeObservedDockingState.TryCreateCurrent(entity, docking, refit", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState must not expose one-shot observed docking aggregation; callers should hold the managed typed docking documents they need.");
    }

    var menuPaths = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "LocalMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuPanel.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedDockingIndex.cs")
    };

    var missingPaths = menuPaths.Where(path => !File.Exists(path)).ToArray();
    if (missingPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify managed docking snapshot menu access; missing sources: " +
            string.Join(", ", missingPaths.Select(path => Path.GetRelativePath(root, path))));
    }

    var offenders = menuPaths
        .Select(path => new
        {
            Path = path,
            Source = File.ReadAllText(path)
        })
        .Select(entry => new
        {
            entry.Path,
            entry.Source,
            Compact = CompactSource(entry.Source)
        })
        .Where(entry =>
            !HasManagedDockingSnapshotAccess(entry.Source) ||
            entry.Source.Contains(".DockingState.Reactive()", StringComparison.Ordinal) ||
            entry.Compact.Contains(CompactSource(".DockingState.Latest()"), StringComparison.Ordinal) ||
            entry.Compact.Contains(CompactSource(".DockingState.TryLatest("), StringComparison.Ordinal))
        .Select(entry => Path.GetRelativePath(root, entry.Path))
        .ToArray();

    if (offenders.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity menu docking state must read managed typed docking documents instead of nested latest/reactive wrappers: " +
            string.Join(", ", offenders));
    }

    var menuPanel = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuPanel.cs"));
    var tradeMenu = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs"));
    var directDockingOffenders = new List<string>();
    if (!menuPanel.Contains("CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking", StringComparison.Ordinal) ||
        !menuPanel.Contains("_currentDocking ??= ResolveClient().State.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()", StringComparison.Ordinal) ||
        !menuPanel.Contains("docking = _currentDocking.Current", StringComparison.Ordinal) ||
        menuPanel.Contains("ResolveClient().State.Latest<AetheriaRuntimeCurrentDockingDocument>()", StringComparison.Ordinal))
    {
        directDockingOffenders.Add("Assets/Scripts/UI/Menu/MenuPanel.cs");
    }

    if (!tradeMenu.Contains("CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking", StringComparison.Ordinal) ||
        !tradeMenu.Contains("_currentDocking ??= ResolveClient().State.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()", StringComparison.Ordinal) ||
        !tradeMenu.Contains("docking = _currentDocking.Current", StringComparison.Ordinal) ||
        tradeMenu.Contains("ResolveClient().State.Latest<AetheriaRuntimeCurrentDockingDocument>()", StringComparison.Ordinal))
    {
        directDockingOffenders.Add("Assets/Scripts/UI/Menu/TradeMenu.cs");
    }

    if (directDockingOffenders.Count > 0)
    {
        throw new InvalidOperationException(
            "Unity menu docking reads must hold the managed reactive typed docking document and sample Current directly: " +
            string.Join(", ", directDockingOffenders));
    }

    Console.WriteLine("Menu docking state: Unity menus read managed reactive typed docking snapshots");
}

static bool HasManagedDockingSnapshotAccess(string source)
{
    if (source.Contains("AetheriaRuntimeCurrentDockingDocument", StringComparison.Ordinal) &&
        source.Contains("CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking", StringComparison.Ordinal) &&
        source.Contains("State.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()", StringComparison.Ordinal) &&
        source.Contains("_currentDocking.Current", StringComparison.Ordinal) &&
        !source.Contains("ResolveClient().State.Latest<AetheriaRuntimeCurrentDockingDocument>()", StringComparison.Ordinal))
    {
        return true;
    }

    if (source.Contains("AetheriaRuntimeCurrentDockingDocument", StringComparison.Ordinal) &&
        source.Contains("ResolveClient().State.Latest<AetheriaRuntimeCurrentDockingDocument>()", StringComparison.Ordinal) &&
        !source.Contains("ResolveClient().State.CurrentDocking()", StringComparison.Ordinal) &&
        !source.Contains("AetheriaClientReactiveDockingState _dockingState", StringComparison.Ordinal) &&
        !source.Contains(".ReactiveDockingState()", StringComparison.Ordinal))
    {
        return true;
    }

    if (source.Contains("AetheriaUnityObservedDockingIndex", StringComparison.Ordinal) &&
        source.Contains("TryResolveObservedDockingIndex(out var dockingIndex)", StringComparison.Ordinal) &&
        source.Contains("dockingIndex.TryResolveCurrent", StringComparison.Ordinal) &&
        !source.Contains("AetheriaClientReactiveDockingState _reactiveDockingState", StringComparison.Ordinal))
    {
        return true;
    }

    return source.Contains("public sealed class AetheriaUnityObservedDockingIndex", StringComparison.Ordinal) &&
           source.Contains("CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity", StringComparison.Ordinal) &&
           source.Contains("CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking", StringComparison.Ordinal) &&
           source.Contains("CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit", StringComparison.Ordinal) &&
           source.Contains("TryReadCurrentDockingDocuments(", StringComparison.Ordinal) &&
           source.Contains("state.Document<AetheriaRuntimeCurrentEntityDocument>().Reactive()", StringComparison.Ordinal) &&
           source.Contains("state.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()", StringComparison.Ordinal) &&
           source.Contains("state.Document<AetheriaRuntimeStationRefitDocument>().Reactive()", StringComparison.Ordinal) &&
           source.Contains("_currentDocking?.Current", StringComparison.Ordinal) &&
           source.Contains("_stationRefit?.Current", StringComparison.Ordinal) &&
           !source.Contains("State?.CurrentDocking()", StringComparison.Ordinal) &&
           !source.Contains(".State.CurrentDocking()", StringComparison.Ordinal) &&
           !source.Contains("state.Latest<AetheriaRuntimeCurrentEntityDocument>()", StringComparison.Ordinal) &&
           !source.Contains("state.Latest<AetheriaRuntimeCurrentDockingDocument>()", StringComparison.Ordinal) &&
           !source.Contains("state.Latest<AetheriaRuntimeStationRefitDocument>()", StringComparison.Ordinal) &&
           !source.Contains("AetheriaClientReactiveDockingState _dockingState", StringComparison.Ordinal) &&
           !source.Contains(".ReactiveDockingState()", StringComparison.Ordinal);
}

static void RequireUnitySharedDocumentAccessorErgonomics(string root)
{
    var clientStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaClientState.cs");
    if (!File.Exists(clientStatePath))
        throw new InvalidOperationException("Cannot verify shared document accessor ergonomics; AetheriaClientState.cs is missing.");

    var clientState = File.ReadAllText(clientStatePath);
    var requiredClientSymbols = new[]
    {
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>()",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)"
    };
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose typed document handles for shared Unity documents, with reactive sync owned by CultMesh handles: " +
            string.Join(", ", missingClientSymbols));
    }

    if (clientState.Contains("public Task<TDocument> LatestAsync<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public TDocument Latest<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public CultMeshReactiveDocument<TDocument> Reactive<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public Observable<TDocument> Watch<TDocument>", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState must not expose generic latest/reactive/watch shortcuts; callers should hold Document<TDocument>() handles and call CultMesh handle APIs.");
    }

    var unityPaths = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionBarSlot.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayBootShell.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityRuntimeClientProvider.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedTargetQuery.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "HUD", "SchematicDisplay.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "InputScreen", "InputDisplayLayout.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MapRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "Zone Display", "VolumeCloudRenderer.cs")
    };

    var missingPaths = unityPaths.Where(path => !File.Exists(path)).ToArray();
    if (missingPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify shared Unity document accessor ergonomics; missing sources: " +
            string.Join(", ", missingPaths.Select(path => Path.GetRelativePath(root, path))));
    }

    var forbiddenCompactedSymbols = new[]
    {
        ".State.Catalog.Latest()",
        ".State.Settings.Player.Latest()",
        ".State.Settings.VerseHost.Latest()"
    }
        .Select(CompactSource)
        .ToArray();
    var offenders = unityPaths
        .Select(path => new
        {
            Path = path,
            Compact = CompactSource(File.ReadAllText(path))
        })
        .Where(entry => forbiddenCompactedSymbols.Any(symbol => entry.Compact.Contains(symbol, StringComparison.Ordinal)))
        .Select(entry => Path.GetRelativePath(root, entry.Path))
        .ToArray();

    if (offenders.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity presentation code still walks shared CultMesh document handles instead of managed typed document access: " +
            string.Join(", ", offenders));
    }

    var actionBarSlot = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Gameplay",
        "ActionBarSlot.cs"));
    var requiredActionBarSymbols = new[]
    {
        "public abstract class ActionBarBinding : IDisposable",
        "CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog",
        "Client.State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "_catalog?.Current?.FindItem(item, x => x.ItemKey)",
        "binding?.Dispose()",
        "private void OnDestroy()"
    };
    var missingActionBarSymbols = requiredActionBarSymbols
        .Where(symbol => !actionBarSlot.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingActionBarSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionBarSlot should resolve runtime catalog items through a managed reactive catalog document with binding lifetime disposal: " +
            string.Join(", ", missingActionBarSymbols));
    }

    if (actionBarSlot.Contains("AetheriaRuntimeCatalogSession _catalog", StringComparison.Ordinal) ||
        actionBarSlot.Contains("Client.State.ObserveCatalog()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionBarSlot still routes through AetheriaRuntimeCatalogSession instead of its managed reactive typed catalog document.");
    }

    var schematicDisplay = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "HUD",
        "SchematicDisplay.cs"));
    var playerHudStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimePlayerHudState.cs");
    var requiredSchematicDisplaySymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog",
        "CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "ResolveClient().State.Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "ResolveClient().State.Document<AetheriaRuntimeCurrentEntityDocument>().Reactive()",
        "_catalog?.Dispose()",
        "_playerSettings?.Dispose()",
        "_currentEntity?.Dispose()",
        "ResolveCatalog()?.Current?.FindItem(item, x => x.ItemKey)",
        "return _playerSettings?.Current;",
        "private void OnDestroy()"
    };
    var missingSchematicDisplaySymbols = requiredSchematicDisplaySymbols
        .Where(symbol => !schematicDisplay.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSchematicDisplaySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "SchematicDisplay should bind shared catalog/settings/current-entity HUD through managed reactive typed documents: " +
            string.Join(", ", missingSchematicDisplaySymbols));
    }

    var forbiddenSchematicDisplaySymbols = new[]
    {
        "AetheriaRuntimePlayerHudSession _playerHud",
        "ResolvePlayerHud()",
        "ResolveClient().State.ObservePlayerHud()",
        "ResolvePlayerHud()?.Catalog",
        "ResolvePlayerHud()?.PlayerSettings",
        "ResolvePlayerHud()?.Hud"
    };
    var schematicDisplayHits = forbiddenSchematicDisplaySymbols
        .Where(symbol => schematicDisplay.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (schematicDisplayHits.Length > 0)
    {
        throw new InvalidOperationException(
            "SchematicDisplay still routes through AetheriaRuntimePlayerHudSession instead of direct managed reactive typed documents: " +
            string.Join(", ", schematicDisplayHits));
    }

    if (File.Exists(playerHudStatePath) ||
        clientState.Contains("ObservePlayerHud(", StringComparison.Ordinal) ||
        clientState.Contains("AetheriaRuntimePlayerHudSession", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Player HUD state must be read as direct reactive catalog/settings/current-entity documents; remove the aggregate AetheriaRuntimePlayerHudSession helper.");
    }

    var volumeCloudRenderer = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Zone Display",
        "VolumeCloudRenderer.cs"));
    var requiredVolumeCloudSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "_playerSettings?.Dispose()",
        "_playerSettings?.Current"
    };
    var missingVolumeCloudSymbols = requiredVolumeCloudSymbols
        .Where(symbol => !volumeCloudRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingVolumeCloudSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "VolumeCloudRenderer should read player settings through a managed reactive typed document: " +
            string.Join(", ", missingVolumeCloudSymbols));
    }

    if (volumeCloudRenderer.Contains("AetheriaRuntimePlayerSettingsSession _playerSettings", StringComparison.Ordinal) ||
        volumeCloudRenderer.Contains(".ObservePlayer()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "VolumeCloudRenderer still routes player settings through AetheriaRuntimePlayerSettingsSession instead of the managed reactive typed document.");
    }

    var observedTargetQuery = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Gameplay",
        "AetheriaUnityObservedTargetQuery.cs"));
    var requiredObservedTargetSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts",
        ".Document<AetheriaRuntimeZoneContactsDocument>().Reactive()",
        "_zoneContacts?.Dispose()",
        "_zoneContacts?.Current"
    };
    var missingObservedTargetSymbols = requiredObservedTargetSymbols
        .Where(symbol => !observedTargetQuery.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedTargetSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityObservedTargetQuery should read contacts through the managed reactive typed zone-contacts document: " +
            string.Join(", ", missingObservedTargetSymbols));
    }

    if (observedTargetQuery.Contains("AetheriaRuntimeZoneContactsSession _zoneContacts", StringComparison.Ordinal) ||
        observedTargetQuery.Contains(".ObserveZoneContacts()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaUnityObservedTargetQuery still routes zone contacts through AetheriaRuntimeZoneContactsSession instead of the managed reactive typed document.");
    }

    var mapRenderer = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "Menu",
        "MapRenderer.cs"));
    var requiredMapRendererSharedDocumentSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> _objectsViewport",
        "CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> _renderSplatsViewport",
        "ClearViewportCaches()",
        "_objectsViewport?.Dispose()",
        "_renderSplatsViewport?.Dispose()",
        "_objectsViewport?.Current",
        "_renderSplatsViewport?.Current",
        "private void OnDestroy()"
    };
    var missingMapRendererSharedDocumentSymbols = requiredMapRendererSharedDocumentSymbols
        .Where(symbol => !mapRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    var compactMapRenderer = CompactSource(mapRenderer);
    missingMapRendererSharedDocumentSymbols = missingMapRendererSharedDocumentSymbols
        .Concat(new[]
        {
            ".Document<AetheriaRuntimeObjectsViewportDocument>(viewport).Reactive()",
            ".Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport).Reactive()"
        }.Where(symbol => !compactMapRenderer.Contains(symbol, StringComparison.Ordinal)))
        .ToArray();
    if (missingMapRendererSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MapRenderer should bind viewport and player settings through managed reactive Aetheria documents: " +
            string.Join(", ", missingMapRendererSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        mapRenderer,
        "MapRenderer",
        "AetheriaRuntimeObjectsViewportDocument",
        "_objectsViewport",
        ".Document<AetheriaRuntimeObjectsViewportDocument>(viewport).Reactive()",
        "AetheriaRuntimeObjectsViewportSession",
        ".ObserveObjects(viewport)");
    RequireReactiveTypedDocumentAccess(
        mapRenderer,
        "MapRenderer",
        "AetheriaRuntimeRenderSplatsViewportDocument",
        "_renderSplatsViewport",
        ".Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport).Reactive()",
        "AetheriaRuntimeRenderSplatsViewportSession",
        ".ObserveRenderSplats(viewport)");
    RequireReactiveTypedDocumentAccess(
        mapRenderer,
        "MapRenderer",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

    var forbiddenMapRendererSharedDocumentSymbols = new[]
    {
        "AetheriaRuntimeObjectsViewportSession _objectsViewport",
        "AetheriaRuntimeRenderSplatsViewportSession _renderSplatsViewport",
        ".ObserveObjects(viewport)",
        ".ObserveRenderSplats(viewport)"
    };
    var mapRendererRawDocumentHits = forbiddenMapRendererSharedDocumentSymbols
        .Where(symbol => mapRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (mapRendererRawDocumentHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MapRenderer still routes viewport documents through session wrappers instead of managed reactive typed documents: " +
            string.Join(", ", mapRendererRawDocumentHits));
    }

    var mainMenu = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "MainMenu.cs"));
    var requiredMainMenuSharedDocumentSymbols = new[]
    {
        "_sectorMap?.Dispose()",
        "_playerSettings?.Dispose()",
        "_verseHostSettings?.Dispose()",
        "_sectorMap?.Current",
        "_playerSettings?.Current",
        "_verseHostSettings?.Current",
        "private void OnDestroy()"
    };
    var missingMainMenuSharedDocumentSymbols = requiredMainMenuSharedDocumentSymbols
        .Where(symbol => !mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingMainMenuSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu should bind sector, player, and Verse host state through managed reactive Aetheria documents: " +
            string.Join(", ", missingMainMenuSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        mainMenu,
        "MainMenu",
        "AetheriaRuntimeSectorMapDocument",
        "_sectorMap",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "AetheriaRuntimeSectorMapSession",
        ".ObserveSectorMap()");
    RequireReactiveTypedDocumentAccess(
        mainMenu,
        "MainMenu",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");
    RequireReactiveTypedDocumentAccess(
        mainMenu,
        "MainMenu",
        "AetheriaRuntimeVerseHostSettingsDocument",
        "_verseHostSettings",
        ".Document<AetheriaRuntimeVerseHostSettingsDocument>().Reactive()",
        "AetheriaRuntimeVerseHostSettingsSession",
        ".ObserveVerseHost()");

    var tradeMenu = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "Menu",
        "TradeMenu.cs"));
    var requiredTradeMenuSharedDocumentSymbols = new[] { "private void OnDestroy()" };
    var missingTradeMenuSharedDocumentSymbols = requiredTradeMenuSharedDocumentSymbols
        .Where(symbol => !tradeMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTradeMenuSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "TradeMenu should bind shared catalog/settings through managed reactive Aetheria documents with menu lifetime disposal: " +
            string.Join(", ", missingTradeMenuSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        tradeMenu,
        "TradeMenu",
        "AetheriaRuntimeCatalogSnapshot",
        "_catalog",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "AetheriaRuntimeCatalogSession",
        "ResolveClient().State.ObserveCatalog()");
    RequireReactiveTypedDocumentAccess(
        tradeMenu,
        "TradeMenu",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

    var inventoryMenu = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "Menu",
        "InventoryMenu.cs"));
    var requiredInventoryMenuSharedDocumentSymbols = new[] { "private void OnDestroy()" };
    var missingInventoryMenuSharedDocumentSymbols = requiredInventoryMenuSharedDocumentSymbols
        .Where(symbol => !inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingInventoryMenuSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu should bind shared catalog/settings through managed reactive Aetheria documents with menu lifetime disposal: " +
            string.Join(", ", missingInventoryMenuSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        inventoryMenu,
        "InventoryMenu",
        "AetheriaRuntimeCatalogSnapshot",
        "_catalog",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "AetheriaRuntimeCatalogSession",
        "ResolveClient().State.ObserveCatalog()");
    RequireReactiveTypedDocumentAccess(
        inventoryMenu,
        "InventoryMenu",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

    var inventoryPanel = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "Menu",
        "InventoryPanel.cs"));
    var requiredInventoryPanelSharedDocumentSymbols = new[]
    {
        "private void OnDestroy()"
    };
    var missingInventoryPanelSharedDocumentSymbols = requiredInventoryPanelSharedDocumentSymbols
        .Where(symbol => !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingInventoryPanelSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel should bind shared catalog/settings through managed reactive Aetheria documents with panel lifetime disposal: " +
            string.Join(", ", missingInventoryPanelSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        inventoryPanel,
        "InventoryPanel",
        "AetheriaRuntimeCatalogSnapshot",
        "_catalog",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "AetheriaRuntimeCatalogSession",
        "ResolveClient().State.ObserveCatalog()");
    RequireReactiveTypedDocumentAccess(
        inventoryPanel,
        "InventoryPanel",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

    var sectorRenderer = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "UI",
        "Menu",
        "SectorRenderer.cs"));
    var requiredSectorRendererSharedDocumentSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> _sectorMap",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> _currentZone",
        "CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> _zoneDetails",
        ".Current",
        "_sectorMap?.Dispose()",
        "_currentZone?.Dispose()",
        "_zoneDetails?.Dispose()",
        "private void OnDestroy()"
    };
    var missingSectorRendererSharedDocumentSymbols = requiredSectorRendererSharedDocumentSymbols
        .Where(symbol => !sectorRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    var compactSectorRenderer = CompactSource(sectorRenderer);
    missingSectorRendererSharedDocumentSymbols = missingSectorRendererSharedDocumentSymbols
        .Concat(new[]
        {
            ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
            ".Document<AetheriaRuntimeCurrentZoneDocument>().Reactive()",
            ".Document<AetheriaRuntimeZoneDetailsDocument>(zoneIndex).Reactive()"
        }.Where(symbol => !compactSectorRenderer.Contains(symbol, StringComparison.Ordinal)))
        .ToArray();
    if (missingSectorRendererSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "SectorRenderer should bind shared catalog/settings through managed reactive Aetheria documents with renderer lifetime disposal: " +
            string.Join(", ", missingSectorRendererSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        sectorRenderer,
        "SectorRenderer",
        "AetheriaRuntimeSectorMapDocument",
        "_sectorMap",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "AetheriaRuntimeSectorMapSession",
        ".ObserveSectorMap()");
    RequireReactiveTypedDocumentAccess(
        sectorRenderer,
        "SectorRenderer",
        "AetheriaRuntimeCurrentZoneDocument",
        "_currentZone",
        ".Document<AetheriaRuntimeCurrentZoneDocument>().Reactive()",
        "AetheriaRuntimeCurrentZoneSession",
        ".ObserveZone()");
    RequireReactiveTypedDocumentAccess(
        sectorRenderer,
        "SectorRenderer",
        "AetheriaRuntimeZoneDetailsDocument",
        "_zoneDetails",
        ".Document<AetheriaRuntimeZoneDetailsDocument>(zoneIndex).Reactive()",
        "AetheriaRuntimeZoneDetailsSession",
        ".ObserveZone(zoneIndex)");
    RequireReactiveTypedDocumentAccess(
        sectorRenderer,
        "SectorRenderer",
        "AetheriaRuntimeCatalogSnapshot",
        "_catalog",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "AetheriaRuntimeCatalogSession",
        "ResolveClient().State.ObserveCatalog()");
    RequireReactiveTypedDocumentAccess(
        sectorRenderer,
        "SectorRenderer",
        "AetheriaRuntimePlayerSettingsDocument",
        "_playerSettings",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "AetheriaRuntimePlayerSettingsSession",
        ".ObservePlayer()");

    var forbiddenSectorRendererSharedDocumentSymbols = new[]
    {
        "AetheriaRuntimeSectorMapSession _sectorMap",
        "AetheriaRuntimeCurrentZoneSession _currentZone",
        "AetheriaRuntimeZoneDetailsSession _zoneDetails",
        ".ObserveSectorMap()",
        ".ObserveZone(zoneIndex)"
    };
    var sectorRendererRawDocumentHits = forbiddenSectorRendererSharedDocumentSymbols
        .Where(symbol => sectorRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (sectorRendererRawDocumentHits.Length > 0)
    {
        throw new InvalidOperationException(
            "SectorRenderer still routes map/current-zone/details through session wrappers instead of managed reactive typed documents: " +
            string.Join(", ", sectorRendererRawDocumentHits));
    }

    var zoneRenderer = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Zone Display",
        "ZoneRenderer.cs"));
    var requiredZoneRendererSharedDocumentSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts",
        "ResolveClient().State.Document<AetheriaRuntimeZoneContactsDocument>().Reactive()",
        "_zoneContacts?.Dispose()",
        "_zoneContacts?.Current",
        "private void OnDestroy()"
    };
    var missingZoneRendererSharedDocumentSymbols = requiredZoneRendererSharedDocumentSymbols
        .Where(symbol => !zoneRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingZoneRendererSharedDocumentSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ZoneRenderer should bind catalog and contact state through managed reactive Aetheria documents with renderer lifetime disposal: " +
            string.Join(", ", missingZoneRendererSharedDocumentSymbols));
    }

    RequireReactiveTypedDocumentAccess(
        zoneRenderer,
        "ZoneRenderer",
        "AetheriaRuntimeZoneContactsDocument",
        "_zoneContacts",
        "ResolveClient().State.Document<AetheriaRuntimeZoneContactsDocument>().Reactive()",
        "AetheriaRuntimeZoneContactsSession",
        "ResolveClient().State.ObserveZoneContacts()");
    RequireReactiveTypedDocumentAccess(
        zoneRenderer,
        "ZoneRenderer",
        "AetheriaRuntimeCatalogSnapshot",
        "_catalog",
        "ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "AetheriaRuntimeCatalogSession",
        "ResolveClient().State.ObserveCatalog()");

    var forbiddenZoneRendererSharedDocumentSymbols = new[]
    {
        "AetheriaRuntimeZoneContactsSession _zoneContacts",
        "ResolveClient().State.ObserveZoneContacts()"
    };
    var zoneRendererRawDocumentHits = forbiddenZoneRendererSharedDocumentSymbols
        .Where(symbol => zoneRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (zoneRendererRawDocumentHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ZoneRenderer still routes contact state through session wrappers instead of managed reactive typed documents: " +
            string.Join(", ", zoneRendererRawDocumentHits));
    }

    Console.WriteLine("Shared document accessors: Unity shared catalog/settings reads use generic managed typed documents");
}

static void RequireUnityViewportAndMapReadsUseManagedAccessors(string root)
{
    var clientStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaClientState.cs");
    if (!File.Exists(clientStatePath))
        throw new InvalidOperationException("Cannot verify Unity viewport/map managed accessors; AetheriaClientState.cs is missing.");

    var clientState = File.ReadAllText(clientStatePath);
    var requiredClientSymbols = new[]
    {
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>()",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)"
    };
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose generic parameterized document handles for map, viewport, contacts, and current-entity documents: " +
            string.Join(", ", missingClientSymbols));
    }

    var unityPaths = new[]
    {
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedTargetQuery.cs"),
        Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityRenderSplatViewportSource.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "HUD", "SchematicDisplay.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MapRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorMap.cs"),
        Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs"),
        Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs")
    };

    var missingPaths = unityPaths.Where(path => !File.Exists(path)).ToArray();
    if (missingPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify Unity viewport/map managed accessors; missing sources: " +
            string.Join(", ", missingPaths.Select(path => Path.GetRelativePath(root, path))));
    }

    var forbiddenCompactedSymbols = new[]
    {
        ".State.SectorMap.LatestAsync()",
        ".State.ZoneContacts.LatestAsync()",
        ".State.Current.Entity.LatestAsync()",
        ".State.Viewports.Objects(",
        ".State.Viewports.RenderSplats(",
        ".Details.Zone(zoneIndex).LatestAsync()"
    }
        .Select(CompactSource)
        .ToArray();
    var offenders = unityPaths
        .Select(path => new
        {
            Path = path,
            Compact = CompactSource(File.ReadAllText(path))
        })
        .Where(entry => forbiddenCompactedSymbols.Any(symbol => entry.Compact.Contains(symbol, StringComparison.Ordinal)))
        .Select(entry => Path.GetRelativePath(root, entry.Path))
        .ToArray();

    if (offenders.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity map, viewport, contact, and current-entity reads still walk CultMesh document handles instead of managed typed document access: " +
            string.Join(", ", offenders));
    }

    var renderSplatViewportSource = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Gameplay",
        "AetheriaUnityRenderSplatViewportSource.cs"));
    var requiredRenderSplatViewportSourceSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> _renderSplatsViewport",
        "_renderSplatsViewport?.Current",
        "ClearViewportDocument()"
    };
    var missingRenderSplatViewportSourceSymbols = requiredRenderSplatViewportSourceSymbols
        .Where(symbol => !renderSplatViewportSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (!CompactSource(renderSplatViewportSource).Contains(
        ".Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport).Reactive()",
        StringComparison.Ordinal))
    {
        missingRenderSplatViewportSourceSymbols = missingRenderSplatViewportSourceSymbols
            .Concat(new[] { ".Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport).Reactive()" })
            .ToArray();
    }
    if (missingRenderSplatViewportSourceSymbols.Length > 0 ||
        renderSplatViewportSource.Contains(".LatestRenderSplats(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaUnityRenderSplatViewportSource must keep a managed reactive render-splats viewport document instead of polling latest snapshots: " +
            string.Join(", ", missingRenderSplatViewportSourceSymbols));
    }

    if (renderSplatViewportSource.Contains("AetheriaRuntimeRenderSplatsViewportSession _renderSplatsViewport", StringComparison.Ordinal) ||
        renderSplatViewportSource.Contains(".ObserveRenderSplats(viewport)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaUnityRenderSplatViewportSource still routes render-splats viewports through a session wrapper instead of a managed reactive typed document.");
    }

    RequireReactiveTypedDocumentAccess(
        renderSplatViewportSource,
        "AetheriaUnityRenderSplatViewportSource",
        "AetheriaRuntimeRenderSplatsViewportDocument",
        "_renderSplatsViewport",
        ".Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport).Reactive()",
        "AetheriaRuntimeRenderSplatsViewportSession",
        ".ObserveRenderSplats(viewport)");

    var zoneRenderer = File.ReadAllText(Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Zone Display",
        "ZoneRenderer.cs"));
    var requiredZoneRendererViewportSymbols = new[]
    {
        "CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> _objectsViewport",
        "_objectsViewport?.Current",
        "_objectsViewport?.Dispose()"
    };
    var missingZoneRendererViewportSymbols = requiredZoneRendererViewportSymbols
        .Where(symbol => !zoneRenderer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (!CompactSource(zoneRenderer).Contains(
        ".Document<AetheriaRuntimeObjectsViewportDocument>(viewportBounds).Reactive()",
        StringComparison.Ordinal))
    {
        missingZoneRendererViewportSymbols = missingZoneRendererViewportSymbols
            .Concat(new[] { ".Document<AetheriaRuntimeObjectsViewportDocument>(viewportBounds).Reactive()" })
            .ToArray();
    }
    if (missingZoneRendererViewportSymbols.Length > 0 ||
        zoneRenderer.Contains(".LatestObjects(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer fallback presentation queries must keep a managed reactive objects viewport document instead of polling latest snapshots: " +
            string.Join(", ", missingZoneRendererViewportSymbols));
    }

    if (zoneRenderer.Contains("AetheriaRuntimeObjectsViewportSession _objectsViewport", StringComparison.Ordinal) ||
        zoneRenderer.Contains(".ObserveObjects(viewportBounds)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ZoneRenderer still routes fallback objects viewports through a session wrapper instead of a managed reactive typed document.");
    }

    RequireReactiveTypedDocumentAccess(
        zoneRenderer,
        "ZoneRenderer",
        "AetheriaRuntimeObjectsViewportDocument",
        "_objectsViewport",
        ".Document<AetheriaRuntimeObjectsViewportDocument>(viewportBounds).Reactive()",
        "AetheriaRuntimeObjectsViewportSession",
        ".ObserveObjects(viewportBounds)");

    Console.WriteLine("Viewport and map document accessors: Unity reads map, contact, viewport, and current-entity state through managed typed document access");
}

static void RequireAetheriaManagedStateAccessorsCoverDomainDocuments(string root)
{
    var clientStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaClientState.cs");
    var observedDaemonStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeDaemonRenderView.cs");
    var daemonRuntimeDocumentTestsPath = Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Tests",
        "DaemonRuntimeDocumentTests.cs");
    var starbridgePlayerSeatTestsPath = Path.Combine(
        root,
        "Assets",
        "Scripts",
        "Tests",
        "StarbridgePlayerSeatDocumentTests.cs");

    var requiredPaths = new[]
    {
        clientStatePath,
        observedDaemonStatePath,
        daemonRuntimeDocumentTestsPath,
        starbridgePlayerSeatTestsPath
    };
    var missingPaths = requiredPaths.Where(path => !File.Exists(path)).ToArray();
    if (missingPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Cannot verify Aetheria managed state accessor coverage; missing sources: " +
            string.Join(", ", missingPaths.Select(path => Path.GetRelativePath(root, path))));
    }

    var clientState = File.ReadAllText(clientStatePath);
    var daemonRuntimeDocumentTests = File.ReadAllText(daemonRuntimeDocumentTestsPath);
    var topLevelClientState = clientState.Split(
        "public sealed class AetheriaClientDaemonState",
        StringSplitOptions.None)[0];
    var requiredClientSymbols = new[]
    {
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>()",
        "public enum AetheriaClientEveSurface",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaClientEveSurface surface)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(string seatId)"
    };
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose managed document handles for remaining domain documents: " +
            string.Join(", ", missingClientSymbols));
    }

    if (clientState.Contains("public Task<TDocument> LatestAsync<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public TDocument Latest<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public CultMeshReactiveDocument<TDocument> Reactive<TDocument>", StringComparison.Ordinal) ||
        clientState.Contains("public Observable<TDocument> Watch<TDocument>", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState must not reintroduce generic latest/reactive/watch shortcuts; callers should sample explicit document handles or CultMesh reactive documents.");
    }

    var forbiddenFixedReactiveWrappers = new[]
    {
        "LatestCatalog(",
        "LatestCatalogAsync(",
        "ReactiveCatalog(",
        "ReactiveCatalogAsync(",
        "LatestLoadoutTemplates(",
        "LatestLoadoutTemplatesAsync(",
        "ReactiveLoadoutTemplates(",
        "ReactiveLoadoutTemplatesAsync(",
        "LatestSectorMap(",
        "LatestSectorMapAsync(",
        "ReactiveSectorMap(",
        "ReactiveSectorMapAsync(",
        "LatestZoneContacts(",
        "LatestZoneContactsAsync(",
        "ReactiveZoneContacts(",
        "ReactiveZoneContactsAsync(",
        "LatestStationRefit(",
        "LatestStationRefitAsync(",
        "ReactiveStationRefit(",
        "ReactiveStationRefitAsync(",
        "LatestDaemonFrame(",
        "LatestDaemonFrameAsync(",
        "ReactiveDaemonFrame(",
        "ReactiveDaemonFrameAsync(",
        "LatestDaemonSoaView(",
        "LatestDaemonSoaViewAsync(",
        "ReactiveDaemonSoaView(",
        "ReactiveDaemonSoaViewAsync(",
        "LatestZoneRender(",
        "LatestZoneRenderAsync(",
        "ReactiveZoneRender(",
        "ReactiveZoneRenderAsync(",
        "LatestPlayer(",
        "LatestPlayerAsync(",
        "ReactivePlayer(",
        "ReactivePlayerAsync(",
        "LatestVerseHost(",
        "LatestVerseHostAsync(",
        "ReactiveVerseHost(",
        "ReactiveVerseHostAsync(",
        "LatestProviderAdvertisement(",
        "LatestProviderAdvertisementAsync(",
        "LatestHealth(",
        "LatestHealthAsync(",
        "LatestCommandBoundary(",
        "LatestCommandBoundaryAsync(",
        "LatestAuthorityPolicy(",
        "LatestAuthorityPolicyAsync(",
        "LatestFrameDocument(",
        "LatestFrameDocumentAsync(",
        "LatestSoaViewDocument(",
        "LatestSoaViewDocumentAsync(",
        "LatestScenario(",
        "LatestScenarioAsync(",
        "ReactiveScenario(",
        "ReactiveScenarioAsync(",
        "LatestSession(",
        "LatestSessionAsync(",
        "ReactiveSession(",
        "ReactiveSessionAsync(",
        "LatestSummary(",
        "LatestSummaryAsync(",
        "ReactiveSummary(",
        "ReactiveSummaryAsync(",
        "LatestGameSurface(",
        "LatestGameSurfaceAsync(",
        "ReactiveGameSurface(",
        "ReactiveGameSurfaceAsync(",
        "LatestGameTuiSurface(",
        "LatestGameTuiSurfaceAsync(",
        "ReactiveGameTuiSurface(",
        "ReactiveGameTuiSurfaceAsync(",
        "LatestEditorSurface(",
        "LatestEditorSurfaceAsync(",
        "ReactiveEditorSurface(",
        "ReactiveEditorSurfaceAsync(",
        "LatestEditorTuiSurface(",
        "LatestEditorTuiSurfaceAsync(",
        "ReactiveEditorTuiSurface(",
        "ReactiveEditorTuiSurfaceAsync("
    };
    var forbiddenObservedDaemonConvenienceWrappers = new[]
    {
        "LatestObservedDaemonAsync(",
        "LatestObservedDaemon()",
        "TryReadDaemonSoaViewAsync("
    };
    var survivingObservedDaemonConvenienceWrappers = forbiddenObservedDaemonConvenienceWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingObservedDaemonConvenienceWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState still exposes one-shot observed-daemon aggregate wrappers; use caller-owned managed typed documents: " +
            string.Join(", ", survivingObservedDaemonConvenienceWrappers));
    }
    var survivingFixedReactiveWrappers = forbiddenFixedReactiveWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingFixedReactiveWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must not reintroduce named fixed-document reactive wrappers; use Document<TDocument>() handles and CultMesh handle reactivity: " +
            string.Join(", ", survivingFixedReactiveWrappers));
    }

    var forbiddenClientStateWrappers = new[]
    {
        "public AetheriaClientDaemonState Daemon { get; }",
        "public AetheriaClientSettingsState Settings { get; }",
        "public AetheriaClientCurrentState Current { get; }",
        "public AetheriaClientViewportState Viewports { get; }",
        "public AetheriaClientDetailState Details { get; }",
        "public AetheriaClientStarbridgeState Starbridge { get; }",
        "public sealed class AetheriaClientDaemonState",
        "public sealed class AetheriaClientSettingsState",
        "public sealed class AetheriaClientCurrentState",
        "public sealed class AetheriaClientViewportState",
        "public sealed class AetheriaClientDetailState",
        "public sealed class AetheriaClientStarbridgeState"
    };
    var survivingClientStateWrappers = forbiddenClientStateWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingClientStateWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState still exposes nested wrapper state; use flat typed handles plus generic parameterized Document<T> access: " +
            string.Join(", ", survivingClientStateWrappers));
    }

    var forbiddenViewportConvenienceWrappers = new[]
    {
        "LatestMap(",
        "LatestMapAsync(",
        "ReactiveMap(",
        "ReactiveMapAsync(",
        "LatestObjects(",
        "LatestObjectsAsync(",
        "ReactiveObjects(",
        "ReactiveObjectsAsync(",
        "LatestGravity(",
        "LatestGravityAsync(",
        "ReactiveGravity(",
        "ReactiveGravityAsync(",
        "LatestRenderSplats(",
        "LatestRenderSplatsAsync(",
        "ReactiveRenderSplats(",
        "ReactiveRenderSplatsAsync("
    };
    var survivingViewportConvenienceWrappers = forbiddenViewportConvenienceWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingViewportConvenienceWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must use generic viewport document access instead of named latest/reactive wrappers: " +
            string.Join(", ", survivingViewportConvenienceWrappers));
    }

    var forbiddenDetailConvenienceWrappers = new[]
    {
        "LatestZone(",
        "LatestZoneAsync(",
        "ReactiveZone(",
        "ReactiveZoneAsync(",
        "LatestSelectedObject(",
        "LatestSelectedObjectAsync(",
        "ReactiveSelectedObject(",
        "ReactiveSelectedObjectAsync(",
        "LatestInventory(",
        "LatestInventoryAsync(",
        "ReactiveInventory(",
        "ReactiveInventoryAsync("
    };
    var survivingDetailConvenienceWrappers = forbiddenDetailConvenienceWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingDetailConvenienceWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must use generic indexed document access instead of named latest/reactive wrappers: " +
            string.Join(", ", survivingDetailConvenienceWrappers));
    }

    var forbiddenCurrentConvenienceWrappers = new[]
    {
        "LatestZone(",
        "LatestZoneAsync(",
        "ReactiveZone(",
        "ReactiveZoneAsync(",
        "LatestEntity(",
        "LatestEntityAsync(",
        "ReactiveEntity(",
        "ReactiveEntityAsync(",
        "LatestDocking(",
        "LatestDockingAsync(",
        "ReactiveDocking(",
        "ReactiveDockingAsync("
    };
    var survivingCurrentConvenienceWrappers = forbiddenCurrentConvenienceWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingCurrentConvenienceWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must use top-level generic document access instead of named current-state wrappers: " +
            string.Join(", ", survivingCurrentConvenienceWrappers));
    }

    var compactClientState = CompactSource(clientState);
    var requiredStarbridgeParameterizedSymbols = new[]
    {
        "publicCultMeshDocumentHandle<TDocument>Document<TDocument>(stringseatId)"
    };
    var missingStarbridgeParameterizedSymbols = requiredStarbridgeParameterizedSymbols
        .Where(symbol => !compactClientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingStarbridgeParameterizedSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose generic parameterized document access for player-seat state: " +
            string.Join(", ", missingStarbridgeParameterizedSymbols));
    }

    var forbiddenStarbridgeConvenienceWrappers = new[]
    {
        "LatestPlayerSeat(",
        "LatestPlayerSeatAsync(",
        "ReactivePlayerSeat(",
        "ReactivePlayerSeatAsync("
    };
    var survivingStarbridgeConvenienceWrappers = forbiddenStarbridgeConvenienceWrappers
        .Where(symbol => clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingStarbridgeConvenienceWrappers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must use generic parameterized document access instead of named player-seat latest/reactive wrappers: " +
            string.Join(", ", survivingStarbridgeConvenienceWrappers));
    }

    var observedDaemonState = File.ReadAllText(observedDaemonStatePath);
    if (!observedDaemonState.Contains("public static bool TryCreateCurrent(", StringComparison.Ordinal) ||
        !observedDaemonState.Contains("new AetheriaRuntimeDaemonRenderView(currentFrame, currentSoaView, currentZoneRender)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Observed daemon state must compose current values from managed typed frame, SoA, and zone-render documents.");
    }

    if (File.Exists(Path.Combine(
            root,
            "Packages",
            "org.gamecult.aetheria.state",
            "Runtime",
            "AetheriaRuntimeObservedDockingState.cs")))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeObservedDockingState must not return as a docking aggregate wrapper; callers should own the managed typed docking documents directly.");
    }

    if (!daemonRuntimeDocumentTests.Contains("currentDockingReactive.Current.CurrentEntityKey", StringComparison.Ordinal) ||
        daemonRuntimeDocumentTests.Contains("AetheriaRuntimeObservedDockingState", StringComparison.Ordinal) ||
        daemonRuntimeDocumentTests.Contains("client.State.CurrentDocking()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon runtime document tests must teach direct docking samples from caller-owned managed reactive typed documents.");
    }

    if (observedDaemonState.Contains("state.LatestFrame.ReactiveAsync", StringComparison.Ordinal) ||
        observedDaemonState.Contains("state.LatestSoaView.ReactiveAsync", StringComparison.Ordinal) ||
        observedDaemonState.Contains("AetheriaRuntimeReactiveObservedDaemonState", StringComparison.Ordinal) ||
        observedDaemonState.Contains("ReadAsync(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Observed daemon state must not hide typed document access behind aggregate reactive compatibility wrappers.");
    }

    if (observedDaemonState.Contains("state.LatestAsync<AetheriaRuntimeDaemonFrameDocument>()", StringComparison.Ordinal) ||
        observedDaemonState.Contains("state.LatestDaemonSoaViewAsync()", StringComparison.Ordinal) ||
        observedDaemonState.Contains("state.ReactiveObservedDaemonAsync()", StringComparison.Ordinal) ||
        observedDaemonState.Contains("TryReadLatestSoaViewAsync", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Observed daemon state ReadAsync still performs one-shot daemon reads instead of sampling the managed reactive observed document.");
    }

    var checkedSources = new Dictionary<string, string>
    {
        ["DaemonRuntimeDocumentTests.cs"] = File.ReadAllText(daemonRuntimeDocumentTestsPath),
        ["StarbridgePlayerSeatDocumentTests.cs"] = File.ReadAllText(starbridgePlayerSeatTestsPath)
    };
    var forbiddenCompactedSymbols = new[]
    {
        ".State.Catalog.LatestAsync()",
        ".State.Settings.Player.LatestAsync()",
        ".State.Settings.VerseHost.LatestAsync()",
        ".State.Current.Entity.LatestAsync()",
        ".State.Viewports.Objects(",
        ".State.Details.Zone(",
        ".State.Details.Inventory(",
        ".State.Starbridge.PlayerSeat(",
        ".State.DockingState.Latest(",
        ".State.DockingState.Reactive(",
        "AetheriaRuntimeDaemonRenderView.ReadAsync(",
        ".State.Daemon.LatestFrame.LatestAsync()",
        ".State.Daemon.LatestSoaView.LatestAsync()",
        ".State.Daemon.AuthorityPolicy.LatestAsync()",
        ".State.Daemon.GameSurface.LatestAsync()",
        ".State.Daemon.GameTuiSurface.LatestAsync()",
        ".State.Daemon.EditorSurface.LatestAsync()",
        ".State.Daemon.EditorTuiSurface.LatestAsync()",
        "client.State.Daemon.LatestGameSurface()",
        "client.State.Daemon.LatestGameTuiSurface()",
        "client.State.Daemon.LatestEditorSurface()",
        "client.State.Daemon.LatestEditorTuiSurface()",
        "client.State.LatestGameSurface()",
        "client.State.LatestGameTuiSurface()",
        "client.State.LatestEditorSurface()",
        "client.State.LatestEditorTuiSurface()",
        "client.State.ReactiveGameSurface()",
        "client.State.ReactiveGameTuiSurface()",
        "client.State.ReactiveEditorSurface()",
        "client.State.ReactiveEditorTuiSurface()",
        "client.State.ObserveCatalog()",
        "client.State.ObserveDaemonFrame()",
        "client.State.ObserveLoadoutTemplates()",
        "client.State.ObserveSectorMap()",
        "client.State.Settings.ObservePlayer()",
        "client.State.ObserveZoneContacts()",
        "client.State.Details.ObserveInventory(0)",
        "client.State.Viewports.ObserveObjects(viewport)",
        "client.State.Viewports.ObserveRenderSplats(viewport)"
    }
        .Select(CompactSource)
        .ToArray();
    var offenders = checkedSources
        .Select(entry => new
        {
            entry.Key,
            Compact = CompactSource(entry.Value)
        })
        .Where(entry => forbiddenCompactedSymbols.Any(symbol => entry.Compact.Contains(symbol, StringComparison.Ordinal)))
        .Select(entry => entry.Key)
        .ToArray();

    if (offenders.Length > 0)
    {
        throw new InvalidOperationException(
            "Managed state examples still teach raw CultMesh handle walks for ordinary latest reads: " +
            string.Join(", ", offenders));
    }

    if (!clientState.Contains("StarbridgeSummary", StringComparison.Ordinal) ||
        !checkedSources["DaemonRuntimeDocumentTests.cs"].Contains(
            "client.State.Document<AetheriaRuntimeStarbridgeSessionSummaryDocument>()",
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Starbridge summary must be registered in managed Aetheria typed document access.");
    }

    if (!checkedSources["StarbridgePlayerSeatDocumentTests.cs"].Contains(
            ".Document<AetheriaRuntimeStarbridgePlayerSeatDocument>(seat.SeatId).Reactive()",
            StringComparison.Ordinal) ||
        !checkedSources["StarbridgePlayerSeatDocumentTests.cs"].Contains(
            ".Document<AetheriaRuntimeStarbridgePlayerSeatDocument>(seat.SeatId)",
            StringComparison.Ordinal) ||
        checkedSources["StarbridgePlayerSeatDocumentTests.cs"].Contains(
            ".Latest<AetheriaRuntimeStarbridgePlayerSeatDocument>(seat.SeatId)",
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Starbridge player-seat examples must use generic parameterized managed document access instead of named wrappers or raw handle walks.");
    }

    Console.WriteLine("Domain document accessors: managed Aetheria state uses generic fixed-document reads plus generic parameterized viewport/detail/Starbridge/Eve-surface access");
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

    if (!registry.Contains("CultMesh.CreateCultNetDocumentRegistry(DocumentTypes, registry)", StringComparison.Ordinal) ||
        !registry.Contains("typeof(AetheriaVerseHostSettings)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria document registry does not register typed verse-host settings.");
    }

    if (!node.Contains("public CultMeshMutableStatePointer<TDocument> MutableDocument<TDocument>(CultRecordKey key)", StringComparison.Ordinal) ||
        !node.Contains("public static CultRecordKey VerseHostSettingsKey", StringComparison.Ordinal) ||
        !node.Contains("public static CultRecordKey PlayerSettingsKey", StringComparison.Ordinal) ||
        !node.Contains("public static CultRecordKey EveCommandAcceptanceStatusKey", StringComparison.Ordinal) ||
        !node.Contains("public CultMeshNode MeshNode => _node;", StringComparison.Ordinal) ||
        !node.Contains("global:aetheria.verse_host_settings.v1", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state node does not expose typed verse-host/settings managed state pointers.");
    }

    if (node.Contains("public CultMeshMutableStatePointer<AetheriaVerseHostSettings> VerseHostSettings()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<AetheriaPlayerSettings> PlayerSettings()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<AetheriaEveCommandAcceptanceStatus> EveCommandAcceptanceStatus()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<EveSurfaceState> CatalogSurface()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<EveSurfaceState> OperationsSurface()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<EveSurfaceState> PlayerSettingsSurface()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<EveProviderAdvertisementState> ProviderAdvertisementSurface()", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<AetheriaRuntimeSession> RuntimeSession(", StringComparison.Ordinal) ||
        node.Contains("public CultMeshMutableStatePointer<AetheriaTradeValuePolicy> TradeValuePolicy()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state node still exposes named document helpers instead of generic mutable typed document access.");
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
        "node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReadAsync()",
        "node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey)",
        ".ReplaceAsync(normalized)",
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
    var gameplayBootShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayBootShell.cs");
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
        gameplayBootShellPath,
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
    var gameplayBootShell = File.ReadAllText(gameplayBootShellPath);
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

    if (aetheriaState.Contains("AetheriaRuntimeStateReader", StringComparison.Ordinal) ||
        aetheriaState.Contains("TryReadDaemonFrame", StringComparison.Ordinal) ||
        aetheriaState.Contains("ReadVerseHostSettings", StringComparison.Ordinal) ||
        aetheriaState.Contains("AetheriaRuntimeDaemonFrameStore", StringComparison.Ordinal) ||
        aetheriaState.Contains("AetheriaRuntimeCatalogStore", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes file-backed daemon state reads; clients should use AetheriaRuntimeVerseClient.");
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
        "private AetheriaUnityGameplayBootShell GameplayBootShell =>",
        "var boot = GameplayBootShell.Boot();",
        "SceneWiring.ConfigureCurrentEntityPresentation(_currentEntityPresentation, boot.RuntimeCatalog);",
        "SceneWiring.ConfigureTargetPresentation(",
        "boot.RuntimeCatalog,",
        "SceneWiring.ConfigureActionBarPresentation(",
        "ItemManager = boot.ItemManager;",
        "_loadoutItemFactory = boot.LoadoutItemFactory;"
    };
    var missingActionGameManagerSymbols = requiredActionGameManagerSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingActionGameManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer delegates gameplay boot through AetheriaUnityGameplayBootShell: " +
            string.Join(", ", missingActionGameManagerSymbols));
    }

    var forbiddenActionGameManagerBootSymbols = new[]
    {
        "AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory)",
        "Aetheria runtime target: {stateBoot.TargetLabel} via {stateBoot.TargetKind} ({stateBoot.TargetSource})",
        "!stateBoot.SupportsLocalStateFileRead",
        "stateBoot.StateFileExists",
        "private static AetheriaRuntimeCatalogSnapshot _runtimeCatalog",
        "FindTypedRuntimeItem(",
        "_runtimeCatalog?.FindItem(",
        ".OpenRuntimeCatalog()"
    };
    var actionGameManagerBootHits = forbiddenActionGameManagerBootSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (actionGameManagerBootHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns runtime-target boot details instead of the Unity gameplay boot shell: " +
            string.Join(", ", actionGameManagerBootHits));
    }

    var requiredGameplayBootShellSymbols = new[]
    {
        "public sealed class AetheriaUnityGameplayBootShell",
        "public AetheriaUnityGameplayBootResult Boot()",
        "AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory)",
        "Aetheria runtime target: {stateBoot.TargetLabel} via {stateBoot.TargetKind} ({stateBoot.TargetSource})",
        "Aetheria runtime id: {stateBoot.RuntimeId}",
        "Aetheria runtime state file: {stateBoot.StateFilePath}",
        "!stateBoot.SupportsLocalStateFileRead",
        "stateBoot.StateFileExists",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot.StateFilePath, stateBoot.RuntimeId)",
        ".State",
        ".Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "runtimeCatalogDocument.Current",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "sectorMapDocument.Current",
        "new ItemManager(",
        "new AetheriaUnityLoadoutItemFactory(itemManager, runtimeCatalog)",
        "ZoneRenderer.SetDroppedPickupItemFactory(loadoutItemFactory.CreateLoadoutItem)",
        "ZoneRenderer.BodySettingsCollections = Settings.BodySettingsCollections",
        "AetheriaUnityRenderSettingsBridge.Build(",
        "CockpitHudShell.SetRenderSettings(ZoneRenderer.RenderSettings)",
        "AetheriaUnityRuntimeClientProvider.PlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap",
        "public readonly struct AetheriaUnityGameplayBootResult"
    };
    var missingGameplayBootShellSymbols = requiredGameplayBootShellSymbols
        .Where(symbol => !gameplayBootShell.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingGameplayBootShellSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplayBootShell no longer boots gameplay through the shared client-target report: " +
            string.Join(", ", missingGameplayBootShellSymbols));
    }

    if (gameplayBootShell.Contains(".ObserveCatalog()", StringComparison.Ordinal) ||
        gameplayBootShell.Contains(".ObserveSectorMap()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplayBootShell still routes boot catalog/sector-map reads through legacy session wrappers instead of reactive typed documents.");
    }

    var requiredPresenterSymbols = new[]
    {
        "AetheriaRuntimeStateBoot.Inspect(",
        "AetheriaUnityRuntimePaths.GameDataDirectory",
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
        "ResolveSectorMap(AetheriaRuntimeStateBootReport stateBoot)",
        "AetheriaClient",
        ".State",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "ResolveVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)",
        "AetheriaState.At(AetheriaUnityRuntimePaths.GameDataDirectory)",
        ".ClientTarget",
        "RequestClientTargetCommand(request)",
        "AetheriaRuntimeClientTargetSurfaceCommands.TryRequest(",
        "ResolvePlayerSettings(AetheriaRuntimeStateBootReport stateBoot)",
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
        "<ProjectReference Include=\"..\\Aetheria.State\\Aetheria.State.csproj\" />"
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

    if (replicaToolProject.Contains("Aetheria.State.Unity", StringComparison.Ordinal) ||
        replicaToolProject.Contains("GameCult.Aetheria.State.Unity", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Verse replica tool should not depend on Unity state assemblies; replica sync must stay daemon-state native.");
    }

    var requiredProgramSymbols = new[]
    {
        "sync",
        "follow",
        "AetheriaVerseReplica",
        "ResolveReplicaPath",
        "--endpoint",
        "--replica",
        "--game-data-root",
        "--verse-id",
        "--runtime-id"
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
        "ApplyRawSnapshotResponseAsync"
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

    if (aetheriaState.Contains("AetheriaRuntimeStateReader", StringComparison.Ordinal) ||
        aetheriaState.Contains("TryReadDaemonFrame", StringComparison.Ordinal) ||
        aetheriaState.Contains("ReadVerseHostSettings", StringComparison.Ordinal) ||
        aetheriaState.Contains("AetheriaRuntimeDaemonFrameStore", StringComparison.Ordinal) ||
        aetheriaState.Contains("AetheriaRuntimeCatalogStore", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria state sugar still exposes file-backed daemon state reads; clients should use AetheriaRuntimeVerseClient.");
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
        ".Ui.SurfaceCommandAsync(request, \"unity-main-menu\")",
        "AetheriaState.At(AetheriaUnityRuntimePaths.GameDataDirectory)",
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
        "node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey)",
        ".ReplaceAsync(normalized)",
        "node.MutableDocument<EveProviderAdvertisementState>(AetheriaStateNode.ProviderAdvertisementSurfaceKey)",
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

    if (daemonDocuments.Contains("public sealed class AetheriaRuntimeActionBarBindingCommand", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Action-bar input mappings are Unity-local and must not be daemon command payloads.");
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
        "Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeDaemonFrameDocument?, AetheriaRuntimeDaemonCommandEnvelope> submit",
        "return Submit((client, frame) => client.SetTarget(frame, targetEntityKey));",
        "return Send((client, frame) => client.TransferCargoItem(",
        "command.CargoTransfer.OriginEntityKey",
        "command.TradePurchase.TotalPrice",
        "command.LootPickup.ItemKey",
        "command.LoadoutRestore.TemplateName",
        "command.EquipmentTransfer.SourceKind",
        "command.StoreItem.SourceEquipmentIndex"
    };
    var daemonOperationsClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperationsClient.cs");
    var daemonOperationsClient = File.Exists(daemonOperationsClientPath)
        ? File.ReadAllText(daemonOperationsClientPath)
        : throw new InvalidOperationException("Shared daemon operations client is missing from the runtime package.");
    var requiredDaemonOperationsFacadeSymbols = new[]
    {
        "using GameCult.Mesh;",
        "public AetheriaRuntimeDaemonCommandEnvelope SetTarget(string targetEntityKey)",
        "public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(",
        "public CultMeshOperationReceipt TransferCargoItem(",
        "private AetheriaRuntimeDaemonCommandEnvelope Submit(",
        "private CultMeshOperationReceipt Send("
    };
    var missingDaemonOperationsFacadeSymbols = requiredDaemonOperationsFacadeSymbols
        .Where(symbol => !daemonOperationsClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonOperationsFacadeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria runtime operations facade no longer exposes shared CultMesh receipts: " +
            string.Join(", ", missingDaemonOperationsFacadeSymbols));
    }

    var forbiddenPublicEnvelopeSymbols = new[]
    {
        "public AetheriaRuntimeDaemonCommandEnvelope ClearTarget(",
        "public AetheriaRuntimeDaemonCommandEnvelope TransferCargoItem(",
        "public AetheriaRuntimeDaemonCommandEnvelope SetLookDirection(",
        "public AetheriaRuntimeDaemonCommandEnvelope SetTractorPower("
    };
    var publicEnvelopeHits = forbiddenPublicEnvelopeSymbols
        .Where(symbol => daemonOperationsClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicEnvelopeHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria runtime operations facade exposes command envelopes beyond the typed identity-sensitive command path: " +
            string.Join(", ", publicEnvelopeHits));
    }

    var legacyUnityOperationsPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonOperations.cs");
    if (File.Exists(legacyUnityOperationsPath))
    {
        throw new InvalidOperationException(
            "Aetheria daemon operation facade still lives under Unity gameplay; move shared client operations into the runtime package.");
    }

    var clientAndObserver = daemonClient + "\n" + observer + "\n" + stateNode + "\n" + daemonOperationsClient;
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !clientAndObserver.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon command producers are not filling typed payloads: " +
            string.Join(", ", missingClientSymbols));
    }

    if (clientAndObserver.Contains("ActionBarBinding", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon command producers still expose Unity-local action-bar input bindings.");
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
        ["Unity daemon observer"] = observer
    }
        .SelectMany(source => forbiddenUnityDocumentSubmitSymbols
            .Where(symbol => source.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{source.Key}: {symbol}"))
        .ToArray();
    if (unityDocumentSubmitHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity daemon operation controllers still fill command documents instead of delegating to typed runtime operations: " +
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

static void RequireAuthoritySmokeUsesManagedPointers(string root)
{
    var authoritySmokePath = Path.Combine(root, "Aetheria.State.AuthoritySmoke", "Program.cs");
    if (!File.Exists(authoritySmokePath))
    {
        throw new InvalidOperationException("Authority smoke source is missing.");
    }

    var authoritySmoke = File.ReadAllText(authoritySmokePath);
    var requiredSymbols = new[]
    {
        "writer.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)",
        "writer.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest)",
        "writer.MutableDocument<AetheriaRuntimeStarbridgeSessionDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest)",
        "ravenNode.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)",
        "starfireNode.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)",
        "node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)",
        "node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)",
        ".ReplaceAsync(policy)",
        ".ReplaceAsync(scenario)",
        ".ReplaceAsync(session)",
        ".ReadAsync().ConfigureAwait(false)"
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !authoritySmoke.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Authority smoke no longer exercises managed Verse state pointers: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "PutStarbridgeScenarioAsync(",
        "PutStarbridgeSessionAsync(",
        "PutVerseAuthorityPolicyAsync(",
        "GetVerseAuthorityPolicyAsync(",
        "GetDaemonFrameAsync(",
        "writer.StarbridgeScenario()",
        "writer.StarbridgeSession()",
        "ravenNode.VerseAuthorityPolicy()",
        "starfireNode.VerseAuthorityPolicy()",
        "node.VerseAuthorityPolicy()",
        "node.LatestFrame()"
    };
    var survivingSymbols = forbiddenSymbols
        .Where(symbol => authoritySmoke.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Authority smoke still teaches compatibility helper access instead of managed Verse pointers: " +
            string.Join(", ", survivingSymbols));
    }
}

static void RequireAetheriaStateNodeUsesManagedPointers(string root)
{
    var stateNodePath = Path.Combine(root, "Aetheria.State", "AetheriaStateNode.cs");
    if (!File.Exists(stateNodePath))
    {
        throw new InvalidOperationException("AetheriaStateNode source is missing.");
    }

    var stateNode = File.ReadAllText(stateNodePath);
    var requiredSymbols = new[]
    {
        "public static CultRecordKey WorldKey { get; }",
        "public static CultRecordKey MigrationLedgerKey { get; }",
        "public static CultRecordKey LegacyCatalogQuarantineKey { get; }",
        "public CultMeshMutableStatePointer<TDocument> MutableDocument<TDocument>(CultRecordKey key)",
        "public IReadOnlyList<TDocument> Documents<TDocument>()",
        "private CultMeshMutableStatePointer<T> MutableDocumentPointer<T>(CultRecordKey key)",
        "CultMesh.MutableStatePointer("
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !stateNode.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaStateNode no longer exposes managed typed state pointers: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "public Task<CultRecordHandle<AetheriaWorldState>> PutWorldAsync(",
        "public Task<AetheriaWorldState?> GetWorldAsync(",
        "public Task<CultRecordHandle<AetheriaMigrationLedger>> PutMigrationLedgerAsync(",
        "public Task<AetheriaMigrationLedger?> GetMigrationLedgerAsync(",
        "public Task<CultRecordHandle<AetheriaItemDefinition>> PutItemDefinitionAsync(",
        "public Task<AetheriaItemDefinition?> GetItemDefinitionAsync(",
        "public Task<AetheriaItemDefinition?> GetItemDefinitionByLegacyIdAsync(",
        "public Task<CultRecordHandle<AetheriaCorporation>> PutCorporationAsync(",
        "public Task<AetheriaCorporation?> GetCorporationAsync(",
        "public Task<CultRecordHandle<AetheriaNameFile>> PutNameFileAsync(",
        "public Task<AetheriaNameFile?> GetNameFileAsync(",
        "public Task<CultRecordHandle<AetheriaTradeValuePolicy>> PutTradeValuePolicyAsync(",
        "public Task<AetheriaTradeValuePolicy?> GetTradeValuePolicyAsync(",
        "public Task<CultRecordHandle<EveSurfaceState>> PutCatalogSurfaceAsync(",
        "public Task<EveSurfaceState?> GetCatalogSurfaceAsync(",
        "public Task<CultRecordHandle<EveProviderAdvertisementState>> PutProviderAdvertisementAsync(",
        "public Task<EveProviderAdvertisementState?> GetProviderAdvertisementAsync(",
        "public Task<CultRecordHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument>> PutDaemonProviderAdvertisementAsync(",
        "public Task<AetheriaRuntimeDaemonProviderAdvertisementDocument?> GetDaemonProviderAdvertisementAsync(",
        "public Task<CultRecordHandle<AetheriaRuntimeVerseAuthorityPolicyDocument>> PutVerseAuthorityPolicyAsync(",
        "public Task<AetheriaRuntimeVerseAuthorityPolicyDocument?> GetVerseAuthorityPolicyAsync(",
        "public Task<CultRecordHandle<AetheriaRuntimeDaemonFrameDocument>> PutDaemonFrameAsync(",
        "public Task<AetheriaRuntimeDaemonFrameDocument?> GetDaemonFrameAsync(",
        "public Task<CultRecordHandle<AetheriaRuntimeDaemonSoaViewDocument>> PutDaemonSoaViewAsync(",
        "public Task<AetheriaRuntimeDaemonSoaViewDocument?> GetDaemonSoaViewAsync(",
        "public Task<CultRecordHandle<AetheriaRuntimeStarbridgeScenarioDocument>> PutStarbridgeScenarioAsync(",
        "public Task<AetheriaRuntimeStarbridgeScenarioDocument?> GetStarbridgeScenarioAsync(",
        "public Task<CultRecordHandle<EveSurfaceState>> PutDaemonGameSurfaceAsync(",
        "public Task<EveSurfaceState?> GetDaemonGameSurfaceAsync(",
        "public Task<CultRecordHandle<AetheriaRuntimeSession>> PutRuntimeSessionAsync(",
        "public Task<AetheriaRuntimeSession?> GetRuntimeSessionAsync(",
        "public Task<CultRecordHandle<AetheriaPlayerSettings>> PutPlayerSettingsAsync(",
        "public Task<AetheriaPlayerSettings?> GetPlayerSettingsAsync(",
        "public Task<CultRecordHandle<AetheriaRunState>> PutRunStateAsync(",
        "public Task<AetheriaRunState?> GetRunStateAsync(",
        "public Task<CultRecordHandle<AetheriaEntitySnapshot>> PutEntitySnapshotAsync(",
        "public Task<AetheriaEntitySnapshot?> GetEntitySnapshotAsync(",
        "public Task<CultRecordHandle<AetheriaVerseHostSettings>> PutVerseHostSettingsAsync(",
        "public Task<AetheriaVerseHostSettings?> GetVerseHostSettingsAsync(",
        "public CultMeshMutableStatePointer<AetheriaWorldState> World()",
        "public CultMeshMutableStatePointer<AetheriaMigrationLedger> MigrationLedger()",
        "public CultMeshMutableStatePointer<AetheriaLegacyCatalogQuarantine> LegacyCatalogQuarantine()",
        "public CultMeshMutableStatePointer<AetheriaItemDefinition> ItemDefinition(",
        "public CultMeshMutableStatePointer<AetheriaItemDefinition> ItemDefinitionByLegacyId(",
        "public CultMeshMutableStatePointer<AetheriaCorporation> Corporation(",
        "public CultMeshMutableStatePointer<AetheriaCorporation> CorporationByLegacyId(",
        "public CultMeshMutableStatePointer<AetheriaNameFile> NameFile(",
        "public CultMeshMutableStatePointer<AetheriaNameFile> NameFileByLegacyId(",
        "public CultMeshMutableStatePointer<AetheriaLoadoutTemplate> LoadoutTemplate(",
        "public CultMeshMutableStatePointer<AetheriaRunState> RunState(",
        "public CultMeshMutableStatePointer<AetheriaZoneState> ZoneState(",
        "public CultMeshMutableStatePointer<AetheriaEntitySnapshot> EntitySnapshot(",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonHealthDocument> Health()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeVerseAuthorityPolicyDocument> VerseAuthorityPolicy()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonFrameDocument> LatestFrame()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSessionSummary()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameSurface()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameTuiSurface()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorSurface()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorTuiSurface()",
        "ReadObservedDaemonCommands(",
        "ReadCommittedCommandFacts(",
        "ReadAuthorityLeases(",
        "ReadObservedEveCommands("
    };
    var survivingSymbols = forbiddenSymbols
        .Where(symbol => stateNode.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaStateNode still exposes compatibility document Get/Put helpers instead of managed typed pointers: " +
            string.Join(", ", survivingSymbols));
    }
}

static void RequireDaemonVersePublication(string root)
{
    var daemonDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonDocuments.cs");
    var daemonTickRunnerPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonTickRunner.cs");
    var daemonSoaDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSoaDocuments.cs");
    var daemonSoaFramePublisherPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSoaFramePublisher.cs");
    var daemonStateRefsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonStateRefs.cs");
    var daemonGameSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonGameSurfaceBuilder.cs");
    var daemonEditorSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonEditorSurfaceBuilder.cs");
    var statRecipeSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStatRecipeSurfaceBuilder.cs");
    var tradeValuePolicySurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeTradeValuePolicySurfaceBuilder.cs");
    var runtimeCatalogStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs");
    var daemonSurfaceCommandCatalogPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSurfaceCommandCatalog.cs");
    var committedFactImporterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCommittedFactImporter.cs");
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
        daemonSoaDocumentsPath,
        daemonSoaFramePublisherPath,
        daemonStateRefsPath,
        daemonGameSurfaceBuilderPath,
        daemonEditorSurfaceBuilderPath,
        statRecipeSurfaceBuilderPath,
        tradeValuePolicySurfaceBuilderPath,
        runtimeCatalogStorePath,
        daemonSurfaceCommandCatalogPath,
        committedFactImporterPath,
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
    var daemonSoaDocuments = File.ReadAllText(daemonSoaDocumentsPath);
    var daemonSoaFramePublisher = File.ReadAllText(daemonSoaFramePublisherPath);
    var daemonStateRefs = File.ReadAllText(daemonStateRefsPath);
    var daemonGameSurfaceBuilder = File.ReadAllText(daemonGameSurfaceBuilderPath);
    var daemonEditorSurfaceBuilder = File.ReadAllText(daemonEditorSurfaceBuilderPath);
    var statRecipeSurfaceBuilder = File.ReadAllText(statRecipeSurfaceBuilderPath);
    var tradeValuePolicySurfaceBuilder = File.ReadAllText(tradeValuePolicySurfaceBuilderPath);
    var runtimeCatalogStore = File.ReadAllText(runtimeCatalogStorePath);
    var daemonSurfaceCommandCatalog = File.ReadAllText(daemonSurfaceCommandCatalogPath);
    var committedFactImporter = File.ReadAllText(committedFactImporterPath);
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
        "FrameRecordRef",
        "SoaViewRecordRef",
        "EveGuiSurfaceRecordRef",
        "EveTuiSurfaceRecordRef",
        "EditorGuiSurfaceRecordRef",
        "EditorTuiSurfaceRecordRef"
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

    var forbiddenDaemonProviderDocumentSymbols = new[]
    {
        "StateWitnessPath",
        "FrameWitnessPath",
        "SoaWitnessPath",
        "HealthWitnessPath",
        "CommandBoundaryWitnessPath",
        "EveGuiSurfaceWitnessPath",
        "EveTuiSurfaceWitnessPath",
        "EditorGuiSurfaceWitnessPath",
        "EditorTuiSurfaceWitnessPath",
        "AssetManifestWitnessPath"
    };
    var daemonProviderDocumentHits = forbiddenDaemonProviderDocumentSymbols
        .Where(symbol => daemonDocuments.Contains(symbol, StringComparison.Ordinal) ||
                         daemonEditorSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (daemonProviderDocumentHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon provider advertisement still exposes witness-path vocabulary instead of managed record refs: " +
            string.Join(", ", daemonProviderDocumentHits));
    }

    if (!daemonSoaDocuments.Contains("[CultDocument(\"gamecult.aetheria.daemon_soa_view\", \"gamecult.aetheria.daemon_soa_view.v1\")]", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Daemon SoA view is no longer a registered CultCache/CultNet document.");
    }

    var requiredSoaFramePublisherSymbols = new[]
    {
        "public static class AetheriaRuntimeDaemonSoaFramePublisher",
        "BuildCurrentZoneEntities(",
        "AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile",
        "MemoryMappedFile.CreateOrOpen(",
        "ObserverWritable = false",
        "AetheriaRuntimeDaemonSoaColumnKinds.EntityIndex",
        "AetheriaRuntimeDaemonSoaColumnKinds.Position",
        "AetheriaRuntimeDaemonSoaColumnKinds.Velocity",
        "AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyRadius",
        "AetheriaRuntimeDaemonSoaColumnKinds.RenderVisibility",
        "AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId"
    };
    var missingSoaFramePublisherSymbols = requiredSoaFramePublisherSymbols
        .Where(symbol => !daemonSoaFramePublisher.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSoaFramePublisherSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon SoA frame publisher no longer turns authoritative frames into observer-readable direct-memory slabs: " +
            string.Join(", ", missingSoaFramePublisherSymbols));
    }

    var requiredBoundarySymbols = new[]
    {
        "RuntimeDaemonProviderFileSuffix",
        "RuntimeDaemonHealthFileSuffix",
        "RuntimeDaemonCommandBoundaryFileSuffix",
        "RuntimeDaemonStarbridgeSessionSummaryFileSuffix",
        "RuntimeDaemonGameSurfaceFileSuffix",
        "RuntimeDaemonGameTuiSurfaceFileSuffix",
        "RuntimeDaemonEditorSurfaceFileSuffix",
        "RuntimeDaemonEditorTuiSurfaceFileSuffix",
        "GetDaemonProviderPath(",
        "GetDaemonHealthPath(",
        "GetDaemonCommandBoundaryPath(",
        "GetDaemonStarbridgeSessionSummaryPath(",
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

    var forbiddenPublicationStoreSymbols = new[]
    {
        "WriteDaemonFrame(",
        "ReadDaemonFrame(",
        "WriteDaemonSoaView(",
        "ReadDaemonSoaView(",
        "WriteDaemonProviderAdvertisement(",
        "ReadDaemonProviderAdvertisement(",
        "WriteDaemonHealth(",
        "ReadDaemonHealth(",
        "WriteVerseAuthorityPolicy(",
        "ReadVerseAuthorityPolicy(",
        "WriteDaemonCommandBoundary(",
        "ReadDaemonCommandBoundary(",
        "WriteStarbridgeSessionSummary(",
        "ReadStarbridgeSessionSummary(",
        "WriteDaemonGameSurface(",
        "ReadDaemonGameSurface(",
        "WriteDaemonEditorSurface(",
        "ReadDaemonEditorSurface(",
        "TryReadProviderAdvertisement(",
        "TryReadHealth(",
        "TryReadVerseAuthorityPolicy(",
        "TryReadCommandBoundary(",
        "TryReadAssetManifest(",
        "TryReadStarbridgeSessionSummary(",
        "TryReadGameSurface(",
        "TryReadGameTuiSurface(",
        "TryReadEditorSurface(",
        "TryReadEditorTuiSurface("
    };
    var publicationStoreReaderHits = forbiddenPublicationStoreSymbols
        .Where(symbol => documentStore.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicationStoreReaderHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon latest publication still exposes file-backed sidecar helpers instead of managed typed documents: " +
            string.Join(", ", publicationStoreReaderHits));
    }

    var daemonFrameStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonFrameStore.cs");
    if (File.Exists(daemonFrameStorePath))
    {
        throw new InvalidOperationException(
            "Daemon latest frames must stay on managed CultMesh pointers; delete AetheriaRuntimeDaemonFrameStore.cs instead of reviving sidecar path writes.");
    }

    var daemonPublicationStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonPublicationStore.cs");
    if (File.Exists(daemonPublicationStorePath))
    {
        throw new InvalidOperationException(
            "Daemon latest publications must stay on managed CultMesh pointers; delete AetheriaRuntimeDaemonPublicationStore.cs instead of reviving sidecar path writes.");
    }

    var daemonSoaViewStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonSoaViewStore.cs");
    if (File.Exists(daemonSoaViewStorePath))
    {
        throw new InvalidOperationException(
            "Daemon SoA latest-view publication must stay on managed CultMesh pointers; delete AetheriaRuntimeDaemonSoaViewStore.cs instead of reviving sidecar path writes.");
    }

    var requiredDaemonRegistrySymbols = new[]
    {
        "CultMesh.CreateCultCacheDocumentRegistry(DocumentTypes)",
        "CultMesh.CreateCultNetDocumentRegistry(DocumentTypes, registry)",
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
        "public string RuntimeId { get; }",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(CultRecordKey key)",
        "CultMesh.Document<TDocument>(",
        "Database.WatchRecord<TDocument>(key)",
        "public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> RuntimeCatalog()",
        "AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath)",
        "public CultMeshMutableStatePointer<TDocument> MutableDocument<TDocument>(CultRecordKey key)",
        "public static CultRecordKey WorldKey { get; }",
        "public static CultRecordKey MigrationLedgerKey { get; }",
        "public static CultRecordKey LegacyCatalogQuarantineKey { get; }",
        "private CultMeshMutableStatePointer<T> MutableDocumentPointer<T>(CultRecordKey key)",
        "CultMesh.MutableStatePointer(",
        "AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest"
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

    var forbiddenDaemonNodeSymbols = new[]
    {
        "public CultMeshMutableStatePointer<AetheriaWorldState> World()",
        "public CultMeshMutableStatePointer<AetheriaMigrationLedger> MigrationLedger()",
        "public CultMeshMutableStatePointer<AetheriaLegacyCatalogQuarantine> LegacyCatalogQuarantine()",
        "public CultMeshMutableStatePointer<AetheriaItemDefinition> ItemDefinition(",
        "public CultMeshMutableStatePointer<AetheriaItemDefinition> ItemDefinitionByLegacyId(",
        "public CultMeshMutableStatePointer<AetheriaCorporation> Corporation(",
        "public CultMeshMutableStatePointer<AetheriaCorporation> CorporationByLegacyId(",
        "public CultMeshMutableStatePointer<AetheriaNameFile> NameFile(",
        "public CultMeshMutableStatePointer<AetheriaNameFile> NameFileByLegacyId(",
        "public CultMeshMutableStatePointer<AetheriaLoadoutTemplate> LoadoutTemplate(",
        "public CultMeshMutableStatePointer<AetheriaRunState> RunState(",
        "public CultMeshMutableStatePointer<AetheriaZoneState> ZoneState(",
        "public CultMeshMutableStatePointer<AetheriaEntitySnapshot> EntitySnapshot(",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonHealthDocument> Health()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeVerseAuthorityPolicyDocument> VerseAuthorityPolicy()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonFrameDocument> LatestFrame()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeScenarioDocument> StarbridgeScenario()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionDocument> StarbridgeSession()",
        "public CultMeshMutableStatePointer<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSessionSummary()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameSurface()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonGameTuiSurface()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorSurface()",
        "public CultMeshMutableStatePointer<EveSurfaceState> DaemonEditorTuiSurface()"
    };
    var survivingDaemonNodeSymbols = forbiddenDaemonNodeSymbols
        .Where(symbol => stateNode.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingDaemonNodeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaStateNode still exposes named daemon Verse record helpers instead of the generic typed document handle: " +
            string.Join(", ", survivingDaemonNodeSymbols));
    }

    if (!daemonSurfaceProjector.Contains("public static EveSurfaceState ToState(AetheriaRuntimeSurfaceDocument document)", StringComparison.Ordinal) ||
        !daemonSurfaceProjector.Contains("EveCommandTemplate", StringComparison.Ordinal) ||
        !daemonSurfaceProjector.Contains("EveSurfaceComponent", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Daemon Eve surface projector no longer lowers daemon surfaces into registered Eve surface state.");
    }

    if (!daemonDocuments.Contains("AetheriaRuntimeDaemonCommandKinds", StringComparison.Ordinal))
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
    var daemonQueueSource = daemonDocuments + "\n" + daemonTickRunner + "\n" + tests;
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
        "private const string DaemonRecordTransport = \"cultmesh-record\"",
        "AetheriaRuntimeDaemonSchemas.ProviderAdvertisement",
        "AetheriaRuntimeDaemonSchemas.Frame",
        "AetheriaRuntimeDaemonSchemas.SoaView",
        "AetheriaRuntimeDaemonSchemas.Health",
        "AetheriaRuntimeDaemonSchemas.CommandBoundary",
        "AetheriaRuntimeDaemonSchemas.GameSurface",
        "AetheriaRuntimeDaemonSchemas.EditorSurface",
        "AetheriaRuntimeDaemonSchemas.Command",
        "AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString()",
        "AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString()",
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
            "Odin-visible provider advertisement no longer points at managed daemon CultMesh records: " +
            string.Join(", ", missingProviderAdvertisementSymbols));
    }

    var forbiddenProviderAdvertisementSymbols = new[]
    {
        "DaemonWitnessTransport",
        "AetheriaRuntimeStateBoundary.GetDaemonProviderPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonFramePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonHealthPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonCommandBoundaryPath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonGameSurfacePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonGameTuiSurfacePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonEditorSurfacePath(statePath)",
        "AetheriaRuntimeStateBoundary.GetDaemonEditorTuiSurfacePath(statePath)"
    };
    var forbiddenProviderAdvertisementHits = forbiddenProviderAdvertisementSymbols
        .Where(symbol => providerAdvertisement.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenProviderAdvertisementHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Odin-visible provider advertisement still advertises daemon sidecar paths instead of managed CultMesh records: " +
            string.Join(", ", forbiddenProviderAdvertisementHits));
    }

    var requiredTickSymbols = new[]
    {
        "VerseId",
        "CultMeshAddress",
        "BuildPublications",
        "AetheriaRuntimeDaemonCommandBoundaryDocument.Create",
        "AetheriaRuntimeDaemonSoaFramePublisher.BuildCurrentZoneEntities(stateFilePath, frame)",
        "AetheriaRuntimeDaemonProviderAdvertisementDocument.Create",
        "AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(",
        "StarbridgeScenario",
        "StarbridgeSession",
        "var catalog = options.Catalog ?? new AetheriaRuntimeCatalogSnapshot(",
        "AetheriaRuntimeDaemonGameSurfaceBuilder.Build",
        "AetheriaRuntimeStatRecipeSurfaceBuilder.BuildFromCatalog(catalog)",
        "AetheriaRuntimeTradeValuePolicySurfaceBuilder.BuildFromCatalog(catalog)",
        "AetheriaRuntimeDaemonEditorSurfaceBuilder.Build",
        "SoaView = soaView",
        "ProviderAdvertisement = providerAdvertisement",
        "Health = health",
        "CommandBoundary = commandBoundary",
        "StarbridgeSessionSummary = starbridgeSessionSummary",
        "GameSurface = gameSurface",
        "ObservedCommands",
        "AccountedCommandIds",
        "frame.AccountedCommandIds = accountedBeforeTick",
        "ObservedCommandCount = observedCommands.Length",
        "PublicationSource = \"daemon-published\"",
        "Transport = \"cultmesh-managed\"",
        "CommandBoundaryPath = AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString()"
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

    var forbiddenTickPublicationSymbols = new[]
    {
        "AetheriaRuntimeDaemonFrameStore.PublishFrame(",
        "AetheriaRuntimeDaemonPublicationStore.PublishCommandBoundary(",
        "AetheriaRuntimeDaemonPublicationStore.PublishProviderAdvertisement(",
        "AetheriaRuntimeDaemonPublicationStore.PublishHealth(",
        "AetheriaRuntimeDaemonPublicationStore.PublishAssetManifest(",
        "AetheriaRuntimeDaemonPublicationStore.PublishStarbridgeSessionSummary(",
        "AetheriaRuntimeDaemonPublicationStore.PublishGameSurface(",
        "AetheriaRuntimeDaemonPublicationStore.PublishGameTuiSurface(",
        "AetheriaRuntimeDaemonPublicationStore.PublishEditorSurface(",
        "AetheriaRuntimeDaemonPublicationStore.PublishEditorTuiSurface(",
        "AetheriaRuntimeCatalogStore.ProjectStatRecipeSurfaceDocument(catalog)",
        "AetheriaRuntimeCatalogStore.ProjectTradeValuePolicySurfaceDocument(catalog)",
        "AetheriaRuntimeCatalogStore.ProjectStatRecipeSurfaceDocument(stateFilePath)",
        "AetheriaRuntimeCatalogStore.ProjectTradeValuePolicySurfaceDocument(stateFilePath)"
    };
    var forbiddenTickPublicationHits = forbiddenTickPublicationSymbols
        .Where(symbol => daemonTickRunner.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenTickPublicationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon tick runner must build typed publication documents, not write legacy witness stores: " +
            string.Join(", ", forbiddenTickPublicationHits));
    }

    var requiredDaemonHostSymbols = new[]
    {
        "<OutputType>Exe</OutputType>",
        "ProjectReference Include=\"..\\Aetheria.State\\Aetheria.State.csproj\"",
        "AetheriaStateNode.OpenAsync(",
        "startServer: true",
        "node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)",
        "Catalog = node.RuntimeCatalog().Latest()",
        "node.RuntimeCatalog().Latest());",
        "node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)",
        "node.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest)",
        "node.MutableDocument<AetheriaRuntimeStarbridgeSessionDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest)",
        "StarbridgeScenario = starbridgeScenario",
        "StarbridgeSession = starbridgeSession",
        "BuildPublications = buildPublications",
        "new AetheriaVerseDiscoveryHost(node)",
        "discoveryHost.Update(",
        "PublishDaemonApiDocumentsAsync(node, result)",
        "node.MutableDocument<AetheriaRuntimeDaemonSoaViewDocument>(AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest)",
        "node.MutableDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement)",
        "node.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth)",
        "node.MutableDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary)",
        "node.MutableDocument<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)",
        "node.MutableDocument<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface)",
        "node.MutableDocument<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface)",
        "node.MutableDocument<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface)",
        "node.MutableDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary)",
        ".ReplaceAsync(result.Frame)",
        ".ReplaceAsync(result.SoaView)",
        ".ReplaceAsync(result.Health)",
        ".ReplaceAsync(result.StarbridgeSessionSummary)",
        "InjectEveSurfaceSnapshotAsync(",
        "ReadEveSurfacePublicationAsync(",
        "AetheriaRuntimeVerseRecordKeys.DaemonGameSurface",
        "AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface",
        "AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface",
        "AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface",
        "AetheriaEveCommandBridge.AcceptObservedAsync(",
        "node.Documents<AetheriaRuntimeDaemonCommandDocument>()",
        "currentFrame?.AccountedCommandIds ?? Array.Empty<string>()",
        "AetheriaRuntimeDaemonTickRunner.Tick(",
        "node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync()",
        "node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey)",
        "node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey(options.DaemonId))",
        "node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync()",
        "node.MutableDocument<EveSurfaceState>(AetheriaStateNode.OperationsSurfaceKey)",
        "node.MutableDocument<EveSurfaceState>(AetheriaStateNode.PlayerSettingsSurfaceKey)",
        "node.MutableDocument<EveProviderAdvertisementState>(AetheriaStateNode.ProviderAdvertisementSurfaceKey)",
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

    var forbiddenNamedDaemonHostSymbols = new[]
    {
        "node.LatestFrame()",
        "node.LatestSoaView()",
        "node.ProviderAdvertisement()",
        "node.Health()",
        "node.CommandBoundary()",
        "node.VerseAuthorityPolicy()",
        "node.StarbridgeScenario()",
        "node.StarbridgeSession()",
        "node.StarbridgeSessionSummary()",
        "node.DaemonGameSurface()",
        "node.DaemonGameTuiSurface()",
        "node.DaemonEditorSurface()",
        "node.DaemonEditorTuiSurface()"
    };
    var survivingNamedDaemonHostSymbols = forbiddenNamedDaemonHostSymbols
        .Where(symbol => daemonHostSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingNamedDaemonHostSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria.State.Daemon still uses named daemon Verse record helpers instead of generic typed document handles: " +
            string.Join(", ", survivingNamedDaemonHostSymbols));
    }

    var committedFactImportStart = daemonHostSource.IndexOf(
        "static async Task<AetheriaRuntimeDaemonTickResult> ImportRemoteCommittedFactsAsync",
        StringComparison.Ordinal);
    var committedFactImportEnd = committedFactImportStart >= 0
        ? daemonHostSource.IndexOf("static RudpCultNetSchemaServer StartRtsCultMeshHost", committedFactImportStart, StringComparison.Ordinal)
        : -1;
    var committedFactImportBlock = committedFactImportStart >= 0 && committedFactImportEnd > committedFactImportStart
        ? daemonHostSource.Substring(committedFactImportStart, committedFactImportEnd - committedFactImportStart)
        : "";
    if (!committedFactImportBlock.Contains("node.RuntimeCatalog().Latest())", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Remote committed fact import must pass the managed runtime catalog document into the tick importer instead of reopening catalog storage.");
    }

    if (committedFactImporter.Contains("AetheriaRuntimeCatalogStore.OpenReadOnly", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Committed fact import must use managed catalog input or an explicit empty typed catalog; it must not reopen catalog storage from the state path.");
    }

    var apiPublicationStart = daemonHostSource.IndexOf(
        "static async Task PublishDaemonApiDocumentsAsync",
        StringComparison.Ordinal);
    var apiPublicationEnd = apiPublicationStart >= 0
        ? daemonHostSource.IndexOf("static async Task AcceptEveCommandsAsync", apiPublicationStart, StringComparison.Ordinal)
        : -1;
    var apiPublicationBlock = apiPublicationStart >= 0 && apiPublicationEnd > apiPublicationStart
        ? daemonHostSource.Substring(apiPublicationStart, apiPublicationEnd - apiPublicationStart)
        : "";
    if (apiPublicationBlock.Contains("AetheriaRuntimeDaemonPublicationStore.TryRead", StringComparison.Ordinal) ||
        apiPublicationBlock.Contains("SoaViewStore.TryRead", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon API document publication must use typed tick result documents instead of reopening witness files.");
    }

    if (daemonTickRunner.Contains("AetheriaRuntimeDaemonPublicationStore.TryReadHealth(stateFilePath", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon tick must pass the typed health document through surface builders instead of reopening its witness file.");
    }

    var daemonTickStart = daemonHostSource.IndexOf(
        "static async Task<AetheriaRuntimeDaemonTickResult> TickAsync(",
        StringComparison.Ordinal);
    var daemonTickEnd = daemonTickStart >= 0
        ? daemonHostSource.IndexOf("static async Task PublishCommittedCommandFactsAsync", daemonTickStart, StringComparison.Ordinal)
        : -1;
    var daemonTickBlock = daemonTickStart >= 0 && daemonTickEnd > daemonTickStart
        ? daemonHostSource.Substring(daemonTickStart, daemonTickEnd - daemonTickStart)
        : "";
    if (daemonTickBlock.Contains("AetheriaRuntimeDaemonFrameStore.PublishFrame(", StringComparison.Ordinal) ||
        daemonTickBlock.Contains("AetheriaRuntimeDaemonPublicationStore.Publish", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon host tick publication must publish typed documents through AetheriaStateNode, not legacy witness stores.");
    }

    if (!daemonTickBlock.Contains("if (buildPublications)", StringComparison.Ordinal) ||
        !daemonTickBlock.Contains("PublishDaemonApiDocumentsAsync(node, result)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon ticks must publish API cadence documents through managed AetheriaStateNode pointers.");
    }

    var snapshotHandlerStart = daemonHostSource.IndexOf(
        "server.OnCultNet<CultNetSnapshotRequestMessage>",
        StringComparison.Ordinal);
    var snapshotHandlerEnd = snapshotHandlerStart >= 0
        ? daemonHostSource.IndexOf("var factPuts = node.Documents<AetheriaRuntimeCommittedCommandFactDocument>()", snapshotHandlerStart, StringComparison.Ordinal)
        : -1;
    var snapshotHandler = snapshotHandlerStart >= 0 && snapshotHandlerEnd > snapshotHandlerStart
        ? daemonHostSource.Substring(snapshotHandlerStart, snapshotHandlerEnd - snapshotHandlerStart)
        : "";
    var forbiddenSnapshotStoreReads = new[]
    {
        "AetheriaRuntimeDaemonPublicationStore.TryReadHealth",
        "AetheriaRuntimeDaemonPublicationStore.TryReadStarbridgeSessionSummary"
    };
    var snapshotStoreHits = forbiddenSnapshotStoreReads
        .Where(symbol => snapshotHandler.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (snapshotStoreHits.Length > 0)
    {
        throw new InvalidOperationException(
            "RUDP snapshot responses must inject managed daemon node documents instead of reopening publication-store files: " +
            string.Join(", ", snapshotStoreHits));
    }

    var surfaceSnapshotStart = daemonHostSource.IndexOf(
        "static async Task InjectEveSurfaceSnapshotAsync",
        StringComparison.Ordinal);
    var surfaceSnapshotEnd = surfaceSnapshotStart >= 0
        ? daemonHostSource.IndexOf("static Task<EveSurfaceState?> ReadEveSurfacePublicationAsync", surfaceSnapshotStart, StringComparison.Ordinal)
        : -1;
    var surfaceSnapshotBlock = surfaceSnapshotStart >= 0 && surfaceSnapshotEnd > surfaceSnapshotStart
        ? daemonHostSource.Substring(surfaceSnapshotStart, surfaceSnapshotEnd - surfaceSnapshotStart)
        : "";
    if (surfaceSnapshotBlock.Contains("AetheriaRuntimeDaemonPublicationStore.TryRead", StringComparison.Ordinal) ||
        surfaceSnapshotBlock.Contains("AetheriaRuntimeEveSurfaceStateProjector.ToState(surfaceDocument)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "RUDP Eve surface snapshots still reopen publication-store files instead of using managed Eve surface records.");
    }

    var forbiddenDaemonHostSymbols = new[]
    {
        ".Document<AetheriaRuntimeDaemonFrameDocument>(",
        "node.Document<AetheriaRuntimeVerseAuthorityPolicyDocument>(",
        "node.Document<AetheriaRuntimeStarbridgeScenarioDocument>(",
        "node.Document<AetheriaRuntimeStarbridgeSessionDocument>(",
        "node.Document<EveSurfaceState>(",
        "LatestOrDefaultAsync(",
        "AetheriaRuntimeDaemonFrameStore.TryReadFrame(node.StatePath",
        "Catalog = node.OpenRuntimeCatalog()",
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

    var requiredTradeValuePolicySurfaceSymbols = new[]
    {
        "public static class AetheriaRuntimeTradeValuePolicySurfaceBuilder",
        "public static AetheriaRuntimeSurfaceDocument BuildFromCatalog(",
        "public static AetheriaRuntimeTradeValuePolicySurfaceState ProjectState(",
        "SurfaceId = \"aetheria.tradeValuePolicy\"",
        "AetheriaRuntimeTradeValuePolicySurfaceState",
        "Quality Price Modifier",
        "Rarity Tiers",
        "AetheriaRuntimeTradeValuePolicyCommands.SetQualityMinimum",
        "AetheriaRuntimeTradeValuePolicyCommands.SetTierQuality",
        "BuildCommandTemplates()",
        "control.text",
        "curve.Evaluate(0.25)",
        "ToHex(tier.Red, tier.Green, tier.Blue)"
    };
    var tradeValuePolicySurfaceSource = tradeValuePolicySurfaceBuilder + "\n" + documentStore;
    var missingTradeValuePolicySurfaceSymbols = requiredTradeValuePolicySurfaceSymbols
        .Where(symbol => !tradeValuePolicySurfaceSource.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTradeValuePolicySurfaceSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Trade value policy is no longer exposed as a designer-owned Eve surface: " +
            string.Join(", ", missingTradeValuePolicySurfaceSymbols));
    }

    var requiredStatRecipeSurfaceProjectionSymbols = new[]
    {
        "public static AetheriaRuntimeSurfaceDocument BuildFromCatalog(",
        "public static AetheriaRuntimeStatRecipeSurfaceState ProjectState(",
        "SelectMany(ProjectRows)",
        "AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind)",
        "AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat",
        "new AetheriaRuntimeStatRecipeState(",
        "new AetheriaRuntimeStatInfluenceState("
    };
    var missingStatRecipeSurfaceProjectionSymbols = requiredStatRecipeSurfaceProjectionSymbols
        .Where(symbol => !statRecipeSurfaceBuilder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingStatRecipeSurfaceProjectionSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Stat recipe designer surface projection must live on AetheriaRuntimeStatRecipeSurfaceBuilder: " +
            string.Join(", ", missingStatRecipeSurfaceProjectionSymbols));
    }

    var forbiddenCatalogStoreSurfaceProjectionSymbols = new[]
    {
        "ProjectStatRecipeSurfaceState(",
        "ProjectTradeValuePolicySurfaceState(",
        "ProjectStatRecipeRows(",
        "ProjectStatRecipeRow(",
        "ProjectStatInfluence("
    };
    var catalogStoreSurfaceProjectionHits = forbiddenCatalogStoreSurfaceProjectionSymbols
        .Where(symbol => runtimeCatalogStore.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (catalogStoreSurfaceProjectionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime catalog store still owns designer surface projection helpers that belong on typed surface builders: " +
            string.Join(", ", catalogStoreSurfaceProjectionHits));
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
        "AetheriaRuntimeStarbridgeSessionSummaryDocument? starbridge = null",
        "AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(frame)",
        "AetheriaRuntimeSurfaceStateRefs.SourceRef(stateRef)",
        "AetheriaRuntimeDaemonStateRefs.CurrentEntityName",
        "AetheriaRuntimeDaemonStateRefs.CurrentTargetName",
        "\"game.daemon\"",
        "\"aetheria.daemon.game.starbridge\"",
        "\"Starbridge Session\"",
        "\"Station Stock\"",
        "\"Wave Forecast\"",
        "\"Runtime Roles\"",
        "BuildStarbridgeStationStockCard(starbridge)",
        "BuildStarbridgeWaveForecastCard(starbridge)",
        "BuildStarbridgeRuntimeRolesCard(starbridge)",
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
        "internal static bool TrySubmitArgumentless(",
        "AetheriaRuntimeDaemonCommandKinds.SensorPing => client.SensorPing(frame)"
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
        "\"Managed Records\"",
        "\"aetheria.daemon.editor.records\"",
        "provider.FrameRecordRef",
        "provider.SoaViewRecordRef",
        "provider.HealthRecordRef",
        "provider.CommandBoundaryRecordRef",
        "provider.EveGuiSurfaceRecordRef",
        "provider.EditorGuiSurfaceRecordRef",
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
        "var providerAdvertisement = result.ProviderAdvertisement",
        "var health = result.Health",
        "var commandBoundary = result.CommandBoundary",
        "var gameSurface = result.GameSurface",
        "var editorSurface = result.EditorSurface",
        "var gameTuiSurface = result.GameTuiSurface",
        "var editorTuiSurface = result.EditorTuiSurface",
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
        "node.MutableDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement)",
        "node.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth)",
        "node.MutableDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary)",
        "AetheriaClient.OpenAsync(",
        "daemonCommandClient.Control.SensorPing();",
        "eveCommandClient.Ui.InputSettingsAsync(",
        "AetheriaClient control submission did not appear as a typed daemon state record.",
        "AetheriaClient UI submission did not appear as a typed Eve state record.",
        "node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)",
        "node.MutableDocument<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)",
        ".ReplaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(daemonGameSurface))",
        "reopened.MutableDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement)",
        "reopened.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth)",
        "reopened.MutableDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary)",
        "reopened.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)",
        "reopened.MutableDocument<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)",
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

    var forbiddenSmokeSymbols = new[]
    {
        "PutWorldAsync(",
        "GetWorldAsync(",
        "PutLegacyItemDefinitionAsync(",
        "GetItemDefinitionByLegacyIdAsync(",
        "PutLegacyCorporationAsync(",
        "GetCorporationByLegacyIdAsync(",
        "PutLegacyNameFileAsync(",
        "GetNameFileByLegacyIdAsync(",
        "PutCatalogSurfaceAsync(",
        "GetCatalogSurfaceAsync(",
        "PutOperationsSurfaceAsync(",
        "GetOperationsSurfaceAsync(",
        "PutProviderAdvertisementAsync(",
        "GetProviderAdvertisementAsync(",
        "PutDaemonProviderAdvertisementAsync(",
        "GetDaemonProviderAdvertisementAsync(",
        "PutDaemonHealthAsync(",
        "GetDaemonHealthAsync(",
        "PutDaemonCommandBoundaryAsync(",
        "GetDaemonCommandBoundaryAsync(",
        "PutDaemonFrameAsync(",
        "GetDaemonFrameAsync(",
        "PutDaemonGameSurfaceAsync(",
        "GetDaemonGameSurfaceAsync(",
        "node.ProviderAdvertisement()",
        "node.Health()",
        "node.CommandBoundary()",
        "node.LatestFrame()",
        "node.DaemonGameSurface()",
        "reopened.ProviderAdvertisement()",
        "reopened.Health()",
        "reopened.CommandBoundary()",
        "reopened.LatestFrame()",
        "reopened.DaemonGameSurface()",
        "PutRuntimeSessionAsync(",
        "GetRuntimeSessionAsync(",
        "PutLoadoutTemplateAsync(",
        "GetLoadoutTemplateAsync(",
        "PutEntitySnapshotAsync(",
        "GetEntitySnapshotAsync(",
        "PutZoneStateAsync(",
        "GetZoneStateAsync(",
        "PutRunStateAsync(",
        "GetRunStateAsync(",
        "PutPlayerSettingsAsync(",
        "GetPlayerSettingsAsync(",
        "PutPlayerSettingsSurfaceAsync(",
        "GetPlayerSettingsSurfaceAsync(",
        "PutEveCommandAcceptanceStatusAsync(",
        "GetEveCommandAcceptanceStatusAsync("
    };
    var survivingSmokeSymbols = forbiddenSmokeSymbols
        .Where(symbol => smoke.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingSmokeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "State smoke still teaches compatibility helper access instead of managed typed state pointers: " +
            string.Join(", ", survivingSmokeSymbols));
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
        "`AetheriaRuntimeVerseClient` is the only shared submission boundary for typed command records.",
        "Do not add command ports, cached submitters, mailboxes, or queue-like buses between clients and the typed Verse graph.",
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

static void RequireUnityRuntimeCatalogClientUsesManagedDocument(string root)
{
    var clientPath = Path.Combine(root, "Aetheria.State.Unity", "AetheriaRuntimeCatalogClient.cs");
    var mapperPath = Path.Combine(root, "Aetheria.State.Unity", "AetheriaRuntimeCatalogSnapshotMapper.cs");
    var client = File.Exists(clientPath)
        ? File.ReadAllText(clientPath)
        : throw new InvalidOperationException("Cannot verify Unity runtime catalog client; AetheriaRuntimeCatalogClient.cs is missing.");

    var requiredSymbols = new[]
    {
        "\"aetheria-unity-runtime-catalog\",",
        "enableDurableShardLogs: false",
        "public AetheriaRuntimeCatalogSnapshot ReadCatalog()",
        "return _node.RuntimeCatalog().Latest();",
        "public EveSurfaceState? ReadCatalogSurface()",
        "return _node.CatalogSurface().Latest();"
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !client.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime catalog client must read the managed runtime catalog document: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        "_node.ReadCatalogSnapshot()",
        "_node.GetCatalogSurfaceAsync()",
        "ReadCatalogSurfaceAsync(",
        "AetheriaRuntimeCatalogStore.ReadEveSurfaces",
        "AetheriaRuntimeCatalogStore",
        "ToState(global::GameCult.Eve.Surface.EveSurfaceDocument",
        "AetheriaRuntimeCatalogSnapshotMapper.FromCatalog(",
        "AetheriaCatalogSnapshot"
    };
    var forbiddenHits = forbiddenSymbols
        .Where(symbol => client.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime catalog client still maps authored catalog state instead of using the managed runtime document: " +
            string.Join(", ", forbiddenHits));
    }

    if (File.Exists(mapperPath))
    {
        throw new InvalidOperationException(
            "Unity runtime catalog snapshot mapper is dead projection chaff; AetheriaStateNode.RuntimeCatalog owns this document now.");
    }

    Console.WriteLine("Unity runtime catalog client: catalog reads use the managed typed runtime document");
}

static void RequireCatalogSurfaceUsesManagedRuntimeCatalog(string root)
{
    var projectorPath = Path.Combine(root, "Aetheria.State", "AetheriaCatalogSurfaceProjector.cs");
    var bridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var importPath = Path.Combine(root, "Aetheria.State.Import", "Program.cs");
    var legacySnapshotPath = Path.Combine(root, "Aetheria.State", "AetheriaCatalogSnapshot.cs");
    var projector = File.Exists(projectorPath)
        ? File.ReadAllText(projectorPath)
        : throw new InvalidOperationException("Cannot verify catalog surface projector; AetheriaCatalogSurfaceProjector.cs is missing.");
    var bridge = File.Exists(bridgePath)
        ? File.ReadAllText(bridgePath)
        : throw new InvalidOperationException("Cannot verify Eve command bridge catalog refresh; AetheriaEveCommandBridge.cs is missing.");
    var import = File.Exists(importPath)
        ? File.ReadAllText(importPath)
        : throw new InvalidOperationException("Cannot verify legacy catalog import; Aetheria.State.Import/Program.cs is missing.");

    var requiredProjectorSymbols = new[]
    {
        "Build(AetheriaRuntimeCatalogSnapshot catalog",
        "item.ItemKey",
        "corporation.CorporationKey",
        "catalog.GetManufacturer(item)",
        "catalog.GetNameFile(corporation)"
    };
    var missingProjectorSymbols = requiredProjectorSymbols
        .Where(symbol => !projector.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingProjectorSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Catalog Eve surface must project from the managed runtime catalog document: " +
            string.Join(", ", missingProjectorSymbols));
    }

    if (projector.Contains("Build(AetheriaCatalogSnapshot catalog", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Catalog Eve surface still depends on the legacy authored catalog snapshot instead of the managed runtime catalog document.");
    }

    if (File.Exists(legacySnapshotPath))
    {
        throw new InvalidOperationException(
            "AetheriaCatalogSnapshot is dead projection chaff; AetheriaRuntimeCatalogSnapshot is the managed typed catalog document.");
    }

    var requiredBridgeSymbols = new[]
    {
        "var catalog = await node.RuntimeCatalog().LatestAsync().ConfigureAwait(false);",
        "AetheriaCatalogSurfaceProjector.Build(catalog, command.IssuedAtUtc)"
    };
    var missingBridgeSymbols = requiredBridgeSymbols
        .Where(symbol => !bridge.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingBridgeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Eve catalog refresh must read the managed runtime catalog document: " +
            string.Join(", ", missingBridgeSymbols));
    }

    if (bridge.Contains("ReadCatalogSnapshot()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve catalog refresh still rebuilds the legacy catalog snapshot instead of using AetheriaStateNode.RuntimeCatalog().");
    }

    var requiredImportSymbols = new[]
    {
        "node.MutableDocument<AetheriaLegacyCatalogQuarantine>(AetheriaStateNode.LegacyCatalogQuarantineKey)",
        "node.MutableDocument<AetheriaMigrationLedger>(AetheriaStateNode.MigrationLedgerKey)",
        "node.MutableDocument<AetheriaItemDefinition>(AetheriaCatalogKeys.ItemDefinitionFromLegacyId(item.LegacyId))",
        "node.MutableDocument<AetheriaCorporation>(AetheriaCatalogKeys.CorporationFromLegacyId(corporation.LegacyId))",
        "node.MutableDocument<AetheriaNameFile>(AetheriaCatalogKeys.NameFileFromLegacyId(nameFile.LegacyId))",
        "node.MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey)",
        "AetheriaRuntimeStateMapper.ToTradeValuePolicy(",
        "await node.CatalogSurface().LatestAsync().ConfigureAwait(false);"
    };
    var missingImportSymbols = requiredImportSymbols
        .Where(symbol => !import.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingImportSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Legacy catalog import must write through managed typed document handles and materialize the managed catalog surface: " +
            string.Join(", ", missingImportSymbols));
    }

    var forbiddenImportSymbols = new[]
    {
        "PutLegacyCatalogQuarantineAsync(",
        "PutMigrationLedgerAsync(",
        "PutLegacyItemDefinitionAsync(",
        "PutLegacyCorporationAsync(",
        "PutLegacyNameFileAsync(",
        "PutTradeValuePolicyAsync(",
        "PutCatalogSurfaceAsync("
    };
    var forbiddenImportHits = forbiddenImportSymbols
        .Where(symbol => import.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (forbiddenImportHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Legacy catalog import still uses AetheriaStateNode Put helpers instead of managed typed documents: " +
            string.Join(", ", forbiddenImportHits));
    }

    Console.WriteLine("Catalog Eve surface: refresh path uses the managed runtime catalog document");
}

static void RequireAetheriaRuntimeVerseClientContract(string root)
{
    var clientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseClient.cs");
    var clientStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaClientState.cs");
    var surfaceStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveSurfaceState.cs");
    var oldSurfaceStatePath = Path.Combine(root, "Aetheria.State", "Documents", "EveSurfaceState.cs");
    var stateRefResolverPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateRefResolver.cs");
    var docPath = Path.Combine(root, "docs", "aetheria-verse-client-contract.md");

    var requiredFiles = new[] { clientPath, clientStatePath, surfaceStatePath, stateRefResolverPath, docPath };
    var missingFiles = requiredFiles
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    if (missingFiles.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria runtime Verse client contract cannot be verified because required files are missing: " +
            string.Join(", ", missingFiles));
    }

    var client = File.ReadAllText(clientPath);
    var clientState = File.ReadAllText(clientStatePath);
    var surfaceState = File.ReadAllText(surfaceStatePath);
    var stateRefResolver = File.ReadAllText(stateRefResolverPath);
    var doc = File.ReadAllText(docPath);

    if (File.Exists(oldSurfaceStatePath))
    {
        throw new InvalidOperationException(
            "EveSurfaceState must live in the shared runtime package so every CultMesh client can watch daemon UI surfaces.");
    }

    var requiredClientSymbols = new[]
    {
        "public sealed class AetheriaRuntimeVerseClient",
        "public static class AetheriaRuntimeVerseRecordKeys",
        "public static class AetheriaRuntimeVerseContractRegistry",
        "AetheriaRuntimeVerseContractRegistry.CreateCultCacheRegistry()",
        "AetheriaRuntimeVerseContractRegistry.CreateCultNetRegistry(registry)",
        "public Observable<CultNetDatabaseChange<TDocument>> WatchRecord<TDocument>(",
        "WatchRecord<AetheriaRuntimeDaemonFrameDocument>",
        "public CultMeshMutableStatePointer<TDocument> MutableDocument<TDocument>(",
        "CultMesh.MutableStatePointer(",
        "CultMesh.CreateCultCacheDocumentRegistry(RuntimeDocumentTypes)",
        "CultMesh.CreateCultNetDocumentRegistry(RuntimeDocumentTypes, registry)",
        "typeof(AetheriaRuntimeVerseAuthorityPolicyDocument)",
        "typeof(AetheriaRuntimeCatalogSnapshot)",
        "typeof(AetheriaRuntimeLoadoutTemplatesDocument)",
        "AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy",
        "public static CultRecordKey StarbridgeSessionSummary",
        "new CultRecordKey(\"daemon:aetheria.starbridge.session.latest.v1\")",
        "Document<AetheriaRuntimeVerseAuthorityPolicyDocument>(",
        "typeof(EveSurfaceState)",
        "private AetheriaClientState? _aetheriaState",
        "private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? _managedDaemonFrame",
        "private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>? _managedCatalog",
        "private CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument>? _managedLoadoutTemplates",
        "private CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument>? _managedStarbridgeScenario",
        "private CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument>? _managedStarbridgeSession",
        "_managedDaemonFrame?.Dispose()",
        "_managedCatalog?.Dispose()",
        "_managedLoadoutTemplates?.Dispose()",
        "_managedStarbridgeScenario?.Dispose()",
        "_managedStarbridgeSession?.Dispose()",
        "return _aetheriaState ??= CreateAetheriaState();",
        "AetheriaRuntimeLoadoutTemplatesDocument",
        "ProjectStarbridgeSummaryAsync",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? managedDaemonFrame = null;",
        "managedDaemonFrame = state.Document<AetheriaRuntimeDaemonFrameDocument>().Reactive();",
        "managedCatalog = state.Document<AetheriaRuntimeCatalogSnapshot>().Reactive();",
        "managedLoadoutTemplates = state.Document<AetheriaRuntimeLoadoutTemplatesDocument>().Reactive();",
        "managedStarbridgeScenario = state.Document<AetheriaRuntimeStarbridgeScenarioDocument>().Reactive();",
        "managedStarbridgeSession = state.Document<AetheriaRuntimeStarbridgeSessionDocument>().Reactive();",
        "AetheriaRuntimeDaemonFrameDocument RequireManagedFrame()",
        "RequireManagedFrame()",
        "RequireManagedCatalog()",
        "RequireManagedLoadoutTemplates().Templates",
        "managedStarbridgeScenario?.Current",
        "managedStarbridgeSession?.Current",
        "BootstrapCatalogDocument(",
        "CatalogBootstrapSource(",
        "BootstrapRuntimeCatalogSnapshot()",
        "BootstrapLoadoutTemplatesDocument()",
        "BootstrapPlayerSettingsDocument()",
        "BootstrapVerseHostSettingsDocument()",
        "SubmitDaemonCommandAsync(",
        "SubmitEveCommandAsync(",
        "AetheriaRuntimeVerseRecordKeys.DaemonCommand(command.CommandId)",
        "AetheriaRuntimeVerseRecordKeys.EveCommand(command.CommandId)",
        "DaemonGameTuiSurface { get; }",
        "DaemonEditorTuiSurface { get; }"
    };
    var missingClientSymbols = requiredClientSymbols
        .Where(symbol => !client.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria runtime Verse client is missing typed CultMesh client contract symbols: " +
            string.Join(", ", missingClientSymbols));
    }

    var forbiddenNamedWatchHelpers = new[]
    {
        "WatchProviderAdvertisements(",
        "WatchHealth(",
        "WatchCommandBoundary(",
        "WatchVerseAuthorityPolicies(",
        "WatchLatestFrames(",
        "WatchLatestSoaViews(",
        "WatchStarbridgeScenarios(",
        "WatchStarbridgeSessions(",
        "WatchStarbridgePlayerSeat(",
        "WatchDaemonGameSurfaces(",
        "WatchDaemonGameTuiSurfaces(",
        "WatchDaemonEditorSurfaces(",
        "WatchDaemonEditorTuiSurfaces("
    };
    var survivingNamedWatchHelpers = forbiddenNamedWatchHelpers
        .Where(symbol => client.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingNamedWatchHelpers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient must expose generic WatchRecord<TDocument>(CultRecordKey) instead of named record-watch wrappers: " +
            string.Join(", ", survivingNamedWatchHelpers));
    }

    var forbiddenNamedMutablePointers = new[]
    {
        "ProviderAdvertisement(",
        "Health(",
        "CommandBoundary(",
        "VerseAuthorityPolicy(",
        "LatestFrame(",
        "LatestSoaView(",
        "StarbridgeScenario(",
        "StarbridgeSession(",
        "StarbridgePlayerSeat(",
        "DaemonGameSurface(",
        "DaemonGameTuiSurface(",
        "DaemonEditorSurface(",
        "DaemonEditorTuiSurface("
    };
    var runtimeClientInstanceApi = client.Split(
        "public AetheriaClientState Aetheria()",
        StringSplitOptions.None).Last().Split(
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(",
        StringSplitOptions.None)[0];
    var survivingNamedMutablePointers = forbiddenNamedMutablePointers
        .Where(symbol => runtimeClientInstanceApi.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingNamedMutablePointers.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient must expose generic MutableDocument<TDocument>(CultRecordKey) instead of named mutable pointer wrappers: " +
            string.Join(", ", survivingNamedMutablePointers));
    }

    if (client.Contains("ReactiveDocumentAsync<TDocument>", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient must not expose a second reactive-document alias; callers should use Document<TDocument>(key).ReactiveAsync().");
    }

    if (File.Exists(Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeManagedClientInputs.cs")) ||
        client.Contains("AetheriaRuntimeManagedClientInputs", StringComparison.Ordinal) ||
        client.Contains("AetheriaRuntimeDaemonFrameSession _daemonFrame", StringComparison.Ordinal) ||
        client.Contains("AetheriaRuntimeCatalogSession _catalog", StringComparison.Ordinal) ||
        client.Contains("AetheriaRuntimeLoadoutTemplatesSession _loadoutTemplates", StringComparison.Ordinal) ||
        client.Contains("AetheriaRuntimeStarbridgeScenarioSession _starbridgeScenario", StringComparison.Ordinal) ||
        client.Contains("AetheriaRuntimeStarbridgeRunSession _starbridgeSession", StringComparison.Ordinal) ||
        client.Contains("state.ObserveDaemonFrame()", StringComparison.Ordinal) ||
        client.Contains("state.ObserveCatalog()", StringComparison.Ordinal) ||
        client.Contains("state.ObserveLoadoutTemplates()", StringComparison.Ordinal) ||
        client.Contains("state.Starbridge.ObserveScenario()", StringComparison.Ordinal) ||
        client.Contains("state.Starbridge.ObserveSession()", StringComparison.Ordinal) ||
        client.Contains("catalogDocument.Reactive()", StringComparison.Ordinal) ||
        client.Contains("loadoutTemplatesDocument.Reactive()", StringComparison.Ordinal) ||
        client.Contains("starbridgeScenarioDocument.Reactive()", StringComparison.Ordinal) ||
        client.Contains("starbridgeSessionDocument.Reactive()", StringComparison.Ordinal) ||
        client.Contains("latestFrameDocument.Reactive()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Verse client derived documents must sample directly owned generic reactive typed documents instead of a named input/session wrapper.");
    }

    if (client.Contains("AetheriaRuntimeReactiveProjectionInputs", StringComparison.Ordinal) ||
        client.Contains("private sealed class AetheriaRuntimeManagedClientInputs", StringComparison.Ordinal) ||
        client.Contains("_projectionInputs", StringComparison.Ordinal) ||
        client.Contains("ProjectionInputs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient reintroduced a projection-input helper instead of owning generic reactive typed documents directly.");
    }

    if (client.Contains("ProjectStationRefitAsync", StringComparison.Ordinal) &&
        client.Contains("loadoutTemplatesDocument.LatestAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime Verse client station-refit projection still polls loadout templates instead of sampling managed reactive client inputs.");
    }

    if (client.Contains("ProjectStarbridgeSummaryAsync", StringComparison.Ordinal) &&
        (client.Contains("starbridgeScenarioDocument.LatestAsync()", StringComparison.Ordinal) ||
            client.Contains("starbridgeSessionDocument.LatestAsync()", StringComparison.Ordinal) ||
            client.Contains("catalogDocument.LatestAsync()", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            "Aetheria runtime Verse client Starbridge projection still polls sibling documents instead of sampling managed reactive client inputs.");
    }

    if (client.Contains("RequireFrameAsync", StringComparison.Ordinal) ||
        client.Contains("Aetheria().Daemon.LatestFrame.LatestAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime Verse client projected documents still bootstrap through one-shot latest-frame reads instead of the managed reactive client inputs.");
    }

    if (client.Contains("_fallbackCatalog", StringComparison.Ordinal) ||
        client.Contains("_fallbackLoadoutTemplates", StringComparison.Ordinal) ||
        client.Contains("fallbackCatalog", StringComparison.Ordinal) ||
        client.Contains("fallbackLoadoutTemplates", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime Verse client projections must use managed reactive catalog/loadout documents directly instead of carrying manual fallback snapshots.");
    }

    var requiredClientStateSymbols = new[]
    {
        "public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()",
        "public sealed class AetheriaClientState : IDisposable",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? _eveStateRefFrame",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonHealthDocument>? _eveStateRefHealth",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>? _eveStateRefCommandBoundary",
        "CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>? _eveStateRefCatalog",
        "_eveStateRefFrame ??= Document<AetheriaRuntimeDaemonFrameDocument>().Reactive()",
        "_eveStateRefHealth ??= Document<AetheriaRuntimeDaemonHealthDocument>().Reactive()",
        "_eveStateRefCommandBoundary ??= Document<AetheriaRuntimeDaemonCommandBoundaryDocument>().Reactive()",
        "_eveStateRefCatalog ??= Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "() => _eveStateRefFrame.Current",
        "() => _eveStateRefHealth.Current",
        "() => _eveStateRefCommandBoundary.Current",
        "() => _eveStateRefCatalog.Current",
        "public void Dispose()",
        "_eveStateRefFrame?.Dispose()",
        "_eveStateRefHealth?.Dispose()",
        "_eveStateRefCommandBoundary?.Dispose()",
        "_eveStateRefCatalog?.Dispose()",
        "AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver("
    };
    var missingClientStateSymbols = requiredClientStateSymbols
        .Where(symbol => !clientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientStateSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must own Eve state-ref resolver creation through managed typed documents: " +
            string.Join(", ", missingClientStateSymbols));
    }

    if (clientState.Contains("var frameTask = Daemon.LatestFrameDocumentAsync()", StringComparison.Ordinal) ||
        clientState.Contains("var healthTask = Daemon.LatestHealthAsync()", StringComparison.Ordinal) ||
        clientState.Contains("var commandBoundaryTask = Daemon.LatestCommandBoundaryAsync()", StringComparison.Ordinal) ||
        clientState.Contains("LatestAsync<AetheriaRuntimeDaemonFrameDocument>()", StringComparison.Ordinal) ||
        clientState.Contains("LatestAsync<AetheriaRuntimeDaemonHealthDocument>()", StringComparison.Ordinal) ||
        clientState.Contains("LatestAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>()", StringComparison.Ordinal) ||
        clientState.Contains("Latest<AetheriaRuntimeCatalogSnapshot>()", StringComparison.Ordinal) ||
        clientState.Contains("LatestCatalog)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState Eve state-ref resolver must own reactive typed documents instead of bootstrapping one-shot latest snapshots.");
    }

    if (!client.Contains("_aetheriaState?.Dispose()", StringComparison.Ordinal) ||
        !client.Contains("_aetheriaState = null", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient must dispose AetheriaClientState so cached reactive state-ref resolver documents do not leak.");
    }

    var requiredStateRefResolverSymbols = new[]
    {
        "public static class AetheriaRuntimeStateRefResolver",
        "TryResolveDaemonStateRef(",
        "TryResolveDaemonItemStatRef("
    };
    var missingStateRefResolverSymbols = requiredStateRefResolverSymbols
        .Where(symbol => !stateRefResolver.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingStateRefResolverSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria runtime state-ref resolver is missing typed Eve state-ref symbols: " +
            string.Join(", ", missingStateRefResolverSymbols));
    }

    if (stateRefResolver.Contains("public static class AetheriaRuntimeStateReader", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeStateReader compatibility wrapper still exists; use AetheriaRuntimeStateRefResolver and managed AetheriaClient documents.");
    }

    if (client.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceCultMeshStateRefResolver", StringComparison.Ordinal) ||
        client.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceStateRefResolver", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient still routes Eve state-ref resolution through the file-backed compatibility reader.");
    }

    if (client.Contains("public Func<string, string> CreateEveSurfaceStateRefResolver()", StringComparison.Ordinal) ||
        client.Contains("public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient still exposes Eve state-ref resolver compatibility factories; use AetheriaClientState managed documents.");
    }

    if (client.Contains("GetObservedDaemonStateAsync()", StringComparison.Ordinal) ||
        client.Contains("GetLatestAuthoritativeRunFrameAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient still exposes daemon observation compatibility helpers; use AetheriaClient managed documents instead.");
    }

    if (stateRefResolver.Contains("TryReadDaemonRenderView", StringComparison.Ordinal) ||
        stateRefResolver.Contains("TryReadDaemonSoaView(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("ResolveEveSurfaceStateRef(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("TryResolveEveSurfaceStateRef(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("CreateEveSurfaceCultMeshStateRefResolver(\r\n            string stateFilePath", StringComparison.Ordinal) ||
        stateRefResolver.Contains("CreateEveSurfaceCultMeshStateRefResolver(\n            string stateFilePath", StringComparison.Ordinal) ||
        stateRefResolver.Contains("ReadEveSurface(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("TryReadDaemonGameSurface(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("TryReadDaemonGameTuiSurface(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("TryReadDaemonEditorSurface(", StringComparison.Ordinal) ||
        stateRefResolver.Contains("TryReadDaemonEditorTuiSurface(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria runtime state-ref resolver still exposes daemon acquisition that belongs on managed AetheriaClient documents.");
    }

    var starbridgeSummaryStart = client.IndexOf(
        "Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> ProjectStarbridgeSummaryAsync",
        StringComparison.Ordinal);
    if (starbridgeSummaryStart < 0)
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient no longer exposes the managed Starbridge summary projection.");
    }

    var indexedDocumentStart = client.IndexOf("static string IndexedDocumentId", starbridgeSummaryStart, StringComparison.Ordinal);
    var starbridgeSummaryBlock = indexedDocumentStart > starbridgeSummaryStart
        ? client.Substring(starbridgeSummaryStart, indexedDocumentStart - starbridgeSummaryStart)
        : client.Substring(starbridgeSummaryStart);
    if (!starbridgeSummaryBlock.Contains("RequireManagedCatalog()", StringComparison.Ordinal) ||
        starbridgeSummaryBlock.Contains("catalogDocument.LatestAsync()", StringComparison.Ordinal) ||
        starbridgeSummaryBlock.Contains("BootstrapRuntimeCatalogSnapshot()", StringComparison.Ordinal) ||
        starbridgeSummaryBlock.Contains("AetheriaRuntimeCatalogStore.OpenReadOnly", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Starbridge summary projection must use the managed runtime catalog document instead of reopening the catalog store.");
    }

    var requiredClientBootstrapSymbols = new[]
    {
        "BootstrapCatalogDocument(",
        "CatalogBootstrapSource(",
        "managed Aetheria document bootstrap seed",
        "BootstrapRuntimeCatalogSnapshot()",
        "BootstrapLoadoutTemplatesDocument()",
        "BootstrapPlayerSettingsDocument()",
        "BootstrapVerseHostSettingsDocument()",
        "return AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath);",
        "AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(StatePath)",
        "AetheriaRuntimeCatalogStore.ReadPlayerSettings(StatePath)",
        "AetheriaRuntimeCatalogStore.ReadVerseHostSettings(StatePath)"
    };
    var missingClientBootstrapSymbols = requiredClientBootstrapSymbols
        .Where(symbol => !client.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingClientBootstrapSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient must name catalog/settings file reads as bootstrap seeds for managed documents: " +
            string.Join(", ", missingClientBootstrapSymbols));
    }

    var forbiddenClientCompatibilityStoreBypasses = new[]
    {
        "return Task.FromResult(AetheriaRuntimeCatalogStore.ReadPlayerSettings(StatePath));",
        "return Task.FromResult(AetheriaRuntimeCatalogStore.ReadVerseHostSettings(StatePath));",
        "public static class AetheriaRuntimeBootstrapDocuments",
        "ReadRuntimeCatalogSnapshot()",
        "ReadLoadoutTemplatesDocument()",
        "ReadPlayerSettingsDocument()",
        "ReadVerseHostSettingsDocument()",
        "var catalogDocument = CatalogDocument(",
        "var loadoutTemplatesDocument = CatalogDocument(",
        "CultMeshDocumentHandle<TDocument> CatalogDocument<TDocument>(",
        "projected Aetheria catalog document",
        "Aetheria typed catalog state",
        "public async Task<AetheriaRuntimeDaemonProviderAdvertisementDocument?> GetProviderAdvertisementAsync()",
        "public async Task<AetheriaRuntimeDaemonHealthDocument?> GetHealthAsync()",
        "public async Task<AetheriaRuntimeDaemonCommandBoundaryDocument?> GetCommandBoundaryAsync()",
        "public async Task<AetheriaRuntimeVerseAuthorityPolicyDocument?> GetVerseAuthorityPolicyAsync()",
        "public async Task<AetheriaRuntimeDaemonFrameDocument?> GetLatestFrameAsync()",
        "public async Task<AetheriaRuntimeDaemonSoaViewDocument?> GetLatestSoaViewAsync()",
        "public async Task<AetheriaRuntimeStarbridgeScenarioDocument?> GetStarbridgeScenarioAsync()",
        "public async Task<AetheriaRuntimeStarbridgeSessionDocument?> GetStarbridgeSessionAsync()",
        "public async Task<AetheriaRuntimeStarbridgePlayerSeatDocument?> GetStarbridgePlayerSeatAsync(string seatId)",
        "public async Task PutStarbridgeScenarioAsync(",
        "public async Task PutStarbridgeSessionAsync(",
        "public async Task PutStarbridgePlayerSeatAsync(",
        "public async Task<EveSurfaceState?> GetDaemonGameSurfaceAsync()",
        "public async Task<EveSurfaceState?> GetDaemonGameTuiSurfaceAsync()",
        "public async Task<EveSurfaceState?> GetDaemonEditorSurfaceAsync()",
        "public async Task<EveSurfaceState?> GetDaemonEditorTuiSurfaceAsync()",
        "public AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog()",
        "public async Task<AetheriaRuntimePlayerSettingsSnapshot?> GetPlayerSettingsAsync()",
        "public async Task<AetheriaRuntimeVerseHostSettingsSnapshot?> GetVerseHostSettingsAsync()",
        "() => Task.FromResult(OpenRuntimeCatalog())",
        "OpenRuntimeCatalog());",
        "var frame = await GetLatestFrameAsync().ConfigureAwait(false);",
        "var soaView = await GetLatestSoaViewAsync().ConfigureAwait(false);",
        "var frameTask = GetLatestFrameAsync();",
        "var healthTask = GetHealthAsync();",
        "var commandBoundaryTask = GetCommandBoundaryAsync();",
        "return Database.GetAsync<AetheriaRuntimeDaemonProviderAdvertisementDocument>",
        "return Database.GetAsync<AetheriaRuntimeDaemonHealthDocument>",
        "return Database.GetAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>",
        "return Database.GetAsync<AetheriaRuntimeVerseAuthorityPolicyDocument>",
        "return Database.GetAsync<AetheriaRuntimeDaemonFrameDocument>",
        "return Database.GetAsync<AetheriaRuntimeDaemonSoaViewDocument>",
        "return Database.GetAsync<AetheriaRuntimeStarbridgeScenarioDocument>",
        "return Database.GetAsync<AetheriaRuntimeStarbridgeSessionDocument>",
        "return Database.GetAsync<AetheriaRuntimeStarbridgePlayerSeatDocument>",
        "return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);",
        "return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);",
        "return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);",
        "return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);"
    };
    var clientCompatibilityStoreBypassHits = forbiddenClientCompatibilityStoreBypasses
        .Where(symbol => client.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (clientCompatibilityStoreBypassHits.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient compatibility reads still bypass managed typed documents: " +
            string.Join(", ", clientCompatibilityStoreBypassHits));
    }

    if (client.Contains("public async Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(", StringComparison.Ordinal) ||
        client.Contains("public async Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient exposes raw command-envelope submission publicly; clients should use AetheriaClient.Control and AetheriaClient.Ui.");
    }

    if (client.Contains("AetheriaRuntimeVerseDocument", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeVerseClient reintroduced a private document handle; use shared CultMeshMutableStatePointer<T> instead.");
    }

    var requiredSurfaceSymbols = new[]
    {
        "namespace Aetheria.State.Documents",
        "[CultDocument(\"gamecult.eve.surface\", \"gamecult.eve.surface.v1\")]",
        "public sealed class EveSurfaceState",
        "public sealed class EveSurface",
        "public sealed class EveSurfaceComponent",
        "public sealed class EveSurfaceStateBinding",
        "public EveSurfaceStateBinding[] StateBindings",
        "public sealed class EveCommandTemplate"
    };
    var missingSurfaceSymbols = requiredSurfaceSymbols
        .Where(symbol => !surfaceState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSurfaceSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime Eve surface document contract is incomplete: " +
            string.Join(", ", missingSurfaceSymbols));
    }

    var deletedCommandPortPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCommandPort.cs");
    if (File.Exists(deletedCommandPortPath))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeCommandPort.cs has returned. Submit typed commands through AetheriaRuntimeVerseClient.");
    }

    var requiredDocSymbols = new[]
    {
        "AetheriaRuntimeVerseClient",
        "WatchRecord<T>(CultRecordKey)",
        "MutableDocument<T>(CultRecordKey)",
        "Unity",
        "Verse records",
        "EveSurfaceState",
        "same typed records"
    };
    var missingDocSymbols = requiredDocSymbols
        .Where(symbol => !doc.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDocSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Aetheria Verse client contract note is missing required architecture terms: " +
            string.Join(", ", missingDocSymbols));
    }
}

static void RequireTypedEveCommandBodies(string root)
{
    var eveCommandDocumentPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveCommandDocument.cs");
    var eveCommandClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveCommandClient.cs");
    var verseClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseClient.cs");
    var aetheriaClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaClient.cs");
    var aetheriaUiPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaUi.cs");
    var runtimeCommandPortPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCommandPort.cs");
    var eveCommandBridgePath = Path.Combine(root, "Aetheria.State", "AetheriaEveCommandBridge.cs");
    var stateNodePath = Path.Combine(root, "Aetheria.State", "AetheriaStateNode.cs");
    var documentRegistryPath = Path.Combine(root, "Aetheria.State", "AetheriaDocumentRegistry.cs");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var evePresenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");

    var requiredFiles = new[] { eveCommandDocumentPath, eveCommandClientPath, verseClientPath, aetheriaClientPath, aetheriaUiPath, eveCommandBridgePath, stateNodePath, documentRegistryPath, actionGameManagerPath, mainMenuPath, evePresenterPath };
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
    var verseClient = File.ReadAllText(verseClientPath);
    var aetheriaClient = File.ReadAllText(aetheriaClientPath);
    var aetheriaUi = File.ReadAllText(aetheriaUiPath);
    var normalizedEveCommandClient = eveCommandClient.Replace("\r\n", "\n", StringComparison.Ordinal);
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
        "public sealed class AetheriaRuntimeTradeValuePolicyCommandBody",
        "public AetheriaRuntimeEveCommandKind Kind",
        "public AetheriaRuntimePlayerSettingsCommandBody PlayerSettings",
        "public AetheriaRuntimeInputSettingsCommandBody InputSettings",
        "public AetheriaRuntimeLoadoutTemplateCommit? LoadoutTemplate",
        "public AetheriaRuntimeTradeValuePolicyCommandBody TradeValuePolicy",
        "public CultMeshOperationInvocationDescriptor Invocation",
        "public CultMeshOperationPayload Payload",
        "public CultMeshOperationInvocationRecord Operation",
        "public string OperationId",
        "public string OperationSchemaId",
        "public string OperationRouteKind",
        "public string OperationRouteDescription",
        "public string OperationIdempotencyKey",
        "public Dictionary<string, string> Payload",
        "SetTradeValueQualityMinimum",
        "SetTradeValueTierQuality",
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
        "internal static class AetheriaRuntimeEveCommands",
        "public static class AetheriaRuntimeEveCommandClient",
        "namespace GameCult.Aetheria.State.Verse",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(",
        "internal async Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(",
        "internal async Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(",
        "CreatePlayerSettingsCommand(",
        "CreateInputSettingsCommand(",
        "CreateCatalogCommand(",
        "CreateOperationsCommand(",
        "CreateVerseHostCommand(",
        "CreateLoadoutTemplateCommand(",
        "CreateTradeValuePolicyCommand(",
        "ReadTradeValuePolicyBody(",
        "var command = OperationIdFor(request);",
        "return CommandKindForSurface(request.SurfaceId ?? \"\", OperationIdFor(request));",
        "private static string OperationIdFor(EveSurfaceCommandRequest request)",
        "return request?.Operation?.OperationId ?? \"\";",
        "CultMeshOperationInvocationDescriptor",
        "CultMeshOperationPayload",
        "invocation: request.Operation",
        "payload: request.Payload",
        "CreateInvocation(",
        "CultMesh.OperationInvocationRecord(",
        "Operation = invocation",
        "Operation = invocationRecord",
        "NormalizeInvocationRecord(",
        "ApplyInvocationCompatibilityFields(",
        "Payload = envelope.Payload.ToDictionary()",
        "Payload = (payload ?? CultMeshOperationPayload.Empty).ToDictionary()",
        "return request.Payload.GetString(key);",
        "return request.Payload.GetInt32(key, defaultValue);",
        "return request.Payload.GetDouble(key, defaultValue);",
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
        "node.Documents<AetheriaRuntimeEveCommandDocument>()",
        "AetheriaRuntimeEveCommandClient.NormalizeDocument",
        "AccountedCommandIds",
        "SubmitEveCommandAsync(",
        "EveCommandKey(",
        "typeof(AetheriaRuntimeEveCommandDocument)",
        "SubmitPlayerSettingsCommand(",
        "command.PlayerSettings.PlayerName",
        "command.InputSettings.ActionName",
        "command.InputSettings.InputSystemPath",
        "command.TradeValuePolicy.Value",
        "command.TradeValuePolicy.TierIndex",
        "ExecuteTradeValuePolicyCommandAsync(",
        "MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey)",
        "SubmitInputSettingsCommandAsync(",
        "SubmitLoadoutTemplateCommandAsync(",
        "SubmitKnownSurfaceCommandAsync(",
        "public AetheriaUi Ui => _ui",
        "public sealed class AetheriaUi",
        "Task<CultMeshOperationReceipt> InputSettingsAsync(",
        "Task<CultMeshOperationReceipt> SaveLoadoutTemplateAsync(",
        "Task<CultMeshOperationReceipt> SurfaceCommandAsync("
    };
    var mainMenu = File.ReadAllText(mainMenuPath);
    var typedCommandSources = eveCommandClient + "\n" + verseClient + "\n" + aetheriaClient + "\n" + aetheriaUi + "\n" + eveCommandBridge + "\n" + stateNode + "\n" + documentRegistry + "\n" + actionGameManager + "\n" + mainMenu + "\n" + evePresenter;
    if (aetheriaClient.Contains("public Task<AetheriaRuntimeEveCommandEnvelope> SubmitInputSettingsCommandAsync(", StringComparison.Ordinal) ||
        aetheriaClient.Contains("public Task<AetheriaRuntimeEveCommandEnvelope> SubmitLoadoutTemplateCommandAsync(", StringComparison.Ordinal) ||
        aetheriaClient.Contains("public Task<AetheriaRuntimeEveCommandEnvelope> SubmitKnownSurfaceCommandAsync(", StringComparison.Ordinal) ||
        verseClient.Contains("public Task<AetheriaRuntimeEveCommandEnvelope> SubmitInputSettingsCommandAsync(", StringComparison.Ordinal) ||
        verseClient.Contains("public Task<AetheriaRuntimeEveCommandEnvelope> SubmitLoadoutTemplateCommandAsync(", StringComparison.Ordinal) ||
        verseClient.Contains("public Task<AetheriaRuntimeEveCommandEnvelope> SubmitKnownSurfaceCommandAsync(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClient exposes Eve command envelopes publicly; UI callers should use AetheriaClient.Ui and receive CultMeshOperationReceipt.");
    }

    if (eveCommandClient.Contains("ParseRouteKind(", StringComparison.Ordinal) ||
        eveCommandClient.Contains("OperationId = envelope.Invocation.OperationId", StringComparison.Ordinal) ||
        eveCommandClient.Contains("OperationId = invocation.OperationId", StringComparison.Ordinal) ||
        eveCommandClient.Contains("OperationId = invocationRecord.OperationId", StringComparison.Ordinal) ||
        eveCommandClient.Contains("Payload = new System.Collections.Generic.Dictionary<string, string>(\r\n                    envelope.Payload", StringComparison.Ordinal) ||
        eveCommandClient.Contains("Payload = new System.Collections.Generic.Dictionary<string, string>(\n                    envelope.Payload", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve command persistence reintroduced local CultMesh invocation/payload flattening; use CultMesh.OperationInvocationRecord and CultMeshOperationPayload.ToDictionary.");
    }

    if (eveCommandClient.Contains("public static class AetheriaRuntimeEveCommands", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeEveCommands is public again; public Eve UI operations belong on AetheriaClient.Ui.");
    }

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

    if (typedCommandSources.Contains("TradeValuePolicy()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Eve command handling still uses the named trade-value policy helper instead of generic mutable typed document access.");
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

    var forbiddenUnityEveCommandSubmitSymbols = new[]
    {
        "AetheriaRuntimeEveCommands.TrySendInputSettingsCommand",
        "AetheriaRuntimeEveCommands.TrySendLoadoutTemplateCommand",
        "AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand"
    };
    var unityCommandSources = new Dictionary<string, string>
    {
        [actionGameManagerPath] = actionGameManager,
        [mainMenuPath] = mainMenu,
        [evePresenterPath] = evePresenter
    };
    var legacyUnityCommandHits = unityCommandSources
        .SelectMany(entry => forbiddenUnityEveCommandSubmitSymbols
            .Where(symbol => entry.Value.Contains(symbol, StringComparison.Ordinal))
            .Select(symbol => $"{Path.GetRelativePath(root, entry.Key)} -> {symbol}"))
        .ToArray();
    if (legacyUnityCommandHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity-facing shells still submit Eve commands through legacy helper ports instead of AetheriaRuntimeVerseClient: " +
            string.Join("; ", legacyUnityCommandHits));
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

    if (File.Exists(runtimeCommandPortPath))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeCommandPort.cs has returned. Submit typed command documents through AetheriaRuntimeVerseClient.");
    }

    var forbiddenSharedCommandRuntimeIds = new[]
    {
        "string runtimeId = \"unity-input-provider\"",
        "? \"unity-input-provider\" : runtimeId"
    };
    var sharedCommandRuntimeIdHits = forbiddenSharedCommandRuntimeIds
        .Where(symbol => typedCommandSources.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (sharedCommandRuntimeIdHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared command runtime still uses Unity-specific fallback runtime ids: " +
            string.Join(", ", sharedCommandRuntimeIdHits));
    }

    if (typedCommandSources.Contains("AetheriaRuntimeCommandSubmitter", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared command runtime still preserves the cached command submitter instead of routing typed clients through AetheriaRuntimeVerseClient.");
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
            "Renderer code still speaks to the Eve mailbox log instead of the typed Verse client path: " +
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
    var runtimeStateRefResolverPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateRefResolver.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var mainMenuSurfaceBuilderPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeMainMenuSurfaceBuilder.cs");

    var requiredFiles = new[]
    {
        packageSnapshotPath,
        packageStorePath,
        runtimeStateRefResolverPath,
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
    var runtimeStateRefResolver = File.ReadAllText(runtimeStateRefResolverPath);
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

    var requiredMainMenuSymbols = new[]
    {
        "ResolveVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)",
        ".Document<AetheriaRuntimeVerseHostSettingsDocument>().Reactive()",
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
    var runtimeStateRefResolverPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeStateRefResolver.cs");
    var runtimeStateRefResolver = File.Exists(runtimeStateRefResolverPath)
        ? File.ReadAllText(runtimeStateRefResolverPath)
        : throw new InvalidOperationException("Cannot verify Continue entity readback; shared runtime state-ref resolver is missing.");
    var packageStorePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeCatalogStore.cs");
    var packageStore = File.Exists(packageStorePath)
        ? File.ReadAllText(packageStorePath)
        : throw new InvalidOperationException("Cannot verify Continue entity payload readback; package runtime store is missing.");
    var observedDockingIndexPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedDockingIndex.cs");
    var observedDockingIndex = File.Exists(observedDockingIndexPath)
        ? File.ReadAllText(observedDockingIndexPath)
        : throw new InvalidOperationException("Cannot verify Continue entity identity; AetheriaUnityObservedDockingIndex.cs is missing.");
    var currentEntityPresentationPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityCurrentEntityPresentation.cs");
    var currentEntityPresentation = File.Exists(currentEntityPresentationPath)
        ? File.ReadAllText(currentEntityPresentationPath)
        : throw new InvalidOperationException("Cannot verify Continue entity presentation; AetheriaUnityCurrentEntityPresentation.cs is missing.");
    var currentEntityBinderPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityCurrentEntityBinder.cs");
    var currentEntityBinder = File.Exists(currentEntityBinderPath)
        ? File.ReadAllText(currentEntityBinderPath)
        : throw new InvalidOperationException("Cannot verify Continue current entity binder; AetheriaUnityCurrentEntityBinder.cs is missing.");
    var targetPresentationPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityTargetPresentation.cs");
    var targetPresentation = File.Exists(targetPresentationPath)
        ? File.ReadAllText(targetPresentationPath)
        : throw new InvalidOperationException("Cannot verify Continue target presentation; AetheriaUnityTargetPresentation.cs is missing.");
    var observedTargetQueryPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedTargetQuery.cs");
    var observedTargetQuery = File.Exists(observedTargetQueryPath)
        ? File.ReadAllText(observedTargetQueryPath)
        : throw new InvalidOperationException("Cannot verify Continue target query; AetheriaUnityObservedTargetQuery.cs is missing.");
    var observedFrameApplierPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedFrameApplier.cs");
    var observedFrameApplier = File.Exists(observedFrameApplierPath)
        ? File.ReadAllText(observedFrameApplierPath)
        : throw new InvalidOperationException("Cannot verify Continue frame application; AetheriaUnityObservedFrameApplier.cs is missing.");
    var pilotFrameControllerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotFrameController.cs");
    var pilotFrameController = File.Exists(pilotFrameControllerPath)
        ? File.ReadAllText(pilotFrameControllerPath)
        : throw new InvalidOperationException("Cannot verify Continue pilot frame controller; AetheriaUnityPilotFrameController.cs is missing.");
    var legacyUnityDaemonEntitySnapshotProjectorPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityDaemonEntitySnapshotProjector.cs");
    if (File.Exists(legacyUnityDaemonEntitySnapshotProjectorPath))
    {
        throw new InvalidOperationException(
            "Unity still owns daemon entity snapshot projection; package runtime should lower typed daemon entity snapshots.");
    }

    var daemonEntitySnapshotProjectorPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEntitySnapshotProjector.cs");
    var daemonEntitySnapshotProjector = File.Exists(daemonEntitySnapshotProjectorPath)
        ? File.ReadAllText(daemonEntitySnapshotProjectorPath)
        : throw new InvalidOperationException("Cannot verify Continue entity projection; AetheriaRuntimeEntitySnapshotProjector.cs is missing.");
    var observedEntityRestorerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedEntityRestorer.cs");
    var observedEntityRestorer = File.Exists(observedEntityRestorerPath)
        ? File.ReadAllText(observedEntityRestorerPath)
        : throw new InvalidOperationException("Cannot verify Continue entity projection; AetheriaUnityObservedEntityRestorer.cs is missing.");
    var entityBlueprintMaterializerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityEntityBlueprintMaterializer.cs");
    var entityBlueprintMaterializer = File.Exists(entityBlueprintMaterializerPath)
        ? File.ReadAllText(entityBlueprintMaterializerPath)
        : throw new InvalidOperationException("Cannot verify Continue entity construction materialization; AetheriaUnityEntityBlueprintMaterializer.cs is missing.");
    var observedZoneContextFactoryPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedZoneContextFactory.cs");
    var observedZoneContextFactory = File.Exists(observedZoneContextFactoryPath)
        ? File.ReadAllText(observedZoneContextFactoryPath)
        : throw new InvalidOperationException("Cannot verify Continue zone context projection; AetheriaUnityObservedZoneContextFactory.cs is missing.");
    var gameplaySceneWiringPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplaySceneWiring.cs");
    var gameplaySceneWiring = File.Exists(gameplaySceneWiringPath)
        ? File.ReadAllText(gameplaySceneWiringPath)
        : throw new InvalidOperationException("Cannot verify Continue scene wiring; AetheriaUnityGameplaySceneWiring.cs is missing.");

    var requiredMenuSymbols = new[]
    {
        "ResolveSectorMap",
        "AetheriaClient",
        ".State",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "ContinueGame()",
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
        "ObservedFrameApplier.ApplyLatestZoneRender()",
        "private AetheriaUnityGameplayLoopShell GameplayLoopShell =>",
        "GameplayLoopShell.Tick(Time.deltaTime, Time.time)",
        "GameplayLoopShell.LateTick()",
        "ApplyLatestZoneRender = () => ObservedFrameApplier.ApplyLatestZoneRender()",
        "private AetheriaUnityObservedFrameApplier ObservedFrameApplier =>",
        "private Galaxy ObservedGalaxy { get; set; }",
        "ObservedGalaxy = boot.ObservedGalaxy",
        "ResolveObservedGalaxyZone",
        "private AetheriaUnityObservedZoneContextFactory ObservedZoneContextFactory =>",
        "entity => CurrentEntityBinder.RestoreBinding(entity)",
        "private AetheriaUnityObservedTargetQuery ObservedTargetQuery =>",
        "ResolveObservedTarget = entity => ObservedTargetQuery.GetObservedTarget(entity)",
        "TargetPresentation = _targetPresentation",
        "private readonly AetheriaUnityObservedEntityIndex _observedEntityIndex",
        "private AetheriaUnityEntityBlueprintMaterializer EntityBlueprintMaterializer =>",
        "EntityBlueprintMaterializer.MaterializeObservedEntity",
        "_loadoutItemFactory.CreateLoadoutItem",
        "private AetheriaUnityObservedDockingIndex ObservedDocking =>",
        "ObservedDocking.IsEntityUndocked(CurrentEntity)",
        "IsCurrentEntityObservedUndocked(",
        "private AetheriaUnityCurrentEntityBinder CurrentEntityBinder =>",
        "private readonly AetheriaUnityCurrentEntityPresentation _currentEntityPresentation",
        "CurrentEntityPresentation = _currentEntityPresentation",
        "private AetheriaUnityPilotFrameController PilotFrameController =>",
        "PilotFrameController = PilotFrameController",
        "SceneWiring.ConfigureCurrentEntityPresentation(_currentEntityPresentation, boot.RuntimeCatalog)",
        "SceneWiring.ConfigureTargetPresentation("
    };

    if (actionGameManager.Contains("_authoritativeDaemonEntities", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity entity wrappers are observed daemon facades, not authoritative state; keep the daemon as authority.");
    }

    var forbiddenGameplayCatalogPolicySymbols = new[]
    {
        "private static AetheriaRuntimeCatalogSnapshot _runtimeCatalog",
        "FindTypedRuntimeItem(",
        "_runtimeCatalog?.FindItem(",
        "ArticulatedWeaponBehaviorKinds",
        "_targetPresentation.ResolveTypedRuntimeItem"
    };
    var gameplayCatalogPolicyHits = forbiddenGameplayCatalogPolicySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (gameplayCatalogPolicyHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns runtime catalog presentation policy instead of injecting the boot catalog into Unity presentation helpers: " +
            string.Join(", ", gameplayCatalogPolicyHits));
    }

    var requiredEntitySnapshotProjectorSymbols = new[]
    {
        "public static class AetheriaRuntimeEntitySnapshotProjector",
        "public static AetheriaRuntimeEntitySnapshot[] CreateSnapshots(",
        "public static string DaemonEntityRecordKey(string runId, int zoneIndex, int entityIndex)",
        "new AetheriaRuntimeEntitySnapshot(",
        "entity.EntityIndex,",
        "CreateItemSlots(entity.Equipment)",
        "CreateWeaponStates(runId, zoneIndex, entity.WeaponStates)",
        "CreateBehaviorStates(entity.BehaviorStates)",
        "CreateCargoBays(entity.CargoContents)",
        "new AetheriaRuntimeEntityContactSnapshot("
    };
    var missingEntitySnapshotProjectorSymbols = requiredEntitySnapshotProjectorSymbols
        .Where(symbol => !daemonEntitySnapshotProjector.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingEntitySnapshotProjectorSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon entity snapshot projection must live in the runtime package outside ActionGameManager and Unity Assets: " +
            string.Join(", ", missingEntitySnapshotProjectorSymbols));
    }

    var forbiddenManagerSnapshotProjectionSymbols = new[]
    {
        "private static AetheriaRuntimeEntitySnapshot[] CreateDaemonEntitySnapshots",
        "private static AetheriaRuntimeEntitySnapshot CreateDaemonEntitySnapshot",
        "private static AetheriaRuntimeEntityItemSlotSnapshot[] CreateDaemonItemSlots",
        "private static AetheriaRuntimeCargoBayLoadoutSnapshot[] CreateDaemonCargoBays",
        "private static AetheriaRuntimeWeaponStateSnapshot[] CreateDaemonWeaponStates",
        "private static AetheriaRuntimeBehaviorStateSnapshot[] CreateDaemonBehaviorStates",
        "private static string DaemonEntityRecordKey(",
        "private static int EntityIndexFromRecordKey("
    };
    var managerSnapshotProjectionHits = forbiddenManagerSnapshotProjectionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerSnapshotProjectionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns daemon entity snapshot projection internals: " +
            string.Join(", ", managerSnapshotProjectionHits));
    }

    var requiredObservedEntityRestorerSymbols = new[]
    {
        "public sealed class AetheriaUnityObservedEntityRestorer",
        "public bool TryApplyInPlace(",
        "public void Replace(",
        "EntityConstructionBlueprintMaterializer.MaterializeObservedFromBlueprint(_itemManager, zone, blueprint)",
        "entity.RestoreStatGrids(entitySnapshot.StatGrids)",
        "entity.RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia)",
        "RestoreActiveConsumables(entity, entitySnapshot)",
        "RestoreRuntimeBehaviorState(entity, entitySnapshot, restoredEntities)",
        "_entityIndex.EntitiesByRecordKey",
        "ResolveRuntimeBehavior(entity, weaponState.OwnerKind, weaponState.OwnerIndex, weaponState.BehaviorIndex)",
        "lockWeapon.RestoreRuntimeState(",
        "drive.RestoreRuntimeState(",
        "resourceScanner.RestoreRuntimeState(",
        "_entityIndex.RefreshDaemonIndex();"
    };
    var missingObservedEntityRestorerSymbols = requiredObservedEntityRestorerSymbols
        .Where(symbol => !observedEntityRestorer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedEntityRestorerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed Unity entity mutation/restoration must live in AetheriaUnityObservedEntityRestorer instead of ActionGameManager: " +
            string.Join(", ", missingObservedEntityRestorerSymbols));
    }

    var forbiddenManagerFacadeProjectionSymbols = new[]
    {
        "private bool CanApplyDaemonEntitySnapshotsInPlace(",
        "private void ApplyDaemonEntitySnapshotsInPlace(",
        "private void ReplaceObservedEntitySnapshotsFromTypedSnapshots(",
        "private void RestoreActiveConsumablesFromTypedEntitySnapshot(",
        "private void RestoreRuntimeBehaviorStateFromTypedSnapshot(",
        "private static Behavior ResolveRuntimeBehavior(",
        "private static IReadOnlyList<Behavior> ResolveRuntimeBehaviorList(",
        "EntityConstructionBlueprintMaterializer.MaterializeObservedFromBlueprint(ItemManager, Zone, blueprint)",
        "entity.RestoreStatGrids(entitySnapshot.StatGrids)",
        "RestoreActiveConsumablesFromTypedEntitySnapshot(entity, entitySnapshot)",
        "RestoreRuntimeBehaviorStateFromTypedSnapshot(entity, entitySnapshot, restoredEntities)"
    };
    var managerFacadeProjectionHits = forbiddenManagerFacadeProjectionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerFacadeProjectionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns observed entity mutation/restoration internals: " +
            string.Join(", ", managerFacadeProjectionHits));
    }

    var requiredEntityBlueprintMaterializerSymbols = new[]
    {
        "public sealed class AetheriaUnityEntityBlueprintMaterializer",
        "public EntityConstructionBlueprint MaterializeTemplate(AetheriaRuntimeLoadoutTemplateSnapshot template)",
        "public EntityConstructionBlueprint MaterializeLoadoutEntity(AetheriaRuntimeEntityLoadoutSnapshot entity)",
        "public EntityConstructionBlueprint MaterializeObservedEntity(",
        "private static EntityConstructionBlueprint CreateBlueprint(string kind)",
        "new OrbitalEntityConstructionBlueprint()",
        "new ShipConstructionBlueprint()",
        "shipBlueprint.Position = new float3((float)entity.PositionX",
        "shipBlueprint.IsPlayerShip = isCurrentEntity",
        "blueprint.Equipment = CreateEquippableSlots(entity.Equipment)",
        "blueprint.CargoContents = CreateCargoBayContents(entity.CargoContents)",
        "blueprint.Children = entity.Children",
        "_loadoutItemFactory.CreateLoadoutItem(slot.Item)",
        "_loadoutItemFactory.CreateLoadoutItem(item) as EquippableItem"
    };
    var missingEntityBlueprintMaterializerSymbols = requiredEntityBlueprintMaterializerSymbols
        .Where(symbol => !entityBlueprintMaterializer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingEntityBlueprintMaterializerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity entity construction blueprint materialization must live in AetheriaUnityEntityBlueprintMaterializer instead of ActionGameManager: " +
            string.Join(", ", missingEntityBlueprintMaterializerSymbols));
    }

    var forbiddenManagerEntityConstructionSymbols = new[]
    {
        "private EntityConstructionBlueprint CreateEntityConstructionBlueprint(",
        "private (int2 position, EquippableItem item)[] CreateEquippableSlots(",
        "private (int2 position, ItemInstance item)[][] CreateCargoBayContents(",
        "private EquippableItem CreateEquippableLoadoutItem(",
        "private ItemInstance CreateLoadoutItem(AetheriaRuntimeLoadoutItemSnapshot",
        "new OrbitalEntityConstructionBlueprint()",
        "new ShipConstructionBlueprint",
        "blueprint.Children = entity.Children",
        "ItemManager.CreateSimpleCommodityInstance(typedItem",
        "ItemManager.CreateCraftedInstance(typedItem"
    };
    var managerEntityConstructionHits = forbiddenManagerEntityConstructionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerEntityConstructionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns Unity entity construction blueprint lowering internals: " +
            string.Join(", ", managerEntityConstructionHits));
    }

    var requiredObservedZoneContextFactorySymbols = new[]
    {
        "public sealed class AetheriaUnityObservedZoneContextFactory",
        "public Zone ResolveContext(",
        "private readonly Dictionary<int, Zone> _observedZoneContextsByDaemonIndex",
        "private static ZoneConstructionBlueprint CreateZoneConstructionBlueprint(",
        "private static BodyConstructionData CreateBodyConstructionData(",
        "private static GasGiantConstructionData CreateGasGiantConstructionData(",
        "private static SunConstructionData CreateSunConstructionData(",
        "private static void PopulateBodyConstructionData(",
        "new Zone(_itemManager, _planetSettings, constructionBlueprint, galaxyZone, _observedGalaxy)",
        "_playMusic(MusicType.Overworld);"
    };
    var missingObservedZoneContextFactorySymbols = requiredObservedZoneContextFactorySymbols
        .Where(symbol => !observedZoneContextFactory.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedZoneContextFactorySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed Unity zone context lowering must live in AetheriaUnityObservedZoneContextFactory instead of ActionGameManager: " +
            string.Join(", ", missingObservedZoneContextFactorySymbols));
    }

    var forbiddenManagerZoneContextProjectionSymbols = new[]
    {
        "private void PrepareObservedDaemonZoneContext(",
        "private static ZoneConstructionBlueprint CreateDaemonZoneConstructionBlueprint(",
        "private static BodyConstructionData CreateDaemonBodyConstructionData(",
        "private static GasGiantConstructionData CreateDaemonGasGiantConstructionData(",
        "private static SunConstructionData CreateDaemonSunConstructionData(",
        "private static void PopulateDaemonBodyConstructionData(",
        "new Zone(ItemManager, Settings.PlanetSettings",
        "private readonly Dictionary<int, Zone> _observedZoneContextsByDaemonIndex"
    };
    var managerZoneContextProjectionHits = forbiddenManagerZoneContextProjectionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerZoneContextProjectionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns observed zone/body context lowering internals: " +
            string.Join(", ", managerZoneContextProjectionHits));
    }

    var requiredObservedFrameApplierSymbols = new[]
    {
        "public sealed class AetheriaUnityObservedFrameApplier",
        "public bool ApplyLatestZoneRender()",
        "observer.LastRenderView?.ZoneRender",
        "private bool TryRestoreEntityGraphFromZoneRender(",
        "if (string.IsNullOrWhiteSpace(render.RunId))",
        "Aetheria zone-render feed does not identify a run id.",
        "AetheriaRuntimeEntitySnapshotProjector.CreateSnapshots(runId, render.ZoneIndex, render.EntitySnapshots)",
        ".OrderBy(entity => entity.EntityIndex)",
        "_entityRestorer.TryApplyInPlace(",
        "zoneRenderer?.ApplyZoneRender(render)",
        "_zoneContextFactory.ResolveContext(targetZone, render)",
        "_entityRestorer.Replace(entitySnapshots, currentEntityKey, _getZone())",
        "_restoreCurrentEntityBinding(currentEntity)",
        "_entityIndex.TryResolveEntityByRecordKey(currentEntityKey, out var currentEntity)",
        "_entityIndex.EntitiesByDaemonIndex",
        "zoneRenderer?.LoadDaemonZoneView(_entityIndex.EntitiesByDaemonIndex, render)",
        "zoneRenderer?.RestoreDroppedPickupsFromZoneRender(render)"
    };
    var missingObservedFrameApplierSymbols = requiredObservedFrameApplierSymbols
        .Where(symbol => !observedFrameApplier.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedFrameApplierSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed Unity frame application must live in AetheriaUnityObservedFrameApplier instead of ActionGameManager: " +
            string.Join(", ", missingObservedFrameApplierSymbols));
    }

    var forbiddenManagerFrameApplicationSymbols = new[]
    {
        "_lastAppliedAuthoritativeDaemonFrameId",
        "_lastAppliedAuthoritativeDaemonFramePath",
        "_lastAppliedAuthoritativeDaemonRunId",
        "_lastAppliedAuthoritativeDaemonZoneIndex",
        "private bool TryRestoreEntityGraphFromZoneRender(",
        "AetheriaRuntimeRtsProjection.ProjectZoneRender(observed.Frame)",
        "AetheriaRuntimeEntitySnapshotProjector.CreateSnapshots(runId, daemonZone)",
        "ObservedEntityRestorer.TryApplyInPlace(",
        "ObservedEntityRestorer.Replace(entitySnapshots, currentEntityKey, Zone)",
        "ZoneRenderer?.LoadDaemonZoneView(_observedEntityIndex.EntitiesByDaemonIndex, render)",
        "ZoneRenderer?.RestoreDroppedPickupsFromDaemonZoneState(daemonZone)",
        "render.ActionBarBindings",
        "private static AetheriaRuntimeActionBarBindingSnapshot ToActionBarBindingSnapshot("
    };
    var managerFrameApplicationHits = forbiddenManagerFrameApplicationSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerFrameApplicationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns authoritative daemon frame application internals: " +
            string.Join(", ", managerFrameApplicationHits));
    }

    var requiredObservedTargetQuerySymbols = new[]
    {
        "public sealed class AetheriaUnityObservedTargetQuery : IDisposable",
        "CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts",
        "public Entity GetObservedTarget(Entity observer)",
        "public float GetObservedInfoGathered(Entity observer, Entity target)",
        "public bool IsObservedHostileContact(Entity observer, Entity target)",
        "public AetheriaRuntimeZoneContactRow[] GetObservedVisibleContacts(",
        "private bool TryQueryEntityContact(",
        "private bool TryQueryEntityTarget(",
        "AetheriaRuntimeZoneContactRow",
        "AetheriaRuntimeZoneTargetRow",
        ".State",
        ".Document<AetheriaRuntimeZoneContactsDocument>().Reactive()",
        "_zoneContacts?.Current",
        "_zoneContacts?.Dispose()",
        "_entityIndex.TryResolveEntityByDaemonIndex(targetEntityIndex, out var targetEntity)"
    };
    var missingObservedTargetQuerySymbols = requiredObservedTargetQuerySymbols
        .Where(symbol => !observedTargetQuery.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedTargetQuerySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed Unity target/contact query lowering must live in AetheriaUnityObservedTargetQuery instead of ActionGameManager: " +
            string.Join(", ", missingObservedTargetQuerySymbols));
    }

    if (observedTargetQuery.Contains("AetheriaRuntimeZoneContactsSession _zoneContacts", StringComparison.Ordinal) ||
        observedTargetQuery.Contains(".ObserveZoneContacts()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Observed Unity target/contact query still routes zone contacts through AetheriaRuntimeZoneContactsSession instead of the managed reactive typed document.");
    }

    var forbiddenManagerTargetQuerySymbols = new[]
    {
        "private bool TryQueryDaemonEntityContact(",
        "private bool TryQueryDaemonEntityTarget(",
        "private Entity GetObservedTarget(",
        "private float GetObservedInfoGathered(",
        "private bool IsObservedHostileContact(",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityContact(",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityTarget("
    };
    var managerTargetQueryHits = forbiddenManagerTargetQuerySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerTargetQueryHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns observed daemon target/contact query internals: " +
            string.Join(", ", managerTargetQueryHits));
    }

    var requiredObservedDockingSymbols = new[]
    {
        "public sealed class AetheriaUnityObservedDockingIndex",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity",
        "CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking",
        "CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit",
        "TryReadCurrentDockingDocuments(",
        "state.Document<AetheriaRuntimeCurrentEntityDocument>().Reactive()",
        "state.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive()",
        "state.Document<AetheriaRuntimeStationRefitDocument>().Reactive()",
        "_currentDocking?.Current",
        "_stationRefit?.Current",
        "public bool IsEntityUndocked(Entity entity)",
        "public bool TryResolveDockingBay(",
        "out AetheriaRuntimeCurrentDockingDocument docking",
        "_observedEntityIndex.TryResolveEntityByRecordKey(docking.DockParentEntityKey",
        "docking.DockingBayIndex"
    };
    var missingObservedDockingSymbols = requiredObservedDockingSymbols
        .Where(symbol => !observedDockingIndex.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedDockingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed docking relationship queries must live in AetheriaUnityObservedDockingIndex instead of ActionGameManager: " +
            string.Join(", ", missingObservedDockingSymbols));
    }

    var forbiddenObservedDockingSymbols = new[]
    {
        "state.Latest<AetheriaRuntimeCurrentEntityDocument>()",
        "state.Latest<AetheriaRuntimeCurrentDockingDocument>()",
        "state.Latest<AetheriaRuntimeStationRefitDocument>()",
        "AetheriaRuntimeObservedDockingState",
        "new AetheriaRuntimeObservedDockingState(",
        "State?.CurrentDocking()",
        ".State.CurrentDocking()",
        "ReactiveEntity()",
        "ReactiveDocking()"
    };
    var observedDockingHits = forbiddenObservedDockingSymbols
        .Where(symbol => observedDockingIndex.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (observedDockingHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed docking state composition must use owned managed reactive typed documents in AetheriaUnityObservedDockingIndex: " +
            string.Join(", ", observedDockingHits));
    }

    var requiredCurrentEntityBinderSymbols = new[]
    {
        "public sealed class AetheriaUnityCurrentEntityBinder",
        "public void RestoreBinding(",
        "public void ClearBinding()",
        "private void BindUndocked(",
        "ObservedDocking.TryResolveDockingBay(currentEntity, out var dockParent, out _)",
        "CurrentEntityPresentation?.BindDocked(",
        "CurrentEntityPresentation?.BindUndocked(",
        "TargetPresentation?.ClearIndicators();",
        "CurrentEntityPresentation?.ClearBinding();",
        "DisablePlayerInput?.Invoke();",
        "ApplyActionBarBindings?.Invoke(Array.Empty<AetheriaUnityActionBarBinding>())",
        "TargetPresentation?.ReconcileVisibleTargetIndicators(GetCurrentEntity?.Invoke())",
        "SetViewDirection?.Invoke((float3)AetheriaMath.ToUnityXZ(entity.CultDirection))"
    };
    var missingCurrentEntityBinderSymbols = requiredCurrentEntityBinderSymbols
        .Where(symbol => !currentEntityBinder.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingCurrentEntityBinderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Current-entity binding orchestration must live in AetheriaUnityCurrentEntityBinder instead of ActionGameManager: " +
            string.Join(", ", missingCurrentEntityBinderSymbols));
    }

    var forbiddenManagerCurrentEntityBinderSymbols = new[]
    {
        "if (ObservedDocking.TryResolveDockingBay(currentEntity, out var dockParent, out _))",
        "_currentEntityPresentation.BindDocked(",
        "_currentEntityPresentation.BindUndocked(",
        "_targetPresentation.ClearIndicators();",
        "_currentEntityPresentation.ClearBinding();",
        "private void BindToEntity(",
        "DeathPost.weight = 0;",
        "ZoneRenderer.TryGetEntityInstance(entity, out var entityInstance)",
        "UpdateTowingStation"
    };
    var managerCurrentEntityBinderHits = forbiddenManagerCurrentEntityBinderSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerCurrentEntityBinderHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns current-entity bind orchestration instead of delegating to AetheriaUnityCurrentEntityBinder: " +
            string.Join(", ", managerCurrentEntityBinderHits));
    }

    var requiredTargetPresentationSymbols = new[]
    {
        "public sealed class AetheriaUnityTargetPresentation",
        "public Func<Entity, double, bool, AetheriaRuntimeZoneContactRow[]> ResolveVisibleContacts { get; set; }",
        "ResolveVisibleContacts?.Invoke(",
        "public Entity Tick(Entity currentEntity, float time)",
        "public void ReconcileVisibleTargetIndicators(Entity currentEntity)",
        "public void UpdateTargetIndicators(",
        "renderSettings.NormalizeDetectionProgress(",
        "renderSettings.ResolveTargetSpottedFillEnabled(",
        "renderSettings.NormalizeTargetVisibilityFill(",
        "renderSettings.NormalizeVisibilityToTargetFill(",
        "renderSettings.NormalizeTargetStatusFill(",
        "renderSettings.ResolveLockIndicatorNoiseAmplitude(",
        "renderSettings.ResolveLockIndicatorNoiseFrequency(",
        "renderSettings.ResolveLockSpinSpeed(",
        "ResolveInfoGathered?.Invoke(currentEntity, target)",
        "ResolveHostileContact?.Invoke(currentEntity, observedTarget)",
        "public AetheriaRuntimeCatalogSnapshot RuntimeCatalog { get; set; }",
        "RuntimeCatalog?.FindItem(target.Hull, x => x.ItemKey)"
    };
    var missingTargetPresentationSymbols = requiredTargetPresentationSymbols
        .Where(symbol => !targetPresentation.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTargetPresentationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Daemon-observed target HUD presentation must live in AetheriaUnityTargetPresentation instead of ActionGameManager: " +
            string.Join(", ", missingTargetPresentationSymbols));
    }

    var requiredPilotFrameControllerSymbols = new[]
    {
        "public sealed class AetheriaUnityPilotFrameController",
        "public void Tick(Entity currentEntity, float deltaTime, float timeSeconds)",
        "TargetPresentation?.Tick(currentEntity, timeSeconds)",
        "Input.Player.Look.ReadValue<Vector2>()",
        "PilotCommands.RequestLookDirection(viewDirection)",
        "renderSettings.NormalizeHeatstrokePost(currentEntity.Heatstroke)",
        "renderSettings.ResolveSevereHeatstrokePostWeight(currentEntity.Heatstroke, timeSeconds)",
        "Input.Player.Move.ReadValue<Vector2>()",
        "PilotCommands.RequestMoveVector(movement)",
        "Input.Player.TractorBeam.ReadValue<float>()",
        "PilotCommands.RequestTractorPower(Saturate("
    };
    var missingPilotFrameControllerSymbols = requiredPilotFrameControllerSymbols
        .Where(symbol => !pilotFrameController.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingPilotFrameControllerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Undocked pilot-frame input/presentation policy must live in AetheriaUnityPilotFrameController instead of ActionGameManager: " +
            string.Join(", ", missingPilotFrameControllerSymbols));
    }

    var forbiddenManagerPilotFrameSymbols = new[]
    {
        "Input.Player.Look.ReadValue<Vector2>()",
        "Input.Player.Move.ReadValue<Vector2>()",
        "Input.Player.TractorBeam.ReadValue<float>()",
        "PilotCommands.RequestLookDirection(_viewDirection)",
        "PilotCommands.RequestMoveVector(movement)",
        "PilotCommands.RequestTractorPower(Saturate(",
        "HeatstrokePost.weight = (float)renderSettings.NormalizeHeatstrokePost(",
        "SevereHeatstrokePost.weight ="
    };
    var managerPilotFrameHits = forbiddenManagerPilotFrameSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerPilotFrameHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns undocked pilot-frame input/presentation internals instead of delegating to AetheriaUnityPilotFrameController: " +
            string.Join(", ", managerPilotFrameHits));
    }

    var forbiddenManagerDockingScanSymbols = new[]
    {
        "TryGetDaemonParentSnapshot(",
        "TryResolveDaemonDockingBay(",
        "parentSnapshot.DockingBayAssignments"
    };
    var managerDockingScanHits = forbiddenManagerDockingScanSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerDockingScanHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still scans daemon docking relationships instead of delegating to AetheriaUnityObservedDockingIndex: " +
            string.Join(", ", managerDockingScanHits));
    }

    var requiredCurrentEntityPresentationSymbols = new[]
    {
        "public sealed class AetheriaUnityCurrentEntityPresentation",
        "public void BindUndocked(",
        "public void BindDocked(",
        "public void ClearBinding()",
        "public void Tick(float deltaTime)",
        "private readonly List<IDisposable> _shipSubscriptions",
        "private readonly List<IDisposable> _targetSubscriptions",
        "public (HardpointData[] hardpoints, Transform[] barrels, PlaceUIElementWorldspace crosshair)[] ArticulationGroups",
        "public (LockWeapon targetLock, PlaceUIElementWorldspace indicator, Rotate spin)[] LockingIndicators",
        "entity.TargetedByCount.Subscribe",
        "entity.Target.Subscribe",
        "target.IncomingHit.Where",
        "_hitMarkerTime = HitMarkerDuration",
        "LockIndicator.Instantiate<PlaceUIElementWorldspace>()",
        "TradeMenu.Inventory = entity.CargoBays.First()",
        "DockCamera.enabled = true",
        "FollowCamera.enabled = false",
        "DockCamera.Follow = orbitalInstance.transform",
        "ZoneRenderer.TryGetBodyView(parentOrbitPlanetBodyKey, out var parentBodyView)",
        "Menu.ShowTab(MenuTab.Inventory)"
    };
    var missingCurrentEntityPresentationSymbols = requiredCurrentEntityPresentationSymbols
        .Where(symbol => !currentEntityPresentation.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingCurrentEntityPresentationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Current-entity Unity presentation binding must live in AetheriaUnityCurrentEntityPresentation instead of ActionGameManager: " +
            string.Join(", ", missingCurrentEntityPresentationSymbols));
    }

    var forbiddenManagerCurrentPresentationSymbols = new[]
    {
        "private List<IDisposable> _shipSubscriptions",
        "private List<IDisposable> _targetSubscriptions",
        "private (HardpointData[] hardpoints, Transform[] barrels, PlaceUIElementWorldspace crosshair)[] _articulationGroups",
        "private (LockWeapon targetLock, PlaceUIElementWorldspace indicator, Rotate spin)[] _lockingIndicators",
        "private float _hitMarkerTime",
        "private void DoDock(",
        "TradeMenu.Inventory =",
        "DockCamera.Follow =",
        "DockCamera.LookAt =",
        "DockCamera.enabled = true",
        "FollowCamera.enabled = false",
        "ZoneRenderer.TryGetBodyView(parentOrbitPlanetBodyKey, out var parentBodyView)",
        "Menu.ShowTab(MenuTab.Inventory)",
        "CurrentEntity.TargetedByCount.Subscribe",
        "CurrentEntity.Target.Subscribe",
        "target.IncomingHit.Where",
        "LockIndicator.Instantiate<PlaceUIElementWorldspace>()"
    };
    var managerCurrentPresentationHits = forbiddenManagerCurrentPresentationSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerCurrentPresentationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns bind-time current-entity presentation state instead of delegating to AetheriaUnityCurrentEntityPresentation: " +
            string.Join(", ", managerCurrentPresentationHits));
    }

    if (actionGameManager.Contains("ItemManager.GetRuntimeItem", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager still resolves item metadata through Unity ItemManager instead of the runtime catalog.");
    }

    if (actionGameManager.Contains("public bool TryGetObservedCurrentEntity(out Entity entity)", StringComparison.Ordinal) ||
        actionGameManager.Contains("public bool TryGetObservedDockingBay(out EquippedDockingBay dockingBay)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose broad observed current-entity or docking-bay read shims; clients must use typed current projections and key-scoped facade adapters.");
    }

    if (actionGameManager.Contains("public EquippedDockingBay DockingBay", StringComparison.Ordinal) ||
        actionGameManager.Contains("public Entity DockedEntity", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose renderer-local docked entity or docking bay state as public gameplay truth; clients must use typed current-docking projections.");
    }

    if (actionGameManager.Contains("public Entity TowingStation", StringComparison.Ordinal) ||
        actionGameManager.Contains("AvailableCargoBays()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose renderer-local towing context or available cargo bay enumeration as public gameplay truth; clients must use typed projections and daemon operations.");
    }

    if (actionGameManager.Contains("public int Credits", StringComparison.Ordinal) ||
        actionGameManager.Contains("Credits = render.Credits", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not mirror run credits as manager-global gameplay state; clients must read credits through typed projections such as StationRefitAsync.");
    }

    if (actionGameManager.Contains("public static AetheriaRuntimeCatalogSnapshot RuntimeCatalog", StringComparison.Ordinal) ||
        actionGameManager.Contains("public AetheriaRuntimeCatalogSnapshot RuntimeCatalog", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose the typed runtime catalog as manager-global gameplay state; clients must open typed catalog views through AetheriaClient.");
    }

    if (actionGameManager.Contains("public AetheriaRuntimeDaemonRenderSettings ObservedDaemonRenderSettings()", StringComparison.Ordinal) ||
        actionGameManager.Contains("ObservedDaemonRenderSettings(", StringComparison.Ordinal) ||
        actionGameManager.Contains("new AetheriaRuntimeDaemonRenderSettings(", StringComparison.Ordinal) ||
        actionGameManager.Contains("ConfigureSchematicDisplayRenderSettings()", StringComparison.Ordinal) ||
        actionGameManager.Contains("SchematicDisplay?.SetRenderSettings(", StringComparison.Ordinal) ||
        actionGameManager.Contains("TargetSchematicDisplay?.SetRenderSettings(", StringComparison.Ordinal) ||
        actionGameManager.Contains("private void UpdatePlayerPanel()", StringComparison.Ordinal) ||
        actionGameManager.Contains("private void UpdateTargetPanel(Entity target)", StringComparison.Ordinal) ||
        actionGameManager.Contains("public EntityConstructionBlueprint CreateEntityConstructionBlueprint(AetheriaRuntimeLoadoutTemplateSnapshot template)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must delegate daemon render-settings and loadout-template lowering to focused renderer/bootstrap adapters, not expose them as client gameplay APIs.");
    }

    if (actionGameManager.Contains("ObservedLoadoutTemplates", StringComparison.Ordinal) ||
        actionGameManager.Contains("_observedLoadoutTemplates", StringComparison.Ordinal) ||
        actionGameManager.Contains("LoadRuntimeLoadoutTemplates", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not cache or expose loadout templates as manager-global observed state; clients must read typed loadout templates directly.");
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

    var requiredGameplaySceneWiringSymbols = new[]
    {
        "public void ConfigureCurrentEntityPresentation(",
        "presentation.RuntimeCatalog = runtimeCatalog",
        "public void ConfigureTargetPresentation(",
        "presentation.ResolveTarget = observedTargetQuery.GetObservedTarget",
        "presentation.ResolveInfoGathered = observedTargetQuery.GetObservedInfoGathered",
        "presentation.ResolveHostileContact = observedTargetQuery.IsObservedHostileContact",
        "presentation.RuntimeCatalog = runtimeCatalog"
    };

    var missingGameplaySceneWiringSymbols = requiredGameplaySceneWiringSymbols
        .Where(symbol => !gameplaySceneWiring.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingGameplaySceneWiringSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplaySceneWiring no longer has the daemon-frame Continue presentation boot path: " +
            string.Join(", ", missingGameplaySceneWiringSymbols));
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
        "_currentEntityPresentation.Tick(Time.deltaTime);",
        "PilotFrameController.Tick(CurrentEntity, Time.deltaTime, Time.time)",
        "_targetPresentation.UpdateTargetIndicators(",
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
        "public string RecordKey"
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
    var pilotCommandSenderPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotCommandSender.cs");
    var pilotFrameControllerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotFrameController.cs");
    var pilotOperationControllerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotOperationController.cs");
    var legacyPilotFrameAdapterPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotFrameAdapter.cs");
    var legacyPilotOperationAdapterPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityPilotOperationAdapter.cs");
    var observedTargetQueryPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedTargetQuery.cs");
    var observedFrameApplierPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedFrameApplier.cs");
    var legacyUnityDaemonEntitySnapshotProjectorPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityDaemonEntitySnapshotProjector.cs");
    var daemonEntitySnapshotProjectorPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEntitySnapshotProjector.cs");
    var observedEntityRestorerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedEntityRestorer.cs");
    var entityBlueprintMaterializerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityEntityBlueprintMaterializer.cs");
    var observedZoneContextFactoryPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedZoneContextFactory.cs");
    var currentEntityBinderPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityCurrentEntityBinder.cs");
    var gameplaySceneWiringPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplaySceneWiring.cs");
    var daemonGameplayOperationsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonOperationsClient.cs");
    var legacyDaemonGameplayOperationsPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaDaemonOperations.cs");
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
    var pilotCommandSender = File.Exists(pilotCommandSenderPath)
        ? File.ReadAllText(pilotCommandSenderPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityPilotCommandSender.cs is missing.");
    var pilotFrameController = File.Exists(pilotFrameControllerPath)
        ? File.ReadAllText(pilotFrameControllerPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityPilotFrameController.cs is missing.");
    var pilotOperationController = File.Exists(pilotOperationControllerPath)
        ? File.ReadAllText(pilotOperationControllerPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityPilotOperationController.cs is missing.");
    if (File.Exists(legacyPilotFrameAdapterPath) ||
        File.Exists(legacyPilotOperationAdapterPath) ||
        actionGameManager.Contains("AetheriaUnityPilotFrameAdapter", StringComparison.Ordinal) ||
        actionGameManager.Contains("AetheriaUnityPilotOperationAdapter", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity pilot control must be owned by named controllers; adapter-shaped pilot access is obsolete.");
    }
    var observedTargetQuery = File.Exists(observedTargetQueryPath)
        ? File.ReadAllText(observedTargetQueryPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityObservedTargetQuery.cs is missing.");
    var observedFrameApplier = File.Exists(observedFrameApplierPath)
        ? File.ReadAllText(observedFrameApplierPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityObservedFrameApplier.cs is missing.");
    if (File.Exists(legacyUnityDaemonEntitySnapshotProjectorPath))
    {
        throw new InvalidOperationException(
            "Unity still owns daemon entity snapshot projection; package runtime should lower typed daemon entity snapshots.");
    }

    var daemonEntitySnapshotProjector = File.Exists(daemonEntitySnapshotProjectorPath)
        ? File.ReadAllText(daemonEntitySnapshotProjectorPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaRuntimeEntitySnapshotProjector.cs is missing.");
    var observedEntityRestorer = File.Exists(observedEntityRestorerPath)
        ? File.ReadAllText(observedEntityRestorerPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityObservedEntityRestorer.cs is missing.");
    var entityBlueprintMaterializer = File.Exists(entityBlueprintMaterializerPath)
        ? File.ReadAllText(entityBlueprintMaterializerPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityEntityBlueprintMaterializer.cs is missing.");
    var observedZoneContextFactory = File.Exists(observedZoneContextFactoryPath)
        ? File.ReadAllText(observedZoneContextFactoryPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityObservedZoneContextFactory.cs is missing.");
    var currentEntityBinder = File.Exists(currentEntityBinderPath)
        ? File.ReadAllText(currentEntityBinderPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityCurrentEntityBinder.cs is missing.");
    var gameplaySceneWiring = File.Exists(gameplaySceneWiringPath)
        ? File.ReadAllText(gameplaySceneWiringPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; AetheriaUnityGameplaySceneWiring.cs is missing.");
    var daemonOperations = File.Exists(daemonGameplayOperationsPath)
        ? File.ReadAllText(daemonGameplayOperationsPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; shared daemon operations client is missing.");

    if (File.Exists(legacyDaemonGameplayOperationsPath))
    {
        throw new InvalidOperationException(
            "Unity gameplay still owns AetheriaDaemonOperations; shared daemon operation access belongs in the runtime package.");
    }

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
        "ObservedFrameApplier.ApplyLatestZoneRender()",
        "private AetheriaUnityGameplayLoopShell GameplayLoopShell =>",
        "GameplayLoopShell.Tick(Time.deltaTime, Time.time)",
        "GameplayLoopShell.LateTick()",
        "ApplyLatestZoneRender = () => ObservedFrameApplier.ApplyLatestZoneRender()",
        "ResolveDaemonObserver()",
        "private AetheriaUnityObservedFrameApplier ObservedFrameApplier =>",
        "private Galaxy ObservedGalaxy { get; set; }",
        "ObservedGalaxy = boot.ObservedGalaxy",
        "ResolveObservedGalaxyZone",
        "private AetheriaUnityObservedZoneContextFactory ObservedZoneContextFactory =>",
        "entity => CurrentEntityBinder.RestoreBinding(entity)",
        "private AetheriaUnityEntityBlueprintMaterializer EntityBlueprintMaterializer =>",
        "EntityBlueprintMaterializer.MaterializeObservedEntity",
        "_loadoutItemFactory.CreateLoadoutItem",
        "private AetheriaUnityPilotCommandSender PilotCommands =>",
        "private AetheriaUnityPilotFrameController PilotFrameController =>",
        "PilotFrameController = PilotFrameController",
        "private AetheriaUnityPilotOperationController PilotOperationController =>",
        "private AetheriaUnityObservedTargetQuery ObservedTargetQuery =>",
        "SceneWiring.ConfigureTargetPresentation("
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

    var requiredSceneWiringObserverSymbols = new[]
    {
        "presentation.ResolveTarget = observedTargetQuery.GetObservedTarget",
        "presentation.ResolveInfoGathered = observedTargetQuery.GetObservedInfoGathered",
        "presentation.ResolveHostileContact = observedTargetQuery.IsObservedHostileContact"
    };

    var missingSceneWiringObserverSymbols = requiredSceneWiringObserverSymbols
        .Where(symbol => !gameplaySceneWiring.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSceneWiringObserverSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity scene wiring no longer connects target presentation to observed daemon target queries: " +
            string.Join(", ", missingSceneWiringObserverSymbols));
    }

    if (!daemonEntitySnapshotProjector.Contains("new AetheriaRuntimeEntitySnapshot(", StringComparison.Ordinal) ||
        !daemonEntitySnapshotProjector.Contains("CreateWeaponStates(runId, zoneIndex, entity.WeaponStates)", StringComparison.Ordinal) ||
        !daemonEntitySnapshotProjector.Contains("entity.EntityIndex,", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity observer entity projection no longer carries typed daemon entity indices through the dedicated daemon snapshot projector.");
    }

    if (daemonEntitySnapshotProjector.Contains("EntityIndexFromRecordKey", StringComparison.Ordinal) ||
        observedFrameApplier.Contains("EntityIndexFromRecordKey", StringComparison.Ordinal) ||
        observedEntityRestorer.Contains("EntityIndexFromRecordKey", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity observer entity projection must use typed daemon entity indices instead of parsing synthetic record keys.");
    }

    if (!observedEntityRestorer.Contains("public bool TryApplyInPlace(", StringComparison.Ordinal) ||
        !observedEntityRestorer.Contains("public void Replace(", StringComparison.Ordinal) ||
        !observedEntityRestorer.Contains("EntityConstructionBlueprintMaterializer.MaterializeObservedFromBlueprint(_itemManager, zone, blueprint)", StringComparison.Ordinal) ||
        !observedEntityRestorer.Contains("RestoreRuntimeBehaviorState(entity, entitySnapshot, restoredEntities)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity observer facade restoration no longer flows through the dedicated observed entity restorer.");
    }

    var requiredEntityBlueprintMaterializerSymbols = new[]
    {
        "public sealed class AetheriaUnityEntityBlueprintMaterializer",
        "public EntityConstructionBlueprint MaterializeTemplate(AetheriaRuntimeLoadoutTemplateSnapshot template)",
        "public EntityConstructionBlueprint MaterializeLoadoutEntity(AetheriaRuntimeEntityLoadoutSnapshot entity)",
        "public EntityConstructionBlueprint MaterializeObservedEntity(",
        "private static EntityConstructionBlueprint CreateBlueprint(string kind)",
        "new OrbitalEntityConstructionBlueprint()",
        "new ShipConstructionBlueprint()",
        "shipBlueprint.Position = new float3((float)entity.PositionX",
        "shipBlueprint.Direction = new float2((float)entity.DirectionX",
        "shipBlueprint.IsPlayerShip = isCurrentEntity",
        "blueprint.Equipment = CreateEquippableSlots(entity.Equipment)",
        "blueprint.CargoContents = CreateCargoBayContents(entity.CargoContents)",
        "blueprint.Children = entity.Children",
        "_loadoutItemFactory.CreateLoadoutItem(slot.Item)",
        "_loadoutItemFactory.CreateLoadoutItem(item) as EquippableItem"
    };
    var missingEntityBlueprintMaterializerSymbols = requiredEntityBlueprintMaterializerSymbols
        .Where(symbol => !entityBlueprintMaterializer.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingEntityBlueprintMaterializerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity observer entity construction materialization must live in AetheriaUnityEntityBlueprintMaterializer instead of ActionGameManager: " +
            string.Join(", ", missingEntityBlueprintMaterializerSymbols));
    }

    var forbiddenManagerEntityConstructionSymbols = new[]
    {
        "private EntityConstructionBlueprint CreateEntityConstructionBlueprint(",
        "private (int2 position, EquippableItem item)[] CreateEquippableSlots(",
        "private (int2 position, ItemInstance item)[][] CreateCargoBayContents(",
        "private EquippableItem CreateEquippableLoadoutItem(",
        "private ItemInstance CreateLoadoutItem(AetheriaRuntimeLoadoutItemSnapshot",
        "new OrbitalEntityConstructionBlueprint()",
        "new ShipConstructionBlueprint",
        "blueprint.Children = entity.Children",
        "ItemManager.CreateSimpleCommodityInstance(typedItem",
        "ItemManager.CreateCraftedInstance(typedItem"
    };
    var managerEntityConstructionHits = forbiddenManagerEntityConstructionSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerEntityConstructionHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns Unity entity construction blueprint lowering internals: " +
            string.Join(", ", managerEntityConstructionHits));
    }

    if (!observedZoneContextFactory.Contains("public Zone ResolveContext(", StringComparison.Ordinal) ||
        !observedZoneContextFactory.Contains("private static ZoneConstructionBlueprint CreateZoneConstructionBlueprint(", StringComparison.Ordinal) ||
        !observedZoneContextFactory.Contains("private static BodyConstructionData CreateBodyConstructionData(", StringComparison.Ordinal) ||
        !observedZoneContextFactory.Contains("new Zone(_itemManager, _planetSettings, constructionBlueprint, galaxyZone, _observedGalaxy)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity observer zone context lowering no longer flows through the dedicated observed zone context factory.");
    }

    var requiredObservedFrameApplierSymbols = new[]
    {
        "public sealed class AetheriaUnityObservedFrameApplier",
        "public bool ApplyLatestZoneRender()",
        "observer.LastRenderView?.ZoneRender",
        "private bool TryRestoreEntityGraphFromZoneRender(",
        "AetheriaRuntimeEntitySnapshotProjector.CreateSnapshots(runId, render.ZoneIndex, render.EntitySnapshots)",
        ".OrderBy(entity => entity.EntityIndex)",
        "_entityRestorer.Replace(entitySnapshots, currentEntityKey, _getZone())",
        "_entityRestorer.TryApplyInPlace(",
        "_entityIndex.TryResolveEntityByRecordKey(currentEntityKey, out var currentEntity)",
        "_entityIndex.EntitiesByDaemonIndex",
        "_zoneContextFactory.ResolveContext(targetZone, render)",
        "zoneRenderer?.LoadDaemonZoneView(_entityIndex.EntitiesByDaemonIndex, render)",
        "zoneRenderer?.ApplyZoneRender(render)",
        "zoneRenderer?.RestoreDroppedPickupsFromZoneRender(render)",
        "_restoreCurrentEntityBinding(currentEntity)"
    };
    var missingObservedFrameApplierSymbols = requiredObservedFrameApplierSymbols
        .Where(symbol => !observedFrameApplier.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedFrameApplierSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity observer frame restoration must live in AetheriaUnityObservedFrameApplier instead of ActionGameManager: " +
            string.Join(", ", missingObservedFrameApplierSymbols));
    }

    var forbiddenManagerFrameApplicationSymbols = new[]
    {
        "_lastAppliedAuthoritativeDaemonFrameId",
        "_lastAppliedAuthoritativeDaemonFramePath",
        "_lastAppliedAuthoritativeDaemonRunId",
        "_lastAppliedAuthoritativeDaemonZoneIndex",
        "private bool TryRestoreEntityGraphFromZoneRender(",
        "AetheriaRuntimeRtsProjection.ProjectZoneRender(observed.Frame)",
        "AetheriaRuntimeEntitySnapshotProjector.CreateSnapshots(runId, daemonZone)",
        "ObservedEntityRestorer.Replace(entitySnapshots, currentEntityKey, Zone)",
        "ObservedEntityRestorer.TryApplyInPlace(",
        "ZoneRenderer?.LoadDaemonZoneView(_observedEntityIndex.EntitiesByDaemonIndex, render)",
        "ZoneRenderer?.ApplyZoneRender(render)",
        "ZoneRenderer?.RestoreDroppedPickupsFromDaemonZoneState(daemonZone)",
        "render.ActionBarBindings",
        "private static AetheriaRuntimeActionBarBindingSnapshot ToActionBarBindingSnapshot("
    };
    var managerFrameApplicationHits = forbiddenManagerFrameApplicationSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerFrameApplicationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns authoritative daemon frame restoration instead of delegating to AetheriaUnityObservedFrameApplier: " +
            string.Join(", ", managerFrameApplicationHits));
    }

    var requiredPilotFrameControllerSymbols = new[]
    {
        "public sealed class AetheriaUnityPilotFrameController",
        "public void Tick(Entity currentEntity, float deltaTime, float timeSeconds)",
        "TargetPresentation?.Tick(currentEntity, timeSeconds)",
        "Input.Player.Look.ReadValue<Vector2>()",
        "PilotCommands.RequestLookDirection(viewDirection)",
        "renderSettings.NormalizeHeatstrokePost(currentEntity.Heatstroke)",
        "renderSettings.ResolveSevereHeatstrokePostWeight(currentEntity.Heatstroke, timeSeconds)",
        "Input.Player.Move.ReadValue<Vector2>()",
        "PilotCommands.RequestMoveVector(movement)",
        "Input.Player.TractorBeam.ReadValue<float>()",
        "PilotCommands.RequestTractorPower(Saturate("
    };
    var missingPilotFrameControllerSymbols = requiredPilotFrameControllerSymbols
        .Where(symbol => !pilotFrameController.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingPilotFrameControllerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity observer pilot-frame input and presentation policy must live in AetheriaUnityPilotFrameController instead of ActionGameManager: " +
            string.Join(", ", missingPilotFrameControllerSymbols));
    }

    var forbiddenManagerPilotFrameSymbols = new[]
    {
        "Input.Player.Look.ReadValue<Vector2>()",
        "Input.Player.Move.ReadValue<Vector2>()",
        "Input.Player.TractorBeam.ReadValue<float>()",
        "PilotCommands.RequestLookDirection(_viewDirection)",
        "PilotCommands.RequestMoveVector(movement)",
        "PilotCommands.RequestTractorPower(Saturate(",
        "HeatstrokePost.weight = (float)renderSettings.NormalizeHeatstrokePost(",
        "SevereHeatstrokePost.weight ="
    };
    var managerPilotFrameHits = forbiddenManagerPilotFrameSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerPilotFrameHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns undocked pilot-frame input/presentation internals: " +
            string.Join(", ", managerPilotFrameHits));
    }

    var requiredPilotOperationControllerSymbols = new[]
    {
        "public sealed class AetheriaUnityPilotOperationController",
        "public void RequestInteract()",
        "public void RequestTargetSelection(Entity target)",
        "public void RequestTargetReticle()",
        "public void RequestDock()",
        "public void RequestUndock()",
        "_observedEntityIndex.TryResolveEntityRecordKey(target, out var targetEntityKey)",
        "Submit(operations => operations.Interact(), \"interact\")",
        "Submit(operations => operations.ClearTarget(), \"target clear\")",
        "operations => operations.SetTarget(targetEntityKey)",
        "operations => operations.TargetNearest()",
        "operations => operations.TargetNext()",
        "operations => operations.TargetPrevious()",
        "operations => operations.TargetReticle(",
        "operations => operations.SetOverrideShutdown(enabled)",
        "operations => operations.SensorPing()",
        "operations => operations.SetHeatsinksEnabled(enabled)",
        "operations => operations.ToggleShieldEnabled()",
        "operations => operations.DockNearest()",
        "operations => operations.Undock()",
        "_resolvePilotCommands()?.TrySubmit(submit, label)"
    };
    var missingPilotOperationControllerSymbols = requiredPilotOperationControllerSymbols
        .Where(symbol => !pilotOperationController.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingPilotOperationControllerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity pilot operation lowering must live in AetheriaUnityPilotOperationController instead of ActionGameManager: " +
            string.Join(", ", missingPilotOperationControllerSymbols));
    }

    var forbiddenManagerOperationLoweringSymbols = new[]
    {
        "PilotCommands.TrySubmit(",
        "operations => operations.Interact()",
        "operations => operations.ClearTarget()",
        "operations => operations.SetTarget(",
        "operations => operations.TargetNearest()",
        "operations => operations.TargetNext()",
        "operations => operations.TargetPrevious()",
        "operations => operations.TargetReticle(",
        "operations => operations.SetOverrideShutdown(",
        "operations => operations.SensorPing()",
        "operations => operations.SetHeatsinksEnabled(",
        "operations => operations.ToggleShieldEnabled()",
        "operations => operations.DockNearest()",
        "operations => operations.Undock()"
    };
    var managerOperationLoweringHits = forbiddenManagerOperationLoweringSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerOperationLoweringHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns typed daemon operation lowering instead of delegating to AetheriaUnityPilotOperationController: " +
            string.Join(", ", managerOperationLoweringHits));
    }

    var requiredObservedTargetQuerySymbols = new[]
    {
        "public sealed class AetheriaUnityObservedTargetQuery : IDisposable",
        "CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts",
        "public Entity GetObservedTarget(Entity observer)",
        "public float GetObservedInfoGathered(Entity observer, Entity target)",
        "public bool IsObservedHostileContact(Entity observer, Entity target)",
        "public AetheriaRuntimeZoneContactRow[] GetObservedVisibleContacts(",
        "private bool TryQueryEntityContact(",
        "private bool TryQueryEntityTarget(",
        "AetheriaRuntimeZoneContactRow",
        "AetheriaRuntimeZoneTargetRow",
        ".State",
        ".Document<AetheriaRuntimeZoneContactsDocument>().Reactive()",
        "_zoneContacts?.Current",
        "_zoneContacts?.Dispose()",
        "_entityIndex.TryResolveEntityByDaemonIndex(targetEntityIndex, out var targetEntity)"
    };
    var missingObservedTargetQuerySymbols = requiredObservedTargetQuerySymbols
        .Where(symbol => !observedTargetQuery.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedTargetQuerySymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity observer target/contact projection must live in AetheriaUnityObservedTargetQuery instead of ActionGameManager: " +
            string.Join(", ", missingObservedTargetQuerySymbols));
    }

    if (observedTargetQuery.Contains("AetheriaRuntimeZoneContactsSession _zoneContacts", StringComparison.Ordinal) ||
        observedTargetQuery.Contains(".ObserveZoneContacts()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity observer target/contact projection still routes zone contacts through AetheriaRuntimeZoneContactsSession instead of the managed reactive typed document.");
    }

    var forbiddenManagerTargetQuerySymbols = new[]
    {
        "private bool TryQueryDaemonEntityContact(",
        "private bool TryQueryDaemonEntityTarget(",
        "private Entity GetObservedTarget(",
        "private float GetObservedInfoGathered(",
        "private bool IsObservedHostileContact(",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityContact(",
        "AetheriaRuntimeDaemonRenderQueries.TryQueryEntityTarget("
    };
    var managerTargetQueryHits = forbiddenManagerTargetQuerySymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerTargetQueryHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns observed daemon target/contact query internals: " +
            string.Join(", ", managerTargetQueryHits));
    }

    if (!currentEntityBinder.Contains("public void RestoreBinding(", StringComparison.Ordinal) ||
        !currentEntityBinder.Contains("ObservedDocking.TryResolveDockingBay(currentEntity, out var dockParent, out _)", StringComparison.Ordinal) ||
        !currentEntityBinder.Contains("CurrentEntityPresentation?.BindDocked(", StringComparison.Ordinal) ||
        !currentEntityBinder.Contains("CurrentEntityPresentation?.BindUndocked(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity observer current-entity binding no longer flows through the dedicated current entity binder.");
    }

    var forbiddenObservedReadShims = new[]
    {
        "TryGetObservedGalaxy(out Galaxy galaxy)",
        "TryGetObservedZoneSnapshot(",
        "TryGetObservedRunZone("
    };

    var observedReadShimHits = forbiddenObservedReadShims
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (observedReadShimHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose broad observed galaxy/zone read shims; clients read typed projections through AetheriaClient: " +
            string.Join(", ", observedReadShimHits));
    }

    if (actionGameManager.Contains("public static Galaxy ObservedGalaxy", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose the raw observed galaxy projection as public gameplay state; keep it as a private renderer adapter behind typed client projections.");
    }

    if (actionGameManager.Contains("public static void ProjectObservedDaemonRun(", StringComparison.Ordinal) ||
        mainMenu.Contains("ActionGameManager.ProjectObservedDaemonRun(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon-run projection must not be routed through ActionGameManager; use the explicit observed-run projection holder.");
    }

    if (actionGameManager.Contains("public static bool IsTutorial", StringComparison.Ordinal) ||
        mainMenu.Contains("ActionGameManager.IsTutorial", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not mirror tutorial mode as public manager-global gameplay state; tutorial/run mode belongs to typed daemon run projections.");
    }

    if (actionGameManager.Contains("public static DirectoryInfo GameDataDirectory", StringComparison.Ordinal) ||
        actionGameManager.Contains("public static string RuntimeStateFilePath", StringComparison.Ordinal) ||
        mainMenu.Contains("ActionGameManager.GameDataDirectory", StringComparison.Ordinal) ||
        mainMenu.Contains("ActionGameManager.RuntimeStateFilePath", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity runtime path access must live in AetheriaUnityRuntimePaths, not ActionGameManager gameplay state.");
    }

    var volumeSamplingPath = Path.Combine(root, "Assets", "Scripts", "Zone Display", "VolumeSampling.cs");
    var volumeSampling = File.Exists(volumeSamplingPath)
        ? File.ReadAllText(volumeSamplingPath)
        : throw new InvalidOperationException("Cannot verify presentation environment ownership; VolumeSampling.cs is missing.");
    if (actionGameManager.Contains("CurrentEnvironment", StringComparison.Ordinal) ||
        volumeSampling.Contains("ActionGameManager.Instance?.CurrentEnvironment", StringComparison.Ordinal) ||
        volumeSampling.Contains("ActionGameManager.Instance.CurrentEnvironment", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionGameManager must not expose presentation environment as manager-global gameplay state; visual sampling should read presentation settings directly.");
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

    var compactMapRenderer = CompactSource(mapRenderer);
    var compactSectorRenderer = CompactSource(sectorRenderer);
    var compactSectorMap = CompactSource(sectorMap);
    if (!mapRenderer.Contains("AetheriaUnityRuntimeClientProvider.ResolveClient(", StringComparison.Ordinal) ||
        !compactMapRenderer.Contains(".State.Document<AetheriaRuntimeObjectsViewportDocument>(viewport).Reactive()", StringComparison.Ordinal) ||
        !compactMapRenderer.Contains(".State.Document<AetheriaRuntimeRenderSplatsViewportDocument>(viewport).Reactive()", StringComparison.Ordinal) ||
        !compactMapRenderer.Contains(".State.Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()", StringComparison.Ordinal) ||
        !sectorRenderer.Contains("AetheriaUnityRuntimeClientProvider.ResolveClient(", StringComparison.Ordinal) ||
        !compactSectorRenderer.Contains(".State.Document<AetheriaRuntimeSectorMapDocument>().Reactive()", StringComparison.Ordinal) ||
        !compactSectorRenderer.Contains(".State.Document<AetheriaRuntimeZoneDetailsDocument>(zoneIndex).Reactive()", StringComparison.Ordinal) ||
        !sectorRenderer.Contains(".Current", StringComparison.Ordinal) ||
        !compactSectorRenderer.Contains("ResolveClient().State.Document<AetheriaRuntimeCatalogSnapshot>().Reactive()", StringComparison.Ordinal) ||
        !compactSectorRenderer.Contains(".State.Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()", StringComparison.Ordinal) ||
        !sectorMap.Contains("AetheriaUnityRuntimeClientProvider.ResolveClient(", StringComparison.Ordinal) ||
        !compactSectorMap.Contains(".State.Document<AetheriaRuntimeSectorMapDocument>().Reactive()", StringComparison.Ordinal) ||
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

    RequireReactiveTypedDocumentAccess(
        sectorMap,
        "SectorMap",
        "AetheriaRuntimeSectorMapDocument",
        "_sectorMapDocument",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "AetheriaRuntimeSectorMapSession",
        ".ObserveSectorMap()");

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

    if (actionGameManager.Contains("EntityConstructionBlueprintMaterializer.InstantiateAuthoritativeFromBlueprint", StringComparison.Ordinal) ||
        actionGameManager.Contains("EntityConstructionBlueprintProjector.InstantiateAuthoritativeFromBlueprint", StringComparison.Ordinal) ||
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
        .Where(hit => hit.MethodName == "ResolveContext")
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
    var materializerPath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "EntityConstructionBlueprintMaterializer.cs");
    var materializerSource = File.Exists(materializerPath)
        ? File.ReadAllText(materializerPath)
        : throw new InvalidOperationException("Cannot verify Unity observer authority; EntityConstructionBlueprintMaterializer.cs is missing.");

    var requiredProjectionBoundarySymbols = new[]
    {
        "public static Galaxy ProjectObservedDaemonRun",
        "private Galaxy(",
        "AetheriaRuntimeRunCheckpointCommit run,",
        "public static class EntityConstructionBlueprintCapture",
        "public static EntityConstructionBlueprint Capture(Entity entity)",
        "public static class EntityConstructionBlueprintMaterializer",
        "public static Entity InstantiateAuthoritativeFromBlueprint",
        "public static Entity MaterializeObservedFromBlueprint",
        "private static Entity BuildFromBlueprint",
        "EntityConstructionBlueprintMaterializer.InstantiateAuthoritativeFromBlueprint(_itemManager, this, entityBlueprint)"
    };
    var projectionBoundaryCorpus = materializerSource + "\n" + zoneSource + "\n" + galaxy;
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

    var forbiddenManagerFacadeSymbols = new[]
    {
        "public static ActionGameManager Instance",
        "public Entity CurrentEntity",
        "public ItemManager ItemManager",
        "public Zone Zone",
        "public EntitySettings NewEntitySettings",
        "using Ink;",
        "using Ink.Runtime;",
        "private List<Story>",
        "private readonly (float2 direction, string name)[] _directions",
        "private static float Unlerp(",
        "DeathPostTransitionTime",
        "HypothermiaPost",
        "SevereHypothermiaPost",
        "WormholeCamera",
        "public SectorMap SectorMap",
        "public EventLog EventLog",
        "public void EnablePlayerInput()",
        "public void DisablePlayerInput()"
    };
    var managerFacadeHits = forbiddenManagerFacadeSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerFacadeHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still exposes Unity gameplay facade state as public API: " +
            string.Join(", ", managerFacadeHits));
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
        "public sealed class AetheriaUnityPilotCommandSender",
        "_lastSentDaemonMoveVector",
        "_lastSentDaemonLookDirection",
        "_lastSentDaemonTractorPower",
        "_hasSentDaemonMoveVector",
        "_hasSentDaemonLookDirection",
        "_hasSentDaemonTractorPower",
        "Func<AetheriaClient> _resolveClient",
        "Action<AetheriaControl> submit",
        "submit(client.Control)",
        "Failed to send Aetheria daemon pilot {label} operation",
        "operations => operations.SetMoveVector",
        "operations => operations.SetLookDirection",
        "operations => operations.SetTractorPower"
    };
    var missingSentOperationSymbols = requiredSentOperationSymbols
        .Where(symbol => !pilotCommandSender.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSentOperationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity pilot command sender no longer describes daemon input as sent typed operations: " +
            string.Join(", ", missingSentOperationSymbols));
    }

    var forbiddenSentOperationSymbols = new[]
    {
        "Func<AetheriaDaemonObserver> _resolveObserver",
        "Action<AetheriaRuntimeDaemonOperationsClient> submit",
        "submit(observer.Operations)"
    };
    var sentOperationHits = forbiddenSentOperationSymbols
        .Where(symbol => pilotCommandSender.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (sentOperationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity pilot command sender still routes typed input through the daemon observer operation escape hatch: " +
            string.Join(", ", sentOperationHits));
    }

    var forbiddenManagerPilotCommandSymbols = new[]
    {
        "private bool TrySubmitPilotOperation(",
        "_lastSentDaemonMoveVector",
        "_lastSentDaemonLookDirection",
        "_lastSentDaemonTractorPower",
        "_hasSentDaemonMoveVector",
        "_hasSentDaemonLookDirection",
        "_hasSentDaemonTractorPower",
        "DaemonMoveCommandIntervalSeconds",
        "DaemonLookCommandIntervalSeconds",
        "DaemonTractorCommandIntervalSeconds"
    };
    var managerPilotCommandHits = forbiddenManagerPilotCommandSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerPilotCommandHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still owns pilot command transport/throttling state instead of delegating to AetheriaUnityPilotCommandSender: " +
            string.Join(", ", managerPilotCommandHits));
    }

    var requiredDockingLoweringSymbols = new[]
    {
        "private AetheriaUnityPilotOperationController PilotOperationController =>",
        "PilotOperationController = PilotOperationController",
        "public sealed class AetheriaUnityPilotOperationController",
        "operations => operations.DockNearest()",
        "operations => operations.Undock()",
        "operations => operations.Interact()"
    };

    var dockingLoweringSource = actionGameManager + "\n" + pilotOperationController;
    var missingDockingLoweringSymbols = requiredDockingLoweringSymbols
        .Where(symbol => !dockingLoweringSource.Contains(symbol, StringComparison.Ordinal))
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
        "public ActionGameManager GameManager",
        "ActionGameManager GameManager",
        "private void RequestDock()",
        "private void RequestUndock()",
        "private void RequestInteract()",
        "private void RequestTowToStation()",
        "PilotOperationController.RequestTowToStation(_towingStation)",
        "public void RequestTowToStation(Entity towingStation)",
        "_towingStation",
        "UpdateTowingStation",
        "ResolveObservedEntityZoneIndex(_towingStation)",
        "_towingStation.CultPositionXZ.x",
        "_towingStation.CultPositionXZ.y",
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
        "internal AetheriaRuntimeDaemonCommandEnvelope DockNearest(",
        "internal AetheriaRuntimeDaemonCommandEnvelope Interact(",
        "ApplyDockNearestIntent(run, command, context.Intents, context.DockingDistance)",
        "context.WormholeExitRadius",
        "AetheriaRuntimeDaemonOperationContext.DefaultDockingDistance",
        "AetheriaRuntimeDaemonOperationContext.DefaultWormholeExitRadius",
        "ResolveInteractionDistance(command.ScalarValue, defaultDockingDistance)",
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

    var unityOwnedInteractionTuningHits = FindMethodScopedLineHits(
            actionGameManager,
            new[] { "Settings.GameplaySettings.DockingDistance", "Settings.GameplaySettings.WormholeExitRadius" })
        .Where(hit => hit.MethodName == "TryRequestDaemonDock" || hit.MethodName == "TryRequestDaemonInteract")
        .Select(hit => $"ActionGameManager.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (unityOwnedInteractionTuningHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity interaction requests still carry GameSettings tuning instead of semantic daemon operations: " +
            string.Join("; ", unityOwnedInteractionTuningHits));
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
        "PilotOperationController.RequestShieldToggle()",
        "operations => operations.ToggleShieldEnabled()",
        "AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled",
        "ApplyToggleEquipmentBehaviorItem(run, command, \"Shield\""
    };
    var gameplayInputShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayInputShell.cs");
    var gameplayInputShell = File.Exists(gameplayInputShellPath)
        ? File.ReadAllText(gameplayInputShellPath)
        : throw new InvalidOperationException("Cannot verify Unity shield input lowering; AetheriaUnityGameplayInputShell.cs is missing.");
    var unityPilotOperationSources = actionGameManager + "\n" + pilotOperationController + "\n" + gameplayInputShell;
    var missingShieldToggleSymbols = requiredShieldToggleSymbols
        .Where(symbol =>
            !unityPilotOperationSources.Contains(symbol, StringComparison.Ordinal) &&
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
        "operations => operations.TargetNearest()",
        "operations => operations.TargetNext()",
        "operations => operations.TargetPrevious()",
        "operations => operations.TargetReticle("
    };
    var unityTargetRequestSources = actionGameManager + "\n" + pilotOperationController + "\n" + daemonOperations;
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
        "out var stationZoneIndex",
        "out var station",
        "var destinationX = station.PositionX;",
        "var destinationY = station.PositionZ;",
        "MoveEntityToZone(run, actor, stationZoneIndex, destinationX, destinationY, out var movedEntityKey)",
        "intents.Towing.Add",
        "AetheriaRuntimeDaemonTowIntent",
        "public CultMeshOperationReceipt TowToStation(string stationEntityKey)"
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
        "ResolveSectorMap(stateBoot)",
        ".State",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "sectorMap.FrameId",
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
        "BuildPublications",
        "AetheriaRuntimeDaemonOperations.Execute("
    };

    var missingDaemonTickSymbols = requiredDaemonTickSymbols
        .Where(symbol => !daemonTickRunner.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingDaemonTickSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared simulation ticks no longer produce daemon-owned managed frame documents: " +
            string.Join(", ", missingDaemonTickSymbols));
    }

    if (daemonTickRunner.Contains("AetheriaRuntimeDaemonFrameStore.PublishFrame", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared simulation ticks still write legacy frame witness files instead of returning managed typed documents.");
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
    var laserPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "Laser.cs");
    var laserManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "LaserManager.cs");
    var constantLaserPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "ConstantLaser.cs");
    var constantLaserManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "ConstantLaserManager.cs");
    var guidedProjectilePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "GuidedProjectile.cs");
    var guidedProjectileManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons", "GuidedProjectileManager.cs");
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
            "TryBuildDaemonWorld(",
            "private const string DaemonEntityBodyPrefix = \"aetheria.daemon.entity.\"",
            "id = DaemonEntityBodyPrefix + daemonEntityIndex",
            "TryResolveDaemonEntityHull(zoneRenderer, hit.bodyId, out var hull)",
            "TryResolveTargetDaemonHull(target, hit.bodyId, out var hull)",
            "TryResolveTargetDaemonHull(projectile.TargetInstance, otherBody, out var hull)",
            "TargetDaemonEntityIndex(",
            "onlyDaemonEntityIndex.HasValue && onlyDaemonEntityIndex.Value != daemonEntityIndex",
            "TryParseDaemonEntityBodyId("
        },
        [projectilePath] = new[]
        {
            "AetheriaYmirPhysicsBridge.Instance.TryStepProjectile",
            "projectile killed instead of falling back to Unity physics."
        },
        [laserPath] = new[]
        {
            "public ZoneRenderer ZoneRenderer { get; set; }",
            "AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls",
            "ZoneRenderer,"
        },
        [laserManagerPath] = new[]
        {
            "p.ZoneRenderer = source.ZoneRenderer"
        },
        [constantLaserPath] = new[]
        {
            "public ZoneRenderer ZoneRenderer { get; set; }",
            "AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls",
            "ZoneRenderer,"
        },
        [constantLaserManagerPath] = new[]
        {
            "p.ZoneRenderer = source.ZoneRenderer"
        },
        [guidedProjectilePath] = new[]
        {
            "public ZoneRenderer ZoneRenderer { get; set; }",
            "AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls",
            "ZoneRenderer,",
            "child.ZoneRenderer = ZoneRenderer"
        },
        [guidedProjectileManagerPath] = new[]
        {
            "p.ZoneRenderer = source.ZoneRenderer"
        },
        [hitscanPath] = new[]
        {
            "public ZoneRenderer ZoneRenderer { get; set; }",
            "AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls",
            "ZoneRenderer,"
        },
        [hitscanManagerPath] = new[]
        {
            "p.ZoneRenderer = source.ZoneRenderer"
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

    var ymirBridge = File.ReadAllText(ymirBridgePath);
    if (ymirBridge.Contains("BuildZoneWorld(", StringComparison.Ordinal) ||
        ymirBridge.Contains("BuildTargetWorld(", StringComparison.Ordinal) ||
        ymirBridge.Contains("_zoneBodies", StringComparison.Ordinal) ||
        ymirBridge.Contains("_targetBodies", StringComparison.Ordinal) ||
        ymirBridge.Contains("TargetBodyPrefix", StringComparison.Ordinal) ||
        ymirBridge.Contains("bodyMap[bodyId] = hull", StringComparison.Ordinal) ||
        ymirBridge.Contains("TargetRadiusPadding", StringComparison.Ordinal) ||
        ymirBridge.Contains("view.HasEntityIndex ? view.EntityIndex[i] : i", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Ymir gameplay target/zone queries must be built from daemon SOA entity bodies with explicit daemon entity ids, not Unity HullCollider presentation geometry or row-order fallbacks.");
    }

    if (!ymirBridge.Contains("!view.IsCreated || !view.HasEntityIndex || !view.HasPhysicsRadius", StringComparison.Ordinal) ||
        !ymirBridge.Contains("var daemonEntityIndex = view.EntityIndex[i];", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Ymir daemon body construction must require the daemon EntityIndex SoA column and derive body ids from that column.");
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

    var weaponEffectRoot = Path.Combine(root, "Assets", "Scripts", "Gameplay", "Weapons");
    var weaponSingletonHits = Directory.EnumerateFiles(weaponEffectRoot, "*.cs", SearchOption.TopDirectoryOnly)
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => line.Line.Contains("ActionGameManager.Instance", StringComparison.Ordinal))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();

    if (weaponSingletonHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity weapon effects must receive renderer/input context explicitly instead of reaching through ActionGameManager.Instance: " +
            string.Join("; ", weaponSingletonHits));
    }
}

static async Task RequireTradeValuePolicyEveCommandPersistsAsync()
{
    var tempStatePath = Path.Combine(
        Path.GetTempPath(),
        $"aetheria-trade-policy-command-{Guid.NewGuid():N}.cc");

    try
    {
        await using var commandNode = await AetheriaStateNode.OpenAsync(
            tempStatePath,
            "aetheria-state-verify-trade-policy",
            enableDurableShardLogs: false);

        var policy = AetheriaRuntimeStateMapper.ToTradeValuePolicy(
            AetheriaRuntimeTradeValueSettings.Default,
            DateTimeOffset.UtcNow.ToString("O"));
        await commandNode.MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey)
            .ReplaceAsync(policy)
            .ConfigureAwait(false);
        var originalMinimum = policy.QualityPriceModifier?.Minimum ?? 0;
        var editedMinimum = originalMinimum + 0.125;

        var command = AetheriaRuntimeEveCommandClient.ToDocument(
            AetheriaRuntimeEveCommandClient.CreateTradeValuePolicyCommand(
                AetheriaRuntimeEveCommandKind.SetTradeValueQualityMinimum,
                new AetheriaRuntimeTradeValuePolicyCommandBody
                {
                    Value = editedMinimum
                },
                "aetheria-state-verify"));
        await commandNode.SubmitEveCommandAsync(command).ConfigureAwait(false);

        var report = await AetheriaEveCommandBridge.AcceptObservedAsync(commandNode).ConfigureAwait(false);
        if (report.AcceptedTradeValuePolicyCommands != 1)
        {
            throw new InvalidOperationException(
                "Trade value policy Eve command was not accepted by the typed command bridge.");
        }

        var editedPolicy = await commandNode.MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey).ReadAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Trade value policy disappeared after typed Eve command.");
        var actualMinimum = editedPolicy.QualityPriceModifier?.Minimum ?? 0;
        if (Math.Abs(actualMinimum - editedMinimum) > 0.000001)
        {
            throw new InvalidOperationException(
                $"Trade value policy Eve command did not persist the typed edit. Expected {editedMinimum}, got {actualMinimum}.");
        }
    }
    finally
    {
        try
        {
            File.Delete(tempStatePath);
        }
        catch
        {
            // Best-effort cleanup for verifier scratch state.
        }
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
    var runtimeStateRefResolverPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateRefResolver.cs");
    var runtimeEveSurfaceAdapterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeEveSurfaceAdapter.cs");
    var unityPackageProjectPath = Path.Combine(root, "GameCult.Aetheria.State.Unity.csproj");
    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var gameplayBootShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayBootShell.cs");
    var mainMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "MainMenu.cs");
    var eveSurfacePresenterPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveSurfacePresenter.cs");
    var eveUnitySurfaceHostPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.eve-runtime", "Runtime", "AetheriaEveUnitySurfaceHost.cs");

    var requiredPaths = new[]
    {
        runtimeStateRefResolverPath,
        runtimeEveSurfaceAdapterPath,
        unityPackageProjectPath,
        actionGameManagerPath,
        gameplayBootShellPath,
        mainMenuPath,
        eveSurfacePresenterPath,
        eveUnitySurfaceHostPath
    };

    var missingPaths = requiredPaths
        .Where(path => !File.Exists(path))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();

    if (missingPaths.Length > 0)
    {
        throw new InvalidOperationException(
            "Verse client state authority cannot be verified because required files are missing: " +
            string.Join(", ", missingPaths));
    }

    var runtimeStateRefResolver = File.ReadAllText(runtimeStateRefResolverPath);
    var runtimeEveSurfaceAdapter = File.ReadAllText(runtimeEveSurfaceAdapterPath);
    var unityPackageProject = File.ReadAllText(unityPackageProjectPath);
    var actionGameManager = File.ReadAllText(actionGameManagerPath);
    var gameplayBootShell = File.ReadAllText(gameplayBootShellPath);
    var mainMenu = File.ReadAllText(mainMenuPath);
    var eveSurfacePresenter = File.ReadAllText(eveSurfacePresenterPath);
    var eveUnitySurfaceHost = File.ReadAllText(eveUnitySurfaceHostPath);

    var requiredResolverSymbols = new[]
    {
        "public static class AetheriaRuntimeStateRefResolver",
        "TryResolveDaemonStateRef",
        "TryResolveDaemonItemStatRef",
        "AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix",
        "AetheriaRuntimeDaemonStateRefs.CurrentEntityName",
        "public static CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver(",
        "Func<AetheriaRuntimeDaemonFrameDocument?> frameProvider",
        "Func<AetheriaRuntimeDaemonHealthDocument?> healthProvider",
        "Func<AetheriaRuntimeDaemonCommandBoundaryDocument?> commandBoundaryProvider",
        "frameProvider?.Invoke()",
        "catalogProvider?.Invoke()",
        "CultMesh.StateRefResolver(",
        "FindDaemonItem("
    };

    var missingResolverSymbols = requiredResolverSymbols
        .Where(symbol => !runtimeStateRefResolver.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingResolverSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Shared runtime state-ref resolver is incomplete: " +
            string.Join(", ", missingResolverSymbols));
    }

    if (runtimeStateRefResolver.Contains("public static class AetheriaRuntimeStateReader", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state reader compatibility wrapper still exists; use AetheriaRuntimeStateRefResolver directly.");
    }

    if (runtimeStateRefResolver.Contains("ReadEntitySnapshots", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state-ref resolver still exposes file-backed entity snapshot reads; use daemon zone-render EntitySnapshots documents.");
    }

    if (runtimeStateRefResolver.Contains("TryReadDaemonRenderView", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryReadDaemonSoaView(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryReadDaemonFrame(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("ResolveEveSurfaceStateRef(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryResolveEveSurfaceStateRef(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("CreateEveSurfaceCultMeshStateRefResolver(\r\n            string stateFilePath", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("CreateEveSurfaceCultMeshStateRefResolver(\n            string stateFilePath", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("ReadEveSurface(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryReadDaemonGameSurface(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryReadDaemonGameTuiSurface(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryReadDaemonEditorSurface(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("TryReadDaemonEditorTuiSurface(", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("OpenRuntimeCatalog(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state-ref resolver still exposes daemon file reads; use AetheriaClient and managed typed documents.");
    }

    if (runtimeStateRefResolver.Contains("ReadRunStates", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("ReadZoneStates", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state-ref resolver still exposes file-backed run/zone snapshot reads; use managed daemon frame and zone-render documents.");
    }

    if (runtimeStateRefResolver.Contains("ReadPlayerSettings", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("ReadVerseHostSettings", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("ReadLoadoutTemplates", StringComparison.Ordinal) ||
        runtimeStateRefResolver.Contains("ReadTradeValuePolicy", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime state-ref resolver still exposes file-backed catalog/settings reads; use AetheriaClient managed document handles.");
    }

    var runtimeCatalogStorePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeCatalogStore.cs");
    var runtimeCatalogStore = File.Exists(runtimeCatalogStorePath)
        ? File.ReadAllText(runtimeCatalogStorePath)
        : throw new InvalidOperationException("Cannot verify deleted file-backed runtime catalog readers; AetheriaRuntimeCatalogStore.cs is missing.");
    if (runtimeCatalogStore.Contains("ReadEntitySnapshots", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ReadEntitySnapshotPayload", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("EntitySnapshotSchema", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Runtime catalog store still exposes file-backed entity snapshot projection; daemon zone-render EntitySnapshots owns runtime entity lowering.");
    }

    if (runtimeCatalogStore.Contains("ReadRunStates", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ReadZoneStates", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ReadRunStatePayload", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ReadZoneStatePayload", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("RunStateSchema", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ZoneStateSchema", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Runtime catalog store still exposes file-backed run/zone snapshot projection; managed daemon checkpoint and zone-render documents own runtime state lowering.");
    }

    if (runtimeCatalogStore.Contains("ReadEveSurfaces", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ReadEveSurface(", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ProjectStatRecipeSurfaceDocument", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ProjectTradeValuePolicySurfaceDocument", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("EveSurfaceSchema", StringComparison.Ordinal) ||
        runtimeCatalogStore.Contains("ReadFieldEveSurface", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Runtime catalog store still exposes Eve-surface parsing/projection helpers; surface access must flow through managed EveSurfaceState documents.");
    }

    if (!unityPackageProject.Contains("AetheriaRuntimeStateRefResolver.cs", StringComparison.Ordinal) ||
        unityPackageProject.Contains("AetheriaRuntimeStateReader.cs", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "GameCult.Aetheria.State.Unity.csproj must include AetheriaRuntimeStateRefResolver.cs and not the old state reader filename.");
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
        "CultMeshStateRefResolver? stateRefResolver",
        "ResolvePropRefs(props, resolveStateRef)",
        "ResolvePropRef(props, AetheriaRuntimeSurfaceStateRefs.Source, \"value\", resolveStateRef)",
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
    var observedFrameApplierPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedFrameApplier.cs");
    var observedFrameApplier = File.Exists(observedFrameApplierPath)
        ? File.ReadAllText(observedFrameApplierPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaUnityObservedFrameApplier.cs is missing.");
    var runtimeClientProviderPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityRuntimeClientProvider.cs");
    var runtimeClientProvider = File.Exists(runtimeClientProviderPath)
        ? File.ReadAllText(runtimeClientProviderPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaUnityRuntimeClientProvider.cs is missing.");
    var zoneRendererPath = Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs");
    var zoneRenderer = File.Exists(zoneRendererPath)
        ? File.ReadAllText(zoneRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; ZoneRenderer.cs is missing.");
    var volumeCloudRendererPath = Path.Combine(root, "Assets", "Scripts", "Zone Display", "VolumeCloudRenderer.cs");
    var volumeCloudRenderer = File.Exists(volumeCloudRendererPath)
        ? File.ReadAllText(volumeCloudRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; VolumeCloudRenderer.cs is missing.");
    var renderSplatViewportSourcePath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityRenderSplatViewportSource.cs");
    var renderSplatViewportSource = File.Exists(renderSplatViewportSourcePath)
        ? File.ReadAllText(renderSplatViewportSourcePath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaUnityRenderSplatViewportSource.cs is missing.");
    var schematicDisplayPath = Path.Combine(root, "Assets", "Scripts", "UI", "HUD", "SchematicDisplay.cs");
    var schematicDisplay = File.Exists(schematicDisplayPath)
        ? File.ReadAllText(schematicDisplayPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; SchematicDisplay.cs is missing.");
    var menuPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MenuPanel.cs");
    var menuPanel = File.Exists(menuPanelPath)
        ? File.ReadAllText(menuPanelPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; MenuPanel.cs is missing.");
    var mapRendererPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "MapRenderer.cs");
    var mapRenderer = File.Exists(mapRendererPath)
        ? File.ReadAllText(mapRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; MapRenderer.cs is missing.");
    var sectorMapPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorMap.cs");
    var sectorMap = File.Exists(sectorMapPath)
        ? File.ReadAllText(sectorMapPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; SectorMap.cs is missing.");
    var localMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "LocalMenu.cs");
    var localMenu = File.Exists(localMenuPath)
        ? File.ReadAllText(localMenuPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; LocalMenu.cs is missing.");
    var sectorRendererPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "SectorRenderer.cs");
    var sectorRenderer = File.Exists(sectorRendererPath)
        ? File.ReadAllText(sectorRendererPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; SectorRenderer.cs is missing.");
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var tradeMenu = File.Exists(tradeMenuPath)
        ? File.ReadAllText(tradeMenuPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; TradeMenu.cs is missing.");
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; InventoryMenu.cs is missing.");
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; InventoryPanel.cs is missing.");
    var inputDisplayLayoutPath = Path.Combine(root, "Assets", "Scripts", "UI", "InputScreen", "InputDisplayLayout.cs");
    var inputDisplayLayout = File.Exists(inputDisplayLayoutPath)
        ? File.ReadAllText(inputDisplayLayoutPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; InputDisplayLayout.cs is missing.");
    var aetheriaClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaClient.cs");
    var aetheriaClient = File.Exists(aetheriaClientPath)
        ? File.ReadAllText(aetheriaClientPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaClient.cs is missing.");
    var aetheriaClientStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaClientState.cs");
    var aetheriaClientState = File.Exists(aetheriaClientStatePath)
        ? File.ReadAllText(aetheriaClientStatePath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaClientState.cs is missing.");
    var observedDaemonStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonRenderView.cs");
    var observedDaemonState = File.Exists(observedDaemonStatePath)
        ? File.ReadAllText(observedDaemonStatePath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; AetheriaRuntimeDaemonRenderView.cs is missing.");
    var daemonRuntimeDocumentTestsPath = Path.Combine(root, "Assets", "Scripts", "Tests", "DaemonRuntimeDocumentTests.cs");
    var daemonRuntimeDocumentTests = File.Exists(daemonRuntimeDocumentTestsPath)
        ? File.ReadAllText(daemonRuntimeDocumentTestsPath)
        : throw new InvalidOperationException("Cannot verify daemon state acquisition; DaemonRuntimeDocumentTests.cs is missing.");

    var requiredActionGameManagerSymbols = new[]
    {
        "private AetheriaUnityGameplayBootShell GameplayBootShell =>",
        "var boot = GameplayBootShell.Boot();",
        "SceneWiring.ConfigureCurrentEntityPresentation(_currentEntityPresentation, boot.RuntimeCatalog);",
        "SceneWiring.ConfigureTargetPresentation(",
        "SceneWiring.ConfigureActionBarPresentation(",
        "ItemManager = boot.ItemManager;",
        "_loadoutItemFactory = boot.LoadoutItemFactory;",
        "AetheriaUnityRuntimeClientProvider.PlayerSettings",
        "ResolveDaemonObserver()",
        "private AetheriaUnityObservedFrameApplier ObservedFrameApplier =>"
    };

    var missingActionGameManagerSymbols = requiredActionGameManagerSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingActionGameManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager does not route typed state reads through the shared Verse client and Unity boot shell: " +
            string.Join(", ", missingActionGameManagerSymbols));
    }

    var requiredGameplayBootShellSymbols = new[]
    {
        "public sealed class AetheriaUnityGameplayBootShell",
        "public AetheriaUnityGameplayBootResult Boot()",
        "AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory)",
        "AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot.StateFilePath, stateBoot.RuntimeId)",
        ".State",
        ".Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "runtimeCatalogDocument.Current",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "sectorMapDocument.Current",
        "new ItemManager(",
        "new AetheriaUnityLoadoutItemFactory(itemManager, runtimeCatalog)",
        "ZoneRenderer.SetDroppedPickupItemFactory(loadoutItemFactory.CreateLoadoutItem)",
        "ZoneRenderer.BodySettingsCollections = Settings.BodySettingsCollections",
        "AetheriaUnityRenderSettingsBridge.Build(",
        "CockpitHudShell.SetRenderSettings(ZoneRenderer.RenderSettings)",
        "AetheriaUnityRuntimeClientProvider.PlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap",
        "public readonly struct AetheriaUnityGameplayBootResult"
    };

    var missingGameplayBootShellSymbols = requiredGameplayBootShellSymbols
        .Where(symbol => !gameplayBootShell.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingGameplayBootShellSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay boot shell no longer owns typed runtime boot/catalog/render setup: " +
            string.Join(", ", missingGameplayBootShellSymbols));
    }

    if (gameplayBootShell.Contains(".ObserveCatalog()", StringComparison.Ordinal) ||
        gameplayBootShell.Contains(".ObserveSectorMap()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplayBootShell still routes boot catalog/sector-map reads through legacy session wrappers instead of reactive typed documents.");
    }

    var requiredRuntimeClientProviderSymbols = new[]
    {
        "public static class AetheriaUnityRuntimeClientProvider",
        "private static CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettingsDocument",
        "public static RuntimePlayerSettings PlayerSettings",
        "public static AetheriaClient ResolveClient(string stateFilePath, string runtimeId = \"\")",
        "public static AetheriaClient ResolveClient(AetheriaRuntimeStateBootReport stateBoot, string runtimeId = \"\")",
        "public static AetheriaClient CurrentClientForStateFile(string stateFilePath)",
        "private static readonly Dictionary<string, AetheriaClient> RuntimeClients",
        "RuntimeClients.TryGetValue(cacheKey, out var runtimeClient)",
        "RuntimeClients[cacheKey] = runtimeClient",
        "AetheriaClient",
        ".State",
        ".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()",
        "_playerSettingsDocument.Current",
        "_playerSettingsDocument?.Dispose()",
        "OpenAsync(",
        "pullOnOpen: true",
        "ApplyPlayerSettings(settings, stored)"
    };
    var missingRuntimeClientProviderSymbols = requiredRuntimeClientProviderSymbols
        .Where(symbol => !runtimeClientProvider.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingRuntimeClientProviderSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime client/player-settings boot must live behind AetheriaUnityRuntimeClientProvider: " +
            string.Join(", ", missingRuntimeClientProviderSymbols));
    }

    if (runtimeClientProvider.Contains("AetheriaRuntimePlayerSettingsSession _playerSettingsDocument", StringComparison.Ordinal) ||
        runtimeClientProvider.Contains(".ObservePlayer()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaUnityRuntimeClientProvider still routes player settings through a legacy session wrapper instead of the reactive typed document.");
    }

    var providerOwnedClientAccessSources = new Dictionary<string, string>
    {
        ["Packages/org.gamecult.aetheria.eve-runtime/Runtime/AetheriaEveSurfacePresenter.cs"] = eveSurfacePresenter,
        ["Assets/Scripts/Zone Display/ZoneRenderer.cs"] = zoneRenderer,
        ["Assets/Scripts/Zone Display/VolumeCloudRenderer.cs"] = volumeCloudRenderer,
        ["Assets/Scripts/Gameplay/AetheriaUnityRenderSplatViewportSource.cs"] = renderSplatViewportSource,
        ["Assets/Scripts/UI/Menu/MenuPanel.cs"] = menuPanel,
        ["Assets/Scripts/UI/Menu/MapRenderer.cs"] = mapRenderer,
        ["Assets/Scripts/UI/Menu/SectorMap.cs"] = sectorMap,
        ["Assets/Scripts/UI/Menu/LocalMenu.cs"] = localMenu,
        ["Assets/Scripts/UI/Menu/SectorRenderer.cs"] = sectorRenderer,
        ["Assets/Scripts/UI/HUD/SchematicDisplay.cs"] = schematicDisplay,
        ["Assets/Scripts/UI/Menu/TradeMenu.cs"] = tradeMenu,
        ["Assets/Scripts/UI/Menu/InventoryMenu.cs"] = inventoryMenu,
        ["Assets/Scripts/UI/Menu/InventoryPanel.cs"] = inventoryPanel,
        ["Assets/Scripts/UI/MainMenu.cs"] = mainMenu,
        ["Assets/Scripts/UI/InputScreen/InputDisplayLayout.cs"] = inputDisplayLayout,
        ["Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs"] = daemonObserver
    };
    var directClientOpenHits = providerOwnedClientAccessSources
        .Where(pair =>
            pair.Value.Contains(".OpenAsync(", StringComparison.Ordinal) ||
            pair.Value.Contains(".OpenLocalAsync(", StringComparison.Ordinal))
        .Select(pair => pair.Key)
        .ToArray();
    if (directClientOpenHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime readers must acquire managed typed state through AetheriaUnityRuntimeClientProvider instead of opening local clients: " +
            string.Join(", ", directClientOpenHits));
    }

    var missingProviderClientAccess = providerOwnedClientAccessSources
        .Where(pair => !pair.Value.Contains("AetheriaUnityRuntimeClientProvider.ResolveClient(", StringComparison.Ordinal))
        .Select(pair => pair.Key)
        .ToArray();
    if (missingProviderClientAccess.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity runtime readers no longer use the shared managed runtime client provider: " +
            string.Join(", ", missingProviderClientAccess));
    }

    if (!observedFrameApplier.Contains("private AetheriaRuntimeZoneRenderDocument _lastZoneRender;", StringComparison.Ordinal) ||
        !observedFrameApplier.Contains("ApplyLatestZoneRender()", StringComparison.Ordinal) ||
        !observedFrameApplier.Contains("observer.LastRenderView?.ZoneRender", StringComparison.Ordinal) ||
        !observedDaemonState.Contains("public AetheriaRuntimeZoneRenderDocument ZoneRender { get; }", StringComparison.Ordinal) ||
        !observedDaemonState.Contains("CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender", StringComparison.Ordinal) ||
        !observedDaemonState.Contains("var currentZoneRender = zoneRender?.Current;", StringComparison.Ordinal) ||
        !observedDaemonState.Contains("new AetheriaRuntimeDaemonRenderView(currentFrame, currentSoaView, currentZoneRender)", StringComparison.Ordinal) ||
        !daemonObserver.Contains("private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _daemonFrame;", StringComparison.Ordinal) ||
        !daemonObserver.Contains("private CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> _daemonSoaView;", StringComparison.Ordinal) ||
        !daemonObserver.Contains("private CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> _zoneRender;", StringComparison.Ordinal) ||
        !daemonObserver.Contains("AetheriaRuntimeDaemonRenderView.TryCreateCurrent(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Observed zone-render state acquisition must sample managed typed zone-render documents directly.");
    }

    var requiredDaemonStateFacadeSymbols = new[]
    {
        "public CultMeshDocumentHandle<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeDaemonHealthDocument> Health { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeVerseAuthorityPolicyDocument> AuthorityPolicy { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeDaemonFrameDocument> LatestFrame { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView { get; }",
        "public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameSurface { get; }",
        "public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> GameTuiSurface { get; }",
        "public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorSurface { get; }",
        "public CultMeshDocumentHandle<global::Aetheria.State.Documents.EveSurfaceState> EditorTuiSurface { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeCatalogSnapshot> Catalog { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimePlayerSettingsDocument> PlayerSettings { get; }",
        "public CultMeshDocumentHandle<AetheriaRuntimeVerseHostSettingsDocument> VerseHostSettings { get; }"
    };
    var missingDaemonStateFacadeSymbols = requiredDaemonStateFacadeSymbols
        .Where(symbol => !aetheriaClientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingDaemonStateFacadeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose daemon service state as managed typed document handles: " +
            string.Join(", ", missingDaemonStateFacadeSymbols));
    }

    var forbiddenDaemonStateFacadeSymbols = new[]
    {
        "public AetheriaClientDaemonState Daemon { get; }",
        "public sealed class AetheriaClientDaemonState",
        "public AetheriaClientSettingsState Settings { get; }",
        "public sealed class AetheriaClientSettingsState"
    };
    var survivingDaemonStateFacadeSymbols = forbiddenDaemonStateFacadeSymbols
        .Where(symbol => aetheriaClientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingDaemonStateFacadeSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState still exposes daemon/settings facade wrappers instead of flat managed typed document handles: " +
            string.Join(", ", survivingDaemonStateFacadeSymbols));
    }

    var requiredManagedClientConnectionSymbols = new[]
    {
        "public AetheriaClientState State => _state;",
        "var frame = CurrentDaemonFrame();",
        "private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument>? _daemonFrame;",
        "_daemonFrame ??= State.Document<AetheriaRuntimeDaemonFrameDocument>().Reactive();"
    };
    var missingManagedClientConnectionSymbols = requiredManagedClientConnectionSymbols
        .Where(symbol => !aetheriaClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingManagedClientConnectionSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClient must expose the managed state object without owning document-catalog shortcuts: " +
            string.Join(", ", missingManagedClientConnectionSymbols));
    }

    if (aetheriaClient.Contains("State.ReactiveDaemonFrame()", StringComparison.Ordinal) ||
        aetheriaClient.Contains("TryReactiveDaemonSoaView()", StringComparison.Ordinal) ||
        aetheriaClient.Contains("State.ReactiveZoneRender()", StringComparison.Ordinal) ||
        aetheriaClient.Contains("CurrentObservedDaemon()", StringComparison.Ordinal) ||
        aetheriaClient.Contains("State.CurrentObservedDaemon()", StringComparison.Ordinal) ||
        aetheriaClient.Contains("State.ReactiveObservedDaemon()", StringComparison.Ordinal) ||
        aetheriaClient.Contains("Aetheria() => State", StringComparison.Ordinal) ||
        aetheriaClient.Contains("AetheriaRuntimeDaemonRenderView.TryCreateCurrent(", StringComparison.Ordinal) ||
        aetheriaClient.Contains("AetheriaRuntimeDaemonRenderView?", StringComparison.Ordinal) ||
        aetheriaClient.Contains("observedState.TryCurrent(out var current)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClient command submission still samples observed daemon state through an aggregate reactive wrapper instead of direct managed typed documents.");
    }

    var requiredManagedStateAccessSymbols = new[]
    {
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>()",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaClientEveSurface surface)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(AetheriaRuntimeRtsViewportBounds viewport)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(int entityOrZoneIndex)",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>(string seatId)"
    };
    var missingManagedStateAccessSymbols = requiredManagedStateAccessSymbols
        .Where(symbol => !aetheriaClientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingManagedStateAccessSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState must expose direct typed document handles instead of AetheriaClient forwarding shortcuts or per-document session wrappers: " +
            string.Join(", ", missingManagedStateAccessSymbols));
    }

    if (aetheriaClientState.Contains("public Task<TDocument> LatestAsync<TDocument>", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("public TDocument Latest<TDocument>", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("public Observable<TDocument> Watch<TDocument>", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("public CultMeshReactiveDocument<TDocument> Reactive<TDocument>", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("DocumentBySchema(", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("TryGetDocumentBySchema(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState still exposes generic latest/reactive/watch or schema-string shortcuts instead of direct typed CultMesh document handles.");
    }

    if (aetheriaClientState.Contains("public AetheriaRuntimeDaemonRenderView? CurrentObservedDaemon(", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("AetheriaRuntimeDaemonRenderView.TryCreateCurrent(frame, soaView, zoneRender", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaClientState must not expose one-shot observed daemon aggregation; command owners should hold managed typed daemon documents.");
    }

    var requiredManagedCommandFrameSymbols = new[]
    {
        "internal AetheriaRuntimeDaemonFrameDocument? CurrentDaemonFrame()",
        "_daemonFrame ??= State.Document<AetheriaRuntimeDaemonFrameDocument>().Reactive();",
        "return _daemonFrame.Current;",
        "Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeDaemonFrameDocument?, AetheriaRuntimeDaemonCommandEnvelope> submit",
        "frame?.SessionId ?? _sessionId"
    };
    var missingManagedCommandFrameSymbols = requiredManagedCommandFrameSymbols
        .Where(symbol => !aetheriaClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingManagedCommandFrameSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClient command submission must sample only the managed daemon frame document instead of composing an observed render aggregate: " +
            string.Join(", ", missingManagedCommandFrameSymbols));
    }

    if (!daemonRuntimeDocumentTests.Contains("ManagedStateSamplesCurrentDaemonRenderViewFromReactiveDocuments()", StringComparison.Ordinal) ||
        !daemonRuntimeDocumentTests.Contains("using var observedFrame = client.State.Document<AetheriaRuntimeDaemonFrameDocument>().Reactive();", StringComparison.Ordinal) ||
        !daemonRuntimeDocumentTests.Contains("using var observedSoaView = client.State.Document<AetheriaRuntimeDaemonSoaViewDocument>().Reactive();", StringComparison.Ordinal) ||
        !daemonRuntimeDocumentTests.Contains("using var observedZoneRender = client.State.Document<AetheriaRuntimeZoneRenderDocument>().Reactive();", StringComparison.Ordinal) ||
        !daemonRuntimeDocumentTests.Contains("AetheriaRuntimeDaemonRenderView.TryCreateCurrent(", StringComparison.Ordinal) ||
        daemonRuntimeDocumentTests.Contains("client.State.CurrentObservedDaemon()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Daemon runtime document tests must teach observed daemon sampling through direct reactive daemon documents.");
    }

    var requiredObservedDaemonStateSymbols = new[]
    {
        "AetheriaRuntimeDaemonSoaViewIndex.Build(soaView)",
        "public static bool TryCreateCurrent(",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> frame",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? soaView",
        "CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender",
        "var currentFrame = frame?.Current;",
        "var currentZoneRender = zoneRender?.Current;",
        "new AetheriaRuntimeDaemonRenderView(currentFrame, currentSoaView, currentZoneRender)"
    };
    var missingObservedDaemonStateSymbols = requiredObservedDaemonStateSymbols
        .Where(symbol => !observedDaemonState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingObservedDaemonStateSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed daemon state must be composed from managed typed daemon documents: " +
            string.Join(", ", missingObservedDaemonStateSymbols));
    }

    if (observedDaemonState.Contains("AetheriaRuntimeDaemonFrameSession", StringComparison.Ordinal) ||
        observedDaemonState.Contains("AetheriaRuntimeDaemonSoaViewSession", StringComparison.Ordinal) ||
        observedDaemonState.Contains("AetheriaRuntimeZoneRenderSession", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("TryObserveDaemonSoaView(options)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Observed daemon state still composes legacy session wrappers instead of direct reactive CultMesh documents.");
    }

    var clientStateSessionWrapperSymbols = new[]
    {
        "Session Observe",
        "new AetheriaRuntimeCatalogSession",
        "new AetheriaRuntimeDaemonFrameSession",
        "new AetheriaRuntimeLoadoutTemplatesSession",
        "new AetheriaRuntimeSectorMapSession",
        "new AetheriaRuntimeZoneContactsSession",
        "new AetheriaRuntimeStationRefitSession",
        "new AetheriaRuntimeZoneRenderSession",
        "new AetheriaRuntimeCurrentZoneSession",
        "new AetheriaRuntimeCurrentEntitySession",
        "new AetheriaRuntimeCurrentDockingSession",
        "new AetheriaRuntimePlayerSettingsSession",
        "new AetheriaRuntimeVerseHostSettingsSession",
        "new AetheriaRuntimeObjectsViewportSession",
        "new AetheriaRuntimeRenderSplatsViewportSession",
        "new AetheriaRuntimeInventorySession",
        "new AetheriaRuntimeStarbridgeScenarioSession",
        "new AetheriaRuntimeStarbridgeRunSession",
        "new AetheriaRuntimeStarbridgeSummarySession",
        "new AetheriaRuntimeStarbridgePlayerSeatSession"
    };
    var clientStateSessionWrapperHits = clientStateSessionWrapperSymbols
        .Where(symbol => aetheriaClientState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (clientStateSessionWrapperHits.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState still exposes per-document Observe* session wrappers instead of direct Reactive* document access: " +
            string.Join(", ", clientStateSessionWrapperHits));
    }

    var publicObserveAccessorHits = System.Text.RegularExpressions.Regex
        .Matches(
            aetheriaClientState,
            @"public\s+[^{;=]+\s+Observe[A-Za-z0-9_]*\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant)
        .Select(match => match.Value.Trim())
        .ToArray();
    if (publicObserveAccessorHits.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClientState reintroduced public Observe* state accessors; expose direct Reactive* typed documents instead: " +
            string.Join(", ", publicObserveAccessorHits));
    }

    var runtimeStateSessionsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeStateSessions.cs");
    if (File.Exists(runtimeStateSessionsPath))
    {
        throw new InvalidOperationException(
            "AetheriaRuntimeStateSessions.cs still exists; per-document session wrappers must be deleted in favor of direct CultMesh reactive documents.");
    }

    var forbiddenObservedDaemonStateSymbols = new[]
    {
        "string statePath",
        "FramePath",
        "SoaViewPath",
        "AetheriaRuntimeDaemonFrameStore.GetFramePath",
        "SoaViewStore.GetViewPath",
        "state.LatestFrame.ReactiveAsync",
        "state.LatestSoaView.ReactiveAsync",
        "public static async Task<AetheriaRuntimeDaemonRenderView?> ReadAsync(",
        "LatestObservedDaemonAsync(",
        "LatestObservedDaemon()",
        "public sealed class AetheriaRuntimeReactiveObservedDaemonState",
        "ReactiveObservedDaemonAsync",
        "ReactiveObservedDaemon(",
        "TryCurrent(out AetheriaRuntimeDaemonRenderView? observed)",
        "return new AetheriaRuntimeDaemonRenderView(frame, soaView);",
        "AetheriaRuntimeRtsProjection.ProjectZoneRender(Frame)",
        "zoneRender = null"
    };
    var observedDaemonStateBypassHits = forbiddenObservedDaemonStateSymbols
        .Where(symbol => observedDaemonState.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (observedDaemonStateBypassHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Observed daemon state still projects managed daemon documents back into witness file paths: " +
            string.Join(", ", observedDaemonStateBypassHits));
    }

    var forbiddenDaemonClientBypassSymbols = new[]
    {
        ".ReadAsync(State)",
        "return _verse.GetObservedDaemonStateAsync();",
        "return _verse.GetLatestAuthoritativeRunFrameAsync();",
        "return _verse.GetHealthAsync();",
        "return _verse.GetVerseAuthorityPolicyAsync();",
        "return _verse.GetLatestSoaViewAsync();",
        "AetheriaRuntimeDaemonPublicationStore.TryReadVerseAuthorityPolicy",
        "return _verse.GetDaemonGameSurfaceAsync();",
        "return _verse.GetDaemonGameTuiSurfaceAsync();",
        "return _verse.GetDaemonEditorSurfaceAsync();",
        "return _verse.GetDaemonEditorTuiSurfaceAsync();",
        "public AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog()",
        "public async Task<AetheriaRuntimePlayerSettingsSnapshot?> PlayerSettingsAsync()",
        "public async Task<AetheriaRuntimeVerseHostSettingsSnapshot?> VerseHostSettingsAsync()",
        "public async Task<System.Collections.Generic.IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>> LoadoutTemplatesAsync()",
        "public async Task<AetheriaRuntimeDaemonRenderView?> ObserveAsync()",
        "public async Task<AetheriaRuntimeLoadoutTemplateCommit> LoadoutTemplateAsync(",
        "scenario ??= await _verse.GetStarbridgeScenarioAsync()",
        "session ??= await _verse.GetStarbridgeSessionAsync()",
        "var frame = await _verse.GetLatestFrameAsync()",
        "public async Task<AetheriaRuntimeDaemonFrameDocument?> LatestAuthoritativeRunFrameAsync()",
        "public async Task<AetheriaRuntimeRtsViewportDocument> MapViewportAsync(",
        "public async Task<AetheriaRuntimeObjectsViewportDocument> ObjectsViewportAsync(",
        "public async Task<AetheriaRuntimeGravityViewportDocument> GravityViewportAsync(",
        "public async Task<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplatsViewportAsync(",
        "public async Task<AetheriaRuntimeAssetManifestDocument> AssetManifestAsync()",
        "public async Task<AetheriaRuntimeCurrentZoneDocument> CurrentZoneAsync()",
        "public async Task<AetheriaRuntimeCurrentEntityDocument> CurrentEntityAsync()",
        "public async Task<AetheriaRuntimeCurrentDockingDocument> CurrentDockingAsync()",
        "public async Task<AetheriaRuntimeZoneContactsDocument> ZoneContactsAsync()",
        "public async Task<AetheriaRuntimeStationRefitDocument> StationRefitAsync()",
        "public async Task<AetheriaRuntimeSectorMapDocument> SectorMapAsync()",
        "public async Task<AetheriaRuntimeZoneDetailsDocument> ZoneDetailsAsync(",
        "public async Task<AetheriaRuntimeZoneRenderDocument> ZoneRenderAsync()",
        "public async Task<AetheriaRuntimeStarbridgeSessionSummaryDocument> StarbridgeSessionSummaryAsync(",
        "public async Task<AetheriaRuntimeSelectedObjectDocument> SelectedObjectAsync(",
        "public async Task<AetheriaRuntimeInventoryDocument> InventoryAsync(",
        "public async Task<AetheriaRuntimeDaemonHealthDocument?> DaemonHealthAsync()",
        "public async Task<AetheriaRuntimeVerseAuthorityPolicyDocument?> AuthorityStatusAsync()",
        "public async Task<AetheriaRuntimeDaemonSoaViewDocument?> SoaViewAsync()",
        "public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonGameSurfaceAsync()",
        "public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonGameTuiSurfaceAsync()",
        "public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonEditorSurfaceAsync()",
        "public async Task<global::Aetheria.State.Documents.EveSurfaceState?> DaemonEditorTuiSurfaceAsync()",
        "public CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver()",
        "public CultMeshDocumentHandle<TDocument> Document<TDocument>()",
        "public Observable<TDocument> Watch<TDocument>()",
        "public Task<CultMeshReactiveDocument<TDocument>> ReactiveAsync<TDocument>(",
        "public CultMeshReactiveDocument<TDocument> Reactive<TDocument>("
    };
    var daemonClientBypassHits = forbiddenDaemonClientBypassSymbols
        .Where(symbol => aetheriaClient.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (daemonClientBypassHits.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaClient still bypasses managed daemon documents for compatibility reads: " +
            string.Join(", ", daemonClientBypassHits));
    }

    var forbiddenActionGameManagerReaderSymbols = new[]
    {
        "AetheriaRuntimeStateReader.ReadPlayerSettings",
        "AetheriaRuntimeStateReader.ReadLoadoutTemplates",
        "AetheriaRuntimeStateReader.OpenRuntimeCatalog",
        "LoadoutTemplatesAsync()",
        "ObservedLoadoutTemplates",
        "_observedLoadoutTemplates",
        "LoadRuntimeLoadoutTemplates",
        "AetheriaRuntimeStateBoot.Inspect(",
        "ResolveClient(stateBoot.StateFilePath).OpenRuntimeCatalog()",
        "ItemManager = new ItemManager(",
        "ZoneRenderer.SetDroppedPickupItemFactory(",
        "ZoneRenderer.BodySettingsCollections = Settings.BodySettingsCollections",
        "ResolveRuntimeVerseClient(",
        "PlayerSettingsAsync()",
        "ApplyRuntimePlayerSettings(",
        "CreateDefaultRuntimePlayerSettings(",
        "_runtimeClient"
    };
    var actionGameManagerReaderHits = forbiddenActionGameManagerReaderSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (actionGameManagerReaderHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still reads typed state through the runtime file reader instead of the shared Aetheria client facade: " +
            string.Join(", ", actionGameManagerReaderHits));
    }

    if (!daemonObserver.Contains("AetheriaClient", StringComparison.Ordinal) ||
        !daemonObserver.Contains("AetheriaUnityRuntimeClientProvider.ResolveClient(", StringComparison.Ordinal) ||
        !daemonObserver.Contains("AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory)", StringComparison.Ordinal) ||
        !daemonObserver.Contains("AetheriaRuntimeDaemonRenderView", StringComparison.Ordinal) ||
        !daemonObserver.Contains("CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _daemonFrame", StringComparison.Ordinal) ||
        !daemonObserver.Contains("CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> _daemonSoaView", StringComparison.Ordinal) ||
        !daemonObserver.Contains("CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> _zoneRender", StringComparison.Ordinal) ||
        !daemonObserver.Contains("_daemonFrame ??= state.Document<AetheriaRuntimeDaemonFrameDocument>().Reactive();", StringComparison.Ordinal) ||
        !daemonObserver.Contains("_daemonSoaView ??= TryReactive<AetheriaRuntimeDaemonSoaViewDocument>(state);", StringComparison.Ordinal) ||
        !daemonObserver.Contains("_zoneRender ??= state.Document<AetheriaRuntimeZoneRenderDocument>().Reactive();", StringComparison.Ordinal) ||
        !daemonObserver.Contains("AetheriaRuntimeDaemonRenderView.TryCreateCurrent(", StringComparison.Ordinal) ||
        !daemonObserver.Contains("DisposeRenderViewDocuments()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaDaemonObserver must sample daemon render views through direct managed reactive daemon documents.");
    }

    if (daemonObserver.Contains("AetheriaRuntimeStateReader.TryReadDaemonRenderView", StringComparison.Ordinal) ||
        daemonObserver.Contains(".ReadAsync(client.State)", StringComparison.Ordinal) ||
        daemonObserver.Contains("AetheriaRuntimeReactiveObservedDaemonState", StringComparison.Ordinal) ||
        daemonObserver.Contains(".ReactiveObservedDaemon()", StringComparison.Ordinal) ||
        daemonObserver.Contains("reactive.TryCurrent(out var observed)", StringComparison.Ordinal) ||
        daemonObserver.Contains("AetheriaRuntimeObservedDaemonSession", StringComparison.Ordinal) ||
        daemonObserver.Contains(".ObserveDaemon()", StringComparison.Ordinal) ||
        daemonObserver.Contains("return new AetheriaRuntimeDaemonRenderView(frame, soaView, zoneRender);", StringComparison.Ordinal) ||
        daemonObserver.Contains("DisposeObservedDaemonSession()", StringComparison.Ordinal) ||
        daemonObserver.Contains("DisposeReactiveObservedDaemonDocuments()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaDaemonObserver still routes through observed-daemon compatibility wrappers instead of direct reactive documents.");
    }

    if (daemonObserver.Contains("ObserveAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AetheriaDaemonObserver still reads observed daemon state through the AetheriaClient compatibility helper.");
    }

    if (!mainMenu.Contains("AetheriaClient", StringComparison.Ordinal) ||
        !mainMenu.Contains("ResolveSectorMap(AetheriaRuntimeStateBootReport stateBoot)", StringComparison.Ordinal) ||
        !mainMenu.Contains(".State", StringComparison.Ordinal) ||
        !mainMenu.Contains(".Document<AetheriaRuntimeSectorMapDocument>().Reactive()", StringComparison.Ordinal) ||
        !mainMenu.Contains(".Document<AetheriaRuntimePlayerSettingsDocument>().Reactive()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu no longer routes sector-map lookup through the shared Aetheria client facade.");
    }

    if (mainMenu.Contains("AetheriaRuntimeStateReader.TryReadDaemonFrame", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu still reads daemon frames through the runtime file reader instead of the shared Aetheria client facade.");
    }

    if (mainMenu.Contains("OpenRuntimeCatalog(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu still names runtime catalog access as a raw catalog open instead of a managed latest document read.");
    }

    if (mainMenu.Contains("LatestRuntimeCatalog(", StringComparison.Ordinal) ||
        mainMenu.Contains("AetheriaUnityObservedRunProjection.Project(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "MainMenu still owns gameplay catalog/sector projection instead of leaving observed-scene boot to AetheriaUnityGameplayBootShell.");
    }

    var requiredGameplayBootSymbols = new[]
    {
        ".Document<AetheriaRuntimeCatalogSnapshot>().Reactive()",
        "runtimeCatalogDocument.Current",
        ".Document<AetheriaRuntimeSectorMapDocument>().Reactive()",
        "sectorMapDocument.Current",
        "Galaxy.ProjectObservedSectorMap(",
        "sectorMap.IsTutorial",
        "sectorMap.GenerationSeed",
        "new AetheriaUnityGameplayBootResult(",
        "ObservedGalaxy"
    };
    var missingGameplayBootSymbols = requiredGameplayBootSymbols
        .Where(symbol => !gameplayBootShell.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingGameplayBootSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "AetheriaUnityGameplayBootShell must own managed catalog/sector-map projection for the observed gameplay scene: " +
            string.Join(", ", missingGameplayBootSymbols));
    }

    var observedRunProjectionPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedRunProjection.cs");
    if (File.Exists(observedRunProjectionPath))
    {
        throw new InvalidOperationException(
            "AetheriaUnityObservedRunProjection is legacy projection chaff; gameplay boot should project the managed sector-map document directly.");
    }

    var forbiddenMainMenuReaderSymbols = new[]
    {
        "AetheriaRuntimeStateReader.ReadPlayerSettings",
        "AetheriaRuntimeStateReader.ReadVerseHostSettings"
    };
    var mainMenuReaderHits = forbiddenMainMenuReaderSymbols
        .Where(symbol => mainMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (mainMenuReaderHits.Length > 0)
    {
        throw new InvalidOperationException(
            "MainMenu still reads typed state through the runtime file reader instead of the shared Aetheria client facade: " +
            string.Join(", ", mainMenuReaderHits));
    }

    if (!eveSurfacePresenter.Contains("AetheriaUnityRuntimeClientProvider.ResolveClient(", StringComparison.Ordinal) ||
        !eveSurfacePresenter.Contains("ReadDaemonSurface(statePath)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve surface presenter no longer routes daemon surface lookup through the shared AetheriaClient facade.");
    }

    if (!eveSurfacePresenter.Contains("client.State.CreateEveSurfaceCultMeshStateRefResolver()", StringComparison.Ordinal) ||
        !eveSurfacePresenter.Contains("ResolveClient(statePath).State.CreateEveSurfaceCultMeshStateRefResolver()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Aetheria Eve surface presenter no longer resolves provider state refs through managed AetheriaClientState documents.");
    }

    if (eveSurfacePresenter.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceStateRefResolver", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity Eve surface lowering still resolves state refs through the file reader instead of the shared AetheriaClient facade.");
    }

    if (eveUnitySurfaceHost.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceStateRefResolver", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity Eve surface host still resolves default state refs through the file reader instead of the managed runtime client provider.");
    }

    if (!aetheriaClientState.Contains("AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Managed client state no longer resolves Eve state refs through the typed runtime state-ref resolver.");
    }

    if (aetheriaClientState.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceCultMeshStateRefResolver", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("AetheriaRuntimeStateReader.CreateEveSurfaceStateRefResolver", StringComparison.Ordinal) ||
        aetheriaClientState.Contains("public Func<string, string> CreateEveSurfaceStateRefResolver()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Managed client state still routes or exposes Eve state-ref resolution through file-backed/delegate compatibility.");
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
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify runtime simulation tuning authority; InventoryMenu.cs is missing.");

    var requiredInventoryRequestSymbols = new[]
    {
        "private void RequestEntityShutdownPerformance(string targetEntityKey, float shutdownPerformance)",
        "private void RequestEquippedItemOverrideShutdown(EquippedItem item, bool enabled)",
        "private void RequestThermotoggleTargetTemperature(",
        "operations => operations.SetShutdownPerformance(",
        "operations => operations.SetItemOverrideShutdown(",
        "operations => operations.SetThermotoggleTargetTemperature("
    };

    var missingInventoryRequestSymbols = requiredInventoryRequestSymbols
        .Where(symbol => !inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingInventoryRequestSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Runtime simulation tuning request methods are missing from InventoryMenu's typed daemon operation boundary: " +
            string.Join(", ", missingInventoryRequestSymbols));
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

    var shutdownRequest = inventoryMenu.IndexOf("RequestEntityShutdownPerformance(", StringComparison.Ordinal);
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
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify hull conductivity authority; InventoryPanel.cs is missing.");

    if (!inventoryPanel.Contains("RequestHullConductivityToggle", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("operations => operations.ToggleHullConductivity", StringComparison.Ordinal))
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
        .Where(symbol => inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity hull conductivity requests still reject through renderer-local grid bounds instead of daemon authority: " +
            string.Join(", ", unityAcceptanceHits));
    }

    if (inventoryPanel.Contains("public bool RequestHullConductivityToggle(", StringComparison.Ordinal))
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
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify inventory entity rename authority; InventoryPanel.cs is missing.");

    if (!inventoryPanel.Contains("RequestEntityName", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("operations => operations.SetEntityName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Entity rename no longer has a typed daemon request primitive.");
    }

    if (inventoryPanel.Contains("_displayedEntity.Name =", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still renames entities directly instead of using the typed daemon request primitive.");
    }

    if (inventoryPanel.Contains("GameManager.RequestEntityName", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel still routes entity rename through ActionGameManager instead of its typed operation client.");
    }

    var renameRequest = inventoryPanel.IndexOf("RequestEntityName(_displayedEntity", StringComparison.Ordinal);
    var titleRefreshAfterRename = inventoryPanel.IndexOf("Title.text = _displayedEntity.Name", renameRequest, StringComparison.Ordinal);
    if (renameRequest >= 0 && titleRefreshAfterRename >= 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel still refreshes entity title immediately after submitting a daemon rename request.");
    }
}

static void RequireWeaponGroupRequestAuthority(string root)
{
    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify weapon group authority; InventoryMenu.cs is missing.");

    if (!inventoryMenu.Contains("RequestWeaponGroupMembership", StringComparison.Ordinal) ||
        !inventoryMenu.Contains("operations => operations.SetWeaponGroupMembership", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Weapon group membership no longer has a typed daemon request primitive.");
    }

    if (inventoryMenu.Contains("WeaponGroupDragObject", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Weapon-group action-bar binding still keeps the dead drag-object path alive instead of routing through live gameplay APIs.");
    }

    var forbiddenLocalAcceptanceSymbols = new[]
    {
        "item?.Entity?.WeaponGroups == null",
        "groupIndex >= item.Entity.WeaponGroups.Length"
    };
    var localAcceptanceHits = forbiddenLocalAcceptanceSymbols
        .Where(symbol => inventoryMenu.Contains(symbol, StringComparison.Ordinal))
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
        .Where(symbol => inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (publicAcceptanceApiHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Weapon-group request APIs still expose submission as public acceptance state: " +
            string.Join(", ", publicAcceptanceApiHits));
    }

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
    var actionBarPresentationPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityActionBarPresentation.cs");
    var actionBarPresentation = File.Exists(actionBarPresentationPath)
        ? File.ReadAllText(actionBarPresentationPath)
        : throw new InvalidOperationException("Cannot verify action-bar binding authority; AetheriaUnityActionBarPresentation.cs is missing.");
    var gameplayInputShellPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayInputShell.cs");
    var gameplayInputShell = File.Exists(gameplayInputShellPath)
        ? File.ReadAllText(gameplayInputShellPath)
        : throw new InvalidOperationException("Cannot verify action-bar binding authority; AetheriaUnityGameplayInputShell.cs is missing.");
    var gameplaySceneWiringPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplaySceneWiring.cs");
    var gameplaySceneWiring = File.Exists(gameplaySceneWiringPath)
        ? File.ReadAllText(gameplaySceneWiringPath)
        : throw new InvalidOperationException("Cannot verify action-bar binding authority; AetheriaUnityGameplaySceneWiring.cs is missing.");

    var requiredManagerSymbols = new[]
    {
        "ActionBarPresentation = _actionBarPresentation",
        "SceneWiring.ConfigureActionBarPresentation(",
        "ApplyActionBarBindings = _ => _actionBarPresentation?.ApplyLocalBindings()"
    };

    var missingManagerSymbols = requiredManagerSymbols
        .Where(symbol => !actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingManagerSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager no longer delegates action-bar binding request/restore through the action-bar presentation owner: " +
            string.Join(", ", missingManagerSymbols));
    }

    if (actionGameManager.Contains("AetheriaUnityActionBarBindingAdapter", StringComparison.Ordinal) ||
        File.Exists(Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityActionBarBindingAdapter.cs")))
    {
        throw new InvalidOperationException(
            "Action-bar binding restore must go straight to AetheriaUnityActionBarPresentation; the old adapter owned no authority.");
    }

    var requiredSceneWiringSymbols = new[]
    {
        "public void ConfigureActionBarPresentation(",
        "gameplayInputShell?.ActionBarSlots ?? Array.Empty<ActionBarSlot>()",
        "actionBarPresentation?.Bind(",
        "resolveActionBarClient);"
    };
    var missingSceneWiringSymbols = requiredSceneWiringSymbols
        .Where(symbol => !gameplaySceneWiring.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingSceneWiringSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity scene wiring no longer injects action-bar slots into the action-bar presentation owner: " +
            string.Join(", ", missingSceneWiringSymbols));
    }

    var requiredGameplayInputShellSymbols = new[]
    {
        "private void RegisterActionBarInput()",
        "private void CreateActionBarSlot(string controlPath)",
        "var action = new InputAction(binding: controlPath);",
        "_actionBarActions.Add(action);",
        "_actionBarSlots.Add(slot);",
        "DragSession.RegisterTarget(dragAction => ActionBarPresentation.RequestBinding(slot, dragAction));",
        "slot.PointerExitTrigger.OnPointerExitAsObservable().Subscribe(_ => DragSession.UnregisterTarget());"
    };
    var missingGameplayInputShellSymbols = requiredGameplayInputShellSymbols
        .Where(symbol => !gameplayInputShell.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingGameplayInputShellSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity gameplay input shell no longer owns local action-bar control binding and drag target wiring: " +
            string.Join(", ", missingGameplayInputShellSymbols));
    }

    var requiredPresentationSymbols = new[]
    {
        "public void RequestBinding(ActionBarSlot slot, DragObject dragAction)",
        "public bool RequestWeaponGroupBinding(int slotIndex, int groupIndex)",
        "public bool ClearBinding(int slotIndex)",
        "public void ApplyLocalBindings()",
        "public void ApplyBindings(",
        "private AetheriaUnityActionBarBinding CreateBindingCommit(",
        "private ActionBarBinding CreateBinding(",
        "private void SetLocalBinding(AetheriaUnityActionBarBinding binding)",
        "_localBindings = (_localBindings ?? Array.Empty<AetheriaUnityActionBarBinding>())",
        "SetLocalBinding(binding);",
        "ApplyLocalBindings();"
    };

    var missingPresentationSymbols = requiredPresentationSymbols
        .Where(symbol => !actionBarPresentation.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingPresentationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Action-bar presentation no longer owns typed daemon drag/drop binding requests and observed restore lowering: " +
            string.Join(", ", missingPresentationSymbols));
    }

    var forbiddenPresentationSymbols = new[]
    {
        "_resolveClient()?.Operations.SetActionBarBinding(",
        "_resolveClient()?.Operations.ClearActionBarBinding(",
        "Failed to send Aetheria daemon action-bar binding operation",
        "Failed to send Aetheria daemon action-bar clear operation"
    };
    var presentationHits = forbiddenPresentationSymbols
        .Where(symbol => actionBarPresentation.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (presentationHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Action-bar presentation still treats local input bindings as daemon gameplay operations: " +
            string.Join(", ", presentationHits));
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
        "slot == null || groupIndex < 0",
        "RequestActionBarBinding(",
        "CreateActionBarBinding(",
        "CreateActionBarBindingCommit(",
        "ResolveActionBarClient()?.Operations.SetActionBarBinding("
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
        "TryRequestDaemonActionBarBinding(",
        "TryRequestDaemonActionBarBindingClear(",
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
    var requiredInventoryActionBarSymbols = new[]
    {
        "SetActionBarPresentation(AetheriaUnityActionBarPresentation actionBarPresentation)"
    };
    var missingInventoryActionBarSymbols = requiredInventoryActionBarSymbols
        .Where(symbol => !inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingInventoryActionBarSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu no longer owns typed equipped-item action-bar binding operations: " +
            string.Join(", ", missingInventoryActionBarSymbols));
    }
    var forbiddenInventoryActionBarSymbols = new[]
    {
        "ProjectEquippedItemActionBarSlots(",
        "RequestWeaponGroupActionBarBinding(",
        "RequestClearActionBarBinding(",
        "_actionBarPresentation?.RequestWeaponGroupBinding(slotIndex, groupIndex)",
        "_actionBarPresentation?.ClearBinding(slotIndex)",
        "operations => operations.SetActionBarBinding(",
        "operations => operations.ClearActionBarBinding(controlPath)",
        "SetActionBarBinding(",
        "ClearActionBarBinding(controlPath)"
    };
    var inventoryActionBarHits = forbiddenInventoryActionBarSymbols
        .Where(symbol => inventoryMenu.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (inventoryActionBarHits.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryMenu still sends local action-bar binding edits to the daemon: " +
            string.Join(", ", inventoryActionBarHits));
    }
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

    if (actionBarSlot.Contains("ActionGameManager.Instance", StringComparison.Ordinal) ||
        !actionBarSlot.Contains("protected GameSettings Settings { get; }", StringComparison.Ordinal) ||
        !actionBarPresentation.Contains("new ActionBarGearBinding(entity, slot, client, _settings,", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ActionBarSlot must receive icon presentation settings explicitly instead of reaching through ActionGameManager.Instance.");
    }
}

static void RequireInventoryDoubleClickTransferRequestAuthority(string root)
{
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify inventory transfer authority; InventoryPanel.cs is missing.");

    var inventoryMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryMenu.cs");
    var inventoryMenu = File.Exists(inventoryMenuPath)
        ? File.ReadAllText(inventoryMenuPath)
        : throw new InvalidOperationException("Cannot verify inventory transfer authority; InventoryMenu.cs is missing.");

    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    if (!File.Exists(actionGameManagerPath))
    {
        throw new InvalidOperationException("Cannot verify inventory transfer authority; ActionGameManager.cs is missing.");
    }

    var requiredRequests = new[]
    {
        "RequestCargoItemTransfer",
        "RequestCargoItemEquip",
        "RequestEquippedItemStore",
        "RequestEquippedItemEquip",
        "operations => operations.TransferCargoItem(",
        "operations => operations.EquipItem(",
        "operations => operations.StoreItem("
    };

    var inventoryTransferSources = inventoryPanel + "\n" + inventoryMenu;
    var missingRequests = requiredRequests
        .Where(symbol => !inventoryTransferSources.Contains(symbol, StringComparison.Ordinal))
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
        .Where(symbol => inventoryTransferSources.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (unityInventoryAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity inventory request code still rejects requests through renderer-local capacity or membership checks: " +
            string.Join(", ", unityInventoryAcceptanceHits));
    }

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

    var requiredTypedSlotValidationSymbols = new[]
    {
        "TryResolveTypedInventoryRows(",
        "TryValidateTypedCargoSlot(",
        "TryValidateTypedEquipmentSlot(",
        ".Document<AetheriaRuntimeInventoryDocument>(entityIndex)",
        ".Reactive()",
        "origin.Cargo.TryGetValue(item, out var originPosition)",
        "SourceIndex == cargoIndex",
        "SourceIndex == equipmentIndex",
        "row.X == originPosition.x",
        "row.Y == originPosition.y",
        "row.X == item.Position.x",
        "row.Y == item.Position.y"
    };
    var missingTypedSlotValidationSymbols = requiredTypedSlotValidationSymbols
        .Where(symbol =>
            !inventoryMenu.Contains(symbol, StringComparison.Ordinal) ||
            !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingTypedSlotValidationSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory cargo/equipment submissions must validate Unity facade adapters against typed inventory projection slot identity: " +
            string.Join(", ", missingTypedSlotValidationSymbols));
    }

    var forbiddenUnknownOriginSymbols = new[] { "int.MinValue" };
    var unknownOriginHits = new[] { inventoryMenuPath, inventoryPanelPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenUnknownOriginSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();
    if (unknownOriginHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory cargo/equipment submissions still send unknown origin coordinates instead of typed slot positions: " +
            string.Join("; ", unknownOriginHits));
    }

    var forbiddenManagerCommandTargetSymbols = new[]
    {
        "TryResolveObservedCargoBayCommandTarget",
        "TryResolveObservedEquippedItemCommandTarget",
        "TryResolveCargoBayCommandTarget",
        "TryResolveEquippedItemCommandTarget"
    };
    var managerCommandTargetHits = new[] { actionGameManagerPath, inventoryMenuPath, inventoryPanelPath }
        .SelectMany(path => File.ReadLines(path)
            .Select((line, index) => new { Path = path, LineNumber = index + 1, Line = line }))
        .Where(line => forbiddenManagerCommandTargetSymbols.Any(symbol => line.Line.Contains(symbol, StringComparison.Ordinal)))
        .Select(line => $"{Path.GetRelativePath(root, line.Path)}:{line.LineNumber}: {line.Line.Trim()}")
        .ToArray();
    if (managerCommandTargetHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Inventory cargo/equipment submissions must not depend on ActionGameManager command-target adapters: " +
            string.Join("; ", managerCommandTargetHits));
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
    var publicAcceptanceApiHits = new[] { inventoryMenuPath, inventoryPanelPath }
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
        "RequestCargoItemTransfer",
        "RequestCargoItemEquip",
        "RequestEquippedItemStore",
        "RequestEquippedItemEquip"
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
    var tradeMenuPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "TradeMenu.cs");
    var tradeMenu = File.Exists(tradeMenuPath)
        ? File.ReadAllText(tradeMenuPath)
        : throw new InvalidOperationException("Cannot verify trade purchase authority; TradeMenu.cs is missing.");

    var requiredRequests = new[]
    {
        "RequestTradePurchase",
        "operations => operations.TradePurchase("
    };

    var missingRequests = requiredRequests
        .Where(symbol => !tradeMenu.Contains(symbol, StringComparison.Ordinal))
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
        .Where(symbol => tradeMenu.Contains(symbol, StringComparison.Ordinal))
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

    if (tradeMenu.Contains("GameManager.RequestTradePurchase", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("TradeMenu still routes purchases through ActionGameManager instead of its typed operation client.");
    }

    var forbiddenSubmissionAcceptanceSymbols = new[]
    {
        "if (!GameManager.RequestTradePurchase(",
        "\"Purchase request rejected!\"",
        "public bool RequestTradePurchase("
    };
    var submissionAcceptanceHits = new[] { tradeMenuPath }
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

    var firstTradeRequest = tradeMenu.IndexOf("RequestTradePurchase(", StringComparison.Ordinal);
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
        [Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeSnapshotDocuments.cs")] = new[]
        {
            "AetheriaRuntimeDroppedPickupCommit",
            "public IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> DroppedPickups",
            "public double Temperature { get; set; }"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs")] = new[]
        {
            "private AetheriaUnityLoadoutItemFactory _loadoutItemFactory",
            "_loadoutItemFactory = boot.LoadoutItemFactory"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplayBootShell.cs")] = new[]
        {
            "public sealed class AetheriaUnityGameplayBootShell",
            "ZoneRenderer.SetDroppedPickupItemFactory(loadoutItemFactory.CreateLoadoutItem)"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityObservedFrameApplier.cs")] = new[]
        {
            "public sealed class AetheriaUnityObservedFrameApplier",
            "zoneRenderer?.RestoreDroppedPickupsFromZoneRender(render)"
        },
        [Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityLoadoutItemFactory.cs")] = new[]
        {
            "public sealed class AetheriaUnityLoadoutItemFactory",
            "public ItemInstance CreateLoadoutItem(AetheriaRuntimeLoadoutItemCommit item)",
            "_itemManager.CreateSimpleCommodityInstance",
            "_itemManager.CreateCraftedInstance"
        },
        [Path.Combine(root, "Assets", "Scripts", "Zone Display", "ZoneRenderer.cs")] = new[]
        {
            "public void SetDroppedPickupItemFactory(Func<AetheriaRuntimeLoadoutItemCommit, ItemInstance> createDroppedPickupItem)",
            "public void RestoreDroppedPickupsFromZoneRender(AetheriaRuntimeZoneRenderDocument render)",
            "_createDroppedPickupItem?.Invoke(pickup.Item)",
            "AetheriaRuntimeDroppedPickupCommit",
            "DroppedPickups",
            "DropItem(",
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

    var actionGameManagerPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "ActionGameManager.cs");
    var actionGameManager = File.Exists(actionGameManagerPath)
        ? File.ReadAllText(actionGameManagerPath)
        : throw new InvalidOperationException("Cannot verify dropped pickup state; ActionGameManager.cs is missing.");
    var forbiddenManagerSymbols = new[]
    {
        "ZoneRenderer.DropItem(",
        "AetheriaRuntimeDroppedPickupCommit",
        "ClearRenderedLoot("
    };
    var managerHits = forbiddenManagerSymbols
        .Where(symbol => actionGameManager.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (managerHits.Length > 0)
    {
        throw new InvalidOperationException(
            "ActionGameManager still lowers daemon dropped pickups into Unity loot instead of delegating renderer presentation: " +
            string.Join(", ", managerHits));
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
    var loadoutProjectorPath = Path.Combine(root, "Assets", "Scripts", "ServerShared", "AetheriaRuntimeLoadoutProjector.cs");
    var loadoutSnapshotProjectorPath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaRuntimeLoadoutSnapshotProjector.cs");
    var clientStatePath = Path.Combine(
        root,
        "Packages",
        "org.gamecult.aetheria.state",
        "Runtime",
        "AetheriaClientState.cs");
    var dragSessionPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityDragSession.cs");
    var dragObjectsPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityDragObjects.cs");
    var gameplaySceneWiringPath = Path.Combine(root, "Assets", "Scripts", "Gameplay", "AetheriaUnityGameplaySceneWiring.cs");

    if (File.Exists(loadoutProjectorPath))
    {
        throw new InvalidOperationException(
            "Legacy AetheriaRuntimeLoadoutProjector must stay deleted; loadout-template saves should compose typed commits from managed daemon-frame documents.");
    }

    var requiredFiles = new[] { eveCommandDocumentPath, loadoutCommandsPath, eveBridgePath, runtimeStateMapperPath, inventoryPanelPath, loadoutSnapshotProjectorPath, clientStatePath, dragSessionPath, gameplaySceneWiringPath };
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
    var loadoutSnapshotProjector = File.ReadAllText(loadoutSnapshotProjectorPath);
    var clientState = File.ReadAllText(clientStatePath);
    var dragSession = File.ReadAllText(dragSessionPath);
    var gameplaySceneWiring = File.ReadAllText(gameplaySceneWiringPath);
    var dragObjects = File.Exists(dragObjectsPath)
        ? File.ReadAllText(dragObjectsPath)
        : throw new InvalidOperationException("Cannot verify inventory drag/drop authority; AetheriaUnityDragObjects.cs is missing.");

    if (actionGameManager.Contains("ProjectLoadoutTemplate(Entity entity)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("ActionGameManager.ProjectLoadoutTemplate", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout-template projection must not live on ActionGameManager; callers should use the managed daemon-frame snapshot path.");
    }

    if (actionGameManager.Contains("public DragObject DragObject", StringComparison.Ordinal) ||
        actionGameManager.Contains("public bool HasDragTarget", StringComparison.Ordinal) ||
        actionGameManager.Contains("_dragObject", StringComparison.Ordinal) ||
        actionGameManager.Contains("_endDragCallback", StringComparison.Ordinal) ||
        actionGameManager.Contains("public abstract class DragObject", StringComparison.Ordinal) ||
        actionGameManager.Contains("public abstract class ItemDragObject", StringComparison.Ordinal) ||
        actionGameManager.Contains("public class ItemInstanceDragObject", StringComparison.Ordinal) ||
        actionGameManager.Contains("public class EquippedItemDragObject", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.DragObject", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.HasDragTarget", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.BeginDrag", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.EndDrag", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.TryGetDraggedItem", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.RegisterDragTarget", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.UnregisterDragTarget", StringComparison.Ordinal) ||
        actionGameManager.Contains("public void BeginDrag(", StringComparison.Ordinal) ||
        actionGameManager.Contains("public bool TryGetDraggedItem(", StringComparison.Ordinal) ||
        actionGameManager.Contains("public void RegisterDragTarget(", StringComparison.Ordinal) ||
        actionGameManager.Contains("public void UnregisterDragTarget(", StringComparison.Ordinal) ||
        actionGameManager.Contains("public bool EndDrag(", StringComparison.Ordinal) ||
        !actionGameManager.Contains("private readonly AetheriaUnityDragSession _dragSession", StringComparison.Ordinal) ||
        !actionGameManager.Contains("SceneWiring.ConfigureInventoryDragSession(_dragSession)", StringComparison.Ordinal) ||
        !gameplaySceneWiring.Contains("public void ConfigureInventoryDragSession(AetheriaUnityDragSession dragSession)", StringComparison.Ordinal) ||
        !gameplaySceneWiring.Contains("Inventory?.SetDragSession(dragSession)", StringComparison.Ordinal) ||
        !gameplaySceneWiring.Contains("ShipPanel?.SetDragSession(dragSession)", StringComparison.Ordinal) ||
        !gameplaySceneWiring.Contains("TargetShipPanel?.SetDragSession(dragSession)", StringComparison.Ordinal) ||
        !dragSession.Contains("public sealed class AetheriaUnityDragSession", StringComparison.Ordinal) ||
        !dragObjects.Contains("public abstract class DragObject", StringComparison.Ordinal) ||
        !dragObjects.Contains("public abstract class ItemDragObject : DragObject", StringComparison.Ordinal) ||
        !dragObjects.Contains("public class ItemInstanceDragObject : ItemDragObject", StringComparison.Ordinal) ||
        !dragObjects.Contains("public class EquippedItemDragObject : ItemDragObject", StringComparison.Ordinal) ||
        !dragSession.Contains("private DragObject _dragObject", StringComparison.Ordinal) ||
        !dragSession.Contains("private Action<DragObject> _endDragCallback", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("public void SetDragSession(AetheriaUnityDragSession dragSession)", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("private AetheriaUnityDragSession DragSession", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("DragSession.TryGetDraggedItem(out var itemDragObject)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Inventory drag/drop must not use ActionGameManager as a drag facade; inject the shared AetheriaUnityDragSession into inventory UI.");
    }

    var requiredInventoryPanelSymbols = new[]
    {
        "RequestLoadoutTemplateSave(Entity entity)",
        "TryResolveEntityRecordKey(entity, out var targetEntityKey)",
        "CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _loadoutFrame",
        "ProjectLoadoutTemplate(targetEntityKey)",
        "AetheriaRuntimeLoadoutSnapshotProjector.ProjectLoadoutTemplate(",
        ".Document<AetheriaRuntimeDaemonFrameDocument>().Reactive()",
        "_loadoutFrame?.Dispose()",
        ".Ui.SaveLoadoutTemplateAsync(loadout, \"unity-inventory\")"
    };
    var missingInventoryPanelSymbols = requiredInventoryPanelSymbols
        .Where(symbol => !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingInventoryPanelSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "InventoryPanel no longer sends a typed Eve loadout-template command from the managed reactive daemon frame: " +
            string.Join(", ", missingInventoryPanelSymbols));
    }

    if (inventoryPanel.Contains("AetheriaRuntimeDaemonFrameSession _loadoutFrame", StringComparison.Ordinal) ||
        inventoryPanel.Contains(".ObserveDaemonFrame()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still routes loadout snapshots through AetheriaRuntimeDaemonFrameSession instead of a managed reactive typed daemon-frame document.");
    }

    RequireReactiveTypedDocumentAccess(
        inventoryPanel,
        "InventoryPanel",
        "AetheriaRuntimeDaemonFrameDocument",
        "_loadoutFrame",
        ".Document<AetheriaRuntimeDaemonFrameDocument>().Reactive()",
        "AetheriaRuntimeDaemonFrameSession",
        ".ObserveDaemonFrame()");

    if (inventoryPanel.Contains(".LoadoutTemplateAsync(", StringComparison.Ordinal) ||
        inventoryPanel.Contains(".ProjectLoadoutTemplateAsync(client.State, targetEntityKey)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("AetheriaRuntimeReactiveLoadoutSnapshotProjector", StringComparison.Ordinal) ||
        inventoryPanel.Contains(".ReactiveLoadoutSnapshotProjector()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel still asks an aggregate compatibility helper for loadout templates instead of caching the managed reactive daemon frame.");
    }

    var requiredLoadoutSnapshotProjectorSymbols = new[]
    {
        "public static AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(",
        "AetheriaRuntimeRunCheckpointCommit run,",
        "TryParseEntityKey(entityKey, out var zoneIndex, out var entityIndex)",
        "ProjectLoadoutTemplate(run, zoneIndex, entityIndex)",
        "ProjectEntityLoadout(entity, entities)"
    };
    var missingLoadoutSnapshotProjectorSymbols = requiredLoadoutSnapshotProjectorSymbols
        .Where(symbol => !loadoutSnapshotProjector.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (missingLoadoutSnapshotProjectorSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Loadout template save payloads must be composed from managed typed daemon frame documents: " +
            string.Join(", ", missingLoadoutSnapshotProjectorSymbols));
    }

    if (loadoutSnapshotProjector.Contains("ProjectLoadoutTemplateAsync(", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains("AetheriaClientState state", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains(".Document<AetheriaRuntimeDaemonFrameDocument>().Reactive()", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains("frame.Current?.Run", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout snapshot composition must not own managed document sampling; InventoryPanel already caches the reactive daemon frame.");
    }

    if (loadoutSnapshotProjector.Contains("state.Daemon.LatestFrame.LatestAsync()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout template save payloads still perform one-shot daemon frame reads instead of using a reactive typed frame document.");
    }

    if (loadoutSnapshotProjector.Contains("state.Daemon.LatestFrame.ReactiveAsync", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains("state.LatestFrame.ReactiveAsync", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains(".ObserveDaemonFrame()", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains(".ReactiveDaemonFrame()", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout template save payloads must use generic AetheriaClientState reactive typed document access instead of named wrappers or raw handle walks.");
    }

    if (clientState.Contains("ReactiveLoadoutSnapshotProjector", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains("AetheriaRuntimeReactiveLoadoutSnapshotProjector", StringComparison.Ordinal) ||
        loadoutSnapshotProjector.Contains("ProjectLoadoutTemplate(string entityKey)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout template save payloads must not recreate aggregate reactive projector wrappers; cache or sample the managed typed daemon frame document directly.");
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

    if (!inventoryPanel.Contains("RequestLoadoutTemplateSave(_displayedEntity)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.RequestLoadoutTemplateSave(_displayedEntity)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("EntityConstructionBlueprintCapture.Capture(_displayedEntity)", StringComparison.Ordinal) ||
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
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; InventoryPanel.cs is missing.");
    var tradeQueriesPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeDaemonTradeItemQueries.cs");
    var tradeQueries = File.Exists(tradeQueriesPath)
        ? File.ReadAllText(tradeQueriesPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; runtime trade item queries are missing.");
    var rtsDocumentsPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeRtsViewportDocuments.cs");
    var rtsDocuments = File.Exists(rtsDocumentsPath)
        ? File.ReadAllText(rtsDocumentsPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; RTS viewport documents are missing.");
    var rtsProjectionPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeRtsProjection.cs");
    var rtsProjection = File.Exists(rtsProjectionPath)
        ? File.ReadAllText(rtsProjectionPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; RTS projection source is missing.");
    var clientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaClient.cs");
    var client = File.Exists(clientPath)
        ? File.ReadAllText(clientPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; AetheriaClient.cs is missing.");
    var clientStatePath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaClientState.cs");
    var clientState = File.Exists(clientStatePath)
        ? File.ReadAllText(clientStatePath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; AetheriaClientState.cs is missing.");
    var verseClientPath = Path.Combine(root, "Packages", "org.gamecult.aetheria.state", "Runtime", "AetheriaRuntimeVerseClient.cs");
    var verseClient = File.Exists(verseClientPath)
        ? File.ReadAllText(verseClientPath)
        : throw new InvalidOperationException("Cannot verify loadout restore authority; AetheriaRuntimeVerseClient.cs is missing.");

    var requiredSymbols = new[]
    {
        "RequestRuntimeLoadoutRestore",
        "operations => operations.RestoreLoadout",
        "AetheriaRuntimeStationLoadoutRestoreOption",
        "_dropdownStationRefitLoadouts",
        "stationRefit?.LoadoutRestoreOptions",
        "loadout.TargetEntityKey",
        "loadout.TemplateName",
        "loadout.Price",
        "loadout.CanRestore"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Loadout restore no longer has a typed daemon request primitive: " +
            string.Join(", ", missingSymbols));
    }

    if (actionGameManager.Contains("price > Credits", StringComparison.Ordinal) ||
        inventoryPanel.Contains("price > Credits", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Unity loadout restore still rejects requests through renderer-local credits instead of daemon acceptance.");
    }

    if (actionGameManager.Contains("ObservedTradeValueSettings()", StringComparison.Ordinal) ||
        inventoryPanel.Contains("ObservedTradeValueSettings()", StringComparison.Ordinal) ||
        actionGameManager.Contains("AetheriaUnityProjectionSettings.TradeValueSettings", StringComparison.Ordinal) ||
        inventoryPanel.Contains("AetheriaUnityProjectionSettings.TradeValueSettings", StringComparison.Ordinal) ||
        actionGameManager.Contains("blueprint.Price(ItemManager)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("blueprint.Price(ItemManager)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout restore must price requests through catalog-owned runtime trade policy instead of Unity settings or ItemManager value authority.");
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

    if (!inventoryPanel.Contains("RequestRuntimeLoadoutRestore(_dropdownStationRefitLoadouts[selection.TemplateIndex])", StringComparison.Ordinal) ||
        inventoryPanel.Contains("GameManager.RequestRuntimeLoadoutRestore", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer owns typed loadout restore operations directly.");
    }

    if (inventoryPanel.Contains("AetheriaRuntimeDaemonTradeItemQueries.TryProjectLoadoutTemplatePrice(", StringComparison.Ordinal) ||
        inventoryPanel.Contains("blueprint.Price(GameManager.ItemManager)", StringComparison.Ordinal) ||
        inventoryPanel.Contains("LoadoutTemplatesAsync()", StringComparison.Ordinal) ||
        inventoryPanel.Contains("ResolveLoadoutTemplates", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "InventoryPanel must consume StationRefitAsync loadout restore options instead of pricing or enumerating loadouts locally.");
    }

    if (!tradeQueries.Contains("public static bool TryProjectLoadoutTemplatePrice(", StringComparison.Ordinal) ||
        !tradeQueries.Contains("TryProjectEntityLoadoutPrice(", StringComparison.Ordinal) ||
        !tradeQueries.Contains("typedItem.Stackable", StringComparison.Ordinal) ||
        !tradeQueries.Contains("typedItem.Price * Math.Max(1, item.Quantity)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Shared runtime trade item queries must own typed loadout template price projection.");
    }

    if (!rtsDocuments.Contains("public IReadOnlyList<AetheriaRuntimeStationLoadoutRestoreOption> LoadoutRestoreOptions", StringComparison.Ordinal) ||
        !rtsDocuments.Contains("public sealed class AetheriaRuntimeStationLoadoutRestoreOption", StringComparison.Ordinal) ||
        !rtsDocuments.Contains("public string TargetEntityKey", StringComparison.Ordinal) ||
        !rtsDocuments.Contains("public int Price", StringComparison.Ordinal) ||
        !rtsDocuments.Contains("public bool CanRestore", StringComparison.Ordinal) ||
        !rtsProjection.Contains("ProjectLoadoutRestoreOptions(", StringComparison.Ordinal) ||
        !rtsProjection.Contains("AetheriaRuntimeDaemonTradeItemQueries.TryProjectLoadoutTemplatePrice(", StringComparison.Ordinal) ||
        !rtsProjection.Contains("credits >= price", StringComparison.Ordinal) ||
        !clientState.Contains("public CultMeshDocumentHandle<AetheriaRuntimeLoadoutTemplatesDocument> LoadoutTemplates", StringComparison.Ordinal) ||
        !verseClient.Contains("RequireManagedLoadoutTemplates().Templates", StringComparison.Ordinal) ||
        !verseClient.Contains("ProjectStationRefit(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "StationRefitAsync must publish typed loadout restore options with shared runtime pricing and daemon-target identity.");
    }

    var loadoutRestoreMatch = System.Text.RegularExpressions.Regex.Match(
        inventoryPanel,
        @"private void RequestRuntimeLoadoutRestore\(AetheriaRuntimeStationLoadoutRestoreOption loadout\)[\s\S]*?\n    \}");
    if (actionGameManager.Contains("RequestRuntimeLoadoutRestore(AetheriaRuntimeLoadoutTemplateSnapshot template, out Entity", StringComparison.Ordinal) ||
        actionGameManager.Contains("public bool RequestRuntimeLoadoutRestore(", StringComparison.Ordinal) ||
        inventoryPanel.Contains("RequestRuntimeLoadoutRestore(template, out", StringComparison.Ordinal) ||
        inventoryPanel.Contains("RequestRuntimeLoadoutRestore(loadoutEntry.template, out", StringComparison.Ordinal) ||
        !loadoutRestoreMatch.Success ||
        loadoutRestoreMatch.Value.Contains("Display(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Loadout restore is still pretending daemon submission synchronously yields accepted Unity state.");
    }
}

static void RequireDockedCurrentShipRequestAuthority(string root)
{
    var inventoryPanelPath = Path.Combine(root, "Assets", "Scripts", "UI", "Menu", "InventoryPanel.cs");
    var inventoryPanel = File.Exists(inventoryPanelPath)
        ? File.ReadAllText(inventoryPanelPath)
        : throw new InvalidOperationException("Cannot verify docked current-ship authority; InventoryPanel.cs is missing.");

    var requiredSymbols = new[]
    {
        "RequestDockedCurrentShip",
        "operations => operations.SetDockedCurrentShip"
    };

    var missingSymbols = requiredSymbols
        .Where(symbol => !inventoryPanel.Contains(symbol, StringComparison.Ordinal))
        .ToArray();

    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            "Docked current-ship selection no longer has a typed daemon request primitive: " +
            string.Join(", ", missingSymbols));
    }

    if (inventoryPanel.Contains("DockedEntity.Children.Contains(ship)", StringComparison.Ordinal))
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
    var unityAcceptanceHits = FindMethodScopedLineHits(inventoryPanel, forbiddenUnityAcceptanceSymbols)
        .Where(hit => hit.MethodName == "RequestDockedCurrentShip")
        .Select(hit => $"InventoryPanel.cs:{hit.LineNumber}: {hit.Line.Trim()}")
        .ToArray();
    if (unityAcceptanceHits.Length > 0)
    {
        throw new InvalidOperationException(
            "Unity docked current-ship selection still rejects through renderer-local player/docking state instead of daemon acceptance: " +
            string.Join(", ", unityAcceptanceHits));
    }

    if (inventoryPanel.Contains("public bool RequestDockedCurrentShip(", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Docked current-ship request API still exposes submission as public acceptance state.");
    }

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

    if (inventoryPanel.Contains("GameManager.RequestDockedCurrentShip", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("RequestDockedCurrentShip(ship)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("InventoryPanel no longer owns typed current-ship selection operations directly.");
    }

    if (!inventoryPanel.Contains("TryResolveCurrentEntityKey(out var currentEntityKey)", StringComparison.Ordinal) ||
        !inventoryPanel.Contains("currentEntityKey = ResolveCurrentEntity()?.EntityKey ?? \"\"", StringComparison.Ordinal) ||
        inventoryPanel.Contains("AetheriaClientReactiveDockingState _reactiveDockingState", StringComparison.Ordinal) ||
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

static bool ContainsUnitySettingsMember(string source, string memberName)
{
    return source.Contains($" Settings.{memberName}", StringComparison.Ordinal) ||
           source.Contains($"(Settings.{memberName}", StringComparison.Ordinal) ||
           source.Contains($", Settings.{memberName}", StringComparison.Ordinal) ||
           source.Contains($"\tSettings.{memberName}", StringComparison.Ordinal) ||
           source.Contains($"\nSettings.{memberName}", StringComparison.Ordinal);
}

static void RequireReactiveTypedDocumentAccess(
    string source,
    string ownerName,
    string documentType,
    string fieldName,
    string reactiveAccessor,
    string forbiddenSessionType,
    string forbiddenObserveAccessor)
{
    var compactSource = CompactSource(source);
    var compactReactiveAccessor = CompactSource(reactiveAccessor);
    var requiredSymbols = new[]
    {
        $"CultMeshReactiveDocument<{documentType}> {fieldName}",
        $"{fieldName}?.Current",
        $"{fieldName}?.Dispose()"
    };
    var missingSymbols = requiredSymbols
        .Where(symbol => !source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (!string.IsNullOrWhiteSpace(compactReactiveAccessor) &&
        !compactSource.Contains(compactReactiveAccessor, StringComparison.Ordinal))
    {
        missingSymbols = missingSymbols
            .Concat(new[] { reactiveAccessor })
            .ToArray();
    }
    if (missingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            $"{ownerName} should read {documentType} through a managed reactive typed document: " +
            string.Join(", ", missingSymbols));
    }

    var forbiddenSymbols = new[]
    {
        $"{forbiddenSessionType} {fieldName}",
        forbiddenObserveAccessor
    };
    var survivingSymbols = forbiddenSymbols
        .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
        .ToArray();
    if (survivingSymbols.Length > 0)
    {
        throw new InvalidOperationException(
            $"{ownerName} still routes {documentType} through a legacy session wrapper instead of the managed reactive typed document: " +
            string.Join(", ", survivingSymbols));
    }
}

static string CompactSource(string source)
{
    return string.Concat((source ?? "").Where(character => !char.IsWhiteSpace(character)));
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
