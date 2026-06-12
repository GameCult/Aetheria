# Aetheria Perfect Machine Map

Date: 2026-06-12

This is the Proprioception and Imagination pass for turning Aetheria from a
Unity project with ancestral cache/network/UI machinery into a coherent
GameCult machine: typed CultCache state, CultMesh networking, and Eve CultUI
surfaces lowered into Unity without JSON or RethinkDB remaining as live
authority.

## Objective

Aetheria should persist and replicate game state as typed CultCache documents,
publish multiplayer/service state through CultMesh, and render operator/runtime
interfaces from Eve CultUI surfaces. The old JSON, RethinkDB, JsonKnownTypes,
local CultCache, and legacy UI paths should be migration-only or deleted.

## Current Mechanism

- Unity client/runtime lives under `Assets/`, with gameplay scripts reading and
  mutating old `CultCache` data, especially through `ActionGameManager`.
- Shared domain state is built around `DatabaseEntry`, GUID identity, MessagePack
  attributes, Newtonsoft attributes, JsonKnownTypes polymorphism, and static
  cache references.
- Local persistence is mostly old MessagePack files in `GameData/`, but JSON
  backing stores, JSON converters, JsonKnownTypes, and Newtonsoft attributes are
  still welded into the model.
- `Economy.Server` uses LiteNetLib and MessagePack messages, while its database
  path still directly connects to RethinkDB and depends on the old shared cache
  model.
- A vendored RethinkDB driver lives under
  `Assets/Scripts/ServerShared/NIH/RethinkDb/` and pins old Newtonsoft.
- The old Unity DB inspector is an IMGUI editor window under
  `Assets/Scripts/CultCache/Editor/`, tied to Rethink tables and the old
  cache model.
- Runtime UI is old Unity UI/uGUI prefabs plus `MonoBehaviour` scripts under
  `Assets/Scripts/UI/` and `Assets/Prefabs/UI/`.
- Aetheria already has Unity UIElements support available through
  `com.unity.modules.uielements`, but no imported Eve Unity lowering package.

## Invariants

- Durable game state is typed CultCache state, not JSON documents or ad hoc
  sidecars.
- Network-visible state is published through CultMesh/CultNet documents, not a
  RethinkDB changefeed or bespoke status endpoint.
- Record identity belongs to CultCache record keys and typed references, not to
  mutable domain base classes.
- UI surfaces are provider-owned Eve CultUI documents; Unity is a lowering
  runtime, not the owner of UI truth.
- Migration code may read old formats, but old stores cannot decide live state
  after migration.
- The runtime must have one commit path for user action, load/import, server
  replication, and editor mutation of the same state.

## Current Authority Map

- Owner: `DatabaseEntry` plus old `CultCache` act as the practical state owner.
- Inputs: Unity gameplay code, editor DB inspector actions, legacy data files,
  RethinkDB changefeeds, and server messages.
- Outputs: in-memory domain objects, old MessagePack files, optional JSON files,
  RethinkDB table writes, and UI/editor projections.
- Derived state: UI panels, editor rows, Rethink table names, and serialized
  payloads pretend to be projections but can still influence write paths.
- Forbidden writers: Rethink changefeed handlers, old JSON backing stores,
  JsonKnownTypes converters, editor table exports, static cache globals, and
  server-side `DatabaseCache` paths.
- Shared paths: manual gameplay edits, editor edits, server updates, file load,
  file save, and migration all need to converge on one typed commit primitive.
- Deletion line: no new behavior should be added to the old `DatabaseEntry`,
  RethinkDB, JsonKnownTypes, or IMGUI DB inspector paths.

## Target Authority Map

- Owner: `Aetheria.State`, a typed document layer backed by modern
  `GameCult.Caching` and opened through a CultMesh node.
- Inputs: typed commands, save/import migration records, CultMesh replication,
  editor mutations, and deterministic simulation facts.
- Outputs: CultCache `.cc` state files, CultMesh document streams, Eve surface
  documents, simulation events, and migration reports.
- Derived state: Unity scene objects, HUD panels, editor inspectors, debug
  views, server dashboards, and compatibility DTOs.
- Forbidden writers: Unity UI components, RethinkDB, legacy JSON stores,
  `DatabaseEntry.ID`, global cache statics, and any compatibility reader.
- Shared paths: gameplay input, editor edits, import/deep-load, replication,
  simulation ticks, and tests all call the same typed state service.
- Deletion line: after migration smokes pass, delete vendored RethinkDB,
  Newtonsoft/JsonKnownTypes dependencies, old JSON backing stores, and the old
  cache editor window.

## Intended Change

