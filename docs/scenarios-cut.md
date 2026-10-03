# Scenarios: Cut Map

Date: 2026-09-30, 02:10 CEST. Imagination pass.

Status: Cut 1 and Cut S2 are on master (S2 merged at `a24ffd77`, branch `eureka/aetheria-release-scenarios-menu`, cut
reports `aetheria-release:cut_report:cut-scenarios-menu.h1` and `.h2`). Every new run now comes from a `Scenario`;
section R.9 is the map of the machine as built and supersedes R.4 where they differ. Cut S3 (test designs and the
four scenarios that need them) has not started. Everything outside R.9 and R.6's S3 is history. This document owns the
means; the campaign is `aetheria-release` (ruling `adopt-mining-and-scenarios`).

Anchors are against Aetheria `origin/master` `dcd7bbc5` (the fire-control merge), read in the clone
`C:\ws72-fuse5` with `git show`/`git grep`. The operator's tree `F:\Projects\Aetheria` was not touched. No
build ran and no Yggdrasil job ran. **(read)** marks a claim read from source at the anchor. **(probe)** marks a
claim measured against a real file.

---

## R. Re-scope of Cuts 2-4 (Imagination, 2026-10-03)

This section owns the remaining scenarios work. Where it disagrees with sections 0.2, 0b, 2.1, 2.3, 2.4, 3 (Cuts 2-4),
4, 5.1 and 6, this section is current and those are history. Anchors are against `origin/master` `4895c752`.

### R.1 The request, in the operator's words (2026-10-03)

> "Map the rest of the scenario cut. This should be a pretty trivial harness to set up, each scenario consists of a
> script that sets up the world in whatever way it needs to, leaning on existing galaxy and zone generation
> primitives. And we can populate a main menu submenu with those options. My menu code makes that almost a one
> liner."

> "That's just how I would build it. Keeps things simple, does the job, can be extended, even establishes tools for
> in game scripting that would be handy for modders."

> "Have you forgotten that the game is open source? We don't need to ship modding tools if anyone can compile plugins
> and addons with the same toolchain we use"

So a scenario is a C# script compiled with the game. A modder adds one the way we do: a class in the source tree.
There is no command-sequence format and no script runtime.

Earlier, the purpose: "Scenarios as in game environments specifically crafted to test various game systems, to be
accessible via the main menu so I don't have to manually recreate all the wild situations the smoke script
previously asked me to verify to prove the game is working."

### R.2 What landed (Cut 1), and what it means now

On master **(read)**:
- `RunStart` (`Assets/Scripts/ServerShared/RunStart.cs`, 131 lines): `GenerateArena`, `Check`, `Stage`, and the
  private `Build`/`Place`. `StartGame` (`ActionGameManager.cs:812-820`) calls it with no scenario.
- `Zone.Admit` (`Zone.cs:138`), the one admission primitive.
- The `Scenario` catalog record (`aetheria.scenario` v1) with `ScenarioShip`, `ScenarioEntity`, `ScenarioStance`
  (`Scenario.cs`), listed in `AetheriaStores.CatalogTypes` (`:9`).
- `Loadouts.Materialize` takes a `Loadout` **instance** (`Loadout.cs:104`), not a ref, so a fit built in code needs
  no catalog record. The hull type picks the entity class.
- **(probe)** `GameData/Aetheria.cc` at `4895c752` holds no `aetheria.scenario` record, no `aetheria.loadout` record
  and no `Smoke *` design (`grep -a -c`). Deleting the scenario record type migrates no data.

The landed record type is a data shape: a player and a list of placed presets. The operator's words make a scenario a
code shape, and the two game modes scenarios too (R.3). S2 therefore replaces the record with a script class. Nothing is lost: no record exists, and a script can
still use a `capturepreset` preset by name.

### R.3 Rulings of 2026-10-03

- **`game-modes-are-scenarios`.** Operator: "Btw, the two game modes we have now should also be scenarios. The
  tutorial Galaxy and the main Galaxy are two ways of setting the game up, so they're scenarios now, too." New Game
  has no setup path of its own.
- **`verification-ledger-b`.** Operator: "Agree with b." The typed ledger is deferred (follow-up
  `scenarios-verification-ledger`). Each scenario's `Brief` names what to verify; results stay in the prose checklist
  (`docs/merge-to-master-checklist.md` section 8). This supersedes the Q2 B ruling of 2026-09-30.
- **`weapon-feel-to-smoke-cut-test-hull`** (option A). The Hands probe on master found that no ship hull has a 2x2
  energy hardpoint, so ChargeBlast SG fits nothing. The Autocannon (ballistic 2x2) fits only the turret, and no ship
  hull has a side mount: every mount faces forward with the default 120 degree arc. Weapon feel and the Arcs beam
  check therefore move to S3, on one product-less test hull with a 2x2 energy hardpoint, a 2x2 ballistic hardpoint
  and side mounts. S2's Arcs keeps the bow, stern and turret checks.

### R.4 Target shape

**A scenario sets up the whole game: the galaxy and the run's first entities.** The two game modes and every test
arena are scenarios, staged by one owner through one path.

- **`Scenario`**, an abstract C# class (`Assets/Scripts/ServerShared/Scenario.cs`, rewritten):
  - `Name`, and `Brief` (the conditions and what to verify; shown under the button).
  - `virtual uint Seed => 0`. Zero means the clock, as `Galaxy`'s constructors already treat it
    (`Galaxy.cs:108,158`): the two game modes are new every time. A test arena overrides it with a fixed seed, so it
    has the same layout at every launch.
  - `abstract Galaxy Generate(GalaxyStage stage)`: which galaxy.
  - `virtual bool Ambient => true`: whether the entrance zone keeps its generated ships and turrets. Test arenas
    override it to false.
  - `abstract void Stage(ScenarioStage stage)`: what the run starts with.
- **`GalaxyStage`**, the galaxy half of the vocabulary. It wraps the existing constructors and takes their inputs
  (the sector, tutorial, background and name settings, the cache, `PlayerSettings`, the narrative directory, log and
  progress callback), which the menu hands to `RunStart`:
  - `Galaxy Main()`: `new Galaxy(SectorGenerationSettings, ...)` at the scenario's seed.
  - `Galaxy Prelude()`: `new Galaxy(TutorialGenerationSettings, ..., narrativeDirectory, ...)` at the scenario's seed.
  - Both derive the background noise position from the seed instead of `UnityEngine.Random`
    (`MainMenu.cs:137,162`). `Prelude` keeps the search for a noise position whose cloud density at the centre is at
    least 0.5 (`:160-164`), drawing from the seeded random. Seed zero draws from the clock, so the modes still look
    different every time.
- **`ScenarioStage`**, the run half. Only `RunStart` builds it. Every verb collects failures by name, and nothing is
  admitted until `Stage` returns clean:
  - `Loadout Fit(string hull, params (string design, int2 cell, ItemRotation rotation)[] slots)` and
    `Loadout Bare(string hull)`: an in-memory `Loadout`.
  - `Loadout Generated(string hull, Faction faction = null)`: a `LoadoutGenerator` fit. An empty `hull` means any
    ship hull. This is the old no-scenario branch of `RunStart.Stage`, now a verb.
  - `Loadout Preset(string name)`: a `capturepreset` record.
  - `Ship Player(Loadout fit, float2 at, float2 facing = default)`.
  - `Entity Place(Loadout fit, float2 at, float2 facing = default, ScenarioStance stance = Neutral, bool piloted = false)`.
  - `void Cargo(Entity entity, params string[] designs)`.
  - `Galaxy`, `Arena`, `StartingHull` (`GameSettings.StartingHullName`) and `TutorialGenerationSettings`, for
    reading.
- **The two game modes** (`Assets/Scripts/ServerShared/Scenarios/`):
  - `TutorialGalaxy`: `Generate` returns `stage.Prelude()`. `Stage` places
    `Generated(StartingHull, Galaxy.ResolveFaction(stage.TutorialGenerationSettings.ProtagonistFaction))` at the
    origin. `ScenarioStage` exposes `TutorialGenerationSettings` for this.
    This is today's New Game, since `TutorialPassed` is never set and New Game always takes the prelude branch.
  - `MainGalaxy`: `Generate` returns `stage.Main()`. `Stage` places `Generated(StartingHull)` with no faction at the
    origin. This is today's `TutorialPassed` branch, which is unreachable.
- **`Scenarios`**: two static arrays. `Modes` holds `TutorialGalaxy` and `MainGalaxy`. `Tests` holds the test arenas.
  There is no flag field on `Scenario`.
- **`RunStart` is the one owner of a new run.** `RunStart.Generate(scenario, galaxyStage)` builds the galaxy.
  `GenerateArena(..., scenario)` applies `Ambient`. `Stage(itemManager, arena, scenario, ...)` runs `scenario.Stage`,
  then admits through `Zone.Admit`. A scenario is always present: there is no null branch.
- **Menu.** "New Game" opens a submenu listing `Scenarios.Modes` and, when `Debug.isDebugBuild`, `Scenarios.Tests`
  after them, then Back. Each button runs one `Launch(Scenario)`, which:
  1. clears the run;
  2. shows the "Generating Galaxy" dialog;
  3. on the background task, calls `RunStart.Generate`;
  4. next frame, sets `CurrentGalaxy` and `PendingScenario`, then `EnterGame`.

  This is the operator's "main menu submenu", and it puts the two modes "in" the list her ruling put them in. Self
  chose it. Having Tutorial and Main as two top-level buttons is the alternative, and it costs the same if she
  prefers it.
- **`PendingScenario`** is command-only transport. `Launch` writes it, and `StartGame`'s new-run branch reads and nulls
  it. A new run with no pending scenario is an error.
- **`ActionGameManager.IsTutorial` is no longer an owner.** It duplicates `Galaxy.IsPrelude`: `Galaxy`'s save
  constructor already sets `IsPrelude = savedGame.IsTutorial` (`Galaxy.cs:50`). Its readers read
  `CurrentGalaxy.IsPrelude` instead: `RunSave.Capture` (`ActionGameManager.cs:253`; it drops its `isTutorial`
  parameter and reads `galaxy.IsPrelude`), `spawnturret` (`:581`),
  `PopulateLevel` (`:731`) and `StartGame` (`:818`). `SavedGame.IsTutorial` stays as the persisted field.
- **`PlayerSettings.TutorialPassed` is dead.** It is never written, and its only reader is the deleted branch.

### R.5 What the old map's machinery becomes

| Old machinery | Now | |
|---|---|---|
| `aetheria.scenario` record, `ScenarioShip`, `ScenarioEntity` | replaced by script classes | **cut** in S2 |
| the no-scenario path of `RunStart.Stage` and New Game's own galaxy branches | the two mode scenarios | **cut** in S2 |
| `ActionGameManager.IsTutorial`, `PlayerSettings.TutorialPassed` | `Galaxy.IsPrelude`; nothing | **cut** in S2 |
| `aetheria.loadout` presets | optional script input | **keep** |
| `AetherDb scenario-seed` | scripts are code | **cut** (never built) |
| check entry before `RunSave.Clear` (`RunStart.Check`) | a script may read the arena | **cut**; the rot test stages every scenario headless |
| ledger (old Cut 3) | ruled `verification-ledger-b` | **deferred**, follow-up `scenarios-verification-ledger` |
| per-scenario check lists (5.1) | `Brief` | **fold** |
| test designs (Q3 A) | needed by the fused scenarios | **keep**, S3 |

### R.6 Cuts

