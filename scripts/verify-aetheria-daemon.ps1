param(
    [string] $CultLibRoot = "E:\Projects\CultLib",
    [string] $EveUnityRoot = "E:\Projects\EveUnity",
    [string] $YmirRoot = "E:\Projects\Ymir"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Aetheria.State.Daemon.Smoke\Aetheria.State.Daemon.Smoke.csproj"

foreach ($dependency in @(
    @{ Name = "CultLib"; Path = $CultLibRoot },
    @{ Name = "EveUnity"; Path = $EveUnityRoot },
    @{ Name = "Ymir"; Path = $YmirRoot }
)) {
    if (-not (Test-Path -LiteralPath $dependency.Path -PathType Container)) {
        throw "$($dependency.Name) root does not exist: $($dependency.Path)"
    }
}

$properties = @(
    "-p:CultLibRoot=$CultLibRoot",
    "-p:EveUnityRoot=$EveUnityRoot",
    "-p:YmirRoot=$YmirRoot"
)

& dotnet restore $project @properties
if ($LASTEXITCODE -ne 0) {
    throw "Aetheria daemon dependency restore failed with exit code $LASTEXITCODE."
}

& dotnet run --project $project @properties --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Aetheria daemon smoke failed with exit code $LASTEXITCODE."
}

Write-Host "Aetheria daemon dependency-root and simulation verification passed."
