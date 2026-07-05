import { app, BrowserWindow, ipcMain, shell } from "electron";
import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";
import { createWriteStream, existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  AetheriaCultMeshClient,
} from "./aetheria-cultmesh.js";
import { registerAetheriaRtsIpcHandlers } from "./aetheria-rts-generated-bindings.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(__dirname, "..");
const repoRoot = resolve(projectRoot, "..");
const logsRoot = resolve(projectRoot, "logs");
const runtimeRoot = resolve(process.env.AETHERIA_RUNTIME_ROOT ?? resolve(projectRoot, "runtime"));
const runtimeStatePath = resolve(runtimeRoot, "aetheria.cc");
const daemonDll = resolve(repoRoot, "Aetheria.State.Daemon", "bin", "Debug", "net10.0", "Aetheria.State.Daemon.dll");
const rendererIndex = resolve(projectRoot, "wwwroot", "index.html");
const clientCultMeshPort = Number.parseInt(process.env.AETHERIA_CLIENT_CULTMESH_PORT ?? "3076", 10);
const verseId = process.env.AETHERIA_VERSE_ID?.trim() || "aetheria.local";
const daemonId = process.env.AETHERIA_DAEMON_ID?.trim() || "aetheria-daemon";
const configuredDaemonUri = process.env.AETHERIA_CULTMESH_URI?.trim() ?? "";
const launchLocalDaemon = configuredDaemonUri.length === 0;
const daemonCultMeshUri = configuredDaemonUri || `cultmesh://aetheria/daemon/${encodeURIComponent(daemonId)}`;
const clientCultMeshAdvertiseHost = process.env.AETHERIA_CLIENT_CULTMESH_ADVERTISE_HOST?.trim() || "127.0.0.1";
const localDaemonRudpEndpoint = launchLocalDaemon ? `rudp://127.0.0.1:${clientCultMeshPort}` : "";
const electronSmoke = process.env.AETHERIA_ELECTRON_SMOKE === "1";
const electronSmokeResultPath = process.env.AETHERIA_ELECTRON_SMOKE_RESULT;

let daemonProcess: ChildProcessWithoutNullStreams | null = null;
let mainWindow: BrowserWindow | null = null;
let aetheriaClient: AetheriaCultMeshClient | null = null;
let isQuitting = false;

