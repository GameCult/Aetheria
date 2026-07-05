import { encode } from "@msgpack/msgpack";
import { performance } from "node:perf_hooks";
import { CultMesh } from "cultmesh-ts";
import type {
  CultMeshDocumentCatalog,
  CultMeshDocumentPublicationSource,
  CultMeshPublicationDocumentBinding,
  CultMeshOperationContext,
  CultMeshQueryContext,
  CultMeshSurfaceCatalogIndexDiagnostic,
  CultMeshVerse,
} from "cultmesh-ts";
import type {
  CultNetDocumentPutRawMessage,
  CultNetPeer,
} from "cultnet-ts";
import {
  AetheriaRtsSchemas,
  aetheriaRuntimeEveCommandDocumentSlots,
  createAetheriaRuntimeRtsDocuments,
  createAetheriaRuntimeRtsOperationHandles,
  createAetheriaRuntimeRtsVerseHandles,
  createAetheriaRuntimeRtsQueryHandles,
  describeAetheriaRuntimeRtsLiveFeedSurface,
  describeAetheriaRuntimeRtsQueryHandles,
  describeAetheriaRuntimeRtsSurfaceCatalog,
  type AetheriaRuntimeDaemonCommandReceipt,
  type AssetManifestDocument,
  type AuthorityStatusDocument,
  type DaemonHealthDocument,
  type GravityViewportResponse,
  type InventoryDocument,
  type ObjectsViewportResponse,
  type RenderSplatsViewportResponse,
  type AetheriaRuntimeRtsLiveFeedDiagnostic,
  type AetheriaRuntimeRtsQueryDiagnostic,
  type AetheriaRuntimeRtsSurfaceCatalogDiagnostic,
  type AetheriaRuntimeRtsOperationHandles,
  type AetheriaRuntimeRtsVerseHandles,
  type AetheriaRuntimeRtsDocuments,
  type AetheriaRuntimeViewportFeedRequest,
  type AetheriaRuntimeViewportFeedSnapshot,
  type RtsSetMoveVectorRequest,
  type RtsSetTargetRequest,
  type SelectedObjectDocument,
  type SelectedObjectRequest,
  type StarbridgeSessionDocument,
  type ViewportRequest,
  type ViewportResponse,
} from "./aetheria-rts-bindings.js";
import {
  readAuthorityStatusDocument,
  readAssetManifestDocument,
  readDaemonHealthDocument,
  buildGravityViewportDocumentFromFrame,
  buildInventoryDocumentFromFrame,
  buildObjectsViewportDocumentFromFrame,
  buildRenderSplatsViewportDocumentFromFrame,
  buildSelectedObjectDocumentFromFrame,
  readStarbridgeSessionSummaryDocument,
  buildViewportDocumentFromFrame,
} from "./aetheria-rts-local-documents.js";

const connectionId = 0x43554c54;
const eveSurfaceSchemaId = "gamecult.eve.surface.v1";
const defaultEveSurfaceRecordKey = "eve:surface:aetheria.daemon.game";

type AetheriaPublicationDocumentSpec = CultMeshPublicationDocumentBinding & {
  readonly localPath: string;
  readonly remoteRecordKey?: string;
};

type AetheriaRuntimeRtsQueryExecutors = {
  mapViewport: (request: ViewportRequest) => Promise<ViewportResponse>;
  objectsViewport: (request: ViewportRequest) => Promise<ObjectsViewportResponse>;
  gravityViewport: (request: ViewportRequest) => Promise<GravityViewportResponse>;
  renderSplatsViewport: (request: ViewportRequest) => Promise<RenderSplatsViewportResponse>;
  selectedObject: (request: SelectedObjectRequest) => Promise<SelectedObjectDocument>;
  inventory: (request: SelectedObjectRequest) => Promise<InventoryDocument>;
  daemonHealth: () => Promise<DaemonHealthDocument>;
  authorityStatus: () => Promise<AuthorityStatusDocument>;
  starbridgeSession: () => Promise<StarbridgeSessionDocument>;
  assetManifest: () => Promise<AssetManifestDocument>;
};

export type AetheriaCultMeshClientOptions = {
  publicationMode?: "local" | "remote";
  snapshotTimeoutMs?: number;
};

export type AetheriaCultMeshDaemonTarget = {
  readonly uri: string;
  readonly peerId?: string;
  readonly verseId?: string;
  readonly role?: string;
  readonly endpoints?: readonly string[];
};

