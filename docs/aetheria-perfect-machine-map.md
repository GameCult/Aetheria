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

- Unity client/runtime lives under `Assets/`. Gameplay now opens the typed
  runtime catalog from `GameData/aetheria-world.cc` and projects current
  `ItemData` DTOs through `AetheriaRuntimeItemCatalog`. `ActionGameManager` no
  longer owns or opens a legacy catalog cache.
  `GameCult.Aetheria.State.Unity`, the embedded Unity package under
  `Packages/org.gamecult.aetheria.state`, owns the Unity runtime path to
  `GameData/aetheria-world.cc` through `AetheriaRuntimeStateBoundary` and owns
  the Unity boot-time state-file existence probe through
  `AetheriaRuntimeStateBoot`. `ActionGameManager` consumes that package-owned
  boot report and throws if `aetheria-world.cc` is missing before gameplay
  boot. When the typed state file exists,
  `ActionGameManager` opens a package-owned read-only runtime catalog snapshot
  from the `.cc` directory store before constructing the temporary legacy
  `ItemManager`. Player settings saves, loadout saves, shutdown checkpoints,
  and wormhole transition checkpoints now queue typed `.cc.pending` Verse
  commit commands through the embedded runtime state package. Run checkpoint
  commands include the current zone plus its runtime entity snapshots.
  `Aetheria.State` owns applying those command envelopes into canonical
  `AetheriaPlayerSettings`, `AetheriaLoadoutTemplate`, `AetheriaRunState`,
  `AetheriaZoneState`, and `AetheriaEntitySnapshot` documents through
  `AetheriaStateNode`. Local run saves, loadouts, player settings files, zone
  files, and generated keyboard layout caches no longer write bespoke durable files. The
  legacy
  `RuntimePlayerSettings` runtime object is no longer named or decorated as a
  MessagePack persistence shape. `Economy.Server` now drains `aetheria-world.cc.pending`
  into canonical typed state and drains `aetheria-world.cc.eve.pending` through
  the provider-owned Eve command bridge on startup and on a daemon polling loop
  while hosting the CultMesh state node. `Aetheria.State.ApplyPending` remains a
  bounded local operator applicator for both pending lanes.
- Shared domain state is still partly built around `RuntimeCatalogEntry`, GUID
  identity, runtime catalog metadata, and static projection references. Dead
  user-record and galaxy-map-layer catalog roots have been deleted, and
  surviving runtime DTOs no longer carry legacy catalog group/table annotations. Newtonsoft,
  JsonKnownTypes, RethinkDB, LiteNetLib client transport, and the broken
  `Economy.Shared` wrapper have been removed from live source. The stale
  `StrategyGameManager.csbak` backup file and unused Unity asset MessagePack
  formatter classes have also been deleted, so the remaining direct
  `MessagePackSerializer` calls are no longer present in live runtime source.
- Local legacy catalog data remains in `GameData/AetherDB.msgpack` and
  `GameData/NameFile/*.msgpack` as migration inputs only. Unity gameplay no
  longer opens those MessagePack files at runtime. The old
  `PlayerSettings.msgpack`, `.loadout`, `.zone`, and
  `GameData/KeyboardLayouts/*.msgpack` authority paths are disabled or deleted.
  The old `SavedGame`/`SavedZone` DTOs and `Galaxy` save-loader constructor are
  deleted. The dead `SavedStory` JSON DTO is deleted.
  `RuntimeZoneBlueprint`, body/orbit zone runtime data, item-instance runtime
  data, `Ship`, and `EntitySettings` remain as runtime
  construction/loadout/session projections, but no longer use save-file or
  serializer vocabulary or declare themselves as MessagePack persistence
  documents.
- `Aetheria.State` now expands `AetheriaPlayerSettings` beyond an active-run
  pointer into the typed Verse replacement for `PlayerSettings.msgpack`: player
  name, tutorial flag, story-file hash cursors, gameplay formatting, graphics
  preferences, input binding overrides, and action-bar inputs. Unity's menu and
  input screens mutate `RuntimePlayerSettings` in memory and queue typed Verse
  commits through the shared player-settings commit primitive; the in-memory
  projection is not portable state authority.
- `Aetheria.State` now exposes typed node put/get ports for run state, zone
  state, and entity snapshots. The state smoke writes a run referencing a zone,
  a zone referencing an entity snapshot, and an entity snapshot carrying
  position, direction, faction, hull, equipment slots, weapon groups, and a stat
  grid. This proves the `.zone` replacement graph is durable typed state. Unity
  now queues current-zone/current-entity-collection snapshots through the
  runtime commit log during run checkpoints; `RuntimeZoneBlueprint` and
  `RuntimeEntityBlueprint` remain runtime construction/loadout projections
  rather than durable file formats.
