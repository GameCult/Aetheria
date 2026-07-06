import {
  AetheriaRtsSchemas,
  aetheriaRuntimeAssetManifestDocumentSlots as assetManifestSlots,
  aetheriaRuntimeAssetManifestEntrySlots as assetManifestEntrySlots,
  aetheriaRuntimeAssetRefSlots as assetRefSlots,
  aetheriaRuntimeAuthorityRuleSlots as authorityRuleSlots,
  aetheriaRuntimeCargoBayLoadoutCommitSlots as cargoBaySlots,
  aetheriaRuntimeDaemonFrameDocumentSlots as frameSlots,
  aetheriaRuntimeDaemonHealthDocumentSlots as healthSlots,
  aetheriaRuntimeEntitySnapshotCommitSlots as entitySlots,
  aetheriaRuntimeEntityStatGridCommitSlots as statGridSlots,
  aetheriaRuntimeGameViewportBoundsSlots as viewportBoundsSlots,
  aetheriaRuntimeGameViewportDocumentSlots as gameViewportSlots,
  aetheriaRuntimeGameViewportObjectSlots as viewportObjectSlots,
  aetheriaRuntimeGravityViewportDocumentSlots as gravityViewportSlots,
  aetheriaRuntimeLoadoutItemCommitSlots as itemSlots,
  aetheriaRuntimeLoadoutItemSlotCommitSlots as itemSlotSlots,
  aetheriaRuntimeObjectsViewportDocumentSlots as objectsViewportSlots,
  aetheriaRuntimeRenderSplatLayerDefinitionSlots as renderSplatLayerSlots,
  aetheriaRuntimeRenderSplatsViewportDocumentSlots as renderSplatsViewportSlots,
  aetheriaRuntimeRenderSplatSoaSlots as renderSplatSoaSlots,
  aetheriaRuntimeRtsBodyViewSlots as bodyViewSlots,
  aetheriaRuntimeRtsEntityStatusSlots as viewportStatusSlots,
  aetheriaRuntimeRtsGravityInfluenceSlots as gravityInfluenceSlots,
  aetheriaRuntimeRtsInventoryItemSlots as viewportInventoryItemSlots,
  aetheriaRuntimeRunCheckpointCommitSlots as runSlots,
  aetheriaRuntimeStarbridgeBaseStatusSlots as starbridgeBaseSlots,
  aetheriaRuntimeStarbridgeRuntimeRoleSlots as starbridgeRoleSlots,
  aetheriaRuntimeStarbridgeSessionSummaryDocumentSlots as starbridgeSummarySlots,
  aetheriaRuntimeStarbridgeStationStockItemSlots as starbridgeStockSlots,
  aetheriaRuntimeStarbridgeWaveForecastSlots as starbridgeWaveForecastSlots,
  aetheriaRuntimeVerseAuthorityPolicyDocumentSlots as authorityPolicySlots,
  aetheriaRuntimeZoneSnapshotCommitSlots as zoneSlots,
} from "./aetheria-rts-generated-bindings.js";
import { cultMeshRectFromBounds, cultMeshViewportRequest } from "cultmesh-ts";
import type {
  AssetManifestDocument,
  AssetRef,
  AuthorityStatusDocument,
  BodyView,
  DaemonHealthDocument,
  EntityStatus,
  GravityViewportResponse,
  GravityInfluence,
  InventoryDocument,
  InventoryItem,
  ObjectsViewportResponse,
  RenderSplatsViewportResponse,
  SelectedObjectDocument,
  SelectedObjectRequest,
  StarbridgeSessionDocument,
  ViewObject,
  ViewportRequest,
  ViewportResponse,
} from "./aetheria-rts-bindings.js";

const missingDaemonRunId = "aetheria.run.unknown";