export type AetheriaMenuSurfaceRequest = {
  surfaceId?: string;
  inGame?: boolean;
  canOpenRuntimeInputScreen?: boolean;
  panelOnly?: boolean;
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

export type {
  BodyView,
  EntityStatus,
  GravityViewportResponse,
  GravityInfluence,
  InventoryItem,
  ObjectsViewportResponse,
  AetheriaRuntimeViewportFeedRequest,
  AetheriaRuntimeViewportFeedSnapshot,
  RtsSetMoveVectorRequest,
  RtsSetTargetRequest,
  SelectedObjectDocument,
  SelectedObjectRequest,
  StarbridgeSessionDocument,
  ViewObject,
  ViewportRequest,
  ViewportResponse,
} from "./aetheria-rts-bindings.js";

export class AetheriaCultMeshClient {
  #peer: CultNetPeer | null = null;
  private readonly verse: CultMeshVerse;
  private readonly queryVerse: CultMeshVerse;
  private readonly commandVerse: CultMeshVerse;
  private readonly daemonTarget: Required<AetheriaCultMeshDaemonTarget>;

  public constructor(
    daemonTarget: string | AetheriaCultMeshDaemonTarget,
    statePath: string,
    private readonly runtimeId = "aetheria-rts-electron",
    options: AetheriaCultMeshClientOptions = {},
  ) {
    this.daemonTarget = normalizeDaemonTarget(daemonTarget);
    const publicationMode = options.publicationMode ?? "local";
    this.publicationDescription = publicationMode === "remote" ? this.daemonTarget.uri : statePath;
    this.verse = CultMesh.verse("aetheria.local", this.runtimeId);
    this.queryVerse = publicationMode === "remote"
      ? this.verse.withRoute("network", this.publicationDescription)
      : this.verse.withRoute("shared-memory", this.publicationDescription);
    this.commandVerse = this.verse
      .withRoute("network", this.daemonTarget.uri)
      .withClaim("commander-control", { shardId: "aetheria.local" });
    this.publications = CultMesh.documentsFromPublication(
      this.createPublicationSource(publicationMode),
      createAetheriaPublicationDocuments(statePath, publicationMode),
      {
        routeHint: this.queryVerse.context.routeHint,
        timeoutMs: options.snapshotTimeoutMs,
        pollMs: 50,
        messageIdPrefix: `${this.runtimeId}:snapshot`,
      },
    );
    const executors = {
      mapViewport: async (request: ViewportRequest) => buildViewportDocumentFromFrame(await this.fetchLatestFrameDocument(), request),
      objectsViewport: async (request: ViewportRequest) => buildObjectsViewportDocumentFromFrame(await this.fetchLatestFrameDocument(), request),
      gravityViewport: async (request: ViewportRequest) => buildGravityViewportDocumentFromFrame(await this.fetchLatestFrameDocument(), request),
      renderSplatsViewport: async (request: ViewportRequest) => buildRenderSplatsViewportDocumentFromFrame(await this.fetchLatestFrameDocument(), request),
      selectedObject: async (request: SelectedObjectRequest) => buildSelectedObjectDocumentFromFrame(await this.fetchLatestFrameDocument(), request),
      inventory: async (request: SelectedObjectRequest) => buildInventoryDocumentFromFrame(await this.fetchLatestFrameDocument(), request),
      daemonHealth: async () => readDaemonHealthDocument(await this.fetchDaemonHealthDocument()),
      authorityStatus: async () => readAuthorityStatusDocument(await this.fetchAuthorityPolicyDocument()),
      starbridgeSession: async () => readStarbridgeSessionSummaryDocument(await this.fetchStarbridgeSessionSummaryDocument()),
      assetManifest: async () => readAssetManifestDocument(await this.fetchAssetManifestDocument()),
    };
    this.queryExecutors = executors;
    this.queries = createAetheriaRuntimeRtsQueryHandles(
      executors,
      this.queryVerse.context.routeHint,
      {
        objectsViewport: CultMesh.pollingQueryWatcher(executors.objectsViewport, { intervalMs: 50 }),
        gravityViewport: CultMesh.pollingQueryWatcher(executors.gravityViewport, { intervalMs: 50 }),
        renderSplatsViewport: CultMesh.pollingQueryWatcher(executors.renderSplatsViewport, { intervalMs: 50 }),
        selectedObject: CultMesh.pollingQueryWatcher(executors.selectedObject, { intervalMs: 50 }),
        inventory: CultMesh.pollingQueryWatcher(executors.inventory, { intervalMs: 50 }),
        daemonHealth: CultMesh.pollingQueryWatcher(executors.daemonHealth, { intervalMs: 250 }),
        authorityStatus: CultMesh.pollingQueryWatcher(executors.authorityStatus, { intervalMs: 250 }),
        starbridgeSession: CultMesh.pollingQueryWatcher(executors.starbridgeSession, { intervalMs: 250 }),
        assetManifest: CultMesh.pollingQueryWatcher(executors.assetManifest, { intervalMs: 1000 }),
      },
    );
    this.documents = createAetheriaRuntimeRtsDocuments(
      this.queryVerse.context.routeHint,
      {
        daemonFrame: async () => this.fetchLatestFrameDocument(),
        daemonHealth: async () => this.fetchDaemonHealthDocument(),
        authorityPolicy: async () => this.fetchAuthorityPolicyDocument(),
        starbridgeSession: async () => this.fetchStarbridgeSessionSummaryDocument(),
        assetManifest: async () => this.fetchAssetManifestDocument(),
      },
    );
    this.operations = createAetheriaRuntimeRtsOperationHandles(
      (commandId, issuedAtUtc, command, context) =>
        this.sendCommandDocument(commandId, issuedAtUtc, command, context),
    );
    this.aetheria = createAetheriaRuntimeRtsVerseHandles(
      this.queryVerse.context,
      this.commandVerse.context,
      this.queries,
      this.operations,
      this.documents,
    );
  }

  private readonly publicationDescription: string;
  private readonly publications: CultMeshDocumentCatalog;
  private readonly queryExecutors: AetheriaRuntimeRtsQueryExecutors;
  private readonly queries: ReturnType<typeof createAetheriaRuntimeRtsQueryHandles>;
  private readonly documents: AetheriaRuntimeRtsDocuments;
  private readonly operations: AetheriaRuntimeRtsOperationHandles;
  private readonly aetheria: AetheriaRuntimeRtsVerseHandles;

  public async close(): Promise<void> {
    this.#peer?.close();
    this.#peer = null;
  }

  public async waitForFrame(timeoutMs: number): Promise<void> {
    const started = Date.now();
    let lastError = "";
    while (Date.now() - started < timeoutMs) {
      try {
        await this.mapViewport(CultMesh.viewportRequest(CultMesh.rectFromBounds(-1000, -1000, 1000, 1000)));
        return;
      } catch (error) {
        lastError = error instanceof Error ? error.message : String(error);
        await delay(250);
      }
    }

    throw new Error(`Timed out waiting for Aetheria CultMesh frame at ${this.daemonTarget.uri}. ${lastError}`);
  }

  public async mapViewport(request: ViewportRequest): Promise<ViewportResponse> {
    return this.queryExecutors.mapViewport(request);
  }

  public async objectsViewport(request: ViewportRequest): Promise<ObjectsViewportResponse> {
    return this.queryExecutors.objectsViewport(request);
  }

  public async gravityViewport(request: ViewportRequest): Promise<GravityViewportResponse> {
    return this.queryExecutors.gravityViewport(request);
  }

  public async renderSplatsViewport(request: ViewportRequest): Promise<RenderSplatsViewportResponse> {
    return this.queryExecutors.renderSplatsViewport(request);
  }

  public async selectedObject(request: SelectedObjectRequest): Promise<SelectedObjectDocument> {
    return this.queryExecutors.selectedObject(request);
  }

  public async inventory(request: SelectedObjectRequest): Promise<InventoryDocument> {
    return this.queryExecutors.inventory(request);
  }

  public async daemonHealth(): Promise<DaemonHealthDocument> {
    return this.aetheria.daemon.health();
  }

  public async authorityStatus(): Promise<AuthorityStatusDocument> {
    return this.aetheria.daemon.authorityStatus();
  }

  public async starbridgeSession(): Promise<StarbridgeSessionDocument> {
    return this.aetheria.daemon.starbridgeSession();
  }

  public async assetManifest(): Promise<AssetManifestDocument> {
    return this.aetheria.daemon.assetManifest();
  }

  public async mainMenuSurface(request: AetheriaMenuSurfaceRequest = {}): Promise<AetheriaMenuSurfaceDocument> {
    return buildMainMenuSurface(
      request.surfaceId || "aetheria.main_menu.root",
      request.canOpenRuntimeInputScreen === true,
      request.inGame === true,
      request.panelOnly === true);
  }

  public async eveSurface(request: AetheriaEveSurfaceRequest = {}): Promise<AetheriaMenuSurfaceDocument> {
    const recordKey = eveSurfaceRecordKey(request);
    const document = CultMesh.documentFromPublication(
      {
        kind: "peer-snapshot",
        peer: () => this.peer(),
        endpoint: this.resolvedRudpEndpoint(),
      },
      eveSurfaceSchemaId,
      recordKey,
      {
        documentId: recordKey,
        routeHint: this.queryVerse.context.routeHint,
        sourceId: recordKey,
        timeoutMs: 1500,
        pollMs: 50,
        messageIdPrefix: `${this.runtimeId}:eve-surface`,
      },
    );

    return normalizeEveSurfaceDocument(await document.latest(this.queryContext()));
  }

  public async submitEveCommand(request: AetheriaEveCommandRequest): Promise<AetheriaRuntimeDaemonCommandReceipt> {
    const issuedAtUtc = request?.issuedAtUtc || new Date().toISOString();
    const commandId = `${this.runtimeId}:eve:${Date.now().toString(36)}:${Math.random().toString(36).slice(2)}`;
    const command: unknown[] = [];
    command[aetheriaRuntimeEveCommandDocumentSlots.schema] = AetheriaRtsSchemas.eveCommand;
    command[aetheriaRuntimeEveCommandDocumentSlots.commandId] = commandId;
    command[aetheriaRuntimeEveCommandDocumentSlots.providerId] = request?.providerId ?? "";
    command[aetheriaRuntimeEveCommandDocumentSlots.surfaceId] = request?.surfaceId ?? "";
    command[aetheriaRuntimeEveCommandDocumentSlots.command] = request?.command ?? "";
    command[aetheriaRuntimeEveCommandDocumentSlots.issuedAtUtc] = issuedAtUtc;
    command[aetheriaRuntimeEveCommandDocumentSlots.clientId] = request?.clientId || this.runtimeId;
    command[aetheriaRuntimeEveCommandDocumentSlots.payload] = normalizeCommandPayload(request?.payload);

    const message: CultNetDocumentPutRawMessage = {
      schemaVersion: "cultnet.document_put_raw.v0",
      messageId: commandId,
      document: {
        schemaId: AetheriaRtsSchemas.eveCommand,
        recordKey: `daemon:eve-commands:${stableToken(commandId)}:${AetheriaRtsSchemas.eveCommand}`,
        storedAt: issuedAtUtc,
        payloadEncoding: "messagepack",
        payload: encode(command),
        sourceRuntimeId: this.runtimeId,
        sourceRole: "rts-client",
        tags: ["aetheria-rts", "eve-command"],
      },
    };
    const peer = await this.peer();
    peer.send(message);
    await delay(80);
    return {
      commandId,
      operationId: commandId,
      accepted: true,
      route: this.commandVerse.context.routeHint,
      diagnostic: "submitted Eve surface command",
    };
  }

  public queryDiagnostics(): Readonly<Record<string, AetheriaRuntimeRtsQueryDiagnostic>> {
    return describeAetheriaRuntimeRtsQueryHandles(this.queries);
  }

  public liveFeedDiagnostics(): Readonly<Record<string, AetheriaRuntimeRtsLiveFeedDiagnostic>> {
    return {
      viewportFeed: describeAetheriaRuntimeRtsLiveFeedSurface(this.createViewportFeed()),
    };
  }

  public surfaceCatalogDiagnostics(): AetheriaRuntimeRtsSurfaceCatalogDiagnostic {
    const catalog = describeAetheriaRuntimeRtsSurfaceCatalog(this.queries, this.operations, this.documents);
    return CultMesh.describeSurfaceCatalog(catalog.catalogId, [
      ...catalog.surfaces,
      CultMesh.describeSurface(this.createViewportFeed()),
    ]);
  }

  public surfaceCatalogIndexDiagnostics(): CultMeshSurfaceCatalogIndexDiagnostic {
    return CultMesh.surfaceCatalogIndex(this.surfaceCatalogDiagnostics());
  }

  public watchViewportFeed(
    request: AetheriaRuntimeViewportFeedRequest,
    callback: (snapshot: AetheriaRuntimeViewportFeedSnapshot) => void,
  ): () => void {
    return this.createViewportFeed().watch(request, this.queryContext(), callback);
  }

  public async setMoveVector(request: RtsSetMoveVectorRequest): Promise<AetheriaRuntimeDaemonCommandReceipt> {
    return this.aetheria.entity(request.actorEntityKey).pilot.move(
      CultMesh.vec2(request.directionX, request.directionY),
      {
        scalar: request.scalar,
        observedFrameId: request.observedFrameId,
      },
    );
  }

  public async setTarget(request: RtsSetTargetRequest): Promise<AetheriaRuntimeDaemonCommandReceipt> {
    return this.aetheria.entity(request.actorEntityKey).pilot.target(request.targetEntityKey, {
      observedFrameId: request.observedFrameId,
    });
  }

  private async fetchLatestFrameDocument(): Promise<unknown> {
    return this.fetchPublicationDocument(AetheriaRtsSchemas.daemonFrame);
  }

  private async fetchDaemonHealthDocument(): Promise<unknown> {
    return this.fetchPublicationDocument(AetheriaRtsSchemas.daemonHealth);
  }

  private async fetchAuthorityPolicyDocument(): Promise<unknown> {
    return this.fetchPublicationDocument(AetheriaRtsSchemas.verseAuthorityPolicy);
  }

  private async fetchStarbridgeSessionSummaryDocument(): Promise<unknown> {
    return this.fetchPublicationDocument(AetheriaRtsSchemas.starbridgeSessionSummary);
  }

  private async fetchAssetManifestDocument(): Promise<unknown> {
    return this.fetchPublicationDocument(AetheriaRtsSchemas.assetManifest);
  }

  private fetchPublicationDocument(schemaId: string): Promise<unknown> {
    return retryTransientPublicationRead(() => this.publications.latest({ schemaId }, this.queryContext()));
  }

  private createPublicationSource(
    mode: "local" | "remote",
  ): (binding: CultMeshPublicationDocumentBinding) => CultMeshDocumentPublicationSource {
    if (mode === "remote") {
      return () => ({
        kind: "peer-snapshot",
        peer: () => this.peer(),
        endpoint: this.resolvedRudpEndpoint(),
      });
    }

    return binding => ({
      kind: "single-file",
      path: (binding as AetheriaPublicationDocumentSpec).localPath,
    });
  }

  private createViewportFeed() {
    return CultMesh.liveFeed<AetheriaRuntimeViewportFeedRequest, AetheriaRuntimeViewportFeedSnapshot>(
      "gamecult.aetheria.rts.viewport_feed.v1",
      async (request, context) => {
        const objects = await this.queries.objectsViewport.execute(request.viewport, context);
        return this.createViewportFeedSnapshot(request, objects);
      },
      {
        sources: [
          ...this.queries.objectsViewport.sources,
          ...this.queries.gravityViewport.sources,
          ...this.queries.daemonHealth.sources,
          ...this.queries.authorityStatus.sources,
          ...this.queries.starbridgeSession.sources,
          ...this.queries.assetManifest.sources,
        ],
        routeHint: this.queryVerse.context.routeHint,
        watchFeed: (request, context, callback) => {
          let disposed = false;
          let sampling = false;
          const unsubscribe = this.queries.objectsViewport.watch(request.viewport, context, objects => {
            if (disposed || sampling) {
              return;
            }

            sampling = true;
            void this.createViewportFeedSnapshot(request, objects)
              .then(snapshot => {
                if (!disposed) {
                  callback(snapshot);
                }
              })
              .finally(() => {
                sampling = false;
              });
          });

          return () => {
            disposed = true;
            unsubscribe();
          };
        },
      },
    );
  }

  private async createViewportFeedSnapshot(
    request: AetheriaRuntimeViewportFeedRequest,
    objects: ObjectsViewportResponse,
  ): Promise<AetheriaRuntimeViewportFeedSnapshot> {
    const startedAt = performance.now();
    const selectedEntityIndex = request.selectedEntityIndex ?? -1;
    const [
      gravity,
      daemonHealth,
      authorityStatus,
      starbridgeSession,
      assetManifest,
      selectedObject,
      inventory,
    ] = await Promise.all([
      this.queryExecutors.gravityViewport(request.viewport),
      this.queryExecutors.daemonHealth(),
      this.queryExecutors.authorityStatus(),
      this.queryExecutors.starbridgeSession(),
      this.queryExecutors.assetManifest(),
      selectedEntityIndex >= 0
        ? this.queryExecutors.selectedObject({ entityIndex: selectedEntityIndex })
        : Promise.resolve(null),
      selectedEntityIndex >= 0
        ? this.queryExecutors.inventory({ entityIndex: selectedEntityIndex })
        : Promise.resolve(null),
    ]);

    return {
      viewport: composeViewport(objects, gravity),
      selectedObject,
      inventory,
      daemonHealth,
      authorityStatus,
      starbridgeSession,
      assetManifest,
      receivedAtUtc: new Date().toISOString(),
      sampleMs: performance.now() - startedAt,
    };
  }

  private queryContext(): CultMeshQueryContext {
    return this.queryVerse.queryContext();
  }

  private async sendCommandDocument(
    commandId: string,
    issuedAtUtc: string,
    command: unknown[],
    context: CultMeshOperationContext,
  ): Promise<void> {
    const message: CultNetDocumentPutRawMessage = {
      schemaVersion: "cultnet.document_put_raw.v0",
      messageId: commandId,
      document: {
        schemaId: AetheriaRtsSchemas.daemonCommand,
        recordKey: `daemon:commands:${stableToken(commandId)}:${AetheriaRtsSchemas.daemonCommand}`,
        storedAt: issuedAtUtc,
        payloadEncoding: "messagepack",
        payload: encode(command),
        sourceRuntimeId: context.runtimeId,
        sourceRole: "rts-client",
        tags: ["aetheria-rts"],
      },
    };
    const peer = await this.peer();
    peer.send(message);
    await delay(80);
  }

  private async peer(): Promise<CultNetPeer> {
    if (this.#peer)
      return this.#peer;
    const peers = CultMesh.createPeerCatalog();
    const leases = CultMesh.createAuthorityLeaseCatalog();
    const leaseId = `${this.daemonTarget.peerId}:authority`;
    peers.upsert({
      peerId: this.daemonTarget.peerId,
      verseId: this.daemonTarget.verseId,
      endpoints: this.daemonTarget.endpoints,
      roles: [this.daemonTarget.role],
      authorityLeaseId: leaseId,
    });
    leases.upsert({
      leaseId,
      verseId: this.daemonTarget.verseId,
      peerId: this.daemonTarget.peerId,
      roles: [this.daemonTarget.role],
      validFrom: new Date(Date.now() - 60_000),
      expiresAt: new Date(Date.now() + 60_000),
    });
    this.#peer = await CultMesh.createRudpPeerForAuthorizedPeer(
      this.runtimeId,
      connectionId,
      peers,
      leases,
      this.daemonTarget.verseId,
      this.daemonTarget.role,
      {
      connectTimeoutMs: 2000,
      maxFragmentBytes: 1200,
      maxPendingReliablePackets: 512,
      },
    );
    this.#peer.on("close", () => {
      this.#peer = null;
    });
    return this.#peer;
  }

  private resolvedRudpEndpoint(): string {
    const endpoint = this.daemonTarget.endpoints.find(value => value.toLowerCase().startsWith("rudp://"));
    if (!endpoint) {
      throw new Error(`Daemon ${this.daemonTarget.uri} has no resolved RUDP transport endpoint. Resolve it through Odin/CultMesh before opening a transport peer.`);
    }
    return endpoint;
  }
}

