# CultMesh Cross-Runtime Primitives

Aetheria is the shakedown cruise for CultMesh. The Rust rebuild should not turn every useful idea into Aetheria-only glue. If a pattern helps any typed Verse daemon expose state, operations, queries, native views, authority, or UI surfaces across runtimes, it belongs in the shared CultMesh/CultLib layer. CultMesh should be expansive enough that application code feels like it is holding managed native state, while the runtime handles locality, synchronization, authority, codecs, and cache layout.

The bar is deliberately high: CultMesh should feel managed, cozy, and almost unfairly ergonomic to the developer, while the runtime silently chooses the fastest valid path underneath. In-process calls, shared CultCache slabs, native slices, IPC, remote transport, and browser/WASM views are locality choices, not different application APIs. The developer writes against typed Verse concepts; CultMesh supplies the plush handles, reactive wrappers, generated operations, query surfaces, native views, and diagnostics that make those concepts feel obvious.

The promotion rule is simple: if a helper would make Aetheria, Brokkr, Odin,
Bifrost, Eve, Ymir, or VoidBot nicer in the same way, it is not an Aetheria
helper. Hoist it into the shared cross-runtime CultMesh/CultLib layer first,
then let Aetheria consume it as ordinary infrastructure. Aetheria may define
domain nouns and policies; CultMesh owns the managed-feeling primitive shape
that makes those nouns feel native from C#, Rust, TS, Unity, browser/WASM, and
tooling hosts.

This is not a minimalist transport library. CultMesh should grow the shared
abstractions that make daemon state feel intimate: typed state pointers,
reactive wrappers, query handles, operation handles, native slice descriptors,
authority claims, locality routing, schema-generated handles, UI bindings,
diagnostics, and deterministic math/query primitives. "Primitive" does not
only mean a small value type; it means any reusable cross-runtime developer
affordance that can be hoisted into the shared library layer. The goal is a
managed, cozy developer surface that reads like ordinary domain code and
compiles down to the fastest safe path the runtime can prove.

## Design Bar

1. Typed state is the default. User code should hold handles, pointers, operations, and query objects, not record keys, schema ids, slot numbers, or raw payload maps.
2. The same semantic surface works in Rust, TS, Unity/C#, browser/WASM, Eve/CultUI, and native tools.
3. Locality is invisible until the developer asks for diagnostics. Co-located runtimes get shared memory or direct calls; remote runtimes get transport; browser runtimes get WASM-compatible views.
4. Authority is a first-class Verse primitive. Policies, leases, runtime roles, claims, and future quorum hooks should be reusable across daemons.
5. UI surfaces can contain typed pointers into daemon state. The UI runtime resolves, watches, invalidates, and invokes typed operations automatically.
6. Native views expose zero-copy or near-zero-copy slices when safe, with the same state semantics as remote query/subscription fallbacks.
7. Schema evolution is part of the developer experience. Bindings and migration manifests come from one source instead of hand-maintained slot maps.

## Canonical State Before Projection

CultMesh should make one canonical typed document feel native everywhere. If a
feature's game state is `AetheriaRuntimeFooDocument`, the daemon, Unity,
Electron, browser, tools, and future Rust clients should all hold managed
handles to that document type. Authority, prediction, debouncing,
reconciliation, quorum, locality, and transport are handle/runtime behavior,
not reasons for each application to create a private "truth" object and then a
client projection copy.

A projection is a different state shape, not the normal visibility mechanism.
Use projected documents or query surfaces for hidden-information filtering,
derived aggregation, viewport/windowing, SoA/native memory layout, lossy UI
summaries, Eve/CultUI surfaces, or temporary compatibility bridges. Do not
project merely because a client needs to read daemon-owned state.

This rule is cross-runtime. C#, Rust, TypeScript, Unity, browser/WASM, and tool
hosts should all express the same intent: grab a typed document/collection/query
handle, read it for display, or mutate/submit through it when authority policy
allows. CultMesh supplies the managed access conventions and generated handles
that make that simple.

## Ownership Boundary

