# Verse Authority Implementation Plan

This is the staged build map for the Aetheria co-op Verse architecture. It is
written to prevent Jenga: every stage has one owner, one allowed state flow,
one demolition target, and one verifier. If a stage cannot pass its verifier,
do not build the next stage on top of it.

## North Star

Raven and Starfire each run a local CultMesh/CultNet Verse node.

- Unity Raven is a thin renderer/input runtime for Raven-owned claims.
- Electron/RTS Starfire is a thin renderer/input runtime for RTS-owned claims.
- Aetheria state is synchronized as typed Verse documents and typed committed
  facts.
- Authority is selected by typed policy state, not by client type, transport
  branch, string operation names, or viewport queries.
- Future authority modes remain representable without forcing the first trusted
  co-op implementation to carry consensus machinery.

The immediate product target is Starbridge: one RTS chair and one to four pilot
clients defending the same base through shared Verse state. The RTS runtime
naturally authors base, wave, hostile, infrastructure, fabrication,
drone/turret, station-stock, construction-ghost, target-mark, and commander
support claims. Pilot runtimes naturally author their own movement, local
combat, salvage, docking, refit, anchoring, cooling, repair, survival-pod,
cargo/equipment, and local target-mark claims. These are policy-scoped claim
kinds, not client-type privileges.

The release-facing source for that product target is
`E:/Projects/AetheriaLore/Aetheria/Game Design/Aetheria Starbridge.md`. This
plan translates that design into code gates; it is not allowed to turn
Starbridge into a generic RTS-client migration. Starbridge is the proof that
Verse authority, typed state, station stock, equipment, heat, repair, salvage,
construction, waves, and recovery are all one shared war machine.

## Hard Rules

1. No public stringly command APIs.
2. No remote viewport query may become gameplay state exchange.
3. No client may own simulation behavior that another runtime would also need.
4. No hidden command ingress. Every mutation path must hit the authority gate.
5. No unscoped "sync the world" path in hot runtime code. Snapshot sync is
   allowed only as typed, scoped Verse document exchange or as a diagnostic.
6. No compatibility layer survives without a deletion ticket in this document.
7. A stage is done only when its verifier passes and its demolition target is
   gone or explicitly quarantined.
8. Starbridge role verbs must enter the system as typed operations,
   projections, or lease-policy state. A UI role may filter or prioritize those
   verbs, but may not define a parallel gameplay branch.
9. Reusable client ergonomics belong in CultMesh/CultLib primitives, not in
   Aetheria-specific bridges. A state pointer, operation handle, query surface,
   projection recipe, authority scope, watch, or native slab descriptor that
   Unity and Electron both need must be hoisted to the shared cross-runtime
   layer.

## CultMesh Primitive Dependency

Aetheria is a pressure test for CultMesh. The desired local sugar is not an
Aetheria facade that happens to work in Unity; it is a shared CultMesh surface
that can be generated or adapted into C#, TypeScript, Rust, WASM, Eve/CultUI,
Odin/Bifrost tools, and co-located native renderers.

The shared roadmap lives at
`E:/Projects/CultLib/src/GameCult.Mesh/docs/cross-runtime-primitives-roadmap.md`.
Stage 7 and Stage 8 work should push reusable abstractions there whenever the
same semantic shape appears in more than one runtime.

Immediate Aetheria mappings:

| Aetheria shape | CultMesh primitive it should become |
| --- | --- |
| `SectorMapAsync`, Starbridge session summary, daemon health, authority policy | typed state pointers |
| Objects, gravity, selected object, inventory, stats, station stock | typed query surfaces or projection recipes |
| Pilot movement, targeting, equipment activation, docking, cooling, repair, salvage, commander orders | typed operation handles with authority claims |
| Eve/CultUI state refs and command buttons | typed state pointers plus operation handles embedded in the surface |
| Zone render SoA and Ymir physics state | native slice view descriptors with runtime adapters |
| Unity/Electron/Rust binding parity | generated schema, slot, enum, query, pointer, and operation descriptors |

## System Control Flow

Target runtime flow:

```text
local input
  -> typed command/proposal document
  -> local Verse node
  -> daemon tick command gate
  -> authority policy decision
  -> simulation mutation
  -> typed committed command fact
  -> local Verse document publication
  -> peer scoped fact sync
  -> peer fact import gate
  -> peer local state mutation
  -> local viewport/projection query
  -> renderer
```

Forbidden runtime flow:

```text
local client
  -> ask remote daemon for gameplay viewport
  -> render/use remote projection as authoritative game state
```

Diagnostic and editor tools may ask a remote runtime for a projection, but game
clients must converge their local Verse state from typed facts.

## Current Code Map

| Area | Current responsibility | Desired responsibility |
| --- | --- | --- |
| `Aetheria.State.Daemon/Program.cs` | daemon boot, tick loop, RUDP endpoint, command gate, fact publication | boot/tick host only; policy/fact/sync logic extracted behind typed services |
| `AetheriaStateNode` | full daemon document registry and typed state helpers | canonical typed state facade for daemon-owned documents |
| `AetheriaRuntimeVerseClient` | client-side typed state facade, but registry currently omits newer authority/fact documents | canonical thin client facade over the same typed Verse documents clients need |
| `AetheriaRuntimeAuthorityRouter` | pure policy decision engine | keep pure, allocation-light, topology-free |
| `AetheriaRuntimeDaemonTickRunner` | simulation tick and operation execution | only place local commands mutate sim state |
| `AetheriaRuntimeCommittedFactImporter` | replays trusted remote facts through policy gate | peer convergence path, eventually used by daemon loop |
| `AetheriaVerseReplica.SyncSnapshotAsync` | broad snapshot helper used by smoke tests | diagnostic/scoped transport verifier, not gameplay architecture |
| RTS web/Electron client | map renderer plus command sender | thin local Verse client using same state loop as Unity |
| Unity shell | still has legacy gameplay shell pressure | input/rendering only, local Verse command submitter and state renderer |

## Stage Graph

```text
0. boundary inventory
  -> 1. typed policy schema
  -> 2. command gate
  -> 3. deterministic two-role proof
  -> 4. scoped live Verse document exchange
  -> 5. live committed fact publication
  -> 6. live committed fact import
  -> 7. thin client mode parity
  -> 8. Unity gameplay shell demolition
  -> 9. leases
  -> 10. future witness/quorum modes
```

Each stage consumes only artifacts from earlier stages. If stage 6 needs a
transport feature from stage 4, build that feature in stage 4 first.

## Build Contract

This plan is a runbook, not a suggestion list. A stage may create only the
artifacts listed in that stage, may depend only on earlier completed stages, and
must delete or quarantine the named demolition target before the next stage
starts.

The active lane is:

```text
Stage 6 live fact import
  -> Stage 7 client parity
  -> Stage 8 Unity shell demolition
```

Blocked lanes:

- Do not build leases until Stage 7 passes.
- Do not expand RTS gameplay features until Stage 7 proves it is a thin Verse
  client.
- Do not optimize Unity rendering around mirrored gameplay hierarchy until
  Stage 8 decides what remains of Unity's local shell.
- Do not add witness/quorum machinery until the trusted co-op fast path is
  clean.

Stage gates:

| Gate | Must prove | Must not introduce |
| --- | --- | --- |
| Transport gate | Scoped typed documents can move between live daemons | broad hot-loop shard sync |
| Fact gate | Remote committed facts mutate local state through the same authority router | viewport-as-gameplay-state |
| Client gate | Unity and Electron submit the same typed command documents | client-specific gameplay behavior |
| Demolition gate | Unity has no authoritative gameplay state | new Unity-side simulation branches |

Any implementation task that does not move the current gate forward must be
either a verifier, a demolition step, or a documented dependency fix.

## Dependency Ledger

Stage 6 depends on:

- Stage 4 scoped document transport;
- Stage 5 committed command fact documents;
- authority router decisions from Stage 1;
- daemon command/tick accounting from Stage 2;
- the two-role smoke harness from Stage 3.

Stage 7 depends on Stage 6 and may not invent another command surface. The RTS
client's current `command` and `viewport` IPC shape is tolerated only as a
diagnostic shell around CultMesh documents until Stage 7 replaces it with
typed local Verse submission and local projection reads.

Stage 8 depends on Stage 7 and may delete Unity shell concepts only after the
same state and projection access path is available from Unity and Electron.

## Current Stop Line

Stop building upward when any of these is true:

- live daemon-loop import cannot converge from peer facts;
- remote state is being read through a projection that is then treated as
  authoritative gameplay;
- a client-facing API accepts operation names as strings or untyped payloads;
- a new queue/bus exists between input and the daemon authority gate;
- a mutation point is not listed in Stage 0.