function normalizeDaemonTarget(target: string | AetheriaCultMeshDaemonTarget): Required<AetheriaCultMeshDaemonTarget> {
  const value = typeof target === "string"
    ? { uri: target }
    : target;
  if (!value.uri.trim()) {
    throw new Error("Aetheria CultMesh daemon target URI is required.");
  }
  if (value.uri.toLowerCase().startsWith("rudp://")) {
    throw new Error("Aetheria daemon targets must be CultMesh URIs; raw RUDP endpoints belong in resolved peer-card endpoints.");
  }
  return {
    uri: value.uri,
    peerId: value.peerId?.trim() || "aetheria-rts-daemon",
    verseId: value.verseId?.trim() || "aetheria.local",
    role: value.role?.trim() || "aetheria-rts-daemon",
    endpoints: value.endpoints ?? [],
  };
}

async function retryTransientPublicationRead<T>(read: () => Promise<T>): Promise<T> {
  let lastError: unknown;
  for (let attempt = 0; attempt < 40; attempt += 1) {
    try {
      return await read();
    } catch (error) {
      lastError = error;
      if (!isTransientPublicationReadError(error))
        throw error;
      await delay(25 + attempt * 8);
    }
  }
  throw lastError;
}

function isTransientPublicationReadError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return message.includes("EBUSY") ||
    message.includes("EPERM") ||
    message.includes("ENOENT") ||
    message.includes("did not contain schema");
}

