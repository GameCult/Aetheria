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
- No headless ballistics. Whether a shot geometrically hits stays with Unity physics
  (fork D).
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
    membership directly. Its `hit` command goes through the damage owner (fork D).
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
| `UnityEngine.Random` | `MainMenu.cs:125,148` (background noise), `EntityInstance.cs:413-428` (loot) | noise stays in the menu as a galaxy-generation input; loot roll moves to ServerShared on `ItemManager.Random` (fork D) |
| Clock RNG | `ItemManager.cs:20` | cut; the seed is a constructor argument, and Unity passes a clock-derived one |
| Coroutines | `IntroCutscene` | deleted (dead) |
| UniRx | `Observable.NextFrame` in the menu, `EveryUpdate` in `Die` | stay in Unity; `Run` uses C# events, and `Entity.Death` (UniRx, headless-compiled) is what `Run` subscribes to |
| `Debug.Log` | `:264`, `:657`, `MainMenu` | injected `Action<string>` |
| Scene load | `MainMenu` | stays; the handoff static becomes `ActionGameManager.NewRun : (Galaxy, bool)?`, command-only, consumed once in `Start`; null means Continue |
| MonoBehaviour state | `_currentEntity`, `DockedEntity`, `DockingBay`, `TowingStation`, `Zone`, `ItemManager` | projections of `Run` |
| Barrel transforms | `EntityInstance.cs:516` -> `HardpointTransforms` | render-owned aim; `CombatState` stops depending on it (fork H) |

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

### Cut 2: combat and death resolve without Unity (forks H, D)

- `Combat.cs:101`: `shouldFire` reads `testWeapon.Direction`, the same reader shots use
  (`Behaviors.cs:24-43`). Its fallback is hull-relative item rotation; with Unity
  running, the articulated barrel transform wins as today.
- Damage owner: `EntityInstance.cs:276-314` (`DamageSchematic`) and `:330-395`
  (hit-shape) move to `Entity`:
  - `void ApplyHit(HullHit hit)` with `HullHit { Damage, Penetration, Spread, DamageType, Source, HullUv, LocalDirection }`;
  - `void ApplySplash(float damage, DamageType type, Entity source, float2 localDirection)`.
  - The only Unity-specific step, `transform.InverseTransformDirection`, becomes a
    rotation by `Ship.Rotation` (`Ship.cs:71`) at the caller. `HullCollider` subscribers
    call these methods.
- Death owner: `Zone` subscribes each entity's `Death` when it is added. On death:
  - remove the entity from `Entities` and its agent from `Agents`;
  - roll drops with `ItemManager.Random` against `LootDropProbability`. That field moves
    from `GameSettings.cs:19` to `GameplaySettings`, together with `LootDropVelocity`.
  - Keep `Zone.Loot : ReactiveCollection<LootDrop { ItemInstance Item; float3 Position; float2 Velocity }>`.
  - `EntityInstance.cs:405-437` keeps the destroy effect only. `ZoneRenderer.DropItem`
    lowers `Loot.ObserveAdd`.
- Pickup owner: `bool Zone.TryPickUp(Entity, LootDrop)` stores the item in the first
  cargo bay that accepts it and removes the drop. `ShieldManager.cs:29` calls it.
- Tests:

| Test | Asserts | Mutation that must kill it |
|---|---|---|
| `CombatStateFiresHeadless` | a `Minion` ship with a target in range steps 10 s headless without throwing and raises at least one `OnFire` | revert `:101` |
| `HardpointHitDamagesItemThenHull` | a hit on hardpoint index k with damage greater than armor plus item durability lowers that item's durability to 0 and the hull by the remainder | armor not subtracted; hull skipped |
| `NpcDeathRemovesEntityAndAgent` | after hull death the entity and its agent are gone from the zone | removal omitted |
| `LootDropsAreSeeded` | two zones from the same seed drop identical item lists | roll uses a clock RNG |
| `PickUpStoresAndRemoves` | the item lands in cargo and the drop is gone; a full bay leaves the drop | drop removed on failure |

- Verification:
  - Headless green; Unity batchmode has no `error CS`.
  - Negative: `rg -n "Random\.value|onUnitSphere" Assets/Scripts/Gameplay/EntityInstance.cs`
    matches only visual effects, not the drop decision.
  - Negative: `rg -n "Entities\.Remove" Assets/Scripts --glob '!ServerShared/**'` is empty.
  - Operator: a Unity fight still damages and drops loot.
- Ledger: `EntityInstance` about -110 +12; `Entity` +95; `Zone` +45; `ShieldManager` ±3;
  `Combat` ±1; tests +180. Net production about +40. It moves rules; it adds none.

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

**D. How far combat goes headless.**
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
| 2 | `EntityInstance` ~110 | `Entity` ~95; `Zone` ~45; tests ~180 | 0 targets; two settings keys move |
| 3 | extracted duplicate of the `save` lot check | `Playground.cs` ~220; `MoveTo` +6; scenario 26 | 0 targets; `AetherDb` gains one command |
| 4 | — | 2 scenario files | — |

Net production code across Cuts 0-3 is about +300. Tests are about +400. No executable,
package, daemon or store is added. Rules move from MonoBehaviours to ServerShared, and
the only genuinely new behavior is `MoveToPositionState` and the scenario interpreter.
In exchange, Continue works again (Cut 0), and agents can falsify lifecycle, save and
provenance claims without the editor.

## 7. Risks

- **Unity batchmode is the only proof for Cuts 1-2's `Assembly-CSharp` edits.** It needs
  the editor closed. The operator's in-editor play remains the proof for the lowering
  (cameras, menus, action bar, pickup visuals).
- **`Zone` subscribing `Death` changes NPC death timing in Unity.** Removal now happens
  inside the damage call rather than in `EntityInstance`'s subscriber. The operator's
  fight check covers it.
- **The playground's `hit` is not ballistics.** A scenario that passes proves damage,
  death, loot and save. It does not prove that shots connect in the game.
- **Cut B's recorded green is contradicted by §2.5.** Soul should rerun the suite before
  trusting any provenance verification that reopens a store.
