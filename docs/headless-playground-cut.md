# Headless Playground: Run Lifecycle Cut Map

Date: 2026-09-17

Status: Imagination pass, cut map. Nothing here has landed. Anchors are against
`codex/item-provenance` HEAD `1e647953`. Mechanism claims marked **(probe)** were run
headless against the real catalog `GameData/Aetheria.cc` (read-only) and the authored
`Assets/Resources/Settings.asset`. Run and player stores went to a temp directory. The
probe was built with `--artifacts-path` in scratch, so this tree got no `bin`/`obj`
writes. CultLib was a temporary detached worktree at `a0813c6`, since removed. The probe
source is not kept; §2.8 records what it did.

## 0. Target

Operator, 2026-09-17: player inputs "map directly to the same entity inputs the AI
uses". An agent should be able to "spin up a playground ... generate galaxies, puppeteer
an entity around yourself using the existing agent routines", and "This should be
possible without involving Unity".

Ends:
- An agent (Hands or Soul) runs a scripted session headless. The script can:
  - start a new game (galaxy, entrance zone, player ship);
  - drive the pilot by hand-set entity inputs or by an existing `Agent`;
  - dock, undock, warp between zones, take hits, die, and save;
  - reopen the stores and Continue.
- The session emits a compact trace. It exits non-zero when an expectation fails or an
  exception escapes.
- First consumers:
  - the headless half of the CultCache-migration play smoke
    (`docs/cultcache-migration-cut.md:2739-2765`);
  - the headless half of the provenance Cut C operator checks
    (`docs/item-provenance-cut.md` §6, "Operator-only checks after Cut C");
  - the reopen defect in §2.6.

Invariants:
- **One owner of the run lifecycle.** The playground and `ActionGameManager` both call
  the same ServerShared owner. Neither re-implements new game, zone entry, warp, dock,
  save, Continue or death.
- **One input surface.** Manual input, agent routines and playground commands write the
  same entity fields and call the same behavior methods. The playground has no private
  input path and writes no entity state the player cannot reach. Exceptions are named
  commands that call the same owner the Unity console calls.
- **Determinism.** A scenario with a given seed produces the same trace twice. The
  probe's two runs matched line for line.
- **The playground never touches `GameData/run.cc` or `GameData/player.cc`.** It opens
  the catalog read-only and keeps its run and player stores in a directory it is given.

Non-goals:
- No rendering, no Unity, no new daemon, no CultMesh surface.
- No headless ballistics. Fire control does not detect hits; it rolls them, headless
  (fork D ruling, Cut 2). Unity flies the shot to the outcome.
- No trade menu, no action-bar UI, no input devices.
- Tier colour, properties panel, input screen and CultCache Studio rendering stay
  operator checks.

## 1. Verdict on the operator's claim

**Mostly true for movement and firing. False for how combat resolves, and the AI's
combat state cannot run without Unity.**

- True:
  - Player movement and AI movement write the same `Ship.MovementDirection`
    (`Ship.cs:27`). Player: `ActionGameManager.cs:1201`. AI: `Agent.cs:67,73,77`.
  - Both write the same `Entity.LookDirection` (`Entity.cs:42`). Player:
    `ActionGameManager.cs:1189-1192`. AI: `MoveTo.cs:26`, `Combat.cs:111`,
    `Agent.cs:66`.
  - Both write the same `Entity.Target` (`Entity.cs:40`). Player:
    `ActionGameManager.cs:366-397`. AI: `Minion.cs:14`.
  - Both fire through the same `Weapon.Activate/Deactivate`. Player:
    `ActionBarSlot.cs:190-204`, via the action bar at `ActionGameManager.cs:411-412`.
    AI: `Combat.cs:56,106,108`.
- Player-only inputs, all plain entity writes that an agent could make:
  - `TractorPower` (`ActionGameManager.cs:1214-1216`);
  - `OverrideShutdown`, `HeatsinksEnabled`, shield `Enabled`, `Sensor.Ping` (`:339-362`);
  - gear `IActivatedBehavior` (`ActionBarSlot.cs:159-168`);
  - consumables (`ActionBarSlot.cs:119-122`, `Entity.TryActivateConsumable`).
- Player-only lifecycle actions. These are the orchestration this map moves:
  - interact, which means warp, dock or undock (`ActionGameManager.cs:298-320`);
  - tow (`:889-913`);
  - boarding another docked ship (`InventoryPanel.cs:117-118`).
- **False: combat resolves in Unity.**
  - `InstantWeapon.OnFire` only raises an event (`InstantWeapon.cs:183`). The projectile
    is a Unity effect manager (`EntityInstance.cs:203-204`).
  - Hits are physics colliders (`HullCollider.cs:18`, `GuidedProjectile.cs:170`).
  - Turning a hit into armor, item and hull damage is `EntityInstance.DamageSchematic`
    (`EntityInstance.cs:276-314`), with hit-shape and penetration at `:330-395`.
  - NPC death removes the entity from the zone at `:437`. Loot drops in the same place
    (`:405-435`), rolled with `UnityEngine.Random`.
  - Pickup is a collision trigger (`ShieldManager.cs:29`).
  - **(probe)** Weapon groups fired 6 `OnFire` events at an enemy 50 units ahead. Enemy
    hull stayed 500.0 -> 500.0.
- **False: `CombatState` needs Unity-written data.** It indexes
  `Ship.HardpointTransforms` (`Combat.cs:101`). Only `EntityInstance.cs:516` writes that
  dictionary, from barrel transforms.
  - **(probe)** A `Minion` with a target throws `KeyNotFoundException: 'Energy Hardpoint '`
    on its first combat step.
  - Every NPC agent that acquires a target would throw the same way headless.
  - The weapons' own direction reader already has a fallback (`Behaviors.cs:24-43`).
    `CombatState` does not use it.

## 2. Body findings

### 2.1 Where the run lifecycle lives today

All of this is Unity code:

| Concern | Anchor | Unity dependencies |
|---|---|---|
| Store composition | `ActionGameManager.cs:43-63` (static, `Application.dataPath`) | path only |
| Handoff across scene load | statics `CurrentGalaxy`, `IsTutorial` (`:96-97`), written by `MainMenu.cs:104-105,126,138,152,166`, read by `SectorMap.cs:114-310` | `SceneManager.LoadScene` (`MainMenu.cs:106,139,167`) |
| New game: clear | `MainMenu.cs:112` | none |
| New game: galaxy | `MainMenu.cs:125,148` (`UnityEngine.Random.value` background noise), `:127-141,153-169` (`Task.Run`, then `Observable.NextFrame`) | Random, UniRx main-thread scheduler |
| ItemManager | `ActionGameManager.cs:264`; RNG clock-seeded at `ItemManager.cs:20` | `Debug.Log` |
| Start / Continue | `StartGame` `:725-770`: spawn player ship `:733-744`; Continue resolves the docked child `:751-762`; action bar restore `:764-767` | renderer binding, `SectorMap` reveal |
| Zone entry | `PopulateLevel` `:639-675`: generate/unpack `:643-654`; move pilot `:659-666` | `ZoneRenderer.LoadZone`, `Debug.Log` |
| Wormhole placement (a game rule) | `ZoneRenderer.cs:222-231` (`dir * Pack.Radius * GameSettings.WormholeDistanceRatio`) | lives in a MonoBehaviour |
| Warp | detect `ActionGameManager.cs:312-316`; `EnterWormhole` `:615-637`, which saves on arrival (`:635`) | `ZoneRenderer.WormholeInstances` as the wormhole list |
| Dock / undock / tow | `:801-823`, `DoDock` `:825-843` (cameras, menus), `:845-887` (dialogs), `:889-913` | cameras, dialogs |
| Board a docked ship | `InventoryPanel.cs:117-118` | UI button |
| Save | `SaveRun` `:241-249`, called at quit `:236` and warp `:635` | action-bar slots (`:246`) |
| Death | `BindToEntity` subscribes `Death` -> `Die` (`:1053`); `Die` `:1080-1115` clears the run (`:1088`) | `Time.time`, `Observable.EveryUpdate` post-fx |
| Tick | `Update` `:1151-1220`: input `:1189-1216`, then `Zone.Update(Time.deltaTime)` `:1218` | `Time.deltaTime`, pause flag |
| Dead code | `IntroCutscene` `:772-799`; its only call is commented out (`:760`) | coroutine, `Time.time` |

