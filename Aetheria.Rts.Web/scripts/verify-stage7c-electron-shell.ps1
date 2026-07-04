$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7C Electron shell verifier failed: RTS build failed."
}

if (-not (Get-Content wwwroot\index.html -Raw).Contains('runtime-surface-details')) {
    Write-Error "Stage 7C Electron shell verifier failed: built renderer markup does not expose runtime surface diagnostics."
}

if (-not (Get-Content wwwroot\app.js -Raw).Contains('window.aetheriaRts.surfaceCatalogIndex()')) {
    Write-Error "Stage 7C Electron shell verifier failed: built renderer script does not read the CultMesh surface catalog index."
}

$mainText = Get-Content Electron\main.ts -Raw
if (-not $mainText.Contains('await api.surfaceCatalog()') -or -not $mainText.Contains('await api.surfaceCatalogIndex()')) {
    Write-Error "Stage 7C Electron shell verifier failed: Electron smoke does not call the preload CultMesh surface catalog APIs."
}

if (-not $mainText.Contains('runtimeSurfaceDetails.includes("shared-memory")') -or -not $mainText.Contains('daemon:aetheria.frame.latest.v1')) {
    Write-Error "Stage 7C Electron shell verifier failed: Electron smoke does not verify rendered CultMesh route/source metadata."
}

if (-not $mainText.Contains('await api.setMoveVector') -or
    -not $mainText.Contains('moveReceipt?.commandId') -or
    -not $mainText.Contains('moveReceipt?.operationId') -or
    -not $mainText.Contains('moveReceipt?.accepted') -or
    -not $mainText.Contains('moveReceipt?.route')) {
    Write-Error "Stage 7C Electron shell verifier failed: Electron smoke does not verify typed operation receipts through preload."
}

if (-not $mainText.Contains('AETHERIA_RTS_RESOLVED_RUDP_ENDPOINT has been removed') -or
    -not $mainText.Contains('AETHERIA_RTS_PEER_CULTMESH_ENDPOINTS has been removed') -or
    -not $mainText.Contains('AETHERIA_RTS_VERSE_ID') -or
    -not $mainText.Contains('--verse-id') -or
    -not $mainText.Contains('AETHERIA_RTS_DAEMON_ID') -or
    -not $mainText.Contains('--daemon-id')) {
    Write-Error "Stage 7C Electron shell verifier failed: Electron launcher does not enforce CultMesh URI daemon targeting."
}

$electron = Join-Path $root "node_modules\electron\dist\electron.exe"
if (!(Test-Path -LiteralPath $electron)) {
    Write-Error "Stage 7C Electron shell verifier failed: electron.exe was not found at $electron."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("aetheria-stage7c-electron-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$resultPath = Join-Path $tempRoot "electron-smoke-result.json"
$udp = [System.Net.Sockets.UdpClient]::new(0)
$port = ([System.Net.IPEndPoint]$udp.Client.LocalEndPoint).Port
$udp.Close()

try {
    $env:AETHERIA_RTS_ELECTRON_SMOKE = "1"
    $env:AETHERIA_RTS_RUNTIME_ROOT = $tempRoot
    $env:AETHERIA_RTS_CULTMESH_PORT = $port.ToString()
    $env:AETHERIA_RTS_VERSE_ID = "aetheria.stage7c.electron"
    $env:AETHERIA_RTS_DAEMON_ID = "stage7c-starfire"
    $env:AETHERIA_RTS_ELECTRON_SMOKE_RESULT = $resultPath
    $process = Start-Process -FilePath $electron -ArgumentList "." -WorkingDirectory $root -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Error "Stage 7C Electron shell verifier failed: Electron exited with code $($process.ExitCode)."
    }

    if (!(Test-Path -LiteralPath $resultPath)) {
        Write-Error "Stage 7C Electron shell verifier failed: Electron did not write a smoke result."
    }

    $result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
    if ($result.ok -ne $true) {
        Write-Error "Stage 7C Electron shell verifier failed: $($result.error)"
    }
}
finally {
    Remove-Item Env:\AETHERIA_RTS_ELECTRON_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_RUNTIME_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_CULTMESH_PORT -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_VERSE_ID -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_DAEMON_ID -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_PEER_CULTMESH_ENDPOINTS -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_RESOLVED_RUDP_ENDPOINT -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RTS_ELECTRON_SMOKE_RESULT -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Stage 7C Electron shell verifier passed."