| Belongs in CultMesh/CultLib | Belongs in Aetheria |
| --- | --- |
| Typed document handles, record routing, schema ids, migrations, codecs. | Aetheria world, zones, entities, inventory, stats, equipment, factions, Starbridge scenario concepts. |
| Typed operation handles, receipts, idempotency, authority metadata, prediction hooks. | Pilot, RTS, refit, trade, interaction, construction, combat, and editor operation vocabulary. |
| Typed query surfaces, query planning, locality routing, subscription lifetimes. | Object viewport, gravity viewport, selected object, inventory, stat, station, encounter, and tactical map queries. |
| Reactive state pointers and UI binding semantics. | Aetheria Eve/CultUI surface content and domain-specific panel layout. |
| Native slice descriptors, memory safety contracts, dirty ranges, runtime adapters. | Aetheria render columns, physics body columns, inventory/stat column definitions. |
| Authority primitives: policy modes, leases, claims, runtime roles, diagnostics. | Which Aetheria operation kinds require which claims and which runtime cares most about each thing. |
| CultMath primitives: `Vec2`, `Vec3`, `Rect`, `Circle`, `Sphere`, transforms, deterministic scalar helpers. | Aetheria coordinate conventions, body meanings, sensor ranges, gravity brush semantics. |
| Provider discovery and interface binding conventions for Odin/Bifrost/Eve. | Aetheria provider advertisements, operation/query catalogs, and surface ids. |

## Primitive Set

First implementation footholds now exist in CultLib:

- `E:/Projects/CultLib/src/GameCult.Mesh/CultMeshPrimitives.cs` defines typed operation handles, query surfaces, state pointers, route hints, authority claims, and native slice descriptors.
- `E:/Projects/CultLib/src/GameCult.Mesh/CultMeshPrimitives.cs` also exposes fluent operation/query context builders, with `CultMesh.OperationContextFor(...)` and `CultMesh.QueryContextFor(...)` entrypoints. Application code should read like a typed Verse call, not like manual transport setup.
- `E:/Projects/CultLib/src/GameCult.Geometry/CultGeometryPrimitives.cs` defines shared `CultVec2`, `CultVec3`, `CultRect`, `CultCircle`, and `CultSphere` values for query and physics contracts.
- `E:/Projects/CultLib/packages/cultmesh-ts/src/index.ts` now exposes TS `CultMeshVec2`, `CultMeshRect`, `CultMeshViewportRequest`, `CultMeshQuerySurface`, `CultMeshQueryContext`, `CultMeshOperationHandle`, `CultMeshOperationContext`, `CultMeshOperationPayload`, `CultMeshDocumentHandle`, `CultMeshDocumentCatalog`, `CultMeshCollectionHandle`, `CultMeshStatePointer`, `CultMeshStateBindingDescriptor`, `CultMeshOperationBindingDescriptor`, `CultMeshOperationInvocationDescriptor`, native slice descriptors, authority claims, route hints, fluent context builders, and handle helpers like `CultMesh.rectFromBounds(...)`, `CultMesh.viewportRequest(...)`, `CultMesh.query(...)`, `CultMesh.querySource(...)`, `CultMesh.operation(...)`, `CultMesh.document(...)`, `CultMesh.documents(...)`, `CultMesh.collection(...)`, `CultMesh.operationPayload(...)`, `CultMesh.statePointer(...)`, `CultMesh.stateBinding(...)`, `CultMesh.operationBinding(...)`, `CultMesh.operationInvocation(...)`, `CultMesh.nativeSliceView(...)`, `CultMesh.operationContextFor(...)`, and `CultMesh.queryContextFor(...)`.
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimePlayerSettingsSurfaceBuilder.cs` and `GameCult.Mesh.EveSurfaceDocument` now expose component state bindings as CultMesh-owned portable binding records. Eve no longer owns a parallel live binding DTO, and Aetheria no longer owns a parallel persisted Eve surface state. Aetheria can build runtime surface source data, then publishes the shared CultMesh surface document before renderers lower it.
- `E:/Projects/Eve/packages/org.gamecult.eve.surface/Runtime/EveSurfaceDocument.cs` also exposes component `EmbeddedDocuments` as first-class CultUI slots. Aetheria runtime surfaces mirror that shape with `AetheriaRuntimeEmbeddedDocumentSlot`, so daemon-owned UI can compose nested synced surfaces such as inventory dropdowns without a Unity-only model, facade, or projector.
- Aetheria and Eve live command templates now carry `CultMeshOperationBindingDescriptor`. Eve command requests carry `CultMeshOperationInvocationDescriptor` plus `CultMeshOperationPayload`, so renderer click/change events preserve operation id, schema, route hint, idempotency, and scalar field reads as shared CultMesh metadata. Legacy `command`, `label`, `transport`, and string payload fields remain compatibility projections and persisted DTO fields, but the live API no longer exposes raw command-string/dictionary constructors; controls point at typed CultMesh operations and renderers build requests from shared CultMesh primitives.
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationsClient.cs` is now a typed operation handle surface that returns `CultMeshOperationReceipt` from semantic verbs such as `SetMoveVector`, `SetTarget`, `DockNearest`, and inventory transfers. The lower-level daemon command envelope remains an internal transport wrapper, but public client code should receive shared CultMesh receipts, not Aetheria-specific command envelopes.
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaUi.cs` applies the same rule to Eve/CultUI commands. Unity UI and Eve presenter code should call `AetheriaClient.Ui` and receive `CultMeshOperationReceipt`; `AetheriaRuntimeEveCommandEnvelope` remains a bridge/document persistence detail for the Verse command boundary, not the shape application code leans on.
- `AetheriaRuntimeEveCommands` is internal now. It may help smoke tests and internal bridge code manufacture persisted command documents, but it is not the public renderer/client API. Public callers use `AetheriaClient.Ui`, and the shared receipt is the public outcome type.
- `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonRenderQueries.cs` now exposes daemon render gravity/body query overloads around `CultMath.rect` in Aetheria XY space. `AetheriaRuntimeXzRect` remains only as a Unity legacy adapter.
- `E:/Projects/CultLib/src/GameCult.Caching.MessagePack/DirectoryMessagePackBackingStore.cs` now recovers readable schema-stamped cold records when the hot directory manifest is missing the record's catalog entry. Aetheria hit this with persisted Verse authority policy state; the fix belongs in CultCache because durable Verse state should feel managed and resilient across runtime/schema refreshes, not like hand-maintained manifest bookkeeping.
- `E:/Projects/CultLib/packages/cultcache-ts/src/single-file-messagepack-backing-store.ts` and `E:/Projects/CultLib/packages/cultcache-rs/src/lib.rs` now mirror the same schema-stamped recovery for single-file MessagePack snapshots. Their cache registries resolve recovered schema names and normalize envelopes back to registered public document types, so browser/Electron/Rust clients can share durable Verse state without depending on a stale hot catalog.
- `E:/Projects/CultLib/packages/cultcache-py/src/cultcache_py/stores.py`, `E:/Projects/CultLib/packages/cultcache-py/src/cultnet_py/replication.py`, and `E:/Projects/CultLib/packages/cultcache-ts/src/cult-cache-inspector.ts` now carry the same primitive into Python stores, raw CultNet replication helpers, and tooling inspection. A stale schema id should be a recoverable runtime detail when the cold payload is schema-stamped, not a client-visible failure mode.

### Nested CultUI Surface Discovery

Nested CultUI surfaces are a shared Eve/CultMesh feature, not an Aetheria-only
inventory workaround. Discovery starts in `E:/Projects/Eve/tools/parity/parity-manifest.json`,
which lists the canonical `embedded-surface` fixture and the active runtime
claims for web, Flutter desktop/Linux/Android, iOS/UIKit, Android/Kotlin, Unity
UI Toolkit, and Rust document sync. A renderer that claims CultUI GUI parity must
advertise `embeddedDocuments` there and require the `embedded-surface` fixture.
Every target runtime must be named in that matrix as tested, pending, or
unsupported; an omitted runtime is a broken discovery story, not a harmless
documentation gap.

Run the shared evidence from the owning repos:

```powershell
cd E:\Projects\Eve
node --test web\eve-dsl.test.mjs
powershell -ExecutionPolicy Bypass -File .\scripts\run-parity-harness.ps1