app.whenReady().then(async () => {
  mainWindow = createWindow();
  registerIpc();
  showStartup("Preparing Aetheria RTS", "Building daemon if needed.");

  try {
    if (launchLocalDaemon) {
      await ensureDotnetBuild();
      showStartup("Launching Aetheria RTS", "Starting the Aetheria daemon.");
      mkdirSync(runtimeRoot, { recursive: true });
      daemonProcess = startDotnet("aetheria-daemon", daemonDll, [
        "--state",
        runtimeStatePath,
        "--verse-id",
        verseId,
        "--daemon-id",
        daemonId,
        "--client-cultmesh-port",
        clientCultMeshPort.toString(),
        "--client-cultmesh-advertise-host",
        clientCultMeshAdvertiseHost,
        "--tick-interval-ms",
        "20",
        "--fixed-delta-ms",
        "20",
        "--api-publication-interval-ms",
        "100000",
        "--no-odin-announcements",
      ]);
    } else {
      showStartup("Connecting Aetheria RTS", `Using daemon ${daemonCultMeshUri}.`);
      mkdirSync(runtimeRoot, { recursive: true });
    }

    aetheriaClient = new AetheriaCultMeshClient(
      {
        uri: daemonCultMeshUri,
        peerId: daemonId,
        verseId,
        role: "aetheria-daemon",
        endpoints: localDaemonRudpEndpoint ? [localDaemonRudpEndpoint] : [],
      },
      runtimeStatePath,
      "aetheria-electron-client",
      { publicationMode: launchLocalDaemon ? "local" : "remote" });

    showStartup("Launching Aetheria RTS", "Waiting for the daemon CultMesh frame.");
    await aetheriaClient.waitForFrame(30000);
    await mainWindow.loadFile(rendererIndex);
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
  void aetheriaClient?.close();
  stopChild(daemonProcess);
});

function createWindow(): BrowserWindow {
  const window = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 1000,
    minHeight: 640,
    show: !electronSmoke,
    backgroundColor: "#0b1016",
    title: "Aetheria RTS",
    frame: false,
    autoHideMenuBar: true,
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
        const bodyMode = document.body.className;
        const eveHostText = document.querySelector("#eve-surface-host")?.textContent ?? "";
        const eveSurface = api ? await api.eveSurface({ recordKey: "eve:surface:aetheria.daemon.game" }) : null;
        const health = api ? await api.daemonHealth() : null;
        const authority = api ? await api.authorityStatus() : null;
        const starbridge = api ? await api.starbridgeSession() : null;
        const assetManifest = api ? await api.assetManifest() : null;
        const surfaceCatalog = api ? await api.surfaceCatalog() : null;
        const surfaceCatalogIndex = api ? await api.surfaceCatalogIndex() : null;
        const viewport = api ? await api.mapViewport({ minX: -5000, minY: -5000, maxX: 5000, maxY: 5000 }) : null;
        const renderSplatsViewport = api ? await api.renderSplatsViewport({ minX: -1500, minY: -1000, maxX: 1500, maxY: 1000 }) : null;
        const actor = viewport?.objects?.find(object => object.controlled) ?? viewport?.objects?.[0] ?? null;
        const findComponent = (component, predicate) => {
          if (!component || typeof component !== "object") return null;
          if (predicate(component)) return component;
          for (const child of component.children ?? []) {
            const match = findComponent(child, predicate);
            if (match) return match;
          }
          return null;
        };
        const eveFieldSurface = findComponent(eveSurface?.surface?.root, component =>
          component.kind === "field.surface2d" || component.kind === "gravity.surface");
        const eveReceipt = api ? await api.submitEveCommand({
          providerId: "aetheria.daemon",
          surfaceId: "aetheria.game",
          command: "sensor_ping",
          clientId: "aetheria-electron-smoke",
          payload: {},
        }) : null;
        return {
          hasApi: !!api &&
            typeof api.mapViewport === "function" &&
            typeof api.objectsViewport === "function" &&
            typeof api.gravityViewport === "function" &&
            typeof api.renderSplatsViewport === "function" &&
            typeof api.selectedObject === "function" &&
            typeof api.inventory === "function" &&
            typeof api.daemonHealth === "function" &&
            typeof api.authorityStatus === "function" &&
            typeof api.starbridgeSession === "function" &&
            typeof api.assetManifest === "function" &&
            typeof api.surfaceCatalog === "function" &&
            typeof api.surfaceCatalogIndex === "function" &&
            typeof api.eveSurface === "function" &&
            typeof api.submitEveCommand === "function",
          status,
          bodyMode,
          eveHostText,
          eveSurface,
          health,
          authority,
          starbridge,
          assetManifest,
          surfaceCatalog,
          surfaceCatalogIndex,
          viewport,
          renderSplatsViewport,
          actor,
          eveFieldSurface,
          eveReceipt
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
  const bodyMode = stringValue(result.bodyMode);
  const eveHostText = stringValue(result.eveHostText);
  const eveSurface = objectValue(result.eveSurface);
  const health = objectValue(result.health);
  const authority = objectValue(result.authority);
  const starbridge = objectValue(result.starbridge);
  const surfaceCatalog = objectValue(result.surfaceCatalog);
  const surfaceCatalogIndex = objectValue(result.surfaceCatalogIndex);
  const viewport = objectValue(result.viewport);
  const renderSplatsViewport = objectValue(result.renderSplatsViewport);
  const actor = objectValue(result.actor);
  const eveFieldSurface = objectValue(result.eveFieldSurface);
  const eveReceipt = objectValue(result.eveReceipt);
  return result.hasApi === true &&
    status.includes("Aetheria Daemon") &&
    bodyMode.includes("eve-game-mode") &&
    eveHostText.includes("Daemon Frame") &&
    eveHostText.includes("Typed Command Boundary") &&
    objectValue(eveSurface?.surface)?.id === "aetheria.game" &&
    health?.status === "healthy" &&
    authority?.policyId === "aetheria.trusted-coop.v1" &&
    starbridge?.scenarioName === "Frontier Fabricator Defense" &&
    arrayValue(viewport?.objects).length > 0 &&
    renderSplatsViewport?.schema === "gamecult.aetheria.render_splats_viewport.v1" &&
    arrayValue(renderSplatsViewport?.layers).some(layer =>
      objectValue(layer)?.layerKey === "fog.tint") &&
    stringValue(eveFieldSurface?.id).length > 0 &&
    arrayValue(eveFieldSurface?.embeddedDocuments).some(slot =>
      objectValue(slot)?.slotId === "renderSplats") &&
    arrayValue(eveFieldSurface?.embeddedDocuments).some(slot =>
      objectValue(slot)?.slotId === "gravity") &&
    arrayValue(eveFieldSurface?.embeddedDocuments).some(slot =>
      objectValue(slot)?.slotId === "objects") &&
    stringValue(actor?.entityKey).length > 0 &&
    surfaceCatalog?.catalogId === "gamecult.aetheria.surfaces.v1" &&
    arrayValue(surfaceCatalogIndex?.queries).length > 0 &&
    arrayValue(surfaceCatalogIndex?.operations).some(surface =>
      objectValue(surface)?.surfaceId === "gamecult.aetheria.pilot.set_move_vector.v1") &&
    stringValue(eveReceipt?.commandId).length > 0 &&
    objectValue(eveReceipt?.route)?.kind === "network" &&
    eveReceipt?.accepted === true;
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
  void aetheriaClient?.close();
  aetheriaClient = null;
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
      endpoint: daemonCultMeshUri,
      verseId,
      daemonId,
      peerEndpoints: [],
      daemonRunning: daemonProcess != null && !daemonProcess.killed,
      daemonMode: launchLocalDaemon ? "local" : "remote",
    }));
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

