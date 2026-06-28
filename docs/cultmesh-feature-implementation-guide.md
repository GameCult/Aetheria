# Implementing Aetheria Features With CultMesh

Date: 2026-06-28

This guide describes the target developer experience for adding Aetheria state
with CultMesh. If the workflow grows into a chain of facades, sessions,
projectors, adapters, and surface builders, stop and simplify the state API.

The rule is blunt:

```text
small UI-only value: derive it inline from already accessible typed state
named UI-only projection: define the projected document, then read it
simulation feature: define simulation state, publish typed state, consume it
hot rendering/physics: use SoA, not object-graph UI documents
```

CultMesh owns document identity, sync, prediction, reconciliation, and reactive
access. Aetheria code should read like it is using game state, not walking
protocol layers.

## The Ergonomic Bar

UI-only state should take one or two steps.

Use one step when the value is small and already derivable from state the caller
has:

```csharp
var currentDocking = client.State.Current.ReactiveDocking();
var bayName = currentDocking.Current?.DockingBayName ?? "";
```

Use two steps when the projection is shared, non-trivial, or should have a
stable schema:

```text
1. Define the daemon-projected typed document.
2. Read it from Unity as a managed reactive typed document.
```

Simulation state should take two or three steps:

```text
1. Put the simulation data and rules in the daemon-owned domain model.
2. Publish the useful client-facing shape as typed CultMesh state.
3. Read that state from Unity and send typed operations back when input mutates truth.
```

Anything beyond that needs a clear reason. Performance-sensitive entity
rendering uses SoA because it is a different data shape, not because ordinary UI
state should become ceremonial.

## What CultMesh Should Hide

Feature authors should not manually handle:

- document routing keys;
- protocol envelopes;
- schema lookup;
- cache hydration;
- sync subscriptions;
- prediction dispatch;
- reconciliation smoothing;
- session wrapper lifetime around a single document;
- joins against raw daemon frame rows in UI code.

Those are CultMesh/runtime concerns. Unity callers should hold either a
`CultMeshReactiveDocument<TDocument>` or a named typed handle that returns one.

## What Not To Add

Do not add new single-document wrappers such as:

```csharp
AetheriaRuntimeSomeStateSession
```

when the wrapper only stores a document, exposes `Current`, and calls
`Dispose()`. That is a managed reactive document with a worse name.

Do not add feature-specific access paths like this:

```text
daemon document -> session -> facade -> projector -> adapter -> surface builder -> caller
```

That shape preserves reconstruction-era scaffolding. It does not express the
game.

## Choosing The State Shape

Use inline derivation when:

- the value is UI-only;
- the derivation is local and obvious;
- the source typed document is already available to the caller;
- no other system needs the result as a named state surface.

Use a projected typed document when:

- multiple callers need the same derived state;
- the state is useful to Unity, RTS, tests, tooling, or the website simulator;
- the derivation joins multiple source documents;
- the result needs a stable schema or cross-runtime binding;
- the projection should update reactively as daemon state changes.

Use daemon-owned simulation state when:

- the value affects game rules;
- input can mutate it;
- it needs authority, replay, persistence, or reconciliation;
- the daemon must validate operations before publishing the next state.

Use SoA/native views when:

- Unity jobs, Burst, Ymir, or rendering need large columnar data;
- row count and per-frame access make object graphs the wrong format;
- the data is a hot view over daemon truth rather than a UI document.

## One-Step UI Derivation

For a small UI-only value, read existing typed state directly.

```csharp
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class DockingBadge : IDisposable
{
    private readonly AetheriaClient _client;
    private CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _docking;

    public DockingBadge(AetheriaClient client)
    {
        _client = client;
    }

    public string Text
    {
        get
        {
            _docking ??= _client.State.Current.ReactiveDocking();
            var current = _docking.Current;
            return current?.IsDocked == true ? current.DockingBayName : "";
        }
    }

    public void Dispose()
    {
        _docking?.Dispose();
    }
}
```

The caller does not need a custom facade, session, projector, or adapter. If the
inline derivation becomes shared or hard to read, promote it to a projected
document.

## Two-Step UI Projection

Use this for shared presentation state such as player HUD, docking summary,
commander wave status, station service summary, or Starbridge support alerts.

### Step 1: Define The Projected Document

Put the document type in the shared runtime package so every runtime sees the
same schema:

```text
Packages/org.gamecult.aetheria.state/Runtime
```

Example:

```csharp
[MessagePackObject]
[CultDocument(AetheriaRuntimeDaemonSchemas.ZoneDefenseStatus)]
public sealed class AetheriaRuntimeZoneDefenseStatusDocument
{
    [Key(0)]
    public int ZoneIndex { get; set; } = -1;

    [Key(1)]
    public double BaseShieldRatio { get; set; }

    [Key(2)]
    public int IncomingHostileCount { get; set; }

    [Key(3)]
    public int ActiveTurretCount { get; set; }

    [Key(4)]
    public string AlertLevel { get; set; } = "";
}
```

Then expose it as a named projection from `AetheriaClientState`:

```csharp
public CultMeshReactiveDocument<AetheriaRuntimeZoneDefenseStatusDocument>
    ReactiveZoneDefenseStatus()
{
    return Mesh.Reactive<AetheriaRuntimeZoneDefenseStatusDocument>(
        AetheriaRuntimeDaemonDocuments.ZoneDefenseStatus);
}
```

The exact registration/publishing code belongs in the runtime plumbing. It
should be generated or centralized. The feature author should not have to write
five separate access classes for one projected state shape.

### Step 2: Read It From Unity