## Stage 0: Boundary Inventory

Status: started, still open because RTS and Unity client cleanup are not
complete.

Owner: docs and search harness.

Allowed work:

- Map every command ingress.
- Map every simulation mutation point.
- Map every runtime projection/query path.

Known command ingress:

- daemon CultNet document put;
- `AetheriaStateNode.SubmitDaemonCommandAsync`;
- `AetheriaRuntimeVerseClient.SubmitDaemonCommandAsync`;
- Unity `AetheriaRuntimeDaemonOperationClient`;
- RTS `AetheriaCultMeshClient.command`;
- Eve command lowering.

Known mutation points:

- `AetheriaRuntimeDaemonOperations.Execute`;
- `AetheriaRuntimeRtsSimulation.Step`;
- fact import through `AetheriaRuntimeCommittedFactImporter`;
- any Unity-side gameplay mutation found by follow-up audit.

Demolition target:

- Any command path that bypasses the daemon authority gate.

Verifier:

- A repository search note or smoke assertion listing all command/mutation
  ingress paths. No later stage may add an unlisted ingress.
- Search terms that must stay reviewed:
  - `SubmitDaemonCommand`
  - `command(`
  - `Apply(`
  - `SyncSnapshotAsync`
  - `viewport`
  - `ZoneRenderer`
  - `ActionGameManager`
  - `Queue`, `Channel`, `ConcurrentQueue`

Exit criteria:

- Boundary map is current.
- New mutation code can name the stage that authorizes it.

## Stage 1: Typed Policy Schema And Router

Status: implemented for first trusted co-op slice.

Owner: `AetheriaRuntimeAuthorityRouter` and authority documents.

Build:

- `AetheriaRuntimeVerseAuthorityPolicyDocument`
- `AetheriaRuntimeAuthorityRule`
- `AetheriaRuntimeAuthorityLeaseDocument`
- command metadata:
  - `AuthorRuntimeId`
  - `SubjectKey`
  - `ClaimKind`

Supported now:

- `any-trusted-runtime`
- `host-authoritative`
- `delegated-runtime`
- `interest-lease`

Represented but fail closed:

- `owning-runtime`
- `witness-quorum`
- `operator-finality`
- `mergeable-crdt`

Demolition target:

- Client-type or topology checks inside gameplay operations.

Verifier:

- `Aetheria.State.AuthoritySmoke` pure router checks.
- Unsupported modes reject through normal receipt paths.

Exit criteria:

- Router is pure.
- Router is not transport-aware.
- Router can run before every daemon tick.

## Stage 2: Daemon Command Gate

Status: implemented for daemon tick ingress.

Owner: daemon tick boundary.

Build:

- Read pending typed command documents.
- Load policy and leases.
- Reject unauthorized commands before simulation mutation.
- Pass only authorized commands into `AetheriaRuntimeDaemonTickRunner`.
- Publish applied/rejected receipts in latest frame.
- Keep cumulative applied/rejected receipts for followers that miss the exact
  tick.

Allowed flow:

```text
typed command document -> authority router -> tick runner
```

Demolition target:

- Any direct call from client command input into simulation operations.

Verifier:

- `Aetheria.State.AuthoritySmoke` verifies accepted and rejected command
  receipts.
- `dotnet build Aetheria.State.Daemon/Aetheria.State.Daemon.csproj`

Exit criteria:

- Unauthorized command ids are accounted.
- Accounted commands are not re-evaluated forever.
- The frame exposes the active authority shape for diagnostics.

## Stage 3: Deterministic Two-Role Proof

Status: implemented through in-process and once-mode daemon proofs.

Owner: `Aetheria.State.AuthoritySmoke`.

Build:

```text
raven-local
  runtime: raven-unity
  owns: Raven movement/combat claims

starfire-local
  runtime: starfire-rts
  owns: hostile/RTS claims
```

Verifier:

- In-process tick proof:
  - Raven-authored Raven movement applies.
  - Raven-authored hostile movement rejects.
  - Starfire-authored hostile movement applies.
  - Starfire-authored Raven movement rejects.
- Once-mode daemon process proof with the same receipt shape.

Demolition target:

- Any test or client path that assumes "local daemon means all authority."

Exit criteria:

- The same typed policy document loads in both stores.
- Two real daemon processes make opposite decisions deterministically.

## Stage 4: Scoped Live Verse Document Exchange

Status: implemented for frame and committed-fact visibility.

Owner: CultNet/CultMesh scoped snapshot transport plus Aetheria verifier.

Problem:

- The live smoke currently uses `AetheriaVerseReplica.SyncSnapshotAsync`, which
  asks for broad snapshots. After committed facts were added, that path times
  out while children remain alive. Even if fixed by timeout tuning, broad sync
  is the wrong runtime primitive.

Build:

- A scoped snapshot/fetch path that requests explicit schema ids and record
  keys.
- A verifier that asks only for:
  - latest daemon frame;
  - committed command fact schema or deterministic fact record keys;
- later, specific policy/lease docs when needed.
- The client/runtime facade registry must include every typed document it asks
  to sync.
- Scoped snapshot application must behave like a typed document patch, not a
  full shard replacement. Full shard replacement remains a diagnostic/replica
  path.
- The daemon endpoint must explicitly project latest frames and committed facts
  when requested so live Verse reads are not dependent on broad cache snapshots.

Allowed flow:

```text
peer endpoint -> scoped typed document request -> local Verse cache
```

Forbidden flow:

```text
peer endpoint -> full shard snapshot -> gameplay state path
```

Demolition target:

- Broad snapshot calls in authority smoke. Completed for the focused authority
  verifier.
- Broad snapshot calls in client runtime code. Still to audit before Stage 7.

Verifier:

- Concurrent daemon smoke can observe the peer's latest frame through a scoped
  request.
- The same smoke can observe committed fact documents through a scoped request.
- Failure diagnostics include endpoint, requested schema ids, record keys, and
  child process status.

Exit criteria:

- No hot co-op verifier depends on unfiltered shard snapshots.
- Scoped fetch is a reusable CultNet/CultMesh primitive, not a one-off string
  helper hidden in the test.

Verification:

- `dotnet build Aetheria.State.Daemon\Aetheria.State.Daemon.csproj`
- `dotnet run --project Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj`

Notes:

- `CultNetSchemaShardSnapshotFetcherOptions` now accepts request-level schema
  and record-key filters.
- `AetheriaVerseReplica.SyncScopedSnapshotAsync` applies returned raw documents
  as a patch so scoped reads do not delete unrelated local Verse state.
- The authority smoke now waits separately for frame receipt visibility and
  committed fact visibility.
- Stage 6 satisfied the daemon hot-loop requirement through direct scoped typed
  fetches. The running daemon now asks a peer only for committed fact documents
  and decodes them through the CultNet document registry without opening a
  heavyweight replica node on every tick.

## Stage 5: Live Committed Fact Publication

Status: implemented for local commands and live peer visibility.

Owner: daemon tick result publisher.

Build:

- `AetheriaRuntimeCommittedCommandFactDocument`
- deterministic fact id/record key;
- applied fact creation for applied commands;
- rejected fact creation for policy-rejected commands;
- CultCache and CultNet registry entries;
- publication from the daemon tick after local command handling.

Allowed flow:

```text
tick result + original typed command -> committed command fact document
```

Demolition target:

- Tests constructing facts as the only proof of convergence.

Verifier:

- Live daemon peer can scoped-sync applied and rejected facts from another
  daemon.
- Fact documents contain source runtime, source daemon, source frame, subject,
  claim kind, command kind, outcome, and typed command payload.

Exit criteria:

- Every applied local command has a fact.
- Every policy-rejected local command that was observed has a fact.
- Facts are durable enough for a peer that samples later.

Verification:

- `Aetheria.State.AuthoritySmoke` proves that Starfire can observe Raven's
  applied Raven movement fact and Raven's rejected hostile fact.
- `Aetheria.State.AuthoritySmoke` proves that Raven can observe Starfire's
  applied hostile fact and Starfire's rejected Raven fact.

## Stage 6: Live Committed Fact Import

Status: implemented for the trusted co-op slice and verified by the live
two-daemon smoke.

Owner: fact importer and daemon peer loop.

Build:

- Poll/sync peer committed fact documents through Stage 4 scoped transport.
- Re-run authority policy using the fact's source runtime, subject, and claim.
- Reject unauthorized or malformed facts without local mutation.
- Keep imported fact ids so facts are idempotent.
- Publish import receipts.
- Use a direct scoped fact fetch or long-lived peer state reader. Do not open a
  new full `AetheriaStateNode` replica in the tick hot path.
