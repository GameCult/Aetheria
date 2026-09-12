# CultCache Migration Cut

Date: 2026-09-12

Status: cut map. Ends are owned by `cultcache-migration-target.md`; this
document owns the means. Nothing here is cut until the operator commits the
five pending ServerShared edits (Cut 0).

Evidence base: CultLib `main` at `c2a9a6e`; Aetheria `codex/aetheria-state-rebuild`
at `68fbce4a` plus five uncommitted files. Line numbers refer to those revisions.

Canonical runtimes are the C# reference under `CultLib\src` and the packages
under `CultLib\packages` (`cultcache-ts`, `cultcache-rs`, `cultcache-py`,
`cultnet-*`, `cultmesh-*`). The neighboring repos `F:\Projects\cultcache-rs`,
`cultcache-py`, `cultnet-rs`, `cultnet-ts` are defunct copies (operator
correction, 2026-09-12): no parity claim, no evidence, and no cut in this
document refers to them, and the target document's list is being corrected.

## 1. Answers to the open questions

### Q1. Is routing a C# behavior or a cross-runtime semantic?

**Routing is a cache semantic every sibling already has. C# is the outlier, and
no bytes change.**

- TypeScript routes by type: `addBackingStore(store, ...types)` and
  `#resolveRoute` pick the stores registered for the type, else the untyped
  stores; the first is primary, the rest are mirrors
  (`CultLib\packages\cultcache-ts\src\cult-cache.ts:180-192, 663-677`, writes at
  `401-402, 447-448, 502-503`).
- Rust routes the same way (`packages\cultcache-rs\src\lib.rs:1950-1963,
  2363-2380`); batch puts require exactly one route per type and one store per
  batch (`2174-2186`). Its README claims this "mirrors the C# behavior"
  (`packages\cultcache-rs\README.md:216-241`); that claim is false today.
- Python keeps `stores_by_type` and `generic_stores` and writes to every store
  in the resolved list (`packages\cultcache-py\src\cultcache_py\cache.py:23-24,
  100-105, 199-201, 300-302`).
- C# replicates every record to every attached store
  (`CultLib\src\GameCult.Caching\CultCache.cs:2097-2101, 2135-2139`) and pushes
  all entries into a newly attached store (`1436-1439`). The typed overload
  `AddBackingStore(CacheBackingStore, Type[])` existed in the pre-rewrite C#
  cache (it survives as a dead symbol in
  `tests\GameCult.Networking.Tests\lcov.info:460`) and was dropped by
  `da94389 Rewrite CultCache as attribute-first storage`.
- No runtime writes store identity into the file. Every runtime writes the same
  snapshot `[formatVersion, catalog[], records[]]` with records
  `[key, schemaId, storedAt, payload]` (C# `CultDocumentMessagePackSerialization.cs:180-251`;
  TS `single-file-messagepack-backing-store.ts:203-232`; Rust `lib.rs:244-280,
  2401-2455`; Python `stores.py:137-152`). `storeId` is an optional header field
  in the contract (`cultcache-persistence-format.md:93`) that nothing implements.
  The CultNet wire record is likewise `schemaId + key`
  (`packages\cultnet-rs\src\replication.rs:36-60`).

Decision: routing is written into the contract as a **cache composition rule**,
not a wire rule. C# adopts one home store per type with no mirrors, which is a
strict subset of what TS and Rust do. Sibling caches change no code; the
cultcache-rs README sentence is corrected. Every `.cc` file a routed cache emits
is a complete single-store file readable by any runtime, and that is the parity
claim to test (section 3).

### Q2. How do Unity.Mathematics values encode portably?

**As fixed-length positional arrays of components, which is already Aetheria's
wire shape. CultMath defines no encoding and is not adopted in this migration.**

- Aetheria's formatters write `float2` as `[f32,f32]`, `float3` as three,
  `float4` as four, `int2` as `[int,int]`, `bool2` as `[bool,bool]`
  (`Aetheria\Assets\Scripts\ServerShared\CultCache\Serialization\MathFormatters.cs:8,
  49, 90, 131, 177`); `MathResolver.cs` maps the types, nullables, arrays and
  lists, with one bug at `:55` (`int2?[]` mapped to `ArrayFormatter<float2?>`).
  `bool2[,]` goes through MessagePack's `TwoDimensionalArrayFormatter` as
  `[len0, len1, [flat...]]`.
- CultMath lives at `CultLib\packages\cultmath` (`float2`, `float3`, `float4`,
  `int2`, `bool2`, `double2/3`, `quaternion`, `rect`, `Color32`, `Random`) with
  no MessagePack attributes, formatters, ext types, or serde in its Rust core.
  No sibling runtime defines a vector shape.
- The persisted member type name is the CLR full name
  (`CultGeneratedDocumentMetadata.cs:258-282`, used at `CultCache.cs:710`), so a
  `float2` member hashes into the schema id as `Unity.Mathematics.float2`. That
  is the existing rule for every member (`System.Int32` and friends); the
  generator does the same (`CultDocumentMessagePackGenerator.cs:354-384`). A
  sibling would have to emit the same string only if it declared the same schema,
  and none does.
- CultLib has no consumer formatter extension point: `Options` is a
  `static readonly` composite of `CultDocumentResolver` and `StandardResolver`
  (`CultDocumentMessagePackSerialization.cs:50-55`), and generated serializers
  read that same static at call time
  (`CultDocumentMessagePackGenerator.cs:290, 308`).

Decision: Cut 3 adds one registration call that composes consumer resolvers
ahead of `StandardResolver` before the first serialization, and the contract
gains a rule: **vector-like value types encode as fixed-length positional arrays
of primitive components; no ext types, no maps.** Aetheria keeps
Unity.Mathematics in `ServerShared` (26,566 lines lean on `Unity.Mathematics.math`)
and registers its existing formatters through that call. Switching to CultMath
is a separate migration and is rejected here (section 6).

Known non-portable shape, recorded and left alone: `EntityPack` carries
`Dictionary<int2, PersistentBehaviorData[]>` (`EntitySerializer.cs:183`), a map
with array keys. It lives in the Run store, which no other runtime reads.

### Q3. What replaces `System.Type` fields?

**A string holding the `BehaviorData` subclass simple name, matching the sibling
field that already does this.**

- The only persisted `Type` is `StatModifierData.RequireBehavior`
  (`ServerShared\Behaviors\StatModifier.cs:22-23`), written as
  `AssemblyQualifiedName` by `TypeFormatterResolver.cs:17`, read only at
  `StatModifier.cs:73, 80` as an exact `GetType() ==` filter.
- `StatReference.Target` (`StatModifier.cs:136-137`) has the same
  `[InspectableType(typeof(BehaviorData))]` and is already a `string` type name
  resolved by name at `:63`.
- The closed set is the `[Union]` list on `BehaviorData`
  (`Behaviors\Behaviors.cs:152-190`).

