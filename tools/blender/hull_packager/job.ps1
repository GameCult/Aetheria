# job.ps1 -Scratch <dir> -Game <folder> [-Blender <exe>] [-OpenScad <exe>] [-Lock <starfire_lock.py>]
# The packager's regression job: package_hull.py on the fixture manifest; on a manifest generated from the committed
# aetheria.nakovalnya package, compared (compare.py) with that package; a second run without --replace (must refuse and leave
# the package byte-identical); a --replace that fails (must leave it byte-identical); and AetherDb compose over the result.
# Launch detached. It logs to <Scratch>\job.log and writes <Scratch>\job.status (pid, state, time):
#   Start-Process powershell -ArgumentList '-NoProfile','-File','tools\blender\hull_packager\job.ps1','-Scratch','<dir>','-Game','<dir>\game'
# <Game> holds GameData\Aetheria.cc and ModTools\AetherDb.exe (build_aetherdb.ps1); its GameData\Mods is emptied first.
param(
    [Parameter(Mandatory)] [string]$Scratch,
    [Parameter(Mandatory)] [string]$Game,
    [string]$Blender = "C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe",
    [string]$OpenScad = "C:\Tools\OpenSCAD-2026.09.23-x86-64\openscad.com",
    [string]$Lock = "F:\Projects\eureka-handoff-aux-2026-10-19\starfire_lock.py"
)
$ErrorActionPreference = "Stop"
$Repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$Log = "$Scratch\job.log"
$Status = "$Scratch\job.status"
$Packager = "$Repo\tools\blender\package_hull.py"
function Say($text) { Add-Content -Path $Log -Value "$(Get-Date -Format s) $text" -Encoding utf8 }
function State($text) { Set-Content -Path $Status -Value "pid $PID`n$text`n$(Get-Date -Format s)" -Encoding utf8 }
function Native($label, [scriptblock]$block, [int]$want = 0) {
    Say "BEGIN $label"
    & $block 2>&1 | ForEach-Object { Add-Content -Path $Log -Value "  $_" -Encoding utf8 }
    $code = $LASTEXITCODE
    Say "END $label exit $code"
    if ($code -ne $want) { throw "$label exited $code, wanted $want" }
}
function Digest($dir) { (Get-ChildItem $dir -Recurse -File | Sort-Object FullName | ForEach-Object { "$($_.Name) $((Get-FileHash $_.FullName).Hash)" }) -join "`n" }
function Pack($manifest, $flags = @()) { & $Blender --background --factory-startup --python-use-system-env --python $Packager -- $manifest @flags }

New-Item -ItemType Directory -Force $Scratch | Out-Null
Set-Content -Path $Log -Value "" -Encoding utf8
State "waiting for the Starfire lock"
if (Test-Path $Lock) { & py -3 $Lock acquire hull-packager 2>&1 | ForEach-Object { Say $_ } }
try {
    $env:GAME_FOLDER = $Game
    $env:OPENSCAD = $OpenScad
    $Fixture = "$Repo\tools\blender\tests\fixtures\hulls\zh-ammo-tender\package.json"
    $Committed = "$Repo\GameData\Mods\aetheria.nakovalnya"
    $Mods = "$Game\GameData\Mods"
    $Nak = "$Scratch\nakovalnya.json"
    Set-Location $Game
    Remove-Item -Recurse -Force $Mods -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $Mods | Out-Null

    State "running: fixture"
    Native "packager on the fixture manifest" { Pack $Fixture }
    Native "validate fixture" { & "$Game\ModTools\AetherDb.exe" ship-authoring validate "$Mods\smoke.package.tender\ship.cc" }

    State "running: nakovalnya"
    Native "manifest from the committed package" { & py -3 "$PSScriptRoot\gen_manifest.py" "$Committed\ship.cc" "$Committed\hull.scad" $Nak --like Djinni --extent 37.6 58.4 14.9 --maker Zhestokost }
    Native "packager on the Nakovalnya manifest" { Pack $Nak }
    Native "validate nakovalnya" { & "$Game\ModTools\AetherDb.exe" ship-authoring validate "$Mods\aetheria.nakovalnya\ship.cc" }
    Native "compare to the committed package" { & py -3 "$PSScriptRoot\compare.py" "$Committed\ship.cc" "$Mods\aetheria.nakovalnya\ship.cc" }

    State "running: refusal, failed replace, compose"
    $before = Digest "$Mods\aetheria.nakovalnya"
    Native "second run without --replace refuses" { Pack $Nak } 1
    if ($before -ne (Digest "$Mods\aetheria.nakovalnya")) { throw "the refused run changed the package" }
    Say "REFUSAL LEFT THE PACKAGE BYTE-IDENTICAL"
    # A manifest that fails after the build has started: its first row sits far off the raster (hull.scad is beside it already).
    $bad = Get-Content $Nak -Raw | ConvertFrom-Json
    $bad.rows[0].cell = @(500, 500)
    [IO.File]::WriteAllText("$Scratch\bad.json", ($bad | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding $false))
    Native "failed --replace" { Pack "$Scratch\bad.json" @("--replace") } 1
    if (-not (Select-String -Path $Log -Pattern "PACKAGE_FAILED: row .* raster" -Quiet)) { throw "the --replace failed, but not on the off-raster row" }
    if ($before -ne (Digest "$Mods\aetheria.nakovalnya")) { throw "the failed --replace changed the package" }
    Say "FAILED REPLACE LEFT THE PACKAGE BYTE-IDENTICAL"
    Remove-Item "$Scratch\derived.cc" -ErrorAction SilentlyContinue
    Native "compose" { & "$Game\ModTools\AetherDb.exe" ship-authoring compose "$Game\GameData\Aetheria.cc" "$Scratch\derived.cc" $Mods }
    Say "JOB OK"
    State "done: OK"
} catch {
    Say "JOB FAILED: $_"
    State "done: FAILED $_"
} finally {
    if (Test-Path $Lock) { & py -3 $Lock release hull-packager 2>&1 | ForEach-Object { Say $_ } }
}