S2 alone delivers the whole menu: the two modes and six test arenas. S3 adds the four that need test designs.

**Cut S2. Every setup is a scenario: the harness, the two game modes, the New Game submenu, and seven test arenas.**
Branch `eureka/aetheria-release-scenarios-menu` from `origin/master`. The arenas: Djinni shakedown, Starved reactor,
Arcs (bow, stern and turret checks only), Duel, Launcher angles, Long haul (`Ambient` true). Mind:
`aetheria-release:cut_spec:cut-scenarios-menu.r3`, which supersedes r2 and r1.

**Cut S3. Test designs and the scenarios that need them** (content), on S2. Five product-less smoke weapons (5.3)
and one product-less test hull, `Smoke Test Hull`, with a 2x2 energy hardpoint, a 2x2 ballistic hardpoint and side
mounts. A transient `AetherDb smoke-designs apply` writes all six and is deleted in the same cut. Scenarios: Fused
rounds, Refused rounds, AI fused discipline, and Weapon feel (on the test hull). Arcs gains its beam target and
check on the test hull. When S3 lands, Self runs `git checkout -- GameData/Aetheria.cc` in `F:\Projects\Aetheria`.
Mind: `aetheria-release:cut_spec:cut-scenarios-smoke.r2`, which supersedes r1.

### R.7 Questions

None open. `verification-ledger` was answered B.

### R.8 Ledger

| Cut | Removed | Added |
|---|---|---|
| S2 | record types 45; `RunStart.Check`, `Build`, `Place` and the null branch 90; New Game's two galaxy branches 60; `IsTutorial` 6; `TutorialPassed` 1; record-shaped test scaffolding 60 | `Scenario` 25; `GalaxyStage` 50; `ScenarioStage` 95; `RunStart` 25; two modes 30; six arenas 130; menu 35; tests 140 |
| S3 | the transient command, once spent | five weapon designs and one test hull (catalog); four scenarios 95; the Arcs beam target 5; condition tests 100 |

S2 removes the `aetheria.scenario` schema. No store, target, package or daemon is added.

### R.9 The machine as built (Modeling, 2026-10-04; anchors `origin/master` `a24ffd77`, **(read)**)

Owner, inputs, outputs and the rest, per authority. Where this differs from R.4 the build wins (marked *differs*).

**Flow.** `MainMenu.ShowScenarios` (lists `Scenarios.Modes`, then `Scenarios.Tests` when `Debug.isDebugBuild`) ->
`MainMenu.Launch(scenario)` builds a `GalaxyStage` from the settings and runs `RunStart.Generate` on a background task
-> next frame writes `ActionGameManager.CurrentGalaxy` and `PendingScenario`, loads scene `ARPG` -> `StartGame` sees
`PendingScenario != null` and calls `StartScenario` -> inside `RunSave.Replace`: `RunStart.GenerateArena`,
`PopulateLevel(Entrance)`, `RunStart.Stage` -> `BindToEntity(staged.Player)`. Continue (no pending scenario) takes the
saved-run branch of `StartGame` unchanged.

| Authority | Owner | Inputs | Outputs | Derived state | Forbidden writers |
|---|---|---|---|---|---|
| Which setups exist | `Scenarios` (`Modes`, `Tests`) | source | the menu's list | none | the menu (it lists, never adds), `ActionGameManager` |
| Setup of one run | `Scenario` subclass (`Name`, `Brief`, `Seed`, `Ambient`, `Generate`, `Stage`) | `GalaxyStage`, `ScenarioStage` | a `Galaxy`; staged fits | none; it declares and holds no Unity type | anything else choosing a new run's galaxy, hull, faction or player |
| Seed | `Scenario.Seed`; `GalaxyStage.SeedFrom` | scenario seed, clock | `GalaxyStage.Seed`, never 0 | background noise position (`Main`, `Prelude`), `Galaxy.Seed`, item random | `UnityEngine.Random` (no longer read for galaxy setup) |
| Galaxy construction | `GalaxyStage` (`Main()`, `Prelude()`), `RunStart.Generate` | sector/tutorial/background/name settings, `CultCache`, `PlayerSettings`, narrative directory, log, progress, clock | `Galaxy` | `Galaxy.IsPrelude`, zone links | `MainMenu` (it only builds the stage and hands it over) |
| Zone links | `Galaxy` via `Delaunay.Edges` (double precision, Bowyer-Watson, `ServerShared/Delaunay.cs`) | zone positions | edges, then the existing link elimination | connected graph for 2+ distinct points | the vendored `NIH/MIConvexHull` (deleted: float precision dropped zones, ~2% of galaxies threw) |
| Arena | `RunStart.GenerateArena` -> `ZoneGenerator.GenerateZone(..., galaxy.IsPrelude, scenario.Ambient)` | item manager, zone settings, galaxy, scenario | `Entrance.PackedContents` | stations and orbits kept when `Ambient` is false; generated ships and turrets dropped | `Scenario` (declares `Ambient` only) |
| Item random for staging | `RunStart.SeedItems` | `galaxy.Seed * 0x9E3779B1u + step` (step 1 arena, step 2 staging) | `ItemManager.Random` | lot quality, generated fits, maker and workmanship | the process clock; `PopulateLevel` cannot shift it (the staging step reseeds) |
| Staging | `RunStart.Stage` with `ScenarioStage` | item manager, arena zone, scenario, `StartingHullName`, `TutorialGenerationSettings`, failure list | `RunStart.Staged` (player, entities in order) or null | `ScenarioStage.Failures`, `Placed`, `PlayerShip` | `Scenario.Stage` (it queues; it cannot admit) |
| Admission | `Zone.Admit` | entity, `piloted` | the entity in the zone | stance IFF set both ways with the player; a pilot per piloted entity | everything but `RunStart.Stage` for a new run |
| Tutorial or not | `Galaxy.IsPrelude` only | set by the prelude constructor, or `SavedGame.IsTutorial` on load | read by `RunSave.Capture`, `spawnturret`, `PopulateLevel`, `LoadoutGenerator`, `ZoneGenerator` | `SavedGame.IsTutorial` is the persisted copy, written from `galaxy.IsPrelude` | no static mirror may exist |
| Saved run vs new run | `RunSave.Replace(cache, start)` (`SavedGame.cs:163`) | start func | a staged result, or null | on success the old run's records go; on null or throw the records the start wrote go and the saved run stays | `MainMenu` (it no longer clears the run before generating) |
| Scenario transport | `ActionGameManager.PendingScenario` | `Launch` writes, `StartScenario` reads and nulls | none | also switches `ItemManager` to a fresh `ProvenanceLedger` (`:274`) | command-only; nothing derives from it |

*Differs from R.4:* the fit verbs (`Fit`, `Bare`, `Generated`, `Preset`) return a `ScenarioFit` (`ScenarioStage.cs:102`),
not a `Loadout`; `RunStart.Check` and the cleared-before-check order are gone, replaced by all-or-nothing staging inside
`RunSave.Replace`; `GalaxyStage` takes an injectable clock for tests; `RunStart.Staged` is the return shape.

**Shared paths.** `Zone.Admit` is shared with docking and undocking (`UndockingAdmitsWithNoAgent`); a stance survives
docking but not destruction. `ZoneGenerator.GenerateZone` is shared by new runs and by zone loading as the player
travels. `PlayerSettings` is read by `GalaxyStage.Prelude` (narrative) and written elsewhere; nothing here writes it.
`SavedGame` is shared with Continue: the new-run path must write what Continue reads (`SaveReadsPrelude`).

**Cut line.** A new scenario is one class in `ServerShared/Scenarios/` plus one entry in `Scenarios.Modes` or `.Tests`;
nothing else changes. S3 adds content (test hull, weapon designs, four scenarios, the Arcs beam target) and the
transient `AetherDb smoke-designs apply`; it adds no verb unless the Weapon feel arena needs one.

**Verification layer.** `tests/Aetheria.Shared.Tests/ScenarioTests.cs` (headless, no Unity): `EveryScenarioStages`
(rot test over `Modes` and `Tests`), `ModesMatchOldNewGame`, `SeedRules` (a fixed seed fixes the layout and every fit,
including after the item random is scrambled between arena and staging), `EveryZoneIsLinked` (Delaunay, by seed and
mode), `AGalaxyLeavesTheCatalogsFactionsAsAuthored`, `StagingIsAllOrNothing`, `StanceAndPilot`,
`AStanceSurvivesDockingButNotDestruction`, `UndockingAdmitsWithNoAgent`,
`AQuietArenaHasNoGeneratedShipOrTurretButKeepsItsStations`, `AScenarioPlayerIsItsFitAtItsPlaceWithItsCargo`,
`ATurretStagesAsAStationaryOrbitalEntity`, `AMisplacedHullOrPlayerIsAStagingFailure`, `ArcsGeometry`,
`EveryUnsoldDesignIsAScenarioTestDesign`, `SaveReadsPrelude`, `OldPlayerSettingsLoad`. `RunSaveTests` pins that a failed
start's own writes are removed and the saved run stays. Not covered headless: `MainMenu.Launch` and `ShowScenarios`,
`StartScenario`'s Unity half (`BindToEntity`, `QueueZoneReveal`), and the refuse dialog; those need the in-editor pass
(`docs/merge-to-master-checklist.md` section 8). No build or test ran for this map.

**Stale docs and state found, not edited here.**
- `docs/merge-to-master-checklist.md:36`, step 4 "New Game (tutorial)": describes one New Game button; New Game now opens
  the scenarios submenu and the tutorial is `Tutorial Galaxy` in it.
- `docs/cultcache-migration-cut.md` (`:98,141,654,2599,2663`, and the `IsTutorial` signatures at `:2440,2607,2614,2654`)
  and `docs/cultcache-migration-postmortem.md:360`: New Game calls `RunSave.Clear` first. The run is now replaced only
  after staging (`RunSave.Replace`), which closes the "clears before generation succeeds" scar.
- `docs/headless-playground-cut.md` (`:110,112,246,308,377,398,415,449,476`): the `IsTutorial` static, `MainMenu.cs`
  line anchors, `NewRun = (sector, tutorial)` and `Run.New(..., bool isTutorial, ...)` describe the deleted path. The
  caller-built galaxy it wants is now `RunStart.Generate`. Its acceptance grep for `IsTutorial` assignments is vacuous.
- `docs/item-provenance-cut.md:255`, `docs/item-provenance-substrate.md:135`: "New Game has already cleared the run
  store (`MainMenu.cs:111-112`)" is false now; `PendingScenario` swaps in a fresh ledger (`ActionGameManager.cs:274`).
- `docs/item-provenance-substrate.md:72`, `docs/cultcache-migration-cut.md:1775`: cite `NIH\MIConvexHull`, deleted.
- `docs/tutorial-script-sketch.md:19`: names `IsTutorial`; it is `Galaxy.IsPrelude`.
- `docs/scenarios-cut.md` itself: sections 0.2, 0b, 2.x, 3 (Cuts 2-4), 4, 5.1, 6 and the Anchors paragraph describe
  `aetheria.scenario`, `RunStart.Check` and `AetherDb scenario-seed`; history, marked superseded by R.
- Not stale: `RestoredHullsTests` passing `isTutorial: true` to `ZoneGenerator.GenerateZone`, because that parameter is
  the generator's own, fed by `galaxy.IsPrelude`. Name residue only: `ZoneGenerator`'s `isTutorial` parameter and
  `SavedGame.IsTutorial` (kept deliberately as the persisted field, R.4). Neither is a second owner.

