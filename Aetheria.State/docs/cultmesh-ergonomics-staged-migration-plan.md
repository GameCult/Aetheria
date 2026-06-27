# CultMesh Ergonomics Staged Migration Plan

This is the staged plan for getting CultMesh ergonomics from "usable transport and replicated records" to the ideal developer experience: typed state that feels local, reactive UI surfaces that resolve daemon pointers automatically, native slab views for fast clients, and authority semantics that are explicit without making every client hand-roll routing code.

The Rust/WASM rebuild remains valuable, but it is not the MVP path. The near-term goal is to build the perfect machine in C# first, freeze the API shape we actually want, then let any later Rust body implement that shape rather than inherit the current leaks.

## North Star

C# clients should read like domain code:

```csharp
var aetheria = verse.Aetheria();

var currentDockingBay = await aetheria.Current
    .DockingBay
    .LatestAsync();

using var inventorySubscription = aetheria.Current
    .Inventory
    .Watch(inventory => RenderInventory(inventory));

await aetheria.Entity(actorEntityId)
    .Pilot
    .MoveAsync(CultMesh.Vec2(input.X, input.Y));

var objects = await aetheria.Zone(zoneId)
    .Objects
    .VisibleTo(controlledEntityIds)
    .Within(rect)
    .LatestAsync();

using var renderView = await aetheria.Zone(zoneId)
    .RenderView
    .AsNativeArraysAsync();
```

TypeScript clients should have the same semantic shape:

```ts
const aetheria = verse.aetheria();

const dockingBay = await aetheria.current()
  .dockingBay()
  .latest();

const stopInventory = aetheria.current()
  .inventory()
  .watch(renderInventory);

await aetheria.entity(actorEntityId)
  .pilot.move(CultMesh.vec2(input.x, input.y));

const objects = await aetheria.zone(zoneId)
  .objects.visibleTo(controlledEntityIds)
  .within(rect)
  .latest();

const gravity = await aetheria.zone(zoneId)
  .gravity.influences()
  .intersecting(rect)
  .latest();
```

Eve/CultUI surfaces should carry typed state pointers and operation handles, not stringly command documents:

```ts
surface.panel("Selection", {
  entity: verse.aetheria().selection().entity().pointer(),
  stats: verse.aetheria().selection().entity().stats().pointer(),
  actions: verse.aetheria().selection().entity().operations(),
});
```

Ymir should be reachable through the same typed query surface pattern:

```ts
const hits = await verse.ymir()
  .world(worldId)
  .overlapSphere({ center, radius })
  .layers("gameplay")
  .all();
```

Authority should be claimed through typed operation context sugar, with CultMesh owning the generic primitive and Aetheria owning the policy:

```csharp
await aetheria.Entity(actorEntityId)
    .Pilot
    .WithAuthority("pilot-control", shardId: $"entity:{actorEntityId}")
    .MoveAsync(CultMesh.Vec2(input.X, input.Y));
```

## Current Baseline

The project already has the first layer of primitive sugar:

- `CultMeshOperationHandle`, `CultMeshOperationBindingDescriptor`, `CultMeshOperationBindingRecord`, `CultMeshOperationInvocationDescriptor`, `CultMeshOperationInvocationRecord`, and `CultMeshOperationPayload` exist in CultLib.
- `CultMeshStatePointer`, state binding descriptors/records, route hints, authority claims, query surfaces, viewport requests, native slice descriptors, and context builders exist in C# and TypeScript.
- CultMath/CultGeometry now has vector and rect primitives, including XY viewport vocabulary.
- RTS bindings are generated from the Aetheria schema and expose a typed-ish Verse facade.
- Aetheria Eve surfaces persist CultMesh binding/invocation records and keep legacy fields as compatibility mirrors.
- `AetheriaClient`, `AetheriaControl`, and `AetheriaUi` expose ergonomic C# entry points, but the domain facade is still incomplete.

The remaining problem is that the sugar is not yet the only obvious path. There are still public or semi-public seams where clients can fall back into raw envelopes, record keys, ad hoc frame projection, manual state resolution, locality-specific wiring, and renderer-side facade adaptation.

