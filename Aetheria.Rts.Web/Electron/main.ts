import { app, BrowserWindow, ipcMain, shell, type WebContents } from "electron";
import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";
import { createWriteStream, existsSync, mkdirSync, readFileSync, watch, writeFileSync, type FSWatcher } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  AetheriaCultMeshClient,
} from "./aetheria-cultmesh.js";
import type {
  AetheriaMenuSurfaceComponent,
  AetheriaMenuSurfaceDocument,
} from "./aetheria-cultmesh.js";
import { registerAetheriaRtsIpcHandlers } from "./aetheria-rts-generated-bindings.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(__dirname, "..");
const repoRoot = resolve(projectRoot, "..");
const logsRoot = resolve(projectRoot, "logs");
const runtimeRoot = resolve(process.env.AETHERIA_RTS_RUNTIME_ROOT ?? resolve(projectRoot, "runtime"));
const runtimeStatePath = resolve(runtimeRoot, "aetheria-rts.cc");
const debugSurfacePath = resolve(process.env.AETHERIA_CULTUI_DEBUG_SURFACE_PATH ?? resolve(repoRoot, "GameData", "cultui-debug-surface.cultui"));
const daemonDll = resolve(repoRoot, "Aetheria.State.Daemon", "bin", "Debug", "net10.0", "Aetheria.State.Daemon.dll");
const rendererIndex = resolve(projectRoot, "wwwroot", "index.html");
const rtsCultMeshPort = Number.parseInt(process.env.AETHERIA_RTS_CULTMESH_PORT ?? "3076", 10);
const rtsVerseId = process.env.AETHERIA_RTS_VERSE_ID?.trim() || "aetheria.local";
const rtsDaemonId = process.env.AETHERIA_RTS_DAEMON_ID?.trim() || "starfire-rts";
const configuredDaemonUri = process.env.AETHERIA_RTS_CULTMESH_URI?.trim() ?? "";
const legacyDaemonEndpoint = process.env.AETHERIA_RTS_DAEMON_ENDPOINT?.trim() ?? "";
const launchLocalDaemon = configuredDaemonUri.length === 0 && legacyDaemonEndpoint.length === 0;
const rtsCultMeshUri = configuredDaemonUri || `cultmesh://odin/aetheria/rts/${encodeURIComponent(rtsDaemonId)}`;
const rtsCultMeshAdvertiseHost = process.env.AETHERIA_RTS_CULTMESH_ADVERTISE_HOST?.trim() || "127.0.0.1";
const removedResolvedRudpEndpoint = process.env.AETHERIA_RTS_RESOLVED_RUDP_ENDPOINT?.trim() ?? "";
const removedPeerCultMeshEndpoints = process.env.AETHERIA_RTS_PEER_CULTMESH_ENDPOINTS?.trim() ?? "";
const localDaemonRudpEndpoint = launchLocalDaemon ? `rudp://127.0.0.1:${rtsCultMeshPort}` : "";
const electronSmoke = process.env.AETHERIA_RTS_ELECTRON_SMOKE === "1";
const electronSmokeResultPath = process.env.AETHERIA_RTS_ELECTRON_SMOKE_RESULT;
const rendererView = process.env.AETHERIA_RTS_VIEW?.trim() ?? "";

let daemonProcess: ChildProcessWithoutNullStreams | null = null;
let mainWindow: BrowserWindow | null = null;
let rtsClient: AetheriaCultMeshClient | null = null;
let isQuitting = false;
let debugSurfaceWatcher: FSWatcher | null = null;
let debugSurfaceWatchTimer: NodeJS.Timeout | null = null;
const debugSurfaceSubscriptions = new Map<string, WebContents>();

type AetheriaFileSurfaceDocument = {
  readonly schema?: string;
  readonly providerId?: string;
  readonly providerKind?: string;
  readonly title?: string;
  readonly version?: number;
  readonly updatedAtUtc?: string;
  readonly surface?: AetheriaFileSurfaceTree;
  readonly commands?: readonly AetheriaFileCommand[];
};

type AetheriaFileSurfaceTree = {
  readonly id?: string;
  readonly root?: AetheriaFileSurfaceComponent;
  readonly styles?: readonly AetheriaFileStyleToken[];
};

