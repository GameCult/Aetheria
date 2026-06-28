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
  createAetheriaRuntimeRtsDocuments,
  createAetheriaRuntimeRtsOperationHandles,
  createAetheriaRuntimeRtsVerseFacade,
  createAetheriaRuntimeRtsQueryHandles,
  describeAetheriaRuntimeRtsLiveFeedSurface,
  describeAetheriaRuntimeRtsQueryHandles,
  describeAetheriaRuntimeRtsSurfaceCatalog,
  type AetheriaRuntimeDaemonCommandReceipt,
  type AssetManifestProjection,
  type AuthorityStatusProjection,
  type DaemonHealthProjection,
  type GravityViewportResponse,
  type InventoryProjection,
  type ObjectsViewportResponse,
  type AetheriaRuntimeRtsLiveFeedDiagnostic,
  type AetheriaRuntimeRtsProjectionDiagnostic,
  type AetheriaRuntimeRtsSurfaceCatalogDiagnostic,
  type AetheriaRuntimeRtsOperationHandles,
  type AetheriaRuntimeRtsVerseFacade,
  type AetheriaRuntimeRtsDocuments,
  type AetheriaRuntimeViewportFeedRequest,
  type AetheriaRuntimeViewportFeedSnapshot,
  type RtsSetMoveVectorRequest,
  type RtsSetTargetRequest,
  type SelectedObjectProjection,
  type SelectedObjectRequest,
  type StarbridgeSessionProjection,
  type ViewportRequest,
  type ViewportResponse,
} from "./aetheria-rts-bindings.js";
import {
  projectAuthorityStatus,
  projectAssetManifest,
  projectDaemonHealth,
  projectGravityViewportFromFrame,
  projectInventoryFromFrame,
  projectObjectsViewportFromFrame,
  projectSelectedObjectFromFrame,
  projectStarbridgeSessionSummary,
  projectViewportFromFrame,
} from "./aetheria-rts-local-projection.js";

const connectionId = 0x43554c54;

type AetheriaPublicationDocumentSpec = CultMeshPublicationDocumentBinding & {
  readonly localPath: string;
  readonly remoteRecordKey?: string;
};

export type AetheriaCultMeshClientOptions = {
  publicationMode?: "local" | "remote";
  snapshotTimeoutMs?: number;
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
  SelectedObjectProjection,
  SelectedObjectRequest,
  StarbridgeSessionProjection,
  ViewObject,
  ViewportRequest,
  ViewportResponse,
} from "./aetheria-rts-bindings.js";

export class AetheriaCultMeshClient {
  #peer: CultNetPeer | null = null;
  private readonly verse: CultMeshVerse;
  private readonly queryVerse: CultMeshVerse;
  private readonly commandVerse: CultMeshVerse;

  public constructor(
    private readonly endpoint: string,
    statePath: string,
    private readonly runtimeId = "aetheria-rts-electron",
    options: AetheriaCultMeshClientOptions = {},
  ) {
    const publicationMode = options.publicationMode ?? "local";
    this.publicationDescription = publicationMode === "remote" ? this.endpoint : statePath;
    this.verse = CultMesh.verse("aetheria.local", this.runtimeId);
    this.queryVerse = publicationMode === "remote"
      ? this.verse.withRoute("network", this.publicationDescription)
      : this.verse.withRoute("shared-memory", this.publicationDescription);
    this.commandVerse = this.verse
      .withRoute("network", this.endpoint)
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
      mapViewport: async (request: ViewportRequest) => projectViewportFromFrame(await this.fetchLatestFrameDocument(), request),
      objectsViewport: async (request: ViewportRequest) => projectObjectsViewportFromFrame(await this.fetchLatestFrameDocument(), request),
      gravityViewport: async (request: ViewportRequest) => projectGravityViewportFromFrame(await this.fetchLatestFrameDocument(), request),
      selectedObject: async (request: SelectedObjectRequest) => projectSelectedObjectFromFrame(await this.fetchLatestFrameDocument(), request),
      inventory: async (request: SelectedObjectRequest) => projectInventoryFromFrame(await this.fetchLatestFrameDocument(), request),
      daemonHealth: async () => projectDaemonHealth(await this.fetchDaemonHealthDocument()),
      authorityStatus: async () => projectAuthorityStatus(await this.fetchAuthorityPolicyDocument()),
      starbridgeSession: async () => projectStarbridgeSessionSummary(await this.fetchStarbridgeSessionSummaryDocument()),
      assetManifest: async () => projectAssetManifest(await this.fetchAssetManifestDocument()),
    };
    this.queries = createAetheriaRuntimeRtsQueryHandles(
      executors,
      this.queryVerse.context.routeHint,
      {
        objectsViewport: CultMesh.pollingQueryWatcher(executors.objectsViewport, { intervalMs: 50 }),
        gravityViewport: CultMesh.pollingQueryWatcher(executors.gravityViewport, { intervalMs: 50 }),
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
    this.aetheria = createAetheriaRuntimeRtsVerseFacade(
      this.queryVerse.context,
      this.commandVerse.context,
      this.queries,
      this.operations,
      this.documents,
    );
  }

  private readonly publicationDescription: string;
  private readonly publications: CultMeshDocumentCatalog;
  private readonly queries: ReturnType<typeof createAetheriaRuntimeRtsQueryHandles>;
  private readonly documents: AetheriaRuntimeRtsDocuments;
  private readonly operations: AetheriaRuntimeRtsOperationHandles;
  private readonly aetheria: AetheriaRuntimeRtsVerseFacade;

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

    throw new Error(`Timed out waiting for Aetheria CultMesh frame at ${this.endpoint}. ${lastError}`);
  }

