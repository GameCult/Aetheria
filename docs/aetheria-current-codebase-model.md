# Aetheria Current Codebase Model

Date: 2026-06-22

This is a modeling pass over the live codebase as it exists now. It is not the
ideal architecture; it is the current control-flow map, with migration pressure
points called out where Unity still acts like more than a thin renderer/input
lowerer.

## Topology

- `Aetheria.State` owns the CultCache/CultNet typed state node, document
  registry, Eve surface state, provider advertisement projection, command
  acceptance, and durable Aetheria state documents.
- `Aetheria.State.Daemon` is the local Verse member. It opens an
  `AetheriaStateNode`, starts the CultMesh server, publishes discovery, accepts
  typed Eve commands, reads typed daemon commands, ticks the authoritative run
  document, and republishes daemon witnesses.
- `Packages/org.gamecult.aetheria.state/Runtime` is the embedded Unity-facing
  runtime package. It contains the shared daemon frame documents, typed command
  clients, render query helpers, stat recipe surfaces, replica/discovery code,
  and the SoA publication schema that Unity can map.
- `Assets/Scripts` is the Unity client. It still contains the gameplay facade
  graph, behavior simulation classes, zone renderer, UI shells, and Ymir query
  bridge. During the migration it is partly renderer/input client and partly
  legacy simulation host.
- `Aetheria.State.Verify` is the migration fence. It asserts that deleted
  authority paths stay dead: stringly public command APIs, queues as public API,
  Unity physics authority, direct Unity-side zone rendering ownership, old
  serializer/database paths, and several daemon publication invariants.

## Boot And Daemon Tick

The daemon entry point is `Aetheria.State.Daemon/Program.cs`.

Current daemon control flow:

1. Parse daemon options: state path, daemon id, session id, Verse id, CultMesh
   address, tick interval, fixed delta.
2. Open `AetheriaStateNode` with `startServer: true`.
3. Start `AetheriaVerseDiscoveryHost`.
4. Ensure world, Verse host settings, runtime session, compatibility surfaces,
   and first daemon tick.
5. On each interval:
   - accept observed Eve commands through `AetheriaEveCommandBridge`;
   - read the current daemon frame from the state path;
   - collect typed loadout templates from the node cache;
   - collect observed daemon command documents from the state graph;
   - call `AetheriaRuntimeDaemonTickRunner.Tick`;
   - publish the resulting frame, SoA view, provider advertisement, health,
     command boundary, game GUI/TUI surface, and editor GUI/TUI surface back
     into the typed state node.

`AetheriaRuntimeDaemonTickRunner.Tick` is the authoritative tick boundary. It
filters already-accounted commands, executes new daemon commands through
`AetheriaRuntimeDaemonOperations.Execute`, stamps zone simulation time, builds
`AetheriaRuntimeDaemonFrameDocument`, writes the frame witness, writes the
current-zone entity SoA slab, then publishes command boundary, provider
advertisement, health, and Eve surfaces.

Important nuance: the current tick applies command effects to a typed run
checkpoint. It is authoritative for many menu, inventory, targeting, movement
intent, docking, loot, trade, loadout, and action-bar operations, but it is not
yet a complete standalone simulation engine for every behavior that still lives
under Unity `Assets/Scripts/ServerShared/Behaviors`.

## Command Flow

Unity public gameplay calls mostly route through typed operation wrappers now:

- `AetheriaDaemonObserver` polls observed daemon state and owns
  `AetheriaDaemonOperations`.
- `AetheriaDaemonOperations` exposes typed methods such as `SetTarget`,
  `SetMoveVector`, `TransferCargoItem`, `TradePurchase`, `RestoreLoadout`,
  `ToggleHullConductivity`, and `SetBehaviorActive`.
- `AetheriaRuntimeDaemonOperationClient` builds
  `AetheriaRuntimeDaemonCommandDocument` values and submits them through
  `AetheriaRuntimeCommandSubmitter.TrySubmitDaemonCommand`.
- The daemon reads those typed command documents and applies them in
  `AetheriaRuntimeDaemonOperations`.

This is much healthier than the earlier `Apply(command, payload)` shape. The
remaining compromise is that the command document is one broad record with many
optional fields, selected by `AetheriaRuntimeDaemonCommandKinds`. That is typed
at the document boundary, but not as strong as distinct command body types per
operation. The verifier now prevents public Unity-facing queue/string APIs from
returning, but the command shape is still a union-ish document rather than
fully discriminated typed bodies.

Eve commands are a parallel typed edge for operator/runtime UI surfaces. The
daemon accepts observed Eve commands, applies known surface commands, and
publishes acceptance status. Daemon-published game/editor surfaces should keep
moving toward command templates that point at typed daemon command documents or
typed state refs, not generic payload maps.

## Observation And Render Flow