- `Aetheria.State` now defines `AetheriaLoadoutTemplate` as the typed Verse
  replacement for bespoke `.loadout` files. It stores structured hull,
  equipment, cargo bay, docking bay, child-entity, assignment, and weapon-group
  state through record-key references and typed value slots instead of opaque
  runtime blueprint serialization. Unity's save/loadout UI now projects its
  in-memory `RuntimeEntityBlueprint` into a typed Verse commit command; the
  in-memory list remains a UI/session cache, not durable authority.
- `ActionGameManager` opens `AetheriaRuntimeCatalogStore` over
  `aetheria-world.cc`, projects it through `AetheriaRuntimeItemCatalog`, and
  gives `ItemManager` explicit item lookup authority. `RuntimeCatalogLink<T>`
  is now a hydrated identifier/value holder, not a process-global catalog
  resolver. The old `LegacyItemCatalogBoundary`, `LegacyItemCatalogCache`, and
  runtime MessagePack deserializer path have been deleted. The old
  `DatabaseEntry`/`DatabaseLink<T>` names have been demoted to
  `RuntimeCatalogEntry`/`RuntimeCatalogLink<T>` runtime projection helpers, not
  MessagePack objects or a global union root.
- `ItemManager` no longer exposes the raw runtime item catalog reader as public
  gameplay/UI API. Temporary item instantiation bridges now go through the
  narrow `ItemManager.GetCatalogEntry<T>` method. `ItemManager` no longer
  exposes a legacy catalog enumeration API. This prevents catalog authority from
  leaking into every caller that only needs a domain lookup. The item properties UI no longer uses
  `ItemManager` for manufacturer display; it resolves the manufacturer through
  the package-owned `ActionGameManager.RuntimeCatalog` typed snapshot. Entity
  restore and loadout manufacturer-distance weighting no longer use `ItemManager`
  for faction lookup; they resolve factions through the `Galaxy` typed
  corporation projection. There is no live console item-spawn command; future
  operator item actions should be typed command documents or Eve/CultUI
  commands, not in-client catalog hydration shortcuts. `LoadoutGenerator` also
  receives the typed runtime catalog and uses it to own
  item candidate selection before hydrating selected legacy item DTOs by ID for
  exact fitting checks and behavior construction. The unused `TradeMenuDebug`
  script has been deleted instead of preserving an old uGUI debug path that
  turned typed trade rows back into legacy `ItemData` objects. That hydration
  now comes from typed state, not `AetherDB.msgpack`.
- `Galaxy` generation no longer accepts a runtime item catalog reader or `ItemManager`.
  Sector and tutorial generation receive the package-owned typed runtime
  catalog. `Galaxy` projects typed corporation v2 records into temporary legacy
  `Faction` DTOs, including allegiance edges, for the existing simulation shape
  and resolves full name arrays from `aetheria.name_file.v2` records. The
  runtime no longer opens the old `GameData/NameFile/*.msgpack` directory.
- `RuntimeCatalogLink<T>.Value` can resolve legacy links only after
  `ActionGameManager` binds the typed runtime item catalog projected from
  `aetheria-world.cc`. MessagePack catalog construction no longer grabs global
  link-resolution authority.
- `Economy.Server` now starts the modern `Aetheria.State` CultMesh node, drains
  pending Unity runtime commits through the typed state applicator, and no
  longer owns RethinkDB/LiteNetLib state.
- `Aetheria.State.Import` writes a typed quarantine manifest and migration
  ledger for the legacy catalog files. It also raw-decodes stable old
  MessagePack union fields into typed item/faction/name-file documents without
  compiling the old Unity domain model into `Aetheria.State`. The current
  checked-in catalog maps to 115 item definitions, 12 factions, and 12 name
  files. Item definitions now carry legacy manufacturer IDs, price, shape
  dimensions, occupied cell counts, full typed shape-cell masks, hardpoint
  type, hull type, interior shape masks for hull/cargo equipment, hull
  hardpoint definitions, behavior kind fingerprints, typed recursive behavior
  payloads, stack size, durability, and weapon range/caliber/type/fire/modifier
  classifications. Corporation documents now carry the legacy short name, true
  description from key 3, name-file and boss-hull legacy IDs, influence
  distance, allegiance count, full allegiance edges, and music bank IDs. Empty
  legacy GUID references are imported as absent links rather than as
  `Guid.Empty` catalog IDs.
  Name-file documents are now `aetheria.name_file.v2` records and carry both
  sample names for compact surfaces and the full legacy name array for future
  `Galaxy`/Markov name generation cutover. Corporation documents are now
  `aetheria.corporation.v2` records because runtime generation needs actual
  allegiance edges, not only a count. `Aetheria.State` now owns the canonical
  legacy-ID record key mapping for migrated item, corporation, and name-file
  documents.