  public async mapViewport(request: ViewportRequest): Promise<ViewportResponse> {
    return this.aetheria.zone().viewport.within(request);
  }

  public async objectsViewport(request: ViewportRequest): Promise<ObjectsViewportResponse> {
    return this.aetheria.zone().objects.visibleWithin(request);
  }

  public async gravityViewport(request: ViewportRequest): Promise<GravityViewportResponse> {
    return this.aetheria.zone().gravity.within(request);
  }

  public async selectedObject(request: SelectedObjectRequest): Promise<SelectedObjectProjection> {
    return this.aetheria.selectedObject(request.entityIndex);
  }

  public async inventory(request: SelectedObjectRequest): Promise<InventoryProjection> {
    return this.aetheria.inventory(request.entityIndex);
  }

  public async daemonHealth(): Promise<DaemonHealthProjection> {
    return this.aetheria.daemon.health();
  }

  public async authorityStatus(): Promise<AuthorityStatusProjection> {
    return this.aetheria.daemon.authorityStatus();
  }

  public async starbridgeSession(): Promise<StarbridgeSessionProjection> {
    return this.aetheria.daemon.starbridgeSession();
  }

  public async assetManifest(): Promise<AssetManifestProjection> {
    return this.aetheria.daemon.assetManifest();
  }

  public projectionDiagnostics(): Readonly<Record<string, AetheriaRuntimeRtsProjectionDiagnostic>> {
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
    return this.publications.latest({ schemaId }, this.queryContext());
  }

  private createPublicationSource(
    mode: "local" | "remote",
  ): (binding: CultMeshPublicationDocumentBinding) => CultMeshDocumentPublicationSource {
    if (mode === "remote") {
      return () => ({
        kind: "peer-snapshot",
        peer: () => this.peer(),
        endpoint: this.endpoint,
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
      this.aetheria.zone().gravity.within(request.viewport),
      this.aetheria.daemon.health(),
      this.aetheria.daemon.authorityStatus(),
      this.aetheria.daemon.starbridgeSession(),
      this.aetheria.daemon.assetManifest(),
      selectedEntityIndex >= 0
        ? this.aetheria.selectedObject(selectedEntityIndex)
        : Promise.resolve(null),
      selectedEntityIndex >= 0
        ? this.aetheria.inventory(selectedEntityIndex)
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
    this.#peer = await CultMesh.createRudpPeer(this.runtimeId, connectionId, this.endpoint, {
      connectTimeoutMs: 2000,
      maxFragmentBytes: 1200,
      maxPendingReliablePackets: 512,
    });
    this.#peer.on("close", () => {
      this.#peer = null;
    });
    return this.#peer;
  }
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
      localPath: `${statePath}.daemon.frame.cc`,
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
      localPath: `${statePath}.daemon.health.cc`,
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
      localPath: `${statePath}.authority.policy.cc`,
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
      localPath: `${statePath}.daemon.starbridge.session.cc`,
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
      localPath: `${statePath}.daemon.assets.cc`,
    },
  ];

  return documents.map(document => mode === "remote" && document.remoteRecordKey
    ? {
        ...document,
        recordKey: document.remoteRecordKey,
      }
    : document);
}

function delay(milliseconds: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}
