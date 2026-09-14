# CultCache Migration Target

Date: 2026-09-12

Status: landed; awaiting operator play smoke and Studio click-through. Aetheria runs on CultLib's CultCache and CultMath, cut over on
`codex/cultcache-cutover` from Cut 8a (`4f4beb0f`) through Cut 10 (`4a545469`
to the commit that set this status). The means are in
`docs/cultcache-migration-cut.md`.

Aetheria moves off its in-tree CultCache, the ancestor of the one in CultLib,
onto modern CultCache in `F:\Projects\CultLib`. Adopting the infrastructure the
rest of GameCult maintains replaces a private copy with a shared one. This
document states the end state and the invariants that constrain getting there.

## This touches the foundation

CultNet is built on CultCache, and CultMesh on CultNet. Every CultLib change
below lands under all GameCult infrastructure, not just Aetheria.

- **Wire parity is an invariant.** CultCache maintains wire parity with the
  other runtimes, and only the packages inside CultLib are canonical:
  `CultLib\packages\cultcache-{py,rs,ts}`, `cultnet-{py,rs,ts}`, and
  `cultmesh-{browser,kotlin,py,rs,ts}`. The neighboring repositories
  `F:\Projects\cultcache-rs`, `cultcache-py`, `cultnet-rs`, and `cultnet-ts`
  are defunct and are not parity targets. A change that alters bytes on disk or
  on the wire must land in every canonical runtime, or it does not land.
