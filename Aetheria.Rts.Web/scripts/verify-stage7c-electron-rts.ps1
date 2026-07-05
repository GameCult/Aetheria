$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7C Electron RTS verifier failed: RTS build failed."
}

if (-not (Get-Content wwwroot\index.html -Raw).Contains('eve-surface-host')) {
    Write-Error "Stage 7C Electron RTS verifier failed: built renderer markup does not host the daemon Eve surface."
}

$appText = Get-Content wwwroot\app.js -Raw
if (-not $appText.Contains('window.aetheriaRts.eveSurface') -or
    $appText.Contains('legacyViewportMode') -or
    $appText.Contains('mainMenuSurface') -or
    $appText.Contains('main-menu-mode')) {
    Write-Error "Stage 7C Electron RTS verifier failed: renderer is not daemon-Eve-only."
}

$mainText = Get-Content Electron\main.ts -Raw
if (-not $mainText.Contains('await api.surfaceCatalog()') -or -not $mainText.Contains('await api.surfaceCatalogIndex()')) {
    Write-Error "Stage 7C Electron RTS verifier failed: Electron smoke does not call the preload CultMesh surface catalog APIs."
}

if (-not $mainText.Contains('await api.renderSplatsViewport') -or
    -not $mainText.Contains('eveFieldSurface') -or
    -not $mainText.Contains('embeddedDocuments') -or
    -not $mainText.Contains('fog.tint')) {
    Write-Error "Stage 7C Electron RTS verifier failed: Electron smoke does not verify daemon Eve field document lowering."
}

if (-not $mainText.Contains('await api.submitEveCommand') -or
    -not $mainText.Contains('eveReceipt?.commandId') -or
    -not $mainText.Contains('eveReceipt?.accepted') -or
    -not $mainText.Contains('eveReceipt?.route')) {
    Write-Error "Stage 7C Electron RTS verifier failed: Electron smoke does not verify typed operation receipts through preload."
}

if (-not $mainText.Contains('AETHERIA_VERSE_ID') -or
    -not $mainText.Contains('--verse-id') -or
    -not $mainText.Contains('AETHERIA_DAEMON_ID') -or
    -not $mainText.Contains('--daemon-id')) {
    Write-Error "Stage 7C Electron RTS verifier failed: Electron launcher does not configure daemon Verse identity."
}
if ($mainText.Contains('AETHERIA_RTS_') -or
    $mainText.Contains('starfire-rts') -or
    $mainText.Contains('aetheria-rts-daemon') -or
    $mainText.Contains('aetheria-rts-electron') -or
    $mainText.Contains('rtsCultMesh') -or
    $mainText.Contains('rtsDaemonId') -or
    $mainText.Contains('rtsVerseId')) {
    Write-Error "Stage 7C Electron RTS verifier failed: Electron launcher still uses RTS-branded daemon connection authority."
}

$electron = Join-Path $root "node_modules\electron\dist\electron.exe"
if (!(Test-Path -LiteralPath $electron)) {
    Write-Error "Stage 7C Electron RTS verifier failed: electron.exe was not found at $electron."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("aetheria-stage7c-electron-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$resultPath = Join-Path $tempRoot "electron-smoke-result.json"
$udp = [System.Net.Sockets.UdpClient]::new(0)
$port = ([System.Net.IPEndPoint]$udp.Client.LocalEndPoint).Port
$udp.Close()

try {
    $env:AETHERIA_ELECTRON_SMOKE = "1"
    $env:AETHERIA_RUNTIME_ROOT = $tempRoot
    $env:AETHERIA_CLIENT_CULTMESH_PORT = $port.ToString()
    $env:AETHERIA_VERSE_ID = "aetheria.stage7c.electron"
    $env:AETHERIA_DAEMON_ID = "stage7c-aetheria-daemon"
    $env:AETHERIA_ELECTRON_SMOKE_RESULT = $resultPath
    $process = Start-Process -FilePath $electron -ArgumentList "." -WorkingDirectory $root -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Error "Stage 7C Electron RTS verifier failed: Electron exited with code $($process.ExitCode)."
    }

    if (!(Test-Path -LiteralPath $resultPath)) {
        Write-Error "Stage 7C Electron RTS verifier failed: Electron did not write a smoke result."
    }

    $result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
    if ($result.ok -ne $true) {
        Write-Error "Stage 7C Electron RTS verifier failed: $($result.error)"
    }
}
finally {
    Remove-Item Env:\AETHERIA_ELECTRON_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_RUNTIME_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_CLIENT_CULTMESH_PORT -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_VERSE_ID -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_DAEMON_ID -ErrorAction SilentlyContinue
    Remove-Item Env:\AETHERIA_ELECTRON_SMOKE_RESULT -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Stage 7C Electron RTS verifier passed."