export function buildSelectedObjectDocumentFromFrame(
  frameDocument: unknown,
  request: SelectedObjectRequest,
): SelectedObjectDocument {
  const context = frameContext(frameDocument);
  const entity = context.entities.find(candidate => num(candidate[entitySlots.entityIndex], -1) === request.entityIndex);
  return {
    schema: AetheriaRtsSchemas.selectedObject,
    frameId: context.frameId,
    runId: context.runId,
    zoneIndex: context.zoneIndex,
    entityIndex: request.entityIndex,
    selected: entity ? toViewObject(entity, context.runId, context.zoneIndex) : null,
  };
}

export function buildInventoryDocumentFromFrame(frameDocument: unknown, request: SelectedObjectRequest): InventoryDocument {
  const context = frameContext(frameDocument);
  const entity = context.entities.find(candidate => num(candidate[entitySlots.entityIndex], -1) === request.entityIndex);
  const allItems = entity ? inventory(entity) : [];
  return {
    schema: AetheriaRtsSchemas.inventory,
    frameId: context.frameId,
    runId: context.runId,
    zoneIndex: context.zoneIndex,
    entityIndex: request.entityIndex,
    entityKey: entity ? entityKey(context.runId, context.zoneIndex, request.entityIndex) : "",
    items: allItems,
    equipment: allItems.filter(item => item.source === "equipment"),
    cargo: allItems.filter(item => item.source === "cargo"),
  };
}

export function readViewportDocument(viewportDocument: unknown): ViewportResponse {
  const document = arr(viewportDocument);
  const objects = readViewportObjects(list<unknown[]>(document[gameViewportSlots.objects]));
  const gravityInfluences = readGravityInfluences(list<unknown[]>(document[gameViewportSlots.gravityInfluences]));
  const bodies = readBodyViews(list<unknown[]>(document[gameViewportSlots.bodies]));
  return {
    schema: str(document[gameViewportSlots.schema]) || AetheriaRtsSchemas.gameViewport,
    frameId: num(document[gameViewportSlots.frameId]),
    publishedAtUtc: str(document[gameViewportSlots.publishedAtUtc]),
    simulationTimeSeconds: num(document[gameViewportSlots.simulationTimeSeconds]),
    runId: str(document[gameViewportSlots.runId]) || missingDaemonRunId,
    zoneIndex: num(document[gameViewportSlots.zoneIndex]),
    zoneName: str(document[gameViewportSlots.zoneName]),
    currentEntityKey: str(document[gameViewportSlots.currentEntityKey]),
    viewport: readViewportBounds(document[gameViewportSlots.viewport]),
    controlledEntityIndices: numberList(document[gameViewportSlots.controlledEntityIndices]),
    objects,
    gravityInfluences,
    bodies,
  };
}

export function readObjectsViewportDocument(viewportDocument: unknown): ObjectsViewportResponse {
  const document = arr(viewportDocument);
  return {
    schema: str(document[objectsViewportSlots.schema]) || AetheriaRtsSchemas.objectsViewport,
    frameId: num(document[objectsViewportSlots.frameId]),
    publishedAtUtc: str(document[objectsViewportSlots.publishedAtUtc]),
    simulationTimeSeconds: num(document[objectsViewportSlots.simulationTimeSeconds]),
    runId: str(document[objectsViewportSlots.runId]) || missingDaemonRunId,
    zoneIndex: num(document[objectsViewportSlots.zoneIndex]),
    zoneName: str(document[objectsViewportSlots.zoneName]),
    currentEntityKey: str(document[objectsViewportSlots.currentEntityKey]),
    viewport: readViewportBounds(document[objectsViewportSlots.viewport]),
    controlledEntityIndices: numberList(document[objectsViewportSlots.controlledEntityIndices]),
    objects: readViewportObjects(list<unknown[]>(document[objectsViewportSlots.objects])),
  };
}

