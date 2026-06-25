# Stage 7 Thin Client Staged Implementation Plan

This is the build map for turning Unity and Electron into thin CultMesh clients
without building Jenga. The broader Verse authority plan explains why the
architecture exists; this document explains the exact order to build it in.

Current gate: Stage 7D.4. Stage 7C must be re-verified after any local runtime
script or daemon launch change, but no new architecture is allowed there unless
the verifier fails.

Stage rule: a stage may consume only the artifacts listed as inputs, may create
only the artifacts listed as outputs, and must remove or quarantine its
demolition target before the next stage starts.

## How To Use This Plan

This is the migration runbook. When working on the thin-client split, pick the
first incomplete slice in the current stage and do only that slice. A slice is
complete only when its proof passes and its demolition target is gone,
quarantined, or explicitly carried forward by the next slice.

Every implementation move must answer five questions before code changes:

1. Which stage and slice does this advance?
2. Which existing surface is being replaced or narrowed?
3. Which typed document, projection, or facade owns the new shape?
4. Which verifier proves the old path did not grow back?
5. Which later slice is now unlocked?

If an edit cannot answer those questions, it is not Stage 7 work. Put it in a
parking note or defer it.

## Build Tracks

Stage 7 has four tracks. Only the active track may create new runtime shape.

| Track | Purpose | Current state | Owner |
| --- | --- | --- | --- |
| Contract | Define typed command and projection vocabulary shared by clients. | Electron slice exists; Unity parity next. | `AetheriaRuntimeVerseClient`, generated TS bindings, projection modules |
| Publication | Make daemon state visible as typed local state, not remote viewport fetches. | Electron frame/health/policy sidecars exist. | daemon publication store and local readers |
| Client shell | Bind UI/input/rendering to the typed facade. | Electron proven; Unity next. | Electron main/preload/client and Unity presentation shell |
| Demolition | Delete or quarantine legacy gameplay ownership. | Waiting for Unity parity and cross-runtime smoke. | Stage 8 only |

Do not mix tracks inside one slice unless the slice explicitly says so.

## Target Shape

Every client runtime is a local Verse participant:

1. The UI/runtime process owns input, rendering, and local presentation state.
2. The local Verse node owns typed document exchange with the local daemon and
   peers.
3. The daemon owns simulation and publishes committed typed facts.
4. Reads are local projections over local Verse state.
5. Writes are typed command/fact documents.
6. No public client API accepts `command(kind, payload)`, raw document bags, or
   viewport strings as gameplay semantics.

The ergonomic layer for that shape must graduate into CultMesh/CultLib when it
is reusable. Aetheria may prove the API with domain sugar, but shared concepts
belong in the cross-runtime primitive layer: typed state pointers, typed
operation handles, typed query surfaces, projection recipes, authority claims,
reactive watches, and native slab descriptors. The current shared roadmap is
`E:/Projects/CultLib/src/GameCult.Mesh/docs/cross-runtime-primitives-roadmap.md`.

Electron and Unity must converge on this shape:

```text
UI input
  -> typed client facade
  -> typed command document
  -> local Verse node
  -> local daemon command gate
  -> simulation
  -> committed fact/frame documents
  -> local Verse replica
  -> typed local projection
  -> renderer/UI panels
```

The map viewport is just one projection. Selected object state, inventory,
cargo, authority status, and peer health must follow the same pattern.

Starbridge is the current design pressure for this shape. One RTS commander and
one to four Unity pilots must share the same Verse facts while each runtime
authors only the typed claims it should own.

The product source for that pressure is
`E:/Projects/AetheriaLore/Aetheria/Game Design/Aetheria Starbridge.md`. Stage 7
does not own the whole release, but every client-parity slice should make one
of Starbridge's shared surfaces more portable: session/base facts, tactical map
projections, station/refit state, commander verbs, pilot field verbs, recovery,
waves, or episode/progression data.

The staged Starbridge implementation slices live in
`Aetheria.State/docs/verse-authority-implementation-plan.md` under
`Starbridge Slice Map`. Treat that map as the product-facing build order for
new co-op verbs. This Stage 7 runbook remains the client-parity/demolition
runbook: it decides when Unity and Electron are thin enough to consume those
verbs without creating client-owned gameplay branches.

Commander-facing Starbridge surfaces:

- map, fog, wave, hostile, and base-system projections;
- infrastructure placement, power routing, fabrication, drone/turret orders,
  station-stock, recovered-technology, construction-ghost, target-mark, and
  commander support operations;
- authority diagnostics for which runtime owns each claim kind.

Pilot-facing Starbridge surfaces:

- responsive ship movement, local combat, salvage, docking, refit, anchoring,
  cooling, repair, target-mark, survival-pod, cargo, equipment, and status
  operations/projections;
- support-gear validity derived from equipped daemon state, not from client
  role assumptions.

None of those are allowed to become client-local gameplay branches. If the RTS
client can do it, Unity or another runtime must be able to inspect the same
typed state and issue the same typed operation when policy allows it. If Unity
can do it, the RTS client must be able to observe the committed result through
local Verse state rather than through a peer viewport.

## Current Baseline

Completed and usable:

- Stage 6 live committed fact import passes.
- Electron public API exposes typed methods: `mapViewport`, `setMoveVector`,
  and `setTarget`.
- Electron IPC exposes typed channels:
  `aetheria-rts:map-viewport`, `aetheria-rts:set-move-vector`, and
  `aetheria-rts:set-target`.
- `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts` is transport-focused.
- `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` is generated
  from C# `[Key]` declarations.
- `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts` keeps ergonomic command
  encoders and public TS request/response types.
- `Aetheria.Rts.Web/Electron/aetheria-rts-local-projection.ts` projects the
  latest daemon frame into a local map viewport.
- Electron no longer requests the remote RTS viewport snapshot document.
- `AetheriaRuntimeRtsProjection` owns the matching C# map projection and the
  daemon delegates the temporary RUDP compatibility hook to it.
- `AetheriaRuntimeStarbridgeProjection` owns the first S0 session-summary
  projection, and `AetheriaClient.StarbridgeSessionSummaryAsync` exposes it as
  a named facade read without routing through map viewport state. Scenario and
  session facts now have latest-record keys in local Verse, so clients can ask
  the facade for the active Starbridge summary without passing documents around.
- The daemon boot path seeds the first Starbridge scenario/session facts from
  daemon-owned local state. A fresh once-mode daemon publishes `Frontier
  Fabricator Defense` as typed scenario/session documents, and the authority
  smoke reads the resulting summary through `AetheriaClient` instead of Unity
  scene authoring.
- Selected-object and inventory reads are now named projection documents rather
  than hidden whole-zone viewport reads. The map viewport remains a map
  projection; entity inspection and inventory panels have their own typed
  facade results.
- Stage 7D.4/S1 has started splitting the map projection itself. Objects visible
  to the union of controlled units and XY gravity influences now have separate
  typed documents and facade reads: `ObjectsViewportAsync` and
  `GravityViewportAsync`. `MapViewportAsync` remains as a compatibility
  composition over those two projections while Unity/Electron callers migrate.
- The Electron renderer now asks for `objectsViewport` and `gravityViewport`
  through preload IPC and composes its local draw model from those typed
  projections. `mapViewport` remains available for compatibility and simple
  diagnostics, but the app-shell path exercises the split projection contract.
- Unity's `MapRenderer` now refreshes its map header through the same
  `ObjectsViewportAsync` and `GravityViewportAsync` facade reads instead of
  reaching straight for the whole authoritative frame. This is only the first
  Unity parity cut: the legacy scene renderer still consumes whole-zone daemon
  snapshots until Stage 8 demolition replaces that shell.