Unity observes daemon state through `AetheriaDaemonObserver`:

1. Poll `AetheriaRuntimeStateReader.TryReadObservedDaemonState`.
2. Feed the result through `AetheriaRuntimeDaemonObservationCursor`.
3. If the SoA view changed, map daemon memory through
   `AetheriaDaemonSoaMemoryMap`.
4. Lower the mapped columns to `AetheriaDaemonRenderNativeView`, which exposes
   `NativeArray<T>` columns for Burst/Jobs-friendly rendering.

The daemon SoA hot slab is written by
`AetheriaRuntimeDaemonSoaFramePublisher.PublishCurrentZoneEntities`. It
currently publishes entity index, position, rotation, velocity, physics body
radius/mass/inverse mass, render scale, visibility, LOD, and render group. This
is the right direction for a thin Unity client: daemon state appears as a native
view instead of a serialized object graph.

Current limitation: physics body radius, mass, inverse mass, render scale, LOD,
and render group are still generic defaults in the publisher. Entity identity
and position are real; body semantics are not yet rich enough to replace all
renderer/gameplay guesses.

`ActionGameManager.ApplyLatestAuthoritativeDaemonFrame` is the main Unity frame
consumer. It reads the newest authoritative daemon frame, restores or updates
the Unity observed facade graph, calls `ZoneRenderer.ApplyDaemonFrame`, and
loads the daemon zone view when the zone changes.

`ZoneRenderer` is currently a projection cache. It lowers daemon zone snapshots
into Unity GameObjects for bodies, belts, wormholes, entities, compass markers,
loot visuals, and gravity presentation. Per frame, it asks
`AetheriaRuntimeDaemonRenderQueries` for body poses, asteroid poses, visible
entity indices, compass markers, wormhole exits, gravity terrain height, and
gravity terrain bands.

This means `ZoneRenderer` is no longer the canonical level hierarchy owner, but
it still exists as a persistent Unity hierarchy cache. The migration target is
smaller: a per-frame query/lowering layer that feeds native render buffers and
presentation objects only where GameObject views are still unavoidable.

## Physics And Ymir

Unity physics is already fenced as forbidden gameplay authority by
`Aetheria.State.Verify`.

`Assets/Resources/Prefabs/Lightning.prefab` is published under the
`effect.shot.bolt` presentation role. Its `LightningCompute` component and
shader/material bundle are owned by EveUnity with their original Unity GUIDs;
Aetheria owns only the configured prefab and its asset-manifest advertisement.

Texture production currently uses checked-in, pre-generated provider assets.
Substance is not part of the toolchain. A later asset-pipeline pass may move
texture generation into Blender baking, but that pipeline remains provider-side
and cannot become an EveUnity runtime dependency.

Current Ymir control flow:

- Ymir is deliberately a Box3D wrapper and physics-daemon boundary; its name
  means **Not Invented Here**. Box3D owns integration and contact lifecycle.
  Ymir owns stable game-facing body identity, session revisions, command
  receipts, and typed contact-fact identity.
- `AetheriaYmirWorldPhysics` owns the retained physics transaction for each
  `(RunId, ZoneIndex)`: a world session for ships and pickups, and an isolated
  payload session for ordnance. Aetheria owns the active run/zone set and
  disposes both sessions when a run or zone leaves it. Each fixed simulation
  substep has its own physics-step identity even when several substeps share
  one publication frame. Entity bodies use stable `EntityId`, never mutable
  zone-list indices; current indices are projection data and may change during
  cross-zone moves.
- Session creation is the only full body bootstrap. Ordinary ticks lower
  membership changes, movement/orientation changes, tractor forces, gravity
  fields, and pickup-rejection velocity into explicit retained-session
  commands. Post-step body values in the run checkpoint are projections from
  Ymir, not an independent physics solver.
- Restart material lives in a second daemon-private CultCache at
  `<public-state>.ymir.cc`; it is absent from the public Aetheria document
  registry and client subscription database. Ymir emits immutable incremental
  journal chunks plus bounded per-frame resume descriptors. The daemon flushes
  chunks, then resumes, then the public frame. That public frame is the commit
  marker. Startup restores the exact `(RunId, FrameId, ZoneIndex)` records
  before the client CultMesh host starts, and fails closed on incomplete active
  persistence.
- Cargo collection accepts only typed Ymir `Begin` facts. Aetheria persists one
  pickup-contact receipt per Ymir `FactId` before exposing the resulting event,
  so duplicate fact delivery cannot add cargo twice. Proximity and client loot
  commands do not collect cargo.
- Ordinary direct, constant, and charged weapon fire resolves through
  deterministic shot receipts. It does not create a Ymir body or derive damage
  from collision.