export function readGravityViewportDocument(viewportDocument: unknown): GravityViewportResponse {
  const document = arr(viewportDocument);
  return {
    schema: str(document[gravityViewportSlots.schema]) || AetheriaRtsSchemas.gravityViewport,
    frameId: num(document[gravityViewportSlots.frameId]),
    publishedAtUtc: str(document[gravityViewportSlots.publishedAtUtc]),
    simulationTimeSeconds: num(document[gravityViewportSlots.simulationTimeSeconds]),
    runId: str(document[gravityViewportSlots.runId]) || missingDaemonRunId,
    zoneIndex: num(document[gravityViewportSlots.zoneIndex]),
    zoneName: str(document[gravityViewportSlots.zoneName]),
    viewport: readViewportBounds(document[gravityViewportSlots.viewport]),
    gravityInfluences: readGravityInfluences(list<unknown[]>(document[gravityViewportSlots.gravityInfluences])),
    bodies: readBodyViews(list<unknown[]>(document[gravityViewportSlots.bodies])),
  };
}

export function readRenderSplatsViewportDocument(viewportDocument: unknown): RenderSplatsViewportResponse {
  const document = arr(viewportDocument);
  return {
    schema: str(document[renderSplatsViewportSlots.schema]) || AetheriaRtsSchemas.renderSplatsViewport,
    frameId: num(document[renderSplatsViewportSlots.frameId]),
    publishedAtUtc: str(document[renderSplatsViewportSlots.publishedAtUtc]),
    simulationTimeSeconds: num(document[renderSplatsViewportSlots.simulationTimeSeconds]),
    runId: str(document[renderSplatsViewportSlots.runId]) || missingDaemonRunId,
    zoneIndex: num(document[renderSplatsViewportSlots.zoneIndex]),
    zoneName: str(document[renderSplatsViewportSlots.zoneName]),
    viewport: readViewportBounds(document[renderSplatsViewportSlots.viewport]),
    layers: list<unknown[]>(document[renderSplatsViewportSlots.layers]).map(readRenderSplatLayer),
    splats: readRenderSplatSoa(document[renderSplatsViewportSlots.splats]),
  };
}

export function readDaemonHealthDocument(healthDocument: unknown): DaemonHealthDocument {
  const health = arr(healthDocument);
  return {
    schema: str(health[healthSlots.schema]) || AetheriaRtsSchemas.daemonHealth,
    daemonId: str(health[healthSlots.daemonId]),
    verseId: str(health[healthSlots.verseId]),
    publishedAtUtc: str(health[healthSlots.publishedAtUtc]),
    statePath: str(health[healthSlots.statePath]),
    frameId: num(health[healthSlots.frameId]),
    observedCommandCount: num(health[healthSlots.observedCommandCount]),
    appliedCommandCount: num(health[healthSlots.appliedCommandCount]),
    rejectedCommandCount: num(health[healthSlots.rejectedCommandCount]),
    status: str(health[healthSlots.status]) || "unknown",
    publicationSource: str(health[healthSlots.publicationSource]),
    transport: str(health[healthSlots.transport]),
    commandBoundaryPath: str(health[healthSlots.commandBoundaryPath]),
  };
}

export function readAuthorityStatusDocument(policyDocument: unknown): AuthorityStatusDocument {
  const policy = arr(policyDocument);
  return {
    schema: str(policy[authorityPolicySlots.schema]) || AetheriaRtsSchemas.verseAuthorityPolicy,
    verseId: str(policy[authorityPolicySlots.verseId]),
    policyId: str(policy[authorityPolicySlots.policyId]),
    ruleVersion: str(policy[authorityPolicySlots.ruleVersion]),
    hostRuntimeId: str(policy[authorityPolicySlots.hostRuntimeId]),
    defaultMode: str(policy[authorityPolicySlots.defaultMode]),
    updatedAtUtc: str(policy[authorityPolicySlots.updatedAtUtc]),
    rules: list<unknown[]>(policy[authorityPolicySlots.rules]).map(rule => ({
      ruleId: str(rule[authorityRuleSlots.ruleId]),
      subjectPrefix: str(rule[authorityRuleSlots.subjectPrefix]),
      claimKinds: stringList(rule[authorityRuleSlots.claimKinds]),
      mode: str(rule[authorityRuleSlots.mode]),
      runtimeIds: stringList(rule[authorityRuleSlots.runtimeIds]),
      leaseScope: str(rule[authorityRuleSlots.leaseScope]),
      priority: num(rule[authorityRuleSlots.priority]),
    })),
  };
}

