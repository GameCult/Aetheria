# Implementing Aetheria Features With CultMesh

Date: 2026-06-28

This guide describes the target developer experience for adding Aetheria state
with CultMesh. If the workflow grows into a chain of facades, sessions,
projectors, adapters, and surface builders, stop and simplify the state API.
That chain is heretek now: it means the feature is compensating for a missing
typed document, query, operation, pointer, native view, or generated handle.

The rule is blunt:

```text
small UI-only value: derive it inline from already accessible typed state
named UI-only projection: define the projected document, then read it
simulation feature: define one canonical typed document, let daemon authority mutate it, consume the same document everywhere
hot rendering/physics: use SoA, not object-graph UI documents
```

CultMesh owns document identity, sync, prediction, reconciliation, and reactive
access. Aetheria code should read like it is using game state, not walking
protocol layers.

## Canonical Document Rule

The default shape is one shared typed document contract.

```text
AetheriaRuntimeFooDocument
  -> daemon authority mutates/publishes it
  -> Unity/Electron/tools read the same document type
  -> authorized clients write intent or predictions through the managed handle
```

Do not split a feature into "daemon truth" and a separate "CultMesh typed
state" unless the second shape is intentionally different. The assembly is
shared, so the document the daemon owns is also the document clients receive.
Across runtimes, callers should feel like they are grabbing a handle on typed
state and either reading it for display or writing to it for client input.

A second document is justified only when it earns a distinct job:

- hidden-information filtering;
- expensive or shared derived state;
- viewport/windowed selection;
- SoA/native render or physics layout;
- lossy presentation summaries;
- UI surface documents;
- compatibility mirrors with a named removal stage.

Projection is not the normal way to make state client-visible. Projection is an
exception for a different shape.

## The Ergonomic Bar

UI-only state should take one or two steps.

Use one step when the value is small and already derivable from state the caller
has:

```csharp
var currentDocking = client.State.Reactive<AetheriaRuntimeCurrentDockingDocument>();
var bayName = currentDocking.Current?.DockingBayName ?? "";
```

Use two steps when the projection is shared, non-trivial, or should have a
stable schema:

```text
1. Define the typed projected document.
2. Read it from Unity as a managed reactive typed document.
```

Simulation state should take two or three steps:

```text
1. Define the canonical typed document in the shared runtime package.
2. Let the daemon mutate/publish that document as the authority for the Verse.
3. Read or modify the same managed document from clients according to authority policy.
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
game. At most, one boundary adapter may exist at an actual boundary: Unity
GameObject presentation, Eve/CultUI lowering, legacy import, persistence, or
native view ownership. If two or more layers appear in a row just to obtain one
domain value, stop and move the missing primitive into CultMesh/CultLib or the
generated Aetheria handle surface.

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

Use a canonical simulation document when:

- the value affects game rules;
- input can mutate it;
- it needs authority, replay, persistence, or reconciliation;
- the daemon must validate operations before publishing the next state.

Use a projected typed document when the caller needs a different state shape,
not merely because a client needs to see daemon-owned state.

Use SoA/native views when:

- Unity jobs, Burst, Ymir, or rendering need large columnar data;
- row count and per-frame access make object graphs the wrong format;
- the data is a hot view over canonical simulation state rather than a UI document.

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
            _docking ??= _client.State
                .Reactive<AetheriaRuntimeCurrentDockingDocument>();
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

Before adding this projection, ask whether the UI can read the canonical
simulation document directly. If it can, do that. The projection exists only
when the UI shape is genuinely derived, filtered, windowed, or shared enough to
deserve its own schema.

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

Then make it available through generic managed state access:

```csharp
using var status =
    client.State.Reactive<AetheriaRuntimeZoneDefenseStatusDocument>();
```

The exact registration/publishing code belongs in the runtime plumbing. It
should be generated or centralized. The feature author should not have to write
five separate access classes, wrapper methods, sessions, or facades for one
projected state shape.

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
        _status ??= _client.State
            .Reactive<AetheriaRuntimeZoneDefenseStatusDocument>();
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

### Step 1: Define The Canonical Document

Simulation truth is the shared typed document. The daemon owns authority over
that document, but the document contract is not daemon-private.

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

### Step 2: Let The Daemon Own Authority

The daemon validates operations, applies ticks, and publishes the canonical
document into the Verse. Unity does not own gameplay truth, but Unity does not
need a separate client-facing copy either. CultMesh handles sync. If the client
has prediction authority, local changes to the managed document are
predictions; reconciliation corrects them without requiring feature code to
manually shuttle deltas.

### Step 3: Read Or Mutate Through The Managed Handle

Unity reads the same document through the managed typed handle:

```csharp
var support = client.State.Reactive<AetheriaRuntimeStationSupportDocument>();
var cooling = support.Current?.CoolingReserve ?? 0;
```

When the client does not have direct simulation authority, it submits a typed
operation:

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

When the client has prediction or simulation authority, the ergonomic target is
even simpler: modifying the managed reactive document records a prediction,
debounced by the update frame, and CultMesh routes/reconciles it according to
the Verse authority policy.

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

SoA should be a named high-performance view over canonical simulation state. It
should not force ordinary presentation code to understand frame slabs, column
handles, or native buffer ownership.

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
- Unity caller reads the typed document directly.

For simulation features, test:

- daemon tick/application mutates the canonical typed document;
- typed operation validation accepts and rejects correctly;
- clients read the same shared document type the daemon publishes;
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
Unity caller uses client.State.Reactive<TDocument>()
Unity caller submits typed operations for mutations
authorized caller mutates managed typed state as prediction/input
hot renderer uses named SoA view
```

Bad verifier expectations:

```text
Unity caller owns AetheriaRuntime*Session for one document
Unity caller calls ObserveFeatureName() wrapper only to read Current
UI caller reads raw daemon frames and joins rows
feature adds facade/projector/adapter/surface-builder chain
feature introduces multiple translation layers to get one typed value
```

The verifier should make the codebase harder to regress into ceremony, not
freeze the ceremony in place.

## Final Check

Before calling a CultMesh feature ergonomic, answer these questions:

- Can a UI-only inline value be implemented in one local derivation?
- Can a shared UI projection be implemented by defining the document and reading
  it from Unity?
- Can a simulation feature be explained as one canonical typed document plus
  authority policy and typed operations?
- Does Unity code read domain state instead of protocol plumbing?
- Is SoA reserved for hot paths?
- Did the verifier protect the clean path instead of the old wrappers?

If the answer is no, fix the access shape before adding more feature code.