- `AetheriaCatalogSnapshot` is the typed catalog read surface over materialized
  `.cc` records. It exposes trade-item, manufacturer, corporation prefix, and
  corporation name-file queries, plus equipment, hardpoint, and behavior
  queries without touching runtime projection DTOs.
- The embedded `GameCult.Aetheria.State.Unity` package now owns the
  Unity-visible immutable catalog read-model contract for
  trade/equipment/behavior/hardpoint/manufacturer/corporation/name queries,
  typed item shape masks, typed interior masks, hardpoints, and behavior
  payloads. It also owns a read-only Unity-compatible CultCache directory-store
  opener for the known Aetheria catalog schemas, so Unity runtime code can read
  `aetheria-world.cc` catalog records without runtime projection DTOs or a MessagePack
  catalog cache. The SDK-style `Aetheria.State.Unity` facade includes
  that package source and owns the full `Aetheria.State` mapper plus Eve surface
  read path for .NET smokes. Neither is a simulation owner and neither writes
  state.
  `Aetheria.Shared.Unity` references this package directly so `Galaxy` can
  consume typed name files without loading legacy `NameFile` documents. The
  package also owns Unity's typed runtime commit log writer for settings,
  loadout-template, and run-checkpoint command envelopes under
  `aetheria-world.cc.pending`. This log is command-only: it cannot decide
  canonical state, and the `Aetheria.State` node applicator deletes applied
  commands after writing typed documents. Run checkpoint envelopes now carry
  current-zone and entity snapshots so the old `.zone` file path has a live
  typed runtime projection path.
- `Aetheria.State.ApplyPending` opens the typed state node and applies queued
  Unity runtime command envelopes from `aetheria-world.cc.pending`, deleting
  successfully applied command files by default. It is an operational bridge,
  not a second state owner; the applicator delegates all writes to
  `AetheriaStateNode`.
- `Economy.Server` hosts the CultMesh state node and now owns the long-running
  pending runtime commit drain loop. `--apply-pending-once` runs the same drain
  path once for smoke/operator use without keeping the process alive.
- `AetheriaRuntimeCommitDrainStatus` and the `aetheria.operations` Eve surface
  publish pending-drain health, pending depth, applied counts, failures, and
  timestamps as typed state. Console logs are notification-only.
- `AetheriaProviderAdvertisementProjector` publishes
  `gamecult.eve.provider_advertisement.v1` for the `aetheria` provider,
  advertising the catalog and operations surfaces, command boundaries, schemas,
  and `.cc` witness path. This is the discovery map for Odin/Eve, not a health
  page.
- `AetheriaCatalogSurfaceProjector` now emits the first provider-owned Eve
  surface from typed catalog state. The importer materializes a
  `gamecult.eve.surface.v1` catalog operator document at
  `eve:surface:aetheria.catalog.operator` with summary, trade-catalog, and
  corporation views. This is a typed CultCache surface document, not a renderer
  dashboard or JSON status card.
- The embedded Unity state package can now read `gamecult.eve.surface` records
  directly from the CultCache `.cc` store into the shared Eve surface contract
  DTOs. This gives Unity the same provider-owned retained tree that the
  SDK-style state client reads, without JSON fixtures, HTTP dashboard
  summaries, or renderer-owned state.
- `AetheriaEveSurfacePresenter` is the first runtime UI Toolkit consumer of
  those typed surface documents. It owns mounting only: state file resolution,
  surface lookup, lowering, and command emission into a typed pending queue.
  Provider command acceptance still belongs to the future CultMesh command
  bridge.
- `GameData/aetheria-world.cc` is now materialized from the importer as the
  project-local typed state file for the checked-in catalog. The importer stores
  relative provenance in the state document, not machine-local absolute paths.
  Unity resolves this path through the embedded `GameCult.Aetheria.State.Unity`
  runtime package, not through the legacy catalog boundary. Unity also asks the
  package to inspect whether the typed state file exists during boot; the
  warning is notification-only and does not make legacy catalog data the typed
  state owner.
  Import rebuilds clear the generated `.cc`, `.cc.records`, and `.cultmesh`
  outputs for the selected state path after capturing legacy inputs, so schema
  evolution can rematerialize typed state instead of being blocked by stale
  embedded schema catalogs.
  `Aetheria.State.Verify` opens the materialized file and checks that migration
  ledger counts match actual typed catalog records, that name-file v2 records
  carry their full name arrays, that the legacy-ID lookup API resolves migrated
  item, corporation, and name-file documents, and that the expanded typed
  catalog facts, typed catalog snapshot queries, and typed Eve catalog surface
  are present in the `.cc` store. The current verifier sees 52 shaped item
  masks, 5 interior masks, 3 hardpoint-host hulls, 22 hardpoints, 39
  behavior-bearing item definitions, 65 behavior payloads, 661 behavior fields,
  18 behavior-side legacy-ID references, 51 hardpoint-tagged equipment items, 3
  hull items, 18 weapon-facet item definitions, and 143 corporation allegiance
  edges. Legacy content includes at least one overhanging hardpoint
  (`LonginusX`); migration verification
  preserves that payload and does not convert content repair into mapper
  authority.
