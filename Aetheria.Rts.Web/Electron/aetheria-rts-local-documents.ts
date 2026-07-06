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
  aetheriaRuntimeProjectileCommitSlots as projectileSlots,
  aetheriaRuntimeRunCheckpointCommitSlots as runSlots,
  aetheriaRuntimeSunVisualCommitSlots as sunVisualSlots,
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

export function buildViewportDocumentFromFrame(frameDocument: unknown, request: ViewportRequest): ViewportResponse {
  const objects = buildObjectsViewportDocumentFromFrame(frameDocument, request);
  const gravity = buildGravityViewportDocumentFromFrame(frameDocument, request);
  return {
    schema: AetheriaRtsSchemas.gameViewport,
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

export function buildObjectsViewportDocumentFromFrame(
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
  const runId = str(run[runSlots.runId]) || missingDaemonRunId;
  const viewport = normalizeViewport(request);
  const entities = list<unknown[]>(zone[zoneSlots.entities]);
  const controlledEntityIndices = entities
    .filter(isPlayerControlled)
    .map(entity => num(entity[entitySlots.entityIndex], -1))
    .filter(index => index >= 0);
  const controlled = entities.filter(entity => controlledEntityIndices.includes(num(entity[entitySlots.entityIndex], -1)));
  const projectiles = list<unknown[]>(zone[zoneSlots.projectiles]);
  const objects = entities
    .filter(entity => entityIntersectsViewport(entity, viewport))
    .filter(entity => isPlayerControlled(entity) ||
      controlled.length === 0 ||
      controlled.some(observer => canSee(observer, entity)))
    .map(entity => toViewObject(entity, runId, num(zone[zoneSlots.zoneIndex])))
    .concat(projectiles
      .filter(projectile => projectile[projectileSlots.active] !== false)
      .filter(projectile => projectileIntersectsViewport(projectile, viewport))
      .map(toProjectileViewObject));

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

export function buildGravityViewportDocumentFromFrame(
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
  const runId = str(run[runSlots.runId]) || missingDaemonRunId;
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

export function buildRenderSplatsViewportDocumentFromFrame(
  frameDocument: unknown,
  request: ViewportRequest,
): RenderSplatsViewportResponse {
  const frame = arr(frameDocument);
  const run = arr(frame[frameSlots.run]);
  const zones = list<unknown[]>(run[runSlots.zones]);
  const currentZoneIndex = num(run[runSlots.currentZoneIndex], -1);
  const zone = zones.find(candidate => num(candidate[zoneSlots.zoneIndex], -1) === currentZoneIndex) ??
    zones[0] ??
    [];
  const runId = str(run[runSlots.runId]) || missingDaemonRunId;
  const viewport = normalizeViewport(request);
  const layers = defaultRenderSplatLayers();
  const layerIndices: ReadonlyMap<string, number> = new Map(layers.map((layer, index) => [layer.layerKey, index] as const));
  const builder = new RenderSplatBuilder();
  const viewportCenterX = (viewport.minX + viewport.maxX) * 0.5;
  const viewportCenterY = (viewport.minY + viewport.maxY) * 0.5;
  const viewportHalfX = Math.max(0.0001, (viewport.maxX - viewport.minX) * 0.5);
  const viewportHalfY = Math.max(0.0001, (viewport.maxY - viewport.minY) * 0.5);
  const terrainDepth = num(zone[zoneSlots.gravityTerrainDepth]);
  const terrainWaveFrequency = num(zone[zoneSlots.gravityTerrainWaveFrequency], 1);

  if (terrainDepth !== 0) {
    builder.add({
      layerIndex: requiredLayerIndex(layerIndices, "gravity.height"),
      centerX: viewportCenterX,
      centerY: viewportCenterY,
      halfExtentX: viewportHalfX,
      halfExtentY: viewportHalfY,
      channel: 1,
      falloff: 0,
      valueR: -terrainDepth,
      valueA: 1,
      sourceKey: "environment.gravity_terrain",
      sourceKind: 2,
      frequencyX: 3,
      frequencyY: 3,
      animationSpeed: terrainWaveFrequency * 0.025,
      sourceFlags: 1,
    });
  }

  builder.add({
    layerIndex: requiredLayerIndex(layerIndices, "fog.surface_height"),
    centerX: viewportCenterX,
    centerY: viewportCenterY,
    halfExtentX: viewportHalfX,
    halfExtentY: viewportHalfY,
    channel: 4,
    falloff: 0,
    valueR: 1,
    valueA: 1,
    sourceKey: "environment.fog_surface_height",
    sourceKind: 2,
    frequencyX: 4,
    frequencyY: 4,
    animationSpeed: 0.015,
    sourceFlags: 1,
  });
  builder.add({
    layerIndex: requiredLayerIndex(layerIndices, "fog.patch_height"),
    centerX: viewportCenterX,
    centerY: viewportCenterY,
    halfExtentX: viewportHalfX,
    halfExtentY: viewportHalfY,
    channel: 4,
    falloff: 0,
    valueR: 1,
    valueA: 1,
    sourceKey: "environment.fog_patch_height",
    sourceKind: 2,
    frequencyX: 9,
    frequencyY: 9,
    animationSpeed: 0.02,
    sourceFlags: 1,
  });
  builder.add({
    layerIndex: requiredLayerIndex(layerIndices, "fog.patch"),
    centerX: viewportCenterX,
    centerY: viewportCenterY,
    halfExtentX: viewportHalfX,
    halfExtentY: viewportHalfY,
    channel: 4,
    falloff: 0,
    valueR: 1,
    valueA: 1,
    sourceKey: "environment.fog_patch",
    sourceKind: 2,
    frequencyX: 6,
    frequencyY: 6,
    animationSpeed: 0.01,
    sourceFlags: 1,
  });

  for (const body of list<unknown[]>(zone[zoneSlots.bodies])) {
    if (!gravityInfluenceIntersectsViewport(body, viewport))
      continue;

    const radius = resolveGravityRadius(body);
    const bodyKey = str(body[bodySlots.bodyKey]);
    builder.add({
      layerIndex: requiredLayerIndex(layerIndices, "gravity.height"),
      centerX: num(body[bodySlots.gravityInfluenceCenterX]),
      centerY: num(body[bodySlots.gravityInfluenceCenterZ]),
      halfExtentX: radius,
      halfExtentY: radius,
      channel: 1,
      falloff: 3,
      valueR: num(body[bodySlots.gravityWellDepth]),
      valueA: 1,
      sourceKey: bodyKey,
    });

    if (num(body[bodySlots.gravityWaveRadius]) > 0 && num(body[bodySlots.gravityWaveDepth]) !== 0) {
      builder.add({
        layerIndex: requiredLayerIndex(layerIndices, "gravity.wave"),
        centerX: num(body[bodySlots.gravityInfluenceCenterX]),
        centerY: num(body[bodySlots.gravityInfluenceCenterZ]),
        halfExtentX: num(body[bodySlots.gravityWaveRadius]),
        halfExtentY: num(body[bodySlots.gravityWaveRadius]),
        channel: 2,
        falloff: 2,
        valueR: num(body[bodySlots.gravityWaveDepth]),
        valueG: num(body[bodySlots.gravityWaveSpeed]),
        valueA: 1,
        sourceKey: bodyKey,
      });
    }

    if (str(body[bodySlots.kind]).toLowerCase().includes("sun")) {
      const sunVisual = arr(body[bodySlots.sunVisual]);
      const tintRadius = Math.max(
        radius,
        Math.max(32, num(body[bodySlots.bodyRadiusMultiplier]) * 70) *
          Math.max(0.01, num(sunVisual[sunVisualSlots.lightRadiusMultiplier], 1)),
      );
      builder.add({
        layerIndex: requiredLayerIndex(layerIndices, "fog.tint"),
        centerX: num(body[bodySlots.gravityInfluenceCenterX]),
        centerY: num(body[bodySlots.gravityInfluenceCenterZ]),
        halfExtentX: tintRadius,
        halfExtentY: tintRadius,
        channel: 4,
        falloff: 2,
        valueR: num(sunVisual[sunVisualSlots.fogTintColorX]),
        valueG: num(sunVisual[sunVisualSlots.fogTintColorY]),
        valueB: num(sunVisual[sunVisualSlots.fogTintColorZ]),
        valueA: 1,
        sourceKey: bodyKey,
      });
    }
  }

  for (const entity of list<unknown[]>(zone[zoneSlots.entities])) {
    if (!isPlayerControlled(entity))
      continue;

    const visibility = Math.max(180, num(entity[entitySlots.visibility]));
    const x = num(entity[entitySlots.positionX]);
    const y = num(entity[entitySlots.positionZ]);
    if (x + visibility < viewport.minX ||
      x - visibility > viewport.maxX ||
      y + visibility < viewport.minY ||
      y - visibility > viewport.maxY) {
      continue;
    }

    builder.add({
      layerIndex: requiredLayerIndex(layerIndices, "visibility.mask"),
      centerX: x,
      centerY: y,
      halfExtentX: visibility,
      halfExtentY: visibility,
      channel: 0,
      falloff: 2,
      valueR: 1,
      valueG: 1,
      valueB: 1,
      valueA: 1,
      sourceKey: entityKey(runId, num(zone[zoneSlots.zoneIndex]), num(entity[entitySlots.entityIndex], -1)),
    });
  }

  return {
    schema: AetheriaRtsSchemas.renderSplatsViewport,
    frameId: num(frame[frameSlots.frameId]),
    publishedAtUtc: str(frame[frameSlots.publishedAtUtc]),
    simulationTimeSeconds: num(frame[frameSlots.simulationTimeSeconds]),
    runId,
    zoneIndex: num(zone[zoneSlots.zoneIndex]),
    zoneName: str(zone[zoneSlots.name]) || `Zone ${num(zone[zoneSlots.zoneIndex])}`,
    viewport,
    layers,
    splats: builder.build(),
  };
}

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

function toProjectileViewObject(projectile: unknown[]): ViewObject {
  return {
    entityIndex: -1,
    entityKey: str(projectile[projectileSlots.projectileId]),
    displayName: str(projectile[projectileSlots.weaponKind]) || "projectile",
    kind: "projectile",
    factionKey: str(projectile[projectileSlots.factionKey]),
    x: num(projectile[projectileSlots.positionX]),
    y: num(projectile[projectileSlots.positionZ]),
    z: num(projectile[projectileSlots.positionY]),
    directionX: num(projectile[projectileSlots.directionX]),
    directionY: num(projectile[projectileSlots.directionY]),
    velocityX: num(projectile[projectileSlots.velocityX]),
    velocityY: num(projectile[projectileSlots.velocityY]),
    controlled: false,
    targetEntityIndex: num(projectile[projectileSlots.targetEntityIndex], -1),
    isActive: projectile[projectileSlots.active] !== false,
    visibility: num(projectile[projectileSlots.radius]),
    iconAsset: spriteAsset("map.entity.projectile"),
    status: {
      hull: num(projectile[projectileSlots.damage]),
      shield: 0,
      heat: num(projectile[projectileSlots.ageSeconds]),
    },
    inventory: [],
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
  const kind = str(body[bodySlots.kind]);
  return {
    bodyKey: str(body[bodySlots.bodyKey]),
    orbitKey: str(body[bodySlots.orbitKey]),
    name: str(body[bodySlots.name]),
    kind,
    x: num(body[bodySlots.gravityInfluenceCenterX]),
    y: num(body[bodySlots.gravityInfluenceCenterZ]),
    radius: Math.max(32, num(body[bodySlots.bodyRadiusMultiplier]) * 70),
    isAsteroidBelt: kind.toLowerCase().includes("asteroid"),
    iconAsset: bodyIconAsset(kind),
    iconSize: num(body[bodySlots.iconSize]),
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

function projectileIntersectsViewport(projectile: unknown[], viewport: ViewportRequest): boolean {
  const x = num(projectile[projectileSlots.positionX]);
  const y = num(projectile[projectileSlots.positionZ]);
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

type RenderSplatRow = {
  layerIndex: number;
  centerX: number;
  centerY: number;
  halfExtentX: number;
  halfExtentY: number;
  rotationCos?: number;
  rotationSin?: number;
  channel: number;
  falloff: number;
  valueR?: number;
  valueG?: number;
  valueB?: number;
  valueA?: number;
  sourceKey: string;
  sourceKind?: number;
  frequencyX?: number;
  frequencyY?: number;
  phaseX?: number;
  phaseY?: number;
  animationSpeed?: number;
  sourceFlags?: number;
};

class RenderSplatBuilder {
  private readonly rows: RenderSplatRow[] = [];

  public add(row: RenderSplatRow): void {
    this.rows.push(row);
  }

  public build(): RenderSplatsViewportResponse["splats"] {
    return {
      count: this.rows.length,
      centerX: this.rows.map(row => row.centerX),
      centerY: this.rows.map(row => row.centerY),
      halfExtentX: this.rows.map(row => row.halfExtentX),
      halfExtentY: this.rows.map(row => row.halfExtentY),
      rotationCos: this.rows.map(row => row.rotationCos ?? 1),
      rotationSin: this.rows.map(row => row.rotationSin ?? 0),
      channel: this.rows.map(row => row.channel),
      falloff: this.rows.map(row => row.falloff),
      valueR: this.rows.map(row => row.valueR ?? 0),
      valueG: this.rows.map(row => row.valueG ?? 0),
      valueB: this.rows.map(row => row.valueB ?? 0),
      valueA: this.rows.map(row => row.valueA ?? 0),
      sourceKey: this.rows.map(row => row.sourceKey),
      layerIndex: this.rows.map(row => row.layerIndex),
      sourceKind: this.rows.map(row => row.sourceKind ?? 0),
      frequencyX: this.rows.map(row => row.frequencyX ?? 1),
      frequencyY: this.rows.map(row => row.frequencyY ?? 1),
      phaseX: this.rows.map(row => row.phaseX ?? 0),
      phaseY: this.rows.map(row => row.phaseY ?? 0),
      animationSpeed: this.rows.map(row => row.animationSpeed ?? 0),
      sourceFlags: this.rows.map(row => row.sourceFlags ?? 0),
    };
  }
}

function defaultRenderSplatLayers(): RenderSplatsViewportResponse["layers"] {
  return [
    layer("gravity.height", "Gravity Height", 1, "add", "R16_SFloat"),
    layer("gravity.wave", "Gravity Wave", 2, "add", "R16_SFloat"),
    layer("visibility.mask", "Visibility Mask", 0, "max", "R16_SFloat"),
    layer("fog.surface_height", "Fog Surface Height", 4, "add", "R16_SFloat"),
    layer("fog.patch_height", "Fog Patch Height", 4, "add", "R16_SFloat"),
    layer("fog.patch", "Fog Patch", 4, "max", "R16_SFloat"),
    layer("fog.tint", "Fog Tint", 4, "add", "B10G11R11_UFloatPack32"),
    layer("influence.mask", "Influence", 3, "add", "R16_SFloat"),
  ];
}

function layer(
  layerKey: string,
  displayName: string,
  channel: number,
  blendMode: string,
  graphicsFormat: string,
): RenderSplatsViewportResponse["layers"][number] {
  return {
    layerKey,
    displayName,
    channel,
    blendMode,
    graphicsFormat,
    clearBeforeDraw: true,
    clearR: 0,
    clearG: 0,
    clearB: 0,
    clearA: 0,
  };
}

function requiredLayerIndex(layers: ReadonlyMap<string, number>, key: string): number {
  const index = layers.get(key);
  if (index == null)
    throw new Error(`Missing render splat layer ${key}.`);
  return index;
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
