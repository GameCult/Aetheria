# Aetheria Verse Client Contract

Aetheria clients should enter the runtime through CultMesh typed state, not through
daemon internals. The daemon owns simulation authority and publishes daemon state
as Verse records. Unity, tools, test harnesses, and later non-Unity clients open
the same state store with the same runtime document registry and read/write typed
documents through CultNet.

The ergonomic contract is stricter than "typed records are available
somewhere." A client must be able to ask CultMesh for the domain state it wants
in one semantic call and receive a typed reactive document, collection, query
result, operation handle, or native view. The caller should not manually walk
through daemon frames, projected rows, record keys, schema slots, state refs,
renderer-local facade indexes, or transport route decisions to reconstruct one
gameplay value.

The shared entrypoint is `AetheriaRuntimeVerseClient` in the runtime state
package. It opens a local `CultMeshNode` with the runtime-only Aetheria contract
registry and exposes:

- current typed reads for provider advertisement, health, command boundary,
  latest daemon frame, latest SoA view, and daemon game/editor Eve surfaces;
- reactive watches via `WatchRecord<T>()`, including `WatchLatestFrames()` and
  `WatchLatestSoaViews()`, plus daemon GUI/TUI surface watches;
- `CultMeshMutableStatePointer<T>` handles for transparent reactive POCO
  presentation, including read, watch, and replace through shared CultMesh
  Verse context semantics;
- typed daemon and Eve command submission through the same command record keys
  used by the daemon.

This is the baseline local-record shape:

```csharp
using var client = await AetheriaRuntimeVerseClient.OpenAsync(
    statePath,
    runtimeId: "unity-render-client");

using var frames = client.WatchLatestFrames().Subscribe(change =>
{
    var frame = change.Document;
    if (frame == null)
        return;

    // Feed presentation jobs from the authoritative daemon frame.
});

var soa = await client.GetLatestSoaViewAsync();
```

That baseline is not the desired presentation API for gameplay state. Unity
menus, HUDs, renderers, RTS panels, and tools should move toward semantic
handles:

```csharp
using var verse = await AetheriaRuntimeVerseClient.OpenAsync(statePath, "tool-client");
var aetheria = verse.Aetheria();

using var inventory = aetheria.Current.Inventory.Watch(RenderInventory);
using var docking = aetheria.Current.Docking.Watch(RenderDocking);
using var dockingBay = aetheria.Current.DockingBay.Watch(RenderDockingBay);

var stationRefit = await aetheria.Station.Refit.LatestAsync();
var visibleObjects = await aetheria.Zone(currentZoneId)
    .Objects
    .VisibleToCurrentPlayer()
    .Within(cameraRect)
    .LatestAsync();
```

The implementation behind those handles may read a local CultCache record,
execute a derived query against the latest daemon frame, subscribe to a remote
Verse, bind an Eve state pointer, or expose a native slab view. That decision is
CultMesh infrastructure. The caller gets a typed domain value and diagnostics
when it asks for them.

Unity should treat the client as its Aetheria-facing runtime surface. It can
still use Burst, DOTS rendering, and native views for presentation, but the state
authority boundary is the typed CultMesh contract. No Unity-facing code should
reach for string command names, mutable payload dictionaries, local simulation
ticks, or daemon implementation classes.

No Unity-facing code should reconstruct state by joining `Current*Async()`,
`StationRefitAsync()`, raw frame projections, typed rows, record keys, and
`AetheriaUnityObservedFacadeIndex`. Renderer-local facades are temporary
presentation caches, not gameplay-state accessors. If the caller wants current
docking bay, target details, station stock, current inventory, or zone contacts,
the public API should expose that as a typed CultMesh handle directly.

## Embedded Host Option

Running an embeddable daemon core in Unity remains a deployment option, but it
does not change the client contract. Even in-process hosting should publish and
consume the same typed records so that the Unity renderer, a standalone daemon,
and another client all agree on schema and semantics.

The alternative idea of "Unity runs CultMesh using the same state assembly, but
not the daemon assembly" is exactly what this contract supports. The Unity side
references the runtime state package, opens `AetheriaRuntimeVerseClient`, and
uses CultMesh reactive wrappers to observe daemon state as if it were native
state. `AetheriaRuntimeVerseClient.Aetheria()` now exposes the first shared C#
domain facade for projected current/station/zone documents, and `AetheriaClient`
delegates to that same facade instead of owning a private projection wrapper.

## Eve Surface Boundary

The daemon already publishes game and editor Eve surface records at these keys:

- `eve:surface:aetheria.daemon.game`
- `eve:surface:aetheria.daemon.game.tui`
- `eve:surface:aetheria.daemon.editor`
- `eve:surface:aetheria.daemon.editor.tui`

`AetheriaRuntimeVerseRecordKeys` exposes those keys and
`AetheriaRuntimeVerseClient` exposes typed reads, mutable state pointers, and
`WatchRecord<EveSurfaceState>()` subscriptions for all four. The
`EveSurfaceState` document contract lives in the shared runtime package so a
Unity client, terminal client, daemon inspector, or later non-C# runtime binding
can lower the same published UI surface. The existing Eve Unity runtime host can
keep resolving Aetheria state refs automatically; it should consume those refs as
part of lowering the shared surface, not as a separate authority path.