- `.voidbot/state/aetheria.cc` is the repo Persona state witness. Aetheria is
  not registered as a VoidBot Discord identity yet, so mutations use the
  repo-local `void-self-state.mjs apply-operation` typed boundary rather than
  the registered Face MCP path.
- The old IMGUI DB inspector has been deleted. `NameTools` can still
  clean/generate names, but legacy NameFile `.msgpack` export is disabled. The
  remaining Unity helper files formerly under `Assets/Scripts/CultCache` now
  live under `Assets/Scripts/UnitySupport` because they are color/curve helpers,
  not cache authority.
- Galaxy generation no longer reads legacy `Faction` or `NameFile` entries
  through `ItemManager`; it requires the typed runtime catalog opened from
  `GameData/aetheria-world.cc`.
- Entity restore and loadout manufacturer-distance weighting no longer read
  legacy `Faction` entries through `ItemManager`; they use `Galaxy.ResolveFaction`
  over the typed corporation projection.
- The `give` command no longer enumerates legacy item catalog entries; typed
  catalog selection owns the command match, with runtime item projection kept
  as an instantiation-only bridge.
- `LoadoutGenerator` no longer enumerates legacy item catalog entries for its
  candidate pool; typed catalog filtering owns the first item selection pass,
  with runtime item projection kept as a fitting/instantiation bridge.
- `LoadoutGenerator` names the post-selection bridge as runtime item
  projection: typed catalog rows own candidate selection, and `ItemManager`
  only projects the selected row into the temporary DTO shape needed by fitting
  and instantiation.
- `TradeMenuDebug` no longer enumerates legacy item catalog entries for its
  table rows; typed trade catalog records own the row set, with legacy DTO
  hydration kept for the old debug UI callbacks.
- MessagePack is no longer used as a runtime object-cloning shortcut for
  `EntitySettings`, and UI/player-settings startup no longer registers the old
  MessagePack resolver. The custom Unity-side MessagePack resolver and math/type
  formatter source has been deleted with the old bespoke file format path.
  Unused Unity asset formatters for GameObject, Material, Sprite, and Texture2D
  are deleted. Keyboard layout DTOs are plain runtime parse/display models, not
  MessagePack save shapes. `PlayerSettings` is a plain session-local runtime
  object until typed Verse settings are imported into Unity. `ZoneData` body/orbit DTOs, `ItemInstance` DTOs,
  `EntitySettings`, `Ship`, Unity inspector game settings, environment/volume
  settings, exponential curves, Wwise bindings, and enum dictionaries are also
  plain runtime/session/config projections now, not MessagePack persistence
  shapes. The global scene-load MessagePack resolver hook is deleted. The slime
  compute settings path no longer serializes its local parameter struct for
  change detection. Agent task runtime shapes no longer carry MessagePack
  object, union, key, or ignore metadata. They remain plain in-memory AI task
  DTOs until a typed Verse task document exists.
- Legacy player, faction, name-file, galaxy-map-layer, and narrative helper
  DTOs no longer carry MessagePack object/key/ignore metadata. Typed
  corporation/name-file/catalog documents own durable state; these classes are
  inspector/runtime projections only. Item and behavior DTO field layout for
  `AetheriaRuntimeItemCatalog` no longer depends on MessagePack attributes. The
  temporary bridge uses `RuntimeCatalogKeyAttribute` while typed item instances
  and behavior factories are being built.
- `Aetheria.Shared.Unity` no longer references the vendored `MessagePack`
  assembly. The embedded `GameCult.Aetheria.State.Unity` package still depends
  on MessagePackReader to open CultCache `.cc` records; that dependency belongs
  to the typed state package boundary until it can move to a modern CultLib
  Unity package.
- The dead story compiled-JSON cache sketch and its SHA helper are deleted;
  story compilation currently reads Ink source directly until a typed Verse
  story/cache document exists.
- Runtime UI is old Unity UI/uGUI prefabs plus `MonoBehaviour` scripts under
  `Assets/Scripts/UI/` and `Assets/Prefabs/UI/`.