function buildMainMenuSurface(
  surfaceId: string,
  canOpenRuntimeInputScreen: boolean,
  inGame: boolean,
  _panelOnly = false,
): AetheriaMenuSurfaceDocument {
  const updatedAtUtc = new Date().toISOString();
  const activeSurfaceId = normalizeMainMenuSurfaceId(surfaceId);
  return buildMainMenuPanelSurface(activeSurfaceId, canOpenRuntimeInputScreen, inGame, updatedAtUtc);
}

function buildMainMenuPanelSurface(
  surfaceId: string,
  canOpenRuntimeInputScreen: boolean,
  inGame: boolean,
  updatedAtUtc: string,
): AetheriaMenuSurfaceDocument {
  if (surfaceId === "aetheria.main_menu.settings") {
    return surfaceDocument(
      surfaceId,
      "Aetheria Settings",
      updatedAtUtc,
      [
        command("aetheria.main_menu.settings.show_player_settings", "Player Settings"),
        command("aetheria.main_menu.settings.show_verse_settings", "Verse"),
        command("aetheria.main_menu.settings.show_input_settings", "Input"),
        command("aetheria.main_menu.settings.back_to_main", "Back"),
      ],
      menuPanel(
        surfaceId,
        [
          text("aetheria.mainMenu.settings.title", "SETTINGS", "text.title", { margin: "0 0 2.4rem 0" }, mainMenuTitleStyle("4.4rem")),
          buttonColumn(
            "aetheria.mainMenu.settings.actions",
            button("aetheria.mainMenu.settings.playerSettings", "Player Settings", "aetheria.main_menu.settings.show_player_settings"),
            button("aetheria.mainMenu.settings.verse", "Verse", "aetheria.main_menu.settings.show_verse_settings"),
            button("aetheria.mainMenu.settings.input", "Input", "aetheria.main_menu.settings.show_input_settings"),
            button("aetheria.mainMenu.settings.back", "Back", "aetheria.main_menu.settings.back_to_main"),
          ),
        ],
      ),
    );
  }

  if (surfaceId === "aetheria.main_menu.input_settings") {
    const children = [
      text("aetheria.mainMenu.input.title", "INPUT", "text.title", { margin: "0 0 2.2rem 0" }, mainMenuTitleStyle("4.4rem")),
      metric("aetheria.mainMenu.input.bindingOverrides", "Binding Overrides", "0"),
      metric("aetheria.mainMenu.input.actionBarInputs", "Action-Bar Inputs", "0"),
      text(
        "aetheria.mainMenu.input.note",
        canOpenRuntimeInputScreen && inGame
          ? "The runtime Eve input screen owns low-level InputSystem rebinding and action-bar input edits."
          : "This title shell reports typed player-settings state. Launch a run to open the runtime Eve input screen that owns low-level InputSystem rebinding."),
      buttonColumn(
        "aetheria.mainMenu.input.actions",
        button("aetheria.mainMenu.input.back", "Back", "aetheria.main_menu.settings.back_to_settings"),
      ),
    ];
    return surfaceDocument(
      surfaceId,
      "Aetheria Input Settings",
      updatedAtUtc,
      [command("aetheria.main_menu.settings.back_to_settings", "Back")],
      menuPanel(surfaceId, children));
  }

  if (surfaceId === "aetheria.main_menu.player_settings") {
    return surfaceDocument(
      surfaceId,
      "Aetheria Player Settings",
      updatedAtUtc,
      [command("aetheria.main_menu.settings.back_to_settings", "Back")],
      menuPanel(
        surfaceId,
        [
          text("aetheria.mainMenu.player.title", "PLAYER", "text.title", { margin: "0 0 2.2rem 0" }, mainMenuTitleStyle("4.4rem")),
          metric("aetheria.mainMenu.player.temperatureUnit", "Temperature Unit", "Celsius"),
          metric("aetheria.mainMenu.player.significantDigits", "Significant Digits", "3"),
          metric("aetheria.mainMenu.player.shutdown", "Default Shutdown", "25%"),
          buttonColumn(
            "aetheria.mainMenu.player.actions",
            button("aetheria.mainMenu.player.back", "Back", "aetheria.main_menu.settings.back_to_settings"),
          ),
        ]));
  }

  if (surfaceId === "aetheria.main_menu.verse_settings") {
    return surfaceDocument(
      surfaceId,
      "Aetheria Verse Settings",
      updatedAtUtc,
      [command("aetheria.main_menu.settings.back_to_settings", "Back")],
      menuPanel(
        surfaceId,
        [
          text("aetheria.mainMenu.verse.title", "VERSE", "text.title", { margin: "0 0 2.2rem 0" }, mainMenuTitleStyle("4.4rem")),
          metric("aetheria.mainMenu.verse.name", "Name", "Local Aetheria"),
          metric("aetheria.mainMenu.verse.id", "Verse", "aetheria.local"),
          metric("aetheria.mainMenu.verse.visibility", "Visibility", "local"),
          buttonColumn(
            "aetheria.mainMenu.verse.actions",
            button("aetheria.mainMenu.verse.back", "Back", "aetheria.main_menu.settings.back_to_settings"),
          ),
        ]));
  }

  const actionButtons = [
    ...(!inGame ? [button("aetheria.main_menu.root.continue", "Continue", "aetheria.main_menu.root.continue")] : []),
    button("aetheria.main_menu.root.newGame", "New Game", "aetheria.main_menu.root.new_game"),
    button("aetheria.main_menu.root.settings", "Settings", "aetheria.main_menu.root.show_settings"),
    button("aetheria.main_menu.root.quit", "Quit", "aetheria.main_menu.root.quit"),
  ];
  return surfaceDocument(
    "aetheria.main_menu.root",
    "Aetheria Starbridge",
    updatedAtUtc,
    [
      ...(!inGame ? [command("aetheria.main_menu.root.continue", "Continue")] : []),
      command("aetheria.main_menu.root.new_game", "New Game"),
      command("aetheria.main_menu.root.show_settings", "Settings"),
      command("aetheria.main_menu.root.quit", "Quit"),
    ],
    menuPanel(
      "aetheria.main_menu.root",
      [
        text("aetheria.main_menu.root.title", "AETHERIA", "text.title", { margin: "0 0 -1.6rem 0" }, mainMenuTitleStyle("5.9rem")),
        text("aetheria.main_menu.root.subtitle", "STARBRIDGE", "text.subtitle", { margin: "0 0 0.35rem 16.8rem" }, {
          font: "100 2.6rem/1 Montserrat, sans-serif",
          color: "rgba(232, 250, 255, 0.9)",
          whiteSpace: "nowrap",
        }),
        buttonColumn("aetheria.main_menu.root.actions", ...actionButtons),
      ]),
  );
}