cd E:\Projects\CultLib\packages\cultnet-rs
cargo test rust_preserves_cultui_embedded_surface_slots_through_typed_document_sync
```

When Flutter is installed, also run from `E:/Projects/Eve/flutter/eve_parity`:

```powershell
flutter test --plain-name embedded_surface_fixture_contract
```

Aetheria's local proof stays thin: build the Unity package and run
`Aetheria.State.Verify`. The verifier should prove Aetheria consumes the shared
contract through `AetheriaRuntimeSurfaceDocuments`, `AetheriaRuntimeEmbeddedDocumentSlot`,
and the Eve UI Toolkit lowerer, not through local projectors, facades, or
adapter-shaped child state.

### Typed Document Handles

Current leak:

```ts
await node.putRaw(recordKey, schemaId, msgpackBytes);
const frame = decodeFrame(await node.get("daemon:aetheria.frame.latest.v1"));
```

Desired shape:

```ts
const run = await verse.aetheria().currentRun().watch();
const frame = await run.frame().latest();
```

The handle owns record keys, schema ids, codec choice, migration readers, cache hydration, and subscription invalidation. The application code sees the typed value and its stable semantic location in the Verse.

### Typed Operation Handles

Current leak:

```ts
await client.setMoveVector({
  actorEntityKey,
  directionX: input.x,
  directionY: input.y,
  scalar: 1,
});
```

Desired shape:

```ts
await verse
  .aetheria()
  .entity(actorEntityId)
  .pilot()
  .move({ x: input.x, y: input.y });
```

CultMesh should generate operation handles from schema. The handle carries payload shape, authority claim metadata, routing, idempotency, receipts, diagnostics, and optional prediction hooks. The runtime decides whether this is a local direct call, a shared-slab write, a network operation, or a WASM host call. The RTS binding generator now emits `createAetheriaRuntimeRtsOperationHandles(...)` for `setMoveVector` and `setTarget`; the Electron client consumes those generated TS `CultMesh.operation(...)` handles while preserving the current daemon command document as an internal transport detail.

### Typed Query Surfaces

Current leak:

```ts
const frame = await client.latestFrame();
const objects = projectObjectsViewport(frame, rect, controlledEntityIds);
```

Desired shape:

```ts
const objects = await verse
  .aetheria()
  .zone(zoneId)
  .objects()
  .visibleTo(controlledEntityIds)
  .within(rect)
  .watch();
```

Queries are derived-state surfaces, not bespoke HTTP endpoints or local helper layers. CultMesh should own query identity, parameters, caching, invalidation, locality routing, and remote execution. A co-located daemon can execute directly over slabs; a remote peer can execute through the Verse; a browser can consume the same query shape through generated TS/WASM bindings. The RTS binding generator now emits response document types, `createAetheriaRuntimeRtsQueryHandles(...)` for every RTS read surface, renderer `aetheria-rts-contract.ts`, `AetheriaRtsIpcChannels`, the preload bridge, and `registerAetheriaRtsIpcHandlers(...)`. Those generated read handles are backed by `CultMesh.query(...)` with explicit daemon frame, health, authority, and Starbridge source descriptors. The RTS client passes its local publication route into the generated handles once, then uses automatic query contexts so the query handle owns locality defaults. Because `CultMeshQuerySurface` preserves `sources` and `routeHint`, the RTS client exposes `queryDiagnostics()` directly from generated handle metadata instead of maintaining a parallel diagnostic table. Electron and browser clients consume generated contracts instead of duplicating query/response shapes, IPC channel names, handler registration, or viewport methods as the primary abstraction.

### Reactive State Pointers

Current leak:

```csharp
using var client = await AetheriaClient.OpenAsync(statePath, runtimeId);
var resolver = client.State.CreateEveSurfaceCultMeshStateRefResolver();
var selected = resolver(surface.StateRef);
```

Desired shape:

```ts
surface.panel("Selection", {
  entity: verse.aetheria().selection().entity().pointer(),
  actions: verse.aetheria().selection().entity().operations(),
});
```

Eve/CultUI props should be allowed to hold typed state pointers and operation handles. The UI runtime resolves them, subscribes to them, handles unavailable state, invalidates stale values, and invokes typed operations without custom per-daemon adapters.

### Native Slice Views

Current leak:

```csharp
var descriptor = await client.GetSoaViewAsync();
var map = MemoryMappedFile.OpenExisting(descriptor.MemoryMapName);
```

Desired shape:

```csharp
using var view = await verse.Aetheria()
    .Zone(zoneId)
    .RenderView()
    .AsNativeArrays();