export function readStarbridgeSessionSummaryDocument(summaryDocument: unknown): StarbridgeSessionDocument {
  const summary = arr(summaryDocument);
  return {
    schema: str(summary[starbridgeSummarySlots.schema]) || AetheriaRtsSchemas.starbridgeSessionSummary,
    frameId: num(summary[starbridgeSummarySlots.frameId]),
    publishedAtUtc: str(summary[starbridgeSummarySlots.publishedAtUtc]),
    sessionId: str(summary[starbridgeSummarySlots.sessionId]) || "starbridge-session",
    scenarioId: str(summary[starbridgeSummarySlots.scenarioId]) || "starbridge.local",
    scenarioName: str(summary[starbridgeSummarySlots.scenarioName]) || "Starbridge",
    runId: str(summary[starbridgeSummarySlots.runId]) || "local-starbridge",
    zoneIndex: num(summary[starbridgeSummarySlots.zoneIndex]),
    zoneName: str(summary[starbridgeSummarySlots.zoneName]),
    phase: str(summary[starbridgeSummarySlots.phase]) || "setup",
    currentWaveIndex: num(summary[starbridgeSummarySlots.currentWaveIndex]),
    baseStatus: toStarbridgeBaseStatus(arr(summary[starbridgeSummarySlots.baseStatus])),
    stationStock: list<unknown[]>(summary[starbridgeSummarySlots.stationStock]).map(toStarbridgeStationStockItem),
    waveForecast: list<unknown[]>(summary[starbridgeSummarySlots.waveForecast]).map(toStarbridgeWaveForecast),
    runtimeRoles: list<unknown[]>(summary[starbridgeSummarySlots.runtimeRoles]).map(toStarbridgeRuntimeRole),
  };
}

export function readAssetManifestDocument(assetManifestDocument: unknown): AssetManifestDocument {
  const manifest = arr(assetManifestDocument);
  return {
    schema: str(manifest[assetManifestSlots.schema]) || AetheriaRtsSchemas.assetManifest,
    publishedAtUtc: str(manifest[assetManifestSlots.publishedAtUtc]),
    runId: str(manifest[assetManifestSlots.runId]),
    baseUri: str(manifest[assetManifestSlots.baseUri]),
    assets: list<unknown[]>(manifest[assetManifestSlots.assets]).map(toAssetManifestEntry),
  };
}

function frameContext(frameDocument: unknown): {
  frameId: number;
  runId: string;
  zoneIndex: number;
  entities: unknown[][];
} {
  const frame = arr(frameDocument);
  const run = arr(frame[frameSlots.run]);
  const zones = list<unknown[]>(run[runSlots.zones]);
  const currentZoneIndex = num(run[runSlots.currentZoneIndex], -1);
  const zone = zones.find(candidate => num(candidate[zoneSlots.zoneIndex], -1) === currentZoneIndex) ??
    zones[0] ??
    [];
  return {
    frameId: num(frame[frameSlots.frameId]),
    runId: str(run[runSlots.runId]) || missingDaemonRunId,
    zoneIndex: num(zone[zoneSlots.zoneIndex]),
    entities: list<unknown[]>(zone[zoneSlots.entities]),
  };
}

function readViewportBounds(value: unknown): ViewportRequest {
  const bounds = arr(value);
  return cultMeshViewportRequest(
    cultMeshRectFromBounds(
      num(bounds[viewportBoundsSlots.minX]),
      num(bounds[viewportBoundsSlots.minY]),
      num(bounds[viewportBoundsSlots.maxX]),
      num(bounds[viewportBoundsSlots.maxY]),
    ),
    [],
  );
}

function readViewportObjects(objects: unknown[][]): ViewObject[] {
  return objects.map(readViewportObject);
}