type AetheriaFileSurfaceComponent = {
  readonly id?: string;
  readonly kind?: string;
  readonly props?: unknown;
  readonly children?: readonly AetheriaFileSurfaceComponent[];
};

type AetheriaFileStyleToken = {
  readonly name?: string;
  readonly value?: string;
};

type AetheriaFileCommand = {
  readonly command?: string;
  readonly label?: string;
  readonly transport?: string;
};

app.whenReady().then(async () => {
  mainWindow = createWindow();
  registerIpc();
  showStartup("Preparing Aetheria RTS", "Building daemon if needed.");

  try {
    if (legacyDaemonEndpoint) {
      throw new Error("AETHERIA_RTS_DAEMON_ENDPOINT has been removed. Configure AETHERIA_RTS_CULTMESH_URI and let Odin/CultMesh resolve the transport.");
    }
    if (removedResolvedRudpEndpoint) {
      throw new Error("AETHERIA_RTS_RESOLVED_RUDP_ENDPOINT has been removed. Configure AETHERIA_RTS_CULTMESH_URI and let Odin/CultMesh resolve the daemon transport.");
    }
    if (removedPeerCultMeshEndpoints) {
      throw new Error("AETHERIA_RTS_PEER_CULTMESH_ENDPOINTS has been removed. Configure AETHERIA_RTS_CULTMESH_URI and let Odin/CultMesh discover peer endpoints.");
    }

    if (rendererView === "main-menu") {
      showStartup("Launching Aetheria Menu", "Lowering the CultUI main-menu surface.");
      mkdirSync(runtimeRoot, { recursive: true });
    } else if (launchLocalDaemon) {
      await ensureDotnetBuild();
      showStartup("Launching Aetheria RTS", "Starting the Aetheria daemon.");
      mkdirSync(runtimeRoot, { recursive: true });
      daemonProcess = startDotnet("aetheria-daemon", daemonDll, [
        "--state",
        runtimeStatePath,
        "--verse-id",
        rtsVerseId,
        "--daemon-id",
        rtsDaemonId,
        "--rts-cultmesh-port",
        rtsCultMeshPort.toString(),
        "--rts-cultmesh-advertise-host",
        rtsCultMeshAdvertiseHost,
        "--tick-interval-ms",
        "20",
        "--fixed-delta-ms",
        "20",
        "--api-publication-interval-ms",
        "100000",
      ]);
    } else {
      showStartup("Connecting Aetheria RTS", `Using daemon ${rtsCultMeshUri}.`);
      mkdirSync(runtimeRoot, { recursive: true });
    }

    rtsClient = new AetheriaCultMeshClient(
      {
        uri: rtsCultMeshUri,
        peerId: rtsDaemonId,
        verseId: rtsVerseId,
        role: "aetheria-rts-daemon",
        endpoints: localDaemonRudpEndpoint ? [localDaemonRudpEndpoint] : [],
      },
      runtimeStatePath,
      "aetheria-rts-electron",
      { publicationMode: launchLocalDaemon ? "local" : "remote" });

    if (rendererView === "main-menu") {
      await mainWindow.loadFile(rendererIndex, { hash: "main-menu" });
    } else {
      showStartup("Launching Aetheria RTS", "Waiting for the daemon CultMesh frame.");
      await rtsClient.waitForFrame(30000);
      await mainWindow.loadFile(rendererIndex);
    }
    if (electronSmoke) {
      const result = await runElectronSmoke(mainWindow);
      writeElectronSmokeResult({ ok: true, result });
      console.log(JSON.stringify(result, null, 2));
      exitElectronSmoke(0);
    }
  } catch (error) {
    await showFailure(error);
    if (electronSmoke) {
      writeElectronSmokeResult({
        ok: false,
        error: error instanceof Error ? error.stack ?? error.message : String(error),
      });
      console.error(error instanceof Error ? error.stack ?? error.message : String(error));
      exitElectronSmoke(1);
    }
  }
});

app.on("window-all-closed", () => {
  app.quit();
});

app.on("before-quit", () => {
  isQuitting = true;
  void rtsClient?.close();
  stopChild(daemonProcess);
  closeDebugSurfaceWatcher();
});

