param(
    [string] $CultLibRoot = "",
    [string] $EveUnityRoot = "",
    [string] $YmirRoot = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectsRoot = Split-Path -Parent $repoRoot
if ([string]::IsNullOrWhiteSpace($CultLibRoot)) { $CultLibRoot = Join-Path $projectsRoot "CultLib" }
if ([string]::IsNullOrWhiteSpace($EveUnityRoot)) { $EveUnityRoot = Join-Path $projectsRoot "EveUnity" }
if ([string]::IsNullOrWhiteSpace($YmirRoot)) { $YmirRoot = Join-Path $projectsRoot "Ymir" }
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

$ymirSession = Join-Path $YmirRoot "src\Ymir.Core\YmirSession.cs"
$box2DProject = Join-Path $YmirRoot "src\Ymir.Box3D\Ymir.Box3D.csproj"
$box2DSource = Join-Path $YmirRoot "extern\box3d\CMakeLists.txt"
if (-not (Test-Path -LiteralPath $ymirSession -PathType Leaf) -or
    -not (Test-Path -LiteralPath $box2DProject -PathType Leaf)) {
    throw "Ymir does not expose the retained Box2D session API required by Aetheria. Use the current Aetheria integration revision."
}
if (-not (Test-Path -LiteralPath $box2DSource -PathType Leaf)) {
    throw "Ymir's pinned Box2D submodule is missing. Run 'git -C $YmirRoot submodule update --init --recursive'."
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