- Aetheria already has Unity UIElements support available through
  `com.unity.modules.uielements`. It now imports local staged
  `org.gamecult.eve.surface` and `org.gamecult.eve.unity-uitoolkit` packages:
  the first carries no-engine `gamecult.eve.surface.v1` contract DTOs, and the
  second lowers those retained trees into UI Toolkit `VisualElement` trees.
  These packages are renderer boundaries, not UI truth owners.
  `org.gamecult.aetheria.eve-runtime` now adds the Aetheria-specific
  `UIDocument` presenter that reads provider-owned Eve surfaces from
  `GameData/aetheria-world.cc` and mounts them through the lowerer. Its command
  path queues `gamecult.eve.command.v1` envelopes under
  `aetheria-world.cc.eve.pending` for the provider-owned command bridge instead
  of accepting renderer-local command effects. `AetheriaEveCommandBridge`
  currently accepts the advertised catalog/operations refresh commands,
  republishes provider-owned surfaces, rejects unknown/unadvertised commands,
  and records `AetheriaEveCommandDrainStatus` as typed state.

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
  The embedded `GameCult.Aetheria.State.Unity` package owns Unity's runtime
  typed state file path plus the boot-time state-file probe, and is the landing
  zone for future typed catalog boot.
  `ActionGameManager` opens the package-owned typed runtime catalog snapshot
  and binds `RuntimeCatalogLink<T>` resolution through `AetheriaRuntimeItemCatalog`.
  Runtime code receives narrow `ItemManager` catalog methods where gameplay
  only needs item lookups. `Galaxy`, entity restore, and faction-distance
  loadout weighting have been moved off legacy faction catalog reads.
  The embedded Unity state package owns the read-only runtime catalog model
  contract and the read-only known-schema `.cc` catalog opener. `ActionGameManager`
  now exposes that typed package snapshot at boot. The SDK-style
  `Aetheria.State.Unity` facade maps typed `.cc` documents into the same
  contract for full .NET smokes and Eve surface reads. Neither writes state or
  owns simulation. The runtime no longer has a MessagePack catalog cache.
  `AetheriaRuntimeItemCatalog` materializes temporary `ItemData` DTOs from typed
  item records for the old simulation object model. `RuntimeCatalogEntry` is a
  projection identity helper, not a persistence base, and behavior type
  selection now uses an explicit runtime catalog map instead of
  `UnionAttribute` reflection.
  Item/behavior DTO field layout for the temporary projection bridge is now
  marked with project-owned `RuntimeCatalogKeyAttribute`, not MessagePack
  metadata. Item properties
  manufacturer display is a typed snapshot consumer; loadout generation is the
  remaining runtime single-ID item lookup bridge. The old trade debug UI and
  console `give` command have been deleted. No live caller can enumerate legacy
  catalog entries through `ItemManager`.
- Inputs: Unity gameplay code, legacy catalog files, typed state documents, and
  CultMesh server state.
- Outputs: `Aetheria.State` emits `.cc` state and CultMesh documents. Runtime
  typed item catalog projection emits in-memory domain objects only; the old
  local save and editor catalog write paths have been cut. Migrated catalog documents are
  addressed through `AetheriaCatalogKeys` and `AetheriaStateNode` legacy-ID
  methods, not importer-local string concatenation. Player settings, loadout
  templates, and run checkpoints have a shared Unity commit-log primitive plus
  a typed state node applicator. Run checkpoints now carry the current zone and
  its entity snapshots through that same command/apply spine. Whole-galaxy
  unloaded-zone projection still needs to be added once lazy generation has a
  typed state owner. The old `SaveState`/`SaveZone` command surface is gone.
  The catalog operator UI is emitted as a typed Eve surface document derived
  from the catalog snapshot.
- Derived state: UI panels, generated keyboard layouts, editor rows, typed
  state boot warnings, and serialized legacy payloads are projections,
  migration inputs, diagnostics, or disabled session-local DTOs, not durable
  authority.
- Forbidden writers: old JSON backing stores, JsonKnownTypes converters,
  Rethink/LiteNet paths, IMGUI database editor exports, local save-file writes,
  generated keyboard layout caches, and server-side `DatabaseCache` paths.
- Shared paths: manual gameplay edits, editor edits, server updates, file load,
  file save, and migration all need to converge on one typed commit primitive.
- Deletion line: no new behavior should be added to old `ItemData` DTO
  metadata or MessagePack catalog paths except bounded migration readers and the
  current typed item projection bridge. `RuntimeCatalogEntry` is no longer a
  persistence owner.

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
  `RuntimeCatalogEntry.ID`, global cache statics, and any compatibility reader.
- Shared paths: gameplay input, editor edits, import/deep-load, replication,
  simulation ticks, and tests all call the same typed state service.
- Deletion line: replace the remaining `RuntimeCatalogEntry`/`ItemData` runtime DTO
  projection with native typed item instances, then remove old MessagePack
  catalog metadata from live Unity source once import-only migration no longer
  needs it.

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

Until the shared Eve package lands upstream, Aetheria carries a local staged
Unity package pair:

- `org.gamecult.eve.surface`: no-engine DTOs for `gamecult.eve.surface.v1`.
- `org.gamecult.eve.unity-uitoolkit`: UI Toolkit lowering from those DTOs to
  native `VisualElement` trees.