---

## Rulings (operator, 2026-09-30)

- **Q1 A.** The arena is the entrance zone of a small prelude galaxy. Operator: "A is fine, I guess, since some of
  the stuff we'll be testing won't work in an isolated zone with no galaxy."
- **Q2 B.** The agent records results with `AetherDb record`; the menu shows them read-only. **Superseded 2026-10-03
  by `verification-ledger-b` (section R.3): deferred.**
- **Q3 A.** Test designs are committed catalog designs with no product.
- **Q4: none of A-C.** "Debug console commands are hella useful in general and fun for sandbox gamers, never toss a
  single one." Cut 0 is cancelled: `give` (with its hull route), `spawnturret` and every other console command stay.
  The neutral wanderers are not a console command and no ruling retires them, so they stay too. Scenarios coexist
  with the console; they are not its replacement.
- **Smoke weapons.** The operator will not restore their tree's uncommitted `GameData/Aetheria.cc` themselves:
  "No I won't, but you may." When Cut 4 lands, Self runs `git checkout -- GameData/Aetheria.cc` in
  `F:\Projects\Aetheria`. That one file is the only exception to the rule that agents do not touch the
  operator's tree.
- **The ten real designs with no product (operator, 2026-09-30).** Soul's census of Cut 1 found that "no product"
  was not a clean marker for Q3 test designs, because ten shipped designs have none. The operator ruled:
  - **Industrial gear:** Assembly Line, Deep Ore Extractor, Surface Ore Extractor, Industrial Thermostatic Heater,
    Refinery, Shipyard. "An old carryover from this Aetheria repo's early history as an RTS game. We'll want those
    when we eventually bring production back, but it's also fine to just delete them since the new economy won't
    look anything like the old one." **Self: delete them.** Git history keeps them.
    **Correction, same day:** the **Industrial Thermostatic Heater stays**. Operator: "that industrial heater is sort
    of needed, though, because currently stations are mostly idle so there's nothing to keep them above freezing
    and they often cool to near invisibility." Only the other five are deleted. The census test allows product-less
    gear that stations actually equip, and that set is derived from station generation, not from a hand list.
    **Superseded, same day.** Stations have never equipped the heater. Operator: "Stations freezing is an existing
    defect, I don't think I ever made those heaters spawn." Station-heater ruling A: "Yep, A. The thermostatic
    behavior is the same one used in ship cockpits to keep the player from freezing, and the industrial version
    should fit anywhere as a generic equippable tool."
    - Every generated station spawns with one heater, through station generation.
    - The heater shares the cockpit thermostat's owner.
    - It is equippable on any hull.
    - Self default: as generic gear it gets a product, so the census test needs no station exception.
    **Station power and the Tractor Beam (operator, 2026-09-30):**
    - "Yep, we need a station reactor." Author a reactor that fits Zenith's 16-cell reactor slot, with a product,
      so the station heater has power.
    - "Nope, ships don't need these, 'fits anywhere' was a description of tool-type gear, which doesn't require a
      specific hardpoint." The heater keeps its 12-cell shape. The ship-heater test is dropped.
    - "Screw old saves." No retrofit of heaters onto stations loaded from existing saves.
    - "Tractor Beam is supposed to be for item pickup. A ship without a pickup behavior shouldn't be able to pick
      things up, but there is currently no such limitation AFAIK." Picking up items requires a pickup Behavior,
      which the Tractor Beam carries.
    - Starting loadouts (operator): "Yep, everyone gets tractor beams. Pickup behavior was previously discussed as
      part of the field shield map, as one of the behaviors which we'll have various visual effects for. The tractor
      beam item would use the existing pickup VFX but with the standardized pickup animation." Every generated ship
      and the player's starting ship carry a Tractor Beam. The pickup Behavior and its presentation follow
      `docs/shield-presentation-contract.md` ("Shield and Pickup Presentation Contract").
    - Autocannon (operator): "Leave the autocannon unsold, we'll need to author a bunch more hulls before all the
      gear variety in the game has a home." Self's implementation: generation and stock never offer gear that no
      hull's hardpoint can fit. The rule is derived from hardpoint fit, not a hand list. The Autocannon keeps its
      product and becomes sold automatically once a hull fits it. The census test is unaffected.
    - Station reactors by manufacturer (operator, 2026-09-30): "On station reactors: A is a compensator that breaks
      the setting; a station with no access to Zhestokost gear should not have Zhestokost gear, so B."
      - Batch 2 (`b3e7995c`) authored a Zhestokost Station Reactor scaled from Manhattan. A faction without
        Zhestokost allegiance got no reactor, so its station heater had no power.
      - The fix is content, not a generation exception: station reactors are authored for other manufacturers, so
        that every faction's stations can power their heater from gear the faction can actually get.
      - The heater must be reachable the same way.
    - **Soul on batch 2 (`89ef80d2..adad301b`, 2026-09-30): do not merge.**
      - F1 (high): the station reactor burns out after about 20 idle minutes. By minute 40 all 128 cells are
        below freezing, and at 120 minutes the hull reads 155 K. Minutes 0-20 overshoot to 447 K, which makes the
        station a sensor beacon.
      - F2 (high): the idle test samples minutes 5-10 and does not pay for the heater. The inert-thermostat mutant
        S17 survives.
      - F3 (medium, operator question): `Entity.ItemFits` (the item fits inside the hardpoint; used for equip, save
        load and presets) and `HardpointData.Takes` (the item fills the hardpoint; used by generation and
        `HasHome`) are two fit rules. Under `Takes`, about 15 designs players can mount are kept out of station
        stock.
      - F4 (high): LRMM72 and SRMM72 have no `DamageCurve` but now have products, and 22 of 300 NPCs carry them.
        `SampleDps` throws an NRE at `InstantWeapon.cs:64`. Autocannon, pswarm and plight are also missing curves.
      - **F3 RULED (operator, 2026-09-30): "Hardpoint fit is loose: nobody's gonna stop you from putting a small
        reactor in a large reactor's hardpoint."** One fit rule: an item fits if its shape fits within the
        hardpoint. Generation, `HasHome`, stock, equip, save load, presets and `AetherDb hardpoint-fit` all use
        it. So the Autocannon now has a home (the Turret) and is sold. The Autocannon ruling's own rule, "never
        stock gear no hull can fit", is unchanged.
      - **Batch 3 rulings (operator, 2026-09-30):**
        - **Soul on batch 4 (`2125f2b1..aa3baf12`, 2026-10-01): do not merge yet.**
          - **Blocker: origin outside the hardpoint.** The item origin can land outside its hardpoint (`Entity.cs:825`,
            `:879-885`). Every other hardpoint lookup reads the cell under the origin, `Hardpoints[item.Position]`:
            ArcFor, barrels, thrusters, `ActionGameManager`. For an L-shaped hardpoint that lookup returns nothing, so
            Unity throws on the first shot. It is latent: every catalog hardpoint is rectangular today.
            Two ways to fix it:
            - keep the origin inside the hardpoint;
            - look the hardpoint up by the item's cells.

            Probe: `scratchpad/soul5/SoulScen5Probes.cs` P30.
          - **Minor:**
            - `IsFilledBy` is its own oracle in the test.
            - Last-row search and thermostat-order are unpinned.
            - A gun can't be dragged within its own hardpoint (`InventoryPanel.cs:389`).
            - The new station-reactor products drop their source products' quality spread.
          - **Thermal:** the Vulcan Station Reactor burns out within 10 idle minutes. That goes to the thermal
            campaign. Core Power ship reactors also shut down from heat in fights.
          - **Queued, not dispatched:** the operator ordered a drain on 2026-10-01.
        - **F1 re-ruled after batch 4 (operator, 2026-09-30): "A, separate thermal balance cut".** Radiators did not fix
          it. Heat cannot cross the hull to Zenith's edge radiators: the reactor runs at 360-436 K while the radiator
          cells sit at their 278 K floor. Every station reactor wears out between minute 20 and minute 100. Ships
          show the same imbalance under load: MoveOnPro runs at about 480 K and is thermally shut down for most of a
          fight (S8). Thermal balance becomes its own campaign, measured by a steady-state harness across all hulls
          at idle and under load. Its levers are reactor idle heat, hull conductivity, reactor tolerance and radiator
          placement. Scenarios Cut 1 merges with the station freeze recorded as a known defect, which predates this
          work. The 120-minute idle test is held for that campaign.
        - Idle station reactor overheating and burnout (F1): **"Stations should have radiators"** (option C). The
          station has a heat source and no sink.
        - Per-manufacturer station reactors: **"Keep the variety, not every galaxy will have Zhestokost in it"**.
          The census showed every main-sector galaxy holds all 12 factions today. The reactors are authored anyway.
        - Rossum & Douglas missing from its own allegiance: the operator asked "Does allegiance to oneself even make
          sense?". Self's proposal: a faction always reaches its own manufacturer's gear, and allegiance lists only
          other factions. **Ruled: "removing the 11 self-entries was my intention".** Own-faction reach is implicit
          in the one reach function.
      - **Soul on batch 3 (`adad301b..2125f2b1`, 2026-09-30): do not merge yet.**
        - S1: the new belt-failure test is flaky and failed 5 of 8 runs.
        - S2: a second occupancy rule. Entity.cs:820-823 requires every hardpoint cell to be free, while tool gear may
          fill a hardpoint's spare cells. So a generated Turret refuses its own gun back (40 of 955). Operator question.
        - **S2 RULED B (operator, 2026-09-30): "B is the design intent, saving slots in a hardpoint can be a valid
          tradeoff for crowded ships if you really need to fit an extra tool".** A hardpoint item needs only its own
          cells free. A hardpoint's leftover cells may hold general (tool) gear. A hardpoint still holds at most one
          hardpoint item; option C, two hardpoint items sharing one hardpoint, was not chosen.
        - S3: placement searches fewer offsets than `Takes`, so an L-shaped hardpoint crashes generation. Latent.
        - S4: asteroid respawn timers never count down, so mined asteroids never return. **Already fixed by mining:**
          `codex/mining` deleted the belt task threading (Cut 1, `22a54ccb`), `MineAsteroid` and `RespawnTimers` (Cut 2).
          Respawn is `Zone.ChunkWear.BrokenUntil`. **Merge rule:** when scenarios and mining meet, mining's side wins
          for belt and chunk code. **Corrected 2026-10-01 by mining Cut 3 Soul (rehearsal `62b7cefb`/`393334c4`, 557/557):**
          scenarios' `CreateOrbit`/`AddOrbit` changes are only `SettleBelts()` calls, so drop `SettleBelts` and all
          three calls (lines 169/190/324 on scenarios); they merge cleanly but reference the deleted `BeltUpdates`. Delete
          scenarios tests `RunStartTests.cs:639` and `:669` (deleted belt API). Conflicts: `CatalogTypes` take both;
          `Zone.cs` delete `MineAsteroid`; `FireControlCut124Tests.cs:2609` take scenarios' strict Exact;
          `GameData/Aetheria.cc` take scenarios' `70a7b0a9` then rerun `AetherDb field-kinds apply`.
        - S5: a shield charges while unbilled. This is documented design.
        - S6: pins are missing for the filling preference, product reuse and the thermostat band.
        - Held: F2 (no unbilled running in the catalog, players only warmer), F4 (no nulls; no exceptions in 24k-tick
          ambient runs or crowded fights), the census, rotation, and no other fit checker.
      - F5: `_time` is advanced before the belt wait. F6: mining races the belt tasks. F7: reuse by cell count is
        unpinned.
      - Soul did **not** reproduce the Zhestokost gap: every station in 5 seeds and 12 factions got a reactor. The
        premise behind the per-manufacturer reactor ruling is being checked (census) before any authoring.
    - Tractor/pickup (`adad301b`, Hands stopped at the fork): the ruled design is `headless-playground-cut.md` fork
      L, which needs loot as simulation bodies. That substrate does not exist, so it becomes its own campaign, typed
      in the new session (`F:\Projects\HANDOFF-eureka-typed-2026-09-30.md`). The census rule "price 0 is unsold"
      waits for it.
  - **Weapons:** Autocannon, LRMM72, SRMM72, Tractor Beam. "yep that's a content gap". They get products.
  - After both, "no product" means exactly "Q3 test design". A census test pins that: every product-less design in
    the shipped catalog is a scenario test design.