- `Aetheria.State.AuthoritySmoke` verifies the C# projection.
- `npm run verify:stage7b` fails if public generic APIs return, if the transport
  wrapper regains layout ownership, if stale viewport decoders return, or if the
  generated binding metadata is stale.

Remaining risk:

- Electron command writes still use CultMesh RUDP document puts. That is
  acceptable for the command lane, but replacing the semantic command arrays
  with a typed TS document writer remains open.
- Unity does not yet consume the same thin client facade.
- Peer health is currently represented by daemon health; a divergent peer sync
  health document can be added to the projection catalog when the daemon
  publishes it.

## Stage Graph

```text
7B.0 inventory freeze
  -> 7B.1 generated contract metadata
  -> 7B.2 typed command facade
  -> 7B.3 local map projection
  -> 7B.4 local projection catalog
  -> 7B.5 local replica read path
  -> 7C Electron thin client
  -> 7D Unity thin client parity
  -> 7E cross-runtime co-op smoke
  -> 8 Unity gameplay shell demolition
```

Do not skip 7B.5. If Unity parity is built on top of loopback viewport/frame
requests, the bridge becomes the new legacy shell.

## 7B.0 Inventory Freeze

Status: complete.

Inputs:

- Stage 6 live committed fact import.
- Existing Unity shell.
- Existing Electron RTS shell.

Build:

- Inventory every client mutation path.
- Inventory every gameplay read/projection path.
- Classify each path as keep, replace, or delete.

Outputs:

- `Aetheria.State/docs/stage-7-client-surface-inventory.md`

Demolition target:

- Unknown public command/read surfaces.

Verifier:

```powershell
rg -n "command\(|viewport\(|Apply\(command|CommandPort|CachedCommandPort|EventBus|Bus" Assets Packages Aetheria.Rts.Web
```

Every hit must be listed in the inventory before implementation continues.

Stop line:

- No new runtime abstraction can be added until this inventory names the surface
  it replaces.

## 7B.1 Generated Contract Metadata

Status: complete for current command/frame/health/authority/snapshot slice.

Inputs:

- C# MessagePack document declarations in
  `Packages/org.gamecult.aetheria.state/Runtime`.

Build:

- Generate TS schema ids, enum ids, and slot maps from C# `[Key]`
  declarations.
- Treat generated metadata as read-only.
- Expand source coverage whenever a projection consumes a new nested document.

Outputs:

- `Aetheria.Rts.Web/scripts/generate-rts-bindings.mjs`
- `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts`

Demolition target:

- Hand-maintained TS copies of C# document layout.

Verifier:

```powershell
cd Aetheria.Rts.Web
npm run check:rts-bindings
```

Stop line:

- No TS projection or command writer may index a MessagePack array without slot
  metadata from this generator.

## 7B.2 Typed Command Facade

Status: complete for movement and target commands.

Inputs:

- Generated command metadata from 7B.1.
- Existing daemon typed command document.

Build:

- Expose typed TS request objects for command semantics.
- Keep command id, runtime id, issue time, and local session wiring inside the
  client runtime wrapper.
- Keep UI components unaware of transport payload shape.

Outputs:

- `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts`
- `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts`
- `Aetheria.Rts.Web/Electron/main.ts`
- `Aetheria.Rts.Web/Electron/preload.cjs`
- `Aetheria.Rts.Web/Client/app.ts`

Demolition target:

- Public `command(kind, payload)` and generic command IPC.

Verifier:

```powershell
cd Aetheria.Rts.Web
npm run verify:stage7b
```

Stop line:

- Do not add station, inventory, or combat commands until the command catalog is
  typed from UI to daemon gate.

## 7B.3 Local Map Projection

Status: complete for Electron map viewport shape; direct local replica reads
remain 7B.5.

Inputs:

- Generated daemon frame and nested snapshot metadata from 7B.1.
- Typed map request/response model from 7B.2.
- C# projection semantics in `AetheriaRuntimeRtsProjection`.

Build:

- Fetch the latest local daemon frame document.
- Project the map viewport in TS from the frame document.
- Use controlled-unit visibility union for fog of war.
- Include objects, status, inventory, gravity influences, and body views.
- Keep remote RTS viewport document handling as daemon compatibility only.

Outputs:

- `Aetheria.Rts.Web/Electron/aetheria-rts-local-projection.ts`
- `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`
- `Aetheria.State.AuthoritySmoke/Program.cs`

Demolition target:

- Electron requesting `gamecult.aetheria.rts_viewport.v1` from the daemon.
- Stale `decodeViewportDocument` and `viewportRecordKey` TS binding helpers.

Verifier:

