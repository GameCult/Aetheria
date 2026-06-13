# Aetheria Perfect Machine Map

Date: 2026-06-13

This is the Proprioception and Imagination pass for turning Aetheria from a
Unity project with ancestral cache/network/UI machinery into a coherent
GameCult machine: typed CultCache state, CultMesh networking, and Eve CultUI
surfaces lowered into Unity without JSON or RethinkDB remaining as live
authority.

## Objective

Aetheria should persist and replicate game state as typed CultCache documents,
publish multiplayer/service state through CultMesh, and render operator/runtime
interfaces from Eve CultUI surfaces. The old JSON, RethinkDB, JsonKnownTypes,
legacy catalog cache, and legacy UI paths should be migration-only or deleted.

## Current Mechanism

- Unity client/runtime lives under `Assets/`. Gameplay still reads the legacy
  item/name catalog through `LegacyCatalogBoundary`, but `ActionGameManager` no
  longer owns a global legacy cache. Local run saves, loadouts, player settings
  files, zone files, and generated keyboard layout caches no longer write
  bespoke durable files. The legacy `PlayerSettings` runtime object is no
  longer decorated as a MessagePack persistence shape.
- Shared domain state is still partly built around `DatabaseEntry`, GUID
  identity, MessagePack attributes, and static cache references. Newtonsoft,
  JsonKnownTypes, RethinkDB, LiteNetLib client transport, and the broken
  `Economy.Shared` wrapper have been removed from live source. The stale
  `StrategyGameManager.csbak` backup file and unused Unity asset MessagePack
  formatter classes have also been deleted, so the remaining direct
  `MessagePackSerializer` calls are the two legacy catalog deserializers and
  their resolver setup.
- Local legacy catalog data remains in `GameData/AetherDB.msgpack` and
  `GameData/NameFile/*.msgpack` as migration/catalog inputs. The old
  `PlayerSettings.msgpack`, `.loadout`, `.zone`, and
  `GameData/KeyboardLayouts/*.msgpack` authority paths are disabled or deleted.
  The old `SavedGame`/`SavedZone` DTOs and `Galaxy` save-loader constructor are
  deleted. The dead `SavedStory` JSON DTO is deleted. `ZonePack` and
  `EntityPack` remain as runtime construction/loadout snapshots, but no longer
  declare themselves as MessagePack persistence documents.
- `LegacyCatalogBoundary` opens `LegacyCatalogCache` as the only concrete
  pull-only catalog cache. Runtime consumers receive `ILegacyCatalogReader`,
  so old MessagePack backing stores may hydrate in-memory domain objects for
  the current Unity runtime, but this path cannot push or delete legacy files.
  The legacy backing-store write/realtime APIs and public cache mutation methods
  have been deleted; only backing-store pull hydration can populate it. The
  backing-store serializer methods are also deleted, so the legacy cache
  implementation exposes deserialization only.
- `DatabaseLink<T>.Value` can resolve legacy links only after
  `LegacyCatalogBoundary` binds the pull-only catalog cache. Legacy catalog
  construction no longer grabs global `DatabaseLinkBase` authority.
- `Economy.Server` now starts the modern `Aetheria.State` CultMesh node and no
  longer owns RethinkDB/LiteNetLib state.
- `Aetheria.State.Import` writes a typed quarantine manifest and migration
  ledger for the legacy catalog files. It also raw-decodes stable old
  MessagePack union fields into typed item/faction/name-file documents without
  compiling the old Unity domain model into `Aetheria.State`. The current
  checked-in catalog maps to 115 item definitions, 12 factions, and 12 name
  files. Item definitions now carry legacy manufacturer IDs, price, shape
  dimensions, occupied cell counts, hardpoint type, hull type, behavior kind
  fingerprints, stack size, durability, and weapon range/caliber/type/fire/
  modifier classifications. Corporation documents now carry the legacy short
  name, true description from key 3, name-file and boss-hull legacy IDs,
  influence distance, allegiance count, and music bank IDs. Empty legacy GUID
  references are imported as absent links rather than as `Guid.Empty` catalog
  IDs.
  `Aetheria.State` now owns the canonical legacy-ID record key mapping for
  migrated item, corporation, and name-file documents.