function readViewportObject(object: unknown[]): ViewObject {
  return {
    entityIndex: num(object[viewportObjectSlots.entityIndex], -1),
    entityKey: str(object[viewportObjectSlots.entityKey]),
    displayName: str(object[viewportObjectSlots.displayName]),
    kind: str(object[viewportObjectSlots.kind]),
    factionKey: str(object[viewportObjectSlots.factionKey]),
    x: num(object[viewportObjectSlots.x]),
    y: num(object[viewportObjectSlots.y]),
    z: num(object[viewportObjectSlots.z]),
    directionX: num(object[viewportObjectSlots.directionX]),
    directionY: num(object[viewportObjectSlots.directionY]),
    velocityX: num(object[viewportObjectSlots.velocityX]),
    velocityY: num(object[viewportObjectSlots.velocityY]),
    controlled: bool(object[viewportObjectSlots.controlled]),
    targetEntityIndex: num(object[viewportObjectSlots.targetEntityIndex], -1),
    isActive: object[viewportObjectSlots.isActive] !== false,
    visibility: num(object[viewportObjectSlots.visibility]),
    iconAsset: assetRef(arr(object[viewportObjectSlots.iconAsset]), entityIconAsset(str(object[viewportObjectSlots.kind]), bool(object[viewportObjectSlots.controlled]))),
    status: readViewportStatus(object[viewportObjectSlots.status]),
    inventory: list<unknown[]>(object[viewportObjectSlots.inventory]).map(readViewportInventoryItem),
  };
}

function readViewportStatus(value: unknown): EntityStatus {
  const status = arr(value);
  return {
    hull: num(status[viewportStatusSlots.hull]),
    shield: num(status[viewportStatusSlots.shield]),
    heat: num(status[viewportStatusSlots.heat]),
  };
}

function readViewportInventoryItem(value: unknown[]): InventoryItem {
  const itemKey = str(value[viewportInventoryItemSlots.itemKey]);
  return {
    source: str(value[viewportInventoryItemSlots.source]),
    itemKey,
    quantity: num(value[viewportInventoryItemSlots.quantity]),
    quality: num(value[viewportInventoryItemSlots.quality]),
    durability: num(value[viewportInventoryItemSlots.durability]),
    enabled: value[viewportInventoryItemSlots.enabled] !== false,
    iconAsset: assetRef(arr(value[viewportInventoryItemSlots.iconAsset]), itemIconAsset(itemKey)),
  };
}

function readGravityInfluences(influences: unknown[][]): GravityInfluence[] {
  return influences.map(influence => ({
    bodyKey: str(influence[gravityInfluenceSlots.bodyKey]),
    orbitKey: str(influence[gravityInfluenceSlots.orbitKey]),
    kind: str(influence[gravityInfluenceSlots.kind]),
    x: num(influence[gravityInfluenceSlots.x]),
    y: num(influence[gravityInfluenceSlots.y]),
    radius: num(influence[gravityInfluenceSlots.radius]),
    gravityDepth: num(influence[gravityInfluenceSlots.gravityDepth]),
    gravityDepthExponent: num(influence[gravityInfluenceSlots.gravityDepthExponent]),
    waveRadius: num(influence[gravityInfluenceSlots.waveRadius]),
    waveDepth: num(influence[gravityInfluenceSlots.waveDepth]),
    waveSpeed: num(influence[gravityInfluenceSlots.waveSpeed]),
  }));
}

function readBodyViews(bodies: unknown[][]): BodyView[] {
  return bodies.map(body => {
    const kind = str(body[bodyViewSlots.kind]);
    return {
      bodyKey: str(body[bodyViewSlots.bodyKey]),
      orbitKey: str(body[bodyViewSlots.orbitKey]),
      name: str(body[bodyViewSlots.name]),
      kind,
      x: num(body[bodyViewSlots.x]),
      y: num(body[bodyViewSlots.y]),
      radius: num(body[bodyViewSlots.radius]),
      isAsteroidBelt: bool(body[bodyViewSlots.isAsteroidBelt]),
      body: num(body[bodyViewSlots.body]),
      iconAsset: assetRef(arr(body[bodyViewSlots.iconAsset]), bodyIconAsset(kind)),
      iconSize: num(body[bodyViewSlots.iconSize]),
    };
  });
}