```powershell
cd Aetheria.Rts.Web
npm run verify:stage7b
npm run build

cd ..
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Stop line:

- Do not add selected panels on top of the old viewport document. They must use
  the projection catalog in 7B.4.

## 7B.4 Local Projection Catalog

Status: complete for Electron selected object, inventory/cargo, daemon health,
and authority status.

Inputs:

- Local frame projection module from 7B.3.
- Generated snapshot metadata from 7B.1.

Build:

- Add typed local projections for:
  - selected object summary;
  - selected object stats/status;
  - equipment loadout;
  - cargo inventory;
  - peer sync health;
  - authority policy/status.
- Keep projection functions pure over local frame/health/policy documents.
- Give Electron UI panels typed facade methods instead of reading map payloads
  sideways.

Outputs:

- `Aetheria.Rts.Web/Electron/aetheria-rts-local-projection.ts`
- `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts`
- `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts`
- `Aetheria.Rts.Web/Client/app.ts`
- `Aetheria.Rts.Web/Electron/main.ts`
- `Aetheria.Rts.Web/Electron/preload.cjs`
- optional C# parity projection smoke additions in
  `Aetheria.State.AuthoritySmoke/Program.cs`

Demolition target:

- UI panels deriving selected-object details from ad hoc map object shape.
- Any new bespoke RTS gameplay behavior in the browser client.

Verifier:

```powershell
cd Aetheria.Rts.Web
npm run verify:stage7b
npm run build
```

Additional acceptance:

- Selecting an owned pawn or station shows status and inventory from typed local
  projections.
- The browser client still contains no simulation rules beyond presentation,
  selection, and command intent creation.
- `npm run verify:stage7b` requires the typed projection symbols and IPC
  channels.

Stop line:

- Do not move Unity onto the facade until 7B.5 replaces Electron's loopback
  frame/health/policy fetches with local replica reads.

## 7B.5 Local Replica Read Path

Status: complete for Electron frame, health, and authority-policy reads.

Inputs:

- Projection catalog from 7B.4.
- CultMesh/CultCache local replica/runtime APIs.

Build:

- Replace loopback frame, health, and authority-policy fetches with direct local
  CultCache publication reads.
- Keep the projection function signatures stable.
- Leave transport code responsible only for sync, not projection semantics.

Outputs:

- `Aetheria.Rts.Web/Electron/aetheria-local-publication-reader.ts`;
- updated `AetheriaCultMeshClient` read path;
- verifier proving map reads no longer issue snapshot requests for local frame
  data.
- authority policy sidecar publication through
  `AetheriaRuntimeDaemonPublicationStore`.

Demolition target:

- Electron map/panel reads that depend on loopback daemon snapshot requests.
- Missing local authority policy publication.

Verifier:

```powershell
rg -n "sendSnapshotRequest|recordKeys|schemaIds" Aetheria.Rts.Web/Electron
cd Aetheria.Rts.Web
npm run verify:stage7b
npm run build
```

Allowed remaining hits must be sync/bootstrap plumbing outside the Electron
client runtime. `aetheria-cultmesh.ts` must not contain snapshot request code.

Stop line:

- Do not begin Unity parity until Electron launch proves the local publication
  files exist and can drive map/panel projections.

## 7C Electron Thin Client

Status: complete for automated local-runtime and Electron app-shell smoke.

Inputs:

- Typed command facade from 7B.2.
- Local projection catalog from 7B.4.
- Local replica read path from 7B.5.

Build:

- Electron launches or attaches to the local daemon.
- Browser UI renders map, selected panels, health, and authority status from
  typed facade methods.
- Browser UI submits only typed command intents.
- Enemy/pawn/station behavior remains daemon simulation, not RTS client code.

Demolition target:

- Browser-side gameplay simulation.
- Renderer access to raw CultMesh transport calls.

Verifier:

```powershell
cd Aetheria.Rts.Web
npm run build
npm run verify:stage7b
npm run verify:stage7c
npm run verify:stage7c:electron
```

Automated smoke:

- builds the RTS renderer and Electron main process;
- starts the Aetheria daemon once with a disposable state file;
- reads frame, selected object, inventory, daemon health, and authority policy
  through the compiled Electron runtime facade;
- fails if map/panel projections cannot be driven from local CultCache
  publication files.

Electron shell smoke:

- launches the real Electron app with a disposable runtime directory;
- starts the daemon through the app's normal launch path;
- waits for the renderer to refresh through preload IPC;
- checks status, selected object details, equipment/cargo, daemon health,
  authority policy, controlled list, and canvas sizing;
- exits non-zero if the app shell cannot drive the same typed projection path.

Optional manual smoke:

- Launch Electron.
- Select owned pawn.
- Observe status/inventory.
- Issue movement/target command.
- Confirm daemon frame changes and UI updates from local projection reads.

Stop line:

- Do not add RTS features that require browser-owned gameplay state.
- Unity parity may begin only by reusing the typed local client shape proven by
  Electron.

## 7D Unity Thin Client Parity

Status: next.

Inputs:

- Electron-proven typed client facade.
- Unity shell inventory from 7B.0.

Build principle:

Unity does not get a bespoke bridge. Unity gets the same local-client shape
Electron proved: typed command submission, typed local projection reads, peer
health, and authority status. The Unity-specific code may adapt data into
render buffers and input gestures, but it must not define new gameplay
semantics.

### 7D.0 Re-verify Electron Baseline

Status: complete.

Purpose:

- Make sure Stage 7D starts from a working thin-client proof rather than a
  remembered one.

Allowed edits:

- Verifier flake fixes only.
- No new runtime abstraction.

Proof:

```powershell
cd Aetheria.Rts.Web
npm run verify:stage7b
npm run verify:stage7c
npm run verify:stage7c:electron

cd ..
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Demolition target:

- Any Stage 7C verifier that can pass while the renderer uses generic
  command/viewport APIs or loopback viewport snapshots.

Unlocks:

- 7D.1.

### 7D.1 Unity Surface Triage

Status: complete.

Purpose:

- Turn the Unity inventory into an edit queue with owners. This is not another
  broad modeling pass; it is the last stop before code movement.

Allowed edits:

- Documentation and search harness updates.
- No runtime behavior changes except verifier-only assertions.

Map these surfaces into keep/replace/delete:

- `ActionGameManager` input methods;
- `AetheriaDaemonObserver` command and read paths;
- `AetheriaRuntimeDaemonOperationClient`;
- `AetheriaRuntimeDaemonSurfaceCommands`;
- `ZoneRenderer` zone/frame loading;
- Unity physics entry points: `Rigidbody`, `Collider`, `Physics.`;
- UI panel reads that go through manager singletons.

Output:

- Update `Aetheria.State/docs/stage-7-client-surface-inventory.md` with a
  slice assignment for each still-actionable Unity hit:
  `7D.2 facade`, `7D.3 commands`, `7D.4 reads`, `7D.5 renderer`, or
  `8 demolition`.

Proof:

```powershell
rg -n "ActionGameManager|AetheriaDaemonObserver|AetheriaRuntimeDaemonOperationClient|AetheriaRuntimeDaemonSurfaceCommands|ZoneRenderer|Rigidbody|Collider|Physics\\.|SubmitDaemonCommand|Queue|Channel|ConcurrentQueue|Bus|CommandPort|Apply\\(" Assets Packages/org.gamecult.aetheria.state
```

Every actionable hit must have an owner in the inventory.

Demolition target:

- Unknown Unity gameplay ownership.

Unlocks:

- 7D.2.

### 7D.2 Shared Client Facade Shape

Status: complete for initial facade. Stage 7D.3 may now reroute Unity command
callers through this facade; Stage 7D.4 may add narrower projections as specific
read sites move.

Purpose:

- Give C#/Unity an ergonomic facade matching the Electron-facing shape without
  copying Electron's IPC shell.

Allowed edits:

- Add or narrow C# facade types in
  `Packages/org.gamecult.aetheria.state/Runtime`.
- Keep facade methods typed and named by game intent or projection:
  `SetMoveVector`, `SetTarget`, `MapViewport`, `SelectedObject`, `Inventory`,
  `DaemonHealth`, `AuthorityStatus`.
- The facade may wrap `AetheriaRuntimeVerseClient` and publication stores.

Forbidden edits:

- No `Apply(command, payload)`.
- No public `Command(string kind, object payload)`.
- No cached command port.
- No queue between input and the authority gate.
- No Unity-only command vocabulary.

Output:

- `AetheriaClient` in
  `Packages/org.gamecult.aetheria.state/Runtime/AetheriaClient.cs`.
- The facade owns a long-lived `AetheriaRuntimeVerseClient`, exposes the typed
  `AetheriaRuntimeDaemonOperationsClient`, and reads map viewport, selected
  object, inventory, daemon health, authority policy, and SoA view data through
  named methods.
- `AetheriaDaemonObserver` now resolves an `AetheriaClient` and exposes
  `Client`/`Operations` from that shared facade instead of owning separate
  operation and Verse clients.
- Unity package project/asmdef now includes the new facade/projection files and
  the CultMath dependency required by render/object viewport queries.

Proof:

```powershell
rg -n "Apply\\(command|Command\\(string|payload|CachedCommandPort|CommandPort|Queue|Channel|ConcurrentQueue|EventBus|Bus" Assets Packages/org.gamecult.aetheria.state
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
dotnet build .\GameCult.Aetheria.State.Unity.csproj
```

Proof status on 2026-06-24: authority smoke passed; Unity package build passed.
`Aetheria.State.Unity.Smoke` is not a clean 7D.2 verifier yet because the
default workspace state has a corrupt CultNet shard log, while a fresh state
copy lacks the expected typed Eve surface fixture.

Demolition target:

- Unity-facing generic command wrappers.

Unlocks:

- 7D.3 and 7D.4.

### 7D.3 Unity Command Reroute

Status: complete for the currently known Unity command ingress.

Completed slice:

- `ActionGameManager` no longer owns a cached runtime
  `AetheriaRuntimeVerseClient` for boot/catalog/settings/loadout template
  paths.
- Runtime catalog opening, player settings reads, loadout template reads, input
  settings commands, and loadout template save/delete commands now route through
  the shared `AetheriaClient` facade.
