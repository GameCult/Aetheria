# CultCache Migration Cut

Date: 2026-09-13 (second Imagination pass; first pass 2026-09-12, `82c1e72e`)

Status: cut map. Ends are owned by `cultcache-migration-target.md`; this
document owns the means. Progress (update in each cut's landing commit):
Cuts 0-4 landed on CultLib `codex/cultcache-store-routing` and passed their
Soul passes (Cut 4 ended at `5bd4e4e`, generator deleted, registry aligned to
MessagePack round-trip semantics; the five `GameCult.Eve.Surface` documents'
schema ids change because their constructor-filled get-only members now enter
the catalog, and no store on disk holds them). Cut 5 landed (ended at
`47c7111`): mirrors deleted in TS, Rust and Python; each sibling enforces one
home store, a record's home cannot move, refusal at load from a non-home
store, zero stores in memory, validation before any store write, a TS per-cache
serial queue and a Python per-cache lock, globals only under `__global__` with
a legacy-key load shim. Re-pin follow-ups: TS `addBackingStore`,
`addGenericBackingStore`, `registerDocumentType`, `registerRegistry`,
`registerNameLookup` and `registerIndex` now return promises (VoidBot's vendor
fork, if it adopts CultLib's package); Rust callers of `add_backing_store`
should propagate the new `Result` with `?` (Epiphany `epiphany-core`,
CodexConnector); Rust `CacheBackingStore` implementations must provide
`push_all` (Epiphany's already does); Heimdall re-vendors CultLib when it moves.
Cut 6 is in progress. A new cut, **Cut 6b: CultMath grows what Aetheria
needs**, runs in parallel on CultLib branch `codex/cultmath-hlsl-parity` and
merges before Cut 7. Cuts 7-10 are not started.

Cut 6b decisions (operator, 2026-09-14). Aetheria adopts CultMath to exercise
it, but the audit showed it is not a drop-in: missing `float2x2`, `float3x3`,
`int3`, `int4`, `mul`, float3 `snoise`, vector `pow`/`sqrt`/trig overloads,
comparisons returning bool vectors, `any`/`all`, several swizzles, and any
Unity `Vector`/`Quaternion` bridge; `record struct` types made components
read-only; `hash`, `normalize` and `Random` differ. CultMath's parity target is
HLSL, not Unity.Mathematics: HLSL source should compile as C# with the right
using directive, so components and swizzles are writable (the design doc's
"prefer immutable value types" is amended). Where CultMath intentionally
differs from Unity.Mathematics it keeps its behavior, following HLSL where HLSL
defines the intrinsic; old runs are discarded, so a different galaxy per seed
costs nothing. Only what Aetheria uses is added. The Unity bridge lives in
CultMath's Unity package; the core stays engine-free. Cut 8 then swaps
Aetheria (ServerShared, `Aetheria.Shared`, `tools\AetherDb`, Unity-side
scripts) onto CultMath and deletes the `Library\PackageCache` Unity.Mathematics
source include from `Aetheria.Shared.csproj`. Line
numbers below refer to the evidence base, not to the branch.

Evidence base: CultLib `main` at `c2a9a6e`; Aetheria `codex/aetheria-state-rebuild`
at `82c1e72e`. Line numbers refer to those revisions (`CC:` is
`src\GameCult.Caching\CultCache.cs`, `DMS:` is
`src\GameCult.Caching.MessagePack\DirectoryMessagePackBackingStore.cs`, `CND:`
is `src\GameCult.Networking\CultNetDatabase.cs`). Every mechanism claim a cut
is built on was established by running code; probes are in section 7, cited
as `[P<n>]`. Consumer facts come from a sweep of `CultLib\src`, `CultLib\tests`,
and every C# repo under `F:\Projects` referencing `GameCult.Caching` (Aquarium,
Ymir, Mimir, Delvehold, AquaSynth, Gjallar, Brokkr, Eve, EveUnity, EvePlugins),
excluding `bin`, `obj`, `vendor`, `CultLib-*` worktrees and `AetheriaEve*`.

Canonical runtimes are the C# reference under `CultLib\src` and the packages
under `CultLib\packages`. `F:\Projects\cultcache-rs`, `cultcache-py`,
`cultnet-rs`, `cultnet-ts` are defunct and cited nowhere. `F:\Projects\AetheriaEve`
and its worktrees are taxidermy, not a consumer.

## 1. Answers to the open questions

### Q1. Is routing a C# behavior or a cross-runtime semantic?

**Routing is the contract in every runtime: one home store per document type.
Replication and mirrors are deleted everywhere. No bytes change.**

- TypeScript routes by type and mirrors the rest: `addBackingStore(store,
  ...types)` (`CultLib\packages\cultcache-ts\src\cult-cache.ts:180-188`),
  `#resolveRoute` returns `{primary, mirrors}` (`:663-677`), and `put`,
  `putEnvelope`, `delete` push to `primary` then every mirror (`:401-402,
  447-448, 502-503`; `StoreRoute.mirrors` at `:18`).
- Rust: `add_backing_store`/`add_generic_backing_store`
  (`CultLib\packages\cultcache-rs\src\lib.rs:1950-1963`), `resolve_route_indices`
  (`:2363-2380`), mirror loops at `:2092-2094, 2213-2215, 2252-2254,
  2323-2325`; `put_prepared_batch` already demands one route and one store
  (`:2174-2189`). Its README claims this "mirrors the C# behavior"
  (`CultLib\packages\cultcache-rs\README.md:216-241`), which is false.
- Python: `stores_by_type: dict[str, list[BackingStore]]`, `generic_stores:
  list` (`CultLib\packages\cultcache-py\src\cultcache_py\cache.py:23-24`); write loops
  at `:199-201, 218-219, 239-241, 269-270`; resolver at `:300-302`.
- C# replicates every record to every store (`CC:2097-2101, 2135-2139`) and
  pushes all entries into a newly attached store (`CC:1436-1439`). `[P2-A]`:
  attach a second store after hydrating the first, flush with no mutation, and
  the second file is written with the first store's record.
- Nobody uses a mirror: every cache in `CultLib\src`, `CultLib\tests` and every
  consumer attaches one untyped store; the only multi-store cache anywhere is
  the Rust test `type_specific_store_routes_before_generic_store`
  (`CultLib\packages\cultcache-rs\src\lib.rs:3714-3748`), whose types never share a store. No test asserts
  mirroring.
- No runtime writes store identity into the file: all write
  `[formatVersion, catalog[], records[]]`, records `[key, schemaId, storedAt,
  payload]` (C# `CultDocumentMessagePackSerialization.cs:180-251`; TS
  `CultLib\packages\cultcache-ts\src\single-file-messagepack-backing-store.ts:203-232`;
  Rust `CultLib\packages\cultcache-rs\src\lib.rs:244-280, 2401-2455`; Python
  `CultLib\packages\cultcache-py\src\cultcache_py\stores.py:137-152`). `storeId`
  (`cultcache-persistence-format.md:93`) is implemented nowhere.

Decision: a document type has exactly one home store, chosen by its most
specific registered route (C# by assignable CLR type; TS, Rust, Python by the
exact type string they already route on); a second untyped store or an
overlapping route is a registration error; a write with no home is an error.
C# implements it (Cut 3); TS, Rust and Python delete their mirror paths
(Cut 5). Each routed file is a complete single-store file (section 3).

### Q2. How do Unity.Mathematics values encode portably?

**As fixed-length positional arrays of components, Aetheria's existing wire
shape `[P5]`. CultMath defines no encoding and is not adopted.** (Superseded
2026-09-14: Aetheria adopts CultMath, the operator's decision for dogfooding
GameCult's math library. The same positional-array encoding applies to CultMath
types, and gaps found by the parity audit are filled in CultMath rather than
worked around in Aetheria. Unity.Mathematics stays licensed for tooling that
supports a Unity game under the Unity Companion License; CultLib itself uses
CultMath only.)

- Aetheria writes `float2` as `[f32,f32]`, `float3` three, `float4` four,
  `int2` `[int,int]`, `bool2` `[bool,bool]`
  (`ServerShared\CultCache\Serialization\MathFormatters.cs:8, 49, 90, 131,
  177`); the real `Faction` record's two `float3` slots are `Array(n3)` on disk
  `[P5]`. `MathResolver.cs:55` maps `int2?[]` to `ArrayFormatter<float2?>`, a
  bug fixed in Cut 8. `bool2[,]` uses MessagePack's `TwoDimensionalArrayFormatter`.
- CultMath (`CultLib\packages\cultmath`) has no serialization; no sibling
  defines a vector shape.
- The persisted member type name is the CLR full name, the same rule as
  `System.Int32` (at the evidence base, `CultGeneratedDocumentMetadata.cs:258-282`
  and the generator; since the Cut 4 generator deletion the registry's
  `CultSchemaTypeNames` alone owns it, with nested types written `Outer+Inner`).
- CultLib has no consumer formatter extension point: `Options` is a
  `static readonly` composite under `MessagePackSecurity.UntrustedData`
  (`CultDocumentMessagePackSerialization.cs:50-55`); generated serializers
  read it at call time `[P4]` (historical: the generator and its serializers
  were deleted in Cut 4; every payload now goes through `MessagePackSerializer`
  with `OptionsFor(assembly)`).
- `[P5]`: MessagePack 3's own generator (`MessagePackAnalyzer` 3.1.7, a
  dependency of `MessagePack`) fails the build (CS0426) on
  `Dictionary<CultRecordRef<T>, float>` by emitting a reference to a
  non-existent `GeneratedMessagePackResolver.GameCult.Caching.CultRecordRefFormatter<T>`;
  and `UntrustedData` refuses `CultRecordRef<T>` as a dictionary key ("No
  hash-resistant equality comparer available"). A `MessagePackSecurity`
  subclass supplying a comparer over the key string fixes the second. The
  generator reaches consumers transitively through CultLib's `MessagePack`
  reference, so the owner is CultLib: `[P6]` shows that
  `ExcludeAssets="analyzers"` on the library's `MessagePack` reference does not
  stop it, `[assembly: MessagePackKnownFormatter]` does not fix the emission,
  and a direct `<PackageReference Include="MessagePackAnalyzer" Version="3.1.7"
  PrivateAssets="all" />` in the library does stop it. CultLib composes
  `StandardResolver` explicitly and never registers a generated resolver, so
  it does not need the generator.

Decision: options are owned by the assembly that declares the documents.
Cut 4 adds `[assembly: CultCacheFormatterResolver(typeof(R))]` and
`CultDocumentMessagePackSerialization.OptionsFor(Assembly)`; generated and
reflective serializers resolve options from the document's assembly, so there
is no mutable static, no registration call, no load-order hazard. The contract
gains: **vector-like value types encode as fixed-length positional arrays of
primitive components; no ext types, no maps.** Aetheria keeps Unity.Mathematics
(26,566 lines of `ServerShared` use `Unity.Mathematics.math`) and declares its
`MathResolver` through the attribute.

Known non-portable shape, left alone: `EntityPack.Dictionary<int2,
PersistentBehaviorData[]>` (`EntitySerializer.cs:183`) lives in the Run store
no other runtime reads.

### Q3. What replaces `System.Type` fields?

**A `string` holding the `BehaviorData` subclass simple name, which the sibling
field already uses.**

- The only persisted `Type` is `StatModifierData.RequireBehavior`
  (`ServerShared\Behaviors\StatModifier.cs:22-23`), written as
  `AssemblyQualifiedName` by `TypeFormatterResolver.cs:17`, read at
  `StatModifier.cs:73, 80` as `b.GetType() == _data.RequireBehavior`.
- `StatReference.Target` (`:136-137`) has the same `[InspectableType(typeof(BehaviorData))]`,
  is a `string`, and is resolved by name at `:63`. The closed set is the
  `[Union]` list on `BehaviorData` (`Behaviors\Behaviors.cs:152-190`).

Decision: `public string RequireBehavior;` with `[Key(4)]` unchanged;
comparisons become `b.GetType().Name == _data.RequireBehavior`; the importer
writes `Type.GetType(stored)?.Name ?? ""`. `TypeFormatterResolver.cs` is
deleted. Rejected: the union tag (opaque, bound to MessagePack-C#) and any
CLR-qualified name.

### Q4. Do CultNet or CultMesh depend on replication, declared-type schema, or exact-type watches?

**No. One store per cache everywhere; no base-typed writes; every watch names
a leaf. What they do use is in the audit (2.0) and named in the authority map.**

- One store per cache: `CultCacheMessagePack.cs:119, 128`; `CultMesh.DocumentFromStore`
  (`CultMesh.cs:1588-1603`); `CultNetLocal.cs:146`; the Studio by reflection
  (`CultCacheStudioWindow.cs:688`); Aquarium (`AquariumCultStateStore.cs:24-26`);
  AquaSynth, Ymir, Mimir, Gjallar through `OpenAsync`/`Create`. Test pairs
  (`BackingStoreTests.cs:27/40, 67/76, 822/825, 856/864, 909/915, 1501/1509`;
  `CultMeshStreamingTests.cs:2402-2475, 2634/2643, 2688/2697`) are two caches
  on one file. `CultNetLocal.cs:148` reads `BackingStores`; it stays.
- Declared-type schema: `CND:875, 913` take the descriptor from
  `GetRequired<T>()`. `[P3]`: `PutAsync<Gear>(key, new Weapon())` persists the
  `Gear` schema, publishes a `Gear` schema id, keeps a `Weapon` in memory, and
  reloads as `Gear`. `PublishCacheUpdate` (`CND:1214-1215`),
  `CultNetDocumentRegistry.cs:327`, `CultNetDatabaseServer.cs:391-398`,
  `CultNetDatabaseSubscriptionServer.cs:504-513` already use `document.GetType()`.
  `CultMesh.cs:3515-3538` converts to the stored CLR type before
  `PutAsync<storedType>`. No caller passes a base type or interface.
- Exact-type watches: `CND:1134-1140` is CultNet's own exact match; callers
  name leaves (`CultMeshGameSession.cs:155`, `Server.cs:490-733`, AquaSynth,
  Brokkr). `CultMesh.Collection<T>` (`CultMesh.cs:2289-2290`) snapshots with the
  assignable `GetAll` and streams with the exact `Watch`; after Cut 3 both are
  assignable. `CultNetDatabase.Watch<T>` stays exact.
- Transactions: `CultCache.ExecuteTransactionAsync`'s only non-test caller is
  `CultNetDatabase.ExecuteTransactionAsync` (`CND:534`), whose only callers are
  tests; `RequireTransactionsForAuthoritativeWrites` is tests-only. `[P2-B]`
  shows the single-store rule. The CultNet wrapper goes in Cut 2; the cache
  primitive is replaced by the explicit batch `Commit` in Cut 3 (2.2), because
  atomic multi-record commit is a capability the operator keeps.
- Globals: nothing in `CultLib\src` or any consumer calls `GetGlobal<T>`
  (definition at `CC:1765`; `CultNetDatabase.WatchGlobal<T>` at `CND:1162`).
  AquaSynth declares `[CultGlobal]` types (`AquaSynthDaemonService.cs:295`,
  `AquaSynthCultNetDaemon.cs:20`, `CultCachePatchDocument.cs:8`) and opens
  through `Create`/`OpenAsync`, so today those get invented defaults; how it
  reads them back is determined in Cut 3's Soul step.
- The Studio reaches the cache by string reflection (types `CultCache`,
  `CultCacheOpenOptions`, `CultCacheMessagePack`, `CultRecordKey`,
  `CultRecordRef<>`; members `PullOnOpen`, `OpenAsync`, `IsDirty`, `Registry`,
  `AllDescriptors`, `GetRequired(Type)`, `AllStoredDocuments`,
  `UpsertAsync(Type, object, CultRecordKey?)`, `Remove(CultRecordKey)`,
  `FlushAsync(bool)`, and descriptor/catalog members). Cut 6 replaces the
  reflection with a compile-time reference, so nothing is frozen for it; the
  Studio package moves in lockstep in the same release (Cut 7).
- At the evidence base the GameCult generator discovered members on the
  declaring type only and had no `[Union]` handling. Cut 4 deleted it, so
  Aetheria's Unity and headless builds share the reflective registry and no
  longer depend on whether a generator runs. Re-pin follow-up for Delvehold:
  remove the generator `ProjectReference`s in `Delvehold.WorldHost.csproj:13`
  and `Delvehold.Protocol.csproj:11`, and stop `Directory.Build.props:8`
  preferring the stale `CultLib-aetheria-authority` worktree; it builds today
  only because of that preference.

### Q5. Can existing stores hold records written under a declared parent schema?

**No consumer writes one; the fix changes no existing bytes.**

- Aquarium: `AddAsync(state, new CultRecordHandle<T>(key))`, concrete `T`
  (`AquariumCultStateStore.cs:82-83, 95-96`). Ymir: four concrete `UpsertAsync`
  (`YmirServicePublicationDocument.cs:169-177`, `YmirWorldStateDocument.cs:255`).
  Delvehold, AquaSynth, Mimir, Gjallar: grepped in Cut 3's Soul step for a
  base-typed `T` (none expected; the sweep found none).
- `[P2-E]`: today the payload under the `Gear` schema id already has
  `Weapon`'s two slots. `CreateStoredDocument(typeof(T), ...)` at `CC:1632,
  1641` becomes `document.GetType()`. The new loud failure: a runtime type
  without `[CultDocument]` throws from `GetRequired` (`CC:493-498`).

### Q6. Which consumers pin which revision, and what is the release order?

| Consumer | Takes CultLib by | Pin | Affected |
|---|---|---|---|
| Aquarium | .NET sibling checkout, unpinned (`Aquarium.Epiphany.csproj:14-15`) | none | Compiles against `main` at once; `new CultCache()`, `AddAsync`, `GetByName`, `PullAllBackingStoresAsync` all kept |
| Ymir | .NET sibling, unpinned (`Ymir.Core.csproj:4-14`) | none | `OpenAsync` with `UseDirectoryStore`, `StoreFlushOnDispose`, `UpsertAsync` kept |
| AquaSynth | .NET sibling | none found | `Create`, `OpenAsync`, `[CultGlobal]` types, `UseDirectoryStore` kept; invented globals stop |
| Mimir | .NET sibling | none found | `OpenAsync`, snapshot/catalog types, `GeneratedPayloadSerializer`, `FlushAsync(soft: true)` via CultMesh kept |
| Gjallar | .NET sibling | none found | `Create`, `FlushAllBackingStores`, `FlushOnDispose`, `StoreFlushOnDispose` kept |
| Brokkr, Eve, EveUnity, EvePlugins | Unity package / sibling | various | `Watch<T>` via CultMesh, `new CultCache(registry)`, `FlushAsync(soft: true)` via CultMesh kept |
| Delvehold | .NET sibling with a revision guard | `334e60f`, 52 behind | Not until it re-pins; `Registry`, `FlushOnDispose`, generator kept |
| Heimdall | submodule, TS only | `b6b1d9c` | `CultLib\packages\cultcache-ts` mirror deletion (Cut 5) on next bump; it attaches one store |
| Idunn, Epiphany, Odin, Ghostlight, Muninn, Ratatoskr, CodexConnector | Cargo by commit | various | `CultLib\packages\cultcache-rs` mirror deletion (Cut 5) on re-pin; all use `add_generic_backing_store` once |
| Huginn, Sai, Stonks, Vili | npm sibling paths | none | as Heimdall, immediately |
| Aetheria | nothing yet | n/a | the consumer this cut serves |

Release mechanics: `unity\org.gamecult.cultlib\package.json` is at `1.0.56`
with committed DLLs; newest tag `cultlib-unity-v1.0.46` (`1fc68a4`); install
docs stale (`docs\nuget-packaging.md:22`, package README `:15`). The Studio is
`org.gamecult.caching.unity` 1.0.0 at `src\GameCult.Unity\Assets\Caching`,
untagged, with no dependency on the runtime package (hence its reflection).
`publish-packages.yml` publishes npm and PyPI on tags only.

Order: CultLib `main` (Cuts 1-6) -> Cut 7 tags `cultlib-unity-v1.0.57`,
`caching-unity-v1.1.0`, `cultcache-ts-v0.14.0`, `cultcache-py-v0.3.0` ->
Aetheria (Cuts 8-10). Aquarium, Ymir, AquaSynth, Mimir, Gjallar are built in
Cut 2's and Cut 3's Soul steps because they track `main`.

### Q7. Where do Aetheria's world types fall, and where are the save points?

Facts from the data `[P5]`: `GameData\AetherDB.msgpack` holds 167 records in
ten tags (0:13, 1:51, 2:25, 3:3, 13:12, 17:3, 29:4, 30:1, 31:18, 32:37) plus
12 name files (tag 9, three slots). The root union declares 30 tags
(`DatabaseEntry.cs:23-54`); tags 4, 5, 6, 14, 20 name classes that are not
`DatabaseEntry` subclasses; tags 8 (`GalaxyMapLayerData`) and 11 (`PlayerData`)
have no records and no live reader, and both types are deleted;
`ConsumableItemData` (`ItemData.cs:348`) and `PatrolOrbitsTask` are subclasses
with no tag. Agent tasks are not persisted (`Zone.Agents`, `Zone.cs:35`, is
absent from `ZonePack`) and this migration adds no persistence. Action-bar
bindings index into the run's entity (`SavedGame.cs:111-118`), so they are Run
state.

| Type | Store | Kind | Why |
|---|---|---|---|
| ItemData tree: SimpleCommodityData, CompoundCommodityData, GearData, HullData, CargoBayData, DockingBayData, WeaponItemData, ConsumableItemData | Catalog | document | `ItemInstance.Data`, `FactionProductData.Design`, `Faction.BossHull`, `WeaponData.AmmoType` |
| Faction | Catalog | document | `ItemData.Manufacturer`, `EntityPack.Faction`, `SavedGame.Factions`, `Faction.Allegiance` keys |
| FactionProductData | Catalog | document | `ItemInstance.Product` (`ItemInstance.cs:49`) |
| PersonalityAttribute | Catalog | document | keys in `Faction.Personality`, `CompoundCommodityData.DemandProfile` |
| NameFile | Catalog | document | `Faction.GeonameFile` (`Corporations.cs:40`), `Galaxy.cs:338` |
| BehaviorData tree, WeaponData, ItemRole | inside item design | value | `EquippableItemData.Behaviors` (`ItemData.cs:351`) |
| OrbitData | Run | document | `BodyData.Orbit`, `OrbitData.Parent`, `OrbitalEntityPack.Orbit`, `MoveTo.Orbit`; created at runtime (`Zone.cs:120, 265`) |
| BodyData tree: PlanetData, GasGiantData, SunData, AsteroidBeltData | Run | document | `Mining.Asteroids`, `Survey.Planets`, `MiningToolData.AsteroidBelt`, `Zone.Planets` keys |
| SavedZone (with ZonePack) | Run | document | one per zone; `List<CultRecordRef<OrbitData>>`, `List<CultRecordRef<BodyData>>` |
| SavedGame | Run | `[CultGlobal]` | run root: factions, home/boss zones, current zone, action-bar bindings |
| EntityPack tree, ItemInstance tree, PersistentBehaviorData, SavedActionBarBinding | inside SavedZone / SavedGame | value | no identity |
| AgentTask tree | not persisted | value owned by `Agent` | not saved today |
| Entity, Ship, OrbitalEntity, Zone, Galaxy | live simulation | not records | tags 14 and 20 deleted |
| PlayerSettings | Player | `[CultGlobal]` | name, tutorial flag, credits |
| InputLayout | Player | document keyed by layout name | today one file per layout under `GameData\KeyboardLayouts` |

Save points today (`Gameplay\ActionGameManager.cs` unless noted):
`SavePlayerSettings` (`:76-79`) writes `GameData\PlayerSettings.msgpack`
embedding the run; `SaveState` (`:240-249`) via `Zone.PackZone()`
(`Zone.cs:107-118`). Triggers: quit (`:238`), wormhole entry (`:611`), death
(`Die`, `:1057-1065`, leaves `SavedRun` on disk), settings Back
(`UI\MainMenu.cs:237`), new game (`MainMenu.cs:139, 168`). Dead: `SaveLoadout`
(`:233-236`; `:272-275`) and `SaveZone` (`:1093-1094`). Rebinding writes
`GameData\KeyboardLayouts\*.msgpack` (`UI\InputScreen\InputDisplayLayout.cs:495-501`).
Loads: `ActionGameManager.cs:49-57`, `:65-72`; resume `MainMenu.cs:97-106`
through `Galaxy(CultCache, SavedGame, ...)` (`Galaxy.cs:47-90`); layouts
`InputDisplayLayout.cs:88-91`.

After Cut 10: `SaveRun()` flushes the cache (only dirty stores write); `Die()`
disposes and deletes the run store; new game deletes any run store and creates
a fresh one.

### Q8. How do identity comparisons survive without `DatabaseEntry.ID`?

**Documents are singleton instances per cache and identity is the record key;
reference equality replaces ID equality, keys replace stored Guids.**

- The only `Equals`/`GetHashCode` override is `DatabaseEntry.cs:60-69`; it
  goes with the class.
- One instance per key (`CC:1274`, `:1710-1719`), so the seven dictionaries
  keyed by `Faction`/`OrbitData` (`Galaxy.cs:18, 19, 28, 34`; `ZoneGenerator.cs:97,
  243`; `UI\Menu\SectorMap.cs:51`) work with the default comparer; saves embed
  references, not copies.
- The 14 `.ID ==` comparisons (`Entity.cs:291, 304, 319`; `LoadoutGenerator.cs:159,
  161`; `Narrative\ZoneConstraints.cs:37, 52`; `Zone.cs:316`;
  `UI\Menu\SectorRenderer.cs:65`; `TradeMenu.cs:292, 300`;
  `TradeMenuDebug.cs:275, 283`) become reference or `CultRecordRef.Key`
  comparisons.
- Guid-keyed runtime dictionaries (`Zone.cs:59-77, 122, 265`;
  `Zone Display\ZoneRenderer.cs:342-413`; `ItemsOfType` `Entity.cs:331, 334,
  1332, 1489-1589`; `TradeMenu*.cs`) key by `CultRecordKey`. The two
  `DatabaseLink<T>` fields (`ItemInstance.cs:26`, `SavedGame.cs:118`) and every
  raw `Guid` reference become `CultRecordRef<T>`; `Dictionary<Guid, float>`
  becomes `Dictionary<CultRecordRef<T>, float>` (string-keyed map on the wire,
  deserializable under Cut 4's security `[P5]`).
- Saves storing faction Guids (`SavedGame.cs:56-73`, `EntitySerializer.cs:43`)
  store `CultRecordRef<Faction>`.
- The key of a document in hand comes from `CultCache.TryGetHandle` (`CC:1699`,
  kept: CultNet uses it). The four `DatabaseLinkBase.Cache` writers go with the
  static; `ItemManager.GetData` (`ItemManager.cs:62-75`) is the single
  resolution path. `ZoneGenerator.cs:164` uses the first eight characters of
  the key.

Catalog keys carry the legacy Guid in `D` format (slot 0 is a 16-byte `bin`
`[P5]`). Run and Player keys are cache-minted (`N`, `CC:2200`).

## 2. CultLib authority map

### 2.0 Subtraction audit and target shape

Measured: `GameCult.Caching` + `GameCult.Caching.MessagePack` are 5,259 lines,
870 of them `///` lines restating member names (`CC` 461 of 2,704;
`CultDocumentContracts.cs` 87/192; `CultGeneratedDocumentMetadata.cs` 93/308;
`CultManagedDocument.cs` 78/415; `CultCacheMessagePack.cs` 43/134;
`CultDocumentMessagePackSerialization.cs` 69/475; `DMS` 30/973).
`GetAwaiter().GetResult()` appears at `CC:1432, 1433, 2184`: the store
subscriptions and the global materializer, all on the deletion line.
`GenerateDocumentationFile` is on (`src\Directory.Build.props:6`) with no
`NoWarn`, so the comment removal adds `<NoWarn>$(NoWarn);CS1591</NoWarn>` to
the two Caching projects.

| Surface | Consumers | Verdict |
|---|---|---|
| `MaterializeMissingGlobals`, `CultCache(registry, bool)` | `CultCacheMessagePack.cs:105` only | delete (2.1) |
| `CultCacheOpenOptions.ConfigureCache`, `ConfigureStore`, `DirectoryStorePath`, `ConfigureDirectoryStore`, `DirectoryStoreHydrationFilter` | none (`DirectoryStoreHydrationFilter` tests only) | delete |
| `FlushBackingStore(store)`, `PrepareForReloadOrShutdown[Async]` | none | delete; `FlushAsync` writes every dirty store |
| `TryGet<` | AquaSynth (`CultCachePatchDocument.cs:104`), missed by the first audit | keep |
| `TryGetByName`, `TryGetByIndex`, `Resolve<` | none | delete |
| `CultCache.Logger`, store `Logger`, `NullLogger` use | none | delete |
| store `FlushOnDispose` | none directly; `CultCacheOpenOptions.StoreFlushOnDispose` (Gjallar, Ymir, Delvehold) sets it | keep both; collapsing the two flush-on-dispose flags is a follow-up |
| store `HydrationFilter`, `PullSelected`, `PullBackingStoreRecordsAsync`, `CultPersistedRecordMetadata` | tests only | delete |
| `soft` flag on `FlushAsync`/`FlushAllBackingStores`/`PushAll`/`CommitBatch` | no store reads it (`CC:2638`, `DMS:360`); Mimir (`EveDashboard:900`, `CultMeshMedia:422`) and Brokkr (`BrokkrCultMeshMirror.cs:68-124`) pass `soft: true` through `CultMesh.FlushAsync`/`CultNetLocal.FlushAsync` | delete from Caching; `CultMesh.FlushAsync(bool)` and `CultNetLocal.FlushAsync(bool)` keep their parameter and stop forwarding it. Removing it there and at the four external call sites is a named follow-up |
| SoA: `Soa<T>`, `CultSoaTable`, `CultSoaColumn`, `CultCacheSoaStore`, `CultCacheSoaTypeTable`, `CultCacheSoaMember` (`CultManagedDocument.cs:136-414`), `_soa` hooks in `CC` | tests only | **park**, not delete: built for a stated reason (ECS-style structure-of-arrays columns over cached documents, performance without compromising document ergonomics). Cut 2 step 1 tags the last commit holding it as `parked/cultcache-soa` before removal, adds `docs\parked-features.md`, and amends `docs\runtime-parity-scope.md:23-27, 33` to say the SoA table is parked at that tag |
| `Document<T>`, `CultManagedDocument<T>`, `CultNetDatabase.Document<T>` | tests only | removed from the live cache; `CultManagedDocument<T>` is parked with SoA at `parked/cultcache-soa` (`docs\parked-features.md`, CultLib `1982d73`) |
| Transactions: `ExecuteTransactionAsync` (both), `CultCacheTransaction`, `CommitTransaction`, `VisibleStoredDocuments`, `_ambientTransaction`, `_transactionGate`, `CacheBackingStore.CommitBatch` (+ `DMS:308-351`), `CultNetDatabase.ExecuteTransactionAsync` (`CND:519-555`), `RequireTransactionsForAuthoritativeWrites` (`CND:305`), `EnsureAuthoritativeTransaction` (`CND:1791-1796`), `AfterCommit` (`CND:898-901`) | tests only in C#. Siblings: TS has no batch; Python has `put_envelopes` (`CultLib\packages\cultcache-py\src\cultcache_py\cache.py:227-251`, one document type per call, `push_all`, no rollback) used by `CultLib\packages\cultnet-py\src\cultnet_py\replication.py:98`; Rust has a cache-level `put_prepared_batch` (`CultLib\packages\cultcache-rs\src\lib.rs:2162-2201`: one store per batch, all-or-nothing `push_all`) with no caller, and a **store-level** batch family (`compare_and_swap_batch`, `compare_exchange`, `compare_exchange_snapshot`, `delete_batch_if_unchanged`, `CultLib\packages\cultcache-rs\src\lib.rs:397-800`, `8f29ee5`) consumed by Odin (12 sites), Idunn (31), CodexConnector (1) and Ghostlight (`ghostlight-dungeon\src\app_session.rs:119, 371`, plus tests): every one passes an explicit batch value; none relies on ambient in-flight visibility. Operator: atomic multi-record commit is a desired capability | **replace, not delete**: the ambient `AsyncLocal` overlay, the `SemaphoreSlim` gate, `CultCacheTransaction`, `CommitTransaction` and `VisibleStoredDocuments` (~230 lines) go in Cut 3 in the same commit that lands `Commit(Action<CultCacheBatch>)` (2.2); `CacheBackingStore.CommitBatch` and `DMS:308-351` stay as the store side. `CultNetDatabase.ExecuteTransactionAsync`, `RequireTransactionsForAuthoritativeWrites`, `EnsureAuthoritativeTransaction`, `AfterCommit` are deleted (tests only; CultNet callers use `PutAsync`). `cultcache-persistence-format.md:42-55` is amended to the explicit-batch shape, not retracted |
| Directory store legacy formats `cultcache.store.v2.directory-indexed`, `v3.directory-immutable-pages`, v1 inline records: `LoadLegacyRecords`, `_legacyInlineRecords`, `LegacyRecordPath`, `MetadataRecordPath`, `_needsIndexUpgrade`, `_manifestUsesMetadataPages` | no store on disk anywhere scanned uses them; only v4 is written | delete; a v2/v3 manifest is refused with its format string |
| Directory store `ReadStageProbe`, `FlushStageProbe`, stage constants, `ReadPersistedGeneration` | tests only | delete, with the tests that inject through them |
| Directory store itself, `AcquireCommitLease`, `UseDirectoryStore`, `DefaultRecordDirectoryPath` | Ymir (`YmirWorldStateDocument.cs:248, 267`, `YmirServicePublicationDocument.cs:161`), AquaSynth (`IpaTrialResults.cs:190`) | keep (v4 read/write and the lease) |
| Schema migration: `LastSchemaMigrationReports`, `CultSchemaMigrationReport/Warning/Kind`, `ResolvePersistedSchemaReport` | tests only | keep: the contract (`cultcache-schema-compatibility.md:40-45`) promises the typed report and soft migration must not be silent |
| `GetStoredDocuments<` | tests only | delete (fold into `GetAll<T>`) |
| `GetGlobal<T>` | tests only (`BackingStoreTests.cs:342, 358, 365`); Aetheria uses it in Cut 10 | keep |
| `FlushAttachedStoresOnDispose` | via `CultCacheOpenOptions.FlushOnDispose` (Gjallar, Ymir, Delvehold) | keep |
| `LastSuccessfulFlushAtUtc` (cache and store) | tests only | delete |
| store `EntryAdded/Updated/Deleted` subjects | cache only | replace with one `internal` callback set at attach; the three `Subject`s go |
| `CultCacheMessagePack.Create` | AquaSynth (`AquaSynthDaemonService.cs:923`, `CultCachePatchDocument.cs:90`), Gjallar (`VerseState.cs:35`) | keep as the synchronous open; `OpenAsync` (Mimir, AquaSynth, Ymir, CultNetLocal, Studio) returns `Task.FromResult(Create(...))` |
| `CultCacheOpenOptions.PullOnOpen` | Studio by reflection (`:687`); external setters found in Cut 2: AquaSynth (`CultCachePatchDocument.cs:92` and `AquaSynthDaemonService.cs:923` set `false`; `IpaTrialResults.cs:189`, `SpeechDistributedTraining.cs:262` pass `File.Exists`-style values; a test sets `true`) and Mimir (`BufferSmoke\Program.cs:1943, 3691, 3990, 4066, 7622`) | kept through Cut 2. `false` means "write this file without loading it", which attach-is-hydration cannot express: Cut 3 must decide how a consumer overwrites a store (open Cut 3 item, 2.2) and migrate these call sites or keep an explicit overwrite option |
| `OnUpdate` | `CND:494, 1202` | keep |
| `Watch<T>`, `WatchRecord<T>` | `CultMesh.cs:2290, 2317, 2348, 1527` | keep; assignable |
| `TryGetHandle`, `AllEntries`, `AllStoredDocuments`, `BackingStores`, `GetByIndex<`, `Get(key)`, `Remove(CultRecordKey)`, `Remove<T>`, `UpsertAsync<T>`, `UpsertAsync(Type,...)`, `AddAsync<T>`, `GetAll<`, `Get<`, `GetByName<`, `IsDirty`, `Dispose`, `FlushAllBackingStores`, `FlushAsync`, `PullAllBackingStoresAsync` | CultNet/CultMesh, Aquarium, Gjallar, Ymir, Studio | keep |
| `CultDocumentRegistry.Shared/ForTypes/GetRequired/GetRequiredBySchemaId/AllDescriptors`, descriptor `SchemaId/SchemaName/ToCatalogEntry/GeneratedPayloadSerializer`, `CultRecordHandle<`, `[CultName]`, `[CultGlobal]`, `[CultIndex]` | Mimir, Delvehold, AquaSynth, CultNet | keep |
| `SerializeSnapshot/DeserializeSnapshot`, `CultPersistedStoreSnapshot/Record`, `CultSchemaCatalogEntry`, `SerializeUntyped/DeserializeUntyped`, `Serialize</Deserialize<`, `SerializePersistedRecord/DeserializePersistedRecord` (`DMS:112, 415, 522, 869`) | CultMesh, Mimir, directory store | keep |
| `SerializeSchemaCatalog/DeserializeSchemaCatalog` | tests only | delete |
| Generated metadata provider types, `GameCult.Caching.MessagePack.Analyzers` (empty `Class1.cs` packaging host) | generator output; the host spreads the analyzer to every MessagePack consumer (Networking, Delvehold, two test projects) | keep this cut; packaging the analyzer without an empty project is a follow-up |
| `///` lines restating names | none | delete in every file a cut touches (`CC`, `CultManagedDocument.cs`, `CultCacheMessagePack.cs`, `CultDocumentMessagePackSerialization.cs`, `DMS`); the two contracts files are the documentation |

**Target shape after Cuts 2-4** (the original `Aetheria\...\CultCache.cs` is
437 lines for a cache, routing, an inheritance-aware index and six stores;
this is what the cache half of `CultCache.cs` should read like; the "about 900
lines including the registry" figure was wrong: the registry and schema half
alone is 1,057 lines and is not reshaped by this migration (Cut 3 measured the
cache half at 1,025 lines after adding routing and commit). Original sketch,
at about 900 lines including the
registry):

```csharp
public sealed class CultCache : IDisposable
{
    readonly CultDocumentRegistry _registry;
    readonly List<(CacheBackingStore Store, Type[] Homes)> _stores = new();
    readonly Dictionary<string, CultStoredDocument> _entries = new(StringComparer.Ordinal);
    readonly Dictionary<Type, Dictionary<string, string>> _names = new();
    readonly Dictionary<(Type, string), Dictionary<string, string>> _indexes = new();
    readonly Dictionary<Type, string> _globals = new();
    readonly ConditionalWeakTable<object, CultRecordKeyBox> _handles = new();
    readonly Subject<Change> _changes = new();   // Change: (Kind, Key, Descriptor, Document, Previous)
    readonly object _gate = new();
    bool _dirtyInMemory;                          // only meaningful with zero stores

    public CultCache(CultDocumentRegistry? registry = null)
    public void AddBackingStore(CacheBackingStore store, params Type[] homes)   // attach = hydrate
    public Task PullAllBackingStoresAsync()                                      // re-pull
    public void FlushAllBackingStores() / public Task FlushAsync()               // every dirty writable store
    public Task<CultRecordHandle<T>> AddAsync<T>(T doc, CultRecordHandle<T>? handle = null)   // = UpsertAsync
    public Task<CultRecordHandle<T>> UpsertAsync<T>(T doc, CultRecordHandle<T>? handle = null)
    public Task<CultRecordKey> UpsertAsync(Type type, object doc, CultRecordKey? key = null)
    public object? Get(CultRecordKey key); T? Get<T>(CultRecordKey key); IEnumerable<T> GetAll<T>()
    public T? GetGlobal<T>(); T? GetByName<T>(string name); T? GetByIndex<T>(string alias, string value)
    public bool Remove(CultRecordKey key); void Remove<T>(CultRecordHandle<T> handle)
    public Observable<CultCacheDocumentChange<T>> Watch<T>(); WatchRecord<T>(CultRecordKey key)
    public CultRecordHandle<T>? TryGetHandle<T>(T doc)
    public bool IsDirty; IReadOnlyList<CacheBackingStore> BackingStores; CultDocumentRegistry Registry
    public IEnumerable<object> AllEntries; IEnumerable<CultStoredDocument> AllStoredDocuments
    public bool FlushAttachedStoresOnDispose; event Action<object?, object?>? OnUpdate
    public void Commit(Action<CultCacheBatch> stage)   // atomic multi-record commit on one home store
    public void Dispose()

    CacheBackingStore? Home(Type type)            // most specific routed store, else the untyped one, else null
    void Admit(CultStoredDocument stored, CacheBackingStore? source, bool durable)   // every add: write, batch, or load
    void Evict(CultStoredDocument stored, CacheBackingStore? source, bool durable)   // every remove
}

public sealed class CultCacheBatch                 // an explicit value; nothing is visible until Commit returns
{
    public CultRecordHandle<T> Upsert<T>(T document, CultRecordHandle<T>? handle = null)
    public CultRecordKey Upsert(Type type, object document, CultRecordKey? key = null)
    public void Remove(CultRecordKey key)
    public void Expect(CultRecordKey key, object? current)   // current = the instance this cache holds at key, or null = must be absent
    public void ExpectUnchanged()                              // the home store's persisted content equals what this cache last loaded
}
public enum CultCommitOutcome { Committed, Mismatch, Contended }
// on CultCache:
public bool Commit(Action<CultCacheBatch> stage)                 // false = a condition failed: no write, no memory change, no notification
public CultCommitOutcome TryCommit(Action<CultCacheBatch> stage) // one non-blocking lock attempt; otherwise identical

public abstract class CacheBackingStore : IDisposable
{
    protected CacheBackingStore(bool readOnly = false)
    public bool IsReadOnly { get; }  public bool IsDirty { get; protected set; }  public bool FlushOnDispose { get; set; }
    public IReadOnlyList<CultSchemaMigrationReport> LastSchemaMigrationReports
    internal Action<CultStoredDocument> Loaded, Unloaded;   // set by the cache at attach
    public abstract void PullAll(); public abstract void Push(CultStoredDocument e); public abstract void Delete(CultStoredDocument e); public abstract void PushAll();
    public abstract CultCommitOutcome CommitBatch(CultCommitRequest request, bool wait);   // one durable step under the store's lock
}
public sealed class CultCommitRequest   // built by Commit from the batch; identities are (schemaId, storedAt) captured at stage time
{
    IReadOnlyList<CultStoredDocument> Upserts; IReadOnlyList<CultStoredDocument> Deletes;
    IReadOnlyList<(CultRecordKey Key, string? SchemaId, string? StoredAt)> Expected;   // null pair = must be absent
    bool ExpectUnchanged;
}
```

`Task`-returning members stay `Task`-returning because CultNet, Aquarium, Ymir
and the Studio await them, but nothing inside awaits: they return
`Task.FromResult`/`Task.CompletedTask` and there is no `async` keyword in
`CultCache.cs`. The `SemaphoreSlim`, the `AsyncLocal`, and the `_stateGate`
lock collapse into `_gate`; `Commit` takes a synchronous `Action` because no
consumer awaits inside a batch.

### 2.1 Global documents

**What the legacy cache did.** `Aetheria\...\CultCache.cs:44-53` instantiates
every `DatabaseEntry` subclass carrying `[GlobalSettings]` in the constructor.
`GlobalSettingsAttribute` (`Attributes.cs:4`) is applied to no type (grep of
`Assets\Scripts` and `tools` finds only the declaration and the editor's global
list at `DatabaseView.cs:118, 164`). The loop runs over an empty set. Aetheria
has never had a persisted global; the mechanism hid that nobody owned when one
would exist.

**What CultLib does.** `new CultCache()` and `Create` instantiate a default for
every `[CultGlobal]` type (`CC:1300-1316, 2155-2186`); `OpenAsync` does it
after the pull (`CultCacheMessagePack.cs:90`). `[P1]`: with a `[CultGlobal]`
type registered, `new CultCache()` then attach then pull then flush **destroys
the file's existing records** (attach pushes the invented global, the dirty
single-file store skips its pull at `CC:2572-2573`, the flush writes only the
invention); the `OpenAsync` order leaves the file byte-identical. No consumer
reads a global back (Q4).

**Inventory after the cut.**

| Global | Store | Read by | Existence owned by | Missing at main menu | Missing at resume | Missing at new game |
|---|---|---|---|---|---|---|
| `PlayerSettings` | Player | `MainMenu` (name, tutorial flag), `ActionGameManager` (credits) | boot: `ActionGameManager.Awake` after `AetheriaStores.Open`, when `GetGlobal<PlayerSettings>()` is null, upserts one and flushes (Cut 10) | cannot happen after boot | same | same |
| `SavedGame` | Run | `MainMenu` (resume), `Galaxy(CultCache, SavedGame, ...)` | new game: `ActionGameManager.BeginRun()` creates the run store and writes it; `Die()` deletes the run store (Cut 10) | no run file: Resume hidden; run file without `SavedGame`: refuse to resume, log the key, New Game only | as main menu | new game deletes any run store first |
| catalog globals | Catalog | none exist | authored data; catalog is read-only | `AetheriaStores.Open` throws naming every `[CultGlobal]` type routed to the catalog with no record | same | same |

**CultLib's role.** `[CultGlobal]` means exactly: at most one record of the
type per cache, `GetGlobal<T>()` returns it or `null`, and a write without a
handle keys it `global:{SchemaId}` (`ResolveKey`, `CC:2195-2198`, kept). The
cache never creates one. Siblings already do this: TS enforces a single
`__global__` record on `put` and pull (`CultLib\packages\cultcache-ts\src\cult-cache.ts:394-399,
691-713`) and invents nothing; Python likewise
(`CultLib\packages\cultcache-py\src\cultcache_py\cache.py:125-128, 186-187`); Rust has no
global concept. The C# key `global:{schemaId}` versus the siblings' `__global__`
is an existing divergence, recorded, not changed (it would change bytes).

- Owner: the consumer code path that begins the global's lifecycle, per
  global above.
- Inputs: the hydrated cache (`GetGlobal<T>() == null`).
- Outputs: one `UpsertAsync` on the home store.
- Derived: `GetGlobal<T>` on a cache with no stores and no upserts is `null`.
- Forbidden writers (deleted): `InitializeGlobals` (`CC:2155-2186`),
  `MaterializeMissingGlobals` (`CC:1313-1316`), the two-argument constructor
  (`CC:1300-1307`), the `initializeGlobals` plumbing in `CultCacheMessagePack.cs:75,
  84, 90, 98, 105`, `ContainsDurableRecord` (`CC:2413-2416`, `DMS:277-281`).
- Singleton enforcement (new, matching TS/Python): `Admit` throws for a
  `[CultGlobal]` descriptor whose `_globals` entry holds a different key,
  before any store is touched, on writes and on loads.
- Migration: none needed functionally (no reader). Tests
  `BackingStoreTests.cs:342, 358, 365` create the global with `UpsertAsync`
  first. AquaSynth's `[CultGlobal]` types stop receiving invented defaults;
  Cut 3's Soul step reads how AquaSynth obtains them and reports.
- Invariants: loading never writes; no state exists that its owner did not
  create.

### 2.2 Store routing, attachment, dirtiness, read-only stores

**Owner.** `_stores` and `Home(Type)`: among routed entries with a `Homes`
type assignable from the document type, the one whose type is most derived;
else the untyped store; else `null`, which is an error for every write once
any store is attached (zero stores is an in-memory cache).

**Attachment is hydration.** `AddBackingStore(store, params Type[] homes)`
validates (`IsDirty` false; no second untyped store; no `homes` type already
claimed by exact equality; **no admitted record would change home**), sets
`store.Loaded = e => Admit(e, store)` and `store.Unloaded = e => Evict(e, store)`,
appends to `_stores`, and calls `store.PullAll()`. There is no interval in
which a store is attached but unread. `PullAllBackingStoresAsync` remains as
re-pull (`CultMesh.DocumentFromStore` polls at `CultMesh.cs:1603`).

**Overwriting a store (decided 2026-09-13, operator: option A).** The consumer
owns replacing a store's contents: open (which hydrates), `Remove` every
record it does not intend to keep, upsert, flush. The flush writes the whole
snapshot through the atomic replace, so the old contents are replaced in one
step; no CultLib API is added. `PullOnOpen` is deleted in Cut 3. Call sites:
`AquaSynth\src\AquaSynth.Core\CultCachePatchDocument.cs:90-98`
(`WriteDefaultAsync`) and `AquaSynth\src\AquaSynth.Faust\AquaSynthDaemonService.cs:923`
(`WriteReceiptAsync`) both write a single-document file and adopt the remove-all
form; today `Create` never pulls, so their `PullOnOpen = false` was already a
no-op. The `File.Exists`-style setters (`IpaTrialResults.cs:175-189`,
`SpeechDistributedTraining.cs:262`, Mimir `BufferSmoke\Program.cs:1943, 3691,
3990, 4066, 7622`) and the AquaSynth test's `PullOnOpen = true` just drop the
property, since hydrating a missing file is empty.

**A record's home cannot change after admission.** Because `Home` is computed
from the stores attached so far, attaching an untyped store first and a typed
store later would move the home of every admitted record of the typed store's
types, leaving a stale copy in the untyped file and a refusal on the next
reload. So the last validation in `AddBackingStore` is: for every distinct
`Descriptor.DocumentType` in `_entries`, `Home(type)` computed with the
candidate included must equal `Home(type)` computed without it; otherwise
throw `InvalidOperationException("Attaching {store} would move {schema} from
{oldHome} to {newHome}; attach routed stores before the untyped store.")`. This
is one loop at attach time and no new surface; the consequence is the
attach-order rule callers already want: routed stores first, the untyped store
(if any) last. `AetheriaStores.Open` attaches three routed stores and no
untyped store, so the check never fires there; `OpenAsync` attaches exactly
one untyped store to an empty cache.

**Inputs.** `document.GetType()` (always the runtime type); `_stores`; each
store's `IsReadOnly`.

**Outputs.** Exactly one `store.Push` or `store.Delete` per mutation, on the
home store, before `_entries` changes.

**Derived state.**
- `IsDirty` is `_stores.Any(s => s.Store.IsDirty)` when stores exist, else
  `_dirtyInMemory`. `RecomputeDirtyState` (`CC:2279-2284`) and the three
  `_hasUnflushedMutations = _backingStores.Any(...)` lines (`CC:1459, 1484,
  1991`) go.
- A store's `IsDirty` derives from its own `Push`/`Delete`; `PullAll` sets it
  false (`CC:2614`; `DMS:224`). `[P2-A]` shows loading writing today.
- `_globals`, `_names`, `_indexes` stay keyed by concrete type; `GetGlobal<T>`,
  `GetByName<T>`, `GetByIndex<T>` enumerate keys assignable to `T` (0 ->
  `null`; 1 -> `Get`; >1 -> throw naming the candidates). `[P2-D]` shows all
  three exact today.
- `Watch<T>()` is `_changes.Where(c => typeof(T).IsAssignableFrom(c.Descriptor.DocumentType)).Select(c => new CultCacheDocumentChange<T>(c.Kind, c.Key, (T?)c.Document, (T?)c.Previous))`;
  `PublishChange`'s `Activator.CreateInstance` (`CC:2286-2301`) goes. `[P2-D]`
  shows `Watch<Base>` receives nothing today.

**Concurrency and commit semantics (Soul, Cut 3; operator decisions 2026-09-13, all option A).**
- Lock order is the cache gate, then the store lock, on every path: attach,
  pull, write, flush, commit. A store's I/O blocks that cache's readers; no
  finer-grained locking. (Cut 3 as first landed took them in both orders and
  deadlocked a directory-store pull against an upsert, and a re-pull could erase
  a staged single-file write.)
- An unconditional `Commit` behaves exactly like a flush of its home store, and
  a `Commit` also persists earlier staged writes to that store and clears
  dirty. Per store type (operator decision 2026-09-13, option A): a single-file
  store writes the cache's snapshot, last-writer-wins; a directory store lands
  its changed pages onto the current manifest under the commit lease, so other
  writers' records survive (the merge its page layout and lease were built
  for, asserted by `DirectoryMessagePackBackingStore_ConcurrentInstances_MergeUnderOneCommitLease`
  and relied on by Ymir and AquaSynth). Only conditional commits (`Expect`,
  `ExpectUnchanged`) protect against a concurrent change to the same records.
