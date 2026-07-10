import { EveBrowserProviderHost } from "../node_modules/@gamecult/eve-browser-lowering/dist/index.js";
import { mountEveElectronProvider } from "../node_modules/@gamecult/eve-electron/src/eve-electron-renderer.mjs";

void mountEveElectronProvider({
  EveBrowserProviderHost,
  resolveAssetUrl: uri => uri.startsWith("cultmesh://")
    ? `aetheria-cdn://asset?uri=${encodeURIComponent(uri)}`
    : uri,
});
