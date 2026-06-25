# Aetheria Rust Rebuild Freeze Checklist

This checklist turns the current C# daemon and client integrations into a measured, deprecated reference before the rebuild begins in earnest. The goal is not to migrate the C# shape piece by piece, and it is not strict parity with every current wart. The goal is to make current behavior visible enough that the new daemon, client APIs, and shared CultMesh/CultLib primitives can deliberately preserve, improve, or discard it while building the clean Aetheria/Ymir/CultMesh shape.

## Freeze Artifacts

| Artifact | Source of truth | Output |
| --- | --- | --- |
| Daemon schema catalog | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs` | Generated schema manifest with schema ids, document names, MessagePack key order, enum ids, and record key conventions. |
| Runtime snapshot schema catalog | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeSnapshotDocuments.cs` | Generated schema manifest for run, zone, entity, body, inventory, loadout, cargo, equipment, and stat grids. |
| Authority schema catalog | `Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseAuthorityPolicy.cs` | Generated schema manifest plus policy-mode behavior table. |
| Ymir C# reference sample | `Assets/Scripts/ServerShared/YmirPhysicsContracts.cs` | Current DTO and query behavior evidence for Rust Ymir design. Preserve only semantics we still want. |
| Current TS generated contract | `Aetheria.Rts.Web/scripts/generate-rts-bindings.mjs` and `Aetheria.Rts.Web/Electron/aetheria-rts-generated-bindings.ts` | Evidence that schema extraction exists today; replace it with shared CultMesh/CultLib generation for typed handles, operations, queries, state pointers, native views, authority, and migration manifests. |
| Shared CultMesh/CultLib primitive target | `Aetheria.State/docs/cultmesh-cross-runtime-primitives.md` | Design bar and acceptance vocabulary for hoisting typed documents, operations, queries, state pointers, native views, authority, geometry, locality routing, schema evolution, and surface bindings out of Aetheria glue. |
| Initial daemon fixture exporter | `Aetheria.State.Freeze/Program.cs` | Runs the C# daemon once, reads current daemon publications, emits normalized JSON plus MessagePack bytes for frame, public docs, SoA descriptor, projection fixtures, authority decisions, Ymir query probes, and seeded pilot/interaction/refit command-batch fixtures. |
| Current client verifier gates | `Aetheria.Rts.Web/scripts/verify-stage7b-rts-client.ps1`, `verify-stage7c-local-runtime.ps1`, `verify-stage7c-electron-shell.ps1`, `Aetheria.State/scripts/verify-stage7d-unity-parity.ps1` | Current smoke gates while C# is still live. They prove today still runs; they do not define the future daemon or client API surface. |

## Golden Fixtures

The C# daemon and clients are measured when these fixtures can be generated from the current implementation. The rebuild should consume them as migration probes: green where current behavior is intentional, red or replaced where current behavior is accidental, and redesigned wherever the client semantics should become simpler.

| Fixture | Must contain | Proves |
| --- | --- | --- |
| `seeded-local-rts-world` | The daemon-seeded `local-rts` run with zones, entities, bodies, inventory, loadouts, stat grids, current entity, and authority policy. | Rust can load current world semantics without inheriting the host seeding code. |
| `command-batch-pilot` | Targeting, movement, look, tractor, sensor ping, shield toggle, weapon group fire. Initial exporter emits this batch and captures applied/rejected command accounting. | Rust operations cover the same gameplay intents through typed operations. |
| `command-batch-refit` | Rename, transfer cargo, trade purchase, equip from cargo, store equipped item, set docked current ship, pick up loot, restore loadout. Initial exporter emits this batch and captures applied/rejected command accounting: rename/transfer/trade/equip/store currently apply; current-ship selection, pickup without a seeded drop, and restore without a template currently reject. | Rust operations cover inventory/economy/refit intent without inheriting command-document ergonomics or accidental rejection behavior. |
| `command-batch-interaction` | Dock, dock nearest, undock, interact, tow to station. Initial exporter emits this batch and captures applied/rejected command accounting. | Rust operations cover interaction/travel intent and may simplify ambiguous current behavior. |
| `authority-decisions` | Allowed and rejected commands for host-authoritative, delegated runtime, owning runtime, and lease cases. | Rust authority has explicit behavior for currently implemented modes and explicit design choices for modes C# only stubs. |
| `post-tick-frame` | Frame after a fixed command batch and fixed delta, including command accounting and committed facts. | Rust tick composition can be compared to current output where useful, then diverge where the new simulation model is better. |
| `objects-viewport` | XY rect, controlled unit set, visible objects, statuses, inventory summaries. | Rust query surface can replace TS local projection copies. |
| `gravity-viewport` | XY rect, intersecting gravity brushes, body views. | Rust query surface can replace Unity/TS gravity reconstruction. |
| `selected-object` | Entity selection, status, target, faction, inventory identity. | Rust selected-object query supports current UI expectations through cleaner typed state. |
| `inventory` | Equipment and cargo rows, source identity, item stats, quantity, quality, durability. | Rust inventory projection can drive Unity and RTS panels. |
| `soa-view` | Current-zone render columns, column kinds, vector columns, dirty ranges, native view descriptor. | Rust view publication can feed Unity and native clients. |
| `ymir-queries` | Step integration, radial field acceleration, contact separation, overlap circle, cast circle, overlap sphere, cast sphere, and invalid-input cases. Initial exporter emits typed request/result probes with explicit vector, world, body, field, hit, and contact records in JSON plus MessagePack. Vector fields now use shared CultLib `GameCult.Geometry` primitives (`CultVec2`/`CultVec3`) instead of fixture-local DTOs. | Rust Ymir preserves useful physical semantics while replacing C# endpoint shape with Rust/CultMath primitives. |