### 2.2 Already headless

- `Aetheria.Shared/Aetheria.Shared.csproj` compiles `Assets/Scripts/ServerShared/**`
  plus UniRx (`UniRxLibrary`) and Ink with no Unity defines. It is the
  Unity-independence check.
- Galaxy generation, zone generation, `Zone`, entities, behaviors, agents, `RunSave`,
  `AetheriaStores` and `ItemManager` all run under it. The probe measured:
  - galaxy seed 42: 196 zones;
  - the first zone with ships and stations: 7 entities and 4 agents;
  - a player `LonginusX` spawned through `LoadoutGenerator`;
  - thrust by hand-set `MovementDirection` reached 142 m/s in 5 s;
  - a `Minion` patrol moved the player ship 642 units in 10 s;
  - `TryDock` and `TryUndock` succeeded (`Entity.cs:721-755`);
  - `Ship.EnterWormhole` followed by stepping fired `OnEnteredWormhole` (`Ship.cs:82-90,372`);
  - `Death` fired on hull damage (`Entity.cs:277-280`);
  - `RunSave.Capture` and `RunSave.Commit` wrote a 489 KB `run.cc`.
- Settings: `GameSettings` is a ScriptableObject (`GameSettings.cs:11`) holding
  ServerShared classes.
  - `tools/AetherDb/AuthoredSettings.cs` reads them from the asset YAML. The probe
    placed every field (0 unplaced).
  - Two lifecycle inputs are Unity-only `GameSettings` fields: `StartingHullName`
    (`GameSettings.cs:17`, authored `LonginusX`) and `WormholeDistanceRatio` (`:23`,
    authored 0.75).
- Randomness:
  - `ItemManager.Random` is clock-seeded (`ItemManager.cs:20`).
  - `Galaxy` is clock-seeded only when `seed == 0` (`Galaxy.cs:108,158`).
  - `Zone` seeds from the zone name (`Zone.cs:54`).
  - `Zone.Update` runs belt updates on `Task.Run` (`Zone.cs:144`). It is still
    deterministic (probe: identical traces).

### 2.3 Agents

- The agent API is `Agent.Update(dt)` (`Agent.cs:47-56`) over a `BaseState` graph.
- `Minion` (`Minion.cs:7-21`) patrols (`PatrolOrbits.cs`), auto-targets the first visible
  enemy (`:14`), and enters `CombatState`.
- Agents are created only for packed non-player ships at zone construction
  (`Zone.cs:88-93,99-106`) and are updated before entities (`:147-150`).
- Nothing ever removes one from `Zone.Agents`.
- The player ship never gets an agent.
- An agent held in a zone's list would stay behind when its ship warps (`PopulateLevel`
  moves the entity, not agents).
- There is no "fly to a point" state. `MoveToState` is abstract and its only concrete
  state is `MoveToOrbitState` (`MoveTo.cs:7-37`).

### 2.4 AetherDb

- Commands: `census`, `station-fit`, `hardpoint-fit`, `loadout [seed]`, `save`,
  `factions`, `dangling`, `settings`, `settings-dump` (`Program.cs:18-35`).
- Stores open through `AetherDb.Open`, which calls `AetheriaStores.Open` with
  `GameData/run.cc` hard-wired (`AetherDb.cs`). `save` (`Program.cs:349-410`) is the only
  played-save reader.
- It already owns `AuthoredSettings` (YamlDotNet) and references `Aetheria.Shared`, so it
  has everything the playground needs.

### 2.5 Tests at HEAD

**(probe)** `dotnet test tests/Aetheria.Shared.Tests` at `1e647953` with CultLib
`a0813c6`: **32 passed, 2 failed.** Both failures are
`MessagePackSerializationException: Failed to deserialize SavedZone value`:
- `LoadoutTests.MaterializedLotsSurviveSaveAndReload`
- `RunSaveTests.CommitKeepsOnlyReachableLots`

This contradicts the "34 tests green" verification that `docs/item-provenance-cut.md`
sets for Cut B.

### 2.6 The reopen defect, confirmed

**(probe)** Reopening a run store that holds a `SavedZone` with any entity throws:

```
MessagePackSerializationException: Failed to deserialize SavedZone value.
  TypeAccessException: No hash-resistant equality comparer available for type: CultMath.int2
```

- The failing field is `EntityPack.PersistedBehaviors`, a
  `Dictionary<int2, PersistentBehaviorData[]>` (`EntitySerializer.cs:200`), written at
  `:35-40` and read at `:126-130`.
- The player ship's dictionary was **empty** (0 persisted behaviors) and it still threw.
  MessagePack builds the dictionary's comparer before reading any entry.
- `CultMessagePackSecurity` runs `UntrustedData` and adds a comparer only for
  `CultRecordRef<T>` keys.
- No other run-store type has a struct dictionary key. `SavedGame` uses `int` keys, the
  ledger uses `int`, and `PlayerSettings` uses `string`.
- Consequence: **Continue has been unable to load any run that visited a zone** since
  entities entered the run store under this security. Migration smoke step 6 cannot
  pass. Unity reads through the same CultLib serializer, which is inferred, not run
  here.

### 2.7 Asides found in passing (not this cut's to fix)

- `TargetNearest` picks the **farthest** visible enemy: `MaxBy(length(...))`
  (`ActionGameManager.cs:378-379`).
- The interact loop enters every wormhole in range, then calls `Dock()` anyway
  (`:312-317`).
- `SaveRun` passes `DockedEntity ?? CurrentEntity` (`:245`), but `BindToEntity` nulls
  `DockedEntity` (`:946`). The order is fragile, not wrong today.

### 2.8 The probe (discarded)

A scratch console referencing `Aetheria.Shared` and `AuthoredSettings.cs`. It:
1. Loaded authored settings.
2. Opened the stores (catalog read-only; run and player in temp).
3. Generated a galaxy with seed 42 and set `ItemManager.Random` to seed 7.
4. Generated zones outward from the entrance until one held ships and stations.
5. Spawned the player ship, drove it by hand, then by a `Minion`.
6. Forced a target, fired groups by hand, then docked, undocked and entered a wormhole.
7. Killed an NPC by hull damage, then saved, disposed and reopened.

Traces from two runs were identical.

## 3. Authority map

**Owner: `Run`, a new ServerShared class (`Assets/Scripts/ServerShared/Run.cs`).** It
owns one live run's lifecycle: start, zone entry, pilot, dock state, warp, save,
Continue and death. There is one instance per run. It holds no Unity type.

- **Inputs:**
  - the `CultCache` from `AetheriaStores.Open`, composed by the caller;
  - `GameplaySettings`, `ZoneGenerationSettings`, `PlanetSettings`;
  - a `uint seed` for `ItemManager`;
  - `Action<string> log`;
  - for a new run, a caller-built `Galaxy` and `isTutorial`;
  - `dt` per step;
  - commands from either lowering;
  - `SavedActionBarBinding[]` at save, owned by the Unity action bar (the playground
    passes empty).
- **Outputs:**
  - state: `Galaxy`, `ItemManager`, `Zone`, `Pilot` (the piloted `Entity`), `Station`
    and `Bay` when docked, `TowingStation`, `Ended`;
  - events: `ZoneEntered(Zone)`, `PilotChanged(Entity)`, `Docked(Entity, EquippedDockingBay)`,
    `Undocked`, `WarpArrived(GalaxyZone)`, `Saved`, `Died(CauseOfDeath)`;
  - store writes through `RunSave.Commit` and `RunSave.Clear` only.
- **Derived state:**
  - `ActionGameManager.CurrentEntity`, `.Zone`, `.ItemManager`, `.DockedEntity`,
    `.DockingBay` and `.TowingStation` become read-only projections of `Run`, for
    display only.
  - `ZoneRenderer.WormholeInstances` becomes render-only: its keys are
    `Zone.Wormholes`, and it is no longer the wormhole list gameplay reads.
  - `SectorMap` reads `Run.Galaxy` for display.
