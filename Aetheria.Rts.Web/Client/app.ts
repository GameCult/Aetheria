import type {
  AetheriaRtsApi,
  AuthorityStatusDocument,
  AetheriaRuntimeViewportFeedSnapshot,
  BodyView,
  DaemonHealthDocument,
  GravityInfluence,
  InventoryItem,
  InventoryDocument,
  SelectedObjectDocument,
  StarbridgeSessionDocument,
  ViewObject,
  Viewport,
  ViewportResponse,
} from "./aetheria-rts-contract.js";

export {};

declare global {
  interface Window {
    aetheriaRts: AetheriaRtsApi;
  }
}

const canvas = requiredElement<HTMLCanvasElement>("#map");
const statusEl = requiredElement<HTMLElement>("#status");
const zoomLabel = requiredElement<HTMLOutputElement>("#zoom-label");
const selectionTitle = requiredElement<HTMLElement>("#selection-title");
const selectionDetails = requiredElement<HTMLElement>("#selection-details");
const frameDetails = requiredElement<HTMLElement>("#frame-details");
const starbridgeDetails = requiredElement<HTMLElement>("#starbridge-details");
const runtimeSurfaceDetails = requiredElement<HTMLElement>("#runtime-surface-details");
const controlledList = requiredElement<HTMLElement>("#controlled-list");
const ctx = requiredContext(canvas);

let zoom = 1;
let centerX = 0;
let centerY = 0;
let selectedEntityIndex = -1;
let latest: ViewportResponse | null = null;
let latestSelected: SelectedObjectDocument | null = null;
let latestInventory: InventoryDocument | null = null;
let latestDaemonHealth: DaemonHealthDocument | null = null;
let latestAuthority: AuthorityStatusDocument | null = null;
let latestStarbridge: StarbridgeSessionDocument | null = null;
let latestSurfaceIndex: Awaited<ReturnType<AetheriaRtsApi["surfaceCatalogIndex"]>> | null = null;
let latestOperationReceipt: string | null = null;
let latestReceivedAt = 0;
let latestPollMs = 0;
let refreshInFlight = false;
let viewportFeedUnsubscribe: (() => void) | null = null;

function requiredElement<TElement extends Element>(selector: string): TElement {
  const element = document.querySelector<TElement>(selector);
  if (!element) {
    throw new Error(`Aetheria RTS shell is missing ${selector}.`);
  }

  return element;
}

function requiredContext(target: HTMLCanvasElement): CanvasRenderingContext2D {
  const context = target.getContext("2d");
  if (!context) {
    throw new Error("Canvas 2D context is unavailable.");
  }

  return context;
}

const worldSpan = () => 2200 / zoom;

function currentViewport(): Viewport {
  const span = worldSpan();
  const aspect = Math.max(0.25, canvas.clientWidth / Math.max(1, canvas.clientHeight));
  const width = span * aspect;
  const height = span;
  return {
    minX: centerX - width * 0.5,
    minY: centerY - height * 0.5,
    maxX: centerX + width * 0.5,
    maxY: centerY + height * 0.5
  };
}

function setStatus(text: string): void {
  statusEl.textContent = text;
}

function startViewportFeed(): void {
  const viewport = currentViewport();
  viewportFeedUnsubscribe?.();
  viewportFeedUnsubscribe = window.aetheriaRts.watchViewportFeed({
    viewport,
    selectedEntityIndex
  }, applyViewportSnapshot);
}

function applyViewportSnapshot(snapshot: AetheriaRuntimeViewportFeedSnapshot): void {
  latestPollMs = snapshot.sampleMs;
  latestReceivedAt = Date.parse(snapshot.receivedAtUtc) || Date.now();
  latest = snapshot.viewport;
  latestSelected = snapshot.selectedObject;
  latestInventory = snapshot.inventory;
  latestDaemonHealth = snapshot.daemonHealth;
  latestAuthority = snapshot.authorityStatus;
  latestStarbridge = snapshot.starbridgeSession;

  const controlled = snapshot.viewport.objects.find(object => object.controlled);
  if (controlled && selectedEntityIndex < 0) {
    selectedEntityIndex = controlled.entityIndex;
    centerX = controlled.x;
    centerY = controlled.y;
    startViewportFeed();
    return;
  }

  setStatus(`${snapshot.viewport.zoneName || `Zone ${snapshot.viewport.zoneIndex}`} frame ${snapshot.viewport.frameId}`);
  render();
  renderPanels();
}