## Rust Rebuild Acceptance Gates

The first rebuild milestone is accepted only when the new architecture demonstrates the intended cross-runtime shape.

1. A Rust Verse node can load the measured fixture world and publish typed Aetheria state.
2. A TS client can submit typed operations through shared CultMesh operation handles without constructing command documents, record keys, or raw MessagePack payloads.
3. A browser client can render map state by watching shared typed object/gravity query surfaces, not by reading local `.cc` files or duplicating projection code.
4. A Unity client can consume a shared typed render view as native arrays without owning gameplay state or running Unity physics for authority.
5. Rust Ymir owns physical truth for step, overlap, cast, contact, and broadphase queries.
6. Eve/CultUI surfaces can bind to shared CultMesh state pointers and typed operations without manual state-ref resolver glue in client code.
7. Authority policy is configured through shared CultMesh authority primitives and changes who may author claims without changing operation schemas.
8. The API can run in native daemon mode and expose a WASM-compatible surface for browser simulation host mode.
9. The old C# daemon can be turned off for the browser RTS proof and the world still runs, renders, and accepts commands.

## CultMesh Sugar Gates

These are not cosmetic. If these fail, the architecture is still leaking.

The expanded primitive spec is `Aetheria.State/docs/cultmesh-cross-runtime-primitives.md`. Use it as the shared-library ownership boundary whenever a new adapter starts appearing in Aetheria-specific code.

| Gate | Bad smell | Desired shape |
| --- | --- | --- |
| Typed operations | Client code constructs `CommandId`, `recordKey`, `schemaId`, or `document_put_raw`. | CultMesh operation handles expose `verse.aetheria().entity(actor).pilot().move(...)`. |
| Typed queries | Client code fetches a frame and calls local projection helpers. | CultMesh query surfaces expose `verse.aetheria().zone(zone).objects().visibleTo(units).within(rect)`. |
| Typed state pointers | UI code resolves string state refs manually. | CultMesh state pointers are first-class values resolved by CultUI/CultMesh runtime. |
| Native views | Unity code manually opens memory maps by daemon-specific paths. | CultMesh native slice views expose `RenderView().AsNativeArrays()` from the typed Verse handle. |
| Authority | Each daemon invents policy, lease, claim, and runtime-role glue. | CultMesh authority primitives carry policy modes, claims, leases, runtime roles, and future quorum hooks. |
| Geometry | Each runtime reinvents vectors, rects, circles, spheres, and coordinate conversions. | CultMath/CultMesh provide shared geometry values and deterministic scalar helpers. |
| Locality | Co-deployed services still talk through bespoke URLs or files. | CultMesh chooses in-process, shared slab, IPC, network, or WASM transport behind one semantic API. |
| Ymir queries | Unity or TS posts JSON to a hard-coded local URL for normal co-deployed queries. | Aetheria/Ymir queries are typed, colocated when possible, and remotely routable when needed. |
| Schema generation | TS, C#, and Rust each hand-maintain slot maps. | One schema source generates cross-runtime CultMesh bindings and legacy migration manifests. |
| Client semantics | Each runtime gets bespoke helper layers and transport assumptions. | Unity, TS, browser, Rust, native, and Eve/CultUI share the same operation/query/state semantics with runtime-specific ergonomic sugar. |

## Current Verification Commands

These are current-state gates, not proof that the Rust rebuild is complete. They are useful while freezing the C# contract.

```powershell
dotnet build .\GameCult.Aetheria.State.Unity.csproj /clp:ErrorsOnly /p:UseSharedCompilation=false
dotnet build .\Aetheria.State.Daemon\Aetheria.State.Daemon.csproj /clp:ErrorsOnly /p:UseSharedCompilation=false
dotnet run --project .\Aetheria.State.Verify\Aetheria.State.Verify.csproj
.\Aetheria.State\scripts\verify-stage7d-unity-parity.ps1
cd .\Aetheria.Rts.Web
npm run check:rts-bindings
npm run build
npm run verify:stage7b
npm run verify:stage7c
npm run verify:stage7c:electron
```

## Immediate Build Work

1. Expand `Aetheria.State.Freeze` beyond baseline plus pilot/interaction/refit/authority/Ymir probes into post-tick, richer authority, and richer Ymir fixture families.
2. Expand `generate-rts-bindings.mjs` or replace it with shared CultMesh/CultLib schema and binding generation for Rust, TS, Unity/C#, browser/WASM, and Eve/CultUI, including legacy migration manifests.
3. Expand the initial CultMath-shaped Ymir fixture cases with explicit rect, circle, sphere, broadphase, sparse-cluster, and viewport-intersection schemas.
4. Begin the shared primitive implementation from `cultmesh-cross-runtime-primitives.md` before the Rust daemon hardens around one-off Aetheria adapters.
5. Create an initial Rust workspace with `ymir_math`, `ymir_physics`, `aetheria_schema`, and `aetheria_world`.
6. Make Rust Ymir satisfy the intentional Ymir fixture cases before wiring it into Aetheria; document any deliberate improvements over C# behavior.
7. Make Rust Aetheria load the measured world fixture and answer object/gravity viewport queries through hoisted CultMesh query/state primitives shared by TS, Unity, browser, and Eve/CultUI.

Generate the initial daemon fixture set with:

```powershell
dotnet run --project .\Aetheria.State.Freeze\Aetheria.State.Freeze.csproj -- --root . --state .\obj\freeze\aetheria-freeze-state.cc --out .\Aetheria.State\fixtures\rust-rebuild-freeze
```

For smoke runs that should not touch checked-in fixtures, write to `.\obj\freeze-smoke\fixtures`.

The rebuild begins once the C# behavior is measured enough to learn from it without copying its leaks.