Create a modern Aetheria state runtime that opens a CultMesh node over a typed
CultCache file, registers Aetheria document types, and exposes narrow ports for
gameplay, editor tools, migration, and UI projection. The state runtime becomes
the only live writer. Everything else becomes a consumer, a command source, or
a migration reader.

Use modern CultLib surfaces:

- `GameCult.Caching` for `[CultDocument]`, record keys, indexes, references,
  schema catalog, and typed persistence.
- `GameCult.Caching.MessagePack` for the binary CultCache backing format.
- `GameCult.Mesh` for node/database illusion, durable shard logs, Verse
  descriptors, authority leases, replication, and peer discovery.
- Eve CultUI for portable UI documents and command envelopes.

## UI Direction

Unity should lower Eve surfaces through UI Toolkit, not through the old uGUI
CultUI package as the strategic path.

The package should live in the Eve repo as an importable Unity package, for
example `org.gamecult.eve.unity-uitoolkit`. It should consume
`gamecult.eve.surface.v1` and `gamecult.eve.command.v1`, lower retained
components into `UIDocument`/`VisualElement` trees, and publish commands back to
the provider through CultMesh/CultNet.

Aetheria should import that Eve package and provide surfaces. It should not own
a bespoke Unity lowering. The existing CultLib Unity uGUI package remains useful
as prior art for resolver-backed controls and inspector generation, but it is
not the new portable UI authority.

First Aetheria surfaces to publish:

- runtime HUD
- inventory
- menu shell
- sector/map view
- typed state inspector
- migration report / quarantine viewer
- server/session status

## Migration Phases

1. Baseline
   - Record the current compile/test status for Unity and .NET projects.
   - Keep this map current before cutting code.
   - Count old authority references with `rg` so deletion has a visible target.

2. Modern state spine
   - Add references to modern `GameCult.Caching`,
     `GameCult.Caching.MessagePack`, and `GameCult.Mesh`.
   - Create `Aetheria.State` document definitions using `[CultDocument]`,
     `[MessagePackObject]`, `[CultName]`, `[CultIndex]`, and typed references.
   - Add a smoke that writes, flushes, reopens, and reads typed state from a
     CultCache `.cc` file.

3. Legacy quarantine
   - Move legacy readers into an explicit migration namespace/project.
   - Read old `GameData/AetherDB.msgpack` and related files without granting
     them runtime authority.
   - Emit typed CultCache records plus a migration ledger.

4. Runtime cutover
   - Replace `ActionGameManager` cache bootstrap with the new state runtime.
   - Convert domain references from GUID/base-class patterns to typed record
     refs.
   - Remove runtime dependency on old `CultCache` and `DatabaseEntry` as owners.

5. Mesh host
   - Replace `Economy.Server` RethinkDB and LiteNetLib database authority with a
     CultMesh node.
   - Keep any transport bridge only if it delegates to CultMesh and protects a
     named external compatibility contract.
   - Publish Verse descriptors and state subscriptions through CultMesh.

6. Eve UI
   - Build or import the Eve UI Toolkit lowering package from the Eve repo.
   - Publish Aetheria UI as Eve surfaces.
   - Replace the old IMGUI DB inspector first, because it is closest to state
     authority.
   - Then replace runtime HUD/menu/inventory/map screens.

7. Purge
   - Delete vendored RethinkDB.
   - Delete JsonKnownTypes.
   - Delete Newtonsoft dependencies and attributes from live code.
   - Delete old JSON backing stores.
   - Delete or quarantine old cache abstractions that no longer protect an
     invariant.

## Verification

- `rg "Newtonsoft|JsonObject|JsonProperty|JsonConvert|JsonKnownTypes|RethinkDb|RethinkTable|DatabaseCache"` is zero outside migration quarantine and docs.
- `rg "DatabaseEntry" Assets Economy.*` shows no live runtime ownership path.
- `dotnet list package --vulnerable --include-transitive` is clean for active
  maintained projects.
- CultCache smoke proves write, flush, reopen, query, and typed reference
  resolution.
- CultMesh smoke proves node start, typed put/get, subscription, flush, and
  reopen.
- Migration smoke proves old data can be read once and converted without old
  readers remaining on the live runtime path.
- Unity play smoke proves runtime UI reads from Eve surfaces and sends commands
  through the shared state service.
- UI Toolkit lowering parity compares Aetheria surfaces against the Eve browser
  renderer for component tree, state bindings, command envelopes, disabled/stale
  states, and visible error surfaces.

## Immediate Cut Line

Do not bump Newtonsoft as the fix. That keeps the wrong organ alive with newer
paint on it.

The first implementation cut should create the modern typed state spine and a
legacy quarantine. After that, every RethinkDB/Newtonsoft/JsonKnownTypes removal
has somewhere clean to land, and every old path can be judged by whether it
still owns behavior.