- **Forbidden writers after the cut:**
  - `ActionGameManager` may not call `RunSave.Clear`, `RunSave.Capture`, `RunSave.Commit`,
    `ZoneGenerator.GenerateZone` or `new Zone`, and may not add or remove the pilot in
    `Zone.Entities`.
  - It may not construct `ItemManager`, spawn the player ship, subscribe `Death` to end
    the run, or set `CurrentEntity`, `DockedEntity`, `DockingBay` or `TowingStation`.
  - `MainMenu` may not call `RunSave.Clear` or construct `Galaxy(cache, saved)`.
  - `InventoryPanel` may not write `CurrentEntity` or `DockingBay.DockedShip`.
  - `ZoneRenderer` may not create `Wormhole` objects.
  - The playground may not write `Position`, `Velocity`, `Hull.Durability` or zone
    membership directly. It has no `hit` command; damage comes only from fire control
    (fork D ruling).
- **Fire control (Cut 2).** `FireControl` (ServerShared) owns hit, cell and damage; `Zone`
  owns death removal, loot drops and the pickup commit.
  - `Entity.TargetItem` is a command, written only through `TrySelectTargetItem`. The
    aimed item is derived from it plus reveal.
  - Unity weapon effects and `HullCollider` are presentation of `ShotOutcome`.
  - Forbidden deciders of hits or damage: Unity weapon effects, `HullCollider`, any
    `Physics.*` query, `EntityInstance`, `ShieldManager` (it may only call
    `Zone.TryPickUp`), the playground, and `Entity.HardpointTransforms` (deleted).
- **Shared paths:**
  - Manual input and agent input write the same entity fields (§1), and both go before
    `Run.Step`. Unity writes input in `Update`, then calls `run.Step(Time.deltaTime)`.
    The playground updates its pilot agent inside `Run.Step`, before `Zone.Update`, so
    the order matches.
  - Direct interaction (Unity's Interact key), programmatic interaction (playground
    `dock`/`warp`) and Continue's docked landing all commit through `Run.TryDock`,
    `Run.TryEnterWormhole` and `Run.Land`.
  - A new run and Continue share `Run.EnterZone`.
  - Warp arrival and quit share `Run.Save`.
- **Pilot agent:**
  - `Run.PilotAgent` (nullable) is owned by `Run`, not by `Zone.Agents`, so it follows
    the pilot across warps.
  - Unity leaves it null. It may never be non-null while Unity input is enabled.
  - `Zone.Agents` stays the owner of NPC agents.
- **Deletion line:**
  - `ActionGameManager`: bodies of `SaveRun`, `EnterWormhole`, `PopulateLevel`'s
    simulation half, `StartGame`, `Dock`, `DoDock`'s state half, `Undock`'s checks,
    `TowShip`, `Die`'s clear, and `IntroCutscene` (entirely);
  - statics `CurrentGalaxy` and `IsTutorial`;
  - `MainMenu.cs:104-105,112`;
  - `ZoneRenderer.cs:222-231`'s wormhole construction;
  - `ItemManager.cs:20`'s clock seed.

  All of these go in the same cut that adds `Run`. None survives as a fallback.

Unity dependencies, each injected or cut:

| Dependency | Where | Disposition |
|---|---|---|
| `Time.deltaTime` | `:1218` | injected: `Run.Step(float dt)` |
| `Time.time` | `IntroCutscene`, `Die` post-fx | cutscene deleted; post-fx stays in Unity, on `Died` |
| `UnityEngine.Random` | `MainMenu.cs:125,148` (background noise), `EntityInstance.cs:413-428` (loot) | noise stays in the menu as a galaxy-generation input; loot roll moves to `Zone` on `ItemManager.Random` (Cut 2); effect spread jitter (`ProjectileManager.cs:20-26`) stays visual |
| Clock RNG | `ItemManager.cs:20` | cut; the seed is a constructor argument, and Unity passes a clock-derived one |
| Coroutines | `IntroCutscene` | deleted (dead) |
| UniRx | `Observable.NextFrame` in the menu, `EveryUpdate` in `Die` | stay in Unity; `Run` uses C# events, and `Entity.Death` (UniRx, headless-compiled) is what `Run` subscribes to |
| `Debug.Log` | `:264`, `:657`, `MainMenu` | injected `Action<string>` |
| Scene load | `MainMenu` | stays; the handoff static becomes `ActionGameManager.NewRun : (Galaxy, bool)?`, command-only, consumed once in `Start`; null means Continue |
| MonoBehaviour state | `_currentEntity`, `DockedEntity`, `DockingBay`, `TowingStation`, `Zone`, `ItemManager` | projections of `Run` |
| Barrel transforms | `EntityInstance.cs:516` -> `HardpointTransforms` | `HardpointTransforms` deleted (Cut 2); barrels are presentation only, `GetBarrel` |
| `Physics.*` hit queries | `Projectile`, `GuidedProjectile`, `Laser`, `Lightning`, `HullCollider` subjects | deleted from damage paths; `FireControl` rolls (Cut 2) |

## 4. Cuts

Build budget for every cut:
- Packages: `Aetheria.Shared` (netstandard2.1), `tools/AetherDb` and
  `tests/Aetheria.Shared.Tests` (net10.0), Debug only.
- CultLib at `a0813c6`, unmodified, under fork R (a).
- No new project, target, package or schema store.
- Build host: this Windows workstation, which is also the target for the headless
  checks.
- `Assembly-CSharp` is proven only by Unity batchmode with the editor closed, per
  `docs/item-provenance-cut.md` §6. This is the operator's call.
- Headless commands use `--artifacts-path <scratch>` while another agent is reading the
  tree.

### Cut 0: make a run with entities reopenable (fork R)

**Ruled (operator, 2026-09-17):** fork R is the pairs array. It lands with the provenance
Cut B fix batch; see `docs/item-provenance-cut.md`.


- `EntitySerializer.cs:200`: `PersistedBehaviors` becomes
  `(int2 position, PersistentBehaviorData[] data)[]`, matching `Equipment` at `:197-199`.
  - Key 5 is kept. The wire shape changes from a map to an array; saves are discarded
    and no `GameData/run.cc` exists.
  - `:35-40` packs pairs.
  - `:126-130` builds a local lookup at unpack.
- `RunSaveTests.cs` `BarePack` initializer: `Array.Empty<...>()`.
- Verification:
  - The two HEAD-red tests go green: 34 passed.
  - Mutation: restore the `Dictionary<int2,…>` type. Both tests go red with the
    `int2` comparer message.
  - Add `ReopenedPackKeepsPersistedBehaviors` to `RunSaveTests`. A pack with two
    positions, each with one persisted behavior, commits and reopens, and the positions
    and data are equal.
    - Mutation: pack drops `data`, so the test goes red.
    - Mutation: unpack ignores the lookup, so the test goes red.
- Negative: `rg -n "Dictionary<int2" Assets/Scripts/ServerShared` is empty.
- Ledger: about ±6 C#, +25 test.

### Cut 1: `Run` becomes the single lifecycle owner; Unity lowers it

This is subtraction first, then one new file.

- Deletes and moves:
  - `ActionGameManager.cs`:
    - `IntroCutscene` `:772-799` (dead).
    - Statics `CurrentGalaxy` and `IsTutorial` (`:96-97`).
    - `SaveRun` body `:243-248` becomes `Run?.Save(actionBar)`. Quit (`:236`) calls it.
    - `EnterWormhole` `:615-637` and the detect loop `:312-316` become
      `Run.TryEnterWormhole()`. `WarpArrived` drives `SectorMap.QueueZoneReveal`.
    - `PopulateLevel` `:639-675`: the simulation half moves into `Run.EnterZone`. What
      stays is a `ZoneEntered` handler: `ZoneRenderer.LoadZone`, `PlayMusic`, rebind.
    - `StartGame` `:725-770` becomes `Run.New` or `Run.Continue` in `Start`. It keeps
      the sector reveal and action-bar restore in Unity. Restore reads
      `min(slots, saved.Length)`, so a playground-written save continues in Unity.
    - `Dock` `:801-823` becomes `Run.TryDock()`. `DoDock` `:825-843` keeps cameras and
      menus on `Docked`; its state writes `:828,830,840-841` go to `Run`.
    - `Undock` `:845-887` becomes `Run.Undock()`, returning `UndockResult`. The dialog
      text maps from the result.
    - `TowShip` `:889-913` becomes `Run.Tow()`.
    - `Die` `:1080-1088` becomes a `Died` handler: unbind, menus, post-fx,
      `SavePlayerSettings`. The `RunSave.Clear` at `:1088` goes. `:1053` subscribes
      `Run.Died`.
    - `:264` constructs no `ItemManager`.
    - `Update` `:1218` becomes `Run.Step(Time.deltaTime)`.
  - `MainMenu.cs`:
    - Continue (`:102-107`) sets `ActionGameManager.NewRun = null` and loads the scene.
    - New Game drops `:112` and sets `NewRun = (sector, tutorial)` at `:138,166`.
  - `SectorMap.cs:114-310` reads `ActionGameManager.Instance.Run.Galaxy`.
  - `InventoryPanel.cs:117-118` becomes `GameManager.Run.Board(ship)`.
  - `ZoneRenderer.cs:222-231` iterates `zone.Wormholes`.
  - `ItemManager.cs:20`: the seed becomes a constructor argument. Call sites: 11 in
    tests, 1 in `AetherDb`, and `Run`.