function readRenderSplatLayer(layer: unknown[]): RenderSplatsViewportResponse["layers"][number] {
  return {
    layerKey: str(layer[renderSplatLayerSlots.layerKey]),
    displayName: str(layer[renderSplatLayerSlots.displayName]),
    channel: num(layer[renderSplatLayerSlots.channel]),
    blendMode: str(layer[renderSplatLayerSlots.blendMode]),
    graphicsFormat: str(layer[renderSplatLayerSlots.graphicsFormat]),
    clearBeforeDraw: bool(layer[renderSplatLayerSlots.clearBeforeDraw]),
    clearR: num(layer[renderSplatLayerSlots.clearR]),
    clearG: num(layer[renderSplatLayerSlots.clearG]),
    clearB: num(layer[renderSplatLayerSlots.clearB]),
    clearA: num(layer[renderSplatLayerSlots.clearA]),
  };
}

function readRenderSplatSoa(value: unknown): RenderSplatsViewportResponse["splats"] {
  const splats = arr(value);
  return {
    count: num(splats[renderSplatSoaSlots.count]),
    centerX: numberList(splats[renderSplatSoaSlots.centerX]),
    centerY: numberList(splats[renderSplatSoaSlots.centerY]),
    halfExtentX: numberList(splats[renderSplatSoaSlots.halfExtentX]),
    halfExtentY: numberList(splats[renderSplatSoaSlots.halfExtentY]),
    rotationCos: numberList(splats[renderSplatSoaSlots.rotationCos]),
    rotationSin: numberList(splats[renderSplatSoaSlots.rotationSin]),
    channel: numberList(splats[renderSplatSoaSlots.channel]),
    falloff: numberList(splats[renderSplatSoaSlots.falloff]),
    valueR: numberList(splats[renderSplatSoaSlots.valueR]),
    valueG: numberList(splats[renderSplatSoaSlots.valueG]),
    valueB: numberList(splats[renderSplatSoaSlots.valueB]),
    valueA: numberList(splats[renderSplatSoaSlots.valueA]),
    sourceKey: stringList(splats[renderSplatSoaSlots.sourceKey]),
    layerIndex: numberList(splats[renderSplatSoaSlots.layerIndex]),
    sourceKind: numberList(splats[renderSplatSoaSlots.sourceKind]),
    frequencyX: numberList(splats[renderSplatSoaSlots.frequencyX]),
    frequencyY: numberList(splats[renderSplatSoaSlots.frequencyY]),
    phaseX: numberList(splats[renderSplatSoaSlots.phaseX]),
    phaseY: numberList(splats[renderSplatSoaSlots.phaseY]),
    animationSpeed: numberList(splats[renderSplatSoaSlots.animationSpeed]),
    sourceFlags: numberList(splats[renderSplatSoaSlots.sourceFlags]),
  };
}

function toViewObject(entity: unknown[], runId: string, zoneIndex: number): ViewObject {
  const kind = str(entity[entitySlots.kind]);
  const controlled = isPlayerControlled(entity);
  return {
    entityIndex: num(entity[entitySlots.entityIndex], -1),
    entityKey: entityKey(runId, zoneIndex, num(entity[entitySlots.entityIndex], -1)),
    displayName: str(entity[entitySlots.name]),
    kind,
    factionKey: str(entity[entitySlots.factionKey]),
    x: num(entity[entitySlots.positionX]),
    y: num(entity[entitySlots.positionZ]),
    z: num(entity[entitySlots.positionY]),
    directionX: num(entity[entitySlots.directionX]),
    directionY: num(entity[entitySlots.directionY]),
    velocityX: num(entity[entitySlots.velocityX]),
    velocityY: num(entity[entitySlots.velocityY]),
    controlled,
    targetEntityIndex: num(entity[entitySlots.targetEntityIndex], -1),
    isActive: bool(entity[entitySlots.isActive]),
    visibility: num(entity[entitySlots.visibility]),
    iconAsset: entityIconAsset(kind, controlled),
    status: {
      hull: stat(entity, "hull"),
      shield: stat(entity, "shield"),
      heat: stat(entity, "heat"),
    },
    inventory: inventory(entity),
  };
}

