import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptPath = fileURLToPath(import.meta.url);
const root = resolve(scriptPath, "..", "..");
const repoRoot = resolve(root, "..");

const sources = [
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeEveCommandDocument.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeAssetDocuments.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonRenderQueries.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonTradeItemQueries.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeSnapshotDocuments.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeGameViewportDocuments.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRenderSplatDocuments.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeStarbridgeDocuments.cs"),
  resolve(repoRoot, "Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseAuthorityPolicy.cs"),
];
const outputPath = resolve(root, "Electron/aetheria-rts-generated-bindings.ts");
const rendererContractPath = resolve(root, "Client/aetheria-rts-contract.ts");
const preloadPath = resolve(root, "Electron/preload.cjs");
const check = process.argv.includes("--check");

const ipcChannels = [
  ["mapViewport", "aetheria-rts:map-viewport"],
  ["objectsViewport", "aetheria-rts:objects-viewport"],
  ["gravityViewport", "aetheria-rts:gravity-viewport"],
  ["renderSplatsViewport", "aetheria-rts:render-splats-viewport"],
  ["viewportFeed", "aetheria-rts:viewport-feed"],
  ["viewportFeedStop", "aetheria-rts:viewport-feed-stop"],
  ["viewportFeedUpdate", "aetheria-rts:viewport-feed-update"],
  ["selectedObject", "aetheria-rts:selected-object"],
  ["inventory", "aetheria-rts:inventory"],
  ["daemonHealth", "aetheria-rts:daemon-health"],
  ["authorityStatus", "aetheria-rts:authority-status"],
  ["starbridgeSession", "aetheria-rts:starbridge-session"],
  ["assetManifest", "aetheria-rts:asset-manifest"],
  ["setMoveVector", "aetheria-rts:set-move-vector"],
  ["setTarget", "aetheria-rts:set-target"],
  ["surfaceCatalog", "aetheria-rts:surface-catalog"],
  ["surfaceCatalogIndex", "aetheria-rts:surface-catalog-index"],
  ["eveSurface", "aetheria-rts:eve-surface"],
  ["submitEveCommand", "aetheria-rts:submit-eve-command"],
  ["windowControl", "aetheria-rts:window-control"],
  ["health", "aetheria-rts:health"],
];

const sourceText = sources.map(path => readFileSync(path, "utf8")).join("\n");

const enums = [
  {
    exportName: "AetheriaRuntimeDaemonCommandKinds",
    entries: parseEnum(sourceText, "AetheriaRuntimeDaemonCommandKinds"),
  },
  {
    exportName: "AetheriaRuntimeEveCommandKinds",
    entries: parseEnum(sourceText, "AetheriaRuntimeEveCommandKind"),
  },
];
const documents = [
  {
    exportName: "aetheriaRuntimeDaemonCommandDocument",
    className: "AetheriaRuntimeDaemonCommandDocument",
    schemaName: "daemonCommand",
  },
  {
    exportName: "aetheriaRuntimeEveCommandDocument",
    className: "AetheriaRuntimeEveCommandDocument",
    schemaName: "eveCommand",
  },
  {
    exportName: "aetheriaRuntimeDaemonFrameDocument",
    className: "AetheriaRuntimeDaemonFrameDocument",
    schemaName: "daemonFrame",
  },
  {
    exportName: "aetheriaRuntimeDaemonHealthDocument",
    className: "AetheriaRuntimeDaemonHealthDocument",
    schemaName: "daemonHealth",
  },
  {
    exportName: "aetheriaRuntimeAssetRef",
    className: "AetheriaRuntimeAssetRef",
  },
  {
    exportName: "aetheriaRuntimeAssetManifestDocument",
    className: "AetheriaRuntimeAssetManifestDocument",
    schemaName: "assetManifest",
  },
  {
    exportName: "aetheriaRuntimeAssetManifestEntry",
    className: "AetheriaRuntimeAssetManifestEntry",
  },
  {
    exportName: "aetheriaRuntimeGameViewportDocument",
    className: "AetheriaRuntimeGameViewportDocument",
    schemaName: "gameViewport",
  },
  {
    exportName: "aetheriaRuntimeObjectsViewportDocument",
    className: "AetheriaRuntimeObjectsViewportDocument",
    schemaName: "objectsViewport",
  },
  {
    exportName: "aetheriaRuntimeGravityViewportDocument",
    className: "AetheriaRuntimeGravityViewportDocument",
    schemaName: "gravityViewport",
  },
  {
    exportName: "aetheriaRuntimeRenderSplatsViewportDocument",
    className: "AetheriaRuntimeRenderSplatsViewportDocument",
    schemaName: "renderSplatsViewport",
  },
  {
    exportName: "aetheriaRuntimeRenderSplatLayerDefinition",
    className: "AetheriaRuntimeRenderSplatLayerDefinition",
  },
  {
    exportName: "aetheriaRuntimeRenderSplatSoa",
    className: "AetheriaRuntimeRenderSplatSoa",
  },
  {
    exportName: "aetheriaRuntimeCurrentZoneDocument",
    className: "AetheriaRuntimeCurrentZoneDocument",
    schemaName: "currentZone",
  },
  {
    exportName: "aetheriaRuntimeCurrentEntityDocument",
    className: "AetheriaRuntimeCurrentEntityDocument",
    schemaName: "currentEntity",
  },
  {
    exportName: "aetheriaRuntimeCurrentDockingDocument",
    className: "AetheriaRuntimeCurrentDockingDocument",
    schemaName: "currentDocking",
  },
  {
    exportName: "aetheriaRuntimeStationRefitDocument",
    className: "AetheriaRuntimeStationRefitDocument",
    schemaName: "stationRefit",
  },
  {
    exportName: "aetheriaRuntimeStationRefitEntityOption",
    className: "AetheriaRuntimeStationRefitEntityOption",
  },
  {
    exportName: "aetheriaRuntimeStationStockItem",
    className: "AetheriaRuntimeStationStockItem",
  },
  {
    exportName: "aetheriaRuntimeSectorMapDocument",
    className: "AetheriaRuntimeSectorMapDocument",
    schemaName: "sectorMap",
  },
  {
    exportName: "aetheriaRuntimeSectorMapZone",
    className: "AetheriaRuntimeSectorMapZone",
  },
  {
    exportName: "aetheriaRuntimeSectorMapLink",
    className: "AetheriaRuntimeSectorMapLink",
  },
  {
    exportName: "aetheriaRuntimeZoneRenderDocument",
    className: "AetheriaRuntimeZoneRenderDocument",
    schemaName: "zoneRender",
  },
  {
    exportName: "aetheriaRuntimeZoneRenderBodyPose",
    className: "AetheriaRuntimeZoneRenderBodyPose",
  },
  {
    exportName: "aetheriaRuntimeZoneRenderAsteroidBeltPose",
    className: "AetheriaRuntimeZoneRenderAsteroidBeltPose",
  },
  {
    exportName: "aetheriaRuntimeSelectedObjectDocument",
    className: "AetheriaRuntimeSelectedObjectDocument",
    schemaName: "selectedObject",
  },
  {
    exportName: "aetheriaRuntimeInventoryDocument",
    className: "AetheriaRuntimeInventoryDocument",
    schemaName: "inventory",
  },
  {
    exportName: "aetheriaRuntimeRunCheckpointCommit",
    className: "AetheriaRuntimeRunCheckpointCommit",
  },
  {
    exportName: "aetheriaRuntimeZoneSnapshotCommit",
    className: "AetheriaRuntimeZoneSnapshotCommit",
  },
  {
    exportName: "aetheriaRuntimeEntitySnapshotCommit",
    className: "AetheriaRuntimeEntitySnapshotCommit",
  },
  {
    exportName: "aetheriaRuntimeProjectileCommit",
    className: "AetheriaRuntimeProjectileCommit",
  },
  {
    exportName: "aetheriaRuntimeBodySnapshotCommit",
    className: "AetheriaRuntimeBodySnapshotCommit",
  },
  {
    exportName: "aetheriaRuntimeSunVisualCommit",
    className: "AetheriaRuntimeSunVisualCommit",
  },
  {
    exportName: "aetheriaRuntimeEntityStatGridCommit",
    className: "AetheriaRuntimeEntityStatGridCommit",
  },
  {
    exportName: "aetheriaRuntimeCargoBayLoadoutCommit",
    className: "AetheriaRuntimeCargoBayLoadoutCommit",
  },
  {
    exportName: "aetheriaRuntimeLoadoutItemSlotCommit",
    className: "AetheriaRuntimeLoadoutItemSlotCommit",
  },
  {
    exportName: "aetheriaRuntimeLoadoutItemCommit",
    className: "AetheriaRuntimeLoadoutItemCommit",
  },
  {
    exportName: "aetheriaRuntimeGameViewportBounds",
    className: "AetheriaRuntimeViewportBounds",
  },
  {
    exportName: "aetheriaRuntimeGameViewportObject",
    className: "AetheriaRuntimeViewportObject",
  },
  {
    exportName: "aetheriaRuntimeRtsEntityStatus",
    className: "AetheriaRuntimeEntityStatus",
  },
  {
    exportName: "aetheriaRuntimeRtsInventoryItem",
    className: "AetheriaRuntimeInventoryItem",
  },
  {
    exportName: "aetheriaRuntimeRtsGravityInfluence",
    className: "AetheriaRuntimeGravityInfluence",
  },
  {
    exportName: "aetheriaRuntimeRtsBodyView",
    className: "AetheriaRuntimeBodyView",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeScenarioDocument",
    className: "AetheriaRuntimeStarbridgeScenarioDocument",
    schemaName: "starbridgeScenario",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeSessionDocument",
    className: "AetheriaRuntimeStarbridgeSessionDocument",
    schemaName: "starbridgeSession",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeSessionSummaryDocument",
    className: "AetheriaRuntimeStarbridgeSessionSummaryDocument",
    schemaName: "starbridgeSessionSummary",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeStationStockItem",
    className: "AetheriaRuntimeStarbridgeStationStockItem",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeWaveDefinition",
    className: "AetheriaRuntimeStarbridgeWaveDefinition",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeWaveForecast",
    className: "AetheriaRuntimeStarbridgeWaveForecast",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeRuntimeRole",
    className: "AetheriaRuntimeStarbridgeRuntimeRole",
  },
  {
    exportName: "aetheriaRuntimeStarbridgeBaseStatus",
    className: "AetheriaRuntimeStarbridgeBaseStatus",
  },
  {
    exportName: "aetheriaRuntimeVerseAuthorityPolicyDocument",
    className: "AetheriaRuntimeVerseAuthorityPolicyDocument",
    schemaName: "verseAuthorityPolicy",
  },
  {
    exportName: "aetheriaRuntimeAuthorityRule",
    className: "AetheriaRuntimeAuthorityRule",
  },
  {
    exportName: "aetheriaRuntimeAuthorityRuntimeRole",
    className: "AetheriaRuntimeAuthorityRuntimeRole",
  },
  {
    exportName: "aetheriaRuntimeAuthorityLeaseDocument",
    className: "AetheriaRuntimeAuthorityLeaseDocument",
    schemaName: "authorityLease",
  },
].map(document => ({
  ...document,
  slots: parseMessagePackSlots(sourceText, document.className),
  schema: parseCultDocumentSchema(sourceText, document.className),
}));