Decision: `RequireBehavior` becomes `string`; the comparison becomes
`b.GetType().Name == _data.RequireBehavior`; the importer maps the stored
assembly-qualified name to `Type.GetType(name).Name`. `TypeFormatterResolver.cs`
is deleted. Rejected: the union tag as an `int` (opaque in the Studio and bound
to MessagePack-C#), and keeping a CLR name string (that is what broke portability).
No operator decision needed.

### Q4. Do CultNet or CultMesh depend on replication, declared-type schema, or exact-type watches?

**No. Every CultNet and CultMesh cache has one store; no caller writes through a
base type; every watch names a concrete leaf. Three things do lean on current
mechanics and are named in the authority map.**

- Replication: every `AddBackingStore` caller in CultLib attaches one store per
  cache (`CultCacheMessagePack.cs:119, 128`, one branch or the other;
  `CultMesh.cs:1589` `DocumentFromStore`; `CultNetLocal.cs:146` through
  `OpenAsync`; `CultCacheStudioWindow.cs:688` by reflection). Every consumer
  does too (Aquarium `:24-26`; AquaSynth, Ymir, Mimir, Gjallar and AetheriaEve
  all through `OpenAsync`/`Create`). Tests that show two `AddBackingStore` calls
  are two caches on one file (`BackingStoreTests.cs:27/40, 67/76, 822/825,
  856/864, 909/915, 1501/1509`; `CultMeshStreamingTests.cs:2402-2475,
  2634/2643, 2688/2697`), which is sharing, not replication. `CultNetLocal.cs:148`
  reads `cache.BackingStores.OfType<SingleFileMessagePackBackingStore>().FirstOrDefault()`;
  the property stays.
- What does lean on attach-time push (`CultCache.cs:1436-1439`): callers that
  construct the cache with globals materialized and attach afterwards:
  `CultCacheMessagePack.Create` (`:75`), `CultMesh.DocumentFromStore`
  (`CultMesh.cs:1589`), Aquarium, and most tests. The push is also a live bug: it
  marks the store dirty before the first pull, and `SingleFileBackingStore.PullAll`
  returns without loading when dirty (`CultCache.cs:2572-2573`), so a `Create()`
  over a registry with any `[CultGlobal]` type never reads its own file. The
  authority map gives these pre-attach defaults an explicit owner instead.
- Declared-type schema: `CultNetDatabase.PutAsync<T>` and `PutPredictedAsync<T>`
  take the descriptor from `GetRequired<T>()` for shard, schema and log
  (`CultNetDatabase.cs:869-902, 908-927`), while `PublishCacheUpdate` already
  uses `document.GetType()` (`:1214-1215`), as do `CultNetDocumentRegistry.cs:327`,
  `CultNetDatabaseServer.cs:391-398` and `CultNetDatabaseSubscriptionServer.cs:504-513`.
  `CultMesh.cs:3515-3538` converts a same-schema value to the stored CLR type and
  calls `PutAsync<storedType>` by `MakeGenericMethod`, so its runtime type equals
  `T`. No caller passes `object`, an interface, or a base class, and no CultNet
  fixture uses document inheritance. Cut 2 makes `PutAsync<T>` derive its
  descriptor from the runtime type so CultNet and the cache stop having two
  answers; no wire change follows because every existing call has `T` equal to
  the runtime type.
- Exact-type watches: `CultNetDatabase.Watch<T>` (`:1134-1140`) is its own
  exact match on `CultNetDatabaseChange<documentType>`; `WatchByName`/`WatchByIndex`
  (`:1171-1189`) filter it through `_cache.GetByName<T>`/`GetByIndex<T>`. Every
  caller names a leaf (`CultMeshGameSession.cs:155`, `Server.cs:490-733`,
  AquaSynth, Brokkr, AetheriaEve `WatchRecord<T>`). `CultMesh.Collection<T>`
  (`CultMesh.cs:2289-2290`) snapshots with the assignable `GetAll` and streams
  with the exact `Watch`; after Cut 2 both are assignable and the inconsistency
  closes by itself. `CultNetDatabase.Watch<T>` stays exact in this cut: nothing
  needs it changed, and changing it is scope.
- Transactions: `CultNetDatabase.ExecuteTransactionAsync` (`:519-555`) wraps the
  cache primitive; the only non-test caller is `AetheriaStateNode.cs:460` on a
  single directory store. No test asserts the single-store rule; the message
  exists only in source.
- The Studio reaches the cache by string reflection (`OpenAsync`, `GetRequired`,
  `UpsertAsync(Type, object, CultRecordKey?)`, `Remove`, `FlushAsync`, `IsDirty`
  at `CultCacheStudioWindow.cs:688-739`); those signatures are frozen for Cut 2.
- The generator cannot see Aetheria's hierarchies: it requires `[CultDocument]`
  on the declaring type, discovers members with `GetMembers()` on that type only
  (`CultDocumentMessagePackGenerator.cs:43-50, 80`), and has no `[Union]`
  handling. That confirms Cut 6's choice not to reference it.
- Stale docs describing the old typed overload and mirrors: `CultLib\README.md:240-264`,
  `src\GameCult.Caching\GameCult.Caching.txt:157`, the generator README. Cut 1
  rewrites them.

### Q5. Can existing stores hold records written under a declared parent schema?

**No consumer writes one, and the fix does not touch existing bytes.**

- Aquarium writes two fixed keys with `AddAsync(state, new CultRecordHandle<T>(key))`
  where `T` is the concrete class
  (`Aquarium\src\Aquarium.Epiphany\State\AquariumCultStateStore.cs:82-83, 95-96`).
- AetheriaEve calls `PutAsync<T>` and `UpsertAsync` with concrete document types
  everywhere (`Aetheria.State\AetheriaStateNode.cs:360-554`,
  `Aetheria.State.Daemon\AetheriaYmirPersistenceCoordinator.cs:212, 269`); it has
  no document subclasses, only interface implementations.
- Ymir's four `UpsertAsync` calls are typed to the concrete document
  (`Ymir\src\Ymir.Core\...\YmirServicePublicationDocument.cs:169-177`,
  `YmirWorldStateDocument.cs:255`).
- Heimdall uses only cultcache-ts; Idunn only cultcache-rs. Neither has C#.
- Delvehold references Caching and the generator but was not audited for calls;
  Cut 2's Soul step greps it.

What the fix changes: `CreateStoredDocument(typeof(T), ...)` at
`CultCache.cs:1632, 1641` becomes `document.GetType()`. Records already on disk
keep their persisted `schemaId` and read back exactly as before. The new loud
failure: passing an instance whose runtime type has no `[CultDocument]` throws
from `GetRequired` (`CultCache.cs:493-498`) instead of silently storing the base
shape. The payload path already serialized the runtime type
(`CultDocumentMessagePackSerialization.cs:465`), so the old behavior was a
schema-id lie over a subclass payload, which is exactly what the probe saw.

### Q6. Which consumers pin which revision, and what is the release order?

| Consumer | Takes CultLib by | Pin | Affected by this cut |
|---|---|---|---|
| AetheriaEve | Unity UPM git URL (two projects) + .NET sibling `..\..\CultLib` | `b9cbd75` (package 1.0.56), 94 behind | Not until it re-pins; then unaffected (single directory store per cache, concrete `PutAsync`) |
| Aquarium | .NET sibling checkout, unpinned (`Aquarium.Epiphany.csproj:14-15`) | none | Compiles against `main` immediately; single store, concrete types; must build after Cut 2 |
| Ymir | .NET sibling checkout, unpinned (`Ymir.Core.csproj:4-14`) | none | Same as Aquarium |
| Delvehold | .NET sibling with a hard revision guard (`Directory.Build.props/targets`) | `334e60f`, 52 behind | Not until it re-pins |
| Heimdall | submodule, TS packages only | `b6b1d9c` | No |
| Idunn, Epiphany, Odin, Ghostlight, Muninn, Ratatoskr, CodexConnector | Cargo by commit | various | No (Rust cache unchanged) |
| Huginn, Sai, Stonks, Vili, EvePlugins | npm sibling paths | none | No |
| Aetheria | nothing yet | n/a | The consumer this cut serves |

Release mechanics: the Unity package template `unity\org.gamecult.cultlib\package.json`
is at `1.0.56` with committed DLLs under `Runtime\Plugins`; the newest release
tag is `cultlib-unity-v1.0.46` (`1fc68a4`, 2026-08-17). Versions 47 through 56
were never tagged, which is why AetheriaEve pins a raw commit. Install docs are
stale (`docs\nuget-packaging.md:22` says v1.0.46, the package README says
v1.0.41). CultCache Studio ships as a separate package,
`org.gamecult.caching.unity` 1.0.0 at `src\GameCult.Unity\Assets\Caching`, never
tagged, referenced by no consumer. No workflow publishes C# or Unity artifacts;
`publish-packages.yml` covers only npm and PyPI tags.

Order: CultLib `main` (Cuts 1-4) -> Cut 5 tags `cultlib-unity-v1.0.57` and
`caching-unity-v1.1.0` -> Aetheria consumes the tags (Unity) and the tagged
commit (headless, through a Delvehold-style revision guard). No other consumer
is forced to move. Aquarium and Ymir are built in Cut 2's verification because
they track `main` unpinned.

### Q7. Where do Aetheria's world types fall, and where are the save points?

Corrections to the target first: the root union has 30 tags, not 29
(`DatabaseEntry.cs:23-54`); five name classes that are not `DatabaseEntry`
subclasses (tags 4, 5, 6 item instances; 14 `OrbitalEntity`; 20 `Ship`), so
they were never cache records; and two `DatabaseEntry` subclasses have no tag
at all (`ConsumableItemData`, `ItemData.cs:348`; `PatrolOrbitsTask`), so they
could never have been written. Agent tasks are not persisted today:
`Zone.Agents` (`Zone.cs:35`) is absent from `ZonePack`.

| Type | Store | Kind | Why |
|---|---|---|---|
| SimpleCommodityData, CompoundCommodityData, GearData, HullData, CargoBayData, DockingBayData, WeaponItemData, ConsumableItemData | Catalog | document | referenced by `ItemInstance.Data`, `FactionProductData.Design`, `Faction.BossHull`, `WeaponData.AmmoType` |
| Faction | Catalog | document | referenced by `ItemData.Manufacturer`, `EntityPack.Faction`, `SavedGame.Factions`, `Faction.Allegiance` keys |
| FactionProductData | Catalog | document | referenced by `ItemInstance.Product` (`ItemInstance.cs:49`) |
| PersonalityAttribute | Catalog | document | keys in `Faction.Personality`, `CompoundCommodityData.DemandProfile` |
| NameFile | Catalog | document | `Faction.GeonameFile` (`Corporations.cs:40`), read at `Galaxy.cs:338` |
| GalaxyMapLayerData (tag 8), PlayerData (tag 11) | dropped | - | no live reference (`ItemManager.cs:20` is commented out; `PlayerData` referenced by nothing). Not imported. |
| BehaviorData and subclasses, WeaponData, ItemRole | inside item design | value | owned by `EquippableItemData.Behaviors` (`ItemData.cs:351`) |
| OrbitData | Run | document | referenced by Guid from `BodyData.Orbit`, `OrbitData.Parent`, `OrbitalEntityPack.Orbit`, `MoveTo.Orbit`; created at runtime (`Zone.cs:120, 265`) |
| BodyData: PlanetData, GasGiantData, SunData, AsteroidBeltData | Run | document | referenced by `Mining.Asteroids`, `Survey.Planets`, `MiningToolData.AsteroidBelt`, `Zone.Planets` keys |
| SavedZone (with its ZonePack) | Run | document | one per zone; holds `CultRecordRef` lists to its orbits and bodies |
| SavedGame | Run | `[CultGlobal]` document | the run root: factions, home/boss zones, current zone, action-bar bindings |
| EntityPack (ShipPack, OrbitalEntityPack), ItemInstance and subclasses, PersistentBehaviorData, SavedActionBarBinding | inside SavedZone / SavedGame | value | no identity; `SavedActionBarBinding` indexes into the run's entity (`SavedGame.cs:111-118`), so it stays with the run rather than the Player store the target table names |
| AgentTask and subclasses | not persisted | value owned by `Agent` | not saved today; this migration adds no persistence. `Reserved` suggests shared ownership; if tasks are ever persisted they become Run documents |
| Entity, Ship, OrbitalEntity, Zone, Galaxy | live simulation | not records | stale union tags 14 and 20 are deleted |
| PlayerSettings | Player | `[CultGlobal]` document | name, tutorial flag, credits; `SavedRun` leaves it and becomes the Run store |
| InputLayout | Player | document keyed by layout name | today one file per layout under `GameData\KeyboardLayouts` |
| Brush, InputLayout row/column unions | value | - | not cache data |

Save points today (`Gameplay\ActionGameManager.cs` unless noted):
`SavePlayerSettings` (`:76-79`) writes `GameData\PlayerSettings.msgpack`, which
embeds the whole run; `SaveState` (`:240-249`) builds it via `Zone.PackZone()`
(`Zone.cs:107-118`). Triggers: quit (`:238`), wormhole entry (`:611`), death
(`Die`, `:1057-1065`, which nulls the galaxy but leaves `SavedRun` on disk),
settings Back (`UI\MainMenu.cs:237`), new game (`MainMenu.cs:139, 168`). Two
writers are dead or orphaned: `SaveLoadout` (`:233-236`, path assignment
commented out at `:272-275`) and `SaveZone` (`:1093-1094`, no callers). Key
rebinding writes `GameData\KeyboardLayouts\*.msgpack`
(`UI\InputScreen\InputDisplayLayout.cs:495-501`). Loads: the catalog at
`ActionGameManager.cs:49-57` (which also writes, see Q1 of the target), player
settings at `:65-72`, run resume at `MainMenu.cs:97-106` through
`Galaxy(CultCache, SavedGame, ...)` (`Galaxy.cs:47-90`).

After the cut there is one run save path (`SaveRun`) used by quit, wormhole and
menu; `Die` deletes the run store file; new game creates a fresh one.

### Q8. How do identity comparisons survive without `DatabaseEntry.ID`?

**Documents are singleton instances per cache, and identity is the record key.
Reference equality replaces ID equality; keys replace stored Guids.**

- The only `Equals`/`GetHashCode` override is on `DatabaseEntry`
  (`DatabaseEntry.cs:60-69`); no subclass overrides. It goes with the class.
- The cache holds exactly one instance per key (`_entries`, `CultCache.cs:1274`)
  and `Get` returns that instance (`:1710-1719`), so the seven dictionaries keyed
  by `Faction` or `OrbitData` (`Galaxy.cs:18, 19, 28, 34`; `ZoneGenerator.cs:97,
  243`; `UI\Menu\SectorMap.cs:51`) work with the default comparer once the
  override is gone. The precondition is that no code deserializes a second copy
  of a catalog document; saves embed references, not copies, so this holds.
- The 14 explicit `.ID ==` comparisons (`Entity.cs:291, 304, 319`;
  `LoadoutGenerator.cs:159, 161`; `Narrative\ZoneConstraints.cs:37, 52`;
  `Zone.cs:316`; `UI\Menu\SectorRenderer.cs:65`; `TradeMenu.cs:292, 300`;
  `TradeMenuDebug.cs:275, 283`) become reference comparisons where both sides are
  documents, or `CultRecordRef.Key` comparisons where one side is a reference.
- Guid-keyed runtime dictionaries (`Zone.cs:59-77, 122, 265`;
  `Zone Display\ZoneRenderer.cs:342-413`; `ItemsOfType` in `Entity.cs:331, 334,
  1332, 1489-1589`, `TradeMenu*.cs`) key by `CultRecordKey` taken from the
  `CultRecordRef` on the instance. `ItemInstance.Data` and
  `SavedActionBarConsumableBinding.Target`, the only two `DatabaseLink<T>` fields,
  become `CultRecordRef<T>`; all other references are already raw `Guid` fields
  and become `CultRecordRef<T>` too.
- Saves that store faction Guids (`SavedGame.cs:56-73`, `EntitySerializer.cs:43`)
  store `CultRecordRef<Faction>`.
- The key of a document in hand comes from `CultCache.TryGetHandle`
  (`CultCache.cs:1699`). No helper type is added; the four `DatabaseLinkBase.Cache`
  writers (`CultCache.cs:42`, `ActionGameManager.cs:49`, `DatabaseView.cs:107`,
  `AetherDb.cs:23`) go with the static, and `ItemManager.GetData`
  (`ItemManager.cs:62-75`) is the single resolution path for item designs.
- `ZoneGenerator.cs:164` names a planet from the first eight characters of its
  ID; it uses the first eight characters of the key.

Catalog keys carry the legacy Guid in `D` format. Run and Player keys are
whatever the cache mints (`N` format, `CultCache.cs:2200`). Keys are opaque
strings; the mix is fine.

## 2. CultLib authority map: store routing

Written in the Loud Rebuild Contract form. It governs Cut 2.

**Owner.** The route table inside `CultCache`: `AddBackingStore(store)` for the
sole unrouted store, `AddBackingStore(store, params Type[] homeTypes)` for routed
stores, and one private `HomeStore(CultDocumentDescriptor)` that resolves the
most specific assignable route. Nothing else decides where a record lands.

**Inputs.** The record's `Descriptor.DocumentType`, which is now always the
runtime type (`CreateStoredDocument(document.GetType(), ...)`); the route table;
each store's `IsReadOnly`; whether the cache has hydrated (any `Pull*` has run
on any attached store).