function createWindow(): BrowserWindow {
  const isMainMenuWindow = rendererView === "main-menu";
  const window = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 1000,
    minHeight: 640,
    show: !electronSmoke,
    backgroundColor: "#0b1016",
    title: "Aetheria RTS",
    frame: !isMainMenuWindow,
    autoHideMenuBar: isMainMenuWindow,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: resolve(__dirname, "preload.cjs"),
    },
  });

  window.webContents.setWindowOpenHandler(({ url }) => {
    void shell.openExternal(url);
    return { action: "deny" };
  });

  return window;
}

async function runElectronSmoke(window: BrowserWindow): Promise<Record<string, unknown>> {
  const started = Date.now();
  let lastResult: Record<string, unknown> | null = null;
  while (Date.now() - started < 15000) {
    lastResult = await window.webContents.executeJavaScript(`
      (async () => {
        await new Promise(resolve => setTimeout(resolve, 100));
        const api = window.aetheriaRts;
        const status = document.querySelector("#status")?.textContent ?? "";
        const selectionTitle = document.querySelector("#selection-title")?.textContent ?? "";
        const selectionDetails = document.querySelector("#selection-details")?.textContent ?? "";
        const frameDetails = document.querySelector("#frame-details")?.textContent ?? "";
        const runtimeSurfaceDetails = document.querySelector("#runtime-surface-details")?.textContent ?? "";
        const controlledText = document.querySelector("#controlled-list")?.textContent ?? "";
        const canvas = document.querySelector("#map");
        const health = api ? await api.daemonHealth() : null;
        const authority = api ? await api.authorityStatus() : null;
        const starbridge = api ? await api.starbridgeSession() : null;
        const assetManifest = api ? await api.assetManifest() : null;
        const surfaceCatalog = api ? await api.surfaceCatalog() : null;
        const surfaceCatalogIndex = api ? await api.surfaceCatalogIndex() : null;
        const viewport = api ? await api.mapViewport({ minX: -5000, minY: -5000, maxX: 5000, maxY: 5000 }) : null;
        const actor = viewport?.objects?.find(object => object.controlled) ?? viewport?.objects?.[0] ?? null;
        const moveReceipt = api && actor ? await api.setMoveVector({
          actorEntityKey: actor.entityKey,
          directionX: 1,
          directionY: 0,
          scalar: 0.1,
          observedFrameId: viewport.frameId,
        }) : null;
        return {
          hasApi: !!api &&
            typeof api.mapViewport === "function" &&
            typeof api.objectsViewport === "function" &&
            typeof api.gravityViewport === "function" &&
            typeof api.selectedObject === "function" &&
            typeof api.inventory === "function" &&
            typeof api.daemonHealth === "function" &&
            typeof api.authorityStatus === "function" &&
            typeof api.starbridgeSession === "function" &&
            typeof api.assetManifest === "function" &&
            typeof api.surfaceCatalog === "function" &&
            typeof api.surfaceCatalogIndex === "function",
          status,
          selectionTitle,
          selectionDetails,
          frameDetails,
          runtimeSurfaceDetails,
          controlledText,
          canvasClientWidth: canvas?.clientWidth ?? 0,
          canvasClientHeight: canvas?.clientHeight ?? 0,
          canvasWidth: canvas?.width ?? 0,
          canvasHeight: canvas?.height ?? 0,
          health,
          authority,
          starbridge,
          assetManifest,
          surfaceCatalog,
          surfaceCatalogIndex,
          moveReceipt
        };
      })()
    `, true) as Record<string, unknown>;

    if (isElectronSmokeReady(lastResult))
      return lastResult;
    await delay(250);
  }

  throw new Error(`Aetheria RTS Electron smoke did not reach ready state. Last result: ${JSON.stringify(lastResult)}`);
}