function normalizeMainMenuSurfaceId(surfaceId: string): string {
  switch (surfaceId) {
    case "":
      return "aetheria.main_menu.root";
    case "aetheria.main_menu.root":
    case "aetheria.main_menu.settings":
    case "aetheria.main_menu.input_settings":
    case "aetheria.main_menu.player_settings":
    case "aetheria.main_menu.verse_settings":
      return surfaceId;
    default:
      return "aetheria.main_menu.root";
  }
}

function surfaceDocument(
  surfaceId: string,
  title: string,
  updatedAtUtc: string,
  commands: AetheriaMenuSurfaceCommand[],
  ...children: AetheriaMenuSurfaceComponent[]
): AetheriaMenuSurfaceDocument {
  return {
    providerId: "aetheria",
    providerKind: "game.menu",
    title,
    version: 1,
    updatedAtUtc,
    surface: {
      id: surfaceId,
      root: node(
        `${surfaceId}.root`,
        "surface",
        {},
        { position: "relative", overflow: "hidden", width: "100%", height: "100vh", minHeight: "100vh" },
        { background: "rgba(0,0,0,0)" },
        ...children),
      styles: [
        { name: "font.title.family", value: "Montserrat" },
        { name: "font.title.style", value: "Thin" },
        { name: "font.title.weight", value: "100" },
        { name: "font.body.family", value: "Ubuntu" },
        { name: "font.body.style", value: "Regular" },
        { name: "font.body.weight", value: "400" },
        {
          name: "font.web.google",
          value: "https://fonts.googleapis.com/css2?family=Montserrat:wght@100&family=Ubuntu:wght@400&display=swap",
        },
      ],
    },
    commands,
  };
}