- An attached store uses its cache's gate as its own lock
  (`CacheBackingStore.Gate`, Cut 3 fix `5aad137`), so cache and store locks
  cannot be taken out of order. The cache also takes the gate around each
  store's pull, which covers third-party stores that do not lock it themselves.
- Observers never run under the gate (Soul, Cut 3 second pass): `Watch`
  observers and `OnUpdate` are published after the outermost gate exit on
  every path, because a subscriber that blocks on another thread which reads
  the cache would otherwise deadlock. A store adopts its loaded view before
  anything is published, so a throwing handler cannot leave the store holding
  records the cache dropped.
- Each call publishes only the changes it admitted, on its own thread, after
  the gate is released and before it returns (CultLib `544c087`). Only
  `OnUpdate` exceptions reach the caller; `Watch` subscriber exceptions follow
  R3's unhandled-exception handling.
- **Order is data, not scheduling (operator decision 2026-09-13).** The cache
  does not schedule delivery. Every admitted change gets the next value of a
  per-cache, in-memory sequence under the gate that admits it, and each
  published change carries it (`Sequence`). Cross-thread delivery order is not
  guaranteed; a consumer that keeps a latest value drops a change whose
  sequence is lower than the one it applied (`CultMesh.WatchRecord` into
  `ApplyCanonicalSnapshot`). Rejected: save-order delivery with tickets and
  turn waits (CultLib `bcae483`, deleted), which made the cache a delivery
  scheduler, deadlocked against `CultNetDatabaseSubscriptionServer`'s
  `_lifecycleGate`, and could stall a cache on an interrupted wait; and a
  non-blocking delivery queue, which kept the same authority in the cache.
  Cross-process order stays with `StoredAt` and conditional commit.