- `InputDisplayLayout` now owns an explicit `AetheriaClient` for input settings
  changes and the old `ActionGameManager` input-settings ingress has been
  removed.
- `MainMenu` daemon-frame reads, player-settings reads, Verse-host settings
  reads, and known Eve surface command submission now route through
  `AetheriaClient` instead of a menu-owned raw `AetheriaRuntimeVerseClient`.
- `AetheriaEveSurfacePresenter` now reads daemon Eve surfaces and submits Eve
  surface commands through `AetheriaClient`.
- `AetheriaEveUnitySurfaceHost` uses the file-backed runtime state reader for
  default state-ref resolution instead of constructing a raw Verse client.
- Low-level operation/Eve command compatibility fallbacks now open
  `AetheriaClient`; `AetheriaRuntimeVerseClient` is confined to the facade
  implementation, its own type, and tests.
- `AetheriaDaemonObserver` already exposes facade-backed typed operations, so
  existing observer command calls reuse the shared local Verse client instead of
  opening per-command runtime clients.
- Action-bar activation bindings now receive an explicit `AetheriaClient` when
  restored, and submit consumable, behavior-active, and weapon-group-active
  typed operations directly through the facade. The old public
  `ActionGameManager.RequestActionBar...` activation shims are gone.
- `InventoryPanel` now owns an explicit `AetheriaClient` for the dropdown/current
  ship slice: loadout restore, set docked current ship, entity rename, and hull
  conductivity toggles submit typed daemon operations directly from the panel.
  The matching `ActionGameManager` request shims were deleted; the manager only
  exposes observed entity record-key resolution for the panel's Unity facade
  entities.
- Inventory drag/drop and double-click transfers now submit typed
  `TransferCargoItem`, `EquipItem`, and `StoreItem` operations directly from
  `InventoryPanel`/`InventoryMenu` through explicit `AetheriaClient` instances.
  `ActionGameManager` no longer exposes cargo/equipment transfer request shims;
  it only resolves observed Unity facade cargo/equipment objects to daemon
  record keys and indices.
- `TradeMenu` now submits typed `TradePurchase` operations through its own
  `AetheriaClient` for station stock, commodity quantity purchases, cargo
  purchases, and docked ship hull purchases. The old
  `ActionGameManager.RequestTradePurchase` shim is gone.
- `InventoryMenu` now submits detailed equipped-item controls through its own
  `AetheriaClient`: item override shutdown, thermotoggle target temperature,
  weapon-group membership, action-bar binding, and action-bar clearing. The old
  public manager request shims for those menu commands are gone; the manager
  only resolves observed item identity and action-bar control paths.
- `InventoryPanel` now owns loadout-template save submission through
  `AetheriaClient.SubmitLoadoutTemplateCommandAsync`; `ActionGameManager` only
  exposes the temporary Unity-facade loadout projection helper.
- Remaining pilot input ingress in `ActionGameManager` now submits through a
  shared typed facade operation helper: movement, look direction, tractor power,
  targeting, reticle targeting, override shutdown, sensor ping, heatsinks,
  shields, interact, tow, dock, and undock. The private `TryRequestDaemon...`
  pilot command shims are gone; the manager remains the input gesture router
  until Stage 7D.4/7D.5 replace its read/projection dependencies.

Carried forward:

- New Starbridge verbs may expose new command surfaces only as typed
  daemon/state operations. The next command-expansion slice should cover
  station-stock authoring/refit, construction anchoring, target marks,
  support-gear cooling/repair, fabrication, drone/turret orders, and commander
  support. If a verb is not yet represented in daemon/state code, build it there
  first; do not hang it from Unity or Electron UI code.

Purpose:

- Move Unity input/UI mutations onto the typed local facade.

Allowed edits:

- Change `ActionGameManager` and UI callers so they create typed intent values
  and call the shared facade.
- Keep runtime id, command id, issue time, and endpoint wiring inside the
  facade/runtime wrapper.
- Leave old methods only as thin forwarding shims if removing them would
  explode call-site churn; mark each shim for Stage 8 deletion in the
  inventory.

Forbidden edits:

- Do not add gameplay logic to Unity to make rerouting easier.
- Do not make UI panels construct daemon command documents directly.

Output:

- Unity movement, target, loadout, and surface commands route through typed
  local command submission.

Proof:

```powershell
rg -n "SubmitDaemonCommand|RestoreLoadout|SetTarget|SetMove|Apply\\(|commandKind|payload" Assets Packages/org.gamecult.aetheria.state
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Search hits must show either typed facade usage or an inventory-listed Stage 8
shim.

Demolition target:

- Unity command paths that bypass the local daemon authority gate.

Unlocks:

- 7D.5 command input side.

### 7D.4 Unity Projection Reroute

Status: active. The first focused read reroute is complete for the map screen's
zone title and minimap-asteroid setting reads.

Purpose:

- Move Unity gameplay reads onto local typed projections/state. Unity can cache
  render data, but the cache must be presentation-only.

Allowed edits:

- Read daemon frame, health, authority policy, render SoA, selected object, and
  inventory through the shared C# client facade or existing typed state readers.
- Add projection helpers only if they are pure over local state/publications.
- Keep projection results portable enough for another runtime to ask for the
  same semantic data.

Forbidden edits:

- No remote viewport as game truth.
- No persistent Unity hierarchy as game truth.
- No `ZoneRenderer.LoadZone` ownership of level contents.

Output:

- Unity read sites stop depending on manager singleton gameplay state where a
  typed local projection exists.
- `ZoneRenderer` consumes per-frame projection/render data only.

Completed slice:

- `Assets/Scripts/UI/Menu/MapRenderer.cs` now owns an explicit `AetheriaClient`
  and refreshes through `ObjectsViewportAsync` and `GravityViewportAsync` for
  its current XY bounds, with minimap asteroid visibility still read from
  `PlayerSettingsAsync`. Its unused `ActionGameManager` dependency was removed.
- `Assets/Scripts/UI/Menu/SectorRenderer.cs` now owns an explicit
  `AetheriaClient` for the zone-details Eve surface. It reads daemon zone
  facts from `ZoneDetailsAsync`, sector topology from `SectorMapAsync`, hull
  type lookups from `OpenRuntimeCatalog`, and formatting settings from
  `PlayerSettingsAsync` instead of asking `ActionGameManager` for zone
  snapshots, runtime catalog, or player formatting. It also centers the sector
  view from the typed
  `CurrentZoneAsync` projection instead of carrying a serialized
  `ActionGameManager` dependency for `TryGetObservedRunZone`.
- `gamecult.aetheria.sector_map.v1` now defines a portable sector topology
  projection over local daemon frame state: current/entrance/exit zones,
  discovered zones, zone XY positions, adjacency links, and faction index
  markers. `AetheriaClient.SectorMapAsync` exposes this as the next replacement
  for `SectorMap`'s legacy `ObservedGalaxy` graph reads.
- `Assets/Scripts/UI/Menu/SectorMap.cs` now renders from
  `AetheriaClient.SectorMapAsync` and emits clicked zone indices instead of
  `GalaxyZone` objects. `SectorRenderer` resolves its zone details from the
  same typed sector projection plus daemon zone contents, so the sector menu no
  longer calls `ActionGameManager.TryGetObservedGalaxy`.
- `Assets/Scripts/UI/MainMenu.cs` now boots the observed Unity galaxy from the
  typed `SectorMapAsync` projection instead of `LatestAuthoritativeRunFrameAsync`.
  The sector-map feed carries tutorial mode, generation seed, and faction
  relationships so Unity no longer needs a whole daemon frame just to enter the
  observer scene.
- `gamecult.aetheria.zone_render.v1` now names the transitional Unity zone
  renderer feed. `AetheriaRuntimeRtsProjection.ProjectZoneRender` owns the
  projection, `AetheriaClient.ZoneRenderAsync` exposes it as a facade read, and
  `ActionGameManager` now restores the Unity shell from
  `AetheriaRuntimeZoneRenderDocument` rather than directly from a raw daemon
  run snapshot. The feed no longer carries the whole run or current zone
  snapshot into Unity;
  it carries explicit run-level render facts such as current entity key,
  credits, action-bar bindings, simulation time, render radius, adjacent-zone
  summaries, body poses, asteroid-belt poses, asteroid-instance poses, dropped
  pickups, entity facade rows, orbits, and bodies. `ZoneRenderer` no longer
  derives render radius from a zone snapshot, stores simulation time inside it,
  or recomputes render facts from it. This is still a Stage 8 shim because
  Unity rebuilds a GameObject hierarchy from typed facade rows; the value is
  making the boundary explicit and shared before replacing that payload with
  native/projection slabs.
- `gamecult.aetheria.current_entity.v1` now names the portable current-subject
  feed. `AetheriaRuntimeRtsProjection.ProjectCurrentEntity` derives the active
  entity from the typed run current entity key and returns entity identity,
  status, inventory, equipment, and cargo without requiring callers to know the
  entity index. `AetheriaClient.CurrentEntityAsync` exposes the feed for Unity
  and Electron parity work.
- `Assets/Scripts/UI/Menu/TradeMenu.cs` now uses its explicit `AetheriaClient`
  for trade item catalog/manufacturer lookups and player formatting settings.
  Its spreadsheet behavior columns and trade item details Eve surface no longer
  read `ActionGameManager.RuntimeCatalog` or `RuntimePlayerSettings`.
- `Assets/Scripts/UI/Menu/InventoryMenu.cs` now uses its explicit
  `AetheriaClient` for item catalog/manufacturer lookups and player formatting
  settings. Ship settings, cargo item details, equipped item details, and typed
  shape-cell lookups no longer read `ActionGameManager.RuntimeCatalog` or
  `RuntimePlayerSettings`.
- `Assets/Scripts/UI/Menu/InventoryPanel.cs` now uses its explicit
  `AetheriaClient` for loadout restore pricing, hull/item shape and durability
  lookups, hardpoint color typing, and temperature label formatting. The panel
  no longer reads `ActionGameManager.RuntimeCatalog` or
  `RuntimePlayerSettings` for those surfaces.
- `Assets/Scripts/Gameplay/ActionBarSlot.cs` now uses the binding's explicit
  `AetheriaClient` for gear action-bar icon catalog lookup instead of reading
  `ActionGameManager.RuntimeCatalog`.
- `Assets/Scripts/Zone Display/ZoneRenderer.cs`,
  `Assets/Scripts/Gameplay/EntityInstance.cs`, and
  `Assets/Scripts/Gameplay/ShipInstance.cs` now resolve hull, pickup, and
  durability catalog data through a `ZoneRenderer`-owned `AetheriaClient`
  catalog cache instead of `ActionGameManager.RuntimeCatalog`.
- `Assets/Scripts/UI/InputScreen/InputDisplayLayout.cs` now reads action-bar
  input visibility from `AetheriaClient.PlayerSettingsAsync()` instead of
  `ActionGameManager.RuntimePlayerSettings`.
- `Assets/Scripts/Zone Display/VolumeCloudRenderer.cs` now reads nebula quality
  from `AetheriaClient.PlayerSettingsAsync()` instead of
  `ActionGameManager.RuntimePlayerSettings`, while preserving the serialized
  renderer quality as fallback if local state is unavailable.
- `Assets/Scripts/UI/MainMenu.cs` now reads the runtime catalog through its
  local `AetheriaClient` when bootstrapping the legacy observed-galaxy
  projection. This does not make the observed-galaxy projection a desired final
  shape; it removes the direct manager-global catalog dependency while that
  projection remains quarantined.
- `Assets/Scripts/UI/HUD/SchematicDisplay.cs` now reads item catalog data and
  player formatting settings through a local `AetheriaClient`. The direct
  `ActionGameManager.RuntimeCatalog` and `RuntimePlayerSettings` reads are gone
  from the Unity codebase.
- `global:aetheria.trade_value_policy.v1` now owns the authored typed trade
  value policy used by inventory, trade, loadout pricing, and pickup rendering.
  `AetheriaRuntimeCatalogSnapshot.TradeValueSettings` reads that policy through
  the runtime catalog store. The old `ActionGameManager.ObservedTradeValueSettings`
  service-locator method and Unity `AetheriaUnityProjectionSettings` helper have
  both been deleted.
- Inventory and trade docking/refit affordances now validate Unity facade cargo
  objects against `gamecult.aetheria.current_docking.v1` before presenting or
  selecting the current docking bay. `InventoryMenu`, `InventoryPanel`, and
  `TradeMenu` all read `CurrentDockingAsync`. `InventoryPanel` loadout restore
  targets and `TradeMenu` docked-ship purchase targets now resolve from
  `DockParentEntityKey` instead of `ActionGameManager.DockedEntity`.
- `gamecult.aetheria.station_refit.v1` now owns the typed station/refit selector
  model for currently docked play: dock parent, docking bay, available docked
  entities, player-ship flags, cargo bay counts, and hull item keys.
  `InventoryPanel` and `TradeMenu` build their entity/cargo selector options
  from `AetheriaClient.StationRefitAsync`; `TradeMenu` also derives available
  ship purchase counts from the same typed projection. Their remaining Unity
  facade lookups are key-validated adapters used only to call existing
  `Display(...)` methods.
- `ActionGameManager.ObservedAvailableEntities()` has been deleted. Inventory
  dropdown selection now resolves a single typed station/refit entity key
  through `TryResolveObservedEntityFacadeByRecordKey`, keeping the Unity facade
  bridge key-scoped while the old `Display(Entity)` UI remains a Stage 8 shim.
- Inventory menu/panel docking-bay display now reads
  `gamecult.aetheria.current_docking.v1` first and adapts the Unity docking bay
  facade by `DockParentEntityKey` plus `DockingBayIndex` through
  `TryResolveObservedDockingBayFacadeByRecordKey`. The menu code no longer asks
  `ActionGameManager` for a broad current docking bay before validation.
- `gamecult.aetheria.station_refit.v1` now also publishes typed docking-bay
  rows: slot identity, occupied entity key/name/hull, current-entity match, and
  bay cargo items. `InventoryPanel` and `InventoryMenu` must resolve the current
  `AetheriaRuntimeStationDockingBayRow` before adapting the legacy Unity docking
  bay facade for display, so station/refit truth lives in the daemon projection
  instead of the facade graph.
- `gamecult.aetheria.station_refit.v1` now publishes loadout restore options
  with template identity, daemon target entity key, shared trade-policy price,
  and restore eligibility. `InventoryPanel` no longer enumerates loadout
  templates or computes restore prices locally; it renders and submits the typed
  row that came from `StationRefitAsync`.
- `gamecult.aetheria.station_refit.v1` now publishes trade cargo target rows for
  the current docking bay and each player ship cargo bay, including the target
  label, entity key, bay index, hull key, and cargo items. `TradeMenu` no longer
  reconstructs cargo selector targets from available entity options or reads the
  docking bay's cargo from station stock; target counts use the explicit row.
- `gamecult.aetheria.station_refit.v1` now enriches station stock rows with
  shared-policy price, affordability, and owned quantity. `TradeMenu` renders
  ownership from `OwnedQuantity`; it no longer counts player ships or target
  cargo locally.
- Inventory menu current-entity display now reads
  `gamecult.aetheria.current_entity.v1` first and adapts the Unity entity facade
  by daemon record key through `TryResolveObservedEntityFacadeByRecordKey`. The
  menu code no longer asks `ActionGameManager` for a broad current entity before
  validation.
- `gamecult.aetheria.current_entity.v1` now carries the current entity shutdown
  threshold. The inventory ship-settings Eve surface renders and updates that
  typed fact by daemon entity key instead of storing a Unity `Entity` facade as
  settings authority.
- `gamecult.aetheria.current_entity.v1` now carries typed player HUD status:
  override shutdown, shield activity, heatsinks, heat exposure, visibility,
  hull ratio, radiator range, sensor cooldown, reactor draw, capacitor charge,
  and Aether-drive RPM. `SchematicDisplay` reads those facts from
  `AetheriaClient.CurrentEntityAsync()` for the player schematic instead of
  reading those gameplay facts from the Unity `Entity` facade.
- Shared RTS/current-entity inventory rows now publish `SourceIndex`, `X`, and
  `Y` beside the source kind and item key. Cargo and equipment operations can
  validate against typed projection slot identity instead of searching Unity
  facade graphs for the command target.
- `InventoryPanel` and `InventoryMenu` now validate cargo/equipment submissions
  against those typed inventory rows before sending daemon operations. The
  remaining Unity facade adapter only supplies the presentation object's entity
  key/source index, and cargo submissions now include the origin grid cell
  instead of sentinel coordinates.
- `ActionGameManager` no longer exposes cargo/equipment command-target adapter
  methods. Inventory UI derives the presentation object's owner/index locally,
  validates that identity against typed projection rows, and only then submits
  through `AetheriaClient.Operations`.
- `LocalMenu` no longer asks `ActionGameManager` for docked local-story state.
  It reads `gamecult.aetheria.current_docking.v1`, adapts the story-bearing
  docking parent by typed parent key and bay index, and then renders the shared
  local-story Eve surface.
- `ActionGameManager.TryGetObservedCurrentEntity` and
  `TryGetObservedDockingBay` have been deleted. Current subject and docking
  state must be read through typed projections first; Unity facade access is
  limited to key-scoped adapters.
- `ActionGameManager.ObservedLoadoutTemplates` and its preload cache have been
  deleted. Loadout templates are read through `AetheriaClient.LoadoutTemplatesAsync`
  by the UI that needs them instead of being copied into manager-global state.
- `ActionGameManager.TryGetObservedGalaxy`, `TryGetObservedZoneSnapshot`, and
  `TryGetObservedRunZone` have been deleted as public read shims. Sector/map
  callers must use typed projections; the remaining observed galaxy object is a
  quarantined scene bootstrap adapter and Stage 8 renderer adapter, not a
  portable client API.
- `AetheriaUnityObservedFrameApplier` now consumes
  `AetheriaClient.ZoneRenderAsync()` for the per-frame Unity shell handoff
  instead of rebuilding `gamecult.aetheria.zone_render.v1` from
  `LastObservedState.Frame`. Its current-zone lookup also reads the typed
  zone-render feed, and the applier no longer receives the whole observed
  galaxy facade just to apply a frame. The temporary projected-zone lookup now
  lives beside `AetheriaUnityObservedRunProjection`, and `ActionGameManager`
  no longer keeps an observed-galaxy property. The remaining whole-graph
  dependency is lazily resolved inside the quarantined Unity `Zone`
  construction adapter until Stage 8 replaces the persistent scene graph with
  projection/native render consumption. The handoff is named
  `ApplyLatestZoneRender` so Unity code no longer describes this path as whole
  authoritative daemon frame application.
- `gamecult.aetheria.zone_details.v1` now owns the portable sector-zone details
  feed used by Unity's sector details panel: zone identity, mass, radius, body
  kinds, entity hull item keys, and contents availability. `SectorRenderer`
  reads it through `AetheriaClient.ZoneDetailsAsync(zoneIndex)` instead of
  pulling `LatestAuthoritativeRunFrameAsync()` and searching the whole run for
  a raw `AetheriaRuntimeZoneSnapshotCommit`.
- `gamecult.aetheria.zone_contacts.v1` now owns the portable current-zone
  target/contact feed used by Unity target presentation. `AetheriaUnityObservedTargetQuery`
  reads it through `AetheriaClient.ZoneContactsAsync()` and
  `AetheriaUnityTargetPresentation` consumes typed contact rows instead of
  asking raw zone snapshots or `AetheriaRuntimeDaemonRenderQueries` for target
  facts.
- `gamecult.aetheria.zone_contacts.v1` also carries target/contact position,
  delta, and distance facts. `ZoneRenderer.TryGetDaemonTargetDistance` now
  refreshes a typed target-row cache from `ZoneContactsAsync()` instead of
  querying `_daemonZoneSnapshot` through `TryQueryEntityTarget`, so weapon
  convergence no longer depends on a raw snapshot read. `ZoneRenderer` also
  derives minimap compass markers and visible-entity fade state from cached
  typed contact rows instead of `QueryCompassMarkers` or
  `QueryVisibleEntityIndices`, and it loads presentation entities through
  `ObjectsViewportAsync(...)` for the current XY bounds instead of
  `QueryPresentationEntityIndices`. `GravityViewportAsync(...)` now also
  carries viewport-scoped body rows with the body payload Unity needs for
  prefab/material lowering, so `ZoneRenderer` no longer calls `QueryBodyViews`
  over the raw zone snapshot. The gravity viewport also carries the terrain
  radius/depth/exponent/wave-frequency scalars used by the local render sampler,
  so terrain heights and minimap gravity bands no longer ask raw zone snapshots.
- `ActionGameManager.DockedEntity` and `DockingBay` have been removed as public
  properties. Docking presentation now sets the renderer perspective directly,
  while menu/refit/local-story callers read typed current-docking state and use
  only key-scoped facade adapters when they still need Unity objects.
- Docked current-entity binding now reads `CurrentDockingAsync()` through
  `AetheriaUnityObservedDockingIndex` instead of scanning the current zone
  snapshot for docking-bay parent relationships. The dock camera receives body
  look-at context from the typed `ZoneRenderAsync()` body-pose feed, so docked
  presentation no longer accepts a raw `AetheriaRuntimeZoneSnapshotCommit`.
- `ActionGameManager.TowingStation` is now private renderer/input context, and
  the unused `AvailableCargoBays` public enumeration has been deleted. Tow
  requests still lower through typed daemon operations; available cargo choices
  belong to typed station/refit projections, not manager-local enumerators.
- `ActionGameManager.Credits` has been deleted. Credits are projected through
  typed client state such as `StationRefitAsync`; Unity must not mirror run
  currency into manager-global state.
- `ActionGameManager.RuntimeCatalog` is now a private boot cache for
  manager-internal adapter construction only. Client UI must open typed catalog
  views through `AetheriaClient`, not through manager-global Unity state.
- `ActionGameManager` no longer keeps an `ObservedGalaxy` property. The
  remaining galaxy projection lives in `AetheriaUnityObservedRunProjection`,
  is populated from typed `SectorMapAsync` state, and exists only as a
  quarantined renderer/legacy `Zone` construction adapter. Portable clients
  must use typed daemon projections rather than raw Unity galaxy state.
- `ActionGameManager.IsTutorial` has been deleted. Tutorial/run mode is read
  from typed daemon run/frame projections at the call site instead of mirrored
  into public manager-global Unity state.
- `ActionGameManager.CurrentEnvironment` has been deleted. The volume renderer
  reads its own presentation settings directly; environment visuals must not be
  exposed as manager-global gameplay state.
- Save-loadout projection now runs through
  `AetheriaClient.LoadoutTemplateAsync(entityKey)` and
  `AetheriaRuntimeLoadoutSnapshotProjector`. Unity submits the typed template
  command after resolving the displayed facade to a daemon entity key; it no
  longer serializes the Unity `Entity` facade as the source of save truth.
- `ActionGameManager.DragObject` and `HasDragTarget` are no longer public.
  Inventory drag/drop uses `TryGetDraggedItem` and `EndDrag`'s consumed result
  instead of peeking raw manager gesture state.
- Raw inventory drag gesture state now lives in `AetheriaUnityDragSession`.
  `ActionGameManager` keeps only a narrow facade for scene UI callbacks instead
  of owning the drag object and target callback fields directly.
- `ActionGameManager.RuntimePlayerSettings` is now private boot/input context.
  UI and render surfaces must read player settings through their own
  `AetheriaClient` projections instead of the gameplay manager.
- `ActionGameManager.GameDataDirectory` and `RuntimeStateFilePath` have moved
  to `AetheriaUnityRuntimePaths`. Unity runtime path discovery is boot plumbing,
  not gameplay-manager state.
- `AetheriaRuntimeTradeValuePolicySurfaceBuilder` now exposes the authored
  `aetheria.trade_value_policy.v1` state as a provider-owned Eve/CultUI
  designer surface. The daemon editor publishes it beside the stat recipe
  surface, so trade value, loadout pricing, station stock, and future RTS
  economy tooling inspect the same typed policy instead of reviving Unity
  settings as gameplay truth.
- The trade value policy designer surface now has typed Eve/CultUI edit
  commands for the quality price curve and rarity tier thresholds. The command
  body is `AetheriaRuntimeTradeValuePolicyCommandBody`, and the daemon bridge
  persists edits through `AetheriaTradeValuePolicy` instead of a string payload
  or Unity settings object.

Next slice:

- Continue with station/refit read surfaces that still depend on the legacy
  Unity facade graph rather than manager-global catalog/settings. The direct
  `ActionGameManager.RuntimeCatalog` and `RuntimePlayerSettings` cleanup is
  complete, trade/pricing policy is now authored and edited as daemon state,
  and the remaining 7D.4 work is replacing station stock, cargo, inventory, and
  loadout reads with portable typed projections.

Active 7D.4 implementation queue:

This queue is ordered. Do not pick a later row because it is easier. Each row
must either delete its legacy read path or name the exact Stage 8 shim that
still owns the remaining Unity object adaptation. Starbridge-facing reads that
appear in Unity must become named `AetheriaClient` facade reads before they are
allowed to become Electron/RTS features.

| Order | Slice | Build | Delete or quarantine | Proof |
| --- | --- | --- | --- | --- |
| 0 | Policy/editor typed state | Trade value policy is authored and edited through `aetheria.trade_value_policy.v1`, `AetheriaRuntimeTradeValuePolicySurfaceBuilder`, typed Eve command bodies, and `AetheriaEveCommandBridge` persistence. | No Unity `GameSettings` or public string command/payload path may own pricing policy. | Unity compile, `verify-stage7d-unity-parity.ps1`, and `Aetheria.State.Verify` require typed commands, editable controls, and `PutTradeValuePolicyAsync`. |
| 1 | Sector topology | `SectorMap`, `SectorRenderer`, and main-menu observed-game boot consume `SectorMapAsync`, `CurrentZoneAsync`, daemon zone contents, catalog, and player settings through explicit clients. `SectorMapAsync` carries tutorial mode, generation seed, and faction relationships so Unity can lower the temporary observed galaxy without a whole daemon frame. | `SectorMap` no longer reads `ActionGameManager.TryGetObservedGalaxy`, passes `GalaxyZone`, or owns a legacy faction graph. `MainMenu` may not call `LatestAuthoritativeRunFrameAsync` for observed-game boot. | `verify-stage7d-unity-parity.ps1`; scoped search for `TryGetObservedGalaxy`, `GalaxyZone`, `Dictionary<Faction` in sector menu files, and `LatestAuthoritativeRunFrameAsync` in `MainMenu` returns no forbidden hits. |
| 2 | Zone render feed | Split `ActionGameManager` frame lowering into `gamecult.aetheria.zone_render.v1`, currently a typed transitional feed over explicit render facts, entity facade rows, orbit rows, and body rows. It no longer exposes the whole current-zone snapshot. Body poses, asteroid-belt poses, asteroid-instance poses, dropped pickups, entity facades, orbits, and bodies are render-feed facts, not per-frame Unity recomputations over the snapshot. Target, compass, and visibility rows come from `gamecult.aetheria.zone_contacts.v1`; viewport-scoped presentation loading comes from `ObjectsViewportAsync(...)`; visible body discovery and gravity terrain sampling facts come from `GravityViewportAsync(...)`. | `ZoneRenderer.LoadDaemonZoneView` remains only as a listed Stage 8 shim; it may not gain new state ownership. Raw run restoration in `ActionGameManager`, whole-zone exposure in `zone_render`, run/zone snapshot storage in `ZoneRenderer`, and `ZoneRenderer` calls to body/belt/asteroid-instance/target/compass/visibility/presentation/gravity-terrain daemon queries are forbidden. | Unity compile plus verifier checks for `ZoneRenderAsync`, `ProjectZoneRender`, no `TryRestoreEntityGraphFromDaemonRun`, no `render.Run`, no `render?.Zone`, no zone snapshot property on `AetheriaRuntimeZoneRenderDocument`, no `_daemonRunSnapshot`, no `_daemonZoneSnapshot`, no body/belt/asteroid-instance pose recomputation, no `QueryBodyViews`, no `EvaluateGravityTerrainHeight`, no `QueryGravityTerrainBand`, no `TryQueryEntityTarget`, no `QueryCompassMarkers`, no `QueryVisibleEntityIndices`, and no `QueryPresentationEntityIndices` in `ZoneRenderer`. |
| 3 | Current entity/status feed | Replace `ActionGameManager` entity facade reads needed by HUD, docking camera, local menu, click selection, inventory, and station/refit affordances with typed current-subject projections. `gamecult.aetheria.current_entity.v1` owns current entity identity/status/inventory/equipment/cargo, shutdown threshold, and player HUD status facts. `gamecult.aetheria.current_docking.v1` owns the current entity's docked state, dock parent identity, and docking bay index. | Unity facade objects may remain only as presentation adapters after their daemon keys are validated against `CurrentEntityAsync` or `CurrentDockingAsync`. Direct `TryGetObservedCurrentEntity` and `TryGetObservedDockingBay` calls are forbidden in newly migrated menu logic. Manager methods that only resolve daemon identity become private shims or move to state projection code. Target/enemy schematic rows remain a Stage 8 presentation shim until a target-entity projection exists. | HUD/menu surfaces read through `AetheriaClient`; no public manager read API returns Unity facade objects for portable state. Verifier requires `CurrentEntityAsync`, `CurrentDockingAsync`, `ProjectCurrentEntity`, `ProjectCurrentDocking`, typed-key validation in `InventoryMenu`, and typed HUD status consumption in `SchematicDisplay`. |
| 4 | Docking/refit projection split | Promote station stock, docked ships, loadout slots, cargo, and refit eligibility from menu-local facade graph reads into named typed projections that match Starbridge S2. Keep pricing through `aetheria.trade_value_policy.v1`. | `InventoryPanel`, `InventoryMenu`, and `TradeMenu` may adapt Unity UI controls, but stock/refit truth cannot come from manager-global observed cargo, docked ship, or trade collections. Broad available-entity reads are deleted; any remaining facade-object bridge must be key-scoped and listed as a Stage 8 shim with the typed projection that will replace it. | Unity and Electron can inspect the same selected station/pawn inventory and submit the same typed refit operation through `AetheriaClient`; verifier searches reject new manager-global inventory/stock reads. |
| 5 | Gravity/object viewport parity | Finish using `ObjectsViewportAsync` and `GravityViewportAsync` wherever Unity currently asks for whole-zone data to draw map or tactical views. | Whole-zone daemon frame reads are kept only for renderer compatibility until 7D.5. | Electron and Unity use equivalent projection calls for XY map bounds. |
| 6 | Policy/editor surfaces | Extend the same typed Eve/CultUI authoring shape to Starbridge station/wave data after station/refit read parity is complete. | No return to Unity `GameSettings` as gameplay truth. | New policy surfaces must define typed bodies, command lowering, daemon bridge persistence, and verifier coverage before UI work depends on them. |

Proof:

```powershell
.\Aetheria.State\scripts\verify-stage7d-unity-parity.ps1
rg -n "ObservedGalaxy|LoadZone|ApplyDaemonFrame|GetComponent<|FindObjects|GameObject.Find|ZoneRenderer|ActionGameManager\\." Assets/Scripts Packages/org.gamecult.aetheria.state
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Allowed hits must be presentation-only, editor-only, or listed as Stage 8
demolition.