function command(commandId: string, label: string): AetheriaMenuSurfaceCommand {
  return { command: commandId, label, transport: "cultmesh" };
}

function node(
  id: string,
  kind: string,
  props: Record<string, string>,
  layoutOrChild?: Record<string, string> | AetheriaMenuSurfaceComponent,
  styleOrChild?: Record<string, string> | AetheriaMenuSurfaceComponent,
  ...children: AetheriaMenuSurfaceComponent[]
): AetheriaMenuSurfaceComponent {
  const layout = isSurfaceComponent(layoutOrChild) ? undefined : layoutOrChild;
  const style = isSurfaceComponent(styleOrChild) ? undefined : styleOrChild;
  const normalizedChildren = [
    ...(isSurfaceComponent(layoutOrChild) ? [layoutOrChild] : []),
    ...(isSurfaceComponent(styleOrChild) ? [styleOrChild] : []),
    ...children,
  ];
  return { id, kind, props, layout, style, children: normalizedChildren };
}

function text(
  id: string,
  value: string,
  kind = "text",
  layout?: Record<string, string>,
  style?: Record<string, string>,
): AetheriaMenuSurfaceComponent {
  return node(id, kind, { value }, layout, style);
}

function metric(id: string, label: string, value: string): AetheriaMenuSurfaceComponent {
  return node(id, "metric", { label, value });
}