- The payload phase follows the world phase in the same retained zone owner.
  Ymir advances payload bodies without world fields or payload-to-payload
  response, then revision-checked Box3D circle casts or overlaps query the
  current world session against explicit stable entity-body candidates.
  Aetheria interprets those query facts; Ymir does not own damage policy.
- Authored `DeployableWeapon` behavior now creates retained mine payloads.
  Aetheria owns deployment admission, arming time, trigger transition,
  detonation delay, blast selection, damage, and lifecycle events. Ymir moves
  the unarmed payload and reports overlap/contact facts after it becomes a
  stationary trigger body. Eve projects the payload kind, age, armed,
  triggered, stationary, radius, delay, and magnitude state.
- The fossil `Projectile`, `GuidedProjectile`, their managers, Unity-side Ymir
  stepping adapter, and dedicated proof tests are deleted. EveUnity lowers
  ordinary weapon travel from daemon `shot.receipt` facts.
- Fossil laser, lightning, hitscan, and constant weapon effect managers are
  also deleted. They no longer cast against Ymir from Unity to choose endpoints
  or shield responses; EveUnity consumes the receipt trajectory, impact kind,
  intensity, and provider presentation-role assets.
- The remaining Unity `AetheriaYmirPhysicsBridge` lowers clickable presentation
  bounds into `YmirPhysicsQueries.CastSphere`; it knows nothing about daemon
  entities, hulls, shields, targets, zones, or combat.

This is the right authority direction: daemon physics enters Ymir from daemon
state, while Unity only asks Ymir a local presentation-picking question.
Remaining debt:

- World and payload sessions are still in-process organs of the Aetheria
  daemon. A future remote Ymir lowering must preserve the same ordered zone
  transaction, stable body identities, revisions, and query facts rather than
  restoring a second Aetheria physics authority.
- The in-process session registry is the live daemon implementation. Private
  replay reconstruction is wired and restart-proven for a consumed pickup
  contact, including no duplicate receipt, event, or rejection kick. Ymir's
  complete in-memory journal is not compacted yet; disk writes are incremental,
  memory retention is not.
- Clickable raycasts still construct query bodies from Unity click bounds. That
  is presentation picking, not simulation authority, but it should stay clearly
  labeled as renderer/UI picking.
- The original `Mine Launcher` item was recovered from the quarantined
  2021-03-05 catalog by stable legacy ID, normalized from its historical effect
  reference to `DeployableWeapon`, and merged into canonical typed provider
  state. The supplemental MessagePack file is migration provenance only.
- `Mine`, `MineManager`, `InstantWeaponEffectManager`, and their serialized
  launcher prefab are deleted. A script-free provider prefab supplies the
  original mesh/materials. Eve publishes lifecycle and pulse properties;
  EveUnity generically lowers those semantic properties to material emission.

## Gravity Terrain

Daemon zone/body state owns gravity as positive depth magnitudes. The Ymir
adapter passes `GravityWellDepth` directly into positive-strength radial fields,
which attract. Shared render queries subtract those magnitudes into negative
terrain wells, and `AetheriaRuntimeGameDocuments.RenderSplatsViewport` performs
the same derived sign conversion for `gravity.height` splats.

`AetheriaRuntimeDaemonRenderQueries` owns gravity influence brushes, terrain
sampling, and terrain bands. `ZoneRenderer` consumes those queries. The main
menu embeds the typed gravity, render-splat, and object viewport documents; its
old fallback body/object world has been deleted.

The old `Assets/Scripts/Gravity.cs` remains a legacy Unity-local visual
evaluator. It does not own gameplay gravity or canonical terrain facts. Unity
receives provider projections for the active camera/minimap/render pass, while
`AetheriaYmirWorldPhysics` receives physics fields from the same daemon state.

## Stats And Behaviors

Stats are mid-migration.

Designer-facing recipe state exists in
`AetheriaRuntimeStatRecipes` and `AetheriaRuntimeStatRecipeSurfaceBuilder`.
Runtime item stat inspection exists in
`AetheriaRuntimeDaemonItemStatQueries`. Recipes are explicit: base value plus
enabled condition modifiers for quality, durability, heat, charge, ammo, range,
integrity, pilot skill, and environment. The recipe evaluator caches enabled
modifiers and dependency masks on the Unity-side `StatRecipe`.

The live behavior system still uses `PerformanceStat` heavily. Every behavior
constructs stat objects, registers them, and calls `Evaluate(...)` during
runtime. If a recipe exists, it uses the recipe evaluator. If not, it falls back
to the legacy exponent model:

- quality exponent;
- durability exponent multiplier;
- heat exponent multiplier;
- curve sampling for behavior-specific effects.

`StatModifier` still mutates target `PerformanceStat` modifier dictionaries at
runtime by behavior and entity. This is a partial modifier system, but it is
attached to the old `PerformanceStat` object graph rather than a compact
blueprint/instance stat set.