**Pre-attach defaults.** `new CultCache()` and `CultCacheMessagePack.Create`
materialize `[CultGlobal]` defaults before any store exists, and Aquarium,
`CultMesh.DocumentFromStore` and most tests rely on that order. Those entries
are owned by a `_pendingDefaults` set until the cache can tell whether they are
missing from durable storage. `AddBackingStore` no longer pushes them. They are
admitted, in one place (`AdmitPendingDefaults`), at the end of every `Pull*`
(each default whose key was not loaded from its home store is routed and pushed
as a mutation) and at the start of every flush or transaction commit that runs
before any pull. A default whose home store is read-only is dropped from the
pending set and logged. This is also the fix for the pre-pull dirty store that
makes `SingleFileBackingStore.PullAll` skip loading (`CultCache.cs:2572-2573`).

**Outputs.** Exactly one `store.Push`, `store.Delete`, or `store.CommitBatch`
per mutation, on the home store. `IsDirty` is `any store IsDirty` when stores are
attached and the in-memory flag only when none are.

**Derived state.**
- `_hasUnflushedMutations` is no longer an owner after hydration; it is derived
  from the stores. `RecomputeDirtyState` (`CultCache.cs:2279-2284`) is deleted.
- A store's `IsDirty` is derived from `Push`/`Delete`/`CommitBatch` on that
  store only. `PullAll` sets it false and nothing on the load path sets it true
  (`SingleFileBackingStore.PullAll:2614`, `DirectoryMessagePackBackingStore.PullAllCore:224`).