function menuPanel(id: string, children: AetheriaMenuSurfaceComponent[]): AetheriaMenuSurfaceComponent {
  return node(
    `${id}.menu`,
    "column",
    {},
    {
      position: "relative",
      padding: "7.25rem 0 0 6.75rem",
      gap: "1.1rem",
      width: "44rem",
      maxWidth: "calc(100vw - 3rem)",
      minHeight: "100vh",
      alignItems: "flex-start",
    },
    { color: "#e9fbff", background: "rgba(0,0,0,0)" },
    ...children);
}

function mainMenuTitleStyle(fontSize: string): Record<string, string> {
  return {
    font: `100 ${fontSize}/0.98 Montserrat, sans-serif`,
    color: "rgba(232, 250, 255, 0.94)",
    whiteSpace: "nowrap",
  };
}

function button(id: string, label: string, commandId: string): AetheriaMenuSurfaceComponent {
  return node(
    id,
    "control.button",
    { label, command: commandId },
    { minWidth: "0", width: "220px", height: "32px", padding: "0" },
    {
      background: "rgba(0, 0, 0, 0)",
      border: "0",
      borderWidth: "0",
      borderStyle: "solid",
      borderRadius: "0",
      boxShadow: "none",
      font: "400 1.55rem/32px Ubuntu, sans-serif",
      color: "#e8fbff",
      textAlign: "left",
    });
}

function buttonColumn(id: string, ...children: AetheriaMenuSurfaceComponent[]): AetheriaMenuSurfaceComponent {
  return node(id, "column", {}, { gap: "0.18rem", alignItems: "flex-start" }, { color: "#e8fbff" }, ...children);
}

function isSurfaceComponent(value: unknown): value is AetheriaMenuSurfaceComponent {
  return !!value && typeof value === "object" && "kind" in value && "props" in value && "children" in value;
}

function stableToken(value: string): string {
  return value
    .replace(/[^a-zA-Z0-9]+/gu, "-")
    .replace(/^-+|-+$/gu, "")
    .replace(/--+/gu, "-")
    .toLowerCase() || "empty";
}