The daemon translation does not retain those object-owned dictionaries.
`AetheriaRuntimeBehaviorSimulation` persists switch, trigger, and modifier
lifecycle state in the authoritative snapshot. Runtime stat reads derive the
fossil multiplier-product and constant-sum result from those sources, including
required-behavior and descendant-kind targeting. This makes reconnect replay
idempotent and keeps the Unity fossil out of the gameplay authority path.
The same daemon organ executes common resource gates in group order. Energy,
thermal cells, cargo, and durability remain owned by their narrow transaction
subsystems; behavior composition decides only whether and when to call them.
Thermotoggle target temperature is also daemon-owned behavior state. Authored
data seeds it, adjustable commands mutate it, and its high/low-pass predicate
gates the same ordered chain.

Migration target:

- blueprint/catalog rows define base stat ids and default values;
- designer recipes define explicit condition dependencies per stat;
- item/entity instances carry only condition values and sparse modifier state;
- each stat has a dependency mask so hot paths sample only required conditions;
- current stat queries are daemon-owned and can answer "what is this stat now?"
  without Unity spelunking behavior objects;
- Unity behavior code consumes resolved current stat values or cached stat
  handles, not raw `PerformanceStat` exponent objects.

## UI Surfaces

The current UI direction is Eve/CultUI:

- daemon publishes game/editor GUI and TUI surfaces;
- compatibility state surfaces exist for operations, player settings, catalog,
  loadout templates, zone details, trade item details, and stat recipes;
- the daemon's pilot surface publishes docked spatial equipment and cargo grids
  with generic Eve drop commands; Unity and browser lowerers own transient drag
  state but not fit, access, mutation, or receipts;
- Unity still lowers local surfaces through UI Toolkit for several remaining
  menu/trade panels;
- Brokkr should be considered just another daemon publishing Unity editor Eve
  surfaces, not a special Codex socket.

The architectural note in `Aetheria.State/docs/verse-daemon-shape.md` still
describes optional ecosystem indexing, but it is not the Aetheria session path.
Aetheria clients connect directly to the daemon's advertised CultMesh endpoint;
directly configured daemon peers exchange typed committed facts there as well.
Odin may index the provider only when explicitly enabled. Eve/CultUI defines
presentation, and the daemon owns side effects.

Remaining UI debt is not mostly visual. It is resolution semantics: if an Eve
surface contains a typed pointer to daemon state, the runtime should resolve it
automatically. Current surfaces still often duplicate display values into
component props. That works for rendering, but it is weaker than state-ref
driven UI where agents and clients can inspect or invoke the same typed daemon
state.

## Remaining Unity Responsibility

Unity is already less authoritative than it was, but not detached.

Still in Unity:

- observed `Galaxy`, `Zone`, `Entity`, `Ship`, `EquippedItem`, and behavior
  facade graph projection;
- live behavior classes and much stat evaluation;
- `ActionGameManager` as a large input/UI/projection coordinator;
- `ZoneRenderer` persistent GameObject projection cache;
- prefab/hull visual instantiation and effect attachment;
- some presentation picking and clickable queries;
- old gravity material evaluator in `Gravity`;
- Unity-specific rendering settings and asset palette lowering.

Moved or moving out:

- durable world/catalog/player/run/zone/entity state;
- typed daemon command acceptance and operation application;
- provider advertisements, health, command boundaries, and surfaces;
- current-zone entity hot slab for native rendering and Ymir body construction;
- shared render queries for body poses, gravity terrain, wormholes, markers,
  visible entities, and asteroid poses;
- Unity physics authority.

## High-Value Next Cuts

1. Enrich daemon SoA physics columns. Publish body radius/mass/inverse mass
   from typed entity/hull state instead of `1.0` placeholders.
2. Add daemon current-stat query documents/surfaces. Designers and clients
   should query current resolved stats through daemon-owned state refs, not
   inspect Unity behavior objects.
3. Convert hot behavior reads from `PerformanceStat` to stat handles with
   dependency masks and cached condition sampling. Leave legacy exponent
   evaluation only as migration import/readback.
4. Shrink `ZoneRenderer` toward stateless per-frame render lowering. Persistent
   GameObjects can remain for prefabs, but zone/body/entity discovery should be
   query-driven from daemon state.
5. Promote Ymir's typed local query surface to CultNet/CultMesh query handles
   when the Ymir daemon interface is ready.
6. Make Eve state refs first-class in the UI runtime so surfaces can point to
   daemon state and clients resolve it automatically.

## Mental Model

The current machine is no longer "Unity owns the game and exports some state."
It is now "the Aetheria daemon owns typed state and publications, while Unity is
a powerful but still-too-large observer/projection runtime." The main migration
task is to keep deleting places where Unity turns observed daemon facts back
into local authority.