- Keep import bookkeeping in frame state until the durable import ledger exists:
  - imported fact ids;
  - rejected imported fact ids;
  - duplicate imported fact ids;
  - cumulative imported fact ids;
  - cumulative rejected imported fact ids.

Allowed flow:

```text
remote committed fact -> local authority router -> tick runner import mode
```

Implementation slices:

1. Done: `AetheriaVerseReplica.FetchScopedSnapshotAsync` fetches a filtered raw
   CultNet snapshot without opening a replica state node.
2. Done: `AetheriaVerseReplica.FetchScopedDocumentsAsync<T>` decodes only the
   requested typed documents through the CultNet document registry.
3. Done: the daemon peer loop reads peer committed facts with the direct scoped
   fetch primitive and no broad replica open/pull.
4. Done: each peer fact is routed through
   `AetheriaRuntimeCommittedFactImporter`.
5. Done: import, duplicate, reject, cumulative import, and cumulative reject
   receipts are published into the daemon frame.
6. Done: the daemon publishes the final post-import frame locally after peer
   import so clients read the converged state, not the pre-import tick.
7. Done: the live two-daemon smoke requires local state convergence:
   - Starfire sees Raven movement through local state.
   - Raven sees Starfire hostile movement through local state.
   - unauthorized cross-role facts are rejected.

Demolition target:

- Remote viewport queries used as multiplayer gameplay state.
- Any per-tick peer import path that pulls or replaces the whole shard.

Verifier:

- Raven local Verse imports Starfire hostile/RTS facts.
- Starfire local Verse imports Raven pilot facts.
- Unauthorized hostile facts from Raven are rejected and leave local state
  unchanged.
- Duplicate facts do not replay.
- Diagnostics on failure include:
  - source daemon id;
  - peer endpoint;
  - requested schema ids;
  - fetched fact count;
  - imported/rejected/duplicate fact ids;
  - child daemon stdout/stderr tail.

Exit criteria:

- Starfire sees Raven movement through local state, not remote viewport reads.
- Raven sees Starfire hostile updates through local state, not remote viewport
  reads.
- The live smoke passes with two daemon processes and no broad snapshot in the
  runtime path.

Notes:

- The verifier caught a useful distinction: accepting a movement fact is not the
  same thing as applying movement locally. The smoke now seeds movement-capable
  entities and checks X/Z movement, matching the runtime's current RTS axes.
- Stage 6 proves the trusted co-op fact lane only. It does not grant clients
  permission to use remote viewport queries as gameplay state.

## Stage 7: Thin Client Mode Parity

Status: active. Stage 7A through 7C are implemented for the Electron proving
surface; Stage 7D.4 is the current gate after the initial shared C# facade and
typed Unity command reroute.

Owner: Unity and Electron runtime clients.

Build:

Stage 7 is the client parity stage. It is not an RTS feature pass. Its job is
to make Unity and Electron use the same local Verse shape before any more
gameplay is added.

Implementation slices:

1. Inventory every client-facing mutation/read path:
   - Electron main process IPC handlers;
   - RTS browser bridge/client calls;
   - Unity gameplay shell command submission;
   - Unity renderer/input reads;
   - daemon CultMesh request handlers used by clients.
2. Delete or quarantine public stringly client APIs:
   - no public `command(kind, payload)` shape;
   - no client-visible operation names as raw strings;
   - no untyped payload bags crossing client boundaries.
3. Replace Electron command submission with typed local Verse documents:
   - typed movement intent;
   - typed selection/control intent;
   - typed station/pawn interaction intents;
   - command ids and runtime ids created by the client runtime wrapper, not UI
     components.
4. Replace Electron gameplay reads with local projections:
   - map viewport projection over local Verse state;
   - selected object status projection;
   - inventory/cargo projection;
   - fog/visible set as the union of controlled unit visibility from local
     state.
5. Give Unity the same client package shape:
   - shared runtime configuration;
   - typed local Verse command submitter;
   - typed local projection reader;
   - peer health and authority policy status;
   - no Unity-only gameplay operation path.
6. Prove cross-runtime parity:
   - Raven Unity and Starfire Electron each launch their own local Verse node;
   - both nodes point at the same verse id and peer endpoints;
   - each node submits typed commands only to its local daemon;
   - each node observes converged remote facts through local state;
   - neither runtime reads a peer viewport as authoritative gameplay.
7. Only after the parity smoke passes, begin Stage 8 Unity shell demolition.

Shared runtime configuration:

- verse id;
- runtime id;
- peer endpoints;
- policy preset/id;
- local state path.

Client startup must expose:

- authority policy id;
- peer sync health;
- local daemon endpoint;
- local frame id/time;
- import receipt counters.

Stage 7 sub-gates:

### Stage 7A: Client Surface Inventory

Build artifacts:

- a checked-in inventory of every Unity and Electron mutation entry point;
- a checked-in inventory of every Unity and Electron gameplay read/projection
  entry point;
- a classification for each entry: keep, replace with typed local Verse, or
  delete in Stage 8.

Current artifact:

- `Aetheria.State/docs/stage-7-client-surface-inventory.md`
- `Aetheria.State/docs/stage-7-thin-client-staged-implementation-plan.md`

Current status:

- complete as the active inventory artifact;
- new command, viewport, queue, cached port, or bus hits must update this
  artifact before implementation continues.

Acceptance check:

- `rg` for public `command`, `viewport`, `Apply(command`, untyped payload bags,
  cached command ports, and ad-hoc buses has a reviewed owner/action for every
  hit.

Stop line:

- Do not build replacement abstractions until the mutation/read inventory is
  complete. Otherwise the old maze grows a new hallway.

### Stage 7B: Typed Client Runtime Contract

Build artifacts:

- a shared typed client runtime contract for local command submission;
- typed projection request/response documents for map, selected object status,
  inventory, cargo, peer health, and authority status;
- one runtime wrapper that owns command id, runtime id, verse id, and local
  daemon endpoint wiring.

Acceptance check:

- UI components can no longer construct transport payloads directly.
- Client code can submit only typed command documents.
- Client code can read only typed local documents/projections.

Current artifacts:

- `Aetheria.Rts.Web/Client/app.ts` uses typed `mapViewport`,
  `setMoveVector`, and `setTarget` calls instead of generic command/viewport
  calls.
- `Aetheria.Rts.Web/Electron/preload.cjs` and `main.ts` expose typed IPC
  method names only.
- `Aetheria.Rts.Web/Electron/aetheria-cultmesh.ts` is transport-focused and
  consumes binding helpers instead of owning MessagePack slot layout.
- `Aetheria.Rts.Web/scripts/generate-rts-bindings.mjs` derives schema ids,
  command kind ids, and MessagePack slot maps from the C# `[Key]` declarations.
- `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` is the
  generated contract metadata for command, frame, health, viewport, and
  authority documents, plus the nested snapshot payloads needed by local
  projection.
- `Aetheria.Rts.Web/Electron/aetheria-rts-bindings.ts` owns only ergonomic
  command encoders and public request/response types over the generated
  contract metadata.
- `Aetheria.Rts.Web/Electron/aetheria-rts-local-projection.ts` projects the
  latest daemon frame into the Electron map viewport locally, and also owns
  selected-object, inventory/cargo, daemon health, and authority-status
  projections.
- `Aetheria.Rts.Web/Electron/aetheria-local-publication-reader.ts` reads local
  CultCache publication sidecars for frame, health, and authority policy.
- `Aetheria.Rts.Web/scripts/verify-stage7c-local-runtime.ps1` proves the
  compiled Electron runtime facade can drive map, selection, inventory, health,
  and authority projections from a one-shot daemon's local publications.