The worst current pattern is not one bad helper. It is a repeated protocol walk:

1. read one projected document;
2. extract a record key, index, schema-specific row, or state reference;
3. read another projected document or local frame;
4. resolve the key through a renderer-local facade index;
5. adapt the result into a domain object;
6. wire manual refresh or blocking reads around the whole path.

That entire stack must collapse behind one typed CultMesh document, collection, query, or operation handle. If a client wants current docking, current inventory, current target, current stats, zone contacts, station refit state, or visible render objects, the caller should make one semantic CultMesh call and receive a typed reactive value. CultMesh owns cache hydration, routing, sync, invalidation, watch lifetimes, schema binding, derived-state execution, and locality choice.

## Migration Rules

1. Repeated Aetheria-specific glue becomes a generated Aetheria facade.
2. Repeated cross-runtime glue becomes CultMesh/CultLib.
3. Public client APIs must expose typed domain operations, typed query surfaces, typed pointers, or native views.
4. Raw payload dictionaries, command names, schema ids, record keys, slot ids, and transport-local route details are allowed inside generated/runtime internals only.
5. Compatibility mirrors are temporary scaffolding. They must have a named removal stage.
6. Unity is a renderer/input client. It should not own gameplay state, zone hierarchy, simulation cadence, physics, or entity authority by accident.
7. The browser/RTS client and Unity client must use the same semantic Verse facade, even when their locality/runtime routes differ.
8. Client code must not manually compose state access from multiple protocol layers. A semantic state read is one CultMesh call from the caller's point of view.
9. Renderer-local object facades are presentation caches. They are not the API for game state, inventory, docking, targeting, or station services.

## Stage 0: Freeze The Vocabulary

Status: mostly done, keep tightening.

Goal: make the desired surface explicit enough that every later stage can delete ad hoc code instead of inventing new abstractions in place.

Targets:

- Keep `Aetheria.State/docs/cultmesh-cross-runtime-primitives.md` as the primitive ownership boundary.
- Keep `Aetheria.State/docs/daemon-code-map-for-rust-port.md` as the current daemon/control-flow map.
- Treat this document as the staged migration checklist.
- Define canonical names for:
  - document handles
  - operation handles
  - operation invocation records
  - query surfaces
  - state pointers
  - native slice views
  - authority claims
  - route hints
  - geometry values

Gates:

- No new public API may introduce `Apply(command, payload)` style call sites.
- No new client API may require a caller to know transport record keys or raw schema slots.
- No new client API may require a caller to join projected documents, frame rows, record keys, and renderer-local facades to obtain one domain value.
- All new examples in docs must use the North Star shape.

## Stage 1: Harden Shared CultMesh Primitives

Status: started.

Goal: make CultMesh primitives boring, portable, serializable, and pleasant enough that Aetheria does not need local substitutes.

Targets:

- C# primitives in `E:/Projects/CultLib/src/GameCult.Mesh/CultMeshPrimitives.cs`.
- Geometry primitives in `E:/Projects/CultLib/src/GameCult.Geometry/CultGeometryPrimitives.cs`.
- TS primitives in `E:/Projects/Aetheria/packages/cultmesh-ts/src/index.ts`.
- MessagePack/JSON-compatible record forms for operation/state/query/native/authority values.
- Typed payload readers and builders so domain facades do not pass `Dictionary<string, object?>` in public.
- Stable route and authority record forms that can cross process/runtime boundaries.

Work:

- Add typed scalar/vector/list readers to operation payloads.
- Add generated or handwritten conversion helpers between descriptor objects and record objects.
- Add invariant tests for C#/TS round trips.
- Keep primitive names runtime-neutral. Aetheria, Brokkr, Eve, Odin, VoidBot, and Ymir should all be able to use the same vocabulary.

Gates:

```powershell
dotnet test E:\Projects\CultLib\tests\GameCult.Mesh.Tests\GameCult.Mesh.Tests.csproj --no-restore -v:minimal
npm test --workspace packages/cultmesh-ts
```

## Stage 2: Generate Domain Facades

Status: started in RTS bindings; incomplete across C#/Unity/Eve.