- Settings (fork S (a)):
  - `StartingHullName` and `WormholeDistanceRatio` move from `GameSettings.cs:17,23` to
    `GameplaySettings` (`Settings.cs:155`).
  - The asset keys move under `GameplaySettings:` in `Settings.asset` by text edit, with
    Unity closed.
  - Readers `ActionGameManager.cs:738` and `ZoneRenderer` go through
    `ItemManager.GameplaySettings`.
- `Zone.cs`: `public Wormhole[] Wormholes`, built in the constructor from
  `GalaxyZone.AdjacentZones` with the rule from `ZoneRenderer.cs:226-230`. Null galaxy
  zone gives an empty array.
- New `Run.cs`: types and rules, no bodies here.
  - `static Run New(CultCache, GameplaySettings, ZoneGenerationSettings, PlanetSettings, Galaxy, bool isTutorial, uint seed, Action<string> log)`
    1. `RunSave.Clear`.
    2. `ItemManager` over `RunSave.Lots`, seeded.
    3. `EnterZone(Entrance)`.
    4. Spawn the player ship per `:734-743`.
    5. Set the pilot. Returns a live run.
  - `static Run Continue(CultCache, …, uint seed, Action<string> log)`
    - Returns null when there is no `SavedGame`.
    - Otherwise `Galaxy(cache, saved, log)`, then `EnterZone(saved zone)`, then
      `Land(saved entity)`.
    - `Land` handles an orbital entity per `:753-756`: pilot is the `IsPlayerShip`
      child, and the run is docked there.
  - `void Step(float dt)`
    - Throws if `Ended`.
    - Updates `PilotAgent`, then `Zone.Update(dt)`.
  - `void EnterZone(GalaxyZone)`: the rule from `:643-666`. It raises `ZoneEntered`.
  - `bool TryEnterWormhole()`
    - Picks the nearest `Zone.Wormholes` entry within
      `GameplaySettings.WormholeExitRadius`. Requires the pilot to be a `Ship`, not
      mid-animation, and undocked.
    - On `OnEnteredWormhole`:
      1. `EnterZone(target)`.
      2. Discover adjacent zones.
      3. `ExitWormhole` at the back-link wormhole with
         `WormholeExitVelocity * ItemManager.Random.NextFloat2Direction()`.
      4. `Save(lastActionBar)`.
      5. Raise `WarpArrived`.
  - `EquippedDockingBay TryDock()`: the rule from `:803-821`, within `DockingDistance`.
  - `UndockResult Undock()`: `Ok`, `NotDocked`, `MissingCockpit`, `MissingThruster`,
    `MissingReactor`, `BayNotEmpty`. Check order as `:850-885`.
  - `bool Board(Ship)`: docked only; the ship must be the station's `IsPlayerShip`
    child; updates `Bay.DockedShip`.
  - `void Tow()`: the rule from `:891-911`.
  - `void Save(SavedActionBarBinding[] actionBar)`
    - `RunSave.Capture(cache, Galaxy, Zone, Station ?? Pilot, isTutorial, actionBar)`,
      then `Commit(…, ItemManager.Lots)`.
    - No-op when `Ended`.
    - Remembers `actionBar` for warp saves.
  - Death: `Run` subscribes `Pilot.Death` whenever the pilot changes. On death:
    1. `Ended = true`.
    2. `RunSave.Clear`.
    3. Dispose subscriptions.
    4. Raise `Died(cause)`.
  - `Agent PilotAgent { get; set; }`: setting it while `Ended` throws.
- Tests (in `RunTests`, same project, fixture catalog as `LoadoutTests`). Each is paired
  with the mutation that must kill it:

| Test | Asserts | Mutation that must kill it |
|---|---|---|
| `NewRunSpawnsPilotInEntrance` | pilot is a `Ship` with `IsPlayerShip`, in `Zone.Entities`, `Zone.GalaxyZone == Galaxy.Entrance`; the run store is cleared of a prior `SavedGame` | `New` skips `RunSave.Clear`; spawn omits `Zones.Entities.Add` |
| `WarpMovesPilotAndSaves` | stepping after `TryEnterWormhole` lands the pilot in the target zone, out of the old zone's entities, with `SavedGame.CurrentZone` equal to the target index after reopen | arrival skips `Save`; `EnterZone` leaves the pilot in the old zone |
| `ContinueLandsDocked` | save while docked, reopen, `Continue`: `Station` is the saved orbital, `Pilot` is its player child | `Land` binds to the orbital itself |
| `DeathEndsAndClears` | hull death clears every run record; `Step` throws; `Save` writes nothing | `Died` handler omits `Clear`; `Ended` not set |
| `PilotAgentFollowsWarp` | a `Minion` set as `PilotAgent` still updates the pilot (position changes) after a warp; `Zone.Agents` of both zones exclude it | agent added to `Zone.Agents` |
| `WormholesAreZoneOwned` | `Zone.Wormholes.Length == AdjacentZones.Count` and positions follow the ratio | ratio ignored |

- Verification:
  - Headless build and tests green.
  - Unity batchmode has no `error CS`.
  - Negative greps:
    - `rg -n "RunSave\.(Clear|Commit|Capture)" Assets/Scripts --glob '!ServerShared/**'` is empty.
    - `rg -n "CurrentGalaxy|IsTutorial\s*=" Assets/Scripts` is empty.
    - `rg -n "new Wormhole" Assets/Scripts` matches only `Zone.cs`.
    - `rg -n "new ItemManager\(" Assets/Scripts` matches only `Run.cs`.
    - `rg -n "ZoneGenerator\.GenerateZone|new Zone\(" Assets/Scripts --glob '!ServerShared/**'` is empty.
    - `rg -n "CurrentEntity\s*=[^=]" Assets/Scripts --glob '!ServerShared/**'` is empty.
    - `rg -n "DateTime\.Now" Assets/Scripts/ServerShared/ItemManager.cs` is empty.
- Ledger:
  - `ActionGameManager` about -190 +45; `MainMenu` -4 +2; `ZoneRenderer` -9 +3;
    `InventoryPanel` -2 +1; `SectorMap` ±12 renames; `ItemManager` ±1; `Zone` +10;
    `Run.cs` about +190; tests about +200.
  - Statics: -2 +1.
  - Net production code is about +40. That buys the invariant: the lifecycle has one
    owner, and every rule it holds existed before in a MonoBehaviour.

### Cut 2: fire control rolls hits; death and loot resolve in `Zone` (forks F, A, T, V, E, L)

Rulings, 2026-09-17: "Just roll the dice based on weapon stats, targeting system stats
(new subsystem) and sensor state." And: "We want the player as well as the AI to be able
to aim for specific subsystems once it has gathered enough target data to reveal those
subsystems". Anchors are at HEAD `562cdcf2`. Hands' uncommitted `Behaviors.cs` edit
moves the `BehaviorData` union list down by 10 lines.

**Body facts this cut stands on** (**(probe)** of `GameData/Aetheria.cc` read-only, CultLib
`a0813c6` worktree since removed, plus a GUID grep of effect prefabs):
- 51 designs, 37 products. `LonginusX` is the only ship hull (ControlModule, Sensors,
  Energy 2, Launcher 2, Radiator 2, Reactor, AetherDrive). `Turret` has Ballistic 2 and
  Sensors 1. `Zenith` has no weapon hardpoints.