- `AetheriaCatalogSnapshot` is the typed catalog read surface over materialized
  `.cc` records. It exposes trade-item, manufacturer, corporation prefix, and
  corporation name-file queries, plus equipment, hardpoint, and behavior
  queries without touching `DatabaseEntry`.
- `AetheriaCatalogSurfaceProjector` now emits the first provider-owned Eve
  surface from typed catalog state. The importer materializes a
  `gamecult.eve.surface.v1` catalog operator document at
  `eve:surface:aetheria.catalog.operator` with summary, trade-catalog, and
  corporation views. This is a typed CultCache surface document, not a renderer
  dashboard or JSON status card.
- `GameData/aetheria-world.cc` is now materialized from the importer as the
  project-local typed state file for the checked-in catalog. The importer stores
  relative provenance in the state document, not machine-local absolute paths.
  `Aetheria.State.Verify` opens the materialized file and checks that migration
  ledger counts match actual typed catalog records, that the legacy-ID lookup
  API resolves migrated item, corporation, and name-file documents, and that
  the expanded typed catalog facts, typed catalog snapshot queries, and typed
  Eve catalog surface are present in the `.cc` store. The current verifier sees
  39 behavior-bearing item definitions, 51 hardpoint-tagged equipment items, 3
  hull items, and 18 weapon-facet item definitions.
- `.voidbot/state/aetheria.cc` is the repo Persona state witness. Aetheria is
  not registered as a VoidBot Discord identity yet, so mutations use the
  repo-local `void-self-state.mjs apply-operation` typed boundary rather than
  the registered Face MCP path.
- The old IMGUI DB inspector under `Assets/Scripts/CultCache/Editor/` has been
  deleted. `NameTools` can still clean/generate names, but legacy NameFile
  `.msgpack` export is disabled.
- MessagePack is no longer used as a runtime object-cloning shortcut for
  `EntitySettings`, and UI/player-settings startup no longer registers the old
  MessagePack resolver. Resolver registration is confined to legacy catalog
  deserialization. Unused Unity asset formatters for GameObject, Material,
  Sprite, and Texture2D are deleted. Keyboard layout DTOs are plain runtime
  parse/display models, not MessagePack save shapes. `PlayerSettings` is a
  plain session-local runtime object until typed Verse settings are imported
  into Unity. The global scene-load MessagePack resolver hook is deleted. The
  slime compute settings path no longer serializes its local parameter struct
  for change detection.
- The dead story compiled-JSON cache sketch and its SHA helper are deleted;
  story compilation currently reads Ink source directly until a typed Verse
  story/cache document exists.
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

- Owner: `Aetheria.State` owns the new typed state spine for durable state.
  `LegacyCatalogBoundary` is the only named owner for old MessagePack catalog
  reads and legacy `DatabaseLink<T>` resolution inside the Unity runtime until
  catalog migration lands. Runtime code receives `ILegacyCatalogReader`, not the
  concrete cache. The legacy cache no longer has a public mutation surface.
- Inputs: Unity gameplay code, legacy catalog files, typed state documents, and
  CultMesh server state.
- Outputs: `Aetheria.State` emits `.cc` state and CultMesh documents. Legacy
  catalog reads emit in-memory domain objects only; the old local save and
  editor catalog write paths have been cut. Migrated catalog documents are
  addressed through `AetheriaCatalogKeys` and `AetheriaStateNode` legacy-ID
  methods, not importer-local string concatenation. The catalog operator UI is
  emitted as a typed Eve surface document derived from the catalog snapshot.
- Derived state: UI panels, generated keyboard layouts, editor rows, and
  serialized legacy payloads are projections, migration inputs, or disabled
  session-local DTOs, not durable authority.
- Forbidden writers: old JSON backing stores, JsonKnownTypes converters,
  Rethink/LiteNet paths, IMGUI database editor exports, local save-file writes,
  generated keyboard layout caches, and server-side `DatabaseCache` paths.
- Shared paths: manual gameplay edits, editor edits, server updates, file load,
  file save, and migration all need to converge on one typed commit primitive.
- Deletion line: no new behavior should be added to `LegacyCatalogBoundary`, the
  old `DatabaseEntry`, `LegacyCatalogCache`, or MessagePack catalog paths
  except bounded migration readers that emit typed `Aetheria.State` documents.

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
- Deletion line: after catalog migration smokes pass, delete or quarantine the
  remaining `LegacyCatalogCache`/`DatabaseEntry` runtime catalog dependency and
  remove old MessagePack backing store classes from live Unity source.

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

