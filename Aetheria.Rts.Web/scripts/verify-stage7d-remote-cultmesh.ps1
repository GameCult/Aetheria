$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $root
Set-Location $root

npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7D remote CultMesh verifier failed: RTS build failed."
}

$daemonProject = Join-Path $repoRoot "Aetheria.State.Daemon\Aetheria.State.Daemon.csproj"
dotnet build $daemonProject -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7D remote CultMesh verifier failed: daemon build failed."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("aetheria-stage7d-remote-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$statePath = Join-Path $tempRoot "aetheria-raven.cc"
$daemonOut = Join-Path $tempRoot "daemon.out.log"
$daemonErr = Join-Path $tempRoot "daemon.err.log"
$port = Get-Random -Minimum 41000 -Maximum 64000
$endpoint = "rudp://127.0.0.1:$port"
$daemon = $null

try {
    $daemon = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run",
        "--no-build",
        "--project",
        $daemonProject,
        "--",
        "--state",
        $statePath,
        "--rts-cultmesh-host",
        "127.0.0.1",
        "--rts-cultmesh-port",
        $port,
        "--tick-interval-ms",
        "20",
        "--fixed-delta-ms",
        "20",
        "--api-publication-interval-ms",
        "100000"
    ) -RedirectStandardOutput $daemonOut -RedirectStandardError $daemonErr -WindowStyle Hidden -PassThru

    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline) {
        if ($daemon.HasExited) {
            Write-Error "Stage 7D remote CultMesh verifier failed: daemon exited early ($($daemon.ExitCode)).`n$(Get-Content $daemonErr -Raw)`n$(Get-Content $daemonOut -Raw)"
        }

        if ((Test-Path $daemonOut) -and ((Get-Content $daemonOut -Raw) -match "Aetheria Verse daemon published frame")) {
            break
        }

        Start-Sleep -Milliseconds 250
    }

    if (-not (Test-Path $daemonOut) -or -not ((Get-Content $daemonOut -Raw) -match "Aetheria Verse daemon published frame")) {
        Write-Error "Stage 7D remote CultMesh verifier failed: daemon did not publish a frame.`n$(if (Test-Path $daemonOut) { Get-Content $daemonOut -Raw })"
    }

    $smokeScript = Join-Path $tempRoot "remote-smoke.mjs"
    @'
import { pathToFileURL } from "node:url";

const endpoint = process.env.AETHERIA_STAGE7D_RESOLVED_RUDP_ENDPOINT;
const clientModulePath = process.env.AETHERIA_STAGE7D_REMOTE_CLIENT_MODULE;
if (!endpoint)
  throw new Error("Stage 7D verifier missing resolved RUDP endpoint.");
if (!clientModulePath)
  throw new Error("Stage 7D verifier missing compiled client module path.");

const { AetheriaCultMeshClient } = await import(pathToFileURL(clientModulePath).href);
const client = new AetheriaCultMeshClient({
  uri: "cultmesh://odin/aetheria/rts/stage7d-starfire",
  peerId: "stage7d-daemon",
  verseId: "aetheria.local",
  role: "aetheria-rts-daemon",
  endpoints: [endpoint],
}, "unused-local-state.cc", "stage7d-starfire", {
  publicationMode: "remote",
  snapshotTimeoutMs: 5000,
});

try {
  await client.waitForFrame(15000);
  const viewport = await client.mapViewport({ minX: -5000, minY: -5000, maxX: 5000, maxY: 5000 });
  const actor = viewport.objects.find(object => object.controlled);
  if (!actor)
    throw new Error("Remote CultMesh viewport did not expose a controlled unit.");

  const health = await client.daemonHealth();
  const authority = await client.authorityStatus();
  const starbridge = await client.starbridgeSession();
  const receipt = await client.setMoveVector({
    actorEntityKey: actor.entityKey,
    directionX: 1,
    directionY: 0,
    scalar: 0.1,
    observedFrameId: viewport.frameId,
  });

  if (health.status !== "healthy")
    throw new Error(`Unexpected daemon health ${health.status}.`);
  if (authority.policyId !== "aetheria.trusted-coop.v1")
    throw new Error(`Unexpected authority policy ${authority.policyId}.`);
  if (starbridge.scenarioName !== "Frontier Fabricator Defense")
    throw new Error(`Unexpected Starbridge scenario ${starbridge.scenarioName}.`);
  if (!receipt.accepted)
    throw new Error(`Remote setMoveVector was rejected: ${receipt.diagnostic ?? "no diagnostic"}`);

  console.log(JSON.stringify({
    endpoint,
    frameId: viewport.frameId,
    visibleObjects: viewport.objects.length,
    health: health.status,
    policyId: authority.policyId,
    starbridgeScenario: starbridge.scenarioName,
    operationId: receipt.operationId,
  }, null, 2));
} finally {
  await client.close();
}
'@ | Set-Content -LiteralPath $smokeScript -Encoding UTF8

    $env:AETHERIA_STAGE7D_RESOLVED_RUDP_ENDPOINT = $endpoint
    $env:AETHERIA_STAGE7D_REMOTE_CLIENT_MODULE = Join-Path $root "electron-dist\aetheria-cultmesh.js"
    node $smokeScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7D remote CultMesh verifier failed: remote RTS client could not read or command the daemon over CultMesh."
    }
}
finally {
    Remove-Item Env:\AETHERIA_STAGE7D_RESOLVED_RUDP_ENDPOINT -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_STAGE7D_REMOTE_CLIENT_MODULE -ErrorAction SilentlyContinue
    if ($daemon -and -not $daemon.HasExited) {
        Stop-Process -Id $daemon.Id -Force
    }
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Stage 7D remote CultMesh verifier passed."
