import {
  AetheriaRtsSchemas,
  aetheriaRuntimeAssetManifestDocumentSlots as assetManifestSlots,
  aetheriaRuntimeAssetManifestEntrySlots as assetManifestEntrySlots,
  aetheriaRuntimeAssetRefSlots as assetRefSlots,
  aetheriaRuntimeAuthorityRuleSlots as authorityRuleSlots,
  aetheriaRuntimeBodySnapshotCommitSlots as bodySlots,
  aetheriaRuntimeCargoBayLoadoutCommitSlots as cargoBaySlots,
  aetheriaRuntimeDaemonFrameDocumentSlots as frameSlots,
  aetheriaRuntimeDaemonHealthDocumentSlots as healthSlots,
  aetheriaRuntimeEntitySnapshotCommitSlots as entitySlots,
  aetheriaRuntimeEntityStatGridCommitSlots as statGridSlots,
  aetheriaRuntimeLoadoutItemCommitSlots as itemSlots,
  aetheriaRuntimeLoadoutItemSlotCommitSlots as itemSlotSlots,
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
  AssetManifestProjection,
  AssetRef,
  AuthorityStatusProjection,
  BodyView,
  DaemonHealthProjection,
  GravityViewportResponse,
  GravityInfluence,
  InventoryProjection,
  InventoryItem,
  ObjectsViewportResponse,
  SelectedObjectProjection,
  SelectedObjectRequest,
  StarbridgeSessionProjection,
  ViewObject,
  ViewportRequest,
  ViewportResponse,
} from "./aetheria-rts-bindings.js";

export function projectViewportFromFrame(frameDocument: unknown, request: ViewportRequest): ViewportResponse {
  const objects = projectObjectsViewportFromFrame(frameDocument, request);
  const gravity = projectGravityViewportFromFrame(frameDocument, request);
  return {
    schema: AetheriaRtsSchemas.rtsViewport,
    frameId: objects.frameId,
    publishedAtUtc: objects.publishedAtUtc,
    simulationTimeSeconds: objects.simulationTimeSeconds,
    runId: objects.runId,
    zoneIndex: objects.zoneIndex,
    zoneName: objects.zoneName,
    currentEntityKey: objects.currentEntityKey,
    viewport: objects.viewport,
    controlledEntityIndices: objects.controlledEntityIndices,
    objects: objects.objects,
    gravityInfluences: gravity.gravityInfluences,
    bodies: gravity.bodies,
  };
}

export function projectObjectsViewportFromFrame(
  frameDocument: unknown,
  request: ViewportRequest,
): ObjectsViewportResponse {
  const frame = arr(frameDocument);
  const run = arr(frame[frameSlots.run]);
  const zones = list<unknown[]>(run[runSlots.zones]);
  const currentZoneIndex = num(run[runSlots.currentZoneIndex], -1);
  const zone = zones.find(candidate => num(candidate[zoneSlots.zoneIndex], -1) === currentZoneIndex) ??
    zones[0] ??
    [];
  const runId = str(run[runSlots.runId]) || "local-rts";
  const viewport = normalizeViewport(request);
  const entities = list<unknown[]>(zone[zoneSlots.entities]);
  const controlledEntityIndices = entities
    .filter(isPlayerControlled)
    .map(entity => num(entity[entitySlots.entityIndex], -1))
    .filter(index => index >= 0);
  const controlled = entities.filter(entity => controlledEntityIndices.includes(num(entity[entitySlots.entityIndex], -1)));
  const objects = entities
    .filter(entity => entityIntersectsViewport(entity, viewport))
    .filter(entity => isPlayerControlled(entity) ||
      controlled.length === 0 ||
      controlled.some(observer => canSee(observer, entity)))
    .map(entity => toViewObject(entity, runId, num(zone[zoneSlots.zoneIndex])));

  return {
    schema: AetheriaRtsSchemas.objectsViewport,
    frameId: num(frame[frameSlots.frameId]),
    publishedAtUtc: str(frame[frameSlots.publishedAtUtc]),
    simulationTimeSeconds: num(frame[frameSlots.simulationTimeSeconds]),
    runId,
    zoneIndex: num(zone[zoneSlots.zoneIndex]),
    zoneName: str(zone[zoneSlots.name]) || `Zone ${num(zone[zoneSlots.zoneIndex])}`,
    currentEntityKey: str(run[runSlots.currentEntityKey]),
    viewport,
    controlledEntityIndices,
    objects,
  };
}