- `Aetheria.Rts.Web/scripts/verify-stage7c-electron-shell.ps1` launches the real
  Electron app shell and proves the renderer can refresh through preload IPC
  using the same typed projection facade.
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs`
  owns typed map viewport projection over local daemon frame state.
- `Aetheria.State.Daemon/Program.cs` delegates the temporary RUDP RTS viewport
  snapshot hook to the shared projection API.
- `Aetheria.Rts.Web/scripts/verify-stage7b-rts-client.ps1` fails if public
  generic APIs return or if the Electron transport wrapper regains document
  layout ownership, fails if stale remote viewport decoder helpers return, and
  it fails if generated bindings are stale.
- `Aetheria.State.AuthoritySmoke` verifies projection behavior for controlled
  unit visibility union, status, inventory, and gravity influence intersections.

Remaining Stage 7B work:

1. Move Unity command/read paths onto the same typed local client shape proven by
   Electron.
2. Add a separate peer sync health projection if daemon health stops being the
   canonical peer health surface.
3. Replace the remaining semantic command construction arrays with a CultMesh TS
   typed document writer once that primitive is available locally.
4. Delete or narrow the daemon RTS viewport RUDP hook once no compatibility
   client or diagnostic path depends on it.

Stop line:

- Do not touch Unity gameplay shell demolition until Electron uses this
  contract first; Electron is the smaller proving surface.

### Stage 7C: Electron RTS Thin Client

Build artifacts:

- Electron main process launches or attaches to the local daemon;
- browser code talks to the typed client runtime wrapper, not raw IPC command
  strings;
- map rendering reads a local projection;
- selected station/pawn panels read local typed projections;
- player commands write typed local command documents.

Acceptance check:

- The RTS client can select owned stations and pawns, show status/inventory,
  and issue movement/interaction commands without bespoke gameplay simulation
  in the browser or Electron shell.

Stop line:

- If a feature needs gameplay logic, put the logic in daemon/state code and
  expose the result as typed state. The RTS client stays thin.

### Stage 7D: Unity Thin Client Parity

Build artifacts:

- Unity gets the same local Verse client contract as Electron;
- Unity input submits typed local command documents;
- Unity rendering reads local typed projections/state;
- `ActionGameManager` no longer owns simulation decisions needed by another
  runtime.

Acceptance check:

- Unity and Electron can run against equivalent local daemon setup with the
  same command/projection contract.

Stop line:

- Do not preserve a Unity-only command path for convenience. If Unity needs it,
  Electron or another runtime should be able to use the same typed operation.

### Stage 7E: Cross-Runtime Co-op Smoke

Build artifacts:

- a smoke that launches Raven Unity/client-shape and Starfire Electron/client-
  shape against separate local Verse nodes;
- both nodes peer through CultMesh using the same verse id;
- commands are submitted locally on both sides;
- observations are read locally on both sides.

Acceptance check:

- Raven movement appears in Starfire through Starfire local state.
- Starfire/RTS hostile or pawn commands appear in Raven through Raven local
  state.
- The smoke fails if either side uses a remote gameplay viewport to pass.

Stop line:

- Stage 8 starts only after this smoke passes.

Allowed flow:

```text
client input -> local typed command -> local daemon
local daemon state -> local viewport/projection -> renderer
```

Demolition target:

- Client-specific gameplay behavior branches.
- Cached command port abstractions that exist only to hide ad-hoc transport.
- RTS-only bespoke gameplay behaviors.
- `AetheriaCultMeshClient.command` as a public untyped/request-shaped gameplay
  API.
- RTS `viewport` IPC as a gameplay state source. It may remain only as a local
  projection request against local Verse state.

Verifier:

- Raven Unity and Starfire Electron on one machine observe the same converged
  facts.
- The RTS map sees Raven through local Verse state.
- Unity sees RTS/hostile state through local Verse state.

Exit criteria:

- Switching renderer does not change authority or simulation behavior.

## Stage 8: Unity Gameplay Shell Demolition

Status: not started.

Owner: Unity package cleanup.

Build:

- Reduce `ActionGameManager` to input/render orchestration or delete it.
- Move any remaining gameplay state mutation into daemon/state assemblies.
- Replace Unity-side game queries with local Verse projection reads.
- Treat Unity physics as forbidden runtime state authority.

Demolition target:

- Unity-side authoritative gameplay state.
- `ZoneRenderer.LoadZone` as a mirrored level hierarchy owner.
- Any XZ naming that leaks Unity 2D legacy into Aetheria domain APIs.
- Any Unity physics path that owns game truth instead of rendering/interpolating
  Ymir/Aetheria state.

Verifier:

- Unity can launch as a renderer/input shell against an already-running daemon.
- A non-Unity client can access equivalent state and projections.

Exit criteria:

- Aetheria can be viewed and controlled from another runtime without Unity.

## Stage 9: Authority Leases

Status: deferred until Stage 7 passes.

Owner: lease policy and diagnostics.

Build:

```text
subject: entity.hostile.7
claim kinds: combat, close-combat-response
runtime: raven-unity
window: time/frame bounded
scope: combat-near-raven
```

Starbridge lease examples:

- RTS grants a pilot close-defense response around a shield node.
- RTS grants temporary local drone reaction authority inside a leash envelope.
- A pilot with equipped cooling gear authors a bounded cooling-support claim for
  an overheated ally, turret, or base module.
- A pilot with equipped repair gear authors a bounded repair-support claim for
  a damaged structure or ship.
- Construction anchoring is a typed pilot claim over an RTS-authored ghost, not
  a client-local completion event.

Verifier:

- Starfire can lease close-combat response authority to Raven.
- Lease expiry returns authority to the base policy.
- Lease decisions are visible in receipts/diagnostics.

Demolition target:

- Hardcoded "near player means Unity owns it" branches.

Exit criteria:

- Leases are policy state, not gameplay special cases.

## Stage 10: Future Witness/Quorum Modes

Status: deliberately deferred.

Owner: future CultNet/CultMesh authority module.

Build later:

- simulation observations;
- quorum candidates;
- deterministic claim hashes;
- witness eligibility rules;
- operator finality/challenge rules;
- mergeable CRDT law framework where appropriate.

Verifier:

- New modes plug into the same policy seam:

```text
operation/observation -> policy resolution -> authority decision -> commit path
```

Demolition target:

- Any witness/quorum implementation that requires rewriting gameplay
  operations.

Exit criteria:

- Trusted co-op remains fast while additional authority modes become selectable
  policy strategies.

## Immediate Work Queue

Current gate: Stage 7D.4.

Starbridge is the current co-op target for this gate: the RTS commander can own
map-scale station, fabrication, drone, turret, infrastructure, hostile,
construction-ghost, target-mark, and wave-control claims, while each Unity pilot
owns responsive claims for its own ship, salvage, docking/refit, construction
anchoring, cooling, repair, survival-pod recovery, and nearby combat
responsiveness. The implementation must keep those as typed Verse
operations/policies so later authority modes can change placement without
rewriting gameplay commands.

Execution pointer:

- `Aetheria.State/docs/stage-7-thin-client-staged-implementation-plan.md`
  is the active staged implementation runbook for the current Unity/Electron
  client-parity gate. Work must follow its 7D.4 queue before 7D.5 shell
  contraction, then 7E cross-runtime smoke, then Stage 8 demolition.
- This document owns the authority/Starbridge product order. The Stage 7
  runbook owns which Unity/Electron surface can move next without creating a
  client-specific gameplay branch.

## Staged Implementation Plan

This is the build runway for the current migration. It is intentionally more
strict than the architecture notes above it. Every stage has one product proof,
one authority proof, and one demolition target. If a stage cannot name all
three, it is not ready to build.

Design source:

- `E:/Projects/AetheriaLore/Aetheria/Game Design/Aetheria Starbridge.md`

Starbridge release spine:

- One RTS chair and one to four pilot clients defend one daemon-authored base.
- The RTS chair owns the operational interface: base systems, power, shields,
  fabrication, drones, turrets, marks, construction ghosts, wave tools, and
  station support.
- Pilots own embodied field execution: movement, combat, docking/refit, salvage,
  anchoring, cooling, repair, cargo/equipment, survival pods, and local marks.
- Station stock, docked ships, loadouts, cargo, pricing, support gear, and
  recovered technology are shared Verse state. They are not Unity scene state,
  Electron UI state, or a sidecar gameplay API.
- First-release authority is trusted co-op policy. It must remain compatible
  with leases, server-authoritative mode, witness quorum, operator finality, and
  CRDT-like strategies, but the first playable path must stay fast and explicit.
- The first playable loop is one 20-30 minute defense scenario with readable
  waves, five hostile archetypes, station refit, salvage economy, support gear,
  construction anchoring, ship loss/recovery, recovered technology choices,
  victory/defeat, and run score.

Release stop lines:

- Do not build consensus machinery to make the first trusted co-op loop work.
- Do not add new RTS verbs until the station/refit shared resource surface is
  typed, observable from both clients, and mutation-tested through committed
  facts.
- Do not add survival pods, boss rewards, or episode progression until at least
  one commander verb and one pilot field verb have crossed Raven/Starfire through
  local Verse state.
- Do not preserve Unity gameplay ownership for history. Git is history; runtime
  compatibility paths need deletion tickets and verifiers.

The sequence is:

```text
S0 scenario/session facts
  -> S1 visibility projections
  -> S2 station/refit read parity
  -> S2 station/refit operation parity
  -> 7D.5 Unity shell contraction
  -> 7E.1 two-client local Verse smoke
  -> S3 first commander verb
  -> S4 first pilot field verb
  -> S5 Unity death/respawn demolition
  -> S6 complete Starbridge loop
  -> S7 trusted leases and diagnostics
  -> S8 episodes/progression
