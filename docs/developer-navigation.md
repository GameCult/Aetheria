# Aetheria Developer Navigation

Date: 2026-06-25

This is the handoff map for working in the post-migration Aetheria tree. It is
written for someone who already knows original Aetheria and needs to understand
what changed, where the new seams are, and how to keep moving without being
ambushed by half-finished architecture.

The short version: Unity is being reduced to a renderer, input surface, and
editor environment. Durable game state and portable gameplay facts now live in
typed CultCache/CultNet/CultMesh records under `Aetheria.State`. The daemon is
the authority over canonical shared documents, not a private truth source that
exports a second client copy. Unity and the Electron RTS client are clients of
the same typed Verse state.

## Start Here

Read these in this order:

1. `docs/developer-navigation.md`
   You are here. This is the repo map and daily working guide.
2. `docs/aetheria-current-codebase-model.md`
   Control-flow model of the codebase before the latest Stage 7 work. Some
   names have moved, but it is still the best broad map.
3. `docs/game-modes-and-progression.md`
   Product-mode and cross-mode progression authority: Terminus, Starbridge,
   Arena, Hangar, deployment, and settlement.
4. `docs/hangar-launcher.md`
   Shared launcher, deployment admission, ship-bay composition, and the cut
   from generic New Game to mode bootstrap.
5. `Aetheria.State/docs/stage-7-thin-client-staged-implementation-plan.md`
   The staged migration plan. Use this when deciding what to build next.
6. `Aetheria.State/docs/stage-7-client-surface-inventory.md`
   Surface inventory and edit queue for Unity/Electron/client boundaries.
7. `docs/aetheria-verse-client-contract.md`
   Intended client contract: use typed Verse state, not daemon internals.
8. `docs/cultmesh-feature-implementation-guide.md`
   Step-by-step guide for adding canonical typed state once, letting daemon
   authority own mutation, and consuming/interacting with the same document from
   Unity and other clients.
9. `Aetheria.State/docs/verse-authority-implementation-plan.md`
   Authority and Starbridge staged implementation map.
10. `docs/cockpit-doctrine-combat.md`
   Target pilot experience: first-person cockpit, direct and delegated helm,
   doctrine-owned combat intent, cognition-owned precision execution, and the
   typed state required to make delegation legible. This defines experience,
   while the authority implementation plan remains authoritative for build order.

Longer background/reference docs:

- `docs/aetheria-perfect-machine-map.md`
- `Aetheria.State/docs/verse-daemon-shape.md`
- `Aetheria.State/docs/verse-authority-policy.md`
- `Aetheria.State/docs/cultmesh-cross-runtime-primitives.md`
- `Aetheria.State/docs/cultmesh-ergonomics-staged-migration-plan.md`
- `Aetheria.State/docs/daemon-code-map-for-rust-port.md`
- `Aetheria.State/docs/rust-rebuild-freeze-checklist.md`

## What Changed

Original Aetheria had a lot of live gameplay state flowing through Unity object
graphs: `ActionGameManager`, `Zone`, `Entity`, behavior instances, Unity UI
panels, save files, catalog DTOs, and renderer caches all knew more than they
should.

The current migration is moving toward this shape:

```text
client input
  -> typed client handles
  -> typed command document
  -> local Verse node
  -> daemon command gate
  -> simulation
  -> typed daemon frame/fact documents
  -> local Verse replica
  -> managed typed document/query or native SoA view
  -> Unity/Electron rendering and UI
```

The important shift is not "Unity talks to a daemon over a side channel." The
important shift is "every runtime is a CultMesh participant reading and writing
typed Verse state."

## Main Projects

`Aetheria.State`

Durable typed state and shared runtime contract. This is the center of the new
world. It owns document types, schema registration, state mapping, Eve command
bridges, provider advertisement, player settings, loadout templates, trade
policy, Verse target state, and managed derived document/query helpers.

`Aetheria.State.Daemon`

The local daemon process. It opens the state node, starts CultMesh, accepts Eve
and daemon command documents, ticks the authoritative run state, and republishes
daemon frame/SoA/health/provider/surface records.

`Packages/org.gamecult.aetheria.state/Runtime`

The Unity-embeddable runtime state package. Unity, tests, daemon, and Electron
binding generation all depend on this contract. This package contains the
shared C# client handles (`AetheriaClient`, `AetheriaControl`, `AetheriaUi`),
daemon command documents, RTS/current-entity documents, Starbridge documents,
authority policy, SoA documents, Eve surface builders, and runtime catalog
snapshots.

`Assets/Scripts`

Unity client, legacy simulation shell, UI, renderer, and migration adapters.
This is no longer supposed to be the source of portable gameplay truth. Much of
it still exists as a presentation adapter over old object shapes while the
daemon contract fills in.