These are import boundaries, not Aetheria UI authority. The provider-owned
surface document in CultCache/CultMesh remains the truth, and the lowerer only
projects it.

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
   - Done: promote typed name files to `aetheria.name_file.v2` with full name
     arrays, so future `Galaxy` name generation has a Verse-owned replacement
     for legacy `NameFile.Names`.
   - Done: promote typed corporations to `aetheria.corporation.v2` with full
     allegiance edges, so temporary `Faction` DTO projection can preserve
     loadout generation behavior without reading legacy faction entries.
   - Done: expand the stable item mapper to include equipment facets and
     behavior kind fingerprints needed by future Unity/Eve catalog consumers.
   - Done: import full item shape-cell masks into typed catalog documents and
     expose them through the Unity-facing read facade.
   - Done: import typed interior shape masks and hull hardpoint definitions
     needed by future equipment/cargo layout and fitting consumers.
   - Done: import recursive typed behavior payloads so future behavior
     factories no longer have to depend on legacy `BehaviorData` objects just
     to see authored behavior fields.
   - Done: expand typed `AetheriaPlayerSettings` so `PlayerSettings.msgpack`
     no longer lacks a durable Verse replacement for actual runtime settings
     values.
   - Done: add typed `AetheriaLoadoutTemplate` documents and smoke coverage so
     `.loadout` no longer lacks a durable Verse replacement.
   - Done: add typed node ports and smoke coverage for run -> zone -> entity
     snapshots so `.zone` no longer lacks a durable Verse replacement graph.
   - Remaining: add typed documents/mappers for runtime object graphs,
     typed behavior factory construction, simulation state, and any catalog
     fields not covered by the stable scalar/fingerprint/payload pass.

4. Runtime cutover
   - Done: add a Unity-facing typed catalog read facade and smoke proving it can
     read the materialized `.cc` catalog plus Eve surface without the legacy
     catalog reader.
   - Done: move `Galaxy` faction selection and name generation to the typed
     runtime catalog; legacy `Faction`/`NameFile` catalog entries no longer
     decide generated sector factions or zone names.
   - Done: move entity faction restore and loadout manufacturer-distance
     weighting to `Galaxy.ResolveFaction`, so legacy `Faction` catalog entries
     no longer decide runtime faction references after generation.
   - Done: move `LoadoutGenerator` item candidate selection to the typed
     runtime catalog; legacy item DTO hydration remains only for exact fitting
     checks and behavior construction.
   - Done: delete unused `TradeMenuDebug`; the old debug uGUI trade path no
     longer hydrates typed trade rows back into legacy `ItemData` objects.
   - Done: delete the console `give` command instead of preserving a debug
     operator path that hydrated typed item rows back into legacy item DTOs.
   - Done: delete `ItemManager.GetCatalogEntries<T>` after all live callers
     moved to typed catalog selection.
   - Done: delete `ILegacyItemCatalogReader.GetAll<T>` and the type/global indexes
     inside `LegacyItemCatalogCache`; the legacy cache is now only a pull-fed GUID
     lookup bridge.
   - Done: remove `GameData/NameFile/*.msgpack` from Unity runtime legacy
     catalog boot; typed name files now own runtime names, and old name files
     remain migration inputs only.
   - Done: narrow `ILegacyItemCatalogReader` and `DatabaseLink<T>` to `ItemData`;
     `LegacyItemCatalogCache` ignores non-item entries from `AetherDB.msgpack`.
   - Done: replace `ActionGameManager` cache bootstrap with
     `AetheriaRuntimeItemCatalog`, a typed-state-backed projection from
     `AetheriaRuntimeCatalogSnapshot`; delete `LegacyItemCatalogBoundary`,
     `LegacyItemCatalogCache`, and the runtime MessagePack deserializer path.
   - Done: demote `DatabaseEntry` and `DatabaseLink<T>` from MessagePack
     union/object shapes to plain runtime identity/link helpers.
   - Done: rename live Unity projection helpers from `DatabaseEntry`,
     `DatabaseLink<T>`, and `InspectableDatabaseLinkAttribute` to
     `RuntimeCatalogEntry`, `RuntimeCatalogLink<T>`, and
     `InspectableRuntimeCatalogLinkAttribute`, and move the old
     `ServerShared/CultCache` folder to `ServerShared/RuntimeProjection`.
   - Done: replace behavior union reflection with an explicit runtime catalog
     behavior map and remove all live `Union(...)` annotations.
   - Done: demote agent task runtime shapes from MessagePack object/key/union
     metadata to plain in-memory DTOs.
   - Done: demote legacy player, faction, name-file, galaxy-map-layer, and
     narrative helper DTOs from MessagePack metadata to plain runtime/inspector
     shapes.
   - Done: replace remaining item/behavior DTO MessagePack field metadata with
     `RuntimeCatalogKeyAttribute`; no live `Assets/Scripts` source depends on
     MessagePack, Newtonsoft, RethinkDB, or bespoke save-file serializer symbols.
   - Done: delete dead `PlayerData` and `GalaxyMapLayerData` catalog roots, and
     remove legacy catalog group/table annotations from surviving runtime DTOs.
   - Done: rename loadout generation's selected-item bridge from legacy
     hydration to runtime item projection; typed catalog filtering remains the
     selection owner.
   - Done: remove the stale `MessagePack` assembly reference from
     `Aetheria.Shared.Unity`; the remaining MessagePack reference is contained
     in the typed state package's `.cc` reader.
   - Convert domain references from GUID/base-class patterns to typed record
     refs.
   - Remove runtime dependency on `ItemData` DTOs as item instance owners.