function isElectronSmokeReady(result: Record<string, unknown>): boolean {
  const status = stringValue(result.status);
  const selectionTitle = stringValue(result.selectionTitle);
  const selectionDetails = stringValue(result.selectionDetails);
  const frameDetails = stringValue(result.frameDetails);
  const runtimeSurfaceDetails = stringValue(result.runtimeSurfaceDetails);
  const controlledText = stringValue(result.controlledText);
  const health = objectValue(result.health);
  const authority = objectValue(result.authority);
  const starbridge = objectValue(result.starbridge);
  const surfaceCatalog = objectValue(result.surfaceCatalog);
  const surfaceCatalogIndex = objectValue(result.surfaceCatalogIndex);
  const moveReceipt = objectValue(result.moveReceipt);
  return result.hasApi === true &&
    status.includes("frame") &&
    selectionTitle.length > 0 &&
    selectionTitle !== "No Selection" &&
    selectionDetails.includes("Equipment") &&
    selectionDetails.includes("Cargo") &&
    frameDetails.includes("Daemon") &&
    frameDetails.includes("Authority") &&
    runtimeSurfaceDetails.includes("shared-memory") &&
    runtimeSurfaceDetails.includes("daemon:aetheria.frame.latest.v1") &&
    runtimeSurfaceDetails.includes("gamecult.aetheria.pilot.set_move_vector.v1") &&
    controlledText.length > 0 &&
    numberValue(result.canvasClientWidth) > 0 &&
    numberValue(result.canvasClientHeight) > 0 &&
    numberValue(result.canvasWidth) > 0 &&
    numberValue(result.canvasHeight) > 0 &&
    health?.status === "healthy" &&
    authority?.policyId === "aetheria.trusted-coop.v1" &&
    starbridge?.scenarioName === "Frontier Fabricator Defense" &&
    surfaceCatalog?.catalogId === "gamecult.aetheria.rts.surfaces.v1" &&
    arrayValue(surfaceCatalogIndex?.queries).length > 0 &&
    arrayValue(surfaceCatalogIndex?.operations).some(surface =>
      objectValue(surface)?.surfaceId === "gamecult.aetheria.pilot.set_move_vector.v1") &&
    stringValue(moveReceipt?.commandId).length > 0 &&
    stringValue(moveReceipt?.operationId) === "gamecult.aetheria.pilot.set_move_vector.v1" &&
    objectValue(moveReceipt?.route)?.kind === "network" &&
    moveReceipt?.accepted === true;
}