const generated = render(enums, documents);
const rendererContract = renderRendererContract();
const preload = renderPreload();
const existing = readExisting(outputPath);
const existingRendererContract = readExisting(rendererContractPath);
const existingPreload = readExisting(preloadPath);

if (check) {
  if (existing !== generated || existingRendererContract !== rendererContract || existingPreload !== preload) {
    console.error("RTS generated bindings are stale. Run `npm run generate:rts-bindings`.");
    process.exit(1);
  }
} else {
  writeFileSync(outputPath, generated, "utf8");
  writeFileSync(rendererContractPath, rendererContract, "utf8");
  writeFileSync(preloadPath, preload, "utf8");
}

function readExisting(path) {
  try {
    return readFileSync(path, "utf8");
  } catch {
    return "";
  }
}

function parseEnum(text, enumName) {
  const body = readTypeBody(text, `enum ${enumName}`);
  const entries = [];
  let nextValue = 0;
  for (const rawLine of body.split(/\r?\n/u)) {
    const line = rawLine.replace(/\/\/.*$/u, "").trim().replace(/,$/u, "");
    if (!line)
      continue;

    const match = /^([A-Za-z_][A-Za-z0-9_]*)(?:\s*=\s*(-?\d+))?$/u.exec(line);
    if (!match)
      continue;

    const value = match[2] === undefined ? nextValue : Number.parseInt(match[2], 10);
    entries.push([toCamelCase(match[1]), value]);
    nextValue = value + 1;
  }

  return entries;
}

function parseMessagePackSlots(text, className) {
  const body = readTypeBody(text, `class ${className}`);
  const slots = [];
  const propertyPattern = /\[Key\((\d+)\)\][\s\S]*?public\s+[A-Za-z0-9_<>,.?[\]\s]+\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get;\s*set;\s*\}/gu;
  for (const match of body.matchAll(propertyPattern)) {
    slots.push([toCamelCase(match[2]), Number.parseInt(match[1], 10)]);
  }

  if (slots.length === 0)
    throw new Error(`No [Key] properties found for ${className}.`);

  return slots.sort((left, right) => left[1] - right[1]);
}

function parseCultDocumentSchema(text, className) {
  const classIndex = text.indexOf(`class ${className}`);
  if (classIndex < 0)
    throw new Error(`Could not find ${className}.`);

  const prefix = text.slice(Math.max(0, classIndex - 500), classIndex);
  const matches = [...prefix.matchAll(/\[CultDocument\("([^"]+)",\s*"([^"]+)"\)\]/gu)];
  return matches.length > 0 ? matches[matches.length - 1][2] : "";
}

function readTypeBody(text, declaration) {
  const declarationIndex = text.indexOf(declaration);
  if (declarationIndex < 0)
    throw new Error(`Could not find ${declaration}.`);

  const openIndex = text.indexOf("{", declarationIndex);
  if (openIndex < 0)
    throw new Error(`Could not find body for ${declaration}.`);

  let depth = 0;
  for (let index = openIndex; index < text.length; index += 1) {
    const character = text[index];
    if (character === "{")
      depth += 1;
    if (character === "}") {
      depth -= 1;
      if (depth === 0)
        return text.slice(openIndex + 1, index);
    }
  }

  throw new Error(`Could not read body for ${declaration}.`);
}

function render(enums, documents) {
  const schemas = documents
    .filter(document => document.schema)
    .map(document => `  ${document.schemaName}: "${document.schema}",`)
    .join("\n");
  const enumBlocks = enums
    .map(item => renderEnumBlock(item.exportName, item.entries))
    .join("\n\n");
  const slotBlocks = documents
    .map(document => renderSlotBlock(document.exportName, document.slots))
    .join("\n\n");

  return `// <auto-generated />\n// Generated by scripts/generate-rts-bindings.mjs from Aetheria C# MessagePack documents.\n\nimport { CultMesh } from "cultmesh-ts";\nimport type { CultMeshLiveFeedDiagnostic, CultMeshOperationContext, CultMeshOperationReceipt, CultMeshQuerySource, CultMeshQueryContext, CultMeshQuerySurfaceDiagnostic, CultMeshQueryWatcher, CultMeshRouteHint, CultMeshSurfaceCatalogDiagnostic, CultMeshSurfaceCatalogIndexDiagnostic, CultMeshUnsubscribe, CultMeshVerseContext, CultMeshViewportRequest } from "cultmesh-ts";\nimport type { IpcMain } from "electron";\n\nexport const AetheriaRtsSchemas = {\n${schemas}\n} as const;\n\n${enumBlocks}\n\nexport const AetheriaRtsIpcChannels = {\n${renderIpcChannelLines()}\n} as const;\n\n${slotBlocks}\n\n${renderRtsDocumentTypes("CultMeshViewportRequest")}\n\n${renderRtsOperations()}\n`;
}