- Global materialization (`InitializeGlobals`, `:2155-2186`) is a mutation. It
  routes like any other. A global whose home store is read-only is not
  materialized; `GetGlobal<T>` returns null and the cache logs it. A missing
  catalog global is a data defect the Studio fixes, not something the runtime
  invents at startup.
- `_globalKeys`, `_nameMaps`, `_indexMaps` stay keyed by concrete type; typed
  lookups search every registered key assignable to `T`. Ambiguity (two
  assignable types both holding the name, or two assignable globals) throws.
- `Watch<T>` is derived from the same change stream but projects a change of a
  runtime type assignable to `T` into `CultCacheDocumentChange<T>`
  (`CultManagedDocument.cs:34` is a sealed invariant generic, so `is
  CultCacheDocumentChange<T>` at `CultCache.cs:1404` can only ever match exactly).

**Forbidden writers.**
- `AddStoredDocumentInternal` and `RemoveStoredDocumentInternal` may not push to
  or delete from any store when `source != null` (the load path). The loops at
  `CultCache.cs:2097-2101` and `2135-2139` are deleted, not conditioned.
- `AddBackingStore` may not push existing entries into the new store
  (`:1436-1439`, deleted). Attaching after hydration throws; attaching while the
  cache holds only pre-attach defaults is the normal `Create()` order.
- `CultNetDatabase.PutAsync<T>` and `PutPredictedAsync<T>`
  (`CultNetDatabase.cs:869-927`) may not take their descriptor from `T`; they
  take it from `document.GetType()`, as `PublishCacheUpdate` (`:1214-1215`)
  already does. Every existing call passes the runtime type as `T`, so no
  behavior or bytes change.
- The signatures the Studio reflects on (`OpenAsync`, `GetRequired`,
  `UpsertAsync(Type, object, CultRecordKey?)`, `Remove`, `FlushAsync`, `IsDirty`)
  and the public `BackingStores` property that `CultNetLocal.cs:148` reads are
  frozen.
- `CommitTransaction` may not iterate all stores (`:1957-1958`). It resolves the
  home store of every staged mutation; if they resolve to more than one store it
  throws before touching any store. The `_backingStores.Count > 1` guard at
  `:1933-1937` is deleted; a routed cache with three stores may transact as long
  as the batch lands in one.
- A load event for a record whose home store is not the emitting store throws
  from `AddStoredDocumentInternal`; the record is not admitted.
- A `Push`, `Delete`, `CommitBatch` or `PushAll` on a read-only store throws
  inside the store, and the cache checks `IsReadOnly` on the home store before
  it mutates `_entries`, so in-memory state never diverges from a refused write.
- `Directory` store index upgrade (`_needsIndexUpgrade`,
  `DirectoryMessagePackBackingStore.cs:362, 399`) is a flush-time format
  rewrite, not dirtiness; it is left alone for writable stores and never runs on
  a read-only store because the cache never flushes one.
- The generic `T` of `AddAsync<T>`/`UpsertAsync<T>` no longer chooses the
  descriptor. `UpsertAsync(Type, object)` keeps its `IsInstanceOfType` check as an
  argument guard and also uses the runtime type.

**Shared paths.** `AddAsync`, `UpsertAsync<T>`, `UpsertAsync(Type, object)`,
`Remove`, `InitializeGlobals`, and `CommitTransaction` all go through
`HomeStore(descriptor)` then the store's own `Push`/`Delete`/`CommitBatch`. The
load path (`EntryAdded`/`EntryUpdated`/`EntryDeleted` subscriptions at
`:1432-1434`) goes through `AddStoredDocumentInternal(source: store)` and stops
at the cache's in-memory state.

**Named demotions.**
- Replication is no longer an owner of durability; durability is derived from
  the home store alone.
- `_hasUnflushedMutations` is no longer an owner once a store is attached; it is
  derived from `store.IsDirty`.
- `typeof(T)` in `AddAsync<T>` is no longer an owner of schema; schema is derived
  from `document.GetType()`.
- The `Count > 1` transaction rule is no longer an owner; atomicity is derived
  from "all staged mutations share one home store".
- Exact `Descriptor.DocumentType` is no longer an owner of typed lookup results;
  results are derived from assignability, as `GetAll<T>` already does
  (`:1751-1759`).