Goal: make the typed facade the public shape in every runtime.

Targets:

- C# facade: `verse.Aetheria().Entity(id).Pilot.MoveAsync(...)`.
- TS facade: `verse.aetheria().entity(id).pilot.move(...)`.
- Eve/CultUI bindings: surface actions are operation handles, not command strings.
- Unity facade: Unity gameplay/input shell calls typed operations and typed queries only.

Work:

- Promote the current RTS binding generator into a general Aetheria CultMesh facade generator.
- Generate C# and TS operation handles from the same schema.
- Generate query builders for objects, gravity, inventory, selection, stats, action availability, and map/RTS visibility.
- Generate state pointer builders for selected object, selected inventory, selected stats, UI panels, and live frame metadata.
- Add a verifier that fails if public client code exposes command-name/payload pairs.

Desired C# shape:

```csharp
await client.Aetheria()
    .Entity(actorEntityId)
    .Equipment(slot)
    .ActivateAsync(target);
```

Desired TS shape:

```ts
await client.aetheria()
  .entity(actorEntityId)
  .equipment(slot)
  .activate(target);
```

Gates:

```powershell
dotnet run --project E:\Projects\Aetheria\Aetheria.State.Verify\Aetheria.State.Verify.csproj --no-restore
npm run generate:rts-bindings
npm run check:rts-bindings
```

## Stage 3: Make Query Surfaces The Client Contract

Status: partially started; local projection still leaks.

Goal: clients ask the daemon for typed projections. They do not reconstruct world truth from frames unless they are inside a generated/internal query executor.

Targets:

- Objects viewport.
- Gravity viewport.
- Visible set for RTS: union of entities visible to controlled units.
- Selected object.
- Current stats.
- Inventory.
- Equipment/action availability.
- Station/service status.
- Ymir overlap/cast queries.

Work:

- Move remaining browser-local projection paths behind generated query executors or daemon query endpoints.
- Make viewport queries use `CultRect`/XY everywhere outside Unity adapter boundaries.
- Give each query a stable typed request and typed response.
- Add watch/latest semantics consistently:
  - `latest()` for one-shot reads.
  - `watch()` for reactive subscriptions.
  - `asNativeArrays()` where co-located slab views are available.

Gates:

- RTS client rendering uses query surfaces, not frame spelunking.
- Unity rendering asks for render/query views, not a mirrored zone hierarchy.
- `ZoneRenderer.LoadZone` and ActionGameManager-owned zone state become vestigial or disappear.
- Public client code has no direct dependency on current daemon frame layout except through generated/internal projection code.

## Stage 3A: Collapse State Access To Typed Reactive Handles

Status: started; `AetheriaRuntimeVerseClient.Aetheria()` now exposes the first
C# domain facade for current, station, and zone projected documents,
`AetheriaClient` delegates to that shared facade, and the broad Unity
menu/HUD/map/render projected-state reads now use typed state handles instead
of transitional `Current*Async()`, `StationRefitAsync()`, `SectorMapAsync()`,
`ZoneContactsAsync()`, or `ZoneRenderAsync()` helpers. `AetheriaClientState`
also indexes projected state documents by shared document type so callers can
retrieve or watch a known typed document with one call while CultMesh owns the
bound live feed.

Goal: every client-facing state access becomes a single CultMesh document, collection, query, or pointer call. The caller names the domain state it wants and gets a typed reactive value. It does not manually inspect daemon frames, current-state projections, station rows, record keys, local facade indexes, or transport route details.

Targets:

- Current entity.
- Current docking.
- Current docking bay.
- Current inventory.
- Current target and target details.
- Current ship settings.
- Station refit state.
- Trade cargo targets.
- Zone contacts.
- Selected object.
- Runtime catalog collections.
- Player settings and input bindings.

Desired C# shape:

```csharp
var current = client.Aetheria().Current;

using var docking = current.Docking.Watch(RenderDocking);
using var bay = current.DockingBay.Watch(RenderDockingBay);
using var inventory = current.Inventory.Watch(RenderInventory);

var refit = await client.Aetheria()
    .Station
    .Refit
    .LatestAsync();

var contacts = await client.Aetheria()
    .LatestAsync<AetheriaRuntimeZoneContactsDocument>();

using var render = client
    .Watch<AetheriaRuntimeZoneRenderDocument>(RenderZone);
```