async function refreshRuntimeSurfaces(): Promise<void> {
  try {
    latestSurfaceIndex = await window.aetheriaRts.surfaceCatalogIndex();
    renderPanels();
  } catch (error) {
    runtimeSurfaceDetails.innerHTML = details([
      ["State", error instanceof Error ? error.message : "unavailable"]
    ]);
  }
}

function resizeCanvas(): void {
  const ratio = Math.max(1, window.devicePixelRatio || 1);
  const width = Math.max(1, Math.floor(canvas.clientWidth * ratio));
  const height = Math.max(1, Math.floor(canvas.clientHeight * ratio));
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
}

function worldToScreen(x: number, y: number): { x: number; y: number } {
  const viewport = currentViewport();
  const sx = (x - viewport.minX) / (viewport.maxX - viewport.minX) * canvas.clientWidth;
  const sy = canvas.clientHeight - (y - viewport.minY) / (viewport.maxY - viewport.minY) * canvas.clientHeight;
  return { x: sx, y: sy };
}

function screenToWorld(x: number, y: number): { x: number; y: number } {
  const viewport = currentViewport();
  return {
    x: viewport.minX + x / Math.max(1, canvas.clientWidth) * (viewport.maxX - viewport.minX),
    y: viewport.minY + (1 - y / Math.max(1, canvas.clientHeight)) * (viewport.maxY - viewport.minY)
  };
}

function worldRadiusToScreen(radius: number): number {
  const viewport = currentViewport();
  return radius / (viewport.maxX - viewport.minX) * canvas.clientWidth;
}

function render(): void {
  resizeCanvas();
  ctx.clearRect(0, 0, canvas.clientWidth, canvas.clientHeight);
  drawGrid();
  if (!latest) {
    return;
  }

  for (const influence of latest.gravityInfluences) {
    drawGravity(influence);
  }
  for (const body of latest.bodies) {
    drawBody(body);
  }
  for (const object of latest.objects) {
    drawObject(object);
  }
}

function drawGrid(): void {
  const viewport = currentViewport();
  const spacing = chooseGridSpacing((viewport.maxX - viewport.minX) / 8);
  const startX = Math.floor(viewport.minX / spacing) * spacing;
  const startY = Math.floor(viewport.minY / spacing) * spacing;
  ctx.lineWidth = 1;
  ctx.strokeStyle = "#1b242a";
  ctx.beginPath();
  for (let x = startX; x <= viewport.maxX; x += spacing) {
    const screen = worldToScreen(x, viewport.minY);
    ctx.moveTo(screen.x, 0);
    ctx.lineTo(screen.x, canvas.clientHeight);
  }
  for (let y = startY; y <= viewport.maxY; y += spacing) {
    const screen = worldToScreen(viewport.minX, y);
    ctx.moveTo(0, screen.y);
    ctx.lineTo(canvas.clientWidth, screen.y);
  }
  ctx.stroke();
}

function chooseGridSpacing(target: number): number {
  const power = 10 ** Math.floor(Math.log10(Math.max(1, target)));
  const scaled = target / power;
  if (scaled > 5) return power * 10;
  if (scaled > 2) return power * 5;
  return power * 2;
}