Unity holds the typed reactive document and reads `Current`.

```csharp
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class ZoneDefenseHud : IDisposable
{
    private readonly AetheriaClient _client;
    private CultMeshReactiveDocument<AetheriaRuntimeZoneDefenseStatusDocument> _status;

    public ZoneDefenseHud(AetheriaClient client)
    {
        _client = client;
    }

    public void Refresh()
    {
        _status ??= _client.State.ReactiveZoneDefenseStatus();
        var status = _status.Current;
        if (status == null)
            return;

        DrawShield(status.BaseShieldRatio);
        DrawIncoming(status.IncomingHostileCount);
        DrawAlert(status.AlertLevel);
    }

    public void Dispose()
    {
        _status?.Dispose();
    }
}
```

That is the whole UI projection workflow. If more steps are required, they
belong in CultMesh/runtime infrastructure, not in every feature.

## Two Or Three-Step Simulation Feature

Use this when the feature changes gameplay: heat support, repair drones,
station construction, scenario progression, commander infrastructure, or wave
director state.

### Step 1: Put Truth In The Daemon

Simulation truth lives with the daemon domain model and tick/apply logic.

```csharp
public sealed class StationSupportState
{
    public double CoolingReserve { get; set; }
    public double RepairNaniteReserve { get; set; }
    public int ActiveSupportDrones { get; set; }
}
```

The daemon mutates this state during ticks and command application. Unity does
not own gameplay truth.

### Step 2: Publish Typed CultMesh State

Expose the client-facing shape as a typed document.

```csharp
[MessagePackObject]
[CultDocument(AetheriaRuntimeDaemonSchemas.StationSupport)]
public sealed class AetheriaRuntimeStationSupportDocument
{
    [Key(0)]
    public double CoolingReserve { get; set; }

    [Key(1)]
    public double RepairNaniteReserve { get; set; }

    [Key(2)]
    public int ActiveSupportDrones { get; set; }
}
```

The daemon publishes this document from its current truth. CultMesh handles sync.
If the client has prediction authority, local changes to the managed document
are predictions; reconciliation corrects them without requiring feature code to
manually shuttle deltas.

### Step 3: Read State And Submit Typed Operations

Unity reads state through the managed typed document:

```csharp
var support = client.State.ReactiveStationSupport();
var cooling = support.Current?.CoolingReserve ?? 0;
```

Unity asks the daemon to mutate truth through a typed operation:

```csharp
client.Operations.Submit(new AetheriaRuntimeDeploySupportDroneOperation
{
    ZoneIndex = currentZone,
    TargetEntityIndex = targetEntity,
    DroneLoadoutId = selectedLoadout
});
```

The daemon validates and applies the operation. The next published document is
the authoritative result.

## SoA For Hot Paths

Use SoA when Unity needs fast columnar access, not when a UI needs a label.

Good SoA candidates:

- visible entity transforms;
- render splats;
- physics bodies;
- sensor/contact rows for thousands of entities;
- Ymir/Burst job inputs.

Bad SoA candidates:

- current docking bay;
- selected object details;
- HUD status;
- player settings;
- station service summary.

SoA should be a named high-performance view over daemon truth. It should not
force ordinary presentation code to understand frame slabs, column handles, or
native buffer ownership.

## Reconnection

State needed to reconstruct a player after a client crash must be in daemon or
scenario/session CultMesh documents, not Unity locals.

Persist or republish:

- player identity and seat;
- current entity or cockpit module;
- docked station and bay;
- inventory/loadout references;
- scenario progress;
- commander infrastructure state;
- active operations that survive reconnect;
- escape pod/proxy ship state when ejected.

On reconnect, Unity should reacquire typed documents and rebuild presentation
from `Current`. Reconnection should not require replaying UI actions.

## Tests

For inline UI derivation, test the source document and the small consuming
component when behavior is non-trivial.

For projected UI documents, test:

- projection from representative daemon state;
- empty/default state;
- reactive update after source state changes;
- Unity caller reads the projected document directly.

For simulation features, test:

- daemon tick/application mutates truth;
- typed operation validation accepts and rejects correctly;
- published document reflects daemon truth;
- prediction/reconciliation behavior when the runtime supports local authority;
- reconnect can reconstruct the required player state.

For SoA, test:

- row identity and generation;
- bounds/ownership;
- empty views;
- large views;
- native buffer lifetime.

## Verifier Rules

Verifier rules should guard the desired ergonomic shape. They should forbid
reconstruction-era detours.

Good verifier expectations:

```text
Unity caller owns CultMeshReactiveDocument<TDocument>
Unity caller reads .Current
Unity caller uses client.State.ReactiveFeatureName()
Unity caller submits typed operations for mutations
hot renderer uses named SoA view
```

Bad verifier expectations:

```text
Unity caller owns AetheriaRuntime*Session for one document
Unity caller calls ObserveFeatureName() wrapper only to read Current
UI caller reads raw daemon frames and joins rows
feature adds facade/projector/adapter/surface-builder chain
```

The verifier should make the codebase harder to regress into ceremony, not
freeze the ceremony in place.

## Final Check

Before calling a CultMesh feature ergonomic, answer these questions:

- Can a UI-only inline value be implemented in one local derivation?
- Can a shared UI projection be implemented by defining the document and reading
  it from Unity?
- Can a simulation feature be explained as daemon truth, typed state, typed
  operation?
- Does Unity code read domain state instead of protocol plumbing?
- Is SoA reserved for hot paths?
- Did the verifier protect the clean path instead of the old wrappers?

If the answer is no, fix the access shape before adding more feature code.