Demolition target:

- Unity mirrored hierarchy or manager singleton reads treated as gameplay
  authority.

Unlocks:

- 7D.5 render shell contraction.

### 7D.5 Unity Renderer/Input Shell Contract

Status: waiting for 7D.3 and 7D.4.

Purpose:

- Make the Unity shell vestigial enough that Stage 8 can delete without
  guessing.

Allowed edits:

- Reduce `ActionGameManager` responsibilities to input orchestration,
  presentation lifetime, and temporary compatibility shims.
- Reduce `ZoneRenderer` responsibilities to translating current local
  projection/render data into Unity render buffers.
- Keep Ymir/Aetheria state authoritative.

Forbidden edits:

- No Unity physics authority.
- No new simulation branch in Unity.
- No manager-owned gameplay state that Electron would also need.

Output:

- A documented list of remaining `ActionGameManager` and `ZoneRenderer`
  responsibilities, each classified as `keep renderer/input`, `shim`, or
  `delete in Stage 8`.

Initial 7D.5 responsibility map:

| Owner | Responsibility | Classification | Replacement or deletion condition |
| --- | --- | --- | --- |
| `ActionGameManager` | Unity input gesture orchestration for pilot movement, target selection, action bar, interaction, docking, tow, shields, heatsinks, and similar player input. | keep renderer/input | Keep only while it submits typed operations through `AetheriaClient.Operations`; delete any branch that mutates gameplay state locally. |
| `ActionGameManager` | Presentation lifetime and menu/camera choreography around the current Unity scene. | keep renderer/input | Keep until a smaller Unity shell owns scene lifecycle directly. No simulation decisions may live here. |
| `ActionGameManager` | `ObservedGalaxy`, `FindObservedGalaxyZone`, observed zone context construction, and daemon-run facade projection. | shim | Delete in Stage 8 after sector topology, current-zone, current-entity, station/refit, and zone render-feed projections cover all remaining callers. No new callers allowed. |
| `ActionGameManager` | `TryGetObservedZoneSnapshot`, `TryGetObservedRunZone`, and public helper reads that return Unity facade objects. | shim | Make private or delete as each panel/HUD/camera caller moves to typed projections. Public portable state reads must live on `AetheriaClient`. |
| `ActionGameManager` | Item/loadout/entity identity resolution helpers used only to bridge existing Unity facade objects to daemon keys. | shim | Move identity projection into daemon/state code or renderer-local cache, then delete the helpers. |
| `ZoneRenderer` | Render-time entity/body/pickup instances, click affordances, materials, labels, camera targets, minimap icons, and SoA/native render binding. | keep renderer/input | Keep as the Unity presentation adapter; it consumes projections/native views and exposes no gameplay authority. |
| `ZoneRenderer` | `ApplyDaemonFrame` render updates over the current zone. | shim | Replace with a typed render-frame/native-view feed. Until then it is allowed only as frame-to-render-cache lowering. |
| `ZoneRenderer` | `LoadDaemonZoneView` clearing/rebuilding a Unity hierarchy from daemon zone contents. | delete in Stage 8 | Remove when `ZoneRenderer` can render from projection/native slabs without owning a mirrored level hierarchy. |
| Unity colliders and click physics bridges | Click targeting, selection, hull affordances, and presentation queries backed by Ymir/Aetheria state. | keep renderer/input | Keep only as UI/render affordances. Any gameplay collision or overlap query belongs in Ymir. |
| Unity `Rigidbody`, `Collider`, or `Physics.*` simulation paths | Any Unity-owned physical truth. | delete in Stage 8 | Forbidden for runtime authority; replace with Ymir queries/projections. |