function drawGravity(influence: GravityInfluence): void {
  const screen = worldToScreen(influence.x, influence.y);
  const radius = worldRadiusToScreen(influence.radius);
  ctx.lineWidth = 1;
  ctx.strokeStyle = influence.kind === "Sun" ? "rgba(246, 192, 82, 0.28)" : "rgba(97, 174, 197, 0.24)";
  ctx.fillStyle = influence.kind === "Sun" ? "rgba(246, 192, 82, 0.05)" : "rgba(97, 174, 197, 0.04)";
  ctx.beginPath();
  ctx.arc(screen.x, screen.y, Math.max(1, radius), 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

function drawBody(body: BodyView): void {
  const screen = worldToScreen(body.x, body.y);
  const radius = Math.max(3, Math.min(28, worldRadiusToScreen(body.radius) * 0.025));
  ctx.fillStyle = body.kind === "sun" ? "#f4bc5d" : body.isAsteroidBelt ? "#9d9080" : "#71a8b7";
  ctx.beginPath();
  ctx.arc(screen.x, screen.y, radius, 0, Math.PI * 2);
  ctx.fill();
}

function drawObject(object: ViewObject): void {
  const screen = worldToScreen(object.x, object.y);
  const selected = object.entityIndex === selectedEntityIndex;
  const station = object.kind.toLowerCase().includes("station");
  const size = selected ? 9 : object.controlled ? 7 : 5;
  ctx.save();
  ctx.translate(screen.x, screen.y);
  ctx.rotate(Math.atan2(object.directionY, object.directionX));
  ctx.fillStyle = object.isActive
    ? selected ? "#ffffff" : object.controlled ? "#54d18a" : object.factionKey === "raider" ? "#e36b4b" : "#aeb7bd"
    : "#56616a";
  ctx.strokeStyle = "#0d1013";
  ctx.lineWidth = 2;
  ctx.beginPath();
  if (station) {
    ctx.rect(-size, -size, size * 2, size * 2);
  } else {
    ctx.moveTo(size + 4, 0);
    ctx.lineTo(-size, size * 0.7);
    ctx.lineTo(-size * 0.45, 0);
    ctx.lineTo(-size, -size * 0.7);
  }
  ctx.closePath();
  ctx.stroke();
  ctx.fill();
  ctx.restore();

  if (object.targetEntityIndex >= 0) {
    const target = latest?.objects.find(candidate => candidate.entityIndex === object.targetEntityIndex);
    if (target) {
      const targetScreen = worldToScreen(target.x, target.y);
      ctx.strokeStyle = object.controlled ? "rgba(84, 209, 138, 0.42)" : "rgba(227, 107, 75, 0.36)";
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(screen.x, screen.y);
      ctx.lineTo(targetScreen.x, targetScreen.y);
      ctx.stroke();
    }
  }

  if (selected) {
    ctx.strokeStyle = "#ffffff";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(screen.x, screen.y, 14, 0, Math.PI * 2);
    ctx.stroke();
  }
}

function renderPanels(): void {
  if (!latest) {
    return;
  }

  const selected = latestSelected?.selected ?? null;
  selectionTitle.textContent = selected?.displayName || "No Selection";
  selectionDetails.innerHTML = selected
    ? details([
      ["Entity", selected.entityIndex.toString()],
      ["Key", selected.entityKey],
      ["Kind", selected.kind || "unknown"],
      ["Faction", selected.factionKey || "unknown"],
      ["State", selected.isActive ? "active" : "disabled"],
      ["Hull", selected.status.hull.toFixed(1)],
      ["Shield", selected.status.shield.toFixed(1)],
      ["Heat", selected.status.heat.toFixed(1)],
      ["Position", `${selected.x.toFixed(1)}, ${selected.y.toFixed(1)}, z ${selected.z.toFixed(1)}`],
      ["Velocity", `${selected.velocityX.toFixed(2)}, ${selected.velocityY.toFixed(2)}`],
      ["Target", selected.targetEntityIndex >= 0 ? `#${selected.targetEntityIndex}` : "none"],
      ["Controlled", selected.controlled ? "yes" : "no"],
      ["Equipment", inventoryText(latestInventory?.equipment ?? [])],
      ["Cargo", inventoryText(latestInventory?.cargo ?? [])]
    ])
    : `<dt class="muted">State</dt><dd class="muted">Click a visible object on the map.</dd>`;

  frameDetails.innerHTML = details([
    ["Run", latest.runId],
    ["Zone", `${latest.zoneIndex}`],
    ["Frame", latest.frameId.toString()],
    ["Sim Time", `${latest.simulationTimeSeconds.toFixed(2)}s`],
    ["Frame Age", `${Math.max(0, Date.now() - latestReceivedAt)}ms`],
    ["Viewport Poll", `${latestPollMs.toFixed(1)}ms`],
    ["Visible", latest.objects.length.toString()],
    ["Daemon", latestDaemonHealth ? `${latestDaemonHealth.status} / ${latestDaemonHealth.transport}` : "unknown"],
    ["Authority", latestAuthority ? latestAuthority.policyId : "unknown"],
    ["Mode", latestAuthority ? latestAuthority.defaultMode : "unknown"],
    ["Last Op", latestOperationReceipt ?? "none"]
  ]);

  runtimeSurfaceDetails.innerHTML = latestSurfaceIndex
    ? details([
      ["Catalog", latestSurfaceIndex.catalogId],
      ["Queries", surfaceListText(latestSurfaceIndex.queries)],
      ["Live Feeds", surfaceListText(latestSurfaceIndex.liveFeeds)],
      ["Operations", surfaceListText(latestSurfaceIndex.operations)],
      ["Pointers", surfaceListText(latestSurfaceIndex.statePointers)],
      ["Native Views", surfaceListText(latestSurfaceIndex.nativeSliceViews)]
    ])
    : `<dt class="muted">State</dt><dd class="muted">Discovering runtime surfaces.</dd>`;

  starbridgeDetails.innerHTML = latestStarbridge
    ? details([
      ["Scenario", latestStarbridge.scenarioName || latestStarbridge.scenarioId],
      ["Phase", latestStarbridge.phase],
      ["Wave", latestStarbridge.currentWaveIndex.toString()],
      ["Base", latestStarbridge.baseStatus.displayName || "unknown"],
      ["Hull", latestStarbridge.baseStatus.hull.toFixed(1)],
      ["Shield", latestStarbridge.baseStatus.shield.toFixed(1)],
      ["Heat", latestStarbridge.baseStatus.heat.toFixed(1)],
      ["Next", waveText(latestStarbridge)],
      ["Stock", stockText(latestStarbridge)],
      ["Roles", roleText(latestStarbridge)]
    ])
    : `<dt class="muted">State</dt><dd class="muted">No Starbridge session.</dd>`;

  controlledList.innerHTML = "";
  const controlledObjects = latest.objects.filter(object => object.controlled);
  if (controlledObjects.length === 0) {
    controlledList.innerHTML = `<span class="muted">No controlled units in viewport.</span>`;
    return;
  }

  for (const object of controlledObjects) {
    const row = document.createElement("button");
    row.type = "button";
    row.className = "controlled-row";
    row.innerHTML = `<span>${escapeHtml(object.displayName || object.entityKey)}</span><span>#${object.entityIndex}</span>`;
    row.addEventListener("click", () => {
      selectedEntityIndex = object.entityIndex;
      centerX = object.x;
      centerY = object.y;
      void refresh();
    });
    controlledList.append(row);
  }
}

function inventoryText(items: InventoryItem[]): string {
  if (items.length === 0) {
    return "empty";
  }

  return items
    .map(item => `${item.itemKey} x${item.quantity}`)
    .join(", ");
}

function details(rows: Array<[string, string]>): string {
  return rows
    .map(([key, value]) => `<dt>${escapeHtml(key)}</dt><dd>${escapeHtml(value)}</dd>`)
    .join("");
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, character => {
    switch (character) {
      case "&": return "&amp;";
      case "<": return "&lt;";
      case ">": return "&gt;";
      case "\"": return "&quot;";
      case "'": return "&#39;";
      default: return character;
    }
  });
}

canvas.addEventListener("click", event => {
  if (!latest) {
    return;
  }

  const rect = canvas.getBoundingClientRect();
  const click = screenToWorld(event.clientX - rect.left, event.clientY - rect.top);
  const nearest = nearestObject(click.x, click.y);

  if (nearest && worldRadiusToScreen(nearest.distance) < 24) {
    selectedEntityIndex = nearest.object.entityIndex;
    void refresh();
  }
});

canvas.addEventListener("contextmenu", event => {
  event.preventDefault();
  if (!latest) {
    return;
  }

  const actor = latest.objects.find(object => object.entityIndex === selectedEntityIndex && object.controlled);
  if (!actor) {
    setStatus("Select an owned pawn or station first.");
    return;
  }

  const rect = canvas.getBoundingClientRect();
  const click = screenToWorld(event.clientX - rect.left, event.clientY - rect.top);
  const nearest = nearestObject(click.x, click.y);
  if (nearest && nearest.object.entityIndex !== actor.entityIndex && worldRadiusToScreen(nearest.distance) < 28) {
    void issueTargetCommand(actor, nearest.object);
    return;
  }

  void issueMoveCommand(actor, click.x, click.y);
});

function nearestObject(x: number, y: number): { object: ViewObject; distance: number } | null {
  if (!latest) {
    return null;
  }

  let nearest: ViewObject | null = null;
  let nearestDistance = Number.POSITIVE_INFINITY;
  for (const object of latest.objects) {
    const dx = object.x - x;
    const dy = object.y - y;
    const distance = Math.hypot(dx, dy);
    if (distance < nearestDistance) {
      nearest = object;
      nearestDistance = distance;
    }
  }

  return nearest ? { object: nearest, distance: nearestDistance } : null;
}

async function issueTargetCommand(actor: ViewObject, target: ViewObject): Promise<void> {
  const receipt = await window.aetheriaRts.setTarget({
    actorEntityKey: actor.entityKey,
    targetEntityKey: target.entityKey,
    observedFrameId: latest?.frameId
  });
  latestOperationReceipt = formatOperationReceipt("setTarget", receipt, `${actor.entityKey} -> ${target.entityKey}`);
  setStatus(latestOperationReceipt);
  await refresh();
}

async function issueMoveCommand(actor: ViewObject, x: number, y: number): Promise<void> {
  const dx = x - actor.x;
  const dy = y - actor.y;
  const length = Math.max(0.001, Math.hypot(dx, dy));
  const receipt = await window.aetheriaRts.setMoveVector({
    actorEntityKey: actor.entityKey,
    directionX: dx / length,
    directionY: dy / length,
    scalar: 1,
    observedFrameId: latest?.frameId
  });
  latestOperationReceipt = formatOperationReceipt("setMoveVector", receipt, `${actor.entityKey} ${dx.toFixed(1)},${dy.toFixed(1)}`);
  setStatus(latestOperationReceipt);
  await refresh();
}

function formatOperationReceipt(
  label: string,
  receipt: Awaited<ReturnType<AetheriaRtsApi["setMoveVector"]>>,
  detail: string
): string {
  const accepted = receipt.accepted ? "accepted" : "rejected";
  return `${label} ${receipt.operationId} ${accepted} via ${receipt.route.kind} ${receipt.commandId} ${detail} @ frame ${latest?.frameId ?? "?"}`;
}

document.querySelector<HTMLButtonElement>("#zoom-in")?.addEventListener("click", () => {
  zoom = Math.min(12, zoom * 1.4);
  updateZoomLabel();
  void refresh();
});

document.querySelector<HTMLButtonElement>("#zoom-out")?.addEventListener("click", () => {
  zoom = Math.max(0.2, zoom / 1.4);
  updateZoomLabel();
  void refresh();
});

document.querySelector<HTMLButtonElement>("#refresh")?.addEventListener("click", () => {
  void refresh();
});

window.addEventListener("resize", () => {
  render();
});

function updateZoomLabel(): void {
  zoomLabel.value = `${zoom.toFixed(1)}x`;
}

async function refresh(): Promise<void> {
  if (refreshInFlight) {
    return;
  }

  refreshInFlight = true;
  try {
    startViewportFeed();
  } catch (error) {
    setStatus(error instanceof Error ? error.message : "Unable to fetch viewport");
  } finally {
    refreshInFlight = false;
  }
}

function surfaceListText(surfaces: ReadonlyArray<{
  readonly surfaceId: string;
  readonly routeHint: { readonly kind: string; readonly description?: string };
  readonly sources: ReadonlyArray<{ readonly sourceId: string; readonly schemaId?: string }>;
}>): string {
  if (surfaces.length === 0) {
    return "none";
  }

  return surfaces
    .map(surface => {
      const route = surface.routeHint.description
        ? `${surface.routeHint.kind} ${surface.routeHint.description}`
        : surface.routeHint.kind;
      const sources = surface.sources.length === 0
        ? "no sources"
        : surface.sources
          .map(source => source.schemaId ? `${source.sourceId} (${source.schemaId})` : source.sourceId)
          .join(" + ");
      return `${surface.surfaceId} -> ${route}; ${sources}`;
    })
    .join(", ");
}

function stockText(session: StarbridgeSessionDocument): string {
  if (session.stationStock.length === 0) {
    return "empty";
  }

  return session.stationStock
    .slice(0, 4)
    .map(item => `${item.itemKey} x${item.quantity}`)
    .join(", ");
}

function waveText(session: StarbridgeSessionDocument): string {
  const wave = session.waveForecast[0];
  if (!wave) {
    return "none";
  }

  const attackers = wave.attackerKeys.length > 0 ? ` / ${wave.attackerKeys.join(", ")}` : "";
  return `${wave.displayName || `Wave ${wave.waveIndex}`}${attackers}`;
}

function roleText(session: StarbridgeSessionDocument): string {
  if (session.runtimeRoles.length === 0) {
    return "none";
  }

  return session.runtimeRoles
    .map(role => `${role.role}:${role.runtimeId}`)
    .join(", ");
}

updateZoomLabel();
void refreshRuntimeSurfaces();
void refresh();
