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
        "https://github.com/GameCult/CultLib.git?path=/unity/org.gamecult.cultlib#cultlib-unity-v1.0.27",
        "f3d008023e1c7c41c38354850824dc72f987603d")
    "org.gamecult.eve.surface" = @(
        "https://github.com/GameCult/EveUnity.git?path=/packages/org.gamecult.eve.surface#eveunity-surface-v0.2.4",
        "e08fa08335f99e9edddeb706912eecfad07cb281")
    "org.gamecult.eve.unity-scene" = @(
        "https://github.com/GameCult/EveUnity.git?path=/packages/org.gamecult.eve.unity-scene#eveunity-scene-v0.3.85",
        "e9a25ffc38f799f0fc30f456175dd9539ada8d9a")
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
    Where-Object { (Get-Content (Join-Path $_.FullName "package.json") -Raw | ConvertFrom-Json).version -eq "1.0.27" } |
    Select-Object -First 1
if (-not $cultLibPackage) { throw "Resolved CultLib package is missing from Library/PackageCache." }

$meshAssembly = Join-Path $cultLibPackage.FullName "Runtime/Plugins/GameCult.Mesh.dll"
if (-not (Test-Path $meshAssembly)) { throw "Resolved GameCult.Mesh.dll is missing." }
$meshApi = & rg -a -o "CultMesh[A-Za-z]+" $meshAssembly 2>&1
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect GameCult.Mesh.dll metadata.`n$meshApi" }
$requiredMeshApiNames = @(
    "CultMeshBodyPublicationDocument",
    "CultMeshBodyPublicationHandle",
    "CultMeshFrameBodyPublisher",
    "CultMeshContentServer",
    "CultMeshSessionContentProviderOptions")
foreach ($apiName in $requiredMeshApiNames) {
    if (-not ($meshApi | Where-Object { $_ -eq $apiName })) {
        throw "Resolved GameCult.Mesh.dll does not expose $apiName."
    }
}

$surfacePackage = Get-ChildItem (Join-Path $ProjectPath "Library/PackageCache") -Directory |
    Where-Object Name -Like "org.gamecult.eve.surface@*" |
    Where-Object { (Get-Content (Join-Path $_.FullName "package.json") -Raw | ConvertFrom-Json).version -eq "0.2.4" } |
    Select-Object -First 1
if (-not $surfacePackage) { throw "Resolved Eve surface 0.2.4 package is missing from Library/PackageCache." }
$inputContractSource = Get-Content (Join-Path $surfacePackage.FullName "Runtime/EveInputCapabilityDocument.cs") -Raw
foreach ($requiredInputContract in @("PayloadKeys", "CurrentValue", "ActionBar", "IconRef")) {
    if ($inputContractSource -notmatch $requiredInputContract) {
        throw "Resolved Eve surface package is missing input contract '$requiredInputContract'."
    }
}

$scenePackage = Get-ChildItem (Join-Path $ProjectPath "Library/PackageCache") -Directory |
    Where-Object Name -Like "org.gamecult.eve.unity-scene@*" |
    Where-Object { (Get-Content (Join-Path $_.FullName "package.json") -Raw | ConvertFrom-Json).version -eq "0.3.85" } |
    Select-Object -First 1
if (-not $scenePackage) { throw "Resolved Eve Unity scene 0.3.85 package is missing from Library/PackageCache." }
$advertisedInputSource = Get-Content (Join-Path $scenePackage.FullName "Runtime/EveUnityAdvertisedInputAction.cs") -Raw
$inputDriverSource = Get-Content (Join-Path $scenePackage.FullName "Runtime/EveUnityPlayableWorldInputDriver.cs") -Raw
$actionBarSource = Get-Content (Join-Path $scenePackage.FullName "Runtime/EveUnityInputActionBar.cs") -Raw
foreach ($requiredInputContract in @("view-direction.v1", "BuildViewDirectionPayload", "SubmitChangedPerformedActions", "scalar.v1", "BuildScalarPayload", "EveUnityInputActionBar")) {
    if (($advertisedInputSource + $inputDriverSource + $actionBarSource) -notmatch [regex]::Escape($requiredInputContract)) {
        throw "Resolved Eve Unity scene package is missing generic input lowering '$requiredInputContract'."
    }
}

$sceneSinkSource = Get-Content (Join-Path $scenePackage.FullName "Runtime/EveUnityGameObjectPlayableWorldSceneSink.cs") -Raw
$presentedEntitySource = Get-Content (Join-Path $scenePackage.FullName "Runtime/EveUnityPresentedEntities.cs") -Raw
foreach ($requiredProjectionBoundary in @(
    "IEveUnityPlayableWorldSceneSink",
    "IEveUnityEntityGenerationSink",
    "IEveUnityPresentedEntityRegistry",
    "void ApplyGeneration(EveUnityPresentedEntityGeneration generation)",
    "public Vector3 Position { get; }")) {
    if (($sceneSinkSource + $presentedEntitySource) -notmatch [regex]::Escape($requiredProjectionBoundary)) {
        throw "Resolved Eve Unity scene package is missing read-only presentation boundary '$requiredProjectionBoundary'."
    }
}
foreach ($forbiddenWriteback in @(
    "EveSurfaceCommandRequest",
    "IEveUnitySurfaceCommandTransport",
    "EveUnityCultMeshPlayableWorldProvider")) {
    if (($sceneSinkSource + $presentedEntitySource) -match [regex]::Escape($forbiddenWriteback)) {
        throw "Unity scene projection gained a forbidden provider writeback dependency '$forbiddenWriteback'."
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