function renderEnumBlock(exportName, entries) {
  const lines = entries
    .map(([name, value]) => `  ${name}: ${value},`)
    .join("\n");
  return `export const ${exportName} = {\n${lines}\n} as const;`;
}

function renderSlotBlock(exportName, slots) {
  const slotLines = slots.map(([name, value]) => `  ${name}: ${value},`).join("\n");
  const slotNames = slots.map(([name]) => `  | "${name}"`).join("\n");
  const typeName = `${toPascalCase(exportName)}Slot`;
  return `export type ${typeName} =\n${slotNames};\n\nexport const ${exportName}Slots: Record<${typeName}, number> = {\n${slotLines}\n};`;
}

function toCamelCase(value) {
  return value.slice(0, 1).toLowerCase() + value.slice(1);
}

function toPascalCase(value) {
  return value.slice(0, 1).toUpperCase() + value.slice(1);
}

function renderRtsOperations() {
  return `export type AetheriaRuntimeSetMoveVectorRequest = {
  actorEntityKey: string;
  directionX: number;
  directionY: number;
  scalar: number;
  observedFrameId?: number;
};

export type AetheriaRuntimeSetTargetRequest = {
  actorEntityKey: string;
  targetEntityKey: string;
  observedFrameId?: number;
};

export type AetheriaRuntimeDaemonCommandReceipt = CultMeshOperationReceipt & {
  commandId: string;
};

export type AetheriaRuntimeSelectedObjectRequest = {
  entityIndex: number;
};

export type AetheriaRuntimeViewportFeedRequest = {
  viewport: CultMeshViewportRequest;
  selectedEntityIndex?: number;
};

export type AetheriaRuntimeViewportFeedSnapshot = {
  viewport: ViewportResponse;
  selectedObject: SelectedObjectDocument | null;
  inventory: InventoryDocument | null;
  daemonHealth: DaemonHealthDocument;
  authorityStatus: AuthorityStatusDocument;
  starbridgeSession: StarbridgeSessionDocument;
  assetManifest: AssetManifestDocument;
  receivedAtUtc: string;
  sampleMs: number;
};

export type AetheriaEveSurfaceRequest = {
  surfaceId?: string;
  recordKey?: string;
};

export type AetheriaEveCommandRequest = {
  providerId?: string;
  surfaceId?: string;
  command?: string;
  clientId?: string;
  issuedAtUtc?: string;
  payload?: Record<string, unknown>;
};

export type AetheriaMenuSurfaceDocument = {
  providerId: string;
  providerKind: string;
  title: string;
  version: number;
  updatedAtUtc: string;
  surface: AetheriaMenuSurfaceTree;
  commands: AetheriaMenuSurfaceCommand[];
};

export type AetheriaMenuSurfaceTree = {
  id: string;
  root: AetheriaMenuSurfaceComponent;
  styles: AetheriaMenuStyleToken[];
};

export type AetheriaMenuSurfaceComponent = {
  id: string;
  kind: string;
  props: Record<string, string>;
  layout?: Record<string, string>;
  style?: Record<string, string>;
  embeddedDocuments?: AetheriaMenuEmbeddedDocumentSlot[];
  children: AetheriaMenuSurfaceComponent[];
};

export type AetheriaMenuEmbeddedDocumentSlot = {
  slotId: string;
  documentId: string;
  schemaId: string;
  presentationKind: string;
};

export type AetheriaMenuStyleToken = {
  name: string;
  value: string;
};

export type AetheriaMenuSurfaceCommand = {
  command: string;
  label: string;
  transport: string;
};

export type AetheriaRtsMainClient = {
  mapViewport(request: CultMeshViewportRequest): Promise<ViewportResponse>;
  objectsViewport(request: CultMeshViewportRequest): Promise<ObjectsViewportResponse>;
  gravityViewport(request: CultMeshViewportRequest): Promise<GravityViewportResponse>;
  renderSplatsViewport(request: CultMeshViewportRequest): Promise<RenderSplatsViewportResponse>;
  selectedObject(request: AetheriaRuntimeSelectedObjectRequest): Promise<SelectedObjectDocument>;
  inventory(request: AetheriaRuntimeSelectedObjectRequest): Promise<InventoryDocument>;
  daemonHealth(): Promise<DaemonHealthDocument>;
  authorityStatus(): Promise<AuthorityStatusDocument>;
  starbridgeSession(): Promise<StarbridgeSessionDocument>;
  assetManifest(): Promise<AssetManifestDocument>;
  watchViewportFeed(request: AetheriaRuntimeViewportFeedRequest, callback: (snapshot: AetheriaRuntimeViewportFeedSnapshot) => void): CultMeshUnsubscribe;
  setMoveVector(request: AetheriaRuntimeSetMoveVectorRequest): Promise<AetheriaRuntimeDaemonCommandReceipt>;
  setTarget(request: AetheriaRuntimeSetTargetRequest): Promise<AetheriaRuntimeDaemonCommandReceipt>;
  eveSurface(request: AetheriaEveSurfaceRequest): Promise<AetheriaMenuSurfaceDocument>;
  submitEveCommand(request: AetheriaEveCommandRequest): Promise<AetheriaRuntimeDaemonCommandReceipt>;
  surfaceCatalogDiagnostics(): CultMeshSurfaceCatalogDiagnostic;
  surfaceCatalogIndexDiagnostics(): CultMeshSurfaceCatalogIndexDiagnostic;
};

export type AetheriaRtsHealthProvider = () => unknown;

export function registerAetheriaRtsIpcHandlers(
  ipcMain: IpcMain,
  getClient: () => AetheriaRtsMainClient,
  getHealth: AetheriaRtsHealthProvider,
): void {
  const viewportFeedSubscriptions = new Map<string, CultMeshUnsubscribe>();
  ipcMain.handle(AetheriaRtsIpcChannels.mapViewport, async (_event, request: CultMeshViewportRequest) =>
    getClient().mapViewport(request));
  ipcMain.handle(AetheriaRtsIpcChannels.objectsViewport, async (_event, request: CultMeshViewportRequest) =>
    getClient().objectsViewport(request));
  ipcMain.handle(AetheriaRtsIpcChannels.gravityViewport, async (_event, request: CultMeshViewportRequest) =>
    getClient().gravityViewport(request));
  ipcMain.handle(AetheriaRtsIpcChannels.renderSplatsViewport, async (_event, request: CultMeshViewportRequest) =>
    getClient().renderSplatsViewport(request));
  ipcMain.handle(AetheriaRtsIpcChannels.selectedObject, async (_event, request: AetheriaRuntimeSelectedObjectRequest) =>
    getClient().selectedObject(request));
  ipcMain.handle(AetheriaRtsIpcChannels.inventory, async (_event, request: AetheriaRuntimeSelectedObjectRequest) =>
    getClient().inventory(request));
  ipcMain.handle(AetheriaRtsIpcChannels.daemonHealth, async () => getClient().daemonHealth());
  ipcMain.handle(AetheriaRtsIpcChannels.authorityStatus, async () => getClient().authorityStatus());
  ipcMain.handle(AetheriaRtsIpcChannels.starbridgeSession, async () => getClient().starbridgeSession());
  ipcMain.handle(AetheriaRtsIpcChannels.assetManifest, async () => getClient().assetManifest());
  ipcMain.handle(AetheriaRtsIpcChannels.viewportFeed, async (event, request: { subscriptionId: string; feed: AetheriaRuntimeViewportFeedRequest }) => {
    viewportFeedSubscriptions.get(request.subscriptionId)?.();
    const unsubscribe = getClient().watchViewportFeed(request.feed, snapshot => {
      event.sender.send(AetheriaRtsIpcChannels.viewportFeedUpdate, {
        subscriptionId: request.subscriptionId,
        snapshot,
      });
    });
    viewportFeedSubscriptions.set(request.subscriptionId, unsubscribe);
  });
  ipcMain.handle(AetheriaRtsIpcChannels.viewportFeedStop, (_event, subscriptionId: string) => {
    viewportFeedSubscriptions.get(subscriptionId)?.();
    viewportFeedSubscriptions.delete(subscriptionId);
  });
  ipcMain.handle(AetheriaRtsIpcChannels.setMoveVector, async (_event, request: AetheriaRuntimeSetMoveVectorRequest) =>
    getClient().setMoveVector(request));
  ipcMain.handle(AetheriaRtsIpcChannels.setTarget, async (_event, request: AetheriaRuntimeSetTargetRequest) =>
    getClient().setTarget(request));
  ipcMain.handle(AetheriaRtsIpcChannels.eveSurface, async (_event, request: AetheriaEveSurfaceRequest) =>
    getClient().eveSurface(request));
  ipcMain.handle(AetheriaRtsIpcChannels.submitEveCommand, async (_event, request: AetheriaEveCommandRequest) =>
    getClient().submitEveCommand(request));
  ipcMain.handle(AetheriaRtsIpcChannels.surfaceCatalog, () => getClient().surfaceCatalogDiagnostics());
  ipcMain.handle(AetheriaRtsIpcChannels.surfaceCatalogIndex, () => getClient().surfaceCatalogIndexDiagnostics());
  ipcMain.handle(AetheriaRtsIpcChannels.health, () => getHealth());
}

export type AetheriaRuntimeDaemonCommandSender = (
  commandId: string,
  issuedAtUtc: string,
  command: unknown[],
  context: CultMeshOperationContext,
) => Promise<void>;

export type AetheriaRuntimeViewportQueryExecutor<TResult> = (
  request: CultMeshViewportRequest,
  context: CultMeshQueryContext,
) => Promise<TResult>;

export type AetheriaRuntimeQueryExecutor<TParameters, TResult> = (
  request: TParameters,
  context: CultMeshQueryContext,
) => Promise<TResult>;

export type AetheriaRuntimeRtsQueryExecutors = {
  mapViewport: AetheriaRuntimeViewportQueryExecutor<ViewportResponse>;
  objectsViewport: AetheriaRuntimeViewportQueryExecutor<ObjectsViewportResponse>;
  gravityViewport: AetheriaRuntimeViewportQueryExecutor<GravityViewportResponse>;
  renderSplatsViewport: AetheriaRuntimeViewportQueryExecutor<RenderSplatsViewportResponse>;
  selectedObject: AetheriaRuntimeQueryExecutor<AetheriaRuntimeSelectedObjectRequest, SelectedObjectDocument>;
  inventory: AetheriaRuntimeQueryExecutor<AetheriaRuntimeSelectedObjectRequest, InventoryDocument>;
  daemonHealth: AetheriaRuntimeQueryExecutor<void, DaemonHealthDocument>;
  authorityStatus: AetheriaRuntimeQueryExecutor<void, AuthorityStatusDocument>;
  starbridgeSession: AetheriaRuntimeQueryExecutor<void, StarbridgeSessionDocument>;
  assetManifest: AetheriaRuntimeQueryExecutor<void, AssetManifestDocument>;
};

export type AetheriaRuntimeRtsQueryWatchers = Partial<{
  mapViewport: CultMeshQueryWatcher<CultMeshViewportRequest, ViewportResponse>;
  objectsViewport: CultMeshQueryWatcher<CultMeshViewportRequest, ObjectsViewportResponse>;
  gravityViewport: CultMeshQueryWatcher<CultMeshViewportRequest, GravityViewportResponse>;
  renderSplatsViewport: CultMeshQueryWatcher<CultMeshViewportRequest, RenderSplatsViewportResponse>;
  selectedObject: CultMeshQueryWatcher<AetheriaRuntimeSelectedObjectRequest, SelectedObjectDocument>;
  inventory: CultMeshQueryWatcher<AetheriaRuntimeSelectedObjectRequest, InventoryDocument>;
  daemonHealth: CultMeshQueryWatcher<void, DaemonHealthDocument>;
  authorityStatus: CultMeshQueryWatcher<void, AuthorityStatusDocument>;
  starbridgeSession: CultMeshQueryWatcher<void, StarbridgeSessionDocument>;
  assetManifest: CultMeshQueryWatcher<void, AssetManifestDocument>;
}>;

export type AetheriaRuntimeRtsQueryDiagnostic = CultMeshQuerySurfaceDiagnostic;

export type AetheriaRuntimeRtsLiveFeedDiagnostic = CultMeshLiveFeedDiagnostic;

export type AetheriaRuntimeRtsSurfaceCatalogDiagnostic = CultMeshSurfaceCatalogDiagnostic;

export type AetheriaRuntimeRtsOperationHandles = ReturnType<typeof createAetheriaRuntimeRtsOperationHandles>;

export type AetheriaRuntimeGameDocuments = ReturnType<typeof createAetheriaRuntimeGameDocuments>;

export type AetheriaRuntimeRtsStatePointers = AetheriaRuntimeGameDocuments;

export type AetheriaRuntimeRtsDocumentResolvers = Partial<{
  daemonFrame: (context: CultMeshQueryContext) => Promise<unknown>;
  daemonHealth: (context: CultMeshQueryContext) => Promise<unknown>;
  authorityPolicy: (context: CultMeshQueryContext) => Promise<unknown>;
  starbridgeSession: (context: CultMeshQueryContext) => Promise<unknown>;
  assetManifest: (context: CultMeshQueryContext) => Promise<unknown>;
}>;

export type AetheriaRuntimeRtsStatePointerResolvers = AetheriaRuntimeRtsDocumentResolvers;

export const AetheriaRuntimeRtsDocumentSources = {
  daemonFrame: CultMesh.querySource("daemon:aetheria.frame.latest.v1", {
    schemaId: AetheriaRtsSchemas.daemonFrame,
    description: "latest daemon frame"
  }),
  daemonHealth: CultMesh.querySource("daemon:aetheria.health.latest.v1", {
    schemaId: AetheriaRtsSchemas.daemonHealth,
    description: "latest daemon health"
  }),
  authorityPolicy: CultMesh.querySource("daemon:aetheria.authority.policy.latest.v1", {
    schemaId: AetheriaRtsSchemas.verseAuthorityPolicy,
    description: "latest Verse authority policy"
  }),
  starbridgeSession: CultMesh.querySource("daemon:aetheria.starbridge.session.latest.v1", {
    schemaId: AetheriaRtsSchemas.starbridgeSessionSummary,
    description: "latest Starbridge session summary"
  }),
  assetManifest: CultMesh.querySource("daemon:aetheria.asset_manifest.latest.v1", {
    schemaId: AetheriaRtsSchemas.assetManifest,
    description: "latest daemon asset manifest"
  })
} as const;

export function createAetheriaRuntimeGameDocuments(
  routeHint: CultMeshRouteHint = CultMesh.routeHint(),
  resolvers: AetheriaRuntimeRtsDocumentResolvers = {},
) {
  return {
    daemonFrame: CultMesh.document(
      AetheriaRuntimeRtsDocumentSources.daemonFrame.sourceId,
      { schemaId: AetheriaRtsSchemas.daemonFrame },
      async (context) => resolvers.daemonFrame?.(context),
      {
        routeHint,
        sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame],
      },
    ),
    daemonHealth: CultMesh.document(
      AetheriaRuntimeRtsDocumentSources.daemonHealth.sourceId,
      { schemaId: AetheriaRtsSchemas.daemonHealth },
      async (context) => resolvers.daemonHealth?.(context),
      {
        routeHint,
        sources: [AetheriaRuntimeRtsDocumentSources.daemonHealth],
      },
    ),
    authorityPolicy: CultMesh.document(
      AetheriaRuntimeRtsDocumentSources.authorityPolicy.sourceId,
      { schemaId: AetheriaRtsSchemas.verseAuthorityPolicy },
      async (context) => resolvers.authorityPolicy?.(context),
      {
        routeHint,
        sources: [AetheriaRuntimeRtsDocumentSources.authorityPolicy],
      },
    ),
    starbridgeSession: CultMesh.document(
      AetheriaRuntimeRtsDocumentSources.starbridgeSession.sourceId,
      { schemaId: AetheriaRtsSchemas.starbridgeSessionSummary },
      async (context) => resolvers.starbridgeSession?.(context),
      {
        routeHint,
        sources: [AetheriaRuntimeRtsDocumentSources.starbridgeSession],
      },
    ),
    assetManifest: CultMesh.document(
      AetheriaRuntimeRtsDocumentSources.assetManifest.sourceId,
      { schemaId: AetheriaRtsSchemas.assetManifest },
      async (context) => resolvers.assetManifest?.(context),
      {
        routeHint,
        sources: [AetheriaRuntimeRtsDocumentSources.assetManifest],
      },
    ),
  } as const;
}

export const createAetheriaRuntimeRtsStatePointers = createAetheriaRuntimeGameDocuments;

export function createAetheriaRuntimeRtsQueryHandles(
  executors: AetheriaRuntimeRtsQueryExecutors,
  routeHint: CultMeshRouteHint = CultMesh.routeHint(),
  watchers: AetheriaRuntimeRtsQueryWatchers = {},
) {
  return {
    mapViewport: CultMesh.query<CultMeshViewportRequest, ViewportResponse>(
      AetheriaRtsSchemas.gameViewport,
      executors.mapViewport,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame], watchQuery: watchers.mapViewport },
    ),
    objectsViewport: CultMesh.query<CultMeshViewportRequest, ObjectsViewportResponse>(
      AetheriaRtsSchemas.objectsViewport,
      executors.objectsViewport,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame], watchQuery: watchers.objectsViewport },
    ),
    gravityViewport: CultMesh.query<CultMeshViewportRequest, GravityViewportResponse>(
      AetheriaRtsSchemas.gravityViewport,
      executors.gravityViewport,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame], watchQuery: watchers.gravityViewport },
    ),
    renderSplatsViewport: CultMesh.query<CultMeshViewportRequest, RenderSplatsViewportResponse>(
      AetheriaRtsSchemas.renderSplatsViewport,
      executors.renderSplatsViewport,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame], watchQuery: watchers.renderSplatsViewport },
    ),
    selectedObject: CultMesh.query<AetheriaRuntimeSelectedObjectRequest, SelectedObjectDocument>(
      AetheriaRtsSchemas.selectedObject,
      executors.selectedObject,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame], watchQuery: watchers.selectedObject },
    ),
    inventory: CultMesh.query<AetheriaRuntimeSelectedObjectRequest, InventoryDocument>(
      AetheriaRtsSchemas.inventory,
      executors.inventory,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonFrame], watchQuery: watchers.inventory },
    ),
    daemonHealth: CultMesh.query<void, DaemonHealthDocument>(
      AetheriaRtsSchemas.daemonHealth,
      executors.daemonHealth,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.daemonHealth], watchQuery: watchers.daemonHealth },
    ),
    authorityStatus: CultMesh.query<void, AuthorityStatusDocument>(
      AetheriaRtsSchemas.verseAuthorityPolicy,
      executors.authorityStatus,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.authorityPolicy], watchQuery: watchers.authorityStatus },
    ),
    starbridgeSession: CultMesh.query<void, StarbridgeSessionDocument>(
      AetheriaRtsSchemas.starbridgeSessionSummary,
      executors.starbridgeSession,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.starbridgeSession], watchQuery: watchers.starbridgeSession },
    ),
    assetManifest: CultMesh.query<void, AssetManifestDocument>(
      AetheriaRtsSchemas.assetManifest,
      executors.assetManifest,
      { routeHint, sources: [AetheriaRuntimeRtsDocumentSources.assetManifest], watchQuery: watchers.assetManifest },
    ),
  } as const;
}

export function describeAetheriaRuntimeRtsQueryHandles(
  handles: ReturnType<typeof createAetheriaRuntimeRtsQueryHandles>,
) {
  return {
    mapViewport: describeAetheriaRuntimeRtsQuerySurface(handles.mapViewport),
    objectsViewport: describeAetheriaRuntimeRtsQuerySurface(handles.objectsViewport),
    gravityViewport: describeAetheriaRuntimeRtsQuerySurface(handles.gravityViewport),
    renderSplatsViewport: describeAetheriaRuntimeRtsQuerySurface(handles.renderSplatsViewport),
    selectedObject: describeAetheriaRuntimeRtsQuerySurface(handles.selectedObject),
    inventory: describeAetheriaRuntimeRtsQuerySurface(handles.inventory),
    daemonHealth: describeAetheriaRuntimeRtsQuerySurface(handles.daemonHealth),
    authorityStatus: describeAetheriaRuntimeRtsQuerySurface(handles.authorityStatus),
    starbridgeSession: describeAetheriaRuntimeRtsQuerySurface(handles.starbridgeSession),
    assetManifest: describeAetheriaRuntimeRtsQuerySurface(handles.assetManifest),
  } as const;
}

export function describeAetheriaRuntimeRtsSurfaceCatalog(
  handles: ReturnType<typeof createAetheriaRuntimeRtsQueryHandles>,
  operations?: AetheriaRuntimeRtsOperationHandles,
  documents: AetheriaRuntimeGameDocuments = createAetheriaRuntimeGameDocuments(),
): AetheriaRuntimeRtsSurfaceCatalogDiagnostic {
  return CultMesh.describeSurfaceCatalog(
    "gamecult.aetheria.surfaces.v1",
    [
      CultMesh.describeSurface(documents.daemonFrame),
      CultMesh.describeSurface(documents.daemonHealth),
      CultMesh.describeSurface(documents.authorityPolicy),
      CultMesh.describeSurface(documents.starbridgeSession),
      CultMesh.describeSurface(documents.assetManifest),
      CultMesh.describeSurface(handles.mapViewport),
      CultMesh.describeSurface(handles.objectsViewport),
      CultMesh.describeSurface(handles.gravityViewport),
      CultMesh.describeSurface(handles.renderSplatsViewport),
      CultMesh.describeSurface(handles.selectedObject),
      CultMesh.describeSurface(handles.inventory),
      CultMesh.describeSurface(handles.daemonHealth),
      CultMesh.describeSurface(handles.authorityStatus),
      CultMesh.describeSurface(handles.starbridgeSession),
      CultMesh.describeSurface(handles.assetManifest),
      ...(operations
        ? [
            CultMesh.describeSurface(operations.setMoveVector),
            CultMesh.describeSurface(operations.setTarget),
          ]
        : []),
    ],
  );
}

function describeAetheriaRuntimeRtsQuerySurface(surface: {
  queryId: string;
  routeHint: CultMeshRouteHint;
  sources: readonly CultMeshQuerySource[];
}): AetheriaRuntimeRtsQueryDiagnostic {
  return CultMesh.describeQuerySurface(surface);
}

export function describeAetheriaRuntimeRtsLiveFeedSurface(surface: {
  feedId: string;
  routeHint: CultMeshRouteHint;
  sources: readonly CultMeshQuerySource[];
}): AetheriaRuntimeRtsLiveFeedDiagnostic {
  return CultMesh.describeLiveFeed(surface);
}

export function createAetheriaRuntimeRtsOperationHandles(
  sendCommand: AetheriaRuntimeDaemonCommandSender,
  createCommandId: () => string = createDefaultCommandId,
) {
  return {
    setMoveVector: CultMesh.operation<AetheriaRuntimeSetMoveVectorRequest, AetheriaRuntimeDaemonCommandReceipt>(
      "gamecult.aetheria.pilot.set_move_vector.v1",
      async (request, context) => {
        const commandId = context.idempotencyKey ?? createCommandId();
        const issuedAtUtc = new Date().toISOString();
        await sendCommand(
          commandId,
          issuedAtUtc,
          encodeSetMoveVectorCommand(commandId, issuedAtUtc, context.runtimeId, request),
          context,
        );
        return {
          commandId,
          ...CultMesh.operationReceipt("gamecult.aetheria.pilot.set_move_vector.v1", true, { route: context.routeHint }),
        };
      },
    ),
    setTarget: CultMesh.operation<AetheriaRuntimeSetTargetRequest, AetheriaRuntimeDaemonCommandReceipt>(
      "gamecult.aetheria.pilot.set_target.v1",
      async (request, context) => {
        const commandId = context.idempotencyKey ?? createCommandId();
        const issuedAtUtc = new Date().toISOString();
        await sendCommand(
          commandId,
          issuedAtUtc,
          encodeSetTargetCommand(commandId, issuedAtUtc, context.runtimeId, request),
          context,
        );
        return {
          commandId,
          ...CultMesh.operationReceipt("gamecult.aetheria.pilot.set_target.v1", true, { route: context.routeHint }),
        };
      },
    ),
  } as const;
}

export type AetheriaRuntimeRtsVerseHandles = ReturnType<typeof createAetheriaRuntimeRtsVerseHandles>;

export function createAetheriaRuntimeRtsVerseHandles(
  queryVerse: CultMeshVerseContext,
  commandVerse: CultMeshVerseContext,
  queries: ReturnType<typeof createAetheriaRuntimeRtsQueryHandles>,
  operations: AetheriaRuntimeRtsOperationHandles,
  documents: AetheriaRuntimeGameDocuments = createAetheriaRuntimeGameDocuments(),
) {
  const mapViewport = CultMesh.bindQuery(queryVerse, queries.mapViewport);
  const objectsViewport = CultMesh.bindQuery(queryVerse, queries.objectsViewport);
  const gravityViewport = CultMesh.bindQuery(queryVerse, queries.gravityViewport);
  const renderSplatsViewport = CultMesh.bindQuery(queryVerse, queries.renderSplatsViewport);
  const selectedObject = CultMesh.bindQuery(queryVerse, queries.selectedObject);
  const inventory = CultMesh.bindQuery(queryVerse, queries.inventory);
  const daemonHealth = CultMesh.bindQuery(queryVerse, queries.daemonHealth);
  const authorityStatus = CultMesh.bindQuery(queryVerse, queries.authorityStatus);
  const starbridgeSession = CultMesh.bindQuery(queryVerse, queries.starbridgeSession);
  const assetManifest = CultMesh.bindQuery(queryVerse, queries.assetManifest);
  const daemonFrameDocument = CultMesh.bindDocument(queryVerse, documents.daemonFrame);
  const daemonHealthDocument = CultMesh.bindDocument(queryVerse, documents.daemonHealth);
  const authorityPolicyDocument = CultMesh.bindDocument(queryVerse, documents.authorityPolicy);
  const starbridgeSessionDocument = CultMesh.bindDocument(queryVerse, documents.starbridgeSession);
  const assetManifestDocument = CultMesh.bindDocument(queryVerse, documents.assetManifest);
  const setMoveVector = CultMesh.bindOperation(commandVerse, operations.setMoveVector);
  const setTarget = CultMesh.bindOperation(commandVerse, operations.setTarget);

  return {
    zone: (_zoneId = "current") => ({
      viewport: {
        within: (request: CultMeshViewportRequest) =>
          mapViewport.execute(request),
        watch: (
          request: CultMeshViewportRequest,
          callback: (value: ViewportResponse) => void,
        ) => mapViewport.watch(request, callback),
      },
      objects: {
        visibleWithin: (request: CultMeshViewportRequest) =>
          objectsViewport.execute(request),
        watchVisibleWithin: (
          request: CultMeshViewportRequest,
          callback: (value: ObjectsViewportResponse) => void,
        ) => objectsViewport.watch(request, callback),
      },
      gravity: {
        within: (request: CultMeshViewportRequest) =>
          gravityViewport.execute(request),
        watchWithin: (
          request: CultMeshViewportRequest,
          callback: (value: GravityViewportResponse) => void,
        ) => gravityViewport.watch(request, callback),
      },
      renderSplats: {
        within: (request: CultMeshViewportRequest) =>
          renderSplatsViewport.execute(request),
        watchWithin: (
          request: CultMeshViewportRequest,
          callback: (value: RenderSplatsViewportResponse) => void,
        ) => renderSplatsViewport.watch(request, callback),
      },
    }),
    entity: (entityKey: string) => ({
      pilot: {
        move: (
          direction: { readonly x: number; readonly y: number },
          options: { scalar?: number; observedFrameId?: number; commandId?: string } = {},
        ) => setMoveVector.invoke({
          actorEntityKey: entityKey,
          directionX: direction.x,
          directionY: direction.y,
          scalar: options.scalar ?? 1,
          observedFrameId: options.observedFrameId,
        }, { idempotencyKey: options.commandId }),
        target: (
          targetEntityKey: string,
          options: { observedFrameId?: number; commandId?: string } = {},
        ) => setTarget.invoke({
          actorEntityKey: entityKey,
          targetEntityKey,
          observedFrameId: options.observedFrameId,
        }, { idempotencyKey: options.commandId }),
      },
    }),
    selectedObject: (entityIndex: number) =>
      selectedObject.execute({ entityIndex }),
    inventory: (entityIndex: number) =>
      inventory.execute({ entityIndex }),
    daemon: {
      state: {
        frame: () => daemonFrameDocument.latest(),
        health: () => daemonHealthDocument.latest(),
        authorityPolicy: () => authorityPolicyDocument.latest(),
        starbridgeSession: () => starbridgeSessionDocument.latest(),
        assetManifest: () => assetManifestDocument.latest(),
      },
      health: () => daemonHealth.execute(undefined),
      authorityStatus: () => authorityStatus.execute(undefined),
      starbridgeSession: () => starbridgeSession.execute(undefined),
      assetManifest: () => assetManifest.execute(undefined),
    },
  } as const;
}

export function encodeSetMoveVectorCommand(
  commandId: string,
  issuedAtUtc: string,
  runtimeId: string,
  request: AetheriaRuntimeSetMoveVectorRequest,
): unknown[] {
  const command = createBaseCommand(commandId, issuedAtUtc, runtimeId, request.actorEntityKey, request.observedFrameId);
  command[aetheriaRuntimeDaemonCommandDocumentSlots.kind] = AetheriaRuntimeDaemonCommandKinds.setMoveVector;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.directionX] = request.directionX;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.directionY] = request.directionY;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.scalarValue] = Math.min(1, Math.max(0, request.scalar));
  return command;
}

export function encodeSetTargetCommand(
  commandId: string,
  issuedAtUtc: string,
  runtimeId: string,
  request: AetheriaRuntimeSetTargetRequest,
): unknown[] {
  const command = createBaseCommand(commandId, issuedAtUtc, runtimeId, request.actorEntityKey, request.observedFrameId);
  command[aetheriaRuntimeDaemonCommandDocumentSlots.kind] = AetheriaRuntimeDaemonCommandKinds.setTarget;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.targetEntityKey] = request.targetEntityKey;
  return command;
}

function createBaseCommand(
  commandId: string,
  issuedAtUtc: string,
  runtimeId: string,
  actorEntityKey: string,
  observedFrameId?: number,
): unknown[] {
  const command = new Array<unknown>(Object.keys(aetheriaRuntimeDaemonCommandDocumentSlots).length).fill(null);
  command[aetheriaRuntimeDaemonCommandDocumentSlots.schema] = AetheriaRtsSchemas.daemonCommand;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.commandId] = commandId;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.clientId] = runtimeId;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.issuedAtUtc] = issuedAtUtc;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.sessionId] = "local";
  command[aetheriaRuntimeDaemonCommandDocumentSlots.observedFrameId] = observedFrameId ?? -1;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.kind] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.actorEntityKey] = actorEntityKey;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.targetEntityKey] = "";
  command[aetheriaRuntimeDaemonCommandDocumentSlots.targetZoneIndex] = -1;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.equipmentIndex] = -1;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.behaviorIndex] = -1;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.weaponGroup] = -1;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.positionX] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.positionY] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.positionZ] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.directionX] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.directionY] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.scalarValue] = 0;
  command[aetheriaRuntimeDaemonCommandDocumentSlots.textValue] = "";
  command[aetheriaRuntimeDaemonCommandDocumentSlots.authorRuntimeId] = "";
  command[aetheriaRuntimeDaemonCommandDocumentSlots.subjectKey] = "";
  command[aetheriaRuntimeDaemonCommandDocumentSlots.claimKind] = "";
  return command;
}

function createDefaultCommandId(): string {
  const cryptoApi = globalThis.crypto as { randomUUID?: () => string } | undefined;
  return cryptoApi?.randomUUID?.() ?? \`command-\${Date.now().toString(36)}-\${Math.random().toString(36).slice(2)}\`;
}`;
}