- **C# is the reference runtime.** The ergonomics arose there; the other
  runtimes follow its contract. Contract changes are written down in
  `CultLib\src\GameCult.Caching\Contracts\` first.
- **Existing consumers must survive.** Aquarium and Ymir build against CultLib
  `main`; other consumers pin revisions or carry copies. AetheriaEve is not a
  consumer: it is preserved as a specimen and receives no consideration.

## Why the previous attempt failed

The first migration (rolled back by `7006a6b0`, plan in
`aetheria-perfect-machine-map.md`) made the cache move phase 2 of seven,
bundled with an `Aetheria.State` service layer, CultMesh hosting, Eve UI, and
an Electron RTS client. The cache never finished moving. This migration moves
the cache and nothing else.

## Target state: Aetheria

- **All persistent state is CultCache state**, split by lifecycle into routed
  stores inside one cache:

  | Store | Holds | Written by | Lifetime |
  |---|---|---|---|
  | Catalog `GameData/Aetheria.cc` | Items, factions, products, name files, other authored records | CultCache Studio | Read-only at runtime |
  | Run `.cc` | Galaxy, zones, bodies, orbits, entities, tasks | The run save path | Deleted on death |
  | Player `.cc` | Settings, bindings, account | The game | Persists across runs |

- **Documents versus values.** A record with identity that something references
  or that changes independently is a `[CultDocument]`. Values without identity
  (item instances in cargo, behavior lists on a design, weapon groups) live
  inside their owning document and keep their MessagePack `[Union]`s.
- **References are `CultRecordRef<T>`**, resolved through an explicit cache.
  `DatabaseLink<T>`, the static `DatabaseLinkBase.Cache`, `DatabaseEntry`, its
  union, and `DatabaseEntry.ID` are gone. A catalog record's identity is its
  record key, which carries the legacy Guid string.
- **The live simulation owns in-play state.** `Entity` and its reactive fields
  are not documents. Quit and zone transition share one save commit
  (`RunSave.Capture` then `RunSave.Commit`); death deletes the run with
  `RunSave.Clear`.
- **No legacy reader exists.** The catalog records carry legacy Guid keys and
  slot numbers from the one-time import; the importer and the legacy files are
  deleted.
- **Existing saves are discarded.** No save converter.
- **Deleted from Aetheria:** `Assets/Scripts/ServerShared/CultCache/`, the
  vendored `Assets/Plugins/MessagePack`, vendored JsonKnownTypes, the legacy
  Guid/Type resolvers, and the IMGUI Database Tools under
  `Assets/Scripts/CultCache/Editor/`.
- **Consumption.** Unity takes the full `org.gamecult.cultlib` UPM package
  pinned by git tag. `Aetheria.Shared/Aetheria.Shared.csproj` references the
  same CultLib version, so the headless build still proves the simulation
  compiles without Unity.

## Target state: CultLib

- **Store routing replaces replication.** Each document type has exactly one
  home store, chosen by the record's runtime type (most specific assignable
  route wins). A cache with one store and no routes behaves as today. Overlapping
  routes, writes of an unrouted type, a record loaded from a store that is not
  its home, and attaching a store after hydration are errors. Multi-store
  replication is deleted: no consumer uses it, and transactions already refuse
  more than one store.
- **Loading never writes.** Store dirtiness derives only from mutations.
- **Read-only stores.** Writes routed to a read-only store throw.
- **Transactions** require every mutation to route to one store, replacing the
  rule that a transacting cache may attach only one store.
- **Runtime type decides schema.** The generic `AddAsync<T>`/`UpsertAsync<T>`
  type parameter stops choosing the schema.
- **Typed lookups honor inheritance.** `Watch<T>`, `GetByName<T>`,
  `GetGlobal<T>`, `GetByIndex<T>` match assignable types, as `GetAll<T>` and
  `Get<T>` already do.
- **Consumers can serialize their value types** (Unity.Mathematics, and whatever
  replaces `System.Type` fields) without breaking wire parity.
- **CultCache Studio absorbs Database Tools:** dictionaries, polymorphic
  `[Union]` fields, Unity.Mathematics types, a record picker for
  `CultRecordRef<T>`, and a custom drawer extension point.

## Evidence

Runtime probe, 2026-09-12, CultLib `main` at `c2a9a6e`, generator and
reflection builds, write then flush then reopen:

| Case | Result |
|---|---|
| Nested `[Union]` field inside a document | Pass |
| Document inheriting `[Key]` fields from an unattributed abstract base | Pass |
| `GetAll<AbstractBase>()`, `Get<AbstractBase>(key)` | Pass |
| `UpsertAsync<Gear>(new Weapon())` | Fail: stored and reloaded as `Gear`, `Weapon` fields lost silently |

Code-read, not probed: exact-type `Watch`/`GetByName`/`GetGlobal`/`GetByIndex`;
no consumer formatter resolver registration (fixed static options); the
generator never emits a codec for hierarchies and silently falls back;
CultCache Studio lacks dictionaries, unions, math types, and a ref picker.

MessagePack 3's analyzer (MsgPack005) rejects `[MessagePackObject]` on an
abstract type without `[Union]`; Aetheria's abstract `ItemData`,
`CraftedItemData`, and `ItemInstance` must change.

## Invariants

- Wire parity across runtimes; C# remains the reference.
- CultNet and CultMesh behavior is preserved or deliberately changed with every
  runtime updated.
- Loading a store never writes it.
- Every document type has one home store; its runtime type decides schema and
  route.
- `ServerShared` compiles headless with no UnityEngine reference.
- No code reads a legacy format.
- The migration does not bundle CultMesh hosting, Eve UI, daemons, or a state
  service layer.

## Out of scope

CultMesh and Eve adoption in Aetheria, R3 for gameplay (UniRx stays), removing
Newtonsoft attributes from the 82 files that carry them, and a state service
layer.

## Open questions for the Imagination pass

1. Is routing a C# cache behavior only, or a semantic every runtime's cache must
   match? What do the Rust, Python, and TypeScript caches do with multiple
   stores today?
2. How do Unity.Mathematics values encode so other runtimes can read them? Does
   CultMath already define portable shapes?
3. What replaces `System.Type` fields (`StatModifierData.RequireBehavior`),
   which have no portable encoding?
4. Do CultNet or CultMesh depend on multi-store replication, on the generic
   `UpsertAsync<T>` declared-type behavior, or on exact-type watches?
5. Can existing `.cc` stores in other projects hold records written under a
   declared parent schema, and what does fixing `UpsertAsync<T>` do to them?
6. Which consumers pin which CultLib revision, and what is the release and
   upgrade order?
7. Where exactly do Aetheria's world types fall on the document/value line, and
   where are the save points?
8. How do catalog identity comparisons (`DatabaseEntry.Equals` on ID,
   `Dictionary<Faction,…>`, `Hull.Data.LinkID == data.ID`) survive without
   `DatabaseEntry.ID`?

## Process

A mini Epiphany loop. Imagination (a Fable agent) maps the cut into
`docs/cultcache-migration-cut.md`: authority maps, sequenced independently
executable cuts, deletion lines, negative checks, parity verification, and
build budgets. Hands (Opus agents) execute the cuts. Soul (a Fable agent)
verifies each against the map and the invariants above.