```

### 7D.4A: Policy And Editor State

Status: active.

Product proof:

- Designers can inspect and edit the trade value policy through an Eve/CultUI
  surface.
- Inventory, trade, station stock, loadout pricing, and RTS economy tooling all
  read the same `aetheria.trade_value_policy.v1` document.

Authority proof:

- Eve surface commands lower into typed Eve command documents, not public
  string commands or untyped payload bags.
- The daemon bridge writes `AetheriaTradeValuePolicy` through `AetheriaStateNode`
  and refreshes the published designer surface from typed state.

Allowed build:

- typed trade policy command body;
- typed command ids for curve and tier edits;
- bridge handling that updates `AetheriaTradeValuePolicy`;
- verifier checks that reject a read-only-only surface or an untyped command
  path.

Demolition target:

- Unity settings or helper methods as the source of price policy truth.

Stop line:

- Do not add more station/refit UI until the policy can be edited through the
  same typed Eve command lane used by other designer surfaces.

### 7D.4B: Station And Refit Read Parity

Status: next.

Product proof:

- A docked pilot can inspect station stock, docked ships, current cargo,
  equipment, loadout slots, pricing, and selected entity status from Unity and
  Electron through equivalent facade reads.

Authority proof:

- Reads are local typed projections over local Verse state. No client treats a
  remote viewport response, Unity manager graph, or scene hierarchy as gameplay
  truth.

Allowed build:

- station stock projection;
- current docking/current entity projection completion;
- loadout, cargo, equipment, inventory, and pricing projection completion;
- generated TS bindings for any new document layout;
- Unity adapters that validate Unity facade objects against daemon keys before
  display.

Demolition target:

- manager-global inventory/catalog/pricing reads;
- unvalidated Unity facade objects used as station/refit truth.

Stop line:

- Do not add refit mutations until the read side is boring and both runtimes can
  see the same state.

### 7D.4C: Station And Refit Operation Parity

Status: blocked by 7D.4B.

Product proof:

- Either runtime can dock, undock, select a docked ship, equip, store, transfer,
  restore a loadout, and perform one purchase/refit operation; the other runtime
  observes the committed result through local Verse state.

Authority proof:

- Every mutation enters as a typed operation document and passes the daemon
  authority gate before simulation state changes.

Allowed build:

- typed operation request/receipt shapes for station/refit verbs;
- daemon validation against station stock, docking, cargo, equipment, pricing,
  and policy state;
- facade methods in C# and TS with named typed operations;
- cross-client operation smoke.

Demolition target:

- any public client refit API that accepts operation strings, raw item bags, or
  UI-local mutation rules.

Stop line:

- Do not start commander build/fabrication/turret verbs until a shared resource
  operation has proven the command/fact/projection loop.

### 7D.5: Unity Shell Contraction

Status: blocked by 7D.4C.

Product proof:

- Unity can still fly, render, inspect inventory, trade/refit, and submit input,
  but `ActionGameManager` is no longer gameplay authority. It is a gesture and
  presentation coordinator only.

Authority proof:

- Unity input submits typed operations through `AetheriaClient`; Unity reads
  portable typed projections. The same operations and projections are usable by
  another runtime.

Allowed build:

- remove remaining public manager request shims;
- replace observed-galaxy/entity reads with facade projections;
- make `ZoneRenderer` consume explicit render/projection data only;
- quarantine remaining Unity object adapters as Stage 8 shims with named
  replacement projections.

Demolition target:

- Unity-side authoritative gameplay state;
- `ZoneRenderer.LoadZone` as level-content ownership;
- new Unity-only command or bus layers.

Stop line:

- Do not claim cross-runtime co-op parity until Unity can run from the same
  command/read contract as Electron.

### 7E.1: Two-Client Local Verse Smoke

Status: blocked by 7D.5.

Product proof:

- Raven Unity and Starfire Electron run as separate local Verse participants and
  see the same current Starbridge session, station/refit state, and controlled
  entities.

Authority proof:

- Raven and Starfire each launch their own CultMesh/CultNet Verse node.
- Shared state converges through typed documents and committed facts, not a
  remote viewport query or an Electron sidecar gameplay API.

Allowed build:

- launch harness for one Unity-shaped client and one Electron-shaped client;
- scoped peer sync checks for session, station/refit, authority policy,
  committed facts, and latest projections;
- diagnostics for source daemon, runtime id, policy id, and rejected facts.

Demolition target:

- one-process assumptions hiding in client code;
- any test that proves co-op by reading one daemon's remote projection.

Stop line:

- Do not expand Starbridge verbs until the clients prove they are peers, not one
  client plus a debug viewer.

### 7E.2: First Commander Verb

Status: blocked by 7E.1.

Product proof:

- Starfire RTS submits one commander tactical verb and Raven Unity observes it
  locally. First candidate: target mark. Second candidate: construction ghost.

Authority proof:

- Commander claims are policy-scoped state, not hardcoded RTS ownership.

Allowed build:

- target-mark or construction-ghost document;
- typed operation and receipt;
- RTS panel command;
- Unity HUD/map projection;
- authority diagnostics showing the runtime and claim kind that authored it.

Demolition target:

- browser-owned enemy/base gameplay;
- hardcoded "RTS owns this because it is RTS" checks outside authority policy.

Stop line:

- Build one commander verb end to end before adding a commander operation menu.

### 7E.3: First Pilot Field Verb

Status: blocked by 7E.2.

Product proof:

- Raven Unity submits one pilot field verb and Starfire RTS observes it locally.
  First candidates: salvage pickup or construction anchoring.

Authority proof:

- Pilot claims are typed facts that the RTS runtime can project and reason
  about; the outcome is not hidden in Unity scene objects.

Allowed build:

- salvage or anchor state document;
- typed operation and receipt;
- Unity input binding or interaction surface;
- RTS panel/map projection;
- daemon validation against equipment, range, ownership, and scenario state.

Demolition target:

- Unity-only interaction logic whose result another runtime cannot inspect.

Stop line:

- Do not build survival pods, full support gear, or wave rewards until one pilot
  verb proves the return trip through Verse.

### 8A: Unity Scene Authority Demolition

Status: blocked by 7E.3.

Product proof:

- A non-Unity client can inspect and control the same Starbridge state that
  Unity renders.

Authority proof:

- Unity scene lifetime, Unity physics, and Unity object hierarchy are
  presentation/runtime concerns only. Ymir, Aetheria state, and typed Verse facts
  own game truth.

Allowed build:

- replace current-zone snapshot render payload with render/projection slabs;
- move death/respawn, object spawning, interaction outcomes, and physics truth
  out of Unity;
- delete or isolate legacy adapters after each replacement.

Demolition target:

- Unity scene or physics authority for gameplay outcomes;
- XZ naming in domain APIs where XY is the intended Aetheria plane.

Stop line:

- Do not call the migration clean while Unity is still required to construct the
  world for other clients to understand it.

### 8B: Complete Starbridge Loop

Status: blocked by 8A.

Product proof:

- A 20-30 minute Starbridge session can run from daemon state: waves, hostiles,
  base damage, refit, salvage/support, pilot loss/recovery, victory/defeat, and
  recovered technology.

Authority proof:

- All outcomes are typed facts or daemon-owned documents that both clients can
  observe through local Verse state.

Allowed build:

- wave schedule and hostile intent;
- boss/reward/recovered-tech facts;
- survival pod, wreck, recovery, replacement ship;
- scenario completion and run score documents.

Demolition target:

- client-owned wave transitions, enemy behavior, reward selection, score, death,
  respawn, or recovered technology.

Stop line:

- Do not add episode progression or large content volume until the first loop is
  mechanically playable and inspectable from both clients.

### 9A: Trusted Leases And Authority Diagnostics

Status: deferred until the claim shapes are stable.

Product proof:

- Designers and players can see which runtime has authority over each active
  claim kind, why a command was denied, and when a lease expires.

Authority proof:

- Changing policy changes who can author a claim without changing operation
  schemas or UI code.

Allowed build:

- bounded leases for close defense, drone reaction, repair, cooling, and
  construction anchoring;
- policy diagnostics surface;
- deny-reason projection;
- compatibility with future server-authoritative, witness-quorum,
  operator-finality, and mergeable-CRDT strategies.

Demolition target:

- hardcoded authority ownership outside policy.

Stop line:

- Do not implement quorum machinery to make trusted co-op work. Trusted co-op
  must stay fast; quorum remains a selectable later policy strategy.

Current staged work order:

| Order | Build now | Code surface | Verifier | Demolition rule |
| --- | --- | --- | --- | --- |
| 1 | Finish S2 station/refit read parity. `current_entity`, `current_docking`, inventory rows, source slot identity, and loadout templates are already moving; split remaining station stock, docked ship, cargo, equipment, pricing, and refit eligibility reads into named projections. | `AetheriaRuntimeRtsProjection`, `AetheriaRuntimeRtsViewportDocuments`, `AetheriaClient`, Unity inventory/trade menus, generated TS bindings. | `verify-stage7d-unity-parity.ps1`, `Aetheria.State.Verify`, `AuthoritySmoke`, `npm run check:rts-bindings`, Unity compile. | Menu logic may adapt Unity controls, but station/refit truth may not come from manager-global observed cargo, docked ship, catalog, pricing, or loadout caches. |
| 2 | Add S2 typed refit operation parity. Dock, undock, select docked ship, equip, store, transfer, restore loadout, and purchase/refit become named operations with typed receipts. | `AetheriaRuntimeDaemonOperationClient`, `AetheriaRuntimeVerseClient`, generated TS operation bindings, Unity/Electron facade methods, daemon operation validation. | Cross-client operation smoke: one runtime issues an allowed refit operation, the other observes the committed result through local Verse state. | Delete or quarantine any public client path that mutates inventory/loadout through operation strings, raw item bags, UI-local rules, or Unity checkpoint rewrites. |
| 3 | Contract Unity to a render/input shell for S2. Inventory/trade UI can remain Unity UI, but every gameplay read/write goes through typed state. | `InventoryMenu`, `InventoryPanel`, `TradeMenu`, `ActionGameManager`, `ZoneRenderer` shims. | Stage 7D verifier rejects direct manager-global refit reads, public manager gameplay reads, new command buses, and Unity-only command paths. | Remaining facade-object bridges must validate daemon keys against typed projections and name the Stage 8 projection/native slab that deletes them. |
| 4 | Prove 7E.1 two-client local Verse parity. Raven Unity and Starfire Electron are peers with separate local Verse nodes, not one runtime viewing another runtime's projection. | launch harness, scoped peer sync, committed facts, session/refit/authority projections, diagnostics. | Raven/Starfire smoke: both clients see the same session, station/refit state, controlled entities, policy id, and fact receipts through local Verse state. | No test may prove co-op by reading a remote viewport or one daemon's debug projection as gameplay state. |
| 5 | Build exactly one S3 commander verb. First candidate: target mark. Second candidate: construction ghost. | RTS facade, daemon command gate, typed mark/ghost documents, Unity HUD/map projection, authority diagnostics. | Raven/Starfire smoke: Starfire authors the commander claim, Raven observes the committed fact locally, and policy diagnostics name the claim kind and author runtime. | No browser-owned enemy/base gameplay, no HTTP sidecar gameplay surface, and no hardcoded "RTS owns this" branch outside policy. |
| 6 | Build exactly one S4 pilot field verb. First candidates: salvage pickup or construction anchoring. | Unity input facade, daemon command gate, typed support/salvage/anchor documents, RTS projection panel. | Raven/Starfire smoke: Raven authors the pilot claim, Starfire observes the committed fact locally, and daemon validation checks range/equipment/ownership/scenario state. | No Unity-only interaction result another runtime cannot inspect through typed state. |
| 7 | Move S5 loss/recovery and S6 waves/rewards into daemon state after field verbs prove the loop. | survival pod, wreck, recovery, replacement ship, wave, hostile intent, boss, victory/defeat, recovered-tech, score documents. | Complete Starbridge loop smoke from a state/catalog seed with both clients attached. | Death/respawn, hostile behavior, boss rewards, score, and wave resolution leave Unity scene ownership. |
| 8 | Add S7 leases and diagnostics only after claim shapes are boring. | authority policy docs, bounded lease docs, claim diagnostics, client overlays. | Trusted-host policy smoke plus diagnostics showing owner, lease, expiry, and deny reason for each active claim. | Do not close the door to quorum, server-authoritative, operator-finality, or mergeable modes, but do not build them into the first fast co-op path. |

## Starbridge Slice Map

This map translates the Starbridge design target into staged code. It is the
anti-Jenga list: build the first missing slice in order, prove it, then remove
the old path it replaces.

Design source:

- `E:/Projects/AetheriaLore/Aetheria/Game Design/Aetheria Starbridge.md`

Execution rules:

1. Build only the first slice whose verifier does not pass.
2. A slice may create typed documents, typed operations, typed projections, and
   policy state named in its `Build` list.
3. A slice may not add client-local gameplay behavior, remote viewport gameplay
   reads, or a new ad-hoc command bus.
4. A slice is not complete until its demolition target is deleted, quarantined
   in the Stage 7 inventory, or carried forward by the next slice with an owner.
5. Starbridge product work follows this map; Unity/Electron parity work follows
   `Aetheria.State/docs/stage-7-thin-client-staged-implementation-plan.md`.

Implementation discipline:

- Treat every Starbridge feature as either `state`, `projection`, `operation`,
  `policy`, or `presentation`. If it does not fit one of those boxes, stop and
  define the missing typed primitive first.
- Build vertical proof slices, not horizontal subsystems. A slice is better
  when it proves one designer-visible verb end to end than when it creates ten
  unused abstractions.
- Prefer small daemon-owned facts plus local projections over broad documents
  that become the next viewport-shaped dumping ground.
- Every UI feature must name the typed state it reads and the typed operation it
  submits. A button without that contract is not ready to implement.
- Every temporary Unity facade bridge must validate daemon keys against a typed
  projection and must be listed as a Stage 8 shim.

Product staging from the Starbridge design:

| Batch | Player-visible proof | Build first | Do not build yet | Done when |
| --- | --- | --- | --- | --- |
| A | A fresh daemon can describe one Starbridge defense without Unity scene authoring. | scenario/session facts, base status, station stock, wave forecast, runtime roles | enemy AI, construction, support gear | `StarbridgeSessionSummaryAsync` returns the active scenario, base, stock, wave, and roles from typed local state. |
| B | Electron and Unity can render the same tactical map slice from local state. | XY gravity viewport, union-of-controlled-units object viewport, selected object/status reads | remote gameplay viewport reads, fog rules hidden in UI | both clients call equivalent facade reads for map and selection panels. |
| C | A docked pilot can inspect station stock, ships, cargo, equipment, pricing, and loadout options. | current entity, current docking, station refit, station stock, loadout template, inventory projections | support gear behavior, survival pods, commander build orders | station/refit UI truth comes from typed projections, not manager-global Unity facade graphs. |
| D | Either client can perform one refit/purchase/transfer operation and the other observes the committed result. | typed S2 operations, daemon validation, committed facts, cross-client smoke | broad inventory editor, client-local item rules | no public client refit API accepts operation strings or untyped payloads. |
| E | The RTS chair can issue one commander tactical verb that pilots can see. | target marks, construction ghost placement, fabrication/build order documents, base system projections | leases, full wave controller | Raven observes Starfire-authored commander facts through local Verse state. |
| F | A pilot can execute one field support verb that the RTS chair can see. | salvage, anchor, cooling, repair, support gear validity, local combat/support claim docs | pod recovery, boss rewards | Starfire observes Raven-authored pilot facts through local Verse state. |
| G | Losing a ship creates recoverable state instead of Unity object death authority. | survival pod, wreck, recovery, replacement ship, dock recovery operations | meta-progression, episode cadence | both clients inspect pod/wreck/recovery facts and Unity no longer owns death/respawn truth. |
| H | A complete 20-30 minute Starbridge loop can run from daemon state. | waves, hostile intent, boss state, victory/defeat, recovered technology, score | witness quorum, adversarial peers | the first scenario can be played as RTS plus pilots with all outcomes represented as typed Verse facts. |
| I | Authority policy becomes designer-visible diagnostics. | bounded leases, claim-kind ownership, denial reasons, active authority overlays | consensus or quorum implementation | changing policy changes who may author claims without changing operation schemas. |
| J | Episodes and progression become content, not code. | scenario completion, unlocks, run score, persistent score currency, recovered-tech pools | large campaign simulation | new Starbridge episodes are data authored against typed scenario/session documents. |

Release-facing gates from the design:

| Gate | Must be true before the next gate | Reason |
| --- | --- | --- |
| Session gate | A daemon-only seed describes the base, commander role, pilot roles, station stock, available ships, first wave forecast, and starting scenario. | The first release cannot depend on a Unity scene to define the defended base. |
| Map gate | Electron and Unity render equivalent XY tactical projections: gravity influences, visible objects, controlled-unit visibility union, selected object status, and authority diagnostics. | The RTS chair and pilots are two distances from the same crisis, not two separate games. |
| Station gate | A docked pilot can inspect, equip, store, transfer, purchase/refit, and restore loadouts from typed station state in both clients. | Support gear, ship loss, and salvage economy all depend on station stock being boring and shared. |
| First commander verb | Build exactly one RTS-authored tactical verb, preferably target mark before construction ghost. | It proves Starfire-authored facts appear to Raven without hiding base gameplay in the browser client. |
| First pilot verb | Build exactly one Raven-authored field verb, preferably salvage pickup or construction anchoring before cooling/repair. | It proves pilot field labor appears to the RTS chair without leaving Unity-only outcomes behind. |
| Loss/recovery gate | Ship loss creates pod, wreck, recovery, replacement, and stock facts readable by both clients. | Player survival and material loss are central to Starbridge; Unity object lifetime cannot own that truth. |
| Wave loop gate | Hostile intent, boss defeat, recovered technology choice, score, and victory/defeat are daemon facts. | The 20-30 minute co-op loop must be playable without client-local wave code. |
| Episode gate | Scenario unlocks, score currency, recovered-tech pools, and faction/scenario metadata are content documents. | Live episodes must be authored against typed state instead of requiring new client code. |

Do not use a later gate as an excuse to skip an earlier one. In particular:
support gear is not allowed until station/refit projections prove equipment
validity; wave rewards are not allowed until pod/recovery state exists; and
leases are policy over already-proven commander and pilot claim documents, not
a way to avoid defining those documents.

Active coding order:

1. Finish Batch C before adding more RTS verbs. Station/refit is the first shared
   resource surface in the Starbridge design, so it must be boring and typed.
2. Prove Batch D with one cross-client refit operation before beginning Batch E.
3. Build exactly one commander verb in Batch E before expanding the commander
   UI. The first candidate is a target mark because it touches visibility,
   authority, and pilot HUD presentation without requiring construction
   anchoring.
4. Build exactly one pilot support verb in Batch F before expanding support
   gear. The first candidate is salvage pickup or construction anchoring; choose
   the one with the smallest existing Unity-only behavior to delete.
5. Delay leases until Batches E and F have stable claim documents. Leases are
   policy over known claims, not a substitute for defining the claims.

Staged dependency ladder:

| Step | Slice | Must already exist | Produces | Blocks |
| --- | --- | --- | --- | --- |
| 0 | S0 scenario/session | daemon seed/import, typed document registry | active scenario/session, base status, station stock, wave forecast | all playable Starbridge verbs |
| 1 | S1 visibility | S0 session identity and controlled runtime roles | XY gravity viewport, union-of-controlled-units object viewport | target marks, ghosts, tactical commands |
| 2 | S2 station/refit | S0 stock plus S1 selected-object/status reads | dock/refit/loadout/cargo operations and projections | support gear, survival loop |
| 3 | S3 commander ops | S1 visibility and S0 base systems | base build, fabrication, drone/turret, marks, wave controls | hostile wave control, leases |
| 4 | S4 pilot ops | S1 visibility and S2 equipment validity | salvage, anchor, cooling, repair, combat/support claims | survival pods, leases |
| 5 | S5 loss/recovery | S2 station/refit and S4 pilot field state | pod, wreck, recovery, respawn, replacement-ship facts | Unity death/respawn demolition |
| 6 | S6 waves/rewards | S3 commander ops and S5 recovery loop | hostile intent, boss state, victory/defeat, recovered tech | first complete Starbridge loop |
| 7 | S7 leases | S3/S4 claim shapes proven under trusted host policy | bounded interest leases and diagnostics | future authority structures |
| 8 | S8 episodes | first complete loop | episode/progression documents | release cadence |

Do not start a later slice to avoid a hard problem in an earlier one. If a later
UI needs data that belongs to an earlier slice, add the projection to the earlier
slice and prove it there.

### Slice S0: Scenario Seed And Session Facts

Stage: 7D.4/7E dependency.

Build:

- typed scenario/session documents for the active Starbridge defense:
  starting base, station stock, available ships, wave table, attacker mix,
  recovered-technology pool, and controlled runtime roles;
- daemon seed/import path that creates those documents without Unity scene
  authoring;
- local projections for session summary, base status, station stock, and wave
  forecast.

Demolition target:

- Unity scene or `ZoneRenderer.LoadZone` as the source of session contents.

Verifier:

- a one-shot daemon started from only state/catalog data publishes a Starbridge
  session summary, station stock, base systems, and first wave forecast readable
  through `AetheriaClient`.

Unlocks:

- S1 map visibility and S2 station/refit operations.

### Slice S1: Map Visibility Projections

Stage: 7D.4.

Build:

- typed local projection for XY gravity viewport;
- typed local projection for objects visible to the union of controlled units;
- Unity and Electron call equivalent projection methods through the client
  facade.

Demolition target:

- remote RTS viewport documents as gameplay state;
- Unity observed-galaxy/sector-map reads for data that belongs to portable
  daemon projections.

Verifier:

- controlled units reveal the union of their visible entities in Electron;
- the same frame/projection call can drive Unity map/HUD presentation without
  consulting manager-global galaxy state.

Unlocks:

- S3 target marks, construction ghosts, and RTS tactical commands.

### Slice S2: Station Stock, Docking, And Refit

Stage: 7D.4/7D.5.

Build:

- typed projections for station stock, docked ships, pilot ship status,
  loadout slots, cargo, and inventory;
- typed operations for dock, undock, set current docked ship, equip, store,
  transfer, restore loadout, and purchase/refit where policy allows them;
- pricing continues to come from authored `aetheria.trade_value_policy.v1`.

Demolition target:

- manager-global inventory/catalog/pricing lookups;
- client-local stock or refit rules.

Verifier:

- Electron and Unity can inspect the same selected station/pawn status and
  issue the same typed refit operation against local Verse state.

Unlocks:

- S4 support gear validity and S5 pilot survival loop.

Implementation ladder:

1. Current-subject identity and docking context:
   - Build `gamecult.aetheria.current_entity.v1` and
     `gamecult.aetheria.current_docking.v1`.
   - Unity menus may keep facade objects only after validating their daemon keys
     against those projections.
   - Delete or quarantine direct manager-global current entity and docking-bay
     reads as each caller moves.
2. Station inventory/read model:
   - Split station stock, docked ships, loadout slots, cargo, and equipment
     state into named typed projections instead of reaching through Unity
     facade graphs.
   - Pricing remains authored by `aetheria.trade_value_policy.v1`.
3. Refit operation model:
   - Use typed operations for dock, undock, set current docked ship, equip,
     store, transfer, restore loadout, and purchase/refit.
   - Operation validity comes from daemon/state projections and policy, not
     client-local inventory rules.
4. Cross-client proof:
   - Unity and Electron inspect the same selected station/pawn state through
     `AetheriaClient`.
   - Either client can issue an allowed refit operation and the other observes
     the committed result through local Verse state.

### Slice S3: Commander Tactical Operations

Stage: 7E after Unity parity smoke starts passing.

Build:

- typed operations for infrastructure placement, fabrication queues,
  drone/turret orders, construction ghost placement, target marks, wave start,
  and commander support calls;
- typed projections for power, shields, buildable hardpoints, fabrication
  queues, drones, turrets, marks, and active threats.

Demolition target:

- any RTS-side bespoke gameplay behavior or browser-owned enemy/base logic.

Verifier:

- Starfire submits commander operations locally; Raven observes committed
  base/tactical facts locally after peer sync.

Unlocks:

- S6 hostile wave control and S7 authority lease policy.

### Slice S4: Pilot Field Operations

Stage: 7E.

Build:

- typed operations for salvage, construction anchoring, cooling, repair,
  target marks, and local combat claims;
- typed projections for heat, durability, support gear, salvage, anchor
  progress, repair/cooling eligibility, and nearby combat targets.

Demolition target:

- Unity-only interaction logic whose outcome another runtime cannot inspect or
  reproduce through typed state.

Verifier:

- Raven submits pilot field operations locally; Starfire observes support,
  salvage, and anchor state through local projections.

Unlocks:

- S5 survival pods and S7 lease policy.

### Slice S5: Pilot Loss And Recovery

Stage: 7E/8 boundary.

Build:

- typed survival-pod, wreck, recovery, respawn, and replacement-ship state;
- station recovery/refit operations that consume station stock and scenario
  rules.

Demolition target:

- Unity scene/object lifetime as the authority for player death, respawn, or
  ship ownership.

Verifier:

- destroying or ejecting a pilot ship produces pod/wreck/stock facts readable
  by both clients; recovery returns the player through typed station state.

Unlocks:

- Stage 8 deletion of Unity gameplay shell death/respawn ownership.

### Slice S6: Waves, Hostiles, And Recovered Technology

Stage: 7E/8.

Build:

- daemon-authored wave schedule, hostile spawn/intent, boss state, victory
  conditions, and recovered technology choices;
- projections for wave pressure, threat vectors, boss status, and post-wave
  rewards.

Demolition target:

- any client-owned enemy behavior, reward selection, or wave transition.

Verifier:

- RTS and Unity observe identical wave transitions and recovered-technology
  choices from local Verse state.

Unlocks:

- scenario completion, score, and meta-progression documents.

### Slice S7: Trusted Co-op Authority Policy

Stage: 9, after 7E proves parity.

Build:

- policy documents that assign claim kinds to RTS, pilot, daemon, or lease
  authority;
- bounded interest leases for close defense, drone reaction envelopes, repair,
  cooling, and construction anchoring;
- diagnostics that show active policy, current lease holder, expiry, and denial
  reason.

Demolition target:

- hardcoded "RTS owns X" or "Unity owns Y" branches outside policy.

Verifier:

- changing policy changes which runtime may author a claim without changing the
  operation schema or client UI code.

Unlocks:

- future witness/quorum authority modes.

### Slice S8: Episode And Progression Documents

Stage: after first playable Starbridge loop.

Build:

- scenario completion, run score, persistent score currency, unlocks, and
  shallow meta-progression documents;
- recovered technology pools tied to scenario/faction definitions.

Demolition target:

- client-local unlock or score state.

Verifier:

- completing a scenario writes typed progression facts that another runtime can
  inspect without Unity.

Progress:

- Starbridge S0 now has typed scenario, session, station-stock, wave, runtime-role,
  and session-summary documents in `AetheriaRuntimeStarbridgeDocuments.cs`.
- `Aetheria.State.Daemon` now seeds the first Starbridge defense session as
  daemon-owned typed Verse facts during boot. A fresh daemon no longer needs a
  Unity scene or `ZoneRenderer.LoadZone` to publish the active scenario,
  session id, base entity, station stock, wave forecast, and commander/pilot
  roles.
- `AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary` projects base
  status, station stock, wave forecast, and runtime roles from daemon frame plus
  optional scenario/session documents without going through an RTS viewport.
- `AetheriaClient.StarbridgeSessionSummaryAsync` exposes that projection as a
  named facade read for Unity, Electron, and future clients.
- `AetheriaRuntimeVerseClient` now owns latest Starbridge scenario/session
  record keys plus typed put/read/watch helpers; the facade resolves those local
  Verse records automatically when callers ask for a session summary.
- Selected-object and inventory reads now have dedicated typed projection
  documents (`gamecult.aetheria.selected_object.v1` and
  `gamecult.aetheria.inventory.v1`). `AetheriaClient.SelectedObjectAsync` and
  `AetheriaClient.InventoryAsync` project those documents directly from the
  latest frame instead of building a whole-zone RTS viewport first.
- Slice S1 now has first-class typed projection documents for
  `gamecult.aetheria.objects_viewport.v1` and
  `gamecult.aetheria.gravity_viewport.v1`. `AetheriaClient.ObjectsViewportAsync`
  returns the controlled-unit visibility union, and
  `AetheriaClient.GravityViewportAsync` returns intersecting XY gravity brushes
  and body views. The older RTS map viewport is now a compatibility composition
  over those two projections.
- `Aetheria.State.AuthoritySmoke` proves the projection directly, through
  `AetheriaClient`, and through a real once-mode daemon process that seeds and
  publishes the Starbridge session facts; `Aetheria.State.Verify` allows the new
  file only as a named transport document boundary.
- RTS generated bindings include the Starbridge document slot maps and schemas;
  `npm run verify:stage7b` and `npm run build` pass.
- `ActionGameManager` boot/catalog/settings/loadout-template access now routes
  through `AetheriaClient`, including input settings and loadout template
  command submission.
- `InputDisplayLayout` now owns an explicit `AetheriaClient` for input settings
  commands, and the old static `ActionGameManager` input-settings command
  ingress has been removed.
- `MainMenu` startup reads and known Eve surface command submission now route
  through `AetheriaClient`.
- Unity Eve surface presentation now routes daemon surface reads and command
  submission through `AetheriaClient`, with default state-ref resolution using
  the file-backed runtime state reader.
- Raw `AetheriaRuntimeVerseClient` references are now confined to
  `AetheriaClient`, the raw client type, and tests.
- `AetheriaDaemonObserver` is facade-backed, so its existing typed operations
  use the shared local client lifetime.
- Action-bar activation, inventory dropdown/current-ship commands, inventory
  drag/drop and double-click transfers, trade purchases, detailed equipped-item
  controls, action-bar binding/clearing, and loadout-template save now submit
  typed daemon/Eve operations from the relevant UI surfaces through explicit
  `AetheriaClient` instances. `ActionGameManager` has shed those public request
  shims and only exposes observed facade identity/control-path resolution for
  those paths.
- Remaining Unity pilot input ingress in `ActionGameManager` now submits through
  a shared typed facade operation helper for movement, look, tractor, targeting,
  reticle targeting, override shutdown, sensor ping, heatsinks, shields,
  interact, tow, dock, and undock. The private `TryRequestDaemon...` pilot
  command shims are gone.
- Stage 7D.4 has started: `MapRenderer` now reads current zone title and
  minimap asteroid visibility directly from local typed Verse state through
  `AetheriaClient`, removing its `ActionGameManager` dependency.
- `SectorRenderer` now reads the zone-details Eve surface data from local typed
  Verse state through `AetheriaClient` instead of manager-owned zone snapshots,
  runtime catalog, and player formatting.
- `gamecult.aetheria.current_docking.v1` and
  `gamecult.aetheria.station_refit.v1` now own the active docking/refit read
  boundary. Unity inventory and trade panels validate current docking through
  typed state, resolve loadout restore and docked-ship purchase targets from
  `DockParentEntityKey`, and build selector options from the station-refit
  projection instead of letting `ObservedAvailableEntities()` define the
  station/refit surface.
- `aetheria.trade_value_policy.v1` now has a daemon-published Eve/CultUI
  designer surface via `AetheriaRuntimeTradeValuePolicySurfaceBuilder`, listed
  beside stat recipes in the daemon editor. The current slice is inspection
  only; persistence must be added through typed Eve command bodies rather than
  Unity settings.

1. Finish rerouting Unity command input through the typed facade by replacing
   manager/menu compatibility shims with explicit client references where the UI
   panels can own them cleanly. Action-bar activation and the inventory
   dropdown/current-ship, inventory transfer/equip/store, trade purchase,
   detailed item controls, action-bar binding/clearing, and loadout-template
   save slices are now facade-backed. Remaining pilot input ingress is now
   facade-backed. The next command targets are
   docking/refit, station-stock authoring, construction-anchor, target mark,
   support-gear, and commander-order surfaces.
2. Continue rerouting Unity reads through local typed projections/state.
3. Contract `ActionGameManager` and `ZoneRenderer` to input/rendering shell
   responsibilities.
4. Prove Raven Unity and Starfire Electron observe the same converged facts
   without remote gameplay viewport reads.
5. Only then begin Unity gameplay shell demolition.

Do not start:

- authority leases;
- Unity shell demolition;
- RTS gameplay feature expansion;
- DOTS/native slice rendering work;

until Stage 7 passes.

## Verification Commands

Run after each stage that touches runtime code:

```powershell
dotnet build Aetheria.State.Daemon\Aetheria.State.Daemon.csproj
dotnet run --project Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj
```

Run when touching RTS/Electron client code:

```powershell
npm run verify:stage7b
npm run verify:stage7c
npm run verify:stage7c:electron
npm run build
```

## Current Known Failure

The focused Stage 4/5 verifier passes: live daemons can expose scoped frames
and committed facts to an external verifier.

Stage 6 now passes for the trusted co-op fact lane: live daemons fetch scoped
typed peer facts, import authorized facts through the local authority router,
publish post-import frames, and converge in the two-daemon smoke.

The current known risk is Stage 7 client parity. RTS/Electron no longer exposes
public generic `command` or `viewport` APIs, the TS contract metadata is
generated from C# document declarations, and the map viewport is projected
locally in TypeScript from the latest daemon frame. Electron selected-object,
inventory/cargo, daemon health, and authority status now read through typed
projection facade methods backed by local CultCache publication sidecars. The
Stage 7C verifiers prove both the compiled Electron runtime facade and the real
Electron app shell against local daemon publications. The remaining Stage 7 risk
is Unity parity: Unity still needs the same typed local Verse client shape before
the gameplay shell can be demolished.