- 18 weapon designs, all single-target, lowered by four effects: `ProjectileManager` 8,
  `LaserManager` 3 (pulse, velocity 0), `GuidedProjectileManager` 5 (3 `GuidedWeaponData`
  -> `InstantWeapon`, 2 `LauncherData` -> `LockWeapon`), `LightningGunManager` 1.
- **No design uses** `ConstantWeaponData`, `ConstantLaser`, `ConstantLightning`,
  `ConstantParticleWeapon`, `HitscanEffect`, `Mine`, airburst (`Flak Cannon`), MIRV
  splits or `ShieldData`.
- Sensor state exists: `Sensor.Execute` accumulates `Entity.EntityInfoGathered[other]` in
  [0,1] with decay (`Sensor.cs:121-154`); `TargetDetectionInfoThreshold` (0.1) gates
  `VisibleEntities` (`Entity.cs:196-207`); `LockWeapon.cs:92` scales lock by info.
- **`TargetArmorInfoThreshold` (0.5) and `TargetGearInfoThreshold` (0.8) are authored and
  read by no code** (`Settings.cs:207-208`, `Settings.asset:320-321`): the reveal tiers
  the ruling describes, unwired.
- Sensors-hardpoint designs are `not if i see you first`, `The Bat` and `Tractor Beam` (no
  behaviors). `EquipHardpoints` (`LoadoutGenerator.cs:208-228`) can fit the Tractor Beam
  into LonginusX's only Sensors slot; that ship gathers no info, so sees and hits nothing.

**Owner: `FireControl`, a new ServerShared static class (`FireControl.cs`).**
- It owns the engage gate, hit probability, reveal, the roll, cell choice and damage
  application.
- Its only state is the pending shots under fork F (b)/(c), and that state lives in
  `Zone`.
- Inputs:
  - weapon stats `Damage`, `Penetration`, `DamageSpread`, `MinRange`, `Range`, `Spread`
    and `Velocity` (`Weapon.cs:22-66`), with `LockWeapon.CanFire` (`:56`) as a gate;
  - the firer's targeting system, or the unaided defaults when it has none;
  - sensor state, `firer.EntityInfoGathered[target]`;
  - `Target` and `TargetItem`;
  - both entities' positions and directions;
  - `ItemManager.Random`, seeded by Cut 1.
- Outputs:
  - `ShotOutcome { Source; Weapon; Target; bool Hit; bool Shielded; EquippedItem Aimed; int2 Cell; float FlightTime }`;
  - shield `TakeHit`, or `Entity.DamageSchematic` plus `IncomingHit`.

**Rules** (defaults unless a fork is named):
- **Engage gate.** The probability is 0 and no draw is consumed when any of these holds:
  - there is no target;
  - the target is not in `VisibleEntities`;
  - the range is outside [`MinRange`, `Range`];
  - a `LockWeapon` is not locked;
  - the target is outside the firing arc (fork A).
- **Hit probability.** `p = Accuracy * pSensor * pSpread`, multiplied by `pDeviation`
  under fork F (c).
  - `pSensor = saturate(unlerp(TargetDetectionInfoThreshold, Resolution, info))`.
  - `pSpread = Spread > 0 ? saturate(angularRadius / (Spread / 2)) : 1`.
  - `angularRadius = degrees(atan(0.5 * max(Shape.Width, Shape.Height) * SchematicCellSize / range))`.
  - `Spread` is already a cone in degrees (`ProjectileManager.cs:18-23`).
  - `SchematicCellSize` is the one new tuning constant. Hands measures it on the
    LonginusX prefab.
- **Reveal.** `IsRevealed(observer, item)`:
  1. Rank the target's non-hull equipment: hardpoint-mounted first, then size
     descending, then equipment index.
  2. Item `i` of `N` is revealed when
     `info >= lerp(TargetArmorInfoThreshold, TargetGearInfoThreshold, i / max(1, N - 1))`.

  Ordering is fork V.
- **Roll.** One draw against `p`. On a hit, a second draw against `Precision` decides
  where it lands:
  - on success, on `Aimed`'s footprint cells;
  - otherwise, on one cell drawn uniformly over the hull `Shape`.

  `Aimed` is `TargetItem` only while that item belongs to `Target` and is revealed. It
  is derived when read; no loop clears it.
- **Damage.** The rule moves verbatim from `EntityInstance.cs:334-394`:
  1. Expand the hit shape `round(DamageSpread)` times.
  2. If `Penetration > .5`, march cells along firer->target, rotated into the target's
     frame by `-target.Direction`. This replaces `transform.InverseTransformDirection`.
  3. Apply `DamageSchematic(Damage, shape)` (`:277-314`, moved to `Entity`).

  - An active shield that `CanTakeHit` takes the hit instead. Every effect repeats that
    rule today (`Projectile.cs:60-71`).
  - `Damage` stays raw. `DamageCurve` still feeds only DPS estimates.

**Targeting system (new subsystem).**
- `TargetingSystemData : BehaviorData` takes union 39. A `TargetingSystem` behavior sits
  on a `GearData` item, mounted per fork T.
- Its stats are `PerformanceStat`, so lot quality reaches them through
  `EquippedItem.Evaluate`, as it does for weapon stats:
  - `Accuracy` (0..1): the ceiling on hit probability.
  - `Resolution` (detection threshold..1): the info level at which sensor state stops
    limiting hits.
  - `Precision` (0..1): the chance a hit lands on the aimed item.
  - `Tracking`: how much deviation it forgives. Only under fork F (c).
- Energy and heat come from the existing `EnergyDraw` and `Heat` behaviors on the item.
- An entity with no targeting system, or with its system offline or destroyed:
  - fires with `GameplaySettings.UnaidedAccuracy`, `Resolution = 1` and `Precision = 0`;
  - can still select a revealed item, but its hits scatter.
- A schematic hit on the targeting system degrades fire.

**Target item: one selection path.**
- `Entity.TargetItem : ReactiveProperty<EquippedItem>` sits beside `Target`
  (`Entity.cs:40`).
- `bool Entity.TrySelectTargetItem(EquippedItem)` is the only writer. It accepts null, or
  an item of `Target.Value` that `IsRevealed`.
- Changing `Target` nulls it.
- Three callers use it:
  - the player, from a select/cycle command beside `ActionGameManager.cs:366-397`; the
    schematic HUD shows revealed items, which is presentation;
  - the AI, from `CombatState`;
  - the playground, from `aim`.

**Per weapon kind.**
- `InstantWeapon` family:
  - `Execute` (`InstantWeapon.cs:181-191`) calls `FireControl.Fire` for each burst shot.
  - `OnFire` becomes `event Action<ShotOutcome>`.
  - Charge multipliers already fold into `Damage` and `Spread`.
- Guided weapons (`GuidedWeaponData`, `LauncherData`) use the same roll.
  - Lock time survives as the existing `LockWeapon.CanFire` gate.
  - Flight is presentation.
- Lightning (`plight`) makes one single-target roll. `HitRadius` is presentation.
- `ConstantWeapon` rolls once per `GameplaySettings.BeamResolveInterval` for
  `Damage * interval`, raised as `OnBeamShot(ShotOutcome)`. That is about 10 lines, and
  no design uses it yet.
- Deferred, because no design needs them: airburst splash, MIRV splits, and `Mine`.
  - The earlier draft's `ApplySplash` is not added.
- `TractorBeam` is not a hit. It stays Unity physics.

**AI and turrets.**
- `Combat.cs:100-102`:
  - `shouldFire = FireControl.HitProbability(testWeapon, target) >= Settings.AgentMinHitProbability`.
  - The `HardpointTransforms` read is deleted.
  - `AgentMinHitProbability` replaces `AgentFiringMinDot`, which nothing reads.
  - `:84-98` (looking at the predicted intercept) survives only under fork F (c).
- Subsystem policy: through `TrySelectTargetItem`, select the revealed target `Weapon`
  item with the highest `RangeDamagePerSecond(range)`, or null if there is none.