Proof:

```powershell
rg -n "ActionGameManager|ZoneRenderer|Rigidbody|Collider|Physics\\.|ObservedGalaxy|LoadZone" Assets Packages/org.gamecult.aetheria.state
```

Every hit is classified. Runtime tests from 7D.0 still pass.

Demolition target:

- Unclassified Unity gameplay shell behavior.

Unlocks:

- 7E cross-runtime co-op smoke.

Demolition target:

- Unity-only gameplay command paths.
- Persistent Unity mirror hierarchy as gameplay state.
- Unity physics as runtime authority.

Verifier:

```powershell
rg -n "ActionGameManager|ZoneRenderer|Rigidbody|Collider|Physics\\." Assets Packages/org.gamecult.aetheria.state
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Every remaining Unity physics or gameplay shell hit must be presentation-only,
input-only, editor-only, or explicitly queued for Stage 8 deletion.

Stop line:

- Do not delete major Unity shell code until Unity can render and command
  through the shared facade.

## 7E Cross-Runtime Co-op Smoke

Status: waiting for 7D.

Inputs:

- Electron thin client from 7C.
- Unity thin client from 7D.
- Stage 6 peer committed fact import.

Build:

- Raven Unity and Starfire Electron each launch or attach to a local Verse node.
- Both nodes use the same verse id and peer endpoints.
- Each runtime submits only local typed commands.
- Each runtime observes remote facts through local state.

Demolition target:

- Client code treating peer viewport or peer UI state as authoritative gameplay.

Verifier:

- Unity controls a pawn.
- Electron observes that pawn in map view.
- Electron commands owned units.
- Unity observes remote commanded units through the same daemon state.
- Both local daemons converge on imported committed facts.

Stop line:

- Stage 8 Unity shell demolition starts only after this passes.

## Stage 8 Entry Contract

Stage 8 may delete or collapse Unity gameplay shell code only when:

- Electron map and selected panels read local projections.
- Unity uses the same command/read contract shape.
- Cross-runtime smoke passes.
- The inventory has no unowned mutation/read surface.
- The verifier suite passes.

Until then, Stage 8 work is allowed only as quarantine notes, not demolition.