**Deletion line (cut before anything is added).**
`CultCache.cs:1436-1439` (push-all on attach), `2097-2101` and `2135-2139`
(replication loops), `1933-1937` (single-store transaction guard),
`2279-2284` (`RecomputeDirtyState`) and its callers at `2106`, `2144`, `1506`,
`1553`; the `_hasUnflushedMutations = _backingStores.Any(...)` recomputations at
`1459`, `1484`, `1991`; `typeof(T)` at `1632`, `1641`. No test in
`tests\GameCult.Caching.Tests` attaches two stores to one cache (every
`AddBackingStore` call at `BackingStoreTests.cs:27-1602` is one store per cache;
the two-store cases at `:822-825`, `:856-864`, `:909-915` are two caches on one
file), so no test is deleted for replication.

## 3. Wire-parity plan

| Change | Bytes on disk or wire | Runtimes that change | How parity is verified |
|---|---|---|---|
| Store routing, attach rules, read-only stores, loading never writes, transaction rule | none; each routed file is a complete `cultcache.store.v1` snapshot | C# only (siblings already route) | New C# test: a three-store routed cache and three single-store caches holding the same records with pinned `storedAt` produce byte-identical files. Cross-runtime: the C# interop peer (`tests\GameCult.Caching.InteropPeer\Program.cs`) gains a `write-routed` mode that writes two files; the existing writer-by-reader loop in `packages\cultcache-ts\test\cult-cache.test.ts:536-617` reads both with the unchanged TS, Rust, and Python readers |
| Runtime type decides schema | none for existing records; a new record written through a base-typed `T` now carries the subclass schema id and payload it always should have | C# only (siblings have no inheritance) | The canonical fixture ids in `Contracts\cultcache-schema-compatibility.md:20-24` must not move (a test already asserts them in `BackingStoreTests.cs`); a new test writes `Weapon` through `UpsertAsync<Gear>` and reopens it as `Weapon` |
| Assignable typed lookups and watches | none | C# only | unit tests; Mesh and Networking suites re-run as the regression gate |
| Consumer formatter resolver | payload bytes of consumer-owned schemas only; CultLib's own documents are unchanged | C# only | A test document with a custom value type round-trips through reflection and generated paths; the interop note document's bytes are unchanged |
| Contract text: composition rule, value-type encoding rule | none | doc-only: `cultcache-rs\README.md:216-241` corrected | review |

The hosted interop workflow (`.github\workflows\cultnet-interop.yml`) tests
CultNet frames only; `.cc` parity lives in the cultcache-ts test above, which
runs on `npm test` and on `cultcache-ts-v*` release tags. Byte-level comparison
across runtimes is not attempted: Rust writes a stub catalog and second-precision
`storedAt` (`lib.rs:2397-2433`), so it would fail today for reasons unrelated to
this cut. That is a pre-existing parity gap and is recorded, not fixed here.

## 4. Cut sequence

Each cut is an independently executable Hands task with its own Soul check.
Build hosts are the Windows workstation for .NET and Unity; every .NET target
below is built and tested on the host it runs on. Nothing builds for Linux.

### Cut 0. Commit the pending ServerShared edits (done)

- Done on `codex/aetheria-state-rebuild`: the five ServerShared edits are
  `6bd3e176`, verified by the headless `Aetheria.Shared` and `AetherDb` builds, a
  Unity 6000.3.24f1 batchmode compile (the agent can run Unity headless when the
  editor is closed), and `AetherDb doctor`, `station-fit`, `loadout`. The 6000.3
  settings reserialization is `560779a4`; the TMP fallback atlas that batchmode
  cleared was restored, not committed.
- Consequence for the cuts below: "operator Unity recompile" steps can be a
  batchmode compile run by the agent while the editor is closed. A play smoke
  still needs the operator.

### Cut 1. CultLib contract and red tests

- Repo/branch: CultLib, `codex/cultcache-store-routing`.
- Deletes first: nothing (documentation and tests only).
- Adds: `src\GameCult.Caching\Contracts\cultcache-store-composition.md` with
  the authority map of section 2, the value-type encoding rule, and the
  consumer-resolver rule; amends `cultcache-persistence-format.md:52-54`
  (transactions require one home store, not one attached store) and
  `cultcache-schema-compatibility.md` (runtime type decides schema; lookups are
  assignable). Corrects `packages\cultcache-rs\README.md:216-241`, and rewrites
  the stale typed-overload and mirror prose in `CultLib\README.md:240-264`,
  `src\GameCult.Caching\GameCult.Caching.txt:157`, and the generator README.
- Tests: `tests\GameCult.Caching.Tests\StoreRoutingTests.cs`, written against
  today's API so they compile and are red:
  1. two stores on one cache, pull the first, flush: the second store's file must
     not exist (red: replication writes it);
  2. `UpsertAsync<Gear>(new Weapon())`, flush, reopen, `Get<Weapon>` non-null
     (red: stored as `Gear`);
  3. `GetByName<AbstractBase>(name)` finds a concrete subclass record (red);
  4. `Watch<AbstractBase>()` receives a subclass change (red);
  5. `GetGlobal<AbstractBase>()` finds the concrete global (red);
  6. `CultCacheMessagePack.Create` over a registry containing a `[CultGlobal]`
     type, then `PullAllBackingStoresAsync`, loads the records already in the
     file (red: the attach-time push marks the store dirty and the pull skips).
  API-shape tests (routed attach, read-only refusal, attach-after-hydrate,
  overlapping routes, foreign-store load refusal, transaction across two homes,
  byte-identical routed files) arrive with Cut 2 and must pass there.
- Build budget: `dotnet test tests\GameCult.Caching.Tests` (net10.0, Windows).
- Soul: the five tests are red for the stated reasons and no other test changed
  state.

### Cut 2. CultLib routing implementation

- Repo/branch: CultLib, same branch, after Cut 1 and after Q4 is answered.
- Files: `src\GameCult.Caching\CultCache.cs`;
  `src\GameCult.Caching.MessagePack\CultDocumentMessagePackSerialization.cs`
  (`SingleFileMessagePackBackingStore` read-only flag) and
  `DirectoryMessagePackBackingStore.cs` (same); `CultCacheMessagePack.cs`
  (`CultCacheOpenOptions.ReadOnly`); `src\GameCult.Networking\CultNetDatabase.cs:869-927`
  (descriptor from the runtime type); `tests\GameCult.Caching.Tests\StoreRoutingTests.cs`;
  `tests\GameCult.Caching.InteropPeer\Program.cs` (`write-routed` mode);
  `packages\cultcache-ts\test\cult-cache.test.ts:536-617` (read the two routed
  files).
- Deletes first: the deletion line in section 2.
- New behavior: the authority map in section 2, exactly. Read-only is a
  constructor argument or init property on `CacheBackingStore`
  (`IsReadOnly`), enforced in the store and checked by the cache.
- Tests that must fail before and pass after: Cut 1's six, plus the API-shape
  tests, plus: a `Create()`-ordered cache whose default global is absent from the
  file writes it on first flush, and one whose global is present loads the
  persisted value and writes nothing. Negative checks: a catalog-routed record never appears in the run file
  (assert by deserializing the run snapshot and listing schema ids); opening a
  routed cache and flushing without mutation leaves every file's bytes unchanged
  (hash before and after); `AddBackingStore` after `PullAllBackingStoresAsync`
  throws; a record of a run-routed type found inside the catalog file throws on
  load and is absent from `AllEntries`; a transaction staging one catalog and one
  run record throws and neither store's `IsDirty` changes; a global whose home
  store is read-only is not materialized.