- Latest-value consumers subscribe first, then take the cache's one sequenced
  read (document and current sequence under the gate) and ignore any change at
  or below that sequence (operator decision 2026-09-13; Soul found CultMesh
  mirrors' snapshot reads carried no sequence). `OnUpdate` and the streams
  CultNet derives from it carry no sequence and are not order-protected;
  schema-alias handles and removals are not stale-protected. Both are
  documented, not fixed here.
- Follow-ups outside this migration (found while landing the sequence, CultLib
  `4562340`; none introduced by it): `CultNetDatabaseSubscriptionServer.ApplyProjectedChange`
  keeps a latest value per record from `CultNetDatabaseChange`, which carries no
  sequence (it comes from `OnUpdate`, whose `(previous, current)` signature
  cannot, or from database publishes after the cache call), so a stale change
  can overwrite a newer one; fixing it means publishing CultNet changes from
  `cache.Watch` with `Sequence`. The same holds for `CultNetDatabase.WatchRecord`
  and Mesh handles over a database. `RefreshAsync` on the Mesh mirrors writes
  its value without a sequence, and `ObserveAsync` reads before subscribing, so
  a change between the two is missed.
- One key lives in one store: a write or load that would put a key already held
  from another store throws and changes nothing.
- `OnUpdate` fires for loads only, as before the migration; writers publish
  their own changes.
- A missing single-file store keeps staged writes dirty and does not drop loaded
  records.
- A store that fails to hydrate on open (corrupt, or an unregistered schema)
  throws and is never overwritten; consumers such as Gjallar and AquaSynth's
  writers surface the error rather than deleting and rewriting.
- `LateRouteOverAdmittedTypeThrows`: the reverse-order fixture holds only
  unrouted types in the untyped store, since a routed type there is a foreign
  record.

**Read-only stores.** `CacheBackingStore(bool readOnly = false)`,
`IsReadOnly`; `SingleFileMessagePackBackingStore(string filePath, bool
readOnly = false)`; `DirectoryMessagePackBackingStore(string manifestPath,
string? recordDirectory = null, bool readOnly = false)`;
`CultCacheOpenOptions.ReadOnly`. `Push`, `Delete`, `PushAll` on a read-only
store throw `InvalidOperationException("Backing store {path} is read-only.")`;
`FlushAllBackingStores` skips them.

**Admit and Evict (the shared path).**
```
Admit(stored, source, durable):
    home = Home(stored.Descriptor.DocumentType)
    if source != null and home != source: throw "{schema} record {key} was loaded from {source} but its home is {home}"
    if source == null:
        if _stores.Count > 0 and home == null: throw "no home store for {schema}"
        if home?.IsReadOnly: throw
    if descriptor.IsGlobal and _globals has a different key: throw
    if source == null and !durable: home?.Push(stored)         // store first; a batch has already committed
    lock _gate: replace entry, reindex, _dirtyInMemory |= (_stores.Count == 0 and source == null)
    publish Change; OnUpdate
```
`Evict` mirrors it with `Delete`. `AddAsync`, `UpsertAsync<T>`,
`UpsertAsync(Type, object)`, `Remove` are the one-record case (`durable:
false`); the load path is the store's `Loaded`/`Unloaded` callbacks (`source`
set); a committed batch admits each record with `durable: true`. `[P2-C]`
shows the current order leaves a document in memory after the store refused
it.