function renderRendererContract() {
  return `// <auto-generated />
// Generated by scripts/generate-rts-bindings.mjs from the RTS CultMesh surface contract.

import type {
  CultMeshOperationReceipt,
  CultMeshSurfaceCatalogDiagnostic,
  CultMeshSurfaceCatalogIndexDiagnostic,
  CultMeshViewportRequest,
} from "cultmesh-ts";

export type Viewport = CultMeshViewportRequest;

export type AetheriaRuntimeSetMoveVectorRequest = {
  actorEntityKey: string;
  directionX: number;
  directionY: number;
  scalar: number;
  observedFrameId?: number;
};

export type AetheriaRuntimeSetTargetRequest = {
  actorEntityKey: string;
  targetEntityKey: string;
  observedFrameId?: number;
};

export type AetheriaRuntimeDaemonCommandReceipt = CultMeshOperationReceipt & {
  commandId: string;
};

export type AetheriaRuntimeSelectedObjectRequest = {
  entityIndex: number;
};

export type AetheriaRuntimeViewportFeedRequest = {
  viewport: Viewport;
  selectedEntityIndex?: number;
};

export type AetheriaRuntimeViewportFeedSnapshot = {
  viewport: ViewportResponse;
  selectedObject: SelectedObjectDocument | null;
  inventory: InventoryDocument | null;
  daemonHealth: DaemonHealthDocument;
  authorityStatus: AuthorityStatusDocument;
  starbridgeSession: StarbridgeSessionDocument;
  assetManifest: AssetManifestDocument;
  receivedAtUtc: string;
  sampleMs: number;
};

${renderRtsDocumentTypes("Viewport")}

export type AetheriaEveSurfaceRequest = {
  surfaceId?: string;
  recordKey?: string;
};

export type AetheriaEveCommandRequest = {
  providerId?: string;
  surfaceId?: string;
  command?: string;
  clientId?: string;
  issuedAtUtc?: string;
  payload?: Record<string, unknown>;
};

export type AetheriaMenuSurfaceDocument = {
  providerId: string;
  providerKind: string;
  title: string;
  version: number;
  updatedAtUtc: string;
  surface: AetheriaMenuSurfaceTree;
  commands: AetheriaMenuSurfaceCommand[];
};

export type AetheriaMenuSurfaceTree = {
  id: string;
  root: AetheriaMenuSurfaceComponent;
  styles: AetheriaMenuStyleToken[];
};

export type AetheriaMenuSurfaceComponent = {
  id: string;
  kind: string;
  props: Record<string, string>;
  embeddedDocuments?: AetheriaMenuEmbeddedDocumentSlot[];
  children: AetheriaMenuSurfaceComponent[];
};

export type AetheriaMenuEmbeddedDocumentSlot = {
  slotId: string;
  documentId: string;
  schemaId: string;
  presentationKind: string;
};

export type AetheriaMenuStyleToken = {
  name: string;
  value: string;
};

export type AetheriaMenuSurfaceCommand = {
  command: string;
  label: string;
  transport: string;
};

export type AetheriaRtsApi = {
  mapViewport(request: Viewport): Promise<ViewportResponse>;
  objectsViewport(request: Viewport): Promise<ObjectsViewportResponse>;
  gravityViewport(request: Viewport): Promise<GravityViewportResponse>;
  renderSplatsViewport(request: Viewport): Promise<RenderSplatsViewportResponse>;
  selectedObject(request: AetheriaRuntimeSelectedObjectRequest): Promise<SelectedObjectDocument>;
  inventory(request: AetheriaRuntimeSelectedObjectRequest): Promise<InventoryDocument>;
  daemonHealth(): Promise<DaemonHealthDocument>;
  authorityStatus(): Promise<AuthorityStatusDocument>;
  starbridgeSession(): Promise<StarbridgeSessionDocument>;
  assetManifest(): Promise<AssetManifestDocument>;
  watchViewportFeed(request: AetheriaRuntimeViewportFeedRequest, callback: (snapshot: AetheriaRuntimeViewportFeedSnapshot) => void): () => void;
  setMoveVector(request: AetheriaRuntimeSetMoveVectorRequest): Promise<AetheriaRuntimeDaemonCommandReceipt>;
  setTarget(request: AetheriaRuntimeSetTargetRequest): Promise<AetheriaRuntimeDaemonCommandReceipt>;
  surfaceCatalog(): Promise<CultMeshSurfaceCatalogDiagnostic>;
  surfaceCatalogIndex(): Promise<CultMeshSurfaceCatalogIndexDiagnostic>;
  eveSurface(request: AetheriaEveSurfaceRequest): Promise<AetheriaMenuSurfaceDocument>;
  submitEveCommand(request: AetheriaEveCommandRequest): Promise<AetheriaRuntimeDaemonCommandReceipt>;
  windowControl(action: "minimize" | "maximize" | "close"): Promise<void>;
  health(): Promise<unknown>;
};
`;
}