- Build budget: `dotnet build src\GameCult.Caching src\GameCult.Caching.MessagePack`;
  `dotnet test tests\GameCult.Caching.Tests tests\GameCult.Networking.Tests
  tests\GameCult.Mesh.Tests` (all net10.0, Windows; Mesh and Networking are
  minutes, not seconds); `dotnet build F:\Projects\Aquarium\src\Aquarium.Epiphany`
  and `F:\Projects\Ymir\src\Ymir.Core` (unpinned sibling consumers); `npm test`
  in `packages\cultcache-ts` for the interop loop (builds the C# peer, needs Rust
  and Python toolchains present).
- Soul: every negative check above, the Q4 dependencies confirmed untouched by
  the Mesh/Networking suites, Aquarium and Ymir compile, Delvehold grep shows
  no base-typed writes, the schema-compatibility fixture ids unchanged, and the
  six reflected Studio signatures plus `BackingStores` still resolve by name
  (a reflection assertion in the test project, since the Studio only fails at
  runtime).

### Cut 3. CultLib consumer formatter resolver

- Repo/branch: CultLib, same branch (separate files from Cut 2; may run in
  parallel with it).
- Files: `src\GameCult.Caching.MessagePack\CultDocumentMessagePackSerialization.cs`,
  a test in `tests\GameCult.Caching.Tests`.
- Deletes first: the `static readonly` initializer of `Options` (`:50-55`)
  becomes the default value of a once-settable property.
- New behavior: `CultDocumentMessagePackSerialization.ConfigureResolvers(params
  IFormatterResolver[] consumerResolvers)` composes the consumer resolvers ahead
  of `CultDocumentResolver` and `StandardResolver`; a second call, or a call
  after any serialization, throws. Generated serializers already read `Options`
  at call time (`CultDocumentMessagePackGenerator.cs:290, 308`), so they need no
  change.
- Tests: a document with a custom `struct Pair(float, float)` and a resolver
  that writes `[f,f]` round-trips through reflection and through the generated
  path; calling `ConfigureResolvers` twice throws; the interop note document's
  serialized bytes are unchanged with a consumer resolver installed.
- Build budget: as Cut 2's first two commands plus the Caching tests.
- Soul: the three tests; no other project touched.

### Cut 4. CultCache Studio absorbs Database Tools

- Repo/branch: CultLib, `codex/cultcache-studio-drawers` (independent of Cuts
  1-3 in files; depends on Cut 2 only for not writing on open).
- Files: `src\GameCult.Unity\Assets\Caching\Editor\CultCacheStudioWindow.cs`
  (847 lines, fixed `DrawValue` chain at `:326-364`), a new
  `CultCacheStudioDrawers.cs` beside it, `Assets\Caching\package.json`
  (`1.0.0` -> `1.1.0`).
- Deletes first: the "anything else as a disabled text field" fallback in
  `DrawValue`; unknown types now route through the drawer registry and fail
  visibly if nothing claims them.
- New behavior: dictionaries (key and value drawn recursively); abstract or
  interface members offer a subtype popup built from the member type's
  `[MessagePack.Union]` attributes (not from reflection over all subclasses, so
  the popup can only choose what the wire can encode); `CultRecordRef<T>` gets a
  record picker listing documents assignable to `T` by name; non-primitive
  structs keep the reflective path (this covers Unity.Mathematics and CultMath,
  verify rather than assume); a drawer extension point discovered through Unity
  `TypeCache` on a `[CultInspectorDrawer(typeof(MemberType))]` attribute, so a
  consumer's editor assembly can register drawers with no runtime coupling.
- Tests: none executable by the agent; Studio is Unity editor code. Operator
  opens `src\GameCult.Unity` in Unity, loads a `.cc` with each member kind, and
  confirms edit and save; opening then closing without edits leaves the file
  bytes unchanged.
- Build budget: Unity editor compile of `src\GameCult.Unity` (operator).
- Operator step: the compile and the manual check.

### Cut 5. CultLib release

- Repo/branch: CultLib `main` after Cuts 1-4 merge.
- Files: `unity\org.gamecult.cultlib\package.json` (`1.0.56` -> `1.0.57`),
  `unity\org.gamecult.cultlib\Runtime\Plugins\*.dll` via
  `scripts\build-unity-package.ps1 -UpdateTemplate`, `unity\org.gamecult.cultlib\README.md:15`,
  `docs\nuget-packaging.md:22`.
- Deletes first: the stale install lines.
- New behavior: tags `cultlib-unity-v1.0.57` and `caching-unity-v1.1.0` on the
  same commit; pushed.
- Verification: the committed DLLs are byte-identical to a fresh
  `artifacts\unity\org.gamecult.cultlib` build; `GameCult.Caching.dll` in the
  template exposes `AddBackingStore(CacheBackingStore, Type[])` (reflection
  check from a throwaway script); tags resolve on `origin`.
- Build budget: `dotnet publish` of `GameCult.Mesh`, `GameCult.Networking.WebSockets`
  (netstandard2.1) and `GameCult.Mesh.Quic.Native`, Release, Windows host, as
  the script does today.

### Cut 6. Aetheria data model cutover

- Repo/branch: Aetheria, new branch `codex/cultcache-cutover` from Cut 0.
  Depends on Cuts 0 and 5.
- Deletes first (before any new attribute is added):
  `Assets\Scripts\ServerShared\CultCache\CultCache.cs` (437),
  `DatabaseEntry.cs` (91), `ReflectionExtensions.cs` (127) and
  `CollectionExtensions.cs` (75) unless a non-cache user remains,
  `Serialization\JsonKnownTypes\**` (320, with its stray `.csproj`),
  `Serialization\TypeFormatterResolver.cs` (65), `Serialization\JsonConverters.cs`
  (169) if no `JsonConvert` caller survives, `Serialization\RegisterResolver.cs`
  (37); `Assets\Plugins\MessagePack\**` (97 files, 30,088 lines, and its
  asmdef); `Assets\Scripts\CultCache\Editor\**` (17 files, 1,963 lines); the
  `Assets\Plugins\MessagePack\**` compile include in
  `Aetheria.Shared\Aetheria.Shared.csproj:23`; the `MessagePack` reference in
  `Aetheria.Shared.Unity.asmdef`; the root `[Union]` list and every
  `JsonKnownTypes` attribute; the stale tags 4, 5, 6, 14, 20.
