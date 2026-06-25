# Stage 7A Client Surface Inventory

Date: 2026-06-24

Stage 7A exists to stop the client migration from turning into Jenga. This is
the reviewed inventory for Unity, Electron, and daemon client-facing surfaces.
Stage 7D.1 refreshed the Unity half of this ledger before Unity parity code
movement began.

## Gate

Current gate: Stage 7D.4 Unity projection reroute.

Implementation runbook:

- `Aetheria.State/docs/stage-7-thin-client-staged-implementation-plan.md`

Stage 7A is complete. Stage 7D.1 reuses this artifact as the edit queue: every
remaining Unity-facing mutation and gameplay read/projection path must be
assigned to `7D.2 facade`, `7D.3 commands`, `7D.4 reads`, `7D.5 renderer`, or
`8 demolition`. Stage 8 Unity gameplay shell demolition may not start until
Stage 7E passes.

## Target Shape

```text
client input -> local typed Verse command document -> local daemon
local daemon state -> local typed projection/read -> renderer or UI panel
peer daemon facts -> local authority import -> local state
```

Forbidden as public client shape:

- `command(kind, payload)`;
- browser or Unity code constructing transport payloads directly;
- hand-encoded MessagePack array layouts in client code;
- remote gameplay viewport reads used as state authority;
- cached command-port facades or ad-hoc buses between input and the local Verse
  command document.

## Electron Mutation Surfaces

| Surface | Evidence | Current Shape | Decision | Stage |
| --- | --- | --- | --- | --- |
| Browser right-click move/target | `Aetheria.Rts.Web/Client/app.ts:478`, `:490` | Browser now calls typed `window.aetheriaRts.setTarget(...)` and `setMoveVector(...)`; it no longer constructs `{ kind: ... }` transport payloads. | Keep as temporary typed facade. Replace with shared generated TS Verse bindings when available. | 7B/7C |
| Preload command bridge | `Aetheria.Rts.Web/Electron/preload.cjs:5`, `:6` | Exposes typed `setMoveVector` and `setTarget` IPC calls. | Keep until the broader typed runtime facade replaces preload method wiring. | 7B |
| Electron IPC command handler | `Aetheria.Rts.Web/Electron/main.ts:98`, `:101` | Accepts `RtsSetMoveVectorRequest` and `RtsSetTargetRequest` and forwards to typed client methods. | Keep as Stage 7B shim. | 7B |
| TypeScript CultMesh command writer | `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts`; `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts`; `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` | Transport wrapper exposes typed methods; generated TS contract metadata owns schema ids, command kind ids, and MessagePack slot maps derived from the C# `[Key]` declarations, including the current 30-slot daemon command document. | Keep the semantic encode helpers here until the broader CultMesh TS typed document writer lands; do not reintroduce manual slot maps. | 7B |
| Daemon RTS command receive hook | `Aetheria.State.Daemon/Program.cs:454` | RUDP handler accepts raw command documents and submits them to the local node. | Quarantine for 7B, then replace or narrow. It can remain as an internal local typed document transport endpoint, but it must not define the public client API. | 7B |

## Electron Read And Projection Surfaces

