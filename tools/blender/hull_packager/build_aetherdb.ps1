# build_aetherdb.ps1 -Game <folder> -CultLibRoot <dir> -CultMathRoot <dir> [-Repo <dir>]
# Publishes tools/AetherDb into <Game>\ModTools, the AetherDb.exe package_hull.py runs. <Game> also needs GameData\Aetheria.cc
# (the shipped catalog's content, not its LFS pointer). CultLibRoot and CultMathRoot are CultLib worktrees at the revisions
# Directory.Build.props names for this repo revision (git worktree add --detach <dir> <rev>).
# ErrorOnDuplicatePublishOutputFiles=false is the NETSDK1152 workaround: Aetheria.Shared and CultLib publish the same files.
param(
    [Parameter(Mandatory)] [string]$Game,
    [Parameter(Mandatory)] [string]$CultLibRoot,
    [Parameter(Mandatory)] [string]$CultMathRoot,
    [string]$Repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
)
$ErrorActionPreference = "Stop"
& dotnet publish "$Repo\tools\AetherDb" -c Release -o "$Game\ModTools" "-p:CultLibRoot=$CultLibRoot" "-p:CultMathRoot=$CultMathRoot" "-p:ErrorOnDuplicatePublishOutputFiles=false"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish exited $LASTEXITCODE" }
if (-not (Test-Path "$Game\ModTools\AetherDb.exe")) { throw "no AetherDb.exe in $Game\ModTools" }