function requireClient(): AetheriaCultMeshClient {
  if (!aetheriaClient)
    throw new Error("Aetheria CultMesh client is not initialized.");
  return aetheriaClient;
}

async function ensureDotnetBuild(): Promise<void> {
  if (existsSync(daemonDll) && !daemonBuildInputsAreNewer(daemonDll))
    return;

  await runProcess("dotnet", ["build", resolve(repoRoot, "Aetheria.State.Daemon", "Aetheria.State.Daemon.csproj")], "Aetheria.State.Daemon.build");
}

function daemonBuildInputsAreNewer(outputPath: string): boolean {
  const outputMtime = statSync(outputPath).mtimeMs;
  const sourceRoots = [
    resolve(repoRoot, "Aetheria.State.Daemon"),
    resolve(repoRoot, "Aetheria.State"),
    resolve(repoRoot, "Packages", "org.gamecult.aetheria.state", "Runtime"),
  ];

  return sourceRoots.some(sourceRoot => newestSourceMtime(sourceRoot) > outputMtime);
}

function newestSourceMtime(root: string): number {
  if (!existsSync(root))
    return 0;

  const stat = statSync(root);
  if (stat.isFile())
    return isBuildInput(root) ? stat.mtimeMs : 0;

  let newest = 0;
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const path = resolve(root, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "bin" || entry.name === "obj")
        continue;
      newest = Math.max(newest, newestSourceMtime(path));
    } else if (entry.isFile() && isBuildInput(path)) {
      newest = Math.max(newest, statSync(path).mtimeMs);
    }
  }
  return newest;
}

function isBuildInput(path: string): boolean {
  return path.endsWith(".cs") || path.endsWith(".csproj") || path.endsWith(".props") || path.endsWith(".targets");
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