- `TurretController.cs:80-83` uses the same gate and threshold.
- `HardpointTransforms` is deleted everywhere:
  - `Entity.HardpointTransforms` (`Entity.cs:52-53`);
  - its writer, `EntityInstance.cs:514-517`;
  - its reader branch in `Behavior.Direction` (`Behaviors.cs:28-35`).
- `Direction` becomes hull-relative item rotation in both lowerings.
- `Barrels` stay in Unity for `GetBarrel`.

**Death, loot and pickup** (carried from the earlier draft).
- `Zone` subscribes to the `Death` of each entity it adds. On death it:
  - removes the entity and its agent;
  - drops each non-hull item with probability `LootDropProbability`, and all cargo,
    using `ItemManager.Random`;
  - adds the drops to
    `Zone.Loot : ReactiveCollection<LootDrop { ItemInstance Item; float3 Position; float2 Velocity }>`.
- `LootDropProbability` and `LootDropVelocity` move from `GameSettings.cs:19-20` to
  `GameplaySettings` (fork S).
- `Run.Died` reads no zone membership, so subscription order does not matter.
- `bool Zone.TryPickUp(Entity, LootDrop)` stores the item in the first bay that accepts
  it and removes the drop. Contact detection is fork L.

**Unity becomes presentation.**
- `InstantWeaponEffectManager.Fire` (`:7`) takes the `ShotOutcome`.
  - Effects fly to the rolled cell's world point, or to a miss offset, and play the
    impact on arrival.
  - They apply nothing.
- Hit loops, `SendHit` and `TakeHit` are deleted from the four used effects:
  - `Projectile.cs:57-93,99-112`;
  - `GuidedProjectile.cs:152-189`;
  - `Laser.cs:40-66`;
  - `Lightning.cs:34-52`; its endpoint capture stays as a visual.
- `HullCollider.cs:13-41,48-65`: `Hit`, `Splash`, `SendHit`, `SendSplash` and their
  argument classes are deleted. `OnCollisionEnter` (ship collision) stays, deferred.
- `EntityInstance.cs`:
  - `:277-396` is deleted;
  - `:405-437` keeps only the destroy effect.
- `ZoneRenderer.DropItem` lowers `Loot.ObserveAdd`.
- `ShieldManager.cs:25-41` calls `Zone.TryPickUp`. `Loot.ObserveRemove` destroys the
  pickup.

**Catalog and loadouts.**
- Add two targeting-system designs, 1-cell and 2-cell.
  - Each has products from at least the manufacturers that sell the capacitor.
  - The capacitor is the existing required interior item, so this lets `IsAvailable` find
    one in every galaxy.
- `LoadoutGenerator.FillInterior` (after `:250-260`) picks one with `required: true` for
  every entity that has `Weapons`:
  - the starting LonginusX, which uses the same generator;
  - NPC ships and turrets.

  Zenith gets none.
- Authoring follows the Cut C precedent:
  - a Hands scratch console upserts the records;
  - Unity and CultCache Studio are closed;
  - whether Studio renders union 39 is an operator check.
- **This lands after provenance Cut C.** Cut C re-upserts every `ItemData` and deletes
  `ItemData.Manufacturer`.
- `AetherDb` captures before and after:
  - `census`: 2 more designs;
  - `loadout 1`: 1 more interior item per armed entity;
  - `dangling`: 0;
  - on reopen: 0 schema reports.

**Tests** (`FireControlTests`, `ZoneDeathTests`). Each is paired with the mutation that
must kill it:

| Test | Asserts | Mutation that must kill it |
|---|---|---|
| `RollsAreSeeded` | two identical zones, same seed, 50 shots: identical `ShotOutcome` sequences | `FireControl` draws from `new Random()` |
| `ProbabilityFollowsInputs` | `p` is 0 below detection and outside range; rises with info up to `Resolution`; falls with `Spread`; is capped at `UnaidedAccuracy` with no system | drop `pSensor`; drop the range gate |
| `AimedHitLandsOnSelectedItem` | with `Precision` 1 and `p` 1, only the aimed item's durability falls | the roll ignores `Aimed` |
| `SelectionNeedsReveal` | below the item's tier `TrySelectTargetItem` is false; above it, true; a target change nulls it; info decaying below the tier makes `Aimed` null | `IsRevealed` returns true; the `Target` change skips the null |
| `RevealOrder` | hardpoint items reveal before interior items, larger before smaller | ordering dropped |
| `HardpointHitDamagesItemThenHull` | damage above armor plus item durability zeroes the item, and the remainder hits the hull | armor not subtracted; hull skipped |
| `ShieldTakesHit` | an active shield fixture absorbs the hit; the schematic is untouched | shield branch removed |
| `CombatStateKillsHeadless` | a `Minion` with a targeting system and a target in range steps headless without throwing and lowers target durability | restore `Combat.cs:101`; the threshold compares against 2 |
| `NpcDeathRemovesEntityAndAgent` | entity and agent are gone after death | removal omitted |
| `LootDropsAreSeeded` | same seed, identical drop lists | the drop roll uses a clock RNG |
| `PickUpStoresAndRemoves` | the item is stored and the drop removed; a full bay keeps the drop | drop removed on failure |
| `ShotResolvesOnArrival` (F (b)/(c)) | no damage before `range / Velocity`; a target removed before arrival takes none | damage applied at fire time |

**Playground.** Cut 3's `hit` command is withdrawn (fork D).
- New command `aim <item#>|none`, and new fact `target.revealed`.
- `provenance-c.play` kills by fire:
  1. `target nearest`
  2. `pilot minion`
  3. `step <s>`
  4. `expect zone.entities == <n-1>`
  5. `pickup all`
  6. `expect cargo.count > 0`

  Hands fixes `<s>` after the first seeded run.
- In `lifecycle.play`, the pilot dies by parking unarmed at an armed NPC and stepping
  until `expect ended == true`.

**Verification.**
- The headless build and tests are green, and each mutation above goes red.
- Two playground runs give byte-identical traces.
- These negative greps are each empty:
  - `rg -n "HardpointTransforms" Assets/Scripts`
  - `rg -n "SendHit|SendSplash|DamageSchematic|TakeHit\(|Durability\s*[-+]?=" Assets/Scripts --glob '!ServerShared/**'`
  - `rg -n "Physics\." Assets/Scripts/Gameplay/Weapons Assets/Scripts/Gameplay/HullCollider.cs`
  - `rg -n "Random\.value|onUnitSphere" Assets/Scripts/Gameplay/EntityInstance.cs`
  - `rg -n "Entities\.Remove" Assets/Scripts --glob '!ServerShared/**'`
- Unity batchmode with the editor closed (`docs/item-provenance-cut.md` §6) reports no
  `error CS`.
- Operator checks in Unity:
  - shots show rolled impacts and misses;
  - damage matches the HUD;
  - reveal and selection work;
  - kills drop loot, and pickup stores it;
  - the tractor beam still pulls.

**Risks.**
- The Tractor Beam can take LonginusX's only Sensors slot; that NPC never sees or fights.
  Check `loadout` over several seeds. The fix is data or `EquipHardpoints`, not fire
  control.
- Balance numbers (`UnaidedAccuracy`, `SchematicCellSize`, stat ranges) are first guesses;
  the playground is the tuning harness.

**Ledger.** `EntityInstance` about -155 +8; `HullCollider` -35; the four used effects
-75 +35; `ShieldManager` -8 +3; `Combat` -5 +8; `TurretController` ±4; `Behaviors` -8;
`Entity` -2 +70; `InstantWeapon`/`ConstantWeapon` ±15; `FireControl.cs` +160;
`TargetingSystem.cs` +45; `Zone` +50; `LoadoutGenerator` +12; `Settings` +5 -1; tests
+320; catalog 2 designs plus products. Fork E (b) deletes a further ~560. Net production
about +80, or -480 with E (b). It buys the invariant: combat resolves without Unity and
every hit rule has one owner.

**Cut 2 forks**, ranked by what they block:

**F. When a shot resolves.** Blocks the `FireControl` API and the tests.

**Ruled (c) (operator, 2026-09-17: "I like c"):** the shot resolves on arrival, and its hit
chance falls with how far the target deviated from the predicted intercept.