function toStarbridgeBaseStatus(status: unknown[]): StarbridgeSessionDocument["baseStatus"] {
  return {
    entityKey: str(status[starbridgeBaseSlots.entityKey]),
    displayName: str(status[starbridgeBaseSlots.displayName]),
    hull: num(status[starbridgeBaseSlots.hull]),
    shield: num(status[starbridgeBaseSlots.shield]),
    heat: num(status[starbridgeBaseSlots.heat]),
    isActive: bool(status[starbridgeBaseSlots.isActive]),
  };
}

function toStarbridgeStationStockItem(item: unknown[]): StarbridgeSessionDocument["stationStock"][number] {
  const itemKey = str(item[starbridgeStockSlots.itemKey]);
  return {
    itemKey,
    quantity: num(item[starbridgeStockSlots.quantity]),
    quality: num(item[starbridgeStockSlots.quality]),
    durability: num(item[starbridgeStockSlots.durability]),
    source: str(item[starbridgeStockSlots.source]) || "station",
    iconAsset: assetRef(arr(item[starbridgeStockSlots.iconAsset]), itemIconAsset(itemKey)),
  };
}

function toStarbridgeWaveForecast(wave: unknown[]): StarbridgeSessionDocument["waveForecast"][number] {
  return {
    waveIndex: num(wave[starbridgeWaveForecastSlots.waveIndex]),
    displayName: str(wave[starbridgeWaveForecastSlots.displayName]),
    attackerKeys: stringList(wave[starbridgeWaveForecastSlots.attackerKeys]),
    bossKey: str(wave[starbridgeWaveForecastSlots.bossKey]),
    recoveredTechnologyKeys: stringList(wave[starbridgeWaveForecastSlots.recoveredTechnologyKeys]),
  };
}

function toStarbridgeRuntimeRole(role: unknown[]): StarbridgeSessionDocument["runtimeRoles"][number] {
  return {
    runtimeId: str(role[starbridgeRoleSlots.runtimeId]),
    role: str(role[starbridgeRoleSlots.role]),
    entityKey: str(role[starbridgeRoleSlots.entityKey]),
  };
}

function inventory(entity: unknown[]): InventoryItem[] {
  const items: InventoryItem[] = [];
  for (const slot of list<unknown[]>(entity[entitySlots.equipment]))
    addSlot(items, "equipment", slot);
  for (const bay of list<unknown[]>(entity[entitySlots.cargoContents])) {
    for (const slot of list<unknown[]>(bay[cargoBaySlots.items]))
      addSlot(items, "cargo", slot);
  }

  return items.filter(item => item.itemKey.length > 0);
}

function addSlot(items: InventoryItem[], source: string, slot: unknown[]): void {
  const item = arr(slot[itemSlotSlots.item]);
  const itemKey = str(item[itemSlots.itemKey]);
  items.push({
    source,
    itemKey,
    quantity: num(item[itemSlots.quantity]),
    quality: num(item[itemSlots.quality]),
    durability: num(item[itemSlots.durability]),
    enabled: bool(item[itemSlots.enabled]),
    iconAsset: itemIconAsset(itemKey),
  });
}

function isPlayerControlled(entity: unknown[]): boolean {
  return str(entity[entitySlots.factionKey]).toLowerCase() === "player";
}

function stat(entity: unknown[], name: string): number {
  const grid = list<unknown[]>(entity[entitySlots.statGrids])
    .find(candidate => str(candidate[statGridSlots.name]).toLowerCase() === name.toLowerCase());
  return grid ? numberList(grid[statGridSlots.values])[0] ?? 0 : 0;
}

