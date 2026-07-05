$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $root
Set-Location $root

npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7C verifier failed: RTS build failed."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("aetheria-stage7c-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$statePath = Join-Path $tempRoot "aetheria.cc"
$endpoint = $null

try {
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $daemonOutput = dotnet run --project (Join-Path $repoRoot "Aetheria.State.Daemon\Aetheria.State.Daemon.csproj") -- `
            --state $statePath `
            --once `
            --client-cultmesh-port 0 2>&1
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $daemonOutput | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7C verifier failed: one-shot daemon did not complete."
    }

    foreach ($line in $daemonOutput) {
        if ($line -match "Aetheria client CultMesh endpoint: (rudp://127\.0\.0\.1:\d+)") {
            $endpoint = $Matches[1]
        }
    }
    if ([string]::IsNullOrWhiteSpace($endpoint)) {
        Write-Error "Stage 7C verifier failed: one-shot daemon did not report a client CultMesh endpoint."
    }

    $smokeScript = Join-Path $tempRoot "stage7c-smoke.mjs"
    @'
import { pathToFileURL } from "node:url";

const statePath = process.env.AETHERIA_STAGE7C_STATE_PATH;
const endpoint = process.env.AETHERIA_STAGE7C_ENDPOINT;
const clientModulePath = process.env.AETHERIA_STAGE7C_CLIENT_MODULE;
if (!statePath || !endpoint)
  throw new Error("Stage 7C verifier missing state path or endpoint.");
if (!clientModulePath)
  throw new Error("Stage 7C verifier missing compiled client module path.");

const { AetheriaCultMeshClient } = await import(pathToFileURL(clientModulePath).href);

const client = new AetheriaCultMeshClient({
  uri: "cultmesh://aetheria/daemon/stage7c-aetheria-daemon",
  peerId: "stage7c-daemon",
  verseId: "aetheria.local",
  role: "aetheria-daemon",
  endpoints: [endpoint],
}, statePath, "stage7c-verifier");
const viewport = await client.mapViewport({ minX: -5000, minY: -5000, maxX: 5000, maxY: 5000 });
const objectsViewport = await client.objectsViewport({ minX: -5000, minY: -5000, maxX: 5000, maxY: 5000 });
const gravityViewport = await client.gravityViewport({ minX: -5000, minY: -5000, maxX: 5000, maxY: 5000 });
if (!Number.isFinite(viewport.frameId))
  throw new Error("Viewport query did not produce a frame id.");
if (!Array.isArray(viewport.objects) || viewport.objects.length === 0)
  throw new Error("Viewport query did not expose visible objects.");
if (objectsViewport.schema !== "gamecult.aetheria.objects_viewport.v1")
  throw new Error(`Objects viewport used unexpected schema: ${objectsViewport.schema}`);
if (gravityViewport.schema !== "gamecult.aetheria.gravity_viewport.v1")
  throw new Error(`Gravity viewport used unexpected schema: ${gravityViewport.schema}`);
if (!Array.isArray(objectsViewport.objects) || objectsViewport.objects.length !== viewport.objects.length)
  throw new Error("Objects viewport did not match composed map object set.");
if (!Array.isArray(gravityViewport.gravityInfluences) || gravityViewport.gravityInfluences.length !== viewport.gravityInfluences.length)
  throw new Error("Gravity viewport did not match composed map gravity set.");

const selectedIndex = viewport.controlledEntityIndices[0] ?? viewport.objects[0]?.entityIndex;
if (!Number.isFinite(selectedIndex))
  throw new Error("No entity was available for the selected-object query.");

const selected = await client.selectedObject({ entityIndex: selectedIndex });
if (!selected.selected)
  throw new Error(`Selected-object query did not resolve entity ${selectedIndex}.`);

const inventory = await client.inventory({ entityIndex: selectedIndex });
if (!Array.isArray(inventory.equipment) || !Array.isArray(inventory.cargo))
  throw new Error("Inventory query did not return equipment and cargo arrays.");

const health = await client.daemonHealth();
if (health.status !== "healthy")
  throw new Error(`Daemon health document was not healthy: ${health.status}`);

const authority = await client.authorityStatus();
if (authority.policyId !== "aetheria.trusted-coop.v1")
  throw new Error(`Unexpected authority policy id: ${authority.policyId}`);

const starbridge = await client.starbridgeSession();
if (starbridge.schema !== "gamecult.aetheria.starbridge_session_summary.v1")
  throw new Error(`Unexpected Starbridge schema: ${starbridge.schema}`);
if (starbridge.scenarioName !== "Frontier Fabricator Defense")
  throw new Error(`Unexpected Starbridge scenario: ${starbridge.scenarioName}`);
if (!Array.isArray(starbridge.stationStock) || !starbridge.stationStock.some(item => item.itemKey === "repair-parts"))
  throw new Error("Starbridge session did not expose daemon-seeded station stock.");
if (!Array.isArray(starbridge.waveForecast) || !starbridge.waveForecast.some(wave => wave.displayName === "Scout Probe"))
  throw new Error("Starbridge session did not expose daemon-seeded wave forecast.");

const surfaceCatalog = client.surfaceCatalogDiagnostics();
if (surfaceCatalog.catalogId !== "gamecult.aetheria.surfaces.v1")
  throw new Error(`Unexpected Aetheria surface catalog id: ${surfaceCatalog.catalogId}`);
if (!surfaceCatalog.surfaces.some(surface => surface.surfaceId === "gamecult.aetheria.viewport_feed.v1" && surface.kind === "live-feed"))
  throw new Error("Aetheria surface catalog did not expose the viewport live feed.");
if (!surfaceCatalog.surfaces.some(surface => surface.surfaceId === "gamecult.aetheria.pilot.set_move_vector.v1" && surface.kind === "operation"))
  throw new Error("RTS surface catalog did not expose the move operation.");
if (!surfaceCatalog.surfaces.some(surface =>
  surface.surfaceId === "daemon:aetheria.frame.latest.v1" &&
  surface.kind === "document" &&
  surface.routeHint?.kind === "shared-memory" &&
  surface.sources?.some(source => source.schemaId === "gamecult.aetheria.daemon_frame.v1")))
  throw new Error("RTS surface catalog did not expose the daemon frame document with shared route/source metadata.");

const surfaceIndex = client.surfaceCatalogIndexDiagnostics();
if (surfaceIndex.catalogId !== surfaceCatalog.catalogId)
  throw new Error("RTS surface catalog index did not preserve the catalog id.");
if (!Array.isArray(surfaceIndex.operations) || surfaceIndex.operations.length < 2)
  throw new Error("RTS surface catalog index did not group operation surfaces.");
if (!Array.isArray(surfaceIndex.liveFeeds) || !surfaceIndex.liveFeeds.some(surface => surface.surfaceId === "gamecult.aetheria.viewport_feed.v1"))
  throw new Error("Aetheria surface catalog index did not group the viewport live feed.");
if (!Array.isArray(surfaceIndex.queries) || !surfaceIndex.queries.some(surface => surface.surfaceId === "gamecult.aetheria.objects_viewport.v1"))
  throw new Error("RTS surface catalog index did not group generated query surfaces.");
if (!Array.isArray(surfaceIndex.documents) || !surfaceIndex.documents.some(surface => surface.surfaceId === "daemon:aetheria.frame.latest.v1"))
  throw new Error("RTS surface catalog index did not group generated document surfaces.");

console.log(JSON.stringify({
  frameId: viewport.frameId,
  visibleObjects: viewport.objects.length,
  gravityInfluences: gravityViewport.gravityInfluences.length,
  selectedEntityIndex: selected.selected.entityIndex,
  equipmentItems: inventory.equipment.length,
  cargoItems: inventory.cargo.length,
  health: health.status,
  policyId: authority.policyId,
  starbridgeScenario: starbridge.scenarioName,
  starbridgeStock: starbridge.stationStock.length,
  surfaceCatalog: {
    surfaces: surfaceCatalog.surfaces.length,
    operations: surfaceIndex.operations.length,
    queries: surfaceIndex.queries.length,
    liveFeeds: surfaceIndex.liveFeeds.length,
    documents: surfaceIndex.documents.length
  }
}, null, 2));
'@ | Set-Content -LiteralPath $smokeScript -Encoding UTF8

    $env:AETHERIA_STAGE7C_STATE_PATH = $statePath
    $env:AETHERIA_STAGE7C_ENDPOINT = $endpoint
    $env:AETHERIA_STAGE7C_CLIENT_MODULE = Join-Path $root "electron-dist\aetheria-cultmesh.js"
    node $smokeScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7C verifier failed: local Electron runtime handles could not read daemon publications."
    }
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Stage 7C local runtime verifier passed."