```

CultMesh/CultCache should define native view descriptors, dirty ranges, schema fingerprints, lifetimes, and safety rules. Unity gets `NativeArray<T>`-friendly views. Rust gets slices. Browser/WASM gets typed array views when co-located or a compatible copied view when remote. The semantic surface stays the same.

### Authority Primitives

Current leak:

```csharp
var decision = AetheriaRuntimeAuthorityRouter.Authorize(policy, command, runtimeId, leases);
```

Desired shape:

```rust
let receipt = verse
    .aetheria()
    .entity(actor)
    .pilot()
    .move(vec2)
    .with_authority(runtime.claims().local_responsiveness())
    .await?;
```

Near-term shared-library sugar already exists in C# and TS:

```csharp
var context = CultMesh.OperationContextFor("unity-raven")
    .Claim("pilot-control", shardId: "zone:raven")
    .Route(CultMeshLocalityKind.SharedMemory, "co-located Verse")
    .Idempotency("move:raven:1")
    .Build();
```

```ts
const context = CultMesh.operationContextFor("browser-starfire")
  .claim("simulation-authority", { shardId: "zone:starfire" })
  .route("wasm", "browser-local query")
  .idempotency("move:starfire:1")
  .build();
```

Authority is not topology. CultMesh should provide reusable policy modes, claim kinds, leases, runtime roles, denial reasons, receipts, and diagnostics. Aetheria configures which operation families require which claims. Future server-authoritative, trusted distributed, lease, browser-host, and quorum modes should use the same primitive vocabulary.

### Geometry And Math Values

Current leak:

```ts
viewport({ minX, minY, maxX, maxY });
```

Desired shape:

```ts
const rect = CultMesh.rect(CultMesh.vec2(x0, y0), CultMesh.vec2(x1, y1));
await zone.gravity().influences().intersecting(rect);
```

CultMath/CultMesh should provide deterministic cross-runtime primitives for vectors, rects, circles, spheres, transforms, scalar tolerances, and spatial query inputs. In CultLib C# this starts as `CultVec2`, `CultVec3`, `CultRect`, `CultCircle`, and `CultSphere` under `GameCult.Geometry`; in TS this starts as `CultMeshVec2`, `CultMeshRect`, and `CultMeshViewportRequest` in `cultmesh-ts`. SoA does not require splitting a vector into unrelated conceptual values; the layout can still be cache-efficient while the API treats position, velocity, and acceleration as meaningful vector values.

### Locality-Aware Routing

Current leak:

```ts
await fetch("http://127.0.0.1:39217/ymir/overlap-sphere", ...);
```

Desired shape:

```ts
const hits = await verse
  .ymir()
  .world(worldId)
  .overlapSphere({ center, radius })
  .layers("gameplay")
  .all();