**Atomic commit.** `Commit(Action<CultCacheBatch> stage)`:
```
Commit(stage):
    batch = new CultCacheBatch(_registry, _handles)     // Upsert builds CultStoredDocuments now; Remove records keys
    stage(batch)
    homes = distinct Home(type) over batch.Upserts and the existing entries for batch.Removes
    if homes.Count > 1: throw "batch spans {a} and {b}; a commit lands in one home store"   // nothing touched
    validate every upsert as Admit does (home present, not read-only, global singleton)
    home?.CommitBatch(upserts, deletes)                  // one durable step, restores its staging on failure
    lock _gate: Admit(each upsert, null, durable: true); Evict(each delete, null, durable: true)
    publish one Change per record, after the store accepted
```
**Conditional commit (compare-exchange).** The seven Rust variants
(`compare_and_swap_entry` :397, `insert_entry_if_absent` :422,
`compare_and_swap_batch` :441, `append_if_snapshot_unchanged` :495,
`compare_exchange` :713, `compare_exchange_snapshot` :759,
`try_compare_exchange_snapshot` :770, all in
`CultLib\packages\cultcache-rs\src\lib.rs`, expectations as
`CultCacheExpectedEnvelope` :291) collapse to two conditions on the batch:
- `Expect(key, current)`: `current` must be the instance the cache holds at
  `key` (looked up through `_handles`; anything else throws) or `null` for
  "must be absent". The batch captures the record's identity, `(SchemaId,
  StoredAt)` from `_entries[key]`, at stage time. Only named keys are
  constrained, so unrelated concurrent writes do not lose the race (Rust's
  per-entry family).
- `ExpectUnchanged()`: the home store's persisted record set must equal what
  the cache last loaded from it, compared as the ordered list of `(key,
  SchemaId, StoredAt)` (Rust's snapshot family; fences unknown inserts).

Identity is `(SchemaId, StoredAt)` rather than payload bytes because the
cache does not retain bytes and the in-memory object may have been mutated
since it was observed; it is sound only if every write to a key mints a
`StoredAt` strictly later than the record it replaces, so `Push`,
`CommitBatch` and the batch builder bump a minted `StoredAt` by one tick when
it is not later than the existing record's (`"O"` has 100 ns resolution and
two writes can land in one tick; `StoredAtIsStrictlyIncreasingPerKey` in
Cut 3 is the proof, since `[P7]` produced no result). `Commit` returns `false`
when a condition fails: no store write, no in-memory change, no notification.
`TryCommit` makes one non-blocking lock attempt and reports `Contended`
instead of waiting (Idunn's `try_` use); it is the same path with `wait:
false`, about ten lines. A conditional commit on a store with staged
single-record writes (`IsDirty`) throws: that view is neither what was
observed nor what is being committed, so flush first.

Store side. Single-file store: a lock file beside the store
(`<path>.lock`, `FileMode.OpenOrCreate`, `FileShare.None`, retry every 10 ms
for 30 s when `wait`, one attempt when not), mirroring
`DirectoryMessagePackBackingStore.AcquireCommitLease` (`DMS:928-949`);
under it: re-read the file, evaluate `Expected` against the re-read records
and `ExpectUnchanged` against the fingerprint taken at the last `PullAll`
or successful commit, apply upserts and deletes, write temp then
`File.Replace` (`CC:2659-2694` unchanged), refresh `Entries` and the
fingerprint, release. Plain `PushAll` takes the same lock so two processes
never interleave a write, but compares nothing: **a plain flush is
last-writer-wins**, and processes sharing a store must all use conditional
commit; the contract says so. Directory store: conditions are evaluated
under its existing `AcquireCommitLease` against the manifest re-read from
disk (`_durableIndex` identities are the same `(SchemaId, StoredAt)`),
`ExpectUnchanged` against the manifest's record set, then its existing
content-addressed pages and manifest write. The identity and the lock are
proven by Cut 3's `ConditionalCommitTests.cs` (stale `Expect` detected after
a concurrent write, unrelated writes ignored, `Expect(null)` as create-once,
`ExpectUnchanged` failing on an unrelated insert, two writers racing on one
file finishing at exactly `2N`); the probe meant to pre-establish them `[P7]`
never produced a result.

Parity: Rust already has both families and a `try_` (`with_exclusive_lock`
:700 uses `fs2` on a lock file); TS and Python have neither and are recorded
in the contract as not implementing conditional commit. Whether Rust's
`fs2` lock and C#'s `FileShare.None` open on the same lock file exclude each
other across runtimes on one file is not established here and is a named
follow-up; no store is shared across runtimes today.

The batch is an explicit value: reads during `stage` see committed state
only, matching every consumer with a batch primitive (Rust `put_prepared_batch`
requires one store per batch, `CultLib\packages\cultcache-rs\src\lib.rs:2174-2186`; Ghostlight and Odin pass
explicit `compare_and_swap_batch`/`compare_exchange` values, section 2.0). No
`AsyncLocal`, no ambient overlay. The store side is the existing
`CacheBackingStore.CommitBatch` (`CC:2425-2452`: stage, `PushAll`, restore on
failure; `DMS:308-351`: pages then manifest). For a zero-store cache the
batch admits in memory. Siblings: Rust already matches (one store per batch,
all-or-nothing); TS has no batch and Python's `put_envelopes` is per type and
not all-or-nothing across types; both are recorded in the contract as not yet
implementing the primitive, with no bytes at stake and no cut in this
migration.

**Forbidden writers.** The replication loops (`CC:2097-2101, 2135-2139`);
attach-time push (`CC:1436-1439`); `typeof(T)` at `CC:1632, 1641`;
`CND:875, 913` taking the descriptor from `T` (they take
`_cache.Registry.GetRequired(document.GetType())`); `SingleFileBackingStore.PullAll`'s
dirty early-return (`CC:2572-2573`) stays: attach-time pull always sees a
clean store, and re-pull must not erase staged mutations.

**Named demotions.** Replication is no longer an owner of durability. Attach-time
push is no longer an owner of a store's contents. `_hasUnflushedMutations` is
no longer an owner once a store exists. `typeof(T)` is no longer an owner of
schema. Exact `DocumentType` is no longer an owner of lookups or watches. The
cache is no longer an owner of any global's existence. The ambient
transaction is no longer an owner of visibility; a batch is a value and the
store's commit is the boundary.

### 2.3 Serialization options

**Owner.** The assembly that declares a document. `[assembly:
CultCacheFormatterResolver(typeof(R))]` (`AllowMultiple = true`; `R` exposes
`public static readonly IFormatterResolver Instance` or a public parameterless
constructor). `CultDocumentMessagePackSerialization.OptionsFor(Assembly)`
builds and caches
`Standard.WithResolver(CompositeResolver.Create(consumers..., CultDocumentResolver.Instance, StandardResolver.Instance)).WithSecurity(CultMessagePackSecurity.Instance)`
per assembly; `Options` (static) stays as the base for assemblies declaring
nothing and for the store envelope.

**Inputs.** `type.Assembly` of the document. **Outputs.** one options instance
per assembly, used by `SerializeUntyped`/`DeserializeUntyped`
(`CultDocumentMessagePackSerialization.cs:84-121`) and by the generator's
emitted `var options = ...` (`CultDocumentMessagePackGenerator.cs:290, 308`,
which becomes `OptionsFor(typeof(X).Assembly)`).

**Slot authority and generic documents (operator decisions 2026-09-13).**
MessagePack's integer `[Key]` is the single slot authority for schema identity,
catalog, compatibility and serialization. `GameCult.Caching` references
`MessagePack.Annotations` directly and matches `KeyAttribute` and
`IgnoreMemberAttribute` by type instead of by name string: the core was once
meant to be serializer-independent, but `.cc` is canonically MessagePack, so
that indirection is dropped. A generic document's declared resolvers come only
from the document type's own assembly (`typeof(Doc<X>).Assembly`).

**Generator and registry agreement (Soul, Cut 4).** Old-versus-new consumer
probes found what the single agreement test missed: per-declaring-type accessor
names collide when two documents share a base with `[CultName]`/`[CultIndex]`;
the generator read a base declaration where reflection read the override, which
changed ids and in one case bytes; duplicate slots (including `new`-hidden
members) went unchecked once MessagePack's analyzer stopped reaching consumers;
inherited private setters stopped compiling; and the typed
`Serialize<T>`/`Deserialize<T>` helpers bypassed per-assembly options. The fixes
name accessors per document, use the most-derived declaration in both paths,
reject duplicate slots and string keys with one message each, and route the
typed helpers through the untyped path, proven by a descriptor-equality sweep
across member shapes with a byte pin for overrides.

**The generator is deleted (operator decision 2026-09-13).** With `.cc`
canonically MessagePack, CultLib's source generator only duplicated the
reflective registry: every runtime read of its output already fell back to
reflection and `MessagePackSerializer` (Unity always ran that way), no consumer
builds AOT, IL2CPP or trimmed, no benchmark or commit shows a speed benefit, and
the duplication caused every Cut 4 defect below. The reflective
`CultDocumentRegistry` is the only descriptor authority and keeps every
rejection rule; payloads go through `MessagePackSerializer` with
`OptionsFor(assembly)`. MessagePack's own generator stays contained. If an AOT
target ever appears, MessagePack's own AOT generator is the answer, not a
second copy of the rules. The generator member-discovery and sweep work below
is superseded; the rules it settled now live in the registry alone.
The first deletion attempt showed the fallback claim was incomplete: the
generated codec was the only writer for `[CultDocument]` types without
`[MessagePackObject]` (MessagePack's resolver refuses them), and the generator
named nested member types `Outer.Inner` where reflection uses `Outer+Inner`.
Operator decisions: every `[CultDocument]` must carry `[MessagePackObject]`
(the registry rejects one without it), and nested member type names keep
reflection's `+` form, after a scan confirms no persisted store holds the dot
form. Follow-ups: Delvehold drops its two analyzer references when it re-pins
CultLib; EveUnity's `GenericWorldCaptureTests.cs:65` (both projects) moves
off the deleted `Deserialize<T>` when it re-pins.

**Overrides (Cut 4 fix, CultLib `7c4bc2b`).** The "most-derived declaration"
rule proposed after Soul's review was wrong: MessagePack itself reads the base
declaration's attributes (an `[IgnoreMember]` override still serializes at the
base slot; a re-keyed override throws a duplicate key). Both the generator and
the registry therefore reject any override whose `[Key]` or `[IgnoreMember]`
differs from its base declaration, and accept overrides that repeat or omit
them, pinned against MessagePack's own bytes. Typed
`Serialize<T>`/`Deserialize<T>` are deleted rather than rerouted.

Follow-ups outside this migration: a global-namespace document type is named
`<global namespace>.Type` by the generator and `Type` by `CultSchemaTypeNames`,
so its schema id differs between builds (fixing it changes those ids); and the
generator emits `typeof(Doc<T>)` for generic document types, which does not
compile, so a generic document cannot live in an assembly that runs the
generator.

**Security.** `CultMessagePackSecurity : MessagePackSecurity` copies
`UntrustedData` and overrides `GetHashCollisionResistantEqualityComparer<T>()`
to return, for `CultRecordRef<TDoc>`, a comparer over `Key.Value` built from
`GetEqualityComparer<string>()` `[P5]`.

**Forbidden writers.** No mutable static, no registration method, no first-use
ordering rule.

## 3. Wire-parity plan

| Change | Bytes on disk or wire | Runtimes that change | Verified by |
|---|---|---|---|
| One home store per type; mirrors deleted | none; every routed file is a complete `cultcache.store.v1` snapshot | C# (Cut 3), TS, Rust, Python (Cut 5) | C# `RoutedStoresWriteTheSameBytesAsSingleStores`; the C# interop peer's `write-routed` mode writes `catalog.cc` and `run.cc` and the writer-by-reader loop in `CultLib\packages\cultcache-ts\test\cult-cache.test.ts:536-617` reads both with TS, Rust and Python readers |
| Attach hydrates; loading never writes; read-only stores | none | C# only (sibling attach does not read; sibling pull does not write) | Cut 3 tests |
| Globals never invented; singleton enforced | none (key divergence recorded) | C# only | Cut 3 tests |
| Runtime type decides schema | none for existing records | C# only | fixture ids in `cultcache-schema-compatibility.md:20-24` unchanged; `UpsertWeaponThroughGearHandleReloadsAsWeapon` |
| Assignable lookups and watches | none | C# only | Cut 3 tests; Mesh and Networking suites |
| Subtraction (SoA parked, managed documents, legacy directory formats, wrappers) | none; v4 directory manifests and v1 single files unchanged | C# only | full suites at their prior pass counts minus the deleted tests; external consumers build |
| Atomic commit becomes an explicit batch on one home store | none (the store's commit is unchanged: single-file one replace, directory pages then manifest) | C# only; Rust already matches; TS and Python recorded as not implementing it | Cut 3 tests `BatchIsAllOrNothingOnStoreFailure`, `BatchAcrossTwoHomesThrows`, `BatchObserversSeeOnlyCommittedRecords` |
| Conditional commit (`Expect`, `ExpectUnchanged`) and the single-file lock file | none in the store file; a new `<path>.lock` sidecar beside single-file stores (the directory store already has `.commit.lock`) | C# gains what Rust has (`compare_exchange` :713, `compare_exchange_snapshot` :759, `try_` :770 in `CultLib\packages\cultcache-rs\src\lib.rs`); TS and Python recorded as not implementing it; cross-runtime lock exclusion on one file is a follow-up probe | `ConditionalCommitTests.cs` (Cut 3), `[P7]` |
| Per-assembly options, security comparer | payload bytes of consumer-declared schemas only | C# only | `InteropNoteBytesUnchanged`; `RefKeyedDictionaryRoundTrips` |
| Contract text | none | docs in all four | review |

The hosted workflow (`.github\workflows\cultnet-interop.yml`) tests CultNet
frames only; `.cc` parity is the `CultLib\packages\cultcache-ts` test, run by `npm test` and on
`cultcache-ts-v*` tags. Rust writes a stub catalog and second-precision
`storedAt` (`CultLib\packages\cultcache-rs\src\lib.rs:2397-2433`), a pre-existing byte-parity gap this cut
neither widens nor closes.

## 4. Cut sequence

Each cut is an independently executable Hands task with its own Soul check.
Build host for every step is the Windows workstation. Cuts 2 and 3 are kept
separate so Soul can falsify pure subtraction (no behavior change) apart from
the behavior change.

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

- Repo/branch: `F:\Projects\CultLib`, `codex/cultcache-store-routing`.
- Adds `src\GameCult.Caching\Contracts\cultcache-store-composition.md` (sections
  2.1-2.3 as contract: routing, attachment, dirtiness, read-only, globals,
  options ownership, value-type encoding, the explicit-batch commit);
  amends `cultcache-persistence-format.md:42-55` ("Transaction Visibility"
  becomes: a commit is an explicit batch of records that resolve to one home
  store; the store commits it as one durable step; nothing is visible, to the
  committing flow or to observers, until the store has accepted it; a batch
  may carry conditions, per-record `(schemaId, storedAt)` identity or
  whole-store unchanged, evaluated under the store's exclusive lock, and a
  failed condition is a lost race, not an error; **conditional commit is the
  only write protected against a concurrent change to the same records: a
  plain flush or unconditional commit on a single-file store writes the
  cache's snapshot and is last-writer-wins, and on a directory store lands its
  changed pages onto the current manifest, last-writer-wins per key, so
  processes that must not overwrite each other's changes use conditional
  commit**; batch and conditional commit are specified for C# and not yet
  implemented there; Rust implements both; TS and Python implement neither
  yet) and
  `cultcache-schema-compatibility.md` (runtime type decides schema; lookups
  assignable); amends `docs\runtime-parity-scope.md:23-27, 33` (SoA claim
  retracted); rewrites `CultLib\README.md:240-264`, the generator README, and
  `CultLib\packages\cultcache-rs\README.md:216-241`.
- Adds `tests\GameCult.Caching.Tests\StoreRoutingTests.cs`, compiling against
  today's API, red for the stated reason:
  1. `LoadingNeverWritesASecondStore` (seed A; attach A, pull, attach B, flush;
     B's file must not exist). Red: `[P2-A]`.
  2. `UpsertWeaponThroughGearHandleReloadsAsWeapon`. Red: `[P2-E]`.
  3. `GetByNameMatchesAssignableTypes`. Red: `[P2-D]`.
  4. `WatchMatchesAssignableTypes`. Red: `[P2-D]`.
  5. `GetGlobalMatchesAssignableTypes` (after an explicit upsert). Red: `[P2-D]`.
  6. `ConstructingACacheInventsNothing` (registry with a `[CultGlobal]` type;
     `new CultCache(registry).AllEntries` empty). Red: `[P1]`.
  7. `AttachThenFlushLeavesASeededFileByteIdentical` (seeded file, registry
     with a `[CultGlobal]` type; attach, pull, flush; bytes unchanged, record
     loads). Red: `[P1]`.
  8. `RefusedPushLeavesNothingInMemory` (store whose `Push` throws; after the
     failed upsert `Get(key)` null). Red: `[P2-C]`.
- Command: `dotnet test tests\GameCult.Caching.Tests --filter FullyQualifiedName~StoreRoutingTests`.
- Soul: eight red for the stated reasons; nothing under `src\` changed.

### Cut 2. CultLib subtraction (no behavior change)

- Repo/branch: same, after Cut 1. Every deletion here has no consumer outside
  tests per the audit; behavior for every kept member is unchanged.
- Deletes, in this order:
  1. Park SoA, as its own commit so Soul can check it apart from the rest:
     `git tag -a parked/cultcache-soa c2a9a6e` (or the branch commit before
     this one) with a message stating the intent ("ECS-style structure-of-arrays
     columns over cached documents, chasing performance without compromising
     document ergonomics"), the span (`CultManagedDocument.cs:133-414`:
     `CultSoaTable<T>`, `CultSoaColumn<T>`, `CultCacheSoaStore`,
     `CultCacheSoaTypeTable`, `CultCacheSoaMember`; `CC:1419-1422` `Soa<T>`;
     the `_soa` field and its `Upsert`/`Remove` calls at `CC:1970, 1987, 2094,
     2133`; the SoA tests in `BackingStoreTests.cs`), and that nothing outside
     tests consumed it. Add `docs\parked-features.md` naming the tag, the
     span, and the restore path (`git show parked/cultcache-soa:src/GameCult.Caching/CultManagedDocument.cs`
     and re-hook `_soa` in `Admit`/`Evict`). Amend `docs\runtime-parity-scope.md:23-27,
     33` to say the SoA table is parked at that tag, not that the claim was
     wrong. Then delete the span. Nothing else is parked: no other surface on
     this list carries a stated design reason; if Hands find one in a commit
     message, they stop and flag it.
  2. `CultManagedDocument.cs:72-131` (`CultManagedDocument<T>`); `CC:1387-1396`
     (`Document<T>`); `CND:855-864` (`Document<T>`).
  3. CultNet's transaction wrapper only: `CND:519-555`, `:305`
     (`RequireTransactionsForAuthoritativeWrites`), `:872, 898-901,
     1791-1796` (tests only). The cache's own transaction machinery is **not**
     touched here; Cut 3 replaces it in the commit that lands `Commit`.
  4. Wrappers and unused members: `CC:1519-1523` (`FlushAsync` becomes the
     one-liner `FlushAllBackingStores(); return Task.CompletedTask;` — the only
     wrapper kept, because both names have consumers), `:1525-1558`
     (`FlushBackingStore`, `FlushBackingStoreCore`), `:1560-1575`
     (`PrepareForReloadOrShutdown` ×2), `:1467-1490` (`PullBackingStoreRecordsAsync`),
     `:1731-1738` (`TryGet<`), `:1748-1760` (`GetStoredDocuments<`, fold into
     `GetAll<T>`), `:1787-1794` (`TryGetByName`), `:1810-1817`
     (`TryGetByIndex`), `:1819-1825` (`Resolve<`), `:1318-1325` (`Logger`),
     `:1332-1335` (`LastSuccessfulFlushAtUtc`) and store `:2365-2368`,
     `CacheBackingStore.Logger` (`:2350-2354`), the `soft` parameter everywhere
     in the two Caching projects except `CultCache.FlushAsync(bool soft = false)`,
     which Studio reflects on (`CultCacheStudioWindow.cs:739`) and keeps until
     Cut 6, `EntryAdded/EntryUpdated/EntryDeleted`
     (`:2383-2391`; replaced by `protected internal Action<CultStoredDocument>? Loaded,
     Unloaded`, since `DirectoryMessagePackBackingStore` lives in another
     assembly), the `GetAwaiter().GetResult()` subscriptions (`:1432-1434`;
     `AddStoredDocumentInternal` becomes synchronous `void`, its `async` and
     `await Task.CompletedTask` go).
  5. `CultCacheMessagePack.cs`: `ConfigureCache`, `ConfigureStore`,
     `DirectoryStorePath`, `DirectoryStoreHydrationFilter`,
     `ConfigureDirectoryStore` and their uses (`:32-40, 47-61, 109, 113-118,
     127`). `OpenAsync` keeps its behavior here (pull, then its existing
     materialization): `Create` never pulls, so `Task.FromResult(Create(...))`
     is only correct once attach hydrates, and moves to Cut 3.
  6. `DMS`: `HydrationFilter` (`:46, 635`), `ReadStageProbe`, `FlushStageProbe`,
     stage constants and every `?.Invoke` (`:158, 245, 418, 456`),
     `ReadPersistedGeneration`, `PullSelected`/`PullSelectedCore`
     (`:229-274`) together with the base `CacheBackingStore.PullSelected`
     (`CC:2402-2409`) and `CultPersistedRecordMetadata` (`CC:2304-2323`), which
     the directory store and open options use until this step, `LoadLegacyRecords`, `_legacyInlineRecords`,
     `LegacyRecordPath`, `MetadataRecordPath`, `_needsIndexUpgrade`,
     `_manifestUsesMetadataPages`, `_manifestUsesImmutablePages`, the v2/v3
     format constants and every branch on them. A missing manifest is an empty
     store (the store synthesizes a `v1.directory` manifest for it today and
     keeps treating it as empty); an existing manifest whose
     `FormatVersion` is not the v4 constant throws
     `InvalidOperationException("Directory store {path} is {format}; only
     {v4} is readable.")`.
  7. `CultDocumentMessagePackSerialization.cs:144-175` (`SerializeSchemaCatalog`,
     `DeserializeSchemaCatalog`).
  8. Every `///` line in all eight files of the two Caching projects; `<NoWarn>$(NoWarn);CS1591</NoWarn>`
     in `GameCult.Caching.csproj` and `GameCult.Caching.MessagePack.csproj`.
     Comments that carry an invariant (e.g. the dirty-pull guard at
     `CC:2568-2571`) stay as `//`.
  9. Tests that exist only to exercise the deleted surface (SoA, managed
     documents, stage probes, legacy directory formats, selective hydration,
     the CultNet transaction wrapper) are deleted with it; the cache
     transaction tests stay until Cut 3 rewrites them against `Commit`;
     `tests\GameCult.Caching.Tests\README.md` is updated.
- `CultMesh.FlushAsync(bool soft)` and `CultNetLocal.FlushAsync(bool soft)`
  keep their signatures and call `FlushAsync()`; the parameter is dead, and
  its removal together with the four external call sites (Mimir
  `EveDashboard:900`, `CultMeshMedia:422`; Brokkr `BrokkrCultMeshMirror.cs:68-124`)
  is a named follow-up, not part of this migration.
- Commands: `dotnet build CultLib.sln`; `dotnet test tests\GameCult.Caching.Tests
  tests\GameCult.Networking.Tests tests\GameCult.Mesh.Tests tests\GameCult.Geometry.Tests`;
  `dotnet build` of `F:\Projects\Aquarium\src\Aquarium.Epiphany`,
  `F:\Projects\Ymir\src\Ymir.Core`, `F:\Projects\AquaSynth` (its .NET
  projects), `F:\Projects\Mimir` (its .NET projects), `F:\Projects\Gjallar\src\Gjallar`;
  `rg "ConfigureCache|ConfigureStore|DirectoryStorePath|ConfigureDirectoryStore|FlushBackingStore\(|PrepareForReloadOrShutdown|TryGetByName|TryGetByIndex|ExecuteTransactionAsync|\.Soa<|\.Document<|LastSuccessfulFlushAtUtc|HydrationFilter"` over those five repos, Delvehold, Brokkr, Eve, EveUnity, EvePlugins must be empty (Brokkr, Eve*, Delvehold are not built here; the grep is the proof).
- Behavior changes Cut 2 did make (Soul, Cut 2), recorded rather than reversed:
  (B1) an exception while the cache admits a loaded record now propagates out
  of `PullAll`, where the R3 `Subject` path swallowed it; Cut 3's `Admit`
  makes load all-or-nothing. (B2) attaching one store to a second cache
  silently replaces the first cache's `Loaded`/`Unloaded`; Cut 3 refuses it.
  (B3) a manifest deleted while the process runs empties the store on the next
  flush, since a missing manifest is an empty store. (B4) a v1 single-file
  snapshot opened as a directory store throws instead of converting. Also: a
  clean flush on a fresh directory store writes nothing, and orphan pages with
  no manifest are ignored. Atomic replace, pages-before-manifest ordering,
  content-addressed pages and both leases are unchanged in code, but their
  tests went with the stage probes; Cut 3 restores coverage.
- Soul: `git tag -l parked/cultcache-soa` resolves to a commit whose
  `CultManagedDocument.cs` still holds `CultSoaTable`, and `docs\parked-features.md`
  names it; Cut 1's eight tests still red for the same reasons (no behavior
  moved); every kept test passes; the five external builds green; the grep
  empty; `wc -l` delta of the two Caching projects against `e9b91c1` reported
  (the ambient transaction machinery stays until Cut 3, so the 2.0 size target
  is checked there); `rg "GetAwaiter\(\)\.GetResult\(\)" src\GameCult.Caching`
  empty (the `async |AsyncLocal|SemaphoreSlim` emptiness check moves to Cut 3);
  `rg "///" src\GameCult.Caching src\GameCult.Caching.MessagePack`
  empty; `cultcache.store.v4` directory manifests and v1 single files written
  before the cut still open.