function renderRtsDocumentTypes(viewportTypeName) {
  return `export type ViewportResponse = {
  schema: string;
  frameId: number;
  publishedAtUtc: string;
  simulationTimeSeconds: number;
  runId: string;
  zoneIndex: number;
  zoneName: string;
  currentEntityKey: string;
  viewport: ${viewportTypeName};
  controlledEntityIndices: number[];
  objects: ViewObject[];
  gravityInfluences: GravityInfluence[];
  bodies: BodyView[];
};

export type ObjectsViewportResponse = {
  schema: string;
  frameId: number;
  publishedAtUtc: string;
  simulationTimeSeconds: number;
  runId: string;
  zoneIndex: number;
  zoneName: string;
  currentEntityKey: string;
  viewport: ${viewportTypeName};
  controlledEntityIndices: number[];
  objects: ViewObject[];
};

export type GravityViewportResponse = {
  schema: string;
  frameId: number;
  publishedAtUtc: string;
  simulationTimeSeconds: number;
  runId: string;
  zoneIndex: number;
  zoneName: string;
  viewport: ${viewportTypeName};
  gravityInfluences: GravityInfluence[];
  bodies: BodyView[];
};

export type RenderSplatsViewportResponse = {
  schema: string;
  frameId: number;
  publishedAtUtc: string;
  simulationTimeSeconds: number;
  runId: string;
  zoneIndex: number;
  zoneName: string;
  viewport: ${viewportTypeName};
  layers: RenderSplatLayerDefinition[];
  splats: RenderSplatSoa;
};

export type RenderSplatLayerDefinition = {
  layerKey: string;
  displayName: string;
  channel: number;
  blendMode: string;
  graphicsFormat: string;
  clearBeforeDraw: boolean;
  clearR: number;
  clearG: number;
  clearB: number;
  clearA: number;
};

export type RenderSplatSoa = {
  count: number;
  centerX: number[];
  centerY: number[];
  halfExtentX: number[];
  halfExtentY: number[];
  rotationCos: number[];
  rotationSin: number[];
  channel: number[];
  falloff: number[];
  valueR: number[];
  valueG: number[];
  valueB: number[];
  valueA: number[];
  sourceKey: string[];
  layerIndex: number[];
  sourceKind: number[];
  frequencyX: number[];
  frequencyY: number[];
  phaseX: number[];
  phaseY: number[];
  animationSpeed: number[];
  sourceFlags: number[];
};

export type ViewObject = {
  entityIndex: number;
  entityKey: string;
  displayName: string;
  kind: string;
  factionKey: string;
  x: number;
  y: number;
  z: number;
  directionX: number;
  directionY: number;
  velocityX: number;
  velocityY: number;
  controlled: boolean;
  targetEntityIndex: number;
  isActive: boolean;
  visibility: number;
  iconAsset: AssetRef;
  status: EntityStatus;
  inventory: InventoryItem[];
};

export type EntityStatus = {
  hull: number;
  shield: number;
  heat: number;
};

export type InventoryItem = {
  source: string;
  itemKey: string;
  quantity: number;
  quality: number;
  durability: number;
  enabled: boolean;
  iconAsset: AssetRef;
};

export type SelectedObjectDocument = {
  schema: string;
  frameId: number;
  runId: string;
  zoneIndex: number;
  entityIndex: number;
  selected: ViewObject | null;
};

export type InventoryDocument = {
  schema: string;
  frameId: number;
  runId: string;
  zoneIndex: number;
  entityIndex: number;
  entityKey: string;
  items: InventoryItem[];
  equipment: InventoryItem[];
  cargo: InventoryItem[];
};

export type DaemonHealthDocument = {
  schema: string;
  daemonId: string;
  verseId: string;
  publishedAtUtc: string;
  statePath: string;
  frameId: number;
  observedCommandCount: number;
  appliedCommandCount: number;
  rejectedCommandCount: number;
  status: string;
  publicationSource: string;
  transport: string;
  commandBoundaryPath: string;
};

export type AuthorityStatusDocument = {
  schema: string;
  verseId: string;
  policyId: string;
  ruleVersion: string;
  hostRuntimeId: string;
  defaultMode: string;
  updatedAtUtc: string;
  rules: AuthorityRuleDocument[];
};

export type AuthorityRuleDocument = {
  ruleId: string;
  subjectPrefix: string;
  claimKinds: string[];
  mode: string;
  runtimeIds: string[];
  leaseScope: string;
  priority: number;
};

export type StarbridgeSessionDocument = {
  schema: string;
  frameId: number;
  publishedAtUtc: string;
  sessionId: string;
  scenarioId: string;
  scenarioName: string;
  runId: string;
  zoneIndex: number;
  zoneName: string;
  phase: string;
  currentWaveIndex: number;
  baseStatus: StarbridgeBaseStatusDocument;
  stationStock: StarbridgeStationStockItemDocument[];
  waveForecast: StarbridgeWaveForecastDocument[];
  runtimeRoles: StarbridgeRuntimeRoleDocument[];
};

export type StarbridgeBaseStatusDocument = {
  entityKey: string;
  displayName: string;
  hull: number;
  shield: number;
  heat: number;
  isActive: boolean;
};

export type StarbridgeStationStockItemDocument = {
  itemKey: string;
  quantity: number;
  quality: number;
  durability: number;
  source: string;
  iconAsset: AssetRef;
};

export type AssetManifestDocument = {
  schema: string;
  publishedAtUtc: string;
  runId: string;
  baseUri: string;
  assets: AssetManifestEntry[];
};

export type AssetManifestEntry = {
  ref: AssetRef;
  sizeBytes: number;
  width: number;
  height: number;
  tags: string[];
};

export type AssetRef = {
  assetKey: string;
  kind: string;
  uri: string;
  transport: string;
  contentHash: string;
  mimeType: string;
  metadata: Record<string, string>;
};

export type StarbridgeWaveForecastDocument = {
  waveIndex: number;
  displayName: string;
  attackerKeys: string[];
  bossKey: string;
  recoveredTechnologyKeys: string[];
};

export type StarbridgeRuntimeRoleDocument = {
  runtimeId: string;
  role: string;
  entityKey: string;
};

export type GravityInfluence = {
  bodyKey: string;
  orbitKey: string;
  kind: string;
  x: number;
  y: number;
  radius: number;
  gravityDepth: number;
  gravityDepthExponent: number;
  waveRadius: number;
  waveDepth: number;
  waveSpeed: number;
};

export type BodyView = {
  bodyKey: string;
  orbitKey: string;
  name: string;
  kind: string;
  x: number;
  y: number;
  radius: number;
  isAsteroidBelt: boolean;
  iconAsset: AssetRef;
  iconSize: number;
};
`;
}