Until that shared Eve package exists, Aetheria publishes provider surfaces as
typed CultCache documents using a local mirror of `gamecult.eve.surface.v1`.
The mirror is not a new UI authority; it is the state provider contract that a
future Eve package should replace or align with.

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
   - Done: capture old `GameData/AetherDB.msgpack` and related name files as a
     typed quarantine manifest without granting them runtime authority.
   - Done: emit typed CultCache records plus a migration ledger for the catalog
     quarantine preflight.
   - Done: implement a bounded raw payload mapper that converts stable old
     `DatabaseEntry` union fields into typed item/faction/name-file catalog
     documents without making `Aetheria.State` depend on Unity's legacy model.
   - Done: expand the stable item mapper to include equipment facets and
     behavior kind fingerprints needed by future Unity/Eve catalog consumers.
   - Remaining: add typed documents/mappers for runtime object graphs,
     full behavior payloads, Unity shape masks, simulation state, and any
     catalog fields not covered by the stable scalar/fingerprint pass.

4. Runtime cutover
   - Replace `ActionGameManager` cache bootstrap with the new state runtime.
   - Convert domain references from GUID/base-class patterns to typed record
     refs.
   - Remove runtime dependency on `LegacyCatalogCache` and `DatabaseEntry` as
     owners.

5. Mesh host
   - Replace `Economy.Server` RethinkDB and LiteNetLib database authority with a
     CultMesh node.
   - Keep any transport bridge only if it delegates to CultMesh and protects a
     named external compatibility contract.
   - Publish Verse descriptors and state subscriptions through CultMesh.

6. Eve UI
   - Done: publish the typed catalog operator surface from `Aetheria.State`.
   - Build or import the Eve UI Toolkit lowering package from the Eve repo.
   - Replace the old IMGUI DB inspector first, because it is closest to state
     authority.
   - Then replace runtime HUD/menu/inventory/map screens.

7. Purge
   - Done: delete vendored RethinkDB.
   - Done: delete JsonKnownTypes.
   - Done: delete Newtonsoft dependencies and attributes from live code.
   - Done: delete old JSON backing stores.
   - Done: delete the broken `Economy.Shared` wrapper and tracked build output.
   - Done: disable legacy local save, loadout, zone, player-settings, keyboard
     layout, DB inspector, and NameFile export writers.
   - Done: delete the old `SavedGame`/`SavedZone` runtime save DTO and loader.
   - Done: stop legacy catalog pull/read paths from writing entries back to
     their source backing store.
   - Done: delete legacy catalog backing-store write/realtime APIs.
   - Done: delete public legacy catalog cache mutation APIs.
   - Done: delete the stale `StrategyGameManager.csbak` backup source and
     unused Unity asset MessagePack formatters.
   - Done: demote keyboard layout DTOs from MessagePack shapes and delete the
     global scene-load MessagePack resolver hook.
   - Done: demote `PlayerSettings` and nested settings from MessagePack shapes
     after the legacy settings writer was disabled.
   - Remaining: delete or quarantine old cache abstractions that no longer
     protect an invariant once catalog migration has a typed reader.

## Verification

- `rg "Newtonsoft|JsonObject|JsonProperty|JsonConvert|JsonKnownTypes|RethinkDb|RethinkTable|DatabaseCache"` is zero outside migration quarantine and docs.
- `rg "DatabaseEntry" Assets Economy.*` shows no live runtime ownership path.
- `dotnet list package --vulnerable --include-transitive` is clean for active
  maintained projects.
- CultCache smoke proves write, flush, reopen, query, and typed reference
  resolution.
- CultMesh smoke proves node start, typed put/get, subscription, flush, and
  reopen.
- Eve surface smoke proves provider-owned surface documents are generated from
  typed state and survive `.cc` store reopen before any renderer owns them.
- Quarantine import smoke proves old catalog file facts can be captured into
  typed state without old readers remaining on the live runtime path.
- Migration smoke proves stable old catalog payload fields can be read once and
  converted without old readers remaining on the live runtime path.
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