### Cut 3. CultLib routing, attachment, globals, read-only, lookups

- Repo/branch: same, after Cut 2.
- Files: `CC`; `CultDocumentMessagePackSerialization.cs`
  (`SingleFileMessagePackBackingStore` ctor); `DMS` (ctor, `:277-281`);
  `CultCacheMessagePack.cs` (`ReadOnly`, `initializeGlobals` plumbing);
  `CND:875, 913`; `tests\GameCult.Caching.Tests\StoreRoutingTests.cs`,
  `BackingStoreTests.cs:342, 358, 365`; `tests\GameCult.Caching.InteropPeer\Program.cs`.
- Deletes first: 2.1's forbidden writers and 2.2's forbidden writers.
- New behavior: sections 2.1 and 2.2 exactly, in the shape of 2.0.
- Interop peer: mode `write-routed <catalog.cc> <run.cc>` attaching two routed
  single-file stores (`interop-note` -> catalog, `interop-run-note` -> run),
  one record each, flush. `read` unchanged.
- Cut 1 fixtures this cut rewrites (Soul, Cut 1): `LoadingNeverWritesASecondStore`
  attaches its second store with a home type (e.g. `AddBackingStore(second,
  typeof(RoutingOther))`, `RoutingOther` added to the registry), because a
  second untyped store is now a registration error and the Cut 1 form could not
  name a home type against the old API. `StoreRoutingTests.GlobalRegistry` and
  `BackingStoreTests.cs:332-334` stop setting `<IsGlobal>k__BackingField` by
  reflection; their fixtures become real `[CultGlobal]` types, safe once nothing
  invents globals. Soul: `rg "k__BackingField" tests` empty.
- Tests that pass after: Cut 1's eight, plus in `StoreRoutingTests.cs`:
  `RoutedStoresWriteTheSameBytesAsSingleStores` (pin `storedAt` through
  `UpsertAsync(Type, object, key)` on a `CultStoredDocument` built with a fixed
  timestamp, or compare after normalizing `storedAt`); `CatalogRecordNeverLandsInRunStore`;
  `AttachAfterDirtyThrows`; `SecondUntypedStoreThrows`; `DuplicateHomeTypeThrows`;
  `WriteWithoutHomeThrows` (both stores clean, `Get` null);
  `ForeignRecordIsRefusedOnLoad` (attach throws naming key and stores;
  `AllEntries` empty); `LateRouteOverAdmittedTypeThrows` (attach an untyped
  store holding a `Note`, then attach a store routed to `Note`; throws naming
  both stores; `_entries` unchanged; the untyped store not dirty; the reverse
  order succeeds); `ReadOnlyStoreRefusesWrites` (throws; file bytes
  unchanged; `Get` null; `IsDirty` false); `ReadOnlyStoreIsNotFlushed`;
  `GlobalSingletonIsEnforced` (second key throws; a file with two records of a
  global type is refused at attach); `AmbiguousAssignableLookupThrows`.