- The scope doc says projectiles "resolve on arrival against how far the target
  deviated". The ruling names only weapon, targeting and sensor inputs, and does not
  settle this either way.
- (a) Roll and apply at fire time. The smallest change; visuals arrive late, and
  maneuvering does not evade.
- (b) Roll at fire time, and apply after `range / Velocity` from a pending queue in
  `Zone`. Visuals and damage agree, but there is still no evasion.
- (c) (b), plus a deviation factor at arrival:
  `pDeviation = saturate(1 - distance(target, predicted intercept) / (hullRadius + Tracking))`.
  It keeps the scope doc's evasion and the look-at-intercept code, for about 25 more
  lines.
- **Recommend (c).** It is the scope doc's standing design, and it fits the ruling as one
  more factor. If "just roll the dice" meant dropping evasion, take (b).

**A. Firing arc.** Blocks `CombatState` and how the player's ship feels.

**Ruled (operator, 2026-09-17): per-hardpoint arcs.** "Firing arcs are determined per
hardpoint, that data is unfortunately baked into the ship prefabs at the moment". None of
(a)-(c) as written. The arc data has to become headless-readable hull data; being re-mapped.
Refined the same day: "I'd say default to 120 degrees, use existing item rotation direction
to determine the mount direction, like how the reaction thrusters work". A weapon's mount
direction is its equipped item's `ItemRotation`, as in `Thruster.cs:61-63`, and its arc is
120 degrees wide by default. Prefab firing points become presentation only.

- (a) No arc; facing is presentation.
- (b) Gate on `dot(weapon.Direction, toTarget) >= FireArcDot`, one authored setting. The
  ship must roughly face its target, and player and AI share the gate.
- (c) The arc as a probability factor.
- **Recommend (b), with a wide arc.** It keeps piloting meaningful without precision
  aiming. (a) is right if articulated turrets are meant to cover every hardpoint.

**T. Where the targeting system mounts.** Blocks the catalog and loadout work.
- (a) Interior `GearData` (`HardpointType.Tool`), placed by `FillInterior` like the
  capacitor. No hull or prefab edits.
- (b) A behavior on the control-module designs (`Cockpit 2x2`, `Turret Control Module`).
  No new item, but then it is not a separate subsystem to hit or refit.
- (c) The Sensors hardpoint. It competes for the one sensor slot.
- **Recommend (a).**

**V. Reveal ordering.** Blocks `RevealOrder` only; a default exists.
- (a) Hardpoint-mounted, then size, then index. Static, so reveal does not flicker.
- (b) Rank by each item's current share of `VisibilitySources`, so emitters reveal first.
  Reveal flickers as emissions change.
- (c) Reveal every item at `TargetGearInfoThreshold`, with no ranking.
- **Recommend (a).** Emissions already drive how fast info accumulates.

**E. The six effect families no design uses.** Blocks only the Unity-side ledger.
- (a) Strip their hit loops and keep them.
- (b) Delete their scripts, managers and prefabs once a GUID grep shows no catalog or
  scene reference. That covers `ConstantLaser*`, `ConstantLightning*`,
  `ConstantParticleWeapon*`, `Hitscan*`, `Mine*`, and `ShieldManager`'s mine branch
  (`:42-45`).
- **Recommend (b).** About 560 lines that serve nothing.

**L. Loot contact.** Low-blocking.
- (a) `Zone.TryPickUp` is the one commit.
  - Contact detection stays Unity collision, per the scope doc's deferral.
  - The playground's `pickup all` matches drops by spawn position within
    `DockingDistance`.
- (b) `Zone` owns drop motion and proximity pickup every step, and Unity renders it. That
  pulls `TractorBeam` headless too.
- **Recommend (a).** Say plainly that contact has two detectors until the tractor beam
  moves.

### Cut 3: `AetherDb play`

- `AetherDb.cs`: `Open` takes `dataDir`. Run and player paths come from it. It defaults
  to `GameData` for the existing commands and is required for `play`.
- `Program.cs`: `case "play": return Play(args)`.
  - Usage: `play <scenario-file|-> --data <dir> [--seed <u>]`.
  - `play` refuses a `--data` that resolves to `<root>/GameData`.
- New `tools/AetherDb/Playground.cs`. It is a line interpreter over `Run` with no
  simulation rules of its own. One command per line; `#` starts a comment.

| Command | Calls |
|---|---|
| `new [tutorial] [galaxy-seed=<u>]` | `Galaxy(...)` from authored settings, then `Run.New` |
| `continue` | `Run.Continue`; fails the scenario when null |
| `reopen` | dispose cache, `AetheriaStores.Open` again (quit/relaunch; the layer where §2.6 shows) |
| `step <seconds>` | `Run.Step(1/60)` repeated |
| `move <x> <y>` / `look <x> <z>` / `tractor <0..1>` | pilot entity fields, as `ActionGameManager.cs:1189-1216` |
| `target nearest\|none\|<entity#>` | `Pilot.Target.Value` |
| `fire <group> on\|off` | `WeaponGroups[g].weapons` Activate/Deactivate, as `ActionBarSlot.cs:190-204` |
| `gear <equipment#> on\|off` | `IActivatedBehavior` Activate/Deactivate |
| `pilot manual\|minion\|patrol\|goto station <i>\|goto wormhole <i>` | `Run.PilotAgent` |
| `dock` / `undock` / `board <entity#>` / `warp` / `tow` | `Run.TryDock` / `Undock` / `Board` / `TryEnterWormhole` / `Tow` |
| `hit <entity#> <damage> [hardpoint <k>]` | `Entity.ApplyHit` (Cut 2) |
| `pickup all` | `Zone.TryPickUp` for drops within `DockingDistance` |
| `save` | `Run.Save(empty)` |
| `expect <fact> <op> <value>` | facts: `ended`, `zone`, `docked`, `pilot.hull`, `zone.entities`, `run.savedgame`, `lots.absent`, `lots.mismatched`, `cargo.count` |
| `state` | trace one state line |

  - `goto` needs `MoveToPositionState : MoveToState` in `MoveTo.cs` (about 6 lines),
    wrapped in a one-state `Agent`. It is the only new agent code.
  - The trace goes to stdout, one line per step command and per `Run` event:
    `t=<zone time> zone=<name> <event> k=v…`. `state` adds pilot position, velocity,
    hull, target, docked, entity and agent counts, lot count and `run.cc` bytes.
  - An exception prints its type, message and first frame, then ends the scenario.
  - Exit code is failed expectations plus escaped exceptions.
  - `lots.absent` and `lots.mismatched` reuse the checks in `Program.cs:399-408`. They
    are extracted to one method that `save` also calls, not copied.
- First smoke, `tools/AetherDb/scenarios/lifecycle.play`, checked in:

```
new galaxy-seed=42
state
move 0 1
step 5
pilot goto station 0
step 60
dock
expect docked == true
undock
pilot goto wormhole 0
step 60
warp
step 5
expect run.savedgame == true
reopen
continue
expect zone == <the warp target, printed by the trace>
expect lots.absent == 0
expect lots.mismatched == 0
target nearest
pilot minion
step 30
hit pilot 100000
expect ended == true
expect run.savedgame == false
```

  - Hands fixes the literal zone name after the first run. The scenario checks that it
    reproduces, not what it is.
- Verification:
  - `dotnet run --project tools/AetherDb -- play tools/AetherDb/scenarios/lifecycle.play --data <scratch>`
    exits 0.
  - Running it twice gives byte-identical traces.
  - Mutation: revert Cut 0, and the scenario exits non-zero at `continue` with the
    `int2` message.
  - Mutation: `Run.Died` omits `Clear`, and `expect run.savedgame == false` fails.
  - Negative: `rg -n "RunSave\.|ZoneGenerator|\.Position\s*=|Durability\s*=" tools/AetherDb/Playground.cs`
    is empty.
- Ledger: `Playground.cs` about +220; `AetherDb.cs` ±4; `Program.cs` +3 and ±10
  (extraction); `MoveTo.cs` +6; scenario +26. No new target.

### Cut 4: consumer scenarios