---

## 0. Target

### 0.1 The request, in the operator's words (2026-09-30)

> "I feel like this would be much more manageable as a list of things to verify over time rather than a
> monolithic smoke script that doesn't respect how the game works. Move gear onto Djinni? That's a lot of items
> to spawn, and am I expected to have the catalog memorized to spawn them? If you want me to test a bunch of
> behaviors under specific conditions, find a way to spawn me in an arena where those conditions are present. If
> you want me to kill a guy and loot him, spawn me next to a ship with no armor or weapons, that sort of thing. I
> wouldn't mind a scenario affordance in the main menu for this sort of job, that way the test fixtures can be
> authored, retained and available. But that sounds like a Eureka cut."

What those words settle, so no question below re-asks them:
- **A scenario is an arena whose conditions are present at spawn.** The player's ship and every other entity are
  already fitted and placed. The operator never spawns gear by catalog name and never fits a ship by hand.
- **The fixtures are authored and retained,** and the main menu lists them.
- **Verification is a list of behaviours checked over time,** not a play order.

### 0.2 Ends

- The main menu lists scenarios. Picking one starts a run in its arena.
- A scenario is typed catalog data. It is authored once, kept in git, and replayed at any later revision.
- Every operator check is a typed **check** record: what to do, what to observe, where the rule came from, and
  the scenario that sets it up. Results accumulate as typed records carrying the revision they were observed at.
- Checks A-F and checklist steps 7-20 are the first consumers (section 5).

### 0.3 Invariants

- **One owner decides what a new run starts with.** A plain New Game and a scenario launch share the menu path,
  the galaxy build, `StartGame` and the staging owner. Scenario launch is New Game with a scenario as input; there
  is no second boot path. A plain New Game is the absent-scenario case.
- **One primitive admits an entity into a zone.** Zone construction, staging, warp arrival and undock all admit
  through it, and agents are created there and nowhere else.
- **A scenario sets conditions and nothing else.** Once staged, its entities are ordinary run state. The rules
  under test run through the same code whether or not a scenario started the run. No scenario-only branch exists
  in simulation code.
- **Fixtures are typed state.** Scenario, loadout presets and test designs are CultCache catalog records. Checks and
  results are CultCache records. No load-bearing JSON, text scripts or prose checklists.
- **A scenario that cannot stage is refused before anything is cleared,** with its failures listed.
- **The catalog stays read-only at runtime** (`AetheriaStores.cs:13-21`).

### 0.4 Not in scope

- The full run lifecycle owner (`Run`) of `docs/headless-playground-cut.md` Cut 1 (warp, dock, save, death as one
  ServerShared owner) and its `AetherDb play` interpreter (Cut 3). This map takes over only the new-run staging
  half. The playground map is stale (anchors `1e647953`, fork S superseded by settings-globals) and must be
  re-anchored before Hands. When it lands, its `new` command stages a `Scenario` record instead of reading a
  `.play` text line.
- Brokkr. It is not needed for any cut here: agents author scenarios headless through `AetherDb`, and the operator
  launches them from the menu. Brokkr's Unity actions (`setEditorPlayState`, `captureEditorView`, scene edits;
  `F:\Projects\Brokkr` `1357705`, read) have no way to pick a scenario in the menu. Agent-driven Unity play would
  need an Aetheria install cut plus a "launch scenario" intent. Recorded as a follow-up, not a fork.
- The ship-purchase docking-bay gap (checklist section 8). It is a design gap in `TradeMenu.Buy` and
  `CommissionShip`, not a verification affordance. `CommissionShip` stays; `TradeMenu.Buy` is its consumer.
- Tier colours (settings-globals Cut 1). The check is recorded as a known failure (section 5).
- Balance and feel tuning. A check can record a feel call; it does not tune.

---

## 1. Body findings

### 1.1 The new-run path today (read)

| Concern | Anchor | Notes |
|---|---|---|
| Menu | `MainMenu.cs:92-184` | Continue `:103-108`; New Game `:110-176`; Settings; Quit. The same component runs in-game (`InGame`, `:96`), so the menu reappears on death (`ActionGameManager.cs:1171`). |
| Clear the run | `MainMenu.cs:113` | `RunSave.Clear` at click time, before the galaxy is built. |
| Galaxy | `MainMenu.cs:124-175` | `TutorialPassed` is never set anywhere, so New Game always takes the prelude branch `:146-175`. The background noise position comes from `UnityEngine.Random` (`:128,153`). The seed defaults to the clock (`Galaxy.cs:106,158`). |
| Handoff | statics `ActionGameManager.CurrentGalaxy`, `IsTutorial` (`:96-97`) | read by `StartGame`, `SectorMap`, and nulled by `Die` (`:1173`). |
| Start | `StartGame` `:796-834`, called from `Start` `:491` | New run `:803-815`: `PopulateLevel(Entrance)`, `LoadoutGenerator.GenerateShipLoadout` filtered by `GameSettings.StartingHullName` (`GameSettings.cs:13`, authored `LonginusX`), unpack, `Zone.Entities.Add`, `Activate`, `BindToEntity`. Continue `:817-833`. |
| Zone entry | `PopulateLevel` `:710-744` | generates the pack lazily (`:714-722`), moves the pilot (`:731-737`). |

### 1.2 What a scenario can reuse (read, one probe)

- **Loadout presets already exist.** `aetheria.loadout` (`Loadout.cs:13-20`) holds a hull design, per-cell slot
  designs with rotation, and weapon groups. It is a catalog type (`AetheriaStores.cs:9`), authored in Studio or by
  the editor console command `capturepreset` (`ActionGameManager.cs:624-662`, `Loadouts.Commit` `Loadout.cs:70-87`).
  `Loadouts.Materialize` (`:96-159`) builds a live `Ship` all-or-nothing, listing every failure.
  - **No game path calls `Materialize`** (`LoadoutGenerator.cs:152-153` says so; only `LoadoutTests.cs:310,899`
    call it).
  - **(probe)** The live catalog holds no preset: the schema name `aetheria.loadout` does not occur in
    `GameData/Aetheria.cc` (12,698,108 bytes at `dcd7bbc5`), while every populated type's name does (for example
    `aetheria.hulldata`, `aetheria.weaponitemdata`). Method: `grep -a -o` over the file.
  - `Materialize` always builds a `Ship` (`:131`). A turret hull cannot be materialized.
  - It resolves every design through a product (`Resolve`, `:103-115`). A design with no product fails.
- **Unbranded lots exist.** `ItemManager.CreateLot(design, maker, quality)` (`ItemManager.cs:161`) with no maker is
  how `give` made its items (`ActionGameManager.cs:538,542`). Provenance's "no Manufacturer row" check depends on
  them.
- **Economy exclusion follows from products.** Generation iterates products and requires
  `product.Manufacturer.IsSet()` and `design.Price > 0` (`LoadoutGenerator.cs:126-133`). A design with no product
  is never generated, stocked or sold. Loot is what a killed entity carries (`EntityInstance.cs:302-321`), so it
  cannot leak through loot either unless a scenario fits it.
- **Prelude galaxies offer every product.** `IsAvailable` returns true when `Galaxy.IsPrelude`
  (`LoadoutGenerator.cs:154-156`). A preset materialized in a prelude arena never fails on faction availability.
- **Agents are made only at zone construction.** `Zone`'s constructor gives every packed non-player `Ship` a
  `Minion` (`Zone.cs:120-131`, `CreateAgent` `:135-142`). An entity added later gets none. A `Minion` targets the
  first visible enemy and fights (`Minion.cs`).
- **An entity joins a zone in four other places,** each by hand: `spawnturret` (`ActionGameManager.cs:587`),
  `PopulateLevel` (`:735`), `StartGame` (`:813`) and `Entity.TryUndock` (`Entity.cs:1006`).
- **IFF overrides are entity-local and unsaved.** `Entity.SetIff(other, bool?)` (`Entity.cs:529-533`) decides
  `IsHostileTo` ahead of faction rules (`:535-552`). A `Materialize`d ship has no faction, so it is neutral unless
  overridden.
- **A target that dies and a target that leaves are the same case in fire control.**
  `targetGone = shot.Target != null && !zone.Entities.Contains(shot.Target)` (`FireControl.cs:610`), and death
  removes the entity from `Zone.Entities` (`Zone.cs:93`). Check A.3 ("the target warps away") is therefore set up by
  killing a fragile target while a fused round is in flight. No departure machinery is needed.
- **An orbital entity with no orbit stays where it is put** (`OrbitalEntity.cs` `Update`: position follows the
  orbit only when one is set). That is how `spawnturret` placed turrets (`ActionGameManager.cs:583-588`).
- **Headless tests already open the live catalog and the authored settings**
  (`RestoredHullsTests.cs`: `AuthoredSettings.Load`, `OpenReadOnlyRealCatalog`). A staging test can generate a
  prelude galaxy, build the arena zone and stage a scenario with no Unity.

### 1.3 Debug affordances a scenario replaces (read)

| Affordance | Anchor | Added for |
|---|---|---|
| `give <name>` | `ActionGameManager.cs:515-544` | ad hoc items; the hull route through `CommissionShip` was added tonight in `f935c29b` for step 7 |
| `spawnturret` | `:564-589` | a hostile turret next to the player |
| Neutral wanderers | `ZoneGenerator.cs:342-356`, `EligibleWandererFactions` `:361-365`, `ZoneGenerationSettings.NeutralWandererCount` (`Settings.cs:156`), tests `IffAndCombatTests.cs:329-350` | commit `f732cff9` (2026-09-17): "Spawn neutral wanderers so combat has something non-hostile to target". The code comment calls it a "Testing affordance". Every generated zone carries two. |

