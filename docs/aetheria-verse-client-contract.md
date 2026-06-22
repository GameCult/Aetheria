# Aetheria Verse Client Contract

Aetheria clients should enter the runtime through CultMesh typed state, not through
daemon internals. The daemon owns simulation authority and publishes daemon state
as Verse records. Unity, tools, test harnesses, and later non-Unity clients open
the same state store with the same runtime document registry and read/write typed
documents through CultNet.

The shared entrypoint is `AetheriaRuntimeVerseClient` in the runtime state
package. It opens a local `CultMeshNode` with the runtime-only Aetheria contract
registry and exposes:

- current typed reads for provider advertisement, health, command boundary,
  latest daemon frame, latest SoA view, and daemon game/editor Eve surfaces;
- reactive watches via `WatchRecord<T>()`, including `WatchLatestFrames()` and
  `WatchLatestSoaViews()`, plus daemon GUI/TUI surface watches;
- `AetheriaRuntimeVerseDocument<T>` handles for transparent reactive POCO
  presentation;
- typed daemon and Eve command submission through the same command record keys
  used by the daemon.

This is the intended shape for the Unity client:

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

Unity should treat the client as its Aetheria-facing runtime surface. It can
still use Burst, DOTS rendering, and native views for presentation, but the state
authority boundary is the typed CultMesh contract. No Unity-facing code should
reach for string command names, mutable payload dictionaries, local simulation
ticks, or daemon implementation classes.

## Embedded Host Option

Running an embeddable daemon core in Unity remains a deployment option, but it
does not change the client contract. Even in-process hosting should publish and
consume the same typed records so that the Unity renderer, a standalone daemon,
and another client all agree on schema and semantics.

The alternative idea of "Unity runs CultMesh using the same state assembly, but
not the daemon assembly" is exactly what this contract supports. The Unity side
references the runtime state package, opens `AetheriaRuntimeVerseClient`, and
uses CultMesh reactive wrappers to observe daemon state as if it were native
state. The current Unity CultMesh DLL exposes `WatchRecord<T>()`; once the
packaged DLL catches up with local CultLib, the client handle can delegate
directly to CultMesh `Document<T>` without changing callers.

## Eve Surface Boundary

The daemon already publishes game and editor Eve surface records at these keys:

- `eve:surface:aetheria.daemon.game`
- `eve:surface:aetheria.daemon.game.tui`
- `eve:surface:aetheria.daemon.editor`
- `eve:surface:aetheria.daemon.editor.tui`

`AetheriaRuntimeVerseRecordKeys` exposes those keys and
`AetheriaRuntimeVerseClient` exposes typed reads, document handles, and
`WatchRecord<EveSurfaceState>()` subscriptions for all four. The
`EveSurfaceState` document contract lives in the shared runtime package so a
Unity client, terminal client, daemon inspector, or later non-C# runtime binding
can lower the same published UI surface. The existing Eve Unity runtime host can
keep resolving Aetheria state refs automatically; it should consume those refs as
part of lowering the shared surface, not as a separate authority path.
