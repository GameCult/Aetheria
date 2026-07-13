param(
    [string] $ProjectPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "Aetheria.Unity")
)

$ErrorActionPreference = "Stop"

function Assert-Equal([object] $Actual, [object] $Expected, [string] $Label) {
    if ($Actual -ne $Expected) {
        throw "$Label expected '$Expected', got '$Actual'."
    }
}

$manifest = Get-Content (Join-Path $ProjectPath "Packages/manifest.json") -Raw | ConvertFrom-Json
$lock = Get-Content (Join-Path $ProjectPath "Packages/packages-lock.json") -Raw | ConvertFrom-Json
$expected = @{
    "org.gamecult.cultlib" = @(
        "https://github.com/GameCult/CultLib.git?path=/unity/org.gamecult.cultlib#cultlib-unity-v1.0.9",
        "b1fcd95c255cd3057894678ff7d2dba28c5eb29e")
    "org.gamecult.eve.surface" = @(
        "https://github.com/GameCult/EveUnity.git?path=/packages/org.gamecult.eve.surface#eveunity-surface-v0.2.0",
        "010ddeff90b70ce5278da373ce2dd0bb2c9eb9ac")
    "org.gamecult.eve.unity-scene" = @(
        "https://github.com/GameCult/EveUnity.git?path=/packages/org.gamecult.eve.unity-scene#eveunity-scene-v0.2.3",
        "61a255a4d5f965c39cce01554a4ec4bce627d24e")
}

foreach ($packageName in $expected.Keys) {
    $dependency = $manifest.dependencies.$packageName
    $resolved = $lock.dependencies.$packageName
    Assert-Equal $dependency $expected[$packageName][0] "$packageName manifest ref"
    Assert-Equal $resolved.version $dependency "$packageName lock ref"
    Assert-Equal $resolved.hash $expected[$packageName][1] "$packageName resolved hash"
}

$cultLibPackage = Get-ChildItem (Join-Path $ProjectPath "Library/PackageCache") -Directory |
    Where-Object Name -Like "org.gamecult.cultlib@*" |
    Select-Object -First 1
if (-not $cultLibPackage) { throw "Resolved CultLib package is missing from Library/PackageCache." }

$meshAssembly = Join-Path $cultLibPackage.FullName "Runtime/Plugins/GameCult.Mesh.dll"
if (-not (Test-Path $meshAssembly)) { throw "Resolved GameCult.Mesh.dll is missing." }
$meshApi = & rg -a -o "CultMesh[A-Za-z]+" $meshAssembly 2>&1
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect GameCult.Mesh.dll metadata.`n$meshApi" }
$bodyApiNames = @("CultMeshBodyPublicationDocument", "CultMeshBodyPublicationHandle", "CultMeshFrameBodyPublisher")
foreach ($apiName in $bodyApiNames) {
    if (-not ($meshApi | Where-Object { $_ -eq $apiName })) {
        throw "Resolved GameCult.Mesh.dll does not expose $apiName."
    }
}

$assemblies = Get-ChildItem (Join-Path $ProjectPath "Library/ScriptAssemblies") -Filter *.dll -ErrorAction Stop |
    Select-Object -ExpandProperty Name
$forbidden = $assemblies | Where-Object { $_ -match "^(Aetheria|GameCult\.Aetheria)(\.|$)|ServerShared" }
if ($forbidden) { throw "Aetheria gameplay/ServerShared assemblies entered the Unity client: $($forbidden -join ', ')" }

$bootstrapSource = Get-Content (Join-Path $ProjectPath "Assets/AetheriaUnityClient.cs") -Raw
foreach ($required in @("EVEUNITY_RENDEZVOUS_ENDPOINT", "EveUnityCultMeshPlayableWorldProvider", "ConfigureProvider")) {
    if ($bootstrapSource -notmatch [regex]::Escape($required)) { throw "Client bootstrap is missing discovery input '$required'." }
}
foreach ($forbiddenInput in @("AetheriaRuntimeVerseSchemas", "AetheriaRuntimeVerseRecordKeys", "productSchema", "recordKey")) {
    if ($bootstrapSource -match [regex]::Escape($forbiddenInput)) { throw "Client bootstrap depends on forbidden product input '$forbiddenInput'." }
}

Write-Host "Aetheria Unity client dependency and bootstrap verification passed."