- Durability coverage restored (Soul, Cut 2), without a public probe: faults
  come from real file locks. `tests\GameCult.Caching.Tests\DirectoryStoreDurabilityTests.cs`:
  `FailedManifestReplaceKeepsPreviousGeneration` (hold the manifest with
  `FileShare.None`; a dirty flush throws; after release a reopen reads the old
  values, `IsDirty` stays true, and the next flush commits);
  `SameStoredAtDifferentPayloadWritesADistinctPage`;
  `PullAllWaitsForHeldCommitLease` (hold `<records>\.commit.lock`; `PullAll`
  has not returned within 100 ms and returns after release);
  `MutationBlockedBehindFlushStaysDirty`; `FlushNeverOpensUnchangedPages` (a
  clean page held with `FileShare.None`; flush succeeds);
  `MissingManifestIgnoresOrphanPagesAndFlushDeletesThem`;
  `TamperedPageFailsLoadNamingPage`. `DirectoryStoreHonorsConditions` covers
  the conditional-commit path.
- Load and attachment: `PullAllAdmissionFailureLeavesStoreAndCacheConsistent`
  (a record that fails `Admit` leaves neither the store's entries nor the cache
  partly updated); `StoreAttachedToSecondCacheThrows` (`AddBackingStore`
  refuses a store whose `Loaded` is already set). Decide
  `ResolveLegacyUncataloguedRecordCatalog` (`DMS`): delete it under the target
  invariant that legacy formats are read only by the importer, unless a v4
  store can reach it, in which case test it against v4.
- CultNet: `Commit` must not publish CultNet puts or deletes staged in a batch
  before the store commits. Add `CultNetPutInsideCommitPublishesOnlyAfterCommit`,
  or make `CultNetDatabase` writes refuse inside a batch; pick the smaller.
- Commands: `dotnet build CultLib.sln`; the four test projects as Cut 2; the
  five external builds as Cut 2; `rg "UpsertAsync<|AddAsync<|PutAsync<"` over
  Delvehold, AquaSynth, Mimir, Gjallar reviewed for a base-typed `T` (none
  expected); `rg "GetGlobal<|global:" F:\Projects\AquaSynth` to report how its
  `[CultGlobal]` types are read now that nothing invents them.
- Soul: every test; schema-compatibility fixture ids unchanged; Mesh,
  Networking, Geometry suites at their Cut 2 counts; external builds green;
  the AquaSynth global report written into this document's section 7;
  `CC` at or below the 2.0 shape's size; `rg "MaterializeMissingGlobals|InitializeGlobals|ContainsDurableRecord|PullOnOpen|initializeGlobals|ExecuteTransactionAsync|VisibleStoredDocuments" src tests` empty, and `rg "AsyncLocal|SemaphoreSlim" src\GameCult.Caching` empty (Mesh and Networking use `SemaphoreSlim` for unrelated reasons).
- Atomic commit, in the same commit as the routing change: delete
  `CC:1577-1622` (`ExecuteTransactionAsync` ×2), `:1907-1921`
  (`VisibleStoredDocuments`), `:1923-2066` (`CommitTransaction`,
  `CultCacheTransaction`), every `_ambientTransaction` check (`:1449, 1474,
  1497, 1536, 1630, 1674, 1714, 1832, 1860`), `_transactionGate`; add
  `CultCacheBatch` and `Commit(Action<CultCacheBatch>)` as in 2.2; keep
  `CacheBackingStore.CommitBatch` (`CC:2425-2452`, minus its `soft`) and
  `DMS:308-351`. Tests (behavioral): `BatchIsAllOrNothingOnStoreFailure` (a
  store whose `PushAll` throws on the second call: after the failed commit no
  record of the batch is in `Get`, the store's file is unchanged, `IsDirty` is
  false); `BatchAcrossTwoHomesThrows` (one catalog and one run record: throws
  before either store is touched; both `IsDirty` false; neither key readable);
  `BatchObserversSeeOnlyCommittedRecords` (`Watch<T>` receives exactly one
  change per committed record, none before `CommitBatch` returned, none for a
  failed batch); `BatchReadsSeeCommittedStateOnly` (inside `stage`, `Get` of a
  key upserted earlier in the same batch returns the pre-commit value);
  `SingleUpsertAndBatchShareOneAdmissionPath` (a global singleton violation
  is refused identically through `UpsertAsync` and through a batch).