Desired TS shape:

```ts
const current = client.aetheria().current();

const stopDocking = current.docking().watch(renderDocking);
const stopBay = current.dockingBay().watch(renderDockingBay);
const stopInventory = current.inventory().watch(renderInventory);

const refit = await client.aetheria()
  .station()
  .refit()
  .latest();
```

Work:

- Keep pushing shared CultMesh typed reactive document and collection handles down until type/schema lookup is owned by CultMesh rather than by the Aetheria facade registry.
- Generate Aetheria current/station/zone/catalog accessors from schema metadata.
- Move derived-state joins, such as current docking bay from current docking plus station refit plus entity records, into generated/internal query executors.
- Replace remaining Unity menu/HUD/render calls to `TryResolve*` and `_observedFacadeIndex` with typed handles or generated/internal render adapters.
- Replace blocking `GetAwaiter().GetResult()` reads in client presentation code with watch/latest handles that own lifetimes.
- Keep renderer-local facade indexes only inside render adapter internals while native/query views are still being migrated.
- Add verifiers that treat multi-hop state reads in client code as failures.

Gates:

- `InventoryMenu`, `InventoryPanel`, `TradeMenu`, `LocalMenu`, `SchematicDisplay`, `SectorRenderer`, `MapRenderer`, and `ZoneRenderer` do not manually join state projections to obtain domain values.
- Shared projected documents are reachable through `client.Aetheria().Document<TDocument>()`, `client.Aetheria().LatestAsync<TDocument>()`, and `client.Watch<TDocument>()` as the C# stepping stone toward CultMesh-native type/schema lookup.
- Client-facing code contains no `_observedFacadeIndex.TryResolve*` outside Unity render adapter internals.
- Client-facing code contains no `ResolveClient().Current*Async().GetAwaiter().GetResult()` or equivalent blocking state reads.
- `TryGetTypedCurrentDockingBayFacade` and similar transitional helpers are deleted rather than renamed.
- Verifiers assert the semantic API shape, not the transitional facade-adaptation scaffolding.

## Stage 4: Resolve Eve State Pointers Automatically

Status: primitive records exist; runtime resolution is not yet the whole story.

Goal: an Eve surface can contain a typed pointer into daemon state and the UI runtime resolves it, watches it, and binds it without custom per-panel plumbing.

Targets:

- Eve surface documents carry `CultMeshStateBindingRecord`.
- Operation buttons carry `CultMeshOperationBindingRecord`.
- User actions emit `CultMeshOperationInvocationRecord`.
- CultUI runtime resolves pointers via the current Verse.
- Unity UIToolkit lowerer consumes the same surface semantics as browser/Electron.

Work:

- Add a pointer resolver interface to CultMesh/CultUI rather than Aetheria-specific code.
- Add watch lifetime management to UI runtime.
- Add loading/error/stale states as generic pointer states.
- Remove manual state-ref resolver glue from Aetheria UI code once the generic runtime path exists.

Gates:

- Selection panel can bind selected entity/stats/inventory from pointers only.
- No Aetheria panel needs to manually decode a state reference.
- Surface command persistence contains CultMesh invocation records as the authoritative form.

## Stage 5: Native View Sugar For Unity And Co-Located Clients

Status: designed; implementation still thin.

Goal: Unity becomes a thin, brutally fast renderer/input client over Aetheria/Ymir state.

Targets:

- `AsNativeArraysAsync()` over CultCache slabs.
- Unity-safe lifetime wrappers around shared memory/native slice descriptors.
- Burst-readable views for render columns and physics state.
- Dirty ranges/version stamps so clients can skip unchanged slabs.
- Render-oriented projections generated by Aetheria, not maintained as a Unity hierarchy.

Work:

- Define native slice descriptors for Aetheria render views and Ymir physics views.
- Wrap descriptors in Unity `NativeArray<T>`/`NativeSlice<T>` adapters with explicit lifetime ownership.
- Add schema/version validation before exposing a slab as a native view.
- Move camera/frustum queries to daemon query surfaces.
- Keep Unity-specific transform/material/sprite concerns in Unity, but pull entity membership, object location, gravity influence, physics state, inventory, and stats from Aetheria/Ymir.

Desired Unity shape:

```csharp
using var view = await verse.Aetheria()
    .Zone(zoneId)
    .RenderView
    .ForCamera(cameraQuery)
    .AsNativeArraysAsync();

RenderJobs.Schedule(view.Objects, view.Sprites, view.Gravity);
```

Gates:

```powershell
dotnet build E:\Projects\Aetheria\GameCult.Aetheria.State.Unity.csproj --no-restore -v:minimal
dotnet build E:\Projects\Aetheria\Aetheria.State.Unity.Smoke\Aetheria.State.Unity.Smoke.csproj --no-restore -v:minimal
```

## Stage 6: Authority Sugar And Locality Policies

Status: primitive claims exist; gameplay policy needs first-class wiring.

Goal: authority is explicit and configurable without leaking into every gameplay call.

Targets:

- Server-authoritative mode.
- Trusted co-op distributed simulation mode.
- Future quorum/re-sim mode, without blocking MVP.
- Per-shard/per-entity authority claims.
- Route hints chosen by Verse/runtime policy.

Work:

- Add Aetheria authority policy config as data, not hardcoded branch logic.
- Add facade sugar:
  - `WithAuthority(...)`
  - `PreferLocality(...)`
  - `AsNode(...)`
  - `WithIdempotency(...)`
- Attach operation context automatically where a facade knows the actor/entity/shard.
- Add diagnostics that explain why an operation routed to local/shared-memory/network/host.
- Keep CultMesh generic: it owns claims, leases, routes, receipts, and diagnostics; Aetheria owns which operations require which claims.

Gates:

- Unity player can own simulation authority for its own pilot state in trusted co-op config.
- RTS player can own hostile pawns or commanded pawns in trusted co-op config.
- Server-authoritative config can force daemon/host-only simulation authority.
- No public gameplay API requires manually constructing a command port.

## Stage 7: Ymir Query Facade And Physics Ownership

Status: Ymir owns more physics directionally, but query sugar and benchmark discipline need to catch up.

Goal: physics is a typed service with the same CultMesh ergonomics as Aetheria state.

Targets:

- `OverlapSphere`.
- Brush/viewport intersection for gravity influence queries.
- Collision/cast queries needed by abilities and movement.
- Gravity terrain influence queries.
- Physics benchmark suite with deterministic sparse clustered scenes.

Work:

- Add Ymir typed query handles using CultMesh query surface primitives.
- Add C# and TS facade generation for Ymir.
- Add Aetheria-to-Ymir state references rather than duplicating physics ownership in Unity.
- Add benchmarks that compare vector SoA, scalar SoA, and spatial acceleration structures.
- Keep Unity physics out of gameplay state. Unity may render/debug physics but not authoritatively simulate it.

Gates:

- Gameplay queries use Ymir facade operations.
- Gravity viewport queries can ask for every body whose influence brush intersects an XY viewport.
- Benchmarks are deterministic and run in CI or a documented local perf lane.

## Stage 8: One Schema Pipeline

Status: pieces exist; still too hand-maintained.

Goal: one schema source generates cross-runtime facades, record serializers, pointer/query/operation metadata, and docs.

Targets:

- C# facade generation.
- TS facade generation.
- Eve/CultUI binding metadata.
- Unity native view adapters.
- Query request/response records.
- Operation payload records.
- Schema evolution manifests.
- Legacy readers only where explicitly needed.

Work:

- Replace one-off RTS binding generation with a CultMesh schema generator.
- Generate operation payload types instead of exposing generic payload maps.
- Generate query result types and pointer target types.
- Generate docs/snippets from schema metadata so examples cannot rot.
- Add schema version compatibility checks to native view and network decode paths.

Gates:

- Adding a new Aetheria operation once creates C#, TS, Eve operation binding, verifier metadata, and docs.
- Adding a new query once creates C#, TS, Eve pointer/query metadata, and query executor stubs.
- No runtime has a hand-authored duplicate of another runtime's binding vocabulary.

## Stage 9: Delete Compatibility Shells

Status: not started.

Goal: remove scaffolding once the generated typed path is the only supported path.

Remove:

- Public command-name/payload APIs.
- Cached command port concepts.
- Public raw frame spelunking for client projections.
- Public record-key/schema-slot access in client code.
- Manual state-ref resolution in UI panels.
- Legacy XZ vocabulary outside Unity adapter names.
- Unity-owned zone hierarchy/gameplay simulation state.
- Browser-local bespoke gameplay behavior.

Keep only as internals where required:

- Wire-level records.
- Legacy readers for persisted documents that predate the migration.
- Diagnostic tools that intentionally expose raw records.
- Tests that assert legacy decode behavior until the migration window closes.

Gates:

- Verifier blocks new public stringly operation surfaces.
- `rg` sweeps show no client-facing `Apply(command`, `commandName`, `payload`, `recordKey`, or raw slot access outside generated/runtime internals.
- Unity, RTS/Electron, and Eve all consume the same semantic facade.

## Stage 10: MVP Acceptance

The migration is MVP-complete when:

- Aetheria daemon owns gameplay simulation state.
- Ymir owns physics state and gameplay physics queries.
- Unity is rendering/input focused.
- RTS/Electron is a thin CultMesh client, not a parallel gameplay implementation.
- Eve/CultUI surfaces use typed state pointers and typed operation handles.
- Aetheria exposes current stats, inventory, equipment, selection, objects viewport, gravity viewport, and authority-aware operations as typed surfaces.
- Unity and browser clients can point at the same Verse and observe/interact with the same simulation according to authority policy.
- Co-located clients can use native slab views where available.
- Remote clients use the same facade shape over network routes.
- Locality is visible in diagnostics, not application logic.

## Non-Goals For This MVP

- Rebuilding the daemon in Rust.
- Making the browser host the full simulation in WASM.
- Quorum/re-sim consensus implementation.
- Perfect zero-copy support across every runtime.
- Exact parity with every historical Unity behavior.

These are future expansion paths. The migration must not close the door on them, but it should not block the MVP on them.

## Verification Lane

Use this lane when changing the sugar surface:

```powershell
dotnet test E:\Projects\CultLib\tests\GameCult.Mesh.Tests\GameCult.Mesh.Tests.csproj --no-restore -v:minimal
dotnet run --project E:\Projects\Aetheria\Aetheria.State.Verify\Aetheria.State.Verify.csproj --no-restore
dotnet build E:\Projects\Aetheria\GameCult.Aetheria.State.Unity.csproj --no-restore -v:minimal
dotnet build E:\Projects\Aetheria\Aetheria.State.Unity.Smoke\Aetheria.State.Unity.Smoke.csproj --no-restore -v:minimal
npm run generate:rts-bindings
npm run check:rts-bindings
npm run build
```

Add Ymir benchmark and authority smoke commands to this lane as those stages land.

## Sugar Targets Checklist

- [ ] Typed Aetheria C# domain facade is complete.
- [ ] Typed Aetheria TS domain facade is complete.
- [ ] Typed Ymir C# and TS query facades exist.
- [ ] Eve/CultUI resolves CultMesh state pointers automatically.
- [ ] Eve/CultUI invokes operation records without Aetheria-specific command glue.
- [ ] Query surfaces cover objects viewport, gravity viewport, selection, stats, inventory, equipment, and services.
- [ ] Unity render path consumes native views or query results, not gameplay-owned mirrored state.
- [ ] RTS render path consumes query surfaces, not bespoke frame projection as public client behavior.
- [ ] Authority policy is data-driven per Verse.
- [ ] Operation context sugar attaches route, claim, and idempotency without caller boilerplate.
- [ ] Schema generator emits C#, TS, Eve metadata, query records, operation payload records, and docs.
- [ ] Public APIs do not expose stringly command names or raw payload dictionaries.
- [ ] Compatibility mirrors are internal, named, and scheduled for deletion.