- Kept and moved: `MathFormatters.cs` and `MathResolver.cs` (fix `:55`) under
  `ServerShared\Serialization\`, registered by one `AetheriaSerialization.Configure()`
  that calls Cut 3's `ConfigureResolvers`; the `Inspectable*` attributes from
  `Attributes.cs` that Studio drawers will read.
- Adds:
  - `Directory.Build.props`/`Directory.Build.targets` at the Aetheria root,
    copied from `F:\Projects\Delvehold` (`CultLibRoot`, `CultLibRevision` set to
    the Cut 5 commit, the revision and clean-tree guard). Reused, not invented.
  - `Aetheria.Shared.csproj`: `ProjectReference` to `GameCult.Caching` and
    `GameCult.Caching.MessagePack` (both netstandard2.1). The MessagePack
    generator is not referenced: Unity cannot run it, and the target notes it
    silently falls back on hierarchies, so both bodies use reflection descriptors
    and behave the same.
  - `Packages\manifest.json`: `org.gamecult.cultlib` at
    `https://github.com/GameCult/CultLib.git?path=/unity/org.gamecult.cultlib#cultlib-unity-v1.0.57`
    and `org.gamecult.caching.unity` at
    `...?path=/src/GameCult.Unity/Assets/Caching#caching-unity-v1.1.0`;
    `Aetheria.Shared.Unity.asmdef` references `GameCult.CultLib` and keeps
    `noEngineReferences: true`.
  - `[CultDocument("aetheria.<type>", "1")]` on every concrete document type in
    the Q7 table; `[CultGlobal]` on `SavedGame` and `PlayerSettings`; `[CultName]`
    on the existing `Name` members. `[MessagePackObject]` is removed from abstract
    bases that are not union roots (`ItemData`, `CraftedItemData`,
    `EquippableItemData`, `BodyData`, `AgentTask`) to satisfy MsgPack005; the
    value unions (`ItemInstance`, `BehaviorData`, `WeaponData`, `EntityPack`,
    `SavedActionBarBinding`, `InputLayout`) keep theirs.
  - Every `Guid` and `DatabaseLink<T>` reference field becomes `CultRecordRef<T>`
    (`Dictionary<Guid, float>` becomes `Dictionary<CultRecordRef<T>, float>`;
    the ref formatter writes string keys, so the map stays portable).
  - `RequireBehavior` becomes `string` (Q3).
  - `ServerShared\AetheriaStores.cs`: one static `Open(catalogPath, runPath,
    playerPath, readOnlyCatalog)` that builds the routed cache. This is the
    composition root shared by the game, the tool, and tests; the route table is
    written once.
  - Identity changes of Q8 across the 75 Unity-side and 16 ServerShared call
    sites; `DatabaseLinkBase.Cache` gone; `ItemManager.GetData` is the resolution
    path.
  - A small Aetheria editor file (`Assets\Scripts\Editor\CultCacheDrawers.cs`)
    registering Studio drawers for `[InspectableType]` (behavior name picker),
    `[InspectableColor]` (`float3`/`float4` as color) and
    `[InspectableAnimationCurve]` (`float4[]`).
  - `tests\Aetheria.Shared.Tests` (xunit, net10.0) with the negative checks
    below; the headless counterpart of the Unity compile.
- Tests that must fail before and pass after (they cannot compile before; "fail
  before" is the headless build failing on the deleted types): open the routed
  cache on a copy of `GameData` whose run and player files already hold their
  globals, flush, assert all three files' hashes are unchanged; write a `WeaponItemData` through a `GearData`-typed call and read it
  back as `WeaponItemData`; writing to the catalog store throws and the catalog
  file is unchanged; a `SavedZone` never appears in the catalog file; the
  `Dictionary<Faction, ...>` in `Galaxy` resolves the same instance from two
  `Get<Faction>` calls.
- Build budget: `dotnet build Aetheria.Shared\Aetheria.Shared.csproj` and
  `dotnet test tests\Aetheria.Shared.Tests` (Windows host; CultLib is built as a
  project reference from the pinned checkout). Unity: one editor recompile after
  the cut, by the operator; the agent updates every Unity call site it can find
  by grep but cannot prove the Unity compile.
- Operator steps: Unity recompile; approve the two UPM git URLs (network fetch
  from GitHub on first import).
- Soul: headless build green; the five tests; `rg "DatabaseEntry|DatabaseLink|
  JsonKnownTypes|TypeFormatterResolver|MultiFileBackingStore" Assets tools` is
  empty; `Assets\Plugins\MessagePack` gone; the asmdef has no `MessagePack`
  reference; manifest pins the two tags; the revision guard refuses a dirty or
  wrong-revision CultLib.

### Cut 7. One-shot importer

- Repo/branch: Aetheria, same branch, after Cut 6.
- Files: `tools\AetherDb\Import.cs` (new), `Program.cs` (`import` and
  `legacy-census` commands), `GameData\Aetheria.cc` (new, committed), and
  `.gitattributes` gaining `*.cc filter=lfs diff=lfs merge=lfs -text` before the
  `.cc` is added: `AetherDB.msgpack` is LFS-tracked today and `.cc` matches no
  rule (`git check-attr filter` reports `unspecified`), so without the rule the
  catalog would land in plain git.
- Deletes first: `doctor` (it deserializes `DatabaseEntry[]`, which no longer
  exists), the `withNameFiles` branch and its comment in `AetherDb.cs:7-10, 25,
  34`.
- New behavior: the importer reads `GameData\AetherDB.msgpack` (a MessagePack
  array of `[tag, payload]`) and `GameData\NameFile\*.msgpack` (12 files, each
  `[tag, payload]`) with `MessagePackReader`, never through the legacy types.
  A tag table maps the 30 legacy tags to new document types (tags 4, 5, 6, 14,
  20 cannot occur; 8 and 11 are dropped and counted). The payload is rewritten
  slot by slot against the new type's `[Key]` members: a 16-byte `Guid` (the
  legacy `NativeGuidResolver`) or a legacy `DatabaseLink` array `[guid]` at a
  `CultRecordRef` slot becomes the Guid's `D` string; `Guid` map keys likewise;
  the `RequireBehavior` string becomes a simple name; nested `[MessagePackObject]`
  values and `[Union]` members are recursed by their own `[Key]` and `[Union]`
  metadata; everything else is copied. The rewritten payload is deserialized
  with CultLib and written with `AetheriaStores.Open` (catalog only, writable
  for this one command) under the legacy Guid `D` string as key. Slot numbers
  are untouched.
- Verification: `legacy-census` counts records per tag structurally; `import`
  prints the same per-type counts from the new cache; they must match minus the
  dropped tags, and every `CultRecordRef` in the new store must resolve. The
  `census`, `factions`, `hardpoint-fit` outputs (Cut 9 versions) must match
  their pre-cut outputs captured at Cut 0.
- Then, in the same commit: delete `GameData\AetherDB.msgpack`,
  `GameData\NameFile\`, and the 32 empty per-type folders the legacy
  `MultiFileBackingStore` constructor created (`CultCache.cs:237-244`). Keep
  `GameData\KeyboardLayouts` for Cut 8.
- Build budget: `dotnet run --project tools\AetherDb -- legacy-census`, then
  `-- import`, both net10.0 on Windows.
- Soul: counts match; `GameData\Aetheria.cc` deserializes with
  `CultDocumentMessagePackSerialization.DeserializeSnapshot` and every schema id
  in its records is in its catalog; no legacy file remains; `git status` shows
  the `.cc` added and the legacy files removed in one commit.

### Cut 8. Runtime cutover

- Repo/branch: Aetheria, same branch, after Cut 7.
- Files: `Assets\Scripts\Gameplay\ActionGameManager.cs`, `UI\MainMenu.cs`,
  `UI\InputScreen\InputDisplayLayout.cs`, `ServerShared\Galaxy.cs`,
  `ServerShared\SavedGame.cs`, `ServerShared\PlayerSettings.cs`,
  `ServerShared\Zone.cs`.
- Deletes first: the catalog bootstrap at `ActionGameManager.cs:49-57`;
  `SavePlayerSettings`/`SaveState` (`:76-79, 240-249`) as they stand;
  `SaveLoadout` (`:233-236`, dead path) and `SaveZone` (`:1093-1094`, no
  callers); the `PlayerSettings.msgpack` reader (`:65-72`); the per-file layout
  writer (`InputDisplayLayout.cs:495-501`) and reader (`:88-91`); the
  `GameData/PlayerSettings.msgpack` `.gitignore` line.
- New behavior: `ActionGameManager` opens the cache through `AetheriaStores.Open`
  with the catalog read-only, the run store at a per-run path, and the player
  store; `SaveRun()` is the single run save path (one `ExecuteTransactionAsync`
  over `SavedGame` and the `SavedZone`/`OrbitData`/`BodyData` records) and is
  called from quit, wormhole entry, and menu; `Die()` closes and deletes the run
  store file; new game creates a fresh run store; `SavedGame` becomes the Run
  global and `PlayerSettings` the Player global; `InputLayout` records live in
  the Player store keyed by layout name; `Galaxy(CultCache, SavedGame, ...)`
  resolves `CultRecordRef<Faction>`.
- Tests: headless, in `tests\Aetheria.Shared.Tests`: save a run, reopen, the
  same zones and refs resolve; delete-run leaves the catalog and player files
  byte-identical; a second `SaveRun` after a wormhole is the only writer of the
  run file (hash the run file, assert catalog and player hashes unchanged).
- Build budget: headless build and tests as Cut 6; Unity recompile and a play
  smoke (new game, one wormhole, quit, resume, die) by the operator.
- Soul: the three tests; the play smoke; `rg "PlayerSettings.msgpack|\\.zone|
  \\.loadout|KeyboardLayouts" Assets` is empty; exactly one call site writes the
  run store.