These are scenario files only; no code.
- `cultcache-smoke.play` covers migration smoke steps 3-8 minus the rendering and UI
  parts:
  - new, warp, warp again with the `SavedZone` count constant (`expect run.savedzones == <n>`, a new fact);
  - reopen and continue on the same zone and entity;
  - reopen before any step and check every visited zone still holds contents;
  - die, then the run is cleared.
  - Steps 1-2 (`player.cc` from settings UI), 3's input screen and 9 (loadout UI) stay
    operator-only.
- `provenance-c.play` covers the Cut C checks:
  - new; kill an NPC with `hit`; `pickup all`;
  - `expect cargo.count > 0`;
  - warp; reopen; continue;
  - `expect lots.absent == 0`, `expect lots.mismatched == 0`;
  - die; `expect run.savedgame == false`.
  - Tier colour, Manufacturer row, trade purchase and Studio rendering stay
    operator-only.

## 5. Forks

**R. Reopen defect.** Blocking: every Continue and two HEAD tests.
- (a) `PersistedBehaviors` as a pairs array. This is local, matches the sibling pack
  fields, and saves are discarded anyway.
- (b) CultLib: a hash-resistant comparer for CultMath value types in
  `CultMessagePackSecurity`. That is a foundation change with a new pin and a Unity
  package release, and it keeps a map for 0-12 entries.
- (c) Key persisted behaviors by equipment index. It saves bytes but couples to
  equipment order, which `Pack` does not promise.
- **Recommend (a).**

**P. Where the playground lives.**
- (a) `AetherDb play`. It already has settings, stores and the save checks.
- (b) Scenarios only as xunit tests. They cannot use authored settings without dragging
  YamlDotNet into tests, and agents cannot puppeteer interactively.
- (c) A new `tools/AetherPlay` executable, which duplicates store and settings loading
  for no second consumer.
- **Recommend (a),** with lifecycle rules tested in xunit (Cut 1 and 2 tests) and
  sessions in `play`.

**H. How `CombatState` decides to fire.**
- (a) Read `Weapon.Direction`, the same reader the shot uses.
- (b) A ServerShared articulation model owning barrel aim, with Unity rendering it. That
  is correct under gimbals but a new subsystem with no playground need yet.
- (c) The playground writes `HardpointTransforms`. Rejected: a second writer
  compensating for a missing owner.
- **Recommend (a).**
- **Superseded by the fire-control ruling (Cut 2).** Nothing reads barrel aim for
  gameplay. `CombatState` fires when `FireControl.HitProbability` clears
  `AgentMinHitProbability`, and `HardpointTransforms` is deleted. Fork A (firing arc)
  replaces this question.

**D. How far combat goes headless.**
**Ruled (operator, 2026-09-17): none of (a)-(c).** Hit detection goes away entirely, as
`docs/three-gates-scope.md` "Fire control" already planned: "We wanted to get rid of hit
detection anyway, remember? Just roll the dice based on weapon stats, targeting system
stats (new subsystem) and sensor state." Cut 2 is re-mapped as fire control in
`ServerShared`: a hit is a roll over weapon stats, the stats of a new targeting-system
subsystem, and sensor state. The playground's `hit` stand-in is not needed.
A second ruling the same day adds subsystem aim: "We want the player as well as the AI to be
able to aim for specific subsystems once it has gathered enough target data to reveal those
subsystems".

Consequences:
- Cut 2 is fire control plus the parts of (a) that survive: `DamageSchematic` moves to
  `Entity`, and death removal, loot roll and the pickup commit move to `Zone`.
- Effects and `HullCollider` stop deciding damage.
- (b)'s "two truths about hits" cannot arise, because Unity detects nothing.
- Cut 3's `hit <entity#>` command and its §3 forbidden-writer exception are withdrawn.
  Cut 3's command table, `lifecycle.play` (`hit pilot 100000`) and Cut 4's
  `provenance-c.play` ("kill an NPC with `hit`") still name it. They are to be re-mapped
  to fire (`target`, `aim`, `fire`/`pilot minion`, `step`) per Cut 2's Playground note.
- §7's "`hit` is not ballistics" risk becomes "rolled balance is not tuned".
- New forks F, A, T, V, E and L live in Cut 2. Fork H is superseded.

Options as mapped before the ruling:
- (a) Move damage application, death removal, loot roll and pickup into ServerShared;
  Unity keeps hit detection; the playground's `hit` supplies hits.
- (b) (a) plus headless ballistics. That is a projectile simulation Unity would have to
  adopt, or else two truths about hits. It needs its own map.
- (c) Nothing moves; the playground's fights stop at `OnFire`, and the Cut C
  kill-and-loot check stays operator-only.
- **Recommend (a).** The operator asked for fight and loot. Without (a) or (b) the
  playground cannot kill anything by the rules the game uses.

**S. The two Unity-only lifecycle settings.**
- (a) Move `StartingHullName`, `WormholeDistanceRatio` and (Cut 2) `LootDrop*` into
  `GameplaySettings`, with a text edit of `Settings.asset` while Unity is closed.
- (b) Pass them as `Run` constructor arguments read from `GameSettings`; the playground
  reads them by name from YAML. That leaves two readers of one authored value.
- (c) A new `RunRules` bundle, which is a type with no owner the existing settings class
  lacks.
- **Recommend (a).**

**N. Is the scene handoff static acceptable?** Low-blocking.
- (a) `ActionGameManager.NewRun : (Galaxy, bool)?`, consumed once by `Start`.
- (b) `MainMenu` builds the whole `Run` on its generation thread. That moves zone
  generation's run-store upserts off the main thread, and the `CultCache` threading
  contract is not checked here.
- **Recommend (a).**

## 6. Subtraction ledger

| Cut | Removed | Added | Targets, schemas, stores |
|---|---|---|---|
| 0 | `Dictionary<int2,…>` field and map-building pack code, ~6 | pairs pack/unpack ~6; test ~25 | `EntityPack` key 5 wire shape map -> array; no new schema |
| 1 | `ActionGameManager` ~190 (incl. dead `IntroCutscene` 28), 2 statics, `MainMenu` 4, `ZoneRenderer` 9, `InventoryPanel` 2, clock seed | `Run.cs` ~190; `Zone` +10; AGM handlers ~45; 1 static; tests ~200 | 0 targets; `Settings.asset` two keys move |
| 2 | `EntityInstance` ~155, `HullCollider` ~35, used effects' hit loops ~75, `HardpointTransforms` and its readers ~15, `AgentFiringMinDot`; fork E (b) a further ~560 | `FireControl.cs` ~160; `TargetingSystem.cs` ~45; `Entity` ~70; `Zone` ~50; effects ~35; `LoadoutGenerator` ~12; tests ~320 | 0 targets; `BehaviorData` union 39; 2 catalog designs plus products (after provenance Cut C); `LootDrop*` keys move; `SchematicCellSize`, `UnaidedAccuracy`, `BeamResolveInterval`, `AgentMinHitProbability` added |
| 3 | extracted duplicate of the `save` lot check | `Playground.cs` ~220; `MoveTo` +6; scenario 26 | 0 targets; `AetherDb` gains one command |
| 4 | — | 2 scenario files | — |

Net production code across Cuts 0-3 is about +340, or about -220 if fork E (b) deletes the
unused effects. Tests are about +540. No executable, package, daemon or store is added.
Rules move from MonoBehaviours to ServerShared. The genuinely new behavior is:
- `MoveToPositionState` and the scenario interpreter;
- fire control's roll, which replaces physics hit detection per the operator's ruling;
- subsystem reveal and selection;
- the targeting-system item.
In exchange, Continue works again (Cut 0), and agents can falsify lifecycle, save and
provenance claims without the editor.

## 7. Risks

- **Unity batchmode is the only proof for Cuts 1-2's `Assembly-CSharp` edits.** It needs
  the editor closed. The operator's in-editor play remains the proof for the lowering
  (cameras, menus, action bar, pickup visuals).
- **`Zone` subscribing `Death` changes NPC death timing in Unity.** Removal now happens
  inside the damage call rather than in `EntityInstance`'s subscriber. The operator's
  fight check covers it.
- **Rolled combat is untuned.** A passing scenario proves that the roll, damage, death,
  loot and save follow the rules. It does not prove the rules are fun. The balance numbers
  in Cut 2 are first guesses.
- **Cut B's recorded green is contradicted by §2.5.** Soul should rerun the suite before
  trusting any provenance verification that reopens a store.