| Surface | Evidence | Current Shape | Decision | Stage |
| --- | --- | --- | --- | --- |
| Browser viewport polling | `Aetheria.Rts.Web/Client/app.ts:166` | Browser requests `mapViewport` every 50 ms and treats the returned document as the main map projection. | Keep as named projection shim, but replace underlying remote viewport request with local typed projection reads. | 7B/7C |
| Preload viewport bridge | `Aetheria.Rts.Web/Electron/preload.cjs:4` | Exposes typed `mapViewport` projection request. | Keep until the broader typed runtime facade replaces preload method wiring. | 7B |
| Electron IPC viewport handler | `Aetheria.Rts.Web/Electron/main.ts:95` | Accepts typed viewport bounds and forwards to `AetheriaCultMeshClient.mapViewport`. | Keep as Stage 7B shim. | 7B |
| TypeScript CultMesh viewport reader | `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts`; `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts`; `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` | Transport wrapper asks for the named RTS viewport schema; generated TS contract metadata owns viewport slot maps while semantic decode helpers produce ergonomic UI types. | Replace the remote viewport request with local typed projection reads. | 7B/7C |
| Daemon RTS viewport hook | `Aetheria.State.Daemon/Program.cs:420`, `:501`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs` | Daemon snapshot hook now delegates viewport construction to the shared typed `AetheriaRuntimeRtsProjection` API. The hook still serves it over RUDP for Electron. | Keep only as temporary transport plumbing. Replace Electron's RUDP viewport read with local projection reads, then delete or narrow this hook. | 7B/7C |

## Unity Mutation Surfaces

| Surface | Evidence | Current Shape | Decision | Stage |
| --- | --- | --- | --- | --- |
| Runtime input settings commands | `Assets/Scripts/UI/InputScreen/InputDisplayLayout.cs` | `InputDisplayLayout` now owns an explicit `AetheriaClient` and submits typed input settings commands through the shared facade. The old static `ActionGameManager` input-settings ingress has been deleted. | Keep as the Stage 7D direct-panel pattern: panel owns presentation/input state, facade owns typed Verse submission. | 7D |
| Loadout restore | `Assets/Scripts/Gameplay/ActionGameManager.cs:1110`; caller at `Assets/Scripts/UI/Menu/InventoryPanel.cs:258` | Unity UI calls manager method for daemon-owned loadout operation. | Replace with typed client facade method. | 7D |
| Daemon operation submitter | `Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs:40`, `:44`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationClient.cs:49`, `:54` | Unity uses typed operation wrappers that open `AetheriaRuntimeVerseClient` and submit `AetheriaRuntimeDaemonCommandDocument`. | Keep as the semantic source for Stage 7B typed commands, but remove per-call client open churn when the shared runtime wrapper lands. | 7B/7D |
| Surface command helpers | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSurfaceCommands.cs:23`, `:37` | Eve/CultUI surface commands submit typed daemon operations. | Keep, but ensure surface command templates point at typed documents or typed state refs rather than generic payload maps. | 7D |

## Unity Read And Projection Surfaces

| Surface | Evidence | Current Shape | Decision | Stage |
| --- | --- | --- | --- | --- |
| Daemon observer polling | `Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs:130`, `:139` | Opens `AetheriaRuntimeVerseClient` and reads observed daemon state. | Keep. This is the closest current Unity surface to the target contract. | 7D |
| SoA native view mapping | `Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs:83`, `:153` | Maps daemon SoA view into `AetheriaDaemonRenderNativeView`. | Keep. This remains the rendering fast path. | 7D/8 |
| `ActionGameManager` frame lowering | `Assets/Scripts/Gameplay/ActionGameManager.cs`; `Assets/Scripts/Gameplay/AetheriaUnityObservedFrameApplier.cs` | Unity loop delegates `ApplyLatestZoneRender` to the observed frame applier, which reads the typed `ZoneRenderAsync()` facade. | Quarantine. Stage 7D may keep it as a shell; Stage 8 should delete or reduce it once Unity renders from projection/native views. | 7D/8 |
| Observed Galaxy facade | `Assets/Scripts/Gameplay/AetheriaUnityObservedRunProjection.cs`; created from typed sector-map boot in `Assets/Scripts/UI/MainMenu.cs` | Unity maintains an observed facade graph only as a legacy scene-construction adapter. `ActionGameManager` no longer exposes or stores it. | Quarantine for Stage 7D, demolish in Stage 8 where clients can use equivalent typed projections. | 7D/8 |
| `ZoneRenderer` frame projection | `Assets/Scripts/Zone Display/ZoneRenderer.cs:239` | Persistent GameObject projection cache lowers typed `zone_render`, viewport, and contact feeds. | Keep as presentation cache only until Stage 8. It must not become an authority or mirrored hierarchy owner. | 7D/8 |
| Menu reads from manager singletons | Examples: `Assets/Scripts/UI/Menu/SectorMap.cs:94`, `Assets/Scripts/UI/Menu/InventoryMenu.cs:411`, `Assets/Scripts/UI/Menu/TradeMenu.cs:530` | UI panels read catalog/settings/state through `ActionGameManager`. | Replace with shared client projection/state refs as panels are migrated. | 7D/8 |

## Shared Runtime Contract Surfaces To Keep

| Surface | Evidence | Decision |
| --- | --- | --- |
| `AetheriaRuntimeVerseClient` | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseClient.cs:153`, `:177`, `:508` | Keep and make it the shape both Unity and Electron mirror. TS needs generated/manual bindings for the same typed records. |
| Typed operation wrappers | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationClient.cs:7`; `AetheriaRuntimeDaemonOperationsClient` wrappers | Keep as semantic command catalog. Stage 7B should expose equivalent TS typed operations. |
| Render queries | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonRenderQueries.cs:645`, `:974`, `:1079`, `:1165` | Keep as projection internals. Unity consumes typed zone/body/contact/viewport rows; asteroid instance poses are now carried by `zone_render` instead of recomputed from snapshots. |
| Authority policy and health docs | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseAuthorityPolicy.cs:45`; `AetheriaRuntimeVerseClient.GetHealthAsync` at `:237`; generated TS metadata in `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` | Keep. Stage 7 client startup must show policy id, peer health, daemon endpoint, frame id/time, and import counters. |

## Ad-Hoc Bus Search Results

Reviewed search terms:

```text
command, viewport, Apply(command, payload, Queue, ConcurrentQueue, Channel,
Bus, EventBus, CommandPort, cached command, port
```

Actionable hits now:

- TypeScript raw CultMesh document put and snapshot request helpers.
- Daemon RTS command/viewport request handlers.

Retired hits:

- Electron IPC `aetheria-rts:command` and `aetheria-rts:viewport`;
- browser `window.aetheriaRts.command` and `window.aetheriaRts.viewport`;
- public `RtsCommandRequest` command union;
- `createCommandDocument`.

Non-actionable or already-quarantined hits:

- shader/material `Queue` metadata;
- ordinary import/export/transport wording;
- daemon peer endpoint option parsing;
- command receipt/import bookkeeping from Stage 6.

No current `CommandPort`, `CachedCommandPort`, `EventBus`, or public
`Apply(command, payload)` implementation was found in the scoped search.

## Stage 7D.1 Unity Edit Queue

Date: 2026-06-24

Stage 7D.0 verifier baseline passed after the Electron app-shell smoke was
hardened. Unity parity may now begin, but only through the following queue.

Reviewed source search:

```powershell
rg -n "ActionGameManager|AetheriaDaemonObserver|AetheriaRuntimeDaemonOperationClient|AetheriaRuntimeDaemonSurfaceCommands|ZoneRenderer|Rigidbody|Collider|Physics\.|SubmitDaemonCommand|CommandPort|CachedCommandPort|EventBus|Apply\(" Assets/Scripts Packages/org.gamecult.aetheria.state/Runtime -g "*.cs" -S
rg -n "ObservedGalaxy|LoadZone|ApplyDaemonFrame|GameObject\.Find|FindObjects|GetComponent<" Assets/Scripts -g "*.cs" -S
rg -n "Queue|Channel|ConcurrentQueue|Bus" Assets/Scripts Packages/org.gamecult.aetheria.state/Runtime -g "*.cs" -S
```

### 7D.2 Facade Candidates

Status on 2026-06-24: initial shared facade is in place.
`AetheriaClient` owns the local Verse client lifetime, exposes typed daemon
operations, and provides named local reads for map viewports, selected object,
inventory, daemon health, authority policy, and SoA view data.
`AetheriaDaemonObserver` now exposes that shared client instead of keeping a
separate operation client and Verse client. The remaining rows are either
carried into 7D.3 command reroute or 7D.4 projection reroute.

| Surface | Evidence | Current Shape | Stage 7D Action |
| --- | --- | --- | --- |
| Unity local daemon observer | `Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs` | Now owns a shared `AetheriaClient`, local observation cursor, and SoA render mapping. | Keep as Unity presentation shell. 7D.3 should move remaining command ingress through `observer.Client`/`observer.Operations`; 7D.4 should move read sites to named facade projections. |
| Runtime daemon operation client | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationClient.cs` | Typed command helper. The shared facade can now inject a submit delegate so sends reuse its long-lived Verse client instead of opening a new client per command. | Keep as typed command document builder behind `AetheriaClient`. Do not expose generic command APIs above it. |
| Generated operation catalog | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationsClient.cs` | Ergonomic typed wrappers over `AetheriaRuntimeDaemonOperationClient`; nullable observed state is now explicit for pre-frame command issue. | Keep as public typed operation vocabulary exposed through `AetheriaClient.Operations`. |
| Eve/CultUI surface commands | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSurfaceCommands.cs:8`, `:37`; catalog at `AetheriaRuntimeDaemonSurfaceCommandCatalog.cs:58` | Surface command ingress now routes through `AetheriaClient` and facade-backed daemon command document submission. | Keep as typed Eve ingress behind the facade. Do not let UI callers submit daemon documents directly. |
| Runtime Verse client submit | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseClient.cs:508`, `:528` | Canonical typed command document submission. | Keep as the low-level primitive used by the facade. Do not expose stringly command APIs above it. |

### 7D.3 Command Reroute Queue

| Surface | Evidence | Current Shape | Stage 7D Action |
| --- | --- | --- | --- |
| Input settings commands | `Assets/Scripts/UI/InputScreen/InputDisplayLayout.cs` | Panel now resolves and owns an `AetheriaClient`, then submits typed input settings values through `AetheriaClient.SubmitInputSettingsCommandAsync`. | Done for 7D.3. Use this as the pattern for remaining direct UI command reroutes. |
| Loadout template save/delete | `Assets/Scripts/UI/Menu/InventoryPanel.cs`; projection helper at `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeLoadoutSnapshotProjector.cs` | `InventoryPanel` owns loadout-template save submission through `AetheriaClient.SubmitLoadoutTemplateCommandAsync` and now asks `AetheriaClient.LoadoutTemplateAsync(entityKey)` to project the target daemon entity into a template. Save no longer serializes the Unity `Entity` facade. | Done for save. The remaining Unity loadout projector is renderer/bootstrap adapter code for constructing Unity presentation objects from saved templates, not the save path. |
| Loadout restore/current ship/name/hull toggles | `Assets/Scripts/UI/Menu/InventoryPanel.cs` | `InventoryPanel` owns an explicit `AetheriaClient` and submits `RestoreLoadout`, `SetDockedCurrentShip`, `SetEntityName`, and `ToggleHullConductivity` through `AetheriaClient.Operations`. The old manager request shims are gone; the panel uses the manager only to resolve observed Unity facade entities to daemon record keys and local price context. | Done for this dropdown/current-ship slice. Price/context logic that another runtime needs should move into daemon/state code before Stage 8. |
| Inventory drag/drop and double-click item transfer | `Assets/Scripts/UI/Menu/InventoryPanel.cs`; `Assets/Scripts/UI/Menu/InventoryMenu.cs` | Inventory panels/menus own explicit `AetheriaClient` instances and submit `TransferCargoItem`, `EquipItem`, and `StoreItem` through typed facade operations. `ActionGameManager` no longer exposes cargo/equipment transfer request shims; it only resolves observed Unity facade cargo/equipment objects to daemon record keys and indices. | Done for this transfer slice. |
| Trade purchases | `Assets/Scripts/UI/Menu/TradeMenu.cs` | `TradeMenu` owns an explicit `AetheriaClient` and submits typed `TradePurchase` operations for station stock, cargo purchases, commodity quantity purchases, and docked ship hull purchases. `ActionGameManager` no longer exposes trade purchase request shims. | Done for current trade purchase surface. Station-stock authoring/refit verbs still need first-class typed surfaces where they are not already represented by trade commands. |
| Equipped-item details and action-bar binding commands | `Assets/Scripts/UI/Menu/InventoryMenu.cs`; `Assets/Scripts/Gameplay/ActionBarSlot.cs` | `InventoryMenu` submits equipped-item override shutdown, thermotoggle target temperature, weapon-group membership, action-bar bind, and action-bar clear through its explicit `AetheriaClient`. Restored action-bar bindings hold an explicit `AetheriaClient` and submit consumable, behavior-active, and weapon-group-active typed operations. `ActionGameManager` no longer exposes public request shims for these menu commands; it only resolves observed item identity and action-bar control paths. | Done for current detailed item/action-bar surface. Drag-to-action-bar still originates in the manager because drag registration currently lives there, but it submits through the shared facade rather than a daemon shim. |
| Runtime target/control commands | `Assets/Scripts/Gameplay/ActionGameManager.cs` plus `AetheriaDaemonObserver.Operations` | `ActionGameManager` remains the local input gesture router for movement, look, tractor power, target selection/cycling, reticle targeting, override shutdown, sensor ping, heatsinks, shields, interact, tow, dock, and undock. It now submits those through a shared typed facade operation helper instead of private daemon shim methods. | Done for current pilot input ingress. Stage 8 should contract this to input orchestration only after equivalent projections replace manager-owned reads. |
| Starbridge commander and pilot verbs | Starbridge design target in `E:/Projects/AetheriaLore/Aetheria/Game Design/Aetheria Starbridge.md` | Station stock, docking/refit, salvage, construction anchoring, target marks, cooling, repair, drone/turret orders, infrastructure placement, fabrication, wave/hostile control, and survival-pod recovery are not all first-class command surfaces yet. | Add them only as typed daemon/state operations or typed projections. Unity may issue pilot-local intent; RTS may issue commander intent; neither client may own bespoke gameplay behavior for these verbs. |
| Menu startup and Eve commands | `Assets/Scripts/UI/MainMenu.cs:20`, `:461` | Main menu now owns an `AetheriaClient`; frame/settings reads and known Eve command submission route through the facade. Client-target file edits still use the typed local target utility. | Keep as facade-backed UI shell. Stage 8 should delete the menu-owned compatibility shims once panels can hold explicit client references. |
| Eve surface presenter | `Packages/org.gamecult.aetheria.eve-runtime/Runtime/AetheriaEveSurfacePresenter.cs` | Presenter now owns an `AetheriaClient`; daemon Eve surface reads, state-ref resolution, and surface command submission use the facade or file-backed state reader. | Keep as Unity presentation infrastructure. It must remain render/input plumbing, not gameplay authority. |

### 7D.4 Projection And Read Queue

| Surface | Evidence | Current Shape | Stage 7D Action |
| --- | --- | --- | --- |
| Observed daemon state polling | `Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs:74`, `:120` | Polls local Verse state and emits changed observations. | Keep as local read primitive, but expose named typed projections through 7D.2 facade rather than leaking raw observed state to gameplay shell callers. |
| Observed galaxy facade | `Assets/Scripts/Gameplay/AetheriaUnityObservedRunProjection.cs`; booted from typed sector-map state in `Assets/Scripts/UI/MainMenu.cs` | Unity builds a projected Galaxy facade only as a legacy scene-construction adapter. `ActionGameManager` no longer stores or exposes it. | Quarantine for Stage 7D. Replace reads with local projection/state access where possible. Delete or collapse in Stage 8 after equivalent projections exist. |
| Runtime catalog/settings reads | scoped search across `Assets/Scripts` and `Packages/org.gamecult.aetheria.state` | No direct `ActionGameManager.RuntimeCatalog` or `ActionGameManager.RuntimePlayerSettings` reads remain. `InventoryMenu`, `InventoryPanel`, `TradeMenu`, `ActionBarSlot`, `ZoneRenderer`, `EntityInstance`, `ShipInstance`, `InputDisplayLayout`, `VolumeCloudRenderer`, `MainMenu`, and `SchematicDisplay` have moved their local catalog/formatting/settings reads to explicit `AetheriaClient` instances or a renderer-owned client cache. | Done for direct manager-global catalog/settings cleanup. Continue replacing observed-galaxy/entity facade reads with portable typed projections. |
| Map screen zone/settings reads | `Assets/Scripts/UI/Menu/MapRenderer.cs` | `MapRenderer` now owns an explicit `AetheriaClient` and reads zone title from `ObjectsViewportAsync` plus minimap asteroid visibility from `PlayerSettingsAsync`. The unused `ActionGameManager` reference was removed. | Done for this focused 7D.4 read slice. Continue moving map/sector reads off the temporary observed galaxy and global runtime settings where typed projections exist. |
| Sector zone details reads | `Assets/Scripts/UI/Menu/SectorRenderer.cs` | The zone-details Eve surface now reads sector topology from `SectorMapAsync`, daemon zone facts from `ZoneDetailsAsync`, hull type lookups from `OpenRuntimeCatalog`, and formatting settings from `PlayerSettingsAsync` through an explicit `AetheriaClient`. It no longer asks `ActionGameManager` for zone snapshots, runtime catalog, or player formatting, and no longer reads the whole authoritative frame for this surface. | Done for zone-details surface. The sector map layout/reveal path still uses the legacy `GalaxyZone` facade until every remaining renderer signal is represented by portable typed projections. |
| Target/contact presentation reads | `Assets/Scripts/Gameplay/AetheriaUnityObservedTargetQuery.cs`; `Assets/Scripts/Gameplay/AetheriaUnityTargetPresentation.cs`; projection in `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs` | Unity target presentation now reads `gamecult.aetheria.zone_contacts.v1` through `AetheriaClient.ZoneContactsAsync()`. The feed carries typed target/contact identity, visibility, hostility, target position, deltas, and distance for the current zone. Target HUD, target-distance renderer cache, minimap compass markers, and renderer visibility fades no longer call snapshot render-query helpers for those facts. | Done for current target HUD indicators, target-distance renderer cache, compass markers, and visibility fade state. Remaining target presentation objects are Unity affordances over typed contact rows. |
| Docked current-entity binding | `Assets/Scripts/Gameplay/AetheriaUnityObservedDockingIndex.cs`; `Assets/Scripts/Gameplay/AetheriaUnityCurrentEntityBinder.cs`; `Assets/Scripts/Gameplay/AetheriaUnityCurrentEntityPresentation.cs` | Docking state now comes from `AetheriaClient.CurrentDockingAsync()` and resolves the Unity docking-bay facade by typed dock parent key plus bay index. Dock camera body focus uses `AetheriaClient.ZoneRenderAsync()` body poses rather than passing a raw current-zone snapshot into presentation. | Done for current docked binding path. The remaining Unity facade entity/docking-bay objects are presentation adapters, not portable gameplay state. |
| Inventory/trade item detail reads | `Assets/Scripts/UI/Menu/InventoryMenu.cs`; `Assets/Scripts/UI/Menu/InventoryPanel.cs`; `Assets/Scripts/UI/Menu/TradeMenu.cs` | These panels already own explicit `AetheriaClient` instances for typed command submission. They now also read runtime catalog/manufacturer data and player formatting settings through those local clients for ship settings, cargo item details, equipped item details, trade table columns, trade behavior fields, trade item details, dropdown loadout pricing, inventory hull/item geometry, hardpoint typing, durability lookup, and temperature labels. | Done for current detail/dropdown/panel slice. Remaining HUD schematic, entity prefab presentation, and action-bar icon/name reads still use manager globals until migrated. |
| Current ship settings facts | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsViewportDocuments.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`; `Assets/Scripts/UI/Menu/InventoryMenu.cs` | `CurrentEntityAsync` now publishes the current entity shutdown-performance threshold. `InventoryMenu` renders the ship-settings Eve surface from the typed current-entity document and submits shutdown changes by daemon entity key instead of storing a Unity `Entity` facade as the settings source. | Done for current ship-settings read slice. The daemon still validates range and applies authority; Unity only renders the typed row and submits intent. |
| Current cockpit HUD facts | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsViewportDocuments.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`; `Assets/Scripts/UI/HUD/SchematicDisplay.cs` | `CurrentEntityAsync` now publishes a typed current-entity HUD status row for override shutdown, shield activity, heatsinks, heat/hypothermia exposure, visibility, hull ratio, radiator range, sensor cooldown, reactor draw, capacitor charge, and Aether-drive RPM. The player schematic HUD reads that row through its local `AetheriaClient` instead of pulling those facts from the Unity `Entity` facade. | Done for the current player HUD status slice. Target/enemy schematic rows still adapt Unity presentation objects until a target-entity projection replaces that display path. |
| Station refit docking-bay rows | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsViewportDocuments.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`; `Assets/Scripts/UI/Menu/InventoryPanel.cs`; `Assets/Scripts/UI/Menu/InventoryMenu.cs` | `StationRefitAsync` now publishes typed docking-bay rows with slot identity, occupied entity identity, current-entity match, hull key, and cargo items. Inventory menu/panel docking-bay display validates the current row before adapting the Unity docking-bay facade. | Done for the current docking-bay display slice. Continue promoting station stock/refit eligibility and remaining cargo presentation facts into typed station/refit projections before Stage 8. |
| Loadout restore options | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsViewportDocuments.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaClient.cs`; `Assets/Scripts/UI/Menu/InventoryPanel.cs` | `StationRefitAsync` now composes daemon frame state, Verse loadout templates, and runtime catalog policy into typed restore options. Each row carries template name, daemon target entity key, price, and `CanRestore`. InventoryPanel renders and submits those rows instead of reading `LoadoutTemplatesAsync` or pricing templates locally. | Done for current restore dropdown slice. The daemon still validates restore acceptance on command apply; clients only display the projected row and submit intent. |
| Trade cargo target rows | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsViewportDocuments.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`; `Assets/Scripts/UI/Menu/TradeMenu.cs` | `StationRefitAsync` now publishes typed cargo target rows for the current docking bay and player ship bays. Each row carries target kind, label, entity key, bay index, hull key, and cargo items. TradeMenu builds the target selector and owned counts from those rows instead of reconstructing targets from available entities or treating station stock as docking-bay cargo. | Done for current target cargo/count slice. Remaining trade row filtering and purchase submission stay typed; Stage 8 can delete more Unity presentation adapters once row rendering is fully projection-native. |
| Station stock trade facts | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsViewportDocuments.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`; `Assets/Scripts/UI/Menu/TradeMenu.cs` | `StationRefitAsync` enriches each station stock row with shared-policy price, affordability, and owned quantity. Hull ownership is counted from typed available entities; item ownership is counted from typed cargo target rows. TradeMenu renders the `OwnedQuantity` row fact instead of counting Unity ships or cargo dictionaries locally. | Done for current stock row fact slice. The daemon projection owns trade row display facts; command apply still revalidates purchase acceptance. |
| Action-bar and renderer catalog reads | `Assets/Scripts/Gameplay/ActionBarSlot.cs`; `Assets/Scripts/Zone Display/ZoneRenderer.cs`; `Assets/Scripts/Gameplay/EntityInstance.cs`; `Assets/Scripts/Gameplay/ShipInstance.cs` | Action-bar bindings resolve gear icon catalog data through their explicit `AetheriaClient`. `ZoneRenderer` owns a local `AetheriaClient` catalog cache used for entity hull prefabs, pickup labels, pickup tier color, entity hull binding, and ship durability normalization. | Done for current presentation catalog slice. Remaining HUD schematic catalog/formatting and main-menu observed-galaxy boot still need separate treatment. |
| Input and nebula settings reads | `Assets/Scripts/UI/InputScreen/InputDisplayLayout.cs`; `Assets/Scripts/Zone Display/VolumeCloudRenderer.cs` | The input screen reads action-bar input visibility from `AetheriaClient.PlayerSettingsAsync()`. The volume cloud renderer reads nebula quality from `AetheriaClient.PlayerSettingsAsync()` and falls back to its serialized quality if local state cannot be read. | Done for current settings slice. |
| Main-menu and HUD catalog/settings reads | `Assets/Scripts/UI/MainMenu.cs`; `Assets/Scripts/UI/HUD/SchematicDisplay.cs` | Main menu reads the runtime catalog and typed sector-map boot state through its local `AetheriaClient`; the legacy observed-galaxy boot projection now lowers from `SectorMapAsync` rather than a whole authoritative daemon frame. Schematic HUD reads item catalog data and player formatting settings through its local `AetheriaClient`. | Done for direct manager-global read cleanup and whole-frame boot read removal. `SchematicDisplay.cs` was transcoded to UTF-8 before editing because it contained legacy non-UTF8 degree-symbol bytes. |
| Trade value settings projection | `Aetheria.State/Documents/AetheriaTradeValuePolicy.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeCatalogSnapshot.cs`; `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeCatalogStore.cs`; `InventoryMenu.cs`; `InventoryPanel.cs`; `TradeMenu.cs`; `ZoneRenderer.cs` | Trade value projection is now authored typed state. The daemon seeds `global:aetheria.trade_value_policy.v1`, provider advertisements include `aetheria.trade_value_policy.v1`, and runtime catalog snapshots read that policy into `AetheriaRuntimeCatalogSnapshot.TradeValueSettings`. Unity panels/renderers consume it through explicit `AetheriaClient` catalog caches. The Unity `GameSettings` projection helper and `ActionGameManager.ObservedTradeValueSettings` are gone. | Done for manager, Unity-settings source, and code-only default cleanup. Next policy work is designer-facing Eve/CultUI authoring, not another Unity settings bridge. |
| Sector map reads | `Assets/Scripts/UI/Menu/SectorMap.cs`; `Assets/Scripts/UI/Menu/SectorRenderer.cs`; `Assets/Scripts/UI/MainMenu.cs` | Sector map renders from `AetheriaClient.SectorMapAsync`, emits clicked zone indices, and resolves details from `SectorMapAsync` plus daemon zone contents. Main-menu observed-game boot also uses `SectorMapAsync` for run id, frame id, tutorial mode, generation seed, zones, links, discovery, and faction relationships. It no longer reads `ActionGameManager.TryGetObservedGalaxy`, passes `GalaxyZone` objects through UI events, or reads whole daemon frames for Unity observed-galaxy boot. | Done for the current topology/detail/boot surface. Remaining work is richer faction/home/boss icon metadata as typed sector-map fields if designers still need those signals. |
| Frame lowering | `Assets/Scripts/Gameplay/AetheriaUnityObservedFrameApplier.cs`; `Assets/Scripts/Gameplay/AetheriaUnityObservedRunProjection.cs`; `Assets/Scripts/Gameplay/AetheriaUnityObservedZoneContextProjector.cs`; `Assets/Scripts/Gameplay/ActionGameManager.cs` | Frame handoff is now named as `ApplyLatestZoneRender`: it reads `ZoneRenderAsync()` and adapts only the target zone needed for the Unity presentation shell. The frame applier no longer receives the whole observed galaxy facade, the temporary projected-zone lookup lives beside the observed-run projection holder, and `ActionGameManager` no longer keeps an observed-galaxy property. The remaining whole-graph dependency is lazily resolved inside the quarantined `Zone` construction adapter because the legacy `Zone` constructor still requires it. | Keep narrowing toward typed projection/native render consumption. Persistent gameplay mirror belongs to Stage 8 demolition. |

### 7D.5 Renderer/Input Shell Queue

| Surface | Evidence | Current Shape | Stage 7D Action |
| --- | --- | --- | --- |
| Zone renderer frame application | `Assets/Scripts/Zone Display/ZoneRenderer.cs:220`, `:239` | `LoadDaemonZoneView` clears/rebuilds a Unity hierarchy; `ApplyZoneRender` updates render-time zone data, caches target/contact rows from `ZoneContactsAsync()`, loads presentation entities from `ObjectsViewportAsync(...)`, and discovers body views plus terrain sampling facts from `GravityViewportAsync(...)` for the current XY bounds. Unity no longer recomputes target, compass, visibility, presentation entity, visible body-view, terrain-height, terrain-band, asteroid-instance, dropped-pickup, entity-facade, orbit, or body facts from raw zone snapshot render queries. | Keep only as render cache. `ApplyZoneRender` is acceptable as per-frame projection consumption; `LoadDaemonZoneView` is now the remaining Stage 8 hierarchy rebuild shim once renderer data is native/projection-driven. |
| Entity instance cache | `ZoneRenderer.cs`; `AetheriaUnityObservedFacadeIndex.cs`; target presentation wiring in `AetheriaUnityGameplaySceneWiring.cs` | Unity keeps daemon-indexed presentation objects for rendering, docking, target HUD, and camera binding. Daemon-index entity lookup lives in the explicit facade index/scene wiring instead of an `ActionGameManager` shim. | Classify as temporary renderer shell. It must not become gameplay authority; replace with render/native views as Stage 8 proceeds. |
| SoA render fast path | `Assets/Scripts/Gameplay/AetheriaDaemonRenderBuffer.cs:50`, `AetheriaDaemonIndirectRenderer.cs:40`, `AetheriaDaemonObserver.LastRenderNativeView` | Reads observer SoA/native view for rendering. | Keep. This is aligned with thin renderer direction. Attach it to the 7D.2 facade instead of direct observer singleton lookup. |
| Ymir click/physics bridge | `Assets/Scripts/Gameplay/Physics/AetheriaYmirPhysicsBridge.cs` | Uses Ymir/clickable/hull mappings, not `UnityEngine.Physics` as simulation authority. | Keep as presentation/query bridge, then narrow behind facade/render shell. Any remaining Unity collider components are click/render affordances, not gameplay physics authority. |
| `GetComponent` and prefab collider hits | many source/prefab hits | Mostly Unity presentation wiring, object pooling, click affordances, and imported asset metadata. | Non-actionable for 7D unless the component participates in gameplay authority. Recheck during Stage 8 demolition. |

### Non-Actionable Queue Hits

| Hit | Evidence | Decision |
| --- | --- | --- |
| Event log queue | `Assets/Scripts/EventLog.cs:13` | UI log buffer; not a command bus. Keep. |
| Sector reveal queue | `Assets/Scripts/UI/Menu/SectorMap.cs:54`, `:79` | Presentation animation queue; not a gameplay command bus. Keep until the sector map projection is replaced. |
| KD-tree query queue | `Assets/Scripts/ServerShared/NIH/KDTree/KDQuery/*.cs` | Internal spatial query implementation. Keep unless replaced by CultMath/Ymir spatial structures in a later performance pass. |
| `System.Threading.Channels.dll` asmdef reference | `Packages/org.gamecult.aetheria.state/Runtime/GameCult.Aetheria.State.Unity.asmdef` | Assembly reference only; no ad hoc runtime channel found in scoped source. |
| `Physics.` source search | scoped C# search | No actionable `UnityEngine.Physics.*` simulation authority call found in `Assets/Scripts` or state runtime during this pass. |

## Stage 7B Build Order

Done:

- public renderer API no longer exposes `window.aetheriaRts.command` or
  `window.aetheriaRts.viewport`;
- Electron IPC no longer exposes `aetheria-rts:command` or
  `aetheria-rts:viewport`;
- browser code now calls typed methods `setMoveVector`, `setTarget`, and
  `mapViewport`;
- `aetheria-cultmesh.ts` is now transport-focused and no longer owns
  MessagePack slot layout;
- local generated-style bindings in `aetheria-rts-bindings.ts` encode/decode
  named slots for command documents, including the 30-slot daemon command
  layout;
- `scripts/generate-rts-bindings.mjs` derives schema ids, command kind ids, and
  slot maps from the C# MessagePack document declarations into
  `aetheria-rts-generated-bindings.ts` for daemon command, daemon frame, daemon
  health, RTS viewport payloads, nested daemon snapshot payloads, Verse
  authority policy, authority rule, and authority lease;
- `AetheriaRuntimeRtsProjection` owns typed viewport projection over local
  daemon frame state, including controlled-unit visibility union, status,
  inventory, and gravity influence intersections;
- daemon RUDP snapshot handling delegates RTS viewport construction to
  `AetheriaRuntimeRtsProjection` instead of carrying map projection logic
  inline;
- `Aetheria.State.AuthoritySmoke` verifies the projection behavior against a
  local daemon frame;
- Electron map reads now fetch the latest daemon frame and project it locally in
  `aetheria-rts-local-projection.ts` instead of requesting the remote RTS
  viewport document;
- Electron selected-object, inventory/cargo, daemon health, and authority status
  panels now read through typed local projection facade methods instead of
  deriving panel state sideways from the map payload;
- Electron map/panel projection reads now use local CultCache publication files
  through `aetheria-local-publication-reader.ts` instead of loopback
  `sendSnapshotRequest`;
- the daemon publishes the Verse authority policy to a local `.authority.policy.cc`
  sidecar so Electron can read authority status locally alongside frame and
  health;
- `Aetheria.Rts.Web/scripts/verify-stage7c-local-runtime.ps1` starts a one-shot
  daemon and proves the compiled Electron runtime facade can read map,
  selected-object, inventory, health, and authority projections from local
  CultCache publications;
- `Aetheria.Rts.Web/scripts/verify-stage7c-electron-shell.ps1` launches the real
  Electron app shell against a disposable runtime directory and proves the
  renderer can refresh through preload IPC using the typed projection facade;
- Electron preload/main expose typed projection channels:
  `aetheria-rts:selected-object`, `aetheria-rts:inventory`,
  `aetheria-rts:daemon-health`, and `aetheria-rts:authority-status`;
- `Aetheria.Rts.Web/scripts/verify-stage7b-rts-client.ps1` fails if the old
  public generic API returns or if the transport wrapper regains document
  layout/codec ownership, fails if stale remote viewport decoder helpers return,
  and also fails if the generated bindings are stale.

Remaining:

1. Move Unity command/read paths onto the same typed local client shape proven by
   Electron.
2. Add a separate peer sync health projection if daemon health stops being the
   canonical peer health surface.
3. Replace the remaining semantic command construction arrays with a CultMesh
   TS typed document writer once that primitive is available locally.
4. Narrow Electron preload/main IPC behind a typed runtime facade.
5. Keep the daemon RTS viewport handler only as temporary compatibility plumbing
   until no client or diagnostic path depends on it.
6. Keep the verifier checking for old public generic surfaces and transport
   wrapper layout leaks:
   - `window.aetheriaRts.command`;
   - `window.aetheriaRts.viewport`;
   - `ipcMain.handle("aetheria-rts:command"`;
   - `ipcMain.handle("aetheria-rts:viewport"`;
   - `function createCommandDocument`;
   - `decodeViewportDocument`;
   - `viewportRecordKey`;
   - public TS command union `{ kind: ... }` used as transport payload;
   - hand-indexed MessagePack document layout in `aetheria-cultmesh.ts`.

## Stage 7A Exit Check

This inventory is the Stage 7A artifact. Stage 7A has exited. Electron's Stage
7B/7C proving surface now has generated binding metadata, typed command facade
methods, local map/panel projections, local CultCache publication reads, and
runtime/app-shell verifiers. The next use of this file is Stage 7D.1: refresh
the Unity entries into a concrete edit queue before changing Unity behavior.