5. Mesh host
   - Replace `Economy.Server` RethinkDB and LiteNetLib database authority with a
     CultMesh node.
   - Keep any transport bridge only if it delegates to CultMesh and protects a
     named external compatibility contract.
   - Publish Verse descriptors and state subscriptions through CultMesh.

6. Eve UI
   - Done: publish the typed catalog operator surface from `Aetheria.State`.
   - Done: stage the Eve surface contract DTO package and UI Toolkit lowering
     package as importable Unity packages in Aetheria while the neighboring Eve
     repo is dirty on unrelated work.
   - Done: teach the embedded Unity state package to read
     `gamecult.eve.surface` records from the `.cc` store into that shared
     contract.
   - Done: add `org.gamecult.aetheria.eve-runtime` with a `UIDocument`
     presenter that mounts typed Eve surfaces from `GameData/aetheria-world.cc`
     through UI Toolkit without giving the renderer state authority.
   - Done: queue renderer-emitted Eve commands as typed
     `gamecult.eve.command.v1` envelopes under `.eve.pending`, separate from
     runtime state commits so the existing state applicator cannot accidentally
     accept commands it does not own.
   - Done: add the provider-owned Eve command bridge that drains `.eve.pending`,
     validates provider/surface/command templates, invokes the current refresh
     handlers, republishes accepted surfaces, rejects unknown commands, and
     records typed command-drain status.
   - Extend the command bridge beyond refresh commands as gameplay/editor Eve
     surfaces acquire provider-owned handlers.
   - Wire the presenter into a Unity scene/prefab for the first runtime surface
     and replace a concrete uGUI screen.
   - Move the staged packages into the Eve repo once its worktree is clean, then
     import them back into Aetheria from Eve instead of carrying a local copy.
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
   - Done: remove the stale Unity `SaveState`/`SaveZone` command names after
     their bespoke serializers were deleted.
   - Done: replace warning-only player settings, loadout, shutdown, and
     wormhole save paths with typed `.cc.pending` Verse commit commands and a
     state-node applicator that writes canonical typed documents.
   - Done: extend run checkpoint commands to carry current-zone and entity
     snapshots into canonical `AetheriaZoneState` and `AetheriaEntitySnapshot`
     documents.
   - Done: add `Aetheria.State.ApplyPending` as a bounded local applicator for
     queued runtime commits until the drain loop is hosted as a daemon.
   - Done: move the pending runtime commit drain into `Economy.Server` startup
     and daemon polling, with `--apply-pending-once` for bounded operation.
   - Done: publish pending-drain health as typed CultCache state and an Eve
     operations surface.
   - Done: publish Eve command-drain health as typed CultCache state and include
     it in the operations surface.
   - Done: publish an Eve provider advertisement so Odin/Eve can discover
     Aetheria surfaces and command boundaries through typed state.
   - Done: stop legacy catalog pull/read paths from writing entries back to
     their source backing store.
   - Done: delete legacy catalog backing-store write/realtime APIs.
   - Done: delete public legacy catalog cache mutation APIs.
   - Done: delete the stale `StrategyGameManager.csbak` backup source and
     unused Unity asset MessagePack formatters.
   - Done: demote keyboard layout DTOs from MessagePack shapes, delete the
     global scene-load MessagePack resolver hook, and remove the now-unused
     Unity-side custom MessagePack resolver/formatter source.
   - Done: demote `PlayerSettings` and nested settings from MessagePack shapes
     after the legacy settings writer was disabled.
   - Done: delete the process-global runtime catalog resolver from
     `RuntimeCatalogLink<T>`; item instances now carry hydrated identifiers and
     `ItemManager` owns resolution through the typed runtime catalog reader.
   - Done: quarantine the vendored Unity `MessagePack` assembly by disabling
     asmdef auto-reference; only explicit state-spine assemblies should see it
     while the Unity CultCache bridge still needs a low-level `.cc` codec.
   - Done: demote `ZonePack`/`EntityPack` runtime names to
     `RuntimeZoneBlueprint`/`RuntimeEntityBlueprint`, and rename loadout
     collections away from save-payload vocabulary. These are now explicitly
     runtime construction projections, not portable state authority.
   - Done: rename the old `EntitySerializer` runtime helper to
     `RuntimeEntityBlueprintProjector`; it captures/instantiates runtime
     blueprint projections and no longer presents itself as a serializer.
   - Done: demote Unity's live `PlayerSettings` runtime object to
     `RuntimePlayerSettings`; `AetheriaPlayerSettings` remains the typed Verse
     state document owner, while Unity only keeps a session projection and
     queues typed player-settings commits.
   - Done: route input-screen binding/action-bar edits through the typed
     player-settings commit primitive now that the Unity state package is live;
     the stale runtime-only keyboard layout warning is gone.
   - Remaining: delete or quarantine old cache abstractions that no longer
     protect an invariant once catalog migration has a typed reader.