Staying, because no scenario replaces them: `iff` (flip a live target's stance mid-play), `trackmissile` (camera),
`revealzones` (map), `tow` (a verb bound to the console), `capturepreset` (the in-play authoring tool for presets).

### 1.4 Collisions with open branches (read)

- `codex/mining` rewrites `Zone.cs` (+239/-). Cut 1 edits the constructor loop and adds the admission primitive.
  Land Cut 1 after mining merges, or re-anchor.
- `codex/moddable-ships` edits `AetheriaStores.cs` (line 9, the catalog type list). Cuts 1 and 3 edit the same
  lines.
- `codex/item-provenance` carries the narrative map, which edits `MainMenu.cs:132,160` and the `Galaxy`
  constructors. Cut 2 edits the same New Game block.

---

## 0b. Identity, lifecycle, authority

No cut is mapped against an empty cell.

| Kind | What names it | Lifecycle | Who decides |
|---|---|---|---|
| **Scenario** (`aetheria.scenario`, catalog) | `CultName` `Name`; key `scenario:<Name>`, derived like `Loadouts.KeyOf` | Authored, then edited in place. A rename is a new record; the old one is deleted, and the checks that pointed at it are repointed in the same commit. A result never points at a scenario (it points at a check), so history survives. A scenario whose presets stop materializing is caught by the staging test (Cut 4), not at play time. | Authored by agents through `AetherDb` or by the operator in Studio. The operator owns what an arena contains. |
| **Loadout preset** (`aetheria.loadout`, existing) | key `loadout:<Name>` (`Loadout.cs:62`) | Unchanged: authored, replaced only explicitly (`Commit(..., replace)`). | Unchanged. |
| **Test design** (catalog `ItemData`, no product) | name; key minted by `Upsert` | Authored with the scenario that needs it. Lives as long as a preset references it. Per Q3. | Operator (Q3) on whether they exist at all. |
| **Check** (`aetheria.check`, ledger store) | `CultName` `Slug`; key `check:<Slug>` | Authored. Its statement is edited in place when a ruling changes (git keeps the history). It is retired, never deleted, once any result references it: `Retired` plus a reason. | The rule's owner (a ruling or a map) decides what it says. Agents transcribe it with a source pointer. |
| **Check result** (`aetheria.checkresult`, ledger store) | key `checkresult:<Slug>:<At as UTC ticks>` | Append-only. Never edited or deleted. A correction is a new result whose note says what it corrects. The latest by `At` is the current status. Order is carried in the data (`At`), not in delivery. | The operator's observation. Who writes the record is Q2. |
| **Pending scenario** (runtime) | `ActionGameManager.PendingScenario`, a static | Written once by the menu's launch, read and nulled once by `StartGame`'s new-run branch. Null for Continue. Never saved. | Command-only transport. It decides nothing. |
| **Staged entities** (runtime, then run state) | ordinary entities | Born at staging. From then on they are run state and saved with the zone like any other entity. Two things are not saved: whether a ship was left unpiloted, and scenario IFF overrides. After Continue, an unpiloted scenario ship gets a `Minion` by the constructor rule and its stance reverts to faction rules (neutral, since presets carry no faction). A scenario run is meant to be played through in one sitting. | `RunStart` at birth; the zone afterwards. |

---

## 2. Target shape

### 2.1 Types (names and rules; bodies are Hands')

`Assets/Scripts/ServerShared/Scenario.cs`, catalog:

- `Scenario` (`aetheria.scenario` v1):
  - `Name` (`CultName`).
  - `Brief`: the conditions in one or two lines, shown in the menu.
  - `Seed` (`uint`): the arena galaxy and the background noise both derive from it, so the arena is the same layout
    every launch. Combat rolls are already zone-seeded (`Zone.cs:84`). Lot quality still comes from the
    clock-seeded `ItemManager.Random` (`ItemManager.cs:20`), so stats vary slightly per launch. Accepted.
  - `Ambient` (`bool`): true keeps the arena zone as generated. False keeps planets, orbits and stations but drops
    generated ships and turrets, so nothing wanders into the conditions.
  - `Player` (`ScenarioShip`).
  - `Entities` (`List<ScenarioEntity>`).
- `ScenarioShip`: `Loadout` (`CultRecordRef<Loadout>`), `Position` (`float2`, zone xz), `Direction` (`float2`),
  `Cargo` (`List<CultRecordRef<EquippableItemData>>`, stored in the first cargo bay).
- `ScenarioEntity : ScenarioShip`: `Stance` (`Neutral | Hostile`, applied both ways through `SetIff`) and `Piloted`
  (`bool`: a `Minion`, or no agent at all).
- A preset's hull type picks the entity class: a ship hull gives a `Ship`; a turret hull gives an orbital entity
  with no orbit, stationary at its position. A station hull is refused as a staging failure.

`Assets/Scripts/ServerShared/Checks.cs`, ledger store (`GameData/Checks.cc`, committed, LFS by the existing
`GameData/*.cc` rule):

- `Check` (`aetheria.check` v1): `Slug` (`CultName`), `Statement` (what to do and what must be observed),
  `Source` (the ruling or map pointer, `doc:line`), `Scenario` (`CultRecordRef<Scenario>`, optional), `Setup`
  (text, only when there is no scenario: "FieldShieldTest scene", "CultCache Studio", "Addressables play mode Use
  Existing Build"), `Retired` and `RetiredReason`.
- `CheckResult` (`aetheria.checkresult` v1): `Check` (ref), `Result` (`Pass | Fail | Partial | Blocked`),
  `Revision` (the commit the operator played, plus `+dirty` when the tree had changes), `At` (UTC), `Note`,
  `Recorder`.

Naming: "check", not "behaviour". `Behavior` is already the gear-behaviour type family
(`Assets/Scripts/ServerShared/Behaviors/`), and a document named `Behaviour` beside it reads as a typo.

Where they live, and why:
- Scenarios, presets and test designs are design-side fixtures. They go in the catalog: presets are already
  catalog data, cross-type refs resolve inside one store, and Studio edits them side by side.
- Checks and results are the verification ledger. They go in their own store. The shipped catalog carries no
  history, results never trigger a catalog write, and "one commit lands in one store" holds: recording a result
  touches the ledger only.

### 2.2 The staging owner

**Owner: `RunStart`, a static class in `Assets/Scripts/ServerShared/RunStart.cs`.** It decides what a new run
starts with: how the arena zone is generated, the player ship, the other entities, their stance, pilot and cargo.
It holds no Unity type.

- **Inputs:** `ItemManager` (and through it the catalog), the `Galaxy`, `ZoneGenerationSettings`, the optional
  `Scenario`, and, for the absent-scenario case, the default hull filter and faction `StartGame` uses today
  (`:805-809`).
- **Outputs:**
  - the arena's `ZonePack`, with the `Ambient` rule applied inside generation (`ZoneGenerator.GenerateZone`
    gains the parameter), not generated and then pruned;
  - the admitted player `Ship`, flagged `IsPlayerShip`;
  - the admitted scenario entities;
  - a failure list. Staging is all-or-nothing, as `Materialize` is.
- **A check entry** that validates a scenario without a zone (every preset materializes, every cargo design
  exists). The menu calls it before `RunSave.Clear`; the staging tests call it on every catalog scenario.
- **Derived state:** `ActionGameManager.CurrentEntity` is bound to the returned player. `PendingScenario` is
  transport only.
- **Forbidden writers:** `StartGame` may not generate, unpack or admit the player ship. `MainMenu` may not stage.
  Console commands stay (Q4 ruling), but `give` and `spawnturret` admit through `Zone.Admit`, the same primitive
  as scenario staging. They get no admission path of their own. Cut 1 repoints them.
- **Shared paths:** New Game and scenario launch both run menu, galaxy, `StartGame`, `RunStart`. Continue does not
  stage.
- **Deletion line:** `StartGame` `:805-814` (generator, unpack, add, activate). The only surviving line is the
  bind.

**Admission primitive: `Zone.Admit(Entity entity, bool piloted)`.** It adds the entity to `Entities`, activates it,
and, when `piloted`, gives it the zone's `Minion` (the `CreateAgent` rule, `Zone.cs:135-142`). It is the only writer
of `Entities.Add` and the only creator of agents.
- The constructor loop (`:120-131`) calls it with `piloted: ship && !IsPlayerShip`. It keeps its own rule that
  moves a ship packed at the origin (`:129-130`); that is generation placement, not admission.
- `PopulateLevel` (`ActionGameManager.cs:735`), `Entity.TryUndock` (`Entity.cs:1006-1007`) and `RunStart` call it.
  `spawnturret` (`:587`) is deleted.
- The `Death` subscription (`Zone.cs:93`) stays on `ObserveAdd` and still covers every join.

**`Loadouts.Materialize` gains two rules** (`Loadout.cs:96-159`):
- A design with **no product at all** is built unbranded, through `CreateLot(design, default, quality)` at one
  named quality constant (0.95, the quality `give` used). A design whose products are all unavailable still fails,
  as today. The rule turns "no manufacturer makes it" into "outside the economy", which generation already obeys.
- The **hull type picks the entity class** (section 2.1).
- The comment at `LoadoutGenerator.cs:150-153` ("No game path materializes a preset yet") is rewritten to name
  `RunStart`.

### 2.3 The Unity lowering

- `MainMenu.ShowMain` (`:92-184`) gains **Scenarios**, shown when `Debug.isDebugBuild` (editor and development
  builds). It opens a panel with one button per catalog scenario, ordered by name. Under each button is its brief
  and its checks, each with its latest result and revision, read from the ledger (display only). A scenario whose
  check entry fails shows the failures and is disabled.
- **One launch body** is extracted from the New Game lambda (`:110-176`). New Game calls it with no scenario; a
  scenario button calls it with one. With a scenario, it runs the prelude branch (`:146-175`) with `Seed`, derives
  the noise position from `Seed` instead of `UnityEngine.Random`, sets `PendingScenario`, and loads `ARPG`. The
  check entry runs first; `RunSave.Clear` (`:113`) moves after it, so a refused scenario leaves the saved run intact.
- `StartGame`'s new-run branch (`:803-815`) calls `RunStart`, binds the returned player and nulls
  `PendingScenario`. The Continue branch asserts it is null.
- `AetheriaStores.Open` gains an optional ledger path. The game attaches `GameData/Checks.cc` read-only; `AetherDb`
  attaches it writable for the record commands (Cut 3).

### 2.4 Authoring

- **Agents author headless through `AetherDb`.** A seed command (Cut 4) declares the first set in C#. It writes a
  record only when the key is absent. When the key exists and differs from what it would write, it refuses and
  reports; that means Studio edited it, and Studio wins. When the key exists and matches, it does nothing. It
  validates every scenario through the `RunStart` check entry before committing. So the catalog is the authority,
  and the seed code never clobbers it.
- **The operator authors in Studio and in play:** Studio for scenario fields; `capturepreset` for a ship fitted by
  hand in play.
- Results are recorded per Q2.

---

## 3. Cuts

Build budget, every cut:
- `Aetheria.Shared` (netstandard2.1, the Unity-independence check), `tests/Aetheria.Shared.Tests`, `tools/AetherDb`
  (net10.0), Debug.
- CultLib is unchanged; the pins are those in `hands-fuse8/run.sh`.
- No new project, target, package or daemon. One new store file (`GameData/Checks.cc`), three new schemas.
- **Build host:** Yggdrasil builds the headless projects (`run.sh <rev>`). `Assembly-CSharp` is proven only by a
  Unity 6000.3.24f1 batchmode compile on Starfire, with the editor closed by the operator. Never kill Unity.
- `run.sh` mounts a pinned `data/Aetheria.cc` over the tree's LFS file. Any cut that changes the catalog (Cut 4)
  must refresh that pin, or its content tests read the old catalog. Cut 4's tests that read the ledger mount it the
  same way. Yggdrasil's disk is saturated: one job per cut, `--artifacts-path` in the container.

Order: 1, 2, 3, 4. Cut 0 is cancelled (Q4 ruling). Cuts 1 and 2 branch from `master` directly. Cuts 1 and 3 are headless. Cut 2
is the only Unity-side cut. Cut 4 is content.

### Cut 0. Retire the ad hoc test affordances (Q4). CANCELLED

**Cancelled 2026-09-30 by the Q4 ruling ("never toss a single one").** Nothing below runs. It is kept as history.
Cut 1 branches from `master`.

- **Repo/branch:** Aetheria, `codex/scenarios` from `master` `dcd7bbc5`.
- **Deletes first** (per Q4 A; B keeps the item branch of `give` and the wanderers):
  - `ActionGameManager.cs:515-544`, `give` (30 lines).
  - `ActionGameManager.cs:564-589`, `spawnturret` (26 lines).
  - `ZoneGenerator.cs:342-356`, neutral wanderers (15 lines), and `EligibleWandererFactions` `:361-365` (5).
  - `Settings.cs:156`, `NeutralWandererCount` (1). It is absent from `Settings.asset` (settings-globals `:72`), so
    no asset edit is needed.
  - `IffAndCombatTests.cs:329-350`, the two wanderer tests (22).
- **Keeps:** `CommissionShip` (`:898-903`) and its consumer `TradeMenu.Buy`; the console fixes in `f935c29b`.
- **Docs:** `docs/merge-to-master-checklist.md` section 8, the `give` bullet: add "retired by the scenarios cut"
  with the commit. `docs/settings-globals-cut.md:72,348` get a one-line history note.
- **Verification:**
  - builds: `Aetheria.Shared`, the test project. Batchmode compile (`ActionGameManager.cs` changed).
  - tests: the full suite is green. The locomotion "real enemy-count check" (`95394f78`) counts generated ships;
    if it counted wanderers, fix its expectation and say so.
  - negative: `rg -n '"give"|"spawnturret"' Assets/Scripts` is empty;
    `rg -n "NeutralWanderer|EligibleWandererFactions" Assets tests tools` is empty. Both patterns were checked to
    match only these sites today.
  - operator: none. There is nothing to see until Cut 2.
- **Ledger:** about -99 lines; 0 added.

### Cut 1. `RunStart`, `Zone.Admit`, and the scenario type (headless)

- **Repo/branch:** Aetheria `codex/scenarios`, from `master` (Cut 0 cancelled). Re-anchor `Zone.cs` if mining has merged.
- **First:** with `AetherDb hardpoint-fit` and a scratch `Materialize` against the live catalog, confirm that
  `Djinni`, `LonginusX` and one turret hull each have a product and materialize bare. If one has no product, stop
  and report: the Q3 rule would make it unbranded, which may not be intended for a real hull.
- **Deletes first:**
  - `ActionGameManager.cs:805-814`, the new-run spawn (it moves into `RunStart`; this cut leaves `StartGame`
    calling `RunStart` with no scenario, so behaviour is unchanged).
  - The four hand-written joins: `ActionGameManager.cs:735-736`, `Entity.cs:1006-1007`, `Zone.cs:124-125`, and the
    agent add at `Zone.cs:128`. Each becomes `Admit`.
- **Repoints (Q4 ruling, console commands stay):** `give`'s hull route (`ActionGameManager.cs:515-544`) and
  `spawnturret` (`:564-589`) admit through `Zone.Admit`. If either already reaches one of the four joins above,
  that repoint covers it. A test pins that `give` of a hull and `spawnturret` both land in `Entities` through
  `Admit`, and its mutation (restoring a direct add) must fail it.
- **Adds:** `Scenario.cs`, `RunStart.cs`, `Zone.Admit`, the `ambient` parameter on `ZoneGenerator.GenerateZone`
  (`:38-43`; the ship loop `:334-340` and `PlaceTurrets` calls `:309,331` honour it), `Scenario` in
  `AetheriaStores.CatalogTypes` (`:9`), and the two `Materialize` rules (`Loadout.cs:103-115,131`).
- **Authority map:** section 2.2. Owner `RunStart`; admission `Zone.Admit`; deletion line as above.
- **Verification:**
  - builds: `Aetheria.Shared`, tests, `AetherDb`. Batchmode compile (`ActionGameManager.cs`, `Entity.cs` changed).
  - tests (new `RunStartTests`: live catalog, authored settings, a prelude galaxy at a fixed seed). Each rule
    must fail under its own mutation:
    - With no scenario, staging yields a player of the `StartingHullName` hull, `IsPlayerShip`, admitted, at the
      origin. This pins that New Game is unchanged.
    - With a scenario, the player is the preset's hull and fit at the authored position and direction, and its
      cargo holds the listed designs.
    - A piloted entity has exactly one agent in `Zone.Agents`; an unpiloted one has none and stays put over 5 s of
      `Zone.Update`.
    - `Hostile` makes both `IsHostileTo` directions true. `Neutral` makes both false.
    - `Ambient: false` leaves no ship or turret in the arena pack that the scenario did not place. Stations remain.
    - A turret preset stages as a stationary orbital entity. A station preset is a staging failure.
    - A scenario naming a missing design fails with that design named. The zone is unchanged, and no
      `RunSave` call was made (the check entry touches no store).
    - A product-less design materializes unbranded; a design whose only product is unavailable still fails.
      This is two tests, because the second case is the existing contract.
    - `Admit` is the only agent creator: after a warp (`PopulateLevel`'s join through `Admit(pilot, false)`) the
      pilot has no agent. The constructor's pack path still gives every non-player ship a `Minion`.
  - Stryker on `RunStart.cs`, `Zone.cs` (`Admit` and the constructor loop), `Loadout.cs` (`Materialize`), scoped
    to the diff. Triage survivors by name.
  - negative: `rg -n "Entities\.Add\(" Assets/Scripts --glob '!**/ZoneGenerator.cs'` matches only
    `Zone.cs` (once, inside `Admit`), `Sensor.cs:169` and `Entity.cs:242` (both are `VisibleEntities`/pings, not
    zones). `rg -n "new Minion\(" Assets/Scripts` matches only `Zone.cs`.
  - operator: none beyond the Cut 2 smoke.
- **Ledger:** `Scenario.cs` +60; `RunStart.cs` +110; `Zone` +12 -8; `Loadout.cs` +20; `ZoneGenerator` +4;
  `ActionGameManager` -11 +4; `Entity` ±2; `AetheriaStores` ±1; tests +300.

### Cut 2. The main menu launches scenarios (Unity)

**Superseded 2026-10-03 by section R.** Kept as history.

- **Repo/branch:** Aetheria `codex/scenarios`, on Cut 1.
- **Deletes first:** the duplicated body of the two galaxy branches in the New Game lambda (`MainMenu.cs:124-175`)
  collapses into one launch body. The standard branch (`:124-145`) is live code only when `TutorialPassed` is
  true, and it is kept. Only the duplication goes.
- **Adds:** the Scenarios panel, `PendingScenario`, the seed-derived noise, the check entry before
  `RunSave.Clear`. `StartGame` passes `PendingScenario` to `RunStart`.
- **Authority map:**
  - Owner of what the run starts with: `RunStart` (unchanged).
  - `MainMenu` owns which scenario was picked and when the run is cleared. `PendingScenario` is a command-only
    transport, consumed once.
  - Forbidden: `MainMenu` stages nothing and admits nothing. `StartGame` reads `PendingScenario` only in the
    new-run branch.
  - Shared paths: both buttons run one launch body. In-game menu (after death) and title menu run the same one.
- **Verification:**
  - builds: batchmode compile, editor closed.
  - negative: `rg -n "RunSave\.Clear" Assets/Scripts/UI/MainMenu.cs` appears once, after the check entry.
    `rg -n "PendingScenario\s*=" Assets/Scripts` matches the launch body and `StartGame`'s null only.
  - operator (Unity, Starfire):
    1. Title menu: Scenarios is listed. New Game still starts a LonginusX at the entrance with a Zenith station
       (the Cut 1 test pins the ship; this proves the lowering).
    2. Launch any scenario. The arena matches its brief; launch it again and the layout is the same.
    3. Die in a scenario. From the in-game menu, relaunch the same scenario.
    4. Quit mid-scenario, then Continue. The run continues (the 0b rule: unpiloted ships now patrol).
    5. Break a scenario on purpose in a scratch catalog (a missing design). It is shown disabled with the failure,
       and Continue still offers the previous run.
- **Ledger:** `MainMenu` +45 -25; `ActionGameManager` +4; one static.

### Cut 3. The verification ledger (headless; Q2)

**Superseded 2026-10-03 by section R.** Kept as history.

- **Repo/branch:** Aetheria `codex/scenarios`, on Cut 1 (independent of Cut 2).
- **Adds:** `Checks.cs` (`Check`, `CheckResult`); `AetheriaStores.LedgerTypes` and the optional path;
  `GameData/Checks.cc` (empty, committed).
  - Under Q2 B, `AetherDb` gains two commands:
    - `checks [scenario]`: each check with its latest result, its revision, and "never" when there is none.
    - `record <slug> <pass|fail|partial|blocked> --revision <sha> [--note <text>]`. It refuses an unknown or
      retired slug. It opens the ledger writable and the catalog read-only.
  - Under Q2 A, the menu panel gains Pass/Fail/Partial and a note field per check. It writes through the same
    ledger primitive, and the game attaches the ledger writable.
- **Authority map:**
  - Owner of results: the ledger store. The only writer is the Q2 writer, which calls one append primitive,
    `Ledger.Record`.
  - Derived, display only: the menu's latest-status line and `checks`' output.
  - Forbidden: nothing edits or deletes a `CheckResult`; nothing writes a result into the catalog.
- **Verification:**
  - tests (new `LedgerTests`, temp stores):
    - Recording appends and never replaces: two results for one check both survive a reopen, and the latest is
      the one with the later `At`, even when written first.
    - A retired or unknown slug is refused with nothing written.
    - The catalog file is byte-identical after `record` (a catalog opened read-only cannot be written; this pins
      that the command opened it that way).
    - A result carries the revision it was given, and `record` without `--revision` is a usage error.
  - Stryker on `Checks.cs` and the `AetherDb` record path.
  - negative: `rg -n "CheckResult" Assets/Scripts --glob '!**/Checks.cs'` matches only the menu's read (and under
    Q2 A its record call).
- **Ledger:** `Checks.cs` +50; `AetheriaStores` +4; `AetherDb` +90 (B) or `MainMenu` +50 (A); tests +120.

### Cut 4. The first scenarios, checks and history (content)

**Superseded 2026-10-03 by section R.** Kept as history.

- **Repo/branch:** Aetheria `codex/scenarios`, on Cuts 2 and 3. Hands authors on the branch clone, never in
  `F:\Projects\Aetheria`.
- **First:** Self restores the uncommitted smoke weapons in the operator's tree (`git checkout --
  GameData/Aetheria.cc` in `F:\Projects\Aetheria`, that file only, per the smoke-weapons ruling) before the
  operator pulls. This cut recreates them as
  committed designs.
- **Adds:**
  - A transient `AetherDb scenario-seed [apply]` command, with the never-clobber rule of section 2.4.
    - It writes the test designs (section 5.3), the presets (5.4), the scenarios (5.2) and the checks (5.1).
    - It imports the 2026-09-30 smoke results (checklist section 8) as `CheckResult`s at revision `113164fa`,
      recorder "operator (play), recorded by agent".
    - It runs once, the data is committed, and the command is deleted in the same cut. The catalog is the authority
      from then on.
  - `ScenarioConditionTests` (live catalog; pinned catalog refreshed, see the build budget). Per scenario, stage it
    and assert the conditions its checks need, not the behaviour, which existing suites own. For example:
    - `Fused rounds`: the bare hull lies within the arc on the player's initial aim line and has no target
      selected; the close hull is nearer than `Smoke Proximity`'s `BlastRadius`; `Smoke Refused` has
      `Range < BlastRadius`; a direct-fire weapon can kill the fragile hull.
    - `Duel`: the AI is piloted, hostile both ways, and carries a slow missile weapon; the player can see it.
    - `Arcs`: the three bare hulls lie in the bow, beam and stern sectors of the player's mounts; the turret is
      hostile.
    - Every catalog scenario passes the check entry. This is the rot test.
    - Every non-retired check with a scenario points at an existing one. Every check without a scenario has a
      `Setup`.
  - Docs: `docs/merge-to-master-checklist.md` sections 2 and 8 get one line: steps 7-20 and handoff checks A-F are
    now checks in `GameData/Checks.cc`, listed by `AetherDb checks`. The prose steps stay as history.
- **Verification:**
  - tests: `ScenarioConditionTests`, with mutation by editing a fixture. Moving the close hull outside the blast
    radius, dropping the AI's missile weapon, or flipping a stance must each fail its condition. This is a one-off
    Soul probe, not a committed suite.
  - `AetherDb checks` lists every check in section 5.1, with the imported results on 3, 4, 5, 6 and "never"
    elsewhere.
  - operator: play each scenario and report per check. That report is the first real use of the ledger.
- **Ledger:** seed command +300 then -300; catalog +5 designs, +11 presets, +10 scenarios; ledger +44 checks, +7
  results; tests +250; docs ±4.

---

## 4. Authority map (whole campaign)

- **Owner of a new run's contents:** `RunStart`. It reads the scenario (or its absence) and writes the arena pack
  and the admitted entities.
- **Owner of zone membership and agents:** `Zone.Admit`.
- **Owner of fixtures:** the catalog (`Scenario`, `Loadout`, test designs), written by the seed, Studio or
  `capturepreset`, and read-only at runtime.
- **Owner of verification history:** the ledger store. It is append-only and written by the Q2 writer.
- **Derived:** menu status lines; `AetherDb checks`; `PendingScenario` (transport).
- **Forbidden writers:** `StartGame` spawning ships; `MainMenu` staging; console commands admitting entities;
  anything editing a result; any catalog write at runtime.
- **Shared paths:** New Game and every scenario launch: one launch body, `StartGame`, `RunStart`, `Admit`. Warp,
  undock, construction and staging: `Admit`.
- **Deletion line:** `StartGame` `:805-814`; the four hand-written joins; the duplicated New
  Game branches; the transient seed command.

---

## 5. The first consumers

Sources: `docs/merge-to-master-checklist.md` section 2 (steps 7-20) and section 8; checks A-F from
`F:\Projects\HANDOFF-aetheria-play-smoke-2026-09-29.md` section 3. That file sits outside the repo, so the checks
below restate A-F and the ledger becomes their home. The rulings behind A-F are in `fire-control-cut.md`, in the
block "Ruled (operator, 2026-09-30)" (`:2625-2690`).

### 5.1 Checks

| Slug | Statement (abridged; the record carries the full text) | Source | Setup |
|---|---|---|---|
| `restored-hull-flies` | Thrusters move and turn the Djinni. | step 7; `locomotion-cut.md:501` | Djinni shakedown |
| `restored-hull-speed-cap` | Take the hull offline with thrusters firing; speed does not run away. | step 7; `:377` | Djinni shakedown |
| `weak-reactor-starves-legibly` | A reactor too small for the loadout shows starvation, not a cooked reactor. | step 8; `stats-and-power-cut.md:705-706` | Starved reactor |
| `brownout-degrades` | Brownout reads as degradation, not breakage. | step 9; `:774,990` | Starved reactor |
| `brownout-refill-stutters` | A weapon refilling under brownout stutters rather than firing at full rate. | step 9; `:729-730` | Starved reactor |
| `memory-flat-across-zones` | Several zones with kills; memory does not climb. | step 10; `:672-673,980-982` | Long haul |
| `side-mounts-fire-abeam` | Side-mounted weapons fire at the beam target. | step 11; `fire-control-cut.md:397-398` | Arcs |
| `turret-tracks-round` | The hostile turret tracks the player all the way round. | step 11 | Arcs |
| `no-fire-through-hull` | Nothing fires at the stern target through the hull. | step 11 | Arcs |
| `shots-roll` | Rolled impacts and misses; a miss reads as a near-miss; damage matches the HUD. | step 12; `:659-661` | Duel |
| `subsystem-aim` | Reveal and selection work; aiming at a revealed subsystem concentrates damage. | step 12 | Duel |
| `kill-drops-loot` | The kill drops its cargo; pickup stores it; the tractor beam pulls. | step 12; `headless-playground-cut.md:789-794` | Duel |
| `loot-keeps-brand` | Picked-up loot shows its manufacturer. | step 6; `item-provenance-cut.md:528-539` | Duel |
| `target-vanish-safe` | The target dies with shots in flight: no crash, no frozen ship. | step 12; `fire-control-cut.md:1464-1470` | Duel |
| `beam-reads-higher` | The HUD `hull` factor reads higher from the beam than from the bow. | step 13; `:2380-2383` | Duel |
| `armour-face-missile` | Turn the armoured face into the AI's slow missile; it lands there. | step 13 | Duel |
| `ai-engages` | The AI still engages normally. | D; `:2658-2660` | Duel |
| `launcher-edge-pulse` | Launcher into the bow, then a flank: the schematic pulses the facing edge only. | step 14; `:2519-2520` | Launcher angles |
| `guided-explodes-on-detonation` | A guided round's explosion shows only when the simulation detonated. | E | Launcher angles |
| `guided-target-left-fades` | Kill the target while a guided round flies: the round fades with no explosion. | E | Launcher angles |
| `guided-hit-at-target` | A guided hit plays its effect at the target. | E | Launcher angles |
| `fused-clear-line-max-range` | Nothing selected, clear line: the round bursts at max range along the arc-clamped aim. | A.1; `:2625,2636-2642` | Fused rounds |
| `fused-first-hull-stop` | Nothing selected, aim at the bare hull: the round stops and detonates on it. | A.2; `:2632-2634` | Fused rounds |
| `fused-target-left-no-burst` | Fire at the fragile target, kill it with the direct weapon before arrival: no burst. | A.3; `:2636-2642`, `FireControl.cs:610` | Fused rounds |
| `fused-out-of-arc` | Select the stern target: the round fires and bursts at its range along the arc-clamped aim. | A.4; `:2643-2645` | Fused rounds |
| `fused-arming-pushout` | Select the close hull: the burst is pushed out to `BlastRadius` from the ship's centre. | A.5; `:2646-2653` | Fused rounds |
| `airburst-at-model` | A proximity airburst beside the LonginusX lands damage where the model shows the blast. | step 15; `:3094-3097` | Fused rounds |
| `penetrator-reaches-interior` | A delayed penetrator into the LonginusX nose detonates inside. | step 15 | Fused rounds |
| `hud-forecast-fused` | The HUD shows Direct, Burst at N m or Refused for fused weapons, and a percentage for direct weapons. | C; `:2661-2663` | Fused rounds |
| `unbranded-no-manufacturer` | An unbranded smoke weapon shows no Manufacturer row. | step 6 | Fused rounds |
| `refused-shot-free` | A refused shot costs no ammo, energy, heat, sound, visibility or cooldown. | B; `:2664-2666` | Refused rounds |
| `refused-beam-no-flicker` | A refused beam never starts. | B | Refused rounds |
| `refused-charge-never-charges` | A refused charged weapon never starts charging. | B | Refused rounds |
| `instant-and-charged-fire` | Autocannon and ChargeBlast SG fire; effects, particles and action-bar icons show. | step 5; `addressables-cut.md:362-366` | Weapon feel |
| `burst-cadence` | Burst cadence is unchanged. | F | Weapon feel |
| `charge-release-press` | A fast release-and-press on a charged weapon starts no second charge. | F | Weapon feel |
| `existing-build-play` | Under "Use Existing Build", the Weapon feel checks hold (GUID keys in the player catalog). | step 20; `addressables-cut.md:367-370` | Weapon feel, Setup: play mode Use Existing Build |
| `ai-never-fires-refused` | The hostile AI whose only weapon is refused never fires it. | D | AI fused discipline |
| `ai-never-bomb-fishes` | The neutral AI carrying a fused weapon never fires it. | D | AI fused discipline |
| `continue-keeps-items` | Warp, quit, Continue: same items, same tier and brand. | step 16; `item-provenance-cut.md:536-537` | Long haul |
| `death-ends-run` | Die: Continue is disabled and `run.cc` holds no ledger. | step 17; `:540-541` | Long haul |
| `shield-panel-rig` | The numbered shield-panel look, including the Cut 5 dicing and break-through. | step 18; `shield-panel-cut.md:683-700,737-739` | Setup: FieldShieldTest scene |
| `studio-*` (5 checks) | The fire-control union renders; the schematic underlay; restored hull records; the fuse fields edit; the provenance ledger renders from a real `run.cc`. | step 19 | Setup: CultCache Studio |
| `addressables-duplicates` | Analyze the duplicate dependencies: 5 known leftovers. | step 20 | Setup: Addressables Analyze |
| `new-game-start`, `purchase-keeps-brand`, `tier-colour` | New Game; buying keeps the brand; tier colours show. | steps 3, 4, 6 | Setup: New Game |

Imported results (checklist section 8, revision `113164fa`, 2026-09-30):
- `new-game-start`: pass.
- `purchase-keeps-brand`: pass.
- `instant-and-charged-fire`: partial ("charged weapon unprovable: 1x2 hardpoint").
- `tier-colour`: fail ("known defect, `settings-globals-cut.md:150`; fixed by settings-globals Cut 1").
- `unbranded-no-manufacturer`: blocked ("`give Lamp` named a test fixture").
- `loot-keeps-brand`: no result ("not yet reported").

### 5.2 Scenarios

All use `Ambient: false` except Long haul. Positions are authored in the seed and pinned by the condition tests.

| Scenario | Player | Entities | Checks |
|---|---|---|---|
| Djinni shakedown | Djinni, full fit | none | 2 |
| Starved reactor | a combat hull with an undersized reactor, energy weapons and a shield | 1 unpiloted bare hull ahead, neutral | 3 |
| Arcs | a hull with side mounts | 3 unpiloted bare hulls (bow, beam, stern), neutral; 1 turret preset, hostile | 3 |
| Duel | combat fit | 1 piloted LonginusX, hostile, carrying a slow missile weapon, cargo holding a branded item | 8 |
| Launcher angles | GT 3K or pswarm plus a direct weapon | 1 unpiloted LonginusX, hostile; 1 fragile bare hull | 4 |
| Fused rounds | Smoke Proximity, Smoke Delayed, a direct weapon | unpiloted: LonginusX mid range, hostile; bare hull on the clear bearing; fragile bare hull; close bare hull inside `BlastRadius`; stern target | 9 |
| Refused rounds | Smoke Refused (instant, beam, charged) plus one working weapon | 1 unpiloted bare hull | 3 |
| Weapon feel | a hull with a 2x2 energy hardpoint: ChargeBlast SG, Autocannon, a burst weapon | 1 unpiloted bare hull | 4 |
| AI fused discipline | a sturdy hull | 1 piloted neutral with a fused weapon; 1 piloted hostile whose only weapon is refused | 2 |
| Long haul | standard combat fit, `Ambient: true` | none | 3 |

### 5.3 Test designs (Q3)

None has a product. They are unbranded and outside the economy.
- `Smoke Proximity`: Proximity, blast 8.
- `Smoke Delayed`: Delayed, blast 4, penetration 2 cells.
- `Smoke Refused`: Proximity, blast 30, range 20.

These three are the operator's own designs from tonight, Spectra clones, energy, 1x2.
- `Smoke Refused Beam` and `Smoke Refused Charge`: a constant weapon and a charged weapon with a fuse and
  `Range < BlastRadius`.
- All must pass `CultRecordRefs.Validate` (R-heat, stat modifiers, roles).

### 5.4 Presets

About eleven, named after their scenarios. The seed author picks designs with `AetherDb hardpoint-fit`. The only
hard constraints are those the condition tests pin.

---

## 6. Subtraction ledger

| Cut | Removed | Added | Targets, schemas, stores |
|---|---|---|---|
| 0 | `give` 30, `spawnturret` 26, wanderers 20, setting 1, tests 22 | 0 | no change |
| 1 | `StartGame` spawn 10, joins 6 | `Scenario.cs` 60, `RunStart.cs` 110, `Admit` 12, `Materialize` 20, `GenerateZone` 4; tests 300 | +1 schema (`aetheria.scenario`) |
| 2 | duplicated New Game body about 25 | menu panel and launch body about 45; 1 static | none |
| 3 | none | `Checks.cs` 50, stores 4, `AetherDb` 90 (or the menu 50); tests 120 | +2 schemas, +1 store file |
| 4 | the seed command, once spent (300) | seed 300; condition tests 250; catalog and ledger data | catalog content, ledger content |

Net production code is about +250. That buys:
- the main-menu scenario affordance;
- typed checks with history;
- one staging owner and one admission primitive (from five hand-written joins);
- the first game consumer of the presets that already existed.

It removes three ad hoc test affordances and the wanderers that ran in every zone.

---

## 7. Operator questions

Each question stands alone. The first is what the scenario request leaves open.

**Q1. Is a scenario arena part of a real run?**
Your words settle what an arena holds. They leave open what surrounds it. Checks 16 and 17 (warp, save,
Continue, death) need a real run. Every other check needs only the arena.
- **A.** The arena is the entrance zone of a small prelude galaxy built from the scenario's seed. Stations,
  planets and wormholes are there, and the run saves on warp and quit, continues and ends at death like any run.
  Launching a scenario replaces your saved run, as New Game does. Scenario-only details (unpiloted ships, forced
  stances) do not survive a Continue.
- **B.** A sealed arena: one zone, no galaxy, no saving. Launching never touches your saved run. Checks 16 and 17
  go back to plain New Game play. This is a second zone-entry path with no galaxy under it, which `PopulateLevel`
  and the sector map do not support today.
- **Recommended: A.** It is the same New Game path with an input, so there is no second boot path. Every rule
  under test then runs exactly as it does in a real run. Replacing the saved run seems harmless for a dev
  affordance.
- **Depends on it:** Cut 2's launch body; the 0b lifecycle row for staged entities; whether Long haul exists.

**Q2. Who records a check's result, and where?**
You play and observe. Something has to write "pass at `abc123`, note ...".
- **A.** In game. The Scenarios panel gets Pass/Fail/Partial and a note per check. The game writes the ledger file
  in your working tree, and you commit it with your other work.
- **B.** The agent. You tell the session what you saw, and it records typed results with `AetherDb record` on a
  branch and pushes. The menu shows each check's latest result, read-only.
- **C.** No typed results. Checks are typed, and results go in a prose ledger doc, as checklist section 8 does now.
- **Recommended: B.** It matches how tonight's smoke already worked: you play, the agent records. Nothing in the
  game writes a file that git tracks, and no agent needs to touch your tree. The menu still shows what is stale.
  A can be added later on the same append primitive.
- **Depends on it:** Cut 3's writer and whether the game attaches the ledger writable.

**Q3. Where do test-only item designs live?**
Checks A-C need fused weapons the catalog does not ship. Tonight they were authored in place and left
uncommitted.
- **A.** Committed catalog designs with no product. With no product nothing makes them, so they are never
  generated, stocked, sold or looted. A preset builds them unbranded (a new `Materialize` rule). Studio lists them
  beside real content.
- **B.** Committed designs with a product and price 0. Generation skips price 0 today, but only through a filter
  written for another reason, so the exclusion is a coincidence.
- **C.** A scratch catalog swapped in per session, as checklist step 2a proposed. Scenarios would then depend on
  which catalog is loaded, and the fixtures would not be retained.
- **Recommended: A.**
- **Depends on it:** the `Materialize` rule in Cut 1; section 5.3.

**Q4. Which ad hoc test affordances do scenarios retire?**
`give` got its hull route tonight (your ruling: "mothballed ships do not require docking bays"), and step 7 was its
reason. `spawnturret` places a turret. Neutral wanderers spawn two non-hostile ships in every generated zone, "so
combat has something non-hostile to target" (`f732cff9`).
- **A.** Delete all three. Scenarios cover ships, cargo, turrets and neutral targets. `CommissionShip` stays for
  purchases.
- **B.** Delete the hull route and `spawnturret`. Keep `give` for items (ad hoc poking outside scenarios) and keep
  the wanderers.
- **C.** Keep everything.
- **Recommended: A.** Each one is a second way to create what scenarios now author, and the wanderers change every
  zone of real play for a testing reason. `iff`, `trackmissile`, `revealzones`, `tow` and `capturepreset` stay.
- **Depends on it:** Cut 0.

Defaults Self took, which you may overrule:
- Scenarios show only in editor and development builds.
- The ledger is its own committed store, `GameData/Checks.cc`, not the catalog.
- "Check", not "behaviour", because `Behavior` is the gear type family.
- Brokkr is not installed in this campaign (section 0.4).
- The scenario record holds no "start docked" field. No first consumer needs one.

---

## 8. Follow-ups outside this campaign

- Re-anchor `docs/headless-playground-cut.md`. Its Cut 1 now builds on `RunStart` and `Zone.Admit`; its `new`
  command stages a `Scenario`.
- Brokkr install plus a "launch scenario" intent, if agents should drive Unity play.
- A durable agent authoring command for presets (fit by design name with validation), instead of transient seed
  commands.
- `AetherDb` carries spent one-shot migration commands (`targeting-catalog-6c`, `-6d`, `firing-arc-migrate`, and
  others; `Program.cs` is 1,477 lines). This is a subtraction pass of its own.
- `ItemManager`'s clock seed (`ItemManager.cs:20`) makes lot quality vary per scenario launch. It is recorded,
  not fixed here.

## Mining Cut 3: Soul (`codex/mining` `39cec96a..55ca6d3d`, 2026-10-01)

**Verdict: hold for one fix batch.** It is queued, not dispatched, because the operator ordered a drain.

No correctness bug was found. The suite passes 516/516 with the catalog `b698e224` and the settings `b2e346f5` that the commit pins. The shared `pins/data/Settings.asset` (`347752dd`) is scenarios' file, so any runner that mounts that path by default has been testing mining with the wrong settings.

Findings:
- **F1 (medium, operator):** picking a rock in a big belt costs one scan of every rock the search circle reaches. `Zone.ChunksNear` (`Zone.cs:333`) and `VisibleChunksInReach` (`Entity.cs:378-385`) run on every reticle, next or previous key press. Measured cost: 63 ms at 30k rocks, 449 ms at 300k and 4.3 s at 3M. The per-tick cost is fine.
- **F2 (medium):** nothing tests that the shipped catalog has a rock kind. Taking either side of the catalog conflict unchanged ships dark belts silently. Add a test for "at least one rock kind with weight above 0".
- **F3:** the merge rule is corrected above.
- **F4 (medium):** nothing tests the ping half of `Sensor.Gain`, because the golden fixture saturates at the cap. Two mutants survive (`Sensor.cs:168`, `:189`). Soul's sweep fixture kills both and gives the same hash (`35DE0E26…`) before extraction, at extraction and at the tip.
- **F5:** `FieldKinds.Ensure`'s explicit save (`ZoneData.cs:162`) is needed on a clean store. Keep it and commit the probe.
- **F6/F7/F8 (low):** the `SetTarget` refusal is tested only on dark rocks (`Entity.cs:345`). The outer skip edge of `ChunksNear` is untested. Rock reach reads `Enabled`, and nothing tests it.
- **Deviation (operator):** launchers count toward rock reach (Q13), but under Q12 they can't mine. So after Cut 4 a player can pick a rock that no weapon can act on.
- **Plausible (operator visual check):** the target indicator uses `AsteroidVerticalOffset`, but the shader also subtracts the nebula surface height.

Promises that held:
- The `Gain` extraction is bit-identical.
- `SetTarget` is the only writer.
- A dark target is dropped on the tick and on `Activate`.
- Docking, broken rocks and wormholes are handled.
- The catalog has 206 identical records, plus one kind and 18 weapons now Exact.
- Old saves get a kind on first load.

The fix batch commits Soul's four probes (in the session scratchpad, `soul-mc3-run/probes/`) and the catalog-kind test.

**Rulings (operator, 2026-10-01): "Fix the belt freeze; launchers shouldn't count toward reach".**
- F1: fix it before the merge. The cost of a key press must not grow with belt size.
- Rock reach counts only weapons that can mine; under Q12 launchers can't. This supersedes Hands' Q13 reading.
- The fix batch was dispatched with both rulings.

**Mining fix batch, Hands (2026-10-01): `f88d1fc1`, `83d8371e` on `codex/mining`. 523/523; 9 mutants each killed by their own test.**
- Launchers: `Weapon.CanMine` (virtual, true by default; `LockWeapon` sets false) is the one predicate. Reach reads `CanMine && Active`. Cut 4's firing gate (`FireControl.cs:175`) must read the same predicate.
- Soul F2/F4/F5/F6/F7/F8 tests are committed.
- Belt freeze **not built; stopped at a fork.**
  - Each rock has its own angular speed (`Distance` is a continuous float), so no band shares a phase.
  - At 3M rocks, 633k are in reach and 2,240 are visible.
  - Proposed: a derived per-belt index (bands of 256 by distance, sorted by turn, with the query arc widened by rate spread and at most 8 band re-sorts per query); a visibility upper bound that skips dark bands; best-first next/previous and reticle; the rules move from `ActionGameManager` to `Entity`.
  - Work in progress (unbuilt) is in the session scratchpad: `hands-mc3fix-index-wip.patch`.
- Question: today's Previous with no target picks the second-farthest rock (off by one). Hands recommends fixing it.

**Belt-freeze rulings (operator, 2026-10-01):**
- **Structure:** "Let's go for it. There's a voice in my head screaming that this is too much code and we should just reduce the whole belt to a single entity, but this stuff will be extra important for when we have EW and much more crowded levels. Just don't specialize the indexing too much towards asteroid belts, because we'll want a bunch of subsystems feeding targeting data. Think the spacebound microfauna from the slime mold experiment, pretty sure there's a campaign for that."
  - The index is a general targeting index that many sources feed: belts, entities, later EW and microfauna. Belts are one provider, not the shape of the index.
  - The slime-mold experiment is `Assets/Shaders/Compute/Slime/Slime.cs`. No campaign doc was found by grep on master docs or by voidbot; ask the operator before assuming one.
- **Previous off-by-one:** "Fix the off-by-one". With no current target, Previous picks the farthest rock.
- **Next step:** an Imagination pass (Opus) maps the general index. Inputs: the provider seam, motion models (orbiting rocks with per-rock rates, and moving entities), the brightness/visibility bound as a per-provider bound, and best-first queries. Hands' work in progress (`hands-mc3fix-index-wip.patch`) is input, not spec. Then Hands on Sonnet, then Soul.