`Aetheria.Rts.Web`

Electron RTS client. It is intentionally thin: browser UI plus Electron
CultMesh/Verse access, generated query/document reads, and typed operation helpers.
Generated TypeScript bindings live in
`Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` and are derived
from C# `[Key]` declarations.

`Aetheria.State.Verify`

The fence. If a migration rule matters, this project should enforce it.
Whenever you delete a bad path or promote a new typed surface, add a verifier
assertion so the old path does not grow back.

`Aetheria.State.AuthoritySmoke`

Smoke coverage for authority/session behavior. This is a good "does the
co-op/authority spine still breathe?" check.

`Aetheria.State.Freeze`

Documentation/freeze support for the C# API/Rust-port discussion. The Rust
rebuild is shelved for MVP purposes, but the mapping is useful context.

## Daily Commands

Run these from repo root unless noted.

Core verifier:

```powershell
dotnet run --project .\Aetheria.State.Verify\Aetheria.State.Verify.csproj
```

Unity/runtime compile:

```powershell
dotnet build .\GameCult.Aetheria.State.Unity.csproj --no-restore -v:minimal
```

Replica compile:

```powershell
dotnet build .\Aetheria.State.Replica\Aetheria.State.Replica.csproj --no-restore -v:minimal
```

Authority smoke:

```powershell
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Stage 7 Unity parity:

```powershell
.\Aetheria.State\scripts\verify-stage7d-unity-parity.ps1
```

RTS/Electron contract checks:

```powershell
cd .\Aetheria.Rts.Web
npm run check:rts-bindings
npm run verify:stage7c:electron
```

Regenerate RTS bindings after changing C# `[MessagePackObject]` or `[Key]`
contracts consumed by Electron:

```powershell
cd .\Aetheria.Rts.Web
npm run generate:rts-bindings
```

Expected recurring warnings:

- `System.Numerics.Vectors` version conflict from Unity/Brokkr reference
  overlap. This is noisy but currently non-fatal.
- Nullable warnings in some runtime surface builders. They are not new proof of
  failure unless they become errors or touch code you are actively changing.

## Current Mental Model

### State Owner

Typed CultCache/CultNet/CultMesh documents are the state owner. For normal
gameplay features, define one canonical shared document and let authority policy
decide who may write or predict it. Do not create "daemon truth" plus a separate
client-facing copy unless the second document is intentionally filtered,
aggregated, windowed, lossy, SoA/native, or a named compatibility bridge. The
primary world file is:

```text
GameData/aetheria-world.cc
```

Local client target state also exists so Unity can resolve which Verse to open.
Do not resurrect ad-hoc local files for gameplay state, loadouts, player
settings, command ports, or viewport payload passing.

### Daemon Owner

`Aetheria.State.Daemon/Program.cs` is the process entry. The important runtime
tick boundary is in:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonTickRunner.cs
```

Command application is in:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperations.cs
```

### Client Handles

Use `AetheriaClient` first:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaClient.cs
```

It exposes generic managed document reads such as `State.Latest<TDocument>()`
and `State.Reactive<TDocument>()`, parameterized handles for viewport/detail
documents, Starbridge seat handles, typed controls through `AetheriaControl`,
and typed UI surface helpers through `AetheriaUi`.

If a Unity panel or Electron surface needs a fact, prefer the generic managed
document call first. Add a named handle only when it carries semantic identity,
parameters, operation policy, or a distinct derived/native shape. Avoid handing
callers raw daemon frames unless the caller is an explicitly fenced transition.

### Unity Shell

Unity should do:

- input capture;
- camera and presentation;
- UI lowering;
- local view caches;
- Burst/DOTS/native rendering eventually;
- editor affordances.

Unity should not do:

- portable gameplay authority;
- server-authoritative simulation ticks;
- trade pricing truth;
- persistent loadout/player settings truth;
- stringly public command APIs;
- ad-hoc queues or buses as gameplay boundaries;
- Unity physics authority.

`ActionGameManager` is now a shell and compatibility adapter. It is still big,
but it is no longer the place to add new portable gameplay state.

### Electron RTS Client

The RTS client should have the same shape as Unity:

- local typed Verse/CultMesh access;
- typed viewport/object/gravity/current selection reads;
- typed command operations;
- no bespoke gameplay rules except UI presentation and input behavior.

Do not add gameplay-only truth to `Aetheria.Rts.Web/Client/app.ts`. If it looks
like a simulation rule, it belongs in the daemon/state layer.

## Important Files By Task

Add or inspect a typed runtime document:

- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeSnapshotDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeGameViewportDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeStarbridgeDocuments.cs`
- `Aetheria.State/AetheriaDocumentRegistry.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseClient.cs`

Add or inspect a daemon command:

- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationClient.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationsClient.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaControl.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperations.cs`
- `Aetheria.State.Verify/Program.cs`

Add a managed derived document/query:

- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeGameDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaClient.cs`
- `Aetheria.Rts.Web/Electron/aetheria-rts-local-documents.ts`
- `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts`
- `Aetheria.State.Verify/Program.cs`

Work on Unity thin-client migration:

- `Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs`
- `Assets/Scripts/Gameplay/AetheriaUnityGameplayBootShell.cs`
- `Assets/Scripts/Gameplay/AetheriaUnityGameplayInputShell.cs`
- `Assets/Scripts/Gameplay/AetheriaUnityObservedFrameApplier.cs`
- `Assets/Scripts/Gameplay/AetheriaUnityCurrentEntityBinder.cs`
- `Assets/Scripts/Gameplay/AetheriaUnityTargetPresentation.cs`
- `Assets/Scripts/UI/Menu/*.cs`
- `Assets/Scripts/UI/HUD/SchematicDisplay.cs`
- `Assets/Scripts/Zone Display/ZoneRenderer.cs`

Work on Eve/CultUI surfaces:

- `GameCult.Eve.Surface.EveSurfaceDocument` for daemon-published CultUI/Eve surfaces
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeSurfaceDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeEveCommandClient.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntime*SurfaceBuilder.cs`
- `Packages/org.gamecult.aetheria.eve-runtime/Runtime/AetheriaEveSurfacePresenter.cs`
- `Packages/org.gamecult.aetheria.eve-runtime/Runtime/AetheriaEveUnitySurfaceHost.cs`

Work on RTS/Electron:

- `Aetheria.Rts.Web/Client/app.ts`
- `Aetheria.Rts.Web/Client/aetheria-rts-contract.ts`
- `Aetheria.Rts.Web/Electron/main.ts`
- `Aetheria.Rts.Web/Electron/preload.cjs`
- `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts`
- `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts`
- `Aetheria.Rts.Web/Electron/aetheria-rts-local-documents.ts`
- `Aetheria.Rts.Web/scripts/generate-rts-bindings.mjs`

Work on native/SoA render path:

- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaDocuments.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaFramePublisher.cs`
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaViewIndex.cs`
- `Assets/Scripts/Gameplay/AetheriaDaemonRenderNativeView.cs`
- `Assets/Scripts/Gameplay/AetheriaDaemonRenderBuffer.cs`
- `Assets/Scripts/Gameplay/AetheriaDaemonRenderMatrixJob.cs`
- `Assets/Scripts/Gameplay/AetheriaDaemonRenderGroupMatrixJob.cs`

Work on Ymir/physics:

- `Assets/Scripts/Gameplay/Physics/AetheriaYmirPhysicsBridge.cs`
- `Assets/Scripts/Gameplay/Weapons/*Manager.cs`
- `Assets/Scripts/Gameplay/Weapons/*Projectile*.cs`
- CultMath/Ymir code may live outside this repo; verify paths before editing.

## Migration Rules

1. Public client APIs must be typed.
   No public `Apply(command, payload)`, string command names, or raw payload
   dictionaries.

2. If Unity needs a gameplay fact, ask whether Electron/RTS would need the same
   fact.
   If yes, promote it into `Aetheria.State` and read it through `AetheriaClient`.

3. If a fact is needed every frame, prefer a typed query/document or SoA/native view.
   Do not make Unity crawl a whole daemon frame in a hot path unless the code is
   explicitly transitional and fenced by the verifier.

4. Add verifier coverage when deleting or replacing authority paths.
   `Aetheria.State.Verify` and `verify-stage7d-unity-parity.ps1` are not busy
   work; they are the migration memory.

5. Keep Electron thin.
   RTS UI may select, display, and submit typed operations. It should not invent
   enemy AI, movement rules, trade rules, station rules, or combat rules.

6. Keep Unity honest.
   Unity may adapt old object graphs for presentation until Stage 8 finishes,
   but new state truth belongs in typed documents.

7. Do not preserve legacy shape for its own sake.
   Git preserves history. The migration should delete dead shims when the typed
   replacement exists and tests prove it.

8. Treat facade/projector/adapter/surface-builder chains as a design failure.
   One adapter at a true boundary is fine. Stacked translation layers used to
   recover one typed value mean the code is missing a CultMesh primitive,
   generated handle, canonical document, query, operation, pointer, or native
   view.

## Known Transitional Debt

These are not surprises; they are known pressure points.

- `ActionGameManager` still exists and is still too large. It has been reduced,
  but it remains a compatibility shell around input, scene wiring, and legacy
  presentation.
- Unity `Entity`, `Zone`, and behavior objects still exist as presentation and
  compatibility adapters. Do not treat them as the final state authority.
- `ZoneRenderer` still has persistent presentation caches. Stage 8 should keep
  crushing it down toward per-frame query/native rendering.
- Some command bodies are still a broad typed union document selected by
  `AetheriaRuntimeDaemonCommandKinds`. This is typed at the document boundary,
  but not as elegant as distinct operation bodies.
- Some TypeScript generated binding metadata covers document slots better than
  nested ergonomic decode helpers. After C# contract changes, regenerate and
  check bindings.
- The C# daemon is still the MVP path. The Rust rebuild notes are context, not
  the current target.
- Physics is supposed to move to Ymir. Unity physics authority is forbidden, but
  some Unity-side presentation/bridge code still exists.

## How To Add A New Gameplay Surface

Use this checklist before writing code:

1. Name the owner.
   Is this daemon state, client presentation, local input configuration, or
   editor-only tooling?

2. Define the typed shape.
   Add or extend a MessagePack/CultNet document in the runtime package. Use
   explicit `[Key]` slots and stable names.

3. Add the simplest access path.
   Prefer `AetheriaClient.State.Latest<TDocument>()` or
   `AetheriaClient.State.Reactive<TDocument>()` for canonical documents. Add a
   named handle only when type alone cannot identify the document, such as
   parameterized viewport/detail state or multiple documents sharing one CLR
   type. Use `AetheriaControl` or `AetheriaUi` for typed operations and UI
   commands.

4. Lower Unity and Electron through the same semantics.
   Unity and RTS do not need identical UI, but they should observe the same
   facts and submit the same operation when policy allows it.

5. Add or update generated TS bindings.
   Run `npm run generate:rts-bindings` from `Aetheria.Rts.Web` when contract
   slots change.

6. Fence the old path.
   Add checks to `Aetheria.State.Verify/Program.cs` or the Stage 7 parity
   script.

7. Run the relevant gates.
   At minimum: state verifier, Unity compile, parity script, and RTS binding
   check when TS-visible contracts changed.

## How To Find Things Fast

Search for a typed schema:

```powershell
rg -n "gamecult\.aetheria\..*\.v1|CultDocument|MessagePackObject" Aetheria.State Packages/org.gamecult.aetheria.state/Runtime
```

Search for Unity still touching manager/global state:

```powershell
rg -n "ActionGameManager\.|RuntimeCatalog|RuntimePlayerSettings|TryGetObserved" Assets/Scripts -g "*.cs"
```

Search for stringly command smells:

```powershell
rg -n "Apply\(command|payload|Dictionary<string|commandKind|CommandPort|EventBus|ConcurrentQueue|Channel" Assets/Scripts Packages/org.gamecult.aetheria.state Aetheria.Rts.Web
```

Search for Unity physics authority:

```powershell
rg -n "OnCollision|OnTrigger|Rigidbody|Collider|Physics\." Assets/Scripts -g "*.cs"
```

Search for RTS contract drift:

```powershell
rg -n "aetheriaRuntime.*Slots|AetheriaRuntime.*Slot" Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts
```

## Git And Generated Files

Keep these out of commits:

- `Aetheria.Rts.Web/node_modules/`
- `Aetheria.Rts.Web/electron-dist/`
- `Aetheria.Rts.Web/bin/`
- `Aetheria.Rts.Web/obj/`
- `Aetheria.Rts.Web/logs/`
- `Aetheria.Rts.Web/runtime/`
- `Aetheria.Rts.Web/wwwroot/*.js`
- `Aetheria.State.* / bin` and `obj`
- local `.cc.records` caches

Commit these when changed deliberately:

- C# source and Unity `.meta` files for new Unity scripts;
- `Aetheria.Rts.Web/package.json` and `package-lock.json`;
- TypeScript source under `Client/` and `Electron/`;
- generated RTS binding metadata when C# document slots change;
- docs and verifier scripts;
- checked-in state seed files only when intentionally regenerated.

## If You Are Nervous

That is reasonable. This tree has been through a forceful migration and some of
the names are newer than the instincts you built from the original codebase.
The useful way to look at it is:

- your original simulation concepts are still recognizable;
- the authority boundary moved out of Unity;
- the new code is trying to make those concepts portable across Unity, RTS,
  daemon, and later clients;
- anything that feels like ceremony should either be defended by a verifier or
  deleted in a later cleanup pass.

When in doubt, follow the typed state:

```text
canonical document -> managed CultMesh handle -> client presentation/input
typed operation -> daemon authority -> same canonical document
projection/native view -> only when the shape is intentionally different
```

If a path does not fit that shape, it is probably transitional debt or a bug.