function renderIpcChannelLines() {
  return ipcChannels
    .map(([name, channel]) => `  ${name}: "${channel}",`)
    .join("\n");
}

function renderPreload() {
  const channelLines = renderIpcChannelLines();
  const apiLines = ipcChannels
    .filter(([name]) => name !== "viewportFeedUpdate" &&
      name !== "viewportFeedStop")
    .map(([name]) => {
      if (name === "viewportFeed") {
        return `  watchViewportFeed: (request, callback) => {
    const subscriptionId = \`viewport-feed-\${Date.now().toString(36)}-\${Math.random().toString(36).slice(2)}\`;
    const listener = (_event, message) => {
      if (message?.subscriptionId === subscriptionId) {
        callback(message.snapshot);
      }
    };
    ipcRenderer.on(channels.viewportFeedUpdate, listener);
    void ipcRenderer.invoke(channels.viewportFeed, { subscriptionId, feed: request });
    return () => {
      ipcRenderer.off(channels.viewportFeedUpdate, listener);
      void ipcRenderer.invoke(channels.viewportFeedStop, subscriptionId);
    };
  },`;
      }
      const hasNoArguments = name === "health" ||
        name === "daemonHealth" ||
        name === "authorityStatus" ||
        name === "starbridgeSession" ||
        name === "assetManifest" ||
        name === "surfaceCatalog" ||
        name === "surfaceCatalogIndex";
      const parameters = hasNoArguments ? "()" : "request";
      const invokeArguments = hasNoArguments ? `channels.${name}` : `channels.${name}, request`;
      return `  ${name}: ${parameters} => ipcRenderer.invoke(${invokeArguments}),`;
    })
    .join("\n");

  return `// <auto-generated />
// Generated by scripts/generate-rts-bindings.mjs from the RTS IPC surface contract.

const { contextBridge, ipcRenderer } = require("electron");

const channels = {
${channelLines}
};

contextBridge.exposeInMainWorld("aetheriaRts", {
${apiLines}
});
`;
}
