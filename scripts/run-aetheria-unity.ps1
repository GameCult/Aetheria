param(
  [string] $UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe",
  [int] $Port = 3076,
  [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Aetheria.Unity"
$artifacts = Join-Path $project "Build\Logs"
$clientExe = Join-Path $project "Build\Windows\Aetheria.exe"
$daemonProject = Join-Path $root "Aetheria.State.Daemon\Aetheria.State.Daemon.csproj"
$state = Join-Path $project "Build\aetheria-unity.cc"
New-Item -ItemType Directory -Force $artifacts | Out-Null

if (-not $SkipBuild) {
  $assetLog = Join-Path $artifacts "asset-bundles.log"
  $assets = Start-Process $UnityExe -ArgumentList @(
    "-batchmode", "-quit", "-projectPath", $root,
    "-executeMethod", "Aetheria.Editor.EveAssetBundleBuilder.BuildWindows",
    "-logFile", $assetLog
  ) -PassThru -WindowStyle Hidden
  Write-Host "Asset build PID: $($assets.Id)"
  Write-Host "Asset build log: $assetLog"
  if (-not $assets.WaitForExit(240000)) { Stop-Process $assets.Id -Force; throw "Asset build timed out." }
  if ($assets.ExitCode -ne 0) { Get-Content $assetLog -Tail 120; throw "Asset build failed." }

  $clientLog = Join-Path $artifacts "client-build.log"
  $build = Start-Process $UnityExe -ArgumentList @(
    "-batchmode", "-quit", "-projectPath", $project,
    "-executeMethod", "AetheriaUnityBuild.BuildWindows",
    "-logFile", $clientLog
  ) -PassThru -WindowStyle Hidden
  Write-Host "Client build PID: $($build.Id)"
  Write-Host "Client build log: $clientLog"
  if (-not $build.WaitForExit(300000)) { Stop-Process $build.Id -Force; throw "Client build timed out." }
  if ($build.ExitCode -ne 0 -or -not (Test-Path $clientExe)) { Get-Content $clientLog -Tail 120; throw "Client build failed." }
}

if (-not (Test-Path $clientExe)) { throw "Client executable not found. Run without -SkipBuild first." }
$daemonLog = Join-Path $artifacts "daemon.log"
$daemon = Start-Process dotnet -ArgumentList @(
  "run", "--project", $daemonProject, "--",
  "--root", $root,
  "--state", $state,
  "--client-cultmesh-host", "127.0.0.1",
  "--client-cultmesh-advertise-host", "127.0.0.1",
  "--client-cultmesh-port", $Port,
  "--tick-interval-ms", 250,
  "--fixed-delta-ms", 20,
  "--no-odin-announcements"
) -PassThru -WindowStyle Hidden -RedirectStandardOutput $daemonLog -RedirectStandardError "$daemonLog.error"
Write-Host "Daemon PID: $($daemon.Id)"
Write-Host "Daemon log: $daemonLog"

try {
  $ready = $false
  for ($i = 0; $i -lt 60; $i++) {
    if ($daemon.HasExited) { throw "Aetheria daemon exited. See $daemonLog" }
    if ((Test-Path $daemonLog) -and (Select-String $daemonLog -Pattern "Aetheria client CultMesh endpoint: rudp://127.0.0.1:$Port" -Quiet)) { $ready = $true; break }
    Start-Sleep -Milliseconds 500
  }
  if (-not $ready) { throw "Aetheria daemon did not publish its Eve endpoint." }
  $env:EVEUNITY_RENDEZVOUS_ENDPOINT = "rudp://127.0.0.1:$Port"
  $env:EVEUNITY_SURFACE_ID = "aetheria.pilot"
  $client = Start-Process $clientExe -ArgumentList "-force-d3d11" -PassThru
  Write-Host "Client PID: $($client.Id)"
  Write-Host "Close the client window to stop the daemon."
  $client.WaitForExit()
}
finally {
  if (-not $daemon.HasExited) { Stop-Process $daemon.Id -Force }
}