function composeViewport(
  objects: ObjectsViewportResponse,
  gravity: GravityViewportResponse,
): ViewportResponse {
  return {
    schema: "gamecult.aetheria.rts_viewport.v1",
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

function createAetheriaPublicationDocuments(
  statePath: string,
  mode: "local" | "remote",
): readonly AetheriaPublicationDocumentSpec[] {
  const documents: readonly AetheriaPublicationDocumentSpec[] = [
    {
      ...CultMesh.publicationDocument(
        AetheriaRtsSchemas.daemonFrame,
        "daemon:aetheria.frame.latest.v1",
        {
          documentId: "daemon:aetheria.frame.latest.v1",
          sourceId: "daemon:aetheria.frame.latest.v1",
        },
      ),
      localPath: statePath,
    },
    {
      ...CultMesh.publicationDocument(
        AetheriaRtsSchemas.daemonHealth,
        "daemon:aetheria.health.latest.v1",
        {
          documentId: "daemon:aetheria.health.latest.v1",
          sourceId: "daemon:aetheria.health.latest.v1",
        },
      ),
      localPath: statePath,
      remoteRecordKey: "daemon:aetheria.health.v1",
    },
    {
      ...CultMesh.publicationDocument(
        AetheriaRtsSchemas.verseAuthorityPolicy,
        "daemon:aetheria.authority.policy.latest.v1",
        {
          documentId: "daemon:aetheria.authority.policy.latest.v1",
          sourceId: "daemon:aetheria.authority.policy.latest.v1",
        },
      ),
      localPath: statePath,
      remoteRecordKey: "global:aetheria.verse_authority_policy.v1",
    },
    {
      ...CultMesh.publicationDocument(
        AetheriaRtsSchemas.starbridgeSessionSummary,
        "daemon:aetheria.starbridge.session.latest.v1",
        {
          documentId: "daemon:aetheria.starbridge.session.latest.v1",
          sourceId: "daemon:aetheria.starbridge.session.latest.v1",
        },
      ),
      localPath: statePath,
    },
    {
      ...CultMesh.publicationDocument(
        AetheriaRtsSchemas.assetManifest,
        "daemon:aetheria.asset_manifest.latest.v1",
        {
          documentId: "daemon:aetheria.asset_manifest.latest.v1",
          sourceId: "daemon:aetheria.asset_manifest.latest.v1",
        },
      ),
      localPath: statePath,
    },
  ];

  return documents.map(document => mode === "remote" && document.remoteRecordKey
    ? {
        ...document,
        recordKey: document.remoteRecordKey,
      }
    : document);
}

function eveSurfaceRecordKey(request: AetheriaEveSurfaceRequest): string {
  if (request?.recordKey && request.recordKey.trim())
    return request.recordKey.trim();

  switch (request?.surfaceId) {
    case "aetheria.game":
    case "aetheria.daemon.game":
      return "eve:surface:aetheria.daemon.game";
    case "aetheria.game.tui":
    case "aetheria.daemon.game.tui":
      return "eve:surface:aetheria.daemon.game.tui";
    case "aetheria.main_menu.root":
      return "eve:surface:aetheria.main_menu.root";
    case "aetheria.inventory.panel":
      return "eve:surface:aetheria.inventory.panel";
    case "aetheria.inventory.panel.dropdown":
      return "eve:surface:aetheria.inventory.panel.dropdown";
    case "aetheria.map.zone_details":
      return "eve:surface:aetheria.map.zone_details";
    case "aetheria.trade.menu":
      return "eve:surface:aetheria.trade.menu";
    default:
      return defaultEveSurfaceRecordKey;
  }
}

function normalizeEveSurfaceDocument(document: unknown): AetheriaMenuSurfaceDocument {
  const portableSlots = Array.isArray(document) &&
    typeof document[1] === "string" &&
    document[1] === eveSurfaceSchemaId;
  const providerSlot = portableSlots ? 2 : 0;
  const providerKindSlot = portableSlots ? 3 : 1;
  const titleSlot = portableSlots ? 4 : 2;
  const versionSlot = portableSlots ? 5 : 3;
  const updatedAtSlot = portableSlots ? 6 : 4;
  const surfaceSlot = portableSlots ? 7 : 5;
  const commandsSlot = portableSlots ? 8 : 6;
  const surface = readField(document, "surface", surfaceSlot);
  const surfaceId = stringOr(readField(surface, "id", 0), "aetheria.surface.missing");
  return {
    providerId: stringOr(readField(document, "providerId", providerSlot), "aetheria.daemon"),
    providerKind: stringOr(readField(document, "providerKind", providerKindSlot), "daemon"),
    title: stringOr(readField(document, "title", titleSlot), surfaceId),
    version: numberOr(readField(document, "version", versionSlot), 0),
    updatedAtUtc: stringOr(readField(document, "updatedAtUtc", updatedAtSlot), ""),
    surface: {
      id: surfaceId,
      root: normalizeEveComponent(readField(surface, "root", 1), `${surfaceId}.root`),
      styles: arrayValue(readField(surface, "styles", 2))
        .map(token => ({
          name: stringOr(readField(token, "name", 0), ""),
          value: stringOr(readField(token, "value", 1), ""),
        })),
    },
    commands: arrayValue(readField(document, "commands", commandsSlot))
      .map(command => {
        const operation = readField(command, "operation", 0);
        const routeHint = readField(operation, "routeHint", 3);
        return {
          command: stringOr(readField(operation, "operationId", 0), ""),
          label: stringOr(readField(operation, "label", 1), ""),
          transport: stringOr(readField(routeHint, "description", 1), "cultmesh"),
        };
      })
      .filter(command => command.command.length > 0),
  };
}

function normalizeEveComponent(component: unknown, fallbackId: string): AetheriaMenuSurfaceComponent {
  return {
    id: stringOr(readField(component, "id", 0), fallbackId),
    kind: stringOr(readField(component, "kind", 1), "surface"),
    props: stringRecord(readField(component, "props", 2)),
    layout: stringRecord(readField(component, "layout", 6)),
    style: stringRecord(readField(component, "style", 7)),
    embeddedDocuments: arrayValue(readField(component, "embeddedDocuments", 5))
      .map(normalizeEmbeddedDocumentSlot)
      .filter(slot => slot.documentId.length > 0),
    children: arrayValue(readField(component, "children", 3))
      .map((child, index) => normalizeEveComponent(child, `${fallbackId}.${index}`)),
  };
}

function normalizeEmbeddedDocumentSlot(slot: unknown): AetheriaMenuEmbeddedDocumentSlot {
  return {
    slotId: stringOr(readField(slot, "slotId", 0), ""),
    documentId: stringOr(readField(slot, "documentId", 1), ""),
    schemaId: stringOr(readField(slot, "schemaId", 2), ""),
    presentationKind: stringOr(readField(slot, "presentationKind", 3), ""),
  };
}

function readField(value: unknown, property: string, index: number): unknown {
  if (Array.isArray(value))
    return value[index];
  if (value && typeof value === "object") {
    const record = value as Record<string, unknown>;
    if (property in record)
      return record[property];
    const pascal = property.charAt(0).toUpperCase() + property.slice(1);
    if (pascal in record)
      return record[pascal];
  }
  return undefined;
}

function arrayValue(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function normalizeCommandPayload(payload: Record<string, unknown> | undefined): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(payload ?? {})) {
    if (value == null)
      continue;
    result[key] = typeof value === "string" ? value : String(value);
  }
  return result;
}

function objectValue(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function stringOr(value: unknown, fallback: string): string {
  return typeof value === "string" ? value : fallback;
}

function numberOr(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringRecord(value: unknown): Record<string, string> {
  const source = objectValue(value);
  const result: Record<string, string> = {};
  for (const [key, entry] of Object.entries(source)) {
    if (entry == null)
      continue;
    result[key] = typeof entry === "string" ? entry : String(entry);
  }
  return result;
}

function delay(milliseconds: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}