## Verification

- `rg "Newtonsoft|JsonObject|JsonProperty|JsonConvert|JsonKnownTypes|RethinkDb|RethinkTable|DatabaseCache"` is zero outside migration quarantine and docs.
- `rg "DatabaseEntry|DatabaseLink|InspectableDatabaseLink|ServerShared\\\\CultCache" Assets Economy.*` shows no live runtime ownership path.
- `dotnet list package --vulnerable --include-transitive` is clean for active
  maintained projects.
- CultCache smoke proves write, flush, reopen, query, and typed reference
  resolution, including full player settings as the `PlayerSettings.msgpack`
  replacement, loadout templates as the `.loadout` replacement, and a run ->
  zone -> entity snapshot graph as the `.zone` replacement.
- CultMesh smoke proves node start, typed put/get, subscription, flush, and
  reopen.
- Eve surface smoke proves provider-owned surface documents are generated from
  typed state and survive `.cc` store reopen before any renderer owns them.
- Quarantine import smoke proves old catalog file facts can be captured into
  typed state without old readers remaining on the live runtime path.
- Migration smoke proves stable old catalog payload fields can be read once and
  converted without old readers remaining on the live runtime path.
- Unity runtime catalog smoke proves read-only catalog consumers can open the
  typed `.cc` store through both the SDK-style `Aetheria.State.Unity` opener and
  the embedded package-owned read-only catalog opener, receiving the same
  package-owned runtime catalog read models without a MessagePack catalog cache,
  including typed item masks, interior masks, hardpoint definitions,
  corporation/name-file links, and typed behavior payloads. It also proves the
  embedded package can read provider-owned Eve surface documents from the same
  CultCache store. The smoke proves the Unity runtime state commit log can
  queue player-settings and run snapshot commands, the `Aetheria.State` node
  can apply them into canonical typed settings/run/zone/entity state, and
  commands are cleared after application. It also proves renderer-emitted Eve
  commands are queued as typed command envelopes separately from state commits.
- Unity batchmode compile with Editor `6000.4.2f1` returned cleanly after the
  runtime catalog resolver cut; `Logs/codex-unity-compile.log` has no compiler
  error hits.
- `rg ".Data.Value|BindRuntimeItemCatalog|ResolveRuntimeItemCatalog|private static IRuntimeItemCatalogReader"` in
  `Assets/Scripts` now finds only `ItemManager` checking its own hydrated value.
- Unity batchmode compile also returned cleanly after disabling `MessagePack`
  auto-reference; `Assets/Scripts` and `Assets/Editor` have no `using
  MessagePack`, `MessagePackSerializer`, `[MessagePackObject]`, or
  `IMessagePackFormatter` hits.
- Unity batchmode compile returned cleanly after the runtime blueprint rename;
  live Unity source has no `EntityPack`, `ShipPack`, `OrbitalEntityPack`,
  `ZonePack`, `PackedContents`, `PackZone`, `EntitySerializer.Pack`, or
  `EntitySerializer.Unpack` hits.
- Runtime entity blueprint projection no longer uses the `EntitySerializer`
  authority name; live Unity source calls `RuntimeEntityBlueprintProjector`.
- Unity runtime settings projection is now `RuntimePlayerSettings`; live Unity
  source has no standalone `PlayerSettings` class/property/method symbols.
- Input binding and action-bar edits now queue the same typed player-settings
  commit path as menu settings changes; the old `SaveLayout` runtime-only
  warning is gone.
- Keyboard display layout parsing is named as a runtime projection parser, and
  the dead commented Ink `ToJson` write path has been removed from live source.
- `Aetheria.State.Smoke` proves the provider-owned Eve command bridge drains
  `gamecult.eve.command.v1` envelopes, accepts advertised refresh commands,
  rejects unknown commands, persists `AetheriaEveCommandDrainStatus`, and exposes
  the Eve command drain through the operations surface.
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
