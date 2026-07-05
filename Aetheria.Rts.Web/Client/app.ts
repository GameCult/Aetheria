import {
  renderEveSurface,
  type EveCommandIntent,
  type EveEmbeddedDocumentRequest,
  type EveSurfaceComponent,
} from "../node_modules/@gamecult/eve-browser-lowering/dist/index.js";
import type {
  AetheriaMenuSurfaceDocument,
  AetheriaRtsApi,
  Viewport,
} from "./aetheria-rts-contract.js";

export {};

declare global {
  interface Window {
    aetheriaRts: AetheriaRtsApi;
  }
}

const host = requiredElement<HTMLElement>("#eve-surface-host");
const statusEl = requiredElement<HTMLElement>("#status");
let latestEveSurfaceKey = "";
let renderDaemonEveSurfaceNow: (() => Promise<void>) | null = null;
let eveSurfacePoll: number | null = null;

function requiredElement<TElement extends Element>(selector: string): TElement {
  const element = document.querySelector<TElement>(selector);
  if (!element) {
    throw new Error(`Aetheria RTS is missing ${selector}.`);
  }

  return element;
}

function setStatus(text: string): void {
  statusEl.textContent = text;
}

async function showDaemonEveSurface(): Promise<void> {
  document.body.classList.add("eve-game-mode");
  host.setAttribute("aria-label", "Aetheria daemon Eve surface");

  const renderLatest = async () => {
    try {
      const surface = await window.aetheriaRts.eveSurface({
        recordKey: "eve:surface:aetheria.daemon.game",
      });
      const surfaceKey = `${surface.providerId}:${surface.surface.id}:${surface.version}:${surface.updatedAtUtc}`;
      if (surfaceKey === latestEveSurfaceKey) {
        return;
      }

      latestEveSurfaceKey = surfaceKey;
      renderEveSurface(surface, host, {
        body: document.body,
        assetUrlResolver: resolveAetheriaAssetUrl,
        clientId: "aetheria.rts.electron",
        commandSink: intent => submitEveCommand(intent),
        documentResolver: resolveEveDocument,
        source: "Aetheria Daemon",
        statusElement: statusEl,
      });
      wireWindowControls();
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Aetheria daemon Eve surface unavailable.");
    }
  };

  renderDaemonEveSurfaceNow = renderLatest;
  await renderLatest();
  if (eveSurfacePoll == null) {
    eveSurfacePoll = window.setInterval(() => {
      void renderLatest();
    }, 250);
  }
}

async function resolveEveDocument(
  request: EveEmbeddedDocumentRequest,
  component: EveSurfaceComponent,
): Promise<{ document?: unknown; documentId: string; schemaId: string; surface?: AetheriaMenuSurfaceDocument["surface"] } | undefined> {
  if (request.schemaId === "gamecult.eve.surface.v1" || request.slotId === "mainMenuPanel") {
    const surface = await window.aetheriaRts.eveSurface({
      surfaceId: request.documentId,
    });
    return {
      documentId: request.documentId,
      schemaId: "gamecult.eve.surface.v1",
      surface: surface.surface,
    };
  }

  const viewport = viewportFromComponent(component);
  if (request.schemaId === "gamecult.aetheria.render_splats_viewport.v1" || request.slotId === "renderSplats") {
    return {
      document: await window.aetheriaRts.renderSplatsViewport(viewport),
      documentId: request.documentId,
      schemaId: "gamecult.aetheria.render_splats_viewport.v1",
    };
  }
  if (request.schemaId === "gamecult.aetheria.gravity_viewport.v1" || request.slotId === "gravity") {
    return {
      document: await window.aetheriaRts.gravityViewport(viewport),
      documentId: request.documentId,
      schemaId: "gamecult.aetheria.gravity_viewport.v1",
    };
  }
  if (request.schemaId === "gamecult.aetheria.objects_viewport.v1" || request.slotId === "objects") {
    return {
      document: await window.aetheriaRts.objectsViewport(viewport),
      documentId: request.documentId,
      schemaId: "gamecult.aetheria.objects_viewport.v1",
    };
  }
  return undefined;
}

function viewportFromComponent(component: EveSurfaceComponent): Viewport {
  const props = component.props ?? {};
  return {
    minX: numberProp(props.minX, -1500),
    minY: numberProp(props.minY, -1000),
    maxX: numberProp(props.maxX, 1500),
    maxY: numberProp(props.maxY, 1000),
  };
}

function numberProp(value: unknown, fallback: number): number {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string") {
    const parsed = Number.parseFloat(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }
  return fallback;
}

function resolveAetheriaAssetUrl(uri: string): string {
  if (!uri.startsWith("resources://")) {
    return uri;
  }

  const resourcePath = uri.slice("resources://".length).replace(/^\/+/, "");
  const filePath = resourcePath.match(/\.[a-z0-9]+$/i)
    ? resourcePath
    : `${resourcePath}.png`;
  return new URL(`../../Assets/Resources/${filePath}`, document.baseURI).href;
}

async function submitEveCommand(intent: EveCommandIntent): Promise<void> {
  try {
    const receipt = await window.aetheriaRts.submitEveCommand({
      providerId: intent.providerId,
      surfaceId: intent.surfaceId,
      command: intent.command,
      clientId: intent.clientId,
      issuedAtUtc: intent.issuedAt,
      payload: intent.payload,
    });
    setStatus(`${intent.command}: ${receipt.accepted ? "submitted" : "rejected"}`);
    latestEveSurfaceKey = "";
    if (renderDaemonEveSurfaceNow) {
      window.setTimeout(() => {
        void renderDaemonEveSurfaceNow?.();
      }, 120);
    }
  } catch (error) {
    setStatus(error instanceof Error ? error.message : `Failed to submit ${intent.command}.`);
  }
}

function wireWindowControls(): void {
  document.querySelectorAll<HTMLButtonElement>("[data-window-control]").forEach(button => {
    if (button.dataset.windowControlWired === "true") {
      return;
    }
    button.dataset.windowControlWired = "true";
    button.addEventListener("click", () => {
      const action = button.dataset.windowControl as "minimize" | "maximize" | "close" | undefined;
      if (action) {
        void window.aetheriaRts.windowControl(action);
      }
    });
  });
}

void showDaemonEveSurface();