```

The caller asks for a typed operation or query. CultMesh chooses in-process, shared slab, IPC, network, RUDP, browser/WASM bridge, or remote Verse execution. The choice is observable for diagnostics, but it is not encoded into application logic.

### Schema Evolution

Current leak:

```ts
const entityId = payload[ENTITY_ID_SLOT];
```

Desired shape:

```ts
const entity = frame.entities.byId(entityId);
```

Bindings should be generated from one schema source into Rust, TS, Unity/C#, browser/WASM, Eve/CultUI metadata, and migration manifests. MessagePack key order, schema ids, deprecated fields, compatibility readers, and version negotiation are shared machinery.

### Surface Bindings

Current leak:

```json
{ "kind": "button", "command": "dock", "payload": { "entityId": 42 } }
```

Desired shape:

```ts
button({
  icon: "anchor",
  enabled: selected.docking().canDock(),
  onPress: selected.docking().dock(),
});
```

CultUI should bind to typed state and operation handles. A surface can be rendered in Unity, browser, terminal, or an MCP host without rewriting command payloads or state-ref resolution. Bifrost exposes daemon Verse tools through these provider-owned interfaces; it is not a Brokkr-specific socket wrapper.

The TS side now has the primitive shape too:

```ts
const selected = CultMesh.statePointer(
  "aetheria.selection.current",
  () => selection.resolve(),
  (emit) => selection.watch(emit),
);
```

That pointer is still only a primitive, not the final generated Verse handle.
The next step is generator-owned state paths, codecs, unavailable-state
diagnostics, and UI binding metadata.

The RTS generated bindings now create state pointer handles for the daemon
frame, health, authority policy, and Starbridge session documents, and the RTS
surface catalog advertises them with the same shared-memory route and schema
source metadata as the query handles. This is the proof shape Eve/CultUI and
Bifrost should consume: inspect typed pointer surfaces, resolve/watch them
through the local Verse, and leave record keys and daemon-specific resolver
rules below the shared primitive layer.

Those generated pointers are now Verse-bound in the RTS handle surface as well. The
Electron client supplies publication-reader resolvers once, generated code binds
the pointers with `CultMesh.bindStatePointer(queryVerse, pointer)`, and callers
can resolve the underlying daemon documents through the handle surface without manually
constructing query contexts or knowing where the local publication files live.

The same rule applies to projected documents and collections. Browser,
Electron, Unity, Eve, and tool runtimes should ask CultMesh for a typed
document or collection handle, bind it to the Verse once, and then call
`latest`, `watch`, `replace`, or `watchChanges`. Local CultCache records,
remote Verse documents, shared-memory queries, and quorum-backed
authorities are configuration details behind that handle. Generated Aetheria
RTS handles should target this shape instead of emitting schema dictionaries,
transport-specific lookups, or hand-written state-file readers.

## Aetheria Proofs

Aetheria should prove these primitives by forcing each client to use the same shared shape:

1. Rust Aetheria loads the measured fixture world and publishes typed state through CultMesh.
2. Rust Ymir answers step, overlap, cast, contact, broadphase, sparse-cluster, and viewport-intersection queries through typed query surfaces.
3. The browser RTS client renders object and gravity viewports through query handles and submits operations through typed handles.
4. Unity consumes render and physics views as native slices while keeping gameplay authority out of the scene hierarchy.
5. Eve/CultUI panels bind to state pointers and operation handles with no daemon-specific resolver.
6. Bifrost MCP tools discover provider interfaces through Odin and invoke typed Verse operations/queries without knowing whether the live daemon is Brokkr, Aetheria, or something else.
7. Authority diagnostics explain which runtime may author which claim without changing operation schemas.

## Performance Contract

The cozy API must not be a slow API.

- Handles should be allocation-light after binding.
- Query parameter objects should be value-shaped and cacheable.
- Native views should expose contiguous slabs and dirty ranges where co-location allows it.
- Remote fallbacks should keep the same semantic API but may return copied values or streamed deltas.
- Hot paths must support generated code, stable layout, pooled codecs, and deterministic math.
- The runtime should expose diagnostics for route choice, copy count, slab identity, schema version, authority decision, and subscription churn.

## Build Implication

When the Rust rebuild needs a new adapter, ask whether that adapter is actually a missing CultMesh primitive. Aetheria should contain the game. CultMesh/CultLib should contain the magic that makes the game feel native from every runtime.