- Conditional commit, same commit as the batch: files `CC` (`CultCacheBatch.Expect`,
  `ExpectUnchanged`, `CultCommitRequest`, `CultCommitOutcome`, `Commit` returning
  `bool`, `TryCommit`, the `StoredAt` bump in the batch builder and `Push`),
  `CultDocumentMessagePackSerialization.cs` (`SingleFileMessagePackBackingStore`:
  lock file, fingerprint after `PullAll`, `CommitBatch(request, wait)`,
  `PushAll` under the lock), `DMS` (`CommitBatch(request, wait)` evaluating
  conditions under `AcquireCommitLease` against the re-read manifest).
  Tests (behavioral, `tests\GameCult.Caching.Tests\ConditionalCommitTests.cs`):
  `StaleExpectFailsWithNothingChanged` (two caches on one file; A commits a
  new value; B's `Expect(key, itsInstance)` commit returns false; B's file
  bytes hash, `Get`, `IsDirty` and `Watch` count unchanged);
  `ExpectNullIsCreateOnce` (two caches both `Expect(key, null)` and upsert; the
  second returns false); `PerEntryCommitSurvivesUnrelatedWrite` (A writes key
  `b`; B's commit expecting only `a` returns true and the file holds both);
  `ExpectUnchangedFailsOnUnrelatedInsert` (A inserts `c`; B's
  `ExpectUnchanged` commit returns false); `TwoWritersRacingExactlyOneWins`
  (in-process first: two `SingleFileMessagePackBackingStore` instances on one
  file driven from two threads, each performing N conditional increments of
  one record with observe-outside, re-check-under-lock; the final value is
  `2N` and each writer reports at least one `Mismatch`; this proves the lock
  property because the lock is a `FileShare.None` handle, which excludes
  within a process exactly as across processes); `TwoProcessesRacingExactlyOneWins`
  (the cross-process form, only if the in-process test is judged
  insufficient: the test launches exactly two children with an explicit host,
  `dotnet <path-to-child.dll>`, never `Environment.ProcessPath`; the child
  role is an environment variable, `CULTCACHE_RACE_CHILD=1`, checked before
  any other logic in the child's `Main`; the child `Main` throws if it is ever
  asked to spawn; the parent asserts `Process.GetProcessesByName` shows at
  most two children at any sample and fails the test otherwise; `N` is
  bounded and every loop has an iteration cap); `TryCommitReportsContended` (the
  test holds `<path>.lock` with `FileShare.None`; `TryCommit` returns
  `Contended` at once with bytes unchanged; `Commit` on another thread waits
  and succeeds after release); `PlainFlushNeverInterleaves` (one process
  flushes in a loop while another commits conditionally; every read of the
  file deserializes); `StoredAtIsStrictlyIncreasingPerKey` (a thousand
  upserts of one key in a loop produce a thousand distinct `StoredAt`s);
  `DirtyStoreRefusesConditionalCommit`; `CrossStoreConditionalBatchThrowsBeforeAnyStore`
  (both stores' files unchanged, neither lock file created);
  `DirectoryStoreHonorsConditions` (the four condition tests repeated against
  a `DirectoryMessagePackBackingStore`).
  `BackingStoreTests.cs` transaction tests (`:1362, 1412, 1422, 1457, 1514,
  1544, 1585, 1593`) are rewritten against `Commit` where they prove
  durability ordering, deleted where they proved the ambient overlay.

### Cut 4. CultLib serialization options and security

- Repo/branch: same branch; files disjoint from Cut 3 except the store
  constructor; may run in parallel with Cut 3.
- Files: `CultDocumentMessagePackSerialization.cs`, new
  `CultCacheFormatterResolverAttribute.cs`, `CultMessagePackSecurity.cs`,
  `CultDocumentMessagePackGenerator.cs:290, 308`,
  `GameCult.Caching.MessagePack.csproj` and `GameCult.Networking.csproj`
  (each gains `<PackageReference Include="MessagePackAnalyzer" Version="3.1.7"
  PrivateAssets="all" />` beside its `MessagePack` reference, so MessagePack's
  source generator stops flowing to consumers `[P6]`; CultLib never registers
  a generated resolver),
  `tests\GameCult.Caching.Tests\SerializationOptionsTests.cs`, `AssemblyInfo.cs`,
  and the member discovery in `CultDocumentMessagePackGenerator.cs` (`:58-61, 80,
  116, 204-207`).
- Generator member discovery (Soul, Cut 1). Today the generator walks only
  members declared on the class (`:80`) and emits a codec only when those slots
  are dense from 0, so a derived document's generated descriptor lists only its
  own members while the reflective one (`CultCache.cs:1115, 1125`) includes
  inherited ones: the two builds hash different schema ids and catalogs for the
  same type. The generator now discovers members along the base-type chain
  exactly as `CultDocumentRegistry` does (public instance fields and settable
  properties, inherited included, `[IgnoreMember]` excluded), and density is
  computed over that full set. Unkeyed ordering (generator by name, reflection
  by metadata token) is closed by requiring `[Key]` on every persisted member of
  a `[CultDocument]` type: a generator diagnostic error, and the reflective
  registry throws the same message. First step of the cut: `rg` every consumer
  (CultLib src, Aquarium, Ymir, Mimir, AquaSynth, Gjallar, Delvehold) for
  `[CultDocument]` types with unkeyed members and list them; if any has a
  persisted store, stop and report before adding the rule.
- Deletes first: `.WithSecurity(MessagePackSecurity.UntrustedData)` at `:55`.
- New behavior: section 2.3. `public sealed class CultCacheFormatterResolverAttribute : Attribute { public CultCacheFormatterResolverAttribute(Type resolverType); public Type ResolverType { get; } }`;
  `public static MessagePackSerializerOptions OptionsFor(Assembly documentAssembly)`;
  `public sealed class CultMessagePackSecurity : MessagePackSecurity { public static readonly CultMessagePackSecurity Instance; }`.
- Tests: `DeclaredResolverEncodesValueTypeAsPositionalArray` (test assembly
  declares a resolver for `struct Pair(float A, float B)` writing `[f,f]`;
  reflective and generated paths both emit the two-element array and round-trip);
  `GeneratedAndReflectiveDescriptorsAgreeForDerivedDocuments` (for
  `RoutingWeapon : RoutingGear` the generated and reflective `SchemaId` are
  equal, the catalog lists `Name@0, Damage@1`, a codec is emitted, and
  `UpsertWeaponThroughGearHandleReloadsAsWeapon` passes through it);
  `UnkeyedDocumentMemberIsRejected`; schema-compatibility fixture ids unchanged;
  `RefKeyedDictionaryRoundTrips`; `InteropNoteBytesUnchanged` (checked-in
  constant captured on `main`); `UnknownAssemblyGetsBaseOptions`;
  `RefKeyedDictionaryCompilesInAConsumer`: a test-only console project under
  `tests\` that references `GameCult.Caching.MessagePack` and declares a
  document with `Dictionary<CultRecordRef<T>, float>` builds without an
  analyzer-removal target (this is the build `[P5]` could not do).
- Commands: `dotnet build CultLib.sln`; `dotnet test tests\GameCult.Caching.Tests
  tests\GameCult.Networking.Tests`; `dotnet build F:\Projects\Aquarium\src\Aquarium.Epiphany`
  and `F:\Projects\Ymir\src\Ymir.Core` (they receive the analyzer change
  through Networking and Caching.MessagePack).
- Soul: the five tests; `rg "MessagePackSecurity.UntrustedData" src` finds only
  the copy-constructor argument; `obj\**\MessagePack.SourceGenerator` is absent
  from the consumer test project's build output.

### Cut 5. Sibling runtimes: delete mirrors

- Repo/branch: landed on `codex/cultcache-store-routing` with Cuts 1-4 (planned as `codex/cultcache-one-home-store`), independent of Cuts 2-4;
  three language-scoped tasks, parallel.
- TypeScript (`CultLib\packages\cultcache-ts`, all TS lines below in its `src\cult-cache.ts`): delete `mirrors` (`src\cult-cache.ts:18`),
  the three mirror pushes (`:402, 448, 503`), the `slice(1)` lists (`:663-677`);
  `#resolveRoute(type): CacheBackingStore | undefined`; `addBackingStore`
  (`:180-188`) throws on a second untyped store or a claimed type. Tests:
  `rejects a second generic backing store`, `rejects a type registered to two
  stores`, `routes each type to its home store`; the writer-by-reader loop
  (`:536-617`) adds the two files from the C# `write-routed` mode. Command:
  `npm test` in `CultLib\packages\cultcache-ts`. Version `0.13.5` -> `0.14.0`.
- Rust (`CultLib\packages\cultcache-rs`, all Rust lines below in its `src\lib.rs`): delete the four mirror loops (`src\lib.rs:2092-2094,
  2213-2215, 2252-2254, 2323-2325`) and the `route.len() != 1` branch
  (`:2174-2181`); `fn resolve_route_index(&self, type_id: &str) -> Option<usize>`
  replaces `resolve_route_indices` (`:2363-2380`); `add_backing_store`
  (`:1950-1959`) returns `Result<()>` and errors on a second generic store or a
  claimed type; callers (`src\lib.rs` tests, `examples\cultcache_interop.rs:77`, both under `CultLib\packages\cultcache-rs`)
  take the `Result`. Tests: `second_generic_store_is_rejected`,
  `type_claimed_twice_is_rejected`; `type_specific_store_routes_before_generic_store`
  stays. Command: `cargo test` in `CultLib\packages\cultcache-rs`. Version `0.1.0` ->
  `0.2.0`.
- Python (`CultLib\packages\cultcache-py`): `stores_by_type: dict[str, BackingStore]`,
  `generic_store: BackingStore | None` (`CultLib\packages\cultcache-py\src\cultcache_py\cache.py:23-24`, all Python lines below in that file); `add_backing_store`
  (`:100-102`) raises on a claimed type, `add_generic_store` (`:104-105`) on a
  second store; `_store_for_type(type) -> BackingStore` raises when none
  (`:300-302`); the four write loops (`:199-201, 218-219, 239-241, 269-270`)
  become single calls; `_all_specific_stores` (`:304-311`) deleted;
  `pull_all_backing_stores` (`:111`) iterates the distinct stores. Tests:
  `test_second_generic_store_rejected`, `test_type_claimed_twice_rejected`,
  `test_types_route_to_home_store`. Command: `python -m unittest discover -s tests` in `packages\cultcache-py` with `PYTHONPATH=src` (pytest is not installed; the suite is unittest). Soul, Cut 5: the siblings did not enforce C#'s "a record's home cannot change after admission" or refuse a record loaded from a store that is not its home, and TS and Rust rejected zero-store writes the contract treats as in-memory; all three are brought to the C# reference in a follow-up commit on the same branch, and Python's `add_backing_store(store, [])` becomes a generic store. A later Soul pass over the sibling series found TS and Python write paths touching the store before validation and accessors ran, TS open to async interleaving, Python accepting a global under any key, and Rust `load_soa` non-atomic. Operator decisions (2026-09-14, all option A): a TS cache serializes attach, pull, writes and registrations through one per-cache queue (its own mutations only, not observer delivery); TS and Python store a global only under `__global__`; Rust deletes the uncalled `load_envelope` and makes `load_soa` atomic with the home check; cross-runtime error wording stays as is. Every sibling write validates everything before touching a store, and a refused write changes neither store nor cache. A further Soul pass (after CultLib `15c6a5d`) found the TS queue could deadlock when a store awaited the same cache and swallowed rejections of unawaited calls, Python's reentrant lock let a nested write corrupt a load or registration, a Python global stored under a legacy key made the cache unloadable after one `put_global`, and the Rust batch was all-or-nothing only for stores with an atomic `push_all`. Operator decisions (2026-09-14): (B) a global stored under a key other than `__global__` loads as a compatibility shim that never writes; the first write to that global stores it under `__global__` and deletes the legacy record in the same validated write, and the shim is removed once no store holds a legacy-key global; two globals of one type on disk are still refused. (A) Re-entrant mutation from inside a store, decoder, accessor or updater is forbidden in Python with a clear error. TS first implemented it with an AsyncLocalStorage guard, which Soul showed refused the ordinary write-back pattern (a store's change handler calling `put` without the store waiting, which simply queues) while still missing waits that escape the async context; operator decision (A): the TS guard is deleted and the contract states the precise rule for every runtime: a store call must not wait for a write to its own cache to finish; write-backs that are not awaited by the store queue normally. Legacy global retirement stays two-step, and a crash leaving both records on disk stays a loud refusal at load. (A) A Rust store keeps receiving its full current view on a batch, and the default `push_all` fails closed instead of wiping rows partway.
  Version `0.2.0` -> `0.3.0`.
- Soul: the nine tests; `rg -n "mirror" packages\cultcache-ts\src packages\cultcache-rs\src packages\cultcache-py\src`
  empty; `npm test` green including the routed-file reads.

### Cut 6. CultCache Studio: compile-time reference and drawers

- Repo/branch: CultLib, `codex/cultcache-studio-drawers`, after Cut 3.
- Files: `src\GameCult.Unity\Assets\Caching\package.json` (`1.0.0` -> `1.1.0`;
  `"dependencies": { "org.gamecult.cultlib": "1.0.57" }`),
  `Editor\GameCult.Unity.Caching.Editor.asmdef` (reference `GameCult.CultLib`),
  `Editor\CultCacheStudioWindow.cs`, new `Editor\CultCacheStudioDrawers.cs`,
  `Runtime\CultCacheInspectorAttributes.cs` (one new attribute).
- Deletes first: the reflection bridge `CultCacheBridge` (`:683-790`) and the
  `PullOnOpen` line (`:687`); the disabled-text-field fallback in `DrawValue`
  (`:326-364`). The window calls `CultCacheMessagePack.OpenAsync`,
  `cache.UpsertAsync(Type, object, key)`, `cache.Remove(key)`,
  `cache.FlushAsync()`, `cache.IsDirty`, `cache.Registry.AllDescriptors`,
  `cache.AllStoredDocuments` directly.
- New behavior: `[AttributeUsage(Class)] CultInspectorDrawerAttribute(Type memberType)`;
  drawers discovered by `TypeCache.GetTypesWithAttribute<CultInspectorDrawerAttribute>()`
  implementing `ICultInspectorDrawer { object Draw(string label, object value, FieldInfo member); }`;
  built-ins for `IDictionary` (recursive keys and values, add/remove), abstract
  or interface members (subtype popup from the member type's `[MessagePack.Union]`
  attributes only), `CultRecordRef<T>` (popup of documents assignable to `T` by
  `[CultName]`, plus the raw key); vector and matrix drawers target CultMath
  types only, and the Studio package takes no Unity.Mathematics dependency.
  Unclaimed types render a
  visible error row.
- Soul, Cut 6 (after CultLib `8be174b`): expanding a foldout dirtied and
  rewrote records, a throwing drawer broke the pane, multi-dimensional arrays
  threw, empty-ref dictionary keys could produce an unloadable store, `New` on
  a directory store wrote nothing, and drawers could only claim whole types
  while Aetheria's `[InspectableColor]`, `[InspectableType]`,
  `[InspectableText]` and `[InspectableAnimationCurve]` need attribute claims.
  Operator decisions (2026-09-14): the `CultInspector*` attributes move into
  CultLib's engine-free core so headless shared models can use them; a drawer
  claims a member type or an attribute (attribute first), receives the value
  type, and can fall back to a public default draw; opening any directory store
  creates no folder or lock file (only a write does); no by-name reflection on
  CultLib types remains. Game data stores never live under a Unity `Assets`
  folder: CultCache exists so Unity does not manage game data, so the Studio's
  `New` refuses an `Assets` path and `Open` warns on one. Cut 8's manifest adds
  `org.gamecult.cultmath` by git URL, since Unity does not resolve package
  dependencies from git.
- Verification (operator decision 2026-09-14): the Studio assemblies compile in
  batchmode; the manual click-through happens in Aetheria after Cut 7 releases
  the packages and Cut 8 wires them in, against the tagged package rather than
  a scratch build. CultLib follow-up outside this migration: `src\GameCult.Unity`
  has no committed `Packages\manifest.json` (the root `.gitignore` excludes
  `Packages/*`) and its demo UI assembly does not compile (`ZLinq` never
  restored; Unity.Mathematics in `Assets\UI`). Original plan: batchmode compile of `src\GameCult.Unity`; then the operator
  opens a `.cc` with each member kind, edits, saves, and confirms open-then-close
  without edits leaves the file bytes unchanged.

### Cut 7. CultLib release

- Repo/branch: CultLib `main` after Cuts 1-6 merge.
- Files: `unity\org.gamecult.cultlib\package.json` (`1.0.56` -> `1.0.57`);
  `Runtime\Plugins\*.dll` via `powershell -File scripts\build-unity-package.ps1 -UpdateTemplate`;
  `unity\org.gamecult.cultlib\README.md:15`, `docs\nuget-packaging.md:22`;
  the three package versions from Cut 5.
- Tags on one commit: `cultlib-unity-v1.0.57`, `caching-unity-v1.1.0`,
  `cultcache-ts-v0.14.0`, `cultcache-py-v0.3.0`; pushed; the last two run
  `publish-packages.yml`.
- Verification: committed DLLs byte-identical to a fresh build;
  `GameCult.Caching.dll` exposes `AddBackingStore(CacheBackingStore, Type[])`
  and lacks `MaterializeMissingGlobals`, `ExecuteTransactionAsync`, `Soa`
  (reflection from a throwaway script); `git ls-remote --tags origin` lists
  the four tags; the publish jobs succeed.

### Cut 8. Aetheria data model cutover and AetherDb

- Repo/branch: Aetheria, `codex/cultcache-cutover` from
  `codex/aetheria-state-rebuild`. Depends on Cut 7.
- Deletes first: `Assets\Scripts\ServerShared\CultCache\CultCache.cs` (437),
  `DatabaseEntry.cs` (91), `ReflectionExtensions.cs` (127) and
  `CollectionExtensions.cs` (75) unless `rg "GetAllChildClasses|GetParentTypes"`
  finds a non-cache caller, `Serialization\JsonKnownTypes\**` (320 and its
  `.csproj`), `Serialization\TypeFormatterResolver.cs` (65),
  `Serialization\JsonConverters.cs` (169) if `rg "JsonConvert\."` outside it is
  empty, `Serialization\RegisterResolver.cs` (37); `Assets\Plugins\MessagePack\**`
  (97 files, 30,088 lines, asmdef, `.meta`s); `Assets\Scripts\CultCache\Editor\**`
  (17 files, 1,963 lines); the compile include at `Aetheria.Shared.csproj:23`;
  the `MessagePack` reference in `Aetheria.Shared.Unity.asmdef`; the root
  `[Union]` list and every `JsonKnownTypes` attribute and `using`; tags 4, 5,
  6, 14, 20; `GalaxyMapLayerData` (`GlobalData.cs:16`), `PlayerData`
  (`PlayerData.cs:10`), `GlobalSettingsAttribute` (`Attributes.cs:4`);
  AetherDb `doctor`, `migrate-products`, `AetherDb.Save()`.
- Kept and moved: `MathFormatters.cs`, `MathResolver.cs` (fix `:55` to
  `ArrayFormatter<int2?>`) to `ServerShared\Serialization\`; the `Inspectable*`
  attributes Cut 6 drawers read.
- Adds:
  - `Directory.Build.props`/`.targets` at the Aetheria root, copied from
    `F:\Projects\Delvehold` with `CultLibRoot` defaulting to `..\CultLib` and
    `CultLibRevision` set to the Cut 7 commit. Kept on its own merits: the only
    working pin for a sibling checkout (`pack-nuget.ps1` publishes nowhere).
  - `Aetheria.Shared.csproj`: `ProjectReference` to `$(CultLibRoot)\src\GameCult.Caching\GameCult.Caching.csproj`
    and `GameCult.Caching.MessagePack.csproj`. No analyzer handling: Cut 4
    keeps MessagePack's generator inside CultLib, so both bodies serialize
    through `DynamicObjectResolver` (Aetheria's backend is Mono,
    `ProjectSettings.asset:660-661`). The GameCult generator is not referenced.
  - `ServerShared\AssemblyInfo.cs`: `[assembly: CultCacheFormatterResolver(typeof(MathResolver))]`.
  - `Packages\manifest.json`: `"org.gamecult.cultlib": "https://github.com/GameCult/CultLib.git?path=/unity/org.gamecult.cultlib#cultlib-unity-v1.0.57"`,
    `"org.gamecult.caching.unity": "https://github.com/GameCult/CultLib.git?path=/src/GameCult.Unity/Assets/Caching#caching-unity-v1.1.0"`;
    `Aetheria.Shared.Unity.asmdef` references `GameCult.CultLib`, keeps
    `noEngineReferences: true`.
  - `[CultDocument("aetheria.<lowercase type>", "1")]` on every concrete
    document type in Q7; `[CultGlobal]` on `SavedGame`, `PlayerSettings`;
    `[CultName]` on existing `Name` members. `[MessagePackObject]` removed from
    the non-union abstract bases (`ItemData`, `CraftedItemData`,
    `EquippableItemData`, `BodyData`, `AgentTask`); value unions keep theirs.
  - References: `Guid` and `DatabaseLink<T>` -> `CultRecordRef<T>`;
    `Dictionary<Guid, float>` -> `Dictionary<CultRecordRef<T>, float>` with
    `[CultReference(typeof(T), many: true)]`; `List<Guid>` ->
    `List<CultRecordRef<T>>`. Slots unchanged; the legacy `Key(0)` slot is left
    unclaimed (the importer writes `nil`; MessagePack skips unclaimed slots).
  - `RequireBehavior` -> `string`.
  - `ServerShared\AetheriaStores.cs`:
    ```csharp
    public static class AetheriaStores
    {
        public static readonly Type[] CatalogTypes = { typeof(ItemData), typeof(Faction), typeof(FactionProductData), typeof(PersonalityAttribute), typeof(NameFile) };
        public static readonly Type[] RunTypes = { typeof(OrbitData), typeof(BodyData), typeof(SavedZone), typeof(SavedGame) };
        public static readonly Type[] PlayerTypes = { typeof(PlayerSettings), typeof(InputLayout) };
        // Attaches (hydrates) the catalog read-only unless catalogWritable, then the run and player stores when given.
        // Throws when a [CultGlobal] type routed to the catalog has no record.
        public static CultCache Open(string catalogPath, string? runPath = null, string? playerPath = null, bool catalogWritable = false);
    }
    ```
  - Q8's identity edits across the 75 Unity-side and 16 ServerShared sites
    (`rg "DatabaseLink|DatabaseEntry|\.ID\b|LinkID" Assets tools` is the
    worklist); `DatabaseLinkBase.Cache` gone; `ItemManager.GetData` resolves.
  - `Assets\Scripts\Editor\CultCacheDrawers.cs`: drawers for
    `[InspectableType]` (popup over `BehaviorData`'s `[Union]` names, writes
    the string), `[InspectableColor]`, `[InspectableAnimationCurve]`.
  - `tools\AetherDb`: `AetherDb.Open` -> `AetheriaStores.Open(root\GameData\Aetheria.cc,
    catalogWritable: command == "clear-boss-hulls" && apply)`; `census`,
    `factions`, `station-fit`, `hardpoint-fit`, `loadout`, `settings`,
    `settings-dump`, `clear-boss-hulls` rewritten over `CultRecordRef` and
    `TryGetHandle`; `save` reads `GameData\run.cc` when present; help lists
    every command. It must compile; it cannot run until Cut 9.
  - `tests\Aetheria.Shared.Tests` (xunit, net10.0) with a temp-directory
    catalog fixture built through `Open(catalogWritable: true)`:
    `OpenThenFlushLeavesEveryFileByteIdentical` (run and player seeded with
    their globals), `WeaponWrittenThroughGearHandleReloadsAsWeapon`,
    `CatalogRefusesWrites`, `SavedZoneNeverLandsInCatalog`,
    `FactionIsASingletonInstance`, `MissingCatalogGlobalIsLoud`.
- Commands: `dotnet build Aetheria.Shared\Aetheria.Shared.csproj`; `dotnet build
  tools\AetherDb`; `dotnet test tests\Aetheria.Shared.Tests`; Unity batchmode
  compile; operator accepts the two UPM git URLs on first editor open.
- Soul: headless build green with the pin guard (and refusing a dirty
  `CultLibRoot`); the six tests; `rg "DatabaseEntry|DatabaseLink|JsonKnownTypes|TypeFormatterResolver|MultiFileBackingStore|GlobalSettings" Assets tools`
  empty; `Assets\Plugins\MessagePack` gone; asmdef without `MessagePack`;
  manifest pins the two tags; batchmode clean;
  `rg "ReactiveProperty|ReactiveCollection" Assets\Scripts\ServerShared` shows
  no member of a document or embedded value.

### Cut 9. One-shot importer

- Repo/branch: same, after Cut 8.
- Files: `tools\AetherDb\Import.cs` (new), `Program.cs` (`legacy-census`,
  `import`), `GameData\Aetheria.cc` (new, committed), and `.gitattributes`
  gaining `*.cc filter=lfs diff=lfs merge=lfs -text` before the `.cc` is
  added: `AetherDB.msgpack` is LFS-tracked today and `.cc` matches no rule
  (`git check-attr filter` reports `unspecified`), so without the rule the
  catalog would land in plain git.
- Deletes first: the `withNameFiles` branch and comment (`AetherDb.cs:7-10, 25, 34`).
- New behavior `[P5]`: `AetherDB.msgpack` is a MessagePack array of 167
  `[tag, payload]` pairs; each name file is `[9, payload]`. `Import.cs` walks
  them with `MessagePackReader`. Tag table: 0 SimpleCommodityData,
  1 CompoundCommodityData, 2 GearData, 3 HullData, 9 NameFile, 13 Faction,
  15 OrbitData, 16/25-28 BodyData leaves, 17 PersonalityAttribute,
  29 CargoBayData, 30 DockingBayData, 31 WeaponItemData, 32 FactionProductData;
  any other tag is an error. Per record: slot 0 (16-byte `bin` Guid) becomes
  the key in `D` format and is written as `nil`; at every `CultRecordRef<T>`
  slot a 16-byte `bin` or a legacy `[bin16]` becomes the `D` string, `nil`
  stays; at every ref-keyed map slot the `bin16` keys become `D` strings; at
  `StatModifierData` slot 4 the assembly-qualified name becomes the simple
  name; nested `[MessagePackObject]` values and `[Union]` members are recursed
  by their own `[Key]`/`[Union]` metadata (reflection over the new types);
  everything else is copied raw. The rewritten payload is deserialized through
  `CultDocumentMessagePackSerialization.DeserializeUntyped` and written with
  `AetheriaStores.Open(catalogPath, catalogWritable: true)` via
  `UpsertAsync(type, document, new CultRecordKey(legacyKey))`, then `FlushAsync`.
  `[P5]` did this for one `Faction` (16 slots, 12 allegiances, two `float3`s,
  two refs) and read it back from the `.cc`.
- Verification: `legacy-census` per-tag counts (`[P5]`: 0:13, 1:51, 2:25, 3:3,
  13:12, 17:3, 29:4, 30:1, 31:18, 32:37; 12 name files); `import` prints
  per-type counts and every unresolvable `CultRecordRef` (zero, except
  `Faction.BossHull` `Guid.Empty`, reported as unset); `census`, `factions`,
  `hardpoint-fit` equal their Cut 0 captures.
- Same commit: delete `GameData\AetherDB.msgpack`, `GameData\NameFile\`, the 32
  empty per-type folders. Keep `GameData\KeyboardLayouts` for Cut 10.
- Commands: `dotnet run --project tools\AetherDb -- legacy-census`; `-- import`;
  the three comparisons.
- Soul: counts match; `Aetheria.cc` deserializes with `DeserializeSnapshot`
  and every record's schema id is in its catalog; no legacy file remains;
  `git check-attr filter GameData/Aetheria.cc` reports `lfs`; one commit.

### Cut 10. Runtime cutover, importer removal, docs

- Repo/branch: same, after Cut 9.
- Files: `Assets\Scripts\Gameplay\ActionGameManager.cs`, `UI\MainMenu.cs`,
  `UI\InputScreen\InputDisplayLayout.cs`, `ServerShared\Galaxy.cs`,
  `SavedGame.cs`, `PlayerSettings.cs`, `Zone.cs`, `.gitignore`,
  `tools\AetherDb\Import.cs`, `docs\cultcache-migration-target.md`,
  `docs\three-gates-scope.md:61-68`.
- Deletes first: the catalog bootstrap (`ActionGameManager.cs:49-57`);
  `SavePlayerSettings`/`SaveState` (`:76-79, 240-249`); `SaveLoadout`
  (`:233-236`); `SaveZone` (`:1093-1094`); the settings reader (`:65-72`); the
  layout writer/reader (`InputDisplayLayout.cs:495-501, 88-91`); the
  `GameData/PlayerSettings.msgpack` `.gitignore` line (replaced by
  `GameData/run.cc`, `GameData/player.cc`); `Import.cs` and its two commands;
  the "loading writes" paragraph (`three-gates-scope.md:61-68`).
- New behavior:
  - `ActionGameManager.Awake`: `Cache = AetheriaStores.Open(catalog, runPath:
    File.Exists(run) ? run : null, playerPath: player)`; then `if
    (Cache.GetGlobal<PlayerSettings>() == null) { Cache.UpsertAsync(new
    PlayerSettings()); Cache.FlushAsync(); }`.
  - `MainMenu`: Resume shown when a run store is attached and
    `GetGlobal<SavedGame>()` is non-null; a run store without `SavedGame` logs
    `"run store has no SavedGame; refusing to resume"` and shows New Game only.
  - `BeginRun()`: dispose any run store, delete `run.cc`, reopen with the run
    path, generate the galaxy upserting `SavedGame`, `SavedZone`s, orbits and
    bodies as created (`Zone.AddOrbit`, `Zone.cs:120, 265`), then `SaveRun()`.
  - `SaveRun()`: `Cache.FlushAsync()` (writes only dirty stores); the only run
    writer; called from quit (`:238`), wormhole entry (`:611`), settings Back
    (`MainMenu.cs:237`). Not a `Commit`: the run's durability unit is the
    whole run store, whose single-file flush is already one atomic replace of
    every record it holds (`SingleFileBackingStore.PushAll`, `CC:2638-2657`),
    and orbits and bodies are created across the whole generation and play
    session, not inside one code block. A batch would add a staging copy of
    state the store already stages, for no extra atomicity.
  - `Die()`: no `SaveRun`; the run store is disposed and `run.cc` deleted.
  - `InputLayout` records live in the player store keyed by layout name; a
    rebind upserts and flushes.
  - `Galaxy(CultCache, SavedGame, ...)` resolves `CultRecordRef<Faction>`.
- Tests (`tests\Aetheria.Shared.Tests`): `SaveRunWritesOnlyTheRunStore`
  (catalog and player hashes unchanged), `RunRoundTrips`,
  `DeleteRunLeavesCatalogAndPlayerByteIdentical`, `PlayerSettingsIsCreatedOnceByBoot`.
- Docs: `cultcache-migration-target.md` status "done" with the commit range.
- Commands: headless build and tests as Cut 8; batchmode compile; the operator's
  play smoke (new game, one wormhole, quit, resume, die; `run.cc` gone,
  `player.cc` and `Aetheria.cc` hashes unchanged).
- Soul: the four tests; the play smoke; `rg "PlayerSettings.msgpack|\.zone\b|\.loadout|KeyboardLayouts|Import\.cs|legacy-census" Assets tools`
  empty; `rg -c "SaveRun\(" Assets` shows one definition and three callers;
  `git ls-files GameData` lists `Aetheria.cc` only.

Steps needing Unity: 6 (CultLib's Unity project), 8 and 10 (batchmode compile
by the agent; UPM acceptance and the play smoke by the operator).

## 5. Subtraction ledger

Lines are C# unless noted; sizes are measured where a file is named, bounded
estimates otherwise.

| Cut | Removed | Added | Targets, dependencies, schemas |
|---|---|---|---|
| 1 | 0 | ~150 doc, ~160 test | 0 |
| 2 | `CultManagedDocument.cs` −355 (SoA parked at a tag, managed doc); `CC` −~670 (`///` 461, wrappers/unused/`soft`/subjects ~210); `CultCacheMessagePack.cs` −~60; `DMS` −~380 (legacy formats, filter, probes, `///`); `CultDocumentMessagePackSerialization.cs` −~100 (catalog helpers, `///`); `CND` −~60; deleted tests ~−450 | ~40 (`Loaded/Unloaded`, `NoWarn`), ~30 (`parked-features.md`) | public surface −20 members; 1 tag; 0 targets |
| 3 | ~95 (2.1/2.2 deletion lines) + ~230 (ambient transaction machinery) | ~150 impl (routing, globals, read-only, lookups) + ~70 (`CultCacheBatch`, `Commit`) + ~150 (conditions, request, outcome, `TryCommit`, single-file lock and conditional commit ~60, directory conditional commit ~40, `StoredAt` bump), ~720 test, ~30 interop peer, 1 small console project for the two-process race | +`AddBackingStore(store, Type[])`, +`IsReadOnly`, +`ReadOnly`, +`Commit`, +`TryCommit`, +`CultCacheBatch`, +`CultCommitRequest`, +`CultCommitOutcome`; −ctor overload, −`MaterializeMissingGlobals`, −`ContainsDurableRecord`, −`PullOnOpen`, −`ExecuteTransactionAsync` ×2; −`AsyncLocal`, −`SemaphoreSlim`; +1 lock-file sidecar per single-file store |
| 4 | 1 | ~80 impl, ~110 test, 2 csproj lines | +1 assembly attribute; MessagePack's generator no longer flows to consumers |
| 5 | TS ~18, Rust ~30, Python ~20 | TS ~12 + ~40 test, Rust ~15 + ~40 test, Python ~10 + ~30 test | three version bumps |
| 6 | ~120 (reflection bridge, fallback) | ~300 editor, 1 attribute | Studio `1.0.0` -> `1.1.0`, +1 package dependency |
| 7 | 2 doc lines | 2 doc lines, rebuilt DLLs | 4 tags |
| 8 | 1,771 − ~305 kept + 30,088 + 1,963 + ~60 ≈ **33,600** | ~40 `AetheriaStores`, ~150 drawers, ~180 tests, ~40 props/targets, ~400 attribute/reference edits, ~120 AetherDb ≈ **930** | −1 vendored MessagePack, −1 JsonKnownTypes, −1 asmdef; +2 UPM packages, +2 ProjectReferences, +1 test project; 20 schemas replace 1 union |
| 9 | ~15, 2 legacy data files + 12 name files + 32 folders | ~280 importer (deleted in Cut 10), 1 `.cc`, 1 `.gitattributes` line | 0 |
| 10 | ~120 + ~280 (importer) + 8 | ~110, ~120 tests | −4 private file formats |

Expected net: CultLib **−1,650** lines of source and −450 of tests in Cut 2,
then −325/+370 source and +720 tests in Cut 3 and +80/+110 in Cut 4: the two
Caching projects end near 3,150 lines with `CultCache.cs` near 1,000, against
5,259 and 2,704 today, and C# gains the conditional commit Rust already has;
the siblings shrink; Aetheria about **−33,000** lines, two vendored
dependencies and four private formats gone.

Proposed follow-ups, outside this migration: remove `soft` from
`CultMesh.FlushAsync`/`CultNetLocal.FlushAsync` and its four external call
sites; collapse `FlushOnDispose`/`StoreFlushOnDispose` into one flag; package
the generator without the empty `GameCult.Caching.MessagePack.Analyzers`
project; strip `///` from `CultDocumentContracts.cs` and
`CultGeneratedDocumentMetadata.cs` (180 lines untouched by these cuts); unify
the global key across runtimes (changes bytes); batch commit and conditional
commit in `CultLib\packages\cultcache-ts` and a cross-type all-or-nothing and
conditional batch in `CultLib\packages\cultcache-py`; a probe that a Rust
`fs2` lock and a C# `FileShare.None` open on one `.lock` file exclude each
other, before any store is shared across runtimes.

## 6. Risks and rejected paths

Risks:
- Unity call sites are found by grep and proven by batchmode; expect one
  fixup round after each compile in Cuts 8 and 10.
- MessagePack 2.x (vendored) to 3.1.7: depth is not a risk (deepest payload 7
  against a 500 limit `[P5]`); `[Union]` on abstract bases, nullable
  formatters and `keyAsPropertyName` types are the unknowns, surfaced by the
  round-trip tests in Cuts 8-10.
- MessagePack 3's generator fails on ref-keyed maps `[P5]`; after Cut 4 it no
  longer reaches any consumer of `GameCult.Caching.MessagePack` or
  `GameCult.Networking` `[P6]`. A consumer that references the `MessagePack`
  package directly still gets it and must not use ref-keyed maps until the
  upstream bug is fixed.
- Attach-is-hydration: a caller that attached, mutated, then pulled (none
  found) now throws at attach; a caller that attaches an untyped store before a
  routed one (none exists; every caller attaches one store) throws at the
  second attach; explicit pulls after attach are harmless re-pulls.
- Cut 3 replaces the ambient transaction with an explicit batch. Any consumer
  that relied on reading its own staged records mid-transaction would break;
  none exists (the only caller was a tests-only CultNet wrapper), and every
  sibling consumer passes explicit batches. Cut 2 parks SoA rather than
  retracting it.
- Conditional commit identity is `(schemaId, storedAt)`, not payload bytes;
  it is sound only with the strictly-increasing `StoredAt` rule, which a
  writer outside CultLib (another runtime writing the same file) need not
  obey. Rust compares whole envelopes. Until the cross-runtime lock and
  identity are probed together, a store is written by one runtime.
- The single-file lock file is a sidecar next to every single-file store;
  a store on a read-only directory cannot commit conditionally (nor could it
  flush), and a stale lock file after a crash is harmless (`FileShare.None`
  is released with the process).
- The conditional-commit design is the one part of this map not
  pre-established by a probe (`[P7]` failed, section 7). Cut 3's tests carry
  that burden; if `(schemaId, storedAt)` proves insufficient there, the
  fallback is comparing payload bytes retained per record in the store's
  staging, which costs memory, not bytes on disk.
- Any test or probe that spawns processes must launch an explicit host with
  a child role set by an environment variable checked first, cap every loop,
  and fail if a child would spawn: the `[P7]` fork bomb came from launching
  the apphost with the dll path as `args[0]`.
- Deleting the v2/v3 directory formats refuses any store nobody found; if one
  exists it fails loudly with its format string and is rebuilt from source.
- `ReactiveProperty` fields reachable from a document break without the
  vendored resolver; none found; Cut 8's Soul grep confirms.
- Slot type changes are hard rejects (`CC:947-1027`); Aetheria field edits need
  a new slot or a version bump.
- The C# global key differs from TS/Python; cross-runtime `.cc` byte parity is
  a pre-existing gap.

Rejected paths:
- Mirrors or replication, anywhere.
- A `_pendingDefaults` set or deferred-admission queue (first pass); a
  `_hydrated` flag; materializing globals after hydration in `OpenAsync`.
- A mutable `Options` static with a set-once `ConfigureResolvers` (first pass).
- Keeping managed documents or legacy directory formats because tests cover
  them. (SoA is parked, not rejected.)
- Deleting atomic commit as a capability (second pass, retracted by the
  operator: multi-record commits recur in Ghostlight and Epiphany work); the
  ambient `AsyncLocal` implementation is what goes, not the primitive.
- Ambient in-flight visibility inside a batch: no consumer in any runtime
  reads its own staged records; Rust, Ghostlight and Odin all pass explicit
  batch values.
- Seven separate compare-exchange entry points mirroring Rust's: two
  conditions on the one batch cover all of them.
- Payload-byte identity for conditions: the cache does not retain bytes and
  the in-memory instance may have been mutated since it was observed;
  `(schemaId, storedAt)` with a strictly increasing `storedAt` is the same
  fact at no memory cost.
- Stripping MessagePack's generator in each consumer's build: the analyzer
  arrives through CultLib's reference, so CultLib owns the exclusion.
- Per-field `[MessagePackFormatter]` attributes; `Dictionary<string, float>`
  instead of ref-keyed maps; a list of pairs.
- A `storeId` in the file header; a save converter; a `DatabaseEntry` `ID`
  shim; union tags for `RequireBehavior`;
  a JSON intermediate or `extern alias` for the import; the GameCult generator
  in `Aetheria.Shared`; persisting agent tasks.

## 7. Probes

All probes live under
`C:\Users\Meta\AppData\Local\Temp\claude\F--Projects-Aetheria\2a6aec4d-7cba-481c-8586-be54662389a5\scratchpad\`,
reference CultLib `c2a9a6e` by project, and were run with
`dotnet run --project <dir>\Probe.csproj -c Debug` on 2026-09-13.

| Id | Probe | Result |
|---|---|---|
| P1 | `cc-global-probe`: seeded file; `new CultCache()` (globals on) -> attach -> pull -> flush, versus `OpenAsync` | `new CultCache()` order: after ctor `entries=1 cacheDirty=True`; after attach `storeDirty=True`; `note loaded: NO`; after flush `note still on disk: False, file bytes changed: True`. `OpenAsync`: `note loaded: persisted`; `note still on disk: True, file bytes changed: False`. |
| P2-A | `cc-mechanics-probe` (global-free registry): attach A, pull, attach B, flush, reopen B | `after pulling A: note loaded=True`; `after attaching B: B.IsDirty=True cache.IsDirty=True`; `after flush with no mutation: B file exists=True B records=1 keys=note`; `reopening B alone: note present=True`. |
| P2-B | same: transaction with two stores | `InvalidOperationException: A CultCache transaction requires zero or one durable backing store; ...` |
| P2-C | same: a store whose `Push` throws | `upsert threw InvalidOperationException`; `cache.Get(key) != null = True, cache.IsDirty=False`. |
| P2-D | same: base-typed lookups and watch | `Watch<Base> events=0 Watch<Leaf> events=1`; `GetByName<Base>(alpha) null=True GetByName<Leaf>(alpha) null=False`; `GetGlobal<GlobalBase>() null=True GetGlobal<GlobalLeaf>() null=False`; `GetAll<Base>().Count=1`. |
| P2-E | same: `UpsertAsync<Gear>(new Weapon())`, flush, inspect | `schema=probe.gear payloadArrayLength=2`. |
| P3 | `cc-cultnet-probe`: `CultNetDatabase.PutAsync<Gear>(key, new Weapon())`, `db.Watch<Gear>()`, flush, reopen | `persisted schema=probe.gear; published change schema is Gear=True; in-memory type=Weapon`; `reopen: Get<Weapon> null=True; Get<Gear> type=Gear`. |
| P4 | `cc-generated-probe` (GameCult generator, `EmitCompilerGeneratedFiles`) | `generated serializer present=True deserializer present=True`; emitted `...g.cs:53, 65`: `var options = global::GameCult.Caching.MessagePack.CultDocumentMessagePackSerialization.Options;`, members via `options.Resolver` at `:57-58, 71, 75`. |
| P5 | `cc-import-probe`: structural read of `GameData\AetherDB.msgpack` and one name file; depth; one `Faction` slot rewrite; `.cc` write and read-back; depth limit | `167 records; per tag: 0:13, 1:51, 2:25, 3:3, 13:12, 17:3, 29:4, 30:1, 31:18, 32:37`; `max nesting depth = 7 (tag 2); UntrustedData limit = 500`; `Australia.msgpack: outer array 2, tag 9, payload slots 3`; `Faction payload slots=16: 0:Binary(len16) 1-4:String 5:Map(n0) 6:Nil 7:Array(n3) 8:Array(n3) 9:Binary(len16) 10:Binary(len16) 11:Integer 12:Map(n12) 13-15:Integer`; stock options: `TypeAccessException: No hash-resistant equality comparer available for type: GameCult.Caching.CultRecordRef`; with the security subclass: `key=bbd619ed-… Name=Adrasteia Geoname=95c298ae-… Allegiance=12`; `reopened from .cc: … schema=aetheria.faction … Allegiance=12 payloadSlots=16`; `depth 100: ok; 499: ok; 600: MessagePackSerializationException`. Build: with MessagePack's generator active, `CS0426: The type name 'GameCult' does not exist in the type 'GeneratedMessagePackResolver'` (emitted `case 0: return new global::MessagePack.GeneratedMessagePackResolver.GameCult.Caching.CultRecordRefFormatter<…>()`); `ExcludeAssets="analyzers"` on direct references to `MessagePack`, `MessagePack.Annotations`, `MessagePackAnalyzer` does not remove it; an `Analyzer Remove` target `BeforeTargets="CoreCompile"` does. |

| P6 | `cc-analyzer-probe`: scratch library `Lib` (netstandard2.1, `MessagePack` 3.1.7, a generic `Ref<T>` struct with a custom formatter and resolver, standing in for `GameCult.Caching.MessagePack`) and a consumer `App` declaring `Dictionary<Ref<Doc>, float>`; four library shapes built with `dotnet build App\App.csproj -c Debug -p:<variant>` | default: `CS0426: The type name 'Lib' does not exist in the type 'GeneratedMessagePackResolver'`; `ExcludeAssets="analyzers" PrivateAssets="analyzers"` on `Lib`'s `MessagePack` reference: same error; `[assembly: MessagePackKnownFormatter(typeof(RefFormatter<>))]` in `Lib`: same error (emitted `case 0: return new global::MessagePack.GeneratedMessagePackResolver.Lib.RefFormatter<global::App.Doc>();`); `<PackageReference Include="MessagePackAnalyzer" Version="3.1.7" PrivateAssets="all" />` in `Lib`: `Build succeeded.` |
| E1 | grep evidence (no code run) for the batch-commit shape | TS `cultcache-ts\src\*.ts`: no batch, transaction or atomic member; Python `CultLib\packages\cultcache-py\src\cultcache_py\cache.py:227-251` `put_envelopes` only, called from `CultLib\packages\cultnet-py\src\cultnet_py\replication.py:98`; Rust cache-level `put_prepared_batch` (`CultLib\packages\cultcache-rs\src\lib.rs:2162-2201`): no caller in `CultLib\packages` or in Odin, Idunn, Epiphany, CodexConnector, Ghostlight, Muninn, Ratatoskr; Rust store-level `compare_exchange*`/`compare_and_swap*`/`delete_batch_if_unchanged` (`CultLib\packages\cultcache-rs\src\lib.rs:397-800`): Odin 12 sites, Idunn 31, CodexConnector 1, Ghostlight `ghostlight-dungeon\src\app_session.rs:119, 371` (+3 tests) and `Ghostlight\crates\ghostlight-dungeon\src\world\consumer.rs:788` ("a malformed or stale batch commits nothing"); Epiphany's crates: none. Every call passes an explicit expected/replacement batch; none reads staged state mid-commit. |

| P7 | `cc-cas-probe`: conditional commit over the existing single-file snapshot format, implemented in the probe (identity `(schemaId, storedAt)`, `<file>.lock` with `FileShare.None`, temp then `File.Replace`), meant to race two child processes on one file | **UNVERIFIED, no result recorded.** The race step launched `Environment.ProcessPath` (the apphost) with the dll path as `args[0]`, so no child took the `race` branch; each re-ran the parent and spawned two more, an exponential fork bomb that locked the workstation three times. Its output was never captured (buffered behind `tail`), so not even the single-process identity, per-entry and snapshot observations that ran before the spawn can be cited. The coordinator patched the source (environment-variable child role checked first, explicit `dotnet <dll>` host); it has not been re-run and must not be run in this pass. Every `(schemaId, storedAt)` and lock claim in 2.2 is therefore design, proven by Cut 3's `ConditionalCommitTests.cs`, not by a probe. |

Not established by running code, marked as design: the routed C# cache
itself (Cut 3's tests are its proof); the Studio's reflective struct path over
Unity.Mathematics (Cut 6's operator check); how AquaSynth reads its
`[CultGlobal]` types (Cut 3's Soul step reports it here).