### Cut 9. AetherDb rewrite and final deletions

- Repo/branch: Aetheria, same branch, after Cut 8 has run.
- Files: `tools\AetherDb\AetherDb.cs`, `Program.cs`, `Import.cs`, `docs\`.
- Deletes first: `Import.cs` and the `import`/`legacy-census` commands (the
  one-shot has run; a reader with no data is a liability); `migrate-products`
  (already applied, `b8ecfffd`); the `Save()` method (`AetherDb.cs:37`), since
  the tool now writes through the cache.
- New behavior: `AetherDb.Open` calls `AetheriaStores.Open` (catalog writable
  only for `clear-boss-hulls apply`); `census`, `factions`, `station-fit`,
  `hardpoint-fit`, `loadout`, `settings`, `settings-dump` run over the new cache;
  `save` reads the run store; the help text lists every command.
- Docs: `cultcache-migration-target.md` status line to "done" with the commit
  range; a short "rejected paths" note stays in this document; the
  `three-gates-scope.md:61-68` "loading writes" paragraph is deleted because the
  smell no longer exists.
- Tests: each command exits 0 against the committed `GameData\Aetheria.cc`;
  `census` output equals the Cut 0 capture.
- Build budget: `dotnet build tools\AetherDb`, run each command.
- Soul: `rg "legacy|msgpack" tools docs` shows only history notes; `git ls-files
  GameData` lists `Aetheria.cc` and nothing legacy.

Steps needing the operator's Unity recompile: 0, 4 (CultLib's Unity project),
6, 8. The agent cannot perform them.

## 5. Subtraction ledger

Lines are C# unless noted; estimates are bounded by the measured sizes above.

| Cut | Removed | Added | Targets, dependencies, schemas |
|---|---|---|---|
| 1 | 0 | ~120 doc, ~120 test | 0 |
| 2 | ~45 (deletion line) | ~260 impl, ~350 test, ~40 interop peer, ~20 TS test | 0 new targets; `IsReadOnly` and the typed `AddBackingStore` overload are the only new public surface |
| 3 | ~6 | ~40 impl, ~60 test | 0 |
| 4 | ~10 (fallback) | ~400 editor | Studio package `1.0.0` -> `1.1.0`; 1 new attribute |
| 5 | 2 doc lines | 2 doc lines, rebuilt DLLs | 2 tags |
| 6 | 1,771 (ServerShared\CultCache) - ~305 kept formatters + 30,088 (MessagePack) + 1,963 (Database Tools) + ~60 (asmdef/csproj lines) ≈ **33,500** | ~60 `AetheriaStores`, ~200 drawers, ~200 tests, ~40 props/targets, ~400 attribute and reference edits ≈ **900** | -1 vendored MessagePack, -1 vendored JsonKnownTypes, -1 asmdef; +2 UPM packages, +2 ProjectReferences, +1 test project; 20 `[CultDocument]` schemas replace 1 union |
| 7 | ~50 (`doctor`, name-file branch), 2 legacy data files + 12 name files + 32 empty folders | ~300 importer (temporary), 1 `.cc` | 0 |
| 8 | ~120 (bootstrap, three writers, two readers) | ~90 (`SaveRun`, open path) | -3 file formats (`PlayerSettings.msgpack`, `.zone`, `.loadout`, `KeyboardLayouts\*.msgpack`) |
| 9 | ~300 (importer) + ~60 (dead commands) | ~40 | 0 |

Expected net: CultLib about +1,300 lines, of which ~530 are tests and ~400 is
editor tooling that replaces 1,963 lines in Aetheria; Aetheria about **-33,000**
lines, two vendored dependencies and four private file formats gone, one shared
package in.

## 6. Risks and rejected paths

Risks:
- The Unity compile is only provable by the operator, at Cuts 6 and 8. The
  headless build catches `ServerShared`; it cannot catch `Gameplay`, `UI`,
  `Zone Display`, or `Editor` call sites. Expect one round of fixups after each
  recompile.
- MessagePack moves from a vendored 2.x (no version string; `e9431261`
  quarantined its Unity assembly) to 3.1.7 with `MessagePackSecurity.UntrustedData`
  (`CultDocumentMessagePackSerialization.cs:55`). Deep `EntityPack` graphs may hit
  the untrusted-data depth limit; Cut 8's save test is where that shows.
- Unity runs no source generator, so documents serialize through
  `DynamicObjectResolver`. Aetheria's scripting backend is Mono (only Android is
  set in `ProjectSettings.asset:660-661`), so this works; IL2CPP would not.
- `RarityTier` and anything else marked `keyAsPropertyName` serializes as a map
  and would hash a different canonical schema than its array-shaped neighbors;
  it lives in Unity settings, not the cache, so it is out of scope, but any such
  type that turns out to be embedded in a document must be converted to keys.
- Any `ReactiveProperty` field reachable from a document breaks without the
  vendored `MessagePack.ReactiveProperty` resolver. The census found none
  (reactive state is on `Entity` and `EquippedItem`, which are not persisted),
  but Cut 6's Soul grep must confirm.
- Schema drift after the cut is governed by CultLib's slot comparison
  (`CultCache.cs:947-1027`); a type change in a slot is a hard reject. Aetheria
  field edits that change a slot's type need a new slot or a version bump, which
  is a discipline the legacy union never asked for.
- Cross-runtime byte parity of `.cc` files is a pre-existing gap (Rust stub
  catalog and timestamps). This cut does not widen it and does not close it.

Rejected paths:
- Mirrors or replication in C#. No consumer uses them, transactions already
  refused them, and the sibling mirror semantics are a local convenience the
  contract never promised.
- A `storeId` or route field in the file header. It would change bytes in every
  runtime to record a fact each file already implies by existing.
- A save converter. The target discards saves; a converter would be a second
  legacy reader.
- Keeping `DatabaseEntry` with an `ID` shim. The shim would be the surviving
  owner of identity and the whole point is to demote it.
- Switching `ServerShared` to CultMath. CultMath has no serialization and the
  math library touches 26,566 lines; that is a separate migration.
- Union tags as the `RequireBehavior` encoding, or per-field
  `[MessagePackFormatter]` attributes instead of a resolver. Both push encoding
  knowledge into thirty fields.
- A JSON intermediate for the import, or an `extern alias` reference to a
  pre-cut `Aetheria.Shared` build. A structural rewrite driven by the new
  types' `[Key]` metadata is smaller than either and is deleted after use.
- Referencing the MessagePack source generator from `Aetheria.Shared`. Unity
  cannot run it, and two bodies serializing by different codecs is exactly the
  split authority the migration exists to remove.
- Persisting agent tasks. They are not persisted today; adding it would be new
  behavior in a migration that moves the cache and nothing else.
