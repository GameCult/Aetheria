import {
  renderEveSurface,
  type EveCommandIntent,
  type EveEmbeddedDocumentRequest,
  type EveSurfaceComponent,
} from "../node_modules/@gamecult/eve-browser-lowering/dist/index.js";
import type {
  AetheriaMenuSurfaceComponent,
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
let activeMainMenuSurfaceId = "aetheria.main_menu.root";
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
      const loweredSurface = withActiveMainMenuPanel(surface, activeMainMenuSurfaceId);
      const surfaceKey = `${surface.providerId}:${surface.surface.id}:${surface.version}:${surface.updatedAtUtc}:${activeMainMenuSurfaceId}`;
      if (surfaceKey === latestEveSurfaceKey) {
        return;
      }

      latestEveSurfaceKey = surfaceKey;
      renderEveSurface(loweredSurface, host, {
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

async function showMainMenuPanel(surfaceId: string): Promise<void> {
  activeMainMenuSurfaceId = normalizeMainMenuSurfaceId(surfaceId);
  latestEveSurfaceKey = "";
  if (renderDaemonEveSurfaceNow) {
    await renderDaemonEveSurfaceNow();
    return;
  }

  await showDaemonEveSurface();
}

function normalizeMainMenuSurfaceId(surfaceId: string): string {
  switch (surfaceId) {
    case "aetheria.main_menu.settings":
    case "aetheria.main_menu.player_settings":
    case "aetheria.main_menu.verse_settings":
    case "aetheria.main_menu.input_settings":
      return surfaceId;
    default:
      return "aetheria.main_menu.root";
  }
}

function withActiveMainMenuPanel(
  document: AetheriaMenuSurfaceDocument,
  panelSurfaceId: string,
): AetheriaMenuSurfaceDocument {
  return {
    ...document,
    surface: {
      ...document.surface,
      root: rewriteMainMenuPanelSlot(document.surface.root, panelSurfaceId),
    },
  };
}

type MutableEveComponent = AetheriaMenuSurfaceComponent & {
  layout?: Record<string, string>;
  style?: Record<string, string>;
};

function rewriteMainMenuPanelSlot(
  component: AetheriaMenuSurfaceComponent,
  panelSurfaceId: string,
): AetheriaMenuSurfaceComponent {
  const source = component as MutableEveComponent;
  const props = source.props ?? {};
  const embeddedDocuments = source.embeddedDocuments ?? [];
  const isMainMenuSlot = props.slotId === "mainMenuPanel" ||
    embeddedDocuments.some(slot => slot.slotId === "mainMenuPanel");
  return {
    ...source,
    props: isMainMenuSlot
      ? { ...props, documentId: panelSurfaceId }
      : props,
    embeddedDocuments: isMainMenuSlot
      ? embeddedDocuments.map(slot => slot.slotId === "mainMenuPanel"
        ? { ...slot, documentId: panelSurfaceId }
        : slot)
      : source.embeddedDocuments,
    children: source.children.map(child => rewriteMainMenuPanelSlot(child, panelSurfaceId)),
  };
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
  if (intent.command.startsWith("aetheria.main_menu.")) {
    handleMenuCommand(intent.command);
    return;
  }

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
  } catch (error) {
    setStatus(error instanceof Error ? error.message : `Failed to submit ${intent.command}.`);
  }
}

function handleMenuCommand(command: string): void {
  switch (command) {
    case "aetheria.main_menu.root.show_settings":
      void showMainMenuPanel("aetheria.main_menu.settings");
      return;
    case "aetheria.main_menu.settings.show_player_settings":
      void showMainMenuPanel("aetheria.main_menu.player_settings");
      return;
    case "aetheria.main_menu.settings.show_verse_settings":
      void showMainMenuPanel("aetheria.main_menu.verse_settings");
      return;
    case "aetheria.main_menu.settings.show_input_settings":
      void showMainMenuPanel("aetheria.main_menu.input_settings");
      return;
    case "aetheria.main_menu.settings.back_to_settings":
      void showMainMenuPanel("aetheria.main_menu.settings");
      return;
    case "aetheria.main_menu.settings.back_to_main":
      void showMainMenuPanel("aetheria.main_menu.root");
      return;
    default:
      setStatus(`Aetheria Starbridge: ${command || "no command"}`);
      return;
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
