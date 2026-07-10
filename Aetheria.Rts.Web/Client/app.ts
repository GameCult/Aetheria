import {
  EveBrowserProviderHost,
  type EveCommandIntent,
  type EveProviderSurfaceAdvertisement,
  type EveEmbeddedDocumentRequest,
  type EveSurfaceComponent,
} from "../node_modules/@gamecult/eve-browser-lowering/dist/index.js";
import type {
  AetheriaRtsApi,
} from "./aetheria-rts-contract.js";

export {};

declare global {
  interface Window {
    aetheriaRts: AetheriaRtsApi;
    eveProvider: {
      providerAdvertisement: AetheriaRtsApi["eveProviderAdvertisement"];
      surface: AetheriaRtsApi["eveSurface"];
      document: AetheriaRtsApi["eveDocument"];
      submitCommand: AetheriaRtsApi["submitEveCommand"];
      windowControl: AetheriaRtsApi["windowControl"];
    };
  }
}

const host = requiredElement<HTMLElement>("#eve-surface-host");
const statusEl = requiredElement<HTMLElement>("#status");

function requiredElement<TElement extends Element>(selector: string): TElement {
  const element = document.querySelector<TElement>(selector);
  if (!element) {
    throw new Error(`Aetheria Starbridge is missing ${selector}.`);
  }

  return element;
}

function setStatus(text: string): void {
  statusEl.textContent = text;
}

async function showDaemonEveSurface(): Promise<void> {
  document.body.classList.add("eve-game-mode");
  host.setAttribute("aria-label", "Aetheria daemon Eve surface");
  const requestedSurfaceId = new URLSearchParams(location.search).get("surface") || "";
  const providerHost = new EveBrowserProviderHost(host, {
    providerAdvertisement: () => window.eveProvider.providerAdvertisement(),
    surface: surface => window.eveProvider.surface(surfaceRequest(surface)),
    submitCommand: submitEveCommand,
    resolveDocument: resolveEveDocument,
    resolveAssetUrl: resolveAetheriaAssetUrl,
  }, {
    body: document.body,
    clientId: "eve-electron-client",
    pollMs: 250,
    requestedSurfaceId,
    source: "CultMesh provider",
    statusElement: statusEl,
  });
  await providerHost.start();
  wireWindowControls();
  new MutationObserver(wireWindowControls).observe(host, { childList: true, subtree: true });
}

function surfaceRequest(surface: EveProviderSurfaceAdvertisement): { surfaceId?: string; recordKey?: string } {
  return surface.key ? { recordKey: surface.key } : { surfaceId: surface.surfaceId };
}

async function resolveEveDocument(
  request: EveEmbeddedDocumentRequest,
  component: EveSurfaceComponent,
): ReturnType<AetheriaRtsApi["eveDocument"]> {
  return window.eveProvider.document({
    ...request,
    context: { viewport: viewportFromComponent(component) },
  });
}

function viewportFromComponent(component: EveSurfaceComponent): Record<string, number> {
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
  if (!uri.startsWith("cultmesh://")) {
    return uri;
  }

  return `aetheria-cdn://asset?uri=${encodeURIComponent(uri)}`;
}

async function submitEveCommand(intent: EveCommandIntent): Promise<void> {
  try {
    const receipt = await window.eveProvider.submitCommand({
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

function wireWindowControls(): void {
  document.querySelectorAll<HTMLButtonElement>("[data-window-control]").forEach(button => {
    if (button.dataset.windowControlWired === "true") {
      return;
    }
    button.dataset.windowControlWired = "true";
    button.addEventListener("click", () => {
      const action = button.dataset.windowControl as "minimize" | "maximize" | "close" | undefined;
      if (action) {
        void window.eveProvider.windowControl(action);
      }
    });
  });
}

void showDaemonEveSurface();