export function projectGravityViewportFromFrame(
  frameDocument: unknown,
  request: ViewportRequest,
): GravityViewportResponse {
  const frame = arr(frameDocument);
  const run = arr(frame[frameSlots.run]);
  const zones = list<unknown[]>(run[runSlots.zones]);
  const currentZoneIndex = num(run[runSlots.currentZoneIndex], -1);
  const zone = zones.find(candidate => num(candidate[zoneSlots.zoneIndex], -1) === currentZoneIndex) ??
    zones[0] ??
    [];
  const runId = str(run[runSlots.runId]) || "local-rts";
  const viewport = normalizeViewport(request);
  const visibleBodies = list<unknown[]>(zone[zoneSlots.bodies])
    .filter(body => gravityInfluenceIntersectsViewport(body, viewport));

  return {
    schema: AetheriaRtsSchemas.gravityViewport,
    frameId: num(frame[frameSlots.frameId]),
    publishedAtUtc: str(frame[frameSlots.publishedAtUtc]),
    simulationTimeSeconds: num(frame[frameSlots.simulationTimeSeconds]),
    runId,
    zoneIndex: num(zone[zoneSlots.zoneIndex]),
    zoneName: str(zone[zoneSlots.name]) || `Zone ${num(zone[zoneSlots.zoneIndex])}`,
    viewport,
    gravityInfluences: visibleBodies.map(toGravityInfluence),
    bodies: visibleBodies.map(toBodyView),
  };
}