function stringValue(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function numberValue(value: unknown): number {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}

function objectValue(value: unknown): Record<string, unknown> | null {
  return value != null && typeof value === "object" ? value as Record<string, unknown> : null;
}

function arrayValue(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function writeElectronSmokeResult(result: Record<string, unknown>): void {
  if (!electronSmokeResultPath)
    return;

  writeFileSync(electronSmokeResultPath, JSON.stringify(result, null, 2), "utf8");
}

function exitElectronSmoke(exitCode: number): void {
  isQuitting = true;
  void rtsClient?.close();
  rtsClient = null;
  stopChild(daemonProcess);
  daemonProcess = null;
  mainWindow?.destroy();
  mainWindow = null;
  setImmediate(() => app.exit(exitCode));
}

function registerIpc(): void {
  registerAetheriaRtsIpcHandlers(
    ipcMain,
    requireClient,
    () => ({
      status: "ok",
      transport: "cultmesh-rudp",
      endpoint: rtsCultMeshUri,
      verseId: rtsVerseId,
      daemonId: rtsDaemonId,
      peerEndpoints: [],
      daemonRunning: daemonProcess != null && !daemonProcess.killed,
      daemonMode: launchLocalDaemon ? "local" : "remote",
    }));
  ipcMain.handle("aetheria-rts:main-menu-surface", async (_event, request) =>
    requireClient().mainMenuSurface(request));
  ipcMain.handle("aetheria-rts:debug-surface", () => readDebugSurface());
  ipcMain.handle("aetheria-rts:debug-surface-watch", (event, subscriptionId: string) => {
    if (!subscriptionId)
      throw new Error("Debug surface subscription id is required.");

    debugSurfaceSubscriptions.set(subscriptionId, event.sender);
    event.sender.once("destroyed", () => {
      debugSurfaceSubscriptions.delete(subscriptionId);
      stopDebugSurfaceWatcherIfIdle();
    });
    ensureDebugSurfaceWatcher();
    event.sender.send("aetheria-rts:debug-surface-changed", {
      subscriptionId,
      surface: readDebugSurface(),
    });
  });
  ipcMain.handle("aetheria-rts:debug-surface-watch-stop", (_event, subscriptionId: string) => {
    debugSurfaceSubscriptions.delete(subscriptionId);
    stopDebugSurfaceWatcherIfIdle();
  });
  ipcMain.handle("aetheria-rts:window-control", (event, action: string) => {
    const window = BrowserWindow.fromWebContents(event.sender);
    if (!window)
      return;

    switch (action) {
      case "minimize":
        window.minimize();
        return;
      case "maximize":
        if (window.isMaximized())
          window.unmaximize();
        else
          window.maximize();
        return;
      case "close":
        window.close();
        return;
      default:
        throw new Error(`Unknown window control '${action}'.`);
    }
  });
}

function readDebugSurface(): AetheriaMenuSurfaceDocument {
  if (!existsSync(debugSurfacePath)) {
    throw new Error(`CultUI debug surface file not found: ${debugSurfacePath}`);
  }

  const document = JSON.parse(readFileSync(debugSurfacePath, "utf8")) as AetheriaFileSurfaceDocument;
  return normalizeDebugSurface(document);
}

function ensureDebugSurfaceWatcher(): void {
  if (debugSurfaceWatcher)
    return;

  debugSurfaceWatcher = watch(debugSurfacePath, { persistent: false }, () => {
    if (debugSurfaceWatchTimer)
      clearTimeout(debugSurfaceWatchTimer);
    debugSurfaceWatchTimer = setTimeout(publishDebugSurfaceChange, 50);
  });
  debugSurfaceWatcher.once("error", error => {
    console.warn(`CultUI debug surface watcher failed for ${debugSurfacePath}:`, error);
    closeDebugSurfaceWatcher();
  });
}

function publishDebugSurfaceChange(): void {
  debugSurfaceWatchTimer = null;
  let surface: AetheriaMenuSurfaceDocument;
  try {
    surface = readDebugSurface();
  } catch (error) {
    console.warn(error instanceof Error ? error.message : String(error));
    return;
  }

  for (const [subscriptionId, webContents] of debugSurfaceSubscriptions) {
    if (webContents.isDestroyed()) {
      debugSurfaceSubscriptions.delete(subscriptionId);
      continue;
    }

    webContents.send("aetheria-rts:debug-surface-changed", { subscriptionId, surface });
  }
  stopDebugSurfaceWatcherIfIdle();
}

function stopDebugSurfaceWatcherIfIdle(): void {
  if (debugSurfaceSubscriptions.size === 0)
    closeDebugSurfaceWatcher();
}

function closeDebugSurfaceWatcher(): void {
  if (debugSurfaceWatchTimer) {
    clearTimeout(debugSurfaceWatchTimer);
    debugSurfaceWatchTimer = null;
  }
  debugSurfaceWatcher?.close();
  debugSurfaceWatcher = null;
}

function normalizeDebugSurface(document: AetheriaFileSurfaceDocument): AetheriaMenuSurfaceDocument {
  const surfaceId = stringOr(document.surface?.id, "aetheria.debug.file_surface");
  return {
    providerId: stringOr(document.providerId, "aetheria.debug"),
    providerKind: stringOr(document.providerKind, "debug.file_surface"),
    title: stringOr(document.title, "Aetheria Debug Surface"),
    version: typeof document.version === "number" && Number.isFinite(document.version)
      ? document.version
      : Date.now(),
    updatedAtUtc: stringOr(document.updatedAtUtc, new Date().toISOString()),
    surface: {
      id: surfaceId,
      root: normalizeDebugComponent(document.surface?.root, `${surfaceId}.root`),
      styles: Array.isArray(document.surface?.styles)
        ? document.surface.styles.map(token => ({
          name: stringOr(token.name, ""),
          value: stringOr(token.value, ""),
        }))
        : [],
    },
    commands: Array.isArray(document.commands)
      ? document.commands.map(command => ({
        command: stringOr(command.command, ""),
        label: stringOr(command.label, command.command ?? ""),
        transport: stringOr(command.transport, "debug-log"),
      }))
      : [],
  };
}

function normalizeDebugComponent(
  component: AetheriaFileSurfaceComponent | undefined,
  fallbackId: string,
): AetheriaMenuSurfaceComponent {
  if (!component) {
    return {
      id: fallbackId,
      kind: "surface",
      props: {},
      children: [],
    };
  }

  return {
    id: stringOr(component.id, fallbackId),
    kind: stringOr(component.kind, "surface"),
    props: normalizeDebugProps(component.props),
    children: Array.isArray(component.children)
      ? component.children.map((child, index) => normalizeDebugComponent(child, `${fallbackId}.${index}`))
      : [],
  };
}

function normalizeDebugProps(props: unknown): Record<string, string> {
  if (Array.isArray(props)) {
    return Object.fromEntries(
      props
        .map(prop => objectValue(prop))
        .filter(prop => prop && typeof prop.key === "string")
        .map(prop => [String(prop!.key), stringOr(prop!.value, "")]));
  }

  if (props != null && typeof props === "object") {
    return Object.fromEntries(
      Object.entries(props as Record<string, unknown>)
        .map(([key, value]) => [key, stringOr(value, "")]));
  }

  return {};
}

function stringOr(value: unknown, fallback: string): string {
  return typeof value === "string" ? value : fallback;
}

function requireClient(): AetheriaCultMeshClient {
  if (!rtsClient)
    throw new Error("Aetheria RTS CultMesh client is not initialized.");
  return rtsClient;
}

async function ensureDotnetBuild(): Promise<void> {
  if (existsSync(daemonDll))
    return;

  await runProcess("dotnet", ["build", resolve(repoRoot, "Aetheria.State.Daemon", "Aetheria.State.Daemon.csproj")], "Aetheria.State.Daemon.build");
}

function startDotnet(name: string, dllPath: string, args: string[]): ChildProcessWithoutNullStreams {
  mkdirSync(logsRoot, { recursive: true });
  const child = spawn("dotnet", [dllPath, ...args], {
    cwd: repoRoot,
    windowsHide: true,
  });
  pipeChildLogs(name, child);
  child.once("exit", (code, signal) => {
    if (!isQuitting) {
      void showFailure(new Error(`${name} exited unexpectedly (${signal ?? code ?? "unknown"}). Check ${logsRoot}.`));
    }
  });
  return child;
}

function runProcess(command: string, args: string[], logName: string): Promise<void> {
  mkdirSync(logsRoot, { recursive: true });
  const child = spawn(command, args, {
    cwd: repoRoot,
    windowsHide: true,
  });
  pipeChildLogs(logName, child);
  return new Promise((resolvePromise, reject) => {
    child.once("error", reject);
    child.once("exit", code => {
      if (code === 0) {
        resolvePromise();
        return;
      }

      reject(new Error(`${command} ${args.join(" ")} failed with exit code ${code}. Check ${logsRoot}.`));
    });
  });
}

function pipeChildLogs(name: string, child: ChildProcessWithoutNullStreams): void {
  const stdout = createWriteStream(resolve(logsRoot, `${name}.log`), { flags: "a" });
  const stderr = createWriteStream(resolve(logsRoot, `${name}.err.log`), { flags: "a" });
  child.stdout.pipe(stdout);
  child.stderr.pipe(stderr);
}

function stopChild(child: ChildProcessWithoutNullStreams | null): void {
  if (!child || child.killed)
    return;

  child.kill();
}

function delay(milliseconds: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

function showStartup(title: string, detail: string): void {
  void mainWindow?.loadURL(`data:text/html;charset=utf-8,${encodeURIComponent(startupHtml(title, detail))}`);
}

async function showFailure(error: unknown): Promise<void> {
  const message = error instanceof Error ? error.message : String(error);
  await mainWindow?.loadURL(`data:text/html;charset=utf-8,${encodeURIComponent(startupHtml("Aetheria RTS failed to start", message))}`);
}

function startupHtml(title: string, detail: string): string {
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>${escapeHtml(title)}</title>
  <style>
    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      background: #0b1016;
      color: #dbe7f3;
      font-family: "Segoe UI", sans-serif;
    }
    main {
      width: min(520px, calc(100vw - 48px));
    }
    h1 {
      margin: 0 0 12px;
      font-size: 24px;
      font-weight: 650;
    }
    p {
      margin: 0;
      color: #8fa3b7;
      line-height: 1.5;
    }
  </style>
</head>
<body>
  <main>
    <h1>${escapeHtml(title)}</h1>
    <p>${escapeHtml(detail)}</p>
  </main>
</body>
</html>`;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}
