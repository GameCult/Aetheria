param(
  [Parameter(Mandatory = $true)] [string] $Root,
  [Parameter(Mandatory = $true)] [string] $State,
  [Parameter(Mandatory = $true)] [string] $CultLibRoot,
  [Parameter(Mandatory = $true)] [string] $YmirRoot,
  [Parameter(Mandatory = $true)] [string] $EveUnityRoot,
  [switch] $ForceImport
)

$ErrorActionPreference = "Stop"
$rootPath = [IO.Path]::GetFullPath($Root)
$statePath = [IO.Path]::GetFullPath($State)
$daemonProject = Join-Path $rootPath "Aetheria.State.Daemon\Aetheria.State.Daemon.csproj"
$importProject = Join-Path $rootPath "Aetheria.State.Import\Aetheria.State.Import.csproj"
$daemonExe = Join-Path $rootPath "Aetheria.State.Daemon\bin\Debug\net10.0\Aetheria.State.Daemon.exe"
$recordsPath = "$statePath.records"
$cultMeshPath = [IO.Path]::ChangeExtension($statePath, ".cultmesh")
$ymirStatePath = "$statePath.ymir.cc"
$ymirRecordsPath = "$ymirStatePath.records"
$cultLibPath = [IO.Path]::GetFullPath($CultLibRoot)
$ymirPath = [IO.Path]::GetFullPath($YmirRoot)
$eveUnityPath = [IO.Path]::GetFullPath($EveUnityRoot)

foreach ($required in @($daemonProject, $importProject, $cultLibPath, $ymirPath, $eveUnityPath)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Required Aetheria development path is missing: $required"
  }
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $statePath) | Out-Null

& dotnet build $daemonProject -c Debug --nologo `
  "-p:CultLibRoot=$cultLibPath" "-p:YmirRoot=$ymirPath" "-p:EveUnityRoot=$eveUnityPath"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $daemonExe)) {
  throw "Aetheria Debug daemon build failed with exit code $LASTEXITCODE."
}

if ($ForceImport) {
  foreach ($ownedStatePath in @($statePath, $recordsPath, $cultMeshPath, $ymirStatePath, $ymirRecordsPath)) {
    if (Test-Path -LiteralPath $ownedStatePath) {
      Remove-Item -LiteralPath $ownedStatePath -Recurse -Force
    }
  }
}
$hasImportedState = (Test-Path -LiteralPath $statePath) -and
  ((Test-Path -LiteralPath $cultMeshPath) -or (Test-Path -LiteralPath $recordsPath))
if (-not $hasImportedState) {
  & dotnet run --project $importProject -c Debug `
    "-p:CultLibRoot=$cultLibPath" "-p:YmirRoot=$ymirPath" "-p:EveUnityRoot=$eveUnityPath" `
    -- $rootPath $statePath
  $hasImportedState = (Test-Path -LiteralPath $statePath) -and
    ((Test-Path -LiteralPath $cultMeshPath) -or (Test-Path -LiteralPath $recordsPath))
  if ($LASTEXITCODE -ne 0 -or -not $hasImportedState) {
    throw "Aetheria development-state import failed with exit code $LASTEXITCODE."
  }
}

Write-Host "Aetheria Debug daemon preparation complete: $daemonExe"