export function projectSelectedObjectFromFrame(
  frameDocument: unknown,
  request: SelectedObjectRequest,
): SelectedObjectProjection {
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

export function projectInventoryFromFrame(frameDocument: unknown, request: SelectedObjectRequest): InventoryProjection {
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

export function projectDaemonHealth(healthDocument: unknown): DaemonHealthProjection {
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

export function projectAuthorityStatus(policyDocument: unknown): AuthorityStatusProjection {
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

export function projectStarbridgeSessionSummary(summaryDocument: unknown): StarbridgeSessionProjection {
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

export function projectAssetManifest(assetManifestDocument: unknown): AssetManifestProjection {
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
    runId: str(run[runSlots.runId]) || "local-rts",
    zoneIndex: num(zone[zoneSlots.zoneIndex]),
    entities: list<unknown[]>(zone[zoneSlots.entities]),
  };
}

function normalizeViewport(request: ViewportRequest): ViewportRequest {
  return cultMeshViewportRequest(
    cultMeshRectFromBounds(request.minX, request.minY, request.maxX, request.maxY),
    request.controlledEntityIndices,
  );
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

function toStarbridgeBaseStatus(status: unknown[]): StarbridgeSessionProjection["baseStatus"] {
  return {
    entityKey: str(status[starbridgeBaseSlots.entityKey]),
    displayName: str(status[starbridgeBaseSlots.displayName]),
    hull: num(status[starbridgeBaseSlots.hull]),
    shield: num(status[starbridgeBaseSlots.shield]),
    heat: num(status[starbridgeBaseSlots.heat]),
    isActive: bool(status[starbridgeBaseSlots.isActive]),
  };
}

function toStarbridgeStationStockItem(item: unknown[]): StarbridgeSessionProjection["stationStock"][number] {
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

function toStarbridgeWaveForecast(wave: unknown[]): StarbridgeSessionProjection["waveForecast"][number] {
  return {
    waveIndex: num(wave[starbridgeWaveForecastSlots.waveIndex]),
    displayName: str(wave[starbridgeWaveForecastSlots.displayName]),
    attackerKeys: stringList(wave[starbridgeWaveForecastSlots.attackerKeys]),
    bossKey: str(wave[starbridgeWaveForecastSlots.bossKey]),
    recoveredTechnologyKeys: stringList(wave[starbridgeWaveForecastSlots.recoveredTechnologyKeys]),
  };
}

function toStarbridgeRuntimeRole(role: unknown[]): StarbridgeSessionProjection["runtimeRoles"][number] {
  return {
    runtimeId: str(role[starbridgeRoleSlots.runtimeId]),
    role: str(role[starbridgeRoleSlots.role]),
    entityKey: str(role[starbridgeRoleSlots.entityKey]),
  };
}

function toGravityInfluence(body: unknown[]): GravityInfluence {
  return {
    bodyKey: str(body[bodySlots.bodyKey]),
    orbitKey: str(body[bodySlots.orbitKey]),
    kind: str(body[bodySlots.kind]),
    x: num(body[bodySlots.gravityInfluenceCenterX]),
    y: num(body[bodySlots.gravityInfluenceCenterZ]),
    radius: resolveGravityRadius(body),
    gravityDepth: num(body[bodySlots.gravityWellDepth]),
    gravityDepthExponent: num(body[bodySlots.gravityDepthExponent]),
    waveRadius: num(body[bodySlots.gravityWaveRadius]),
    waveDepth: num(body[bodySlots.gravityWaveDepth]),
    waveSpeed: num(body[bodySlots.gravityWaveSpeed]),
  };
}

function toBodyView(body: unknown[]): BodyView {
  return {
    bodyKey: str(body[bodySlots.bodyKey]),
    orbitKey: str(body[bodySlots.orbitKey]),
    name: str(body[bodySlots.name]),
    kind: str(body[bodySlots.kind]),
    x: num(body[bodySlots.gravityInfluenceCenterX]),
    y: num(body[bodySlots.gravityInfluenceCenterZ]),
    radius: Math.max(32, num(body[bodySlots.bodyRadiusMultiplier]) * 70),
    isAsteroidBelt: str(body[bodySlots.kind]).toLowerCase().includes("asteroid"),
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

function entityIntersectsViewport(entity: unknown[], viewport: ViewportRequest): boolean {
  const x = num(entity[entitySlots.positionX]);
  const y = num(entity[entitySlots.positionZ]);
  return x >= viewport.minX && x <= viewport.maxX && y >= viewport.minY && y <= viewport.maxY;
}

function gravityInfluenceIntersectsViewport(body: unknown[], viewport: ViewportRequest): boolean {
  const x = num(body[bodySlots.gravityInfluenceCenterX]);
  const y = num(body[bodySlots.gravityInfluenceCenterZ]);
  const radius = resolveGravityRadius(body);
  return x + radius >= viewport.minX &&
    x - radius <= viewport.maxX &&
    y + radius >= viewport.minY &&
    y - radius <= viewport.maxY;
}

function canSee(observer: unknown[], target: unknown[]): boolean {
  if (num(observer[entitySlots.entityIndex], -1) === num(target[entitySlots.entityIndex], -2))
    return true;

  const dx = num(observer[entitySlots.positionX]) - num(target[entitySlots.positionX]);
  const dy = num(observer[entitySlots.positionZ]) - num(target[entitySlots.positionZ]);
  const range = Math.max(180, num(observer[entitySlots.visibility]));
  return dx * dx + dy * dy <= range * range;
}

function isPlayerControlled(entity: unknown[]): boolean {
  return str(entity[entitySlots.factionKey]).toLowerCase() === "player";
}

function stat(entity: unknown[], name: string): number {
  const grid = list<unknown[]>(entity[entitySlots.statGrids])
    .find(candidate => str(candidate[statGridSlots.name]).toLowerCase() === name.toLowerCase());
  return grid ? numberList(grid[statGridSlots.values])[0] ?? 0 : 0;
}

function resolveGravityRadius(body: unknown[]): number {
  const explicit = num(body[bodySlots.gravityInfluenceRadius]);
  if (explicit > 0)
    return explicit;
  return Math.max(32, num(body[bodySlots.bodyRadiusMultiplier]) * 70);
}

function entityKey(runId: string, zoneIndex: number, entityIndex: number): string {
  return `global:aetheria.run_state.${runId}.zone.${zoneIndex}.entity.${entityIndex}.v1`;
}

function toAssetManifestEntry(entry: unknown[]): AssetManifestProjection["assets"][number] {
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
    return spriteAsset("map.entity.player", "Sprites/Map/player");

  const normalized = kind.trim().toLowerCase();
  if (normalized.includes("station"))
    return spriteAsset("map.entity.station", "Sprites/Map/station");
  if (normalized.includes("orbital"))
    return spriteAsset("map.entity.orbital", "Sprites/Map/orbital");

  return spriteAsset("map.entity.ship", "Sprites/Map/ship");
}

function itemIconAsset(itemKey: string): AssetRef {
  const key = itemKey.trim();
  return key.length > 0
    ? textureAsset(`item.${key}.icon`, `item.${key}.icon`)
    : emptyAsset("texture");
}

function spriteAsset(assetKey: string, resourcePath: string): AssetRef {
  return asset(assetKey, "sprite", `resources://${resourcePath}`, "resources", "image/*");
}

function textureAsset(assetKey: string, resourcePath: string): AssetRef {
  return asset(assetKey, "texture", resourcePath.startsWith("resources://") ? resourcePath : `resources://${resourcePath}`, "resources", "image/*");
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
