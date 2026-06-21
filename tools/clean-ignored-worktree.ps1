param(
    [switch]$CleanBuild,
    [switch]$CleanDaemonGenerated,
    [switch]$CleanUnityCache,
    [switch]$CleanWwiseGenerated,
    [switch]$IncludeAgentState,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

function Invoke-GitClean {
    param(
        [string]$Label,
        [string[]]$Paths
    )

    if ($Paths.Count -eq 0) {
        return
    }

    Write-Host ""
    Write-Host "== $Label =="

    $mode = if ($Apply) { "-fdX" } else { "-ndX" }
    & git clean $mode -- @Paths
}

function Get-PathSizeMb {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $size = (Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum

    [PSCustomObject]@{
        Path = $Path
        MB = [math]::Round(($size / 1MB), 2)
    }
}

function Show-Summary {
    $ignoredCount = (& git ls-files -i -o --exclude-standard | Measure-Object).Count
    Write-Host "Ignored path count: $ignoredCount"

    $roots = @(
        "Library",
        "Temp",
        "GameData",
        "Aetheria-Economy_WwiseProject",
        "Aetheria.State\bin",
        "Aetheria.State\obj",
        "Aetheria.State.Verify\bin",
        "Aetheria.State.Verify\obj",
        "Aetheria.State.Unity\bin",
        "Aetheria.State.Unity\obj",
        "obj",
        ".brokkr",
        ".codex",
        ".idea",
        "UserSettings"
    )

    $sizes = foreach ($root in $roots) {
        Get-PathSizeMb $root
    }

    $sizes |
        Where-Object { $null -ne $_ } |
        Sort-Object MB -Descending |
        Format-Table -AutoSize
}

$anyCleanSwitch =
    $CleanBuild -or
    $CleanDaemonGenerated -or
    $CleanUnityCache -or
    $CleanWwiseGenerated -or
    $IncludeAgentState

if (-not $anyCleanSwitch) {
    Show-Summary
    Write-Host ""
    Write-Host "Dry-run examples:"
    Write-Host "  tools\clean-ignored-worktree.ps1 -CleanBuild"
    Write-Host "  tools\clean-ignored-worktree.ps1 -CleanDaemonGenerated"
    Write-Host "  tools\clean-ignored-worktree.ps1 -CleanUnityCache"
    Write-Host ""
    Write-Host "Add -Apply to actually delete the selected ignored paths."
    exit 0
}

if ($CleanBuild) {
    Invoke-GitClean "Build and generated IDE files" @(
        "Aetheria.State/bin",
        "Aetheria.State/obj",
        "Aetheria.State.Daemon/bin",
        "Aetheria.State.Daemon/obj",
        "Aetheria.State.Import/bin",
        "Aetheria.State.Import/obj",
        "Aetheria.State.Replica/bin",
        "Aetheria.State.Replica/obj",
        "Aetheria.State.Smoke/bin",
        "Aetheria.State.Smoke/obj",
        "Aetheria.State.Unity/bin",
        "Aetheria.State.Unity/obj",
        "Aetheria.State.Unity.Smoke/bin",
        "Aetheria.State.Unity.Smoke/obj",
        "Aetheria.State.Verify/bin",
        "Aetheria.State.Verify/obj",
        "Economy.Server/bin",
        "Economy.Server/obj",
        "Economy.Shared/bin",
        "Economy.Shared/obj",
        "obj"
    )
}

if ($CleanDaemonGenerated) {
    Invoke-GitClean "Daemon generated CultCache and Eve sidecars" @(
        "GameData/*.cc.daemon.*.cc",
        "GameData/*.cc.records",
        "GameData/*.cc.records.*",
        "GameData/*.cultmesh",
        "GameData/*.cc.eve.pending",
        "GameData/*.cc.before-*"
    )
}

if ($CleanUnityCache) {
    Invoke-GitClean "Unity transient cache" @(
        "Library",
        "Temp",
        "UserSettings"
    )
}

if ($CleanWwiseGenerated) {
    Invoke-GitClean "Wwise generated/cache/profiling files" @(
        "Aetheria-Economy_WwiseProject/.cache",
        "Aetheria-Economy_WwiseProject/GeneratedSoundBanks",
        "Aetheria-Economy_WwiseProject/*.prof",
        "Assets/StreamingAssets/Audio/GeneratedSoundBanks"
    )
}

if ($IncludeAgentState) {
    Invoke-GitClean "Local agent/editor runtime state" @(
        ".brokkr",
        ".codex",
        ".idea"
    )
}