function entityKey(runId: string, zoneIndex: number, entityIndex: number): string {
  return `global:aetheria.run_state.${runId}.zone.${zoneIndex}.entity.${entityIndex}.v1`;
}

function toAssetManifestEntry(entry: unknown[]): AssetManifestDocument["assets"][number] {
  return {
    ref: assetRef(arr(entry[assetManifestEntrySlots.ref])),
    sizeBytes: num(entry[assetManifestEntrySlots.sizeBytes]),
    width: num(entry[assetManifestEntrySlots.width]),
    height: num(entry[assetManifestEntrySlots.height]),
    tags: stringList(entry[assetManifestEntrySlots.tags]),
  };
}

function entityIconAsset(kind: string, controlled: boolean): AssetRef {
  if (controlled)
    return spriteAsset("map.entity.player");

  const normalized = kind.trim().toLowerCase();
  if (normalized.includes("station"))
    return spriteAsset("map.entity.station");
  if (normalized.includes("orbital"))
    return spriteAsset("map.entity.orbital");
  if (normalized.includes("projectile"))
    return spriteAsset("map.entity.projectile");

  return spriteAsset("map.entity.ship");
}

function bodyIconAsset(kind: string): AssetRef {
  const normalized = kind.trim().toLowerCase();
  if (normalized.includes("sun") || normalized.includes("star"))
    return spriteAsset("map.body.sun");
  if (normalized.includes("asteroid"))
    return spriteAsset("map.body.asteroid");

  return spriteAsset("map.body.planet");
}

function itemIconAsset(itemKey: string): AssetRef {
  const key = itemKey.trim();
  return key.length > 0
    ? textureAsset(`item.${key}.icon`)
    : emptyAsset("texture");
}

function spriteAsset(assetKey: string): AssetRef {
  return asset(assetKey, "sprite", cultMeshAssetUri(assetKey), "cultmesh", "image/*");
}

function textureAsset(assetKey: string): AssetRef {
  return asset(assetKey, "texture", cultMeshAssetUri(assetKey), "cultmesh", "image/*");
}

function cultMeshAssetUri(assetKey: string): string {
  return `cultmesh://aetheria/assets/${assetKey.trim().replace(/[.\\]+/g, "/").replace(/^\/+|\/+$/g, "")}`;
}

function assetRef(value: unknown[], fallback: AssetRef = emptyAsset()): AssetRef {
  const assetKey = str(value[assetRefSlots.assetKey]);
  if (assetKey.length === 0)
    return fallback;

  return asset(
    assetKey,
    str(value[assetRefSlots.kind]),
    str(value[assetRefSlots.uri]),
    str(value[assetRefSlots.transport]),
    str(value[assetRefSlots.mimeType]),
    str(value[assetRefSlots.contentHash]),
    stringRecord(value[assetRefSlots.metadata]),
  );
}

function asset(
  assetKey: string,
  kind: string,
  uri: string,
  transport: string,
  mimeType: string,
  contentHash = "",
  metadata: Record<string, string> = {},
): AssetRef {
  return {
    assetKey,
    kind,
    uri,
    transport,
    contentHash,
    mimeType,
    metadata,
  };
}

function emptyAsset(kind = ""): AssetRef {
  return asset("", kind, "", "", "");
}

function arr(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function list<T extends unknown[]>(value: unknown): T[] {
  return Array.isArray(value) ? value.filter(Array.isArray) as T[] : [];
}

function numberList(value: unknown): number[] {
  return Array.isArray(value) ? value.map(candidate => num(candidate)) : [];
}

function stringList(value: unknown): string[] {
  return Array.isArray(value) ? value.map(candidate => str(candidate)).filter(candidate => candidate.length > 0) : [];
}

function stringRecord(value: unknown): Record<string, string> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    return {};

  return Object.fromEntries(
    Object.entries(value)
      .map(([key, candidate]) => [key, str(candidate)])
      .filter(([, candidate]) => candidate.length > 0),
  );
}

function str(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function num(value: unknown, fallback = 0): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function bool(value: unknown): boolean {
  return value === true;
}
